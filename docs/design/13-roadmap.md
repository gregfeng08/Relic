# 13 — Roadmap

Status: **DRAFT v1 (lead)** · Last updated 2026-10-13
Milestones are **playable checkpoints** Greg can review. Dates are deliberately absent: progress depends on agent sessions, not calendar time.

| Milestone | Playable result | Streams |
|---|---|---|
| **M0 — Groundwork** ✅ | Specs, Unity 6 URP skeleton, effect grammar, test harness | Lead |
| **M1 — Three sandboxes** (in progress) | (a) Walk, climb and dash across endless toon-shaded terrain; (b) draw spells at a Writing Desk; (c) read 50 generated relics and 10 bosses | W, M, I in parallel |
| **M2 — One living region** | Geography-following borders with passes (09), the first terrain characters, the world graph v1 (10) with a few POIs and landmarks, LLM-written relics from a real local model | W, Lore, I |
| **M3 — First fight** | The combat runtime executes EffectPrograms: weapon basic and skill, a spell from your tome, a relic ultimate, enemies with readable patterns, statuses, HP, mana, stamina, and death leaving a grave | Lead + I + M |
| **M4 — First dungeon** | A graph-generated dungeon in the region, with a boss built by the boss generator, loot, essences, and relic lore pointing to a place in the world | Dungeons, I |
| **M5 — First sanctuary** | Vault, forge rerolls, Soulbinder, cartographer and journal map, writing desk, Reliquary | Sanctuaries, M |
| **M6 — The loop** | Permadeath character runs: start at the origin sanctuary, follow leads, dungeon, boss, get it home. Trained stats grow by doing. Region-to-region travel. **The "one-hour feeling" test.** | All |
| **M7 — Breadth** | More regions, terrain characters, archetypes, glyphs and dungeon themes. NPC dialogue via the LLM (grounded in the world graph) | All |
| **M8 — Together** | Multiplayer: shared world, server-authoritative world graph, group boss scaling, lootable graves, trading | Lead + new streams |

**Later and parallel:** the D&D-style campaign mode (RLC-016), a cloud LLM for scaling, a controller pass, an art pass (kitbash library).

## Review rhythm
Each milestone ends with a **feel review** card on the board, with a short checklist for Greg. Nothing in the next milestone builds on unreviewed feel decisions. Code-level checks are automated.
