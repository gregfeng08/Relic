# 22 — RelicScript: players write code in the world

Status: **AGREED direction (Greg, 2026-11-18)**; spell unification is a lead recommendation · Last updated 2026-11-18

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

## Spells: glyphs and code are two views of one language (lead recommendation)
**Don't make spells fully programmatic.** Most players won't code, and the drawn-glyph fantasy is beautiful and approachable. But don't keep two separate systems either:
- **One representation:** a spell is a **RelicScript program**. Drawn glyphs are a *visual syntax* for it. The glyph sentence from spellcraft v2 (04) parses into the same syntax tree as typed code.
- **"View as code":** at the Writing Desk, any drawn spell can be opened as code, and simple code can be shown back as a circle. Players who never code never have to; players who want more expressiveness switch views.
- **Teaching by play:** seeing your drawn spell as code is a gentle, optional introduction to programming. Expert spells (loops, conditions, multi-stage) are only practical in code, and that's fine.
- **The Magister** interprets novel *glyph* sentences into this same language (04 v2), so the LLM path and the code path meet in one place.

```csharp
spell.Name = "Tide Weave";
for (int i = 0; i < 3; i++)
    spell.Emit(Shape.Line, angle: i * 15).Seek().OnHit(Soak(4), Damage(8));
spell.Then(Delay(0.5f)).Area(Shape.Ring, radius: 4).Apply(Root(1));
```

## Learning: what to take from Boot.dev (and what not to)
Boot.dev is a gamified coding *course*: XP, levels, daily quests, chests, streaks, leaderboards, and "boss battles" won by solving exercises. It's a learning platform wearing RPG clothes, while Relic is a game that happens to allow code.
- **Take:**
  - **A gentle, ordered ramp:** language features unlock as you learn glyphs. For example, loops come from a "Repetition" glyph found in a ruin, so learning to code *is* exploring (pillar 1).
  - **Instant feedback:** test your spell on the desk's preview dummies.
  - **A helpful tutor:** the optional explanation from the Magister.
  - **Small puzzles:** a few ruins have glyph-locks you solve with a short script.
- **Leave:** XP, streaks, chests and leaderboards for *coding*. They'd turn coding into homework and clash with the grounded pillar. Coding should be a craft you choose, not a chore with rewards attached.

## Build order
1. The interpreter (lexer, parser, interpreter, metering, errors) with heavy tests. One self-contained agent job.
2. Spell bindings: code to EffectProgram, plus the glyph ↔ code round trip. Lands after the combat runtime (M3) and spells v2.
3. Workshop and personal bindings. Then the endgame `relic` command.
