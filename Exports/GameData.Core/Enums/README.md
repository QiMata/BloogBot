# GameData.Core Enums

This directory contains comprehensive enumerations for World of Warcraft Classic game data. These enums provide strongly-typed representations of game constants, flags, and identifiers used throughout the WoW Classic client and server architecture.

## Overview

The enums in this library serve as the foundation for type-safe interaction with World of Warcraft game data. They cover all major aspects of the game including character attributes, spell systems, network protocols, item classifications, and UI interactions.

## Enum Categories

### Character & Player Data

#### **Character Attributes**
- **[Class.cs](Class.cs)** - Player classes (Warrior, Paladin, Hunter, Rogue, Priest, Shaman, Mage, Warlock, Druid)
- **[Race.cs](Race.cs)** - Player races with Description attributes (Human, Orc, Dwarf, Night Elf, Undead, Tauren, Gnome, Troll)
- **[Gender.cs](Gender.cs)** - Character gender enumeration

#### **Character States & Flags**
- **[CharacterFlags.cs](CharacterFlags.cs)** - Character status flags
- **[AtLoginFlags.cs](AtLoginFlags.cs)** - Login state flags
- **[PlayerFlags.cs](PlayerFlags.cs)** - Player state flags
- **[PlayerFieldByteFlags.cs](PlayerFieldByteFlags.cs)** - Player field byte flags
- **[PlayerFieldByte2Flags.cs](PlayerFieldByte2Flags.cs)** - Extended player field flags
- **[PlayerExtraFlags.cs](PlayerExtraFlags.cs)** - Additional player flags

### Unit & Combat System

#### **Unit Flags & States**
- **[UnitFlags.cs](UnitFlags.cs)** - Comprehensive unit flags (attackable, PvP, combat states, etc.)
- **[NPCFlags.cs](NPCFlags.cs)** - NPC interaction flags (vendor, trainer, quest giver, etc.)
- **[UnitState.cs](UnitState.cs)** - Unit state enumeration
- **[DeathState.cs](DeathState.cs)** - Unit death states
- **[UnitStandStateType.cs](UnitStandStateType.cs)** - Unit stance types

#### **Movement & Navigation**
- **[MovementFlags.cs](MovementFlags.cs)** - Movement state flags
- **[MovementFlags2.cs](MovementFlags2.cs)** - Extended movement flags
- **[UnitMoveType.cs](UnitMoveType.cs)** - Movement types

#### **Combat & Damage**
- **[Powers.cs](Powers.cs)** - Power types (Mana, Rage, Focus, Energy, Happiness)
- **[DamageType.cs](DamageType.cs)** - Damage classifications
- **[VictimState.cs](VictimState.cs)** - Combat victim states
- **[HitInfo.cs](HitInfo.cs)** - Combat hit information flags
- **[MeleeHitOutcome.cs](MeleeHitOutcome.cs)** - Melee combat outcomes

### Spell & Magic System

#### **Spell Mechanics**
- **[SpellTrigger.cs](SpellTrigger.cs)** - Spell trigger types
- **[EffectType.cs](EffectType.cs)** - Spell effect classifications
- **[SpellModOp.cs](SpellModOp.cs)** - Spell modification operations
- **[SpellModType.cs](SpellModType.cs)** - Spell modifier types

#### **Spell Interrupts & Auras**
- **[SpellInterruptFlags.cs](SpellInterruptFlags.cs)** - Spell interrupt conditions
- **[SpellChannelInterruptFlags.cs](SpellChannelInterruptFlags.cs)** - Channel interrupt flags
- **[SpellAuraInterruptFlags.cs](SpellAuraInterruptFlags.cs)** - Aura interrupt conditions
- **[UnitAuraFlags.cs](UnitAuraFlags.cs)** - Aura state flags

### Item & Equipment System

#### **Item Classifications**
- **[ItemQuality.cs](ItemQuality.cs)** - Item quality tiers (Poor, Common, Uncommon, Rare, Epic, Legendary)
- **[ItemClass.cs](ItemClass.cs)** - Main item categories
- **[ItemSubclass.cs](ItemSubclass.cs)** - Item subcategories
- **[InventoryType.cs](InventoryType.cs)** - Equipment slot types

#### **Inventory & Equipment**
- **[EquipSlot.cs](EquipSlot.cs)** - Equipment slot enumeration
- **[InventorySlot.cs](InventorySlot.cs)** - Inventory slot types
- **[PlayerSlots.cs](PlayerSlots.cs)** - Player inventory slots
- **[EquipmentSlots.cs](EquipmentSlots.cs)** - Equipment slot definitions
- **[InventorySlots.cs](InventorySlots.cs)** - Inventory slot definitions
- **[BankItemSlots.cs](BankItemSlots.cs)** - Bank storage slots

#### **Item States & Mechanics**
- **[Bonding.cs](Bonding.cs)** - Item binding types
- **[ItemUpdateState.cs](ItemUpdateState.cs)** - Item update states
- **[ItemDynFlags.cs](ItemDynFlags.cs)** - Dynamic item flags
- **[EnchantmentSlot.cs](EnchantmentSlot.cs)** - Item enchantment slots

### Network & Communication

#### **Network Protocol**
- **[Opcode.cs](Opcode.cs)** - Complete WoW Classic network opcode definitions (500+ opcodes)
- **[ResponseCode.cs](ResponseCode.cs)** - Server response codes

#### **Chat & Communication**
- **[ChatMsg.cs](ChatMsg.cs)** - Chat message types (say, party, guild, whisper, etc.)
- **[Language.cs](Language.cs)** - In-game language types
- **[PlayerChatTag.cs](PlayerChatTag.cs)** - Player chat tags

### Object System

#### **Object Types & Updates**
- **[WoWObjectType.cs](WoWObjectType.cs)** - Game object type classifications
- **[ObjectUpdateType.cs](ObjectUpdateType.cs)** - Object update types
- **[ObjectUpdateFlags.cs](ObjectUpdateFlags.cs)** - Object update flags
- **[UpdateFields.cs](UpdateFields.cs)** - Object field update definitions

#### **High-Level Objects**
- **[HighGuidFlag.cs](HighGuidFlag.cs)** - GUID type flags
- **[DynamicObjectType.cs](DynamicObjectType.cs)** - Dynamic object classifications
- **[CorpseType.cs](CorpseType.cs)** - Corpse type enumeration
- **[CorpseFlags.cs](CorpseFlags.cs)** - Corpse state flags

### UI & Interaction

#### **Game States**
- **[QuestFrameState.cs](QuestFrameState.cs)** - Quest UI states
- **[MerchantState.cs](MerchantState.cs)** - Merchant window states
- **[LootState.cs](LootState.cs)** - Loot window states
- **[LoginStates.cs](LoginStates.cs)** - Login process states

#### **Emotes & Actions**
- **[Emote.cs](Emote.cs)** - Character emote definitions
- **[TextEmote.cs](TextEmote.cs)** - Text-based emotes

### Skills & Progression

#### **Skills & Stats**
- **[Skills.cs](Skills.cs)** - Character skill definitions
- **[StatType.cs](StatType.cs)** - Character statistic types
- **[UnitMods.cs](UnitMods.cs)** - Unit modifier types

### Pet & Companion System

#### **Pet Mechanics**
- **[PetType.cs](PetType.cs)** - Pet classifications
- **[PetModeFlags.cs](PetModeFlags.cs)** - Pet behavior flags
- **[HappinessState.cs](HappinessState.cs)** - Pet happiness levels
- **[LoyaltyLevel.cs](LoyaltyLevel.cs)** - Pet loyalty states
- **[PetSpellState.cs](PetSpellState.cs)** - Pet spell states
- **[PetTalk.cs](PetTalk.cs)** - Pet communication types

### Transaction & Economy

#### **Trading & Commerce**
- **[BuyResult.cs](BuyResult.cs)** - Purchase transaction results
- **[SellResult.cs](SellResult.cs)** - Sale transaction results
- **[InventoryResult.cs](InventoryResult.cs)** - Inventory operation results

### Miscellaneous Systems

#### **Environment & World**
- **[EnvironmentalDamageType.cs](EnvironmentalDamageType.cs)** - Environmental damage sources
- **[RestType.cs](RestType.cs)** - Character rest states
- **[Role.cs](Role.cs)** - Character roles

## Helper Utilities

### **[EnumCustomAttributeHelper.cs](EnumCustomAttributeHelper.cs)**
Provides extension methods for working with enum custom attributes:

```csharp
public static string GetDescription(this Enum value)
```

This helper enables retrieving Description attribute values from enums, particularly useful with the Race enum:

```csharp
Race.NightElf.GetDescription(); // Returns "Night Elf"
```

## Usage Examples

### Basic Enum Usage
```csharp
using GameData.Core.Enums;

// Character creation
var playerClass = Class.Warrior;
var playerRace = Race.Human;
var gender = Gender.Male;

// Check unit flags
if (unit.Flags.HasFlag(UnitFlags.UNIT_FLAG_IN_COMBAT))
{
    // Unit is in combat
}

// Network message handling
if (opcode == Opcode.SMSG_CHAR_ENUM)
{
    // Handle character enumeration
}
```

### Using Description Attributes
```csharp
// Get user-friendly race names
string raceName = Race.NightElf.GetDescription(); // "Night Elf"
string humanName = Race.Human.GetDescription();   // "Human"
```

### Flag Operations
```csharp
// Working with flags
var unitFlags = UnitFlags.UNIT_FLAG_PVP | UnitFlags.UNIT_FLAG_IN_COMBAT;

// Check multiple flags
bool isPvpCombat = unitFlags.HasFlag(UnitFlags.UNIT_FLAG_PVP) && 
                   unitFlags.HasFlag(UnitFlags.UNIT_FLAG_IN_COMBAT);
```

## Design Patterns

### Namespace Organization
All enums are contained within the `GameData.Core.Enums` namespace for clear organization and to avoid naming conflicts.

### Explicit Values
Many enums include explicit numeric values that correspond to the actual values used in the WoW Classic protocol and data structures.

### Flag Enums
Enums representing bitwise flags are marked with the `[Flags]` attribute and use powers of 2 for their values.

### Custom Attributes
Some enums (like Race) include `[Description]` attributes for user-friendly display names.

## Integration

These enums integrate seamlessly with:
- **Network Protocol Handling** - Opcodes and message types
- **Object Management** - Type-safe object classifications
- **UI Systems** - State management and user interactions
- **Game Logic** - Combat, spells, items, and character progression
- **Data Persistence** - Character and world state serialization

## Version Compatibility

These enums are specifically designed for **World of Warcraft Classic (Vanilla 1.12.x)**. The values and definitions match the original game client and server implementations.

## Contributing

When adding new enums or modifying existing ones:

1. Ensure values match the original WoW Classic specifications
2. Add appropriate documentation comments
3. Use consistent naming conventions
4. Include `[Flags]` attribute for bitwise enums
5. Add `[Description]` attributes where user-friendly names are needed
6. Maintain alphabetical organization within categories

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*