# StateManager Service

A .NET 8 Worker Service that provides central orchestration for bot instances in the WWoW (Westworld of Warcraft) ecosystem.

## Overview

The StateManager service is the central orchestration hub of the WWoW AI system. It implements a stack-based finite state machine to control bot behavior, tracking what each bot is currently doing (its "state") and managing transitions between tasks like combat, traveling, resting, and more.

At runtime, the StateManager continually updates the active state and pushes or pops states on a stack as conditions change. This design enables nested goals - for example, if the bot dies, a sequence of corpse-retrieval states can be pushed on the stack on top of the regular grinding state. When those interim states complete, the bot automatically returns to its previous state.

The service also enforces safety "kill-switches" and notifies other components when certain events occur. For instance, StateManager will stop a bot if it has been stuck in one state or position for too long (preventing endless loops) and can send alerts (via Discord integration) when that happens. It similarly monitors for unusual events (like unexpected teleportation) and triggers a stop and alert if detected.

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

## Project Structure

```
Services/StateManager/
+-- StateManager.csproj              # .NET 8 Worker project file
+-- README.md                        # This documentation
+-- Program.cs                       # Service entry point
+-- StateManagerWorker.cs            # Main BackgroundService worker
+-- Settings/
|   +-- StateManagerSettings.cs      # Configuration settings
+-- Clients/
|   +-- ActivityMemberUpdateClient.cs # Activity updates
|   +-- MangosSOAPClient.cs          # MaNGOS server communication
|   +-- StateManagerUpdateClient.cs  # State update broadcasts
+-- Listeners/
|   +-- CharacterStateSocketListener.cs # Character state events
|   +-- StateManagerSocketListener.cs   # External state commands
+-- Repository/
    +-- ActorDatabase.cs             # Actor/bot data storage
    +-- ReamldRepository.cs          # Realm database access
```

## Key Components

### StateManagerWorker

The main `BackgroundService` that orchestrates the state machine:

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

### State Interface (IBotState)

The contract for all states in the system:

```csharp
public interface IBotState
{
    void Update();
}
```

Every concrete state class implements `Update()` with its behavior, called repeatedly when that state is on top of the stack.

### Dependency Container (IDependencyContainer)

Bridge between StateManager and game-specific logic:

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

### Socket Listeners

| Listener | Port | Purpose |
|----------|------|---------|
| CharacterStateSocketListener | 5002 | Receives character activity snapshots, provides state info |
| StateManagerSocketListener | 8088 | Processes state change requests, coordinates transitions |

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

### Movement States

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
- **BotCommLayer**: Protobuf IPC communication

## Usage

### Running the Service

```bash
dotnet run
```

### Service Flow

1. **Initialization**: Loads configuration and character definitions
2. **Dependency Check**: Verifies PathfindingService is running, launches if needed
3. **Account Management**: Creates MaNGOS accounts for configured characters if they don't exist
4. **Bot Spawning**: Starts BackgroundBotRunner instances for each character
5. **State Monitoring**: Continuously monitors and manages character states
6. **Communication**: Handles incoming requests via socket listeners

### Starting a Bot Programmatically

```csharp
// 1. Obtain bot instance (via BotLoader MEF)
var myBot = botLoader.GetBot("FrostMageBot");

// 2. Create dependency container
var container = myBot.GetDependencyContainer(botSettings, probe, hotspots);

// 3. Start the bot
myBot.Start(container, () => Console.WriteLine("Bot stopped."));
```

## Configuration

### appsettings.json

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
  },
  "StateManager": {
    "UpdateIntervalMs": 50,
    "StuckInStateTimeoutMs": 300000,
    "StuckInPositionTimeoutMs": 60000
  }
}
```

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

## Related Documentation

- See `Exports/BotRunner/README.md` for behavior trees and bot framework
- See `Services/PathfindingService/README.md` for navigation details
- See `Services/DecisionEngineService/README.md` for ML integration
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
