using System;
using System.Collections.Generic;

namespace Relic.Magic
{
    /// <summary>
    /// A glyph (one or more strokes) reduced to a fixed number of points, scaled
    /// to a unit box and centered on its centroid. This is the $P/$Q "point cloud"
    /// representation: stroke order, stroke direction and stroke count stop
    /// mattering, which is exactly what a forgiving glyph recognizer needs.
    /// </summary>
    public sealed class PointCloud
    {
        public const int DefaultSampleCount = 32;

        public readonly Vec2[] Points;

        PointCloud(Vec2[] points)
        {
            Points = points;
        }

        /// <summary>
        /// Length of the path through the normalized points: how much "ink" the
        /// shape has for its size. A scribble has far more than any glyph.
        /// </summary>
        public float PathLength
        {
            get
            {
                float len = 0f;
                for (int i = 1; i < Points.Length; i++) len += Vec2.Distance(Points[i - 1], Points[i]);
                return len;
            }
        }

        public static PointCloud FromStrokes(IList<Stroke> strokes, int n = DefaultSampleCount)
        {
            var lists = new List<IList<Vec2>>(strokes.Count);
            foreach (var s in strokes) lists.Add(s.Points);
            return FromPointLists(lists, n);
        }

        public static PointCloud FromPointLists(IList<IList<Vec2>> strokes, int n = DefaultSampleCount)
        {
            var resampled = Resample(strokes, n);
            Normalize(resampled);
            return new PointCloud(resampled);
        }

        /// <summary>
        /// The same drawing seen at a few small tilts (degrees, counter-clockwise).
        /// Each variant is rotated about its centroid before normalizing, so a
        /// slightly tilted glyph can be matched upright.
        /// </summary>
        public static PointCloud[] Tilted(IList<Stroke> strokes, int n, float[] tiltsDegrees)
        {
            var lists = new List<IList<Vec2>>(strokes.Count);
            foreach (var s in strokes) lists.Add(s.Points);
            var resampled = Resample(lists, n);

            float cx = 0f, cy = 0f;
            foreach (var p in resampled) { cx += p.X; cy += p.Y; }
            var centroid = new Vec2(cx / n, cy / n);

            var result = new PointCloud[tiltsDegrees.Length];
            for (int t = 0; t < tiltsDegrees.Length; t++)
            {
                float rad = tiltsDegrees[t] * (float)Math.PI / 180f;
                var pts = new Vec2[n];
                for (int i = 0; i < n; i++) pts[i] = rad == 0f ? resampled[i] : centroid + (resampled[i] - centroid).Rotated(rad);
                Normalize(pts);
                result[t] = new PointCloud(pts);
            }
            return result;
        }

        /// <summary>
        /// Resamples all strokes to n points spaced evenly along the combined path.
        /// Points are never interpolated across a pen lift.
        /// </summary>
        static Vec2[] Resample(IList<IList<Vec2>> strokes, int n)
        {
            float total = 0f;
            Vec2 first = Vec2.Zero;
            bool haveFirst = false;
            foreach (var s in strokes)
            {
                if (s == null || s.Count == 0) continue;
                if (!haveFirst) { first = s[0]; haveFirst = true; }
                for (int i = 1; i < s.Count; i++) total += Vec2.Distance(s[i - 1], s[i]);
            }

            var result = new Vec2[n];
            if (!haveFirst) return result;
            if (total <= 1e-6f)
            {
                for (int i = 0; i < n; i++) result[i] = first;
                return result;
            }

            float interval = total / (n - 1);
            float acc = 0f;
            int count = 0;
            result[count++] = first;
            Vec2 last = first;
            foreach (var s in strokes)
            {
                if (s == null || s.Count == 0) continue;
                Vec2 prev = s[0];
                for (int i = 1; i < s.Count && count < n; i++)
                {
                    Vec2 cur = s[i];
                    float d = Vec2.Distance(prev, cur);
                    while (d > 0f && acc + d >= interval && count < n)
                    {
                        float t = (interval - acc) / d;
                        Vec2 q = Vec2.Lerp(prev, cur, t);
                        result[count++] = q;
                        prev = q;
                        d = Vec2.Distance(prev, cur);
                        acc = 0f;
                    }
                    acc += d;
                    prev = cur;
                    last = cur;
                }
            }
            // Floating point can leave us one or two short; pad with the final point.
            while (count < n) result[count++] = last;
            return result;
        }

        static void Normalize(Vec2[] pts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            float size = Math.Max(maxX - minX, maxY - minY);
            if (size < 1e-6f) size = 1f;

            float cx = 0f, cy = 0f;
            foreach (var p in pts) { cx += p.X; cy += p.Y; }
            cx /= pts.Length;
            cy /= pts.Length;

            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vec2((pts[i].X - cx) / size, (pts[i].Y - cy) / size);
        }
    }
}
