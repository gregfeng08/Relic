using System.Collections.Generic;

namespace Relic.Magic
{
    /// <summary>One pen-down-to-pen-up polyline.</summary>
    public sealed class Stroke
    {
        public readonly List<Vec2> Points = new List<Vec2>();

        public Stroke() { }

        public Stroke(IEnumerable<Vec2> points)
        {
            Points.AddRange(points);
        }

        public int Count => Points.Count;

        public float Length
        {
            get
            {
                float len = 0f;
                for (int i = 1; i < Points.Count; i++) len += Vec2.Distance(Points[i - 1], Points[i]);
                return len;
            }
        }

        public Bounds2 Bounds
        {
            get
            {
                var b = Bounds2.Empty;
                foreach (var p in Points) b.Encapsulate(p);
                return b;
            }
        }
    }

    /// <summary>
    /// Everything drawn on one page, in drawing order. The drawing order matters
    /// only for grouping a glyph's strokes (a glyph's strokes are drawn one after
    /// another); the spell itself reads glyphs by position, not by order drawn.
    /// </summary>
    public sealed class Drawing
    {
        public readonly List<Stroke> Strokes = new List<Stroke>();

        public Drawing() { }

        public Drawing(IEnumerable<Stroke> strokes)
        {
            Strokes.AddRange(strokes);
        }

        public Bounds2 Bounds
        {
            get
            {
                var b = Bounds2.Empty;
                foreach (var s in Strokes) b.Encapsulate(s.Bounds);
                return b;
            }
        }

        public static Bounds2 BoundsOf(IList<Stroke> strokes)
        {
            var b = Bounds2.Empty;
            foreach (var s in strokes) b.Encapsulate(s.Bounds);
            return b;
        }
    }
}
