# Relic World (stream W): terrain sandbox

## Run it
1. Open `Assets/Relic/World/Scenes/TerrainSandbox.unity` and press Play.
   If the scene or materials are missing, run **Relic > World > Build Terrain Sandbox**.
2. Click the Game view to capture the mouse (Esc releases it).

| Input | Action |
|---|---|
| WASD | Move (camera-relative) |
| Mouse / wheel | Orbit camera / zoom |
| Shift press | Dash (~4 m, eases out over 0.24 s, stamina, **no invulnerability**) |
| Keep holding Shift | Sprint after the dash (Genshin-style, stamina) |
| Space | Jump · on a wall: climb-jump (S + Space leaps off) · underwater: swim up. No glider. |
| Ctrl or C (in deep water) | Dive and swim down. Underwater, W follows the camera (look down to go down). Breath bar shows under water; running out forces you up and drains stamina |
| Push into a real wall (≥65°, taller than you) | Climb (stamina). Grabs after 0.2 s on foot, instantly from a jump |
| F3 | Debug overlay: seed, world/scene position, origin, chunk, region + family, tier, biome, fps, streaming stats |
| F4 | Debug fly (Space/Ctrl up/down, Shift fast) |
| F5 | Rebuild the world with the next seed |
| F6 | Toggle a slow day cycle |
| F10 | Toggle props (trees, rocks, grass) for perf comparisons |
| F7 / F9 | Hero: random kit / armor weight (Character stream) |
| F8 | Benchmark: auto-sprint 3 km straight along the camera direction, then log a pass/fail perf summary |

Perf lines start with `[Relic.Perf]` (Console / Editor.log): a line every 10 s while moving, a
summary when the benchmark finishes and when play stops. PASS = average ≤ 16.6 ms and no frame over 50 ms.

## Knobs
- **TerrainSandbox** component (scene): seed, view distance, LOD distances, collider distance, upload budget (ms/frame).
- **Settings/PlayerConfig.asset**: every movement, stamina and camera constant.
- **SkyController** (on the runtime "Sun" object): sun angle/colour, sky colours, toon ambient and shade colour, fog.
- **Materials/ToonTerrain.mat**: rock slope thresholds, strata, ground variation, rim. **ToonCharacter.mat**: outline width/colour.
- Generation tuning lives in `Logic/Regions/RegionCatalog.cs` (per-family parameters) and
  `Logic/Terrain/TerrainGenerator.cs` (layer wavelengths/amplitudes). Changing either changes the world:
  bump `TerrainGenerator.Version` and update the golden hash in `Tests/TerrainTests.cs`.

## Layout
- `Logic/` (pure C#, tested by `dotnet test tools/Relic.Tests`): `SimplexNoise2`, `IRegionField` +
  `WarpedVoronoiRegionField` (domain-warped, size-weighted jittered Voronoi, nearest-3 blend),
  `RegionCatalog`, `TerrainGenerator` (`HeightAt`, `Sample`), `BiomeTable`, `PropScatter`, `ChunkMath`/`LodPlan`/`FloatingOriginMath`.
- `Runtime/`: `ChunkStreamer` (worker threads, upload budget, job-baked colliders), `ChunkMeshBuilder`,
  `FloatingOrigin`, `SkyController`, `PlayerController`, `OrbitCamera`, `PlayerAvatar`, `SandboxHud`, `PerfMonitor`, `TerrainSandbox`.
- `Shaders/`: toon terrain, toon character (inverted-hull outline), water, gradient sky (hand-written URP HLSL).
- `Editor/`: scene builder; `SandboxCapture` renders screenshots from batchmode
  (`-executeMethod Relic.World.Editor.SandboxCapture.Run -captureDir <dir>`).
- `PlayTests/`: play-mode smoke tests (streaming, spawn, 4.5 km auto-sprint with origin shift, 100 km jump).

## Determinism
Everything is a pure function of the seed and coordinates, through `Relic.Core.Random` hashing.
The height path uses only doubles, integers and `Math.Sqrt`/`Math.Floor` (no `sin`/`cos`/`pow`, no float
expressions): Unity's Mono may evaluate float expressions at higher precision than .NET, which broke the
golden hash once. The same golden hash is checked in dotnet and Unity EditMode.

## Known issues / not done
- No Burst port: chunks are built by the pure Logic code on worker threads (~1–6 ms per chunk).
- Shadow distance is the URP asset's 50 m (lead-owned); long terrain shadows stop there.
- Swimming on the surface has no stamina drain yet; drowning only drains stamina (HP hook: `PlayerController.Suffocating`, TODO(HP)). No rivers. Lakes use a sloping water table, so a lake that
  straddles a region border can tilt slightly.
- Real frame times are only measured on Greg's machine (batchmode has no GPU load).

## Feel and looks (W2)
- **Movement:** speed, facing, camera follow, camera collision and ground clamp are all critically
  damped (SmoothDamp); ground snapping keeps you grounded over crests and small drops; step-ups and
  snaps go into `PlayerController.VisualYOffset` and landings into `LandingSquash` (avatars read both).
  Every constant is in `PlayerConfig` (Accel/Stop/Reverse time, TurnSmoothTime, camera follow times).
  `PlayTests/MovementSmoothnessTests` checks a scripted run for jitter, snaps and micro-bouncing.
- **Terrain:** soft ridges, gentler terraces, each grid cell split along its flatter diagonal,
  anti-aliased cel bands and filtered (re-sharpened) shadows. `-captureSet ridges` shows peaks,
  mesas, cliffs and region borders at forced LODs 0/1/2.
- **Snow caps:** a climate-shifted snow line that the tallest peaks of every region reach
  (`TerrainGenerator.SnowLineFor`), a noisy transition band, and snow only on slopes it can hold.
- **Props:** `PropScatter` (Logic) places 14 kinds by biome and region family; `Runtime/Props`
  streams them on worker threads, draws them with `Graphics.RenderMeshInstanced` (procedural
  low-poly meshes, `ToonProp`/`ToonGrass` shaders with wind sway and shrink-fade), and adds
  trunk/boulder colliders near the player on layer 1 (the camera ignores them).
- Captures: `-captureSet default|ridges|snow|dive|props|all`.
