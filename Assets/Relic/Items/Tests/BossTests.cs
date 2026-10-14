using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Items.Bosses;

namespace Relic.Items.Tests
{
    public class BossTests
    {
        [Test]
        public void EveryGeneratedBossPassesReadability_AcrossThousandsOfSeeds()
        {
            int bosses = 0, airborne = 0, remixes = 0, phases = 0;
            float smallestGap = float.MaxValue, shortestWind = float.MaxValue;
            var failures = new List<string>();
            for (ulong world = 1; world <= 3; world++)
            {
                var ctx = StubWorld.Create(world);
                for (int i = 0; i < 1000; i++)
                {
                    var boss = BossGenerator.Generate(world, i, i % 11, ctx);
                    var rep = ReadabilityChecker.Check(boss);
                    if (!rep.Passed) failures.Add($"{boss.Name} ({boss.Archetype} t{boss.Tier}): {rep}");
                    bosses++;
                    airborne += rep.AirborneFlags.Count;
                    phases += boss.Phases.Count;
                    remixes += boss.Phases.Sum(p => p.Patterns.Count(x => x.Remix != null));
                    smallestGap = Math.Min(smallestGap, rep.SmallestGap);
                    shortestWind = Math.Min(shortestWind, rep.ShortestWindUp);
                }
            }
            TestContext.WriteLine($"{bosses} bosses, {phases} phases, {remixes} remixes, {airborne} airborne patterns flagged; smallest gap {smallestGap:0.00} m, shortest wind-up {shortestWind:0.00} s");
            Assert.IsEmpty(failures, string.Join("\n", failures.Take(10)));
            Assert.GreaterOrEqual(shortestWind, Readability.MinWindUp);
        }

        [Test]
        public void BossesHaveTwoToFourPhasesOfTwoToFourPatterns()
        {
            var ctx = StubWorld.Create(8);
            for (int i = 0; i < 300; i++)
            {
                var b = BossGenerator.Generate(8, i, i % 11, ctx);
                Assert.That(b.Phases.Count, Is.InRange(2, 4));
                foreach (var p in b.Phases) Assert.That(p.Patterns.Count, Is.InRange(2, 4));
                for (int k = 1; k < b.Phases.Count; k++) Assert.IsTrue(b.Phases[k].Patterns.Any(x => x.Remix != null), "later phases remix");
            }
        }

        [Test]
        public void SameSeedGivesSameBoss()
        {
            var a = BossGenerator.Generate(42, 3, 6, StubWorld.Create(42));
            var b = BossGenerator.Generate(42, 3, 6, StubWorld.Create(42));
            Assert.AreEqual(ItemJson.Serialize(a), ItemJson.Serialize(b));
            var c = BossGenerator.Generate(42, 4, 6, StubWorld.Create(42));
            Assert.AreNotEqual(ItemJson.Serialize(a), ItemJson.Serialize(c));
        }

        [Test]
        public void BossJsonRoundTrips()
        {
            var a = BossGenerator.Generate(5, 1, 9, StubWorld.Create(5));
            string json = ItemJson.Serialize(a);
            var back = ItemJson.Deserialize<BossDefinition>(json);
            Assert.AreEqual(json, ItemJson.Serialize(back));
        }

        [Test]
        public void ArchetypeDecidesEssence()
        {
            var w = StubWorld.Create(1);
            Assert.AreEqual(Relic.Core.Stats.StatId.INT, BossGenerator.Generate(1, 0, 4, w, "Librarian").Essence.Stat);
            Assert.AreEqual(Relic.Core.Stats.StatId.STR, BossGenerator.Generate(1, 0, 4, w, "Warlord").Essence.Stat);
            Assert.AreEqual(Relic.Core.Stats.StatId.INS, BossGenerator.Generate(1, 0, 4, w, "Oracle").Essence.Stat);
            Assert.AreEqual(Relic.Core.Stats.StatId.VIT, BossGenerator.Generate(1, 0, 4, w, "Beastmother").Essence.Stat);
            Assert.AreEqual(Relic.Core.Stats.StatId.AGI, BossGenerator.Generate(1, 0, 4, w, "Duelist").Essence.Stat);
            Assert.AreEqual(Relic.Core.Stats.StatId.CHR, BossGenerator.Generate(1, 0, 4, w, "Courtier").Essence.Stat);
        }

        [Test]
        public void HealthMatchesFightTimeTargets()
        {
            // Far boss: ~5 min with top gear (6× starter), ~30 min with starter gear.
            Assert.AreEqual(5f, BossHealth.SoloMinutes(10, 6f), 0.01f);
            Assert.AreEqual(30f, BossHealth.SoloMinutes(10, 1f), 0.01f);
            // Every tier: top gear for that tier (its power band) takes the tier's target minutes.
            for (int t = 0; t <= 10; t++)
                Assert.AreEqual(BossHealth.TopGearMinutes(t), BossHealth.SoloMinutes(t, BossHealth.PowerBand(t)), 0.01f);
            // Group scaling.
            float hp = BossHealth.BaseHealth(5);
            Assert.AreEqual(hp, BossHealth.ForPlayers(hp, 1), 0.01f);
            Assert.AreEqual(hp * 1.8f, BossHealth.ForPlayers(hp, 2), 0.01f);
            Assert.AreEqual(hp * 4.2f, BossHealth.ForPlayers(hp, 5), 0.01f);
        }

        // ---------- the checker must catch violations ----------

        static BossDefinition Wrap(BossPattern p, int tier)
        {
            PatternLibrary.Finish(p);
            var b = new BossDefinition { Tier = tier };
            for (int i = 0; i < 2; i++)
            {
                var ph = new BossPhase { StartsAtHealth = i == 0 ? 1f : 0.5f, Patterns = { p, ItemJson.Clone(p) } };
                ph.Patterns[1].Name = p.Name + " again";
                ph.Rotation.Add(new RotationStep { Pattern = 0, Recover = 1f });
                ph.Rotation.Add(new RotationStep { Pattern = 1, Recover = 1f });
                b.Phases.Add(ph);
            }
            return b;
        }

        static BossPattern Single(params ActionSpec[] actions)
        {
            var rule = new EffectRule { Name = "test", Trigger = new TriggerSpec { Kind = TriggerKind.Active }, Cooldown = 2f };
            rule.Actions.AddRange(actions);
            return new BossPattern { Name = "test", Blocks = { PatternBlock.Ring }, Program = new EffectProgram { Rules = { rule } } };
        }

        static ActionSpec Ring(int count, float delay, float size = 0.4f, params ActionSpec[] extra)
        {
            var payload = new List<ActionSpec> { new ActionSpec { Kind = ActionKind.Damage, Amount = 10f } };
            payload.AddRange(extra);
            return new ActionSpec { Kind = ActionKind.Emit, Emitter = new EmitterSpec { Shape = EmitShape.Ring, Count = count, Speed = 6f, Range = 18f, Size = size, Delay = delay }, Payload = payload };
        }

        static bool Mentions(ReadabilityReport r, string text) => r.Errors.Any(e => e.Contains(text));

        [Test]
        public void CheckerRejectsMissingWindUp()
        {
            var r = ReadabilityChecker.Check(Wrap(Single(Ring(8, 0.2f)), 2));
            Assert.IsTrue(Mentions(r, "wind-up"), r.ToString());
        }

        [Test]
        public void CheckerRejectsClosedRingsAtLowTier()
        {
            var r = ReadabilityChecker.Check(Wrap(Single(Ring(24, 1f)), 2));
            Assert.IsTrue(Mentions(r, "gaps"), r.ToString());
            // two 12-rings released together are one 24-ring
            var r2 = ReadabilityChecker.Check(Wrap(Single(Ring(12, 1f), Ring(12, 1.1f)), 2));
            Assert.IsTrue(Mentions(r2, "ring of 24"), r2.ToString());
            // ...but staggered by a second they're fine
            var r3 = ReadabilityChecker.Check(Wrap(Single(Ring(12, 1f), Ring(12, 2f)), 2));
            Assert.IsFalse(Mentions(r3, "gaps"), r3.ToString());
        }

        [Test]
        public void CheckerRejectsDirectDamageAndLockdown()
        {
            var direct = ReadabilityChecker.Check(Wrap(Single(new ActionSpec { Kind = ActionKind.Damage, Amount = 10f }), 2));
            Assert.IsTrue(Mentions(direct, "can't be dodged"), direct.ToString());
            var root = ReadabilityChecker.Check(Wrap(Single(Ring(8, 1f, 0.4f, new ActionSpec { Kind = ActionKind.ApplyStatus, Status = StatusId.Rooted, Duration = 2f })), 2));
            Assert.IsTrue(Mentions(root, "Rooted"), root.ToString());
        }

        [Test]
        public void CheckerRejectsFastSeekersAndInescapableAreas()
        {
            var seek = Ring(1, 1f);
            seek.Emitter.Shape = EmitShape.Single; seek.Emitter.Seeking = true; seek.Emitter.Speed = 12f;
            Assert.IsTrue(Mentions(ReadabilityChecker.Check(Wrap(Single(seek), 2)), "outrun"));

            var area = new ActionSpec
            {
                Kind = ActionKind.CreateArea,
                Area = new AreaSpec { Shape = AreaShape.Circle, Radius = 12f, Duration = 2f, Delay = 0.4f },
                Payload = new List<ActionSpec> { new ActionSpec { Kind = ActionKind.Damage, Target = TargetKind.EnemiesInArea, Amount = 10f } },
            };
            Assert.IsTrue(Mentions(ReadabilityChecker.Check(Wrap(Single(area), 2)), "escaped"));
        }

        [Test]
        public void CheckerRequiresAirborneFlagOnLobbedShots()
        {
            var lob = Ring(4, 1f);
            lob.Emitter.Shape = EmitShape.Burst; lob.Emitter.SpreadDegrees = 30f;
            var p = Single(lob);
            p.Blocks[0] = PatternBlock.Lobbed;
            Assert.IsTrue(Mentions(ReadabilityChecker.Check(Wrap(p, 2)), "airborne"));
        }

        [Test]
        public void CheckerRejectsRemixThatOnlyChangesNumbers()
        {
            var w = StubWorld.Create(3);
            var boss = BossGenerator.Generate(3, 0, 5, w);
            var remix = boss.Phases[1].Patterns.First(p => p.Remix != null);
            var source = boss.Phases.SelectMany(ph => ph.Patterns).First(p => p.Name == remix.RemixOf);
            remix.Program = ItemJson.Clone(source.Program); // "faster version" with identical structure
            var rep = ReadabilityChecker.Check(boss);
            Assert.IsTrue(Mentions(rep, "only changes numbers"), rep.ToString());
        }
    }
}
