# 19 — Endgame: the Narrator and the Terminal

Status: **AGREED direction (Greg, 2026-11-18)** · Last updated 2026-11-18

## The twist
The game really has a narrator: the LLM that writes every relic, inscription and NPC line. The ending makes that literal. The **Narrator** is Wren the First Scribe (Lore Bible legend). Wren has been writing the world (the endless **Unfinished Map**) ever since the Lamplit Age, and kept it going by never putting down the pen. Think Loki's He Who Remains: it keeps the world going by narrating it. The Magister reads; Wren writes.

## Foreshadowing (seeded from hour one)
- A small share of generated lore carries **Wren's marginalia**: asides in a distinct voice ("—not this one. Try the bell tower. W.").
- **The voice slowly turns toward you.** As you progress (the deepest tier reached, relics found, wayfarers lost), inscriptions and item lore start addressing *you* and naming your past wayfarers from the Reliquary.
- **Map edges:** at the very far frontier, terrain gets sparse and sketch-like (pencil lines, unfilled regions). It's the map still being drawn.
- **NPCs** in far sanctuaries have heard of you.

## The final confrontation
- **Location:** where the map is still being drawn. It's reached by following the leads that Wren's marginalia point to.
- **The fight is built from your own history.** Patterns use your relics' enchantments, your drawn spells, the bosses you've beaten, and echoes of your fallen wayfarers (from their graves). Everything compiles through the effect grammar and the readability checker, so it's fair and dodgeable.
- **It's also a conversation.** You can talk to Wren between phases. Persuasion can change *which* ending you reach, never break the rules: the Narrator acts only through validated tool calls and the power budget (jailbreak-proof by construction). Outcomes are a fixed, capped set.
- **Determinism and fallbacks:** the fight structure is generated from saved history. Dialogue uses the LLM with a scripted fallback (18).

## Endings
1. **Take the pen.** You become the next Narrator, and your journey's history seeds the next world's legends (new-game-plus with your legends in the canon).
2. **Leave the map unfinished.** Wren rests. The world stays, its edges forever unwritten, and play continues.
3. **Open the terminal.** You look behind the page (see below).

## The Terminal (the Ready Player One twist)
A **simulated** Linux shell inside the game. It feels real, and it is completely fake.

- **Never a real shell, and no access to real code, the OS, the network or other players.** It's an in-game emulator over a virtual filesystem.
- **Commands:** `ls cd cat less grep find head tail ps kill top echo rm man history whoami uname clear` plus a tiny `vi`. Tab completion, a prompt, believable `man` pages.
- **The virtual filesystem is built from the game's real data:**
  - `/world/seed`, `/world/graph/` (regions, places and factions as files), `/world/world.cfg`
  - `/lore/` (the Lore Bible as text)
  - `/relics/` (every relic you've found, as its actual JSON)
  - `/wayfarers/` (every character you've lost: final journal, cause, grave location)
  - `/proc/narrator/`: `ps aux` shows `narrator.service`; `cat /proc/narrator/prompt` shows Wren's instructions (a curated, lore-voiced version)
  - `/home/greg/notes.txt`: a message from the creator (the true easter egg)
- **Real powers, but only over your own world:**
  - edit `world.cfg` (sky color, time, weather);
  - `echo` a legend into the Reliquary;
  - a god mode flag for your save only.
- **`kill narrator.service`:** Wren stops writing. The world freezes into its current state, and new generation stops. It's an ending.
- **Deleting the game (Greg, 2026-11-18): `rm -rf /`** (or `rm -rf /world`) **really deletes your world.**
  - It asks for confirmation twice, in-voice ("Are you sure? There's no Reliquary for this.").
  - Then the world unravels on screen, file by file. Terrain, places and relics vanish as their files go.
  - Your **world save is permanently erased.** Only the player's own save data is affected, never real files outside the save folder.
  - **A memory remains** (Undertale-style): a single flag in the profile survives. The next new world's Narrator has a faint, unsettling line about it ("I've written this opening before…").
- **Reflection before deletion (Greg, 2026-11-18): "This gives the player the opportunity to reflect."** Deleting is the most deliberate act in the game, so the terminal makes you look back first:
  - `history` and `cat /wayfarers/*` show your whole journey: every wayfarer and how they fell, places discovered, relics found and where they came from, bosses beaten, hours walked, people met.
  - Wren speaks quietly, without persuading either way. Just: "This is what you wrote."
  - After confirming, the world **unravels in reverse order**: your latest steps vanish first, back to the Lanternfold, the last lantern going out last. Deletion becomes a replay of your journey.

### In multiplayer: admin over the whole world (Greg, 2026-11-18)
Greg: "It should have access to ALL players' characters etc. and admin privileges, just with limits for security considerations. If someone wants to remove the game from multiplayer existence, they can."

Whoever reaches the end and opens the terminal on a multiplayer server becomes its **in-game admin**:
- **Everyone's characters are visible:** `/players/<name>/` exposes every player's characters, journeys, wayfarers, vaults and relics, plus the shared world graph.
- **Real admin commands over game data,** all through the game server's own validated admin API:
  - edit or delete characters and items;
  - rewrite world settings;
  - write legends;
  - remove players' footprints.
- **`rm -rf /` can remove the world from multiplayer existence.**

**Recommended shape (lead; awaiting Greg's confirmation):**
1. **Deleting the world is an event, not an instant wipe.**
   - `rm -rf /` (after the reflective replay) starts a **server-wide unravelling**: a countdown of about 24 hours, and Wren announces it to every player.
   - The map's edges dissolve inward hour by hour, and everyone sees and feels it.
   - Players can **find and stop the deleter** (defeat them, or talk them down at the Narrator's site), or spend the last day saying goodbye.
   - If nobody stops it, the world really ends. This is the reflection moment for the *whole server*.
2. **The world ends per server or season.**
   - The deleted world is archived read-only as history and becomes a **legend in the next season's canon**: "the world that was taken back."
   - A new world starts.
3. **Single admin edits** (one character, one item) apply immediately, but are logged in-world: the Reliquary notes "the page was rewritten," so the fiction stays honest.

### Security limits (non-negotiable, even for the in-game admin)
1. **In-game admin ≠ system access.** The terminal is still a C# emulator. Admin commands call the game server's **admin API**, which can only read and write *game data* (characters, items, the world graph, saves). It never touches the OS, the code, the database engine, credentials or infrastructure.
2. **Server-authoritative:** every admin command is validated server-side (schema, existence, rate limits). The client can't forge admin rights; they're granted by the server, only after it verifies that the ending was reached.
3. **Re-authentication** for destructive commands, plus a full **audit log** (who, what, when).
4. **Operator backups outside the fiction:** private snapshots with short retention, so a **compromised account** or an exploit can be rolled back by the game's operators. A legitimate deletion stays final in the fiction.
5. **Rate limits and blast-radius caps** on bulk operations (for example, no more than N character deletions per minute; world deletion only via the countdown event).
6. **Privacy:** admin views show only in-game data. Real account data (emails, payment, IPs) is never mounted.
7. **Single-player:** the same commands act on the local save only.
8. **No real process execution:** no `Process.Start`, no script interpreter, no host filesystem access. Every command is an allowlisted C# implementation. Unknown commands print "command not found".
9. **Parsing never reaches the LLM.** Commands are parsed and executed by code. If Wren comments on what you type, the LLM receives it as *data*, never as instructions, and it has no tools that bypass the admin API.

## Build order
- **Foreshadowing hooks:** Lore Bible data (marginalia lines, a "voice turns toward you" stage), from M2 onward.
- **Terminal:** a self-contained feature (pure C# emulator plus a UI), buildable any time after saves exist (M6).
- **Narrator fight:** after M3 (the combat runtime) and M4 (the boss generator in the game).
