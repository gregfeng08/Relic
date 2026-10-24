# 14 — The Hero: look, equipment visuals, animation

Status: **v1 — brief for stream C (Character)** · Last updated 2026-10-23
Greg wants the hero **procedural**: no downloaded character packs. It's based on Greg's own reference sheet and cover sketch. Those are kept locally in `art-ref/` (gitignored, never committed: the repo is public).

## The look (from Greg's reference)
A wandering adventurer in soft anime cel-shading (Genshin-adjacent), with a warm palette.

| Part | Reference |
|---|---|
| Hair | Very long, straight, white (slightly warm), to below the waist. A side lock curls forward over one shoulder, plus a few loose strands and one cowlick on top. |
| Crown | A small gold crown/tiara pinned on one side of the head (upper left) |
| Eyes | Green (cover art) |
| Scarf | Red-orange scarf wrapped high around the neck and lower face, with tails (pulled up as a mask in the cover art) |
| Coat | Long brown trench coat to below the knees, open at the front, a flared skirt that swings. Big patch pockets, turned-back cuffs, a toggle button at the collar, a hood (up in the cover art). |
| Under | A dark brown or blue shirt; a **yellow belt** with a gold buckle |
| Trousers | Baggy olive-green trousers bunched over the boots |
| Boots | Chunky brown laced boots with dark soles |
| Hands | Metal claw-tipped gauntlets (grey, segmented, pointed fingertips) |
| Kit | Backpack (flap and two pockets), waterskin, health and mana potions (round and slim flasks), a dagger, a long staff with a spiral head and an orb, a torch, food, and an **inherited amulet** on a cord |

Later and out of scope: a **baby dragon companion** (backlog, a later milestone).

## Shown equipment (gear is visible)

| Slot | How it shows |
|---|---|
| Weapon | In the right hand while fighting or drawn; stowed otherwise (staff/bow/greatsword on the back, sword/dagger at the hip, tome on a belt strap). Built from the item's `ItemVisual.KitParts` (see Items `Forms`) with its material, wear and glow. |
| Relic | **The inherited amulet** on the chest, the relic's form at small scale, glowing with `EmissiveColor` × `EmissiveIntensity`. Brighter as resonance builds (later). |
| Armor | It changes the coat. **Light:** the coat alone. **Medium:** plus leather pauldrons, bracers and a chest strap. **Heavy:** plus metal plates on the shoulders and chest, greaves, and a heavier coat. The coat is tinted by the armor material. |
| Trinkets ×2 | Small charms hanging from the belt (shape by trinket kind, glow if Rare or better) |
| Always | The backpack, waterskin and potion flasks on the belt (pillar 7: you carry real things) |

## Animation (procedural, no clips yet)
- Idle breathing and weight shift; run, sprint (lean forward); dash (a low lunge, the coat flaring back); jump and fall; climb; swim.
- **Secondary motion:** hair, scarf tails and coat skirt use simple spring or Verlet chains, so they trail and settle naturally.
- **Hitbox rule (03-combat):** the torso and head are the hitbox. Hair, coat skirt and scarf are cosmetic "glancing" parts and must stay visually distinct and outline-friendly.

## Technical
- **Assembly:** `Assets/Relic/Character/` with `Relic.Character` (runtime) and optional `Relic.Character.Logic` (pure: item → visual mapping, tested). It references Core, Items.Logic (for `Item`/`ItemVisual` data) and World (for `PlayerController` state).
- **Hook:** World must not reference Character. World exposes an avatar factory hook, the Game layer registers the procedural hero at startup, and World falls back to the old placeholder if nothing is registered.
- **Meshes:** generated in code (lofted tubes and capsules, a flared coat skirt, ribbon hair strands) with the World toon material and outline. Target ≤ 15k triangles.
- **Debug:** **F7** rolls and equips a random kit for the current tier, **F9** cycles armor weight, and a small HUD line shows the equipped item names.
