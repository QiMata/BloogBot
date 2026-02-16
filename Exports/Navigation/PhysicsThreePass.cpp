// PhysicsThreePass.cpp - Three-pass movement decomposition implementation
//
// =====================================================================================
// IMPORTANT: CODE PATH USAGE NOTE
// =====================================================================================
// This module provides standalone three-pass movement functions that can be called
// directly via the PhysicsThreePass namespace. However, the main physics entry point
// (PhysicsEngine::StepV2) uses PhysicsEngine's OWN implementations of ExecuteUpPass,
// ExecuteSidePass, and ExecuteDownPass - NOT the functions in this file.
//
// The functions here include additional features (like climbing sensor sweep) that
// are NOT active in the main code path. If you need to modify three-pass behavior
// for StepV2, edit PhysicsEngine.cpp instead.
//
// To consolidate: consider having PhysicsEngine delegate to these functions, or
// remove this module if the PhysicsEngine implementations are preferred.
// =====================================================================================
#include "PhysicsThreePass.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsEngine.h"
#include "PhysicsTolerances.h"
#include "SceneQuery.h"
#include "VMapLog.h"
#include <sstream>
#include <algorithm>
#include <cfloat>

using namespace PhysicsCollideSlide;

namespace PhysicsThreePass
{

// =====================================================================================
// CLIMBING SENSOR SWEEP (Constrained Climbing Mode)
// =====================================================================================
// NOTE: This climbing sensor is used by PhysicsThreePass::ExecuteUpPass but NOT by
// PhysicsEngine::ExecuteUpPass. The main StepV2 code path does not use this sensor.
// =====================================================================================
// PhysX CCT implements "constrained climbing mode" where before lifting the character
// up (auto-step), it performs a forward sensor sweep to detect if there's actually
// climbable geometry ahead. This prevents unnecessary vertical movement when:
//   1. Walking on flat ground with no obstacles
//   2. Moving away from obstacles (no need to step up)
//   3. The obstacle ahead is too tall to step over
//
// This optimization improves movement quality and reduces unnecessary capsule lifts
// that could cause visual jitter or unexpected collision behavior.
// =====================================================================================

ClimbingSensorConfig GetDefaultClimbingSensorConfig(float radius)
{
    ClimbingSensorConfig config{};
    config.enabled = true;
    // Forward sensor distance: slightly more than radius to detect obstacles ahead
    config.sensorDistance = radius * 2.0f;
    // Maximum climbable angle (matches walkable slope threshold)
    config.maxClimbAngle = 60.0f; // cos(60) = 0.5 = DEFAULT_WALKABLE_MIN_NORMAL_Z
    return config;
}

bool PerformClimbingSensorSweep(
    uint32_t mapId,
    const ThreePassState& st,
    float radius,
    float height,
    const G3D::Vector3& sideVector,
    float stepOffset,
    const ClimbingSensorConfig& config)
{
    // If climbing sensor is disabled, always allow step-up
    if (!config.enabled) {
        return true;
    }

    // Check if we have meaningful horizontal movement
    float sideMagnitude = sideVector.magnitude();
    if (sideMagnitude < MIN_MOVE_DISTANCE) {
        // No horizontal movement - skip step-up entirely (PhysX behavior)
        PHYS_INFO(PHYS_MOVE, "[ClimbingSensor] No side movement - skipping step-up");
        return false;
    }

    G3D::Vector3 sideDir = sideVector.directionOrZero();

    // Build a capsule at current position for the forward sensor sweep
    // We sweep at foot level (not elevated) to detect obstacles we might step onto
    CapsuleCollision::Capsule sensorCap = PhysShapes::BuildFullHeightCapsule(
        st.x, st.y, st.z, radius, height);

    // Perform forward sweep to detect obstacles
    std::vector<SceneHit> sensorHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, sensorCap, sideDir, config.sensorDistance, sensorHits, playerFwd);

    // Analyze hits to determine if there's climbable geometry ahead
    bool foundClimbableObstacle = false;
    float closestObstacleDist = FLT_MAX;
    G3D::Vector3 closestNormal(0, 0, 1);

    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;

    for (const auto& hit : sensorHits) {
        if (!hit.hit) 
            continue;
        
        // Skip start-penetrating contacts (we're already inside something)
        if (hit.startPenetrating) 
            continue;
        
        // Only consider hits within our sensor range
        if (hit.distance > config.sensorDistance) 
            continue;

        // Check if this is a side/wall hit (not floor or ceiling)
        // A climbable obstacle typically has a horizontal-ish normal
        float normalZAbs = std::fabs(hit.normal.z);
        
        // If the normal is mostly horizontal (wall-like), this could be a step
        if (normalZAbs < walkableCosMin) {
            // This is a vertical surface (wall) - potential step candidate
            // Check if it's within our step height range
            
            // Estimate the height of the obstacle based on contact point
            // If the contact is at foot level and the normal is horizontal,
            // there's likely a step to climb
            
            float contactHeight = hit.point.z - st.z;
            
            // Only consider obstacles that are within our step height range
            // Obstacles too high (above step height) shouldn't trigger step-up
            // Obstacles at or below feet level are climbable
            if (contactHeight >= -0.1f && contactHeight <= stepOffset) {
                if (hit.distance < closestObstacleDist) {
                    closestObstacleDist = hit.distance;
                    closestNormal = hit.normal;
                    foundClimbableObstacle = true;
                }
            }
        }
        else {
            // This is a floor-like surface (normal mostly vertical)
            // If it's a walkable slope ahead, we don't need to step up
            // If it's a non-walkable slope ahead, we might need to try stepping
            
            // For now, a forward-facing slope within step height could indicate
            // a ramp we can walk up normally (no step needed)
            // But if we're about to hit a step edge, we do need to lift
            
            // Check if this is a ground contact ahead within step range
            float heightDiff = hit.point.z - st.z;
            if (heightDiff > 0.01f && heightDiff <= stepOffset) {
                // There's elevated ground ahead within step height - need to step up
                foundClimbableObstacle = true;
                if (hit.distance < closestObstacleDist) {
                    closestObstacleDist = hit.distance;
                    closestNormal = hit.normal;
                }
            }
        }
    }

    // Additional check: perform a low-level raycast to detect step edges
    // A step edge is characterized by:
    //   1. A horizontal surface at foot level (current ground)
    //   2. A vertical rise within step height
    //   3. Another horizontal surface at the top
    
    if (!foundClimbableObstacle) {
        // Cast a ray forward at foot level + small offset
        G3D::Vector3 rayStart(st.x, st.y, st.z + 0.1f);
        G3D::Vector3 rayEnd = rayStart + sideDir * config.sensorDistance;
        
        // If this ray hits something, check if there's a step
        // For simplicity, we'll trust the capsule sweep results
        // More sophisticated implementations could do additional raycasts
    }

    {
        std::ostringstream oss;
        oss.setf(std::ios::fixed);
        oss.precision(4);
        oss << "[ClimbingSensor] foundClimbable=" << (foundClimbableObstacle ? 1 : 0)
            << " closestDist=" << closestObstacleDist
            << " stepOffset=" << stepOffset
            << " sideDir=(" << sideDir.x << "," << sideDir.y << ")";
        PHYS_INFO(PHYS_MOVE, oss.str());
    }

    return foundClimbableObstacle;
}

DecomposedMovement DecomposeMovement(
    const G3D::Vector3& direction,
    const G3D::Vector3& upDirection,
    float stepOffset,
    bool isJumping,
    bool standingOnMoving)
{
    DecomposedMovement result{};
    result.stepOffset = stepOffset;
    
    // Decompose direction into vertical (parallel to up) and horizontal (perpendicular)
    float verticalComponent = direction.dot(upDirection);
    G3D::Vector3 verticalVec = upDirection * verticalComponent;
    G3D::Vector3 horizontalVec = direction - verticalVec;
    
    result.isMovingUp = (verticalComponent > 0.0f);
    
    // Check for meaningful side movement
    float sideMagnitude = horizontalVec.magnitude();
    result.hasSideMovement = (sideMagnitude > MIN_MOVE_DISTANCE);
    
    // Cancel stepOffset when jumping (unless standing on moving platform)
    if (isJumping && !standingOnMoving) {
        result.stepOffset = 0.0f;
        PHYS_INFO(PHYS_MOVE, "[Decompose] Cancelled stepOffset - player is jumping");
    }
    
    // PhysX CCT: Cancel stepOffset when there's no lateral movement AND not on a moving platform.
    // This prevents unwanted auto-step when standing still, which could cause the character
    // to climb onto small obstacles that move against it (e.g., doors, elevators).
    // From PhysX: "const bool sideVectorIsZero = !standingOnMovingUp && Ps::isAlmostZero(SideVector);"
    const bool sideVectorIsZero = !standingOnMoving && !result.hasSideMovement;
    if (sideVectorIsZero) {
        result.stepOffset = 0.0f;
        PHYS_INFO(PHYS_MOVE, "[Decompose] Cancelled stepOffset - no lateral movement (sideVectorIsZero)");
    }
    
    // Build the three movement vectors
    if (verticalComponent <= 0.0f) {
        result.downVector = verticalVec;
        result.upVector = G3D::Vector3(0, 0, 0);
    } else {
        result.upVector = verticalVec;
        result.downVector = G3D::Vector3(0, 0, 0);
    }
    
    result.sideVector = horizontalVec;
    
    // Apply auto-step lift to upVector if we have side movement AND not jumping
    if (result.hasSideMovement && result.stepOffset > 0.0f) {
        result.upVector += upDirection * result.stepOffset;
    }
    
    return result;
}

SlideResult ExecuteUpPass(
    uint32_t mapId,
    ThreePassState& st,
    float radius,
    float height,
    const DecomposedMovement& decomposed,
    float& clampedStepOffset)
{
    SlideResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    clampedStepOffset = decomposed.stepOffset;
    
    float upMagnitude = decomposed.upVector.magnitude();
    if (upMagnitude < MIN_MOVE_DISTANCE) {
        return result;
    }
    
    G3D::Vector3 upDir = decomposed.upVector.directionOrZero();
    float originalZ = st.z;
    
    // Only apply climbing sensor for auto-step (not for jumps)
    bool isAutoStep = decomposed.hasSideMovement && decomposed.stepOffset > 0.0f && !decomposed.isMovingUp;
    
    if (isAutoStep) {
        ClimbingSensorConfig sensorConfig = GetDefaultClimbingSensorConfig(radius);
        
        bool hasClimbableGeometry = PerformClimbingSensorSweep(
            mapId, st, radius, height, decomposed.sideVector, decomposed.stepOffset, sensorConfig);
        
        if (!hasClimbableGeometry) {
            // No climbable obstacle detected - skip the step-up entirely
            PHYS_INFO(PHYS_MOVE, "[UpPass] Climbing sensor: no obstacle - skipping step-up");
            clampedStepOffset = 0.0f;
            
            // Recalculate upMagnitude without step offset
            // If there's still upward intent (jump), process that
            G3D::Vector3 pureUpward = decomposed.upVector - G3D::Vector3(0, 0, decomposed.stepOffset);
            upMagnitude = pureUpward.magnitude();
            
            if (upMagnitude < MIN_MOVE_DISTANCE) {
                return result;
            }
            
            upDir = pureUpward.directionOrZero();
        }
    }
    
    // Perform upward sweep
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(
        st.x, st.y, st.z, radius, height);
    std::vector<SceneHit> upHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, cap, upDir, upMagnitude, upHits, playerFwd);
    
    // Find earliest blocking hit
    const SceneHit* earliest = nullptr;
    float minDist = FLT_MAX;
    for (const auto& hit : upHits) {
        if (!hit.hit || hit.startPenetrating) 
            continue;
        if (hit.distance < 1e-6f) 
            continue;
        if (hit.distance < minDist) {
            minDist = hit.distance;
            earliest = &hit;
        }
    }
    
    float advance = upMagnitude;
    if (earliest) {
        // Use contact offset (skin width) to maintain separation from ceiling
        const float contactOffset = PhysicsTol::GetContactOffset(radius);
        advance = std::max(0.0f, minDist - contactOffset);
        result.hitWall = true;
        result.lastHitNormal = earliest->normal.directionOrZero();
    }
    
    // Apply upward movement
    st.z += advance;
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    result.distanceMoved = advance;
    result.distanceRemaining = upMagnitude - advance;
    result.iterations = 1;
    
    // Clamp step offset to actual delta
    float actualDelta = st.z - originalZ;
    clampedStepOffset = std::min(decomposed.stepOffset, actualDelta);
    
    return result;
}

SlideResult ExecuteSidePass(
    uint32_t mapId,
    ThreePassState& st,
    float radius,
    float height,
    const DecomposedMovement& decomposed)
{
    float sideMagnitude = decomposed.sideVector.magnitude();
    if (sideMagnitude < MIN_MOVE_DISTANCE) {
        SlideResult empty{};
        empty.finalPosition = G3D::Vector3(st.x, st.y, st.z);
        return empty;
    }
    
    G3D::Vector3 sideDir = decomposed.sideVector.directionOrZero();
    
    // Use CollideAndSlide for the side pass
    PhysicsCollideSlide::SlideState slideState;
    slideState.x = st.x;
    slideState.y = st.y;
    slideState.z = st.z;
    slideState.orientation = st.orientation;
    
    SlideResult result = PhysicsCollideSlide::CollideAndSlide(
        mapId, slideState, radius, height, sideDir, sideMagnitude, /*horizontalOnly*/ true);
    
    // Update state from slide result
    st.x = slideState.x;
    st.y = slideState.y;
    st.z = slideState.z;
    
    return result;
}

SlideResult ExecuteDownPass(
    uint32_t mapId,
    ThreePassState& st,
    float radius,
    float height,
    const DecomposedMovement& decomposed,
    float clampedStepOffset)
{
    SlideResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    // result.heightRange is default-initialized by TriangleHeightRange()
    
    float originalZ = st.z;
    
    // Calculate total downward distance
    float undoStepOffset = decomposed.hasSideMovement ? clampedStepOffset : 0.0f;
    float downMagnitude = decomposed.downVector.magnitude();
    float snapDistance = PhysicsConstants::STEP_DOWN_HEIGHT;
    float totalDown = undoStepOffset + downMagnitude + snapDistance;
    
    if (totalDown < MIN_MOVE_DISTANCE) {
        return result;
    }
    
    G3D::Vector3 downDir(0, 0, -1);
    
    // Perform downward sweep
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, radius, height);
    std::vector<SceneHit> downHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, cap, downDir, totalDown, downHits, playerFwd);

    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const float snapEps = 1e-4f;
    const float maxAllowedPenDepth = 0.02f;

    // Track triangle height range from all hits for slope validation
    for (const auto& hit : downHits) {
        if (!hit.hit) continue;
        // Record all contact heights (including penetrating for roughness analysis)
        result.heightRange.RecordContact(hit.point.z);
    }

    // Ground candidate selection
    struct GroundCandidate
    {
        const SceneHit* hit{ nullptr };
        float planeZ{ 0.0f };
        float snapZ{ 0.0f };
        float toi{ 0.0f };
        bool walkable{ false };
    };

    std::vector<GroundCandidate> candidates;
    candidates.reserve(downHits.size());

    for (const auto& hit : downHits) {
        if (!hit.hit || hit.startPenetrating) continue;
        if (hit.distance < 1e-6f) continue;

        const bool walkable = (std::fabs(hit.normal.z) >= walkableCosMin);

        float nx = hit.normal.x, ny = hit.normal.y, nz = hit.normal.z;
        float px = hit.point.x, py = hit.point.y, pz = hit.point.z;
        float planeZ = pz;
        if (std::fabs(nz) > 1e-6f) {
            planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
        }

        float snapZ = planeZ + snapEps;
        if (snapZ > originalZ) snapZ = originalZ;

        GroundCandidate c;
        c.hit = &hit;
        c.planeZ = planeZ;
        c.snapZ = snapZ;
        c.toi = hit.distance;
        c.walkable = walkable;
        candidates.push_back(c);
    }

    auto validateCandidate = [&](const GroundCandidate& c, float& outMaxPenDepth, int& outPenCount) -> bool {
        CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, c.snapZ, radius, height);
        std::vector<SceneHit> overlaps;
        SceneQuery::SweepCapsule(mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);

        outMaxPenDepth = 0.0f;
        outPenCount = 0;
        for (const auto& oh : overlaps) {
            if (!oh.startPenetrating) continue;
            ++outPenCount;
            outMaxPenDepth = std::max(outMaxPenDepth, std::max(0.0f, oh.penetrationDepth));
        }

        return outMaxPenDepth <= maxAllowedPenDepth;
    };

    // Sort candidates: walkable first, higher planeZ first, earlier TOI as tie-breaker
    std::stable_sort(candidates.begin(), candidates.end(), [&](const GroundCandidate& a, const GroundCandidate& b) {
        if (a.walkable != b.walkable) return a.walkable > b.walkable;
        if (std::fabs(a.planeZ - b.planeZ) > 1e-4f) return a.planeZ > b.planeZ;
        return a.toi < b.toi;
    });

    const GroundCandidate* chosen = nullptr;
    float chosenMaxPen = FLT_MAX;
    int chosenPenCount = 0;

    for (const auto& c : candidates) {
        float maxPen = 0.0f; int penCount = 0;
        if (validateCandidate(c, maxPen, penCount)) {
            chosen = &c;
            chosenMaxPen = maxPen;
            chosenPenCount = penCount;
            break;
        }
    }

    if (!chosen && !candidates.empty()) {
        for (const auto& c : candidates) {
            if (!c.walkable) continue;
            float maxPen = 0.0f; int penCount = 0;
            (void)validateCandidate(c, maxPen, penCount);
            if (!chosen || maxPen < chosenMaxPen) {
                chosen = &c;
                chosenMaxPen = maxPen;
                chosenPenCount = penCount;
            }
        }
    }

    if (chosen && chosen->hit) {
        st.z = chosen->snapZ;
        st.isGrounded = true;
        st.vz = 0.0f;
        st.groundNormal = chosen->hit->normal.directionOrZero();

        result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
        result.hitWall = !chosen->walkable;
        result.lastHitNormal = chosen->hit->normal.directionOrZero();
        result.distanceMoved = chosen->toi;
    } else {
        // No ground found - undo step offset and prepare to fall
        st.z -= clampedStepOffset;
        st.isGrounded = false;
        result.distanceRemaining = totalDown;
        result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    }
    
    return result;
}

bool ValidateSlopeAfterDownPass(
    const G3D::Vector3& contactNormal,
    float contactHeight,
    float originalBottomZ,
    float stepOffset)
{
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    
    if (std::fabs(contactNormal.z) < walkableCosMin) {
        float touchedTriHeight = contactHeight - originalBottomZ;
        if (touchedTriHeight > stepOffset) {
            return false; // Non-walkable
        }
    }
    
    return true; // Walkable
}

// =====================================================================================
// ENHANCED SLOPE VALIDATION WITH TRIANGLE HEIGHT RANGE
// =====================================================================================
// This provides more accurate slope validation by considering the actual geometry
// of the contacted triangles, not just their normals. Key benefits:
//   1. Distinguishes between smooth ramps and stepped geometry
//   2. Detects terrain roughness that might cause movement issues
//   3. Improves step detection for better auto-step decisions
// =====================================================================================

bool ValidateSlopeWithHeightRange(
    const G3D::Vector3& contactNormal,
    const PhysicsCollideSlide::TriangleHeightRange& heightRange,
    float stepOffset)
{
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    
    // Basic normal check - if normal indicates non-walkable slope, fail immediately
    if (std::fabs(contactNormal.z) < walkableCosMin) {
        PHYS_INFO(PHYS_MOVE, "[SlopeValidation] Non-walkable normal: z=" << contactNormal.z);
        return false;
    }
    
    // If no height range data available, fall back to normal-only validation
    if (!heightRange.valid) {
        return true; // Walkable based on normal alone
    }
    
    // Height span analysis
    // - Flat terrain: heightSpan ~= 0
    // - Gentle slope: heightSpan < stepOffset
    // - Step/ledge: heightSpan >= stepOffset (sharp vertical change)
    
    // Case 1: Very flat terrain - always walkable
    if (heightRange.IsFlat(0.05f)) {
        PHYS_INFO(PHYS_MOVE, "[SlopeValidation] Flat terrain: span=" << heightRange.heightSpan);
        return true;
    }
    
    // Case 2: Step-like geometry
    // If the height span is close to or exceeds step offset AND the normal is
    // near-vertical (not a smooth ramp), this might be a step that we should
    // allow stepping onto rather than sliding down.
    if (heightRange.IsLikelyStep(stepOffset * 0.8f)) {
        // This is step-like geometry - check if the normal is appropriate
        // A legitimate step has a mostly-vertical contact normal (from the top surface)
        if (contactNormal.z >= 0.9f) {
            // Near-flat top surface of a step - walkable
            std::ostringstream oss;
            oss.setf(std::ios::fixed);
            oss.precision(4);
            oss << "[SlopeValidation] Step-like: span=" << heightRange.heightSpan 
                << " normalZ=" << contactNormal.z << " -> walkable";
            PHYS_INFO(PHYS_MOVE, oss.str());
            return true;
        }
        else {
            // Steep normal on step-like geometry - might be the vertical face
            // This typically means we're hitting the side of a step, not the top
            std::ostringstream oss;
            oss.setf(std::ios::fixed);
            oss.precision(4);
            oss << "[SlopeValidation] Step-like steep: span=" << heightRange.heightSpan 
                << " normalZ=" << contactNormal.z << " -> checking...";
            PHYS_INFO(PHYS_MOVE, oss.str());
            
            // If we have multiple contacts (contactCount > 1), we might be touching
            // both the face and top of a step - allow if any contact is walkable
            if (heightRange.contactCount > 1) {
                return true; // Give benefit of doubt when multiple contacts
            }
        }
    }
    
    // Case 3: Moderate height variation - consistent with ramps or rough terrain
    // If the normal passes walkable check and height span is reasonable,
    // the surface is walkable
    if (heightRange.heightSpan <= stepOffset * 1.5f) {
        std::ostringstream oss;
        oss.setf(std::ios::fixed);
        oss.precision(4);
        oss << "[SlopeValidation] Moderate terrain: span=" << heightRange.heightSpan 
            << " stepOffset=" << stepOffset << " -> walkable";
        PHYS_INFO(PHYS_MOVE, oss.str());
        return true;
    }
    
    // Case 4: Large height variation with walkable normal
    // This is unusual - could be very rough terrain or spanning multiple elevations
    // Log a warning but allow if normal passes
    {
        std::ostringstream oss;
        oss.setf(std::ios::fixed);
        oss.precision(4);
        oss << "[SlopeValidation] WARNING: Large span=" << heightRange.heightSpan 
            << " with normalZ=" << contactNormal.z << " contacts=" << heightRange.contactCount;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    // Default: trust the normal
    return true;
}

ThreePassResult PerformThreePassMove(
    const PhysicsInput& input,
    ThreePassState& st,
    float radius,
    float height,
    const G3D::Vector3& moveDir,
    float distance,
    float dt,
    float stepOffsetOverride)
{
    ThreePassResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    result.groundNormal = G3D::Vector3(0, 0, 1);
    
    float originalZ = st.z;
    G3D::Vector3 upDirection(0, 0, 1);
    
    // Determine if player is jumping
    bool hasJumpFlag = (input.moveFlags & MOVEFLAG_JUMPING) != 0;
    bool isFallingWithUpwardVelocity = ((input.moveFlags & MOVEFLAG_FALLINGFAR) != 0) && (input.vz > 0.0f);
    bool isJumping = hasJumpFlag || isFallingWithUpwardVelocity;
    
    if (st.isGrounded && !hasJumpFlag) {
        isJumping = false;
    }
    
    // Determine if standing on a moving platform (transport).
    // In WoW, this is indicated by a non-zero transport GUID (boats, zeppelins, elevators).
    // When on a transport, we preserve step offset even without player input so the character
    // can properly ride on the moving surface and auto-step over obstacles on the transport.
    const bool standingOnMoving = (input.transportGuid != 0);
    
    // Scale move direction by distance
    G3D::Vector3 fullMove = moveDir.directionOrZero() * distance;
    
    // Step 1: Decompose movement
    float stepOffset = PhysicsConstants::STEP_HEIGHT;
    if (stepOffsetOverride >= 0.0f)
        stepOffset = stepOffsetOverride;

    DecomposedMovement decomposed = DecomposeMovement(
        fullMove, upDirection, stepOffset, isJumping, standingOnMoving);
    
    // Step 2: UP PASS
    float clampedStepOffset = 0.0f;
    SlideResult upResult = ExecuteUpPass(input.mapId, st, radius, height, decomposed, clampedStepOffset);
    result.collisionUp = upResult.hitWall;
    result.actualStepUpDelta = st.z - originalZ;
    
    // Step 3: SIDE PASS
    SlideResult sideResult = ExecuteSidePass(input.mapId, st, radius, height, decomposed);
    result.collisionSide = sideResult.hitWall || sideResult.hitCorner;
    
    // Step 4: DOWN PASS
    SlideResult downResult = ExecuteDownPass(input.mapId, st, radius, height, decomposed, clampedStepOffset);
    result.collisionDown = st.isGrounded;
    
    // Step 5: Post-pass slope validation using triangle height range
    if (st.isGrounded) {
        result.groundNormal = st.groundNormal;
        
        // Use enhanced slope validation if height range data is available
        if (downResult.heightRange.valid) {
            bool walkable = ValidateSlopeWithHeightRange(
                st.groundNormal, downResult.heightRange, clampedStepOffset);
            result.hitNonWalkable = !walkable;
        }
        else {
            // Fall back to traditional validation
            bool walkable = ValidateSlopeAfterDownPass(
                st.groundNormal, st.z, originalZ, clampedStepOffset);
            result.hitNonWalkable = !walkable;
        }
    }
    
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    
    return result;
}

} // namespace PhysicsThreePass
