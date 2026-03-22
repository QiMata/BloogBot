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

## Key Differences from Current PhysicsEngine.cpp

| Aspect | WoW.exe | Our Implementation | Action |
|---|---|---|---|
| **Gravity** | `19.29110527` | `19.2911f` (truncated) | Updated to exact |
| **Jump velocity** | `-7.955547` / `-9.096748` | `7.9535f` | Updated to exact; added swim |
| **Terminal velocity** | `60.148003` (computed: `55 × 1.0936`) | `60.148f` | Updated to exact |
| **Fall displacement** | Two-phase (accel + terminal) | Single equation | Need to implement split-frame |
| **Time unit** | Integer ms × 0.001 | Float seconds | Match: convert ms→s before physics |
| **Collision response** | sin(45°) scaling on slide vectors | Custom collide-and-slide | Compare slide vector math |
| **Speed epsilon** | `2.384e-07` | `1e-6` | Updated |
