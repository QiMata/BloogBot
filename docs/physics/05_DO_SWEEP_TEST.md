<!-- File: 05_doSweepTest.md -->

# doSweepTest — Sweep Loop and Collision Processing

## Purpose

`doSweepTest` is an internal helper that moves the controller’s swept volume through the world for a single movement **phase** (e.g., up / side / sensor / down). It performs an **iterative sweep-and-slide loop**:

- sweep the controller shape along a direction
- if it hits, move to impact and compute a slide response
- repeat with the remaining displacement until exhausted, too small, or iteration-limited

It is invoked multiple times per `moveCharacter` call with different directions and iteration caps.

---

## High-Level Behavior

In simplified terms, `doSweepTest` does:

1. **Early-out for tiny motion**
   - If the requested move vector length is below `minDist`, do nothing and return (avoids jitter from microscopic moves).

2. **Sweep along direction**
   - Perform a convex sweep of the controller’s geometry along `direction`.
   - PhysX typically uses scene queries or cached touched geometry updated earlier (e.g., `updateTouchedGeoms`) to find the closest blocking hit.

3. **No hit → move full distance**
   - If no collision is detected:
     - Move the controller the full requested distance (advance `volume.mCenter` by `direction`).
     - Return `false` (no collision occurred).

4. **Hit → move to impact and compute slide**
   - If a collision is found at some fraction of the sweep:
     - Move to the impact position (often leaving a small gap such as “skin/contact offset”).
     - Compute the **remaining displacement** (untraveled portion of the original request).
     - Use the surface **hit normal** to compute the slide response via `collisionResponse(...)`.

5. **Iterate**
   - Update the direction to the new slide direction and repeat:
     - stop when remaining motion is ≤ `minDist`
     - or `maxIter` reached
     - or slide result becomes too small (stuck)

6. **Return results**
   - Return `true` if any collision occurred during the process.
   - Output `NbCollisions` (or similar counter) for downstream state/flags.

---

## Sweep Loop: Control Flow and Stop Conditions

### Loop stop conditions (typical)
- Remaining displacement length `<= minDist`
- Iteration count `>= maxIter`
- The computed slide displacement is essentially zero (e.g., you are wedged into a corner)

### What “minDist” prevents
Even when the controller *should* “technically” move by a very tiny amount, floating-point error can cause oscillation:
- repeated micro-sweeps
- repeated hit events
- jitter on slopes or near edges

A non-zero `minDist` clamps that behavior by treating sub-threshold motion as “no move”.

---

## Collision Handling: Slide vs Stop

### Default behavior
By default, PhysX CCT tends to **slide** on most collisions (walls, slopes, obstacles), unless modified by user behavior callbacks or filtering.

### Behavior callbacks (conceptually)
PhysX exposes callback hooks (e.g., `PxControllerBehaviorCallback`) that can influence response:
- whether to treat a surface as “non-slidable”
- friction-like behavior changes
- special cases such as one-way platforms (engine-dependent)

In practice for CCT: **bump is usually 0**, **friction usually 1** for “slide”.

---

## `collisionResponse`: How the Slide Vector Is Produced

The core logic (conceptually) decomposes motion into:
- **normal component** (into/out of the surface)
- **tangent component** (parallel to the surface)

Then constructs a new target using:
- `bump * normalComponent * amplitude`
- `friction * tangentComponent * amplitude`

Typical tuning in CCT:
- `bump = 0` (no bouncing)
- `friction = 1` (full tangent slide)

### Normalization flag
A flag like `STF_NORMALIZE_RESPONSE` may alter the response to avoid “creeping loss” of magnitude around acute angles:
- when enabled, normal/tangent vectors may be normalized before scaling
- used to reduce corner-sticking or drift artifacts

---

## Initial Overlap (Distance == 0) and Overlap Recovery

If the sweep begins in overlap, the sweep distance may be zero:

- `C.distance == 0` indicates “starting contact / overlap”
- Some older approaches attempted to compute MTD (minimum translation distance) inline in `doSweepTest`
- In newer flow, PhysX tends to do overlap recovery **outside** `doSweepTest` (e.g., dedicated overlap recovery / penetration resolution pass)

So `doSweepTest` is generally focused on **sweep-and-slide**, not heavy overlap resolution.

---

## Flags and Controller State Side-Effects

During sweeps, the implementation typically records additional metadata:
- touching another controller: `STF_TOUCH_OTHER_CCT`
- touching an obstacle: `STF_TOUCH_OBSTACLE`
- candidate ground triangle / slope validation flags, etc.

Downstream, `moveCharacter` may use:
- `NbCollisions > 0` to set collision flags like `eCOLLISION_DOWN`, `eCOLLISION_SIDES`, `eCOLLISION_UP`
- touched actor/shape references for “standing on” state queries

---

## Return Value and `NbCollisions` Semantics (Inference)

Based on typical usage patterns:

- `returnValue` is likely `true` iff **a collision occurred**
- `outNbCollisions` reports the number of collisions handled in the loop

> Note: Some call sites may treat the return value as “did any work happen” (e.g., not early-out), but naming and common patterns suggest “collided”.

---

## Pseudocode (Approximate)

> This is not line-exact PhysX code. It captures the iterative sweep-and-slide structure and common control flow.

```cpp
bool SweepTest::doSweepTest(
    SweptVolume& volume,
    PxVec3 direction,
    PxU32 maxIter,
    float minDist,
    PxU32* outNbCollisions)
{
    *outNbCollisions = 0;

    float remaining = direction.magnitude();
    if (remaining <= minDist)
        return false; // trivial move

    PxVec3 currentDir = direction / remaining;
    PxExtendedVec3 currentPos = volume.mCenter;

    bool collided = false;
    PxU32 iter = 0;

    while (iter < maxIter && remaining > minDist)
    {
        SweptContact C;
        bool hit = CollideGeoms(
            /*context*/ this,
            /*volume*/ volume,
            /*geomStream*/ mGeomStream,
            /*pos*/ currentPos,
            /*dir*/ currentDir,
            /*out*/ C,
            /*sweep*/ true,
            /*allowInitialOverlap?*/ !mUserParams.mOverlapRecovery);

        if (!hit)
        {
            // Move full remaining distance
            currentPos += toExtendedVec3(currentDir * remaining);
            remaining = 0.0f;
            break;
        }

        collided = true;
        (*outNbCollisions)++;

        // Move to impact point (possibly minus skin/contact offset)
        float travel = C.distance;
        currentPos += toExtendedVec3(currentDir * travel);
        remaining -= travel;

        // If starting overlap and overlap recovery is enabled, resolution is usually handled elsewhere
        if (mUserParams.mOverlapRecovery && C.distance == 0.0f)
        {
            // Typically: do not resolve here; rely on overlap recovery pass
        }

        PxVec3 hitNormal = C.normal;

        // Slide response (typical CCT tuning: bump=0, friction=1)
        float bump = 0.0f;
        float friction = 1.0f;
        bool normalize = (mFlags & STF_NORMALIZE_RESPONSE) != 0;

        PxExtendedVec3 targetPos = currentPos;
        collisionResponse(
            /*inout*/ targetPos,
            /*current*/ currentPos,
            /*dir*/ currentDir,
            /*normal*/ hitNormal,
            /*bump*/ bump,
            /*friction*/ friction,
            /*normalize*/ normalize);

        PxVec3 newDelta = toVec3(targetPos - currentPos);
        float newLen = newDelta.magnitude();

        if (newLen <= 1e-5f)
            break; // stuck

        currentDir = newDelta / newLen;
        remaining = newLen;

        iter++;
    }

    volume.mCenter = currentPos;
    return collided;
}
```

---

## Example Walkthroughs

### Walking into a wall at 45°
- Side sweep hits the wall mid-way
- Move to contact point
- Slide response projects remaining motion onto wall tangent
- Next iteration sweeps along the wall and completes remaining distance

### Stepping off a ledge
- Side sweep may be clear
- Sensor/down logic detects ground below
- Down sweep either lands or continues falling depending on step/slope rules and thresholds (`minDist` can affect whether a tiny down move is skipped)

### Sliding on a steep slope (force slide)
- Down sweep immediately contacts slope
- Slide response converts downward motion into downhill tangent motion
- Multiple iterations can advance the character along the slope in a single frame, bounded by `maxIter`

---

## Porting Notes / Replication Checklist

- **Sweep shape** must match CCT geometry precisely (capsule/box, offsets, contact/skin distances).
- Preserve the **phase order**: sweep → move to hit → slide response → repeat.
- Use a **minDist** threshold to prevent jitter and micro-iteration churn.
- Use a **hard iteration cap** (e.g., 10) to prevent pathological infinite loops.
- Ensure corner cases behave correctly:
  - head-on into wall → stop
  - grazing contact → smooth slide
  - U-shaped corner → doesn’t explode or jitter; may stop if fully wedged

---

## Open Items (Needs Exact Source Alignment)

These details must be confirmed against the exact PhysX implementation you are mirroring:

- exact definition and usage of `minDist`
- exact rules for applying “skin/contact offset” at impact
- precise semantics of the function’s return value vs `NbCollisions`
- when and why `STF_NORMALIZE_RESPONSE` is set
- exact treatment of initial overlap (`distance == 0`) within this function vs external recovery

---
