# PathfindingService - A* Pathfinding Worker Service

Stateless worker service that wraps the native `Navigation.dll` for path computation. Listens on **port 9002** (post-2026-05-18 port refactor — see `Services/WoWStateManager/CLAUDE.md` for the WWoW 9000-9099 port range).

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Service entry point and DI setup |
| `PathfindingServiceWorker.cs` | Background worker processing path requests |
| `PathfindingSocketServer.cs` | Protobuf TCP socket server for path requests |

## Dependencies

- **Navigation.dll** (C++ native, from `Exports/Navigation/`)
- **BotCommLayer** (Protobuf IPC)

## Communication

Receives pathfinding requests via Protobuf TCP sockets from ForegroundBotRunner/BackgroundBotRunner and returns computed paths.
