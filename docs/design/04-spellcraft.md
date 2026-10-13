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

## Decisions (Greg, 2026-10-13)
1. **Freehand drawing, plus a glyph palette** (as a fallback and for controllers). Precision matters when drawing.
2. **Found staves and tomes carry pre-written spells.** You can use them right away and study them to learn their glyphs.
