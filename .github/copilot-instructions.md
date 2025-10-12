# BloogBot/WestworldOfWarcraft AI Instructions

BloogBot is a sophisticated World of Warcraft automation platform that creates AI-driven characters indistinguishable from human players. This is a distributed multi-service architecture with process injection, AI decision-making, and advanced pathfinding capabilities.

## Architecture Overview

**Core Structure**: Hybrid architecture combining process injection (`ForegroundBotRunner`) with distributed services (`BackgroundBotRunner`, `StateManager`, `PathfindingService`). The system operates on legacy WoW clients (1.12.1, 2.4.3, 3.3.5a) using C++/C# interop.

**Key Components**:
- `Exports/`: Core libraries (BotRunner, WoWSharpClient, Navigation C++ modules)  
- `Services/`: Distributed .NET 8 Worker Services with TCP socket communication
- `BotProfiles/`: Class-specific AI logic (MEF plugins implementing `IBot`)
- `BloogBot.AI/`: Semantic Kernel integration for advanced AI decision-making
- `UI/`: WPF management interfaces and Blazor web dashboards

## Development Patterns

**Bot Profile Development**: Create new class behaviors by implementing `IBot` in `BotProfiles/` with MEF `[Export(typeof(IBot))]`. Each profile defines task factories (`CreateRestTask`, `CreatePvERotationTask`, etc.) and uses state machine patterns for combat logic.

**Service Communication**: Services use TCP sockets with custom protocols. `StateManager` orchestrates on port 8088, `PathfindingService` on 5000, character state listeners on 5002. Use `ActivityMemberState` for character state synchronization.

**Memory Management**: `ForegroundBotRunner` uses direct memory access patterns with `ObjectManager` for game object enumeration. Always use position validation and range checking for safety.

## Build & Test Workflow

**Setup**: Run `setup.ps1` to download/configure WoW client files. Build requires VS 2022 with C++ workloads for native components.

**Testing**: Use xUnit for unit tests. Integration tests in `Tests/` verify pathfinding and networking. Run `PathfindingService` before testing navigation components.

**Configuration**: Services use `appsettings.json` + environment-specific configs. Database can be SQLite (default) or SQL Server. Configure WoW paths in `bootstrapperSettings.json`.

## Critical Implementation Details

**Process Injection**: `Loader.dll` (C++) bootstraps .NET runtime inside WoW process. Use `Bootstrapper.exe` to launch with injection. Anti-cheat bypass requires specific memory protection patterns.

**Pathfinding**: Uses Detour navigation meshes (`.mmtile` files) with sub-5ms A* performance. Always validate map data exists before pathfinding calls.

**AI Integration**: `BloogBot.AI` uses Semantic Kernel with activity-based plugin loading. Plugins activate based on current `BotActivity` state (Questing, Combat, Trading, etc.).

**Error Handling**: Services must handle WoW client disconnections gracefully. Use circuit breaker patterns for external service calls (MaNGOS SOAP, Ollama AI).

## Key Files for Understanding
- `BotProfiles/MageFrost/Tasks/PvERotationTask.cs` - Combat AI implementation patterns
- `Services/StateManager/StateManagerWorker.cs` - Service orchestration example  
- `Exports/BotRunner/Interfaces/IBot.cs` - Plugin contract definition
- `CODING_STANDARDS.md` - Project-specific C# conventions and architecture guidelines