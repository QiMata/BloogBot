#include "ModelInstance.h"
#include "WorldModel.h"
#include "VMapDefinitions.h"
#include "CylinderCollision.h"
#include <iostream>
#include <algorithm>

namespace VMAP
{
    bool ModelSpawn::readFromFile(FILE* rf, ModelSpawn& spawn)
    {
        if (!rf)
        {
            std::cerr << "[ModelSpawn] ERROR: NULL file pointer" << std::endl;
            return false;
        }
        uint32_t check = 0;

        // Read flags
        check += fread(&spawn.flags, sizeof(uint32_t), 1, rf);

        // EoF check
        if (!check)
        {
            if (ferror(rf))
                std::cerr << "[ModelSpawn] Error reading ModelSpawn!" << std::endl;
            return false;
        }

        // Read basic data
        check += fread(&spawn.adtId, sizeof(uint16_t), 1, rf);
        check += fread(&spawn.ID, sizeof(uint32_t), 1, rf);

        // FIXED: Read position as 3 floats in ONE call (matching reference)
        check += fread(&spawn.iPos, sizeof(float), 3, rf);

        // FIXED: Read rotation as 3 floats in ONE call (matching reference)
        check += fread(&spawn.iRot, sizeof(float), 3, rf);

        check += fread(&spawn.iScale, sizeof(float), 1, rf);

        // Read bounding box if present
        bool has_bound = (spawn.flags & MOD_HAS_BOUND) != 0;
        if (has_bound)
        {
            // FIXED: Read as two Vector3 directly (matching reference)
            G3D::Vector3 bLow, bHigh;
            check += fread(&bLow, sizeof(float), 3, rf);
            check += fread(&bHigh, sizeof(float), 3, rf);
            spawn.iBound = G3D::AABox(bLow, bHigh);
        }

        // Read name length
        uint32_t nameLen;
        check += fread(&nameLen, sizeof(uint32_t), 1, rf);

        // FIXED: Validate read count (matching reference)
        if (check != uint32_t(has_bound ? 17 : 11))
        {
            std::cerr << "[ModelSpawn] Error reading ModelSpawn!" << std::endl;
            return false;
        }

        // Sanity check name length
        if (nameLen > 500)
        {
            std::cerr << "[ModelSpawn] Error: Name too long: " << nameLen << std::endl;
            return false;
        }

        // FIXED: Use fixed buffer like reference (avoids dynamic allocation)
        char nameBuff[500];
        check = fread(nameBuff, sizeof(char), nameLen, rf);
        if (check != nameLen)
        {
            std::cerr << "[ModelSpawn] Error reading name string!" << std::endl;
            return false;
        }

        spawn.name = std::string(nameBuff, nameLen);

        return true;
    }

    ModelInstance::ModelInstance()
        : iInvScale(0), iModel(nullptr)
    {
    }

    ModelInstance::ModelInstance(const ModelSpawn& spawn, std::shared_ptr<WorldModel> model)
        : ModelSpawn(spawn), iModel(model)
    {
        iInvRot = G3D::Matrix3::fromEulerAnglesZYX(
            G3D::pi() * iRot.y / 180.f,  // z rotation
            G3D::pi() * iRot.x / 180.f,  // y rotation  
            G3D::pi() * iRot.z / 180.f   // x rotation
        ).inverse();
        iInvScale = 1.f / iScale;
    }

    bool ModelInstance::intersectRay(const G3D::Ray& ray, float& maxDist,
        bool stopAtFirstHit, bool ignoreM2Model) const
    {
        if (!iModel)
        {
            return false;
        }

        float time = ray.intersectionTime(iBound);
        if (time == G3D::inf())
        {
            return false;
        }

        // child bounds are defined in object space:
        G3D::Vector3 p = iInvRot * (ray.origin() - iPos) * iInvScale;
        G3D::Ray modRay(p, iInvRot * ray.direction());
        float distance = maxDist * iInvScale;

        bool hit = iModel->IntersectRay(modRay, distance, stopAtFirstHit, ignoreM2Model);

        if (hit)
        {
            distance *= iScale;
            maxDist = distance;
        }

        return hit;
    }

    void ModelInstance::intersectPoint(const G3D::Vector3& p, AreaInfo& info) const
    {
        if (!iModel)
        {
            return;
        }

        // M2 files don't contain area info, only WMO files
        if (flags & MOD_M2)
            return;

        if (!iBound.contains(p))
            return;

        // child bounds are defined in object space:
        G3D::Vector3 pModel = iInvRot * (p - iPos) * iInvScale;
        G3D::Vector3 zDirModel = iInvRot * G3D::Vector3(0, 0, -1);  // Vector3::down()
        float zDist = 10000.0f;

        if (iModel->IntersectPoint(pModel, zDirModel, zDist, info))
        {
            G3D::Vector3 modelGround = pModel + zDirModel * zDist;
            // Transform back to world space. Note that:
            // Mat * vec == vec * Mat.transpose()
            // and for rotation matrices: Mat.inverse() == Mat.transpose()
            float world_Z = ((modelGround * iInvRot) * iScale + iPos).z;
            if (info.ground_Z < world_Z)
            {
                info.ground_Z = world_Z;
                info.adtId = adtId;
            }
        }
    }

    bool ModelInstance::GetLocationInfo(const G3D::Vector3& p, LocationInfo& info) const
    {
        if (!iModel || (flags & MOD_M2))
            return false;
        if (!iBound.contains(p))
            return false;

        G3D::Vector3 pModel = iInvRot * (p - iPos) * iInvScale;
        G3D::Vector3 zDirModel = iInvRot * G3D::Vector3(0, 0, -1);
        float zDist = 10000.0f;
        GroupLocationInfo groupInfo;

        if (iModel->GetLocationInfo(pModel, zDirModel, zDist, groupInfo))
        {
            G3D::Vector3 modelGround = pModel + zDirModel * zDist;
            float world_Z = ((modelGround * iInvRot) * iScale + iPos).z;
            if (info.ground_Z < world_Z)
            {
                info.rootId = groupInfo.rootId;
                info.hitModel = groupInfo.hitModel;
                info.ground_Z = world_Z;
                info.hitInstance = this;
                return true;
            }
        }
        return false;
    }

    bool ModelInstance::GetLiquidLevel(const G3D::Vector3& p, LocationInfo& info,
        float& liqHeight) const
    {
        if (!info.hitModel)
            return false;

        G3D::Vector3 pModel = iInvRot * (p - iPos) * iInvScale;
        if (info.hitModel->GetLiquidLevel(pModel, liqHeight))
        {
            liqHeight = (G3D::Vector3(pModel.x, pModel.y, liqHeight) * iInvRot * iScale + iPos).z;
            return true;
        }
        return false;
    }

    void ModelInstance::getAreaInfo(G3D::Vector3& pos, uint32_t& flags,
        int32_t& adtId, int32_t& rootId, int32_t& groupId) const
    {
        if (!iModel || (this->flags & MOD_M2))
            return;

        if (!iBound.contains(pos))
            return;

        G3D::Vector3 pModel = iInvRot * (pos - iPos) * iInvScale;
        G3D::Vector3 zDirModel = iInvRot * G3D::Vector3(0, 0, -1);
        float zDist = 10000.0f;

        AreaInfo info;
        if (iModel->IntersectPoint(pModel, zDirModel, zDist, info))
        {
            flags = info.flags;
            adtId = this->adtId;
            rootId = info.rootId;
            groupId = info.groupId;

            G3D::Vector3 modelGround = pModel + zDirModel * zDist;
            float world_Z = ((modelGround * iInvRot) * iScale + iPos).z;
            if (pos.z > world_Z)
                pos.z = world_Z;
        }
    }

    // Transform a vertex from model space to world space
    G3D::Vector3 ModelInstance::TransformToWorld(const G3D::Vector3& modelVertex) const
    {
        // Apply scale, then rotation, then translation
        // Note: iInvRot is the inverse rotation, so we need to transpose it (which equals inverse for rotation matrices)
        return (modelVertex * iScale) * iInvRot + iPos;
    }

    // Transform cylinder from world space to model space
    Cylinder ModelInstance::TransformCylinderToModel(const Cylinder& worldCylinder) const
    {
        // Transform base position to model space
        G3D::Vector3 modelBase = iInvRot * (worldCylinder.base - iPos) * iInvScale;

        // Transform axis (just rotate, no translation)
        G3D::Vector3 modelAxis = iInvRot * worldCylinder.axis;

        // Scale radius and height
        float modelRadius = worldCylinder.radius * iInvScale;
        float modelHeight = worldCylinder.height * iInvScale;

        return Cylinder(modelBase, modelAxis, modelRadius, modelHeight);
    }

    // Helper function to compute closest point on AABB to a point
    static G3D::Vector3 ClosestPointOnAABox(const G3D::AABox& box, const G3D::Vector3& point)
    {
        G3D::Vector3 result;
        result.x = std::max(box.low().x, std::min(point.x, box.high().x));
        result.y = std::max(box.low().y, std::min(point.y, box.high().y));
        result.z = std::max(box.low().z, std::min(point.z, box.high().z));
        return result;
    }

    // Check cylinder collision with this model instance
    CylinderIntersection ModelInstance::IntersectCylinder(const Cylinder& worldCylinder) const
    {
        CylinderIntersection result;

        if (!iModel)
            return result;

        // Quick bounds check
        if (!iBound.intersects(worldCylinder.getBounds()))
            return result;

        // For now, return a basic intersection based on bounds
        // A full implementation would need to access the model's triangle data
        // and perform proper cylinder-triangle intersection tests

        // This is a simplified implementation - you would need to enhance WorldModel
        // to expose its mesh data for proper cylinder collision
        result.hit = iBound.intersects(worldCylinder.getBounds());
        if (result.hit)
        {
            // Approximate contact point as closest point on bounds
            G3D::Vector3 cylCenter = worldCylinder.getCenter();
            result.contactPoint = ClosestPointOnAABox(iBound, cylCenter);
            result.contactHeight = result.contactPoint.z;

            // Calculate and normalize contact normal
            G3D::Vector3 normal = cylCenter - result.contactPoint;
            float length = normal.magnitude();
            if (length > 0.0001f)
            {
                result.contactNormal = normal / length;
            }
            else
            {
                result.contactNormal = G3D::Vector3(0, 0, 1);
            }

            result.penetrationDepth = 0.0f;
        }

        return result;
    }

    // Sweep a cylinder through this model
    std::vector<CylinderSweepHit> ModelInstance::SweepCylinder(const Cylinder& worldCylinder,
        const G3D::Vector3& sweepDir, float sweepDistance) const
    {
        std::vector<CylinderSweepHit> hits;

        if (!iModel)
            return hits;

        // Create swept bounds for broad phase
        G3D::AABox sweepBounds = worldCylinder.getBounds();
        Cylinder endCyl(worldCylinder.base + sweepDir * sweepDistance,
            worldCylinder.axis, worldCylinder.radius, worldCylinder.height);
        sweepBounds.merge(endCyl.getBounds());

        // Quick bounds check
        if (!iBound.intersects(sweepBounds))
            return hits;

        // This would need full implementation with access to model's mesh data
        // For now, return empty or implement simplified version

        return hits;
    }

    // Get transformed mesh data for external collision testing
    bool ModelInstance::GetTransformedMeshData(std::vector<G3D::Vector3>& outVertices,
        std::vector<uint32_t>& outIndices) const
    {
        if (!iModel)
            return false;

        // Get the raw mesh data from the WorldModel
        std::vector<G3D::Vector3> modelVertices;
        if (!iModel->GetAllMeshData(modelVertices, outIndices))
            return false;

        // Clear and reserve space for transformed vertices
        outVertices.clear();
        outVertices.reserve(modelVertices.size());

        // Transform each vertex from model space to world space
        // Transformation: scale -> rotate -> translate
        // Since iInvRot is the inverse rotation matrix, we need to transpose it
        // (which equals inverse for rotation matrices) to get the forward rotation
        auto vertIt = modelVertices.begin();
        while (vertIt != modelVertices.end())
        {
            // Apply scale
            G3D::Vector3 scaled = (*vertIt) * iScale;

            // Apply rotation (iInvRot is inverse, so we transpose it back)
            // For a rotation matrix, inverse = transpose, so to get original rotation
            // from inverse, we transpose again
            // Mat * vec == vec * Mat.transpose()
            G3D::Vector3 rotated = scaled * iInvRot;

            // Apply translation
            G3D::Vector3 worldPos = rotated + iPos;

            outVertices.push_back(worldPos);
            ++vertIt;
        }

        return true;
    }

    // Check if cylinder collides with model
    bool ModelInstance::CheckCylinderCollision(const Cylinder& worldCylinder,
        float& outContactHeight, G3D::Vector3& outContactNormal) const
    {
        if (!iModel)
            return false;

        // Quick bounds check first
        if (!iBound.intersects(worldCylinder.getBounds()))
            return false;

        // Transform cylinder to model space for testing
        Cylinder modelCylinder = TransformCylinderToModel(worldCylinder);

        // This would need proper implementation with mesh data access
        // For now, use bounds-based approximation
        G3D::Vector3 cylCenter = worldCylinder.getCenter();
        if (iBound.contains(cylCenter))
        {
            outContactHeight = iBound.high().z;
            outContactNormal = G3D::Vector3(0, 0, 1);
            return true;
        }

        return false;
    }

    // Test if cylinder can fit at position without collision
    bool ModelInstance::CanCylinderFitAtPosition(const Cylinder& worldCylinder, float tolerance) const
    {
        if (!iModel)
            return true;  // No model = no collision

        // Manually expand bounds by tolerance
        G3D::AABox expandedCylBounds = worldCylinder.getBounds();
        G3D::Vector3 expansion(tolerance, tolerance, tolerance);
        G3D::AABox expandedBounds(
            expandedCylBounds.low() - expansion,
            expandedCylBounds.high() + expansion
        );

        return !iBound.intersects(expandedBounds);
    }
}