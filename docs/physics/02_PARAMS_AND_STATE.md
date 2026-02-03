# Character Controller Parameters and Persistent State (PhysX 5.6.1)

## Overview

PhysX’s Character Controller (CCT) uses a combination of user-defined parameters and internally maintained state to control movement behavior. This section enumerates all relevant parameters (both tunable and internal) and explains their roles. Preserving these exactly will ensure your reproduction matches PhysX.

## Key Parameters (PxControllerDesc / CCTParams)

PhysX defines a struct `CCTParams` internally to hold many of these values. They originate from the controller’s descriptor (`PxControllerDesc`) or manager settings. Important parameters include:

- **Up Direction (`upDirection` / `mQuatFromUp`)**: The unit vector defining “up” for the controller. All gravity, step, and slope logic is relative to this. PhysX supports arbitrary up vectors (e.g., for characters on inclined planes or walls). `mQuatFromUp` is a rotation aligning the world up (Y-axis) to the specified up direction, used internally for certain transformations.

- **Slope Limit (`slopeLimit`)**: The cosine of the maximum walkable slope angle. A surface whose normal makes an angle greater than this limit with the up vector is “non-walkable.” For example, with `slopeLimit = cos(45°) ˜ 0.707`, any surface steeper than 45° is considered too steep. PhysX uses this in `testSlope(normal, upDirection, mSlopeLimit)` checks to flag collisions as non-walkable. Non-walkable hits can trigger special handling (prevent upward movement or force sliding – see `08_Slope_Step_Ceiling_Rules.md`).

- **Step Offset (`stepOffset`)**: The maximum height the controller can climb in a single step. If the controller’s forward movement would hit an obstacle lower than this height, PhysX will attempt a step up and over it. Internally, `stepOffset` is added to the Up vector motion when a horizontal move is present, and the upward sweep tests if the controller can be elevated by this amount onto the obstacle. If successful, the remaining forward motion proceeds at the elevated position. Step offset is canceled in certain cases (e.g., if the controller is moving up due to a jump or if no lateral motion exists).

- **Contact Offset (`contactOffset`)**: A “skin width” around the controller used to keep it slightly separated from obstacles. Typically a small fraction of the controller radius/extent. This prevents frequent deep penetrations and jitter by starting collision resolution early. In PhysX, the contact offset is passed into sweep tests (via `PxHitFlag::eASSUME_NO_INITIAL_OVERLAP` usage and inflation of the sweep shape). While not explicitly shown above, it influences sweeps (e.g., the controller’s shape is expanded by `contactOffset` for collision queries). When replicating, you should subtract the skin width from desired motion to avoid penetrating surfaces.

- **Min Move Distance (`minDist` in `PxController::move`)**: A threshold to ignore very small motions. If the requested displacement’s length is below this, the controller will not actually move. This helps avoid tiny oscillations due to precision errors. PhysX checks the length of remaining displacement against `minDist` during sweeps and will break out of the loop if it’s = `minDist`. In practice, `minDist` is often set to a small epsilon (e.g., `0.001`).

- **Invisible Wall Height (`invisibleWallHeight`)**: A parameter (in units of length) that defines how high an “invisible wall” is, for non-walkable slopes. In PhysX’s CCT, this was historically used so that if you hit a wall below this height, it might be treated differently (for example, to prevent climbing an unwalkable slope that is short). By default this might be `0.0` (disabled), but if set, it could prevent stepping up onto tiny ledges on otherwise non-walkable surfaces. (This parameter is not commonly used; replicate if needed for completeness.)

- **Max Jump Height (`maxJumpHeight`)**: Another rarely-used parameter (units of length) influencing how the controller treats fall distances. It’s meant to help the controller stick to the ground when walking down slopes, by predicting that a drop less than `maxJumpHeight` should be handled as continuous ground. In PhysX 5.6, this might be integrated in how the “down” sweep or “sensor” is done, but it’s not explicitly used in the snippets above. You may replicate it to be safe: if stepping down less than this height, the controller could treat it as a drop rather than free-fall.

- **Max Edge Length (`maxEdgeLength` and tessellation)**: If a walkable surface has a long edge, PhysX can tessellate it to better approximate contact points. `mMaxEdgeLength` squared is stored as `mMaxEdgeLength2`. If an intersecting triangle’s edge exceeds this, PhysX might subdivide it (increasing `nbTessellation` count). The boolean `mTessellation` toggles this feature. This primarily affects how ground contact normals are computed on large polygons – you can likely skip this in a simple replication, but be aware it exists.

- **Overlap Recovery (`overlapRecovery`)**: A boolean (default true) controlling whether the controller should attempt to recover from overlaps automatically. When true, if the CCT finds itself intersecting geometry, it will compute a depenetration offset (`mOverlapRecover` in `Controller`) to push itself out. This is crucial for stable behavior; keep it enabled and implement the penetration resolution loop.

- **Precise Sweeps (`preciseSweeps`)**: A boolean (default true) that, if set, uses more accurate sweep tests by enabling the `PxHitFlag::ePRECISE_SWEEP` flag. Precise sweeps handle internal edge collisions better at the cost of performance. PhysX enables this by default for CCT to avoid snagging on edges. You should do the same (or at least have an equivalent high-quality sweep) to prevent the controller from catching on seams.

- **Prevent Vertical Sliding Against Ceiling (`preventVerticalSlidingAgainstCeiling`)**: A boolean that, if true, stops the character from attempting to slide along a ceiling when pressed up against it. In PhysX, when enabled, if the controller’s side movement causes a hit with a downward-facing normal (ceiling), the controller will cease vertical adjustment and effectively not slide horizontally either (it “sticks” under the ceiling rather than gliding). This is implemented by limiting the “up” sweep iterations to 1 and zeroing out upward motion if a ceiling is hit. It prevents a common jitter where a character crouches under a low ceiling and slides.

Most of these parameters are set once (when creating the controller or its manager) and remain constant, except `upDirection` which can be changed at runtime via `PxController::setUpDirection` (PhysX internally recalculates `mQuatFromUp` and related vectors on change).

## Persistent State Variables (Controller & SweepTest)

During simulation, the CCT keeps track of several pieces of state between frames.

### Update: mapping this to a stateless physics service

Our physics system must be able to accept inputs from different clients and must not rely on caching mutable controller state across frame updates inside the physics engine. Any "between frames" data must be modeled explicitly and passed in/out as data.

To do this, replace all "cached" PhysX references (shape/actor pointers, obstacle handles, timestamps, etc.) with **opaque IDs/tokens** provided by the caller and/or derived from query results.

Recommended split:

- **Stable identity (provided by caller):**
  - `CharacterId` (unique per character/controller instance)
  - `ClientId` (optional, if multiple clients may issue moves for the same service)
- **Surface reference (returned by physics, passed back next call):**
  - `SurfaceRefId` (opaque token identifying what we were standing on last tick)
  - `SurfaceLocalPoint` (optional: local-space point on the surface used for platform delta)
- **Per-tick execution context (provided by caller):**
  - `TickId` (monotonic simulation tick; replaces scene timestamp gating)
  - `DeltaTime`

The physics step becomes a pure function:

- **Input:** `CharacterMoveRequest(CharacterId, TickId, Params, PrevStateToken?, DesiredDisplacement, WorldSnapshot/QueryInterface)`
- **Output:** `CharacterMoveResult(NewPose, CollisionFlags, NewStateToken?, Diagnostics)`

Where `PrevStateToken`/`NewStateToken` is a small serializable blob containing only the information required to reproduce PhysX-like behavior (e.g., moving platform tracking). The physics system does not retain it internally.

- **Position (`mPosition` / `PxExtendedVec3`)**: In a stateless system, the controller position is owned by the caller. The physics step receives the current pose and returns the updated pose.

- **Collision Flags (`mCollisionFlags`)**: Returned as part of `CharacterMoveResult` each tick. Do not cache internally; callers can store the last result if needed.

- **Touched Shape/Actor (`mCctModule.mTouchedShape` and `mCctModule.mTouchedActor`)**: Replace pointer caching with an opaque `SurfaceRefId` suitable for serialization (e.g., `{Type=Entity, EntityId, ShapeId}` or `{Type=Obstacle, ObstacleId}`). The physics step may return `SurfaceRefId` when grounded; the caller passes it back next tick.

- **Touched Obstacle Handle (`mCctModule.mTouchedObstacleHandle`)**: Same as above: model as `SurfaceRefId` with a discriminator instead of an engine-side handle.

- **Touched Pos (Shape/Obstacle) (`mTouchedPos*`)**: Store this inside the returned `StateToken` as `SurfaceLocalPoint` (and optionally the previous computed world point). Next tick, recompute the world point from the current surface transform and derive `delta`.

- **Delta from platform (`mDeltaXP`)**: Return as part of `CharacterMoveResult` (e.g., `BaseVelocity` or `BaseDelta`). If callers need it next tick, they can include it in their own state, but the physics engine should treat it as ephemeral output.

- **Overlap Recovery Vector (`mOverlapRecover`)**: For a stateless system, either:
  - compute and apply the full depenetration in the same tick (preferred), or
  - include any remaining recovery vector in the returned `StateToken` (`PendingDepenetration`) and have the caller pass it back next tick.

- **Cached Standing On Moving (`mCachedStandingOnMoving`)**: Put this boolean in the `StateToken` (e.g., `WasStandingOnMovingSurface`). The physics step reads it only if it cannot/should not recompute platform delta for the current `TickId`.

- **Previous Scene Timestamp (`mPreviousSceneTimestamp`)**: Replace with `LastPlatformUpdateTickId` in the `StateToken`. The caller provides `TickId`; the physics step applies platform motion only if `TickId != LastPlatformUpdateTickId`.

- **Global Time (`mGlobalTime` and `mPreviousGlobalTime`)**: In a stateless model, use `DeltaTime` provided in the request to compute coefficients (e.g., `timeCoeff = 1/DeltaTime`). Do not accumulate internal clocks.

- **Statistics (`mNbIterations`, `mNbFullUpdates`, etc.)**: Counters in `mCctModule` for how many iterations and sub-updates were done in the last move. These can be useful for debugging (e.g., if `nbIterations` hits the max of 10 often, it means the character is colliding multiple times in a single move). You can update similar counters to verify your implementation (not strictly needed for functionality).

Finally, note that the PhysX controller manager holds global arrays for all controllers’ shapes (`mCCTShapes`) and obstacle data (lists of boxes/capsules). These are used to exclude the controller’s own actor from queries and to simulate controller-controller collisions. For simplicity, you might first implement a single controller without these complexities. If you do need multiple controllers interacting, you’ll need to implement similar global tracking so that one controller can treat others as obstacles (the PhysX code encodes controller indices into a `userData` pointer for this purpose).

## Recap and Usage

Before each simulation step for your controller, load these parameters from your config (`slopeLimit`, `stepOffset`, etc.). In a stateless system, the caller is responsible for providing the prior pose and prior `StateToken` (if any), and for storing the returned pose/token for the next tick.

1. After moving, return the new position, collision flags, and an updated `StateToken` containing `SurfaceRefId` + platform reference point (if grounded).
2. If any depenetration must carry over, return it in the `StateToken`.
3. Return platform delta/velocity as an output field (do not cache internally).

By mirroring both parameters and persistent state logic, your controller will behave the same as PhysX in all edge cases (stepping, sliding, riding platforms, etc.).
