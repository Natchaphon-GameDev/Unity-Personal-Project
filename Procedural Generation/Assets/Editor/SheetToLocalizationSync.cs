// Assets/Editor/SheetToLocalizationSync.cs
// Unity 6000.2 + com.unity.localization 1.5.8
// Syncs a published Google Sheet (CSV/TSV) into a String Table Collection.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Localization;
using UnityEditor.Localization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

using UnityEngine.Networking;

// Localization (Editor / Runtime)
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

public class SheetToLocalizationSync : EditorWindow
{
    [Header("Source")] [SerializeField] private string sheetUrl =
        "https://docs.google.com/spreadsheets/d/e/2PACX-1vSoynZ4jCeAyMcQdskc0ljqmNnog7OUrynmoBvpPvWtIy4CK_GU-HeAs176feQgtqrF0ifAqw6-ZnRj/pub?gid=0&single=true&output=csv";


    [Header("Target Collection")]
    [SerializeField] private string collectionName = "Game";   // String Table Collection name

    [Header("Options")]
    [SerializeField] private string keyHeader = "Key";         // First column header for keys
    [SerializeField] private bool trimWhitespace = true;       // Trim cells
    [SerializeField] private bool createLocalesIfMissing = true;
    [SerializeField] private bool addMissingKeys = true;       // Create new entries
    [SerializeField] private bool updateExistingValues = true; // Update existing values
    [SerializeField] private bool deleteKeysNotInSheet = false;// Danger: removes entries not present in sheet
    [SerializeField] private bool reportOnly = false;          // Dry-run

    private string log = "";
    private Vector2 scroll;

    [MenuItem("Tools/Localization/Sheet → Sync")]
    public static void Open()
    {
        GetWindow<SheetToLocalizationSync>("Sheet → Localization Sync");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Google Sheet → Localization Tables", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("1) Source", EditorStyles.miniBoldLabel);
        sheetUrl = EditorGUILayout.TextField("Published CSV/TSV URL", sheetUrl);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("2) Target", EditorStyles.miniBoldLabel);
        collectionName = EditorGUILayout.TextField("String Table Collection", collectionName);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("3) Options", EditorStyles.miniBoldLabel);
        keyHeader = EditorGUILayout.TextField("Key Header", keyHeader);
        trimWhitespace = EditorGUILayout.Toggle("Trim Whitespace", trimWhitespace);
        createLocalesIfMissing = EditorGUILayout.Toggle("Create Locales If Missing", createLocalesIfMissing);
        addMissingKeys = EditorGUILayout.Toggle("Add Missing Keys", addMissingKeys);
        updateExistingValues = EditorGUILayout.Toggle("Update Existing Values", updateExistingValues);
        deleteKeysNotInSheet = EditorGUILayout.ToggleLeft("Delete keys not in sheet (use with caution)", deleteKeysNotInSheet);
        reportOnly = EditorGUILayout.ToggleLeft("Report only (dry-run)", reportOnly);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Sync Now", GUILayout.Height(30)))
        {
            _ = SyncNow();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Log", EditorStyles.miniBoldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    async System.Threading.Tasks.Task SyncNow()
    {
        try
        {
            if (string.IsNullOrEmpty(sheetUrl))
            {
                Log("❌ Please paste a valid published CSV/TSV URL.");
                return;
            }

            Log($"Fetching: {sheetUrl}");
            var text = await DownloadText(sheetUrl);
            if (string.IsNullOrEmpty(text))
            {
                Log("❌ Download failed or returned empty content.");
                return;
            }

            // Detect delimiter: if a tab exists in header line, assume TSV; else CSV
            var headerLine = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            char delimiter = headerLine.Contains('\t') ? '\t' : ',';

            var rows = ParseSeparatedValues(text, delimiter);
            if (rows.Count == 0)
            {
                Log("❌ No rows parsed from the sheet.");
                return;
            }

            // Header map
            var header = rows[0];
            var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Count; i++)
            {
                var name = trimWhitespace ? header[i].Trim() : header[i];
                if (!headerIndex.ContainsKey(name))
                    headerIndex[name] = i;
            }

            if (!headerIndex.ContainsKey(keyHeader))
            {
                Log($"❌ Could not find Key header \"{keyHeader}\" in first row.");
                return;
            }

            // Locale columns = all headers except the key header
            var localeHeaders = header
                .Where(h => !string.Equals(h, keyHeader, StringComparison.OrdinalIgnoreCase))
                .Select(h => trimWhitespace ? h.Trim() : h)
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (localeHeaders.Count == 0)
            {
                Log("❌ No locale columns found. Add columns like: en, th, ja, fr-FR, etc.");
                return;
            }

            // Load or create the String Table Collection
            var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
            if (collection == null)
            {
                Log($"ℹ️ Creating String Table Collection: {collectionName}");
                if (!reportOnly)
                    collection =
                        LocalizationEditorSettings.CreateStringTableCollection(collectionName, "Assets/Localization");


                else
                    collection =
                        LocalizationEditorSettings
                            .GetStringTableCollection(collectionName); // will still be null in dry-run, but fine
            }
            else
            {
                Log($"Found String Table Collection: {collectionName}");
            }

            // Ensure locales exist and tables for those locales exist in the collection
            var localeIds = new List<LocaleIdentifier>();
            foreach (var lh in localeHeaders)
            {
                var id = new LocaleIdentifier(lh); // supports "en", "th", "fr-FR", etc.
                var locale = LocalizationEditorSettings.GetLocale(id);
                if (locale == null && createLocalesIfMissing && !reportOnly)
                {
                    // Create and save a Locale asset, then register it.
                    var folder = "Assets/Localization/Locales";
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    var assetPath = $"{folder}/{id.Code}.asset";

                    locale = Locale.CreateLocale(id);          // create in-memory
                    AssetDatabase.CreateAsset(locale, assetPath); // persist as .asset
                    LocalizationEditorSettings.AddLocale(locale); // register with Localization system
                    AssetDatabase.SaveAssets();

                    Log($"➕ Created Locale asset and added: {id.Code} → {assetPath}");
                }
                else if (locale == null)
                {
                    Log($"⚠️ Locale not found: {lh} (will skip creating and still attempt to map if present later)");
                }

                localeIds.Add(id);

                if (collection != null)
                {
                    var table = collection.GetTable(id) as StringTable;
                    if (table == null && !reportOnly)
                    {
                        Log($"➕ Adding String Table for locale: {lh}");
                        // Add per-locale table under this collection
                        table = collection.AddNewTable(id) as StringTable;
                    }
                }
            }

            // Build a map: locale → StringTable (create if missing)
            var tableMap = new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);
            if (collection != null)
            {
                foreach (var id in localeIds)
                {
                    var t = collection.GetTable(id) as StringTable;
                    if (t != null)
                        tableMap[id.Code] = t;
                }
            }

            // Gather current keys across the collection (union of all tables)
            var existingKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in tableMap)
            {
                var t = kv.Value;
                if (t == null) continue;
                foreach (var e in t.Values)
                    existingKeys.Add(e.Key); // entry.Key is the string key
            }

            // Parse rows → { key, per-locale values }
            var sheetKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0) continue;

                var key = GetCell(row, headerIndex[keyHeader]);
                if (trimWhitespace) key = key.Trim();
                if (string.IsNullOrEmpty(key)) continue;

                sheetKeys.Add(key);

                foreach (var lh in localeHeaders)
                {
                    var idx = headerIndex.TryGetValue(lh, out var col) ? col : -1;
                    if (idx < 0) continue;

                    var value = GetCell(row, idx);
                    if (trimWhitespace) value = value.Trim();

                    // Find table for this locale
                    if (!tableMap.TryGetValue(new LocaleIdentifier(lh).Code, out var table) || table == null)
                    {
                        Log($"⚠️ No table for locale '{lh}' in collection '{collectionName}'. Skipping '{key}'.");
                        continue;
                    }

                    var entry = table.GetEntry(key);
                    if (entry == null)
                    {
                        if (addMissingKeys && !reportOnly)
                        {
                            table.AddEntry(key, value ?? string.Empty);
                            MarkDirty(table);
                            Log($"➕ [{lh}] {key} = \"{Shorten(value)}\"");
                        }
                        else
                        {
                            Log($"(skip add) [{lh}] {key}");
                        }
                    }
                    else
                    {
                        if (updateExistingValues && !reportOnly)
                        {
                            if (!string.Equals(entry.Value ?? "", value ?? "", StringComparison.Ordinal))
                            {
                                entry.Value = value ?? string.Empty;
                                MarkDirty(table);
                                Log($"✏️  [{lh}] {key} = \"{Shorten(value)}\"");
                            }
                            else
                            {
                                // unchanged
                            }
                        }
                    }
                }
            }

            // Delete keys not in sheet (optional)
            if (deleteKeysNotInSheet && !reportOnly)
            {
                var toDelete = existingKeys.Except(sheetKeys).ToList();
                if (toDelete.Count > 0)
                {
                    Log($"🗑 Removing {toDelete.Count} keys not present in the sheet...");
                    foreach (var key in toDelete)
                    {
                        foreach (var t in tableMap.Values)
                        {
                            var entry = t?.GetEntry(key);
                            if (entry != null)
                            {
                                t.RemoveEntry(key);
                                MarkDirty(t);
                                Log($"🗑 [{t.LocaleIdentifier.Code}] {key}");
                            }
                        }
                    }
                }
                else
                {
                    Log("No extra keys to remove.");
                }
            }

            // Save assets
            if (!reportOnly)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Log("✅ Sync complete.");
        }
        catch (Exception ex)
        {
            Log("❌ Error: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    static void MarkDirty(UnityEngine.Object obj)
    {
        if (obj != null)
            EditorUtility.SetDirty(obj);
    }

    void Log(string s)
    {
        log += s + "\n";
        Repaint();
    }

    static async System.Threading.Tasks.Task<string> DownloadText(string url)
    {
        using (var req = UnityWebRequest.Get(url))
        {
#if UNITY_6000_2_OR_NEWER
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                await System.Threading.Tasks.Task.Delay(30);
            }
#else
            await req.SendWebRequest();
#endif
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
                return null;
#else
            if (req.isNetworkError || req.isHttpError)
                return null;
#endif
            return req.downloadHandler.text;
        }
    }

    // Robust-ish CSV/TSV parser (handles quotes, escaped quotes, tabs or commas)
    static List<List<string>> ParseSeparatedValues(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var sb = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Look ahead for escaped quote
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                }
                else if (c == '\r')
                {
                    // ignore
                }
                else if (c == '\n')
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;

                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        // last cell
        row.Add(sb.ToString());
        rows.Add(row);

        // trim trailing empty line if present
        if (rows.Count > 0 && rows.Last().Count == 1 && string.IsNullOrEmpty(rows.Last()[0]))
            rows.RemoveAt(rows.Count - 1);

        return rows;
    }

    static string GetCell(List<string> row, int index)
    {
        if (index < 0 || index >= row.Count) return "";
        return row[index] ?? "";
    }

    static string Shorten(string s, int max = 80)
    {
        if (s == null) return "";
        s = s.Replace("\n", "\\n");
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "…";
    }
}
#endif
