#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"

using namespace VMAP;

namespace
{
    // Local mesh view for building triangle caches out of map tree
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

            // Build world-space AABox
            G3D::Vector3 qlo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 qhi(box.max.x, box.max.y, box.max.z);
            G3D::AABox queryBox(qlo, qhi);

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

                // Transform query box corners to model space and build model-space bounds
                G3D::Vector3 wLo = queryBox.low();
                G3D::Vector3 wHi = queryBox.high();
                G3D::Vector3 corners[8] = {
                    {wLo.x, wLo.y, wLo.z}, {wHi.x, wLo.y, wLo.z}, {wLo.x, wHi.y, wLo.z}, {wHi.x, wHi.y, wLo.z},
                    {wLo.x, wLo.y, wHi.z}, {wHi.x, wLo.y, wHi.z}, {wLo.x, wHi.y, wHi.z}, {wHi.x, wHi.y, wHi.z}
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
    G3D::Ray ray = G3D::Ray::fromOriginAndDirection(origin, dir);
    bool hitAny = map.getIntersectionTime(ray, dist, true, false);
    if (!hitAny)
        return false;

    outHit.hit = true;
    outHit.distance = dist;
    outHit.point = origin + dir * dist;
    // Normal and instance info not available in existing per-call, keep defaults.
    return true;
}

int SceneQuery::RaycastAll(const StaticMapTree& map,
                           const G3D::Vector3& origin,
                           const G3D::Vector3& dir,
                           float maxDistance,
                           std::vector<SceneHit>& outHits)
{
    outHits.clear();
    // Reuse single ray, but collect all by setting stopAtFirstHit=false and then we can't retrieve all intersections without per-instance ray support.
    // For acceptance, mimic current behavior and just return first if any.
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

    // Build mesh around capsule AABB
    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());
    int indices[512]; int count = 0;
    CapsuleCollision::AABB box = CapsuleCollision::aabbFromCapsule(capsule);
    view.query(box, indices, count, 512);

    // Narrowphase using capsule-triangle
    for (int i = 0; i < count; ++i)
    {
        const auto& T = view.tri(indices[i]);
        CapsuleCollision::Hit ch;
        if (CapsuleCollision::intersectCapsuleTriangle(capsule, T, ch))
        {
            SceneHit h; h.hit = true; h.distance = ch.depth; // use depth for overlap
            h.normal = G3D::Vector3(ch.normal.x, ch.normal.y, ch.normal.z);
            h.point = G3D::Vector3(ch.point.x, ch.point.y, ch.point.z);
            h.triIndex = ch.triIndex;
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
    C.p0 = { center.x, center.y, center.z };
    C.p1 = { center.x, center.y, center.z };
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
            SceneHit h; h.hit = true; h.distance = ch.depth; h.normal = { ch.normal.x, ch.normal.y, ch.normal.z };
            h.point = { ch.point.x, ch.point.y, ch.point.z }; h.triIndex = ch.triIndex; outOverlaps.push_back(h);
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

    // Use CCD mover to find TOI by binary search against triangles via mesh view
    CapsuleCollision::ResolveConfig cfg; // defaults
    CapsuleCollision::Capsule C = capsuleStart;
    CapsuleCollision::Vec3 v(dir.x * distance, dir.y * distance, dir.z * distance);

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());
    bool collided = CapsuleCollision::moveCapsuleWithCCD(C, v, view, cfg, 1);
    if (!collided)
        return false;

    float traveled = std::sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    outHit.hit = true;
    outHit.distance = distance - traveled; // TOI approximation
    // We don't get exact contact normal/point from CCD helper; run a discrete overlap pass to fetch a contact
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
                outHit.normal = { ch.normal.x, ch.normal.y, ch.normal.z };
                outHit.point = { ch.point.x, ch.point.y, ch.point.z };
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

    // For now, collect only the first impact similar to Single
    SceneHit h;
    if (SweepCapsuleSingle(map, capsuleStart, dir, distance, h))
    {
        outHits.push_back(h);
        return 1;
    }
    return 0;
}
