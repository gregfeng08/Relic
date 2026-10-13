using System;
using System.Collections.Generic;

namespace Relic.Magic
{
    public enum Severity
    {
        Info,
        Warning,
        /// <summary>The spell can't be cast until this is fixed.</summary>
        Error,
    }

    /// <summary>A plain-language note about a spell drawing, for the player.</summary>
    public sealed class SpellDiagnostic
    {
        public Severity Severity;
        public string Message;
        /// <summary>The glyph this is about, if any (the desk highlights it).</summary>
        public GlyphInstance Glyph;

        public SpellDiagnostic(Severity severity, string message, GlyphInstance glyph = null)
        {
            Severity = severity;
            Message = message;
            Glyph = glyph;
        }

        public override string ToString() => $"{Severity}: {Message}";
    }

    /// <summary>Where on the circle a glyph sits, by distance from the center.</summary>
    public enum CircleBand
    {
        Center,
        Inner,
        Outer,
    }

    /// <summary>Glyph size relative to what its layer expects. Bigger glyphs mean more.</summary>
    public enum Emphasis
    {
        Small,
        Normal,
        Large,
    }

    public static class EmphasisRules
    {
        /// <summary>Size ratios (drawn / expected) below this read as Small…</summary>
        public const float SmallBelow = 0.72f;
        /// <summary>…and above this as Large. Wide on purpose: only deliberate size reads as emphasis.</summary>
        public const float LargeAbove = 1.35f;

        /// <summary>Expected glyph size (larger side of its box) as a fraction of the circle's radius.</summary>
        public static float NominalSize(GlyphRole role) => role == GlyphRole.Core ? 0.40f : 0.25f;

        /// <summary>Size the generator draws each emphasis at, relative to nominal.</summary>
        public static float DrawScale(Emphasis e) => e == Emphasis.Small ? 0.6f : e == Emphasis.Large ? 1.55f : 1f;

        /// <summary>Strength multiplier the compiler applies.</summary>
        public static float Strength(Emphasis e) => e == Emphasis.Small ? 0.75f : e == Emphasis.Large ? 1.3f : 1f;

        public static Emphasis FromRatio(float ratio) =>
            ratio < SmallBelow ? Emphasis.Small : ratio > LargeAbove ? Emphasis.Large : Emphasis.Normal;
    }

    /// <summary>One recognized glyph and where it sits in the circle.</summary>
    public sealed class GlyphInstance
    {
        public GlyphDefinition Glyph;
        /// <summary>0–1 drawing precision (1 for placed or pre-written glyphs).</summary>
        public float Precision = 1f;
        public float Confidence = 1f;
        public Emphasis Emphasis = Emphasis.Normal;
        public CircleBand Band;
        /// <summary>Radians clockwise from the top of the circle.</summary>
        public float Angle;
        /// <summary>Distance of the glyph's center from the circle's center, in circle radii.</summary>
        public float Radius;
        /// <summary>Larger side of the glyph's box, in circle radii (0 when not drawn).</summary>
        public float RelativeSize;
        /// <summary>Drawing-space box (empty when not drawn).</summary>
        public Bounds2 Bounds = Bounds2.Empty;
        /// <summary>Indices into the source drawing's strokes.</summary>
        public readonly List<int> StrokeIndices = new List<int>();

        public float Strength => EmphasisRules.Strength(Emphasis);

        public string Label => Emphasis == Emphasis.Normal ? Glyph.Name : $"{Glyph.Name} ({Emphasis.ToString().ToLowerInvariant()})";

        public override string ToString() => $"{Label} p={Precision:0.00}";
    }

    /// <summary>A group of strokes inside the circle that isn't any known glyph.</summary>
    public sealed class UnrecognizedMark
    {
        public Bounds2 Bounds;
        public readonly List<int> StrokeIndices = new List<int>();
        public Recognition BestGuess;
        public CircleBand Band;
        public float Angle;
    }

    /// <summary>
    /// A parsed spell circle: the core sigil, the inner modifiers and the ring
    /// glyphs in clockwise order from the top. Built from a drawing by
    /// <see cref="CircleParser"/>, or directly from a recipe (palette, found pages).
    /// </summary>
    public sealed class SpellCircle
    {
        public bool HasCircle;
        public bool IsClosed;
        public Vec2 Center;
        public float Radius;
        /// <summary>Radius (in circle radii) of an optional inner guide circle, if one was drawn.</summary>
        public float GuideRadius;
        public readonly List<int> CircleStrokeIndices = new List<int>();

        /// <summary>Every core sigil found (a valid spell has exactly one).</summary>
        public readonly List<GlyphInstance> Cores = new List<GlyphInstance>();
        public readonly List<GlyphInstance> Inner = new List<GlyphInstance>();
        /// <summary>Clockwise from the top: this order is the spell's sequence.</summary>
        public readonly List<GlyphInstance> Ring = new List<GlyphInstance>();
        public readonly List<UnrecognizedMark> Unrecognized = new List<UnrecognizedMark>();
        public readonly List<SpellDiagnostic> Diagnostics = new List<SpellDiagnostic>();

        public GlyphInstance Core => Cores.Count > 0 ? Cores[0] : null;

        public IEnumerable<GlyphInstance> AllGlyphs
        {
            get
            {
                foreach (var g in Cores) yield return g;
                foreach (var g in Inner) yield return g;
                foreach (var g in Ring) yield return g;
            }
        }

        public bool HasErrors
        {
            get
            {
                foreach (var d in Diagnostics)
                    if (d.Severity == Severity.Error)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Weighted mean drawing precision (the core counts double): the spell's
        /// "ink quality". 1 when nothing was drawn by hand.
        /// </summary>
        public float Quality
        {
            get
            {
                float sum = 0f, w = 0f;
                foreach (var g in AllGlyphs)
                {
                    float wt = g.Glyph.Role == GlyphRole.Core ? 2f : 1f;
                    sum += g.Precision * wt;
                    w += wt;
                }
                return w <= 0f ? 1f : sum / w;
            }
        }

        /// <summary>Builds a circle straight from a recipe, every glyph at the given precision.</summary>
        public static SpellCircle FromRecipe(SpellRecipe recipe, GlyphRegistry registry, float precision = 1f)
        {
            var c = new SpellCircle { HasCircle = true, IsClosed = true, Radius = 1f };
            if (recipe == null) return c;

            GlyphInstance Make(RecipeGlyph rg, CircleBand band, float angle)
            {
                if (!registry.TryGet(rg.Id, out var def))
                {
                    c.Diagnostics.Add(new SpellDiagnostic(Severity.Error, $"This page uses a glyph nobody here knows ('{rg.Id}')."));
                    return null;
                }
                return new GlyphInstance { Glyph = def, Precision = precision, Emphasis = rg.Emphasis, Band = band, Angle = angle };
            }

            if (!string.IsNullOrEmpty(recipe.Core))
            {
                var core = Make(new RecipeGlyph(recipe.Core), CircleBand.Center, 0f);
                if (core != null) Sort(c, core);
            }
            for (int i = 0; i < recipe.Inner.Count; i++)
            {
                var g = Make(recipe.Inner[i], CircleBand.Inner, SlotAngle(i, recipe.Inner.Count));
                if (g != null) Sort(c, g);
            }
            for (int i = 0; i < recipe.Ring.Count; i++)
            {
                var g = Make(recipe.Ring[i], CircleBand.Outer, SlotAngle(i, recipe.Ring.Count));
                if (g != null) Sort(c, g);
            }
            return c;
        }

        /// <summary>Angle of slot i of n, clockwise from the top.</summary>
        public static float SlotAngle(int i, int n) => n <= 0 ? 0f : (float)(2 * Math.PI * i / n);

        /// <summary>Files a glyph under its role.</summary>
        internal static void Sort(SpellCircle c, GlyphInstance g)
        {
            switch (g.Glyph.Role)
            {
                case GlyphRole.Core: c.Cores.Add(g); break;
                case GlyphRole.Inner: c.Inner.Add(g); break;
                default: c.Ring.Add(g); break;
            }
        }

        /// <summary>Converts the circle back into a recipe (what a scholar would copy down).</summary>
        public SpellRecipe ToRecipe()
        {
            var r = new SpellRecipe { Core = Core?.Glyph.Id };
            foreach (var g in Inner) r.Inner.Add(new RecipeGlyph(g.Glyph.Id, g.Emphasis));
            foreach (var g in Ring) r.Ring.Add(new RecipeGlyph(g.Glyph.Id, g.Emphasis));
            return r;
        }
    }
}
