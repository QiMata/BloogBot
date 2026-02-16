# BotRunner - Core Bot Orchestration Engine

Core behavior tree execution shared by both ForegroundBotRunner and BackgroundBotRunner.

## Key Files

| File | Purpose |
|------|---------|
| `BotRunnerService.cs` | Main service loop, behavior tree execution |
| `ClassContainer.cs` | Dependency injection container for bot components |
| `WoWNameGenerator.cs` | Random character name generation |

## Dependencies

- **GameData.Core** — All interfaces (IObjectManager, IWoWUnit, etc.)
- **BotCommLayer** — Protobuf IPC for inter-service communication

## Architecture Notes

- This is a **shared library**, not a standalone service
- Both `Services/ForegroundBotRunner/` and `Services/BackgroundBotRunner/` depend on this
- Changes here affect both execution modes — test both after modifications
