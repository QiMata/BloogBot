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

    // Local mesh view for building triangle caches out of map tree (internal space)
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

                    // Transform model-local vertices into internal/global space (fix space mismatch)
                    bool canSkipTransform = std::abs(inst.iScale - 1.0f) < 1e-5f;
                    // Skip rotation identity check (Matrix3 API); rely on scale and position only
                    canSkipTransform = canSkipTransform && (std::abs(inst.iPos.x)<1e-5f && std::abs(inst.iPos.y)<1e-5f && std::abs(inst.iPos.z)<1e-5f);

                    G3D::Vector3 ia, ib, ic;
                    if (canSkipTransform) {
                        ia = a; ib = b; ic = c; // vertices already in internal/global space
                    } else {
                        ia = (a * inst.iScale) * inst.iRot + inst.iPos;
                        ib = (b * inst.iScale) * inst.iRot + inst.iPos;
                        ic = (c * inst.iScale) * inst.iRot + inst.iPos;
                    }
                    CapsuleCollision::Triangle T; T.a = { ia.x, ia.y, ia.z }; T.b = { ib.x, ib.y, ib.z }; T.c = { ic.x, ic.y, ic.z }; T.doubleSided = false; T.collisionMask = instMask;
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
        mutable std::vector<CapsuleCollision::Triangle> m_cache;
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

int SceneQuery::OverlapCapsule(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsule,
    std::vector<SceneHit>& outOverlaps,
    uint32_t includeMask,
    const QueryParams& params)
{
    outOverlaps.clear();

    CapsuleCollision::Capsule C = capsule; // world input
    G3D::Vector3 wp0(capsule.p0.x, capsule.p0.y, capsule.p0.z);
    G3D::Vector3 wp1(capsule.p1.x, capsule.p1.y, capsule.p1.z);
    G3D::Vector3 ip0 = NavCoord::WorldToInternal(wp0);
    G3D::Vector3 ip1 = NavCoord::WorldToInternal(wp1);
    C.p0 = { ip0.x, ip0.y, ip0.z }; C.p1 = { ip1.x, ip1.y, ip1.z };

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    // Build internal-space AABB around capsule ends (include radius) with small inflation.
    G3D::Vector3 iLo = ip0.min(ip1) - G3D::Vector3(C.r, C.r, C.r);
    G3D::Vector3 iHi = ip0.max(ip1) + G3D::Vector3(C.r, C.r, C.r);
    CapsuleCollision::AABB internalBox; internalBox.min = { iLo.x, iLo.y, iLo.z }; internalBox.max = { iHi.x, iHi.y, iHi.z }; CapsuleCollision::aabbInflate(internalBox, 0.005f);

    int indices[512]; int count = 0;
    view.query(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int cacheIdx = indices[i];
        const auto& T = view.tri(cacheIdx);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C, T, ch))
        {
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            const ModelInstance* mi = view.triInstance(cacheIdx);
            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi ? mi->ID : 0; EnsureUpwardNormal(h.normal, h); outOverlaps.push_back(h);
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
    CapsuleCollision::Capsule C; C.p0 = { iCenter.x, iCenter.y, iCenter.z }; C.p1 = { iCenter.x, iCenter.y, iCenter.z }; C.r = radius;

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    // Internal-space AABB for sphere
    G3D::Vector3 iLo = iCenter - G3D::Vector3(radius, radius, radius);
    G3D::Vector3 iHi = iCenter + G3D::Vector3(radius, radius, radius);
    CapsuleCollision::AABB internalBox; internalBox.min = { iLo.x, iLo.y, iLo.z }; internalBox.max = { iHi.x, iHi.y, iHi.z }; CapsuleCollision::aabbInflate(internalBox, 0.005f);

    int indices[512]; int count = 0;
    view.query(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int cacheIdx = indices[i];
        const auto& T = view.tri(cacheIdx);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectSphereTriangle(C.p0, C.r, T, ch))
        {
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            const ModelInstance* mi = view.triInstance(cacheIdx);
            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi ? mi->ID : 0; EnsureUpwardNormal(h.normal, h); outOverlaps.push_back(h);
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

    CapsuleCollision::Capsule C0 = capsuleStart;
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
    G3D::Vector3 iP0 = NavCoord::WorldToInternal(wP0);
    G3D::Vector3 iP1 = NavCoord::WorldToInternal(wP1);
    // Roundtrip back to world to verify scaling consistency
    G3D::Vector3 wP0_round = NavCoord::InternalToWorld(iP0);
    G3D::Vector3 wP1_round = NavCoord::InternalToWorld(iP1);
    G3D::Vector3 deltaP0 = wP0_round - wP0;
    G3D::Vector3 deltaP1 = wP1_round - wP1;
    PHYS_TRACE(PHYS_CYL, "[CapsuleRoundTrip] p0W=(" << wP0.x << "," << wP0.y << "," << wP0.z << ") p0I=(" << iP0.x << "," << iP0.y << "," << iP0.z
        << ") p0W2=(" << wP0_round.x << "," << wP0_round.y << "," << wP0_round.z << ") delta=(" << deltaP0.x << "," << deltaP0.y << "," << deltaP0.z << ")");
    PHYS_TRACE(PHYS_CYL, "[CapsuleRoundTrip] p1W=(" << wP1.x << "," << wP1.y << "," << wP1.z << ") p1I=(" << iP1.x << "," << iP1.y << "," << iP1.z
        << ") p1W2=(" << wP1_round.x << "," << wP1_round.y << "," << wP1_round.z << ") delta=(" << deltaP1.x << "," << deltaP1.y << "," << deltaP1.z << ")");
    C0.p0 = { iP0.x, iP0.y, iP0.z }; C0.p1 = { iP1.x, iP1.y, iP1.z };
    G3D::Vector3 iDir = NavCoord::WorldDirToInternal(dir);
    CapsuleCollision::Vec3 vel(iDir.x * distance, iDir.y * distance, iDir.z * distance);

    // Internal-space sweep endpoints and AABB (replace previous world-space box construction)
    G3D::Vector3 iP0End = iP0 + iDir * distance;
    G3D::Vector3 iP1End = iP1 + iDir * distance;
    G3D::Vector3 iMin = iP0.min(iP1).min(iP0End.min(iP1End)) - G3D::Vector3(C0.r, C0.r, C0.r);
    G3D::Vector3 iMax = iP0.max(iP1).max(iP0End.max(iP1End)) + G3D::Vector3(C0.r, C0.r, C0.r);
    CapsuleCollision::AABB sweepBoxI; sweepBoxI.min = { iMin.x, iMin.y, iMin.z }; sweepBoxI.max = { iMax.x, iMax.y, iMax.z }; CapsuleCollision::aabbInflate(sweepBoxI, 0.005f);
    PHYS_TRACE(PHYS_CYL, "Sweep AABB internal min=(" << sweepBoxI.min.x << "," << sweepBoxI.min.y << "," << sweepBoxI.min.z
        << ") max=(" << sweepBoxI.max.x << "," << sweepBoxI.max.y << "," << sweepBoxI.max.z << ")");

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.queryInternal(sweepBoxI, triIdxs, triCount, kCap);
    PHYS_TRACE(PHYS_CYL, "view.queryInternal returned triCount=" << triCount << " (cap=" << kCap << ")");

    // Walkable slope cosine threshold (runtime if available, else default)
    float walkableCos = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    if (PhysicsEngine::Instance())
        walkableCos = PhysicsEngine::Instance()->GetWalkableCosMin();

    auto slopeAngleDeg = [](const G3D::Vector3& n) -> float {
        float cz = std::max(-1.0f, std::min(1.0f, n.z));
        return std::acos(cz) * (180.0f / 3.14159265359f);
    };

    // Diagnostic: log all triangles at t=0 with raw depth, even if not overlapping.
    for (int di = 0; di < triCount; ++di)
    {
        int cacheIdx = triIdxs[di];
        int modelTri = view.triLocalIndex(cacheIdx);
        const auto& T = view.tri(cacheIdx);
        CapsuleCollision::Vec3 sOnSeg, sOnTri;
        CapsuleCollision::closestPoints_Segment_Triangle(C0.p0, C0.p1, T, sOnSeg, sOnTri);
        CapsuleCollision::Vec3 diff = sOnSeg - sOnTri;
        float dist = diff.length();
        float rawDepth = capsuleStart.r - dist; // can be negative if separated
        float segLen2 = (C0.p1 - C0.p0).length2();
        float segT = 0.0f;
        if (segLen2 > CapsuleCollision::EPSILON * CapsuleCollision::EPSILON)
            segT = CapsuleCollision::cc_clamp(CapsuleCollision::Vec3::dot(sOnSeg - C0.p0, (C0.p1 - C0.p0)) / segLen2, 0.0f, 1.0f);
        const char* region = ClassifyCapsuleRegion(segT);
        G3D::Vector3 wOnTri = NavCoord::InternalToWorld(G3D::Vector3(sOnTri.x, sOnTri.y, sOnTri.z));
        G3D::Vector3 wAxisPt = NavCoord::InternalToWorld(G3D::Vector3(sOnSeg.x, sOnSeg.y, sOnSeg.z));
        float radialLen = (wOnTri - wAxisPt).magnitude();
        float radialRatio = capsuleStart.r > 0.0f ? (radialLen / capsuleStart.r) : 0.0f;
        // Triangle normal (internal -> world)
        G3D::Vector3 wA = NavCoord::InternalToWorld(G3D::Vector3(T.a.x, T.a.y, T.a.z));
        G3D::Vector3 wB = NavCoord::InternalToWorld(G3D::Vector3(T.b.x, T.b.y, T.b.z));
        G3D::Vector3 wC = NavCoord::InternalToWorld(G3D::Vector3(T.c.x, T.c.y, T.c.z));
        G3D::Vector3 triN = (wB - wA).cross(wC - wA).directionOrZero();
        float angleDeg = slopeAngleDeg(triN);
        bool walkable = (triN.z >= walkableCos);
        G3D::Vector3 bary = ComputeBarycentric(wOnTri, wA, wB, wC);
        const float edgeEps = 0.05f;
        int smallCount = (bary.x < edgeEps) + (bary.y < edgeEps) + (bary.z < edgeEps);
        const char* triRegion = smallCount >= 2 ? "VERTEX" : (smallCount == 1 ? "EDGE" : "FACE");
        bool overlap = rawDepth >= 0.0f;
        const ModelInstance* mi = view.triInstance(cacheIdx);
        PHYS_TRACE(PHYS_CYL, "t0 triCacheIdx=" << cacheIdx << " modelTri=" << modelTri << " instId=" << (mi?mi->ID:0)
            << " rawDepth=" << rawDepth << " dist=" << dist
            << " segT=" << segT << " region=" << region
            << " radialRatio=" << radialRatio
            << " bary=(" << bary.x << "," << bary.y << "," << bary.z << ") triRegion=" << triRegion
            << " walkable=" << (walkable?"Y":"N") << " cosZ=" << triN.z
            << " overlap=" << (overlap?"Y":"N"));
    }

    // Collect start-penetrating overlaps (t=0)
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const auto& T = view.tri(cacheIdx);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C0, T, ch))
        {
            // Contact normal from intersection
            G3D::Vector3 iContactN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wContactN = NavCoord::InternalDirToWorld(iContactN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            const ModelInstance* mi = view.triInstance(cacheIdx);

            // Triangle surface normal (independent of contact feature) in world space
            G3D::Vector3 wA = NavCoord::InternalToWorld(G3D::Vector3(T.a.x, T.a.y, T.a.z));
            G3D::Vector3 wB = NavCoord::InternalToWorld(G3D::Vector3(T.b.x, T.b.y, T.b.z));
            G3D::Vector3 wC = NavCoord::InternalToWorld(G3D::Vector3(T.c.x, T.c.y, T.c.z));
            G3D::Vector3 wSurfN = (wB - wA).cross(wC - wA).directionOrZero();
            // Ensure upward for surface normal before walkability test (do not mutate contact normal)
            SceneHit dummyHit; EnsureUpwardNormal(wSurfN, dummyHit);
            float angleDegSurf = slopeAngleDeg(wSurfN);
            bool walkableSurf = (wSurfN.z >= walkableCos);

            // Additional contact diagnostics (unchanged)
            CapsuleCollision::Vec3 sOnSeg, sOnTri;
            CapsuleCollision::closestPoints_Segment_Triangle(C0.p0, C0.p1, T, sOnSeg, sOnTri);
            CapsuleCollision::Vec3 segDir = C0.p1 - C0.p0;
            float segLen2 = segDir.length2();
            float segT = 0.0f;
            if (segLen2 > CapsuleCollision::EPSILON * CapsuleCollision::EPSILON)
            {
                segT = CapsuleCollision::cc_clamp(CapsuleCollision::Vec3::dot(sOnSeg - C0.p0, segDir) / segLen2, 0.0f, 1.0f);
            }
            const char* region = ClassifyCapsuleRegion(segT);
            G3D::Vector3 wAxisPt = NavCoord::InternalToWorld(G3D::Vector3(sOnSeg.x, sOnSeg.y, sOnSeg.z));
            G3D::Vector3 wRad = wP - wAxisPt;
            float radialLen = wRad.magnitude();
            float radialRatio = radialLen / C0.r;

            // Triangle barycentric classification (world space)
            G3D::Vector3 bary = ComputeBarycentric(wP, wA, wB, wC);
            const float edgeEps = 0.05f;
            int smallCount = (bary.x < edgeEps) + (bary.y < edgeEps) + (bary.z < edgeEps);
            const char* triRegion = smallCount >= 2 ? "VERTEX" : (smallCount == 1 ? "EDGE" : "FACE");

            PHYS_TRACE(PHYS_CYL, "Start penetration triCacheIdx=" << cacheIdx << " modelTri=" << view.triLocalIndex(cacheIdx) << " instId=" << (mi?mi->ID:0)
                << " depth=" << ch.depth
                << " wPoint=(" << wP.x << "," << wP.y << "," << wP.z << ")"
                << " surfN=(" << wSurfN.x << "," << wSurfN.y << "," << wSurfN.z << ") contactN=(" << wContactN.x << "," << wContactN.y << "," << wContactN.z << ")"
                << " cosZ_surf=" << wSurfN.z << " cosZ_contact=" << wContactN.z << " walkableSurf=" << (walkableSurf?"Y":"N")
                << " slopeDegSurf=" << angleDegSurf << " thresh=" << walkableCos
                << " segT=" << segT << " region=" << region
                << " radialLen=" << radialLen << " radialRatio=" << radialRatio
                << " bary=(" << bary.x << "," << bary.y << "," << bary.z << ") triRegion=" << triRegion);
            // Use surface normal for hit.normal to keep consistent walkability classification downstream
            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wSurfN; h.point = wP; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi ? mi->ID : 0; h.startPenetrating = true; h.penetrationDepth = ch.depth;
            // Already ensured upward on wSurfN; mark flip info
            h.normalFlipped = dummyHit.normalFlipped;
            outHits.push_back(h);
        }
    }
    if (!outHits.empty())
    {
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT with startPenetrating hits count=" << outHits.size());
        std::sort(outHits.begin(), outHits.end(), [](const SceneHit& a, const SceneHit& b) { return a.triIndex < b.triIndex; });
        return (int)outHits.size();
    }

    struct HitTmp { float t; int triCacheIdx; int triLocalIdx; uint32_t instId; G3D::Vector3 nI; G3D::Vector3 pI; float penetrationDepth; };
    std::vector<HitTmp> candidates; candidates.reserve(triCount);
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const auto& T = view.tri(cacheIdx);
        const ModelInstance* mi = view.triInstance(cacheIdx);
        uint32_t instId = mi ? mi->ID : 0;
        float toi; CapsuleCollision::Vec3 n, p;
        bool sweepHit = CapsuleCollision::capsuleTriangleSweep(C0, vel, T, toi, n, p);
        if (sweepHit && toi >= 0.0f && toi <= 1.0f)
        {
            CapsuleCollision::Capsule impactCapsule = C0;
            impactCapsule.p0 = impactCapsule.p0 + vel * toi;
            impactCapsule.p1 = impactCapsule.p1 + vel * toi;
            CapsuleCollision::Hit ch; CapsuleCollision::intersectCapsuleTriangle(impactCapsule, T, ch);
            candidates.push_back(HitTmp{ toi, cacheIdx, view.triLocalIndex(cacheIdx), instId, { n.x, n.y, n.z }, { p.x, p.y, p.z }, ch.depth });
        }
    }
    if (!candidates.empty())
    {
        std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b) { if (a.t == b.t) return a.triLocalIdx < b.triLocalIdx; return a.t < b.t; });
        for (auto& cand : candidates)
        {
            SceneHit hit; hit.hit = true; hit.time = CapsuleCollision::cc_clamp(cand.t, 0.0f, 1.0f); hit.distance = hit.time * distance; hit.penetrationDepth = cand.penetrationDepth;
            G3D::Vector3 wImpact = NavCoord::InternalToWorld(G3D::Vector3(cand.pI.x, cand.pI.y, cand.pI.z));
            G3D::Vector3 wContactN = NavCoord::InternalDirToWorld(G3D::Vector3(cand.nI.x, cand.nI.y, cand.nI.z));
            // Surface normal for classification
            const auto& T = view.tri(cand.triCacheIdx);
            G3D::Vector3 wA = NavCoord::InternalToWorld(G3D::Vector3(T.a.x, T.a.y, T.a.z));
            G3D::Vector3 wB = NavCoord::InternalToWorld(G3D::Vector3(T.b.x, T.b.y, T.b.z));
            G3D::Vector3 wC = NavCoord::InternalToWorld(G3D::Vector3(T.c.x, T.c.y, T.c.z));
            G3D::Vector3 wSurfN = (wB - wA).cross(wC - wA).directionOrZero();
            SceneHit dummyFlip; EnsureUpwardNormal(wSurfN, dummyFlip);
            bool walkableSurf = (wSurfN.z >= walkableCos);
            hit.point = wImpact; hit.normal = wSurfN; hit.triIndex = cand.triLocalIdx; hit.instanceId = cand.instId; hit.startPenetrating = false; hit.normalFlipped = dummyFlip.normalFlipped;
            PHYS_TRACE(PHYS_CYL, "Sweep HIT toi=" << hit.time << " dist=" << hit.distance << " instId=" << hit.instanceId << " modelTri=" << hit.triIndex
                << " impactPoint=(" << hit.point.x << "," << hit.point.y << "," << hit.point.z << ")"
                << " surfN=(" << wSurfN.x << "," << wSurfN.y << "," << wSurfN.z << ") contactN=(" << wContactN.x << "," << wContactN.y << "," << wContactN.z << ")"
                << " cosZ_surf=" << wSurfN.z << " cosZ_contact=" << wContactN.z << " walkableSurf=" << (walkableSurf?"Y":"N")
                << " penetrationDepth=" << hit.penetrationDepth);
            outHits.push_back(hit);
        }
        PHYS_TRACE(PHYS_CYL, "SweepCapsule EXIT hits count=" << outHits.size());
        return (int)outHits.size();
    }

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