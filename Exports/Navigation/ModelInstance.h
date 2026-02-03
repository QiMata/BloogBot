// ModelInstance.h
#pragma once

#include <memory>
#include <string>
#include <vector>
#include "Vector3.h"
#include "AABox.h"
#include "Ray.h"
#include "Matrix3.h"
#include "VMapDefinitions.h"  // For MOD_M2 and other constants

namespace VMAP
{
    // Forward declarations
    class WorldModel;
    class GroupModel;
    struct AreaInfo;
    struct GroupLocationInfo;

    // LocationInfo defined here to avoid duplication
    struct LocationInfo
    {
        const class ModelInstance* hitInstance;
        const GroupModel* hitModel;
        float ground_Z;
        int32_t rootId;

        LocationInfo() : hitInstance(nullptr), hitModel(nullptr),
            ground_Z(-G3D::inf()), rootId(-1) {
        }
    };

    class ModelSpawn
    {
    public:
        uint32_t flags;
        uint16_t adtId;
        uint32_t ID;
        G3D::Vector3 iPos;
        G3D::Vector3 iRot; // spawn rotation euler (degrees)
        float iScale;
        G3D::AABox iBound;
        std::string name;

        static bool readFromFile(FILE* rf, ModelSpawn& spawn);
        const G3D::AABox& getBounds() const { return iBound; }
    };

    class ModelInstance : public ModelSpawn
    {
    public:
        G3D::Matrix3 iInvRot; // world->model rotation
        G3D::Matrix3 iRot;    // model->world rotation (cached)
        float iInvScale;
        std::shared_ptr<WorldModel> iModel;

        ModelInstance();
        ModelInstance(const ModelSpawn& spawn, std::shared_ptr<WorldModel> model);

        // Original ray-based collision methods
        bool intersectRay(const G3D::Ray& ray, float& maxDist, bool stopAtFirstHit, bool ignoreM2Model = false) const;
        void intersectPoint(const G3D::Vector3& p, AreaInfo& info) const;
        bool GetLocationInfo(const G3D::Vector3& p, LocationInfo& info) const;
        bool GetLiquidLevel(const G3D::Vector3& p, LocationInfo& info, float& liqHeight) const;
        void setUnloaded() { iModel = nullptr; }
        void getAreaInfo(G3D::Vector3& pos, uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const;

        // Collision mask source-of-truth for this instance
        void SetCollisionMask(uint32_t mask) { collisionMask = mask; }
        uint32_t GetCollisionMask() const { return collisionMask; }

        // Transform vertex from model to world space
        G3D::Vector3 TransformToWorld(const G3D::Vector3& modelVertex) const;

    private:
        // Per-instance collision mask (default: all bits set). Later may map from materials.
        uint32_t collisionMask = 0xFFFFFFFFu;
    };
}