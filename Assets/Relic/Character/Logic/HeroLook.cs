using System.Collections.Generic;

namespace Relic.Character
{
    public enum WeaponKind { None, Sword, Greatsword, Bow, Staff, Tome }

    /// <summary>Where a weapon rides when it isn't drawn (14-character "Shown equipment").</summary>
    public enum StowSlot { Back, Hip, BeltStrap }

    public enum ArmorWeight { Light, Medium, Heavy }

    public enum AmuletShape { Diamond, Ring, Bell, Key, Cage, Disc }

    public enum CharmShape { Ring, Brooch, Signet, Token }

    /// <summary>How the weapon is built: which kit parts, which colors, how worn, how bright.</summary>
    public sealed class WeaponLook
    {
        public WeaponKind Kind;
        public StowSlot Stow;
        public readonly HashSet<string> Parts = new HashSet<string>();
        /// <summary>Blade/limbs/haft/cover: the item's material.</summary>
        public Rgb Primary;
        /// <summary>Grip wrap, string, binding.</summary>
        public Rgb Secondary;
        /// <summary>Crossguard, pommel, bands, clasp.</summary>
        public Rgb Accent;
        public float Wear;
        public Rgb GlowColor;
        /// <summary>0..4 (ItemVisual.EmissiveIntensity); 0 = no glow.</summary>
        public float Glow;
        public string Decal = "none";
        public string Name;

        public bool Has(string part) => Parts.Contains(part);
        public bool Glows => Glow > 0.01f && !GlowColor.IsBlack;
    }

    public sealed class ArmorLook
    {
        public ArmorWeight Weight;
        public Rgb Coat;
        public Rgb CoatTrim;
        /// <summary>Leather pieces (Medium and up).</summary>
        public Rgb Leather;
        /// <summary>Metal plates (Heavy).</summary>
        public Rgb Plate;
        public float Wear;
        public string Name;
    }

    public sealed class AmuletLook
    {
        public AmuletShape Shape;
        public Rgb Metal;
        public Rgb GlowColor;
        /// <summary>EmissiveIntensity, 0 when no relic is equipped (the amulet is dormant).</summary>
        public float Glow;
        public string Form;
        public string Name;
        public bool Glows => Glow > 0.01f && !GlowColor.IsBlack;
    }

    public sealed class CharmLook
    {
        public CharmShape Shape;
        public Rgb Metal;
        public Rgb GlowColor;
        public float Glow;
        public string Name;
        public bool Glows => Glow > 0.01f && !GlowColor.IsBlack;
    }

    /// <summary>Everything the runtime needs to dress the hero. Built by <see cref="EquipmentMapper"/>.</summary>
    public sealed class HeroLook
    {
        public WeaponLook Weapon;          // null = unarmed
        public ArmorLook Armor;            // never null (no armor = the plain coat, Light)
        public AmuletLook Amulet;          // never null (the inherited amulet is always worn)
        public readonly CharmLook[] Charms = new CharmLook[2];   // entries may be null
        public readonly List<string> ItemNames = new List<string>();
    }
}
