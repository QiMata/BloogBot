// VMapManager2.h - Enhanced with cylinder collision support
#pragma once

#include "IVMapManager.h"
#include <unordered_map>
#include <unordered_set>
#include <memory>
#include <shared_mutex>
#include <string>
#include "Vector3.h"
#include "CylinderCollision.h"

namespace VMAP
{
    class StaticMapTree;
    class WorldModel;
    class ModelInstance;

    typedef std::unordered_map<uint32_t, StaticMapTree*> InstanceTreeMap;
    typedef std::unordered_map<std::string, std::shared_ptr<WorldModel>> ModelFileMap;

    class VMapManager2 : public IVMapManager
    {
    protected:
        // Tree to check collision
        ModelFileMap iLoadedModelFiles;
        InstanceTreeMap iInstanceMapTrees;
        std::unordered_set<uint32_t> iLoadedMaps;
        std::string iBasePath;

        bool _loadMap(uint32_t pMapId, const std::string& basePath, uint32_t tileX, uint32_t tileY);

        mutable std::shared_mutex m_modelsLock;

    public:
        // public for debug
        G3D::Vector3 convertPositionToInternalRep(float x, float y, float z) const;
        static std::string getMapFileName(unsigned int pMapId);

        VMapManager2();
        ~VMapManager2();

        // Map management
        void setBasePath(const std::string& path);

        void initializeMap(uint32_t mapId);
        bool isMapInitialized(uint32_t mapId) const {
            return iLoadedMaps.count(mapId) > 0;
        }

        // IVMapManager interface implementation
        VMAPLoadResult loadMap(const char* pBasePath, unsigned int pMapId, int x, int y) override;
        void unloadMap(unsigned int pMapId, int x, int y) override;
        void unloadMap(unsigned int pMapId) override;

        bool isInLineOfSight(unsigned int pMapId, float x1, float y1, float z1,
            float x2, float y2, float z2, bool ignoreM2Model) override;
        ModelInstance* FindCollisionModel(unsigned int mapId, float x0, float y0, float z0,
            float x1, float y1, float z1) override;
        bool getObjectHitPos(unsigned int pMapId, float x1, float y1, float z1,
            float x2, float y2, float z2,
            float& rx, float& ry, float& rz, float pModifyDist) override;
        float getHeight(unsigned int pMapId, float x, float y, float z, float maxSearchDist) override;

        bool processCommand(char* /*pCommand*/) override { return false; }

        bool getAreaInfo(unsigned int pMapId, float x, float y, float& z,
            uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const override;
        bool isUnderModel(unsigned int pMapId, float x, float y, float z,
            float* outDist = nullptr, float* inDist = nullptr) const override;
        bool GetLiquidLevel(uint32_t pMapId, float x, float y, float z,
            uint8_t ReqLiquidTypeMask, float& level, float& floor, uint32_t& type) const override;

        std::shared_ptr<WorldModel> acquireModelInstance(const std::string& basepath, const std::string& filename);

        // New cylinder collision methods
        CylinderIntersection IntersectCylinder(unsigned int pMapId, const Cylinder& worldCylinder) const;
        std::vector<CylinderSweepHit> SweepCylinder(unsigned int pMapId, const Cylinder& worldCylinder,
            const G3D::Vector3& sweepDir, float sweepDistance) const;
        bool CheckCylinderCollision(unsigned int pMapId, const Cylinder& worldCylinder,
            float& outContactHeight, G3D::Vector3& outContactNormal,
            ModelInstance** outHitInstance = nullptr) const;
        bool CanCylinderFitAtPosition(unsigned int pMapId, const Cylinder& worldCylinder,
            float tolerance = 0.05f) const;

        // Find walkable surface for cylinder movement
        bool FindCylinderWalkableSurface(unsigned int pMapId, const Cylinder& worldCylinder,
            float currentHeight, float maxStepUp, float maxStepDown,
            float& outHeight, G3D::Vector3& outNormal) const;

        // Get height using cylinder for more accurate ground detection
        // NOTE: This is non-const because it calls the non-const getHeight() method
        float GetCylinderHeight(unsigned int pMapId, float x, float y, float z,
            float cylinderRadius, float cylinderHeight, float maxSearchDist);

        // Check if cylinder path is clear
        bool IsCylinderPathClear(unsigned int pMapId, const Cylinder& startCylinder,
            const G3D::Vector3& endPos, float stepHeight = 2.3f) const;

        // Get all collision candidates for a cylinder
        void GetCylinderCollisionCandidates(unsigned int pMapId, const Cylinder& worldCylinder,
            std::vector<ModelInstance*>& outInstances) const;

        // Convert cylinder between coordinate systems
        Cylinder ConvertCylinderToInternal(const Cylinder& worldCylinder) const;
        Cylinder ConvertCylinderToWorld(const Cylinder& internalCylinder) const;

        // Multi-map cylinder operations
        CylinderIntersection IntersectCylinderAllMaps(const Cylinder& worldCylinder) const;
        bool CanCylinderFitAllMaps(const Cylinder& worldCylinder, float tolerance = 0.05f) const;
    };
}