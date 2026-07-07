using NUnit.Framework;
using UnityEngine;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>
    /// Edit-mode tests for the save schema: JSON round-trips through JsonUtility (the
    /// same path the real save file uses) and the version-validation guard.
    /// </summary>
    public class SaveDataTests
    {
        static IdleRpgRules RichRules() => new IdleRpgRules
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
            DropChance = 1f, // guarantee equipment to serialize
            SellValueBase = 5,
        };

        [Test]
        public void JsonRoundTrip_PreservesGameStateAndEquipment()
        {
            var game = new IdleRpg(RichRules());
            game.Tick(25f);

            var original = SaveData.FromGame(game, rngSeed: 4242, nowUnixUtc: 1_700_000_000L);
            string json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(original.version, restored.version);
            Assert.AreEqual(original.savedAtUnixUtc, restored.savedAtUnixUtc);
            Assert.AreEqual(original.rngSeed, restored.rngSeed);
            Assert.AreEqual(original.sim.Level, restored.sim.Level);
            Assert.AreEqual(original.sim.Gold, restored.sim.Gold);
            Assert.AreEqual(original.sim.Stage, restored.sim.Stage);
            Assert.AreEqual(original.sim.Equipped.Count, restored.sim.Equipped.Count);
            if (original.sim.Equipped.Count > 0)
            {
                Assert.AreEqual(original.sim.Equipped[0].Slot, restored.sim.Equipped[0].Slot);
                Assert.AreEqual(original.sim.Equipped[0].Rarity, restored.sim.Equipped[0].Rarity);
                Assert.AreEqual(original.sim.Equipped[0].MagnitudePercent, restored.sim.Equipped[0].MagnitudePercent);
            }
        }

        [Test]
        public void RestoringFromRoundTrippedSave_ReproducesTheSameGame()
        {
            var rules = RichRules();
            var game = new IdleRpg(rules);
            game.Tick(25f);

            var save = SaveData.FromGame(game, 4242, 1_700_000_000L);
            var restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            var reloaded = new IdleRpg(rules, restored.sim, 4242);

            Assert.AreEqual(game.Level, reloaded.Level);
            Assert.AreEqual(game.Attack, reloaded.Attack);
            Assert.AreEqual(game.Gold, reloaded.Gold);
            Assert.AreEqual(game.Stage, reloaded.Stage);
            Assert.AreEqual(game.MonsterHp, reloaded.MonsterHp);
        }

        [Test]
        public void IsValid_RejectsNullWrongVersionAndMissingSim()
        {
            Assert.IsFalse(SaveSystem.IsValid(null));
            Assert.IsFalse(SaveSystem.IsValid(new SaveData { version = SaveSystem.CurrentVersion + 1, sim = IdleRpgState.NewGame() }));
            Assert.IsFalse(SaveSystem.IsValid(new SaveData { version = SaveSystem.CurrentVersion, sim = null }));
            Assert.IsTrue(SaveSystem.IsValid(new SaveData { version = SaveSystem.CurrentVersion, sim = IdleRpgState.NewGame() }));
        }
    }
}
