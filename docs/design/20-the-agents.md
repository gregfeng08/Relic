# 20 — The Agents: a living world, kept running by many hands

Status: **AGREED direction (Greg, 2026-11-18)** · Last updated 2026-11-18

## Greg's direction
- "The lore should be managed by a **constantly updating separate agent** rather than all pre-written, based on all the characters' data and movements."
- "I want the players to get the idea that there are **multiple agents in motion behind the scenes**, working hard individually to keep the game working. That's why the Narrator as the final boss would be great."

## The crew
The game's generative systems become **characters with jobs**. Each one is a real backend process (an LLM-backed service plus deterministic code) with its own name, voice and log. The Narrator (19) coordinates them, the way a lead coordinates a team.

| Agent | Real job in the code | Character |
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

## The final fight, reframed
The Narrator calls in the crew. Each phase belongs to one agent:
- **The Cartographer** redraws the arena.
- **The Smith** forges weapons from *your* relics.
- **The Warden** sends the guardians you defeated.
- **The Magister** reads your own spells back at you.
- **The Narrator** comes last.

## Engineering notes
- **One interface per agent:** inputs (events), outputs (validated data), cadence, budget, fallback. All are LLM-optional (18).
- **Agent personas live in a voice guide,** separate from the Lore Bible. The persona never changes the agent's rules or validators.
- **Multiplayer:** each agent is a server-side worker with its own queue and rate limits. Clients only receive results.
