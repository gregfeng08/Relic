using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Items.Text;

namespace Relic.Items.Tests
{
    public class OverrollTests
    {
        [Test]
        public void OverrollStepsFollowExponentialTail()
        {
            var rng = new Pcg32(20261013);
            const int n = 400000;
            var atLeast = new int[12];
            int max = 0;
            for (int i = 0; i < n; i++)
            {
                int k = Overroll.Steps(ref rng);
                max = Math.Max(max, k);
                for (int j = 0; j <= k && j < atLeast.Length; j++) atLeast[j]++;
            }
            for (int m = 1; m <= 4; m++)
            {
                double empirical = atLeast[m] / (double)n;
                double expected = Math.Exp(-m);
                // relative tolerance 5% (n = 400k gives ~1% standard error at m = 4)
                Assert.AreEqual(expected, empirical, expected * 0.05, $"P(k ≥ {m})");
            }
            Assert.LessOrEqual(max, Overroll.HiddenCap);
        }

        [Test]
        public void HiddenCapIsTen()
        {
            Assert.AreEqual(10, Overroll.FromSample(50.0));
            Assert.AreEqual(10, Overroll.FromSample(10.0));
            Assert.AreEqual(9, Overroll.FromSample(9.99));
            Assert.AreEqual(0, Overroll.FromSample(0.3));
        }

        [Test]
        public void StatValuesRespectRangeAndOverrolls()
        {
            for (ulong seed = 1; seed <= 3000; seed++)
            {
                var bt = (BaseType)(seed % 10);
                var rarity = bt == BaseType.Relic ? Rarity.Legendary : (Rarity)(seed % 4);
                foreach (var stat in StatRoller.Roll(bt, rarity, (int)(seed % 11), seed, 0))
                {
                    if (stat.OverrollSteps == 0)
                        Assert.That(stat.Value, Is.InRange(stat.Min, stat.Ceiling), stat.Stat.ToString());
                    else
                    {
                        Assert.Greater(stat.Value, stat.Ceiling);
                        Assert.AreEqual(Overroll.Apply(stat.Ceiling, stat.OverrollSteps), stat.Value, 1.01f); // rounded to display precision, and at least one unit past the ceiling
                    }
                }
            }
        }

        [Test]
        public void RerollIsAFreshSampleWithSameLines()
        {
            var gen = new ItemGenerator(StubWorld.Create(7));
            var item = gen.Generate(new ItemSpec { WorldSeed = 7, Index = 3, Tier = 4, Rarity = Rarity.Epic, BaseType = BaseType.Bow });
            var r1 = gen.Reroll(item);
            var r2 = gen.Reroll(r1);
            Assert.AreEqual(1, r1.RerollCount);
            Assert.AreEqual(2, r2.RerollCount);
            Assert.AreEqual(item.Stats.Count, r1.Stats.Count);
            for (int i = 0; i < item.Stats.Count; i++) Assert.AreEqual(item.Stats[i].Stat, r1.Stats[i].Stat);
            Assert.AreNotEqual(ItemJson.Serialize(item.Stats), ItemJson.Serialize(r1.Stats));
            Assert.AreEqual(item.Name, r1.Name);
            // The same reroll count always gives the same sample.
            Assert.AreEqual(ItemJson.Serialize(r1.Stats), ItemJson.Serialize(StatRoller.Roll(item.BaseType, item.Rarity, item.Tier, item.Seed, 1)));
        }
    }

    public class ItemGenerationTests
    {
        static readonly Rarity[] AllRarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        [Test]
        public void EveryGeneratedProgramFitsItsBudget_AcrossThousandsOfSeeds()
        {
            int items = 0, scaled = 0;
            float fillSum = 0f; int fillN = 0;
            for (ulong world = 1; world <= 4; world++)
            {
                var gen = new ItemGenerator(StubWorld.Create(world));
                for (int i = 0; i < 600; i++)
                {
                    var rarity = AllRarities[i % AllRarities.Length];
                    int tier = (i / 5) % 11;
                    var item = gen.Generate(new ItemSpec { WorldSeed = world, Index = i, Tier = tier, Rarity = rarity });
                    float budget = PowerBudget.For(item.Rarity, item.Tier);
                    var v = EffectValidator.Validate(item.Effects, budget);
                    if (item.Effects.Rules.Count > 0) Assert.IsTrue(v.IsValid, $"{item.Name} ({item.Rarity} t{item.Tier}): {v}");
                    Assert.LessOrEqual(item.Effects.Rules.Count, EffectValidator.MaxRules);
                    if (item.Generator.ScaleApplied < 1f) scaled++;
                    if (item.Rarity >= Rarity.Epic) { fillSum += v.Cost / v.Budget; fillN++; }
                    items++;
                }
            }
            TestContext.WriteLine($"{items} items valid; {scaled} needed ScaleToFit; mean Epic+ budget use {fillSum / fillN:P0}");
            Assert.Greater(fillSum / fillN, 0.6f, "templates should use most of the budget");
        }

        [Test]
        public void SameSeedGivesSameItems()
        {
            string Run()
            {
                var gen = new ItemGenerator(StubWorld.Create(99));
                var all = new List<Item>();
                for (int i = 0; i < 40; i++)
                    all.Add(gen.Generate(new ItemSpec { WorldSeed = 99, Index = i, Tier = i % 11, Rarity = AllRarities[i % 5] }));
                return ItemJson.Serialize(all);
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void LegendaryRelicsHaveLoreEnchantmentsUltimateAndVisual()
        {
            var world = StubWorld.Create(5);
            var gen = new ItemGenerator(world);
            for (int i = 0; i < 200; i++)
            {
                var item = gen.Generate(new ItemSpec { WorldSeed = 5, Index = i, Tier = i % 11, Rarity = Rarity.Legendary });
                Assert.AreEqual(BaseType.Relic, item.BaseType);
                Assert.AreEqual(ItemSlot.Relic, item.Slot);
                Assert.AreEqual(GenerationMode.Template, item.Generator.Mode);
                Assert.IsNotNull(item.Lore?.History);
                Assert.That(StyleGuide.SentenceCount(item.Lore.History), Is.InRange(2, 4), item.Lore.History);
                Assert.IsEmpty(StyleGuide.ForbiddenIn(item.Lore.History));
                Assert.IsNotEmpty(item.Lore.WorldRefs);
                foreach (var id in item.Lore.WorldRefs) Assert.IsTrue(world.Contains(id), id);
                var ult = item.Effects.Rules[item.Effects.Rules.Count - 1];
                Assert.AreEqual(TriggerKind.Active, ult.Trigger.Kind);
                Assert.AreEqual(Relic.Core.Stats.ResourceId.Resonance, ult.CostResource);
                int enchantments = item.Effects.Rules.Count - 1;
                Assert.That(enchantments, Is.InRange(1, 3));
                Assert.IsNotEmpty(item.Visual.KitParts);
                Assert.IsNotNull(item.Memory);
            }
        }

        [Test]
        public void ItemJsonRoundTrips()
        {
            var gen = new ItemGenerator(StubWorld.Create(3));
            var item = gen.Generate(new ItemSpec { WorldSeed = 3, Index = 1, Tier = 6, Rarity = Rarity.Legendary });
            string json = ItemJson.Serialize(item);
            var back = ItemJson.Deserialize<Item>(json);
            Assert.AreEqual(json, ItemJson.Serialize(back));
            Assert.AreEqual(item.Seed, back.Seed);
        }

        [Test]
        public void StoreReturnsPersistedItemInsteadOfRegenerating()
        {
            var store = new InMemoryItemStore();
            var fake = new FakeGenerator((req, call) => ItemJson.Serialize(TemplateAuthor.Compose((RelicBrief)req.Context)));
            var gen = new ItemGenerator(StubWorld.Create(11), fake, store);
            var spec = new ItemSpec { WorldSeed = 11, Index = 0, Tier = 2, Rarity = Rarity.Legendary };
            var a = gen.Generate(spec);
            int calls = fake.Calls;
            var b = gen.Generate(spec);
            Assert.AreEqual(calls, fake.Calls, "persisted item must not be regenerated");
            Assert.AreEqual(ItemJson.Serialize(a), ItemJson.Serialize(b));
        }

        [Test]
        public void ThemePickerFavorsRegionMotifs()
        {
            int tide = 0;
            for (ulong s = 0; s < 4000; s++)
                if (ThemePicker.Pick(s, new[] { "coast" }).Motif.Id == "tide") tide++;
            // tide is one of 3 coast motifs (weight 3) among 12: 3 / (3*3 + 9) = 1/6
            Assert.AreEqual(4000 / 6.0, tide, 120);
        }

        [Test]
        public void NameFreshnessBansWordsAboveFivePercent()
        {
            var f = new NameFreshness(200);
            for (int i = 0; i < 40; i++) f.Add("Name" + (char)('a' + i % 26) + i + " Stone");
            Assert.IsTrue(f.IsOverused("stone"));
            Assert.IsFalse(f.IsOverused("lantern"));
            var g = new NameFreshness(200);
            for (int i = 0; i < 40; i++) g.Add("Unique" + new string((char)('a' + i % 26), 1 + i / 26));
            g.Add("Lantern of Brackmere");
            Assert.IsFalse(g.IsOverused("lantern")); // 1 use in 41 names: a second use is 2/42 < 5%
            g.Add("Lantern of Sedgefen");
            Assert.IsTrue(g.IsOverused("lantern"));  // a third use would be 3/43 > 5%
        }

        [Test]
        public void TemplateNamesStayFreshAcrossFiftyRelics()
        {
            var gen = new ItemGenerator(StubWorld.Create(2026));
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 50; i++)
            {
                var item = gen.Generate(new ItemSpec { WorldSeed = 2026, Index = i, Tier = i % 11, Rarity = Rarity.Legendary });
                foreach (var w in StyleGuide.Words(item.Name)) counts[w] = (counts.TryGetValue(w, out int c) ? c : 0) + 1;
            }
            foreach (var kv in counts) Assert.LessOrEqual(kv.Value, 3, $"\"{kv.Key}\" used in {kv.Value} of 50 names");
        }

        [Test]
        public void StyleGuideCatchesStockWords()
        {
            CollectionAssert.Contains(StyleGuide.ForbiddenIn("The Ethereal Blade of Doom"), "ethereal");
            CollectionAssert.Contains(StyleGuide.ForbiddenIn("The Ethereal Blade of Doom"), "doom");
            Assert.IsEmpty(StyleGuide.ForbiddenIn("Doorstep Lantern")); // "doom" must match whole words only
            Assert.AreEqual(3, StyleGuide.SentenceCount("One. Two! Three?"));
        }
    }
}
