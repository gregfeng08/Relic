using System;
using System.Collections.Generic;
using Relic.Core.Effects;

namespace Relic.Items
{
    /// <summary>
    /// Sizes template effects to a power target. A template is a function from a
    /// magnitude scalar s to a rule; the fitter binary-searches the largest s
    /// whose (tidied, human-friendly) rule costs no more than the target.
    /// ScaleToFit only shrinks, so this is how templates grow with tier and rarity.
    /// </summary>
    public static class EffectFitter
    {
        public delegate EffectRule RuleBuilder(float s);

        /// <summary>Build, tidy and fit. Returns the rule (never over target unless even s = min is over).</summary>
        public static EffectRule Fit(RuleBuilder build, float targetPp, Func<EffectRule, float> cost = null, float minS = 0.02f, float maxS = 1000f)
        {
            if (cost == null) cost = PowerBudget.Cost;
            float lo = minS, hi = maxS;
            if (cost(Tidy(build(lo))) > targetPp) return Tidy(build(lo));
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (cost(Tidy(build(mid))) <= targetPp) lo = mid; else hi = mid;
            }
            return Tidy(build(lo));
        }

        /// <summary>Rounds magnitudes to numbers a player can read (whole damage, half-second durations...).</summary>
        public static EffectRule Tidy(EffectRule rule)
        {
            if (rule == null) return null;
            TidyActions(rule.Actions);
            return rule;
        }

        static void TidyActions(List<ActionSpec> actions)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                switch (a.Kind)
                {
                    case ActionKind.Damage:
                    case ActionKind.Heal:
                        a.Amount = a.Amount >= 5f ? (float)Math.Floor(a.Amount) : Math.Max(0.5f, Floor(a.Amount, 0.5f));
                        break;
                    case ActionKind.RestoreResource:
                        a.Amount = Math.Max(1f, (float)Math.Floor(a.Amount));
                        break;
                    case ActionKind.ApplyStatus:
                        a.Duration = Math.Min(EffectValidator.MaxStatusDuration, Math.Max(0.5f, Floor(a.Duration, 0.5f)));
                        break;
                    case ActionKind.Knockback:
                    case ActionKind.Pull:
                        a.Amount = Math.Min(15f, Math.Max(0.5f, Floor(a.Amount, 0.5f)));
                        break;
                    case ActionKind.ModifyStat:
                        if (a.Multiplier != 0f) a.Multiplier = Math.Sign(a.Multiplier) * Math.Max(0.01f, Floor(Math.Abs(a.Multiplier), 0.01f));
                        else if (a.Amount != 0f) a.Amount = Math.Sign(a.Amount) * Math.Max(0.5f, Floor(Math.Abs(a.Amount), 0.5f));
                        break;
                }
                TidyActions(a.Payload);
            }
        }

        static float Floor(float v, float step) => (float)(Math.Floor(v / step + 1e-4) * step);
    }
}
