# WWoW (Westworld of Warcraft) - GitHub Copilot Context

## Project Overview

WWoW is a simulation platform transforming a World of Warcraft-like server into a living world populated by AI-driven bots. The goal is creating AI-controlled characters indistinguishable from human players, serving as a testbed for agent-based AI research in complex game environments.

## Brand Name

- **Official Name**: WWoW (Westworld of Warcraft)
- **Legacy Name**: BloogBot (deprecated, migrating away from this name)
- Use "WWoW" in all new documentation and comments
- The project simulates autonomous agents in a WoW-style game world

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET 8.0 | LTS |
| UI Framework | WPF (Windows) | - |
| Orchestration | .NET Aspire | 9.0 |
| State Machine | Stateless | 5.17+ |
| AI/ML | Microsoft Semantic Kernel | 1.54+ |
| Serialization | Protobuf-net | - |
| Database | SQLite / SQL Server | - |
| Native Code | C++ (MSVC v143) | C++20 |
| Navigation | Detour/Recast | - |
| Build System | MSBuild / VS 2022 | - |

## Solution Architecture

```
/Exports/           - Core shared libraries (C# and C++ DLLs)
/Services/          - Background worker services
/UI/                - User interface projects (WPF, Aspire)
/Tests/             - Unit and integration tests
/BloogBot.AI/       - Semantic Kernel AI integration
```

### Project Navigation Guide

The workspace is organized into logical layers. Understanding these layers helps navigate the codebase efficiently:

| Directory | Purpose | Key Entry Points |
|-----------|---------|------------------|
| `Exports/` | Shared libraries consumed by services and UI | `GameData.Core` (interfaces), `BotCommLayer` (IPC), `WoWSharpClient` (protocol) |
| `Services/` | Background worker processes | `PathfindingService`, `StateManager`, `DecisionEngineService` |
| `UI/` | User-facing applications | `WWoW.Systems.AppHost` (Aspire orchestrator), `StateManagerUI` (WPF) |
| `Tests/` | Test projects mirroring source | `{ProjectName}.Tests` naming convention |

**Dependency Flow**: `GameData.Core` ? `BotCommLayer` ? `BotRunner` ? Services ? UI

**Native C++ Projects** (in `Exports/`):
- `Navigation/` - Physics simulation and Detour pathfinding (Navigation.dll)
- `Loader/` - CLR bootstrapper for DLL injection (Loader.dll)  
- `FastCall/` - x86 calling convention helpers (FastCall.dll)

**Aspire Orchestration**: The `WWoW.Systems.AppHost` project in `UI/WWoW.Systems/` orchestrates all services. Register new services in its `Program.cs`.

### Key Projects

| Project | Purpose |
|---------|---------|
| `BotRunner` | Core orchestration library - behavior trees, pathfinding clients |
| `WoWSharpClient` | Pure C# WoW protocol implementation for headless bots |
| `BotCommLayer` | Protobuf IPC for inter-service communication |
| `GameData.Core` | Shared interfaces (IObjectManager, IWoWUnit, etc.) |
| `PathfindingService` | A* pathfinding using Detour navmesh |
| `StateManager` | Stack-based finite state machine |
| `Navigation.dll` | C++ physics and pathfinding native library |
| `Loader.dll` | C++ CLR bootstrapper for DLL injection |
| `FastCall.dll` | C++ x86 calling convention helper |

## Coding Conventions

### C# Style

```csharp
// Use file-scoped namespaces
namespace WWoW.Services.StateManager;

// Prefer primary constructors for DI (C# 12)
public class MyService(ILogger<MyService> logger, IOptions<Settings> options)
{
    // Fields derived from constructor parameters are implicitly readonly
}

// Use expression-bodied members where appropriate
public int Health => _healthValue;

// Async methods should end with "Async"
public async Task<Path> CalculatePathAsync(Position start, Position end);

// Use init-only properties for DTOs
public record PathResult
{
    public required IReadOnlyList<Position> Waypoints { get; init; }
    public float TotalDistance { get; init; }
}
```

### Naming Conventions

- **Interfaces**: Prefix with `I` (e.g., `IWoWUnit`, `IObjectManager`)
- **State classes**: Suffix with `State` (e.g., `CombatState`, `RestState`)
- **Service classes**: Suffix with `Service` (e.g., `PathfindingService`)
- **Client classes**: Suffix with `Client` (e.g., `PathfindingClient`)
- **Handler classes**: Suffix with `Handler` (e.g., `PacketHandler`)
- **Worker services**: Suffix with `Worker` (e.g., `BackgroundBotWorker`)

### Project References

- Use `<ProjectReference>` for solution-internal dependencies
- Keep dependency flow: Core ? Services ? UI (no reverse references)
- `GameData.Core` should have minimal external dependencies

## Key Design Patterns

### State Machine Pattern (StateManager)

```csharp
// States are pushed/popped on a stack
_stateStack.Push(new CombatState(_container));

// State interface
public interface IBotState
{
    void Update();
}
```

### Dependency Injection Pattern

```csharp
// Services use IDependencyContainer for game-specific factories
public interface IDependencyContainer
{
    Func<Stack<IBotState>, IDependencyContainer, IBotState> CreateRestState { get; }
    BotSettings BotSettings { get; }
    IEnumerable<Hotspot> Hotspots { get; }
}
```

### Protobuf Communication (BotCommLayer)

```csharp
// Request/Response pattern over TCP sockets
var server = new ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(port);
server.OnMessageReceived += HandleRequest;
```

## Worker Service Pattern

Background services inherit from `BackgroundService`:

```csharp
public class MyWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Main loop logic
            await Task.Delay(100, stoppingToken);
        }
    }
}
```

## Game Object Hierarchy

```
IWoWObject (base)
??? IWoWUnit (NPCs, creatures)
?   ??? IWoWPlayer (other players)
?       ??? IWoWLocalPlayer (controlled character)
??? IWoWItem
??? IWoWContainer (bags)
??? IWoWGameObject (world objects)
??? IWoWDynamicObject
??? IWoWCorpse
```

## Physics Constants (Navigation.dll)

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.29 | WoW gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.96 | Initial jump velocity |
| `STEP_HEIGHT` | 2.125 | Max auto-step height |
| `STEP_DOWN_HEIGHT` | 4.0 | Max ground snap distance |

## Supported WoW Versions

| Version | Build | Codename |
|---------|-------|----------|
| 1.12.1 | 5875 | Vanilla |
| 2.4.3 | 8606 | TBC |
| 3.3.5a | 12340 | WotLK |

## Common Tasks

### Adding a New Service

1. Create project under `/Services/` using Worker SDK
2. Add `<ProjectReference>` to `GameData.Core` and `BotCommLayer`
3. Implement `BackgroundService` pattern
4. Register in `WWoW.Systems.AppHost/Program.cs`
5. Add README.md with architecture documentation

### Adding a New State

1. Create class implementing `IBotState`
2. Place in appropriate namespace (SharedStates or bot-specific)
3. Suffix class name with `State`
4. Implement `Update()` method with single-responsibility logic

### Adding Protobuf Messages

1. Edit `.proto` files in `BotCommLayer/Models/ProtoDef/`
2. Run `protocsharp.bat` to regenerate C# classes
3. Update service consumers to use new message types

## Testing Guidelines

- Use xUnit for unit tests
- Test projects mirror source structure: `Tests/{ProjectName}.Tests/`
- Mock `IObjectManager` and game interfaces for unit tests
- Integration tests may use real PathfindingService with test navmesh data

## Documentation Standards

- Every project MUST have a `README.md`
- Document public APIs with XML comments
- Include architecture diagrams in ASCII art format
- Reference `ARCHITECTURE.md` from project READMEs

## Security Notes

- This project is for private servers and AI research only
- Do not use on official Blizzard servers
- Native components (Loader, FastCall) perform memory manipulation
- Warden bypass is tailored to specific private server implementations

## AI Assistant Guidelines

### Prefer Direct File Edits Over Scripts

When making changes to this codebase:

- **DO NOT** use terminal commands (`run_command_in_terminal`) unless absolutely necessary
- **DO NOT** generate Python scripts to perform file modifications or analysis
- **DO** use direct file editing tools (`replace_string_in_file`, `multi_replace_string_in_file`, `create_file`)
- **DO** use search tools (`code_search`, `file_search`, `get_file`) for analysis

**Rationale**: Direct file edits are:
1. More transparent and reviewable
2. Less error-prone than script execution
3. Consistent with the workspace's build system (MSBuild/VS 2022)
4. Easier to track in version control

**Exceptions** (when terminal commands ARE appropriate):
- Running `msbuild` or `dotnet build` to verify compilation
- Running unit tests with `dotnet test`
- Git operations explicitly requested by the user
- Package restore operations

### Prompt Generation Guidelines

When generating prompts, instructions, or text intended for copying to clipboard:

- **DO NOT** use markdown codeblocks (triple backticks) around generated prompts
- **DO** use plain text formatting for prompts to avoid clipboard and copy/paste issues
- **DO** use bullet points, numbered lists, or simple indentation for structure
- **DO** clearly separate the prompt content from surrounding explanatory text

**Rationale**: Codeblocks can cause issues when users copy/paste prompts to other tools or contexts, as the formatting may not transfer correctly or may include unwanted characters.

---

*See ARCHITECTURE.md for complete system documentation.*
