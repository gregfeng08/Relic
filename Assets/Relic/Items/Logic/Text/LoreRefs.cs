using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Relic.Lore;

namespace Relic.Items.Text
{
    /// <summary>
    /// Proper nouns in generated text. Finds which world and Bible names a text mentions
    /// (for worldRefs) and which capitalized words it uses that nobody gave it: the style
    /// guide's first rule is "use only the names you're given".
    /// </summary>
    public static class LoreRefs
    {
        static readonly Regex Capitalized = new Regex(@"(?<![\p{L}'’-])\p{Lu}[\p{L}]*(?:[-'’]\p{L}+)*", RegexOptions.CultureInvariant);

        /// <summary>Ids of every world entity, legend and era named in the text (whole words, case-sensitive).</summary>
        public static List<string> Scan(string text, WorldContext w)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(text) || w == null) return ids;
            foreach (var id in w.AllIds())
            {
                string name = DraftValidator.StripArticle(w.NameOf(id));
                if (Mentions(text, name)) ids.Add(id);
            }
            return ids;
        }

        public static bool Mentions(string text, string name)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(name)) return false;
            return Regex.IsMatch(text, @"(?<![\p{L}])" + Regex.Escape(name) + @"(?![\p{L}])", RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Capitalized words a text may use: every name in the world context, every name and
        /// term the Lore Bible itself uses (legends, eras, cultures, festivals, mood lines),
        /// and the item sentence's own facts (maker, place, culture, form).
        /// </summary>
        public static HashSet<string> AllowedProperNouns(WorldContext w, ItemSentence s)
        {
            var set = new HashSet<string>(StringComparer.Ordinal) { "I" };
            void Add(string text)
            {
                if (string.IsNullOrEmpty(text)) return;
                foreach (Match m in Capitalized.Matches(text)) set.Add(Clean(m.Value));
            }
            if (w != null)
            {
                foreach (var r in w.Regions) { Add(r.Name); Add(r.Landmark); }
                foreach (var f in w.Factions) Add(f.Name);
                foreach (var p in w.Figures) { Add(p.Name); Add(p.Title); }
                foreach (var e in w.Events) Add(e.Name);
                var bible = w.Bible;
                if (bible != null)
                {
                    foreach (var l in bible.Legends) { Add(l.Name); Add(l.Epithet); Add(l.Summary); }
                    foreach (var e in bible.Eras) { Add(e.Name); Add(e.Summary); foreach (var x in e.Echoes) Add(x); }
                    foreach (var c in bible.Cultures)
                    {
                        Add(c.Name); Add(c.Adjective); Add(c.Identity); Add(c.Pride);
                        foreach (var f in c.Festivals) Add(f.Name);
                        foreach (var x in c.ItemNaming.Epithets) Add(x);
                        foreach (var x in c.ItemNaming.Adjectives) Add(x);
                    }
                    foreach (var m in bible.Moods.Moods) { foreach (var x in m.Lines) Add(x); foreach (var x in m.Words) Add(x); }
                    foreach (var f in bible.Factions) { Add(f.Label); foreach (var x in f.NamePatterns) Add(x); }
                }
            }
            if (s != null)
            {
                Add(s.Maker); Add(s.MakerHome); Add(s.CultureAdjective);
                Add(Relic.Lore.LoreSampler.Title(s.Form ?? ""));
            }
            return set;
        }

        /// <summary>Capitalized words not at a sentence start that aren't in the allowed set.</summary>
        public static List<string> Invented(string text, HashSet<string> allowed)
        {
            var bad = new List<string>();
            if (string.IsNullOrEmpty(text)) return bad;
            foreach (Match m in Capitalized.Matches(text))
            {
                if (AtSentenceStart(text, m.Index)) continue;
                string word = Clean(m.Value);
                if (!allowed.Contains(word) && !bad.Contains(word)) bad.Add(word);
            }
            return bad;
        }

        static bool AtSentenceStart(string text, int i)
        {
            int j = i - 1;
            while (j >= 0 && (text[j] == ' ' || text[j] == '"' || text[j] == '“' || text[j] == '(')) j--;
            return j < 0 || text[j] == '.' || text[j] == '!' || text[j] == '?' || text[j] == ':';
        }

        static string Clean(string word)
        {
            if (word.EndsWith("'s", StringComparison.Ordinal) || word.EndsWith("’s", StringComparison.Ordinal)) word = word.Substring(0, word.Length - 2);
            return word;
        }
    }
}
