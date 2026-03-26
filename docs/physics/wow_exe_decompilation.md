# WoW.exe 1.12.1 Movement Physics — Decompiled Pseudocode

**Binary:** `WoW.exe` build 5875 (PE32, x86, Sep 19 2006)
**Image base:** 0x00400000
**Analysis method:** `objdump -d -M intel` + float constant scanning
**Date:** 2026-03-21

---

## Physics Constant Table (VA 0x0081DA50 — 0x0081DA94)

These are **static read-only** floats in `.rdata`. The client reads them by absolute address.

| VA | Hex (IEEE 754) | Value | Name |
|---|---|---|---|
| `0x0081DA54` | `0x3F3504F3` | `0.70710677` | `SIN_45` — sin(45°) = cos(45°) = 1/√2 |
| `0x0081DA58` | `0x419A542F` | `19.29110527` | **`GRAVITY`** (yards/s²) |
| `0x0081DA5C` | `0xBD54536A` | `-0.05183736` | `-1/GRAVITY` |
| `0x0081DA60` | `0x411A542F` | `9.64555264` | `HALF_GRAVITY` (GRAVITY / 2) |
| `0x0081DA64` | `0x421A542F` | `38.58221054` | `DOUBLE_GRAVITY` (GRAVITY × 2) |
| `0x0081DA68` | `0x3DD4536A` | `0.10367472` | `2/GRAVITY` |
| `0x0081DA6C` | `0x3CD4536A` | `0.02591868` | `0.5/GRAVITY` |
| `0x0081DA70` | `0xB4800000` | `-2.384e-07` | Negative epsilon |
| `0x0081DA74` | `0x3F8BFB16` | `1.09360003` | `STEP_HEIGHT_FACTOR` |
| `0x0081DA78` | `0x7F800000` | `+inf` | Infinity sentinel |
| `0x0081DA7C` | `0x40490FDB` | `3.14159274` | `PI` |
| `0x0081DA80` | `0x41200000` | `10.0` | `FALL_DAMAGE_START_SPEED` |
| `0x0081DA84` | `0x4131C71C` | `11.11111069` | Fall damage scalar (100/9) |
| `0x0081DA88` | `0x40B1C71C` | `5.55555534` | Half fall scalar (50/9) |
| `0x0081DA8C` | `0x41200000` | `10.0` | Duplicate |
| `0x0081DA90` | `0x40A00000` | `5.0` | Fall damage threshold? |

### Dynamically Computed (writable .bss / global vars)

| VA | Value | Computed By | Formula |
|---|---|---|---|
| `0x0087D894` | `60.148003` | `SetTerminalVelocity` (0x7C6160) | `param × 1.0936` |
| `0x0087D898` | `7.0` | `SetTerminalVelocitySwim` (0x7C6180) | `param × 1.0936` |
| `0x00CF5D00` | `6.2832` | init (0x7C5DD0) | `PI × 2` |
| `0x00CF5CD0` | `0.15915` | init (0x7C5E50) | `1.0 / (2×PI)` |
| `0x00CF5D68` | `100.0` | init (0x7C7BD0) | `FALL_DMG_START_SPEED²` |
| `0x00CF5D7C` | `123.457` | init (0x7C7C00) | `(100/9)²` |

### Other Referenced Constants

| VA | Value | Used In |
|---|---|---|
| `0x00801360` | `0.001` | `MS_TO_SEC` — fallTime (int ms) → seconds |
| `0x0080E020` | `0.051837` | `1/GRAVITY` (positive copy) |
| `0x008029D4` | `2.384e-07` | Speed epsilon (effectively zero) |
| `0x007FFA24` | `0.5` | Generic half |
| `0x007FF9D8` | `1.0` | Generic one |
| `0x007FFD74` | `0.0` | Generic zero |

---

## Key Functions

### 1. ApplyGravity (VA 0x007C5D20)

**Signature:** `float __thiscall CMovement::ApplyGravity(float dt)`
**`this`** = `CMovement*` in ECX (thiscall convention)

```
struct CMovement {
    // +0x40: uint32 movementFlags
    // +0x78: int32  fallTime_ms
    // +0x7C: float  fallStartZ
    // +0xA0: float  currentFallSpeed (positive = downward)
};
```

**Pseudocode:**

```cpp
float CMovement::ApplyGravity(float dt)
{
    float terminalVel;
    if (this->movementFlags & MOVEFLAG_SAFE_FALL)   // 0x20000000
        terminalVel = g_terminalVelSwim;             // [0x87D898] = 7.0
    else
        terminalVel = g_terminalVel;                 // [0x87D894] = 60.148

    // Clamp current speed to terminal velocity
    float speed = this->currentFallSpeed;            // [ecx+0xA0]
    if (speed > terminalVel)
        speed = terminalVel;

    // Integrate: newSpeed = speed + GRAVITY * dt
    float newSpeed = speed + GRAVITY * dt;           // fmul [0x81DA58]

    // Clamp to terminal velocity again
    if (newSpeed > terminalVel)
        newSpeed = terminalVel;

    return newSpeed;
}
```

### 2. ComputeFallDisplacement (VA 0x007C5E70)

**Signature:** `float __thiscall CMovement::ComputeFallDisplacement(float dt)`

This is the key function that computes **vertical distance traveled** during a fall frame.
It handles two cases: normal freefall, and hitting terminal velocity mid-frame.

```cpp
float CMovement::ComputeFallDisplacement(float dt)
{
    float terminalVel;
    if (this->movementFlags & MOVEFLAG_SAFE_FALL)
        terminalVel = g_terminalVelSwim;
    else
        terminalVel = g_terminalVel;

    // Clamp current speed to terminal
    float speed = this->currentFallSpeed;
    if (speed > terminalVel)
        speed = terminalVel;

    // Check if we'll hit terminal velocity this frame
    float newSpeed = speed + GRAVITY * dt;
    if (newSpeed <= terminalVel)
    {
        // Case 1: Normal freefall — standard kinematic equation
        // displacement = speed * dt + ½g * dt²
        return speed * dt + HALF_GRAVITY * dt * dt;
    }
    else
    {
        // Case 2: Hit terminal velocity mid-frame
        // Split into accelerating phase + constant phase
        float invGrav = 1.0f / GRAVITY;              // [0x80E020] = 0.051837
        float t_accel = (terminalVel - speed) * invGrav;
        float d_accel = HALF_GRAVITY * t_accel * t_accel;
        float t_const = dt - t_accel;
        float d_const = t_const * terminalVel;
        return d_accel + d_const;
    }
}
```

### 3. GetFallSpeed (VA 0x007C5D70)

Converts integer fallTime (ms) to current fall speed.

```cpp
float CMovement::GetFallSpeed()
{
    float dt = this->fallTime_ms * MS_TO_SEC;        // [ecx+0x78] * 0.001
    return ApplyGravity(dt);                          // calls 0x7C5D20
}
```

### 4. GetVerticalDisplacement (VA 0x007C5F00)

Converts integer fallTime (ms) to total fall displacement.

```cpp
float CMovement::GetVerticalDisplacement(int fallTime_ms)
{
    float dt = fallTime_ms * MS_TO_SEC;
    return ComputeFallDisplacement(dt);               // calls 0x7C5E70
}
```

### 5. GetFallZ (VA 0x007C5F30)

Returns the Z position after falling for `fallTime_ms`.

```cpp
float CMovement::GetFallZ(int fallTime_ms)
{
    float displacement = GetVerticalDisplacement(fallTime_ms);
    return this->positionZ + displacement;            // [ecx+0x18] + displacement
}
```

### 6. ShouldEnterFreefall (VA 0x007C5DE0)

Tests whether a moving character should enter freefall.

```cpp
bool CMovement::ShouldEnterFreefall()
{
    if (!(this->movementFlags & MOVEFLAG_FALLING))    // 0x2000 (ah & 0x20)
        return false;

    // Check if current speed exceeds safe fall threshold
    float speed = this->currentFallSpeed;
    if (speed == 0.0f)
        return false;

    float dt = this->fallTime_ms * MS_TO_SEC;
    float threshold = GetFallSpeedThreshold();         // calls 0x7C5DA0

    return dt > threshold;  // fall speed exceeds deceleration threshold
}
```

### 7. BeginJump (VA 0x007C6230)

Initiates a jump. Sets initial upward velocity.

```cpp
bool CMovement::BeginJump(bool fromWater)
{
    if (fromWater && !(this->movementFlags & 0x40000000))
        return false;

    // Check if on transport or in state that blocks jumping
    auto* transport = this->transport;                 // [ecx+0xA4]
    if (transport != nullptr) {
        if (transport->flags & 0x4)        // transport type check
            return false;
        if (transport->flags & 0x200)      // another transport flag
            return false;
    }

    if (this->movementFlags & 0x203800)    // swimming/flying/levitating
        return false;

    float jumpVel;
    if (this->movementFlags & MOVEFLAG_SWIMMING)    // 0x200000
        jumpVel = -9.096748f;                        // 0xC1118C48
    else
        jumpVel = -7.955547f;                        // 0xC0FE93D8

    StartFall(jumpVel);                              // calls 0x7C61F0
    return true;
}
```

### 8. StartFall / BeginFreefall (VA 0x007C61F0)

Sets up the freefall state.

```cpp
void CMovement::StartFall(float initialVelocity)
{
    ClearCollisionState();                            // calls 0x7C5CD0

    // Set falling flags, clear ground flags
    this->movementFlags &= ~(MOVEFLAG_FORWARD_MASK);
    this->movementFlags |= MOVEFLAG_FALLING;         // 0x2000

    this->fallTime_ms = 0;                           // [esi+0x78]
    this->fallStartZ = this->positionZ;              // [esi+0x7C] = [esi+0x18]
    this->currentFallSpeed = initialVelocity;        // [esi+0xA0]
}
```

### 9. SetTerminalVelocity (VA 0x007C6160)

Dynamically sets terminal velocity. Called during client initialization.

```cpp
void SetTerminalVelocity(float baseSpeed)
{
    g_terminalVel = baseSpeed * STEP_HEIGHT_FACTOR;  // [0x87D894] = param * 1.0936
}
// Swim version at 0x7C6180 writes to [0x87D898]
```

### 10. CollisionResponse (VA 0x007C5A20)

Applies collision response including normal scaling by sin(45°).
This is the **slide vector computation** — called after a collision test.

```cpp
void CMovement::CollisionResponse(int responseType)
{
    if (movementFlags & 0x2000 && responseType == 0)   // falling + no collision
        return;

    UpdateMovementVectors();                           // calls 0x7C5880

    uint8 flags = (uint8)(movementFlags & 0x0F);

    if (flags & 0x03) {  // has both forward + lateral components
        if (flags & 0x0C) {  // also has vertical component
            if (flags & 0x02) {  // specific collision type
                // Full 3D collision response
                // Negate velocity, build slide normal
                vec3 normal = GetCollisionNormal();
                vec3 velocity = {this->velX, this->velY, this->velZ};

                // Apply collision: reflect and add normals
                // ... (vector math using position at +0x5C and velocity at +0x68)

                // Scale all components by sin(45°) = 0.707107
                position.x *= SIN_45;   // fmul [0x81DA54]
                position.y *= SIN_45;
                position.z *= SIN_45;
                velocity.x *= SIN_45;
                velocity.y *= SIN_45;
            }
        }
        // ... (other collision branches)
    }
}
```

---

## CMovement Structure Layout (Partial)

Reconstructed from field access patterns:

```
offset  type     name                 evidence
------  ----     ----                 --------
+0x10   vec3     position?            lea eax,[edi+0x10] in 0x7C6381
+0x18   float    positionZ            used in GetFallZ (+displacement)
+0x30   ptr      spline?              [esi+0x30] in 0x7C5D0A
+0x38   uint64   transportGuid        [edi+0x38..0x3C] in 0x7C6352
+0x40   uint32   movementFlags        all functions check this
+0x5C   vec3     collisionPosition    lea edi,[esi+0x5C]
+0x68   float    velocityComponent1   swapped/negated in collision
+0x6C   float    velocityComponent2   swapped/negated in collision
+0x78   int32    fallTime_ms          multiplied by 0.001 for seconds
+0x7C   float    fallStartZ           set in StartFall to positionZ
+0xA0   float    currentFallSpeed     gravity integration target
+0xA4   ptr      transport            collision/platform object
```

---

## Movement Flag Checks Found

| Flag | Hex | Check Location | Purpose |
|---|---|---|---|
| Forward+Lateral | `0x03` | 0x7C5A46 | Has XY velocity component |
| Vertical | `0x0C` | 0x7C5A4E | Has Z velocity component |
| Direction bit | `0x04` | 0x7C5ADC | Negate X vs Y velocity |
| Falling | `0x2000` | 0x7C5A2C | In freefall state |
| Swimming | `0x200000` | 0x7C6261 | Swim jump velocity |
| Safe Fall | `0x20000000` | 0x7C5D23 | Use swim terminal vel |
| Transport | `0x02000000` | 0x7C6367 | On a transport/elevator |
| Swim+Fly+Lev | `0x203800` | 0x7C61D6 | Blocks jumping |

---

## CMovement Structure Layout (Complete — from deep binary analysis)

```
+0x10: vec3     startPos (x,y,z)
+0x18: float    positionZ
+0x1C: float    startFacing
+0x2C: float    scale (default 1.0)
+0x30: ptr      spline?
+0x38: uint64   GUID (low + high)
+0x40: uint32   movementFlags
+0x44: vec3     currentPos (x,y,z)
+0x50: float    facing (radians)
+0x54: float    pitch (radians)
+0x58: uint32   moveTimestamp (ms)
+0x5C: vec3     direction (normalized)
+0x68: float    forwardSpeed (computed)
+0x6C: float    strafeSpeed (computed)
+0x70: float    pitchCos
+0x74: float    pitchSin
+0x7C: float    heightRef (from startPos.z)
+0x84: float    groundZ
+0x88: float    runSpeed (server-set via SMSG_FORCE_*_SPEED_CHANGE)
+0x8C: float    runBackSpeed
+0x90: float    swimBackSpeed
+0x94: float    swimSpeed
+0x98: float    swimBackSpeedAlt
+0x9C: float    turnPitchRate
+0xA0: float    fallStartVelocity
+0xA4: ptr      transport
+0xB0: float    collisionSkinFraction (0.333333 = 1/3)
+0xB4: float    stepUpHeight (2.027778)
+0xB8: float    collisionScale (1.0)
```

### GetCurrentSpeed (VA 0x7C4C90)

Speed selection logic based on movement flags:
```cpp
float CMovement::GetCurrentSpeed() {
    if (!(flags & 0x0F)) return 0.0f;           // no directional input
    if (transport) return transport.dist / transport.duration * 1000.0;
    if (flags & DESCENDING) {
        if (flags & BACKWARD) return max(swimBackSpeedAlt, swimSpeed);
        return swimSpeed;
    }
    if (!(flags & WALK_MODE) && !(flags & FLYING)) {
        if (flags & BACKWARD) return max(swimBackSpeed, runBackSpeed);
        return runBackSpeed;  // Note: uses +0x8C (runBackSpeed) for non-walk forward
    }
    return max(runSpeed, runBackSpeed);
}
```

### CollisionResponse / Diagonal Damping (VA 0x7C5A20)

When moving forward+strafe simultaneously, velocity is multiplied by sin(45°) = 0.707107:
```cpp
if (hasForward && hasStrafe) {
    position.x *= SIN_45;  // [0x81DA54]
    position.y *= SIN_45;
    position.z *= SIN_45;
    forwardSpeed *= SIN_45;
    strafeSpeed *= SIN_45;
}
```

### Movement Flag Masks

| Mask | Value | Purpose |
|---|---|---|
| Directional check | `0x200F` | FORWARD\|BACKWARD\|STRAFE_L\|STRAFE_R\|LEVITATING |
| Any movement | `0x20FF` | Above + TURN + PITCH |
| Simple sync | `0x75A01DFF` | Flags copied from movement update packet |
| Full sync | `0x75A07DFF` | Above + PENDING_STOP/PENDING_STRAFE_STOP |

---

## Parity Status (updated 2026-03-21)

| Aspect | WoW.exe | Our Implementation | Status |
|---|---|---|---|
| **Gravity** | `19.29110527` | `19.29110527f` | **DONE** |
| **Jump velocity** | `-7.955547` / `-9.096748` | Defined in constants | **DONE** |
| **Terminal velocity** | `60.148003` / `7.0` (safe fall) | Both implemented | **DONE** |
| **Fall displacement** | Two-phase (accel + terminal) | Two-phase in ProcessAirMovement + PerformVerticalPlacementOrFall | **DONE** |
| **Safe Fall flag** | MOVEFLAG 0x20000000 → termVel=7.0 | Checked in ApplyGravity + ComputeFallDisplacement | **DONE** |
| **Diagonal damping** | sin(45°) = 0.707107 | Applied in CalculateMoveSpeed | **DONE** |
| **Step height** | 2.027778 yards | Updated from 2.125 | **DONE** |
| **Collision skin** | 0.333333 (1/3 bbox) | Added as constant | **DONE** |
| **Airborne flag masking** | Directional bits ignored when falling | effectiveFlags strips directional bits | **DONE** |
| **Root flag masking** | All movement blocked when rooted | effectiveFlags strips movement bits | **DONE** |
| **Speed epsilon** | `2.384e-07` | Updated | **DONE** |
| **Time unit** | Integer ms × 0.001 | ms→s at StepV2 entry, s→ms at output | **DONE** |
| **Collision response** | sin(45°) on slide vectors in CollisionResponse | PhysX-style reflection (architecturally different) | **ACCEPTABLE** — our VMAP sweep produces equivalent results |
| **Ground snap** | Not fully decompiled in binary | Multi-ray ground detection with step-up/down | **ACCEPTABLE** — exceeds binary reference |
| **Collision sweep** | AABB 2-pass (0x633840): full displacement + half-step | Capsule sweep multi-pass | **ACCEPTABLE** — different primitive, same behavior |
| **Slope limit** | tan(50°) = 1.19175363 @ 0x80E008 | WALKABLE_TAN_MAX_SLOPE = 1.19175363 | **DONE** |
| **AABB expansion** | √2 = 1.414214 @ 0x80E00C for diagonal step-down | Added as SQRT_2 constant | **DONE** |
| **Skin epsilon** | 1/720 = 0.001389 @ 0x80DFEC | Added as COLLISION_SKIN_EPSILON | **DONE** |
| **Speed thresholds** | >60²=teleport, <3²=jitter @ 0x80C734/0x80C5BC | Added as constants | **DONE** |

## Collision Sweep Architecture (VA 0x633840)

WoW.exe uses a **2-pass swept AABB** for local player collision:

### AABB Construction
- **Horizontal**: `position ± collisionSkin` (0.333333 = 1/3 bbox)
- **Vertical**: `position.Z` to `position.Z + stepHeight` (2.027778)
- On transport: displacement is rotated through the transport's 3x3 matrix via `Vec3TransformCoord` (0x4549A0)

### Grounded Path (not falling, not swimming)
1. **Slope limit**: `slopeLimit = max(boundingRadius * tan(50°), collisionSkin + 1/720)`
2. **Pass 1 — Full displacement merge**: build the displaced AABB and union it with the start box via `0x6373B0`
3. **Pass 2 — Half-step merge**: build the contracted half-step AABB and union it into the same query volume via `0x6373B0`
4. **Step height adjust**: `bbox.maxZ += min(2*radius, speed*dt)`, then `bbox.minZ = bbox.maxZ - (stepHeight + radius*tan(50°))`
5. **Terrain test**: `CWorldCollision::TestTerrain` (0x6721B0) — query the merged volume

### Falling Path (MOVEFLAG_FALLING 0x2000)
1. Compute fall displacement via `ComputeFallDisplacement` (0x7C6140)
2. Merge the displaced fall box into the current query volume via `0x6373B0`
3. Slope descent clamp: `displacement.Z *= -tan(50°)`
4. Epsilon expansion sweep: `0x637300`/`0x6372D0`
5. Terrain test: `TestTerrain` (0x6721B0)

### Swimming Path (MOVEFLAG_SWIMMING 0x200000)
1. Half displacement: `displacement *= 0.5`
2. Build end-position AABB with `collisionSkin * √2` vertical contraction
3. Merge that displaced swim box into the query volume via `0x6373B0`
4. `TestTerrain` with flag `0x30000`
5. For each contact: negate the contact normal via `0x637330`; the actual slide helper remains unresolved in this slice

### Key Functions
| Address | Name | Purpose |
|---------|------|---------|
| `0x633840` | `CMovement::CollisionStep` | Main collision orchestrator |
| `0x6373B0` | `AABB::Merge` helper | Unions the current query box with another AABB |
| `0x6721B0` | `CWorldCollision::TestTerrain` | Static position terrain test |
| `0x637300` | `CWorldCollision::ExpandAndSweep` | Epsilon-expanded sweep |
| `0x637330` | `Vec3Negate` helper | Flips the contact normal vector after `TestTerrain` |
| `0x4549A0` | `Vec3TransformCoord` | 3x3 matrix × vector |
| `0x617430` | `CMovement::GetBoundingRadius` | Unit bounding radius |
| `0x7C6140` | `CMovement::ComputeFallZ` | Fall displacement from fallTime |

### Architectural Difference: AABB vs Capsule
WoW.exe uses an **axis-aligned bounding box** (AABB) for collision, NOT a capsule. Our engine uses capsule sweeps via VMAP's `SceneQuery::SweepCapsule`. Both produce equivalent results for normal gameplay — the capsule is slightly more accurate for diagonal movement near corners, while the AABB is faster. The behavioral difference is negligible for physics parity.

## Spatial Collision Grid (VA 0x6AA8B0)

The core intersection query operates on a spatial grid covering the entire map:

### Grid Parameters
| Constant | Address | Value | Meaning |
|----------|---------|-------|---------|
| Grid center | `0x7FFAB4` | 17066.666 | `51200/3` — world-space center offset |
| Grid scale | `0x810AE4` | 0.24 | World → grid conversion (`1/4.1667`) |
| Cell center | `0x86AA2C` | 0.5 | Half-cell offset for rounding |
| Grid extent | `0x7FFAB0` | 34133.332 | `2 × 17066.666` — full map width |
| Walkable Z | `0x80DFFC` | 0.642788 | cos(50°) — min normal.Z for walkable |

### Grid Structure
- **Map size**: 34133.33 yards (matches WoW's 64×64 ADT grid at 533.33 yards/ADT)
- **Cell size**: ~4.167 yards (`1 / 0.24`)
- **Chunk size**: 8 cells = ~33.33 yards (matches ADT sub-chunks)
- **Grid indices**: computed as `floor(worldPos * 0.24 - 0.5)`, then `>> 3` for chunk

### Query Flow (0x6AA8B0)
1. Convert AABB bounds from world space to grid indices
2. Divide by 8 to get chunk range
3. For each chunk in range: call `0x6AADC0` (per-chunk intersection test)
4. Each chunk tests against terrain heightmap + WMO/M2 BSP trees
5. Results: array of contact structs (52 bytes each: point, normal, depth, flags)

### Contact Struct (52 bytes = 0x34)
```
+0x00  Vec3   contactPoint     World-space contact location
+0x08  float  normalZ          Quick-access normal Z (checked against 0.642788)
+0x0C  Vec3   contactNormal    Full normal vector
+0x18  float  penetrationDepth
+0x1C  ...    flags/instanceId
```

### Key Function Chain
```
CollisionStep (0x633840)
  → AABB::Merge (0x6373B0)              // Merge start + end AABBs
  → CWorldCollision::TestTerrain (0x6721B0)
    → SpatialQuery (0x6AA8B0)
      → PerChunkTest (0x6AADC0)         // Per-chunk terrain + model intersection
        → TerrainHeightmap test
        → WMO BSP tree intersection
        → M2 doodad collision
      → Filter: normal.Z >= cos(50°)
      → Copy to result array (stride 0x34)
  → Vec3Negate helper (0x637330)        // Flip contact normals from TestTerrain
```

### Grounded Post-TestTerrain Helper Notes (2026-03-25)

- `0x6367B0` is still the open grounded wall/corner driver, but the local helper chain is now better mapped:
  - `0x636610`
    - `1` contact vector: copy through
    - `2` contact vectors: merge the pair via the helper’s built-in scale constant
    - `3` contact vectors: choose the lone axis from the minority orientation group
    - `4` contact vectors: emit zero vector
  - `0x635D80`
    - computes the horizontal correction vector from the selected contact plane
    - normalizes the plane’s horizontal component, applies the movement-direction dot product and step distance, then adds the `0.001f` epsilon at `0x801360`
    - returns `XY` only (`Z = 0`)
  - `0x635C00`
    - computes the vertical correction from the selected contact plane
    - returns `Z` only (`X = Y = 0`)
    - can also mutate the in-flight movement fraction / distance pointer before the correction is returned
  - `0x636100`
    - remains partially unresolved, but it gates whether the grounded path takes the `0x635D80` horizontal-correction branch or the alternate `0x635C00` retry branch with `this->flags |= 0x04000000`
- Practical implication for native parity work:
  - the remaining stateless mismatch is no longer the merged blocker selector or the horizontal epsilon nudge
  - the open work is the selected-plane `Z` correction and distance bookkeeping that still happens around `0x635C00` / `0x636100`

### Selected-contact container (`0xC4E52C` / `0xC4E534` / `0xC4E544`)

- Fresh local disassembly on 2026-03-26 adds one important structural constraint around the grounded helper chain:
  - `0x6312C0` zero-initializes a small global container rooted at `0xC4E52C`
  - that container carries separate child-array pointers at:
    - `0xC4E534` = `0x34`-stride contact/plane records
    - `0xC4E544` = `0x08`-stride paired selector payload
  - both child arrays default to capacity `0x100`
- `0x6367B0` consumes those globals as a selected-contact path, not a whole-query walk:
  - it reads one chosen `0x34` record from `0xC4E534[index]`
  - it also reads one paired `0x08` payload from `0xC4E544[index]`
  - the following `0x6334A0` / `0x636100` work therefore runs on a selected entry, not on every contact in the merged query
- `0x6351A0` is the only direct caller currently identified that returns the paired `0xC4E544[index]` payload:
  - it first calls `0x632BA0`
  - then gates the selected index through `0x633720`
  - then checks the local candidate buffer with `0x635410` / `0x6353D0`
  - only after that chain does it hand the `0xC4E544[index]` pair back to its caller
  - fresh 2026-03-26 full-window disassembly adds the branch shape:
    - if `0x633720` succeeds and `0x635410` finds a matching local candidate, `0x6351A0` returns `0xC4E544[index]` directly and marks the state out-param
    - if `0x633720` succeeds but `0x635410` fails, it returns a zeroed pair with success
    - if `0x633720` fails, it falls through the `0x7C5DA0` / `0x6353D0` gate and then into `0x635090` for the alternate pair result
  - fresh raw captures now live in `docs/physics/0x633720_disasm.txt` and `docs/physics/0x635090_disasm.txt`
  - `0x633720`
    - wrapper builds `position + offset`, passes that world point plus the selected index and `this+0x15C` into `0x633760`, then returns the inverse boolean of `0x633760`
  - `0x633760`
    - reads `normal.z` from `0xC4E534[index]`
    - compares that selected contact against the relaxed `0x80E000` threshold when `0x5FA550(...)` is false and against the standard walkable `0x80DFFC` threshold when `0x5FA550(...)` is true
    - on the threshold-sensitive path it then calls `0x6335D0` before deciding whether the selected index stays on the direct `0xC4E544[index]` return path or falls into the alternate `0x635090` path
  - `0x635090`
    - first calls `0x6336A0`
    - on success it delegates to `0x634AE0` to produce the working 3-vector
    - on failure it negates the incoming vector
    - both paths normalize that 3-vector and then write the final two-float pair result back to the caller
  - `0x5FA550` now has a raw capture in `docs/physics/0x5FA550_disasm.txt`
    - it walks model/tree flags rooted at `this+0x110` and can recurse through `0x468460(..., 0x1DF)` before returning `0` or `1`
    - practical implication: the relaxed-vs-standard threshold split inside `0x633760` is model-property driven, not a geometric point-in-triangle test
- `0x632700` adds one concrete filter detail for that selector chain:
  - candidate contacts are rejected only when the candidate-direction dot product is effectively non-opposing (`>= -1e-5f`)
  - the local client does not carry our custom grounded blocker thresholds like `opposeScore <= 0.15f` or dominant-axis `> 0.25f`
  - removing those thresholds alone did not fix the packet-backed Undercity frame-15 transport stall, which reinforces that the real blocker is the missing selected-contact state/path rather than the score guards by themselves
- Practical implication for parity work:
  - do not broadcast `CheckWalkable` over every merged-query contact as a replacement for the raw `walkable` bit
  - the missing parity path is the binary-selected contact + grounded-wall-state feed into `0x6334A0`, not the helper body by itself
  - production-DLL tracing on packet-backed Undercity frame 16 now adds one more concrete constraint: the selected blocker contact resolves only to parent static WMO instance `0x00003B34` with `instance/model flags = 0x00000004` and `rootWmoId = 1150`, while no WMO group match is found for the exact contact triangle
  - inference from that trace: the current `SceneCache` / `TestTerrainAABB` path is preserving the triangle geometry but collapsing the deeper child model identity the client's `0x5FA550` model-property walk uses
  - fresh bounded scene-cache extraction now narrows that one step further: the frame-16 selected blocker is not a doodad child triangle, it is a static WMO-group triangle that round-trips with `rootId = 1150`, `groupId = 3228`, and `groupFlags = 0x0000AA05`
  - the normal `EnsureMapLoaded(...)` path now rebuilds legacy metadata-less `.scene` caches with their preserved bounds, so production queries can auto-upgrade to metadata-bearing caches instead of staying flattened on parent-only WMO identity
  - practical follow-up: this is not an "extract more raw MPQ triangles" blocker anymore; the next native parity pass should trace the selected-contact producer chain (`0x633720` / `0x635090` / paired `0xC4E544`) on top of the now-correct WMO-group metadata feed

## Remote Unit Extrapolation (VA 0x616DE0)

Used for OTHER players/NPCs (not local player). Predicts position between heartbeats:

### Per-Frame Loop
```
while (accumulatedTime < totalDelta):
    if (flags & 0x20FF):  // any movement flag active
        if (hasSpline && !paused):
            ExtrapolateFromFlags(0x616AF0)
            speed² check: >3600 = teleport, <9 = jitter
        else:
            SplineStep(0x7C5360)
            TerrainCollisionCheck(0x6191C0)  // optional
    ApplyDisplacement(0x616CB0)
    accumulatedTime += frameDelta
```

### Speed Thresholds
| Threshold | Value | Check |
|-----------|-------|-------|
| Teleport | speed² > 3600 (>60 y/s) | Reject displacement |
| Jitter | speed² < 9 (<3 y/s) | Ignore micro-movement |

## Packet Send Pipeline

### Movement Command Dispatch (VA 0x615C30)
Jump table at `0x616580` maps 39 movement commands (0x00-0x26) to handlers.
Each handler validates the movement, then sends the appropriate opcode.

| Command Index | Opcode | Name | Validator |
|--------------|--------|------|-----------|
| 0x00 | 0x00B5 | MSG_MOVE_START_FORWARD | `0x7C6AE0` (CanStartForward) |
| 0x01 | 0x00B6 | MSG_MOVE_START_BACKWARD | `0x7C6AE0` (CanStartBackward) |
| 0x02 | 0x00B7 | MSG_MOVE_STOP | `0x7C6BA0` (CanStop) |
| 0x03 | 0x00B8 | MSG_MOVE_START_STRAFE_LEFT | `0x7C6C50` (CanStrafe) |
| 0x04 | 0x00B9 | MSG_MOVE_START_STRAFE_RIGHT | `0x7C6C50` (CanStrafe) |
| 0x05 | 0x00BA | MSG_MOVE_STOP_STRAFE | `0x7C6D30` |
| 0x06 | 0x00EE | MSG_MOVE_HEARTBEAT (fall) | `0x7C61C0` (CanSendFall) |

### Packet Builder (VA 0x600A30)
```
SendMovementPacket(this, timestamp, opcode, flags, ...)
{
    1. Store current orientation at +0xC70 (for facing delta detection)
    2. If SWIMMING: store pitch at +0xC74
    3. Call BuildMovementInfo(0x600860) → serializes into wire buffer
    4. Call NetworkSend(0x5AB630) → pushes to socket
    5. Call UpdateInternalState(0x615B80) → syncs client state
}
```

### Facing Change Detection (0x60E1EA)
Before sending direction change packets (0xDA/0xDB), checks:
- `abs(currentOrientation - storedOrientation)` against threshold at `0x80C408`
- If SWIMMING: also checks pitch delta
- Only sends if delta exceeds threshold (prevents micro-facing packet spam)

### Key Packet Functions
| Address | Name | Purpose |
|---------|------|---------|
| `0x615C30` | `CMovement::ProcessPendingMoves` | Dispatch movement commands |
| `0x616580` | Jump table | 39 command → handler mappings |
| `0x60E0A0` | `CMovement::SendMovementUpdate` | Validates + sends movement packet |
| `0x600A30` | `CMovement::SendPacket` | Builds and sends MovementInfo |
| `0x600860` | `CMovement::BuildMovementInfo` | Serializes MovementInfo to buffer |
| `0x5AB630` | `CDataStore::Send` | Pushes buffer to network socket |
| `0x615B80` | `CMovement::OnPacketSent` | Post-send state update |

## MovementInfo Struct (VA 0x7C6340 — FillMovementInfo)

Exact binary layout of the MovementInfo block written to every movement packet.
**Verified: our `MovementPacketHandler.cs` matches byte-for-byte.**

### Wire Format
```
Offset  Size  Field                CMovement source     Condition
------  ----  -----                ----------------     ---------
+0x00   4     movementFlags        +0x40 & 0x75A07DFF   always
+0x04   4     timestamp            function arg          always
+0x08   4     positionX            +0x10                 always
+0x0C   4     positionY            +0x14                 always
+0x10   4     positionZ            +0x18                 always
+0x14   4     orientation          +0x1C                 always
--- if MOVEFLAG_ONTRANSPORT (0x2000000): ---
+0x18   8     transportGuid        +0x38, +0x3C
+0x20   12    transportOffset      from 0x7C4930
+0x2C   4     transportOrientation from 0x7C4AE0
--- if MOVEFLAG_SWIMMING (0x200000): ---
        4     swimPitch            +0x20
--- always: ---
        4     fallTime             +0x78 (int32 ms)
--- if MOVEFLAG_JUMPING (0x2000): ---
        4     jumpVerticalSpeed    +0xA0 (currentFallSpeed)
        4     jumpSinAngle         +0x68 (forwardSpeed)
        4     jumpCosAngle         +0x6C (strafeSpeed)
        4     jumpHorizontalSpeed  +0x84
--- if MOVEFLAG_SPLINE_ELEVATION (0x4000000): ---
        4     splineElevation      +0x80
```

### Flag Mask Application (0x618909)
```asm
and edx, 0x75A07DFF    ; Strip internal-only flags before wire
```
Strips: `PENDING_STOP (0x80000)`, `PENDING_STRAFE_STOP (0x100000)`,
`PENDING_FORWARD (0x200000)`, `ASCENDING (0x40000)`, `LOCAL_DIRTY (0x80000000)`,
`SPLINE_ENABLED (0x400000)`.

### Transport Flag (0x2000000)
- If `transportGuid != 0`: set bit `0x2000000` in flags
- If `transportGuid == 0`: clear bit `0x2000000`
- Transport offset = position relative to transport origin (from `0x7C4930`)

### Support-State Parity Note (2026-03-24)
- Fresh disassembly of `CMovement::Update` (`0x618C30`) and `CMovement::CollisionStep` (`0x633840`) continues to show explicit transport-local persistence (`transportGuid`, local offset, local orientation) before collision is run in world space.
- The collision branch rotates transport-local displacement through the transport matrix, then performs world collision queries.
- No equivalent persisted “static triangle token” path has been identified for ordinary terrain/WMO support.
- A second spot-check over `0x618C30..0x618D60` and `0x633840..0x6339C0` still only reinforced that same pattern, so support identity should stay coherent only for moving-base metadata and should use the same dynamic runtime ID across AABB and capsule query families.
- Current parity interpretation:
  - Static ground support is recomputed each frame from collision/height queries.
  - Moving-base continuity is the state that persists across frames.
  - Engine-side `standingOnInstanceId` / local support-point state should therefore be treated as moving-base support metadata, not a generic terrain cache.

### Opcodes Requiring Extra Payload (0x6009B0)
These opcodes append additional data after MovementInfo:
`0xE3`, `0xE5`, `0xE7`, `0xF6`, `0x2CF`, `0x2D0`, `0x2DB`, `0x2DD`, `0x2DF`
