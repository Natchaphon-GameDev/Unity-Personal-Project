using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>
    /// Edit-mode tests that capturing and restoring a game preserves its observable
    /// state, including values that are recomputed rather than stored (Attack).
    /// </summary>
    public class StateRoundTripTests
    {
        static IdleRpgRules RoundTripRules() => new IdleRpgRules
        {
            AttackInterval = 1f,
            MaxCatchUpHits = 100,
            BaseAttack = 10,
            AttackPerLevel = 5,
            BaseMonsterHp = 10,
            MonsterHpGrowth = 1.1f,
            BaseGold = 3,
            GoldGrowth = 1.05f,
            BaseXp = 10,
            XpGrowth = 1f,
            BaseXpToNext = 20,
            XpToNextGrowth = 1.25f,
            DropChance = 1f, // build up some equipment to round-trip
            SellValueBase = 5,
        };

        [Test]
        public void CaptureThenRestore_PreservesObservableState()
        {
            var rules = RoundTripRules();
            var original = new IdleRpg(rules);
            original.Tick(25f); // play a while: level up, advance stages, collect loot

            var state = original.CaptureState();
            var restored = new IdleRpg(rules, state, 0);

            Assert.AreEqual(original.Level, restored.Level);
            Assert.AreEqual(original.Xp, restored.Xp);
            Assert.AreEqual(original.Gold, restored.Gold);
            Assert.AreEqual(original.Stage, restored.Stage);
            Assert.AreEqual(original.Attack, restored.Attack);
            Assert.AreEqual(original.MonsterMaxHp, restored.MonsterMaxHp);
            Assert.AreEqual(original.MonsterHp, restored.MonsterHp);
            Assert.AreEqual(original.InBossFight, restored.InBossFight);
            Assert.AreEqual(original.CaptureState().Equipped.Count, restored.CaptureState().Equipped.Count);
        }

        [Test]
        public void RestoredEquipment_StillDrivesDerivedAttack()
        {
            var rules = RoundTripRules();
            var original = new IdleRpg(rules);
            original.Tick(25f);

            var restored = new IdleRpg(rules, original.CaptureState(), 0);

            // Attack is not stored; if restore recomputes it from equipment it will match.
            Assert.AreEqual(original.Attack, restored.Attack);
        }
    }
}
