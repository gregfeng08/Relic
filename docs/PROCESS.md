# How this project runs

Claude owns day-to-day development. Greg sets direction and reviews.

## Where things live

| What | Where |
|---|---|
| Task board (source of truth for tasks) | Private claude.ai artifact "Relic Quest Board" (see `docs/BOARD_URL.txt`) |
| Design decisions | `docs/design/*.md` |
| What happened each session | `docs/SESSION_LOG.md` |
| Legacy 2022 prototype | git tag `legacy-2022` |

## Board columns

**Backlog → Ready → In Progress → Needs Review → Done**, plus **Blocked on Greg** for decisions only Greg can make.

Greg can approve or request changes on any card from the board. Each session starts by reading those responses.

## Parallel workstreams (proposed 2026-10-13)

Three systems are built in parallel by separate agents, each on its own git branch and worktree. The lead (this session) owns the shared contracts, reviews and merges.

| Stream | Owns | Depends on |
|---|---|---|
| **W — World** | Terrain sandbox: noise, chunks, region blending, toon shader, player controller | World-gen spec, project skeleton |
| **M — Magic** | Spellcraft: glyph recognizer, circle parser, compiler to effects, drawing UI | Effect grammar contract, spellcraft spec |
| **I — Items & bosses** | Relic generator (local LLM), power budget, boss/pattern generator | Effect grammar contract, item spec |
| **Lead** | `Relic.Core` (effect grammar, seeds, shared types), specs, board, integration | — |

**Worktrees:** `.wt/world`, `.wt/magic`, `.wt/items` (gitignored) on branches `stream/world`, `stream/magic`, `stream/items`, created from `rebuild`. Each is a separate Unity project with its own `Library/`. They're created manually with `git worktree add .wt/<s> -b stream/<s> rebuild`, because the automatic worktree tool checks out `main`, whose committed `Library/` has paths too long for Windows.

**Rules for parallel work**
- **Do first (lead, in order):** project skeleton → effect grammar contract → tech architecture → the specs each stream needs. Then streams start.
- **Folder and assembly ownership.** Each stream owns one folder and one assembly definition: `Assets/Relic/World`, `Assets/Relic/Magic`, `Assets/Relic/Items`. Only the lead edits `Assets/Relic/Core`, `ProjectSettings/` and `Packages/`. This avoids merge conflicts in Unity's `.meta` and settings files.
- **Pure C# where possible.** Generators, the grammar, the glyph compiler and the power budget live in plain C# with unit tests, so agents can verify work without opening the Unity editor.
- **Contract changes go through the lead.** A stream that needs a new effect type requests it; it doesn't edit Core itself.
- **Only "feel" checks need Greg:** how the terrain looks, how drawing feels, whether relics are fun to read.

## Session protocol (for Claude)

1. Read `docs/SESSION_LOG.md` (latest entry) and the board, including any review verdicts and notes from Greg.
2. Act on review verdicts first, then take the top **Ready** card.
3. Commit after each meaningful step, on a branch, with clear messages.
4. Before stopping, update the board and add a session log entry: what changed, what's next, where it stopped.
5. Never push, publish, or change anything outside the repo without Greg's go-ahead on the board or in chat.
