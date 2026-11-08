using System;
using System.Collections.Generic;
using System.Text;
using Relic.Core.Random;

namespace Relic.Lore
{
    /// <summary>
    /// Deterministic names in one culture's phonology. Same culture + seed gives the same name
    /// on every machine. Every name passes <see cref="IsPlausible"/>: letters only, within the
    /// culture's length bounds, no tripled letters, no banned substrings. Proper nouns in the
    /// world come from here; the LLM may use them but never invents its own.
    /// </summary>
    public sealed class NameGenerator
    {
        const int MaxAttempts = 48;

        readonly CultureDef _culture;
        readonly Phonology _p;
        readonly List<string> _banned;
        readonly int _patternWeight;

        public NameGenerator(CultureDef culture, StyleRules style)
        {
            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _p = culture.Phonology;
            _banned = style != null ? style.BannedNameSubstrings : new List<string>();
            foreach (var pat in _p.Patterns) _patternWeight += Math.Max(0, pat.Weight);
        }

        public CultureDef Culture => _culture;

        /// <summary>A person's name for this seed (given name, sometimes a family name).</summary>
        public string Person(ulong seed)
        {
            var rng = new Pcg32(Hash.Combine(seed, Hash.Of("lore.name.person:" + _culture.Id)));
            return Person(ref rng);
        }

        /// <summary>A place name for this seed.</summary>
        public string Place(ulong seed)
        {
            var rng = new Pcg32(Hash.Combine(seed, Hash.Of("lore.name.place:" + _culture.Id)));
            return Place(ref rng);
        }

        public string Person(ref Pcg32 rng)
        {
            string given = Word(ref rng, null);
            if (_p.SurnameChance > 0 && rng.NextDouble() < _p.SurnameChance)
            {
                string family = Word(ref rng, _p.SurnamePattern);
                if (family != given) return given + " " + family;
            }
            return given;
        }

        public string Place(ref Pcg32 rng)
        {
            var places = _p.Places;
            for (int i = 0; i < MaxAttempts; i++)
            {
                bool suffix = places.Suffixes.Count > 0 && (places.Words.Count == 0 || rng.NextDouble() < places.SuffixChance);
                if (!suffix) return Word(ref rng, null) + " " + places.Words[rng.Range(0, places.Words.Count)];
                // Bound suffixes go on a short root ("S" or "SE") so compounds stay readable: Bramfold, not Gillibetfold.
                string root = Raw(ref rng, rng.NextDouble() < 0.5 ? "S" : "SE");
                string name = Normalize(Join(root, places.Suffixes[rng.Range(0, places.Suffixes.Count)]));
                if (IsPlausible(name, _p.MaxLength + PlaceExtraLength)) return name;
            }
            return Fallback() + (places.Words.Count > 0 ? " " + places.Words[0] : places.Suffixes[0]);
        }

        /// <summary>Bound place names may run this many letters past the culture's MaxLength.</summary>
        public const int PlaceExtraLength = 2;

        /// <summary>One word of the culture: a root shaped by a weighted pattern (or the given shape).</summary>
        public string Word(ref Pcg32 rng, string shape)
        {
            for (int i = 0; i < MaxAttempts; i++)
            {
                string name = Normalize(Raw(ref rng, shape ?? PickShape(ref rng)));
                if (IsPlausible(name)) return name;
            }
            return Fallback();
        }

        string Raw(ref Pcg32 rng, string shape)
        {
            var sb = new StringBuilder();
            foreach (char ch in shape)
            {
                var set = ch == 'S' ? _p.Starts : ch == 'M' ? _p.Middles : _p.Ends;
                AppendSyllable(sb, set[rng.Range(0, set.Count)]);
            }
            return sb.ToString();
        }

        /// <summary>Plausibility for one word (default bounds) or a longer compound.</summary>
        public bool IsPlausible(string name) => IsPlausible(name, _p.MaxLength);

        bool IsPlausible(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name) || name.Length < _p.MinLength || name.Length > maxLength) return false;
            int run = 1, vowelsRun = 0, consonantsRun = 0;
            bool hasVowel = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetter(c)) return false;
                if (i > 0) run = char.ToLowerInvariant(c) == char.ToLowerInvariant(name[i - 1]) ? run + 1 : 1;
                if (run >= 3) return false;
                bool v = IsVowel(c);
                hasVowel |= v;
                vowelsRun = v ? vowelsRun + 1 : 0;
                consonantsRun = v ? 0 : consonantsRun + 1;
                if (vowelsRun > 3 || consonantsRun > 4) return false;
            }
            if (!hasVowel) return false;
            string lower = name.ToLowerInvariant();
            foreach (var bad in _banned) if (lower.Contains(bad)) return false;
            return true;
        }

        string PickShape(ref Pcg32 rng)
        {
            int r = rng.Range(0, Math.Max(1, _patternWeight));
            foreach (var pat in _p.Patterns)
            {
                r -= Math.Max(0, pat.Weight);
                if (r < 0) return pat.Shape;
            }
            return _p.Patterns[0].Shape;
        }

        /// <summary>The shortest safe name: first start + first end. Validated by tests for every culture.</summary>
        public string Fallback() => Normalize(Join(_p.Starts[0], _p.Ends[0]));

        static void AppendSyllable(StringBuilder sb, string syllable)
        {
            string joined = Join(sb.ToString(), syllable);
            sb.Clear().Append(joined);
        }

        /// <summary>Joins two parts, dropping a repeated vowel at the seam ("Ama" + "a" = "Ama").</summary>
        static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            char last = char.ToLowerInvariant(a[a.Length - 1]);
            char first = char.ToLowerInvariant(b[0]);
            if (last == first && IsVowel(last)) return a + b.Substring(1);
            return a + b;
        }

        /// <summary>Lower-case with a capital first letter.</summary>
        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string lower = s.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        static bool IsVowel(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a': case 'e': case 'i': case 'o': case 'u': case 'y': return true;
                default: return false;
            }
        }
    }
}
