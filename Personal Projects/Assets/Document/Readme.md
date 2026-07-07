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
6. `SetWindowLong(GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW)` —
   click-through: mouse input falls through to the real taskbar, and the overlay is
   hidden from Alt-Tab. Setting `WS_EX_LAYERED` *without* ever calling
   `SetLayeredWindowAttributes` is the trick — it enables hit-test pass-through while
   DWM keeps the per-pixel transparency (calling it would make Unity's DirectX window
   opaque).

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

It also creates `Assets/Scenes/TaskbarHero.unity` (an orthographic camera centred on
the origin that clears to solid color with alpha 0, the `TaskbarOverlay` object, the
`IdleRpgGame` object, and the hidden sample visuals below), the `IdleRpgConfig` tuning
asset, sets the scene as the only build scene, and switches the active build target to
Windows.

## The idle RPG game

The overlay runs a tiny **idle RPG**: a hero auto-fights an endless line of monsters on
the taskbar strip, earning gold and XP and levelling up (which raises attack), while
each monster is a little tougher than the last. It is *pure idle* — no input, the hero
cannot lose; the only loop is time → power.

Following the repo convention, the game logic is a plain-C# class with no Unity
dependencies, so it is unit-testable outside play mode:

- `Assets/Scripts/IdleRpg.cs` — the simulation. `Tick(deltaSeconds)` advances the fight
  deterministically (no randomness): the hero strikes on a cadence, defeated monsters
  grant gold + XP and spawn a tougher one, accumulated XP levels the hero up.
- `Assets/Scripts/IdleRpgRules.cs` — plain tuning values + scaling formulas the sim
  reads (kept Unity-free so tests can build one directly).
- `Assets/Scripts/IdleRpgConfig.cs` — a `ScriptableObject` mirror of the rules so
  balance can be tweaked in the inspector (`Assets/Settings/IdleRpgConfig.asset`).
- `Assets/Scripts/IdleRpgView.cs` — the only MonoBehaviour: it builds its own minimal
  visuals in code (tinted squares for hero/monster, a monster HP bar, a
  `Lv / ATK / Gold / Stage` label) and drives the sim each frame. Runs in the editor on
  any platform.
- `Assets/Tests/IdleRpgTests.cs` — edit-mode NUnit tests over the deterministic sim.

## 2D vs 3D sample visuals (kept, hidden)

The scene's `SampleVisuals` object holds the earlier "proof of life" samples and a
`VisualSampleSwitcher`. The setup now leaves this object **disabled** so the idle RPG is
what shows; re-enable `SampleVisuals` in the inspector to compare the samples again:

- `Sample3D` — the spinning cube.
- `Sample2D` — an orange sprite (built-in `UISprite`) spinning around Z.

To switch: change the **Mode** dropdown on `SampleVisuals`, or press **Tab** at runtime
(the overlay must have keyboard focus — but it is now click-through, so Tab only works
in the editor / play mode).

## Testing (needs a Windows machine)

Win32 calls cannot run on macOS. Development happens here; testing happens on a Windows PC:

1. On the Mac: run the setup menu item, then **File > Build Profiles > Windows > Build**.
2. Copy the build folder to the Windows PC and run the `.exe`.
3. Success looks like: the hero fighting monsters on the taskbar (HP bar draining, the
   `Lv / ATK / Gold / Stage` numbers climbing), taskbar visible through the empty areas,
   the overlay staying on top and animating while other apps have focus, and taskbar
   clicks passing straight through the overlay.
4. If the overlay shows a **black bar** instead of transparency, re-check the settings
   table above (HDR and flip-model are the usual culprits).

Because the overlay is click-through and has no chrome, it can't be focused or clicked
to close. `Assets/Scripts/TaskbarHotkeys.cs` registers a global **Ctrl+Alt+Q** quit
hotkey via Win32 `RegisterHotKey`, running on a dedicated background thread with its own
`GetMessage` loop — `RegisterHotKey` posts `WM_HOTKEY` to the registering thread's
queue and Unity owns the main window's message pump, so a separate thread is needed.
(Task Manager still works as a fallback.)

## Next steps (not part of environment setup)

- Persist progress between runs (save gold/level/stage to disk) so the idle game
  actually idles across sessions.
- Real art / animation for the hero and monsters instead of tinted squares, plus a
  gold / level-up pop.
- Multi-monitor / auto-hide taskbar handling (re-query the rect when it moves).
- Conditional click-through (toggle `WS_EX_TRANSPARENT` off) if interactive elements
  are ever added.
