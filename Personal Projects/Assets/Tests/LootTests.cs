using System.Collections.Generic;
using NUnit.Framework;
using TaskbarHero;

namespace TaskbarHero.Tests
{
    /// <summary>Edit-mode tests for loot drops, auto-equip/auto-sell, and the equipment stat bonuses.</summary>
    public class LootTests
    {
        // Flat rules where every monster dies in one hit, plus a guaranteed drop.
        static IdleRpgRules LootRules() => new IdleRpgRules
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
            DropChance = 1f, // always drop
            SellValueBase = 5,
        };

        [Test]
        public void FirstDrop_OnEmptySlot_IsEquipped()
        {
            var game = new IdleRpg(LootRules());

            game.Tick(1f); // one kill -> one guaranteed drop, slot was empty so it equips

            Assert.AreEqual(1, game.CaptureState().Equipped.Count);
        }

        [Test]
        public void SameSeed_ProducesIdenticalEquipment()
        {
            var a = new IdleRpg(LootRules());
            var b = new IdleRpg(LootRules());

            a.Tick(10f);
            b.Tick(10f);

            var ea = a.CaptureState().Equipped;
            var eb = b.CaptureState().Equipped;
            Assert.AreEqual(ea.Count, eb.Count);
            for (int i = 0; i < ea.Count; i++)
            {
                Assert.AreEqual(ea[i].Slot, eb[i].Slot);
                Assert.AreEqual(ea[i].Rarity, eb[i].Rarity);
                Assert.AreEqual(ea[i].Stat, eb[i].Stat);
                Assert.AreEqual(ea[i].MagnitudePercent, eb[i].MagnitudePercent);
            }
        }

        [Test]
        public void EquippedAttackItem_RaisesAttack()
        {
            var rules = LootRules();
            var state = IdleRpgState.NewGame();
            state.Equipped = new List<EquipmentItem>
            {
                new EquipmentItem(EquipSlot.Weapon, Rarity.Epic, StatKind.AttackPercent, 100, 1),
            };

            var game = new IdleRpg(rules, state, 0);

            Assert.AreEqual(20, game.Attack); // base 10 * (1 + 100%)
        }

        [Test]
        public void EquippedGoldFindItem_RaisesGoldReward()
        {
            var rules = LootRules();
            rules.DropChance = 0f; // isolate the gold-find multiplier from fresh drops

            var plain = new IdleRpg(rules);
            plain.Tick(1f); // one kill -> base gold 3

            var state = IdleRpgState.NewGame();
            state.Equipped = new List<EquipmentItem>
            {
                new EquipmentItem(EquipSlot.Trinket, Rarity.Rare, StatKind.GoldFindPercent, 100, 1),
            };
            var rich = new IdleRpg(rules, state, 0);
            rich.Tick(1f); // one kill -> gold 3 * (1 + 100%) = 6

            Assert.AreEqual(3, plain.Gold);
            Assert.AreEqual(6, rich.Gold);
        }
    }
}
