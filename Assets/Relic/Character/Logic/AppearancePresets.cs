using System.Collections.Generic;

namespace Relic.Character
{
    /// <summary>
    /// Built-in looks. Default is Greg's hero (white hair, red scarf, brown trench coat, olive
    /// trousers, gauntlets); Unwritten is the starting body (and the sandbox default); the others
    /// prove the parameters really change the character.
    /// </summary>
    public static class AppearancePresets
    {
        public static HeroAppearance Default() => new HeroAppearance { Name = "Wanderer" };

        /// <summary>
        /// The unwritten wayfarer (docs/design/14-character.md, Decision 2026-11-18): the body every
        /// player starts in, before the Narrator has written them. A blank page: a soft, rounded
        /// figure in a plain parchment cloak with the hood up, an ink-dark fringe, two ink-dot eyes
        /// and a blush (no mouth yet), soft mittens and cloth-wrapped boots, no pack. A faded red
        /// bookmark ribbon trails from the hood's tip, the one mark of the book they belong to.
        /// </summary>
        public static HeroAppearance Unwritten()
        {
            var a = new HeroAppearance { Name = "Unwritten" };
            a.Body.Height = 0.94f;
            a.Body.Build = 0.6f;
            a.Body.HeadSize = 1.1f;
            a.Body.SkinTone = "#F7E1CE";
            a.Face.Style = FaceStyle.Hint;
            a.Face.EyeColor = "#3B3848";
            a.Face.EyeShape = EyeShape.Round;
            a.Face.EyeSize = 1f;
            a.Hair.Style = HairStyle.Bob;
            a.Hair.Length = 0f;
            a.Hair.Bangs = BangsStyle.Straight;
            a.Hair.SideLock = false;
            a.Hair.Cowlick = false;
            a.Hair.Color = "#4F5368";
            a.Hair.ShadeTint = "#8C90AC";
            a.Outfit.Coat.Style = CoatStyle.Cloak;
            a.Outfit.Coat.HoodUp = true;
            a.Outfit.Coat.Length = 0.6f;
            a.Outfit.Coat.Color = "#EAD8B4";
            a.Outfit.Coat.TrimColor = "#C8A975";
            a.Outfit.Scarf.On = false;
            a.Outfit.ShirtColor = "#7F89A3";
            a.Outfit.Legwear = LegwearKind.Trousers;
            a.Outfit.LegwearColor = "#8A8FA6";
            a.Outfit.BootsColor = "#A88D6E";
            a.Outfit.Hands = HandsKind.Gloves;
            a.Outfit.HandsColor = "#B59B7C";
            a.Outfit.BeltColor = "#BCA88A";
            a.Outfit.Pack = false;
            a.Accessories.Add(new AccessorySpec { Kind = AccessoryKind.Bookmark, Color = "#C95F4E", Side = Side.Right });
            return a;
        }

        /// <summary>A preset by name (case-insensitive), or null.</summary>
        public static HeroAppearance ByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (HeroAppearance a in All())
                if (string.Equals(a.Name, name.Trim(), System.StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        /// <summary>Black twin tails, short blue jacket, skirt, gloves, a flower pin, no scarf.</summary>
        public static HeroAppearance Mira()
        {
            var a = new HeroAppearance { Name = "Mira" };
            a.Body.Height = 0.95f;
            a.Body.Build = 0.2f;
            a.Body.SkinTone = "#F6D2BC";
            a.Face.EyeColor = "#5A6FD6";
            a.Face.EyeShape = EyeShape.Round;
            a.Face.EyeSize = 1.1f;
            a.Hair.Style = HairStyle.TwinTails;
            a.Hair.Length = 0.7f;
            a.Hair.Bangs = BangsStyle.Straight;
            a.Hair.SideLock = false;
            a.Hair.Cowlick = false;
            a.Hair.Color = "#2E2A3A";
            a.Hair.ShadeTint = "#7A7FB0";
            a.Outfit.Coat.Length = 0.15f;
            a.Outfit.Coat.Color = "#4C6FB8";
            a.Outfit.Coat.TrimColor = "#F2F2F2";
            a.Outfit.Scarf.On = false;
            a.Outfit.ShirtColor = "#F3EDE4";
            a.Outfit.Legwear = LegwearKind.Skirt;
            a.Outfit.LegwearColor = "#2F3F6E";
            a.Outfit.TightsColor = "#2B2730";
            a.Outfit.BootsColor = "#5A3A2C";
            a.Outfit.Hands = HandsKind.Gloves;
            a.Outfit.HandsColor = "#F3EDE4";
            a.Outfit.BeltColor = "#C9A24A";
            a.Accessories.Add(new AccessorySpec { Kind = AccessoryKind.Flower, Color = "#F48FB1", Side = Side.Right });
            return a;
        }

        /// <summary>Auburn high ponytail, mid-length green coat, yellow scarf, gauntlets, a hair pin.</summary>
        public static HeroAppearance Rowan()
        {
            var a = new HeroAppearance { Name = "Rowan" };
            a.Body.Height = 1.02f;
            a.Body.Build = 0.5f;
            a.Body.SkinTone = "#E8B994";
            a.Face.EyeColor = "#B5772E";
            a.Face.EyeShape = EyeShape.Sharp;
            a.Hair.Style = HairStyle.Ponytail;
            a.Hair.Length = 0.75f;
            a.Hair.Bangs = BangsStyle.Parted;
            a.Hair.SideLock = false;
            a.Hair.Color = "#A3472A";
            a.Hair.ShadeTint = "#C27A6A";
            a.Outfit.Coat.Length = 0.55f;
            a.Outfit.Coat.Color = "#4F7A55";
            a.Outfit.Coat.TrimColor = "#D8B25A";
            a.Outfit.Scarf.Color = "#E8C04A";
            a.Outfit.ShirtColor = "#E9DFC9";
            a.Outfit.LegwearColor = "#5B4636";
            a.Outfit.BootsColor = "#3E2E26";
            a.Accessories.Add(new AccessorySpec { Kind = AccessoryKind.HairPin, Color = "#D8B25A", Side = Side.Left });
            return a;
        }

        /// <summary>Silver-lilac bob, no coat, a ribbon, bare hands.</summary>
        public static HeroAppearance Ash()
        {
            var a = new HeroAppearance { Name = "Ash" };
            a.Body.Build = 0.3f;
            a.Face.EyeColor = "#B04A7A";
            a.Face.EyeShape = EyeShape.Round;
            a.Hair.Style = HairStyle.Bob;
            a.Hair.Length = 0.3f;
            a.Hair.Bangs = BangsStyle.Straight;
            a.Hair.SideLock = false;
            a.Hair.Color = "#D9D3EC";
            a.Hair.ShadeTint = "#B6A6D6";
            a.Outfit.Coat.On = false;
            a.Outfit.Scarf.Color = "#6A4FA0";
            a.Outfit.ShirtColor = "#8E6FB8";
            a.Outfit.Legwear = LegwearKind.Skirt;
            a.Outfit.LegwearColor = "#3A3350";
            a.Outfit.Hands = HandsKind.Bare;
            a.Accessories.Add(new AccessorySpec { Kind = AccessoryKind.Ribbon, Color = "#E35D8C", Side = Side.Right });
            return a;
        }

        /// <summary>F11 order: the unwritten starting body first, then the written looks.</summary>
        public static IReadOnlyList<HeroAppearance> All() => new[] { Unwritten(), Default(), Mira(), Rowan(), Ash() };
    }
}
