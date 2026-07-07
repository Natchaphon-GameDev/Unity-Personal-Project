using UnityEngine;
using UnityEngine.UI;

namespace TaskbarHero
{
    /// <summary>
    /// The app's mode state machine. On boot it configures the overlay window from saved
    /// settings, then shows the settings panel on first run or drops straight into
    /// gameplay. Settings mode makes the whole window interactive at a fixed comfortable
    /// size; gameplay mode shrinks to the strip, scales the canvas, and enables selective
    /// click-through. The sim keeps ticking in both modes (GameController owns the tick).
    /// </summary>
    public sealed class AppFlow : MonoBehaviour
    {
        [SerializeField] GameController controller;
        [SerializeField] OverlayWindow overlay;
        [SerializeField] SettingsPanel settingsPanel;
        [SerializeField] HudPanel hud;
        [SerializeField] ClickThroughController clickThrough;
        [SerializeField] CanvasScaler canvasScaler;

        const int GameplayBaseWidth = 480;
        const int GameplayBaseHeight = 160;
        const int SettingsWidth = 520;
        const int SettingsHeight = 420;

        bool settingsWasFirstRun;

        void Start()
        {
            overlay.Initialize();

            var settings = controller.Settings;
            AudioListener.volume = settings.MasterVolume;
            overlay.SetTopMost(settings.AlwaysOnTop);
            StartupRegistry.SetEnabled(settings.LaunchAtStartup);

            settingsPanel.Initialize(settings, this);
            hud.Initialize(this);

            if (!settings.FirstRunDone)
                EnterSettings(firstRun: true);
            else
                EnterGameplay(firstEntry: true);
        }

        /// <summary>Reopen settings over gameplay (HUD gear button).</summary>
        public void OpenSettings() => EnterSettings(firstRun: false);

        /// <summary>Settings confirm button ("Next" on first run, "Back" when reopened).</summary>
        public void SubmitSettings()
        {
            if (settingsWasFirstRun)
            {
                controller.Settings.FirstRunDone = true;
                controller.Settings.Save();
                EnterGameplay(firstEntry: true);
            }
            else
            {
                EnterGameplay(firstEntry: false);
            }
        }

        void EnterSettings(bool firstRun)
        {
            settingsWasFirstRun = firstRun;

            if (clickThrough != null)
                clickThrough.enabled = false;
            overlay.SetClickThrough(false);

            SetCanvasScale(1);
            overlay.SetSize(SettingsWidth, SettingsHeight);
            if (firstRun)
                overlay.PositionTopCenterPrimary(SettingsWidth, SettingsHeight);

            hud.SetVisible(false);
            settingsPanel.Show(firstRun);
        }

        void EnterGameplay(bool firstEntry)
        {
            settingsPanel.Hide();

            int scale = Mathf.Clamp(controller.Settings.WindowScale, 1, 3);
            int width = GameplayBaseWidth * scale;
            int height = GameplayBaseHeight * scale;

            SetCanvasScale(scale);
            overlay.SetSize(width, height);
            if (firstEntry)
                overlay.PositionTopCenterPrimary(width, height);

            hud.SetVisible(true);
            if (clickThrough != null)
                clickThrough.enabled = true;

            if (firstEntry)
                hud.ShowOfflineToast(controller.PendingOffline);
        }

        void SetCanvasScale(int scale)
        {
            if (canvasScaler != null)
                canvasScaler.scaleFactor = scale;
        }
    }
}
