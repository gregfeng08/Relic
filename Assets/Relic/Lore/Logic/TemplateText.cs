using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Relic.Lore
{
    /// <summary>
    /// Slot templates: "The Founding of {place}". Slots are lower-case words in braces.
    /// Filling is strict: a missing value throws, so a template can never leak "{place}".
    /// </summary>
    public static class TemplateText
    {
        static readonly Regex SlotRx = new Regex(@"\{([a-zA-Z][a-zA-Z0-9]*)\}", RegexOptions.CultureInvariant);

        /// <summary>Slot names in order of first appearance.</summary>
        public static List<string> SlotsIn(string template)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(template)) return list;
            foreach (Match m in SlotRx.Matches(template))
                if (!list.Contains(m.Groups[1].Value)) list.Add(m.Groups[1].Value);
            return list;
        }

        /// <summary>True when braces appear that are not well-formed slots ("{ place}", "{").</summary>
        public static bool HasStrayBraces(string template)
        {
            if (string.IsNullOrEmpty(template)) return false;
            string stripped = SlotRx.Replace(template, "");
            return stripped.IndexOf('{') >= 0 || stripped.IndexOf('}') >= 0;
        }

        public static string Fill(string template, IDictionary<string, string> values)
        {
            return SlotRx.Replace(template ?? "", m =>
            {
                string key = m.Groups[1].Value;
                if (values == null || !values.TryGetValue(key, out string v) || v == null)
                    throw new ArgumentException("No value for slot {" + key + "} in \"" + template + "\"");
                return v;
            });
        }

        /// <summary>Upper-cases the first letter (for lines that start with a slot).</summary>
        public static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        static readonly Regex ArticleRx = new Regex(@"\b([Aa]) (?=[AEIOUaeiou])", RegexOptions.CultureInvariant);

        /// <summary>"a Eskeln cloak" → "an Eskeln cloak", "A oar-staff" → "An oar-staff" (after filling slots).</summary>
        public static string FixArticles(string s) =>
            string.IsNullOrEmpty(s) ? s : ArticleRx.Replace(s, m => m.Groups[1].Value + "n ");

        /// <summary>Whole-word, case-insensitive hits of banned words or phrases in a text.</summary>
        public static List<string> BannedIn(string text, IEnumerable<string> banned)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text) || banned == null) return found;
            foreach (var w in banned)
            {
                if (string.IsNullOrEmpty(w)) continue;
                if (Regex.IsMatch(text, @"(?<![\p{L}])" + Regex.Escape(w) + @"(?![\p{L}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    found.Add(w);
            }
            return found;
        }
    }
}
