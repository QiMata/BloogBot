// PhysicsTolerances.h - Unified tolerances (skin/offsets/biases) for sweeps and contacts
#pragma once

#include "PhysicsMath.h"

namespace PhysicsTol
{
    // =====================================================================================
    // CONTACT OFFSET (Skin Width)
    // =====================================================================================
    // The contact offset (also called "skin width" in PhysX) is the distance at which 
    // contacts are generated before actual penetration occurs. This creates a "safety margin"
    // around the character that helps:
    //   1. Prevent tunneling through thin geometry
    //   2. Provide smoother collision response
    //   3. Give the solver time to react before deep penetration
    //
    // PhysX CCT uses mContactOffset (typically 0.01 - 0.1 units) added to sweep distances.
    // When a collision is detected at distance D, we advance to (D - ContactOffset) to
    // maintain the skin separation.
    //
    // For WoW characters (radius ~0.3-1.0), a contact offset of 0.01-0.02 is appropriate.
    // Larger characters may benefit from slightly larger values.
    // =====================================================================================
    
    /// Default contact offset for character controllers.
    /// This is the minimum separation maintained between the character and obstacles.
    constexpr float DEFAULT_CONTACT_OFFSET = 0.01f;
    
    /// Compute contact offset based on character radius for better scaling.
    /// Returns a value between minOffset and maxOffset based on radius percentage.
    /// @param radius The character's collision radius
    /// @param minOffset Minimum contact offset (default: 0.01)
    /// @param maxOffset Maximum contact offset (default: 0.05)
    inline float ContactOffset(float radius, float minOffset = 0.01f, float maxOffset = 0.05f)
    {
        // Use ~3% of radius, clamped to reasonable bounds
        // This scales appropriately for different character sizes:
        //   - Gnome (r=0.3): ~0.01 (clamped to min)
        //   - Human (r=0.31): ~0.01
        //   - Tauren (r=0.97): ~0.03
        return PhysicsMath::Clamp(radius * 0.03f, minOffset, maxOffset);
    }
    
    /// Get contact offset for a specific character, or use default if radius unknown.
    inline float GetContactOffset(float radius = 0.0f)
    {
        if (radius <= 0.0f)
            return DEFAULT_CONTACT_OFFSET;
        return ContactOffset(radius);
    }

    // =====================================================================================
    // OTHER TOLERANCES
    // =====================================================================================
    
    /// Normal comparison epsilon
    inline float NormalEps() { return 1e-3f; }

    /// Ground Z bias for final verification/snap (WoW-like max of 0.05f)
    inline float GroundZBias(float radius)
    {
        return PhysicsMath::Clamp(radius * 0.05f, 0.01f, 0.05f);
    }

    /// Broadphase AABB inflation used when collecting candidates
    inline float AABBInflation(float radius)
    {
        // Minimal inflation based on radius percentage without skin usage
        return PhysicsMath::Clamp(radius * 0.01f, 0.0f, 0.1f);
    }

    /// Epsilon values
    inline float ToiEps()    { return 1e-4f; }
}
