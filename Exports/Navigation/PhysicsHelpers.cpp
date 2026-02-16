#include "PhysicsHelpers.h"
#include "PhysicsBridge.h" // for MovementFlags bit definitions
#include "VMapLog.h"
#include <cmath>
#include <sstream>
#include <algorithm>

namespace PhysicsHelpers
{
    Intent BuildMovementIntent(uint32_t moveFlags, float orientation)
    {
        Intent intent{};
        float c = std::cos(orientation);
        float s = std::sin(orientation);
        float dirX = 0.0f, dirY = 0.0f;

        // Use shared MovementFlags from PhysicsBridge.h to avoid mismatches
        if (moveFlags & MOVEFLAG_FORWARD)      { dirX += c;  dirY += s; }
        if (moveFlags & MOVEFLAG_BACKWARD)     { dirX -= c;  dirY -= s; }
        if (moveFlags & MOVEFLAG_STRAFE_LEFT)  { dirX -= s;  dirY += c; }
        if (moveFlags & MOVEFLAG_STRAFE_RIGHT) { dirX += s;  dirY -= c; }

        float mag = std::sqrt(dirX * dirX + dirY * dirY);
        if (mag > 0.0001f) { dirX /= mag; dirY /= mag; intent.hasInput = true; }

        intent.dir = G3D::Vector3(dirX, dirY, 0.0f);
        intent.jumpRequested = (moveFlags & MOVEFLAG_JUMPING) != 0;
        return intent;
    }

    MovementPlan BuildMovementPlan(
        uint32_t moveFlags,
        float orientation,
        float runSpeed,
        float walkSpeed,
        float runBackSpeed,
        float swimSpeed,
        float swimBackSpeed,
        bool hasInput,
        float dt,
        bool isSwimming)
    {
        MovementPlan plan{};

        // Derive 2D basis from orientation
        const float co = std::cos(orientation);
        const float si = std::sin(orientation);
        G3D::Vector3 fwd(co, si, 0.0f);
        G3D::Vector3 left(-si, co, 0.0f);

        const bool moveFwd   = (moveFlags & MOVEFLAG_FORWARD) != 0;
        const bool moveBack  = (moveFlags & MOVEFLAG_BACKWARD) != 0;
        const bool strafeL   = (moveFlags & MOVEFLAG_STRAFE_LEFT) != 0;
        const bool strafeR   = (moveFlags & MOVEFLAG_STRAFE_RIGHT) != 0;
        const bool walk      = (moveFlags & MOVEFLAG_WALK_MODE) != 0;

        // Build raw desired direction from flags
        G3D::Vector3 dir(0, 0, 0);
        if (moveFwd)   dir += fwd;
        if (moveBack)  dir = dir - fwd;
        if (strafeL)   dir += left;
        if (strafeR)   dir = dir - left;

        plan.hasInput = hasInput || (dir.magnitude() > 1e-6f);
        if (!plan.hasInput) {
            return plan;
        }

        plan.dir = dir.directionOrZero();

        // Choose speed based on environment and flags
        if (isSwimming) {
            const bool backNoForward = moveBack && !moveFwd;
            plan.speed = backNoForward ? swimBackSpeed : swimSpeed;
        } else if (walk) {
            plan.speed = walkSpeed;
        } else {
            // Apply backward speed when moving backward without forward
            const bool backNoForward = moveBack && !moveFwd;
            plan.speed = backNoForward ? runBackSpeed : runSpeed;
        }

        plan.dist = std::max(0.0f, plan.speed * dt);

        return plan;
    }

    G3D::Vector3 ComputePendingDepenetrationFromOverlaps(const std::vector<SceneHit>& overlaps)
    {
        G3D::Vector3 acc(0, 0, 0);
        float maxDepth = 0.0f;
        
        for (const auto& oh : overlaps) {
            if (!oh.startPenetrating) 
                continue;
                
            float d = std::max(0.0f, oh.penetrationDepth);
            if (d <= 1e-6f) 
                continue;
                
            G3D::Vector3 n = oh.normal.directionOrZero();
            if (n.magnitude() <= 1e-6f) 
                continue;

            // Prefer upward-facing hemisphere for stability
            if (n.z < 0.0f) 
                n = -n;

            acc += n * d;
            maxDepth = std::max(maxDepth, d);
        }

        if (acc.magnitude() <= 1e-6f)
            return G3D::Vector3(0, 0, 0);

        // Conservative per-tick max to avoid popping (5cm)
        const float maxPerTick = 0.05f;
        float mag = acc.magnitude();
        float clampMag = std::min(maxPerTick, std::max(0.001f, std::min(mag, maxDepth)));
        return acc.directionOrZero() * clampMag;
    }

    float CalculateMoveSpeed(uint32_t moveFlags, float runSpeed, float walkSpeed,
                             float runBackSpeed, float swimSpeed, float swimBackSpeed,
                             bool isSwimming)
    {
        if (isSwimming) {
            if (moveFlags & MOVEFLAG_BACKWARD) return swimBackSpeed;
            return swimSpeed;
        }
        if (moveFlags & MOVEFLAG_WALK_MODE) return walkSpeed;
        if (moveFlags & MOVEFLAG_BACKWARD) return runBackSpeed;
        return runSpeed;
    }

    float ComputeSlideImpactRatio(
        const G3D::Vector3& dirN,
        const G3D::Vector3& slideSourceN)
    {
        G3D::Vector3 nH(slideSourceN.x, slideSourceN.y, 0.0f);
        float ratio = 0.0f;
        
        if (nH.magnitude() > 1e-6f) {
            nH = nH.directionOrZero();
            float cosA = std::fabs(dirN.dot(nH));
            cosA = std::max(0.0f, std::min(1.0f, cosA));
            float angle = std::acos(cosA);
            
            const float nearRightAngleEps = 0.005f;
            if (cosA <= nearRightAngleEps) {
                ratio = 0.0f;
            } else {
                ratio = (float)(angle / (G3D::pi() * 0.5));
            }
        }
        
        return std::max(0.0f, std::min(1.0f, ratio));
    }
}
