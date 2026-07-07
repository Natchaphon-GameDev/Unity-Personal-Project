using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>Edit-mode tests for the player's tap-to-attack bonus hit.</summary>
    public class TapStrikeTests
    {
        // Long attack interval so plain Tick() does no auto-strike; we only advance the
        // tap cooldown. Monsters are tanky enough to survive a single tap unless a test
        // wants a kill.
        static IdleRpgRules TapRules() => new IdleRpgRules
        {
            AttackInterval = 10f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 5,
            BaseMonsterHp = 100,
            MonsterHpGrowth = 1f,
            BaseGold = 3,
            GoldGrowth = 1f,
            BaseXp = 10,
            XpGrowth = 1f,
            BaseXpToNext = 1000000,
            XpToNextGrowth = 1f,
            TapDamageMultiplier = 3f,
            TapCooldownSeconds = 0.15f,
        };

        [Test]
        public void Tap_DealsMultipliedDamage()
        {
            var game = new IdleRpg(TapRules());

            bool fired = game.TapStrike();

            Assert.IsTrue(fired);
            Assert.AreEqual(70, game.MonsterHp); // 100 - (10 attack * 3)
        }

        [Test]
        public void Tap_WhileOnCooldown_DoesNothing()
        {
            var game = new IdleRpg(TapRules());

            Assert.IsTrue(game.TapStrike());   // 100 -> 70
            Assert.IsFalse(game.TapStrike());  // still on cooldown
            Assert.AreEqual(70, game.MonsterHp);
        }

        [Test]
        public void Tap_AfterCooldownElapses_FiresAgain()
        {
            var game = new IdleRpg(TapRules());

            Assert.IsTrue(game.TapStrike());
            game.Tick(0.2f); // > cooldown; no auto-strike because interval is 10s
            Assert.IsTrue(game.TapStrike());

            Assert.AreEqual(40, game.MonsterHp); // two taps of 30
        }

        [Test]
        public void Tap_ThatKills_GrantsRewardAndAdvancesStage()
        {
            var rules = TapRules();
            rules.BaseMonsterHp = 20; // dies to one 30-damage tap
            var game = new IdleRpg(rules);

            game.TapStrike();

            Assert.AreEqual(2, game.Stage);
            Assert.AreEqual(3, game.Gold);
        }
    }
}
