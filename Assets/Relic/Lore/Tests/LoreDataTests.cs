using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.World.Regions;

namespace Relic.Lore.Tests
{
    public class LoreDataTests
    {
        static LoreBible B => LoreTestData.Bible;

        [Test]
        public void AllDataLoads()
        {
            Assert.That(B.Cultures.Count, Is.InRange(6, 8), "cultures");
            Assert.That(B.Eras.Count, Is.InRange(3, 4), "eras");
            Assert.That(B.Legends.Count, Is.InRange(6, 10), "legends");
            Assert.That(B.Factions.Count, Is.GreaterThanOrEqualTo(10), "faction archetypes");
            Assert.That(B.Motifs.Count, Is.GreaterThanOrEqualTo(7), "motifs");
            Assert.IsEmpty(LoreValidator.Validate(B));
        }

        [Test]
        public void NoDuplicateIds()
        {
            AssertUnique("culture", B.Cultures.Select(x => x.Id));
            AssertUnique("faction", B.Factions.Select(x => x.Id));
            AssertUnique("event", B.Events.Select(x => x.Id));
            AssertUnique("motif", B.Motifs.Select(x => x.Id));
            AssertUnique("mood", B.Moods.Moods.Select(x => x.Id));
            AssertUnique("era", B.Eras.Select(x => x.Id));
            AssertUnique("legend", B.Legends.Select(x => x.Id));
            AssertUnique("culture name", B.Cultures.Select(x => x.Name));
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
        public void EveryCultureReferencesValidRegionFamilies()
        {
            var world = new HashSet<string>(Enum.GetNames(typeof(RegionFamily)));
            foreach (var c in B.Cultures)
            {
                Assert.IsNotEmpty(c.Families, c.Id);
                foreach (var f in c.Families) Assert.IsTrue(world.Contains(f.ToString()), c.Id + ": " + f);
            }
        }

        [Test]
        public void EveryRegionFamilyHasAHomeCulture()
        {
            foreach (RegionFamily f in Enum.GetValues(typeof(RegionFamily)))
            {
                var lf = (LoreFamily)Enum.Parse(typeof(LoreFamily), f.ToString());
                Assert.IsTrue(B.Cultures.Any(c => c.Families[0] == lf), "no culture calls " + f + " home");
            }
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
        }

        [Test]
        public void DataIsMostlyBrightAndDarkEventsAreRare()
        {
            double brightFactions = B.Factions.Where(f => f.Tone == Tone.Bright).Sum(f => f.Weight) / (double)B.Factions.Sum(f => f.Weight);
            double brightEvents = B.Events.Where(e => e.Tone == Tone.Bright).Sum(e => e.Weight) / (double)B.Events.Sum(e => e.Weight);
            Assert.That(brightFactions, Is.GreaterThanOrEqualTo(0.7), "faction weight share");
            Assert.That(brightEvents, Is.GreaterThanOrEqualTo(0.7), "event weight share");
            foreach (var e in B.Events.Where(e => e.Kind == EventKind.War || e.Kind == EventKind.Disaster))
                Assert.AreEqual(1, e.Weight, e.Id + " should be rare");
        }

        [Test]
        public void DataContainsNoBannedWords()
        {
            var docs = new Dictionary<string, object>
            {
                ["cultures"] = B.Cultures, ["factions"] = B.Factions, ["events"] = B.Events,
                ["motifs"] = B.Motifs, ["moods"] = B.Moods, ["eras"] = B.Eras, ["legends"] = B.Legends,
            };
            foreach (var kv in docs)
            {
                var hits = TemplateText.BannedIn(LoreJson.Serialize(kv.Value), B.Style.BannedWords);
                Assert.IsEmpty(hits, kv.Key + " uses banned words");
            }
        }

        [Test]
        public void LegendsAreReferencedByCanonNames()
        {
            Assert.IsNotNull(B.Legend("old-hearth"));
            Assert.IsNotNull(B.Legend("wren"));
            Assert.IsNotNull(B.Culture("wendle"), "the origin sanctuary's culture");
            Assert.AreEqual(LoreFamily.Meadow, B.Culture("wendle").Families[0], "the Lanternfold sits in Meadow at (0, 0)");
        }
    }
}
