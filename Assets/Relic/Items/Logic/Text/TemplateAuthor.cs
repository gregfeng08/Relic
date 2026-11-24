using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Lore;

namespace Relic.Items.Text
{
    /// <summary>
    /// Deterministic stand-in for the LLM: writes the same draft shape from the item
    /// sentence, the Lore Bible's naming patterns and mood lines, and the effect templates.
    /// Used for TEMPLATE MODE samples (so the pipeline, budgets, rolls and grammar can be
    /// reviewed before any model is approved) and as the fallback relic when the LLM fails.
    /// </summary>
    public static partial class TemplateAuthor
    {
        public const string Source = "template-v2";

        public static RelicDraft Compose(RelicBrief b)
        {
            var rng = Pcg32.ForPurpose(b.Seed, "relic.template");
            var bible = b.World?.Lore ?? LoreSource.Default;
            var s = b.Sentence ?? throw new ArgumentException("RelicBrief.Sentence is required");
            var culture = bible.Culture(s.Culture) ?? bible.Cultures[0];
            var theme = b.Theme ?? ItemGrammar.ThemeOf(s, bible);

            var draft = new RelicDraft { Name = Name(b, culture, theme) };
            if (b.Kind == DraftKind.RareName)
            {
                draft.Flavor = Flavor(b, theme, ref rng);
                return draft;
            }
            draft.Lore = Lore(b, culture, theme, ref rng, out var used);
            draft.WorldRefs = used;
            var ctx = new TemplateContext { Theme = theme, Archetype = b.Archetype ?? Archetypes.ById(s.Archetype), Tier = b.Tier, Rarity = b.Rarity, BaseType = b.BaseType, HomeRegion = b.HomeRegion };

            if (b.Kind == DraftKind.Epic)
            {
                draft.Enchantments = new EffectProgram();
                float target = b.BudgetPp * (0.85f + 0.1f * rng.NextFloat());
                draft.Enchantments.Rules.Add(FitEnchantment(ctx, target, new HashSet<string>(), b.Cursed, ref rng, out _));
                return draft;
            }

            // Legendary: ultimate first (its own allowance), then enchantments share what's left.
            var ultTemplate = EffectTemplates.Ultimates[rng.Range(0, EffectTemplates.Ultimates.Length)];
            var ultBuilder = ultTemplate.Factory(ctx, ref rng);
            draft.Ultimate = EffectFitter.Fit(ultBuilder, b.BudgetPp * (0.25f + 0.1f * rng.NextFloat()));
            float remaining = b.BudgetPp - PowerBudget.Cost(draft.Ultimate);

            draft.Enchantments = new EffectProgram();
            int count = Math.Max(1, Math.Min(3, b.EnchantmentCount));
            bool cursed = b.Cursed;
            EffectRule curse = null;
            if (cursed && count < 3)
            {
                // A standing curse rule (lowers a stat while equipped) buys power for the others.
                curse = EffectTemplates.CurseRule(ctx, ref rng);
                remaining -= PowerBudget.Cost(curse); // negative cost: adds budget
                cursed = false;
            }
            float fill = 0.88f + 0.1f * rng.NextFloat();
            var weights = new float[count];
            float wsum = 0f;
            for (int i = 0; i < count; i++) { weights[i] = 1f + rng.NextFloat(); wsum += weights[i]; }
            var usedTemplates = new HashSet<string>();
            float pool = remaining * fill;
            for (int i = 0; i < count; i++)
            {
                float target = pool * weights[i] / wsum;
                wsum -= weights[i];
                var rule = FitEnchantment(ctx, target, usedTemplates, cursed && i == 0, ref rng, out float cost);
                pool -= cost;
                draft.Enchantments.Rules.Add(rule);
            }
            if (curse != null) draft.Enchantments.Rules.Add(curse);
            draft.Visual = Visual(b, theme, ref rng);
            return draft;
        }

        /// <summary>
        /// Builds the first template (in EnchantmentChooser order: the archetype's triggers about
        /// 80% of the time) that fits the target; falls back to the best partial fit.
        /// </summary>
        static EffectRule FitEnchantment(TemplateContext ctx, float target, HashSet<string> used, bool cursed, ref Pcg32 rng, out float cost)
        {
            ActionSpec drawback = cursed ? EffectTemplates.Drawback(ctx, ref rng) : null;
            EffectRule best = null; float bestCost = float.MaxValue;
            foreach (var t in EnchantmentChooser.Ranked(ctx, ref rng, used))
            {
                if (drawback != null && t.Trigger == TriggerKind.Passive) continue; // a price needs a moment to be paid in
                var builder = t.Factory(ctx, ref rng);
                EffectFitter.RuleBuilder withCurse = builder;
                if (drawback != null)
                    withCurse = s =>
                    {
                        var r = builder(s);
                        if (r.Actions.Count < EffectValidator.MaxActionsPerList && r.Trigger.Kind != TriggerKind.Passive) r.Actions.Add(ItemJson.Clone(drawback));
                        return r;
                    };
                var rule = EffectFitter.Fit(withCurse, target);
                float c = PowerBudget.Cost(rule);
                if (c <= target + 1e-3f && c >= 0.5f * target)
                {
                    used.Add(t.Id);
                    cost = c;
                    return rule;
                }
                if (c <= target + 1e-3f && (best == null || c > bestCost || bestCost > target)) { best = rule; bestCost = c; }
            }
            if (best == null)
            {
                // Nothing fits (tiny budget): a bare rider at minimum strength, sized down by ScaleToFit later.
                best = EffectFitter.Fit(EffectTemplates.Enchantments[0].Factory(ctx, ref rng), target);
                bestCost = PowerBudget.Cost(best);
            }
            cost = bestCost;
            return best;
        }

        // ---------- visual ----------

        /// <summary>The relic's look from its sentence: the kit form's parts, its material, the motif's glow.</summary>
        static ItemVisual Visual(RelicBrief b, Theme theme, ref Pcg32 rng)
        {
            var s = b.Sentence;
            var form = Forms.FormById(s.Kit) ?? Forms.RelicForms[0];
            var parts = new List<string>(form.Parts);
            int keep = Math.Min(parts.Count, 3 + rng.Range(0, 2));
            while (parts.Count > keep) parts.RemoveAt(rng.Range(1, parts.Count)); // keep the defining first part
            float glow = Materials.Find(s.Material)?.Glow ?? 0f;
            return new ItemVisual
            {
                KitParts = parts,
                Material = s.Material,
                Wear = ItemGrammar.Wear(s, ref rng),
                EmissiveColor = theme.Color,
                Decal = rng.Chance(0.25f) ? "makers-mark" : Forms.DecalFor(theme.Motif.Id),
                EmissiveIntensity = (float)Math.Round(Math.Min(4f, 0.5f + 0.3f * b.Tier + glow), 2),
            };
        }
    }

    /// <summary>
    /// ITextGenerator that writes TemplateAuthor drafts as JSON, so TEMPLATE MODE
    /// runs through exactly the same parse → validate → fit path as an LLM.
    /// Requires TextRequest.Context to be the RelicBrief.
    /// </summary>
    public sealed class TemplateGenerator : ITextGenerator
    {
        public string Name => TemplateAuthor.Source;

        public Task<TextResult> GenerateAsync(TextRequest request, CancellationToken cancellationToken = default)
        {
            var brief = request.Context as RelicBrief;
            if (brief == null) throw new TextGenerationException("TemplateGenerator needs a RelicBrief in TextRequest.Context");
            var draft = TemplateAuthor.Compose(brief);
            return Task.FromResult(new TextResult { Text = ItemJson.Serialize(draft), Source = Name });
        }
    }
}
