using System.Collections.Generic;
using Relic.Core.Effects;

namespace Relic.Items
{
    // The item model (docs/design/08-items-bosses.md). Plain classes with public
    // fields so Unity's C# 9 compiler, Newtonsoft and the dotnet harness all accept
    // them. Serialized with ItemJson (camelCase, enums as strings).

    public enum BaseType
    {
        Sword,
        Greatsword,
        Bow,
        Staff,
        Tome,
        LightArmor,
        MediumArmor,
        HeavyArmor,
        Trinket,
        Relic,
    }

    public enum ItemSlot
    {
        Weapon,
        Relic,
        Armor,
        Trinket,
    }

    /// <summary>
    /// What a stat line on an item modifies. The trained stats mirror Core.Stats.StatId;
    /// the rest are derived numbers (weapon damage, attack speed, ...). See ItemStats.CoreStat.
    /// </summary>
    public enum ItemStatKind
    {
        // Weapon
        Damage,         // points per basic-attack hit (10 = starter hit at tier 0)
        AttackSpeed,    // basic attacks per second
        CritChance,     // percent
        SkillPower,     // percent bonus to the weapon skill
        // Armor and pools
        Defense,        // flat DEF (Core StatId.DEF)
        MaxHealth,
        MaxMana,
        MaxStamina,
        MoveSpeed,      // percent
        ManaRegen,      // mana per second
        StatusResist,   // percent shorter statuses
        // Relic
        ResonanceGain,  // percent faster ultimate charge
        // Trained-stat bonuses (Core StatId)
        Strength,
        Agility,
        Vitality,
        Intelligence,
        Insight,
        Charisma,
    }

    public sealed class ItemStat
    {
        public ItemStatKind Stat;
        /// <summary>Final rolled value (includes overroll steps).</summary>
        public float Value;
        /// <summary>Lower end of the normal range.</summary>
        public float Min;
        /// <summary>Top of the normal range. Overrolls go past it in +5% steps.</summary>
        public float Ceiling;
        /// <summary>Steps past the ceiling (0 = a normal roll). Hidden cap 10.</summary>
        public int OverrollSteps;
    }

    public sealed class ItemLore
    {
        /// <summary>One line (Rare items).</summary>
        public string Flavor;
        /// <summary>2–4 sentence history paragraph (Epic and Legendary).</summary>
        public string History;
        /// <summary>Ids of world-context entities the lore refers to (places, factions, people, events).</summary>
        public List<string> WorldRefs = new List<string>();
    }

    /// <summary>Parameters for the kitbash renderer (renderer itself is out of scope).</summary>
    public sealed class ItemVisual
    {
        public List<string> KitParts = new List<string>();
        public string Material;
        /// <summary>0 = pristine, 1 = falling apart.</summary>
        public float Wear;
        /// <summary>"#RRGGBB".</summary>
        public string EmissiveColor;
        public string Decal;
        /// <summary>0–4.</summary>
        public float EmissiveIntensity;
    }

    public enum AwakeningState
    {
        Dormant,
        Stirring,
        Awakened,
    }

    /// <summary>Relic memory (data only for now; the runtime that fills it comes later).</summary>
    public sealed class ItemMemory
    {
        public Dictionary<string, int> KillsByEnemyType = new Dictionary<string, int>();
        public int BossesKilled;
        public List<string> PlacesVisited = new List<string>();
        public AwakeningState Awakening = AwakeningState.Dormant;
        public float AwakeningProgress;
    }

    public enum GenerationMode
    {
        /// <summary>Tables only (Common/Uncommon, and everything non-text).</summary>
        Tables,
        /// <summary>Text written by the deterministic TemplateGenerator (no LLM).</summary>
        Template,
        /// <summary>Text written by an LLM, validated, persisted.</summary>
        Llm,
        /// <summary>The LLM failed every attempt; a template relic was used instead.</summary>
        LlmFallback,
    }

    public sealed class GeneratorInfo
    {
        public string Name;
        public int Version;
        public GenerationMode Mode;
        /// <summary>How many text-generation attempts were made (0 for table-only items).</summary>
        public int Attempts;
        /// <summary>Factor ScaleToFit applied to the LLM's program (1 = it fit as written).</summary>
        public float ScaleApplied = 1f;
        /// <summary>Model or template identifier that wrote the text, when there is one.</summary>
        public string TextSource;
    }

    public sealed class Item
    {
        /// <summary>Stable id from the seed and generation index.</summary>
        public string Id;
        /// <summary>The seed every deterministic roll of this item derives from.</summary>
        public ulong Seed;
        /// <summary>Forge rerolls applied (0 = the original roll).</summary>
        public int RerollCount;
        public string Name;
        public BaseType BaseType;
        public ItemSlot Slot;
        public Rarity Rarity;
        public int Tier;
        /// <summary>Seeded theme, e.g. "tide · betrayal" (Rare and up).</summary>
        public string Theme;
        /// <summary>Relics: the physical object (lantern, bell, ...).</summary>
        public string Form;
        public List<ItemStat> Stats = new List<ItemStat>();
        /// <summary>Weapon skill, relic enchantments and ultimate, armor/trinket passives.</summary>
        public EffectProgram Effects = new EffectProgram();
        public ItemLore Lore;
        public ItemVisual Visual = new ItemVisual();
        public ItemMemory Memory;
        public GeneratorInfo Generator = new GeneratorInfo();
    }

    public static class ItemSlots
    {
        public static ItemSlot For(BaseType t)
        {
            switch (t)
            {
                case BaseType.LightArmor:
                case BaseType.MediumArmor:
                case BaseType.HeavyArmor: return ItemSlot.Armor;
                case BaseType.Trinket: return ItemSlot.Trinket;
                case BaseType.Relic: return ItemSlot.Relic;
                default: return ItemSlot.Weapon;
            }
        }

        public static bool IsWeapon(BaseType t) => For(t) == ItemSlot.Weapon;
    }
}
