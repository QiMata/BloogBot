# Physics Engine Development - Movement Recording System

## Overview

This document describes how to record player movement data from World of Warcraft 1.12.1 and use it to develop/test a physics engine that replicates vanilla WoW's character controller.

## Recording Movement Data

### Prerequisites
1. Bot must be injected and player must be in-world
2. Use a shaman or any character with access to teleportation/movement abilities for testing

### Commands
Say these in game chat (SAY channel):
- **`record start`** - Begin recording at 50ms intervals (20 FPS)
- **`record stop`** - Stop recording and save to files

### Output Location
Recordings are saved to:
```
%USERPROFILE%\Documents\BloogBot\MovementRecordings\
```

Two formats are generated:
- **JSON** - Human-readable, good for inspection and debugging
- **BIN** - Protobuf binary, compact format for physics engine consumption

## Movement Data Structure

Each frame captured includes:

| Field | Type | Description |
|-------|------|-------------|
| `frameTimestamp` | uint64 | Milliseconds since recording started |
| `position` | {x, y, z} | World coordinates (yards) |
| `facing` | float | Orientation in radians (0 = north, π/2 = west) |
| `movementFlags` | uint32 | Bitfield of movement states |
| `movementFlagsHex` | string | Hex representation of movement flags |
| `fallTime` | uint32 | Milliseconds since fall started (0 if not falling) |
| `currentSpeed` | float | Active movement speed (yards/sec) |
| `walkSpeed` | float | Walk speed (yards/sec) |
| `runSpeed` | float | Forward run speed (yards/sec) |
| `runBackSpeed` | float | Backward run speed (yards/sec) |
| `swimSpeed` | float | Forward swim speed (yards/sec) |
| `swimBackSpeed` | float | Backward swim speed (yards/sec) |
| `turnRate` | float | Turn rate (radians/sec) |
| `transportGuid` | uint64 | GUID of transport entity (0 if not on transport) |

### Per-unit data (nearby units in frame)

| Field | Type | Description |
|-------|------|-------------|
| `guid` | uint64 | Unit GUID |
| `position` | {x, y, z} | World position |
| `facing` | float | Orientation |
| `hasMoveSpline` | bool | Whether unit has an active MoveSpline |
| `splineFlags` | uint32 | Spline flags (if hasMoveSpline) |
| `splineTimePassed` | int32 | Ms elapsed on spline |
| `splineDuration` | uint32 | Total spline duration ms |
| `splineNodeCount` | uint32 | Number of spline nodes |

**Spline data note:** Only recordings made after the spline JSON fix (2026-02-08 12:28+) contain valid player spline data. Currently only `Dralrahgra_Durotar_2026-02-08_12-28-15.json` has this data.

## Movement Flags (1.12.1)

```csharp
[Flags]
enum MovementFlags : uint
{
    None            = 0x00000000,
    Forward         = 0x00000001,
    Backward        = 0x00000002,
    StrafeLeft      = 0x00000004,
    StrafeRight     = 0x00000008,
    TurnLeft        = 0x00000010,
    TurnRight       = 0x00000020,
    PitchUp         = 0x00000040,
    PitchDown       = 0x00000080,
    WalkMode        = 0x00000100,
    OnTransport     = 0x00000200,
    Levitating      = 0x00000400,
    Root            = 0x00000800,
    Falling         = 0x00002000,  // Jump/fall state
    FallingFar      = 0x00004000,
    Swimming        = 0x00200000,
    SplineEnabled   = 0x00400000,
    CanFly          = 0x00800000,
    Flying          = 0x01000000,
    WaterWalking    = 0x10000000,
    SafeFall        = 0x20000000,
    Hover           = 0x40000000,
}
```

## Physics Constants (Universal for all WoW versions)

These are hardcoded in the WoW client and apply to all versions:

| Constant | Value | Unit | Notes |
|----------|-------|------|-------|
| **Gravity** | `19.2911` | yards/sec² | Downward acceleration |
| **Terminal Velocity** | `60.148` | yards/sec | Maximum fall speed |
| **Jump Velocity** | `-7.9555` | yards/sec | Initial upward velocity (negative = up) |
| **Mountain Climb Angle** | `0.6427` | radians | ~36.8° - max walkable slope |

## Default Speeds

| Move Type | Speed (yards/sec) |
|-----------|-------------------|
| Walk | 2.5 |
| Run | 7.0 |
| Run Backward | 4.5 |
| Swim | 4.722222 |
| Swim Backward | 2.5 |
| Turn Rate | 3.141594 (π) |

Speed modifiers stack multiplicatively:
- 100% run speed = 7.0 yards/sec
- 60% mount bonus = 7.0 × 1.6 = 11.2 yards/sec
- Sprint (+50%) = 7.0 × 1.5 = 10.5 yards/sec

## Example JSON Recording

```json
{
  "characterName": "Dralrahgra",
  "mapId": 1,
  "zoneName": "Durotar",
  "startTimestampUtc": 1738798500000,
  "frameIntervalMs": 50,
  "description": "Jump off cliff test",
  "frameCount": 120,
  "durationMs": 6000,
  "frames": [
    {
      "frameTimestamp": 0,
      "movementFlags": 1,
      "movementFlagsHex": "0x00000001",
      "position": { "x": -616.5, "y": -4251.2, "z": 38.5 },
      "facing": 2.356,
      "fallTime": 0,
      "walkSpeed": 2.5,
      "runSpeed": 7.0,
      "runBackSpeed": 4.5,
      "swimSpeed": 4.722222,
      "swimBackSpeed": 2.5,
      "turnRate": 3.141594
    },
    {
      "frameTimestamp": 50,
      "movementFlags": 8193,
      "movementFlagsHex": "0x00002001",
      "position": { "x": -616.3, "y": -4251.0, "z": 38.2 },
      "facing": 2.356,
      "fallTime": 45,
      "walkSpeed": 2.5,
      "runSpeed": 7.0,
      ...
    }
  ]
}
```

## Physics Engine Requirements

Your physics engine should replicate:

### 1. Ground Movement
- Forward/backward movement at appropriate speeds
- Strafing (same speed as forward)
- Walk mode toggle (reduces speed to 2.5)
- Slope handling (climb angle limit)
- Collision detection

### 2. Jump Physics
- Initial vertical velocity: -7.9555 yards/sec (upward)
- Gravity: 19.2911 yards/sec² (downward)
- Horizontal velocity maintained during jump
- Air control (limited strafing while airborne)

### 3. Fall Physics
- Same gravity as jump
- Terminal velocity: 60.148 yards/sec
- Fall damage calculation based on fall time/distance

### 4. Swimming
- Water detection
- Swim speeds (different from ground)
- Vertical swim movement (pitch up/down)

## Test Recording Suggestions

1. **Basic movement test**: Walk forward, stop, strafe left, strafe right
2. **Jump test**: Jump in place, jump while running forward
3. **Fall test**: Fall from various heights (teleport to high ground)
4. **Slope test**: Run up/down hills, find slope limits
5. **Swimming test**: Swim in water, dive, surface

## Loading Binary Recordings

```csharp
using Game;
using Google.Protobuf;

// Load recording
using var input = File.OpenRead("recording.bin");
var recording = MovementRecording.Parser.ParseFrom(input);

Console.WriteLine($"Character: {recording.CharacterName}");
Console.WriteLine($"Frames: {recording.Frames.Count}");

foreach (var frame in recording.Frames)
{
    Console.WriteLine($"T={frame.FrameTimestamp}ms: ({frame.Position.X}, {frame.Position.Y}, {frame.Position.Z}) flags=0x{frame.MovementFlags:X8}");
}
```

## Protobuf Schema

The movement data is defined in `Exports/BotCommLayer/Models/ProtoDef/game.proto`:

```protobuf
message MovementData {
    uint32 movementFlags = 1;
    uint32 fallTime = 2;
    float jumpVerticalSpeed = 3;
    float jumpSinAngle = 4;
    float jumpCosAngle = 5;
    float jumpHorizontalSpeed = 6;
    float swimPitch = 7;
    float walkSpeed = 8;
    float runSpeed = 9;
    float runBackSpeed = 10;
    float swimSpeed = 11;
    float swimBackSpeed = 12;
    float turnRate = 13;
    Position position = 14;
    float facing = 15;
    uint64 frameTimestamp = 16;
}

message MovementRecording {
    string characterName = 1;
    uint32 mapId = 2;
    string zoneName = 3;
    uint64 startTimestampUtc = 4;
    uint32 frameIntervalMs = 5;
    repeated MovementData frames = 6;
    string description = 7;
}
```

## Memory Offsets Reference

For direct memory reading (advanced). All offsets from player object base pointer (CMovementInfo base = +0x9A8):

### Position & Orientation
| Offset | Type | Field | CMovementInfo Offset |
|--------|------|-------|---------------------|
| +0x9B8 | float | Position X | base + 0x10 |
| +0x9BC | float | Position Y | base + 0x14 |
| +0x9C0 | float | Position Z | base + 0x18 |
| +0x9C4 | float | Facing | base + 0x1C |
| +0x9C8 | float | SwimPitch | base + 0x20 |

### Movement State
| Offset | Type | Field | CMovementInfo Offset |
|--------|------|-------|---------------------|
| +0x9E8 | uint32 | MovementFlags | base + 0x40 |

### Transport Data (confirmed via zeppelin recording)
| Offset | Type | Field | CMovementInfo Offset | Notes |
|--------|------|-------|---------------------|-------|
| +0x9E0 | uint64 | TransportGUID | base + 0x38 | Reads MoTransport GUID when on zeppelin |

When TransportGUID != 0, the Position fields (+0x9B8-0x9C0) auto-switch to transport-local coordinates. MOVEFLAG_ONTRANSPORT (0x200) is **never set** in vanilla 1.12.1.

### Jump & Fall
| Offset | Type | Field | CMovementInfo Offset |
|--------|------|-------|---------------------|
| +0xA20 | uint32 | FallStartTime | base + 0x78 |
| +0xA28 | float | FallStartHeight | base + 0x80 (always 0 in recordings) |

### Speeds
| Offset | Type | Field | Default | CMovementInfo Offset |
|--------|------|-------|---------|---------------------|
| +0xA2C | float | CurrentSpeed | varies | base + 0x84 |
| +0xA30 | float | WalkSpeed | 2.5 | base + 0x88 |
| +0xA34 | float | RunSpeed | 7.0 | base + 0x8C |
| +0xA38 | float | RunBackSpeed | 4.5 | base + 0x90 |
| +0xA3C | float | SwimSpeed | 4.722 | base + 0x94 |
| +0xA40 | float | SwimBackSpeed | 2.5 | base + 0x98 |
| +0xA44 | float | TurnRate | 3.14159 | base + 0x9C |

### MoveSpline (confirmed via flight path + charge recordings)
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xA4C | ptr | MoveSplinePtr | NULL when standing, valid heap pointer during flight paths / warrior Charge |

MoveSpline client struct at pointer:
- +0x00: nodeCount (uint32)
- +0x18: splineFlags (uint32, 0x300 = CATMULLROM|FLYING)
- +0x20: time_passed (int32, ms elapsed)
- +0x24: duration (uint32, total ms)
- +0x3C: pointsData (ptr to Vector3[] of spline nodes)
