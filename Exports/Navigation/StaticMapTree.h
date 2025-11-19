// StaticMapTree.h - Enhanced with cylinder collision support
#pragma once

#include <unordered_map>
#include <string>
#include <memory>
#include <vector>
#include "BIH.h"
#include "Vector3.h"
#include "Ray.h"
#include "ModelInstance.h"
#include "CylinderCollision.h"
#include "CoordinateTransforms.h"

namespace VMAP
{
    class VMapManager2;
    class GroupModel;
    class WorldModel;

    // Callback for cylinder collision with map
    class MapCylinderCallback
    {
    public:
        MapCylinderCallback(ModelInstance* val, const Cylinder& cyl)
            : prims(val), cylinder(cyl), bestIntersection() {}

        bool operator()(const G3D::Vector3& point, uint32_t entry)
        {
            CylinderIntersection result = prims[entry].IntersectCylinder(cylinder);
            if (result.hit)
            {
                // stamp instance id for diagnostics
                result.instanceId = prims[entry].ID;
                if (!bestIntersection.hit || result.contactHeight > bestIntersection.contactHeight)
                {
                    bestIntersection = result;
                    hitInstance = &prims[entry];
                }
                return true;
            }
            return false;
        }

        ModelInstance* prims;
        const Cylinder& cylinder;
        CylinderIntersection bestIntersection;
        ModelInstance* hitInstance = nullptr;
    };

    // Callback for cylinder sweep through map
    class MapCylinderSweepCallback
    {
    public:
        MapCylinderSweepCallback(ModelInstance* val, const Cylinder& cyl,
            const G3D::Vector3& dir, float dist)
            : prims(val), cylinder(cyl), sweepDir(dir), sweepDistance(dist) {}

        void operator()(const G3D::Vector3& point, uint32_t entry)
        {
            // Convert internal cylinder and direction to world space before calling ModelInstance
            Cylinder worldCyl(
                NavCoord::InternalToWorld(cylinder.base),
                cylinder.axis,
                cylinder.radius,
                cylinder.height);
            G3D::Vector3 worldSweepDir = NavCoord::InternalDirToWorld(sweepDir);

            std::vector<CylinderSweepHit> modelHits = prims[entry].SweepCylinder(
                worldCyl, worldSweepDir, sweepDistance);

            // Merge hits from this model
            for (auto& h : modelHits)
            {
                // stamp instance id on unified query record for diagnostics
                h.q.instanceId = prims[entry].ID;
                allHits.push_back(h);
            }
        }

        ModelInstance* prims;
        const Cylinder& cylinder;
        G3D::Vector3 sweepDir;
        float sweepDistance;
        std::vector<CylinderSweepHit> allHits;
    };

    class StaticMapTree
    {
    private:
        uint32_t iMapID;
        std::string iBasePath;
        bool iIsTiled;

        BIH iTree;
        ModelInstance* iTreeValues;
        uint32_t iNTreeValues;

        std::unordered_map<uint32_t, uint32_t> iLoadedSpawns;
        std::unordered_map<uint32_t, bool> iLoadedTiles;

        // Preload all tiles for maximum performance
        bool PreloadAllTiles(VMapManager2* vm);

    public:
        StaticMapTree(uint32_t mapId, const std::string& basePath);
        ~StaticMapTree();

        bool InitMap(const std::string& fname, VMapManager2* vm);
        bool LoadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm);
        void UnloadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm);
        void UnloadMap(VMapManager2* vm);

        // Original collision and height queries
        bool isInLineOfSight(const G3D::Vector3& pos1, const G3D::Vector3& pos2, bool ignoreM2Model) const;
        bool getObjectHitPos(const G3D::Vector3& pos1, const G3D::Vector3& pos2,
            G3D::Vector3& resultHitPos, float modifyDist) const;
        float getHeight(const G3D::Vector3& pos, float maxSearchDist) const;
        bool getAreaInfo(G3D::Vector3& pos, uint32_t& flags, int32_t& adtId,
            int32_t& rootId, int32_t& groupId) const;
        bool GetLocationInfo(const G3D::Vector3& pos, LocationInfo& info) const;
        bool isUnderModel(G3D::Vector3& pos, float* outDist = nullptr, float* inDist = nullptr) const;

        bool getIntersectionTime(const G3D::Ray& ray, float& maxDist,
            bool stopAtFirstHit, bool ignoreM2Model) const;
        // Extended variant: optionally retrieve hit point (world), hit normal (world), instanceId and triangle index
        bool getIntersectionTime(const G3D::Ray& ray, float& maxDist,
            bool stopAtFirstHit, bool ignoreM2Model,
            G3D::Vector3* outHitPointW, G3D::Vector3* outHitNormalW, uint32_t* outInstanceId, int* outTriIndex) const;

        ModelInstance* FindCollisionModel(const G3D::Vector3& pos1, const G3D::Vector3& pos2);

        // New cylinder collision methods
        CylinderIntersection IntersectCylinder(const Cylinder& cyl) const;
        std::vector<CylinderSweepHit> SweepCylinder(const Cylinder& cyl,
            const G3D::Vector3& sweepDir, float sweepDistance) const;
        bool CheckCylinderCollision(const Cylinder& cyl,
            float& outContactHeight, G3D::Vector3& outContactNormal,
            ModelInstance** outHitInstance = nullptr) const;
        bool CanCylinderFitAtPosition(const Cylinder& cyl, float tolerance = 0.05f) const;

        // Find best walkable surface for cylinder movement
        bool FindCylinderWalkableSurface(const Cylinder& cyl,
            float currentHeight, float maxStepUp, float maxStepDown,
            float& outHeight, G3D::Vector3& outNormal) const;

        // Get all model instances that a cylinder might collide with
        void GetCylinderCollisionCandidates(const Cylinder& cyl,
            std::vector<ModelInstance*>& outInstances) const;

        // Utility functions
        static uint32_t packTileID(uint32_t tileX, uint32_t tileY);
        static void unpackTileID(uint32_t ID, uint32_t& tileX, uint32_t& tileY);
        static std::string getTileFileName(uint32_t mapID, uint32_t tileX, uint32_t tileY);
        static bool CanLoadMap(const std::string& vmapPath, uint32_t mapID, uint32_t tileX, uint32_t tileY);

        // Getters
        bool isTiled() const;
        uint32_t numLoadedTiles() const;

        // New lightweight accessors for query facade (read-only)
        inline const BIH* GetBIHTree() const { return &iTree; }
        inline const ModelInstance* GetInstancesPtr() const { return iTreeValues; }
        inline uint32_t GetInstanceCount() const { return iNTreeValues; }

#ifdef MMAP_GENERATOR
        void getModelInstances(ModelInstance*& models, uint32_t& count);
#endif
    };
}