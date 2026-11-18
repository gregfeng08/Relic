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

### In multiplayer too (Greg, 2026-11-18)
The terminal exists in multiplayer, and so does deletion. There it means **erasing your own footprint from a shared world**:
- **What it removes:** your characters, saves, vaults and progress, permanently, through a server-side, server-validated request. Never anyone else's data.
- **Your world-unique relics:**
  - **Vaulted** ones return to the world, to be found again; their lore gains a line about a wayfarer who is no longer remembered.
  - **Carried** ones drop where you stand.
- **Shared history keeps an absence, not a hole (grounded):**
  - the Reliquary entries you wrote, and the legends that name you, become **a crossed-out name** ("a wayfarer whose name has been taken back");
  - your graves become empty waycairns;
  - other players' records stay intact. They just can't read your name anymore.
- **Reflection works the same way:** your journey, replayed and unraveled, but only your threads in the shared map.
- **Account safety:** a stolen or compromised account mustn't be able to wipe someone's history instantly.
  - The server requires a re-authentication at the moment of deletion.
  - It keeps a private, out-of-game backup for a short support window (for example 7 days) for account-recovery cases only.
  - The in-game fiction stays final.
- **Mounts:** the terminal in multiplayer mounts only the player's own data. Shared world data is read-only and never shows other players' private data.

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
