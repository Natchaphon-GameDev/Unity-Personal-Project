using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TaskbarHero.EditorTools
{
    /// <summary>
    /// One-click environment setup for the floating desktop-overlay idle RPG. Rebuilds the
    /// whole scene (camera, EventSystem, overlay services, game controller, HUD + settings
    /// UI) so nothing has to be hand-wired. Idempotent: safe to run again at any time.
    /// </summary>
    public static class TaskbarHeroSetup
    {
        const string ScenePath = "Assets/Scenes/TaskbarHero.unity";
        const string ConfigPath = "Assets/Settings/IdleRpgConfig.asset";

        static readonly Color TextColor = UiTheme.Cream;

        [MenuItem("Tools/Taskbar Hero/Set Up Environment")]
        public static void SetUpEnvironment()
        {
            ApplyPlayerSettings();
            DisableHdrOnUrpAssets();
            TaskbarHeroArt.GenerateAll();
            CreateOverlayScene();
            TrySwitchToWindowsTarget();
            Debug.Log("Taskbar Hero: environment setup finished.");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "TaskbarHero";

            // Keep simulating and rendering while another window has focus — a desktop
            // overlay is almost never the focused window.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false; // borderless; size is driven by SetWindowPos

            // Open at the settings-panel size to minimize a resize flash on first run.
            PlayerSettings.defaultScreenWidth = 520;
            PlayerSettings.defaultScreenHeight = 420;

            // Flip-model swapchains break the DWM "sheet of glass" transparency trick.
            PlayerSettings.useFlipModelSwapchain = false;

            // That flag only affects D3D11 — D3D12 (the Unity 6 automatic default) always
            // presents via flip model and renders the overlay on solid black, so pin the
            // Windows build to D3D11 explicitly.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });

            Debug.Log("Taskbar Hero: player settings applied.");
        }

        static void DisableHdrOnUrpAssets()
        {
            // An HDR backbuffer has no usable alpha channel — the overlay would render on
            // black instead of transparent.
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset != null && asset.supportsHDR)
                {
                    asset.supportsHDR = false;
                    EditorUtility.SetDirty(asset);
                    Debug.Log("Taskbar Hero: disabled HDR on " + path);
                }
            }

            AssetDatabase.SaveAssets();
        }

        static void CreateOverlayScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // alpha 0 = see-through after DWM extend
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.gameObject.AddComponent<Physics2DRaycaster>(); // lets the monster collider take clicks

            // Input + UI event plumbing (project uses the Input System, so the module must
            // be InputSystemUIInputModule, not the legacy StandaloneInputModule).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var overlayGo = new GameObject("Overlay",
                typeof(OverlayWindow), typeof(WindowDragController), typeof(ClickThroughController), typeof(TaskbarHotkeys));
            var overlay = overlayGo.GetComponent<OverlayWindow>();
            var drag = overlayGo.GetComponent<WindowDragController>();
            var clickThrough = overlayGo.GetComponent<ClickThroughController>();
            SetRefs(drag, ("overlay", overlay));
            SetRefs(clickThrough, ("overlay", overlay), ("drag", drag));

            var config = LoadOrCreateConfig();

            var gameGo = new GameObject("Game");
            var view = gameGo.AddComponent<IdleRpgView>();
            var sfx = gameGo.AddComponent<SfxPlayer>();
            var controller = gameGo.AddComponent<GameController>();
            SetRefs(view, ("config", config));
            SetRefs(controller, ("config", config), ("view", view), ("sfx", sfx));

            BuildSampleVisuals();

            var canvas = BuildCanvas(out var scaler);
            var hud = BuildHud(canvas.transform, out var hudRefs);
            var settings = BuildSettings(canvas.transform, out var settingsRefs);

            var appFlow = new GameObject("AppFlow").AddComponent<AppFlow>();
            SetRefs(appFlow,
                ("controller", controller),
                ("overlay", overlay),
                ("settingsPanel", settings),
                ("hud", hud),
                ("clickThrough", clickThrough),
                ("canvasScaler", scaler));

            WireHud(hud, hudRefs);
            WireSettings(settings, settingsRefs);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Taskbar Hero: created " + ScenePath + " and set it as the only build scene.");
        }

        // --- Canvas / HUD / Settings construction -------------------------------------

        static Canvas BuildCanvas(out CanvasScaler scaler)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true; // keep the pixel font and 9-slice borders crisp
            scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // scaleFactor driven by AppFlow
            scaler.scaleFactor = 1f;
            return canvas;
        }

        struct HudRefs
        {
            public GameObject hudRoot, lootRoot, toastRoot;
            public Text goldText, statsText, lootFeedText, toastText;
            public Button gearButton, closeButton;
        }

        static HudPanel BuildHud(Transform canvas, out HudRefs refs)
        {
            var hudRoot = UiObject("HudRoot", canvas);
            Stretch(Rt(hudRoot));

            // Gold counter: coin icon + amount in a gold-rimmed pill, top-left.
            var goldPill = UiPanel("GoldPill", hudRoot.transform, "pill");
            PlaceTopLeft(Rt(goldPill), 8, 6, 116, 24);
            var coinIcon = UiSpriteImage("CoinIcon", goldPill.transform, Ui("coin"));
            PlaceTopLeft(Rt(coinIcon), 5, 4, 16, 16);
            var gold = WithOutline(MakeText(goldPill.transform, "GoldText", "0", 16, TextAnchor.MiddleLeft));
            gold.color = UiTheme.Gold;
            gold.raycastTarget = false;
            Stretch(Rt(gold.gameObject), 26, 1, 6, 1);

            var stats = WithOutline(MakeText(hudRoot.transform, "StatsText", "", 14, TextAnchor.UpperLeft));
            stats.raycastTarget = false; // don't block dragging the body under it
            PlaceTopLeft(Rt(stats.gameObject), 10, 36, 340, 40);

            // Font-safe glyphs (the pixel font has no gear or heavy ✕ glyph):
            // "=" reads as a settings/menu button, "X" as close. Stacked vertically in
            // the corner so the monster HP bar has the horizontal space next to them.
            var close = MakeButton(hudRoot.transform, "CloseButton", "X", out _);
            PlaceTopRight(Rt(close.gameObject), 8, 6, 28, 26);

            var gear = MakeButton(hudRoot.transform, "GearButton", "=", out _);
            PlaceTopRight(Rt(gear.gameObject), 8, 36, 28, 26);

            // Loot feed: chest icon + rarity-colored line in a slim dark pill, bottom-left.
            var lootRoot = UiPanel("LootRoot", hudRoot.transform, "frame");
            PlaceBottomLeft(Rt(lootRoot), 8, 6, 330, 24);
            var chestIcon = UiSpriteImage("ChestIcon", lootRoot.transform, Art("chest"));
            PlaceTopLeft(Rt(chestIcon), 4, 4, 16, 16);
            var loot = MakeText(lootRoot.transform, "LootFeedText", "", 14, TextAnchor.MiddleLeft);
            loot.raycastTarget = false;
            Stretch(Rt(loot.gameObject), 26, 1, 6, 1);
            lootRoot.SetActive(false);

            // Welcome-back toast: iron panel with a gold title over the stats line.
            var toastRoot = UiPanel("ToastRoot", hudRoot.transform, "panel");
            PlaceCenter(Rt(toastRoot), 360, 92, 0);
            var toastTitle = WithOutline(MakeText(toastRoot.transform, "ToastTitle", "WELCOME BACK", 18, TextAnchor.MiddleCenter));
            toastTitle.color = UiTheme.Gold;
            toastTitle.raycastTarget = false;
            PlaceTopLeftFull(Rt(toastTitle.gameObject), 14, 26);
            var toastText = MakeText(toastRoot.transform, "ToastText", "", 14, TextAnchor.MiddleCenter);
            toastText.raycastTarget = false;
            Stretch(Rt(toastText.gameObject), 12, 10, 12, 44);
            toastRoot.SetActive(false);

            var hud = hudRoot.AddComponent<HudPanel>();
            refs = new HudRefs
            {
                hudRoot = hudRoot,
                lootRoot = lootRoot,
                toastRoot = toastRoot,
                goldText = gold,
                statsText = stats,
                lootFeedText = loot,
                toastText = toastText,
                gearButton = gear,
                closeButton = close,
            };
            return hud;
        }

        static void WireHud(HudPanel hud, HudRefs r) => SetRefs(hud,
            ("hudRoot", r.hudRoot),
            ("goldText", r.goldText),
            ("statsText", r.statsText),
            ("lootRoot", r.lootRoot),
            ("lootFeedText", r.lootFeedText),
            ("gearButton", r.gearButton),
            ("closeButton", r.closeButton),
            ("toastRoot", r.toastRoot),
            ("toastText", r.toastText));

        struct SettingsRefs
        {
            public GameObject settingsRoot;
            public Slider volume;
            public Toggle alwaysOnTop, startup;
            public Button sizeButton, confirmButton;
            public Text sizeLabel, confirmLabel;
        }

        static SettingsPanel BuildSettings(Transform canvas, out SettingsRefs refs)
        {
            var root = UiPanel("SettingsRoot", canvas, "panel");
            Stretch(Rt(root));

            // Crimson header banner with the panel name, like the reference game's windows.
            var banner = UiPanel("TitleBanner", root.transform, "banner");
            PlaceTopCenter(Rt(banner), 14, 280, 46);
            var title = WithOutline(MakeText(banner.transform, "Title", "TASKBAR HERO", 22, TextAnchor.MiddleCenter));
            title.color = UiTheme.Gold;
            title.raycastTarget = false;
            Stretch(Rt(title.gameObject));

            MakeLabel(root.transform, "VolumeLabel", "Master Volume", 30, 108);
            var volume = MakeSlider(root.transform, "VolumeSlider", 1f);
            PlaceTopLeft(Rt(volume.gameObject), 220, 112, 266, 22);

            MakeLabel(root.transform, "AlwaysOnTopLabel", "Always on Top", 30, 162);
            var alwaysOnTop = MakeToggle(root.transform, "AlwaysOnTopToggle", true);
            PlaceTopLeft(Rt(alwaysOnTop.gameObject), 452, 158, 34, 34);

            MakeLabel(root.transform, "StartupLabel", "Launch at Windows Startup", 30, 216);
            var startup = MakeToggle(root.transform, "StartupToggle", false);
            PlaceTopLeft(Rt(startup.gameObject), 452, 212, 34, 34);

            MakeLabel(root.transform, "SizeLabelText", "Window Size", 30, 270);
            var sizeButton = MakeButton(root.transform, "SizeButton", "1x", out var sizeLabel);
            PlaceTopLeft(Rt(sizeButton.gameObject), 360, 264, 126, 42);

            var confirm = MakeButton(root.transform, "ConfirmButton", "Next", out var confirmLabel);
            confirmLabel.color = UiTheme.Gold;
            confirmLabel.fontSize = 20;
            PlaceTopLeft(Rt(confirm.gameObject), 160, 326, 200, 52);

            var footer = MakeText(root.transform, "Footer", "You can change these anytime in Settings.", 12, TextAnchor.MiddleCenter);
            footer.raycastTarget = false;
            footer.color = UiTheme.CreamDim;
            PlaceTopLeftFull(Rt(footer.gameObject), 390, 24);

            var panel = root.AddComponent<SettingsPanel>();
            refs = new SettingsRefs
            {
                settingsRoot = root,
                volume = volume,
                alwaysOnTop = alwaysOnTop,
                startup = startup,
                sizeButton = sizeButton,
                sizeLabel = sizeLabel,
                confirmButton = confirm,
                confirmLabel = confirmLabel,
            };
            return panel;
        }

        static void WireSettings(SettingsPanel panel, SettingsRefs r) => SetRefs(panel,
            ("settingsRoot", r.settingsRoot),
            ("volumeSlider", r.volume),
            ("alwaysOnTopToggle", r.alwaysOnTop),
            ("startupToggle", r.startup),
            ("sizeButton", r.sizeButton),
            ("sizeLabel", r.sizeLabel),
            ("confirmButton", r.confirmButton),
            ("confirmLabel", r.confirmLabel));

        // --- UGUI builder helpers -----------------------------------------------------

        static Font LegacyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Font PixelFont
        {
            get
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/TaskbarHero/KenneyMini.ttf");
                return font != null ? font : LegacyFont;
            }
        }

        static Sprite Ui(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/TaskbarHero/UI/{name}.png");

        static Sprite Art(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/TaskbarHero/{name}.png");

        static GameObject UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject UiImage(string name, Transform parent, Color color)
        {
            var go = UiObject(name, parent);
            go.AddComponent<Image>().color = color;
            return go;
        }

        /// <summary>A 9-sliced pixel-art panel using one of the generated UI sprites.</summary>
        static GameObject UiPanel(string name, Transform parent, string spriteName)
        {
            var go = UiObject(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = Ui(spriteName);
            image.type = Image.Type.Sliced;
            return go;
        }

        /// <summary>A plain sprite image (icon), not raycastable so it never blocks dragging.</summary>
        static GameObject UiSpriteImage(string name, Transform parent, Sprite sprite)
        {
            var go = UiObject(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return go;
        }

        /// <summary>Dark 1px drop outline so floating text stays readable over any desktop.</summary>
        static Text WithOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Outline;
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        static Text MakeText(Transform parent, string name, string content, int fontSize, TextAnchor anchor)
        {
            var go = UiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = PixelFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void MakeLabel(Transform parent, string name, string content, float x, float y)
        {
            var text = MakeText(parent, name, content, 17, TextAnchor.MiddleLeft);
            text.raycastTarget = false;
            PlaceTopLeft(Rt(text.gameObject), x, y, 320, 30);
        }

        static Button MakeButton(Transform parent, string name, string label, out Text labelText)
        {
            var go = UiPanel(name, parent, "button");
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            labelText = MakeText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter);
            labelText.raycastTarget = false;
            Stretch(Rt(labelText.gameObject));
            return button;
        }

        static Toggle MakeToggle(Transform parent, string name, bool isOn)
        {
            var go = UiPanel(name, parent, "frame");
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = go.GetComponent<Image>();
            var check = UiImage("Checkmark", go.transform, UiTheme.Gold);
            Stretch(Rt(check), 7, 7, 7, 7);
            toggle.graphic = check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(isOn);
            return toggle;
        }

        static Slider MakeSlider(Transform parent, string name, float value)
        {
            var go = UiObject(name, parent);
            var slider = go.AddComponent<Slider>();

            var background = UiPanel("Background", go.transform, "frame");
            Stretch(Rt(background));

            var fillArea = UiObject("Fill Area", go.transform);
            Stretch(Rt(fillArea), 4, 4, 4, 4);
            var fill = UiImage("Fill", fillArea.transform, UiTheme.Gold);
            var fillRt = Rt(fill);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(10f, 0f);

            var handleArea = UiObject("Handle Slide Area", go.transform);
            Stretch(Rt(handleArea), 6, 0, 6, 0);
            var handle = UiPanel("Handle", handleArea.transform, "button");
            var handleRt = Rt(handle);
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.sizeDelta = new Vector2(16f, 0f);

            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        // --- RectTransform placement (top-left origin, pixel coords) -------------------

        static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // Full-width row anchored to the top, spanning the panel width.
        static void PlaceTopLeftFull(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(0f, h);
        }

        static void PlaceTopCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceTopRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceBottomLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void PlaceCenter(RectTransform rt, float w, float h, float yOffset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }

        // --- Misc ---------------------------------------------------------------------

        static IdleRpgConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<IdleRpgConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<IdleRpgConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        static void BuildSampleVisuals()
        {
            // The 2D/3D "proof of life" samples are kept for reference but hidden, so the
            // actual game is what shows. Re-enable SampleVisuals in the inspector to compare.
            var samples = new GameObject("SampleVisuals");
            var switcher = samples.AddComponent<VisualSampleSwitcher>();

            var sample3D = new GameObject("Sample3D");
            sample3D.transform.SetParent(samples.transform);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ProofOfLifeCube";
            cube.transform.SetParent(sample3D.transform);
            cube.transform.position = new Vector3(0f, 1f, -7f);
            cube.transform.rotation = Quaternion.Euler(20f, 30f, 0f);
            cube.AddComponent<Rotator>();

            var sample2D = new GameObject("Sample2D");
            sample2D.transform.SetParent(samples.transform);
            var spriteGo = new GameObject("ProofOfLifeSprite");
            spriteGo.transform.SetParent(sample2D.transform);
            spriteGo.transform.position = new Vector3(0f, 1f, -7f);
            spriteGo.transform.localScale = new Vector3(12f, 12f, 1f);
            var spriteRenderer = spriteGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            spriteRenderer.color = new Color(1f, 0.6f, 0.1f);
            var rotator = spriteGo.AddComponent<Rotator>();
            SetRefs(rotator, ("axis", (object)Vector3.forward));

            SetRefs(switcher, ("sample3D", sample3D), ("sample2D", sample2D));
            sample2D.SetActive(false);
            samples.SetActive(false);
        }

        static void TrySwitchToWindowsTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
                return;

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                Debug.LogWarning("Taskbar Hero: could not switch to the Windows build target. " +
                                 "Install 'Windows Build Support (Mono)' via Unity Hub, restart the editor, then run this menu item again.");
        }

        // Sets serialized fields on a component. Object values set objectReferenceValue;
        // a Vector3 value sets vector3Value (used for Rotator.axis).
        static void SetRefs(Object component, params (string prop, object value)[] refs)
        {
            var so = new SerializedObject(component);
            foreach (var (prop, value) in refs)
            {
                var property = so.FindProperty(prop);
                if (property == null)
                {
                    Debug.LogWarning($"Taskbar Hero: no serialized property '{prop}' on {component.GetType().Name}.");
                    continue;
                }

                if (value is Vector3 v)
                    property.vector3Value = v;
                else
                    property.objectReferenceValue = value as Object;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
