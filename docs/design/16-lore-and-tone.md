# 16 — Lore & Tone: the Lore Bible

Status: **AGREED; story wiped 2026-11-18, procedural lore BUILT (RLC-058)**: the Bible is now a lean constitution plus per-world generated cultures and prehistory, and the seed for a living Chronicler agent (20) · Last updated 2026-11-18

## Tone (Greg, 2026-10-28)
The template samples read "too Elden Ring-coded." Relic is **bright medieval fantasy**: wonder, warmth, craft, community and adventure, with light humor. **Darkness is an accent, not the baseline.** Ruins, old wars and the occasional villain exist so the bright parts mean something.

- **Touchstones (proposed):** Genshin's Mondstadt and Liyue, Zelda: Breath of the Wild and Tears of the Kingdom, Witch Hat Atelier, Ni no Kuni, Studio Ghibli, Dragon Quest
- **Avoid:** Dark Souls / Elden Ring grimness, debt-and-doom as the default mood, every relic tied to a death

**Mood weights for anything generated:** about 70% bright (wonder, craft, kinship, celebration, journey, discovery, friendly rivalry, legacy, homecoming) and about 30% somber (loss, old war, exile, curse), used where a place or boss calls for it.

## Can one agent write the lore for an infinite world?
Yes, if it writes the **rules and palettes**, not the places. An infinite world can't be hand-written, but its **canon** and **ingredients** can be. Then the world graph (10) instantiates them by seed, and the LLM narrates within them.

### Layer 1: Canon (fixed, written once, approved by Greg)
- **What magic is.** The in-world explanation (pillar 7) for:
  - **relics:** why old objects hold power and resonance;
  - **essences and soulbinding:** what a boss essence is, and why it binds to a soul;
  - **glyph magic:** why drawn symbols work, and why anyone can learn them;
  - **death and graves:** why your items stay where you fell.
- **The origin sanctuary:** where every journey starts, and why it's safe.
- **A history skeleton:** 3–4 eras (for example an Age of Wonders, a Great Quiet, the Rekindling, the present), as a few sentences each, with the big events that every region can echo.
- **A handful of legendary figures and artifacts** that recur across the world: the "famous names" that relics and NPCs can reference anywhere.

### Layer 2: Palettes (what the generator draws from)
- **6–8 cultures.** Each one defines:
  - a name phonology (syllable sets), so names within a culture sound related;
  - architecture, materials, crafts, food and festivals;
  - its values and what it's proud of;
  - typical factions, item forms and motifs;
  - which region families and climates it favors.
- **Faction archetypes:** guilds, orders, households, companies and circles, each with goals and friendly or unfriendly relations.
- **Event templates:** founding, festival, great discovery, invention, friendly rivalry, rescue, a migration, a lost expedition, and occasionally a disaster or war.
- **Item vocabulary per culture:** forms, materials, makers' marks and naming patterns (feeds 17-item-grammar).

### Layer 3: Instantiation (procedural, by seed)
1. Each region gets a **culture**, or a blend of two near borders and passes.
2. **Factions, figures and events** are drawn from templates and linked into the world graph (10).
3. The **LLM narrates** names, lore and inscriptions using the tone guide, the culture's vocabulary and the local subgraph. It never invents structure.

## Process
1. **A Lore agent drafts the Bible** (`docs/lore/`): canon, cultures and templates as structured data plus readable prose.
2. **Greg reviews the tone and cultures** (the main feel review).
3. The Items stub world and the template moods are **replaced by Bible data**, and the sample relics are regenerated. Later, the world graph (M2) consumes it too.

## Decision 2026-11-18: wipe the story, keep the rules (Greg)
Lore is now **procedural and event-driven** (20: the Chronicler). The hand-written Bible is cut down to a lean **constitution**:
- **Keep (the rules):**
  - the tone and style guide (bright medieval fantasy, 70/30 moods, banned words);
  - a few one-line mechanical truths that ground the systems (pillar 7): loved, used things gain resonance, which is why relics, essences, graves and sanctuaries work;
  - the name phonology generators;
  - faction, event and motif *templates*.
- **Wipe (the story):** the four eras, the ten legends, Corvane and the Quarrel, specific festivals, fixed place names (the Lanternfold and the Old Hearth become generated or generic: "the first hearth"), and the seven fixed cultures as *content*.
- **Procedural cultures:** each world generates its own cultures from culture templates (climate affinities, crafts, material palettes, values, phonology families). They keep the same data shape Items consumes today, so the item grammar keeps working.
- **World creation:** a short procedural prehistory (a few generated eras, founders and relic-worthy events), so the first relics have something to remember. After that, the Chronicler writes history as players act. Like the wayfarer, *the world starts mostly unwritten*.

### Built (RLC-058, 2026-11-18)
What exists now (`Assets/Relic/Lore`, `docs/lore/`):
- **The constitution** (`docs/lore/constitution.md`): tone and the 70/30 rule, one-line mechanical truths (resonance → relics, essences, graves, sanctuaries, wilding; magic is *written* incantations, i.e. RelicScript, read by the impartial Magister; the generated *first hearth*), plus the style guide. `canon.md` and `cultures.md` are deleted, and so are `eras.json`, `legends.json` and the seven culture files. A test fails if any wiped name (the old cultures, Corvane, the Quarrel's Imperative, the Lanternfold, Wren…) returns to the data.
- **Templates as data:** 10 culture templates and 7 phonology families (`culture-templates.json`, `phonology.json`, `docs/lore/culture-templates.md`), item-form tags (`forms.json`: base type and relic kit, so Items maps any shield form onto the shield kit), 5 era templates (`prehistory.json`), and the faction, event, motif and mood templates, scrubbed of fixed names (event templates now list era *templates*; mood lines gain an `{era}` slot).
- **`CultureGenerator`:** 5–8 cultures per world seed, same `CultureDef` shape Items consumes (plus `Template`, `PhonologyFamily`, a two-sentence `Mood` and `FormTags`). Each has at least one metal and two relic forms; every region family is claimed; neighbors get relations.
- **`WorldPrehistory`:** 3–4 generated eras (a founding era first, a waking era last, at most one strife era), one founder per culture, 1–2 events per era from the event templates, and the figures and relic-worthy artifacts they leave (the waking era always opens with a find). Founders, figures and artifacts are the world's legends; places become region names.
- **`LoreBible.ForWorld(seed)`** returns a world's bible (rules shared, constitution never mutated); everything is deterministic from the seed and covered by tests (determinism, 5–8 cultures, metals, phonology distinctness, prehistory shape, tone).
- **Items:** `StubWorld` builds from the world's lore (regions named after prehistory places, events from the prehistory, `legend.*`/`era.*` refs now point at generated founders, artifacts and eras); `CultureForms` reads the form tags; coherence and PowerBand tests stay green. Samples: `docs/lore/samples.md`, `docs/samples/relics-2026.md`, `docs/samples/bosses-2026.md`.
- **Not yet:** the Chronicler (20) writing history after world creation; the real world graph (10) consuming the generated cultures and prehistory; a generated name for the origin sanctuary in the world itself (the constitution only promises a *first hearth*). Other design docs (19, 20) still mention the old names in passing.
