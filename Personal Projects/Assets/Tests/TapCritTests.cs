using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>Edit-mode tests for tap critical hits.</summary>
    public class TapCritTests
    {
        static IdleRpgRules CritRules(float critChance) => new IdleRpgRules
        {
            AttackInterval = 10f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 5,
            BaseMonsterHp = 1000,
            MonsterHpGrowth = 1f,
            BaseGold = 3,
            GoldGrowth = 1f,
            BaseXp = 10,
            XpGrowth = 1f,
            BaseXpToNext = 1000000,
            XpToNextGrowth = 1f,
            TapDamageMultiplier = 3f,
            TapCooldownSeconds = 0.15f,
            TapCritChance = critChance,
            TapCritMultiplier = 2f,
        };

        [Test]
        public void Tap_WithGuaranteedCrit_DealsMultipliedCritDamageAndReportsCrit()
        {
            var game = new IdleRpg(CritRules(1f));

            bool fired = game.TapStrike(out bool crit);

            Assert.IsTrue(fired);
            Assert.IsTrue(crit);
            Assert.AreEqual(1000 - 60, game.MonsterHp); // 10 attack * 3 tap * 2 crit
        }

        [Test]
        public void Tap_WithNoCritChance_NeverCrits()
        {
            var game = new IdleRpg(CritRules(0f));

            game.TapStrike(out bool crit);

            Assert.IsFalse(crit);
            Assert.AreEqual(1000 - 30, game.MonsterHp); // 10 attack * 3 tap, no crit bonus
        }

        [Test]
        public void Tap_WhileOnCooldown_ReportsNoCrit()
        {
            var game = new IdleRpg(CritRules(1f));

            game.TapStrike(out _);
            bool fired = game.TapStrike(out bool crit);

            Assert.IsFalse(fired);
            Assert.IsFalse(crit);
        }

        [Test]
        public void ParameterlessTapStrike_StillWorksWithCritsEnabled()
        {
            var game = new IdleRpg(CritRules(1f));

            bool fired = game.TapStrike();

            Assert.IsTrue(fired);
            Assert.AreEqual(1000 - 60, game.MonsterHp);
        }
    }
}
