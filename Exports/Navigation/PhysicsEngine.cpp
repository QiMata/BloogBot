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

#include <xmmintrin.h>
#include "PhysicsTolerances.h"
#include "DynamicObjectRegistry.h"

// Extracted physics modules
#include "PhysicsCollideSlide.h"
#include "PhysicsGroundSnap.h"
#include "PhysicsMovement.h"

#include <algorithm>
#include <cstring>
#include <filesystem>
#include <iostream>
#include <iomanip>
#include <cfloat>
#include <chrono>
#include <limits>
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

    constexpr float WOW_WALKABLE_MIN_NORMAL_Z = 0.6427876353263855f;        // 0x80DFFC
    constexpr float WOW_RELAXED_WALKABLE_MIN_NORMAL_Z = 0.1736481785774231f; // 0x80E000
    constexpr float WOW_TRIANGLE_PRISM_EPSILON = 0.0833333358168602f;        // 0x80E004
    constexpr float WOW_CORNER_PLANE_EPSILON = 0.0013888889225199819f;       // 0x80DFEC
    constexpr float WOW_PLANE_BUILD_EPSILON = 9.54e-7f;                      // 0x8026BC
    constexpr float WOW_SELECTOR_SUPPORT_DIAGONAL_X = 0.8796418905258179f;   // 0x80DFE4
    constexpr float WOW_SELECTOR_SUPPORT_DIAGONAL_Z = 0.4756366014480591f;   // 0x80DFE0
    constexpr float WOW_SELECTOR_SUPPORT_NEGATIVE_DIAGONAL_Z = -0.4756366014480591f; // 0x80E014
    constexpr float WOW_SELECTOR_CLIP_NEGATIVE_EPSILON = -0.0013888889225199819f; // 0x80DFF0
    constexpr float WOW_SELECTOR_RATIO_EPSILON = 2.384185791015625e-7f;      // 0x8029D4
    constexpr float WOW_SELECTOR_FOOTPRINT_SAMPLE_HEIGHT_FACTOR = 1.8493989706039429f; // 0x80C740
    constexpr float WOW_SELECTOR_LOOSE_RATIO_THRESHOLD = -9.5367431640625e-07f; // 0x80DFF4
    constexpr float WOW_SELECTOR_STRICT_RATIO_THRESHOLD = -0.02777777798473835f; // 0x7FF9C8
    constexpr float WOW_SELECTOR_RECORD_FILTER_THRESHOLD = -9.999999747378752e-06f; // 0x80C5C4
    constexpr float WOW_TERRAIN_QUERY_FIELD20_THRESHOLD = -0.6457718014717102f; // 0x80DFE8
    constexpr float WOW_TERRAIN_QUERY_CACHE_MISS_EXPANSION = 0.1666666716337204f; // 0x3E2AAAAB
    constexpr float WOW_SELECTOR_SOURCE_OUTCODE_THRESHOLD = -0.01944444328546524f; // 0x8101B4
    constexpr float WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON = 0.01944444328546524f; // 0x8101B8
    constexpr float WOW_SELECTOR_RECORD_VERTICAL_EXTRUSION = 32000.0f; // 0x46FA0000
    constexpr float WOW_TERRAIN_QUERY_GRID_CENTER = 17066.666015625f; // 0x7FFAB4
    constexpr float WOW_TERRAIN_QUERY_GRID_SCALE = 0.23999999463558197f; // 0x810AE4
    constexpr float WOW_TERRAIN_QUERY_GRID_BIAS = 0.5f; // 0x86AA2C
    constexpr uint32_t WOW_AABB_OUTCODE_MIN_X = 0x01u;
    constexpr uint32_t WOW_AABB_OUTCODE_MAX_X = 0x02u;
    constexpr uint32_t WOW_AABB_OUTCODE_MIN_Y = 0x04u;
    constexpr uint32_t WOW_AABB_OUTCODE_MAX_Y = 0x08u;
    constexpr uint32_t WOW_AABB_OUTCODE_MIN_Z = 0x10u;
    constexpr uint32_t WOW_AABB_OUTCODE_MAX_Z = 0x20u;
    constexpr std::array<uint32_t, 5> WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES = { 0u, 9u, 17u, 1u, 18u };
    constexpr std::array<uint16_t, 12> WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES =
    {
        17u, 9u, 0u,
        9u, 1u, 0u,
        9u, 17u, 18u,
        9u, 18u, 1u,
    };
    constexpr uint32_t WOW_TERRAIN_QUERY_BASE_MASK_MODEL_TRUE = 0x00100111u;
    constexpr uint32_t WOW_TERRAIN_QUERY_BASE_MASK_MODEL_FALSE = 0x00102111u;
    constexpr uint32_t WOW_TERRAIN_QUERY_WATERWALK_AUGMENT = 0x00030000u;
    constexpr uint32_t WOW_TERRAIN_QUERY_TREE_AUGMENT = 0x00008000u;

    inline float EvaluatePlane(const G3D::Vector3& normal, float planeDistance, const G3D::Vector3& point)
    {
        return normal.dot(point) + planeDistance;
    }

    inline float ComputeBinaryRsqrt(float value)
    {
        return _mm_cvtss_f32(_mm_rsqrt_ss(_mm_set_ss(value)));
    }

    inline bool HasAnyTerrainQueryPayloadBitsSet(const WoWCollision::TerrainQueryPairPayload& payload)
    {
        uint32_t firstBits = 0u;
        uint32_t secondBits = 0u;
        std::memcpy(&firstBits, &payload.first, sizeof(firstBits));
        std::memcpy(&secondBits, &payload.second, sizeof(secondBits));
        return (firstBits | secondBits) != 0u;
    }

    inline int32_t QuantizeTerrainQueryGridAxis(float worldCoordinate)
    {
        return static_cast<int32_t>(std::floor(((WOW_TERRAIN_QUERY_GRID_CENTER - worldCoordinate) * WOW_TERRAIN_QUERY_GRID_SCALE) - WOW_TERRAIN_QUERY_GRID_BIAS));
    }

    inline G3D::Vector3 TransformSelectorLocalPointToWorld(const std::array<G3D::Vector3, 3>& basisRows,
                                                           const G3D::Vector3& localPoint)
    {
        return (basisRows[0] * localPoint.x) +
               (basisRows[1] * localPoint.y) +
               (basisRows[2] * localPoint.z);
    }

    inline G3D::Vector3 TransformSelectorWorldNormalToLocal(const std::array<G3D::Vector3, 3>& basisRows,
                                                            const G3D::Vector3& worldNormal)
    {
        return G3D::Vector3(
            (worldNormal.x * basisRows[0].x) + (worldNormal.y * basisRows[1].x) + (worldNormal.z * basisRows[2].x),
            (worldNormal.x * basisRows[0].y) + (worldNormal.y * basisRows[1].y) + (worldNormal.z * basisRows[2].y),
            (worldNormal.x * basisRows[0].z) + (worldNormal.y * basisRows[1].z) + (worldNormal.z * basisRows[2].z));
    }

    inline G3D::Vector3 NormalizeSelectorPlaneNormalBinary(const G3D::Vector3& normal)
    {
        return normal * (1.0f / normal.magnitude());
    }

    bool PlaneTouchesTopFootprintCorner(const SceneQuery::AABBContact& contact,
                                        const G3D::Vector3& position,
                                        float collisionRadius,
                                        float boundingHeight)
    {
        G3D::Vector3 normal = contact.normal.directionOrZero();
        if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
            normal = contact.rawNormal.directionOrZero();
        }
        if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
            return false;
        }

        const float topZ = position.z + boundingHeight;
        const G3D::Vector3 corners[4] = {
            G3D::Vector3(position.x - collisionRadius, position.y + collisionRadius, topZ),
            G3D::Vector3(position.x + collisionRadius, position.y + collisionRadius, topZ),
            G3D::Vector3(position.x - collisionRadius, position.y - collisionRadius, topZ),
            G3D::Vector3(position.x + collisionRadius, position.y - collisionRadius, topZ),
        };

        for (const auto& corner : corners) {
            if (std::fabs(EvaluatePlane(normal, contact.planeDistance, corner)) <= WOW_CORNER_PLANE_EPSILON) {
                return true;
            }
        }

        return false;
    }

    bool PointInsideExpandedTrianglePrism(const SceneQuery::AABBContact& contact, const G3D::Vector3& position)
    {
        const G3D::Vector3 up(0.0f, 0.0f, 1.0f);
        const G3D::Vector3 vertices[3] = { contact.triangleA, contact.triangleB, contact.triangleC };

        for (int i = 0; i < 3; ++i) {
            const G3D::Vector3& current = vertices[i];
            const G3D::Vector3& next = vertices[(i + 1) % 3];

            G3D::Vector3 edgePlaneNormal = (next - current).cross(up);
            const float edgePlaneMagSq = edgePlaneNormal.squaredMagnitude();
            if (edgePlaneMagSq <= WOW_PLANE_BUILD_EPSILON) {
                return false;
            }

            edgePlaneNormal = edgePlaneNormal * (1.0f / std::sqrt(edgePlaneMagSq));
            const float edgePlaneDistance = -edgePlaneNormal.dot(current);
            if (EvaluatePlane(edgePlaneNormal, edgePlaneDistance, position) > WOW_TRIANGLE_PRISM_EPSILON) {
                return false;
            }
        }

        return true;
    }

} // end anonymous namespace

WoWCollision::CheckWalkableResult WoWCollision::CheckWalkable(const SceneQuery::AABBContact& contact,
                                                              const G3D::Vector3& position,
                                                              float collisionRadius,
                                                              float boundingHeight,
                                                              bool useStandardWalkableThreshold,
                                                              bool groundedWallFlagBefore)
{
    CheckWalkableResult result{};
    result.groundedWallFlagAfter = groundedWallFlagBefore;

    G3D::Vector3 normal = contact.normal.directionOrZero();
    if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
        normal = contact.rawNormal.directionOrZero();
    }
    if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
        return result;
    }

    const float walkableThreshold = useStandardWalkableThreshold
        ? WOW_WALKABLE_MIN_NORMAL_Z
        : WOW_RELAXED_WALKABLE_MIN_NORMAL_Z;

    if (normal.z > walkableThreshold) {
        if (PointInsideExpandedTrianglePrism(contact, position)) {
            result.groundedWallFlagAfter = false;
        }

        result.walkable = true;
        return result;
    }

    if (normal.z >= 0.0f) {
        result.walkable = groundedWallFlagBefore;
        return result;
    }

    if (!PlaneTouchesTopFootprintCorner(contact, position, collisionRadius, boundingHeight)) {
        result.walkable = groundedWallFlagBefore;
        return result;
    }

    if (groundedWallFlagBefore) {
        result.walkableState = true;
        result.groundedWallFlagAfter = false;
    }

    result.walkable = (-normal.z) > WOW_WALKABLE_MIN_NORMAL_Z;
    return result;
}

WoWCollision::SelectedContactThresholdGateResult WoWCollision::EvaluateSelectedContactThresholdGate(
    const SceneQuery::AABBContact& contact,
    const G3D::Vector3& currentPosition,
    const G3D::Vector3& projectedPosition,
    bool useStandardWalkableThreshold)
{
    SelectedContactThresholdGateResult result{};

    G3D::Vector3 normal = contact.normal.directionOrZero();
    if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
        normal = contact.rawNormal.directionOrZero();
    }

    result.normalZ = normal.z;
    result.currentPositionInsidePrism = PointInsideExpandedTrianglePrism(contact, currentPosition);
    result.projectedPositionInsidePrism = PointInsideExpandedTrianglePrism(contact, projectedPosition);
    result.thresholdSensitive = normal.z <= (useStandardWalkableThreshold
        ? WOW_WALKABLE_MIN_NORMAL_Z
        : WOW_RELAXED_WALKABLE_MIN_NORMAL_Z);
    result.wouldUseDirectPair = result.thresholdSensitive && result.projectedPositionInsidePrism;
    return result;
}

bool WoWCollision::IsPointInsideAabbInclusive(const G3D::Vector3& boundsMin,
                                              const G3D::Vector3& boundsMax,
                                              const G3D::Vector3& point)
{
    if (point.x < boundsMin.x || point.y < boundsMin.y || point.z < boundsMin.z) {
        return false;
    }

    if (point.x > boundsMax.x || point.y > boundsMax.y || point.z > boundsMax.z) {
        return false;
    }

    return true;
}

bool WoWCollision::DoAabbsOverlapInclusive(const G3D::Vector3& boundsMinA,
                                           const G3D::Vector3& boundsMaxA,
                                           const G3D::Vector3& boundsMinB,
                                           const G3D::Vector3& boundsMaxB)
{
    return !(boundsMinA.x > boundsMaxB.x ||
             boundsMinA.y > boundsMaxB.y ||
             boundsMinA.z > boundsMaxB.z ||
             boundsMaxA.x < boundsMinB.x ||
             boundsMaxA.y < boundsMinB.y ||
             boundsMaxA.z < boundsMinB.z);
}

uint32_t WoWCollision::BuildAabbOutcode(const G3D::Vector3& point,
                                        const G3D::Vector3& boundsMin,
                                        const G3D::Vector3& boundsMax)
{
    uint32_t outcode = 0u;

    if (point.x < boundsMin.x) {
        outcode |= WOW_AABB_OUTCODE_MIN_X;
    } else if (point.x > boundsMax.x) {
        outcode |= WOW_AABB_OUTCODE_MAX_X;
    }

    if (point.y < boundsMin.y) {
        outcode |= WOW_AABB_OUTCODE_MIN_Y;
    } else if (point.y > boundsMax.y) {
        outcode |= WOW_AABB_OUTCODE_MAX_Y;
    }

    if (point.z < boundsMin.z) {
        outcode |= WOW_AABB_OUTCODE_MIN_Z;
    } else if (point.z > boundsMax.z) {
        outcode |= WOW_AABB_OUTCODE_MAX_Z;
    }

    return outcode;
}

bool WoWCollision::TriangleSharesAabbOutcodeReject(uint32_t firstOutcode,
                                                   uint32_t secondOutcode,
                                                   uint32_t thirdOutcode)
{
    return (firstOutcode & secondOutcode & thirdOutcode) != 0u;
}

bool WoWCollision::TriangleSharesSelectorPlaneOutcodeReject(uint32_t firstOutcode,
                                                            uint32_t secondOutcode,
                                                            uint32_t thirdOutcode)
{
    return (firstOutcode & secondOutcode & thirdOutcode) != 0u;
}

uint32_t WoWCollision::CountTrianglesPassingAabbOutcodeReject(const uint16_t* triangleIndices,
                                                              uint32_t triangleCount,
                                                              const uint32_t* vertexOutcodes,
                                                              uint32_t vertexOutcodeCount)
{
    if ((triangleCount != 0u && triangleIndices == nullptr) || (vertexOutcodeCount != 0u && vertexOutcodes == nullptr)) {
        return 0u;
    }

    uint32_t acceptedCount = 0u;
    for (uint32_t triangleIndex = 0; triangleIndex < triangleCount; ++triangleIndex) {
        const uint32_t baseIndex = triangleIndex * 3u;
        const uint32_t index0 = triangleIndices[baseIndex + 0u];
        const uint32_t index1 = triangleIndices[baseIndex + 1u];
        const uint32_t index2 = triangleIndices[baseIndex + 2u];
        if (index0 >= vertexOutcodeCount || index1 >= vertexOutcodeCount || index2 >= vertexOutcodeCount) {
            continue;
        }

        if (!TriangleSharesAabbOutcodeReject(
                vertexOutcodes[index0],
                vertexOutcodes[index1],
                vertexOutcodes[index2])) {
            ++acceptedCount;
        }
    }

    return acceptedCount;
}

uint32_t WoWCollision::BuildTerrainQueryMask(bool modelPropertyFlagSet,
                                             uint32_t movementFlags,
                                             float field20Value,
                                             bool rootTreeFlagSet,
                                             bool childTreeFlagSet)
{
    uint32_t queryMask = modelPropertyFlagSet
        ? WOW_TERRAIN_QUERY_BASE_MASK_MODEL_TRUE
        : WOW_TERRAIN_QUERY_BASE_MASK_MODEL_FALSE;

    if ((movementFlags & MOVEFLAG_WATERWALKING) != 0u &&
        (movementFlags & MOVEFLAG_SWIMMING) == 0u &&
        field20Value > WOW_TERRAIN_QUERY_FIELD20_THRESHOLD) {
        queryMask |= WOW_TERRAIN_QUERY_WATERWALK_AUGMENT;
    }

    if (rootTreeFlagSet && childTreeFlagSet) {
        queryMask |= WOW_TERRAIN_QUERY_TREE_AUGMENT;
    }

    return queryMask;
}

bool WoWCollision::IsTerrainQueryPayloadEnabled(uint32_t movementFlags,
                                                const TerrainQueryPairPayload& payload)
{
    if (HasAnyTerrainQueryPayloadBitsSet(payload)) {
        return (movementFlags & 0x00F00000u) != 0u;
    }

    return (movementFlags & 0x0000000Fu) != 0u;
}

bool WoWCollision::ShouldRunDynamicCallbackProducer(bool callbackPresent,
                                                    uint32_t movementFlags)
{
    return callbackPresent && ((movementFlags & 0x00F00000u) != 0u);
}

bool WoWCollision::ShouldVisitTerrainQueryStampedEntry(uint32_t entryVisitStamp,
                                                       uint32_t currentVisitStamp)
{
    return entryVisitStamp != currentVisitStamp;
}

uint32_t WoWCollision::BeginTerrainQueryProducerPass(uint32_t currentVisitStamp,
                                                     std::vector<SelectorCandidateRecord>& ioRecords)
{
    ioRecords.clear();
    return currentVisitStamp + 1u;
}

void WoWCollision::BuildTerrainQueryChunkSpan(const G3D::Vector3& worldBoundsMin,
                                              const G3D::Vector3& worldBoundsMax,
                                              TerrainQueryChunkSpan& outSpan)
{
    outSpan.cellMinX = QuantizeTerrainQueryGridAxis(worldBoundsMax.x);
    outSpan.cellMaxX = QuantizeTerrainQueryGridAxis(worldBoundsMin.x);
    outSpan.cellMinY = QuantizeTerrainQueryGridAxis(worldBoundsMax.y);
    outSpan.cellMaxY = QuantizeTerrainQueryGridAxis(worldBoundsMin.y);
    outSpan.chunkMinX = outSpan.cellMinX >> 3;
    outSpan.chunkMaxX = outSpan.cellMaxX >> 3;
    outSpan.chunkMinY = outSpan.cellMinY >> 3;
    outSpan.chunkMaxY = outSpan.cellMaxY >> 3;
}

uint32_t WoWCollision::EnumerateTerrainQueryChunkCoordinates(const TerrainQueryChunkSpan& span,
                                                             std::vector<TerrainQueryChunkCoordinate>& outCoordinates)
{
    outCoordinates.clear();
    for (int32_t primary = span.chunkMinX; primary <= span.chunkMaxX; ++primary) {
        for (int32_t secondary = span.chunkMinY; secondary <= span.chunkMaxY; ++secondary) {
            outCoordinates.push_back(TerrainQueryChunkCoordinate{ primary, secondary });
        }
    }

    return static_cast<uint32_t>(outCoordinates.size());
}

uint32_t WoWCollision::BuildOptionalSelectorChildDispatchMask(const uint32_t* childPresenceFlags,
                                                              uint32_t childCount,
                                                              uint32_t movementFlags)
{
    if (childCount != 0u && childPresenceFlags == nullptr) {
        return 0u;
    }

    uint32_t dispatchMask = 0u;
    for (uint32_t childIndex = 0; childIndex < childCount; ++childIndex) {
        if (childPresenceFlags[childIndex] == 0u) {
            continue;
        }

        const uint32_t flagBit = (1u << (childIndex + 16u));
        if ((movementFlags & flagBit) != 0u) {
            dispatchMask |= flagBit;
        }
    }

    return dispatchMask;
}

WoWCollision::TerrainQueryEntryDispatchAction WoWCollision::EvaluateTerrainQueryEntryDispatch(bool entryFlagMaskedOut,
                                                                                               bool alreadyVisited,
                                                                                               bool hasSourceGeometry,
                                                                                               uint32_t movementFlags,
                                                                                               const TerrainQueryPairPayload& payload,
                                                                                               bool traversalAllowsDispatch,
                                                                                               const G3D::Vector3& entryBoundsMin,
                                                                                               const G3D::Vector3& entryBoundsMax,
                                                                                               const G3D::Vector3& queryBoundsMin,
                                                                                               const G3D::Vector3& queryBoundsMax)
{
    if (entryFlagMaskedOut || alreadyVisited || !hasSourceGeometry) {
        return TERRAIN_QUERY_ENTRY_SKIP;
    }

    if (!IsTerrainQueryPayloadEnabled(movementFlags, payload)) {
        return TERRAIN_QUERY_ENTRY_SKIP;
    }

    if (!traversalAllowsDispatch) {
        return TERRAIN_QUERY_ENTRY_ABORT;
    }

    if (!DoAabbsOverlapInclusive(entryBoundsMin, entryBoundsMax, queryBoundsMin, queryBoundsMax)) {
        return TERRAIN_QUERY_ENTRY_SKIP;
    }

    return TERRAIN_QUERY_ENTRY_DISPATCH;
}

bool WoWCollision::ShouldDispatchDynamicTerrainQueryEntry(bool entryFlagEnabled,
                                                          bool alreadyVisited,
                                                          bool callbackSucceeded,
                                                          const G3D::Vector3& entryBoundsMin,
                                                          const G3D::Vector3& entryBoundsMax,
                                                          const G3D::Vector3& queryBoundsMin,
                                                          const G3D::Vector3& queryBoundsMax)
{
    return entryFlagEnabled &&
           !alreadyVisited &&
           callbackSucceeded &&
           DoAabbsOverlapInclusive(entryBoundsMin, entryBoundsMax, queryBoundsMin, queryBoundsMax);
}

void WoWCollision::BuildTerrainQueryBounds(const G3D::Vector3& projectedPosition,
                                           float collisionRadius,
                                           float boundingHeight,
                                           G3D::Vector3& outBoundsMin,
                                           G3D::Vector3& outBoundsMax)
{
    outBoundsMin.x = projectedPosition.x - collisionRadius;
    outBoundsMin.y = projectedPosition.y - collisionRadius;
    outBoundsMin.z = projectedPosition.z;

    outBoundsMax.x = projectedPosition.x + collisionRadius;
    outBoundsMax.y = projectedPosition.y + collisionRadius;
    outBoundsMax.z = projectedPosition.z + boundingHeight;
}

uint32_t WoWCollision::CopyTerrainQueryWalkableContactsAndPairs(const SceneQuery::AABBContact* inputContacts,
                                                                const TerrainQueryPairPayload* inputPairs,
                                                                uint32_t inputCount,
                                                                std::vector<SceneQuery::AABBContact>& outContacts,
                                                                std::vector<TerrainQueryPairPayload>& outPairs)
{
    outContacts.clear();
    outPairs.clear();

    if ((inputCount != 0u && inputContacts == nullptr) || (inputCount != 0u && inputPairs == nullptr)) {
        return 0u;
    }

    outContacts.reserve(inputCount);
    outPairs.reserve(inputCount);

    for (uint32_t i = 0; i < inputCount; ++i) {
        if (inputContacts[i].normal.z < WOW_WALKABLE_MIN_NORMAL_Z) {
            continue;
        }

        outContacts.push_back(inputContacts[i]);
        outPairs.push_back(inputPairs[i]);
    }

    return static_cast<uint32_t>(outContacts.size());
}

void WoWCollision::AppendTerrainQueryPairPayloadRange(uint32_t previousRecordCount,
                                                      uint32_t currentRecordCount,
                                                      const TerrainQueryPairPayload& payload,
                                                      std::vector<TerrainQueryPairPayload>& ioPairs)
{
    const uint32_t preservedCount = std::min(previousRecordCount, currentRecordCount);
    if (ioPairs.size() < preservedCount) {
        ioPairs.resize(preservedCount);
    }

    ioPairs.resize(currentRecordCount);
    for (uint32_t i = preservedCount; i < currentRecordCount; ++i) {
        ioPairs[static_cast<size_t>(i)] = payload;
    }
}

void WoWCollision::ZeroTerrainQueryPairPayloadRange(uint32_t previousRecordCount,
                                                    uint32_t currentRecordCount,
                                                    std::vector<TerrainQueryPairPayload>& ioPairs)
{
    const uint32_t preservedCount = std::min(previousRecordCount, currentRecordCount);
    if (ioPairs.size() < preservedCount) {
        ioPairs.resize(preservedCount);
    }

    ioPairs.resize(currentRecordCount);
    for (uint32_t i = preservedCount; i < currentRecordCount; ++i) {
        ioPairs[static_cast<size_t>(i)] = TerrainQueryPairPayload{};
    }
}

void WoWCollision::MergeAabbBounds(const G3D::Vector3& boundsMinA,
                                   const G3D::Vector3& boundsMaxA,
                                   const G3D::Vector3& boundsMinB,
                                   const G3D::Vector3& boundsMaxB,
                                   G3D::Vector3& outBoundsMin,
                                   G3D::Vector3& outBoundsMax)
{
    outBoundsMin = G3D::Vector3(
        std::min(boundsMinA.x, boundsMinB.x),
        std::min(boundsMinA.y, boundsMinB.y),
        std::min(boundsMinA.z, boundsMinB.z));
    outBoundsMax = G3D::Vector3(
        std::max(boundsMaxA.x, boundsMaxB.x),
        std::max(boundsMaxA.y, boundsMaxB.y),
        std::max(boundsMaxA.z, boundsMaxB.z));
}

void WoWCollision::AddScalarToVector3(G3D::Vector3& ioVector, float scalar)
{
    ioVector.x += scalar;
    ioVector.y += scalar;
    ioVector.z += scalar;
}

void WoWCollision::SubtractScalarFromVector3(G3D::Vector3& ioVector, float scalar)
{
    ioVector.x -= scalar;
    ioVector.y -= scalar;
    ioVector.z -= scalar;
}

void WoWCollision::BuildTerrainQueryCacheMissBounds(const G3D::Vector3& projectedPosition,
                                                    float collisionRadius,
                                                    float boundingHeight,
                                                    const G3D::Vector3& cachedBoundsMin,
                                                    const G3D::Vector3& cachedBoundsMax,
                                                    G3D::Vector3& outBoundsMin,
                                                    G3D::Vector3& outBoundsMax)
{
    G3D::Vector3 expandedQueryBoundsMin;
    G3D::Vector3 expandedQueryBoundsMax;
    BuildTerrainQueryBounds(
        projectedPosition,
        collisionRadius,
        boundingHeight,
        expandedQueryBoundsMin,
        expandedQueryBoundsMax);
    SubtractScalarFromVector3(expandedQueryBoundsMin, WOW_TERRAIN_QUERY_CACHE_MISS_EXPANSION);
    AddScalarToVector3(expandedQueryBoundsMax, WOW_TERRAIN_QUERY_CACHE_MISS_EXPANSION);
    MergeAabbBounds(
        expandedQueryBoundsMin,
        expandedQueryBoundsMax,
        cachedBoundsMin,
        cachedBoundsMax,
        outBoundsMin,
        outBoundsMax);
}

bool WoWCollision::EvaluateTerrainQueryMergedQueryTransaction(const G3D::Vector3& projectedPosition,
                                                              float collisionRadius,
                                                              float boundingHeight,
                                                              const G3D::Vector3& cachedBoundsMin,
                                                              const G3D::Vector3& cachedBoundsMax,
                                                              bool modelPropertyFlagSet,
                                                              uint32_t movementFlags,
                                                              float field20Value,
                                                              bool rootTreeFlagSet,
                                                              bool childTreeFlagSet,
                                                              bool queryDispatchSucceeded,
                                                              TerrainQueryMergedQueryTrace& outTrace)
{
    outTrace = TerrainQueryMergedQueryTrace{};
    BuildTerrainQueryBounds(
        projectedPosition,
        collisionRadius,
        boundingHeight,
        outTrace.queryBoundsMin,
        outTrace.queryBoundsMax);

    outTrace.cacheContainsBoundsMin = IsPointInsideAabbInclusive(
        cachedBoundsMin,
        cachedBoundsMax,
        outTrace.queryBoundsMin) ? 1u : 0u;
    outTrace.cacheContainsBoundsMax = IsPointInsideAabbInclusive(
        cachedBoundsMin,
        cachedBoundsMax,
        outTrace.queryBoundsMax) ? 1u : 0u;

    if (outTrace.cacheContainsBoundsMin != 0u && outTrace.cacheContainsBoundsMax != 0u) {
        outTrace.reusedCachedQuery = 1u;
        outTrace.returnedSuccess = 1u;
        return true;
    }

    BuildTerrainQueryCacheMissBounds(
        projectedPosition,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        outTrace.mergedBoundsMin,
        outTrace.mergedBoundsMax);
    outTrace.builtMergedBounds = 1u;

    outTrace.queryMask = BuildTerrainQueryMask(
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet);
    outTrace.builtQueryMask = 1u;
    outTrace.queryInvoked = 1u;
    outTrace.queryDispatchSucceeded = queryDispatchSucceeded ? 1u : 0u;
    outTrace.returnedSuccess = outTrace.queryDispatchSucceeded;
    return queryDispatchSucceeded;
}

bool WoWCollision::EvaluateTerrainQuerySelectedContactContainerTransaction(
    const G3D::Vector3& projectedPosition,
    float collisionRadius,
    float boundingHeight,
    const G3D::Vector3& cachedBoundsMin,
    const G3D::Vector3& cachedBoundsMax,
    bool modelPropertyFlagSet,
    uint32_t movementFlags,
    float field20Value,
    bool rootTreeFlagSet,
    bool childTreeFlagSet,
    const SceneQuery::AABBContact* existingContacts,
    const TerrainQueryPairPayload* existingPairs,
    uint32_t existingCount,
    const SceneQuery::AABBContact* queryContacts,
    const TerrainQueryPairPayload* queryPairs,
    uint32_t queryCount,
    bool queryDispatchSucceeded,
    std::vector<SceneQuery::AABBContact>& outContacts,
    std::vector<TerrainQueryPairPayload>& outPairs,
    TerrainQuerySelectedContactContainerTrace& outTrace)
{
    outTrace = TerrainQuerySelectedContactContainerTrace{};
    outContacts.clear();
    outPairs.clear();

    if ((existingCount != 0u && (existingContacts == nullptr || existingPairs == nullptr)) ||
        (queryCount != 0u && (queryContacts == nullptr || queryPairs == nullptr))) {
        return false;
    }

    const bool transactionSucceeded = EvaluateTerrainQueryMergedQueryTransaction(
        projectedPosition,
        collisionRadius,
        boundingHeight,
        cachedBoundsMin,
        cachedBoundsMax,
        modelPropertyFlagSet,
        movementFlags,
        field20Value,
        rootTreeFlagSet,
        childTreeFlagSet,
        queryDispatchSucceeded,
        outTrace.mergedQuery);

    if (outTrace.mergedQuery.reusedCachedQuery != 0u) {
        outTrace.reusedExistingContainer = 1u;
        outContacts.assign(existingContacts, existingContacts + existingCount);
        outPairs.assign(existingPairs, existingPairs + existingCount);
        outTrace.outputContactCount = existingCount;
        outTrace.returnedSuccess = 1u;
        return true;
    }

    if (!transactionSucceeded) {
        return false;
    }

    outTrace.copiedQueryResults = 1u;
    outTrace.outputContactCount = CopyTerrainQueryWalkableContactsAndPairs(
        queryContacts,
        queryPairs,
        queryCount,
        outContacts,
        outPairs);
    outTrace.returnedSuccess = 1u;
    return true;
}

void WoWCollision::NegatePlane(const G3D::Vector3& normal,
                               float planeDistance,
                               SelectorSupportPlane& outPlane)
{
    outPlane.normal = -normal;
    outPlane.planeDistance = -planeDistance;
}

void WoWCollision::BuildPlaneFromNormalAndPoint(const G3D::Vector3& normal,
                                                const G3D::Vector3& point,
                                                SelectorSupportPlane& outPlane)
{
    outPlane.normal = normal;
    outPlane.planeDistance = -normal.dot(point);
}

void WoWCollision::BuildObjectLocalQueryBounds(const G3D::Vector3& worldBoundsMin,
                                               const G3D::Vector3& worldBoundsMax,
                                               const G3D::Vector3& objectPosition,
                                               G3D::Vector3& outLocalBoundsMin,
                                               G3D::Vector3& outLocalBoundsMax)
{
    const G3D::Vector3 localOffset = -objectPosition;
    outLocalBoundsMin = worldBoundsMin + localOffset;
    outLocalBoundsMax = worldBoundsMax + localOffset;
}

bool WoWCollision::BuildPlaneFromTrianglePoints(const G3D::Vector3& point0,
                                                const G3D::Vector3& point1,
                                                const G3D::Vector3& point2,
                                                SelectorSupportPlane& outPlane)
{
    const G3D::Vector3 edge20 = point2 - point0;
    const G3D::Vector3 edge10 = point1 - point0;
    const G3D::Vector3 normal = edge20.cross(edge10);
    if (normal.squaredMagnitude() < (WOW_PLANE_BUILD_EPSILON * WOW_PLANE_BUILD_EPSILON)) {
        return false;
    }

    BuildPlaneFromNormalAndPoint(normal.directionOrZero(), point0, outPlane);
    return true;
}

bool WoWCollision::BuildSelectorHullSourceGeometry(const G3D::Vector3* supportPoints,
                                                   uint32_t supportPointCount,
                                                   SelectorSupportPlane* outPlanes,
                                                   uint32_t planeCount,
                                                   G3D::Vector3* outPoints,
                                                   uint32_t outPointCount,
                                                   G3D::Vector3* outAnchorPoint0,
                                                   G3D::Vector3* outAnchorPoint1)
{
    if (supportPointCount < 8u || planeCount < 6u || outPlanes == nullptr || outPointCount < 8u || outPoints == nullptr || supportPoints == nullptr) {
        return false;
    }

    for (uint32_t i = 0; i < 6u; ++i) {
        InitializeSelectorSupportPlane(outPlanes[i]);
    }

    for (uint32_t i = 0; i < 8u; ++i) {
        outPoints[i] = G3D::Vector3::zero();
    }

    if (outAnchorPoint0) {
        *outAnchorPoint0 = G3D::Vector3::zero();
    }

    if (outAnchorPoint1) {
        *outAnchorPoint1 = G3D::Vector3::zero();
    }

    for (uint32_t i = 0; i < 8u; ++i) {
        outPoints[i] = supportPoints[i];
    }

    const G3D::Vector3& point0 = outPoints[0];
    const G3D::Vector3& point1 = outPoints[1];
    const G3D::Vector3& point2 = outPoints[2];
    const G3D::Vector3& point3 = outPoints[3];
    const G3D::Vector3& point4 = outPoints[4];
    const G3D::Vector3& point5 = outPoints[5];
    const G3D::Vector3& point6 = outPoints[6];
    const G3D::Vector3& point7 = outPoints[7];

    BuildPlaneFromNormalAndPoint(
        NormalizeSelectorPlaneNormalBinary((point6 - point1).cross(point5 - point1)),
        point1,
        outPlanes[0]);
    BuildPlaneFromNormalAndPoint(
        NormalizeSelectorPlaneNormalBinary((point7 - point0).cross(point4 - point0)),
        point0,
        outPlanes[1]);
    BuildPlaneFromNormalAndPoint(
        NormalizeSelectorPlaneNormalBinary((point4 - point0).cross(point5 - point0)),
        point0,
        outPlanes[2]);
    BuildPlaneFromNormalAndPoint(
        NormalizeSelectorPlaneNormalBinary((point6 - point3).cross(point7 - point3)),
        point3,
        outPlanes[3]);
    BuildPlaneFromNormalAndPoint(
        NormalizeSelectorPlaneNormalBinary((point4 - point5).cross(point6 - point5)),
        point5,
        outPlanes[4]);
    BuildPlaneFromNormalAndPoint(
        -outPlanes[4].normal,
        point2,
        outPlanes[5]);

    return true;
}

bool WoWCollision::TransformSelectorSupportPointBuffer(const G3D::Vector3* inputPoints,
                                                       uint32_t pointCount,
                                                       const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                       const G3D::Vector3& translation,
                                                       G3D::Vector3* outPoints,
                                                       uint32_t outPointCount)
{
    if ((pointCount != 0u && inputPoints == nullptr) || pointCount > outPointCount || outPoints == nullptr) {
        return false;
    }

    for (uint32_t i = 0; i < pointCount; ++i) {
        outPoints[i] = TransformSelectorLocalPointToWorld(transformBasisRows, inputPoints[i]) + translation;
    }

    return true;
}

uint32_t WoWCollision::BuildSelectorObjectCallbackMask(uint32_t movementFlags)
{
    uint32_t callbackMask = (movementFlags & 0x10u) != 0u ? 0xC6u : 0xEEu;
    if ((movementFlags & 0x20u) != 0u) {
        callbackMask &= ~0x24u;
    }

    if ((movementFlags & 0x40u) == 0u) {
        callbackMask &= ~0x02u;
    }

    if ((movementFlags & 0x4000u) == 0u) {
        callbackMask &= ~0x40u;
    }

    return callbackMask;
}

bool WoWCollision::ShouldResolveSelectorObjectNode(bool selectorEnabled,
                                                   bool nodeEnabled,
                                                   bool allowInactiveNode)
{
    return selectorEnabled && (nodeEnabled || allowInactiveNode);
}

const void* WoWCollision::ResolveSelectorObjectNodePointer(bool selectorEnabled,
                                                           const void* nodePointer,
                                                           bool nodeEnabled,
                                                           bool allowInactiveNode)
{
    return ShouldResolveSelectorObjectNode(
               selectorEnabled,
               nodeEnabled,
               allowInactiveNode)
        ? nodePointer
        : nullptr;
}

uint32_t WoWCollision::EvaluateSelectorObjectRouterEntries(const SelectorObjectRouterEntryRecord* entries,
                                                           uint32_t entryCount,
                                                           bool selectorEnabled,
                                                           const G3D::Vector3& queryBoundsMin,
                                                           const G3D::Vector3& queryBoundsMax,
                                                           SelectorObjectRouterTrace* outTrace)
{
    SelectorObjectRouterTrace trace{};
    if (entryCount != 0u && entries == nullptr) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    uint32_t accumulator = 0u;
    for (uint32_t entryIndex = 0; entryIndex < entryCount; ++entryIndex) {
        const SelectorObjectRouterEntryRecord& entry = entries[entryIndex];
        if (!DoAabbsOverlapInclusive(entry.boundsMin, entry.boundsMax, queryBoundsMin, queryBoundsMax)) {
            ++trace.overlapRejectedCount;
            continue;
        }

        const void* nodePointer = ResolveSelectorObjectNodePointer(
            selectorEnabled,
            reinterpret_cast<const void*>(static_cast<uintptr_t>(entry.nodeToken)),
            entry.nodeEnabled != 0u,
            false);
        if (nodePointer == nullptr) {
            ++trace.nodeRejectedCount;
            continue;
        }

        ++trace.dispatchedCount;
        const uint32_t previousAccumulator = accumulator;
        accumulator |= (entry.callbackReturn & 0xFFu);
        if (accumulator != previousAccumulator) {
            ++trace.accumulatorUpdatedCount;
        }
    }

    trace.result = accumulator;
    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return accumulator;
}

bool WoWCollision::ShouldUseSelectorObjectCallback(uint64_t callbackToken)
{
    return callbackToken != 0u;
}

void WoWCollision::FinalizeSelectorObjectNoCallbackState(uint32_t inputHitResult,
                                                         uint32_t inputRecordCount,
                                                         uint32_t inputOutputFlags,
                                                         SelectorObjectNoCallbackState& outState)
{
    outState.hitResult = inputHitResult;
    outState.recordCount = inputRecordCount;
    outState.outputFlags = inputOutputFlags;
}

uint32_t WoWCollision::EvaluateSelectorLeafQueueMutation(uint32_t triangleIndex,
                                                         uint32_t stateMaskByte,
                                                         bool predicateRejected,
                                                         uint32_t& ioOverflowFlags,
                                                         uint16_t* pendingIds,
                                                         uint32_t pendingIdCapacity,
                                                         uint32_t& ioPendingCount,
                                                         uint16_t* acceptedIds,
                                                         uint32_t acceptedIdCapacity,
                                                         uint32_t& ioAcceptedCount,
                                                         uint8_t* stateBytes,
                                                         uint32_t stateByteCount,
                                                         SelectorLeafQueueMutationTrace* outTrace)
{
    SelectorLeafQueueMutationTrace trace{};
    const uint32_t stateByteIndex = triangleIndex * 2u;
    if ((stateBytes == nullptr) ||
        stateByteIndex >= stateByteCount ||
        (pendingIds == nullptr && pendingIdCapacity != 0u) ||
        (acceptedIds == nullptr && acceptedIdCapacity != 0u) ||
        ioPendingCount > pendingIdCapacity ||
        ioAcceptedCount > acceptedIdCapacity) {
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    trace.stateByteBefore = stateBytes[stateByteIndex];
    if ((trace.stateByteBefore & (stateMaskByte & 0xFFu)) != 0u) {
        trace.skippedByMask = 1u;
        trace.stateByteAfter = stateBytes[stateByteIndex];
        trace.pendingCountAfter = ioPendingCount;
        trace.acceptedCountAfter = ioAcceptedCount;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    constexpr uint32_t WOW_SELECTOR_PENDING_QUEUE_CAPACITY = 0x2000u;
    if (ioPendingCount >= WOW_SELECTOR_PENDING_QUEUE_CAPACITY || ioPendingCount >= pendingIdCapacity) {
        ioOverflowFlags |= 1u;
        trace.overflowed = 1u;
        trace.stateByteAfter = stateBytes[stateByteIndex];
        trace.pendingCountAfter = ioPendingCount;
        trace.acceptedCountAfter = ioAcceptedCount;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    pendingIds[ioPendingCount] = static_cast<uint16_t>(triangleIndex & 0xFFFFu);
    ++ioPendingCount;
    trace.pendingEnqueued = 1u;

    stateBytes[stateByteIndex] |= 0x80u;
    trace.visitedBitSet = 1u;

    if (predicateRejected) {
        trace.predicateRejected = 1u;
        trace.stateByteAfter = stateBytes[stateByteIndex];
        trace.pendingCountAfter = ioPendingCount;
        trace.acceptedCountAfter = ioAcceptedCount;
        if (outTrace != nullptr) {
            *outTrace = trace;
        }

        return 0u;
    }

    if (ioAcceptedCount < acceptedIdCapacity) {
        acceptedIds[ioAcceptedCount] = static_cast<uint16_t>(triangleIndex & 0xFFFFu);
        ++ioAcceptedCount;
        trace.acceptedEnqueued = 1u;
    }

    trace.stateByteAfter = stateBytes[stateByteIndex];
    trace.pendingCountAfter = ioPendingCount;
    trace.acceptedCountAfter = ioAcceptedCount;
    if (outTrace != nullptr) {
        *outTrace = trace;
    }

    return trace.acceptedEnqueued;
}

bool WoWCollision::BuildSelectorNodeTraversalPayload(const SelectorNodeTraversalRecord& node,
                                                     const G3D::Vector3* querySupportPoints,
                                                     uint32_t supportPointCount,
                                                     uint32_t callbackMask,
                                                     SelectorNodeTraversalPayload& outPayload)
{
    if (querySupportPoints == nullptr || supportPointCount == 0u) {
        outPayload = SelectorNodeTraversalPayload{};
        return false;
    }

    outPayload = SelectorNodeTraversalPayload{};
    BuildSelectorSupportPointBounds(
        querySupportPoints,
        supportPointCount,
        outPayload.queryBoundsMin,
        outPayload.queryBoundsMax);
    outPayload.callbackMaskWord = (callbackMask | 0x80u) & 0xFFFFu;
    outPayload.acceptedCount = 0u;
    outPayload.traversalBaseToken = node.traversalBaseToken;
    outPayload.extraNodeToken = node.extraNodeToken;
    outPayload.stateBytesToken = node.stateBytesToken;
    outPayload.vertexBufferToken = node.vertexBufferToken;
    outPayload.triangleIndexToken = node.triangleIndexToken;
    return true;
}

void WoWCollision::BuildSelectorSupportPointBounds(const G3D::Vector3* points,
                                                   uint32_t pointCount,
                                                   G3D::Vector3& outBoundsMin,
                                                   G3D::Vector3& outBoundsMax)
{
    if (pointCount == 0u || points == nullptr) {
        outBoundsMin = G3D::Vector3::zero();
        outBoundsMax = G3D::Vector3::zero();
        return;
    }

    outBoundsMin = points[0];
    outBoundsMax = points[0];
    for (uint32_t i = 1u; i < pointCount; ++i) {
        outBoundsMin = G3D::Vector3(
            std::min(outBoundsMin.x, points[i].x),
            std::min(outBoundsMin.y, points[i].y),
            std::min(outBoundsMin.z, points[i].z));
        outBoundsMax = G3D::Vector3(
            std::max(outBoundsMax.x, points[i].x),
            std::max(outBoundsMax.y, points[i].y),
            std::max(outBoundsMax.z, points[i].z));
    }
}

bool WoWCollision::BuildSelectorDynamicObjectHullSourceGeometry(const SelectorSupportPlane* sourcePlanes,
                                                                uint32_t planeCount,
                                                                const G3D::Vector3& objectBoundsMin,
                                                                const G3D::Vector3& objectBoundsMax,
                                                                const G3D::Vector3* localSupportPoints,
                                                                uint32_t supportPointCount,
                                                                const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                                const G3D::Vector3& translation,
                                                                SelectorSupportPlane* outPlanes,
                                                                uint32_t outPlaneCount,
                                                                G3D::Vector3* outPoints,
                                                                uint32_t outPointCount,
                                                                G3D::Vector3* outAnchorPoint0,
                                                                G3D::Vector3* outAnchorPoint1)
{
    if ((planeCount != 0u && sourcePlanes == nullptr) ||
        supportPointCount < 8u ||
        localSupportPoints == nullptr ||
        outPlaneCount < 6u ||
        outPlanes == nullptr ||
        outPointCount < 8u ||
        outPoints == nullptr) {
        return false;
    }

    if (EvaluateSelectorSourceAabbCull(sourcePlanes, planeCount, objectBoundsMin, objectBoundsMax) == 0u) {
        return false;
    }

    std::array<G3D::Vector3, 8> transformedPoints{};
    if (!TransformSelectorSupportPointBuffer(
            localSupportPoints,
            8u,
            transformBasisRows,
            translation,
            transformedPoints.data(),
            static_cast<uint32_t>(transformedPoints.size()))) {
        return false;
    }

    return BuildSelectorHullSourceGeometry(
        transformedPoints.data(),
        static_cast<uint32_t>(transformedPoints.size()),
        outPlanes,
        outPlaneCount,
        outPoints,
        outPointCount,
        outAnchorPoint0,
        outAnchorPoint1);
}

bool WoWCollision::BuildSelectorBvhChildTraversal(const SelectorBvhNodeRecord& node,
                                                  const G3D::Vector3& boundsMin,
                                                  const G3D::Vector3& boundsMax,
                                                  SelectorBvhChildTraversal& outTraversal)
{
    outTraversal = SelectorBvhChildTraversal{};

    if ((node.controlWord & 0x4u) != 0u) {
        return false;
    }

    const uint32_t axis = node.controlWord & 0x3u;
    if (axis > 2u) {
        return false;
    }

    outTraversal.axis = axis;
    outTraversal.splitCoordinate = node.splitCoordinate;
    outTraversal.lowChildIndex = node.lowChildIndex;
    outTraversal.highChildIndex = node.highChildIndex;
    outTraversal.lowBoundsMin = boundsMin;
    outTraversal.lowBoundsMax = boundsMax;
    outTraversal.highBoundsMin = boundsMin;
    outTraversal.highBoundsMax = boundsMax;

    const float axisMin = axis == 0u ? boundsMin.x : (axis == 1u ? boundsMin.y : boundsMin.z);
    const float axisMax = axis == 0u ? boundsMax.x : (axis == 1u ? boundsMax.y : boundsMax.z);

    if (axisMin <= node.splitCoordinate) {
        outTraversal.visitLow = 1u;
        if (axis == 0u) {
            outTraversal.lowBoundsMax.x = std::min(outTraversal.lowBoundsMax.x, node.splitCoordinate);
        } else if (axis == 1u) {
            outTraversal.lowBoundsMax.y = std::min(outTraversal.lowBoundsMax.y, node.splitCoordinate);
        } else {
            outTraversal.lowBoundsMax.z = std::min(outTraversal.lowBoundsMax.z, node.splitCoordinate);
        }
    }

    if (axisMax >= node.splitCoordinate) {
        outTraversal.visitHigh = 1u;
        if (axis == 0u) {
            outTraversal.highBoundsMin.x = std::max(outTraversal.highBoundsMin.x, node.splitCoordinate);
        } else if (axis == 1u) {
            outTraversal.highBoundsMin.y = std::max(outTraversal.highBoundsMin.y, node.splitCoordinate);
        } else {
            outTraversal.highBoundsMin.z = std::max(outTraversal.highBoundsMin.z, node.splitCoordinate);
        }
    }

    return true;
}

void WoWCollision::TranslateSelectorSourceGeometry(const G3D::Vector3& translation,
                                                   SelectorSupportPlane* ioPlanes,
                                                   uint32_t planeCount,
                                                   G3D::Vector3* ioPoints,
                                                   uint32_t pointCount,
                                                   G3D::Vector3* ioAnchorPoint0,
                                                   G3D::Vector3* ioAnchorPoint1)
{
    if (ioPoints) {
        for (uint32_t i = 0; i < pointCount; ++i) {
            ioPoints[i] += translation;
        }
    }

    if (ioPlanes) {
        for (uint32_t i = 0; i < planeCount; ++i) {
            ioPlanes[i].planeDistance -= ioPlanes[i].normal.dot(translation);
        }
    }

    if (ioAnchorPoint0) {
        *ioAnchorPoint0 += translation;
    }

    if (ioAnchorPoint1) {
        *ioAnchorPoint1 += translation;
    }
}

uint32_t WoWCollision::BuildSelectorSourcePlaneOutcode(const SelectorSupportPlane* planes,
                                                       uint32_t planeCount,
                                                       const G3D::Vector3& point)
{
    if (planeCount != 0u && planes == nullptr) {
        return 0u;
    }

    uint32_t outcode = 0u;
    for (uint32_t i = 0; i < planeCount; ++i) {
        const float signedDistance = planes[i].normal.dot(point) + planes[i].planeDistance;
        if (signedDistance < WOW_SELECTOR_SOURCE_OUTCODE_THRESHOLD) {
            outcode |= (1u << i);
        }
    }

    return outcode;
}

uint32_t WoWCollision::EvaluateSelectorSourceAabbCull(const SelectorSupportPlane* planes,
                                                      uint32_t planeCount,
                                                      const G3D::Vector3& boundsMin,
                                                      const G3D::Vector3& boundsMax)
{
    if (planeCount != 0u && planes == nullptr) {
        return 0u;
    }

    for (uint32_t i = 0; i < planeCount; ++i) {
        const G3D::Vector3 supportPoint(
            planes[i].normal.x < 0.0f ? boundsMin.x : boundsMax.x,
            planes[i].normal.y < 0.0f ? boundsMin.y : boundsMax.y,
            planes[i].normal.z < 0.0f ? boundsMin.z : boundsMax.z);
        if ((planes[i].normal.dot(supportPoint) + planes[i].planeDistance) < WOW_SELECTOR_SOURCE_OUTCODE_THRESHOLD) {
            return 0u;
        }
    }

    return 3u;
}

uint32_t WoWCollision::EvaluateSelectorHullTransformedBoundsCull(const SelectorSupportPlane* planes,
                                                                uint32_t planeCount,
                                                                const G3D::Vector3& localBoundsMin,
                                                                const G3D::Vector3& localBoundsMax,
                                                                const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                                const G3D::Vector3& translation)
{
    if (planeCount != 0u && planes == nullptr) {
        return 0u;
    }

    for (uint32_t i = 0; i < planeCount; ++i) {
        const G3D::Vector3 localPlaneNormal = TransformSelectorWorldNormalToLocal(
            transformBasisRows,
            planes[i].normal);

        const G3D::Vector3 localSupportPoint(
            localPlaneNormal.x < 0.0f ? localBoundsMin.x : localBoundsMax.x,
            localPlaneNormal.y < 0.0f ? localBoundsMin.y : localBoundsMax.y,
            localPlaneNormal.z < 0.0f ? localBoundsMin.z : localBoundsMax.z);
        const G3D::Vector3 worldSupportPoint = TransformSelectorLocalPointToWorld(
            transformBasisRows,
            localSupportPoint) + translation;

        if ((planes[i].normal.dot(worldSupportPoint) + planes[i].planeDistance) < WOW_SELECTOR_SOURCE_OUTCODE_THRESHOLD) {
            return 0u;
        }
    }

    return 3u;
}

uint32_t WoWCollision::EvaluateSelectorHullPointWithMargin(const SelectorSupportPlane* planes,
                                                           uint32_t planeCount,
                                                           const G3D::Vector3& point,
                                                           float margin)
{
    if (planeCount != 0u && planes == nullptr) {
        return 0u;
    }

    const float threshold = -margin;
    for (uint32_t i = 0; i < planeCount; ++i) {
        if ((planes[i].normal.dot(point) + planes[i].planeDistance) < threshold) {
            return 0u;
        }
    }

    return 3u;
}

uint32_t WoWCollision::EvaluateSelectorHullPointEpsilon(const SelectorSupportPlane* planes,
                                                        uint32_t planeCount,
                                                        const G3D::Vector3& point)
{
    if (planeCount != 0u && planes == nullptr) {
        return 0u;
    }

    for (uint32_t i = 0; i < planeCount; ++i) {
        if ((planes[i].normal.dot(point) + planes[i].planeDistance) < WOW_SELECTOR_SOURCE_OUTCODE_THRESHOLD) {
            return 0u;
        }
    }

    return 3u;
}

uint32_t WoWCollision::CountSelectorSourceTrianglesPassingPlaneOutcodes(const SelectorSupportPlane* planes,
                                                                        uint32_t planeCount,
                                                                        const G3D::Vector3* samplePoints,
                                                                        uint32_t samplePointCount)
{
    if ((samplePointCount != 0u && samplePoints == nullptr) || samplePointCount <= WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back()) {
        return 0u;
    }

    std::array<uint32_t, WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back() + 1u> pointOutcodes{};
    for (uint32_t sampleIndex : WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES) {
        pointOutcodes[sampleIndex] = BuildSelectorSourcePlaneOutcode(planes, planeCount, samplePoints[sampleIndex]);
    }

    uint32_t acceptedCount = 0u;
    for (size_t triangleOffset = 0; triangleOffset < WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES.size(); triangleOffset += 3u) {
        const uint16_t point0Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset];
        const uint16_t point1Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 1u];
        const uint16_t point2Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 2u];
        if (!TriangleSharesSelectorPlaneOutcodeReject(
                pointOutcodes[point0Index],
                pointOutcodes[point1Index],
                pointOutcodes[point2Index])) {
            ++acceptedCount;
        }
    }

    return acceptedCount;
}

bool WoWCollision::BuildSelectorSourceScanWindow(int32_t cellRowIndex,
                                                 int32_t cellColumnIndex,
                                                 int32_t queryRowMin,
                                                 int32_t queryColumnMin,
                                                 int32_t queryRowMax,
                                                 int32_t queryColumnMax,
                                                 SelectorSourceScanWindow& outWindow)
{
    outWindow.rowMin = std::max(queryRowMin - (cellRowIndex * 8), 0);
    outWindow.columnMin = std::max(queryColumnMin - (cellColumnIndex * 8), 0);
    outWindow.rowMax = std::min(queryRowMax - (cellRowIndex * 8), 7);
    outWindow.columnMax = std::min(queryColumnMax - (cellColumnIndex * 8), 7);
    if (outWindow.rowMin > outWindow.rowMax || outWindow.columnMin > outWindow.columnMax) {
        return false;
    }

    outWindow.pointStartIndex = (outWindow.rowMin * 17) + outWindow.columnMin;
    outWindow.rowAdvancePointCount = 17 - ((outWindow.columnMax - outWindow.columnMin) + 1);
    return true;
}

uint32_t WoWCollision::BuildLocalBoundsAabbOutcode(const G3D::Vector3& localBoundsMin,
                                                   const G3D::Vector3& localBoundsMax,
                                                   const G3D::Vector3& point)
{
    uint32_t outcode = 0u;
    if ((point.x - localBoundsMin.x + WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) < 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MIN_X;
    }

    if ((point.x - localBoundsMax.x - WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) > 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MAX_X;
    }

    if ((point.y - localBoundsMin.y + WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) < 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MIN_Y;
    }

    if ((point.y - localBoundsMax.y - WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) > 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MAX_Y;
    }

    if ((point.z - localBoundsMin.z + WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) < 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MIN_Z;
    }

    if ((point.z - localBoundsMax.z - WOW_SELECTOR_LOCAL_BOUNDS_OUTCODE_EPSILON) > 0.0f) {
        outcode |= WOW_AABB_OUTCODE_MAX_Z;
    }

    return outcode;
}

bool WoWCollision::EvaluateTriangleLocalBoundsAabbReject(const G3D::Vector3& localBoundsMin,
                                                         const G3D::Vector3& localBoundsMax,
                                                         const G3D::Vector3& point0,
                                                         const G3D::Vector3& point1,
                                                         const G3D::Vector3& point2)
{
    const uint32_t outcode0 = BuildLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, point0);
    const uint32_t outcode1 = BuildLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, point1);
    const uint32_t outcode2 = BuildLocalBoundsAabbOutcode(localBoundsMin, localBoundsMax, point2);
    return TriangleSharesAabbOutcodeReject(outcode0, outcode1, outcode2);
}

uint32_t WoWCollision::BuildSelectorSourceSubcellMask(uint32_t rowIndex,
                                                      uint32_t columnIndex)
{
    return 1u << (((rowIndex >> 1u) << 2u) + (columnIndex >> 1u));
}

bool WoWCollision::IsSelectorSourceSubcellMaskedOut(uint32_t rowIndex,
                                                    uint32_t columnIndex,
                                                    uint32_t cellMaskFlags)
{
    return (BuildSelectorSourceSubcellMask(rowIndex, columnIndex) & cellMaskFlags) != 0u;
}

void WoWCollision::BuildTranslatedTriangleSelectorRecord(const G3D::Vector3& localPoint0,
                                                         const G3D::Vector3& localPoint1,
                                                         const G3D::Vector3& localPoint2,
                                                         const G3D::Vector3& translation,
                                                         bool useApproximatePlaneBuildPath,
                                                         SelectorCandidateRecord& outRecord)
{
    outRecord.filterPlane = {};
    outRecord.points[0] = localPoint0 + translation;
    outRecord.points[1] = localPoint1 + translation;
    outRecord.points[2] = localPoint2 + translation;

    if (useApproximatePlaneBuildPath) {
        // 0x6AC54C..0x6AC616 takes the SSE rsqrt path when the client CPU feature flag is set.
        G3D::Vector3 normal = (outRecord.points[2] - outRecord.points[0]).cross(outRecord.points[1] - outRecord.points[0]);
        normal = normal * ComputeBinaryRsqrt(normal.squaredMagnitude());
        BuildPlaneFromNormalAndPoint(normal, outRecord.points[0], outRecord.filterPlane);
        return;
    }

    BuildPlaneFromTrianglePoints(
        outRecord.points[0],
        outRecord.points[1],
        outRecord.points[2],
        outRecord.filterPlane);
}

uint32_t WoWCollision::AppendSelectorSourceTriangleCandidateRecords(const SelectorSupportPlane* planes,
                                                                    uint32_t planeCount,
                                                                    const G3D::Vector3* samplePoints,
                                                                    uint32_t samplePointCount,
                                                                    const G3D::Vector3& translation,
                                                                    bool useApproximatePlaneBuildPath,
                                                                    std::vector<SelectorCandidateRecord>& ioRecords)
{
    if ((samplePointCount != 0u && samplePoints == nullptr) || samplePointCount <= WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back()) {
        return 0u;
    }

    const size_t initialCount = ioRecords.size();
    ioRecords.reserve(initialCount + CountSelectorSourceTrianglesPassingPlaneOutcodes(
        planes,
        planeCount,
        samplePoints,
        samplePointCount));

    std::array<uint32_t, WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back() + 1u> pointOutcodes{};
    for (uint32_t sampleIndex : WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES) {
        pointOutcodes[sampleIndex] = BuildSelectorSourcePlaneOutcode(planes, planeCount, samplePoints[sampleIndex]);
    }

    for (size_t triangleOffset = 0; triangleOffset < WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES.size(); triangleOffset += 3u) {
        const uint16_t point0Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset];
        const uint16_t point1Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 1u];
        const uint16_t point2Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 2u];
        if (TriangleSharesSelectorPlaneOutcodeReject(
                pointOutcodes[point0Index],
                pointOutcodes[point1Index],
                pointOutcodes[point2Index])) {
            continue;
        }

        SelectorCandidateRecord record{};
        BuildTranslatedTriangleSelectorRecord(
            samplePoints[point0Index],
            samplePoints[point1Index],
            samplePoints[point2Index],
            translation,
            useApproximatePlaneBuildPath,
            record);
        ioRecords.push_back(record);
    }

    return static_cast<uint32_t>(ioRecords.size() - initialCount);
}

uint32_t WoWCollision::AppendSelectorSourceScanWindowCandidateRecords(const SelectorSupportPlane* planes,
                                                                      uint32_t planeCount,
                                                                      const G3D::Vector3* pointGrid,
                                                                      uint32_t pointGridPointCount,
                                                                      const SelectorSourceScanWindow& scanWindow,
                                                                      uint32_t cellMaskFlags,
                                                                      const G3D::Vector3& translation,
                                                                      bool useApproximatePlaneBuildPath,
                                                                      std::vector<SelectorCandidateRecord>& ioRecords)
{
    if (pointGrid == nullptr ||
        scanWindow.rowMin > scanWindow.rowMax ||
        scanWindow.columnMin > scanWindow.columnMax ||
        scanWindow.pointStartIndex < 0 ||
        static_cast<uint32_t>(scanWindow.pointStartIndex) >= pointGridPointCount) {
        return 0u;
    }

    const size_t initialCount = ioRecords.size();
    int32_t currentPointIndex = scanWindow.pointStartIndex;
    for (int32_t rowIndex = scanWindow.rowMin; rowIndex <= scanWindow.rowMax; ++rowIndex) {
        int32_t currentColumnIndex = currentPointIndex;
        for (int32_t columnIndex = scanWindow.columnMin; columnIndex <= scanWindow.columnMax; ++columnIndex) {
            if (!IsSelectorSourceSubcellMaskedOut(
                    static_cast<uint32_t>(rowIndex),
                    static_cast<uint32_t>(columnIndex),
                    cellMaskFlags) &&
                currentColumnIndex >= 0 &&
                static_cast<uint32_t>(currentColumnIndex) < pointGridPointCount) {
                AppendSelectorSourceTriangleCandidateRecords(
                    planes,
                    planeCount,
                    pointGrid + currentColumnIndex,
                    pointGridPointCount - static_cast<uint32_t>(currentColumnIndex),
                    translation,
                    useApproximatePlaneBuildPath,
                    ioRecords);
            }

            ++currentColumnIndex;
        }

        currentPointIndex = currentColumnIndex + scanWindow.rowAdvancePointCount;
    }

    return static_cast<uint32_t>(ioRecords.size() - initialCount);
}

uint32_t WoWCollision::AppendLocalBoundsScanWindowTriangleCandidateRecords(const G3D::Vector3& localBoundsMin,
                                                                           const G3D::Vector3& localBoundsMax,
                                                                           const G3D::Vector3* pointGrid,
                                                                           uint32_t pointGridPointCount,
                                                                           const SelectorSourceScanWindow& scanWindow,
                                                                           uint32_t cellMaskFlags,
                                                                           const G3D::Vector3& translation,
                                                                           bool useApproximatePlaneBuildPath,
                                                                           std::vector<SelectorCandidateRecord>& ioRecords)
{
    if (pointGrid == nullptr ||
        scanWindow.rowMin > scanWindow.rowMax ||
        scanWindow.columnMin > scanWindow.columnMax ||
        scanWindow.pointStartIndex < 0 ||
        static_cast<uint32_t>(scanWindow.pointStartIndex) >= pointGridPointCount) {
        return 0u;
    }

    const size_t initialCount = ioRecords.size();
    int32_t currentPointIndex = scanWindow.pointStartIndex;
    for (int32_t rowIndex = scanWindow.rowMin; rowIndex <= scanWindow.rowMax; ++rowIndex) {
        int32_t currentColumnIndex = currentPointIndex;
        for (int32_t columnIndex = scanWindow.columnMin; columnIndex <= scanWindow.columnMax; ++columnIndex) {
            if (IsSelectorSourceSubcellMaskedOut(
                    static_cast<uint32_t>(rowIndex),
                    static_cast<uint32_t>(columnIndex),
                    cellMaskFlags) ||
                currentColumnIndex < 0 ||
                (static_cast<uint32_t>(currentColumnIndex) + WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back()) >= pointGridPointCount) {
                ++currentColumnIndex;
                continue;
            }

            std::array<uint32_t, WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES.back() + 1u> pointOutcodes{};
            for (uint32_t sampleIndex : WOW_SELECTOR_SOURCE_SAMPLE_POINT_INDICES) {
                pointOutcodes[sampleIndex] = BuildLocalBoundsAabbOutcode(
                    localBoundsMin,
                    localBoundsMax,
                    pointGrid[currentColumnIndex + sampleIndex]);
            }

            for (size_t triangleOffset = 0; triangleOffset < WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES.size(); triangleOffset += 3u) {
                const uint16_t point0Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset];
                const uint16_t point1Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 1u];
                const uint16_t point2Index = WOW_SELECTOR_SOURCE_TRIANGLE_POINT_INDICES[triangleOffset + 2u];
                if (TriangleSharesAabbOutcodeReject(
                        pointOutcodes[point0Index],
                        pointOutcodes[point1Index],
                        pointOutcodes[point2Index])) {
                    continue;
                }

                SelectorCandidateRecord record{};
                BuildTranslatedTriangleSelectorRecord(
                    pointGrid[currentColumnIndex + point0Index],
                    pointGrid[currentColumnIndex + point1Index],
                    pointGrid[currentColumnIndex + point2Index],
                    translation,
                    useApproximatePlaneBuildPath,
                    record);
                ioRecords.push_back(record);
            }

            ++currentColumnIndex;
        }

        currentPointIndex = currentColumnIndex + scanWindow.rowAdvancePointCount;
    }

    return static_cast<uint32_t>(ioRecords.size() - initialCount);
}

void WoWCollision::AppendSelectorQuadRecordPair(const G3D::Vector3& basePoint,
                                                const G3D::Vector3& firstEdge,
                                                const G3D::Vector3& secondEdge,
                                                const G3D::Vector3& normal,
                                                std::vector<SelectorCandidateRecord>& ioRecords)
{
    SelectorCandidateRecord firstRecord{};
    BuildPlaneFromNormalAndPoint(normal, basePoint, firstRecord.filterPlane);
    firstRecord.points[0] = basePoint;
    firstRecord.points[1] = basePoint + firstEdge;
    firstRecord.points[2] = firstRecord.points[1] + secondEdge;
    ioRecords.push_back(firstRecord);

    SelectorCandidateRecord secondRecord{};
    BuildPlaneFromNormalAndPoint(normal, basePoint, secondRecord.filterPlane);
    secondRecord.points[0] = basePoint;
    secondRecord.points[1] = basePoint + secondEdge;
    secondRecord.points[2] = secondRecord.points[1] + firstEdge;
    ioRecords.push_back(secondRecord);
}

uint32_t WoWCollision::BuildAabbBoundarySelectorCandidateRecords(const G3D::Vector3& boundaryMin,
                                                                 const G3D::Vector3& boundaryMax,
                                                                 const G3D::Vector3& queryBoundsMin,
                                                                 const G3D::Vector3& queryBoundsMax,
                                                                 std::vector<SelectorCandidateRecord>& outRecords)
{
    const size_t initialCount = outRecords.size();
    const float spanX = boundaryMax.x - boundaryMin.x;
    const float spanY = boundaryMax.y - boundaryMin.y;
    const G3D::Vector3 verticalEdge(0.0f, 0.0f, WOW_SELECTOR_RECORD_VERTICAL_EXTRUSION);

    // 0x6AB530 / 0x6ABA30 emit two triangles per crossed XY boundary face,
    // always extruding upward from boundaryMin.z by a fixed 32000 units.
    if (queryBoundsMin.y <= boundaryMin.y) {
        AppendSelectorQuadRecordPair(
            G3D::Vector3(boundaryMin.x, boundaryMin.y, boundaryMin.z),
            G3D::Vector3(spanX, 0.0f, 0.0f),
            verticalEdge,
            G3D::Vector3(0.0f, -1.0f, 0.0f),
            outRecords);
    }

    if (queryBoundsMax.y >= boundaryMax.y) {
        AppendSelectorQuadRecordPair(
            G3D::Vector3(boundaryMax.x, boundaryMax.y, boundaryMin.z),
            G3D::Vector3(-spanX, 0.0f, 0.0f),
            verticalEdge,
            G3D::Vector3(0.0f, 1.0f, 0.0f),
            outRecords);
    }

    if (queryBoundsMin.x <= boundaryMin.x) {
        AppendSelectorQuadRecordPair(
            G3D::Vector3(boundaryMin.x, boundaryMax.y, boundaryMin.z),
            G3D::Vector3(0.0f, -spanY, 0.0f),
            verticalEdge,
            G3D::Vector3(-1.0f, 0.0f, 0.0f),
            outRecords);
    }

    if (queryBoundsMax.x >= boundaryMax.x) {
        AppendSelectorQuadRecordPair(
            G3D::Vector3(boundaryMax.x, boundaryMin.y, boundaryMin.z),
            G3D::Vector3(0.0f, spanY, 0.0f),
            verticalEdge,
            G3D::Vector3(1.0f, 0.0f, 0.0f),
            outRecords);
    }

    return static_cast<uint32_t>(outRecords.size() - initialCount);
}

void WoWCollision::BuildTransformedTriangleSelectorRecord(const std::array<G3D::Vector3, 3>& transformBasisRows,
                                                          const G3D::Vector3& localNormal,
                                                          const G3D::Vector3& point0,
                                                          const G3D::Vector3& point1,
                                                          const G3D::Vector3& point2,
                                                          SelectorCandidateRecord& outRecord)
{
    std::array<G3D::Vector3, 3> normalizedBasisRows = transformBasisRows;
    for (G3D::Vector3& row : normalizedBasisRows) {
        const float rowMagnitude = row.magnitude();
        if (rowMagnitude >= PhysicsConstants::SPEED_EPSILON) {
            row /= rowMagnitude;
        }
    }

    outRecord.points[0] = point0;
    outRecord.points[1] = point1;
    outRecord.points[2] = point2;

    const G3D::Vector3 worldNormal =
        (normalizedBasisRows[0] * localNormal.x) +
        (normalizedBasisRows[1] * localNormal.y) +
        (normalizedBasisRows[2] * localNormal.z);
    BuildPlaneFromNormalAndPoint(worldNormal, point0, outRecord.filterPlane);
}

void WoWCollision::TransformWorldPointToTransportLocal(const G3D::Vector3& worldPoint,
                                                       const G3D::Vector3& transportPosition,
                                                       float transportOrientation,
                                                       G3D::Vector3& outLocalPoint)
{
    const float cosO = std::cos(transportOrientation);
    const float sinO = std::sin(transportOrientation);
    const G3D::Vector3 delta = worldPoint - transportPosition;
    outLocalPoint.x = (delta.x * cosO) + (delta.y * sinO);
    outLocalPoint.y = (-delta.x * sinO) + (delta.y * cosO);
    outLocalPoint.z = delta.z;
}

void WoWCollision::TransformWorldVectorToTransportLocal(const G3D::Vector3& worldVector,
                                                        float transportOrientation,
                                                        G3D::Vector3& outLocalVector)
{
    const float cosO = std::cos(transportOrientation);
    const float sinO = std::sin(transportOrientation);
    outLocalVector.x = (worldVector.x * cosO) + (worldVector.y * sinO);
    outLocalVector.y = (-worldVector.x * sinO) + (worldVector.y * cosO);
    outLocalVector.z = worldVector.z;
}

void WoWCollision::BuildTransportLocalPlane(const G3D::Vector3& worldNormal,
                                            const G3D::Vector3& worldPoint,
                                            const G3D::Vector3& transportPosition,
                                            float transportOrientation,
                                            SelectorSupportPlane& outPlane)
{
    G3D::Vector3 localNormal;
    G3D::Vector3 localPoint;
    TransformWorldVectorToTransportLocal(worldNormal, transportOrientation, localNormal);
    TransformWorldPointToTransportLocal(worldPoint, transportPosition, transportOrientation, localPoint);
    BuildPlaneFromNormalAndPoint(localNormal, localPoint, outPlane);
}

void WoWCollision::TransformSelectorCandidateRecordToTransportLocal(const SelectorCandidateRecord& worldRecord,
                                                                    const G3D::Vector3& transportPosition,
                                                                    float transportOrientation,
                                                                    SelectorCandidateRecord& outLocalRecord)
{
    TransformWorldPointToTransportLocal(
        worldRecord.points[0],
        transportPosition,
        transportOrientation,
        outLocalRecord.points[0]);
    TransformWorldPointToTransportLocal(
        worldRecord.points[1],
        transportPosition,
        transportOrientation,
        outLocalRecord.points[1]);
    TransformWorldPointToTransportLocal(
        worldRecord.points[2],
        transportPosition,
        transportOrientation,
        outLocalRecord.points[2]);
    BuildTransportLocalPlane(
        worldRecord.filterPlane.normal,
        worldRecord.points[0],
        transportPosition,
        transportOrientation,
        outLocalRecord.filterPlane);
}

void WoWCollision::TransformSelectorCandidateRecordBufferToTransportLocal(uint32_t transportGuidLow,
                                                                          uint32_t transportGuidHigh,
                                                                          const G3D::Vector3& transportPosition,
                                                                          float transportOrientation,
                                                                          SelectorCandidateRecord* ioRecords,
                                                                          uint32_t recordCount)
{
    if ((transportGuidLow | transportGuidHigh) == 0u || ioRecords == nullptr || recordCount == 0u) {
        return;
    }

    for (uint32_t recordIndex = 0; recordIndex < recordCount; ++recordIndex) {
        SelectorCandidateRecord localRecord{};
        TransformSelectorCandidateRecordToTransportLocal(
            ioRecords[recordIndex],
            transportPosition,
            transportOrientation,
            localRecord);
        ioRecords[recordIndex] = localRecord;
    }
}

void WoWCollision::InitializeSelectorSupportPlane(SelectorSupportPlane& outPlane)
{
    outPlane.normal = G3D::Vector3(0.0f, 0.0f, 1.0f);
    outPlane.planeDistance = 0.0f;
}

float WoWCollision::ClampSelectorReportedBestRatio(float bestRatio)
{
    return (bestRatio <= WOW_CORNER_PLANE_EPSILON) ? 0.0f : bestRatio;
}

bool WoWCollision::FinalizeSelectorTriangleSourceWrapper(bool hasOverridePosition,
                                                         bool terrainQuerySucceeded,
                                                         float inputBestRatio,
                                                         float& outReportedBestRatio)
{
    if (!hasOverridePosition && !terrainQuerySucceeded) {
        outReportedBestRatio = 0.0f;
        return false;
    }

    outReportedBestRatio = ClampSelectorReportedBestRatio(inputBestRatio);
    return true;
}

void WoWCollision::InitializeSelectorTriangleSourceWrapperSeeds(G3D::Vector3& outTestPoint,
                                                                G3D::Vector3& outCandidateDirection,
                                                                float& outBestRatio)
{
    outTestPoint = G3D::Vector3(0.0f, 0.0f, -1.0f);
    outCandidateDirection = G3D::Vector3(0.0f, 0.0f, -1.0f);
    outBestRatio = 1.0f;
}

bool WoWCollision::EvaluateSelectorTriangleSourceWrapperTransaction(const G3D::Vector3& defaultPosition,
                                                                    const G3D::Vector3* overridePosition,
                                                                    bool terrainQuerySucceeded,
                                                                    float inputBestRatio,
                                                                    SelectorTriangleSourceWrapperTrace& outTrace)
{
    outTrace = SelectorTriangleSourceWrapperTrace{};
    outTrace.supportPlaneInitCount = 7u;
    outTrace.validationPlaneInitCount = 9u;
    outTrace.scratchPointZeroCount = 9u;
    outTrace.usedOverridePosition = (overridePosition != nullptr) ? 1u : 0u;
    outTrace.terrainQueryInvoked = (overridePosition == nullptr) ? 1u : 0u;
    outTrace.terrainQuerySucceeded = terrainQuerySucceeded ? 1u : 0u;
    outTrace.selectedPosition = (overridePosition != nullptr) ? *overridePosition : defaultPosition;
    outTrace.inputBestRatio = inputBestRatio;

    InitializeSelectorTriangleSourceWrapperSeeds(
        outTrace.testPoint,
        outTrace.candidateDirection,
        outTrace.initialBestRatio);

    const bool accepted = FinalizeSelectorTriangleSourceWrapper(
        overridePosition != nullptr,
        terrainQuerySucceeded,
        inputBestRatio,
        outTrace.reportedBestRatio);
    outTrace.returnedSuccess = accepted ? 1u : 0u;
    outTrace.queryFailureZeroedOutput =
        (!accepted && overridePosition == nullptr && !terrainQuerySucceeded && outTrace.reportedBestRatio == 0.0f) ? 1u : 0u;
    return accepted;
}

bool WoWCollision::EvaluateSelectorTriangleSourceVariableTransaction(const G3D::Vector3& defaultPosition,
                                                                     const G3D::Vector3* overridePosition,
                                                                     const G3D::Vector3& projectedPosition,
                                                                     uint32_t supportPlaneInitCount,
                                                                     uint32_t validationPlaneInitCount,
                                                                     uint32_t scratchPointZeroCount,
                                                                     const G3D::Vector3& testPoint,
                                                                     const G3D::Vector3& candidateDirection,
                                                                     float initialBestRatio,
                                                                     float collisionRadius,
                                                                     float boundingHeight,
                                                                     const G3D::Vector3& cachedBoundsMin,
                                                                     const G3D::Vector3& cachedBoundsMax,
                                                                     bool modelPropertyFlagSet,
                                                                     uint32_t movementFlags,
                                                                     float field20Value,
                                                                     bool rootTreeFlagSet,
                                                                     bool childTreeFlagSet,
                                                                     bool queryDispatchSucceeded,
                                                                     bool rankingAccepted,
                                                                     uint32_t rankingCandidateCount,
                                                                     int32_t rankingSelectedRecordIndex,
                                                                     float rankingReportedBestRatio,
                                                                     SelectorTriangleSourceVariableTransactionTrace& outTrace)
{
    outTrace = SelectorTriangleSourceVariableTransactionTrace{};
    outTrace.supportPlaneInitCount = supportPlaneInitCount;
    outTrace.validationPlaneInitCount = validationPlaneInitCount;
    outTrace.scratchPointZeroCount = scratchPointZeroCount;
    outTrace.usedOverridePosition = (overridePosition != nullptr) ? 1u : 0u;
    outTrace.selectedPosition = (overridePosition != nullptr) ? *overridePosition : defaultPosition;
    outTrace.projectedPosition = projectedPosition;
    outTrace.testPoint = testPoint;
    outTrace.candidateDirection = candidateDirection;
    outTrace.initialBestRatio = initialBestRatio;
    outTrace.rankingReportedBestRatio = rankingReportedBestRatio;
    outTrace.rankingCandidateCount = rankingCandidateCount;
    outTrace.rankingSelectedRecordIndex = rankingSelectedRecordIndex;

    if (overridePosition == nullptr) {
        outTrace.terrainQueryInvoked = 1u;

        TerrainQueryMergedQueryTrace mergedQueryTrace{};
        const bool terrainQuerySucceeded = EvaluateTerrainQueryMergedQueryTransaction(
            projectedPosition,
            collisionRadius,
            boundingHeight,
            cachedBoundsMin,
            cachedBoundsMax,
            modelPropertyFlagSet,
            movementFlags,
            field20Value,
            rootTreeFlagSet,
            childTreeFlagSet,
            queryDispatchSucceeded,
            mergedQueryTrace);
        outTrace.terrainQuerySucceeded = terrainQuerySucceeded ? 1u : 0u;
        outTrace.terrainQueryReusedCachedQuery = mergedQueryTrace.reusedCachedQuery;
        outTrace.terrainQueryBuiltMergedBounds = mergedQueryTrace.builtMergedBounds;
        outTrace.terrainQueryBuiltQueryMask = mergedQueryTrace.builtQueryMask;
        outTrace.terrainQueryMask = mergedQueryTrace.queryMask;

        if (!terrainQuerySucceeded) {
            outTrace.outputReportedBestRatio = 0.0f;
            outTrace.queryFailureZeroedOutput = 1u;
            return false;
        }
    }

    outTrace.rankingInvoked = 1u;
    outTrace.rankingAccepted = rankingAccepted ? 1u : 0u;
    outTrace.outputReportedBestRatio = ClampSelectorReportedBestRatio(rankingReportedBestRatio);
    outTrace.zeroClampedOutput =
        (outTrace.outputReportedBestRatio == 0.0f && rankingReportedBestRatio <= WOW_CORNER_PLANE_EPSILON) ? 1u : 0u;
    outTrace.returnedSuccess = 1u;
    return true;
}

void WoWCollision::BuildSelectorSupportPlanes(const G3D::Vector3& position,
                                              float verticalOffset,
                                              float horizontalRadius,
                                              std::array<SelectorSupportPlane, 9>& outPlanes)
{
    outPlanes[0] = { G3D::Vector3(-1.0f, 0.0f, 0.0f), position.x - horizontalRadius };
    outPlanes[1] = { G3D::Vector3(1.0f, 0.0f, 0.0f), -position.x - horizontalRadius };
    outPlanes[2] = { G3D::Vector3(0.0f, 1.0f, 0.0f), -position.y - horizontalRadius };
    outPlanes[3] = { G3D::Vector3(0.0f, -1.0f, 0.0f), position.y - horizontalRadius };
    outPlanes[4] = { G3D::Vector3(0.0f, 0.0f, 1.0f), -position.z - verticalOffset };

    const float diagonalX = WOW_SELECTOR_SUPPORT_DIAGONAL_X;
    const float diagonalZ = WOW_SELECTOR_SUPPORT_DIAGONAL_Z;
    outPlanes[5] = {
        G3D::Vector3(-diagonalX, 0.0f, -diagonalZ),
        (position.x * diagonalX) + (position.z * diagonalZ)
    };
    outPlanes[6] = {
        G3D::Vector3(diagonalX, 0.0f, -diagonalZ),
        (position.z * diagonalZ) - (position.x * diagonalX)
    };
    outPlanes[7] = {
        G3D::Vector3(0.0f, diagonalX, -diagonalZ),
        (position.z * diagonalZ) - (position.y * diagonalX)
    };
    outPlanes[8] = {
        G3D::Vector3(0.0f, -diagonalX, -diagonalZ),
        (position.y * diagonalX) + (position.z * diagonalZ)
    };
}

void WoWCollision::BuildSelectorNeighborhood(const G3D::Vector3& position,
                                             float verticalOffset,
                                             float horizontalRadius,
                                             std::array<G3D::Vector3, 9>& outPoints,
                                             std::array<uint8_t, 32>& outSelectorIndices)
{
    outPoints[0] = position;
    outPoints[1] = G3D::Vector3(position.x - horizontalRadius, position.y - horizontalRadius, position.z - horizontalRadius);
    outPoints[2] = G3D::Vector3(position.x - horizontalRadius, position.y + horizontalRadius, position.z - horizontalRadius);
    outPoints[3] = G3D::Vector3(position.x + horizontalRadius, position.y + horizontalRadius, position.z - horizontalRadius);
    outPoints[4] = G3D::Vector3(position.x + horizontalRadius, position.y - horizontalRadius, position.z - horizontalRadius);
    outPoints[5] = G3D::Vector3(position.x - horizontalRadius, position.y - horizontalRadius, position.z + verticalOffset);
    outPoints[6] = G3D::Vector3(position.x - horizontalRadius, position.y + horizontalRadius, position.z + verticalOffset);
    outPoints[7] = G3D::Vector3(position.x + horizontalRadius, position.y + horizontalRadius, position.z + verticalOffset);
    outPoints[8] = G3D::Vector3(position.x + horizontalRadius, position.y - horizontalRadius, position.z + verticalOffset);

    outSelectorIndices = {
        1u, 2u, 6u, 5u,
        3u, 4u, 8u, 7u,
        2u, 3u, 7u, 6u,
        4u, 1u, 5u, 8u,
        5u, 6u, 7u, 8u,
        0u, 1u, 2u, 0u,
        3u, 4u, 0u, 2u,
        3u, 0u, 4u, 1u
    };
}

float WoWCollision::EvaluateSelectorPlaneRatio(const G3D::Vector3& candidatePoint,
                                               const SelectorSupportPlane& plane,
                                               const G3D::Vector3& testPoint)
{
    const float numerator = EvaluatePlane(plane.normal, plane.planeDistance, candidatePoint);
    const float denominator = plane.normal.dot(testPoint);
    if (std::fabs(denominator) <= WOW_SELECTOR_RATIO_EPSILON) {
        return 0.0f;
    }

    return numerator / denominator;
}

void WoWCollision::ClipSelectorPointStripAgainstPlane(const SelectorSupportPlane& plane,
                                                      uint32_t clipPlaneIndex,
                                                      SelectorPointStrip& ioStrip)
{
    if (ioStrip.count == 0 || ioStrip.count > ioStrip.points.size()) {
        return;
    }

    std::array<float, 15> signedDistances{};
    float minSignedDistance = std::numeric_limits<float>::max();
    float maxSignedDistance = -std::numeric_limits<float>::max();

    for (uint32_t i = 0; i < ioStrip.count; ++i) {
        const float signedDistance = -EvaluatePlane(plane.normal, plane.planeDistance, ioStrip.points[i]);
        signedDistances[i] = signedDistance;
        minSignedDistance = std::min(minSignedDistance, signedDistance);
        maxSignedDistance = std::max(maxSignedDistance, signedDistance);
    }

    if (minSignedDistance > WOW_SELECTOR_CLIP_NEGATIVE_EPSILON) {
        return;
    }

    if (maxSignedDistance < WOW_CORNER_PLANE_EPSILON) {
        ioStrip.count = 0;
        return;
    }

    const SelectorPointStrip originalStrip = ioStrip;
    const uint32_t originalCount = originalStrip.count;
    ioStrip.count = 0;

    auto appendPoint = [&](const G3D::Vector3& point, uint32_t sourceIndex) {
        if (ioStrip.count >= ioStrip.points.size()) {
            return;
        }

        ioStrip.points[ioStrip.count] = point;
        ioStrip.sourceIndices[ioStrip.count] = sourceIndex;
        ++ioStrip.count;
    };

    auto appendIntersection = [&](uint32_t previousIndex, uint32_t currentIndex) {
        const float previousSignedDistance = signedDistances[previousIndex];
        const float currentSignedDistance = signedDistances[currentIndex];
        const G3D::Vector3 delta = originalStrip.points[currentIndex] - originalStrip.points[previousIndex];
        const float interpolation = previousSignedDistance / (currentSignedDistance - previousSignedDistance);
        const G3D::Vector3 intersection = originalStrip.points[previousIndex] - (delta * interpolation);
        appendPoint(intersection, clipPlaneIndex);
    };

    uint32_t previousIndex = originalCount - 1;
    for (uint32_t currentIndex = 0; currentIndex < originalCount; ++currentIndex) {
        const float previousSignedDistance = signedDistances[previousIndex];
        const float currentSignedDistance = signedDistances[currentIndex];
        const bool previousInside = previousSignedDistance >= 0.0f;
        const bool currentInside = currentSignedDistance >= 0.0f;

        if (!previousInside) {
            if (!currentInside) {
                previousIndex = currentIndex;
                continue;
            }

            if (currentSignedDistance > WOW_CORNER_PLANE_EPSILON) {
                appendIntersection(previousIndex, currentIndex);
            }

            appendPoint(originalStrip.points[currentIndex], originalStrip.sourceIndices[currentIndex]);
            previousIndex = currentIndex;
            continue;
        }

        if (currentInside) {
            appendPoint(originalStrip.points[currentIndex], originalStrip.sourceIndices[currentIndex]);
            previousIndex = currentIndex;
            continue;
        }

        if (previousSignedDistance > WOW_CORNER_PLANE_EPSILON) {
            appendIntersection(previousIndex, currentIndex);
        }

        previousIndex = currentIndex;
    }

    if (ioStrip.count < 3) {
        ioStrip.count = 0;
    }
}

bool WoWCollision::ClipSelectorPointStripAgainstPlanePrefix(const SelectorSupportPlane* planes,
                                                            uint32_t planeCount,
                                                            SelectorPointStrip& ioStrip)
{
    if (planes == nullptr && planeCount != 0u) {
        return false;
    }

    for (uint32_t planeIndex = 0; planeIndex < planeCount; ++planeIndex) {
        ClipSelectorPointStripAgainstPlane(planes[planeIndex], planeIndex, ioStrip);
        if (ioStrip.count == 0) {
            return false;
        }
    }

    return true;
}

bool WoWCollision::ClipSelectorPointStripExcludingPlane(const std::array<SelectorSupportPlane, 9>& planes,
                                                        uint32_t excludedPlaneIndex,
                                                        SelectorPointStrip& ioStrip)
{
    for (uint32_t planeIndex = 0; planeIndex < planes.size(); ++planeIndex) {
        if (planeIndex == excludedPlaneIndex) {
            continue;
        }

        ClipSelectorPointStripAgainstPlane(planes[planeIndex], planeIndex, ioStrip);
        if (ioStrip.count == 0) {
            return false;
        }
    }

    return true;
}

bool WoWCollision::ValidateSelectorPointStripCandidate(const SelectorPointStrip& strip,
                                                       const G3D::Vector3& testPoint,
                                                       const std::array<SelectorSupportPlane, 9>& planes,
                                                       uint32_t planeIndex,
                                                       float& inOutBestRatio,
                                                       SelectorCandidateValidationTrace* outTrace)
{
    SelectorCandidateValidationTrace trace{};
    trace.inputBestRatio = inOutBestRatio;
    trace.outputBestRatio = inOutBestRatio;

    if (planeIndex >= planes.size() || strip.count > strip.points.size()) {
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    const SelectorSupportPlane& selectedPlane = planes[planeIndex];
    float candidateBestRatio = inOutBestRatio;
    bool firstPassAllBelowLooseThreshold = true;

    for (uint32_t i = 0; i < strip.count; ++i) {
        const float ratio = EvaluateSelectorPlaneRatio(strip.points[i], selectedPlane, testPoint);
        if (ratio < candidateBestRatio) {
            candidateBestRatio = (ratio > 0.0f) ? ratio : 0.0f;
        }

        if (ratio > WOW_SELECTOR_LOOSE_RATIO_THRESHOLD) {
            firstPassAllBelowLooseThreshold = false;
        }
    }

    SelectorPointStrip rebuiltStrip = strip;
    bool rebuildSucceeded = false;
    bool secondPassAllBelowStrictThreshold = false;

    if (firstPassAllBelowLooseThreshold) {
        trace.rebuildExecuted = 1;
        rebuildSucceeded = ClipSelectorPointStripExcludingPlane(planes, planeIndex, rebuiltStrip);
        trace.rebuildSucceeded = rebuildSucceeded ? 1u : 0u;
        trace.finalStripCount = rebuiltStrip.count;
        if (!rebuildSucceeded) {
            trace.candidateBestRatio = candidateBestRatio;
            if (outTrace) {
                *outTrace = trace;
            }
            return false;
        }

        secondPassAllBelowStrictThreshold = true;
        for (uint32_t i = 0; i < rebuiltStrip.count; ++i) {
            const float ratio = EvaluateSelectorPlaneRatio(rebuiltStrip.points[i], selectedPlane, testPoint);
            if (ratio > WOW_SELECTOR_STRICT_RATIO_THRESHOLD) {
                secondPassAllBelowStrictThreshold = false;
            }
        }

        if (secondPassAllBelowStrictThreshold) {
            trace.candidateBestRatio = candidateBestRatio;
            trace.firstPassAllBelowLooseThreshold = 1;
            trace.secondPassAllBelowStrictThreshold = 1;
            if (outTrace) {
                *outTrace = trace;
            }
            return false;
        }
    }
    else {
        trace.finalStripCount = strip.count;
    }

    const bool improvedBestRatio = candidateBestRatio < inOutBestRatio;
    if (improvedBestRatio) {
        inOutBestRatio = candidateBestRatio;
    }

    trace.candidateBestRatio = candidateBestRatio;
    trace.outputBestRatio = inOutBestRatio;
    trace.firstPassAllBelowLooseThreshold = firstPassAllBelowLooseThreshold ? 1u : 0u;
    trace.secondPassAllBelowStrictThreshold = secondPassAllBelowStrictThreshold ? 1u : 0u;
    trace.improvedBestRatio = improvedBestRatio ? 1u : 0u;

    if (outTrace) {
        *outTrace = trace;
    }

    return improvedBestRatio;
}

bool WoWCollision::BuildSelectorCandidatePlaneRecord(const std::array<G3D::Vector3, 9>& points,
                                                     const std::array<uint8_t, 3>& selectorIndices,
                                                     const G3D::Vector3& translation,
                                                     const SelectorSupportPlane& sourcePlane,
                                                     std::array<SelectorSupportPlane, 4>& outPlanes)
{
    auto buildPlaneFromPoints = [](const G3D::Vector3& pointA,
                                   const G3D::Vector3& pointB,
                                   const G3D::Vector3& pointC,
                                   SelectorSupportPlane& outPlane) -> bool
    {
        G3D::Vector3 normal = (pointC - pointA).cross(pointB - pointA);
        const float normalMagnitudeSq = normal.squaredMagnitude();
        if (normalMagnitudeSq <= WOW_PLANE_BUILD_EPSILON) {
            return false;
        }

        normal = normal * (1.0f / std::sqrt(normalMagnitudeSq));
        outPlane.normal = normal;
        outPlane.planeDistance = -normal.dot(pointA);
        return true;
    };

    constexpr std::array<uint32_t, 3> secondPointOffsets = { 1u, 2u, 0u };
    constexpr std::array<uint32_t, 3> oppositePointOffsets = { 2u, 0u, 1u };

    for (uint32_t planeIndex = 0; planeIndex < 3; ++planeIndex) {
        const uint32_t currentSelector = selectorIndices[planeIndex];
        const uint32_t secondSelector = selectorIndices[secondPointOffsets[planeIndex]];
        const uint32_t oppositeSelector = selectorIndices[oppositePointOffsets[planeIndex]];

        const G3D::Vector3& pointA = points[currentSelector];
        const G3D::Vector3& pointB = points[secondSelector];
        const G3D::Vector3& pointC = points[oppositeSelector];
        const G3D::Vector3 translatedPoint = pointA + translation;

        if (!buildPlaneFromPoints(pointA, pointB, translatedPoint, outPlanes[planeIndex])) {
            return false;
        }

        const float oppositeDot = outPlanes[planeIndex].normal.dot(pointC - pointA);
        if (oppositeDot > 0.0f) {
            outPlanes[planeIndex].normal = -outPlanes[planeIndex].normal;
            outPlanes[planeIndex].planeDistance = -outPlanes[planeIndex].planeDistance;
        }
    }

    outPlanes[3].normal = sourcePlane.normal;
    outPlanes[3].planeDistance = -sourcePlane.normal.dot(points[selectorIndices[0]] + translation);
    return true;
}

bool WoWCollision::BuildSelectorCandidateQuadPlaneRecord(const std::array<G3D::Vector3, 9>& points,
                                                         const std::array<uint8_t, 4>& selectorIndices,
                                                         const G3D::Vector3& translation,
                                                         const SelectorSupportPlane& sourcePlane,
                                                         std::array<SelectorSupportPlane, 5>& outPlanes)
{
    auto buildPlaneFromPoints = [](const G3D::Vector3& pointA,
                                   const G3D::Vector3& pointB,
                                   const G3D::Vector3& pointC,
                                   SelectorSupportPlane& outPlane) -> bool
    {
        G3D::Vector3 normal = (pointC - pointA).cross(pointB - pointA);
        const float normalMagnitudeSq = normal.squaredMagnitude();
        if (normalMagnitudeSq <= WOW_PLANE_BUILD_EPSILON) {
            return false;
        }

        normal = normal * (1.0f / std::sqrt(normalMagnitudeSq));
        outPlane.normal = normal;
        outPlane.planeDistance = -normal.dot(pointA);
        return true;
    };

    for (uint32_t planeIndex = 0; planeIndex < 4; ++planeIndex) {
        const uint32_t currentSelector = selectorIndices[planeIndex];
        const uint32_t nextSelector = selectorIndices[(planeIndex + 1u) & 3u];
        const uint32_t previousSelector = selectorIndices[(planeIndex + 3u) & 3u];

        const G3D::Vector3& pointA = points[currentSelector];
        const G3D::Vector3& pointB = points[nextSelector];
        const G3D::Vector3& pointC = points[previousSelector];
        const G3D::Vector3 translatedPoint = pointA + translation;

        if (!buildPlaneFromPoints(pointA, pointB, translatedPoint, outPlanes[planeIndex])) {
            return false;
        }

        const float previousDot = outPlanes[planeIndex].normal.dot(pointC - pointA);
        if (previousDot > 0.0f) {
            outPlanes[planeIndex].normal = -outPlanes[planeIndex].normal;
            outPlanes[planeIndex].planeDistance = -outPlanes[planeIndex].planeDistance;
        }
    }

    outPlanes[4].normal = sourcePlane.normal;
    outPlanes[4].planeDistance = -sourcePlane.normal.dot(points[selectorIndices[0]] + translation);
    return true;
}

bool WoWCollision::HasSelectorCandidateWithUnitZ(const SelectorSupportPlane* candidates, uint32_t candidateCount)
{
    if (candidates == nullptr && candidateCount != 0u) {
        return false;
    }

    for (uint32_t candidateIndex = 0; candidateIndex < candidateCount; ++candidateIndex) {
        if (std::fabs(candidates[candidateIndex].normal.z - 1.0f) <= WOW_PLANE_BUILD_EPSILON) {
            return true;
        }
    }

    return false;
}

bool WoWCollision::HasSelectorCandidateWithNegativeDiagonalZ(const SelectorSupportPlane* candidates,
                                                             uint32_t candidateCount)
{
    if (candidates == nullptr && candidateCount != 0u) {
        return false;
    }

    for (uint32_t candidateIndex = 0; candidateIndex < candidateCount; ++candidateIndex) {
        if (std::fabs(candidates[candidateIndex].normal.z - WOW_SELECTOR_SUPPORT_NEGATIVE_DIAGONAL_Z) <= WOW_PLANE_BUILD_EPSILON) {
            return true;
        }
    }

    return false;
}

bool WoWCollision::IsSelectorContactWithinAlternateWorkingVectorBand(float normalZ)
{
    if (!(normalZ <= WOW_WALKABLE_MIN_NORMAL_Z)) {
        return false;
    }

    if (!(normalZ < 0.0f)) {
        return true;
    }

    return (-normalZ) <= WOW_WALKABLE_MIN_NORMAL_Z;
}

WoWCollision::SelectorAlternateWorkingVectorMode WoWCollision::EvaluateSelectorAlternateWorkingVectorMode(
    float selectedNormalZ,
    uint32_t candidateCount)
{
    if (!IsSelectorContactWithinAlternateWorkingVectorBand(selectedNormalZ)) {
        return SELECTOR_ALTERNATE_VECTOR_NEGATED_FIRST_CANDIDATE;
    }

    if (candidateCount == 2u) {
        return SELECTOR_ALTERNATE_VECTOR_TWO_CANDIDATE_BUILDER;
    }

    if (candidateCount <= 1u || candidateCount > 4u) {
        return SELECTOR_ALTERNATE_VECTOR_NEGATED_FIRST_CANDIDATE;
    }

    return SELECTOR_ALTERNATE_VECTOR_SELECTED_CONTACT_NORMAL;
}

bool WoWCollision::EvaluateSelectorPlaneFootprintMismatch(const G3D::Vector3& position,
                                                          float collisionRadius,
                                                          const SelectorSupportPlane& selectedPlane)
{
    const float sampleHeight = collisionRadius * WOW_SELECTOR_FOOTPRINT_SAMPLE_HEIGHT_FACTOR;
    const std::array<G3D::Vector3, 5> samplePoints{
        G3D::Vector3(position.x + collisionRadius, position.y + collisionRadius, position.z + sampleHeight),
        G3D::Vector3(position.x - collisionRadius, position.y + collisionRadius, position.z + sampleHeight),
        G3D::Vector3(position.x - collisionRadius, position.y + collisionRadius, position.z + sampleHeight),
        G3D::Vector3(position.x + collisionRadius, position.y - collisionRadius, position.z + sampleHeight),
        G3D::Vector3(position.x - collisionRadius, position.y - collisionRadius, position.z + sampleHeight),
    };

    for (const G3D::Vector3& samplePoint : samplePoints) {
        const float signedDistance = selectedPlane.normal.dot(samplePoint) + selectedPlane.planeDistance;
        if (std::fabs(signedDistance) > PhysicsConstants::COLLISION_SKIN_EPSILON) {
            return true;
        }
    }

    return false;
}

void WoWCollision::BuildSelectorPlaneIntersectionPoint(const SelectorSupportPlane& selectedPlane,
                                                       const SelectorSupportPlane& firstCandidatePlane,
                                                       const SelectorSupportPlane& secondCandidatePlane,
                                                       G3D::Vector3& outPoint)
{
    const G3D::Vector3 selectedCross = firstCandidatePlane.normal.cross(secondCandidatePlane.normal);
    const G3D::Vector3 firstCross = secondCandidatePlane.normal.cross(selectedPlane.normal);
    const G3D::Vector3 secondCross = selectedPlane.normal.cross(firstCandidatePlane.normal);
    const float determinant = selectedPlane.normal.dot(selectedCross);

    const G3D::Vector3 numerator =
        (selectedCross * (-selectedPlane.planeDistance)) +
        (firstCross * (-firstCandidatePlane.planeDistance)) +
        (secondCross * (-secondCandidatePlane.planeDistance));

    outPoint = numerator * (1.0f / determinant);
}

void WoWCollision::BuildSelectorTriangleEdgeDirection(const SelectorCandidateRecord& selectedRecord,
                                                      const G3D::Vector3& intersectionPoint,
                                                      const G3D::Vector3& lineDirection,
                                                      G3D::Vector3& outDirection,
                                                      SelectorTriangleEdgeDirectionTrace* outTrace)
{
    SelectorTriangleEdgeDirectionTrace trace{};
    trace.bestScore = std::numeric_limits<float>::max();
    outDirection = G3D::Vector3(0.0f, 0.0f, 0.0f);

    constexpr std::array<uint32_t, 3> nextPointOffsets = { 1u, 2u, 0u };

    for (uint32_t edgeIndex = 0; edgeIndex < 3u; ++edgeIndex) {
        const G3D::Vector3& currentPoint = selectedRecord.points[edgeIndex];
        const G3D::Vector3& nextPoint = selectedRecord.points[nextPointOffsets[edgeIndex]];

        G3D::Vector3 edgeDirection = currentPoint - nextPoint;
        const float edgeMagnitude = edgeDirection.magnitude();
        if (std::fabs(edgeMagnitude) <= WOW_SELECTOR_RATIO_EPSILON) {
            trace.zeroLengthRejectedCount++;
            continue;
        }

        edgeDirection = edgeDirection * (1.0f / edgeMagnitude);

        float score = 0.0f;
        if (std::fabs(edgeDirection.dot(lineDirection) - 1.0f) <= WOW_PLANE_BUILD_EPSILON) {
            trace.pointToLineScoredCount++;

            const G3D::Vector3 pointOffset = currentPoint - intersectionPoint;
            const float projectedDistance = pointOffset.dot(lineDirection);
            const G3D::Vector3 projectedPoint = intersectionPoint + (lineDirection * projectedDistance);
            const G3D::Vector3 rejection = currentPoint - projectedPoint;
            score = rejection.squaredMagnitude();
        }
        else {
            trace.planeScoredCount++;

            const G3D::Vector3 planeNormal = edgeDirection.cross(lineDirection);
            const float signedPlaneOffset = planeNormal.dot(currentPoint) - planeNormal.dot(intersectionPoint);
            score = signedPlaneOffset * signedPlaneOffset;
        }

        if (score < trace.bestScore) {
            trace.bestScore = score;
            trace.selectedEdgeIndex = edgeIndex;
            outDirection = edgeDirection;
        }
    }

    if (outTrace) {
        *outTrace = trace;
    }
}

void WoWCollision::BuildSelectorTwoCandidateWorkingVector(const G3D::Vector3& position,
                                                          float collisionRadius,
                                                          const SelectorCandidateRecord& selectedRecord,
                                                          const SelectorSupportPlane& firstCandidatePlane,
                                                          const SelectorSupportPlane& secondCandidatePlane,
                                                          G3D::Vector3& outVector,
                                                          SelectorTwoCandidateWorkingVectorTrace* outTrace)
{
    SelectorTwoCandidateWorkingVectorTrace trace{};

    G3D::Vector3 lineDirection = firstCandidatePlane.normal.cross(secondCandidatePlane.normal);
    const float lineMagnitude = lineDirection.magnitude();
    lineDirection = lineDirection * (1.0f / lineMagnitude);
    trace.lineDirection = lineDirection;

    if (std::fabs(lineDirection.z) <= WOW_PLANE_BUILD_EPSILON) {
        trace.returnedSelectedNormal = 1u;
        trace.rejectedByLineZGate = 1u;
        outVector = selectedRecord.filterPlane.normal;
        if (outTrace) {
            *outTrace = trace;
        }
        return;
    }

    if (std::fabs(lineDirection.dot(selectedRecord.filterPlane.normal)) <= WOW_PLANE_BUILD_EPSILON) {
        trace.returnedSelectedNormal = 1u;
        trace.rejectedBySelectedPlaneDotGate = 1u;
        outVector = selectedRecord.filterPlane.normal;
        if (outTrace) {
            *outTrace = trace;
        }
        return;
    }

    if (std::fabs(lineDirection.z - 1.0f) > WOW_PLANE_BUILD_EPSILON &&
        EvaluateSelectorPlaneFootprintMismatch(position, collisionRadius, selectedRecord.filterPlane)) {
        trace.returnedSelectedNormal = 1u;
        trace.rejectedByFootprintMismatch = 1u;
        outVector = selectedRecord.filterPlane.normal;
        if (outTrace) {
            *outTrace = trace;
        }
        return;
    }

    G3D::Vector3 intersectionPoint(0.0f, 0.0f, 0.0f);
    BuildSelectorPlaneIntersectionPoint(
        selectedRecord.filterPlane,
        firstCandidatePlane,
        secondCandidatePlane,
        intersectionPoint);

    SelectorTriangleEdgeDirectionTrace edgeTrace{};
    G3D::Vector3 edgeDirection(0.0f, 0.0f, 0.0f);
    BuildSelectorTriangleEdgeDirection(
        selectedRecord,
        intersectionPoint,
        lineDirection,
        edgeDirection,
        &edgeTrace);

    trace.selectedEdgeIndex = edgeTrace.selectedEdgeIndex;
    trace.edgeDirection = edgeDirection;

    G3D::Vector3 workingVector = edgeDirection.cross(lineDirection);
    const float workingMagnitude = workingVector.magnitude();
    if (std::fabs(workingMagnitude) <= WOW_SELECTOR_RATIO_EPSILON) {
        trace.returnedNegatedFirstCandidate = 1u;
        outVector = -firstCandidatePlane.normal;
        if (outTrace) {
            *outTrace = trace;
        }
        return;
    }

    workingVector = workingVector * (1.0f / workingMagnitude);
    if (workingVector.dot(firstCandidatePlane.normal) > 0.0f) {
        workingVector = -workingVector;
        trace.orientationNegated = 1u;
    }

    trace.returnedConstructedVector = 1u;
    trace.workingVector = workingVector;
    outVector = workingVector;
    if (outTrace) {
        *outTrace = trace;
    }
}

void WoWCollision::BuildSelectorAlternatePair(const G3D::Vector3& position,
                                              float collisionRadius,
                                              const SelectorCandidateRecord& selectedRecord,
                                              const SelectorSupportPlane* candidatePlanes,
                                              uint32_t candidateCount,
                                              const G3D::Vector3& inputMove,
                                              float windowStartScalar,
                                              float windowEndScalar,
                                              SelectorPair& outPair,
                                              SelectorAlternatePairTrace* outTrace)
{
    SelectorAlternatePairTrace trace{};
    G3D::Vector3 workingVector(0.0f, 0.0f, 0.0f);

    if (!IsSelectorContactWithinAlternateWorkingVectorBand(selectedRecord.filterPlane.normal.z)) {
        trace.usedNegatedInputWorkingVector = 1u;
        workingVector = -inputMove;
    }
    else {
        switch (EvaluateSelectorAlternateWorkingVectorMode(selectedRecord.filterPlane.normal.z, candidateCount)) {
        case SELECTOR_ALTERNATE_VECTOR_NEGATED_FIRST_CANDIDATE:
            trace.usedNegatedFirstCandidate = 1u;
            workingVector = -candidatePlanes[0].normal;
            break;

        case SELECTOR_ALTERNATE_VECTOR_TWO_CANDIDATE_BUILDER:
            trace.usedTwoCandidateBuilder = 1u;
            BuildSelectorTwoCandidateWorkingVector(
                position,
                collisionRadius,
                selectedRecord,
                candidatePlanes[0],
                candidatePlanes[1],
                workingVector);
            break;

        case SELECTOR_ALTERNATE_VECTOR_SELECTED_CONTACT_NORMAL:
        default:
            trace.usedSelectedContactNormal = 1u;
            workingVector = selectedRecord.filterPlane.normal;
            break;
        }
    }

    trace.workingVector = workingVector;

    float horizontalX = workingVector.x;
    float horizontalY = workingVector.y;
    const float horizontalMagnitude = std::sqrt((horizontalX * horizontalX) + (horizontalY * horizontalY));
    trace.horizontalMagnitude = horizontalMagnitude;

    if (std::fabs(horizontalMagnitude) > WOW_SELECTOR_RATIO_EPSILON) {
        const float invHorizontalMagnitude = 1.0f / horizontalMagnitude;
        horizontalX *= invHorizontalMagnitude;
        horizontalY *= invHorizontalMagnitude;
        trace.normalizedHorizontal = 1u;
    }

    const float numerator = (windowEndScalar - windowStartScalar) * (-workingVector.dot(inputMove));
    const float denominator = (workingVector.x * horizontalX) + (workingVector.y * horizontalY);
    trace.denominator = denominator;

    float scale = numerator;
    if (std::fabs(denominator) > WOW_SELECTOR_RATIO_EPSILON) {
        scale = numerator / denominator;
    }

    trace.scale = scale;
    outPair.first = horizontalX * scale;
    outPair.second = horizontalY * scale;

    if (outTrace) {
        *outTrace = trace;
    }
}

bool WoWCollision::EvaluateSelectorAlternateUnitZFallbackGate(float airborneTimeScalar,
                                                              float elapsedTimeScalar,
                                                              float horizontalSpeedScale,
                                                              float requestedDistance)
{
    const float remainingWindow = airborneTimeScalar - elapsedTimeScalar;
    if (remainingWindow < 0.0f) {
        return false;
    }

    return (remainingWindow * horizontalSpeedScale) >= requestedDistance;
}

float WoWCollision::ComputeJumpTimeScalar(uint32_t movementFlags,
                                          float verticalSpeed)
{
    if ((movementFlags & MOVEFLAG_JUMPING) == 0u) {
        return 0.0f;
    }

    return verticalSpeed * PhysicsConstants::NEG_INV_GRAVITY;
}

bool WoWCollision::EvaluateSelectorPairFollowupGate(float windowStartScalar,
                                                    float windowSpanScalar,
                                                    const G3D::Vector3& moveVector,
                                                    bool alternateUnitZState,
                                                    uint32_t movementFlags,
                                                    float verticalSpeed,
                                                    float horizontalSpeedScale)
{
    if (alternateUnitZState) {
        return true;
    }

    if (!(verticalSpeed < 0.0f)) {
        return false;
    }

    const float jumpTimeScalar = ComputeJumpTimeScalar(movementFlags, verticalSpeed);
    if ((windowStartScalar + windowSpanScalar) < jumpTimeScalar) {
        return true;
    }

    if (windowStartScalar > jumpTimeScalar) {
        return false;
    }

    const float remainingHorizontalAllowance = (jumpTimeScalar - windowStartScalar) * horizontalSpeedScale;
    const float remainingHorizontalAllowanceSq = remainingHorizontalAllowance * remainingHorizontalAllowance;
    const float horizontalMoveSq = (moveVector.x * moveVector.x) + (moveVector.y * moveVector.y);
    return remainingHorizontalAllowanceSq > horizontalMoveSq;
}

float WoWCollision::ComputeVerticalTravelTimeScalar(float verticalDistance,
                                                    bool preferEarlierPositiveRoot,
                                                    uint32_t movementFlags,
                                                    float verticalSpeed)
{
    const float terminalVelocity = (movementFlags & MOVEFLAG_SAFE_FALL) != 0u
        ? PhysicsConstants::SAFE_FALL_TERMINAL_VELOCITY
        : PhysicsConstants::TERMINAL_VELOCITY;

    float clampedVerticalSpeed = verticalSpeed;
    if (clampedVerticalSpeed > terminalVelocity) {
        clampedVerticalSpeed = terminalVelocity;
    }

    if (std::fabs(clampedVerticalSpeed) <= WOW_SELECTOR_RATIO_EPSILON) {
        const float timeToTerminal = terminalVelocity * PhysicsConstants::INV_GRAVITY;
        const float distanceToTerminal = 0.5f * terminalVelocity * timeToTerminal;
        if (verticalDistance <= distanceToTerminal) {
            if (verticalDistance > 0.0f) {
                return std::sqrt(verticalDistance * (2.0f * PhysicsConstants::INV_GRAVITY));
            }

            return 0.0f;
        }

        return ((verticalDistance - distanceToTerminal) / terminalVelocity) + timeToTerminal;
    }

    const float discriminant = (clampedVerticalSpeed * clampedVerticalSpeed) +
                               (verticalDistance * PhysicsConstants::DOUBLE_GRAVITY);
    const float root = discriminant > 0.0f ? std::sqrt(discriminant) : 0.0f;
    const float earlierIntersection = (-clampedVerticalSpeed - root) * PhysicsConstants::INV_GRAVITY;
    float laterIntersection = (root - clampedVerticalSpeed) * PhysicsConstants::INV_GRAVITY;
    const float timeToTerminal = (terminalVelocity - clampedVerticalSpeed) * PhysicsConstants::INV_GRAVITY;

    if (laterIntersection > timeToTerminal) {
        const float distanceToTerminal = ((timeToTerminal * PhysicsConstants::HALF_GRAVITY) + clampedVerticalSpeed) *
                                         timeToTerminal;
        laterIntersection = timeToTerminal + ((verticalDistance - distanceToTerminal) / terminalVelocity);
    }

    if (preferEarlierPositiveRoot) {
        return earlierIntersection > 0.0f ? earlierIntersection : 0.0f;
    }

    return laterIntersection;
}

float WoWCollision::EvaluateSelectorPairWindowAdjustment(float windowSpanScalar,
                                                         float windowStartScalar,
                                                         G3D::Vector3& moveVector,
                                                         float* outMoveMagnitude,
                                                         bool alternateUnitZState,
                                                         float horizontalReferenceMagnitude,
                                                         uint32_t movementFlags,
                                                         float verticalSpeed,
                                                         float horizontalSpeedScale,
                                                         float referenceZ,
                                                         float positionZ)
{
    const bool followupGateAccepted = EvaluateSelectorPairFollowupGate(
        windowStartScalar,
        windowSpanScalar,
        moveVector,
        alternateUnitZState,
        movementFlags,
        verticalSpeed,
        horizontalSpeedScale);

    float scaledHorizontalWindow = 0.0f;
    if (horizontalReferenceMagnitude > WOW_SELECTOR_RATIO_EPSILON) {
        const float horizontalMoveLength = std::sqrt((moveVector.x * moveVector.x) + (moveVector.y * moveVector.y));
        scaledHorizontalWindow = (horizontalMoveLength / horizontalReferenceMagnitude) * windowSpanScalar;
    }

    const float verticalDistance = (referenceZ - positionZ) - moveVector.z;
    const float verticalTravelTime = ComputeVerticalTravelTimeScalar(
        verticalDistance,
        followupGateAccepted,
        movementFlags,
        verticalSpeed);

    if (!(verticalTravelTime > windowStartScalar)) {
        moveVector = G3D::Vector3(0.0f, 0.0f, 0.0f);
        if (outMoveMagnitude) {
            *outMoveMagnitude = 0.0f;
        }
        return 0.0f;
    }

    const float remainingWindow = verticalTravelTime - windowStartScalar;
    if (remainingWindow > windowSpanScalar) {
        return windowSpanScalar;
    }

    if (remainingWindow < scaledHorizontalWindow) {
        const float horizontalScale = remainingWindow / scaledHorizontalWindow;
        moveVector.x *= horizontalScale;
        moveVector.y *= horizontalScale;
        if (outMoveMagnitude) {
            *outMoveMagnitude = moveVector.magnitude();
        }
    }

    return remainingWindow;
}

void WoWCollision::EvaluateSelectorPairConsumer(float requestedDistance,
                                                const G3D::Vector3& inputMove,
                                                bool directionRankingAccepted,
                                                int32_t selectedIndex,
                                                uint32_t selectedCount,
                                                bool directGateAccepted,
                                                bool hasNegativeDiagonalCandidate,
                                                bool alternateUnitZFallbackGateAccepted,
                                                bool hasUnitZCandidate,
                                                const SelectorPair& directPair,
                                                const SelectorPair& alternatePair,
                                                SelectorPairConsumerTrace& outTrace)
{
    outTrace = SelectorPairConsumerTrace{};
    outTrace.requestedDistance = requestedDistance;
    outTrace.selectedIndex = selectedIndex;
    outTrace.selectedCount = selectedCount;
    outTrace.directionRankingAccepted = directionRankingAccepted ? 1u : 0u;
    outTrace.directGateAccepted = directGateAccepted ? 1u : 0u;
    outTrace.inputMove = inputMove;
    outTrace.outputMove = inputMove;

    if (std::fabs(requestedDistance) <= WOW_SELECTOR_RATIO_EPSILON ||
        (selectedIndex >= 0 && static_cast<uint32_t>(selectedIndex) == selectedCount)) {
        outTrace.preservedInputMove = 1u;
        return;
    }

    const G3D::Vector3 scaledMove = inputMove * requestedDistance;
    outTrace.outputMove = scaledMove;

    if (!directionRankingAccepted) {
        outTrace.outputMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        outTrace.zeroedMoveOnRankingFailure = 1u;
        outTrace.returnCode = 2;
        return;
    }

    outTrace.returnCode = 1;
    if (directGateAccepted) {
        outTrace.directGateState = 1u;
        if (hasNegativeDiagonalCandidate) {
            outTrace.outputPair = directPair;
            outTrace.returnedDirectPair = 1u;
        }
        else {
            outTrace.returnedZeroPair = 1u;
        }
        return;
    }

    if (scaledMove.z < 0.0f &&
        alternateUnitZFallbackGateAccepted &&
        hasUnitZCandidate) {
        outTrace.alternateUnitZState = 1u;
        outTrace.returnedZeroPair = 1u;
        return;
    }

    outTrace.outputPair = alternatePair;
    outTrace.returnedAlternatePair = 1u;
}

bool WoWCollision::EvaluateSelectorCandidateRecordSet(const SelectorCandidateRecord* records,
                                                      uint32_t recordCount,
                                                      const G3D::Vector3& testPoint,
                                                      const SelectorSupportPlane* clipPlanes,
                                                      uint32_t clipPlaneCount,
                                                      const std::array<SelectorSupportPlane, 9>& validationPlanes,
                                                      uint32_t validationPlaneIndex,
                                                      float& inOutBestRatio,
                                                      uint32_t& outBestRecordIndex,
                                                      SelectorRecordEvaluationTrace* outTrace)
{
    SelectorRecordEvaluationTrace trace{};
    trace.inputBestRatio = inOutBestRatio;
    trace.outputBestRatio = inOutBestRatio;
    trace.recordCount = recordCount;
    trace.selectedRecordIndex = outBestRecordIndex;

    if ((records == nullptr && recordCount != 0u) ||
        (clipPlanes == nullptr && clipPlaneCount != 0u) ||
        validationPlaneIndex >= validationPlanes.size()) {
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    bool updatedBestRatio = false;

    for (uint32_t recordIndex = 0; recordIndex < recordCount; ++recordIndex) {
        const SelectorCandidateRecord& record = records[recordIndex];
        const float filterDot = record.filterPlane.normal.dot(testPoint);
        if (filterDot >= WOW_SELECTOR_RECORD_FILTER_THRESHOLD) {
            trace.dotRejectedCount++;
            continue;
        }

        SelectorPointStrip strip{};
        strip.count = 3;
        for (uint32_t pointIndex = 0; pointIndex < strip.count; ++pointIndex) {
            strip.points[pointIndex] = record.points[pointIndex];
            strip.sourceIndices[pointIndex] = 0xFFFFFFFFu;
        }

        if (!ClipSelectorPointStripAgainstPlanePrefix(clipPlanes, clipPlaneCount, strip)) {
            trace.clipRejectedCount++;
            continue;
        }

        float candidateBestRatio = std::numeric_limits<float>::max();
        if (!ValidateSelectorPointStripCandidate(
                strip,
                testPoint,
                validationPlanes,
                validationPlaneIndex,
                candidateBestRatio)) {
            trace.validationRejectedCount++;
            continue;
        }

        trace.validationAcceptedCount++;

        if (candidateBestRatio <= inOutBestRatio) {
            inOutBestRatio = candidateBestRatio;
            outBestRecordIndex = recordIndex;
            updatedBestRatio = true;
            trace.updatedBestRatio = 1;
            trace.selectedRecordIndex = recordIndex;
            trace.selectedBestRatio = candidateBestRatio;
            trace.selectedStripCount = strip.count;
        }
    }

    trace.outputBestRatio = inOutBestRatio;
    if (outTrace) {
        *outTrace = trace;
    }

    return updatedBestRatio;
}

bool WoWCollision::EvaluateSelectorTriangleSourceRanking(const SelectorCandidateRecord* records,
                                                         uint32_t recordCount,
                                                         const G3D::Vector3& testPoint,
                                                         const G3D::Vector3& candidateDirection,
                                                         const std::array<G3D::Vector3, 9>& points,
                                                         const std::array<SelectorSupportPlane, 9>& supportPlanes,
                                                         const std::array<uint8_t, 32>& selectorIndices,
                                                         std::array<SelectorSupportPlane, 5>& ioBestCandidates,
                                                         uint32_t& ioCandidateCount,
                                                         float& ioBestRatio,
                                                         SelectorSourceRankingTrace* outTrace)
{
    SelectorSourceRankingTrace trace{};
    trace.inputBestRatio = ioBestRatio;
    trace.outputBestRatio = ioBestRatio;
    trace.finalCandidateCount = ioCandidateCount;

    constexpr uint32_t selectorSourceCount = 4u;
    constexpr uint32_t selectorTripletOffset = 20u;
    constexpr uint32_t selectorTripletStride = 3u;

    if ((records == nullptr && recordCount != 0u) ||
        ioCandidateCount > ioBestCandidates.size() ||
        selectorTripletOffset + (selectorSourceCount * selectorTripletStride) > selectorIndices.size()) {
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    bool acceptedAnySource = false;

    for (uint32_t sourceIndex = 0; sourceIndex < selectorSourceCount; ++sourceIndex) {
        const SelectorSupportPlane& sourcePlane = supportPlanes[5u + sourceIndex];
        const float sourceDot = sourcePlane.normal.dot(candidateDirection);
        if (sourceDot >= 0.0f) {
            trace.dotRejectedCount++;
            continue;
        }

        const uint32_t selectorOffset = selectorTripletOffset + (sourceIndex * selectorTripletStride);
        const std::array<uint8_t, 3> selectorTriplet = {
            selectorIndices[selectorOffset],
            selectorIndices[selectorOffset + 1u],
            selectorIndices[selectorOffset + 2u]
        };

        std::array<SelectorSupportPlane, 4> candidateClipPlanes{};
        if (!BuildSelectorCandidatePlaneRecord(
                points,
                selectorTriplet,
                candidateDirection,
                sourcePlane,
                candidateClipPlanes)) {
            trace.builderRejectedCount++;
            continue;
        }

        float candidateRatio = ioBestRatio;
        uint32_t bestRecordIndex = 0xFFFFFFFFu;
        if (!EvaluateSelectorCandidateRecordSet(
                records,
                recordCount,
                testPoint,
                candidateClipPlanes.data(),
                3u,
                supportPlanes,
                5u + sourceIndex,
                candidateRatio,
                bestRecordIndex)) {
            trace.evaluatorRejectedCount++;
            continue;
        }

        acceptedAnySource = true;
        trace.acceptedSourceCount++;

        const float overwriteThreshold = ioBestRatio - WOW_CORNER_PLANE_EPSILON;
        if (candidateRatio < overwriteThreshold) {
            ioBestCandidates[0] = sourcePlane;
            ioCandidateCount = 1u;
            trace.overwriteCount++;
        }
        else {
            if (ioCandidateCount >= ioBestCandidates.size()) {
                if (outTrace) {
                    trace.outputBestRatio = ioBestRatio;
                    trace.finalCandidateCount = ioCandidateCount;
                    *outTrace = trace;
                }
                return false;
            }

            const float appendThreshold = ioBestRatio + WOW_CORNER_PLANE_EPSILON;
            if (candidateRatio <= appendThreshold) {
                ioBestCandidates[ioCandidateCount] = sourcePlane;
                ioCandidateCount++;
                trace.appendCount++;
            }
        }

        if (candidateRatio <= ioBestRatio) {
            ioBestRatio = candidateRatio;
            trace.bestRatioUpdatedCount++;
            trace.selectedSourceIndex = sourceIndex;

            if (ioCandidateCount > 1u) {
                const uint32_t newestIndex = ioCandidateCount - 1u;
                std::swap(ioBestCandidates[0], ioBestCandidates[newestIndex]);
                trace.swappedBestToFront++;
            }
        }
    }

    trace.outputBestRatio = ioBestRatio;
    trace.finalCandidateCount = ioCandidateCount;
    if (outTrace) {
        *outTrace = trace;
    }

    return acceptedAnySource;
}

bool WoWCollision::EvaluateSelectorDirectionRanking(const SelectorCandidateRecord* records,
                                                    uint32_t recordCount,
                                                    const G3D::Vector3& testPoint,
                                                    const G3D::Vector3& candidateDirection,
                                                    const std::array<G3D::Vector3, 9>& points,
                                                    const std::array<SelectorSupportPlane, 9>& supportPlanes,
                                                    const std::array<uint8_t, 32>& selectorIndices,
                                                    std::array<SelectorSupportPlane, 5>& ioBestCandidates,
                                                    uint32_t& ioCandidateCount,
                                                    float& ioBestRatio,
                                                    float& outReportedBestRatio,
                                                    uint32_t& ioBestRecordIndex,
                                                    SelectorDirectionRankingTrace* outTrace)
{
    SelectorDirectionRankingTrace trace{};
    trace.inputBestRatio = ioBestRatio;
    trace.outputBestRatio = ioBestRatio;
    trace.reportedBestRatio = ioBestRatio;
    trace.finalCandidateCount = ioCandidateCount;
    trace.selectedRecordIndex = ioBestRecordIndex;

    constexpr uint32_t selectorDirectionCount = 5u;
    constexpr uint32_t selectorRingStride = 4u;

    if ((records == nullptr && recordCount != 0u) ||
        ioCandidateCount > ioBestCandidates.size() ||
        selectorDirectionCount * selectorRingStride > selectorIndices.size()) {
        outReportedBestRatio = ioBestRatio;
        if (outTrace) {
            trace.reportedBestRatio = outReportedBestRatio;
            *outTrace = trace;
        }
        return false;
    }

    bool acceptedAnyDirection = false;

    for (uint32_t directionIndex = 0; directionIndex < selectorDirectionCount; ++directionIndex) {
        const SelectorSupportPlane& directionPlane = supportPlanes[directionIndex];
        const float directionDot = directionPlane.normal.dot(candidateDirection);
        if (directionDot >= 0.0f) {
            trace.dotRejectedCount++;
            continue;
        }

        const uint32_t selectorOffset = directionIndex * selectorRingStride;
        const std::array<uint8_t, 4> selectorRing = {
            selectorIndices[selectorOffset],
            selectorIndices[selectorOffset + 1u],
            selectorIndices[selectorOffset + 2u],
            selectorIndices[selectorOffset + 3u]
        };

        std::array<SelectorSupportPlane, 5> candidateClipPlanes{};
        if (!BuildSelectorCandidateQuadPlaneRecord(
                points,
                selectorRing,
                candidateDirection,
                directionPlane,
                candidateClipPlanes)) {
            trace.builderRejectedCount++;
            continue;
        }

        float candidateRatio = ioBestRatio;
        uint32_t bestRecordIndex = ioBestRecordIndex;
        if (!EvaluateSelectorCandidateRecordSet(
                records,
                recordCount,
                testPoint,
                candidateClipPlanes.data(),
                4u,
                supportPlanes,
                directionIndex,
                candidateRatio,
                bestRecordIndex)) {
            trace.evaluatorRejectedCount++;
            continue;
        }

        acceptedAnyDirection = true;
        trace.acceptedDirectionCount++;

        const float overwriteThreshold = ioBestRatio - WOW_CORNER_PLANE_EPSILON;
        if (candidateRatio < overwriteThreshold) {
            ioBestCandidates[0] = directionPlane;
            ioCandidateCount = 1u;
            trace.overwriteCount++;
        }
        else {
            if (ioCandidateCount >= ioBestCandidates.size()) {
                outReportedBestRatio = ClampSelectorReportedBestRatio(ioBestRatio);
                trace.outputBestRatio = ioBestRatio;
                trace.reportedBestRatio = outReportedBestRatio;
                trace.finalCandidateCount = ioCandidateCount;
                trace.selectedRecordIndex = ioBestRecordIndex;
                if (outReportedBestRatio == 0.0f) {
                    trace.zeroClampedOutput = 1u;
                }
                if (outTrace) {
                    *outTrace = trace;
                }
                return false;
            }

            const float appendThreshold = ioBestRatio + WOW_CORNER_PLANE_EPSILON;
            if (candidateRatio <= appendThreshold) {
                ioBestCandidates[ioCandidateCount] = directionPlane;
                ioCandidateCount++;
                trace.appendCount++;
            }
        }

        if (candidateRatio <= ioBestRatio) {
            ioBestRatio = candidateRatio;
            ioBestRecordIndex = bestRecordIndex;
            trace.bestRatioUpdatedCount++;
            trace.selectedDirectionIndex = directionIndex;
            trace.selectedRecordIndex = ioBestRecordIndex;

            if (ioCandidateCount > 1u) {
                const uint32_t newestIndex = ioCandidateCount - 1u;
                std::swap(ioBestCandidates[0], ioBestCandidates[newestIndex]);
                trace.swappedBestToFront++;
            }
        }
    }

    outReportedBestRatio = ClampSelectorReportedBestRatio(ioBestRatio);
    trace.outputBestRatio = ioBestRatio;
    trace.reportedBestRatio = outReportedBestRatio;
    trace.finalCandidateCount = ioCandidateCount;
    trace.selectedRecordIndex = ioBestRecordIndex;
    if (outReportedBestRatio == 0.0f) {
        trace.zeroClampedOutput = 1u;
    }

    if (outTrace) {
        *outTrace = trace;
    }

    return acceptedAnyDirection;
}

namespace
{
    struct GroundedWallCandidate
    {
        uint32_t index = 0xFFFFFFFFu;
        G3D::Vector3 axis = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 sourceNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 orientedNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float rawOpposeScore = 0.0f;
        float orientedOpposeScore = 0.0f;
        bool usedPositionReorientation = false;
        const SceneQuery::AABBContact* contact = nullptr;
    };

    struct GroundedWallSelectionInfo
    {
        uint32_t queryContactCount = 0;
        uint32_t candidateCount = 0;
        uint32_t selectedContactIndex = 0xFFFFFFFFu;
        G3D::Vector3 selectedNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 orientedNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 primaryAxis = G3D::Vector3(0.0f, 0.0f, 0.0f);
        G3D::Vector3 mergedWallNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        float rawOpposeScore = 0.0f;
        float orientedOpposeScore = 0.0f;
        bool usedPositionReorientation = false;
        const SceneQuery::AABBContact* selectedContact = nullptr;
    };

    bool SelectGroundedWallContact(const std::vector<SceneQuery::AABBContact>& slideContacts,
                                   const G3D::Vector3& currentPosition,
                                   const G3D::Vector3& moveDir2D,
                                   GroundedWallSelectionInfo& outInfo)
    {
        outInfo = GroundedWallSelectionInfo{};
        outInfo.queryContactCount = static_cast<uint32_t>(slideContacts.size());

        std::vector<GroundedWallCandidate> candidates;
        candidates.reserve(slideContacts.size());
        std::vector<G3D::Vector3> blockerAxes;
        blockerAxes.reserve(4);

        auto appendAxis = [&](const G3D::Vector3& axis) {
            for (const auto& existing : blockerAxes) {
                if ((existing - axis).squaredMagnitude() <= 1e-6f) {
                    return;
                }
            }

            if (blockerAxes.size() < 4) {
                blockerAxes.push_back(axis);
            }
        };

        for (uint32_t i = 0; i < slideContacts.size(); ++i) {
            const auto& contact = slideContacts[i];
            if (contact.walkable) {
                continue;
            }

            G3D::Vector3 normal = contact.normal.directionOrZero();
            if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
                continue;
            }

            if (!WoWCollision::IsSelectorContactWithinAlternateWorkingVectorBand(normal.z)) {
                continue;
            }

            G3D::Vector3 horizontalNormal(normal.x, normal.y, 0.0f);
            const float horizontalMag = horizontalNormal.magnitude();
            if (horizontalMag <= PhysicsConstants::VECTOR_EPSILON) {
                continue;
            }

            const float rawOpposeScore = std::max(0.0f, -horizontalNormal.directionOrZero().dot(moveDir2D));

            bool usedPositionReorientation = false;
            const G3D::Vector3 toCurrentPosition(
                currentPosition.x - contact.point.x,
                currentPosition.y - contact.point.y,
                0.0f);
            if (toCurrentPosition.squaredMagnitude() > PhysicsConstants::VECTOR_EPSILON &&
                horizontalNormal.dot(toCurrentPosition) < 0.0f) {
                normal = -normal;
                horizontalNormal = -horizontalNormal;
                usedPositionReorientation = true;
            }

            horizontalNormal = horizontalNormal * (1.0f / horizontalMag);
            const float orientedOpposeScore = -horizontalNormal.dot(moveDir2D);
            if (orientedOpposeScore <= 1.0e-5f) {
                continue;
            }

            G3D::Vector3 axis(0.0f, 0.0f, 0.0f);
            if (std::fabs(horizontalNormal.x) >= std::fabs(horizontalNormal.y)) {
                axis = G3D::Vector3(horizontalNormal.x > 0.0f ? 1.0f : -1.0f, 0.0f, 0.0f);
            }
            else {
                axis = G3D::Vector3(0.0f, horizontalNormal.y > 0.0f ? 1.0f : -1.0f, 0.0f);
            }

            candidates.push_back(GroundedWallCandidate{
                i,
                axis,
                contact.normal.directionOrZero(),
                normal,
                rawOpposeScore,
                orientedOpposeScore,
                usedPositionReorientation,
                &contact });
        }

        outInfo.candidateCount = static_cast<uint32_t>(candidates.size());
        if (candidates.empty()) {
            return false;
        }

        float bestScore = 0.0f;
        size_t bestIndex = 0;
        for (const auto& candidate : candidates) {
            bestScore = std::max(bestScore, candidate.orientedOpposeScore);
        }
        for (size_t i = 0; i < candidates.size(); ++i) {
            if (candidates[i].orientedOpposeScore >= bestScore) {
                bestIndex = i;
                bestScore = candidates[i].orientedOpposeScore;
            }
        }

        const auto& selected = candidates[bestIndex];
        outInfo.selectedContactIndex = selected.index;
        outInfo.selectedNormal = selected.sourceNormal;
        outInfo.orientedNormal = selected.orientedNormal;
        outInfo.primaryAxis = selected.axis;
        outInfo.rawOpposeScore = selected.rawOpposeScore;
        outInfo.orientedOpposeScore = selected.orientedOpposeScore;
        outInfo.usedPositionReorientation = selected.usedPositionReorientation;
        outInfo.selectedContact = selected.contact;

        appendAxis(outInfo.primaryAxis);
        for (const auto& candidate : candidates) {
            if ((candidate.axis - outInfo.primaryAxis).squaredMagnitude() <= 1e-6f) {
                continue;
            }
            appendAxis(candidate.axis);
        }

        if (blockerAxes.empty()) {
            return false;
        }

        if (blockerAxes.size() == 1) {
            outInfo.mergedWallNormal = blockerAxes[0];
        }
        else if (blockerAxes.size() == 2) {
            G3D::Vector3 merged = blockerAxes[0] + blockerAxes[1];
            outInfo.mergedWallNormal = merged.magnitude() > PhysicsConstants::VECTOR_EPSILON
                ? merged.directionOrZero()
                : blockerAxes[0];
        }
        else if (blockerAxes.size() == 3) {
            int xAxisIndices[3] = { 0, 0, 0 };
            int yAxisIndices[3] = { 0, 0, 0 };
            int xAxisCount = 0;
            int yAxisCount = 0;

            for (size_t i = 0; i < blockerAxes.size(); ++i) {
                const auto& axis = blockerAxes[i];
                if (std::fabs(axis.y) <= PhysicsConstants::VECTOR_EPSILON) {
                    xAxisIndices[xAxisCount++] = static_cast<int>(i);
                }
                else if (std::fabs(axis.x) <= PhysicsConstants::VECTOR_EPSILON) {
                    yAxisIndices[yAxisCount++] = static_cast<int>(i);
                }
            }

            if (xAxisCount == 1) {
                outInfo.mergedWallNormal = blockerAxes[xAxisIndices[0]];
            }
            else if (yAxisCount > 0) {
                outInfo.mergedWallNormal = blockerAxes[yAxisIndices[0]];
            }
            else {
                outInfo.mergedWallNormal = blockerAxes[0];
            }
        }
        else {
            outInfo.mergedWallNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
        }

        return outInfo.mergedWallNormal.magnitude() > PhysicsConstants::VECTOR_EPSILON;
    }
}

bool WoWCollision::ResolveGroundedWallContacts(const std::vector<SceneQuery::AABBContact>& slideContacts,
                                               const G3D::Vector3& currentPosition,
                                               const G3D::Vector3& requestedMove,
                                               float collisionRadius,
                                               float boundingHeight,
                                               bool& groundedWallState,
                                               G3D::Vector3& outResolvedMove,
                                               G3D::Vector3& outWallNormal,
                                               float& outBlockedFraction,
                                               GroundedWallResolutionTrace* outTrace)
{
    GroundedWallResolutionTrace trace{};
    trace.queryContactCount = static_cast<uint32_t>(slideContacts.size());
    trace.groundedWallStateBefore = groundedWallState ? 1u : 0u;

    outResolvedMove = requestedMove;
    outWallNormal = G3D::Vector3(0.0f, 0.0f, 1.0f);
    outBlockedFraction = 1.0f;

    const float requested2D = std::sqrt((requestedMove.x * requestedMove.x) + (requestedMove.y * requestedMove.y));
    trace.requested2D = requested2D;
    if (requested2D <= PhysicsConstants::VECTOR_EPSILON) {
        trace.groundedWallStateAfter = groundedWallState ? 1u : 0u;
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    const G3D::Vector3 moveDir2D = G3D::Vector3(requestedMove.x, requestedMove.y, 0.0f).directionOrZero();
    if (moveDir2D.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
        trace.groundedWallStateAfter = groundedWallState ? 1u : 0u;
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    GroundedWallSelectionInfo selectionInfo;
    const bool hasMergedWallNormal = SelectGroundedWallContact(slideContacts, currentPosition, moveDir2D, selectionInfo);
    trace.candidateCount = selectionInfo.candidateCount;
    trace.selectedContactIndex = selectionInfo.selectedContactIndex;
    trace.usedPositionReorientation = selectionInfo.usedPositionReorientation ? 1u : 0u;
    trace.selectedNormal = selectionInfo.selectedNormal;
    trace.orientedNormal = selectionInfo.orientedNormal;
    trace.primaryAxis = selectionInfo.primaryAxis;
    trace.mergedWallNormal = selectionInfo.mergedWallNormal;
    trace.finalWallNormal = selectionInfo.mergedWallNormal;
    trace.rawOpposeScore = selectionInfo.rawOpposeScore;
    trace.orientedOpposeScore = selectionInfo.orientedOpposeScore;

    if (selectionInfo.selectedContact != nullptr) {
        trace.selectedInstanceId = selectionInfo.selectedContact->instanceId;
        trace.rawWalkable = selectionInfo.selectedContact->walkable ? 1u : 0u;
        trace.selectedPoint = selectionInfo.selectedContact->point;
        trace.selectedThresholdPoint = currentPosition + requestedMove;
        const auto withoutState = WoWCollision::CheckWalkable(
            *selectionInfo.selectedContact,
            currentPosition,
            collisionRadius,
            boundingHeight,
            true,
            false);
        const auto withState = WoWCollision::CheckWalkable(
            *selectionInfo.selectedContact,
            currentPosition,
            collisionRadius,
            boundingHeight,
            true,
            groundedWallState);
        const auto thresholdGateStandard = WoWCollision::EvaluateSelectedContactThresholdGate(
            *selectionInfo.selectedContact,
            currentPosition,
            trace.selectedThresholdPoint,
            true);
        const auto thresholdGateRelaxed = WoWCollision::EvaluateSelectedContactThresholdGate(
            *selectionInfo.selectedContact,
            currentPosition,
            trace.selectedThresholdPoint,
            false);
        trace.walkableWithoutState = withoutState.walkable ? 1u : 0u;
        trace.walkableWithState = withState.walkable ? 1u : 0u;
        trace.selectedThresholdNormalZ = thresholdGateStandard.normalZ;
        trace.selectedCurrentPositionInsidePrism = thresholdGateStandard.currentPositionInsidePrism ? 1u : 0u;
        trace.selectedProjectedPositionInsidePrism = thresholdGateStandard.projectedPositionInsidePrism ? 1u : 0u;
        trace.selectedThresholdSensitiveStandard = thresholdGateStandard.thresholdSensitive ? 1u : 0u;
        trace.selectedThresholdSensitiveRelaxed = thresholdGateRelaxed.thresholdSensitive ? 1u : 0u;
        trace.selectedWouldUseDirectPairStandard = thresholdGateStandard.wouldUseDirectPair ? 1u : 0u;
        trace.selectedWouldUseDirectPairRelaxed = thresholdGateRelaxed.wouldUseDirectPair ? 1u : 0u;
    }

    if (!hasMergedWallNormal) {
        trace.groundedWallStateAfter = groundedWallState ? 1u : 0u;
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    outWallNormal = selectionInfo.mergedWallNormal;
    const float intoSurface = requestedMove.dot(outWallNormal);
    if (intoSurface >= -PhysicsConstants::VECTOR_EPSILON) {
        trace.groundedWallStateAfter = groundedWallState ? 1u : 0u;
        if (outTrace) {
            *outTrace = trace;
        }
        return false;
    }

    G3D::Vector3 horizontalProjectedMove = requestedMove - (outWallNormal * intoSurface);
    horizontalProjectedMove.z = 0.0f;
    G3D::Vector3 horizontalWallNormal(outWallNormal.x, outWallNormal.y, 0.0f);
    const float horizontalWallMag = horizontalWallNormal.magnitude();
    if (horizontalWallMag > PhysicsConstants::VECTOR_EPSILON) {
        horizontalWallNormal = horizontalWallNormal * (1.0f / horizontalWallMag);
        horizontalProjectedMove += horizontalWallNormal * 0.001f;
    }

    trace.horizontalProjectedMove = horizontalProjectedMove;
    float horizontalResolved2D = std::sqrt(
        (horizontalProjectedMove.x * horizontalProjectedMove.x) +
        (horizontalProjectedMove.y * horizontalProjectedMove.y));
    trace.horizontalResolved2D = horizontalResolved2D;

    G3D::Vector3 projectedMove = horizontalProjectedMove;
    float slopedResolved2D = horizontalResolved2D;
    bool usedWalkableSelectedContact = false;
    bool usedNonWalkableVertical = false;
    bool usedUphillDiscard = false;
    bool usedPrimaryAxisFallback = false;
    trace.branchKind = GROUNDED_WALL_BRANCH_HORIZONTAL;

    if (selectionInfo.selectedContact != nullptr) {
        const bool selectedContactRawWalkable = selectionInfo.selectedContact->walkable;
        WoWCollision::CheckWalkableResult walkableResult = WoWCollision::CheckWalkable(
            *selectionInfo.selectedContact,
            currentPosition,
            collisionRadius,
            boundingHeight,
            true,
            groundedWallState);

        // Only raw walkable selected contacts take the direct vertical-only path.
        // Stateful carry-over can keep flat support or side blockers "walkable"
        // for selection purposes, but those contacts still belong in the later
        // non-walkable branch gate instead of zeroing XY immediately.
        if (selectedContactRawWalkable && walkableResult.walkable) {
            const float intoPlane = requestedMove.dot(selectionInfo.selectedNormal);
            G3D::Vector3 verticalCorrection = requestedMove - (selectionInfo.selectedNormal * intoPlane);

            const float verticalLimit = collisionRadius;
            if (std::fabs(verticalCorrection.z) > verticalLimit &&
                verticalLimit > PhysicsConstants::VECTOR_EPSILON) {
                verticalCorrection.z = (verticalCorrection.z > 0.0f ? verticalLimit : -verticalLimit);
            }

            projectedMove = G3D::Vector3(0.0f, 0.0f, verticalCorrection.z);
            groundedWallState = walkableResult.groundedWallFlagAfter;
            usedWalkableSelectedContact = true;
            trace.branchKind = GROUNDED_WALL_BRANCH_WALKABLE_SELECTED_VERTICAL;
        }
    }

    uint32_t nonWalkableGateReturnCode = 0u;
    if (!usedWalkableSelectedContact) {
        bool foundStatefulSupportCandidate = false;
        float highestStatefulSupportZ = -FLT_MAX;

        for (const auto& contact : slideContacts) {
            G3D::Vector3 supportNormal = contact.normal.directionOrZero();
            if (supportNormal.magnitude() <= PhysicsConstants::VECTOR_EPSILON) {
                supportNormal = contact.rawNormal.directionOrZero();
            }

            // The stateful fallback scan is only looking for actual support faces.
            // Horizontal or upward-facing blockers can be temporarily "walkable"
            // under grounded-wall state, but they are not valid support for the
            // non-walkable vertical retry gate.
            if ((-supportNormal.z) <= WOW_WALKABLE_MIN_NORMAL_Z) {
                continue;
            }

            const WoWCollision::CheckWalkableResult supportWalkable = WoWCollision::CheckWalkable(
                contact,
                currentPosition,
                collisionRadius,
                boundingHeight,
                true,
                groundedWallState);
            if (!supportWalkable.walkable || !supportWalkable.walkableState) {
                continue;
            }

            foundStatefulSupportCandidate = true;
            highestStatefulSupportZ = std::max(highestStatefulSupportZ, contact.point.z);
        }

        if (foundStatefulSupportCandidate) {
            const float upwardClearanceThreshold = std::max(0.05f, collisionRadius * 0.25f);
            nonWalkableGateReturnCode =
                highestStatefulSupportZ > (currentPosition.z + upwardClearanceThreshold) ? 2u : 1u;
        }
    }

    if (!usedWalkableSelectedContact && nonWalkableGateReturnCode == 2u) {
        const float intoPlane = requestedMove.dot(selectionInfo.selectedNormal);
        projectedMove = requestedMove - (selectionInfo.selectedNormal * intoPlane);

        const float verticalLimit = collisionRadius;
        if (std::fabs(projectedMove.z) > verticalLimit &&
            verticalLimit > PhysicsConstants::VECTOR_EPSILON) {
            const float scale = verticalLimit / std::fabs(projectedMove.z);
            projectedMove.x *= scale;
            projectedMove.y *= scale;
            projectedMove.z *= scale;
        }

        slopedResolved2D = std::sqrt(
            (projectedMove.x * projectedMove.x) +
            (projectedMove.y * projectedMove.y));

        if (projectedMove.z > 0.0f &&
            horizontalResolved2D > slopedResolved2D + 1e-4f) {
            projectedMove = horizontalProjectedMove;
            slopedResolved2D = horizontalResolved2D;
            usedUphillDiscard = true;
        }
        else {
            usedNonWalkableVertical = true;
            trace.branchKind = GROUNDED_WALL_BRANCH_NON_WALKABLE_VERTICAL;
        }
    }

    trace.branchProjectedMove = projectedMove;
    trace.slopedResolved2D = slopedResolved2D;

    float resolved2D = std::sqrt((projectedMove.x * projectedMove.x) + (projectedMove.y * projectedMove.y));
    if (!usedWalkableSelectedContact &&
        resolved2D < (requested2D * 0.25f) &&
        selectionInfo.primaryAxis.magnitude() > PhysicsConstants::VECTOR_EPSILON &&
        (selectionInfo.primaryAxis - outWallNormal).squaredMagnitude() > 1e-6f) {
        const float primaryInto = requestedMove.dot(selectionInfo.primaryAxis);
        if (primaryInto < -PhysicsConstants::VECTOR_EPSILON) {
            G3D::Vector3 primaryProjectedMove = requestedMove - (selectionInfo.primaryAxis * primaryInto);
            primaryProjectedMove.z = 0.0f;
            const float primaryResolved2D = std::sqrt(
                (primaryProjectedMove.x * primaryProjectedMove.x) +
                (primaryProjectedMove.y * primaryProjectedMove.y));

            if (primaryResolved2D > resolved2D + 1e-4f) {
                projectedMove = primaryProjectedMove;
                resolved2D = primaryResolved2D;
                outWallNormal = selectionInfo.primaryAxis;
                trace.finalWallNormal = selectionInfo.primaryAxis;
                usedPrimaryAxisFallback = true;
            }
        }
    }

    if (usedNonWalkableVertical) {
        groundedWallState = true;
    }

    if (resolved2D <= PhysicsConstants::VECTOR_EPSILON ||
        projectedMove.directionOrZero().dot(moveDir2D) <= PhysicsConstants::VECTOR_EPSILON) {
        projectedMove = G3D::Vector3(0.0f, 0.0f, 0.0f);
        resolved2D = 0.0f;
    }
    else if (resolved2D > requested2D) {
        projectedMove = projectedMove * (requested2D / resolved2D);
        resolved2D = requested2D;
    }

    outResolvedMove = projectedMove;
    outBlockedFraction = std::max(0.0f, std::min(1.0f, resolved2D / requested2D));

    trace.usedWalkableSelectedContact = usedWalkableSelectedContact ? 1u : 0u;
    trace.usedNonWalkableVertical = usedNonWalkableVertical ? 1u : 0u;
    trace.usedUphillDiscard = usedUphillDiscard ? 1u : 0u;
    trace.usedPrimaryAxisFallback = usedPrimaryAxisFallback ? 1u : 0u;
    trace.finalProjectedMove = projectedMove;
    trace.finalResolved2D = resolved2D;
    trace.blockedFraction = outBlockedFraction;
    trace.groundedWallStateAfter = groundedWallState ? 1u : 0u;
    if (outTrace) {
        *outTrace = trace;
    }

    return true;
}

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

// [DELETED] ComputeStartOverlapSlideNormal — old 3-pass helper


// [DELETED] HandleNoHorizontalMovement — old 3-pass helper


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

// [DELETED] Old 3-pass system (DecomposeMovement, ExecuteUp/Side/Down, PerformThreePassMove, GroundMoveElevatedSweep)
// Replaced by the grounded CollisionStepWoW helper; StepV2 owns the top-level
// 0x633840 branch selection between airborne, swim, and grounded movement.


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

// [DELETED] PerformVerticalPlacementOrFall — old 3-pass helper


// =============================================================================
// WoW.exe grounded CollisionStep branch (0x633C7B onward) — AABB-based implementation
//
// StepV2 handles the top-level 0x633840 branch order. This helper covers the
// grounded branch only and replaces the old custom ground collision logic with:
// 1. Build AABB at position: ±collisionSkin XY, Z to Z+stepHeight
// 2. Compute slope-limited sweep distance
// 3. SWEEP 1: full displacement → TestTerrainAABB
// 4. SWEEP 2: half-step with √2-contracted AABB → TestTerrainAABB
// 5. Step height adjustment: maxZ += min(2r, speed*dt)
// 6. Ground search: minZ = maxZ - (r + speed*dt*tan50°)
// 7. Final terrain validation
// =============================================================================
void PhysicsEngine::CollisionStepWoW(const PhysicsInput& input, const MovementIntent& intent,
    MovementState& st, float radius, float height,
    const G3D::Vector3& moveDir, float intendedDist, float dt, float moveSpeed)
{
    if (intendedDist < MIN_MOVE_DISTANCE) return;

    st.wallHit = false;
    st.wallHitNormal = G3D::Vector3(0, 0, 1);
    st.wallBlockedFraction = 1.0f;

    const G3D::Vector3 dirN = moveDir.directionOrZero();

    // Binary parity (0x633C7B): snap to ground surface before computing sweep.
    // WoW.exe queries GetGroundZ at the current position and snaps Z to the
    // terrain surface before building the AABB sweep volume. Without this,
    // the character can start slightly above/below the terrain (from previous
    // frame's movement), causing the sweep to miss contacts or produce
    // incorrect AABB bounds.
    {
        float snapZ = SceneQuery::GetGroundZ(input.mapId, st.x, st.y,
            st.z + PhysicsConstants::STEP_HEIGHT,
            PhysicsConstants::STEP_HEIGHT + PhysicsConstants::STEP_DOWN_HEIGHT);
        if (VMAP::IsValidHeight(snapZ) &&
            snapZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT &&
            snapZ <= st.z + PhysicsConstants::STEP_HEIGHT) {
            st.z = snapZ;
        }
    }

    const float startX = st.x, startY = st.y, startZ = st.z;

    // WoW.exe constants from binary (VA 0x633840)
    const float skin = PhysicsConstants::COLLISION_SKIN_FRACTION;  // 0.333333
    const float stepH = PhysicsConstants::STEP_HEIGHT;             // 2.027778
    const float tan50 = PhysicsConstants::WALKABLE_TAN_MAX_SLOPE;  // 1.19175363
    const float sqrt2 = PhysicsConstants::SQRT_2;                  // 1.41421354
    const float speedDt = moveSpeed * dt;

    // Step 1: Compute slope limit (0x633C7B)
    // slopeLimit = max(boundingRadius * tan(50°), skin + 1/720)
    const float slopeFromRadius = radius * tan50;
    const float slopeThreshold = skin + PhysicsConstants::COLLISION_SKIN_EPSILON;
    const float slopeLimit = std::max(slopeFromRadius, slopeThreshold);

    // Step 2: Compute total sweep distance
    const float sweepDist = slopeLimit + speedDt;

    // Step 3: Compute end position
    float endX = startX + dirN.x * sweepDist;
    float endY = startY + dirN.y * sweepDist;

    // Step 4: Build AABB at end position with WoW.exe bounds
    // WoW.exe (0x633E06) step height adjustment:
    //   maxZ = pos.Z + stepHeight + min(2*radius, speed*dt)
    //   minZ = maxZ - (radius + speed*dt*tan(50°))
    // This creates a search volume that encompasses terrain on both uphill/downhill.
    const float stepUp = std::min(2.0f * radius, speedDt);
    const float adjustedMaxZ = startZ + stepH + stepUp;
    const float slopeDown = radius + speedDt * tan50;
    const float adjustedMinZ = adjustedMaxZ - slopeDown - stepH;
    G3D::Vector3 endBoxMin(endX - skin, endY - skin, adjustedMinZ);
    G3D::Vector3 endBoxMax(endX + skin, endY + skin, adjustedMaxZ);

    // Step 5a/5b: WoW.exe does not keep explicit sweep-contact lists here.
    // The 0x6373B0 helper is an AABB merge routine: it unions the start box with
    // the full-step box, then unions that result with the contracted half-step box
    // before the final TestTerrain query runs.
    G3D::Vector3 startBoxMin(startX - skin, startY - skin, adjustedMinZ);
    G3D::Vector3 startBoxMax(startX + skin, startY + skin, adjustedMaxZ);
    float halfDist = speedDt * 0.5f;
    float contracted = skin * sqrt2;
    const float halfX = startX + dirN.x * halfDist;
    const float halfY = startY + dirN.y * halfDist;
    G3D::Vector3 halfBoxMin(halfX - contracted, halfY - contracted, adjustedMinZ);
    G3D::Vector3 halfBoxMax(halfX + contracted, halfY + contracted, adjustedMaxZ);

    G3D::Vector3 queryBoxMin = startBoxMin;
    G3D::Vector3 queryBoxMax = startBoxMax;
    WoWCollision::MergeAabbBounds(queryBoxMin, queryBoxMax, endBoxMin, endBoxMax, queryBoxMin, queryBoxMax);
    WoWCollision::MergeAabbBounds(queryBoxMin, queryBoxMax, halfBoxMin, halfBoxMax, queryBoxMin, queryBoxMax);

    // Step 5c: Test terrain on the merged query volume (0x6721B0 TestTerrain)
    std::vector<SceneQuery::AABBContact> slideContacts;
    SceneQuery::TestTerrainAABB(input.mapId, queryBoxMin, queryBoxMax, slideContacts);

    auto buildMergedBlockerNormal = [&](const G3D::Vector3& currentPosition,
        const G3D::Vector3& moveDir2D,
        G3D::Vector3& outNormal,
        G3D::Vector3& outPrimaryAxis,
        G3D::Vector3& outPrimaryContactNormal,
        const SceneQuery::AABBContact*& outPrimaryContact) -> bool {
        struct BlockerCandidate
        {
            G3D::Vector3 axis;
            G3D::Vector3 sourceNormal;
            float score;
            const SceneQuery::AABBContact* contact;
        };

        std::vector<BlockerCandidate> candidates;
        candidates.reserve(slideContacts.size());
        std::vector<G3D::Vector3> blockerAxes;
        blockerAxes.reserve(4);

        auto appendAxis = [&](const G3D::Vector3& axis) {
            for (const auto& existing : blockerAxes) {
                if ((existing - axis).squaredMagnitude() <= 1e-6f)
                    return;
            }

            if (blockerAxes.size() < 4)
                blockerAxes.push_back(axis);
        };

        for (const auto& contact : slideContacts) {
            if (contact.walkable)
                continue;

            G3D::Vector3 normal = contact.normal.directionOrZero();
            if (normal.magnitude() <= PhysicsConstants::VECTOR_EPSILON)
                continue;

            // The grounded resolver after TestTerrain works from a small set of
            // AABB blocker axes rather than projecting across every raw triangle
            // normal. We recover those axes from the current query in movement
            // direction order, then merge them with the same 1 / 2 / 3+ rules
            // visible in the local 0x636610 helper.
            G3D::Vector3 horizontalNormal(normal.x, normal.y, 0.0f);
            const float horizontalMag = horizontalNormal.magnitude();
            if (horizontalMag <= PhysicsConstants::VECTOR_EPSILON)
                continue;

            const G3D::Vector3 toCurrentPosition(
                currentPosition.x - contact.point.x,
                currentPosition.y - contact.point.y,
                0.0f);
            if (toCurrentPosition.squaredMagnitude() > PhysicsConstants::VECTOR_EPSILON &&
                horizontalNormal.dot(toCurrentPosition) < 0.0f) {
                normal = -normal;
                horizontalNormal = -horizontalNormal;
            }

            horizontalNormal = horizontalNormal * (1.0f / horizontalMag);
            const float opposeScore = -horizontalNormal.dot(moveDir2D);
            if (opposeScore <= 1.0e-5f)
                continue;

            if (std::fabs(horizontalNormal.x) >= std::fabs(horizontalNormal.y)) {
                candidates.push_back(BlockerCandidate{
                    G3D::Vector3(horizontalNormal.x > 0.0f ? 1.0f : -1.0f, 0.0f, 0.0f),
                    normal,
                    opposeScore,
                    &contact });
            }
            else {
                candidates.push_back(BlockerCandidate{
                    G3D::Vector3(0.0f, horizontalNormal.y > 0.0f ? 1.0f : -1.0f, 0.0f),
                    normal,
                    opposeScore,
                    &contact });
            }
        }

        outPrimaryAxis = G3D::Vector3(0, 0, 0);
        outPrimaryContactNormal = G3D::Vector3(0, 0, 0);
        outPrimaryContact = nullptr;
        if (!candidates.empty()) {
            float bestScore = 0.0f;
            size_t bestIndex = 0;
            for (const auto& candidate : candidates)
                bestScore = std::max(bestScore, candidate.score);
            for (size_t i = 0; i < candidates.size(); ++i) {
                if (candidates[i].score >= bestScore) {
                    bestIndex = i;
                    bestScore = candidates[i].score;
                }
            }

            outPrimaryAxis = candidates[bestIndex].axis;
            outPrimaryContactNormal = candidates[bestIndex].sourceNormal;
            outPrimaryContact = candidates[bestIndex].contact;
            appendAxis(outPrimaryAxis);
            for (const auto& candidate : candidates) {
                if ((candidate.axis - outPrimaryAxis).squaredMagnitude() <= 1e-6f)
                    continue;
                appendAxis(candidate.axis);
            }
        }

        if (blockerAxes.empty())
            return false;

        if (blockerAxes.size() == 1) {
            outNormal = blockerAxes[0];
        }
        else if (blockerAxes.size() == 2) {
            G3D::Vector3 merged = blockerAxes[0] + blockerAxes[1];
            outNormal = merged.magnitude() > PhysicsConstants::VECTOR_EPSILON
                ? merged.directionOrZero()
                : blockerAxes[0];
        }
        else if (blockerAxes.size() == 3) {
            // Local WoW.exe 0x636610 does not zero the three-axis case. It
            // chooses the lone axis from the minority orientation group:
            // one X-axis against two Y-axes, or one Y-axis against two X-axes.
            int xAxisIndices[3] = { 0, 0, 0 };
            int yAxisIndices[3] = { 0, 0, 0 };
            int xAxisCount = 0;
            int yAxisCount = 0;

            for (size_t i = 0; i < blockerAxes.size(); ++i) {
                const auto& axis = blockerAxes[i];
                if (std::fabs(axis.y) <= PhysicsConstants::VECTOR_EPSILON) {
                    xAxisIndices[xAxisCount++] = static_cast<int>(i);
                }
                else if (std::fabs(axis.x) <= PhysicsConstants::VECTOR_EPSILON) {
                    yAxisIndices[yAxisCount++] = static_cast<int>(i);
                }
            }

            if (xAxisCount == 1) {
                outNormal = blockerAxes[xAxisIndices[0]];
            }
            else if (yAxisCount > 0) {
                outNormal = blockerAxes[yAxisIndices[0]];
            }
            else {
                outNormal = blockerAxes[0];
            }
        }
        else {
            // Local WoW.exe 0x636610 zeroes the merged blocker vector once all
            // four cardinal blocker axes are present.
            outNormal = G3D::Vector3(0, 0, 0);
        }

        return outNormal.magnitude() > PhysicsConstants::VECTOR_EPSILON;
    };

    auto resolveWallSlide = [&](const G3D::Vector3& currentPosition,
        const G3D::Vector3& requestedMove,
        G3D::Vector3& outResolvedMove,
        G3D::Vector3& outWallNormal,
        float& outBlockedFraction) -> bool {
        return WoWCollision::ResolveGroundedWallContacts(
            slideContacts,
            currentPosition,
            requestedMove,
            radius,
            height,
            st.groundedWallState,
            outResolvedMove,
            outWallNormal,
            outBlockedFraction,
            nullptr);

        outResolvedMove = requestedMove;
        outWallNormal = G3D::Vector3(0, 0, 1);

        const float requested2D = std::sqrt((requestedMove.x * requestedMove.x) + (requestedMove.y * requestedMove.y));
        if (requested2D <= PhysicsConstants::VECTOR_EPSILON) {
            outBlockedFraction = 1.0f;
            return false;
        }

        const G3D::Vector3 moveDir2D = G3D::Vector3(requestedMove.x, requestedMove.y, 0.0f).directionOrZero();
        G3D::Vector3 primaryAxis(0, 0, 0);
        G3D::Vector3 primaryContactNormal(0, 0, 0);
        const SceneQuery::AABBContact* primaryContact = nullptr;
        if (moveDir2D.magnitude() <= PhysicsConstants::VECTOR_EPSILON ||
            !buildMergedBlockerNormal(currentPosition, moveDir2D, outWallNormal, primaryAxis, primaryContactNormal, primaryContact)) {
            outBlockedFraction = 1.0f;
            return false;
        }

        const float intoSurface = requestedMove.dot(outWallNormal);
        if (intoSurface >= -PhysicsConstants::VECTOR_EPSILON) {
            outBlockedFraction = 1.0f;
            return false;
        }

        G3D::Vector3 horizontalProjectedMove = requestedMove - (outWallNormal * intoSurface);
        horizontalProjectedMove.z = 0.0f;
        G3D::Vector3 horizontalWallNormal(outWallNormal.x, outWallNormal.y, 0.0f);
        const float horizontalWallMag = horizontalWallNormal.magnitude();
        if (horizontalWallMag > PhysicsConstants::VECTOR_EPSILON) {
            // Local WoW.exe 0x635D80 adds a tiny 0.001f horizontal pushout
            // after the normal correction so the resolved move is not left
            // exactly on the blocker plane.
            horizontalWallNormal = horizontalWallNormal * (1.0f / horizontalWallMag);
            horizontalProjectedMove += horizontalWallNormal * 0.001f;
        }

        G3D::Vector3 projectedMove = horizontalProjectedMove;
        float horizontalResolved2D = std::sqrt(
            (horizontalProjectedMove.x * horizontalProjectedMove.x) +
            (horizontalProjectedMove.y * horizontalProjectedMove.y));
        bool usedWalkableSelectedContact = false;
        bool usedNonWalkableVertical = false;

        if (primaryContact != nullptr) {
            WoWCollision::CheckWalkableResult walkableResult = WoWCollision::CheckWalkable(
                *primaryContact,
                currentPosition,
                radius,
                height,
                true,
                st.groundedWallState);

            if (walkableResult.walkable) {
                const float intoPlane = requestedMove.dot(primaryContactNormal);
                G3D::Vector3 verticalCorrection = requestedMove - (primaryContactNormal * intoPlane);

                const float verticalLimit = radius;
                if (std::fabs(verticalCorrection.z) > verticalLimit &&
                    verticalLimit > PhysicsConstants::VECTOR_EPSILON) {
                    verticalCorrection.z = (verticalCorrection.z > 0.0f ? verticalLimit : -verticalLimit);
                }

                // Local WoW.exe 0x635C00 returns the selected-plane vertical correction
                // only (X = Y = 0). Preserve that shape on the stateful walkable path
                // instead of turning the support face into a plane-slide vector.
                projectedMove = G3D::Vector3(0.0f, 0.0f, verticalCorrection.z);
                st.groundedWallState = walkableResult.groundedWallFlagAfter;
                usedWalkableSelectedContact = true;
            }

        }

        // Binary-backed 0x636100 branch gate: decides between horizontal (0x635D80)
        // and vertical (0x635C00) correction paths.
        //
        // Walkable contacts (normal.z >= cos50° = 0.6428) already handled above.
        // Non-walkable contacts with a slope component (0 < normal.z < cos50°)
        // go to the vertical retry path with 0x04000000 flag (gate return code 2).
        // Pure horizontal contacts (normal.z ≈ 0) use horizontal correction only.
        //
        // Binary 0x636100: checks if selectedNormal.z is between 0 and walkable
        // threshold. If so, builds horizontal working vector and probes upward.
        // Simplified: use the Z threshold directly instead of the full probe,
        // since the probe's purpose is to determine if the contact has a vertical
        // component worth correcting for.
        const float walkableThreshold = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;  // cos(50°) = 0.6428
        if (!usedWalkableSelectedContact &&
            primaryContactNormal.z > PhysicsConstants::VECTOR_EPSILON &&
            primaryContactNormal.z < walkableThreshold) {
            // 0x636100 gate return 2: non-walkable slope with vertical component.
            // Project onto selected contact plane and clamp Z to radius.
            // Set 0x04000000 flag per binary (gate return code 2 behavior).
            const float intoPlane = requestedMove.dot(primaryContactNormal);
            projectedMove = requestedMove - (primaryContactNormal * intoPlane);

            const float verticalLimit = radius;
            if (std::fabs(projectedMove.z) > verticalLimit &&
                verticalLimit > PhysicsConstants::VECTOR_EPSILON) {
                const float scale = verticalLimit / std::fabs(projectedMove.z);
                projectedMove.x *= scale;
                projectedMove.y *= scale;
                projectedMove.z *= scale;
            }

            const float slopedResolved2D = std::sqrt(
                (projectedMove.x * projectedMove.x) +
                (projectedMove.y * projectedMove.y));

            // Discard vertical correction if it manufactures uphill while losing
            // forward progress vs the horizontal branch
            if (projectedMove.z > 0.0f &&
                horizontalResolved2D > slopedResolved2D + 1e-4f) {
                projectedMove = horizontalProjectedMove;
            }
            else {
                usedNonWalkableVertical = true;
            }
        }

        float resolved2D = std::sqrt((projectedMove.x * projectedMove.x) + (projectedMove.y * projectedMove.y));
        if (!usedWalkableSelectedContact &&
            resolved2D < (requested2D * 0.25f) &&
            primaryAxis.magnitude() > PhysicsConstants::VECTOR_EPSILON &&
            (primaryAxis - outWallNormal).squaredMagnitude() > 1e-6f) {
            const float primaryInto = requestedMove.dot(primaryAxis);
            if (primaryInto < -PhysicsConstants::VECTOR_EPSILON) {
                G3D::Vector3 primaryProjectedMove = requestedMove - (primaryAxis * primaryInto);
                primaryProjectedMove.z = 0.0f;
                const float primaryResolved2D = std::sqrt(
                    (primaryProjectedMove.x * primaryProjectedMove.x) +
                    (primaryProjectedMove.y * primaryProjectedMove.y));

                if (primaryResolved2D > resolved2D + 1e-4f) {
                    projectedMove = primaryProjectedMove;
                    resolved2D = primaryResolved2D;
                    outWallNormal = primaryAxis;
                }
            }
        }

        if (usedNonWalkableVertical) {
            st.groundedWallState = true;
        }

        if (resolved2D <= PhysicsConstants::VECTOR_EPSILON ||
            projectedMove.directionOrZero().dot(moveDir2D) <= PhysicsConstants::VECTOR_EPSILON) {
            projectedMove = G3D::Vector3(0, 0, 0);
            resolved2D = 0.0f;
        }
        else if (resolved2D > requested2D) {
            projectedMove = projectedMove * (requested2D / resolved2D);
            resolved2D = requested2D;
        }

        outResolvedMove = projectedMove;
        outBlockedFraction = std::max(0.0f, std::min(1.0f, resolved2D / requested2D));
        return true;
    };

    const G3D::Vector3 requestedMove(dirN.x * intendedDist, dirN.y * intendedDist, 0.0f);
    G3D::Vector3 resolvedMove = requestedMove;
    G3D::Vector3 wallNormal(0, 0, 1);
    float blockedFraction = 1.0f;

    // Binary-backed retry loop from WoW.exe 0x6367B0:
    // Each iteration re-queries terrain contacts at the CURRENT position, resolves
    // wall slide, then advances position. Exit when originalDist - accumulated < 1.0f
    // or after 5 small-remainder iterations. Matches the for(;;) resweep loop in the binary.
    {
        const float originalDist = intendedDist;
        float accumulatedDist = 0.0f;
        float currentX = startX;
        float currentY = startY;
        G3D::Vector3 currentMoveVec = requestedMove;
        G3D::Vector3 totalResolvedMove(0, 0, 0);
        int smallRemainCount = 0;

        for (int wallIter = 0; wallIter < 20; wallIter++) {  // safety limit; binary uses smallRemainCount>5

            // Re-query terrain contacts at current position for this iteration
            // Build AABB around current position + move direction (matching binary's per-iteration TestTerrain)
            float curEndX = currentX + currentMoveVec.x;
            float curEndY = currentY + currentMoveVec.y;
            G3D::Vector3 curStartBoxMin(currentX - skin, currentY - skin, adjustedMinZ);
            G3D::Vector3 curStartBoxMax(currentX + skin, currentY + skin, adjustedMaxZ);
            G3D::Vector3 curEndBoxMin(curEndX - skin, curEndY - skin, adjustedMinZ);
            G3D::Vector3 curEndBoxMax(curEndX + skin, curEndY + skin, adjustedMaxZ);
            float curHalfX = currentX + currentMoveVec.x * 0.5f;
            float curHalfY = currentY + currentMoveVec.y * 0.5f;
            float contracted = skin * sqrt2;
            G3D::Vector3 curHalfMin(curHalfX - contracted, curHalfY - contracted, adjustedMinZ);
            G3D::Vector3 curHalfMax(curHalfX + contracted, curHalfY + contracted, adjustedMaxZ);

            G3D::Vector3 iterQueryMin = curStartBoxMin;
            G3D::Vector3 iterQueryMax = curStartBoxMax;
            // Merge start, end, half AABBs
            iterQueryMin.x = std::min({iterQueryMin.x, curEndBoxMin.x, curHalfMin.x});
            iterQueryMin.y = std::min({iterQueryMin.y, curEndBoxMin.y, curHalfMin.y});
            iterQueryMin.z = std::min({iterQueryMin.z, curEndBoxMin.z, curHalfMin.z});
            iterQueryMax.x = std::max({iterQueryMax.x, curEndBoxMax.x, curHalfMax.x});
            iterQueryMax.y = std::max({iterQueryMax.y, curEndBoxMax.y, curHalfMax.y});
            iterQueryMax.z = std::max({iterQueryMax.z, curEndBoxMax.z, curHalfMax.z});

            // Update shared slideContacts so resolveWallSlide uses fresh terrain data
            slideContacts.clear();
            SceneQuery::TestTerrainAABB(input.mapId, iterQueryMin, iterQueryMax, slideContacts);

            G3D::Vector3 iterResolved = currentMoveVec;
            G3D::Vector3 iterWallNormal(0, 0, 1);
            float iterBlocked = 1.0f;

            const G3D::Vector3 currentPosition(currentX, currentY, startZ);
            bool iterHit = resolveWallSlide(currentPosition, currentMoveVec, iterResolved, iterWallNormal, iterBlocked);

            totalResolvedMove = totalResolvedMove + iterResolved;
            if (iterHit) {
                wallNormal = iterWallNormal;
                blockedFraction = iterBlocked;
            }

            // Advance current position (binary: self->pos += curDir * hitDist)
            currentX += iterResolved.x;
            currentY += iterResolved.y;

            float iterXYDist = std::sqrt(iterResolved.x * iterResolved.x + iterResolved.y * iterResolved.y);
            accumulatedDist += iterXYDist;

            float leftover = originalDist - accumulatedDist;

            if (!iterHit)
                break;  // No wall hit — done

            // Binary exit: remaining2D >= 1.0 resets smallRemainCount; else increment
            if (leftover >= 1.0f) {
                smallRemainCount = 1;
            } else {
                smallRemainCount++;
                if (smallRemainCount > 5)
                    break;  // Binary safety exit
            }

            if (leftover < 1.0f)
                break;  // Less than 1 yard remaining — done

            // Prepare next iteration with remaining distance in slide direction
            float resolvedLen = std::sqrt(iterResolved.x * iterResolved.x + iterResolved.y * iterResolved.y);
            if (resolvedLen < 1e-6f)
                break;  // No progress

            float scale = leftover / resolvedLen;
            currentMoveVec = G3D::Vector3(iterResolved.x * scale, iterResolved.y * scale, 0.0f);
        }

        resolvedMove = totalResolvedMove;
    }

    bool hitWall = blockedFraction < 0.99f;
    endX = startX + resolvedMove.x;
    endY = startY + resolvedMove.y;
    st.wallBlockedFraction = blockedFraction;
    const float predictedSupportZ = std::max(
        startZ - PhysicsConstants::STEP_DOWN_HEIGHT,
        std::min(startZ + stepH + stepUp, startZ + resolvedMove.z));

    G3D::Vector3 finalBoxMin(endX - skin, endY - skin, adjustedMinZ);
    G3D::Vector3 finalBoxMax(endX + skin, endY + skin, adjustedMaxZ);
    std::vector<SceneQuery::AABBContact> contacts;
    SceneQuery::TestTerrainAABB(input.mapId, finalBoxMin, finalBoxMax, contacts);

    auto isStatefulSupportWalkable = [&](const SceneQuery::AABBContact& contact, float queryZ) -> bool {
        if (contact.walkable) {
            return true;
        }

        if (!st.groundedWallState) {
            return false;
        }

        WoWCollision::CheckWalkableResult walkableResult = WoWCollision::CheckWalkable(
            contact,
            G3D::Vector3(endX, endY, queryZ),
            radius,
            height,
            true,
            st.groundedWallState);
        return walkableResult.walkable;
    };

    auto resolveSupportContact = [&](float supportZ, G3D::Vector3& outNormal,
        const SceneQuery::AABBContact*& outSupport) -> bool {
        const float minGroundZ = startZ - PhysicsConstants::STEP_DOWN_HEIGHT;
        const float maxGroundZ = startZ + stepH + stepUp;
        const SceneQuery::AABBContact* bestSupport = nullptr;
        float bestZError = FLT_MAX;
        float bestNormalZ = -FLT_MAX;

        for (const auto& c : contacts) {
            if (!isStatefulSupportWalkable(c, supportZ))
                continue;
            if (c.point.z < minGroundZ || c.point.z > maxGroundZ)
                continue;

            const float zError = std::fabs(c.point.z - supportZ);
            const float normalZ = std::fabs(c.normal.z);
            bool better = false;
            if (!bestSupport) {
                better = true;
            }
            else if (zError < bestZError - 1e-4f) {
                better = true;
            }
            else if (std::fabs(zError - bestZError) <= 1e-4f) {
                if (normalZ > bestNormalZ + 1e-4f)
                    better = true;
                else if (std::fabs(normalZ - bestNormalZ) <= 1e-4f && c.point.z > bestSupport->point.z)
                    better = true;
            }

            if (better) {
                bestSupport = &c;
                bestZError = zError;
                bestNormalZ = normalZ;
            }
        }

        if (!bestSupport)
            return false;

        outNormal = bestSupport->normal.directionOrZero();
        if (outNormal.z < 0.0f)
            outNormal = -outNormal;
        outSupport = bestSupport;
        return true;
    };

    // Ground Z selection — binary-backed approach (0x635600 / 0x636100).
    // WoW.exe uses the AABB contacts from TestTerrainAABB as the PRIMARY ground
    // source, selecting the walkable contact closest to the predicted support Z.
    // GetGroundZ is only used as a FALLBACK when no AABB contact is found.
    // This prevents Z oscillation on multi-level terrain (Valley of Trials)
    // where GetGroundZ can bounce between terrain layers frame-to-frame.
    float bestGroundZ = -FLT_MAX;
    G3D::Vector3 bestNormal(0, 0, 1);
    const SceneQuery::AABBContact* bestSupportContact = nullptr;
    bool foundGround = false;

    // PRIMARY: select best walkable AABB contact closest to predictedSupportZ.
    // The binary's 0x636100 selected-plane logic picks from the TestTerrainAABB
    // contact set using the same Z-error + normal-Z ranking. predictedSupportZ
    // provides better slope tracking than startZ alone.
    {
        const float minGroundZ = startZ - PhysicsConstants::STEP_DOWN_HEIGHT;
        const float maxGroundZ = startZ + stepH + stepUp;
        float bestZError = FLT_MAX;
        float bestNormalZ = -FLT_MAX;

        for (const auto& c : contacts) {
            if (!isStatefulSupportWalkable(c, predictedSupportZ))
                continue;
            if (c.point.z < minGroundZ || c.point.z > maxGroundZ)
                continue;

            const float zError = std::fabs(c.point.z - predictedSupportZ);
            const float normalZ = std::fabs(c.normal.z);
            bool better = false;
            if (!foundGround) {
                better = true;
            }
            else if (zError < bestZError - 1e-4f) {
                better = true;
            }
            else if (std::fabs(zError - bestZError) <= 1e-4f) {
                if (normalZ > bestNormalZ + 1e-4f)
                    better = true;
                else if (std::fabs(normalZ - bestNormalZ) <= 1e-4f && c.point.z > bestGroundZ)
                    better = true;
            }

            if (better) {
                bestGroundZ = c.point.z;
                bestNormal = c.normal.directionOrZero();
                if (bestNormal.z < 0.0f) bestNormal = -bestNormal;
                bestSupportContact = &c;
                bestZError = zError;
                bestNormalZ = normalZ;
                foundGround = true;
            }
        }
    }

    // FALLBACK: if no AABB contact was found, use GetGroundZ (barycentric
    // interpolation on the exact ADT/VMAP triangle mesh).
    if (!foundGround) {
        const float supportQueryZ = predictedSupportZ + stepUp + PhysicsConstants::COLLISION_SKIN_EPSILON;
        float groundZ = SceneQuery::GetGroundZ(input.mapId, endX, endY,
            supportQueryZ,
            stepH + stepUp + PhysicsConstants::STEP_DOWN_HEIGHT);
        if (VMAP::IsValidHeight(groundZ) &&
            groundZ >= startZ - PhysicsConstants::STEP_DOWN_HEIGHT &&
            groundZ <= startZ + stepH + stepUp) {
            bestGroundZ = groundZ;
            foundGround = true;
            (void)resolveSupportContact(bestGroundZ, bestNormal, bestSupportContact);
        }
    }

    if (!foundGround) {
        // No ground at end position — enter freefall
        st.x = endX;
        st.y = endY;
        st.isGrounded = false;
        st.vz = PhysicsConstants::FALL_START_VELOCITY;
        st.vx = dirN.x * moveSpeed;
        st.vy = dirN.y * moveSpeed;
        return;
    }

    // Step height adjustment already built into AABB bounds (step 4).
    // bestGroundZ is the exact surface Z from barycentric interpolation.

    // Step 6: Wall response has already been resolved above using the
    // client-style contact-plane projection path.

    // But limit actual XY displacement to intendedDist (don't overshoot)
    {
        float dx = endX - startX, dy = endY - startY;
        float actualDist = std::sqrt(dx * dx + dy * dy);
        if (actualDist > intendedDist && intendedDist > 0) {
            float scale = intendedDist / actualDist;
            endX = startX + dx * scale;
            endY = startY + dy * scale;
            // Re-query AABB contacts at clamped position for consistent ground Z
            G3D::Vector3 clampedBoxMin(endX - skin, endY - skin, adjustedMinZ);
            G3D::Vector3 clampedBoxMax(endX + skin, endY + skin, adjustedMaxZ);
            std::vector<SceneQuery::AABBContact> clampedContacts;
            SceneQuery::TestTerrainAABB(input.mapId, clampedBoxMin, clampedBoxMax, clampedContacts);
            const float minGZ = startZ - PhysicsConstants::STEP_DOWN_HEIGHT;
            const float maxGZ = startZ + stepH + stepUp;
            float clampBestErr = FLT_MAX;
            for (const auto& c : clampedContacts) {
                if (!c.walkable) continue;
                if (c.point.z < minGZ || c.point.z > maxGZ) continue;
                float err = std::fabs(c.point.z - predictedSupportZ);
                if (err < clampBestErr) {
                    clampBestErr = err;
                    bestGroundZ = c.point.z;
                    bestNormal = c.normal.directionOrZero();
                    if (bestNormal.z < 0.0f) bestNormal = -bestNormal;
                }
            }
            if (clampBestErr == FLT_MAX) {
                // Fallback to GetGroundZ if no AABB contact found at clamped pos
                float clampedZ = SceneQuery::GetGroundZ(input.mapId, endX, endY,
                    startZ + stepH, stepH + PhysicsConstants::STEP_DOWN_HEIGHT);
                if (VMAP::IsValidHeight(clampedZ)) {
                    bestGroundZ = clampedZ;
                }
            }
        }
    }

    // Binary-backed chooser probe (0x635F80): multi-level terrain disambiguator.
    // When the selected ground Z jumps significantly between frames on flat-ish
    // terrain, it means the AABB query picked a contact from a different terrain
    // layer. The chooser probe checks alignment between the movement direction
    // and the Z delta. If misaligned (Z jumping while moving horizontally), it
    // clamps the Z change to maintain layer continuity.
    //
    // Constants from binary: kWoWChooserAlignmentDotMin = cos(10°) = 0.9848
    {
        const float zDelta = bestGroundZ - startZ;
        const float horizontalDist = std::sqrt((endX - startX) * (endX - startX) +
                                                (endY - startY) * (endY - startY));

        // Only probe when Z jumps more than step height on primarily horizontal movement
        if (std::fabs(zDelta) > stepH && horizontalDist > PhysicsConstants::VECTOR_EPSILON) {
            // Compute the 3D movement direction and check if Z component is consistent
            // with the slope. On genuinely sloped terrain, |zDelta/horizontalDist| matches
            // the terrain slope. On multi-layer terrain, the ratio is extreme.
            const float slopeRatio = std::fabs(zDelta) / horizontalDist;
            const float maxAllowedSlope = PhysicsConstants::WALKABLE_TAN_MAX_SLOPE * 1.5f; // 50° * 1.5 = generous

            if (slopeRatio > maxAllowedSlope) {
                // Z jump is too steep for the horizontal movement — wrong terrain layer.
                // Clamp to the maximum walkable slope from startZ.
                const float maxZChange = horizontalDist * PhysicsConstants::WALKABLE_TAN_MAX_SLOPE;
                if (zDelta > 0.0f) {
                    bestGroundZ = std::min(bestGroundZ, startZ + maxZChange);
                } else {
                    bestGroundZ = std::max(bestGroundZ, startZ - maxZChange);
                }
            }
        }
    }

    // Step 8: Commit position
    st.x = endX;
    st.y = endY;
    st.z = bestGroundZ;
    st.isGrounded = true;
    st.vz = 0.0f;
    st.vx = dirN.x * moveSpeed;
    st.vy = dirN.y * moveSpeed;
    st.groundNormal = bestNormal;

    // Binary 0x635600 tail: reset groundedWallState when landing on a clearly
    // walkable surface. The flag should only persist across frames when the
    // character is actively traversing a non-walkable slope (gate return 2 path).
    // Without this reset, the flag accumulates and causes isStatefulSupportWalkable
    // to accept contacts from wrong terrain layers.
    if (bestSupportContact && bestSupportContact->walkable) {
        st.groundedWallState = false;
    }
    st.supportInstanceId = 0;
    st.supportLocalPoint = G3D::Vector3(0, 0, 0);

    if (bestSupportContact && bestSupportContact->instanceId != 0) {
        auto* dynReg = DynamicObjectRegistry::Instance();
        G3D::Vector3 localSupport;
        if (dynReg && dynReg->TryGetLocalPoint(bestSupportContact->instanceId, bestSupportContact->point, localSupport)) {
            st.supportInstanceId = bestSupportContact->instanceId;
            st.supportLocalPoint = localSupport;
        }
    }

    if (hitWall) {
        st.wallHit = true;
        st.wallHitNormal = wallNormal;
    }
}

// [DELETED] Old 3-pass system (DecomposeMovement, ExecuteUp/Side/Down, PerformThreePassMove, GroundMoveElevatedSweep)
// Replaced by the grounded CollisionStepWoW helper; StepV2 remains responsible
// for the top-level 0x633840 branch order.


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
		out.groundedWallState = input.groundedWallState;
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
		out.groundedWallState = input.groundedWallState;
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
	st.groundedWallState = input.groundedWallState != 0;
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
	// WoW.exe 0x633A29 checks the airborne helper before the swim helper.
	// Preserve that precedence when both states are present on the same frame,
	// while still allowing non-swimming unsupported states to fall through to air.
	const bool useAirbornePath = inputAirborneFlag || (!st.isGrounded && !isSwimming);
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
		// EXCEPTION: if the character has FALLINGFAR (not JUMPING) and the pre-move
		// ground probe found a walkable contact, let the grounded state stand.
		// This prevents single-frame ground misses from locking into FALLINGFAR
		// oscillation on multi-level terrain where the capsule sweep intermittently
		// misses the correct layer.
		const bool isJumping = (input.moveFlags & MOVEFLAG_JUMPING) != 0;
		if (preserveAirborne && (isJumping || !st.isGrounded)) {
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
		input.moveFlags, st.orientation,
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
	else if (useAirbornePath) {
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
		// Ground movement — WoW.exe-style CollisionStep.
		if (trustGroundedReplay && intendedDist > 0.0f) {
			// Replay calibration path: run full ground sweep for step/slope Z behavior,
			// then re-lock X/Y to the trusted capture displacement.
			const float trustedX = st.x + (input.vx * dt);
			const float trustedY = st.y + (input.vy * dt);
			st.vx = input.vx;
			st.vy = input.vy;
			st.vz = 0.0f;

			CollisionStepWoW(input, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);

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
			// WoW.exe-style ground collision (replaces 3-pass UP→SIDE→DOWN)
			MovementState preMove = st;
			CollisionStepWoW(input, intent, st, r, h, moveDir, intendedDist, dt, moveSpeed);

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

			// Walk experiment DELETED — WoW.exe (0x633840) doesn't have one.
			// CollisionStepWoW handles step height + walkability directly.
		} else {
			// Idle while grounded: query terrain at current position.
			// WoW.exe doesn't do special idle processing — the character stays
			// at its current position. If ground vanishes, the next frame's
			// mode check (isGrounded) will route to air movement.
			float idleGroundZ = SceneQuery::GetGroundZ(input.mapId, st.x, st.y,
				st.z + PhysicsConstants::STEP_HEIGHT,
				PhysicsConstants::STEP_HEIGHT + PhysicsConstants::STEP_DOWN_HEIGHT);
			if (VMAP::IsValidHeight(idleGroundZ) &&
				idleGroundZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT &&
				idleGroundZ <= st.z + PhysicsConstants::STEP_HEIGHT) {
				st.z = idleGroundZ;
				st.isGrounded = true;
				st.vz = 0.0f;
			}
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

	// False-airborne rescue: only for REPLAY calibration mode.
	// Live mode uses AABB CollisionStepWoW which doesn't have false-airborne issues.
	// Replay mode needs this because recordings were captured with the real WoW.exe
	// client which has different ground detection geometry.
	if (!st.isGrounded && !isSwimming && !inputAirborneFlag && trustGroundedReplayInput) {
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
	const bool enteredWaterThisFrame = finalLiq.isSwimming && !isSwimming;
	if (enteredWaterThisFrame) {
		if (finalLiq.hasLevel) {
			st.z = std::max(st.z, finalLiq.level - PhysicsConstants::WATER_LEVEL_DELTA);
		}
		// WoW.exe applies 0.5x horizontal velocity damping when crossing into a
		// swimming state. Keep the damped state in the output velocity, not only
		// in the carried state, so the next frame observes the same transition.
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
		if (enteredWaterThisFrame) {
			actualV.x = st.vx;
			actualV.y = st.vy;
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
	// Static terrain / stair support must come from current-frame collision.
	// Keep these bridge fields inert for wire compatibility; WoW.exe evidence still
	// only supports persisted moving-base continuity, not a synthetic terrain hold.
	out.stepUpBaseZ = PhysicsConstants::INVALID_HEIGHT;
	out.stepUpAge = 0;

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

	// Ground contact persistence REMOVED — CollisionStepWoW uses AABB terrain
	// test with barycentric Z interpolation. No false-airborne rescue needed.

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
	out.groundedWallState = st.groundedWallState ? 1u : 0u;

	out.standingOnInstanceId = st.supportInstanceId;
	out.standingOnLocalX = st.supportLocalPoint.x;
	out.standingOnLocalY = st.supportLocalPoint.y;
	out.standingOnLocalZ = st.supportLocalPoint.z;
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
