using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;

namespace Relic.Items
{
    /// <summary>
    /// The physical half of a theme: what the relic is made of or does
    /// (tide, ember, thorn...). Decides statuses, damage kind, colors and words.
    /// </summary>
    public sealed class Motif
    {
        public string Id;
        public string[] Words;          // nouns for names ("brine", "undertow")
        public StatusId Primary;
        public StatusId Secondary;
        public DamageKind DamageKind;
        public string Color;            // emissive "#RRGGBB"
        public string[] Materials;
        public string[] RegionTags;     // where it feels at home (InRegionTag conditions)
        public ConditionKind? WorldCondition; // IsRaining / IsNight when grounded in weather or time
        public StatId FavoredStat;
        public string Verb;             // "soaks", "burns": used in lore
    }

    /// <summary>The human half of a theme: the story it carries (betrayal, vigil...).</summary>
    public sealed class Mood
    {
        public string Id;
        public string[] Words;          // name modifiers ("Turncoat's", "Sworn")
        public float CurseChance;       // chance the relic trades a drawback for power
    }

    public sealed class Theme
    {
        public Motif Motif;
        public Mood Mood;
        public override string ToString() => Motif.Id + " · " + Mood.Id;
    }

    public static class Themes
    {
        public static readonly Motif[] Motifs =
        {
            new Motif { Id = "tide", Words = new[] { "brine", "undertow", "weir", "tide", "kelp", "silt", "eddy", "fathom", "breaker", "estuary" },
                Primary = StatusId.Soaked, Secondary = StatusId.Chilled, DamageKind = DamageKind.Magical, Color = "#3FA7C9",
                Materials = new[] { "sea-glass", "barnacled bronze", "driftwood" }, RegionTags = new[] { "coast", "river", "marsh" },
                WorldCondition = ConditionKind.IsRaining, FavoredStat = StatId.VIT, Verb = "drowns" },
            new Motif { Id = "ember", Words = new[] { "cinder", "kiln", "tallow", "char", "ember", "soot", "slag", "bellows", "pyre", "firebrick" },
                Primary = StatusId.Burning, Secondary = StatusId.Exhausted, DamageKind = DamageKind.Magical, Color = "#FF7A2E",
                Materials = new[] { "fired clay", "blackened iron", "kiln-glazed stone" }, RegionTags = new[] { "kilns", "badlands", "mines" },
                WorldCondition = null, FavoredStat = StatId.STR, Verb = "burns" },
            new Motif { Id = "frost", Words = new[] { "rime", "hoarfrost", "sleet", "thaw", "icicle", "frostbite", "floe", "snowmelt", "glaze", "permafrost" },
                Primary = StatusId.Chilled, Secondary = StatusId.Rooted, DamageKind = DamageKind.Magical, Color = "#9FD8FF",
                Materials = new[] { "rime-iron", "blue quartz", "whalebone" }, RegionTags = new[] { "tundra", "highland" },
                WorldCondition = ConditionKind.IsNight, FavoredStat = StatId.VIT, Verb = "freezes" },
            new Motif { Id = "thorn", Words = new[] { "bramble", "briar", "thorn", "burr", "hawthorn", "nettle", "blackthorn", "teasel", "thistle", "sloe" },
                Primary = StatusId.Bleeding, Secondary = StatusId.Rooted, DamageKind = DamageKind.Physical, Color = "#7FBF4A",
                Materials = new[] { "black hawthorn wood", "green copper", "woven briar" }, RegionTags = new[] { "forest", "orchards" },
                WorldCondition = null, FavoredStat = StatId.AGI, Verb = "snares" },
            new Motif { Id = "storm", Words = new[] { "squall", "gale", "thunderhead", "gust", "downpour", "stormglass", "lightning-scar", "crosswind", "flurry", "thunder" },
                Primary = StatusId.Dazed, Secondary = StatusId.Staggered, DamageKind = DamageKind.Magical, Color = "#C8D7FF",
                Materials = new[] { "storm-glass", "fulgurite", "tarred canvas" }, RegionTags = new[] { "highland", "steppe", "coast" },
                WorldCondition = ConditionKind.IsRaining, FavoredStat = StatId.AGI, Verb = "batters" },
            new Motif { Id = "venom", Words = new[] { "adder", "nightshade", "wasp", "hemlock", "sting", "fang", "foxglove", "henbane", "asp", "wolfsbane" },
                Primary = StatusId.Poisoned, Secondary = StatusId.Exhausted, DamageKind = DamageKind.Physical, Color = "#A4E04A",
                Materials = new[] { "green bronze", "lacquered horn", "apothecary glass" }, RegionTags = new[] { "marsh", "forest" },
                WorldCondition = null, FavoredStat = StatId.INT, Verb = "sickens" },
            new Motif { Id = "stone", Words = new[] { "cairn", "flint", "granite", "quarry", "slate", "menhir", "lintel", "keystone", "boulder", "scree" },
                Primary = StatusId.Staggered, Secondary = StatusId.Dazed, DamageKind = DamageKind.Physical, Color = "#C9B48A",
                Materials = new[] { "carved granite", "flint-knapped steel", "slate" }, RegionTags = new[] { "quarries", "highland", "ruins" },
                WorldCondition = null, FavoredStat = StatId.STR, Verb = "crushes" },
            new Motif { Id = "bell", Words = new[] { "bell", "toll", "chime", "clapper", "carillon", "knell", "peal", "belfry", "tocsin", "angelus" },
                Primary = StatusId.Dazed, Secondary = StatusId.Silenced, DamageKind = DamageKind.Magical, Color = "#F2D27A",
                Materials = new[] { "bell-bronze", "cracked brass", "oak and pewter" }, RegionTags = new[] { "shrines", "ruins" },
                WorldCondition = null, FavoredStat = StatId.CHR, Verb = "deafens" },
            new Motif { Id = "ink", Words = new[] { "ink", "quill", "ledger", "vellum", "cipher", "margin", "palimpsest", "colophon", "index", "rubric" },
                Primary = StatusId.Silenced, Secondary = StatusId.Dazed, DamageKind = DamageKind.Magical, Color = "#6A5ACD",
                Materials = new[] { "vellum and iron", "gall-ink stained oak", "bone" }, RegionTags = new[] { "ruins", "shrines" },
                WorldCondition = null, FavoredStat = StatId.INS, Verb = "silences" },
            new Motif { Id = "blood", Words = new[] { "vein", "scab", "marrow", "gash", "lifeblood", "clot", "sinew", "bloodletting", "tourniquet", "lancet" },
                Primary = StatusId.Bleeding, Secondary = StatusId.Exhausted, DamageKind = DamageKind.Physical, Color = "#B3122E",
                Materials = new[] { "red-lacquered bone", "surgeon's steel", "dark leather" }, RegionTags = new[] { "badlands", "steppe" },
                WorldCondition = ConditionKind.IsNight, FavoredStat = StatId.VIT, Verb = "bleeds" },
            new Motif { Id = "salt", Words = new[] { "salt", "lye", "saltpan", "brinecrust", "cure", "pickle", "evaporite", "halite", "saltlick", "rocksalt" },
                Primary = StatusId.Bleeding, Secondary = StatusId.Exhausted, DamageKind = DamageKind.Physical, Color = "#EDE8DC",
                Materials = new[] { "salt-crusted silver", "white oak", "pumice" }, RegionTags = new[] { "saltpans", "coast" },
                WorldCondition = null, FavoredStat = StatId.CHR, Verb = "scours" },
            new Motif { Id = "moth", Words = new[] { "moth", "lantern", "wick", "dusk", "candle", "lamplight", "gloaming", "taper", "lampblack", "moonmoth" },
                Primary = StatusId.Dazed, Secondary = StatusId.Burning, DamageKind = DamageKind.Magical, Color = "#FFE9A8",
                Materials = new[] { "paper and brass", "beeswax and tin", "horn-paned iron" }, RegionTags = new[] { "shrines", "orchards" },
                WorldCondition = ConditionKind.IsNight, FavoredStat = StatId.INS, Verb = "dazzles" },
        };

        public static readonly Mood[] Moods =
        {
            new Mood { Id = "betrayal", Words = new[] { "Turncoat's", "False", "Oathbreaker's", "Broken", "Two-Faced" }, CurseChance = 0.5f },
            new Mood { Id = "vigil", Words = new[] { "Watchman's", "Vigil", "Wakeful", "Sentinel's", "Night-Watch" }, CurseChance = 0.15f },
            new Mood { Id = "grief", Words = new[] { "Widow's", "Mourning", "Keening", "Last", "Bereaved" }, CurseChance = 0.3f },
            new Mood { Id = "oath", Words = new[] { "Sworn", "Oath", "Pledged", "Binding", "Vowed" }, CurseChance = 0.15f },
            new Mood { Id = "hunger", Words = new[] { "Famine", "Lean", "Gnawing", "Starveling's", "Hungry" }, CurseChance = 0.6f },
            new Mood { Id = "exile", Words = new[] { "Exile's", "Outcast", "Far-Shore", "Wanderer's", "Unhoused" }, CurseChance = 0.25f },
            new Mood { Id = "pride", Words = new[] { "Crowned", "Vaunting", "Proud", "Gilded", "Peacock's" }, CurseChance = 0.3f },
            new Mood { Id = "mercy", Words = new[] { "Mercy", "Alms", "Gentle", "Pardon", "Almoner's" }, CurseChance = 0.1f },
            new Mood { Id = "debt", Words = new[] { "Tithe", "Debtor's", "Ledger", "Owed", "Usurer's" }, CurseChance = 0.4f },
            new Mood { Id = "homecoming", Words = new[] { "Hearth", "Homeward", "Threshold", "Returning", "Doorstep" }, CurseChance = 0.1f },
        };

        public static Motif MotifById(string id) => Array.Find(Motifs, m => m.Id == id);
        public static Mood MoodById(string id) => Array.Find(Moods, m => m.Id == id);
    }

    /// <summary>
    /// Picks a theme for a seed. When the drop region's tags are known, motifs that
    /// belong there (tide on the coast, ember near kilns) are three times as likely,
    /// so loot feels grounded in where it was found.
    /// </summary>
    public static class ThemePicker
    {
        public static Theme Pick(ulong seed, IList<string> regionTags = null)
        {
            var rng = Pcg32.ForPurpose(seed, "item.theme");
            var weights = new float[Themes.Motifs.Length];
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float w = 1f;
                if (regionTags != null)
                    foreach (var tag in Themes.Motifs[i].RegionTags)
                        if (regionTags.Contains(tag)) { w = 3f; break; }
                weights[i] = w;
                total += w;
            }
            float x = rng.NextFloat() * total;
            int motif = weights.Length - 1;
            for (int i = 0; i < weights.Length; i++)
            {
                x -= weights[i];
                if (x < 0f) { motif = i; break; }
            }
            var mood = Themes.Moods[rng.Range(0, Themes.Moods.Length)];
            return new Theme { Motif = Themes.Motifs[motif], Mood = mood };
        }

        public static Theme Parse(string theme)
        {
            if (string.IsNullOrEmpty(theme)) return null;
            var parts = theme.Split('·');
            if (parts.Length != 2) return null;
            var m = Themes.MotifById(parts[0].Trim());
            var d = Themes.MoodById(parts[1].Trim());
            return m == null || d == null ? null : new Theme { Motif = m, Mood = d };
        }
    }
}
