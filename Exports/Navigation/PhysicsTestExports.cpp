// PhysicsTestExports.cpp - C exports for physics testing from managed code
// These functions expose the internal physics primitives for unit testing.

#include "PhysicsEngine.h"
#include "SceneQuery.h"
#include "CapsuleCollision.h"
#include "MapLoader.h"
#include "CoordinateTransforms.h"
#include <cstring>

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
            return g_testMapLoader->Initialize(dataPath ? dataPath : "maps/");
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

} // extern "C"
