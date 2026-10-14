using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Relic.Core.Effects;
using Relic.Core.Random;
using Relic.Core.Stats;
using Relic.Items.Text;

namespace Relic.Items
{
    /// <summary>What to generate. The world seed and index decide everything deterministic.</summary>
    public sealed class ItemSpec
    {
        public ulong WorldSeed;
        /// <summary>Generation index within the world (drop counter). Item seed = f(world seed, index).</summary>
        public int Index;
        public int Tier;
        public Rarity Rarity;
        /// <summary>Null = rolled from the seed. Relic always means Legendary.</summary>
        public BaseType? BaseType;
        /// <summary>Region the item dropped in; null = the stub region whose tier is closest.</summary>
        public string RegionId;
    }

    public sealed class PipelineOptions
    {
        /// <summary>Retries after the first attempt, with the errors fed back (08: "retry up to 3 times").</summary>
        public int MaxRetries = 3;
        /// <summary>An ultimate may use at most this share of the relic budget, priced without resonance relief.</summary>
        public float UltimateMaxShare = 0.4f;
        public int MaxTokens = 1400;
        public float Temperature = 0.8f;
    }

    /// <summary>What happened while writing one item's text (for benchmarks and debugging).</summary>
    public sealed class GenerationRecord
    {
        public string ItemId;
        public DraftKind Kind;
        public int Attempts;
        public bool UsedFallback;
        public bool FirstTryValid;
        public int JsonFailures;
        public int TransportFailures;
        public double Seconds;
        public float ScaleApplied = 1f;
        public List<List<string>> ErrorsPerAttempt = new List<List<string>>();
    }

    /// <summary>
    /// The item pipeline (08-items-bosses): tables → stats with overrolls →
    /// table effects → (Rare+) text step with parse/validate/retry/fallback →
    /// budget fit → persist.
    /// </summary>
    public sealed class ItemGenerator
    {
        public const string GeneratorName = "relic.items";
        public const int GeneratorVersion = 1;

        static readonly BaseType[] DropTypes =
        {
            BaseType.Sword, BaseType.Greatsword, BaseType.Bow, BaseType.Staff, BaseType.Tome,
            BaseType.LightArmor, BaseType.MediumArmor, BaseType.HeavyArmor, BaseType.Trinket,
        };

        readonly WorldContext _world;
        readonly ITextGenerator _text;
        readonly IItemStore _store;
        readonly NameFreshness _freshness;
        readonly PipelineOptions _options;

        public readonly List<GenerationRecord> Records = new List<GenerationRecord>();
        public NameFreshness Freshness => _freshness;
        public WorldContext World => _world;

        /// <param name="text">Writer for Rare+ text. Null = TemplateGenerator (TEMPLATE MODE, no LLM).</param>
        public ItemGenerator(WorldContext world, ITextGenerator text = null, IItemStore store = null, NameFreshness freshness = null, PipelineOptions options = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _text = text ?? new TemplateGenerator();
            _store = store;
            _freshness = freshness ?? new NameFreshness();
            _options = options ?? new PipelineOptions();
        }

        public bool IsTemplateMode => _text is TemplateGenerator;

        public static ulong ItemSeed(ulong worldSeed, int index) => Hash.Combine(worldSeed, Hash.Of("item"), unchecked((ulong)index));

        public static string ItemId(ulong seed) => "itm-" + seed.ToString("x16");

        /// <summary>
        /// Synchronous convenience (template mode, tests, editor tools). With a real network generator
        /// on a thread that has a SynchronizationContext (Unity's main thread), it runs off-thread so
        /// continuations can't deadlock. In the game, prefer GenerateAsync from the background pool.
        /// </summary>
        public Item Generate(ItemSpec spec) =>
            SynchronizationContext.Current == null || IsTemplateMode
                ? GenerateAsync(spec).GetAwaiter().GetResult()
                : Task.Run(() => GenerateAsync(spec)).GetAwaiter().GetResult();

        public async Task<Item> GenerateAsync(ItemSpec spec, CancellationToken ct = default)
        {
            ulong seed = ItemSeed(spec.WorldSeed, spec.Index);
            string id = ItemId(seed);
            if (_store != null && _store.TryGet(id, out var saved)) return saved;

            var baseRng = Pcg32.ForPurpose(seed, "item.base");
            var rarity = spec.Rarity;
            BaseType bt;
            if (spec.BaseType.HasValue) bt = spec.BaseType.Value;
            else if (rarity == Rarity.Legendary) bt = BaseType.Relic;
            else bt = DropTypes[baseRng.Range(0, DropTypes.Length)];
            if (bt == BaseType.Relic) rarity = Rarity.Legendary;

            int tier = Math.Max(0, Math.Min(Difficulty.MaxTier, spec.Tier));
            var home = spec.RegionId != null ? _world.Region(spec.RegionId) : NearestRegion(tier);
            Theme theme = rarity >= Rarity.Rare ? ThemePicker.Pick(seed, home?.Tags) : null;
            float budget = PowerBudget.For(rarity, tier);

            var item = new Item
            {
                Id = id,
                Seed = seed,
                BaseType = bt,
                Slot = ItemSlots.For(bt),
                Rarity = rarity,
                Tier = tier,
                Theme = theme?.ToString(),
                Stats = StatRoller.Roll(bt, rarity, tier, seed, 0),
                Memory = bt == BaseType.Relic ? new ItemMemory() : null,
                Generator = new GeneratorInfo { Name = GeneratorName, Version = GeneratorVersion, Mode = GenerationMode.Tables },
            };

            var ctx = new TemplateContext { Theme = theme, Tier = tier, Rarity = rarity, BaseType = bt, HomeRegion = home };
            AddTableEffects(item, ctx, budget);
            item.Visual = TableVisual(bt, tier, rarity, theme, seed);

            var nameRng = Pcg32.ForPurpose(seed, "item.noun");
            string noun = Forms.BaseNoun(bt, ref nameRng);
            item.Form = bt == BaseType.Relic ? Forms.PickRelicForm(seed) : noun.ToLowerInvariant();

            if (rarity <= Rarity.Uncommon)
            {
                item.Name = Forms.TableName(bt, rarity, tier, seed);
            }
            else
            {
                var kind = rarity == Rarity.Rare ? DraftKind.RareName : rarity == Rarity.Epic ? DraftKind.Epic : DraftKind.Legendary;
                float textBudget = budget - PowerBudget.Cost(item.Effects);
                var brief = BuildBrief(item, kind, theme, home, textBudget);
                await WriteText(item, brief, budget, ct).ConfigureAwait(false);
            }

            var check = EffectValidator.Validate(item.Effects, budget);
            if (item.Effects.Rules.Count > 0 && !check.IsValid)
                throw new InvalidOperationException($"generated item {id} failed validation: {check}");

            _store?.Put(item);
            return item;
        }

        /// <summary>Forge reroll: a fresh stat sample from sub-seed Hash.Combine(seed, rerollCount). Text and effects are kept.</summary>
        public Item Reroll(Item item)
        {
            var copy = ItemJson.Clone(item);
            copy.RerollCount = item.RerollCount + 1;
            copy.Stats = StatRoller.Roll(item.BaseType, item.Rarity, item.Tier, item.Seed, copy.RerollCount);
            _store?.Put(copy);
            return copy;
        }

        WorldRegion NearestRegion(int tier)
        {
            WorldRegion best = null;
            foreach (var r in _world.Regions)
                if (best == null || Math.Abs(r.Tier - tier) < Math.Abs(best.Tier - tier)) best = r;
            return best;
        }

        // ---------- table effects ----------

        static readonly float[] BaseShare = { 0.7f, 0.55f, 0.5f, 0.4f, 0f };

        void AddTableEffects(Item item, TemplateContext ctx, float budget)
        {
            if (item.BaseType == BaseType.Relic) return;
            var rng = Pcg32.ForPurpose(item.Seed, "item.effects");
            float share = BaseShare[(int)item.Rarity];
            EffectFitter.RuleBuilder baseBuilder = null;
            if (item.Slot == ItemSlot.Weapon) baseBuilder = EffectTemplates.WeaponSkill(ctx);
            else if (item.Rarity >= Rarity.Uncommon || rng.Chance(0.4f)) baseBuilder = EffectTemplates.SlotPassive(ctx, ref rng);
            if (baseBuilder != null) AddFitted(item, baseBuilder, budget * share);

            // "Maybe one simple effect from a template list": Uncommon half the time, Rare always.
            bool extra = item.Rarity == Rarity.Rare || (item.Rarity == Rarity.Uncommon && rng.Chance(0.5f));
            if (extra)
            {
                string templateId = item.Slot == ItemSlot.Armor ? "retort" : "rider";
                var t = EffectTemplates.EnchantmentById(templateId);
                float left = budget - PowerBudget.Cost(item.Effects);
                AddFitted(item, t.Factory(ctx, ref rng), Math.Min(left, budget * 0.4f));
            }
        }

        static void AddFitted(Item item, EffectFitter.RuleBuilder builder, float target)
        {
            var rule = EffectFitter.Fit(builder, target);
            if (PowerBudget.Cost(rule) > target)
            {
                var p = new EffectProgram { Rules = { rule } };
                EffectValidator.ScaleToFit(p, target);
                rule = p.Rules[0];
            }
            item.Effects.Rules.Add(rule);
        }

        static ItemVisual TableVisual(BaseType bt, int tier, Rarity rarity, Theme theme, ulong seed)
        {
            var rng = Pcg32.ForPurpose(seed, "item.visual");
            return new ItemVisual
            {
                KitParts = Forms.TableKit(bt),
                Material = Forms.MaterialWord(bt, tier, ref rng).ToLowerInvariant(),
                Wear = (float)Math.Round(rarity == Rarity.Common ? 0.5f + 0.4f * rng.NextFloat() : 0.1f + 0.4f * rng.NextFloat(), 2),
                EmissiveColor = theme?.Motif.Color ?? "#000000",
                Decal = theme != null ? Forms.DecalFor(theme.Motif.Id) : "none",
                EmissiveIntensity = rarity >= Rarity.Rare ? 0.25f * (int)rarity : 0f,
            };
        }

        // ---------- text step ----------

        RelicBrief BuildBrief(Item item, DraftKind kind, Theme theme, WorldRegion home, float textBudget)
        {
            var rng = Pcg32.ForPurpose(item.Seed, "item.brief");
            int count = 1;
            if (kind == DraftKind.Legendary)
            {
                float x = rng.NextFloat();
                count = x < 0.3f ? 1 : x < 0.75f ? 2 : 3;
            }
            bool cursed = kind != DraftKind.RareName && theme != null && rng.Chance(theme.Mood.CurseChance);
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
                    // Cap the ultimate's share, priced as if resonance gave no relief (see README).
                    float cap = _options.UltimateMaxShare * itemBudget;
                    if (EffectFitter.UnrelievedCost(d.Ultimate) > cap)
                    {
                        var u = new EffectProgram { Rules = { d.Ultimate } };
                        float relief = PowerBudget.Cost(d.Ultimate) / Math.Max(1e-6f, EffectFitter.UnrelievedCost(d.Ultimate));
                        scale = EffectValidator.ScaleToFit(u, cap * relief);
                        d.Ultimate = u.Rules[0];
                    }
                    var all = new EffectProgram();
                    all.Rules.AddRange(d.Enchantments.Rules);
                    all.Rules.Add(d.Ultimate);
                    if (PowerBudget.Cost(all) > brief.BudgetPp) scale = Math.Min(scale, EffectValidator.ScaleToFit(all, brief.BudgetPp));
                    item.Effects = all;
                    item.Visual = d.Visual;
                    return scale;
                }
            }
        }
    }
}
