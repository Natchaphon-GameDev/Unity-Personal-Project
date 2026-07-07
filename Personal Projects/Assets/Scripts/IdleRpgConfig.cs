using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Designer-facing tuning asset for the idle RPG. Mirrors <see cref="IdleRpgRules"/>
    /// so balance can be tweaked in the inspector without touching code, following
    /// the repo's "data lives in a ScriptableObject" convention.
    /// </summary>
    [CreateAssetMenu(menuName = "Taskbar Hero/Idle RPG Config", fileName = "IdleRpgConfig")]
    public sealed class IdleRpgConfig : ScriptableObject
    {
        [Header("Hero attack cadence")]
        [Min(0.05f)] public float attackInterval = 0.6f;
        [Min(1)] public int maxCatchUpHits = 200;

        [Header("Hero power")]
        [Min(1)] public int baseAttack = 5;
        [Min(0)] public int attackPerLevel = 3;

        [Header("Monster HP scaling (per stage)")]
        [Min(1)] public int baseMonsterHp = 20;
        [Min(1f)] public float monsterHpGrowth = 1.12f;

        [Header("Gold reward scaling (per stage)")]
        [Min(0)] public int baseGold = 4;
        [Min(1f)] public float goldGrowth = 1.08f;

        [Header("XP reward scaling (per stage)")]
        [Min(0)] public int baseXp = 6;
        [Min(1f)] public float xpGrowth = 1.06f;

        [Header("Level-up XP curve (per level)")]
        [Min(1)] public int baseXpToNext = 20;
        [Min(1f)] public float xpToNextGrowth = 1.25f;

        [Header("Loot & equipment")]
        [Range(0f, 1f)] public float dropChance = 0.08f;
        [Range(0f, 1f)] public float rareDropChance = 0.25f;
        [Range(0f, 1f)] public float epicDropChance = 0.05f;
        [Min(1)] public int lootBaseMagnitude = 4;
        [Min(0)] public int lootRarityBonus = 4;
        [Min(1)] public int lootStageDivisor = 5;
        [Min(0)] public int lootMagnitudeSpread = 3;
        [Min(1)] public int sellValueBase = 5;

        [Header("Bosses (every N stages)")]
        [Min(0)] public int bossEveryNStages = 10;
        [Min(1f)] public float bossHpMultiplier = 8f;
        [Min(1f)] public float bossTimeLimitSeconds = 30f;
        [Min(1f)] public float bossGoldMultiplier = 10f;

        [Header("Tap-to-attack")]
        [Min(1f)] public float tapDamageMultiplier = 3f;
        [Min(0f)] public float tapCooldownSeconds = 0.15f;

        [Header("Offline progress")]
        [Min(0f)] public float offlineCapHours = 8f;

        public IdleRpgRules ToRules() => new IdleRpgRules
        {
            AttackInterval = attackInterval,
            MaxCatchUpHits = maxCatchUpHits,
            BaseAttack = baseAttack,
            AttackPerLevel = attackPerLevel,
            BaseMonsterHp = baseMonsterHp,
            MonsterHpGrowth = monsterHpGrowth,
            BaseGold = baseGold,
            GoldGrowth = goldGrowth,
            BaseXp = baseXp,
            XpGrowth = xpGrowth,
            BaseXpToNext = baseXpToNext,
            XpToNextGrowth = xpToNextGrowth,
            DropChance = dropChance,
            RareDropChance = rareDropChance,
            EpicDropChance = epicDropChance,
            LootBaseMagnitude = lootBaseMagnitude,
            LootRarityBonus = lootRarityBonus,
            LootStageDivisor = lootStageDivisor,
            LootMagnitudeSpread = lootMagnitudeSpread,
            SellValueBase = sellValueBase,
            BossEveryNStages = bossEveryNStages,
            BossHpMultiplier = bossHpMultiplier,
            BossTimeLimitSeconds = bossTimeLimitSeconds,
            BossGoldMultiplier = bossGoldMultiplier,
            TapDamageMultiplier = tapDamageMultiplier,
            TapCooldownSeconds = tapCooldownSeconds,
            OfflineCapSeconds = offlineCapHours * 3600.0,
        };
    }
}
