# GameData.Core

Core game data definitions and interfaces for the WWoW bot system. This library provides the shared type system used across all bot components, defining the contracts for game objects, frames, and enumerations.

## Overview

GameData.Core is a foundational library that:
- **Defines Interfaces**: Contracts for all game object types (players, units, items, etc.)
- **Provides Enumerations**: Game constants like class types, races, spell schools, etc.
- **Models Data Structures**: Position, inventory, spells, and other game state
- **Abstracts Frames**: UI frame interfaces for vendor, quest, trainer interactions

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
    public float AngleTo(Position other);
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

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Newtonsoft.Json | 13.0.3 | Serialization support |

## Project References

- **BotCommLayer**: Protobuf message types for IPC

## Usage

This library is referenced by:
- **WoWSharpClient**: Implements interfaces for network client
- **ForegroundBotRunner**: Implements interfaces for in-process bot
- **BotRunner**: Uses interfaces for behavior trees
- **BloogBot.AI**: Uses interfaces for AI decision making

## Related Documentation

- See `ARCHITECTURE.md` for system design
- See `Exports/WoWSharpClient/README.md` for network client implementation
