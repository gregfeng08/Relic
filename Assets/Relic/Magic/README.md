# Relic.Magic: spellcraft (stream M)

Drawn magic. You draw a circle of glyphs, and it compiles into the shared
effect grammar (`docs/design/05-effect-grammar.md`). Concept and brief:
`docs/design/04-spellcraft.md`.

```
strokes ─► CircleParser ─► SpellCircle ─► SpellCompiler ─► EffectProgram (+ name, mana, cooldown, notes)
             │ recognizer: PointCloudRecognizer ($Q-style) against Resources/RelicGlyphs.json
recipe ("water | split seek | line") ─► SpellCircle.FromRecipe ─┘
recipe ─► RecipeDrawer ─► strokes        (pre-written pages; round-trips)
```

## Run the Writing Desk

1. Open `Assets/Relic/Magic/Scenes/WritingDesk.unity` (or use the menu **Relic → Magic → Open Writing Desk**) and press Play.
2. Draw on the parchment with the left mouse button:
   - one **large closed circle** first (lifting the pen partway is fine, and so is a small gap of up to 20°);
   - a **core sigil** in the middle;
   - **modifiers** in the inner ring;
   - **shapes** on the outer ring. They happen in order, clockwise from the top.
   - Draw each glyph's strokes one after another (the parser groups consecutive strokes).
3. Recognized glyphs are recolored: amber for the core, teal for inner glyphs, red for the ring, grey for marks it couldn't read. Each glyph is labeled with its precision. Faint dashed rings show the center and inner bands.
4. The right panels show the spell's name, description, mana, cooldown, misfire chance, ink quality, power, plain-language notes, and the effect-program JSON. The arena plays the spell against three drifting dummies, and a hit flashes the status color.
5. **Palette** mode places glyphs by clicking (the fallback and controller path). In **Draw** mode, palette clicks build a recipe that **Trace** shows as a faint guide to draw over. You can also type a recipe, for example `water | split seek | line+`.
6. **Pages** load the pre-written spells (`Logic/Recipes/PrewrittenSpells.cs`) as ink. Clear the page and trace them to practice.

Keys: Ctrl+Z undo, Space cast, Delete clear. **Remove last** and the S/M/L buttons edit the palette recipe. S/M/L sets the size of the next placed glyph.

To rebuild the scene: **Relic → Magic → Rebuild Writing Desk Scene**, or run in batchmode:
`Unity.exe -batchmode -quit -projectPath . -executeMethod Relic.Magic.DeskEditor.WritingDeskSceneBuilder.Build`.
To save PNGs of the glyph sheet, every page and the arena: **Relic → Magic → Save Desk Snapshots** (`-executeMethod Relic.Magic.DeskEditor.WritingDeskSnapshots.Capture -snapshotDir <dir>`).

## How the circle is read

| Layer | Where | Meaning |
|---|---|---|
| Core (exactly one) | center, under 0.28 R | Substance → payload: Water soaks, Flame burns, Stone staggers (physical damage), Wind knocks back, Light dazes, Growth roots |
| Inner | 0.28–0.62 R, or inside a drawn guide circle | More, Less, Far, Lasting, Split (the first adds bolts, later ones split on impact), Seek, Heavy, Swift |
| Ring | outside that | Line, Cone, Ring, Orbit each add a shape. Delay holds back every shape after it. Return applies to the shape before it. Wall turns the Line next to it into a standing wall |

- **Size = emphasis.** A glyph drawn at under 0.72× or over 1.35× its expected size reads as Small (×0.75) or Large (×1.3). Expected size is 0.40 R for the core and 0.25 R for the others.
- **Precision = efficiency.** At a weighted ink quality of 75% or more (the core counts double), there's no penalty. Below that, up to +60% mana, +40% cooldown and a 35% misfire chance. Palette glyphs count as clean ink (90%).
- **Price.** Power P is `PowerBudget.SumActions` (pp at a 4 s baseline). Cooldown is `1.3·√P` and mana is `2.65·P^1.2`, tuned so Tide Seekers costs 3 s and 20 mana. A circle holds at most 20 pp. `EffectValidator` checks the result against the caster's budget (default Rare, tier 0).
- **No shape** on the ring means a small burst where you aim.
- All numbers are in `Logic/Compile/SpellTuning.cs`.

## Add a glyph (no code)

Edit `Resources/RelicGlyphs.json`:

```json
{
  "id": "tidepool", "name": "Tidepool", "role": "Inner", "effect": "Lasting",
  "hint": "How to draw it, in words (shown on hover).",
  "words": ["Pooling"],
  "strokes": [
    [[-1, -1], [1, -1], [1, 1], [-1, 1], [-1, -1]],
    [{"arc": [0, 0, 0.5, 0.5, 90, 450]}]
  ]
}
```

- `role` is `Core`, `Inner` or `Ring`. An Inner or Ring glyph reuses an existing `effect` (see `GlyphEffect`). A Core needs a `core` block: `{"damage": 8, "damageKind": "Magical", "status": "Soaked", "statusDuration": 4, "knockback": 0}`.
- Strokes sit in roughly a [-1, 1] box with Y up, in the order and direction a person would draw them. A stroke is a list of `[x, y]` points and `{"arc": [cx, cy, rx, ry, fromDeg, toDeg]}` pieces (degrees counter-clockwise from +X; `to < from` runs clockwise).
- Orientation matters: the recognizer forgives about ±10° of tilt, not rotation.
- Then run `dotnet test tools/Relic.Tests`. `SyntheticAccuracyIsAtLeast95Percent` prints a confusion list. If your glyph is confused with another, make it more distinct. Point clouds tell glyphs apart by where the ink is, not by stroke order.
- A brand-new effect (not one of the existing `GlyphEffect` values) needs code in `SpellCompiler`.

## Layout

- `Logic/` is pure C# (`Relic.Magic.Logic`, no UnityEngine): geometry, recognizer, glyph registry, circle parser, compiler, recipes, namer (`ISpellNamer` plus a deterministic `StubSpellNamer`; the LLM namer comes later and adds flavor only).
- `Runtime/` (`Relic.Magic`): `WritingDeskBootstrap` (the only component in the scene), `WritingDesk` (IMGUI), `InkCanvas`, `SpellPreview` (debug arena), `GlyphLibrary` (loads the JSON from Resources).
- `Tests/` holds pure NUnit tests, run by both dotnet and Unity. `EditorTests/` holds Unity-only smoke tests for the runtime. `Editor/` holds the scene builder and snapshots.

## Tests

- `dotnet test tools/Relic.Tests` (fast, from the repo root).
- Unity EditMode: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults r.xml -logFile u.log`.
