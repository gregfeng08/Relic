# The lore constitution

Relic's story is **procedural and event-driven** (design docs 16 and 20). Nothing here is a fixed place, person or date. This page holds the few **rules** that every generated world, and every line the Chronicler or an LLM writes, must agree with. Greg's decision of 2026-11-18 wiped the old hand-written story (the eras, the legends, the named villain and his war, the festivals, the fixed towns and the seven fixed cultures). What survives is below.

## Tone in one line
Bright medieval fantasy: wonder, warmth, craft, community, adventure and gentle humor, with darkness as an accent. About **70% of generated moods are bright and 30% somber**. Details in [`style-guide.md`](style-guide.md).

## Mechanical truths
One line each. They explain why the systems work (pillar 7: *could a character explain why this works?*).

1. **Resonance.** The world pays attention, and things that are used and loved for a long time gather **resonance**. That is the root of every truth below.
2. **Relics.** A relic is an object loved and used for so long that the world began to answer it. Its history *is* its enchantment, and it hums louder near the places it remembers, which is why relic lore points somewhere.
3. **Essences.** A creature that did one thing for a very long time condenses that habit into an essence when it is beaten. A Soulbinder writes it into your soul (no visible marks), up to each stat's cap.
4. **Magic is written.** Spells are **written incantations** (RelicScript, doc 22) read by the world's impartial reader, **the Magister**: literal, patient, never reads the same text two ways, charges the caster's mana for what it reads, and refuses text that tries to *command* the world rather than ask it.
5. **Graves.** Things are only things: what you carry stays where you fall, in a waycairn the next wayfarer may carry onward. What you read into a Reliquary survives you.
6. **Sanctuaries.** Every sanctuary grows around a hearthstone, the most listened-to place for miles. Wild things keep clear of it, sometimes helped by a patron spirit or a garrison.
7. **Wilding.** Places nobody has listened to for a long time grow wild, which is why danger rises with distance from the first hearth.
8. **The first hearth.** Every journey starts at a generated origin sanctuary built around the world's *first hearth*. Its name, culture and founders come from the world seed.

## What each world generates
Per world seed, from the data in `Assets/Relic/Lore/Data` (see [`README.md`](README.md)):

- **5–8 cultures** from the culture templates ([`culture-templates.md`](culture-templates.md)), each speaking one **phonology family**.
- **A short prehistory:** 3–4 eras (always a founding era first and a waking era last), one founder per culture, a handful of events from the event templates ([`templates.md`](templates.md)), and the figures and relic-worthy artifacts those events leave.
- After world creation, the **Chronicler** (doc 20) writes history as players act. Like the wayfarer, the world starts mostly unwritten.

## Changing the constitution
These rules change only with Greg's approval. Templates (cultures, eras, events, factions, motifs, moods) can grow freely as long as the tests stay green and the tone stays bright.
