# BloogBot Architecture

## Overview

BloogBot is a comprehensive World of Warcraft automation system designed with a modular, service-oriented architecture. The system is built to run bots either as background services or injected directly into the WoW game process, with sophisticated pathfinding, decision-making, and state management capabilities.

## High-Level Architecture

The BloogBot system follows a distributed service architecture with the following key characteristics:

- **Multi-Process Design**: Core services run as separate processes for isolation and fault tolerance
- **Injection Framework**: Critical bot logic runs inside the WoW process via DLL injection for low-latency game interaction
- **State Machine Coordination**: A finite state machine orchestrates bot behavior across different game scenarios
- **AI-Driven Decision Making**: ML-powered decision engine for adaptive bot behavior
- **Communication Layer**: Protobuf-based inter-service communication for reliable message passing

### Core Components

1. **StateManager**: Central orchestrator managing bot state transitions and coordination
2. **PathfindingService**: Navigation and movement planning using A* pathfinding
3. **DecisionEngineService**: AI-powered rule engine for dynamic decision making
4. **WoWSharpClient**: Low-level game client integration and memory manipulation
5. **ForegroundBotRunner**: Injection framework for running bot logic inside WoW process
6. **BackgroundBotRunner**: Standalone bot service for external coordination

## Service Architecture

### StateManager Service
- **Purpose**: Finite state machine implementation for bot behavior coordination
- **Technology**: .NET 8 Worker Service
- **Key Features**:
  - Stack-based state transitions
  - Safety kill-switches and monitoring
  - Discord integration for alerts
  - Support for nested goals (e.g., death recovery during grinding)
- **Location**: `Services/StateManager/`

### PathfindingService
- **Purpose**: Navigation and pathfinding using game world data
- **Technology**: .NET 8 Background Service with unsafe code for performance
- **Key Features**:
  - A* pathfinding algorithm on navigation meshes
  - Multi-threaded path computation
  - Support for dynamic obstacle avoidance
  - Uses precomputed move maps from `Bot/mmaps` directory
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
- **Purpose**: DLL injection framework for running bot logic inside WoW process
- **Technology**: Dual-target (.NET 8 + .NET Framework 4.8)
- **Key Features**:
  - Native CLR hosting via ICLRRuntimeHost
  - Debug support with environment variable controls
  - Automatic PDB copying for debugging
  - Shim architecture for compatibility
- **Location**: `Services/ForegroundBotRunner/`

### BackgroundBotRunner
- **Purpose**: Standalone bot service for external coordination
- **Technology**: .NET 8 Worker Service
- **Key Features**:
  - Independent process execution
  - Service management and monitoring
  - Integration with other bot services
- **Location**: `Services/BackgroundBotRunner/`

## Core Libraries

### WoWSharpClient
- **Purpose**: Low-level WoW client integration and game state management
- **Technology**: .NET 8 with unsafe code
- **Key Features**:
  - Memory reading/writing for game state access
  - Object manager for game entities
  - Movement packet handling
  - Anti-cheat (Warden) bypass mechanisms
  - Network protocol implementation
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
- **Purpose**: High-performance pathfinding algorithms
- **Technology**: C++ with native optimization
- **Key Features**:
  - Navigation mesh processing
  - Physics simulation
  - Performance-critical pathfinding operations
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

### StateManagerUI
- **Purpose**: WPF-based management interface
- **Technology**: .NET 8 WPF
- **Key Features**:
  - Bot status monitoring
  - Configuration management
  - Real-time logging display
- **Location**: `UI/StateManagerUI/`

### WWoW.Systems
- **Purpose**: Aspire-based application hosting
- **Technology**: .NET 8 with .NET Aspire
- **Key Features**:
  - Service orchestration
  - Distributed application management
  - Development environment support
- **Location**: `UI/WWoW.Systems/`

## Data Flow and Communication

### Message Flow
1. **StateManager** coordinates overall bot behavior
2. **PathfindingService** provides navigation paths via protobuf messages
3. **DecisionEngineService** processes rules and returns decisions
4. **WoWSharpClient** executes low-level game actions
5. **BotCommLayer** handles all inter-service communication

### Injection Process
1. **StateManager** launches WoW process
2. **Loader.dll** injected into WoW process
3. **Loader** initializes .NET CLR inside WoW
4. **ForegroundBotRunner** (.NET Framework 4.8 shim) loads
5. Bot logic executes with direct memory access

### Debug Flow
1. Set `BLOOGBOT_WAIT_DEBUG=1` environment variable
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
2. Set `BLOOGBOT_WAIT_DEBUG=1` environment variable
3. Ensure PDB files are copied to injection directory
4. Attach Visual Studio debugger to WoW.exe process
5. Use Managed (.NET Framework) debugging mode

## Deployment Architecture

### Service Distribution
- **StateManager**: Central coordinator, typically runs on main machine
- **PathfindingService**: Can run distributed for load balancing
- **DecisionEngineService**: Stateless, horizontally scalable
- **UI Components**: Local development and management interfaces

### Configuration Management
- **Environment Variables**: Runtime configuration
- **JSON Config Files**: Complex settings and rules
- **Database Storage**: Rule definitions and execution history

This architecture provides a robust, scalable foundation for World of Warcraft automation with clear separation of concerns, strong debugging capabilities, and sophisticated anti-detection mechanisms.