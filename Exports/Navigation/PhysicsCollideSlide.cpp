// PhysicsCollideSlide.cpp - Iterative collide-and-slide system implementation
#include "PhysicsCollideSlide.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsEngine.h"
#include "PhysicsTolerances.h"
#include "VMapLog.h"
#include <sstream>
#include <cfloat>

namespace PhysicsCollideSlide
{

G3D::Vector3 ComputeSlideTangent(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& surfaceNormal)
{
    // Project movement direction onto the plane defined by the surface normal
    // tangent = moveDir - (moveDir . normal) * normal
    float dot = moveDir.dot(surfaceNormal);
    G3D::Vector3 tangent = moveDir - surfaceNormal * dot;
    
    float mag = tangent.magnitude();
    if (mag > 1e-6f) {
        return tangent * (1.0f / mag);
    }
    return G3D::Vector3(0, 0, 0);
}

G3D::Vector3 ComputeCreaseDirection(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& normal1,
    const G3D::Vector3& normal2)
{
    // Crease direction is the cross product of the two normals
    // This gives the direction along the intersection line of the two planes
    G3D::Vector3 crease = normal1.cross(normal2);
    float mag = crease.magnitude();
    
    if (mag < 1e-6f) {
        // Normals are parallel - no valid crease
        return G3D::Vector3(0, 0, 0);
    }
    
    crease = crease * (1.0f / mag);
    
    // Ensure crease direction is in the same hemisphere as the movement
    if (crease.dot(moveDir) < 0.0f) {
        crease = -crease;
    }
    
    return crease;
}

bool IsDirectionBlocked(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& constraintNormal)
{
    // Direction is blocked if it points into the constraint surface
    return moveDir.dot(constraintNormal) < -1e-6f;
}

SlideResult CollideAndSlide(
    uint32_t mapId,
    SlideState& st,
    float radius,
    float height,
    const G3D::Vector3& moveDir,
    float distance,
    bool horizontalOnly,
    bool preventCeilingSlide)
{
    SlideResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    result.finalVelocity = moveDir;
    result.distanceMoved = 0.0f;
    result.distanceRemaining = distance;
    result.iterations = 0;
    result.hitWall = false;
    result.hitCorner = false;
    result.hitCeiling = false;
    result.lastHitNormal = G3D::Vector3(0, 0, 1);
    // result.heightRange is default-initialized by TriangleHeightRange()

    // Early exit for trivial cases
    if (distance < MIN_MOVE_DISTANCE || moveDir.magnitude() < 1e-6f) {
        result.distanceRemaining = 0.0f;
        return result;
    }

    // Setup: Normalize direction and prepare for iteration
    const G3D::Vector3 originalDirN = moveDir.directionOrZero();
    const G3D::Vector3 originalDirN2D = horizontalOnly
        ? G3D::Vector3(originalDirN.x, originalDirN.y, 0.0f).directionOrZero()
        : originalDirN;

    G3D::Vector3 currentPosition = result.finalPosition;
    if (horizontalOnly) 
        currentPosition.z = result.finalPosition.z;

    // Target position for this move (targetPosition in PhysX)
    G3D::Vector3 targetPosition = currentPosition + originalDirN2D * distance;
    float remaining = distance;
    
    // Track constraint normals for corner detection
    std::vector<G3D::Vector3> constraintNormals;
    constraintNormals.reserve(MAX_SLIDE_ITERATIONS);

    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);

    // Main iteration loop
    for (int iter = 0; iter < MAX_SLIDE_ITERATIONS && remaining > MIN_MOVE_DISTANCE; ++iter) {
        result.iterations = iter + 1;

        // Recompute direction from target - current (PhysX-style)
        G3D::Vector3 currentDirection = targetPosition - currentPosition;
        if (horizontalOnly) 
            currentDirection.z = 0.0f;
            
        const float length = currentDirection.magnitude();
        if (length <= MIN_MOVE_DISTANCE) {
            result.distanceRemaining = 0.0f;
            break;
        }
        G3D::Vector3 currentDir = currentDirection * (1.0f / length);

        // PhysX early-out: if velocity is against the original velocity,
        // stop dead to avoid tiny oscillations in sloping corners
        if (originalDirN2D.magnitude() > 1e-6f) {
            const float dp = currentDir.dot(originalDirN2D);
            if (dp <= 0.0f) {
                PHYS_INFO(PHYS_MOVE, "[CollideAndSlide] early-out: currentDir opposes originalDir");
                result.distanceRemaining = remaining;
                break;
            }
        }

        // Build capsule and sweep
        CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(
            currentPosition.x, currentPosition.y, currentPosition.z,
            radius, height);

        std::vector<SceneHit> hits;
        SceneQuery::SweepCapsule(mapId, cap, currentDir, remaining, hits, playerFwd);

        // Find earliest blocking hit
        const SceneHit* earliest = nullptr;
        float minDist = FLT_MAX;
        
        for (const auto& hit : hits) {
            if (!hit.hit || hit.startPenetrating)
                continue;
            if (horizontalOnly) {
                // In horizontal mode, always accept Side hits. For Bottom/Top hits,
                // only accept if the hit normal has a significant horizontal component.
                // This prevents bots from phasing through WMO objects at foot level
                // (e.g. catapults, barricades) that register as Bottom capsule contacts
                // but act as horizontal barriers.
                if (hit.region != SceneHit::CapsuleRegion::Side) {
                    float hMag = std::sqrt(hit.normal.x * hit.normal.x + hit.normal.y * hit.normal.y);
                    if (hMag < 0.3f) continue;  // Skip purely vertical contacts (floor/ceiling)
                }
            }
            if (hit.distance < 1e-6f)
                continue;
            if (hit.distance < minDist) {
                minDist = hit.distance;
                earliest = &hit;
            }
            
            // Track triangle height range for all valid hits
            result.heightRange.RecordContact(hit.point.z);
        }

        // No collision - move the full remaining distance
        if (!earliest) {
            currentPosition += currentDir * remaining;
            result.distanceMoved += remaining;
            remaining = 0.0f;
            result.distanceRemaining = 0.0f;
            result.finalPosition = currentPosition;
            break;
        }

        // Collision detected - advance to just before the collision point
        // Use contact offset (skin width) to maintain separation from surfaces
        const float contactOffset = PhysicsTol::GetContactOffset(radius);
        float safeAdvance = std::max(0.0f, minDist - contactOffset);
        currentPosition += currentDir * safeAdvance;
        result.distanceMoved += safeAdvance;
        remaining -= safeAdvance;
        result.lastHitNormal = earliest->normal.directionOrZero();
        result.finalPosition = currentPosition;

        // Stop when remaining motion becomes very small
        if (remaining <= MIN_MOVE_DISTANCE) {
            result.distanceRemaining = 0.0f;
            break;
        }

        // =========================================================================
        // CEILING SLIDE PREVENTION
        // =========================================================================
        // If we hit a ceiling surface and ceiling slide prevention is enabled,
        // stop the movement immediately without sliding. This is critical for:
        //   1. Proper jump behavior (don't glide along ceilings)
        //   2. Preventing unexpected horizontal displacement during up-movement
        //   3. Matching PhysX CCT behavior
        // =========================================================================
        if (preventCeilingSlide && IsCeilingSurface(earliest->normal)) {
            result.hitCeiling = true;
            result.distanceRemaining = remaining;
            
            std::ostringstream oss;
            oss.setf(std::ios::fixed);
            oss.precision(4);
            oss << "[CollideAndSlide] Ceiling hit - preventing slide. normalZ=" 
                << earliest->normal.z << " remaining=" << remaining;
            PHYS_INFO(PHYS_MOVE, oss.str());
            
            // Don't slide - stop movement immediately
            break;
        }

        // Check if surface is walkable
        bool isWalkable = std::fabs(earliest->normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
        if (!isWalkable) {
            result.hitWall = true;
        }

        if (remaining < MIN_MOVE_DISTANCE) {
            result.distanceRemaining = 0.0f;
            break;
        }

        // Get the horizontal component of the normal for slide calculation
        G3D::Vector3 hitNormalH = earliest->normal;
        if (horizontalOnly) {
            hitNormalH.z = 0.0f;
            float mag = hitNormalH.magnitude();
            if (mag > 1e-6f) {
                hitNormalH = hitNormalH * (1.0f / mag);
            } else {
                // Purely vertical surface in horizontal mode - can't slide
                result.distanceRemaining = remaining;
                break;
            }
        }

        constraintNormals.push_back(hitNormalH);

        // Corner case: two or more constraints
        if (constraintNormals.size() >= 2) {
            G3D::Vector3 crease = ComputeCreaseDirection(
                currentDir,
                constraintNormals[constraintNormals.size() - 2],
                constraintNormals[constraintNormals.size() - 1]);

            if (crease.magnitude() > 1e-6f) {
                // Check if crease direction is blocked by any previous constraint
                bool creaseBlocked = false;
                for (const auto& cn : constraintNormals) {
                    if (IsDirectionBlocked(crease, cn)) {
                        creaseBlocked = true;
                        break;
                    }
                }

                if (!creaseBlocked) {
                    currentDir = crease;
                    if (horizontalOnly) {
                        currentDir.z = 0.0f;
                        currentDir = currentDir.directionOrZero();
                    }
                    targetPosition = currentPosition + currentDir * remaining;
                    result.hitCorner = true;
                    continue;
                }
            }

            // Crease blocked or invalid - we're stuck in a corner
            result.distanceRemaining = remaining;
            result.hitCorner = true;
            PHYS_INFO(PHYS_MOVE, "[CollideAndSlide] STUCK in corner - stopping");
            break;
        }

        // Single constraint - compute slide using PhysX-style collisionResponse
        const G3D::Vector3 n = hitNormalH.directionOrZero();
        
        // Step 1: Compute reflection vector
        G3D::Vector3 reflectDir = currentDir - n * (2.0f * currentDir.dot(n));
        float reflectMag = reflectDir.magnitude();
        if (reflectMag > 1e-6f) {
            reflectDir = reflectDir * (1.0f / reflectMag);
        }
        
        // Step 2: Decompose reflected direction into normal and tangent
        float normalMag = reflectDir.dot(n);
        G3D::Vector3 normalCompo = n * normalMag;
        G3D::Vector3 tangentCompo = reflectDir - normalCompo;
        
        // Step 3: Apply bump and friction parameters
        // For WoW-like movement: bump=0.0 (no bounce), friction=1.0 (full slide)
        const float bump = 0.0f;
        const float friction = 1.0f;
        const float amplitude = remaining;
        
        // PhysX-style target mutation
        G3D::Vector3 newTarget = currentPosition;
        if (bump != 0.0f) {
            G3D::Vector3 normN = normalCompo;
            float normMag = normN.magnitude();
            if (normMag > 1e-6f) 
                normN = normN * (1.0f / normMag);
            newTarget += normN * bump * amplitude;
        }
        if (friction != 0.0f) {
            G3D::Vector3 tangN = tangentCompo;
            float tangMag = tangN.magnitude();
            if (tangMag > 1e-6f) 
                tangN = tangN * (1.0f / tangMag);
            newTarget += tangN * friction * amplitude;
        }
        
        targetPosition = newTarget;
        if (horizontalOnly) {
            targetPosition.z = currentPosition.z;
        }
        
        // Compute slide direction for velocity output
        G3D::Vector3 slideDir = tangentCompo;
        if (horizontalOnly) 
            slideDir.z = 0.0f;
        float slideMag = slideDir.magnitude();
        if (slideMag > 1e-6f) {
            slideDir = slideDir * (1.0f / slideMag);
        }
        
        if (slideDir.magnitude() < 1e-6f) {
            result.distanceRemaining = remaining;
            PHYS_INFO(PHYS_MOVE, "[CollideAndSlide] No valid slide direction - stopping");
            break;
        }

        // Check if slide direction is blocked by any previous constraint
        for (size_t i = 0; i < constraintNormals.size() - 1; ++i) {
            if (IsDirectionBlocked(slideDir, constraintNormals[i])) {
                break;
            }
        }

        result.finalVelocity = slideDir;
    }

    // Update movement state position
    st.x = result.finalPosition.x;
    st.y = result.finalPosition.y;
    if (!horizontalOnly) {
        st.z = result.finalPosition.z;
    }

    return result;
}

} // namespace PhysicsCollideSlide
