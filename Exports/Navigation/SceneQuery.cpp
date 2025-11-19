#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include <algorithm>
#include <cmath>
#include <vector>
#include <cstdlib>
#include <mutex>

using namespace VMAP;

namespace
{
    // Note: debug instance/point helpers removed. We collect triangle candidates in the mesh view
    // and perform diagnostics at the end of SweepCapsuleAll.

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

        void query(const CapsuleCollision::AABB& box, int* outIndices, int& count, int maxCount) const override
        {
            // Mesh query: collect triangles. No debug init here.

            count = 0;
            m_cache.clear();
            m_triToInstance.clear();
            m_triToLocalTri.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
                return;

            // Build world-space AABox from input AABB and convert to internal map space
            G3D::Vector3 wLo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 wHi(box.max.x, box.max.y, box.max.z);
            // Standardize world AABB inflation to catch boundary cases (match sweeps)
            const float wInfl = 0.005f;
            wLo = wLo - G3D::Vector3(wInfl, wInfl, wInfl);
            wHi = wHi + G3D::Vector3(wInfl, wInfl, wInfl);

            // Transform all 8 corners of the world-space AABB into internal space and compute a conservative internal AABB.
            G3D::Vector3 worldCorners[8] = {
                { wLo.x, wLo.y, wLo.z }, { wHi.x, wLo.y, wLo.z }, { wLo.x, wHi.y, wLo.z }, { wHi.x, wHi.y, wLo.z },
                { wLo.x, wLo.y, wHi.z }, { wHi.x, wLo.y, wHi.z }, { wLo.x, wHi.y, wHi.z }, { wHi.x, wHi.y, wHi.z }
            };

            G3D::Vector3 qLo, qHi;
            // initialize with first corner
            G3D::Vector3 iC0 = NavCoord::WorldToInternal(worldCorners[0]);
            qLo = iC0; qHi = iC0;
            for (int ci = 1; ci < 8; ++ci)
            {
                G3D::Vector3 iC = NavCoord::WorldToInternal(worldCorners[ci]);
                qLo.x = std::min(qLo.x, iC.x); qLo.y = std::min(qLo.y, iC.y); qLo.z = std::min(qLo.z, iC.z);
                qHi.x = std::max(qHi.x, iC.x); qHi.y = std::max(qHi.y, iC.y); qHi.z = std::max(qHi.z, iC.z);
            }
            // Inflate more along Y to catch near-miss instances (diagnostic)
            G3D::Vector3 qInflate(0.08f, 6.0f, 0.08f);
            G3D::AABox queryBox(qLo - qInflate, qHi + qInflate);

            // Conservative radius estimate: infer capsule/sweep radius from world-space AABB size
            // The caller includes capsule radius when building the world AABB. Compute an approximate
            // world-space radius as half the largest axis extent and use it to further inflate
            // per-instance model-space boxes below (converted by instance inverse scale).
            G3D::Vector3 worldAabbLo = wLo;
            G3D::Vector3 worldAabbHi = wHi;
            G3D::Vector3 worldExt = worldAabbHi - worldAabbLo;
            float approxWorldRadius = std::max(std::max(worldExt.x, worldExt.y), worldExt.z) * 0.5f;

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            if (!m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap))
            {
                // Diagnostic for 226014 as before
                for (uint32_t i = 0; i < m_instanceCount; ++i)
                {
                    if (m_instances[i].ID == 226014)
                    {
                        bool ib = m_instances[i].iBound.intersects(queryBox);
                        G3D::Vector3 qbLo = queryBox.low(); G3D::Vector3 qbHi = queryBox.high();
                        G3D::Vector3 bLo = m_instances[i].iBound.low(); G3D::Vector3 bHi = m_instances[i].iBound.high();
                        break;
                    }
                }
                // Fallback: brute-force include any instance whose bounds intersect queryBox
                std::vector<char> present(m_instanceCount, 0);
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    const ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if ((inst.GetCollisionMask() & m_includeMask) == 0) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i;
                }
                if (instCount == 0) return; // nothing even after fallback
            }
            else
            {
                // Augment BIH result with a conservative fallback: add any intersecting instance not returned by BIH.
                // This guards against BIH edge-case misses on tight boxes.
                std::vector<char> present(m_instanceCount, 0);
                for (uint32_t k = 0; k < instCount; ++k)
                {
                    uint32_t idx = instIdx[k];
                    if (idx < m_instanceCount) present[idx] = 1;
                }
                uint32_t added = 0;
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    if (present[i]) continue;
                    const ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if ((inst.GetCollisionMask() & m_includeMask) == 0) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i; present[i] = 1; ++added;
                }
            }

            size_t totalEmitted = 0;
            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) { continue; }

                if (!inst.iBound.intersects(queryBox)) {
                    // Instance bounds did not intersect; skip.
                    continue;
                }

                uint32_t instMask = inst.GetCollisionMask();
                if ((instMask & m_includeMask) == 0)
                {
                    continue;
                }

                // Transform query box corners (internal) to model space
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
                // Inflate model-space box (larger Y) plus conservative radius converted to model-space
                G3D::Vector3 mInflate(0.08f, 6.0f, 0.08f);
                // Convert approximate world radius into model-space by applying instance inverse scale
                float modelRadiusInfl = approxWorldRadius * inst.iInvScale;
                G3D::Vector3 modelRadiusVec(modelRadiusInfl, modelRadiusInfl, modelRadiusInfl);
                modelBox = G3D::AABox(modelBox.low() - (mInflate + modelRadiusVec), modelBox.high() + (mInflate + modelRadiusVec));

                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData)
                {
                    if (!inst.iModel->GetAllMeshData(vertices, indices))
                        continue;
                    // Relax culling further for full-mesh path: expand box again slightly
                    const G3D::Vector3 extra(0.10f, 0.50f, 0.10f);
                    modelBox = G3D::AABox(modelBox.low() - extra, modelBox.high() + extra);
                }

                size_t triCount = indices.size() / 3;
                size_t emittedBefore = m_cache.size();
                size_t triVisited = 0, triInBox = 0;

                auto emitTri = [&](const G3D::Vector3& a, const G3D::Vector3& b, const G3D::Vector3& c, int localTriIndex)
                    {
                        // Transform model-space vertex directly into INTERNAL space (do not apply WorldToInternal twice)
                        G3D::Vector3 ia = (a * inst.iScale) * inst.iRot + inst.iPos;
                        G3D::Vector3 ib = (b * inst.iScale) * inst.iRot + inst.iPos;
                        G3D::Vector3 ic = (c * inst.iScale) * inst.iRot + inst.iPos;
                        CapsuleCollision::Triangle T; T.a = { ia.x, ia.y, ia.z }; T.b = { ib.x, ib.y, ib.z }; T.c = { ic.x, ic.y, ic.z }; T.doubleSided = true; T.collisionMask = instMask;
                        int triIndex = (int)m_cache.size();
                        m_cache.push_back(T); m_triToInstance.push_back(idx); m_triToLocalTri.push_back(localTriIndex);
                        if (count < maxCount) outIndices[count++] = triIndex;
                    };

                for (size_t t = 0; t < triCount; ++t)
                {
                    uint32_t i0 = indices[t * 3 + 0];
                    uint32_t i1 = indices[t * 3 + 1];
                    uint32_t i2 = indices[t * 3 + 2];
                    if (i0 >= vertices.size() || i1 >= vertices.size() || i2 >= vertices.size()) continue;
                    const G3D::Vector3& a = vertices[i0];
                    const G3D::Vector3& b = vertices[i1];
                    const G3D::Vector3& c = vertices[i2];

                    ++triVisited;
                    if (haveBoundsData)
                    {
                        G3D::Vector3 lo = a.min(b).min(c);
                        G3D::Vector3 hi = a.max(b).max(c);
                        G3D::AABox triBox(lo, hi);
                        if (!triBox.intersects(modelBox))
                            continue;
                        ++triInBox;
                    }
                    else
                    {
                        ++triInBox; // full-mesh path
                    }

                    emitTri(a, b, c, (int)t);
                    if (count >= maxCount) break;
                }

                size_t emittedNow = m_cache.size() - emittedBefore;
                totalEmitted += emittedNow;

                // Sample first few emitted triangles for diagnostic (log world-space triangle verts)
                if (emittedNow > 0)
                {
                    int samples = 0;
                    const int maxSamples = 6;
                    for (size_t triIdx = emittedBefore; triIdx < m_cache.size() && samples < maxSamples; ++triIdx, ++samples)
                    {
                        const auto& TT = m_cache[triIdx];
                        // Convert internal tri verts back to world for logging
                        G3D::Vector3 wA = NavCoord::InternalToWorld(G3D::Vector3(TT.a.x, TT.a.y, TT.a.z));
                        G3D::Vector3 wB = NavCoord::InternalToWorld(G3D::Vector3(TT.b.x, TT.b.y, TT.b.z));
                        G3D::Vector3 wC = NavCoord::InternalToWorld(G3D::Vector3(TT.c.x, TT.c.y, TT.c.z));

                        // Internal-space triangle coords (TT.* are stored in internal space)
                        G3D::Vector3 iA(TT.a.x, TT.a.y, TT.a.z);
                        G3D::Vector3 iB(TT.b.x, TT.b.y, TT.b.z);
                        G3D::Vector3 iC(TT.c.x, TT.c.y, TT.c.z);
                        G3D::Vector3 triLoI = iA.min(iB).min(iC);
                        G3D::Vector3 triHiI = iA.max(iB).max(iC);
                        G3D::AABox triBoxI(triLoI, triHiI);
                        bool intersectsQuery = triBoxI.intersects(queryBox);
                        if (intersectsQuery)
                        {
                            G3D::Vector3 qLo = queryBox.low();
                            G3D::Vector3 qHi = queryBox.high();
                            // Emit internal coords and offsets relative to query box to see any offshift
                            G3D::Vector3 offLo = triLoI - qLo;
                            G3D::Vector3 offHi = triHiI - qHi;
                        }
                    }
                }

                if (count >= maxCount) break;
            }
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

static inline const char* CapsulePartFromT(float t)
{
    if (t <= 0.1f) return "cap-bottom";
    if (t >= 0.9f) return "cap-top";
    return "side";
}

static inline void LogTriangleSurfaceInfo(const CapsuleCollision::Triangle& T, int triLocalIdx)
{
    CapsuleCollision::Vec3 N; float d;
    CapsuleCollision::trianglePlane(T, N, d);
    G3D::Vector3 iN(N.x, N.y, N.z);
    G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
    G3D::Vector3 va = NavCoord::InternalToWorld(G3D::Vector3(T.a.x, T.a.y, T.a.z));
    G3D::Vector3 vb = NavCoord::InternalToWorld(G3D::Vector3(T.b.x, T.b.y, T.b.z));
    G3D::Vector3 vc = NavCoord::InternalToWorld(G3D::Vector3(T.c.x, T.c.y, T.c.z));
    G3D::Vector3 cent = (va + vb + vc) * (1.0f / 3.0f);

    // Cross-product consistency check (analytic normal from vertices)
    {
        // Compute edges in INTERNAL space
        CapsuleCollision::Vec3 u = T.b - T.a;
        CapsuleCollision::Vec3 v = T.c - T.a;
        // cross in internal
        CapsuleCollision::Vec3 crossI = { u.y * v.z - u.z * v.y,
                                          u.z * v.x - u.x * v.z,
                                          u.x * v.y - u.y * v.x };
        float lenI = std::sqrt(crossI.x * crossI.x + crossI.y * crossI.y + crossI.z * crossI.z);
        if (lenI <= 1e-6f)
        {
        }
        else
        {
            // normalize internal cross
            CapsuleCollision::Vec3 crossINorm = { crossI.x / lenI, crossI.y / lenI, crossI.z / lenI };
            // convert analytic normal to world via InternalDirToWorld
            G3D::Vector3 crossINormG3D(crossINorm.x, crossINorm.y, crossINorm.z);
            G3D::Vector3 crossW = NavCoord::InternalDirToWorld(crossINormG3D);
            // normalize world cross
            float lenW = std::sqrt(crossW.x * crossW.x + crossW.y * crossW.y + crossW.z * crossW.z);
            if (lenW > 1e-6f)
            {
                crossW = crossW / lenW;
            }
            // compare with wN (may be unnormalized)
            float wNlen = std::sqrt(wN.x * wN.x + wN.y * wN.y + wN.z * wN.z);
            G3D::Vector3 wNNorm = wNlen > 1e-6f ? (wN / wNlen) : wN;
            float dotv = wNNorm.dot(crossW);
            float angleDeg = 0.0f;
            if (dotv >= -1.0f && dotv <= 1.0f)
                angleDeg = std::acos(std::min(1.0f, std::max(-1.0f, dotv))) * (180.0f / 3.14159265358979323846f);

            // If the analytic normal is opposite sign, log note (winding difference)
            if (dotv < 0.0f)
            {
            }
        }
    }
}

bool SceneQuery::RaycastSingle(const StaticMapTree& map,
    const G3D::Vector3& origin,
    const G3D::Vector3& dir,
    float maxDistance,
    SceneHit& outHit,
    const QueryParams& params)
{
    outHit = SceneHit();
    if (maxDistance <= 0.0f)
        return false;

    // Convert origin and compute internal-space max distance by mapping the end point
    G3D::Vector3 iOrigin = NavCoord::WorldToInternal(origin);
    G3D::Vector3 iEndWorld = origin + dir * maxDistance;
    G3D::Vector3 iEnd = NavCoord::WorldToInternal(iEndWorld);
    G3D::Vector3 iDir = iEnd - iOrigin;
    float distI = iDir.magnitude();
    if (distI <= 1e-9f)
        return false;

    // Use the internal direction vector for the ray (may be non-unit)
    G3D::Vector3 iDirNorm = iDir; // keep as-is; getIntersectionTime expects param in same units
    G3D::Ray ray = G3D::Ray::fromOriginAndDirection(iOrigin, iDirNorm);
    float hitDistI = distI;
    bool hitAny = map.getIntersectionTime(ray, hitDistI, true, false);
    if (!hitAny)
        return false;

    // Compute world-space hit point from internal hit data
    G3D::Vector3 iHitPoint = iOrigin + iDirNorm * hitDistI;
    G3D::Vector3 wHitPoint = NavCoord::InternalToWorld(iHitPoint);

    // Compute actual world-space distance along original world direction
    float worldDist = (wHitPoint - origin).magnitude();

    outHit.hit = true;
    outHit.distance = worldDist;
    outHit.time = (maxDistance > 0.0f ? CapsuleCollision::cc_clamp(worldDist / maxDistance, 0.0f, 1.0f) : 0.0f);
    outHit.point = wHitPoint;
    return true;
}

int SceneQuery::RaycastAll(const StaticMapTree& map,
    const G3D::Vector3& origin,
    const G3D::Vector3& dir,
    float maxDistance,
    std::vector<SceneHit>& outHits,
    const QueryParams& params)
{
    outHits.clear();
    SceneHit h;
    if (RaycastSingle(map, origin, dir, maxDistance, h, params))
    {
        outHits.push_back(h);
        return 1;
    }
    return 0;
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

    G3D::Vector3 wLo = wp0.min(wp1) - G3D::Vector3(C.r, C.r, C.r);
    G3D::Vector3 wHi = wp0.max(wp1) + G3D::Vector3(C.r, C.r, C.r);
    // Standardize inflation like sweeps
    const float wInfl = 0.005f;
    wLo = wLo - G3D::Vector3(wInfl, wInfl, wInfl);
    wHi = wHi + G3D::Vector3(wInfl, wInfl, wInfl);
    CapsuleCollision::AABB worldBox = AABBFromAABox(G3D::AABox(wLo, wHi));

    int indices[512]; int count = 0;
    view.query(worldBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int triIdx = indices[i];
        const auto& T = view.tri(triIdx);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C, T, ch))
        {
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);

            CapsuleCollision::Vec3 onSeg, onTri;
            if (true)
            {
                CapsuleCollision::Vec3 sOnSeg, sOnTri;
                CapsuleCollision::closestPoints_Segment_Triangle(C.p0, C.p1, T, sOnSeg, sOnTri);
                onSeg = sOnSeg; onTri = sOnTri;
            }
            CapsuleCollision::Vec3 segDir = C.p1 - C.p0;
            float segLen2 = segDir.length2();
            float t = 0.0f;
            if (segLen2 > CapsuleCollision::EPSILON * CapsuleCollision::EPSILON)
            {
                CapsuleCollision::Vec3 rel = onSeg - C.p0;
                t = CapsuleCollision::cc_clamp(CapsuleCollision::Vec3::dot(rel, segDir) / segLen2, 0.0f, 1.0f);
            }
            const char* part = CapsulePartFromT(t);

            LogTriangleSurfaceInfo(T, view.triLocalIndex(triIdx));

            const ModelInstance* mi = view.triInstance(triIdx);
            if (mi)
            {
                G3D::Vector3 wPos = NavCoord::InternalToWorld(mi->iPos);
                G3D::Vector3 rotDeg = mi->ModelSpawn::iRot;
                G3D::Vector3 ip0v(C.p0.x, C.p0.y, C.p0.z);
                G3D::Vector3 ip1v(C.p1.x, C.p1.y, C.p1.z);
                G3D::Vector3 localP0 = mi->iInvRot * ((ip0v - mi->iPos) * mi->iInvScale);
                G3D::Vector3 localP1 = mi->iInvRot * ((ip1v - mi->iPos) * mi->iInvScale);
            }

            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f;
            h.normal = wN; h.point = wP; h.triIndex = ch.triIndex; h.instanceId = mi ? mi->ID : 0;
            EnsureUpwardNormal(h.normal, h);
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

    G3D::Vector3 wLo = center - G3D::Vector3(radius, radius, radius);
    G3D::Vector3 wHi = center + G3D::Vector3(radius, radius, radius);
    // Standardize inflation like sweeps
    const float wInfl = 0.005f;
    wLo = wLo - G3D::Vector3(wInfl, wInfl, wInfl);
    wHi = wHi + G3D::Vector3(wInfl, wInfl, wInfl);
    CapsuleCollision::AABB worldBox = AABBFromAABox(G3D::AABox(wLo, wHi));

    G3D::Vector3 iCenter = NavCoord::WorldToInternal(center);

    CapsuleCollision::Capsule C; C.p0 = { iCenter.x, iCenter.y, iCenter.z }; C.p1 = { iCenter.x, iCenter.y, iCenter.z }; C.r = radius;

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    int indices[512]; int count = 0;
    view.query(worldBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int triIdx = indices[i];
        const auto& T = view.tri(triIdx);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectSphereTriangle(C.p0, C.r, T, ch))
        {
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            LogTriangleSurfaceInfo(T, view.triLocalIndex(triIdx));

            const ModelInstance* mi = view.triInstance(triIdx);
            if (mi)
            {
                G3D::Vector3 wPos = NavCoord::InternalToWorld(mi->iPos);
                G3D::Vector3 rotDeg = mi->ModelSpawn::iRot;
                G3D::Vector3 localC = mi->iInvRot * ((G3D::Vector3(C.p0.x, C.p0.y, C.p0.z) - mi->iPos) * mi->iInvScale);
            }
            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = ch.triIndex; h.instanceId = mi ? mi->ID : 0;
            EnsureUpwardNormal(h.normal, h);
            outOverlaps.push_back(h);
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

bool SceneQuery::SweepCapsuleSingle(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    SceneHit& outHit,
    uint32_t includeMask,
    const QueryParams& params)
{
    // Centralize logic: use SweepCapsuleAll and return the first hit if any.
    outHit = SceneHit();
    std::vector<SceneHit> hits;
    int n = SweepCapsuleAll(map, capsuleStart, dir, distance, hits, includeMask, params);
    if (n <= 0) return false;
    outHit = hits.front();
    return true;
}

int SceneQuery::SweepCapsuleAll(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    std::vector<SceneHit>& outHits,
    uint32_t includeMask,
    const QueryParams& params)
{
    outHits.clear();
    if (distance <= 0.0f)
        return 0;

    // Convert capsule and direction to internal space (for intersection tests)
    CapsuleCollision::Capsule C0 = capsuleStart;
    G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
    G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);

    // If caller provided an inflation (used here to reduce sweep start offset), nudge the capsule
    // start position slightly toward the sweep direction to reduce misses due to tiny offsets.
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
    C0.p0 = { iP0.x, iP0.y, iP0.z }; C0.p1 = { iP1.x, iP1.y, iP1.z };
    G3D::Vector3 iDir = NavCoord::WorldDirToInternal(dir);
    CapsuleCollision::Vec3 vel(iDir.x * distance, iDir.y * distance, iDir.z * distance);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    // Broad-phase: swept AABB between start and end (world space)
    G3D::Vector3 wP0End = wP0 + dir * distance;
    G3D::Vector3 wP1End = wP1 + dir * distance;
    G3D::Vector3 wMin = wP0.min(wP1).min(wP0End.min(wP1End)) - G3D::Vector3(C0.r, C0.r, C0.r);
    G3D::Vector3 wMax = wP0.max(wP1).max(wP0End.max(wP1End)) + G3D::Vector3(C0.r, C0.r, C0.r);
    CapsuleCollision::AABB sweepBox = AABBFromAABox(G3D::AABox(wMin, wMax));
    CapsuleCollision::aabbInflate(sweepBox, 0.005f);
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.query(sweepBox, triIdxs, triCount, kCap);

    // Diagnostic: log sweep geometry (world and internal) and sweep params before per-triangle evaluation

    // Gather start-penetrating overlaps first (t=0). If any, early out after collecting all at t=0
    std::vector<SceneHit> startHits;
    for (int i = 0; i < triCount; ++i)
    {
        int ti = triIdxs[i];
        const auto& T = view.tri(ti);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C0, T, ch))
        {
            // Convert to world
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            const ModelInstance* mi = view.triInstance(ti);

            // Log the triangle we intersected (start-penetrating)

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = ti; h.instanceId = mi ? mi->ID : 0; h.startPenetrating = true;
            EnsureUpwardNormal(h.normal, h);
            startHits.push_back(h);
        }
    }
    if (!startHits.empty())
    {
        // Sort deterministically by tri index for stability
        std::sort(startHits.begin(), startHits.end(), [](const SceneHit& a, const SceneHit& b) { return a.triIndex < b.triIndex; });
        outHits = startHits;
        return (int)outHits.size();
    }

    // --- replace centroid-based ray target with triangle-closest-point ray and prefer triangles within radius ---
    const int kDiagnosticCount = 6;
    struct TriDist { int triIdx; float dist2; float dist; G3D::Vector3 centroidW; G3D::Vector3 triClosestI; G3D::Vector3 segClosestI; };
    std::vector<TriDist> triDists; triDists.reserve(triCount);

    for (int i = 0; i < triCount; ++i)
    {
        int ti = triIdxs[i];
        const auto& T = view.tri(ti);

        // compute closest points between capsule segment (internal) and triangle (internal)
        CapsuleCollision::Vec3 sSeg, sTri;
        CapsuleCollision::closestPoints_Segment_Triangle(C0.p0, C0.p1, T, sSeg, sTri);
        G3D::Vector3 pSegI(sSeg.x, sSeg.y, sSeg.z);
        G3D::Vector3 pTriI(sTri.x, sTri.y, sTri.z);
        G3D::Vector3 diff = pSegI - pTriI;
        float d2 = diff.x * diff.x + diff.y * diff.y + diff.z * diff.z;
        float d = std::sqrt(d2);

        // centroid world for logging convenience
        G3D::Vector3 iA(T.a.x, T.a.y, T.a.z), iB(T.b.x, T.b.y, T.b.z), iC(T.c.x, T.c.y, T.c.z);
        G3D::Vector3 centI = (iA + iB + iC) * (1.0f / 3.0f);
        G3D::Vector3 centW = NavCoord::InternalToWorld(centI);

        TriDist td; td.triIdx = ti; td.dist2 = d2; td.dist = d; td.centroidW = centW; td.triClosestI = pTriI; td.segClosestI = pSegI;
        triDists.push_back(td);
    }

    // sort globally by distance
    std::sort(triDists.begin(), triDists.end(), [](const TriDist& a, const TriDist& b) { return a.dist2 < b.dist2; });

    // Prefer triangles whose closest-segment distance is within capsule radius + epsilon.
    const float kNearEps = 0.01f;
    std::vector<TriDist> selectedTDs; selectedTDs.reserve(kDiagnosticCount);
    for (const TriDist& td : triDists)
    {
        if ((float)selectedTDs.size() >= kDiagnosticCount) break;
        if (td.dist <= (C0.r + kNearEps))
            selectedTDs.push_back(td);
    }
    // If not enough near candidates, fill from nearest by distance
    if ((int)selectedTDs.size() < kDiagnosticCount)
    {
        for (const TriDist& td : triDists)
        {
            if ((int)selectedTDs.size() >= kDiagnosticCount) break;
            // avoid duplicates
            bool found = false;
            for (const TriDist& s : selectedTDs) if (s.triIdx == td.triIdx) { found = true; break; }
            if (!found) selectedTDs.push_back(td);
        }
    }

    struct HitTmp { float t; int triIdx; G3D::Vector3 nI; G3D::Vector3 pI; uint32_t instId; };
    std::vector<HitTmp> candidates; candidates.reserve(selectedTDs.size());

    for (const TriDist& td : selectedTDs)
    {
        int selTriIdx = td.triIdx;
        const auto& T = view.tri(selTriIdx);
        int triLocal = view.triLocalIndex(selTriIdx);
        const ModelInstance* mi = view.triInstance(selTriIdx);
        uint32_t instId = mi ? mi->ID : 0;

        float toi; CapsuleCollision::Vec3 n, p;
        bool sweepHit = CapsuleCollision::capsuleTriangleSweep(C0, vel, T, toi, n, p);
        if (sweepHit && toi >= 0.0f && toi <= 1.0f)
        {
            HitTmp tmp; tmp.t = toi; tmp.triIdx = selTriIdx; tmp.nI = { n.x, n.y, n.z }; tmp.pI = { p.x, p.y, p.z };
            tmp.instId = instId;
            candidates.push_back(tmp);
        }
    }

    // If analytic candidates found among the selected triangles, pick earliest TOI as before
    if (!candidates.empty())
    {
        std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b) { if (a.t == b.t) return a.triIdx < b.triIdx; return a.t < b.t; });
        const HitTmp& best = candidates.front();

        SceneHit bestHit;
        bestHit.hit = true;
        bestHit.time = CapsuleCollision::cc_clamp(best.t, 0.0f, 1.0f);
        bestHit.distance = bestHit.time * distance;
        G3D::Vector3 iImpact(best.pI.x, best.pI.y, best.pI.z);
        G3D::Vector3 wImpact = NavCoord::InternalToWorld(iImpact);
        G3D::Vector3 iNormal(best.nI.x, best.nI.y, best.nI.z);
        G3D::Vector3 wNormal = NavCoord::InternalDirToWorld(iNormal);
        bestHit.point = wImpact; bestHit.normal = wNormal; bestHit.triIndex = best.triIdx; bestHit.instanceId = best.instId;
        bestHit.startPenetrating = false;
        EnsureUpwardNormal(bestHit.normal, bestHit);

        outHits.push_back(bestHit);
        return 1;
    }

    const float overlapInflation = 0.01f;
    CapsuleCollision::Capsule inflCaps = capsuleStart;
    inflCaps.r += overlapInflation;
    std::vector<SceneHit> overlapHits;
    int nOverlap = OverlapCapsule(map, inflCaps, overlapHits, includeMask, params);
    if (nOverlap > 0)
    {
        outHits = overlapHits;
        return nOverlap;
    }

    return 0;
}

// New pure sweep that only finds time of impact and impact point/normal.
bool SceneQuery::SweepCapsuleTOI(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    SceneHit& outHit,
    uint32_t includeMask,
    const QueryParams& params)
{
    // Use the centralized SweepCapsuleAll implementation and extract the earliest hit (TOI)
    outHit = SceneHit();
    std::vector<SceneHit> hits;
    int n = SweepCapsuleAll(map, capsuleStart, dir, distance, hits, includeMask, params);
    if (n <= 0) return false;
    // hits are the earliest cohort (or start-penetrating); use the first one as the TOI
    outHit = hits.front();
    return true;
}

// Debug helper: test all triangles of a specific instance against a world-space capsule and log any collisions.
// Returns number of triangles that intersect the capsule.
int SceneQuery::DebugTestInstanceCapsuleTriangles(const StaticMapTree& map, uint32_t instanceId, const CapsuleCollision::Capsule& capsuleWorld)
{
    int hitCount = 0;
    // Convert world capsule to internal space
    G3D::Vector3 wp0(capsuleWorld.p0.x, capsuleWorld.p0.y, capsuleWorld.p0.z);
    G3D::Vector3 wp1(capsuleWorld.p1.x, capsuleWorld.p1.y, capsuleWorld.p1.z);
    G3D::Vector3 ip0 = NavCoord::WorldToInternal(wp0);
    G3D::Vector3 ip1 = NavCoord::WorldToInternal(wp1);
    CapsuleCollision::Capsule C = capsuleWorld;
    C.p0 = { ip0.x, ip0.y, ip0.z };
    C.p1 = { ip1.x, ip1.y, ip1.z };

    const ModelInstance* found = nullptr;
    const ModelInstance* instances = map.GetInstancesPtr();
    uint32_t instCount = map.GetInstanceCount();
    for (uint32_t i = 0; i < instCount; ++i)
    {
        if (instances[i].ID == instanceId)
        {
            found = &instances[i];
            break;
        }
    }
    if (!found)
    {
        return 0;
    }
    if (!found->iModel)
    {
        return 0;
    }

    // Iterate groups and triangles
    for (uint32_t gi = 0; ; ++gi)
    {
        const GroupModel* gm = found->iModel->GetGroupModel(gi);
        if (!gm) break;
        const auto& verts = gm->GetVertices();
        const auto& tris = gm->GetTriangles();
        for (uint32_t ti = 0; ti < tris.size(); ++ti)
        {
            const MeshTriangle& mt = tris[ti];
            if (mt.idx0 >= verts.size() || mt.idx1 >= verts.size() || mt.idx2 >= verts.size())
                continue;
            // Convert model-space vertices into INTERNAL space using instance transform
            G3D::Vector3 a_model = verts[mt.idx0];
            G3D::Vector3 b_model = verts[mt.idx1];
            G3D::Vector3 c_model = verts[mt.idx2];
            G3D::Vector3 a_internal = (a_model * found->iScale) * found->iRot + found->iPos;
            G3D::Vector3 b_internal = (b_model * found->iScale) * found->iRot + found->iPos;
            G3D::Vector3 c_internal = (c_model * found->iScale) * found->iRot + found->iPos;

            // Build CapsuleCollision::Triangle in internal space
            CapsuleCollision::Triangle T;
            T.a = { a_internal.x, a_internal.y, a_internal.z };
            T.b = { b_internal.x, b_internal.y, b_internal.z };
            T.c = { c_internal.x, c_internal.y, c_internal.z };
            T.doubleSided = true;
            T.collisionMask = found->GetCollisionMask();

            // Test intersection
            CapsuleCollision::Hit ch;
            if (CapsuleCollision::intersectCapsuleTriangle(C, T, ch))
            {
                ++hitCount;
                G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
                G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
                G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
                G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);

                // Also compute closest segment-triangle distance for comparison
                CapsuleCollision::Vec3 sSeg, sTri;
                CapsuleCollision::closestPoints_Segment_Triangle(C.p0, C.p1, T, sSeg, sTri);
                G3D::Vector3 pSegI(sSeg.x, sSeg.y, sSeg.z);
                G3D::Vector3 pTriI(sTri.x, sTri.y, sTri.z);
                float d = (pSegI - pTriI).magnitude();
                G3D::Vector3 pSegW = NavCoord::InternalToWorld(pSegI);
                G3D::Vector3 pTriW = NavCoord::InternalToWorld(pTriI);
            }
        }
    }

    return hitCount;
}