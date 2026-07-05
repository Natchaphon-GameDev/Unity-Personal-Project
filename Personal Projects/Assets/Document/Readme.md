# Taskbar Hero — Win32 overlay experiment

This branch (`feature/taskbar-hero`) practices one concept: **running a Unity game as a
transparent overlay on top of the Windows taskbar**, in the style of "Taskbar Hero",
using raw Win32 API calls via P/Invoke.

## How it works

A normal Unity Windows player is turned into a taskbar overlay at runtime by
`TaskbarOverlayWindow` (`Assets/Scripts/TaskbarOverlayWindow.cs`):

1. `GetActiveWindow()` — grab the Unity player's own window handle.
2. `FindWindow("Shell_TrayWnd", null)` + `GetWindowRect` — locate the taskbar and its screen rect.
3. `SetWindowLong(GWL_STYLE, WS_POPUP | WS_VISIBLE)` — strip the title bar and border.
4. `DwmExtendFrameIntoClientArea(margins = -1)` — the "sheet of glass" trick: every pixel
   the camera clears to alpha 0 becomes actually transparent, so the taskbar shows through.
5. `SetWindowPos(HWND_TOPMOST, taskbar rect)` — pin the window exactly over the taskbar.

All Win32 declarations live in `Assets/Scripts/Win32.cs`. The whole thing is wrapped in
`#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`, so the project stays fully workable in the
editor on macOS — the overlay only activates in a Windows player build.

## Settings this needs (applied by the setup menu item)

Run **Tools > Taskbar Hero > Set Up Environment** once (idempotent). It applies:

| Setting | Value | Why |
|---|---|---|
| Run In Background | on | the overlay is almost never the focused window |
| Visible In Background | on | keep rendering while unfocused |
| Fullscreen Mode | Windowed | the window must be repositionable |
| Resizable Window | off | fixed to the taskbar rect |
| Use Flip Model Swapchain | off | flip-model breaks the DWM transparency trick |
| URP asset HDR | off | HDR backbuffer has no usable alpha → black instead of transparent |

It also creates `Assets/Scenes/TaskbarHero.unity` (camera clears to solid color with
alpha 0, the `TaskbarOverlay` object, and the sample visuals below), sets it as the
only build scene, and switches the active build target to Windows.

## 2D vs 3D sample visuals

The scene's `SampleVisuals` object holds two "proof of life" samples and a
`VisualSampleSwitcher` that shows exactly one of them:

- `Sample3D` — the spinning cube.
- `Sample2D` — an orange sprite (built-in `UISprite`) spinning around Z.

To switch: change the **Mode** dropdown on `SampleVisuals` in the inspector, or press
**Tab** at runtime. In the taskbar build the overlay must have keyboard focus for Tab
to register — click the overlay once first.

## Testing (needs a Windows machine)

Win32 calls cannot run on macOS. Development happens here; testing happens on a Windows PC:

1. On the Mac: run the setup menu item, then **File > Build Profiles > Windows > Build**.
2. Copy the build folder to the Windows PC and run the `.exe`.
3. Success looks like: a rotating cube floating on the taskbar, taskbar visible through
   the empty areas, overlay staying on top and animating while other apps have focus.
4. If the overlay shows a **black bar** instead of transparency, re-check the settings
   table above (HDR and flip-model are the usual culprits).

To quit the overlay during testing, use Task Manager or Alt+F4 while it has focus —
there is no window chrome. (A proper quit key is an obvious next step.)

## Next steps (not part of environment setup)

- Note lanes / hit detection — as plain-C# logic classes with edit-mode tests,
  following the repo's logic-separate-from-MonoBehaviour convention.
- Keyboard input while unfocused (the window rarely has focus, so normal Unity input
  won't fire; needs `RegisterHotKey` or a low-level keyboard hook — more Win32 practice).
- Optional click-through (`WS_EX_LAYERED | WS_EX_TRANSPARENT`) so clicks pass to the
  real taskbar except over game elements.
- Multi-monitor / auto-hide taskbar handling (re-query the rect when it moves).
