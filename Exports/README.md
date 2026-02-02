# Exports - Core Libraries and Native Components

> **Part of WWoW (Westworld of Warcraft)** - An AI-driven simulation platform for WoW-style game environments.

## Overview

The **Exports** directory contains the core shared libraries (C# and C++) that power the WWoW system. These components provide the foundational capabilities for both injected (ForegroundBotRunner) and headless (BackgroundBotRunner) bot implementations.

## Directory Structure

```
Exports/
├── BotCommLayer/        # Protobuf IPC communication
├── BotRunner/           # Behavior tree framework and clients
├── FastCall/            # C++ x86 calling convention helper
├── GameData.Core/       # Shared interfaces and models
├── Loader/              # C++ CLR bootstrapper for DLL injection
├── Navigation/          # C++ pathfinding and physics engine
├── WinImports/          # Windows P/Invoke declarations
└── WoWSharpClient/      # Pure C# WoW protocol implementation
```

## Component Summary

| Component | Type | Purpose |
|-----------|------|---------|
| **BotCommLayer** | C# Library | Protobuf message definitions and socket infrastructure |
| **BotRunner** | C# Library | Behavior trees, pathfinding client, state coordination |
| **FastCall** | C++ DLL | x86 fastcall helper for legacy function invocation |
| **GameData.Core** | C# Library | Game object interfaces (IWoWUnit, IObjectManager, etc.) |
| **Loader** | C++ DLL | CLR bootstrapper for injection-based bot execution |
| **Navigation** | C++ DLL | Detour/Recast pathfinding and physics simulation |
| **WinImports** | C# Library | Windows API P/Invoke declarations |
| **WoWSharpClient** | C# Library | Headless WoW client via network protocol |

## Architecture Overview

```
                        WWoW System
┌─────────────────────────────────────────────────────────────────────┐
│                    Consumer Layer                                   │
│  ┌─────────────────────┐  ┌─────────────────────┐                   │
│  │ ForegroundBotRunner │  │ BackgroundBotRunner │                   │
│  │ (Injected)          │  │ (Headless)          │                   │
│  └──────────┬──────────┘  └──────────┬──────────┘                   │
├─────────────┴────────────────────────┴──────────────────────────────┤
│                     Exports Layer                                   │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │
│  │  BotRunner   │  │ GameData.Core│  │ BotCommLayer │               │
│  │(Orchestration)│ │ (Interfaces) │  │   (IPC)      │               │
│  └──────────────┘  └──────────────┘  └──────────────┘               │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │
│  │WoWSharpClient│  │  WinImports  │  │   Loader     │               │
│  │  (Network)   │  │  (P/Invoke)  │  │  (C++ CLR)   │               │
│  └──────────────┘  └──────────────┘  └──────────────┘               │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐                                 │
│  │  Navigation  │  │   FastCall   │                                 │
│  │ (C++ Physics)│  │ (C++ Calling)│                                 │
│  └──────────────┘  └──────────────┘                                 │
└─────────────────────────────────────────────────────────────────────┘
```

---

# Core Bot Engine (ForegroundBotRunner Architecture)

The **Core Bot Engine** runs inside the World of Warcraft game process to enable in-process bot functionality. It is responsible for low-level memory access and in-game function calls, acting as the bridge between the game client and higher-level bot logic. Its main roles include:

- **Reading/writing game memory** for state inspection and manipulation
- **Calling internal game functions** (e.g., to move or cast spells)
- **Hooking game routines** (especially anti-cheat functions) for stealth operation

By encapsulating these tasks, the Core Bot Engine provides a safe, high-performance API that higher-level modules can use without dealing with the complexities of memory manipulation and anti-cheat bypass.

## High-Level Architecture and Injection Process

The Core Bot Engine runs *inside* the WoW process. This is achieved by a separate injector program (the **Bootstrapper**) that:

1. Launches the WoW client process
2. Allocates memory in WoW to write the path of `Loader.dll`
3. Creates a remote thread to load `Loader.dll` into WoW

Once injected, `Loader.dll` bootstraps within the game process:

1. Starts the .NET Common Language Runtime (CLR) inside WoW
2. Loads the WWoW core assembly
3. Initializes all bot subsystems

### Native Helper (FastCall.dll)

In addition to `Loader.dll`, the bot uses **FastCall.dll** for certain function calls on older game clients (e.g., Vanilla WoW 1.12.1). This DLL exports helper functions that invoke game routines with calling conventions that .NET cannot easily handle directly (such as x86 fastcall).

For newer expansions (TBC/WotLK), the FastCall helper is largely bypassed in favor of direct delegate calls from C#.

## Memory Access: Reading and Writing Game Memory

The **MemoryManager** provides safe memory access:

```csharp
// Read primitive types
var health = MemoryManager.ReadInt(unitBase + Offsets.Health);
var position = MemoryManager.ReadFloat(playerBase + Offsets.PosX);

// Write to game memory
MemoryManager.WriteInt(targetAddress, newValue);
MemoryManager.WriteBytes(codeAddress, patchBytes);
```

### Memory Addresses

The `MemoryAddresses.cs` file contains addresses for each supported client version:

| Version | Build | Addresses |
|---------|-------|-----------|
| Vanilla | 5875 | Player data, object list, function pointers |
| TBC | 8606 | Same structure, different offsets |
| WotLK | 12340 | Same structure, different offsets |

## Calling Internal Game Functions

The bot calls game functions via C# delegates with exact signatures:

```csharp
// Define delegate with correct calling convention
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int CastSpellByIdDelegate(ulong targetGuid, int spellId, bool unknown);

// Create delegate from function address
var CastSpell = Marshal.GetDelegateForFunctionPointer<CastSpellByIdDelegate>(
    (IntPtr)MemoryAddresses.CastSpellById);

// Call game function
CastSpell(targetGuid, spellId, false);
```

### IGameFunctionHandler Pattern

Each expansion has a specific handler:

- `VanillaGameFunctionHandler` - Uses FastCall.dll for some functions
- `TBCGameFunctionHandler` - Direct delegate calls
- `WotLKGameFunctionHandler` - Direct delegate calls

The `Functions` static class provides version-agnostic access:

```csharp
// Version-agnostic API
Functions.CastSpellById(spellId, targetGuid);
Functions.ClickToMove(position);
Functions.InteractWithNpc(npcGuid);
```

## Anti-Cheat Countermeasures (Warden Bypass)

The Core Bot Engine implements countermeasures against WoW's **Warden** anti-cheat:

### Module Scanning Hook

WWoW hooks `Module32First` and `Module32Next` to hide its DLLs:

```
Warden: "List all loaded modules"
→ WWoW intercepts and filters out:
  - Loader.dll
  - FastCall.dll
  - WWoW assemblies
→ Warden sees only legitimate game modules
```

### Memory Scanning Hook

WWoW hooks Warden's `PageScan` and `MemScan` functions:

```
Warden: "Read memory at address X"
→ WWoW intercepts:
  1. Temporarily restore original bytes at X
  2. Let Warden read "clean" memory
  3. Re-apply WWoW's patches
→ Warden sees unmodified game memory
```

### HackManager

All patches are registered with `HackManager` for coordinated cloaking:

```csharp
// Register a patch
var hack = new Hack("SpeedHack", address, originalBytes, newBytes);
HackManager.Register(hack);

// During Warden scan, all registered hacks are temporarily disabled
```

> ⚠️ **Warning**: These measures are tailored for specific private server Warden implementations. Botting is never 100% safe.

## Developer Onboarding Guide

### Key Files and Classes

| File | Purpose |
|------|---------|
| `MemoryManager.cs` | All memory read/write operations |
| `MemoryAddresses.cs` | Address database per client version |
| `IGameFunctionHandler.cs` | Interface for game function calls |
| `Functions.cs` | Version-agnostic game function facade |
| `ObjectManager.cs` | Game object enumeration and wrappers |
| `WardenDisabler.cs` | Anti-cheat hook implementation |
| `HackManager.cs` | Memory patch registration and management |
| `Detour.cs` | Inline function hooking |

### Adding a New Game API Call

1. **Find the function address** for each game version
2. **Add to MemoryAddresses.cs**:
   ```csharp
   public static readonly uint NewFunction = 0x00123456;
   ```
3. **Define delegate in GameFunctionHandler**:
   ```csharp
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   delegate void NewFunctionDelegate(int param1);
   ```
4. **Add to IGameFunctionHandler interface**
5. **Expose in Functions.cs**

### Reading New Memory Values

1. **Find the address/offset** via reverse engineering
2. **Add to MemoryAddresses.cs** (for static globals)
3. **Implement read in appropriate class**:
   ```csharp
   public int NewValue => MemoryManager.ReadInt(
       ObjectManager.PlayerBase + Offsets.NewValue);
   ```

### Debugging Techniques

1. **Compile in Debug mode**
2. **Launch via Bootstrapper** (as admin)
3. **Attach Visual Studio to WoW.exe** after injection
4. **Set breakpoints** in managed code
5. **Use console output** for logging (Loader opens a console)

```csharp
// Debug logging
Console.WriteLine($"Player health: {LocalPlayer.Health}");
Logger.Log($"Casting spell {spellId}");
```

### Common Pitfalls

| Issue | Cause | Solution |
|-------|-------|----------|
| Instant crash | Wrong calling convention | Verify delegate attributes |
| Silent failure | Wrong address | Use Cheat Engine to verify |
| Access violation | Protected memory | Use MemoryManager.WriteBytes |
| Warden detection | Unregistered hack | Register with HackManager |

## Security Notes

⚠️ **Important**:

- This project is for **private servers only**
- Do not use on official Blizzard servers
- Memory manipulation may trigger anti-cheat on some servers
- The Warden bypass is specific to supported private server implementations

## Related Documentation

- See `Navigation/README.md` for pathfinding details
- See `BotCommLayer/README.md` for IPC protocol
- See `WoWSharpClient/README.md` for network client
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
