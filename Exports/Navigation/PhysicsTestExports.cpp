// PhysicsTestExports.cpp - C exports for physics testing from managed code
// These functions expose the internal physics primitives for unit testing.

#include "PhysicsEngine.h"
#include "SceneQuery.h"
#include "SceneCache.h"
#include "CapsuleCollision.h"
#include "MapLoader.h"
#include "CoordinateTransforms.h"
#include "DynamicObjectRegistry.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include "StaticMapTree.h"
#include <cstring>
#include <cstdlib>
#include <string>
#define NOMINMAX
#include <windows.h>
#include <filesystem>

// Global instances for testing
static MapLoader* g_testMapLoader = nullptr;

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

        // Need to get the static map tree for overlap test
        // This requires the VMAP manager to be initialized via SceneQuery
        return 0;  // TODO: Implement when needed
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
        uint64_t guid, float x, float y, float z, float orientation)
    {
        DynamicObjectRegistry::Instance()->UpdatePosition(guid, x, y, z, orientation);
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

} // extern "C"
