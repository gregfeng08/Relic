using System;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Combat
{
    /// <summary>
    /// Headless time-to-kill estimates, used to check generated content against the
    /// power bands in 03-combat (a boss takes about 5 min with top gear and about 30 with starter gear).
    /// </summary>
    public static class BalanceSim
    {
        public sealed class Result
        {
            public float Seconds;
            public bool Killed;
            public float DamageDealt;
            public float Dps => Seconds > 0f ? DamageDealt / Seconds : 0f;
        }

        /// <summary>
        /// One attacker repeatedly uses every Active rule of its programs on a passive
        /// target until it dies or maxSeconds pass. The target doesn't fight back (that's
        /// what player skill is for); this measures raw offensive throughput.
        /// </summary>
        public static Result TimeToKill(
            Func<CombatSim, CombatEntity> makeAttacker,
            float targetHp,
            float targetDef,
            ulong seed,
            float maxSeconds = 7200f,
            float dt = 0.05f,
            float hitChance = 0.7f)
        {
            var space = new AbstractSpace(seed) { HitChance = hitChance };
            var sim = new CombatSim(space, seed);
            space.Bind(sim.Entities);
            var attacker = makeAttacker(sim);
            if (!sim.Entities.Contains(attacker)) sim.Add(attacker);
            var target = sim.Add(new CombatEntity { Id = 9999, Name = "Target", Team = 2, IsBoss = true, MaxHp = targetHp, Hp = targetHp });
            target.BaseStats[StatId.DEF] = targetDef;

            float dealt = 0f;
            sim.DamageDealt += (src, tgt, amount, killed) => { if (tgt == target) dealt += amount; };

            while (sim.Time < maxSeconds && target.Alive)
            {
                foreach (var eq in attacker.Programs)
                    for (int i = 0; i < eq.Program.Rules.Count; i++)
                        if (eq.Program.Rules[i].Trigger.Kind == TriggerKind.Active && eq.CooldownRemaining(i) <= 0f)
                            sim.Use(attacker, eq, i, target);
                // Simple regen so mana-costed skills keep flowing (refined once the stats spec lands).
                attacker.Mana = Math.Min(attacker.MaxMana, attacker.Mana + 5f * dt);
                sim.Tick(dt);
            }
            return new Result { Seconds = sim.Time, Killed = !target.Alive, DamageDealt = dealt };
        }
    }
}
