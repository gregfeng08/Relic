# Session log

Newest first.

## 2026-10-12 (later) — Core loop decisions

- Greg decided: permadeath with dropped items + a vault, skill over stats (StarBreak), one hero, gear-defined builds, Wynncraft-style sanctuaries, no level gating.
- Added pillar 6 "Skill is the only gate" to 01-vision. Rewrote 02-core-loop with the decisions, a sanctuaries section and consequences for the other specs.
- Board: RLC-001 done, RLC-004 in review (follow-ups about dropped items and what survives death), new card RLC-019 for the sanctuary spec.
- **Next:** combat & characters spec (RLC-005), then the item spec (RLC-006).

## 2026-10-12 — Ideation kickoff

- Ideated core concepts: LLM-composed relics (effect grammar + power budget + kitbashed visuals), layered-noise world with region blending, graph-first dungeons, world graph as shared source of truth, D&D-style systems under action combat, NPC LLM dialogue (later).
- Decisions: single-player first (online later), action combat, Genshin-style art (low-poly OK for prototyping), local LLM first, Unity 6 (6000.3.9f1) + URP, infinite map long-term starting with one region.
- Cloned github.com/gregfeng08/Relic. Audited the legacy 2022.3 prototype: ~350 lines (radial room placement, stub classes/inventory). The `Library/` folder is committed (~14k files) and needs removing. Tagged it `legacy-2022` and started branch `rebuild`.
- Drafted `docs/design/01-vision.md` and `02-core-loop.md`. Published the quest board.
- **Next:** Greg reviews the vision, answers the core-loop questions, and approves pushing. Then combat/characters spec.
