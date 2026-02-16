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
- **Purpose**: Rule-based decision engine for dynamic bot behavior
- **Technology**: .NET 8 with ML.NET integration
- **Key Features**:
  - Dynamic rule definition and execution
  - PHP code execution for business rules
  - Artisan command support
  - Audit logging and execution tracking
- **Location**: `Services/DecisionEngineService/`

### ForegroundBotRunner
- **Purpose**: Injected bot logic running inside WoW.exe process
- **Technology**: .NET 8 (injected via Loader.dll CLR hosting)
- **Key Features**:
  - Direct memory reading/writing for game state access
  - Object Manager enumeration (units, players, items, game objects)
  - GrindBot state machine (Idle → FindTarget → Combat → Loot → Rest → Dead)
  - Combat rotations for all 9 classes
  - Movement recording system (JSON/protobuf output)
  - Thread synchronization for safe main-thread execution
- **Location**: `Services/ForegroundBotRunner/`

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
- **Purpose**: Core bot execution logic and client integration
- **Technology**: Multi-target (.NET 8/.NET Framework 4.8)
- **Key Features**:
  - Bot behavior implementations
  - Client adapter patterns
  - Activity tracking and monitoring
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
- **Purpose**: Legacy function call support for older WoW clients
- **Technology**: C++
- **Key Features**:
  - Custom calling conventions
  - Legacy client compatibility
  - Performance-optimized function calls
- **Location**: `Exports/FastCall/`

## UI and Management

### WoWStateManagerUI
- **Purpose**: WPF-based management interface for WoWStateManager
- **Technology**: .NET 8 WPF
- **Key Features**:
  - Bot status monitoring
  - Character state display
  - Configuration management
- **Location**: `UI/WoWStateManagerUI/`

## Data Flow and Communication

### Injected Client Flow
1. **WoWStateManager** launches WoW.exe process
2. **Loader.dll** injected into WoW process
3. **Loader** initializes .NET CLR inside WoW, loads ForegroundBotRunner
4. **ForegroundBotRunner** hooks EndScene, starts ObjectManager polling
5. **GrindBot** state machine drives gameplay (find target → combat → loot → rest)
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
1. Create new states implementing `IBotState`
2. Add state factory methods to dependency container
3. Implement state transitions in StateManager
4. Test state isolation and interrupt handling

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

### Test Infrastructure
- **Navigation.Physics.Tests**: Physics engine calibration against recorded movement data (42/43 passing)
- **WoWSharpClient.Tests**: Protocol packet format validation (937+ tests)
- **BotRunner.Tests**: Integration tests (login, movement recording, screen detection)
- **PathfindingService.Tests**: Pathfinding and physics interop tests