using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>Finds the repo (the folder with Assets/) and loads the real constitution once per run.</summary>
    public static class LoreTestData
    {
        static LoreBible _bible;

        /// <summary>World seeds most tests sweep.</summary>
        public static readonly ulong[] Seeds = { 1, 2, 3, 7, 42, 2026, 31337, 900001 };

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

        /// <summary>The constitution: rules and templates, no cultures or prehistory.</summary>
        public static LoreBible Bible => _bible ?? (_bible = LoreBible.LoadDirectory(DataDir()));

        /// <summary>One generated world.</summary>
        public static LoreBible World(ulong seed) => Bible.ForWorld(seed);

        /// <summary>Every culture from the sweep worlds.</summary>
        public static List<CultureDef> SweepCultures()
        {
            var all = new List<CultureDef>();
            foreach (var s in Seeds) all.AddRange(World(s).Cultures);
            return all;
        }

        /// <summary>Raw documents keyed by file name, for tests that corrupt one and expect the loader to refuse it.</summary>
        public static Dictionary<string, string> Raw()
        {
            var docs = new Dictionary<string, string>();
            foreach (var f in LoreBible.Files) docs[f] = File.ReadAllText(Path.Combine(DataDir(), f));
            return docs;
        }
    }
}
