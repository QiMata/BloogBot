## Overview

When the character is standing on a moving object (e.g., elevator or moving platform), PhysX’s CCT code adjusts the character’s movement so that it stays “glued” to the object. The `rideOnTouchedObject()` function handles this by decomposing the platform’s motion and merging it with the character’s.

## Key Steps in `rideOnTouchedObject()`

Assuming `mTouchedShape` (or an obstacle) from last frame is not null (meaning the character was on something), PhysX does the following:

### 1) Determine if an update is needed (timestamp gating)

#### Update: stateless service model

PhysX uses internal caching (`mPreviousSceneTimestamp`, `mCachedStandingOnMoving`, touched shape pointers, and stored local/world touched points). In a stateless physics service, this must be externalized as a `PrevStateToken` passed in, and a `NewStateToken` returned.

Suggested token fields:

- `SurfaceRefId` (opaque ID of the supporting surface)
- `SurfaceLocalPoint` (local-space point used to track frame-to-frame motion; for obstacles, store the equivalent reference point)
- `PrevSurfaceWorldPoint` (optional; can be derived but storing avoids recompute)
- `LastPlatformUpdateTickId`
- `CachedStandingOnMoving`

They check whether the scene’s simulation timestamp advanced since last update to avoid double-counting the same platform motion.

Replace this with tick gating:

- `canDoUpdate = (TickId != PrevStateToken.LastPlatformUpdateTickId)`

If the tick did **not** advance (e.g., duplicate requests), reuse cached data (and skip recomputing motion).

### 2) Compute platform movement (`delta`)

#### For a shape (moving actor)

- Get the current global pose of the touched shape and compare it to the stored position from last frame.

Key values:

- `posPreviousFrame = PrevStateToken.PrevSurfaceWorldPoint` (or compute from prior info)
- `posCurrentFrame = CurrentSurfaceTransform * PrevStateToken.SurfaceLocalPoint`
- `delta = posCurrentFrame - posPreviousFrame`  
  (The movement of the platform over the frame.)

#### For an obstacle (from `PxObstacleContext`)

They either have world positions directly or compute similarly if rotation is involved.

### 3) Query behavior flags (optional callback)

They query `PxControllerBehaviorCallback` (if provided) for the touched object to see if the user defined special flags.

- `eCCT_CAN_RIDE_ON_OBJECT`  
  - By default, obstacles are rideable.
  - For shapes, the default `behaviorFlags` is `0` (meaning rideable unless user says otherwise).
  - If the user clears `eCCT_CAN_RIDE_ON_OBJECT`, the controller will not move with that object (so it won’t add platform motion into controller movement).

Typical default behavior:

- Rideable for static/kinematic shapes and obstacles.

### 4) Update cached “standing on moving” state

They derive `standingOnMoving` based on `delta` and whether the object is rideable:

- If `canDoUpdate`:
  - `standingOnMoving = !isAlmostZero(delta)`
- Else:
  - `standingOnMoving = mCachedStandingOnMoving`

Then (in the returned `NewStateToken`):

- `CachedStandingOnMoving = standingOnMoving`
- `LastPlatformUpdateTickId = TickId`
- `PrevSurfaceWorldPoint = posCurrentFrame`

This ensures motion is applied once per sim step and avoids “flicker” from floating point noise.

### 5) Apply platform movement

If `standingOnMoving == true` and `behaviorFlags & eCCT_USER_DEFINED_RIDE` is **not** set (normal riding), they decompose motion into vertical and lateral components relative to `upDirection`.

#### Decompose `delta` into up and side components

- `dir_dot_up = delta.dot(upDirection)`  
  Determines how much of the platform motion is vertical vs lateral.

- `deltaUpDisp`  
  The component of `delta` along `upDirection`.
- `deltaSideDisp`  
  The remainder (horizontal / lateral component).

#### Vertical component rule: apply upward immediately, downward via sweeps

##### If platform moved **up** (`dir_dot_up > 0`)

They immediately move the character up by that amount (directly adjusting the controller volume center):

~~~cpp
if (deltaMovingUp) {
    volume.mCenter.x += PxExtended(deltaUpDisp.x);
    volume.mCenter.y += PxExtended(deltaUpDisp.y);
    volume.mCenter.z += PxExtended(deltaUpDisp.z);
}
~~~

Notes:

- The upward part is applied **before** any sweeps.
- They do **not** add upward motion into `disp`; they directly modify the controller position.
- This avoids “sinking” or one-frame lag behind a rising base.
- If the base pushes the character into a ceiling, later overlap recovery / collision response handles it.

##### If platform moved **down** or not up (`dir_dot_up <= 0`)

They do **not** directly move the character down. Instead, they add the vertical delta into the frame displacement:

~~~cpp
else {
    disp += deltaUpDisp;
}
~~~

Effect:

- The character follows the platform down via the normal sweep process (down sweep).
- Prevents hovering above a fast-descending platform.
- If gravity is also applied, this adds to it (ensuring the character keeps contact).

#### Horizontal component rule: add side motion to displacement

For the lateral component (`deltaSideDisp`):

- If the object is rideable (default):
  - Add it to `disp`
  - This carries the character sideways with the platform.

Examples:

- Platform moves east → character gets +east motion in `disp`.
- If player also inputs movement, it composes naturally.

#### User-defined ride hook

If `behaviorFlags & eCCT_USER_DEFINED_RIDE` is set:

- PhysX marks `standingOnMoving`, but does **not** modify `disp` or controller position.
- User code is expected to apply custom displacement.

## Return value and persisted data

- `rideOnTouchedObject()` returns the `standingOnMoving` boolean.
- `Controller::move` uses this to drive additional logic (step/slope logic differences when riding).
- They also compute and store base velocity in world units per second:

- Return `BaseVelocity = delta * timeCoeff` (or `BaseDelta = delta`) in the move result.

This is exposed through `PxControllerState` so external systems can know how fast the ground is moving them.

## Ride-on summary

### Vertical base motion

- **Upward platform:** character position is immediately raised pre-sweep.
- **Downward platform:** downward component is added to `disp` so the down sweep moves with the platform.

### Horizontal base motion

- Side component is added to `disp` so the character moves with the platform.

### Rideable vs non-rideable

- Applied only if object is flagged rideable (by default: yes).
- If flagged non-rideable:
  - The controller does not automatically carry the player.
  - With no friction simulation in CCT, the platform may slide out from under the character, and gravity causes them to fall when unsupported.

### Timestamp check

Ensures each platform motion is applied once per simulation step, robust with sub-stepping or variable step.

## Implementation notes

To replicate, you need to track the object underfoot and its movement:

- Track the previous platform position using a stable reference point.
- For shapes, PhysX stores:
  - `mTouchedPosShape_Local`
  - `mTouchedPosShape_World`

Suggested approach:

- At contact:
  - Store local point: `local = inverse(actorTransform) * charPosition`
- Next frame:
  - Recompute world point: `world = actorTransform * local`
  - `delta = world - prevWorld`
  - Update `prevWorld = world`

For purely translating objects, you may simplify by storing previous global pose and subtracting, but the local-point method handles rotation.

Handle detachment:

- If the object is no longer touched (jumped off, removed, etc.):
  - Set touched reference to null and reset caches.

## Testing scenarios

- Stand on a steadily moving platform:
  - No slip or lag; stable relative position on platform.
- Platform moves up into a ceiling:
  - Ride up until head contact; ceiling logic should engage.
- Platform moves down quickly:
  - Character should follow (no hover).
- Platform moves sideways:
  - Character moves with it (no slipping).
- Walk while platform moves:
  - Input + platform motion compose correctly (including opposite directions).
- Platform starts/stops:
  - `standingOnMoving` transitions cleanly and doesn’t flicker from float noise.

## Rotation considerations

PhysX’s local-point method handles rotating platforms. If you don’t support rotation initially:

- Approximate with linear motion only, or
- Use the actor’s transform each frame and the saved local reference point to reproduce PhysX behavior.

## Edge cases

- Standing at the edge of a moving platform:
  - Depending on contact caching, you may need to retain “touched” state from the down probe even near edges.
- Very high base speeds / teleports:
  - `delta` can become large in one frame; this can cause tunneling unless later logic corrects it.
- Floating error flicker:
  - Use an `isAlmostZero(delta)` threshold (e.g., `< 0.001`) to avoid toggling `standingOnMoving`.

## Porting notes

If your engine exposes platform velocity directly:

- You can use velocity integration, but PhysX’s approach is robust because it computes *actual* frame-to-frame displacement of a fixed point on the object.

Always update ride motion **before** main character sweeps so the sweeps “see” the updated base position.

## Notes on sources

- **Sources:** PhysX `rideOnTouchedObject` implementation (referenced conceptually in this document).
- **Search terms:** “PhysX moving platform character”, “PxController rideOnTouchedObject”, “mTouchedPosShape”.