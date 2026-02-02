// PhysicsMovement.h - Air and swim movement processing
#pragma once

#include "Vector3.h"
#include "PhysicsBridge.h"
#include <cstdint>

namespace PhysicsMovement
{
    // Movement state for air/swim processing
    struct MovementState
    {
        float x, y, z;
        float vx, vy, vz;
        float orientation;
        float pitch;
        bool isGrounded;
        bool isSwimming;
        float fallTime;
        G3D::Vector3 groundNormal;
    };

    // Movement intent from input flags
    struct MovementIntent
    {
        G3D::Vector3 dir;      // Normalized planar desired direction (xy, z=0)
        bool hasInput;         // Any movement key pressed
        bool jumpRequested;    // Jump flag present
    };

    /// Processes air movement: gravity, air control, ground detection.
    /// Updates state position based on falling physics.
    void ProcessAirMovement(
        const PhysicsInput& input,
        const MovementIntent& intent,
        MovementState& st,
        float dt,
        float speed);

    /// Processes swim movement: horizontal and vertical (pitch) control.
    /// Updates state position based on swimming physics.
    void ProcessSwimMovement(
        const PhysicsInput& input,
        const MovementIntent& intent,
        MovementState& st,
        float dt,
        float speed);

    /// Applies gravity to vertical velocity with terminal velocity clamp.
    void ApplyGravity(MovementState& st, float dt);

    /// Builds movement intent from input flags and orientation.
    MovementIntent BuildMovementIntent(uint32_t moveFlags, float orientation);

    /// Calculates movement speed based on input flags and swim state.
    float CalculateMoveSpeed(const PhysicsInput& input, bool isSwimming);

} // namespace PhysicsMovement
