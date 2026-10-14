using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Relic.Combat;
using Relic.Core.Effects;
using Relic.Core.Stats;
using Relic.Items;

namespace Relic.Game.Tests
{
    /// <summary>
    /// The 03-combat power bands, checked end to end: items from the real generator,
    /// fought through the real combat rules. A stock character must stay viable, and a
    /// top-tier kit with trained stats should land near 6× starter offense (5/30 minutes).
    /// </summary>
    public class PowerBandTests
    {
        const float BossHp = 20000f;
        const int Seeds = 12;

        static CombatEntity Build(CombatSim sim, IEnumerable<Item> kit, float trained)
        {
            var hero = sim.Add(new CombatEntity { Id = 1, Name = "Hero", Team = 1 });
            hero.BaseStats[StatId.STR] = trained;
            hero.BaseStats[StatId.INT] = trained;
            foreach (var item in kit)
            {
                var weaponDamage = item.Stats.FirstOrDefault(s => s.Stat == ItemStatKind.Damage);
                var speed = item.Stats.FirstOrDefault(s => s.Stat == ItemStatKind.AttackSpeed);
                if (weaponDamage != null && speed != null)
                {
                    // The basic attack comes from weapon stats, not from the effect budget.
                    sim.Equip(hero, new EffectProgram
                    {
                        Rules =
                        {
                            new EffectRule
                            {
                                Name = "Basic attack",
                                Trigger = new TriggerSpec { Kind = TriggerKind.Active },
                                Cooldown = 1f / speed.Value,
                                Actions = { new ActionSpec { Kind = ActionKind.Damage, Amount = weaponDamage.Value } },
                            },
                        },
                    }, "basic");
                }
                foreach (var s in item.Stats)
                {
                    if (s.Stat == ItemStatKind.Strength) hero.BaseStats[StatId.STR] += s.Value;
                    if (s.Stat == ItemStatKind.Intelligence) hero.BaseStats[StatId.INT] += s.Value;
                }
                if (item.Effects != null && item.Effects.Rules.Count > 0) sim.Equip(hero, item.Effects, item.Id);
            }
            return hero;
        }

        static List<Item> StarterKit(ItemGenerator gen, int i) => new List<Item>
        {
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 1000 + i, Tier = 0, Rarity = Rarity.Common, BaseType = BaseType.Sword }),
        };

        static List<Item> TopKit(ItemGenerator gen, int i) => new List<Item>
        {
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 2000 + i, Tier = 10, Rarity = Rarity.Epic, BaseType = BaseType.Sword }),
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 3000 + i, Tier = 10, Rarity = Rarity.Legendary, BaseType = BaseType.Relic }),
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 4000 + i, Tier = 10, Rarity = Rarity.Epic, BaseType = BaseType.Trinket }),
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 5000 + i, Tier = 10, Rarity = Rarity.Epic, BaseType = BaseType.Trinket }),
            gen.Generate(new ItemSpec { WorldSeed = 11, Index = 6000 + i, Tier = 10, Rarity = Rarity.Epic, BaseType = BaseType.HeavyArmor }),
        };

        [Test]
        public void TopKitIsAboutSixTimesStarterOffense()
        {
            var gen = new ItemGenerator(StubWorld.Create(11));
            var starter = new List<float>();
            var top = new List<float>();
            for (int i = 0; i < Seeds; i++)
            {
                var s = StarterKit(gen, i);
                var t = TopKit(gen, i);
                starter.Add(BalanceSim.TimeToKill(sim => Build(sim, s, 0f), BossHp, 0f, (ulong)(100 + i)).Seconds);
                // "Trained stats and essences": about +12 STR/INT on top of gear.
                top.Add(BalanceSim.TimeToKill(sim => Build(sim, t, 12f), BossHp, 0f, (ulong)(100 + i)).Seconds);
            }
            TestContext.WriteLine("starter: " + string.Join(", ", starter.Select(x => x.ToString("0"))));
            TestContext.WriteLine("top:     " + string.Join(", ", top.Select(x => x.ToString("0"))));
            float s0 = starter.Average(), t0 = top.Average();
            float ratio = s0 / t0;
            TestContext.WriteLine($"starter {s0:0}s, top {t0:0}s, ratio {ratio:0.00}× (target ≈ 6×, accept 4.5–8)");
            Assert.That(ratio, Is.InRange(4.5f, 8f));
        }

        [Test, Explicit("diagnostic: per-item and sword+item kill times for one top kit")]
        public void Diagnose()
        {
            var gen = new ItemGenerator(StubWorld.Create(11));
            var t = TopKit(gen, 0);
            foreach (var item in t.Prepend(StarterKit(gen, 0)[0]))
            {
                var r = BalanceSim.TimeToKill(sim => Build(sim, new[] { item }, 12f), BossHp, 0f, 1, maxSeconds: 3000f);
                TestContext.WriteLine($"{item.BaseType} {item.Rarity} t{item.Tier}: {r.Seconds:0}s dps {r.Dps:0.0} | " +
                    string.Join(", ", item.Stats.Select(s => $"{s.Stat}={s.Value:0.##}")) + " | " +
                    string.Join("; ", item.Effects.Rules.Select(x => $"{x.Name}[{x.Trigger.Kind} cd{x.Cooldown} {x.CostResource}{x.CostAmount}]")));
            }
            foreach (var other in t.Skip(1))
            {
                var r = BalanceSim.TimeToKill(sim => Build(sim, new[] { t[0], other }, 12f), BossHp, 0f, 1, maxSeconds: 3000f);
                TestContext.WriteLine($"sword + {other.BaseType}: {r.Seconds:0}s | " + Json(other));
            }
        }

        static string Json(Item i) => EffectJson.Serialize(i.Effects);

        [Test]
        public void StockCharacterCanStillKillAnything()
        {
            // "Nothing is locked": starter gear kills a top-tier boss, just slowly.
            var gen = new ItemGenerator(StubWorld.Create(11));
            var kit = StarterKit(gen, 0);
            var r = BalanceSim.TimeToKill(sim => Build(sim, kit, 0f), BossHp * 3f, 0f, 5);
            Assert.IsTrue(r.Killed);
        }
    }
}
