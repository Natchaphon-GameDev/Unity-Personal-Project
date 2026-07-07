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
        };
    }
}
