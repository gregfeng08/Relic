using System;
using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Random;

namespace Relic.Lore
{
    /// <summary>
    /// Template-mode instantiation of Bible data: names, faction names, relic one-liners and
    /// event summaries filled from a seed. This is a stand-in for the world graph (it invents
    /// places on the spot instead of reading them from the graph) and the basis of
    /// docs/lore/samples.md. Slot conventions: {faction} values carry their own article
    /// ("the Bramwick Map House"); {culture} is the culture's adjective.
    /// </summary>
    public sealed class LoreSampler
    {
        readonly LoreBible _bible;
        readonly MoodPicker _moods;
        readonly Dictionary<string, NameGenerator> _names = new Dictionary<string, NameGenerator>();
        Pcg32 _rng;

        public LoreSampler(LoreBible bible, ulong seed)
        {
            _bible = bible ?? throw new ArgumentNullException(nameof(bible));
            _moods = new MoodPicker(bible.Moods);
            _rng = new Pcg32(Hash.Combine(seed, Hash.Of("lore.sampler")));
            foreach (var c in bible.Cultures) _names[c.Id] = new NameGenerator(c, bible.Style);
        }

        public NameGenerator Names(CultureDef c) => _names[c.Id];
        public LoreBible Bible => _bible;

        public CultureDef AnyCulture() => _bible.Cultures[_rng.Range(0, _bible.Cultures.Count)];

        public string Person(CultureDef c) => _names[c.Id].Person(ref _rng);
        public string Place(CultureDef c) => _names[c.Id].Place(ref _rng);

        /// <summary>A region-sized name: "the Bramwick Downs".</summary>
        public string Region(CultureDef c)
        {
            string root = _names[c.Id].Word(ref _rng, null);
            return "the " + root + " " + RegionNoun(c.Families[0]);
        }

        public static string RegionNoun(LoreFamily f)
        {
            switch (f)
            {
                case LoreFamily.Meadow: return "Downs";
                case LoreFamily.Forest: return "Woods";
                case LoreFamily.Highlands: return "Heights";
                case LoreFamily.Coast: return "Shore";
                case LoreFamily.Badlands: return "Redlands";
                case LoreFamily.Frost: return "Snows";
                default: return "Fens";
            }
        }

        public T Pick<T>(IList<T> list) => list[_rng.Range(0, list.Count)];

        public FactionArchetype FactionArchetypeFor(CultureDef c) => _bible.Faction(Pick(c.Factions));

        /// <summary>A faction name from an archetype, e.g. "The Bramwick Map House".</summary>
        public string FactionName(FactionArchetype f, CultureDef c)
        {
            var values = new Dictionary<string, string>
            {
                ["place"] = Place(c),
                ["founder"] = FirstWord(Person(c)),
                ["culture"] = c.Adjective,
                ["word"] = Title(Pick(_bible.Motif(Pick(c.Motifs)).Words)),
            };
            return TemplateText.Fill(Pick(f.NamePatterns), values);
        }

        /// <summary>"The Bramwick Map House" → "the Bramwick Map House" for use mid-sentence.</summary>
        public static string MidSentence(string name) =>
            name != null && name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name.Substring(4) : name;

        /// <summary>
        /// A relic name ready for mid-sentence use: "the Sun-Mirror Shield of Saez Wells",
        /// "the Good Lantern", but "Alulavi's Seed-Pouch Charm" unchanged.
        /// </summary>
        public static string WithArticle(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.StartsWith("The ", StringComparison.Ordinal)) return MidSentence(name);
            int space = name.IndexOf(' ');
            string first = space < 0 ? name : name.Substring(0, space);
            return first.EndsWith("'s", StringComparison.Ordinal) ? name : "the " + name;
        }

        public MoodDef PickMood() => _moods.Pick(ref _rng);
        public MoodDef PickMoodIn(Tone tone) => _moods.PickIn(ref _rng, tone);

        /// <summary>Fisher-Yates shuffle with the sampler's generator.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Range(0, i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        public sealed class SampleRelic
        {
            public string Name;
            public string Line;
            public CultureDef Culture;
            public MoodDef Mood;
            public MotifDef Motif;
            public string Form;
            public string Maker;
            public string Mark;
        }

        /// <summary>A named relic with a one-line story, in a culture and a mood picked at 70/30.</summary>
        /// <param name="usedLines">Optional: line templates already used; they are avoided while others remain, and the pick is added.</param>
        public SampleRelic RelicOf(CultureDef c, MoodDef mood = null, ICollection<string> usedLines = null)
        {
            mood = mood ?? PickMood();
            var motif = MoodPicker.MotifFor(ref _rng, _bible, mood, c.Motifs);
            string form = Pick(c.ItemForms);
            string maker = Person(c);
            string place = Place(c);
            string word = Pick(motif.Words);
            var naming = c.ItemNaming;
            var nameValues = new Dictionary<string, string>
            {
                ["maker"] = FirstWord(maker),
                ["form"] = Title(form),
                ["place"] = place,
                ["epithet"] = Pick(naming.Epithets),
                ["adjective"] = Pick(naming.Adjectives),
                ["word"] = Title(word),
            };
            var legend = Pick(_bible.Legends);
            var lineValues = new Dictionary<string, string>
            {
                ["maker"] = maker,
                ["place"] = place,
                ["culture"] = c.Adjective,
                ["form"] = form,
                ["word"] = word,
                ["legend"] = LegendName(legend),
                ["faction"] = MidSentence(FactionName(FactionArchetypeFor(c), c)),
            };
            return new SampleRelic
            {
                Name = TemplateText.Fill(Pick(naming.Patterns), nameValues),
                Line = TemplateText.FixArticles(TemplateText.Capitalize(TemplateText.Fill(PickLine(mood, usedLines), lineValues))),
                Culture = c,
                Mood = mood,
                Motif = motif,
                Form = form,
                Maker = maker,
                Mark = Pick(naming.MakersMarks),
            };
        }

        public static string LegendName(LegendDef l) =>
            string.IsNullOrEmpty(l.Epithet) ? l.Name : l.Name + " " + l.Epithet;

        string PickLine(MoodDef mood, ICollection<string> used)
        {
            if (used == null) return Pick(mood.Lines);
            var fresh = mood.Lines.FindAll(l => !used.Contains(l));
            string line = Pick(fresh.Count > 0 ? fresh : mood.Lines);
            used.Add(line);
            return line;
        }

        /// <summary>An event's name and summary with every slot filled from the culture.</summary>
        public KeyValuePair<string, string> EventOf(EventTemplate t, CultureDef c)
        {
            var values = new Dictionary<string, string>();
            foreach (var s in t.Slots) values[s.Name] = SlotValue(s, c);
            string name = TemplateText.Capitalize(TemplateText.Fill(Pick(t.NamePatterns), TitleWords(values, t)));
            string summary = TemplateText.FixArticles(TemplateText.Capitalize(TemplateText.Fill(t.Summary, values)));
            return new KeyValuePair<string, string>(name, summary);
        }

        string SlotValue(TemplateSlot s, CultureDef c)
        {
            switch (s.Kind)
            {
                case SlotKind.Place: return Place(c);
                case SlotKind.Region: return Region(c);
                case SlotKind.Faction: return MidSentence(FactionName(FactionArchetypeFor(c), c));
                case SlotKind.Figure: return Person(c);
                case SlotKind.Artifact: return WithArticle(RelicOf(c).Name);
                case SlotKind.Culture: return c.Adjective;
                case SlotKind.Era: return Pick(_bible.Eras).Name;
                case SlotKind.Legend: return LegendName(Pick(_bible.Legends));
                case SlotKind.Number: return (5 * _rng.Range(1, 19)).ToString(CultureInfo.InvariantCulture);
                default: return s.Words.Count > 0 ? Pick(s.Words) : Pick(c.ItemForms);
            }
        }

        /// <summary>Word slots are title-cased inside names ("The First Harvest Fair").</summary>
        static Dictionary<string, string> TitleWords(Dictionary<string, string> values, EventTemplate t)
        {
            var copy = new Dictionary<string, string>(values);
            foreach (var s in t.Slots)
            {
                if (s.Kind == SlotKind.Word) copy[s.Name] = Title(values[s.Name]);
            }
            return copy;
        }

        public static string FirstWord(string name)
        {
            int i = name.IndexOf(' ');
            return i < 0 ? name : name.Substring(0, i);
        }

        /// <summary>Title Case per word and per hyphen part: "shepherd's crook" → "Shepherd's Crook".</summary>
        public static string Title(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var chars = s.ToCharArray();
            bool start = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (start && char.IsLetter(chars[i])) chars[i] = char.ToUpperInvariant(chars[i]);
                start = chars[i] == ' ' || chars[i] == '-';
            }
            return new string(chars);
        }
    }
}
