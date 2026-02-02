# StateManager Service

## Overview
A .NET 8 Worker Service that manages the state and coordination of World of Warcraft bot instances in the BloogBot ecosystem.

The **StateManager** service is the central orchestration hub of the WWoW (Westworld of Warcraft) AI system. It implements a stack-based **finite state machine** to control bot behavior, tracking what each bot is currently doing (its "state") and managing transitions between tasks like combat, traveling, resting, and more.
## Overview

At runtime, the StateManager continually updates the active state and pushes or pops states on a stack as conditions change. This design enables nested goals - for example, if the bot dies, a sequence of corpse-retrieval states can be pushed on the stack on top of the regular grinding state. When those interim states complete, the bot automatically returns to its previous state.
The StateManager service is a background service that orchestrates multiple bot instances, manages character states, and provides centralized coordination for the bot ecosystem. It acts as the central hub for managing bot accounts, character definitions, and communication between various services.

In the overall WWoW architecture, StateManager serves as the **brain of the bot's automation**. It coordinates closely with other systems (navigation, combat logic, AI decision engines) through defined states. The service also enforces safety "kill-switches" and notifies other components when certain events occur. For instance, StateManager will stop a bot if it has been stuck in one state or position for too long (preventing endless loops) and can send alerts (via Discord integration) when that happens. It similarly monitors for unusual events (like unexpected teleportation) and triggers a stop and alert if detected, as long as those features are enabled in configuration.
## Features

Overall, StateManager ensures bots operate autonomously, react to in-game events, and stay within defined safety parameters - essential for creating AI-controlled characters indistinguishable from human players.
- **Character State Management**: Tracks and manages individual character states and activities
- **Background Bot Orchestration**: Automatically starts and manages BackgroundBotRunner instances for each configured character
- **Socket-based Communication**: Provides real-time communication via TCP socket listeners
- **Account Management**: Integrates with MaNGOS SOAP API for automatic account creation and GM level management
- **Service Coordination**: Automatically launches and manages dependent services like PathfindingService
- **Configuration-driven**: Fully configurable via JSON settings files

## Architecture

```
+------------------------------------------------------------------+
|                    StateManager Service                           |
+------------------------------------------------------------------+
|                                                                   |
|  +-----------------------------------------------------------+   |
|  |              StateManagerWorker (BackgroundService)        |   |
|  |           Main service loop - coordinates all components   |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|         +--------------------+--------------------+               |
|         |                    |                    |               |
|  +--------------+    +---------------+    +---------------+      |
|  | State Stack  |    |  IBotState    |    | Dependency    |      |
|  |              |    |  Interface    |    | Container     |      |
|  | - Push/Pop   |    |               |    |               |      |
|  | - Current    |    | - Update()    |    | - BotSettings |      |
|  | - Base State |    | - States:     |    | - Hotspots    |      |
|  |              |    |   Combat      |    | - Probe       |      |
|  |              |    |   Travel      |    | - Factories   |      |
|  |              |    |   Rest        |    |               |      |
|  +--------------+    +---------------+    +---------------+      |
|         |                    |                    |               |
|  +-----------------------------------------------------------+   |
|  |                    External Communication                  |   |
|  |  +------------------+    +-----------------------------+   |   |
|  |  | Socket Listeners |    |  Clients (Pathfinding, etc) |   |   |
|  |  +------------------+    +-----------------------------+   |   |
|  +-----------------------------------------------------------+   |
|                                                                   |
+------------------------------------------------------------------+
```
### Core Components

## Project Structure
- **StateManagerWorker**: Main background service that orchestrates all operations
- **CharacterStateSocketListener**: Handles character state updates and queries
- **StateManagerSocketListener**: Manages state change requests and responses
- **MangosSOAPClient**: Communicates with MaNGOS server for account management

```
Services/StateManager/
??? StateManager.csproj              # .NET 8 Worker project file
??? README.md                         # This documentation
??? Program.cs                        # Service entry point
??? StateManagerWorker.cs             # Main BackgroundService worker
??? Settings/
?   ??? StateManagerSettings.cs       # Configuration settings
??? Clients/
?   ??? ActivityMemberUpdateClient.cs # Activity updates
?   ??? MangosSOAPClient.cs           # MaNGOS server communication
?   ??? StateManagerUpdateClient.cs   # State update broadcasts
??? Listeners/
?   ??? CharacterStateSocketListener.cs # Character state events
?   ??? StateManagerSocketListener.cs   # External state commands
??? Repository/
    ??? ActorDatabase.cs              # Actor/bot data storage
    ??? ReamldRepository.cs           # Realm database access
```
### Dependencies

## Key Components
- **BackgroundBotRunner**: Individual bot instance management
- **DecisionEngineService**: AI decision-making capabilities
- **PromptHandlingService**: Natural language processing
- **PathfindingService**: Navigation and movement coordination

### StateManagerWorker
## Configuration

The main `BackgroundService` that orchestrates the state machine:
### appsettings.json

```csharp
public class StateManagerWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Update active state
            // Check kill-switches
            // Process state transitions
            await Task.Delay(50, stoppingToken);
        }
    }
}
```
```json
{
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": "5000"
  },
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": "5002"
  },
  "StateManagerListener": {
    "IpAddress": "127.0.0.1",
    "Port": "8088"
  },
  "MangosSOAP": {
    "IpAddress": "http://localhost:7878"
  }
}
```

### State Interface (IBotState)
### Character Definitions

The contract for all states in the system:
Character configurations are managed through `StateManagerSettings.json` which defines:
- Character account names
- Character-specific settings
- Bot behavior configurations

```csharp
public interface IBotState
{
    void Update();
}
```
## Usage

Every concrete state class implements `Update()` with its behavior, called repeatedly when that state is on top of the stack.
### Running the Service

### Dependency Container (IDependencyContainer)
The StateManager runs as a Windows Service or console application:

Bridge between StateManager and game-specific logic:
```bash
dotnet run
```

```csharp
public interface IDependencyContainer
{
    // Factory methods for state creation
    Func<Stack<IBotState>, IDependencyContainer, IBotState> CreateRestState { get; }
    Func<Stack<IBotState>, IDependencyContainer, IWoWUnit, IBotState> CreateMoveToTargetState { get; }
    
    // Configuration and environment
    BotSettings BotSettings { get; }
    IEnumerable<Hotspot> Hotspots { get; }
    Probe Probe { get; }
    
    // Utility methods
    IWoWUnit FindClosestTarget();
    void CheckForTravelPath(Stack<IBotState> states, bool reverse, bool needsToRest);
}
```
### Service Flow

1. **Initialization**: Loads configuration and character definitions
2. **Dependency Check**: Verifies PathfindingService is running, launches if needed
3. **Account Management**: Creates MaNGOS accounts for configured characters if they don't exist
4. **Bot Spawning**: Starts BackgroundBotRunner instances for each character
5. **State Monitoring**: Continuously monitors and manages character states
6. **Communication**: Handles incoming requests via socket listeners

### Socket Communication

## State Types

### Base States

| State | Purpose |
|-------|---------|
| `GrindState` | Default looping state for killing mobs in a hotspot |
| `PowerlevelState` | Base state for power-leveling mode |

### Combat States

| State | Purpose |
|-------|---------|
| `CombatStateBase` | Shared combat utility and targeting logic |
| `*Bot.CombatState` | Class-specific combat rotations |
#### Character State Listener (Port 5002)
- Receives character activity snapshots
- Provides current character state information
- Handles character state queries

### Movement States
#### State Manager Listener (Port 8088)
- Processes state change requests
- Coordinates state transitions
- Manages inter-service communication

| State | Purpose |
|-------|---------|
| `MoveToTargetState` | Move into combat range of a target |
| `MoveToPositionState` | Navigate to specific coordinates |
| `TravelState` | Long-distance waypoint-based travel |

### Resource States

| State | Purpose |
|-------|---------|
| `RestState` | Recover health/mana (eat/drink) |
| `LootState` | Loot corpses after kills |
| `GatherObjectState` | Interact with world objects (mining, etc.) |

### Vendor/Errand States

| State | Purpose |
|-------|---------|
| `RepairEquipmentState` | Repair gear at NPC |
| `SellItemsState` | Sell items to vendor |
| `BuyItemsState` | Purchase consumables |

### Death Recovery States

| State | Purpose |
|-------|---------|
| `ReleaseCorpseState` | Release spirit on death |
| `MoveToCorpseState` | Navigate back to corpse |
| `RetrieveCorpseState` | Accept resurrection |

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Data.Sqlite.Core | 8.0.8 | SQLite database access |
| Newtonsoft.Json | 13.0.3 | Configuration parsing |

## Project References

- **BackgroundBotRunner**: Headless bot execution
- **ForegroundBotRunner**: Injected bot execution
- **DecisionEngineService**: ML-based decision making
- **PathfindingService**: Navigation and pathfinding

## Configuration

### Bot Settings (botSettings.json)

```json
{
  "UseTeleportKillswitch": true,
  "UseStuckInPositionKillswitch": true,
  "UseStuckInStateKillswitch": true,
  "CurrentBotName": "FrostMageBot",
  "CurrentTravelPath": {
    "Name": "ElwynnForest_ToGoldshire",
    "Waypoints": [...]
  },
  "GrindingHotspot": {
    "Id": 1,
    "RepairVendor": {...},
    "Innkeeper": {...}
  },
  "TargetingIncludedNames": [],
  "TargetingExcludedNames": ["Hogger"]
}
```
## Development

### Service Settings (appsettings.json)
### Prerequisites

```json
{
  "StateManager": {
    "UpdateIntervalMs": 50,
    "StuckInStateTimeoutMs": 300000,
    "StuckInPositionTimeoutMs": 60000
  }
}
```
- .NET 8 SDK
- Access to MaNGOS server with SOAP interface enabled
- SQLite (for state persistence)

## Usage
### Key Classes

### Starting a Bot
- `StateManagerWorker`: Main orchestration logic
- `StateManagerSettings`: Configuration management
- `CharacterStateSocketListener`: Character state communication
- `MangosSOAPClient`: Server integration
- `ActivitySnapshot`: Character state data structure

```csharp
// 1. Obtain bot instance (via BotLoader MEF)
var myBot = botLoader.GetBot("FrostMageBot");

// 2. Create dependency container
var container = myBot.GetDependencyContainer(botSettings, probe, hotspots);

// 3. Start the bot
myBot.Start(container, () => Console.WriteLine("Bot stopped."));
```

### Issuing Travel Commands
### Building

```csharp
// Navigate to a destination
myBot.Travel(container, reverse: false, () => {
    Console.WriteLine("Travel complete.");
    myBot.Start(container, null);  // Resume grinding
});
```bash
dotnet build
```

### Stopping a Bot

```csharp
myBot.Stop();
```

## State Machine Flow

```
                    +------------------+
                    |     Start()      |
                    +--------+---------+
                             |
                             v
                    +------------------+
               +--->|   GrindState     |<---+
               |    +--------+---------+    |
               |             |              |
               |    Target   | Death        | Errand
               |    Found    | Detected     | Complete
               |             |              |
               v             v              v
        +-----------+  +-----------+  +-----------+
        |  Combat   |  |  Release  |  |  Repair/  |
        |  State    |  |  Corpse   |  |  Sell     |
        +-----------+  +-----------+  +-----------+
               |             |              |
               v             v              |
        +-----------+  +-----------+        |
        |   Loot    |  |  MoveTo   |        |
        |   State   |  |  Corpse   |        |
        +-----------+  +-----------+        |
               |             |              |
               +------+------+--------------+
                      |
                      v
               +-------------+
               |    Rest     |
               |    State    |
               +-------------+
                      |
                      +-----> (back to GrindState)
```

## Kill-Switches

The StateManager implements several safety mechanisms:

| Kill-Switch | Description |
|-------------|-------------|
| `TeleportKillswitch` | Stops if unexpected teleport detected |
| `StuckInPositionKillswitch` | Stops if position unchanged too long |
| `StuckInStateKillswitch` | Stops if same state active too long |

When triggered, these write to `StuckLog.txt` and optionally send Discord alerts.
### Testing

## Extending the StateManager
```bash
dotnet test
```

### Creating a New State
## Integration

1. Implement `IBotState`:
The StateManager integrates with several other services in the BloogBot ecosystem:

```csharp
public class MyCustomState : IBotState
{
    private readonly Stack<IBotState> _stateStack;
    private readonly IDependencyContainer _container;
    
    public MyCustomState(Stack<IBotState> stateStack, IDependencyContainer container)
    {
        _stateStack = stateStack;
        _container = container;
    }
    
    public void Update()
    {
        // State logic here
        // Push new states or let StateManager pop this one
    }
}
```
- **UI Integration**: StateManagerUI provides a WPF interface for monitoring and control
- **Bot Runners**: Manages ForegroundBotRunner and BackgroundBotRunner instances
- **AI Services**: Coordinates with DecisionEngineService and PromptHandlingService
- **Game Integration**: Communicates with WoW client through BotCommLayer

## Troubleshooting

### Common Issues

2. Use the `State` suffix naming convention
3. Keep `Update()` focused on a single responsibility
4. Don't manually pop the state - let StateManager handle it
1. **Port Already in Use**: Ensure configured ports are available
2. **MaNGOS Connection Failed**: Verify MaNGOS server is running and SOAP is enabled
3. **PathfindingService Not Found**: Check PathfindingService configuration and availability
4. **Character Creation Failed**: Verify MaNGOS SOAP credentials and permissions

### Adding a New Bot
### Logging

1. Create project under bot modules folder
2. Implement `IBot` interface
3. Provide `GetDependencyContainer` implementation:
The service uses structured logging with configurable levels. Check logs for detailed error information and service status.

```csharp
public IDependencyContainer GetDependencyContainer(
    BotSettings settings, 
    Probe probe, 
    IEnumerable<Hotspot> hotspots) =>
    new DependencyContainer(
        AdditionalTargetingCriteria,
        CreateRestState,
        CreateMoveToTargetState,
        CreatePowerlevelCombatState,
        settings, probe, hotspots);
```

4. Add `[Export(typeof(IBot))]` attribute for MEF discovery

## Integration with Other Services

```
+----------------+     +------------------+     +------------------+
| StateManager   |<--->| PathfindingService|     | DecisionEngine   |
|                |     |                  |     |                  |
| State Machine  |     | Navigation       |     | ML Predictions   |
| Orchestration  |     | Pathfinding      |     | Action Decisions |
+----------------+     +------------------+     +------------------+
        |                      ^                        ^
        |                      |                        |
        v                      |                        |
+----------------+     +------------------+     +------------------+
| BotRunner(s)   |     | BotCommLayer     |     | PromptHandling   |
|                |     |                  |     |                  |
| Foreground/    |     | Protobuf IPC     |     | AI Prompts       |
| Background     |     | Communication    |     | Natural Language |
+----------------+     +------------------+     +------------------+
```

## Monitoring and Debugging

### Probe Telemetry

```csharp
// Access current state and latency
var currentState = container.Probe.CurrentState;    // "CombatState"
var latency = container.Probe.UpdateLatency;        // 45ms
```
### Performance Considerations

### Logging

- Use `Logger.Log()` for debug output
- Stuck events logged to `StuckLog.txt`
- Discord alerts for critical events

## Threading Notes
- Each character spawns a separate BackgroundBotRunner instance
- Socket listeners run on separate threads
- Database operations are optimized for concurrent access
- Memory usage scales with the number of managed characters

- State `Update()` calls execute on the main game thread
- Use `ThreadSynchronizer.RunOnMainThread()` for game API calls from async code
- Configuration changes take effect on the next tick
## Contributing

## Related Documentation
When contributing to the StateManager service:

- See `Exports/BotRunner/README.md` for behavior trees and bot framework
- See `Services/PathfindingService/README.md` for navigation details
- See `Services/DecisionEngineService/README.md` for ML integration
- See `ARCHITECTURE.md` for system overview
1. Follow established patterns for background service implementation
2. Ensure proper error handling and logging
3. Test with multiple character configurations
4. Verify socket communication protocols
5. Document any new configuration options

---
## License

*This component is part of the WWoW (Westworld of Warcraft) simulation platform - creating AI-controlled characters indistinguishable from human players.*
Part of the BloogBot project ecosystem.