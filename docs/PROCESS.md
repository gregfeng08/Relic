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

## Session protocol (for Claude)

1. Read `docs/SESSION_LOG.md` (latest entry) and the board, including any review verdicts and notes from Greg.
2. Act on review verdicts first, then take the top **Ready** card.
3. Commit after each meaningful step, on a branch, with clear messages.
4. Before stopping, update the board and add a session log entry: what changed, what's next, where it stopped.
5. Never push, publish, or change anything outside the repo without Greg's go-ahead on the board or in chat.
