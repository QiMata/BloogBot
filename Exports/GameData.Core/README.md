# GameData.Core

GameData.Core is a .NET 8 library that provides core data structures, interfaces, and enumerations for World of Warcraft game data interaction. This library serves as the foundation for bot automation systems by offering strongly-typed representations of WoW game objects, player data, and game mechanics.

## Overview

This library provides a comprehensive set of interfaces and models that represent various aspects of the World of Warcraft game world, including:

- **Game Objects**: Players, NPCs, items, containers, and game objects
- **Game Mechanics**: Spells, buffs/debuffs, combat states, and movement
- **UI Frames**: Quest dialogs, merchant windows, and other game interfaces
- **Data Models**: Positions, inventories, character stats, and game state

## Key Features

### Core Interfaces

- **IWoWObject** - Base interface for all game objects
- **IWoWUnit** - Extended interface for units (players, NPCs, pets)
- **IWoWPlayer** - Player-specific interface with character data
- **IWoWItem** - Item and equipment interface
- **IObjectManager** - Game object management interface

### Game Data Models

- **Position** - 3D coordinates with distance calculations and vector operations
- **HighGuid** - WoW object identification system
- **Spell** - Spell casting and effect data
- **Inventory** - Item storage and management
- **CharacterSelect** - Character selection data

### Enumerations

Comprehensive enums covering all aspects of WoW gameplay:

- **Class, Race, Gender** - Character attributes
- **MovementFlags, UnitFlags** - Character states and movement
- **SpellSchools, DamageType** - Magic and combat systems
- **ItemQuality, InventoryType** - Item classification
- **ChatMsg, Opcode** - Communication and networking

### Frame Interfaces

UI interaction interfaces for various game windows:

- **IQuestFrame** - Quest acceptance and completion
- **IMerchantFrame** - Vendor interactions
- **ITaxiFrame** - Flight path selection
- **ITrainerFrame** - Skill and spell training
- **ILootFrame** - Loot distribution

## Project Structure

```
GameData.Core/
??? Constants/           # Game constants and static data
?   ??? RaceConstants.cs
?   ??? Spellbook.cs
??? Enums/              # Comprehensive game enumerations
?   ??? Enums.cs        # Core game enums
?   ??? UpdateFields.cs # Object update field definitions
??? Frames/             # UI frame interfaces
?   ??? IQuestFrame.cs
?   ??? IMerchantFrame.cs
?   ??? [Other frame interfaces]
??? Interfaces/         # Core game object interfaces
?   ??? IWoWObject.cs
?   ??? IWoWUnit.cs
?   ??? IWoWPlayer.cs
?   ??? [Other interfaces]
??? Models/            # Data models and structures
    ??? Position.cs
    ??? Spell.cs
    ??? Inventory.cs
    ??? [Other models]
```

## Usage Examples

### Working with Positions

```csharp
var playerPos = new Position(100.5f, 200.3f, 15.0f);
var targetPos = new Position(105.0f, 205.0f, 15.5f);

float distance = playerPos.DistanceTo(targetPos);
float distance2D = playerPos.DistanceTo2D(targetPos);

// Vector operations
var direction = targetPos - playerPos;
var normalized = direction.GetNormalizedVector();
```

### Checking Unit States

```csharp
public void CheckUnitStatus(IWoWUnit unit)
{
    if (unit.IsInCombat)
        Console.WriteLine("Unit is in combat");
    
    if (unit.IsMoving)
        Console.WriteLine("Unit is moving");
    
    if (unit.HasBuff("Blessing of Might"))
        Console.WriteLine("Unit has blessing buff");
    
    var healthPercent = unit.HealthPercent;
    Console.WriteLine($"Health: {healthPercent}%");
}
```

### Player Information

```csharp
public void DisplayPlayerInfo(IWoWPlayer player)
{
    Console.WriteLine($"Name: {player.Name}");
    Console.WriteLine($"Class: {player.Class}");
    Console.WriteLine($"Race: {player.Race}");
    Console.WriteLine($"Level: {player.Level}");
    Console.WriteLine($"Position: {player.Position}");
    Console.WriteLine($"Health: {player.Health}/{player.MaxHealth}");
    Console.WriteLine($"Mana: {player.Mana}/{player.MaxMana}");
}
```

## Dependencies

- **.NET 8.0** - Target framework
- **Newtonsoft.Json** (v13.0.3) - JSON serialization
- **BotCommLayer** - Communication layer (project reference)

## Technical Details

### Memory Layout

The library includes several `StructLayout` attributes for interoperability with native WoW memory structures:

- `XYZ` - 3D coordinate structure
- `TriangleStruct` - Navigation mesh triangles
- `RaycastHitStruct` - Collision detection results

### Unsafe Code

The project enables unsafe code blocks (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`) for direct memory access when interfacing with the game client.

### Output Configuration

Build outputs are redirected to `..\..\Bot` directory for integration with the main bot application.

## Integration

This library is designed to be used by:

- **Bot automation systems** for game state reading
- **Pathfinding services** for navigation
- **Decision engines** for AI behavior
- **UI automation** for interface interaction

## Contributing

When extending this library:

1. Follow existing naming conventions
2. Add comprehensive XML documentation
3. Include appropriate safety checks for unsafe operations
4. Maintain compatibility with existing interfaces
5. Add unit tests for new functionality

## License

This library is part of the BloogBot project. Refer to the main project license for usage terms and conditions.

---

**Note**: This library is designed for educational and research purposes. Users are responsible for ensuring compliance with game terms of service and applicable laws.