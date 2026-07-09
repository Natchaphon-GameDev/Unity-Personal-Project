// TaskbarHeroMac.m — Cocoa counterpart of the Win32 layer (Win32.cs) for the macOS
// player. Exposes the same primitives OverlayWindow/TaskbarHotkeys need, speaking the
// same contract as the Windows path: global TOP-LEFT-origin coordinates. Retina support
// is disabled in PlayerSettings, so Unity pixels == Cocoa points and no scaling is done.
//
// Lives outside Assets/ so Unity doesn't try to import the source; only the compiled
// dylib ships. Rebuild (from the repo root):
//
//   clang -dynamiclib -fobjc-arc -mmacosx-version-min=11.0 \
//     -arch arm64 -arch x86_64 \
//     -framework Cocoa -framework Carbon \
//     -o "Personal Projects/Assets/Plugins/macOS/TaskbarHeroMac.dylib" \
//     "Personal Projects/NativePlugins/TaskbarHeroMac.m"

#import <Cocoa/Cocoa.h>
#import <Carbon/Carbon.h>
#import <math.h>
#import <stdbool.h>

static NSWindow *tbh_window = nil;
static id<NSObject> tbh_activityToken = nil;

static EventHotKeyRef tbh_quitHotkeyRef = NULL;
static EventHandlerRef tbh_quitHandlerRef = NULL;
static volatile int tbh_quitHotkeyFired = 0;

// ---- coordinate helpers -----------------------------------------------------
// Cocoa's global space is bottom-left-origin; the shared C# side speaks
// top-left-origin (Win32 convention). Flip against the primary screen's top edge.

static CGFloat tbh_PrimaryMaxY(void)
{
    return NSMaxY(NSScreen.screens.firstObject.frame);
}

static NSRect tbh_ToTopLeft(NSRect r)
{
    r.origin.y = tbh_PrimaryMaxY() - NSMaxY(r);
    return r;
}

static NSRect tbh_FromTopLeft(int x, int y, int w, int h)
{
    return NSMakeRect(x, tbh_PrimaryMaxY() - y - h, w, h);
}

static void tbh_WriteRect(NSRect r, int *left, int *top, int *right, int *bottom)
{
    *left = (int)lround(NSMinX(r));
    *top = (int)lround(NSMinY(r));
    *right = (int)lround(NSMaxX(r));
    *bottom = (int)lround(NSMaxY(r));
}

// ---- window -----------------------------------------------------------------

static void tbh_ApplyTransparency(void)
{
    tbh_window.opaque = NO;
    tbh_window.backgroundColor = NSColor.clearColor;
    tbh_window.hasShadow = NO;

    NSView *view = tbh_window.contentView;
    view.wantsLayer = YES;
    view.layer.opaque = NO;
}

bool TBH_Initialize(void)
{
    NSApplication *app = NSApplication.sharedApplication;
    tbh_window = app.mainWindow ?: app.windows.firstObject;
    if (tbh_window == nil)
        return false;

    tbh_window.styleMask = NSWindowStyleMaskBorderless;
    tbh_ApplyTransparency();
    tbh_window.collectionBehavior |= NSWindowCollectionBehaviorCanJoinAllSpaces;

    // Opt out of App Nap so the idle sim keeps ticking while never focused
    // (AllowingIdleSystemSleep: don't keep the whole Mac awake).
    if (tbh_activityToken == nil)
        tbh_activityToken = [NSProcessInfo.processInfo
            beginActivityWithOptions:NSActivityUserInitiatedAllowingIdleSystemSleep
                          reason:@"TaskbarHero overlay simulation"];
    return true;
}

void TBH_SetClickThrough(bool enabled)
{
    tbh_window.ignoresMouseEvents = enabled ? YES : NO;
}

void TBH_SetTopMost(bool topMost)
{
    tbh_window.level = topMost ? NSFloatingWindowLevel : NSNormalWindowLevel;
}

bool TBH_GetWindowRect(int *left, int *top, int *right, int *bottom)
{
    if (tbh_window == nil)
        return false;
    tbh_WriteRect(tbh_ToTopLeft(tbh_window.frame), left, top, right, bottom);
    return true;
}

void TBH_SetFrame(int x, int y, int width, int height)
{
    if (tbh_window == nil)
        return;

    NSRect f = tbh_FromTopLeft(x, y, width, height);
    if (NSEqualSizes(f.size, tbh_window.frame.size))
    {
        [tbh_window setFrameOrigin:f.origin];
        return;
    }

    [tbh_window setFrame:f display:YES];
    tbh_ApplyTransparency(); // resizing can revert the surface to opaque black (known Unity/macOS issue)
}

// ---- screens ----------------------------------------------------------------

/// Bounds (full frame, or visibleFrame when workArea — excludes menu bar + Dock) of
/// the screen nearest the center of the given top-left-origin rect. Mirrors
/// MonitorFromPoint(MONITOR_DEFAULTTONEAREST) + GetMonitorInfo.
bool TBH_GetMonitorBounds(int x, int y, int width, int height, bool workArea,
                          int *left, int *top, int *right, int *bottom)
{
    if (NSScreen.screens.count == 0)
        return false;

    NSRect probe = tbh_FromTopLeft(x, y, width, height);
    NSPoint center = NSMakePoint(NSMidX(probe), NSMidY(probe));

    NSScreen *best = NSScreen.screens.firstObject;
    CGFloat bestDist = CGFLOAT_MAX;
    for (NSScreen *s in NSScreen.screens)
    {
        if (NSPointInRect(center, s.frame)) { best = s; break; }
        CGFloat dx = MAX(NSMinX(s.frame) - center.x, MAX(center.x - NSMaxX(s.frame), 0));
        CGFloat dy = MAX(NSMinY(s.frame) - center.y, MAX(center.y - NSMaxY(s.frame), 0));
        CGFloat d = dx * dx + dy * dy;
        if (d < bestDist) { bestDist = d; best = s; }
    }

    tbh_WriteRect(tbh_ToTopLeft(workArea ? best.visibleFrame : best.frame),
                  left, top, right, bottom);
    return true;
}

bool TBH_GetPrimaryWorkArea(int *left, int *top, int *right, int *bottom)
{
    NSScreen *primary = NSScreen.screens.firstObject;
    if (primary == nil)
        return false;
    tbh_WriteRect(tbh_ToTopLeft(primary.visibleFrame), left, top, right, bottom);
    return true;
}

// ---- input ------------------------------------------------------------------

bool TBH_GetCursorPos(int *x, int *y)
{
    NSPoint p = NSEvent.mouseLocation;
    *x = (int)lround(p.x);
    *y = (int)lround(tbh_PrimaryMaxY() - p.y);
    return true;
}

bool TBH_IsLeftMouseDown(void)
{
    return (NSEvent.pressedMouseButtons & 1) != 0;
}

// ---- global quit hotkey (Ctrl+Alt+Q -> Control+Option+Q) ---------------------
// Carbon RegisterEventHotKey still works fine on modern macOS, needs no
// permission prompts, and fires even though the overlay is never the key window.
// Delivered on the main run loop, so no background thread (unlike the Win32 path).

static OSStatus tbh_HotkeyHandler(EventHandlerCallRef next, EventRef event, void *userData)
{
    tbh_quitHotkeyFired = 1;
    return noErr;
}

bool TBH_RegisterQuitHotkey(void)
{
    EventTypeSpec spec = { kEventClassKeyboard, kEventHotKeyPressed };
    InstallApplicationEventHandler(&tbh_HotkeyHandler, 1, &spec, NULL, &tbh_quitHandlerRef);

    EventHotKeyID hotkeyId = { 'TBHQ', 1 };
    OSStatus err = RegisterEventHotKey(kVK_ANSI_Q, controlKey | optionKey, hotkeyId,
                                       GetApplicationEventTarget(), 0, &tbh_quitHotkeyRef);
    return err == noErr;
}

void TBH_UnregisterQuitHotkey(void)
{
    if (tbh_quitHotkeyRef != NULL) { UnregisterEventHotKey(tbh_quitHotkeyRef); tbh_quitHotkeyRef = NULL; }
    if (tbh_quitHandlerRef != NULL) { RemoveEventHandler(tbh_quitHandlerRef); tbh_quitHandlerRef = NULL; }
}

bool TBH_ConsumeQuitHotkey(void)
{
    if (!tbh_quitHotkeyFired)
        return false;
    tbh_quitHotkeyFired = 0;
    return true;
}
