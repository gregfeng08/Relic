using System;
using System.Collections.Generic;

namespace Relic.Items
{
    /// <summary>What a material physically is. Decides which bases it can make (a sword is metal).</summary>
    public enum MaterialKind
    {
        Metal,
        Wood,
        Leather,
        Cloth,
        Paper,
        Stone,
        Glass,
        Bone,
        Clay,
        Gem,
        /// <summary>A finish or trim (lacquer, wax, dye, straw): never the body of an item, only its accent.</summary>
        Accent,
    }

    /// <summary>
    /// One material (17-item-grammar, "Materials"): its kind, the first tier it turns up at,
    /// its look (tint, glow, how it wears) and one small property tag, a tiny stat line.
    /// Items-side table: the Lore Bible lists material names per culture and motif, and a
    /// test checks every one of them is described here.
    /// </summary>
    public sealed class MaterialDef
    {
        public string Name;
        public MaterialKind Kind;
        public int TierMin;
        /// <summary>Base color "#RRGGBB".</summary>
        public string Tint;
        /// <summary>0–1: how much it glows on its own (glass, amber).</summary>
        public float Glow;
        /// <summary>Added to the quality's wear (soft materials show age; stone and glass don't).</summary>
        public float WearBias;
        /// <summary>The property tag's stat line.</summary>
        public ItemStatKind Tag;
        /// <summary>The tag in words, for samples and tooltips: "rings true".</summary>
        public string TagText;

        public MaterialDef(string name, MaterialKind kind, int tierMin, string tint, ItemStatKind tag, string tagText, float glow = 0f, float wear = 0f)
        {
            Name = name; Kind = kind; TierMin = tierMin; Tint = tint; Tag = tag; TagText = tagText; Glow = glow; WearBias = wear;
        }
    }

    public static class Materials
    {
        static MaterialDef M(string n, MaterialKind k, int t, string tint, ItemStatKind tag, string text, float glow = 0f, float wear = 0f) =>
            new MaterialDef(n, k, t, tint, tag, text, glow, wear);

        const MaterialKind Metal = MaterialKind.Metal, Wood = MaterialKind.Wood, Leather = MaterialKind.Leather, Cloth = MaterialKind.Cloth,
            Paper = MaterialKind.Paper, Stone = MaterialKind.Stone, Glass = MaterialKind.Glass, Bone = MaterialKind.Bone,
            Clay = MaterialKind.Clay, Gem = MaterialKind.Gem, Accent = MaterialKind.Accent;
        const ItemStatKind Resist = ItemStatKind.StatusResist, Stamina = ItemStatKind.MaxStamina, Move = ItemStatKind.MoveSpeed,
            Regen = ItemStatKind.ManaRegen, Hp = ItemStatKind.MaxHealth, Ins = ItemStatKind.Insight, Vit = ItemStatKind.Vitality,
            Chr = ItemStatKind.Charisma, Def = ItemStatKind.Defense;

        /// <summary>Every known material. Generic ones (iron, ash, wool...) fill gaps when a culture has nothing that fits a base.</summary>
        public static readonly MaterialDef[] All =
        {
            // metals
            M("iron", Metal, 0, "#6E6E73", Resist, "honest and hard to dent"),
            M("bronze", Metal, 0, "#A87B3E", Resist, "warm and forgiving"),
            M("copper", Metal, 0, "#B8693D", Regen, "carries a charge like a kettle carries heat"),
            M("red copper", Metal, 1, "#B5533A", Regen, "warm to the touch"),
            M("green copper", Metal, 1, "#5E9A7A", Resist, "weathered green and stubborn", wear: 0.1f),
            M("brass", Metal, 1, "#C9A44A", Chr, "polished bright enough to smile at"),
            M("mirror-brass", Metal, 4, "#E0C46A", Chr, "throws the light around", glow: 0.2f),
            M("pewter", Metal, 1, "#8E9399", Vit, "soft, homely and hard to lose"),
            M("oak and pewter", Metal, 2, "#8C8270", Vit, "kitchen-sturdy"),
            M("bell-bronze", Metal, 2, "#B88A3C", Chr, "rings true"),
            M("steel", Metal, 2, "#9AA1A8", Resist, "keeps its edge"),
            M("tempered steel", Metal, 4, "#8F99A6", Resist, "springs back"),
            M("silver", Metal, 4, "#D9DDE3", Ins, "spirits show a little clearer near it", glow: 0.1f),
            M("silver wire", Accent, 3, "#D9DDE3", Ins, "fine silver wire"),
            M("rime-iron", Metal, 5, "#9FB8C8", Resist, "never quite warms", glow: 0.1f),
            M("blued steel", Metal, 6, "#4D5D7A", Resist, "takes a blow and shrugs"),
            M("star-iron", Metal, 8, "#5A6378", Ins, "fell out of the sky, they say", glow: 0.2f),
            M("riveted plate", Metal, 6, "#7D8189", Def, "riveted the old way"),
            M("construct iron", Metal, 7, "#55585E", Def, "made for an old construct; it still stands to attention", wear: 0.15f),
            // woods
            M("ash", Wood, 0, "#C8B08A", Stamina, "light in the hand"),
            M("oak", Wood, 0, "#9C7A4E", Hp, "solid as a door"),
            M("willow", Wood, 0, "#B7A574", Stamina, "bends rather than breaks"),
            M("birch", Wood, 0, "#E0D6C2", Stamina, "pale and springy"),
            M("driftwood", Wood, 0, "#A89C8A", Stamina, "salt-seasoned and light", wear: 0.15f),
            M("reed-cane", Wood, 0, "#C9B77A", Move, "hollow and quick"),
            M("hawthorn wood", Wood, 1, "#8E6B4A", Resist, "knotty and stubborn"),
            M("yew", Wood, 2, "#A0603A", Stamina, "a bowyer's favorite"),
            M("bog-oak", Wood, 3, "#3F3428", Hp, "black from a thousand years in the marsh"),
            M("mountain cedar", Wood, 3, "#A8693E", Resist, "smells of the high passes"),
            M("living willow", Wood, 3, "#8FAE6A", Vit, "still putting out leaves now and then", glow: 0.05f),
            M("lute-wood", Wood, 3, "#B5824C", Chr, "hums when you set it down"),
            M("amberwood", Wood, 4, "#C98A3A", Vit, "warm-grained, with sunlight caught in it", glow: 0.1f),
            M("ironwood", Wood, 5, "#5E4632", Hp, "too hard for most saws"),
            M("heartwood", Wood, 8, "#7A3F2A", Vit, "cut from the middle of a very old tree"),
            // leathers, cloths, papers
            M("hide", Leather, 0, "#8A6A4A", Stamina, "plain and dependable", wear: 0.1f),
            M("boiled leather", Leather, 1, "#7A5636", Stamina, "stiff where it counts", wear: 0.1f),
            M("oxhide", Leather, 3, "#6E4B30", Hp, "thick as a farmer's opinion", wear: 0.1f),
            M("sealskin", Leather, 5, "#4E4A48", Resist, "sheds rain and spray"),
            M("elkhide", Leather, 7, "#7D5C3E", Stamina, "soft, tough and quiet"),
            M("wool", Cloth, 0, "#D8CDB8", Resist, "keeps the weather out", wear: 0.15f),
            M("felted wool", Cloth, 1, "#C9BBA0", Resist, "thick as a winter blanket", wear: 0.1f),
            M("dune-goat wool", Cloth, 2, "#D9C29A", Resist, "cool by day, warm by night"),
            M("sailcloth", Cloth, 1, "#E6DDC8", Move, "catches the wind"),
            M("kite-silk", Cloth, 3, "#F0E6D2", Move, "wants to fly"),
            M("moth-silk", Cloth, 4, "#EDE3F0", Move, "light as a moth's wing", glow: 0.05f),
            M("quilted cotton", Cloth, 2, "#E2D8C6", Hp, "every quilt square sewn by someone different", wear: 0.1f),
            M("parchment", Paper, 0, "#E8DDB8", Regen, "takes ink well", wear: 0.1f),
            M("oiled paper", Paper, 0, "#E6D29A", Regen, "glows when a lamp is behind it", glow: 0.1f),
            M("vellum", Paper, 2, "#EFE4C8", Regen, "remembers every word written on it"),
            M("birch bark", Paper, 1, "#E8E0CE", Regen, "peels into pages"),
            // stone, glass, gems, bone, clay
            M("river stone", Stone, 0, "#8D8F87", Hp, "smoothed by a hundred years of river"),
            M("flint", Stone, 0, "#6C6A66", Resist, "strikes a spark"),
            M("slate", Stone, 0, "#5C6470", Hp, "cool and flat"),
            M("granite", Stone, 1, "#A09A92", Hp, "stubborn as a mountain"),
            M("sandstone", Stone, 0, "#D2A878", Hp, "warm from the sun"),
            M("white stone", Stone, 1, "#E8E6E0", Chr, "bright as a harbor wall"),
            M("kiln brick", Stone, 1, "#A2533A", Hp, "holds its heat for hours"),
            M("lodestone", Stone, 4, "#4A4A52", Ins, "always knows north"),
            M("sea-glass", Glass, 1, "#8FD1C9", Regen, "frosted by the tide", glow: 0.2f),
            M("frosted glass", Glass, 2, "#D6E4EA", Ins, "a little foggy, like early morning", glow: 0.2f),
            M("lamp-glass", Glass, 2, "#F2E2A8", Ins, "made to hold a flame", glow: 0.4f),
            M("blue quartz", Gem, 3, "#7FA6D8", Ins, "clear as a cold morning", glow: 0.3f),
            M("sun-glass", Glass, 5, "#F7C65A", Ins, "holds the noon light a while", glow: 0.5f),
            M("star-glass", Glass, 7, "#BFD7FF", Ins, "speckled like a night sky", glow: 0.6f),
            M("amber", Gem, 2, "#D99A2B", Vit, "a little sunlight that never left", glow: 0.3f),
            M("honey-amber", Gem, 3, "#E6A93A", Vit, "smells faintly of the hive", glow: 0.3f),
            M("river pearl", Gem, 3, "#EEE8DE", Chr, "lustrous and lucky", glow: 0.1f),
            M("coral", Gem, 2, "#E87A6A", Regen, "grown, not made"),
            M("whalebone", Bone, 1, "#E6DCC4", Vit, "carved through a long winter"),
            M("reindeer antler", Bone, 0, "#D8C8A8", Vit, "shed in spring and kept"),
            M("carved horn", Bone, 1, "#B89A70", Chr, "carries a tune"),
            M("fired clay", Clay, 0, "#B86B45", Hp, "fired hard and true"),
            M("red clay", Clay, 0, "#A8553A", Hp, "dug from the riverbank"),
            M("river clay", Clay, 0, "#9C8A6E", Hp, "smooth and grey"),
            M("ochre clay", Clay, 1, "#C98A3E", Hp, "the color of a desert noon"),
            M("glazed clay", Clay, 2, "#5E9AB8", Chr, "glazed in a cheerful blue"),
            M("porcelain", Clay, 3, "#F2F0EA", Chr, "thin enough to see light through", glow: 0.1f),
            // accents: finishes, trims and fillings, never the body of an item
            M("red lacquer", Accent, 1, "#B0302A", Resist, "lacquered red"),
            M("beeswax", Accent, 0, "#E8C66A", Resist, "waxed against the weather"),
            M("ochre dye", Accent, 0, "#C98A3E", Chr, "dyed ochre"),
            M("salt", Accent, 0, "#EDE8DC", Resist, "salt-cured"),
            M("wheat straw", Accent, 0, "#E2C77A", Chr, "bound with plaited straw"),
            M("cattail down", Accent, 0, "#D9CDB0", Hp, "padded with cattail down"),
            M("reeds", Accent, 0, "#B7A86A", Move, "wrapped in woven reeds"),
            M("gourd", Accent, 0, "#C9A85A", Hp, "fitted with a dried gourd"),
            M("wicker", Accent, 0, "#C2A270", Move, "wicker-wrapped"),
            M("woven briar", Accent, 1, "#6E7A3A", Resist, "wrapped in woven briar"),
            M("pressed petals", Accent, 0, "#F2A6C4", Chr, "set with pressed petals"),
            M("oak-gall ink", Accent, 0, "#2E2A3A", Regen, "inked with oak-gall"),
            M("goose quill", Accent, 0, "#EEE8DC", Regen, "fletched with goose quill"),
        };

        static readonly Dictionary<string, MaterialDef> ByName = Build();

        static Dictionary<string, MaterialDef> Build()
        {
            var d = new Dictionary<string, MaterialDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in All) d[m.Name] = m;
            return d;
        }

        public static MaterialDef Find(string name) => name != null && ByName.TryGetValue(name, out var m) ? m : null;

        /// <summary>
        /// What a base can be made of (17: "grounded: a sword is metal, a tome is paper and leather").
        /// Relics take their kinds from their form (Forms.RelicForm.Kinds).
        /// </summary>
        public static MaterialKind[] KindsFor(BaseType t)
        {
            switch (t)
            {
                case BaseType.Sword:
                case BaseType.Greatsword:
                case BaseType.HeavyArmor: return new[] { Metal };
                case BaseType.Bow:
                case BaseType.Staff: return new[] { Wood, Bone };
                case BaseType.Tome: return new[] { Paper, Leather };
                case BaseType.LightArmor: return new[] { Cloth, Leather };
                case BaseType.MediumArmor: return new[] { Leather, Metal };
                case BaseType.Trinket: return new[] { Metal, Stone, Glass, Gem, Bone, Wood, Clay };
                default: return new[] { Metal, Glass, Wood, Stone, Gem, Bone, Clay };
            }
        }

        /// <summary>Generic materials of the given kinds that a tier can have, for bases a culture doesn't supply.</summary>
        public static List<MaterialDef> Generic(MaterialKind[] kinds, int tier)
        {
            var list = new List<MaterialDef>();
            foreach (var m in All)
                if (Array.IndexOf(kinds, m.Kind) >= 0 && m.TierMin <= tier && m.TierMin >= tier - 4) list.Add(m);
            if (list.Count == 0)
                foreach (var m in All)
                    if (Array.IndexOf(kinds, m.Kind) >= 0 && m.TierMin <= tier) list.Add(m);
            return list;
        }
    }
}
