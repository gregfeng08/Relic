using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Relic.Lore
{
    /// <summary>
    /// Cross-checks the whole bible: ids are unique and well-formed, every reference points at
    /// something real, templates only use declared slots, and phonologies can make names.
    /// Returns every problem rather than stopping at the first.
    /// </summary>
    public static class LoreValidator
    {
        static readonly Regex IdRx = new Regex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant);
        static readonly Regex SyllableRx = new Regex(@"^[A-Za-z]+$", RegexOptions.CultureInvariant);
        static readonly Regex ColorRx = new Regex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

        public static readonly string[] ItemNameSlots = { "maker", "form", "place", "epithet", "adjective", "word" };
        public static readonly string[] FactionNameSlots = { "place", "founder", "culture", "word" };
        public static readonly string[] MoodLineSlots = { "maker", "place", "culture", "form", "word", "legend", "faction", "era" };
        public static readonly string[] CultureMoodSlots = { "people", "adjective" };
        public static readonly string[] EraNameSlots = { "word" };
        public static readonly string[] EraSummarySlots = { "culture", "founder", "place", "word" };
        public static readonly string[] FounderSummarySlots = { "founder", "culture", "place", "craft", "form", "value" };
        public static readonly string[] ArtifactSummarySlots = { "culture", "form", "place", "era", "maker" };
        /// <summary>Items.BaseType names a form tag may use (Items checks they parse).</summary>
        public static readonly string[] BaseNames = { "Sword", "Greatsword", "Bow", "Staff", "Tome", "LightArmor", "MediumArmor", "HeavyArmor", "Trinket", "Relic" };

        public static List<string> Validate(LoreBible b)
        {
            var e = new List<string>();
            Rules(e, b);
            Ids(e, "culture", b.Cultures, c => c.Id);
            Ids(e, "faction", b.Factions, f => f.Id);
            Ids(e, "event", b.Events, x => x.Id);
            Ids(e, "motif", b.Motifs, m => m.Id);
            Ids(e, "mood", b.Moods.Moods, m => m.Id);
            Ids(e, "era", b.Eras, x => x.Id);
            Ids(e, "legend", b.Legends, l => l.Id);

            foreach (var c in b.Cultures) Culture(e, b, c);
            foreach (var f in b.Factions) Faction(e, b, f);
            foreach (var t in b.Events) Event(e, b, t);
            foreach (var m in b.Motifs) Motif(e, b, m);
            Moods(e, b.Moods);
            Eras(e, b, b.Eras);
            foreach (var l in b.Legends) Legend(e, b, l);
            if (b.Style.BannedNameSubstrings.Count == 0) e.Add("style: bannedNameSubstrings is empty");
            foreach (var s in b.Style.BannedNameSubstrings)
                if (s != s.ToLowerInvariant()) e.Add("style: banned name substring '" + s + "' must be lower case");
            return e;
        }

        /// <summary>The constitution: phonology families, form tags, culture and era templates.</summary>
        static void Rules(List<string> e, LoreBible b)
        {
            Ids(e, "phonology", b.Phonologies, p => p.Id);
            Ids(e, "culture template", b.CultureTemplates, t => t.Id);
            Ids(e, "era template", b.PrehistoryRules.Eras, t => t.Id);
            foreach (var p in b.Phonologies)
            {
                string w = "phonology " + p.Id;
                Need(e, w, "label", p.Label);
                NeedAny(e, w, "adjectiveEndings", p.AdjectiveEndings);
                foreach (var s in p.AdjectiveEndings)
                    if (s == null || (s.Length > 0 && !SyllableRx.IsMatch(s))) e.Add(w + ": bad adjective ending '" + s + "'");
                Phonology(e, w, p.Phonology);
            }
            foreach (var f in b.Forms)
            {
                string w = "form '" + f.Form + "'";
                Need(e, w, "form", f.Form);
                if (Array.IndexOf(BaseNames, f.Base) < 0) e.Add(w + ": unknown base '" + f.Base + "'");
                if ((f.Base == "Relic") != !string.IsNullOrEmpty(f.Kit)) e.Add(w + ": relic forms (and only they) need a kit");
            }
            if (b.CultureTemplates.Count < CultureGenerator.MinCultures)
                e.Add("culture templates: need at least " + CultureGenerator.MinCultures);
            var covered = new HashSet<LoreFamily>();
            foreach (var t in b.CultureTemplates) CultureTemplate(e, b, t, covered);
            foreach (LoreFamily f in Enum.GetValues(typeof(LoreFamily)))
                if (b.CultureTemplates.Count > 0 && !covered.Contains(f)) e.Add("culture templates: no template likes " + f);

            var pr = b.PrehistoryRules;
            foreach (var t in pr.Eras)
            {
                string w = "era template " + t.Id;
                NeedAny(e, w, "namePatterns", t.NamePatterns);
                NeedAny(e, w, "summaries", t.Summaries);
                NeedAny(e, w, "echoes", t.Echoes);
                if (t.Weight <= 0) e.Add(w + ": weight must be positive");
                foreach (var p in t.NamePatterns)
                {
                    Slots(e, w + " name", p, EraNameSlots);
                    if (TemplateText.SlotsIn(p).Count > 0 && t.Words.Count == 0) e.Add(w + ": name uses {word} but words is empty");
                }
                foreach (var s in t.Summaries) Slots(e, w + " summary", s, EraSummarySlots);
            }
            if (!pr.Eras.Exists(x => x.Position == EraPosition.First)) e.Add("prehistory: no First era template");
            if (!pr.Eras.Exists(x => x.Position == EraPosition.Last)) e.Add("prehistory: no Last era template");
            if (pr.Eras.FindAll(x => x.Position == EraPosition.Middle).Count < 2) e.Add("prehistory: need at least two Middle era templates");
            NeedAny(e, "prehistory", "founderSummaries", pr.FounderSummaries);
            NeedAny(e, "prehistory", "artifactSummaries", pr.ArtifactSummaries);
            NeedAny(e, "prehistory", "brightEpithets", pr.BrightEpithets);
            NeedAny(e, "prehistory", "somberEpithets", pr.SomberEpithets);
            foreach (var s in pr.FounderSummaries) Slots(e, "prehistory founder summary", s, FounderSummarySlots);
            foreach (var s in pr.ArtifactSummaries) Slots(e, "prehistory artifact summary", s, ArtifactSummarySlots);
        }

        static void CultureTemplate(List<string> e, LoreBible b, CultureTemplate t, HashSet<LoreFamily> covered)
        {
            string w = "culture template " + t.Id;
            Need(e, w, "label", t.Label);
            if (t.Weight <= 0) e.Add(w + ": weight must be positive");
            NeedAny(e, w, "families", t.Families);
            covered.UnionWith(t.Families);
            NeedAny(e, w, "climates", t.Climates);
            NeedAny(e, w, "phonologies", t.Phonologies);
            foreach (var id in t.Phonologies) if (b.Phonology(id) == null) e.Add(w + ": unknown phonology '" + id + "'");
            NeedAny(e, w, "identities", t.Identities);
            NeedAny(e, w, "architecture", t.Architecture);
            NeedAny(e, w, "crafts", t.Crafts);
            NeedAny(e, w, "materials", t.Materials);
            NeedAny(e, w, "metals", t.Metals);
            NeedAny(e, w, "foods", t.Foods);
            NeedAny(e, w, "festivals", t.Festivals);
            NeedAny(e, w, "values", t.Values);
            NeedAny(e, w, "prides", t.Prides);
            NeedAny(e, w, "factions", t.Factions);
            NeedAny(e, w, "motifs", t.Motifs);
            NeedAny(e, w, "founderEpithets", t.FounderEpithets);
            if (t.MoodLines.Count < 2) e.Add(w + ": needs at least two mood lines");
            foreach (var l in t.MoodLines) Slots(e, w + " mood line", l, CultureMoodSlots);
            foreach (var id in t.Factions) if (b.Faction(id) == null) e.Add(w + ": unknown faction '" + id + "'");
            foreach (var id in t.Motifs) if (b.Motif(id) == null) e.Add(w + ": unknown motif '" + id + "'");
            int relic = 0;
            foreach (var f in t.ItemForms)
            {
                var tags = b.FormTagsOf(f);
                if (tags.Count == 0) e.Add(w + ": form '" + f + "' has no tag in forms.json");
                if (tags.Exists(x => x.Base == "Relic")) relic++;
            }
            if (relic < 2) e.Add(w + ": needs at least two relic forms");
            var n = t.ItemNaming;
            NeedAny(e, w, "itemNaming.patterns", n.Patterns);
            NeedAny(e, w, "itemNaming.epithets", n.Epithets);
            NeedAny(e, w, "itemNaming.adjectives", n.Adjectives);
            NeedAny(e, w, "itemNaming.makersMarks", n.MakersMarks);
            foreach (var p in n.Patterns) Slots(e, w + " item pattern", p, ItemNameSlots);
        }

        static void Ids<T>(List<string> e, string kind, List<T> items, Func<T, string> id)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in items)
            {
                string v = id(it);
                if (string.IsNullOrEmpty(v) || !IdRx.IsMatch(v)) e.Add(kind + ": bad id '" + v + "' (lower-case kebab-case)");
                else if (!seen.Add(v)) e.Add(kind + ": duplicate id '" + v + "'");
            }
        }

        static void Need(List<string> e, string where, string what, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) e.Add(where + ": missing " + what);
        }

        static void NeedAny<T>(List<string> e, string where, string what, List<T> list)
        {
            if (list == null || list.Count == 0) e.Add(where + ": " + what + " is empty");
        }

        static void Slots(List<string> e, string where, string template, ICollection<string> allowed)
        {
            if (TemplateText.HasStrayBraces(template)) e.Add(where + ": stray brace in \"" + template + "\"");
            foreach (var s in TemplateText.SlotsIn(template))
                if (!allowed.Contains(s)) e.Add(where + ": unknown slot {" + s + "} in \"" + template + "\"");
        }

        static void Culture(List<string> e, LoreBible b, CultureDef c)
        {
            string w = "culture " + c.Id;
            Need(e, w, "name", c.Name);
            Need(e, w, "adjective", c.Adjective);
            Need(e, w, "identity", c.Identity);
            Need(e, w, "architecture", c.Architecture);
            Need(e, w, "pride", c.Pride);
            Need(e, w, "mood", c.Mood);
            foreach (var f in c.ItemForms)
                if (!c.FormTags.Exists(x => string.Equals(x.Form, f, StringComparison.OrdinalIgnoreCase))) e.Add(w + ": form '" + f + "' has no tag");
            NeedAny(e, w, "families", c.Families);
            if (new HashSet<LoreFamily>(c.Families).Count != c.Families.Count) e.Add(w + ": duplicate family");
            NeedAny(e, w, "climates", c.Climates);
            NeedAny(e, w, "crafts", c.Crafts);
            NeedAny(e, w, "materials", c.Materials);
            NeedAny(e, w, "foods", c.Foods);
            NeedAny(e, w, "festivals", c.Festivals);
            NeedAny(e, w, "values", c.Values);
            NeedAny(e, w, "itemForms", c.ItemForms);
            foreach (var f in c.Festivals) { Need(e, w, "festival name", f.Name); Need(e, w, "festival description", f.Description); }
            foreach (var id in c.Factions) if (b.Faction(id) == null) e.Add(w + ": unknown faction '" + id + "'");
            foreach (var id in c.Motifs) if (b.Motif(id) == null) e.Add(w + ": unknown motif '" + id + "'");
            NeedAny(e, w, "factions", c.Factions);
            NeedAny(e, w, "motifs", c.Motifs);
            foreach (var r in c.Relations)
            {
                if (b.Culture(r.Culture) == null) e.Add(w + ": relation to unknown culture '" + r.Culture + "'");
                if (r.Culture == c.Id) e.Add(w + ": relation to itself");
            }
            var n = c.ItemNaming;
            NeedAny(e, w, "itemNaming.patterns", n.Patterns);
            NeedAny(e, w, "itemNaming.epithets", n.Epithets);
            NeedAny(e, w, "itemNaming.adjectives", n.Adjectives);
            NeedAny(e, w, "itemNaming.makersMarks", n.MakersMarks);
            foreach (var p in n.Patterns) Slots(e, w + " item pattern", p, ItemNameSlots);
            Phonology(e, w, c.Phonology);
        }

        static void Phonology(List<string> e, string w, Phonology p)
        {
            NeedAny(e, w, "phonology.starts", p.Starts);
            NeedAny(e, w, "phonology.ends", p.Ends);
            NeedAny(e, w, "phonology.patterns", p.Patterns);
            foreach (var list in new[] { p.Starts, p.Middles, p.Ends, p.Places.Suffixes })
                foreach (var s in list)
                    if (s == null || !SyllableRx.IsMatch(s)) e.Add(w + ": bad syllable '" + s + "' (letters only)");
            bool usesMiddle = p.SurnamePattern != null && p.SurnamePattern.IndexOf('M') >= 0;
            foreach (var pat in p.Patterns)
            {
                if (!Shape(pat.Shape)) e.Add(w + ": bad pattern shape '" + pat.Shape + "'");
                if (pat.Weight <= 0) e.Add(w + ": pattern weight must be positive");
                if (pat.Shape != null && pat.Shape.IndexOf('M') >= 0) usesMiddle = true;
            }
            if (usesMiddle && p.Middles.Count == 0) e.Add(w + ": patterns use M but phonology.middles is empty");
            if (p.SurnameChance < 0 || p.SurnameChance > 1) e.Add(w + ": surnameChance outside 0..1");
            if (p.SurnameChance > 0 && !Shape(p.SurnamePattern)) e.Add(w + ": bad surnamePattern '" + p.SurnamePattern + "'");
            if (p.MinLength < 2 || p.MaxLength < p.MinLength) e.Add(w + ": bad name length bounds");
            if (p.Places.Suffixes.Count == 0 && p.Places.Words.Count == 0) e.Add(w + ": places need suffixes or words");
            if (p.Places.SuffixChance < 0 || p.Places.SuffixChance > 1) e.Add(w + ": places.suffixChance outside 0..1");
        }

        static bool Shape(string s)
        {
            if (string.IsNullOrEmpty(s) || s[0] != 'S') return false;
            foreach (char ch in s) if (ch != 'S' && ch != 'M' && ch != 'E') return false;
            return s.LastIndexOf('S') == 0;
        }

        static void Faction(List<string> e, LoreBible b, FactionArchetype f)
        {
            string w = "faction " + f.Id;
            Need(e, w, "label", f.Label);
            NeedAny(e, w, "namePatterns", f.NamePatterns);
            NeedAny(e, w, "goals", f.Goals);
            NeedAny(e, w, "symbols", f.Symbols);
            if (f.Weight <= 0) e.Add(w + ": weight must be positive");
            foreach (var p in f.NamePatterns) Slots(e, w, p, FactionNameSlots);
            foreach (var list in new[] { f.Allies, f.Rivals, f.Opposes })
                foreach (var id in list)
                    if (b.Faction(id) == null || id == f.Id) e.Add(w + ": bad relation '" + id + "'");
        }

        static void Event(List<string> e, LoreBible b, EventTemplate t)
        {
            string w = "event " + t.Id;
            Need(e, w, "summary", t.Summary);
            NeedAny(e, w, "eras", t.Eras);
            NeedAny(e, w, "slots", t.Slots);
            NeedAny(e, w, "namePatterns", t.NamePatterns);
            NeedAny(e, w, "leaves", t.Leaves);
            if (t.Weight <= 0) e.Add(w + ": weight must be positive");
            foreach (var id in t.Eras) if (b.EraTemplate(id) == null) e.Add(w + ": unknown era template '" + id + "'");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in t.Slots)
                if (string.IsNullOrEmpty(s.Name) || !names.Add(s.Name)) e.Add(w + ": bad or duplicate slot '" + s.Name + "'");
            foreach (var p in t.NamePatterns) Slots(e, w + " name", p, names);
            Slots(e, w + " summary", t.Summary, names);
            var used = new HashSet<string>(TemplateText.SlotsIn(t.Summary));
            foreach (var p in t.NamePatterns) used.UnionWith(TemplateText.SlotsIn(p));
            foreach (var s in t.Slots) if (!used.Contains(s.Name)) e.Add(w + ": slot '" + s.Name + "' is never used");
        }

        static void Motif(List<string> e, LoreBible b, MotifDef m)
        {
            string w = "motif " + m.Id;
            NeedAny(e, w, "words", m.Words);
            NeedAny(e, w, "statuses", m.Statuses);
            NeedAny(e, w, "shapes", m.Shapes);
            NeedAny(e, w, "tones", m.Tones);
            NeedAny(e, w, "moods", m.Moods);
            NeedAny(e, w, "materials", m.Materials);
            NeedAny(e, w, "families", m.Families);
            if (m.Statuses.Contains(Relic.Core.Effects.StatusId.None)) e.Add(w + ": status None");
            if (m.Color == null || !ColorRx.IsMatch(m.Color)) e.Add(w + ": bad color '" + m.Color + "'");
            foreach (var id in m.Moods)
            {
                var mood = b.Mood(id);
                if (mood == null) e.Add(w + ": unknown mood '" + id + "'");
                else if (!m.Tones.Contains(mood.Tone)) e.Add(w + ": mood '" + id + "' is " + mood.Tone + " but the motif lacks that tone");
            }
        }

        static void Moods(List<string> e, MoodTable t)
        {
            if (!(t.BrightShare > 0 && t.BrightShare < 1)) e.Add("moods: brightShare must be between 0 and 1");
            bool bright = false, somber = false;
            foreach (var m in t.Moods)
            {
                string w = "mood " + m.Id;
                if (m.Weight <= 0) e.Add(w + ": weight must be positive");
                NeedAny(e, w, "words", m.Words);
                NeedAny(e, w, "lines", m.Lines);
                NeedAny(e, w, "effectLean", m.EffectLean);
                foreach (var line in m.Lines) Slots(e, w, line, MoodLineSlots);
                if (m.Tone == Tone.Bright) bright = true; else somber = true;
            }
            if (!bright || !somber) e.Add("moods: need at least one Bright and one Somber mood");
        }

        static void Eras(List<string> e, LoreBible b, List<EraDef> eras)
        {
            var orders = new HashSet<int>();
            foreach (var x in eras)
            {
                string w = "era " + x.Id;
                Need(e, w, "name", x.Name);
                Need(e, w, "summary", x.Summary);
                NeedAny(e, w, "echoes", x.Echoes);
                if (x.StartYearsAgo < x.EndYearsAgo) e.Add(w + ": starts after it ends");
                if (b.EraTemplate(x.Template) == null) e.Add(w + ": unknown era template '" + x.Template + "'");
                if (x.Order < 0 || x.Order >= eras.Count || !orders.Add(x.Order)) e.Add(w + ": orders must be 0..n-1, unique");
            }
        }

        static void Legend(List<string> e, LoreBible b, LegendDef l)
        {
            string w = "legend " + l.Id;
            Need(e, w, "name", l.Name);
            Need(e, w, "summary", l.Summary);
            if (l.Culture != null && b.Culture(l.Culture) == null) e.Add(w + ": unknown culture '" + l.Culture + "'");
            if (b.Era(l.Era) == null) e.Add(w + ": unknown era '" + l.Era + "'");
            foreach (var id in l.Motifs) if (b.Motif(id) == null) e.Add(w + ": unknown motif '" + id + "'");
            foreach (var id in l.Related) if (b.Legend(id) == null || id == l.Id) e.Add(w + ": bad related legend '" + id + "'");
        }
    }
}
