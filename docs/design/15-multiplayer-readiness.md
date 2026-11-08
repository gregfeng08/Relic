# 15 — Multiplayer readiness

Status: **v1 (lead)** · Last updated 2026-10-28
Multiplayer ships in **M8**. We don't build networking now, but every system follows these rules, so adding it later is a transport job and not a rewrite.

## Target model
- **Server-authoritative.** A dedicated headless server owns the truth: world graph, items, vaults, graves, combat resolution. Clients send *intent* and render the results.
- **Not lockstep.** Float math isn't bit-identical across machines, so we don't rely on clients simulating the same thing. Seeds make generation *reproducible*; the server is still the authority.
- **Client prediction for your own movement only.** Your hero moves instantly; the server reconciles. Everything else (enemies, projectiles, other players) is server state shown with interpolation.
- **LLM generation runs server-side** (or in the cloud). Clients never generate relics or lore in multiplayer; they receive the saved result (06).

## Rules for every stream, starting now

| # | Rule | Status |
|---|---|---|
| 1 | **Simulation is separate from presentation.** Rules live in pure C# (`Logic/`, `Relic.Combat`), and Unity code only displays and collects input. | ✅ Core, Combat, Items and Magic logic are pure |
| 2 | **Intent is a command.** Player actions become command objects (`Move`, `Dash`, `UseAbility(program, rule, target)`, `Equip(itemId)`, `Interact(entityId)`) that the simulation applies. Nothing in the UI mutates game state directly. | ⏳ Apply in M3 (combat runtime) |
| 3 | **Combat runs at a fixed tick** (for example 30 Hz), separate from the render frame. Visuals interpolate. | ⏳ M3. `CombatSim.Tick(dt)` is already tick-based |
| 4 | **Stable IDs for everything that persists or is shared:** entities, items (already seed-derived), places and graves. Never use Unity instance IDs or object references in saved or shared state. | ✅ items · ⏳ entities in M3 |
| 5 | **No hidden global game state.** No static singletons holding state. Systems get their dependencies passed in, so a server can host many players and regions in one process. | ✅ so far · keep it |
| 6 | **Generation is a pure function of seed and coordinates;** persisted deltas sit on top (06). | ✅ |
| 7 | **Persistence behind an interface** (`IItemStore` exists). Saves become a database later. | ✅ items · ⏳ world and characters |
| 8 | **Chunks and regions are the unit of interest:** a client only receives what's near it. | ✅ chunking exists |

## Choosing a network stack (decide at M8, not now)
Candidates: Unity Netcode for GameObjects, Netcode for Entities, FishNet, Mirror, or custom.

The deciding needs:
1. dedicated headless server builds;
2. client-side prediction and reconciliation for movement;
3. RotMG-scale crowds: about 50–100 players at a boss, with many projectiles;
4. good interest management.

**Projectiles are the scaling problem.** We'll replicate *pattern spawns* (an EffectProgram plus a seed and start time) rather than individual bullets. Clients then simulate the readable patterns locally, and the server resolves hits.

## Where it matters soonest
**M3, the combat runtime.** It gets built command-driven, at a fixed tick, with server-style hit resolution, even in single-player. That's the one place where cutting corners now would cost a rewrite later.
