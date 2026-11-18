using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Items.Text;

namespace Relic.Items
{
    // The text step of the item pipeline (Rare+): brief from the grammar sentence →
    // writer (LLM or template) → parse → DraftValidator → retry ≤ 3 → fallback → apply.
    public sealed partial class ItemGenerator
    {
        RelicBrief BuildBrief(Item item, DraftKind kind, Theme theme, WorldRegion home, float textBudget)
        {
            var rng = Pcg32.ForPurpose(item.Seed, "item.brief");
            int count = 1;
            if (kind == DraftKind.Legendary)
            {
                float x = rng.NextFloat();
                count = x < 0.3f ? 1 : x < 0.75f ? 2 : 3;
            }
            bool cursed = kind != DraftKind.RareName && theme != null && theme.IsSomber && rng.Chance(theme.Leans("curse") ? 1f : theme.Leans("drawback") ? 0.4f : 0f);
            return new RelicBrief
            {
                Kind = kind,
                Seed = item.Seed,
                ItemId = item.Id,
                Rarity = item.Rarity,
                Tier = item.Tier,
                BaseType = item.BaseType,
                Form = item.Form,
                Theme = theme,
                BudgetPp = Math.Max(0f, textBudget),
                EnchantmentCount = count,
                Cursed = cursed,
                World = _world,
                HomeRegion = home,
                BannedWords = _freshness.Banned(),
                TakenNames = _freshness.RecentNames(),
                Stats = item.Stats,
                Sentence = item.Grammar,
                Archetype = Archetypes.ById(item.Grammar.Archetype),
            };
        }

        async Task WriteText(Item item, RelicBrief brief, float itemBudget, CancellationToken ct)
        {
            var record = new GenerationRecord { ItemId = item.Id, Kind = brief.Kind };
            var request = new TextRequest
            {
                Purpose = brief.Kind == DraftKind.Legendary ? "relic" : brief.Kind == DraftKind.Epic ? "epic" : "rare_name",
                JsonSchema = Prompts.Schema(brief),
                MaxTokens = brief.Kind == DraftKind.RareName ? 200 : _options.MaxTokens,
                Temperature = _options.Temperature,
                Seed = (int)(item.Seed & 0x7FFFFFFF),
                Context = brief,
            };
            request.Messages.Add(new TextMessage("system", Prompts.SystemPrompt(brief.Kind)));
            request.Messages.Add(new TextMessage("user", Prompts.UserPrompt(brief)));

            RelicDraft accepted = null;
            string source = null;
            for (int attempt = 0; attempt <= _options.MaxRetries && accepted == null; attempt++)
            {
                record.Attempts++;
                List<string> errors;
                TextResult result = null;
                try
                {
                    result = await _text.GenerateAsync(request, ct).ConfigureAwait(false);
                    record.Seconds += result.Seconds;
                }
                catch (TextGenerationException e)
                {
                    record.TransportFailures++;
                    record.ErrorsPerAttempt.Add(new List<string> { e.Message });
                    continue; // nothing to feed back: the model never answered
                }

                if (!ItemJson.TryDeserialize<RelicDraft>(result.Text, out var draft, out string parseError))
                {
                    record.JsonFailures++;
                    errors = new List<string> { "the output is not valid JSON for the schema: " + parseError };
                }
                else errors = DraftValidator.Validate(draft, brief, _freshness);

                record.ErrorsPerAttempt.Add(errors);
                if (errors.Count == 0)
                {
                    accepted = draft;
                    source = result.Source;
                    if (attempt == 0) record.FirstTryValid = true;
                    break;
                }
                request.Messages.Add(new TextMessage("assistant", result.Text ?? ""));
                request.Messages.Add(new TextMessage("user", Prompts.Retry(errors)));
            }

            GenerationMode mode;
            if (accepted != null) mode = IsTemplateMode ? GenerationMode.Template : GenerationMode.Llm;
            else
            {
                accepted = TemplateAuthor.Compose(brief);
                DraftValidator.Validate(accepted, brief, null); // normalizes the ultimate
                source = TemplateAuthor.Source;
                mode = GenerationMode.LlmFallback;
                record.UsedFallback = true;
            }

            float scale = Apply(item, accepted, brief, itemBudget);
            record.ScaleApplied = scale;
            item.Generator.Mode = mode;
            item.Generator.Attempts = record.Attempts;
            item.Generator.ScaleApplied = scale;
            item.Generator.TextSource = source;
            Records.Add(record);
        }

        float Apply(Item item, RelicDraft d, RelicBrief brief, float itemBudget)
        {
            item.Name = d.Name.Trim();
            var culture = Bible.Culture(item.Grammar.Culture);
            item.Grammar.NamePattern = NamePatterns.Match(item.Name, item.Grammar, culture);
            _freshness.Add(item.Name);
            float scale = 1f;
            switch (brief.Kind)
            {
                case DraftKind.RareName:
                    item.Lore = new ItemLore { Flavor = d.Flavor.Trim() };
                    return 1f;

                case DraftKind.Epic:
                {
                    item.Lore = new ItemLore { History = d.Lore.Trim(), WorldRefs = d.WorldRefs };
                    var ench = d.Enchantments;
                    if (PowerBudget.Cost(ench) > brief.BudgetPp) scale = EffectValidator.ScaleToFit(ench, brief.BudgetPp);
                    item.Effects.Rules.AddRange(ench.Rules);
                    return scale;
                }

                default:
                {
                    item.Lore = new ItemLore { History = d.Lore.Trim(), WorldRefs = d.WorldRefs };
                    DraftValidator.NormalizeUltimate(d.Ultimate);
                    // The ultimate may use at most its own allowance (PowerBudget.Ultimate).
                    float cap = Math.Min(_options.UltimateMaxShare * itemBudget, PowerBudget.Ultimate(item.Tier));
                    if (PowerBudget.Cost(d.Ultimate) > cap)
                    {
                        var u = new EffectProgram { Rules = { d.Ultimate } };
                        scale = EffectValidator.ScaleToFit(u, cap);
                        d.Ultimate = u.Rules[0];
                    }
                    var all = new EffectProgram();
                    all.Rules.AddRange(d.Enchantments.Rules);
                    all.Rules.Add(d.Ultimate);
                    if (PowerBudget.Cost(all) > brief.BudgetPp) scale = Math.Min(scale, EffectValidator.ScaleToFit(all, brief.BudgetPp));
                    item.Effects = all;
                    item.Visual = d.Visual;
                    // The look keeps the grammar's facts: its material and the motif's color.
                    item.Visual.Material = item.Grammar.Material;
                    if (brief.Theme != null) item.Visual.EmissiveColor = brief.Theme.Color;
                    return scale;
                }
            }
        }
    }
}
