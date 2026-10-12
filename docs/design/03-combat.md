# 03 — Combat & Character

Status: **DRAFT — needs Greg's review** · Last updated 2026-10-12

## Decisions (Greg, 2026-10-12)

| Topic | Decision |
|---|---|
| Camera | **Third-person like Genshin.** Projectiles must be readable. |
| Abilities | **From gear:** weapon = basic attack + skill, relic = ultimate, armor and trinkets = passives. |
| Defense | **Positioning, no invulnerability.** A dodge moves your hitbox; it never makes you untouchable. Closer to RotMG than to Souls. |

## Core feel

**A third-person bullet-hell action RPG.** You see Genshin's world from Genshin's camera. Fights play like RotMG and StarBreak: survival comes from reading patterns and being somewhere else when the shot lands. Every hit you take is one you could have avoided, and that's what makes permadeath fair.

## Controls (keyboard and mouse; controller later)

| Input | Action | Source |
|---|---|---|
| WASD | Move | — |
| Mouse | Camera and aim (center-screen reticle) | — |
| LMB (hold) | Basic attack | Weapon |
| E | Skill | Weapon |
| Q | Ultimate | Relic |
| Shift (tap) | Dash | Stamina |
| Shift (hold) | Sprint | Stamina |
| Space | Jump, climb, glide | Stamina |
| 1 / 2 | HP potion / mana potion | Consumables |
| Tab | Boss camera toggle (see below) | — |

**Aiming:** ranged attacks fire toward the reticle. Melee swings snap a little toward the nearest enemy in front of you (like Genshin). Holding attack while moving is the norm, as in RotMG, so you're always shooting and always dodging.

## Hitbox and dash

- **Small hitbox.** The player's damage hitbox is a small capsule at the character's core, clearly smaller than the visible model. The player only feels this as "that barely missed me," which feels great.
- **Optional hitbox marker:** a faint glowing core during combat (a setting, on by default during boss fights) so precise dodging is learnable. It's like Touhou's focus dot.
- **Dash:** about 4 m in about 0.18 s, costs stamina, short cooldown. **No invulnerability frames.** It's a burst of movement, so a dash *into* a projectile still hits you.
- **Sprint:** faster movement that drains stamina. It's the main way to escape dense patterns.
- **Air:** jumping and gliding move you over ground-level patterns but expose you to aerial ones. Bosses use that trade-off.

## Readable projectiles (third-person bullet hell)

Third-person cameras make depth hard to judge, so the rules are strict:

1. **Most projectiles travel on a plane near the ground.** Patterns are 2.5D, readable from behind the character like RotMG viewed at an angle.
2. **Everything airborne casts a ground marker** (a shadow or a landing decal), so you dodge the marker, not the 3D object.
3. **Wind-ups always come with a warning:** a pose, a sound and a ground decal that fills up before damage. Bigger attacks wind up longer.
4. **Color language is fixed across the game:** enemy projectiles are high-contrast with a bright core and a dark outline, and they never use the player's own effect colors. Toon shading helps a lot here.
5. **Boss camera:** inside a boss arena the camera pulls back and up, with a wider field of view and a slight top-down tilt, so the whole pattern is visible. Tab switches back to the normal close camera.
6. **No unavoidable damage.** Every damage source in the game can be dodged by positioning. This is a hard rule.

## Damage model

- **Defense (DEF)** comes from armor and subtracts flat damage per hit, with a floor so nothing is ever fully blocked: `damage = max(raw − DEF, raw × 0.2)`. Many weak shots are blocked well by DEF; big hits mostly get through. Building for DEF suits the "power through it" player.
- **Critical hits** come from AGI and gear.
- **Status effects** on enemies come from the item effect grammar: burn, chill (slow), soaked, shock, stagger, root. On the player: slowed, weakened, silenced (no mana abilities), dazed (no dash). VIT shortens their duration.

### Power bands (a target to tune)

| Setup | Damage per second vs. starter |
|---|---|
| Starter gear, untrained | 1× |
| Good gear from mid-distance regions | ~3× |
| Top-ceiling gear, trained stats, essences | ~8× |
| Plus overrolls (rare) | up to ~12× |

Boss health is fixed by region tier. A far boss that takes about 3 minutes with top gear takes about 25–40 minutes with starter gear. That's grueling but **possible**, which is the StarBreak feel.

**Consequence: no hard enrage timers.** A timer that wipes you at 10 minutes would be a power gate in disguise. Bosses can change phases or patterns over time, but never "kill if the fight runs long."

## Resources

| Resource | Max set by | Regen set by | Notes |
|---|---|---|---|
| HP | VIT, armor | VIT | HP potions on hotkey 1; slow regen out of combat |
| Mana | INT, gear | INS | Spent by mana abilities; mana potions on hotkey 2 |
| Stamina | VIT | AGI | Dash, sprint, climb, glide, heavy attacks |
| Ultimate charge | — | Dealing and dodging damage | Fills the relic's ultimate; dodging near a projectile ("grazing") gives a little charge, which rewards precise play |

## Gear slots

| Slot | Gives |
|---|---|
| Weapon | Basic attack pattern + skill (E). The weapon type decides the playstyle: sword, greatsword, bow, staff, tome-and-focus, and so on. |
| Relic | Ultimate (Q) + its custom enchantments. One relic equipped at a time. |
| Armor | DEF, HP, passives. Light, medium and heavy, with movement trade-offs. |
| Trinket × 2 | Passives and stat bonuses. |

A "mage" is a staff weapon, a spell-focused relic and INT/INS trinkets. A "tank" is heavy armor, a greatsword and VIT. The build is whatever you assemble.

## Enemies: harder through behavior

Farther regions raise danger mostly by **how enemies act**:
- More projectiles per pattern with smaller gaps
- Faster projectiles, shorter (but always present) wind-ups
- Mixed enemy groups whose patterns overlap
- Elites with modifiers, generated with the same grammar as items ("Soaking," "Hasted," "Splitting")
- Moderate number scaling (health and damage) on top of that, so power still matters

## Bosses

- **Multiple phases**, each adding or remixing patterns
- **Learnable**, the same patterns every time for a given boss archetype
- **The arena matters:** pillars to hide behind, hazards, falling terrain
- **The archetype decides essence and theme** (Librarian → INT essence, book-swarm patterns, a library arena)
- Bosses are generated from **pattern building blocks** (rings, spirals, aimed bursts, lasers, area denial, summons), combined by rules and themed by the archetype. That's a separate boss-generation spec later.

## Open questions (for Greg)

1. **Elements and reactions?** Genshin-style reactions (soaked + shock = electrocute) would give relic enchantments rich material and make combos fun. I recommend a **small set (4–5 elements, about 6 reactions)**. Or no elements, just status effects?
2. **Power bands:** does "a top-gear boss takes about 3 minutes; with starter gear, about 30" feel right, or should the gap be wider?
