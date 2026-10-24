# 07 — World Generation: Terrain & Regions

Status: **v1 — brief for stream W** · Last updated 2026-10-13
Scope: terrain, regions, blending, biomes, chunk streaming, look, and a character to explore with. Out of scope for now: dungeons, sanctuaries, the world graph, POIs (separate specs).

## Goals (milestone M1, "walk around a toon-shaded world")
1. An **endless** world from one seed. Walk any direction; it keeps going with no seams.
2. **Regions** that feel distinct (coastal, highland, forest, badlands, …) and **blend** smoothly at their borders.
3. **Genshin-like readability:** toon/cel shading, clear silhouettes, slope-based rock on cliffs, vistas.
4. A **third-person character**: run, dash (Shift; keep holding to sprint), jump and climb, with a Genshin-style camera. No glider (Greg, 2026-10-23).
5. Smooth: 60 fps on the dev laptop while streaming chunks.

## World model
- **Units:** meters. Y is up. The origin (0, 0) is the starting sanctuary's location.
- **Seed:** a `ulong worldSeed`. Everything below is a pure function of the seed and coordinates (see 06 on determinism).
- **Regions** are Voronoi cells over a **jittered grid**, spacing **1024 m**. A cell's center is `cell × 1024 + jitter(seed, cell) × 0.7 × 1024`. Each region gets parameters from `Pcg32(Hash.Combine(seed, Hash.Coord(cx, cz), Hash.Of("region")))`:
  - `biomeFamily` (enum), `heightScale`, `ruggedness`, `erosion`, `waterLevelBias`, `temperatureBias`, `humidityBias`, `palette` (splat colors)
- **Difficulty tier:** use `Relic.Core.Stats.Difficulty.TierAt(x, z)` (1200 m per tier, capped at 10). Never reimplement it; every stream must agree on the number.
- **Blending:** for any point, find the nearest 3 region centers. Weights come from the distance differences with a smoothstep falloff over a ~200 m border band. **Parameters are blended, then the terrain is generated once** from the blended parameters. Never blend two finished heightmaps.

## Height function (pure, in `World/Logic`)
`float HeightAt(double x, double z)` must be deterministic, thread-safe and allocation-free:
1. **Continentalness** (very low frequency) decides ocean vs. land vs. inland, run through a spline curve.
2. **Erosion** (low frequency) decides flat vs. rugged.
3. **Peaks and valleys** (medium frequency, ridged noise) decides mountains.
4. **Domain warping** on the sample coordinates turns blobby noise into ridges and coastlines.
5. Detail octaves (fBm) are scaled by ruggedness.
6. Each layer is multiplied by the blended region parameters.

Noise is implemented in `Logic` (OpenSimplex2 or Perlin, seeded through Pcg32/Hash, pure C#). A Burst/Jobs port in `Runtime` is welcome for speed, but it must match the Logic version within 1e-3 m, enforced by a test.

**Climate:** temperature and humidity noise maps (plus region biases, plus altitude cooling) choose the **biome** for splat and foliage through a Whittaker-style table. The biome is per-point, so a single region can hold beach, grassland and snowcap.

## Chunks and streaming
- **Chunk:** 64 m × 64 m, mesh grid 65 × 65 vertices (1 m) at LOD0. LODs 1–3 halve resolution, with skirts to hide cracks.
- **View distance:** about 1.5 km with LODs.
- **Generation runs off the main thread** (Jobs/Burst, or Tasks for the Logic version). The main thread only uploads meshes, with a per-frame budget so there are no hitches.
- **Floating origin:** when the player passes 4 km from the scene origin, recenter the world. Physics and shaders must stay correct at 100 km+.
- **Water:** a sea-level plane plus lakes where the height drops below the local water level. Simple stylized shader.

## Look (toon / cel, URP)
- **Terrain shader:** cel-banded lighting (2–3 bands), slope-based rock blend, biome splat from vertex data or a small splat texture, rim light, distance fog toward the sky color.
- **Character and props:** the same toon ramp, plus an outline (inverted-hull or screen-space edge).
- **Skybox:** a gradient sky and a sun disc. A day/night cycle is welcome but optional for M1.
- **Real shadows matter** (03-combat: you dodge by shadows), so keep shadow casting crisp.
- Placeholder art (low-poly, primitive-built) is fine. The look comes from shading and color.

## Player controller (M1 version)
- Third-person orbit camera (mouse), collision-aware, Genshin-like framing.
- Move, **sprint** (stamina), **dash** (about 4 m in about 0.18 s, stamina, **no invulnerability**), jump, **climb** real walls (≥65°, taller than the hero; stamina drain).
- A stamina bar on a minimal HUD. HP and mana bars can be stubs.
- Stats affecting speed and stamina come later. Use constants in one config ScriptableObject.

## Acceptance (Greg reviews on the board)
- [ ] Walk 3 km in a straight line from the origin: no seams, holes or visible chunk pops at normal play distance.
- [ ] At least 3 visibly different region types reachable within 2 km of spawn, with smooth borders.
- [ ] Frame time ≤ 16.6 ms on average and no hitch over 50 ms while sprinting across chunks (log a perf summary).
- [ ] Same seed gives the same world (a test hashes the heights of N sample points).
- [ ] Toon look: banded lighting, outlines, cliffs read as rock.
- [ ] Movement feels responsive. This one is Greg's "feel" check.

## Deliverables
- `Assets/Relic/World/Logic`: noise, region field, blending, climate/biome, height function, chunk math. **All tested.**
- `Assets/Relic/World/Runtime`: chunk streamer, mesh builder, floating origin, water, shaders/materials, player controller, camera, a debug overlay (seed, position, region, tier, biome, fps).
- `Assets/Relic/World/Scenes/TerrainSandbox.unity`: press Play and explore.
- A short `Assets/Relic/World/README.md`: how to run it, the knobs, known issues.
