# 04_SweepTest_moveCharacter.md — Motion Decomposition and Sweep Passes

## Title
**Inside `SweepTest::moveCharacter` — Decomposing Motion (Up/Side/Down) and Step Handling**

## Purpose

`SweepTest::moveCharacter()` is the heart of PhysX CCT movement. It decomposes the desired displacement into three components (upward, lateral, downward) and processes each in sequence to implement stepping and sliding.

This document breaks down:
- How motion is split into **Up / Side / Down**
- How **auto-step (stepOffset)** is injected into the Up pass
- How the three **sweep passes** implement stepping, wall sliding, and ground snapping
- How **non-walkable slope** logic interacts with side/down passes (and constrained climbing)

---

## Function Signature and Inputs

~~~cpp
PxControllerCollisionFlags SweepTest::moveCharacter(
    const InternalCBData_FindTouchedGeom* userData,
    InternalCBData_OnHit*                userHitData,
    SweptVolume&                         volume,
    const PxVec3&                        direction,
    const UserObstacles&                 userObstacles,
    PxF32                                minDist,
    const PxControllerFilters&           filters,
    bool                                 constrainedClimbingMode,
    bool                                 standingOnMoving,
    const PxRigidActor*&                 touchedActor,
    const PxShape*&                      touchedShape,
    PxU64                                contextID
);
~~~

### Key Inputs

- **`direction`**  
  Desired movement vector for this frame (world space). In PhysX this is typically **post overlap-recovery** displacement (often referred to as `disp`).

- **`volume`**  
  Swept representation of controller shape + pose (capsule/box). Carries `mCenter` and geometry details. `moveCharacter()` updates `volume.mCenter` as movement occurs.

- **`standingOnMoving`**  
  Indicates whether the character starts the frame on a moving object. This affects step behavior and special cases (e.g., stepping/following motion even when side input is near zero).

- **`constrainedClimbingMode`**  
  True for capsule controllers in constrained climbing mode (e.g., `PxCapsuleClimbingMode::eCONSTRAINED`). In constrained mode, the controller **cannot climb non-walkable slopes**; horizontal motion into steep slopes is effectively cancelled (via the “walk experiment” path).

- **`filters`, `userObstacles`, `userData`, `userHitData`**  
  Collision filtering + supplemental obstacle sets + callback context. Each sweep pass can trigger hit callbacks (e.g., `onShapeHit`).

### Outputs

- **`touchedActor`, `touchedShape`** *(out)*  
  Set when the downward sweep finds a walkable surface. Used later to update “standing on” state.

- **Return value**: `PxControllerCollisionFlags`  
  Bitfield indicating collisions on:
  - `eCOLLISION_SIDES`
  - `eCOLLISION_UP`
  - `eCOLLISION_DOWN`

---

## Motion Decomposition (Up / Side / Down)

The first major operation is splitting `direction` into a component parallel to the controller’s **up axis** and a component tangent to it.

### Decompose `direction` into normal + tangent

- Compute:

  - `dir_dot_up = direction.dot(upDirection)`

- Use:

  - `decomposeVector(normal_compo, tangent_compo, direction, upDirection)`

Where:

- `normal_compo` = projection of `direction` onto `upDirection`
- `tangent_compo` = `direction - normal_compo` (planar / lateral component)

### Assign to UpVector vs DownVector

Initialize:
- `UpVector = (0,0,0)`
- `SideVector = (0,0,0)`
- `DownVector = (0,0,0)`

Rules:
- If `dir_dot_up > 0`:  
  - `UpVector = normal_compo`
  - `DownVector = 0`
- Else:
  - `DownVector = normal_compo` *(may be zero if purely lateral)*
  - `UpVector = 0`

Always:
- `SideVector = tangent_compo`

So:
- `direction = UpVector + SideVector + DownVector`

Examples:
- Flat walking: `Up=0`, `Down=0`, `Side=direction`
- Jumping: `Up>0`, `Down=0`, `Side=lateral`
- Falling: `Up=0`, `Down<0`, `Side=lateral`

---

## Step Offset Injection (Auto-Step)

PhysX auto-step works by **adding vertical lift** (stepOffset) to the **UpVector**, *but only when stepping makes sense.*

### When to add `stepOffset`

PhysX computes whether side motion is effectively present:

- `sideVectorIsZero = isAlmostZero(SideVector) && !standingOnMoving`

If **not** zero (meaning: lateral motion exists *or* we’re on moving ground), then:

- `UpVector += upDirection * stepOffset`

This produces the classic step pattern:
1. Move up by `stepOffset`
2. Move sideways at raised height
3. Move down to land on top of a small obstacle

### Why it’s conditional

If you allow auto-step while standing still (or moving only vertically), the controller can “auto-climb” tiny bumps unintentionally. PhysX avoids that by requiring meaningful lateral intent, except for some moving-platform cases.

### Cancel stepOffset when already moving upward

PhysX disables auto-step in cases where the input already contains an up component (jumping / uphill movement), unless the character is being carried upward by a moving object:

- If `dir_dot_up > 0` and **not** `standingOnMovingUp` → `stepOffset = 0`

Rationale:
- Jumping shouldn’t get “extra” magical lift.
- Uphill movement already has an up component; adding stepOffset can overshoot / destabilize.

---

## Temporal Box for Scene Query (Candidate Gathering)

Before doing sweeps, PhysX computes a broad region encompassing the volume’s path:

~~~cpp
volume.computeTemporalBox(*this, temporalBox, volume.mCenter, direction);
updateTouchedGeoms(userData, userObstacles, temporalBox, filters, SideVector);
~~~

- `computeTemporalBox(...)` expands bounds to include the entire motion path (+ offsets).
- `updateTouchedGeoms(...)` gathers candidate geometry/obstacles to test against in the narrowphase sweeps.

This is an optimization layer: you can replicate behavior either with a similar broadphase pre-query or by letting your engine’s scene query handle it per sweep.

---

## Sweep Pass 1 — Upward Sweep (Step Up)

### When it runs
- Only if `UpVector` is non-zero  
  (either from real upward motion or injected stepOffset)

### What it does
Sweeps the controller upward to:
- Apply step lift (`stepOffset`)
- Handle real upward motion
- Detect ceilings / overhead obstructions

PhysX calls something like:

~~~cpp
doSweepTest(..., UpVector, SideVector, maxIterUp, &NbCollisions, minDist, filters, SWEEP_PASS_UP, ...);
~~~

### Iteration count (`maxIterUp`)
- If `mPreventVerticalSlidingAgainstCeiling` is enabled: `maxIterUp = 1`
- Otherwise:
  - If `SideVector` is ~zero: allow more iterations (`MAX_ITER`)
  - Else: `maxIterUp = 1`

In practice: **Up pass is typically one sweep** when lateral movement exists.

### Ceiling handling
If Up sweep hits a surface with a normal pointing “down” (ceiling), PhysX can set an internal flag (often described as `preventVerticalMotion`) which later cancels vertical components during side/down phases to prevent scraping/creeping under ceilings.

---

## Sweep Pass 2 — Side Sweep (Horizontal / Lateral Slide)

### When it runs
- If `SideVector` is non-zero

### What it does
Sweeps laterally and performs collide-and-slide.

PhysX calls:

~~~cpp
doSweepTest(..., SideVector, SideVector, maxIterSides, &NbCollisions, minDist, filters, SWEEP_PASS_SIDE, ...);
~~~

- `maxIterSides` is typically `MAX_ITER` (~10)
- Iteration enables sliding across multiple contacts in one frame (corners, compound obstacles).

### Collision flags
If any collision occurs in side sweep:

~~~cpp
if (NbCollisions)
    CollisionFlags |= PxControllerCollisionFlag::eCOLLISION_SIDES;
~~~

### Non-walkable slope trigger during side pass (constrained climbing)
PhysX can detect “too steep” contacts during side movement. In constrained mode, a steep slope hit at a height that implies climbing (above original bottom + stepOffset) sets:

- `STF_HIT_NON_WALKABLE`

This often triggers an **early exit** because the controller will re-run movement in the “walk experiment” mode to resolve non-walkable slope behavior.

---

## Sensor Sweep (Ground Validation Without Moving)

After side pass (and before the real down pass), PhysX may do a *small* downward sweep (a “sensor”) to detect/validate ground contact data without committing movement.

Typical structure:

~~~cpp
PxExtendedVec3 saved = volume.mCenter;
doSweepTest(..., sensorDownVector, SideVector, 1, &NbCollisions, minDist, filters, SWEEP_PASS_SENSOR, ...);
volume.mCenter = saved; // restore (pure query)
~~~

This exists to ensure certain “validate triangle” state becomes available even when small movements might be culled by `minDist`.

---

## Sweep Pass 3 — Downward Sweep (Gravity / Ground Snap)

### When it runs
Almost always. Even if `DownVector` is zero, PhysX still often needs a down check after stepping up or moving sideways to:
- Snap to ground
- Detect “standing on” contacts
- Enter/exit falling state

PhysX calls:

~~~cpp
doSweepTest(..., DownVector, SideVector, maxIterDown, &NbCollisions, minDist, filters, SWEEP_PASS_DOWN, ...);
~~~

### Iteration count (`maxIterDown`)
Typically:
- `maxIterDown = 1` (normal behavior)
- In walk experiment + “force sliding” mode: `maxIterDown = MAX_ITER`  
  to allow sliding down steep slopes fully.

This is how PhysX differentiates:
- **Prevent climbing (no force slide):** can stand on steep slopes but cannot climb
- **Prevent climbing + force sliding:** will slide down steep slopes

### Collision flags
If down sweep collides:

- `CollisionFlags |= eCOLLISION_DOWN`

This typically indicates “on ground” (or at least contacted something below).

### Non-walkable slope final detection (down pass)
Down pass is where PhysX often finalizes whether the “ground” is too steep to be walkable.

If:
- slope is too steep (`testSlope(...)` indicates non-walkable), and
- contact height suggests stepping/climbing beyond `stepOffset`

Then:
- `STF_HIT_NON_WALKABLE` is set  
- The controller may early-exit to rerun movement via walk experiment.

---

## Collision Flags Summary

At the end of `moveCharacter`, the returned bitfield is built roughly as:

- `eCOLLISION_SIDES` → any lateral collision during side pass
- `eCOLLISION_DOWN`  → any collision during down pass (ground contact)
- `eCOLLISION_UP`    → upward obstruction (typically from up pass / ceiling logic)

`eCOLLISION_UP` is often derived from:
- collisions during the up sweep, especially ceiling hits, and/or
- internal `preventVerticalMotion` behavior indicating overhead obstruction.

---

## Pseudocode Outline (Structural Model)

> This is a structural approximation meant to preserve the flow and branch intent.

~~~cpp
PxControllerCollisionFlags SweepTest::moveCharacter(...)
{
    PxControllerCollisionFlags flags = 0;

    // 1) Mark moving-up state
    if (direction.dot(upDir) > 0)
        mFlags |= STF_IS_MOVING_UP;
    else
        mFlags &= ~STF_IS_MOVING_UP;

    // 2) Decompose motion
    PxVec3 normal, lateral;
    decomposeVector(normal, lateral, direction, upDir);

    PxVec3 UpVector(0), SideVector = lateral, DownVector(0);
    if (normal.dot(upDir) > 0) UpVector = normal;
    else                      DownVector = normal;

    // 3) Step offset injection
    float stepOffset = mUserParams.mStepOffset;

    if ((mFlags & STF_IS_MOVING_UP) && !standingOnMovingUp)
        stepOffset = 0.0f;

    bool sideVectorIsZero = isAlmostZero(SideVector) && !standingOnMoving;
    if (!sideVectorIsZero)
        UpVector += upDir * stepOffset;

    // 4) Gather candidates (temporal box)
    PxExtendedBounds3 temporalBox;
    volume.computeTemporalBox(*this, temporalBox, volume.mCenter, direction);
    updateTouchedGeoms(userData, userObstacles, temporalBox, filters, SideVector);

    // 5) Up pass
    if (!UpVector.isZero())
    {
        PxU32 maxIterUp = computeMaxIterUp(...);
        doSweepTest(..., UpVector, SideVector, maxIterUp, &nbColUp, ...);
        // may set preventVerticalMotion on ceiling hit
    }

    PxExtendedVec3 savedPos = volume.mCenter;

    // 6) Side pass
    if (!SideVector.isZero())
    {
        doSweepTest(..., SideVector, SideVector, MAX_ITER, &nbColSide, ...);
        if (nbColSide) flags |= eCOLLISION_SIDES;

        // constrained climbing: steep slope hit triggers non-walkable
        if (constrainedClimbingMode && steepSlopeHitAboveStepOffset)
        {
            mFlags |= STF_HIT_NON_WALKABLE;
            // early exit likely (walk experiment rerun occurs outside)
        }
    }

    // 7) Sensor pass (query-only, restore position)
    doSweepTest(..., sensorDown, SideVector, 1, &nbSensor, ...);
    volume.mCenter = savedPos;

    // 8) Down pass
    PxU32 maxIterDown = computeMaxIterDown(...);
    doSweepTest(..., DownVector, SideVector, maxIterDown, &nbColDown, ...);

    if (nbColDown) flags |= eCOLLISION_DOWN;

    // non-walkable slope detection may set STF_HIT_NON_WALKABLE (trigger rerun)
    if (groundIsSteepAndAboveStepOffset)
        mFlags |= STF_HIT_NON_WALKABLE;

    // touchedActor/touchedShape updated from down contact
    return flags;
}
~~~

---

## Stepping Logic Recap

Stepping emerges from **Up + Side + Down** sequencing:

1. **Up pass** lifts by `stepOffset` (if lateral intent exists)
2. **Side pass** moves laterally at the raised height (clearing small obstacles)
3. **Down pass** drops onto the obstacle top (or back to ground)

If the obstacle is taller than `stepOffset`, the controller can’t clear it via the raised side pass, and collision/non-walkable logic routes movement into “blocked/slide” behavior instead.

---

## Non-Walkable Slope Logic Recap

- **Prevent climbing (no force slide):**
  - Can stand on steep slopes
  - Cannot ascend them
  - Down pass typically uses `maxIterDown = 1` (no iterative sliding)

- **Prevent climbing + force sliding:**
  - Will slide down steep slopes
  - Down pass allows `maxIterDown = MAX_ITER` during walk experiment rerun

- **Constrained climbing mode:**
  - More eager to classify steep slope contacts during side pass as non-walkable
  - Cancels ascent immediately rather than “partially climbing then resolving”

---

## Additional Internal Flags / State (Advanced)

Common internal markers referenced in this flow:
- `STF_VALIDATE_TRIANGLE_SIDE / STF_VALIDATE_TRIANGLE_DOWN`
- `STF_HIT_NON_WALKABLE`
- `STF_WALK_EXPERIMENT`
- `mContactNormalSidePass / mContactNormalDownPass`
- `mTouchedTriMin / mTouchedTriMax`
- “prevent vertical motion” ceiling state

These can matter for edge cases (minDist culling, slope transitions, ledge edges).

---

## Porting Notes

To reproduce PhysX-like behavior:
- Always run **Up** before **Side** when stepOffset applies; otherwise you’ll collide with low obstacles instead of stepping.
- Honor `minDist` cutoffs to avoid jitter and unstable micro-iterations.
- Ensure **down pass** always validates ground after step/lateral motion.
- Mirror constrained vs unconstrained behavior when handling steep slopes.
- If you care about ceiling behavior, replicate the “prevent vertical sliding against ceiling” logic that cancels vertical components after an overhead hit.

---

## References (placeholders)

- PhysX CCT `SweepTest::moveCharacter` implementation
- Step offset logic and collision flag assignment
- Non-walkable slope / walk experiment logic (`STF_HIT_NON_WALKABLE`, `STF_WALK_EXPERIMENT`)
- `testSlope(...)`, slopeLimit usage, and constrained climbing behavior
