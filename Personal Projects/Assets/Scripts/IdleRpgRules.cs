using System;

namespace TaskbarHero
{
    /// <summary>
    /// Plain-C# tuning values and scaling formulas for <see cref="IdleRpg"/>.
    /// Deliberately free of Unity types so the simulation stays unit-testable
    /// outside play mode; the designer-facing <see cref="IdleRpgConfig"/>
    /// ScriptableObject builds one of these to hand to the sim.
    /// </summary>
    public sealed class IdleRpgRules
    {
        /// <summary>Seconds between the hero's automatic attacks.</summary>
        public float AttackInterval = 0.6f;

        /// <summary>Upper bound on attacks resolved in one Tick, so a huge delta
        /// (e.g. after the app was suspended) can't spin the loop unbounded.</summary>
        public int MaxCatchUpHits = 200;

        public int BaseAttack = 5;
        public int AttackPerLevel = 3;

        public int BaseMonsterHp = 20;
        public float MonsterHpGrowth = 1.12f;

        public int BaseGold = 4;
        public float GoldGrowth = 1.08f;

        public int BaseXp = 6;
        public float XpGrowth = 1.06f;

        public int BaseXpToNext = 20;
        public float XpToNextGrowth = 1.25f;

        // --- Loot & equipment ---
        // Chance a defeated monster drops an item. 0 disables loot entirely (default so
        // the base sim tests see no drops). Rare/Epic chances are checked in that order;
        // anything else is Common.
        public float DropChance = 0f;
        public float RareDropChance = 0.25f;
        public float EpicDropChance = 0.05f;
        public int LootBaseMagnitude = 4;   // percent bonus a Common item starts at
        public int LootRarityBonus = 4;     // extra percent per rarity tier above Common
        public int LootStageDivisor = 5;    // +1% magnitude per this many stages
        public int LootMagnitudeSpread = 3; // random extra percent, 0..this inclusive
        public int SellValueBase = 5;       // gold multiplier when an item is auto-sold

        // --- Bosses ---
        // Every Nth stage is a timed boss. 0 disables bosses (default for base sim tests).
        public int BossEveryNStages = 0;
        public float BossHpMultiplier = 8f;
        public float BossTimeLimitSeconds = 30f;
        public float BossGoldMultiplier = 10f;

        // --- Tap-to-attack ---
        public float TapDamageMultiplier = 3f;
        public float TapCooldownSeconds = 0.15f;
        // Chance a tap lands a critical hit that multiplies the tap damage again.
        // 0 disables crits (default so the base sim tests draw no extra rng).
        public float TapCritChance = 0f;
        public float TapCritMultiplier = 3f;

        // --- Offline progress ---
        public double OfflineCapSeconds = 8 * 3600;

        public int MonsterHpForStage(int stage) => ScaledInt(BaseMonsterHp, MonsterHpGrowth, stage);
        public int GoldForStage(int stage) => ScaledInt(BaseGold, GoldGrowth, stage);
        public int XpForStage(int stage) => ScaledInt(BaseXp, XpGrowth, stage);
        public int XpToNextForLevel(int level) => ScaledInt(BaseXpToNext, XpToNextGrowth, level);

        // baseValue * growth^(step-1), rounded, clamped to at least 1. step is 1-based.
        static int ScaledInt(int baseValue, float growth, int step)
        {
            double value = baseValue * Math.Pow(growth, Math.Max(0, step - 1));
            int rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return rounded < 1 ? 1 : rounded;
        }
    }
}
