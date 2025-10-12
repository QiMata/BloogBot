#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"

using namespace VMAP;

namespace
{
    // World <-> internal space conversions (match VMapManager2::convertPositionToInternalRep)
    static inline G3D::Vector3 WorldToInternal(const G3D::Vector3& w)
    {
        constexpr float MID = 0.5f * 64.0f * 533.33333333f;
        return G3D::Vector3(MID - w.x, MID - w.y, w.z);
    }
    static inline G3D::Vector3 InternalToWorld(const G3D::Vector3& i)
    {
        constexpr float MID = 0.5f * 64.0f * 533.33333333f;
        return G3D::Vector3(MID - i.x, MID - i.y, i.z);
    }
    // Direction/normal conversion: only invert X/Y
    static inline G3D::Vector3 WorldDirToInternal(const G3D::Vector3& d)
    {
        return G3D::Vector3(-d.x, -d.y, d.z);
    }
    static inline G3D::Vector3 InternalDirToWorld(const G3D::Vector3& d)
    {
        return G3D::Vector3(-d.x, -d.y, d.z);
    }

    // Local mesh view for building triangle caches out of map tree (internal space)
    class MapMeshView : public CapsuleCollision::TriangleMeshView
    {
    public:
        MapMeshView(const BIH* tree, const ModelInstance* instances, uint32_t instanceCount)
            : m_tree(tree), m_instances(instances), m_instanceCount(instanceCount)
        {
            m_cache.reserve(1024);
        }

        void query(const CapsuleCollision::AABB& box, int* outIndices, int& count, int maxCount) const override
        {
            count = 0;
            m_cache.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
                return;

            // Build world-space AABox from input AABB and convert to internal map space
            G3D::Vector3 wLo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 wHi(box.max.x, box.max.y, box.max.z);
            G3D::Vector3 iLo = WorldToInternal(wLo);
            G3D::Vector3 iHi = WorldToInternal(wHi);
            // Handle axis flip by reordering min/max after conversion
            G3D::Vector3 qLo = iLo.min(iHi);
            G3D::Vector3 qHi = iLo.max(iHi);
            G3D::AABox queryBox(qLo, qHi);

            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            if (!m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap) || instCount == 0)
                return;

            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) continue;
                if (!inst.iBound.intersects(queryBox)) continue;

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

                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData)
                {
                    if (!inst.iModel->GetAllMeshData(vertices, indices))
                        continue;
                }

                auto emitTri = [&](const G3D::Vector3& a, const G3D::Vector3& b, const G3D::Vector3& c)
                {
                    // Transform model-space triangle to internal world space
                    G3D::Vector3 wa = (a * inst.iScale) * inst.iInvRot + inst.iPos;
                    G3D::Vector3 wb = (b * inst.iScale) * inst.iInvRot + inst.iPos;
                    G3D::Vector3 wc = (c * inst.iScale) * inst.iInvRot + inst.iPos;
                    CapsuleCollision::Triangle T;
                    T.a = { wa.x, wa.y, wa.z };
                    T.b = { wb.x, wb.y, wb.z };
                    T.c = { wc.x, wc.y, wc.z };
                    T.doubleSided = false;
                    int triIndex = (int)m_cache.size();
                    m_cache.push_back(T);
                    if (count < maxCount)
                        outIndices[count++] = triIndex;
                };

                size_t triCount = indices.size() / 3;
                for (size_t t = 0; t < triCount; ++t)
                {
                    uint32_t i0 = indices[t * 3 + 0];
                    uint32_t i1 = indices[t * 3 + 1];
                    uint32_t i2 = indices[t * 3 + 2];
                    if (i0 >= vertices.size() || i1 >= vertices.size() || i2 >= vertices.size())
                        continue;
                    const G3D::Vector3& a = vertices[i0];
                    const G3D::Vector3& b = vertices[i1];
                    const G3D::Vector3& c = vertices[i2];

                    if (!haveBoundsData)
                    {
                        G3D::Vector3 lo = a.min(b).min(c);
                        G3D::Vector3 hi = a.max(b).max(c);
                        G3D::AABox triBox(lo, hi);
                        if (!triBox.intersects(modelBox))
                            continue;
                    }

                    emitTri(a, b, c);
                    if (count >= maxCount)
                        break;
                }

                if (count >= maxCount) break;
            }
        }

        const CapsuleCollision::Triangle& tri(int idx) const override { return m_cache[idx]; }
        int triangleCount() const override { return (int)m_cache.size(); }

    private:
        const BIH* m_tree;
        const ModelInstance* m_instances;
        uint32_t m_instanceCount;
        mutable std::vector<CapsuleCollision::Triangle> m_cache;
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

bool SceneQuery::RaycastSingle(const StaticMapTree& map,
                               const G3D::Vector3& origin,
                               const G3D::Vector3& dir,
                               float maxDistance,
                               SceneHit& outHit)
{
    outHit = SceneHit();
    float dist = maxDistance;
    // Convert to internal space for map query
    G3D::Vector3 iOrigin = WorldToInternal(origin);
    G3D::Vector3 iDir = WorldDirToInternal(dir);
    G3D::Ray ray = G3D::Ray::fromOriginAndDirection(iOrigin, iDir);
    bool hitAny = map.getIntersectionTime(ray, dist, true, false);
    if (!hitAny)
        return false;

    outHit.hit = true;
    outHit.distance = dist;
    // Convert hit point back to world using original world origin/dir
    outHit.point = origin + dir * dist;
    return true;
}

int SceneQuery::RaycastAll(const StaticMapTree& map,
                           const G3D::Vector3& origin,
                           const G3D::Vector3& dir,
                           float maxDistance,
                           std::vector<SceneHit>& outHits)
{
    outHits.clear();
    SceneHit h;
    if (RaycastSingle(map, origin, dir, maxDistance, h))
    {
        outHits.push_back(h);
        return 1;
    }
    return 0;
}

int SceneQuery::OverlapCapsule(const StaticMapTree& map,
                               const CapsuleCollision::Capsule& capsule,
                               std::vector<SceneHit>& outOverlaps)
{
    outOverlaps.clear();

    // Convert capsule to internal space
    CapsuleCollision::Capsule C = capsule;
    C.p0 = { WorldToInternal(G3D::Vector3(capsule.p0.x, capsule.p0.y, capsule.p0.z)).x,
             WorldToInternal(G3D::Vector3(capsule.p0.x, capsule.p0.y, capsule.p0.z)).y,
             WorldToInternal(G3D::Vector3(capsule.p0.x, capsule.p0.y, capsule.p0.z)).z };
    C.p1 = { WorldToInternal(G3D::Vector3(capsule.p1.x, capsule.p1.y, capsule.p1.z)).x,
             WorldToInternal(G3D::Vector3(capsule.p1.x, capsule.p1.y, capsule.p1.z)).y,
             WorldToInternal(G3D::Vector3(capsule.p1.x, capsule.p1.y, capsule.p1.z)).z };

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());
    int indices[512]; int count = 0;
    view.query(CapsuleCollision::aabbFromCapsule(C), indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        const auto& T = view.tri(indices[i]);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C, T, ch))
        {
            // Convert contact back to world
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = InternalToWorld(iP);

            SceneHit h; h.hit = true; h.distance = ch.depth; // use depth for overlap
            h.normal = wN; h.point = wP; h.triIndex = ch.triIndex;
            outOverlaps.push_back(h);
        }
    }

    return (int)outOverlaps.size();
}

int SceneQuery::OverlapSphere(const StaticMapTree& map,
                              const G3D::Vector3& center,
                              float radius,
                              std::vector<SceneHit>& outOverlaps)
{
    outOverlaps.clear();

    // Represent as tiny capsule with zero length for reuse
    CapsuleCollision::Capsule C;
    G3D::Vector3 iC = WorldToInternal(center);
    C.p0 = { iC.x, iC.y, iC.z };
    C.p1 = { iC.x, iC.y, iC.z };
    C.r = radius;

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());
    int indices[512]; int count = 0;
    view.query(CapsuleCollision::aabbFromCapsule(C), indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        const auto& T = view.tri(indices[i]);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectSphereTriangle(C.p0, C.r, T, ch))
        {
            // Convert contact back to world
            G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
            G3D::Vector3 wN = InternalDirToWorld(iN);
            G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
            G3D::Vector3 wP = InternalToWorld(iP);

            SceneHit h; h.hit = true; h.distance = ch.depth; h.normal = wN;
            h.point = wP; h.triIndex = ch.triIndex; outOverlaps.push_back(h);
        }
    }

    return (int)outOverlaps.size();
}

int SceneQuery::OverlapBox(const StaticMapTree& map,
                           const G3D::AABox& box,
                           std::vector<SceneHit>& outOverlaps)
{
    outOverlaps.clear();

    // Approximate by testing sphere at center with radius = half-diagonal projected to XY (~broad check only)
    G3D::Vector3 lo = box.low(), hi = box.high();
    G3D::Vector3 c = (lo + hi) * 0.5f;
    G3D::Vector3 ext = (hi - lo) * 0.5f;
    float r = std::sqrt(ext.x * ext.x + ext.y * ext.y + ext.z * ext.z);
    return OverlapSphere(map, c, r, outOverlaps);
}

bool SceneQuery::SweepCapsuleSingle(const StaticMapTree& map,
                                    const CapsuleCollision::Capsule& capsuleStart,
                                    const G3D::Vector3& dir,
                                    float distance,
                                    SceneHit& outHit)
{
    outHit = SceneHit();

    // Convert capsule and sweep vector to internal space
    CapsuleCollision::Capsule C = capsuleStart;
    G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
    G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);
    G3D::Vector3 iP0 = WorldToInternal(wP0);
    G3D::Vector3 iP1 = WorldToInternal(wP1);
    C.p0 = { iP0.x, iP0.y, iP0.z };
    C.p1 = { iP1.x, iP1.y, iP1.z };

    G3D::Vector3 iDir = WorldDirToInternal(dir);
    CapsuleCollision::Vec3 v(iDir.x * distance, iDir.y * distance, iDir.z * distance);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());
    CapsuleCollision::ResolveConfig cfg; // defaults
    bool collided = CapsuleCollision::moveCapsuleWithCCD(C, v, view, cfg, 1);
    if (!collided)
        return false;

    float traveled = std::sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    outHit.hit = true;
    outHit.distance = distance - traveled; // TOI approximation in world units

    // Fetch contact info via discrete overlap in internal space, then convert back to world
    int idx[256]; int cnt = 0;
    view.query(CapsuleCollision::aabbFromCapsule(C), idx, cnt, 256);
    float best = -1.0f;
    for (int i = 0; i < cnt; ++i)
    {
        const auto& T = view.tri(idx[i]);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(C, T, ch))
        {
            if (ch.depth > best)
            {
                best = ch.depth;
                G3D::Vector3 iN(ch.normal.x, ch.normal.y, ch.normal.z);
                G3D::Vector3 wN = InternalDirToWorld(iN);
                G3D::Vector3 iP(ch.point.x, ch.point.y, ch.point.z);
                G3D::Vector3 wP = InternalToWorld(iP);
                outHit.normal = wN;
                outHit.point = wP;
                outHit.triIndex = ch.triIndex;
            }
        }
    }

    return true;
}

int SceneQuery::SweepCapsuleAll(const StaticMapTree& map,
                                const CapsuleCollision::Capsule& capsuleStart,
                                const G3D::Vector3& dir,
                                float distance,
                                std::vector<SceneHit>& outHits)
{
    outHits.clear();

    SceneHit h;
    if (SweepCapsuleSingle(map, capsuleStart, dir, distance, h))
    {
        outHits.push_back(h);
        return 1;
    }
    return 0;
}
