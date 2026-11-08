using System;
using System.IO;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>
    /// Writes docs/lore/samples.md on demand:
    ///   RELIC_LORE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteLoreSamples
    /// The normal run checks the same output without writing it.
    /// </summary>
    public class LoreSampleTool
    {
        const ulong DefaultSeed = 2026;

        [Test, Explicit("writes docs/lore/samples.md; run on demand")]
        public void WriteLoreSamples()
        {
            ulong seed = DefaultSeed;
            var env = Environment.GetEnvironmentVariable("RELIC_LORE_SEED");
            if (!string.IsNullOrEmpty(env)) seed = ulong.Parse(env);
            string path = Path.Combine(LoreTestData.RepoRoot(), "docs", "lore", "samples.md");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, LoreSamples.Markdown(LoreTestData.Bible, seed).Replace("\r\n", "\n"));
            TestContext.WriteLine("wrote " + path);
        }

        /// <summary>Prints example names per culture (seeds 1-6 people, 1-4 places), as listed in docs/lore/cultures.md.</summary>
        [Test, Explicit("prints example names; run on demand")]
        public void PrintExampleNames()
        {
            foreach (var c in LoreTestData.Bible.Cultures)
            {
                var g = new NameGenerator(c, LoreTestData.Bible.Style);
                var names = new System.Collections.Generic.List<string>();
                for (ulong s = 1; s <= 6; s++) names.Add(g.Person(s));
                for (ulong s = 1; s <= 4; s++) names.Add(g.Place(s));
                TestContext.Progress.WriteLine(c.Id + ": " + string.Join(", ", names));
            }
        }

        [Test]
        public void SamplesAreCleanAndDeterministic()
        {
            var b = LoreTestData.Bible;
            for (ulong seed = 1; seed <= 25; seed++)
            {
                string md = LoreSamples.Markdown(b, seed);
                Assert.AreEqual(md, LoreSamples.Markdown(b, seed), "seed " + seed);
                StringAssert.Contains(LoreSamples.TemplateBanner, md);
                Assert.IsFalse(md.Contains("{") || md.Contains("}"), "unfilled slot, seed " + seed);
                Assert.IsEmpty(TemplateText.BannedIn(md, b.Style.BannedWords), "seed " + seed);
                Assert.IsFalse(md.Contains("the the ") || md.Contains("The the "), "doubled article, seed " + seed);
            }
        }

        [Test]
        public void EveryEventAndMoodLineFills()
        {
            var b = LoreTestData.Bible;
            var s = new LoreSampler(b, 5);
            foreach (var c in b.Cultures)
            {
                foreach (var t in b.Events)
                {
                    var ev = s.EventOf(t, c);
                    Assert.IsFalse(ev.Key.Contains("{") || ev.Value.Contains("{"), t.Id);
                }
                foreach (var m in b.Moods.Moods)
                {
                    var r = s.RelicOf(c, m);
                    Assert.IsFalse(r.Name.Contains("{") || r.Line.Contains("{"), m.Id);
                }
            }
        }
    }
}
