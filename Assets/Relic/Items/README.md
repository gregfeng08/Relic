# Relic.Items: items, relics and bosses (stream I)

Briefs: `docs/design/08-items-bosses.md`, `docs/design/17-item-grammar.md` (the item grammar), `docs/design/16-lore-and-tone.md` (the Lore Bible). Contracts used (not edited): `Relic.Core` effect grammar, `PowerBudget`, `EffectValidator`, `Pcg32`/`Hash`; `Relic.Lore` (`LoreBible`, `NameGenerator`, `MoodPicker`, `TemplateText`).

```
Logic/   (Relic.Items.Logic: pure C#, compiled by the dotnet harness too; references Relic.Core and Relic.Lore.Logic)
  Model/       Item (+ Item.Grammar), ItemStat (+ Source), ItemLore, ItemVisual, ItemMemory, GeneratorInfo, ItemJson
  Grammar/     ItemSentence, ItemGrammar (resolver), Archetypes, ArchetypeLines, Materials, CultureForms, NamePatterns,
               Theme + ThemePicker + MotifTraits, CoherenceValidator, CoherenceStats, LoreSource
  Rolls/       StatTables (base tables), StatRoller, Overroll
  Lore/        StubWorld (a seeded world instantiated from the Lore Bible) + WorldContext, StyleGuide + NameFreshness, Forms
  Effects/     EffectTemplates (+ GrammarTemplates, EnchantmentChooser), TemplateContext, EffectFitter, EffectDescriber
  Text/        ITextGenerator, LlamaServerGenerator, FakeGenerator, TemplateGenerator/TemplateAuthor (+ TemplateWriting),
               Prompts, JsonSchemaBuilder, RelicBrief/RelicDraft/DraftValidator, DraftGrammar, LoreRefs
  Generation/  ItemGenerator (the pipeline) + ItemTextStep, IItemStore (+ in-memory and JSON-file stores), LlmBenchmark
  Bosses/      BossDefinition, BossHealth, BossArchetypes, PatternLibrary, BossGenerator, ReadabilityChecker
  Samples/     SampleWriter, CoherenceMarkdown, CompactJson
Runtime/ (Relic.Items: Unity) ItemDebugInspector — add to a GameObject, use the context menu "Generate item"/"Generate boss"
Tests/   (Relic.Items.Tests) NUnit, pure logic: runs under `dotnet test tools/Relic.Tests` and Unity EditMode
```

## The item grammar (17)

An item is a sentence, resolved **before** anything else, in order:

```
Base → Archetype → Culture/Maker → Material → Theme (mood + motif) → Quality → (stats, effects, look) → Name → Lore
```

| Step | Where it comes from | What reads it |
|---|---|---|
| **Base** | `ItemSpec` or the seed (`item.base`) | slot, budget, kit allowance (05) |
| **Archetype** | `Archetypes.Pick(base, seed)`: Duelist, Warden, Hunter, Scholar, Wanderer, Smith, Herald, weighted per base (a staff is mostly a Scholar's, heavy armor a Warden's or Smith's) | stat lines, favored triggers, fallback statuses, the base noun ("Sabre", "Walking Staff") |
| **Culture / maker** | the drop region's family → `bible.CulturesOf(family)` (the region's own culture ×4, cultures that make the base ×3); maker named by the culture's `NameGenerator`, working in one of that culture's regions | the form ("carved longbow"), the name pattern, makers' marks, lore lines |
| **Material** | culture materials ∩ what the base is made of (`Materials.KindsFor`: a sword is metal, a tome paper or leather; relics by kit form) ∩ tier; generic tier materials fill gaps; a form that names its material ("whalebone bow") is made of it | tint, wear, glow, one small property tag (a tiny stat line: bell-bronze "rings true" → CHR) |
| **Theme** | `MoodPicker.Pick` (70/30 bright/somber) then `MoodPicker.MotifFor(mood, culture.Motifs)` | statuses and shapes (motif), glow color, decal, effect lean (mood), curse (somber `curse` always, `drawback` 40%) |
| **Quality** | rarity (Worn/Plain… Fine/Sturdy… Masterwork/Heirloom; somber Mended/Field-Worn) | wear, Common/Uncommon names |

Everything is a pure function of the item seed (each step has its own purpose salt). One exception: a culture's relic forms whose noun is overused in recent names (`NameFreshness`) are a third as likely, so names (already order-dependent and persisted) also steer forms away from repeats.

**Rarity layers** (`ItemSentence.LayersFor`), every field is resolved for every item but only these show:

| Rarity | Shows |
|---|---|
| Common | base · material · quality — plain name "Worn Steel Sword" |
| Uncommon | + one archetype stat line (`ItemStat.Source = "archetype"`) and the archetype's noun |
| Rare | + theme (motif statuses in effects, motif glow) and a real name from the culture's `itemNaming.patterns` (no `{maker}` patterns yet), a one-line flavor |
| Epic | + culture and maker, a 1–3 sentence lore line (a Bible mood line), one written enchantment |
| Legendary | + world references (a place, and a Bible legend or era), a lead toward another place, and an ultimate |

**Downstream rules:**
* **Stats** (`ArchetypeLines`): base primaries; trinkets carry the archetype's trained stat as their main line; Uncommon+ adds the archetype line (its first favored line the base allows); further secondaries are 3× likelier from the archetype's lines; every item gets its material's tag line. `Archetypes.Forbids`: staves and tomes never carry STR, heavy armor never AGI (lines *and* stat effects).
* **Effects** (`EnchantmentChooser`): about 80% of enchantments come from the archetype's favored templates (Duelist rider/exploit/riposte/executioner, Warden retort/aura/refuge/temper, Hunter longshot/death-burst/bosshunter, Scholar sentinel/echo, Wanderer trail/tailwind/refuge, Smith exploit/executioner/temper, Herald rally/hearth/aura); the rest follow the mood's `effectLean`; lean-matching templates are 2× likelier within either group. New templates for the bright leans: `hearth` (allies mend), `rally` (ally aura), `lamplight` (Insight), `tailwind` (haste), `longshot` (Hunter range). Statuses come from the motif, then the archetype (`TemplateContext.AllowedStatuses`). Drawbacks only on somber items.
* **Look** (`TableVisual`, `TemplateAuthor.Visual`): material name, wear (quality + material + mood), motif glow color and decal, kit parts from the relic's kit form. The LLM's visual must keep the material; the color is enforced.
* **Names and lore** (`TemplateWriting`): name from a culture pattern, always keeping the form's head noun; Rare flavor lines; Epic/Legendary lore from the mood's Bible lines (`{maker} {place} {culture} {form} {word} {legend} {faction}`, `TemplateText.Fill` + `FixArticles`), fitted to the style limits (≤ 22 words per sentence, ≤ 3 sentences); Legendary adds one lead sentence naming another region and a legend or era.

**Coherence validator** (`CoherenceValidator.Validate(item, world)`), next to `EffectValidator`: culture lives in the region's family; material kind fits the base and tier; stat lines (forbidden lines, archetype line, trinket main line, material tag); statuses fit motif ∪ archetype; no stat bonus a base forbids; drawbacks only when somber, and always for a `curse` mood (Epic+); theme/name/lore/maker/enchantment/world-ref/lead/ultimate layers match the rarity; visual material and glow follow the sentence. Messages read like `itm-… "Name" (Epic staff t4; Scholar · Aruvi (…) · amberwood · wonder/lantern · Masterwork): rolls Strength, which a staff never carries`. `CoherenceStats` aggregates the 80% trigger share and the mood split. `CoherenceTests` runs 3,000 items over 5 worlds; every one passes both validators.

### Lore Bible
* `LoreSource.Default` loads `Assets/Relic/Lore/Data` by walking up from the working/base directory (like the Lore tests). **Player builds** must call `LoreSource.Use(LoreBible.FromJson(...))` with TextAssets first (and optionally `LoreSource.UseStyleGuide(text)`); `StubWorld.FromBible(seed, bible)` takes one explicitly and the world carries it (`WorldContext.Bible`, not serialized).
* **StubWorld** (v2): 8 regions (every family once + one repeat), each settled by a Bible culture and named by its `NameGenerator`; 5 factions from the culture's faction archetypes (names filled from the seat region); 10 figures named in their culture's phonology with mostly bright fates; 6 events from the Bible's event templates at their weights, dated inside their era. Legends and eras are referenceable as `legend.<id>` / `era.<id>`.
* **StyleGuide**: Items' banned words plus `bible.Style.BannedWords`; `MaxLoreSentences`/`MaxSentenceWords` from `style.json`. **NameFreshness** bans words in more than 5% of the last 200 names, except the sentence's facts (form noun, maker, place).
* Items-side tables for fields the Bible doesn't have: `Materials` (kind, tier, tint, glow, wear, tag), `CultureForms` (culture form → base / kit form), `MotifTraits` (damage kind, world condition, decal), `Archetypes`. `GrammarTests` fails if a Bible material, motif or form has no row.

## Item pipeline

```
ItemSpec (world seed, index, tier, rarity, [base type], [region])
  → item seed = Hash.Combine(worldSeed, Hash.Of("item"), index); id = "itm-<hex>"
  → store hit? return the saved item (generated text is never regenerated)
  → base type → ItemGrammar.Resolve → the sentence (Item.Grammar)
  → stats from the sentence (StatRoller.Roll(sentence, ...)): uniform in [min, ceiling] + overroll steps
  → table effects: weapon skill (E) / armor or trinket passive, + one archetype-chosen effect (Uncommon 50%, Rare)
  → text step by rarity:
       Common/Uncommon  plain table name, no text model
       Rare             culture-pattern name + one-line flavor
       Epic             name + lore line + 1 written enchantment (budget = item budget − table effects)
       Legendary relic  name + lore with lead + 1–3 enchantments + ultimate (Q) + visual
  → parse strictly (unknown fields = error) → DraftValidator (+ DraftGrammar) → retry ≤ 3 with errors fed back
       → still failing: template relic (GenerationMode.LlmFallback)
  → ScaleToFit if only over budget; ultimate capped at 40% of the budget (see "Resonance pricing")
  → EffectValidator.Validate(effects, ItemGenerator.Budget(baseType, rarity, tier)) must pass → store
```

* **Determinism.** Everything except LLM text is a pure function of the seed (see the form exception above). Written text (template or LLM) is generated once and persisted (`IItemStore`; `JsonFileItemStore` writes one JSON file per item).
* **Forge reroll** (`ItemGenerator.Reroll`) re-samples stat *values* from sub-seed `Hash.Combine(itemSeed, rerollCount)` using the item's sentence; lines, effects and text are kept.
* **Overrolls.** `k = floor(Exponential(λ = 1))`, capped at 10 (hidden). A roll with k ≥ 1 is `ceiling × (1 + 0.05k)` (and always at least one display unit above the ceiling).
* **Stat tables** (`StatTables`): tier-0 Common ranges × rarity (1.0 / 1.05 / 1.1 / 1.15 / 1.2) × tier growth (power stats `PowerBudget.TierFactor` = 1 + 0.2t, trained-stat bonuses 1 + 0.1t, rates 1 + 0.04t). Attack speed is fixed per weapon type. Calibrated against `Relic.Game.Tests.PowerBandTests` (currently 6.65× top vs starter). Secondary lines: Uncommon +1, Rare +2, Epic/Legendary +3, plus the material tag.
* **Effect templates** are functions of a magnitude `s`; `EffectFitter` binary-searches the largest `s` whose tidied rule fits a target pp. Template mode fills 85–98% of the budget. Cursed relics add a drawback action (never on a Passive rule) or a standing "Price of Keeping" stat penalty.

### LLM step
* `ITextGenerator.GenerateAsync(TextRequest)`: messages, a JSON schema, sampler settings, and the structured `RelicBrief` (now with the `ItemSentence` and archetype) in `Context`.
* The **system prompt** carries the item-sentence rules, the full narration style guide (`docs/lore/style-guide.md` via `LoreSource.StyleGuideText`), the hard rules (banned words incl. the Bible's, name and sentence limits) and the effect grammar summary. The **user prompt** carries the sentence as facts (form and head noun, archetype and its triggers, material and tag, mood/lean, motif statuses/shapes/words, culture, maker and mark, the culture's naming patterns), the rolled stats, overused words, and the world context including Bible legends and eras with ids.
* **DraftGrammar** feeds back: a name without the form noun, capitalized words not in the world, the Bible or the sentence ("use only the names you are given"), sentences over 22 words, Legendary lore without a place, a Bible ref or a lead, and a visual material other than the sentence's. `worldRefs` is a closed enum of world and Bible ids.
* `LlamaServerGenerator`: llama.cpp `llama-server` over HTTP (`/v1/chat/completions` with `response_format: json_schema`, or `/completion` with a ChatML prompt). Failures become `TextGenerationException` and count as an attempt.
* `FakeGenerator`: canned responses (queue or function), records requests, can simulate transport failures. All pipeline tests use it. `TemplateGenerator` writes `TemplateAuthor` drafts as JSON so TEMPLATE MODE goes through the same parse → validate → fit path.

Running it for real (after the download is approved): start `llama-server -m models/<model>.gguf -ngl 99 -c 8192 --port 8080`, then `new ItemGenerator(world, new LlamaServerGenerator())`.

### Resonance pricing (workaround)
**Ultimates** are forced to `Resonance 100, cooldown 30 s` and fitted to their own allowance, `PowerBudget.Ultimate(tier)`. Since calibration v2, resource costs no longer discount power (the old `1 / (1 + cost / 40)` relief is gone), so an ultimate is priced by its cooldown alone.

## Bosses

`BossGenerator.Generate(worldSeed, index, tier, world, [archetype])` → `BossDefinition` (pure data, `ItemJson` round-trips).

* **Archetypes** (`BossArchetypes`): Librarian (INT), Warlord (STR), Oracle (INS), Beastmother (VIT), Tidecaller (VIT), Smith-King (STR), Duelist (AGI), Courtier (CHR). Each has an essence stat, block weights, statuses (never Rooted or Staggered), pattern names, a tell flourish, titles, arenas, hazards and lore templates.
* **Blocks** (`PatternLibrary`): Ring, Spiral, AimedBurst, Cone, Laser (line area), AreaDenial, Lobbed (airborne burst that leaves puddles), Wall (a barrier to hide behind, then a ring). Each is one Active rule. Farther tiers: more projectiles, faster shots, shorter wind-ups (never under 0.4 s); hit damage `10 × (1 + 0.4 × tier)`.
* **Phases**: 2 (tier 0–2), 3 (tier 3+), sometimes 4 (tier 7+), at evenly spaced health thresholds. Phase 1 has 2–3 base patterns; each later phase adds a new block and 1–2 **remixes** of earlier patterns: *fused* (A then B), *lingering* (shots leave puddles), *returning*, *shattering* (shots split), *doubled* (a second staggered wave). A remix must change the pattern's structure (`Readability.Signature`), not just its numbers. The rotation plays every pattern once per loop with a recovery window after each.
* **No enrage timers**: the model has no time-based fields; phases change on health only.
* **Essence**: archetype stat, `1 + tier / 3`.
* **Identity** (v2): a boss belongs to the culture of the region it guards (`BossDefinition.Culture`) and is named by that culture's `NameGenerator`. About 70% are **guardians** (canon: "many bosses are wild guardians rather than villains") with bright lore (`LoreTemplates`); the rest (`Tone = "Somber"`, `SomberLore`) are Quarrel constructs or things left alone in the Hush. World refs are scanned from the lore and title.

### Readability checker (`ReadabilityChecker.Check`)
Errors (every generated boss must have none; tested over 3,000 seeds):
1. Every Emit/CreateArea that carries damage has a wind-up (`delay`) ≥ 0.4 s.
2. Rings and spirals (waves released within 0.3 s are combined) leave gaps ≥ 2 × body width (1.2 m) at tier ≤ 3, ≥ 1.25 × body width (0.75 m) above tier 3, measured 6 m from the boss. Cones: the same gap at tier ≤ 3; above tier 3, the gap or room to step out of the fan. Cone areas must leave an opening.
3. Lobbed patterns must mark their shots `airborne` (flagged in the report so the runtime gives them shadows).
4. No unavoidable damage: no damage, status, pull or knockback applied directly to players (only via projectiles or areas); no Rooted/Staggered/Pull on players (they would make the next hit unavoidable); seeking shots no faster than 0.8 × run speed; aimed shots can be sidestepped (`run speed × (wind-up + travel time)` ≥ half the fan width + body); areas can be left during their tell (`run speed × tell + dash`).
5. Structure: 2–4 phases, thresholds falling from 100%, 2–4 patterns per phase, every pattern in the rotation, recovery ≥ 0.5 s, every later phase has a structural remix, all programs pass `EffectValidator`.

Player numbers assumed until the controller/runtime pins them: body width 0.6 m, run speed 6 m/s, dash 4 m.

### Boss health math
Power bands from 03-combat: starter gear = 1×, top gear for a tier = the item power band `P = 1 + 0.5 × tier` (6× at tier 10).

```
starter DPS (effective) = 10 dmg/hit × 1.5 hits/s × 0.6 uptime = 9
target with top gear T  = 2 + 0.3 × tier minutes            (5 min at tier 10)
base HP                 = 9 × P × T × 60
starter-gear time       = P × T minutes                       (30 min at tier 10)
group HP                = base HP × (1 + 0.8 × (players − 1))
```

| Tier | Solo HP | Top gear | Starter gear | 2 players | 4 players |
|---|---|---|---|---|---|
| 0 | 1,080 | 2.0 min | 2.0 min | 1,944 | 3,672 |
| 2 | 2,808 | 2.6 min | 5.2 min | 5,054 | 9,547 |
| 4 | 5,184 | 3.2 min | 9.6 min | 9,331 | 17,626 |
| 6 | 8,208 | 3.8 min | 15.2 min | 14,774 | 27,907 |
| 8 | 11,880 | 4.4 min | 22.0 min | 21,384 | 40,392 |
| 10 | 16,200 | 5.0 min | 30.0 min | 29,160 | 55,080 |

The 0.6 uptime is the knob to tune once fights are playable (the share of a fight spent hitting rather than dodging).

## Samples (RLC-014)

```
RELIC_SAMPLE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteSamples
```
writes `docs/samples/relics-<seed>.md` (50 Legendary relics, tiers cycling 0–10 and regions cycling through all eight, plus 12 ordinary drops; each item has a **Coherence** block with its sentence, and the top has the set's coherence numbers and mood split) and `docs/samples/bosses-<seed>.md` (10 bosses, tiers 0–10, every archetype). Both are marked **TEMPLATE MODE — not LLM output** until a model is approved. `SampleSetIsValid` checks the same set in the normal test run. To write LLM samples later, pass a `LlamaServerGenerator` to `SampleWriter.GenerateRelics(seed, generator)`.

## Model benchmark (pending download approval, RLC-026)
Harness: `LlmBenchmark.Run(generator, world, seed, relics)` runs N Legendary relics through the real pipeline and reports valid-JSON rate, first-try acceptance, retries per relic, fallbacks and seconds per relic (`BenchmarkReport.ToMarkdownRow`). VRAM: read `nvidia-smi` during the run. Plan: 30 relics per model, chat endpoint, `-ngl 99 -c 8192`, the game closed.

| model | relics | valid JSON | accepted first try | retries / relic | fallbacks | s / relic | VRAM |
|---|---|---|---|---|---|---|---|
| Qwen2.5-3B-Instruct Q4_K_M (~2 GB) | — | — | — | — | — | — | — |
| Qwen2.5-7B-Instruct Q4_K_M (~4.7 GB) | — | — | — | — | — | — | — |
| Llama-3.1-8B-Instruct Q4_K_M (~4.9 GB) | — | — | — | — | — | — | — |

Expectation (to verify): with schema-constrained decoding the valid-JSON rate should be ~100% for all three; the differences will be first-try acceptance (style guide, world references, grammar sense) and speed. Provisional pick: a 7B at Q4 for offline batch pre-generation, a 3–4B at Q4 for in-game top-ups.

## Grammar gaps hit (requests for the lead)
1. **Summon action** for bosses (blocks list "summons (later)").
2. **Area anchor**: an area can follow the wearer or sit "at the target"; there's no "here, static" (dash trails) and no explicit orientation for cone/line areas (from source toward target is assumed).
3. **Emitter angle offset / spin direction**: interleaved rings and counter-rotating spirals can't be expressed, so "doubled" remixes repeat the same angles.
4. ~~Resonance pricing~~: resolved in calibration v2 (no resource relief; ultimates have their own allowance).
5. ~~Budget scale check~~: resolved in calibration v2. Bonus budgets are now 0.5–3.0 pp × (1 + 0.2·tier), weapon skills and ultimates have their own allowances, and PowerBandTests confirms about 7× top-vs-starter.
6. **Passive auras**: a Passive CreateArea has a duration (≤ 30 s); confirm it means "always on while equipped".
7. Player numbers for readability (body width, run speed) and a player HP baseline for boss hit damage.

## Known limitations
* Template prose has limited variety (three Bible lines per mood, eight bright and four somber leads): repetition across 50 relics is expected; it's a stand-in for the LLM.
* Player builds need `LoreSource.Use(...)` before generating (no Assets folder to read the Bible from). `KitRoller` (Character) calls `StubWorld.Create(seed)`, which uses `LoreSource.Default`.
* New material names (amberwood, bell-bronze, sea-glass...) are not in Character's `MaterialPalette`; it falls back to its defaults. `Materials.Find(name).Tint` has a color for each.
* The somber "points toward ... nobody will say why" lead names no Bible ref; it is only used when the mood line already names one.
* The checker measures gaps at one reference distance and treats spirals as rings (conservative); it doesn't simulate overlap between consecutive patterns in a rotation (the recovery window guards that) or arena hazards.
* Derived stats (attack speed, crit, max HP...) are item-level numbers; the runtime that applies them comes at integration.
