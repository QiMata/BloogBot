# BackgroundBotRunner — Headless Protocol Emulation Bot

Headless bot that runs as a standalone .NET Worker Service. No game client required — communicates with MaNGOS server via pure C# WoW protocol emulation (WoWSharpClient).

## Build & Run

Launched by StateManager via `dotnet BackgroundBotRunner.dll` with environment variables:
- `WWOW_ACCOUNT_NAME` — Account name (resolves bot class via WoWNameGenerator)
- `WWOW_ACCOUNT_PASSWORD` — Plaintext password
- `WWOW_CHARACTER_CLASS` / `WWOW_CHARACTER_RACE` — Optional overrides

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `Program.cs` | 16 | Entry point — Serilog init, host builder, registers BackgroundBotWorker |
| `BackgroundBotWorker.cs` | 347 | Main orchestrator — lifecycle, connections, agent factory |

**Total: 363 lines.** This is a minimal orchestrator that delegates all logic to shared libraries.

## Architecture

```
BackgroundBotWorker (IHostedService)
├── PathfindingClient → PathfindingService (port 5001)
├── CharacterStateUpdateClient → StateManager (port 5002)
├── WoWClient → MaNGOS server (auth + world protocol)
├── WoWSharpObjectManager (singleton) → IObjectManager
├── BotRunnerService → behavior tree execution
└── NetworkClientComponentFactory → agents (targeting, spellcasting, loot, vendor, etc.)
```

### Agent Factory Lifecycle
- Created dynamically when `WoWClient.WorldClient.IsConnected` becomes true
- Disposed on disconnect
- `MaintainAgentFactory()` polls every 100ms

## Dependencies

- **BotRunner** — BotRunnerService behavior trees
- **WoWSharpClient** — Pure C# WoW protocol (auth, world, packets)
- **BotProfiles** — Combat rotation profiles

## Configuration (appsettings.json)

| Section | Key | Default |
|---------|-----|---------|
| PathfindingService | IpAddress/Port | 127.0.0.1:5001 |
| CharacterStateListener | IpAddress/Port | 127.0.0.1:5002 |
| RealmEndpoint | IpAddress | 127.0.0.1 |
| BotBehavior | MaxPullRange, RestHpThresholdPct, GatherDetectRange, etc. | Various |

## ForegroundBotRunner vs BackgroundBotRunner

| Aspect | Background (this) | Foreground |
|--------|-------------------|------------|
| Execution | Standalone dotnet.exe process | DLL injected into WoW.exe |
| Game client | Not required | Required |
| Object access | WoW protocol packets | Direct memory read/write |
| Scalability | Unlimited instances | 1 per WoW.exe |
| Code size | 363 lines | ~13,400 lines |
| Latency | Network-bound (100-500ms) | Near-instant |
