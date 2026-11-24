using System.Text.RegularExpressions;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// Which of a culture's naming patterns (itemNaming.patterns, from its culture template) a name follows.
    /// {form} and {maker} must be the sentence's own words; the other slots are flair.
    /// </summary>
    public static class NamePatterns
    {
        /// <summary>The first matching pattern, or null when the name follows none of them.</summary>
        public static string Match(string name, ItemSentence s, CultureDef culture)
        {
            if (string.IsNullOrEmpty(name) || s == null || culture == null) return null;
            foreach (var p in culture.ItemNaming.Patterns)
                if (Matches(name, p, s)) return p;
            return null;
        }

        public static bool Matches(string name, string pattern, ItemSentence s)
        {
            string rx = Regex.Escape(pattern)
                .Replace(@"\{form}", Regex.Escape(LoreSampler.Title(s.Form ?? "")))
                .Replace(@"\{maker}", Regex.Escape(LoreSampler.FirstWord(s.Maker ?? "")))
                .Replace(@"\{place}", @"[\p{L}' -]+")
                .Replace(@"\{epithet}", @"[\p{L}'-]+(?: [\p{L}'-]+)?")
                .Replace(@"\{adjective}", @"[\p{L}'-]+")
                .Replace(@"\{word}", @"[\p{L}'-]+");
            return Regex.IsMatch(name, "^" + rx + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
