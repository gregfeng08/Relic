using System;
using System.Collections.Generic;
using Relic.Core.Random;

namespace Relic.Magic
{
    /// <summary>
    /// Reads a drawing as a spell circle:
    /// 1. finds the outer circle (the largest roughly round stroke, plus any arcs
    ///    that continue it) and checks that it's closed;
    /// 2. groups the remaining strokes into glyphs (a glyph's strokes are drawn
    ///    one after another) and recognizes each group;
    /// 3. files glyphs as core / inner / ring, orders the ring clockwise from the
    ///    top, and reads emphasis from each glyph's size relative to the circle.
    ///
    /// Deterministic: the same strokes always give the same circle. Keep one
    /// parser per drawing surface; it caches recognitions so re-parsing after each
    /// new stroke only recognizes the groups that stroke touches.
    /// </summary>
    public sealed class CircleParser
    {
        public const int MaxStrokesPerGlyph = 4;
        /// <summary>Glyph centers closer than this (in circle radii) sit in the center.</summary>
        public const float CenterBand = 0.28f;
        /// <summary>Default boundary between the inner and the outer ring, unless a guide circle is drawn.</summary>
        public const float InnerBand = 0.62f;
        /// <summary>Ink whose center is farther out than this is outside the circle.</summary>
        public const float OutsideBand = 1.3f;
        /// <summary>Largest gap (degrees) still counted as a closed circle.</summary>
        public const float MaxClosedGapDegrees = 20f;
        /// <summary>Glyphs recognized with less confidence than this get a "looks like" warning.</summary>
        public const float AmbiguousConfidence = 0.08f;

        readonly GlyphRegistry _registry;
        readonly Dictionary<ulong, Recognition> _cache = new Dictionary<ulong, Recognition>();

        public CircleParser(GlyphRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public GlyphRegistry Registry => _registry;

        public SpellCircle Parse(Drawing drawing)
        {
            var c = new SpellCircle();
            var strokes = drawing?.Strokes ?? new List<Stroke>();
            var live = new List<int>();
            for (int i = 0; i < strokes.Count; i++)
                if (strokes[i] != null && strokes[i].Count > 0)
                    live.Add(i);

            if (live.Count == 0)
            {
                c.Diagnostics.Add(new SpellDiagnostic(Severity.Error, "The page is empty. Draw a large circle, then a core sigil in its center."));
                return c;
            }

            var glyphStrokes = FindCircle(strokes, live, c);
            if (!c.HasCircle)
            {
                // Still read the glyphs (useful while practicing), measured against the ink itself.
                var b = Bounds2.Empty;
                foreach (int i in live) b.Encapsulate(strokes[i].Bounds);
                c.Center = b.Center;
                c.Radius = Math.Max(1e-3f, b.MaxExtent * 0.5f);
            }

            ReadGlyphs(strokes, glyphStrokes, c);
            return c;
        }

        // ---------------- Circle ----------------

        List<int> FindCircle(List<Stroke> strokes, List<int> live, SpellCircle c)
        {
            int cand = -1;
            float bestExtent = -1f;
            foreach (int i in live)
            {
                float e = strokes[i].Bounds.MaxExtent;
                if (e > bestExtent) { bestExtent = e; cand = i; }
            }

            var rest = new List<int>(live);
            if (!FitCircle(strokes[cand].Points, out var center, out float radius, out float roughness) || roughness > 0.2f
                || Coverage(strokes[cand].Points, center, out _) < 180f)
            {
                c.Diagnostics.Add(new SpellDiagnostic(Severity.Error, "Start with a large circle around your glyphs: it's what holds the spell together."));
                return rest;
            }

            // Ink mostly outside the "circle" means it's really a glyph, not the spell's circle.
            int outside = 0, others = 0;
            foreach (int i in live)
            {
                if (i == cand) continue;
                others++;
                if (Vec2.Distance(strokes[i].Bounds.Center, center) > radius * OutsideBand) outside++;
            }
            if (others > 0 && outside * 2 > others)
            {
                c.Diagnostics.Add(new SpellDiagnostic(Severity.Error, "Your glyphs need to sit inside one large circle."));
                return rest;
            }

            c.HasCircle = true;
            c.Center = center;
            c.Radius = radius;
            c.CircleStrokeIndices.Add(cand);
            rest.Remove(cand);

            // Arcs that continue the circle (people lift the pen while drawing big circles).
            var circlePoints = new List<IList<Vec2>> { strokes[cand].Points };
            foreach (int i in new List<int>(rest))
            {
                var s = strokes[i];
                if (s.Bounds.MaxExtent < 0.3f * radius) continue;
                int inBand = 0;
                foreach (var p in s.Points)
                {
                    float r = Vec2.Distance(p, center) / radius;
                    if (r > 0.85f && r < 1.15f) inBand++;
                }
                if (inBand < 0.85f * s.Count) continue;
                if (Coverage(s.Points, center, out _) < 25f) continue;
                c.CircleStrokeIndices.Add(i);
                circlePoints.Add(s.Points);
                rest.Remove(i);
            }

            float gapDeg = LargestGap(circlePoints, center, out float gapCenter);
            c.IsClosed = gapDeg <= MaxClosedGapDegrees;
            if (!c.IsClosed)
                c.Diagnostics.Add(new SpellDiagnostic(Severity.Error,
                    $"The circle isn't closed: there's a gap at the {Direction(gapCenter)}. Join the ends so the magic can't leak out."));

            // An optional inner guide circle, concentric, sets the inner/outer boundary.
            foreach (int i in new List<int>(rest))
            {
                var s = strokes[i];
                if (!FitCircle(s.Points, out var gc, out float gr, out float rough) || rough > 0.15f) continue;
                if (Vec2.Distance(gc, center) > 0.15f * radius) continue;
                float rel = gr / radius;
                if (rel < 0.35f || rel > 0.85f) continue;
                if (Coverage(s.Points, gc, out _) < 300f) continue;
                c.GuideRadius = rel;
                c.CircleStrokeIndices.Add(i);
                rest.Remove(i);
                break;
            }

            // Ink outside the circle is ignored.
            int stray = 0;
            foreach (int i in new List<int>(rest))
            {
                if (Vec2.Distance(strokes[i].Bounds.Center, center) > radius * OutsideBand)
                {
                    rest.Remove(i);
                    stray++;
                }
            }
            if (stray > 0)
                c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning,
                    stray == 1 ? "One mark outside the circle is ignored." : $"{stray} marks outside the circle are ignored."));
            return rest;
        }

        /// <summary>
        /// Least-squares (Kåsa) circle fit. Roughness is the spread of point
        /// distances from the fitted circle, relative to its radius.
        /// </summary>
        public static bool FitCircle(IList<Vec2> pts, out Vec2 center, out float radius, out float roughness)
        {
            center = Vec2.Zero;
            radius = 0f;
            roughness = float.MaxValue;
            int n = pts.Count;
            if (n < 5) return false;

            double mx = 0, my = 0;
            foreach (var p in pts) { mx += p.X; my += p.Y; }
            mx /= n; my /= n;

            double sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0;
            foreach (var p in pts)
            {
                double x = p.X - mx, y = p.Y - my, z = x * x + y * y;
                sxx += x * x; syy += y * y; sxy += x * y;
                sxz += x * z; syz += y * z; sz += z;
            }
            // Solve [sxx sxy; sxy syy] [D E] = -[sxz syz] (the F term vanishes for centered data).
            double det = sxx * syy - sxy * sxy;
            if (Math.Abs(det) < 1e-12) return false;
            double d = (-sxz * syy + syz * sxy) / det;
            double e = (-syz * sxx + sxz * sxy) / det;
            double f = -sz / n; // the Σx, Σy terms vanish after centering
            double cx = -d / 2, cy = -e / 2;
            double r2 = cx * cx + cy * cy - f;
            if (r2 <= 0) return false;
            double r = Math.Sqrt(r2);

            center = new Vec2((float)(cx + mx), (float)(cy + my));
            radius = (float)r;

            double sum = 0, sumSq = 0;
            foreach (var p in pts)
            {
                double dist = Vec2.Distance(p, center);
                sum += dist;
                sumSq += dist * dist;
            }
            double mean = sum / n;
            double var = Math.Max(0, sumSq / n - mean * mean);
            roughness = (float)(Math.Sqrt(var) / Math.Max(1e-6, mean));
            return true;
        }

        const int Bins = 72; // 5° each

        static bool[] MarkBins(IList<IList<Vec2>> strokes, Vec2 center)
        {
            var bins = new bool[Bins];
            foreach (var pts in strokes)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    float a = (pts[i] - center).ClockwiseAngleFromTop();
                    bins[BinOf(a)] = true;
                    if (i == 0) continue;
                    float a0 = (pts[i - 1] - center).ClockwiseAngleFromTop();
                    float delta = a - a0;
                    if (delta > Math.PI) delta -= (float)(2 * Math.PI);
                    if (delta < -Math.PI) delta += (float)(2 * Math.PI);
                    if (Math.Abs(delta) > Math.PI / 2) continue; // a jump, not a stroke along the rim
                    int steps = (int)Math.Ceiling(Math.Abs(delta) / (2 * Math.PI / Bins)) * 2;
                    for (int k = 1; k < steps; k++)
                        bins[BinOf(a0 + delta * k / steps)] = true;
                }
            }
            return bins;
        }

        static int BinOf(float angle)
        {
            double a = angle % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            int b = (int)(a / (2 * Math.PI) * Bins);
            return b >= Bins ? Bins - 1 : b;
        }

        /// <summary>Degrees of the circle around <paramref name="center"/> that the points sweep.</summary>
        static float Coverage(IList<Vec2> pts, Vec2 center, out bool[] bins)
        {
            bins = MarkBins(new List<IList<Vec2>> { pts }, center);
            int marked = 0;
            foreach (bool b in bins) if (b) marked++;
            return marked * 360f / Bins;
        }

        static float LargestGap(IList<IList<Vec2>> strokes, Vec2 center, out float gapCenter)
        {
            var bins = MarkBins(strokes, center);
            int bestRun = 0, bestStart = 0;
            for (int start = 0; start < Bins; start++)
            {
                if (bins[start]) continue;
                int prev = (start + Bins - 1) % Bins;
                if (!bins[prev] && start != 0) continue; // not the start of a run (wraps handled below)
                int run = 0;
                while (run < Bins && !bins[(start + run) % Bins]) run++;
                if (run > bestRun) { bestRun = run; bestStart = start; }
            }
            gapCenter = (float)((bestStart + bestRun * 0.5) / Bins * 2 * Math.PI);
            return bestRun * 360f / Bins;
        }

        /// <summary>Plain words for a clockwise-from-top angle.</summary>
        public static string Direction(float angle)
        {
            string[] names = { "top", "top right", "right", "bottom right", "bottom", "bottom left", "left", "top left" };
            double a = angle % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            int i = (int)Math.Round(a / (Math.PI / 4)) % 8;
            return names[i];
        }

        // ---------------- Glyphs ----------------

        void ReadGlyphs(List<Stroke> strokes, List<int> glyphStrokes, SpellCircle c)
        {
            int n = glyphStrokes.Count;
            var bounds = new Bounds2[n];
            for (int i = 0; i < n; i++) bounds[i] = strokes[glyphStrokes[i]].Bounds;

            // best[k]: best total value for the first k strokes; from[k]: where its last group starts.
            var best = new float[n + 1];
            var from = new int[n + 1];
            var rec = new Recognition[n + 1, MaxStrokesPerGlyph + 1];
            for (int k = 1; k <= n; k++)
            {
                best[k] = float.MinValue;
                for (int len = 1; len <= MaxStrokesPerGlyph && len <= k; len++)
                {
                    int start = k - len;
                    // A glyph's strokes touch or nearly touch each other.
                    if (len > 1 && !Connected(bounds, start, k, 0.03f * c.Radius)) continue;

                    var group = new List<Stroke>(len);
                    for (int j = start; j < k; j++) group.Add(strokes[glyphStrokes[j]]);
                    var r = RecognizeCached(group);
                    rec[k, len] = r;
                    // A group is worth (strokes^1.5) × precision: splitting a clean glyph
                    // into glyph-like pieces (an arrowhead reads as Less), or merging two
                    // glyphs into a mess, both lose.
                    float value = best[start] + (r.IsRecognized ? (float)Math.Pow(len, 1.5) * (0.25f + r.Precision) : 0f);
                    if (value > best[k]) { best[k] = value; from[k] = start; }
                }
            }

            // Walk back the chosen groups (then restore drawing order).
            var groups = new List<KeyValuePair<int, int>>();
            for (int k = n; k > 0; k = from[k]) groups.Add(new KeyValuePair<int, int>(from[k], k));
            groups.Reverse();

            float centerBand = CenterBand;
            float innerBand = c.GuideRadius > 0f ? c.GuideRadius : InnerBand;

            foreach (var g in groups)
            {
                int start = g.Key, end = g.Value, len = end - start;
                var r = rec[end, len];
                var b = Bounds2.Empty;
                for (int j = start; j < end; j++) b.Encapsulate(bounds[j]);
                var rel = (b.Center - c.Center) / c.Radius;
                float radius = rel.Length;
                float angle = ReadingAngle(rel.ClockwiseAngleFromTop());
                var band = radius < centerBand ? CircleBand.Center : radius < innerBand ? CircleBand.Inner : CircleBand.Outer;

                if (!r.IsRecognized)
                {
                    var mark = new UnrecognizedMark { Bounds = b, BestGuess = r, Band = band, Angle = angle };
                    for (int j = start; j < end; j++) mark.StrokeIndices.Add(glyphStrokes[j]);
                    c.Unrecognized.Add(mark);
                    string where = band == CircleBand.Center ? "in the center" : $"at the {Direction(angle)}";
                    string guess = r.Closest != null ? $" (it looked a little like {r.Closest.Name})" : "";
                    c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning, $"A mark {where} wasn't clear enough to read{guess}, so it's ignored."));
                    continue;
                }

                var inst = new GlyphInstance
                {
                    Glyph = r.Glyph,
                    Precision = r.Precision,
                    Confidence = r.Confidence,
                    Band = band,
                    Angle = angle,
                    Radius = radius,
                    RelativeSize = b.MaxExtent / c.Radius,
                    Bounds = b,
                };
                inst.Emphasis = EmphasisRules.FromRatio(inst.RelativeSize / EmphasisRules.NominalSize(r.Glyph.Role));
                for (int j = start; j < end; j++) inst.StrokeIndices.Add(glyphStrokes[j]);
                SpellCircle.Sort(c, inst);

                if (r.Confidence < AmbiguousConfidence && r.RunnerUp != null)
                    c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning,
                        $"Your {r.Glyph.Name} looks a lot like {r.RunnerUp.Name}. Draw it more carefully so it isn't misread.", inst));

                if (c.HasCircle) CheckPlacement(inst, c);
            }

            // Order: cores closest to the center first; inner and ring clockwise from the top.
            c.Cores.Sort((a, b) => a.Radius.CompareTo(b.Radius));
            c.Inner.Sort((a, b) => a.Angle.CompareTo(b.Angle));
            c.Ring.Sort((a, b) => a.Angle != b.Angle ? a.Angle.CompareTo(b.Angle) : a.Radius.CompareTo(b.Radius));
        }

        /// <summary>
        /// True when strokes [start, end) form one connected cluster, two strokes
        /// being linked when their boxes are closer than about a third of the
        /// larger one (or <paramref name="minPad"/>).
        /// </summary>
        static bool Connected(Bounds2[] bounds, int start, int end, float minPad)
        {
            int n = end - start;
            var reached = new bool[n];
            reached[0] = true;
            int count = 1;
            bool grew = true;
            while (grew && count < n)
            {
                grew = false;
                for (int i = 0; i < n; i++)
                {
                    if (reached[i]) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (!reached[j]) continue;
                        var a = bounds[start + i];
                        var b = bounds[start + j];
                        float pad = Math.Max(0.35f * Math.Max(a.MaxExtent, b.MaxExtent), minPad);
                        if (Bounds2.Gap(a, b) <= pad)
                        {
                            reached[i] = true;
                            count++;
                            grew = true;
                            break;
                        }
                    }
                }
            }
            return count == n;
        }

        /// <summary>Glyphs this far left of the top (degrees) still count as "at the top" and are read first.</summary>
        public const float TopTolerance = 20f;

        /// <summary>
        /// Clockwise-from-top angle used for reading order: a glyph just left of
        /// the top is read first, not last (people center the first glyph by eye).
        /// </summary>
        public static float ReadingAngle(float clockwiseFromTop)
        {
            float tol = TopTolerance * (float)Math.PI / 180f;
            return clockwiseFromTop > 2 * Math.PI - tol ? clockwiseFromTop - (float)(2 * Math.PI) : clockwiseFromTop;
        }

        static void CheckPlacement(GlyphInstance g, SpellCircle c)
        {
            string name = g.Glyph.Name;
            switch (g.Glyph.Role)
            {
                case GlyphRole.Core:
                    if (g.Band != CircleBand.Center)
                        c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning, $"{name} is a core sigil; it belongs in the very center of the circle.", g));
                    break;
                case GlyphRole.Inner:
                    if (g.Band != CircleBand.Inner)
                        c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning, $"{name} is an inner glyph; draw it between the core and the outer ring.", g));
                    break;
                case GlyphRole.Ring:
                    if (g.Band != CircleBand.Outer)
                        c.Diagnostics.Add(new SpellDiagnostic(Severity.Warning, $"{name} is a ring glyph; draw it on the outer ring, where its position sets the order.", g));
                    break;
            }
        }

        Recognition RecognizeCached(List<Stroke> group)
        {
            ulong key = 0x5EED;
            foreach (var s in group)
            {
                key = Hash.Combine(key, (ulong)s.Count);
                foreach (var p in s.Points)
                    key = Hash.Combine(key, unchecked((ulong)(long)Math.Round(p.X * 64f)), unchecked((ulong)(long)Math.Round(p.Y * 64f)));
            }
            if (_cache.TryGetValue(key, out var r)) return r;
            r = _registry.Recognizer.Recognize(group);
            if (_cache.Count > 4096) _cache.Clear();
            _cache[key] = r;
            return r;
        }
    }
}
