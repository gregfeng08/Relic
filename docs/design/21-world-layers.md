# 21 — World layers: an infinite world that agents can change

Status: **PROPOSAL (lead) — needs Greg's review** · Last updated 2026-11-18
Greg: "I wonder if chunk-wise generation is the best? That way we can manage adaptability." The Chronicler writes living history, the Cartographer redraws, the Rogue corrupts regions, and players leave graves and change things. So the world must be **adaptable**, not just generated.

## Answer: chunks for *building*, layers for *meaning*
Chunks (64 m tiles) are the right unit for **building and streaming geometry**. An infinite world can't exist any other way, and they give us threads, LODs and floating origin. But chunks are the wrong unit for **deciding what the world is**. Adaptability lives in layers above and on top of them:

```
 L3  World graph        (regions, places, factions, history)   ← Chronicler, Cartographer, Rogue
       │ coarse: one region ≈ 1–2 km, macro-tiles ≈ 4 km
 L2  Region plans       (biome mix, landmarks, roads, rivers,  ← Cartographer
       │                 POI sites, borders/passes, corruption)
 L1  Base terrain        pure function of seed + x,z            (never edited)
       │
 L0  Deltas (overlay)   sparse, persistent edits keyed by       ← agents + players
       │                 region/chunk: carved paths, burned
       │                 forest, graves, built bridges, corrupted
       ▼                 patches, cleared camps
     Chunk build = L1 sampled → L2 plan applied → L0 deltas applied → mesh + props
```

- **L1 base terrain** stays a pure, deterministic function (golden hash test). It's never edited, which keeps multiplayer cheap: clients can build terrain locally.
- **L2 region plans** are generated per region (or per 4 km macro-tile) from the seed *and* the world graph. They decide what a region *contains*: biome blend, landmark positions, rivers and lakes, roads between sanctuaries, border passes (09), and which areas are corrupted. A plan is small data that agents can **regenerate or amend**.
- **L0 deltas** are sparse edits (a list of operations: flatten, carve, raise, paint biome, place or remove prop, place structure, corruption mask), each with a version. They're stored per region and persisted (and server-authoritative in multiplayer).
- **Rebuilding:** when a plan or delta for an area changes, only the affected chunks are **invalidated and rebuilt** in the background, the same path as streaming. It's never visible as a hitch.

## Why this works for adaptability
- **The Chronicler** writes history into L3 (the world graph). A history event can request L2 changes ("the festival grounds at Mogwade grew").
- **The Cartographer** owns L2. It redraws a region plan when the world graph changes, and repairs Rogue damage by rebuilding from the seed and graph.
- **The Rogue** writes **corruption deltas** into L0 and L2 (a corruption mask and its "wrong" plan). Cleansing simply removes them. The base layer is untouched, so restoring is always possible.
- **Players** create L0 deltas through gameplay: graves, cleared camps, opened shortcuts (multiplayer: shared).
- **Determinism:** L1 is reproducible anywhere; L2 and L0 are persisted data. So any client can rebuild the exact same world.

## Cost
L2 plans are tiny (kilobytes per region). L0 deltas are sparse. Rebuilding a chunk takes milliseconds on worker threads (measured: about 2–4 ms per chunk). Agents act on regions at their own cadence (18), never per frame.

## A fuller world (Greg, 2026-11-18: "a full world: foliage, biomes, terrain")
With L2 in place, the world can fill out with features that span many chunks:
- **Terrain features:** rivers that run downhill to lakes and the sea, waterfalls, canyons, sea arches, karst pillar fields, mesas, glacier tongues, hot springs, caves (cave entrances first).
- **Biomes:** more biomes per region family, with sub-biomes (birch groves inside forests, wildflower meadows, salt flats in the badlands, tundra moss), and smooth ecotones.
- **Foliage:** density maps per biome, fallen logs, mushrooms, reeds at water's edges, vines on cliffs, flowers by season, trees that lean and grow by slope and wind.
- **Human touch:** roads and paths between sanctuaries, bridges at river crossings, ruins, camps, waystones, fences and fields near villages.

## Proposed build order (M2: "one living region")
1. L2 region plans: rivers and lakes, roads, landmark sites; L0 delta storage plus chunk invalidation.
2. Biome and foliage expansion on top of plans.
3. World graph v1 (10) wired to L2 (places sit on plan sites).
4. Borders and passes (09 layer 2) as part of L2.
5. Chronicler v0 writing to L3, and the Rogue's corruption deltas as a test of the whole stack.
