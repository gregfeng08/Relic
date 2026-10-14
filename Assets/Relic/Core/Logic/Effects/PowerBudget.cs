using System;
using System.Collections.Generic;

namespace Relic.Core.Effects
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// Prices an EffectProgram in power points (pp). 1 pp ≈ one baseline hit
    /// (10 damage at tier 0) delivered once per ~4 s. Every generator (relics,
    /// spells, bosses, elites) is checked against a budget in pp, so nothing is
    /// free and a cheap-looking combination can't hide a lot of power.
    ///
    /// The coefficients are a first pass, meant to be tuned in playtests.
    /// Change them here only (lead-owned).
    /// </summary>
    public static class PowerBudget
    {
        // ---------- Budgets ----------
        //
        // Calibration (v2, 2026-10-13). A starter sword is about 5–6 pp of offense. The
        // 03-combat power bands want a full top-tier kit with trained stats to land near
        // 6× that (8× with overrolls). Tier growth is shared with weapon base damage
        // (1 + 0.2·tier = 3× at tier 10). Trained stats and essences add about 1.25×,
        // and bonus enchantments the remaining ~1.6×. Active kit (weapon skill E, relic
        // ultimate Q) gets its own allowance so rarity only buys *bonus* effects.
        // Verified by Relic.Game.Tests.PowerBandTests with BalanceSim.

        static readonly float[] RarityBonus = { 0.5f, 0.9f, 1.4f, 2.0f, 3.0f };

        /// <summary>1 + 0.2·tier, tier clamped to 0–10: the shared growth curve for item power.</summary>
        public static float TierFactor(int tier)
        {
            int t = Math.Max(0, Math.Min(tier, 10));
            return 1f + 0.2f * t;
        }

        /// <summary>
        /// Budget for an item's <b>bonus</b> effects (enchantments, passives, curses
        /// and their pay-offs) at a rarity and region tier (0 = starting area).
        /// </summary>
        public static float For(Rarity rarity, int tier) => RarityBonus[(int)rarity] * TierFactor(tier);

        /// <summary>Allowance for a weapon's skill (E) or a spell page: about a 20-damage hit every 5 s at tier 0.</summary>
        public static float ActiveSkill(int tier) => 1.6f * TierFactor(tier);

        /// <summary>Allowance for a relic's ultimate (Q). With a 30 s cooldown this is about a 150-damage burst at tier 0.</summary>
        public static float Ultimate(int tier) => 2.0f * TierFactor(tier);

        /// <summary>
        /// Allowance for one drawn spell circle. A tome's pages are its main attack, so a
        /// spell is weapon-scale (a starter sword is about 5–6 pp), and the big end is
        /// held back by mana rather than by this number.
        /// </summary>
        public static float Spell(int tier) => 14f * TierFactor(tier);

        // ---------- Costs ----------

        public static float Cost(EffectProgram program)
        {
            if (program?.Rules == null) return 0f;
            float total = 0f;
            foreach (var rule in program.Rules) total += Cost(rule);
            return total;
        }

        public static float Cost(EffectRule rule)
        {
            if (rule == null) return 0f;
            float actions = SumActions(rule.Actions);
            float freq = Frequency(rule.Trigger, rule.Cooldown);
            float chance = Clamp(rule.Chance, 0f, 1f);
            float conditions = ConditionFactor(rule.Conditions);
            // Resource costs (mana, resonance) don't discount power: how often a rule can
            // fire is already priced through its cooldown. Mana is a separate, in-game limit.
            return actions * freq * chance * conditions;
        }

        /// <summary>How often a trigger fires, relative to an Active ability on a 4 s cooldown (= 1).</summary>
        public static float Frequency(TriggerSpec trigger, float cooldown)
        {
            float baseFreq;
            switch (trigger?.Kind ?? TriggerKind.Active)
            {
                case TriggerKind.Active: baseFreq = 1f; break;
                case TriggerKind.Passive: baseFreq = 1f; break;
                case TriggerKind.OnHit: baseFreq = 6f; break; // ~1.5 basic attacks/s × 4 s (measured with BalanceSim)
                case TriggerKind.OnKill: baseFreq = 0.5f; break;
                case TriggerKind.OnTakeDamage: baseFreq = 1.2f; break;
                case TriggerKind.OnDash: baseFreq = 1f; break;
                case TriggerKind.OnCloseCall: baseFreq = 0.8f; break;
                case TriggerKind.OnLowHealth: baseFreq = 0.3f; break;
                case TriggerKind.Periodic: baseFreq = 4f / Math.Max(0.25f, trigger.Interval); break;
                case TriggerKind.OnStatusApplied: baseFreq = 1f; break;
                default: baseFreq = 1f; break;
            }

            if (trigger != null && trigger.Kind == TriggerKind.Passive) return baseFreq;

            // A cooldown caps the rate: Active scales with it, reactive triggers are capped by it.
            if (cooldown > 0f)
            {
                float capped = 4f / Math.Max(0.2f, cooldown);
                baseFreq = trigger != null && trigger.Kind == TriggerKind.Active ? capped : Math.Min(baseFreq, capped);
            }
            return Clamp(baseFreq, 0.05f, 10f);
        }

        static float ConditionFactor(List<ConditionSpec> conditions)
        {
            if (conditions == null || conditions.Count == 0) return 1f;
            float f = 1f;
            foreach (var c in conditions)
            {
                switch (c.Kind)
                {
                    case ConditionKind.TargetIsBoss: f *= 0.5f; break;
                    case ConditionKind.SelfHealthBelow:
                    case ConditionKind.TargetHealthBelow: f *= 0.4f + 0.6f * Clamp(c.Value, 0f, 1f); break;
                    case ConditionKind.InRegionTag: f *= 0.5f; break;
                    default: f *= 0.7f; break;
                }
            }
            return Math.Max(0.2f, f);
        }

        public static float SumActions(List<ActionSpec> actions)
        {
            if (actions == null) return 0f;
            float sum = 0f;
            foreach (var a in actions) sum += Cost(a);
            return sum;
        }

        public static float Cost(ActionSpec a)
        {
            if (a == null) return 0f;
            bool onSelf = a.Target == TargetKind.Self || a.Target == TargetKind.AlliesInArea;
            float c;
            switch (a.Kind)
            {
                case ActionKind.Damage:
                    c = a.Amount / 10f;
                    if (a.Target == TargetKind.EnemiesInArea) c *= 1.6f;
                    // Hurting yourself is a drawback that gives budget back.
                    return onSelf ? -0.5f * c : c;

                case ActionKind.ApplyStatus:
                    c = StatusWeight(a.Status) * (Math.Max(0f, a.Duration) / 3f) * (float)Math.Pow(Math.Max(1, a.Stacks), 0.7);
                    if (a.Target == TargetKind.EnemiesInArea) c *= 1.6f;
                    return onSelf ? -0.5f * c : c;

                case ActionKind.RemoveStatus:
                    return onSelf ? 0.8f : 0.2f;

                case ActionKind.Heal:
                    c = 1.2f * a.Amount / 10f;
                    return onSelf ? c : 0f; // healing enemies is never worth anything

                case ActionKind.RestoreResource:
                    return onSelf ? a.Amount / 20f : 0f;

                case ActionKind.Knockback:
                case ActionKind.Pull:
                    return 0.3f * Math.Abs(a.Amount) * (a.Target == TargetKind.EnemiesInArea ? 1.6f : 1f);

                case ActionKind.ModifyStat:
                {
                    float magnitude = a.Multiplier != 0f ? a.Multiplier * 15f : a.Amount * 0.5f;
                    float time = a.Duration > 0f ? Math.Min(1f, a.Duration / 10f) : 1.5f;
                    c = magnitude * time;
                    // Lowering your own stat is a drawback; lowering an enemy's is a debuff worth paying for.
                    return onSelf ? c : -c;
                }

                case ActionKind.Emit:
                {
                    var e = a.Emitter;
                    if (e == null) return 0f;
                    float payload = Math.Max(0f, SumActions(a.Payload));
                    float count = (float)Math.Pow(Math.Max(1, e.Count), 0.8);
                    float reach = (float)Math.Pow(Math.Max(1f, e.Range) / 15f, 0.3);
                    float mods = (e.Seeking ? 1.4f : 1f) * (e.Returning ? 1.2f : 1f) * (1f + 0.4f * Math.Max(0, e.SplitCount));
                    return payload * count * reach * mods;
                }

                case ActionKind.CreateArea:
                {
                    var ar = a.Area;
                    if (ar == null) return 0f;
                    float payload = Math.Max(0f, SumActions(a.Payload));
                    float ticks = ar.TickInterval > 0f ? (float)Math.Pow(Math.Max(1f, ar.Duration / ar.TickInterval), 0.8) : 1f;
                    float size = (float)Math.Pow(AreaSize(ar) / 28f, 0.6); // 28 m² ≈ a 3 m circle
                    return payload * ticks * Math.Max(0.3f, size);
                }

                case ActionKind.CreateBarrier:
                {
                    var ar = a.Area;
                    if (ar == null) return 0f;
                    return 0.05f * Math.Max(1f, ar.Length) * Math.Max(0.5f, ar.Duration);
                }
            }
            return 0f;
        }

        public static float StatusWeight(StatusId s)
        {
            switch (s)
            {
                case StatusId.Soaked: return 0.5f;
                case StatusId.Bleeding:
                case StatusId.Burning:
                case StatusId.Poisoned: return 1.0f;
                case StatusId.Chilled:
                case StatusId.Exhausted:
                case StatusId.Dazed: return 1.2f;
                case StatusId.Staggered:
                case StatusId.Silenced: return 1.5f;
                case StatusId.Rooted: return 2.0f;
                default: return 0f;
            }
        }

        static float AreaSize(AreaSpec a)
        {
            switch (a.Shape)
            {
                case AreaShape.Circle: return (float)(Math.PI * a.Radius * a.Radius);
                case AreaShape.Cone: return (float)(Math.PI * a.Radius * a.Radius * (a.Angle / 360f));
                default: return Math.Max(0f, a.Length) * Math.Max(0.1f, a.Width);
            }
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
