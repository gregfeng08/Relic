using System;
using System.Collections.Generic;
using System.IO;
using Relic.Core.Random;

namespace Relic.Magic.Tests
{
    /// <summary>
    /// Shared fixtures: the shipped glyph file (found by walking up from the test
    /// binary in dotnet, or from the project root in Unity) and a seeded generator
    /// of sloppy, human-like drawings of a glyph.
    /// </summary>
    public static class MagicTestData
    {
        const string GlyphPath = "Assets/Relic/Magic/Resources/RelicGlyphs.json";

        static GlyphRegistry _registry;

        public static GlyphRegistry Registry => _registry ?? (_registry = GlyphRegistry.FromJson(File.ReadAllText(FindGlyphFile())));

        public static string FindGlyphFile()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, GlyphPath);
                    if (File.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("could not find " + GlyphPath);
        }

        /// <summary>How sloppy a synthetic drawing is.</summary>
        public sealed class Sloppiness
        {
            public float MaxRotationDegrees = 15f;
            /// <summary>Independent x/y stretch, ± fraction.</summary>
            public float AspectJitter = 0.15f;
            /// <summary>Per-point jitter, as a fraction of glyph size (standard deviation).</summary>
            public float PointNoise = 0.02f;
            /// <summary>Slow hand wobble, as a fraction of glyph size.</summary>
            public float Wobble = 0.04f;
            /// <summary>Range of point spacing as a fraction of glyph size (models mouse sampling rate).</summary>
            public float MinSpacing = 0.01f, MaxSpacing = 0.08f;
            public float MinSize = 40f, MaxSize = 220f;
            /// <summary>Randomize stroke order and direction.</summary>
            public bool Shuffle = true;

            public static Sloppiness Default => new Sloppiness();
            public static Sloppiness None => new Sloppiness
            {
                MaxRotationDegrees = 0f, AspectJitter = 0f, PointNoise = 0f, Wobble = 0f,
                MinSpacing = 0.02f, MaxSpacing = 0.02f, MinSize = 100f, MaxSize = 100f, Shuffle = false,
            };
        }

        /// <summary>Draws a glyph the way a person with a mouse might, deterministically from <paramref name="rng"/>.</summary>
        public static List<Stroke> Sloppy(GlyphDefinition glyph, ref Pcg32 rng, Sloppiness s, Vec2 center, float fixedSize = -1f)
        {
            float size = rng.Range(s.MinSize, s.MaxSize);
            if (fixedSize > 0f) size = fixedSize * rng.Range(0.92f, 1.08f);
            float rot = rng.Range(-s.MaxRotationDegrees, s.MaxRotationDegrees) * (float)Math.PI / 180f;
            float sx = 1f + rng.Range(-s.AspectJitter, s.AspectJitter);
            float sy = 1f + rng.Range(-s.AspectJitter, s.AspectJitter);
            float spacing = size * rng.Range(s.MinSpacing, s.MaxSpacing);
            // Wobble: a couple of slow sine waves in x and y along the path.
            float wf1 = rng.Range(0.5f, 2f), wf2 = rng.Range(0.5f, 2f);
            float wp1 = rng.Range(0f, 6.283f), wp2 = rng.Range(0f, 6.283f);

            var b = glyph.TemplateBounds;
            float ext = Math.Max(1e-6f, b.MaxExtent);
            var c = b.Center;

            var strokes = new List<Stroke>();
            float pathPos = 0f;
            foreach (var templateStroke in glyph.Template)
            {
                // Template → shaped polyline in drawing space.
                var shaped = new List<Vec2>();
                foreach (var p in templateStroke)
                {
                    var q = (p - c) / ext;
                    q = new Vec2(q.X * sx, q.Y * sy).Rotated(rot) * size;
                    shaped.Add(q);
                }

                // Re-sample at the "mouse" spacing, adding wobble and noise.
                var stroke = new Stroke();
                for (int i = 0; i < shaped.Count - 1; i++)
                {
                    var a = shaped[i];
                    var d = shaped[i + 1] - a;
                    float len = d.Length;
                    int steps = Math.Max(1, (int)Math.Ceiling(len / spacing));
                    for (int k = 0; k < steps; k++)
                    {
                        var q = a + d * ((float)k / steps);
                        float t = (pathPos + len * k / steps) / size;
                        q = q + new Vec2((float)Math.Sin(t * wf1 * 6.283f + wp1), (float)Math.Sin(t * wf2 * 6.283f + wp2)) * (s.Wobble * size);
                        q = q + new Vec2(Gaussian(ref rng), Gaussian(ref rng)) * (s.PointNoise * size);
                        stroke.Points.Add(center + q);
                    }
                    pathPos += len;
                }
                var end = shaped[shaped.Count - 1];
                stroke.Points.Add(center + end + new Vec2(Gaussian(ref rng), Gaussian(ref rng)) * (s.PointNoise * size));

                if (s.Shuffle && rng.Chance(0.5f)) stroke.Points.Reverse();
                strokes.Add(stroke);
            }

            if (s.Shuffle)
            {
                for (int i = strokes.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    var tmp = strokes[i]; strokes[i] = strokes[j]; strokes[j] = tmp;
                }
            }
            return strokes;
        }

        static float Gaussian(ref Pcg32 rng)
        {
            // Box–Muller.
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>
        /// A whole spell drawn by hand: a slightly wobbly circle (closed with a
        /// small overlap) and every glyph of the recipe drawn sloppily at its
        /// layout position, in the order a scribe would draw them.
        /// </summary>
        public static Drawing SloppySpell(string recipe, ref Pcg32 rng, Sloppiness s, Vec2 center, float radius)
        {
            var layout = RecipeDrawer.Layout(SpellRecipe.Parse(recipe), Registry, center, radius);
            var d = new Drawing();
            var circle = new Stroke();
            float start = rng.Range(0f, 360f);
            float wob = s.Wobble * 0.5f;
            for (int i = 0; i <= 90; i++)
            {
                float deg = start - 368f * i / 90f;
                double a = deg * Math.PI / 180.0;
                float r = radius * (1f + wob * (float)Math.Sin(i * 0.21f)) + Gaussian(ref rng) * radius * s.PointNoise * 0.3f;
                circle.Points.Add(center + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * r);
            }
            d.Strokes.Add(circle);
            foreach (var p in layout.Placements)
                d.Strokes.AddRange(Sloppy(p.Glyph, ref rng, s, p.Center, p.Size));
            return d;
        }

        public static Sloppiness Tidy => new Sloppiness { PointNoise = 0.005f, Wobble = 0.01f, MaxRotationDegrees = 3f, AspectJitter = 0.03f };
        public static Sloppiness Messy => new Sloppiness { PointNoise = 0.03f, Wobble = 0.07f, MaxRotationDegrees = 20f, AspectJitter = 0.2f };

        /// <summary>A circle stroke (for building whole spell drawings by hand in tests).</summary>
        public static Stroke Circle(Vec2 center, float radius, float startDeg = 90f, float sweepDeg = -360f, int points = 72)
        {
            var s = new Stroke();
            for (int i = 0; i <= points; i++)
            {
                double a = (startDeg + sweepDeg * i / points) * Math.PI / 180.0;
                s.Points.Add(center + new Vec2((float)Math.Cos(a), (float)Math.Sin(a)) * radius);
            }
            return s;
        }
    }
}
