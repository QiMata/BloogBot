// PhysicsCollideSlide.h - Iterative collide-and-slide system
// Handles wall collision with multiple bounces and corner detection.
#pragma once

#include "Vector3.h"
#include "PhysicsBridge.h"
#include "SceneQuery.h"
#include <vector>

namespace PhysicsCollideSlide
{
    // Maximum iterations for collide-and-slide per pass
    // ⚠️ CRITICAL: Must be 10, not 4. Lower values cause stuck issues in complex geometry.
    // PhysX CCT uses 10 as the default (see PHYSX_CCT_RULES.md Section 1.1 & 15.2).
    constexpr int MAX_SLIDE_ITERATIONS = 10;

    // Minimum distance to consider movement (avoids infinite loops)
    constexpr float MIN_MOVE_DISTANCE = 0.001f;

    // =========================================================================
    // CEILING SLIDE PREVENTION
    // =========================================================================
    // Ceiling slide prevention stops the character from sliding along ceiling
    // surfaces during upward movement. This is important because:
    //   1. Characters should not glide along ceilings when jumping
    //   2. Sliding on ceilings can cause unexpected horizontal displacement
    //   3. PhysX CCT implements this as a fundamental constraint
    // =========================================================================
    
    /// Threshold for considering a surface as a "ceiling" (normal points downward)
    /// cos(120°) = -0.5, meaning surfaces steeper than 60° from vertical are ceilings
    constexpr float CEILING_NORMAL_Z_THRESHOLD = -0.5f;
    
    /// Checks if a surface normal indicates a ceiling (downward-facing)
    /// @param normal The surface normal to check
    /// @return true if the surface is a ceiling
    inline bool IsCeilingSurface(const G3D::Vector3& normal)
    {
        return normal.z <= CEILING_NORMAL_Z_THRESHOLD;
    }

    // =========================================================================
    // TRIANGLE HEIGHT RANGE TRACKING
    // =========================================================================
    // Tracking the height range of contacted triangles improves slope validation
    // by providing more accurate information about the terrain geometry.
    // This helps distinguish between:
    //   - Flat surfaces (minZ ? maxZ)
    //   - Ramps/slopes (gradual minZ to maxZ difference)
    //   - Steps/ledges (sharp minZ to maxZ difference)
    // =========================================================================

    /// Height range information from contacted triangles
    struct TriangleHeightRange
    {
        float minZ;             // Minimum Z coordinate of all contact points
        float maxZ;             // Maximum Z coordinate of all contact points
        float heightSpan;       // maxZ - minZ (terrain roughness indicator)
        int contactCount;       // Number of contacts that contributed to this range
        bool valid;             // True if at least one valid contact was recorded
        
        TriangleHeightRange()
            : minZ(FLT_MAX), maxZ(-FLT_MAX), heightSpan(0.0f), contactCount(0), valid(false) {}
        
        /// Records a contact point's height
        void RecordContact(float z)
        {
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
            heightSpan = maxZ - minZ;
            ++contactCount;
            valid = true;
        }
        
        /// Checks if the height range suggests a step (sharp vertical change)
        bool IsLikelyStep(float stepThreshold) const
        {
            return valid && heightSpan >= stepThreshold;
        }
        
        /// Checks if the height range suggests flat or gently sloped terrain
        bool IsFlat(float flatThreshold = 0.1f) const
        {
            return valid && heightSpan <= flatThreshold;
        }
    };

    // Result of a single CollideAndSlide pass
    struct SlideResult
    {
        G3D::Vector3 finalPosition;     // Position after all iterations
        G3D::Vector3 finalVelocity;     // Remaining velocity direction (may be zero)
        float distanceMoved;            // Total distance actually moved
        float distanceRemaining;        // Distance that couldn't be traveled
        int iterations;                 // Number of iterations used
        bool hitWall;                   // True if blocked by non-walkable surface
        bool hitCorner;                 // True if constrained by multiple surfaces (corner)
        bool hitCeiling;                // True if hit a ceiling surface (new for ceiling prevention)
        G3D::Vector3 lastHitNormal;     // Normal of the last surface hit
        TriangleHeightRange heightRange; // Height range of contacted triangles (new for slope validation)
    };

    // Internal movement state for slide operations
    struct SlideState
    {
        float x, y, z;
        float orientation;
    };

    /// Computes the slide direction when hitting a single surface.
    /// Returns the tangent direction along the surface, or zero vector if fully blocked.
    G3D::Vector3 ComputeSlideTangent(
        const G3D::Vector3& moveDir,
        const G3D::Vector3& surfaceNormal);

    /// Computes the crease direction when constrained by two surfaces (corner case).
    /// Returns the direction along the intersection of two planes, or zero if invalid.
    G3D::Vector3 ComputeCreaseDirection(
        const G3D::Vector3& moveDir,
        const G3D::Vector3& normal1,
        const G3D::Vector3& normal2);

    /// Checks if a movement direction is blocked by a constraint normal.
    /// Returns true if the direction opposes the normal (would move into the surface).
    bool IsDirectionBlocked(
        const G3D::Vector3& moveDir,
        const G3D::Vector3& constraintNormal);

    /// Performs iterative collide-and-slide movement along a direction.
    /// Returns the result containing final position and remaining distance.
    /// This handles multiple bounces off surfaces and corner detection.
    /// @param preventCeilingSlide If true, prevents sliding along ceiling surfaces (default: false)
    SlideResult CollideAndSlide(
        uint32_t mapId,
        SlideState& st,
        float radius,
        float height,
        const G3D::Vector3& moveDir,
        float distance,
        bool horizontalOnly = true,
        bool preventCeilingSlide = false);

} // namespace PhysicsCollideSlide
