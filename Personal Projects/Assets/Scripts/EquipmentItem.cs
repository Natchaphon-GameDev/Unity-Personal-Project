using System;

namespace TaskbarHero
{
    /// <summary>Which equipment slot an item occupies. Cast to int for the equipped-by-slot array.</summary>
    public enum EquipSlot { Weapon, Armor, Trinket }

    /// <summary>Item rarity, ascending. Higher rarity means a stronger bonus and a higher sell value.</summary>
    public enum Rarity { Common, Rare, Epic }

    /// <summary>Which hero stat an item's bonus applies to.</summary>
    public enum StatKind { AttackPercent, GoldFindPercent }

    /// <summary>
    /// A piece of equipment dropped by a monster. Plain, serializable data (no Unity
    /// types) so it lives in both the pure sim and the JSON save file. Enum fields
    /// serialize as their underlying int, which JsonUtility handles.
    /// </summary>
    [Serializable]
    public sealed class EquipmentItem
    {
        public EquipSlot Slot;
        public Rarity Rarity;
        public StatKind Stat;

        /// <summary>The bonus size, as a whole-number percent (e.g. 12 == +12%).</summary>
        public int MagnitudePercent;

        /// <summary>The stage the item dropped on; drives sell value and rough power.</summary>
        public int ItemLevel;

        public EquipmentItem() { }

        public EquipmentItem(EquipSlot slot, Rarity rarity, StatKind stat, int magnitudePercent, int itemLevel)
        {
            Slot = slot;
            Rarity = rarity;
            Stat = stat;
            MagnitudePercent = magnitudePercent;
            ItemLevel = itemLevel;
        }

        /// <summary>Comparable strength used to decide auto-equip vs auto-sell. Rarity weights magnitude.</summary>
        public int PowerScore => MagnitudePercent * ((int)Rarity + 1);

        /// <summary>Gold granted when this item is auto-sold instead of equipped.</summary>
        public long SellValue(IdleRpgRules rules)
            => (long)rules.SellValueBase * ((int)Rarity + 1) * (ItemLevel + 1);
    }
}
