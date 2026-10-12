# 02 — Core Loop

Status: **AGREED (revision 4)** · Last updated 2026-10-12

## Decisions (Greg, 2026-10-12)

| Topic | Decision |
|---|---|
| Death | **Permadeath.** Carried items drop into a **grave** at the death location. In multiplayer, anyone can loot it. A **vault** keeps stored items safe. |
| Gating | **Nothing is locked.** A stock character *can* beat anything (slowly, like Star Cathedral on a stock Duskwing). |
| Power ceiling | **Very high**, so less-skilled players can power through. Stat rolls can go past it, with exponentially rarer rolls. |
| Hero | **One hero.** No party, no classes. |
| Builds | **Defined by gear.** |
| Character growth | **No XP levels, but stats grow by doing**: train to get stronger, study to get wiser. Six stats: STR, AGI, VIT, INT, INS, CHR, plus mana for magic. Permanent boosts come from boss **essences** bound into the soul at a Soulbinder. |
| Hubs | **Sanctuaries** (Wynncraft cities): live, trade, vault. |
| Knowledge | Reliquary knowledge carries across characters for now. Open to changing it. **Maps might be items** (see below). |

## Three loops

### Moment to moment (seconds)
**Traverse → spot → engage.**
Run, climb and glide (Genshin). Spot silhouettes, glints, ruins, camps. Action combat with dodges, gear-granted abilities and readable tells. Loot pops with rarity colors. Permadeath gives every fight real tension.

### Session (30–90 minutes)
**Sanctuary → follow a lead → explore → dungeon → boss → loot → get it home (or push on).**

1. **Prepare at a sanctuary.** Vault, trade, bind essences, read the rumor board, buy or copy maps, pick a lead.
2. **Explore.** Cross a region, find points of interest, fight, grab loot. Training happens naturally as you play.
3. **Dungeon.** Graph-generated, themed by the region's lore, ending in a boss.
4. **Payoff.** Boss loot, maybe a relic or a permanent boost. Relic lore names new places, which become new leads.
5. **Get it home.** Vault the valuables, or gamble on going deeper. Everything carried is at risk until it's vaulted.

### Long term (weeks)
- **Push outward.** Danger rises with distance from the origin. Far sanctuaries are destinations in themselves.
- **Complete the Reliquary.** Collect relics, resolve their leads.
- **Craft and perfect builds.** Hunt for great rolls; reroll at the forge.
- **Grow the character.** Train stats, earn permanent boosts. Permadeath makes a well-developed character precious.
- **(Later)** Shared world: lootable graves, first discoveries, world-unique relics, player trading, map trading, D&D-style campaigns.

## Power: three ways to get stronger, plus skill

```
 effective power = player skill  ×  ( gear  +  trained stats  +  permanent boosts )
                   (always the        (very high combined ceiling; nothing is
                    shortcut)          required — it makes things easier)
```

### 1. Gear with exponential rolls
Each stat on an item has a normal range up to a **ceiling**. A roll can also land **past the ceiling** in steps, with each step exponentially rarer:

P(at least +k steps past the ceiling) = e<sup>−λk</sup>

With λ = 1 (a tuning knob) and each step = +5% over the ceiling:

| Past ceiling | Chance per roll | Average rolls needed |
|---|---|---|
| +1 (+5%) | 36.8% | ~3 |
| +2 (+10%) | 13.5% | ~7 |
| +3 (+15%) | 5.0% | ~20 |
| +5 (+25%) | 0.67% | ~150 |
| +8 (+40%) | 0.034% | ~3,000 |
| +10 (+50%) | 0.0045% | ~22,000 |

- **No visible hard cap.** Every roll has a chance at something better, but each step is about 2.7× rarer than the last.
- **A hidden safety cap** (for example +10) stops a once-in-a-million roll from breaking balance in a shared world.
- **Forge rerolls** draw a fresh sample each time, so chasing a +5 costs about 150 rerolls on average. That's a long-term goal by design.

### 2. Trained stats (learn by doing)
No XP levels. Stats grow from what you actually do: fighting up close trains STR, studying lore trains INS. Growth slows down the higher a stat gets (diminishing returns), so training helps a lot early and becomes a slow polish later.

Stat list (Greg, 2026-10-12):

| Stat | Grows by | Affects |
|---|---|---|
| **STR** Strength | Heavy weapons, carrying, climbing | Melee damage, stagger |
| **AGI** Agility | Precise hits, perfect dodges, sprinting | Move speed, attack speed, crit chance |
| **VIT** Vitality | Taking hits and surviving, long journeys | Max HP, HP regen, stamina, debuff resistance |
| **INT** Intelligence | Casting spells, solving puzzles | Spell power, **max mana** |
| **INS** Insight | Reading lore, studying relics, meditating | **Mana regen**, **seeing secrets** (hidden doors, ancient scripts, lore details) |
| **CHR** Charisma | Talking with NPCs, trading | Prices, NPC disposition, persuasion checks |

**Insight is the pillar-1 stat.** A high-INS character literally sees more of the world: they can read scripts others can't and notice cracked walls. Studying makes the world deeper.

Trained stats are lost on permadeath. That's what makes death sting even with a full vault.

### Resources: HP, mana, stamina
Every character has all three bars. Their gear decides which ones they rely on.

| Resource | Used for | Pool | Regen |
|---|---|---|---|
| HP | Staying alive | VIT | VIT |
| Mana | Spells and magical abilities (from staves, tomes, relics) | INT | INS |
| Stamina | Dodges, sprinting, climbing, gliding, heavy attacks | VIT | AGI |

- **A mage is a gear build, not a class.** Equip a staff and tomes and your abilities draw on mana, so you train INT and INS. A warrior build lives on stamina and STR. Hybrids, like a spellblade, juggle both.
- **Why INT and INS split mana:** INT is how much you can hold, INS is how fast it comes back. Mages want both, and studying the world (INS) literally fuels magic.

### 3. Permanent boosts: Essences bound to the soul
Bosses and rare enemies drop **essences** themed by what they were. A **Librarian** boss drops an **INT essence**; a berserker warlord drops STR; a blind oracle drops INS.
- **Where they come from:** procedural bosses get an archetype from the world graph, and the archetype decides the essence (librarian, scholar or archmage → INT; giant or warlord → STR; and so on). Players learn to read a boss for what it will give.
- **How they're used:** take an essence to a **Soulbinder** at a sanctuary. They merge it **into your character's soul** and the stat rises permanently. Nothing appears on the body. Instead there's a **Soul** screen showing every essence you've bound and where it came from, like a record of your character's history.
- **Limits:** a cap per stat, so maxing every stat is the long-term chase.
- **Death:** bound essences die with the character. Unbound essences can be vaulted and, in multiplayer, traded.
- *Idea for later:* when you die, a faint soul shard holding part of your bound essence stays in your grave. That would make corpse runs and grave looting even more tempting.

## Sanctuaries

| Service | Purpose |
|---|---|
| Vault | Permanent storage; survives permadeath |
| Merchants / trade | NPC trade now, player trade later |
| Forge | Rerolls and re-enchanting |
| Soulbinder | Binds essences into your soul (permanent stat gains) |
| Cartographer | Copies your explored area onto map items; sells charts |
| Rumor board / NPCs | Leads to dungeons, relics, other sanctuaries |
| Reliquary shrine | Collection and unsolved leads |

## Death and graves
- On death, carried and equipped items drop into a **grave** at that spot. Vaulted items are safe.
- Your next character can make a **corpse run** to recover the grave, possibly deep in dangerous territory.
- **Multiplayer:** anyone can loot it. Other players' graves become treasure on the map, and that makes stories.

## Maps as items (proposal)
- **Each character has its own fog of war** that clears as you explore. It dies with the character.
- **A map is an item.** At a cartographer, copy what you've explored onto a map you can carry, vault, sell or trade. Reading a map clears fog for any character.
- **Treasure maps and charts drop as loot.** They're leads (pillar 2), and they're how you find hidden places.
- **The Reliquary stays account-wide** (lore and relic entries). Map knowledge doesn't, so cartography matters.
- **Later in multiplayer:** a skilled scout's map of a far region becomes valuable trade goods.

## Resolved follow-ups
- Stats renamed to STR / AGI / VIT / INT / INS / CHR; mana added (INT pool, INS regen).
- Permanent boosts are essences themed by the boss, bound into the soul at a Soulbinder, with no visible body marks.
