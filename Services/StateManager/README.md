# StateManager Service

## Overview

The **StateManager** service is the central orchestration hub of the WWoW (Westworld of Warcraft) AI system. It implements a stack-based **finite state machine** to control bot behavior, tracking what each bot is currently doing (its "state") and managing transitions between tasks like combat, traveling, resting, and more.

At runtime, the StateManager continually updates the active state and pushes or pops states on a stack as conditions change. This design enables nested goals - for example, if the bot dies, a sequence of corpse-retrieval states can be pushed on the stack on top of the regular grinding state. When those interim states complete, the bot automatically returns to its previous state.

In the overall WWoW architecture, StateManager serves as the **brain of the bot's automation**. It coordinates closely with other systems (navigation, combat logic, AI decision engines) through defined states. The service also enforces safety "kill-switches" and notifies other components when certain events occur. For instance, StateManager will stop a bot if it has been stuck in one state or position for too long (preventing endless loops) and can send alerts (via Discord integration) when that happens. It similarly monitors for unusual events (like unexpected teleportation) and triggers a stop and alert if detected, as long as those features are enabled in configuration.

Overall, StateManager ensures bots operate autonomously, react to in-game events, and stay within defined safety parameters - essential for creating AI-controlled characters indistinguishable from human players.

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

### Service Settings (appsettings.json)

```json
{
  "StateManager": {
    "UpdateIntervalMs": 50,
    "StuckInStateTimeoutMs": 300000,
    "StuckInPositionTimeoutMs": 60000
  }
}
```

## Usage

### Starting a Bot

```csharp
// 1. Obtain bot instance (via BotLoader MEF)
var myBot = botLoader.GetBot("FrostMageBot");

// 2. Create dependency container
var container = myBot.GetDependencyContainer(botSettings, probe, hotspots);

// 3. Start the bot
myBot.Start(container, () => Console.WriteLine("Bot stopped."));
```

### Issuing Travel Commands

```csharp
// Navigate to a destination
myBot.Travel(container, reverse: false, () => {
    Console.WriteLine("Travel complete.");
    myBot.Start(container, null);  // Resume grinding
});
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

## Extending the StateManager

### Creating a New State

1. Implement `IBotState`:

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

2. Use the `State` suffix naming convention
3. Keep `Update()` focused on a single responsibility
4. Don't manually pop the state - let StateManager handle it

### Adding a New Bot

1. Create project under bot modules folder
2. Implement `IBot` interface
3. Provide `GetDependencyContainer` implementation:

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

### Logging

- Use `Logger.Log()` for debug output
- Stuck events logged to `StuckLog.txt`
- Discord alerts for critical events

## Threading Notes

- State `Update()` calls execute on the main game thread
- Use `ThreadSynchronizer.RunOnMainThread()` for game API calls from async code
- Configuration changes take effect on the next tick

## Related Documentation

- See `Exports/BotRunner/README.md` for behavior trees and bot framework
- See `Services/PathfindingService/README.md` for navigation details
- See `Services/DecisionEngineService/README.md` for ML integration
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform - creating AI-controlled characters indistinguishable from human players.*
