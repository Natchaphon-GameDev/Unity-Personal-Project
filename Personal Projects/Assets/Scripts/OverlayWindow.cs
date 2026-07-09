using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace TaskbarHero
{
    /// <summary>
    /// Turns the player window into a free-floating, borderless, transparent
    /// overlay: content-sized, optionally always-on-top, and able to
    /// toggle click-through so clicks on empty (transparent) pixels fall through to the
    /// desktop while clicks on the game are captured. Movement/hit-testing are driven by
    /// <see cref="WindowDragController"/> and <see cref="ClickThroughController"/>.
    ///
    /// Windows goes through <see cref="Win32"/>; macOS through <see cref="MacNative"/>
    /// (TaskbarHeroMac.dylib), which speaks the same top-left-origin coordinate contract.
    /// Every method is a safe no-op in the editor and on other platforms, and all
    /// the coordinate/query helpers return false there, so callers never need to branch.
    ///
    /// DWM transparency invariants (kept from the taskbar version): the window is layered
    /// but we never call SetLayeredWindowAttributes; the camera clears to alpha 0; the
    /// flip-model swapchain and URP HDR are off.
    /// </summary>
    public sealed class OverlayWindow : MonoBehaviour
    {
        public static OverlayWindow Instance { get; private set; }

        public bool IsClickThrough { get; private set; }
        public bool IsReady { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hwnd;
#endif

        void Awake() => Instance = this;

        /// <summary>Make the window borderless + transparent + layered. Interactive (not click-through) to start.</summary>
        public void Initialize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            hwnd = Win32.GetActiveWindow();

            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, Win32.WS_POPUP | Win32.WS_VISIBLE);

            var margins = new Win32.MARGINS { LeftWidth = -1 };
            Win32.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            uint ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_LAYERED);

            IsClickThrough = false;
            IsReady = true;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            IsClickThrough = false;
            IsReady = MacNative.TBH_Initialize();
#endif
        }

        /// <summary>
        /// Resize, keeping the current top-left corner where possible. If the new size would
        /// push the window past the edge of its monitor (e.g. a taller Settings panel opening
        /// while the strip sits near the bottom of the screen), the window is nudged back
        /// on-screen instead.
        /// </summary>
        public void SetSize(int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;

            int x = 0, y = 0;
            bool hasPos = Win32.GetWindowRect(hwnd, out var current);
            if (hasPos)
            {
                x = current.Left;
                y = current.Top;
                ClampToMonitor(ref x, ref y, width, height, MonitorNearestRect(x, y, width, height));
            }

            uint flags = Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED;
            if (!hasPos)
                flags |= Win32.SWP_NOMOVE;
            Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, flags);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!IsReady || !MacNative.TBH_GetWindowRect(out int x, out int y, out _, out _))
                return;
            ClampToMonitor(ref x, ref y, width, height);
            MacNative.TBH_SetFrame(x, y, width, height);
#endif
        }

        /// <summary>Center the window horizontally near the top of the primary monitor's work area.</summary>
        public void PositionTopCenterPrimary(int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;

            var origin = new Win32.POINT { X = 0, Y = 0 };
            IntPtr monitor = Win32.MonitorFromPoint(origin, Win32.MONITOR_DEFAULTTOPRIMARY);
            var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Win32.MONITORINFO)) };

            int x = 100, y = 8;
            if (Win32.GetMonitorInfo(monitor, ref info))
            {
                int workWidth = info.rcWork.Right - info.rcWork.Left;
                x = info.rcWork.Left + Mathf.Max(0, (workWidth - width) / 2);
                y = info.rcWork.Top + 8;
            }

            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x, y, width, height,
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_FRAMECHANGED);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!IsReady) return;

            int x = 100, y = 8;
            if (MacNative.TBH_GetPrimaryWorkArea(out int left, out int top, out int right, out _))
            {
                x = left + Mathf.Max(0, (right - left - width) / 2);
                y = top + 8;
            }

            MacNative.TBH_SetFrame(x, y, width, height);
            MacNative.TBH_SetTopMost(true); // matches the HWND_TOPMOST in the Windows placement
#endif
        }

        public void SetTopMost(bool topMost)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;
            Win32.SetWindowPos(hwnd, topMost ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
                0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!IsReady) return;
            MacNative.TBH_SetTopMost(topMost);
#endif
        }

        /// <summary>Toggle whether mouse input passes through the window. Only writes when the state changes.</summary>
        public void SetClickThrough(bool enabled)
        {
            if (enabled == IsClickThrough)
                return;
            IsClickThrough = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;
            uint ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            ex = enabled ? (ex | Win32.WS_EX_TRANSPARENT) : (ex & ~Win32.WS_EX_TRANSPARENT);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!IsReady) return;
            MacNative.TBH_SetClickThrough(enabled);
#endif
        }

        /// <summary>
        /// Move to (x, y), clamped so the window stays fully within its monitor's full bounds
        /// (including the taskbar/Dock strip) rather than the work area, so the overlay can be
        /// dragged over/onto the taskbar instead of being blocked at its edge.
        /// </summary>
        public void MoveTo(int x, int y)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;

            if (Win32.GetWindowRect(hwnd, out var current))
            {
                int width = current.Right - current.Left;
                int height = current.Bottom - current.Top;
                // Pick the monitor under the *destination* rect, not the window's current
                // (stale, pre-move) rect. Using the current rect pins the window to whichever
                // monitor it already overlaps, so it can never cross onto an extended monitor
                // since any x/y that would land it there gets clamped straight back.
                ClampToMonitor(ref x, ref y, width, height, MonitorNearestRect(x, y, width, height));
            }

            Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!IsReady || !MacNative.TBH_GetWindowRect(out int left, out int top, out int right, out int bottom))
                return;

            int width = right - left;
            int height = bottom - top;
            // Same destination-rect rule as Windows: clamp against the monitor under
            // where the window is headed, so it can cross onto an extended monitor.
            ClampToMonitor(ref x, ref y, width, height);
            MacNative.TBH_SetFrame(x, y, width, height);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        /// <summary>Finds the monitor nearest the center of the given (proposed) rect.</summary>
        IntPtr MonitorNearestRect(int x, int y, int width, int height)
        {
            var center = new Win32.POINT { X = x + width / 2, Y = y + height / 2 };
            return Win32.MonitorFromPoint(center, Win32.MONITOR_DEFAULTTONEAREST);
        }

        /// <summary>
        /// Nudges (x, y) so a window of the given size stays fully within the given monitor's
        /// full bounds (screen pixels, including the taskbar). No-op (leaves x, y unchanged)
        /// if the monitor info can't be read.
        /// </summary>
        void ClampToMonitor(ref int x, ref int y, int width, int height, IntPtr monitor)
        {
            var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Win32.MONITORINFO)) };
            if (!Win32.GetMonitorInfo(monitor, ref info))
                return;

            int minX = info.rcMonitor.Left;
            int maxX = Mathf.Max(minX, info.rcMonitor.Right - width);
            int minY = info.rcMonitor.Top;
            int maxY = Mathf.Max(minY, info.rcMonitor.Bottom - height);

            x = Mathf.Clamp(x, minX, maxX);
            y = Mathf.Clamp(y, minY, maxY);
        }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        /// <summary>
        /// Mac counterpart of the Win32 clamp: nudges (x, y) so the window stays fully
        /// within the full bounds (including the Dock strip) of the monitor nearest the
        /// destination rect. No-op if screen info can't be read.
        /// </summary>
        void ClampToMonitor(ref int x, ref int y, int width, int height)
        {
            if (!MacNative.TBH_GetMonitorBounds(x, y, width, height, workArea: false,
                    out int left, out int top, out int right, out int bottom))
                return;

            x = Mathf.Clamp(x, left, Mathf.Max(left, right - width));
            y = Mathf.Clamp(y, top, Mathf.Max(top, bottom - height));
        }
#endif

        public bool TryGetCursorPosition(out int screenX, out int screenY)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Win32.GetCursorPos(out var p)) { screenX = p.X; screenY = p.Y; return true; }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (MacNative.TBH_GetCursorPos(out screenX, out screenY)) return true;
#endif
            screenX = 0; screenY = 0;
            return false;
        }

        public bool TryGetWindowRect(out int left, out int top, out int right, out int bottom)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsReady && Win32.GetWindowRect(hwnd, out var r))
            {
                left = r.Left; top = r.Top; right = r.Right; bottom = r.Bottom;
                return true;
            }
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (IsReady && MacNative.TBH_GetWindowRect(out left, out top, out right, out bottom))
                return true;
#endif
            left = top = right = bottom = 0;
            return false;
        }

        /// <summary>True while the physical left mouse button is held (drag release authority in the player).</summary>
        public bool IsLeftMouseDown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return MacNative.TBH_IsLeftMouseDown();
#else
            return false;
#endif
        }
    }
}
