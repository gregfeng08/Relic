using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>Finds the repo (the folder with Assets/) and loads the real Bible once per run.</summary>
    public static class LoreTestData
    {
        static LoreBible _bible;

        public static string RepoRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, "Assets", "Relic", "Lore"))) return d.FullName;
                    d = d.Parent;
                }
            }
            throw new DirectoryNotFoundException("Could not find the repo root (a folder containing Assets/Relic/Lore)");
        }

        public static string DataDir() => Path.Combine(RepoRoot(), "Assets", "Relic", "Lore", "Data");

        public static LoreBible Bible => _bible ?? (_bible = LoreBible.LoadDirectory(DataDir()));

        /// <summary>Raw documents, for tests that corrupt one file and expect the loader to refuse it.</summary>
        public sealed class RawDocs
        {
            public List<KeyValuePair<string, string>> Cultures = new List<KeyValuePair<string, string>>();
            public string Factions, Events, Motifs, Moods, Eras, Legends, Style;

            public LoreBible Load() => LoreBible.FromJson(Cultures, Factions, Events, Motifs, Moods, Eras, Legends, Style);
        }

        public static RawDocs Raw()
        {
            string dir = DataDir();
            var raw = new RawDocs();
            var files = Directory.GetFiles(Path.Combine(dir, LoreBible.CulturesFolder), "*.json");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (var f in files) raw.Cultures.Add(new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f)));
            raw.Factions = File.ReadAllText(Path.Combine(dir, LoreBible.FactionsFile));
            raw.Events = File.ReadAllText(Path.Combine(dir, LoreBible.EventsFile));
            raw.Motifs = File.ReadAllText(Path.Combine(dir, LoreBible.MotifsFile));
            raw.Moods = File.ReadAllText(Path.Combine(dir, LoreBible.MoodsFile));
            raw.Eras = File.ReadAllText(Path.Combine(dir, LoreBible.ErasFile));
            raw.Legends = File.ReadAllText(Path.Combine(dir, LoreBible.LegendsFile));
            raw.Style = File.ReadAllText(Path.Combine(dir, LoreBible.StyleFile));
            return raw;
        }
    }
}
