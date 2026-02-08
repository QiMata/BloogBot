# Object Update Protocol — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `Objects/Object.cpp`, `Objects/UpdateData.h`, `Objects/UpdateFields_1_12_1.h`.

## Overview

Object updates are sent via `SMSG_UPDATE_OBJECT` (0xA9) or `SMSG_COMPRESSED_UPDATE_OBJECT` (0x1F6, zlib-compressed).
Each packet contains one or more **update blocks**, each describing a create, values change, movement change, or destruction.

## Packet Structure

```
SMSG_UPDATE_OBJECT:
    uint32  blockCount;          // Number of update blocks
    [UpdateBlock] * blockCount;  // Consecutive update blocks

SMSG_COMPRESSED_UPDATE_OBJECT:
    uint32  uncompressedSize;    // Size before compression
    uint8[] compressedData;      // zlib-compressed SMSG_UPDATE_OBJECT payload
```

## Update Types (ObjectUpdateType)

| Value | Name | Description |
|-------|------|-------------|
| 0 | UPDATETYPE_VALUES | Field values changed (descriptors only) |
| 1 | UPDATETYPE_MOVEMENT | Movement/position update only |
| 2 | UPDATETYPE_CREATE_OBJECT | Object created (normal) |
| 3 | UPDATETYPE_CREATE_OBJECT2 | Object created (self/new) |
| 4 | UPDATETYPE_OUT_OF_RANGE_OBJECTS | Objects leaving visibility |
| 5 | UPDATETYPE_NEAR_OBJECTS | Objects entering visibility |

## Update Flags (ObjectUpdateFlags)

| Flag | Value | Description |
|------|-------|-------------|
| UPDATEFLAG_SELF | 0x01 | Update is for the viewing player |
| UPDATEFLAG_TRANSPORT | 0x02 | Object is a transport (includes path progress) |
| UPDATEFLAG_MELEE_ATTACKING | 0x04 | Unit is in melee combat (victim GUID included) |
| UPDATEFLAG_HIGHGUID | 0x08 | High GUID data present (uint32) |
| UPDATEFLAG_ALL | 0x10 | "All" marker present (uint32 = 1) |
| UPDATEFLAG_LIVING | 0x20 | Full MovementInfo + 6 speeds |
| UPDATEFLAG_HAS_POSITION | 0x40 | Static position (x, y, z, o) |

## Object Type IDs

| Value | Name | Base Fields | Description |
|-------|------|-------------|-------------|
| 0 | TYPEID_OBJECT | 6 | Base object |
| 1 | TYPEID_ITEM | 0x30 (48) | Item in inventory |
| 2 | TYPEID_CONTAINER | 0x74 (116) | Bag/container |
| 3 | TYPEID_UNIT | 0xBC (188) | NPC creature |
| 4 | TYPEID_PLAYER | 0x4FC (1276) | Player character |
| 5 | TYPEID_GAMEOBJECT | 0x20 (32) | World object (chest, door, etc.) |
| 6 | TYPEID_DYNAMICOBJECT | 0x10 (16) | AOE effect, trap |
| 7 | TYPEID_CORPSE | 0x26 (38) | Player corpse |

---

## Update Block Formats

### UPDATETYPE_VALUES (0)

Updates descriptor field values for an existing object.

```
uint8       updateType;      // 0
PackedGUID  objectGuid;      // Target object
[ValuesBlock]                // Changed field values
```

### UPDATETYPE_MOVEMENT (1)

Updates only the movement/position of an existing object.

```
uint8       updateType;      // 1
PackedGUID  objectGuid;      // Target object
[MovementBlock]              // Movement data
```

### UPDATETYPE_CREATE_OBJECT (2) / CREATE_OBJECT2 (3)

Creates a new object with movement and initial field values.
Type 3 is used for the player's own character and newly spawned objects.

```
uint8       updateType;      // 2 or 3
PackedGUID  objectGuid;      // New object
uint8       objectTypeId;    // TYPEID_* from table above
[MovementBlock]              // Position + movement state
[ValuesBlock]                // All initial field values
```

### UPDATETYPE_OUT_OF_RANGE_OBJECTS (4)

Notifies client to remove objects from its cache.

```
uint8       updateType;      // 4
uint32      guidCount;       // Number of GUIDs
PackedGUID[] guids;          // GUIDs to remove
```

### UPDATETYPE_NEAR_OBJECTS (5)

Notifies client about objects entering visibility (GUIDs only, create packets follow).

```
uint8       updateType;      // 5
uint32      guidCount;       // Number of GUIDs
PackedGUID[] guids;          // GUIDs entering range
```

---

## PackedGUID Format

Variable-length GUID encoding to save bandwidth (most GUIDs have zero high bytes).

```
uint8  mask;                 // Bitmask: bit N set = byte N is non-zero
uint8  bytes[popcount(mask)]; // Only non-zero bytes, in order
```

Example: GUID `0x0000000000000005`
- Non-zero byte at position 0 (value 0x05)
- Mask = `0x01`, data = `[0x05]` → 2 bytes total instead of 8

## MovementBlock

The format depends on `updateFlags`:

### With UPDATEFLAG_LIVING (0x20) — Units & Players

```
uint8       updateFlags;     // Flag byte
[MovementInfo]:              // Serialized via MovementInfo::Write()
    uint32  moveFlags;       // See movement-protocol.md
    uint32  serverTime;      // Server timestamp (ms)
    float   posX;
    float   posY;
    float   posZ;
    float   orientation;
    // IF moveFlags & ONTRANSPORT (0x02000000):
        ObjectGuid transportGuid;
        float   transportX;
        float   transportY;
        float   transportZ;
        float   transportO;
    // IF moveFlags & SWIMMING (0x00200000):
        float   swimPitch;
    uint32  fallTime;
    // IF moveFlags & JUMPING (0x00002000):
        float   jumpZSpeed;
        float   jumpCosAngle;
        float   jumpSinAngle;
        float   jumpXYSpeed;
    // IF moveFlags & SPLINE_ELEVATION (0x04000000):
        float   splineElevation;
// Speeds (always present with LIVING):
float   walkSpeed;           // Default 2.5
float   runSpeed;            // Default 7.0
float   runBackSpeed;        // Default 4.5
float   swimSpeed;           // Default 4.722
float   swimBackSpeed;       // Default 2.5
float   turnRate;            // Default π (3.14159)
// IF moveFlags & SPLINE_ENABLED:
    [SplineData]             // Movement spline points
```

### With UPDATEFLAG_HAS_POSITION (0x40) — GameObjects

```
uint8       updateFlags;     // Flag byte
float       posX;
float       posY;
float       posZ;
float       orientation;
```

### Additional flag data (appended after position):

```
// IF UPDATEFLAG_HIGHGUID (0x08):
    uint32  highGuidUnk;     // Always 0

// IF UPDATEFLAG_ALL (0x10):
    uint32  allFlag;         // Always 1

// IF UPDATEFLAG_MELEE_ATTACKING (0x04):
    PackedGUID victimGuid;   // Current melee target

// IF UPDATEFLAG_TRANSPORT (0x02):
    uint32  pathProgress;    // Transport spline progress (ms)
```

---

## ValuesBlock

Descriptor field values using a bitmask to indicate which fields are present.

```
uint8       blockCount;      // Number of 32-bit mask blocks
uint32[]    updateMask;      // blockCount × uint32 bitmask
uint32[]    values;          // One uint32 per set bit in mask
```

**Reading algorithm:**
1. Read `blockCount` (uint8)
2. Read `blockCount` × uint32 as the bitmask
3. For each bit set in the bitmask (scanning from bit 0):
   - Read one uint32 value
   - Store at the field index corresponding to that bit

**Field indices** are defined in `update-fields-1.12.1.md`. Each field is exactly 4 bytes (uint32).
64-bit values (like GUIDs) span two consecutive field indices (low word first).

### Special Field Handling

Some fields are modified before sending for visibility/security:

| Field | Modification |
|-------|-------------|
| UNIT_NPC_FLAGS | Filtered based on quest state, vendor availability |
| UNIT_FIELD_FLAGS | GM sees UNIT_FLAG_AURAS_VISIBLE added |
| UNIT_DYNAMIC_FLAGS | Loot/tracking flags filtered per player |
| UNIT_FIELD_HEALTH / MAXHEALTH | May show percentage instead of actual value |
| UNIT_FIELD_FACTIONTEMPLATE | Adjusted in raid encounters |
| GAMEOBJECT_DYN_FLAGS | Quest activation flags per player |
| UNIT_FIELD_BASEATTACKTIME | Clamped to ≥ 0 |
| UNIT_MOD_CAST_SPEED | Clamped to ≥ 0.001 |

---

## SMSG_DESTROY_OBJECT (0xAA)

Sent when an object is removed from the world.

```
uint64  guid;                // Full 8-byte GUID (not packed)
```

---

## Typical Flows

### Player Enters World
1. Server sends `SMSG_UPDATE_OBJECT` with `UPDATETYPE_CREATE_OBJECT2` for the player (self)
   - updateFlags: SELF | LIVING | ALL
   - Full MovementInfo + all descriptor fields
2. Server sends `SMSG_UPDATE_OBJECT` with `UPDATETYPE_CREATE_OBJECT` for all nearby objects
   - Units: updateFlags: LIVING (+ MELEE_ATTACKING if in combat)
   - GameObjects: updateFlags: HAS_POSITION (+ TRANSPORT if transport)

### Field Value Change
1. Server sends `SMSG_UPDATE_OBJECT` with `UPDATETYPE_VALUES`
   - Only changed fields in the bitmask
   - Example: health change → only UNIT_FIELD_HEALTH bit set

### Object Leaves Range
1. Server sends `SMSG_UPDATE_OBJECT` with `UPDATETYPE_OUT_OF_RANGE_OBJECTS`
   - List of GUIDs to remove from client cache

### Movement Update (server-initiated)
1. Server sends `SMSG_UPDATE_OBJECT` with `UPDATETYPE_MOVEMENT`
   - Full MovementInfo for the object
   - Used for teleports, forced position updates
