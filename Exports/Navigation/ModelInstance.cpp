#include "ModelInstance.h"
#include "WorldModel.h"
#include "VMapDefinitions.h"
#include "CylinderCollision.h"
#include <iostream>
#include <algorithm>
#include "VMapLog.h"
#include "CoordinateTransforms.h"

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
        // Compute world->model rotation from spawn Euler angles (degrees)
        const G3D::Vector3& eulerDeg = this->ModelSpawn::iRot;
        iInvRot = G3D::Matrix3::fromEulerAnglesZYX(
            G3D::pi() * eulerDeg.y / 180.f,  // z rotation
            G3D::pi() * eulerDeg.x / 180.f,  // y rotation  
            G3D::pi() * eulerDeg.z / 180.f   // x rotation
        ).inverse();
        // Cache model->world rotation once
        iRot = iInvRot.inverse();
        // Precompute inverse scale
        iInvScale = 1.f / iScale;
    }

    bool ModelInstance::intersectRay(const G3D::Ray& ray, float& maxDist,
        bool stopAtFirstHit, bool ignoreM2Model) const
    {
        if (!iModel)
        {
            return false;
        }

        // Log incoming ray and instance transform for debug correlation
        G3D::Vector3 rayOrgI = ray.origin();
        G3D::Vector3 rayDirI = ray.direction();
        G3D::Vector3 rayOrgW = NavCoord::InternalToWorld(rayOrgI);
        G3D::Vector3 rayDirW = NavCoord::InternalDirToWorld(rayDirI);
        G3D::Vector3 instPosI = iPos; // instance position stored in internal
        G3D::Vector3 instPosW = NavCoord::InternalToWorld(instPosI);

        PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] enter instId=" << ID << " name='" << name << "'"
            << " rayOrgI=(" << rayOrgI.x << "," << rayOrgI.y << "," << rayOrgI.z << ")"
            << " rayOrgW=(" << rayOrgW.x << "," << rayOrgW.y << "," << rayOrgW.z << ")"
            << " rayDirI=(" << rayDirI.x << "," << rayDirI.y << "," << rayDirI.z << ")"
            << " rayDirW=(" << rayDirW.x << "," << rayDirW.x << "," << rayDirW.z << ")"
            << " instPosI=(" << instPosI.x << "," << instPosI.y << "," << instPosI.z << ")"
            << " instPosW=(" << instPosW.x << "," << instPosW.y << "," << instPosW.z << ")"
            << " iScale=" << iScale << " iRotEulerDeg=(" << ModelSpawn::iRot.x << "," << ModelSpawn::iRot.y << "," << ModelSpawn::iRot.z << ")");

        float time = ray.intersectionTime(iBound);
        if (time == G3D::inf())
        {
            PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] early-exit bounds miss instId=" << ID);
            return false;
        }

        // Transform ray into model space correctly accounting for rotation and scale
        // Transform origin: translate into instance internal-space then rotate and scale into model-space
        G3D::Vector3 p = iInvRot * (ray.origin() - iPos) * iInvScale;
        // Transform direction: rotate then scale (directions do not translate)
        G3D::Vector3 d = iInvRot * ray.direction();
        d = d * iInvScale; // apply inverse scale to direction so model-space dir and distance are consistent

        G3D::Ray modRay(p, d);
        // Compute model-space max distance corresponding to world-space maxDist
        float distance = maxDist * iInvScale;

        // Log model-space ray (modRay) and a world-space approximation for its origin/direction
        G3D::Vector3 modOrg = modRay.origin();
        G3D::Vector3 modDir = modRay.direction();
        // Compute an approximate world-space origin/direction by first mapping the model-space point into internal
        // then converting the internal result into world-space. This ensures labels match values in logs.
        G3D::Vector3 modOrgInternal = (modOrg * iScale) * iRot + iPos; // internal-space position
        G3D::Vector3 modOrgW = NavCoord::InternalToWorld(modOrgInternal);
        G3D::Vector3 modDirInternal = (modDir * iScale) * iRot; // direction in internal space
        G3D::Vector3 modDirW = NavCoord::InternalDirToWorld(modDirInternal); // convert to world direction
        PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] modRay.modelOrg=(" << modOrg.x << "," << modOrg.y << "," << modOrg.z << ")" 
            << " modRay.modelDir=(" << modDir.x << "," << modDir.y << "," << modDir.z << ")"
            << " modOrgWApprox=(" << modOrgW.x << "," << modOrgW.y << "," << modOrgW.z << ")"
            << " modDirWApprox=(" << modDirW.x << "," << modDirW.y << "," << modDirW.z << ")"
            << " distanceModelSpace=" << distance << " maxDistWorldIn=" << maxDist);

        bool hit = iModel->IntersectRay(modRay, distance, stopAtFirstHit, ignoreM2Model);

        if (hit)
        {
            // distance is in model-space. Compute the model-space hit point and convert to world BEFORE scaling the distance
            G3D::Vector3 modelHitPoint = modRay.origin() + modRay.direction() * distance;
            G3D::Vector3 internalHitPoint = (modelHitPoint * iScale) * iRot + iPos; // internal-space
            G3D::Vector3 worldHitPoint = NavCoord::InternalToWorld(internalHitPoint);

            // Log the model hit location and the world-space converted point
            PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] hit instId=" << ID << " triHitModelDistance=" << distance
                << " modelHitPoint=(" << modelHitPoint.x << "," << modelHitPoint.y << "," << modelHitPoint.z << ")"
                << " internalHitPoint=(" << internalHitPoint.x << "," << internalHitPoint.y << "," << internalHitPoint.z << ")"
                << " worldHitPoint=(" << worldHitPoint.x << "," << worldHitPoint.y << "," << worldHitPoint.z << ")");

            // Convert model-space distance back to world-space distance and set maxDist accordingly
            distance *= iScale;
            maxDist = distance;

            PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] returning hit maxDistWorld=" << maxDist);
        }
        else
        {
            PHYS_TRACE(PHYS_CYL, "[MI::intersectRay] no hit instId=" << ID);
        }

        if (hit)
        {
            return true;
        }

        return false;
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
            // Transform back to world space using model->world rotation (iRot)
            float world_Z = TransformToWorld(modelGround).z;
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
            float world_Z = TransformToWorld(modelGround).z;
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
            // Use TransformToWorld to correctly compute world-space z
            liqHeight = TransformToWorld(G3D::Vector3(pModel.x, pModel.y, liqHeight)).z;
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
            float world_Z = TransformToWorld(modelGround).z;
            if (pos.z > world_Z)
                pos.z = world_Z;
        }
    }

    // Transform a vertex from model space to world space (now returns true world-space coords)
    G3D::Vector3 ModelInstance::TransformToWorld(const G3D::Vector3& modelVertex) const
    {
        // Compute internal-space global position then convert to world-space
        G3D::Vector3 internalPos = (modelVertex * iScale) * iRot + iPos;
        return NavCoord::InternalToWorld(internalPos);
    }

    // Transform cylinder from world space to model space
    Cylinder ModelInstance::TransformCylinderToModel(const Cylinder& worldCylinder) const
    {
        // Convert world base and axis into internal-space first
        G3D::Vector3 baseInternal = NavCoord::WorldToInternal(worldCylinder.base);
        G3D::Vector3 axisInternal = NavCoord::WorldDirToInternal(worldCylinder.axis);

        // Transform base position to model space (internal -> model)
        G3D::Vector3 modelBase = iInvRot * (baseInternal - iPos) * iInvScale;

        // Transform axis (just rotate, no translation)
        G3D::Vector3 modelAxis = iInvRot * axisInternal;

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

        // Transform cylinder into model space and perform precise triangle test
        Cylinder modelCylinder = TransformCylinderToModel(worldCylinder);
        CylinderIntersection modelHit = iModel->IntersectCylinder(modelCylinder);

        if (modelHit.hit)
        {
            // Transform contact point back to instance/world space
            G3D::Vector3 internalWorldPt = (modelHit.contactPoint * iScale) * iRot + iPos; // internal-space
            G3D::Vector3 worldPt = NavCoord::InternalToWorld(internalWorldPt);
            // Transform normal (direction only) using model->world rotation and convert to world-space
            G3D::Vector3 worldN_internal = modelHit.contactNormal * iRot;
            G3D::Vector3 worldN = NavCoord::InternalDirToWorld(worldN_internal);
            float nLen = worldN.magnitude();
            if (nLen > 0.0001f)
                worldN /= nLen;

            result = modelHit;
            result.contactPoint = worldPt;
            result.contactHeight = worldPt.z;
            result.contactNormal = worldN;
            result.instanceId = ID;

            LOG_INFO("[MI][IntersectCylinder] name='" << name << "' id=" << ID
                << " adt=" << adtId << " hit=1 mesh=1 contactZ=" << result.contactHeight
                << " nZ_model=" << modelHit.contactNormal.z << " nZ_world=" << result.contactNormal.z);
        }
        else
        {
            LOG_DEBUG("[MI][IntersectCylinder] name='" << name << "' id=" << ID
                << " adt=" << adtId << " hit=0 mesh=0 boundsIntersect=1");
        }

        return result;
    }

    // Sweep a cylinder through this model
    std::vector<CylinderSweepHit> ModelInstance::SweepCylinder(const Cylinder& worldCylinder,
        const G3D::Vector3& sweepDir, float sweepDistance) const
    {
        std::vector<CylinderSweepHit> hits;

        if (!iModel)
        {
            return hits;
        }

        // Create swept bounds for broad phase
        G3D::AABox sweepBounds = worldCylinder.getBounds();
        Cylinder endCyl(worldCylinder.base + sweepDir * sweepDistance,
            worldCylinder.axis, worldCylinder.radius, worldCylinder.height);
        sweepBounds.merge(endCyl.getBounds());

        // Transform cylinder and sweep into model space
        Cylinder modelCylinder = TransformCylinderToModel(worldCylinder);
        // Convert sweep direction to internal then rotate to model-space
        G3D::Vector3 sweepDirInternal = NavCoord::WorldDirToInternal(sweepDir);
        G3D::Vector3 modelSweepDir = iInvRot * sweepDirInternal; // rotate direction only

        // Apply inverse scale to direction and distance so model-space sweep is consistent with model scaling
        modelSweepDir = modelSweepDir * iInvScale;
        float modelSweepDistance = sweepDistance * iInvScale;
        std::vector<CylinderSweepHit> modelHits = iModel->SweepCylinder(modelCylinder, modelSweepDir, modelSweepDistance);

        // Transform results back to instance/world space
        hits.reserve(modelHits.size());
        auto it = modelHits.begin();
        while (it != modelHits.end())
        {
            CylinderSweepHit h = *it;
            // Position/height: transform model-space vertex into internal then to world
            G3D::Vector3 internalPos = (h.position * iScale) * iRot + iPos;
            G3D::Vector3 wpos = NavCoord::InternalToWorld(internalPos);
            h.position = wpos;
            h.height = wpos.z;
            // Normal using model->world rotation then convert to world-space
            G3D::Vector3 wn_internal = h.normal * iRot;
            G3D::Vector3 wn = NavCoord::InternalDirToWorld(wn_internal);
            float nLen = wn.magnitude();
            if (nLen > 0.0001f) wn /= nLen; else wn = G3D::Vector3(0,0,1);
            h.normal = wn;
            h.q.normal = wn;
            h.q.instanceId = ID;

            // Triangle surface enrichment
            uint32_t triGlobal = h.triangleIndex;
            uint32_t cumulative = 0;
            const GroupModel* foundGroup = nullptr;
            uint32_t localTri = 0;
            for (uint32_t gi = 0; ; ++gi)
            {
                const GroupModel* gm = iModel->GetGroupModel(gi);
                if (!gm) break;
                uint32_t triCount = (uint32_t)gm->GetTriangles().size();
                if (triGlobal < cumulative + triCount)
                {
                    foundGroup = gm;
                    localTri = triGlobal - cumulative;
                    h.groupIndex = gi;
                    break;
                }
                cumulative += triCount;
            }
            if (foundGroup && localTri < foundGroup->GetTriangles().size())
            {
                const auto& tri = foundGroup->GetTriangles()[localTri];
                const auto& verts = foundGroup->GetVertices();
                if (tri.idx0 < verts.size() && tri.idx1 < verts.size() && tri.idx2 < verts.size())
                {
                    G3D::Vector3 v0m = verts[tri.idx0];
                    G3D::Vector3 v1m = verts[tri.idx1];
                    G3D::Vector3 v2m = verts[tri.idx2];
                    G3D::Vector3 v0w = TransformToWorld(v0m);
                    G3D::Vector3 v1w = TransformToWorld(v1m);
                    G3D::Vector3 v2w = TransformToWorld(v2m);
                    G3D::Vector3 nTri = CylinderHelpers::CalculateTriangleNormalOriented(v0w, v1w, v2w);
                    if (nTri.z < 0.0f) nTri = -nTri;
                    G3D::Vector3 centroid = (v0w + v1w + v2w) * (1.0f/3.0f);
                    h.triNormal = nTri;
                    h.triCentroid = centroid;
                }
            }

            hits.push_back(h);
            ++it;
        }

        if (!hits.empty())
            PHYS_TRACE(PHYS_CYL, "[MI::Sweep] hits="<<hits.size()<<" name='"<<name<<"' id="<<ID);
        return hits;
    }

    bool ModelInstance::GetTransformedMeshData(std::vector<G3D::Vector3>& outVertices,
        std::vector<uint32_t>& outIndices) const
    {
        if (!iModel)
            return false;

        std::vector<G3D::Vector3> modelVertices;
        if (!iModel->GetAllMeshData(modelVertices, outIndices))
            return false;

        outVertices.clear();
        outVertices.reserve(modelVertices.size());

        auto vertIt = modelVertices.begin();
        while (vertIt != modelVertices.end())
        {
            // Correct transformation: scale -> iRot -> translate, then convert to world
            G3D::Vector3 internalPos = ((*vertIt) * iScale) * iRot + iPos;
            G3D::Vector3 worldPos = NavCoord::InternalToWorld(internalPos);
            outVertices.push_back(worldPos);
            ++vertIt;
        }

        return true;
    }

    bool ModelInstance::CheckCylinderCollision(const Cylinder& worldCylinder,
        float& outContactHeight, G3D::Vector3& outContactNormal) const
    {
        if (!iModel)
            return false;

        if (!iBound.intersects(worldCylinder.getBounds()))
            return false;

        Cylinder modelCylinder = TransformCylinderToModel(worldCylinder);

        float ch = 0.0f; G3D::Vector3 n(0,0,1);
        bool hit = iModel->CheckCylinderCollision(modelCylinder, ch, n);
        if (hit)
        {
            G3D::Vector3 wn_internal = n * iRot;
            G3D::Vector3 wn = NavCoord::InternalDirToWorld(wn_internal);
            float nLen = wn.magnitude();
            if (nLen > 0.0001f) wn /= nLen; else wn = G3D::Vector3(0,0,1);

            // Transform contact height using TransformToWorld for correctness
            float worldZ = TransformToWorld(G3D::Vector3(0,0,ch)).z;

            outContactHeight = worldZ;
            outContactNormal = wn;

            LOG_INFO("[MI][CheckCylinderCollision] name='" << name << "' id=" << ID
                << " adt=" << adtId << " hit=1 ch=" << outContactHeight << " nZ=" << outContactNormal.z);
            return true;
        }

        return false;
    }

    bool ModelInstance::CanCylinderFitAtPosition(const Cylinder& worldCylinder, float tolerance) const
    {
        if (!iModel)
            return true;  // No model = no collision

        Cylinder expanded = worldCylinder;
        expanded.radius += tolerance;

        if (!iBound.intersects(expanded.getBounds()))
            return true;

        Cylinder modelCylinder = TransformCylinderToModel(expanded);
        bool ok = iModel->CanCylinderFitAtPosition(modelCylinder, 0.0f);
        LOG_DEBUG("[MI][CanFit] name='" << name << "' id=" << ID << " ok=" << (ok?1:0));
        return ok;
    }
}