using System.Collections.Generic;
using Relic.Core.Stats;

namespace Relic.Core.Effects
{
    // The effect grammar: the one language that relics, spells, weapon skills,
    // enemy attacks and elite modifiers all compile into. One runtime executes it,
    // one validator balances it. Contract: docs/design/05-effect-grammar.md.
    //
    // Shape:  EffectProgram → Rules → (Trigger, Conditions, Actions)
    //         Actions can carry a Payload: child actions applied to whatever an
    //         Emit projectile or an Area touches.
    //
    // Units: distances in meters, times in seconds, damage/heal in absolute
    // points where 10 = one baseline starter-weapon hit at tier 0.
    //
    // Plain classes with public fields (not records/init) so Unity's C# 9
    // compiler, Newtonsoft and the .NET test harness all accept them.

    public sealed class EffectProgram
    {
        public int Version = 1;
        public List<EffectRule> Rules = new List<EffectRule>();
    }

    public sealed class EffectRule
    {
        /// <summary>Display name, e.g. "Undertow". Flavor only; never affects behavior.</summary>
        public string Name;
        public TriggerSpec Trigger = new TriggerSpec();
        public List<ConditionSpec> Conditions = new List<ConditionSpec>();
        public List<ActionSpec> Actions = new List<ActionSpec>();
        /// <summary>Probability the rule fires when triggered, in (0, 1].</summary>
        public float Chance = 1f;
        /// <summary>Seconds before the rule can fire again. Required (≥ 0.2) for Active rules.</summary>
        public float Cooldown;
        /// <summary>Resource spent when the rule fires (mana for spells, stamina for heavy skills).</summary>
        public ResourceId CostResource = ResourceId.Mana;
        public float CostAmount;
    }

    public enum TriggerKind
    {
        /// <summary>Fires when the player uses it: weapon skill (E), spell page, relic release (Q).</summary>
        Active,
        /// <summary>Always on while equipped: stat modifiers and auras.</summary>
        Passive,
        OnHit,
        OnKill,
        OnTakeDamage,
        OnDash,
        /// <summary>An enemy projectile passed within a hand's width without hitting.</summary>
        OnCloseCall,
        /// <summary>Own HP crossed below Threshold (fraction 0–1).</summary>
        OnLowHealth,
        /// <summary>Every Interval seconds.</summary>
        Periodic,
        /// <summary>When Status is applied to a target by the wearer.</summary>
        OnStatusApplied,
    }

    public sealed class TriggerSpec
    {
        public TriggerKind Kind = TriggerKind.Active;
        /// <summary>Periodic: seconds between firings.</summary>
        public float Interval;
        /// <summary>OnLowHealth: HP fraction (0–1).</summary>
        public float Threshold;
        /// <summary>OnStatusApplied: which status.</summary>
        public StatusId Status;
    }

    public enum ConditionKind
    {
        TargetHasStatus,
        SelfHasStatus,
        SelfHealthBelow,     // Value = fraction 0–1
        TargetHealthBelow,   // Value = fraction 0–1
        TargetIsBoss,
        DistanceToTargetBelow, // Value = meters
        DistanceToTargetAbove, // Value = meters
        IsNight,
        IsRaining,
        InRegionTag,         // Tag = world-graph region tag, e.g. "coast"
    }

    public sealed class ConditionSpec
    {
        public ConditionKind Kind;
        public StatusId Status;
        public float Value;
        public string Tag;
        /// <summary>Inverts the condition ("target is NOT soaked").</summary>
        public bool Negate;
    }

    public enum ActionKind
    {
        Damage,          // Amount, DamageKind
        ApplyStatus,     // Status, Duration, Stacks
        RemoveStatus,    // Status
        Heal,            // Amount
        RestoreResource, // Resource, Amount
        Knockback,       // Amount = meters pushed away from the source
        Pull,            // Amount = meters pulled toward the source
        ModifyStat,      // Stat, Amount (flat) or Multiplier, Duration (0 = while equipped)
        Emit,            // Emitter + Payload applied to whatever each projectile hits
        CreateArea,      // Area + Payload applied each tick to whatever is inside
        CreateBarrier,   // Area (Wall shape): a physical obstacle that blocks projectiles
    }

    public enum TargetKind
    {
        /// <summary>Whatever the trigger is about: the hit enemy, the attacker, the projectile's victim.</summary>
        Target,
        Self,
        /// <summary>Enemies inside an Area (only meaningful inside a CreateArea payload).</summary>
        EnemiesInArea,
        /// <summary>Allies inside an Area, the wearer included.</summary>
        AlliesInArea,
    }

    public enum DamageKind
    {
        /// <summary>Scales with STR; reduced by DEF.</summary>
        Physical,
        /// <summary>Scales with INT; reduced by DEF.</summary>
        Magical,
    }

    public sealed class ActionSpec
    {
        public ActionKind Kind;
        public TargetKind Target = TargetKind.Target;

        public float Amount;
        public DamageKind DamageKind = DamageKind.Physical;

        public StatusId Status;
        public float Duration;
        public int Stacks = 1;

        public ResourceId Resource;
        public StatId Stat;
        /// <summary>ModifyStat: multiplicative bonus (0.1 = +10%). Used instead of Amount when non-zero.</summary>
        public float Multiplier;

        public EmitterSpec Emitter;
        public AreaSpec Area;

        /// <summary>Emit / CreateArea: actions applied to what gets hit or what stands inside.</summary>
        public List<ActionSpec> Payload;
    }

    public enum EmitShape
    {
        Single, // one projectile toward the aim point
        Line,   // Count projectiles in a row, one after another
        Cone,   // Count projectiles fanned across SpreadDegrees
        Ring,   // Count projectiles evenly around the source
        Spiral, // Ring released over time while rotating
        Burst,  // Count projectiles with random spread (seeded)
        Orbit,  // Count projectiles circling the source for Range seconds
    }

    public sealed class EmitterSpec
    {
        public EmitShape Shape = EmitShape.Single;
        public int Count = 1;
        /// <summary>Meters per second.</summary>
        public float Speed = 15f;
        /// <summary>Max travel distance in meters (Orbit: lifetime in seconds).</summary>
        public float Range = 15f;
        public float SpreadDegrees;
        /// <summary>Seconds before release (wind-up). Enemy attacks must have a readable tell here.</summary>
        public float Delay;
        /// <summary>Curves toward the nearest valid target.</summary>
        public bool Seeking;
        /// <summary>Comes back to the source after reaching Range.</summary>
        public bool Returning;
        /// <summary>On first impact, splits into this many smaller projectiles (0 = no split).</summary>
        public int SplitCount;
        /// <summary>Projectile radius in meters (also the hitbox).</summary>
        public float Size = 0.25f;
        /// <summary>Lobbed through the air; lands where its shadow is. False = travels on the ground plane.</summary>
        public bool Airborne;
    }

    public enum AreaShape
    {
        Circle, // Radius
        Cone,   // Radius + Angle
        Line,   // Length + Width
        Wall,   // Length + Width (CreateBarrier)
    }

    public sealed class AreaSpec
    {
        public AreaShape Shape = AreaShape.Circle;
        public float Radius = 3f;
        public float Angle = 60f;
        public float Length;
        public float Width = 1f;
        /// <summary>Seconds the area exists.</summary>
        public float Duration = 1f;
        /// <summary>Seconds between payload applications (0 = once on creation).</summary>
        public float TickInterval;
        /// <summary>Seconds of visible tell before the area becomes active.</summary>
        public float Delay;
        /// <summary>Centered on the wearer instead of the target/aim point (auras).</summary>
        public bool FollowsSelf;
    }
}
