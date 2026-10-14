# Relic World (stream W): terrain sandbox

## Run it
1. Open `Assets/Relic/World/Scenes/TerrainSandbox.unity` and press Play.
   If the scene or materials are missing, run **Relic > World > Build Terrain Sandbox**.
2. Click the Game view to capture the mouse (Esc releases it).

| Input | Action |
|---|---|
| WASD | Move (camera-relative) |
| Mouse / wheel | Orbit camera / zoom |
| Shift tap | Dash (~4 m in 0.18 s, stamina, **no invulnerability**) |
| Shift hold | Sprint (stamina) |
| Space | Jump · in the air: open/close glider · on a wall: climb-jump (S + Space leaps off) |
| Walk into a steep wall | Climb (stamina) |
| F3 | Debug overlay: seed, world/scene position, origin, chunk, region + family, tier, biome, fps, streaming stats |
| F4 | Debug fly (Space/Ctrl up/down, Shift fast) |
| F5 | Rebuild the world with the next seed |
| F6 | Toggle a slow day cycle |
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
- Props: `PropScatter` (trees, pines, boulders per biome) is written and tested but **not rendered yet**.
- No Burst port: chunks are built by the pure Logic code on worker threads (~1–6 ms per chunk).
- Shadow distance is the URP asset's 50 m (lead-owned); long terrain shadows stop there.
- Swimming has no stamina drain and no drowning yet. No rivers. Lakes use a sloping water table, so a lake that
  straddles a region border can tilt slightly.
- Real frame times are only measured on Greg's machine (batchmode has no GPU load).
