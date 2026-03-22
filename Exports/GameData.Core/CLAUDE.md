# GameData.Core — Interface Layer (Zero Dependencies)

Core interface library defining all game object contracts. **Zero project dependencies** — only NuGet refs (Newtonsoft.Json, Serilog). Every service in the solution depends on this.

## Directory Structure

| Directory | Files | Purpose |
|-----------|-------|---------|
| `Interfaces/` | 15 | Game object contracts (IObjectManager, IWoWUnit, IWoWLocalPlayer, etc.) |
| `Frames/` | 13 | UI frame interfaces (IMerchantFrame, IQuestFrame, ITrainerFrame, etc.) |
| `Models/` | 13 | Data structures (Position, Spell, HighGuid, ItemCacheInfo, etc.) |
| `Enums/` | 159 | Game constants (Opcode, MovementFlags, UnitFlags, Class, Race, etc.) |
| `Constants/` | 3 | Static spell books, race data, spell definitions |

**Total: ~203 .cs files**

## Interface Hierarchy

```
IWoWObject (base: Guid, ObjectType, Position, Facing)
├── IWoWGameObject (Entry, DisplayId, Interact())
│   ├── IWoWUnit (Health, Mana, Target, Auras, Combat, Movement — 90+ properties)
│   │   ├── IWoWPlayer (Race, Class, Level, Inventory, Skills, QuestLog)
│   │   │   └── IWoWLocalPlayer (Corpse, Debuffs, ComboPoints, Copper)
│   │   └── IWoWLocalPet
│   └── IWoWItem (ItemId, Quantity, Quality, Durability, Use())
│       └── IWoWContainer (NumOfSlots, GetItemGuid())
├── IWoWCorpse
└── IWoWDynamicObject
```

## Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IObjectManager` | Central registry — 200+ methods: object collections, movement, combat, inventory, NPC interaction, spell/skill access, UI frames |
| `IWoWUnit` | NPCs/creatures — health, mana, target, auras, casting, movement flags, speeds |
| `IWoWLocalPlayer` | Controlled character — corpse lifecycle, debuff flags, combo points, copper |
| `IWoWEventHandler` | 80+ events: connection, combat, loot, NPC, spells, quests, party, chat |
| `ILoginScreen` | Login credentials + IsLoggedIn state |
| `IRealmSelectScreen` | Realm list + selection |
| `IMerchantFrame` | Vendor buy/sell/repair |

## Key Models

| Model | Purpose |
|-------|---------|
| `Position` | 3D coordinates with distance/vector math, includes native structs (XYZ, RaycastHitStruct, CapsuleSweepRequestStruct) |
| `HighGuid` | 64-bit GUID split into high/low 32-bit parts (WoW protocol format) |
| `ItemCacheInfo` | Item template: class, subclass, quality, equip slot, required level |
| `Spell` | Spell definition: id, cost, name, description |
| `CharacterSelect` | Character list entry for login screen |

## Key Enums

| Enum | Purpose |
|------|---------|
| `Opcode` | 400+ packet opcodes (CMSG_*, SMSG_*) |
| `MovementFlags` | 32 flags: FORWARD, BACKWARD, JUMPING, FALLINGFAR, SWIMMING, etc. |
| `UnitFlags` | Combat, stunned, confused, fleeing, etc. |
| `WoWObjectType` | Item, Container, Unit, Player, GameObj, DynamicObj, Corpse |
| `WoWScreenState` | LoginScreen, Connecting, CharacterSelect, InWorld, etc. |
| `UpdateFields` | Object field indices for network updates |
| `NPCFlags` | Gossip, Questgiver, Vendor, Trainer, FlightMaster, etc. |

## Design Patterns

- **Dual implementation**: Interfaces support both FG (memory-mapped) and BG (packet-based) implementations
- **.NET 8 DIMs**: Default interface methods for convenience properties (HealthPercent, Mana, etc.)
- **No database access**: Purely in-memory state representation
- **AnyCPU target**: Compatible with x86 (FG/test host) and x64 (PathfindingService)
