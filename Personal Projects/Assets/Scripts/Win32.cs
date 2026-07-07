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
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
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

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_FRAMECHANGED = 0x0020;

        // Free-floating overlay: drag + selective click-through + monitor placement.
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
        public const int VK_LBUTTON = 0x01;

        // Global hotkey modifiers / messages (used by TaskbarHotkeys).
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint WM_QUIT = 0x0012;
        public const uint WM_HOTKEY = 0x0312;

        public const uint VK_Q = 0x51;

        // Registry (HKCU Run key for "launch at startup"). Done via advapi32 P/Invoke
        // rather than Microsoft.Win32.Registry, which isn't in the player's .NET Standard
        // reference set.
        public static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        public const int KEY_QUERY_VALUE = 0x0001;
        public const int KEY_SET_VALUE = 0x0002;
        public const int REG_SZ = 1;
        public const int ERROR_SUCCESS = 0;

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
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetModuleFileName(IntPtr hModule, System.Text.StringBuilder filename, int size);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegCreateKeyEx(
            IntPtr hKey, string subKey, int reserved, string classType, int options,
            int samDesired, IntPtr securityAttributes, out IntPtr result, out int disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int options, int samDesired, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegSetValueEx(IntPtr hKey, string valueName, int reserved, int type, byte[] data, int cbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegQueryValueEx(IntPtr hKey, string valueName, int reserved, out int type, byte[] data, ref int cbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegDeleteValue(IntPtr hKey, string valueName);

        [DllImport("advapi32.dll")]
        public static extern int RegCloseKey(IntPtr hKey);
    }
}
