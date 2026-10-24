using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Items;

namespace Relic.Character.Tests
{
    public sealed class EquipmentMapperTests
    {
        [Test]
        public void EmptyKitIsThePlainHero()
        {
            HeroLook look = EquipmentMapper.Map(new HeroKit());
            Assert.IsNull(look.Weapon);
            Assert.AreEqual(ArmorWeight.Light, look.Armor.Weight);
            Assert.AreEqual(MaterialPalette.Coat, look.Armor.Coat);
            Assert.IsFalse(look.Amulet.Glows, "no relic: the amulet is dormant");
            Assert.IsNull(look.Charms[0]);
            Assert.AreEqual(0, look.ItemNames.Count);
        }

        [Test]
        public void WeaponsStowWhereTheBriefSays()
        {
            Assert.AreEqual(StowSlot.Back, EquipmentMapper.StowFor(WeaponKind.Staff));
            Assert.AreEqual(StowSlot.Back, EquipmentMapper.StowFor(WeaponKind.Bow));
            Assert.AreEqual(StowSlot.Back, EquipmentMapper.StowFor(WeaponKind.Greatsword));
            Assert.AreEqual(StowSlot.Hip, EquipmentMapper.StowFor(WeaponKind.Sword));
            Assert.AreEqual(StowSlot.BeltStrap, EquipmentMapper.StowFor(WeaponKind.Tome));
        }

        [Test]
        public void ArmorWeightFollowsBaseType()
        {
            var roller = new KitRoller(11);
            foreach (var bt in new[] { BaseType.LightArmor, BaseType.MediumArmor, BaseType.HeavyArmor })
            {
                HeroKit kit = roller.Roll(3, 0, BaseType.Sword, bt);
                HeroLook look = EquipmentMapper.Map(kit);
                Assert.AreEqual(EquipmentMapper.WeightOf(bt), look.Armor.Weight);
            }
        }

        [Test]
        public void RelicMakesTheAmuletGlow()
        {
            HeroKit kit = new KitRoller(5).Roll(2, 1);
            HeroLook look = EquipmentMapper.Map(kit);
            Assert.AreEqual(ItemSlot.Relic, kit.Relic.Slot);
            Assert.IsTrue(look.Amulet.Glows);
            Assert.AreEqual(EquipmentMapper.ShapeFor(kit.Relic.Form), look.Amulet.Shape);
        }

        [Test]
        public void RandomKitsMapForEveryTier()
        {
            var roller = new KitRoller(1337);
            for (int tier = 0; tier <= 10; tier++)
            {
                for (int roll = 0; roll < 3; roll++)
                {
                    HeroKit kit = roller.Roll(tier, roll);
                    HeroLook look = EquipmentMapper.Map(kit);
                    Assert.IsNotNull(look.Weapon, "tier " + tier);
                    Assert.AreNotEqual(WeaponKind.None, look.Weapon.Kind);
                    Assert.IsNotNull(look.Charms[0]);
                    Assert.IsNotNull(look.Charms[1]);
                    Assert.AreEqual(5, look.ItemNames.Count);
                    Assert.Greater(look.Weapon.Parts.Count, 0);
                }
            }
        }

        [Test]
        public void KitsAreDeterministic()
        {
            HeroKit a = new KitRoller(42).Roll(4, 7);
            HeroKit b = new KitRoller(42).Roll(4, 7);
            Assert.AreEqual(a.Weapon.Name, b.Weapon.Name);
            Assert.AreEqual(a.Armor.Name, b.Armor.Name);
            Assert.AreEqual(a.Relic.Name, b.Relic.Name);
        }

        [Test]
        public void MaterialWordsHaveTints()
        {
            Assert.IsTrue(MaterialPalette.IsKnown("iron"));
            Assert.IsTrue(MaterialPalette.IsKnown("Black Yew"));
            Rgb gold = MaterialPalette.For("tarnished gold leaf", Rgb.Black);
            Assert.Greater(gold.R, 0.8f);
            Assert.AreEqual(Rgb.White, MaterialPalette.For("???", Rgb.White));
            Assert.AreEqual(new Rgb(1f, 0f, 0f), Rgb.Hex("#FF0000"));
            Assert.IsTrue(Rgb.Hex("nope").IsBlack);
        }

        [Test]
        public void GlowNeedsRareTrinkets()
        {
            var item = new Item { BaseType = BaseType.Trinket, Slot = ItemSlot.Trinket, Rarity = Rarity.Common, Form = "signet" };
            item.Visual.EmissiveColor = "#ff8800";
            item.Visual.EmissiveIntensity = 1f;
            Assert.IsFalse(EquipmentMapper.Charm(item).Glows);
            item.Rarity = Rarity.Epic;
            Assert.IsTrue(EquipmentMapper.Charm(item).Glows);
            Assert.AreEqual(CharmShape.Signet, EquipmentMapper.Charm(item).Shape);
        }
    }
}
