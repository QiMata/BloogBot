// PhysicsGroundSnap.h - Ground snapping and step detection utilities
#pragma once

#include "Vector3.h"
#include "PhysicsBridge.h"
#include "SceneQuery.h"

namespace PhysicsGroundSnap
{
    // Internal movement state for ground snap operations
    struct GroundSnapState
    {
        float x, y, z;
        float vx, vy, vz;
        float orientation;
        bool isGrounded;
        G3D::Vector3 groundNormal;
    };

    /// Attempts to step up within maxUp distance to a walkable surface.
    /// Returns true if Z was snapped up and ground state set.
    bool TryStepUpSnap(
        uint32_t mapId,
        GroundSnapState& st,
        float radius,
        float height,
        float maxUp);

    /// Attempts to snap down to a walkable surface within step-down limits.
    /// Returns true if snapped to ground, false if will fall.
    bool TryDownwardStepSnap(
        uint32_t mapId,
        GroundSnapState& st,
        float radius,
        float height);

    /// Performs a vertical sweep down and snaps to walkable if found.
    /// Returns true if snapped.
    bool VerticalSweepSnapDown(
        uint32_t mapId,
        GroundSnapState& st,
        float radius,
        float height,
        float maxDown);

    /// Computes a small horizontal depenetration vector from current overlaps.
    /// Returns the applied XY push magnitude.
    float ApplyHorizontalDepenetration(
        uint32_t mapId,
        GroundSnapState& st,
        float radius,
        float height,
        bool walkableOnly);

    /// Computes a vertical depenetration push to resolve upward-facing contacts.
    /// Returns the applied Z delta.
    float ApplyVerticalDepenetration(
        uint32_t mapId,
        GroundSnapState& st,
        float radius,
        float height);

    /// Performs a horizontal capsule sweep and returns earliest blocking distance.
    /// If no blocking hit, returns dist.
    float HorizontalSweepAdvance(
        uint32_t mapId,
        float x, float y, float z,
        float orientation,
        float radius,
        float height,
        const G3D::Vector3& dir,
        float dist);

} // namespace PhysicsGroundSnap
