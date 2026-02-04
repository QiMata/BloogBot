# PhysX CCT – `PxControllerCollisionFlags SweepTest::moveCharacter`

> Source: your uploaded `CctCharacterController.cpp` (PhysX Character Kinematic / CCT).

This is the **core “collide & slide” + “auto-step”** routine for PhysX’s kinematic character controller (CCT).  
It moves a **swept volume** (capsule/box/…) along a requested displacement, resolves collisions, and returns **collision flags** (`UP`, `SIDES`, `DOWN`) while also setting internal state (non-walkable, triangle validation, contact normals/heights, etc).

---

## Signature

```cpp
PxControllerCollisionFlags SweepTest::moveCharacter(
    const InternalCBData_FindTouchedGeom* userData,
    InternalCBData_OnHit* userHitData,
    SweptVolume& volume,
    const PxVec3& direction,
    const UserObstacles& userObstacles,
    float min_dist,
    const PxControllerFilters& filters,
    bool constrainedClimbingMode,
    bool standingOnMoving,
    const PxRigidActor*& touchedActor,
    const PxShape*& touchedShape,
    PxU64 contextID);
```

### What it returns
`PxControllerCollisionFlags` bitmask:
- `eCOLLISION_UP`   → hit something during the **up** sweep.
- `eCOLLISION_SIDES`→ hit something during the **side** sweep.
- `eCOLLISION_DOWN` → hit something during the **down** sweep (ground / landing).

### Key side effects (state)
This function manipulates/depends on internal members such as:
- `mFlags` (internal CCT flags: slope handling, triangle validation, non-walkable, etc.)
- `mContactNormalDownPass`, `mContactNormalSidePass`
- `mContactPointHeight`, `mTouchedTriMax` (used for “how high is this contact?” tests)
- `mTouchedObstacleHandle`, `touchedActor`, `touchedShape` (last touched entity)
- `mCachedTriIndexIndex` (0/1/2 per-pass cache selection)

---

## High-level idea: **3-pass movement**

PhysX CCT resolves a full motion `direction` by decomposing it into:

1. **Up pass**: optional upward “pre-step” + actual upward component (jumping)
2. **Side pass**: lateral motion with collide-and-slide
3. **Down pass**: snap down / fall / landing, plus slope checks

Visually:

```
desired direction
   ├─ UpVector   (vertical component + optional stepOffset)
   ├─ SideVector (planar / tangent component)
   └─ DownVector (vertical component, later “undo stepOffset”)
```

---

## Step 0: setup + “moving up” state

Important locals:

- `stepOffset = mUserParams.mStepOffset` (auto-step height)
- `upDirection = mUserParams.mUpDirection`
- `originalHeight = volume.mCenter.dot(upDirection)`
- `originalBottomPoint = originalHeight - volume.mHalfHeight`
- `dir_dot_up = direction.dot(upDirection)` (signed “vertical” intent)

### `STF_IS_MOVING_UP`
If `dir_dot_up > 0`, the controller sets `STF_IS_MOVING_UP`. Otherwise it clears it.

### When the code cancels `stepOffset`
When *not* standing on a moving-up platform, and the intent is truly upward (jumping), the code cancels stepping:

- If **player is moving up** and **not** `standingOnMovingUp`, it sets `stepOffset = 0`.

**Meaning:** stepping is for “walk forward & climb a curb”. When you’re actively jumping, don’t inject an extra auto-step lift.

---

## Step 1: decompose motion into up/side/down

It decomposes the requested displacement into components parallel/perpendicular to the up vector:

- `Ps::decomposeVector(normal_compo, tangent_compo, direction, upDirection)`
- If `dir_dot_up <= 0`:  
  - `DownVector = normal_compo`  
- Else:  
  - `UpVector = normal_compo`
- `SideVector = tangent_compo`

### Disable auto-step when there’s no lateral intent
A key guard:

- If the **side motion is effectively zero**, auto-step is disabled to prevent the CCT from “climbing” tiny obstacles that move into it.

In code: it computes `sideVectorIsZero` (with special handling for arbitrary up vectors and moving platforms).

### Apply auto-step lift
If there *is* lateral motion (`!sideVectorIsZero`):

- `UpVector += upDirection * stepOffset`

This is the classic “move up a bit, move sideways, then move down” stepping pattern.

---

## Step 2: initial broadphase / touched-geometry gather

Before the per-pass sweeps, it computes a temporal AABB for the whole move:

- `volume.computeTemporalBox(..., volume.mCenter, direction)`
- `updateTouchedGeoms(userData, userObstacles, temporalBox, filters, SideVector)`

This seeds the list of potentially colliding shapes/obstacles so the per-pass sweeps don’t have to query the world blindly each time.

---

## Step 3: UP pass

Setup:
- `mCachedTriIndexIndex = 0`
- `maxIterUp` selection:
  - If `mPreventVerticalSlidingAgainstCeiling`: `maxIterUp = 1`
  - Else: `maxIterUp = (SideVector ≈ 0) ? MAX_ITER : 1`

Then (unless `STF_WALK_EXPERIMENT` is enabled) it performs:

```cpp
doSweepTest(..., volume, UpVector, SideVector,
            maxIterUp, &NbCollisions, min_dist,
            filters, SWEEP_PASS_UP, touchedActor, touchedShape, contextID);
```

If `NbCollisions > 0`:
- sets `CollisionFlags |= eCOLLISION_UP`
- **clamps** `stepOffset` so the later down pass doesn’t “undo” more than the up pass actually achieved:

```cpp
Delta = currentHeight - originalHeight;
stepOffset = min(stepOffset, Delta);
```

---

## Step 4: SIDE pass

Setup:
- `mCachedTriIndexIndex = 1`
- clears `STF_VALIDATE_TRIANGLE_SIDE`

Then:

```cpp
doSweepTest(..., volume, SideVector, SideVector,
            maxIterSides, &NbCollisions, min_dist,
            filters, SWEEP_PASS_SIDE, touchedActor, touchedShape, contextID);
```

If collisions:
- sets `CollisionFlags |= eCOLLISION_SIDES`

### “Sensor” sweep (constrained climbing mode)
There’s a special probe path:

If:
- `constrainedClimbingMode`
- `volume` is a **capsule**
- the side pass did **not** validate a triangle
- side motion magnitude `< capsuleRadius`

Then it does a *tiny extra sweep* (`SWEEP_PASS_SENSOR`) with a “sensor” displacement of length `capsuleRadius` along the side direction, restoring the volume center after the probe.

Purpose (practically): **detect immediate blockers** that might not have produced a validated triangle in the main side sweep, helping the constrained climb / non-walkable logic make better decisions.

---

## Step 5: DOWN pass

Setup:
- `mCachedTriIndexIndex = 2`
- clears `STF_VALIDATE_TRIANGLE_DOWN`
- clears `touchedActor/touchedShape` and touched obstacle handle

Undo the artificial up motion (only if we injected it):

```cpp
if(!sideVectorIsZero)
    DownVector -= upDirection * stepOffset;
```

Then:

```cpp
doSweepTest(..., volume, DownVector, SideVector,
            maxIterDown, &NbCollisions, min_dist,
            filters, SWEEP_PASS_DOWN, touchedActor, touchedShape, contextID);
```

If collisions and we weren’t moving upward (`dir_dot_up <= 0`):
- sets `CollisionFlags |= eCOLLISION_DOWN`

### Slope-related side-contact fix (in down pass)
If:
- slope handling enabled (`mHandleSlope`)
- we didn’t hit another CCT / obstacle
- triangle side validation happened (`STF_VALIDATE_TRIANGLE_SIDE`)
- and `testSlope(mContactNormalSidePass, upDirection, slopeLimit)` says “too steep”

Then in `constrainedClimbingMode`, if:
- `mContactPointHeight > originalBottomPoint + stepOffset`

… it flags `STF_HIT_NON_WALKABLE` and may early-return (unless walk experiment mode will re-run).

---

## Step 6: post-pass slope test (ground is non-walkable)

After the down pass, there’s an additional “am I standing on a steep poly?” check when moving down or not moving up:

Conditions (simplified):
- `mHandleSlope`
- not touching another CCT/obstacle
- `STF_VALIDATE_TRIANGLE_DOWN`
- `dir_dot_up <= 0`

It computes:
- `Normal = mContactNormalDownPass` (contact normal)
- `touchedTriHeight = mTouchedTriMax - originalBottomPoint`

If:
- `touchedTriHeight > stepOffset`
- and `testSlope(Normal, upDirection, slopeLimit)` returns “non-walkable”

Then:
- `mFlags |= STF_HIT_NON_WALKABLE`
- and the function returns early (unless walk experiment mode wants to continue).

---

## Step 7: walk-experiment recovery sweep

When `STF_WALK_EXPERIMENT` is enabled and we end up on a non-walkable situation, the code performs a recovery move:

- sets `STF_NORMALIZE_RESPONSE`
- computes a “Recover” distance based on:
  - how far we went up from the start
  - plus the absolute vertical intent in the original direction
- builds `RecoverPoint = -upDirection * Recover`
- computes an MD (minimum distance) value:
  - `MD = (Recover < min_dist) ? Recover/maxIter : min_dist`

Then calls:

```cpp
doSweepTest(..., volume, RecoverPoint, SideVector,
            maxIter, &NbCollisions, MD,
            filters, SWEEP_PASS_UP /*compat*/, touchedActor, touchedShape, contextID);
```

(There’s an inline comment noting this is technically a **down** move even though it tags the pass as `UP` for legacy compatibility.)

Finally it clears `STF_NORMALIZE_RESPONSE` and returns the collision flags.

---

## Practical takeaways (for your WoW movement replication)

If you’re replicating “vanilla WoW-ish” grounded/jump movement, this function gives a clean template:

1. **Split movement** into vertical + planar components relative to an up axis.
2. **Auto-step** only when:
   - planar intent exists (player input)
   - *not* jumping upward under your own power
3. Run **(up → side → down)** sweeps, with:
   - slide on side hits
   - snap-to-ground on down hits
4. Handle **slope constraints** *after* down pass using contact normals + “contact height”.
5. Optionally add a **probe/sensor sweep** to catch “edge cases” in constrained climbing.

---

## Cross-references in the same file (worth reading next)

- `SweepTest::doSweepTest(...)` – where the actual sweep loop, hit processing, and response computation happens.
- `SweepTest::updateTouchedGeoms(...)` – broadphase collection for candidate shapes.
- `testSlope(...)` – slope-limit predicate used by both side and down validation paths.
