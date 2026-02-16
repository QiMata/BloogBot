# Movement Protocol — WoW 1.12.1 (Build 5875)

> Parsed from MaNGOS `Objects/MovementInfo.h`, `Objects/Object.cpp`, `Handlers/MovementHandler.cpp`.

## Overview

Player movement uses a **client-authoritative** model: the client sends movement state changes,
and the server validates and broadcasts them to nearby players. Each movement packet carries
a full `MovementInfo` structure.

Movement opcodes are bidirectional (`MSG_MOVE_*`):
- **C→S**: Client sends its current movement state
- **S→C**: Server broadcasts the movement to other nearby players (prepends the mover's PackedGUID)

## Movement Flags (uint32)

| Flag | Value | Description |
|------|-------|-------------|
| MOVEFLAG_NONE | 0x00000000 | No movement |
| MOVEFLAG_FORWARD | 0x00000001 | Moving forward |
| MOVEFLAG_BACKWARD | 0x00000002 | Moving backward |
| MOVEFLAG_STRAFE_LEFT | 0x00000004 | Strafing left |
| MOVEFLAG_STRAFE_RIGHT | 0x00000008 | Strafing right |
| MOVEFLAG_TURN_LEFT | 0x00000010 | Turning left |
| MOVEFLAG_TURN_RIGHT | 0x00000020 | Turning right |
| MOVEFLAG_PITCH_UP | 0x00000040 | Pitching up (swimming) |
| MOVEFLAG_PITCH_DOWN | 0x00000080 | Pitching down (swimming) |
| MOVEFLAG_WALK_MODE | 0x00000100 | Walking (not running) |
| MOVEFLAG_LEVITATING | 0x00000400 | Levitating |
| MOVEFLAG_ROOT | 0x00000800 | Rooted (cannot move) |
| MOVEFLAG_FALLING | 0x00001000 | Falling |
| MOVEFLAG_FALLINGFAR | 0x00004000 | Falling from height |
| MOVEFLAG_SWIMMING | 0x00200000 | In water, swimming |
| MOVEFLAG_ASCENDING | 0x00400000 | Moving upward (flying) |
| MOVEFLAG_CAN_FLY | 0x00800000 | Can fly |
| MOVEFLAG_FLYING | 0x01000000 | Currently flying |
| MOVEFLAG_ONTRANSPORT | 0x02000000 | On a transport (ship/zeppelin) |
| MOVEFLAG_SPLINE_ELEVATION | 0x04000000 | Spline elevation data present |
| MOVEFLAG_SPLINE_ENABLED | 0x08000000 | Server-controlled spline movement |
| MOVEFLAG_WATERWALKING | 0x10000000 | Walking on water |
| MOVEFLAG_SAFE_FALL | 0x20000000 | Slow fall active |
| MOVEFLAG_HOVER | 0x40000000 | Hovering |

### Conditional Fields

Several flags control which optional fields appear in the packet:

| Flag | Adds Fields |
|------|-------------|
| MOVEFLAG_ONTRANSPORT | Transport GUID + local position (4 floats) |
| MOVEFLAG_SWIMMING | Swim pitch (1 float) |
| MOVEFLAG_FALLING / JUMPING | Jump info (4 floats) |
| MOVEFLAG_SPLINE_ELEVATION | Spline elevation (1 float) |

**Note:** `MOVEFLAG_FALLING` (0x2000) is labeled `MOVEFLAG_JUMPING` in some sources.
The client sets this flag during the jump arc. The same flag gates the jump data fields.

## MovementInfo Packet Format

### Client → Server (Read order)

```
uint32  moveFlags;           // Movement flag bitmask
uint32  clientTime;          // Client timestamp (GetTickCount)
float   posX;                // World X position
float   posY;                // World Y position
float   posZ;                // World Z position
float   orientation;         // Facing angle (radians, 0-2π)

// IF moveFlags & ONTRANSPORT (0x02000000):
    ObjectGuid transportGuid;  // Packed GUID of the transport
    float   t_posX;            // Local X offset on transport
    float   t_posY;            // Local Y offset on transport
    float   t_posZ;            // Local Z offset on transport
    float   t_orientation;     // Local orientation on transport

// IF moveFlags & SWIMMING (0x00200000):
    float   swimPitch;         // Camera pitch while swimming (-π/2 to π/2)

uint32  fallTime;              // Milliseconds since fall started (0 if not falling)

// IF moveFlags & JUMPING (0x00002000):
    float   jumpZSpeed;        // Vertical velocity at jump start
    float   jumpCosAngle;      // cos(jump direction angle)
    float   jumpSinAngle;      // sin(jump direction angle)
    float   jumpXYSpeed;       // Horizontal speed at jump start

// IF moveFlags & SPLINE_ELEVATION (0x04000000):
    float   splineElevation;   // Spline elevation value
```

### Server → Client (Write order)

Same structure but with **server time** instead of client time:

```
uint32  moveFlags;
uint32  serverTime;          // Server timestamp (WorldTimer::getMSTime)
float   posX, posY, posZ, orientation;
// ... same conditional fields as above ...
```

### Server → Other Players (Broadcast)

When the server relays a movement packet to other players:

```
PackedGUID moverGuid;        // GUID of the moving entity
[MovementInfo]               // Using Write() format (server time)
```

## Movement Opcodes

### Basic Movement Start/Stop

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_START_FORWARD | 0xB5 | W key pressed |
| MSG_MOVE_START_BACKWARD | 0xB6 | S key pressed |
| MSG_MOVE_STOP | 0xB7 | Movement key released |
| MSG_MOVE_START_STRAFE_LEFT | 0xB8 | Q key pressed |
| MSG_MOVE_START_STRAFE_RIGHT | 0xB9 | E key pressed |
| MSG_MOVE_STOP_STRAFE | 0xBA | Strafe key released |
| MSG_MOVE_START_TURN_LEFT | 0xBC | A key pressed |
| MSG_MOVE_START_TURN_RIGHT | 0xBD | D key pressed |
| MSG_MOVE_STOP_TURN | 0xBE | Turn key released |

### Jump / Fall

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_JUMP | 0xBB | Space pressed (sets JUMPING flag) |
| MSG_MOVE_FALL_LAND | 0xC9 | Landed after falling (clears JUMPING) |

### Swimming

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_START_SWIM | 0xCA | Entered water (sets SWIMMING flag) |
| MSG_MOVE_STOP_SWIM | 0xCB | Left water (clears SWIMMING flag) |
| MSG_MOVE_START_PITCH_UP | 0xBF | Swimming upward |
| MSG_MOVE_START_PITCH_DOWN | 0xC0 | Swimming downward |
| MSG_MOVE_STOP_PITCH | 0xC1 | Stopped pitch change |

### Run/Walk Toggle

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_SET_RUN_MODE | 0xC2 | Switch to running |
| MSG_MOVE_SET_WALK_MODE | 0xC3 | Switch to walking (sets WALK_MODE flag) |

### Facing / Pitch

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_SET_FACING | 0xDA | Orientation changed |
| MSG_MOVE_SET_PITCH | 0xDB | Pitch changed (swimming) |

### Heartbeat

| Opcode | Hex | Trigger |
|--------|-----|---------|
| MSG_MOVE_HEARTBEAT | 0xEE | Periodic position sync (every ~500ms while moving) |

### Teleport

| Opcode | Hex | Direction |
|--------|-----|-----------|
| MSG_MOVE_TELEPORT | 0xC5 | S→C: Server teleports player |
| MSG_MOVE_TELEPORT_ACK | 0xC7 | C→S: Client acknowledges teleport |
| MSG_MOVE_WORLDPORT_ACK | 0xDC | C→S: Client loaded new map |

## Server-Forced Speed Changes

The server can override client speeds. These use a request/ACK pattern:

### Flow
1. Server sends `SMSG_FORCE_*_SPEED_CHANGE` with new speed
2. Client must respond with `CMSG_FORCE_*_SPEED_CHANGE_ACK` echoing the change
3. Client applies the speed change

| Speed Type | Force Opcode (S→C) | ACK Opcode (C→S) |
|------------|---------------------|-------------------|
| Run | SMSG_FORCE_RUN_SPEED_CHANGE (0xE2) | CMSG_FORCE_RUN_SPEED_CHANGE_ACK (0xE3) |
| Run Back | SMSG_FORCE_RUN_BACK_SPEED_CHANGE (0xE4) | CMSG_FORCE_RUN_BACK_SPEED_CHANGE_ACK (0xE5) |
| Swim | SMSG_FORCE_SWIM_SPEED_CHANGE (0xE6) | CMSG_FORCE_SWIM_SPEED_CHANGE_ACK (0xE7) |
| Walk | SMSG_FORCE_WALK_SPEED_CHANGE (0x2DA) | CMSG_FORCE_WALK_SPEED_CHANGE_ACK (0x2DB) |
| Swim Back | SMSG_FORCE_SWIM_BACK_SPEED_CHANGE (0x2DC) | CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK (0x2DD) |
| Turn Rate | SMSG_FORCE_TURN_RATE_CHANGE (0x2DE) | CMSG_FORCE_TURN_RATE_CHANGE_ACK (0x2DF) |

## Root / Unroot

| Opcode | Direction | Description |
|--------|-----------|-------------|
| SMSG_FORCE_MOVE_ROOT (0xE8) | S→C | Server roots the player |
| CMSG_FORCE_MOVE_ROOT_ACK (0xE9) | C→S | Client acknowledges root |
| SMSG_FORCE_MOVE_UNROOT (0xEA) | S→C | Server unroots the player |
| CMSG_FORCE_MOVE_UNROOT_ACK (0xEB) | C→S | Client acknowledges unroot |

## Knockback

```
MSG_MOVE_KNOCK_BACK (0xF1):
  S→C:
    PackedGUID  targetGuid;
    uint32      sequenceId;
    float       vcos;            // cos(knockback direction)
    float       vsin;            // sin(knockback direction)
    float       horizontalSpeed; // XY speed
    float       verticalSpeed;   // Z speed (negative = up)
  C→S:
    [MovementInfo]               // Client's new state after knockback
```

## Monster Movement (SMSG_MONSTER_MOVE)

Server-authoritative movement for NPCs. Uses spline-based paths.

```
SMSG_MONSTER_MOVE (0xDD):
    PackedGUID  moverGuid;
    float       posX, posY, posZ;    // Starting position
    uint32      serverTime;          // Spline start time
    uint8       moveType;            // 0=Normal, 1=Stop, 2=FacingSpot, 3=FacingTarget, 4=FacingAngle
    // IF moveType == FacingSpot:
        float   targetX, targetY, targetZ;
    // IF moveType == FacingTarget:
        uint64  targetGuid;
    // IF moveType == FacingAngle:
        float   angle;
    // IF moveType == Normal:
        uint32  splineFlags;         // SplineFlags bitmask
        uint32  duration;            // Total movement time (ms)
        uint32  pointCount;          // Number of spline points
        [float x, y, z] * pointCount;  // Waypoints
```

## Default Speeds

| Speed Type | Default Value | Unit |
|------------|---------------|------|
| Walk | 2.500 | yards/sec |
| Run | 7.000 | yards/sec |
| Run Back | 4.500 | yards/sec |
| Swim | 4.722 | yards/sec |
| Swim Back | 2.500 | yards/sec |
| Turn Rate | π (3.14159) | radians/sec |

## Jump Physics

When `MOVEFLAG_JUMPING` (0x2000) is set:

- `jumpZSpeed`: Initial vertical velocity (negative = upward, typically ≈ -7.96)
- `jumpSinAngle` / `jumpCosAngle`: Direction of horizontal movement during jump
- `jumpXYSpeed`: Horizontal speed during jump (locked at jump start)
- `fallTime`: Milliseconds since the jump/fall began

Gravity constant: **19.2911 yards/sec²** (applied to vertical velocity over fallTime)

## Validation

The server validates incoming movement packets:

1. **Position bounds**: World coordinates must be within valid map range
2. **Transport bounds**: Local transport offsets must be within ±250 (X/Y) and ±100 (Z)
3. **Speed check**: Distance traveled between heartbeats compared to allowed speed
4. **Flag consistency**: Certain flag combinations are invalid
5. **A % N != 0**: For anti-cheat, movement anomalies are flagged

## Client Memory Layout

See `MEMORY.md` for CMovementInfo offsets in the 1.12.1 client binary:

```
CMovementInfo base  = playerObject + 0x9A8
Position X          = base + 0x10  (0x9B8)
Position Y          = base + 0x14  (0x9BC)
Position Z          = base + 0x18  (0x9C0)
Facing              = base + 0x1C  (0x9C4)
SwimPitch           = base + 0x20  (0x9C8)
MoveFlags           = base + 0x40  (0x9E8)
FallStartTime       = base + 0x78  (0xA20)
FallStartHeight     = base + 0x80  (0xA28)
CurrentSpeed        = base + 0x84  (0xA2C)
WalkSpeed           = base + 0x88  (0xA30)
RunSpeed            = base + 0x8C  (0xA34)
RunBackSpeed        = base + 0x90  (0xA38)
SwimSpeed           = base + 0x94  (0xA3C)
SwimBackSpeed       = base + 0x98  (0xA40)
TurnRate            = base + 0x9C  (0xA44)
```
