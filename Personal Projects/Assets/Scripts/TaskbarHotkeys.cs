using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Threading;
#endif

namespace TaskbarHero
{
    /// <summary>
    /// Registers a global quit hotkey (Ctrl+Alt+Q) that works even though the overlay
    /// is click-through and almost never the focused window. Normal Unity input can't
    /// help here: with WS_EX_TRANSPARENT the window receives no keyboard focus, so we
    /// go through Win32 RegisterHotKey instead.
    ///
    /// RegisterHotKey delivers WM_HOTKEY to the message queue of the thread that
    /// registered it, and Unity owns the main window's message pump — so we run a
    /// dedicated background thread with its own GetMessage loop and hand the result
    /// back to the main thread via a flag. No-op in the editor / off Windows, so the
    /// project stays fully workable on macOS.
    /// </summary>
    public sealed class TaskbarHotkeys : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int HotkeyIdQuit = 1;

        Thread thread;
        uint threadId;
        volatile bool quitRequested;
        volatile bool running;

        void Start()
        {
            running = true;
            thread = new Thread(HotkeyLoop) { IsBackground = true, Name = "TaskbarHotkeys" };
            thread.Start();
        }

        void Update()
        {
            // Application.Quit must be called from the main thread.
            if (quitRequested)
                Application.Quit();
        }

        void OnDestroy()
        {
            running = false;
            if (threadId != 0)
                Win32.PostThreadMessage(threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread?.Join(500);
        }

        void HotkeyLoop()
        {
            threadId = Win32.GetCurrentThreadId();

            // MOD_NOREPEAT: fire once per press, not repeatedly while held.
            if (!Win32.RegisterHotKey(IntPtr.Zero, HotkeyIdQuit,
                    Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_NOREPEAT, Win32.VK_Q))
            {
                Debug.LogWarning("TaskbarHotkeys: RegisterHotKey failed; the Ctrl+Alt+Q quit hotkey is unavailable.");
                return;
            }

            // GetMessage blocks until a message arrives; PostThreadMessage(WM_QUIT)
            // from OnDestroy makes it return 0, ending the loop cleanly.
            while (running && Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == Win32.WM_HOTKEY && (int)msg.wParam == HotkeyIdQuit)
                    quitRequested = true;
            }

            Win32.UnregisterHotKey(IntPtr.Zero, HotkeyIdQuit);
        }
#endif
    }
}
