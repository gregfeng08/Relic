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
        public string[] LoreTemplates;    // placeholders: {name} {region} {landmark} {faction} {event} {figure}
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
                    "{name} kept the archive at {landmark} for {faction} until {event}. When the wages stopped, they kept the books anyway, and nothing leaves {region} unrecorded.",
                    "{name} catalogued every debt {faction} was ever owed. After {event} they began cataloguing the people who owed them.",
                },
            },
            new BossArchetype
            {
                Id = "Warlord", EssenceStat = StatId.STR, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 3, 3, 1, 2, 1, 1 },
                Statuses = new[] { StatusId.Bleeding, StatusId.Exhausted },
                PatternNames = new[] { "Shield Wall Breaks", "Wheel of Spears", "Javelin Volley", "Cavalry Sweep", "The Charge", "Caltrops", "Siege Stones", "Palisade" },
                TellFlourish = "its banner snaps taut and the drummers stop",
                Titles = new[] { "Marshal of {region}", "the Iron Tithe", "Warlord of {faction}" },
                Arenas = new[] { "a churned muster-field ringed with stakes", "the courtyard of a burnt fort" },
                Hazards = new[] { "stakes that break into splinters", "trampled mud that slows the unwary" },
                LoreTemplates = new[]
                {
                    "{name} held {region} for {faction} through {event}, and never handed it back. Their soldiers still collect the tithe at {landmark}.",
                    "{name} won {event} for {faction} and was paid in land instead of coin. They have been taking the rest by force ever since.",
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
                    "{name} told {faction} the date of {event} a year early, and nobody listened. They have been reading {region}'s fortunes alone at {landmark} ever since.",
                    "{name} put out their own eyes to stop seeing {event}. It did not help.",
                },
            },
            new BossArchetype
            {
                Id = "Beastmother", EssenceStat = StatId.VIT, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 2, 2, 0, 2, 3, 0 },
                Statuses = new[] { StatusId.Poisoned, StatusId.Bleeding },
                PatternNames = new[] { "Quill Shake", "Circling Brood", "Spit", "Raking Claws", "Lunge", "Nest Rot", "Egg Clutch", "Burrow" },
                TellFlourish = "the brood in the walls shrieks and goes silent",
                Titles = new[] { "Mother of the {region} Brood", "the Den-Queen", "the Thing under {landmark}" },
                Arenas = new[] { "a den of bones and moulted quills", "a flooded quarry where the brood nests in the walls" },
                Hazards = new[] { "rotting nest-matter that poisons", "brood hatchlings (cosmetic until summons exist)" },
                LoreTemplates = new[]
                {
                    "Herders in {region} left offerings at {landmark} for {name} until {event}. Then the offerings stopped, and the brood came looking for them.",
                    "{faction} tried to drive {name} out of {region} and lost a generation of hunters doing it.",
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
                    "{name} drowned {region} on the orders of {faction} during {event}. The water never fully went back down, and neither did they.",
                    "{name} kept {landmark} for {faction} for forty years and was paid in salt. When {event} came they opened the sluices.",
                },
            },
            new BossArchetype
            {
                Id = "Smith-King", EssenceStat = StatId.STR, DamageKind = DamageKind.Physical,
                BlockWeights = new float[] { 1, 0, 1, 2, 0, 3, 3, 1 },
                Statuses = new[] { StatusId.Burning, StatusId.Exhausted },
                PatternNames = new[] { "Rivet Burst", "Turning Bellows", "Nail Shot", "Spark Fan", "Quench Line", "Molten Pour", "Slag Toss", "Anvil Drop" },
                TellFlourish = "the forge-vents roar and the floor grates glow orange",
                Titles = new[] { "the Smith-King of {region}", "Lord of the Slag", "Master of {landmark}" },
                Arenas = new[] { "a forge hall with quench troughs for cover", "a slag-field around a cold furnace" },
                Hazards = new[] { "quench troughs that put out Burning", "vents that flare on a cycle" },
                LoreTemplates = new[]
                {
                    "{name} forged the arms {faction} carried through {event} and crowned themself with the leftovers. Their furnace at {landmark} has not gone cold since.",
                    "{name} melted down the bells of {region} to arm {faction}. The town has been quiet ever since.",
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
                    "{name} settled {faction}'s quarrels at {landmark} for a fee, and never lost. After {event} they stopped waiting to be hired.",
                    "{name} killed {figure}'s heir in a duel over a debt and has been waiting at {landmark} for the next challenger ever since.",
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
                Arenas = new[] { "a ballroom with folding screens for cover", "a garden court with poisoned fountains" },
                Hazards = new[] { "fountains laced with poison", "screens that fold and unfold" },
                LoreTemplates = new[]
                {
                    "{name} hosted the dinner at {landmark} where {faction} agreed to {event}. Every guest who left early is buried in {region}.",
                    "{name} has outlived three heads of {faction} by never saying no to any of them.",
                },
            },
        };

        public static BossArchetype ById(string id) => Array.Find(All, a => a.Id == id);
    }
}
