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
    // Debug: allow targeting a single instance ID via env var VMAP_DEBUG_INSTANCE
    static uint32_t g_meshview_debug_instance = 0;
    static float g_meshview_debug_hit_x = 0.0f;
    static float g_meshview_debug_hit_y = 0.0f;
    static float g_meshview_debug_hit_z = 0.0f;
    static bool g_meshview_have_hit = false;
    static std::once_flag g_meshview_debug_init;
    static void InitMeshViewDebug()
    {
        const char* ev = std::getenv("VMAP_DEBUG_INSTANCE");
        if (ev)
        {
            char* end = nullptr;
            unsigned long v = std::strtoul(ev, &end, 10);
            if (end != ev)
                g_meshview_debug_instance = static_cast<uint32_t>(v);
        }
        const char* hit = std::getenv("VMAP_DEBUG_HIT"); // format: x,y,z
        if (hit)
        {
            float hx=0, hy=0, hz=0;
            if (sscanf(hit, "%f,%f,%f", &hx, &hy, &hz) == 3)
            {
                g_meshview_debug_hit_x = hx; g_meshview_debug_hit_y = hy; g_meshview_debug_hit_z = hz; g_meshview_have_hit = true;
            }
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
            // Ensure debug init runs once
            std::call_once(g_meshview_debug_init, InitMeshViewDebug);

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

            G3D::Vector3 iLo = NavCoord::WorldToInternal(wLo);
            G3D::Vector3 iHi = NavCoord::WorldToInternal(wHi);
            G3D::Vector3 qLo = iLo.min(iHi); // reorder after conversion
            G3D::Vector3 qHi = iLo.max(iHi);
            // Inflate more along Y to catch near-miss instances (diagnostic)
            G3D::Vector3 qInflate(0.08f, 6.0f, 0.08f);
            G3D::AABox queryBox(qLo - qInflate, qHi + qInflate);

            PHYS_TRACE(PHYS_CYL, "[MeshView.query] AABB worldLo=(" << wLo.x << "," << wLo.y << "," << wLo.z
                << ") worldHi=(" << wHi.x << "," << wHi.y << "," << wHi.z
                << ") intLo=(" << queryBox.low().x << "," << queryBox.low().y << "," << queryBox.low().z
                << ") intHi=(" << queryBox.high().x << "," << queryBox.high().y << "," << queryBox.high().z
                << ") includeMask=0x" << std::hex << m_includeMask << std::dec
                << " qInfl=(0.08,6.0,0.08) mInfl(Y)~6.0)");

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            if (!m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap))
            {
                PHYS_TRACE(PHYS_CYL, "[MeshView.query] BIH returned 0 instances");
                // Diagnostic for 226014 as before
                for (uint32_t i = 0; i < m_instanceCount; ++i)
                {
                    if (m_instances[i].ID == 226014)
                    {
                        bool ib = m_instances[i].iBound.intersects(queryBox);
                        G3D::Vector3 qbLo = queryBox.low(); G3D::Vector3 qbHi = queryBox.high();
                        G3D::Vector3 bLo = m_instances[i].iBound.low(); G3D::Vector3 bHi = m_instances[i].iBound.high();
                        PHYS_TRACE(PHYS_CYL, "[MeshView.query][DBG] inst226014 presentInBIH=0 intersects="<<(ib?1:0)
                            <<" boundLo=("<<bLo.x<<","<<bLo.y<<","<<bLo.z<<") boundHi=("<<bHi.x<<","<<bHi.y<<","<<bHi.z
                            <<") qLo=("<<qbLo.x<<","<<qbLo.y<<","<<qbLo.z<<") qHi=("<<qbHi.x<<","<<qbHi.y<<","<<qbHi.z<<")");
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
                if (added > 0)
                {
                    PHYS_TRACE(PHYS_CYL, "[MeshView.query][Fallback] addedIntersectingInstances=" << added << " totalInstances=" << instCount);
                }
            }

            PHYS_TRACE(PHYS_CYL, "[MeshView.query] BIH candidates instances=" << instCount);
            // Diagnostic for 226014
            {
                int idx226014 = -1; bool inList = false;
                for (uint32_t i = 0; i < m_instanceCount; ++i) { if (m_instances[i].ID == 226014) { idx226014 = (int)i; break; } }
                if (idx226014 >= 0)
                {
                    for (uint32_t i = 0; i < instCount; ++i) if (instIdx[i] == (uint32_t)idx226014) { inList = true; break; }
                    bool ib = m_instances[idx226014].iBound.intersects(queryBox);
                    G3D::Vector3 qbLo = queryBox.low(); G3D::Vector3 qbHi = queryBox.high();
                    G3D::Vector3 bLo = m_instances[idx226014].iBound.low(); G3D::Vector3 bHi = m_instances[idx226014].iBound.high();
                    PHYS_TRACE(PHYS_CYL, "[MeshView.query][DBG] inst226014 inList="<<(inList?1:0)<<" boundIntersects="<<(ib?1:0)
                        <<" boundLo=("<<bLo.x<<","<<bLo.y<<","<<bLo.z<<") boundHi=("<<bHi.x<<","<<bHi.y<<","<<bHi.z
                        <<") qLo=("<<qbLo.x<<","<<qbLo.y<<","<<qbLo.z<<") qHi=("<<qbHi.x<<","<<qbHi.y<<","<<qbHi.z<<")");
                }
            }

            size_t totalEmitted = 0;
            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) { PHYS_TRACE(PHYS_CYL, "[MeshView.query] skip inst id="<<inst.ID<<" no model name='"<<inst.name<<"'"); continue; }

                // If we're debugging this instance, precompute modelBox (model-space) -> used for debug comparison
                G3D::AABox modelBoxMS(G3D::Vector3(0,0,0), G3D::Vector3(0,0,0));
                bool haveModelBoxMS = false;
                if (g_meshview_debug_instance != 0 && inst.ID == g_meshview_debug_instance)
                {
                    G3D::Vector3 wLoI = queryBox.low();
                    G3D::Vector3 wHiI = queryBox.high();
                    G3D::Vector3 cornersDbg[8] = {
                        {wLoI.x, wLoI.y, wLoI.z}, {wHiI.x, wLoI.y, wLoI.z}, {wLoI.x, wHiI.y, wLoI.z}, {wHiI.x, wHiI.y, wLoI.z},
                        {wLoI.x, wLoI.y, wHiI.z}, {wHiI.x, wLoI.y, wHiI.z}, {wLoI.x, wHiI.y, wHiI.z}, {wHiI.x, wHiI.y, wHiI.z}
                    };
                    G3D::Vector3 c0Dbg = inst.iInvRot * ((cornersDbg[0] - inst.iPos) * inst.iInvScale);
                    modelBoxMS = G3D::AABox(c0Dbg, c0Dbg);
                    for (int ci = 1; ci < 8; ++ci)
                    {
                        modelBoxMS.merge(inst.iInvRot * ((cornersDbg[ci] - inst.iPos) * inst.iInvScale));
                    }
                    G3D::Vector3 mInflateDbg(0.08f, 6.0f, 0.08f);
                    modelBoxMS = G3D::AABox(modelBoxMS.low() - mInflateDbg, modelBoxMS.high() + mInflateDbg);
                    haveModelBoxMS = true;
                }

                if (!inst.iBound.intersects(queryBox)) {
                    // Only emit the noisy bound_no_intersect trace when the instance matches the debug id configured in VMAP_DEBUG_INSTANCE
                    if (g_meshview_debug_instance != 0 && inst.ID == g_meshview_debug_instance)
                    {
                        G3D::Vector3 qbLo = queryBox.low(); G3D::Vector3 qbHi = queryBox.high();
                        G3D::Vector3 bLo = inst.iBound.low(); G3D::Vector3 bHi = inst.iBound.high();
                        PHYS_TRACE(PHYS_CYL, "[MeshView.query] skip inst id="<<inst.ID<<" name='"<<inst.name<<"' boundLo=("<<bLo.x<<","<<bLo.y<<","<<bLo.z<<") boundHi=("<<bHi.x<<","<<bHi.y<<","<<bHi.z<<") qLo=("<<qbLo.x<<","<<qbLo.y<<","<<qbLo.z<<") qHi=("<<qbHi.x<<","<<qbHi.y<<","<<qbHi.z<<") reason=bound_no_intersect");

                        if (haveModelBoxMS)
                        {
                            G3D::Vector3 mLo = modelBoxMS.low();
                            G3D::Vector3 mHi = modelBoxMS.high();
                            G3D::Vector3 mLoI = (mLo * inst.iScale) * inst.iRot + inst.iPos;
                            G3D::Vector3 mHiI = (mHi * inst.iScale) * inst.iRot + inst.iPos;
                            G3D::Vector3 mLoW = NavCoord::InternalToWorld(mLoI);
                            G3D::Vector3 mHiW = NavCoord::InternalToWorld(mHiI);

                            // wLo and wHi are available in this scope as the world-space query box
                            PHYS_TRACE(PHYS_CYL, "[MeshView.debugCompare] hitW=(" << (g_meshview_have_hit?g_meshview_debug_hit_x:0.0f) << "," << (g_meshview_have_hit?g_meshview_debug_hit_y:0.0f) << "," << (g_meshview_have_hit?g_meshview_debug_hit_z:0.0f) << ")"
                                << " qWLo=(" << wLo.x << "," << wLo.y << "," << wLo.z << ") qWHi=(" << wHi.x << "," << wHi.y << "," << wHi.z << ")"
                                << " modelBoxWLo=(" << mLoW.x << "," << mLoW.y << "," << mLoW.z << ") modelBoxWHi=(" << mHiW.x << "," << mHiW.y << "," << mHiW.z << ")");
                        }
                    }
                    continue; }

                uint32_t instMask = inst.GetCollisionMask();
                if ((instMask & m_includeMask) == 0)
                {
                    PHYS_TRACE(PHYS_CYL, "[MeshView.query] inst id=" << inst.ID << " name='" << inst.name << "' masked-out includeMask=0x" << std::hex << m_includeMask << std::dec << ", instMask=0x" << std::hex << instMask << std::dec);
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
                // Inflate model-space box (larger Y)
                G3D::Vector3 mInflate(0.08f, 6.0f, 0.08f);
                modelBox = G3D::AABox(modelBox.low() - mInflate, modelBox.high() + mInflate);

                // Log instance bounds and modelBox
                {
                    G3D::Vector3 bLo = inst.iBound.low();
                    G3D::Vector3 bHi = inst.iBound.high();
                    PHYS_TRACE_DEEP(PHYS_CYL, "  instBoundsI lo=(" << bLo.x << "," << bLo.y << "," << bLo.z << ") hi=(" << bHi.x << "," << bHi.y << "," << bHi.z << ") modelBoxMS lo=(" << modelBox.low().x << "," << modelBox.low().y << "," << modelBox.low().z << ") hi=(" << modelBox.high().x << "," << modelBox.high().y << "," << modelBox.high().z << ")");
                }

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
                size_t totalEmitted = 0;
                PHYS_TRACE(PHYS_CYL, "[MeshView.query] instance id=" << inst.ID << " name='" << inst.name << "' haveBoundsData=" << (haveBoundsData?1:0)
                    << " triCount=" << triCount << " visited=" << triVisited << " inBoxOrSkipped=" << triInBox << " emittedTris=" << emittedNow);

                // Sample first few triangle world AABBs for diagnostic (instance 226014 only)
                if (inst.ID == 226014 && emittedNow > 0)
                {
                    int samples = 0;
                    for (size_t triIdx = emittedBefore; triIdx < m_cache.size() && samples < 8; ++triIdx, ++samples)
                    {
                        const auto& TT = m_cache[triIdx];
                        // Convert internal tri verts back to world for logging
                        G3D::Vector3 wA = NavCoord::InternalToWorld(G3D::Vector3(TT.a.x, TT.a.y, TT.a.z));
                        G3D::Vector3 wB = NavCoord::InternalToWorld(G3D::Vector3(TT.b.x, TT.b.y, TT.b.z));
                        G3D::Vector3 wC = NavCoord::InternalToWorld(G3D::Vector3(TT.c.x, TT.c.y, TT.c.z));
                        G3D::Vector3 tLo = wA.min(wB).min(wC);
                        G3D::Vector3 tHi = wA.max(wB).max(wC);
                        PHYS_TRACE_DEEP(PHYS_CYL, "  triSample idx="<<triIdx<<" worldLo=("<<tLo.x<<","<<tLo.y<<","<<tLo.z<<") worldHi=("<<tHi.x<<","<<tHi.y<<","<<tHi.z<<")");
                    }
                }

                if (count >= maxCount) break;
            }

            PHYS_TRACE(PHYS_CYL, "[MeshView.query] totalTrianglesEmitted=" << totalEmitted << " returnedIndices=" << count);
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
    PHYS_TRACE(PHYS_CYL, "  triSurface triLocal=" << triLocalIdx
        << " triN=(" << wN.x << "," << wN.y << "," << wN.z << ")"
        << " centroidW=(" << cent.x << "," << cent.y << "," << cent.z << ")"
        << " v0W=(" << va.x << "," << va.y << "," << va.z << ")"
        << " v1W=(" << vb.x << "," << vb.y << "," << vb.z << ")"
        << " v2W=(" << vc.x << "," << vc.y << "," << vc.z << ")");
}

bool SceneQuery::RaycastSingle(const StaticMapTree& map,
                               const G3D::Vector3& origin,
                               const G3D::Vector3& dir,
                               float maxDistance,
                               SceneHit& outHit,
                               const QueryParams& params)
{
    outHit = SceneHit();
    float dist = maxDistance;
    G3D::Vector3 iOrigin = NavCoord::WorldToInternal(origin);
    G3D::Vector3 iDir = NavCoord::WorldDirToInternal(dir);
    G3D::Ray ray = G3D::Ray::fromOriginAndDirection(iOrigin, iDir);
    bool hitAny = map.getIntersectionTime(ray, dist, true, false);
    if (!hitAny)
        return false;

    outHit.hit = true;
    outHit.distance = dist;
    outHit.time = (maxDistance > 0.0f ? CapsuleCollision::cc_clamp(dist / maxDistance, 0.0f, 1.0f) : 0.0f);
    outHit.point = origin + dir * dist;
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

    PHYS_TRACE(PHYS_CYL, "[OverlapCapsule] capsuleW p0=("<<wp0.x<<","<<wp0.y<<","<<wp0.z<<") p1=("<<wp1.x<<","<<wp1.y<<","<<wp1.z<<") r="<<C.r
        <<" capsuleI p0=("<<ip0.x<<","<<ip0.y<<","<<ip0.z<<") p1=("<<ip1.x<<","<<ip1.y<<","<<ip1.z<<")");

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);

    G3D::Vector3 wLo = wp0.min(wp1) - G3D::Vector3(C.r, C.r, C.r);
    G3D::Vector3 wHi = wp0.max(wp1) + G3D::Vector3(C.r, C.r, C.r);
    // Standardize inflation like sweeps
    const float wInfl = 0.005f;
    wLo = wLo - G3D::Vector3(wInfl, wInfl, wInfl);
    wHi = wHi + G3D::Vector3(wInfl, wInfl, wInfl);
    CapsuleCollision::AABB worldBox = AABBFromAABox(G3D::AABox(wLo, wHi));

    PHYS_TRACE(PHYS_CYL, "[OverlapCapsule] AABB wLo=(" << wLo.x << "," << wLo.y << "," << wLo.z
        << ") wHi=(" << wHi.x << "," << wHi.y << "," << wHi.z << ") r=" << C.r);

    int indices[512]; int count = 0;
    view.query(worldBox, indices, count, 512);
    PHYS_TRACE(PHYS_CYL, "[OverlapCapsule] BIH candidates triangles=" << count);

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
            PHYS_TRACE(PHYS_CYL, "  contact depth=" << ch.depth
                << " pointW=(" << wP.x << "," << wP.y << "," << wP.z << ")"
                << " normalW=(" << wN.x << "," << wN.y << "," << wN.z << ")"
                << " part=" << part);

            const ModelInstance* mi = view.triInstance(triIdx);
            if (mi)
            {
                G3D::Vector3 wPos = NavCoord::InternalToWorld(mi->iPos);
                G3D::Vector3 rotDeg = mi->ModelSpawn::iRot;
                G3D::Vector3 ip0v(C.p0.x, C.p0.y, C.p0.z);
                G3D::Vector3 ip1v(C.p1.x, C.p1.y, C.p1.z);
                G3D::Vector3 localP0 = mi->iInvRot * ((ip0v - mi->iPos) * mi->iInvScale);
                G3D::Vector3 localP1 = mi->iInvRot * ((ip1v - mi->iPos) * mi->iInvScale);
                PHYS_TRACE(PHYS_CYL, "OverlapCapsule hit model='" << mi->name << "' id=" << mi->ID
                    << " adt=" << mi->adtId << " part=" << part
                    << " triLocal=" << view.triLocalIndex(triIdx)
                    << " posW=(" << wPos.x << "," << wPos.y << "," << wPos.z << ")"
                    << " rotEulerDeg=(" << rotDeg.x << "," << rotDeg.y << "," << rotDeg.z << ")"
                    << " scale=" << mi->iScale
                    << " capsuleLocal.p0=(" << localP0.x << "," << localP0.y << "," << localP0.z << ")"
                    << " p1=(" << localP1.x << "," << localP1.y << "," << localP1.z << ")");
            }

            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f;
            h.normal = wN; h.point = wP; h.triIndex = ch.triIndex; h.instanceId = mi ? mi->ID : 0;
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
    PHYS_TRACE(PHYS_CYL, "[OverlapSphere] enter centerW=(" << center.x << "," << center.y << "," << center.z << ") centerI=(" << iCenter.x << "," << iCenter.y << "," << iCenter.z << ") r=" << radius
        << " wLo=(" << wLo.x << "," << wLo.y << "," << wLo.z << ") wHi=(" << wHi.x << "," << wHi.y << "," << wHi.z << ") mask=0x" << std::hex << includeMask << std::dec);

    CapsuleCollision::Capsule C; C.p0 = { iCenter.x, iCenter.y, iCenter.z }; C.p1 = { iCenter.x, iCenter.y, iCenter.z }; C.r = radius;

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount(), includeMask);
    int indices[512]; int count = 0;
    view.query(worldBox, indices, count, 512);
    PHYS_TRACE(PHYS_CYL, "[OverlapSphere] BIH candidates=" << count);

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
            PHYS_TRACE(PHYS_CYL, "  contact depth=" << ch.depth
                << " pointW=(" << wP.x << "," << wP.y << "," << wP.z << ")"
                << " normalW=(" << wN.x << "," << wN.y << "," << wN.z << ")");
            const ModelInstance* mi = view.triInstance(triIdx);
            if (mi)
            {
                G3D::Vector3 wPos = NavCoord::InternalToWorld(mi->iPos);
                G3D::Vector3 rotDeg = mi->ModelSpawn::iRot;
                G3D::Vector3 localC = mi->iInvRot * ((G3D::Vector3(C.p0.x, C.p0.y, C.p0.z) - mi->iPos) * mi->iInvScale);
                PHYS_TRACE(PHYS_CYL, "OverlapSphere hit model='" << mi->name << "' id=" << mi->ID
                    << " adt=" << mi->adtId << " triLocal=" << view.triLocalIndex(triIdx)
                    << " posW=(" << wPos.x << "," << wPos.y << "," << wPos.z << ")"
                    << " rotEulerDeg=(" << rotDeg.x << "," << rotDeg.y << "," << rotDeg.z << ")"
                    << " scale=" << mi->iScale << " sphereLocal.center=(" << localC.x << "," << localC.y << "," << localC.z << ") r=" << (C.r * mi->iInvScale) << ")");
            }
            SceneHit h; h.hit = true; h.distance = ch.depth; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = ch.triIndex; h.instanceId = mi ? mi->ID : 0; outOverlaps.push_back(h);
        }
    }

    PHYS_TRACE(PHYS_CYL, "[OverlapSphere] overlaps=" << outOverlaps.size());
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
    PHYS_TRACE(PHYS_CYL, "[SweepCapsuleAll] sweepAABB wMin=(" << wMin.x << "," << wMin.y << "," << wMin.z
        << ") wMax=(" << wMax.x << "," << wMax.y << "," << wMax.z << ") r=" << C0.r << " triCandidates=" << triCount);
    if (triCount <= 0)
        return 0;

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

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = wN; h.point = wP; h.triIndex = ti; h.instanceId = mi ? mi->ID : 0; h.startPenetrating = true;
            startHits.push_back(h);
        }
    }
    if (!startHits.empty())
    {
        // Sort deterministically by tri index for stability
        std::sort(startHits.begin(), startHits.end(), [](const SceneHit& a, const SceneHit& b){ return a.triIndex < b.triIndex; });
        outHits = startHits;
        return (int)outHits.size();
    }

    // Sweep per triangle and collect candidates
    struct HitTmp { float t; int triIdx; G3D::Vector3 nI; G3D::Vector3 pI; uint32_t instId; };
    std::vector<HitTmp> candidates; candidates.reserve(triCount);

    for (int i = 0; i < triCount; ++i)
    {
        int ti = triIdxs[i];
        const auto& T = view.tri(ti);
        float toi; CapsuleCollision::Vec3 n, p;
        if (CapsuleCollision::capsuleTriangleSweep(C0, vel, T, toi, n, p))
        {
            if (toi >= 0.0f && toi <= 1.0f)
            {
                // Log detailed triangle info for diagnostics
                LogTriangleSurfaceInfo(T, view.triLocalIndex(ti));
                // Convert internal impact point/normal to world for logging
                G3D::Vector3 iP(p.x, p.y, p.z);
                G3D::Vector3 wP = NavCoord::InternalToWorld(iP);
                G3D::Vector3 iN(n.x, n.y, n.z);
                G3D::Vector3 wN = NavCoord::InternalDirToWorld(iN);
                const ModelInstance* miLog = view.triInstance(ti);
                PHYS_TRACE(PHYS_CYL, "[SweepCapsuleAll] candidate triIdx=" << ti
                    << " triLocal=" << view.triLocalIndex(ti)
                    << " toi=" << toi
                    << " pointW=(" << wP.x << "," << wP.y << "," << wP.z << ")"
                    << " normalW=(" << wN.x << "," << wN.y << "," << wN.z << ")"
                    << " instId=" << (miLog?miLog->ID:0)
                    << " name='" << (miLog?miLog->name:"(none)") << "'");

                HitTmp tmp; tmp.t = toi; tmp.triIdx = ti; tmp.nI = { n.x, n.y, n.z }; tmp.pI = { p.x, p.y, p.z };
                const ModelInstance* mi = view.triInstance(ti); tmp.instId = mi ? mi->ID : 0;
                candidates.push_back(tmp);
            }
        }
    }

    if (candidates.empty())
        return 0;

    // Sort by time (earliest first). Tie-break by triangle index for deterministic order.
    std::sort(candidates.begin(), candidates.end(), [](const HitTmp& a, const HitTmp& b){ if (a.t == b.t) return a.triIdx < b.triIdx; return a.t < b.t; });

    // Convert all candidates into SceneHit entries (return all hits along the sweep)
    for (const auto& c : candidates)
    {
        SceneHit h; h.hit = true; h.time = CapsuleCollision::cc_clamp(c.t, 0.0f, 1.0f); h.distance = h.time * distance;
        // Convert normal and point to world
        G3D::Vector3 wN = NavCoord::InternalDirToWorld(c.nI);
        G3D::Vector3 wP = NavCoord::InternalToWorld(c.pI);
        h.normal = wN; h.point = wP; h.triIndex = c.triIdx; h.instanceId = c.instId; h.startPenetrating = false;
        outHits.push_back(h);
    }

    return (int)outHits.size();
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