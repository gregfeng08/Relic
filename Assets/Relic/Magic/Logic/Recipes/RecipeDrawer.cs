using System;
using System.Collections.Generic;

namespace Relic.Magic
{
    /// <summary>Where one glyph goes on a page.</summary>
    public sealed class GlyphPlacement
    {
        public GlyphDefinition Glyph;
        public Emphasis Emphasis;
        public CircleBand Band;
        public Vec2 Center;
        /// <summary>Larger side of the glyph's box, in drawing units.</summary>
        public float Size;
    }

    /// <summary>A whole spell laid out on a page: the circle plus every glyph's placement.</summary>
    public sealed class SpellLayout
    {
        public Vec2 Center;
        public float Radius;
        public readonly List<GlyphPlacement> Placements = new List<GlyphPlacement>();

        /// <summary>
        /// Strokes a scribe would draw: the circle first (clockwise from the top),
        /// then each glyph's template strokes, core first, then inner, then ring.
        /// </summary>
        public Drawing ToDrawing()
        {
            var d = new Drawing();
            d.Strokes.Add(RecipeDrawer.CircleStroke(Center, Radius));
            foreach (var p in Placements)
                d.Strokes.AddRange(p.Glyph.Render(p.Center, p.Size));
            return d;
        }
    }

    /// <summary>
    /// Builds the drawing of a pre-written spell (a found tome page, a staff
    /// carving, a teacher's example) from its recipe, so the player can see and
    /// study the glyphs. Round-trips: recipe → drawing → CircleParser →
    /// SpellCompiler gives the same program as compiling the recipe directly.
    /// </summary>
    public static class RecipeDrawer
    {
        /// <summary>Where inner glyphs sit, in circle radii from the center.</summary>
        public const float InnerRadius = 0.46f;
        /// <summary>Where ring glyphs sit, in circle radii from the center.</summary>
        public const float RingRadius = 0.8f;

        public static SpellLayout Layout(SpellRecipe recipe, GlyphRegistry registry, Vec2 center, float radius)
        {
            var layout = new SpellLayout { Center = center, Radius = radius };
            if (recipe == null) return layout;

            if (recipe.Core != null && registry.TryGet(recipe.Core, out var core))
                layout.Placements.Add(Place(core, Emphasis.Normal, CircleBand.Center, center, radius));

            int nRing = recipe.Ring.Count, nInner = recipe.Inner.Count;
            // Stagger inner glyphs by up to half a slot so big glyphs don't collide
            // (never a whole slot, so the inner glyphs keep their clockwise order).
            float stagger = nRing > 0 && nInner > 1 ? (float)Math.Min(Math.PI / nRing, Math.PI / nInner) : 0f;
            for (int i = 0; i < nInner; i++)
            {
                if (!registry.TryGet(recipe.Inner[i].Id, out var g)) continue;
                float a = SpellCircle.SlotAngle(i, nInner) + stagger;
                layout.Placements.Add(Place(g, recipe.Inner[i].Emphasis, CircleBand.Inner, center + OnCircle(a) * (InnerRadius * radius), radius));
            }
            for (int i = 0; i < nRing; i++)
            {
                if (!registry.TryGet(recipe.Ring[i].Id, out var g)) continue;
                float a = SpellCircle.SlotAngle(i, nRing);
                layout.Placements.Add(Place(g, recipe.Ring[i].Emphasis, CircleBand.Outer, center + OnCircle(a) * (RingRadius * radius), radius));
            }
            return layout;
        }

        public static Drawing Draw(SpellRecipe recipe, GlyphRegistry registry, Vec2 center, float radius) =>
            Layout(recipe, registry, center, radius).ToDrawing();

        static GlyphPlacement Place(GlyphDefinition g, Emphasis e, CircleBand band, Vec2 at, float radius) => new GlyphPlacement
        {
            Glyph = g,
            Emphasis = e,
            Band = band,
            Center = at,
            Size = EmphasisRules.NominalSize(g.Role) * EmphasisRules.DrawScale(e) * radius,
        };

        /// <summary>Unit vector at a clockwise-from-top angle.</summary>
        public static Vec2 OnCircle(float clockwiseFromTop) =>
            new Vec2((float)Math.Sin(clockwiseFromTop), (float)Math.Cos(clockwiseFromTop));

        /// <summary>A closed circle drawn clockwise from the top, ending slightly past its start.</summary>
        public static Stroke CircleStroke(Vec2 center, float radius, int points = 96)
        {
            var s = new Stroke();
            for (int i = 0; i <= points; i++)
            {
                float a = (float)(2 * Math.PI * i / points);
                s.Points.Add(center + OnCircle(a) * radius);
            }
            return s;
        }
    }
}
