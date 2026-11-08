using System.Collections.Generic;

namespace Relic.Lore
{
    /// <summary>
    /// One culture of the Wide (docs/lore/cultures.md). Loaded from Data/Cultures/&lt;id&gt;.json.
    /// Generators read the structured lists; the LLM gets the short prose fields as context.
    /// </summary>
    public sealed class CultureDef
    {
        public string Id;
        public string Name;
        /// <summary>Adjective form: "Wendle bread", "Solannish glass".</summary>
        public string Adjective;
        /// <summary>One line: who they are.</summary>
        public string Identity;

        /// <summary>Region families this culture settles, strongest first. Never empty.</summary>
        public List<LoreFamily> Families = new List<LoreFamily>();
        public List<Climate> Climates = new List<Climate>();

        public Phonology Phonology = new Phonology();

        public string Architecture;
        public List<string> Crafts = new List<string>();
        public List<string> Materials = new List<string>();
        public List<string> Foods = new List<string>();
        public List<FestivalDef> Festivals = new List<FestivalDef>();
        public List<string> Values = new List<string>();
        public string Pride;

        /// <summary>Faction archetype ids (factions.json) typical of this culture.</summary>
        public List<string> Factions = new List<string>();

        /// <summary>Item forms this culture makes ("bell", "kite-blade"); Items maps them to bases.</summary>
        public List<string> ItemForms = new List<string>();
        /// <summary>Motif ids (motifs.json) this culture favors.</summary>
        public List<string> Motifs = new List<string>();
        public ItemNaming ItemNaming = new ItemNaming();

        public List<CultureRelation> Relations = new List<CultureRelation>();
    }

    /// <summary>
    /// Syllable sets for names. A pattern like "SME" picks one Start, one Middle and one End
    /// syllable. Names within a culture share syllables, so they sound related.
    /// </summary>
    public sealed class Phonology
    {
        public List<string> Starts = new List<string>();
        public List<string> Middles = new List<string>();
        public List<string> Ends = new List<string>();
        public List<SyllablePattern> Patterns = new List<SyllablePattern>();

        /// <summary>Chance a person also gets a family name (a second root).</summary>
        public double SurnameChance;
        /// <summary>Pattern shape for family names, e.g. "SE".</summary>
        public string SurnamePattern = "SE";

        /// <summary>Letter bounds for each generated word.</summary>
        public int MinLength = 3;
        public int MaxLength = 10;

        public PlaceNaming Places = new PlaceNaming();
    }

    public sealed class SyllablePattern
    {
        /// <summary>Letters S (start), M (middle), E (end), in order. "S", "SE", "SME", "SMME".</summary>
        public string Shape;
        public int Weight = 1;
    }

    public sealed class PlaceNaming
    {
        /// <summary>Bound endings joined to a root: "Bram" + "wick" = "Bramwick".</summary>
        public List<string> Suffixes = new List<string>();
        /// <summary>Free words after a root: "Iravi Glade".</summary>
        public List<string> Words = new List<string>();
        /// <summary>Chance of a bound suffix rather than a free word.</summary>
        public double SuffixChance = 0.6;
    }

    public sealed class FestivalDef
    {
        public string Name;
        public string Season;
        public string Description;
    }

    /// <summary>How a culture names and marks its items (feeds 17-item-grammar).</summary>
    public sealed class ItemNaming
    {
        /// <summary>Patterns with slots: {maker}, {form}, {place}, {epithet}, {adjective}, {word}.</summary>
        public List<string> Patterns = new List<string>();
        public List<string> Epithets = new List<string>();
        public List<string> Adjectives = new List<string>();
        /// <summary>How makers sign their work: "a stamped wheat-ear on the tang".</summary>
        public List<string> MakersMarks = new List<string>();
    }

    public sealed class CultureRelation
    {
        public string Culture;
        public RelationKind Kind;
        public string Note;
    }
}
