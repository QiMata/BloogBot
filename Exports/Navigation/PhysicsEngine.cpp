// =====================================================================================
// PhysicsEngine.cpp - Simplified physics tuned toward vanilla WoW 1.12.1 feel
// 
// This file implements a PhysX CCT-style character controller with:
//   - Three-pass movement decomposition (UP → SIDE → DOWN)
//   - Iterative collide-and-slide for wall collision
//   - Auto-step functionality for stairs/ledges
//   - Ground snapping and slope validation
//
// Organization:
//   1. Includes and namespace declarations
//   2. Singleton management
//   3. Anonymous namespace helpers
//   4. Delegating wrappers to extracted modules
//   5. Ground movement entry point
//   6. Main entry point (StepV2)
//
// NOTE: Core physics algorithms have been extracted to separate modules:
//   - PhysicsCollideSlide.h/.cpp  - Iterative wall collision
//   - PhysicsThreePass.h/.cpp     - UP/SIDE/DOWN movement decomposition
//   - PhysicsGroundSnap.h/.cpp    - Ground detection and snapping
//   - PhysicsMovement.h/.cpp      - Air and swim movement
// =====================================================================================

// -------------------------------------------------------------------------------------
// Includes
// -------------------------------------------------------------------------------------
#include "PhysicsEngine.h"
#include "Navigation.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "ModelInstance.h"
#include "CapsuleCollision.h"
#include "PhysicsBridge.h"
#include "VMapDefinitions.h"
#include "PhysicsHelpers.h"
#include "PhysicsLiquidHelpers.h"
#include "PhysicsDiagnosticsHelpers.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsSelectHelpers.h"
#include "SceneQuery.h"
#include "PhysicsTolerances.h"

// Extracted physics modules
#include "PhysicsCollideSlide.h"
#include "PhysicsThreePass.h"
#include "PhysicsGroundSnap.h"
#include "PhysicsMovement.h"

#include <algorithm>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <cfloat>
#include <chrono>
#include <set>
#include <sstream>

using namespace PhysicsConstants;
using namespace VMAP;

// =====================================================================================
// SECTION 1: SINGLETON MANAGEMENT
// =====================================================================================

PhysicsEngine* PhysicsEngine::s_instance = nullptr;

PhysicsEngine::PhysicsEngine()
    : m_initialized(false)
{
}

PhysicsEngine::~PhysicsEngine()
{
    Shutdown();
}

PhysicsEngine* PhysicsEngine::Instance()
{
    if (!s_instance)
        s_instance = new PhysicsEngine();
    return s_instance;
}

void PhysicsEngine::Destroy()
{
    delete s_instance;
    s_instance = nullptr;
}

void PhysicsEngine::Initialize()
{
    if (m_initialized)
        return;

    SceneQuery::Initialize();
    m_initialized = true;
    PHYS_INFO(PHYS_MOVE, "Initialize done");
}

void PhysicsEngine::Shutdown()
{
    PHYS_INFO(PHYS_MOVE, "Shutdown");
    m_initialized = false;
}

// =====================================================================================
// SECTION 2: ANONYMOUS NAMESPACE HELPERS
// These are internal utilities used by the physics engine implementation.
// Most pure functions have been moved to PhysicsHelpers module.
// =====================================================================================

namespace
{
    /// Parameters for PhysX-style "walk experiment" second pass.
    /// Used when initial move lands on non-walkable slope.
    struct WalkExperimentParams
    {
        bool forceSlide{ false };
    };
} // end anonymous namespace

// =====================================================================================
// SECTION 3: SMALL HELPER METHODS
// Logging, slide computation, and single-operation utilities.
// Many pure computations have been moved to PhysicsHelpers module.
// =====================================================================================

float PhysicsEngine::LogSlideImpactAndComputeRatio(
    const G3D::Vector3& dirN,
    const G3D::Vector3& slideSourceN,
    float dist,
    float advance)
{
    float ratio = PhysicsHelpers::ComputeSlideImpactRatio(dirN, slideSourceN);
    
    // Compute angle for logging
    G3D::Vector3 nH(slideSourceN.x, slideSourceN.y, 0.0f);
    float angleDeg = 0.0f;
    if (nH.magnitude() > 1e-6f) {
        nH = nH.directionOrZero();
        float cosA = std::fabs(dirN.dot(nH));
        cosA = std::max(0.0f, std::min(1.0f, cosA));
        float angle = std::acos(cosA);
        angleDeg = angle * (180.0f / (float)G3D::pi());
        
        const float nearRightAngleEps = 0.005f;
        if (cosA <= nearRightAngleEps) {
            PHYS_INFO(PHYS_MOVE, "[Impact] near-90deg; cancelling slide movement");
        }
    }
    
    std::ostringstream oss; 
    oss.setf(std::ios::fixed); 
    oss.precision(4);
    oss << "[Impact] dist=" << dist << " advance=" << advance
        << " angleDeg=" << angleDeg << " ratio=" << ratio;
    PHYS_INFO(PHYS_MOVE, oss.str());
    
    return ratio;
}

bool PhysicsEngine::ComputeStartOverlapSlideNormal(
    const PhysicsInput& input,
    const MovementState& st,
    float r,
    float h,
    const G3D::Vector3& dirN,
    G3D::Vector3& outSlideN)
{
    CapsuleCollision::Capsule capStart = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> startOverlaps;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(input.mapId, capStart, G3D::Vector3(0,0,0), 0.0f, startOverlaps, playerFwd);
    
    int count = 0; 
    int opposeCount = 0;
    G3D::Vector3 accum(0, 0, 0);
    
    for (const auto& oh : startOverlaps) {
        if (!oh.startPenetrating) 
            continue;
        if (std::fabs(oh.normal.z) >= 0.7f) 
            continue;
            
        G3D::Vector3 nH(oh.normal.x, oh.normal.y, 0.0f);
        if (nH.magnitude() <= 1e-6f) 
            continue;
            
        nH = nH.directionOrZero();
        float dot = dirN.dot(nH); 
        if (dot < 0.0f) 
            ++opposeCount;
            
        accum += nH; 
        ++count;
    }
    
    if (count > 0) {
        outSlideN = accum.directionOrZero();
        return true;
    }
    return false;
}

void PhysicsEngine::HandleNoHorizontalMovement(
    const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float r,
    float h,
    const G3D::Vector3& dirN,
    float dist,
    float dt,
    float moveSpeed)
{
    std::ostringstream d; 
    d.setf(std::ios::fixed); 
    d.precision(5);
    d << "[GroundMove] early-exit: hasInput=" << (intent.hasInput ? 1 : 0)
      << " moveFlags=0x" << std::hex << input.moveFlags << std::dec
      << " dirN=(" << dirN.x << "," << dirN.y << ") mag=" << dirN.magnitude()
      << " intendedDist=" << dist << " dt=" << dt << " speed=" << moveSpeed;
    PHYS_INFO(PHYS_MOVE, d.str());
    
    PerformVerticalPlacementOrFall(input, intent, st, r, h, dt, moveSpeed, 
        "ground path: no horizontal movement");
}

void PhysicsEngine::ApplySlideMovement(
    const PhysicsInput& input,
    MovementState& st,
    float r,
    float h,
    const G3D::Vector3& dirN,
    const G3D::Vector3& slideSourceN,
    float remaining)
{
    G3D::Vector3 nH(slideSourceN.x, slideSourceN.y, 0.0f);
    if (nH.magnitude() <= 1e-6f) {
        PHYS_INFO(PHYS_MOVE, "[Slide] skipped: invalid horizontal normal");
        return;
    }
    nH = nH.directionOrZero();
    
    // Project intended direction onto the contact plane (tangent)
    G3D::Vector3 slideDir = (dirN - nH * dirN.dot(nH));
    slideDir.z = 0.0f; 
    slideDir = slideDir.directionOrZero();
    
    float slideIntended = remaining;
    if (slideDir.magnitude() <= 1e-6f || slideIntended <= 1e-6f)
        return;

    // Sweep along slide direction
    CapsuleCollision::Capsule capSlide = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> slideHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(input.mapId, capSlide, slideDir, slideIntended, slideHits, playerFwd);
    
    // Find earliest blocking side hit
    const SceneHit* earliest2 = nullptr; 
    float minDist2 = FLT_MAX;
    for (const auto& hh : slideHits) {
        if (!hh.hit || hh.startPenetrating) 
            continue;
        if (hh.region != SceneHit::CapsuleRegion::Side) 
            continue;
        if (hh.distance < 1e-6f) 
            continue;
        if (hh.distance < minDist2) { 
            minDist2 = hh.distance; 
            earliest2 = &hh; 
        }
    }
    
    float advance2 = slideIntended;
    if (earliest2) 
        advance2 = std::max(0.0f, std::min(slideIntended, minDist2));
    
    {
        std::ostringstream oss; 
        oss.setf(std::ios::fixed); 
        oss.precision(4);
        oss << "[Slide] remain=" << remaining << " intended=" << slideIntended
            << " advance=" << advance2;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    // Apply slide movement
    st.x += slideDir.x * advance2;
    st.y += slideDir.y * advance2;
    
    {
        std::ostringstream s2; 
        s2.setf(std::ios::fixed); 
        s2.precision(5);
        s2 << "[SlideXY] slideDir=(" << slideDir.x << "," << slideDir.y << ") adv2=" << advance2
           << " dXY=(" << (slideDir.x * advance2) << "," << (slideDir.y * advance2) << ")";
        PHYS_INFO(PHYS_MOVE, s2.str());
    }
    
    ApplyHorizontalDepenetration(input, st, r, h, /*walkableOnly*/ true);
}

// =====================================================================================
// SECTION 4: DELEGATING WRAPPERS TO EXTRACTED MODULES
// These methods delegate to the extracted physics modules while maintaining
// the PhysicsEngine class interface for backward compatibility.
// =====================================================================================

G3D::Vector3 PhysicsEngine::ComputeSlideTangent(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& surfaceNormal) const
{
    return PhysicsCollideSlide::ComputeSlideTangent(moveDir, surfaceNormal);
}

G3D::Vector3 PhysicsEngine::ComputeCreaseDirection(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& normal1,
    const G3D::Vector3& normal2) const
{
    return PhysicsCollideSlide::ComputeCreaseDirection(moveDir, normal1, normal2);
}

bool PhysicsEngine::IsDirectionBlocked(
    const G3D::Vector3& moveDir,
    const G3D::Vector3& constraintNormal) const
{
    return PhysicsCollideSlide::IsDirectionBlocked(moveDir, constraintNormal);
}

PhysicsEngine::SlideResult PhysicsEngine::CollideAndSlide(
    const PhysicsInput& input,
    MovementState& st,
    float radius,
    float height,
    const G3D::Vector3& moveDir,
    float distance,
    bool horizontalOnly)
{
    // Convert to module state type
    PhysicsCollideSlide::SlideState slideState;
    slideState.x = st.x;
    slideState.y = st.y;
    slideState.z = st.z;
    slideState.orientation = st.orientation;
    
    // Delegate to extracted module
    PhysicsCollideSlide::SlideResult moduleResult = PhysicsCollideSlide::CollideAndSlide(
        input.mapId, slideState, radius, height, moveDir, distance, horizontalOnly);
    
    // Update movement state from result
    st.x = slideState.x;
    st.y = slideState.y;
    st.z = slideState.z;
    
    // Convert result to engine type
    SlideResult result{};
    result.finalPosition = moduleResult.finalPosition;
    result.finalVelocity = moduleResult.finalVelocity;
    result.distanceMoved = moduleResult.distanceMoved;
    result.distanceRemaining = moduleResult.distanceRemaining;
    result.iterations = moduleResult.iterations;
    result.hitWall = moduleResult.hitWall;
    result.hitCorner = moduleResult.hitCorner;
    result.lastHitNormal = moduleResult.lastHitNormal;
    
    return result;
}

bool PhysicsEngine::TryStepUpSnap(
    const PhysicsInput& input,
    MovementState& st,
    float r,
    float h,
    float maxUp)
{
    // Convert to module state type
    PhysicsGroundSnap::GroundSnapState snapState;
    snapState.x = st.x;
    snapState.y = st.y;
    snapState.z = st.z;
    snapState.vx = st.vx;
    snapState.vy = st.vy;
    snapState.vz = st.vz;
    snapState.orientation = st.orientation;
    snapState.isGrounded = st.isGrounded;
    snapState.groundNormal = st.groundNormal;
    
    // Delegate to extracted module
    bool result = PhysicsGroundSnap::TryStepUpSnap(input.mapId, snapState, r, h, maxUp);
    
    // Update movement state from result
    st.x = snapState.x;
    st.y = snapState.y;
    st.z = snapState.z;
    st.vz = snapState.vz;
    st.isGrounded = snapState.isGrounded;
    st.groundNormal = snapState.groundNormal;
    
    return result;
}

// =====================================================================================
// SECTION 5: THREE-PASS MOVEMENT SYSTEM (PHASE 2)
// PhysX CCT-style UP → SIDE → DOWN movement decomposition.
// These methods delegate to PhysicsThreePass module.
// =====================================================================================

PhysicsEngine::DecomposedMovement PhysicsEngine::DecomposeMovement(
    const G3D::Vector3& direction,
    const G3D::Vector3& upDirection,
    float stepOffset,
    bool isJumping,
    bool standingOnMoving) const
{
    // Delegate to extracted module
    PhysicsThreePass::DecomposedMovement moduleResult = 
        PhysicsThreePass::DecomposeMovement(direction, upDirection, stepOffset, isJumping, standingOnMoving);
    
    // Convert to engine type
    DecomposedMovement result{};
    result.upVector = moduleResult.upVector;
    result.sideVector = moduleResult.sideVector;
    result.downVector = moduleResult.downVector;
    result.stepOffset = moduleResult.stepOffset;
    result.isMovingUp = moduleResult.isMovingUp;
    result.hasSideMovement = moduleResult.hasSideMovement;
    
    return result;
}

PhysicsEngine::SlideResult PhysicsEngine::ExecuteUpPass(
    const PhysicsInput& input,
    MovementState& st,
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
        PHYS_INFO(PHYS_MOVE, "[UpPass] No upward movement needed");
        return result;
    }
    
    G3D::Vector3 upDir = decomposed.upVector.directionOrZero();
    float originalZ = st.z;
    
    // PhysX CCT Rule 6.2.4: UP pass uses maxIter=1 when there's side movement.
    // Our implementation uses a single sweep (effectively maxIter=1), which satisfies
    // this requirement. Iterative UP passes are only needed for pure vertical movement
    // (e.g., jumping straight up into complex ceiling geometry).
    
    // Per PHYSX_CCT_RULES.md Section 15.1: Sweep distance must include contact offset
    // to ensure we find collisions within skin-width distance
    const float contactOffset = PhysicsTol::GetContactOffset(radius);
    const float sweepDist = upMagnitude + contactOffset;
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[UpPass] Starting sweep dist=" << sweepDist << " (includes contactOffset=" << contactOffset << ")";
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    // Perform upward sweep
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(
        st.x, st.y, st.z, radius, height);
    std::vector<SceneHit> upHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(input.mapId, cap, upDir, sweepDist, upHits, playerFwd);
    
    // Find earliest blocking hit (ceiling or obstacle above)
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
        // Per PHYSX_CCT_RULES.md Section 15.1: subtract contact offset from advance distance
        // to maintain skin-width separation from ceiling (contactOffset already computed above)
        advance = std::max(0.0f, minDist - contactOffset);
        result.hitWall = true;
        result.lastHitNormal = earliest->normal.directionOrZero();
        {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[UpPass] Hit ceiling at dist=" << minDist;
            PHYS_INFO(PHYS_MOVE, oss.str());
        }
    }
    
    // Apply upward movement
    st.z += advance;
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    result.distanceMoved = advance;
    result.distanceRemaining = upMagnitude - advance;
    result.iterations = 1;
    
    // Clamp step offset to actual delta (PhysX CCT logic)
    float actualDelta = st.z - originalZ;
    clampedStepOffset = std::min(decomposed.stepOffset, actualDelta);
    
    {
        std::ostringstream oss; 
        oss.setf(std::ios::fixed); 
        oss.precision(4);
        oss << "[UpPass] Complete: advance=" << advance 
            << " actualDelta=" << actualDelta
            << " clampedStepOffset=" << clampedStepOffset
            << " newZ=" << st.z;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    return result;
}

PhysicsEngine::SlideResult PhysicsEngine::ExecuteSidePass(
    const PhysicsInput& input,
    MovementState& st,
    float radius,
    float height,
    const DecomposedMovement& decomposed)
{
    float sideMagnitude = decomposed.sideVector.magnitude();
    if (sideMagnitude < MIN_MOVE_DISTANCE) {
        SlideResult empty{};
        empty.finalPosition = G3D::Vector3(st.x, st.y, st.z);
        PHYS_INFO(PHYS_MOVE, "[SidePass] No lateral movement needed");
        return empty;
    }
    
    G3D::Vector3 sideDir = decomposed.sideVector.directionOrZero();
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[SidePass] Starting CollideAndSlide dist=" << sideMagnitude;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    // Use the full iterative CollideAndSlide for the side pass
    SlideResult result = CollideAndSlide(
        input, st, radius, height, sideDir, sideMagnitude, /*horizontalOnly*/ true);
    
    {
        std::ostringstream oss; 
        oss.setf(std::ios::fixed); 
        oss.precision(4);
        oss << "[SidePass] Complete: moved=" << result.distanceMoved
            << " remaining=" << result.distanceRemaining
            << " iterations=" << result.iterations
            << " hitWall=" << (result.hitWall ? 1 : 0)
            << " hitCorner=" << (result.hitCorner ? 1 : 0);
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    return result;
}

PhysicsEngine::SlideResult PhysicsEngine::ExecuteDownPass(
    const PhysicsInput& input,
    MovementState& st,
    float radius,
    float height,
    const DecomposedMovement& decomposed,
    float clampedStepOffset)
{
    SlideResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    
    // Store original Z for clamping
    float originalZ = st.z;
    
    // Calculate total downward distance:
    // 1. Undo the step offset (if we applied it and have side movement)
    // 2. Add any intended downward movement
    // 3. Add ground snap distance
    
    float undoStepOffset = 0.0f;
    if (decomposed.hasSideMovement) {
        undoStepOffset = clampedStepOffset;
    }
    
    float downMagnitude = decomposed.downVector.magnitude(); // Intended downward movement (negative becomes positive)
    float snapDistance = PhysicsConstants::STEP_DOWN_HEIGHT;
    
    // Total down sweep distance
    float totalDown = undoStepOffset + downMagnitude + snapDistance;
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[DownPass] Starting: undoStep=" << undoStepOffset
            << " downMagnitude=" << downMagnitude
            << " snapDist=" << snapDistance
            << " totalDown=" << totalDown;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    
    if (totalDown < MIN_MOVE_DISTANCE) {
        PHYS_INFO(PHYS_MOVE, "[DownPass] No downward movement needed");
        return result;
    }
    
    G3D::Vector3 downDir(0, 0, -1);
    
    // Perform downward sweep
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, radius, height);
    std::vector<SceneHit> downHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(input.mapId, cap, downDir, totalDown, downHits, playerFwd);

    // ---------------------------------------------------------------------
    // PhysX-style ground selection:
    // - Consider multiple walkable candidates.
    // - Prefer a candidate that results in minimal penetration after snapping.
    // - Prefer "highest valid" support (avoid snapping down onto terrain under WMOs).
    // ---------------------------------------------------------------------
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const float snapEps = 1e-4f;
    // Allow tiny overlap slop; anything larger means "this ground choice is invalid".
    const float maxAllowedPenDepth = 0.02f; // 2cm

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

    // Collect candidates (walkable first; keep non-walkable only as last resort fallback).
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
        // Temporarily snap Z and check for overlaps.
        CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, c.snapZ, radius, height);
        std::vector<SceneHit> overlaps;
        SceneQuery::SweepCapsule(input.mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);

        outMaxPenDepth = 0.0f;
        outPenCount = 0;
        for (const auto& oh : overlaps) {
            if (!oh.startPenetrating) continue;
            ++outPenCount;
            outMaxPenDepth = std::max(outMaxPenDepth, std::max(0.0f, oh.penetrationDepth));
        }

        return outMaxPenDepth <= maxAllowedPenDepth;
    };

    // Sort candidates:
    // 1) walkable first
    // 2) higher planeZ first (PhysX-like: prefer support that avoids penetration)
    // 3) earlier TOI as a tie-breaker
    std::stable_sort(candidates.begin(), candidates.end(), [&](const GroundCandidate& a, const GroundCandidate& b) {
        if (a.walkable != b.walkable) return a.walkable > b.walkable;
        if (std::fabs(a.planeZ - b.planeZ) > 1e-4f) return a.planeZ > b.planeZ;
        return a.toi < b.toi;
    });

    const GroundCandidate* chosen = nullptr;
    float chosenMaxPen = FLT_MAX;
    int chosenPenCount = 0;

    // Validate candidates in order; accept first that doesn't create significant penetration.
    for (const auto& c : candidates) {
        float maxPen = 0.0f; int penCount = 0;
        if (validateCandidate(c, maxPen, penCount)) {
            chosen = &c;
            chosenMaxPen = maxPen;
            chosenPenCount = penCount;
            break;
        }
    }

    // If no candidate is penetration-free within slop, fall back to the "least bad" walkable candidate
    // (min penetration). This mimics solver behavior where overlap correction will follow.
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

        {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(5);
            oss << "[DownPass] Landed: z=" << st.z
                << " planeZ=" << chosen->planeZ
                << " toi=" << chosen->toi
                << " nZ=" << chosen->hit->normal.z
                << " walkable=" << (chosen->walkable ? 1 : 0)
                << " penCount=" << chosenPenCount
                << " maxPen=" << chosenMaxPen;
            PHYS_INFO(PHYS_MOVE, oss.str());
        }
    } else {
        // No ground found - will transition to falling
        // IMPORTANT: Undo the step offset lift to prevent artificial height gain
        // The UP pass lifted us by clampedStepOffset for auto-step purposes,
        // but since we found no ground, we must restore the original Z before falling
        st.z -= clampedStepOffset;
        st.isGrounded = false;
        result.distanceRemaining = totalDown;
        result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
        
        {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[DownPass] No ground found - will fall, undid stepOffset=" << clampedStepOffset
                << " newZ=" << st.z;
            PHYS_INFO(PHYS_MOVE, oss.str());
        }
    }
    
    return result;
}

bool PhysicsEngine::ValidateSlopeAfterDownPass(
    const G3D::Vector3& contactNormal,
    float contactHeight,
    float originalBottomZ,
    float stepOffset) const
{
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    
    // Check if the contact normal indicates a non-walkable slope (use absolute value)
    if (std::fabs(contactNormal.z) < walkableCosMin) {
        // Additional check: only flag as non-walkable if contact is above step offset
        // This prevents flagging walkable slopes that are within step range
        float touchedTriHeight = contactHeight - originalBottomZ;
        if (touchedTriHeight > stepOffset) {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[SlopeValidate] Non-walkable slope: normalZ=" << contactNormal.z 
                << " height=" << touchedTriHeight << " > stepOffset=" << stepOffset;
            PHYS_INFO(PHYS_MOVE, oss.str());
            return false; // Non-walkable
        }
    }
    
    return true; // Walkable
}

PhysicsEngine::ThreePassResult PhysicsEngine::PerformThreePassMove(
    const PhysicsInput& input,
    MovementState& st,
    float radius,
    float height,
    const G3D::Vector3& moveDir,
    float distance,
    float dt,
    float stepOffsetOverride /*= -1.0f*/)
{
    ThreePassResult result{};
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    result.groundNormal = G3D::Vector3(0, 0, 1);
    
    float originalZ = st.z;
    G3D::Vector3 upDirection(0, 0, 1);
    
    // =========================================================================
    // Determine if player is jumping - ONLY use explicit jump flags, not velocity
    // The velocity can be artificially high from computation errors or previous
    // frame artifacts. Jump intent should come from:
    // 1. MOVEFLAG_JUMPING - player initiated a jump
    // 2. MOVEFLAG_FALLINGFAR with positive input vz - mid-jump with upward motion
    // 
    // We do NOT use st.vz > 0 alone because that could be from:
    // - Previous frame computation artifacts
    // - Slope movement
    // - External forces
    // =========================================================================
    bool hasJumpFlag = (input.moveFlags & MOVEFLAG_JUMPING) != 0;
    bool isFallingWithUpwardVelocity = ((input.moveFlags & MOVEFLAG_FALLINGFAR) != 0) && (input.vz > 0.0f);
    bool isJumping = hasJumpFlag || isFallingWithUpwardVelocity;
    
    // Additional safeguard: if grounded, we're not jumping regardless of velocity
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
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[ThreePass] Starting move dist=" << distance 
            << " isJumping=" << (isJumping ? 1 : 0)
            << " hasJumpFlag=" << (hasJumpFlag ? 1 : 0)
            << " isGrounded=" << (st.isGrounded ? 1 : 0)
            << " inputVz=" << input.vz;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    // =========================================================================
    // Step 1: Decompose movement into Up/Side/Down components
    // =========================================================================
    float stepOffset = PhysicsConstants::STEP_HEIGHT;
    if (stepOffsetOverride >= 0.0f)
        stepOffset = stepOffsetOverride;

    DecomposedMovement decomposed = DecomposeMovement(
        fullMove,
        upDirection,
        stepOffset,
        isJumping,
        standingOnMoving);
    
    // =========================================================================
    // Step 2: UP PASS - Step-up lift + any upward intent
    // =========================================================================
    float clampedStepOffset = 0.0f;
    SlideResult upResult = ExecuteUpPass(input, st, radius, height, decomposed, clampedStepOffset);
    result.collisionUp = upResult.hitWall;
    result.actualStepUpDelta = st.z - originalZ;
    
    // =========================================================================
    // Step 3: SIDE PASS - Horizontal collide-and-slide
    // =========================================================================
    SlideResult sideResult = ExecuteSidePass(input, st, radius, height, decomposed);
    result.collisionSide = sideResult.hitWall || sideResult.hitCorner;
    
    // =========================================================================
    // Step 4: DOWN PASS - Undo step offset + snap to ground
    // =========================================================================
    SlideResult downResult = ExecuteDownPass(input, st, radius, height, decomposed, clampedStepOffset);
    result.collisionDown = st.isGrounded;
    
    // =========================================================================
    // Step 5: Post-pass slope validation
    // Per PHYSX_CCT_RULES.md Section 10.3: Use the original stepOffset (not clamped)
    // for slope validation. The clamped value reflects how far we actually lifted,
    // but validation should use the configured step threshold.
    // =========================================================================
    if (st.isGrounded) {
        result.groundNormal = st.groundNormal;
        bool walkable = ValidateSlopeAfterDownPass(
            st.groundNormal,
            st.z,
            originalZ,
            stepOffset);  // Use original stepOffset per Section 10.3
        result.hitNonWalkable = !walkable;
        
        if (result.hitNonWalkable) {
            PHYS_INFO(PHYS_MOVE, "[ThreePass] Landed on non-walkable slope");
        }
    }
    
    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[ThreePass] Complete: pos=(" << st.x << "," << st.y << "," << st.z << ")"
            << " collisionUp=" << (result.collisionUp ? 1 : 0)
            << " collisionSide=" << (result.collisionSide ? 1 : 0)
            << " collisionDown=" << (result.collisionDown ? 1 : 0)
            << " hitNonWalkable=" << (result.hitNonWalkable ? 1 : 0)
            << " grounded=" << (st.isGrounded ? 1 : 0);
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    return result;
}

// -----------------------------------------------------------------------------
// Helper implementations: depenetration and sweeps
// These delegate to PhysicsGroundSnap module.
// -----------------------------------------------------------------------------

float PhysicsEngine::ApplyHorizontalDepenetration(const PhysicsInput& input,
                                       MovementState& st,
                                       float r,
                                       float h,
                                       bool walkableOnly)
{
    // Convert to module state type
    PhysicsGroundSnap::GroundSnapState snapState;
    snapState.x = st.x;
    snapState.y = st.y;
    snapState.z = st.z;
    snapState.vx = st.vx;
    snapState.vy = st.vy;
    snapState.vz = st.vz;
    snapState.orientation = st.orientation;
    snapState.isGrounded = st.isGrounded;
    snapState.groundNormal = st.groundNormal;
    
    // Delegate to extracted module
    float result = PhysicsGroundSnap::ApplyHorizontalDepenetration(input.mapId, snapState, r, h, walkableOnly);
    
    // Update movement state from result
    st.x = snapState.x;
    st.y = snapState.y;
    
    return result;
}

float PhysicsEngine::ApplyVerticalDepenetration(const PhysicsInput& input,
                                     MovementState& st,
                                     float r,
                                     float h)
{
    // Convert to module state type
    PhysicsGroundSnap::GroundSnapState snapState;
    snapState.x = st.x;
    snapState.y = st.y;
    snapState.z = st.z;
    snapState.vx = st.vx;
    snapState.vy = st.vy;
    snapState.vz = st.vz;
    snapState.orientation = st.orientation;
    snapState.isGrounded = st.isGrounded;
    snapState.groundNormal = st.groundNormal;
    
    // Delegate to extracted module
    float result = PhysicsGroundSnap::ApplyVerticalDepenetration(input.mapId, snapState, r, h);
    
    // Update movement state from result
    st.z = snapState.z;
    st.vz = snapState.vz;
    st.isGrounded = snapState.isGrounded;
    st.groundNormal = snapState.groundNormal;
    
    return result;
}

float PhysicsEngine::HorizontalSweepAdvance(const PhysicsInput& input,
                                 const MovementState& st,
                                 float r,
                                 float h,
                                 const G3D::Vector3& dir,
                                 float dist)
{
    return PhysicsGroundSnap::HorizontalSweepAdvance(
        input.mapId, st.x, st.y, st.z, st.orientation, r, h, dir, dist);
}

bool PhysicsEngine::VerticalSweepSnapDown(const PhysicsInput& input,
                               MovementState& st,
                               float r,
                               float h,
                               float maxDown)
{
    // Convert to module state type
    PhysicsGroundSnap::GroundSnapState snapState;
    snapState.x = st.x;
    snapState.y = st.y;
    snapState.z = st.z;
    snapState.vx = st.vx;
    snapState.vy = st.vy;
    snapState.vz = st.vz;
    snapState.orientation = st.orientation;
    snapState.isGrounded = st.isGrounded;
    snapState.groundNormal = st.groundNormal;
    
    // Delegate to extracted module
    bool result = PhysicsGroundSnap::VerticalSweepSnapDown(input.mapId, snapState, r, h, maxDown);
    
    // Update movement state from result
    st.z = snapState.z;
    st.vz = snapState.vz;
    st.isGrounded = snapState.isGrounded;
    st.groundNormal = snapState.groundNormal;
    
    return result;
}

bool PhysicsEngine::PerformVerticalPlacementOrFall(const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float r,
    float h,
    float dt,
    float moveSpeed,
    const char* reasonLog)
{
    bool snapped = TryDownwardStepSnap(input, st, r, h);
    if (!snapped) {
        st.isGrounded = false;
        // Only process vertical falling here to avoid double-applying XY when a ground move already occurred.
        if (st.vz >= 0.0f) st.vz = -0.1f;
        // Apply gravity and vertical displacement without changing XY
        const float vz0 = st.vz;
        const float dz = vz0 * dt - 0.5f * GRAVITY * dt * dt;
        ApplyGravity(st, dt);
        st.z += dz;
        // Perform downward CCD to clamp to ground if encountered
        {
            const float stepDownLimit = PhysicsConstants::STEP_DOWN_HEIGHT;
            CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z - dz, r, h);
            G3D::Vector3 downDir(0, 0, -1);
            float sweepDist = std::max(0.0f, dz < 0.0f ? -dz : 0.0f) + stepDownLimit;
            std::vector<SceneHit> downHits;
            G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
            SceneQuery::SweepCapsule(input.mapId, cap, downDir, sweepDist, downHits, playerFwd);
            const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
            const SceneHit* bestNP = nullptr; float bestTOI = FLT_MAX; float bestZ = -FLT_MAX;
            for (const auto& hhit : downHits) {
                if (hhit.startPenetrating) continue;
                if (std::fabs(hhit.normal.z) < walkableCosMin) continue;
                bool better = false;
                if (!bestNP) better = true; else {
                    if ((hhit.instanceId == 0) && (bestNP->instanceId != 0)) better = true;
                    else if ((hhit.instanceId == bestNP->instanceId)) {
                        if (hhit.distance < bestTOI - 1e-6f) better = true;
                        else if (std::fabs(hhit.distance - bestTOI) <= 1e-6f && hhit.point.z < bestZ) better = true;
                    }
                }
                if (better) { bestNP = &hhit; bestTOI = hhit.distance; bestZ = hhit.point.z; }
            }
            if (bestNP) {
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
        }
        return false;
    }
    return true;
}

void PhysicsEngine::GroundMoveElevatedSweep(const PhysicsInput& input,
    const MovementIntent& intent,
    MovementState& st,
    float r,
    float h,
    const G3D::Vector3& moveDir,
    float intendedDist,
    float dt,
    float moveSpeed)
{
    G3D::Vector3 dirN = moveDir.directionOrZero();
    dirN.z = 0.0f;
    dirN = dirN.directionOrZero();
    
    if (dirN.magnitude() < 1e-6f || intendedDist < MIN_MOVE_DISTANCE) {
        // No horizontal movement - just handle vertical placement
        HandleNoHorizontalMovement(input, intent, st, r, h, dirN, intendedDist, dt, moveSpeed);
        return;
    }

    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);

    // =========================================================================
    // Use the new 3-pass movement system (PhysX CCT style)
    // UP → SIDE → DOWN
    // =========================================================================
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[GroundMove] Starting 3-pass movement dist=" << intendedDist;
        PHYS_INFO(PHYS_MOVE, oss.str());
    }
    
    ThreePassResult result = PerformThreePassMove(input, st, r, h, dirN, intendedDist, dt);
    
    {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[GroundMove] 3-pass result: "
            << " collisionUp=" << (result.collisionUp ? 1 : 0)
            << " collisionSide=" << (result.collisionSide ? 1 : 0)
            << " collisionDown=" << (result.collisionDown ? 1 : 0)
            << " hitNonWalkable=" << (result.hitNonWalkable ? 1 : 0)
            << " pos=(" << st.x << "," << st.y << "," << st.z << ")";
        PHYS_INFO(PHYS_MOVE, oss.str());
    }

    // Resolve any remaining horizontal overlaps
    ApplyHorizontalDepenetration(input, st, r, h, /*walkableOnly*/ true);

    // =========================================================================
    // Handle non-walkable slope or no ground
    // =========================================================================
    
    if (result.hitNonWalkable) {
		// PhysX default behavior (PxControllerNonWalkableMode::ePREVENT_CLIMBING):
		// do NOT forcibly fall when standing on a non-walkable slope. Instead, prevent
		// upward progress (handled by the 3-pass/walk-experiment logic) and allow the
		// controller to remain grounded.
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Non-walkable slope - prevent climbing (stay grounded)");
		st.isGrounded = true;
		st.vz = 0.0f;

		// Ensure horizontal velocity doesn't contain any vertical component.
		// If callers need force-sliding behavior, that should be implemented as a
		// separate mode with iterative down-pass sliding.
		st.vx = 0.0f;
		st.vy = 0.0f;
		return;
    }

    if (!st.isGrounded) {
        // No ground found within range: start falling
        // Note: ExecuteDownPass should have already undone the step offset,
        // so st.z is at the correct position for falling
        PHYS_INFO(PHYS_MOVE, "[GroundMove] No ground - transitioning to air movement");
        // Reset vertical velocity to start falling - do NOT preserve any positive vz
        // that might have been set from previous frame artifacts
        st.vz = -0.1f;
        ProcessAirMovement(input, intent, st, dt, moveSpeed);
    } else {
        // Grounded - set horizontal velocity
        G3D::Vector3 vProj = dirN * moveSpeed;
        st.vx = vProj.x; 
        st.vy = vProj.y; 
        st.vz = 0.0f;
    }
}

// =====================================================================================
// SECTION 6: MOVEMENT HELPERS
// Intent building, speed calculation, gravity, and movement plan computation.
// =====================================================================================

PhysicsEngine::MovementIntent PhysicsEngine::BuildMovementIntent(const PhysicsInput& input, float orientation) const
{
	// Delegate to pure helper to compute directional intent and jump flag.
	auto pure = PhysicsHelpers::BuildMovementIntent(input.moveFlags, orientation);
	MovementIntent intent{};
	intent.dir = pure.dir;
	intent.hasInput = pure.hasInput;
	intent.jumpRequested = pure.jumpRequested;
	return intent;
}

float PhysicsEngine::CalculateMoveSpeed(const PhysicsInput& input, bool swim)
{
	return PhysicsHelpers::CalculateMoveSpeed(
		input.moveFlags, input.runSpeed, input.walkSpeed, 
		input.runBackSpeed, input.swimSpeed, swim);
}

void PhysicsEngine::ApplyGravity(MovementState& st, float dt)
{
    st.vz -= GRAVITY * dt; 
    if (st.vz < -60.0f) 
        st.vz = -60.0f;
}

// =====================================================================================
// SECTION 7: GROUND SNAP HELPERS
// TryDownwardStepSnap and related ground detection utilities.
// These delegate to PhysicsGroundSnap module.
// =====================================================================================

bool PhysicsEngine::TryDownwardStepSnap(const PhysicsInput& input,
	MovementState& st,
	float r,
	float h)
{
    // Convert to module state type
    PhysicsGroundSnap::GroundSnapState snapState;
    snapState.x = st.x;
    snapState.y = st.y;
    snapState.z = st.z;
    snapState.vx = st.vx;
    snapState.vy = st.vy;
    snapState.vz = st.vz;
    snapState.orientation = st.orientation;
    snapState.isGrounded = st.isGrounded;
    snapState.groundNormal = st.groundNormal;
    
    // Delegate to extracted module
    bool result = PhysicsGroundSnap::TryDownwardStepSnap(input.mapId, snapState, r, h);
    
    // Update movement state from result
    st.z = snapState.z;
    st.vz = snapState.vz;
    st.isGrounded = snapState.isGrounded;
    st.groundNormal = snapState.groundNormal;
    
    return result;
}

// =====================================================================================
// SECTION 8: AIR MOVEMENT
// Handles falling/jumping physics with gravity and ground detection.
// Delegates to PhysicsMovement module.
// =====================================================================================

void PhysicsEngine::ProcessAirMovement(
    const PhysicsInput& input, 
    const MovementIntent& intent,
    MovementState& st, 
    float dt, 
    float speed)
{
    // Convert to module types
    PhysicsMovement::MovementState moveState;
    moveState.x = st.x;
    moveState.y = st.y;
    moveState.z = st.z;
    moveState.vx = st.vx;
    moveState.vy = st.vy;
    moveState.vz = st.vz;
    moveState.orientation = st.orientation;
    moveState.pitch = st.pitch;
    moveState.isGrounded = st.isGrounded;
    moveState.isSwimming = st.isSwimming;
    moveState.fallTime = st.fallTime;
    moveState.groundNormal = st.groundNormal;
    
    PhysicsMovement::MovementIntent moveIntent;
    moveIntent.dir = intent.dir;
    moveIntent.hasInput = intent.hasInput;
    moveIntent.jumpRequested = intent.jumpRequested;
    
    // Delegate to extracted module
    PhysicsMovement::ProcessAirMovement(input, moveIntent, moveState, dt, speed);
    
    // Update movement state from result
    st.x = moveState.x;
    st.y = moveState.y;
    st.z = moveState.z;
    st.vx = moveState.vx;
    st.vy = moveState.vy;
    st.vz = moveState.vz;
    st.isGrounded = moveState.isGrounded;
    st.fallTime = moveState.fallTime;
    st.groundNormal = moveState.groundNormal;
}

// =====================================================================================
// SECTION 9: SWIM MOVEMENT
// Handles underwater movement with pitch-based vertical control.
// Delegates to PhysicsMovement module.
// =====================================================================================

void PhysicsEngine::ProcessSwimMovement(
    const PhysicsInput& input, 
    const MovementIntent& intent,
    MovementState& st, 
    float dt, 
    float speed)
{
    // Convert to module types
    PhysicsMovement::MovementState moveState;
    moveState.x = st.x;
    moveState.y = st.y;
    moveState.z = st.z;
    moveState.vx = st.vx;
    moveState.vy = st.vy;
    moveState.vz = st.vz;
    moveState.orientation = st.orientation;
    moveState.pitch = st.pitch;
    moveState.isGrounded = st.isGrounded;
    moveState.isSwimming = st.isSwimming;
    moveState.fallTime = st.fallTime;
    moveState.groundNormal = st.groundNormal;
    
    PhysicsMovement::MovementIntent moveIntent;
    moveIntent.dir = intent.dir;
    moveIntent.hasInput = intent.hasInput;
    moveIntent.jumpRequested = intent.jumpRequested;
    
    // Delegate to extracted module
    PhysicsMovement::ProcessSwimMovement(input, moveIntent, moveState, dt, speed);
    
    // Update movement state from result
    st.x = moveState.x;
    st.y = moveState.y;
    st.z = moveState.z;
    st.vx = moveState.vx;
    st.vy = moveState.vy;
    st.vz = moveState.vz;
}

// =====================================================================================
// SECTION 10: MAIN ENTRY POINT (StepV2)
// The primary physics simulation step function.
// =====================================================================================

PhysicsOutput PhysicsEngine::StepV2(const PhysicsInput& input, float dt)
{
	// Log input at the beginning
	LogStepInputSummary(input, dt);

	// NOTE (PhysX alignment): PhysX CCT's SweepTest::moveCharacter does not take a dt and
	// always operates on a caller-provided displacement for the frame. Our StepV2 is a
	// higher-level MMO movement integrator (WoW-like) that must handle variable/zero dt
	// calls from the game loop/network layer.
	// We intentionally treat dt<=0 as a non-simulating query to keep output stable.
	// If called with a non-positive dt, treat this as a non-simulating query.
	// Avoid applying gravity/sweeps with dt==0, and keep output stable.
	if (dt <= 0.0f) {
		PhysicsOutput out{};
		out.x = input.x;
		out.y = input.y;
		out.z = input.z;
		out.orientation = input.orientation;
		out.pitch = input.pitch;
		// Preserve caller-provided velocities; with dt<=0 we cannot reliably integrate or recompute.
		out.vx = input.vx;
		out.vy = input.vy;
		out.vz = input.vz;
		out.moveFlags = input.moveFlags;

		// Keep liquid outputs consistent even on dt<=0.
		SceneQuery::LiquidInfo liq = SceneQuery::EvaluateLiquidAt(input.mapId, input.x, input.y, input.z);
		out.liquidZ = liq.level;
		out.liquidType = liq.type;
		if (liq.isSwimming)
			out.moveFlags |= MOVEFLAG_SWIMMING;
		else
			out.moveFlags &= ~MOVEFLAG_SWIMMING;

		out.groundZ = input.z;
		PHYS_INFO(PHYS_MOVE, "[StepV2] dt<=0; returning output without simulation");
		return out;
	}

	PhysicsOutput out{};
	if (!m_initialized) {
		out.x = input.x;
		out.y = input.y;
		out.z = input.z;
		out.orientation = input.orientation;
		out.pitch = input.pitch;
		out.vx = input.vx;
		out.vy = input.vy;
		out.vz = input.vz;
		out.moveFlags = input.moveFlags;
		return out;
	}

	float r = input.radius;
	float h = input.height;
    

	MovementState st{};
	st.x = input.x; st.y = input.y; st.z = input.z;
	st.orientation = input.orientation; st.pitch = input.pitch;
	st.vx = input.vx; st.vy = input.vy; st.vz = input.vz; st.fallTime = input.fallTime;
	st.groundNormal = { 0,0,1 };
	const bool inputSwimmingFlag = (input.moveFlags & MOVEFLAG_SWIMMING) != 0;
	const bool inputAirborneFlag = (input.moveFlags & (MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR)) != 0;
	const bool inputFlyingFlag = (input.moveFlags & (MOVEFLAG_FLYING | MOVEFLAG_LEVITATING | MOVEFLAG_HOVER)) != 0;
	// NOTE (stateless MMO): input flags represent the caller's last-frame state.
	// We preserve these unless StepV2 simulation detects a real state transition.
	// We still use queries to *inform* grounding, but we avoid immediately overriding
	// airborne flags purely from a pre-probe.
	st.isGrounded = !(inputSwimmingFlag || inputFlyingFlag || inputAirborneFlag);
	const bool hasPrevGround = (input.prevGroundZ > PhysicsConstants::INVALID_HEIGHT) && (input.prevGroundNz > 0.0f);
	if (!st.isGrounded && hasPrevGround) {
		float groundDelta = std::fabs(st.z - input.prevGroundZ);
		if (groundDelta <= PhysicsConstants::STEP_DOWN_HEIGHT)
			st.isGrounded = true;
	}

	// Track previous position for actual velocity computation
	G3D::Vector3 prevPos(st.x, st.y, st.z);

	// ---------------------------------------------------------------------
	// Apply deferred depenetration from previous tick (R1 intent).
	// ---------------------------------------------------------------------
	{
		// NOTE (PhysX alignment): PhysX performs overlap recovery/corrections as part of the
		// controller pipeline (e.g., Controller::move applies mOverlapRecover to the frame
		// displacement). We keep a small deferred depenetration vector in the MMO layer and
		// apply it at the start of the tick for stability across frames/network updates.
		G3D::Vector3 pending(input.pendingDepenX, input.pendingDepenY, input.pendingDepenZ);
		if (pending.magnitude() > 1e-6f) {
			st.x += pending.x;
			st.y += pending.y;
			st.z += pending.z;
			PHYS_INFO(PHYS_MOVE, std::string("[OverlapRecover] applied pending depen (")
				<< pending.x << "," << pending.y << "," << pending.z << ")");
		}
	}

	MovementIntent intent = BuildMovementIntent(input, st.orientation);

	// Evaluate liquid to decide swim vs ground/air (use SceneQuery directly)
	auto liq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	bool isSwimming = liq.isSwimming;
	if (isSwimming) {
		st.isGrounded = false;
	}
	st.isSwimming = isSwimming;
	const bool isFlying = inputFlyingFlag;
	const bool isRooted = (input.moveFlags & MOVEFLAG_ROOT) != 0;

	// ---------------------------------------------------------------------
	// PhysX-like pre-move ground probe (findTouchedObject concept).
	// Grounded should primarily be determined by queries, not by stale flags.
	// ---------------------------------------------------------------------
	{
		// NOTE (PhysX alignment): In PhysX, support tracking (touched shape/obstacle) is
		// handled inside Controller::move/rideOnTouchedObject and uses the scene query system.
		// StepV2 is not a full PxController implementation, so we approximate this with a
		// simple downward probe to keep WoW-style grounded state stable.
		// NOTE (stateless MMO): we probe even when airborne to get a candidate support normal,
		// but we do not force the grounded state/flags to change based on this probe alone.
		// Grounding transitions should be driven by the DOWN pass / placement logic.
		if (!isSwimming && !isFlying) {
			const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
			const float probeDist = PhysicsConstants::STEP_DOWN_HEIGHT;
			CapsuleCollision::Capsule capProbe = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
			std::vector<SceneHit> downHits;
			G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
			SceneQuery::SweepCapsule(input.mapId, capProbe, G3D::Vector3(0, 0, -1), probeDist, downHits, playerFwd);

			const SceneHit* best = PhysSelect::FindEarliestWalkableNonPen(downHits, walkableCosMin);
			if (!best) {
				// Fallback: accept a penetrating walkable contact as being 'on ground' (repositional).
				const SceneHit* bestPen = nullptr;
				float bestPenZ = -FLT_MAX;
				for (const auto& hhit : downHits) {
					if (!hhit.startPenetrating) continue;
					if (std::fabs(hhit.normal.z) < walkableCosMin) continue;
					if (!bestPen || hhit.point.z > bestPenZ) {
						bestPen = &hhit;
						bestPenZ = hhit.point.z;
					}
				}
				best = bestPen;
			}

			if (best) {
				// Detect support surface and update grounded state only.
				// Do not snap/adjust Z here; vertical placement is handled by the move passes.
				st.groundNormal = best->normal.directionOrZero();
			} else {
				// Leave grounded state unchanged here; the move pipeline will decide.
			}
		}
	}

	// ---------------------------------------------------------------------
	// PhysX-like initial overlap recovery (R16/R17 intent).
	// If we start the tick penetrating geometry, attempt to depenetrate with
	// bounded iterations before doing any movement sweeps.
	// ---------------------------------------------------------------------
	G3D::Vector3 deferredDepen(0, 0, 0);
	if (!isSwimming && !isFlying) {
		// NOTE (PhysX alignment): PhysX can run overlap recovery inside doSweepTest when
		// mUserParams.mOverlapRecovery is enabled (computeMTD path). We do a simplified,
		// bounded depenetration pre-pass here because our MMO controller is not based on
		// PhysX geometry types and we need deterministic behavior across content (terrain/WMO).
		constexpr int kMaxRecoverIters = 4;
		float totalRecovered = 0.0f;
		for (int i = 0; i < kMaxRecoverIters; ++i) {
			// Using existing helpers as a first-class overlap recovery step.
			// Vertical first (most common: clipped into ground), then horizontal.
			float dz = ApplyVerticalDepenetration(input, st, r, h);
			float dxy = ApplyHorizontalDepenetration(input, st, r, h, /*walkableOnly*/ false);
			float step = dz + dxy;
			totalRecovered += step;
			if (step <= 1e-6f)
				break;
		}

		// If we still start penetrating after recovery, compute a deferred depenetration
		// vector from remaining penetrations using a zero-distance overlap sweep.
		// This prefers resolving along the most separating direction (sum of normals)
		// instead of always biasing upward.
		{
			CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
			std::vector<SceneHit> overlaps;
			G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
			SceneQuery::SweepCapsule(input.mapId, capHere, G3D::Vector3(0, 0, 0), 0.0f, overlaps, playerFwd);

			G3D::Vector3 depenSum(0, 0, 0);
			int penCount = 0;
			for (const auto& oh : overlaps) {
				if (!oh.startPenetrating) continue;
				float d = std::max(0.0f, oh.penetrationDepth);
				if (d <= 1e-6f) continue;
				G3D::Vector3 n = oh.normal.directionOrZero();
				if (n.magnitude() <= 1e-6f) continue;
				depenSum += n * d;
				++penCount;
			}

			// Conservative per-tick clamp (PhysX-style).
			// Keep this small to avoid tunneling/overshoot.
			const float maxDeferredDepen = 0.05f;
			float mag = depenSum.magnitude();
			if (penCount > 0 && mag > 1e-6f) {
				deferredDepen = depenSum * (std::min(maxDeferredDepen, mag) / mag);
			}
		}

		if (totalRecovered > 1e-6f) {
			std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(5);
			oss << "[OverlapRecover] total=" << totalRecovered
				<< " pos=(" << st.x << "," << st.y << "," << st.z << ")";
			PHYS_INFO(PHYS_MOVE, oss.str());
		}
	}

	// -------------------------------------------------------------------------
	// PhysX-style initial volume query with FULL direction vector.
	// -------------------------------------------------------------------------
	// NOTE (PhysX alignment): In PhysX CCT, Controller::move performs an initial
	// temporal bounding volume query using the FULL direction vector BEFORE
	// decomposing movement into UP/SIDE/DOWN passes. This is critical because:
	//   "the main difference between this initial query and subsequent ones is
	//    that we use the full direction vector here, not the components along
	//    each axis. So there is a good chance that this initial query will
	//    contain all the motion we need, and thus subsequent queries will be
	//    skipped." -- CctCharacterController.cpp
	//
	// We approximate this by performing an early sweep using the full intended
	// displacement. This pre-caches geometry that might be touched during any
	// of the three movement passes.
	// -------------------------------------------------------------------------
	PhysicsHelpers::MovementPlan plan = PhysicsHelpers::BuildMovementPlan(
		input.moveFlags, input.orientation, 
		input.runSpeed, input.walkSpeed, input.runBackSpeed, input.swimSpeed,
		intent.hasInput, dt, isSwimming);
	
	// Log the movement plan
	{
		std::ostringstream oss; 
		oss.setf(std::ios::fixed); 
		oss.precision(4);
		oss << "[Intent] hasInput=" << (plan.hasInput ? 1 : 0)
			<< " flags=0x" << std::hex << input.moveFlags << std::dec
			<< " dir=(" << plan.dir.x << "," << plan.dir.y << ")"
			<< " speed=" << plan.speed << " dist=" << plan.dist << " dt=" << dt
			<< (isSwimming ? " swim" : ((input.moveFlags & MOVEFLAG_WALK_MODE) ? " walk" : " run"));
		PHYS_INFO(PHYS_MOVE, oss.str());
	}

	// -------------------------------------------------------------------------
	// PhysX-style initial volume query (PHYSX_CCT_RULES.md Section 5)
	// -------------------------------------------------------------------------
	// PhysX CCT pre-fetches geometry using a temporal bounding box that encompasses
	// all possible positions during the frame. Our tile-based caching approximates
	// this by performing a forward sweep that triggers geometry loading. The actual
	// collision detection occurs in the UP/SIDE/DOWN passes.
	// -------------------------------------------------------------------------
	if (!isSwimming && !isFlying && plan.hasInput && plan.dist > MIN_MOVE_DISTANCE) {
		G3D::Vector3 fullDirection = plan.dir * plan.dist;

		// Rule 4.2.6: Cancel stepOffset when jumping (not on moving platform).
		const float stepOffset = st.isGrounded ? PhysicsConstants::STEP_HEIGHT : 0.0f;

		CapsuleCollision::Capsule capTemporal = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);

		// Sweep distance per Rules 5.2 & 15.7: displacement + stepUp + stepDown + contactOffset
		const float contactOffset = PhysicsTol::GetContactOffset(r);
		float temporalSweepDist = plan.dist + stepOffset + PhysicsConstants::STEP_DOWN_HEIGHT + contactOffset;

		std::vector<SceneHit> temporalHits;
		G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
		SceneQuery::SweepCapsule(input.mapId, capTemporal, fullDirection.directionOrZero(), temporalSweepDist, temporalHits, playerFwd);

		// NOTE: PhysX populates mGeomStream with additional vertical sweeps here.
		// Our tile-level caching makes this redundant - geometry is cached on first access.

		{
			std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
			oss << "[InitialVolumeQuery] fullDir=(" << fullDirection.x << "," << fullDirection.y << "," << fullDirection.z << ")"
				<< " dist=" << plan.dist
				<< " temporalSweepDist=" << temporalSweepDist
				<< " stepOffset=" << stepOffset
				<< " hits=" << temporalHits.size();
			PHYS_INFO(PHYS_MOVE, oss.str());
		}
	}
    float moveSpeed = plan.speed;
    G3D::Vector3 moveDir = plan.dir;
    float intendedDist = plan.dist;
    bool planHasInput = plan.hasInput;

    if (isFlying) {
        moveSpeed = input.flightSpeed;
        intendedDist = moveSpeed * dt;
    }
    if (isRooted) {
        moveSpeed = 0.0f;
        intendedDist = 0.0f;
        moveDir = G3D::Vector3(0, 0, 0);
        planHasInput = false;
    }

    // Removed SceneQuery::ComputeCapsuleSweep diagnostics and manifold usage

    if (isFlying) {
		st.isGrounded = false;
		st.isSwimming = false;
		if (planHasInput && moveSpeed > 0.0f) {
			st.vx = moveDir.x * moveSpeed;
			st.vy = moveDir.y * moveSpeed;
		}
		if (isRooted) {
			st.vx = 0.0f;
			st.vy = 0.0f;
		}
		float climbVz = intent.hasInput ? std::sin(st.pitch) * moveSpeed : st.vz;
		st.vz = climbVz;
		st.x += st.vx * dt;
		st.y += st.vy * dt;
		st.z += st.vz * dt;
	}
	else if (isSwimming) {
		st.isGrounded = false;
		st.isSwimming = true;
		ProcessSwimMovement(input, intent, st, dt, moveSpeed);
	}
	else if (!st.isGrounded) {
		st.isSwimming = false;
		if (planHasInput && moveSpeed > 0.0f) {
			st.vx = moveDir.x * moveSpeed;
			st.vy = moveDir.y * moveSpeed;
		}
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
	else if (intent.jumpRequested) {
		// Immediate jump
		st.vz = PhysicsConstants::JUMP_VELOCITY;
		st.isGrounded = false;
		st.isSwimming = false;
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
    else {
		// Ground movement.
		// NOTE: GroundMoveElevatedSweep uses a PhysX-style UP→SIDE→DOWN pipeline and already
		// handles vertical placement/falling as part of the DOWN pass.
		if (intendedDist > 0.0f) {
			// First pass: regular ground move (UP→SIDE→DOWN)
			MovementState preMove = st;
			GroundMoveElevatedSweep(input, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);

			// If we ended grounded on a non-walkable slope, do a PhysX-style "walk experiment"
			// second pass: restore pre-move pose and retry with stepOffset cancelled.
			//
			// In PhysX, the walk experiment rerun cancels stepOffset injection (no auto-step lift)
			// and attempts a purely lateral move constrained by the non-walkable contact.
			// Here we approximate that by running the 3-pass mover with STEP_OFFSET = 0.
			// (Prevents unintended stepping onto steep slopes while still allowing standing.)
			const bool endedOnNonWalkable = st.isGrounded && (std::fabs(st.groundNormal.z) < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z);
			if (endedOnNonWalkable) {
				MovementState retry = preMove;
				(void)PerformThreePassMove(input, retry, r, h, moveDir, intendedDist, dt, /*stepOffsetOverride*/ 0.0f);
				st = retry;
			}
		} else {
			// Idle while grounded: still need to settle to ground / begin falling if ground vanished.
			PerformVerticalPlacementOrFall(input, intent, st, r, h, dt, moveSpeed, "idle: vertical placement");
		}
        // Post-step penetration diagnostics: check for any remaining overlaps
        {
            CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
            std::vector<SceneHit> overlaps;
            G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
            SceneQuery::SweepCapsule(input.mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);
            int penCount = 0, walkablePen = 0, sidePen = 0; float maxDepth = 0.0f;
            for (const auto& oh : overlaps) {
                if (!oh.startPenetrating) continue;
                ++penCount; maxDepth = std::max(maxDepth, std::max(0.0f, oh.penetrationDepth));
                if (oh.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) ++walkablePen;
                if (oh.region == SceneHit::CapsuleRegion::Side) ++sidePen;
            }
            if (penCount > 0) {
                std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
                oss << "[DepenDiag] post VerticalPlacement overlaps count=" << penCount
                    << " walkable=" << walkablePen << " side=" << sidePen
                    << " maxDepth=" << maxDepth
                    << " at pos=(" << st.x << "," << st.y << "," << st.z << ")";
                PHYS_INFO(PHYS_MOVE, oss.str());
            }
        }
    }

	SceneQuery::LiquidInfo finalLiq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	if (finalLiq.isSwimming && !isSwimming) {
		if (finalLiq.hasLevel) {
			st.z = std::max(st.z, finalLiq.level - PhysicsConstants::WATER_LEVEL_DELTA);
		}
		st.vx *= 0.5f;
		st.vy *= 0.5f;
		st.vz = 0.0f;
		st.isGrounded = false;
	}
	else if (!finalLiq.isSwimming && isSwimming) {
		st.isGrounded = st.isGrounded && !finalLiq.isSwimming;
	}
	isSwimming = finalLiq.isSwimming;
	st.isSwimming = isSwimming;

	// Compute actual velocity based on position delta over dt for this step
	G3D::Vector3 curPos(st.x, st.y, st.z);
	G3D::Vector3 actualV(0, 0, 0);
	if (dt > 0.0f)
		actualV = (curPos - prevPos) * (1.0f / dt);
    else
        PHYS_INFO(PHYS_MOVE, "[StepV2] Non-positive dt; skipping velocity calc");

	// Suppress vertical component unless airborne or swimming.
	// PhysX-style: grounded state should dominate; don't infer airborne from velocity alone.
	bool airborne = !st.isGrounded;
	if (!airborne && !isSwimming) {
		actualV.z = 0.0f;
	}

	// Output
	out.x = st.x; out.y = st.y; out.z = st.z;
	out.orientation = st.orientation; out.pitch = st.pitch;
	out.vx = actualV.x; out.vy = actualV.y; out.vz = actualV.z;
	out.moveFlags = input.moveFlags;
	if (isSwimming) out.moveFlags |= MOVEFLAG_SWIMMING; else out.moveFlags &= ~MOVEFLAG_SWIMMING;

	// Update movement flags for V2
	// Clear JUMPING unless jump was requested this frame
	if (!intent.jumpRequested) {
		out.moveFlags &= ~MOVEFLAG_JUMPING;
	}
	// Mark falling when not grounded and vertical velocity negative
	if (!st.isGrounded && st.vz < 0.0f) {
		out.moveFlags |= MOVEFLAG_FALLINGFAR; // use existing flag to indicate falling
	}
	else {
		out.moveFlags &= ~MOVEFLAG_FALLINGFAR;
	}

	out.groundZ = st.z;
	out.liquidZ = finalLiq.level;
	out.liquidType = finalLiq.type;
	out.groundNx = st.groundNormal.x;
	out.groundNy = st.groundNormal.y;
	out.groundNz = st.groundNormal.z;

	out.pendingDepenX = deferredDepen.x;
	out.pendingDepenY = deferredDepen.y;
	out.pendingDepenZ = deferredDepen.z;

	out.standingOnInstanceId = input.standingOnInstanceId;
	out.standingOnLocalX = input.standingOnLocalX;
	out.standingOnLocalY = input.standingOnLocalY;
	out.standingOnLocalZ = input.standingOnLocalZ;
	// Sync SWIMMING flag with final liquid evaluation
	if (finalLiq.isSwimming) {
		const uint32_t incompatibleSwim =
			MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR | MOVEFLAG_FLYING | MOVEFLAG_ROOT |
			MOVEFLAG_PENDING_STOP | MOVEFLAG_PENDING_UNSTRAFE | MOVEFLAG_PENDING_FORWARD |
			MOVEFLAG_PENDING_BACKWARD | MOVEFLAG_PENDING_STR_LEFT | MOVEFLAG_PENDING_STR_RGHT;
		out.moveFlags |= MOVEFLAG_SWIMMING;
		out.moveFlags &= ~incompatibleSwim;
		if (intent.hasInput && !(out.moveFlags & (MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT)))
			out.moveFlags |= MOVEFLAG_FORWARD;
	}
	else {
		out.moveFlags &= ~MOVEFLAG_SWIMMING;
	}
	// Output summary log
	{
		std::ostringstream oss;
		oss << "[StepV2] OutputSummary frame=" << input.frameCounter << "\n"
			<< "  pos=(" << out.x << "," << out.y << "," << out.z << ")\n"
			<< "  velOut=(" << out.vx << "," << out.vy << "," << out.vz << ")\n"
			<< "  flags=0x" << std::hex << out.moveFlags << std::dec << "\n"
			<< "  groundZ=" << out.groundZ << " liquidZ=" << out.liquidZ << " liquidType=" << static_cast<int>(out.liquidType);
		PHYS_INFO(PHYS_MOVE, oss.str());
	}
	return out;
}