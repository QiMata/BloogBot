// PhysicsMovement.cpp - Air and swim movement implementation
#include "PhysicsMovement.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsEngine.h"
#include "PhysicsHelpers.h"
#include "SceneQuery.h"
#include "VMapDefinitions.h"
#include "VMapLog.h"
#include <cmath>
#include <sstream>
#include <cfloat>

namespace PhysicsMovement
{

// Returns the effective terminal velocity based on movement flags.
// WoW.exe: MOVEFLAG_SAFE_FALL (0x20000000) selects swim/safe-fall terminal vel (7.0)
// instead of normal (60.148). Used by Slow Fall, Levitate, Safe Fall (rogue).
static float GetTerminalVelocity(uint32_t moveFlags)
{
    if (moveFlags & MOVEFLAG_SAFE_FALL)
        return PhysicsConstants::SAFE_FALL_TERMINAL_VELOCITY;
    return PhysicsConstants::TERMINAL_VELOCITY;
}

void ApplyGravity(MovementState& st, float dt, uint32_t moveFlags)
{
    const float termVel = GetTerminalVelocity(moveFlags);
    st.vz -= PhysicsConstants::GRAVITY * dt;
    if (st.vz < -termVel)
        st.vz = -termVel;
}

MovementIntent BuildMovementIntent(uint32_t moveFlags, float orientation)
{
    auto pure = PhysicsHelpers::BuildMovementIntent(moveFlags, orientation);
    MovementIntent intent{};
    intent.dir = pure.dir;
    intent.hasInput = pure.hasInput;
    intent.jumpRequested = pure.jumpRequested;
    return intent;
}

float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming)
{
    float speed;
    if (isSwimming) {
        speed = (input.moveFlags & MOVEFLAG_BACKWARD) ? input.swimBackSpeed : input.swimSpeed;
    } else if (input.moveFlags & MOVEFLAG_WALK_MODE) {
        speed = input.walkSpeed;
    } else if (input.moveFlags & MOVEFLAG_BACKWARD) {
        speed = input.runBackSpeed;
    } else {
        speed = input.runSpeed;
    }

    // WoW.exe CollisionResponse (0x7C5A20): when moving forward+strafe simultaneously,
    // all velocity components are multiplied by sin(45°) = 0.707107 to maintain constant
    // total speed (diagonal normalization). VA 0x0081DA54 = 0x3F3504F3.
    const bool hasForward  = (input.moveFlags & (MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD)) != 0;
    const bool hasStrafe   = (input.moveFlags & (MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT)) != 0;
    if (hasForward && hasStrafe)
        speed *= PhysicsConstants::SIN_45;

    return speed;
}

// WoW.exe ComputeFallDisplacement (VA 0x7C5E70): Two-phase fall displacement.
// If terminal velocity is reached mid-frame, splits into:
//   Phase 1: Acceleration from v0 to termVel (kinematic equation)
//   Phase 2: Constant velocity at termVel for remaining time
// This produces accurate positions for long falls where a single-equation
// approach diverges from the client.
static float ComputeFallDisplacement(float vz0, float dt, uint32_t moveFlags)
{
    const float termVel = GetTerminalVelocity(moveFlags);
    // vz0 is negative (downward), termVel is positive. Clamp speed magnitude.
    float speed0 = -vz0;  // positive falling speed
    if (speed0 > termVel) speed0 = termVel;

    float newSpeed = speed0 + PhysicsConstants::GRAVITY * dt;
    if (newSpeed <= termVel) {
        // Case 1: Normal freefall — standard kinematic equation
        // dz = -speed0 * dt - 0.5 * g * dt²  (negative = downward)
        return -(speed0 * dt + PhysicsConstants::HALF_GRAVITY * dt * dt);
    } else {
        // Case 2: Hit terminal velocity mid-frame — split into two phases
        float t_accel = (termVel - speed0) * PhysicsConstants::INV_GRAVITY;
        float d_accel = speed0 * t_accel + PhysicsConstants::HALF_GRAVITY * t_accel * t_accel;
        float t_const = dt - t_accel;
        float d_const = t_const * termVel;
        return -(d_accel + d_const);  // negative = downward
    }
}

void ProcessAirMovement(
    const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float dt,
    float speed)
{
    st.fallTime += dt;

    // Preserve horizontal velocity while falling (no air control)
    G3D::Vector3 startPos(st.x, st.y, st.z);
    const float vz0 = st.vz;

    // Two-phase fall displacement matching WoW.exe ComputeFallDisplacement
    const float dz = ComputeFallDisplacement(vz0, dt, input.moveFlags);

    // Apply gravity to velocity (with terminal clamp based on SAFE_FALL flag)
    ApplyGravity(st, dt, input.moveFlags);

    // Predict next position with constant horizontal velocity
    G3D::Vector3 endPos = startPos + G3D::Vector3(st.vx * dt, st.vy * dt, dz);

    // Commit position
    st.x = endPos.x;
    st.y = endPos.y;
    st.z = endPos.z;

    // When the caller provides exact velocity (TRUST_INPUT_VELOCITY), the trajectory
    // is already known — skip ground collision detection to avoid premature landing
    // on nearby slopes that the character is jumping over.
    const bool trustVel = (input.physicsFlags & PHYSICS_FLAG_TRUST_INPUT_VELOCITY) != 0;
    if (trustVel)
        return;

    // Skip ground collision detection during the ascending phase of a jump.
    // When vz0 > 0, the character is moving upward and the capsule naturally
    // overlaps ground geometry for the first several frames. Snapping to ground
    // during ascent would kill the jump velocity (vz → 0) immediately.
    // Ground collision only matters during descent (vz0 <= 0) when the character
    // is approaching a landing surface.
    if (vz0 > 1e-4f)
        return;

    // WoW.exe-style landing detection: query terrain at the predicted end position.
    // Uses GetGroundZ (barycentric point-in-triangle) matching WoW.exe's heightmap.
    // The character lands when endPos.z is at or below the terrain surface.
    constexpr float LANDING_TOLERANCE = 0.3f;

    float groundZ = SceneQuery::GetGroundZ(input.mapId, endPos.x, endPos.y,
        endPos.z + PhysicsConstants::STEP_HEIGHT,
        PhysicsConstants::STEP_HEIGHT + PhysicsConstants::STEP_DOWN_HEIGHT);

    if (VMAP::IsValidHeight(groundZ) && endPos.z <= groundZ + LANDING_TOLERANCE) {
        st.z = groundZ;
        st.vz = 0.0f;
        st.isGrounded = true;
        st.groundNormal = G3D::Vector3(0, 0, 1); // Approximate — GetGroundZ doesn't return normal
    }
    // Old capsule landing detection DELETED — using GetGroundZ (WoW.exe parity)
}

void ProcessSwimMovement(
    const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float dt,
    float speed)
{
    // Handles swim movement: horizontal and vertical (pitch) control.
    // Total velocity magnitude = swimSpeed regardless of pitch angle.
    // Horizontal speed = swimSpeed * cos(pitch), vertical = swimSpeed * sin(pitch).
    if (intent.hasInput) {
        float horizScale = std::cos(st.pitch);
        st.vx = intent.dir.x * speed * horizScale;
        st.vy = intent.dir.y * speed * horizScale;
    }
    else {
        st.vx = st.vy = 0;
    }

    float desiredVz = 0.0f;
    // Apply pitch-based vertical movement for forward and backward swimming.
    // Forward: pitch down -> descend, pitch up -> ascend
    // Backward: direction reversed (pitch down -> ascend, pitch up -> descend)
    // Strafe-only: no vertical movement (WoW keeps depth constant)
    if (intent.hasInput) {
        if (input.moveFlags & MOVEFLAG_FORWARD)
            desiredVz = std::sin(st.pitch) * speed;
        else if (input.moveFlags & MOVEFLAG_BACKWARD)
            desiredVz = -std::sin(st.pitch) * speed;
    }

    st.vz = desiredVz;
    st.x += st.vx * dt;
    st.y += st.vy * dt;
    st.z += st.vz * dt;
}

} // namespace PhysicsMovement
