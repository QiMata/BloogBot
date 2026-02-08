# ForegroundBotRunner

A .NET 8 console application that provides direct memory interaction and foreground automation for World of Warcraft through process injection and memory manipulation.

## Overview

ForegroundBotRunner operates directly inside the World of Warcraft 1.12.1 client process via DLL injection, providing low-level access to game objects, memory manipulation, and direct function calling for advanced bot automation scenarios. Unlike BackgroundBotRunner which uses network protocols, ForegroundBotRunner reads and writes game memory directly for maximum control and responsiveness.

The service is designed as an injectable library that loads into the WoW game process, providing real-time enumeration of game objects, direct memory reading and writing with protection mechanisms, native function hooking and detours, thread synchronization for safe main-thread execution, and anti-Warden protection against detection systems.

This approach enables capabilities not possible through network-based automation: instant object enumeration without network latency, direct manipulation of player state and actions, precise control over character movement and facing, interception of game events before they reach the server, and seamless integration with the game's rendering pipeline.

## Architecture

```
+------------------------------------------------------------------+
|                     ForegroundBotRunner                           |
+------------------------------------------------------------------+
|                                                                   |
|  +-----------------------------------------------------------+   |
|  |                    Loader.cs                              |   |
|  |           DLL injection orchestrator and entry point      |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|  +-----------------------------------------------------------+   |
|  |                    Program.cs                             |   |
|  |               Main execution thread (STA)                 |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|         +--------------------+--------------------+               |
|         |                    |                    |               |
|  +--------------+    +----------------+    +---------------+     |
|  | Memory       |    | Object         |    | Hack          |     |
|  | Management   |    | Manager        |    | Manager       |     |
|  |              |    |                |    |               |     |
|  | - Read/Write |    | - Enumeration  |    | - Patches     |     |
|  | - Protection |    | - Filtering    |    | - Anti-Warden |     |
|  | - P/Invoke   |    | - LocalPlayer  |    | - Enable/     |     |
|  |              |    |                |    |   Disable     |     |
|  +--------------+    +----------------+    +---------------+     |
|         |                    |                    |               |
|  +-----------------------------------------------------------+   |
|  |                    Native Layer                            |   |
|  |  +-----------------+    +--------------------------+       |   |
|  |  | Functions.cs    |    | Detour.cs                |       |   |
|  |  | (Game Funcs)    |    | (Hook Management)        |       |   |
|  |  +-----------------+    +--------------------------+       |   |
|  +-----------------------------------------------------------+   |
|                              |                                    |
|                              v                                    |
|                    +-------------------+                          |
|                    | WoW.exe Process   |                          |
|                    | (Direct Memory)   |                          |
|                    +-------------------+                          |
+------------------------------------------------------------------+
```

## Project Structure

```
Services/ForegroundBotRunner/
+-- ForegroundBotRunner.csproj  # .NET 8 Console Application project
+-- Program.cs                  # Main entry point
+-- Loader.cs                   # Thread-safe DLL injection loader
+-- Mem/                        # Memory management and protection
|   +-- Memory.cs               # Core memory operations (read/write)
|   +-- MemoryAddresses.cs      # Static memory addresses for WoW 1.12.1
|   +-- Offsets.cs              # Dynamic memory offsets for structures
|   +-- Functions.cs            # Native function calls (CastSpell, CTM, etc.)
|   +-- Detour.cs               # Function detouring and hooking
|   +-- Hack.cs                 # Memory hack definitions
|   +-- HackManager.cs          # Memory patch management
|   +-- ThreadSynchronizer.cs   # Main thread synchronization
|   +-- AntiWarden/
|       +-- WardenDisabler.cs   # Anti-cheat bypass protection
|   +-- Hooks/
|       +-- SignalEventManager.cs  # Event hooking system
+-- Objects/                    # Game object representations
|   +-- WoWObject.cs            # Base game object reader
|   +-- WoWUnit.cs              # NPC and creature objects
|   +-- WoWPlayer.cs            # Other player objects
|   +-- LocalPlayer.cs          # Local player implementation with actions
|   +-- LocalPet.cs             # Pet object management
|   +-- WoWItem.cs              # Item objects
|   +-- WoWContainer.cs         # Bag and container objects
|   +-- WoWGameObject.cs        # Interactive game objects (chests, nodes, etc.)
|   +-- ItemCacheInfo.cs        # Item data caching
+-- Statics/                    # Global managers
|   +-- ObjectManager.cs        # Central object enumeration
|   +-- WoWEventHandler.cs      # Game event processing
+-- Frames/                     # UI frame management
|   +-- DialogFrame.cs          # Dialog interaction
+-- README.md                   # This documentation
```

## Key Components

### Memory Management

The memory subsystem provides safe access to game process memory:

```csharp
// Read game memory
var health = Memory.Read<int>(unitBase + Offsets.Health);
var position = Memory.Read<Position>(unitBase + Offsets.Position);

// Write game memory
Memory.Write<int>(playerBase + Offsets.TargetGuid, targetGuid);
Memory.Write<float>(playerBase + Offsets.Facing, newFacing);
```

### Object Manager

Central hub for game object enumeration and management:

```csharp
public class ObjectManager : IObjectManager
{
    public IWoWLocalPlayer Player { get; private set; }
    public IEnumerable<IWoWObject> Objects { get; }
    public IEnumerable<IWoWUnit> Units { get; }
    public IEnumerable<IWoWPlayer> Players { get; }
    public IEnumerable<IWoWItem> Items { get; }
    public IEnumerable<IWoWGameObject> GameObjects { get; }
}
```

Usage example:

```csharp
// Access game objects
var player = ObjectManager.LocalPlayer;
var target = ObjectManager.GetUnitByGuid(targetGuid);
var nearbyUnits = ObjectManager.Units.Where(u =>
    u.Position.DistanceTo(player.Position) < 40);
```

### Hack Manager

Manages memory patches and hooks:

```csharp
internal static class HackManager
{
    static internal void AddHack(Hack hack)
    {
        Console.WriteLine($"[HACK MANAGER] Adding hack {hack.Name}");
        Hacks.Add(hack);
        EnableHack(hack);
    }

    static internal void EnableHack(Hack hack)
    {
        // Apply memory patch
        Memory.Write(hack.Address, hack.NewBytes);
    }

    static internal void DisableHack(Hack hack)
    {
        // Restore original bytes
        Memory.Write(hack.Address, hack.OriginalBytes);
    }
}
```

### Thread Synchronizer

Ensures game operations execute on the main thread:

```csharp
ThreadSynchronizer.RunOnMainThread(() =>
{
    // This executes during EndScene hook
    player.CastSpell(spellId);
    // Safe execution of game operations
});
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Fasm.NET | 1.70.3.2 | Flat Assembler .NET wrapper for runtime code generation |
| Newtonsoft.Json | 13.0.3 | JSON serialization and configuration |
| System.Memory | 4.6.3 | Advanced memory operations and spans |
| Vcpkg.Nuget | 1.5.0 | Native dependency management |

### Project References

- **BotRunner**: Core automation engine integration
- **GameData.Core**: Shared data structures and interfaces

### External Dependencies

- **FastCall.dll**: Native calling convention bridge
- **Fasm.NET.dll**: Runtime assembly generation

### Framework References

- **System.ComponentModel.Composition**: MEF composition for plugin architecture

## Usage

### Process Injection

The ForegroundBotRunner is typically loaded via the Loader DLL:

```cpp
// C++ Loader calls into managed code
int Load(string args)
{
    thread = new Thread(() => {
        Program.Main(args.Split(" "));
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    return 1;
}
```

### Object Enumeration

Access real-time game objects:

```csharp
// Get all hostile units in range
var hostiles = ObjectManager.Units
    .Where(u => u.Health > 0)
    .Where(u => u.UnitReaction == UnitReaction.Hostile)
    .Where(u => u.DistanceToPlayer < 30);

// Target and attack
ObjectManager.SetTarget(hostile.Guid);
ObjectManager.StartMeleeAttack();
```

### Movement Control

Direct movement through memory manipulation:

```csharp
// Start movement in specific direction
ObjectManager.StartMovement(ControlBits.Front);

// Set precise facing
ObjectManager.SetFacing(MathHelper.ToRadians(90));

// Stop all movement
ObjectManager.StopAllMovement();
```

### Function Hooking

```csharp
// Hook EndScene for main thread execution
var hook = new Detour(
    Functions.EndScene,
    myEndSceneHandler,
    5 // instruction length
);
hook.Apply();
```

### Memory Patches

Apply runtime memory modifications:

```csharp
var antiAfkHack = new Hack
{
    Name = "Anti-AFK",
    Address = 0x12345678,
    OriginalBytes = new byte[] { 0x74, 0x08 },
    NewBytes = new byte[] { 0xEB, 0x08 }
};

HackManager.AddHack(antiAfkHack);
```

## Configuration

### Build Settings

The project uses compile-time configuration through project properties:
- **Target Framework**: .NET 8.0
- **Output Type**: Console Application (Exe)
- **Unsafe Blocks**: Enabled for memory operations
- **Base Output Path**: `../../Bot` (shared ecosystem directory)

### Memory Addresses

Static memory addresses are defined in `MemoryAddresses.cs` for WoW 1.12.1:

```csharp
public static class MemoryAddresses
{
    public const int LocalPlayerSpellsBase = 0x00C0D788;
    public const int LastHardwareAction = 0x00B4B424;
    public const int ZoneTextPtr = 0x00B42140;
    public const int ObjectManager = 0x00B41414;
    public const int LocalPlayerGuid = 0x00B41408;
    // ... additional addresses
}
```

## Safety & Anti-Detection

### Anti-Warden System

Protection against Blizzard's anti-cheat mechanisms:

```csharp
// WardenDisabler.cs provides protection against:
// - Memory scan detection
// - Code injection detection
// - Suspicious API call monitoring
```

### Thread Safety

All game interactions are synchronized to the main thread to prevent race conditions and crashes.

### Memory Protection

Careful memory management to avoid access violations:

```csharp
try
{
    var value = Memory.Read<int>(address);
    return value;
}
catch (Exception)
{
    // Graceful handling of invalid memory access
    return defaultValue;
}
```

## Integration with WWoW Ecosystem

### StateManager Integration

ForegroundBotRunner can be managed by the StateManager service for direct memory access scenarios where network-based automation is insufficient.

### BotRunner Coordination

Works alongside BotRunner for hybrid automation:
- **ForegroundBotRunner**: Direct memory access and real-time operations
- **BotRunner**: High-level behavior trees and decision making
- **Communication**: Shared interfaces and data structures

## Development Guidelines

### Memory Safety

- Always validate memory addresses before access
- Use try-catch blocks for memory operations
- Implement proper cleanup and disposal patterns

### Threading

- Use ThreadSynchronizer for main thread operations
- Avoid blocking the main game thread
- Implement proper cancellation tokens

### Error Handling

- Log all exceptions with context
- Graceful degradation on memory access failures
- Comprehensive error recovery mechanisms

## Performance Considerations

### Memory Efficiency

- Minimize memory allocations in hot paths
- Use object pooling for frequently created objects
- Careful management of native resources

### Real-time Operations

- 50ms enumeration cycles for object updates
- Efficient object filtering and querying
- Optimized memory reading patterns

### CPU Usage

- Balanced between responsiveness and CPU usage
- Efficient native function calling
- Minimal overhead on game performance

## Security Considerations

**Important Security Notes**:

### Legal Compliance

- This code is for educational and research purposes
- Users must ensure compliance with applicable terms of service
- No warranty provided for detection avoidance

### Process Injection

- Requires elevated privileges for memory access
- May trigger antivirus software warnings
- Should only be used in controlled environments

### Anti-Cheat Awareness

- Anti-Warden system for educational understanding
- Detection methods evolve continuously
- No guarantee of undetectability
- Designed for private servers only
- Do not use on official Blizzard servers

## Educational Value

The ForegroundBotRunner serves as a comprehensive example of:

### Advanced Windows Programming

- Process injection and DLL loading
- Memory management and protection
- Native function calling and hooking

### Game Development Concepts

- Object management systems
- Event-driven architectures
- Real-time performance optimization

### Security Research

- Anti-cheat system analysis
- Memory protection mechanisms
- Code injection techniques

## Troubleshooting

### Common Issues

**Injection Failures**:
- Verify target process architecture (x86/x64)
- Check for adequate privileges
- Ensure compatible .NET runtime

**Memory Access Violations**:
- Validate memory addresses for current game version
- Check for proper thread synchronization
- Verify anti-virus interference

**Detection Issues**:
- Review anti-Warden configuration
- Check for signature updates
- Verify memory patch integrity

### Debugging

Enable detailed logging for troubleshooting:

```csharp
// Console output for debugging
Console.WriteLine($"[OBJECT MANAGER] {objectCount} objects enumerated");
Console.WriteLine($"[HACK MANAGER] Adding hack {hack.Name}");
```

## Resources

The project includes:
- `Resources/Fasm.NET.dll` - Assembly compiler
- `Resources/FastCall.dll` - Native call marshaling

## Running

1. Start WoW.exe (1.12.1 client)
2. Run ForegroundBotRunner.exe
3. Bot injects and begins operation

## Related Documentation

- See [Loader README](../../Exports/Loader/README.md) for process injection and CLR hosting
- See [FastCall README](../../Exports/FastCall/README.md) for native function calling bridge
- See [BotRunner README](../../Exports/BotRunner/README.md) for high-level automation engine
- See [StateManager README](../StateManager/README.md) for multi-bot coordination service
- See [GameData.Core README](../../Exports/GameData.Core/README.md) for shared data structures
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*


# WoW 1.12.1.5875 — Client Game State Memory Offsets

## Overview

This document covers how to detect which screen the WoW 1.12.1 (build 5875) client is currently showing by reading memory. The vanilla client does **not** use ASLR, so the base address is always `0x00400000`. All absolute addresses below already include this base.

There is no single "master state" variable that cleanly enumerates every screen. Instead, detection requires reading a **combination** of offsets and applying logic.

---

## Primary Offsets

### GameState (IsInGame)

| Property | Value |
|----------|-------|
| **Address** | `0x00B4B424` |
| **Rebased (no base)** | `0x0074B424` |
| **Type** | `byte` |
| **Values** | `0` = Not in game, `1` = In game world |

This is the most widely referenced offset. It is a simple boolean byte:
- **0**: The player is NOT in the game world. This covers the login screen, character select, character creation, realm selection, loading screens, and any other "glue" (out-of-game) screen.
- **1**: The player IS in the game world and the Object Manager is active.

**Usage (C#):**
```csharp
bool isInGame = Memory.Read<byte>(0x00B4B424) == 1;
```

> **Note:** The address `0x00B4B424` is the absolute virtual address. Some sources list it as `0x74B424` which is rebased (subtract the `0x400000` module base). They refer to the same location.

---

### ContinentID (MapID)

| Property | Value |
|----------|-------|
| **Address** | `0x0086F694` |
| **Type** | `uint32` |
| **Loading value** | `0xFF` (255) or `0xFFFFFFFF` (-1 unsigned) |

This offset holds the current continent/map ID. During a **loading screen**, this value becomes `255` (`0xFF`). After loading completes and the player enters the world, it changes to the actual map ID (e.g., 0 = Eastern Kingdoms, 1 = Kalimdor, etc.).

**Usage for loading screen detection:**
```csharp
uint continentId = Memory.Read<uint>(0x0086F694);
bool isLoading = (continentId == 0xFF || continentId == 0xFFFFFFFF);
```

---

### Object Manager Base

| Property | Value |
|----------|-------|
| **Address** | `0x00B41414` |
| **Type** | `pointer (uint32)` |

The Object Manager base pointer. When this is **zero/null**, the client has not entered the game world — the Object Manager does not exist at the login screen, character select, or character creation screens. When non-zero, the Object Manager is initialized and the player is in the world (or about to be).

**Usage:**
```csharp
uint objMgrBase = Memory.Read<uint>(0x00B41414);
bool objectManagerExists = (objMgrBase != 0);
```

This can serve as an additional confirmation of the in-game state alongside the GameState byte.

---

### Client Connection (NetClient)

| Property | Value |
|----------|-------|
| **Address** | `0x00B41DA0` |
| **Type** | `pointer (uint32)` |

A pointer to the client's network connection object (`ClientServiceConnection`). When this is **non-null**, the client has an active connection to the server. When **null**, the client is disconnected (either at the initial login screen before authenticating, or has been disconnected).

**Usage:**
```csharp
uint connectionPtr = Memory.Read<uint>(0x00B41DA0);
bool isConnected = (connectionPtr != 0);
```

---

### Glue Screen Name (Lua Global: CURRENT_GLUE_SCREEN)

The WoW client maintains a Lua global variable `CURRENT_GLUE_SCREEN` that is a string identifying the current glue (out-of-game) screen. This can be read by executing Lua via `FrameScript_Execute` (internal function injection) or by locating it in the Lua global table in memory.

**Known string values:**

| String Value | Screen |
|-------------|--------|
| `"login"` | Login / authentication screen |
| `"charselect"` | Character selection screen |
| `"charcreate"` | Character creation screen |
| `"realmwizard"` | Realm/server selection screen |
| `"movie"` | Cinematic / movie playback |
| `"credits"` | Credits screen |

**How to read it (via Lua injection):**

If you have the ability to call `FrameScript_Execute` (`0x00419210`), you can execute:
```lua
-- From inside the glue environment
local screen = CURRENT_GLUE_SCREEN
```

Or, using `FrameScript_GetText` (`0x00703BF0`) after executing a script that stores the value.

> **Important:** The `CURRENT_GLUE_SCREEN` variable does **not** update after you enter the game world. It retains the last glue screen value (usually `"charselect"`). You must combine this with the `GameState` byte to know if you're actually in the world.

---

## GlueState Numeric Values

When using pattern scanning or reading the glue state as an integer (as tools like HBRelog do), the numeric GlueState values are:

| Value | State |
|-------|-------|
| `-1` | None / Unknown |
| `0` | Disconnected |
| `1` | Updater / Patching |
| `2` | Character Selection |
| `3` | Character Creation |
| `6` | Server/Realm Selection |
| `7` | Credits |
| `8` | Regional Selection |

> **Note:** The GlueState offset must be found via **pattern scanning** in 1.12.1 — it is not a well-known static address for this build the way `GameState` is. The HBRelog project uses byte pattern scanning to locate it dynamically. The pattern approach is more robust since the exact address may vary between modified client builds used by different private servers.

---

## Additional Useful Offsets

| Offset | Address | Type | Description |
|--------|---------|------|-------------|
| PlayerName | `0x00827D88` | `string` | Current logged-in character name (empty when not in game) |
| PlayerClass | `0x00879E89` | `byte` | Current character class ID |
| WoW Version | `0x000837C0` | `string` | Client build version string |
| LastHardwareAction | `0x00CF0BC8` | `uint32` | Timestamp of last hardware input event |
| GetCurrentKeyboardFocus | `0x009CE474` | `pointer` | Currently focused UI widget |
| Lua_DoString | `0x00419210` | `function` | FrameScript_Execute — run Lua code |
| FrameScript_GetText | `0x00703BF0` | `function` | Retrieve Lua return values |
| FrameScript_SignalEvent | `0x00703E50` | `function` | Fire a Lua event |

---

## Comprehensive State Detection Logic

Here is a recommended approach that combines the above offsets to reliably determine the current client screen:

```csharp
public enum ClientScreen
{
    LoginScreen,
    RealmSelect,
    CharacterSelect,
    CharacterCreate,
    LoadingScreen,
    InGame,
    Disconnected,
    Unknown
}

public ClientScreen GetCurrentScreen()
{
    byte gameState = Memory.Read<byte>(0x00B4B424);
    uint objMgrBase = Memory.Read<uint>(0x00B41414);
    uint connectionPtr = Memory.Read<uint>(0x00B41DA0);
    uint continentId = Memory.Read<uint>(0x0086F694);

    // --- IN GAME ---
    // GameState == 1 AND Object Manager exists
    if (gameState == 1 && objMgrBase != 0)
    {
        // Check if we're on a loading screen (transitioning between zones)
        // ContinentID becomes 0xFF during loading
        if (continentId == 0xFF || continentId == 0xFFFFFFFF)
            return ClientScreen.LoadingScreen;
        
        return ClientScreen.InGame;
    }

    // --- NOT IN GAME (Glue Screens) ---
    
    // Not connected to any server at all
    if (connectionPtr == 0)
        return ClientScreen.Disconnected;

    // Connected but not in game — we're on a glue screen
    // At this point, to distinguish login/charselect/charcreate,
    // you have several options:

    // Option A: Read GlueState integer (requires pattern scan)
    // Option B: Read CURRENT_GLUE_SCREEN Lua global (requires Lua access)
    // Option C: Check for UI frame existence (GlueDialogBackground, etc.)
    // Option D: Heuristic — check if PlayerName is populated
    
    string playerName = Memory.ReadString(0x00827D88, 40);
    
    if (string.IsNullOrEmpty(playerName))
    {
        // No player name set — likely at login screen or realm select
        return ClientScreen.LoginScreen;
    }
    else
    {
        // Player name exists — we've been to character select at some point
        // This is likely character select or loading into world
        
        if (continentId == 0xFF || continentId == 0xFFFFFFFF)
            return ClientScreen.LoadingScreen;
        
        return ClientScreen.CharacterSelect;
    }
}
```

### State Detection Summary Table

| GameState (B4B424) | ObjMgr (B41414) | Connection (B41DA0) | ContinentID (86F694) | Likely Screen |
|:---:|:---:|:---:|:---:|---|
| 0 | 0 | 0 | any | **Disconnected** (pre-login or lost connection) |
| 0 | 0 | != 0 | any | **Glue Screen** (login, charselect, charcreate, realmselect) |
| 0 | 0 | != 0 | 0xFF | **Loading Screen** (entering world from character select) |
| 1 | != 0 | != 0 | valid ID | **In Game** (fully loaded into world) |
| 1 | != 0 | != 0 | 0xFF | **Loading Screen** (zoning between maps while in game) |
| 0 | != 0 | != 0 | any | **Transitional** (briefly during logout/disconnect) |

---

## Important Notes

1. **No ASLR**: WoW 1.12.1 does not use Address Space Layout Randomization. The module base is always `0x00400000`, so all addresses are fixed and static across every launch.

2. **Warden Anti-Cheat**: The 1.12.1 client includes Warden, which performs memory scans. Reading memory from an external process is generally not detected, but modifying game memory at known offsets can trigger Warden scans. The offsets documented here are for **reading only**.

3. **Private Server Variations**: Some private servers ship modified `WoW.exe` binaries. While the offsets above are for the stock 1.12.1.5875 client, modified executables may have different addresses. Always verify against the specific binary you're working with.

4. **Loading Screen Detection**: The most reliable loading screen indicator is the `ContinentID` becoming `0xFF` (255). This works for both the initial world entry from character select AND zone transitions while in game (e.g., entering a dungeon).

5. **Character Creator vs Character Select**: Distinguishing between these two from pure memory reading (without Lua access) is harder. Both show `GameState = 0` with an active connection. The GlueState numeric value (2 vs 3) or the `CURRENT_GLUE_SCREEN` Lua variable is the most reliable way to tell them apart.

---

## Sources

- OwnedCore 1.12.1.5875 Info Dump Thread (multiple pages)
- HBRelog Memory Injection source (WowDevs/HBRelog-Memory-Injection on GitHub)
- OwnedCore "Login State" thread
- OwnedCore "Help getting GameState offsets" thread
- WoW vanilla UI source (GlueXML/GlueDialog.lua)
- Various open-source 1.12.1 bot projects (ZzukBot, Fishbot-1.12.1)

# WoW 1.12.1.5875 — Client Infrastructure Offsets

Non-player, non-character, non-world offsets for the WoW 1.12.1 (build 5875) client.
These cover the Lua engine, rendering pipeline, camera system, chat buffer, networking, input handling, UI frames, error system, raycasting, and Warden anti-cheat.

> **Base address**: `0x400000` (no ASLR in this build). All addresses below are absolute virtual addresses unless noted as relative offsets from a struct/pointer.

---

## 1. Lua / FrameScript Engine

The client embeds a Lua 5.0 interpreter. These are the key entry points for executing Lua and retrieving results.

| Name | Address | Signature / Type | Notes |
|------|---------|-----------------|-------|
| `Lua_DoString` (FrameScript_Execute) | `0x419210` | `void __cdecl (const char* code, const char* source, int unk)` | Executes a Lua string. The `source` param is typically `""` and `unk` is `0`. This is a **`__cdecl`** call. |
| `FrameScript__GetText` | `0x703BF0` | `const char* __fastcall (const char* varName, int unk1, int unk2)` | Reads the value of a Lua global variable as a string. 1.12.1 does **not** have `FrameScript::GetLocalizedText`; this is the equivalent. |
| `FrameScript_SignalEvent` | `0x703E50` | — | Fires a FrameScript event to registered handlers. |
| `FrameScript_SignalEvent2` | `0x703F50` | — | Alternate event signaling path. |
| `FrameScript_RegisterFunction` | *(requires pattern scan)* | — | Registers a C function as a Lua global. Used by addons/hooks to expose native functions to Lua. |
| `GetCurrentKeyBoardFocus` | `0x9CE474` | `ptr` (static) | Points to the UI frame currently holding keyboard focus. Null if no editbox is focused. Useful for knowing if the user is typing in chat. |

### Lua Injection Pattern (C#)

```csharp
// Execute Lua code
// Lua_DoString is __cdecl: (const char* code, const char* source, int unk)
delegate void Lua_DoStringDelegate(string code, string source, int unk);
Lua_DoString("CastSpellByName('Fireball')", "DoString", 0);

// Read a Lua global variable
// FrameScript__GetText is __fastcall: (const char* varName, int, int)
string result = FrameScript__GetText("CURRENT_GLUE_SCREEN", -1, 0);
```

---

## 2. DirectX 9 / Rendering

WoW 1.12.1 uses Direct3D 9. The D3D9 device pointer is stored in a static chain. EndScene (VTable index 42) is the primary hook point for injecting code on the render thread.

| Name | Address / Offset | Type | Notes |
|------|-----------------|------|-------|
| `D3D9_DevicePtr1` | `0xC5DF88` | `uint32 (ptr)` | First pointer in the device chain. |
| `D3D9_DevicePtr2` | `+0x397C` | offset from `*DevicePtr1` | Second indirection offset. |
| `EndScene_VTableIndex` | `+0xA8` | offset from VTable base | EndScene is VTable entry 42 (42 × 4 = 0xA8). |

### Resolving the EndScene Address

```csharp
// Follow the pointer chain to find EndScene
uint pDevice  = ReadUInt32(0xC5DF88);            // Step 1: read device ptr
uint pEnd     = ReadUInt32(pDevice + 0x397C);     // Step 2: second indirection
uint pScene   = ReadUInt32(pEnd);                 // Step 3: VTable base
uint endScene = ReadUInt32(pScene + 0xA8);        // Step 4: EndScene function ptr

// EndScene is called once per frame (~every 10-16ms at 60 FPS)
// Hook it to execute code on WoW's main thread
```

### Why EndScene?

- It's called every frame on the main thread, making it safe to call WoW internal functions
- All game objects are fully set up when EndScene fires
- Running code from arbitrary threads causes crashes; EndScene provides a safe execution context
- D3D9 Reset (VTable offset `0x40`) should also be hooked to handle device resets

---

## 3. Camera System

The camera is accessed through a pointer chain. The camera struct contains position, orientation matrix, FOV, clip planes, and aspect ratio.

| Name | Address / Offset | Type | Notes |
|------|-----------------|------|-------|
| `CameraPtr` | `0x0074B2BC` | `uint32 (ptr)` | Base pointer to camera manager. Rebased: `0x34B2BC`. |
| `CameraPtrOffset` | `+0x65B8` | offset | Offset from `*CameraPtr` to active camera struct. |
| `CameraPosition` | `+0x08` | `CVec3 (3×float)` | X, Y, Z position of the camera in world space. |
| `CameraMatrix` | `+0x14` | `float[9]` (36 bytes) | 3×3 rotation/orientation matrix. |
| `CameraFieldOfView` | `+0x40` | `float` | Current FOV in radians. |
| `CameraNearClip` | `+0x44` | `float` | Near clip plane distance. |
| `CameraFarClip` | `+0x48` | `float` | Far clip plane distance. |
| `CameraAspect` | `+0x4C` | `float` | Aspect ratio (width/height). |
| `CameraFollowingGUID` | `+0x88` | `uint64 (GUID)` | GUID of the unit the camera is following. |

### Reading Camera Position (C#)

```csharp
IntPtr camMgr = ReadIntPtr(0x0074B2BC);
IntPtr camera = ReadIntPtr(camMgr + 0x65B8);
float camX = ReadFloat(camera + 0x08);
float camY = ReadFloat(camera + 0x0C);
float camZ = ReadFloat(camera + 0x10);
```

### World-to-Screen Projection

The camera matrix + position + FOV are used for world-to-screen projection. The `CGWorldFrame__GetActiveCamera` function at `0x4818F0` returns the active camera pointer directly.

---

## 4. Chat System

Chat messages are stored in a circular buffer at a fixed memory location.

| Name | Address / Offset | Type | Notes |
|------|-----------------|------|-------|
| `ChatBase` | `0xB50580` | `ptr` (static) | Base address of the chat message buffer. |
| `NextMessage` | `+0x800` | offset | Stride between consecutive chat messages in the buffer. Each message entry is 0x800 (2048) bytes. |

### Chat Buffer Structure

Each message in the buffer is a 2048-byte record. The buffer is circular — the client overwrites the oldest messages. You can iterate through recent messages by walking from `ChatBase` in increments of `0x800`.

---

## 5. Network / Connection

| Name | Address | Type | Notes |
|------|---------|------|-------|
| `ClientServiceConnection` | `0xB41DA0` | `uint32 (ptr)` | Pointer to the active network connection object. **Null** = disconnected from server. Non-null = connected. |
| `NetClient::ProcessMessage` | *(in-class)* | `void __thiscall (int tickCount, CDataStore* dataStore)` | Opcode handler dispatcher. `m_handlers[]` is at offset `+0x74` within the NetClient instance. |

### Packet Handling

The client dispatches incoming server packets through `NetClient::ProcessMessage`. It reads a 16-bit opcode from the `CDataStore` and looks up a handler in `m_handlers[opCode]` (at offset `+0x74` in the NetClient struct). If a handler exists, it's called; otherwise the packet is reset/discarded.

This is the interception point for packet sniffing tools like SzimatSzatyor.

---

## 6. Input / Anti-AFK

| Name | Address | Type | Notes |
|------|---------|------|-------|
| `LastHardwareAction` | `0xCF0BC8` | `uint32` | Stores the tick count of the last keyboard/mouse input event. The client compares this against the current tick count to determine AFK status. |

### Anti-AFK Usage

```csharp
// Write the current tick count to prevent AFK timeout
// Do this every few seconds from an external process
WriteUInt32(0xCF0BC8, (uint)Environment.TickCount);
```

**Important**: namreeb (prominent 1.12.1 reverse engineer) recommends writing tick count to this address rather than patching the AFK-check function at `0x482ED8`, because Warden can detect code patches but cannot easily detect data writes to this address.

---

## 7. Client Version / Build Info

| Name | Address | Type | Notes |
|------|---------|------|-------|
| `WoWVersionOffset` | `0x00837C0` | `uint32` | Contains the build number (`5875`). Can be read to verify you're attached to the correct client version. |

---

## 8. UI Frame System

| Name | Address / Offset | Type | Notes |
|------|-----------------|------|-------|
| `UIFrame` | *(from Info Dump p4, partial)* | — | Base of the UI frame hierarchy. Individual frames (like `MerchantFrame`, `GossipFrame`) can be detected by walking the frame list or querying Lua. |
| `GetCurrentKeyBoardFocus` | `0x9CE474` | `ptr` | Currently focused UI editbox frame. |

### Detecting Open Windows via Lua

Since there's no single "is merchant open" memory flag, the standard approach is to use `Lua_DoString` + `FrameScript__GetText` to query frame visibility:

```lua
-- Check if merchant window is open
if MerchantFrame and MerchantFrame:IsVisible() then ... end
```

---

## 9. Raycasting / Line of Sight

These functions perform collision/intersection tests against the world geometry. Essential for line-of-sight checks and navigation.

| Name | Address | Signature | Notes |
|------|---------|-----------|-------|
| `CMap::VectorIntersect` (Traceline) | `0x69BFF0` | `int (CVec3* start, CVec3* end, CVec3* hitPoint, float* distance, uint32 flags)` | The classic "traceline" — casts a ray between two points and returns whether it hit world geometry. |
| `World::Intersect` | `0x6AA160` | `int (CVec3[] line, float* distance, CVec3* intersection, int flags)` | Alternate intersection function with slightly different parameters and flag values. |
| `CGWorldFrame::HitTestPoint` | `0x7E57E0` | — | Tests a screen-space point against the 3D world. |
| `CGWorldFrame__GetActiveCamera` | `0x4818F0` | `ptr ()` | Returns the active camera pointer. Used together with traceline for screen-to-world raycasts. |

### Traceline Flags

The flags parameter controls which geometry types to test against (terrain, WMO interiors, WMO exteriors, M2 models, etc.). These differ between `CMap::VectorIntersect` and `World::Intersect`.

---

## 10. Click-to-Move (CTM)

CTM is the client's built-in pathfinding/movement system. Writing to the CTM struct triggers character movement.

| Name | Notes |
|------|-------|
| `CTM_Action` | The action type — determines movement behavior. |
| `CTM_GUID` | Target GUID for interaction-type actions. |
| `CTM_X/Y/Z` | Destination coordinates. |

### CTM Action Types

```csharp
public enum ClickToMoveType : uint
{
    FaceTarget      = 0x1,
    Face            = 0x2,
    Stop            = 0x3,   // Throws exception in some contexts
    Move            = 0x4,
    NpcInteract     = 0x5,
    Loot            = 0x6,
    ObjInteract     = 0x7,
    FaceOther       = 0x8,
    Skin            = 0x9,
    AttackPosition  = 0xA,
    AttackGuid      = 0xB,
    ConstantFace    = 0xC,
    None            = 0xD,
    Attack          = 0x10,
    Idle            = 0x13,
}
```

**Note**: The CTM base address for 1.12.1 requires scanning or cross-referencing. One user reported the CTM GUID address at `0xC4D980`. The exact CTM struct base varies in different source dumps — verify against your binary.

---

## 11. Error / Display System

| Name | Address | Notes |
|------|---------|-------|
| `CGGameUI__DisplayError` | `0x496720` | Function that displays red error text (e.g., "Inventory is full"). Takes an error index and format args. |
| `g_errorMessages[]` | *(initialized at startup)* | Static array of error message structs. Index 0 = `ERR_INV_FULL`, index 1 = `ERR_BANK_FULL`, etc. Each entry has: `Message` (string key), `Unknown1` (type), `Extra` (sound), `Unknown2` (sub-type), `Unknown3` (duration). |
| `ErrorMessage output buffer` | `0xB4DA40` | Where the formatted error string is written before display. |

---

## 12. Warden Anti-Cheat

Warden is the client-side anti-cheat module. It performs memory scans, module checks, and timing validation.

### Known Warden-Scanned Addresses (1.12.1)

These are addresses that Warden's `MEM_CHECK` (opcode `0xF3`) is known to scan. Modifying bytes at these locations will trigger detection:

| Address | Size | Description |
|---------|------|-------------|
| `0x0040362B` | 3 bytes | `PollNet` / `WardenClient_Process` check |
| `0x004711E0` | 2 bytes | `CCharCreateInfo::CreateCharacter` — character name validation |
| `0x004711EA` | 1 byte | Character name validation (continued) |
| `0x00482BE3` | 1 byte | `CGWorldFrame::UpdateDayNightInfo` — time related |
| `0x00482ED8` | 6 bytes | `CGWorldFrame::OnWorldUpdate` — anti-AFK check. **Do not patch this**; write to `LastHardwareAction` instead. |
| `0x00494A50` | 7 bytes | `CGGameUI::CanPerformAction` |
| `0x0049F5DD` | 1 byte | `Script_SendChatMessage` — chat while dead check |
| `0x0049F6F2` | 3 bytes | `Script_SendChatMessage` — CMSG_MESSAGECHAT validation |
| `0x004C21C0` | 1 byte | `AutoLoot` — `EVENT_LOOT_BIND_CONFIRM` |
| `0x004D1C17` | 2 bytes | `Script_CanViewOfficerNote` |
| `0x00518062` | 1 byte | `Script_UnitLevel` — see level instead of skull |
| `0x00538610` | 4 bytes | `NETEVENTQUEUE::Poll` jump table |

### Warden Module Cache

After a session, the loaded Warden module is cached in `WDB/wowcache.wdb`:

| Offset | Size | Description |
|--------|------|-------------|
| `0x00` | 4 | Magic: `'WRDN'` |
| `0x04` | 4 | Client build (e.g., `5875`) |
| `0x08` | 4 | Client locale (e.g., `'enUS'`) |
| `0x14` | 16 | MD5 hash of the Warden module |
| `0x24` | 4 | Record length |
| `0x2C` | var | Encrypted module data |

### Safe vs. Unsafe Approaches

| Approach | Warden Risk | Notes |
|----------|-------------|-------|
| External memory reads | **Low** | Warden doesn't scan for external readers |
| Writing to data addresses (e.g., `LastHardwareAction`) | **Low** | Data writes are very hard to detect |
| Patching code bytes at scanned addresses | **HIGH** | Warden `MEM_CHECK` will detect this |
| DLL injection | **Medium** | Warden can enumerate loaded modules |
| EndScene hook (VTable) | **Medium** | Standard approach; some servers check for it |

---

## 13. Miscellaneous Client Functions

| Name | Address | Signature | Notes |
|------|---------|-----------|-------|
| `Lua_GetLocalizedText` | `0x3225E0` | — | Listed in PQR configs, but namreeb notes this function **does not exist** in 1.12.1. `FrameScript__GetText` (`0x703BF0`) is used instead. |
| `CGGameUI__Target` | `0x489A40` | — | Sets the current target. |
| `ItemWDBCacheGetRow` | `0x55BA30` | — | Reads a row from the item cache DBC. |
| `CallAutoLoot` | `0x4C1FA0` | — | Triggers auto-loot behavior. |
| `Detour` (code cave) | `0xBF0F0` | — | A location used by PQR-style tools for detour injection. Original bytes: `55 8B EC 81 EC F8 00 00 00`. |
| `UnitIsEnemy` | `0x6061E0` | `int __thiscall (void* unit1, void* unit2)` | Returns `2` if enemies, `1` if friendly. Used for aggro detection. |

---

## 14. DBC (Client Database) Access

The client stores game data in DBC (DataBase Client) files. You can read rows programmatically:

```csharp
public static uint ClientDB_GetRow(uint dbcPointer, uint row)
{
    uint maxIndex = ReadUInt32(dbcPointer + 0xC);
    uint minIndex = ReadUInt32(dbcPointer + 0x10);

    if (row > maxIndex || row < minIndex)
        return 0;

    return ReadUInt32(ReadUInt32(dbcPointer + 0x8) + (4 * row));
}
```

---

## Sources

- OwnedCore: [WoW] 1.12.1.5875 Info Dump Thread (pages 3, 4, 5, 9, 16, 25, 26, 29, 34, 36)
- AmeisenBot documentation (EndScene hooking)
- namreeb's contributions (FrameScript, Warden, anti-AFK guidance)
- Various GitHub projects: HBRelog, ZzukBot, Fishbot-1.12.1, VanillaMagic
- PQR offset configurations for build 5875

---

*Document generated: 2026-02-04*
*For companion document on game state detection offsets, see: wow_1121_gamestate_offsets.md*