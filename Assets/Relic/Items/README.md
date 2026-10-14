# Relic.Items: items, relics and bosses (stream I)

Brief: `docs/design/08-items-bosses.md`. Contract used (not edited): `Relic.Core` effect grammar, `PowerBudget`, `EffectValidator`, `Pcg32`/`Hash`.

```
Logic/   (Relic.Items.Logic: pure C#, compiled by the dotnet harness too)
  Model/       Item, ItemStat, ItemLore, ItemVisual, ItemMemory, GeneratorInfo, ItemJson
  Rolls/       StatTables (roll tables), StatRoller, Overroll
  Lore/        StubWorld + WorldContext, Themes + ThemePicker, StyleGuide + NameFreshness, Forms (relic forms, kit parts, table names)
  Effects/     EffectTemplates (weapon skills, passives, enchantments, ultimates, curses), EffectFitter, EffectDescriber
  Text/        ITextGenerator, LlamaServerGenerator, FakeGenerator, TemplateGenerator/TemplateAuthor,
               Prompts, JsonSchemaBuilder, RelicBrief/RelicDraft/DraftValidator
  Generation/  ItemGenerator (the pipeline), IItemStore (+ in-memory and JSON-file stores), LlmBenchmark
  Bosses/      BossDefinition, BossHealth, BossArchetypes, PatternLibrary, BossGenerator, ReadabilityChecker
  Samples/     SampleWriter (markdown samples), CompactJson
Runtime/ (Relic.Items: Unity) ItemDebugInspector — add to a GameObject, use the context menu "Generate item"/"Generate boss"
Tests/   (Relic.Items.Tests) NUnit, pure logic: runs under `dotnet test tools/Relic.Tests` and Unity EditMode
```

## Item pipeline

```
ItemSpec (world seed, index, tier, rarity, [base type], [region])
  → item seed = Hash.Combine(worldSeed, Hash.Of("item"), index); id = "itm-<hex>"
  → store hit? return the saved item (generated text is never regenerated)
  → base type, theme (Rare+), home region, form       (Pcg32 per purpose: "item.base", "item.theme", ...)
  → stats: uniform in [min, ceiling] + overroll steps   (StatRoller)
  → table effects: weapon skill (E) / armor or trinket passive, + a simple rider (Uncommon 50%, Rare)
  → text step by rarity:
       Common/Uncommon  table name, no text model
       Rare             name + one-line flavor
       Epic             name + lore + 1 written enchantment (budget = item budget − table effects)
       Legendary relic  name + lore + 1–3 enchantments + ultimate (Q) + visual
  → parse strictly (unknown fields = error) → DraftValidator → retry ≤ 3 with errors fed back
       → still failing: template relic (GenerationMode.LlmFallback)
  → ScaleToFit if only over budget; ultimate capped at 40% of the budget (see "Resonance pricing")
  → EffectValidator.Validate(effects, PowerBudget.For(rarity, tier)) must pass → store
```

* **Determinism.** Everything except LLM text is a pure function of the seed. Name freshness (below) makes names depend on generation *order* too, so written text (template or LLM) is treated like LLM output: generated once and persisted (`IItemStore`; `JsonFileItemStore` writes one JSON file per item).
* **Forge reroll** (`ItemGenerator.Reroll`) re-samples stat *values* from sub-seed `Hash.Combine(itemSeed, rerollCount)`; which stat lines an item has, its effects and its text are kept.
* **Overrolls.** `k = floor(Exponential(λ = 1))`, capped at 10 (hidden). A roll with k ≥ 1 is `ceiling × (1 + 0.05k)` (and always at least one display unit above the ceiling). Test: empirical P(k ≥ n) within 5% of e^−n for n = 1..4 over 400k samples.
* **Stat tables** (`StatTables`): tier-0 Common ranges × rarity (1.0 / 1.15 / 1.3 / 1.5 / 1.75) × tier growth (power stats 1 + 0.5t, trained-stat bonuses 1 + 0.2t, rates 1 + 0.04t). Secondary lines: Uncommon +1, Rare +2, Epic/Legendary +3.
* **Effect templates** are functions of a magnitude `s`; `EffectFitter` binary-searches the largest `s` whose tidied rule (whole damage, half-second durations) fits a target pp. Template mode fills 85–98% of the budget. Cursed relics (chance set by the theme's mood) add a drawback action or a standing "Price of Keeping" stat penalty; the negative cost buys power for the other rules.

### Themes, world context, style guide
* **Theme** = motif (tide, ember, frost, thorn, storm, venom, stone, bell, ink, blood, salt, moth: decides statuses, damage kind, colors, materials, vocabulary) · mood (betrayal, vigil, grief, oath, hunger, exile, pride, mercy, debt, homecoming: decides the story and the curse chance). Motifs that belong to the drop region's tags are 3× as likely.
* **StubWorld** builds 8 regions (with tiers, tags and a landmark), 5 factions, 10 people (with a fate) and 6 events from the seed, until the world graph exists. Lore must name at least one of them and list its ids in `worldRefs`; every relic's lore ends with a lead to another place or person (pillar 2).
* **StyleGuide**: banned stock words (ethereal, shadow-whisper, whisper(ing), eternal, ancient power, doom, void), lore 2–4 sentences. **NameFreshness** bans any word that would appear in more than 5% of the last 200 names (with fewer than 20 names, no word repeats), exact repeats, and (template mode) the same words reordered.

### LLM step
* `ITextGenerator.GenerateAsync(TextRequest)`: messages, a JSON schema, sampler settings, and the structured `RelicBrief` in `Context`.
* `LlamaServerGenerator`: llama.cpp `llama-server` over HTTP. Default `http://127.0.0.1:8080`, `/v1/chat/completions` with `response_format: {type: "json_schema", json_schema: {schema}}` (the server applies the model's chat template), or `/completion` with `json_schema` and a ChatML prompt. Configurable URL, endpoint, timeout (default 120 s), API key; `IsReadyAsync()` polls `/health`. Failures become `TextGenerationException` and count as an attempt.
* The **schema** (`Prompts.Schema`) is built by reflection from the Core grammar classes (`JsonSchemaBuilder`), so it can't drift from what `EffectJson` accepts. Payload recursion is unrolled to depth 3 (llama.cpp's schema-to-grammar converter dislikes recursive `$ref`s). World refs, kit parts and decals are closed enums.
* The **system prompt** contains a grammar summary generated from the Core enums, the style guide and the budget rules; the user prompt has the brief (rarity, tier, form, theme, budget, curse, rolled stats, overused words) and the world context with ids.
* `FakeGenerator`: canned responses (queue or function), records requests, can simulate transport failures. All pipeline tests use it.
* `TemplateGenerator` writes `TemplateAuthor` drafts as JSON so TEMPLATE MODE goes through the same parse → validate → fit path.

Running it for real (after the download is approved): start `llama-server -m models/<model>.gguf -ngl 99 -c 8192 --port 8080`, then `new ItemGenerator(world, new LlamaServerGenerator())`.

### Resonance pricing (workaround)
`PowerBudget` gives every rule a resource "relief" of `1 / (1 + cost / 40)`. An ultimate costs one full resonance bar (100), so it is priced at 29% of its power, on top of its 30 s cooldown. To keep ultimates from swallowing a relic, the pipeline forces every ultimate to `Resonance 100, cooldown 30 s` and caps its *unrelieved* cost (`EffectFitter.UnrelievedCost`) at 40% of the budget, shrinking it with `ScaleToFit` if needed. The samples show both numbers. Proper fix requested from the lead (see below).

## Bosses

`BossGenerator.Generate(worldSeed, index, tier, world, [archetype])` → `BossDefinition` (pure data, `ItemJson` round-trips).

* **Archetypes** (`BossArchetypes`): Librarian (INT), Warlord (STR), Oracle (INS), Beastmother (VIT), Tidecaller (VIT), Smith-King (STR), Duelist (AGI), Courtier (CHR). Each has an essence stat, block weights, statuses (never Rooted or Staggered), pattern names, a tell flourish, titles, arenas, hazards and lore templates.
* **Blocks** (`PatternLibrary`): Ring, Spiral, AimedBurst, Cone, Laser (line area), AreaDenial, Lobbed (airborne burst that leaves puddles), Wall (a barrier to hide behind, then a ring). Each is one Active rule. Farther tiers: more projectiles, faster shots, shorter wind-ups (never under 0.4 s); hit damage `10 × (1 + 0.4 × tier)`.
* **Phases**: 2 (tier 0–2), 3 (tier 3+), sometimes 4 (tier 7+), at evenly spaced health thresholds. Phase 1 has 2–3 base patterns; each later phase adds a new block and 1–2 **remixes** of earlier patterns: *fused* (A then B), *lingering* (shots leave puddles), *returning*, *shattering* (shots split), *doubled* (a second staggered wave). A remix must change the pattern's structure (`Readability.Signature`), not just its numbers. The rotation plays every pattern once per loop with a recovery window after each.
* **No enrage timers**: the model has no time-based fields; phases change on health only.
* **Essence**: archetype stat, `1 + tier / 3`.

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
writes `docs/samples/relics-<seed>.md` (50 Legendary relics, tiers cycling 0–10, plus 12 ordinary drops) and `docs/samples/bosses-<seed>.md` (10 bosses, tiers 0–10, every archetype). Both are marked **TEMPLATE MODE — not LLM output** until a model is approved. `SampleSetIsValid` checks the same set in the normal test run. To write LLM samples later, pass a `LlamaServerGenerator` to `SampleWriter.GenerateRelics(seed, generator)`.

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
4. **Resonance pricing**: price `CostResource = Resonance` by charge time instead of the generic relief (see above).
5. **Budget scale check**: Legendary at tier 0 is 25 pp ≈ 62 extra DPS-equivalent against a starter weapon's ~15 DPS (≈ 6 pp), so a full-budget relic is ~4× a weapon at every tier and template numbers get large (e.g. 40–50 damage on-hit procs at tier 0). Intended?
6. **Passive auras**: a Passive CreateArea has a duration (≤ 30 s); confirm it means "always on while equipped".
7. Player numbers for readability (body width, run speed) and a player HP baseline for boss hit damage.

## Known limitations
* Template prose has limited variety (two origin sentences per mood, one detail per motif): repetition across 50 relics is expected; it's a stand-in for the LLM.
* The checker measures gaps at one reference distance and treats spirals as rings (conservative); it doesn't simulate overlap between consecutive patterns in a rotation (the recovery window guards that) or arena hazards.
* Derived stats (attack speed, crit, max HP...) are item-level numbers; the runtime that applies them comes at integration.
