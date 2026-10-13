# 11 — Dungeons

Status: **DRAFT v1 (lead)** · Last updated 2026-10-13
Builds on the 2022 prototype's idea (rooms placed without overlap), but generates the **mission graph first** and the layout second.

## Pipeline
1. **Context:** the world graph says what this place *is* ("the Sunken Nave of the Drowned Priests, flooded in the Drowning, tier 3, a boss of archetype Tidecaller").
2. **Mission graph** (rule-based graph grammar, seeded):
   ```
   Entrance → Combat → Fork ─┬→ Puzzle → Key ──┐
                             └→ Combat → Loot ─┤
                                     Lock ←─────┘ → Rest → Boss → Reward → Exit
   ```
   - Rewrite rules add cycles (shortcuts back to the entrance), optional branches (secrets, pillar 1), and lock-and-key pairs (a key is always reachable before its lock).
   - **Length scales with tier:** 6–10 nodes at tier 0, 15–25 at tier 8+.
   - A **rest node** always comes before the boss (a sanctuary-like safe room with no vault). Permadeath needs a breather.
3. **Layout:** place **handmade room prefabs** (tagged with doorway sockets and theme) onto the graph:
   - Pick prefabs that match each node's role and theme.
   - Connect them with corridors via socket matching. Reject overlaps using bounds checks (from the 2022 prototype) and backtrack.
   - Organic sections (caves between rooms) are carved with cellular automata.
4. **Population:**
   - Enemies are chosen by theme and tier; elites get grammar modifiers.
   - Loot: one loot node per branch, and the relic chance is highest at the boss.
   - **Inscriptions and lore objects** come from the world graph (INS-gated readings reveal secret doors).
5. **Validation:**
   - The entrance can reach every required node.
   - Every key comes before its lock.
   - The boss arena meets 03-combat's rules: room to dodge, cover, no unavoidable damage.
   - Generation is deterministic from `Hash.Combine(worldSeed, placeId)`.

## Grounded touches (pillar 7)
- Doors are physical: barred, collapsed, flooded. Keys are real objects: a crank, a valve that drains a room, a lever.
- A flooded dungeon has water mechanics. Soaked is everywhere and fire doesn't work, which teaches the status rules.
- Dungeons persist: cleared rooms stay cleared until the world's rules say otherwise (monsters can return to dungeons that "breed" them, explained by lore).

## Group play
Arenas are sized for groups. Boss HP scales with players present (03-combat). Puzzles can't need exactly N players.

## Ownership
A future **Dungeons stream** at `Assets/Relic/Dungeons/`, depending on Core and the world graph types. The mission-graph generator and validator are pure Logic and fully testable. Room prefabs are art-light placeholders first.
