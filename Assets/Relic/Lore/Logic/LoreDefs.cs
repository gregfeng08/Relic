using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Stats;

namespace Relic.Lore
{
    /// <summary>A kind of group the world graph can instantiate (factions.json).</summary>
    public sealed class FactionArchetype
    {
        public string Id;
        public FactionKind Kind;
        public Tone Tone;
        /// <summary>Plain description for prompts and docs: "a guild of mapmakers".</summary>
        public string Label;
        /// <summary>Patterns with slots: {place}, {founder}, {culture}, {word}.</summary>
        public List<string> NamePatterns = new List<string>();
        public List<string> Goals = new List<string>();
        public List<string> Symbols = new List<string>();
        /// <summary>Archetype ids this one works well with.</summary>
        public List<string> Allies = new List<string>();
        /// <summary>Friendly rivals: competition, bets, races, never bloodshed.</summary>
        public List<string> Rivals = new List<string>();
        /// <summary>Real opponents (rare; mostly the somber archetypes).</summary>
        public List<string> Opposes = new List<string>();
        /// <summary>Relative pick weight.</summary>
        public int Weight = 1;
    }

    public sealed class TemplateSlot
    {
        public string Name;
        public SlotKind Kind;
        /// <summary>Suggested values for Word slots; when empty, the culture's vocabulary is used.</summary>
        public List<string> Words = new List<string>();
    }

    /// <summary>A kind of historical event (events.json). The world graph fills the slots.</summary>
    public sealed class EventTemplate
    {
        public string Id;
        public EventKind Kind;
        public Tone Tone;
        /// <summary>Relative pick weight; disasters and wars are low.</summary>
        public int Weight = 1;
        /// <summary>Era ids where it can happen (eras.json).</summary>
        public List<string> Eras = new List<string>();
        public List<TemplateSlot> Slots = new List<TemplateSlot>();
        /// <summary>Event names, e.g. "The Founding of {place}". Only declared slots may appear.</summary>
        public List<string> NamePatterns = new List<string>();
        /// <summary>A one-sentence summary for prompts and the Reliquary.</summary>
        public string Summary;
        /// <summary>What the event leaves in the world for other systems to place.</summary>
        public List<EventTrace> Leaves = new List<EventTrace>();
    }

    /// <summary>The physical half of a theme (motifs.json). Maps to statuses (03) and shapes (17).</summary>
    public sealed class MotifDef
    {
        public string Id;
        /// <summary>Nouns for names and lore.</summary>
        public List<string> Words = new List<string>();
        public List<StatusId> Statuses = new List<StatusId>();
        public List<MotifShape> Shapes = new List<MotifShape>();
        /// <summary>Tones this motif can carry. Most motifs carry both.</summary>
        public List<Tone> Tones = new List<Tone>();
        /// <summary>Mood ids that suit it best.</summary>
        public List<string> Moods = new List<string>();
        public StatId FavoredStat;
        /// <summary>Emissive color "#RRGGBB".</summary>
        public string Color;
        public List<string> Materials = new List<string>();
        public List<LoreFamily> Families = new List<LoreFamily>();
    }

    /// <summary>The human half of a theme: the story an item or place carries.</summary>
    public sealed class MoodDef
    {
        public string Id;
        public Tone Tone;
        /// <summary>Relative weight within its tone.</summary>
        public int Weight = 1;
        /// <summary>Name modifiers ("Homeward", "Festival").</summary>
        public List<string> Words = new List<string>();
        /// <summary>How effects lean (17): "ally", "aura", "discovery", "drawback"...</summary>
        public List<string> EffectLean = new List<string>();
        /// <summary>Template-mode lore lines with slots: {maker}, {place}, {culture}, {form}, {word}, {legend}, {faction}.</summary>
        public List<string> Lines = new List<string>();
    }

    /// <summary>moods.json: the mood list plus the share of Bright picks.</summary>
    public sealed class MoodTable
    {
        public double BrightShare = 0.7;
        public List<MoodDef> Moods = new List<MoodDef>();
    }

    public sealed class EraDef
    {
        public string Id;
        public string Name;
        /// <summary>0 = oldest.</summary>
        public int Order;
        public int StartYearsAgo;
        public int EndYearsAgo;
        public string Summary;
        /// <summary>What every region can echo from this era: ruins, customs, relic kinds.</summary>
        public List<string> Echoes = new List<string>();
    }

    /// <summary>A famous name that may be referenced anywhere (legends.json).</summary>
    public sealed class LegendDef
    {
        public string Id;
        public LegendKind Kind;
        public string Name;
        public string Epithet;
        /// <summary>Culture id, or null for pan-cultural legends.</summary>
        public string Culture;
        public string Era;
        public Tone Tone;
        public string Summary;
        public List<string> Motifs = new List<string>();
        /// <summary>Other legend ids this one is tied to.</summary>
        public List<string> Related = new List<string>();
    }

    /// <summary>style.json: hard rules every generated name and line must pass.</summary>
    public sealed class StyleRules
    {
        /// <summary>Whole words or phrases banned from narration (case-insensitive).</summary>
        public List<string> BannedWords = new List<string>();
        /// <summary>Substrings banned inside generated proper names (rude or off-tone fragments).</summary>
        public List<string> BannedNameSubstrings = new List<string>();
        public int MaxSentenceWords = 22;
        public int MaxLoreSentences = 3;
    }
}
