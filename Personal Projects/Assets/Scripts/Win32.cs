using System;
using System.Runtime.InteropServices;

namespace TaskbarHero
{
    /// <summary>
    /// Raw Win32 declarations used to turn the Unity player window into a
    /// borderless, transparent, always-on-top overlay pinned over the taskbar.
    /// Names mirror the Win32 API so they can be looked up in Microsoft docs.
    /// </summary>
    internal static class Win32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int LeftWidth;
            public int RightWidth;
            public int TopHeight;
            public int BottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const uint WS_POPUP = 0x80000000;
        public const uint WS_VISIBLE = 0x10000000;

        // Click-through overlay styles. WS_EX_LAYERED | WS_EX_TRANSPARENT lets mouse
        // input fall through to the real taskbar; WS_EX_TOOLWINDOW keeps the overlay
        // out of Alt-Tab. NOTE: we deliberately never call SetLayeredWindowAttributes
        // — with Unity's DirectX swapchain that would turn the whole window opaque and
        // defeat the DWM per-pixel transparency.
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_EX_LAYERED = 0x00080000;

        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_FRAMECHANGED = 0x0020;

        // Global hotkey modifiers / messages (used by TaskbarHotkeys).
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint WM_QUIT = 0x0012;
        public const uint WM_HOTKEY = 0x0312;

        public const uint VK_Q = 0x51;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern uint SetWindowLong(IntPtr hWnd, int index, uint newValue);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int width, int height, uint flags);

        [DllImport("Dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern int GetMessage(out MSG message, IntPtr hWnd, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        public static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
    }
}
