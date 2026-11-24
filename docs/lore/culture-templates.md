# Culture templates and phonology families

Cultures are no longer written by hand. Each world grows **5–8 cultures** from these templates (`CultureGenerator`, data in `Assets/Relic/Lore/Data/culture-templates.json` and `phonology.json`). See [`samples.md`](samples.md) for one generated world.

## How a culture is generated
For each culture, `CultureGenerator.Generate(rules, worldSeed)`:

1. Picks a **template** (by weight, never the same one twice in a world).
2. Picks one of the template's **phonology families** and keeps a seeded ~70% of its syllables, so two cultures of the same family sound related but not identical.
3. Names the people from a short root in those sounds ("the Corley") and makes the **adjective** with one of the family's endings ("Corleyish").
4. Takes the template's **region families** (its home first, plus one neighbor) and climates, then a seeded selection of crafts, materials (**always at least one metal**), foods, festivals, values, factions, motifs, item forms (**always at least two relic forms**, each with its form tag), naming patterns, maker's marks and **two mood lines**.
5. After all cultures exist, makes sure **every region family** is claimed by someone, and gives neighbors **relations** (trade, friendly rivalry, kinship, and now and then a polite wariness).

The result has the same `CultureDef` shape the item grammar has always used, plus `Template`, `PhonologyFamily`, `Mood` and `FormTags`.

## The templates

| Template | Home (also) | Phonologies | In one line |
|---|---|---|---|
| `meadow-growers` | Meadow (Forest, Coast) | hearth, harbor | Farmers, millers and bakers who think most problems improve with a pie. |
| `grove-tenders` | Forest (Marsh, Meadow) | grove, reed | Woodwrights and grove-tenders who share their villages with spirits. |
| `terrace-builders` | Highlands (Frost, Badlands) | terrace, saga | Terrace-builders, tea growers, bellfounders and kite-flyers. |
| `harbor-folk` | Coast (Meadow, Marsh) | harbor, grove | Sunny harbor folk who keep the lighthouses lit. |
| `mesa-potters` | Badlands (Highlands, Coast) | mesa, terrace | Potters, glassblowers and star-readers who run the caravans. |
| `frost-herders` | Frost (Coast, Highlands) | saga, hearth | Northern herders who measure winters in tales. |
| `fen-ferryfolk` | Marsh (Coast, Forest) | reed, hearth | Stilt-village ferry-keepers with frog choirs and terrible puns. |
| `forge-folk` | Highlands (Badlands, Meadow) | saga, mesa, terrace | Smiths, miners and clockmakers who can fix anything. |
| `orchard-keepers` | Meadow (Coast, Forest) | harbor, grove, hearth | Orchard-keepers and cider-makers who plant for grandchildren. |
| `wind-riders` | Badlands (Meadow, Frost) | mesa, saga | Plains riders and kite-flyers who read weather from a cloud. |

Each template lists pools larger than any one culture uses: identities, architecture, crafts, non-metal materials, **metals**, foods, generic festivals, values, prides, faction archetypes, motifs, item forms, naming (patterns, epithets, adjectives, maker's marks), mood lines (slots `{people}` and `{adjective}`) and founder epithets. **Maker's marks are written to work on any form** ("stamped where the hand rests"), and every material is one Items describes (`Materials.cs`).

## Phonology families

| Family | Sounds like | Example syllables |
|---|---|---|
| `hearth` | short, cozy syllables ending in -wick, -ley, -kin; often a family name | Bram, Pell, Tib + -wick, -ley, -kin; places -fold, -ford, -mere |
| `grove` | flowing, vowel-rich three-syllable names; no family names | A, Se, Li + ru, la, vi + -vi, -oru, -ani; places Grove, Glade |
| `terrace` | crisp two- and three-syllable names; a one-root family name | Tes, Kar, Yen + -an, -ek, -ari; places Steps, Terrace, Pass |
| `harbor` | sunny, sing-song names ending in -o, -etta, -ine | Cor, Lis, Vel + -ello, -etta, -ine; places Harbor, Light, Cove |
| `mesa` | warm, open names with z, sh and q sounds | Az, Zan, Qa + -esh, -oum, -ad; places Mesa, Kiln, Wells |
| `saga` | sturdy northern names ending in -ild, -rik, -holm | Bryn, Tor, Ulf + -ild, -rik, -holm; places Hall, Firth, Ridge |
| `reed` | bouncy, burbling names that are hard to say crossly | Pib, Wob, Nim + -et, -kin, -ling; places Stilts, Ferry, Landing |

All names come from `NameGenerator` and pass its plausibility rules (letters only, length bounds, no tripled letters or stutters, no banned fragments). Tests check that each family's names vary and that families rarely produce the same name.

## Item forms and their tags
`forms.json` tags every form a template can make with its Items **base** (`Sword`, `Greatsword`, `Bow`, `Staff`, `Tome`, `LightArmor`, `MediumArmor`, `HeavyArmor`, `Trinket`, `Relic`) and, for relics, its **kit** (`Forms.RelicForms`: `shield`, `bell`, `lantern`, `mask`…). That is how Items maps a reed shield or a pie-tin buckler onto the shield kit. A form may carry two tags (a charm is both a trinket and a relic).

## Adding a template
Add an entry to `culture-templates.json` (copy one), use existing phonology ids, faction and motif ids, tagged forms and Items materials, give it at least one metal and two relic forms, then run `dotnet test tools/Relic.Tests`. The loader names every problem it finds.
