using System.Collections.Generic;

namespace Relic.Character
{
    /// <summary>
    /// Built-in looks. Default is Greg's hero (white hair, red scarf, brown trench coat, olive
    /// trousers, gauntlets); the others prove the parameters really change the character.
    /// </summary>
    public static class AppearancePresets
    {
        public static HeroAppearance Default() => new HeroAppearance { Name = "Wanderer" };

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

        public static IReadOnlyList<HeroAppearance> All() => new[] { Default(), Mira(), Rowan(), Ash() };
    }
}
