using System.Collections.Generic;
using NUnit.Framework;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Combat.Tests
{
    public class CombatTests
    {
        AbstractSpace _space;
        CombatSim _sim;
        CombatEntity _hero, _foe;

        [SetUp]
        public void SetUp()
        {
            _space = new AbstractSpace(1) { HitChance = 1f, AreaInsideChance = 1f };
            _sim = new CombatSim(_space, 1);
            _space.Bind(_sim.Entities);
            _hero = _sim.Add(new CombatEntity { Id = 1, Name = "Hero", Team = 1 });
            _foe = _sim.Add(new CombatEntity { Id = 2, Name = "Foe", Team = 2, MaxHp = 1000f, Hp = 1000f });
        }

        static EffectProgram BasicAttack(float damage = 10f, float cooldown = 0.5f) => new EffectProgram
        {
            Rules =
            {
                new EffectRule
                {
                    Name = "Swing",
                    Trigger = new TriggerSpec { Kind = TriggerKind.Active },
                    Cooldown = cooldown,
                    Actions = { new ActionSpec { Kind = ActionKind.Damage, Amount = damage } },
                },
            },
        };

        static EffectProgram Undertow() => new EffectProgram
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

        void Run(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.05f) _sim.Tick(0.05f); }

        [Test]
        public void DefenseHasAFloor()
        {
            _foe.BaseStats[StatId.DEF] = 50f;
            _sim.DealDamage(_hero, _foe, 10f, DamageKind.Physical);
            Assert.AreEqual(998f, _foe.Hp, 1e-3);
        }

        [Test]
        public void StrengthScalesPhysicalDamage()
        {
            _hero.BaseStats[StatId.STR] = 10f;
            _sim.DealDamage(_hero, _foe, 10f, DamageKind.Physical);
            Assert.AreEqual(988f, _foe.Hp, 1e-3);
        }

        [Test]
        public void UndertowPullsAndRootsOnlySoakedTargets()
        {
            var weapon = _sim.Equip(_hero, BasicAttack(), "weapon");
            _sim.Equip(_hero, Undertow(), "relic");

            _sim.Use(_hero, weapon, 0, _foe);
            Assert.IsFalse(_foe.Has(StatusId.Rooted), "dry target: no undertow");

            _sim.ApplyStatus(null, _foe, StatusId.Soaked, 5f);
            Run(0.5f);
            _sim.Use(_hero, weapon, 0, _foe);
            Assert.IsTrue(_foe.Has(StatusId.Rooted));
            Assert.AreEqual(-3f, _space.Displacement[_foe], 1e-3);

            // Within Undertow's 2 s cooldown, a second hit doesn't pull again.
            Run(0.5f);
            _sim.Use(_hero, weapon, 0, _foe);
            Assert.AreEqual(-3f, _space.Displacement[_foe], 1e-3);
        }

        [Test]
        public void TriggeredDamageCannotChainForever()
        {
            var echo = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Name = "Echo",
                        Trigger = new TriggerSpec { Kind = TriggerKind.OnHit },
                        Actions = { new ActionSpec { Kind = ActionKind.Damage, Amount = 1f } },
                    },
                },
            };
            var weapon = _sim.Equip(_hero, BasicAttack(), "weapon");
            _sim.Equip(_hero, echo, "trinket");
            int hits = 0;
            _sim.DamageDealt += (s, t, a, k) => hits++;
            _sim.Use(_hero, weapon, 0, _foe);
            Assert.AreEqual(2, hits);
        }

        [Test]
        public void BurningDealsDamageOverTimeIgnoringDefense()
        {
            _foe.BaseStats[StatId.DEF] = 100f;
            _sim.ApplyStatus(_hero, _foe, StatusId.Burning, 3f);
            Run(3.5f);
            float dealt = 1000f - _foe.Hp;
            Assert.That(dealt, Is.InRange(8f, 9f));
            Assert.IsFalse(_foe.Has(StatusId.Burning));
        }

        [Test]
        public void WaterAndFireCancelPhysically()
        {
            _sim.ApplyStatus(null, _foe, StatusId.Soaked, 5f);
            _sim.ApplyStatus(null, _foe, StatusId.Burning, 5f);
            Assert.IsFalse(_foe.Has(StatusId.Burning), "fire doesn't catch on a soaked target");
            Assert.IsFalse(_foe.Has(StatusId.Soaked), "but it dries them");

            _sim.ApplyStatus(null, _foe, StatusId.Burning, 5f);
            _sim.ApplyStatus(null, _foe, StatusId.Soaked, 5f);
            Assert.IsFalse(_foe.Has(StatusId.Burning), "water puts out fire");
            Assert.IsTrue(_foe.Has(StatusId.Soaked));
        }

        [Test]
        public void VitalityShortensStatuses()
        {
            _foe.BaseStats[StatId.VIT] = 50f;
            _sim.ApplyStatus(null, _foe, StatusId.Chilled, 4f);
            Assert.AreEqual(2f, _foe.GetStatus(StatusId.Chilled).Remaining, 1e-3);
        }

        [Test]
        public void ActiveRulesRespectCooldownManaAndSilence()
        {
            var spell = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Trigger = new TriggerSpec { Kind = TriggerKind.Active },
                        Cooldown = 2f, CostResource = ResourceId.Mana, CostAmount = 30f,
                        Actions = { new ActionSpec { Kind = ActionKind.Damage, Amount = 5f, DamageKind = DamageKind.Magical } },
                    },
                },
            };
            _hero.Mana = 50f;
            var eq = _sim.Equip(_hero, spell, "tome");
            Assert.AreEqual(CombatSim.UseResult.Ok, _sim.Use(_hero, eq, 0, _foe));
            Assert.AreEqual(CombatSim.UseResult.OnCooldown, _sim.Use(_hero, eq, 0, _foe));
            Run(2.05f);
            Assert.AreEqual(CombatSim.UseResult.NotEnoughResource, _sim.Use(_hero, eq, 0, _foe));
            _hero.Mana = 100f;
            _sim.ApplyStatus(null, _hero, StatusId.Silenced, 5f);
            Assert.AreEqual(CombatSim.UseResult.Silenced, _sim.Use(_hero, eq, 0, _foe));
        }

        [Test]
        public void LowHealthTriggerFiresOncePerDip()
        {
            var lastStand = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Trigger = new TriggerSpec { Kind = TriggerKind.OnLowHealth, Threshold = 0.3f },
                        Actions = { new ActionSpec { Kind = ActionKind.Heal, Target = TargetKind.Self, Amount = 20f } },
                    },
                },
            };
            _sim.Equip(_hero, lastStand, "armor");
            _sim.DealDamage(_foe, _hero, 75f, DamageKind.Physical); // 100 → 25 (< 30%) → heal to 45
            Assert.AreEqual(45f, _hero.Hp, 1e-3);
            _sim.DealDamage(_foe, _hero, 10f, DamageKind.Physical); // 45 → 35: above threshold, re-arms
            _sim.DealDamage(_foe, _hero, 10f, DamageKind.Physical); // 35 → 25: dips again → heal
            Assert.AreEqual(45f, _hero.Hp, 1e-3);
        }

        [Test]
        public void SeekingWaterBoltsSoakAndDamage()
        {
            var bolts = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Trigger = new TriggerSpec { Kind = TriggerKind.Active }, Cooldown = 3f,
                        Actions =
                        {
                            new ActionSpec
                            {
                                Kind = ActionKind.Emit,
                                Emitter = new EmitterSpec { Shape = EmitShape.Single, Count = 3, Seeking = true, Speed = 20f, Range = 20f },
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
            var eq = _sim.Equip(_hero, bolts, "tome");
            _sim.Use(_hero, eq, 0, _foe);
            Assert.AreEqual(1000f, _foe.Hp, "projectiles take time to arrive");
            Run(1f);
            Assert.AreEqual(1000f - 24f, _foe.Hp, 1e-3);
            Assert.IsTrue(_foe.Has(StatusId.Soaked));
        }

        [Test]
        public void PassiveStatBonusLastsWhileEquipped()
        {
            var ring = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Trigger = new TriggerSpec { Kind = TriggerKind.Passive },
                        Actions = { new ActionSpec { Kind = ActionKind.ModifyStat, Stat = StatId.STR, Amount = 10f } },
                    },
                },
            };
            var eq = _sim.Equip(_hero, ring, "trinket");
            Run(10f);
            Assert.AreEqual(10f, _hero.Stat(StatId.STR));
            _sim.Unequip(_hero, eq);
            Assert.AreEqual(0f, _hero.Stat(StatId.STR));
        }

        [Test]
        public void AuraDamagesNearbyEnemiesOverTime()
        {
            var aura = new EffectProgram
            {
                Rules =
                {
                    new EffectRule
                    {
                        Trigger = new TriggerSpec { Kind = TriggerKind.Passive },
                        Actions =
                        {
                            new ActionSpec
                            {
                                Kind = ActionKind.CreateArea,
                                Area = new AreaSpec { Shape = AreaShape.Circle, Radius = 3f, FollowsSelf = true, TickInterval = 1f, Duration = 1f },
                                Payload = new List<ActionSpec> { new ActionSpec { Kind = ActionKind.Damage, Target = TargetKind.EnemiesInArea, Amount = 5f } },
                            },
                        },
                    },
                },
            };
            _sim.Equip(_hero, aura, "armor");
            Run(3.01f);
            Assert.That(1000f - _foe.Hp, Is.InRange(15f - 1e-3f, 20f + 1e-3f));
            Assert.AreEqual(100f, _hero.Hp, "auras don't hurt their owner");
        }

        [Test]
        public void BalanceSimIsDeterministicAndKills()
        {
            System.Func<CombatSim, CombatEntity> make = sim =>
            {
                var h = sim.Add(new CombatEntity { Id = 1, Name = "Hero", Team = 1 });
                sim.Equip(h, BasicAttack(), "starter sword");
                return h;
            };
            var a = BalanceSim.TimeToKill(make, 1000f, 0f, seed: 7);
            var b = BalanceSim.TimeToKill(make, 1000f, 0f, seed: 7);
            Assert.IsTrue(a.Killed);
            Assert.AreEqual(a.Seconds, b.Seconds);
            // Starter sword: a direct melee hit (only projectiles roll to miss) of 10 dmg
            // every 0.5 s = 20 dps → 100 swings → ~49.5 s for 1000 HP.
            Assert.That(a.Seconds, Is.InRange(49f, 51f));
        }
    }
}
