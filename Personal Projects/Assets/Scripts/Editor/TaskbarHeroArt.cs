using System.IO;
using UnityEditor;
using UnityEngine;

namespace TaskbarHero.EditorTools
{
    /// <summary>
    /// Generates the pixel-art 9-slice UI sprites (iron panel, crimson banner, bronze
    /// button, bar frame, gold pill, coin) as PNGs under Resources/TaskbarHero/UI, and
    /// enforces pixel-crisp import settings (point filter, no compression, borders) on
    /// them and on the Tiny Dungeon world sprites. Deterministic and idempotent, so the
    /// generated PNGs can be committed and regenerated at will.
    /// </summary>
    public static class TaskbarHeroArt
    {
        const string ArtDir = "Assets/Resources/TaskbarHero";
        const string UiDir = ArtDir + "/UI";

        [MenuItem("Tools/Taskbar Hero/Generate UI Art")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(UiDir);

            // Iron panel: 2px outline, 3px beveled iron frame, 2px inner line, dark fill.
            Save("panel", Rings(24, (d, topLeft) =>
                d <= 1 ? UiTheme.Outline
                : d == 2 ? (topLeft ? UiTheme.IronLight : UiTheme.IronDark)
                : d <= 4 ? UiTheme.Iron
                : d <= 6 ? UiTheme.Outline
                : UiTheme.PanelFill));

            // Crimson header banner: dark maroon edge around a lit crimson body.
            Save("banner", Rings(20, (d, topLeft) =>
                d <= 1 ? UiTheme.Outline
                : d <= 3 ? UiTheme.BannerEdge
                : d <= 5 ? (topLeft ? UiTheme.BannerLight : UiTheme.Banner)
                : UiTheme.BannerFill));

            // Bronze button: beveled so it reads as raised.
            Save("button", Rings(16, (d, topLeft) =>
                d <= 1 ? UiTheme.Outline
                : d <= 3 ? (topLeft ? UiTheme.ButtonLight : UiTheme.ButtonDark)
                : d == 4 ? UiTheme.Button
                : UiTheme.ButtonFill));

            // Slim dark frame for bars, toggles and the loot feed pill.
            Save("frame", Rings(10, (d, _) =>
                d == 0 ? UiTheme.Outline
                : d == 1 ? UiTheme.IronDark
                : d == 2 ? UiTheme.Outline
                : UiTheme.BarBack));

            // Gold-rimmed pill for the coin counter.
            Save("pill", Rings(12, (d, topLeft) =>
                d <= 1 ? UiTheme.PillEdge
                : d <= 3 ? (topLeft ? UiTheme.PillRim : UiTheme.PillRimDark)
                : UiTheme.PillFill));

            Save("coin", Coin(14));

            AssetDatabase.Refresh();

            SetImporter($"{UiDir}/panel.png", 100, new Vector4(7, 7, 7, 7));
            SetImporter($"{UiDir}/banner.png", 100, new Vector4(6, 6, 6, 6));
            SetImporter($"{UiDir}/button.png", 100, new Vector4(5, 5, 5, 5));
            SetImporter($"{UiDir}/frame.png", 100, new Vector4(3, 3, 3, 3));
            SetImporter($"{UiDir}/pill.png", 100, new Vector4(4, 4, 4, 4));
            SetImporter($"{UiDir}/coin.png", 100, Vector4.zero);

            // World-space sprites picked from the Tiny Dungeon pack: same PPU as the characters.
            SetImporter($"{ArtDir}/ground_0.png", 16, Vector4.zero);
            SetImporter($"{ArtDir}/ground_1.png", 16, Vector4.zero);
            SetImporter($"{ArtDir}/chest.png", 16, Vector4.zero);

            Debug.Log("Taskbar Hero: UI art generated under " + UiDir);
        }

        /// <summary>
        /// Fills a square texture by concentric rings: <c>pick</c> gets each pixel's
        /// distance-from-edge and whether its nearest edge is the visual top/left
        /// (for bevel highlights). Texture space is bottom-left origin, so visual
        /// top is y == size - 1.
        /// </summary>
        static Texture2D Rings(int size, System.Func<int, bool, Color32> pick)
        {
            var tex = NewTex(size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    bool topLeft = size - 1 - y == d || x == d;
                    tex.SetPixel(x, y, pick(d, topLeft));
                }
            }
            return tex;
        }

        static Texture2D Coin(int size)
        {
            var tex = NewTex(size);
            float c = (size - 1) / 2f;
            float radius = size / 2f - 1f;
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 col;
                    if (dist > radius)
                        col = clear;
                    else if (dist > radius - 1.4f)
                        col = UiTheme.CoinEdge;
                    else if ((dx + 1.5f) * (dx + 1.5f) + (dy - 1.5f) * (dy - 1.5f) <= 4f) // upper-left glint
                        col = UiTheme.CoinLight;
                    else if (dx - dy > 3f) // lower-right shading
                        col = UiTheme.CoinDark;
                    else
                        col = UiTheme.Coin;
                    tex.SetPixel(x, y, col);
                }
            }
            return tex;
        }

        static Texture2D NewTex(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

        static void Save(string name, Texture2D tex)
        {
            tex.Apply();
            File.WriteAllBytes($"{UiDir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void SetImporter(string path, float pixelsPerUnit, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Taskbar Hero: no texture importer at " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
