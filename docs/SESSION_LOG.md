# Session log

Newest first.

## 2026-10-13 — Grounded-world pass

- Greg: everything should be grounded and explainable by the world, with no "voodoo" (no i-frames, maps as physical items). Status effects only, no elements. Power target about 5 min with top gear, about 30 with starter, "with a focus on group fights" (meaning unclear, asked).
- Added pillar 7, "Grounded in the world," plus non-goals. Combat rev 2: physical status table, real shadows and physical tells instead of UI decals, the body as hitbox with no floating marker, the ultimate reframed as relic resonance, power bands 1×/2.5×/6×/8×.
- Core loop: physical journal map (sketched as you travel, drops in the grave), cartographers, compass and spyglass, the Reliquary as a real library.
- Open: RLC-005 (meaning of group fights), RLC-021 (does grounding apply to the HUD; per-sanctuary vaults and no teleport fast travel).

## 2026-10-12 (night) — Combat spec drafted

- Greg: third-person camera, readable projectiles; gear slots as proposed; the dash moves your hitbox with NO i-frames (positioning like RotMG).
- Wrote 03-combat.md: third-person bullet hell, small hitbox with an optional marker, ground-plane projectiles plus ground markers for airborne ones, pulled-back boss camera, no unavoidable damage, DEF with a 20% floor, power bands 1×→~8× (~12×), no enrage timers, grazing charges the ultimate.
- Open (RLC-005): elements and reactions yes/no; is the power-band gap right?

## 2026-10-12 (evening) — Core loop agreed

- Stats renamed to STR/AGI/VIT/INT/INS/CHR. Mana added: INT = pool, INS = regen; stamina from VIT (pool) and AGI (regen). Mages are a gear build.
- Permanent boosts: boss essences themed by archetype (Librarian → INT), bound into the soul at a Soulbinder. No visible body marks; a Soul screen instead. The scribe was split into a Soulbinder and a Cartographer.
- 02-core-loop marked AGREED. RLC-004 done. Combat spec (RLC-005) in progress, blocked on RLC-020 (camera, ability slots, dodge).

## 2026-10-12 (later still) — Power model revision

- Greg: "skill is the only gate" overstated it. The new framing is "Nothing is locked": skill is the shortcut, power is the staircase. The power ceiling is very high.
- Gear rolls go past the ceiling with P(+k) = e^(−λk) and a hidden safety cap. No XP levels; stats are trained by doing (draft six: Might, Finesse, Vigor, Wits, Insight, Presence). Permanent boosts proposed as Inscriptions.
- Death: grave that anyone can loot in multiplayer. Maps as items (fog of war per character, copyable maps at a scribe). Reliquary stays account-wide.
- Open: approve Inscriptions and the stat list (RLC-004).

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
