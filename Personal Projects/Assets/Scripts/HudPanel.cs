using UnityEngine;
using UnityEngine.UI;

namespace TaskbarHero
{
    /// <summary>
    /// The gameplay heads-up display: live stats, a loot feed line, gear (open settings)
    /// and close (quit) buttons, and the welcome-back offline toast. Reads live state from
    /// <see cref="GameController"/>; button/loot wiring is set up in <see cref="Initialize"/>.
    /// </summary>
    public sealed class HudPanel : MonoBehaviour
    {
        [SerializeField] GameObject hudRoot;
        [SerializeField] Text goldText;
        [SerializeField] Text statsText;
        [SerializeField] GameObject lootRoot;
        [SerializeField] Text lootFeedText;
        [SerializeField] Button gearButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject toastRoot;
        [SerializeField] Text toastText;

        GameController controller;
        float lootFeedTimer;
        float toastTimer;

        public void Initialize(AppFlow flow)
        {
            controller = FindFirstObjectByType<GameController>();

            if (gearButton != null)
                gearButton.onClick.AddListener(flow.OpenSettings);
            if (closeButton != null)
                closeButton.onClick.AddListener(Application.Quit);

            if (toastRoot != null)
                toastRoot.SetActive(false);
            if (lootRoot != null)
                lootRoot.SetActive(false);
            if (lootFeedText != null)
                lootFeedText.text = string.Empty;

            if (controller != null && controller.Game != null)
                controller.Game.OnLoot += OnLoot;
        }

        void OnDestroy()
        {
            if (controller != null && controller.Game != null)
                controller.Game.OnLoot -= OnLoot;
        }

        public void SetVisible(bool visible)
        {
            if (hudRoot != null)
                hudRoot.SetActive(visible);
        }

        public void ShowOfflineToast(OfflineResult result)
        {
            if (toastRoot == null || toastText == null || !result.HasAnything)
                return;

            // Offline stages can go backwards (failed boss timers), so only brag about gains.
            var parts = new System.Collections.Generic.List<string>();
            if (result.GoldGained > 0)
                parts.Add($"+{result.GoldGained:N0} gold");
            if (result.StagesGained > 0)
                parts.Add($"+{result.StagesGained} stages");
            if (result.LevelsGained > 0)
                parts.Add($"+{result.LevelsGained} lv");
            string line = string.Join("   ", parts);
            toastText.text = line;
            toastRoot.SetActive(true);
            toastTimer = 6f;
        }

        void Update()
        {
            var game = controller != null ? controller.Game : null;
            if (goldText != null && game != null)
                goldText.text = game.Gold.ToString("N0");
            if (statsText != null && game != null)
            {
                string text = $"Lv {game.Level}   ATK {game.Attack}   Stage {game.Stage}";
                if (game.InBossFight)
                    text += $"\n<color={UiTheme.BossHex}>BOSS  {Mathf.CeilToInt(game.BossTimeRemaining)}s</color>";
                statsText.text = text;
            }

            if (lootFeedTimer > 0f)
            {
                lootFeedTimer -= Time.deltaTime;
                if (lootFeedTimer <= 0f)
                {
                    if (lootFeedText != null)
                        lootFeedText.text = string.Empty;
                    if (lootRoot != null)
                        lootRoot.SetActive(false);
                }
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                if (toastTimer <= 0f && toastRoot != null)
                    toastRoot.SetActive(false);
            }
        }

        void OnLoot(EquipmentItem item, bool equipped)
        {
            if (lootFeedText == null)
                return;
            lootFeedText.text = (equipped ? "Equipped " : "Sold ")
                + $"<color={UiTheme.RarityHex(item.Rarity)}>{item.Rarity} {item.Slot}</color> +{item.MagnitudePercent}%";
            if (lootRoot != null)
                lootRoot.SetActive(true);
            lootFeedTimer = 4f;
        }
    }
}
