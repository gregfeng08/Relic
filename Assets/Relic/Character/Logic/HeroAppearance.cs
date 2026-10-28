using System.Collections.Generic;

namespace Relic.Character
{
    // The hero's look as data (docs/design/14-character.md). The plan for player customization
    // is image → vision model → this spec → the procedural builder, never image → mesh. Plain
    // classes with public fields (C# 9, Newtonsoft, dotnet harness); colors are "#RRGGBB".
    // Every field has a safe default and AppearanceRules.Sanitize clamps anything out of range.

    public enum HairStyle { LowTail, LongStraight, Ponytail, TwinTails, Bob }
    public enum BangsStyle { Swept, Straight, Parted }
    public enum EyeShape { Round, Soft, Sharp }
    public enum LegwearKind { Trousers, Skirt }
    public enum HandsKind { Gauntlets, Gloves, Bare }
    public enum AccessoryKind { HairPin, Flower, Ribbon }
    public enum Side { Left, Right }

    public sealed class BodySpec
    {
        /// <summary>Overall scale, 0.92..1.05 (1 = about 1.69 m).</summary>
        public float Height = 1f;
        /// <summary>0 = very slender .. 1 = sturdier. Stays in the cute range.</summary>
        public float Build = 0.35f;
        public string SkinTone = "#FAD4B3";
    }

    public sealed class FaceSpec
    {
        public string EyeColor = "#3A9E5C";
        public EyeShape EyeShape = EyeShape.Soft;
        /// <summary>0.85..1.2.</summary>
        public float EyeSize = 1f;
    }

    public sealed class HairSpec
    {
        public HairStyle Style = HairStyle.LowTail;
        /// <summary>0 = shoulder .. 1 = hip (tails and long hair).</summary>
        public float Length = 0.85f;
        public BangsStyle Bangs = BangsStyle.Swept;
        /// <summary>The long forward lock over the left shoulder (curled).</summary>
        public bool SideLock = true;
        public bool Cowlick = true;
        public string Color = "#F7F2E8";
        /// <summary>Tint of the shaded side (warm for light hair).</summary>
        public string ShadeTint = "#F0C9B8";
    }

    public sealed class CoatSpec
    {
        public bool On = true;
        /// <summary>0 = hip-length jacket .. 1 = below the knee.</summary>
        public float Length = 1f;
        public string Color = "#B57B42";
        public string TrimColor = "#E6B44A";
    }

    public sealed class ScarfSpec
    {
        public bool On = true;
        public string Color = "#D9553D";
    }

    public sealed class OutfitSpec
    {
        public CoatSpec Coat = new CoatSpec();
        public ScarfSpec Scarf = new ScarfSpec();
        public string ShirtColor = "#7A5E50";
        public LegwearKind Legwear = LegwearKind.Trousers;
        public string LegwearColor = "#6A8A55";
        /// <summary>Skirt only: the color of the tights below it.</summary>
        public string TightsColor = "#4A3F48";
        public string BootsColor = "#8C5229";
        public HandsKind Hands = HandsKind.Gauntlets;
        public string HandsColor = "#8C9099";
        public string BeltColor = "#EEB834";
    }

    public sealed class AccessorySpec
    {
        public AccessoryKind Kind = AccessoryKind.HairPin;
        public string Color = "#E6B44A";
        public Side Side = Side.Left;
    }

    public sealed class HeroAppearance
    {
        public int Version = 1;
        public string Name = "Wanderer";
        public BodySpec Body = new BodySpec();
        public FaceSpec Face = new FaceSpec();
        public HairSpec Hair = new HairSpec();
        public OutfitSpec Outfit = new OutfitSpec();
        /// <summary>At most AppearanceRules.MaxAccessories.</summary>
        public List<AccessorySpec> Accessories = new List<AccessorySpec>();
    }
}
