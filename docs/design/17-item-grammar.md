# 17 — Item Grammar

Status: **PROPOSAL — needs Greg's review** · Last updated 2026-10-28
Greg: "items seem to be all over the place." The effect grammar (05) makes effects *valid*. Nothing yet makes an item *coherent*: its stats, effects, look and name currently get chosen fairly independently. This grammar makes every part of an item follow from a few choices made first.

## An item is a sentence

```
Item = Base · Archetype · Culture/Maker · Material · Theme · Quality · Name
        │       │            │              │          │        │
        │       │            │              │          │        └─ naming pattern of the culture
        │       │            │              │          └─ motif + mood → effect family
        │       │            │              └─ look + one small property tag
        │       │            └─ forms, decorations, maker's mark (from the Lore Bible, 16)
        │       └─ purpose → which stat lines and which effect triggers fit
        └─ weapon/armor/trinket/relic type → slot, kit allowance (05)
```

**Order of decisions:** Base → Archetype → Culture → Material → Theme → stats → effects → look → name → lore. Each step is constrained by the ones before it.

## Archetypes (the item's purpose)
| Archetype | Feels like | Favored stat lines | Favored triggers and effects |
|---|---|---|---|
| Duelist | precise, quick | AGI, crit, attack speed | OnHit, OnCloseCall; bleed, stagger |
| Warden | protective | VIT, DEF, max HP | OnTakeDamage, auras, barriers; root, knockback |
| Hunter | patient, ranged | AGI, range | OnKill, distance conditions; mark, slow |
| Scholar | clever, arcane | INT, INS, mana | Active spells, Periodic; soaked, dazed, silenced |
| Wanderer | hardy, travel | stamina, move speed, INS | OnDash, OnLowHealth; restore, regen |
| Smith | sturdy, crafted | STR, DEF | heavy hits, stagger, burning |
| Bard/Herald | social, team | CHR, ally buffs | auras on allies, dazed enemies |

**Coherence rules** (a validator in Items):
- A staff never rolls STR as its main line.
- Heavy armor never gets AGI bonuses.
- Effects come from the archetype's favored list about 80% of the time.

## Themes: motif → effect family
Each **motif** maps to physical statuses and shapes, so effects match the item's story:

| Motif | Statuses | Shapes | Example |
|---|---|---|---|
| Tide | soaked, pull | waves, lines | "the tide pulls them close" |
| Ember | burning | bursts, cones | |
| Frost | chilled, root | rings, walls | |
| Bell | dazed, knockback | rings | "the toll staggers" |
| Thorn | bleeding, root | areas | |
| Wind | knockback, haste | cones, orbit | |
| Lantern | dazed, reveal (INS) | auras | |

The **mood** (bright or somber, 16) shifts the trigger style. Bright moods lean toward ally, aura, discovery and celebration effects. Somber ones lean toward curses and drawbacks.

## Materials
- **Culture and tier pick the material** (cloth, leather, bronze, steel, silver, moonstone, rune-oak…).
- **Look:** the material sets the tint, wear and glow.
- **One small property tag**, for example silver = +damage vs. spirits, rune-oak = +INS.
- **Grounded:** a sword is metal, a tome is paper and leather.

## Names (per culture)
- **Patterns:** "[Maker]'s [Form]", "[Form] of [Place]", "[Epithet] [Form]", "The [Adjective] [Form]"
- **Proper nouns:** use the culture's phonology.
- **The LLM** may add flair within the pattern, but never invents proper nouns (they come from the world graph).

## Layers by rarity
| Rarity | Layers present |
|---|---|
| Common | Base · Material · Quality (a plain name) |
| Uncommon | + Archetype (one themed stat line) |
| Rare | + Theme, a real name |
| Epic | + Culture and Maker, a short lore line, 1 enchantment |
| Legendary (relic) | Everything, plus world references, leads and an ultimate |

## What changes in code
1. Items Logic gets an **`ItemGrammar`** that resolves the sentence first. Stat tables, effect templates, visuals and names all read from it.
2. Add a **coherence validator** alongside the budget validator.
3. **Data** for archetypes, motifs and materials (and cultures, from the Lore Bible).
4. **Regenerate the samples** and review them for coherence and tone.
