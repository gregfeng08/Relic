# 08 — Items, Relics & Bosses (generation)

Status: **v1 — brief for stream I** · Last updated 2026-10-13
Depends on: 05-effect-grammar (contract), 02-core-loop (rolls, essences), 03-combat (slots, statuses, power bands, readability), 06 (LLM runtime).

## Item model (`Items/Logic`)
```
Item
  id            stable string (from seed + generation index)
  baseType      Sword · Greatsword · Bow · Staff · Tome · Armor(Light|Medium|Heavy) · Trinket · Relic
  slot          Weapon · Relic · Armor · Trinket
  rarity        Common … Legendary (Core.Effects.Rarity)
  tier          0–10 (region difficulty where it dropped)
  stats[]       { stat (Core.Stats.StatId or derived like AttackSpeed), value, ceiling, overrollSteps }
  effects       EffectProgram: the weapon skill, relic enchantments/ultimate, armor passives
  lore          (relics and epics) name, history paragraph, worldRefs[] (place/faction/person ids)
  visual        { kitParts[], material, wear, emissiveColor, decal, emissiveIntensity }
  memory        (relics) counters: kills by enemy type, bosses, places visited; awakening state
  generator     { name, version } for save compatibility
```

## Stat rolls with overrolls (02-core-loop)
- Each stat rolls uniformly in `[min, ceiling]` for its base type, rarity and tier.
- Then **overroll steps** `k = floor(Exponential(λ))` with **λ = 1**, so P(k ≥ n) = e^−n. Each step adds **+5%** over the ceiling.
- **Hidden safety cap: k ≤ 10.**
- **Forge reroll** = fresh sample with a new sub-seed (`Hash.Combine(itemSeed, rerollCount)`).
- A test must check the empirical distribution against e^−n.

## Generation by tier

| Rarity | Pipeline |
|---|---|
| Common / Uncommon | Deterministic tables: base type, stats, maybe one simple effect from a template list. **No LLM.** |
| Rare | Tables + LLM **name and one-line flavor** only. |
| Epic | Tables + LLM lore paragraph + **one LLM-composed enchantment** (EffectProgram rule). |
| Legendary (relic) | LLM lore that refers to world context, **1–3 enchantments**, an ultimate (Active rule on the relic), and visual parameters. |

## LLM step (relics and epics)
1. **Request** (deterministic from the seed): rarity, tier, base type, a **theme** (e.g. "water, betrayal"), a budget in pp, and **world context**. Until the world graph exists, use a **stub world context**: a small seeded set of fake regions, factions and figures in JSON.
2. **Prompt** with a system prompt that includes the grammar summary from 05, the style guide, and the forbidden-words list (below). The output is **constrained by a JSON schema**: `{ name, lore, worldRefs[], enchantments: EffectProgram, ultimate: EffectRule, visual }`.
3. **Parse** with `EffectJson` (strict), **validate** against the budget, **ScaleToFit** if it's only over budget. On structural errors, retry up to 3 times with the errors fed back, then fall back to a template relic.
4. **Persist** the result (06: LLM output is generated once and saved).

**Style guide.** Names and lore must sound like *this* world, not generic fantasy. Use the stub world's vocabulary. Ban stock words: *ethereal, shadow-whisper, whispering, eternal, ancient power, doom, void*, and any word appearing in more than 5% of recent names (track this). Lore is 2–4 sentences: concrete, historical, with at least one named place or person from the world context.

## Bosses
- **Archetype** (seeded): Librarian, Warlord, Oracle, Beastmother, Tidecaller, Smith-King, … Each defines:
  - its essence stat (Librarian → INT, Warlord → STR, Oracle → INS, Beastmother → VIT, Duelist → AGI, Courtier → CHR);
  - a pattern palette (preferred emit shapes, statuses, arena hazards);
  - a lore template.
- **Pattern building blocks** are EffectPrograms: rings, spirals, aimed bursts, cones, lasers (a line area), area denial, lobbed airborne shots, summons (later).
- **Phases:** 2–4, each a set of 2–4 patterns with a rotation schedule. Later phases remix earlier patterns, not just faster versions of them.
- **Readability checker (required):** every damaging pattern has a wind-up of ≥ 0.4 s; Ring/Cone patterns at tier ≤ 3 leave a gap ≥ 2× body width; airborne shots are flagged. **No unavoidable damage.**
- **Group scaling:** `bossHP = baseHP × (1 + 0.8 × (players − 1))`. Base HP comes from the tier so a solo player kills it in about 5 min with top gear and about 30 min with starter gear (03-combat power bands; document the math).
- **No enrage timers.**
- Output: a `BossDefinition` (pure data: name, archetype, tier, HP, phases → patterns, essence drop, lore) that serializes to JSON.

## Deliverables
- `Items/Logic`: item model, roll tables, overrolls, rerolls, theme picker, stub world context, `ITextGenerator` + `LlamaServerGenerator` + `FakeGenerator`, relic pipeline, boss generator, readability checker. **Tested with FakeGenerator** (no model needed in CI).
- **A sample-generation tool**: a console entry point or EditMode test runner method, plus a short README, that generates **50 relics and 10 bosses** for a seed and writes them to `docs/samples/relics-<seed>.md` and `bosses-<seed>.md` for Greg to read. That review is RLC-014: "are these exciting on paper?"
- **A model benchmark note** comparing 2–3 small GGUF models: valid-JSON rate, retries, seconds per relic, VRAM. Recommend one.
- `Items/Runtime`: optional for now, a debug inspector to view a generated item or boss JSON in the editor.
- `Assets/Relic/Items/README.md`.

## Not in scope yet
Drop tables per enemy, vendors and prices, item memory/awakening runtime (just the data model), the visual kitbash renderer (just the parameters), and the runtime combat executor.
