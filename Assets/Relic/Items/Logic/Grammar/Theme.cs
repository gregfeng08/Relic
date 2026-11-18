using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;
using Relic.Lore;

namespace Relic.Items
{
    /// <summary>
    /// An item's theme (17-item-grammar): a Lore Bible motif (the physical half: statuses,
    /// shapes, color, words) and a mood (the human half: bright or somber, effect lean,
    /// lore lines). Replaces the old Items-only Themes table.
    /// </summary>
    public sealed class Theme
    {
        public MotifDef Motif;
        public MoodDef Mood;

        public Tone Tone => Mood.Tone;
        public bool IsSomber => Mood.Tone == Tone.Somber;

        /// <summary>The motif's first status: what its effects inflict most.</summary>
        public StatusId Primary => Motif.Statuses.Count > 0 ? Motif.Statuses[0] : StatusId.Dazed;
        /// <summary>The motif's second status, or None when it has only one (callers fall back to the archetype's).</summary>
        public StatusId Secondary => Motif.Statuses.Count > 1 ? Motif.Statuses[1] : StatusId.None;
        public DamageKind DamageKind => MotifTraits.For(Motif.Id).DamageKind;
        public ConditionKind? WorldCondition => MotifTraits.For(Motif.Id).WorldCondition;
        public StatId FavoredStat => Motif.FavoredStat;
        public List<string> Words => Motif.Words;
        public string Color => Motif.Color;

        /// <summary>True when the mood leans toward this effect family ("ally", "aura", "drawback"...).</summary>
        public bool Leans(string lean) => Mood.EffectLean.Contains(lean);

        public override string ToString() => Motif.Id + " · " + Mood.Id;
    }

    /// <summary>Picks and parses themes from the Lore Bible.</summary>
    public static class ThemePicker
    {
        /// <summary>
        /// Mood at the Bible's bright share (70/30), then a motif that suits the mood, chosen
        /// from the culture's motifs when any of them fit (MoodPicker.MotifFor).
        /// </summary>
        public static Theme Pick(LoreBible bible, CultureDef culture, ref Pcg32 rng)
        {
            var mood = new MoodPicker(bible.Moods).Pick(ref rng);
            var motif = MoodPicker.MotifFor(ref rng, bible, mood, culture?.Motifs);
            return new Theme { Motif = motif, Mood = mood };
        }

        /// <summary>"lantern · wonder" back into a theme; null when either half is unknown.</summary>
        public static Theme Parse(string theme, LoreBible bible)
        {
            if (string.IsNullOrEmpty(theme) || bible == null) return null;
            var parts = theme.Split('·');
            if (parts.Length != 2) return null;
            var m = bible.Motif(parts[0].Trim());
            var d = bible.Mood(parts[1].Trim());
            return m == null || d == null ? null : new Theme { Motif = m, Mood = d };
        }
    }

    /// <summary>
    /// What Items needs from a motif that the Lore Bible doesn't record: the damage kind
    /// its effects deal, a world condition it feels at home in, and its decal.
    /// </summary>
    public sealed class MotifTraits
    {
        public DamageKind DamageKind = DamageKind.Magical;
        public ConditionKind? WorldCondition;
        public string Decal = "none";

        static readonly Dictionary<string, MotifTraits> Table = new Dictionary<string, MotifTraits>(StringComparer.Ordinal)
        {
            ["tide"] = new MotifTraits { WorldCondition = ConditionKind.IsRaining, Decal = "barnacles" },
            ["ember"] = new MotifTraits { Decal = "scorch" },
            ["frost"] = new MotifTraits { WorldCondition = ConditionKind.IsNight, Decal = "frost-rime" },
            ["bell"] = new MotifTraits { Decal = "gilt-filigree" },
            ["thorn"] = new MotifTraits { DamageKind = DamageKind.Physical, Decal = "thorn-scratches" },
            ["wind"] = new MotifTraits { DamageKind = DamageKind.Physical, Decal = "wind-ribbons" },
            ["lantern"] = new MotifTraits { WorldCondition = ConditionKind.IsNight, Decal = "wax-drips" },
            ["bloom"] = new MotifTraits { Decal = "petals" },
            ["stone"] = new MotifTraits { DamageKind = DamageKind.Physical, Decal = "tally-marks" },
            ["ink"] = new MotifTraits { Decal = "ink-blots" },
            ["hive"] = new MotifTraits { DamageKind = DamageKind.Physical, Decal = "honeycomb" },
            ["star"] = new MotifTraits { WorldCondition = ConditionKind.IsNight, Decal = "star-chart" },
            ["mist"] = new MotifTraits { WorldCondition = ConditionKind.IsRaining, Decal = "moss" },
            ["iron"] = new MotifTraits { DamageKind = DamageKind.Physical, Decal = "rust-bloom" },
            ["song"] = new MotifTraits { Decal = "song-notes" },
            ["sun"] = new MotifTraits { Decal = "sun-rays" },
        };

        static readonly MotifTraits Fallback = new MotifTraits();

        /// <summary>Traits for a motif id; unknown motifs get magical damage and no decal (a test lists any gaps).</summary>
        public static MotifTraits For(string motifId) =>
            motifId != null && Table.TryGetValue(motifId, out var t) ? t : Fallback;

        public static bool Has(string motifId) => motifId != null && Table.ContainsKey(motifId);
    }
}
