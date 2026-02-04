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
