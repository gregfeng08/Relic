# Session log

Newest first.

## 2026-10-13 (late night) — M1 merged, budget calibrated, LLM downloaded

- **Agents:** all three stopped early (a 429 rate limit, then 600 s stall watchdogs during long Unity imports). They were resumed one or two at a time, with Unity run in the background. Lesson: at most 2 concurrent agents, and long Unity runs always in the background.
- **Merged into `rebuild`:** stream/magic (Writing Desk, 99.4% recognizer), stream/world (terrain sandbox, IRegionField with warped and weighted regions, golden hash matching across .NET and Mono), stream/items (relic and boss generators, template samples). The URP shadow distance went from 50 to 150 m, per stream W's request.
- **Calibration v2** (`ec31fe6`): the new PowerBandTests (real items through Relic.Combat) measured top-vs-starter at 20.8×, now about 7×. Details in 05-effect-grammar.
- **LLM:** downloaded llama.cpp b11607 (CUDA 13.4) plus Qwen3.5-4B and 9B Q4_K_M into `models/`. The 4B runs on the GPU at about 35 tok/s. The server was stopped immediately because Greg was in a League match. The benchmark (RLC-031) waits for an idle GPU.
- **Feel reviews waiting on Greg:** RLC-013 (terrain), RLC-024 (Writing Desk), RLC-014 (read the samples).
- **Open stream requests:** grammar additions (Summon; area anchor/orientation; emitter angle offset/spin; ring barrier) and player baselines (body width 0.6 m, run 6 m/s, dash 4 m, HP baseline). Also small effects rounding to silly values ("0.5 damage") in templates.

## 2026-10-13 (night) — Lead specs and combat engine while streams run

- Greg approved the LLM downloads (RLC-026). The exact files get posted once stream I recommends models. Region-shape direction agreed (09): warped, weighted cells now (sent to stream W mid-run, behind `IRegionField`); travel-effort borders with passes and terrain characters in M2.
- Specs: 10-world-graph, 11-dungeons, 12-sanctuaries, 13-roadmap (M1–M8).
- Built `Relic.Combat` (lead-owned): CombatSim interpreter, CombatRules, ICombatSpace, AbstractSpace, BalanceSim. 29/29 tests in dotnet and Unity.
- **Next:** stream reports → review → merge into `rebuild` → feel-review cards; download the LLM files; use BalanceSim to calibrate PowerBudget and boss HP.

## 2026-10-13 (evening) — Groundwork built, streams launched

- Greg approved: push, pillars, the parallel plan. Pushed `rebuild` and tag `legacy-2022`.
- Replaced the 2022 project with a Unity 6000.3.9f1 URP skeleton (Library untracked, .gitignore/.gitattributes, LFS for art). Assemblies: Relic.Core (pure), World/Magic/Items × Logic/Runtime/Tests, Relic.Game.
- Relic.Core: Hash + PCG32, StatId/ResourceId, Difficulty.TierAt, StatusId + physical clears, the effect grammar, PowerBudget, EffectValidator (+ScaleToFit), EffectJson. 16 tests pass in dotnet and Unity EditMode.
- tools/Relic.Tests (dotnet harness) and tools/add-metas.sh (stable GUIDs outside Unity).
- Specs: 05-effect-grammar, 06-tech-architecture (RTX 5060 Laptop 8 GB → 3–4B model in-game), 07-world-terrain, 08-items-bosses, plus the stream M brief in 04.
- Launched three background agents in `.wt/{world,magic,items}`. Stream I is told not to download models: RLC-026 awaits Greg.
- **Next:** review each stream's report, merge into `rebuild`, put the feel reviews on the board.

## 2026-10-13 (later) — Combat agreed, spellcraft concept

- Greg: group fights = bosses designed for groups of players; minimal honest HUD OK; per-sanctuary vaults, couriers, no teleporting.
- 03-combat marked AGREED (group boss health scaling, crowd patterns, per-player loot, contribution threshold). 02-core-loop gained a travel and storage section.
- Greg proposed Witch Hat Atelier-style spell design with semantic interpretation. Wrote the 04-spellcraft concept: glyph circles (core, modifiers, shape ring) compile into the shared effect grammar; stroke recognition plus precision sets quality; the LLM provides flavor only; spells written beforehand with crafted ink.
- Open: RLC-022 (draw vs place; pre-written spells on found gear), RLC-003 (pillars review), RLC-002 (push).

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
