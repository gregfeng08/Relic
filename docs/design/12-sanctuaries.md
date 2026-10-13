# 12 — Sanctuaries

Status: **DRAFT v1 (lead)** · Last updated 2026-10-13
Wynncraft-style safe hubs: live, trade, store. Grounded: every service is a person or a building, and every rule has an in-world reason.

## Placement
- **One major sanctuary per region** (its capital), plus **0–2 minor ones** (outposts, monasteries, caravanserais) along routes and passes.
- **The origin sanctuary** sits at (0, 0), tier 0.
- **Placement rules:** defensible or valuable spots (a river mouth, a pass, a plateau), reachable by at least one route without climbing, and never inside a dungeon's footprint.
- **Why monsters stay out (lore):** warded walls, a patron spirit, or simply garrisoned soldiers. The world graph records which.

## Services
| Service | Person/place | Rules |
|---|---|---|
| Vault | Vaultkeeper's strongroom | One vault per sanctuary. Items stay where you left them. |
| Courier / caravan | Caravan master | Moves items between sanctuaries for a fee and over real time. Caravans travel real routes (later: they can be ambushed). |
| Merchants | Stalls by faction | Prices depend on CHR, faction standing and tier. Far sanctuaries sell better goods. |
| Forge | Smith | Rerolls (fresh roll, costly materials), repairs, and later re-enchanting. |
| Soulbinder | Shrine or sage | Binds essences into the soul (permanent stats, capped per stat). |
| Cartographer | Map house | Copies and merges journal maps, sells local charts (which can be outdated or wrong). |
| Writing desk | Scriptorium | Spellcraft: design spells, buy ink and blank pages. |
| Reliquary | Library or shrine | The archive of found relics and lore across characters, plus unsolved leads. |
| Rumor board / tavern | NPCs | Leads to dungeons, relics, other sanctuaries. |
| Bed | Inn | Rest, set the time of day. **Not a respawn point** (permadeath). |

Minor sanctuaries offer a subset (always a vault and a bed, sometimes a smith).

## Layout generation
- A **town grammar**: a walled core with the services, districts by faction culture, roads aligned to the terrain and to exits toward the routes.
- Built from modular, themed kits (the region's culture sets the architecture).
- **Rule:** every service is reachable within about 60 seconds of walking from the gate. Players visit often, so convenience matters.

## Multiplayer (later)
Sanctuaries are where players meet and trade. Vaults are private. Graves are never inside sanctuaries.

## Ownership
A future **Sanctuaries stream** (`Assets/Relic/Sanctuaries/`), after the world graph and the M2 regions.
