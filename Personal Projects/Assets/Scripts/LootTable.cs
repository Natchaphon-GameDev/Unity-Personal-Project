using System;

namespace TaskbarHero
{
    /// <summary>
    /// Rolls a single <see cref="EquipmentItem"/> from the current stage. Pure and
    /// deterministic given the injected <see cref="Random"/>, so loot is reproducible
    /// in tests and across a save/load (the sim persists its RNG seed).
    /// </summary>
    public static class LootTable
    {
        public static EquipmentItem Roll(IdleRpgRules rules, int stage, Random rng)
        {
            Rarity rarity = Rarity.Common;
            double roll = rng.NextDouble();
            if (roll < rules.EpicDropChance)
                rarity = Rarity.Epic;
            else if (roll < rules.EpicDropChance + rules.RareDropChance)
                rarity = Rarity.Rare;

            var slot = (EquipSlot)rng.Next(0, 3);
            // Trinkets find gold; weapons and armor sharpen the hero's attack.
            var stat = slot == EquipSlot.Trinket ? StatKind.GoldFindPercent : StatKind.AttackPercent;

            int magnitude = rules.LootBaseMagnitude
                + (int)rarity * rules.LootRarityBonus
                + stage / Math.Max(1, rules.LootStageDivisor)
                + rng.Next(0, rules.LootMagnitudeSpread + 1);

            return new EquipmentItem(slot, rarity, stat, magnitude, stage);
        }
    }
}
