# BloogBot Architecture

## Overview

BloogBot is a comprehensive World of Warcraft 1.12.1 automation system with two complementary client approaches:

1. **Injected Client (ForegroundBotRunner)** — Runs inside the real WoW.exe process. Reads memory, calls native functions, automates gameplay. This is the reference implementation and the source of ground-truth data.
2. **Headless Client (WoWSharpClient)** — Standalone process (no graphics/audio). Implements the WoW protocol from scratch. Connects to the server independently, moves via the centralized PhysicsEngine, and plays autonomously.

**The pipeline**: Record data from the injected client → calibrate the PhysicsEngine → validate the headless client against real data → bring the headless client to feature parity.

## High-Level Architecture

```
┌──────────────────────┐                         ┌─────────────────────┐
│  ForegroundBotRunner  │──── GetPath ──────────→│                     │
│  (injected client)    │     LineOfSight         │  PathfindingService │
├──────────────────────┤                    ┌───→│  (single process)   │
│  WoWSharpClient 1     │──── PhysicsStep ──┘    │                     │
│  (headless client)    │     GetPath             │  Navigation.dll     │
├──────────────────────┤     LineOfSight          │  (maps loaded once) │
│  WoWSharpClient N     │────────────────────────→│                     │
└──────────────────────┘     TCP/protobuf:5001    └─────────────────────┘
```

### Core Components

1. **WoWStateManager**: Orchestrator managing bot lifecycle, WoW process spawning, and DLL injection
2. **PathfindingService**: Navigation, pathfinding, and physics simulation using Navigation.dll
3. **ForegroundBotRunner**: Injection framework running inside WoW.exe — memory reading, native function calls, GrindBot state machine
4. **WoWSharpClient**: Headless WoW protocol client — auth, world server, movement, combat
5. **DecisionEngineService**: ML-powered rule engine for adaptive bot behavior
6. **BotCommLayer**: Protobuf-based inter-service communication

## Service Architecture

### WoWStateManager Service
- **Purpose**: Bot lifecycle management — spawns WoW processes, injects DLLs, manages state
- **Technology**: .NET 8 Worker Service
- **Key Features**:
  - WoW process spawning and monitoring
  - Loader.dll injection for CLR hosting
  - Character state tracking via protobuf snapshots
  - Login automation and disconnect recovery
- **Location**: `Services/WoWStateManager/`

### PathfindingService
- **Purpose**: Navigation, pathfinding, and physics simulation using game world data
- **Technology**: .NET 8 Background Service with unsafe code for performance
- **Key Features**:
  - A* pathfinding on navigation meshes (via Navigation.dll)
  - Physics engine for movement simulation (StepPhysicsV2)
  - Line-of-sight and scene queries
  - Uses precomputed move maps from `Bot/mmaps` directory
  - TCP/protobuf server on port 5001
- **Location**: `Services/PathfindingService/`

### DecisionEngineService
- **Purpose**: ML-powered decision engine for adaptive bot behavior
- **Technology**: .NET 8 with ML.NET (SDCA Maximum Entropy)
- **Key Features**:
  - Multiclass classification of optimal actions from game state
  - Trained on ActivitySnapshot protobuf data (binary `.bin` files)
  - SQLite persistence for trained models and weights
  - File system watcher for automatic retraining
  - Health-based healing, threat-based targeting, AoE for groups
- **Location**: `Services/DecisionEngineService/`

### PromptHandlingService
- **Purpose**: AI/LLM integration for intelligent decision support
- **Technology**: .NET 8 with multi-provider support
- **Key Features**:
  - Multi-provider: Azure OpenAI, OpenAI, Ollama (local), Fake (testing)
  - Predefined prompt functions: IntentionParser, GMCommandConstruction, CharacterSkillPrioritization, ConfigEditor
  - Response caching to reduce API calls
  - Chat history management across sessions
- **Location**: `Services/PromptHandlingService/`

### ForegroundBotRunner
- **Purpose**: Injected bot logic running inside WoW.exe process (gold standard)
- **Technology**: .NET 8 (injected via Loader.dll CLR hosting)
- **Key Features**:
  - Direct memory reading/writing for game state access
  - Object Manager enumeration (units, players, items, game objects, containers)
  - 14 UI frame handlers (Login, CharSelect, RealmSelect, Gossip, Quest, Loot, Merchant, Craft, Trainer, Talent, Taxi, Trade, QuestGreeting, Dialog)
  - Signal event hooks (LEARNED_SPELL, CHAT_MSG_SKILL, UI_ERROR, etc.)
  - Packet capture (send/recv hooks on NetClient::Send/ProcessMessage)
  - Connection state machine (Disconnected → Authenticating → CharSelect → InWorld → Transferring)
  - Anti-Warden protection (module/memory scanning hooks)
  - Movement recording system (JSON/protobuf output)
  - Thread synchronization via PostMessage(WM_USER) for safe main-thread execution
- **Location**: `Services/ForegroundBotRunner/`

### BackgroundBotRunner
- **Purpose**: Headless bot execution without game client
- **Technology**: .NET 8 Worker Service using WoWSharpClient
- **Key Features**:
  - Pure C# WoW protocol implementation (no WoW.exe needed)
  - Behavior tree orchestration via BotRunner's IBotTask system
  - 30 network client components (combat, vendor, quest, guild, party, auction, bank, mail, etc.)
  - Horizontal scalability (multiple instances for multi-boxing)
  - State synchronization with StateManager via protobuf snapshots
- **Location**: `Services/BackgroundBotRunner/`

## Core Libraries

### WoWSharpClient
- **Purpose**: Headless WoW 1.12.1 protocol client (no graphics/audio)
- **Technology**: .NET 8 with unsafe code
- **Key Features**:
  - SRP6 authentication and realm list
  - World server connection with header encryption
  - Full CMSG/SMSG packet codec (937+ protocol tests passing)
  - Object manager from SMSG_UPDATE_OBJECT parsing
  - Movement controller and packet sending
  - Network client components for combat, vendor, quest, guild, etc.
- **Location**: `Exports/WoWSharpClient/`

### GameData.Core
- **Purpose**: Shared game data structures and interfaces
- **Technology**: Multi-target (.NET 8/.NET Framework 4.8)
- **Key Features**:
  - Game object interfaces and models
  - Position and coordinate systems
  - Enumeration types for game constants
  - Cross-service data contracts
- **Location**: `Exports/GameData.Core/`

### BotCommLayer
- **Purpose**: Inter-service communication using Protocol Buffers
- **Technology**: Multi-target (.NET 8/.NET Framework 4.8)
- **Key Features**:
  - Async socket-based communication
  - Protobuf message serialization
  - Reactive programming patterns
  - Type-safe message handling
- **Location**: `Exports/BotCommLayer/`

### BotRunner
- **Purpose**: Core bot orchestration — behavior trees, action dispatch, task stack
- **Technology**: Multi-target (.NET 8/.NET Framework 4.8)
- **Key Features**:
  - 75 CharacterAction types dispatched via behavior tree sequences
  - 26 BotTask implementations (combat, movement, questing, gathering, fishing, dungeoneering, vendor, trainer, etc.)
  - Behavior tree engine (Xas.FluentBehaviourTree) rebuilt per incoming action
  - Dual-path sequences: FG (frame-based) with BG (packet-based) fallback
  - Autonomous death recovery (ReleaseCorpse → RetrieveCorpse)
  - Deterministic login state machine (Login → RealmSelect → CharSelect → CreateChar → EnterWorld)
  - Pathfinding client integration with NavigationPath and frame-ahead waypoint acceptance
- **Location**: `Exports/BotRunner/`

## Native Components

### Loader (C++)
- **Purpose**: Native DLL injection bootstrap
- **Technology**: C++ with CLR hosting
- **Key Features**:
  - ICLRRuntimeHost integration
  - Debug console allocation
  - .NET runtime initialization inside WoW process
- **Location**: `Exports/Loader/`

### Navigation (C++)
- **Purpose**: High-performance pathfinding and physics engine
- **Technology**: C++ (Win32, built via CMake)
- **Key Features**:
  - A* pathfinding on MaNGOS navigation meshes (mmaps)
  - Physics engine (StepPhysicsV2) — gravity, jump, ground snap, collision
  - VMap/MMap loading for terrain and WMO collision
  - Scene queries (line-of-sight, height queries)
  - Physics constants: Gravity=19.2911, JumpV=7.9555, TerminalV=60.148
- **Location**: `Exports/Navigation/`

### FastCall (C++)
- **Purpose**: SEH-protected native WoW function call wrappers
- **Technology**: C++ (Win32, x86)
- **Key Features**:
  - __try/__except wrappers for all WoW internal function calls
  - SafeCallback1/SafeCallback3 exports for managed→native interop
  - Prevents AccessViolation crashes from propagating to .NET
  - Functions: BuyVendorItem, EnumerateVisibleObjects, GetCreatureRank/Type, GetItemCacheEntry, GetObjectPtr, GetPlayerGuid, GetUnitReaction, IsSpellOnCooldown, LootSlot, ReleaseCorpse, RetrieveCorpse, SetTarget, SellItemByGuid, SendMovementUpdate, SetControlBit, SetFacing, UseItem
- **Location**: `Exports/FastCall/`

## Bot Profiles

### BotProfiles (27 Class/Spec Combinations)
- **Purpose**: Combat rotation and behavior profiles for all WoW classes
- **Technology**: .NET 8, extends BotBase abstract factory
- **Coverage**: All 9 classes × 3 specs = 27 profiles
  - Warrior (Arms, Fury, Protection), Rogue (Assassination, Combat, Subtlety)
  - Hunter (BM, MM, Survival), Druid (Balance, Feral, Restoration)
  - Paladin (Holy, Protection, Retribution), Priest (Discipline, Holy, Shadow)
  - Shaman (Elemental, Enhancement, Restoration), Mage (Arcane, Fire, Frost)
  - Warlock (Affliction, Demonology, Destruction)
- **Each Profile Implements**: RestTask, PullTargetTask, BuffTask, PvERotationTask, PvPRotationTask
- **Extra Tasks**: HealTask (healers + hybrid DPS), SummonPetTask (hunter/warlock), ConjureItemsTask (mage)
- **Location**: `BotProfiles/`

## UI and Management

### WoWStateManagerUI
- **Purpose**: WPF-based management interface for WoWStateManager
- **Technology**: .NET 8 WPF (MVVM)
- **Key Features**:
  - Real-time bot monitoring dashboard
  - Character management (add/remove/configure)
  - Server status dashboard (MaNGOS connectivity)
  - Big Five personality configuration with decimal precision
- **Location**: `UI/WoWStateManagerUI/`

### .NET Aspire AppHost
- **Purpose**: Containerized WoW server orchestration for development
- **Technology**: .NET 8 Aspire
- **Key Features**:
  - Docker container orchestration for MySQL and MaNGOS
  - Automated port mapping and service discovery
  - Data persistence via volume management
- **Location**: `UI/Systems/Systems.AppHost/`

## Data Flow and Communication

### Injected Client Flow
1. **WoWStateManager** launches WoW.exe via CreateProcess
2. **Loader.dll** injected via VirtualAllocEx + WriteProcessMemory + CreateRemoteThread(LoadLibraryW)
3. **Loader** bootstraps .NET 8 CLR, loads ForegroundBotRunner.dll
4. **ForegroundBotRunner** installs hooks (SignalEvent, PacketLogger), starts ObjectManager enumeration
5. **BotRunner** behavior tree drives gameplay via IPC actions from StateManager
6. **PathfindingService** provides navigation paths via TCP/protobuf on port 5001

### Headless Client Flow
1. **WoWSharpClient** connects directly to auth/world servers (no WoW.exe needed)
2. SRP6 authentication → realm list → world server handshake
3. SMSG_UPDATE_OBJECT populates ObjectManager
4. **MovementController** sends movement packets using PhysicsEngine simulation
5. **PathfindingService** provides both navigation paths and physics steps

### Debug Flow
1. Set `WWOW_WAIT_DEBUG=1` environment variable
2. **Loader** allocates debug console
3. **ForegroundBotRunner** waits for debugger attachment
4. Visual Studio attaches to WoW.exe process
5. Debug with full symbol support

## Technology Stack

### Primary Technologies
- **.NET 8**: Modern services and core libraries
- **.NET Framework 4.8**: Injection compatibility layer
- **C++**: Native performance-critical components
- **Protocol Buffers**: Inter-service communication
- **WPF**: User interface framework
- **.NET Aspire**: Application orchestration
- **CMake**: Unified build system for cross-platform builds
- **GitHub Actions**: CI/CD pipeline automation

### Key Libraries
- **System.Reactive**: Reactive programming patterns
- **Google.Protobuf**: Message serialization
- **Microsoft.Extensions.Hosting**: Service hosting framework
- **Newtonsoft.Json**: JSON serialization

### Build & CI/CD Infrastructure
- **CMake 3.20+**: Cross-platform build system with Visual Studio generator
- **PowerShell Scripts**: Local development build automation
- **GitHub Actions**: Continuous integration and deployment
- **GitVersion**: Semantic versioning and release management
- **Docker**: Containerized build environments for reproducibility
- **CMake Presets**: IDE integration for consistent builds

## Build Configuration

### Build System Architecture
BloogBot uses a unified CMake-based build system that coordinates both native C++ projects and .NET builds:

- **CMake 3.20+**: Primary build system for native components
- **MSBuild Integration**: .NET projects built through CMake custom targets
- **Multi-Platform Support**: Win32 and x64 architectures
- **Unified Output**: All build artifacts organized in `Build/{Platform}/{Configuration}/`

### Platform Targets
- **Any CPU**: Most services and libraries
- **x86**: Injection-related projects (ForegroundBotRunner, StateManager)
- **Win32**: Native C++ projects (Loader, Navigation, FastCall)

### Configuration Strategy
- **Debug**: All managed projects for development
- **Release**: Native projects (Loader, Navigation) for performance

### CI/CD Pipeline
The project includes a comprehensive GitHub Actions workflow that provides:

- **Multi-Configuration Builds**: Debug/Release across Win32/x64 platforms
- **Automated Testing**: Unit tests with coverage reporting
- **Code Quality Analysis**: CodeQL security scanning
- **Artifact Management**: Build outputs, debug symbols, and packages
- **Semantic Versioning**: GitVersion-based release management
- **Containerized Builds**: Docker support for reproducible environments

### AI-Compatible Features
- **Compile Commands**: `compile_commands.json` generation for C++ intellisense
- **Symbol Generation**: Debug symbols (PDB) automatically managed
- **Structured Logging**: Build and test outputs in machine-readable formats
- **Code Indexing**: Support for AI-based code analysis and navigation

## Security and Anti-Detection

### Warden Bypass
- **Module Scanning Hook**: Hides injected DLLs from detection
- **Memory Scanning Hook**: Temporarily reverts patches during scans
- **Dynamic Cloaking**: On-demand hiding of bot presence

### Safety Mechanisms
- **Kill-switches**: Automatic bot stopping on suspicious conditions
- **Position Monitoring**: Stuck detection and recovery
- **State Timeout**: Prevention of infinite loops
- **Teleport Detection**: Unusual movement pattern alerts

## Development Guidelines

### Adding New Services
1. Create project under `Services/` directory
2. Implement `BackgroundService` or `IHostedService`
3. Use dependency injection for configuration
4. Add protobuf definitions for communication
5. Update service registration in host

### Extending Bot Behavior
1. Add new `CharacterAction` enum value in `GameData.Core/Enums/CharacterAction.cs`
2. Add matching `ActionType` value in `communication.proto` and regenerate
3. Map proto→enum in `BotRunnerService.ActionMapping.cs`
4. Build sequence in `BotRunnerService.ActionDispatch.cs` (support both FG frame + BG packet paths)
5. If complex, implement as `IBotTask` in `BotRunner/Tasks/`
6. Add unit tests + LiveValidation integration test

### Debugging Injection
1. Build in Debug configuration with x86 platform
2. Set `WWOW_WAIT_DEBUG=1` environment variable
3. Ensure PDB files are copied to injection directory
4. Attach Visual Studio debugger to WoW.exe process
5. Use Managed (.NET Framework) debugging mode

## Deployment Architecture

### Service Distribution
- **WoWStateManager**: Central orchestrator — spawns WoW processes, manages injection
- **PathfindingService**: Navigation and physics — single process loads map data once
- **ForegroundBotRunner**: Injected into each WoW.exe instance
- **WoWSharpClient**: Headless instances, one per bot account

### Configuration Management
- **Environment Variables**: Runtime configuration (WWOW_WAIT_DEBUG, test accounts)
- **JSON Config Files**: Hotspot patrol paths, character definitions
- **Protobuf Messages**: Inter-service communication (BotCommLayer)

### Test Infrastructure (13 Projects, 280+ Test Files)
- **BotRunner.Tests**: 80 test files — combat, movement, fishing, gathering, equipment, LiveValidation integration tests
- **Navigation.Physics.Tests**: 63 test files — physics engine, terrain, collision, swimming, transport, AABB
- **WoWSharpClient.Tests**: 53 test files — network agents, packet handlers, object updates (937+ tests)
- **WowSharpClient.NetworkTests**: 8 test files — TCP, auth, packet pipeline, reconnection
- **RecordedTests.Shared.Tests**: 27 test files — test orchestration, recording, storage (Azure/S3/local)
- **WWoWBot.AI.Tests**: 12 test files — bot state machine, advisory system, plugin catalog
- **ForegroundBotRunner.Tests**: 12 test files — injection, memory offsets, packet capture, FG/BG parity
- **PromptHandlingService.Tests**: 8 test files — AI decision engine, intent parsing, GM commands
- **PathfindingService.Tests**: 7 test files — socket integration, dynamic objects, diagnostics
- **RecordedTests.PathingTests.Tests**: 6 test files — recorded path replay, configuration
- **WoWStateManagerUI.Tests**: 3 test files — WPF converter tests
- **WoWSimulation.Tests**: 1 test file — mock MaNGOS server