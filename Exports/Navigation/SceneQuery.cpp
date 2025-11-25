#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "PhysicsEngine.h" // added for walkable slope threshold access
#include <algorithm>
#include <cmath>
#include <vector>
#include <cstdlib>
#include <mutex>
#include <iostream>

using namespace VMAP;

namespace
{
    // Helper to ensure normals face the Z-up hemisphere for walkability tests.
    static inline void EnsureUpwardNormal(G3D::Vector3& n, SceneHit& h)
    {
        // Use world Z-up. If normal points downward, flip and mark flipped.
        if (n.z < 0.0f)
        {
            n = -n;
            h.normalFlipped = true;
        }
    }

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
        }

        // Modified: input AABB is already internal-space. Removed world corner transforms and large Y inflate.
        void query(const CapsuleCollision::AABB& internalBox, int* outIndices, int& count, int maxCount) const override
        {
            count = 0;
            m_cache.clear();
            m_triToInstance.clear();
            m_triToLocalTri.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
                return;

            // Use internal min/max directly and apply small symmetric inflation (match ray epsilon intent).
            G3D::Vector3 qLo(internalBox.min.x, internalBox.min.y, internalBox.min.z);
            G3D::Vector3 qHi(internalBox.max.x, internalBox.max.y, internalBox.max.z);
            const float eps = 0.05f; // symmetric small epsilon
            G3D::AABox queryBox(qLo - G3D::Vector3(eps, eps, eps), qHi + G3D::Vector3(eps, eps, eps));

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            bool bihOk = m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap);
            if (!bihOk)
            {
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    const ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if ((inst.GetCollisionMask() & m_includeMask) == 0) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i;
                }
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

                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData)
                {
                    if (!inst.iModel->GetAllMeshData(vertices, indices)) continue;
                }

                size_t triCount = indices.size() / 3;
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
                }
                if (count >= maxCount) break;
            }
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
        G3D::Vector3 p0L = mi->iInvRot * ((wP0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((wP1 - mi->iPos) * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsule.r * invScale;

        CapsuleCollision::Hit hLocal;
        if (CapsuleCollision::intersectCapsuleTriangle(CLocal, Tlocal, hLocal))
        {
            G3D::Vector3 ptL(hLocal.point.x, hLocal.point.y, hLocal.point.z);
            G3D::Vector3 nL(hLocal.normal.x, hLocal.normal.y, hLocal.normal.z);
            G3D::Vector3 wPoint = (ptL * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wNormal = (nL * mi->iRot).directionOrZero();

            SceneHit h; h.hit = true; h.distance = hLocal.depth * mi->iScale; h.time = 0.0f; h.normal = wNormal; h.point = wPoint; h.triIndex = view.triLocalIndex(idx); h.instanceId = mi->ID; EnsureUpwardNormal(h.normal, h); outOverlaps.push_back(h);
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
        G3D::Vector3 cL = mi->iInvRot * ((center - mi->iPos) * invScale);

        CapsuleCollision::Hit hLocal;
        if (CapsuleCollision::intersectSphereTriangle({ cL.x, cL.y, cL.z }, radius * invScale, Tlocal, hLocal))
        {
            G3D::Vector3 ptL(hLocal.point.x, hLocal.point.y, hLocal.point.z);
            G3D::Vector3 nL(hLocal.normal.x, hLocal.normal.y, hLocal.normal.z);
            G3D::Vector3 wPoint = (ptL * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wNormal = (nL * mi->iRot).directionOrZero();

            SceneHit h; h.hit = true; h.distance = hLocal.depth * mi->iScale; h.time = 0.0f; h.normal = wNormal; h.point = wPoint; h.triIndex = view.triLocalIndex(idx); h.instanceId = mi->ID; EnsureUpwardNormal(h.normal, h); outOverlaps.push_back(h);
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
    PHYS_TRACE(PHYS_CYL, "SweepCapsule ENTER includeMask=0x" << std::hex << includeMask << std::dec
        << " distance=" << distance
        << " dirW=(" << dir.x << "," << dir.y << "," << dir.z << ")"
        << " capStart.p0W=(" << capsuleStart.p0.x << "," << capsuleStart.p0.y << "," << capsuleStart.p0.z << ")"
        << " capStart.p1W=(" << capsuleStart.p1.x << "," << capsuleStart.p1.y << "," << capsuleStart.p1.z << ")"
        << " r=" << capsuleStart.r
        << " params.inflation=" << params.inflation);
    outHits.clear();
    if (distance <= 0.0f)
    {
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EARLY EXIT distance<=0");
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
    sweepBoxI.min.z -= (capsuleStart.r * 0.5f);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.queryInternal(sweepBoxI, triIdxs, triCount, kCap);
    PHYS_TRACE(PHYS_CYL, "view.queryInternal returned triCount=" << triCount << " (cap=" << kCap << ")");

    // Walkable slope cosine threshold (runtime if available, else default)
    float walkableCos = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    if (PhysicsEngine::Instance())
        walkableCos = PhysicsEngine::Instance()->GetWalkableCosMin();

    // Detailed triangle comparison logs
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const ModelInstance* mi = view.triInstance(cacheIdx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        // Use internal-space endpoints for model-local transform (fix space mismatch)
        G3D::Vector3 p0L = mi->iInvRot * ((iW0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iW1 - mi->iPos) * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsuleStart.r * invScale;
        CapsuleCollision::Vec3 sSegL, sTriL;
        CapsuleCollision::closestPoints_Segment_Triangle(CLocal.p0, CLocal.p1, Tlocal, sSegL, sTriL);
        float distL = (sSegL - sTriL).length();
        float rawDepthL = CLocal.r - distL;
        float segLen2L = (CLocal.p1 - CLocal.p0).length2();
        float segTL = 0.0f;
        if (segLen2L > CapsuleCollision::EPSILON * CapsuleCollision::EPSILON)
            segTL = CapsuleCollision::Vec3::dot(sSegL - CLocal.p0, (CLocal.p1 - CLocal.p0)) / segLen2L;
        const char* regionL = ClassifyCapsuleRegion(segTL);

        // world triangle
        G3D::Vector3 wA = (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) * mi->iRot + mi->iPos;
        G3D::Vector3 wB = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) * mi->iRot + mi->iPos;
        G3D::Vector3 wC = (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) * mi->iRot + mi->iPos;
        CapsuleCollision::Triangle Tw; Tw.a = { wA.x, wA.y, wA.z }; Tw.b = { wB.x, wB.y, wB.z }; Tw.c = { wC.x, wC.y, wC.z };
        CapsuleCollision::Vec3 sSegW, sTriW;
        CapsuleCollision::closestPoints_Segment_Triangle({ wP0.x, wP0.y, wP0.z }, { wP1.x, wP1.y, wP1.z }, Tw, sSegW, sTriW);
        float distW = (sSegW - sTriW).length();
        float rawDepthW = capsuleStart.r - distW;
        float segLen2W = (Tw.c - Tw.a).length2();

        // bary/world normal
        G3D::Vector3 triN = (wB - wA).cross(wC - wA).directionOrZero();
        SceneHit flipDummy; EnsureUpwardNormal(triN, flipDummy);
        G3D::Vector3 wPtTri(sTriW.x, sTriW.y, sTriW.z);
        G3D::Vector3 bary = ComputeBarycentric(wPtTri, wA, wB, wC);
        int smallCount = (bary.x < 0.05f) + (bary.y < 0.05f) + (bary.z < 0.05f);
        const char* triRegion = (smallCount >= 2 ? "VERTEX" : (smallCount == 1 ? "EDGE" : "FACE"));
        PHYS_TRACE(PHYS_CYL, "tComp triCacheIdx=" << cacheIdx << " modelTri=" << view.triLocalIndex(cacheIdx) << " instId=" << mi->ID
            << " p0W=(" << wP0.x << "," << wP0.y << "," << wP0.z << ") p1W=(" << wP1.x << "," << wP1.y << "," << wP1.z << ")"
            << " p0L=(" << p0L.x << "," << p0L.y << "," << p0L.z << ") p1L=(" << p1L.x << "," << p1L.y << "," << p1L.z << ")"
            << " rW=" << capsuleStart.r << " rL=" << CLocal.r
            << " rawDepthL=" << rawDepthL << " distL=" << distL << " segTL=" << segTL << " regionL=" << regionL
            << " rawDepthW=" << rawDepthW << " distW=" << distW
            << " triN=(" << triN.x << "," << triN.y << "," << triN.z << ") bary=(" << bary.x << "," << bary.y << "," << bary.z << ") triRegion=" << triRegion);

        // Additional diagnostic: improved required sweep distance along dir to reach contact.
        // Previous quadratic approach assumed both closest points remain fixed features; produced unstable huge values.
        // New approach: approximate that the capsule closest point translates by dirN * t. Separation decreases linearly by proj = dot(deltaNorm, dirN).
        // needSweepDist = (gapDist - radius) / proj if proj > 0, else unreachable (-1).
        G3D::Vector3 deltaVec(sSegW.x - sTriW.x, sSegW.y - sTriW.y, sSegW.z - sTriW.z);
        float gapDist = distW; // current separation
        G3D::Vector3 dirN = dir.directionOrZero();
        float rad = capsuleStart.r;
        float deltaLen = deltaVec.magnitude();
        float proj = 0.0f; if (deltaLen > 1e-6f) proj = deltaVec.dot(dirN) / deltaLen; // cosine of angle between separation and motion
        float needSweepDist = -1.0f; // -1 means unreachable along dir
        const char* reachReason = "OK";
        if (gapDist <= rad)
        {
            needSweepDist = 0.0f; reachReason = "already overlapping";
        }
        else if (proj > 1e-4f)
        {
            needSweepDist = (gapDist - rad) / proj; reachReason = "projected";
        }
        else
        {
            reachReason = "proj<=0 (moving away or orthogonal)";
        }
        PHYS_TRACE(PHYS_CYL, "tCompExtra tri=" << view.triLocalIndex(cacheIdx)
            << " gapDist=" << gapDist
            << " radius=" << rad
            << " deltaLen=" << deltaLen
            << " deltaNormDotDir=" << proj
            << " needSweepDist=" << needSweepDist
            << " providedSweepDist=" << distance
            << " reason=" << reachReason);
    }

    // Start penetration check per triangle in model-local space
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
            // Build world hit
            G3D::Vector3 ptL(chL.point.x, chL.point.y, chL.point.z);
            G3D::Vector3 wPoint = (ptL * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wA = (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wB = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wC = (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wSurfN = (wB - wA).cross(wC - wA).directionOrZero();
            SceneHit dummy; EnsureUpwardNormal(wSurfN, dummy);

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wSurfN; h.point = wPoint; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi->ID; h.startPenetrating = true; h.penetrationDepth = chL.depth * mi->iScale; h.normalFlipped = dummy.normalFlipped;
            outHits.push_back(h);

            // Extra: per-hit log with barycentric to correlate with raycast
            G3D::Vector3 barySP = ComputeBarycentric(h.point, wA, wB, wC);
            PHYS_TRACE(PHYS_CYL, "[Hit] startPen tri=" << h.triIndex << " instId=" << h.instanceId
                << " depth=" << h.penetrationDepth
                << " pointW=(" << h.point.x << "," << h.point.y << "," << h.point.z << ")"
                << " normalW=(" << h.normal.x << "," << h.normal.y << "," << h.normal.z << ")"
                << " bary=(" << barySP.x << "," << barySP.y << "," << barySP.z << ")");
        }
    }
    if (!outHits.empty())
    {
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT with startPenetrating hits count=" << outHits.size());
        std::sort(outHits.begin(), outHits.end(), [](const SceneHit& a, const SceneHit& b) { return a.triIndex < b.triIndex; });
        return (int)outHits.size();
    }

    // Analytic sweep in model-local space
    struct HitTmp { float t; int triCacheIdx; int triLocalIdx; uint32_t instId; G3D::Vector3 nWorld; G3D::Vector3 pWorld; float penetrationDepth; };
    std::vector<HitTmp> candidates; candidates.reserve(triCount);
    G3D::Vector3 worldVel = dir * distance;
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const ModelInstance* mi = view.triInstance(cacheIdx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        G3D::Vector3 p0L = mi->iInvRot * ((iW0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iW1 - mi->iPos) * invScale);
        G3D::Vector3 vL = mi->iInvRot * (worldVel * invScale); // worldVel is still world->internal mirrored only through rotation; acceptable approximation
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsuleStart.r * invScale;
        CapsuleCollision::Vec3 velLocal(vL.x, vL.y, vL.z);

        float toi; CapsuleCollision::Vec3 nL, pL;
        if (CapsuleCollision::capsuleTriangleSweep(CLocal, velLocal, Tlocal, toi, nL, pL) && toi >= 0.0f && toi <= 1.0f)
        {
            CapsuleCollision::Capsule impact = CLocal;
            impact.p0 = impact.p0 + velLocal * toi;
            impact.p1 = impact.p1 + velLocal * toi;
            CapsuleCollision::Hit chL; CapsuleCollision::intersectCapsuleTriangle(impact, Tlocal, chL);

            G3D::Vector3 pLocal(pL.x, pL.y, pL.z);
            G3D::Vector3 nLocal(nL.x, nL.y, nL.z);
            G3D::Vector3 wImpact = (pLocal * mi->iScale) * mi->iRot + mi->iPos;
            G3D::Vector3 wNormal = (nLocal * mi->iRot).directionOrZero();

            candidates.push_back(HitTmp{ toi, cacheIdx, view.triLocalIndex(cacheIdx), mi->ID, wNormal, wImpact, chL.depth * mi->iScale });
        }
    }
    if (!candidates.empty())
    {
        std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b) { if (a.t == b.t) return a.triLocalIdx < b.triLocalIdx; return a.t < b.t; });
        for (auto& c : candidates)
        {
            SceneHit h; h.hit = true; h.time = CapsuleCollision::cc_clamp(c.t, 0.0f, 1.0f); h.distance = h.time * distance; h.penetrationDepth = c.penetrationDepth;
            G3D::Vector3 wSurfN = c.nWorld.directionOrZero(); SceneHit dummy; EnsureUpwardNormal(wSurfN, dummy);
            h.normal = wSurfN; h.point = c.pWorld; h.triIndex = c.triLocalIdx; h.instanceId = c.instId; h.startPenetrating = false; h.normalFlipped = dummy.normalFlipped;
            outHits.push_back(h);

            // Extra: per-hit log with barycentric to correlate with raycast
            const CapsuleCollision::Triangle& Tlocal = view.tri(c.triCacheIdx);
            const ModelInstance* mi = view.triInstance(c.triCacheIdx);
            if (mi)
            {
                G3D::Vector3 wA = (G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z) * mi->iScale) * mi->iRot + mi->iPos;
                G3D::Vector3 wB = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) * mi->iScale) * mi->iRot + mi->iPos;
                G3D::Vector3 wC = (G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) * mi->iScale) * mi->iRot + mi->iPos;
                G3D::Vector3 barySW = ComputeBarycentric(h.point, wA, wB, wC);
                PHYS_TRACE(PHYS_CYL, "[Hit] sweep tri=" << h.triIndex << " instId=" << h.instanceId
                    << " t=" << h.time << " dist=" << h.distance
                    << " pointW=(" << h.point.x << "," << h.point.y << "," << h.point.z << ")"
                    << " normalW=(" << h.normal.x << "," << h.normal.y << "," << h.normal.z << ")"
                    << " bary=(" << barySW.x << "," << barySW.y << "," << barySW.z << ")");
            }
        }
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT hits count=" << outHits.size());
        return (int)outHits.size();
    }

    // Fallback overlap
    const float overlapInflation = 0.01f;
    CapsuleCollision::Capsule inflCaps = capsuleStart; inflCaps.r += overlapInflation;
    std::vector<SceneHit> overlapHits;
    int nOverlap = OverlapCapsule(map, inflCaps, overlapHits, includeMask, params);
    PHYS_TRACE(PHYS_CYL, "Fallback OverlapCapsule (r+=" << overlapInflation << ") returned nOverlap=" << nOverlap);
    if (nOverlap > 0)
    {
        outHits = overlapHits;
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT using fallback overlaps count=" << outHits.size());
        return nOverlap;
    }
    PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT no hits");
    return 0;
}