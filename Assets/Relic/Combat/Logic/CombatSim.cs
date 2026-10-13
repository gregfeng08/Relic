using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Combat
{
    /// <summary>
    /// Executes EffectPrograms: triggers, conditions, costs, cooldowns, actions,
    /// damage, statuses, resources. Pure and deterministic for a given seed and
    /// space; geometry is delegated to ICombatSpace.
    ///
    /// Recursion rule: triggered rules fire only from "root" events, meaning
    /// player-used abilities and their projectiles or areas. Effects caused by a
    /// triggered rule never fire further triggers, so OnHit → damage → OnHit
    /// chains are impossible by construction.
    /// </summary>
    public sealed class CombatSim
    {
        public readonly List<CombatEntity> Entities = new List<CombatEntity>();
        public readonly ICombatSpace Space;
        public readonly CombatEnvironment Environment = new CombatEnvironment();
        public float Time { get; private set; }

        /// <summary>Optional debug log (damage, statuses, triggers).</summary>
        public Action<string> Log;

        /// <summary>Raised after damage lands: (source, target, amount, killed).</summary>
        public event Action<CombatEntity, CombatEntity, float, bool> DamageDealt;

        Pcg32 _rng;
        int _depth;

        public CombatSim(ICombatSpace space, ulong seed)
        {
            Space = space;
            _rng = Pcg32.ForPurpose(seed, "combat");
        }

        public CombatEntity Add(CombatEntity e)
        {
            Entities.Add(e);
            return e;
        }

        // ---------------- Equipment ----------------

        public EquippedProgram Equip(CombatEntity owner, EffectProgram program, string source)
        {
            var eq = new EquippedProgram(program, source);
            owner.Programs.Add(eq);
            for (int i = 0; i < program.Rules.Count; i++)
            {
                var rule = program.Rules[i];
                if (rule.Trigger.Kind != TriggerKind.Passive) continue;
                foreach (var a in rule.Actions)
                {
                    if (a.Kind == ActionKind.ModifyStat)
                        owner.Modifiers.Add(new StatModifier { Stat = a.Stat, Flat = a.Amount, Multiplier = a.Multiplier, Remaining = -1f, Source = eq });
                    else if (a.Kind == ActionKind.CreateArea && a.Area != null)
                    {
                        var ctx = new Ctx(owner, null, eq, 1);
                        var payload = a.Payload;
                        Space.CreateArea(owner, null, a.Area, true, inside => RunAreaPayload(payload, ctx, inside));
                    }
                }
            }
            return eq;
        }

        public void Unequip(CombatEntity owner, EquippedProgram eq)
        {
            owner.Programs.Remove(eq);
            owner.Modifiers.RemoveAll(m => m.Source == eq);
        }

        // ---------------- Using abilities ----------------

        public enum UseResult { Ok, NotActive, OnCooldown, NotEnoughResource, Silenced, Dead }

        /// <summary>The player (or an enemy AI) uses an Active rule: weapon skill, spell page, relic release.</summary>
        public UseResult Use(CombatEntity user, EquippedProgram eq, int ruleIndex, CombatEntity target)
        {
            if (!user.Alive) return UseResult.Dead;
            var rule = eq.Program.Rules[ruleIndex];
            if (rule.Trigger.Kind != TriggerKind.Active) return UseResult.NotActive;
            if (eq.Cooldowns[ruleIndex] > 0f) return UseResult.OnCooldown;
            if (rule.CostAmount > 0f && rule.CostResource == ResourceId.Mana && !CombatRules.CanUseMana(user)) return UseResult.Silenced;
            if (!TryPay(user, rule)) return UseResult.NotEnoughResource;

            eq.Cooldowns[ruleIndex] = rule.Cooldown;
            if (!ConditionsHold(rule, user, target)) return UseResult.Ok; // spent, but nothing happens (e.g. wrong target)
            RunActions(rule.Actions, new Ctx(user, target, eq, 0));
            return UseResult.Ok;
        }

        bool TryPay(CombatEntity user, EffectRule rule)
        {
            if (rule.CostAmount <= 0f) return true;
            switch (rule.CostResource)
            {
                case ResourceId.Mana: if (user.Mana < rule.CostAmount) return false; user.Mana -= rule.CostAmount; return true;
                case ResourceId.Stamina: if (user.Stamina < rule.CostAmount) return false; user.Stamina -= rule.CostAmount; return true;
                case ResourceId.Resonance: if (user.Resonance < rule.CostAmount) return false; user.Resonance -= rule.CostAmount; return true;
                case ResourceId.HP: if (user.Hp <= rule.CostAmount) return false; user.Hp -= rule.CostAmount; return true;
            }
            return true;
        }

        // ---------------- External events from the game ----------------

        public void NotifyDash(CombatEntity who) => Fire(TriggerKind.OnDash, who, null, StatusId.None, 0);
        public void NotifyCloseCall(CombatEntity who, CombatEntity shooter) => Fire(TriggerKind.OnCloseCall, who, shooter, StatusId.None, 0);

        // ---------------- Time ----------------

        public void Tick(float dt)
        {
            Time += dt;
            foreach (var e in Entities.ToArray())
            {
                if (!e.Alive) continue;
                TickStatuses(e, dt);
                TickModifiers(e, dt);
                foreach (var eq in e.Programs.ToArray())
                {
                    for (int i = 0; i < eq.Program.Rules.Count; i++)
                    {
                        if (eq.Cooldowns[i] > 0f) eq.Cooldowns[i] = Math.Max(0f, eq.Cooldowns[i] - dt);
                        var rule = eq.Program.Rules[i];
                        if (rule.Trigger.Kind == TriggerKind.Periodic)
                        {
                            eq.PeriodicTimers[i] += dt;
                            if (eq.PeriodicTimers[i] >= rule.Trigger.Interval)
                            {
                                eq.PeriodicTimers[i] -= rule.Trigger.Interval;
                                TryFireRule(e, eq, i, null, 0);
                            }
                        }
                    }
                }
            }
            Space.Tick(dt);
        }

        void TickStatuses(CombatEntity e, float dt)
        {
            for (int i = e.Statuses.Count - 1; i >= 0; i--)
            {
                var s = e.Statuses[i];
                float dps = CombatRules.DotPerSecond(s.Id) * s.Stacks;
                if (dps > 0f)
                {
                    s.DotCarry += dps * Math.Min(dt, s.Remaining);
                    if (s.DotCarry >= 1f)
                    {
                        float whole = (float)Math.Floor(s.DotCarry);
                        s.DotCarry -= whole;
                        var src = FindEntity(s.SourceId);
                        ApplyDamageRaw(src, e, whole, viaHit: false, depth: 1);
                        if (!e.Alive) return;
                    }
                }
                s.Remaining -= dt;
                if (s.Remaining <= 0f) e.Statuses.RemoveAt(i);
            }
        }

        static void TickModifiers(CombatEntity e, float dt)
        {
            for (int i = e.Modifiers.Count - 1; i >= 0; i--)
            {
                var m = e.Modifiers[i];
                if (m.Remaining < 0f) continue;
                m.Remaining -= dt;
                if (m.Remaining <= 0f) e.Modifiers.RemoveAt(i);
            }
        }

        // ---------------- Triggers ----------------

        void Fire(TriggerKind kind, CombatEntity owner, CombatEntity other, StatusId status, int depth)
        {
            if (depth > 0 || owner == null || !owner.Alive) return;
            foreach (var eq in owner.Programs.ToArray())
                for (int i = 0; i < eq.Program.Rules.Count; i++)
                {
                    var t = eq.Program.Rules[i].Trigger;
                    if (t.Kind != kind) continue;
                    if (kind == TriggerKind.OnStatusApplied && t.Status != status) continue;
                    TryFireRule(owner, eq, i, other, depth);
                }
        }

        void TryFireRule(CombatEntity owner, EquippedProgram eq, int i, CombatEntity other, int depth)
        {
            var rule = eq.Program.Rules[i];
            if (eq.Cooldowns[i] > 0f) return;
            if (rule.Chance < 1f && !_rng.Chance(rule.Chance)) return;
            if (!ConditionsHold(rule, owner, other)) return;
            if (rule.CostAmount > 0f && rule.CostResource == ResourceId.Mana && !CombatRules.CanUseMana(owner)) return;
            if (!TryPay(owner, rule)) return;
            eq.Cooldowns[i] = rule.Cooldown;
            Log?.Invoke($"{Time:0.00}s {owner.Name} triggers {rule.Name ?? rule.Trigger.Kind.ToString()}");
            RunActions(rule.Actions, new Ctx(owner, other, eq, depth + 1));
        }

        bool ConditionsHold(EffectRule rule, CombatEntity self, CombatEntity target)
        {
            if (rule.Conditions == null) return true;
            foreach (var c in rule.Conditions)
            {
                bool v;
                switch (c.Kind)
                {
                    case ConditionKind.TargetHasStatus: v = target != null && target.Has(c.Status); break;
                    case ConditionKind.SelfHasStatus: v = self.Has(c.Status); break;
                    case ConditionKind.SelfHealthBelow: v = self.HealthFraction < c.Value; break;
                    case ConditionKind.TargetHealthBelow: v = target != null && target.HealthFraction < c.Value; break;
                    case ConditionKind.TargetIsBoss: v = target != null && target.IsBoss; break;
                    case ConditionKind.DistanceToTargetBelow: v = target != null && Space.Distance(self, target) < c.Value; break;
                    case ConditionKind.DistanceToTargetAbove: v = target != null && Space.Distance(self, target) > c.Value; break;
                    case ConditionKind.IsNight: v = Environment.IsNight; break;
                    case ConditionKind.IsRaining: v = Environment.IsRaining; break;
                    case ConditionKind.InRegionTag: v = c.Tag != null && Environment.RegionTags.Contains(c.Tag); break;
                    default: v = true; break;
                }
                if (c.Negate) v = !v;
                if (!v) return false;
            }
            return true;
        }

        // ---------------- Actions ----------------

        struct Ctx
        {
            public readonly CombatEntity Owner;
            public readonly CombatEntity Target;
            public readonly EquippedProgram Source;
            public readonly int Depth;

            public Ctx(CombatEntity owner, CombatEntity target, EquippedProgram source, int depth)
            {
                Owner = owner; Target = target; Source = source; Depth = depth;
            }

            public Ctx WithTarget(CombatEntity t) => new Ctx(Owner, t, Source, Depth);
        }

        void RunActions(List<ActionSpec> actions, Ctx ctx)
        {
            if (actions == null) return;
            foreach (var a in actions)
            {
                if (a.Kind == ActionKind.Emit)
                {
                    if (a.Emitter == null) continue;
                    var payload = a.Payload;
                    Space.Emit(ctx.Owner, ctx.Target, a.Emitter, victim =>
                    {
                        if (victim == null || !victim.Alive || !ctx.Owner.IsEnemyOf(victim)) return;
                        RunActions(payload, ctx.WithTarget(victim));
                    });
                    continue;
                }
                if (a.Kind == ActionKind.CreateArea)
                {
                    if (a.Area == null) continue;
                    var payload = a.Payload;
                    Space.CreateArea(ctx.Owner, ctx.Target, a.Area, false, inside => RunAreaPayload(payload, ctx, inside));
                    continue;
                }
                if (a.Kind == ActionKind.CreateBarrier)
                {
                    if (a.Area != null) Space.CreateBarrier(ctx.Owner, ctx.Target, a.Area);
                    continue;
                }

                var target = Resolve(a.Target, ctx);
                if (target == null) continue;
                Apply(a, ctx, target);
            }
        }

        void RunAreaPayload(List<ActionSpec> payload, Ctx ctx, IReadOnlyList<CombatEntity> inside)
        {
            if (payload == null) return;
            foreach (var e in inside)
            {
                if (e == null || !e.Alive) continue;
                foreach (var a in payload)
                {
                    bool applies;
                    switch (a.Target)
                    {
                        case TargetKind.EnemiesInArea: applies = ctx.Owner.IsEnemyOf(e); break;
                        case TargetKind.AlliesInArea: applies = !ctx.Owner.IsEnemyOf(e); break;
                        case TargetKind.Self: applies = e == ctx.Owner; break;
                        default: applies = ctx.Owner.IsEnemyOf(e); break;
                    }
                    if (!applies) continue;
                    if (a.Kind == ActionKind.Emit || a.Kind == ActionKind.CreateArea || a.Kind == ActionKind.CreateBarrier)
                        RunActions(new List<ActionSpec> { a }, ctx.WithTarget(e));
                    else
                        Apply(a, ctx.WithTarget(e), e);
                }
            }
        }

        static CombatEntity Resolve(TargetKind kind, Ctx ctx) =>
            kind == TargetKind.Self ? ctx.Owner : ctx.Target;

        void Apply(ActionSpec a, Ctx ctx, CombatEntity target)
        {
            switch (a.Kind)
            {
                case ActionKind.Damage:
                    DealDamage(ctx.Owner, target, a.Amount, a.DamageKind, ctx.Depth);
                    break;
                case ActionKind.ApplyStatus:
                    ApplyStatus(ctx.Owner, target, a.Status, a.Duration, Math.Max(1, a.Stacks), ctx.Depth);
                    break;
                case ActionKind.RemoveStatus:
                    target.Statuses.RemoveAll(s => s.Id == a.Status);
                    break;
                case ActionKind.Heal:
                    Heal(target, a.Amount);
                    break;
                case ActionKind.RestoreResource:
                    Restore(target, a.Resource, a.Amount);
                    break;
                case ActionKind.Knockback:
                case ActionKind.Pull:
                    // Rooted things are held in place: they can't be shoved or dragged.
                    if (CombatRules.CanMove(target))
                        Space.Displace(ctx.Owner, target, a.Kind == ActionKind.Pull ? -a.Amount : a.Amount);
                    break;
                case ActionKind.ModifyStat:
                    target.Modifiers.Add(new StatModifier
                    {
                        Stat = a.Stat,
                        Flat = a.Amount,
                        Multiplier = a.Multiplier,
                        Remaining = a.Duration > 0f ? a.Duration : CombatRules.DefaultBuffDuration,
                        Source = ctx.Source,
                    });
                    break;
            }
        }

        // ---------------- Primitives (also used by the game directly) ----------------

        /// <summary>A hit: scaled by the source's STR/INT, reduced by the target's DEF.</summary>
        public void DealDamage(CombatEntity source, CombatEntity target, float amount, DamageKind kind, int depth = 0)
        {
            if (target == null || !target.Alive || amount <= 0f) return;
            float raw = CombatRules.Scale(amount, kind, source);
            float dealt = CombatRules.Mitigate(raw, target.Stat(StatId.DEF));
            ApplyDamageRaw(source, target, dealt, viaHit: true, depth: depth);
        }

        void ApplyDamageRaw(CombatEntity source, CombatEntity target, float dealt, bool viaHit, int depth)
        {
            if (!target.Alive) return;
            float before = target.HealthFraction;
            target.Hp = Math.Max(0f, target.Hp - dealt);
            bool killed = !target.Alive;
            Log?.Invoke($"{Time:0.00}s {source?.Name ?? "?"} → {target.Name}: {dealt:0.0}{(killed ? " (killed)" : "")}");
            DamageDealt?.Invoke(source, target, dealt, killed);

            if (viaHit && source != null && source != target)
            {
                Fire(TriggerKind.OnHit, source, target, StatusId.None, depth);
                Fire(TriggerKind.OnTakeDamage, target, source, StatusId.None, depth);
            }
            if (!killed) CheckLowHealth(target, before, depth);
            else if (source != null && source != target) Fire(TriggerKind.OnKill, source, target, StatusId.None, depth);
        }

        void CheckLowHealth(CombatEntity e, float before, int depth)
        {
            float now = e.HealthFraction;
            foreach (var eq in e.Programs.ToArray())
                for (int i = 0; i < eq.Program.Rules.Count; i++)
                {
                    var t = eq.Program.Rules[i].Trigger;
                    if (t.Kind != TriggerKind.OnLowHealth) continue;
                    if (now >= t.Threshold) { eq.LowHealthArmed[i] = true; continue; }
                    if (before >= t.Threshold && eq.LowHealthArmed[i] && depth == 0)
                    {
                        eq.LowHealthArmed[i] = false;
                        TryFireRule(e, eq, i, null, depth);
                    }
                }
        }

        public void ApplyStatus(CombatEntity source, CombatEntity target, StatusId status, float duration, int stacks = 1, int depth = 0)
        {
            if (target == null || !target.Alive || status == StatusId.None || duration <= 0f) return;
            // Fire on a soaked target only dries it; the flames don't catch.
            if (status == StatusId.Burning && target.Has(StatusId.Soaked))
            {
                target.Statuses.RemoveAll(s => s.Id == StatusId.Soaked);
                return;
            }
            foreach (var cleared in StatusRules.Clears(status)) target.Statuses.RemoveAll(s => s.Id == cleared);

            float dur = CombatRules.StatusDuration(duration, target);
            var existing = target.GetStatus(status);
            if (existing != null)
            {
                existing.Remaining = Math.Max(existing.Remaining, dur);
                existing.Stacks = Math.Min(5, existing.Stacks + stacks);
                existing.SourceId = source?.Id ?? existing.SourceId;
            }
            else
            {
                target.Statuses.Add(new StatusInstance { Id = status, Remaining = dur, Stacks = Math.Min(5, stacks), SourceId = source?.Id ?? 0 });
            }
            if (source != null) Fire(TriggerKind.OnStatusApplied, source, target, status, depth);
        }

        public void Heal(CombatEntity target, float amount)
        {
            if (!target.Alive || amount <= 0f) return;
            if (target.Has(StatusId.Poisoned)) amount *= CombatRules.PoisonHealingFactor;
            target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        }

        public static void Restore(CombatEntity target, ResourceId r, float amount)
        {
            switch (r)
            {
                case ResourceId.HP: target.Hp = Math.Min(target.MaxHp, target.Hp + amount); break;
                case ResourceId.Mana: target.Mana = Math.Min(target.MaxMana, target.Mana + amount); break;
                case ResourceId.Stamina: target.Stamina = Math.Min(target.MaxStamina, target.Stamina + amount); break;
                case ResourceId.Resonance: target.Resonance = Math.Min(100f, target.Resonance + amount); break;
            }
        }

        CombatEntity FindEntity(int id)
        {
            foreach (var e in Entities) if (e.Id == id) return e;
            return null;
        }
    }
}
