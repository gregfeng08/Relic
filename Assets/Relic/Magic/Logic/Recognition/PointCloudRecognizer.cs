using System;
using System.Collections.Generic;

namespace Relic.Magic
{
    /// <summary>What the recognizer made of a group of strokes.</summary>
    public sealed class Recognition
    {
        /// <summary>Best-matching glyph, or null when nothing matched well enough.</summary>
        public GlyphDefinition Glyph;
        /// <summary>Mean point distance to the best template, in glyph-size units (0 = perfect).</summary>
        public float Distance;
        /// <summary>0–1: how cleanly the glyph was drawn. Feeds spell quality.</summary>
        public float Precision;
        /// <summary>0–1: how clearly it beat the runner-up (0 = a coin flip between two glyphs).</summary>
        public float Confidence;
        /// <summary>Second-best glyph (useful for "did you mean…" hints).</summary>
        public GlyphDefinition RunnerUp;
        /// <summary>Best match even when it was too poor to accept (for "it looked a bit like…" hints).</summary>
        public GlyphDefinition Closest;
        /// <summary>Ink path length relative to the closest template (scribbles are far above 1).</summary>
        public float InkRatio;
        public float RunnerUpDistance;

        public bool IsRecognized => Glyph != null;

        public override string ToString() =>
            Glyph == null ? "unrecognized" : $"{Glyph.Id} (precision {Precision:0.00}, confidence {Confidence:0.00})";
    }

    /// <summary>
    /// A $Q-style point-cloud recognizer (Vatavu, Anthony &amp; Wobbrock's $P/$Q
    /// family): every glyph is a cloud of N points, and the match is a greedy
    /// nearest-point assignment with $Q's early abandoning. Fast, offline,
    /// deterministic, and indifferent to stroke order and direction.
    ///
    /// It is deliberately NOT rotation invariant: glyph orientation is part of
    /// a glyph's meaning ("less" is a chevron pointing down). It tolerates the
    /// small tilts of hand drawing.
    /// </summary>
    public sealed class PointCloudRecognizer
    {
        /// <summary>Mean distances at or below this count as a perfect drawing.</summary>
        public const float CleanDistance = 0.02f;
        /// <summary>Mean distances at or above this are not that glyph at all (calibrated so random scribbles mostly land above it).</summary>
        public const float RejectDistance = 0.11f;

        /// <summary>Tilts (degrees) forgiven for free: a glyph drawn 10° off upright is still clean.</summary>
        static readonly float[] Tilts = { 0f, -10f, 10f };

        /// <summary>
        /// A drawing with this many times the ink path of the glyph it resembles is a
        /// scribble. Messy but honest drawings stay under ~1.5 on the synthetic set.
        /// </summary>
        public const float MaxInkRatio = 1.75f;

        readonly List<GlyphDefinition> _glyphs = new List<GlyphDefinition>();
        readonly int _n;
        readonly float _weightSum;

        public PointCloudRecognizer(IEnumerable<GlyphDefinition> glyphs, int sampleCount = PointCloud.DefaultSampleCount)
        {
            _n = sampleCount;
            _weightSum = (_n + 1) * 0.5f; // Σ (1 - k/n) for k = 0..n-1
            foreach (var g in glyphs)
                if (g != null && g.Template != null && g.Template.Count > 0)
                    _glyphs.Add(g);
        }

        public IReadOnlyList<GlyphDefinition> Glyphs => _glyphs;

        public Recognition Recognize(IList<Stroke> strokes)
        {
            return Recognize(PointCloud.Tilted(strokes, _n, Tilts));
        }

        public Recognition Recognize(PointCloud cloud)
        {
            return Recognize(new[] { cloud });
        }

        /// <summary>
        /// Matches the best of several views of the same drawing (see
        /// <see cref="PointCloud.Tilted"/>) against every template.
        /// </summary>
        public Recognition Recognize(PointCloud[] views)
        {
            float best = float.MaxValue, second = float.MaxValue;
            GlyphDefinition bestGlyph = null, secondGlyph = null;
            foreach (var g in _glyphs)
            {
                var t = g.CloudFor(_n);
                float d = float.MaxValue;
                foreach (var v in views)
                {
                    // Abandon early against the runner-up so we still know the margin.
                    float dv = GreedyCloudMatch(v.Points, t.Points, Math.Min(second, d) * _weightSum);
                    if (dv < d) d = dv;
                }
                if (d < best)
                {
                    second = best; secondGlyph = bestGlyph;
                    best = d; bestGlyph = g;
                }
                else if (d < second)
                {
                    second = d; secondGlyph = g;
                }
            }

            var r = new Recognition
            {
                Distance = best,
                RunnerUp = secondGlyph,
                RunnerUpDistance = second,
            };
            r.Precision = PrecisionFor(best);
            r.Confidence = second >= float.MaxValue || second <= 0f ? 1f : Clamp01(1f - best / second);
            r.Closest = bestGlyph;
            // Too much ink for the shape (a scribble or scratch-out) is not a glyph, however close.
            r.InkRatio = bestGlyph == null ? 0f : views[0].PathLength / Math.Max(1e-3f, bestGlyph.CloudFor(_n).PathLength);
            r.Glyph = best < RejectDistance && r.InkRatio <= MaxInkRatio ? bestGlyph : null;
            if (r.Glyph == null) r.Precision = 0f;
            return r;
        }

        /// <summary>
        /// Maps a mean point distance to a 0–1 drawing precision. Quadratic, so
        /// small wobbles barely count and only real sloppiness drags it down.
        /// </summary>
        public static float PrecisionFor(float meanDistance)
        {
            float x = Clamp01((meanDistance - CleanDistance) / (RejectDistance - CleanDistance));
            return 1f - x * x;
        }

        /// <summary>Mean weighted point distance between two clouds (lower is better).</summary>
        float GreedyCloudMatch(Vec2[] pts, Vec2[] tmpl, float abandonAt)
        {
            int n = pts.Length;
            int step = Math.Max(1, (int)Math.Floor(Math.Sqrt(n)));
            float min = abandonAt;
            for (int i = 0; i < n; i += step)
            {
                float d1 = CloudDistance(pts, tmpl, i, min);
                if (d1 < min) min = d1;
                float d2 = CloudDistance(tmpl, pts, i, min);
                if (d2 < min) min = d2;
            }
            return min / _weightSum;
        }

        static float CloudDistance(Vec2[] a, Vec2[] b, int start, float minSoFar)
        {
            int n = a.Length;
            var matched = new bool[n];
            float sum = 0f;
            int i = start;
            int k = 0;
            do
            {
                float bestD = float.MaxValue;
                int index = -1;
                for (int j = 0; j < n; j++)
                {
                    if (matched[j]) continue;
                    float d = Vec2.DistanceSquared(a[i], b[j]);
                    if (d < bestD) { bestD = d; index = j; }
                }
                matched[index] = true;
                float weight = 1f - (float)k / n;
                sum += weight * (float)Math.Sqrt(bestD);
                if (sum >= minSoFar) return sum; // $Q early abandoning
                i = (i + 1) % n;
                k++;
            } while (i != start);
            return sum;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
