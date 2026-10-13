using System;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Combat
{
    /// <summary>
    /// The numeric rules of combat, in one place (lead-owned, tune in playtests).
    /// See docs/design/03-combat.md.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>Fraction of a hit that always gets through DEF.</summary>
        public const float DefenseFloor = 0.2f;

        /// <summary>Per point of STR (physical) or INT (magical): +2% damage.</summary>
        public const float DamagePerStatPoint = 0.02f;

        /// <summary>Per point of VIT: status durations shrink by 1/(1 + 0.02·VIT).</summary>
        public const float StatusResistPerVit = 0.02f;

        /// <summary>Healing received while Poisoned.</summary>
        public const float PoisonHealingFactor = 0.5f;

        /// <summary>Timed ModifyStat with duration 0 outside a Passive rule lasts this long.</summary>
        public const float DefaultBuffDuration = 5f;

        public static float Scale(float amount, DamageKind kind, CombatEntity source)
        {
            if (source == null) return amount;
            float stat = kind == DamageKind.Magical ? source.Stat(StatId.INT) : source.Stat(StatId.STR);
            return amount * (1f + DamagePerStatPoint * Math.Max(0f, stat));
        }

        /// <summary>DEF subtracts per hit, but at least DefenseFloor of the hit always lands.</summary>
        public static float Mitigate(float raw, float defense) =>
            Math.Max(raw - Math.Max(0f, defense), raw * DefenseFloor);

        public static float StatusDuration(float baseDuration, CombatEntity target) =>
            baseDuration / (1f + StatusResistPerVit * Math.Max(0f, target.Stat(StatId.VIT)));

        /// <summary>Damage per second per stack for damage-over-time statuses. DoT ignores DEF (it isn't a hit).</summary>
        public static float DotPerSecond(StatusId s)
        {
            switch (s)
            {
                case StatusId.Burning: return 3f;
                case StatusId.Bleeding: return 2f;
                case StatusId.Poisoned: return 2f;
                default: return 0f;
            }
        }

        public static bool CanMove(CombatEntity e) => !e.Has(StatusId.Rooted);
        public static bool CanDashOrSprint(CombatEntity e) => !e.Has(StatusId.Exhausted);
        public static bool CanUseMana(CombatEntity e) => !e.Has(StatusId.Silenced);
        public static float MoveSpeedFactor(CombatEntity e) => e.Has(StatusId.Chilled) ? 0.7f : 1f;
        public static float StaminaRegenFactor(CombatEntity e) => e.Has(StatusId.Soaked) ? 0.6f : 1f;
    }
}
