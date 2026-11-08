# 04 — Spellcraft (drawn magic)

Status: **CONCEPT AGREED** (a full spec comes with the workstream) · Last updated 2026-10-13

Inspired by the *idea* behind Witch Hat Atelier's magic: magic isn't an innate gift, it's a **craft**. Spells are drawn as circles of symbols, and anyone who learns the symbols can cast. We borrow that concept only. All our glyphs, names and lore are original.

## Why it fits Relic

| Pillar | How spellcraft serves it |
|---|---|
| Grounded in the world | Magic has rules a character can explain: this symbol means water, this one means "outward." No class, no "you're a mage." |
| Secrets all the way down | **Glyphs are discovered**: carved in ruins, written in ancient spell pages, taught by NPCs. High INS reads older and stranger glyphs. |
| Nothing is locked | Anyone can learn magic; power comes from knowledge and practice. |
| Loot that leads somewhere | Ancient spell pages found in dungeons are loot and lore at once: "Who drew this? Where's the rest?" |
| Gear-defined builds | A mage build is a tome full of spells *you* designed. |

## How a spell is built

A spell is a circle with three layers. It compiles into the **same effect grammar** that relics use (trigger → condition → effect), so one engine runs relics, spells, enemy abilities and elite modifiers.

```
        ┌─────────── Ring glyphs: shape, motion, timing ───────────┐
        │    (cone · line · ring · wall · orbit · return · delay)   │
        │        ┌──── Inner glyphs: modifiers ────┐                │
        │        │  (more · less · far · lasting · │                │
        │        │   split · seek · heavy)         │                │
        │        │      ┌── Core sigil ──┐         │                │
        │        │      │ substance:     │         │                │
        │        │      │ water · flame ·│         │                │
        │        │      │ stone · wind · │         │                │
        │        │      │ light · growth │         │                │
        │        │      └────────────────┘         │                │
        │        └─────────────────────────────────┘                │
        └───────────────────────────────────────────────────────────┘
```

- **Core sigil:** what the spell is made of. It's physical, so it maps to status effects: water soaks, flame burns, stone staggers, growth roots.
- **Inner glyphs:** how much, how far, how long, how many.
- **Ring glyphs:** the shape and motion of the result.
- **Placement matters:** the order around the ring sets the sequence ("cone, then delay, then split"), and glyph size sets emphasis.

**Example.** A water core, plus "split ×3" and "seek," with "line" on the ring, makes three water bolts that curve toward targets and soak them.

## Where the semantic interpretation comes in

The rules that matter for fairness are **deterministic**: same drawing, same spell, every time, which is required for multiplayer later. Interpretation is layered:

1. **Recognizing strokes.** You draw with the mouse or a pen. A stroke recognizer (in the $Q point-cloud family, fast and offline) works out which glyph you drew.
2. **Precision equals quality.** A clean drawing makes a stable, efficient spell. A sloppy one costs more mana, misfires more or is weaker. This is grounded (careless ink, careless magic) and rewards skill.
3. **Reading the arrangement.** Simple geometry (position on the ring, size, order) gives each glyph its role.
4. **Compiling and balancing.** The result becomes effect-grammar JSON, which the power-budget validator checks. Bigger spells cost more mana, more ink and longer cast times. Nothing is free.
5. **The LLM adds flavor only:** a spell name, a description in the world's voice, and an NPC scholar's commentary. The LLM never decides how strong a spell is.
6. **Generated spells use the same rules:** the LLM composes ancient spell pages and enemy spellcasters from glyphs, so everything a player finds can, in principle, be learned and redrawn.

## In play

- **Design at a desk, cast in the field.** Drawing in the middle of a bullet hell is impossible, so spells are written in advance: at a sanctuary writing desk, at a campfire, or in a quiet moment.
- **Ink is a material.** Spells are written onto **tome pages, scrolls or staff carvings** with ink made from monster parts and minerals. Better ink makes more efficient spells, which creates a crafting loop.
- **Casting:** a tome is a weapon. Its pages are your spells, so the weapon skill (E) casts the selected page. Scrolls are one-shot consumables. Staff carvings are permanent but limited in size.
- **Learning glyphs:** you learn a glyph by seeing it, from a ruin carving, a found page or a teacher. Practicing it (drawing it cleanly) trains INT and raises your precision. Some ancient glyphs need high INS even to read.
- **Death:** your tome lands in your grave. Your knowledge of glyphs dies with you, but **the Reliquary library keeps copies of spells you recorded**, so a successor can relearn them.

## Risks

| Risk | Mitigation |
|---|---|
| Drawing feels clunky with a mouse | Large, forgiving glyph shapes; a "trace a known glyph" helper; a controller mode that places glyphs instead of drawing them |
| Players discover broken combinations | Every spell passes the power budget; cost scales faster than effect |
| Too complex for new players | Start with three glyphs and premade pages; complexity grows as you discover more |
| Scope | Ship a small glyph set first (6 cores, 8 modifiers, 7 shapes). Glyphs are data, so more can come later. |

## v2 direction (PROPOSAL, from Greg's review 2026-10-28)
Greg: "Seems a bit complex. Maybe we can just understand it semantically via its attributes? We construct a base language with rules, maybe element types, and then everything on top is semantically understood. That way, people can make extremely complex spells so long as it's understandable by the LLM reviewing it."

**Proposal: a small base language plus a semantic interpreter.**
- **Fewer, very distinct glyphs, typed like words:**
  - **Elements** (nouns): water, fire, stone, wind, light, growth
  - **Verbs:** strike, shield, bind, seek, scatter, return, linger
  - **Qualities** (adjectives): more, less, far, swift, heavy
  - **Relations** (connectors): *then*, *around*, *when hit*, *toward*, *on self*

  Drawing stays simple and recognition stays near-perfect, because the meaning comes from **combination**, not from a big glyph set.
- **The spatial layout becomes a sentence.** The parser turns glyph positions and connectors into a structured sentence (a tree), for example "water · scatter · seek · *then* · bind · *when hit*". Any well-formed composition is allowed, with no fixed recipe slots, so spells can grow as complex as players like.
- **The interpreter reads the sentence and writes the effect.**
  1. A **deterministic core** compiles common patterns instantly.
  2. Novel or complex sentences go to an **LLM "Magister"**, which turns the sentence into an EffectProgram.
  3. **Every result passes the validator and the power budget.** Complexity costs mana and cast time, so power stays bounded however clever the spell.
- **Deterministic and fair:**
  - Each interpretation is **cached by the sentence's canonical form**, so the same drawing always gives the same spell.
  - In multiplayer, the server interprets and shares.
  - Spells are written at a desk, not in combat, so LLM latency doesn't matter.
- **Grounded (pillar 7):** in-world, spells are "read" by the world's magic. A glyph sentence is an instruction the magic understands. That's the lore explanation for the Magister (Lore Bible, 16).

The v1 recognizer and the Writing Desk carry over. What changes is the glyph set, the parser (connectors and a tree instead of fixed rings) and the compiler (core plus Magister).

## Brief for stream M (v1)

**Glyph set v0.** Small on purpose; glyphs are data, so more can come later.
- **Cores (6):** Water → Soaked, Flame → Burning, Stone → Staggered + Physical damage, Wind → Knockback, Light → Dazed, Growth → Rooted
- **Inner modifiers (8):** More (amount ↑), Less (cost ↓), Far (range ↑), Lasting (duration ↑), Split (SplitCount / Count ↑), Seek (Seeking), Heavy (size ↑, speed ↓), Swift (speed ↑)
- **Ring shapes and motion (7):** Line, Cone, Ring, Wall (CreateBarrier), Orbit, Return (Returning), Delay

**Pipeline** (all of it pure C# in `Magic/Logic` except the drawing UI):
1. **Strokes:** a list of polylines, normalized. Recognize glyphs with a $Q-style point-cloud recognizer against templates stored as data. It returns glyph id, confidence and a **precision score** (0–1).
2. **Circle parse:** find the outer circle, then sort glyphs into core (center), inner ring and outer ring by radius. Order on the outer ring is clockwise from the top. Glyph size relative to the circle gives emphasis. Reject spells with no core or an unclosed circle.
3. **Compile** to an `EffectProgram` with one Active rule: core → payload, modifiers → magnitudes and emitter flags, ring → emitter shape and area. Cooldown and mana cost are **derived from `PowerBudget.Cost`**: the more powerful the spell, the more expensive it is to cast. Precision scales efficiency (sloppy = more mana, longer cooldown, a misfire chance).
4. **Validate** with `EffectValidator`. Report errors in plain language ("a Wall needs a Line or a Ring to shape it").
5. **Name and flavor** come through an `ISpellNamer` interface (the LLM later). Use a deterministic stub name for now.

**Unity side (`Magic/Runtime`):**
- A **Writing Desk scene** (`Magic/Scenes/WritingDesk.unity`): draw with the mouse on a parchment canvas, see the recognized glyphs highlighted, and see the compiled spell's JSON, cost, cooldown and mana.
- A **glyph palette** fallback (click to place glyphs in slots).
- A **preview** that plays the spell's emitter shape as simple debug gizmos or particles in a test arena.

**Pre-written spells:** a function that builds a spell drawing (glyph placements) from an `EffectProgram`-compatible recipe, so found tomes can carry spells whose glyphs the player can study. It must round-trip: recipe → circle → compile gives the same program.

**Acceptance:**
- Recognizer accuracy ≥ 95% on a synthetic test set (templates with noise, rotation and scale jitter).
- The example spell (water core + Split ×3 + Seek + Line) compiles to the Tide Seekers program in CoreTests, within tolerance.
- Greg can draw five different spells at the desk without frustration ("feel" review).
- Pure logic tested in dotnet and Unity; `Assets/Relic/Magic/README.md` written.

## Decisions (Greg, 2026-10-13)
1. **Freehand drawing, plus a glyph palette** (as a fallback and for controllers). Precision matters when drawing.
2. **Found staves and tomes carry pre-written spells.** You can use them right away and study them to learn their glyphs.
