# GameData.Core

Core game data definitions and interfaces for the WWoW bot system. This library provides the shared type system used across all bot components, defining the contracts for game objects, frames, and enumerations.
GameData.Core is a .NET 8 library that provides core data structures, interfaces, and enumerations for World of Warcraft game data interaction. This library serves as the foundation for bot automation systems by offering strongly-typed representations of WoW game objects, player data, and game mechanics.

## Overview

GameData.Core is a foundational library that:
- **Defines Interfaces**: Contracts for all game object types (players, units, items, etc.)
- **Provides Enumerations**: Game constants like class types, races, spell schools, etc.
- **Models Data Structures**: Position, inventory, spells, and other game state
- **Abstracts Frames**: UI frame interfaces for vendor, quest, trainer interactions
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
??? Interfaces/
?   ??? IWoWObject.cs           # Base game object interface
?   ??? IWoWUnit.cs             # NPC/creature interface
?   ??? IWoWPlayer.cs           # Other player interface
?   ??? IWoWLocalPlayer.cs      # Controlled character interface
?   ??? IWoWLocalPet.cs         # Pet interface
?   ??? IWoWItem.cs             # Item interface
?   ??? IWoWContainer.cs        # Bag interface
?   ??? IWoWGameObject.cs       # World object interface
?   ??? IWoWDynamicObject.cs    # Dynamic effect interface
?   ??? IWoWCorpse.cs           # Corpse interface
?   ??? IObjectManager.cs       # Object registry interface
?   ??? IWoWEventHandler.cs     # Event system interface
?   ??? ISpell.cs               # Spell data interface
??? Frames/
?   ??? ILoginScreen.cs         # Login UI
?   ??? ICharacterSelectScreen.cs # Character selection
?   ??? IRealmSelectScreen.cs   # Realm selection
?   ??? IMerchantFrame.cs       # Vendor interaction
?   ??? IQuestFrame.cs          # Quest dialog
?   ??? IQuestGreetingFrame.cs  # Quest giver greeting
?   ??? ITrainerFrame.cs        # Class trainer
?   ??? ITalentFrame.cs         # Talent tree
?   ??? ITaxiFrame.cs           # Flight master
?   ??? ITradeFrame.cs          # Player trade
?   ??? ILootFrame.cs           # Loot window
?   ??? IGossipFrame.cs         # NPC dialog
?   ??? ICraftFrame.cs          # Profession crafting
??? Models/
?   ??? Position.cs             # 3D coordinates with utilities
?   ??? Spell.cs                # Spell definition
?   ??? SpellCastTargets.cs     # Spell targeting info
?   ??? Inventory.cs            # Inventory management
?   ??? ItemCacheInfo.cs        # Item template data
?   ??? SkillInfo.cs            # Skill levels
?   ??? QuestSlot.cs            # Quest log entry
?   ??? CharacterSelect.cs      # Character list entry
?   ??? Realm.cs                # Realm info
?   ??? HighGuid.cs             # GUID type extraction
?   ??? UpdateMask.cs           # Object update flags
?   ??? TargetInfo.cs           # Target selection data
??? Enums/
?   ??? Enums.cs                # Game enumerations (Class, Race, etc.)
?   ??? UpdateFields.cs         # Object field indices
?   ??? LiquidType.cs           # Water/lava/slime types
??? Constants/
    ??? Spellbook.cs            # Known spell IDs
    ??? RaceConstants.cs        # Race-specific data
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

## Key Interfaces
## Usage Examples

### IWoWObject
### Working with Positions

Base interface for all game objects:

```csharp
public interface IWoWObject
{
    ulong Guid { get; }
    Position Position { get; }
    float Facing { get; }
    ObjectType ObjectType { get; }
}
```
var playerPos = new Position(100.5f, 200.3f, 15.0f);
var targetPos = new Position(105.0f, 205.0f, 15.5f);

float distance = playerPos.DistanceTo(targetPos);
float distance2D = playerPos.DistanceTo2D(targetPos);

### IWoWUnit
// Vector operations
var direction = targetPos - playerPos;
var normalized = direction.GetNormalizedVector();
```

Interface for units (NPCs, creatures):
### Checking Unit States

```csharp
public interface IWoWUnit : IWoWObject
public void CheckUnitStatus(IWoWUnit unit)
{
    int Health { get; }
    int MaxHealth { get; }
    int Level { get; }
    ulong TargetGuid { get; }
    UnitFlags UnitFlags { get; }
    CreatureType CreatureType { get; }
    // ... more properties
}
```
    if (unit.IsInCombat)
        Console.WriteLine("Unit is in combat");
    
### IWoWLocalPlayer
    if (unit.IsMoving)
        Console.WriteLine("Unit is moving");
    
Extended interface for the controlled character:
    if (unit.HasBuff("Blessing of Might"))
        Console.WriteLine("Unit has blessing buff");
    
```csharp
public interface IWoWLocalPlayer : IWoWPlayer
{
    Inventory Inventory { get; }
    IEnumerable<ISpell> Spells { get; }
    IEnumerable<QuestSlot> QuestLog { get; }
    void CastSpell(int spellId);
    void MoveTo(Position target);
    // ... more methods
    var healthPercent = unit.HealthPercent;
    Console.WriteLine($"Health: {healthPercent}%");
}
```

### IObjectManager

Central registry for all game objects:
### Player Information

```csharp
public interface IObjectManager
public void DisplayPlayerInfo(IWoWPlayer player)
{
    IWoWLocalPlayer LocalPlayer { get; }
    IEnumerable<IWoWUnit> Units { get; }
    IEnumerable<IWoWPlayer> Players { get; }
    IEnumerable<IWoWGameObject> GameObjects { get; }
    IWoWObject GetObjectByGuid(ulong guid);
    Console.WriteLine($"Name: {player.Name}");
    Console.WriteLine($"Class: {player.Class}");
    Console.WriteLine($"Race: {player.Race}");
    Console.WriteLine($"Level: {player.Level}");
    Console.WriteLine($"Position: {player.Position}");
    Console.WriteLine($"Health: {player.Health}/{player.MaxHealth}");
    Console.WriteLine($"Mana: {player.Mana}/{player.MaxMana}");
}
```

## Models
## Dependencies

- **.NET 8.0** - Target framework
- **Newtonsoft.Json** (v13.0.3) - JSON serialization
- **BotCommLayer** - Communication layer (project reference)

## Technical Details

### Memory Layout

The library includes several `StructLayout` attributes for interoperability with native WoW memory structures:

### Position
- `XYZ` - 3D coordinate structure
- `TriangleStruct` - Navigation mesh triangles
- `RaycastHitStruct` - Collision detection results

3D coordinate with utility methods:
### Unsafe Code

```csharp
public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
The project enables unsafe code blocks (`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`) for direct memory access when interfacing with the game client.

    public float DistanceTo(Position other);
    public float AngleTo(Position other);
}
```
### Output Configuration

## Enumerations
Build outputs are redirected to `..\..\Bot` directory for integration with the main bot application.

Common game enums include:
## Integration

- `Class` - Warrior, Paladin, Hunter, etc.
- `Race` - Human, Orc, Dwarf, etc.
- `ObjectType` - Unit, Player, Item, GameObject, etc.
- `UnitFlags` - Combat, stunned, silenced, etc.
- `MovementFlags` - Forward, backward, swimming, flying, etc.
- `SpellSchool` - Physical, Holy, Fire, Nature, Frost, Shadow, Arcane
- `ItemQuality` - Poor, Common, Uncommon, Rare, Epic, Legendary
This library is designed to be used by:

## Dependencies
- **Bot automation systems** for game state reading
- **Pathfinding services** for navigation
- **Decision engines** for AI behavior
- **UI automation** for interface interaction

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3 | Serialization support |
## Contributing

## Project References
When extending this library:

- **BotCommLayer**: Protobuf message types for IPC
1. Follow existing naming conventions
2. Add comprehensive XML documentation
3. Include appropriate safety checks for unsafe operations
4. Maintain compatibility with existing interfaces
5. Add unit tests for new functionality

## Usage
## License

This library is referenced by:
- **WoWSharpClient**: Implements interfaces for network client
- **ForegroundBotRunner**: Implements interfaces for in-process bot
- **BotRunner**: Uses interfaces for behavior trees
- **BloogBot.AI**: Uses interfaces for AI decision making
This library is part of the BloogBot project. Refer to the main project license for usage terms and conditions.

## Related Documentation
---

- See `ARCHITECTURE.md` for system design
- See `Exports/WoWSharpClient/README.md` for network client implementation
**Note**: This library is designed for educational and research purposes. Users are responsible for ensuring compliance with game terms of service and applicable laws.