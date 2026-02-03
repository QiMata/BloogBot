# WWoW Copilot Context Reference

This document provides quick context for GitHub Copilot to understand the codebase patterns and conventions.

## Solution Identity

- **Name**: Westworld of Warcraft (WWoW) / BloogBot
- **Purpose**: AI-driven WoW bot simulation platform
- **Target**: .NET 8.0, C++20
- **Supported WoW**: Vanilla (1.12.1), TBC (2.4.3), WotLK (3.3.5a)

## Key Patterns

### State Machine Pattern (StateManager)
```csharp
// States implement IBotState
public interface IBotState
{
    void Update();
}

// States are managed on a stack
Stack<IBotState> _botStates;
_botStates.Push(new CombatState(/*...*/));
_botStates.Pop();
```

### Dependency Injection Pattern
```csharp
// IDependencyContainer provides factories and settings
public interface IDependencyContainer
{
    BotSettings BotSettings { get; }
    Func<Stack<IBotState>, IDependencyContainer, IBotState> CreateRestState { get; }
    Func<Stack<IBotState>, IDependencyContainer, WoWUnit, IBotState> CreateMoveToTargetState { get; }
}
```

### Packet Handler Pattern (WoWSharpClient)
```csharp
[PacketHandler(OpCode.SMSG_UPDATE_OBJECT)]
public void HandleUpdateObject(PacketReader reader, WoWSharpObjectManager objectManager)
{
    // Handle packet
}
```

### Protobuf IPC Pattern (BotCommLayer)
```csharp
// Server
var server = new ProtobufSocketServer<TRequest, TResponse>(port);
server.OnMessageReceived += HandleMessage;

// Client
var client = new ProtobufSocketClient<TRequest, TResponse>(host, port);
var response = await client.SendAsync(request);
```

### Semantic Kernel Pattern (BloogBot.AI)
```csharp
// KernelCoordinator swaps plugins based on activity
public void OnActivityChanged(BotActivity newActivity)
{
    _kernel.Plugins.Clear();
    foreach (var p in _catalog.For(newActivity))
        _kernel.Plugins.Add(p);
}
```

## Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Bot State | `*State` | `CombatState`, `RestState` |
| Packet Handler | `*Handler` | `MovementHandler`, `ChatHandler` |
| Service | `*Service` | `PathfindingService`, `DecisionEngineService` |
| Worker | `*Worker` | `StateManagerWorker`, `PathfindingServiceWorker` |
| Client | `*Client` | `PathfindingClient`, `WorldClient` |
| Model Interface | `IWoW*` | `IWoWUnit`, `IWoWPlayer` |

## Project Relationships

```
WoWSharpClient ? GameData.Core (interfaces)
BotRunner ? GameData.Core (IObjectManager)
ForegroundBotRunner ? BotRunner, GameData.Core, FastCall (C++ DLL)
BackgroundBotRunner ? BotRunner, WoWSharpClient, PromptHandlingService
StateManager ? BotRunner, BotCommLayer
PathfindingService ? Navigation (C++ DLL), GameData.Core
BloogBot.AI ? GameData.Core (uses Stateless, SemanticKernel)
```

## BotRunner Implementations

The `BotRunner` library provides core bot logic via `BotRunnerService`. Two implementations provide `IObjectManager`:

### ForegroundBotRunner (DLL Injection)
```csharp
// Injected into WoW.exe, uses direct memory access
public class ObjectManager : IObjectManager
{
    // Reads game state from WoW memory
    public IWoWLocalPlayer Player => /* memory read */;
    public IEnumerable<IWoWUnit> Units => /* enumerate via callback */;
}
```
**Key files**: `Statics/ObjectManager.cs`, `Mem/Functions.cs`, `Mem/MemoryAddresses.cs`

### BackgroundBotRunner (Headless)
```csharp
// Uses WoWSharpClient for protocol emulation
public class BackgroundBotWorker : BackgroundService
{
    private readonly WoWClient _wowClient;
    private readonly BotRunnerService _botRunner;
    
    // WoWSharpObjectManager implements IObjectManager via packets
    _botRunner = new BotRunnerService(
        WoWSharpObjectManager.Instance,  // IObjectManager
        _characterStateUpdateClient,
        _pathfindingClient
    );
}
```
**Key files**: `BackgroundBotWorker.cs`, `WoWSharpClient/WoWSharpObjectManager.cs`

## Common Code Locations

| Task | Location |
|------|----------|
| Add game model | `Exports/GameData.Core/Interfaces/` + `Exports/WoWSharpClient/Models/` |
| Add bot state | `Services/StateManager/` or bot-specific module |
| Add packet handler | `Exports/WoWSharpClient/Handlers/` |
| Add IPC message | `Exports/BotCommLayer/Models/` |
| Add bot activity | `BloogBot.AI/States/BotActivity.cs` |
| Modify pathfinding | `Services/PathfindingService/` or `Exports/Navigation/` |
| Add behavior tree action | `Exports/BotRunner/BotRunnerService.cs` |
| Memory-based game access | `Services/ForegroundBotRunner/Mem/`, `Services/ForegroundBotRunner/Statics/` |
| Packet-based game access | `Exports/WoWSharpClient/WoWSharpObjectManager.cs` |
| Headless bot setup | `Services/BackgroundBotRunner/BackgroundBotWorker.cs` |

## Bot Activities (BloogBot.AI)

```csharp
public enum BotActivity
{
    Resting, Questing, Grinding, Professions, Talenting, Equipping,
    Trading, Guilding, Chatting, Helping, Mailing, Partying,
    RolePlaying, Combat, Battlegrounding, Dungeoning, Raiding,
    WorldPvPing, Camping, Auction, Banking, Vending, Exploring,
    Traveling, Escaping, Eventing
}
```

## State Triggers (BloogBot.AI)

```csharp
public enum Trigger
{
    HealthRestored, QuestComplete, QuestFailed, ProfessionLevelUp,
    TalentPointsAvailable, TalentPointsAttributed, EquipmentChanged,
    TradeRequested, TradeComplete, GuildInvite, GuildingEnded,
    ChatMessageReceived, ChattingEnded, HelpRequested, HelpingEnded,
    MailReceived, MailingEnded, PartyInvite, PartyEnded,
    RolePlayEngaged, RolePlayEnded, CombatStarted, CombatEnded,
    BattlegroundStarted, BattlegroundEnded, DungeonStarted, DungeonEnded,
    RaidStarted, RaidEnded, PvPEngaged, PvPEnded, LowHealth,
    CampingStarted, CampingEnded, AuctionStarted, AuctionEnded,
    BankingNeeded, BankingEnded, VendorNeeded, VendingEnded,
    ExplorationStarted, ExploringEnded, TravelRequired, TravelEnded,
    EscapeRequired, EscapeSucceeded, EscapeFailed, EventStarted, EventEnded
}
```

## Key Interfaces

```csharp
// Object Manager
public interface IObjectManager
{
    IWoWLocalPlayer LocalPlayer { get; }
    IEnumerable<IWoWUnit> Units { get; }
    IEnumerable<IWoWPlayer> Players { get; }
    IEnumerable<IWoWGameObject> GameObjects { get; }
}

// Local Player
public interface IWoWLocalPlayer : IWoWPlayer
{
    Position Position { get; }
    int Health { get; }
    int MaxHealth { get; }
    int Mana { get; }
    int MaxMana { get; }
    // ... combat, inventory, spellbook, etc.
}

// Position
public struct Position
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float DistanceTo(Position other);
}
```

## Configuration Keys

**botSettings.json**:
- `DatabaseType` - "sqlite" or "mssql"
- `DatabasePath` - Connection string or file path
- `DiscordBotEnabled` - Enable Discord integration
- `UseTeleportKillswitch` - Stop on teleport detection
- `UseStuckInStateKillswitch` - Stop on state timeout
- `UseStuckInPositionKillswitch` - Stop on position timeout
- `CurrentBotName` - Active bot profile
- `GrindingHotspotId` - Active grinding area

## C++ Interop

**Loader.dll** - Injected into WoW, bootstraps CLR
**FastCall.dll** - x86 fastcall helper for Vanilla client
**Navigation.dll** - Detour/Recast navmesh queries

```csharp
// Example P/Invoke
[DllImport("FastCall.dll", EntryPoint = "BuyVendorItem")]
internal static extern int BuyVendorItem(int itemId, int quantity, ulong vendorGuid, IntPtr functionPtr);
```

## Testing Patterns

```csharp
// Unit tests use xUnit
public class MyServiceTests
{
    [Fact]
    public void MyMethod_WhenCondition_ShouldResult()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

## Common Operations

### Read game memory (legacy in-process)
```csharp
var value = MemoryManager.ReadInt(address);
MemoryManager.WriteBytes(address, bytes);
```

### Execute game function (legacy)
```csharp
Functions.CastSpellById(spellId, targetGuid);
ObjectManager.Player.LuaCall("SelectGossipOption(1)");
```

### Send path request
```csharp
var client = new PathfindingClient(host, port);
var path = await client.FindPathAsync(start, destination);
```

### State transition
```csharp
// Push new state
_botStates.Push(new CombatState(_botStates, _container, target));

// Pop current state (return to previous)
_botStates.Pop();
```

---

## Documentation Cross-References

| Document | Purpose |
|----------|----------|
| [ARCHITECTURE.md](../ARCHITECTURE.md) | High-level system design, BotRunner architecture diagram |
| [PROJECT_STRUCTURE.md](../PROJECT_STRUCTURE.md) | Detailed file/folder layouts for all projects |
| [DEVELOPMENT_GUIDE.md](../DEVELOPMENT_GUIDE.md) | Setup instructions, coding patterns, contribution guide |
| [docs/physics/](../docs/physics/README.md) | PhysX CCT-style physics system documentation |

*This reference is optimized for GitHub Copilot context. For detailed documentation, see links above.*
