// PhysicsMovement.cpp - Air and swim movement implementation
#include "PhysicsMovement.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsEngine.h"
#include "PhysicsHelpers.h"
#include "SceneQuery.h"
#include "VMapLog.h"
#include <cmath>
#include <sstream>
#include <cfloat>

namespace PhysicsMovement
{

void ApplyGravity(MovementState& st, float dt)
{
    st.vz -= PhysicsConstants::GRAVITY * dt; 
    if (st.vz < -60.0f) 
        st.vz = -60.0f;
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
    if (isSwimming) return input.swimSpeed;
    if (input.moveFlags & MOVEFLAG_WALK_MODE) return input.walkSpeed;
    if (input.moveFlags & MOVEFLAG_BACKWARD) return input.runBackSpeed;
    return input.runSpeed;
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
    // Integrate vertical motion using z += vz0*dt - 0.5*g*dt^2
    G3D::Vector3 startPos(st.x, st.y, st.z);
    const float vz0 = st.vz;
    const float dz = vz0 * dt - 0.5f * PhysicsConstants::GRAVITY * dt * dt;
    
    // Apply gravity to velocity (with terminal clamp)
    ApplyGravity(st, dt);

    // Predict next position with constant horizontal velocity
    G3D::Vector3 endPos = startPos + G3D::Vector3(st.vx * dt, st.vy * dt, dz);

    // Commit position
    st.x = endPos.x;
    st.y = endPos.y;
    st.z = endPos.z;

    // Continuous collision: prevent tunneling through ground when falling
    const float r = input.radius;
    const float h = input.height;
    const float stepDownLimit = PhysicsConstants::STEP_DOWN_HEIGHT;
    
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(startPos.x, startPos.y, startPos.z, r, h);
    G3D::Vector3 downDir(0, 0, -1);
    float fallDist = std::max(0.0f, startPos.z - endPos.z);
    float sweepDist = fallDist + stepDownLimit;
    
    std::vector<SceneHit> downHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(input.mapId, cap, downDir, sweepDist, downHits, playerFwd);
    
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const SceneHit* bestNP = nullptr; 
    float bestTOI = FLT_MAX; 
    float bestZ = -FLT_MAX;
    
    for (size_t i = 0; i < downHits.size(); ++i) {
        const auto& hhit = downHits[i];
        if (hhit.startPenetrating) continue;
        if (hhit.normal.z < walkableCosMin) continue;
        
        bool better = false;
        if (!bestNP) better = true;
        else {
            if ((hhit.instanceId == 0) && (bestNP->instanceId != 0)) better = true;
            else if ((hhit.instanceId == bestNP->instanceId)) {
                if (hhit.distance < bestTOI - 1e-6f) better = true;
                else if (std::fabs(hhit.distance - bestTOI) <= 1e-6f && hhit.point.z < bestZ) better = true;
            }
        }
        if (better) { 
            bestNP = &hhit; 
            bestTOI = hhit.distance; 
            bestZ = hhit.point.z; 
        }
    }
    
    if (bestNP) {
        float toiDist = bestNP->distance;
        if (toiDist <= sweepDist + 1e-4f) {
            float nx = bestNP->normal.x, ny = bestNP->normal.y, nz = bestNP->normal.z;
            float px = bestNP->point.x,  py = bestNP->point.y,  pz = bestNP->point.z;
            float snapZ = pz;
            if (std::fabs(nz) > 1e-6f) {
                snapZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
            }
            st.z = snapZ;
            st.vz = 0.0f;
            st.isGrounded = true;
            st.groundNormal = bestNP->normal.directionOrZero();
        }
    } else if (!downHits.empty()) {
        // Fallback for penetrating walkable contacts
        const SceneHit* bestPen = nullptr; 
        float bestPenZ = -FLT_MAX;
        for (const auto& hhit : downHits) {
            if (!hhit.startPenetrating) continue;
            if (hhit.normal.z < walkableCosMin) continue;
            if (hhit.distance > sweepDist + 1e-4f) continue;
            
            bool better = false;
            if (!bestPen) better = true;
            else {
                if ((hhit.instanceId == 0) && (bestPen->instanceId != 0)) better = true;
                else if (hhit.point.z > bestPenZ) better = true;
            }
            if (better) { 
                bestPen = &hhit; 
                bestPenZ = hhit.point.z; 
            }
        }
        if (bestPen) {
            float nx = bestPen->normal.x, ny = bestPen->normal.y, nz = bestPen->normal.z;
            float px = bestPen->point.x,  py = bestPen->point.y,  pz = bestPen->point.z;
            float snapZ = pz;
            if (std::fabs(nz) > 1e-6f) {
                snapZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
            }
            st.z = snapZ;
            st.vz = 0.0f;
            st.isGrounded = true;
            st.groundNormal = bestPen->normal.directionOrZero();
        }
    }
}

void ProcessSwimMovement(
    const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float dt,
    float speed)
{
    // Handles swim movement: horizontal and vertical (pitch) control
    if (intent.hasInput) {
        st.vx = intent.dir.x * speed;
        st.vy = intent.dir.y * speed;
    }
    else {
        st.vx = st.vy = 0;
    }
    
    float desiredVz = 0.0f;
    // Only apply vertical movement if moving forward
    if (intent.hasInput && (input.moveFlags & MOVEFLAG_FORWARD))
        desiredVz = std::sin(st.pitch) * speed;
    
    st.vz = desiredVz;
    st.x += st.vx * dt;
    st.y += st.vy * dt;
    st.z += st.vz * dt;
}

} // namespace PhysicsMovement
