using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Magic
{
    /// <summary>Flavor text for a spell. Never affects how it plays.</summary>
    public sealed class SpellFlavor
    {
        public string Name;
        /// <summary>One sentence in the world's voice saying what the spell does.</summary>
        public string Description;
    }

    /// <summary>
    /// Names and describes spells. The local LLM will implement this later
    /// (generated once and saved, like all LLM output); it only ever adds
    /// flavor and never decides power.
    /// </summary>
    public interface ISpellNamer
    {
        SpellFlavor Name(SpellCircle circle, EffectProgram program);
    }

    /// <summary>
    /// Deterministic stand-in: "[adjective] Core Noun" from each glyph's data
    /// words, picked by a hash of the recipe, plus a plain description built
    /// from the compiled program. Same circle, same name, every time.
    /// </summary>
    public sealed class StubSpellNamer : ISpellNamer
    {
        public SpellFlavor Name(SpellCircle circle, EffectProgram program)
        {
            var recipe = circle.ToRecipe();
            var rng = new Pcg32(Hash.Of("spell.name:" + recipe));

            string core = Pick(circle.Core?.Glyph, ref rng) ?? "Wild";
            int projectiles = 0;
            var rule = program?.Rules != null && program.Rules.Count > 0 ? program.Rules[0] : null;
            if (rule != null)
                foreach (var a in rule.Actions)
                    if (a.Kind == ActionKind.Emit && a.Emitter != null)
                        projectiles += a.Emitter.Count;

            // Noun: Seek wins ("Seekers"), else the first shape, else a burst.
            string noun = null;
            foreach (var g in circle.Inner)
                if (g.Glyph.Effect == GlyphEffect.Seek) noun = Pick(g.Glyph, ref rng);
            // A Wall names the spell even though a Line shapes it.
            if (noun == null)
                foreach (var g in circle.Ring)
                    if (g.Glyph.Effect == GlyphEffect.Wall) { noun = Pick(g.Glyph, ref rng); break; }
            if (noun == null)
                foreach (var g in circle.Ring)
                    if (g.Glyph.Effect != GlyphEffect.Delay && g.Glyph.Effect != GlyphEffect.Return) { noun = Pick(g.Glyph, ref rng); break; }
            if (noun == null) noun = "Burst";
            if (projectiles > 1 && !noun.EndsWith("s")) noun += "s";

            // Adjective: the first modifier that isn't already said by the noun.
            string adjective = null;
            foreach (var g in circle.Inner)
            {
                var e = g.Glyph.Effect;
                if (e == GlyphEffect.Seek || e == GlyphEffect.Split) continue;
                adjective = Pick(g.Glyph, ref rng);
                break;
            }
            if (adjective == null)
                foreach (var g in circle.Ring)
                    if (g.Glyph.Effect == GlyphEffect.Delay) { adjective = Pick(g.Glyph, ref rng); break; }

            return new SpellFlavor
            {
                Name = adjective == null ? $"{core} {noun}" : $"{adjective} {core} {noun}",
                Description = Describe(circle, rule),
            };
        }

        static string Pick(GlyphDefinition g, ref Pcg32 rng)
        {
            if (g == null || g.Words.Count == 0) return g?.Name;
            return g.Words[rng.Range(0, g.Words.Count)];
        }

        // ---------- Description ----------

        static readonly string[] Numbers = { "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };

        static string Count(int n) => n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString(CultureInfo.InvariantCulture);

        static string Describe(SpellCircle circle, EffectRule rule)
        {
            if (rule == null) return "";
            string substance = (circle.Core?.Glyph.Name ?? "raw").ToLowerInvariant();
            var parts = new List<string>();
            List<ActionSpec> payload = null;
            foreach (var a in rule.Actions)
            {
                string s = DescribeAction(a, substance);
                if (s != null) parts.Add(s);
                if (payload == null && a.Payload != null && a.Payload.Count > 0) payload = a.Payload;
            }
            if (parts.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append(i == parts.Count - 1 ? ", then " : ", ");
                sb.Append(parts[i]);
            }
            string text = sb.ToString();
            text = char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";
            string hit = DescribePayload(payload);
            return hit == null ? text : text + " " + hit;
        }

        static string DescribeAction(ActionSpec a, string substance)
        {
            switch (a.Kind)
            {
                case ActionKind.Emit:
                {
                    var e = a.Emitter;
                    bool one = e.Count == 1;
                    string what = one ? $"a {substance} bolt" : $"{Count(e.Count)} {substance} bolts";
                    string how;
                    switch (e.Shape)
                    {
                        case EmitShape.Cone: how = e.SpreadDegrees <= 45f ? " in a narrow fan" : " in a wide fan"; break;
                        case EmitShape.Ring: how = " in every direction"; break;
                        case EmitShape.Orbit: how = $" circling you for {Fmt(e.Range)} s"; break;
                        default: how = ""; break;
                    }
                    var extras = new List<string>();
                    if (e.Seeking) extras.Add(one ? "curves toward its target" : "curve toward their targets");
                    if (e.Returning) extras.Add(one ? "flies back to you" : "fly back to you");
                    if (e.SplitCount > 0) extras.Add((one ? "bursts" : "burst") + $" into {Count(e.SplitCount)} more on impact");
                    string wait = e.Delay > 0f ? $"after {Fmt(e.Delay)} s, " : "";
                    string tail = extras.Count == 0 ? "" : " that " + Join(extras);
                    return wait + what + how + tail;
                }
                case ActionKind.CreateArea:
                {
                    var ar = a.Area;
                    if (ar.Shape == AreaShape.Line) return null; // described with its wall
                    string wait = ar.Delay > 0f ? $"after {Fmt(ar.Delay)} s, " : "";
                    return wait + $"a {Fmt(ar.Radius)} m burst of {substance} where you aim";
                }
                case ActionKind.CreateBarrier:
                {
                    var ar = a.Area;
                    string wait = ar.Delay > 0f ? $"after {Fmt(ar.Delay)} s, " : "";
                    return wait + $"a {Fmt(ar.Length)} m wall of {substance} that stands for {Fmt(ar.Duration)} s";
                }
            }
            return null;
        }

        static string DescribePayload(List<ActionSpec> payload)
        {
            if (payload == null) return null;
            var bits = new List<string>();
            foreach (var p in payload)
            {
                switch (p.Kind)
                {
                    case ActionKind.Damage: bits.Add($"deals {Fmt(p.Amount)} {p.DamageKind.ToString().ToLowerInvariant()} damage"); break;
                    case ActionKind.Knockback: bits.Add($"blows the target back {Fmt(p.Amount)} m"); break;
                    case ActionKind.ApplyStatus: bits.Add($"leaves it {p.Status.ToString().ToLowerInvariant()} for {Fmt(p.Duration)} s"); break;
                }
            }
            return bits.Count == 0 ? null : "Each hit " + Join(bits) + ".";
        }

        static string Join(List<string> items)
        {
            if (items.Count == 1) return items[0];
            return string.Join(", ", items.GetRange(0, items.Count - 1)) + " and " + items[items.Count - 1];
        }

        static string Fmt(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
