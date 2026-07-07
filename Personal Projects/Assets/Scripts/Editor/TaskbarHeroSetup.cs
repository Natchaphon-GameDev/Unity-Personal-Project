using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TaskbarHero.EditorTools
{
    /// <summary>
    /// One-click environment setup for the taskbar-overlay experiment.
    /// Idempotent: safe to run again at any time.
    /// </summary>
    public static class TaskbarHeroSetup
    {
        const string ScenePath = "Assets/Scenes/TaskbarHero.unity";
        const string ConfigPath = "Assets/Settings/IdleRpgConfig.asset";

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
            // Keep simulating and rendering while another window has focus —
            // a taskbar overlay is almost never the focused window.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;

            // Flip-model swapchains break the DWM "sheet of glass" transparency trick.
            PlayerSettings.useFlipModelSwapchain = false;

            Debug.Log("Taskbar Hero: player settings applied.");
        }

        static void DisableHdrOnUrpAssets()
        {
            // An HDR backbuffer has no usable alpha channel — the overlay would
            // render on black instead of transparent.
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

            // Orthographic, centred on the origin: gives the idle-RPG view predictable
            // world units to lay out against on the short, very wide taskbar strip.
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            new GameObject("TaskbarOverlay", typeof(TaskbarOverlayWindow), typeof(TaskbarHotkeys));

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

            var spriteRotator = spriteGo.AddComponent<Rotator>();
            var rotatorProps = new SerializedObject(spriteRotator);
            rotatorProps.FindProperty("axis").vector3Value = Vector3.forward; // Z spin reads as 2D rotation
            rotatorProps.ApplyModifiedPropertiesWithoutUndo();

            var switcherProps = new SerializedObject(switcher);
            switcherProps.FindProperty("sample3D").objectReferenceValue = sample3D;
            switcherProps.FindProperty("sample2D").objectReferenceValue = sample2D;
            switcherProps.ApplyModifiedPropertiesWithoutUndo();

            sample2D.SetActive(false); // match the switcher's default Sample3D mode

            // The 2D/3D "proof of life" samples are kept for reference but hidden, so
            // the actual game is what shows on the taskbar. Re-enable SampleVisuals in
            // the inspector to compare the samples again.
            samples.SetActive(false);

            CreateIdleRpgGame();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Taskbar Hero: created " + ScenePath + " and set it as the only build scene.");
        }

        static void CreateIdleRpgGame()
        {
            // Reuse the tuning asset if it already exists, else create one so balance
            // values live in a designer-editable ScriptableObject (repo convention).
            var config = AssetDatabase.LoadAssetAtPath<IdleRpgConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<IdleRpgConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }

            var game = new GameObject("IdleRpgGame");
            var view = game.AddComponent<IdleRpgView>();

            var viewProps = new SerializedObject(view);
            viewProps.FindProperty("config").objectReferenceValue = config;
            viewProps.ApplyModifiedPropertiesWithoutUndo();
        }

        static void TrySwitchToWindowsTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
                return;

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                Debug.LogWarning("Taskbar Hero: could not switch to the Windows build target. " +
                                 "Install 'Windows Build Support (Mono)' via Unity Hub, restart the editor, then run this menu item again.");
        }
    }
}
