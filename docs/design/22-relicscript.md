# 22 — RelicScript: players write code in the world

Status: **AGREED (Greg, 2026-11-18)**: programmatic spells are the main form; constructs approved · Last updated 2026-11-18

## What it is
A **C#-flavored scripting language** that players write in-game. It looks like C# (`var`, `if`, `for`, `foreach`, functions, simple classes, lambdas), but it runs on **our own interpreter** (pure C#, `Assets/Relic/Script/Logic`), not on .NET. In-world it's the strictest kind of sentence the Magister reads.

### Why not real C#
- **Security:** runtime-compiled C# has the whole of .NET (files, network, reflection). In multiplayer that's how servers get taken over. Space Engineers sandboxes real C# with whitelists and still had exploits.
- **Builds:** IL2CPP (shipping builds) can't compile code at runtime.
- **Fairness:** unrestricted code means bots and server-freezing loops.

### The interpreter's guarantees
- **Whitelisted API only.** The language has no file, network, reflection or threading features at all, and scripts see only game objects we expose.
- **Metered:** an instruction budget per run and per tick, memory caps, and a recursion limit. Infinite loops simply run out of budget.
- **Deterministic:** the same script and inputs give the same result on every machine. In multiplayer the server runs it authoritatively.
- **Works in every build** (IL2CPP included): tree-walking or bytecode interpretation, no runtime compilation.
- **Readable errors** ("line 3: `Damage` needs a number, got a status"), plus an optional LLM helper that explains a failure in plain words.

## What players can script
| Area | Examples | Limits |
|---|---|---|
| **Spells** | Compose an effect program in code | Same validator and power budget as every spell (05). Elegance, never extra power |
| **Workshop** (sanctuary home) | Lanterns, gates, music boxes, lifts, contraptions | No combat effect |
| **Personal** | HUD widgets, `HeroAppearance`, sky and weather (single-player) | Cosmetic or own-world only |
| **Terraforming** | Small edits in your own world | Written as L0 deltas (21) within a budget |
| **Endgame terminal** (19) | A `relic` command running scripts with admin reach | The terminal's security limits |

Never: arbitrary game logic, other players' data (except the endgame admin powers), or anything that skips the budget.

## Spells: programming *is* wizardry (Greg, 2026-11-18)
Greg: "I don't like the idea of drawn glyphs competing with programming. Maybe a base set of spells, and then programmatic spellcasting as the main form with a spell API. That way wizards can just be programmers."

- **No glyph drawing.** The v1 Writing Desk (freehand glyphs) is retired as a spell system (see 04).
- **A base set of spells** that everyone can use without coding:
  - starter spells you begin with;
  - spell pages found in ruins and sold in sanctuaries;
  - spells written into found staves and tomes.

  These are ordinary RelicScript programs that you can open, read and **modify**. Reading a found spell is how players learn the API.
- **Programmatic spellcasting is the main form.** A wizard build *is* a programmer build. Your tome is a spellbook of scripts, written at a desk (an in-game code editor) and cast in combat from the selected page.
- **The Spell API** is a small, well-documented, discoverable library (`spell.Emit`, `Shape`, `OnHit`, `Area`, `Then`, `Delay`, statuses, targeting, conditions, …). Every spell compiles to an EffectProgram, so the validator and power budget still apply. Bigger programs cost more mana and longer cooldowns; elegance is the reward, never extra power.
- **Discovering the API is exploration (pillar 1).** Functions and keywords are learned from spell pages, ruin inscriptions and teachers. An unknown call says "you don't know this incantation yet". Finding a "Repetition" codex in a ruin is what teaches `for` loops.
- **For non-programmers** (an optional lead suggestion): describe a spell in plain words to the Magister, and it drafts a script for you to read and keep. It goes through the same budget and is cached. Base spells and found pages keep any build viable without coding.
- **In-world (pillar 7):** magic is *writing*. The world reads written incantations (the Listening, Lore Bible). RelicScript's syntax is rendered in-world as a runic script on pages, so a spell page looks magical and reads as code. The Lore Bible's glyph section is updated to "written incantations" (RLC-051).

```csharp
// A found page: "Tide Weave", by a Solannish lighthouse-keeper
spell.Name = "Tide Weave";
for (int i = 0; i < 3; i++)
    spell.Emit(Shape.Line, angle: i * 15).Seek().OnHit(Soak(4), Damage(8));
spell.Then(Delay(0.5f)).Area(Shape.Ring, radius: 4).Apply(Root(1));
```

## Constructs: ComputerCraft-style programmable helpers (LATER: Greg, 2026-11-18: "a todo later")
Greg spent hours programming ComputerCraft turtles, so Relic gets its own: **constructs**. These are small programmable automatons, powered by resonance in-world: clockwork beetles, lantern-wisps, stone golems.
- **What they do:** dig, build, farm, haul, ferry, scout, light paths and guard, all run by RelicScript programs you write. They act through the same world-edit layer as everything else (L0 deltas, 21) and are limited per action, so they can't flatten the world.
- **The API (turtle-like):** `move(dir)`, `turn()`, `dig()`, `place(item)`, `inspect()`, `detect()`, `suck()`/`drop()` for inventory, `refuel(resonance)`, `say()`, plus events (`onTick`, `onSee`, `onMessage`) and messaging between constructs (`send`, `receive`, a little wireless network).
- **Where:** anywhere, but with rules. They don't dig inside sanctuaries, don't affect other players' graves or claims, and wild regions push back (the Warden's guardians notice large works).
- **Running costs:** a construct runs while you're within range (like chunk loading). Far away, it pauses, or runs at a slow simulated rate. Each construct has an instruction budget per tick, and you can own only a few at first (more with progression).
- **Multiplayer:** constructs run server-side, metered and deterministic. They're visible to others, and can cooperate between friends.
- **Fourth-wall fit:** players become small-scale agents themselves, writing programs that keep *their* corner of the world running, like the crew (20). The Rogue loves hijacking badly written constructs.
- **Companions:** the baby dragon companion (backlog) could accept simple scripts too ("fetch", "light", "follow at distance").

## Learning: what to take from Boot.dev (and what not to)
Boot.dev is a gamified coding *course*: XP, levels, daily quests, chests, streaks, leaderboards, and "boss battles" won by solving exercises. It's a learning platform wearing RPG clothes, while Relic is a game that happens to allow code.
- **Take:**
  - **A gentle, ordered ramp:** API functions and language features are discovered in the world. For example, loops come from a "Repetition" codex found in a ruin, so learning to code *is* exploring (pillar 1).
  - **Instant feedback:** test your spell on the desk's preview dummies.
  - **A helpful tutor:** the optional explanation from the Magister.
  - **Small puzzles:** a few ruins have script-locks (and construct puzzles) you solve with a short program.
- **Leave:** XP, streaks, chests and leaderboards for *coding*. They'd turn coding into homework and clash with the grounded pillar. Coding should be a craft you choose, not a chore with rewards attached.

## Build order
1. **The interpreter** (lexer, parser, interpreter, metering, errors, whitelisted host API) with heavy tests. One self-contained agent job (RLC-048).
2. **The Spell API, a spellbook editor and base spells:** code to EffectProgram, the in-game code editor (syntax highlighting, errors, a preview dummy), and about 20 base spells as readable scripts. Lands with or after the combat runtime (M3).
3. **Constructs: later** (Greg: a todo for later, too specific for now). It depends on the world layers (21) when it happens.
4. **Workshop and personal bindings,** then the endgame `relic` command.
