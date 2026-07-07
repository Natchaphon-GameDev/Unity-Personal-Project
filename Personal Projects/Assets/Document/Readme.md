# Taskbar Hero — floating desktop-overlay idle RPG

This branch (`feature/taskbar-hero`) practices building a Unity game as a **free-floating,
draggable, transparent desktop widget** (Win32 via P/Invoke) wrapped around a small **idle
RPG** with saving, offline progress, loot, bosses, and click-to-attack. The window is not
pinned to the taskbar — it starts top-center of the screen and can be dragged anywhere.

Everything Windows-specific is behind `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR`, so the
project stays fully workable in the editor on macOS (the Win32 layer no-ops there).

## The overlay window

`Assets/Scripts/OverlayWindow.cs` turns the Unity player window into the widget, driven by
raw Win32 calls in `Assets/Scripts/Win32.cs`:

1. `SetWindowLong(GWL_STYLE, WS_POPUP | WS_VISIBLE)` — borderless.
2. `DwmExtendFrameIntoClientArea(margins = -1)` — the "sheet of glass" trick: pixels the
   camera clears to alpha 0 become truly transparent so the desktop shows through. We never
   call `SetLayeredWindowAttributes` (it would make Unity's DirectX window opaque).
3. `WS_EX_LAYERED | WS_EX_TOOLWINDOW` — layered (for DWM) and hidden from Alt-Tab. Note it
   starts **without** `WS_EX_TRANSPARENT`: the settings panel is fully interactive.
4. `PositionTopCenterPrimary` — `MonitorFromPoint` + `GetMonitorInfo` place the window
   centered near the top of the primary monitor's work area on launch.
5. `SetTopMost(bool)` — `HWND_TOPMOST` / `HWND_NOTOPMOST` for the always-on-top toggle.

### Dragging (`WindowDragController` + `DragHandle`)

The widget body is a semi-transparent world-space sprite (drawn behind the hero/monster so
they stay crisp) with a 2D collider and a `DragHandle`. Pressing empty body area starts a
drag; each frame the window moves by the raw cursor delta (`GetCursorPos`). We use a manual
delta rather than Windows' `HTCAPTION` modal move loop so the game keeps rendering during a
drag; the physical button state (`GetAsyncKeyState(VK_LBUTTON)`) is the release authority,
since a fast drag can outrun the window and lose the mouse-up.

### Dynamic click-through (`ClickThroughController`)

Each frame in gameplay: poll the cursor (a click-through window gets no Unity input, so we
must use `GetCursorPos`), convert to Unity screen space, and `EventSystem.RaycastAll` — one
call covers UI (GraphicRaycaster) and the monster (Physics2DRaycaster). Over content →
window interactive; over empty/transparent area → `WS_EX_TRANSPARENT` on so clicks fall
through to the desktop. Toggling on hover (before the click) means no first-click loss. The
component is disabled in settings mode (whole window interactive).

## App flow & settings

`AppFlow` is the mode state machine. On boot it configures the window from saved settings,
then shows the **settings panel on first run only** (afterwards reachable via the HUD gear):

- **Master Volume** → `AudioListener.volume`
- **Always on Top** → `OverlayWindow.SetTopMost`
- **Launch at Windows Startup** → HKCU `...\CurrentVersion\Run` entry, written via
  `advapi32` P/Invoke in `StartupRegistry` (the project targets .NET Standard 2.1, which
  has no `Microsoft.Win32.Registry`, so we call the API directly).
- **Window Size** 1x/2x/3x → resizes the window and the canvas scale.
- **Next / Back** → persists settings and enters gameplay.

Settings persist to `PlayerPrefs` (`AppSettings`); they're small scalars needed before the
save file loads. The window is 520×420 in settings mode and a 480×160 strip (×scale) in
gameplay; `AppFlow` resizes between modes via `SetWindowPos`.

## The idle RPG

Pure-C# sim (no Unity types), so it's deterministic and unit-testable outside play mode:

- `IdleRpg.cs` — the simulation: auto-attack cadence, kills grant gold+XP, XP levels up
  (raising attack). Emits events (`OnKill`, `OnLevelUp`, `OnLoot`, boss events) the view and
  audio subscribe to. `CaptureState`/restore ctor snapshot progress; derived values
  (attack, monster HP) are recomputed on load so they can't drift.
  - **Loot & equipment** (`EquipmentItem`, `LootTable`): kills can drop items (slot, rarity,
    a % attack or gold-find bonus); better items auto-equip, others auto-sell. Deterministic
    given the RNG seed.
  - **Bosses** every N stages: multiplied HP + a kill timer; win = big gold, timeout = drop
    back a stage and farm until strong enough (then the boss re-triggers).
  - **Tap-to-attack** (`TapStrike`): clicking the monster deals bonus damage on a cooldown.
  - **Offline progress** (`FastForward`): on load, elapsed real time (capped, default 8h) is
    simulated and reported as a "while you were away" toast.
- `IdleRpgRules.cs` / `IdleRpgConfig.cs` — plain tuning values + a ScriptableObject mirror.
  New feature fields default to **feature-neutral** values in the rules (drop chance 0,
  bosses off) so the base sim tests stay unchanged; real gameplay values live on the asset.
- `IdleRpgView.cs` — builds visuals in code (body card, tinted hero/monster, HP bar) and
  renders the sim GameController hands it.
- `GameController.cs` — owns the sim lifecycle: load save → restore → offline fast-forward →
  drive the tick → autosave every 30s and on quit.
- `SaveData` / `SaveSystem` — a single versioned JSON at `persistentDataPath/save.json`,
  written atomically (temp + replace); a bad/newer file is set aside as `.bak` and the game
  starts fresh rather than crash-loop.

Sounds are synthesized (no audio assets): `SfxSynth` builds waveforms, `SfxPlayer` wraps
them in AudioClips and plays them on sim events; the volume slider controls them via
`AudioListener.volume`.

## Setup / regeneration

Run **Tools > Taskbar Hero > Set Up Environment** (idempotent) — it rebuilds the whole
scene (camera + Physics2DRaycaster, EventSystem with `InputSystemUIInputModule`, overlay
services, game controller, and the full HUD + settings canvas, all cross-wired), applies
player settings, and sets the scene as the only build scene.

| Setting | Value | Why |
|---|---|---|
| Product Name | TaskbarHero | registry/exe/prefs name |
| Run / Visible In Background | on | the overlay is rarely the focused window |
| Fullscreen Mode | Windowed | the window must be repositionable |
| Resizable Window | off | borderless; size is driven by `SetWindowPos` |
| Default window size | 520×420 | opens at the settings size (less resize flash) |
| Use Flip Model Swapchain | off | flip-model breaks the DWM transparency trick |
| URP asset HDR | off | HDR backbuffer has no usable alpha → black, not transparent |

## Quit hotkey

The window can be click-through and unfocused, so `TaskbarHotkeys.cs` registers a global
**Ctrl+Alt+Q** quit via Win32 `RegisterHotKey` on a dedicated background thread (Unity owns
the main window's message pump). The HUD **X** button also quits.

## Testing

Sim/persistence/audio logic is covered by edit-mode NUnit tests under `Assets/Tests/`
(`IdleRpgTests`, `LootTests`, `BossTests`, `TapStrikeTests`, `OfflineTests`,
`StateRoundTripTests`, `SaveDataTests`, `SfxSynthTests`) — run them from
`Window > General > Test Runner`. The window/Win32 layer can only be verified on Windows.

### Windows-PC checklist

1. Build (`File > Build Profiles > Windows > Build`) and run the `.exe` on Windows.
2. Window appears borderless, **top-center** of the primary monitor, desktop visible through
   the transparent area; no taskbar button, not in Alt-Tab. A black rectangle instead of
   transparency means HDR or flip-model got re-enabled — re-check the table above.
3. **First run:** settings panel shows (whole window clickable); the volume slider audibly
   changes SFX; **Next** shrinks the window to the strip and gameplay starts.
4. **Drag** the strip across both monitors — the game keeps animating; releasing the button
   outside the window still ends the drag.
5. **Click-through:** clicks on empty/transparent area select desktop icons underneath;
   clicks on the body/monster/buttons are captured; crossing the boundary flips cleanly with
   no stuck states.
6. **Tap** the monster for bonus damage; **gear** reopens settings over gameplay; **X** and
   **Ctrl+Alt+Q** quit.
7. **Always on Top** toggle covers/uncovers; **1x/2x/3x** scales window + content together
   with crisp text.
8. **Launch at Startup** on → `HKCU\...\Run\TaskbarHero` exists (regedit) with the quoted
   exe path; sign out/in → app auto-starts; off → value gone.
9. **Save/offline:** quit, relaunch → progress persists; backdate `savedAtUnixUtc` in
   `%USERPROFILE%\AppData\LocalLow\DefaultCompany\TaskbarHero\save.json` → capped offline
   toast; corrupt the JSON → app starts fresh leaving a `.bak`.
10. **Bosses** at stage 10/20/… (timer, fail→farm→retry), **loot feed** updates on drops.
11. **150% DPI:** verify the click-through hit-test still lines up with the visuals.

## Next steps

- Hero / inventory panel UI backed by the equipment already in the save.
- Real art / animation instead of tinted squares.
- Persist window position (currently re-centers top-center each launch).
