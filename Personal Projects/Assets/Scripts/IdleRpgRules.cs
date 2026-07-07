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
