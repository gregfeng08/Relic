# The Lore Bible

Relic's world is infinite, so nobody can write it place by place. Instead, this Bible writes the **rules and the palettes**: the fixed truths, the peoples, and the kinds of things that happen. Generators instantiate them by seed, and the LLM narrates within them (design doc 16).

**Tone in one line:** bright medieval fantasy (wonder, warmth, craft, community, adventure, gentle humor), with darkness as an accent. About 70% of generated moods are bright and 30% somber.

## What's here

| File | What it is |
|---|---|
| [`canon.md`](canon.md) | Fixed truths and their in-world explanations: the Listening, relics, essences and the Soulbinder, glyph magic and the Magister, graves and the Reliquary, sanctuaries, the Lanternfold, four eras of history, ten legends. |
| [`cultures.md`](cultures.md) | Seven cultures, one per World region family, with name phonology, architecture, crafts, food, festivals, values, factions, item forms, naming and relations. |
| [`templates.md`](templates.md) | Faction archetypes and event templates with slots for the world graph to fill. |
| [`style-guide.md`](style-guide.md) | How narration should sound: rules, banned words, reading level, good and bad examples. |
| [`samples.md`](samples.md) | Template-mode output from the data and name generator, for tone review. |

The **structured data** behind these pages lives in `Assets/Relic/Lore/Data/` and is the source of truth for code:

| Data | Contents |
|---|---|
| `Cultures/<id>.json` | One culture per file: phonology, families, crafts, festivals, item forms, motif ids, naming, relations |
| `factions.json` | Faction archetypes (kind, tone, weight, name patterns, goals, allies/rivals/opposes) |
| `events.json` | Event templates (kind, tone, weight, eras, typed slots, name patterns, summary, traces left) |
| `motifs.json` | Motifs: words, physical statuses (03), shapes (17), tones, moods, favored stat, color, materials, families |
| `moods.json` | The bright share (0.7) and 13 moods with name words, effect leanings and template lore lines |
| `eras.json` | The four eras with year ranges and the echoes every region can show |
| `legends.json` | Ten legendary figures and artifacts |
| `style.json` | Banned narration words, banned name fragments, sentence limits |

## How generators use it

`Assets/Relic/Lore/Logic` (assembly `Relic.Lore.Logic`, pure C#, no UnityEngine):

- **`LoreBible.LoadDirectory(path)`** for tools and tests, or **`LoreBible.FromJson(...)`** for Unity `TextAsset`s. Loading is strict: unknown fields, unknown enum names, duplicate ids, dangling references and undeclared template slots all throw a `LoreDataException` that lists every problem.
- **`NameGenerator(culture, style)`**: `Person(seed)`, `Place(seed)`, `Word(ref rng, shape)`. Deterministic on `Pcg32`, and every name passes the plausibility rules (letters only, length bounds, no tripled letters or stutters, no banned fragments). **All proper nouns in the world come from here.** The LLM uses them but never invents its own.
- **`MoodPicker(bible.Moods)`**: `Pick(ref rng)` honors the 70/30 rule; `Pick(ref rng, brightShare)` lets a place that calls for it (a Quarrel battlefield, a villain's hall) lean darker on purpose. `MoodPicker.MotifFor(...)` picks a motif that can carry the mood, preferring the culture's own.
- **`TemplateText`**: strict slot filling (a missing value throws, so "{place}" can never leak into the game), banned-word checks, article fixing.
- **`LoreSampler` / `LoreSamples`**: template-mode instantiation used for `samples.md`. It's a stand-in for the world graph and shows how the pieces combine.

Pipeline, from the world graph's point of view:
1. A region gets a **culture** from its family (`bible.CulturesOf(family)`), or a blend of two near borders.
2. **Factions** come from the culture's archetype list, and **events** from templates whose eras fit; slots are filled with graph entities.
3. Each place or item draws a **mood** (70/30) and a **motif** that fits it.
4. The **LLM narrates** names, lore and inscriptions with `style-guide.md` as the system prompt and the local subgraph as context.

## How to extend it

- **A new culture:** add `Data/Cultures/<id>.json` (copy an existing one), give it at least one region family, point its `factions` and `motifs` at existing ids, and add a section to `cultures.md`. Run the tests: they check references, phonology sanity and that names stay plausible over 2,000 seeds.
- **A new faction archetype, event template or motif:** add an entry to the JSON. Template slots must be declared and used. Keep somber entries at weight 1 so they stay an accent.
- **A new mood:** add it to `moods.json` with a tone, weight, name words and at least one lore line. The bright share stays at 0.7 unless Greg changes the rule.
- **New canon:** canon changes need Greg's approval. Edit `canon.md` first, then `eras.json` and `legends.json`.
- **After any Data change:** run `bash tools/add-metas.sh` for new files, then `dotnet test tools/Relic.Tests`, and regenerate the samples with
  `RELIC_LORE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteLoreSamples`.
