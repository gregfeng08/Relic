# 18 — Compute budget: where the LLM is (and isn't)

Status: **v1 (lead)** · Last updated 2026-11-08
Greg: "I want to double check computation intensity of this game, since everything seems to rely on LLMs of some sort."

## The short answer
**The LLM is never in the frame loop.**
- **Plain deterministic code:** combat, movement, terrain, stat rolls, effect validation, boss patterns, spell compiling (common patterns) and the world graph's structure.
- **Measured on your laptop:** the sandbox runs at **3.1–3.7 ms per frame (270–330 fps)**.
- **What the LLM does:** it only *writes text and JSON* for rare, cacheable things, as **background jobs**, and every result is saved forever (06). The game never waits on it during play.

## Every LLM call site
Estimates use the measured 35 tokens/s for Qwen3.5-4B on the RTX 5060 (prompt reading is about 200 tokens/s). They get re-measured by the benchmark (RLC-031).

| Use | How often | Output size | Time each (GPU) | When it runs | Without the LLM |
|---|---|---|---|---|---|
| Rare item name and flavor | A few per hour | ~40 tokens | ~1–2 s | Background pool | Template name |
| Epic lore and one enchantment | ~1 per hour | ~300 tokens | ~9 s | Background pool | Template |
| **Legendary relic** | Rare (a boss drop) | ~700 tokens | ~20–25 s | **Pre-generated pool**, filled during loading, menus and sanctuaries | Template relic |
| Place lore and inscriptions | When you enter a region or read something new | ~200 tokens | ~6 s | Pre-fetched as you approach; cached per place | "The inscription is too worn to read" |
| **Spell Magister** (new, novel spells only) | A handful per session, **at a desk** | ~400 tokens | ~12 s | While you write (an "ink settling" moment) | The deterministic core handles common spells instantly with no LLM; cached forever per sentence |
| NPC dialogue (later) | The most frequent | ~80 tokens per line | ~2–3 s, streamed | Live conversation only | Pre-written barks for ambient chatter |
| Photo → character (later) | Once per character | ~300 tokens | ~10 s | Character creation | Manual sliders |

**Typical hour of play:** about 5–15 LLM jobs, roughly 2–4 minutes of total generation, all in the background. Compare: the game renders about 1 million frames' worth of work in that hour without touching the LLM.

## The real cost is GPU sharing, not total compute
While a job runs on the GPU, it competes with rendering and could cause frame dips. Mitigations, in order:
1. **Schedule GPU jobs at quiet moments:** loading, menus, sanctuaries, the writing desk.
2. **Run background jobs on the CPU** (a 24-thread Core Ultra 9; a 4B model at roughly 8–12 tokens/s). That's slower, so a relic takes about 1 minute, but it doesn't touch frame time. Fine for a pool that is filled ahead of need.
3. **Pre-generate pools:** for example, 3 relics per tier and theme kept ready, refilled during downtime.
4. **VRAM:** the 4B model plus context is about 3 GB of the 8 GB. Load it only when needed, or keep it on the CPU.

## Multiplayer and cloud
- **Multiplayer:** the server (or a cloud LLM) does all generation; **clients do zero LLM work.**
- **Cloud cost:** each relic is about 1–2k tokens in and out, so fractions of a cent per relic. A player generates maybe 10–20 jobs per hour.

## The rule we follow
**Every LLM feature must have a non-LLM fallback** (templates, deterministic compilers, pre-written barks). Then the game is fully playable with the LLM switched off, and the LLM makes it richer rather than holding it up.

## Next step
The model benchmark (RLC-031) measures real tokens/s on GPU and CPU, VRAM use, valid-JSON rate, and the frame-time impact while generating. It runs when the GPU is free.
