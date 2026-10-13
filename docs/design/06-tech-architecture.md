# 06 — Tech Architecture

Status: **v1** · Last updated 2026-10-13

## Platform
- **Unity 6000.3.9f1, URP.** Starts from the Universal 3D template. New Input System.
- **Target machine (Greg's dev laptop):** Core Ultra 9 275HX, 31 GB RAM, **RTX 5060 Laptop, 8 GB VRAM**. Target 60 fps at 1080p with the LLM idle.

## Code layout and ownership

```
Assets/Relic/
  Core/      Logic/ (Relic.Core: pure C#)            Tests/       ← LEAD only
  World/     Logic/ (pure) Runtime/ (Unity) Tests/                ← stream W
  Magic/     Logic/ (pure) Runtime/ (Unity) Tests/                ← stream M
  Items/     Logic/ (pure) Runtime/ (Unity) Tests/                ← stream I
  Game/      Runtime/ (composition, bootstrap, scenes)            ← LEAD only
tools/Relic.Tests/   dotnet test harness (globs every Logic/ and Tests/)
docs/                design specs, process, session log
```

- **`Logic/` is pure C#:** `noEngineReferences: true`, no UnityEngine, deterministic. That's where the interesting code goes (generators, math, parsers, validators), so it's testable in milliseconds.
- **`Runtime/`** holds MonoBehaviours, rendering, input and Jobs/Burst versions of hot loops.
- **Dependencies point one way:** stream assemblies reference Core and never each other. Relic.Game references everything and wires it together.
- **C# language level 9** (Unity 6). No records, `init`, file-scoped namespaces or global usings. The dotnet harness enforces this too.
- **ProjectSettings, Packages and Core are lead-owned.** A stream that needs a package asks the lead.

## Testing
| Layer | How | Speed |
|---|---|---|
| Pure logic | `dotnet test tools/Relic.Tests` | seconds |
| Unity compile + EditMode | `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults r.xml -logFile u.log` | 2–4 min |
| Feel / visuals | Greg, via board review cards | — |

Every stream's branch must pass **both** automated layers before asking for review.

## Determinism and seeds
- **All randomness** comes from `Relic.Core.Random.Pcg32`, seeded through `Hash.Combine(worldSeed, …, Hash.Of("purpose"))`. Never use `UnityEngine.Random`, `System.Random` or `string.GetHashCode()` for generation.
- **Terrain, regions, dungeons, base item rolls and boss layouts** are pure functions of seed and coordinates, so they regenerate identically.
- **LLM output is not deterministic** across hardware and model versions. Anything an LLM writes is therefore **generated once and persisted**. The seed decides *what to ask for*; the saved result is canonical. In multiplayer, the server generates and everyone receives the saved result.

## Local LLM
- **Runtime:** `llama.cpp` running as a **sidecar process** (`llama-server`), reached over localhost HTTP. It's robust, upgradeable without touching Unity, and supports **JSON-schema / GBNF constrained output**, so a small model can't produce malformed JSON.
- **Interface:** `ITextGenerator` in `Relic.Items.Logic` (pure C#, `HttpClient`). Implementations: `LlamaServerGenerator` and `FakeGenerator` (canned responses, for tests and CI).
- **Model budget (8 GB VRAM shared with the game):**
  - In-game generation: a **3–4B instruct model at Q4** (about 2.5 GB VRAM), or run on the CPU while the game is busy.
  - Offline and batch generation (pre-filling the relic pool): a **7–8B model at Q4** is fine when the game isn't running.
  - The exact model is chosen by stream I and benchmarked. Weights live in `/models/` (gitignored).
- **Never on the hot path:** a background **generation pool** keeps N relics pre-generated per region tier and theme. Combat never waits for the LLM.
- **Always validated:** every LLM program passes `EffectValidator`, then `ScaleToFit`. After 3 failures it falls back to a template.
- **Later, cloud:** the same `ITextGenerator` interface, with a cloud implementation behind a server.

## Saves
- JSON under `Application.persistentDataPath/saves/<world>/`.
- A world save holds the seed, the generator versions, and **deltas** only: discovered places, persisted generated content (relics, NPC states), vault contents, graves, and the character.
- Generated content records the `version` of the generator that made it, so old saves keep old items exactly.

## Combat runtime (integration milestone, lead)
Streams M and I produce **validated EffectPrograms** and test them in their own sandboxes. The shared runtime that executes programs in-game (projectiles, areas, statuses, damage) is built by the lead during integration, after M1. The streams provide simple debug visualizers in their own Runtime folders until then.

## Unity MCP (later)
Useful once there's something visual to inspect (screenshots, scene edits, the live console). Not needed for the groundwork. Only one editor per project folder, so one stream at a time.
