using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Player-chosen settings, persisted via <see cref="PlayerPrefs"/>. These are small
    /// scalars needed at boot (the first-run flow reads FirstRunDone before the save
    /// file is even loaded), so PlayerPrefs is a better fit than the JSON progress file.
    /// </summary>
    public sealed class AppSettings
    {
        public float MasterVolume = 1f;
        public bool AlwaysOnTop = true;
        public bool LaunchAtStartup = false;
        public int WindowScale = 1;      // 1x / 2x / 3x
        public bool FirstRunDone = false;

        const string KeyVolume = "th.volume";
        const string KeyAlwaysOnTop = "th.alwaysOnTop";
        const string KeyLaunchStartup = "th.launchStartup";
        const string KeyWindowScale = "th.windowScale";
        const string KeyFirstRunDone = "th.firstRunDone";

        public static AppSettings Load() => new AppSettings
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f)),
            AlwaysOnTop = PlayerPrefs.GetInt(KeyAlwaysOnTop, 1) != 0,
            LaunchAtStartup = PlayerPrefs.GetInt(KeyLaunchStartup, 0) != 0,
            WindowScale = Mathf.Clamp(PlayerPrefs.GetInt(KeyWindowScale, 1), 1, 3),
            FirstRunDone = PlayerPrefs.GetInt(KeyFirstRunDone, 0) != 0,
        };

        public void Save()
        {
            PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
            PlayerPrefs.SetInt(KeyAlwaysOnTop, AlwaysOnTop ? 1 : 0);
            PlayerPrefs.SetInt(KeyLaunchStartup, LaunchAtStartup ? 1 : 0);
            PlayerPrefs.SetInt(KeyWindowScale, WindowScale);
            PlayerPrefs.SetInt(KeyFirstRunDone, FirstRunDone ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
