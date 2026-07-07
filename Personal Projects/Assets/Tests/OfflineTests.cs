using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>Edit-mode tests for offline fast-forward and its cap.</summary>
    public class OfflineTests
    {
        static IdleRpgRules OfflineRules() => new IdleRpgRules
        {
            AttackInterval = 1f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 5,
            BaseMonsterHp = 10,
            MonsterHpGrowth = 1f,
            BaseGold = 3,
            GoldGrowth = 1f,
            BaseXp = 10,
            XpGrowth = 1f,
            BaseXpToNext = 20,
            XpToNextGrowth = 1f,
            OfflineCapSeconds = 3600.0,
        };

        [Test]
        public void FastForward_MatchesManualTicks()
        {
            var fast = new IdleRpg(OfflineRules());
            var manual = new IdleRpg(OfflineRules());

            fast.FastForward(3600.0);
            for (int i = 0; i < 3600; i++)
                manual.Tick(1f);

            Assert.AreEqual(manual.Gold, fast.Gold);
            Assert.AreEqual(manual.Stage, fast.Stage);
            Assert.AreEqual(manual.Level, fast.Level);
            Assert.AreEqual(manual.MonsterHp, fast.MonsterHp);
        }

        [Test]
        public void FastForward_ClampsToCap()
        {
            var game = new IdleRpg(OfflineRules());

            var result = game.FastForward(1_000_000.0); // way past the 1h cap

            Assert.AreEqual(3600.0, result.SecondsSimulated, 0.001);
        }

        [Test]
        public void FastForward_NegativeElapsed_IsNoOp()
        {
            var game = new IdleRpg(OfflineRules());
            int stageBefore = game.Stage;
            long goldBefore = game.Gold;

            var result = game.FastForward(-500.0);

            Assert.AreEqual(0, result.StagesGained);
            Assert.AreEqual(0, result.GoldGained);
            Assert.AreEqual(stageBefore, game.Stage);
            Assert.AreEqual(goldBefore, game.Gold);
        }

        [Test]
        public void FastForward_ReportsPositiveGains()
        {
            var game = new IdleRpg(OfflineRules());

            var result = game.FastForward(3600.0);

            Assert.Greater(result.GoldGained, 0);
            Assert.Greater(result.StagesGained, 0);
            Assert.Greater(result.LevelsGained, 0);
            Assert.IsTrue(result.HasAnything);
        }
    }
}
