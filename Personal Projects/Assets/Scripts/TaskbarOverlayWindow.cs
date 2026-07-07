using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Turns the Windows player window into a borderless, transparent,
    /// always-on-top overlay positioned exactly over the taskbar.
    /// No-op in the editor and on non-Windows platforms, so the project
    /// stays fully workable on macOS.
    /// </summary>
    public class TaskbarOverlayWindow : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        void Start()
        {
            var hwnd = Win32.GetActiveWindow();

            var taskbar = Win32.FindWindow("Shell_TrayWnd", null);
            if (taskbar == System.IntPtr.Zero || !Win32.GetWindowRect(taskbar, out var rect))
            {
                Debug.LogError("TaskbarOverlayWindow: taskbar not found, window left unchanged.");
                return;
            }

            // Strip the title bar and border.
            Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, Win32.WS_POPUP | Win32.WS_VISIBLE);

            // "Sheet of glass": makes pixels the camera clears to alpha 0
            // actually transparent, so the taskbar shows through.
            var margins = new Win32.MARGINS { LeftWidth = -1 };
            Win32.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Click-through: let mouse input pass to the real taskbar underneath, and
            // hide the overlay from Alt-Tab. Setting WS_EX_LAYERED alone (without ever
            // calling SetLayeredWindowAttributes) keeps the DWM per-pixel transparency
            // intact while WS_EX_TRANSPARENT makes hit-testing fall through.
            var exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
                exStyle | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW);

            // Pin the window over the taskbar rect, always on top.
            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST,
                rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top,
                Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW | Win32.SWP_FRAMECHANGED);
        }
#endif
    }
}
