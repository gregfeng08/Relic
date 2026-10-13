# 03 — Combat & Character

Status: **DRAFT — revision 2 (grounded pass), one question open** · Last updated 2026-10-13

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
| Q | Release relic (ultimate) | Relic |
| Shift (tap) | Dash | Stamina |
| Shift (hold) | Sprint | Stamina |
| Space | Jump, climb, glide | Stamina |
| 1 / 2 | HP potion / mana potion | Consumables |
| Tab | Boss camera toggle (see below) | — |

**Aiming:** ranged attacks fire toward the reticle. Melee swings snap a little toward the nearest enemy in front of you (like Genshin). Holding attack while moving is the norm, as in RotMG, so you're always shooting and always dodging.

## Hitbox and dash

- **The body is the hitbox.** You get hit where your torso and head are. Shots that only clip a loose sleeve, a cape or a hair strand don't wound you. In-world, that's a glancing brush, not a hit. The player feels this as "that barely missed me," and it's honest.
- **No floating hitbox marker** (it isn't grounded). Precise dodging is learned by reading your character's silhouette, helped by a clean toon-shaded outline in combat. An accessibility setting can add a marker, off by default.
- **Dash:** about 4 m in about 0.18 s, costs stamina, short cooldown. **No invulnerability frames.** It's a burst of movement, so a dash *into* a projectile still hits you.
- **Sprint:** faster movement that drains stamina. It's the main way to escape dense patterns.
- **Air:** jumping and gliding move you over ground-level patterns but expose you to aerial ones. Bosses use that trade-off.

## Readable projectiles (third-person bullet hell)

Third-person cameras make depth hard to judge, so the rules are strict:

1. **Most projectiles travel on a plane near the ground.** Patterns are 2.5D, readable from behind the character like RotMG viewed at an angle.
2. **Everything airborne casts a real shadow**, made strong and crisp by the toon shading, so you dodge the shadow. Falling rocks, leaping enemies and lobbed orbs all show where they'll land the way they would in real life.
3. **Wind-ups are physical tells, never UI decals:** a pose, a sound, plus a world effect such as dust kicking up, ground cracking, light gathering in a staff, or runes on the floor glowing brighter (the runes are part of the boss's magic and explained by its lore). Bigger attacks wind up longer.
4. **Color language is fixed across the game:** enemy projectiles are high-contrast with a bright core and a dark outline, and they never use the player's own effect colors. Toon shading helps a lot here.
5. **Boss camera:** inside a boss arena the camera pulls back and up, with a wider field of view and a slight top-down tilt, so the whole pattern is visible. Tab switches back to the normal close camera.
6. **No unavoidable damage.** Every damage source in the game can be dodged by positioning. This is a hard rule.

## Damage model

- **Defense (DEF)** comes from armor and subtracts flat damage per hit, with a floor so nothing is ever fully blocked: `damage = max(raw − DEF, raw × 0.2)`. Many weak shots are blocked well by DEF; big hits mostly get through. Building for DEF suits the "power through it" player.
- **Critical hits** come from AGI and gear.
- **Status effects only, no elemental reactions** (Greg, 2026-10-13). Every status is something physical:

| Status | What it is | Effect |
|---|---|---|
| Bleeding | Open wound | Damage over time; bandages stop it |
| Burning | On fire | Damage over time; water or rolling on the ground puts it out |
| Chilled | Cold, stiff muscles | Slower movement and attacks |
| Soaked | Wet | Slower stamina regen; puts out fire |
| Poisoned | Toxin | Damage over time; reduced healing |
| Staggered | Knocked off balance | Brief interrupt |
| Rooted | Held by vines, ice or webs | Can't move; can still attack |
| Exhausted | Out of breath | No dash or sprint until stamina recovers |
| Dazed | Blow to the head | Unsteady aim; camera sway |
| Silenced | Throat or focus disrupted | No mana abilities |

VIT shortens durations. Statuses interact in ways that make physical sense (soaked puts out burning), but there's no reaction system.

### Power bands (a target to tune)

| Setup | Damage per second vs. starter |
|---|---|
| Starter gear, untrained | 1× |
| Good gear from mid-distance regions | ~2.5× |
| Top-ceiling gear, trained stats, essences | ~6× |
| Plus overrolls (rare) | up to ~8× |

Boss health is fixed by region tier. Target (Greg, 2026-10-13): **a far boss takes about 5 minutes with top gear and about 30 with starter gear.** That's grueling but **possible**, which is the StarBreak feel. *Open: whether "a focus on group fights" means these times are for a party or for solo players fighting groups of enemies (see open questions).*

**Consequence: no hard enrage timers.** A timer that wipes you at 10 minutes would be a power gate in disguise. Bosses can change phases or patterns over time, but never "kill if the fight runs long."

## Resources

| Resource | Max set by | Regen set by | Notes |
|---|---|---|---|
| HP | VIT, armor | VIT | HP potions on hotkey 1; slow regen out of combat |
| Mana | INT, gear | INS | Spent by mana abilities; mana potions on hotkey 2 |
| Stamina | VIT | AGI | Dash, sprint, climb, glide, heavy attacks |
| Relic resonance | — | Danger and combat | The relic's ultimate. In-world, relics feed on the intensity of battle: hits you land and close calls (projectiles passing within a hand's width) build resonance until the relic can be released. Rewards precise play, and the lore explains it. |

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
- Elites with physical, explainable modifiers ("Veteran," "Armored," "Frenzied," "Pack leader"), generated with the same grammar as items
- Moderate number scaling (health and damage) on top of that, so power still matters

## Bosses

- **Multiple phases**, each adding or remixing patterns
- **Learnable**, the same patterns every time for a given boss archetype
- **The arena matters:** pillars to hide behind, hazards, falling terrain
- **The archetype decides essence and theme** (Librarian → INT essence, book-swarm patterns, a library arena)
- Bosses are generated from **pattern building blocks** (rings, spirals, aimed bursts, lasers, area denial, summons), combined by rules and themed by the archetype. That's a separate boss-generation spec later.

## Resolved
- Plain physical status effects only, no elemental reactions.
- Power target: about 5 minutes with top gear, about 30 with starter gear.

## Open questions (for Greg)
1. **"A focus on group fights"** could mean either:
   a. **Bosses are designed for groups of players** (RotMG-style crowds at a boss), with health that scales with player count.
   b. **Combat focuses on fighting groups of enemies** (packs, mixed squads) more than one-on-one duels.
   c. Both.
