using System.Collections.Generic;
using Relic.Core.Effects;

namespace Relic.Magic
{
    /// <summary>Which layer of the circle a glyph belongs to.</summary>
    public enum GlyphRole
    {
        /// <summary>The substance at the center: what the spell is made of.</summary>
        Core,
        /// <summary>Inner-ring modifiers: how much, how far, how long, how many.</summary>
        Inner,
        /// <summary>Outer-ring glyphs: the shape, motion and timing of the result.</summary>
        Ring,
    }

    /// <summary>
    /// What a non-core glyph does when compiled. Several glyphs may share an
    /// effect (an "ancient" variant of Seek can be added as pure data).
    /// </summary>
    public enum GlyphEffect
    {
        None,
        // Inner modifiers
        More,
        Less,
        Far,
        Lasting,
        Split,
        Seek,
        Heavy,
        Swift,
        // Ring: shapes
        Line,
        Cone,
        Ring,
        Orbit,
        Wall,
        // Ring: motion and timing
        Return,
        Delay,
    }

    /// <summary>What a core sigil puts into everything the spell touches.</summary>
    public sealed class CoreSubstance
    {
        public float Damage;
        public DamageKind DamageKind = DamageKind.Magical;
        public StatusId Status;
        public float StatusDuration;
        /// <summary>Meters pushed away (wind).</summary>
        public float Knockback;
    }

    /// <summary>
    /// One glyph: its meaning and its template shape. Loaded from data
    /// (Resources/RelicGlyphs.json), so new glyphs need no code unless they
    /// introduce a brand-new effect.
    /// </summary>
    public sealed class GlyphDefinition
    {
        public string Id;
        public string Name;
        public GlyphRole Role;
        public GlyphEffect Effect;
        /// <summary>Only for cores.</summary>
        public CoreSubstance Core;
        /// <summary>How to draw it, in words (shown in the palette and on errors).</summary>
        public string Hint;
        /// <summary>Words the stub namer may use for spells containing this glyph.</summary>
        public List<string> Words = new List<string>();
        /// <summary>Template strokes in a roughly [-1, 1] box, Y up.</summary>
        public List<List<Vec2>> Template = new List<List<Vec2>>();

        readonly Dictionary<int, PointCloud> _clouds = new Dictionary<int, PointCloud>();

        public PointCloud CloudFor(int sampleCount)
        {
            lock (_clouds)
            {
                if (!_clouds.TryGetValue(sampleCount, out var cloud))
                {
                    var lists = new List<IList<Vec2>>(Template.Count);
                    foreach (var s in Template) lists.Add(s);
                    cloud = PointCloud.FromPointLists(lists, sampleCount);
                    _clouds[sampleCount] = cloud;
                }
                return cloud;
            }
        }

        /// <summary>Bounds of the template strokes.</summary>
        public Bounds2 TemplateBounds
        {
            get
            {
                var b = Bounds2.Empty;
                foreach (var s in Template)
                    foreach (var p in s)
                        b.Encapsulate(p);
                return b;
            }
        }

        /// <summary>
        /// The template drawn at a center and size (size = the larger side of its
        /// bounding box), as strokes. Used by pre-written spells and the trace helper.
        /// </summary>
        public List<Stroke> Render(Vec2 center, float size)
        {
            var b = TemplateBounds;
            float ext = b.MaxExtent <= 1e-6f ? 1f : b.MaxExtent;
            float scale = size / ext;
            var c = b.Center;
            var result = new List<Stroke>(Template.Count);
            foreach (var s in Template)
            {
                var stroke = new Stroke();
                foreach (var p in s) stroke.Points.Add(center + (p - c) * scale);
                result.Add(stroke);
            }
            return result;
        }

        public override string ToString() => $"{Name} ({Role})";
    }
}
