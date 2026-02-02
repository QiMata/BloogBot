# WWoW Project Structure

This document provides a detailed breakdown of the project structure, intended for quick reference by developers and GitHub Copilot.

## Root Directory

```
BloogBot/
??? BloogBot.sln                    # Main Visual Studio solution
??? README.md                       # Project overview and setup guide
??? ARCHITECTURE.md                 # High-level architecture documentation
??? PROJECT_STRUCTURE.md            # This file
??? DEVELOPMENT_GUIDE.md            # Developer onboarding guide
?
??? BloogBot.AI/                    # AI coordination module
??? Exports/                        # Core libraries and native components
??? Services/                       # Background services and workers
??? Tests/                          # Unit and integration tests
??? UI/                             # User interface projects
```

## Exports Directory (Core Libraries)

### Managed Libraries (.NET)

#### `Exports/BotRunner/`
**Core bot orchestration library** - provides behavior tree execution, pathfinding integration, and state coordination.

> **Important**: This is the shared core library that both `ForegroundBotRunner` and `BackgroundBotRunner` depend on. It defines the bot logic but is agnostic to how game state is accessed.

```
BotRunner/
??? BotRunner.csproj
??? BotRunnerService.cs             # Main bot orchestration (behavior trees, game loop)
??? WoWNameGenerator.cs             # Random WoW-style name generation
??? Constants/
?   ??? BotContext.cs               # Bot context constants
??? Clients/
    ??? PathfindingClient.cs        # Pathfinding service IPC client
    ??? CharacterStateUpdateClient.cs # State sync with StateManager
```

**Dependencies**: `GameData.Core` (interfaces), `Stateless`, `BehaviourTree`

#### `Exports/WoWSharpClient/`
**Pure C# World of Warcraft protocol implementation** - enables headless bot operation without a game client.

> **Important**: This library implements the WoW authentication and world server protocols. It is used by `BackgroundBotRunner` to create a fully emulated game client.

```
WoWSharpClient/
??? WoWSharpClient.csproj
??? WoWSharpObjectManager.cs        # IObjectManager implementation (packet-based)
??? WoWSharpEventEmitter.cs         # Event dispatching
??? OpCodeDispatcher.cs             # Packet opcode routing
?
??? Client/
?   ??? WoWClient.cs                # Main client coordinator
?   ??? WorldClient.cs              # World server connection
?   ??? AuthLoginClient.cs          # Authentication handling (SRP6)
?   ??? PacketManager.cs            # Packet send/receive
?   ??? PacketParser.cs             # Packet deserialization
?
??? Handlers/                       # Packet handlers by type
?   ??? LoginHandler.cs
?   ??? CharacterSelectHandler.cs
?   ??? ObjectUpdateHandler.cs
?   ??? MovementHandler.cs
?   ??? SpellHandler.cs
?   ??? ChatHandler.cs
?   ??? ...
?
??? Models/                         # Game object models (packet-based state)
?   ??? BaseWoWObject.cs
?   ??? WoWObject.cs
?   ??? WoWUnit.cs
?   ??? WoWPlayer.cs
?   ??? WoWLocalPlayer.cs
?   ??? WoWLocalPet.cs
?   ??? WoWItem.cs
?   ??? WoWContainer.cs
?   ??? WoWGameObject.cs
?   ??? WoWDynamicObject.cs
?   ??? WoWCorpse.cs
?   ??? MovementBlockUpdate.cs
?
??? Movement/
?   ??? MovementController.cs       # Character movement (client-side prediction)
?   ??? SplineController.cs         # Path interpolation for NPCs
?
??? Parsers/
?   ??? MovementPacketHandler.cs
?
??? Screens/                        # Login flow screens
?   ??? LoginScreen.cs
?   ??? RealmSelectScreen.cs
?   ??? CharacterSelectScreen.cs
?
??? Utils/
?   ??? ReaderUtils.cs
?   ??? WorldTimeTracker.cs
?
??? Attributes/
    ??? PacketHandlerAttribute.cs   # Packet handler decoration
```

**Dependencies**: `GameData.Core`, `BotRunner`, `BotCommLayer`, `WowSrp` (SRP6 auth)

#### `Exports/BotCommLayer/`
Inter-process communication using Protobuf sockets.

```
BotCommLayer/
??? BotCommLayer.csproj
??? ProtobufSocketServer.cs         # Sync socket server
??? ProtobufAsyncSocketServer.cs    # Async socket server
??? ProtobufSocketClient.cs         # Socket client
??? Models/
    ??? Communication.cs            # IPC message types
    ??? Database.cs                 # Database DTOs
    ??? Game.cs                     # Game state models
    ??? Pathfinding.cs              # Path request/response
```

#### `Exports/GameData.Core/`
Shared interfaces, enums, and data models.

```
GameData.Core/
??? GameData.Core.csproj
?
??? Interfaces/                     # Core abstractions
?   ??? IObjectManager.cs
?   ??? IWoWObject.cs
?   ??? IWoWUnit.cs
?   ??? IWoWPlayer.cs
?   ??? IWoWLocalPlayer.cs
?   ??? IWoWLocalPet.cs
?   ??? IWoWItem.cs
?   ??? IWoWContainer.cs
?   ??? IWoWGameObject.cs
?   ??? IWoWDynamicObject.cs
?   ??? IWoWCorpse.cs
?   ??? ISpell.cs
?   ??? IWoWEventHandler.cs
?
??? Frames/                         # UI frame interfaces
?   ??? ILoginScreen.cs
?   ??? ICharacterSelectScreen.cs
?   ??? IRealmSelectScreen.cs
?   ??? IGossipFrame.cs
?   ??? IMerchantFrame.cs
?   ??? ITrainerFrame.cs
?   ??? IQuestFrame.cs
?   ??? IQuestGreetingFrame.cs
?   ??? ILootFrame.cs
?   ??? ITradeFrame.cs
?   ??? ITaxiFrame.cs
?   ??? ITalentFrame.cs
?   ??? ICraftFrame.cs
?
??? Enums/
?   ??? Enums.cs                    # Common game enums
?   ??? UpdateFields.cs             # Object update field IDs
?   ??? LiquidType.cs
?
??? Models/
?   ??? Position.cs                 # 3D world position
?   ??? Spell.cs
?   ??? Inventory.cs
?   ??? ItemCacheInfo.cs
?   ??? SkillInfo.cs
?   ??? QuestSlot.cs
?   ??? UpdateMask.cs
?   ??? TargetInfo.cs
?   ??? SpellCastTargets.cs
?   ??? CharacterSelect.cs
?   ??? Realm.cs
?   ??? HighGuid.cs
?
??? Constants/
    ??? Spellbook.cs                # Spell ID constants
    ??? RaceConstants.cs            # Race-specific data
```

#### `Exports/WinProcessImports/`
**Windows API P/Invoke Library** - Provides low-level Windows API bindings for process manipulation and DLL injection.

> **See [Exports/WinImports/README.md](Exports/WinImports/README.md) for detailed documentation.**

```
WinImports/
??? WinProcessImports.csproj    # .NET 8 class library
??? WinProcessImports.cs        # P/Invoke declarations
??? README.md                   # Comprehensive documentation
```

**Purpose**:
- Process management (CreateProcess, OpenProcess, CloseHandle)
- Memory operations (VirtualAllocEx, WriteProcessMemory, VirtualFreeEx)
- Thread control (CreateRemoteThread, WaitForSingleObject)
- Module management (LoadLibrary, GetProcAddress, GetModuleHandle)
- Console management (AllocConsole)

**Key Types**:
| Type | Purpose |
|------|---------|
| `MemoryAllocationType` | MEM_COMMIT, MEM_RESERVE |
| `MemoryProtectionType` | PAGE_EXECUTE_READWRITE |
| `ProcessCreationFlag` | CREATE_DEFAULT_ERROR_MODE |
| `STARTUPINFO` | Process startup configuration |
| `PROCESS_INFORMATION` | New process handles and IDs |

**Consumers**: ForegroundBotRunner (DLL injection), Loader.dll

### Native Libraries (C++)

#### `Exports/Loader/`
**CLR Bootstrapper DLL** - Hosts .NET runtime inside a native process for managed code execution.

> **See [Exports/Loader/README.md](Exports/Loader/README.md) for detailed documentation.**

```
Loader/
??? Loader.vcxproj                  # VS2022 C++ project (v143 toolset)
??? dllmain.cpp                     # DLL entry point, CLR initialization
??? CorError.h                      # CLR error definitions (from Windows SDK)
??? README.md                       # Comprehensive documentation
```

**Purpose**: 
- Bootstraps .NET CLR (4.x) inside target process via `ICLRMetaHost` APIs
- Loads and executes managed assembly entry point (`ExecuteInDefaultAppDomain`)
- Allocates console window for debug output
- Provides 10-second debugger attachment window in Debug builds
- Manages CLR lifecycle (start/stop) tied to DLL load/unload

**Key Configuration** (in `dllmain.cpp`):
```cpp
#define LOAD_DLL_FILE_NAME    L"WoWActivityMember.exe"    // Managed assembly
#define NAMESPACE_AND_CLASS   L"WoWActivityMember.Loader" // Entry point class
#define MAIN_METHOD           L"Load"                      // Entry point method
```

**Build Outputs**: `Bot/{Configuration}/net8.0/Loader.dll` (Win32/x64)

#### `Exports/FastCall/`
**x86 Calling Convention Helper DLL** - Bridges managed C# code to native `__fastcall` game functions.

> **See [Exports/FastCall/README.md](Exports/FastCall/README.md) for detailed documentation.**

```
FastCall/
??? FastCall.vcxproj                 # VS2022 C++ project (v143 toolset)
??? dllmain.cpp                     # Exported wrapper functions
??? stdafx.h                        # Precompiled header
??? stdafx.cpp                      # Precompiled header source
??? targetver.h                     # Windows SDK targeting
??? README.md                       # Comprehensive documentation
```

**Purpose**: 
- Wraps game functions using x86 `__fastcall` convention (registers ECX/EDX)
- Exports `__stdcall` functions callable via P/Invoke from C#
- Used primarily for Vanilla (1.12.1) client where most APIs use `__fastcall`
- Handles `EnumerateVisibleObjects`, `LuaCall`, `GetText`, vendor interactions

**Exported Functions**:
| Export | Purpose |
|--------|--------|
| `EnumerateVisibleObjects` | Populates object manager with game entities |
| `LuaCall` | Executes Lua script in game environment |
| `GetText` | Retrieves Lua variable values |
| `LootSlot` | Loots items from loot window |
| `Intersect` / `Intersect2` | Ray-cast line-of-sight checks |
| `BuyVendorItem` | Purchases from vendors |
| `SellItemByGuid` | Sells items to vendors |
| `GetObjectPtr` | Gets object memory pointer by GUID |

**Build Outputs**: `Bot/{Configuration}/net8.0/FastCall.dll` (Win32 only)

#### `Exports/Navigation/`
**Pathfinding and Physics Simulation** using Detour/Recast with a PhysX CCT-style character controller.

> **See [Exports/Navigation/README.md](Exports/Navigation/README.md) for detailed documentation.**

> **Note**: The physics system implements a PhysX Character Controller Toolkit (CCT) style movement system with three-pass decomposition (UP ? SIDE ? DOWN) for accurate WoW-like movement simulation.

```
Navigation/
??? Navigation.vcxproj
??? DllMain.cpp                     # DLL exports (FindPath, StepPhysicsV2, etc.)
??? Navigation.cpp/.h               # A* pathfinding, line-of-sight, capsule queries
??? MoveMap.cpp/.h                  # MaNGOS-style navmesh tile loading
??? PathFinder.cpp/.h               # Path calculation using Detour
?
??? Geometry & Spatial:
?   ??? MapLoader.cpp/.h            # VMAP/ADT geometry loading
?   ??? AABox.cpp/.h                # Axis-aligned bounding box
?   ??? BIH.cpp/.h/.inl             # Bounding interval hierarchy (spatial index)
?   ??? CoordinateTransforms.h      # WoW ? Detour coordinate conversion
?   ??? CapsuleCollision.h          # Capsule collision primitives
?   ??? IVMapManager.h              # Virtual map manager interface
?
??? Physics System (PhysX CCT-style):
?   ?
?   ??? Core:
?   ?   ??? PhysicsEngine.cpp/.h    # Main entry point (StepV2), singleton, orchestration
?   ?   ??? PhysicsTolerances.h     # Constants (STEP_HEIGHT, GRAVITY, JUMP_VELOCITY, etc.)
?   ?   ??? PhysicsBridge.h         # C++ ? C# interop structures (PhysicsInput/Output)
?   ?
?   ??? Movement Modules:
?   ?   ??? PhysicsThreePass.cpp/.h     # UP/SIDE/DOWN movement decomposition
?   ?   ??? PhysicsCollideSlide.cpp/.h  # Iterative wall collision (collide-and-slide)
?   ?   ??? PhysicsGroundSnap.cpp/.h    # Ground detection, step snapping, depenetration
?   ?   ??? PhysicsMovement.cpp/.h      # Air (falling/jumping) and swim movement
?   ?
?   ??? Helpers:
?   ?   ??? PhysicsHelpers.cpp/.h           # Pure utility functions (speed calc, intent)
?   ?   ??? PhysicsShapeHelpers.h           # Capsule building helpers
?   ?   ??? PhysicsSelectHelpers.h          # Hit selection (earliest walkable, etc.)
?   ?   ??? PhysicsLiquidHelpers.cpp/.h     # Water/lava level evaluation
?   ?   ??? PhysicsDiagnosticsHelpers.cpp/.h # Logging and debug output
?   ?   ??? PhysicsMath.h                   # Math utilities
?   ?
?   ??? Scene Queries:
?       ??? SceneQuery.cpp/.h       # Capsule sweeps against VMAP + ADT geometry
?
??? Detour/                         # Recast/Detour navigation library
?   ??? Include/
?   ?   ??? DetourNavMesh.h
?   ?   ??? DetourNavMeshQuery.h
?   ?   ??? DetourNode.h
?   ?   ??? DetourAlloc.h
?   ?   ??? DetourCommon.h
?   ?   ??? DetourMath.h
?   ?   ??? DetourStatus.h
?   ??? Source/
?       ??? DetourNavMesh.cpp
?       ??? DetourNavMeshQuery.cpp
?       ??? DetourNode.cpp
?       ??? DetourAlloc.cpp
?       ??? DetourCommon.cpp
?
??? g3dlite/                        # G3D math library (Vector3, Ray, AABox, etc.)
?   ??? Include/G3D/
?       ??? Vector3.h
?       ??? Ray.h
?       ??? AABox.h
?       ??? g3dmath.h
?       ??? ...
?
??? Utilities/
    ??? Platform/
        ??? CompilerDefs.h
```

**Physics Pipeline (PhysX CCT alignment)**:
```
StepV2(PhysicsInput, dt) ? PhysicsOutput
  ?
  ??? Overlap Recovery (depenetration from previous tick)
  ?
  ??? Movement Mode Selection:
  ?     ?? Flying ? direct velocity integration
  ?     ?? Swimming ? ProcessSwimMovement()
  ?     ?? Airborne ? ProcessAirMovement() + gravity
  ?     ?? Grounded ? Three-Pass Move:
  ?           ?
  ?           ??? UP PASS: Step-up lift + ceiling check
  ?           ??? SIDE PASS: CollideAndSlide() horizontal
  ?           ??? DOWN PASS: Undo step offset + ground snap
  ?
  ??? Output: new position, velocity, moveFlags, groundZ, liquidZ
```

**Key Physics Constants** (`PhysicsTolerances.h`):
| Constant | Value | Description |
|----------|-------|-------------|
| `STEP_HEIGHT` | 0.6f | Max height for auto-stepping (stairs) |
| `STEP_DOWN_HEIGHT` | 0.5f | Max drop for ground snap |
| `GRAVITY` | 19.29f | WoW gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.96f | Initial jump velocity |
| `DEFAULT_WALKABLE_MIN_NORMAL_Z` | 0.5f | ~60° max walkable slope |
| `WATER_LEVEL_DELTA` | 1.0f | Swim threshold below water surface |

## Services Directory

### `Services/PathfindingService/`
A* pathfinding worker service.

```
PathfindingService/
??? PathfindingService.csproj
??? README.md
??? Program.cs                      # Service entry point
??? PathfindingServiceWorker.cs     # Background service
??? PathfindingSocketServer.cs      # IPC server
??? Properties/
?   ??? Resources.Designer.cs
??? Repository/
    ??? Navigation.cs               # Navmesh repository
    ??? Physics.cs                  # Physics calculations
```

### `Services/StateManager/`
Stack-based finite state machine for bot behavior.

```
StateManager/
??? StateManager.csproj
??? README.md
??? Program.cs
??? StateManagerWorker.cs           # Background worker
??? Settings/
?   ??? StateManagerSettings.cs
??? Listeners/
?   ??? StateManagerSocketListener.cs
?   ??? CharacterStateSocketListener.cs
??? Clients/
?   ??? StateManagerUpdateClient.cs
?   ??? ActivityMemberUpdateClient.cs
?   ??? MangosSOAPClient.cs         # Server SOAP interface
??? Repository/
    ??? ReamldRepository.cs         # Realm data access
    ??? ActorDatabase.cs            # Actor state persistence
```

### `Services/DecisionEngineService/`
Rule-based decision processing and combat AI.

```
DecisionEngineService/
??? DecisionEngineService.csproj
??? README.md
??? DecisionEngine.cs               # Main decision logic
??? DecisionEngineWorker.cs         # Background worker
??? CombatPredictionService.cs      # Combat outcome prediction
??? Listeners/
?   ??? CombatModelServiceListener.cs
??? Clients/
?   ??? CombatModelClient.cs
??? Repository/
    ??? MangosRepository.cs         # Game database access
```

### `Services/PromptHandlingService/`
Automatic in-game dialog handling.

```
PromptHandlingService/
??? PromptHandlingService.csproj
??? README.md
??? [Prompt handlers for various dialog types]
```

### `Services/ForegroundBotRunner/`
**Injectable DLL bot service** - runs inside the WoW game client process via DLL injection.

> **Important**: This implementation uses `Loader.dll` for injection and provides an `IObjectManager` via direct memory manipulation. It requires a running WoW game client.

```
ForegroundBotRunner/
??? ForegroundBotRunner.csproj
??? Program.cs                      # Entry point (called after injection)
??? Loader.cs                       # CLR thread bootstrap for injection
?
??? Statics/
?   ??? ObjectManager.cs            # IObjectManager (memory-based)
?   ??? WoWEventHandler.cs          # In-game event handling
?
??? Objects/                        # Memory-based game object wrappers
?   ??? WoWObject.cs
?   ??? WoWUnit.cs
?   ??? WoWPlayer.cs
?   ??? LocalPlayer.cs              # Local player with memory access
?   ??? LocalPet.cs
?   ??? WoWItem.cs
?   ??? WoWContainer.cs
?   ??? WoWGameObject.cs
?   ??? ItemCacheInfo.cs
?
??? Mem/                            # Memory manipulation utilities
?   ??? Memory.cs                   # Core memory read/write
?   ??? MemoryAddresses.cs          # Static memory addresses
?   ??? Offsets.cs                  # Structure offsets
?   ??? Functions.cs                # WoW internal function calls
?   ??? HackManager.cs              # Memory patches
?   ??? Hack.cs                     # Individual hack definitions
?   ??? Detour.cs                   # Function detouring
?   ??? ThreadSynchronizer.cs       # Main thread execution
?   ??? AntiWarden/
?   ?   ??? WardenDisabler.cs       # Anti-cheat bypass
?   ??? Hooks/
?       ??? SignalEventManager.cs   # Event signal capture
?
??? Frames/
?   ??? DialogFrame.cs              # UI dialog handling
?
??? Resources/
    ??? FastCall.dll                # x86 fastcall helper (embedded)
    ??? FastCall.exp
    ??? FastCall.lib
```

**Dependencies**: `BotRunner`, `GameData.Core`, `Fasm.NET` (assembly)

**Injection Flow**:
1. `Loader.dll` (C++) injected into WoW.exe
2. `Loader.dll` bootstraps .NET CLR
3. `Loader.cs` creates STA thread, calls `Program.Main()`
4. `ObjectManager` enumerates game objects via memory
5. `BotRunnerService` executes behavior trees

### `Services/BackgroundBotRunner/`
**Headless bot service** - runs without a game client using WoW protocol emulation.

> **Important**: This implementation uses `WoWSharpClient` for network protocol and `WoWSharpObjectManager` for `IObjectManager`. No WoW installation required.

```
BackgroundBotRunner/
??? BackgroundBotRunner.csproj
??? BackgroundBotWorker.cs          # BackgroundService implementation
```

**Dependencies**: `BotRunner`, `WoWSharpClient`, `PromptHandlingService`

**Startup Flow**:
1. `BackgroundBotWorker` starts as .NET Worker Service
2. Initializes `WoWClient` (auth + world connection)
3. `WoWSharpObjectManager.Instance.Initialize()` sets up packet-based state
4. `BotRunnerService` executes behavior trees
5. `PromptHandlingService` handles automated dialogs

**Configuration** (via `IConfiguration`):
- `PathfindingService:IpAddress/Port` - Pathfinding service endpoint
- `CharacterStateListener:IpAddress/Port` - StateManager endpoint
- `RealmEndpoint:IpAddress` - WoW server address
- `Ollama:BaseUri/Model` - AI prompt handling

## BloogBot.AI Module

Advanced AI coordination using Semantic Kernel.

```
BloogBot.AI/
??? BloogBot.AI.csproj
?
??? Annotations/
?   ??? ActivityPluginAttribute.cs  # Plugin metadata attribute
?
??? Semantic/
?   ??? KernelCoordinator.cs        # SK kernel management
?   ??? PluginCatalog.cs            # Activity-based plugin loading
?   ??? DictionaryExtensions.cs
?
??? States/
?   ??? BotActivity.cs              # 25+ activity types enum
?   ??? Trigger.cs                  # State transition triggers
?
??? StateMachine/
    ??? BotActivityStateMachine.cs  # Stateless-based FSM
```

### Bot Activities (BotActivity enum)
```csharp
Resting, Questing, Grinding, Professions, Talenting, Equipping,
Trading, Guilding, Chatting, Helping, Mailing, Partying,
RolePlaying, Combat, Battlegrounding, Dungeoning, Raiding,
WorldPvPing, Camping, Auction, Banking, Vending, Exploring,
Traveling, Escaping, Eventing
```

## UI Directory

### `UI/StateManagerUI/`
WPF desktop application for bot control.

```
StateManagerUI/
??? StateManagerUI.csproj
??? App.xaml/.cs                    # Application entry
??? MainWindow.xaml/.cs             # Main window
??? AssemblyInfo.cs
??? BasicLogger.cs
??? Views/
?   ??? StateManagerViewModel.cs    # MVVM view model
??? Handlers/
?   ??? CommandHandler.cs           # ICommand implementation
??? Converters/
    ??? ValueConverter.cs
    ??? GreaterThanZeroToBooleanConverter.cs
```

### `UI/WWoW.Systems/`
.NET Aspire orchestration for containerized deployment.

```
WWoW.Systems/
??? WWoW.Systems.AppHost/
?   ??? WWoW.Systems.AppHost.csproj
?   ??? Program.cs                  # Aspire host configuration
?   ??? WowServerConfig.cs          # Container configuration
?
??? WWoW.Systems.ServiceDefaults/
    ??? WWoW.Systems.ServiceDefaults.csproj
    ??? [Shared service configuration]
```

**Aspire Host Orchestrates**:
- MySQL container for WoW database
- WoW Vanilla server container (MaNGOS)
- Service endpoints and volume mounts

## Tests Directory

```
Tests/
??? PathfindingService.Tests/
?   ??? PathfindingService.Tests.csproj
??? BotRunner.Tests/
?   ??? BotRunner.Tests.csproj
??? PromptHandlingService.Tests/
?   ??? PromptHandlingService.Tests.csproj
??? WoWSharpClient.Tests/
    ??? WoWSharpClient.Tests.csproj
```

## Key File Locations Summary

| Need | Location |
|------|----------|
| Bot behavior logic | `Services/StateManager/` |
| WoW protocol handling | `Exports/WoWSharpClient/` |
| Navigation/pathfinding | `Exports/Navigation/Navigation.cpp`, `Services/PathfindingService/` |
| Physics simulation | `Exports/Navigation/PhysicsEngine.cpp` + Physics* modules |
| Physics constants | `Exports/Navigation/PhysicsTolerances.h` |
| Scene geometry queries | `Exports/Navigation/SceneQuery.cpp` |
| Game object models | `Exports/GameData.Core/Models/` |
| AI state machine | `BloogBot.AI/StateMachine/` |
| IPC communication | `Exports/BotCommLayer/` |
| Native DLL injection | `Exports/Loader/` |
| WoW API calls (Vanilla) | `Exports/FastCall/` |
| UI application | `UI/StateManagerUI/` |
| Container orchestration | `UI/WWoW.Systems/WWoW.Systems.AppHost/` |

## Build Output

All projects output to a common `Bot/` folder for unified deployment:
```xml
<OutputPath>..\..\Bot</OutputPath>
```

## Platform Requirements

| Component | Platform |
|-----------|----------|
| C# Projects | `net8.0` / `net8.0-windows` |
| C++ Projects | Windows x86/x64 |
| WPF UI | `net8.0-windows10.0.22621.0` |
| Native DLLs | MSVC v142+ toolset |
