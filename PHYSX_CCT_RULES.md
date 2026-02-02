# PhysX Character Controller (CCT) Movement Rules

> **Purpose**: This document defines the exact algorithm for capsule-based character movement following PhysX CCT conventions. These rules are atomic and must be followed in order.

---

## Table of Contents

1. [Constants and Tolerances](#1-constants-and-tolerances)
2. [Data Structures](#2-data-structures)
3. [Main Entry Point: Controller.move()](#3-main-entry-point-controllermove)
4. [Movement Decomposition](#4-movement-decomposition)
5. [Initial Volume Query](#5-initial-volume-query)
6. [The Three-Pass System](#6-the-three-pass-system)
7. [Sweep Test Algorithm (doSweepTest)](#7-sweep-test-algorithm-dosweeptest)
8. [Collision Response](#8-collision-response)
9. [MTD Recovery (Overlap Resolution)](#9-mtd-recovery-overlap-resolution)
10. [Slope Handling](#10-slope-handling)
11. [Walk Experiment (Non-Walkable Recovery)](#11-walk-experiment-non-walkable-recovery)
12. [Ground Detection](#12-ground-detection)
13. [Moving Platform Support](#13-moving-platform-support)
14. [Edge Cases and Guards](#14-edge-cases-and-guards)

---

## 1. Constants and Tolerances

### 1.1 Movement Constants

```
MAX_ITERATIONS          = 10        # Maximum collision iterations per sweep pass
                                    # ⚠️ CRITICAL: Must be 10, not 4. Lower values cause stuck issues.
MIN_MOVE_DISTANCE       = 0.001     # Distances below this are treated as zero (meters)
CONTACT_OFFSET          = 0.01      # Skin width / separation distance (meters)
DEFAULT_STEP_OFFSET     = 0.5       # Auto-step height (meters)
DEFAULT_SLOPE_LIMIT     = 0.707     # cos(45°) - walkable slope threshold (normal.z >= this)
VOLUME_GROWTH           = 1.5       # Cache bounds growth factor
```

> **⚠️ IMPLEMENTATION NOTE**: The `MAX_ITERATIONS` value of 10 is critical. Using fewer
> iterations (e.g., 4) will cause characters to get stuck in complex geometry, tight
> corners, or when sliding along multiple surfaces. PhysX uses 10 as the default.

### 1.2 Tolerance Rules

| Tolerance | Value | Usage |
|-----------|-------|-------|
| `EPSILON_DISTANCE` | 1e-6 | Distance comparisons |
| `EPSILON_NORMAL` | 1e-5 | Normal vector validity |
| `EPSILON_DOT` | 1e-4 | Dot product comparisons |
| `NEAR_PARALLEL` | 0.999 | Parallel vector detection |
| `NEAR_PERPENDICULAR` | 0.001 | Perpendicular vector detection |

### 1.3 Collision Flags (Bitmask)

```
COLLISION_NONE    = 0x00
COLLISION_SIDES   = 0x01    # Hit wall/obstacle laterally
COLLISION_UP      = 0x02    # Hit ceiling/obstacle above
COLLISION_DOWN    = 0x04    # Hit ground/obstacle below
```

---

## 2. Data Structures

### 2.1 Capsule Definition

```
struct Capsule:
    p0: Vector3          # Bottom sphere center (at feet + radius)
    p1: Vector3          # Top sphere center (at head - radius)
    radius: float        # Capsule radius
    
    # Derived properties:
    height = |p1 - p0|                    # Cylinder height (not total height)
    halfHeight = (|p1 - p0| / 2) + radius # Half of total capsule height
    center = (p0 + p1) / 2                # Capsule center point
    feetZ = min(p0.z, p1.z) - radius      # Lowest point
    headZ = max(p0.z, p1.z) + radius      # Highest point
```

### 2.2 Swept Volume

```
struct SweptVolume:
    center: ExtendedVector3    # Current position (high precision)
    halfHeight: float          # Half total height for bounds
    # For capsule: includes radius in halfHeight
```

### 2.3 Swept Contact (Hit Result)

```
struct SweptContact:
    distance: float           # Distance to hit (0 = overlap)
    worldNormal: Vector3      # Surface normal at hit point
    worldPos: Vector3         # World position of contact
    internalIndex: uint32     # Index in cached triangle array
    triangleIndex: uint32     # Original mesh triangle index
    geom: TouchedGeom*        # Reference to touched geometry
```

### 2.4 Decomposed Movement

```
struct DecomposedMovement:
    upVector: Vector3         # Vertical up component + stepOffset
    sideVector: Vector3       # Horizontal movement component
    downVector: Vector3       # Vertical down component (gravity + step recovery)
    stepOffset: float         # Applied step offset (may be clamped)
    isMovingUp: bool          # True if original direction has upward component
    hasSideMovement: bool     # True if lateral movement is non-zero
```

### 2.5 Movement State (Per-Frame)

```
struct MovementState:
    position: Vector3
    velocity: Vector3
    isGrounded: bool
    groundNormal: Vector3
    groundTriIndex: uint32
    groundInstanceId: uint32
    contactPointHeight: float    # Z of last down-pass contact (for slope validation)
    touchedActor: Actor*
    touchedShape: Shape*
    collisionFlags: uint32
    
    # ⚠️ CRITICAL: Contact point height tracking
    # This field stores the Z coordinate of the contact point from collision.
    # It is used in slope validation to determine if the contact is above
    # the step offset threshold. Formula:
    #   contactPointHeight = dot(contact.worldPos, upDirection)
    # 
    # In constrained climbing mode, if:
    #   contactPointHeight > originalBottomPoint + stepOffset
    # Then the slope is considered non-walkable regardless of normal angle.
```

---

## 3. Main Entry Point: Controller.move()

### 3.1 Function Signature

```
function move(displacement: Vector3, minDist: float, elapsedTime: float, filters: Filters) -> CollisionFlags
```

### 3.2 Execution Steps

```
STEP 3.2.1: Create swept volume from current position
    sweptCapsule.center = controller.position
    sweptCapsule.radius = controller.radius
    sweptCapsule.height = controller.height
    sweptCapsule.halfHeight = (height / 2) + radius

STEP 3.2.2: Determine climbing mode
    constrainedClimbingMode = (capsule AND climbingMode == CONSTRAINED)

STEP 3.2.3: Determine if standing on moving object
    standingOnMoving = (previousTouchedActor != NULL AND previousTouchedActor.isMoving)

STEP 3.2.4: Call core movement function
    collisionFlags = moveCharacter(sweptCapsule, displacement, minDist, filters, 
                                   constrainedClimbingMode, standingOnMoving)

STEP 3.2.5: Handle non-walkable slope (walk experiment)
    IF flags contain HIT_NON_WALKABLE:
        backup = sweptCapsule.center
        
        IF nonWalkableMode == PREVENT_CLIMBING_AND_FORCE_SLIDING:
            # Remove vertical component, keep only tangent
            decompose(displacement, upDirection) -> (normalCompo, tangentCompo)
            experimentDisp = tangentCompo
        ELSE:
            experimentDisp = displacement
        
        # Retry with walk experiment flag set
        SET flag WALK_EXPERIMENT
        sweptCapsule.center = backup
        collisionFlags = moveCharacter(sweptCapsule, experimentDisp, ...)
        CLEAR flag WALK_EXPERIMENT

STEP 3.2.6: Update controller state
    controller.position = sweptCapsule.center
    controller.collisionFlags = collisionFlags
    
    IF kinematicActor exists AND position changed:
        kinematicActor.setKinematicTarget(position, orientation)

STEP 3.2.7: Return collision flags
    RETURN collisionFlags
```

---

## 4. Movement Decomposition

### 4.1 Purpose

Split the input displacement vector into three orthogonal passes to enable auto-stepping and proper ground detection.

### 4.2 Algorithm

```
function decomposeMovement(direction: Vector3, upDirection: Vector3, stepOffset: float,
                           isJumping: bool, standingOnMoving: bool) -> DecomposedMovement:

STEP 4.2.1: Initialize output
    result.upVector = Vector3(0, 0, 0)
    result.downVector = Vector3(0, 0, 0)
    result.stepOffset = stepOffset

STEP 4.2.2: Decompose direction into vertical and horizontal components
    # Project direction onto up axis to get vertical component
    verticalMagnitude = dot(direction, upDirection)
    normalCompo = upDirection * verticalMagnitude
    tangentCompo = direction - normalCompo
    
STEP 4.2.3: Assign vertical component to UP or DOWN vector
    IF verticalMagnitude > 0:
        result.upVector = normalCompo
        result.isMovingUp = TRUE
    ELSE:
        result.downVector = normalCompo
        result.isMovingUp = FALSE

STEP 4.2.4: Assign horizontal component
    result.sideVector = tangentCompo

STEP 4.2.5: Determine if side movement exists
    result.hasSideMovement = (magnitude(tangentCompo) > EPSILON_DISTANCE)
    
    # Also check for standing on moving platform edge case
    IF standingOnMoving AND NOT result.hasSideMovement:
        result.hasSideMovement = TRUE  # Allow step offset for platform following

STEP 4.2.6: Cancel step offset when jumping (not on moving platform)
    IF result.isMovingUp AND NOT standingOnMoving:
        result.stepOffset = 0

STEP 4.2.7: Inject step offset into UP vector (only if side movement exists)
    IF result.hasSideMovement AND result.stepOffset > 0:
        result.upVector = result.upVector + (upDirection * result.stepOffset)

STEP 4.2.8: Return decomposed movement
    RETURN result
```

### 4.3 Decomposition Rules

| Condition | UP Vector | SIDE Vector | DOWN Vector | Step Offset |
|-----------|-----------|-------------|-------------|-------------|
| Moving forward on ground | `upDir * stepOffset` | horizontal dir | gravity | active |
| Jumping | vertical component | horizontal dir | 0 | **cancelled** |
| Falling | 0 | horizontal dir | gravity | active |
| Standing still | 0 | 0 | gravity | active |
| On moving platform | `upDir * stepOffset` | platform-relative | gravity | active |

---

## 5. Initial Volume Query

### 5.1 Purpose

Pre-fetch all geometry that could possibly be touched during the entire movement. This establishes the working set for all three passes.

### 5.2 Algorithm

```
function computeInitialVolumeQuery(volume: SweptVolume, direction: Vector3,
                                   stepOffset: float, filters: Filters) -> TouchedGeomSet:

STEP 5.2.1: Compute temporal bounding box for FULL movement
    # This box encompasses all possible positions during the frame
    temporalBox = computeTemporalBox(volume.center, direction)
    
STEP 5.2.2: Expand bounds by step offset (for auto-step geometry)
    IF stepOffset > 0:
        temporalBox.max.z += stepOffset

STEP 5.2.3: Apply volume growth factor for caching
    temporalBox = scale(temporalBox, VOLUME_GROWTH)
    
    # Bias growth in movement direction for better cache hits
    IF magnitude(sideVector) > EPSILON_DISTANCE:
        sideNorm = normalize(sideVector)
        offset = computeDirectionalBias(temporalBox, sideNorm)
        temporalBox.min += offset
        temporalBox.max += offset

STEP 5.2.4: Query static geometry
    staticGeoms = queryScene(temporalBox, filters, STATIC_ONLY)
    
STEP 5.2.5: Query dynamic geometry  
    dynamicGeoms = queryScene(temporalBox, filters, DYNAMIC_ONLY)

STEP 5.2.6: Query user obstacles (other CCTs, custom obstacles)
    userObstacles = gatherUserObstacles(temporalBox)

STEP 5.2.7: Cache static geometry (reuse across frames if bounds unchanged)
    IF temporalBox is inside cachedBounds AND sceneTimestamp unchanged:
        # Reuse cached static, only refresh dynamic
        RETURN cachedStatic + dynamicGeoms + userObstacles
    ELSE:
        cachedBounds = temporalBox
        cachedStatic = staticGeoms
        RETURN staticGeoms + dynamicGeoms + userObstacles
```

### 5.3 Temporal Box Computation

```
function computeTemporalBox(center: Vector3, direction: Vector3) -> AABB:

STEP 5.3.1: Get volume extents (half-extents for capsule)
    extents = Vector3(radius, radius, halfHeight)

STEP 5.3.2: Compute start bounds
    startMin = center - extents
    startMax = center + extents

STEP 5.3.3: Compute end bounds  
    endCenter = center + direction
    endMin = endCenter - extents
    endMax = endCenter + extents

STEP 5.3.4: Union start and end bounds
    result.min = min(startMin, endMin)
    result.max = max(startMax, endMax)
    
STEP 5.3.5: Return temporal box
    RETURN result
```

---

## 6. The Three-Pass System

### 6.1 Overview

Movement is executed in three sequential passes: UP → SIDE → DOWN. Each pass calls the same `doSweepTest` function but with different parameters.

### 6.2 Pass Execution Order

```
function moveCharacter(volume: SweptVolume, direction: Vector3, minDist: float,
                       filters: Filters, constrainedClimbing: bool, 
                       standingOnMoving: bool) -> CollisionFlags:

STEP 6.2.1: Initialize
    collisionFlags = COLLISION_NONE
    originalHeight = dot(volume.center, upDirection)
    originalBottomPoint = originalHeight - volume.halfHeight

STEP 6.2.2: Decompose movement
    decomposed = decomposeMovement(direction, upDirection, stepOffset,
                                   isJumping, standingOnMoving)

STEP 6.2.3: Initial volume query
    updateTouchedGeoms(volume, direction, filters)

STEP 6.2.4: Execute UP pass
    # Determine max iterations for UP pass
    IF preventVerticalSlidingAgainstCeiling:
        maxIterUp = 1
    ELSE IF hasSideMovement:
        maxIterUp = 1
    ELSE:
        maxIterUp = MAX_ITERATIONS
    
    IF NOT walkExperimentActive:
        upCollisions = 0
        hasMoved = doSweepTest(volume, decomposed.upVector, maxIterUp, 
                               upCollisions, minDist, SWEEP_PASS_UP)
        
        IF upCollisions > 0:
            collisionFlags |= COLLISION_UP
            # Clamp step offset to actual achieved lift
            actualDelta = dot(volume.center, upDirection) - originalHeight
            IF actualDelta < decomposed.stepOffset:
                decomposed.stepOffset = actualDelta

STEP 6.2.5: Execute SIDE pass
    sideCollisions = 0
    hasMoved = doSweepTest(volume, decomposed.sideVector, MAX_ITERATIONS,
                           sideCollisions, minDist, SWEEP_PASS_SIDE)
    
    IF sideCollisions > 0:
        collisionFlags |= COLLISION_SIDES
    
    # =========================================================================
    # ⚠️ CRITICAL: Constrained climbing sensor sweep (capsule only)
    # =========================================================================
    # This sensor sweep MUST happen AFTER the side pass, not before.
    # When lateral movement is smaller than capsule radius, the normal side
    # sweep may miss steep slopes. The sensor extends to capsule radius to
    # detect these slopes and set HIT_NON_WALKABLE appropriately.
    # =========================================================================
    IF constrainedClimbing AND volume.type == CAPSULE:
        sideMagnitude = magnitude(decomposed.sideVector)
        IF sideMagnitude < volume.radius:
            # Movement too small to detect slopes - extend sensor
            sensor = normalize(decomposed.sideVector) * volume.radius
            savedCenter = volume.center
            
            # Perform sensor sweep (don't apply movement, just detect)
            sensorCollisions = 0
            doSweepTest(volume, sensor, 1, sensorCollisions, minDist, SWEEP_PASS_SENSOR)
            
            # Check if sensor detected non-walkable slope
            IF sensorCollisions > 0 AND validateTriangleSide:
                IF testSlope(contactNormalSidePass, upDirection, slopeLimit):
                    IF contactPointHeight > originalBottomPoint + stepOffset:
                        SET flag HIT_NON_WALKABLE
            
            volume.center = savedCenter  # IMPORTANT: Don't apply sensor movement

STEP 6.2.6: Execute DOWN pass
    # =========================================================================
    # ⚠️ CRITICAL: Variable max iterations for DOWN pass
    # =========================================================================
    # Normal DOWN pass uses maxIter=1 (just find ground).
    # Walk experiment with PREVENT_CLIMBING_AND_FORCE_SLIDING uses maxIter=10
    # to allow sliding down non-walkable slopes.
    # =========================================================================
    IF walkExperimentActive AND nonWalkableMode == PREVENT_CLIMBING_AND_FORCE_SLIDING:
        maxIterDown = MAX_ITERATIONS  # Allow full sliding
    ELSE:
        maxIterDown = 1               # Just find ground
    
    # Undo step offset injection
    IF decomposed.hasSideMovement:
        decomposed.downVector = decomposed.downVector - (upDirection * decomposed.stepOffset)
    
    downCollisions = 0
    touchedActor = NULL
    touchedShape = NULL
    hasMoved = doSweepTest(volume, decomposed.downVector, maxIterDown,
                           downCollisions, minDist, SWEEP_PASS_DOWN,
                           OUT touchedActor, OUT touchedShape)
    
    IF downCollisions > 0:
        IF NOT isMovingUp:  # Don't flag down collision when jumping up
            collisionFlags |= COLLISION_DOWN

STEP 6.2.7: Validate slope and trigger walk experiment if needed
    # See Section 10 for slope handling details
    IF handleSlope AND validateTriangleDown AND directionDotUp <= 0:
        IF touchedTriangleHeight > stepOffset AND testSlope(contactNormal, slopeLimit):
            SET flag HIT_NON_WALKABLE

STEP 6.2.8: Return collision flags
    RETURN collisionFlags
```

### 6.3 Pass Parameters Summary

| Pass | Vector | Max Iterations | Contact Tracking | Notes |
|------|--------|----------------|------------------|-------|
| UP | `upVector` | 1 (usually) | No | Artificial lift for auto-step |
| SIDE | `sideVector` | 10 | Optional | Main lateral movement |
| DOWN | `downVector` | 1 or 10 ⚠️ | Yes | Ground detection + step recovery |

> **⚠️ CRITICAL**: DOWN pass uses 1 iteration normally, but **10 iterations** during
> walk experiment with `PREVENT_CLIMBING_AND_FORCE_SLIDING` mode. Using always 1
> iteration breaks slope sliding behavior.

---

## 7. Sweep Test Algorithm (doSweepTest)

### 7.1 Purpose

This is the core iterative collision detection and response loop. It moves the volume as far as possible along a direction while handling collisions.

### 7.2 Full Algorithm

```
function doSweepTest(volume: SweptVolume, direction: Vector3, maxIter: uint32,
                     OUT nbCollisions: uint32, minDist: float, sweepPass: SweepPassType,
                     OUT touchedActor: Actor, OUT touchedShape: Shape) -> bool:

STEP 7.2.1: Early exit check
    IF isZero(direction):
        RETURN FALSE  # No movement needed

STEP 7.2.2: Initialize state
    hasMoved = FALSE
    nbCollisions = 0
    touchedActor = NULL
    touchedShape = NULL
    currentPosition = volume.center
    targetPosition = volume.center + direction

STEP 7.2.3: Main iteration loop
    WHILE maxIter > 0:
        maxIter -= 1
        
        # --- STEP 7.2.3a: Compute current direction to target ---
        currentDirection = targetPosition - currentPosition
        
        # --- STEP 7.2.3b: Update temporal bounds if needed ---
        temporalBox = computeTemporalBox(currentPosition, currentDirection)
        IF temporalBox NOT inside cachedBounds:
            updateTouchedGeoms(currentPosition, currentDirection)
        
        # --- STEP 7.2.3c: Check minimum distance threshold ---
        length = magnitude(currentDirection)
        IF length <= minDist:
            BREAK  # Close enough to target
        
        # --- STEP 7.2.3d: Normalize direction ---
        currentDirection = currentDirection / length
        
        # --- STEP 7.2.3e: Quake2 anti-oscillation check ---
        IF dot(currentDirection, direction) <= 0:
            BREAK  # Moving backwards relative to original intent - stop
        
        # --- STEP 7.2.3f: Mark that we're attempting movement ---
        hasMoved = TRUE
        
        # --- STEP 7.2.3g: Find closest collision ---
        # ⚠️ CRITICAL FIX: Initialize search distance to include contact offset
        # This ensures we find collisions that are within skin-width distance
        contact = SweptContact()
        contact.distance = length + CONTACT_OFFSET  # ← MUST include contact offset!
        
        foundHit = collideGeoms(volume, touchedGeoms, currentPosition, 
                                currentDirection, contact)
        
        # --- STEP 7.2.3h: No collision - move to target ---
        IF NOT foundHit:
            currentPosition = targetPosition
            BREAK
        
        # --- STEP 7.2.3i: Handle overlap (distance = 0) ---
        IF overlapRecoveryEnabled AND contact.distance == 0:
            mtdPosition = computeMTD(volume, touchedGeoms, currentPosition, CONTACT_OFFSET)
            volume.center = mtdPosition
            nbCollisions += 1
            RETURN hasMoved  # Exit after MTD recovery
        
        # --- STEP 7.2.3j: Process collision based on geometry type ---
        IF contact.geom.type == USER_BOX OR contact.geom.type == USER_CAPSULE:
            # Touched another CCT or user obstacle
            IF sweepPass != SWEEP_PASS_SENSOR:
                behaviorFlags = userHitCallback(contact, currentDirection, length)
                stopSliding = NOT (behaviorFlags & CCT_SLIDE)
                
                IF sweepPass == SWEEP_PASS_DOWN:
                    IF touchedObstacle:
                        SET flag TOUCH_OBSTACLE
                        record obstacle position for platform tracking
                    ELSE:
                        SET flag TOUCH_OTHER_CCT
        ELSE:
            # Touched scene geometry (mesh, box, sphere, etc.)
            shape = contact.geom.shape
            actor = contact.geom.actor
            
            IF sweepPass == SWEEP_PASS_DOWN:
                CLEAR flags TOUCH_OTHER_CCT, TOUCH_OBSTACLE
                
                # Validate triangle for slope testing
                IF actor.type == RIGID_STATIC AND contact.internalIndex != INVALID:
                    SET flag VALIDATE_TRIANGLE_DOWN
                    triangle = worldTriangles[contact.internalIndex]
                    contactNormalDownPass = triangle.normal()
                    
                    # Track triangle height range for slope check
                    compute touchedTriMin, touchedTriMax from triangle vertices
                
                # Update touched shape/actor
                touchedShapeOut = shape
                touchedActorOut = actor
                touchedPosShape_World = contact.worldPos
                touchedPosShape_Local = shape.transform.inverse * contact.worldPos
            
            ELSE IF sweepPass == SWEEP_PASS_SIDE OR sweepPass == SWEEP_PASS_SENSOR:
                IF actor.type == RIGID_STATIC AND contact.internalIndex != INVALID:
                    SET flag VALIDATE_TRIANGLE_SIDE
                    triangle = worldTriangles[contact.internalIndex]
                    contactNormalSidePass = triangle.normal()
                    
                    # Check for ceiling hit to prevent vertical sliding
                    IF preventVerticalSlidingAgainstCeiling:
                        IF dot(contactNormalSidePass, upDirection) < 0:
                            preventVerticalMotion = TRUE
            
            # Invoke shape hit callback
            IF sweepPass != SWEEP_PASS_SENSOR:
                behaviorFlags = shapeHitCallback(contact, currentDirection, length)
                stopSliding = NOT (behaviorFlags & CCT_SLIDE)
        
        # --- STEP 7.2.3k: First collision special handling for DOWN pass ---
        IF sweepPass == SWEEP_PASS_DOWN AND NOT stopSliding:
            IF nbCollisions == 0:
                # Standing on another CCT - allow more iterations for sliding off
                maxIter += 9
        
        # --- STEP 7.2.3l: Increment collision counter ---
        nbCollisions += 1
        
        # --- STEP 7.2.3m: Record contact point height ---
        contactPointHeight = dot(contact.worldPos, upDirection)
        
        # --- STEP 7.2.3n: Advance position with contact offset ---
        IF contact.distance > CONTACT_OFFSET:
            currentPosition = currentPosition + currentDirection * (contact.distance - CONTACT_OFFSET)
        
        # --- STEP 7.2.3o: Compute collision response (new target) ---
        worldNormal = contact.worldNormal
        
        # Handle vertical motion prevention
        IF preventVerticalMotion OR (walkExperimentActive AND nonWalkableMode != FORCE_SLIDING):
            # Cancel normal's vertical component
            decompose(worldNormal, upDirection) -> (normalCompo, tangentCompo)
            worldNormal = normalize(tangentCompo)
        
        # Compute new target position via collision response
        collisionResponse(OUT targetPosition, currentPosition, currentDirection,
                          worldNormal, bump=0.0, friction=1.0, normalizeResponse)
    
    # END WHILE

STEP 7.2.4: Finalize
    volume.center = currentPosition
    RETURN hasMoved
```

### 7.3 Sweep Test Invariants

1. **Direction check**: Always verify `magnitude(direction) > EPSILON` before proceeding
2. **Quake2 check**: Exit if `dot(currentDir, originalDir) <= 0` to prevent oscillation
3. **Distance threshold**: Exit if `distance <= minDist` to prevent infinite loops
4. **Contact offset**: Always maintain `CONTACT_OFFSET` separation from surfaces
5. **MTD priority**: Overlap resolution (MTD) takes precedence over sweep

---

## 8. Collision Response

### 8.1 Purpose

Calculate the new target position after a collision, implementing slide behavior along surfaces.

### 8.2 Reflection-Based Response (PhysX Method)

```
function collisionResponse(OUT targetPosition: Vector3, currentPosition: Vector3,
                           currentDirection: Vector3, hitNormal: Vector3,
                           bump: float, friction: float, normalize: bool):

STEP 8.2.1: Compute reflection vector
    # Standard reflection formula: R = D - 2(D·N)N
    reflectDir = currentDirection - hitNormal * 2.0 * dot(currentDirection, hitNormal)
    reflectDir = normalize(reflectDir)

STEP 8.2.2: Decompose reflection into normal and tangent components
    # normalCompo: component along hit normal (bounce)
    # tangentCompo: component perpendicular to hit normal (slide)
    normalCompo = hitNormal * dot(reflectDir, hitNormal)
    tangentCompo = reflectDir - normalCompo

STEP 8.2.3: Compute remaining movement amplitude
    amplitude = magnitude(targetPosition - currentPosition)

STEP 8.2.4: Apply bump and friction coefficients
    targetPosition = currentPosition
    
    IF bump != 0.0:
        IF normalize:
            normalCompo = normalize(normalCompo)
        targetPosition = targetPosition + normalCompo * bump * amplitude
    
    IF friction != 0.0:
        IF normalize:
            tangentCompo = normalize(tangentCompo)
        targetPosition = targetPosition + tangentCompo * friction * amplitude

# Note: PhysX CCT typically uses bump=0.0, friction=1.0
# This effectively projects remaining movement onto the tangent plane (pure slide)
```

### 8.3 Simplified Slide Response (Alternative)

```
function slideResponse(OUT targetPosition: Vector3, currentPosition: Vector3,
                       remainingVelocity: Vector3, hitNormal: Vector3):

STEP 8.3.1: Project velocity onto tangent plane
    # Remove component along normal
    normalComponent = hitNormal * dot(remainingVelocity, hitNormal)
    slideVelocity = remainingVelocity - normalComponent

STEP 8.3.2: Compute new target
    targetPosition = currentPosition + slideVelocity
```

### 8.4 Crease Handling (Two-Plane Collision)

```
function handleCreaseCollision(moveDir: Vector3, normal1: Vector3, normal2: Vector3) -> Vector3:

STEP 8.4.1: Check if normals form a crease (not parallel)
    creaseDir = cross(normal1, normal2)
    IF magnitude(creaseDir) < EPSILON_NORMAL:
        RETURN Vector3(0,0,0)  # Parallel planes - fully blocked

STEP 8.4.2: Normalize crease direction
    creaseDir = normalize(creaseDir)

STEP 8.4.3: Ensure crease direction aligns with movement intent
    IF dot(creaseDir, moveDir) < 0:
        creaseDir = -creaseDir

STEP 8.4.4: Project movement onto crease line
    slideAmount = dot(moveDir, creaseDir)
    RETURN creaseDir * slideAmount
```

---

## 9. MTD Recovery (Overlap Resolution)

### 9.1 Purpose

When the capsule starts in an overlapping state (distance = 0), compute the Minimum Translation Distance to push it out.

### 9.2 Algorithm

```
function computeMTD(volume: SweptVolume, geoms: TouchedGeomSet,
                    position: Vector3, contactOffset: float) -> Vector3:

STEP 9.2.1: Initialize accumulator
    totalMTD = Vector3(0, 0, 0)
    maxPenetration = 0

STEP 9.2.2: Iterate all touched geometries
    FOR EACH geom IN geoms:
        # Only process geometry that should block recovery
        IF NOT shouldApplyRecoveryModule(geom.actor):
            CONTINUE  # Skip dynamic objects (let physics handle them)
        
        # Compute penetration for this geometry
        hit = overlapTest(volume, geom, position)
        
        IF hit.overlapping:
            penetration = hit.depth + contactOffset
            direction = hit.normal  # Points from geom toward capsule
            
            # Accumulate weighted MTD
            IF penetration > maxPenetration:
                maxPenetration = penetration
            
            totalMTD = totalMTD + direction * penetration

STEP 9.2.3: Apply MTD to position
    IF magnitude(totalMTD) > EPSILON_DISTANCE:
        newPosition = position + totalMTD
    ELSE:
        newPosition = position

STEP 9.2.4: Return corrected position
    RETURN newPosition
```

### 9.3 Recovery Module Eligibility

```
function shouldApplyRecoveryModule(actor: RigidActor) -> bool:
    # Always recover from static geometry
    IF actor.type == RIGID_STATIC:
        RETURN TRUE
    
    # Recover from kinematic actors
    IF actor.type == RIGID_DYNAMIC:
        IF actor.flags & KINEMATIC:
            RETURN TRUE
    
    # Don't recover from regular dynamic objects (physics will handle)
    RETURN FALSE
```

---

## 10. Slope Handling

### 10.1 Slope Test Function

```
function testSlope(normal: Vector3, upDirection: Vector3, slopeLimit: float) -> bool:
    # slopeLimit is cos(maxAngle), e.g., cos(45°) ≈ 0.707
    # Returns TRUE if slope is TOO STEEP (non-walkable)
    
    cosAngle = dot(normal, upDirection)
    RETURN cosAngle < slopeLimit
```

### 10.2 Walkability Check

```
function isWalkable(normal: Vector3) -> bool:
    RETURN normal.z >= DEFAULT_SLOPE_LIMIT  # For Z-up coordinate system
```

### 10.3 Slope Validation in DOWN Pass

```
# After DOWN pass collision:

IF handleSlope AND validateTriangleDown AND directionDotUp <= 0:
    # Get contact normal from touched triangle
    normal = contactNormalDownPass
    
    # Compute touched triangle height relative to character bottom
    touchedTriHeight = touchedTriMax - originalBottomPoint
    
    # Check if slope is too steep AND high enough to matter
    IF touchedTriHeight > stepOffset AND testSlope(normal, upDirection, slopeLimit):
        SET flag HIT_NON_WALKABLE
        
        # If not in walk experiment, exit to trigger retry
        IF NOT walkExperimentActive:
            RETURN collisionFlags
```

### 10.4 Side Pass Slope Check (Constrained Climbing)

```
# During SIDE pass for constrained climbing mode:

IF constrainedClimbingMode AND validateTriangleSide:
    IF testSlope(contactNormalSidePass, upDirection, slopeLimit):
        # Check if contact point is above step threshold
        IF contactPointHeight > originalBottomPoint + stepOffset:
            SET flag HIT_NON_WALKABLE
            IF NOT walkExperimentActive:
                RETURN collisionFlags
```

---

## 11. Walk Experiment (Non-Walkable Recovery)

### 11.1 Purpose

When the character lands on a non-walkable slope, retry the movement with modified parameters to either slide down or prevent climbing.

### 11.2 Algorithm

```
function executeWalkExperiment(volume: SweptVolume, originalDisp: Vector3,
                               originalPosition: Vector3) -> CollisionFlags:

STEP 11.2.1: Restore original position
    volume.center = originalPosition

STEP 11.2.2: Modify displacement based on mode
    IF nonWalkableMode == PREVENT_CLIMBING_AND_FORCE_SLIDING:
        # Remove vertical component - only allow horizontal slide
        decompose(originalDisp, upDirection) -> (verticalCompo, horizontalCompo)
        experimentDisp = horizontalCompo
    ELSE:
        # Use original displacement
        experimentDisp = originalDisp

STEP 11.2.3: Set walk experiment flags
    SET flag WALK_EXPERIMENT
    SET flag NORMALIZE_RESPONSE  # Use normalized collision response

STEP 11.2.4: Re-run movement with modified parameters
    # The WALK_EXPERIMENT flag causes:
    # - UP pass to be skipped
    # - DOWN pass to use MAX_ITERATIONS instead of 1
    # - Vertical normal components to be cancelled in collision response
    collisionFlags = moveCharacter(volume, experimentDisp, ...)

STEP 11.2.5: Recovery sweep if still on non-walkable
    IF validateTriangleDown AND testSlope(contactNormalDownPass, slopeLimit):
        # Compute recovery distance
        currentHeight = dot(volume.center, upDirection)
        delta = max(0, currentHeight - originalHeight)
        delta += abs(dot(originalDisp, upDirection))
        
        # Sweep downward to find stable ground
        recoverDir = -upDirection * delta
        doSweepTest(volume, recoverDir, MAX_ITERATIONS, _, minDist, SWEEP_PASS_UP)

STEP 11.2.6: Clear flags and return
    CLEAR flag WALK_EXPERIMENT
    CLEAR flag NORMALIZE_RESPONSE
    RETURN collisionFlags
```

---

## 12. Ground Detection

### 12.1 Ground Probe Sweep

```
function probeGround(volume: SweptVolume, maxDistance: float) -> GroundResult:

STEP 12.1.1: Setup downward sweep
    sweepDir = -upDirection
    
STEP 12.1.2: Execute sweep
    contact = SweptContact()
    contact.distance = maxDistance + CONTACT_OFFSET
    
    foundHit = collideGeoms(volume, touchedGeoms, volume.center, sweepDir, contact)

STEP 12.1.3: Process result
    IF foundHit AND contact.distance <= maxDistance:
        result.grounded = TRUE
        result.groundNormal = contact.worldNormal
        result.groundDistance = contact.distance
        result.groundPoint = contact.worldPos
        result.walkable = isWalkable(contact.worldNormal)
        result.triangleIndex = contact.triangleIndex
        result.shape = contact.geom.shape
        result.actor = contact.geom.actor
    ELSE:
        result.grounded = FALSE

STEP 12.1.4: Return result
    RETURN result
```

### 12.2 Ground Snap

```
function snapToGround(volume: SweptVolume, maxSnapDistance: float) -> bool:

STEP 12.2.1: Probe for ground
    ground = probeGround(volume, maxSnapDistance)
    
    IF NOT ground.grounded:
        RETURN FALSE
    
    IF NOT ground.walkable:
        RETURN FALSE  # Don't snap to non-walkable slopes

STEP 12.2.2: Compute snap delta
    snapDelta = ground.groundDistance - CONTACT_OFFSET
    
    IF snapDelta <= 0:
        RETURN FALSE  # Already on ground

STEP 12.2.3: Apply snap
    volume.center = volume.center - upDirection * snapDelta
    RETURN TRUE
```

---

## 13. Moving Platform Support

### 13.1 Platform Tracking State

```
struct PlatformTrackingState:
    touchedActor: Actor*
    touchedShape: Shape*
    touchedObstacleHandle: uint32
    touchedPosShape_World: Vector3
    touchedPosShape_Local: Vector3
    touchedPosObstacle_World: Vector3
    touchedPosObstacle_Local: Vector3
```

### 13.2 Platform Position Update

```
function updatePlatformPosition(state: PlatformTrackingState, 
                                 OUT deltaPosition: Vector3) -> bool:

STEP 13.2.1: Check if standing on tracked object
    IF state.touchedActor == NULL AND state.touchedObstacleHandle == INVALID:
        deltaPosition = Vector3(0,0,0)
        RETURN FALSE

STEP 13.2.2: Get current transform of touched object
    IF state.touchedActor != NULL:
        currentTransform = state.touchedShape.globalPose
    ELSE:
        currentTransform = getObstacleTransform(state.touchedObstacleHandle)

STEP 13.2.3: Compute new world position from local position
    newWorldPos = currentTransform * state.touchedPosShape_Local

STEP 13.2.4: Compute delta
    deltaPosition = newWorldPos - state.touchedPosShape_World

STEP 13.2.5: Update tracking state
    state.touchedPosShape_World = newWorldPos
    
    RETURN TRUE
```

### 13.3 Apply Platform Movement Before Character Move

```
function applyPlatformMovement(controller: Controller, dt: float):

STEP 13.3.1: Get platform delta
    deltaPos = Vector3(0,0,0)
    hasPlatform = updatePlatformPosition(controller.platformState, OUT deltaPos)
    
    IF NOT hasPlatform:
        RETURN

STEP 13.3.2: Apply delta to controller position
    controller.position = controller.position + deltaPos

STEP 13.3.3: Set standing-on-moving flag for movement decomposition
    controller.standingOnMoving = TRUE
```

---

## 14. Edge Cases and Guards

### 14.1 Zero Direction Guard

```
# At start of any sweep function:
IF magnitude(direction) < EPSILON_DISTANCE:
    RETURN without movement
```

### 14.2 Degenerate Normal Guard

```
# When using a contact normal:
IF magnitude(normal) < EPSILON_NORMAL:
    normal = upDirection  # Fallback to up
ELSE:
    normal = normalize(normal)
```

### 14.3 Parallel Plane Guard (Crease)

```
# When two planes might form a crease:
creaseDir = cross(normal1, normal2)
IF magnitude(creaseDir) < EPSILON_NORMAL:
    # Planes are parallel - movement blocked in this direction
    RETURN Vector3(0,0,0)
```

### 14.4 Infinite Loop Guard

```
# In sweep iteration loop:
iterationCount = 0
MAX_SAFETY_ITERATIONS = 100  # Hard limit beyond maxIter

WHILE iterationCount < MAX_SAFETY_ITERATIONS:
    iterationCount += 1
    # ... sweep logic ...
    
IF iterationCount >= MAX_SAFETY_ITERATIONS:
    LOG_WARNING("Sweep loop exceeded safety limit")
```

### 14.5 Position Delta Sanity Check

```
# After completing movement:
totalDelta = magnitude(finalPosition - startPosition)
maxExpectedDelta = magnitude(originalDirection) * 2.0  # Allow some overshoot from slides

IF totalDelta > maxExpectedDelta:
    LOG_WARNING("Unexpected large movement delta")
    # Optionally clamp or reject
```

### 14.6 NaN/Inf Guards

```
function isValidVector(v: Vector3) -> bool:
    RETURN isFinite(v.x) AND isFinite(v.y) AND isFinite(v.z)

function isValidFloat(f: float) -> bool:
    RETURN isFinite(f) AND NOT isNaN(f)

# Apply at critical points:
IF NOT isValidVector(newPosition):
    LOG_ERROR("Invalid position computed")
    newPosition = previousValidPosition
```

### 14.7 Stuck Detection

```
# Track position history for stuck detection:
IF magnitude(currentPosition - positionTwoFramesAgo) < EPSILON_DISTANCE:
    stuckFrameCount += 1
    IF stuckFrameCount > 10:
        LOG_WARNING("Character may be stuck")
        # Trigger emergency unstick (MTD with larger radius, teleport to navmesh, etc.)
ELSE:
    stuckFrameCount = 0
```

---

## 15. Critical Implementation Fixes

> **Purpose**: This section documents specific implementation details that are easy to get wrong. These are based on real bugs found when comparing custom implementations to PhysX CCT.

### 15.1 Sweep Distance Must Include Contact Offset

**Problem**: Sweeping for exactly the remaining distance misses collisions within skin-width.

**Wrong**:
```cpp
// BUG: May miss collisions that are within contactOffset distance
float sweepDist = remaining;
SceneQuery::SweepCapsule(mapId, cap, dir, sweepDist, hits);
```

**Correct**:
```cpp
// CORRECT: Sweep includes skin width to find all relevant collisions
float contactOffset = GetContactOffset(radius);
float sweepDist = remaining + contactOffset;
SceneQuery::SweepCapsule(mapId, cap, dir, sweepDist, hits);

// When processing hit, subtract contact offset from advance distance
float safeAdvance = max(0.0f, hit.distance - contactOffset);
```

### 15.2 MAX_ITERATIONS Must Be 10

**Problem**: Using fewer iterations causes characters to get stuck.

**Wrong**:
```cpp
static constexpr int MAX_SLIDE_ITERATIONS = 4;  // TOO FEW!
```

**Correct**:
```cpp
static constexpr int MAX_SLIDE_ITERATIONS = 10;  // PhysX default
```

**Why**: Complex geometry (tight corners, multiple angled surfaces) requires multiple iterations to find a valid slide path. With only 4 iterations, the algorithm often exhausts its budget before resolving the collision properly.

### 15.3 Sensor Sweep Must Be AFTER Side Pass

**Problem**: Placing sensor sweep before or during UP pass misses slope detection.

**Wrong**:
```cpp
// In ExecuteUpPass():
if (isAutoStep) {
    bool hasClimbable = PerformClimbingSensorSweep(...);  // WRONG LOCATION!
    if (!hasClimbable) return;
}
```

**Correct**:
```cpp
// In ExecuteSidePass(), AFTER the main side sweep:
if (constrainedClimbingMode && sideMagnitude < capsuleRadius) {
    Vector3 sensor = normalize(sideVector) * capsuleRadius;
    Vector3 savedPos = currentPosition;
    
    // Sensor sweep to detect slopes
    doSweepTest(volume, sensor, 1, collisions, SWEEP_PASS_SENSOR);
    
    // Check for non-walkable
    if (collisions > 0 && validateTriangleSide) {
        if (testSlope(contactNormal) && contactPointHeight > bottomZ + stepOffset) {
            flags |= HIT_NON_WALKABLE;
        }
    }
    
    currentPosition = savedPos;  // Don't apply sensor movement!
}
```

### 15.4 Contact Point Height for Slope Validation

**Problem**: Only checking normal angle misses edge cases where contact is above step threshold.

**Wrong**:
```cpp
// Only checking normal
bool walkable = (normal.z >= WALKABLE_SLOPE_LIMIT);
```

**Correct**:
```cpp
// PhysX also checks contact point height in constrained climbing mode
float contactPointHeight = dot(contact.worldPos, upDirection);
bool walkable = (normal.z >= WALKABLE_SLOPE_LIMIT);

if (constrainedClimbingMode && contactPointHeight > originalBottomZ + stepOffset) {
    walkable = false;  // Too high to step onto regardless of angle
}
```

### 15.5 DOWN Pass Iterations Vary by Mode

**Problem**: Always using 1 iteration for DOWN pass breaks slope sliding.

**Wrong**:
```cpp
// Always 1 iteration
SlideResult downResult = ExecuteDownPass(mapId, st, radius, height, decomposed, 
                                          clampedStepOffset, maxIter=1);
```

**Correct**:
```cpp
// Variable iterations based on walk experiment mode
int maxIterDown = 1;  // Normal: just find ground
if (walkExperimentActive && nonWalkableMode == PREVENT_CLIMBING_AND_FORCE_SLIDING) {
    maxIterDown = MAX_ITERATIONS;  // Allow sliding down slopes
}
SlideResult downResult = ExecuteDownPass(mapId, st, radius, height, decomposed,
                                          clampedStepOffset, maxIterDown);
```

### 15.6 Normalize Response Flag in Walk Experiment

**Problem**: Collision response behaves differently during walk experiment.

**Wrong**:
```cpp
// Same response for all cases
collisionResponse(targetPos, currentPos, dir, normal, 0.0f, 1.0f, false);
```

**Correct**:
```cpp
// Walk experiment uses normalized response
bool normalizeResponse = (flags & STF_WALK_EXPERIMENT) != 0;
collisionResponse(targetPos, currentPos, dir, normal, 0.0f, 1.0f, normalizeResponse);

// In collisionResponse, when normalize=true:
if (normalize) {
    normalCompo = normalize(normalCompo);   // Unit length
    tangentCompo = normalize(tangentCompo); // Unit length
}
```

### 15.7 Temporal Box Must Include All Factors

**Problem**: Temporal box computation misses geometry if factors are omitted.

**Wrong**:
```cpp
AABB temporalBox;
temporalBox.min = position - Vector3(radius, radius, halfHeight);
temporalBox.max = position + direction + Vector3(radius, radius, halfHeight);
```

**Correct**:
```cpp
AABB temporalBox;
Vector3 extents(radius + contactOffset, radius + contactOffset, halfHeight + contactOffset);

// Start position bounds
Vector3 startMin = position - extents;
Vector3 startMax = position + extents;

// End position bounds
Vector3 endMin = position + direction - extents;
Vector3 endMax = position + direction + extents;

// Union
temporalBox.min = min(startMin, endMin);
temporalBox.max = max(startMax, endMax);

// Add max jump height for vertical expansion (caching optimization)
temporalBox.max.z += maxJumpHeight;

// Apply growth factor for better cache coherence
temporalBox = scale(temporalBox, VOLUME_GROWTH);
```

---

## Appendix A: Coordinate System Notes

### A.1 PhysX Default

- Y-up coordinate system
- `upDirection = (0, 1, 0)`

### A.2 WoW/Internal

- Z-up coordinate system  
- `upDirection = (0, 0, 1)`

### A.3 Conversion

When adapting PhysX code:
- Replace `direction[1]` or `.y` with `.z` for vertical
- Replace `upDirection = (0,1,0)` with `upDirection = (0,0,1)`
- Quaternion rotations may need axis adjustment

---

## Appendix B: Pseudocode Conventions

| Symbol | Meaning |
|--------|---------|
| `OUT param` | Output parameter (modified by function) |
| `->` | Returns |
| `|x|` or `magnitude(x)` | Vector magnitude |
| `dot(a,b)` | Dot product |
| `cross(a,b)` | Cross product |
| `normalize(v)` | Unit vector |
| `decompose(v, axis)` | Split into parallel and perpendicular components |

---

## Appendix C: Quick Reference - Function Call Order

```
1. Controller::move(displacement, minDist, dt, filters)
   │
   ├─2. Create SweptCapsule from position
   │
   ├─3. SweepTest::moveCharacter(volume, disp, ...)
   │    │
   │    ├─4. decomposeMovement(disp, up, stepOffset, ...)
   │    │
   │    ├─5. updateTouchedGeoms(initialBounds, filters)  [CACHE]
   │    │
   │    ├─6. doSweepTest(volume, upVector, ...)         [UP PASS]
   │    │    └─ collideGeoms() loop with collisionResponse()
   │    │
   │    ├─7. doSweepTest(volume, sideVector, ...)       [SIDE PASS]
   │    │    └─ collideGeoms() loop with collisionResponse()
   │    │
   │    ├─8. doSweepTest(volume, downVector, ...)       [DOWN PASS]
   │    │    └─ collideGeoms() loop with collisionResponse()
   │    │
   │    └─9. Slope validation & HIT_NON_WALKABLE check
   │
   ├─10. IF HIT_NON_WALKABLE: executeWalkExperiment()
   │     └─ Repeat steps 3-9 with modified params
   │
   └─11. Update controller position & return flags
```

---

*Document Version: 1.0*
*Based on: PhysX 4.x CCT Implementation*
*Target: World of Warcraft Headless Physics Service*
