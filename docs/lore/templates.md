# Templates: factions and events

The world graph doesn't invent groups or history from nothing. It instantiates these archetypes and templates, filling their **slots** with entities it has already generated (places, figures, factions, artifacts). Data: `Assets/Relic/Lore/Data/factions.json` and `events.json`. The loader rejects any template that uses an undeclared slot.

**Slot conventions** (shared by names, summaries and lore lines):
- `{place}`, `{founder}`, `{figure}`, `{rival}`, `{rescuer}`: generated names from the culture's phonology.
- `{faction}`: a faction name that **carries its own article** ("the Bramwick Map House", "Tibdry's Revelers"). So templates never write "the {faction}".
- `{culture}`: the culture's adjective ("Wendle", "Solannish").
- `{artifact}`: an artifact name, with "the" added when needed.
- `{word}`: a plain word, either from the slot's suggestion list or the culture's vocabulary (crafts, item forms, motif words).
- `{number}`: a small round number (years, families).

---

## Faction archetypes

Each archetype has a **kind**, a **tone**, a pick **weight**, 2–3 name patterns, goals, symbols, and three kinds of relations:
- **allies:** they work together.
- **rivals:** *friendly* rivalry, meaning races, contests, bets and pie judgings. Never bloodshed.
- **opposes:** real conflict. This is reserved almost entirely for the three somber archetypes.

| Archetype | Kind | Name patterns (examples) | Goals (one of) | Friendly rivals |
|---|---|---|---|---|
| cartographers | Guild | *The {place} Map House*, *{founder}'s Cartographers* | chart the next valley; add a region to the Unfinished Map | caravanners |
| archivists | Order | *The Keepers of the Open Ledger at {place}* | copy every wayfarer's journal; return a relic's story home | — |
| hearthwardens | Order | *The Hearthwardens of {place}*, *{founder}'s Watch* | keep the gates; relight a lost hearthstone | wayfarers |
| wayfarers | Company | *{founder}'s Far-Walkers*, *The {word} Lantern Company* | reach farther than last year; find the edge of the Wide | hearthwardens, shipwrights |
| caravanners | Company | *{founder} and Daughters, Carriers* | open a route over the pass; beat the ships to market | shipwrights, cartographers |
| smiths | Guild | *The Hammers of {place}* | rediscover a Lamplit alloy; win the spring contest | bellfounders |
| bellfounders | Guild | *The Bellfounders of {place}* | find the Morning Bell; cast a bell heard across three valleys | smiths |
| glyphwrights | Circle | *The Circle of the {word} Word* | discover a lost glyph; teach anyone who asks | lamplighters |
| soulbinders | Circle | *{founder}'s Quiet House* | record every essence bound; learn what an essence remembers | — |
| festival-keepers | Company | *The {word} Fair Committee of {place}* | throw a better fair than the next town over | brewers |
| brewers | Household | *House {founder}, Brewers*, *The {place} Cider House* | guard a recipe kept through the Hush; win the autumn judging | festival-keepers, growers |
| growers | Household | *The {word} Orchard Folk* | grow a fruit lost in the Hush; win the biggest-marrow ribbon | brewers |
| shipwrights | Guild | *{founder}'s Yard* | build a ship fit to follow Captain Corro | caravanners, wayfarers |
| spirit-keepers | Order | *The Keepers of the {word} Shrine* | keep the patron spirit content; learn its favorite song | — |
| lamplighters | Order | *The Lamplighters of the {word} Road* | relight one lamp-tower every year | glyphwrights |
| *imperative-heirs* | Circle | *The Heirs of the Imperative*, *The {word} Choir* | gather the Iron Sentence; wake the old constructs | *(opposes archivists, glyphwrights, hearthwardens)* |
| *wreckers* | Company | *{founder}'s Salvage Crew* | strip a ruin before the Keepers arrive; sell charts that lead nowhere | *(opposes archivists, cartographers)* |
| *tollkeepers* | Household | *The Tollkeepers of {place}* | own every road out of the valley; lock relics away where nobody can hear them | *(opposes caravanners, wayfarers)* |

*Italic rows are the somber archetypes.* They carry 3 of the 32 total weight points (about 9%), so most regions have no villains at all, and the ones that do have small, specific ones. Each culture lists the archetypes typical of it (see `cultures.md`). The world graph picks from that list first.

**Status** (from the world graph, 10): a faction can be *thriving*, *fallen* (its hall is a ruin, which makes a good dungeon) or *hidden*. Bright factions are usually thriving. A fallen bright faction is a great source of relics and leads: "the Map House at Tibdale burned in the Hush; its last chart is still missing."

---

## Event templates

Each template has a **kind**, a **tone**, a **weight**, the **eras** it can happen in, declared **slots**, name patterns, a one-sentence **summary**, and what it **leaves** in the world. The traces it leaves are the hooks other systems place: `Place, Ruin, Landmark, Artifact, Faction, Festival, Road, Rumor, Map`.

| Template | Kind | Eras | Summary | Leaves |
|---|---|---|---|---|
| founding-hearth | Founding | Lamplit, Rekindling | *{founder} of {faction} relit a cold hearthstone and founded {place} around it.* | Place, Faction, Festival |
| founding-crossroads | Founding | all but Quarrel | *Traders of {faction} met at a crossroads to swap {word}, and {place} grew up around their stalls.* | Place, Road |
| festival-first | Festival | all but Quarrel | *{founder} threw a feast at {place} to celebrate the {word}, and it has been held every year since.* | Festival, Rumor |
| festival-revived | Festival | Rekindling | *After {number} quiet years, {faction} brought back the old lantern festival of {place}.* | Festival |
| discovery-ruin | Discovery | Hush, Rekindling | *{figure} found {artifact} in the ruins at {place}, and the Reliquary is still arguing about it.* | Artifact, Ruin, Rumor |
| discovery-glyph | Discovery | Lamplit, Rekindling | *{figure} of {faction} copied a forgotten glyph from a wall at {place} and taught it to anyone who asked.* | Artifact, Rumor, Ruin |
| invention-everyday | Invention | all but Quarrel | *{figure} of {place} invented a better {word}, and three neighboring towns insist they thought of it first.* | Artifact, Rumor |
| invention-wonder | Invention | Lamplit | *{figure} of {faction} built {artifact}, one of the wonders of the Long Roads.* | Artifact, Landmark |
| rivalry-race | FriendlyRivalry | all but Quarrel | *{faction} and {rival} have raced to {place} every spring for {number} years, and the loser buys the cider.* | Festival, Road |
| rivalry-craft | FriendlyRivalry | all but Quarrel | *{figure} and {rival} spent a lifetime trying to make the finer {word}…* | Artifact, Rumor |
| rescue-flood | Rescue | all but Quarrel | *When the river rose, {faction} carried {number} families out of {place}…* | Landmark, Festival |
| rescue-wayfarer | Rescue | Rekindling | *{rescuer} walked a week through the wild past {place} to bring {figure} home.* | Road, Rumor |
| migration | Migration | Hush, Rekindling | *A band of {culture} families crossed into {region} and settled at {place}, bringing their recipes with them.* | Place, Road, Festival |
| lost-expedition-letters | LostExpedition | Rekindling | *{faction} set out for {place} {number} years ago, and their letters still arrive, each from farther away.* | Map, Rumor |
| *lost-expedition-silent* | LostExpedition | all but Quarrel | *{faction} expedition to {place} went quiet; its last letter mentioned {artifact}.* | Map, Rumor, Ruin |
| *disaster* | Disaster | Lamplit, Quarrel, Hush | *A {word} emptied {place} during the long quiet, and the wild moved in after.* | Ruin, Rumor |
| *war-siege* | War | Quarrel | *Imperative constructs besieged {place}, and {figure} of {faction} held the gate until the Silent Field ended the war.* | Ruin, Artifact, Landmark |

*Italic rows are somber, at weight 1 each* (about 9% of all event weight). Wars happen only in the Quarrel. That is the one dark chapter, and it's long over.

### How the world graph uses them
1. For each region, draw **2–4 events** by weight, restricted to templates whose eras fit the region's history.
2. Fill the slots from entities that already exist nearby, or create them (a founder figure, an artifact) and link them with the relations in 10-world-graph (`founded`, `forged`, `locatedIn`, `mentions`…).
3. Place what the event **leaves**: a Ruin becomes a dungeon candidate, an Artifact becomes a relic with a lead, a Festival becomes a sanctuary custom, and a Rumor goes on the board.
4. The summary sentence is the **ground truth** handed to the LLM. The LLM may retell it in its own words, but it can't change who, where or what.

### Moods vs. events
Event weights decide *what happened*. **Moods** (`moods.json`) decide *how it feels now*, and they follow the 70/30 rule through `MoodPicker`. A ruin left by a bright event (an old festival hall) can still carry a somber mood today (loss), and a Quarrel battlefield can be a meadow full of wildflowers where children play at being Concord scribes (legacy). This is how the world stays mostly bright while still having weight.
