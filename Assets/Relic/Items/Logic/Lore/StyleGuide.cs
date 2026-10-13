using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Relic.Items
{
    /// <summary>
    /// The style guide from 08-items-bosses: names and lore must sound like this
    /// world, never stock fantasy. Checks are applied to every LLM draft (a
    /// violation is fed back as an error and retried) and the template generator
    /// avoids the same words.
    /// </summary>
    public static class StyleGuide
    {
        /// <summary>Banned stock words and phrases (case-insensitive, matched on word boundaries).</summary>
        public static readonly string[] Forbidden =
        {
            "ethereal", "shadow-whisper", "shadow whisper", "whispering", "whisper", "whispers", "eternal", "eternity",
            "ancient power", "doom", "void",
        };

        public const int MinLoreSentences = 2;
        public const int MaxLoreSentences = 4;

        static readonly HashSet<string> Stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "of", "a", "an", "and", "s", "in", "to", "at", "on", "for", "from", "with",
        };

        /// <summary>Forbidden words found in a text (empty when clean).</summary>
        public static List<string> ForbiddenIn(string text)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            foreach (var w in Forbidden)
                if (Regex.IsMatch(text, @"(?<![\p{L}])" + Regex.Escape(w) + @"(?![\p{L}])", RegexOptions.IgnoreCase))
                    found.Add(w);
            return found;
        }

        /// <summary>Lower-case content words of a name ("Isel's Weir-Bell" → isel, weir, bell).</summary>
        public static List<string> Words(string name)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(name)) return words;
            foreach (Match m in Regex.Matches(name.ToLowerInvariant(), @"\p{L}+"))
                if (!Stopwords.Contains(m.Value) && m.Value.Length > 1) words.Add(m.Value);
            return words;
        }

        /// <summary>Counts sentences by terminal punctuation (". ", "! ", "? " or end).</summary>
        public static int SentenceCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return Regex.Matches(text.Trim(), @"[^.!?]+[.!?]+(\s|$)").Count;
        }
    }

    /// <summary>
    /// Tracks the words in recently generated names. A word used in more than 5%
    /// of recent names is temporarily banned, so "Lantern" can't take over the
    /// relic pool. Deterministic: same names in the same order give the same bans.
    /// </summary>
    public sealed class NameFreshness
    {
        public const float MaxShare = 0.05f;
        readonly int _window;
        readonly Queue<List<string>> _recent = new Queue<List<string>>();
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public NameFreshness(int window = 200) { _window = Math.Max(1, window); }

        readonly Queue<string> _names = new Queue<string>();

        public int Count => _recent.Count;

        public void Add(string name)
        {
            var words = new List<string>(new HashSet<string>(StyleGuide.Words(name)));
            _recent.Enqueue(words);
            _names.Enqueue((name ?? "").Trim().ToLowerInvariant());
            foreach (var w in words) _counts[w] = (_counts.TryGetValue(w, out int c) ? c : 0) + 1;
            while (_recent.Count > _window)
            {
                _names.Dequeue();
                foreach (var w in _recent.Dequeue())
                    if (--_counts[w] == 0) _counts.Remove(w);
            }
        }

        /// <summary>True when exactly this name (case-insensitive) is among the recent names.</summary>
        public bool Contains(string name) => name != null && _names.Contains(name.Trim().ToLowerInvariant());

        /// <summary>Recent names, oldest first (lower case).</summary>
        public List<string> RecentNames() => new List<string>(_names);

        /// <summary>
        /// True when one more use would put the word above 5% of recent names.
        /// (With fewer than 20 names, that means any word already used once.)
        /// </summary>
        public bool IsOverused(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            int c = _counts.TryGetValue(word.ToLowerInvariant(), out int n) ? n : 0;
            if (c == 0) return false;
            return c + 1 > MaxShare * Math.Max(_recent.Count + 1, 20);
        }

        /// <summary>Words currently banned, sorted (for the prompt).</summary>
        public List<string> Banned()
        {
            var list = new List<string>();
            foreach (var kv in _counts) if (IsOverused(kv.Key)) list.Add(kv.Key);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Overused words in a candidate name (empty when fresh).</summary>
        public List<string> OverusedIn(string name)
        {
            var bad = new List<string>();
            foreach (var w in StyleGuide.Words(name)) if (IsOverused(w)) bad.Add(w);
            return bad;
        }
    }
}
