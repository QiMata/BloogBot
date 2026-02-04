# 07_overlapRecovery_computeMTD.md — Overlap Recovery and Penetration Resolution

## Title
**Overlap Recovery – Computing Minimum Translation Distance (MTD) for Penetrations**

---

## The Problem

When the character starts a frame **overlapping** geometry (e.g., standing on moving terrain that moved up into the controller, or being shoved into a wall by a moving platform), PhysX’s CCT applies an **overlap recovery** to push the character out of interpenetration. Without this, the controller could get stuck or jitter inside objects.

---

## Detection of Overlap

PhysX likely detects overlaps in two places:

- **Initial position overlap (pre-move)**: Before moving, they check if the controller’s shape currently intersects any static geometry. This is done in `findTouchedObstacles` (raycast foot) and/or `updateTouchedGeoms` by creating a “temporal box” including the current position. They might perform an overlap test for the controller at its current position against nearby shapes.

- **Post-move overlap (post-sweep)**: After sweeps, if the final position still overlaps something (maybe due to finite skin or multi-contacts), they double-check.

### From the code (hint)

This shows overlap-recovery logic gated on “contact at zero distance” (initial overlap):

~~~cpp
if(mUserParams.mOverlapRecovery && C.mDistance==0.0f)
{
    /* SweptContact C;
       C.mDistance = 10.0f;
       ... */
}
~~~

This suggests: if `overlapRecovery` is enabled and the sweep found a contact at **zero distance** (meaning initial overlap), they had code (commented out) to ensure some non-zero distance. They likely replaced this with a loop using `PxGeometryQuery::computePenetration` (MTD computation) to find how to push out.

---

## Computing MTD

**MTD (Minimum Translation Distance)**: a vector that, when applied to one shape, separates overlapping shapes by the smallest possible distance.

PhysX provides:

- `PxGeometryQuery::computePenetration(outVector, outDepth, geomA, poseA, geomB, poseB)`
  - returns a **direction** (`outVector`, typically unit length) and a **penetration depth** (`outDepth`) that resolves the overlap.

### In PhysX CCT (expected usage)

When an overlap is detected, they call:

- `computePenetration(mtd, depth, capsuleGeom, volumePose, touchedGeom, globalPose)`

This gives:
- `mtd`: direction to move the controller volume to resolve overlap
- `depth`: how far along that direction to move

Then they likely move the controller out by:

- `delta = mtd * depth`

This is done iteratively if multiple shapes overlap or if one MTD move causes another overlap.

We see hints:

~~~cpp
isValid = PxGeometryQuery::computePenetration(mtd, depth, capsuleGeom, volumePose, touchedGeom, globalPose);
...
if(isValid)
    ...
~~~

---

## Likely Recovery Algorithm (Iterative)

For each overlapping object (either looping through nearby geoms `mGeomStream` or picking the “best” candidate), do:

- Compute MTD vector + depth
- Apply `pos += mtd * depth`
- Re-check overlaps
- Repeat until no overlap or iteration cap reached

A plausible structure:

- `maxIterations = 4 or 10` (small number to avoid infinite loops)

~~~text
for i in range(maxIterations):
    find any overlap with shape j (maybe the largest depth or first)
    if none, break
    computePenetration(mtd, depth, controllerGeom, controllerPose, shapeGeom, shapePose)
    move controller center by mtd * depth
    totalRecovery += mtd * depth
~~~

They may:
- resolve overlaps sequentially, or
- pick the deepest overlap each pass to address worst-first

---

## About `mOverlapRecover` (Why It Exists)

There are two common patterns:

1) **Immediate correction**  
   Resolve all penetration now by directly offsetting the controller pose.

2) **Pipeline-integrated correction (using the normal sweep)**  
   Store the needed correction as a “recovery displacement” and apply it via the regular `move()` sweep path.

### Hypothesis consistent with CCT pipeline

- `Controller::move` adds `mOverlapRecover` from last frame to this frame’s displacement, then clears it.
- Therefore, PhysX may:
  - attempt overlap resolution this frame (bounded),
  - if **not fully resolved** (iteration limit / clamping), store the **remaining** correction in `mOverlapRecover`,
  - apply that remainder next frame (through the standard sweep path so collisions are respected).

This avoids a large “teleport” pop-out and ensures the recovery displacement goes through the same collision constraints.

---

## Confusing Snippet / Unclear Behavior

This snippet appears in an overlap loop (context unclear):

~~~cpp
const float MD = Recover < min_dist ? Recover/float(maxIter) : min_dist;
...
RecoverPoint = -upDirection * Recover;
~~~

Possible interpretations:
- **Capping** or **portioning** recovery to avoid overshoot
- Handling **up-direction-only** recovery (e.g., “sinking into ground”)
- Some scheme to manage `min_dist` interactions

However, as written, `Recover < min_dist` causing an even smaller step (`Recover/maxIter`) is counterintuitive for resolving tiny penetrations quickly—so this may be:
- from an older path,
- mis-copied without surrounding logic,
- or doing something different than “final applied step size”.

Treat this as **needs full context / confirmation**.

---

## Safe Replication Strategy (Actionable)

Even without perfect clarity, a robust, parity-friendly approach is:

1) **Detect overlap** at current pose (broadphase → candidate shapes).
2) For up to `N` iterations:
   - Choose an overlapping shape candidate.
   - Call `computePenetration`.
   - If valid: apply `pos += mtd * depth` (optionally with a tiny bias for “skin”).
   - Re-test overlaps.
3) If still overlapping after `N` iterations:
   - store remaining correction in `mOverlapRecover` (to apply next frame), **or**
   - accept partial resolution (avoid infinite loops).
4) Proceed with normal sweep-based movement.

Notes:
- Prefer **small N** (3–5) to avoid pathological corner cases.
- If your engine has `minDist` behavior that can skip tiny moves, you can either:
  - bypass `minDist` for overlap recovery, **or**
  - allow multi-frame recovery accumulation (store remainder and apply via `mOverlapRecover`).

---

## Why Multi-Frame Recovery Might Exist (Reasonable Hypothesis)

Large penetrations can occur if:
- a moving platform shifts a lot in one frame,
- the character teleports into geometry,
- streaming / snapping causes overlaps.

Instantly popping out can feel like teleporting, may skip collision events, or interact badly with ceiling/floor logic.

Storing a remainder in `mOverlapRecover` and resolving it through the next frame’s sweep keeps movement constrained and reduces one-frame discontinuities.

---

## Edge Cases to Test for Parity

- **Spawn / teleport into ground**: controller should exit without jitter.
- **Platform lifts character into low ceiling**:
  - character should not remain embedded
  - behavior should match whether PhysX pushes down/off or flags ceiling contact
- **Convex corners / wedges**:
  - sequential MTD resolves can fight each other
  - iteration cap prevents infinite loops
- **Multiple overlaps**:
  - ensure deterministic order (or pick deepest-first)

---

## Instrumentation / Debugging Suggestions

When overlap recovery triggers, log:
- number of overlapping candidates
- chosen candidate identity/type
- `mtd`, `depth`, and applied delta
- iteration count
- remaining overlap after the loop
- whether you stored remainder in `mOverlapRecover`

Build a simple parity test:
- Start pose overlapped by a known distance
- Compare frame-by-frame convergence and final stable pose

---

## Search Terms (for source confirmation later)

- PhysX CCT penetration
- `computePenetration` character controller overlap recovery
- `mOverlapRecovery` PhysX CCT
- `mOverlapRecover` Controller::move
- “minimum translation distance” PhysX

---

## Porting Notes

- Use your physics engine’s penetration/MTD query if available.
- Avoid “double resolving” (only do recovery if penetration exists).
- If your engine lacks MTD:
  - fallback to an iterative “push out” using sampled directions and overlap tests (higher complexity).

Suggested iteration counts:
- `3–5` for most scenes; higher if you expect dense geometry overlaps.

Directional bias:
- MTD is typically the “least translation” direction (can be diagonal in corners), which is usually acceptable.
- If you need more PhysX-like behavior, you may later need to bias along `upDirection` in some cases—*but only after confirming from code.*

---

## Conclusion

Overlaps are resolved by:
- computing MTD (direction + depth) using `computePenetration`,
- applying the correction iteratively (bounded),
- optionally storing a remainder in `mOverlapRecover` to be applied in subsequent frames via the normal sweep pipeline.

This is one of the trickiest parts of the CCT—treat it as a first-class subsystem with dedicated logs and parity tests.
