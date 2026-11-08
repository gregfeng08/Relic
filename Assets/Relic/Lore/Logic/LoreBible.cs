using System;
using System.Collections.Generic;
using System.IO;

namespace Relic.Lore
{
    /// <summary>
    /// The whole Lore Bible in memory: cultures, factions, event templates, motifs, moods,
    /// eras, legends and style rules. Load it with <see cref="LoadDirectory"/> (tools, tests)
    /// or <see cref="FromJson"/> (Unity TextAssets). Both validate and throw
    /// <see cref="LoreDataException"/> listing every problem found.
    /// </summary>
    public sealed class LoreBible
    {
        public const string CulturesFolder = "Cultures";
        public const string FactionsFile = "factions.json";
        public const string EventsFile = "events.json";
        public const string MotifsFile = "motifs.json";
        public const string MoodsFile = "moods.json";
        public const string ErasFile = "eras.json";
        public const string LegendsFile = "legends.json";
        public const string StyleFile = "style.json";

        public List<CultureDef> Cultures = new List<CultureDef>();
        public List<FactionArchetype> Factions = new List<FactionArchetype>();
        public List<EventTemplate> Events = new List<EventTemplate>();
        public List<MotifDef> Motifs = new List<MotifDef>();
        public MoodTable Moods = new MoodTable();
        public List<EraDef> Eras = new List<EraDef>();
        public List<LegendDef> Legends = new List<LegendDef>();
        public StyleRules Style = new StyleRules();

        public CultureDef Culture(string id) => Cultures.Find(c => c.Id == id);
        public FactionArchetype Faction(string id) => Factions.Find(f => f.Id == id);
        public EventTemplate Event(string id) => Events.Find(e => e.Id == id);
        public MotifDef Motif(string id) => Motifs.Find(m => m.Id == id);
        public MoodDef Mood(string id) => Moods.Moods.Find(m => m.Id == id);
        public EraDef Era(string id) => Eras.Find(e => e.Id == id);
        public LegendDef Legend(string id) => Legends.Find(l => l.Id == id);

        /// <summary>Cultures that list this family, in data order (strongest claim first).</summary>
        public List<CultureDef> CulturesOf(LoreFamily family)
        {
            var primary = new List<CultureDef>();
            var secondary = new List<CultureDef>();
            foreach (var c in Cultures)
            {
                int i = c.Families.IndexOf(family);
                if (i == 0) primary.Add(c);
                else if (i > 0) secondary.Add(c);
            }
            primary.AddRange(secondary);
            return primary;
        }

        /// <summary>
        /// Builds a bible from raw JSON texts. Culture documents are sorted by id, so the order
        /// they are passed in never changes generation.
        /// </summary>
        public static LoreBible FromJson(
            IEnumerable<KeyValuePair<string, string>> cultureDocs,
            string factions, string events, string motifs, string moods,
            string eras, string legends, string style)
        {
            var bible = new LoreBible();
            foreach (var doc in cultureDocs)
                bible.Cultures.Add(LoreJson.Parse<CultureDef>(doc.Value, doc.Key));
            bible.Cultures.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            bible.Factions = LoreJson.Parse<List<FactionArchetype>>(factions, FactionsFile);
            bible.Events = LoreJson.Parse<List<EventTemplate>>(events, EventsFile);
            bible.Motifs = LoreJson.Parse<List<MotifDef>>(motifs, MotifsFile);
            bible.Moods = LoreJson.Parse<MoodTable>(moods, MoodsFile);
            bible.Eras = LoreJson.Parse<List<EraDef>>(eras, ErasFile);
            bible.Legends = LoreJson.Parse<List<LegendDef>>(legends, LegendsFile);
            bible.Style = LoreJson.Parse<StyleRules>(style, StyleFile);

            var errors = LoreValidator.Validate(bible);
            if (errors.Count > 0)
                throw new LoreDataException("Lore Bible has " + errors.Count + " problem(s):\n  " + string.Join("\n  ", errors));
            return bible;
        }

        /// <summary>Loads Assets/Relic/Lore/Data (or any folder with the same layout).</summary>
        public static LoreBible LoadDirectory(string dataDir)
        {
            if (!Directory.Exists(dataDir)) throw new LoreDataException("Lore data folder not found: " + dataDir);
            string culturesDir = Path.Combine(dataDir, CulturesFolder);
            if (!Directory.Exists(culturesDir)) throw new LoreDataException("Lore cultures folder not found: " + culturesDir);

            var files = Directory.GetFiles(culturesDir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            var cultures = new List<KeyValuePair<string, string>>();
            foreach (var f in files)
                cultures.Add(new KeyValuePair<string, string>(CulturesFolder + "/" + Path.GetFileName(f), File.ReadAllText(f)));

            return FromJson(cultures,
                Read(dataDir, FactionsFile), Read(dataDir, EventsFile), Read(dataDir, MotifsFile),
                Read(dataDir, MoodsFile), Read(dataDir, ErasFile), Read(dataDir, LegendsFile), Read(dataDir, StyleFile));
        }

        static string Read(string dir, string file)
        {
            string path = Path.Combine(dir, file);
            if (!File.Exists(path)) throw new LoreDataException("Missing lore file: " + path);
            return File.ReadAllText(path);
        }
    }
}
