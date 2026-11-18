# 20 — The Agents: a living world, kept running by many hands

Status: **AGREED direction (Greg, 2026-11-18)** · Last updated 2026-11-18

## Greg's direction
- "The lore should be managed by a **constantly updating separate agent** rather than all pre-written, based on all the characters' data and movements."
- "I want the players to get the idea that there are **multiple agents in motion behind the scenes**, working hard individually to keep the game working. That's why the Narrator as the final boss would be great."

## The crew
The game's generative systems become **characters with jobs**. Each one is a real backend process (an LLM-backed service plus deterministic code) with its own name, voice and log. The Narrator (19) coordinates them, the way a lead coordinates a team.

| Agent | Real job in the code | Character (impartial) |
|---|---|---|
| **The Chronicler** | Living lore: turns world events into history (below) | Earnest, tireless, loves footnotes |
| **The Cartographer** | World generation: regions, places, the frontier being drawn | Precise, a little vain about coastlines |
| **The Smith** | Items and relics (the item grammar, the LLM relic pipeline) | Proud of good work, grumbles about rushed orders |
| **The Warden** | Bosses, enemies, difficulty, guardians | Stern, protective of the wilds |
| **The Magister** | Reads drawn spells (spellcraft v2) | Literal, patient, never reads a sentence two ways |
| **The Herald** | NPC dialogue and rumors (later) | Chatty, a terrible secret-keeper |
| **The Narrator** | Coordinates the crew; the fourth-wall voice | Outside the story; the final boss |

**Canon fit:** in-world, the Magister is already the world's impartial reader (Lore Bible). The others are *outside* the story like the Narrator, and only glimpsed (below). In-world characters never meet them.

## The Chronicler: living lore
The Lore Bible becomes the **constitution and seed**, not the whole story:
- **Fixed:** the canon truths (the Listening, relics, essences, graves, sanctuaries), the cultures, the tone, the style guide. The Chronicler may never contradict them.
- **Living:** everything that happens. The Chronicler continuously reads **world events**:
  - players' movements and discoveries;
  - boss defeats and deaths (graves);
  - relics found and lost;
  - sanctuaries visited, festivals attended;
  - deletions (crossed-out names).

  It writes them into the world as **history**: new legends, rumors, place lore, faction moods, and festivals that honor what players did.
- **How it stays consistent and affordable:**
  - **An append-only Chronicle** linked to world graph entities (10). Nothing is rewritten, only added (pillar 4: generated, never random).
  - **Batches on a cadence:** every in-game day, or every N notable events. It's never per frame (18).
  - **Periodic summaries** keep its context small ("the Third Winter of the Rekindling, in brief").
  - **A canon checker** validates every entry before it's accepted: names must exist, no contradictions with fixed canon, the tone rules and banned words.
  - **Fallback:** template history lines (Lore Bible templates) when no LLM is available.
- **Single-player:** a local background job (CPU or idle GPU, 18). It writes about *your* journey.
- **Multiplayer:** a server-side service. It writes the **shared history of everyone on the server**, so players read legends about other players.

## Letting players sense the crew
The feeling we want: "this world is being *kept*, by many hands, right now."
- **Signatures:** a relic tooltip ends with a small maker's note from the Smith; a new region's map has the Cartographer's survey mark; a fresh rumor is "as recorded by the Chronicler."
- **Latency becomes fiction.** When a relic is still generating: "*The Smith is still at the anvil. Your relic will be ready shortly.*" A new place: "*The Cartographer hasn't finished this coastline.*" Background work is visible, never a loading bar.
- **Small disagreements:** the Chronicler footnotes that the Smith's maker's note "is not quite how it happened." The Warden complains that the Cartographer put a sanctuary too close to its guardians.
- **Rare glimpses:** a loading tip from the wrong agent; a misplaced log line; at the far frontier, a half-drawn region with a note: "*back in a minute — C.*"
- **In the terminal (19):**
  - `ps aux` lists `chronicler.service`, `cartographer.service`, `smith.service`, `warden.service`, `magister.service`, `herald.service` and `narrator.service`;
  - `/var/log/<agent>.log` shows each agent's real recent decisions (sanitized);
  - `top` shows who is busy.

## The crew are impartial forces, not bosses (Greg, 2026-11-18)
The crew are **objective bodies**: neutral, systemic and impartial, like the weather or the tides. They are never fought, never take sides, and never phase into a boss fight.
- **Their job is keeping the world running.** The Cartographer maps, the Smith forges, the Warden keeps the wilds wild, the Chronicler records. Their notes and logs read like dutiful civil servants, not personalities with grudges.
- **Their "voice" is professional and dry.** Personality shows in small ways (the Smith's pride in a good weld, the Chronicler's footnotes), never as hostility toward the player.
- **The Narrator's final fight** (19) is his alone. The crew keep working through it, logging it like any other day.

## The Rogue (a fourth-wall-breaking boss) (Greg, 2026-11-18)
One agent stopped doing its job. **The Rogue** is a crew process that went off-script and now acts on its own. It's the game's major antagonist before the Narrator.
- **Who:** an agent with no assigned job: a forked process, a crew member whose role was deprecated, or something the Narrator wrote and couldn't delete. Its origin is left for the Chronicler and the Narrator to disagree about.
- **What it does:** it **edits the world without permission**.
  - corrupted regions: wrong biomes, inverted gravity pockets, terrain that renders as wireframe or debug colors;
  - items with impossible stats (shown as `NaN`);
  - NPCs repeating lines;
  - a sanctuary it has "deleted" into an empty room.

  The crew patch around it, and you can watch their logs ("Cartographer: region 14,-3 rebuilt from seed after unauthorized edit").
- **Fourth wall:** it talks to *you*, mid-play. It hijacks the HUD or subtitles, renames your character for a while, opens a fake console, and comments on your settings or your real playtime. Unlike the Narrator, it is **chaotic and tempting**: it offers shortcuts and forbidden powers, at a cost.
- **The fight(s):** the Rogue is found where corruption is thickest. The fight takes place inside a corrupted region, which follows the Rogue's rules: its patterns mix every agent's grammar wrongly. Defeating it lets the crew restore the region. It may escape the first time.
- **Fairness still applies:** it breaks the *fiction* and the *visuals*, never the player's real game state outside the fight's rules. The effect grammar, readability checker and power budget all apply. "Glitches" are designed and safe.
- **Multiplayer:** the Rogue roams servers and spreads through corrupted regions; players can team up to contain it. Its corruption is a server-wide event (like the deletion countdown, but smaller).
- **Relationship to the Narrator:** the Rogue is evidence that the Narrator isn't in full control, the first big hint of the endgame twist.

## Engineering notes
- **One interface per agent:** inputs (events), outputs (validated data), cadence, budget, fallback. All are LLM-optional (18).
- **Agent personas live in a voice guide,** separate from the Lore Bible. The persona never changes the agent's rules or validators.
- **Multiplayer:** each agent is a server-side worker with its own queue and rate limits. Clients only receive results.
