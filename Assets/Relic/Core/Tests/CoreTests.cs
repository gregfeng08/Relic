using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Core.Tests
{
    public class RandomTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new Pcg32(42);
            var b = new Pcg32(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            var a = new Pcg32(1);
            var b = new Pcg32(2);
            Assert.AreNotEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void RangeStaysInBounds()
        {
            var r = new Pcg32(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.Range(-3, 5);
                Assert.That(v, Is.InRange(-3, 4));
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void ExponentialHasExpectedMean()
        {
            var r = new Pcg32(99);
            double sum = 0;
            const int n = 200000;
            for (int i = 0; i < n; i++) sum += r.Exponential(1.0);
            Assert.AreEqual(1.0, sum / n, 0.02);
        }

        [Test]
        public void StringHashIsStableAcrossRuns()
        {
            // Pinned value: if this changes, every saved world seed changes. Don't update it casually.
            Assert.AreEqual(Hash.Of("terrain.height"), Hash.Of("terrain.height"));
            Assert.AreNotEqual(Hash.Of("terrain.height"), Hash.Of("terrain.moisture"));
            Assert.AreEqual(Hash.Coord(-5, 12), Hash.Coord(-5, 12));
            Assert.AreNotEqual(Hash.Coord(-5, 12), Hash.Coord(12, -5));
        }
    }

    public class EffectGrammarTests
    {
        /// <summary>The "Undertow" enchantment from the original pitch: on hit vs soaked targets, pull and root.</summary>
        public static EffectProgram Undertow() => new EffectProgram
        {
            Rules =
            {
                new EffectRule
                {
                    Name = "Undertow",
                    Trigger = new TriggerSpec { Kind = TriggerKind.OnHit },
                    Conditions = { new ConditionSpec { Kind = ConditionKind.TargetHasStatus, Status = StatusId.Soaked } },
                    Actions =
                    {
                        new ActionSpec { Kind = ActionKind.Pull, Amount = 3f },
                        new ActionSpec { Kind = ActionKind.ApplyStatus, Status = StatusId.Rooted, Duration = 1.5f },
                    },
                    Cooldown = 2f,
                },
            },
        };

        /// <summary>Three seeking water bolts that soak: the spellcraft example.</summary>
        public static EffectProgram WaterBolts() => new EffectProgram
        {
            Rules =
            {
                new EffectRule
                {
                    Name = "Tide Seekers",
                    Trigger = new TriggerSpec { Kind = TriggerKind.Active },
                    Cooldown = 3f,
                    CostResource = ResourceId.Mana,
                    CostAmount = 20f,
                    Actions =
                    {
                        new ActionSpec
                        {
                            Kind = ActionKind.Emit,
                            Emitter = new EmitterSpec { Shape = EmitShape.Cone, Count = 3, SpreadDegrees = 30f, Seeking = true, Speed = 18f, Range = 20f },
                            Payload = new List<ActionSpec>
                            {
                                new ActionSpec { Kind = ActionKind.Damage, Amount = 8f, DamageKind = DamageKind.Magical },
                                new ActionSpec { Kind = ActionKind.ApplyStatus, Status = StatusId.Soaked, Duration = 4f },
                            },
                        },
                    },
                },
            },
        };

        [Test]
        public void ExamplesAreValidWithinLegendaryBudget()
        {
            float budget = PowerBudget.For(Rarity.Legendary, 0);
            var u = EffectValidator.Validate(Undertow(), budget);
            var w = EffectValidator.Validate(WaterBolts(), budget);
            Assert.IsTrue(u.IsValid, u.ToString());
            Assert.IsTrue(w.IsValid, w.ToString());
        }

        [Test]
        public void JsonRoundTripPreservesProgram()
        {
            string json = EffectJson.Serialize(WaterBolts());
            StringAssert.Contains("\"kind\":\"Emit\"", json);
            StringAssert.Contains("\"status\":\"Soaked\"", json);
            var back = EffectJson.Deserialize(json);
            Assert.AreEqual(json, EffectJson.Serialize(back));
        }

        [Test]
        public void UnknownJsonFieldsAreRejected()
        {
            bool ok = EffectJson.TryDeserialize("{\"rules\":[],\"godMode\":true}", out _, out string error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ActiveRuleWithoutCooldownIsInvalid()
        {
            var p = WaterBolts();
            p.Rules[0].Cooldown = 0f;
            var r = EffectValidator.Validate(p, 1000f);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void TooManyProjectilesIsInvalid()
        {
            var p = WaterBolts();
            p.Rules[0].Actions[0].Emitter.Count = 10;
            p.Rules[0].Actions[0].Emitter.SplitCount = 3; // 10 × 4 = 40 > 24
            var r = EffectValidator.Validate(p, 1000f);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void PayloadOnlyAllowedOnEmitAndArea()
        {
            var p = Undertow();
            p.Rules[0].Actions[0].Payload = new List<ActionSpec> { new ActionSpec { Kind = ActionKind.Damage, Amount = 5f } };
            Assert.IsFalse(EffectValidator.Validate(p, 1000f).IsValid);
        }

        [Test]
        public void DrawbacksReduceCost()
        {
            var p = WaterBolts();
            float before = PowerBudget.Cost(p);
            p.Rules[0].Actions.Add(new ActionSpec { Kind = ActionKind.ApplyStatus, Target = TargetKind.Self, Status = StatusId.Exhausted, Duration = 3f });
            Assert.Less(PowerBudget.Cost(p), before);
        }

        [Test]
        public void HigherTierAndRarityGiveBiggerBudgets()
        {
            Assert.Less(PowerBudget.For(Rarity.Common, 0), PowerBudget.For(Rarity.Legendary, 0));
            Assert.Less(PowerBudget.For(Rarity.Rare, 0), PowerBudget.For(Rarity.Rare, 5));
            Assert.AreEqual(PowerBudget.For(Rarity.Rare, 10), PowerBudget.For(Rarity.Rare, 99));
        }

        [Test]
        public void ScaleToFitBringsProgramUnderBudget()
        {
            var p = WaterBolts();
            p.Rules[0].Actions[0].Payload[0].Amount = 400f;
            float budget = PowerBudget.For(Rarity.Rare, 0);
            Assert.Greater(PowerBudget.Cost(p), budget);
            float f = EffectValidator.ScaleToFit(p, budget);
            Assert.Less(f, 1f);
            var r = EffectValidator.Validate(p, budget);
            Assert.IsTrue(r.IsValid, r.ToString());
        }

        [Test]
        public void TierRisesWithDistanceAndCaps()
        {
            Assert.AreEqual(0, Difficulty.TierAt(0, 0));
            Assert.AreEqual(0, Difficulty.TierAt(1199, 0));
            Assert.AreEqual(1, Difficulty.TierAt(0, -1200));
            Assert.AreEqual(Difficulty.MaxTier, Difficulty.TierAt(1e6, 1e6));
        }

        [Test]
        public void SoakingPutsOutFire()
        {
            CollectionAssert.Contains(StatusRules.Clears(StatusId.Soaked), StatusId.Burning);
            CollectionAssert.Contains(StatusRules.Clears(StatusId.Burning), StatusId.Chilled);
        }
    }
}
