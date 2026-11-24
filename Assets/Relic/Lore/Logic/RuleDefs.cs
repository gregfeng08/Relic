using System.Collections.Generic;

namespace Relic.Lore
{
    // The "constitution" side of the Lore Bible (16, decision 2026-11-18): templates that
    // each world instantiates by seed. No culture, era or legend is fixed content any more.

    /// <summary>A family of name sounds (phonology.json). Generated cultures take a subset of its syllables.</summary>
    public sealed class PhonologyFamily
    {
        public string Id;
        /// <summary>How the names sound, in a phrase (for docs and prompts).</summary>
        public string Label;
        /// <summary>Endings that turn a culture's root into its adjective ("" keeps the root: "Bramwen").</summary>
        public List<string> AdjectiveEndings = new List<string>();
        public Phonology Phonology = new Phonology();
    }

    /// <summary>
    /// What an item form is, for Items (forms.json): the base type name (Items.BaseType) and,
    /// for relics, the kit form id (Items.Forms.RelicForms). A form may appear twice ("charm"
    /// as a trinket and as a relic).
    /// </summary>
    public sealed class FormTag
    {
        public string Form;
        public string Base;
        /// <summary>Relic kit form id ("shield", "bell"); null for other bases.</summary>
        public string Kit;
    }

    /// <summary>A kind of people that a world can grow (culture-templates.json).</summary>
    public sealed class CultureTemplate
    {
        public string Id;
        /// <summary>Plain description: "meadow farmers, millers and bakers".</summary>
        public string Label;
        public int Weight = 1;
        /// <summary>Region families it favors, strongest first.</summary>
        public List<LoreFamily> Families = new List<LoreFamily>();
        public List<Climate> Climates = new List<Climate>();
        /// <summary>Phonology family ids it can speak.</summary>
        public List<string> Phonologies = new List<string>();
        public List<string> Identities = new List<string>();
        public List<string> Architecture = new List<string>();
        public List<string> Crafts = new List<string>();
        /// <summary>Non-metal materials and finishes (names from Items' material table).</summary>
        public List<string> Materials = new List<string>();
        /// <summary>Metal-like materials; every generated culture works at least one.</summary>
        public List<string> Metals = new List<string>();
        public List<string> Foods = new List<string>();
        public List<FestivalDef> Festivals = new List<FestivalDef>();
        public List<string> Values = new List<string>();
        public List<string> Prides = new List<string>();
        public List<string> Factions = new List<string>();
        public List<string> Motifs = new List<string>();
        /// <summary>Form names (forms.json).</summary>
        public List<string> ItemForms = new List<string>();
        public ItemNaming ItemNaming = new ItemNaming();
        /// <summary>One-sentence mood lines; each culture gets two. Slots: {people}, {adjective}.</summary>
        public List<string> MoodLines = new List<string>();
        public List<string> FounderEpithets = new List<string>();
    }

    /// <summary>A kind of era a world's prehistory can contain (prehistory.json).</summary>
    public sealed class EraTemplate
    {
        public string Id;
        public EraPosition Position;
        public Tone Tone;
        public int Weight = 1;
        /// <summary>Era names; slot {word}.</summary>
        public List<string> NamePatterns = new List<string>();
        public List<string> Words = new List<string>();
        /// <summary>Slots: {culture}, {founder}, {place}, {word}.</summary>
        public List<string> Summaries = new List<string>();
        public List<string> Echoes = new List<string>();
    }

    /// <summary>prehistory.json: era templates plus the text pools for founders, artifacts and figures.</summary>
    public sealed class PrehistoryRules
    {
        public List<EraTemplate> Eras = new List<EraTemplate>();
        /// <summary>Slots: {founder}, {culture}, {place}, {craft}, {form}, {value}.</summary>
        public List<string> FounderSummaries = new List<string>();
        /// <summary>Slots: {culture}, {form}, {place}, {era}, {maker}.</summary>
        public List<string> ArtifactSummaries = new List<string>();
        public List<string> BrightEpithets = new List<string>();
        public List<string> SomberEpithets = new List<string>();
    }
}
