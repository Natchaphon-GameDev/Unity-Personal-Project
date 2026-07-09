using System;
using UnityEngine;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System.IO;
#endif

namespace TaskbarHero
{
    /// <summary>
    /// Toggles "launch at login". Windows: writes an HKCU Run entry via advapi32 P/Invoke
    /// (Microsoft.Win32.Registry is absent from the player's .NET Standard profile).
    /// macOS: writes a LaunchAgent plist under ~/Library/LaunchAgents that opens the app
    /// bundle at login — plain file IO, no native code, takes effect from the next login
    /// (same semantics as the Run key). A no-op that reports false in the editor and on
    /// other platforms so the settings toggle is safe to bind everywhere.
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
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            try
            {
                if (enabled)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(PlistPath));
                    File.WriteAllText(PlistPath, BuildPlist());
                }
                else if (File.Exists(PlistPath))
                {
                    File.Delete(PlistPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"StartupRegistry: could not update the LaunchAgent plist: {e.Message}");
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
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return File.Exists(PlistPath);
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
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        const string AgentLabel = "com.nsp.taskbarhero";

        static string PlistPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            "Library/LaunchAgents", AgentLabel + ".plist");

        static string BuildPlist()
        {
            // Application.dataPath is <bundle>.app/Contents in the mac player.
            string appBundle = Path.GetDirectoryName(Application.dataPath);
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\">\n" +
                "<dict>\n" +
                "    <key>Label</key><string>" + AgentLabel + "</string>\n" +
                "    <key>ProgramArguments</key>\n" +
                "    <array>\n" +
                "        <string>/usr/bin/open</string>\n" +
                "        <string>" + appBundle + "</string>\n" +
                "    </array>\n" +
                "    <key>RunAtLoad</key><true/>\n" +
                "</dict>\n" +
                "</plist>\n";
        }
#endif
    }
}
