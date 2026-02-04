#pragma once

#include <cstdint>
#include <vector>
#include "Vector3.h"
#include "SceneQuery.h"

namespace PhysicsHelpers
{
    struct Intent
    {
        G3D::Vector3 dir;      // normalized planar desired direction (xy, z=0)
        bool hasInput = false;  // any movement key
        bool jumpRequested = false; // jump flag present
    };

    /// Holds computed movement plan from input flags.
    struct MovementPlan 
    { 
        G3D::Vector3 dir; 
        float speed{0}; 
        float dist{0}; 
        bool hasInput{false}; 
    };

    // Build movement intent from raw flags and orientation. Pure function, no engine dependencies.
    Intent BuildMovementIntent(uint32_t moveFlags, float orientation);

    /// Computes horizontal movement direction, speed, and distance from input flags.
    /// Derives 2D basis from orientation and applies appropriate speed based on movement type.
    MovementPlan BuildMovementPlan(
        uint32_t moveFlags,
        float orientation,
        float runSpeed,
        float walkSpeed,
        float runBackSpeed,
        float swimSpeed,
        bool hasInput,
        float dt,
        bool isSwimming);

    /// Computes a bounded depenetration vector from overlapping contacts.
    /// Uses MTD-like computation but clamps per-tick correction to avoid visual popping.
    /// Prefers upward-facing normals for stability.
    G3D::Vector3 ComputePendingDepenetrationFromOverlaps(const std::vector<SceneHit>& overlaps);

    /// Calculate move speed based on input flags and swimming state.
    float CalculateMoveSpeed(uint32_t moveFlags, float runSpeed, float walkSpeed, 
                             float runBackSpeed, float swimSpeed, bool isSwimming);

    /// Computes slide impact ratio based on angle between direction and surface normal.
    /// Returns a value between 0 and 1 indicating how much movement is preserved.
    float ComputeSlideImpactRatio(
        const G3D::Vector3& dirN,
        const G3D::Vector3& slideSourceN);
}
