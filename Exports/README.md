# Exports - Core Libraries and Native Components

> **Part of WWoW (Westworld of Warcraft)** - An AI-driven simulation platform for WoW-style game environments.

## Overview

The **Exports** directory contains the core shared libraries (C# and C++) that power the WWoW system. These components provide the foundational capabilities for both injected (ForegroundBotRunner) and headless (BackgroundBotRunner) bot implementations. See the repository root [Documentation Map](../README.md#documentation-map) for links to related services, UI tooling, and the recorded test harness.

## Directory Structure

```
Exports/
??? BotCommLayer/        # Protobuf IPC communication
??? BotRunner/           # Behavior tree framework and clients
??? FastCall/            # C++ x86 calling convention helper
??? GameData.Core/       # Shared interfaces and models
??? Loader/              # C++ CLR bootstrapper for DLL injection
??? Navigation/          # C++ pathfinding and physics engine
??? WinImports/          # Windows P/Invoke declarations
??? WoWSharpClient/      # Pure C# WoW protocol implementation
```

## Component Summary

| Component | Type | Purpose |
|-----------|------|---------|
| [**BotCommLayer**](BotCommLayer/README.md) | C# Library | Protobuf message definitions and socket infrastructure |
| [**BotRunner**](BotRunner/README.md) | C# Library | Behavior trees, pathfinding client, state coordination |
| [**FastCall**](FastCall/README.md) | C++ DLL | x86 fastcall helper for legacy function invocation |
| [**GameData.Core**](GameData.Core/README.md) | C# Library | Game object interfaces (IWoWUnit, IObjectManager, etc.) |
| [**Loader**](Loader/README.md) | C++ DLL | CLR bootstrapper for injection-based bot execution |
| [**Navigation**](Navigation/README.md) | C++ DLL | Detour/Recast pathfinding and physics simulation |
| [**WinImports**](WinImports/README.md) | C# Library | Windows API P/Invoke declarations |
| [**WoWSharpClient**](WoWSharpClient/README.md) | C# Library | Headless WoW client via network protocol |

## Architecture

```
                        WWoW System
???????????????????????????????????????????????????????????????????????
?                    Consumer Layer                                   ?
?  ???????????????????????  ???????????????????????                   ?
?  ? ForegroundBotRunner ?  ? BackgroundBotRunner ?                   ?
?  ? (Injected)          ?  ? (Headless)          ?                   ?
?  ???????????????????????  ???????????????????????                   ?
???????????????????????????????????????????????????????????????????????
?                     Exports Layer                                   ?
?                                                                     ?
?  ????????????????  ????????????????  ????????????????               ?
?  ?  BotRunner   ?  ? GameData.Core?  ? BotCommLayer ?               ?
?  ?(Orchestration)? ? (Interfaces) ?  ?   (IPC)      ?               ?
?  ????????????????  ????????????????  ????????????????               ?
?                                                                     ?
?  ????????????????  ????????????????  ????????????????               ?
?  ?WoWSharpClient?  ?  WinImports  ?  ?   Loader     ?               ?
?  ?  (Network)   ?  ?  (P/Invoke)  ?  ?  (C++ CLR)   ?               ?
?  ????????????????  ????????????????  ????????????????               ?
?                                                                     ?
?  ????????????????  ????????????????                                 ?
?  ?  Navigation  ?  ?   FastCall   ?                                 ?
?  ? (C++ Physics)?  ? (C++ Calling)?                                 ?
?  ????????????????  ????????????????                                 ?
???????????????????????????????????????????????????????????????????????
```

## Functional Layers

### Core Infrastructure Layer
- **GameData.Core**: Foundational data types, interfaces, and enumerations
- **WinImports**: Windows API wrappers for system interaction
- **BotCommLayer**: Service communication and messaging infrastructure

### Game Interaction Layer
- **WoWSharpClient**: Pure C# WoW protocol implementation for network communication
- **FastCall**: Native bridge for legacy calling conventions and memory operations
- **Loader**: Process injection and managed code hosting

### Automation Layer
- **BotRunner**: High-level bot orchestration with behavior trees
- **Navigation**: Advanced pathfinding and collision detection

## Native C++ Components

The WWoW ecosystem includes three critical native C++ DLLs:

### FastCall.dll - Function Interop Bridge
Enables managed C# code to call game functions with proper calling conventions.

**Key Exported Functions**:
- `LuaCall()` - Execute Lua scripts within the game client
- `EnumerateVisibleObjects()` - Query visible game objects
- `BuyVendorItem()` / `SellItemByGuid()` - Vendor interactions
- `Intersect()` / `Intersect2()` - 3D collision detection

### Loader.dll - CLR Hosting
Hosts .NET runtime within game process for direct memory access.

**Key Features**:
- CLR 4.0+ hosting with legacy v2 fallback
- Thread-safe CLR initialization
- Debug support with console allocation

### Navigation.dll - Pathfinding Engine
Provides advanced pathfinding using Detour navigation meshes.

**Key API Methods**:
- `CalculatePath()` - A* pathfinding between coordinates
- `IsLineOfSight()` - Visibility testing
- `CapsuleOverlap()` - Collision detection

## Development Guidelines

### Technology Stack
- **Managed Code**: .NET 8 with C# 12, nullable reference types
- **Native Code**: C++17/C++20 with Visual Studio 2022 toolset
- **Communication**: Protocol Buffers, TCP sockets
- **Navigation**: Detour navigation meshes, A* pathfinding

### Build Configuration
All projects output to the shared `Bot/` directory:
- **Debug**: `Bot/Debug/net8.0/`
- **Release**: `Bot/Release/net8.0/`

### Native C++ Build Requirements

**Prerequisites**:
- Visual Studio 2022 with C++ workload
- Windows 10 SDK (latest version)
- Platform Toolset v143 or compatible

**Platform Support**:

| Project | Win32 | x64 | Configuration |
|---------|-------|-----|---------------|
| FastCall | Yes | Yes | Debug/Release |
| Loader | Yes | Yes | Debug/Release |
| Navigation | Yes | Yes | Debug/Release |

**Language Standards**:
- FastCall: C++14 (compatibility with game client)
- Loader: C++17 (Debug), C++20 (Release)
- Navigation: C++20 with modern STL features

## Getting Started

### For Bot Users
1. Start with [WoWSharpClient](WoWSharpClient/README.md) to understand network communication
2. Review [BotRunner](BotRunner/README.md) for behavior and decision making
3. Check [BotCommLayer](BotCommLayer/README.md) for service coordination

### For Developers
1. Begin with [GameData.Core](GameData.Core/README.md) for foundational types
2. Study [Loader](Loader/README.md) and [FastCall](FastCall/README.md) for injection mechanics
3. Explore [Navigation](Navigation/README.md) for pathfinding implementation

## Security Notes

**Important**: This project is for private servers and AI research only.

- Do not use on official Blizzard servers
- Native components (Loader, FastCall) perform memory manipulation
- Warden bypass is tailored to specific private server implementations

## Related Documentation

- See `ARCHITECTURE.md` for system overview
- Each project includes comprehensive README files with detailed documentation

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
