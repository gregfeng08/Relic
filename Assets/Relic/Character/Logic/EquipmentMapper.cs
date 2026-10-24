using System;
using Relic.Core.Effects;
using Relic.Items;

namespace Relic.Character
{
    /// <summary>The five equipped items (any may be null).</summary>
    public sealed class HeroKit
    {
        public Item Weapon, Armor, Relic, Trinket1, Trinket2;
    }

    /// <summary>
    /// Item → visual mapping for the hero (docs/design/14-character.md "Shown equipment").
    /// Pure and deterministic: the same items always dress the hero the same way.
    /// </summary>
    public static class EquipmentMapper
    {
        public static HeroLook Map(HeroKit kit)
        {
            var look = new HeroLook();
            if (kit == null) kit = new HeroKit();
            look.Weapon = Weapon(kit.Weapon);
            look.Armor = Armor(kit.Armor);
            look.Amulet = Amulet(kit.Relic);
            look.Charms[0] = Charm(kit.Trinket1);
            look.Charms[1] = Charm(kit.Trinket2);
            foreach (Item i in new[] { kit.Weapon, kit.Armor, kit.Relic, kit.Trinket1, kit.Trinket2 })
                if (i != null && !string.IsNullOrEmpty(i.Name)) look.ItemNames.Add(i.Name);
            return look;
        }

        public static WeaponKind KindOf(BaseType t)
        {
            switch (t)
            {
                case BaseType.Sword: return WeaponKind.Sword;
                case BaseType.Greatsword: return WeaponKind.Greatsword;
                case BaseType.Bow: return WeaponKind.Bow;
                case BaseType.Staff: return WeaponKind.Staff;
                case BaseType.Tome: return WeaponKind.Tome;
                default: return WeaponKind.None;
            }
        }

        public static StowSlot StowFor(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Sword: return StowSlot.Hip;
                case WeaponKind.Tome: return StowSlot.BeltStrap;
                default: return StowSlot.Back;
            }
        }

        public static WeaponLook Weapon(Item item)
        {
            if (item == null || !ItemSlots.IsWeapon(item.BaseType)) return null;
            ItemVisual v = item.Visual ?? new ItemVisual();
            var w = new WeaponLook
            {
                Kind = KindOf(item.BaseType),
                Wear = Clamp01(v.Wear),
                GlowColor = Rgb.Hex(v.EmissiveColor),
                Glow = Math.Max(0f, Math.Min(4f, v.EmissiveIntensity)),
                Decal = string.IsNullOrEmpty(v.Decal) ? "none" : v.Decal,
                Name = item.Name,
            };
            w.Stow = StowFor(w.Kind);
            var parts = v.KitParts != null && v.KitParts.Count > 0 ? v.KitParts : Forms.TableKit(item.BaseType);
            foreach (string p in parts) if (!string.IsNullOrEmpty(p)) w.Parts.Add(p);
            EnsureCoreParts(w);

            bool wooden = w.Kind == WeaponKind.Bow || w.Kind == WeaponKind.Staff;
            bool bookish = w.Kind == WeaponKind.Tome;
            Rgb fallback = wooden ? MaterialPalette.DefaultWood : bookish ? MaterialPalette.Leather : MaterialPalette.DefaultMetal;
            Rgb primary = MaterialPalette.For(v.Material, fallback);
            Rgb accent = item.Rarity >= Rarity.Epic ? MaterialPalette.Gold
                       : wooden || bookish ? MaterialPalette.DefaultMetal : primary.Scale(0.8f);
            w.Primary = MaterialPalette.Weathered(primary, w.Wear);
            w.Secondary = MaterialPalette.Weathered(bookish ? new Rgb(0.86f, 0.80f, 0.66f) : MaterialPalette.Leather, w.Wear);
            w.Accent = MaterialPalette.Weathered(accent, w.Wear * 0.7f);
            return w;
        }

        /// <summary>A weapon must still read as itself if the kit list is odd (LLM relic vocab, empty lists).</summary>
        static void EnsureCoreParts(WeaponLook w)
        {
            switch (w.Kind)
            {
                case WeaponKind.Sword:
                case WeaponKind.Greatsword: w.Parts.Add("blade"); w.Parts.Add("grip"); break;
                case WeaponKind.Bow: w.Parts.Add("limb"); w.Parts.Add("string"); break;
                case WeaponKind.Staff: w.Parts.Add("haft"); w.Parts.Add("head"); break;
                case WeaponKind.Tome: w.Parts.Add("cover"); break;
            }
        }

        public static ArmorWeight WeightOf(BaseType t)
        {
            switch (t)
            {
                case BaseType.MediumArmor: return ArmorWeight.Medium;
                case BaseType.HeavyArmor: return ArmorWeight.Heavy;
                default: return ArmorWeight.Light;
            }
        }

        public static ArmorLook Armor(Item item)
        {
            var a = new ArmorLook
            {
                Weight = ArmorWeight.Light,
                Coat = MaterialPalette.Coat,
                CoatTrim = MaterialPalette.CoatTrim,
                Leather = MaterialPalette.Leather,
                Plate = MaterialPalette.DefaultMetal,
            };
            if (item == null || item.Slot != ItemSlot.Armor) return a;
            ItemVisual v = item.Visual ?? new ItemVisual();
            a.Name = item.Name;
            a.Weight = WeightOf(item.BaseType);
            a.Wear = Clamp01(v.Wear);
            Rgb mat = MaterialPalette.For(v.Material, a.Weight == ArmorWeight.Light ? MaterialPalette.Coat : MaterialPalette.DefaultMetal);
            if (a.Weight == ArmorWeight.Light)
            {
                // The hide tints the coat, but it stays Greg's brown trench coat.
                a.Coat = Rgb.Lerp(MaterialPalette.Coat, mat, 0.5f);
            }
            else
            {
                // Metal goes on the plates and studs; a heavier coat reads a shade darker.
                a.Plate = mat;
                a.Coat = Rgb.Lerp(MaterialPalette.Coat, mat, 0.12f).Scale(a.Weight == ArmorWeight.Heavy ? 0.86f : 0.94f);
                a.Leather = Rgb.Lerp(MaterialPalette.Leather, mat, 0.15f);
            }
            a.Coat = MaterialPalette.Weathered(a.Coat, a.Wear * 0.6f);
            a.CoatTrim = a.Coat.Scale(0.8f);
            a.Plate = MaterialPalette.Weathered(a.Plate, a.Wear);
            return a;
        }

        public static AmuletShape ShapeFor(string form)
        {
            switch (form)
            {
                case "lantern": case "censer": case "hourglass": case "music-box": return AmuletShape.Cage;
                case "bell": case "chalice": return AmuletShape.Bell;
                case "astrolabe": case "crown": case "seal-ring": return AmuletShape.Ring;
                case "key": case "horn": return AmuletShape.Key;
                case "mask": case "compass": case "mirror": return AmuletShape.Disc;
                default: return AmuletShape.Diamond;
            }
        }

        public static AmuletLook Amulet(Item relic)
        {
            var a = new AmuletLook { Shape = AmuletShape.Diamond, Metal = MaterialPalette.Gold, GlowColor = Rgb.Black, Glow = 0f };
            if (relic == null || relic.Slot != ItemSlot.Relic) return a;
            ItemVisual v = relic.Visual ?? new ItemVisual();
            a.Form = relic.Form;
            a.Name = relic.Name;
            a.Shape = ShapeFor(relic.Form);
            a.Metal = MaterialPalette.For(v.Material, MaterialPalette.Gold);
            a.GlowColor = Rgb.Hex(v.EmissiveColor);
            if (a.GlowColor.IsBlack) a.GlowColor = new Rgb(0.95f, 0.85f, 0.55f);
            // An equipped relic always glows a little (it is the hero's heirloom waking up).
            a.Glow = Math.Max(0.6f, Math.Min(4f, v.EmissiveIntensity));
            return a;
        }

        public static CharmShape CharmFor(string form)
        {
            switch (form)
            {
                case "brooch": return CharmShape.Brooch;
                case "signet": return CharmShape.Signet;
                case "token": return CharmShape.Token;
                default: return CharmShape.Ring;
            }
        }

        public static CharmLook Charm(Item trinket)
        {
            if (trinket == null || trinket.Slot != ItemSlot.Trinket) return null;
            ItemVisual v = trinket.Visual ?? new ItemVisual();
            var c = new CharmLook
            {
                Shape = CharmFor(trinket.Form),
                Metal = MaterialPalette.Weathered(MaterialPalette.For(v.Material, MaterialPalette.DefaultMetal), Clamp01(v.Wear)),
                GlowColor = Rgb.Hex(v.EmissiveColor),
                Name = trinket.Name,
            };
            c.Glow = trinket.Rarity >= Rarity.Rare && !c.GlowColor.IsBlack ? Math.Max(0.5f, Math.Min(4f, v.EmissiveIntensity)) : 0f;
            return c;
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
