# GameData.Core

Core game data definitions and interfaces for the WWoW (Westworld of Warcraft) bot system.

## Overview

GameData.Core is a foundational .NET 8 library that provides the shared type system used across all bot components. It defines:

- **Interfaces**: Contracts for all game object types (players, units, items, etc.)
- **Enumerations**: Game constants like class types, races, spell schools, etc.
- **Models**: Position, inventory, spells, and other game state structures
- **Frames**: UI frame interfaces for vendor, quest, trainer interactions

This library serves as the foundation for bot automation systems by offering strongly-typed representations of WoW game objects, player data, and game mechanics.

## Architecture

```
+------------------------------------------------------------------+
|                         GameData.Core                             |
+------------------------------------------------------------------+
|                                                                   |
|  +------------------------+    +-----------------------------+   |
|  |      Interfaces        |    |         Models              |   |
|  |                        |    |                             |   |
|  |  IWoWObject (base)     |    |  Position (3D coords)       |   |
|  |  IWoWUnit              |    |  Spell                      |   |
|  |  IWoWPlayer            |    |  Inventory                  |   |
|  |  IWoWLocalPlayer       |    |  HighGuid                   |   |
|  |  IWoWItem              |    |  CharacterSelect            |   |
|  |  IObjectManager        |    |  QuestSlot                  |   |
|  +------------------------+    +-----------------------------+   |
|                                                                   |
|  +------------------------+    +-----------------------------+   |
|  |        Frames          |    |       Enumerations          |   |
|  |                        |    |                             |   |
|  |  IQuestFrame           |    |  Class, Race, Gender        |   |
|  |  IMerchantFrame        |    |  ObjectType, UnitFlags      |   |
|  |  ITrainerFrame         |    |  MovementFlags              |   |
|  |  ITaxiFrame            |    |  SpellSchool, ItemQuality   |   |
|  |  ILootFrame            |    |  Opcode, ChatMsg            |   |
|  +------------------------+    +-----------------------------+   |
|                                                                   |
+------------------------------------------------------------------+
```

## Project Structure

```
GameData.Core/
+-- Interfaces/
|   +-- IWoWObject.cs           # Base game object interface
|   +-- IWoWUnit.cs             # NPC/creature interface
|   +-- IWoWPlayer.cs           # Other player interface
|   +-- IWoWLocalPlayer.cs      # Controlled character interface
|   +-- IWoWLocalPet.cs         # Pet interface
|   +-- IWoWItem.cs             # Item interface
|   +-- IWoWContainer.cs        # Bag interface
|   +-- IWoWGameObject.cs       # World object interface
|   +-- IWoWDynamicObject.cs    # Dynamic effect interface
|   +-- IWoWCorpse.cs           # Corpse interface
|   +-- IObjectManager.cs       # Object registry interface
|   +-- IWoWEventHandler.cs     # Event system interface
|   +-- ISpell.cs               # Spell data interface
+-- Frames/
|   +-- ILoginScreen.cs         # Login UI
|   +-- ICharacterSelectScreen.cs # Character selection
|   +-- IRealmSelectScreen.cs   # Realm selection
|   +-- IMerchantFrame.cs       # Vendor interaction
|   +-- IQuestFrame.cs          # Quest dialog
|   +-- IQuestGreetingFrame.cs  # Quest giver greeting
|   +-- ITrainerFrame.cs        # Class trainer
|   +-- ITalentFrame.cs         # Talent tree
|   +-- ITaxiFrame.cs           # Flight master
|   +-- ITradeFrame.cs          # Player trade
|   +-- ILootFrame.cs           # Loot window
|   +-- IGossipFrame.cs         # NPC dialog
|   +-- ICraftFrame.cs          # Profession crafting
+-- Models/
|   +-- Position.cs             # 3D coordinates with utilities
|   +-- Spell.cs                # Spell definition
|   +-- SpellCastTargets.cs     # Spell targeting info
|   +-- Inventory.cs            # Inventory management
|   +-- ItemCacheInfo.cs        # Item template data
|   +-- SkillInfo.cs            # Skill levels
|   +-- QuestSlot.cs            # Quest log entry
|   +-- CharacterSelect.cs      # Character list entry
|   +-- Realm.cs                # Realm info
|   +-- HighGuid.cs             # GUID type extraction
|   +-- UpdateMask.cs           # Object update flags
|   +-- TargetInfo.cs           # Target selection data
+-- Enums/
|   +-- Enums.cs                # Game enumerations (Class, Race, etc.)
|   +-- UpdateFields.cs         # Object field indices
|   +-- LiquidType.cs           # Water/lava/slime types
+-- Constants/
    +-- Spellbook.cs            # Known spell IDs
    +-- RaceConstants.cs        # Race-specific data
```

## Key Interfaces

### IWoWObject

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

### IWoWUnit

Interface for units (NPCs, creatures):

```csharp
public interface IWoWUnit : IWoWObject
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

### IWoWLocalPlayer

Extended interface for the controlled character:

```csharp
public interface IWoWLocalPlayer : IWoWPlayer
{
    Inventory Inventory { get; }
    IEnumerable<ISpell> Spells { get; }
    IEnumerable<QuestSlot> QuestLog { get; }
    void CastSpell(int spellId);
    void MoveTo(Position target);
    // ... more methods
}
```

### IObjectManager

Central registry for all game objects:

```csharp
public interface IObjectManager
{
    IWoWLocalPlayer LocalPlayer { get; }
    IEnumerable<IWoWUnit> Units { get; }
    IEnumerable<IWoWPlayer> Players { get; }
    IEnumerable<IWoWGameObject> GameObjects { get; }
    IWoWObject GetObjectByGuid(ulong guid);
}
```

## Game Object Hierarchy

```
IWoWObject (base)
+-- IWoWUnit (NPCs, creatures)
|   +-- IWoWPlayer (other players)
|       +-- IWoWLocalPlayer (controlled character)
+-- IWoWItem
+-- IWoWContainer (bags)
+-- IWoWGameObject (world objects)
+-- IWoWDynamicObject
+-- IWoWCorpse
```

## Models

### Position

3D coordinate with utility methods:

```csharp
public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public float DistanceTo(Position other);
    public float DistanceTo2D(Position other);
    public float AngleTo(Position other);
    public Position GetNormalizedVector();
}
```

## Enumerations

Common game enums include:

- `Class` - Warrior, Paladin, Hunter, etc.
- `Race` - Human, Orc, Dwarf, etc.
- `ObjectType` - Unit, Player, Item, GameObject, etc.
- `UnitFlags` - Combat, stunned, silenced, etc.
- `MovementFlags` - Forward, backward, swimming, flying, etc.
- `SpellSchool` - Physical, Holy, Fire, Nature, Frost, Shadow, Arcane
- `ItemQuality` - Poor, Common, Uncommon, Rare, Epic, Legendary

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

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3 | Serialization support |

## Project References

- **BotCommLayer**: Protobuf message types for IPC

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

This library is referenced by:

- **WoWSharpClient**: Implements interfaces for network client
- **ForegroundBotRunner**: Implements interfaces for in-process bot
- **BotRunner**: Uses interfaces for behavior trees
- **WWoW.AI**: Uses interfaces for AI decision making

## Related Documentation

- See `ARCHITECTURE.md` for system design
- See `Exports/WoWSharpClient/README.md` for network client implementation

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
