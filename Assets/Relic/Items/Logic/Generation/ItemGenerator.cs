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
    public sealed partial class ItemGenerator
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

        /// <summary>The world's lore (its generated cultures and prehistory).</summary>
        public Relic.Lore.LoreBible Bible => _world.Lore;

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
            var home = spec.RegionId != null ? _world.Region(spec.RegionId) : _world.NearestRegion(tier);
            var bible = Bible;

            // The sentence first (17-item-grammar); everything below reads it.
            var sentence = ItemGrammar.Resolve(seed, bt, rarity, tier, home, _world, bible, _freshness.IsOverused);
            Theme theme = sentence.Has(GrammarLayer.Theme) ? ItemGrammar.ThemeOf(sentence, bible) : null;
            var archetype = Archetypes.ById(sentence.Archetype);
            float budget = Budget(bt, rarity, tier);

            var item = new Item
            {
                Id = id,
                Seed = seed,
                BaseType = bt,
                Slot = ItemSlots.For(bt),
                Rarity = rarity,
                Tier = tier,
                Theme = theme?.ToString(),
                Form = sentence.Form,
                Grammar = sentence,
                Stats = StatRoller.Roll(sentence, rarity, tier, seed, 0),
                Memory = bt == BaseType.Relic ? new ItemMemory() : null,
                Generator = new GeneratorInfo { Name = GeneratorName, Version = GeneratorVersion, Mode = GenerationMode.Tables },
            };

            var ctx = new TemplateContext { Theme = theme, Archetype = archetype, Tier = tier, Rarity = rarity, BaseType = bt, HomeRegion = home };
            AddTableEffects(item, ctx, budget);
            item.Visual = TableVisual(sentence, rarity, theme, seed);

            if (rarity <= Rarity.Uncommon)
            {
                item.Name = TableName(sentence);
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

        /// <summary>Forge reroll: a fresh stat sample from sub-seed Hash.Combine(seed, rerollCount). Text, lines and effects are kept.</summary>
        public Item Reroll(Item item)
        {
            var copy = ItemJson.Clone(item);
            copy.RerollCount = item.RerollCount + 1;
            copy.Stats = item.Grammar != null
                ? StatRoller.Roll(item.Grammar, item.Rarity, item.Tier, item.Seed, copy.RerollCount)
                : StatRoller.Roll(item.BaseType, item.Rarity, item.Tier, item.Seed, copy.RerollCount);
            _store?.Put(copy);
            return copy;
        }

        /// <summary>"Worn Steel Sword", "Sturdy Amberwood Longbow": Common and Uncommon names (quality, material, noun).</summary>
        public static string TableName(ItemSentence s)
        {
            string noun = Relic.Lore.LoreSampler.Title(s.Form);
            var named = CultureForms.NamedMaterial(s.Form);
            bool materialInForm = named != null && named.Name == s.Material;
            return s.Quality + " " + (materialInForm ? "" : Relic.Lore.LoreSampler.Title(s.Material) + " ") + noun;
        }

        // ---------- table effects ----------

        /// <summary>
        /// An item's total power budget: the rarity bonus (PowerBudget.For) plus the kit
        /// allowance its slot carries, which is a weapon's skill (E) or a relic's ultimate (Q).
        /// </summary>
        public static float Budget(BaseType bt, Rarity rarity, int tier)
        {
            float b = PowerBudget.For(rarity, tier);
            var slot = ItemSlots.For(bt);
            if (slot == ItemSlot.Weapon) b += PowerBudget.ActiveSkill(tier);
            if (bt == BaseType.Relic) b += PowerBudget.Ultimate(tier);
            return b;
        }

        static readonly float[] BaseShare = { 0.7f, 0.55f, 0.5f, 0.4f, 0f };

        void AddTableEffects(Item item, TemplateContext ctx, float budget)
        {
            if (item.BaseType == BaseType.Relic) return;
            var rng = Pcg32.ForPurpose(item.Seed, "item.effects");
            float bonus = PowerBudget.For(item.Rarity, item.Tier);
            if (item.Slot == ItemSlot.Weapon)
                AddFitted(item, EffectTemplates.WeaponSkill(ctx), PowerBudget.ActiveSkill(item.Tier));
            else if (item.Rarity >= Rarity.Uncommon || rng.Chance(0.4f))
            {
                AddFitted(item, EffectTemplates.SlotPassive(ctx, ref rng), bonus * BaseShare[(int)item.Rarity]);
                item.Grammar.SlotPassive = item.Effects.Rules[item.Effects.Rules.Count - 1].Name;
            }

            // "Maybe one simple effect": Uncommon half the time, Rare always; chosen from the archetype's triggers.
            bool extra = item.Rarity == Rarity.Rare || (item.Rarity == Rarity.Uncommon && rng.Chance(0.5f));
            if (extra)
            {
                float left = budget - PowerBudget.Cost(item.Effects);
                float target = Math.Min(left, bonus * 0.4f);
                foreach (var t in EnchantmentChooser.Ranked(ctx, ref rng))
                {
                    var rule = EffectFitter.Fit(t.Factory(ctx, ref rng), target);
                    float c = PowerBudget.Cost(rule);
                    if (c > target + 1e-3f || c < 0.3f * target) continue;
                    item.Effects.Rules.Add(rule);
                    break;
                }
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

        /// <summary>The look from the sentence: the material's tint and wear, the motif's glow (Rare+), the culture's form.</summary>
        static ItemVisual TableVisual(ItemSentence s, Rarity rarity, Theme theme, ulong seed)
        {
            var rng = Pcg32.ForPurpose(seed, "item.visual");
            var mat = Materials.Find(s.Material);
            float glow = mat?.Glow ?? 0f;
            var kit = s.Base == BaseType.Relic ? Forms.FormById(s.Kit) : null;
            return new ItemVisual
            {
                KitParts = kit != null ? new List<string>(kit.Parts) : Forms.TableKit(s.Base),
                Material = s.Material,
                Wear = ItemGrammar.Wear(s, ref rng),
                EmissiveColor = theme?.Color ?? (glow > 0f ? mat.Tint : "#000000"),
                Decal = theme != null ? Forms.DecalFor(theme.Motif.Id) : "none",
                EmissiveIntensity = (float)Math.Round(rarity >= Rarity.Rare ? 0.25f * (int)rarity + glow : glow, 2),
            };
        }

    }
}
