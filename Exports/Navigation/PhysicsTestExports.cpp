// PhysicsTestExports.cpp - C exports for physics testing from managed code
// These functions expose the internal physics primitives for unit testing.

#include "PhysicsEngine.h"
#include "SceneQuery.h"
#include "SceneCache.h"
#include "CapsuleCollision.h"
#include "MapLoader.h"
#include "CoordinateTransforms.h"
#include "DynamicObjectRegistry.h"
#include "WmoDoodadFormat.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "StaticMapTree.h"
#include "WorldModel.h"
#include <cstring>
#include <cstdlib>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <algorithm>
#include <cmath>
#include <sstream>
#define NOMINMAX
#include <windows.h>
#include <filesystem>

// Global instances for testing
static MapLoader* g_testMapLoader = nullptr;

struct ExportTriangle
{
    G3D::Vector3 a;
    G3D::Vector3 b;
    G3D::Vector3 c;
};

struct ExportAABBContact
{
    G3D::Vector3 point;
    G3D::Vector3 normal;
    G3D::Vector3 rawNormal;
    G3D::Vector3 triangleA;
    G3D::Vector3 triangleB;
    G3D::Vector3 triangleC;
    float planeDistance;
    float distance;
    uint32_t instanceId;
    uint32_t sourceType;
    uint32_t walkable;
};

struct ExportSelectorSupportPlane
{
    G3D::Vector3 normal;
    float planeDistance;
};

struct ExportSelectorCandidateValidationTrace
{
    float inputBestRatio;
    float candidateBestRatio;
    float outputBestRatio;
    uint32_t firstPassAllBelowLooseThreshold;
    uint32_t rebuildExecuted;
    uint32_t rebuildSucceeded;
    uint32_t secondPassAllBelowStrictThreshold;
    uint32_t improvedBestRatio;
    uint32_t finalStripCount;
};

struct ExportSelectorCandidateRecord
{
    ExportSelectorSupportPlane filterPlane;
    G3D::Vector3 point0;
    G3D::Vector3 point1;
    G3D::Vector3 point2;
};

struct ExportSelectorRecordEvaluationTrace
{
    float inputBestRatio;
    float outputBestRatio;
    float selectedBestRatio;
    uint32_t recordCount;
    uint32_t dotRejectedCount;
    uint32_t clipRejectedCount;
    uint32_t validationRejectedCount;
    uint32_t validationAcceptedCount;
    uint32_t updatedBestRatio;
    uint32_t selectedRecordIndex;
    uint32_t selectedStripCount;
};

struct ExportSelectorSourceRankingTrace
{
    float inputBestRatio;
    float outputBestRatio;
    uint32_t dotRejectedCount;
    uint32_t builderRejectedCount;
    uint32_t evaluatorRejectedCount;
    uint32_t acceptedSourceCount;
    uint32_t overwriteCount;
    uint32_t appendCount;
    uint32_t bestRatioUpdatedCount;
    uint32_t swappedBestToFront;
    uint32_t finalCandidateCount;
    uint32_t selectedSourceIndex;
};

struct ExportSelectorDirectionRankingTrace
{
    float inputBestRatio;
    float outputBestRatio;
    float reportedBestRatio;
    uint32_t dotRejectedCount;
    uint32_t builderRejectedCount;
    uint32_t evaluatorRejectedCount;
    uint32_t acceptedDirectionCount;
    uint32_t overwriteCount;
    uint32_t appendCount;
    uint32_t bestRatioUpdatedCount;
    uint32_t swappedBestToFront;
    uint32_t zeroClampedOutput;
    uint32_t finalCandidateCount;
    uint32_t selectedDirectionIndex;
    uint32_t selectedRecordIndex;
};

struct ExportGroundedWallSelectionTrace
{
    uint32_t queryContactCount;
    uint32_t candidateCount;
    uint32_t selectedContactIndex;
    uint32_t selectedInstanceId;
    uint32_t selectedSourceType;
    uint32_t rawWalkable;
    uint32_t walkableWithoutState;
    uint32_t walkableWithState;
    uint32_t groundedWallStateBefore;
    uint32_t groundedWallStateAfter;
    uint32_t usedPositionReorientation;
    uint32_t usedWalkableSelectedContact;
    uint32_t usedNonWalkableVertical;
    uint32_t usedUphillDiscard;
    uint32_t usedPrimaryAxisFallback;
    uint32_t branchKind;
    G3D::Vector3 selectedPoint;
    G3D::Vector3 selectedNormal;
    G3D::Vector3 orientedNormal;
    G3D::Vector3 primaryAxis;
    G3D::Vector3 mergedWallNormal;
    G3D::Vector3 finalWallNormal;
    G3D::Vector3 horizontalProjectedMove;
    G3D::Vector3 branchProjectedMove;
    G3D::Vector3 finalProjectedMove;
    float rawOpposeScore;
    float orientedOpposeScore;
    float requested2D;
    float horizontalResolved2D;
    float slopedResolved2D;
    float finalResolved2D;
    float blockedFraction;
    uint32_t selectedInstanceFlags;
    uint32_t selectedModelFlags;
    uint32_t selectedGroupFlags;
    int32_t selectedRootId;
    int32_t selectedGroupId;
    uint32_t selectedGroupMatchFound;
    uint32_t selectedResolvedModelFlags;
    uint32_t selectedMetadataSource;
    uint32_t selectedCurrentPositionInsidePrism;
    uint32_t selectedProjectedPositionInsidePrism;
    uint32_t selectedThresholdSensitiveStandard;
    uint32_t selectedThresholdSensitiveRelaxed;
    uint32_t selectedWouldUseDirectPairStandard;
    uint32_t selectedWouldUseDirectPairRelaxed;
    G3D::Vector3 selectedThresholdPoint;
    float selectedThresholdNormalZ;
};

static float ComputeHorizontalOpposeScore(const G3D::Vector3& normal, const G3D::Vector3& moveDir2D)
{
    G3D::Vector3 horizontal(normal.x, normal.y, 0.0f);
    const float horizontalMag = horizontal.magnitude();
    if (horizontalMag <= PhysicsConstants::VECTOR_EPSILON)
        return 0.0f;

    horizontal = horizontal * (1.0f / horizontalMag);
    return std::max(0.0f, -horizontal.dot(moveDir2D));
}

struct ResolvedStaticContactMetadata
{
    uint32_t instanceFlags = 0u;
    uint32_t modelFlags = 0u;
    uint32_t groupFlags = 0u;
    int32_t rootId = -1;
    int32_t groupId = -1;
    bool groupMatchFound = false;
    uint32_t resolvedModelFlags = 0u;
    uint32_t metadataSource = 0u;
};

enum ResolvedContactMetadataSource : uint32_t
{
    RESOLVED_CONTACT_METADATA_NONE = 0u,
    RESOLVED_CONTACT_METADATA_PARENT_INSTANCE = 1u,
    RESOLVED_CONTACT_METADATA_WMO_GROUP = 2u,
    RESOLVED_CONTACT_METADATA_WMO_DOODAD = 3u,
};

static const VMAP::ModelInstance* FindStaticModelInstance(uint32_t mapId, uint32_t instanceId)
{
    if (instanceId == 0u)
        return nullptr;

    auto* vmapMgr = static_cast<VMAP::VMapManager2*>(VMAP::VMapFactory::createOrGetVMapManager());
    if (!vmapMgr)
        return nullptr;

    if (!vmapMgr->isMapInitialized(mapId))
        vmapMgr->initializeMap(mapId);
    const VMAP::StaticMapTree* mapTree = vmapMgr->GetStaticMapTree(mapId);
    if (!mapTree || !mapTree->GetInstancesPtr())
        return nullptr;

    const VMAP::ModelInstance* instances = mapTree->GetInstancesPtr();
    const uint32_t instanceCount = mapTree->GetInstanceCount();
    for (uint32_t i = 0; i < instanceCount; ++i)
    {
        if (instances[i].ID == instanceId)
            return &instances[i];
    }

    return nullptr;
}

static bool NearlyEqualVertex(const G3D::Vector3& lhs, const G3D::Vector3& rhs, float epsilon)
{
    return (lhs - rhs).squaredMagnitude() <= (epsilon * epsilon);
}

static bool MatchTriangleVerticesUnordered(const G3D::Vector3& a,
                                          const G3D::Vector3& b,
                                          const G3D::Vector3& c,
                                          const G3D::Vector3& v0,
                                          const G3D::Vector3& v1,
                                          const G3D::Vector3& v2,
                                          float epsilon)
{
    const G3D::Vector3 expected[3] = { a, b, c };
    const G3D::Vector3 actual[3] = { v0, v1, v2 };
    bool used[3] = { false, false, false };

    for (const auto& wanted : expected)
    {
        bool matched = false;
        for (int i = 0; i < 3; ++i)
        {
            if (used[i])
                continue;
            if (!NearlyEqualVertex(wanted, actual[i], epsilon))
                continue;

            used[i] = true;
            matched = true;
            break;
        }

        if (!matched)
            return false;
    }

    return true;
}

static ResolvedStaticContactMetadata ResolveStaticContactMetadata(uint32_t mapId, const SceneQuery::AABBContact& contact)
{
    ResolvedStaticContactMetadata metadata{};
    metadata.instanceFlags = contact.instanceFlags;
    metadata.modelFlags = contact.modelFlags;
    metadata.groupFlags = contact.groupFlags;
    metadata.rootId = contact.rootId;
    metadata.groupId = contact.groupId;
    metadata.groupMatchFound = contact.groupId != -1;

    if (contact.groupId != -1)
    {
        metadata.resolvedModelFlags = (contact.modelFlags != 0u) ? contact.modelFlags : metadata.resolvedModelFlags;
        metadata.metadataSource = RESOLVED_CONTACT_METADATA_WMO_GROUP;
    }

    if (contact.sourceType == 2u)
    {
        metadata.resolvedModelFlags = VMAP::MOD_M2;
        metadata.metadataSource = RESOLVED_CONTACT_METADATA_WMO_DOODAD;
    }

    const VMAP::ModelInstance* instance = FindStaticModelInstance(mapId, contact.instanceId);

    if (!instance)
        return metadata;

    if (metadata.instanceFlags == 0u)
        metadata.instanceFlags = instance->flags;
    if (metadata.metadataSource == RESOLVED_CONTACT_METADATA_NONE)
        metadata.metadataSource = RESOLVED_CONTACT_METADATA_PARENT_INSTANCE;
    if (!instance->iModel)
        return metadata;

    if (metadata.modelFlags == 0u)
        metadata.modelFlags = instance->iModel->getModelFlags();
    if (metadata.resolvedModelFlags == 0u)
        metadata.resolvedModelFlags = metadata.modelFlags;
    if (metadata.rootId == -1)
        metadata.rootId = static_cast<int32_t>(instance->iModel->GetRootWmoId());

    if (contact.sourceType == 2u || metadata.groupMatchFound || (instance->flags & VMAP::MOD_M2))
        return metadata;

    const auto worldToLocal = [&](const G3D::Vector3& worldPoint) -> G3D::Vector3
    {
        const G3D::Vector3 internalPoint = NavCoord::WorldToInternal(worldPoint);
        return instance->iInvRot * (internalPoint - instance->iPos) * instance->iInvScale;
    };

    const G3D::Vector3 localA = worldToLocal(contact.triangleA);
    const G3D::Vector3 localB = worldToLocal(contact.triangleB);
    const G3D::Vector3 localC = worldToLocal(contact.triangleC);
    constexpr float localVertexMatchEpsilon = 1.0e-3f;

    const uint32_t groupCount = instance->iModel->GetGroupModelCount();
    for (uint32_t groupIndex = 0; groupIndex < groupCount; ++groupIndex)
    {
        const VMAP::GroupModel* group = instance->iModel->GetGroupModel(groupIndex);
        if (!group)
            continue;

        const auto& vertices = group->GetVertices();
        const auto& triangles = group->GetTriangles();
        for (const auto& triangle : triangles)
        {
            const G3D::Vector3& v0 = vertices[triangle.idx0];
            const G3D::Vector3& v1 = vertices[triangle.idx1];
            const G3D::Vector3& v2 = vertices[triangle.idx2];
            if (!MatchTriangleVerticesUnordered(localA, localB, localC, v0, v1, v2, localVertexMatchEpsilon))
                continue;

            metadata.groupFlags = group->GetMogpFlags();
            metadata.groupId = static_cast<int32_t>(group->GetWmoID());
            metadata.groupMatchFound = true;
            metadata.metadataSource = RESOLVED_CONTACT_METADATA_WMO_GROUP;
            return metadata;
        }
    }

    return metadata;
}

extern "C"
{
    // ==========================================================================
    // PHYSICS ENGINE LIFECYCLE
    // ==========================================================================

    __declspec(dllexport) bool InitializePhysics()
    {
        try
        {
            PhysicsEngine::Instance()->Initialize();
            SceneQuery::Initialize();

            // Auto-load displayId→model mapping for dynamic objects
            // Use Win32 GetEnvironmentVariableA (not _dupenv_s) — see SceneQuery.cpp comment
            std::string dataRoot;
            {
                char buf[512] = {0};
                DWORD len = GetEnvironmentVariableA("WWOW_DATA_DIR", buf, sizeof(buf));
                if (len > 0 && len < sizeof(buf))
                {
                    dataRoot = buf;
                    if (!dataRoot.empty() && dataRoot.back() != '/' && dataRoot.back() != '\\')
                        dataRoot += '/';
                }
            }
            std::vector<std::string> vps;
            if (!dataRoot.empty())
                vps.push_back(dataRoot + "vmaps/");
            vps.push_back("vmaps/");
            for (auto& vp : vps)
            {
                if (std::filesystem::exists(vp))
                {
                    DynamicObjectRegistry::Instance()->LoadDisplayIdMapping(vp);
                    break;
                }
            }

            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    __declspec(dllexport) void ShutdownPhysics()
    {
        try
        {
            PhysicsEngine::Destroy();
            if (g_testMapLoader)
            {
                g_testMapLoader->Shutdown();
                delete g_testMapLoader;
                g_testMapLoader = nullptr;
            }
        }
        catch (...) {}
    }

    __declspec(dllexport) PhysicsOutput StepPhysicsV2(const PhysicsInput* input, float dt)
    {
        if (!input)
        {
            PhysicsOutput empty{};
            return empty;
        }
        return PhysicsEngine::Instance()->StepV2(*input, dt);
    }

    // ==========================================================================
    // MAP/TERRAIN FUNCTIONS
    // ==========================================================================

    __declspec(dllexport) bool InitializeMapLoader(const char* dataPath)
    {
        try
        {
            if (!g_testMapLoader)
            {
                g_testMapLoader = new MapLoader();
            }
            bool ok = g_testMapLoader->Initialize(dataPath ? dataPath : "maps/");
            if (ok)
            {
                // Inject into SceneQuery so GetGroundZ / SweepCapsule have ADT data
                SceneQuery::SetMapLoader(g_testMapLoader);
            }
            return ok;
        }
        catch (...)
        {
            return false;
        }
    }

    __declspec(dllexport) bool LoadMapTile(uint32_t mapId, uint32_t tileX, uint32_t tileY)
    {
        if (!g_testMapLoader)
            return false;
        return g_testMapLoader->LoadMapTile(mapId, tileX, tileY);
    }

    __declspec(dllexport) float GetTerrainHeight(uint32_t mapId, float x, float y)
    {
        if (!g_testMapLoader)
            return MapFormat::INVALID_HEIGHT;
        return g_testMapLoader->GetHeight(mapId, x, y);
    }

    /// Gets the combined ground Z (VMAP + ADT) at a position.
    /// Queries both WMO/M2 model geometry and ADT terrain, returns highest walkable surface <= z + 0.5.
    __declspec(dllexport) float GetGroundZ(uint32_t mapId, float x, float y, float z, float maxSearchDist)
    {
        return SceneQuery::GetGroundZ(mapId, x, y, z, maxSearchDist);
    }

    /// Diagnostic: bypass scene cache and query VMAP ray + ADT + BIH directly.
    /// Forces VMAP initialization if not already loaded. Returns ground Z from raw VMAP data.
    /// outVmapZ/outAdtZ/outBihZ receive per-source results (-200000 = not found).
    __declspec(dllexport) float GetGroundZBypassCache(
        uint32_t mapId, float x, float y, float z, float maxSearchDist,
        float* outVmapZ, float* outAdtZ, float* outBihZ, float* outSceneCacheZ)
    {
        auto* vmapMgr = static_cast<VMAP::VMapManager2*>(VMAP::VMapFactory::createOrGetVMapManager());

        // Scene cache result (current behavior)
        float sceneZ = PhysicsConstants::INVALID_HEIGHT;
        auto* cache = SceneQuery::GetSceneCache(mapId);
        if (cache)
            sceneZ = cache->GetGroundZ(x, y, z, maxSearchDist);
        if (outSceneCacheZ) *outSceneCacheZ = sceneZ;

        // Force VMAP initialization (may take 30-60s on first call)
        if (vmapMgr && !vmapMgr->isMapInitialized(mapId))
            vmapMgr->initializeMap(mapId);

        // 1. VMAP ray (model geometry — WMO/M2)
        float vmapZ = PhysicsConstants::INVALID_HEIGHT;
        if (vmapMgr && vmapMgr->isMapInitialized(mapId))
        {
            vmapZ = vmapMgr->getHeight(mapId, x, y, z, maxSearchDist);
            if (!std::isfinite(vmapZ)) vmapZ = PhysicsConstants::INVALID_HEIGHT;
        }
        if (outVmapZ) *outVmapZ = vmapZ;

        // Also try z+2 like MaNGOS does (GetHeightStatic uses z+2 as ray origin)
        float vmapZ2 = PhysicsConstants::INVALID_HEIGHT;
        if (vmapMgr && vmapMgr->isMapInitialized(mapId))
        {
            vmapZ2 = vmapMgr->getHeight(mapId, x, y, z + 2.0f, maxSearchDist);
            if (!std::isfinite(vmapZ2)) vmapZ2 = PhysicsConstants::INVALID_HEIGHT;
        }

        // 2. ADT terrain
        float adtZ = PhysicsConstants::INVALID_HEIGHT;
        if (g_testMapLoader && g_testMapLoader->IsInitialized())
        {
            float h = g_testMapLoader->GetTriangleZ(mapId, x, y);
            if (h > MapFormat::INVALID_HEIGHT + 1.0f) adtZ = h;
        }
        if (outAdtZ) *outAdtZ = adtZ;

        // 3. BIH overlap (for WMO interiors where ray misses)
        float bihZ = PhysicsConstants::INVALID_HEIGHT;
        if (vmapMgr && vmapMgr->isMapInitialized(mapId))
        {
            const VMAP::StaticMapTree* mapTree = vmapMgr->GetStaticMapTree(mapId);
            if (mapTree && mapTree->GetInstancesPtr() && mapTree->GetInstanceCount() > 0)
                bihZ = SceneQuery::GetGroundZByBIH(mapTree, x, y, z, maxSearchDist);
        }
        if (outBihZ) *outBihZ = bihZ;

        // Log all results for diagnostics
        fprintf(stderr, "[GroundZDiag] pos=(%.3f, %.3f, %.3f) scene=%.3f vmap=%.3f vmap(z+2)=%.3f adt=%.3f bih=%.3f\n",
                x, y, z, sceneZ, vmapZ, vmapZ2, adtZ, bihZ);
        fflush(stderr);

        // Return best of non-cached sources (closest to z)
        float bestZ = PhysicsConstants::INVALID_HEIGHT;
        float bestErr = std::numeric_limits<float>::max();
        auto consider = [&](float candidate) {
            if (candidate <= PhysicsConstants::INVALID_HEIGHT + 1.0f) return;
            if (candidate > z + maxSearchDist) return;
            if (candidate < z - maxSearchDist) return;
            float err = std::fabs(candidate - z);
            if (err < bestErr) { bestErr = err; bestZ = candidate; }
        };
        consider(vmapZ);
        consider(vmapZ2);
        consider(adtZ);
        consider(bihZ);
        return bestZ;
    }

    /// Diagnostic: returns info about VMAP state for a map.
    /// Returns: instanceCount in the StaticMapTree, or negative error codes.
    /// Also tries EnsureMapLoaded if not loaded, and logs basePath.
    __declspec(dllexport) int GetVmapDiagnostics(uint32_t mapId)
    {
        auto* vmapMgr = static_cast<VMAP::VMapManager2*>(VMAP::VMapFactory::createOrGetVMapManager());
        if (!vmapMgr) return -2;

        fprintf(stderr, "[VmapDiag] map=%u isInit=%d\n",
            mapId, vmapMgr->isMapInitialized(mapId) ? 1 : 0);
        fflush(stderr);

        // Try loading if not loaded
        if (!vmapMgr->isMapInitialized(mapId))
        {
            SceneQuery::EnsureMapLoaded(mapId);
            fprintf(stderr, "[VmapDiag] After EnsureMapLoaded: isInit=%d\n",
                vmapMgr->isMapInitialized(mapId) ? 1 : 0);
            fflush(stderr);
        }

        if (!vmapMgr->isMapInitialized(mapId))
            return -1;
        auto* mapTree = vmapMgr->GetStaticMapTree(mapId);
        if (!mapTree)
            return -3;
        return (int)mapTree->GetInstanceCount();
    }

    /// Diagnostic: enumerate ALL triangles from the scene cache at (x,y), returning their
    /// interpolated Z values. No acceptance-window filtering — shows ALL surfaces.
    /// Returns number of Z values written to outZValues (up to maxResults).
    /// Also writes instanceId to outInstanceIds if non-null.
    __declspec(dllexport) int EnumerateAllSurfacesAt(
        uint32_t mapId, float x, float y,
        float* outZValues, uint32_t* outInstanceIds, int maxResults)
    {
        auto* cache = SceneQuery::GetSceneCache(mapId);
        if (!cache || maxResults <= 0 || !outZValues) return 0;

        // Access the scene cache internals directly
        // We need the raw triangle data — use QueryTrianglesInAABB with a tiny box
        float pad = 0.01f; // tiny XY padding
        std::vector<CapsuleCollision::Triangle> tris;
        std::vector<uint32_t> instanceIds;
        cache->QueryTrianglesInAABB(x - pad, y - pad, x + pad, y + pad, tris, &instanceIds);

        int count = 0;
        for (size_t i = 0; i < tris.size() && count < maxResults; ++i)
        {
            const auto& t = tris[i];
            // Barycentric test: is (x,y) inside this triangle's XY projection?
            float v0x = t.c.x - t.a.x, v0y = t.c.y - t.a.y;
            float v1x = t.b.x - t.a.x, v1y = t.b.y - t.a.y;
            float v2x = x - t.a.x, v2y = y - t.a.y;
            float d00 = v0x * v0x + v0y * v0y;
            float d01 = v0x * v1x + v0y * v1y;
            float d02 = v0x * v2x + v0y * v2y;
            float d11 = v1x * v1x + v1y * v1y;
            float d12 = v1x * v2x + v1y * v2y;
            float denom = d00 * d11 - d01 * d01;
            if (std::fabs(denom) < 1e-12f) continue;
            float invDenom = 1.0f / denom;
            float u = (d11 * d02 - d01 * d12) * invDenom;
            float v = (d00 * d12 - d01 * d02) * invDenom;
            if (u < -1e-6f || v < -1e-6f || (u + v) > 1.0f + 1e-6f) continue;

            // Interpolate Z
            float triZ = t.a.z + u * (t.c.z - t.a.z) + v * (t.b.z - t.a.z);
            outZValues[count] = triZ;
            if (outInstanceIds) outInstanceIds[count] = (i < instanceIds.size()) ? instanceIds[i] : 0;
            count++;
        }

        return count;
    }

    // ==========================================================================
    // GEOMETRY QUERY FUNCTIONS
    // ==========================================================================

    __declspec(dllexport) int QueryTerrainTriangles(
        uint32_t mapId,
        float minX, float minY,
        float maxX, float maxY,
        MapFormat::TerrainTriangle* triangles,
        int maxTriangles)
    {
        if (!g_testMapLoader || !triangles || maxTriangles <= 0)
            return 0;

        std::vector<MapFormat::TerrainTriangle> tris;
        if (!g_testMapLoader->GetTerrainTriangles(mapId, minX, minY, maxX, maxY, tris))
            return 0;

        int count = static_cast<int>(std::min(tris.size(), static_cast<size_t>(maxTriangles)));
        std::memcpy(triangles, tris.data(), count * sizeof(MapFormat::TerrainTriangle));
        return count;
    }

    __declspec(dllexport) int SweepCapsule(
        uint32_t mapId,
        const CapsuleCollision::Capsule* capsule,
        const G3D::Vector3* direction,
        float distance,
        SceneHit* hits,
        int maxHits,
        const G3D::Vector3* playerForward)
    {
        if (!capsule || !direction || !hits || maxHits <= 0)
            return 0;

        std::vector<SceneHit> hitResults;
        int count = SceneQuery::SweepCapsule(
            mapId, *capsule, *direction, distance, hitResults,
            playerForward ? *playerForward : G3D::Vector3(1, 0, 0));

        count = std::min(count, maxHits);
        for (int i = 0; i < count; ++i)
        {
            hits[i] = hitResults[i];
        }
        return count;
    }

    __declspec(dllexport) int OverlapCapsule(
        uint32_t mapId,
        const CapsuleCollision::Capsule* capsule,
        SceneHit* overlaps,
        int maxOverlaps)
    {
        if (!capsule || !overlaps || maxOverlaps <= 0)
            return 0;

        auto* vmapMgr = static_cast<VMAP::VMapManager2*>(
            VMAP::VMapFactory::createOrGetVMapManager());
        if (!vmapMgr)
            return 0;

        if (!vmapMgr->isMapInitialized(mapId))
            SceneQuery::EnsureMapLoaded(mapId);

        if (!vmapMgr->isMapInitialized(mapId))
            return 0;

        const VMAP::StaticMapTree* mapTree = vmapMgr->GetStaticMapTree(mapId);
        if (!mapTree)
            return 0;

        std::vector<SceneHit> hitResults;
        int count = SceneQuery::OverlapCapsule(*mapTree, *capsule, hitResults);

        int copyCount = (count < maxOverlaps) ? count : maxOverlaps;
        for (int i = 0; i < copyCount; ++i)
            overlaps[i] = hitResults[i];

        return copyCount;
    }

    // ==========================================================================
    // PURE GEOMETRY TESTS (no map data needed)
    // ==========================================================================

    __declspec(dllexport) bool IntersectCapsuleTriangle(
        const CapsuleCollision::Capsule* capsule,
        const CapsuleCollision::Triangle* triangle,
        float* outDepth,
        G3D::Vector3* outNormal,
        G3D::Vector3* outPoint)
    {
        if (!capsule || !triangle)
            return false;

        CapsuleCollision::Hit hit;
        bool result = CapsuleCollision::intersectCapsuleTriangle(*capsule, *triangle, hit);

        if (result)
        {
            if (outDepth) *outDepth = hit.depth;
            if (outNormal) *outNormal = G3D::Vector3(hit.normal.x, hit.normal.y, hit.normal.z);
            if (outPoint) *outPoint = G3D::Vector3(hit.point.x, hit.point.y, hit.point.z);
        }

        return result;
    }

    __declspec(dllexport) bool SweepCapsuleTriangle(
        const CapsuleCollision::Capsule* capsule,
        const G3D::Vector3* velocity,
        const CapsuleCollision::Triangle* triangle,
        float* outToi,
        G3D::Vector3* outNormal,
        G3D::Vector3* outImpactPoint)
    {
        if (!capsule || !velocity || !triangle)
            return false;

        CapsuleCollision::Vec3 vel(velocity->x, velocity->y, velocity->z);
        float toi;
        CapsuleCollision::Vec3 normal, impactPoint;

        bool result = CapsuleCollision::capsuleTriangleSweep(
            *capsule, vel, *triangle, toi, normal, impactPoint);

        if (result)
        {
            if (outToi) *outToi = toi;
            if (outNormal) *outNormal = G3D::Vector3(normal.x, normal.y, normal.z);
            if (outImpactPoint) *outImpactPoint = G3D::Vector3(impactPoint.x, impactPoint.y, impactPoint.z);
        }

        return result;
    }

    // ==========================================================================
    // DIAGNOSTIC/CALIBRATION FUNCTIONS
    // ==========================================================================

    /// Returns physics constants for test validation
    __declspec(dllexport) void GetPhysicsConstants(
        float* gravity,
        float* jumpVelocity,
        float* stepHeight,
        float* stepDownHeight,
        float* walkableMinNormalZ)
    {
        if (gravity) *gravity = PhysicsConstants::GRAVITY;
        if (jumpVelocity) *jumpVelocity = PhysicsConstants::JUMP_VELOCITY;
        if (stepHeight) *stepHeight = PhysicsConstants::STEP_HEIGHT;
        if (stepDownHeight) *stepDownHeight = PhysicsConstants::STEP_DOWN_HEIGHT;
        if (walkableMinNormalZ) *walkableMinNormalZ = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    }

    __declspec(dllexport) bool EvaluateWoWCheckWalkable(
        const ExportTriangle* triangle,
        const G3D::Vector3* contactNormal,
        const G3D::Vector3* position,
        float collisionRadius,
        float boundingHeight,
        bool useStandardWalkableThreshold,
        bool groundedWallFlagBefore,
        bool* outWalkableState,
        bool* outGroundedWallFlagAfter)
    {
        if (!triangle || !contactNormal || !position) {
            if (outWalkableState) *outWalkableState = false;
            if (outGroundedWallFlagAfter) *outGroundedWallFlagAfter = false;
            return false;
        }

        SceneQuery::AABBContact contact{};
        contact.normal = contactNormal->directionOrZero();
        contact.rawNormal = *contactNormal;
        contact.triangleA = triangle->a;
        contact.triangleB = triangle->b;
        contact.triangleC = triangle->c;

        contact.planeDistance = contact.normal.magnitude() > PhysicsConstants::VECTOR_EPSILON
            ? -contact.normal.dot(contact.triangleA)
            : 0.0f;

        const auto result = WoWCollision::CheckWalkable(
            contact,
            *position,
            collisionRadius,
            boundingHeight,
            useStandardWalkableThreshold,
            groundedWallFlagBefore);

        if (outWalkableState) *outWalkableState = result.walkableState;
        if (outGroundedWallFlagAfter) *outGroundedWallFlagAfter = result.groundedWallFlagAfter;
        return result.walkable;
    }

    __declspec(dllexport) bool EvaluateTerrainAABBContactOrientation(
        const ExportTriangle* triangle,
        const G3D::Vector3* boxMin,
        const G3D::Vector3* boxMax,
        G3D::Vector3* outNormal,
        float* outPlaneDistance,
        bool* outWalkable)
    {
        if (!triangle || !boxMin || !boxMax) {
            if (outNormal) *outNormal = G3D::Vector3(0.0f, 0.0f, 0.0f);
            if (outPlaneDistance) *outPlaneDistance = 0.0f;
            if (outWalkable) *outWalkable = false;
            return false;
        }

        const G3D::Vector3 boxCenter = (*boxMin + *boxMax) * 0.5f;
        const auto contact = SceneQuery::BuildTerrainAABBContact(
            boxCenter,
            boxCenter,
            triangle->a,
            triangle->b,
            triangle->c,
            0.0f,
            0u,
            nullptr);

        if (outNormal) *outNormal = contact.normal;
        if (outPlaneDistance) *outPlaneDistance = contact.planeDistance;
        if (outWalkable) *outWalkable = contact.walkable;
        return true;
    }

    __declspec(dllexport) bool EvaluateWoWSelectedContactThresholdGate(
        const ExportTriangle* triangle,
        const G3D::Vector3* contactNormal,
        const G3D::Vector3* currentPosition,
        const G3D::Vector3* projectedPosition,
        bool useStandardWalkableThreshold,
        bool* outCurrentPositionInsidePrism,
        bool* outProjectedPositionInsidePrism,
        bool* outThresholdSensitive,
        float* outNormalZ)
    {
        if (outCurrentPositionInsidePrism) *outCurrentPositionInsidePrism = false;
        if (outProjectedPositionInsidePrism) *outProjectedPositionInsidePrism = false;
        if (outThresholdSensitive) *outThresholdSensitive = false;
        if (outNormalZ) *outNormalZ = 0.0f;

        if (!triangle || !contactNormal || !currentPosition || !projectedPosition) {
            return false;
        }

        SceneQuery::AABBContact contact{};
        contact.normal = contactNormal->directionOrZero();
        contact.rawNormal = *contactNormal;
        contact.triangleA = triangle->a;
        contact.triangleB = triangle->b;
        contact.triangleC = triangle->c;
        contact.planeDistance = contact.normal.magnitude() > PhysicsConstants::VECTOR_EPSILON
            ? -contact.normal.dot(contact.triangleA)
            : 0.0f;

        const auto result = WoWCollision::EvaluateSelectedContactThresholdGate(
            contact,
            *currentPosition,
            *projectedPosition,
            useStandardWalkableThreshold);

        if (outCurrentPositionInsidePrism) *outCurrentPositionInsidePrism = result.currentPositionInsidePrism;
        if (outProjectedPositionInsidePrism) *outProjectedPositionInsidePrism = result.projectedPositionInsidePrism;
        if (outThresholdSensitive) *outThresholdSensitive = result.thresholdSensitive;
        if (outNormalZ) *outNormalZ = result.normalZ;
        return result.wouldUseDirectPair;
    }

    __declspec(dllexport) bool EvaluateWoWPointInsideAabbInclusive(
        const G3D::Vector3* boundsMin,
        const G3D::Vector3* boundsMax,
        const G3D::Vector3* point)
    {
        if (!boundsMin || !boundsMax || !point) {
            return false;
        }

        return WoWCollision::IsPointInsideAabbInclusive(*boundsMin, *boundsMax, *point);
    }

    __declspec(dllexport) bool HasWoWSelectorCandidateWithUnitZ(
        const ExportSelectorSupportPlane* candidates,
        int candidateCount)
    {
        if ((!candidates && candidateCount != 0) || candidateCount < 0 || candidateCount > 5) {
            return false;
        }

        std::array<WoWCollision::SelectorSupportPlane, 5> candidateBuffer{};
        for (int i = 0; i < candidateCount; ++i) {
            candidateBuffer[static_cast<size_t>(i)].normal = candidates[i].normal;
            candidateBuffer[static_cast<size_t>(i)].planeDistance = candidates[i].planeDistance;
        }

        return WoWCollision::HasSelectorCandidateWithUnitZ(
            candidateBuffer.data(),
            static_cast<uint32_t>(candidateCount));
    }

    __declspec(dllexport) bool HasWoWSelectorCandidateWithNegativeDiagonalZ(
        const ExportSelectorSupportPlane* candidates,
        int candidateCount)
    {
        if ((!candidates && candidateCount != 0) || candidateCount < 0 || candidateCount > 5) {
            return false;
        }

        std::array<WoWCollision::SelectorSupportPlane, 5> candidateBuffer{};
        for (int i = 0; i < candidateCount; ++i) {
            candidateBuffer[static_cast<size_t>(i)].normal = candidates[i].normal;
            candidateBuffer[static_cast<size_t>(i)].planeDistance = candidates[i].planeDistance;
        }

        return WoWCollision::HasSelectorCandidateWithNegativeDiagonalZ(
            candidateBuffer.data(),
            static_cast<uint32_t>(candidateCount));
    }

    __declspec(dllexport) int BuildWoWSelectorSupportPlanes(
        const G3D::Vector3* position,
        float verticalOffset,
        float horizontalRadius,
        ExportSelectorSupportPlane* outPlanes,
        int maxPlanes)
    {
        if (!position || !outPlanes || maxPlanes <= 0) {
            return 0;
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> planes{};
        WoWCollision::BuildSelectorSupportPlanes(*position, verticalOffset, horizontalRadius, planes);

        const int count = std::min<int>(static_cast<int>(planes.size()), maxPlanes);
        for (int i = 0; i < count; ++i) {
            outPlanes[i].normal = planes[static_cast<size_t>(i)].normal;
            outPlanes[i].planeDistance = planes[static_cast<size_t>(i)].planeDistance;
        }

        return count;
    }

    __declspec(dllexport) bool BuildWoWSelectorNeighborhood(
        const G3D::Vector3* position,
        float verticalOffset,
        float horizontalRadius,
        G3D::Vector3* outPoints,
        int maxPoints,
        uint8_t* outSelectorIndices,
        int maxSelectorIndices)
    {
        if (!position || !outPoints || !outSelectorIndices || maxPoints < 9 || maxSelectorIndices < 32) {
            return false;
        }

        std::array<G3D::Vector3, 9> points{};
        std::array<uint8_t, 32> selectorIndices{};
        WoWCollision::BuildSelectorNeighborhood(*position, verticalOffset, horizontalRadius, points, selectorIndices);

        for (size_t i = 0; i < points.size(); ++i) {
            outPoints[i] = points[i];
        }
        std::memcpy(outSelectorIndices, selectorIndices.data(), selectorIndices.size());
        return true;
    }

    __declspec(dllexport) float EvaluateWoWSelectorPlaneRatio(
        const G3D::Vector3* candidatePoint,
        const ExportSelectorSupportPlane* plane,
        const G3D::Vector3* testPoint)
    {
        if (!candidatePoint || !plane || !testPoint) {
            return 0.0f;
        }

        WoWCollision::SelectorSupportPlane selectorPlane{};
        selectorPlane.normal = plane->normal;
        selectorPlane.planeDistance = plane->planeDistance;
        return WoWCollision::EvaluateSelectorPlaneRatio(*candidatePoint, selectorPlane, *testPoint);
    }

    __declspec(dllexport) bool ClipWoWSelectorPointStripAgainstPlane(
        const ExportSelectorSupportPlane* plane,
        uint32_t clipPlaneIndex,
        G3D::Vector3* ioPoints,
        uint32_t* ioSourceIndices,
        int maxCapacity,
        int* ioCount)
    {
        if (!plane || !ioPoints || !ioSourceIndices || !ioCount || maxCapacity < 15) {
            return false;
        }

        if (*ioCount < 0 || *ioCount > 15) {
            return false;
        }

        WoWCollision::SelectorSupportPlane selectorPlane{};
        selectorPlane.normal = plane->normal;
        selectorPlane.planeDistance = plane->planeDistance;

        WoWCollision::SelectorPointStrip strip{};
        strip.count = static_cast<uint32_t>(*ioCount);
        for (uint32_t i = 0; i < strip.count; ++i) {
            strip.points[i] = ioPoints[i];
            strip.sourceIndices[i] = ioSourceIndices[i];
        }

        WoWCollision::ClipSelectorPointStripAgainstPlane(selectorPlane, clipPlaneIndex, strip);

        *ioCount = static_cast<int>(strip.count);
        for (uint32_t i = 0; i < strip.count; ++i) {
            ioPoints[i] = strip.points[i];
            ioSourceIndices[i] = strip.sourceIndices[i];
        }

        return true;
    }

    __declspec(dllexport) bool ClipWoWSelectorPointStripAgainstPlanePrefix(
        const ExportSelectorSupportPlane* planes,
        int planeCount,
        G3D::Vector3* ioPoints,
        uint32_t* ioSourceIndices,
        int maxCapacity,
        int* ioCount)
    {
        if ((!planes && planeCount != 0) || planeCount < 0 || planeCount > 9 ||
            !ioPoints || !ioSourceIndices || !ioCount || maxCapacity < 15) {
            return false;
        }

        if (*ioCount < 0 || *ioCount > 15) {
            return false;
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> selectorPlanes{};
        for (int i = 0; i < planeCount; ++i) {
            selectorPlanes[static_cast<size_t>(i)].normal = planes[i].normal;
            selectorPlanes[static_cast<size_t>(i)].planeDistance = planes[i].planeDistance;
        }

        WoWCollision::SelectorPointStrip strip{};
        strip.count = static_cast<uint32_t>(*ioCount);
        for (uint32_t i = 0; i < strip.count; ++i) {
            strip.points[i] = ioPoints[i];
            strip.sourceIndices[i] = ioSourceIndices[i];
        }

        const bool result = WoWCollision::ClipSelectorPointStripAgainstPlanePrefix(
            selectorPlanes.data(),
            static_cast<uint32_t>(planeCount),
            strip);

        *ioCount = static_cast<int>(strip.count);
        for (uint32_t i = 0; i < strip.count; ++i) {
            ioPoints[i] = strip.points[i];
            ioSourceIndices[i] = strip.sourceIndices[i];
        }

        return result;
    }

    __declspec(dllexport) bool EvaluateWoWSelectorCandidateValidation(
        const ExportSelectorSupportPlane* planes,
        int planeCount,
        int planeIndex,
        const G3D::Vector3* testPoint,
        G3D::Vector3* ioPoints,
        uint32_t* ioSourceIndices,
        int maxCapacity,
        int* ioCount,
        float* inOutBestRatio,
        ExportSelectorCandidateValidationTrace* outTrace)
    {
        if (!planes || planeCount < 9 || planeIndex < 0 || planeIndex >= 9 ||
            !testPoint || !ioPoints || !ioSourceIndices || maxCapacity < 15 ||
            !ioCount || !inOutBestRatio) {
            return false;
        }

        if (*ioCount < 0 || *ioCount > 15) {
            return false;
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> selectorPlanes{};
        for (int i = 0; i < 9; ++i) {
            selectorPlanes[static_cast<size_t>(i)].normal = planes[i].normal;
            selectorPlanes[static_cast<size_t>(i)].planeDistance = planes[i].planeDistance;
        }

        WoWCollision::SelectorPointStrip strip{};
        strip.count = static_cast<uint32_t>(*ioCount);
        for (uint32_t i = 0; i < strip.count; ++i) {
            strip.points[i] = ioPoints[i];
            strip.sourceIndices[i] = ioSourceIndices[i];
        }

        WoWCollision::SelectorCandidateValidationTrace trace{};
        const bool result = WoWCollision::ValidateSelectorPointStripCandidate(
            strip,
            *testPoint,
            selectorPlanes,
            static_cast<uint32_t>(planeIndex),
            *inOutBestRatio,
            &trace);

        if (trace.rebuildExecuted != 0u && trace.rebuildSucceeded != 0u) {
            WoWCollision::SelectorPointStrip rebuiltStrip = strip;
            if (WoWCollision::ClipSelectorPointStripExcludingPlane(
                selectorPlanes,
                static_cast<uint32_t>(planeIndex),
                rebuiltStrip)) {
                strip = rebuiltStrip;
            }
        }

        *ioCount = static_cast<int>(strip.count);
        for (uint32_t i = 0; i < strip.count; ++i) {
            ioPoints[i] = strip.points[i];
            ioSourceIndices[i] = strip.sourceIndices[i];
        }

        if (outTrace) {
            outTrace->inputBestRatio = trace.inputBestRatio;
            outTrace->candidateBestRatio = trace.candidateBestRatio;
            outTrace->outputBestRatio = trace.outputBestRatio;
            outTrace->firstPassAllBelowLooseThreshold = trace.firstPassAllBelowLooseThreshold;
            outTrace->rebuildExecuted = trace.rebuildExecuted;
            outTrace->rebuildSucceeded = trace.rebuildSucceeded;
            outTrace->secondPassAllBelowStrictThreshold = trace.secondPassAllBelowStrictThreshold;
            outTrace->improvedBestRatio = trace.improvedBestRatio;
            outTrace->finalStripCount = trace.finalStripCount;
        }

        return result;
    }

    __declspec(dllexport) bool EvaluateWoWSelectorCandidateRecordSet(
        const ExportSelectorCandidateRecord* records,
        int recordCount,
        const G3D::Vector3* testPoint,
        const ExportSelectorSupportPlane* clipPlanes,
        int clipPlaneCount,
        const ExportSelectorSupportPlane* validationPlanes,
        int validationPlaneCount,
        int validationPlaneIndex,
        float* inOutBestRatio,
        int* inOutBestRecordIndex,
        ExportSelectorRecordEvaluationTrace* outTrace)
    {
        if ((!records && recordCount != 0) || recordCount < 0 || recordCount > 5 ||
            !testPoint ||
            (!clipPlanes && clipPlaneCount != 0) || clipPlaneCount < 0 || clipPlaneCount > 9 ||
            !validationPlanes || validationPlaneCount < 9 ||
            validationPlaneIndex < 0 || validationPlaneIndex >= 9 ||
            !inOutBestRatio || !inOutBestRecordIndex) {
            return false;
        }

        std::array<WoWCollision::SelectorCandidateRecord, 5> recordBuffer{};
        for (int i = 0; i < recordCount; ++i) {
            recordBuffer[static_cast<size_t>(i)].filterPlane.normal = records[i].filterPlane.normal;
            recordBuffer[static_cast<size_t>(i)].filterPlane.planeDistance = records[i].filterPlane.planeDistance;
            recordBuffer[static_cast<size_t>(i)].points[0] = records[i].point0;
            recordBuffer[static_cast<size_t>(i)].points[1] = records[i].point1;
            recordBuffer[static_cast<size_t>(i)].points[2] = records[i].point2;
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> clipPlaneBuffer{};
        for (int i = 0; i < clipPlaneCount; ++i) {
            clipPlaneBuffer[static_cast<size_t>(i)].normal = clipPlanes[i].normal;
            clipPlaneBuffer[static_cast<size_t>(i)].planeDistance = clipPlanes[i].planeDistance;
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> validationPlaneBuffer{};
        for (int i = 0; i < 9; ++i) {
            validationPlaneBuffer[static_cast<size_t>(i)].normal = validationPlanes[i].normal;
            validationPlaneBuffer[static_cast<size_t>(i)].planeDistance = validationPlanes[i].planeDistance;
        }

        uint32_t bestRecordIndex = (*inOutBestRecordIndex >= 0)
            ? static_cast<uint32_t>(*inOutBestRecordIndex)
            : 0xFFFFFFFFu;

        WoWCollision::SelectorRecordEvaluationTrace trace{};
        const bool result = WoWCollision::EvaluateSelectorCandidateRecordSet(
            recordBuffer.data(),
            static_cast<uint32_t>(recordCount),
            *testPoint,
            clipPlaneBuffer.data(),
            static_cast<uint32_t>(clipPlaneCount),
            validationPlaneBuffer,
            static_cast<uint32_t>(validationPlaneIndex),
            *inOutBestRatio,
            bestRecordIndex,
            &trace);

        *inOutBestRecordIndex = (bestRecordIndex == 0xFFFFFFFFu)
            ? -1
            : static_cast<int>(bestRecordIndex);

        if (outTrace) {
            outTrace->inputBestRatio = trace.inputBestRatio;
            outTrace->outputBestRatio = trace.outputBestRatio;
            outTrace->selectedBestRatio = trace.selectedBestRatio;
            outTrace->recordCount = trace.recordCount;
            outTrace->dotRejectedCount = trace.dotRejectedCount;
            outTrace->clipRejectedCount = trace.clipRejectedCount;
            outTrace->validationRejectedCount = trace.validationRejectedCount;
            outTrace->validationAcceptedCount = trace.validationAcceptedCount;
            outTrace->updatedBestRatio = trace.updatedBestRatio;
            outTrace->selectedRecordIndex = trace.selectedRecordIndex;
            outTrace->selectedStripCount = trace.selectedStripCount;
        }

        return result;
    }

    __declspec(dllexport) bool EvaluateWoWSelectorTriangleSourceRanking(
        const ExportSelectorCandidateRecord* records,
        int recordCount,
        const G3D::Vector3* testPoint,
        const G3D::Vector3* candidateDirection,
        const G3D::Vector3* points,
        int pointCount,
        const ExportSelectorSupportPlane* supportPlanes,
        int planeCount,
        const uint8_t* selectorIndices,
        int selectorIndexCount,
        ExportSelectorSupportPlane* ioBestCandidates,
        int maxBestCandidates,
        int* ioCandidateCount,
        float* ioBestRatio,
        ExportSelectorSourceRankingTrace* outTrace)
    {
        if ((!records && recordCount != 0) || recordCount < 0 || recordCount > 5 ||
            !testPoint || !candidateDirection ||
            !points || pointCount < 9 ||
            !supportPlanes || planeCount < 9 ||
            !selectorIndices || selectorIndexCount < 32 ||
            !ioBestCandidates || maxBestCandidates < 5 ||
            !ioCandidateCount || !ioBestRatio) {
            return false;
        }

        if (*ioCandidateCount < 0 || *ioCandidateCount > 5) {
            return false;
        }

        std::array<WoWCollision::SelectorCandidateRecord, 5> recordBuffer{};
        for (int i = 0; i < recordCount; ++i) {
            recordBuffer[static_cast<size_t>(i)].filterPlane.normal = records[i].filterPlane.normal;
            recordBuffer[static_cast<size_t>(i)].filterPlane.planeDistance = records[i].filterPlane.planeDistance;
            recordBuffer[static_cast<size_t>(i)].points[0] = records[i].point0;
            recordBuffer[static_cast<size_t>(i)].points[1] = records[i].point1;
            recordBuffer[static_cast<size_t>(i)].points[2] = records[i].point2;
        }

        std::array<G3D::Vector3, 9> pointBuffer{};
        for (int i = 0; i < 9; ++i) {
            pointBuffer[static_cast<size_t>(i)] = points[i];
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> planeBuffer{};
        for (int i = 0; i < 9; ++i) {
            planeBuffer[static_cast<size_t>(i)].normal = supportPlanes[i].normal;
            planeBuffer[static_cast<size_t>(i)].planeDistance = supportPlanes[i].planeDistance;
        }

        std::array<uint8_t, 32> selectorIndexBuffer{};
        std::memcpy(selectorIndexBuffer.data(), selectorIndices, selectorIndexBuffer.size());

        std::array<WoWCollision::SelectorSupportPlane, 5> bestCandidateBuffer{};
        for (int i = 0; i < *ioCandidateCount; ++i) {
            bestCandidateBuffer[static_cast<size_t>(i)].normal = ioBestCandidates[i].normal;
            bestCandidateBuffer[static_cast<size_t>(i)].planeDistance = ioBestCandidates[i].planeDistance;
        }

        uint32_t candidateCount = static_cast<uint32_t>(*ioCandidateCount);
        WoWCollision::SelectorSourceRankingTrace trace{};
        const bool result = WoWCollision::EvaluateSelectorTriangleSourceRanking(
            recordBuffer.data(),
            static_cast<uint32_t>(recordCount),
            *testPoint,
            *candidateDirection,
            pointBuffer,
            planeBuffer,
            selectorIndexBuffer,
            bestCandidateBuffer,
            candidateCount,
            *ioBestRatio,
            &trace);

        *ioCandidateCount = static_cast<int>(candidateCount);
        for (uint32_t i = 0; i < candidateCount; ++i) {
            ioBestCandidates[i].normal = bestCandidateBuffer[i].normal;
            ioBestCandidates[i].planeDistance = bestCandidateBuffer[i].planeDistance;
        }

        if (outTrace) {
            outTrace->inputBestRatio = trace.inputBestRatio;
            outTrace->outputBestRatio = trace.outputBestRatio;
            outTrace->dotRejectedCount = trace.dotRejectedCount;
            outTrace->builderRejectedCount = trace.builderRejectedCount;
            outTrace->evaluatorRejectedCount = trace.evaluatorRejectedCount;
            outTrace->acceptedSourceCount = trace.acceptedSourceCount;
            outTrace->overwriteCount = trace.overwriteCount;
            outTrace->appendCount = trace.appendCount;
            outTrace->bestRatioUpdatedCount = trace.bestRatioUpdatedCount;
            outTrace->swappedBestToFront = trace.swappedBestToFront;
            outTrace->finalCandidateCount = trace.finalCandidateCount;
            outTrace->selectedSourceIndex = trace.selectedSourceIndex;
        }

        return result;
    }

    __declspec(dllexport) bool EvaluateWoWSelectorDirectionRanking(
        const ExportSelectorCandidateRecord* records,
        int recordCount,
        const G3D::Vector3* testPoint,
        const G3D::Vector3* candidateDirection,
        const G3D::Vector3* points,
        int pointCount,
        const ExportSelectorSupportPlane* supportPlanes,
        int planeCount,
        const uint8_t* selectorIndices,
        int selectorIndexCount,
        ExportSelectorSupportPlane* ioBestCandidates,
        int maxBestCandidates,
        int* ioCandidateCount,
        float* ioBestRatio,
        float* outReportedBestRatio,
        int* ioBestRecordIndex,
        ExportSelectorDirectionRankingTrace* outTrace)
    {
        if ((!records && recordCount != 0) || recordCount < 0 || recordCount > 5 ||
            !testPoint || !candidateDirection ||
            !points || pointCount < 9 ||
            !supportPlanes || planeCount < 9 ||
            !selectorIndices || selectorIndexCount < 32 ||
            !ioBestCandidates || maxBestCandidates < 5 ||
            !ioCandidateCount || !ioBestRatio || !outReportedBestRatio || !ioBestRecordIndex) {
            return false;
        }

        if (*ioCandidateCount < 0 || *ioCandidateCount > 5) {
            return false;
        }

        std::array<WoWCollision::SelectorCandidateRecord, 5> recordBuffer{};
        for (int i = 0; i < recordCount; ++i) {
            recordBuffer[static_cast<size_t>(i)].filterPlane.normal = records[i].filterPlane.normal;
            recordBuffer[static_cast<size_t>(i)].filterPlane.planeDistance = records[i].filterPlane.planeDistance;
            recordBuffer[static_cast<size_t>(i)].points[0] = records[i].point0;
            recordBuffer[static_cast<size_t>(i)].points[1] = records[i].point1;
            recordBuffer[static_cast<size_t>(i)].points[2] = records[i].point2;
        }

        std::array<G3D::Vector3, 9> pointBuffer{};
        for (int i = 0; i < 9; ++i) {
            pointBuffer[static_cast<size_t>(i)] = points[i];
        }

        std::array<WoWCollision::SelectorSupportPlane, 9> planeBuffer{};
        for (int i = 0; i < 9; ++i) {
            planeBuffer[static_cast<size_t>(i)].normal = supportPlanes[i].normal;
            planeBuffer[static_cast<size_t>(i)].planeDistance = supportPlanes[i].planeDistance;
        }

        std::array<uint8_t, 32> selectorIndexBuffer{};
        std::memcpy(selectorIndexBuffer.data(), selectorIndices, selectorIndexBuffer.size());

        std::array<WoWCollision::SelectorSupportPlane, 5> bestCandidateBuffer{};
        for (int i = 0; i < *ioCandidateCount; ++i) {
            bestCandidateBuffer[static_cast<size_t>(i)].normal = ioBestCandidates[i].normal;
            bestCandidateBuffer[static_cast<size_t>(i)].planeDistance = ioBestCandidates[i].planeDistance;
        }

        uint32_t candidateCount = static_cast<uint32_t>(*ioCandidateCount);
        uint32_t bestRecordIndex = (*ioBestRecordIndex >= 0)
            ? static_cast<uint32_t>(*ioBestRecordIndex)
            : 0xFFFFFFFFu;

        WoWCollision::SelectorDirectionRankingTrace trace{};
        const bool result = WoWCollision::EvaluateSelectorDirectionRanking(
            recordBuffer.data(),
            static_cast<uint32_t>(recordCount),
            *testPoint,
            *candidateDirection,
            pointBuffer,
            planeBuffer,
            selectorIndexBuffer,
            bestCandidateBuffer,
            candidateCount,
            *ioBestRatio,
            *outReportedBestRatio,
            bestRecordIndex,
            &trace);

        *ioCandidateCount = static_cast<int>(candidateCount);
        *ioBestRecordIndex = (bestRecordIndex == 0xFFFFFFFFu)
            ? -1
            : static_cast<int>(bestRecordIndex);

        for (uint32_t i = 0; i < candidateCount; ++i) {
            ioBestCandidates[i].normal = bestCandidateBuffer[i].normal;
            ioBestCandidates[i].planeDistance = bestCandidateBuffer[i].planeDistance;
        }

        if (outTrace) {
            outTrace->inputBestRatio = trace.inputBestRatio;
            outTrace->outputBestRatio = trace.outputBestRatio;
            outTrace->reportedBestRatio = trace.reportedBestRatio;
            outTrace->dotRejectedCount = trace.dotRejectedCount;
            outTrace->builderRejectedCount = trace.builderRejectedCount;
            outTrace->evaluatorRejectedCount = trace.evaluatorRejectedCount;
            outTrace->acceptedDirectionCount = trace.acceptedDirectionCount;
            outTrace->overwriteCount = trace.overwriteCount;
            outTrace->appendCount = trace.appendCount;
            outTrace->bestRatioUpdatedCount = trace.bestRatioUpdatedCount;
            outTrace->swappedBestToFront = trace.swappedBestToFront;
            outTrace->zeroClampedOutput = trace.zeroClampedOutput;
            outTrace->finalCandidateCount = trace.finalCandidateCount;
            outTrace->selectedDirectionIndex = trace.selectedDirectionIndex;
            outTrace->selectedRecordIndex = trace.selectedRecordIndex;
        }

        return result;
    }

    __declspec(dllexport) bool BuildWoWSelectorCandidatePlaneRecord(
        const G3D::Vector3* points,
        int pointCount,
        const uint8_t* selectorIndices,
        int selectorIndexCount,
        const G3D::Vector3* translation,
        const ExportSelectorSupportPlane* sourcePlane,
        ExportSelectorSupportPlane* outPlanes,
        int maxPlanes)
    {
        if (!points || pointCount < 9 || !selectorIndices || selectorIndexCount < 3 ||
            !translation || !sourcePlane || !outPlanes || maxPlanes < 4) {
            return false;
        }

        std::array<G3D::Vector3, 9> inputPoints{};
        for (size_t i = 0; i < inputPoints.size(); ++i) {
            inputPoints[i] = points[i];
        }

        std::array<uint8_t, 3> inputSelectorIndices{};
        std::memcpy(inputSelectorIndices.data(), selectorIndices, inputSelectorIndices.size());

        WoWCollision::SelectorSupportPlane inputSourcePlane{};
        inputSourcePlane.normal = sourcePlane->normal;
        inputSourcePlane.planeDistance = sourcePlane->planeDistance;

        std::array<WoWCollision::SelectorSupportPlane, 4> planesOut{};
        if (!WoWCollision::BuildSelectorCandidatePlaneRecord(
            inputPoints,
            inputSelectorIndices,
            *translation,
            inputSourcePlane,
            planesOut)) {
            return false;
        }

        for (size_t i = 0; i < planesOut.size(); ++i) {
            outPlanes[i].normal = planesOut[i].normal;
            outPlanes[i].planeDistance = planesOut[i].planeDistance;
        }

        return true;
    }

    __declspec(dllexport) bool BuildWoWSelectorCandidateQuadPlaneRecord(
        const G3D::Vector3* points,
        int pointCount,
        const uint8_t* selectorIndices,
        int selectorIndexCount,
        const G3D::Vector3* translation,
        const ExportSelectorSupportPlane* sourcePlane,
        ExportSelectorSupportPlane* outPlanes,
        int maxPlanes)
    {
        if (!points || pointCount < 9 || !selectorIndices || selectorIndexCount < 4 ||
            !translation || !sourcePlane || !outPlanes || maxPlanes < 5) {
            return false;
        }

        std::array<G3D::Vector3, 9> inputPoints{};
        for (size_t i = 0; i < inputPoints.size(); ++i) {
            inputPoints[i] = points[i];
        }

        std::array<uint8_t, 4> inputSelectorIndices{};
        std::memcpy(inputSelectorIndices.data(), selectorIndices, inputSelectorIndices.size());

        WoWCollision::SelectorSupportPlane inputSourcePlane{};
        inputSourcePlane.normal = sourcePlane->normal;
        inputSourcePlane.planeDistance = sourcePlane->planeDistance;

        std::array<WoWCollision::SelectorSupportPlane, 5> planesOut{};
        if (!WoWCollision::BuildSelectorCandidateQuadPlaneRecord(
            inputPoints,
            inputSelectorIndices,
            *translation,
            inputSourcePlane,
            planesOut)) {
            return false;
        }

        for (size_t i = 0; i < planesOut.size(); ++i) {
            outPlanes[i].normal = planesOut[i].normal;
            outPlanes[i].planeDistance = planesOut[i].planeDistance;
        }

        return true;
    }

    __declspec(dllexport) int QueryTerrainAABBContacts(
        uint32_t mapId,
        const G3D::Vector3* boxMin,
        const G3D::Vector3* boxMax,
        ExportAABBContact* contacts,
        int maxContacts)
    {
        if (!boxMin || !boxMax || !contacts || maxContacts <= 0)
            return 0;

        std::vector<SceneQuery::AABBContact> results;
        SceneQuery::TestTerrainAABB(mapId, *boxMin, *boxMax, results);

        const int count = std::min(static_cast<int>(results.size()), maxContacts);
        for (int i = 0; i < count; ++i)
        {
            const auto& src = results[static_cast<size_t>(i)];
            contacts[i].point = src.point;
            contacts[i].normal = src.normal;
            contacts[i].rawNormal = src.rawNormal;
            contacts[i].triangleA = src.triangleA;
            contacts[i].triangleB = src.triangleB;
            contacts[i].triangleC = src.triangleC;
            contacts[i].planeDistance = src.planeDistance;
            contacts[i].distance = src.distance;
            contacts[i].instanceId = src.instanceId;
            contacts[i].sourceType = src.sourceType;
            contacts[i].walkable = src.walkable ? 1u : 0u;
        }

        return count;
    }

    __declspec(dllexport) bool EvaluateGroundedWallSelection(
        uint32_t mapId,
        const G3D::Vector3* boxMin,
        const G3D::Vector3* boxMax,
        const G3D::Vector3* currentPosition,
        const G3D::Vector3* requestedMove,
        float collisionRadius,
        float boundingHeight,
        bool groundedWallFlagBefore,
        ExportGroundedWallSelectionTrace* outTrace)
    {
        if (!boxMin || !boxMax || !currentPosition || !requestedMove || !outTrace)
            return false;

        std::memset(outTrace, 0, sizeof(ExportGroundedWallSelectionTrace));
        outTrace->selectedContactIndex = 0xFFFFFFFFu;

        std::vector<SceneQuery::AABBContact> contacts;
        SceneQuery::TestTerrainAABB(mapId, *boxMin, *boxMax, contacts);
        outTrace->queryContactCount = static_cast<uint32_t>(contacts.size());

        bool groundedWallState = groundedWallFlagBefore;
        G3D::Vector3 resolvedMove(0.0f, 0.0f, 0.0f);
        G3D::Vector3 wallNormal(0.0f, 0.0f, 1.0f);
        float blockedFraction = 1.0f;
        WoWCollision::GroundedWallResolutionTrace trace{};
        const bool resolved = WoWCollision::ResolveGroundedWallContacts(
            contacts,
            *currentPosition,
            *requestedMove,
            collisionRadius,
            boundingHeight,
            groundedWallState,
            resolvedMove,
            wallNormal,
            blockedFraction,
            &trace);

        outTrace->queryContactCount = trace.queryContactCount;
        outTrace->candidateCount = trace.candidateCount;
        outTrace->selectedContactIndex = trace.selectedContactIndex;
        outTrace->selectedInstanceId = trace.selectedInstanceId;
        outTrace->selectedSourceType = (trace.selectedContactIndex != 0xFFFFFFFFu && trace.selectedContactIndex < contacts.size())
            ? contacts[trace.selectedContactIndex].sourceType
            : 0u;
        outTrace->rawWalkable = trace.rawWalkable;
        outTrace->walkableWithoutState = trace.walkableWithoutState;
        outTrace->walkableWithState = trace.walkableWithState;
        outTrace->groundedWallStateBefore = trace.groundedWallStateBefore;
        outTrace->groundedWallStateAfter = trace.groundedWallStateAfter;
        outTrace->usedPositionReorientation = trace.usedPositionReorientation;
        outTrace->usedWalkableSelectedContact = trace.usedWalkableSelectedContact;
        outTrace->usedNonWalkableVertical = trace.usedNonWalkableVertical;
        outTrace->usedUphillDiscard = trace.usedUphillDiscard;
        outTrace->usedPrimaryAxisFallback = trace.usedPrimaryAxisFallback;
        outTrace->branchKind = trace.branchKind;
        outTrace->selectedPoint = trace.selectedPoint;
        outTrace->selectedNormal = trace.selectedNormal;
        outTrace->orientedNormal = trace.orientedNormal;
        outTrace->primaryAxis = trace.primaryAxis;
        outTrace->mergedWallNormal = trace.mergedWallNormal;
        outTrace->finalWallNormal = trace.finalWallNormal;
        outTrace->horizontalProjectedMove = trace.horizontalProjectedMove;
        outTrace->branchProjectedMove = trace.branchProjectedMove;
        outTrace->finalProjectedMove = trace.finalProjectedMove;
        outTrace->rawOpposeScore = trace.rawOpposeScore;
        outTrace->orientedOpposeScore = trace.orientedOpposeScore;
        outTrace->requested2D = trace.requested2D;
        outTrace->horizontalResolved2D = trace.horizontalResolved2D;
        outTrace->slopedResolved2D = trace.slopedResolved2D;
        outTrace->finalResolved2D = trace.finalResolved2D;
        outTrace->blockedFraction = trace.blockedFraction;
        outTrace->selectedCurrentPositionInsidePrism = trace.selectedCurrentPositionInsidePrism;
        outTrace->selectedProjectedPositionInsidePrism = trace.selectedProjectedPositionInsidePrism;
        outTrace->selectedThresholdSensitiveStandard = trace.selectedThresholdSensitiveStandard;
        outTrace->selectedThresholdSensitiveRelaxed = trace.selectedThresholdSensitiveRelaxed;
        outTrace->selectedWouldUseDirectPairStandard = trace.selectedWouldUseDirectPairStandard;
        outTrace->selectedWouldUseDirectPairRelaxed = trace.selectedWouldUseDirectPairRelaxed;
        outTrace->selectedThresholdPoint = trace.selectedThresholdPoint;
        outTrace->selectedThresholdNormalZ = trace.selectedThresholdNormalZ;

        if (trace.selectedContactIndex != 0xFFFFFFFFu && trace.selectedContactIndex < contacts.size())
        {
            const auto metadata = ResolveStaticContactMetadata(mapId, contacts[trace.selectedContactIndex]);
            outTrace->selectedInstanceFlags = metadata.instanceFlags;
            outTrace->selectedModelFlags = metadata.modelFlags;
            outTrace->selectedGroupFlags = metadata.groupFlags;
            outTrace->selectedRootId = metadata.rootId;
            outTrace->selectedGroupId = metadata.groupId;
            outTrace->selectedGroupMatchFound = metadata.groupMatchFound ? 1u : 0u;
            outTrace->selectedResolvedModelFlags = metadata.resolvedModelFlags;
            outTrace->selectedMetadataSource = metadata.metadataSource;
        }

        return resolved;

        const G3D::Vector3 moveDir2D(requestedMove->x, requestedMove->y, 0.0f);
        const G3D::Vector3 normalizedMoveDir2D = moveDir2D.directionOrZero();
        if (normalizedMoveDir2D.magnitude() <= PhysicsConstants::VECTOR_EPSILON)
            return false;

        struct Candidate
        {
            uint32_t index;
            G3D::Vector3 axis;
            G3D::Vector3 originalNormal;
            G3D::Vector3 orientedNormal;
            float rawOpposeScore;
            float orientedOpposeScore;
            bool usedPositionReorientation;
        };

        std::vector<Candidate> candidates;
        candidates.reserve(contacts.size());

        for (uint32_t i = 0; i < contacts.size(); ++i)
        {
            const auto& contact = contacts[i];
            G3D::Vector3 normal = contact.normal.directionOrZero();
            G3D::Vector3 horizontalNormal(normal.x, normal.y, 0.0f);
            if (horizontalNormal.squaredMagnitude() <= PhysicsConstants::VECTOR_EPSILON)
                continue;

            const float rawOpposeScore = ComputeHorizontalOpposeScore(normal, normalizedMoveDir2D);

            bool usedPositionReorientation = false;
            const G3D::Vector3 toCurrentPosition(
                currentPosition->x - contact.point.x,
                currentPosition->y - contact.point.y,
                0.0f);

            if (toCurrentPosition.squaredMagnitude() > PhysicsConstants::VECTOR_EPSILON &&
                horizontalNormal.dot(toCurrentPosition) < 0.0f)
            {
                normal = -normal;
                horizontalNormal = -horizontalNormal;
                usedPositionReorientation = true;
            }

            const float orientedOpposeScore = ComputeHorizontalOpposeScore(normal, normalizedMoveDir2D);
            if (orientedOpposeScore <= 1.0e-5f)
                continue;

            G3D::Vector3 axis(0.0f, 0.0f, 0.0f);
            if (std::fabs(horizontalNormal.x) >= std::fabs(horizontalNormal.y))
                axis = G3D::Vector3(horizontalNormal.x > 0.0f ? 1.0f : -1.0f, 0.0f, 0.0f);
            else
                axis = G3D::Vector3(0.0f, horizontalNormal.y > 0.0f ? 1.0f : -1.0f, 0.0f);

            candidates.push_back(Candidate{
                i,
                axis,
                contact.normal.directionOrZero(),
                normal,
                rawOpposeScore,
                orientedOpposeScore,
                usedPositionReorientation });
        }

        outTrace->candidateCount = static_cast<uint32_t>(candidates.size());
        if (candidates.empty())
            return false;

        size_t bestIndex = 0;
        float bestScore = candidates[0].orientedOpposeScore;
        for (size_t i = 1; i < candidates.size(); ++i)
        {
            if (candidates[i].orientedOpposeScore > bestScore)
            {
                bestScore = candidates[i].orientedOpposeScore;
                bestIndex = i;
            }
        }

        const auto& candidate = candidates[bestIndex];
        const auto& selectedContact = contacts[candidate.index];
        outTrace->selectedContactIndex = candidate.index;
        outTrace->selectedInstanceId = selectedContact.instanceId;
        outTrace->rawWalkable = selectedContact.walkable ? 1u : 0u;
        outTrace->usedPositionReorientation = candidate.usedPositionReorientation ? 1u : 0u;
        outTrace->selectedPoint = selectedContact.point;
        outTrace->selectedNormal = candidate.originalNormal;
        outTrace->orientedNormal = candidate.orientedNormal;
        outTrace->primaryAxis = candidate.axis;
        outTrace->rawOpposeScore = candidate.rawOpposeScore;
        outTrace->orientedOpposeScore = candidate.orientedOpposeScore;

        const auto withoutState = WoWCollision::CheckWalkable(
            selectedContact,
            *currentPosition,
            collisionRadius,
            boundingHeight,
            true,
            false);
        const auto withState = WoWCollision::CheckWalkable(
            selectedContact,
            *currentPosition,
            collisionRadius,
            boundingHeight,
            true,
            groundedWallFlagBefore);

        outTrace->walkableWithoutState = withoutState.walkable ? 1u : 0u;
        outTrace->walkableWithState = withState.walkable ? 1u : 0u;
        outTrace->groundedWallStateAfter = withState.groundedWallFlagAfter ? 1u : 0u;
        return true;
    }

    /// Computes a capsule sweep diagnostic for a single position/direction
    /// This is useful for debugging sweep behavior at specific locations
    __declspec(dllexport) SceneQuery::SweepResults ComputeCapsuleSweepDiagnostics(
        uint32_t mapId,
        float x, float y, float z,
        float radius, float height,
        float moveDirX, float moveDirY, float moveDirZ,
        float intendedDist)
    {
        G3D::Vector3 moveDir(moveDirX, moveDirY, moveDirZ);
        return SceneQuery::ComputeCapsuleSweep(mapId, x, y, z, radius, height, moveDir, intendedDist);
    }

    // ==========================================================================
    // DYNAMIC OBJECT REGISTRY (elevators, doors, chests)
    // ==========================================================================

    /// Load the displayId→model mapping from the vmaps directory.
    /// Must be called once before RegisterDynamicObject.
    __declspec(dllexport) bool LoadDynamicObjectMapping(const char* vmapsBasePath)
    {
        if (!vmapsBasePath) return false;
        return DynamicObjectRegistry::Instance()->LoadDisplayIdMapping(vmapsBasePath);
    }

    /// Register a dynamic object by displayId. Loads the real .vmo model mesh.
    __declspec(dllexport) bool RegisterDynamicObject(
        uint64_t guid, uint32_t entry, uint32_t displayId,
        uint32_t mapId, float scale)
    {
        return DynamicObjectRegistry::Instance()->RegisterObject(
            guid, entry, displayId, mapId, scale);
    }

    /// Update the world position and orientation of a dynamic object.
    __declspec(dllexport) void UpdateDynamicObjectPosition(
        uint64_t guid, float x, float y, float z, float orientation, uint32_t goState)
    {
        DynamicObjectRegistry::Instance()->UpdatePosition(guid, x, y, z, orientation, goState);
    }

    /// Remove a single dynamic object by GUID.
    __declspec(dllexport) void UnregisterDynamicObject(uint64_t guid)
    {
        DynamicObjectRegistry::Instance()->Unregister(guid);
    }

    /// Remove all dynamic objects on a given map.
    __declspec(dllexport) void ClearDynamicObjects(uint32_t mapId)
    {
        DynamicObjectRegistry::Instance()->ClearMap(mapId);
    }

    /// Remove all dynamic objects (keeps model cache).
    __declspec(dllexport) void ClearAllDynamicObjects()
    {
        DynamicObjectRegistry::Instance()->ClearAll();
    }

    /// Returns number of active dynamic objects.
    __declspec(dllexport) int GetDynamicObjectCount()
    {
        return DynamicObjectRegistry::Instance()->Count();
    }

    /// Returns number of cached model meshes.
    __declspec(dllexport) int GetCachedModelCount()
    {
        return DynamicObjectRegistry::Instance()->CachedModelCount();
    }

    // ==========================================================================
    // SCENE CACHE (pre-processed collision geometry)
    // ==========================================================================

    /// Extract collision geometry for a map and save to .scene file.
    /// Requires VMAP + MapLoader to be initialized (slow, one-time).
    __declspec(dllexport) bool ExtractSceneCache(
        uint32_t mapId, const char* outPath,
        float minX, float minY, float maxX, float maxY)
    {
        try
        {
            auto* vmapMgr = static_cast<VMAP::VMapManager2*>(
                VMAP::VMapFactory::createOrGetVMapManager());
            SceneCache::ExtractBounds bounds;
            bounds.minX = minX; bounds.minY = minY;
            bounds.maxX = maxX; bounds.maxY = maxY;
            auto* cache = SceneCache::Extract(mapId, vmapMgr, g_testMapLoader, bounds);
            if (!cache) return false;
            bool ok = cache->SaveToFile(outPath);
            // Also register in SceneQuery for immediate use
            SceneQuery::SetSceneCache(mapId, cache);
            return ok;
        }
        catch (...) { return false; }
    }

    /// Load a pre-cached .scene file (fast, ~10ms).
    __declspec(dllexport) bool LoadSceneCache(uint32_t mapId, const char* path)
    {
        try
        {
            auto* cache = SceneCache::LoadFromFile(path);
            if (!cache) return false;
            SceneQuery::SetSceneCache(mapId, cache);
            return true;
        }
        catch (...) { return false; }
    }

    /// Check if a map has a loaded scene cache.
    __declspec(dllexport) bool HasSceneCache(uint32_t mapId)
    {
        return SceneQuery::GetSceneCache(mapId) != nullptr;
    }

    /// Unload scene cache for a map.
    __declspec(dllexport) void UnloadSceneCache(uint32_t mapId)
    {
        SceneQuery::SetSceneCache(mapId, nullptr);
    }

    /// Set the scenes directory for auto-discovery.
    __declspec(dllexport) void SetScenesDir(const char* dir)
    {
        if (dir)
            SceneQuery::SetScenesDir(dir);
    }

    // ==========================================================================
    // WMO DOODAD EXTRACTION (MPQ → .doodads files)
    // ==========================================================================

    // StormLib function typedefs (loaded dynamically to avoid hard dependency)
    // StormLib uses TCHAR which is wchar_t when compiled with UNICODE (our DLL is Unicode-built)
    typedef BOOL (WINAPI *pfn_SFileOpenArchive)(const wchar_t*, DWORD, DWORD, HANDLE*);
    typedef BOOL (WINAPI *pfn_SFileCloseArchive)(HANDLE);
    typedef BOOL (WINAPI *pfn_SFileOpenFileEx)(HANDLE, const char*, DWORD, HANDLE*);
    typedef BOOL (WINAPI *pfn_SFileCloseFile)(HANDLE);
    typedef DWORD (WINAPI *pfn_SFileGetFileSize)(HANDLE, LPDWORD);
    typedef BOOL (WINAPI *pfn_SFileReadFile)(HANDLE, void*, DWORD, LPDWORD, LPOVERLAPPED);
    // Note: SFileFindFirstFile/Next/Close are available but not used.
    // We read (listfile) from MPQ instead for better reliability.

    // Parse statistics (file-scope statics for summary output)
    static int s_doodadParseOk = 0;
    static int s_doodadNoChunks = 0;

    // Parse a WMO root file from MPQ and extract doodad placement data.
    // Returns true if doodad data was found and written to outFile.
    // vmapsLookup: pre-built case-insensitive file lookup (lowercase → actual filename)
    static bool ParseWmoRootDoodads(
        HANDLE hMpq,
        const char* wmoPath,
        const std::unordered_map<std::string, std::string>& vmapsLookup,
        pfn_SFileOpenFileEx pOpen,
        pfn_SFileCloseFile pClose,
        pfn_SFileGetFileSize pGetSize,
        pfn_SFileReadFile pRead,
        WmoDoodad::DoodadFile& outFile)
    {

        HANDLE hFile = nullptr;
        if (!pOpen(hMpq, wmoPath, 0, &hFile) || !hFile)
            return false;

        DWORD fileSize = pGetSize(hFile, nullptr);
        if (fileSize == 0 || fileSize == INVALID_FILE_SIZE)
        {
            pClose(hFile);
            return false;
        }

        std::vector<uint8_t> data(fileSize);
        DWORD bytesRead = 0;
        if (!pRead(hFile, data.data(), fileSize, &bytesRead, nullptr) || bytesRead != fileSize)
        {
            pClose(hFile);
            return false;
        }
        pClose(hFile);

        // Parse chunk-based WMO format
        // Raw doodad paths from MODN (before normalization)
        std::vector<char> rawPaths;
        bool foundMODS = false, foundMODD = false, foundMODN = false;
        size_t pos = 0;
        while (pos + 8 <= fileSize)
        {
            char fourcc[5] = {};
            memcpy(fourcc, &data[pos], 4);
            uint32_t chunkSize = 0;
            memcpy(&chunkSize, &data[pos + 4], 4);
            pos += 8;

            if (pos + chunkSize > fileSize)
                break;

            // WMO files store fourcc in reversed byte order: "MODS" → "SDOM" in file
            if (strcmp(fourcc, "SDOM") == 0 && chunkSize >= sizeof(WmoDoodad::DoodadSet))
            {
                uint32_t count = chunkSize / sizeof(WmoDoodad::DoodadSet);
                outFile.sets.resize(count);
                memcpy(outFile.sets.data(), &data[pos], count * sizeof(WmoDoodad::DoodadSet));
                foundMODS = true;
            }
            else if (strcmp(fourcc, "NDOM") == 0 && chunkSize > 0)
            {
                rawPaths.resize(chunkSize);
                memcpy(rawPaths.data(), &data[pos], chunkSize);
                foundMODN = true;
            }
            else if (strcmp(fourcc, "DDOM") == 0 && chunkSize > 0)
            {
                // WMO MODD entry: 24-bit nameIndex (as bitfield) + position + quaternion + scale + color
                // = 4 + 12 + 16 + 4 + 4 = 40 bytes per entry
                constexpr size_t MODD_ENTRY_SIZE = 40;
                uint32_t count = chunkSize / MODD_ENTRY_SIZE;
                outFile.spawns.resize(count);

                for (uint32_t i = 0; i < count; ++i)
                {
                    const uint8_t* entry = &data[pos + i * MODD_ENTRY_SIZE];
                    auto& spawn = outFile.spawns[i];

                    // First 4 bytes: nameIndex (24-bit) packed into uint32
                    uint32_t raw32 = 0;
                    memcpy(&raw32, entry, 4);
                    spawn.nameOffset = raw32 & 0x00FFFFFF;  // 24-bit name offset

                    memcpy(&spawn.posX, entry + 4, 4);
                    memcpy(&spawn.posY, entry + 8, 4);
                    memcpy(&spawn.posZ, entry + 12, 4);
                    memcpy(&spawn.rotX, entry + 16, 4);
                    memcpy(&spawn.rotY, entry + 20, 4);
                    memcpy(&spawn.rotZ, entry + 24, 4);
                    memcpy(&spawn.rotW, entry + 28, 4);
                    memcpy(&spawn.scale, entry + 32, 4);
                    // skip color at offset 36
                }
                foundMODD = true;
            }

            pos += chunkSize;
        }

        if (!foundMODS || !foundMODD || !foundMODN)
        {
            s_doodadNoChunks++;
            return false;
        }
        if (outFile.sets.empty() || outFile.spawns.empty())
            return false;

        // Build normalized name table: convert raw MPQ paths to vmaps filenames.
        // Only include doodads whose .m2.vmo file exists in vmaps/.
        // Build a mapping from raw nameOffset → new nameOffset in normalized table.
        std::unordered_map<uint32_t, uint32_t> offsetMap;

        for (auto& spawn : outFile.spawns)
        {
            uint32_t rawOff = spawn.nameOffset;
            if (offsetMap.count(rawOff))
            {
                spawn.nameOffset = offsetMap[rawOff];
                continue;
            }

            if (rawOff >= rawPaths.size())
            {
                spawn.nameOffset = 0xFFFFFFFF;
                continue;
            }

            const char* rawName = &rawPaths[rawOff];
            std::string normalized = WmoDoodad::NormalizeDoodadName(rawName);

            // Check if the M2 .vmo file exists in vmaps via pre-built lookup
            std::string normLower = normalized;
            std::transform(normLower.begin(), normLower.end(), normLower.begin(), ::tolower);

            bool exists = false;
            // Check for .m2.vmo (most common)
            auto it = vmapsLookup.find(normLower + ".vmo");
            if (it != vmapsLookup.end())
            {
                normalized = it->second;
                if (normalized.size() > 4 && normalized.substr(normalized.size() - 4) == ".vmo")
                    normalized = normalized.substr(0, normalized.size() - 4);
                exists = true;
            }
            else
            {
                // Check for raw .m2 file
                it = vmapsLookup.find(normLower);
                if (it != vmapsLookup.end())
                {
                    normalized = it->second;
                    exists = true;
                }
            }

            if (!exists)
            {
                offsetMap[rawOff] = 0xFFFFFFFF;
                spawn.nameOffset = 0xFFFFFFFF;
                continue;
            }

            uint32_t newOff = static_cast<uint32_t>(outFile.nameTable.size());
            offsetMap[rawOff] = newOff;
            spawn.nameOffset = newOff;

            // Append normalized name (null-terminated) to name table
            outFile.nameTable.insert(outFile.nameTable.end(),
                                     normalized.begin(), normalized.end());
            outFile.nameTable.push_back('\0');
        }

        s_doodadParseOk++;
        return true;
    }

    /// Extract WMO doodad placement data from MPQ archives.
    /// mpqDataDir: path to WoW client Data/ directory (e.g. "D:/World of Warcraft/Data")
    /// vmapsDir: path to vmaps/ directory (e.g. "Bot/Debug/net8.0/vmaps/")
    /// Returns the number of .doodads files written, or -1 on error.
    __declspec(dllexport) int ExtractWmoDoodads(const char* mpqDataDir, const char* vmapsDir)
    {
        if (!mpqDataDir || !vmapsDir)
            return -1;

        // Reset parse counters
        s_doodadParseOk = 0;
        s_doodadNoChunks = 0;

        // Ensure vmapsDir ends with separator
        std::string vmapsPath(vmapsDir);
        if (!vmapsPath.empty() && vmapsPath.back() != '/' && vmapsPath.back() != '\\')
            vmapsPath += '/';

        // Load StormLib dynamically
        HMODULE hStorm = LoadLibraryA("StormLib.dll");
        if (!hStorm)
        {
            // Try next to our DLL
            char dllPath[MAX_PATH] = {};
            HMODULE hSelf = nullptr;
            GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCSTR)&ExtractWmoDoodads, &hSelf);
            if (hSelf)
            {
                GetModuleFileNameA(hSelf, dllPath, MAX_PATH);
                std::string dir(dllPath);
                size_t slash = dir.find_last_of("\\/");
                if (slash != std::string::npos)
                    dir = dir.substr(0, slash + 1);
                hStorm = LoadLibraryA((dir + "StormLib.dll").c_str());
            }
        }
        if (!hStorm)
        {
            std::cerr << "[DoodadExtract] Cannot load StormLib.dll\n";
            return -1;
        }

        auto pOpenArchive  = (pfn_SFileOpenArchive)GetProcAddress(hStorm, "SFileOpenArchive");
        auto pCloseArchive = (pfn_SFileCloseArchive)GetProcAddress(hStorm, "SFileCloseArchive");
        auto pOpenFile     = (pfn_SFileOpenFileEx)GetProcAddress(hStorm, "SFileOpenFileEx");
        auto pCloseFile    = (pfn_SFileCloseFile)GetProcAddress(hStorm, "SFileCloseFile");
        auto pGetFileSize  = (pfn_SFileGetFileSize)GetProcAddress(hStorm, "SFileGetFileSize");
        auto pReadFile     = (pfn_SFileReadFile)GetProcAddress(hStorm, "SFileReadFile");

        if (!pOpenArchive || !pCloseArchive || !pOpenFile || !pCloseFile ||
            !pGetFileSize || !pReadFile)
        {
            std::cerr << "[DoodadExtract] StormLib function(s) not found\n";
            FreeLibrary(hStorm);
            return -1;
        }

        // Collect MPQ archives from Data directory
        std::string dataDir(mpqDataDir);
        if (!dataDir.empty() && dataDir.back() != '/' && dataDir.back() != '\\')
            dataDir += '/';

        std::vector<std::string> mpqFiles;
        for (const auto& entry : std::filesystem::directory_iterator(dataDir))
        {
            if (!entry.is_regular_file()) continue;
            std::string ext = entry.path().extension().string();
            std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);
            if (ext == ".mpq")
                mpqFiles.push_back(entry.path().string());
        }

        // Sort: patch files first (highest priority), then alphabetical
        std::sort(mpqFiles.begin(), mpqFiles.end(), [](const std::string& a, const std::string& b) {
            std::string aName = std::filesystem::path(a).filename().string();
            std::string bName = std::filesystem::path(b).filename().string();
            std::transform(aName.begin(), aName.end(), aName.begin(), ::tolower);
            std::transform(bName.begin(), bName.end(), bName.begin(), ::tolower);
            bool aIsPatch = aName.find("patch") != std::string::npos;
            bool bIsPatch = bName.find("patch") != std::string::npos;
            if (aIsPatch != bIsPatch) return aIsPatch;
            return aName < bName;
        });

        fprintf(stderr, "[DoodadExtract] Found %zu MPQ archives in %s\n", mpqFiles.size(), dataDir.c_str());

        // Pre-build a case-insensitive lookup of all files in vmaps/
        // (avoids O(N*M) directory scans during matching)
        std::unordered_map<std::string, std::string> vmapsFileLookup; // lowercase → actual
        for (const auto& entry : std::filesystem::directory_iterator(vmapsPath))
        {
            if (!entry.is_regular_file()) continue;
            std::string fn = entry.path().filename().string();
            std::string fnLower = fn;
            std::transform(fnLower.begin(), fnLower.end(), fnLower.begin(), ::tolower);
            vmapsFileLookup[fnLower] = fn;
        }
        fprintf(stderr, "[DoodadExtract] vmaps lookup: %zu files indexed\n", vmapsFileLookup.size());

        // Collect all WMO root file paths across all archives
        // Track which files we've already processed to avoid duplicates
        std::unordered_set<std::string> processedWmos;
        int filesWritten = 0;
        int wmosFound = 0;

        // Phase 1: Open all MPQ archives into a list
        std::vector<HANDLE> openArchives;
        std::vector<std::string> archiveNames;
        constexpr DWORD STREAM_FLAG_READ_ONLY = 0x00000100;

        for (const auto& mpqPath : mpqFiles)
        {
            HANDLE hMpq = nullptr;
            std::wstring wMpqPath;
            int wLen = MultiByteToWideChar(CP_ACP, 0, mpqPath.c_str(), -1, nullptr, 0);
            if (wLen > 0) {
                wMpqPath.resize(wLen - 1);
                MultiByteToWideChar(CP_ACP, 0, mpqPath.c_str(), -1, &wMpqPath[0], wLen);
            }

            std::string mpqName = std::filesystem::path(mpqPath).filename().string();
            if (pOpenArchive(wMpqPath.c_str(), 0, STREAM_FLAG_READ_ONLY, &hMpq) && hMpq)
            {
                openArchives.push_back(hMpq);
                archiveNames.push_back(mpqName);
                fprintf(stderr, "[DoodadExtract]   Opened: %s\n", mpqName.c_str());
            }
        }
        int archivesOpened = (int)openArchives.size();
        fprintf(stderr, "[DoodadExtract] %d/%zu archives opened\n", archivesOpened, mpqFiles.size());

        // Phase 2: For each .wmo.vmo in vmaps/, try to read the WMO from MPQ
        // Collect all WMO root file candidates from vmaps
        std::vector<std::pair<std::string, std::string>> wmoVmapsCandidates; // (vmapsName sans .vmo, plainWmoName)
        for (const auto& [lowKey, actualName] : vmapsFileLookup)
        {
            // Match pattern: something.wmo.vmo
            if (lowKey.size() > 8 && lowKey.substr(lowKey.size() - 8) == ".wmo.vmo")
            {
                std::string vmapsName = actualName.substr(0, actualName.size() - 4); // strip .vmo
                std::string wmoName = actualName.substr(0, actualName.size() - 4);   // "Foo.wmo"
                wmoVmapsCandidates.emplace_back(vmapsName, wmoName);
            }
        }
        fprintf(stderr, "[DoodadExtract] Found %zu WMO candidates in vmaps\n", wmoVmapsCandidates.size());

        // Read (listfile) from the first archive that has one to build WMO path mappings
        // Map: lowercase plain WMO name → full MPQ path
        std::unordered_map<std::string, std::string> wmoPathMap; // e.g. "orgrimmar.wmo" → "World\wmo\..."
        for (size_t ai = 0; ai < openArchives.size(); ++ai)
        {
            HANDLE hFile = nullptr;
            if (!pOpenFile(openArchives[ai], "(listfile)", 0, &hFile) || !hFile)
                continue;

            DWORD fileSize = pGetFileSize(hFile, nullptr);
            if (fileSize == 0 || fileSize == INVALID_FILE_SIZE)
            {
                pCloseFile(hFile);
                continue;
            }

            std::vector<char> listData(fileSize + 1, 0);
            DWORD bytesRead = 0;
            pReadFile(hFile, listData.data(), fileSize, &bytesRead, nullptr);
            pCloseFile(hFile);

            // Parse lines and find .wmo files (not group files)
            std::istringstream iss(std::string(listData.data(), bytesRead));
            std::string line;
            int wmoCount = 0;
            while (std::getline(iss, line))
            {
                // Trim whitespace
                while (!line.empty() && (line.back() == '\r' || line.back() == '\n' || line.back() == ' '))
                    line.pop_back();
                if (line.empty()) continue;

                // Check if it's a .wmo file
                std::string lineLower = line;
                std::transform(lineLower.begin(), lineLower.end(), lineLower.begin(), ::tolower);
                if (lineLower.size() < 4 || lineLower.substr(lineLower.size() - 4) != ".wmo")
                    continue;

                // Check if it's a group file (ends with _NNN.wmo)
                std::string stem = std::filesystem::path(lineLower).stem().string();
                bool isGroup = false;
                if (stem.size() > 4)
                {
                    size_t lastUnderscore = stem.rfind('_');
                    if (lastUnderscore != std::string::npos && lastUnderscore < stem.size() - 1)
                    {
                        bool allDigits = true;
                        for (size_t i = lastUnderscore + 1; i < stem.size(); ++i)
                        {
                            if (!isdigit(stem[i])) { allDigits = false; break; }
                        }
                        if (allDigits) isGroup = true;
                    }
                }
                if (isGroup) continue;

                // Normalize plain name to vmaps convention
                const char* plain = WmoDoodad::GetPlainName(line.c_str());
                char nameNorm[512];
                size_t nLen = strlen(plain);
                if (nLen >= sizeof(nameNorm)) nLen = sizeof(nameNorm) - 1;
                memcpy(nameNorm, plain, nLen);
                nameNorm[nLen] = '\0';
                WmoDoodad::FixNameCase(nameNorm, nLen);
                WmoDoodad::FixNameSpaces(nameNorm, nLen);

                std::string key(nameNorm, nLen);
                std::transform(key.begin(), key.end(), key.begin(), ::tolower);

                // Store mapping: normalized lowercase name → original MPQ path
                if (wmoPathMap.find(key) == wmoPathMap.end())
                {
                    wmoPathMap[key] = line;
                    wmoCount++;
                }
            }
            fprintf(stderr, "[DoodadExtract]   %s listfile: %d root WMOs\n", archiveNames[ai].c_str(), wmoCount);
        }
        fprintf(stderr, "[DoodadExtract] Total WMO path mappings: %zu\n", wmoPathMap.size());

        // Phase 3: For each WMO in vmaps, find it in MPQ, parse doodads, write .doodads file
        int candidateIdx = 0;
        for (const auto& [vmapsName, wmoName] : wmoVmapsCandidates)
        {
            std::string wmoNameLower = wmoName;
            std::transform(wmoNameLower.begin(), wmoNameLower.end(), wmoNameLower.begin(), ::tolower);

            auto pathIt = wmoPathMap.find(wmoNameLower);
            if (pathIt == wmoPathMap.end())
                continue;

            const std::string& mpqWmoPath = pathIt->second;
            wmosFound++;
            candidateIdx++;
            if (candidateIdx <= 3)
                fprintf(stderr, "[DoodadExtract] Candidate %d: vmaps='%s' lookup='%s' mpqPath='%s'\n",
                        candidateIdx, vmapsName.c_str(), wmoNameLower.c_str(), mpqWmoPath.c_str());

            // Check if .doodads file already exists
            std::string doodadsPath = vmapsPath + vmapsName + ".doodads";
            if (std::filesystem::exists(doodadsPath))
            {
                filesWritten++;
                continue;
            }

            // Try to open and parse the WMO from each archive
            WmoDoodad::DoodadFile doodadFile;
            bool parsed = false;
            for (HANDLE hMpq : openArchives)
            {
                if (ParseWmoRootDoodads(hMpq, mpqWmoPath.c_str(), vmapsFileLookup,
                                        pOpenFile, pCloseFile, pGetFileSize, pReadFile,
                                        doodadFile))
                {
                    parsed = true;
                    break;
                }
            }
            if (!parsed) continue;

            // Skip if no usable doodads
            bool hasUsable = false;
            for (const auto& spawn : doodadFile.spawns)
            {
                if (spawn.nameOffset != 0xFFFFFFFF) { hasUsable = true; break; }
            }
            if (!hasUsable) continue;

            if (doodadFile.Write(doodadsPath))
            {
                filesWritten++;
                fprintf(stderr, "[DoodadExtract] %s: %zu sets, %zu spawns\n",
                        vmapsName.c_str(), doodadFile.sets.size(), doodadFile.spawns.size());
            }
        }

        // Print parse diagnostic summary
        fprintf(stderr, "[DoodadExtract] Parse stats: noChunks=%d parseOk=%d\n",
                s_doodadNoChunks, s_doodadParseOk);

        // Close all archives
        for (HANDLE hMpq : openArchives)
            pCloseArchive(hMpq);

        FreeLibrary(hStorm);
        fprintf(stderr, "[DoodadExtract] Done: %d archives opened, %d WMOs matched, %d .doodads files\n",
                archivesOpened, wmosFound, filesWritten);
        return filesWritten;
    }

} // extern "C"
