using System;
using System.Collections.Generic;

namespace Relic.Core.Effects
{
    public sealed class ValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public float Cost;
        public float Budget;
        public bool IsValid => Errors.Count == 0 && Cost <= Budget + 1e-4f;
        public bool OverBudget => Cost > Budget + 1e-4f;

        public override string ToString() =>
            $"cost {Cost:0.00}/{Budget:0.00} pp" + (Errors.Count > 0 ? "; " + string.Join("; ", Errors) : "");
    }

    /// <summary>
    /// Checks an EffectProgram from any source (an LLM, a drawn spell, a boss
    /// generator) before it reaches the game. Structural errors mean the program
    /// is rejected. Over-budget programs can be shrunk with ScaleToFit.
    /// </summary>
    public static class EffectValidator
    {
        // Structural limits (lead-owned; see 05-effect-grammar.md).
        public const int MaxRules = 4;
        public const int MaxActionsPerList = 4;
        public const int MaxPayloadDepth = 3;
        public const int MaxProjectiles = 24;
        public const float MinActiveCooldown = 0.2f;
        public const float MaxStatusDuration = 15f;
        public const float MaxAreaDuration = 30f;
        public const float MaxAreaRadius = 12f;
        public const float MaxRange = 40f;
        public const float MinSpeed = 2f, MaxSpeed = 40f;

        public static ValidationResult Validate(EffectProgram program, float budget)
        {
            var r = new ValidationResult { Budget = budget };
            if (program == null) { r.Errors.Add("program is null"); return r; }
            if (program.Rules == null || program.Rules.Count == 0) r.Errors.Add("program has no rules");
            else
            {
                if (program.Rules.Count > MaxRules) r.Errors.Add($"too many rules ({program.Rules.Count} > {MaxRules})");
                for (int i = 0; i < program.Rules.Count; i++) CheckRule(program.Rules[i], $"rules[{i}]", r.Errors);
            }
            r.Cost = PowerBudget.Cost(program);
            return r;
        }

        static void CheckRule(EffectRule rule, string path, List<string> errors)
        {
            if (rule == null) { errors.Add($"{path} is null"); return; }
            if (rule.Trigger == null) errors.Add($"{path}.trigger is missing");
            if (!Finite(rule.Chance) || rule.Chance <= 0f || rule.Chance > 1f) errors.Add($"{path}.chance must be in (0, 1]");
            if (!Finite(rule.Cooldown) || rule.Cooldown < 0f) errors.Add($"{path}.cooldown must be ≥ 0");
            if (!Finite(rule.CostAmount) || rule.CostAmount < 0f) errors.Add($"{path}.costAmount must be ≥ 0");

            var kind = rule.Trigger?.Kind ?? TriggerKind.Active;
            if (kind == TriggerKind.Active && rule.Cooldown < MinActiveCooldown)
                errors.Add($"{path}: active rules need cooldown ≥ {MinActiveCooldown}s");
            if (kind == TriggerKind.Periodic && (rule.Trigger.Interval < 0.25f || !Finite(rule.Trigger.Interval)))
                errors.Add($"{path}.trigger.interval must be ≥ 0.25s");
            if (kind == TriggerKind.OnLowHealth && (rule.Trigger.Threshold <= 0f || rule.Trigger.Threshold >= 1f))
                errors.Add($"{path}.trigger.threshold must be in (0, 1)");
            if (kind == TriggerKind.OnStatusApplied && rule.Trigger.Status == StatusId.None)
                errors.Add($"{path}.trigger.status is required");

            if (rule.Actions == null || rule.Actions.Count == 0) errors.Add($"{path} has no actions");
            else
            {
                if (kind == TriggerKind.Passive)
                {
                    foreach (var a in rule.Actions)
                        if (a != null && a.Kind != ActionKind.ModifyStat && a.Kind != ActionKind.CreateArea)
                            errors.Add($"{path}: passive rules may only ModifyStat or CreateArea (aura), found {a.Kind}");
                }
                CheckActions(rule.Actions, path + ".actions", 1, errors);
            }

            if (rule.Conditions != null)
                for (int i = 0; i < rule.Conditions.Count; i++)
                {
                    var c = rule.Conditions[i];
                    string cp = $"{path}.conditions[{i}]";
                    if (c == null) { errors.Add($"{cp} is null"); continue; }
                    if ((c.Kind == ConditionKind.TargetHasStatus || c.Kind == ConditionKind.SelfHasStatus) && c.Status == StatusId.None)
                        errors.Add($"{cp}.status is required");
                    if (c.Kind == ConditionKind.InRegionTag && string.IsNullOrWhiteSpace(c.Tag))
                        errors.Add($"{cp}.tag is required");
                }
        }

        static void CheckActions(List<ActionSpec> actions, string path, int depth, List<string> errors)
        {
            if (depth > MaxPayloadDepth) { errors.Add($"{path}: payload nested deeper than {MaxPayloadDepth}"); return; }
            if (actions.Count > MaxActionsPerList) errors.Add($"{path}: too many actions ({actions.Count} > {MaxActionsPerList})");
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                string p = $"{path}[{i}]";
                if (a == null) { errors.Add($"{p} is null"); continue; }
                if (!Finite(a.Amount) || !Finite(a.Duration) || !Finite(a.Multiplier)) errors.Add($"{p}: non-finite number");

                switch (a.Kind)
                {
                    case ActionKind.Damage:
                    case ActionKind.Heal:
                    case ActionKind.RestoreResource:
                        if (a.Amount <= 0f) errors.Add($"{p}.amount must be > 0");
                        break;
                    case ActionKind.ApplyStatus:
                        if (a.Status == StatusId.None) errors.Add($"{p}.status is required");
                        if (a.Duration <= 0f || a.Duration > MaxStatusDuration) errors.Add($"{p}.duration must be in (0, {MaxStatusDuration}]");
                        if (a.Stacks < 1 || a.Stacks > 5) errors.Add($"{p}.stacks must be 1–5");
                        break;
                    case ActionKind.RemoveStatus:
                        if (a.Status == StatusId.None) errors.Add($"{p}.status is required");
                        break;
                    case ActionKind.Knockback:
                    case ActionKind.Pull:
                        if (a.Amount <= 0f || a.Amount > 15f) errors.Add($"{p}.amount must be in (0, 15] meters");
                        break;
                    case ActionKind.ModifyStat:
                        if (a.Amount == 0f && a.Multiplier == 0f) errors.Add($"{p}: needs amount or multiplier");
                        if (a.Duration < 0f) errors.Add($"{p}.duration must be ≥ 0");
                        break;
                    case ActionKind.Emit:
                        CheckEmitter(a.Emitter, p + ".emitter", errors);
                        if (a.Payload == null || a.Payload.Count == 0) errors.Add($"{p}: emit needs a payload");
                        break;
                    case ActionKind.CreateArea:
                        CheckArea(a.Area, p + ".area", errors);
                        if (a.Payload == null || a.Payload.Count == 0) errors.Add($"{p}: area needs a payload");
                        break;
                    case ActionKind.CreateBarrier:
                        CheckArea(a.Area, p + ".area", errors);
                        if (a.Area != null && a.Area.Shape != AreaShape.Wall) errors.Add($"{p}.area.shape must be Wall");
                        break;
                }

                bool carriesPayload = a.Kind == ActionKind.Emit || a.Kind == ActionKind.CreateArea;
                if (a.Payload != null && a.Payload.Count > 0)
                {
                    if (!carriesPayload) errors.Add($"{p}: only Emit and CreateArea may have a payload");
                    else CheckActions(a.Payload, p + ".payload", depth + 1, errors);
                }
                if ((a.Target == TargetKind.EnemiesInArea || a.Target == TargetKind.AlliesInArea) && depth == 1)
                    errors.Add($"{p}: {a.Target} only makes sense inside a CreateArea payload");
            }
        }

        static void CheckEmitter(EmitterSpec e, string p, List<string> errors)
        {
            if (e == null) { errors.Add($"{p} is required"); return; }
            int total = Math.Max(1, e.Count) * (1 + Math.Max(0, e.SplitCount));
            if (e.Count < 1) errors.Add($"{p}.count must be ≥ 1");
            if (total > MaxProjectiles) errors.Add($"{p}: {total} projectiles exceeds {MaxProjectiles}");
            if (e.Speed < MinSpeed || e.Speed > MaxSpeed) errors.Add($"{p}.speed must be {MinSpeed}–{MaxSpeed} m/s");
            if (e.Range <= 0f || e.Range > MaxRange) errors.Add($"{p}.range must be in (0, {MaxRange}]");
            if (e.Size <= 0f || e.Size > 3f) errors.Add($"{p}.size must be in (0, 3] m");
            if (e.Delay < 0f || e.Delay > 5f) errors.Add($"{p}.delay must be 0–5 s");
            if (e.SpreadDegrees < 0f || e.SpreadDegrees > 360f) errors.Add($"{p}.spreadDegrees must be 0–360");
        }

        static void CheckArea(AreaSpec a, string p, List<string> errors)
        {
            if (a == null) { errors.Add($"{p} is required"); return; }
            if (a.Duration <= 0f || a.Duration > MaxAreaDuration) errors.Add($"{p}.duration must be in (0, {MaxAreaDuration}]");
            if (a.TickInterval < 0f || (a.TickInterval > 0f && a.TickInterval < 0.2f)) errors.Add($"{p}.tickInterval must be 0 or ≥ 0.2");
            if (a.Delay < 0f || a.Delay > 5f) errors.Add($"{p}.delay must be 0–5 s");
            switch (a.Shape)
            {
                case AreaShape.Circle:
                case AreaShape.Cone:
                    if (a.Radius <= 0f || a.Radius > MaxAreaRadius) errors.Add($"{p}.radius must be in (0, {MaxAreaRadius}]");
                    break;
                default:
                    if (a.Length <= 0f || a.Length > 20f) errors.Add($"{p}.length must be in (0, 20]");
                    if (a.Width <= 0f || a.Width > 6f) errors.Add($"{p}.width must be in (0, 6]");
                    break;
            }
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        // ---------- Scaling ----------

        /// <summary>
        /// Shrinks the beneficial magnitudes of a program (damage, heal, status
        /// duration, projectile count, stat bonuses) until it fits the budget.
        /// Drawbacks are left alone. Returns the factor applied (1 = unchanged).
        /// Mutates the program; clone it first if you need the original.
        /// </summary>
        public static float ScaleToFit(EffectProgram program, float budget)
        {
            if (PowerBudget.Cost(program) <= budget) return 1f;
            string original = EffectJson.Serialize(program);
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                var trial = EffectJson.Deserialize(original);
                Scale(trial, mid);
                if (PowerBudget.Cost(trial) <= budget) lo = mid; else hi = mid;
            }
            var restored = EffectJson.Deserialize(original);
            Scale(restored, lo);
            program.Rules = restored.Rules;
            return lo;
        }

        static void Scale(EffectProgram p, float f)
        {
            foreach (var rule in p.Rules) ScaleActions(rule.Actions, f);
        }

        static void ScaleActions(List<ActionSpec> actions, float f)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                if (a == null) continue;
                if (PowerBudget.Cost(a) <= 0f && a.Payload == null) continue; // drawback: keep it
                switch (a.Kind)
                {
                    case ActionKind.Damage:
                    case ActionKind.Heal:
                    case ActionKind.RestoreResource:
                    case ActionKind.Knockback:
                    case ActionKind.Pull:
                        a.Amount *= f; break;
                    case ActionKind.ApplyStatus:
                        a.Duration *= f; break;
                    case ActionKind.ModifyStat:
                        a.Amount *= f; a.Multiplier *= f; break;
                    case ActionKind.Emit:
                        if (a.Emitter != null) a.Emitter.Count = Math.Max(1, (int)Math.Round(a.Emitter.Count * Math.Sqrt(f)));
                        ScaleActions(a.Payload, (float)Math.Sqrt(f));
                        break;
                    case ActionKind.CreateArea:
                        if (a.Area != null) a.Area.Duration = Math.Max(0.2f, a.Area.Duration * (float)Math.Sqrt(f));
                        ScaleActions(a.Payload, (float)Math.Sqrt(f));
                        break;
                    case ActionKind.CreateBarrier:
                        if (a.Area != null) a.Area.Duration = Math.Max(0.5f, a.Area.Duration * f);
                        break;
                }
            }
        }
    }
}
