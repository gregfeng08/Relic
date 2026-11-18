using System;
using Relic.Core.Effects;

namespace Relic.Items
{
    /// <summary>Which parts of the item sentence an item shows (17: "Layers by rarity").</summary>
    [Flags]
    public enum GrammarLayer
    {
        None = 0,
        Base = 1 << 0,
        Material = 1 << 1,
        Quality = 1 << 2,
        /// <summary>Uncommon: one themed stat line from the archetype.</summary>
        Archetype = 1 << 3,
        /// <summary>Rare: mood + motif drive the effects and the look.</summary>
        Theme = 1 << 4,
        /// <summary>Rare: a real name from the culture's naming patterns.</summary>
        Name = 1 << 5,
        /// <summary>Epic: the culture and its maker are named.</summary>
        Culture = 1 << 6,
        Maker = 1 << 7,
        /// <summary>Epic: a short lore line.</summary>
        Lore = 1 << 8,
        /// <summary>Epic: one written enchantment.</summary>
        Enchantment = 1 << 9,
        /// <summary>Legendary: references to the world and the Bible (places, legends, eras).</summary>
        WorldRefs = 1 << 10,
        /// <summary>Legendary: the lore points somewhere else.</summary>
        Leads = 1 << 11,
        Ultimate = 1 << 12,
    }

    /// <summary>
    /// The item "sentence" (17-item-grammar): Base · Archetype · Culture/Maker · Material ·
    /// Theme · Quality · Name, resolved before anything else. Stats, effects, the look, the
    /// name and the lore all read it. Every field is filled for every item; <see cref="Layers"/>
    /// says which of them the item's rarity actually shows.
    /// </summary>
    public sealed class ItemSentence
    {
        public BaseType Base;
        /// <summary>Archetype id (Archetypes.All): "duelist", "warden"...</summary>
        public string Archetype;
        /// <summary>Lore Bible culture id and its adjective ("Aruvi").</summary>
        public string Culture;
        public string CultureAdjective;
        /// <summary>The noun the item is named with: a culture form ("carved longbow") or the archetype's plain noun ("sabre").</summary>
        public string Form;
        /// <summary>True when <see cref="Form"/> is one of the culture's own item forms.</summary>
        public bool CultureForm;
        /// <summary>Relics: the kitbash form (Forms.RelicForms) the culture form is built from.</summary>
        public string Kit;
        /// <summary>The maker, named in the culture's phonology, and where they worked (a world region).</summary>
        public string Maker;
        public string MakerHomeId;
        public string MakerHome;
        public string MakersMark;
        /// <summary>The body material (Materials.All) and an optional finish or trim from the culture.</summary>
        public string Material;
        public string Accent;
        /// <summary>The material's property tag in words ("rings true") and its stat line.</summary>
        public string MaterialTag;
        public ItemStatKind MaterialStat;
        /// <summary>Mood (Lore moods.json), its tone ("Bright"/"Somber") and the motif (motifs.json).</summary>
        public string Mood;
        public string Tone;
        public string Motif;
        /// <summary>"Worn", "Fine", "Masterwork"...</summary>
        public string Quality;
        /// <summary>The culture naming pattern the name was built from, once named ("{maker}'s {form}").</summary>
        public string NamePattern;
        /// <summary>The world region the item was found in.</summary>
        public string RegionId;
        public GrammarLayer Layers;
        /// <summary>Name of the armor/trinket table passive, if any (not an enchantment, so the archetype trigger share ignores it).</summary>
        public string SlotPassive;

        public bool Has(GrammarLayer layer) => (Layers & layer) == layer;
        public bool IsSomber => Tone == "Somber";

        /// <summary>The last word of the form ("Longbow" for "carved longbow"): every name keeps it.</summary>
        public string HeadNoun
        {
            get
            {
                if (string.IsNullOrEmpty(Form)) return Base.ToString();
                var parts = Form.Split(' ');
                return parts[parts.Length - 1];
            }
        }

        /// <summary>The layers each rarity shows (17, "Layers by rarity").</summary>
        public static GrammarLayer LayersFor(Rarity r)
        {
            var l = GrammarLayer.Base | GrammarLayer.Material | GrammarLayer.Quality;
            if (r >= Rarity.Uncommon) l |= GrammarLayer.Archetype;
            if (r >= Rarity.Rare) l |= GrammarLayer.Theme | GrammarLayer.Name;
            if (r >= Rarity.Epic) l |= GrammarLayer.Culture | GrammarLayer.Maker | GrammarLayer.Lore | GrammarLayer.Enchantment;
            if (r >= Rarity.Legendary) l |= GrammarLayer.WorldRefs | GrammarLayer.Leads | GrammarLayer.Ultimate;
            return l;
        }

        /// <summary>One line for samples and debugging: "Duelist · Aruvi (Lirue of Iravi Glade) · amberwood · wonder/lantern · Fine".</summary>
        public override string ToString()
        {
            string culture = CultureAdjective + (Has(GrammarLayer.Maker) ? " (" + Maker + " of " + MakerHome + ")" : "");
            string theme = Has(GrammarLayer.Theme) ? " · " + Mood + "/" + Motif : "";
            return Archetypes.ById(Archetype)?.Label + " · " + culture + " · " + Material + theme + " · " + Quality;
        }
    }
}
