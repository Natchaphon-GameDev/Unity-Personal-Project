using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>
    /// Edit-mode tests for the boss-every-N-stages loop. Games are started directly in
    /// a boss fight via the restore constructor so each case is isolated from the ticks
    /// it would otherwise take to farm up to a boss stage.
    /// </summary>
    public class BossTests
    {
        // Boss on every even stage; monster HP flat 10 before the boss multiplier.
        static IdleRpgRules BossRules(float hpMultiplier, float goldMultiplier, float timeLimit) => new IdleRpgRules
        {
            AttackInterval = 1f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 0,
            BaseMonsterHp = 10,
            MonsterHpGrowth = 1f,
            BaseGold = 3,
            GoldGrowth = 1f,
            BaseXp = 5,
            XpGrowth = 1f,
            BaseXpToNext = 1000000, // never level up during these short tests
            XpToNextGrowth = 1f,
            BossEveryNStages = 2,
            BossHpMultiplier = hpMultiplier,
            BossGoldMultiplier = goldMultiplier,
            BossTimeLimitSeconds = timeLimit,
        };

        static IdleRpg BossAtStage2(IdleRpgRules rules)
        {
            var state = IdleRpgState.NewGame();
            state.Stage = 2;            // stage 2 is a boss stage (2 % 2 == 0)
            state.InBossFight = true;
            state.BossTimeRemaining = rules.BossTimeLimitSeconds;
            state.MonsterHp = 0;        // full boss HP
            return new IdleRpg(rules, state, 0);
        }

        [Test]
        public void BossStage_HasMultipliedHp()
        {
            var rules = BossRules(hpMultiplier: 5f, goldMultiplier: 10f, timeLimit: 30f);
            var game = BossAtStage2(rules);

            Assert.IsTrue(game.InBossFight);
            Assert.AreEqual(50, game.MonsterMaxHp); // 10 * 5
            Assert.AreEqual(50, game.MonsterHp);
        }

        [Test]
        public void KillingBossInTime_GrantsMultipliedGold_AndAdvances()
        {
            var rules = BossRules(hpMultiplier: 5f, goldMultiplier: 10f, timeLimit: 30f);
            var game = BossAtStage2(rules);

            game.Tick(5f); // 5 hits * 10 = 50 dmg -> boss dies well within the 30s timer

            Assert.IsFalse(game.InBossFight);
            Assert.AreEqual(3, game.Stage);   // advanced past the boss
            Assert.AreEqual(30, game.Gold);   // stage-2 gold 3 * bossGoldMultiplier 10
        }

        [Test]
        public void BossTimeout_DropsBackOneStage()
        {
            // Boss too tough to kill in time: 1000 HP, 3s limit, 10 dmg/hit.
            var rules = BossRules(hpMultiplier: 100f, goldMultiplier: 10f, timeLimit: 3f);
            var game = BossAtStage2(rules);

            game.Tick(3f); // 3 hits, timer expires on the third

            Assert.IsFalse(game.InBossFight);
            Assert.AreEqual(1, game.Stage); // dropped from the boss stage back to farming
        }

        [Test]
        public void FailingBoss_ThenClearingFarmStage_ReEntersBoss()
        {
            var rules = BossRules(hpMultiplier: 100f, goldMultiplier: 10f, timeLimit: 3f);
            var game = BossAtStage2(rules);

            game.Tick(3f); // fail -> stage 1, spawns a normal 10 HP monster
            Assert.AreEqual(1, game.Stage);

            game.Tick(1f); // one hit clears the farm monster -> back to stage 2 boss

            Assert.AreEqual(2, game.Stage);
            Assert.IsTrue(game.InBossFight);
        }
    }
}
