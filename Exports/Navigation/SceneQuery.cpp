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
            PHYS_TRACE(PHYS_CYL, "MapMeshView::query ENTER includeMask=0x" << std::hex << m_includeMask << std::dec
                << " instanceCount=" << m_instanceCount
                << " inputBox.min=(" << box.min.x << "," << box.min.y << "," << box.min.z << ")"
                << " inputBox.max=(" << box.max.x << "," << box.max.y << "," << box.max.z << ")");
            // Mesh query: collect triangles. No debug init here.

            count = 0;
            m_cache.clear();
            m_triToInstance.clear();
            m_triToLocalTri.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
            {
                PHYS_TRACE(PHYS_CYL, "MapMeshView::query EARLY EXIT (invalid inputs) m_tree=" << (m_tree?"Y":"N")
                    << " m_instances=" << (m_instances?"Y":"N") << " m_instanceCount=" << m_instanceCount
                    << " outIndices=" << (outIndices?"Y":"N") << " maxCount=" << maxCount);
                return;
            }

            // Build world-space AABox from input AABB and convert to internal map space
            G3D::Vector3 wLo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 wHi(box.max.x, box.max.y, box.max.z);
            // Standardize world AABB inflation to catch boundary cases (match sweeps)
            const float wInfl = 0.005f;
            wLo = wLo - G3D::Vector3(wInfl, wInfl, wInfl);
            wHi = wHi + G3D::Vector3(wInfl, wInfl, wInfl);
            PHYS_TRACE(PHYS_CYL, "World AABB inflated wInfl=" << wInfl << " wLo=(" << wLo.x << "," << wLo.y << "," << wLo.z
                << ") wHi=(" << wHi.x << "," << wHi.y << "," << wHi.z << ")");

            // Transform all 8 corners of the world-space AABB into internal space and compute a conservative internal AABB.
            G3D::Vector3 worldCorners[8] = {
                { wLo.x, wLo.y, wLo.z }, { wHi.x, wLo.y, wLo.z }, { wLo.x, wHi.y, wLo.z }, { wHi.x, wHi.y, wLo.z },
                { wLo.x, wLo.y, wHi.z }, { wHi.x, wLo.y, wHi.z }, { wLo.x, wHi.y, wHi.z }, { wHi.x, wHi.y, wHi.z }
            };
            for (int ciLog = 0; ciLog < 8; ++ciLog)
                PHYS_TRACE(PHYS_CYL, "World corner[" << ciLog << "]=(" << worldCorners[ciLog].x << "," << worldCorners[ciLog].y << "," << worldCorners[ciLog].z << ")");

            G3D::Vector3 qLo, qHi;
            // initialize with first corner
            G3D::Vector3 iC0 = NavCoord::WorldToInternal(worldCorners[0]);
            qLo = iC0; qHi = iC0;
            PHYS_TRACE(PHYS_CYL, "Internal corner[0]=(" << iC0.x << "," << iC0.y << "," << iC0.z << ") (init)");
            for (int ci = 1; ci < 8; ++ci)
            {
                G3D::Vector3 iC = NavCoord::WorldToInternal(worldCorners[ci]);
                qLo.x = std::min(qLo.x, iC.x); qLo.y = std::min(qLo.y, iC.y); qLo.z = std::min(qLo.z, iC.z);
                qHi.x = std::max(qHi.x, iC.x); qHi.y = std::max(qHi.y, iC.y); qHi.z = std::max(qHi.z, iC.z);
                PHYS_TRACE(PHYS_CYL, "Internal corner[" << ci << "]=(" << iC.x << "," << iC.y << "," << iC.z << ")");
            }
            // Inflate more along Y to catch near-miss instances (diagnostic)
            G3D::Vector3 qInflate(0.08f, 6.0f, 0.08f);
            G3D::AABox queryBox(qLo - qInflate, qHi + qInflate);
            PHYS_TRACE(PHYS_CYL, "Internal AABB pre-inflate qLo=(" << qLo.x << "," << qLo.y << "," << qLo.z << ") qHi=(" << qHi.x << "," << qHi.y << "," << qHi.z
                << ") inflate=(" << qInflate.x << "," << qInflate.y << "," << qInflate.z << ") queryBox.low=(" << queryBox.low().x << "," << queryBox.low().y << "," << queryBox.low().z
                << ") high=(" << queryBox.high().x << "," << queryBox.high().y << "," << queryBox.high().z << ")");

            // Conservative radius estimate
            G3D::Vector3 worldAabbLo = wLo;
            G3D::Vector3 worldAabbHi = wHi;
            G3D::Vector3 worldExt = worldAabbHi - worldAabbLo;
            float approxWorldRadius = std::max(std::max(worldExt.x, worldExt.y), worldExt.z) * 0.5f;
            PHYS_TRACE(PHYS_CYL, "World extents=(" << worldExt.x << "," << worldExt.y << "," << worldExt.z << ") approxWorldRadius=" << approxWorldRadius);

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            bool bihOk = m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap);
            PHYS_TRACE(PHYS_CYL, "BIH QueryAABB result=" << (bihOk?"OK":"MISS") << " instCount=" << instCount << " cap=" << cap);
            if (!bihOk)
            {
                // Diagnostic for 226014 as before
                for (uint32_t i = 0; i < m_instanceCount; ++i)
                {
                    if (m_instances[i].ID == 226014)
                    {
                        bool ib = m_instances[i].iBound.intersects(queryBox);
                        G3D::Vector3 qbLo = queryBox.low(); G3D::Vector3 qbHi = queryBox.high();
                        G3D::Vector3 bLo = m_instances[i].iBound.low(); G3D::Vector3 bHi = m_instances[i].iBound.high();
                        PHYS_TRACE(PHYS_CYL, "DIAG instance 226014 bound.low=(" << bLo.x << "," << bLo.y << "," << bLo.z << ") bound.high=(" << bHi.x << "," << bHi.y << "," << bHi.z
                            << ") query.low=(" << qbLo.x << "," << qbLo.y << "," << qbLo.z << ") query.high=(" << qbHi.x << "," << qbHi.y << "," << qbHi.z << ") intersects=" << (ib?"Y":"N"));
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
                PHYS_TRACE(PHYS_CYL, "Fallback brute-force instCount=" << instCount);
                if (instCount == 0) { PHYS_TRACE(PHYS_CYL, "MapMeshView::query EXIT no instances after fallback"); return; }
            }
            else
            {
                // Augment BIH result with a conservative fallback
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
                PHYS_TRACE(PHYS_CYL, "Augmented BIH instCount=" << instCount << " added=" << added);
            }
            for (uint32_t l = 0; l < instCount; ++l)
            {
                uint32_t ii = instIdx[l];
                if (ii >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[ii];
                G3D::Vector3 bLo = inst.iBound.low(); G3D::Vector3 bHi = inst.iBound.high();
                PHYS_TRACE(PHYS_CYL, "Instance sel idx=" << ii << " ID=" << inst.ID << " bound.low=(" << bLo.x << "," << bLo.y << "," << bLo.z << ") bound.high=(" << bHi.x << "," << bHi.y << "," << bHi.z
                    << ") scale=" << inst.iScale << " invScale=" << inst.iInvScale << " pos=(" << inst.iPos.x << "," << inst.iPos.y << "," << inst.iPos.z << ")");
            }

            size_t totalEmitted = 0;
            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) { PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " skipped (no model)"); continue; }

                if (!inst.iBound.intersects(queryBox)) {
                    PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " ID=" << inst.ID << " skipped (bounds miss query)");
                    continue;
                }

                uint32_t instMask = inst.GetCollisionMask();
                if ((instMask & m_includeMask) == 0)
                {
                    PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " ID=" << inst.ID << " skipped (mask filter) instMask=0x" << std::hex << instMask << std::dec);
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
                G3D::Vector3 mbLo0 = modelBox.low(); G3D::Vector3 mbHi0 = modelBox.high();
                // Inflate model-space box (larger Y) plus conservative radius converted to model-space
                G3D::Vector3 mInflate(0.08f, 6.0f, 0.08f);
                float modelRadiusInfl = approxWorldRadius * inst.iInvScale;
                G3D::Vector3 modelRadiusVec(modelRadiusInfl, modelRadiusInfl, modelRadiusInfl);
                modelBox = G3D::AABox(modelBox.low() - (mInflate + modelRadiusVec), modelBox.high() + (mInflate + modelRadiusVec));
                G3D::Vector3 mbLo = modelBox.low(); G3D::Vector3 mbHi = modelBox.high();
                PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " ID=" << inst.ID << " modelBox preInflate.low=(" << mbLo0.x << "," << mbLo0.y << "," << mbLo0.z
                    << ") preInflate.high=(" << mbHi0.x << "," << mbHi0.y << "," << mbHi0.z << ") inflateVec=(" << (mInflate.x+modelRadiusVec.x) << "," << (mInflate.y+modelRadiusVec.y) << "," << (mInflate.z+modelRadiusVec.z)
                    << ") final.low=(" << mbLo.x << "," << mbLo.y << "," << mbLo.z << ") final.high=(" << mbHi.x << "," << mbHi.x << "," << mbHi.z << ")");

                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " haveBoundsData=" << (haveBoundsData?"Y":"N") << " verts=" << vertices.size() << " indices=" << indices.size());
                if (!haveBoundsData)
                {
                    if (!inst.iModel->GetAllMeshData(vertices, indices)) {
                        PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " GetAllMeshData FAILED");
                        continue;
                    }
                    const G3D::Vector3 extra(0.10f, 0.50f, 0.10f);
                    modelBox = G3D::AABox(modelBox.low() - extra, modelBox.high() + extra);
                    PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " Expanded full-mesh modelBox extra=(" << extra.x << "," << extra.y << "," << extra.z
                        << ") new.low=(" << modelBox.low().x << "," << modelBox.low().y << "," << modelBox.low().z << ") new.high=(" << modelBox.high().x << "," << modelBox.high().y << "," << modelBox.high().z << ")");
                }

                size_t triCount = indices.size() / 3;
                size_t emittedBefore = m_cache.size();
                size_t triVisited = 0, triInBox = 0;

                auto emitTri = [&](const G3D::Vector3& a, const G3D::Vector3& b, const G3D::Vector3& c, int localTriIndex)
                    {
                        G3D::Vector3 ia = (a * inst.iScale) * inst.iRot + inst.iPos;
                        G3D::Vector3 ib = (b * inst.iScale) * inst.iRot + inst.iPos;
                        G3D::Vector3 ic = (c * inst.iScale) * inst.iRot + inst.iPos;
                        CapsuleCollision::Triangle T; T.a = { ia.x, ia.y, ia.z }; T.b = { ib.x, ib.y, ib.z }; T.c = { ic.x, ic.y, ic.z }; T.doubleSided = true; T.collisionMask = instMask;
                        int triIndex = (int)m_cache.size();
                        m_cache.push_back(T); m_triToInstance.push_back(idx); m_triToLocalTri.push_back(localTriIndex);
                        if (count < maxCount) outIndices[count++] = triIndex;
                        PHYS_TRACE(PHYS_CYL, "Emit tri globalIdx=" << triIndex << " localTriIndex=" << localTriIndex << " instIdx=" << idx
                            << " ia=(" << ia.x << "," << ia.y << "," << ia.z << ") ib=(" << ib.x << "," << ib.y << "," << ib.z
                            << ") ic=(" << ic.x << "," << ic.y << "," << ic.z << ")");
                    };

                PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " Tri loop triCount=" << triCount << " maxCount=" << maxCount);
                for (size_t t = 0; t < triCount; ++t)
                {
                    uint32_t i0 = indices[t * 3 + 0];
                    uint32_t i1 = indices[t * 3 + 1];
                    uint32_t i2 = indices[t * 3 + 2];
                    if (i0 >= vertices.size() || i1 >= vertices.size() || i2 >= vertices.size()) { PHYS_TRACE(PHYS_CYL, "Bad tri indices t="<<t); continue; }
                    const G3D::Vector3& a = vertices[i0];
                    const G3D::Vector3& b = vertices[i1];
                    const G3D::Vector3& c = vertices[i2];

                    ++triVisited;
                    if (haveBoundsData)
                    {
                        G3D::Vector3 lo = a.min(b).min(c);
                        G3D::Vector3 hi = a.max(b).max(c);
                        G3D::AABox triBox(lo, hi);
                        bool inBox = triBox.intersects(modelBox);
                        if (!inBox) continue;
                        ++triInBox;
                    }
                    else
                    {
                        ++triInBox; // full-mesh path
                    }

                    emitTri(a, b, c, (int)t);
                    if (count >= maxCount) { PHYS_TRACE(PHYS_CYL, "Reached maxCount=" << maxCount << " breaking tri loop"); break; }
                }

                size_t emittedNow = m_cache.size() - emittedBefore;
                totalEmitted += emittedNow;
                PHYS_TRACE(PHYS_CYL, "Instance idx=" << idx << " Summary triVisited=" << triVisited << " triInBox=" << triInBox << " emitted=" << emittedNow << " totalEmitted=" << totalEmitted << " outCount=" << count);

                if (count >= maxCount) { PHYS_TRACE(PHYS_CYL, "MapMeshView::query terminating (global maxCount reached) count=" << count); break; }
            }
            PHYS_TRACE(PHYS_CYL, "MapMeshView::query EXIT totalTriangles=" << m_cache.size() << " outCount=" << count);
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

int SceneQuery::SweepCapsuleAll(const StaticMapTree& map,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    std::vector<SceneHit>& outHits,
    uint32_t includeMask,
    const QueryParams& params)
{
    // Refactored: Collect all valid contacts using continuous collision detection (GJK/SAT analytic sweep).
    // Returns a sorted list of contacts by time-of-impact, including penetration depth, contact point, normal, triangle index.
    // Handles start penetration (t=0) and all contact types (face, edge, vertex).
    outHits.clear();
    if (distance <= 0.0f)
        return 0;

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
    C0.p0 = { iP0.x, iP0.y, iP0.z }; C0.p1 = { iP1.x, iP1.y, iP1.z };
    G3D::Vector3 iDir = NavCoord::WorldDirToInternal(dir);
    CapsuleCollision::Vec3 vel(iDir.x * distance, iDir.y * distance, iDir.z * distance);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    G3D::Vector3 wP0End = wP0 + dir * distance;
    G3D::Vector3 wP1End = wP1 + dir * distance;
    G3D::Vector3 wMin = wP0.min(wP1).min(wP0End.min(wP1End)) - G3D::Vector3(C0.r, C0.r, C0.r);
    G3D::Vector3 wMax = wP0.max(wP1).max(wP0End.max(wP1End)) + G3D::Vector3(C0.r, C0.r, C0.r);
    CapsuleCollision::AABB sweepBox = AABBFromAABox(G3D::AABox(wMin, wMax));
    CapsuleCollision::aabbInflate(sweepBox, 0.005f);
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.query(sweepBox, triIdxs, triCount, kCap);

    // Collect start-penetrating overlaps (t=0)
    for (int i = 0; i < triCount; ++i)
    {
        int ti = triIdxs[i];
        const auto& T = view.tri(ti);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C0, T, ch))
        {
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
            const ModelInstance* mi = view.triInstance(ti);
            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = ti; h.instanceId = mi ? mi->ID : 0; h.startPenetrating = true; h.penetrationDepth = ch.depth;
            EnsureUpwardNormal(h.normal, h);
            outHits.push_back(h);
        }
    }
    if (!outHits.empty())
    {
        std::sort(outHits.begin(), outHits.end(), [](const SceneHit& a, const SceneHit& b) { return a.triIndex < b.triIndex; });
        return (int)outHits.size();
    }

    // Collect all sweep contacts (face, edge, vertex)
    struct HitTmp { float t; int triIdx; G3D::Vector3 nI; G3D::Vector3 pI; uint32_t instId; float penetrationDepth; };
    std::vector<HitTmp> candidates;
    for (int i = 0; i < triCount; ++i)
    {
        int selTriIdx = triIdxs[i];
        const auto& T = view.tri(selTriIdx);
        int triLocal = view.triLocalIndex(selTriIdx);
        const ModelInstance* mi = view.triInstance(selTriIdx);
        uint32_t instId = mi ? mi->ID : 0;
        float toi; CapsuleCollision::Vec3 n, p;
        bool sweepHit = CapsuleCollision::capsuleTriangleSweep(C0, vel, T, toi, n, p);
        if (sweepHit && toi >= 0.0f && toi <= 1.0f)
        {
            // Compute penetration depth at impact
            CapsuleCollision::Capsule impactCapsule = C0;
            impactCapsule.p0 = impactCapsule.p0 + vel * toi;
            impactCapsule.p1 = impactCapsule.p1 + vel * toi;
            CapsuleCollision::Hit ch;
            CapsuleCollision::intersectCapsuleTriangle(impactCapsule, T, ch);
            HitTmp tmp; tmp.t = toi; tmp.triIdx = selTriIdx; tmp.nI = { n.x, n.y, n.z }; tmp.pI = { p.x, p.y, p.z }; tmp.instId = instId; tmp.penetrationDepth = ch.depth;
            candidates.push_back(tmp);
        }
    }
    if (!candidates.empty())
    {
        std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b) { if (a.t == b.t) return a.triIdx < b.triIdx; return a.t < b.t; });
        for (const auto& cand : candidates)
        {
            SceneHit hit;
            hit.hit = true;
            hit.time = CapsuleCollision::cc_clamp(cand.t, 0.0f, 1.0f);
            hit.distance = hit.time * distance;
            hit.penetrationDepth = cand.penetrationDepth;
            G3D::Vector3 iImpact(cand.pI.x, cand.pI.y, cand.pI.z);
            G3D::Vector3 wImpact = NavCoord::InternalToWorld(iImpact);
            G3D::Vector3 iNormal(cand.nI.x, cand.nI.y, cand.nI.z);
            G3D::Vector3 wNormal = NavCoord::InternalDirToWorld(iNormal);
            hit.point = wImpact; hit.normal = wNormal; hit.triIndex = cand.triIdx; hit.instanceId = cand.instId;
            hit.startPenetrating = false;
            EnsureUpwardNormal(hit.normal, hit);
            outHits.push_back(hit);
        }
        return (int)outHits.size();
    }

    // Fallback: overlap at end position
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
