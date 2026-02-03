# Per-Frame Controller Move Pipeline (PhysX CCT 5.6.1)

## Introduction

When you call the controller’s `move()` for a frame, PhysX executes a well-defined sequence of steps to compute the character’s new position. This document provides a numbered step-by-step guide to the per-frame movement pipeline, from start to finish. Following this sequence is key to replicating the exact behavior.

## Step-by-Step Move Algorithm

### 1. Begin Move – Setup and Lock (if needed)

- **Locking:** If the controller manager uses locking, acquire a write lock before modifying shared data. This prevents concurrent modifications (e.g., multiple controllers moving in parallel or other threads accessing obstacles).
- **Delta Time Update:** Increase the controller’s internal clock by the elapsed time for this move. PhysX accumulates time in `mGlobalTime` and uses it for computing platform velocities.
- **Profile Zone (optional):** PhysX inserts a profiling marker (`PX_PROFILE_ZONE`) at the start of `moveCharacter` for performance measurement. (Not needed for functionality, but you can include similar markers in your engine for debugging or profiling.)

### 2. Initialize Frame Parameters

- **Copy User Params:** Load the controller’s current settings into the internal `SweepTest` module’s parameters (`mCctModule.mUserParams = mUserParams`). This ensures the `SweepTest` uses the latest `upDirection`, `slopeLimit`, etc. for this move. Also propagate flags like `OverlapRecovery`, `PreciseSweeps`, and `PreventVerticalSlidingAgainstCeiling` from the manager into `mUserParams`.
- **Mark First Update:** Set the `STF_FIRST_UPDATE` flag in `mCctModule.mFlags`. PhysX does this at the start of each move to indicate a new frame’s processing. (It might be used internally to differentiate initial vs. iterative calls.)
- **Reset Stats Counters:** Clear iteration counters (`nbIterations`, `nbFullUpdates`, etc.) for this move via `mCctModule.resetStats()`. These counters accumulate as sweeps are performed (for debugging/stats).

### 3. Incorporate Overlap Recovery Displacement

- **Add `mOverlapRecover` to displacement:** If the previous frame left an overlap correction vector, add it to the current frame’s requested displacement (`disp`). In code: `disp = originalDisp + mOverlapRecover`. This means a portion of this frame’s movement might be consumed by pushing the controller out of geometry.
- **Clear `mOverlapRecover`:** Immediately set `mOverlapRecover = (0,0,0)` after adding. The recovery is applied once; if still overlapping after this move, a new recovery will be computed.

### 4. Handle Standing On Object (Moving Platform Logic)

#### Update: stateless service model

Anything PhysX "caches" across frames (touched shape/actor, obstacle handle, previous timestamps, cached standing-on-moving flags, local/world touched points) must be treated as explicit data. The physics engine should not retain mutable per-character controller state between calls.

Model the move API as:

- **Request:** `Move(CharacterId, TickId, DeltaTime, Params, Pose, DesiredDisp, PrevStateToken?)`
- **Result:** `NewPose, CollisionFlags, BaseDelta/BaseVelocity, NewStateToken?`

Where `PrevStateToken`/`NewStateToken` is a small serializable blob used only to reproduce PhysX-like platform behavior and optional overlap recovery carry-over.

- **Validate Cached Ground:** If we had a cached touched shape from last frame (`mTouchedShape`), ensure it’s still valid: the shape still exists in the actor, the actor is in the same scene, and the shape still has scene query collision enabled. If any check fails, clear `mTouchedShape`/`mTouchedActor` (we’re not standing on it anymore).
- **Filter the Shape:** If the cached shape is valid, run `filterTouchedShape(filters)`. This calls the user’s filter callback to see if the standing-on shape should still be considered for collisions. If the filter returns `eNONE` (ignore), PhysX will also clear the touched shape (stop treating it as ground).
- **Raycast Down if No Ground Cached:** If after the above, `mTouchedShape` is null and `mTouchedObstacleHandle` is invalid (meaning we don’t know what’s below us), perform `findTouchedObject()`.

  - This casts a ray straight down from the controller’s bottom to probe for a standing surface. It uses a special pre-filter to ignore the controller’s own actor and any triggers.
  - If it hits a dynamic or kinematic actor within the step height, that shape/actor is set as `mTouchedShape`/`mTouchedActor`, and the contact point is recorded (both world and local positions).
  - Similarly, it checks user obstacles via `ObstacleContext::raycastSingle`. If an obstacle is closer than any shape hit, it selects that as `mTouchedObstacleHandle`.
  - This essentially initializes the “ground” for this frame before any movement. (Note: PhysX does this to know if you start the move on a moving platform or on an obstacle.)

- **Compute Platform Delta:** If there is a supporting surface (from token validation or fresh probe), compute platform delta for this tick (see `09_rideOnTouchedObject.md`) and retrieve `standingOnMoving`. This logic must use only request inputs + `PrevStateToken`:

  - Calculate how much the ground moved since last frame (delta vector).
  - If the ground moved and the user hasn’t flagged it non-rideable, update the character’s position and displacement:
    - If the ground moved upwards, directly adjust the controller’s position upward by that amount (to avoid sinking).
    - If the ground moved downwards, add that downward movement to `disp` so the controller will follow it during the down sweep.
    - Add any horizontal movement of the ground to `disp` (if the object is rideable).
  - Mark `standingOnMoving = true` if the delta was non-zero, and store `mCachedStandingOnMoving` for next frame usage.
  - Store the platform’s movement scaled by `1/elapsedTime` into `mDeltaXP` (for `PxControllerState`).
  - If the object didn’t move (or is user-flagged as not rideable via `PxControllerBehaviorFlag::eCCT_USER_DEFINED_RIDE`), then `standingOnMoving` remains whatever it was last frame (they carry over `mCachedStandingOnMoving`).

- **If no object below:** If after `findTouchedObject` we still have no ground, return `BaseDelta/BaseVelocity = 0` and return a `NewStateToken` with no `SurfaceRefId`.

### 5. Consider Other Controllers as Obstacles

> If you do not have multiple controllers interacting, you can skip this. PhysX includes this for completeness.

- **Clear Buffers:** Ensure the arrays for other controllers’ shapes (boxes, capsules, etc.) are empty at start.
- **Iterate Controllers:** For each other controller in the manager, decide if it should be an obstacle:
  - If the user provided a CCT filter callback (`PxControllerFilterCallback`), call `filter(thisController, otherController)`. If this returns `false`, skip that controller (don’t collide with it).
  - Otherwise, if it’s a box controller, retrieve its OBB (center, extents, rotation); if capsule, its capsule geometry. Store these in the lists (boxes/capsules) and also store an encoded ID as `userData` for each. (PhysX encodes the index and a marker for “it’s a CCT” in a pointer-sized value.)

- **After loop:** The `userObstacles` structure (passed into `moveCharacter`) now contains pointers to these arrays and their lengths. This instructs the sweep code to treat other controllers as static obstacles.

> PhysX uses these arrays in the sweep tests to collide with other controllers. The sweep code (in `Gu::`) will test the SweptVolume against these extra geometries as if they were world objects. Implementing this fully can be complex. A simpler approach: handle controller-vs-controller collisions at a higher level or approximate them by expanding each controller’s shape and doing overlaps. For full parity, you’d need to integrate these into your sweep queries.

### 6. Perform Collide-and-Slide Movement (Sweeps)

- **Prepare for Sweeps:** PhysX sets up an internal data structure for scene query callbacks (`findGeomData` and `userHitData`). These hold pointers to the `PxScene`, render buffer, and the hash set of CCT shapes to ignore, as well as references to the controller itself and obstacles for user callbacks. (In your engine, you might not need this indirection—these are mainly used to allow PhysX’s internal query code to call back into CCT-specific logic like registering new touched triangles.)
- **Reset Walk Experiment Flag:** Clear `STF_WALK_EXPERIMENT` at the start of the move sweep. (This flag is used to indicate a second-pass “slide down” attempt on non-walkable slopes; we ensure it’s off for the first attempt.)
- **Call `moveCharacter`:** Invoke `SweepTest::moveCharacter(...)` with all the prepared data. This will execute the up-to-3 pass sweep algorithm internally. On return, you get:
  - `collisionFlags`: the `PxControllerCollisionFlags` for this move (sides/up/down).
  - Potentially, `mCctModule.mFlags` may have `STF_HIT_NON_WALKABLE` set if a steep slope was encountered that the controller couldn’t climb.
  - `touchedShape` and `touchedActor` outputs (passed by reference) which point to any shape that was last impacted by a downwards sweep (ground contact) during this move. PhysX captures these so it can update `mTouchedShape`/`Actor` after the sweeps.

- **Handle Steep Slope (“Walk Experiment”):** Immediately after the first `moveCharacter` call, PhysX checks if `STF_HIT_NON_WALKABLE` was set. If so, it means the character’s forward motion hit a slope too steep to climb (non-walkable surface). The pipeline then does:

  - Set the `STF_WALK_EXPERIMENT` flag to true. This signals the internal code that we’re in the second-chance slide phase.
  - Restore the character’s position to the backup from before the sweeps (`volume.mCenter = Backup`). Essentially undo the first move attempt’s displacement.
  - Compute an adjusted displacement `xpDisp` which removes upward component:

    - If `nonWalkableMode == ePREVENT_CLIMBING_AND_FORCE_SLIDING`, remove the full component in the up direction (so the character will neither move up nor stay at the same height, but purely horizontal). PhysX does `decomposeVector(xpDisp, tangent, disp, upDirection)` and uses `xpDisp` (the part of `disp` aligned with `upDirection`) for the new `disp` in this case. Actually, here `xpDisp` ends up being the original vertical component; likely a slight bug in variable naming: after decompose, `xpDisp` holds the vertical component and `tangent_compo` the horizontal. They then probably intended to use the horizontal part – but given the code, it sets `xpDisp` to the vertical part if forcing slide, otherwise `xpDisp = disp`. (This detail is subtle; the intent is to eliminate upward movement.)
    - If `nonWalkableMode == ePREVENT_CLIMBING` (no forced slide), they simply reuse the same `disp` (no change). So in this mode the second attempt is with the original full displacement, just now marked as walk experiment (which affects how the down sweep is done – only one iteration, meaning no sliding down).

  - Call `mCctModule.moveCharacter` again with the `xpDisp` vector. This second call will effectively slide the character along the slope or stop it. Because `STF_WALK_EXPERIMENT` is true, inside `moveCharacter` the max iterations for the down phase are adjusted (if forcing slide, multiple iterations are allowed to slide fully down).
  - Clear `STF_WALK_EXPERIMENT` afterward so it doesn’t leak to next frame. The `collisionFlags` from this second run become the final collision flags (PhysX overwrites the previous `collisionFlags` with the second call’s result).

> Note: During the above sweeps, the internal state like `mContactNormalDownPass` and `mContactNormalSidePass` (the last hit normals) and `mFlags` (`STF_*` flags like `WALK_EXPERIMENT`, `HIT_NON_WALKABLE`, etc.) are updated inside `moveCharacter`/`doSweepTest`. These influence branch decisions (like whether to do the sensor pass or whether to set collision flags).

### 7. Finalize Position and State

- **Update touched shape/actor:** PhysX writes back the shape and actor pointers from the (possibly second) `moveCharacter` call into `mCctModule.mTouchedShape` and `mCctModule.mTouchedActor`. This means the ground contact at the end of the move is now cached for next frame. If no ground was hit (character in air), these become `NULL` (or obstacle handle invalid).
- **Store Collision Flags:** Save the returned `collisionFlags` into the controller’s member (`mCollisionFlags`) so that `PxController::getCollisionFlags()` can report them until the next move.
- **Store New Position:** Write the updated position back to the controller’s state. In code: `mPosition = volume.mCenter`. `volume.mCenter` is the `ExtendedVec3` tracked through the sweeps, now containing the final position after all movements and slides.
- **Update Kinematic Actor Pose:** If the controller uses an underlying physics proxy (`mKineActor`), update its transform:

  - Compute the delta move vector `delta = Backup - volume.mCenter` (original position minus new position). Actually PhysX does `const PxVec3 delta = diff(Backup, volume.mCenter)`, which yields (oldPos -> newPos) vector. If this `delta` is non-zero (meaning the controller moved), then:
    - Set the kinematic actor’s target pose position to the new controller position, and orientation (`targetPose.q`) to `mUserParams.mQuatFromUp`. They essentially keep the actor aligned with the “up” direction and centered on the controller. Then call `mKineActor->setKinematicTarget(targetPose)` to schedule the move.
  - If `delta` was zero (controller didn’t move), they do nothing (to avoid waking up a sleeping kinematic or causing redundant updates).

- **Reset Temporary Buffers:** Clear out the arrays of other controllers that were filled earlier by `mManager->resetObstaclesBuffers()`. This frees memory or prepares for the next usage.
- **Unlock (if locked):** If a write lock was taken, release it now.

### 8. Return Collision Flags

The method returns the `PxControllerCollisionFlags` bitfield to the caller, indicating sides/up/down collisions this frame. The calling code (usually the game logic) can react to these (e.g., if `(flags & eCOLLISION_DOWN) == 0`, the character is in air; if `(flags & eCOLLISION_UP)`, a head bump occurred, etc.).

## Diagram of Main Pipeline

```
Initialize frame -> Apply overlap recovery -> Check ground contact (raycast) ->
Adjust for moving platform -> Gather obstacles -> Perform sweeps (up, side, down) ->
If steep slope hit, adjust and sweep again -> Update position and state -> Return flags
```

This pipeline should be executed every frame (or tick) you update the character. Note that within a single `move()` call, multiple internal sweeps and adjustments happen, but from the game’s perspective it’s one discrete move.

## Additional Considerations

### No Movement Case

If the input displacement is zero (or extremely small), PhysX still goes through some of the motions (like finding touched object via raycast) but will short-circuit the sweeps. In `doSweepTest`, the first check compares move length to `minDist`, and if `<= minDist`, it breaks out (no movement). So effectively, the controller stays in place but still updates its `mTouchedShape` if needed.

### Jumping

PhysX CCT doesn’t have an explicit “jump” function; you simulate a jump by supplying an upward displacement in `move()`. The pipeline handles it: `UpVector` will carry the entire displacement (since lateral is zero), the down pass will likely find no ground so no ground collision flag, and if you hit a ceiling, `eCOLLISION_UP` will be set. To ensure a jump doesn’t immediately get canceled, PhysX includes logic to ignore small downward “bumps” at start of jumps (see how `stepOffset` is canceled when moving up and not on a moving object, and how `preventVerticalSlidingAgainstCeiling` can be toggled off to allow upward motion). When replicating, be mindful of these so a jump isn’t erroneously clamped.

### Floating Point Precision

PhysX uses `PxExtended` for positions (64-bit) to avoid precision loss in large worlds. If your engine uses double for world coordinates, you’re fine. If using float (32-bit), be cautious moving the character in extremely large coordinates as you might see jitter. For parity in a typical game scale, floats are acceptable.

## Summary

By following these steps 1:1, you ensure that each component of the movement update is handled. Use this as a checklist in your implementation:

- Applied last frame’s depenetration offset?
- Updated what’s under the character (ground or obstacle)?
- Accounted for moving platforms (with immediate vertical adjust and added horizontal motion)?
- Did the up, side, down sweeps in order?
- If slope too steep, tried second pass without upward movement?
- Updated the final position, and synced any physics proxy?
- Stored collision results and state for next frame?

Each of these corresponds to a section in the above pipeline. If all are checked, your per-frame logic should mirror PhysX’s.
