using System;
using System.Collections.Generic;

namespace TaskbarHero
{
    /// <summary>
    /// A snapshot of everything needed to reconstruct an <see cref="IdleRpg"/>: the
    /// values that are earned/chosen over time. Derived values (Attack, XpToNext,
    /// MonsterMaxHp) are intentionally left out — they are recomputed on load from
    /// level, equipment and stage, so they can never drift out of sync with the rules.
    ///
    /// Plain serializable data (no Unity types) so it doubles as the payload the JSON
    /// save file stores.
    /// </summary>
    [Serializable]
    public sealed class IdleRpgState
    {
        public int Level;
        public int Xp;
        public long Gold;
        public int Stage;

        /// <summary>Remaining HP of the monster in progress. 0 means "spawn a fresh, full one".</summary>
        public int MonsterHp;

        public bool InBossFight;
        public float BossTimeRemaining;

        public List<EquipmentItem> Equipped = new List<EquipmentItem>();

        /// <summary>The starting state of a brand-new game.</summary>
        public static IdleRpgState NewGame() => new IdleRpgState
        {
            Level = 1,
            Xp = 0,
            Gold = 0,
            Stage = 1,
            MonsterHp = 0,
            InBossFight = false,
            BossTimeRemaining = 0f,
            Equipped = new List<EquipmentItem>(),
        };
    }
}
