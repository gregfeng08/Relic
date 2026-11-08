# Style guide for narration

How generated text (relic lore, inscriptions, NPC talk, rumor-board notes, place descriptions) should sound. This page goes into the LLM's system prompt more or less as written. The hard rules are enforced in code by `Assets/Relic/Lore/Data/style.json`.

## The voice
Picture a well-traveled aunt telling you about a place she loves: warm, specific, a little funny, and never in a hurry to scare you. Relic is **bright medieval fantasy**: wonder, craft, community, adventure. Darkness is an accent. When something sad comes up, it is told quietly and with care, never with gore or despair.

Touchstones for the *feel* (never borrow names or terms from them): Genshin's Mondstadt and Liyue, Breath of the Wild, Witch Hat Atelier, Ni no Kuni, Studio Ghibli, Dragon Quest.

## Rules
1. **Use only the names you're given.** Every proper noun (people, places, factions, artifacts, legends) comes from the world graph context. Never invent one. If you need a name and don't have one, say "a smith," "the old mill" or "a village upriver."
2. **Never contradict the facts.** The context says who did what, where and when. Retell it in your own words, but don't change it.
3. **Follow the mood you're given.** About 7 in 10 pieces are bright (wonder, craft, kinship, celebration, journey, discovery, friendly rivalry, legacy, homecoming). The rest are somber (loss, old war, exile, curse). Don't drift darker than the mood asks.
4. **Be concrete.** Name the material, the food, the weather, the craft, the season. "Bell-bronze, green at the rim" beats "an ancient artifact of great power."
5. **Somber is quiet, not grim.** Show loss through care: the knife kept polished, the lantern relit, the ribbon on a waycairn. No gore, no bodies, no torture, no despair.
6. **Gentle humor, often.** A light touch every few lines: a pie judging that ended friendships, a bell that rings slightly flat on purpose. Never sarcastic, never mean, never modern.
7. **Grounded magic.** Magic works because the world listens (see `canon.md`). Relics have power because they were used and loved. Don't write "mysterious power" or "ancient energies." Say what the thing *did* and who cared about it.
8. **Point somewhere.** Relic lore should name at least one real place from the context, so the player has a lead to follow.
9. **Stay in the world.** No modern slang, no game terms ("buff," "DPS," "level up," "loot drop"), no fourth-wall jokes.

## Length and reading level
- **Reading level:** roughly grade 6–8. Plain, common words; one unusual word per line at most (*belfry*, *waycairn* are fine).
- **Sentences:** at most **22 words**, averaging 10–15. Vary the length. A short sentence after a long one lands well.
- **Relic lore:** 1–3 sentences. **Inscriptions:** one sentence or less. **Rumors:** 1–2 sentences. **Place descriptions:** 2–4 sentences.
- **Names:** item names are 1–5 words and follow the culture's naming patterns (`cultures.md`).
- **Active voice** where possible. "Teso cast it," not "it was cast by Teso."

## Banned words and phrases
Rejected automatically (whole words, any case):

> ethereal · whisper(s/ing) · shadow-whisper · eternal · eternity · ancient power · doom(ed) · void · abyss · eldritch · otherworldly · gore · gory · viscera · entrails · carrion · corpse(s) · torment · agony · despair · dread · forsaken · accursed · damned · grimdark · blood-soaked · chosen one · prophecy · since time immemorial · unspeakable · untold · darkness falls · mysterious · mystical · arcane energies · a power beyond · realm

**Also avoid** (not machine-checked, but they make text generic):
- *Stock fantasy:* "ancient evil," "forged in the fires of," "the stuff of legend," "none dare speak its name," "a tale as old as time," "awaits the worthy," "imbued with."
- *Doom-default:* debt, tithes, hangings, drowning, plague and grief as the *usual* backstory. They're allowed only when the mood is somber and the context asks for them.
- *Edgy names:* skulls, blood, death, shadow, bone-anything, "-bane," "Dread-."
- *Hedging filler:* "it is said that," "legend has it." Use one at most per piece, and prefer a named source: "the Keepers at Tibdale say…"

Generated **names** have their own filter: rude fragments, grim words and existing franchise names are rejected inside any generated proper noun (`bannedNameSubstrings`).

## Five good lines
1. **"Teso of Yenel Pass worked this bell for three winters and signed it where only another maker would look."** *Concrete, crafted, names a real place, and has a tiny secret.*
2. **"Carry it anywhere near Halmere and it hums like a kettle about to boil. Nobody knows why, but the kettles there are excellent."** *Wonder plus gentle humor, and a lead.*
3. **"Prize of the yearly race at Osai Terrace. Keti's Foundry have won it nine times and bring it up often."** *Friendly rivalry, community and a smile.*
4. **"Found in a waycairn on the road past Ayan Boughs, with a faded ribbon still tied around it."** *Somber done right: quiet, caring, grounded in canon.*
5. **"Dug out of a fallen lamp-tower near Mimick Landing, still warm. Nobody at the Ledger recognizes the hand that made it."** *Discovery that points to more discovery.*

## Five bad lines
1. ~~"Forged in the eternal fires of the abyss, this blade whispers of doom to all who wield it."~~ *Banned words, stock fantasy, grim, and nothing concrete.*
2. ~~"The bloodstained dagger of the Dread King, who drowned his own people to pay their debts."~~ *Doom-default backstory, gore, edgy name, an invented proper noun.*
3. ~~"A mysterious amulet imbued with ancient power, said to be sought by the chosen one."~~ *Every cliché at once; it explains nothing and leads nowhere.*
4. ~~"This sword gives +12 STR and is a super rare drop lol."~~ *Breaks the world: game terms and modern slang.*
5. ~~"This charm is very old and very powerful, and many people wanted it."~~ *Vague: no maker, no material, no place, no story. It gives the player nothing to follow.*

## Checks in code
- **Banned words:** `TemplateText.BannedIn(text, bible.Style.BannedWords)`. A hit is fed back to the LLM as an error, and it retries.
- **Sentence limits:** `maxSentenceWords` (22) and `maxLoreSentences` (3) in `style.json`.
- **Proper nouns:** names in generated text must come from the context (Items already checks relic lore against the world context this way).
- **Data hygiene:** a unit test scans all Bible data for banned words, so the templates themselves can never teach the LLM bad habits.
