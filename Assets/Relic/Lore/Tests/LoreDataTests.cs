using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.World.Regions;

namespace Relic.Lore.Tests
{
    /// <summary>The constitution's data: complete, consistent, bright, and free of fixed story.</summary>
    public class LoreDataTests
    {
        static LoreBible B => LoreTestData.Bible;

        [Test]
        public void AllDataLoads()
        {
            Assert.That(B.CultureTemplates.Count, Is.GreaterThanOrEqualTo(CultureGenerator.MaxCultures), "culture templates (enough for 8 distinct cultures)");
            Assert.That(B.Phonologies.Count, Is.GreaterThanOrEqualTo(5), "phonology families");
            Assert.That(B.PrehistoryRules.Eras.Count, Is.GreaterThanOrEqualTo(4), "era templates");
            Assert.That(B.Factions.Count, Is.GreaterThanOrEqualTo(10), "faction archetypes");
            Assert.That(B.Motifs.Count, Is.GreaterThanOrEqualTo(7), "motifs");
            Assert.IsEmpty(LoreValidator.Validate(B));
        }

        [Test]
        public void NoDuplicateIds()
        {
            AssertUnique("culture template", B.CultureTemplates.Select(x => x.Id));
            AssertUnique("phonology", B.Phonologies.Select(x => x.Id));
            AssertUnique("era template", B.PrehistoryRules.Eras.Select(x => x.Id));
            AssertUnique("faction", B.Factions.Select(x => x.Id));
            AssertUnique("event", B.Events.Select(x => x.Id));
            AssertUnique("motif", B.Motifs.Select(x => x.Id));
            AssertUnique("mood", B.Moods.Moods.Select(x => x.Id));
            AssertUnique("form tag", B.Forms.Select(x => x.Form + "/" + x.Base));
        }

        static void AssertUnique(string kind, IEnumerable<string> ids)
        {
            var dupes = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, "duplicate " + kind + " ids");
        }

        [Test]
        public void LoreFamiliesMirrorWorldRegionFamilies()
        {
            CollectionAssert.AreEqual(Enum.GetNames(typeof(RegionFamily)), Enum.GetNames(typeof(LoreFamily)));
            foreach (LoreFamily f in Enum.GetValues(typeof(LoreFamily)))
                Assert.AreEqual((int)(RegionFamily)Enum.Parse(typeof(RegionFamily), f.ToString()), (int)f, f.ToString());
        }

        [Test]
        public void EveryRegionFamilyHasAHomeTemplate()
        {
            foreach (LoreFamily f in Enum.GetValues(typeof(LoreFamily)))
                Assert.IsTrue(B.CultureTemplates.Any(t => t.Families[0] == f), "no culture template calls " + f + " home");
        }

        [Test]
        public void EveryStatusAndEventKindIsCovered()
        {
            foreach (StatusId s in Enum.GetValues(typeof(StatusId)))
                if (s != StatusId.None) Assert.IsTrue(B.Motifs.Any(m => m.Statuses.Contains(s)), "no motif applies " + s);
            foreach (EventKind k in Enum.GetValues(typeof(EventKind)))
                Assert.IsTrue(B.Events.Any(e => e.Kind == k), "no event template of kind " + k);
            foreach (FactionKind k in Enum.GetValues(typeof(FactionKind)))
                Assert.IsTrue(B.Factions.Any(f => f.Kind == k), "no faction archetype of kind " + k);
            foreach (var era in B.PrehistoryRules.Eras)
                Assert.IsTrue(B.Events.Any(e => e.Eras.Contains(era.Id)), "no event template fits era " + era.Id);
        }

        [Test]
        public void DataIsMostlyBrightAndDarkEventsAreRare()
        {
            double brightFactions = B.Factions.Where(f => f.Tone == Tone.Bright).Sum(f => f.Weight) / (double)B.Factions.Sum(f => f.Weight);
            double brightEvents = B.Events.Where(e => e.Tone == Tone.Bright).Sum(e => e.Weight) / (double)B.Events.Sum(e => e.Weight);
            double brightEras = B.PrehistoryRules.Eras.Where(e => e.Position == EraPosition.Middle && e.Tone == Tone.Bright).Sum(e => e.Weight)
                / (double)B.PrehistoryRules.Eras.Where(e => e.Position == EraPosition.Middle).Sum(e => e.Weight);
            Assert.That(brightFactions, Is.GreaterThanOrEqualTo(0.7), "faction weight share");
            Assert.That(brightEvents, Is.GreaterThanOrEqualTo(0.7), "event weight share");
            Assert.That(brightEras, Is.GreaterThanOrEqualTo(0.5), "middle-era weight share");
            foreach (var e in B.Events.Where(e => e.Kind == EventKind.War || e.Kind == EventKind.Disaster))
                Assert.AreEqual(1, e.Weight, e.Id + " should be rare");
        }

        [Test]
        public void DataContainsNoBannedWords()
        {
            foreach (var kv in Docs())
            {
                var hits = TemplateText.BannedIn(LoreJson.Serialize(kv.Value), B.Style.BannedWords);
                Assert.IsEmpty(hits, kv.Key + " uses banned words");
            }
        }

        /// <summary>Decision 2026-11-18: the story is wiped. No fixed cultures, eras, legends or places survive in the data.</summary>
        [Test]
        public void DataHoldsNoFixedStory()
        {
            var wiped = new[]
            {
                "Wendle", "Aruvi", "Teskari", "Solann", "Ochrel", "Eskeln", "Lunnet", "Corvane", "Imperative", "Concord",
                "Silent Field", "Iron Sentence", "Lanternfold", "Old Hearth", "Lamplit", "Morning Bell", "Maudry", "Wren", "Corro",
                "Unfinished Map", "the Wide",
            };
            foreach (var kv in Docs())
            {
                string json = LoreJson.Serialize(kv.Value);
                foreach (var w in wiped) StringAssert.DoesNotContain(w, json, kv.Key + " still names " + w);
            }
        }

        static Dictionary<string, object> Docs() => new Dictionary<string, object>
        {
            ["culture templates"] = B.CultureTemplates, ["phonology"] = B.Phonologies, ["forms"] = B.Forms,
            ["prehistory"] = B.PrehistoryRules, ["factions"] = B.Factions, ["events"] = B.Events,
            ["motifs"] = B.Motifs, ["moods"] = B.Moods,
        };
    }
}
