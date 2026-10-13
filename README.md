# Relic

A procedural action RPG: an endless, Genshin-styled world, RotMG-style loot and bosses, and LLM-composed **relics** whose lore points back into the world.

- **Vision and pillars:** [docs/design/01-vision.md](docs/design/01-vision.md)
- **All design specs:** [docs/design/](docs/design/)
- **How the project runs** (board, streams, ownership): [docs/PROCESS.md](docs/PROCESS.md)
- **Session log:** [docs/SESSION_LOG.md](docs/SESSION_LOG.md)

## Requirements
- Unity **6000.3.9f1** (URP)
- .NET SDK 8+ (for the fast test harness)

## Tests
```bash
dotnet test tools/Relic.Tests
```
That runs every pure-logic test in seconds. Unity EditMode tests run the same suites inside the editor (Window → General → Test Runner) or headless:
```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile unity.log
```

## Layout
`Assets/Relic/Core` (shared contracts) · `World` (terrain, regions) · `Magic` (spellcraft) · `Items` (relics, bosses) · `Game` (composition)

The 2022 prototype is preserved at git tag `legacy-2022`.
