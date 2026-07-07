using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>
    /// Edit-mode tests for the pure-C# <see cref="IdleRpg"/> simulation. Uses rules
    /// with no per-stage/per-level growth so the arithmetic is exact and obvious:
    /// every monster has 10 HP, the hero hits for 10, so each attack is one kill.
    /// </summary>
    public class IdleRpgTests
    {
        static IdleRpgRules FlatRules() => new IdleRpgRules
        {
            AttackInterval = 1f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 5,
            BaseMonsterHp = 10,
            MonsterHpGrowth = 1f,   // constant 10 HP monsters
            BaseGold = 3,
            GoldGrowth = 1f,
            BaseXp = 10,
            XpGrowth = 1f,
            BaseXpToNext = 20,      // two kills (2 * 10 XP) => level up
            XpToNextGrowth = 1f,
        };

        [Test]
        public void NewGame_StartsAtLevelOneWithAFullFirstMonster()
        {
            var game = new IdleRpg(FlatRules());

            Assert.AreEqual(1, game.Level);
            Assert.AreEqual(1, game.Stage);
            Assert.AreEqual(0, game.Gold);
            Assert.AreEqual(10, game.Attack);
            Assert.AreEqual(10, game.MonsterMaxHp);
            Assert.AreEqual(game.MonsterMaxHp, game.MonsterHp);
        }

        [Test]
        public void Tick_BelowAttackInterval_DoesNotStrike()
        {
            var game = new IdleRpg(FlatRules());

            game.Tick(0.5f);

            Assert.AreEqual(game.MonsterMaxHp, game.MonsterHp);
        }

        [Test]
        public void Tick_ReachingInterval_DealsAttackDamage()
        {
            var rules = FlatRules();
            rules.BaseMonsterHp = 30; // survive one hit so we can observe the damage
            var game = new IdleRpg(rules);

            game.Tick(1f);

            Assert.AreEqual(20, game.MonsterHp); // 30 - 10
            Assert.AreEqual(1, game.Stage);      // still fighting the same monster
        }

        [Test]
        public void DefeatingMonster_GrantsGoldAndXp_AndSpawnsNextStage()
        {
            var game = new IdleRpg(FlatRules());

            game.Tick(1f); // one kill

            Assert.AreEqual(2, game.Stage);
            Assert.AreEqual(3, game.Gold);
            Assert.AreEqual(10, game.Xp);
            Assert.AreEqual(10, game.MonsterHp); // fresh monster, full HP
        }

        [Test]
        public void AccumulatingXp_LevelsUp_AndRaisesAttack()
        {
            var game = new IdleRpg(FlatRules());

            game.Tick(2f); // two kills => 20 XP => level up

            Assert.AreEqual(2, game.Level);
            Assert.AreEqual(15, game.Attack); // 10 + AttackPerLevel(5)
            Assert.AreEqual(0, game.Xp);      // 20 XP consumed by the level-up
            Assert.AreEqual(3, game.Stage);
            Assert.AreEqual(6, game.Gold);
        }

        [Test]
        public void MonsterHp_ScalesUpWithStage_WhenGrowthAboveOne()
        {
            var rules = new IdleRpgRules { BaseMonsterHp = 20, MonsterHpGrowth = 1.5f };

            Assert.Greater(rules.MonsterHpForStage(2), rules.MonsterHpForStage(1));
            Assert.AreEqual(20, rules.MonsterHpForStage(1)); // growth^0 == 1
        }

        [Test]
        public void Tick_WithHugeDelta_IsCappedByMaxCatchUpHits()
        {
            var rules = FlatRules();
            rules.MaxCatchUpHits = 3; // at most 3 strikes per Tick
            var game = new IdleRpg(rules);

            game.Tick(1000f); // would be 1000 kills if uncapped

            // 3 strikes => 3 kills => started at stage 1, now on stage 4.
            Assert.AreEqual(4, game.Stage);
        }
    }
}
