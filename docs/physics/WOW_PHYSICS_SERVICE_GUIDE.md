# WoW Headless Physics Service - Stateless Implementation Guide

> **Purpose**: This document defines how to implement a stateless physics service for World of Warcraft using PhysX CCT principles. The service calculates one frame of physics given complete input state.

---

## Table of Contents

1. [Service Architecture](#1-service-architecture)
2. [Request/Response Contracts](#2-requestresponse-contracts)
3. [Frame Update Pipeline](#3-frame-update-pipeline)
4. [State Management Rules](#4-state-management-rules)
5. [Ground State Machine](#5-ground-state-machine)
6. [Swimming State Machine](#6-swimming-state-machine)
7. [Flying State Machine](#7-flying-state-machine)
8. [WoW-Specific Constants](#8-wow-specific-constants)
9. [Integration Checklist](#9-integration-checklist)
10. [Diagnostic Output](#10-diagnostic-output)

---

## 1. Service Architecture

### 1.1 Stateless Principle

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT                                   │
│  ┌──────────────┐                          ┌──────────────────┐ │
│  │ Game Logic   │──── PhysicsRequest ────► │ Physics Service  │ │
│  │              │◄─── PhysicsResponse ──── │ (Stateless)      │ │
│  └──────────────┘                          └──────────────────┘ │
│         │                                           │           │
│         ▼                                           ▼           │
│  ┌──────────────┐                          ┌──────────────────┐ │
│  │ State Store  │                          │ Scene Query      │ │
│  │ (per entity) │                          │ (geometry cache) │ │
│  └──────────────┘                          └──────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Key Design Rules

| Rule | Description |
|------|-------------|
| **No Internal State** | Service holds no entity state between calls |
| **Complete Input** | Request contains ALL information needed |
| **Deterministic** | Same input always produces same output |
| **Geometry Cache OK** | Scene geometry can be cached (read-only) |
| **No Side Effects** | Service does not modify world state |

### 1.3 Service Method Signature

```cpp
PhysicsResponse Step(const PhysicsRequest& request);
```

---

## 2. Request/Response Contracts

### 2.1 PhysicsRequest Structure

```cpp
struct PhysicsRequest {
    // === IDENTITY ===
    uint64_t entityId;              // For logging/debugging only
    uint32_t frameCounter;          // Monotonic frame number
    
    // === TIMING ===
    float deltaTime;                // Seconds since last frame (clamped 0.001 - 0.1)
    
    // === WORLD CONTEXT ===
    uint32_t mapId;                 // WoW map ID
    uint32_t sceneTimestamp;        // Increments when geometry changes
    
    // === CURRENT POSITION (from previous frame output) ===
    float x, y, z;                  // World position (WoW coordinates)
    float orientation;              // Facing direction (radians, 0 = North)
    float pitch;                    // Look angle (radians, 0 = horizontal)
    
    // === CURRENT VELOCITY (from previous frame output) ===
    float vx, vy, vz;               // World-space velocity
    
    // === CAPSULE PARAMETERS ===
    float capsuleRadius;            // Collision radius
    float capsuleHeight;            // Total capsule height
    
    // === MOVEMENT INTENT (player input this frame) ===
    MovementIntent intent;          // See 2.2
    
    // === MOVEMENT SPEEDS (from game systems) ===
    float walkSpeed;                // Base walk speed
    float runSpeed;                 // Base run speed  
    float swimSpeed;                // Swim speed
    float flightSpeed;              // Flight speed
    float fallSpeed;                // Terminal velocity (usually fixed)
    
    // === STATE FLAGS (from previous frame output) ===
    bool wasGrounded;               // Was on ground last frame
    bool wasSwimming;               // Was swimming last frame
    bool wasFlying;                 // Was flying last frame
    Vector3 lastGroundNormal;       // Ground normal if was grounded
    
    // === MOVEMENT FLAGS (game state) ===
    uint32_t moveFlags;             // MOVEFLAG_* bitmask
    
    // === PLATFORM STATE (if standing on moving object) ===
    bool onMovingPlatform;          // Standing on dynamic object
    uint32_t platformInstanceId;    // Instance ID of platform
    Vector3 platformVelocity;       // Platform's world velocity
    Vector3 localPosOnPlatform;     // Position in platform's local space
    
    // === TUNING (can be per-entity or global) ===
    float stepOffset;               // Auto-step height (default: 0.5)
    float slopeLimit;               // Walkable slope cosine (default: 0.707)
    float contactOffset;            // Skin width (default: 0.01)
};
```

### 2.2 MovementIntent Structure

```cpp
struct MovementIntent {
    // === INPUT STATE ===
    bool hasInput;                  // Any movement keys pressed
    
    // === DIRECTIONAL INPUT (normalized) ===
    float forwardBack;              // -1 (back) to +1 (forward)
    float leftRight;                // -1 (left) to +1 (right)
    float upDown;                   // -1 (down) to +1 (up) - for swimming/flying
    
    // === ACTIONS ===
    bool jumpRequested;             // Jump key pressed this frame
    bool descendRequested;          // Descend key (swimming/flying)
    bool autoRunActive;             // Auto-run enabled
    
    // === MODIFIERS ===
    bool walkMode;                  // Walk (not run)
    bool backpedaling;              // Moving backward
    bool strafing;                  // Strafing movement
};
```

### 2.3 PhysicsResponse Structure

```cpp
struct PhysicsResponse {
    // === RESULT POSITION ===
    float x, y, z;                  // New world position
    
    // === RESULT VELOCITY ===
    float vx, vy, vz;               // New world velocity
    
    // === RESULT ORIENTATION (unchanged usually) ===
    float orientation;
    float pitch;
    
    // === GROUND STATE ===
    bool isGrounded;                // Currently on walkable ground
    Vector3 groundNormal;           // Normal of ground surface (if grounded)
    float groundZ;                  // Z of ground contact point
    uint32_t groundTriangleIndex;   // Triangle index (for debugging)
    uint32_t groundInstanceId;      // Instance ID of ground geometry
    
    // === CONTACT TRACKING ===
    // ⚠️ CRITICAL: Track contact point height for slope validation
    float contactPointHeight;       // Z of contact point from collision
                                    // Used to check: contactPointHeight > bottomZ + stepOffset
    
    // === LIQUID STATE ===
    bool isSwimming;                // In swimmable liquid
    float liquidZ;                  // Surface Z of liquid (if any)
    uint32_t liquidType;            // WoW liquid type flags
    
    // === COLLISION FLAGS (PhysX-style) ===
    uint32_t collisionFlags;        // COLLISION_UP | COLLISION_SIDES | COLLISION_DOWN
    
    // === MOVEMENT FLAGS (updated) ===
    uint32_t moveFlags;             // Updated MOVEFLAG_* bitmask
    
    // === PLATFORM STATE (updated) ===
    uint32_t standingOnInstanceId;  // Instance we're standing on (0 if none)
    Vector3 localPosOnPlatform;     // Our position in platform's local space
    
    // === DIAGNOSTICS ===
    int iterationsUsed;             // Total sweep iterations this frame
    float distanceRequested;        // How far we tried to move
    float distanceMoved;            // How far we actually moved
    Vector3 depenetrationApplied;   // MTD correction applied (if any)
    bool hitNonWalkable;            // Landed on non-walkable slope
};
```

---

## 3. Frame Update Pipeline

### 3.1 Top-Level Step Function

```
function Step(request: PhysicsRequest) -> PhysicsResponse:

    STEP 3.1.1: Validate and clamp input
        dt = clamp(request.deltaTime, 0.001, 0.1)
        
        IF NOT isValidPosition(request.x, request.y, request.z):
            RETURN errorResponse("Invalid input position")

    STEP 3.1.2: Build initial movement state
        state = MovementState {
            position: Vector3(request.x, request.y, request.z),
            velocity: Vector3(request.vx, request.vy, request.vz),
            orientation: request.orientation,
            pitch: request.pitch,
            isGrounded: request.wasGrounded,
            isSwimming: request.wasSwimming,
            groundNormal: request.lastGroundNormal
        }

    STEP 3.1.3: Evaluate current liquid state
        liquidInfo = SceneQuery.EvaluateLiquidAt(request.mapId, state.position)

    STEP 3.1.4: Determine movement mode
        mode = DetermineMovementMode(request, state, liquidInfo)
        # mode ∈ {GROUNDED, AIRBORNE, SWIMMING, FLYING}

    STEP 3.1.5: Compute movement plan
        plan = ComputeMovementPlan(request, state, mode, dt)
        # plan contains: direction, speed, distance

    STEP 3.1.6: Apply platform movement (before character movement)
        IF request.onMovingPlatform:
            platformDelta = request.platformVelocity * dt
            state.position += platformDelta

    STEP 3.1.7: Execute movement based on mode
        SWITCH mode:
            CASE GROUNDED:
                ExecuteGroundMovement(request, state, plan, dt)
            CASE AIRBORNE:
                ExecuteAirMovement(request, state, plan, dt)
            CASE SWIMMING:
                ExecuteSwimMovement(request, state, plan, dt)
            CASE FLYING:
                ExecuteFlyingMovement(request, state, plan, dt)

    STEP 3.1.8: Final liquid evaluation
        finalLiquid = SceneQuery.EvaluateLiquidAt(request.mapId, state.position)
        HandleLiquidTransition(state, liquidInfo, finalLiquid)

    STEP 3.1.9: Compute output velocity
        IF dt > 0:
            outputVelocity = (state.position - originalPosition) / dt
        ELSE:
            outputVelocity = Vector3(0, 0, 0)

    STEP 3.1.10: Build and return response
        RETURN PhysicsResponse {
            x: state.position.x,
            y: state.position.y,
            z: state.position.z,
            vx: outputVelocity.x,
            vy: outputVelocity.y,
            vz: outputVelocity.z,
            isGrounded: state.isGrounded,
            groundNormal: state.groundNormal,
            isSwimming: finalLiquid.isSwimming,
            liquidZ: finalLiquid.level,
            collisionFlags: state.collisionFlags,
            ...
        }
```

### 3.2 Movement Mode Determination

```
function DetermineMovementMode(request: PhysicsRequest, state: MovementState,
                                liquid: LiquidInfo) -> MovementMode:

    STEP 3.2.1: Check for flying mode (GM flag or mount)
        IF request.moveFlags & MOVEFLAG_FLYING:
            RETURN MovementMode.FLYING

    STEP 3.2.2: Check for swimming
        IF liquid.isSwimming:
            RETURN MovementMode.SWIMMING

    STEP 3.2.3: Check for grounded vs airborne
        IF state.isGrounded:
            RETURN MovementMode.GROUNDED
        ELSE:
            RETURN MovementMode.AIRBORNE
```

### 3.3 Movement Plan Computation

```
function ComputeMovementPlan(request: PhysicsRequest, state: MovementState,
                              mode: MovementMode, dt: float) -> MovementPlan:

    STEP 3.3.1: Compute base move direction from intent
        intent = request.intent
        
        IF NOT intent.hasInput:
            RETURN MovementPlan { direction: (0,0,0), speed: 0, distance: 0 }
        
        # Build local direction from WASD input
        localDir = Vector3(
            intent.forwardBack,    # +1 forward, -1 backward
            -intent.leftRight,     # +1 left, -1 right (WoW convention)
            0
        )
        
        IF magnitude(localDir) < EPSILON:
            RETURN MovementPlan { direction: (0,0,0), speed: 0, distance: 0 }

    STEP 3.3.2: Transform to world direction
        sinYaw = sin(state.orientation)
        cosYaw = cos(state.orientation)
        
        worldDir = Vector3(
            localDir.x * cosYaw - localDir.y * sinYaw,
            localDir.x * sinYaw + localDir.y * cosYaw,
            0
        )
        worldDir = normalize(worldDir)

    STEP 3.3.3: Determine speed
        IF intent.backpedaling:
            speed = request.walkSpeed  # Backpedal is always walk speed
        ELSE IF intent.walkMode:
            speed = request.walkSpeed
        ELSE:
            speed = request.runSpeed

    STEP 3.3.4: Apply mode-specific speed modifiers
        SWITCH mode:
            CASE SWIMMING:
                speed = request.swimSpeed
            CASE FLYING:
                speed = request.flightSpeed

    STEP 3.3.5: Compute distance
        distance = speed * dt

    STEP 3.3.6: Return plan
        RETURN MovementPlan {
            direction: worldDir,
            speed: speed,
            distance: distance,
            hasInput: TRUE
        }
```

---

## 4. State Management Rules

### 4.1 What the Client Must Track (Between Frames)

```cpp
struct EntityPhysicsState {
    // Position (from PhysicsResponse)
    float x, y, z;
    
    // Velocity (from PhysicsResponse)  
    float vx, vy, vz;
    
    // Orientation (client-controlled, not physics)
    float orientation;
    float pitch;
    
    // State flags (from PhysicsResponse)
    bool isGrounded;
    bool isSwimming;
    Vector3 groundNormal;
    
    // Platform tracking (from PhysicsResponse)
    uint32_t standingOnInstanceId;
    Vector3 localPosOnPlatform;
    
    // Frame counter
    uint32_t lastFrameCounter;
};
```

### 4.2 State Flow Between Frames

```
Frame N:
    Request.wasGrounded = ClientState.isGrounded
    Request.wasSwimming = ClientState.isSwimming
    Request.lastGroundNormal = ClientState.groundNormal
    
    Response = PhysicsService.Step(Request)
    
    ClientState.x = Response.x
    ClientState.y = Response.y
    ClientState.z = Response.z
    ClientState.vx = Response.vx
    ClientState.vy = Response.vy
    ClientState.vz = Response.vz
    ClientState.isGrounded = Response.isGrounded
    ClientState.isSwimming = Response.isSwimming
    ClientState.groundNormal = Response.groundNormal
    ClientState.standingOnInstanceId = Response.standingOnInstanceId
    ClientState.localPosOnPlatform = Response.localPosOnPlatform
    ClientState.lastFrameCounter = Request.frameCounter
```

### 4.3 Handling Frame Skips

```
function HandleFrameSkip(lastFrame: uint32, currentFrame: uint32, 
                          dt: float) -> (int, float):
    
    skippedFrames = currentFrame - lastFrame - 1
    
    IF skippedFrames > 0:
        # Option 1: Process multiple small steps
        IF skippedFrames <= 5:
            subDt = dt / (skippedFrames + 1)
            RETURN (skippedFrames + 1, subDt)
        
        # Option 2: Single large step (may cause tunneling)
        ELSE:
            LOG_WARNING("Large frame skip, physics may be inaccurate")
            RETURN (1, clamp(dt, 0.001, 0.1))
    
    RETURN (1, dt)
```

---

## 5. Ground State Machine

### 5.1 Ground Movement Execution

```
function ExecuteGroundMovement(request: PhysicsRequest, INOUT state: MovementState,
                                plan: MovementPlan, dt: float):

    STEP 5.1.1: Handle jump request
        IF request.intent.jumpRequested:
            state.velocity.z = JUMP_VELOCITY
            state.isGrounded = FALSE
            ExecuteAirMovement(request, state, plan, dt)
            RETURN

    STEP 5.1.2: Handle zero movement
        IF plan.distance < MIN_MOVE_DISTANCE:
            # Still need to check for ground under us
            PerformVerticalPlacement(request, state, dt)
            RETURN

    STEP 5.1.3: Execute PhysX-style three-pass movement
        collisionFlags = PerformThreePassMove(
            request, state,
            request.capsuleRadius, request.capsuleHeight,
            plan.direction, plan.distance, dt,
            request.stepOffset
        )

    STEP 5.1.4: Check for non-walkable slope landing
        IF state.isGrounded AND NOT IsWalkable(state.groundNormal):
            # Trigger walk experiment - retry with stepOffset = 0
            savedState = state
            PerformThreePassMove(
                request, state,
                request.capsuleRadius, request.capsuleHeight,
                plan.direction, plan.distance, dt,
                stepOffsetOverride = 0.0  # Cancel auto-step
            )
            
            # If still on non-walkable, keep retry result
            # Otherwise revert (walk experiment made it worse)
            IF NOT state.isGrounded OR IsWalkable(state.groundNormal):
                # Walk experiment succeeded or made us airborne
                # Keep new state
            ELSE:
                # Still on non-walkable - keep retry result anyway
                # (prevents climbing steep slopes)

    STEP 5.1.5: Apply gravity if became airborne
        IF NOT state.isGrounded:
            state.velocity.z -= GRAVITY * dt
```

### 5.2 Three-Pass Move Implementation

```
function PerformThreePassMove(request: PhysicsRequest, INOUT state: MovementState,
                               radius: float, height: float,
                               direction: Vector3, distance: float, dt: float,
                               stepOffsetOverride: float = -1) -> uint32:

    STEP 5.2.1: Setup
        upDirection = Vector3(0, 0, 1)
        originalHeight = state.position.z
        originalBottomZ = originalHeight - (height / 2)
        collisionFlags = 0
        
        stepOffset = stepOffsetOverride >= 0 ? stepOffsetOverride : request.stepOffset

    STEP 5.2.2: Decompose movement
        decomposed = DecomposeMovement(direction * distance, upDirection, stepOffset,
                                       request.intent.jumpRequested, request.onMovingPlatform)

    STEP 5.2.3: Initial volume query (cache geometry)
        fullDirection = direction * distance
        temporalBounds = ComputeTemporalBounds(state.position, fullDirection, stepOffset, radius, height)
        EnsureGeometryCached(request.mapId, temporalBounds)

    STEP 5.2.4: Execute UP pass
        IF NOT walkExperimentActive AND magnitude(decomposed.upVector) > MIN_MOVE_DISTANCE:
            upResult = SweepMove(request, state, radius, height, 
                                 decomposed.upVector, maxIterations = 1)
            
            IF upResult.hitCount > 0:
                collisionFlags |= COLLISION_UP
                # Clamp step offset to actual achieved lift
                actualDelta = state.position.z - originalHeight
                IF actualDelta < stepOffset:
                    stepOffset = actualDelta

    STEP 5.2.5: Execute SIDE pass
        IF magnitude(decomposed.sideVector) > MIN_MOVE_DISTANCE:
            # ⚠️ CRITICAL: Sweep distance must include contact offset
            # This ensures collisions within skin-width are detected
            contactOffset = GetContactOffset(radius)
            
            sideResult = SweepMove(request, state, radius, height,
                                   decomposed.sideVector, maxIterations = MAX_ITERATIONS)
            
            IF sideResult.hitCount > 0:
                collisionFlags |= COLLISION_SIDES
        
        # =========================================================================
        # ⚠️ CRITICAL: Constrained Climbing Sensor Sweep - MUST be AFTER side pass
        # =========================================================================
        # When lateral movement is smaller than capsule radius, the normal sweep
        # may miss steep slopes. Extend a sensor sweep to capsule radius to detect
        # these. This happens AFTER the main side pass, not before or during UP.
        # =========================================================================
        IF constrainedClimbingMode:
            sideMagnitude = magnitude(decomposed.sideVector)
            IF sideMagnitude > 0 AND sideMagnitude < radius:
                sensorDir = normalize(decomposed.sideVector)
                sensorDist = radius
                savedPosition = state.position
                
                # Sensor sweep - detect only, don't apply movement
                sensorResult = SweepMove(request, state, radius, height,
                                         sensorDir * sensorDist, maxIterations = 1)
                
                # Check for non-walkable slope
                IF sensorResult.hitCount > 0:
                    # ⚠️ Track contact point height for slope validation
                    contactPointHeight = sensorResult.contactPointZ
                    IF testSlope(sensorResult.normal, slopeLimit):
                        IF contactPointHeight > originalBottomZ + stepOffset:
                            SET flag HIT_NON_WALKABLE
                
                # IMPORTANT: Restore position - sensor doesn't move character
                state.position = savedPosition

    STEP 5.2.6: Execute DOWN pass
        # =========================================================================
        # ⚠️ CRITICAL: Variable max iterations for DOWN pass
        # =========================================================================
        # - Normal case: maxIter=1 (just find ground)
        # - Walk experiment with FORCE_SLIDING: maxIter=10 (allow slope sliding)
        # Using always 1 iteration breaks slope sliding behavior!
        # =========================================================================
        IF walkExperimentActive AND nonWalkableMode == PREVENT_CLIMBING_AND_FORCE_SLIDING:
            maxIterDown = MAX_ITERATIONS  # Allow sliding down slopes
        ELSE:
            maxIterDown = 1               # Just find ground
        
        # Undo step offset injection
        downVector = decomposed.downVector
        IF decomposed.hasSideMovement:
            downVector -= upDirection * stepOffset
        
        # Clear ground tracking before down pass
        state.isGrounded = FALSE
        state.groundNormal = Vector3(0, 0, 1)
        
        IF magnitude(downVector) > MIN_MOVE_DISTANCE:
            downResult = SweepMove(request, state, radius, height,
                                   downVector, maxIterations = maxIterDown,
                                   OUT touchedActor, OUT touchedNormal)
            
            IF downResult.hitCount > 0:
                IF NOT decomposed.isMovingUp:  # Don't flag collision when jumping
                    collisionFlags |= COLLISION_DOWN
                
                state.isGrounded = TRUE
                state.groundNormal = touchedNormal
                
                # ⚠️ CRITICAL: Track contact point height for slope validation
                state.contactPointHeight = downResult.contactPointZ

    STEP 5.2.7: Return collision flags
        RETURN collisionFlags
```

### 5.3 Vertical Placement (Idle on Ground)

```
function PerformVerticalPlacement(request: PhysicsRequest, INOUT state: MovementState,
                                   dt: float):

    STEP 5.3.1: Probe downward for ground
        probeDistance = STEP_DOWN_HEIGHT + CONTACT_OFFSET
        downDir = Vector3(0, 0, -1)
        
        groundHit = SweepCapsule(request.mapId, state.position, 
                                  request.capsuleRadius, request.capsuleHeight,
                                  downDir, probeDistance)

    STEP 5.3.2: Process result
        IF groundHit.found:
            IF groundHit.distance > CONTACT_OFFSET:
                # Snap down to ground
                state.position.z -= (groundHit.distance - CONTACT_OFFSET)
            
            state.isGrounded = TRUE
            state.groundNormal = groundHit.normal
        ELSE:
            # No ground below - start falling
            state.isGrounded = FALSE
            state.velocity.z -= GRAVITY * dt
```

---

## 6. Swimming State Machine

### 6.1 Swimming Movement Execution

```
function ExecuteSwimMovement(request: PhysicsRequest, INOUT state: MovementState,
                              plan: MovementPlan, dt: float):

    STEP 6.1.1: Clear ground state
        state.isGrounded = FALSE

    STEP 6.1.2: Compute 3D swim direction
        intent = request.intent
        
        # Include vertical input for swimming
        localDir = Vector3(
            intent.forwardBack,
            -intent.leftRight,
            intent.upDown
        )
        
        IF magnitude(localDir) > EPSILON:
            localDir = normalize(localDir)
        ELSE:
            localDir = Vector3(0, 0, 0)

    STEP 6.1.3: Apply pitch to forward component
        # When looking up/down, swimming follows that direction
        IF abs(intent.forwardBack) > EPSILON:
            pitchInfluence = sin(state.pitch) * intent.forwardBack
            localDir.z += pitchInfluence
            localDir = normalize(localDir)

    STEP 6.1.4: Transform to world direction
        sinYaw = sin(state.orientation)
        cosYaw = cos(state.orientation)
        
        worldDir = Vector3(
            localDir.x * cosYaw - localDir.y * sinYaw,
            localDir.x * sinYaw + localDir.y * cosYaw,
            localDir.z
        )

    STEP 6.1.5: Apply movement with collision
        IF magnitude(worldDir) > EPSILON AND plan.speed > 0:
            distance = plan.speed * dt
            SweepMove(request, state, 
                      request.capsuleRadius, request.capsuleHeight,
                      normalize(worldDir), distance, maxIterations = MAX_ITERATIONS)

    STEP 6.1.6: Apply water surface constraint
        liquidInfo = SceneQuery.EvaluateLiquidAt(request.mapId, state.position)
        IF liquidInfo.hasLevel:
            # Don't let head go above water surface (unless jumping out)
            maxZ = liquidInfo.level - WATER_SURFACE_OFFSET
            IF state.position.z > maxZ AND NOT intent.jumpRequested:
                state.position.z = maxZ

    STEP 6.1.7: Handle swim-to-jump
        IF intent.jumpRequested:
            # Check if we're at surface
            IF state.position.z >= liquidInfo.level - WATER_SURFACE_OFFSET:
                state.velocity.z = SWIM_JUMP_VELOCITY
                state.isSwimming = FALSE
```

---

## 7. Flying State Machine

### 7.1 Flying Movement Execution

```
function ExecuteFlyingMovement(request: PhysicsRequest, INOUT state: MovementState,
                                plan: MovementPlan, dt: float):

    STEP 7.1.1: Clear ground state
        state.isGrounded = FALSE
        state.isSwimming = FALSE

    STEP 7.1.2: Compute 3D flight direction
        intent = request.intent
        
        # Flying uses pitch directly for vertical component
        IF intent.hasInput:
            # Forward/back follows pitch angle
            forward = Vector3(
                cos(state.pitch) * cos(state.orientation),
                cos(state.pitch) * sin(state.orientation),
                sin(state.pitch)
            ) * intent.forwardBack
            
            # Strafe is always horizontal
            strafe = Vector3(
                -sin(state.orientation),
                cos(state.orientation),
                0
            ) * intent.leftRight
            
            worldDir = normalize(forward + strafe)
        ELSE:
            worldDir = Vector3(0, 0, 0)

    STEP 7.1.3: Apply movement with collision
        IF magnitude(worldDir) > EPSILON AND plan.speed > 0:
            distance = plan.speed * dt
            SweepMove(request, state,
                      request.capsuleRadius, request.capsuleHeight,
                      worldDir, distance, maxIterations = MAX_ITERATIONS)

    STEP 7.1.4: Handle descend input
        IF intent.descendRequested:
            descendDist = plan.speed * dt
            SweepMove(request, state,
                      request.capsuleRadius, request.capsuleHeight,
                      Vector3(0, 0, -1), descendDist, maxIterations = 1)
```

---

## 8. WoW-Specific Constants

### 8.1 Movement Constants

```cpp
namespace WoWPhysics {
    // Gravity
    constexpr float GRAVITY = 19.29f;            // Units/sec² (WoW specific)
    
    // Jumping
    constexpr float JUMP_VELOCITY = 7.96f;       // Initial upward velocity
    constexpr float SWIM_JUMP_VELOCITY = 4.0f;   // Jump out of water
    
    // Terminal velocity
    constexpr float TERMINAL_VELOCITY = 60.0f;   // Max fall speed
    
    // Step heights
    constexpr float STEP_OFFSET = 0.5f;          // Auto-step height
    constexpr float STEP_DOWN_HEIGHT = 0.6f;     // How far to look for ground
    
    // Slopes
    constexpr float WALKABLE_SLOPE_LIMIT = 0.707f;  // cos(45°)
    constexpr float SLIDE_SLOPE_LIMIT = 0.5f;       // cos(60°) - start sliding
    
    // Collision
    constexpr float CONTACT_OFFSET = 0.01f;      // Skin width
    constexpr float MIN_MOVE_DISTANCE = 0.001f;  // Below this, don't move
    
    // Water
    constexpr float WATER_SURFACE_OFFSET = 0.5f; // How deep before swimming
    constexpr float WATER_LEVEL_DELTA = 1.5f;    // Snap distance when entering water
    
    // =========================================================================
    // ⚠️ CRITICAL: Iteration counts - DO NOT REDUCE
    // =========================================================================
    constexpr int MAX_SLIDE_ITERATIONS = 10;     // PhysX default - MUST be 10, not 4!
}
```

> **⚠️ IMPLEMENTATION WARNING**: The `MAX_SLIDE_ITERATIONS` constant MUST be 10.
> Using fewer iterations (e.g., 4) will cause characters to get stuck in complex
> geometry, tight corners, or when sliding along multiple angled surfaces.
```

### 8.2 Move Flags (Bitmask)

```cpp
enum MoveFlags : uint32_t {
    MOVEFLAG_NONE           = 0x00000000,
    MOVEFLAG_FORWARD        = 0x00000001,
    MOVEFLAG_BACKWARD       = 0x00000002,
    MOVEFLAG_STRAFE_LEFT    = 0x00000004,
    MOVEFLAG_STRAFE_RIGHT   = 0x00000008,
    MOVEFLAG_TURN_LEFT      = 0x00000010,
    MOVEFLAG_TURN_RIGHT     = 0x00000020,
    MOVEFLAG_PITCH_UP       = 0x00000040,
    MOVEFLAG_PITCH_DOWN     = 0x00000080,
    MOVEFLAG_WALK_MODE      = 0x00000100,
    MOVEFLAG_JUMPING        = 0x00002000,
    MOVEFLAG_FALLINGFAR     = 0x00004000,
    MOVEFLAG_SWIMMING       = 0x00200000,
    MOVEFLAG_FLYING         = 0x02000000,
    MOVEFLAG_ROOT           = 0x00000400,    // Cannot move (rooted)
    // ... additional flags
};
```

### 8.3 Liquid Types

```cpp
enum LiquidType : uint32_t {
    LIQUID_TYPE_NO_WATER    = 0x00,
    LIQUID_TYPE_WATER       = 0x01,
    LIQUID_TYPE_OCEAN       = 0x02,
    LIQUID_TYPE_MAGMA       = 0x04,
    LIQUID_TYPE_SLIME       = 0x08,
    LIQUID_TYPE_DARK_WATER  = 0x10,
};
```

---

## 9. Integration Checklist

### 9.1 Before First Frame

```
□ Initialize SceneQuery (VMAP + terrain loaders)
□ Ensure map data is available for target mapId
□ Verify capsule parameters are valid (radius > 0, height > 2*radius)
□ Initialize client-side EntityPhysicsState to valid position
□ Set initial wasGrounded = FALSE, wasSwimming = FALSE
```

### 9.2 Each Frame

```
□ Clamp deltaTime to valid range [0.001, 0.1]
□ Increment frameCounter monotonically
□ Copy previous frame's output state to new request's "was*" fields
□ Populate MovementIntent from input system
□ Call PhysicsService.Step()
□ Store response in EntityPhysicsState
□ Apply position to game entity
□ Update camera / visuals
```

### 9.3 Error Handling

```
□ Handle invalid position response (NaN, huge values)
□ Handle stuck detection (no movement over multiple frames)
□ Handle fall-through-world (z far below terrain)
□ Log and report excessive collision iterations
□ Handle missing geometry gracefully
```

---

## 10. Diagnostic Output

### 10.1 Per-Frame Diagnostic Structure

```cpp
struct PhysicsDiagnostics {
    // Timing
    float computeTimeMs;
    
    // Queries
    int broadphaseTriangles;
    int narrowphaseTests;
    int triangleCacheHits;
    
    // Movement
    Vector3 requestedDisplacement;
    Vector3 actualDisplacement;
    float movementRatio;         // actual / requested
    
    // Collisions
    int upPassIterations;
    int sidePassIterations;
    int downPassIterations;
    int totalIterations;
    
    // Penetration
    bool hadInitialPenetration;
    float maxPenetrationDepth;
    Vector3 mtdApplied;
    
    // State transitions
    bool groundedChanged;
    bool swimmingChanged;
    bool wasWalkExperiment;
};
```

### 10.2 Logging Levels

```cpp
enum PhysicsLogLevel {
    PHYS_LOG_NONE = 0,
    PHYS_LOG_ERROR = 1,      // Errors only
    PHYS_LOG_WARN = 2,       // + warnings
    PHYS_LOG_INFO = 3,       // + per-frame summary
    PHYS_LOG_DEBUG = 4,      // + pass details
    PHYS_LOG_TRACE = 5,      // + per-iteration details
};
```

### 10.3 Key Log Points

```
[Frame N] Step start: pos=(x,y,z) mode=GROUNDED intent=FORWARD
[Frame N] Movement plan: dir=(dx,dy,dz) dist=D speed=S
[Frame N] UP pass: moved=M hit=H iterations=I
[Frame N] SIDE pass: moved=M hit=H iterations=I
[Frame N] DOWN pass: moved=M hit=H iterations=I grounded=G
[Frame N] Walk experiment triggered: retrying with stepOffset=0
[Frame N] Step complete: pos=(x,y,z) flags=0xF diag={...}
```

---

## Appendix A: Common Issues

### A.1 Character Falls Through Ground

**Symptoms**: Character teleports below terrain
**Causes**:
- deltaTime too large (tunneling)
- No ground geometry loaded
- Capsule too small for terrain resolution

**Fixes**:
- Clamp deltaTime to max 0.1 (100ms)
- Use sub-stepping for large deltas
- Verify EnsureMapLoaded() before Step()
- Increase capsule radius

### A.2 Character Gets Stuck on Slopes

**Symptoms**: Can't move up slopes that look walkable
**Causes**:
- slopeLimit too strict
- stepOffset too small
- Walk experiment not triggering

**Fixes**:
- Verify slopeLimit = 0.707 (45°)
- Increase stepOffset to 0.5+
- Check for HIT_NON_WALKABLE flag handling

### A.3 Jittering Against Walls

**Symptoms**: Character vibrates when pressing into wall
**Causes**:
- contactOffset too small
- Quake2 check missing
- Collision response overshoot

**Fixes**:
- Increase contactOffset to 0.01+
- Implement dot(currentDir, originalDir) <= 0 break
- Clamp slide velocity to original magnitude

### A.4 Swimming Breaks at Water Surface

**Symptoms**: Pops in/out of swimming rapidly
**Causes**:
- WATER_SURFACE_OFFSET too small
- No hysteresis in swim detection
- Liquid evaluation jitter

**Fixes**:
- Increase WATER_SURFACE_OFFSET
- Use wasSwimming to resist mode changes
- Smooth liquid level over frames

### A.5 Character Gets Stuck in Corners

**Symptoms**: Character stops moving in tight corners even with input
**Causes**:
- MAX_SLIDE_ITERATIONS too low (e.g., 4 instead of 10)
- Crease direction calculation errors

**Fixes**:
- **⚠️ CRITICAL**: Set `MAX_SLIDE_ITERATIONS = 10` (PhysX default)
- Verify crease direction aligns with movement intent
- Check that crease isn't blocked by existing constraints

### A.6 Climbs Non-Walkable Slopes

**Symptoms**: Character walks up slopes that should cause sliding
**Causes**:
- Sensor sweep in wrong location (before UP pass instead of after SIDE pass)
- Missing contact point height check
- Variable DOWN pass iterations not implemented

**Fixes**:
- Move sensor sweep to AFTER the SIDE pass execution
- Add `contactPointHeight > originalBottomZ + stepOffset` check
- Use `maxIterDown = MAX_ITERATIONS` during walk experiment

### A.7 Misses Collisions Near Surfaces

**Symptoms**: Character clips through thin walls or slides into geometry
**Causes**:
- Sweep distance doesn't include contact offset
- Contact offset not subtracted from advance distance

**Fixes**:
```cpp
// WRONG
float sweepDist = remaining;
// RIGHT
float sweepDist = remaining + contactOffset;

// WRONG
float advance = hit.distance;
// RIGHT  
float advance = max(0.0f, hit.distance - contactOffset);
```

### A.8 Sensor Sweep Applies Movement

**Symptoms**: Character unexpectedly moves forward when climbing detection runs
**Causes**:
- Sensor sweep result is being applied instead of just used for detection

**Fixes**:
- Save position before sensor sweep
- Restore position after sensor sweep completes
- Only use sensor result to set HIT_NON_WALKABLE flag

---

*Document Version: 1.0*
*Target: World of Warcraft 1.12.x Headless Client*
*Based on: PhysX 4.x CCT Architecture*
