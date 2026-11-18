using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Items.Text;
using Relic.Lore;

namespace Relic.Items.Tests
{
    /// <summary>The item grammar (17): data coverage, the sentence, and template drafts that pass the validators.</summary>
    public class GrammarTests
    {
        static readonly Rarity[] TextRarities = { Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        [Test]
        public void TemplateDraftsPassTheDraftValidatorFirstTime()
        {
            var gen = new ItemGenerator(StubWorld.Create(41));
            var failures = new List<string>();
            for (int i = 0; i < 300; i++)
            {
                var item = gen.Generate(new ItemSpec { WorldSeed = 41, Index = i, Tier = i % 11, Rarity = TextRarities[i % 3] });
                var rec = gen.Records.Last();
                if (!rec.FirstTryValid)
                    failures.Add($"{item.Id} {item.Rarity} {item.Grammar}: " + string.Join(" | ", rec.ErrorsPerAttempt[0]));
            }
            Assert.IsEmpty(failures, string.Join("\n", failures.Take(15)));
        }

        [Test]
        public void PromptCarriesTheSentenceAndTheStyleGuide()
        {
            var fake = new FakeGenerator((req, call) => ItemJson.Serialize(TemplateAuthor.Compose((RelicBrief)req.Context)));
            var item = new ItemGenerator(StubWorld.Create(77), fake).Generate(new ItemSpec { WorldSeed = 77, Index = 0, Tier = 3, Rarity = Rarity.Legendary });
            var req = fake.Requests[0];
            StringAssert.Contains("well-traveled aunt", req.Messages[0].Content); // docs/lore/style-guide.md
            StringAssert.Contains("ITEM SENTENCE", req.Messages[1].Content);
            StringAssert.Contains(item.Grammar.Maker, req.Messages[1].Content);
            StringAssert.Contains(item.Grammar.Material, req.Messages[1].Content);
            StringAssert.Contains("legend.wren", req.JsonSchema); // Bible names are referenceable
        }

        [Test]
        public void WriterMustKeepTheGrammarsNamesAndFacts()
        {
            var fake = new FakeGenerator((req, call) =>
            {
                var o = Newtonsoft.Json.Linq.JObject.Parse(ItemJson.Serialize(TemplateAuthor.Compose((RelicBrief)req.Context)));
                if (call == 0)
                {
                    o["name"] = "The Gleaming Thing";
                    o["lore"] = "Forged by Grimbold of Zarathax, who sold it to a wandering merchant.";
                    o["visual"]["material"] = "obsidian";
                    o["worldRefs"] = new Newtonsoft.Json.Linq.JArray(((RelicBrief)req.Context).World.Regions[0].Id);
                }
                return o.ToString();
            });
            var item = new ItemGenerator(StubWorld.Create(77), fake).Generate(new ItemSpec { WorldSeed = 77, Index = 5, Tier = 6, Rarity = Rarity.Legendary });
            string feedback = fake.Requests[1].Messages.Last().Content;
            StringAssert.Contains("form noun", feedback);
            StringAssert.Contains("\"Grimbold\"", feedback);
            StringAssert.Contains("\"Zarathax\"", feedback);
            StringAssert.Contains("visual.material", feedback);
            StringAssert.Contains("legend or an era", feedback);
            Assert.AreEqual(GenerationMode.Llm, item.Generator.Mode, "the corrected second answer is accepted");
        }

        [Test]
        public void EveryBibleMaterialAndFormIsDescribed()
        {
            var bible = LoreSource.Default;
            var missing = new List<string>();
            foreach (var c in bible.Cultures)
                foreach (var m in c.Materials)
                    if (Materials.Find(m) == null) missing.Add(c.Id + " material: " + m);
            foreach (var mo in bible.Motifs)
            {
                foreach (var m in mo.Materials)
                    if (Materials.Find(m) == null) missing.Add(mo.Id + " material: " + m);
                if (!MotifTraits.Has(mo.Id)) missing.Add("motif traits: " + mo.Id);
            }
            missing.AddRange(CultureForms.Unmapped(bible));
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void EveryCultureMakesAtLeastTwoRelicForms()
        {
            foreach (var c in LoreSource.Default.Cultures)
                Assert.GreaterOrEqual(CultureForms.For(c, BaseType.Relic).Count, 2, c.Id);
        }

        [Test]
        public void WorldIsBuiltFromTheBible()
        {
            var w = StubWorld.Create(2026);
            var bible = LoreSource.Default;
            Assert.AreEqual(8, w.Regions.Count);
            foreach (var r in w.Regions)
            {
                var c = bible.Culture(r.Culture);
                Assert.IsNotNull(c, r.Name);
                Assert.IsTrue(c.Families.Exists(f => f.ToString() == r.Family), r.Name + " " + r.Family);
            }
            foreach (var f in w.Factions) Assert.IsNotNull(bible.Faction(f.Archetype), f.Name);
            foreach (var e in w.Events) Assert.IsNotNull(bible.Event(e.Template), e.Name);
            foreach (var l in bible.Legends) Assert.AreEqual(l.Name, w.NameOf(WorldContext.LegendId(l)));
            foreach (var text in w.Regions.Select(r => r.Name).Concat(w.Factions.Select(f => f.Name)).Concat(w.Figures.Select(p => p.Name + " " + p.Fate)).Concat(w.Events.Select(e => e.Name)))
                Assert.IsEmpty(TemplateText.BannedIn(text, bible.Style.BannedWords), text);
        }
    }
}
