using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Relic.Magic
{
    /// <summary>One glyph in a recipe, with its emphasis (drawn size).</summary>
    public sealed class RecipeGlyph
    {
        public string Id;
        public Emphasis Emphasis;

        public RecipeGlyph() { }

        public RecipeGlyph(string id, Emphasis emphasis = Emphasis.Normal)
        {
            Id = id;
            Emphasis = emphasis;
        }

        /// <summary>"split" = normal, "split+" = large, "split-" = small.</summary>
        public static RecipeGlyph Parse(string token)
        {
            token = token.Trim();
            var e = Emphasis.Normal;
            if (token.EndsWith("+")) { e = Emphasis.Large; token = token.Substring(0, token.Length - 1); }
            else if (token.EndsWith("-")) { e = Emphasis.Small; token = token.Substring(0, token.Length - 1); }
            return new RecipeGlyph(token.Trim().ToLowerInvariant(), e);
        }

        public override string ToString() => Id + (Emphasis == Emphasis.Large ? "+" : Emphasis == Emphasis.Small ? "-" : "");
    }

    /// <summary>
    /// A spell as a list of glyphs: what the palette builds, what found tomes
    /// and ancient pages carry, and what the LLM will compose. Layout-free; the
    /// <see cref="RecipeDrawer"/> turns it into a drawing a player can study,
    /// and <see cref="SpellCircle.FromRecipe"/> feeds it to the compiler.
    ///
    /// Text form (handy for tests and data): <c>"water | split seek | line"</c>,
    /// i.e. core | inner glyphs | ring glyphs clockwise from the top, with
    /// <c>+</c>/<c>-</c> suffixes for large/small emphasis.
    /// </summary>
    public sealed class SpellRecipe
    {
        public string Core;
        public readonly List<RecipeGlyph> Inner = new List<RecipeGlyph>();
        public readonly List<RecipeGlyph> Ring = new List<RecipeGlyph>();

        public static SpellRecipe Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var parts = text.Split('|');
            if (parts.Length > 3) throw new FormatException("a recipe has at most three parts: core | inner | ring");
            var r = new SpellRecipe();
            string core = parts[0].Trim();
            r.Core = core.Length == 0 ? null : RecipeGlyph.Parse(core).Id;
            if (parts.Length > 1) AddTokens(parts[1], r.Inner);
            if (parts.Length > 2) AddTokens(parts[2], r.Ring);
            return r;
        }

        static void AddTokens(string s, List<RecipeGlyph> into)
        {
            foreach (var t in s.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                into.Add(RecipeGlyph.Parse(t));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Core ?? "");
            sb.Append(" |");
            foreach (var g in Inner) sb.Append(' ').Append(g);
            sb.Append(" |");
            foreach (var g in Ring) sb.Append(' ').Append(g);
            return sb.ToString();
        }

        // ---------- JSON: {"core":"water","inner":["split","seek"],"ring":["line"]} ----------

        public string ToJson()
        {
            var o = new JObject
            {
                ["core"] = Core,
                ["inner"] = new JArray(Inner.ConvertAll(g => (object)g.ToString()).ToArray()),
                ["ring"] = new JArray(Ring.ConvertAll(g => (object)g.ToString()).ToArray()),
            };
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static SpellRecipe FromJson(string json)
        {
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Exception e) { throw new FormatException("recipe is not valid JSON: " + e.Message, e); }
            foreach (var p in o.Properties())
                if (p.Name != "core" && p.Name != "inner" && p.Name != "ring")
                    throw new FormatException($"unknown recipe field '{p.Name}'");

            var r = new SpellRecipe { Core = (string)o["core"] };
            if (o["inner"] is JArray inner) foreach (var t in inner) r.Inner.Add(RecipeGlyph.Parse((string)t));
            if (o["ring"] is JArray ring) foreach (var t in ring) r.Ring.Add(RecipeGlyph.Parse((string)t));
            return r;
        }

        public int GlyphCount => (Core != null ? 1 : 0) + Inner.Count + Ring.Count;
    }
}
