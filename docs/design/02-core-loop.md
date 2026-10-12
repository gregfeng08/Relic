# 02 — Core Loop

Status: **DRAFT — decisions in, two follow-ups open** · Last updated 2026-10-12

## Decisions (Greg, 2026-10-12)

| Topic | Decision |
|---|---|
| Death | **Permadeath.** The character's items drop where they died. A **vault** stores items safely. |
| Progression | **Skill over stats** (StarBreak: a stock character can clear Star Cathedral). No level gating anywhere. |
| Hero | **One hero.** No party swapping. |
| Builds | **Defined by gear.** No classes. You make the character your own through what you equip. |
| Hubs | **Sanctuaries** (Wynncraft cities): safe places to live, trade and store items. |

## Three loops

### Moment to moment (seconds)
**Traverse → spot → engage.**
Run, climb and glide across terrain (Genshin). Something catches the eye: a silhouette, a glint, a ruin, a camp. Action combat with dodges, gear-granted abilities and readable enemy tells. Loot pops with rarity colors (RotMG bag colors). With permadeath, every fight carries real tension.

### Session (30–90 minutes)
**Sanctuary → follow a lead → explore → dungeon → boss → loot → back to a sanctuary (or push on).**

1. **Prepare at a sanctuary.** Pull gear from the vault, trade, read the rumor board, pick a lead.
2. **Explore.** Cross a region, find points of interest, fight, grab common loot.
3. **Dungeon.** Graph-generated, themed by the region's lore, ending in a boss.
4. **Payoff.** Boss loot, maybe a relic. The relic's lore names new places, which become new leads.
5. **Get it home.** Reach a sanctuary and put valuables in the vault, or gamble on going deeper with them. This is the RotMG tension: everything you carry is at risk until it's vaulted.

### Long term (weeks)
- **Push outward.** Danger rises with distance from the origin. Far sanctuaries are destinations in themselves. A bold player can make that run at any time.
- **Complete the Reliquary.** Collect relics, resolve their leads.
- **Craft builds.** Gear defines who you are. Find combinations, chase enchantments, awaken relics through item memory.
- **(Later)** Shared world: first discoveries, world-unique relics, trading between players, D&D-style campaigns.

## Sanctuaries

Safe zones placed procedurally through the world, each themed to its region and faction.

| Service | Purpose |
|---|---|
| Vault | Permanent storage; survives permadeath (likely shared by every character in a save) |
| Merchants / trade | NPC trading now, player trading later |
| Forge | Tinker with gear: reroll or re-enchant (exact rules in the item spec) |
| Rumor board / NPCs | Leads to dungeons, relics, other sanctuaries |
| Reliquary shrine | View the collection and its unsolved leads |

Far-off sanctuaries are where the best merchants, forges and leads are. Getting to one early is a high-risk, high-reward run.

## Consequences for other specs

- **Combat spec:** the hero has no base stats to grow. Power comes from gear and player skill. Gear's power range has to stay narrow, roughly **1×–2.5× starter power** at the top end (tune later), so a stock character can still win.
- **Item spec:** progression is mostly *horizontal*: new effects, synergies and playstyles rather than bigger numbers. Relic enchantments change *how* you play.
- **World spec:** difficulty bands by distance; sanctuary placement and generation; tougher enemy behavior farther out instead of higher stats.
- **Death:** a grave (a loot bag) at the death location. See the open questions.

## Open follow-ups (for Greg)

1. **What happens to dropped items?** Options:
   a. Gone forever (RotMG)
   b. A grave in the world that your *next* character can travel to and recover (a corpse run with permadeath)
   c. Option b now, and in multiplayer anyone can loot it
2. **What survives death besides the vault?**
   - Proposal: Reliquary knowledge (lore and leads found), the discovered map, and sanctuary waypoints carry over to the next character, because exploration is the point.
   - Alternative: the map resets and only the vault survives.
