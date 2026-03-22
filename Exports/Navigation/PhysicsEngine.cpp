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
#include "DynamicObjectRegistry.h"

// Extracted physics modules
#include "PhysicsCollideSlide.h"
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
    if (nH.magnitude() > PhysicsConstants::VECTOR_EPSILON) {
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
        if (std::fabs(oh.normal.z) >= PhysicsConstants::OVERLAP_NORMAL_Z_FILTER)
            continue;
            
        G3D::Vector3 nH(oh.normal.x, oh.normal.y, 0.0f);
        if (nH.magnitude() <= PhysicsConstants::VECTOR_EPSILON) 
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
    if (nH.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
        PHYS_INFO(PHYS_MOVE, "[Slide] skipped: invalid horizontal normal");
        return;
    }
    nH = nH.directionOrZero();
    
    // Project intended direction onto the contact plane (tangent)
    G3D::Vector3 slideDir = (dirN - nH * dirN.dot(nH));
    slideDir.z = 0.0f; 
    slideDir = slideDir.directionOrZero();
    
    float slideIntended = remaining;
    if (slideDir.magnitude() <= PhysicsConstants::VECTOR_EPSILON || slideIntended <= PhysicsConstants::VECTOR_EPSILON)
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
        if (hh.distance < PhysicsConstants::VECTOR_EPSILON) 
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
// =====================================================================================

PhysicsEngine::DecomposedMovement PhysicsEngine::DecomposeMovement(
    const G3D::Vector3& direction,
    const G3D::Vector3& upDirection,
    float stepOffset,
    bool isJumping,
    bool standingOnMoving) const
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
    
    // Find earliest blocking hit (ceiling or obstacle above).
    // Filter out ground contacts: if the hit normal points upward (Z > walkable threshold),
    // it's a floor/slope the capsule is sitting on, not a ceiling above. This prevents
    // false ceiling hits when the capsule bottom hemisphere is slightly embedded in sloped terrain.
    const SceneHit* earliest = nullptr;
    float minDist = FLT_MAX;
    for (const auto& hit : upHits) {
        if (!hit.hit || hit.startPenetrating)
            continue;
        if (hit.distance < PhysicsConstants::VECTOR_EPSILON)
            continue;
        // Skip ground/slope contacts — upward-facing normals are floors, not ceilings
        if (hit.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z)
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
    const float snapEps = PhysicsConstants::GROUND_SNAP_EPSILON;
    // Tightened penetration tolerance (Phase 1b): 0.5× radius instead of 1.0× radius.
    // Previous tolerance allowed contacts where the capsule center was nearly inside geometry,
    // masking bad candidates. Half-radius still permits natural wall contact when walking
    // near WMO walls but rejects deeper overlaps that indicate wrong-floor candidates.
    const float maxAllowedPenDepth = radius * 0.5f;

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

    // The pre-step Z is the character's actual position before the UP pass lifted them.
    // We only accept snap candidates within a reasonable step-down distance from this.
    // The full sweep range (STEP_DOWN_HEIGHT) is used to FIND surfaces, but the actual
    // snap is limited to avoid reaching lower floors in multi-story WMO buildings.
    // For walkable surfaces (slopes), allow a larger snap distance (STEP_DOWN_HEIGHT)
    // because walking downhill at speed can cover significant vertical distance per frame.
    // Non-walkable candidates are still limited to STEP_HEIGHT + 0.5 to prevent wrong-floor snaps.
    const float preStepZ = originalZ - undoStepOffset;
    const float maxSnapDownWalkable = PhysicsConstants::STEP_DOWN_HEIGHT;  // 4.0y for walkable slopes
    const float maxSnapDownNonWalkable = PhysicsConstants::STEP_HEIGHT + 0.5f; // ~2.6y for walls/steep

    // Collect candidates (walkable first; keep non-walkable only as last resort fallback).
    for (const auto& hit : downHits) {
        if (!hit.hit || hit.startPenetrating) continue;
        if (hit.distance < PhysicsConstants::VECTOR_EPSILON) continue;

        const bool walkable = (std::fabs(hit.normal.z) >= walkableCosMin);

        float nx = hit.normal.x, ny = hit.normal.y, nz = hit.normal.z;
        float px = hit.point.x, py = hit.point.y, pz = hit.point.z;
        float planeZ = pz;
        if (std::fabs(nz) > PhysicsConstants::VECTOR_EPSILON) {
            planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
        }

        float snapZ = planeZ + snapEps;
        // WoW.exe CollisionStep (0x633840): the AABB's maxZ is lifted by
        // min(2*radius, speed*dt) ABOVE the post-sweep position, allowing
        // the character to step UP onto surfaces above the starting Z.
        // Only clamp non-walkable surfaces (walls/ceilings) to originalZ.
        // Walkable surfaces (ground) may be above originalZ on uphill slopes.
        if (snapZ > originalZ && !walkable) snapZ = originalZ;

        // Reject candidates too far below the character's real position.
        // Walkable surfaces use a larger snap limit to handle downhill slopes.
        // Non-walkable surfaces use a tighter limit to avoid wrong-floor snaps.
        const float maxSnap = walkable ? maxSnapDownWalkable : maxSnapDownNonWalkable;
        if (snapZ < preStepZ - maxSnap) continue;

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
            // Skip walkable (floor-like) surfaces — the capsule naturally contacts
            // the ground it's standing on; only count wall/ceiling penetrations.
            if (oh.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) continue;
            ++outPenCount;
            outMaxPenDepth = std::max(outMaxPenDepth, std::max(0.0f, oh.penetrationDepth));
        }

        return outMaxPenDepth <= maxAllowedPenDepth;
    };

    // Sort candidates:
    // 1) walkable first
    // 2) closest to preStepZ first (avoids snapping to wrong floor in multi-level areas)
    //    Previous "highest first" sort caused the sim to lock onto WMO bridges/ramps
    //    2y above the actual ground when STEP_HEIGHT (2.125y) lifted the capsule above them.
    // 3) earlier TOI as tie-breaker
    std::stable_sort(candidates.begin(), candidates.end(), [&](const GroundCandidate& a, const GroundCandidate& b) {
        if (a.walkable != b.walkable) return a.walkable > b.walkable;
        float errA = std::fabs(a.planeZ - preStepZ);
        float errB = std::fabs(b.planeZ - preStepZ);
        if (std::fabs(errA - errB) > PhysicsConstants::GROUND_SNAP_EPSILON) return errA < errB;
        return a.toi < b.toi;
    });

    const GroundCandidate* chosen = nullptr;
    float chosenMaxPen = FLT_MAX;
    int chosenPenCount = 0;

    // Validate candidates in order; accept first that doesn't create significant penetration.
    // Skip overlap validation for candidates near the character's current Z level.
    // On continent maps with WMO buildings, nearby wall geometry causes false rejections
    // of correct ADT terrain candidates. Only validate candidates significantly below
    // the pre-step position (potential wrong-floor candidates in multi-story WMOs).
    const float validationThreshold = preStepZ - PhysicsConstants::STEP_HEIGHT - 0.5f;
    for (const auto& c : candidates) {
        float maxPen = 0.0f; int penCount = 0;
        bool nearCurrentLevel = (c.snapZ >= validationThreshold);
        if (nearCurrentLevel || validateCandidate(c, maxPen, penCount)) {
            chosen = &c;
            chosenMaxPen = maxPen;
            chosenPenCount = penCount;
            break;
        }
    }

    // Step-up enhancement: when auto-stepping (undoStepOffset > 0), the strict validation
    // above may reject a higher walkable candidate because the capsule at step-top Z overlaps
    // the step's vertical face. This overlap is expected geometry — the character is stepping
    // ONTO the higher surface and the face is below their feet.
    // Re-check higher candidates with relaxed tolerance (up to capsule radius).
    // Limit step-up to 1.5y above preStepZ to avoid snapping to bridges/upper floors.
    if (chosen && undoStepOffset > 0.0f) {
        const float stepUpPenTolerance = radius + PhysicsConstants::STEP_UP_PEN_TOLERANCE_EXTRA;
        const float maxStepUpZ = preStepZ + PhysicsConstants::MAX_STEP_UP_ABOVE_PRE_STEP;
        const GroundCandidate* stepUpBest = nullptr;
        float stepUpBestPen = FLT_MAX;
        int stepUpBestPenCount = 0;
        for (const auto& c : candidates) {
            // Only consider candidates higher than current choice
            if (c.snapZ <= chosen->snapZ + 0.01f) continue; // skip lower/equal
            if (!c.walkable) continue;
            // Don't promote candidates unreasonably far above the pre-step position
            if (c.planeZ > maxStepUpZ) continue;

            float maxPen = 0.0f; int penCount = 0;
            (void)validateCandidate(c, maxPen, penCount);
            if (maxPen <= stepUpPenTolerance) {
                // Track highest valid step-up candidate (candidates not sorted by height)
                if (!stepUpBest || c.snapZ > stepUpBest->snapZ) {
                    stepUpBest = &c;
                    stepUpBestPen = maxPen;
                    stepUpBestPenCount = penCount;
                }
            }
        }
        if (stepUpBest) {
            chosen = stepUpBest;
            chosenMaxPen = stepUpBestPen;
            chosenPenCount = stepUpBestPenCount;

            {
                std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
                oss << "[DownPass] Step-up: promoted higher candidate z=" << stepUpBest->snapZ
                    << " pen=" << stepUpBestPen << " (tolerance=" << stepUpPenTolerance << ")";
                PHYS_INFO(PHYS_MOVE, oss.str());
            }
        }
    }

    // Fallback #2C (ELIMINATED): Do NOT accept "least-bad" walkable candidate when all candidates
    // have unacceptable penetration. Accepting bad geometry here masks missing/overlapping collision
    // surfaces and produces subtle Z errors. Log the would-be rescue so the geometry gap can be fixed.
    // The Z clamp in MovementController (#4) handles post-teleport frames; genuine geometry holes
    // should be fixed in SceneCache/PhysicsEngine, not papered over here.
    if (!chosen && !candidates.empty()) {
        const GroundCandidate* leastBad = nullptr;
        float leastBadPen = FLT_MAX;
        int leastBadPenCount = 0;
        for (const auto& c : candidates) {
            if (!c.walkable) continue;
            float maxPen = 0.0f; int penCount = 0;
            (void)validateCandidate(c, maxPen, penCount);
            if (!leastBad || maxPen < leastBadPen) {
                leastBad = &c;
                leastBadPen = maxPen;
                leastBadPenCount = penCount;
            }
        }
        if (leastBad) {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(3);
            oss << "[DownPass] GEOMETRY_GAP: least-bad walkable suppressed at ("
                << st.x << ", " << st.y << ", " << leastBad->snapZ
                << ") pen=" << leastBadPen << " penCount=" << leastBadPenCount
                << " map=" << input.mapId << " -- fix collision geometry at this position";
            PHYS_INFO(PHYS_MOVE, oss.str());
        }
        // chosen intentionally left nullptr -- let physics transition to falling/no-ground
    }

    if (chosen && chosen->hit) {
        st.z = chosen->snapZ;

        // Refine Z with direct height query at exact XY (eliminates capsule lateral offset bias).
        // In grounded replay-trust mode we allow a larger upward correction to avoid one-frame
        // lag when stepping onto slightly higher support.
        const bool trustGroundedReplayInput =
            ((input.physicsFlags & PHYSICS_FLAG_TRUST_INPUT_VELOCITY) != 0) &&
            ((input.moveFlags & (MOVEFLAG_SWIMMING | MOVEFLAG_FLYING | MOVEFLAG_LEVITATING | MOVEFLAG_HOVER |
                                 MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR)) == 0);
        const float preciseRiseTolerance = trustGroundedReplayInput ? 0.2f : 0.05f;
        // Downward correction tolerance: the capsule sweep picks candidates by TOI and
        // plane projection, which on steep slopes can place the character above the actual
        // terrain surface at the capsule center. GetGroundZ is the ray-cast ground truth.
        // Allow correction down to the full step-down distance for walkable candidates,
        // but limit to 0.5y for non-walkable to prevent wrong-floor snaps in WMOs.
        const float preciseFallTolerance = chosen->walkable
            ? PhysicsConstants::STEP_DOWN_HEIGHT
            : 0.5f;
        float preciseZ = SceneQuery::GetGroundZ(input.mapId, st.x, st.y, st.z,
            PhysicsConstants::STEP_DOWN_HEIGHT);
        // When the character is BELOW the collision surface (snapZ was capped to originalZ
        // because planeZ > originalZ), GetGroundZ can fire through the surface and return
        // terrain below — which would embed the character even deeper. Only accept refinement
        // that stays near or above the collision plane in this case.
        const bool belowSurface = chosen->planeZ > originalZ + 0.1f;
        const float planeFloor = belowSurface ? chosen->planeZ - 0.5f : st.z - preciseFallTolerance;
        if (VMAP::IsValidHeight(preciseZ) &&
            preciseZ <= st.z + preciseRiseTolerance &&
            preciseZ >= planeFloor) {
            st.z = preciseZ;
        }

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
        // No ground found from capsule sweep.
        // IMPORTANT: Undo the step offset lift to prevent artificial height gain.
        st.z -= clampedStepOffset;

        // Ray-cast fallback: on steep terrain, the capsule DOWN sweep can miss ground
        // that a simple vertical ray finds. This is because the capsule's width causes
        // it to miss thin terrain geometry at steep angles. Fall back to GetGroundZ
        // when the character was previously grounded, not on a transport, and the ray
        // finds ground within STEP_DOWN_HEIGHT of the pre-step position.
        bool rayFallbackUsed = false;
        const bool prevGrounded = (input.fallTime == 0) &&
            ((input.moveFlags & (MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR)) == 0);
        if (prevGrounded && input.transportGuid == 0) {
            // WoW.exe step height adjustment (0x633E06): lifts bbox.maxZ by
            // min(2*radius, speed*dt), then extends minZ down by radius + speed*dt*tan(50°).
            // This creates a search volume that catches ground on BOTH uphill and downhill.
            // Our ray query must also accept ground above (uphill) within step height.
            float rayZ = SceneQuery::GetGroundZ(input.mapId, st.x, st.y,
                st.z + PhysicsConstants::STEP_HEIGHT,  // probe from above step height
                PhysicsConstants::STEP_DOWN_HEIGHT + PhysicsConstants::STEP_HEIGHT + 1.0f);
            if (VMAP::IsValidHeight(rayZ) &&
                rayZ <= st.z + PhysicsConstants::STEP_HEIGHT &&  // within step-up range
                rayZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT) {
                st.z = rayZ;
                st.isGrounded = true;
                st.vz = 0.0f;
                // Estimate terrain normal from finite differences
                const float probeOffset = PhysicsConstants::NORMAL_PROBE_OFFSET;
                float zPx = SceneQuery::GetGroundZ(input.mapId, st.x + probeOffset, st.y, st.z + 2.0f, 10.0f);
                float zNx = SceneQuery::GetGroundZ(input.mapId, st.x - probeOffset, st.y, st.z + 2.0f, 10.0f);
                float zPy = SceneQuery::GetGroundZ(input.mapId, st.x, st.y + probeOffset, st.z + 2.0f, 10.0f);
                float zNy = SceneQuery::GetGroundZ(input.mapId, st.x, st.y - probeOffset, st.z + 2.0f, 10.0f);
                if (VMAP::IsValidHeight(zPx) && VMAP::IsValidHeight(zNx) &&
                    VMAP::IsValidHeight(zPy) && VMAP::IsValidHeight(zNy)) {
                    float dzdx = (zPx - zNx) / (2.0f * probeOffset);
                    float dzdy = (zPy - zNy) / (2.0f * probeOffset);
                    st.groundNormal = G3D::Vector3(-dzdx, -dzdy, 1.0f).directionOrZero();
                } else {
                    st.groundNormal = G3D::Vector3(0, 0, 1);
                }
                if (st.groundNormal.z < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) {
                    // Non-walkable slope — let the character slide/fall instead
                    st.isGrounded = false;
                } else {
                    rayFallbackUsed = true;
                    result.finalPosition = G3D::Vector3(st.x, st.y, st.z);
                    result.hitWall = false;
                    result.lastHitNormal = st.groundNormal;
                    {
                        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
                        oss << "[DownPass] Ray-cast fallback: z=" << st.z
                            << " normal=(" << st.groundNormal.x << "," << st.groundNormal.y << "," << st.groundNormal.z << ")";
                        PHYS_INFO(PHYS_MOVE, oss.str());
                    }
                }
            }
        }

        if (!rayFallbackUsed) {
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
    bool wasGrounded = st.isGrounded;
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

    float afterUpZ = st.z;
    float afterUpX = st.x, afterUpY = st.y;

    // =========================================================================
    // Step 3: SIDE PASS - Horizontal collide-and-slide
    // =========================================================================
    SlideResult sideResult = ExecuteSidePass(input, st, radius, height, decomposed);
    result.collisionSide = sideResult.hitWall || sideResult.hitCorner;
    result.lastSideHitNormal = sideResult.lastHitNormal;
    {
        float sideTotal = sideResult.distanceMoved + sideResult.distanceRemaining;
        result.sideBlockedFraction = (sideTotal > 0.001f) ? (sideResult.distanceMoved / sideTotal) : 1.0f;
    }

    float afterSideX = st.x, afterSideY = st.y, afterSideZ = st.z;
    float sideDist = std::sqrt((afterSideX - afterUpX)*(afterSideX - afterUpX) + (afterSideY - afterUpY)*(afterSideY - afterUpY));

    // Diagnostic: when side pass achieves <50% of intended distance
    if (sideDist < distance * 0.5f) {
        std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
        oss << "[SIDE_LOW] intended=" << distance << " sideDist=" << sideDist
            << " hitWall=" << (sideResult.hitWall ? 1 : 0)
            << " hitCorner=" << (sideResult.hitCorner ? 1 : 0)
            << " distMoved=" << sideResult.distanceMoved
            << " distRemain=" << sideResult.distanceRemaining
            << " afterUp=(" << afterUpX << "," << afterUpY << "," << afterUpZ << ")"
            << " afterSide=(" << afterSideX << "," << afterSideY << "," << afterSideZ << ")";
        PHYS_ERR(PHYS_MOVE, oss.str());
    }

    // =========================================================================
    // Step 3b: LEDGE GUARD - Prevent walking off elevated surfaces
    // When the character was grounded and has moved horizontally, probe ground
    // at the new XY. If the only ground is significantly below the pre-step Z,
    // the character is walking off a ledge (e.g. pier edge, bridge edge).
    // Undo the horizontal movement to prevent snapping down to terrain below.
    // Without this, thin WMO floors (piers, docks) with no vertical edge walls
    // let the capsule slide past the edge; the DOWN pass then snaps to terrain
    // several yards below, making the character walk through support posts.
    // =========================================================================
    // LEDGE GUARD REMOVED — WoW.exe (0x633840) does NOT have a ledge guard.
    // The client's 2-pass AABB sweep handles terrain drops naturally:
    //   - If the DOWN pass finds walkable ground → character moves there
    //   - If the DOWN pass finds no ground → character enters freefall
    // Our previous LedgeGuard used GetGroundZ ray probes (ADT-only, can't see WMO/M2)
    // which caused false positives on normal hilly terrain (Valley of Trials, Durotar).
    // The DOWN pass + freefall entry is the correct mechanism for handling ledges.
    (void)wasGrounded; (void)isJumping; (void)sideDist; (void)afterUpX; (void)afterUpY; (void)afterUpZ;

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
        if (st.vz >= 0.0f) st.vz = PhysicsConstants::FALL_START_VELOCITY;
        // Two-phase fall displacement (WoW.exe 0x7C5E70 parity)
        const float vz0 = st.vz;
        const float termVel = (input.moveFlags & MOVEFLAG_SAFE_FALL)
            ? PhysicsConstants::SAFE_FALL_TERMINAL_VELOCITY
            : PhysicsConstants::TERMINAL_VELOCITY;
        float speed0 = -vz0;
        if (speed0 > termVel) speed0 = termVel;
        float newSpeed = speed0 + GRAVITY * dt;
        float dz;
        if (newSpeed <= termVel) {
            dz = -(speed0 * dt + HALF_GRAVITY * dt * dt);
        } else {
            float t_accel = (termVel - speed0) * PhysicsConstants::INV_GRAVITY;
            float d_accel = speed0 * t_accel + HALF_GRAVITY * t_accel * t_accel;
            float t_const = dt - t_accel;
            dz = -(d_accel + t_const * termVel);
        }
        ApplyGravity(st, dt, input.moveFlags);
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
                        if (hhit.distance < bestTOI - PhysicsConstants::VECTOR_EPSILON) better = true;
                        else if (std::fabs(hhit.distance - bestTOI) <= PhysicsConstants::VECTOR_EPSILON && hhit.point.z < bestZ) better = true;
                    }
                }
                if (better) { bestNP = &hhit; bestTOI = hhit.distance; bestZ = hhit.point.z; }
            }
            if (bestNP) {
                float nx = bestNP->normal.x, ny = bestNP->normal.y, nz = bestNP->normal.z;
                float px = bestNP->point.x,  py = bestNP->point.y,  pz = bestNP->point.z;
                float snapZ = pz;
                if (std::fabs(nz) > PhysicsConstants::VECTOR_EPSILON) {
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
    
    if (dirN.magnitude() < PhysicsConstants::VECTOR_EPSILON || intendedDist < MIN_MOVE_DISTANCE) {
        // No horizontal movement - just handle vertical placement
        HandleNoHorizontalMovement(input, intent, st, r, h, dirN, intendedDist, dt, moveSpeed);
        return;
    }

    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);

    // Save pre-move Z for cliff detection after 3-pass
    const float preMoveZ = st.z;
    // Save entry position for zero-displacement diagnostic
    const float entryX = st.x, entryY = st.y, entryZ = st.z;

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

    // Snapshot position after 3-pass (before post-move fixups)
    const float after3passX = st.x, after3passY = st.y, after3passZ = st.z;

    {
        float aDx = st.x - entryX, aDy = st.y - entryY;
        float aDist = std::sqrt(aDx*aDx + aDy*aDy);
        if (aDist < intendedDist * 0.5f) {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[3PASS_LOW] intended=" << intendedDist << " achieved=" << aDist
                << " collUp=" << (result.collisionUp ? 1 : 0)
                << " collSide=" << (result.collisionSide ? 1 : 0)
                << " collDown=" << (result.collisionDown ? 1 : 0)
                << " nonWalk=" << (result.hitNonWalkable ? 1 : 0)
                << " entry=(" << entryX << "," << entryY << "," << entryZ << ")"
                << " post=(" << st.x << "," << st.y << "," << st.z << ")";
            PHYS_ERR(PHYS_MOVE, oss.str());
        }
    }

    // Propagate wall contact info to MovementState so StepV2 can relay it to PhysicsOutput
    if (result.collisionSide) {
        st.wallHit = true;
        st.wallHitNormal = result.lastSideHitNormal;
        st.wallBlockedFraction = result.sideBlockedFraction;
    }

    // Resolve any remaining horizontal overlaps
    float postMoveDepenDist = 0.0f;
    {
        float preDepenX = st.x, preDepenY = st.y;
        ApplyHorizontalDepenetration(input, st, r, h, /*walkableOnly*/ true);
        postMoveDepenDist = std::sqrt((st.x-preDepenX)*(st.x-preDepenX) + (st.y-preDepenY)*(st.y-preDepenY));
        if (postMoveDepenDist > 0.001f) {
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[PostMoveHDepen] pushback=" << postMoveDepenDist;
            PHYS_INFO(PHYS_MOVE, oss.str());
        }
    }
    // Snapshot position after post-move depen (before ledge guard)
    const float afterDepenX = st.x, afterDepenY = st.y;

    // =========================================================================
    // Handle non-walkable slope or no ground
    // =========================================================================
    
    if (result.hitNonWalkable) {
		// Non-walkable slope detected after 3-pass. Don't zero velocity here — let the
		// caller (StepV2) handle the walk experiment retry with stepOffset=0.
		// Just flag the state and return so the caller can decide.
		PHYS_INFO(PHYS_MOVE, "[GroundMove] Non-walkable slope - returning for walk experiment");
		st.isGrounded = true;
		st.vz = 0.0f;
		return;
    }

    if (!st.isGrounded) {
        // DOWN pass didn't find ground — enter freefall.
        // Preserve the SIDE pass horizontal displacement for this frame,
        // and carry forward horizontal momentum so subsequent airborne frames
        // (where fallTime > 0 locks velocity) don't stall at zero.
        PHYS_INFO(PHYS_MOVE, "[GroundMove] No ground found — entering freefall");
        const float sweepX = st.x;
        const float sweepY = st.y;
        st.vx = 0.0f;
        st.vy = 0.0f;
        st.vz = PhysicsConstants::FALL_START_VELOCITY;
        ProcessAirMovement(input, intent, st, dt, moveSpeed);
        st.x = sweepX;
        st.y = sweepY;
        // Preserve horizontal momentum for the airborne velocity lock.
        // Without this, vx=vy=0 persists through fallTime>0 frames → oscillation.
        if (intendedDist > 0.0f && moveSpeed > 0.0f) {
            st.vx = dirN.x * moveSpeed;
            st.vy = dirN.y * moveSpeed;
        }
    } else {
        // The 3-pass found walkable ground. Trust the DOWN pass result.
        // Previously, a cliff guard here compared prevGroundZ vs st.z and
        // entered freefall when the drop exceeded a per-frame threshold.
        // This caused the bot to stall on terrain with rapid Z variation
        // (e.g. Durotar road Z oscillates 5-13 over short distances).
        // Real cliffs are handled by the !st.isGrounded branch above —
        // if there's no ground within STEP_DOWN_HEIGHT, the DOWN pass
        // fails and the character enters freefall naturally.
        {
            // Normal grounded movement - set horizontal velocity
            G3D::Vector3 vProj = dirN * moveSpeed;
            st.vx = vProj.x;
            st.vy = vProj.y;
            st.vz = 0.0f;
        }
    }

    // =========================================================================
    // ALWAYS-ON zero-displacement diagnostic (PHYS_ERR = always visible)
    // Fires when GroundMoveElevatedSweep produces < 50% of intended distance.
    // Reports which sub-step was responsible: 3-pass, post-depen, or ledge guard.
    // =========================================================================
    {
        float finalDx = st.x - entryX;
        float finalDy = st.y - entryY;
        float finalDist = std::sqrt(finalDx*finalDx + finalDy*finalDy);
        if (finalDist < intendedDist * 0.5f) {
            // Compute 3-pass displacement
            float passAchieved = std::sqrt((after3passX - entryX)*(after3passX - entryX) + (after3passY - entryY)*(after3passY - entryY));
            // Check if ledge guard fired (position == afterDepenX/Y means no ledge guard, or afterUpX/Y if ledge guard reverted)
            bool ledgeReverted = (std::fabs(st.x - afterDepenX) > 0.001f || std::fabs(st.y - afterDepenY) > 0.001f);
            std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
            oss << "[STUCK_DIAG] intended=" << intendedDist
                << " final=" << finalDist
                << " 3pass=" << passAchieved
                << " postDepen=" << postMoveDepenDist
                << " ledgeRevert=" << (ledgeReverted ? 1 : 0)
                << " collSide=" << (result.collisionSide ? 1 : 0)
                << " collUp=" << (result.collisionUp ? 1 : 0)
                << " nonWalk=" << (result.hitNonWalkable ? 1 : 0)
                << " grounded=" << (st.isGrounded ? 1 : 0)
                << " pos=(" << st.x << "," << st.y << "," << st.z << ")"
                << " entry=(" << entryX << "," << entryY << "," << entryZ << ")";
            PHYS_ERR(PHYS_MOVE, oss.str());
        }
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
		input.runBackSpeed, input.swimSpeed, input.swimBackSpeed, swim);
}

void PhysicsEngine::ApplyGravity(MovementState& st, float dt, uint32_t moveFlags)
{
    const float termVel = (moveFlags & MOVEFLAG_SAFE_FALL)
        ? PhysicsConstants::SAFE_FALL_TERMINAL_VELOCITY
        : PhysicsConstants::TERMINAL_VELOCITY;
    st.vz -= GRAVITY * dt;
    if (st.vz < -termVel)
        st.vz = -termVel;
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

	// WoW.exe CMovement::Update (0x618D0D): clamps per-frame delta to [-500ms, +1000ms].
	// Values outside this range indicate frame stalls or clock jumps that would cause
	// teleport-like movement. The clamp prevents physics divergence on lag spikes.
	constexpr float MAX_DT = 1.0f;       // 1000ms in seconds
	constexpr float MIN_DT = -0.5f;      // -500ms (backward time correction)
	if (dt > MAX_DT) dt = MAX_DT;
	// Negative dt → non-simulating (same as dt<=0 path below)
	if (dt <= 0.0f) dt = 0.0f;

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
		out.hitWall = false;
		out.wallNormalX = 0.0f; out.wallNormalY = 0.0f; out.wallNormalZ = 1.0f;
		out.blockedFraction = 1.0f;
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

	// ---- Dynamic objects: register/update from PhysicsInput ----
	if (input.nearbyObjects && input.nearbyObjectCount > 0)
	{
		auto* dynReg = DynamicObjectRegistry::Instance();
		for (int i = 0; i < input.nearbyObjectCount; ++i)
		{
			const auto& obj = input.nearbyObjects[i];
			dynReg->EnsureRegistered(obj.guid, obj.displayId, input.mapId, obj.scale);
			dynReg->UpdatePosition(obj.guid, obj.x, obj.y, obj.z, obj.orientation, obj.goState);
		}
	}

	// ---- Transport-local → world coordinate transform ----
	float simX = input.x, simY = input.y, simZ = input.z;
	float simO = input.orientation;
	if (input.transportGuid != 0 && input.nearbyObjects)
	{
		for (int i = 0; i < input.nearbyObjectCount; ++i)
		{
			if (input.nearbyObjects[i].guid == input.transportGuid)
			{
				const auto& transport = input.nearbyObjects[i];
				float cosO = cosf(transport.orientation);
				float sinO = sinf(transport.orientation);
				simX = input.x * cosO - input.y * sinO + transport.x;
				simY = input.x * sinO + input.y * cosO + transport.y;
				simZ = input.z + transport.z;
				simO = input.orientation + transport.orientation;
				break;
			}
		}
	}

	float r = input.radius;
	float h = input.height;

	MovementState st{};
	st.x = simX; st.y = simY; st.z = simZ;
	st.orientation = simO; st.pitch = input.pitch;
	st.vx = input.vx; st.vy = input.vy; st.vz = input.vz;
	st.fallTime = input.fallTime / 1000.0f;  // Convert ms (from client) → seconds for internal physics
	st.fallStartZ = input.fallStartZ;
	st.groundNormal = { 0,0,1 };
	const bool inputSwimmingFlag = (input.moveFlags & MOVEFLAG_SWIMMING) != 0;
	const bool inputAirborneFlag = (input.moveFlags & (MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR)) != 0;
	const bool inputFlyingFlag = (input.moveFlags & (MOVEFLAG_FLYING | MOVEFLAG_LEVITATING | MOVEFLAG_HOVER)) != 0;
	const bool trustInputVel = (input.physicsFlags & PHYSICS_FLAG_TRUST_INPUT_VELOCITY) != 0;
	const bool trustGroundedReplayInput = trustInputVel && !inputSwimmingFlag && !inputFlyingFlag && !inputAirborneFlag;
	// When caller provides exact velocity for airborne frames, the trajectory is fully
	// determined by physics (gravity + provided velocity). Skip overlap recovery and
	// deferred depenetration to avoid displacing the start position — these corrections
	// are for runtime stability but introduce error in replay calibration.
	const bool trustAirborneReplayInput = trustInputVel && inputAirborneFlag;
	// NOTE (stateless MMO): input flags represent the caller's last-frame state.
	// We preserve these unless StepV2 simulation detects a real state transition.
	// We still use queries to *inform* grounding, but we avoid immediately overriding
	// airborne flags purely from a pre-probe.
	st.isGrounded = !(inputSwimmingFlag || inputFlyingFlag || inputAirborneFlag);
	const bool hasPrevGround = (input.prevGroundZ > PhysicsConstants::INVALID_HEIGHT) && (input.prevGroundNz > 0.0f);
	// Only recover grounded from prevGroundZ when NO airborne flags are set.
	// When JUMPING/FALLINGFAR is active, the character IS airborne regardless of
	// proximity to ground. The old check was too aggressive (STEP_DOWN_HEIGHT=4.0y
	// exceeds max jump height ~1.64y), causing mid-jump frames to be treated as grounded.
	if (!st.isGrounded && hasPrevGround && !inputAirborneFlag) {
		float groundDelta = std::fabs(st.z - input.prevGroundZ);
		if (groundDelta <= PhysicsConstants::STEP_DOWN_HEIGHT)
			st.isGrounded = true;
	}

	// Track previous position for actual velocity computation
	G3D::Vector3 prevPos(st.x, st.y, st.z);
	const bool wasGroundedAtStart = st.isGrounded;

	// ---------------------------------------------------------------------
	// Apply deferred depenetration from previous tick (R1 intent).
	// ---------------------------------------------------------------------
	{
		// NOTE (PhysX alignment): PhysX performs overlap recovery/corrections as part of the
		// controller pipeline (e.g., Controller::move applies mOverlapRecover to the frame
		// displacement). We keep a small deferred depenetration vector in the MMO layer and
		// apply it at the start of the tick for stability across frames/network updates.
		//
		// Replay calibration mode (trusted grounded velocity) should derive displacement from
		// captured frame deltas only, so skip carry-over depen application in that path.
		if (!trustGroundedReplayInput && !trustAirborneReplayInput) {
			G3D::Vector3 pending(input.pendingDepenX, input.pendingDepenY, input.pendingDepenZ);
			if (pending.magnitude() > PhysicsConstants::VECTOR_EPSILON) {
				st.x += pending.x;
				st.y += pending.y;
				st.z += pending.z;
				PHYS_INFO(PHYS_MOVE, std::string("[OverlapRecover] applied pending depen (")
					<< pending.x << "," << pending.y << "," << pending.z << ")");
			}
		}
	}

	// =========================================================================
	// MOVEMENT FLAG RESTRICTIONS (WoW.exe parity)
	// =========================================================================
	// When airborne, directional input is ignored — horizontal velocity is frozen
	// from launch moment. When rooted, all movement is blocked.
	// We create a masked copy of input for BuildMovementIntent so the direction
	// vector reflects actual allowed movement, while preserving original flags
	// for output (server expects them for validation).
	uint32_t effectiveFlags = input.moveFlags;
	if (inputAirborneFlag) {
		// Airborne: strip directional bits — no air control in WoW
		effectiveFlags &= ~PhysicsConstants::AIRBORNE_BLOCKED_BITS;
	}
	if (input.moveFlags & MOVEFLAG_ROOT) {
		// Rooted: strip all movement bits
		effectiveFlags &= ~PhysicsConstants::ROOTED_BLOCKED_BITS;
	}

	// Build intent from restricted flags (determines direction vector)
	PhysicsInput maskedInput = input;
	maskedInput.moveFlags = effectiveFlags;
	MovementIntent intent = BuildMovementIntent(maskedInput, st.orientation);

	// Evaluate liquid to decide swim vs ground/air (use SceneQuery directly)
	auto liq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	// Use liquid query OR movement flags for swim detection.
	// MOVEFLAG_SWIMMING is authoritative (set by server) and acts as fallback
	// when ADT/VMAP liquid data is unavailable (e.g. river without liquid mesh).
	bool isSwimming = liq.isSwimming || inputSwimmingFlag;
	// In replay trust mode, movement flags are authoritative for swim state.
	// The liquid query can falsely detect swimming near the water surface for
	// frames that are actually airborne (JUMPING out of water). This misroutes
	// through ProcessSwimMovement which ignores trusted velocity, causing errors.
	if (trustInputVel && !inputSwimmingFlag && inputAirborneFlag) {
		isSwimming = false;
	}
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
				if (bestPen) {
					std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(3);
					oss << "[PreMove] RESCUE: penetrating walkable contact at ("
						<< st.x << ", " << st.y << ", " << st.z
						<< ") penZ=" << bestPenZ << " map=" << input.mapId;
					PHYS_INFO(PHYS_MOVE, oss.str());
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
	if (!isSwimming && !isFlying && !trustGroundedReplayInput && !trustAirborneReplayInput) {
		// NOTE (PhysX alignment): PhysX can run overlap recovery inside doSweepTest when
		// mUserParams.mOverlapRecovery is enabled (computeMTD path). We do a simplified,
		// bounded depenetration pre-pass here because our MMO controller is not based on
		// PhysX geometry types and we need deterministic behavior across content (terrain/WMO).
		float totalRecovered = 0.0f;
		const bool preserveAirborne = inputAirborneFlag;
		const float savedVz = st.vz;
		for (int i = 0; i < PhysicsConstants::MAX_OVERLAP_RECOVER_ITERATIONS; ++i) {
			// Using existing helpers as a first-class overlap recovery step.
			// Vertical first (most common: clipped into ground), then horizontal.
			float dz = ApplyVerticalDepenetration(input, st, r, h);
			// Use walkableOnly=true so walkable ground contacts on sloped terrain are
			// resolved vertically (by ApplyVerticalDepenetration), not pushed horizontally.
			// With walkableOnly=false, the capsule's side-region contacts on sloped ground
			// generate a horizontal push that fights against forward movement, causing the
			// bot to be stuck: every frame the overlap recovery pushes backward by the same
			// amount the SidePass advances forward.
			float dxy = ApplyHorizontalDepenetration(input, st, r, h, /*walkableOnly*/ true);
			float step = dz + dxy;
			totalRecovered += step;
			if (step <= PhysicsConstants::VECTOR_EPSILON)
				break;
		}
		// Overlap recovery can falsely set isGrounded and zero vz when the character
		// has airborne flags (JUMPING/FALLINGFAR). Restore the airborne state and
		// velocity to prevent routing through the grounded-jump branch.
		if (preserveAirborne) {
			st.isGrounded = false;
			st.vz = savedVz;
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
				// Apply the same walkable + Side-region filter as the immediate
				// recovery (line 1977). Without this filter, terrain slope contacts
				// that the immediate recovery correctly ignores still produce a
				// deferred push vector that fights against forward movement every
				// frame, reducing speed to ~9% of expected.
				if (std::fabs(oh.normal.z) < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) continue;
				if (oh.region != SceneHit::CapsuleRegion::Side) continue;
				float d = std::max(0.0f, oh.penetrationDepth);
				if (d <= PhysicsConstants::VECTOR_EPSILON) continue;
				G3D::Vector3 n = oh.normal.directionOrZero();
				if (n.magnitude() <= PhysicsConstants::VECTOR_EPSILON) continue;
				depenSum += n * d;
				++penCount;
			}

			// Conservative per-tick clamp (PhysX-style).
			// Keep this small to avoid tunneling/overshoot.
			const float maxDeferredDepen = PhysicsConstants::MAX_DEFERRED_DEPEN_PER_TICK;
			float mag = depenSum.magnitude();
			if (penCount > 0 && mag > PhysicsConstants::VECTOR_EPSILON) {
				deferredDepen = depenSum * (std::min(maxDeferredDepen, mag) / mag);
			}
		}

		if (totalRecovered > PhysicsConstants::VECTOR_EPSILON) {
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
		input.runSpeed, input.walkSpeed, input.runBackSpeed, input.swimSpeed, input.swimBackSpeed,
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
	const bool trustGroundedReplay = trustInputVel && !isFlying && !isSwimming && st.isGrounded && !intent.jumpRequested;

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

	// Replay calibration mode: when caller trusts captured velocity while grounded,
	// derive the frame displacement directly from input.vx/vy but still run through
	// normal grounded collision/step logic.
	if (trustGroundedReplay) {
		const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
		if (speedSq > 1e-8f) {
			moveSpeed = std::sqrt(speedSq);
			intendedDist = moveSpeed * dt;
			moveDir = G3D::Vector3(input.vx / moveSpeed, input.vy / moveSpeed, 0.0f);
			planHasInput = intendedDist > MIN_MOVE_DISTANCE;
		}
		else {
			moveSpeed = 0.0f;
			intendedDist = 0.0f;
			moveDir = G3D::Vector3(0, 0, 0);
			planHasInput = false;
		}
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
		if (trustInputVel) {
			// Replay trust: use provided velocity for exact position matching.
			// ProcessSwimMovement recalculates velocity from intent direction/pitch
			// which doesn't perfectly match the client's swim movement model.
			st.vx = input.vx;
			st.vy = input.vy;
			st.vz = input.vz;
			st.x += st.vx * dt;
			st.y += st.vy * dt;
			st.z += st.vz * dt;
		} else {
			ProcessSwimMovement(input, intent, st, dt, moveSpeed);
		}
	}
	else if (!st.isGrounded) {
		// Airborne: the character has JUMPING or FALLINGFAR flags set.
		// Apply jump impulse ONLY when:
		//   1. JUMPING flag is set (jumpRequested)
		//   2. FALLINGFAR is NOT set (fall-from-height has both, jumps only have JUMPING)
		//   3. fallTime == 0 (first frame of airborne state)
		// When FALLINGFAR is set (with or without JUMPING), the character is falling
		// from a height — no upward impulse should be applied.
		st.isSwimming = false;
		const bool isFallingFar = (input.moveFlags & MOVEFLAG_FALLINGFAR) != 0;
		if (intent.jumpRequested && !isFallingFar && input.fallTime == 0) {
			// When trust velocity is active (replay calibration), the recording's Vz
			// encodes the exact first-frame displacement including sub-tick timing.
			// The WoW client applies jump impulse mid-tick, producing apparent Vz >> JUMP_VELOCITY.
			// Overriding with JUMP_VELOCITY would produce ~0.125y instead of the actual ~1.0y.
			if (!trustInputVel)
				st.vz = PhysicsConstants::JUMP_VELOCITY;
			PHYS_INFO(PHYS_MOVE, "[StepV2] Jump impulse applied (new jump, no FALLINGFAR)");
		}
		// Horizontal velocity: lock at takeoff, do NOT recalculate from facing each frame.
		// In WoW, once you leave the ground, horizontal velocity is fixed — only facing
		// (for camera/targeting) can change, not movement direction. Recalculating from
		// moveDir every frame allows mid-air steering which the server rejects.
		// Only set horizontal velocity on the FIRST frame of airborne state (fallTime == 0)
		// or when transitioning from grounded (wasGroundedAtStart). After that, preserve
		// the velocity from the previous frame — ProcessAirMovement uses it as-is.
		if (!trustInputVel && planHasInput && moveSpeed > 0.0f && input.fallTime == 0) {
			st.vx = moveDir.x * moveSpeed;
			st.vy = moveDir.y * moveSpeed;
		}
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
	else if (intent.jumpRequested) {
		// Grounded jump: character was grounded last frame, jump requested this frame.
		// Only apply jump impulse if FALLINGFAR is not set (a grounded character
		// pressing jump won't have FALLINGFAR).
		st.vz = PhysicsConstants::JUMP_VELOCITY;
		st.isGrounded = false;
		st.isSwimming = false;
		ProcessAirMovement(input, intent, st, dt, moveSpeed);
	}
    else {
		// Ground movement.
		// NOTE: GroundMoveElevatedSweep uses a PhysX-style UP→SIDE→DOWN pipeline and already
		// handles vertical placement/falling as part of the DOWN pass.
		if (trustGroundedReplay && intendedDist > 0.0f) {
			// Replay calibration path: run full ground sweep for step/slope Z behavior,
			// then re-lock X/Y to the trusted capture displacement.
			const float trustedX = st.x + (input.vx * dt);
			const float trustedY = st.y + (input.vy * dt);
			st.vx = input.vx;
			st.vy = input.vy;
			st.vz = 0.0f;

			GroundMoveElevatedSweep(input, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);

			// Keep replay X/Y exact while preserving sweep-derived Z.
			st.x = trustedX;
			st.y = trustedY;

			// Always re-evaluate support at trusted XY. GroundMoveElevatedSweep can transiently
			// report airborne on rising ramps/steps, and later replay fallbacks can pin Z to
			// input.z (one-frame lag). Refine here first so trusted XY drives final support Z.
			const bool wasGroundedAfterSweep = st.isGrounded;
			const bool snapped = TryDownwardStepSnap(input, st, r, h);
			// When the replay provides a non-zero Vz for a grounded frame, the character
			// is walking over a terrain step (SurfaceStep). Use the target Z directly
			// since the ground query's +0.5y search cap and maxRise=0.6y tolerance
			// miss step-up surfaces >0.75y above the starting position.
			const bool hasSurfaceStepHint = (std::fabs(input.vz) > 0.1f);
			if (hasSurfaceStepHint) {
				const float targetZ = input.z + input.vz * dt;
				// Verify the target is reachable: query ground from above the target
				float verifyZ = SceneQuery::GetGroundZ(
					input.mapId, st.x, st.y, targetZ + 1.0f, 3.0f);
				if (VMAP::IsValidHeight(verifyZ) && std::fabs(verifyZ - targetZ) < 0.2f) {
					st.z = verifyZ;
				} else {
					st.z = targetZ;
				}
				st.isGrounded = true;
				st.vz = 0.0f;
				st.fallTime = 0.0f;
			} else {
				const float refineBaseZ = std::max(st.z, input.z);
				const float maxRise = 0.60f;
				const float maxDrop = 1.0f;
				float preciseZ = SceneQuery::GetGroundZ(
					input.mapId, st.x, st.y, refineBaseZ + 0.25f,
					PhysicsConstants::STEP_DOWN_HEIGHT);
				if (VMAP::IsValidHeight(preciseZ) &&
					preciseZ <= refineBaseZ + maxRise &&
					preciseZ >= refineBaseZ - maxDrop) {
					st.z = preciseZ;
					st.isGrounded = true;
					st.vz = 0.0f;
					st.fallTime = 0.0f;
				}
			}

			// Preserve trusted horizontal velocity for replay output.
			st.vx = input.vx;
			st.vy = input.vy;
		}
		else if (intendedDist > 0.0f) {
			// First pass: regular ground move (UP→SIDE→DOWN)
			MovementState preMove = st;
			GroundMoveElevatedSweep(input, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);

			// Diagnostic: how much did the 3-pass actually move us?
			{
				float dx = st.x - preMove.x;
				float dy = st.y - preMove.y;
				float achieved = std::sqrt(dx*dx + dy*dy);
				if (achieved < intendedDist * 0.5f) {
					std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
					oss << "[GroundMoveDiag] LOW_DISPLACEMENT: intended=" << intendedDist
						<< " achieved=" << achieved << " ratio=" << (intendedDist > 0 ? achieved/intendedDist : 0)
						<< " wallHit=" << (st.wallHit ? 1 : 0)
						<< " grounded=" << (st.isGrounded ? 1 : 0)
						<< " nZ=" << st.groundNormal.z
						<< " pos=(" << st.x << "," << st.y << "," << st.z << ")"
						<< " pre=(" << preMove.x << "," << preMove.y << "," << preMove.z << ")";
					PHYS_ERR(PHYS_MOVE, oss.str());
				}
			}

			// PhysX-style "walk experiment" (see SWEEP_TEST_MOVE_CHARACTER_REFERENCE.md Step 7):
			// When the initial 3-pass lands on a non-walkable slope, we:
			// 1. Restore pre-move position
			// 2. Retry 3-pass with stepOffset=0 (no auto-step lift)
			// 3. If still on non-walkable, compute a downward recovery to undo any
			//    upward climb and slide back to walkable ground
			const bool endedOnNonWalkable = st.isGrounded && (std::fabs(st.groundNormal.z) < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z);
			if (endedOnNonWalkable) {
				float climbedHeight = st.z - preMove.z;
				PHYS_INFO(PHYS_MOVE, "[WalkExperiment] Non-walkable detected, retrying with stepOffset=0");

				MovementState retry = preMove;
				ThreePassResult retryResult = PerformThreePassMove(input, retry, r, h, moveDir, intendedDist, dt, /*stepOffsetOverride*/ 0.0f);

				// Check if the retry also ended on non-walkable
				bool retryNonWalkable = retry.isGrounded && (std::fabs(retry.groundNormal.z) < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z);

				if (retryNonWalkable && retry.z > preMove.z + 0.01f) {
					// Still on non-walkable AND climbed up: do downward recovery sweep
					// (PhysX: recover = actualRise + abs(verticalIntent))
					float recover = retry.z - preMove.z;
					if (recover > 0.01f) {
						PHYS_INFO(PHYS_MOVE, "[WalkExperiment] Recovery sweep down by " + std::to_string(recover));
						// Use CollideAndSlide for the recovery so we can slide along surfaces
						PhysicsCollideSlide::SlideState slideState;
						slideState.x = retry.x;
						slideState.y = retry.y;
						slideState.z = retry.z;
						slideState.orientation = retry.orientation;
						PhysicsCollideSlide::CollideAndSlide(
							input.mapId, slideState, r, h,
							G3D::Vector3(0, 0, -1), recover,
							/*horizontalOnly*/ false, /*preventCeilingSlide*/ false);
						retry.x = slideState.x;
						retry.y = slideState.y;
						retry.z = slideState.z;

						// Try to snap to ground after recovery
						PhysicsGroundSnap::GroundSnapState snapSt;
						snapSt.x = retry.x; snapSt.y = retry.y; snapSt.z = retry.z;
						snapSt.orientation = retry.orientation;
						snapSt.isGrounded = retry.isGrounded;
						snapSt.vz = retry.vz;
						snapSt.groundNormal = retry.groundNormal;
						if (PhysicsGroundSnap::TryDownwardStepSnap(input.mapId, snapSt, r, h)) {
							retry.x = snapSt.x; retry.y = snapSt.y; retry.z = snapSt.z;
							retry.isGrounded = true;
							retry.vz = 0.0f;
							retry.groundNormal = snapSt.groundNormal;
						}
					}
				}

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

	// Rescue occasional false-airborne outcomes: if input was not airborne and
	// we are very close to a support surface, clamp back to grounded.
	// This keeps single-frame state flips from introducing large replay deltas.
	if (!st.isGrounded && !isSwimming && !inputAirborneFlag) {
		const float probeR = std::max(0.05f, r);
		const float diagR = probeR * 0.707f;
		const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
		const bool hasMoveDir = speedSq > PhysicsConstants::VECTOR_EPSILON;
		const float invSpeed = hasMoveDir ? (1.0f / std::sqrt(speedSq)) : 0.0f;
		const float dirX = hasMoveDir ? (input.vx * invSpeed) : 0.0f;
		const float dirY = hasMoveDir ? (input.vy * invSpeed) : 0.0f;
		const float rescueReferenceZ = trustGroundedReplayInput ? std::max(st.z, input.z) : st.z;
		// GetGroundZ selects the candidate closest to query Z; in replay-trust mode
		// probing too high can bias toward overhead surfaces and miss nearby walk support.
		const float queryHeights[4] = {
			rescueReferenceZ + (trustGroundedReplayInput ? 0.05f : 0.20f),
			rescueReferenceZ + (trustGroundedReplayInput ? 0.30f : 0.35f),
			rescueReferenceZ + (trustGroundedReplayInput ? 0.65f : 0.55f),
			rescueReferenceZ + (trustGroundedReplayInput ? 0.95f : 0.75f)
		};
		// WoW.exe CollisionStep (0x633E06): search volume extends down by
		// radius + speed*dt*tan(50°) and up by min(2*radius, speed*dt).
		// Use slope-dependent tolerance but ONLY horizontal speed (not fall velocity).
		const float hSpeedDt = std::sqrt(speedSq) * dt; // speedSq is horizontal only (vx² + vy²)
		const float slopeTolerance = std::max(0.5f, r + hSpeedDt * PhysicsConstants::WALKABLE_TAN_MAX_SLOPE);
		const float minRescueDz = trustGroundedReplayInput ? -0.35f : -slopeTolerance;
		const float maxRescueDz = trustGroundedReplayInput ? 0.55f : std::min(slopeTolerance, PhysicsConstants::STEP_HEIGHT);
		const float offsets[9][2] = {
			{0, 0},
			{probeR, 0}, {-probeR, 0}, {0, probeR}, {0, -probeR},
			{diagR, diagR}, {diagR, -diagR}, {-diagR, diagR}, {-diagR, -diagR}
		};

		float bestZ = PhysicsConstants::INVALID_HEIGHT;
		auto considerProbe = [&](float ox, float oy) {
			float probeBestZ = PhysicsConstants::INVALID_HEIGHT;
			for (float queryZ : queryHeights) {
				float pz = SceneQuery::GetGroundZ(
					input.mapId,
					st.x + ox,
					st.y + oy,
					queryZ,
					PhysicsConstants::STEP_DOWN_HEIGHT);
				if (!VMAP::IsValidHeight(pz))
					continue;

				const float dz = pz - rescueReferenceZ;
				if (dz < minRescueDz || dz > maxRescueDz)
					continue;

				if (probeBestZ <= PhysicsConstants::INVALID_HEIGHT || pz > probeBestZ)
					probeBestZ = pz;
			}

			if (probeBestZ > PhysicsConstants::INVALID_HEIGHT &&
				(bestZ <= PhysicsConstants::INVALID_HEIGHT || probeBestZ > bestZ)) {
				bestZ = probeBestZ;
			}
		};

		for (int i = 0; i < 9; ++i) {
			considerProbe(offsets[i][0], offsets[i][1]);
		}

		// In trust-input replay mode, probe support slightly farther forward to
		// recover from one-frame false-airborne transitions on rising terrain.
		if (trustGroundedReplayInput && hasMoveDir) {
			const float forwardR2 = probeR * 2.0f;
			const float forwardR3 = probeR * 3.0f;
			const float forwardR4 = probeR * 4.0f;
			const float forwardR5 = probeR * 5.0f;
			const float sideR = probeR;
			const float perpX = -dirY;
			const float perpY = dirX;

			considerProbe(dirX * probeR, dirY * probeR);
			considerProbe(dirX * forwardR2, dirY * forwardR2);
			considerProbe(dirX * forwardR3, dirY * forwardR3);
			considerProbe(dirX * forwardR4, dirY * forwardR4);
			considerProbe(dirX * forwardR5, dirY * forwardR5);
			considerProbe((dirX * forwardR2) + (perpX * sideR), (dirY * forwardR2) + (perpY * sideR));
			considerProbe((dirX * forwardR2) - (perpX * sideR), (dirY * forwardR2) - (perpY * sideR));
			considerProbe((dirX * forwardR3) + (perpX * sideR), (dirY * forwardR3) + (perpY * sideR));
			considerProbe((dirX * forwardR3) - (perpX * sideR), (dirY * forwardR3) - (perpY * sideR));
		}

		// Trust-replay fallback: if nearby support probing fails but the simulated Z is
		// still close to the caller's non-airborne frame, keep the character grounded.
		// This prevents persistent one-frame false-airborne flips from accumulating drift.
		if (bestZ <= PhysicsConstants::INVALID_HEIGHT && trustGroundedReplayInput) {
			const float inputDz = input.z - st.z;
			const float maxInputFallbackDz = 0.20f;
			if (std::fabs(inputDz) <= maxInputFallbackDz) {
				bestZ = input.z;
			}
		}

		if (bestZ > PhysicsConstants::INVALID_HEIGHT) {
			st.z = bestZ;
			st.isGrounded = true;
			st.vz = 0.0f;
			st.fallTime = 0.0f;
		}
	}

	// Replay trust recovery: keep explicitly non-airborne replay frames grounded when
	// simulation drift is still close to input. This lets the grounded Z refinement path
	// resolve local floor support instead of carrying false-airborne state.
	if (!st.isGrounded && trustGroundedReplayInput && !isSwimming && !inputAirborneFlag) {
		const float replayGroundRecoveryDz = 0.20f;
		const float dzFromInput = st.z - input.z;
		if (std::fabs(dzFromInput) <= replayGroundRecoveryDz) {
			st.z = std::max(st.z, input.z);
			st.isGrounded = true;
			st.vz = 0.0f;
			st.fallTime = 0.0f;
		}
	}

	SceneQuery::LiquidInfo finalLiq = SceneQuery::EvaluateLiquidAt(input.mapId, st.x, st.y, st.z);
	if (finalLiq.isSwimming && !isSwimming) {
		if (finalLiq.hasLevel) {
			st.z = std::max(st.z, finalLiq.level - PhysicsConstants::WATER_LEVEL_DELTA);
		}
		st.vx *= PhysicsConstants::WATER_ENTRY_VELOCITY_DAMP;
		st.vy *= PhysicsConstants::WATER_ENTRY_VELOCITY_DAMP;
		st.vz = 0.0f;
		st.isGrounded = false;
	}
	else if (!finalLiq.isSwimming && isSwimming) {
		st.isGrounded = st.isGrounded && !finalLiq.isSwimming;
	}
	isSwimming = finalLiq.isSwimming;
	st.isSwimming = isSwimming;

	// Compute output velocity.
	// For airborne: use the simulation's end-of-frame velocity (st.vx/vy/vz) rather than
	// position-derived average. The position delta gives v_avg = v0 - 0.5*g*dt, but the
	// actual velocity at frame end is v_end = v0 - g*dt. Using v_avg as next frame's input
	// would cause 0.5*g*dt error per frame (~0.48 y/s at 50ms frames).
	// For grounded: zero all components. Direction is rebuilt from flags each frame.
	G3D::Vector3 curPos(st.x, st.y, st.z);
	G3D::Vector3 actualV(0, 0, 0);
	bool airborne = !st.isGrounded;
	if (dt > 0.0f) {
		actualV = (curPos - prevPos) * (1.0f / dt);
		if (airborne || isSwimming) {
			// Use simulation velocity for vertical component (avoids average vs end-of-frame error)
			actualV.z = st.vz;
		}
	}
    else
        PHYS_INFO(PHYS_MOVE, "[StepV2] Non-positive dt; skipping velocity calc");

	// When grounded, zero all velocity components. Grounded movement direction
	// is rebuilt each frame from movement flags + orientation (BuildMovementPlan),
	// not from carried velocity. Persisting Vx/Vy from position deltas can pollute
	// edge-case logic (rescue probes, grounded→airborne transitions) and cause
	// erratic movement when the direction changes between frames.
	if (!airborne && !isSwimming) {
		actualV = G3D::Vector3(0, 0, 0);
	}

	// Ground Z refinement safety net: multi-ray probing.
	// Save pre-safety-net Z for step-up detection below. The safety net may
	// override the sweep's step-up result; we need the original to detect it.
	const float preSafetyNetZ = st.z;

	// Primary Z refinement now happens inside ExecuteDownPass and PhysicsGroundSnap functions
	// via GetGroundZ at exact character XY. This multi-ray probe catches cases where the
	// capsule sweep completely missed thin WMO floor meshes (e.g. in Orgrimmar).
	// Skip when SurfaceStep hint is active — the trust-grounded path already placed Z
	// at the recording's target surface; re-probing would clamp it back to input.z.
	const bool surfaceStepHintActive = (std::fabs(input.vz) > 0.1f) && trustGroundedReplayInput;
	if (st.isGrounded && !isSwimming && !surfaceStepHintActive) {
		const float preRefineZ = st.z;
		const float refineReferenceZ = input.z;
		const float maxRise = trustGroundedReplayInput ? 0.3f : 0.2f;
		const float maxDrop = 0.5f;
		float queryZ = preRefineZ + 0.3f;

		// Replay trust path: evaluate center and directional probes together.
		// Center-only sampling lags on ramps/stairs when support is at capsule leading edge.
		if (trustGroundedReplayInput) {
			float centerZ = PhysicsConstants::INVALID_HEIGHT;
			bool centerValid = false;
			float bestZ = PhysicsConstants::INVALID_HEIGHT;
			float bestForwardZ = PhysicsConstants::INVALID_HEIGHT;
			float bestForwardDot = -2.0f;
			const float probeR1 = r;          // inner ring at capsule radius
			const float probeR2 = r * 2.0f;   // outer ring at 2x capsule radius
			const float diagR1 = probeR1 * 0.707f;
			const float diagR2 = probeR2 * 0.707f;
			const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
			const bool hasMoveDir = speedSq > PhysicsConstants::VECTOR_EPSILON;
			const float invSpeed = hasMoveDir ? (1.0f / std::sqrt(speedSq)) : 0.0f;
			const float dirX = hasMoveDir ? (input.vx * invSpeed) : 0.0f;
			const float dirY = hasMoveDir ? (input.vy * invSpeed) : 0.0f;
			const float minForwardDot = 0.25f; // use directional support, not rear/lateral probes
			const float offsets[17][2] = {
				{0, 0},
				// Inner ring (capsule radius)
				{probeR1, 0}, {-probeR1, 0}, {0, probeR1}, {0, -probeR1},
				{diagR1, diagR1}, {diagR1, -diagR1}, {-diagR1, diagR1}, {-diagR1, -diagR1},
				// Outer ring (2x capsule radius)
				{probeR2, 0}, {-probeR2, 0}, {0, probeR2}, {0, -probeR2},
				{diagR2, diagR2}, {diagR2, -diagR2}, {-diagR2, diagR2}, {-diagR2, -diagR2}
			};
			const float queryHeights[3] = {
				queryZ,
				queryZ + 0.45f,
				queryZ + 0.90f
			};

			auto sampleProbeZ = [&](float sampleX, float sampleY) {
				float probeZ = PhysicsConstants::INVALID_HEIGHT;
				for (float queryHeight : queryHeights) {
					const float candidateZ = SceneQuery::GetGroundZ(
						input.mapId,
						sampleX,
						sampleY,
						queryHeight,
						PhysicsConstants::STEP_DOWN_HEIGHT);
					if (!VMAP::IsValidHeight(candidateZ) ||
						candidateZ > preRefineZ + maxRise ||
						candidateZ < preRefineZ - maxDrop) {
						continue;
					}

					if (probeZ <= PhysicsConstants::INVALID_HEIGHT || candidateZ > probeZ) {
						probeZ = candidateZ;
					}
				}

				return probeZ;
			};

			auto considerProbe = [&](float ox, float oy) {
				const float pz = sampleProbeZ(st.x + ox, st.y + oy);
				if (pz <= PhysicsConstants::INVALID_HEIGHT) {
					return;
				}

				if (bestZ <= PhysicsConstants::INVALID_HEIGHT || pz > bestZ) {
					bestZ = pz;
				}

				if (!hasMoveDir) {
					return;
				}

				const float offLenSq = (ox * ox) + (oy * oy);
				if (offLenSq <= PhysicsConstants::VECTOR_EPSILON) {
					return;
				}

				const float invOffLen = 1.0f / std::sqrt(offLenSq);
				const float dot = ((ox * invOffLen) * dirX) + ((oy * invOffLen) * dirY);
				if (dot < minForwardDot) {
					return;
				}

				const float forwardZTieEpsilon = 0.002f;
				if (bestForwardZ <= PhysicsConstants::INVALID_HEIGHT || pz > bestForwardZ + forwardZTieEpsilon) {
					bestForwardZ = pz;
					bestForwardDot = dot;
				}
				else if (std::fabs(pz - bestForwardZ) <= forwardZTieEpsilon && dot > bestForwardDot) {
					bestForwardDot = dot;
					bestForwardZ = pz;
				}
			};

			// Center probe: prefer surface closest to input.z (the recorded position)
			// rather than highest. The character IS at input.z, so the closest surface
			// is the correct one. Directional probes still use "highest" for ramp detection.
			// Add a low query near input.z so GetGroundZ's "closest-to-query" selection
			// finds the surface at the character's actual level, not a shelf above.
			{
				const float centerQueryHeights[4] = {
					input.z + 0.05f,     // Near recording level (finds surface at character's feet)
					queryZ,              // preRefineZ + 0.3
					queryZ + 0.45f,
					queryZ + 0.90f
				};
				float bestCenterDist = FLT_MAX;
				for (float cqh : centerQueryHeights) {
					const float candidateZ = SceneQuery::GetGroundZ(
						input.mapId, st.x, st.y, cqh,
						PhysicsConstants::STEP_DOWN_HEIGHT);
					if (!VMAP::IsValidHeight(candidateZ) ||
						candidateZ > preRefineZ + maxRise ||
						candidateZ < preRefineZ - maxDrop) {
						continue;
					}
					float dist = std::fabs(candidateZ - input.z);
					if (centerZ <= PhysicsConstants::INVALID_HEIGHT || dist < bestCenterDist) {
						centerZ = candidateZ;
						bestCenterDist = dist;
					}
				}
			}
			if (centerZ > PhysicsConstants::INVALID_HEIGHT) {
				centerValid = true;
			}

			// Skip index 0 since center probe already sampled above.
			for (int i = 1; i < 17; i++) {
				considerProbe(offsets[i][0], offsets[i][1]);
			}

			// Add movement-aligned look-ahead probes for slope/step transitions.
			if (hasMoveDir) {
				const float frameMoveDist = std::sqrt(speedSq) * dt;
				const float nearForwardProbe = std::max(0.02f, std::min(frameMoveDist, probeR1));
				const float midForwardProbe = std::max(nearForwardProbe, std::min(frameMoveDist * 2.0f, probeR2));
				const float forwardR3 = r * 3.0f;
				const float forwardR4 = r * 4.0f;
				const float forwardR5 = r * 5.0f;
				const float sideR = r * 0.5f;
				const float perpX = -dirY;
				const float perpY = dirX;

				considerProbe(dirX * nearForwardProbe, dirY * nearForwardProbe);
				considerProbe(dirX * midForwardProbe, dirY * midForwardProbe);
				considerProbe(dirX * probeR1, dirY * probeR1);
				considerProbe(dirX * probeR2, dirY * probeR2);
				considerProbe(dirX * forwardR3, dirY * forwardR3);
				considerProbe(dirX * forwardR4, dirY * forwardR4);
				considerProbe(dirX * forwardR5, dirY * forwardR5);
				considerProbe((dirX * probeR2) + (perpX * sideR), (dirY * probeR2) + (perpY * sideR));
				considerProbe((dirX * probeR2) - (perpX * sideR), (dirY * probeR2) - (perpY * sideR));
				considerProbe((dirX * forwardR3) + (perpX * sideR), (dirY * forwardR3) + (perpY * sideR));
				considerProbe((dirX * forwardR3) - (perpX * sideR), (dirY * forwardR3) - (perpY * sideR));
			}

			float chosenZ = PhysicsConstants::INVALID_HEIGHT;
			if (centerValid) {
				chosenZ = centerZ;
				const bool allowCenterLagCompensation = input.prevGroundNz >= 0.97f;
				if (allowCenterLagCompensation &&
					bestZ > PhysicsConstants::INVALID_HEIGHT &&
					bestZ > centerZ &&
					centerZ < input.z - 0.02f) {
					// In replay trust mode, allow modest uplift to nearby support to avoid
					// one-frame center-probe lag on ramps/stairs.
					// Only activate when center probe LAGS behind input.z — if center ≈ input.z,
					// there's no lag to compensate. This prevents lateral WMO probes on flat
					// ground from inflating chosenZ.
					const float maxCenterLagCompensation = 0.22f;
					const float dz = bestZ - centerZ;
					chosenZ = centerZ + std::min(dz, maxCenterLagCompensation);
				}
			}
			else if (bestZ > PhysicsConstants::INVALID_HEIGHT) {
				chosenZ = bestZ;
			}

			if (bestForwardZ > PhysicsConstants::INVALID_HEIGHT) {
				if (chosenZ > PhysicsConstants::INVALID_HEIGHT) {
					const float maxDirectionalRise = 0.20f;
					const float maxDirectionalDrop = 0.03f;
					const float dz = bestForwardZ - chosenZ;
					if (dz > maxDirectionalRise) {
						chosenZ += maxDirectionalRise;
					}
					else if (dz < -maxDirectionalDrop) {
						chosenZ -= maxDirectionalDrop;
					}
					else {
						chosenZ = bestForwardZ;
					}
				}
				else {
					chosenZ = bestForwardZ;
				}
			}

			if (chosenZ > PhysicsConstants::INVALID_HEIGHT) {
				// Replay calibration guardrail: keep grounded trust-refine Z near the
				// captured frame to avoid latching to nearby higher surfaces.
				float maxReplayInputRise = 0.03f;
				const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
				const bool movingReplay = speedSq > PhysicsConstants::VECTOR_EPSILON;
				const bool nearFlatPrevSupport = input.prevGroundNz >= 0.97f;
				const bool steepOrInvertedPrevSupport = input.prevGroundNz <= -0.70f;
				if (!movingReplay && steepOrInvertedPrevSupport) {
					// Avoid one-frame upward snaps when replay is grounded on
					// inverted/steep support and has no XY intent.
					maxReplayInputRise = 0.0f;
				}
				else if (movingReplay && steepOrInvertedPrevSupport) {
					maxReplayInputRise = 0.02f;
				}
				else if (movingReplay && nearFlatPrevSupport) {
					// Only allow large rise when ground is actually ascending.
					// On flat ground near WMO structures, directional probes can latch onto
					// nearby edges/overhangs. Without an ascending trend, cap conservatively
					// to avoid +0.14y false uplift from lateral probe contamination.
					const float prevDz = input.z - input.prevGroundZ;
					maxReplayInputRise = (prevDz > 0.01f) ? 0.14f : 0.04f;
				}

				if (movingReplay && nearFlatPrevSupport && chosenZ <= input.z + 0.005f) {
					// Compensate one-frame grounded replay lag when probe selection stays
					// near input.z on ramps by leading with prior ground trend.
					const float previousGroundDz = input.z - input.prevGroundZ;
					const float trendLeadMax = 0.08f;
					const float trendLead = std::max(-trendLeadMax, std::min(previousGroundDz, trendLeadMax));
					chosenZ += trendLead;
				}

				const float maxReplayInputDrop = 0.20f;
				const float minAllowedZ = input.z - maxReplayInputDrop;
				const float maxAllowedZ = input.z + maxReplayInputRise;
				chosenZ = std::max(minAllowedZ, std::min(chosenZ, maxAllowedZ));
				st.z = chosenZ;
			}
		}
		else {
			float bestZ = PhysicsConstants::INVALID_HEIGHT;
			float bestErr = std::numeric_limits<float>::max();
			const float probeR1 = r;          // inner ring at capsule radius
			const float probeR2 = r * 2.0f;   // outer ring at 2x capsule radius
			const float diagR1 = probeR1 * 0.707f;
			const float diagR2 = probeR2 * 0.707f;
			const float offsets[17][2] = {
				{0, 0},
				// Inner ring (capsule radius)
				{probeR1, 0}, {-probeR1, 0}, {0, probeR1}, {0, -probeR1},
				{diagR1, diagR1}, {diagR1, -diagR1}, {-diagR1, diagR1}, {-diagR1, -diagR1},
				// Outer ring (2x capsule radius)
				{probeR2, 0}, {-probeR2, 0}, {0, probeR2}, {0, -probeR2},
				{diagR2, diagR2}, {diagR2, -diagR2}, {-diagR2, diagR2}, {-diagR2, -diagR2}
			};
			for (int i = 0; i < 17; i++) {
				float pz = SceneQuery::GetGroundZ(input.mapId,
					st.x + offsets[i][0], st.y + offsets[i][1], queryZ,
					PhysicsConstants::STEP_DOWN_HEIGHT);
				if (VMAP::IsValidHeight(pz) &&
					pz <= preRefineZ + maxRise &&
					pz >= preRefineZ - maxDrop) {
					float err = std::fabs(pz - refineReferenceZ);
					if (err < bestErr) {
						bestErr = err;
						bestZ = pz;
					}
				}
			}
			if (bestZ > PhysicsConstants::INVALID_HEIGHT) {
				st.z = bestZ;
			}
		}
	}

	// Trust-replay fallback: when input is explicitly non-airborne but simulation ended
	// airborne, run one last nearby support probe and re-ground if the candidate is close.
	if (!st.isGrounded && trustGroundedReplayInput && !isSwimming && !inputAirborneFlag) {
		const float probeR = std::max(0.05f, r);
		const float diagR = probeR * 0.707f;
		const float referenceZ = std::max(st.z, input.z);
		const float minInputDz = -0.35f;
		const float maxInputDz = 0.35f;
		// Sample with both low and high query origins. GetGroundZ picks the candidate
		// closest to query Z, so a high probe helps catch uphill support that a low
		// probe can miss on multi-level geometry.
		const float queryHeights[3] = {
			input.z + 0.30f,
			input.z + 0.90f,
			referenceZ + 0.30f
		};
		const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
		const bool hasMoveDir = speedSq > PhysicsConstants::VECTOR_EPSILON;
		const bool stationaryReplay = !hasMoveDir;
		const float invSpeed = hasMoveDir ? (1.0f / std::sqrt(speedSq)) : 0.0f;
		const float dirX = hasMoveDir ? (input.vx * invSpeed) : 0.0f;
		const float dirY = hasMoveDir ? (input.vy * invSpeed) : 0.0f;
		const float offsets[13][2] = {
			{0, 0},
			{probeR, 0}, {-probeR, 0}, {0, probeR}, {0, -probeR},
			{diagR, diagR}, {diagR, -diagR}, {-diagR, diagR}, {-diagR, -diagR},
			{dirX * probeR, dirY * probeR},
			{dirX * probeR * 2.0f, dirY * probeR * 2.0f},
			{dirX * probeR * 3.0f, dirY * probeR * 3.0f},
			{dirX * probeR * 4.0f, dirY * probeR * 4.0f}
		};

		float bestZ = PhysicsConstants::INVALID_HEIGHT;
		float bestInputDzAbs = std::numeric_limits<float>::max();
		auto considerCandidate = [&](float pz) {
			const float inputDz = pz - input.z;
			if (inputDz < minInputDz || inputDz > maxInputDz) {
				return;
			}

			if (!stationaryReplay) {
				if (bestZ <= PhysicsConstants::INVALID_HEIGHT || pz > bestZ) {
					bestZ = pz;
				}
				return;
			}

			const float absInputDz = std::fabs(inputDz);
			const float tieEpsilon = 0.002f;
			if (bestZ <= PhysicsConstants::INVALID_HEIGHT ||
				absInputDz + tieEpsilon < bestInputDzAbs ||
				(std::fabs(absInputDz - bestInputDzAbs) <= tieEpsilon && pz < bestZ)) {
				bestZ = pz;
				bestInputDzAbs = absInputDz;
			}
		};
		auto considerProbe = [&](float sampleX, float sampleY) {
			for (float queryZ : queryHeights) {
				float pz = SceneQuery::GetGroundZ(
					input.mapId,
					sampleX,
					sampleY,
					queryZ,
					PhysicsConstants::STEP_DOWN_HEIGHT);
				if (!VMAP::IsValidHeight(pz))
					continue;

				considerCandidate(pz);
			}
		};

		for (const auto& o : offsets) {
			considerProbe(st.x + o[0], st.y + o[1]);
		}

		// Last resort: if neighborhood probes miss, check exact trusted XY with a
		// slightly larger downward window to preserve small descending transitions.
		if (bestZ <= PhysicsConstants::INVALID_HEIGHT) {
			for (float queryZ : queryHeights) {
				float inputSupportZ = SceneQuery::GetGroundZ(
					input.mapId,
					st.x,
					st.y,
					queryZ,
					PhysicsConstants::STEP_DOWN_HEIGHT);
				if (!VMAP::IsValidHeight(inputSupportZ))
					continue;

				const float inputSupportDz = inputSupportZ - input.z;
				if (inputSupportDz >= -0.45f && inputSupportDz <= maxInputDz) {
					considerCandidate(inputSupportZ);
				}
			}
		}

		if (bestZ > PhysicsConstants::INVALID_HEIGHT) {
			if (stationaryReplay) {
				const float stationaryMaxRise = 0.02f;
				bestZ = std::min(bestZ, input.z + stationaryMaxRise);
			}
			st.z = bestZ;
			st.isGrounded = true;
			st.vz = 0.0f;
			st.fallTime = 0.0f;
			actualV.z = 0.0f;
		}
	}

	// Replay trust guardrail: when we remain grounded on non-walkable support,
	// keep Z tightly bounded to the captured frame to avoid persistent over-lift.
	// Skip when a SurfaceStep hint is present (input.vz significant) — the recording
	// explicitly shows a large Z change that the guardrail would incorrectly clamp.
	if (trustGroundedReplayInput && st.isGrounded && !isSwimming && !inputAirborneFlag) {
		const bool nonWalkableSupport =
			st.groundNormal.z < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
		const bool surfaceStepActive = (std::fabs(input.vz) > 0.1f);
		if (nonWalkableSupport && !surfaceStepActive) {
			const float speedSq = (input.vx * input.vx) + (input.vy * input.vy);
			const bool movingReplay = speedSq > PhysicsConstants::VECTOR_EPSILON;
			float maxReplayRise = 0.0f;
			if (movingReplay) {
				maxReplayRise = 0.02f;

				// Estimate support trend using the sampled support delta between the
				// replay input XY and the trusted next XY. This captures uphill
				// transitions more reliably than prevGroundZ when replay trust is active.
				bool resolvedTrend = false;
				float supportTrendDz = 0.0f;
				const float queryBaseZ = std::max(input.z, st.z) + 0.35f;
				const float currentSupportZ = SceneQuery::GetGroundZ(
					input.mapId, input.x, input.y, queryBaseZ, PhysicsConstants::STEP_DOWN_HEIGHT);
				const float nextSupportZ = SceneQuery::GetGroundZ(
					input.mapId, st.x, st.y, queryBaseZ, PhysicsConstants::STEP_DOWN_HEIGHT);
				if (VMAP::IsValidHeight(currentSupportZ) && VMAP::IsValidHeight(nextSupportZ)) {
					const float currentInputDz = currentSupportZ - input.z;
					const float nextInputDz = nextSupportZ - input.z;
					if (currentInputDz >= -0.20f && currentInputDz <= 0.20f &&
						nextInputDz >= -0.45f && nextInputDz <= 0.35f) {
						supportTrendDz = nextSupportZ - currentSupportZ;
						resolvedTrend = true;
					}
				}

				if (!resolvedTrend) {
					const float frameDx = input.vx * dt;
					const float frameDy = input.vy * dt;
					G3D::Vector3 supportN = st.groundNormal;
					if (supportN.z < 0.0f) {
						supportN.x = -supportN.x;
						supportN.y = -supportN.y;
						supportN.z = -supportN.z;
					}

					if (std::fabs(supportN.z) > PhysicsConstants::GROUND_SNAP_EPSILON) {
						supportTrendDz =
							-((supportN.x * frameDx) + (supportN.y * frameDy)) / supportN.z;
						resolvedTrend = true;
					}
				}

				if (resolvedTrend) {
					if (supportTrendDz <= -0.01f) {
						maxReplayRise = 0.0f;
					}
					else if (supportTrendDz >= 0.03f) {
						maxReplayRise = 0.05f;
					}
				}
			}
			const float maxReplayDrop = 0.25f;
			const float minAllowedZ = input.z - maxReplayDrop;
			const float maxAllowedZ = input.z + maxReplayRise;
			st.z = std::max(minAllowedZ, std::min(st.z, maxAllowedZ));
			st.vz = 0.0f;
			actualV.z = 0.0f;
		}
	}
	// Step-up height persistence: after a significant grounded Z rise (stair/ledge),
	// hold the height for a few frames to bridge navmesh polygon gaps at step edges.
	// Uses preSafetyNetZ to detect step-ups that the safety net might have undone.
	{
		constexpr uint32_t MAX_STEP_UP_HOLD = 5;    // ~80-85ms at 60fps
		constexpr float STEP_UP_RISE_THRESHOLD = 0.25f;
		constexpr float STEP_UP_DROP_TOLERANCE = 0.15f;
		const float highestZ = std::max(st.z, preSafetyNetZ);
		const float zRise = highestZ - input.z;
		const bool justSteppedUp = st.isGrounded && !isSwimming && !inputAirborneFlag
			&& zRise > STEP_UP_RISE_THRESHOLD;

		if (justSteppedUp) {
			st.z = highestZ;
			out.stepUpBaseZ = highestZ;
			out.stepUpAge = 0;
		} else if (input.stepUpBaseZ > PhysicsConstants::INVALID_HEIGHT
				&& input.stepUpAge < MAX_STEP_UP_HOLD) {
			if (st.isGrounded && st.z < input.stepUpBaseZ - STEP_UP_DROP_TOLERANCE) {
				// Engine dropped below step-up base — hold
				st.z = input.stepUpBaseZ;
			}
			out.stepUpBaseZ = input.stepUpBaseZ;
			out.stepUpAge = input.stepUpAge + 1;
		} else {
			out.stepUpBaseZ = PhysicsConstants::INVALID_HEIGHT;
			out.stepUpAge = 0;
		}
	}

	// Output
	{
		float outDx = st.x - input.x;
		float outDy = st.y - input.y;
		float outDist = std::sqrt(outDx*outDx + outDy*outDy);
		// Check effective flags (after airborne/root masking), not raw input flags.
		// When airborne, FORWARD is stripped from effective flags — zero XY is expected.
		bool hasEffectiveForward = (effectiveFlags & MOVEFLAG_FORWARD) != 0;
		if (hasEffectiveForward && outDist < 0.001f) {
			std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
			oss << "[OUT_ZERO] FORWARD set but zero XY delta. st=(" << st.x << "," << st.y << "," << st.z
				<< ") input=(" << input.x << "," << input.y << "," << input.z
				<< ") prevPos=(" << prevPos.x << "," << prevPos.y << "," << prevPos.z
				<< ") grounded=" << (st.isGrounded ? 1 : 0)
				<< " swim=" << (isSwimming ? 1 : 0)
				<< " flying=" << (isFlying ? 1 : 0)
				<< " wasGrounded=" << (wasGroundedAtStart ? 1 : 0);
			PHYS_ERR(PHYS_MOVE, oss.str());
		}
	}
	out.x = st.x; out.y = st.y; out.z = st.z;
	out.orientation = st.orientation; out.pitch = st.pitch;
	out.vx = actualV.x; out.vy = actualV.y; out.vz = actualV.z;
	out.moveFlags = input.moveFlags;
	if (isSwimming) out.moveFlags |= MOVEFLAG_SWIMMING; else out.moveFlags &= ~MOVEFLAG_SWIMMING;

	// =========================================================================
	// GROUND CONTACT PERSISTENCE (WoW.exe parity)
	// =========================================================================
	// WoW.exe CollisionStep (0x633840) uses an AABB that dynamically sizes to
	// encompass terrain on both uphill and downhill slopes. Our capsule sweep
	// can intermittently miss terrain geometry, producing single-frame false-
	// airborne states. When the character was grounded on the previous frame
	// (no airborne input flags) and the output position is still near valid
	// ground, maintain grounded state. This matches WoW.exe behavior where
	// the character stays grounded unless clearly airborne (walked off a ledge
	// with no ground within STEP_DOWN_HEIGHT).
	if (!st.isGrounded && !isSwimming && !inputAirborneFlag && wasGroundedAtStart) {
		// Quick ray probe at current XY to check if ground exists nearby
		float persistZ = SceneQuery::GetGroundZ(input.mapId, st.x, st.y,
			st.z + PhysicsConstants::STEP_HEIGHT,
			PhysicsConstants::STEP_DOWN_HEIGHT + PhysicsConstants::STEP_HEIGHT);
		// WoW.exe lifts AABB maxZ by min(2*radius, speed*dt) before sweeping.
		// Our capsule SIDE pass can clip through uphill terrain, leaving st.z
		// several yards below the surface. Since the character was grounded
		// last frame and hasn't jumped, any valid ground at this XY is the
		// correct surface. The character can't have legitimately fallen far
		// in one frame from a grounded state.
		if (VMAP::IsValidHeight(persistZ)) {
			st.z = persistZ;
			st.isGrounded = true;
			st.vz = 0.0f;
			st.fallTime = 0.0f;
			// Update output position since we snapped Z
			out.z = st.z;
			out.vz = 0.0f;
			PHYS_INFO(PHYS_MOVE, "[GroundPersist] Rescued from false-airborne via ray: z=" + std::to_string(persistZ));
		} else {
			std::ostringstream oss; oss.setf(std::ios::fixed); oss.precision(4);
			oss << "[GroundPersist] Ray failed: persistZ=" << persistZ
				<< " st.z=" << st.z << " valid=" << VMAP::IsValidHeight(persistZ)
				<< " wasGrounded=" << wasGroundedAtStart
				<< " inputAirborne=" << inputAirborneFlag;
			PHYS_ERR(PHYS_MOVE, oss.str());
		}
	}

	// =========================================================================
	// AIRBORNE FLAG MANAGEMENT (WoW.exe parity)
	// =========================================================================
	// WoW uses JUMPING (0x2000) for player-initiated jumps (entire arc: ascent + descent).
	// FALLINGFAR (0x4000) is set when falling without a jump (walked off ledge, etc.).
	// When grounded, both are cleared. When airborne:
	//   - If JUMPING was set by input, preserve it (jump in progress)
	//   - If neither was set, engine detected a fall → set FALLINGFAR
	// Additionally, WoW restricts directional flags during airborne:
	//   FORWARD, BACKWARD, STRAFE_LEFT, STRAFE_RIGHT are cleared when falling
	//   (no air control). The client sends them but the server/physics ignores them.
	if (st.isGrounded) {
		out.moveFlags &= ~(MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR);
	}
	else {
		if (!(out.moveFlags & (MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR))) {
			out.moveFlags |= MOVEFLAG_FALLINGFAR;
		}
		// WoW.exe: directional input is locked during airborne.
		// The movement direction is frozen at the moment of leaving the ground.
		// We don't strip the flags from the packet (server expects them for validation),
		// but the physics engine ignores them — horizontal velocity is carried from launch.
	}

	// Ground Z output: when grounded, st.z was snapped to terrain by the DOWN pass.
	// When airborne, st.z is the falling position — use prevGroundZ (the last grounded
	// surface) so the C# side can distinguish real falls (large gap) from capsule sweep
	// misses (small gap near ground).
	if (st.isGrounded) {
		out.groundZ = st.z;
	} else if (VMAP::IsValidHeight(input.prevGroundZ) && input.prevGroundZ > -100000.0f) {
		out.groundZ = input.prevGroundZ;
	} else {
		// No previous ground reference — report character position as fallback.
		// This preserves existing behavior for bots that start airborne.
		out.groundZ = st.z;
	}
	out.fallTime = st.fallTime * 1000.0f;  // Convert seconds (internal) → ms for output

	// Fall distance tracking: detect grounded↔airborne transitions
	if (wasGroundedAtStart && !st.isGrounded) {
		// Grounded → airborne: record the Z where the fall began
		st.fallStartZ = prevPos.z;
		out.fallDistance = 0.0f;
	} else if (!wasGroundedAtStart && st.isGrounded && st.fallStartZ > -100000.0f) {
		// Airborne → grounded: compute total fall distance (positive = downward)
		out.fallDistance = st.fallStartZ - st.z;
		st.fallStartZ = -200000.0f;  // reset sentinel
	} else {
		out.fallDistance = 0.0f;
	}
	out.fallStartZ = st.fallStartZ;
	out.liquidZ = finalLiq.level;
	out.liquidType = finalLiq.type;
	out.groundNx = st.groundNormal.x;
	out.groundNy = st.groundNormal.y;
	out.groundNz = st.groundNormal.z;

	out.hitWall = st.wallHit;
	out.wallNormalX = st.wallHitNormal.x;
	out.wallNormalY = st.wallHitNormal.y;
	out.wallNormalZ = st.wallHitNormal.z;
	out.blockedFraction = st.wallBlockedFraction;

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
