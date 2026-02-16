# WWoW Development Guide

This guide provides developer onboarding information for contributing to the Westworld of Warcraft (WWoW) project.

## Prerequisites

### Required Software
- **Visual Studio 2022** (Community or higher)
  - Workloads: .NET Desktop Development, Desktop Development with C++
  - Individual Components: .NET 8.0 SDK, Windows 10/11 SDK
- **Git** for version control
- **WoW Game Client** (1.12.1, 2.4.3, or 3.3.5a) for testing

### Optional Tools
- **Cheat Engine** for memory inspection/debugging
- **Wireshark** with WoW protocol dissector for packet analysis
- **SQL Server Management Studio** or **DBeaver** for database access

## Getting Started

### 1. Clone and Build

```powershell
git clone https://github.com/QiMata/BloogBot.git
cd BloogBot
```

Open `BloogBot.sln` in Visual Studio 2022.

### 2. Restore Dependencies

```powershell
dotnet restore
```

Or use Visual Studio's NuGet Package Manager.

### 3. Build Solution

Set configuration to **Debug** or **Release**, then build:
- `Ctrl+Shift+B` or `Build > Build Solution`

Ensure all projects build successfully, especially:
- C++ projects: `Loader`, `FastCall`, `Navigation`
- Core C# projects: `WoWSharpClient`, `BotRunner`, `StateManager`

### 4. Configuration

Create/edit configuration files in the output `Bot/` directory:

**bootstrapperSettings.json**:
```json
{
  "PathToWoW": "C:\\Games\\WoW-1.12.1\\WoW.exe"
}
```

**botSettings.json**:
```json
{
  "DatabaseType": "sqlite",
  "DatabasePath": "db.db",
  "DiscordBotEnabled": false,
  "UseTeleportKillswitch": true,
  "UseStuckInStateKillswitch": true,
  "UseStuckInPositionKillswitch": true
}
```

## Project Architecture Quick Reference

### Layer Overview

```
UI Layer          ? User interfaces, Aspire orchestration
Services Layer    ? Background workers (Pathfinding, StateManager, etc.)
Exports Layer     ? Core libraries, native DLLs
BloogBot.AI       ? Advanced AI coordination
```

### Key Projects

| When working on... | Look at... |
|-------------------|------------|
| Bot behavior/states | `Services/StateManager/` |
| Bot behavior trees | `Exports/BotRunner/BotRunnerService.cs` |
| Network protocol (headless) | `Exports/WoWSharpClient/` |
| Memory access (injected) | `Services/ForegroundBotRunner/Mem/`, `/Statics/` |
| Game object models (shared) | `Exports/GameData.Core/` |
| Pathfinding | `Services/PathfindingService/`, `Exports/Navigation/` |
| AI decisions | `BloogBot.AI/`, `Services/DecisionEngineService/` |
| IPC messaging | `Exports/BotCommLayer/` |
| Native injection | `Exports/Loader/`, `Exports/FastCall/` |
| Headless bot setup | `Services/BackgroundBotRunner/` |

> **See [ARCHITECTURE.md](ARCHITECTURE.md#botrunner-architecture) for the BotRunner architecture diagram.**

## Working with BotRunner Implementations

The bot system has two `IObjectManager` implementations. Choose based on your scenario:

### ForegroundBotRunner (DLL Injection)

**When to use**: Development, debugging, testing with visual feedback.

**Setup**:
1. Build the solution (Release or Debug)
2. Run `Bootstrapper` to launch WoW with injection
3. `Loader.dll` injects and bootstraps .NET CLR
4. Bot code runs inside WoW.exe process

**Adding memory-based functionality**:
```csharp
// In Services/ForegroundBotRunner/Statics/ObjectManager.cs
public void MyNewMethod()
{
    // Read from WoW memory
    var value = MemoryManager.ReadInt(MemoryAddresses.SomeAddress);
    
    // Call WoW function via Lua
    Functions.LuaCall("SomeFunction()");
    
    // Execute on main thread (required for most WoW calls)
    ThreadSynchronizer.RunOnMainThread(() => {
        Functions.SomeNativeCall();
    });
}
```

**Key classes**:
- `MemoryManager` - Read/write process memory
- `Functions` - WoW internal function wrappers
- `ThreadSynchronizer` - Main thread execution queue
- `ObjectManager` - `IObjectManager` implementation

### BackgroundBotRunner (Headless)

**When to use**: Server farms, CI testing, resource-efficient operation.

**Setup**:
1. Configure `appsettings.json` with server endpoints
2. Run as .NET Worker Service
3. No WoW client needed

**Adding packet-based functionality**:
```csharp
// In Exports/WoWSharpClient/WoWSharpObjectManager.cs
public void MyNewMethod()
{
    // Send packet to server
    _woWClient.SendPacket(Opcode.CMSG_SOMETHING, payload);
    
    // Access packet-based state
    var position = Player.Position;
}

// Add packet handler in Exports/WoWSharpClient/Handlers/
[PacketHandler(OpCode.SMSG_SOMETHING)]
public void HandleSomething(PacketReader reader, WoWSharpObjectManager om)
{
    var data = reader.ReadInt32();
    // Update object manager state
}
```

**Key classes**:
- `WoWClient` - Auth + world server connections
- `WoWSharpObjectManager` - `IObjectManager` implementation
- `PacketManager` - Send/receive packets
- `MovementController` - Client-side movement prediction

### Shared Code (Both Implementations)

Both implementations use:
- `BotRunnerService` - Add behavior tree actions here
- `GameData.Core` interfaces - Extend `IObjectManager`, `IWoWUnit`, etc.
- `PathfindingClient` - Pathfinding service communication

```csharp
// In Exports/BotRunner/BotRunnerService.cs
private IBehaviourTreeNode BuildMyNewSequence(int param) => new BehaviourTreeBuilder()
    .Sequence("My New Sequence")
        .Condition("Can Do Thing", time => _objectManager.SomeCondition())
        .Do("Do Thing", time => {
            _objectManager.DoSomething(param);
            return BehaviourTreeStatus.Success;
        })
    .End()
    .Build();
```

## Common Development Tasks

### Adding a New Bot State

1. **Create the state class** in `Services/StateManager/` or the relevant bot module:

```csharp
public class MyNewState : IBotState
{
    private readonly Stack<IBotState> _botStates;
    private readonly IDependencyContainer _container;

    public MyNewState(Stack<IBotState> botStates, IDependencyContainer container)
    {
        _botStates = botStates;
        _container = container;
    }

    public void Update()
    {
        // State logic here
        
        // Transition to another state:
        // _botStates.Push(new NextState(_botStates, _container));
        
        // Or pop this state:
        // _botStates.Pop();
    }
}
```

2. **Register the state** in the dependency container if it needs factory creation.

3. **Push the state** from the StateManager or another state when conditions warrant.

### Adding a New Packet Handler

1. **Create handler** in `Exports/WoWSharpClient/Handlers/`:

```csharp
public class MyPacketHandler
{
    [PacketHandler(OpCode.SMSG_MY_PACKET)]
    public void HandleMyPacket(PacketReader reader, WoWSharpObjectManager objectManager)
    {
        // Parse packet data
        var value = reader.ReadInt32();
        
        // Update state or trigger events
    }
}
```

2. **Register the handler** with the `OpCodeDispatcher`.

### Adding a New Game Model Interface

1. **Define interface** in `Exports/GameData.Core/Interfaces/`:

```csharp
public interface IMyGameObject : IWoWObject
{
    int MyProperty { get; }
    void MyMethod();
}
```

2. **Implement** in `Exports/WoWSharpClient/Models/`:

```csharp
public class MyGameObject : WoWObject, IMyGameObject
{
    public int MyProperty => // Read from update fields
    public void MyMethod() => // Implementation
}
```

### Adding a New Activity (BloogBot.AI)

1. **Add to BotActivity enum** in `BloogBot.AI/States/BotActivity.cs`:

```csharp
public enum BotActivity
{
    // Existing...
    MyNewActivity
}
```

2. **Add relevant triggers** in `BloogBot.AI/States/Trigger.cs`:

```csharp
public enum Trigger
{
    // Existing...
    MyNewActivityStarted,
    MyNewActivityEnded
}
```

3. **Configure state machine** in `BotActivityStateMachine.cs`:

```csharp
void ConfigureMyNewActivity() =>
    _sm.Configure(BotActivity.MyNewActivity)
        .OnEntry(ctx => Logger.LogInformation("Starting my new activity"))
        .PermitDynamic(Trigger.MyNewActivityEnded, DecideNextActiveState);
```

### Working with Navigation (C++)

The `Exports/Navigation/` project uses Detour/Recast for navmesh queries.

**Key files**:
- `MapLoader.cpp` - Loads `.mmap` navmesh files
- `PhysicsEngine.cpp` - Physics simulation
- `PhysicsCollideSlide.cpp` - Collision response

**Building**:
- Requires Windows SDK and MSVC toolset
- Output: `Navigation.dll`

### Working with the Protocol (WoWSharpClient)

**Packet flow**:
```
AuthLoginClient ? Authentication
WorldClient ? Game world communication
PacketManager ? Send/receive packets
OpCodeDispatcher ? Route to handlers
```

**Adding new protocol support**:
1. Define OpCode constants
2. Create packet reader/writer
3. Implement handler method
4. Register with dispatcher

## Testing

### Running Unit Tests

```powershell
dotnet test
```

Or use Visual Studio Test Explorer.

### Manual Testing

1. Start WoW client (private server)
2. Run `ForegroundBotRunner` with debugger attached
3. Log in and observe bot behavior
4. Use console output for debugging

### Debugging Tips

**Attaching to WoW process**:
1. Run `Bootstrapper` to launch WoW with injection
2. In Visual Studio: `Debug > Attach to Process > WoW.exe`
3. Set breakpoints in managed code

**C++ debugging**:
- Build in Debug configuration
- `Loader.dll` pauses 10 seconds on load for debugger attachment
- Use Visual Studio's native debugging

**Memory inspection**:
- Use Cheat Engine to verify addresses
- Cross-reference with `MemoryAddresses.cs` constants

## Code Style Guidelines

### C# Conventions
- PascalCase for public members
- camelCase with `_` prefix for private fields
- Async methods end with `Async`
- Use nullable reference types

### State Classes
- Name with `State` suffix (e.g., `CombatState`)
- Single responsibility per state
- Use dependency injection via constructor

### Handlers
- Name with `Handler` suffix
- Use attributes for registration where applicable
- Keep methods focused and small

### Comments
- Match existing file style
- Document non-obvious logic
- Avoid redundant comments

## Common Issues and Solutions

### Build Errors

**"Cannot find Windows SDK"**
- Install Windows 10/11 SDK via Visual Studio Installer
- Check `Navigation.vcxproj` for SDK version requirements

**"Missing .NET targeting pack"**
- Install .NET 8.0 SDK
- Ensure `TargetFramework` matches installed SDK

### Runtime Errors

**"Injection failed"**
- Run Visual Studio as Administrator
- Disable antivirus temporarily
- Check `PathToWoW` in settings

**"Wrong game version"**
- Verify WoW client version matches supported builds
- Check `MemoryAddresses` for version-specific offsets

**"Database connection failed"**
- Ensure SQLite is accessible
- Check connection string in settings
- Verify file permissions

## Contributing

### Workflow

1. **Fork** the repository
2. **Create branch** for your feature: `git checkout -b feature/my-feature`
3. **Make changes** following code style guidelines
4. **Test** thoroughly
5. **Commit** with descriptive messages
6. **Push** and create Pull Request

### Pull Request Guidelines

- Reference related issues
- Describe changes clearly
- Include test results
- Keep PRs focused (one feature/fix per PR)

### Areas Seeking Contributions

- **Class Profiles**: Combat rotations for different WoW classes
- **Questing AI**: Quest log parsing and objective completion
- **Social AI**: Chat responses, group coordination
- **Navigation**: Dynamic obstacle avoidance
- **Documentation**: Tutorials, API docs

## Resources

### Internal Documentation
- `README.md` - Project overview
- `ARCHITECTURE.md` - System architecture
- `PROJECT_STRUCTURE.md` - Directory layout
- `Services/*/README.md` - Service-specific docs
- `Exports/README.md` - Core engine overview

### External References
- [Recast/Detour](https://github.com/recastnavigation/recastnavigation) - Navigation library
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel) - AI orchestration
- [Stateless](https://github.com/dotnet-state-machine/stateless) - State machine library
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) - Cloud-native orchestration

### WoW Protocol Resources
- [WoWDev Wiki](https://wowdev.wiki/) - Protocol documentation
- [MaNGOS](https://www.getmangos.eu/) - Server implementation reference
- [TrinityCore](https://www.trinitycore.org/) - Alternative server reference

## Getting Help

- **GitHub Issues**: Bug reports, feature requests
- **Discussions**: Questions, ideas
- **Code Review**: Learn from PR feedback

---

*Welcome to the WWoW development team! Whether you're here to improve AI behavior, add new features, or fix bugs, your contributions help build a more realistic virtual world.*
