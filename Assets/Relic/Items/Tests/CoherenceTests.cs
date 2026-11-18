using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Items.Tests
{
    /// <summary>Every generated item passes both validators: the power budget (05) and coherence (17).</summary>
    public class CoherenceTests
    {
        static readonly Rarity[] AllRarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

        [Test]
        public void EveryItemIsCoherentAndInBudget_AcrossThousandsOfSeeds()
        {
            var stats = new CoherenceStats();
            for (ulong world = 1; world <= 5; world++)
            {
                var w = StubWorld.Create(world);
                var gen = new ItemGenerator(w);
                for (int i = 0; i < 600; i++)
                {
                    var item = gen.Generate(new ItemSpec { WorldSeed = world, Index = i, Tier = (i / 5) % 11, Rarity = AllRarities[i % 5] });
                    var v = EffectValidator.Validate(item.Effects, ItemGenerator.Budget(item.BaseType, item.Rarity, item.Tier));
                    if (item.Effects.Rules.Count > 0) Assert.IsTrue(v.IsValid, $"{item.Id}: {v}");
                    stats.Add(item, w);
                }
            }
            TestContext.WriteLine(stats.ToString());
            TestContext.WriteLine("moods: " + string.Join(", ", stats.Moods.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
            TestContext.WriteLine("archetypes: " + string.Join(", ", stats.Archetypes.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
            Assert.IsEmpty(stats.Errors, string.Join("\n", stats.Errors.Take(20)));
            Assert.That(stats.FavoredShare, Is.InRange(0.72, 0.92), "about 80% of enchantments use the archetype's triggers");
            Assert.That(stats.BrightShare, Is.InRange(0.65, 0.75), "about 70% bright");
            Assert.AreEqual(stats.Cursed, stats.CursedSomber, "only somber items carry drawbacks");
        }

        [Test]
        public void ValidatorExplainsBrokenItems()
        {
            var w = StubWorld.Create(3);
            var gen = new ItemGenerator(w);
            var staff = gen.Generate(new ItemSpec { WorldSeed = 3, Index = 1, Tier = 2, Rarity = Rarity.Uncommon, BaseType = BaseType.Staff });
            Assert.IsEmpty(CoherenceValidator.Validate(staff, w));
            staff.Stats.Add(new ItemStat { Stat = ItemStatKind.Strength, Value = 2 });
            staff.Visual.Material = "steel";
            var errs = CoherenceValidator.Validate(staff, w);
            Assert.IsTrue(errs.Any(e => e.Contains("Strength") && e.Contains("staff never carries")), string.Join("\n", errs));
            Assert.IsTrue(errs.Any(e => e.Contains("looks like steel")), string.Join("\n", errs));

            var plate = gen.Generate(new ItemSpec { WorldSeed = 3, Index = 2, Tier = 5, Rarity = Rarity.Rare, BaseType = BaseType.HeavyArmor });
            Assert.IsEmpty(CoherenceValidator.Validate(plate, w));
            plate.Effects.Rules.Add(new EffectRule
            {
                Name = "Light Feet",
                Trigger = new TriggerSpec { Kind = TriggerKind.Passive },
                Actions = { new ActionSpec { Kind = ActionKind.ModifyStat, Stat = StatId.AGI, Amount = 2, Target = TargetKind.Self } },
            });
            Assert.IsTrue(CoherenceValidator.Validate(plate, w).Any(e => e.Contains("raises AGI")));
        }

        [Test]
        public void StavesNeverRollStrengthAndHeavyArmorNeverAgility()
        {
            var gen = new ItemGenerator(StubWorld.Create(8));
            for (int i = 0; i < 400; i++)
            {
                var bt = i % 2 == 0 ? BaseType.Staff : BaseType.HeavyArmor;
                var item = gen.Generate(new ItemSpec { WorldSeed = 8, Index = i, Tier = i % 11, Rarity = AllRarities[1 + i % 4], BaseType = bt });
                var banned = bt == BaseType.Staff ? ItemStatKind.Strength : ItemStatKind.Agility;
                Assert.IsFalse(item.Stats.Any(s => s.Stat == banned), $"{item.Name}: {banned}");
            }
        }
    }
}
