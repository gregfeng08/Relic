# 16 — Lore & Tone: the Lore Bible

Status: **PROPOSAL — needs Greg's review** · Last updated 2026-10-28

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
