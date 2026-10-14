using System;
using System.IO;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Items.Bosses;
using Relic.Items.Samples;

namespace Relic.Items.Tests
{
    /// <summary>
    /// Sample-generation tool. Explicit, so it only runs when asked for:
    ///   RELIC_SAMPLE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteSamples
    /// Writes docs/samples/relics-&lt;seed&gt;.md (50 relics) and bosses-&lt;seed&gt;.md (10 bosses).
    /// </summary>
    public class SampleTool
    {
        [Test, Explicit("writes docs/samples; run on demand")]
        public void WriteSamples()
        {
            ulong seed = 2026;
            var env = Environment.GetEnvironmentVariable("RELIC_SAMPLE_SEED");
            if (!string.IsNullOrEmpty(env)) seed = ulong.Parse(env);

            string dir = Path.Combine(RepoRoot(), "docs", "samples");
            Directory.CreateDirectory(dir);

            var relics = SampleWriter.GenerateRelics(seed);
            File.WriteAllText(Path.Combine(dir, $"relics-{seed}.md"), Lf(SampleWriter.RelicsMarkdown(relics)));

            var bosses = SampleWriter.GenerateBosses(seed);
            File.WriteAllText(Path.Combine(dir, $"bosses-{seed}.md"), Lf(SampleWriter.BossesMarkdown(seed, bosses, relics.World)));
            TestContext.WriteLine("wrote samples to " + dir);
        }

        static string Lf(string s) => s.Replace("\r\n", "\n");

        /// <summary>The sample set itself is checked in the normal run (no files written).</summary>
        [Test]
        public void SampleSetIsValid()
        {
            var relics = SampleWriter.GenerateRelics(2026);
            Assert.AreEqual(50, relics.Relics.Count);
            foreach (var r in relics.Relics)
                Assert.IsTrue(EffectValidator.Validate(r.Effects, ItemGenerator.Budget(r.BaseType, r.Rarity, r.Tier)).IsValid, r.Name);
            string md = SampleWriter.RelicsMarkdown(relics);
            StringAssert.Contains("TEMPLATE MODE — not LLM output", md);
            StringAssert.DoesNotContain("INVALID", md);

            var bosses = SampleWriter.GenerateBosses(2026);
            Assert.AreEqual(10, bosses.Count);
            foreach (var b in bosses) Assert.IsTrue(ReadabilityChecker.Check(b).Passed, b.Name);
            string bmd = SampleWriter.BossesMarkdown(2026, bosses, relics.World);
            StringAssert.DoesNotContain("FAILED", bmd);
        }

        [Test]
        public void CompactJsonRoundTrips()
        {
            var relics = SampleWriter.GenerateRelics(31, null, 20);
            foreach (var it in relics.Relics)
            {
                string full = EffectJson.Serialize(it.Effects);
                Assert.AreEqual(full, EffectJson.Serialize(EffectJson.Deserialize(CompactJson.Program(it.Effects))));
                foreach (var r in it.Effects.Rules) StringAssert.Contains("\"kind\"", CompactJson.Rule(r));
            }
            foreach (var b in SampleWriter.GenerateBosses(31, 5))
                foreach (var ph in b.Phases)
                    foreach (var p in ph.Patterns)
                        Assert.AreEqual(EffectJson.Serialize(p.Program), EffectJson.Serialize(EffectJson.Deserialize(CompactJson.Program(p.Program))));
        }

        static string RepoRoot()
        {
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
            {
                var d = new DirectoryInfo(start);
                while (d != null)
                {
                    if (Directory.Exists(Path.Combine(d.FullName, "docs", "design")) && Directory.Exists(Path.Combine(d.FullName, "Assets"))) return d.FullName;
                    d = d.Parent;
                }
            }
            throw new DirectoryNotFoundException("repo root (with docs/design and Assets) not found");
        }
    }
}
