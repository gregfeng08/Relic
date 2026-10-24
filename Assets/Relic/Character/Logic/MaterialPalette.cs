using System;
using System.Collections.Generic;

namespace Relic.Character
{
    /// <summary>An sRGB color as three floats in [0, 1] (no UnityEngine in Logic).</summary>
    public struct Rgb : IEquatable<Rgb>
    {
        public float R, G, B;

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static readonly Rgb Black = new Rgb(0f, 0f, 0f);
        public static readonly Rgb White = new Rgb(1f, 1f, 1f);

        public bool IsBlack => R + G + B < 0.02f;
        public float Luma => 0.299f * R + 0.587f * G + 0.114f * B;

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        public Rgb Scale(float k) => new Rgb(Clamp(R * k), Clamp(G * k), Clamp(B * k));

        /// <summary>"#RRGGBB" (or "RRGGBB"); anything unparsable is black.</summary>
        public static Rgb Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Black;
            string h = hex.Trim().TrimStart('#');
            if (h.Length != 6) return Black;
            try
            {
                int v = Convert.ToInt32(h, 16);
                return new Rgb(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
            }
            catch (FormatException) { return Black; }
        }

        static float Clamp(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        public bool Equals(Rgb o) => R == o.R && G == o.G && B == o.B;
        public override bool Equals(object obj) => obj is Rgb o && Equals(o);
        public override int GetHashCode() => (R.GetHashCode() * 397 ^ G.GetHashCode()) * 397 ^ B.GetHashCode();
        public override string ToString() => "(" + R.ToString("0.00") + ", " + G.ToString("0.00") + ", " + B.ToString("0.00") + ")";
    }

    /// <summary>
    /// The hero's base colors (from Greg's reference sheet) and the item material words
    /// (Items Forms tables) mapped to tints. Unknown words fall back to keyword guesses.
    /// </summary>
    public static class MaterialPalette
    {
        // Reference sheet colors.
        public static readonly Rgb Skin = new Rgb(0.98f, 0.83f, 0.70f);
        public static readonly Rgb Hair = new Rgb(0.97f, 0.95f, 0.91f);
        public static readonly Rgb Scarf = new Rgb(0.84f, 0.33f, 0.24f);
        public static readonly Rgb Coat = new Rgb(0.70f, 0.48f, 0.25f);
        public static readonly Rgb CoatTrim = new Rgb(0.56f, 0.37f, 0.19f);
        public static readonly Rgb Shirt = new Rgb(0.46f, 0.36f, 0.30f);
        public static readonly Rgb Belt = new Rgb(0.93f, 0.72f, 0.18f);
        public static readonly Rgb Gold = new Rgb(0.90f, 0.66f, 0.20f);
        public static readonly Rgb Trousers = new Rgb(0.38f, 0.50f, 0.32f);
        public static readonly Rgb Boots = new Rgb(0.52f, 0.30f, 0.14f);
        public static readonly Rgb Sole = new Rgb(0.16f, 0.12f, 0.11f);
        public static readonly Rgb Gauntlet = new Rgb(0.50f, 0.52f, 0.56f);
        public static readonly Rgb Eye = new Rgb(0.22f, 0.62f, 0.36f);
        public static readonly Rgb Leather = new Rgb(0.42f, 0.27f, 0.16f);
        public static readonly Rgb Canvas = new Rgb(0.62f, 0.55f, 0.42f);
        public static readonly Rgb Cord = new Rgb(0.30f, 0.22f, 0.16f);
        public static readonly Rgb HealthPotion = new Rgb(0.88f, 0.20f, 0.22f);
        public static readonly Rgb ManaPotion = new Rgb(0.25f, 0.45f, 0.95f);
        public static readonly Rgb DefaultMetal = new Rgb(0.55f, 0.57f, 0.60f);
        public static readonly Rgb DefaultWood = new Rgb(0.60f, 0.42f, 0.26f);

        static readonly Dictionary<string, Rgb> Words = new Dictionary<string, Rgb>(StringComparer.OrdinalIgnoreCase)
        {
            // metals
            { "iron", new Rgb(0.46f, 0.47f, 0.50f) }, { "steel", new Rgb(0.63f, 0.65f, 0.69f) },
            { "marsh-iron", new Rgb(0.40f, 0.44f, 0.40f) }, { "kiln-steel", new Rgb(0.60f, 0.53f, 0.48f) },
            { "tempered steel", new Rgb(0.70f, 0.72f, 0.76f) }, { "blued steel", new Rgb(0.40f, 0.49f, 0.64f) },
            { "bell-steel", new Rgb(0.74f, 0.63f, 0.42f) }, { "star-iron", new Rgb(0.58f, 0.60f, 0.74f) },
            { "weir-steel", new Rgb(0.50f, 0.61f, 0.63f) },
            // woods
            { "ash", new Rgb(0.78f, 0.66f, 0.48f) }, { "yew", new Rgb(0.70f, 0.45f, 0.28f) }, { "elm", new Rgb(0.62f, 0.48f, 0.34f) },
            { "bog-oak", new Rgb(0.32f, 0.25f, 0.21f) }, { "hornbeam", new Rgb(0.74f, 0.68f, 0.56f) }, { "ironwood", new Rgb(0.38f, 0.29f, 0.25f) },
            { "black yew", new Rgb(0.23f, 0.18f, 0.17f) }, { "heartwood", new Rgb(0.60f, 0.27f, 0.18f) }, { "salt-cured yew", new Rgb(0.78f, 0.72f, 0.63f) },
            // hides (light armor)
            { "wool", new Rgb(0.62f, 0.55f, 0.44f) }, { "boiled leather", new Rgb(0.62f, 0.40f, 0.22f) }, { "quilted", new Rgb(0.68f, 0.54f, 0.34f) },
            { "oxhide", new Rgb(0.56f, 0.36f, 0.22f) }, { "studded", new Rgb(0.50f, 0.36f, 0.25f) }, { "sealskin", new Rgb(0.42f, 0.40f, 0.38f) },
            { "lamellar", new Rgb(0.53f, 0.35f, 0.24f) }, { "elkhide", new Rgb(0.72f, 0.53f, 0.34f) }, { "riveted", new Rgb(0.46f, 0.34f, 0.25f) },
            // bindings (tomes)
            { "calfskin", new Rgb(0.70f, 0.52f, 0.36f) }, { "vellum", new Rgb(0.88f, 0.82f, 0.68f) }, { "ledger-bound", new Rgb(0.42f, 0.26f, 0.18f) },
            { "gilt vellum", new Rgb(0.90f, 0.78f, 0.50f) }, { "clasped", new Rgb(0.48f, 0.30f, 0.22f) }, { "ironbound", new Rgb(0.33f, 0.29f, 0.27f) },
            { "chained", new Rgb(0.37f, 0.34f, 0.33f) }, { "saltbound", new Rgb(0.80f, 0.80f, 0.76f) }, { "lead-sealed", new Rgb(0.43f, 0.45f, 0.49f) },
        };

        static readonly (string key, Rgb color)[] Keywords =
        {
            ("gold", new Rgb(0.92f, 0.70f, 0.24f)), ("gilt", new Rgb(0.92f, 0.70f, 0.24f)), ("brass", new Rgb(0.80f, 0.64f, 0.30f)),
            ("bronze", new Rgb(0.70f, 0.48f, 0.26f)), ("copper", new Rgb(0.76f, 0.44f, 0.28f)), ("silver", new Rgb(0.80f, 0.82f, 0.86f)),
            ("pewter", new Rgb(0.56f, 0.57f, 0.58f)), ("lead", new Rgb(0.43f, 0.45f, 0.49f)), ("bone", new Rgb(0.90f, 0.86f, 0.74f)),
            ("ivory", new Rgb(0.94f, 0.90f, 0.80f)), ("glass", new Rgb(0.70f, 0.85f, 0.88f)), ("obsidian", new Rgb(0.16f, 0.14f, 0.18f)),
            ("jade", new Rgb(0.36f, 0.66f, 0.48f)), ("amber", new Rgb(0.90f, 0.58f, 0.20f)), ("coral", new Rgb(0.90f, 0.48f, 0.42f)),
            ("salt", new Rgb(0.86f, 0.86f, 0.82f)), ("stone", new Rgb(0.56f, 0.54f, 0.50f)), ("horn", new Rgb(0.46f, 0.36f, 0.28f)),
            ("oak", new Rgb(0.55f, 0.40f, 0.26f)), ("wood", DefaultWood), ("leather", new Rgb(0.55f, 0.36f, 0.22f)),
            ("hide", new Rgb(0.55f, 0.38f, 0.24f)), ("iron", new Rgb(0.46f, 0.47f, 0.50f)), ("steel", new Rgb(0.63f, 0.65f, 0.69f)),
            ("wax", new Rgb(0.90f, 0.84f, 0.66f)), ("pearl", new Rgb(0.92f, 0.90f, 0.88f)), ("ebony", new Rgb(0.18f, 0.14f, 0.13f)),
        };

        /// <summary>Tint for an item material word; <paramref name="fallback"/> when the word means nothing to us.</summary>
        public static Rgb For(string word, Rgb fallback)
        {
            if (string.IsNullOrWhiteSpace(word)) return fallback;
            string w = word.Trim();
            if (Words.TryGetValue(w, out Rgb c)) return c;
            string lower = w.ToLowerInvariant();
            foreach (var k in Keywords)
                if (lower.Contains(k.key)) return k.color;
            return fallback;
        }

        public static bool IsKnown(string word) => !string.IsNullOrWhiteSpace(word) && Words.ContainsKey(word.Trim());

        /// <summary>Wear 0..1 greys and darkens a color a little (scuffed, not filthy).</summary>
        public static Rgb Weathered(Rgb c, float wear)
        {
            wear = wear < 0f ? 0f : wear > 1f ? 1f : wear;
            float g = c.Luma;
            var grey = new Rgb(g, g * 0.97f, g * 0.93f).Scale(0.82f);
            return Rgb.Lerp(c, grey, wear * 0.45f);
        }
    }
}
