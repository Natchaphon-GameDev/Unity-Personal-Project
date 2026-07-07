using System;
using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Toggles "launch when Windows starts" by writing an HKCU Run entry pointing at this
    /// executable. Uses advapi32 P/Invoke directly (Microsoft.Win32.Registry is absent
    /// from the player's .NET Standard profile). Windows-player only; a no-op that reports
    /// false in the editor and on macOS so the settings toggle is safe to bind everywhere.
    /// </summary>
    public static class StartupRegistry
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "TaskbarHero";

        public static void SetEnabled(bool enabled)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Win32.RegCreateKeyEx(Win32.HKEY_CURRENT_USER, RunKeyPath, 0, null, 0,
                    Win32.KEY_SET_VALUE, IntPtr.Zero, out var hKey, out _) != Win32.ERROR_SUCCESS)
            {
                Debug.LogWarning("StartupRegistry: could not open the Run key.");
                return;
            }

            try
            {
                if (enabled)
                {
                    string command = "\"" + GetExecutablePath() + "\"";
                    byte[] data = System.Text.Encoding.Unicode.GetBytes(command + "\0");
                    Win32.RegSetValueEx(hKey, ValueName, 0, Win32.REG_SZ, data, data.Length);
                }
                else
                {
                    Win32.RegDeleteValue(hKey, ValueName); // fine if it wasn't there
                }
            }
            finally
            {
                Win32.RegCloseKey(hKey);
            }
#endif
        }

        public static bool IsEnabled()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Win32.RegOpenKeyEx(Win32.HKEY_CURRENT_USER, RunKeyPath, 0, Win32.KEY_QUERY_VALUE, out var hKey) != Win32.ERROR_SUCCESS)
                return false;

            try
            {
                int size = 0;
                return Win32.RegQueryValueEx(hKey, ValueName, 0, out _, null, ref size) == Win32.ERROR_SUCCESS;
            }
            finally
            {
                Win32.RegCloseKey(hKey);
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static string GetExecutablePath()
        {
            var sb = new System.Text.StringBuilder(1024);
            Win32.GetModuleFileName(IntPtr.Zero, sb, sb.Capacity);
            return sb.ToString();
        }
#endif
    }
}
