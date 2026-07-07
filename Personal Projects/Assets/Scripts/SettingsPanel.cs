using UnityEngine;
using UnityEngine.UI;

namespace TaskbarHero
{
    /// <summary>
    /// The settings panel: master volume, always-on-top, launch-at-startup, and a window
    /// size cycle (1x/2x/3x). Each control applies immediately and persists to
    /// <see cref="AppSettings"/>; the confirm button hands back to <see cref="AppFlow"/>,
    /// which resizes the window and enters gameplay. Shown full-screen on first run and as
    /// an overlay when reopened via the HUD gear.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] GameObject settingsRoot;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Toggle alwaysOnTopToggle;
        [SerializeField] Toggle startupToggle;
        [SerializeField] Button sizeButton;
        [SerializeField] Text sizeLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Text confirmLabel;

        AppSettings settings;
        AppFlow flow;

        public void Initialize(AppSettings appSettings, AppFlow appFlow)
        {
            settings = appSettings;
            flow = appFlow;

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.SetValueWithoutNotify(settings.MasterVolume);
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (alwaysOnTopToggle != null)
            {
                alwaysOnTopToggle.SetIsOnWithoutNotify(settings.AlwaysOnTop);
                alwaysOnTopToggle.onValueChanged.AddListener(OnAlwaysOnTopChanged);
            }

            if (startupToggle != null)
            {
                startupToggle.SetIsOnWithoutNotify(settings.LaunchAtStartup);
                startupToggle.onValueChanged.AddListener(OnStartupChanged);
            }

            if (sizeButton != null)
                sizeButton.onClick.AddListener(CycleSize);
            UpdateSizeLabel();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
        }

        public void Show(bool firstRun)
        {
            if (settingsRoot != null)
                settingsRoot.SetActive(true);
            if (confirmLabel != null)
                confirmLabel.text = firstRun ? "Next" : "Back";
        }

        public void Hide()
        {
            if (settingsRoot != null)
                settingsRoot.SetActive(false);
        }

        void OnVolumeChanged(float value)
        {
            settings.MasterVolume = value;
            AudioListener.volume = value;
            settings.Save();
        }

        void OnAlwaysOnTopChanged(bool on)
        {
            settings.AlwaysOnTop = on;
            if (OverlayWindow.Instance != null)
                OverlayWindow.Instance.SetTopMost(on);
            settings.Save();
        }

        void OnStartupChanged(bool on)
        {
            settings.LaunchAtStartup = on;
            StartupRegistry.SetEnabled(on);
            settings.Save();
        }

        void CycleSize()
        {
            settings.WindowScale = settings.WindowScale % 3 + 1; // 1 -> 2 -> 3 -> 1
            settings.Save();
            UpdateSizeLabel();
        }

        void UpdateSizeLabel()
        {
            if (sizeLabel != null)
                sizeLabel.text = settings.WindowScale + "x";
        }

        void OnConfirm() => flow.SubmitSettings();
    }
}
