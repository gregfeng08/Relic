# How this project runs

Claude owns day-to-day development. Greg sets direction and reviews.

## Where things live

| What | Where |
|---|---|
| Task board (source of truth for tasks) | Private claude.ai artifact "Relic Quest Board" (see `docs/BOARD_URL.txt`) |
| Design decisions | `docs/design/*.md` |
| What happened each session | `docs/SESSION_LOG.md` |
| Legacy 2022 prototype | git tag `legacy-2022` |
| Scenes for Greg to playtest | `Assets/_Playtest/` (top level = needs review; `Archive/` = retired). Rules in its `README.md`: new review scenes go there with a table row; after a verdict or retirement, move to `Archive/` or delete. |

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
| **Lead** | `Relic.Core` (effect grammar, seeds, shared types), `Relic.Combat` (combat rules engine + balance sim), specs, board, integration | — |

**Worktrees:** `.wt/world`, `.wt/magic`, `.wt/items` (gitignored) on branches `stream/world`, `stream/magic`, `stream/items`, created from `main`. Each is a separate Unity project with its own `Library/`. Create them with `git worktree add .wt/<s> -b stream/<s> main`. (Since 2026-11-18, `main` is the working branch; the old `rebuild` branch was fast-forwarded into it.)

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

## Autonomous runs (hourly since 2026-11-18; Greg)
A scheduled task in the Claude desktop app starts a fresh session every hour. It **builds, tests and commits on stream branches only**, and **never merges or pushes**. Greg reviews ready-to-merge cards on the board; merging and pushing happen only in an interactive session with Greg.

**Every run:**
1. **Lock.** If `.wt/AUTORUN.lock` exists and is less than 3 hours old, stop without doing anything. Otherwise write it (timestamp), and delete it at the end.
2. **Read:** the latest `docs/SESSION_LOG.md` entry, this file, the relevant `docs/design/` docs, and the board (`docs/BOARD_URL.txt`). Note any new responses from Greg in the log, so the next interactive session acts on them.
3. **Check the machine:** never run Unity on the main checkout (Greg may have it open); use worktrees. GPU work only if no game is running.
4. **Pick work:** the top **Ready** cards that aren't already in progress, at most 2 at a time, each in its own worktree and branch `stream/<name>` (from `main`). Resume unfinished stream branches before starting new ones. Never touch cards in Review or Blocked.
5. **Verify:** `dotnet test tools/Relic.Tests`; Unity EditMode and PlayMode in the worktree (background); the Unity log scanned for errors; screenshots reviewed for visual work.
6. **Commit** on the stream branch, then move the card to **Needs review** with: the branch name, what changed, test results, screenshot paths (`art-ref/renders/`, local only), and "what to try."
7. **Log:** a `meta/log` entry on the board and a `docs/SESSION_LOG.md` entry (committed on the stream branch, not on `main`).
8. **Unlock and stop.** If a usage limit interrupts a run, the next run resumes from the branches and the board.

**Never:** merge, push, commit directly to `main`, download anything, commit `art-ref/`, change ProjectSettings or Packages without a card that says so, decide anything awaiting Greg's review, or delete branches.
