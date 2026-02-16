# Transport Protocol — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `Transports/Transport.cpp`, `Objects/MovementInfo.h`, `Handlers/MovementHandler.cpp`.

## Overview

Transports are moving GameObjects (ships, zeppelins, elevators) that carry passengers.
Entities on a transport store their position as a **local offset** relative to the transport's
origin, not in world coordinates. The transport itself moves along a predefined spline path.

## Transport GUIDs

| HighGuid | Value | Description |
|----------|-------|-------------|
| HIGHGUID_TRANSPORT | 0xF120 | Static transport (elevator, etc.) |
| HIGHGUID_MO_TRANSPORT | 0x1FC0 | Moving transport (ship, zeppelin) |

GUID structure: `(highGuid << 48) | lowGuid`

## Movement Flag

```
MOVEFLAG_ONTRANSPORT = 0x02000000  (bit 25)
```

When set in a player's `moveFlags`, the movement packet includes transport fields.

## Packet Format

When `MOVEFLAG_ONTRANSPORT` is set in `MovementInfo`:

```
uint32       moveFlags;          // Includes MOVEFLAG_ONTRANSPORT
uint32       timestamp;
float        worldX;             // Player's world X
float        worldY;             // Player's world Y
float        worldZ;             // Player's world Z
float        worldOrientation;   // Player's world facing

// Transport fields (only when MOVEFLAG_ONTRANSPORT is set):
ObjectGuid   transportGuid;      // Packed GUID of the transport
float        localX;             // Offset X on transport
float        localY;             // Offset Y on transport
float        localZ;             // Offset Z on transport
float        localOrientation;   // Offset orientation on transport

// ... remaining conditional fields (swimming, fall, jump, spline) ...
```

## Coordinate Transformation

### Local → World (CalculatePassengerPosition)

Given transport world position `(t_x, t_y, t_z, t_o)` and local offset `(l_x, l_y, l_z, l_o)`:

```
worldX = t_x + l_x * cos(t_o) - l_y * sin(t_o)
worldY = t_y + l_y * cos(t_o) + l_x * sin(t_o)
worldZ = t_z + l_z
worldO = normalize(t_o + l_o)
```

**Key insight:** Only X and Y are rotated by the transport's orientation. Z is simply added.

### World → Local (CalculatePassengerOffset)

Inverse transformation to compute local offset from world position:

```
// Remove translation
dx = worldX - t_x
dy = worldY - t_y
dz = worldZ - t_z

// Inverse rotation
localX = dx * cos(t_o) + dy * sin(t_o)
localY = dy * cos(t_o) - dx * sin(t_o)
localZ = dz
localO = normalize(worldO - t_o)
```

## Boarding / Disembarking

### Client-Side Boarding

1. Client detects collision with transport
2. Client sets `MOVEFLAG_ONTRANSPORT` in `moveFlags`
3. Client computes local offset from transport position
4. Client sends movement packet with transport GUID + local offset

### Server Processing (HandleMoverRelocation)

When server receives a movement packet with `MOVEFLAG_ONTRANSPORT`:

```
IF player has no transport AND MOVEFLAG_ONTRANSPORT is set:
    → Look up transport by GUID from the movement packet
    → Call transport.AddPassenger(player)
    → Set player.m_transport = transport
    → Set MOVEFLAG_ONTRANSPORT on server-side movement info

IF player has transport AND MOVEFLAG_ONTRANSPORT is NOT set:
    → Call transport.RemovePassenger(player)
    → Clear player.m_transport
    → Clear transport data from movement info
```

### Server-Side AddPassenger

```cpp
transport.AddPassenger(unit):
    1. Add unit to transport's PassengerSet
    2. Set unit.m_transport = this
    3. Set MOVEFLAG_ONTRANSPORT on unit
    4. Store transport GUID in unit.movementInfo.t_guid
    5. IF coordinates need adjusting:
       → Copy unit's world position to t_pos
       → Call CalculatePassengerOffset() to convert to local coords
```

## Transport Position Updates

The server updates transport position every **50ms**:

```
Transport.Update():
    1. Evaluate spline path at current time → new (x, y, z)
    2. Compute orientation from spline derivative → atan2(dir.y, dir.x) + π
    3. Call UpdatePosition(x, y, z, o)

UpdatePosition(x, y, z, o):
    1. Relocate transport to new world position
    2. Update collision model position
    3. For each passenger in PassengerSet:
       → Read passenger's local offset (t_pos)
       → Call CalculatePassengerPosition() to get new world position
       → Relocate passenger on the map
```

## Transport Map Changes (Teleportation)

When a transport crosses a map boundary (e.g., ship arriving at a continent):

```
TeleportTransport(newMapId, x, y, z, o):
    For each passenger:
        1. Read passenger's local offset
        2. Calculate new world position using new transport position
        3. IF same map: relocate passenger
        4. IF different map: teleport passenger to new map
           → Player.TeleportTo(newMap, worldX, worldY, worldZ, worldO, TELE_TO_NOT_LEAVE_TRANSPORT)

    Relocate transport to new map position
```

**Important:** `TELE_TO_NOT_LEAVE_TRANSPORT` flag prevents the teleport from clearing the
passenger's transport state.

## Validation

The server validates transport offsets:

```
IF MOVEFLAG_ONTRANSPORT is set:
    Reject if |localX| > 250
    Reject if |localY| > 250
    Reject if |localZ| > 100
```

These bounds represent the maximum size of any transport in the game.

## Transport Path Progress

Transports include path progress in their update data:

```
// In SMSG_UPDATE_OBJECT with UPDATEFLAG_TRANSPORT (0x02):
uint32  pathProgress;        // Current position along the spline path (milliseconds)
```

This allows clients to interpolate the transport's position between server updates.

## SMSG_MONSTER_MOVE_TRANSPORT (0x2AE)

Used for NPC movement on transports:

```
PackedGUID  moverGuid;
PackedGUID  transportGuid;   // Transport the NPC is on
float       localX, localY, localZ;  // Local coordinates on transport
// ... standard SMSG_MONSTER_MOVE fields ...
```

## Client Memory Layout

Transport offset fields in `CMovementInfo` (unverified — see `MEMORY.md`):

```
TransportX     = playerObj + 0x9D0  (base + 0x28)
TransportY     = playerObj + 0x9D4  (base + 0x2C)
TransportZ     = playerObj + 0x9D8  (base + 0x30)
TransportO     = playerObj + 0x9DC  (base + 0x34)
TransportGUID  = playerObj + 0x9E0  (base + 0x38, uint64)
```
