using System;
using System.Collections.Generic;
using System.Globalization;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Items
{
    /// <summary>
    /// Plain-English descriptions of effect programs for sample review and
    /// tooltips: "On hit (once every 2 s), if the target is Soaked: deal 12
    /// magical damage; pull the target 3 m toward you."
    /// </summary>
    public static class EffectDescriber
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        static string N(float v) => v.ToString("0.##", Ci);

        /// <param name="activeLabel">How to name an Active trigger: "Skill (E)", "Release (Q)", "Attack".</param>
        /// <param name="boss">Describe from a boss's point of view (Self = the boss, targets = players).</param>
        public static string Describe(EffectRule r, string activeLabel = "On use", bool boss = false)
        {
            if (r == null) return "(empty rule)";
            var head = new List<string>();
            string trigger = Trigger(r.Trigger, activeLabel, boss);
            var quals = new List<string>();
            if (r.Chance < 1f) quals.Add((r.Chance * 100f).ToString("0", Ci) + "% chance");
            bool active = r.Trigger?.Kind == TriggerKind.Active;
            if (r.Cooldown > 0f) quals.Add(active ? N(r.Cooldown) + " s cooldown" : "at most once every " + N(r.Cooldown) + " s");
            if (r.CostAmount > 0f)
                quals.Add(r.CostResource == ResourceId.Resonance && r.CostAmount >= 100f ? "uses a full resonance bar" : "costs " + N(r.CostAmount) + " " + r.CostResource.ToString().ToLowerInvariant());
            string text = trigger + (quals.Count > 0 ? " (" + string.Join(", ", quals) + ")" : "");
            if (r.Conditions != null && r.Conditions.Count > 0)
            {
                var conds = new List<string>();
                foreach (var c in r.Conditions) conds.Add(Condition(c));
                text += ", " + string.Join(" and ", conds);
            }
            var acts = new List<string>();
            if (r.Actions != null) foreach (var a in r.Actions) acts.Add(Action(a, boss));
            return text + ": " + string.Join("; ", acts) + ".";
        }

        static string Trigger(TriggerSpec t, string activeLabel, bool boss)
        {
            if (t == null) return "Never";
            string you = boss ? "the boss" : "you";
            switch (t.Kind)
            {
                case TriggerKind.Active: return activeLabel;
                case TriggerKind.Passive: return "While equipped";
                case TriggerKind.OnHit: return "On hit";
                case TriggerKind.OnKill: return "On a kill";
                case TriggerKind.OnTakeDamage: return boss ? "When the boss is hit" : "When you are hit";
                case TriggerKind.OnDash: return "When " + you + " dash" + (boss ? "es" : "");
                case TriggerKind.OnCloseCall: return "On a close call (a shot passes within a hand's width)";
                case TriggerKind.OnLowHealth: return "When " + (boss ? "the boss's" : "your") + " health falls below " + (t.Threshold * 100f).ToString("0", Ci) + "%";
                case TriggerKind.Periodic: return "Every " + N(t.Interval) + " s";
                case TriggerKind.OnStatusApplied: return "Whenever " + you + " inflict" + (boss ? "s " : " ") + t.Status;
            }
            return t.Kind.ToString();
        }

        static string Condition(ConditionSpec c)
        {
            bool not = c.Negate;
            switch (c.Kind)
            {
                case ConditionKind.TargetHasStatus: return "if the target is " + (not ? "not " : "") + c.Status;
                case ConditionKind.SelfHasStatus: return "if you are " + (not ? "not " : "") + c.Status;
                case ConditionKind.SelfHealthBelow: return "if your health is " + (not ? "at least " : "below ") + (c.Value * 100f).ToString("0", Ci) + "%";
                case ConditionKind.TargetHealthBelow: return "if the target is " + (not ? "at or above " : "below ") + (c.Value * 100f).ToString("0", Ci) + "% health";
                case ConditionKind.TargetIsBoss: return not ? "against anything but bosses" : "against bosses";
                case ConditionKind.DistanceToTargetBelow: return "if the target is " + (not ? "farther than " : "within ") + N(c.Value) + " m";
                case ConditionKind.DistanceToTargetAbove: return "if the target is " + (not ? "within " : "more than ") + N(c.Value) + " m away";
                case ConditionKind.IsNight: return not ? "by day" : "at night";
                case ConditionKind.IsRaining: return not ? "when it is dry" : "in the rain";
                case ConditionKind.InRegionTag: return (not ? "outside " : "in ") + c.Tag + " regions";
            }
            return c.Kind.ToString();
        }

        static string Who(TargetKind t, bool boss)
        {
            switch (t)
            {
                case TargetKind.Self: return boss ? "the boss" : "yourself";
                case TargetKind.EnemiesInArea: return boss ? "players inside" : "enemies inside";
                case TargetKind.AlliesInArea: return boss ? "the boss's allies inside" : "allies inside";
                default: return boss ? "the player" : "the target";
            }
        }

        static string Stacks(ActionSpec a) => a.Stacks > 1 ? " (" + a.Stacks + " stacks)" : "";

        static string StatChange(ActionSpec a)
        {
            if (a.Multiplier != 0f) return (a.Multiplier > 0 ? "+" : "−") + (Math.Abs(a.Multiplier) * 100f).ToString("0", Ci) + "% " + a.Stat;
            return (a.Amount > 0 ? "+" : "−") + N(Math.Abs(a.Amount)) + " " + a.Stat;
        }

        /// <summary>Top-level action with a verb.</summary>
        public static string Action(ActionSpec a, bool boss = false)
        {
            if (a == null) return "(nothing)";
            string who = Who(a.Target, boss);
            switch (a.Kind)
            {
                case ActionKind.Damage:
                    if (a.Target == TargetKind.Self) return (boss ? "the boss takes " : "you take ") + N(a.Amount) + " damage";
                    return "deal " + N(a.Amount) + " " + a.DamageKind.ToString().ToLowerInvariant() + " damage" + (a.Target == TargetKind.Target ? "" : " to " + who);
                case ActionKind.ApplyStatus:
                    if (a.Target == TargetKind.Self) return (boss ? "the boss becomes " : "you become ") + a.Status + " for " + N(a.Duration) + " s" + Stacks(a);
                    return "inflict " + a.Status + " for " + N(a.Duration) + " s" + Stacks(a) + (a.Target == TargetKind.Target ? "" : " on " + who);
                case ActionKind.RemoveStatus:
                    return "remove " + a.Status + " from " + who;
                case ActionKind.Heal:
                    return a.Target == TargetKind.Self ? "heal " + N(a.Amount) + " HP" : "heal " + who + " for " + N(a.Amount) + " HP";
                case ActionKind.RestoreResource:
                    return "restore " + N(a.Amount) + " " + a.Resource.ToString().ToLowerInvariant();
                case ActionKind.Knockback:
                    return "knock " + who + " back " + N(a.Amount) + " m";
                case ActionKind.Pull:
                    return "pull " + who + " " + N(a.Amount) + " m toward " + (boss ? "the boss" : "you");
                case ActionKind.ModifyStat:
                {
                    string dur = a.Duration > 0f ? " for " + N(a.Duration) + " s" : (boss ? "" : " while equipped");
                    if (a.Target == TargetKind.Self) return (boss ? "the boss gets " : "you get ") + StatChange(a) + dur;
                    return "give " + who + " " + StatChange(a) + dur;
                }
                case ActionKind.Emit:
                    return Emitter(a.Emitter, boss) + Payload(a.Payload, boss, "each hit: ");
                case ActionKind.CreateArea:
                    return AreaText(a.Area, boss) + Payload(a.Payload, boss, a.Area != null && a.Area.TickInterval > 0f ? "every " + N(a.Area.TickInterval) + " s: " : "once: ");
                case ActionKind.CreateBarrier:
                    return "raise a " + N(a.Area?.Length ?? 0f) + " m wall that blocks projectiles for " + N(a.Area?.Duration ?? 0f) + " s";
            }
            return a.Kind.ToString();
        }

        static string Payload(List<ActionSpec> payload, bool boss, string lead)
        {
            if (payload == null || payload.Count == 0) return "";
            var parts = new List<string>();
            foreach (var p in payload) parts.Add(Compact(p, boss));
            return " — " + lead + string.Join(", ", parts);
        }

        /// <summary>Payload action without a verb: "6 magical damage", "Soaked 2 s".</summary>
        static string Compact(ActionSpec a, bool boss)
        {
            string onSelf = a.Target == TargetKind.Self ? (boss ? " to the boss" : " to you") : "";
            switch (a.Kind)
            {
                case ActionKind.Damage: return N(a.Amount) + " " + a.DamageKind.ToString().ToLowerInvariant() + " damage" + onSelf;
                case ActionKind.ApplyStatus: return a.Status + " " + N(a.Duration) + " s" + Stacks(a) + onSelf;
                case ActionKind.RemoveStatus: return "removes " + a.Status;
                case ActionKind.Heal: return "heal " + N(a.Amount);
                case ActionKind.RestoreResource: return "+" + N(a.Amount) + " " + a.Resource.ToString().ToLowerInvariant();
                case ActionKind.Knockback: return "knockback " + N(a.Amount) + " m";
                case ActionKind.Pull: return "pull " + N(a.Amount) + " m";
                case ActionKind.ModifyStat: return StatChange(a) + (a.Duration > 0 ? " " + N(a.Duration) + " s" : "") + onSelf;
                default: return "(" + Action(a, boss) + ")";
            }
        }

        static string Emitter(EmitterSpec e, bool boss)
        {
            if (e == null) return "fire nothing";
            var mods = new List<string>();
            if (e.Seeking) mods.Add("seeking");
            if (e.Airborne) mods.Add("lobbed");
            string adj = mods.Count > 0 ? string.Join(", ", mods) + " " : "";
            string n = e.Count.ToString(Ci);
            string s;
            switch (e.Shape)
            {
                case EmitShape.Single: s = e.Count <= 1 ? "fire a " + adj + "projectile" : "fire " + n + " " + adj + "projectiles"; break;
                case EmitShape.Line: s = "fire " + n + " " + adj + "projectiles in a line"; break;
                case EmitShape.Cone: s = "fire " + n + " " + adj + "projectiles in a " + N(e.SpreadDegrees) + "° cone"; break;
                case EmitShape.Ring: s = "fire a ring of " + n + " " + adj + "projectiles"; break;
                case EmitShape.Spiral: s = "release a spiral of " + n + " " + adj + "projectiles"; break;
                case EmitShape.Burst: s = "loose a burst of " + n + " " + adj + "projectiles" + (e.SpreadDegrees > 0 ? " across " + N(e.SpreadDegrees) + "°" : ""); break;
                case EmitShape.Orbit: s = "summon " + n + " " + adj + "projectiles that orbit " + (boss ? "the boss" : "you") + " for " + N(e.Range) + " s"; break;
                default: s = "fire " + n + " projectiles"; break;
            }
            var extra = new List<string>();
            if (e.Shape != EmitShape.Orbit) extra.Add(N(e.Speed) + " m/s, " + N(e.Range) + " m");
            if (e.Size != 0.25f) extra.Add(N(e.Size) + " m radius");
            if (e.Returning) extra.Add("they return");
            if (e.SplitCount > 0) extra.Add("each splits into " + e.SplitCount + " on impact");
            if (e.Delay > 0f) extra.Add(N(e.Delay) + " s wind-up");
            return s + " (" + string.Join("; ", extra) + ")";
        }

        static string AreaText(AreaSpec a, bool boss)
        {
            if (a == null) return "create nothing";
            string where = a.FollowsSelf ? (boss ? " around the boss" : " around you") : (boss ? " at a player's feet" : " at the target");
            string shape;
            switch (a.Shape)
            {
                case AreaShape.Circle: shape = "a " + N(a.Radius) + " m circle" + where; break;
                case AreaShape.Cone: shape = "a " + N(a.Angle) + "° cone " + N(a.Radius) + " m long" + (a.FollowsSelf ? (boss ? " from the boss" : " in front of you") : where); break;
                case AreaShape.Line: shape = "a " + N(a.Length) + " × " + N(a.Width) + " m line" + (boss ? " from the boss" : " from you"); break;
                default: shape = "a " + N(a.Length) + " m wall"; break;
            }
            string tell = a.Delay > 0f ? ", after a " + N(a.Delay) + " s tell" : "";
            return "create " + shape + " for " + N(a.Duration) + " s" + tell;
        }
    }
}
