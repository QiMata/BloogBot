#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "PhysicsEngine.h"
#include "MapLoader.h"
#include <algorithm>
#include <cmath>
#include <vector>
#include <cstdlib>
#include <mutex>
#include <sstream>
#include <unordered_map>

using namespace VMAP;

namespace
{
    // Helper function to compute barycentric coordinates for a point on a triangle
    static G3D::Vector3 ComputeBarycentric(const G3D::Vector3& p, const G3D::Vector3& a, const G3D::Vector3& b, const G3D::Vector3& c)
    {
        G3D::Vector3 v0 = b - a;
        G3D::Vector3 v1 = c - a;
        G3D::Vector3 v2 = p - a;
        float d00 = v0.dot(v0);
        float d01 = v0.dot(v1);
        float d11 = v1.dot(v1);
        float d20 = v2.dot(v0);
        float d21 = v2.dot(v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return G3D::Vector3(u, v, w);
    }

    // Classify where on capsule the contact occurred (endcap0, endcap1, side) given t in [0,1].
    static const char* ClassifyCapsuleRegion(float t)
    {
        const float capThresh = 0.15f; // heuristic
        if (t <= capThresh) return "CAP0";
        if (t >= 1.0f - capThresh) return "CAP1";
        return "SIDE";
    }

    // Stores triangles in MODEL-LOCAL space; broadphase still done in INTERNAL space like raycast.
    class MapMeshView : public CapsuleCollision::TriangleMeshView
    {
    public:
        MapMeshView(const BIH* tree, const ModelInstance* instances, uint32_t instanceCount, uint32_t includeMask = 0xFFFFFFFFu)
            : m_tree(tree), m_instances(instances), m_instanceCount(instanceCount), m_includeMask(includeMask)
        {
            m_cache.reserve(1024);
            m_triToInstance.reserve(1024);
            m_triToLocalTri.reserve(1024);
            // Diagnostics: log summary of available instances and BIH state
            {
                std::ostringstream oss; oss << "[MapMeshViewInit] instCount=" << m_instanceCount
                    << " includeMask=0x" << std::hex << m_includeMask << std::dec
                    << " BIH=" << (m_tree ? "present" : "null")
                    << " instancesPtr=" << (m_instances ? "present" : "null");
                // PHYS_INFO(PHYS_CYL, oss.str()); // commented out per request
            }
        }

        // Modified: input AABB is already internal-space. Removed world corner transforms and large Y inflate.
        void query(const CapsuleCollision::AABB& internalBox, int* outIndices, int& count, int maxCount) const override
        {
            count = 0;
            m_cache.clear();
            m_triToInstance.clear();
            m_triToLocalTri.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
            {
                std::ostringstream oss; oss << "[MapMeshViewQuerySkip] m_tree=" << (m_tree?1:0)
                    << " m_instances=" << (m_instances?1:0) << " instCount=" << m_instanceCount
                    << " outIndices=" << (outIndices?1:0) << " maxCount=" << maxCount;
                PHYS_INFO(PHYS_CYL, oss.str());
                return;
            }

            // Use internal min/max directly and apply small symmetric inflation (match ray epsilon intent).
            G3D::Vector3 qLo = { internalBox.min.x, internalBox.min.y, internalBox.min.z };
            G3D::Vector3 qHi = { internalBox.max.x, internalBox.max.y, internalBox.max.z };
            const float eps = 0.05f; // symmetric small epsilon
            G3D::AABox queryBox(qLo - G3D::Vector3(eps, eps, eps), qHi + G3D::Vector3(eps, eps, eps));
            // { std::ostringstream oss; oss << "[BroadphaseQuery] internalBoxLo=(" << qLo.x << "," << qLo.y << "," << qLo.z
            //     << ") internalBoxHi=(" << qHi.x << "," << qHi.y << "," << qHi.z << ") eps=" << eps << " instCount=" << m_instanceCount; PHYS_TRACE(PHYS_CYL, oss.str()); }

            // Instance roster diagnostic (sampled)
            {
                int logged = 0;
                for (uint32_t i = 0; i < m_instanceCount && logged < 16; ++i)
                {
                    const ModelInstance& inst = m_instances[i];
                    ++logged;
                }
                // PHYS_TRACE(PHYS_CYL, std::string("[InstanceRosterSummary] total=") << m_instanceCount << " logged=" << logged);
            }

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            bool bihOk = m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap);
            // { std::ostringstream oss; oss << "[BroadphaseQuery] BIH result ok=" << (bihOk?1:0) << " rawInstCount=" << instCount; PHYS_TRACE(PHYS_CYL, oss.str()); }
            if (!bihOk)
            {
                // PHYS_INFO(PHYS_CYL, "[BroadphaseQuery] BIH returned false; falling back to bound scan");
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    const ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if ((inst.GetCollisionMask() & m_includeMask) == 0) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i;
                }
                // { std::ostringstream oss; oss << "[BroadphaseQuery] Fallback AABB scan instCount=" << instCount; PHYS_TRACE(PHYS_CYL, oss.str()); }
                if (instCount == 0) return;
            }
            else
            {
                std::vector<char> present(m_instanceCount, 0);
                for (uint32_t k = 0; k < instCount; ++k)
                {
                    uint32_t idx = instIdx[k];
                    if (idx < m_instanceCount) present[idx] = 1;
                }
                // Log preliminary instance list
                for (uint32_t k = 0; k < instCount; ++k)
                {
                    uint32_t idx = instIdx[k];
                    if (idx >= m_instanceCount) continue;
                    const ModelInstance& inst = m_instances[idx];
                    // std::ostringstream oss; oss << "[BroadphaseInstPre] idx=" << idx << " ID=" << inst.ID << " mask=0x" << std::hex << inst.GetCollisionMask() << std::dec; PHYS_TRACE(PHYS_CYL, oss.str());
                }
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    if (present[i]) continue;
                    const ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if ((inst.GetCollisionMask() & m_includeMask) == 0) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i; present[i] = 1;
                }
            }

            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) continue;
                if (!inst.iBound.intersects(queryBox)) continue;

                uint32_t instMask = inst.GetCollisionMask();
                if ((instMask & m_includeMask) == 0) continue;

                // Convert internal query AABB to model local space for bounds filtering
                G3D::Vector3 wLoI = queryBox.low();
                G3D::Vector3 wHiI = queryBox.high();
                G3D::Vector3 corners[8] = {
                    {wLoI.x, wLoI.y, wLoI.z}, {wHiI.x, wLoI.y, wLoI.z}, {wLoI.x, wHiI.y, wLoI.z}, {wHiI.x, wHiI.y, wLoI.z},
                    {wLoI.x, wLoI.y, wHiI.z}, {wHiI.x, wLoI.y, wHiI.z}, {wLoI.x, wHiI.y, wHiI.z}, {wHiI.x, wHiI.y, wHiI.z}
                };
                G3D::Vector3 c0 = inst.iInvRot * ((corners[0] - inst.iPos) * inst.iInvScale);
                G3D::AABox modelBox(c0, c0);
                for (int ci = 1; ci < 8; ++ci)
                {
                    G3D::Vector3 pm = inst.iInvRot * ((corners[ci] - inst.iPos) * inst.iInvScale);
                    modelBox.merge(pm);
                }

                // Log instance bounds
                G3D::AABox instBound = inst.iBound;
                {
                    // std::ostringstream oss; oss << "[BroadphaseInst] idx=" << idx << " ID=" << inst.ID
                    //     << " mask=0x" << std::hex << instMask << std::dec
                    //     << " worldBoundLo=(" << instBound.low().x << "," << instBound.low().y << "," << instBound.low().z << ")"
                    //     << " worldBoundHi=(" << instBound.high().x << "," << instBound.high().y << "," << instBound.high().z << ")"
                    //     << " modelQueryLo=(" << modelBox.low().x << "," << modelBox.low().y << "," << modelBox.low().z << ")"
                    //     << " modelQueryHi=(" << modelBox.high().x << "," << modelBox.high().y << "," << modelBox.high().z << ")"; // PHYS_TRACE(PHYS_CYL, oss.str());
                }

                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData)
                {
                    if (!inst.iModel->GetAllMeshData(vertices, indices)) {
                        // std::ostringstream oss; oss << "[BroadphaseInst] idx=" << idx << " ID=" << inst.ID << " noMeshData"; // PHYS_TRACE(PHYS_CYL, oss.str());
                        continue;
                    }
                }

                size_t triCount = indices.size() / 3;
                // { std::ostringstream oss; oss << "[BroadphaseInst] idx=" << idx << " ID=" << inst.ID << " verts=" << vertices.size() << " trisRaw=" << triCount << " boundsFiltered=" << (haveBoundsData?1:0); PHYS_TRACE(PHYS_CYL, oss.str()); }

                size_t acceptedThisInst = 0;
                for (size_t t = 0; t < triCount; ++t)
                {
                    uint32_t i0 = indices[t * 3 + 0];
                    uint32_t i1 = indices[t * 3 + 1];
                    uint32_t i2 = indices[t * 3 + 2];
                    if (i0 >= vertices.size() || i1 >= vertices.size() || i2 >= vertices.size()) continue;
                    const G3D::Vector3& a = vertices[i0];
                    const G3D::Vector3& b = vertices[i1];
                    const G3D::Vector3& c = vertices[i2];

                    if (haveBoundsData)
                    {
                        G3D::Vector3 lo = a.min(b).min(c);
                        G3D::Vector3 hi = a.max(b).max(c);
                        G3D::AABox triBox(lo, hi);
                        if (!triBox.intersects(modelBox)) continue;
                    }

                    // Store model-local verts
                    CapsuleCollision::Triangle T; T.a = { a.x, a.y, a.z }; T.b = { b.x, b.y, b.z }; T.c = { c.x, c.y, c.z }; T.doubleSided = false; T.collisionMask = instMask;
                    int triIndex = (int)m_cache.size();
                    m_cache.push_back(T); m_triToInstance.push_back(idx); m_triToLocalTri.push_back((int)t);
                    if (count < maxCount) outIndices[count++] = triIndex; else break;

                    if (acceptedThisInst < 8) { // log first few sample triangles
                        std::ostringstream oss; oss << "[BroadphaseTriSample] instID=" << inst.ID << " localTri=" << t
                            << " a=(" << a.x << "," << a.y << "," << a.z << ") b=(" << b.x << "," << b.y << "," << b.z << ") c=(" << c.x << "," << c.y << "," << c.z << ")"; 
                        // PHYS_TRACE(PHYS_CYL, oss.str()); // commented out per request
                    }
                    ++acceptedThisInst;
                }
                // { std::ostringstream oss; oss << "[BroadphaseInstSummary] ID=" << inst.ID << " acceptedTris=" << acceptedThisInst << " totalOutCount=" << count; PHYS_TRACE(PHYS_CYL, oss.str()); }
                if (count >= maxCount) break;
            }
            // { std::ostringstream oss; oss << "[BroadphaseQuery] FinalTriangleCount=" << count; PHYS_TRACE(PHYS_CYL, oss.str()); }
        }

        // Internal-space variant delegates to query now (keep signature for existing callers)
        void queryInternal(const CapsuleCollision::AABB& internalBox, int* outIndices, int& count, int maxCount) const
        {
            query(internalBox, outIndices, count, maxCount);
        }

        const CapsuleCollision::Triangle& tri(int idx) const override { return m_cache[idx]; }
        int triangleCount() const override { return (int)m_cache.size(); }

        const ModelInstance* triInstance(int triIdx) const
        {
            if (triIdx < 0 || (size_t)triIdx >= m_triToInstance.size()) return nullptr;
            uint32_t instIdx = m_triToInstance[triIdx];
            if (!m_instances || instIdx >= m_instanceCount) return nullptr;
            return &m_instances[instIdx];
        }
        int triLocalIndex(int triIdx) const
        {
            if (triIdx < 0 || (size_t)triIdx >= m_triToLocalTri.size()) return -1;
            return m_triToLocalTri[triIdx];
        }

    private:
        const BIH* m_tree;
        const ModelInstance* m_instances;
        uint32_t m_instanceCount;
        uint32_t m_includeMask;
        mutable std::vector<CapsuleCollision::Triangle> m_cache; // MODEL-LOCAL vertices stored
        mutable std::vector<uint32_t> m_triToInstance;
        mutable std::vector<int> m_triToLocalTri;
    };
}

static inline CapsuleCollision::AABB AABBFromAABox(const G3D::AABox& box)
{
    CapsuleCollision::AABB r;
    G3D::Vector3 lo = box.low();
    G3D::Vector3 hi = box.high();
    r.min = { lo.x, lo.y, lo.z };
    r.max = { hi.x, hi.y, hi.z };
    return r;
}

// OverlapCapsule: test in model-local per instance
int SceneQuery::OverlapCapsule(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsule,
    std::vector<SceneHit>& outOverlaps,
    uint32_t includeMask,
    const QueryParams& params)
{
    outOverlaps.clear();

    G3D::Vector3 wP0(capsule.p0.x, capsule.p0.y, capsule.p0.z);
    G3D::Vector3 wP1(capsule.p1.x, capsule.p1.y, capsule.p1.z);

    // Broadphase internal AABB
    G3D::Vector3 iP0 = NavCoord::WorldToInternal(wP0);
    G3D::Vector3 iP1 = NavCoord::WorldToInternal(wP1);
    G3D::Vector3 iLo = iP0.min(iP1) - G3D::Vector3(capsule.r, capsule.r, capsule.r);
    G3D::Vector3 iHi = iP0.max(iP1) + G3D::Vector3(capsule.r, capsule.r, capsule.r);
    CapsuleCollision::AABB internalBox; internalBox.min = { iLo.x, iLo.y, iLo.z }; internalBox.max = { iHi.x, iHi.y, iHi.z }; CapsuleCollision::aabbInflate(internalBox, 0.005f);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    int indices[512]; int count = 0;
    view.queryInternal(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int idx = indices[i];
        const auto& Tlocal = view.tri(idx);
        const ModelInstance* mi = view.triInstance(idx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        // Use INTERNAL-space capsule endpoints for model-local transform
        G3D::Vector3 p0L = mi->iInvRot * ((iP0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iP1 - mi->iPos) * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsule.r * invScale;

        CapsuleCollision::Hit hLocal;
        if (CapsuleCollision::intersectCapsuleTriangle(CLocal, Tlocal, hLocal))
        {
            // Convert contact back to INTERNAL, then to WORLD
            G3D::Vector3 ptL(hLocal.point.x, hLocal.point.y, hLocal.point.z);
            G3D::Vector3 nL(hLocal.normal.x, hLocal.normal.y, hLocal.normal.z);
            // Apply rotation before translation; scale prior to rotation
            G3D::Vector3 iPoint = mi->iRot * (ptL * mi->iScale) + mi->iPos;
            G3D::Vector3 iNormal = mi->iRot * nL; iNormal = iNormal.directionOrZero();
            G3D::Vector3 wPoint = NavCoord::InternalToWorld(iPoint);
            G3D::Vector3 wNormal = NavCoord::InternalDirToWorld(iNormal).directionOrZero();

            // Log intersected triangle and contact info (capsule overlap)
            {
                std::ostringstream msg; msg << "[OverlapCapsule] tri=" << view.triLocalIndex(idx)
                    << " instId=" << mi->ID
                    << " pointW=(" << wPoint.x << "," << wPoint.y << "," << wPoint.z << ")"
                    << " normalW=(" << wNormal.x << "," << wNormal.y << "," << wNormal.z << ")"
                    << " depth=" << (hLocal.depth * mi->iScale);
                PHYS_INFO(PHYS_CYL, msg.str());
            }

            // DEBUG TRACE: verify closestPointOnTriangle correctness by barycentric reconstruction in WORLD space
            // 1) Build world triangle
            G3D::Vector3 iA = mi->iRot * (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iB = mi->iRot * (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iC = mi->iRot * (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 wA = NavCoord::InternalToWorld(iA);
            G3D::Vector3 wB = NavCoord::InternalToWorld(iB);
            G3D::Vector3 wC = NavCoord::InternalToWorld(iC);
            // 2) Compute barycentrics of world point W.r.t world triangle
            G3D::Vector3 bc = ComputeBarycentric(wPoint, wA, wB, wC);
            float wzRecon = bc.x * wA.z + bc.y * wB.z + bc.z * wC.z;
            G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
            float planeDist = (wPoint - wA).dot(wN);

            // 3) Recompute local closest points to cross-check onSeg/onTri agreement
            CapsuleCollision::Vec3 segOn, triOn;
            bool cpOk = CapsuleCollision::closestPoints_Segment_Triangle(CLocal.p0, CLocal.p1, Tlocal, segOn, triOn);
            G3D::Vector3 triOnI = mi->iRot * (G3D::Vector3(triOn.x, triOn.y, triOn.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 triOnW = NavCoord::InternalToWorld(triOnI);

            // Diagnostic: compute local triangle normal and transform without mirroring to world to compare
            G3D::Vector3 localN = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z))
                .cross(G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z)).directionOrZero();
            G3D::Vector3 internalN = mi->iRot * (localN * mi->iScale); // rotation only (scale uniform assumed)
            internalN = internalN.directionOrZero();
            G3D::Vector3 worldNFromLocal = NavCoord::InternalDirToWorld(internalN).directionOrZero();
            float nDot = wN.dot(worldNFromLocal);
            float zDiff = wN.z - worldNFromLocal.z;
            if (std::fabs(nDot) < 0.999f || std::fabs(zDiff) > 1e-4f) {
                std::ostringstream msg; msg << "[NormalDiagPen] tri=" << view.triLocalIndex(idx)
                    << " instId=" << mi->ID
                    << " wSurfN=(" << wN.x << "," << wN.y << "," << wN.z << ")"
                    << " worldNFromLocal=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ")"
                    << " dot=" << nDot << " zDiff=" << zDiff
                    << " localN=(" << localN.x << "," << localN.y << "," << localN.z << ")";
                // PHYS_INFO(PHYS_SURF, msg.str()); // commented out per request
            }

            // Prefer transformed local normal for stability; flip to ensure up-facing for ground tests
            bool flipped = false;
            G3D::Vector3 chosenN = worldNFromLocal;
            if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }
            if (flipped) {
                std::ostringstream msg; msg << "[NormalFixPen] tri=" << view.triLocalIndex(idx) << " instId=" << mi->ID
                    << " flippedUp=1 original=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ") -> ("
                    << chosenN.x << "," << chosenN.y << "," << chosenN.z << ")";
                PHYS_INFO(PHYS_SURF, msg.str());
            }

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = view.triLocalIndex(idx); h.instanceId = mi->ID; h.startPenetrating = true; h.penetrationDepth = hLocal.depth * mi->iScale; h.normalFlipped = flipped;
            outOverlaps.push_back(h);
        }
    }

    return (int)outOverlaps.size();
}

int SceneQuery::OverlapSphere(const StaticMapTree& map,
    const G3D::Vector3& center,
    float radius,
    std::vector<SceneHit>& outOverlaps,
    uint32_t includeMask,
    const QueryParams& params)
{
    outOverlaps.clear();

    G3D::Vector3 iCenter = NavCoord::WorldToInternal(center);
    G3D::Vector3 iLo = iCenter - G3D::Vector3(radius, radius, radius);
    G3D::Vector3 iHi = iCenter + G3D::Vector3(radius, radius, radius);
    CapsuleCollision::AABB internalBox; internalBox.min = { iLo.x, iLo.y, iLo.z }; internalBox.max = { iHi.x, iHi.y, iHi.z }; CapsuleCollision::aabbInflate(internalBox, 0.005f);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    int indices[512]; int count = 0;
    view.queryInternal(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int idx = indices[i];
        const auto& Tlocal = view.tri(idx);
        const ModelInstance* mi = view.triInstance(idx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        // Use INTERNAL-space center for model-local transform
        G3D::Vector3 cL = mi->iInvRot * ((iCenter - mi->iPos) * invScale);

        CapsuleCollision::Hit hLocal;
        if (CapsuleCollision::intersectSphereTriangle({ cL.x, cL.y, cL.z }, radius * invScale, Tlocal, hLocal))
        {
            G3D::Vector3 ptL(hLocal.point.x, hLocal.point.y, hLocal.point.z);
            G3D::Vector3 nL(hLocal.normal.x, hLocal.normal.y, hLocal.normal.z);
            G3D::Vector3 iPoint = mi->iRot * (ptL * mi->iScale) + mi->iPos;
            G3D::Vector3 iNormal = mi->iRot * nL; iNormal = iNormal.directionOrZero();
            G3D::Vector3 wPoint = NavCoord::InternalToWorld(iPoint);
            G3D::Vector3 wNormal = NavCoord::InternalDirToWorld(iNormal).directionOrZero();

            SceneHit h; h.hit = true; h.distance = hLocal.depth * mi->iScale; h.time = 0.0f; h.normal = wNormal; h.point = wPoint; h.triIndex = view.triLocalIndex(idx); h.instanceId = mi->ID; h.normalFlipped = false; outOverlaps.push_back(h);
        }
    }

    return (int)outOverlaps.size();
}

int SceneQuery::OverlapBox(const StaticMapTree& map,
    const G3D::AABox& box,
    std::vector<SceneHit>& outOverlaps,
    uint32_t includeMask,
    const QueryParams& params)
{
    outOverlaps.clear();

    // Approximate by testing sphere at center with radius = half-diagonal projected to XY (~broad check only)
    G3D::Vector3 lo = box.low(), hi = box.high();
    G3D::Vector3 c = (lo + hi) * 0.5f;
    G3D::Vector3 ext = (hi - lo) * 0.5f;
    float r = std::sqrt(ext.x * ext.x + ext.y * ext.y + ext.z * ext.z);
    return OverlapSphere(map, c, r, outOverlaps, includeMask, params);
}

int SceneQuery::SweepCapsule(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    std::vector<SceneHit>& outHits,
    uint32_t includeMask,
    const QueryParams& params)
{
    outHits.clear();
    if (distance <= 0.0f)
    {
        // Idle settle: single-pass overlap to avoid duplicate traversal/logs
        const float overlapInflation = 0.01f;
        CapsuleCollision::Capsule inflCaps = capsuleStart; inflCaps.r += overlapInflation;

        std::vector<SceneHit> overlaps;
        OverlapCapsule(map, inflCaps, overlaps, includeMask, params);

        // Optional: include terrain overlaps around capsule center
        std::vector<MapFormat::TerrainTriangle> terrainTris;
        if (PhysicsEngine::Instance())
        {
            if (MapLoader* loader = PhysicsEngine::Instance()->GetMapLoader())
            {
                G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
                G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);
                G3D::Vector3 center = (wP0 + wP1) * 0.5f;
                float r = inflCaps.r;
                loader->GetTerrainTriangles(map.GetMapId(), center.x - r, center.y - r, center.x + r, center.y + r, terrainTris);
                if (!terrainTris.empty())
                {
                    CapsuleCollision::Capsule Cw; Cw.p0 = { wP0.x, wP0.y, wP0.z }; Cw.p1 = { wP1.x, wP1.y, wP1.z }; Cw.r = inflCaps.r;
                    for (size_t tIdx = 0; tIdx < terrainTris.size(); ++tIdx)
                    {
                        const auto& tw = terrainTris[tIdx];
                        CapsuleCollision::Triangle Tterrain; Tterrain.a = { tw.ax, tw.ay, tw.az }; Tterrain.b = { tw.bx, tw.by, tw.bz }; Tterrain.c = { tw.cx, tw.cy, tw.cz }; Tterrain.doubleSided = false; Tterrain.collisionMask = 0xFFFFFFFFu;
                        CapsuleCollision::Hit chW;
                        if (CapsuleCollision::intersectCapsuleTriangle(Cw, Tterrain, chW))
                        {
                            G3D::Vector3 wPoint(chW.point.x, chW.point.y, chW.point.z);
                            G3D::Vector3 wA(tw.ax, tw.ay, tw.az), wB(tw.bx, tw.by, tw.bz), wC(tw.cx, tw.cy, tw.cz);
                            G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
                            bool flipped = false; G3D::Vector3 chosenN = wN; if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }
                            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = (int)tIdx; h.instanceId = 0; h.startPenetrating = true; h.penetrationDepth = chW.depth; h.normalFlipped = flipped;
                            overlaps.push_back(h);
                        }
                    }
                }
            }
        }

        if (!overlaps.empty())
        {
            std::sort(overlaps.begin(), overlaps.end(), [](const SceneHit& a, const SceneHit& b) {
                if (std::fabs(a.point.z - b.point.z) > 1e-4f) return a.point.z > b.point.z;
                if (std::fabs(a.penetrationDepth - b.penetrationDepth) > 1e-5f) return a.penetrationDepth > b.penetrationDepth;
                return a.triIndex < b.triIndex;
            });
            outHits = overlaps;
            return (int)outHits.size();
        }
        return 0;
    }

    G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
    G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);
    if (params.inflation != 0.0f)
    {
        float dirLen = dir.magnitude();
        if (dirLen > 1e-6f)
        {
            G3D::Vector3 dirN = dir * (1.0f / dirLen);
            G3D::Vector3 adjust = dirN * params.inflation;
            wP0 = wP0 + adjust;
            wP1 = wP1 + adjust;
        }
    }
    // Convert adjusted world capsule endpoints to INTERNAL space once (mirrors raycast chain)
    G3D::Vector3 iW0 = NavCoord::WorldToInternal(wP0);
    G3D::Vector3 iW1 = NavCoord::WorldToInternal(wP1);
    // Broadphase internal AABB (use original radius)
    G3D::Vector3 iP0 = iW0;
    G3D::Vector3 iP1 = iW1;
    G3D::Vector3 iP0End = iP0 + NavCoord::WorldDirToInternal(dir) * distance;
    G3D::Vector3 iP1End = iP1 + NavCoord::WorldDirToInternal(dir) * distance;
    G3D::Vector3 iMin = iP0.min(iP1).min(iP0End.min(iP1End)) - G3D::Vector3(capsuleStart.r, capsuleStart.r, capsuleStart.r);
    G3D::Vector3 iMax = iP0.max(iP1).max(iP0End.max(iP1End)) + G3D::Vector3(capsuleStart.r, capsuleStart.r, capsuleStart.r);
    CapsuleCollision::AABB sweepBoxI; sweepBoxI.min = { iMin.x, iMin.y, iMin.z }; sweepBoxI.max = { iMax.x, iMax.y, iMax.z }; CapsuleCollision::aabbInflate(sweepBoxI, 0.005f);
    // Reduce vertical dip: use small epsilon instead of radius-based lowering to avoid pulling far-below triangles
    sweepBoxI.min.z -= 0.05f;

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.queryInternal(sweepBoxI, triIdxs, triCount, kCap);
    if (triCount == kCap) {
        // PHYS_INFO(PHYS_CYL, "[SweepCapsuleBroadphase] triCountReachedCap cap=" << kCap);
    }
    // PHYS_INFO(PHYS_CYL, "[SweepCapsuleBroadphase] triCount=" << triCount);
    // Build per-instance distribution summary
    if (triCount > 0) {
        std::unordered_map<uint32_t, int> instTriCounts;
        for (int i = 0; i < triCount; ++i) {
            const ModelInstance* miDist = view.triInstance(triIdxs[i]);
            if (!miDist) continue;
            instTriCounts[miDist->ID]++;
        }
        for (auto& kv : instTriCounts) {
            // PHYS_TRACE(PHYS_CYL, "[SweepCapsuleBroadphaseDist] instID=" << kv.first << " tris=" << kv.second);
        }
    }

    // Prepare terrain triangle query from MapLoader in world-space AABB of sweep (XY only)
    std::vector<MapFormat::TerrainTriangle> terrainTris;
    {
        G3D::Vector3 wP0End = wP0 + dir * distance;
        G3D::Vector3 wP1End = wP1 + dir * distance;
        float minX = std::min(std::min(wP0.x, wP1.x), std::min(wP0End.x, wP1End.x)) - capsuleStart.r;
        float maxX = std::max(std::max(wP0.x, wP1.x), std::max(wP0End.x, wP1End.x)) + capsuleStart.r;
        float minY = std::min(std::min(wP0.y, wP1.y), std::min(wP0End.y, wP1End.y)) - capsuleStart.r;
        float maxY = std::max(std::max(wP0.y, wP1.y), std::max(wP0End.y, wP1End.y)) + capsuleStart.r;
        //PHYS_INFO(PHYS_CYL, "[TerrainQuery] map=" << map.GetMapId() << " sweepDist=" << distance
        //    << " minX=" << minX << " maxX=" << maxX << " minY=" << minY << " maxY=" << maxY
        //    << " capR=" << capsuleStart.r); // commented out per request
        if (PhysicsEngine::Instance())
        {
            MapLoader* loader = PhysicsEngine::Instance()->GetMapLoader();
            if (loader)
            {
                loader->GetTerrainTriangles(map.GetMapId(), minX, minY, maxX, maxY, terrainTris);
                //PHYS_INFO(PHYS_CYL, "[TerrainQuery] result count=" << terrainTris.size()); // commented out per request
                size_t sample = std::min<size_t>(terrainTris.size(), 16);
                for (size_t i = 0; i < sample; ++i)
                {
                    const auto& t = terrainTris[i];
                    //PHYS_TRACE(PHYS_CYL, "[TerrainTriSample] idx=" << i
                    //    << " a=(" << t.ax << "," << t.ay << "," << t.az << ")"
                    //    << " b=(" << t.bx << "," << t.by << "," << t.bz << ")"
                    //    << " c=(" << t.cx << "," << t.cy << "," << t.cz << ")"); // commented out per request
                }
                //if (terrainTris.empty()) {
                //    PHYS_TRACE(PHYS_CYL, "[TerrainQuery] No triangles returned in region");
                //} // commented out per request
            }
            else {
                PHYS_INFO(PHYS_CYL, "[TerrainQuery] MapLoader nullptr (not initialized) map=" << map.GetMapId());
            }
        }
        else {
            PHYS_INFO(PHYS_CYL, "[TerrainQuery] PhysicsEngine instance missing");
        }
    }

    // Start penetration check per triangle in model-local space
    float capMinZWorld = std::min(wP0.z, wP1.z) - capsuleStart.r - 0.05f; // small epsilon
    float capMaxZWorld = std::max(wP0.z, wP1.z) + capsuleStart.r + 0.05f;

    // 1) VMAP model triangles (existing path)
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const ModelInstance* mi = view.triInstance(cacheIdx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        G3D::Vector3 p0L = mi->iInvRot * ((iW0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iW1 - mi->iPos) * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsuleStart.r * invScale;

        CapsuleCollision::Hit chL;
        if (CapsuleCollision::intersectCapsuleTriangle(CLocal, Tlocal, chL))
        {
            // Transform local contact point directly to internal then world; do not recompute via closestPoints.
            G3D::Vector3 ptL(chL.point.x, chL.point.y, chL.point.z);
            G3D::Vector3 iPoint = mi->iRot * (ptL * mi->iScale) + mi->iPos; // model-local -> internal space
            G3D::Vector3 wPoint = NavCoord::InternalToWorld(iPoint);

            // Vertical validation using true contact point
            if (wPoint.z < capMinZWorld || wPoint.z > capMaxZWorld)
            {
                continue;
            }

            // Build world triangle for normal/barycentric (vertices already needed)
            G3D::Vector3 iA = mi->iRot * (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iB = mi->iRot * (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iC = mi->iRot * (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 wA = NavCoord::InternalToWorld(iA);
            G3D::Vector3 wB = NavCoord::InternalToWorld(iB);
            G3D::Vector3 wC = NavCoord::InternalToWorld(iC);

            // Vertical validation using true contact point
            if (wPoint.z < capMinZWorld || wPoint.z > capMaxZWorld)
            {
                continue;
            }

            // Diagnostic: compute local triangle normal and transform without mirroring to world to compare
            G3D::Vector3 localN = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z))
                .cross(G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z)).directionOrZero();
            G3D::Vector3 internalN = mi->iRot * (localN * mi->iScale);
            internalN = internalN.directionOrZero();
            G3D::Vector3 worldNFromLocal = NavCoord::InternalDirToWorld(internalN).directionOrZero();
            float nDot = wPoint.dot(worldNFromLocal);
            float zDiff = wPoint.z - worldNFromLocal.z;
            if (std::fabs(nDot) < 0.999f || std::fabs(zDiff) > 1e-4f) {
                std::ostringstream msg; msg << "[NormalDiagPen] tri=" << view.triLocalIndex(cacheIdx)
                    << " instId=" << mi->ID
                    << " wSurfN=(" << wPoint.x << "," << wPoint.y << "," << wPoint.z << ")"
                    << " worldNFromLocal=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ")"
                    << " dot=" << nDot << " zDiff=" << zDiff
                    << " localN=(" << localN.x << "," << localN.y << "," << localN.z << ")";
                // PHYS_INFO(PHYS_SURF, msg.str()); // commented out per request
            }

            // Prefer transformed local normal for stability; flip to ensure up-facing for ground tests
            bool flipped = false;
            G3D::Vector3 chosenN = worldNFromLocal;
            if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }
            if (flipped) { std::ostringstream msg; msg << "[NormalFixPen] tri=" << view.triLocalIndex(cacheIdx) << " instId=" << mi->ID
                << " flippedUp=1 original=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ") -> ("
                << chosenN.x << "," << chosenN.y << "," << chosenN.z << ")"; PHYS_INFO(PHYS_SURF, msg.str()); }

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi->ID; h.startPenetrating = true; h.penetrationDepth = chL.depth * mi->iScale; h.normalFlipped = flipped;
            outHits.push_back(h);
        }
    }

    // 2) Terrain triangles (world space path)
    if (!terrainTris.empty())
    {
        CapsuleCollision::Capsule Cw; Cw.p0 = { wP0.x, wP0.y, wP0.z }; Cw.p1 = { wP1.x, wP1.y, wP1.z }; Cw.r = capsuleStart.r;
        for (size_t tIdx = 0; tIdx < terrainTris.size(); ++tIdx)
        {
            const auto& tw = terrainTris[tIdx];
            CapsuleCollision::Triangle Tterrain; Tterrain.a = { tw.ax, tw.ay, tw.az }; Tterrain.b = { tw.bx, tw.by, tw.bz }; Tterrain.c = { tw.cx, tw.cy, tw.cz }; Tterrain.doubleSided = false; Tterrain.collisionMask = 0xFFFFFFFFu;
            CapsuleCollision::Hit chW;
            if (CapsuleCollision::intersectCapsuleTriangle(Cw, Tterrain, chW))
            {
                G3D::Vector3 wPoint(chW.point.x, chW.point.y, chW.point.z);
                if (wPoint.z < capMinZWorld || wPoint.z > capMaxZWorld)
                    continue;

                // Compute world normal from triangle cross
                G3D::Vector3 wA(tw.ax, tw.ay, tw.az), wB(tw.bx, tw.by, tw.bz), wC(tw.cx, tw.cy, tw.cz);
                G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
                bool flipped = false; G3D::Vector3 chosenN = wN; if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }

                SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = (int)tIdx; h.instanceId = 0; h.startPenetrating = true; h.penetrationDepth = chW.depth; h.normalFlipped = flipped;
                outHits.push_back(h);
            }
        }
    }

    if (!outHits.empty())
    {
        // Sort start penetration hits so the highest surface (largest point.z) comes first; tie-break by penetration depth
        std::sort(outHits.begin(), outHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (std::fabs(a.point.z - b.point.z) > 1e-4f) return a.point.z > b.point.z; // higher first
            if (std::fabs(a.penetrationDepth - b.penetrationDepth) > 1e-5f) return a.penetrationDepth > b.penetrationDepth; // deeper first
            return a.triIndex < b.triIndex; // stable fallback
        });
        return (int)outHits.size();
    }

    // Analytic sweep in model-local space
    struct HitTmp { float t; int triCacheIdx; int triLocalIdx; uint32_t instId; G3D::Vector3 nWorld; G3D::Vector3 pWorld; float penetrationDepth; G3D::Vector3 centerAtHit; };
    std::vector<HitTmp> candidates; candidates.reserve(triCount + (int)terrainTris.size());
    G3D::Vector3 worldVel = dir * distance;

    // 1) VMAP model triangles
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const ModelInstance* mi = view.triInstance(cacheIdx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        G3D::Vector3 p0L = mi->iInvRot * ((iW0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iW1 - mi->iPos) * invScale);
        G3D::Vector3 iVel = NavCoord::WorldDirToInternal(dir) * distance;
        G3D::Vector3 vL = mi->iInvRot * (iVel * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsuleStart.r * invScale;
        CapsuleCollision::Vec3 velLocal(vL.x, vL.y, vL.z);

        float toi; CapsuleCollision::Vec3 nL, pL;
        if (CapsuleCollision::capsuleTriangleSweep(CLocal, velLocal, Tlocal, toi, nL, pL) && toi >= 0.0f && toi <= 1.0f)
        {
            // Capture model instance transform diagnostics
            PHYS_TRACE(PHYS_CYL, std::string("[SweepDbgXform] instId=") << mi->ID
                << " iPos=(" << mi->iPos.x << "," << mi->iPos.y << "," << mi->iPos.z << ")"
                << " iScale=" << mi->iScale);
            // Log rotation matrix elements (internal-space rotation)
            G3D::Matrix3 R = mi->iRot;
            PHYS_TRACE(PHYS_CYL, std::string("[SweepDbgRot] iRot=")
                << " [" << R.get(0,0) << "," << R.get(0,1) << "," << R.get(0,2) << "]"
                << " [" << R.get(1,0) << "," << R.get(1,1) << "," << R.get(1,2) << "]"
                << " [" << R.get(2,0) << "," << R.get(2,1) << "," << R.get(2,2) << "]");
            G3D::Matrix3 Rinv = mi->iInvRot;
            PHYS_TRACE(PHYS_CYL, std::string("[SweepDbgRotInv] iInvRot=")
                << " [" << Rinv.get(0,0) << "," << Rinv.get(0,1) << "," << Rinv.get(0,2) << "]"
                << " [" << Rinv.get(1,0) << "," << Rinv.get(1,1) << "," << Rinv.get(1,2) << "]"
                << " [" << Rinv.get(2,0) << "," << Rinv.get(2,1) << "," << Rinv.get(2,2) << "]");

            // Local-space triangle vertices
            G3D::Vector3 LA(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z);
            G3D::Vector3 LB(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z);
            G3D::Vector3 LC(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z);
            PHYS_TRACE(PHYS_CYL, std::string("[SweepDbgLocalTri] tri=") << view.triLocalIndex(cacheIdx)
                << " A=(" << LA.x << "," << LA.y << "," << LA.z << ")"
                << " B=(" << LB.x << "," << LB.y << "," << LB.z << ")"
                << " C=(" << LC.x << "," << LC.y << "," << LC.z << ")");

            // Transform triangle to world for normal/barycentric
            G3D::Vector3 iA = mi->iRot * (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iB = mi->iRot * (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 iC = mi->iRot * (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 wA = NavCoord::InternalToWorld(iA);
            G3D::Vector3 wB = NavCoord::InternalToWorld(iB);
            G3D::Vector3 wC = NavCoord::InternalToWorld(iC);
            CapsuleCollision::Triangle Tw; Tw.a = { wA.x, wA.y, wA.z }; Tw.b = { wB.x, wB.y, wB.z }; Tw.c = { wC.x, wC.y, wC.z };

            // True world impact point from local sweep result pL
            G3D::Vector3 ptL(pL.x, pL.y, pL.z);
            G3D::Vector3 iImpact = mi->iRot * (ptL * mi->iScale) + mi->iPos;
            G3D::Vector3 wImpact = NavCoord::InternalToWorld(iImpact);

            float tClamped = CapsuleCollision::cc_clamp(toi, 0.0f, 1.0f);

            // Removed strict vertical gating; rely on correct impact computation and downstream checks

            // Debug: validate closestPointOnTriangle and plane alignment for sweep
            CapsuleCollision::Vec3 segOn, triOn;
            CapsuleCollision::Capsule impact = CLocal; impact.p0 = impact.p0 + velLocal * toi; impact.p1 = impact.p1 + velLocal * toi;
            bool cpOk = CapsuleCollision::closestPoints_Segment_Triangle(impact.p0, impact.p1, Tlocal, segOn, triOn);
            G3D::Vector3 triOnI = mi->iRot * (G3D::Vector3(triOn.x, triOn.y, triOn.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 triOnW = NavCoord::InternalToWorld(triOnI);
            G3D::Vector3 bc = ComputeBarycentric(wImpact, wA, wB, wC);
            float wzRecon = bc.x * wA.z + bc.y * wB.z + bc.z * wC.z;
            G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
            float planeDist = (wImpact - wA).dot(wN);
            PHYS_INFO(PHYS_CYL, std::string("[SweepDbg] tri=") << view.triLocalIndex(cacheIdx) << " instId=" << mi->ID
                << " t=" << tClamped
                << " wImpact=(" << wImpact.x << "," << wImpact.y << "," << wImpact.z << ")"
                << " bc=(" << bc.x << "," << bc.y << "," << bc.z << ")"
                << " wzRecon=" << wzRecon
                << " zDelta=" << (wImpact.z - wzRecon)
                << " planeDist=" << planeDist
                << " triWz=(" << wA.z << "," << wB.z << "," << wC.z << ")"
                << " cpOk=" << (cpOk ? 1 : 0)
                << " triOnW=(" << triOnW.x << "," << triOnW.y << "," << triOnW.z << ")");

            // Diagnostic: compare world cross normal vs transformed local normal
            G3D::Vector3 localN = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z))
                .cross(G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z)).directionOrZero();
            G3D::Vector3 internalN = mi->iRot * (localN * mi->iScale);
            internalN = internalN.directionOrZero();
            G3D::Vector3 worldNFromLocal = NavCoord::InternalDirToWorld(internalN).directionOrZero();
            float nDot = wN.dot(worldNFromLocal);
            float zDiff = wN.z - worldNFromLocal.z;
            if (std::fabs(nDot) < 0.999f || std::fabs(zDiff) > 1e-4f) {
                std::ostringstream msg; msg << "[NormalDiagSweep] tri=" << view.triLocalIndex(cacheIdx)
                    << " instId=" << mi->ID << " t=" << tClamped
                    << " wSurfN=(" << wN.x << "," << wN.y << "," << wN.z << ")"
                    << " worldNFromLocal=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ")"
                    << " dot=" << nDot << " zDiff=" << zDiff
                    << " localN=(" << localN.x << "," << localN.y << "," << localN.z << ")"; PHYS_INFO(PHYS_SURF, msg.str()); }

            // Prefer transformed local normal and force upward-facing for ground checks
            bool flipped = false;
            G3D::Vector3 chosenN = worldNFromLocal;
            if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }
            if (flipped) { std::ostringstream msg; msg << "[NormalFixSweep] tri=" << view.triLocalIndex(cacheIdx) << " instId=" << mi->ID
                << " flippedUp=1 original=(" << worldNFromLocal.x << "," << worldNFromLocal.y << "," << worldNFromLocal.z << ") -> ("
                << chosenN.x << "," << chosenN.y << "," << chosenN.z << ") t=" << tClamped; PHYS_INFO(PHYS_SURF, msg.str()); }

            CapsuleCollision::Hit chL; CapsuleCollision::intersectCapsuleTriangle(impact, Tlocal, chL);
            G3D::Vector3 wCenter0 = (wP0 + wP1) * 0.5f; G3D::Vector3 centerAtHit = wCenter0 + dir * (tClamped * distance);
            candidates.push_back(HitTmp{ toi, cacheIdx, view.triLocalIndex(cacheIdx), mi->ID, chosenN, wImpact, chL.depth * mi->iScale, centerAtHit });
        }
    }

    // 2) Terrain triangles in world space
    if (!terrainTris.empty())
    {
        CapsuleCollision::Capsule Cw; Cw.p0 = { wP0.x, wP0.y, wP0.z }; Cw.p1 = { wP1.x, wP1.y, wP1.z }; Cw.r = capsuleStart.r;
        CapsuleCollision::Vec3 velW(worldVel.x, worldVel.y, worldVel.z);
        for (size_t tIdx = 0; tIdx < terrainTris.size(); ++tIdx)
        {
            const auto& tw = terrainTris[tIdx];
            CapsuleCollision::Triangle Tterrain; Tterrain.a = { tw.ax, tw.ay, tw.az }; Tterrain.b = { tw.bx, tw.by, tw.bz }; Tterrain.c = { tw.cx, tw.cy, tw.cz }; Tterrain.doubleSided = false; Tterrain.collisionMask = 0xFFFFFFFFu;
            float toi; CapsuleCollision::Vec3 nW, pW;
            if (CapsuleCollision::capsuleTriangleSweep(Cw, velW, Tterrain, toi, nW, pW) && toi >= 0.0f && toi <= 1.0f)
            {
                float tClamped = CapsuleCollision::cc_clamp(toi, 0.0f, 1.0f);
                G3D::Vector3 wImpact(pW.x, pW.y, pW.z);
                // Compute world-space normal from triangle (more stable)
                G3D::Vector3 wA(tw.ax, tw.ay, tw.az), wB(tw.bx, tw.by, tw.bz), wC(tw.cx, tw.cy, tw.cz);
                G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
                bool flipped = false; G3D::Vector3 chosenN = wN; if (chosenN.z < 0.0f) { chosenN = -chosenN; flipped = true; }
                G3D::Vector3 wCenter0 = (wP0 + wP1) * 0.5f;
                G3D::Vector3 centerAtHit = wCenter0 + dir * (tClamped * distance);
                // Use depth from discrete impact approximation (optional)
                CapsuleCollision::Hit chW; CapsuleCollision::intersectCapsuleTriangle({ Cw.p0 + velW * toi, Cw.p1 + velW * toi, Cw.r }, Tterrain, chW);
                candidates.push_back(HitTmp{ toi, (int)tIdx, (int)tIdx, 0u, chosenN, wImpact, chW.depth, centerAtHit });
            }
        }
    }

    if (!candidates.empty())
    {
        std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b) { if (a.t == b.t) return a.triLocalIdx < b.triLocalIdx; return a.t < b.t; });
        for (auto& c : candidates)
        {
            SceneHit h; 
            h.hit = true; 
            h.time = CapsuleCollision::cc_clamp(c.t, 0.0f, 1.0f);
            h.distance = h.time * distance; 
            h.penetrationDepth = c.penetrationDepth;
            G3D::Vector3 wSurfN = c.nWorld.directionOrZero();
            h.normal = wSurfN; 
            h.point = c.pWorld; 
            h.triIndex = c.triLocalIdx; 
            h.instanceId = c.instId; 
            h.startPenetrating = false; 
            h.normalFlipped = false;
            outHits.push_back(h);
        }
        return (int)outHits.size();
    }

    // Fallback overlap
    const float overlapInflation = 0.01f;
    CapsuleCollision::Capsule inflCaps = capsuleStart; inflCaps.r += overlapInflation;
    std::vector<SceneHit> overlapHits;
    int nOverlap = OverlapCapsule(map, inflCaps, overlapHits, includeMask, params);
    if (nOverlap > 0)
    {
        outHits = overlapHits;
        return nOverlap;
    }
    return 0;
}