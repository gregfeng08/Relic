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
        public static readonly string[] MoodLineSlots = { "maker", "place", "culture", "form", "word", "legend", "faction" };

        public static List<string> Validate(LoreBible b)
        {
            var e = new List<string>();
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
            Eras(e, b.Eras);
            foreach (var l in b.Legends) Legend(e, b, l);
            if (b.Style.BannedNameSubstrings.Count == 0) e.Add("style: bannedNameSubstrings is empty");
            foreach (var s in b.Style.BannedNameSubstrings)
                if (s != s.ToLowerInvariant()) e.Add("style: banned name substring '" + s + "' must be lower case");
            return e;
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
            foreach (var id in t.Eras) if (b.Era(id) == null) e.Add(w + ": unknown era '" + id + "'");
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

        static void Eras(List<string> e, List<EraDef> eras)
        {
            var orders = new HashSet<int>();
            foreach (var x in eras)
            {
                string w = "era " + x.Id;
                Need(e, w, "name", x.Name);
                Need(e, w, "summary", x.Summary);
                NeedAny(e, w, "echoes", x.Echoes);
                if (x.StartYearsAgo < x.EndYearsAgo) e.Add(w + ": starts after it ends");
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
