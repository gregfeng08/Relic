namespace Relic.Magic
{
    /// <summary>
    /// Every number the spell compiler uses, in one place. First-pass values,
    /// meant to be tuned in playtests (the Writing Desk shows their effect live).
    /// Pure power pricing lives in Relic.Core's PowerBudget (lead-owned); these
    /// only decide what each glyph means and how mana/cooldown follow from power.
    /// </summary>
    public static class SpellTuning
    {
        // ---- Inner modifiers (per glyph, scaled by its emphasis strength e) ----
        public const float MoreDamage = 0.5f;        // damage × (1 + 0.5e)
        public const float MoreKnockback = 0.3f;
        public const float LessDamage = 0.4f;        // damage × (1 − 0.4e)
        public const float LessDuration = 0.3f;      // statuses and knockback × (1 − 0.3e)
        public const float FarRange = 0.6f;          // projectile range × (1 + 0.6e)
        public const float FarRadius = 0.3f;         // area size × (1 + 0.3e)
        public const float LastingDuration = 0.75f;  // statuses, areas, orbits × (1 + 0.75e)
        public const float HeavySize = 0.8f;         // projectile size × (1 + 0.8e)
        public const float HeavySlow = 0.3f;         // speed × (1 − 0.3e)
        public const float HeavyDamage = 0.25f;
        public const float HeavyRadius = 0.2f;
        public const float SwiftSpeed = 0.5f;        // speed × (1 + 0.5e)
        /// <summary>Extra projectiles (first Split) or impact splits (later Splits) for Small / Normal / Large.</summary>
        public static readonly int[] SplitAmount = { 1, 2, 3 };

        // ---- Ring shapes (base values before modifiers) ----
        public const float LineSpeed = 18f, LineRange = 20f;
        /// <summary>A Line with several bolts fans them this many degrees apart (max 60° total).</summary>
        public const float LineFanStep = 15f, LineFanMax = 60f;
        public const int ConeCount = 5;
        public const float ConeSpread = 60f, ConeSpeed = 16f, ConeRange = 12f;
        public const int RingCount = 8;
        public const float RingSpeed = 12f, RingRange = 10f;
        public const int OrbitCount = 3;
        public const float OrbitSpeed = 8f, OrbitLifetime = 4f, OrbitSize = 0.35f;
        public const float ProjectileSize = 0.25f;
        public const float WallLength = 6f, WallWidth = 1f, WallDuration = 5f, WallTouchWidth = 2f, WallTick = 1f;
        public const float BurstRadius = 2.5f, BurstDuration = 0.5f;
        /// <summary>Seconds each Delay glyph adds before the shapes after it.</summary>
        public const float DelaySeconds = 0.6f;

        // ---- Price: cooldown and mana follow from power (pp at a 4 s baseline) ----
        // Tuned so the Tide Seekers example (≈5.4 pp) costs 3 s and 20 mana.
        public const float CooldownPerSqrtPower = 1.3f;   // cooldown = 1.3 √P
        public const float ManaScale = 2.65f;             // mana = 2.65 P^1.2: cost grows faster than effect
        public const float ManaExponent = 1.2f;
        public const float MinCooldown = 0.5f, MaxCooldown = 30f;
        /// <summary>Intrinsic power one circle can hold.</summary>
        public const float MaxPower = 20f;

        // ---- Precision: sloppy ink is less efficient ----
        /// <summary>Spell quality at or above this is clean: no penalty.</summary>
        public const float CleanQuality = 0.75f;
        /// <summary>Quality at or below this takes the full penalty.</summary>
        public const float WorstQuality = 0.3f;
        public const float SloppyMana = 0.6f;       // up to +60% mana
        public const float SloppyCooldown = 0.4f;   // up to +40% cooldown
        public const float SloppyMisfire = 0.35f;   // up to 35% misfire chance
        /// <summary>Precision credited to glyphs placed from the palette (counts as clean ink).</summary>
        public const float PalettePrecision = 0.9f;
    }
}
