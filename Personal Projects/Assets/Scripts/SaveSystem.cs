using System;
using System.IO;
using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Reads/writes the single JSON progress file in <see cref="Application.persistentDataPath"/>.
    /// Writes are atomic (temp file then replace) so a crash mid-save can't corrupt an
    /// existing save; a save that fails to load is set aside as ".bak" and the game
    /// starts fresh rather than crash-looping.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;
        const string FileName = "save.json";

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static long NowUnixUtc() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>True if the payload is present and matches the version this build understands.</summary>
        public static bool IsValid(SaveData data)
            => data != null && data.version == CurrentVersion && data.sim != null;

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            string path = FilePath;
            try
            {
                if (!File.Exists(path))
                    return false;

                var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (!IsValid(loaded))
                {
                    Debug.LogWarning("SaveSystem: save file invalid or from a different version; starting fresh.");
                    Backup(path);
                    return false;
                }

                data = loaded;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem: could not read save ({e.Message}); starting fresh.");
                Backup(path);
                return false;
            }
        }

        public static void Save(SaveData data)
        {
            string path = FilePath;
            string tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem: save failed ({e.Message}).");
                TryDelete(tmp);
            }
        }

        static void Backup(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Copy(path, path + ".bak", true);
            }
            catch { /* best-effort */ }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}
