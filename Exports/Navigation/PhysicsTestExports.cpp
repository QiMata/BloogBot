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
#include <cstring>
#include <cstdlib>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <algorithm>
#include <sstream>
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
