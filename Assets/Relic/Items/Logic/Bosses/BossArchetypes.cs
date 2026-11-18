using System;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Items.Bosses
{
    /// <summary>
    /// A kind of boss: decides its essence, preferred patterns, statuses, arena
    /// and lore. Bosses of one archetype are learnable (the same blocks recur).
    /// </summary>
    public sealed class BossArchetype
    {
        public string Id;
        public StatId EssenceStat;
        /// <summary>Weights per PatternBlock (index = (int)block). 0 = never.</summary>
        public float[] BlockWeights;
        /// <summary>Statuses its attacks inflict. Never Rooted or Staggered (lockdown would make follow-ups unavoidable).</summary>
        public StatusId[] Statuses;
        public DamageKind DamageKind;
        /// <summary>One pattern name per block (index = (int)block).</summary>
        public string[] PatternNames;
        /// <summary>What every wind-up looks like in this boss's arena (added to the block's own tell).</summary>
        public string TellFlourish;
        public string[] Titles;           // "{region}" / "{faction}" / "{landmark}" placeholders
        public string[] Arenas;
        public string[] Hazards;
        /// <summary>Guardian lore, the usual case (canon: many bosses are wild guardians, not villains). Placeholders: {name} {region} {landmark} {faction} {event} {figure} {culture}.</summary>
        public string[] LoreTemplates;
        /// <summary>The somber minority: old constructs of the Quarrel, things left alone in the Hush.</summary>
        public string[] SomberLore;
    }

    public static class BossArchetypes
    {
        //                                         Ring Spir Aim  Cone Laser Area Lob  Wall
        public static readonly BossArchetype[] All =
        {
            new BossArchetype
            {
                Id = "Librarian", EssenceStat = StatId.INT, DamageKind = DamageKind.Magical,
                BlockWeights = new float[] { 2, 3, 2, 1, 1, 1, 1, 2 },
                Statuses = new[] { StatusId.Silenced, StatusId.Dazed },
                PatternNames = new[] { "Scattered Folios", "Turning Pages", "Marginal Notes", "Fanned Quires", "Line of Script", "Ink Spill", "Thrown Tomes", "The Stacks Fall" },
                TellFlourish = "ink beads up along the floor runes",
                Titles = new[] { "the Index-Keeper", "Archivist of {region}", "the Last Reader of {landmark}" },
                Arenas = new[] { "a flooded reading room with toppled shelves for cover", "a round archive whose floor is one huge inlaid index" },
                Hazards = new[] { "shelves that topple when struck", "ink pools that leave footprints" },
                LoreTemplates = new[]
                {
                    "{name} has kept the reading room at {landmark} since the Hush, long after {faction} stopped sending readers. It still shushes visitors.",
                    "The {culture} say {name} was a reading-owl once. Now it guards every book in {region} and lends none of them.",
                },
                SomberLore = new[]
                {
                    "{name} was set to guard an Imperative vault in {region} during the Quarrel. Nobody ever came back to tell it to stop.",
                },
            },
            new BossArchetype
            {
                Id = "Warlord", EssenceStat = StatId.STR, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 3, 3, 1, 2, 1, 1 },
                Statuses = new[] { StatusId.Bleeding, StatusId.Exhausted },
                PatternNames = new[] { "Shield Wall Breaks", "Wheel of Spears", "Javelin Volley", "Cavalry Sweep", "The Charge", "Caltrops", "Siege Stones", "Palisade" },
                TellFlourish = "its banner snaps taut and the drummers stop",
                Titles = new[] { "Marshal of {region}", "the Old Marshal", "Keeper of the Muster-Field" },
                Arenas = new[] { "a churned muster-field ringed with stakes", "the courtyard of a fort nobody remembers the name of" },
                Hazards = new[] { "stakes that break into splinters", "trampled mud that slows the unwary" },
                LoreTemplates = new[]
                {
                    "{name} still drills an army of scarecrows on the old muster-field in {region}. It lets travelers pass once they beat it.",
                    "{faction} hired {name} to guard {landmark} a very long time ago. Nobody remembers to pay it, and it hasn't noticed.",
                },
                SomberLore = new[]
                {
                    "{name} is an Imperative construct from the Quarrel, still holding {landmark} against an army that went home long ago.",
                },
            },
            new BossArchetype
            {
                Id = "Oracle", EssenceStat = StatId.INS, DamageKind = DamageKind.Magical,
                BlockWeights = new float[] { 2, 2, 1, 0, 3, 2, 0, 1 },
                Statuses = new[] { StatusId.Dazed, StatusId.Silenced },
                PatternNames = new[] { "Halo of Omens", "The Wheel Turns", "Pointed Prophecy", "Fan of Fates", "Sightline", "Foreseen Ground", "Falling Stars", "Veil" },
                TellFlourish = "the star-chart on the floor brightens under the next target",
                Titles = new[] { "the Blind Surveyor", "Seer of {landmark}", "the Oracle of {region}" },
                Arenas = new[] { "an observatory with its roof torn open", "a sunken orrery whose rings still turn" },
                Hazards = new[] { "orrery rings that sweep slowly across the floor", "glare from the open roof at noon" },
                LoreTemplates = new[]
                {
                    "{name} reads the stars over {region} and corrects everyone's almanac, whether they asked or not.",
                    "Wayfarers climb to {landmark} to ask {name} the way. It answers with a test first, and then with the way.",
                },
                SomberLore = new[]
                {
                    "{name} saw {event} coming and told nobody. It has watched the sky over {region} alone ever since.",
                },
            },
            new BossArchetype
            {
                Id = "Beastmother", EssenceStat = StatId.VIT, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 2, 2, 0, 2, 3, 0 },
                Statuses = new[] { StatusId.Poisoned, StatusId.Bleeding },
                PatternNames = new[] { "Quill Shake", "Circling Brood", "Spit", "Raking Claws", "Lunge", "Nest Rot", "Egg Clutch", "Burrow" },
                TellFlourish = "the brood in the walls shrieks and goes silent",
                Titles = new[] { "Mother of the {region} Brood", "the Den-Queen", "the Den-Mother of {landmark}" },
                Arenas = new[] { "a den of moulted quills and old nests", "a flooded quarry where the brood nests in the walls" },
                Hazards = new[] { "soured nest-matter that stings", "brood hatchlings (cosmetic until summons exist)" },
                LoreTemplates = new[]
                {
                    "{name} guards the nesting grounds under {landmark}. The {culture} leave her honey cakes, and she leaves them the valley.",
                    "Herders in {region} call {name} the Den-Mother. She is fierce, fair, and very particular about her eggs.",
                },
                SomberLore = new[]
                {
                    "{name} grew huge in the quiet of the Hush, and she has not forgiven {region} for leaving her alone.",
                },
            },
            new BossArchetype
            {
                Id = "Tidecaller", EssenceStat = StatId.VIT, DamageKind = DamageKind.Magical,
                BlockWeights = new float[] { 3, 2, 1, 1, 1, 2, 1, 0 },
                Statuses = new[] { StatusId.Soaked, StatusId.Chilled },
                PatternNames = new[] { "Breakwater", "Whirlpool", "Spray", "Wave Front", "Undertow Line", "Tide Pool", "Brine Hail", "Seawall" },
                TellFlourish = "the water at its feet draws back, the way the sea does before a wave",
                Titles = new[] { "the Weir-Witch", "Tidecaller of {region}", "Keeper of {landmark}" },
                Arenas = new[] { "a tidal flat that floods and drains", "the broken basin of a weir" },
                Hazards = new[] { "tides that rise and fall on a cycle", "slick weed on the stones" },
                LoreTemplates = new[]
                {
                    "{name} keeps the tides at {landmark} for the fishing boats of {region}, and tests anyone who wants to cross.",
                    "The {culture} say {name} was a ferry spirit who grew too big for the ferry. It still keeps the crossing.",
                },
                SomberLore = new[]
                {
                    "{name} was bound to a weir during the Quarrel and never released. The water at {landmark} still answers to it.",
                },
            },
            new BossArchetype
            {
                Id = "Smith-King", EssenceStat = StatId.STR, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 1, 2, 0, 3, 3, 1 },
                Statuses = new[] { StatusId.Burning, StatusId.Exhausted },
                PatternNames = new[] { "Rivet Burst", "Turning Bellows", "Nail Shot", "Spark Fan", "Quench Line", "Molten Pour", "Slag Toss", "Anvil Drop" },
                TellFlourish = "the forge-vents roar and the floor grates glow orange",
                Titles = new[] { "the Smith-King of {region}", "Keeper of the Great Forge", "Master of {landmark}" },
                Arenas = new[] { "a forge hall with quench troughs for cover", "a slag-field around a cold furnace" },
                Hazards = new[] { "quench troughs that put out Burning", "vents that flare on a cycle" },
                LoreTemplates = new[]
                {
                    "{name} keeps the great forge at {landmark} burning for all of {region}, and lets only worthy hands near the anvil.",
                    "{faction} cast their first bells at the forge of {name}. It still checks every one for flaws, loudly.",
                },
                SomberLore = new[]
                {
                    "{name} was an Imperative forge-construct. It still hammers out orders at {landmark}, though nobody comes to collect them.",
                },
            },
            new BossArchetype
            {
                Id = "Duelist", EssenceStat = StatId.AGI, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 1, 3, 2, 2, 1, 0, 0 },
                Statuses = new[] { StatusId.Bleeding, StatusId.Dazed },
                PatternNames = new[] { "Salute", "Pirouette", "Thrown Knives", "Flourish", "Lunge", "Chalk Circle", "Tossed Coins", "Parry Wall" },
                TellFlourish = "it shifts its weight onto the back foot",
                Titles = new[] { "the Twice-Unbeaten", "First Blade of {faction}", "the Duelist of {landmark}" },
                Arenas = new[] { "a chalk-marked duelling yard", "a ballroom with the chandeliers on the floor" },
                Hazards = new[] { "broken chandelier glass", "pillars that can be cut through" },
                LoreTemplates = new[]
                {
                    "{name} guards the bridge at {landmark} and asks every traveler for a friendly bout. It has not lost in three hundred years.",
                    "{faction} send their best to spar with {name} each spring. They come back bruised, delighted and much better.",
                },
                SomberLore = new[]
                {
                    "{name} swore to guard {landmark} until its duel was finished. Its rival never came back from {event}.",
                },
            },
            new BossArchetype
            {
                Id = "Courtier", EssenceStat = StatId.CHR, DamageKind = DamageKind.Magical,
                BlockWeights = new float[] { 2, 3, 1, 1, 0, 2, 0, 1 },
                Statuses = new[] { StatusId.Dazed, StatusId.Poisoned },
                PatternNames = new[] { "Curtsey", "The Waltz", "Pointed Remark", "Fan Snap", "Receiving Line", "Spilled Wine", "Thrown Favours", "Folding Screen" },
                TellFlourish = "the musicians in the gallery stop mid-phrase",
                Titles = new[] { "the Velvet Magistrate", "Chamberlain of {faction}", "Host of {landmark}" },
                Arenas = new[] { "a ballroom with folding screens for cover", "a garden court with fizzing fountains" },
                Hazards = new[] { "fountains that spit stinging spray", "screens that fold and unfold" },
                LoreTemplates = new[]
                {
                    "{name} hosts a ball at {landmark} every midsummer. Guests who dance badly must try again, and again.",
                    "{name} keeps the old manners of {region} alive and lets nobody into {landmark} without a proper bow.",
                },
                SomberLore = new[]
                {
                    "{name} kept up the court at {landmark} after everyone left in the Hush. It is still waiting for the guests.",
                },
            },
        };

        public static BossArchetype ById(string id) => Array.Find(All, a => a.Id == id);
    }
}
