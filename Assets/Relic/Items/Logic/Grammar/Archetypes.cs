using System;
using System.Collections.Generic;
using Relic.Core.Effects;
using Relic.Core.Random;

namespace Relic.Items
{
    /// <summary>
    /// An item's purpose (17-item-grammar): which stat lines fit it, which triggers its
    /// effects use, which statuses it favors when it has no theme, and what its bases are
    /// called. Data rows, not code paths: everything downstream reads these fields.
    /// </summary>
    public sealed class ArchetypeDef
    {
        public string Id;
        /// <summary>Display name: "Duelist".</summary>
        public string Label;
        /// <summary>One line: what it feels like.</summary>
        public string Feel;
        /// <summary>Favored stat lines, best first. The first one the base allows is the item's archetype line.</summary>
        public ItemStatKind[] StatLines;
        /// <summary>The trained stat a trinket of this archetype carries as its main line.</summary>
        public ItemStatKind Trained;
        /// <summary>Triggers its enchantments use about 80% of the time.</summary>
        public TriggerKind[] Triggers;
        /// <summary>Enchantment template ids built on those triggers (EffectTemplates.Enchantments).</summary>
        public string[] Templates;
        /// <summary>Statuses it inflicts when the item has no theme yet (and alongside the motif's).</summary>
        public StatusId[] Statuses;
        /// <summary>Pick weight per base type (index = (int)BaseType). 0 = never.</summary>
        public int[] Weights;

        public bool Favors(TriggerKind t) => Array.IndexOf(Triggers, t) >= 0;
        public bool FavorsTemplate(string id) => Array.IndexOf(Templates, id) >= 0;
        public bool FavorsLine(ItemStatKind k) => Array.IndexOf(StatLines, k) >= 0;
    }

    public static class Archetypes
    {
        //                         Sword Great Bow Staff Tome Light Medium Heavy Trinket Relic
        public static readonly ArchetypeDef[] All =
        {
            new ArchetypeDef
            {
                Id = "duelist", Label = "Duelist", Feel = "precise, quick",
                StatLines = new[] { ItemStatKind.Agility, ItemStatKind.CritChance, ItemStatKind.SkillPower, ItemStatKind.MoveSpeed },
                Trained = ItemStatKind.Agility,
                Triggers = new[] { TriggerKind.OnHit, TriggerKind.OnCloseCall },
                Templates = new[] { "rider", "exploit", "riposte", "executioner" },
                Statuses = new[] { StatusId.Bleeding, StatusId.Staggered },
                Weights = new[] { 5, 1, 1, 0, 0, 2, 1, 0, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "warden", Label = "Warden", Feel = "protective",
                StatLines = new[] { ItemStatKind.Vitality, ItemStatKind.MaxHealth, ItemStatKind.Defense, ItemStatKind.StatusResist },
                Trained = ItemStatKind.Vitality,
                Triggers = new[] { TriggerKind.OnTakeDamage, TriggerKind.Passive, TriggerKind.OnLowHealth },
                Templates = new[] { "retort", "aura", "refuge", "temper" },
                Statuses = new[] { StatusId.Rooted },
                Weights = new[] { 1, 2, 0, 1, 0, 0, 2, 4, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "hunter", Label = "Hunter", Feel = "patient, ranged",
                StatLines = new[] { ItemStatKind.Agility, ItemStatKind.CritChance, ItemStatKind.MaxStamina, ItemStatKind.MoveSpeed },
                Trained = ItemStatKind.Agility,
                Triggers = new[] { TriggerKind.OnKill, TriggerKind.OnHit },
                Templates = new[] { "longshot", "death-burst", "bosshunter" },
                Statuses = new[] { StatusId.Chilled, StatusId.Bleeding },
                Weights = new[] { 1, 0, 5, 0, 0, 2, 2, 0, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "scholar", Label = "Scholar", Feel = "clever, studious",
                StatLines = new[] { ItemStatKind.Intelligence, ItemStatKind.Insight, ItemStatKind.MaxMana, ItemStatKind.ManaRegen },
                Trained = ItemStatKind.Intelligence,
                Triggers = new[] { TriggerKind.Periodic, TriggerKind.OnStatusApplied, TriggerKind.Active },
                Templates = new[] { "sentinel", "echo" },
                Statuses = new[] { StatusId.Soaked, StatusId.Dazed, StatusId.Silenced },
                Weights = new[] { 0, 0, 0, 5, 5, 1, 0, 0, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "wanderer", Label = "Wanderer", Feel = "hardy, made for the road",
                StatLines = new[] { ItemStatKind.MaxStamina, ItemStatKind.MoveSpeed, ItemStatKind.Insight, ItemStatKind.Vitality },
                Trained = ItemStatKind.Insight,
                Triggers = new[] { TriggerKind.OnDash, TriggerKind.OnLowHealth },
                Templates = new[] { "trail", "tailwind", "refuge" },
                Statuses = new StatusId[0],
                Weights = new[] { 1, 0, 2, 2, 0, 3, 2, 0, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "smith", Label = "Smith", Feel = "sturdy, well made",
                StatLines = new[] { ItemStatKind.Strength, ItemStatKind.Defense, ItemStatKind.SkillPower, ItemStatKind.MaxHealth },
                Trained = ItemStatKind.Strength,
                Triggers = new[] { TriggerKind.OnHit, TriggerKind.OnKill, TriggerKind.Passive },
                Templates = new[] { "exploit", "executioner", "temper" },
                Statuses = new[] { StatusId.Staggered, StatusId.Burning },
                Weights = new[] { 1, 4, 0, 0, 0, 0, 1, 3, 1, 1 },
            },
            new ArchetypeDef
            {
                Id = "herald", Label = "Herald", Feel = "social, good with a crowd",
                StatLines = new[] { ItemStatKind.Charisma, ItemStatKind.StatusResist, ItemStatKind.MaxMana, ItemStatKind.Vitality },
                Trained = ItemStatKind.Charisma,
                Triggers = new[] { TriggerKind.Passive, TriggerKind.Periodic },
                Templates = new[] { "rally", "hearth", "aura" },
                Statuses = new[] { StatusId.Dazed },
                Weights = new[] { 1, 1, 0, 2, 2, 1, 1, 0, 1, 1 },
            },
        };

        public static ArchetypeDef ById(string id) => Array.Find(All, a => a.Id == id);

        /// <summary>The archetype for a base type, weighted by the table above (purpose "grammar.archetype").</summary>
        public static ArchetypeDef Pick(BaseType t, ulong itemSeed)
        {
            var rng = Pcg32.ForPurpose(itemSeed, "grammar.archetype");
            int total = 0;
            foreach (var a in All) total += a.Weights[(int)t];
            int r = rng.Range(0, Math.Max(1, total));
            foreach (var a in All)
            {
                r -= a.Weights[(int)t];
                if (r < 0) return a;
            }
            return All[0];
        }

        // ---------- coherence rules that hold for every archetype ----------

        /// <summary>
        /// Stat kinds a base never carries (17: "a staff never rolls STR"; "heavy armor never
        /// gets AGI bonuses"). Applies to stat lines and to stat effects on the wearer.
        /// </summary>
        public static bool Forbids(BaseType t, ItemStatKind k)
        {
            switch (t)
            {
                case BaseType.Staff:
                case BaseType.Tome: return k == ItemStatKind.Strength;
                case BaseType.HeavyArmor: return k == ItemStatKind.Agility;
                default: return false;
            }
        }

        // ---------- what the base is called, by purpose ----------

        static readonly Dictionary<string, string[]> Nouns = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["duelist.Sword"] = new[] { "Sabre", "Rapier", "Arming Sword" },
            ["warden.Sword"] = new[] { "Broadsword", "Arming Sword" },
            ["hunter.Sword"] = new[] { "Hunting Knife", "Hanger" },
            ["wanderer.Sword"] = new[] { "Long Knife", "Hanger" },
            ["smith.Sword"] = new[] { "Falchion", "Broadsword" },
            ["herald.Sword"] = new[] { "Court Sword", "Sabre" },
            ["smith.Greatsword"] = new[] { "Cleaver", "Two-Hander", "Greatsword" },
            ["warden.Greatsword"] = new[] { "Greatsword", "Two-Hander" },
            ["duelist.Greatsword"] = new[] { "Longsword" },
            ["herald.Greatsword"] = new[] { "Greatsword" },
            ["hunter.Bow"] = new[] { "Longbow", "Hunting Bow", "Recurve" },
            ["wanderer.Bow"] = new[] { "Short Bow", "Recurve" },
            ["duelist.Bow"] = new[] { "Recurve" },
            ["scholar.Staff"] = new[] { "Staff", "Rod" },
            ["wanderer.Staff"] = new[] { "Walking Staff", "Quarterstaff" },
            ["herald.Staff"] = new[] { "Banner Staff", "Crook" },
            ["warden.Staff"] = new[] { "Quarterstaff" },
            ["scholar.Tome"] = new[] { "Tome", "Codex" },
            ["herald.Tome"] = new[] { "Songbook", "Daybook" },
            ["wanderer.LightArmor"] = new[] { "Travel Coat", "Jerkin" },
            ["duelist.LightArmor"] = new[] { "Doublet", "Jerkin" },
            ["hunter.LightArmor"] = new[] { "Hunting Jerkin", "Gambeson" },
            ["scholar.LightArmor"] = new[] { "Robe" },
            ["herald.LightArmor"] = new[] { "Tabard" },
        };

        /// <summary>The plain noun for a base and archetype ("Sabre", "Walking Staff"), falling back to Forms.BaseNoun.</summary>
        public static string Noun(ArchetypeDef a, BaseType t, ref Pcg32 rng)
        {
            if (a != null && Nouns.TryGetValue(a.Id + "." + t, out var list)) return list[rng.Range(0, list.Length)];
            return Forms.BaseNoun(t, ref rng);
        }
    }
}
