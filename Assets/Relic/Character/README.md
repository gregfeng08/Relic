# Relic.Character — the procedural hero

Brief: `docs/design/14-character.md`. Everything is generated in code: no imported meshes, no clips.

## Layout
| Folder | Assembly | What |
|---|---|---|
| `Logic/` | `Relic.Character.Logic` (pure C#) | `HeroAppearance` (the look as data: body, face, hair style/bangs/colors, outfit pieces and colors, accessories), `AppearanceJson`/`AppearanceRules` (strict JSON like EffectJson, clamping and repair), `AppearancePresets` (Wanderer = Greg's hero, Mira, Rowan, Ash), `EquipmentMapper` (Item → `HeroLook`: weapon kind/stow/parts/colors/glow, armor weight and coat tint, amulet shape and glow, charms), `MaterialPalette` (reference-sheet colors, material words → tints), `KitRoller` (random kits through the real item pipeline, template mode) |
| `Tests/` | `Relic.Character.Tests` | NUnit for the logic (also run by `dotnet test tools/Relic.Tests`) |
| `Runtime/` | `Relic.Character` | `HeroAvatar` (builds and owns the hero from `Appearance` + `Kit`), `HeroRig`, `HeroShape` (final proportions, about 6.2 heads), `MeshKit` (lofts, sheets, tori, splines → one skinned, vertex-painted mesh), part builders (`HeroFace`, `HeroBody`, `HeroHair`, `HeroHairStyles`, `HeroAccessories`, `HeroCoat`, `HeroSkirt`, `HeroScarf`, `HeroArmor`, `HeroGear`, `HeroWeapon`), `HeroAnimator` (procedural poses), `SpringChains` (hair, scarf, coat secondary motion), `HeroPlayerDriver` (PlayerController → animator, debug keys, HUD) |
| `Resources/` | | `Relic/Character/Toon` shader: the World toon lighting (RelicToonCommon) driven per vertex: albedo, outline width, emission (face paint, glow), rim, own shade tint (warm hair/skin), no-cast-shadow flag (clean face and hair). One material, one draw + outline. |
| `Editor/` | `Relic.Character.Editor` | `HeroCapture`: batchmode review renders |
| `EditTests/`, `PlayTests/` | | Unity smoke tests (build budget, poses, origin shift, sandbox spawn) |

## How it plugs in
World can't see Character. `TerrainSandbox.AvatarFactory` is a static hook; `Relic.Game.CharacterBootstrap`
sets it to `HeroPlayerDriver.Create` before the first scene loads. With no factory (or if it throws) the
sandbox falls back to the old `PlayerAvatar`.

## Building the hero
- One `SkinnedMeshRenderer`, one submesh per material (tinted instances of the World toon material, so the
  cel ramp and outline match the world; `Flat` for eyes and glow). Budget: 15k triangles (tests enforce it).
- Rig bones all have identity rest rotations, so parts are authored directly in avatar space (meters, feet
  at 0, facing +Z, character's left = -X) and bind poses are translations.
- Weights come from position callbacks that blend across joints (torso, arms, legs) or along chains.
- Hair strands, scarf tails and the two coat-skirt panels hang from bone chains simulated by
  `SpringChains`: Verlet with a pull toward the rest shape, fixed 90 Hz substeps (frame-rate independent),
  root-relative storage (no fling on floating-origin recenters), capsule colliders (body for hair/scarf,
  legs for the coat), wind from the body's velocity (trailing when running, flaring on dash), buoyancy
  underwater.

## Debug (sandbox)
- **F7** roll and equip a random kit for the current tier (`Difficulty.TierAt` of the world position).
- **F9** cycle armor weight Light → Medium → Heavy.
- HUD line at the bottom lists the equipped items.

## Review renders
```
Unity -batchmode -quit -projectPath . -executeMethod Relic.Character.Editor.HeroCapture.Run [-captureDir DIR]
```
Default output: `<repo>/art-ref/renders/` (gitignored): three kits × front/three-quarter/back, head
close-ups, a motion sheet (idle, run, sprint, dash, jump, climb) and a drawn bow.

## Appearance (v2)
The hero is built from a `HeroAppearance` spec, so a future "upload a picture" flow can go
image → vision model → appearance JSON → this builder (never image → mesh). `AppearanceJson.Deserialize`
rejects unknown fields and enum names and then sanitizes (clamps ranges, repairs colors, fills missing
parts). Hair styles: LowTail (default), LongStraight, Ponytail, TwinTails, Bob; bangs: Swept, Straight,
Parted. Outfit: coat on/off, length and colors (+ trim), scarf on/off and color, shirt, trousers or
skirt over tights, boots, gauntlets/gloves/bare hands, belt; accessories: hair pin, flower, ribbon.
**F11** in the sandbox cycles the built-in presets (F10 is the World props toggle).
