// PhysicsTolerances.h - Unified tolerances (skin/offsets/biases) for sweeps and contacts
#pragma once

#include "PhysicsMath.h"

namespace PhysicsTol
{
    // Base skin/contact offset used to inflate shapes for conservative contact
    inline float BaseSkin(float radius)
    {
        // Scale with radius; clamp to sane bounds
        return PhysicsMath::Clamp(radius * 0.02f, 0.001f, 0.05f);
    }

    // Alias for contact offset (used during sweeps/broadphase)
    inline float ContactOffset(float radius)
    {
        return BaseSkin(radius);
    }

    // Rest separation after resolution to avoid jitter
    inline float RestOffset(float radius)
    {
        return BaseSkin(radius) * 0.5f;
    }

    // Ground Z bias for final verification/snap (WoW-like max of 0.05f)
    inline float GroundZBias(float radius)
    {
        return PhysicsMath::Clamp(radius * 0.05f, 0.01f, 0.05f);
    }

    // Broadphase AABB inflation used when collecting candidates
    inline float AABBInflation(float radius)
    {
        return BaseSkin(radius) * 0.25f;
    }

    // Epsilon values
    inline float NormalEps() { return 1e-3f; }
    inline float ToiEps()    { return 1e-4f; }
}
