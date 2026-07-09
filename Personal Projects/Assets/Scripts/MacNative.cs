using System.Runtime.InteropServices;

namespace TaskbarHero
{
    /// <summary>
    /// P/Invoke surface of TaskbarHeroMac.dylib (Assets/Plugins/macOS), the Cocoa
    /// counterpart of <see cref="Win32"/>. All coordinates are global top-left-origin,
    /// matching the Win32 convention; retina support is disabled in PlayerSettings so
    /// Unity pixels == Cocoa points. Source + rebuild command live in
    /// NativePlugins/TaskbarHeroMac.m (outside Assets so Unity doesn't import it).
    /// Native bools are 1 byte (C99 _Bool), hence the U1 marshaling.
    /// </summary>
    internal static class MacNative
    {
        const string Lib = "TaskbarHeroMac";

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_Initialize();

        [DllImport(Lib)]
        public static extern void TBH_SetClickThrough([MarshalAs(UnmanagedType.U1)] bool enabled);

        [DllImport(Lib)]
        public static extern void TBH_SetTopMost([MarshalAs(UnmanagedType.U1)] bool topMost);

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_GetWindowRect(out int left, out int top, out int right, out int bottom);

        [DllImport(Lib)]
        public static extern void TBH_SetFrame(int x, int y, int width, int height);

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_GetMonitorBounds(
            int x, int y, int width, int height, [MarshalAs(UnmanagedType.U1)] bool workArea,
            out int left, out int top, out int right, out int bottom);

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_GetPrimaryWorkArea(out int left, out int top, out int right, out int bottom);

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_GetCursorPos(out int x, out int y);

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_IsLeftMouseDown();

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_RegisterQuitHotkey();

        [DllImport(Lib)]
        public static extern void TBH_UnregisterQuitHotkey();

        [DllImport(Lib)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool TBH_ConsumeQuitHotkey();
    }
}
