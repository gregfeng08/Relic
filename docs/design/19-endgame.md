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
- **Multiplayer:** the terminal only ever operates on the player's personal copy (saves, discovered data). Shared world state is read-only, or not mounted at all.

## Security rules (non-negotiable)
1. **No real process execution:** no `Process.Start`, no interpreter, no host filesystem access outside the save directory.
2. **A read-only mapping onto real data.** The only writable targets are the player's own save files, through the game's save API.
3. **A command allowlist** implemented in C#. Unknown commands print "command not found".
4. **Parsing safety:** command parsing never reaches the LLM. If Wren comments on what you type, it receives *data*, not instructions.
5. **Server side:** the terminal runs on the client against the player's data. The server never exposes anything to it.

## Build order
- **Foreshadowing hooks:** Lore Bible data (marginalia lines, a "voice turns toward you" stage), from M2 onward.
- **Terminal:** a self-contained feature (pure C# emulator plus a UI), buildable any time after saves exist (M6).
- **Narrator fight:** after M3 (the combat runtime) and M4 (the boss generator in the game).
