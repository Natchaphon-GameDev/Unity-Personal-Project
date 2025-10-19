using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Scripts.LocalizedText
{
    
    [System.Serializable]
    public class LocalizedTextData
    {
        public string key;
        public string en;
        public string th;
    }

    public class GoogleSheetLocalizer : MonoBehaviour
    {
        [SerializeField] private string sheetUrl =
            "https://docs.google.com/spreadsheets/d/e/2PACX-1vSoynZ4jCeAyMcQdskc0ljqmNnog7OUrynmoBvpPvWtIy4CK_GU-HeAs176feQgtqrF0ifAqw6-ZnRj/pub?gid=0&single=true&output=csv";

        private Dictionary<string, Dictionary<string, string>> localizedData;

        private IEnumerator Start()
        {
            using (var www = UnityWebRequest.Get(sheetUrl))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(www.error);
                    yield break;
                }

                string csv = www.downloadHandler.text;
                ParseCSV(csv);
            }
        }

        private void ParseCSV(string csv)
        {
            localizedData = new Dictionary<string, Dictionary<string, string>>();
            var lines = csv.Split('\n');
            var headers = lines[0].Split(','); // en, th, etc.

            for (var i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split(',');
                if (cells.Length < 2) continue;

                string key = cells[0];
                localizedData[key] = new Dictionary<string, string>();
                for (var j = 1; j < headers.Length; j++)
                {
                    if (j < cells.Length)
                        localizedData[key][headers[j]] = cells[j];
                }
            }

            Debug.Log("Localization loaded: " + localizedData.Count + " entries");
        }

        public string GetText(string key, string lang = "th")
        {
            if (localizedData.TryGetValue(key, out var langs) &&
                langs.TryGetValue(lang, out var text))
                return text;

            return key;
        }
    }
}
