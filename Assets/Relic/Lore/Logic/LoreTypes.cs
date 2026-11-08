namespace Relic.Lore
{
    // Closed vocabularies of the Lore Bible. JSON stores these as strings
    // (StringEnumConverter), so a typo in data fails at load time.

    /// <summary>The emotional register of a mood, motif or template. Generated content is ~70% Bright.</summary>
    public enum Tone
    {
        Bright,
        Somber,
    }

    /// <summary>
    /// Mirrors Relic.World.Regions.RegionFamily by name (the Lore assembly does not reference
    /// World; a test keeps the two in sync).
    /// </summary>
    public enum LoreFamily
    {
        Meadow,
        Forest,
        Highlands,
        Coast,
        Badlands,
        Frost,
        Marsh,
    }

    public enum Climate
    {
        Temperate,
        Humid,
        Alpine,
        Maritime,
        Arid,
        Cold,
        Wetland,
    }

    public enum FactionKind
    {
        Guild,
        Order,
        Household,
        Company,
        Circle,
    }

    public enum EventKind
    {
        Founding,
        Festival,
        Discovery,
        Invention,
        FriendlyRivalry,
        Rescue,
        Migration,
        LostExpedition,
        Disaster,
        War,
    }

    /// <summary>What kind of world-graph entity (or plain value) fills a template slot.</summary>
    public enum SlotKind
    {
        Place,
        Region,
        Faction,
        Figure,
        Artifact,
        Culture,
        Era,
        Legend,
        /// <summary>A plain word from the culture: a craft, food, form or motif word.</summary>
        Word,
        /// <summary>A small number (years, people, days).</summary>
        Number,
    }

    /// <summary>What an event leaves behind in the world: the hooks other systems can place.</summary>
    public enum EventTrace
    {
        Place,
        Ruin,
        Landmark,
        Artifact,
        Faction,
        Festival,
        Road,
        Rumor,
        Map,
    }

    public enum LegendKind
    {
        Figure,
        Artifact,
        Place,
    }

    public enum RelationKind
    {
        Trade,
        FriendlyRivalry,
        Kinship,
        Wary,
    }

    /// <summary>Effect shapes a motif favors (17-item-grammar). Mapped to emitters by Items.</summary>
    public enum MotifShape
    {
        Burst,
        Cone,
        Line,
        Ring,
        Wall,
        Orbit,
        Aura,
        Area,
        Wave,
    }

    /// <summary>Why monsters keep out of a sanctuary (canon section 6). Recorded per sanctuary.</summary>
    public enum SanctuaryWard
    {
        Hearthward,
        Patron,
        Garrison,
    }
}
