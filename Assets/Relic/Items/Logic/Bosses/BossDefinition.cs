using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Items.Bosses
{
    /// <summary>Pattern building blocks (08-items-bosses). Summons need a grammar action that doesn't exist yet.</summary>
    public enum PatternBlock
    {
        Ring,
        Spiral,
        AimedBurst,
        Cone,
        Laser,
        AreaDenial,
        Lobbed,
        Wall,
    }

    /// <summary>One attack: a single Active rule the boss fires, plus what the player sees during its wind-up.</summary>
    public sealed class BossPattern
    {
        public string Name;
        /// <summary>The building blocks it is made of (one for a base pattern, several for a fused remix).</summary>
        public List<PatternBlock> Blocks = new List<PatternBlock>();
        /// <summary>Null for a base pattern; otherwise the remix operation ("fused", "lingering", "returning", "shattering", "doubled").</summary>
        public string Remix;
        /// <summary>Name of the earlier pattern this remixes.</summary>
        public string RemixOf;
        /// <summary>The physical tell during the wind-up (never a UI decal).</summary>
        public string Tell;
        /// <summary>Shortest wind-up of any damaging part, in seconds.</summary>
        public float WindUp;
        /// <summary>Seconds from start until the last part resolves.</summary>
        public float Duration;
        /// <summary>Has lobbed projectiles (they cast real shadows).</summary>
        public bool Airborne;
        public EffectProgram Program = new EffectProgram();
    }

    public sealed class RotationStep
    {
        /// <summary>Index into the phase's Patterns.</summary>
        public int Pattern;
        /// <summary>Seconds of recovery after the pattern resolves (the boss is open to attack).</summary>
        public float Recover;
    }

    public sealed class BossPhase
    {
        public int Index;
        /// <summary>The phase begins when the boss's health falls to this fraction (phase 1 = 1.0). Health only; never time.</summary>
        public float StartsAtHealth;
        public List<BossPattern> Patterns = new List<BossPattern>();
        /// <summary>Patterns in order; the rotation loops.</summary>
        public List<RotationStep> Rotation = new List<RotationStep>();
        /// <summary>What changes this phase, for readers.</summary>
        public string Change;
    }

    public sealed class EssenceDrop
    {
        public StatId Stat;
        public int Amount;
        public string Name;
    }

    /// <summary>
    /// A generated boss: pure data, serializes to JSON (ItemJson). No enrage
    /// timers exist in the model: phases change on health only.
    /// </summary>
    public sealed class BossDefinition
    {
        public string Id;
        public ulong Seed;
        public string Name;
        public string Title;
        public string Archetype;
        public int Tier;
        /// <summary>Solo health. Group health = BaseHealth × (1 + 0.8 × (players − 1)).</summary>
        public float BaseHealth;
        public List<BossPhase> Phases = new List<BossPhase>();
        public EssenceDrop Essence;
        public string Lore;
        public List<string> WorldRefs = new List<string>();
        public string Arena;
        public List<string> Hazards = new List<string>();
        public GeneratorInfo Generator = new GeneratorInfo();

        public float HealthFor(int players) => BossHealth.ForPlayers(BaseHealth, players);
    }

    /// <summary>
    /// Boss health math (03-combat power bands; documented in Items/README.md).
    ///
    ///   starter DPS  = 10 dmg/hit × 1.5 hits/s × 0.6 uptime = 9 effective DPS
    ///   power band P = 1 + 0.5 × tier        (top gear for the tier; 6× at tier 10)
    ///   target T     = 2 + 0.3 × tier minutes with top gear (5 min at tier 10)
    ///   base HP      = 9 × P × T × 60
    ///
    /// With starter gear the same boss takes P × T minutes: 30 min at tier 10,
    /// ~12 min at tier 5, 2 min at tier 0. Groups: HP × (1 + 0.8 × (players − 1)).
    /// </summary>
    public static class BossHealth
    {
        public const float StarterHitDamage = 10f;
        public const float StarterAttacksPerSecond = 1.5f;
        /// <summary>Share of a fight spent hitting (the rest is dodging, repositioning, recovering).</summary>
        public const float CombatUptime = 0.6f;
        public const float GroupScaling = 0.8f;

        public static float StarterDps => StarterHitDamage * StarterAttacksPerSecond * CombatUptime;

        public static float PowerBand(int tier) => 1f + 0.5f * Clamp(tier);

        public static float TopGearMinutes(int tier) => 2f + 0.3f * Clamp(tier);

        public static float BaseHealth(int tier) => (float)Math.Round(StarterDps * PowerBand(tier) * TopGearMinutes(tier) * 60f);

        public static float ForPlayers(float baseHealth, int players) => baseHealth * (1f + GroupScaling * (Math.Max(1, players) - 1));

        /// <summary>Solo minutes to kill with gear worth `gearMultiplier` × starter DPS (1 = starter, 6 = top, 8 = overrolled).</summary>
        public static float SoloMinutes(int tier, float gearMultiplier) => BaseHealth(tier) / (StarterDps * gearMultiplier) / 60f;

        static int Clamp(int t) => Math.Max(0, Math.Min(Difficulty.MaxTier, t));
    }
}
