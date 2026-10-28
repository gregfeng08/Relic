using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Relic.Character
{
    /// <summary>
    /// JSON for HeroAppearance, same conventions as EffectJson: camelCase, enums as strings,
    /// unknown fields and bad enum names rejected (a vision model's output is validated, not guessed at).
    /// </summary>
    public static class AppearanceJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.None,
        };

        public static string Serialize(HeroAppearance a, bool indented = false) =>
            JsonConvert.SerializeObject(a, indented ? Formatting.Indented : Formatting.None, Settings);

        /// <summary>Parses and sanitizes. Throws JsonException on unknown fields or bad enum names.</summary>
        public static HeroAppearance Deserialize(string json)
        {
            var a = JsonConvert.DeserializeObject<HeroAppearance>(json, Settings);
            if (a == null) throw new JsonSerializationException("empty document");
            AppearanceRules.Sanitize(a);
            return a;
        }

        public static bool TryDeserialize(string json, out HeroAppearance appearance, out string error)
        {
            try { appearance = Deserialize(json); error = null; return true; }
            catch (JsonException e) { appearance = null; error = e.Message; return false; }
        }

        public static HeroAppearance Clone(HeroAppearance a) => Deserialize(Serialize(a));
    }

    /// <summary>Ranges and repairs: every value ends up buildable and inside the cute range.</summary>
    public static class AppearanceRules
    {
        public const int CurrentVersion = 1;
        public const int MaxAccessories = 3;
        public const float MinHeight = 0.92f, MaxHeight = 1.05f;
        public const float MinEyeSize = 0.85f, MaxEyeSize = 1.2f;

        /// <summary>Clamps ranges, fills missing parts, fixes bad colors and enums. Returns a list of what was changed.</summary>
        public static List<string> Sanitize(HeroAppearance a)
        {
            var fixes = new List<string>();
            if (a.Version != CurrentVersion) { fixes.Add("version"); a.Version = CurrentVersion; }
            if (string.IsNullOrWhiteSpace(a.Name)) { a.Name = "Wanderer"; fixes.Add("name"); }
            if (a.Name.Length > 32) { a.Name = a.Name.Substring(0, 32); fixes.Add("name"); }
            var d = new HeroAppearance();
            a.Body ??= d.Body;
            a.Face ??= d.Face;
            a.Hair ??= d.Hair;
            a.Outfit ??= d.Outfit;
            a.Outfit.Coat ??= d.Outfit.Coat;
            a.Outfit.Scarf ??= d.Outfit.Scarf;
            a.Accessories ??= new List<AccessorySpec>();

            a.Body.Height = Clamp(a.Body.Height, MinHeight, MaxHeight, "body.height", fixes);
            a.Body.Build = Clamp(a.Body.Build, 0f, 1f, "body.build", fixes);
            a.Body.SkinTone = Hex(a.Body.SkinTone, d.Body.SkinTone, "body.skinTone", fixes);
            a.Face.EyeSize = Clamp(a.Face.EyeSize, MinEyeSize, MaxEyeSize, "face.eyeSize", fixes);
            a.Face.EyeColor = Hex(a.Face.EyeColor, d.Face.EyeColor, "face.eyeColor", fixes);
            a.Face.EyeShape = Enum(a.Face.EyeShape, d.Face.EyeShape, "face.eyeShape", fixes);
            a.Hair.Style = Enum(a.Hair.Style, d.Hair.Style, "hair.style", fixes);
            a.Hair.Bangs = Enum(a.Hair.Bangs, d.Hair.Bangs, "hair.bangs", fixes);
            a.Hair.Length = Clamp(a.Hair.Length, 0f, 1f, "hair.length", fixes);
            a.Hair.Color = Hex(a.Hair.Color, d.Hair.Color, "hair.color", fixes);
            a.Hair.ShadeTint = Hex(a.Hair.ShadeTint, d.Hair.ShadeTint, "hair.shadeTint", fixes);
            OutfitSpec o = a.Outfit, od = d.Outfit;
            o.Coat.Length = Clamp(o.Coat.Length, 0f, 1f, "outfit.coat.length", fixes);
            o.Coat.Color = Hex(o.Coat.Color, od.Coat.Color, "outfit.coat.color", fixes);
            o.Coat.TrimColor = Hex(o.Coat.TrimColor, od.Coat.TrimColor, "outfit.coat.trimColor", fixes);
            o.Scarf.Color = Hex(o.Scarf.Color, od.Scarf.Color, "outfit.scarf.color", fixes);
            o.ShirtColor = Hex(o.ShirtColor, od.ShirtColor, "outfit.shirtColor", fixes);
            o.Legwear = Enum(o.Legwear, od.Legwear, "outfit.legwear", fixes);
            o.LegwearColor = Hex(o.LegwearColor, od.LegwearColor, "outfit.legwearColor", fixes);
            o.TightsColor = Hex(o.TightsColor, od.TightsColor, "outfit.tightsColor", fixes);
            o.BootsColor = Hex(o.BootsColor, od.BootsColor, "outfit.bootsColor", fixes);
            o.Hands = Enum(o.Hands, od.Hands, "outfit.hands", fixes);
            o.HandsColor = Hex(o.HandsColor, od.HandsColor, "outfit.handsColor", fixes);
            o.BeltColor = Hex(o.BeltColor, od.BeltColor, "outfit.beltColor", fixes);

            a.Accessories.RemoveAll(x => x == null);
            if (a.Accessories.Count > MaxAccessories) { a.Accessories.RemoveRange(MaxAccessories, a.Accessories.Count - MaxAccessories); fixes.Add("accessories"); }
            foreach (AccessorySpec x in a.Accessories)
            {
                x.Kind = Enum(x.Kind, AccessoryKind.HairPin, "accessory.kind", fixes);
                x.Side = Enum(x.Side, Side.Left, "accessory.side", fixes);
                x.Color = Hex(x.Color, "#E6B44A", "accessory.color", fixes);
            }
            return fixes;
        }

        static float Clamp(float v, float min, float max, string field, List<string> fixes)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) { fixes.Add(field); return (min + max) * 0.5f; }
            if (v < min) { fixes.Add(field); return min; }
            if (v > max) { fixes.Add(field); return max; }
            return v;
        }

        static string Hex(string v, string fallback, string field, List<string> fixes)
        {
            if (IsHex(v)) return v.StartsWith("#", StringComparison.Ordinal) ? v.ToUpperInvariant() : "#" + v.ToUpperInvariant();
            fixes.Add(field);
            return fallback;
        }

        public static bool IsHex(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            string h = v.StartsWith("#", StringComparison.Ordinal) ? v.Substring(1) : v;
            if (h.Length != 6) return false;
            foreach (char c in h)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
            return true;
        }

        static T Enum<T>(T v, T fallback, string field, List<string> fixes) where T : struct, System.Enum
        {
            if (System.Enum.IsDefined(typeof(T), v)) return v;
            fixes.Add(field);
            return fallback;
        }
    }
}
