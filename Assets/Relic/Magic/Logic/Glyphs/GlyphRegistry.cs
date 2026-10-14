using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Relic.Core.Effects;

namespace Relic.Magic
{
    /// <summary>
    /// The set of glyphs the game knows, loaded from JSON. The shipped set lives in
    /// Assets/Relic/Magic/Resources/RelicGlyphs.json; see the Magic README for the
    /// format and how to add a glyph.
    ///
    /// Template strokes are lists whose entries are either a point <c>[x, y]</c>
    /// or an arc <c>{"arc": [cx, cy, rx, ry, fromDeg, toDeg]}</c> (degrees
    /// counter-clockwise from +X; to &lt; from runs clockwise). Arcs are expanded to
    /// points every 10° when loaded, so the recognizer only ever sees points.
    /// </summary>
    public sealed class GlyphRegistry
    {
        readonly List<GlyphDefinition> _all = new List<GlyphDefinition>();
        readonly Dictionary<string, GlyphDefinition> _byId = new Dictionary<string, GlyphDefinition>(StringComparer.Ordinal);
        PointCloudRecognizer _recognizer;

        public IReadOnlyList<GlyphDefinition> All => _all;

        public GlyphDefinition this[string id] => Get(id);

        public GlyphDefinition Get(string id)
        {
            if (id != null && _byId.TryGetValue(id, out var g)) return g;
            throw new KeyNotFoundException($"no glyph with id '{id}'");
        }

        public bool TryGet(string id, out GlyphDefinition glyph)
        {
            glyph = null;
            return id != null && _byId.TryGetValue(id, out glyph);
        }

        public IEnumerable<GlyphDefinition> WithRole(GlyphRole role)
        {
            foreach (var g in _all)
                if (g.Role == role)
                    yield return g;
        }

        public PointCloudRecognizer Recognizer => _recognizer ?? (_recognizer = new PointCloudRecognizer(_all));

        public void Add(GlyphDefinition glyph)
        {
            Check(glyph);
            if (_byId.ContainsKey(glyph.Id)) throw new FormatException($"duplicate glyph id '{glyph.Id}'");
            _all.Add(glyph);
            _byId[glyph.Id] = glyph;
            _recognizer = null;
        }

        // ---------- JSON ----------

        public static GlyphRegistry FromJson(string json)
        {
            var reg = new GlyphRegistry();
            reg.AddJson(json);
            return reg;
        }

        /// <summary>Adds every glyph in a JSON document (lets mods or later content add glyph packs).</summary>
        public void AddJson(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception e) { throw new FormatException("glyph file is not valid JSON: " + e.Message, e); }

            var glyphs = root["glyphs"] as JArray;
            if (glyphs == null) throw new FormatException("glyph file needs a \"glyphs\" array");
            foreach (var token in glyphs)
            {
                var o = token as JObject;
                if (o == null) throw new FormatException("each glyph must be an object");
                Add(ParseGlyph(o));
            }
        }

        static GlyphDefinition ParseGlyph(JObject o)
        {
            string id = (string)o["id"];
            if (string.IsNullOrWhiteSpace(id)) throw new FormatException("a glyph is missing its \"id\"");
            string where = $"glyph '{id}'";

            var g = new GlyphDefinition
            {
                Id = id,
                Name = (string)o["name"] ?? id,
                Hint = (string)o["hint"] ?? "",
                Role = ParseEnum<GlyphRole>(o["role"], where + ".role"),
                Effect = o["effect"] == null ? GlyphEffect.None : ParseEnum<GlyphEffect>(o["effect"], where + ".effect"),
            };

            if (o["words"] is JArray words)
                foreach (var w in words)
                    g.Words.Add((string)w);

            if (o["core"] is JObject core)
            {
                g.Core = new CoreSubstance
                {
                    Damage = core.Value<float?>("damage") ?? 0f,
                    DamageKind = core["damageKind"] == null ? DamageKind.Magical : ParseEnum<DamageKind>(core["damageKind"], where + ".core.damageKind"),
                    Status = core["status"] == null ? StatusId.None : ParseEnum<StatusId>(core["status"], where + ".core.status"),
                    StatusDuration = core.Value<float?>("statusDuration") ?? 0f,
                    Knockback = core.Value<float?>("knockback") ?? 0f,
                };
            }

            var strokes = o["strokes"] as JArray;
            if (strokes == null) throw new FormatException(where + " needs \"strokes\"");
            foreach (var st in strokes)
            {
                var arr = st as JArray;
                if (arr == null) throw new FormatException(where + ": each stroke must be an array");
                var pts = new List<Vec2>();
                foreach (var entry in arr) ParseStrokeEntry(entry, pts, where);
                g.Template.Add(pts);
            }
            return g;
        }

        static void ParseStrokeEntry(JToken entry, List<Vec2> pts, string where)
        {
            if (entry is JArray p)
            {
                if (p.Count != 2) throw new FormatException(where + ": a point must be [x, y]");
                pts.Add(new Vec2((float)p[0], (float)p[1]));
                return;
            }
            if (entry is JObject obj && obj["arc"] is JArray a)
            {
                if (a.Count != 6) throw new FormatException(where + ": an arc must be [cx, cy, rx, ry, fromDeg, toDeg]");
                float cx = (float)a[0], cy = (float)a[1], rx = (float)a[2], ry = (float)a[3];
                float from = (float)a[4], to = (float)a[5];
                int segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(to - from) / 10f));
                for (int i = 0; i <= segments; i++)
                {
                    double deg = from + (to - from) * i / segments;
                    double rad = deg * Math.PI / 180.0;
                    var q = new Vec2(cx + rx * (float)Math.Cos(rad), cy + ry * (float)Math.Sin(rad));
                    // Avoid duplicating the joint with a preceding point.
                    if (pts.Count > 0 && Vec2.DistanceSquared(pts[pts.Count - 1], q) < 1e-8f) continue;
                    pts.Add(q);
                }
                return;
            }
            throw new FormatException(where + ": stroke entries must be [x, y] or {\"arc\": [...]}");
        }

        static T ParseEnum<T>(JToken token, string where) where T : struct
        {
            string s = (string)token;
            if (s != null && Enum.TryParse(s, false, out T value) && Enum.IsDefined(typeof(T), value)) return value;
            throw new FormatException($"{where}: '{s}' is not one of {string.Join(", ", Enum.GetNames(typeof(T)))}");
        }

        static void Check(GlyphDefinition g)
        {
            string where = $"glyph '{g.Id}'";
            if (g.Template.Count == 0) throw new FormatException(where + " has no strokes");
            foreach (var s in g.Template)
                if (s.Count < 2) throw new FormatException(where + " has a stroke with fewer than 2 points");

            switch (g.Role)
            {
                case GlyphRole.Core:
                    if (g.Core == null) throw new FormatException(where + " is a Core and needs a \"core\" block");
                    if (g.Effect != GlyphEffect.None) throw new FormatException(where + " is a Core and can't have an effect");
                    break;
                case GlyphRole.Inner:
                    if (!IsInnerEffect(g.Effect)) throw new FormatException(where + $": {g.Effect} is not an inner-ring effect");
                    break;
                case GlyphRole.Ring:
                    if (!IsRingEffect(g.Effect)) throw new FormatException(where + $": {g.Effect} is not an outer-ring effect");
                    break;
            }
        }

        public static bool IsInnerEffect(GlyphEffect e) => e >= GlyphEffect.More && e <= GlyphEffect.Swift;
        public static bool IsRingEffect(GlyphEffect e) => e >= GlyphEffect.Line && e <= GlyphEffect.Delay;
    }
}
