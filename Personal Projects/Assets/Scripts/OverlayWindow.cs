using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace TaskbarHero
{
    /// <summary>
    /// Turns the Windows player window into a free-floating, borderless, transparent
    /// overlay: content-sized, no taskbar button, optionally always-on-top, and able to
    /// toggle click-through so clicks on empty (transparent) pixels fall through to the
    /// desktop while clicks on the game are captured. Movement/hit-testing are driven by
    /// <see cref="WindowDragController"/> and <see cref="ClickThroughController"/>.
    ///
    /// Every method is a safe no-op in the editor and on non-Windows platforms, and all
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
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW);

            IsClickThrough = false;
            IsReady = true;
#endif
        }

        /// <summary>Resize, keeping the current top-left corner.</summary>
        public void SetSize(int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;
            Win32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width, height,
                Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
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
#endif
        }

        public void SetTopMost(bool topMost)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;
            Win32.SetWindowPos(hwnd, topMost ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
                0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
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
#endif
        }

        public void MoveTo(int x, int y)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!IsReady) return;
            Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
#endif
        }

        public bool TryGetCursorPosition(out int screenX, out int screenY)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Win32.GetCursorPos(out var p)) { screenX = p.X; screenY = p.Y; return true; }
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
#endif
            left = top = right = bottom = 0;
            return false;
        }

        /// <summary>True while the physical left mouse button is held (drag release authority on Windows).</summary>
        public bool IsLeftMouseDown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
#else
            return false;
#endif
        }
    }
}
