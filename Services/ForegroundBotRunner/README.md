# ForegroundBotRunner

In-process bot execution service that runs directly inside the World of Warcraft 1.12.1 client via DLL injection. Provides direct memory access to game objects and function hooking for seamless bot integration.

## Overview

ForegroundBotRunner is a Windows executable that:
- **Injects into WoW.exe**: Loads bot DLL into the game process
- **Reads Game Memory**: Direct access to object manager, player state, etc.
- **Hooks Game Functions**: Intercepts game events for responsive automation
- **Manages Hacks**: Enables/disables memory patches for bot functionality
- **Bypasses Warden**: Anti-cheat evasion (for private servers)

## Architecture

```
ForegroundBotRunner/
??? Program.cs                    # Entry point
??? Loader.cs                     # DLL injection orchestrator
??? Mem/
?   ??? Memory.cs                 # Memory read/write utilities
?   ??? MemoryAddresses.cs        # Static game addresses
?   ??? Offsets.cs                # Structure field offsets
?   ??? Functions.cs              # Game function pointers
?   ??? Hack.cs                   # Memory patch definition
?   ??? HackManager.cs            # Patch enable/disable
?   ??? Detour.cs                 # Function hook management
?   ??? ThreadSynchronizer.cs     # Main thread execution
?   ??? Hooks/
?   ?   ??? SignalEventManager.cs # Event hook system
?   ??? AntiWarden/
?       ??? WardenDisabler.cs     # Anti-cheat bypass
??? Objects/
?   ??? WoWObject.cs              # Base object reader
?   ??? WoWUnit.cs                # Unit memory reader
?   ??? WoWPlayer.cs              # Player memory reader
?   ??? LocalPlayer.cs            # Local player with actions
?   ??? LocalPet.cs               # Pet control
?   ??? WoWItem.cs                # Item memory reader
?   ??? WoWContainer.cs           # Bag memory reader
?   ??? WoWGameObject.cs          # World object reader
?   ??? ItemCacheInfo.cs          # Item cache parsing
??? Statics/
?   ??? ObjectManager.cs          # Central object registry
?   ??? WoWEventHandler.cs        # Game event dispatcher
??? Frames/
    ??? DialogFrame.cs            # UI frame interaction
```

## Key Components

### Memory Management

```csharp
// Read game memory
var health = Memory.Read<int>(unitBase + Offsets.Health);
var position = Memory.Read<Position>(unitBase + Offsets.Position);

// Write game memory
Memory.Write<int>(playerBase + Offsets.TargetGuid, targetGuid);
```

### Object Manager

```csharp
// Access game objects
var player = ObjectManager.LocalPlayer;
var target = ObjectManager.GetUnitByGuid(targetGuid);
var nearbyUnits = ObjectManager.Units.Where(u => 
    u.Position.DistanceTo(player.Position) < 40);
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

### Thread Synchronization

Game functions must be called from the main thread:

```csharp
ThreadSynchronizer.RunOnMainThread(() =>
{
    // This executes during EndScene hook
    player.CastSpell(spellId);
});
```

## Memory Addresses

Key addresses defined in `MemoryAddresses.cs`:
- Object Manager base pointer
- Local player GUID pointer
- Function addresses (CastSpell, CTM, etc.)
- Frame script execution

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Fasm.NET | 1.70.3.2 | x86 assembly generation |
| Newtonsoft.Json | 13.0.3 | Configuration |
| System.Memory | 4.6.3 | Memory span utilities |
| Vcpkg.Nuget | 1.5.0 | Native dependency management |

## Resources

The project includes:
- `Resources/Fasm.NET.dll` - Assembly compiler
- `Resources/FastCall.dll` - Native call marshaling

## Project References

- **BotRunner**: Behavior tree framework
- **GameData.Core**: Shared interfaces

## Running

1. Start WoW.exe (1.12.1 client)
2. Run ForegroundBotRunner.exe
3. Bot injects and begins operation

## Security Considerations

?? **Warning**: 
- This is designed for private servers only
- Do not use on official Blizzard servers
- Memory manipulation may trigger anti-cheat on some servers
- The Warden bypass is specific to 1.12.1 and may not work on all private servers

## Related Documentation

- See `Exports/WinImports/README.md` for P/Invoke declarations
- See `Exports/FastCall/` for native call assembly
- See `ARCHITECTURE.md` for system overview
