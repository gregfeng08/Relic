using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Relic.Lore.Tests
{
    /// <summary>Procedural cultures (CultureGenerator) and prehistory (WorldPrehistory): shape, coverage, tone and determinism.</summary>
    public class WorldLoreTests
    {
        static LoreBible B => LoreTestData.Bible;

        static IEnumerable<ulong> ManySeeds()
        {
            for (ulong s = 0; s < 150; s++) yield return s * 7919 + 1;
        }

        [Test]
        public void FiveToEightCulturesPerSeed()
        {
            var counts = new HashSet<int>();
            foreach (var seed in ManySeeds())
            {
                int n = B.ForWorld(seed).Cultures.Count;
                Assert.That(n, Is.InRange(CultureGenerator.MinCultures, CultureGenerator.MaxCultures), "seed " + seed);
                counts.Add(n);
            }
            Assert.That(counts.Count, Is.GreaterThanOrEqualTo(3), "the count itself varies by seed");
        }

        [Test]
        public void SameSeedSameWorld_DifferentSeedDifferentWorld()
        {
            foreach (var seed in LoreTestData.Seeds)
            {
                var a = B.ForWorld(seed);
                var b = LoreBible.LoadDirectory(LoreTestData.DataDir()).ForWorld(seed); // a fresh load, not a cache
                Assert.AreEqual(LoreJson.Serialize(a.Cultures), LoreJson.Serialize(b.Cultures), "cultures, seed " + seed);
                Assert.AreEqual(LoreJson.Serialize(a.History), LoreJson.Serialize(b.History), "prehistory, seed " + seed);
            }
            var names1 = string.Join(",", B.ForWorld(1).Cultures.Select(c => c.Adjective));
            var names2 = string.Join(",", B.ForWorld(2).Cultures.Select(c => c.Adjective));
            Assert.AreNotEqual(names1, names2);
        }

        [Test]
        public void MakingAWorldNeverChangesTheConstitution()
        {
            string before = LoreJson.Serialize(B.CultureTemplates) + LoreJson.Serialize(B.Phonologies) + LoreJson.Serialize(B.PrehistoryRules);
            for (ulong s = 0; s < 20; s++) B.ForWorld(s);
            string after = LoreJson.Serialize(B.CultureTemplates) + LoreJson.Serialize(B.Phonologies) + LoreJson.Serialize(B.PrehistoryRules);
            Assert.AreEqual(before, after);
            Assert.IsEmpty(B.Cultures);
            Assert.IsFalse(B.IsWorld);
        }

        [Test]
        public void EveryCultureWorksAMetalAndMakesRelics()
        {
            foreach (var seed in ManySeeds())
                foreach (var c in B.ForWorld(seed).Cultures)
                {
                    var t = B.CultureTemplate(c.Template);
                    Assert.IsTrue(c.Materials.Any(m => t.Metals.Contains(m)), c.Id + " has no metal-like material");
                    Assert.GreaterOrEqual(c.FormTags.Count(f => f.Base == "Relic"), 2, c.Id + " relic forms");
                    foreach (var f in c.ItemForms) Assert.IsTrue(c.FormTags.Any(x => x.Form == f), c.Id + ": untagged " + f);
                }
        }

        [Test]
        public void CulturesAreCompleteDistinctAndCoverEveryFamily()
        {
            foreach (var seed in ManySeeds())
            {
                var w = B.ForWorld(seed);
                Assert.IsEmpty(LoreValidator.Validate(w), "seed " + seed);
                foreach (LoreFamily f in Enum.GetValues(typeof(LoreFamily)))
                    Assert.IsNotEmpty(w.CulturesOf(f), "seed " + seed + ": nobody lives in " + f);
                var adjectives = w.Cultures.Select(c => c.Adjective).ToList();
                Assert.AreEqual(adjectives.Count, adjectives.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "seed " + seed + ": " + string.Join(", ", adjectives));
                Assert.AreEqual(w.Cultures.Count, w.Cultures.Select(c => c.Template).Distinct().Count(), "one culture per template");
                foreach (var c in w.Cultures)
                {
                    var g = new NameGenerator(c, B.Style);
                    Assert.IsTrue(g.IsPlausible(c.Adjective), c.Adjective);
                    Assert.IsTrue(g.IsPlausible(g.Fallback()), c.Id + " fallback " + g.Fallback());
                    Assert.AreEqual(2, c.Mood.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.None).Length, c.Id + " mood: " + c.Mood);
                    Assert.IsEmpty(TemplateText.BannedIn(LoreJson.Serialize(c), B.Style.BannedWords), c.Id);
                    foreach (var text in new[] { c.Name, c.Adjective, c.Identity, c.Mood, c.Pride }.Concat(c.Relations.Select(r => r.Note)))
                        Assert.IsFalse(text.Contains("{") || text.Contains("}"), c.Id + " has an unfilled slot: " + text);
                    foreach (var r in c.Relations) Assert.IsNotNull(w.Culture(r.Culture), c.Id + " relation");
                }
            }
        }

        [Test]
        public void PrehistoryHasErasFoundersAndRelicWorthyEvents()
        {
            int bright = 0, total = 0;
            foreach (var seed in ManySeeds())
            {
                var w = B.ForWorld(seed);
                var h = w.History;
                Assert.That(h.Eras.Count, Is.InRange(3, 4), "eras, seed " + seed);
                Assert.AreEqual(EraPosition.First, B.EraTemplate(h.Eras[0].Template).Position);
                Assert.AreEqual(EraPosition.Last, B.EraTemplate(h.Eras[h.Eras.Count - 1].Template).Position);
                Assert.AreEqual(0, h.Eras[h.Eras.Count - 1].EndYearsAgo, "the last era is the present");
                for (int i = 0; i < h.Eras.Count; i++)
                {
                    Assert.AreEqual(i, h.Eras[i].Order);
                    if (i > 0) Assert.AreEqual(h.Eras[i - 1].EndYearsAgo, h.Eras[i].StartYearsAgo, "eras are contiguous");
                    Assert.IsTrue(h.Events.Any(e => e.Era == h.Eras[i].Id), "seed " + seed + ": era " + h.Eras[i].Id + " has no events");
                }

                Assert.AreEqual(w.Cultures.Count, h.Founders.Count, "one founder per culture");
                foreach (var f in h.Founders)
                {
                    Assert.AreEqual(h.Eras[0].Id, f.Era);
                    Assert.IsNotNull(w.Culture(f.Culture));
                }
                Assert.IsNotEmpty(h.Artifacts, "seed " + seed + ": no relic-worthy artifacts");
                Assert.IsTrue(h.Events.Any(e => e.Leaves.Contains(EventTrace.Artifact)), "seed " + seed + ": no event leaves an artifact");

                foreach (var e in h.Events)
                {
                    var era = w.Era(e.Era);
                    Assert.IsNotNull(era, e.Id);
                    Assert.That(e.YearsAgo, Is.InRange(era.EndYearsAgo, era.StartYearsAgo), e.Id);
                    Assert.IsTrue(B.Event(e.Template).Eras.Contains(era.Template), e.Id + " in the wrong era");
                    if (e.Place != null) Assert.IsTrue(h.Places.Any(p => p.Name == e.Place), e.Place);
                    foreach (var id in e.Legends) Assert.IsNotNull(w.Legend(id), id);
                    if (e.Tone == Tone.Bright) bright++;
                    total++;
                }
                var texts = h.Eras.Select(x => x.Name + " " + x.Summary).Concat(h.Events.Select(x => x.Name + " " + x.Summary))
                    .Concat(w.Legends.Select(x => x.Name + " " + x.Epithet + " " + x.Summary)).ToList();
                foreach (var t in texts) Assert.IsFalse(t.Contains("{") || t.Contains("}"), "unfilled slot, seed " + seed + ": " + t);
                var text = string.Join(" / ", texts);
                Assert.IsEmpty(TemplateText.BannedIn(text, B.Style.BannedWords), "seed " + seed);
                Assert.AreEqual(w.Legends.Count, w.Legends.Select(l => l.Id).Distinct().Count(), "legend ids unique");
                Assert.AreEqual(h.Events.Count, h.Events.Select(e => e.Id).Distinct().Count(), "event ids unique");
            }
            Assert.That(bright / (double)total, Is.GreaterThanOrEqualTo(0.65), "prehistory is mostly bright");
        }
    }
}
