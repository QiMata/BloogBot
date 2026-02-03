# Character Controller Move Call Graph (PhysX CCT 5.6.1)

## Purpose and Overview

This document outlines the call sequence of PhysX’s Character Controller when you invoke a movement update (`PxController::move`). Starting from the public API and drilling down to internal functions, it shows how control flows through the system, including the creation of internal helper objects, collision detection sweeps, and response processing. The goal is to provide a clear “map” of function calls so you can mirror this structure in your engine.

## High-Level Call Chain

### User calls `PxController::move(...)`
The entry point for moving a controller (capsule or box). In PhysX, this is a virtual method implemented by specific controller types (e.g., `PxCapsuleController`).

### `PxCapsuleController::move` -> `Controller::move`
The capsule controller’s move constructs a geometry-specific `SweptVolume` (capsule) with current position and dimensions, then calls the shared `Controller::move` implementation. For example:

- `BoxController::move` creates a `SweptBox` with extents and calls `Controller::move(...)`.
- `CapsuleController::move` creates a `SweptCapsule` (with radius, height, etc.) and calls `Controller::move(...)`.

### `Controller::move` (shared logic)
This is the core routine that orchestrates a move using PhysX’s internal module (`mCctModule` of type `SweepTest`). Key steps in `Controller::move` include:

- Locking scene for write (if configured).
- Advancing internal time and initializing common parameters (render debug, user params copy, etc.).
- Incorporating any stored overlap recovery adjustment into this frame’s displacement (adding `mOverlapRecover` to the requested move vector).
- Updating and filtering the touched object state (floor or obstacle under the controller). If a previously cached shape is no longer valid (e.g. destroyed or filtered out), it’s cleared. If no object is cached, it attempts to find one under the character via downward raycast (`findTouchedObject`).
- If a touched object or obstacle is detected, it invokes `rideOnTouchedObject` to adjust for a moving base (e.g., moving platform). This may modify the displacement vector and track platform motion.
- It then collects other controllers as potential obstacles (populating arrays of other controller shapes if needed).
- Finally, it calls the sweep-based movement: `mCctModule.moveCharacter(...)` to perform collision sweeps and sliding response. This function returns collision flags (sides/up/down) and possibly sets an internal flag for steep slope handling (non-walkable surface).
- If a steep slope was hit, `Controller::move` engages a “walk experiment” retry: it adjusts the displacement to remove upward motion and calls `moveCharacter` again (see Slope Handling in later docs).
- It then updates internal state: caches any newly touched shape/actor, stores the collision flags in `mCollisionFlags`, and updates the controller’s stored position (`mPosition`) to the new position (`volume.mCenter`).
- If a kinematic proxy actor exists, it sets its target pose to match the new position (so the physics actor moves with the controller).
- Resets temporary obstacle buffers and unlocks the scene if needed.
- Returns the `PxControllerCollisionFlags` to the caller.

### `SweepTest::moveCharacter`
This is the internal movement simulation that performs the multi-step collide-and-slide motion. It decomposes the requested motion into up/side/down components and iteratively sweeps the controller’s shape through the world. On each sub-sweep, it handles collision response (sliding along surfaces) and sets flags for side, up, or down collisions.

The `moveCharacter` function uses helper `doSweepTest` calls for each phase (up, side, down) and may engage a “sensor” pass to ensure the character properly stands on ground. Details are in `04_SweepTest_moveCharacter.md`.

### `SweepTest::doSweepTest`
This helper performs an actual convex sweep of the character shape in a given direction and processes the first hit (if any). It moves the shape, reduces the remaining displacement, and computes how the motion should be reflected. It may call `collisionResponse` to slide along surfaces.

If the sweep starts in an overlap, it uses a penetration recovery algorithm (see `07_overlapRecovery_computeMTD.md`). It loops up to a max iteration count to resolve multiple contacts in one pass (e.g., glancing off multiple surfaces) until the requested move is exhausted or blocked. See `05_doSweepTest.md` for step-by-step logic.

### `collisionResponse` (static utility)
This function computes the slide direction after a collision. Given the current travel direction and a hit normal, it reflects the motion vector about the normal, then decomposes it into components parallel and perpendicular to the normal. It then applies “bump” (normal component) and “friction” (tangent component) coefficients to compute a new target position along the surface. PhysX typically uses bump = 0 and friction = 1 for horizontal surfaces (full slide). See `06_collisionResponse.md` for math details.

### Overlap Recovery (penetration resolution)
If the character shape is initially overlapping geometry (e.g., stuck or pushed into a wall), PhysX computes a minimal translation to resolve the overlap using `PxGeometryQuery::computePenetration`. It will perform up to a few iterations to resolve interpenetrations, potentially splitting the resolution across frames using `mOverlapRecover`. The logic ensures the CCT is not stuck in geometry over time. See `07_overlapRecovery_computeMTD.md` for details.

### Moving Platform Adjustment (`rideOnTouchedObject`)
If the CCT is standing on a moving base (kinematic or dynamic), PhysX adjusts the character’s motion so that it “rides” the object. The vertical component of the platform’s movement is applied immediately to avoid penetrations or gaps, and the horizontal component is added to the character’s desired motion. A flag `standingOnMoving` is set to indicate the controller is on a moving object. This mechanism also updates `mDeltaXP` (the motion of the platform) for external use. See `09_rideOnTouchedObject.md` for a breakdown.

## Visual Call Graph

Below is a simplified call graph illustrating the flow when `PxCapsuleController::move` is invoked:

```
PxCapsuleController::move(disp, minDist, elapsedTime, filters)
 └── CapsuleController::move -> Controller::move(SweptCapsule, disp, ...)
      ├── findTouchedObject(...)
      ├── rideOnTouchedObject(volume, upDir, disp, obstacles)
      ├── (collect other CCT obstacles)
      ├── mCctModule.moveCharacter(..., disp, ...)
      │    ├── decompose displacement (Up/Side/Down)
      │    ├── doSweepTest(... UpVector ...) - upward step sweep
      │    ├── doSweepTest(... SideVector ...) - horizontal sweep
      │    ├── doSweepTest(... DownVector ...) - downward sweep
      │    └── set collision flags (sides/up/down)
      └── if (mCctModule.mFlags & STF_HIT_NON_WALKABLE) then:
           ├── set STF_WALK_EXPERIMENT
           ├── adjust disp (remove upward component if forcing slide)
           └── mCctModule.moveCharacter(..., xpDisp, ...) again
      ├── mPosition = volume.mCenter (new position)
      ├── mCollisionFlags = collisionFlags (sides/up/down)
      ├── if (mKineActor) setKinematicTarget(newPose)
      └── return collisionFlags
```

(The above graph omits some details for brevity, such as internal iteration loops and exact parameter lists. Line references indicate where key actions occur in the PhysX 5.6.1 source.)

## Notable Branches and Modules

### Controller vs. SweptVolume
The `Controller` class (in `CctController.h`) holds high-level state and delegates to `SweepTest` (aka `mCctModule`) for the heavy lifting of sweeps. Each controller has one `SweepTest` module instance that performs the shape sweeps and caches relevant info (touched geometry, normals, etc.).

### Capsule vs. Box differences
The logic is largely unified. The main divergence is in how the `SweptVolume` is created (capsules use radius+height, boxes use extents) and whether the “constrained climbing mode” is enabled for capsules. Constrained mode alters slope behavior (preventing the capsule from climbing steep slopes by capping horizontal movement – see `08_Slope_Step_Ceiling_Rules.md`).

### Filter callbacks
The CCT respects filtering. `filters.mFilterCallback` (`PxQueryFilterCallback`) and `filters.mCCTFilterCallback` can exclude certain shapes or controllers. In `findTouchedObject`, a custom pre-filter is used to ignore the CCT’s own shape and triggers, then user filters are applied. Similarly, when considering other controllers as obstacles, `mCCTFilterCallback->filter()` can decide which controllers to treat as colliders.

### Locking
If the controller manager is configured for thread safety (`mLockingEnabled`), a write lock is taken at the start of `Controller::move` and released at the end. This ensures thread-safe updates to shared data like obstacle lists and scene queries.

## Takeaway

Use this call graph as a blueprint in your engine. Ensure that your own CCT update function follows a similar order: update base motion, handle overlaps, then perform the move in phases (up, lateral, down), and finally update the position and collision flags. Each stage is detailed in subsequent documents with exact computations and conditions.
