# PathfindingService - A* Pathfinding Worker Service

Stateless worker service that wraps the native `Navigation.dll` for path computation. Listens on **port 5001**.

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
