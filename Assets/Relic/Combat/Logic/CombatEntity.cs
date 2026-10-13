using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Combat
{
    /// <summary>A player, enemy or boss as the combat rules see it. No position: geometry lives in ICombatSpace.</summary>
    public sealed class CombatEntity
    {
        public int Id;
        public string Name;
        /// <summary>Entities on the same team are allies.</summary>
        public int Team;
        public bool IsBoss;

        public float MaxHp = 100f, Hp = 100f;
        public float MaxMana = 100f, Mana = 100f;
        public float MaxStamina = 100f, Stamina = 100f;
        public float Resonance;

        /// <summary>Trained stats, essences and gear flat values, before modifiers.</summary>
        public readonly Dictionary<StatId, float> BaseStats = new Dictionary<StatId, float>();
        public readonly List<StatModifier> Modifiers = new List<StatModifier>();
        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();
        public readonly List<EquippedProgram> Programs = new List<EquippedProgram>();

        public bool Alive => Hp > 0f;
        public float HealthFraction => MaxHp <= 0f ? 0f : Hp / MaxHp;

        public float Stat(StatId id)
        {
            BaseStats.TryGetValue(id, out float flat);
            float mult = 0f;
            foreach (var m in Modifiers)
            {
                if (m.Stat != id) continue;
                flat += m.Flat;
                mult += m.Multiplier;
            }
            return flat * (1f + mult);
        }

        public StatusInstance GetStatus(StatusId id)
        {
            foreach (var s in Statuses) if (s.Id == id) return s;
            return null;
        }

        public bool Has(StatusId id) => GetStatus(id) != null;

        public bool IsEnemyOf(CombatEntity other) => other != null && other.Team != Team;
    }

    public sealed class StatModifier
    {
        public StatId Stat;
        public float Flat;
        public float Multiplier;
        /// <summary>Seconds left; negative = permanent while its source stays equipped.</summary>
        public float Remaining = -1f;
        public object Source;
    }

    public sealed class StatusInstance
    {
        public StatusId Id;
        public float Remaining;
        public int Stacks = 1;
        public int SourceId;
        /// <summary>Accumulates fractional damage-over-time between ticks.</summary>
        internal float DotCarry;
    }

    /// <summary>An EffectProgram attached to an entity (weapon, relic, armor, spell page, enemy kit).</summary>
    public sealed class EquippedProgram
    {
        public readonly EffectProgram Program;
        public readonly string Source;
        internal readonly float[] Cooldowns;
        internal readonly float[] PeriodicTimers;
        internal readonly bool[] LowHealthArmed;

        public EquippedProgram(EffectProgram program, string source)
        {
            Program = program;
            Source = source;
            int n = program.Rules.Count;
            Cooldowns = new float[n];
            PeriodicTimers = new float[n];
            LowHealthArmed = new bool[n];
            for (int i = 0; i < n; i++) LowHealthArmed[i] = true;
        }

        public float CooldownRemaining(int rule) => Cooldowns[rule];
    }
}
