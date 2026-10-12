# 02 — Core Loop

Status: **DRAFT — revision 3, two small follow-ups open** · Last updated 2026-10-12

## Decisions (Greg, 2026-10-12)

| Topic | Decision |
|---|---|
| Death | **Permadeath.** Carried items drop into a **grave** at the death location. In multiplayer, anyone can loot it. A **vault** keeps stored items safe. |
| Gating | **Nothing is locked.** A stock character *can* beat anything (slowly, like Star Cathedral on a stock Duskwing). |
| Power ceiling | **Very high**, so less-skilled players can power through. Stat rolls can go past it, with exponentially rarer rolls. |
| Hero | **One hero.** No party, no classes. |
| Builds | **Defined by gear.** |
| Character growth | **No XP levels, but stats grow by doing**: train to get stronger, study to get wiser. Plus permanent stat-boost consumables (our own take, not RotMG potions). |
| Hubs | **Sanctuaries** (Wynncraft cities): live, trade, vault. |
| Knowledge | Reliquary knowledge carries across characters for now. Open to changing it. **Maps might be items** (see below). |

## Three loops

### Moment to moment (seconds)
**Traverse → spot → engage.**
Run, climb and glide (Genshin). Spot silhouettes, glints, ruins, camps. Action combat with dodges, gear-granted abilities and readable tells. Loot pops with rarity colors. Permadeath gives every fight real tension.

### Session (30–90 minutes)
**Sanctuary → follow a lead → explore → dungeon → boss → loot → get it home (or push on).**

1. **Prepare at a sanctuary.** Vault, trade, read the rumor board, buy or copy maps, pick a lead.
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
No XP levels. Stats grow from what you actually do: fighting up close trains Might, studying lore trains Insight. Growth slows down the higher a stat gets (diminishing returns), so training helps a lot early and becomes a slow polish later.

Draft stat list, inspired by D&D and the 2022 prototype:

| Stat | Grows by | Affects |
|---|---|---|
| Might | Heavy weapons, carrying, climbing | Melee damage, stagger |
| Finesse | Precise hits, perfect dodges | Crit chance, attack speed |
| Vigor | Taking hits and surviving, long journeys | Health, stamina, resisting debuffs |
| Wits | Using abilities, solving puzzles | Ability power, cooldowns |
| Insight | Reading lore, studying relics and inscriptions | **Seeing secrets**: hidden doors, ancient scripts, item lore details |
| Presence | Talking with NPCs, trading | Prices, NPC disposition, persuasion checks |

**Insight is the pillar-1 stat.** A high-Insight character literally sees more of the world: they can read runes others can't and notice cracked walls. Studying makes the world deeper.

Trained stats are lost on permadeath. That's what makes death sting even with a full vault.

### 3. Permanent boosts (our answer to RotMG pots)
**Proposal: Inscriptions.** Bosses and rare enemies drop *essences*. At a sanctuary's scribe, an essence becomes a rune **inscribed onto your character's body**, permanently adding a stat.
- **They show.** Visible marks glow on the model, so your character's skin records their history.
- **They're themed.** A drowned priest's essence makes a wave rune, and the lore for each one can be generated.
- **They have limits.** A cap per stat, so pushing every stat to max is the long-term chase.
- **They die with you.** They're lost on permadeath. Unused essences can be vaulted.

## Sanctuaries

| Service | Purpose |
|---|---|
| Vault | Permanent storage; survives permadeath |
| Merchants / trade | NPC trade now, player trade later |
| Forge | Rerolls and re-enchanting |
| Scribe | Turns essences into Inscriptions; copies maps |
| Rumor board / NPCs | Leads to dungeons, relics, other sanctuaries |
| Reliquary shrine | Collection and unsolved leads |

## Death and graves
- On death, carried and equipped items drop into a **grave** at that spot. Vaulted items are safe.
- Your next character can make a **corpse run** to recover the grave, possibly deep in dangerous territory.
- **Multiplayer:** anyone can loot it. Other players' graves become treasure on the map, and that makes stories.

## Maps as items (proposal)
- **Each character has its own fog of war** that clears as you explore. It dies with the character.
- **A map is an item.** At a scribe, copy what you've explored onto a map you can carry, vault, sell or trade. Reading a map clears fog for any character.
- **Treasure maps and charts drop as loot.** They're leads (pillar 2), and they're how you find hidden places.
- **The Reliquary stays account-wide** (lore and relic entries). Map knowledge doesn't, so cartography matters.
- **Later in multiplayer:** a skilled scout's map of a far region becomes valuable trade goods.

## Open follow-ups (for Greg)
1. **Inscriptions:** good direction for the permanent boosts, or a different flavor?
2. **Stat list:** do the six stats feel right? Rename, merge or add any?
