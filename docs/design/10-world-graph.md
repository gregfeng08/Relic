# 10 — World Graph & Lore

Status: **DRAFT v1 (lead)** · Last updated 2026-10-13
Pillar 4: *Generated, never random.* The world graph is the single source of truth that terrain, dungeons, relics, bosses, NPCs and maps all refer to, so secrets connect and can be followed.

## What it is
A graph of **entities** and **relations**, generated lazily and deterministically from the world seed as the player approaches. Anything an LLM writes about an entity (names, lore) is persisted the first time it's generated (see 06).

### Entities
| Kind | Examples | Key fields |
|---|---|---|
| Region | "The Greyspine Highlands" | id, center, theme, terrainCharacter, tier, dominant faction |
| Place | ruin, shrine, pass, dungeon, sanctuary, landmark, village | id, region, position, kind, state (intact/ruined/occupied) |
| Faction | "Drowned Priests of Vel'Maren" | id, home region, ethos, symbols, status (thriving/fallen/hidden) |
| Figure | historical or living person | id, faction, role (founder, smith, traitor…), alive?, archetype |
| Event | "The Drowning of Vel'Maren" | id, era, places, factions, figures, outcome |
| Artifact | a relic, a lost tome, a map | id, maker (figure), origin (place), events, current location |

### Relations (typed edges)
`locatedIn · borders · controls · memberOf · founded · forged · betrayed · destroyed · buriedAt · seeks · rivalOf · mentions`

Example chain, which is what makes "Loot that leads somewhere" possible:
```
Relic "Tidecaller's Lament" ─forgedBy→ Figure "High Cantor Iselde" ─memberOf→ Faction "Drowned Priests"
   └─mentions→ Event "The Drowning" ─at→ Place "Vel'Maren Sunken Nave" (dungeon, tier 3)
                                      └─ Faction "Drowned Priests" ─seeks→ Artifact "Tidecaller's Lament"
```
Finding the relic reveals a place you can visit, and a faction that will want the relic back.

## Generation (lazy, layered, deterministic)
1. **Eras:** the world seed generates 3–4 eras of history (an age of founding, a cataclysm, a fragmentation, the present). These are a skeleton only, about 20 events with no prose.
2. **Regions** (from the region field, 09) get a dominant faction and 2–4 historical events drawn from the eras.
3. **Places** are placed by the world generator: passes, landmarks, ruins, dungeons, sanctuaries. Each one is linked to events and factions of its region.
4. **Figures** are created as needed (a dungeon needs a builder, a relic needs a maker).
5. **Prose** (names, lore, inscriptions) is written by the LLM on demand, with the local subgraph as context, then persisted.

The structure (who did what, where, when) is **deterministic and rule-based**. The LLM only **narrates** it. It never invents structure, so lore can't contradict the graph.

## Queries other systems use
- `ContextFor(placeOrItem, radius)`: a compact JSON subgraph to feed the LLM (relic lore, NPC dialogue, inscriptions)
- `LeadsFrom(entity)`: unresolved places mentioned by an entity. This feeds the Reliquary's "unsolved leads" and treasure maps.
- `NearestPlaceOfKind(pos, kind)`: for rumor boards and cartographers
- `Discovered(entity)` / `MarkDiscovered`: per character (journal map) and per archive (Reliquary)

## Naming
- Each faction or culture gets a **phonology** (syllable sets, seeded), so names within a culture sound related and cultures sound different. Names are generated deterministically; the LLM may only *use* them, not invent new proper nouns.
- Banned stock words list shared with the items spec (no "ethereal", "shadow-whisper"…).

## Ownership and code
- `Relic.Core` gets the **entity and relation data types** and query interfaces (lead).
- The generator lives in a future **Lore stream** (or the lead) at `Assets/Relic/Lore/`.
- Until then, the Items stream uses a **stub world context** with the same JSON shape as `ContextFor`.

## Open questions (later, not blocking)
- How much history? Too much becomes noise. Start with about 20 events per 10×10 km and tune.
- Multiplayer: the graph is server-authoritative, and generated prose is broadcast.
