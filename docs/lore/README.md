# The lore constitution

Relic's world is infinite and its story is written as it happens, so nothing here is a fixed place, person or date. These pages hold the **rules and the templates**. Each world seed instantiates them into its own cultures and short prehistory, the Chronicler (doc 20) writes history as players act, and the LLM narrates within all of it (design doc 16, decision 2026-11-18).

**Tone in one line:** bright medieval fantasy (wonder, warmth, craft, community, adventure, gentle humor), with darkness as an accent. About 70% of generated moods are bright and 30% somber.

## What's here

| File | What it is |
|---|---|
| [`constitution.md`](constitution.md) | The mechanical truths in one line each (resonance, relics, essences, written magic and the Magister, graves, sanctuaries, wilding, the first hearth) and what each world generates. |
| [`culture-templates.md`](culture-templates.md) | The ten culture templates, the seven phonology families, item-form tags, and how `CultureGenerator` builds 5–8 cultures per world. |
| [`templates.md`](templates.md) | Era, faction and event templates with the slots the generators fill. |
| [`style-guide.md`](style-guide.md) | How narration should sound: rules, banned words, reading level, good and bad examples. |
| [`samples.md`](samples.md) | One generated world (seed 2026) in template mode, for tone review. |

The **structured data** lives in `Assets/Relic/Lore/Data/` and is the source of truth for code:

| Data | Contents |
|---|---|
| `culture-templates.json` | Culture templates: region families, climates, phonologies, crafts, materials, metals, foods, festivals, values, factions, motifs, item forms, naming, mood lines |
| `phonology.json` | Phonology families: syllable sets, patterns, place naming, adjective endings |
| `forms.json` | Item-form tags: base type and relic kit per form |
| `prehistory.json` | Era templates (position, tone, names, summaries, echoes) and founder, artifact and figure text |
| `factions.json` | Faction archetypes (kind, tone, weight, name patterns, goals, allies/rivals/opposes) |
| `events.json` | Event templates (kind, tone, weight, era templates, typed slots, name patterns, summary, traces left) |
| `motifs.json` | Motifs: words, statuses (03), shapes (17), tones, moods, favored stat, color, materials, families |
| `moods.json` | The bright share (0.7) and the moods with name words, effect leanings and lore lines |
| `style.json` | Banned narration words, banned name fragments, sentence limits |

## How generators use it

`Assets/Relic/Lore/Logic` (assembly `Relic.Lore.Logic`, pure C#, no UnityEngine, no static mutable state):

- **`LoreBible.LoadDirectory(path)`** for tools and tests, or **`LoreBible.FromJson(docs)`** (file name → text) for Unity `TextAsset`s, loads the **constitution**. Loading is strict: unknown fields, unknown enum names, duplicate ids, dangling references, untagged forms and undeclared template slots all throw a `LoreDataException` listing every problem.
- **`bible.ForWorld(seed)`** returns that world's bible: the same rules plus generated `Cultures` (**`CultureGenerator`**), `Eras` and `Legends` (founders, figures, artifacts) and `History` (**`WorldPrehistory`**: eras, founders, events, places). Deterministic from the seed; the constitution is never changed.
- **`NameGenerator(culture, style)`**: deterministic names on `Pcg32`. **All proper nouns in the world come from here.** The LLM uses them but never invents its own.
- **`MoodPicker`** honors the 70/30 rule; **`TemplateText`** fills slots strictly; **`LoreSampler` / `LoreSamples`** build `samples.md` from a world bible.

Items uses it through `LoreSource.Default` (the constitution) and `LoreSource.ForWorld(seed)`; `StubWorld.Create(seed)` builds its regions, factions, figures and events from the world's generated lore.

## How to extend it

- **A culture template, phonology family or form tag:** see [`culture-templates.md`](culture-templates.md).
- **An era, faction or event template, a motif or a mood:** add an entry to the JSON. Template slots must be declared and used. Keep somber entries at low weight so they stay an accent. The bright share stays at 0.7 unless Greg changes the rule.
- **Mechanical truths** change only with Greg's approval (`constitution.md`).
- **After any Data change:** run `bash tools/add-metas.sh` for new files, then `dotnet test tools/Relic.Tests`, and regenerate the samples with
  `RELIC_LORE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteLoreSamples` (and the item samples with `RELIC_SAMPLE_SEED=2026 dotnet test tools/Relic.Tests --filter Name=WriteSamples`).
