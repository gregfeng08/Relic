using System;
using System.Collections.Generic;
using Relic.Core.Random;
using System.Text.RegularExpressions;
using Relic.Lore;

namespace Relic.Items.Text
{
    // TemplateAuthor's words: names from the culture's naming patterns, Rare flavor lines,
    // and lore from the Bible's mood lines plus (Legendary) a lead toward another place.
    public static partial class TemplateAuthor
    {
        // ---------- names ----------

        static string WordKey(string name)
        {
            var w = StyleGuide.Words(name);
            w.Sort(StringComparer.Ordinal);
            return string.Join(" ", w);
        }

        /// <summary>Words of the sentence's facts that a name must be allowed to repeat (form, maker, place).</summary>
        public static HashSet<string> FactWords(ItemSentence s)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (s == null) return set;
            foreach (var w in StyleGuide.Words(s.Form)) set.Add(w);
            foreach (var w in StyleGuide.Words(s.Maker)) set.Add(w);
            foreach (var w in StyleGuide.Words(s.MakerHome)) set.Add(w);
            return set;
        }

        static string Name(RelicBrief b, CultureDef culture, Theme theme)
        {
            var s = b.Sentence;
            var rng = Pcg32.ForPurpose(b.Seed, "relic.template.name");
            var naming = culture.ItemNaming;
            var patterns = new List<string>();
            foreach (var p in naming.Patterns)
                if (b.Kind != DraftKind.RareName || !p.Contains("{maker}")) patterns.Add(p); // makers are named from Epic up
            if (patterns.Count == 0) patterns.Add("The {adjective} {form}");

            var candidates = new List<string>();
            for (int round = 0; round < 8; round++)
            {
                var order = new List<string>(patterns);
                for (int i = order.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = order[i]; order[i] = order[j]; order[j] = t; }
                foreach (var p in order)
                {
                    string adjective = theme.IsSomber || naming.Adjectives.Count == 0 || rng.Chance(0.3f)
                        ? theme.Mood.Words[rng.Range(0, theme.Mood.Words.Count)]
                        : naming.Adjectives[rng.Range(0, naming.Adjectives.Count)];
                    var values = new Dictionary<string, string>
                    {
                        ["maker"] = LoreSampler.FirstWord(s.Maker),
                        ["form"] = LoreSampler.Title(s.Form),
                        ["place"] = s.MakerHome,
                        ["epithet"] = naming.Epithets.Count > 0 ? naming.Epithets[rng.Range(0, naming.Epithets.Count)] : adjective,
                        ["adjective"] = adjective,
                        ["word"] = LoreSampler.Title(theme.Words[rng.Range(0, theme.Words.Count)]),
                    };
                    candidates.Add(TemplateText.Fill(p, values));
                }
            }

            var exempt = FactWords(s);
            var banned = new HashSet<string>(b.BannedWords ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var taken = new HashSet<string>(b.TakenNames ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var takenKeys = new HashSet<string>();
            foreach (var t in taken) takenKeys.Add(WordKey(t));
            foreach (var cand in candidates)
            {
                if (cand.Length > DraftValidator.MaxNameLength) continue;
                if (cand.Split(' ').Length > DraftValidator.MaxNameWords) continue;
                if (StyleGuide.ForbiddenIn(cand).Count > 0) continue;
                if (taken.Contains(cand) || takenKeys.Contains(WordKey(cand))) continue;
                var words = StyleGuide.Words(cand);
                if (new HashSet<string>(words).Count != words.Count) continue; // "Lantern of the Lantern"
                bool ok = true;
                foreach (var w in words) if (banned.Contains(w) && !exempt.Contains(w)) { ok = false; break; }
                if (ok) return cand;
            }
            return candidates[0];
        }

        // ---------- flavor (Rare) ----------

        static readonly string[] BrightFlavor =
        {
            "Made in {place}, where they still argue about the best way to oil {material}.",
            "It smells faintly of {word}, like most good things from {place}.",
            "Somebody in {place} scratched a little {word} on the {form}, for luck.",
            "Good {material}, {tag}; somebody in {place} made sure of it.",
            "Bought at a fair in {place} for a song, and the seller sang it.",
        };

        static readonly string[] SomberFlavor =
        {
            "Mended more than once, and carried a long way from {place}.",
            "The {material} is worn smooth where an old owner held it, somewhere past {place}.",
            "Somebody in {place} kept it oiled for years, though nobody came back for it.",
        };

        static string Flavor(RelicBrief b, Theme theme, ref Pcg32 rng)
        {
            var s = b.Sentence;
            var lines = theme.IsSomber ? SomberFlavor : BrightFlavor;
            var values = new Dictionary<string, string>
            {
                ["place"] = s.MakerHome,
                ["material"] = s.Material,
                ["tag"] = s.MaterialTag ?? "well made",
                ["form"] = s.Form,
                ["word"] = theme.Words[rng.Range(0, theme.Words.Count)],
            };
            return TemplateText.FixArticles(TemplateText.Fill(lines[rng.Range(0, lines.Length)], values));
        }

        // ---------- lore (Epic, Legendary) ----------

        static readonly string[] BrightLeads =
        {
            "It hums a little louder on the road to {lead}, where things from {era} still turn up.",
            "{leadFaction} at {lead} keep a sketch of one just like it, signed by someone who knew {legend}.",
            "Its maker learned the trick from a drawing from {era}, still pinned up at {lead}.",
            "The pattern on its side matches a milestone near {lead}, left over from {era}.",
            "Hold it up to the wind and it leans toward {lead}, the way {legend} once walked.",
            "Wayfarers say {legend} carried one like it on the road to {lead}.",
            "Ask at {lead}: the old folk there still sing a song about it from {era}.",
            "It warms a little near {lead}, as if it remembers being carried there in {era}.",
        };

        static readonly string[] SomberLeads =
        {
            "It grows heavy near {lead}, where the old roads of {era} end in wildflowers.",
            "Someone at {lead} still keeps a candle lit for its owner, as folk there have since {era}.",
            "It points, quietly but always, toward {lead}, and nobody there will say why.",
            "The last letter that mentions it came from {lead}, a little after {era}.",
        };

        static string Lore(RelicBrief b, CultureDef culture, Theme theme, ref Pcg32 rng, out List<string> refs)
        {
            var s = b.Sentence;
            var w = b.World;
            var bible = w.Lore;
            var tone = theme.Tone;
            // {legend} is always a person (artifacts can't walk or be known), in the item's tone where possible.
            var legends = bible.Legends.FindAll(l => l.Tone == tone && l.Kind == LegendKind.Figure);
            if (legends.Count == 0) legends = bible.Legends.FindAll(l => l.Kind == LegendKind.Figure);
            var motifLegends = legends.FindAll(l => l.Motifs.Contains(theme.Motif.Id));
            var legend = (motifLegends.Count > 0 && rng.Chance(0.6f) ? motifLegends : legends)[rng.Range(0, (motifLegends.Count > 0 ? motifLegends : legends).Count)];
            var factions = w.Factions.FindAll(f => f.Culture == culture.Id);
            if (factions.Count == 0) factions = w.Factions;
            var faction = factions[rng.Range(0, factions.Count)];

            var values = new Dictionary<string, string>
            {
                ["maker"] = s.Maker,
                ["place"] = s.MakerHome,
                ["culture"] = culture.Adjective,
                ["form"] = s.Form,
                ["word"] = theme.Words[rng.Range(0, theme.Words.Count)],
                ["legend"] = LoreSampler.LegendName(legend),
                ["faction"] = faction.Name,
                ["era"] = LoreSampler.PastEra(bible, tone, ref rng).Name,
            };

            // A mood line that names something real (a place, a faction, a legend) and keeps
            // to the style guide's sentence limits. Legendary lines leave room for the lead.
            int maxSentences = StyleGuide.MaxLoreSentences - (b.Kind == DraftKind.Legendary ? 1 : 0);
            var shortValues = new Dictionary<string, string>(values) { ["legend"] = legend.Name, ["maker"] = LoreSampler.FirstWord(s.Maker) };
            var lines = new List<string>(theme.Mood.Lines);
            Shuffle(lines, ref rng);
            string line = null, fallback = null;
            foreach (var l in lines)
                foreach (var v in new[] { values, shortValues })
                {
                    if (line != null) break;
                    string cand = Sentences(TemplateText.Fill(l, v));
                    if (StyleGuide.SentenceCount(cand) > maxSentences || StyleGuide.LongSentences(cand).Count > 0) continue;
                    fallback = fallback ?? cand;
                    if (LoreRefs.Scan(cand, w).Count > 0) line = cand;
                }
            line = line ?? fallback ?? Sentences(TemplateText.Fill(lines[0], shortValues));

            string text = line;
            if (b.Kind == DraftKind.Legendary)
            {
                var leadPool = w.Regions.FindAll(r => r.Id != s.MakerHomeId && r.Id != s.RegionId && !LoreRefs.Mentions(line, r.Name));
                if (leadPool.Count == 0) leadPool = w.Regions;
                var lead = leadPool[rng.Range(0, leadPool.Count)];
                var leadFaction = w.Factions[rng.Range(0, w.Factions.Count)];
                // Leads look back to a past era in the item's tone where the world has one (never the present).
                var leadValues = new Dictionary<string, string>
                {
                    ["lead"] = lead.Name,
                    ["leadFaction"] = leadFaction.Name,
                    ["era"] = LoreSampler.PastEra(bible, tone, ref rng).Name,
                    ["legend"] = legend.Name,
                };
                // Try the leads in a seeded order; keep the first that is short enough and, with the line, names a legend or era.
                var leads = new List<string>(tone == Tone.Somber ? SomberLeads : BrightLeads);
                Shuffle(leads, ref rng);
                string chosen = null;
                foreach (var l in leads)
                {
                    string cand = Sentences(TemplateText.Fill(l, leadValues));
                    if (StyleGuide.LongSentences(cand).Count > 0) continue;
                    if (Echoes(line, cand)) continue; // don't hum twice or name the same legend twice
                    chosen = chosen ?? cand;
                    if (HasBibleRef(line + " " + cand, w)) { chosen = cand; break; }
                }
                text = line + " " + (chosen ?? Sentences(TemplateText.Fill(leads[0], leadValues)));
            }
            refs = LoreRefs.Scan(text, w);
            if (refs.Count > 8) refs.RemoveRange(8, refs.Count - 8);
            return text;
        }

        static void Shuffle<T>(List<T> list, ref Pcg32 rng)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = list[i]; list[i] = list[j]; list[j] = t; }
        }

        static readonly string[] EchoWords = { "hum", "glow", "lean", "warm", "pattern" };

        /// <summary>True when the lead repeats the line's verb or legend ("hums like a kettle" ... "hums a little louder").</summary>
        static bool Echoes(string line, string lead)
        {
            foreach (var w in EchoWords)
                if (line.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 && lead.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (Match m in Regex.Matches(lead, @"p{Lu}p{L}+ p{Lu}p{L}+"))
                if (line.Contains(m.Value)) return true;
            return false;
        }

        static bool HasBibleRef(string text, WorldContext w)
        {
            foreach (var id in LoreRefs.Scan(text, w)) if (w.IsBibleRef(id)) return true;
            return false;
        }

        /// <summary>Fixes articles and capitalizes every sentence start ("the Bramwick Map House lend..." → "The ...").</summary>
        public static string Sentences(string text)
        {
            text = TemplateText.FixArticles(text);
            var chars = text.ToCharArray();
            bool start = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (start && char.IsLetter(chars[i])) { chars[i] = char.ToUpperInvariant(chars[i]); start = false; }
                else if (chars[i] == '.' || chars[i] == '!' || chars[i] == '?') start = true;
                else if (!char.IsWhiteSpace(chars[i])) start = false;
            }
            return new string(chars);
        }
    }
}
