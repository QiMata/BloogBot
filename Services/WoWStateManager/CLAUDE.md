# WoWStateManager - Central Bot Orchestration FSM

Central state management service using the Stateless library for finite state machine coordination. Listens on **ports 5002** (character state) and **8088** (state manager API).

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Service entry point |
| `StateManagerWorker.cs` | FSM logic — states: Idle, Patrol, Combat, Rest, Dead, Loot |
| `MangosServerBootstrapper.cs` | Bootstraps connection to MaNGOS game server |
| `MangosServerOptions.cs` | Server connection configuration |
| `PathfindingServiceBootstrapper.cs` | Initializes PathfindingService client connection |

## Architecture

- Uses **Stateless** library for state machine transitions
- Event-driven: reacts to game state changes from WoWSharpClient
- Coordinates with PathfindingService (port 5000) for movement
- When debugging "stuck" states, trace the FSM transitions in `StateManagerWorker.cs`
