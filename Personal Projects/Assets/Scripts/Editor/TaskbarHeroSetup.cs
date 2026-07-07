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

        static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.08f, 0.97f);
        static readonly Color TextColor = new Color(0.93f, 0.9f, 0.85f, 1f);
        static readonly Color ButtonColor = new Color(0.45f, 0.26f, 0.13f, 0.98f);
        static readonly Color FieldColor = new Color(0.13f, 0.11f, 0.1f, 1f);

        [MenuItem("Tools/Taskbar Hero/Set Up Environment")]
        public static void SetUpEnvironment()
        {
            ApplyPlayerSettings();
            DisableHdrOnUrpAssets();
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
            scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // scaleFactor driven by AppFlow
            scaler.scaleFactor = 1f;
            return canvas;
        }

        struct HudRefs
        {
            public GameObject hudRoot, toastRoot;
            public Text statsText, lootFeedText, toastText;
            public Button gearButton, closeButton;
        }

        static HudPanel BuildHud(Transform canvas, out HudRefs refs)
        {
            var hudRoot = UiObject("HudRoot", canvas);
            Stretch(Rt(hudRoot));

            var stats = MakeText(hudRoot.transform, "StatsText", "", 16, TextAnchor.UpperLeft);
            stats.raycastTarget = false; // don't block dragging the body under it
            PlaceTopLeft(Rt(stats.gameObject), 14, 8, 340, 46);

            var loot = MakeText(hudRoot.transform, "LootFeedText", "", 13, TextAnchor.LowerLeft);
            loot.raycastTarget = false;
            loot.color = new Color(0.8f, 0.85f, 0.6f, 1f);
            PlaceBottomLeft(Rt(loot.gameObject), 14, 8, 360, 24);

            // Font-safe glyphs (LegacyRuntime.ttf/Arial lacks a gear and heavy ✕):
            // "≡" reads as a settings/menu button, "X" as close.
            var gear = MakeButton(hudRoot.transform, "GearButton", "≡", out _);
            PlaceTopRight(Rt(gear.gameObject), 44, 8, 30, 28);

            var close = MakeButton(hudRoot.transform, "CloseButton", "X", out _);
            PlaceTopRight(Rt(close.gameObject), 8, 8, 30, 28);

            var toastRoot = UiImage("ToastRoot", hudRoot.transform, new Color(0.05f, 0.05f, 0.06f, 0.92f));
            PlaceCenter(Rt(toastRoot), 380, 84, 0);
            var toastText = MakeText(toastRoot.transform, "ToastText", "", 16, TextAnchor.MiddleCenter);
            toastText.raycastTarget = false;
            Stretch(Rt(toastText.gameObject), 10, 8, 10, 8);
            toastRoot.SetActive(false);

            var hud = hudRoot.AddComponent<HudPanel>();
            refs = new HudRefs
            {
                hudRoot = hudRoot,
                toastRoot = toastRoot,
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
            ("statsText", r.statsText),
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
            var root = UiImage("SettingsRoot", canvas, PanelColor);
            Stretch(Rt(root));

            var title = MakeText(root.transform, "Title", "Taskbar Hero — Settings", 22, TextAnchor.MiddleCenter);
            title.raycastTarget = false;
            PlaceTopLeftFull(Rt(title.gameObject), 18, 40);

            MakeLabel(root.transform, "VolumeLabel", "Master Volume", 30, 96);
            var volume = MakeSlider(root.transform, "VolumeSlider", 1f);
            PlaceTopLeft(Rt(volume.gameObject), 220, 100, 270, 22);

            MakeLabel(root.transform, "AlwaysOnTopLabel", "Always on Top", 30, 150);
            var alwaysOnTop = MakeToggle(root.transform, "AlwaysOnTopToggle", true);
            PlaceTopLeft(Rt(alwaysOnTop.gameObject), 452, 146, 34, 34);

            MakeLabel(root.transform, "StartupLabel", "Launch at Windows Startup", 30, 204);
            var startup = MakeToggle(root.transform, "StartupToggle", false);
            PlaceTopLeft(Rt(startup.gameObject), 452, 200, 34, 34);

            MakeLabel(root.transform, "SizeLabelText", "Window Size", 30, 258);
            var sizeButton = MakeButton(root.transform, "SizeButton", "1x", out var sizeLabel);
            PlaceTopLeft(Rt(sizeButton.gameObject), 360, 252, 126, 40);

            var confirm = MakeButton(root.transform, "ConfirmButton", "Next", out var confirmLabel);
            PlaceTopLeft(Rt(confirm.gameObject), 170, 322, 180, 50);

            var footer = MakeText(root.transform, "Footer", "You can change these anytime in Settings.", 12, TextAnchor.MiddleCenter);
            footer.raycastTarget = false;
            footer.color = new Color(0.7f, 0.68f, 0.62f, 1f);
            PlaceTopLeftFull(Rt(footer.gameObject), 384, 24);

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

        static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        static Text MakeText(Transform parent, string name, string content, int fontSize, TextAnchor anchor)
        {
            var go = UiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = LegacyFont;
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
            var go = UiImage(name, parent, ButtonColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            labelText = MakeText(go.transform, "Label", label, 18, TextAnchor.MiddleCenter);
            labelText.raycastTarget = false;
            Stretch(Rt(labelText.gameObject));
            return button;
        }

        static Toggle MakeToggle(Transform parent, string name, bool isOn)
        {
            var go = UiImage(name, parent, FieldColor);
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = go.GetComponent<Image>();
            var check = UiImage("Checkmark", go.transform, new Color(0.3f, 0.85f, 0.35f, 1f));
            Stretch(Rt(check), 5, 5, 5, 5);
            toggle.graphic = check.GetComponent<Image>();
            toggle.SetIsOnWithoutNotify(isOn);
            return toggle;
        }

        static Slider MakeSlider(Transform parent, string name, float value)
        {
            var go = UiObject(name, parent);
            var slider = go.AddComponent<Slider>();

            var background = UiImage("Background", go.transform, FieldColor);
            Stretch(Rt(background));

            var fillArea = UiObject("Fill Area", go.transform);
            Stretch(Rt(fillArea), 6, 0, 6, 0);
            var fill = UiImage("Fill", fillArea.transform, new Color(0.7f, 0.55f, 0.3f, 1f));
            var fillRt = Rt(fill);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(10f, 0f);

            var handleArea = UiObject("Handle Slide Area", go.transform);
            Stretch(Rt(handleArea), 6, 0, 6, 0);
            var handle = UiImage("Handle", handleArea.transform, new Color(0.86f, 0.8f, 0.7f, 1f));
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
