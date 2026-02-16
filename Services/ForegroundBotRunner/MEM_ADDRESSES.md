# WoW 1.12.1.5875 — Character Controller & Movement Physics Offsets

Reference for replicating the vanilla WoW movement engine frame-by-frame in an external character controller (e.g. PhysX).

All offsets are for build **5875** (1.12.1). No ASLR — addresses are static. Base address: `0x400000`.

---

## 1. Engine Physics Constants (Static Globals)

These are hardcoded float/double values in the `.rdata` section. The **values** are consistent across all WoW versions (verified 1.12 through 3.3.5); only the **addresses** shift per build. For 1.12.1 you'll need to scan for the known values with CE — addresses below are confirmed for 2.4.3 as reference, with the universal constant values that apply to **all** versions including 1.12.1:

| Constant | Type | Value | Notes |
|---|---|---|---|
| **Gravity** | double | `19.2911033630371` | yards/sec² — downward acceleration |
| **Terminal Fall Velocity** | float | `60.1480026245117` | yards/sec — max falling speed |
| **Jump Velocity** | float | `-7.95554733276367` | yards/sec — initial upward velocity (negative = up in Z) |
| **Mountain Climb Angle** | float | `0.642700016498566` | radians (~36.8°) — max walkable slope angle |
| **Game Speed (tick)** | double | `0.00100000004749745` | seconds per tick (1ms base) |
| **HeartbeatInterval** | uint32 | at `0x00615BA7` | 1.12.1 confirmed — movement packet send interval |

### How to Find These in 1.12.1

Scan for the **double** value `19.2911` in the `.rdata` section — this is your gravity address.  
Scan for **float** `60.148` — terminal fall velocity.  
Scan for **float** `-7.9555` — jump velocity.  
Scan for **float** `0.6427` — max slope angle.  
These are read-only constants embedded in the executable, not computed at runtime.

---

## 2. Player Object — Movement Data (Offsets from PlayerBase)

PlayerBase is obtained via the Object Manager. These offsets are from the **base address of the local player CGObject_C**.

### 2.1 Position & Orientation

| Offset | Type | Field | Notes |
|---|---|---|---|
| `+0x9B8` | float | **PositionX** | World X coordinate (CMovementInfo base + 0x10) |
| `+0x9BC` | float | **PositionY** | World Y coordinate (base + 0x14) |
| `+0x9C0` | float | **PositionZ** | World Z coordinate (base + 0x18, up axis) |
| `+0x9C4` | float | **Facing** | Orientation in radians (base + 0x1C) |
| `+0x9C8` | float | **SwimPitch** | Camera pitch while swimming (base + 0x20) |

Also accessible via static pointers:
- `0x00C7B548` → Player X (float)
- `0x00C7B544` → Player Y (float)
- `0x00C7B54C` → Player Z (float)

### 2.2 Movement Flags

| Offset | Type | Field | Notes |
|---|---|---|---|
| `+0x9E8` | uint32 | **MovementFlags** | Primary movement state bitfield (Warden-scanned) |
| `+0x9EF` | uint32 | **MovementState** | Alternative movement state (4 bytes, hex) |
| `+0x9F3` | byte | **MovementType** | Movement modifier type |

#### MovementFlags Enum (1.12.1 vanilla):

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
    WalkMode        = 0x00000100,  // toggled with NUM PAD /
    OnTransport     = 0x00000200,
    Levitating      = 0x00000400,
    Root            = 0x00000800,
    Falling         = 0x00002000,
    FallingFar      = 0x00004000,
    Swimming        = 0x00200000,
    SplineEnabled   = 0x00400000,
    CanFly          = 0x00800000,
    Flying          = 0x01000000,
    OnTransport2    = 0x02000000,
    SplineElevation = 0x04000000,
    RootAlt         = 0x08000000,
    WaterWalking    = 0x10000000,
    SafeFall        = 0x20000000,
    Hover           = 0x40000000,
}
```

#### MovementType Values (+0x9F3):

| Value | Effect |
|---|---|
| `16` (0x10) | Whisp / Water Walk |
| `34` (0x22) | Slow Fall |
| `80` (0x50) | Levitation |
| `144` (0x90) | Dead / Water Walk |

### 2.3 Speed Values

| Offset | Type | Field | Default Value | Notes |
|---|---|---|---|---|
| `+0xA2C` | float | **CurrentSpeed** | varies | Active movement speed (Warden-scanned) |
| `+0xA30` | float | **WalkSpeed** | `2.5` | Walk speed (yards/sec) |
| `+0xA34` | float | **RunSpeed** | `7.0` | Forward run speed (yards/sec) — Warden-scanned |
| `+0xA38` | float | **RunBackSpeed** | `4.5` | Backward run speed |
| `+0xA3C` | float | **SwimSpeed** | `4.722222` | Forward swim speed |
| `+0xA40` | float | **SwimBackSpeed** | `2.5` | Backward swim speed |
| `+0xA44` | float | **TurnRate** | `3.141594` | Turn speed (radians/sec, ~π) |

> **Verified 2026-02-06:** Speed order confirmed by recording a stationary character and matching default values from cMaNGOS `baseMoveSpeed[]` array. CMovementInfo base = `0x9A8`, speeds start at base + `0x88`.

#### Base Move Speeds (from cMaNGOS `Unit.cpp`):

```cpp
float baseMoveSpeed[MAX_MOVE_TYPE] = {
    2.5f,       // MOVE_WALK
    7.0f,       // MOVE_RUN
    4.5f,       // MOVE_RUN_BACK
    4.722222f,  // MOVE_SWIM
    2.5f,       // MOVE_SWIM_BACK
    3.141594f,  // MOVE_TURN_RATE
};
```

Speed is stored as `baseMoveSpeed * speedRate`, where speedRate is the multiplier from buffs/mounts.  
100% run speed = 7.0 yards/sec. A 60% mount = 7.0 × 1.6 = 11.2 yards/sec.

### 2.4 Jump & Fall Data

| Offset | Type | Field | Notes |
|---|---|---|---|
| `+0xA14` | float | **JumpSinAngle** | Sin of jump launch angle (base + 0x6C) |
| `+0xA18` | float | **JumpCosAngle** | Cos of jump launch angle (base + 0x70) |
| `+0xA20` | uint32 | **FallStartTime** | Tick count when fall began (Warden-scanned as "Move state") |
| `+0xA28` | float | **FallStartHeight** | Z coordinate when fall/jump initiated (base + 0x80) |
| `+0xA50` | float | **JumpVelocity** | Current jump vertical velocity (base + 0xA8) |

Static pointer for falling speed:
- `0x0087D894` → Falling Speed (float) — current vertical velocity during fall

### 2.4.1 Transport Data

| Offset | Type | Field | Notes |
|---|---|---|---|
| `+0x9D0` | float | **TransportOffsetX** | Local X on transport (base + 0x28) |
| `+0x9D4` | float | **TransportOffsetY** | Local Y on transport (base + 0x2C) |
| `+0x9D8` | float | **TransportOffsetZ** | Local Z on transport (base + 0x30) |
| `+0x9DC` | float | **TransportOrientation** | Local facing on transport (base + 0x34) |
| `+0x9E0` | uint64 | **TransportGUID** | GUID of transport entity (base + 0x38) |

> **Note:** Transport offset values need empirical verification. TransportGUID at `0x9E0` (8 bytes before MoveFlags) is high confidence. Transport offsets in the unknown1 region are best-guess from CMovementInfo struct layout. Record on a zeppelin/boat with `MOVEFLAG_ONTRANSPORT (0x200)` set to validate.

### 2.5 Unit Dimensions

| Offset | Type | Field | Notes |
|---|---|---|---|
| `+0xA5C` | float | **UnitHeight** | Model height (used for LOS raycasts from "eye" position) |
| `+0xA60` | float | **CollisionHeight** | Collision box Z extent |

---

## 3. CMovementInfo Struct Layout

This is the internal movement data structure passed in movement packets and stored per-unit. When you see a movement packet (MSG_MOVE_*), this is the payload structure:

```cpp
// CMovementInfo base in player object = 0x9A8 (Offsets.Player.MovementStruct)
// Verified: base + 0x40 = 0x9E8 (MoveFlags) ✓
// Verified: base + 0x88 = 0xA30 (WalkSpeed = 2.5) ✓
struct CMovementInfo  // Size: ~0x00AC
{
    char        unknown0[16];       // 0x0000
    CVec3       Position;           // 0x0010  (X, Y, Z as floats)  → reads at 0x9B8
    float       Facing;             // 0x001C  orientation/heading   → reads at 0x9C4
    float       SwimPitch;          // 0x0020  camera pitch (swimming/flying) → reads at 0x9C8
    char        unknown1a[4];       // 0x0024  (padding or transport-related)
    CVec3       TransportOffset;    // 0x0028  local X,Y,Z on transport
    float       TransportFacing;    // 0x0034  local facing on transport
    uint64      TransportGUID;      // 0x0038  GUID of transport (boat/zeppelin)
    uint32      MoveFlags;          // 0x0040  MovementFlags bitfield
    uint32      MoveFlags2;         // 0x0044  extra flags (1 byte used)
    char        unknown2[20];       // 0x0048
    uint32      TimeMoved;          // 0x005C  client timestamp
    char        unknown3[12];       // 0x0060
    float       SinAngle;           // 0x006C  sin of jump angle (for air movement)
    float       CosAngle;           // 0x0070  cos of jump angle
    char        unknown4[4];        // 0x0074
    uint32      FallTime;           // 0x0078  milliseconds since fall started  (reads at 0xA20)
    float       FallStartHeight;    // 0x0080  Z at start of fall               (reads at 0xA28)
    char        unknown5[4];        // 0x0084  (4 bytes, NOT 8 - verified by speed alignment)
    float       WalkSpeed;          // 0x0088                                   (reads at 0xA30)
    float       RunSpeed;           // 0x008C                                   (reads at 0xA34)
    float       RunBackSpeed;       // 0x0090                                   (reads at 0xA38)
    float       SwimSpeed;          // 0x0094                                   (reads at 0xA3C)
    float       SwimBackSpeed;      // 0x0098                                   (reads at 0xA40)
    float       TurnSpeed;          // 0x009C                                   (reads at 0xA44)
    float       FlySpeed;           // 0x00A0  (unused in vanilla)
    float       FlyBackSpeed;       // 0x00A4  (unused in vanilla)
    float       JumpVelocity;       // 0x00A8  current jump velocity            (reads at 0xA50)
};
```

> **Corrected 2026-02-06:** Speed order verified from live recordings. unknown5 is 4 bytes (not 8) based on WalkSpeed reading at `base + 0x88 = 0xA30` with expected value 2.5. Transport offset fields in unknown1 region are estimated from CMovementInfo struct analysis and need empirical verification.

---

## 4. Movement Functions (Code Addresses)

### 4.1 Core Movement Loop

| Address | Function | Notes |
|---|---|---|
| `0x00615CF5` | **CMovement::UpdatePlayerMovement** | Main per-frame movement update — **the function you want to study** |
| `0x00616620` | **CMovement__MoveUnit** | Applies computed movement to unit |
| `0x00616749` | **CMovement::ExecuteMovement** | Sends MSG_MOVE_HEARTBEAT packet |
| `0x007C4955` | **CMovementData::GetPosition** | Returns current position (Warden-scanned) |
| `0x007C4D41` | **CMovementShared::GetBaseSpeed** | Returns base speed for move type (Warden-scanned, 10 bytes) |
| `0x007C625E` | **CMovement::PlayerJump** | Jump initiation logic (2 bytes, Warden-scanned) |
| `0x007C625F` | (within PlayerJump) | Anti-jump / anti-knockback check |
| `0x007C6E88` | **FixSwimming** | Swimming state transition |
| `0x005FE54F` | **CGUnit_C::UpdateBaseAnimation** | References GetBaseSpeed — speed hack detection point |

### 4.2 Terrain & Collision

| Address | Function | Signature | Notes |
|---|---|---|---|
| `0x0069BFF0` | **CMap::VectorIntersect** | `int(CVec3* start, CVec3* end, CVec3* hitPt, float* dist, uint32 flags)` | Traceline / raycast for LOS and ground detection |
| `0x006AA160` | **World::Intersect** | (alternate params) | Higher-level intersection test |
| `0x007E57E0` | **CGWorldFrame::HitTestPoint** | (screen coords) | Screen-to-world raycast |

### 4.3 Movement Packet Handlers

All movement opcodes route to a single handler:

| Address | Function | Notes |
|---|---|---|
| `0x00603BB0` | **Movement Opcode Handler** | Handles ALL MSG_MOVE_* opcodes |

Opcodes handled:
- MSG_MOVE_START_FORWARD
- MSG_MOVE_START_BACKWARD
- MSG_MOVE_STOP
- MSG_MOVE_START_STRAFE_LEFT / RIGHT
- MSG_MOVE_STOP_STRAFE
- MSG_MOVE_JUMP
- MSG_MOVE_START_TURN_LEFT / RIGHT
- MSG_MOVE_STOP_TURN
- MSG_MOVE_SET_RUN_MODE / WALK_MODE
- MSG_MOVE_SET_FACING
- MSG_MOVE_SET_PITCH
- MSG_MOVE_FALL_LAND
- MSG_MOVE_START_SWIM / STOP_SWIM
- MSG_MOVE_HEARTBEAT

### 4.4 Click-to-Move

| Address | Function | Notes |
|---|---|---|
| `0x00611130` | **CGPlayer_C::ClickToMove** | `void(playerBase, action, guidPtr, posPtr, precision)` |

---

## 5. Movement Physics Model — How to Replicate

### 5.1 Frame Update Loop (Pseudocode)

The client runs `CMovement::UpdatePlayerMovement` every frame. Here's the reconstructed logic:

```
deltaTime = currentTime - lastFrameTime  // in seconds

// 1. READ INPUTS → MovementFlags
// 2. COMPUTE DESIRED VELOCITY
if (onGround):
    // Horizontal movement
    speed = GetActiveSpeed()  // run, walk, or swim depending on state
    
    if (MovementFlags & Forward):
        velocity.x = cos(facing) * speed
        velocity.y = sin(facing) * speed
    if (MovementFlags & Backward):
        velocity.x = -cos(facing) * backSpeed
        velocity.y = -sin(facing) * backSpeed
    if (MovementFlags & StrafeLeft):
        velocity.x += cos(facing + π/2) * speed
        velocity.y += sin(facing + π/2) * speed
    if (MovementFlags & StrafeRight):
        velocity.x += cos(facing - π/2) * speed
        velocity.y += sin(facing - π/2) * speed
    
    // Clamp diagonal speed to base speed (no 41% boost)
    if (length(velocity.xy) > speed):
        normalize and scale to speed
    
    // Slope check
    slopeAngle = GetTerrainSlopeAtPosition(position)
    if (slopeAngle > MountainClimbAngle):  // > 0.6427 rad (~36.8°)
        reject movement up the slope
        apply gravity slide down
    
    // Ground snap
    terrainZ = GetTerrainHeight(position.x, position.y)
    velocity.z = (terrainZ - position.z) / deltaTime  // snap to ground

if (jumping):
    // Jump was initiated — apply initial velocity
    velocity.z = JumpVelocity  // -7.9555 (negative = UP in WoW coords)
    // Preserve horizontal momentum from ground speed
    // SinAngle/CosAngle store the jump direction
    fallStartHeight = position.z
    fallStartTime = currentTick

if (falling OR jumping):
    // Gravity integration (simple Euler)
    velocity.z += Gravity * deltaTime  // 19.2911 yards/sec²
    
    // Clamp to terminal velocity
    if (velocity.z > TerminalVelocity):  // 60.148 yards/sec
        velocity.z = TerminalVelocity
    
    // Horizontal movement dampened in air
    // Air control is limited — you keep momentum from jump direction
    // but can still strafe slightly

// 3. INTEGRATE POSITION
newPosition = position + velocity * deltaTime

// 4. COLLISION DETECTION (traceline)
// Cast ray from old position to new position
if (CMap::VectorIntersect(position, newPosition, &hitPoint, &dist, flags)):
    newPosition = hitPoint  // slide along collision surface
    
    // Check if we landed
    if (falling AND hitPoint.z >= terrainHeight):
        // LAND
        fallDistance = fallStartHeight - position.z
        // Compute fall damage (server-side, but client predicts)

// 5. TURNING
if (MovementFlags & TurnLeft):
    facing += TurnRate * deltaTime  // 3.141594 rad/sec
if (MovementFlags & TurnRight):
    facing -= TurnRate * deltaTime

// 6. COMMIT
position = newPosition
```

### 5.2 Jump Arc Physics

WoW uses simple projectile motion with constant gravity:

```
// At jump initiation:
v0 = 7.9555 yards/sec (upward)
g  = 19.2911 yards/sec²

// Height at time t:
z(t) = z0 + v0*t - 0.5*g*t²

// Time to apex:
t_apex = v0 / g = 7.9555 / 19.2911 ≈ 0.4124 seconds

// Max jump height:
h_max = v0² / (2*g) = 63.29 / 38.58 ≈ 1.640 yards ≈ 4.92 feet

// Time to return to ground (from standing jump):
t_total = 2 * t_apex ≈ 0.8248 seconds

// Horizontal distance during jump:
d_horiz = runSpeed * t_total = 7.0 * 0.8248 ≈ 5.774 yards
```

### 5.3 Fall Damage Calculation

Fall damage is computed **server-side** based on fall time reported in MSG_MOVE_FALL_LAND:

```
// Server-side (from cMaNGOS):
// fallDistance = fallStartHeight - currentZ
// Minimum fall distance for damage: ~14 yards (approx)
// At ~78.7 yards fall distance → instant death
// 
// Formula (approximate):
// if (fallDistance > safeFallDistance):
//     damagePercent = (fallDistance - safeFallDistance) / damagePerYard
//     damage = maxHealth * damagePercent
```

### 5.4 Slope / Terrain Traversal

```
MountainClimbAngle = 0.6427 radians ≈ 36.8 degrees

// When character approaches terrain steeper than this:
// - Forward movement UP the slope is rejected
// - Character slides DOWN the slope under gravity
// - Diagonal strafing along the slope IS possible
// - Jump can sometimes clear steep sections (wall-jumping)

// Terrain normal calculation:
// The client reads terrain height from ADT data (MCNK chunks)
// Height is interpolated from 9x9 outer vertices + 8x8 inner vertices
// per terrain chunk using barycentric interpolation on triangles
//
// MCNK header contains:
//   - Base X/Y position
//   - Area flags
//   - Liquid level (water surface Z)
```

### 5.5 Swimming Physics

```
// Swimming is toggled when player Z crosses water surface level
// Water level is stored per-ADT-chunk in MCNK header
// 
// In water:
// - Gravity is NOT applied (or greatly reduced)
// - Speed switches to SwimSpeed (4.722222) / SwimBackSpeed (2.5)
// - PitchUp/PitchDown flags allow vertical swimming
// - MovementFlags & Swimming = 0x00200000
// - Surface swimming: player bobs at water level
// - Underwater: free 3D movement
//
// Fix swimming address: 0x007C6E88
```

---

## 6. Key Values to Read Frame-by-Frame

For your PhysX character controller to match WoW's movement exactly, read these every frame:

### From Player Object (PlayerBase + offset):

| Priority | Offset | What | Why |
|---|---|---|---|
| ★★★ | `+0x9B8/9BC/9C0` | Position X/Y/Z | Current world position (base+0x10) |
| ★★★ | `+0x9C4` | Facing | Current orientation (base+0x1C) |
| ★★★ | `+0x9E8` | MovementFlags | What inputs are active (base+0x40) |
| ★★★ | `+0xA34` | RunSpeed | Current run speed (base+0x8C) |
| ★★ | `+0xA30` | WalkSpeed | Walk speed (base+0x88) |
| ★★ | `+0xA38` | RunBackSpeed | Backward speed (base+0x90) |
| ★★ | `+0xA3C` | SwimSpeed | Swim speed (base+0x94) |
| ★★ | `+0xA44` | TurnRate | Turning speed (base+0x9C) |
| ★★ | `+0xA20` | FallStartTime | When fall began (to compute fall duration) (base+0x78) |
| ★★ | `+0xA28` | FallStartHeight | Z at fall start (base+0x80) |
| ★★ | `+0x9C8` | SwimPitch | Camera pitch while swimming (base+0x20) |
| ★ | `+0x9F3` | MovementType | Levitate/waterwalk/slowfall modifier |
| ★ | `+0xA5C` | UnitHeight | For eye-level raycasts |
| ★ | `+0xA60` | CollisionBoxZ | Collision capsule height |

### From Static Addresses:

| Address | What |
|---|---|
| `0x0087D894` | Current falling speed (float) |
| `0x00C7B548` | Player X (quick access, no pointer chain) |
| `0x00C7B544` | Player Y |
| `0x00C7B54C` | Player Z |

---

## 7. Movement Opcodes (Client → Server)

These are the packets the client sends when movement state changes. Your headless client will need to send these:

| Opcode | Name | When Sent |
|---|---|---|
| `0x00B5` | MSG_MOVE_START_FORWARD | W key pressed |
| `0x00B6` | MSG_MOVE_START_BACKWARD | S key pressed |
| `0x00B7` | MSG_MOVE_STOP | W/S released |
| `0x00B8` | MSG_MOVE_START_STRAFE_LEFT | Q/A strafe pressed |
| `0x00B9` | MSG_MOVE_START_STRAFE_RIGHT | E/D strafe pressed |
| `0x00BA` | MSG_MOVE_STOP_STRAFE | Strafe released |
| `0x00BB` | MSG_MOVE_JUMP | Space pressed |
| `0x00BC` | MSG_MOVE_START_TURN_LEFT | A/Left pressed |
| `0x00BD` | MSG_MOVE_START_TURN_RIGHT | D/Right pressed |
| `0x00BE` | MSG_MOVE_STOP_TURN | Turn released |
| `0x00C7` | MSG_MOVE_SET_FACING | Mouse turn |
| `0x00C9` | MSG_MOVE_FALL_LAND | Landed after fall |
| `0x00CA` | MSG_MOVE_START_SWIM | Entered water |
| `0x00CB` | MSG_MOVE_STOP_SWIM | Left water |
| `0x00DA` | MSG_MOVE_SET_RUN_MODE | Switched to run |
| `0x00DB` | MSG_MOVE_SET_WALK_MODE | Switched to walk |
| `0x00EE` | MSG_MOVE_HEARTBEAT | Periodic position sync |

### Packet Payload:

Every movement packet contains the CMovementInfo struct:
```
[PackedGUID]     // sender GUID (variable length)
[MovementFlags]  // uint32
[MoveFlags2]     // uint8 (vanilla) or uint16 (later)
[Timestamp]      // uint32 — client tick count
[PositionX]      // float
[PositionY]      // float
[PositionZ]      // float
[Orientation]    // float
// Optional fields based on flags:
[TransportGUID + TransportPos]  // if MOVEFLAG_ONTRANSPORT
[Pitch]                          // if MOVEFLAG_SWIMMING
[FallTime]                       // if MOVEFLAG_FALLING
[JumpVelocity + SinAngle + CosAngle + XYSpeed]  // if MOVEFLAG_FALLING
[SplineElevation]                // if MOVEFLAG_SPLINE_ELEVATION
```

---

## 8. Warden Scan Points (Movement-Related)

**DO NOT PATCH** any of these addresses — Warden actively reads them:

| Address | Size | What |
|---|---|---|
| `+0x9E8` (PlayerBase) | scanned | MovementFlags |
| `+0xA20` (PlayerBase) | scanned | FallStartTime / Move state |
| `+0xA2C` (PlayerBase) | scanned | Move Speed |
| `+0xA34` (PlayerBase) | scanned | Forward speed |
| `+0xA60` (PlayerBase) | scanned | Collision box Z |
| `0x00615BA7` | 4 bytes | Movement heartbeat interval |
| `0x00615CF5` | 1 byte | CMovement::UpdatePlayerMovement |
| `0x006163DB` | 2 bytes | Anti-root hack check |
| `0x006163DE` | 10 bytes | Anti-root hack check |
| `0x00616749` | 2 bytes | ExecuteMovement / heartbeat send |
| `0x007C4955` | 1 byte | CMovementData::GetPosition |
| `0x007C4D41` | 10 bytes | CMovementShared::GetBaseSpeed |
| `0x007C625E` | 2 bytes | CMovement::PlayerJump (infinite jump) |
| `0x007C625F` | 1 byte | Anti-jump / anti-knockback |
| `0x007C6206` | 11 bytes | CMovement swim/movement mode |
| `0x007C620D` | 2 bytes | Movement type toggle |
| `0x005FE54F` | 1 byte | UpdateBaseAnimation / speed validation |

---

## 9. Hack Reference Points (Understanding the System)

These are known patch points that reveal HOW the engine works internally. Useful for understanding control flow, **not** for patching:

| Address | Patch | What It Does | What It Tells You |
|---|---|---|---|
| `0x007C63DA` | `8B 4F 78` → `31 C9 90` | No fall damage | Fall damage reads FallTime from PlayerBase+0x78 relative offset |
| `0x007C625E` | 2-byte patch | Infinite jump | Jump check prevents air-jumping — state must be grounded |
| `0x00615CF5` | `F8` → `FE` | Anti-move (freeze) | Movement update flag check |
| `0x006163DB` | `8A 47` → `EB F9` | Anti-root bypass | Root flag checked during movement update |
| `0x006341BC` | `74 25` → `90 90` | Fly hack | Removes ground-check branch |

---

## 10. Terrain Height Queries

The client computes terrain height from ADT map files. For your external engine:

### Using CMap::VectorIntersect (Traceline):
```cpp
// Cast vertical ray downward to find ground height:
CVec3 start = { playerX, playerY, playerZ + 2.0f };  // slightly above
CVec3 end   = { playerX, playerY, playerZ - 500.0f }; // far below
CVec3 hitPoint;
float distance;
uint32 flags = 0x00100171;  // terrain + WMO + M2 collision

int result = CMap_VectorIntersect(&start, &end, &hitPoint, &distance, flags);
if (result) {
    float terrainHeight = hitPoint.z;
}
```

### ADT Terrain Height (Offline Extraction):
```
// Each ADT tile = 533.333 yards × 533.333 yards
// Divided into 16×16 chunks (MCNK)
// Each chunk has:
//   - 9×9 outer height vertices (h9x9)
//   - 8×8 inner height vertices (h8x8)
// Heights are interpolated using barycentric coordinates
// on the triangle mesh formed by these vertices
//
// MCNK header contains:
//   - Base X/Y position
//   - Area flags
//   - Liquid level (water surface Z)
```

---

## 11. Coordinate System

```
WoW uses a LEFT-HANDED coordinate system:
  - X: East/West (East = positive)
  - Y: North/South (North = positive)  
  - Z: Up/Down (Up = positive)

Facing angle:
  - 0     = North (+Y direction)
  - π/2   = West (-X direction)
  - π     = South (-Y direction)
  - 3π/2  = East (+X direction)
  - Increases counter-clockwise when viewed from above

Map coordinates:
  - Origin (0,0) is at the center of the world map
  - Each continent spans roughly ±17066 yards from center
  - 1 yard ≈ 0.9144 meters (for PhysX scaling)
```

---

## 12. Cross-References

- **Gamestate detection**: See `wow_1121_gamestate_offsets.md`
- **Object Manager / Player pointer**: See `wow_1121_gamestate_offsets.md`
- **Client infrastructure (Lua, DirectX, Camera, Warden)**: See `wow_1121_client_infrastructure_offsets.md`
- **cMaNGOS classic source**: `github.com/cmangos/mangos-classic`
- **vMaNGOS source**: `github.com/vmangos/core`

---

## Sources

- OwnedCore 1.12.1 Info Dump Thread (Pages 3, 27, 36, 41)
- OwnedCore CheatEngine Offsets Thread (2.4.x constants, values confirmed universal)
- OwnedCore Movement State thread (CMovementInfo struct)
- OwnedCore Spline Flags thread (MovementFlags enum)
- WowDevs/Fishbot-1.12.1 GitHub (function pointers, hack addresses)
- brian8544/WoWAdminPanel (multi-version trainer, physics constant identification)
- cMaNGOS mangos-classic source (baseMoveSpeed array, movement handler)
- ArcEmu MovementInfo parser (packet structure, flag-conditional fields)
- SolarStrike forums (physics constant values across versions)