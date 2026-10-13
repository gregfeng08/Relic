using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Relic.Core.Effects;

namespace Relic.Items.Text
{
    public enum DraftKind
    {
        /// <summary>Rare: name and one-line flavor only.</summary>
        RareName,
        /// <summary>Epic: name, lore paragraph, one enchantment.</summary>
        Epic,
        /// <summary>Legendary relic: name, lore, 1–3 enchantments, ultimate, visual.</summary>
        Legendary,
    }

    /// <summary>
    /// What to ask the writer for. Built deterministically from the item seed
    /// (06: the seed decides what to ask for; the written result is persisted).
    /// </summary>
    public sealed class RelicBrief
    {
        public DraftKind Kind;
        public ulong Seed;
        public string ItemId;
        public Rarity Rarity;
        public int Tier;
        public BaseType BaseType;
        /// <summary>Relic form ("lantern") or the base noun for epics ("sword").</summary>
        public string Form;
        public Theme Theme;
        /// <summary>Power the written enchantments (and ultimate) may spend, in pp.</summary>
        public float BudgetPp;
        /// <summary>Requested number of enchantments (Legendary 1–3, Epic 1).</summary>
        public int EnchantmentCount = 1;
        /// <summary>The theme's mood asks for a drawback in exchange for power.</summary>
        public bool Cursed;
        public WorldContext World;
        /// <summary>Region the item comes from (nearest to its tier).</summary>
        public WorldRegion HomeRegion;
        /// <summary>Words currently overused in recent names (5% rule).</summary>
        public List<string> BannedWords = new List<string>();
        /// <summary>Stat lines already rolled, for the writer's context.</summary>
        public List<ItemStat> Stats = new List<ItemStat>();
    }

    /// <summary>
    /// What the writer returns, parsed strictly (unknown fields are errors).
    /// Shape: { name, flavor?, lore?, worldRefs?, enchantments?, ultimate?, visual? }.
    /// </summary>
    public sealed class RelicDraft
    {
        public string Name;
        public string Flavor;
        public string Lore;
        public List<string> WorldRefs;
        public EffectProgram Enchantments;
        public EffectRule Ultimate;
        public ItemVisual Visual;
    }

    /// <summary>
    /// Checks a draft against the brief: required fields, style guide, world
    /// references, and the grammar's structural rules. Every message is phrased
    /// so it can be fed back to the model verbatim on a retry. Budget overruns are
    /// not errors here; the pipeline fixes those with ScaleToFit.
    /// </summary>
    public static class DraftValidator
    {
        public const int MaxNameLength = 40;
        public const int MaxNameWords = 5;
        public const int MaxFlavorLength = 160;

        static readonly Regex HexColor = new Regex("^#[0-9A-Fa-f]{6}$");

        public static List<string> Validate(RelicDraft d, RelicBrief brief, NameFreshness freshness)
        {
            var errors = new List<string>();
            if (d == null) { errors.Add("empty response"); return errors; }

            // Name
            if (string.IsNullOrWhiteSpace(d.Name)) errors.Add("name is missing");
            else
            {
                if (d.Name.Length > MaxNameLength) errors.Add($"name is longer than {MaxNameLength} characters");
                if (d.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > MaxNameWords) errors.Add($"name has more than {MaxNameWords} words");
                foreach (var w in StyleGuide.ForbiddenIn(d.Name)) errors.Add($"name uses the banned word \"{w}\"");
                if (freshness != null)
                    foreach (var w in freshness.OverusedIn(d.Name)) errors.Add($"name uses \"{w}\", which is overused in recent names; pick another word");
            }

            if (brief.Kind == DraftKind.RareName)
            {
                if (string.IsNullOrWhiteSpace(d.Flavor)) errors.Add("flavor is missing");
                else
                {
                    if (d.Flavor.Length > MaxFlavorLength) errors.Add($"flavor is longer than {MaxFlavorLength} characters");
                    if (StyleGuide.SentenceCount(d.Flavor) > 1) errors.Add("flavor must be one sentence");
                    foreach (var w in StyleGuide.ForbiddenIn(d.Flavor)) errors.Add($"flavor uses the banned word \"{w}\"");
                }
                return errors;
            }

            // Lore and world references
            if (string.IsNullOrWhiteSpace(d.Lore)) errors.Add("lore is missing");
            else
            {
                int n = StyleGuide.SentenceCount(d.Lore);
                if (n < StyleGuide.MinLoreSentences || n > StyleGuide.MaxLoreSentences)
                    errors.Add($"lore must be {StyleGuide.MinLoreSentences}–{StyleGuide.MaxLoreSentences} sentences (found {n})");
                foreach (var w in StyleGuide.ForbiddenIn(d.Lore)) errors.Add($"lore uses the banned word \"{w}\"");
            }
            if (d.WorldRefs == null || d.WorldRefs.Count == 0) errors.Add("worldRefs must list at least one id from the world context");
            else
            {
                bool named = false;
                foreach (var id in d.WorldRefs)
                {
                    string name = brief.World?.NameOf(id);
                    if (name == null) { errors.Add($"worldRefs contains unknown id \"{id}\""); continue; }
                    if (d.Lore != null && d.Lore.IndexOf(StripArticle(name), StringComparison.OrdinalIgnoreCase) >= 0) named = true;
                }
                if (!named && d.Lore != null) errors.Add("lore must mention by name at least one place, faction, person or event listed in worldRefs");
            }

            // Enchantments
            int min = 1, max = brief.Kind == DraftKind.Epic ? 1 : 3;
            if (d.Enchantments?.Rules == null || d.Enchantments.Rules.Count < min || d.Enchantments.Rules.Count > max)
                errors.Add(max == 1 ? "enchantments must contain exactly 1 rule" : $"enchantments must contain {min}–{max} rules");
            else
            {
                foreach (var r in d.Enchantments.Rules)
                    if (r?.Trigger != null && r.Trigger.Kind == TriggerKind.Active)
                        errors.Add($"enchantment \"{r.Name}\" uses trigger Active; only the ultimate is activated, enchantments must react (OnHit, OnKill, Passive, ...)");
                var structural = EffectValidator.Validate(d.Enchantments, float.MaxValue);
                foreach (var e in structural.Errors) errors.Add("enchantments: " + e);
            }

            if (brief.Kind == DraftKind.Legendary)
            {
                if (d.Ultimate == null) errors.Add("ultimate is missing");
                else
                {
                    if (d.Ultimate.Trigger == null || d.Ultimate.Trigger.Kind != TriggerKind.Active) errors.Add("ultimate.trigger.kind must be Active");
                    else
                    {
                        NormalizeUltimate(d.Ultimate);
                        var probe = new EffectProgram { Rules = { d.Ultimate } };
                        foreach (var e in EffectValidator.Validate(probe, float.MaxValue).Errors) errors.Add("ultimate: " + e.Replace("rules[0]", "ultimate"));
                    }
                }
                if (d.Visual == null) errors.Add("visual is missing");
                else ValidateVisual(d.Visual, errors);
            }
            return errors;
        }

        static void ValidateVisual(ItemVisual v, List<string> errors)
        {
            if (v.KitParts == null || v.KitParts.Count == 0 || v.KitParts.Count > 6) errors.Add("visual.kitParts must list 1–6 parts");
            else
                foreach (var p in v.KitParts)
                    if (Array.IndexOf(Forms.KitParts, p) < 0) errors.Add($"visual.kitParts has unknown part \"{p}\"");
            if (string.IsNullOrWhiteSpace(v.Material) || v.Material.Length > 40) errors.Add("visual.material must be 1–40 characters");
            else foreach (var w in StyleGuide.ForbiddenIn(v.Material)) errors.Add($"visual.material uses the banned word \"{w}\"");
            if (v.Wear < 0f || v.Wear > 1f) errors.Add("visual.wear must be 0–1");
            if (v.EmissiveColor == null || !HexColor.IsMatch(v.EmissiveColor)) errors.Add("visual.emissiveColor must be \"#RRGGBB\"");
            if (v.EmissiveIntensity < 0f || v.EmissiveIntensity > 4f) errors.Add("visual.emissiveIntensity must be 0–4");
            if (v.Decal != null && Array.IndexOf(Forms.Decals, v.Decal) < 0) errors.Add($"visual.decal \"{v.Decal}\" is not a known decal");
        }

        /// <summary>
        /// The game, not the writer, decides how an ultimate is paid for: one full
        /// resonance bar and a fixed cooldown. Applied to every ultimate.
        /// </summary>
        public static void NormalizeUltimate(EffectRule ultimate)
        {
            if (ultimate == null) return;
            ultimate.CostResource = Relic.Core.Stats.ResourceId.Resonance;
            ultimate.CostAmount = EffectTemplates.UltimateResonanceCost;
            ultimate.Cooldown = EffectTemplates.UltimateCooldown;
            ultimate.Chance = 1f;
        }

        public static string StripArticle(string name) =>
            name != null && name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? name.Substring(4) : name;
    }
}
