#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "BIH.h"
#include "CoordinateTransforms.h"
#include "VMapLog.h"
#include "MapLoader.h"
#include "VMapManager2.h"
#include "VMapFactory.h"
#include <filesystem>
#include "PhysicsEngine.h"
#include <algorithm>
#include <cmath>
#include <vector>
#include <cstdlib>
#include <mutex>
#include <sstream>
#include <unordered_map>
#include <set>
#include "PhysicsDiagnosticsHelpers.h"
#include "VMapDefinitions.h"
#include "PhysicsLiquidHelpers.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsTolerances.h"

SceneQuery::SweepResults SceneQuery::ComputeCapsuleSweep(
    uint32_t mapId,
    float x,
    float y,
    float z,
    float r,
    float h,
    const G3D::Vector3& moveDir,
    float intendedDist)
{
    using namespace PhysicsDiag;
    SceneQuery::SweepResults diag{};

    // Build diagnostic capsule using helper (full height from feet)
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(x, y, z, r, h);
    
    // Input magnitude check (environmental: we don't alter behavior for idle here)
    const bool noInput = intendedDist <= 0.0f || moveDir.magnitude() <= 1e-6f;
    std::vector<SceneHit> combinedHits; // unified VMAP+ADT via SceneQuery
    if (!noInput && intendedDist > 0.0f)
    {
        // Use moveDir as the diagnostic forward when orientation is not available in this context
        G3D::Vector3 playerForward = G3D::Vector3(moveDir.x, moveDir.y, 0.0f).directionOrZero();
        SweepCapsule(mapId, cap, moveDir, intendedDist, combinedHits, playerForward);
    }

    // Populate per-source counts (VMAP-only legacy fields now represent combined results)
    {
        diag.vmapHitCount = combinedHits.size();
        size_t vPen = 0, vNonPen = 0, vWalkNP = 0; float vEarliestNP = FLT_MAX;
        float vMinZ = FLT_MAX, vMaxZ = -FLT_MAX; std::set<uint32_t> vInst;
        for (const auto& hHit : combinedHits) {
            if (hHit.startPenetrating) vPen++; else { vNonPen++; vEarliestNP = std::min(vEarliestNP, hHit.distance); if (std::fabs(hHit.normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) vWalkNP++; }
            vMinZ = std::min(vMinZ, hHit.point.z); vMaxZ = std::max(vMaxZ, hHit.point.z); vInst.insert(hHit.instanceId);
        }
        diag.vmapPenCount = vPen; diag.vmapNonPenCount = vNonPen; diag.vmapWalkableNonPen = vWalkNP;
        diag.vmapEarliestNonPen = (vEarliestNP == FLT_MAX) ? -1.0f : vEarliestNP;
        if (diag.vmapHitCount == 0) { vMinZ = 0.0f; vMaxZ = 0.0f; }
        diag.vmapHitMinZ = vMinZ; diag.vmapHitMaxZ = vMaxZ; diag.vmapUniqueInstanceCount = vInst.size();
    }

    // Normalize TOI when moving and sort
    if (!noInput && intendedDist > 0.0f) {
        for (auto& hHit : combinedHits) {
            float toi = (hHit.distance <= 0.0f) ? 0.0f : (hHit.distance / intendedDist);
            if (toi < 0.0f) toi = 0.0f; else if (toi > 1.0f) toi = 1.0f;
            hHit.time = toi;
        }
        std::stable_sort(combinedHits.begin(), combinedHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (a.startPenetrating != b.startPenetrating)
                return a.startPenetrating > b.startPenetrating;
            return a.time < b.time;
        });
    } else if (noInput) {
        std::stable_sort(combinedHits.begin(), combinedHits.end(), [](const SceneHit& a, const SceneHit& b) {
            if (std::fabs(a.point.z - b.point.z) > 1e-4f) return a.point.z > b.point.z;
            return a.triIndex < b.triIndex;
        });
    }

    // Liquid diagnostics at start/end of sweep (do not alter behavior based on liquid)
    {
        auto liqStart = EvaluateLiquidAt(mapId, x, y, z);
        diag.liquidStartHasLevel = liqStart.hasLevel;
        diag.liquidStartLevel = liqStart.level;
        diag.liquidStartType = liqStart.type;
        diag.liquidStartFromVmap = liqStart.fromVmap;
        diag.liquidStartSwimming = liqStart.isSwimming;

        auto liqEnd = noInput ? liqStart : EvaluateLiquidAt(mapId, x + moveDir.x * intendedDist, y + moveDir.y * intendedDist, z);
        diag.liquidEndHasLevel = liqEnd.hasLevel;
        diag.liquidEndLevel = liqEnd.level;
        diag.liquidEndType = liqEnd.type;
        diag.liquidEndFromVmap = liqEnd.fromVmap;
        diag.liquidEndSwimming = liqEnd.isSwimming;

        // Source-specific
        if (liqStart.fromVmap) {
            diag.vmapLiquidStartHasLevel = liqStart.hasLevel;
            diag.vmapLiquidStartLevel = liqStart.level;
            diag.vmapLiquidStartType = liqStart.type;
            diag.vmapLiquidStartSwimming = liqStart.isSwimming;
            diag.adtLiquidStartHasLevel = false; diag.adtLiquidStartLevel = 0.0f; diag.adtLiquidStartType = 0u; diag.adtLiquidStartSwimming = false;
        } else {
            diag.adtLiquidStartHasLevel = liqStart.hasLevel;
            diag.adtLiquidStartLevel = liqStart.level;
            diag.adtLiquidStartType = liqStart.type;
            diag.adtLiquidStartSwimming = liqStart.isSwimming;
            diag.vmapLiquidStartHasLevel = false; diag.vmapLiquidStartLevel = 0.0f; diag.vmapLiquidStartType = 0u; diag.vmapLiquidStartSwimming = false;
        }

        if (liqEnd.fromVmap) {
            diag.vmapLiquidEndHasLevel = liqEnd.hasLevel;
            diag.vmapLiquidEndLevel = liqEnd.level;
            diag.vmapLiquidEndType = liqEnd.type;
            diag.vmapLiquidEndSwimming = liqEnd.isSwimming;
            diag.adtLiquidEndHasLevel = false; diag.adtLiquidEndLevel = 0.0f; diag.adtLiquidEndType = 0u; diag.adtLiquidEndSwimming = false;
        } else {
            diag.adtLiquidEndHasLevel = liqEnd.hasLevel;
            diag.adtLiquidEndLevel = liqEnd.level;
            diag.adtLiquidEndType = liqEnd.type;
            diag.adtLiquidEndSwimming = liqEnd.isSwimming;
            diag.vmapLiquidEndHasLevel = false; diag.vmapLiquidEndLevel = 0.0f; diag.vmapLiquidEndType = 0u; diag.vmapLiquidEndSwimming = false;
        }
    }

    // Combined stats for hits
    diag.hitCount = combinedHits.size();
    float hitMinZ = FLT_MAX, hitMaxZ = -FLT_MAX;
    size_t penCount = 0, nonPenCount = 0, walkableNP = 0; float earliestNP = FLT_MAX; std::set<uint32_t> uniqueInst;
    for (const auto& hHit : combinedHits) {
        if (hHit.startPenetrating) penCount++; else nonPenCount++;
        if (!hHit.startPenetrating) {
            earliestNP = std::min(earliestNP, hHit.distance);
            if (std::fabs(hHit.normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) walkableNP++;
        }
        hitMinZ = std::min(hitMinZ, hHit.point.z);
        hitMaxZ = std::max(hitMaxZ, hHit.point.z);
        uniqueInst.insert(hHit.instanceId);
    }
    if (earliestNP == FLT_MAX) earliestNP = -1.0f;
    if (diag.hitCount == 0) { hitMinZ = 0.0f; hitMaxZ = 0.0f; }
    diag.penCount = penCount;
    diag.nonPenCount = nonPenCount;
    diag.walkableNonPen = walkableNP;
    diag.earliestNonPen = earliestNP;
    diag.hitMinZ = hitMinZ;
    diag.hitMaxZ = hitMaxZ;
    diag.uniqueInstanceCount = uniqueInst.size();

    // Build movement manifold from hits then use pure helpers for dedup, primary selection, and slide direction
    {
        std::vector<ContactPlane> planes; planes.reserve(combinedHits.size());
        for (const auto& hHit : combinedHits) {
            ContactPlane cp;
            cp.normal = hHit.normal.directionOrZero();
            if (cp.normal.magnitude() <= 1e-5f) cp.normal = G3D::Vector3(0,0,1);
            cp.point = hHit.point;
            cp.walkable = (std::fabs(cp.normal.z) >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z);
            cp.penetrating = hHit.startPenetrating;
            planes.push_back(cp);
        }
        // Epsilons derived from capsule radius
        const float normalEps = 1e-3f;
        const float pointZEps = 1e-3f;
        const float pointXYEps = std::max(1e-3f, (r * 0.01f));
        auto dedup = PhysicsDiag::DeduplicatePlanes(planes, normalEps, pointXYEps, pointZEps);

        // Rebuild diag planes
        diag.planes.clear();
        diag.walkablePlanes.clear();
        for (const auto& cp : dedup) {
            SceneQuery::SweepResults::ContactPlane dcp;
            dcp.normal = cp.normal; dcp.point = cp.point; dcp.walkable = cp.walkable; dcp.penetrating = cp.penetrating;
            dcp.source = SceneQuery::SweepResults::StandSource::VMAP;
            diag.planes.push_back(dcp);
            if (dcp.walkable) diag.walkablePlanes.push_back(dcp);
        }

        // Choose primary via pure helper
        bool moving = !noInput;
        auto primarySel = PhysicsDiag::ChoosePrimaryPlane(dedup, moving, /*isSwimming*/ false);
        diag.hasPrimaryPlane = primarySel.first;
        if (diag.hasPrimaryPlane) {
            SceneQuery::SweepResults::ContactPlane dcp;
            dcp.normal = primarySel.second.normal;
            dcp.point = primarySel.second.point;
            dcp.walkable = primarySel.second.walkable;
            dcp.penetrating = primarySel.second.penetrating;
            dcp.source = SceneQuery::SweepResults::StandSource::VMAP;
            diag.primaryPlane = dcp;
        }

        // Compute slide direction
        std::vector<ContactPlane> pureWalk; pureWalk.reserve(diag.walkablePlanes.size());
        for (const auto& wp : diag.walkablePlanes) {
            ContactPlane cp; cp.normal = wp.normal; cp.point = wp.point; cp.walkable = wp.walkable; cp.penetrating = wp.penetrating; pureWalk.push_back(cp);
        }
        auto slideRes = PhysicsDiag::ComputeSlideDir(diag.hasPrimaryPlane ? ContactPlane{diag.primaryPlane.normal, diag.primaryPlane.point, diag.primaryPlane.walkable, diag.primaryPlane.penetrating} : ContactPlane{}, pureWalk, moveDir);
        diag.slideDirValid = slideRes.first;
        diag.slideDir = slideRes.second;
        if (!diag.slideDirValid && moveDir.magnitude() > 1e-6f)
            diag.slideDir = moveDir.directionOrZero();
    }

    // Constraint iterations and thresholds
    diag.constraintIterations = 3;
    diag.slopeClampThresholdZ = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;

    // CCD & depenetration
    {
        float minTOI = FLT_MAX;
        G3D::Vector3 depen(0,0,0);
        float maxPenDepth = 0.0f;
        for (const auto& hHit : combinedHits) {
            if (!hHit.startPenetrating) {
                if (!noInput) minTOI = std::min(minTOI, hHit.time);
            } else {
                float d = std::max(0.0f, hHit.penetrationDepth);
                depen += hHit.normal.directionOrZero() * d;
                if (d > maxPenDepth) maxPenDepth = d;
            }
        }
        if (minTOI == FLT_MAX) minTOI = -1.0f;
        diag.minTOI = minTOI;
        diag.depenetration = depen;
        diag.depenetrationMagnitude = depen.magnitude();
        // suggestedSkinWidth removed; no skin usage
    }

    return diag;
}

bool SceneQuery::LineOfSight(uint32_t mapId, const G3D::Vector3& from, const G3D::Vector3& to)
{
    // VMAP LOS (WMO/M2 geometry)
    bool vmapClear = true;
    if (m_vmapManager)
    {
        try
        {
            if (!m_vmapManager->isMapInitialized(mapId))
                m_vmapManager->initializeMap(mapId);

            vmapClear = m_vmapManager->isInLineOfSight(mapId, from.x, from.y, from.z, to.x, to.y, to.z, false);
        }
        catch (...) {}
    }

    if (!vmapClear)
        return false;

    // ADT terrain raycast using MapLoader triangles
    if (m_mapLoader)
    {
        try
        {
            float minX = std::min(from.x, to.x);
            float minY = std::min(from.y, to.y);
            float maxX = std::max(from.x, to.x);
            float maxY = std::max(from.y, to.y);

            std::vector<MapFormat::TerrainTriangle> tris;
            if (m_mapLoader->GetTerrainTriangles(mapId, minX, minY, maxX, maxY, tris))
            {
                G3D::Vector3 rayStart(from.x, from.y, from.z);
                G3D::Vector3 rayEnd(to.x, to.y, to.z);
                G3D::Vector3 dir = (rayEnd - rayStart);
                float len = dir.magnitude();
                if (len > 1e-6f)
                {
                    dir = dir * (1.0f / len);

                    for (const auto& t : tris)
                    {
                        G3D::Vector3 a(t.ax, t.ay, t.az);
                        G3D::Vector3 b(t.bx, t.by, t.bz);
                        G3D::Vector3 c(t.cx, t.cy, t.cz);

                        G3D::Vector3 edge1 = b - a;
                        G3D::Vector3 edge2 = c - a;
                        G3D::Vector3 pvec = dir.cross(edge2);
                        float det = edge1.dot(pvec);
                        if (std::fabs(det) < 1e-7f)
                            continue;
                        float invDet = 1.0f / det;
                        G3D::Vector3 tvec = rayStart - a;
                        float u = tvec.dot(pvec) * invDet;
                        if (u < 0.0f || u > 1.0f) continue;
                        G3D::Vector3 qvec = tvec.cross(edge1);
                        float v = dir.dot(qvec) * invDet;
                        if (v < 0.0f || u + v > 1.0f) continue;
                        float tHit = edge2.dot(qvec) * invDet;
                        if (tHit >= 0.0f && tHit <= len)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        catch (...) {}
    }

    return true;
}

void SceneQuery::Initialize()
{
    if (m_initialized)
        return;

    try
    {
        // Acquire or create the VMapManager instance and configure it
        m_vmapManager = static_cast<VMAP::VMapManager2*>(VMAP::VMapFactory::createOrGetVMapManager());
        if (m_vmapManager)
        {
            VMAP::VMapFactory::initialize();

            // Try common base paths
            std::vector<std::string> vps = { "vmaps/", "Data/vmaps/", "../Data/vmaps/" };
            for (auto& vp : vps)
            {
                if (std::filesystem::exists(vp))
                {
                    m_vmapManager->setBasePath(vp);
                    break;
                }
            }
        }

        // Initialize MapLoader ahead of time (terrain ADT support)
        if (!m_mapLoader)
        {
            m_mapLoader = new MapLoader();
            // Try common map data paths
            std::vector<std::string> mps = { "maps/", "Data/maps/", "../Data/maps/" };
            for (auto& mp : mps)
            {
                if (std::filesystem::exists(mp))
                {
                    m_mapLoader->Initialize(mp);
                    break;
                }
            }
        }
    }
    catch (...)
    {
        m_vmapManager = nullptr;
        // Leave m_mapLoader as-is on exception; caller may still set it explicitly later
    }

    m_initialized = true;
}

void SceneQuery::EnsureMapLoaded(uint32_t mapId)
{
    if (m_vmapManager && !m_vmapManager->isMapInitialized(mapId))
    {
        m_vmapManager->initializeMap(mapId);
    }
}

float SceneQuery::GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType)
{
    // Prefer ADT (MapLoader) if available
    if (m_mapLoader && m_mapLoader->IsInitialized())
    {
        float level = m_mapLoader->GetLiquidLevel(mapId, x, y);
        if (VMAP::IsValidLiquidLevel(level))
        {
            liquidType = m_mapLoader->GetLiquidType(mapId, x, y);
            return level;
        }
    }

    if (m_vmapManager)
    {
        float level, floor; uint32_t type;
        if (m_vmapManager->GetLiquidLevel(mapId, x, y, z, VMAP::MAP_LIQUID_TYPE_ALL_LIQUIDS, level, floor, type))
        {
            liquidType = type;
            return level;
        }
    }

    return VMAP::VMAP_INVALID_LIQUID_HEIGHT;
}

SceneQuery::LiquidInfo SceneQuery::EvaluateLiquidAt(uint32_t mapId, float x, float y, float z)
{
    using namespace PhysicsLiquid;
    LiquidInfo out{};

    // Gather ADT
    float adtLevel = VMAP::VMAP_INVALID_LIQUID_HEIGHT; uint32_t adtType = VMAP::MAP_LIQUID_TYPE_NO_WATER; bool adtHas = false;
    if (m_mapLoader && m_mapLoader->IsInitialized()) {
        adtLevel = m_mapLoader->GetLiquidLevel(mapId, x, y);
        if (MapFormat::IsValidLiquidLevel(adtLevel)) {
            adtType = m_mapLoader->GetLiquidType(mapId, x, y);
            adtHas = true;
        }
    }

    // Gather VMAP
    float vmapLevel = VMAP::VMAP_INVALID_LIQUID_HEIGHT; uint32_t vmapType = VMAP::MAP_LIQUID_TYPE_NO_WATER; bool vmapHas = false;
    if (m_vmapManager) {
        float level, floor; uint32_t type;
        if (m_vmapManager->GetLiquidLevel(mapId, x, y, z + 2.0f, VMAP::MAP_LIQUID_TYPE_ALL_LIQUIDS, level, floor, type)) {
            vmapLevel = level; vmapType = type; vmapHas = true;
        }
    }

    // Unify types for comparison with managed enums
    uint32_t adtUnified = VMAP::GetLiquidEnumUnified(adtType, false);
    uint32_t vmapUnified = VMAP::GetLiquidEnumUnified(vmapType, true);
    uint32_t waterUnified = VMAP::LIQUID_TYPE_WATER;

    // Pure evaluation
    auto pure = PhysicsLiquid::Evaluate(z, vmapHas, vmapLevel, vmapUnified, adtHas, adtLevel, adtUnified, waterUnified);

    // Map back into LiquidInfo
    out.level = pure.level;
    out.type = pure.type;
    out.fromVmap = pure.fromVmap;
    out.hasLevel = pure.hasLevel;
    out.isSwimming = pure.isSwimming;
    return out;
}

namespace
{
    static inline G3D::Vector3 EnsureSafeNormal(const G3D::Vector3& n, const G3D::Vector3& fallback = G3D::Vector3(0, 0, 1))
    {
        if (n.magnitude() <= 1e-7f)
            return fallback;
        return n.directionOrZero();
    }

    // PhysX-like normal orientation:
    // - For sweeps: the reported normal should oppose motion.
    // - For overlaps: the normal should point from the capsule toward the triangle (separating direction).
    static inline G3D::Vector3 OrientNormalForSweep(const G3D::Vector3& n, const G3D::Vector3& sweepDir)
    {
        G3D::Vector3 nn = EnsureSafeNormal(n);
        if (sweepDir.magnitude() > 1e-7f)
        {
            G3D::Vector3 d = sweepDir.directionOrZero();
            if (nn.dot(d) > 0.0f)
                nn = -nn;
        }
        return nn;
    }

    static inline G3D::Vector3 OrientNormalForOverlap(G3D::Vector3 n, const G3D::Vector3& capsuleMid, const G3D::Vector3& triClosestPoint)
    {
        G3D::Vector3 nn = EnsureSafeNormal(n);
        // Ensure normal points from capsule toward triangle contact, which is a stable separating direction.
        G3D::Vector3 towardTri = (triClosestPoint - capsuleMid);
        if (towardTri.magnitude() > 1e-7f && nn.dot(towardTri) < 0.0f)
            nn = -nn;
        return nn;
    }

    // Minimal squared distance between a segment [a,b] and an AABox. If segment intersects the box, returns 0.
    static inline float SegmentAABBDistSqLocal(const G3D::Vector3& a, const G3D::Vector3& b, const G3D::AABox& box)
    {
        // Quick slab intersection test
        G3D::Vector3 dir = b - a;
        float tmin = 0.0f, tmax = 1.0f;
        for (int i = 0; i < 3; ++i)
        {
            float origin = (&a.x)[i];
            float d = (&dir.x)[i];
            float lo = (&box.low().x)[i];
            float hi = (&box.high().x)[i];
            if (std::fabs(d) < 1e-9f)
            {
                if (origin < lo || origin > hi) { tmin = 1.0f; tmax = 0.0f; break; }
                continue;
            }
            float t1 = (lo - origin) / d;
            float t2 = (hi - origin) / d;
            if (t1 > t2) std::swap(t1, t2);
            tmin = std::max(tmin, t1);
            tmax = std::min(tmax, t2);
            if (tmin > tmax) break;
        }
        if (tmin <= tmax && tmin <= 1.0f && tmax >= 0.0f)
            return 0.0f;

        // Evaluate distance at endpoints (cheap fallback)
        auto pointToAABBDistSq = [](const G3D::Vector3& p, const G3D::AABox& bx) {
            const G3D::Vector3 lo = bx.low();
            const G3D::Vector3 hi = bx.high();
            float d = 0.0f;
            if (p.x < lo.x) { float t = lo.x - p.x; d += t * t; } else if (p.x > hi.x) { float t = p.x - hi.x; d += t * t; }
            if (p.y < lo.y) { float t = lo.y - p.y; d += t * t; } else if (p.y > hi.y) { float t = p.y - hi.y; d += t * t; }
            if (p.z < lo.z) { float t = lo.z - p.z; d += t * t; } else if (p.z > hi.z) { float t = p.z - hi.z; d += t * t; }
            return d;
        };
        float d0 = pointToAABBDistSq(a, box);
        float d1 = pointToAABBDistSq(b, box);
        return std::min(d0, d1);
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
    static SceneHit::CapsuleRegion ClassifyCapsuleRegion(float t)
    {
        const float capThresh = 0.15f; // heuristic
        if (t <= capThresh) return SceneHit::CapsuleRegion::Cap0;
        if (t >= 1.0f - capThresh) return SceneHit::CapsuleRegion::Cap1;
        return SceneHit::CapsuleRegion::Side;
    }

    // Stores triangles in MODEL-LOCAL space; broadphase still done in INTERNAL space like raycast.
    class MapMeshView : public CapsuleCollision::TriangleMeshView
    {
    public:
        MapMeshView(const BIH* tree, const VMAP::ModelInstance* instances, uint32_t instanceCount)
            : m_tree(tree), m_instances(instances), m_instanceCount(instanceCount)
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
            // Adaptive inflation: default tiny margins. Without explicit sweep context, use smaller Z inflate.
            float rInflate = 0.01f;
            float zInflate = 0.008f;
            G3D::AABox queryBox(qLo - G3D::Vector3(rInflate, rInflate, zInflate), qHi + G3D::Vector3(rInflate, rInflate, zInflate));
            
            const uint32_t cap = (std::min<uint32_t>)(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            bool bihOk = m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap);
            
            if (!bihOk)
            {
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    const VMAP::ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
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
                // Log preliminary instance list
                for (uint32_t k = 0; k < instCount; ++k)
                {
                    uint32_t idx = instIdx[k];
                    if (idx >= m_instanceCount) continue;
                    const VMAP::ModelInstance& inst = m_instances[idx];
                }
                for (uint32_t i = 0; i < m_instanceCount && instCount < cap; ++i)
                {
                    if (present[i]) continue;
                    const VMAP::ModelInstance& inst = m_instances[i];
                    if (!inst.iModel) continue;
                    if (!inst.iBound.intersects(queryBox)) continue;
                    instIdx[instCount++] = i; present[i] = 1;
                }
            }

            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount) continue;
                const VMAP::ModelInstance& inst = m_instances[idx];
                if (!inst.iModel) continue;
                if (!inst.iBound.intersects(queryBox)) continue;

                // All loaded geometry is collidable; ignore collision masks

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
                    if (!inst.iModel->GetAllMeshData(vertices, indices)) {
                        continue;
                    }
                }

                size_t triCount = indices.size() / 3;

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
                    CapsuleCollision::Triangle T; T.a = { a.x, a.y, a.z }; T.b = { b.x, b.y, b.z }; T.c = { c.x, c.y, c.z }; T.doubleSided = false; T.collisionMask = 0xFFFFFFFFu;
                    int triIndex = (int)m_cache.size();
                    m_cache.push_back(T); m_triToInstance.push_back(idx); m_triToLocalTri.push_back((int)t);
                    if (count < maxCount) outIndices[count++] = triIndex; else break;

                    if (acceptedThisInst < 8) { // log first few sample triangles
                        std::ostringstream oss; oss << "[BroadphaseTriSample] instID=" << inst.ID << " localTri=" << t
                            << " a=(" << a.x << "," << a.y << "," << a.z << ") b=(" << b.x << "," << b.y << "," << b.z << ") c=(" << c.x << "," << c.y << "," << c.z << ")"; 
                    }
                    ++acceptedThisInst;
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

        const VMAP::ModelInstance* triInstance(int triIdx) const
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
        const VMAP::ModelInstance* m_instances;
        uint32_t m_instanceCount;
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
int SceneQuery::OverlapCapsule(const VMAP::StaticMapTree& map,
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
    CapsuleCollision::AABB internalBox; internalBox.min = { iLo.x, iLo.y, iLo.z }; internalBox.max = { iHi.x, iHi.y, iHi.z }; CapsuleCollision::aabbInflate(internalBox, PhysicsTol::AABBInflation(capsule.r));

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());

    int indices[512]; int count = 0;
    view.queryInternal(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int idx = indices[i];
        const auto& Tlocal = view.tri(idx);
            const VMAP::ModelInstance* mi = view.triInstance(idx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        // Use INTERNAL-space capsule endpoints for model-local transform
        G3D::Vector3 p0L = mi->iInvRot * ((iP0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iP1 - mi->iPos) * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsule.r * invScale;

        // Proximity gate: skip triangles whose local AABox is farther than (r+skin)^2 from segment
        G3D::Vector3 triLo(std::min({Tlocal.a.x, Tlocal.b.x, Tlocal.c.x}), std::min({Tlocal.a.y, Tlocal.b.y, Tlocal.c.y}), std::min({Tlocal.a.z, Tlocal.b.z, Tlocal.c.z}));
        G3D::Vector3 triHi(std::max({Tlocal.a.x, Tlocal.b.x, Tlocal.c.x}), std::max({Tlocal.a.y, Tlocal.b.y, Tlocal.c.y}), std::max({Tlocal.a.z, Tlocal.b.z, Tlocal.c.z}));
        G3D::AABox triBox(triLo, triHi);
        float segDistSq = SegmentAABBDistSqLocal(p0L, p1L, triBox);
        const float skin = 0.0f; // remove skin padding
        float allowSq = (CLocal.r + skin) * (CLocal.r + skin);
        if (segDistSq > allowSq) {
            continue;
        }

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

            // PhysX-like: do not force upward hemisphere; orient overlap normal using capsule->triangle separation direction.
            // Use triOnW as a stable closest-point direction when available.
            G3D::Vector3 capsuleMidW = (wP0 + wP1) * 0.5f;
            G3D::Vector3 chosenN = OrientNormalForOverlap(worldNFromLocal, capsuleMidW, triOnW);
            bool flipped = (chosenN.dot(worldNFromLocal) < 0.0f);

            // Estimate region parameter t along capsule axis using closest points (computed above)
            float segLen = std::sqrt((CLocal.p1.x - CLocal.p0.x)*(CLocal.p1.x - CLocal.p0.x) + (CLocal.p1.y - CLocal.p0.y)*(CLocal.p1.y - CLocal.p0.y) + (CLocal.p1.z - CLocal.p0.z)*(CLocal.p1.z - CLocal.p0.z));
            float tParam = 0.0f;
            if (segLen > 1e-6f) {
                tParam = ((segOn.x - CLocal.p0.x)*(CLocal.p1.x - CLocal.p0.x) + (segOn.y - CLocal.p0.y)*(CLocal.p1.y - CLocal.p0.y) + (segOn.z - CLocal.p0.z)*(CLocal.p1.z - CLocal.p0.z)) / (segLen * segLen);
                if (tParam < 0.0f) tParam = 0.0f; else if (tParam > 1.0f) tParam = 1.0f;
            }

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = view.triLocalIndex(idx); h.instanceId = mi->ID; h.startPenetrating = true; h.penetrationDepth = hLocal.depth * mi->iScale; h.normalFlipped = flipped; h.region = ClassifyCapsuleRegion(tParam);
            outOverlaps.push_back(h);
        }
    }

    return (int)outOverlaps.size();
}

int SceneQuery::OverlapSphere(const VMAP::StaticMapTree& map,
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

    MapMeshView view(map.GetBIHTree(), map.GetInstancesPtr(), map.GetInstanceCount());

    int indices[512]; int count = 0;
    view.queryInternal(internalBox, indices, count, 512);

    for (int i = 0; i < count; ++i)
    {
        int idx = indices[i];
        const auto& Tlocal = view.tri(idx);
        const VMAP::ModelInstance* mi = view.triInstance(idx);
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

int SceneQuery::OverlapBox(const VMAP::StaticMapTree& map,
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
    return OverlapSphere(map, c, r, outOverlaps, includeMask);
}

int SceneQuery::SweepCapsule(uint32_t mapId,
    const CapsuleCollision::Capsule& capsuleStart,
    const G3D::Vector3& dir,
    float distance,
    std::vector<SceneHit>& outHits,
    const G3D::Vector3& playerForward,
    const QueryParams& params)
{
    // Group all sweep diagnostics into a single multi-line log block
    std::ostringstream sweepLog;
    sweepLog << "[SweepCapsule] map=" << mapId
             << " p0=(" << capsuleStart.p0.x << "," << capsuleStart.p0.y << "," << capsuleStart.p0.z << ")"
             << " p1=(" << capsuleStart.p1.x << "," << capsuleStart.p1.y << "," << capsuleStart.p1.z << ")"
             << " r=" << capsuleStart.r
             << " dir=(" << dir.x << "," << dir.y << "," << dir.z << ")"
             << " dist=" << distance << "\n";
    auto flushSweepLog = [&]() {
        PHYS_INFO(PHYS_CYL, sweepLog.str());
    };
    // Acquire map tree from injected manager
    const VMAP::StaticMapTree* map = nullptr;
    if (m_vmapManager)
        map = m_vmapManager->GetStaticMapTree(mapId);
    if (!map) {
        outHits.clear();
        return 0;
    }
    outHits.clear();
    if (distance <= 0.0f)
    {
        // Idle settle: single-pass overlap to avoid duplicate traversal/logs
        CapsuleCollision::Capsule inflCaps = capsuleStart;

        std::vector<SceneHit> overlaps;
        OverlapCapsule(*map, inflCaps, overlaps, 0xFFFFFFFFu);

        // Optional: include terrain overlaps around capsule center
        std::vector<MapFormat::TerrainTriangle> terrainTris;
        if (m_mapLoader)
        {
                G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
                G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);
                G3D::Vector3 center = (wP0 + wP1) * 0.5f;
                float r = inflCaps.r;
                m_mapLoader->GetTerrainTriangles(mapId, center.x - r, center.y - r, center.x + r, center.y + r, terrainTris);
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

        if (!overlaps.empty())
        {
            sweepLog << "  OverlapHits count=" << overlaps.size() << " (idle/settle)\n";
            std::sort(overlaps.begin(), overlaps.end(), [](const SceneHit& a, const SceneHit& b) {
                if (std::fabs(a.point.z - b.point.z) > 1e-4f) return a.point.z > b.point.z;
                if (std::fabs(a.penetrationDepth - b.penetrationDepth) > 1e-5f) return a.penetrationDepth > b.penetrationDepth;
                return a.triIndex < b.triIndex;
            });
            outHits = overlaps;
            // Per-hit compact lines
            for (const auto& h : outHits) {
                sweepLog << "    [PenOverlap] inst=" << h.instanceId << " tri=" << h.triIndex
                         << " pt=(" << h.point.x << "," << h.point.y << "," << h.point.z << ")"
                         << " nZ=" << h.normal.z << " depth=" << h.penetrationDepth << "\n";
            }
            flushSweepLog();
            return (int)outHits.size();
        }
        flushSweepLog();
        return 0;
    }

    G3D::Vector3 wP0(capsuleStart.p0.x, capsuleStart.p0.y, capsuleStart.p0.z);
    G3D::Vector3 wP1(capsuleStart.p1.x, capsuleStart.p1.y, capsuleStart.p1.z);
    // Removed QueryParams inflation adjustment
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
    CapsuleCollision::AABB sweepBoxI; sweepBoxI.min = { iMin.x, iMin.y, iMin.z }; sweepBoxI.max = { iMax.x, iMax.y, iMax.z }; CapsuleCollision::aabbInflate(sweepBoxI, PhysicsTol::AABBInflation(capsuleStart.r));
    // Reduce vertical dip: use small epsilon instead of radius-based lowering to avoid pulling far-below triangles

    MapMeshView view(map->GetBIHTree(), map->GetInstancesPtr(), map->GetInstanceCount());
    const int kCap = 1024; int triIdxs[kCap]; int triCount = 0;
    view.queryInternal(sweepBoxI, triIdxs, triCount, kCap);

    // Append broadphase details (no immediate log)
    sweepLog << "  Broadphase triCount=" << triCount
             << " instCount=" << map->GetInstanceCount()
             << " sweepBoxI.min=(" << sweepBoxI.min.x << "," << sweepBoxI.min.y << "," << sweepBoxI.min.z << ")"
             << " max=(" << sweepBoxI.max.x << "," << sweepBoxI.max.y << "," << sweepBoxI.max.z << ")\n";
    if (triCount == 0) {
        // Provide additional hints when no triangles are found
        sweepLog << "  BroadphaseEmpty: mapLoaded="
                 << (m_vmapManager && m_vmapManager->isMapInitialized(mapId) ? 1 : 0)
                 << " instCount=" << map->GetInstanceCount()
                 << " dirMag=" << dir.magnitude()
                 << " distance=" << distance
                 << " aabbInflate=" << PhysicsTol::AABBInflation(capsuleStart.r)
                 << " zBias=" << PhysicsTol::GroundZBias(capsuleStart.r) << "\n";
    }

    // Build per-instance distribution summary
    if (triCount > 0) {
        std::unordered_map<uint32_t, int> instTriCounts;
        for (int i = 0; i < triCount; ++i) {
            const VMAP::ModelInstance* miDist = view.triInstance(triIdxs[i]);
            if (!miDist) continue;
            instTriCounts[miDist->ID]++;
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

        if (m_mapLoader)
        {
            m_mapLoader->GetTerrainTriangles(mapId, minX, minY, maxX, maxY, terrainTris);
        }
    }

    // Start penetration check per triangle in model-local space
    // Compute Z window for vertical gating. For downward step-down sweeps, include full CCD distance.
    float capMinZWorld, capMaxZWorld;
    {
        const float epsZ = 0.05f;
        // Feet Z (lower endcap) and top Z (upper endcap)
        float feetZ = std::min(wP0.z, wP1.z);
        float topZ  = std::max(wP0.z, wP1.z);
        if (dir.z < -0.5f) {
            // Downward sweep: include entire step-down height from feet
            capMinZWorld = feetZ - PhysicsConstants::STEP_DOWN_HEIGHT - epsZ;
            capMaxZWorld = topZ + epsZ;
        } else {
            // Horizontal/other sweeps: keep tight window around capsule ends
            capMinZWorld = feetZ - capsuleStart.r - epsZ;
            capMaxZWorld = topZ + capsuleStart.r + epsZ;
        }
    }

    // 1) VMAP model triangles (existing path)
    size_t vmapRejectedByProximity = 0;
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const VMAP::ModelInstance* mi = view.triInstance(cacheIdx);
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

            // PhysX-like overlap normal: do not force upward hemisphere.
            // Orient from capsule toward triangle using closest-point direction.
            CapsuleCollision::Vec3 segOnL, triOnL;
            (void)CapsuleCollision::closestPoints_Segment_Triangle(CLocal.p0, CLocal.p1, Tlocal, segOnL, triOnL);
            G3D::Vector3 triOnI = mi->iRot * (G3D::Vector3(triOnL.x, triOnL.y, triOnL.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 triOnW = NavCoord::InternalToWorld(triOnI);
            G3D::Vector3 capsuleMidW = (wP0 + wP1) * 0.5f;
            G3D::Vector3 chosenN = OrientNormalForOverlap(worldNFromLocal, capsuleMidW, triOnW);
            bool flipped = (chosenN.dot(worldNFromLocal) < 0.0f);

            // Compute signed slope along forward (horizontal) for diagnostics
            {
                // World triangle built above: wA,wB,wC
                G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
                // Use player forward (world-space) for slope evaluation; fallback to horizontal sweep dir if zero
                G3D::Vector3 fwd = (playerForward.magnitude() > 1e-6f) ? playerForward : dir;
                fwd.z = 0.0f; fwd = fwd.directionOrZero();
                // Evaluate plane Z at two points along forward from contact point
                auto evalPlaneZ = [](const G3D::Vector3& n, const G3D::Vector3& p0, float x, float y, float zHint) {
                    // plane through p0 with normal n: solve for z at (x,y)
                    // n.x*(x-p0.x) + n.y*(y-p0.y) + n.z*(z-p0.z) = 0 => z = p0.z - (n.x*(x-p0.x) + n.y*(y-p0.y)) / n.z
                    if (std::fabs(n.z) < 1e-6f) return zHint; // near vertical, fallback
                    return p0.z - ((n.x * (x - p0.x) + n.y * (y - p0.y)) / n.z);
                };
                float eps = std::max(0.25f, capsuleStart.r * 0.5f);
                float z1 = evalPlaneZ(wN, wPoint, wPoint.x, wPoint.y, wPoint.z);
                float z2 = evalPlaneZ(wN, wPoint, wPoint.x + fwd.x * eps, wPoint.y + fwd.y * eps, wPoint.z);
                float signedSlope = z2 - z1; // >0 uphill, <0 downhill along forward
                sweepLog << "    [SlopeDiag] tri=" << view.triLocalIndex(cacheIdx)
                         << " inst=" << mi->ID
                         << " slope=" << signedSlope
                         << " z1=" << z1 << " z2=" << z2
                         << " capWin=[" << capMinZWorld << "," << capMaxZWorld << "]"
                         << " zHit=" << wPoint.z << "\n";
            }
            // Append compact penetrating hit line to sweep log
            sweepLog << "  [PenHit] inst=" << mi->ID << " tri=" << view.triLocalIndex(cacheIdx)
                     << " pt=(" << wPoint.x << "," << wPoint.y << "," << wPoint.z << ")"
                     << " nZ=" << chosenN.z << " depth=" << (chL.depth * mi->iScale)
                     << (flipped ? " flip=1" : "")
                     << "\n";

            SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = view.triLocalIndex(cacheIdx); h.instanceId = mi->ID; h.startPenetrating = true; h.penetrationDepth = chL.depth * mi->iScale; h.normalFlipped = flipped;
            outHits.push_back(h);
        }
    }

    // 2) Terrain triangles (world space path)
    size_t terrainRejectedByZWindow = 0;
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
                const float skin = 0.0f;
                G3D::Vector3 wPoint(chW.point.x, chW.point.y, chW.point.z);
                if (wPoint.z < capMinZWorld || wPoint.z > capMaxZWorld) {
                    ++terrainRejectedByZWindow;
                    continue;
                }

                // Compute world normal from triangle cross
                G3D::Vector3 wA(tw.ax, tw.ay, tw.az), wB(tw.bx, tw.by, tw.bz), wC(tw.cx, tw.cy, tw.cz);
                G3D::Vector3 wN = EnsureSafeNormal((wB - wA).cross(wC - wA));
                G3D::Vector3 capsuleMidW = (wP0 + wP1) * 0.5f;
                G3D::Vector3 chosenN = OrientNormalForOverlap(wN, capsuleMidW, wPoint);
                bool flipped = (chosenN.dot(wN) < 0.0f);

                // Estimate closest points distance in world for diagnostics (approximate by impact vs segment)
                float segLenW = (wP1 - wP0).magnitude();
                float tParam = 0.0f;
                if (segLenW > 1e-6f) {
                    G3D::Vector3 segOnW = wPoint; // approximate projection point on segment using triangle contact
                    tParam = ((segOnW - wP0).dot(wP1 - wP0)) / (segLenW * segLenW);
                    if (tParam < 0.0f) tParam = 0.0f; else if (tParam > 1.0f) tParam = 1.0f;
                }
                float cpDist = std::fabs((wPoint - (wP0 + (wP1 - wP0) * tParam)).magnitude());

                // Log penetrating terrain hit including cpDist vs skin
                {
                    std::ostringstream msg; msg << "[PenHit] tri=" << static_cast<int>(tIdx)
                        << " instId=0"
                        << " pointW=(" << wPoint.x << "," << wPoint.y << "," << wPoint.z << ")"
                        << " normalW=(" << chosenN.x << "," << chosenN.y << "," << chosenN.z << ")"
                        << " depth=" << chW.depth << " cpDist=" << cpDist << " skin=" << skin;
                    PHYS_INFO(PHYS_SURF, msg.str());
                }
                // Always accept penetration hits; no skin threshold
                {
                    SceneHit h; h.hit = true; h.distance = 0.0f; h.time = 0.0f; h.normal = chosenN; h.point = wPoint; h.triIndex = (int)tIdx; h.instanceId = 0; h.startPenetrating = true; h.penetrationDepth = chW.depth; h.normalFlipped = flipped; h.region = ClassifyCapsuleRegion(tParam);
                    outHits.push_back(h);
                }
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
        // Log rejection diagnostics when a lot of candidates were pruned
        sweepLog << "  Rejections: vmapProx=" << vmapRejectedByProximity
                 << " terrainZWin=" << terrainRejectedByZWindow << "\n";
        return (int)outHits.size();
    }

    // Analytic sweep in model-local space
    struct HitTmp { float t; int triCacheIdx; int triLocalIdx; uint32_t instId; G3D::Vector3 nWorld; G3D::Vector3 pWorld; float penetrationDepth; G3D::Vector3 centerAtHit; SceneHit::CapsuleRegion region; };
    std::vector<HitTmp> candidates; candidates.reserve(triCount + (int)terrainTris.size());
    G3D::Vector3 worldVel = dir * distance;

    // 1) VMAP model triangles
    size_t vmapRejectedByProximity2 = 0;
    for (int i = 0; i < triCount; ++i)
    {
        int cacheIdx = triIdxs[i];
        const CapsuleCollision::Triangle& Tlocal = view.tri(cacheIdx);
        const VMAP::ModelInstance* mi = view.triInstance(cacheIdx);
        if (!mi) continue;

        float invScale = mi->iInvScale;
        G3D::Vector3 p0L = mi->iInvRot * ((iW0 - mi->iPos) * invScale);
        G3D::Vector3 p1L = mi->iInvRot * ((iW1 - mi->iPos) * invScale);
        G3D::Vector3 iVel = NavCoord::WorldDirToInternal(dir) * distance;
        G3D::Vector3 vL = mi->iInvRot * (iVel * invScale);
        CapsuleCollision::Capsule CLocal; CLocal.p0 = { p0L.x, p0L.y, p0L.z }; CLocal.p1 = { p1L.x, p1L.y, p1L.z }; CLocal.r = capsuleStart.r * invScale;
        CapsuleCollision::Vec3 velLocal(vL.x, vL.y, vL.z);

        // Proximity gate before sweep: skip triangles too far from the capsule segment in local space
        G3D::Vector3 triLo(std::min({Tlocal.a.x, Tlocal.b.x, Tlocal.c.x}), std::min({Tlocal.a.y, Tlocal.b.y, Tlocal.c.y}), std::min({Tlocal.a.z, Tlocal.b.z, Tlocal.c.z}));
        G3D::Vector3 triHi(std::max({Tlocal.a.x, Tlocal.b.x, Tlocal.c.x}), std::max({Tlocal.a.y, Tlocal.b.y, Tlocal.c.y}), std::max({Tlocal.a.z, Tlocal.b.z, Tlocal.c.z}));
        G3D::AABox triBox(triLo, triHi);
        float segDistSq = SegmentAABBDistSqLocal(p0L, p1L, triBox);
        float allowSq = (CLocal.r) * (CLocal.r);
        if (segDistSq > allowSq) {
            ++vmapRejectedByProximity2;
            continue;
        }

        float toi; CapsuleCollision::Vec3 nL, pL;
        if (CapsuleCollision::capsuleTriangleSweep(CLocal, velLocal, Tlocal, toi, nL, pL) && toi >= 0.0f && toi <= 1.0f)
        {
            // Local-space triangle vertices
            G3D::Vector3 LA(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z);
            G3D::Vector3 LB(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z);
            G3D::Vector3 LC(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z);
            // Append local triangle vertices (compact)
            sweepLog << "  [Tri] inst=" << mi->ID << " tri=" << view.triLocalIndex(cacheIdx)
                     << " A=(" << LA.x << "," << LA.y << "," << LA.z << ")"
                     << " B=(" << LB.x << "," << LB.y << "," << LB.z << ")"
                     << " C=(" << LC.x << "," << LC.y << "," << LC.z << ")\n";

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
            CapsuleCollision::Vec3 segOn;
            CapsuleCollision::Vec3 triOn;
            CapsuleCollision::Capsule impact = CLocal; impact.p0 = impact.p0 + velLocal * toi; impact.p1 = impact.p1 + velLocal * toi;
            bool cpOk = CapsuleCollision::closestPoints_Segment_Triangle(impact.p0, impact.p1, Tlocal, segOn, triOn);
            float cpDist = std::sqrt((segOn.x - triOn.x)*(segOn.x - triOn.x) + (segOn.y - triOn.y)*(segOn.y - triOn.y) + (segOn.z - triOn.z)*(segOn.z - triOn.z));
            const float skin = 0.0f;
            float segLen = std::sqrt((impact.p1.x - impact.p0.x)*(impact.p1.x - impact.p0.x) + (impact.p1.y - impact.p0.y)*(impact.p1.y - impact.p0.y) + (impact.p1.z - impact.p0.z)*(impact.p1.z - impact.p0.z));
            float tParam = 0.0f;
            if (segLen > 1e-6f) {
                tParam = ((segOn.x - impact.p0.x)*(impact.p1.x - impact.p0.x) + (segOn.y - impact.p0.y)*(impact.p1.y - impact.p0.y) + (segOn.z - impact.p0.z)*(impact.p1.z - impact.p0.z)) / (segLen * segLen);
                if (tParam < 0.0f) tParam = 0.0f; else if (tParam > 1.0f) tParam = 1.0f;
            }
            G3D::Vector3 triOnI = mi->iRot * (G3D::Vector3(triOn.x, triOn.y, triOn.z) * mi->iScale) + mi->iPos;
            G3D::Vector3 triOnW = NavCoord::InternalToWorld(triOnI);
            G3D::Vector3 bc = ComputeBarycentric(wImpact, wA, wB, wC);
            float wzRecon = bc.x * wA.z + bc.y * wB.z + bc.z * wC.z;
            G3D::Vector3 wN = (wB - wA).cross(wC - wA).directionOrZero();
            float planeDist = (wImpact - wA).dot(wN);
            sweepLog << "    [SweepHit] tri=" << view.triLocalIndex(cacheIdx) << " inst=" << mi->ID
                     << " t=" << tClamped
                     << " impact=(" << wImpact.x << "," << wImpact.y << "," << wImpact.z << ")"
                     << " zDelta=" << (wImpact.z - wzRecon)
                     << " planeDist=" << planeDist
                     << " cpOk=" << (cpOk ? 1 : 0)
                     << " cpDist=" << cpDist << "\n";
            // Slope diagnostics for sweep impact
            {
                G3D::Vector3 wN2 = (wB - wA).cross(wC - wA).directionOrZero();
                G3D::Vector3 fwd = (playerForward.magnitude() > 1e-6f) ? playerForward : dir;
                fwd.z = 0.0f; fwd = fwd.directionOrZero();
                auto evalPlaneZ = [](const G3D::Vector3& n, const G3D::Vector3& p0, float x, float y, float zHint) {
                    if (std::fabs(n.z) < 1e-6f) return zHint;
                    return p0.z - ((n.x * (x - p0.x) + n.y * (y - p0.y)) / n.z);
                };
                float eps = std::max(0.25f, capsuleStart.r * 0.5f);
                float z1 = evalPlaneZ(wN2, wImpact, wImpact.x, wImpact.y, wImpact.z);
                float z2 = evalPlaneZ(wN2, wImpact, wImpact.x + fwd.x * eps, wImpact.y + fwd.y * eps, wImpact.z);
                float signedSlope = z2 - z1;
                sweepLog << "    [SlopeDiag] tri=" << view.triLocalIndex(cacheIdx)
                         << " inst=" << mi->ID
                         << " slope=" << signedSlope
                         << " z1=" << z1 << " z2=" << z2
                         << " capWin=[" << capMinZWorld << "," << capMaxZWorld << "]"
                         << " zHit=" << wImpact.z << "\n";
            }

            // Diagnostic: compare world cross normal vs transformed local normal
            G3D::Vector3 localN = (G3D::Vector3(Tlocal.b.x, Tlocal.b.y, Tlocal.b.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z))
                .cross(G3D::Vector3(Tlocal.c.x, Tlocal.c.y, Tlocal.c.z) - G3D::Vector3(Tlocal.a.x, Tlocal.a.y, Tlocal.a.z)).directionOrZero();
            G3D::Vector3 internalN = mi->iRot * (localN * mi->iScale);
            internalN = internalN.directionOrZero();
            G3D::Vector3 worldNFromLocal = NavCoord::InternalDirToWorld(internalN).directionOrZero();
            float nDot = wN.dot(worldNFromLocal);
            float zDiff = wN.z - worldNFromLocal.z;
            // keep diag gated but do not emit separate lines

            // PhysX-like sweep normal: do not force upward hemisphere.
            // Ensure normal opposes motion direction.
            G3D::Vector3 chosenN = OrientNormalForSweep(worldNFromLocal, dir);
            bool flipped = (chosenN.dot(worldNFromLocal) < 0.0f);

            CapsuleCollision::Hit chL; CapsuleCollision::intersectCapsuleTriangle(impact, Tlocal, chL);
            G3D::Vector3 wCenter0 = (wP0 + wP1) * 0.5f; G3D::Vector3 centerAtHit = wCenter0 + dir * (tClamped * distance);
            HitTmp tmp; tmp.t = toi; tmp.triCacheIdx = cacheIdx; tmp.triLocalIdx = view.triLocalIndex(cacheIdx); tmp.instId = mi->ID; tmp.nWorld = chosenN; tmp.pWorld = wImpact; tmp.penetrationDepth = chL.depth * mi->iScale; tmp.centerAtHit = centerAtHit; tmp.region = ClassifyCapsuleRegion(tParam); candidates.push_back(tmp);
        }
    }

    // 2) Terrain triangles in world space
    size_t terrainRejectedByZWindow2 = 0;
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
                G3D::Vector3 wN = EnsureSafeNormal((wB - wA).cross(wC - wA));
                G3D::Vector3 chosenN = OrientNormalForSweep(wN, dir);
                bool flipped = (chosenN.dot(wN) < 0.0f);
                G3D::Vector3 wCenter0 = (wP0 + wP1) * 0.5f;
                G3D::Vector3 centerAtHit = wCenter0 + dir * (tClamped * distance);
                // Use depth from discrete impact approximation (optional)
                CapsuleCollision::Hit chW; CapsuleCollision::intersectCapsuleTriangle({ Cw.p0 + velW * toi, Cw.p1 + velW * toi, Cw.r }, Tterrain, chW);
                // Approx cpDist against capsule axis at TOI
                float segLenW2 = (wP1 - wP0).magnitude();
                float tParam2 = 0.0f;
                if (segLenW2 > 1e-6f) {
                    tParam2 = ((wImpact - wP0).dot(wP1 - wP0)) / (segLenW2 * segLenW2);
                    if (tParam2 < 0.0f) tParam2 = 0.0f; else if (tParam2 > 1.0f) tParam2 = 1.0f;
                }
                float cpDist = (wImpact - (wP0 + (wP1 - wP0) * tParam2)).magnitude();
                sweepLog << "    [SweepHit] tri=" << (int)tIdx << " inst=0"
                         << " t=" << tClamped
                         << " impact=(" << wImpact.x << "," << wImpact.y << "," << wImpact.z << ")"
                     << " cpDist=" << cpDist << "\n";
                // Approximate region using world-space projection
                float segLenW = (wP1 - wP0).magnitude();
                float tParam = 0.0f;
                if (segLenW > 1e-6f) {
                    tParam = ((wImpact - wP0).dot(wP1 - wP0)) / (segLenW * segLenW);
                    if (tParam < 0.0f) tParam = 0.0f; else if (tParam > 1.0f) tParam = 1.0f;
                }
                HitTmp tmp; tmp.t = toi; tmp.triCacheIdx = (int)tIdx; tmp.triLocalIdx = (int)tIdx; tmp.instId = 0u; tmp.nWorld = chosenN; tmp.pWorld = wImpact; tmp.penetrationDepth = chW.depth; tmp.centerAtHit = centerAtHit; tmp.region = ClassifyCapsuleRegion(tParam); candidates.push_back(tmp);
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
            h.region = c.region;
            outHits.push_back(h);
            // Append compact candidate line
            sweepLog << "  [TOI] inst=" << c.instId << " tri=" << c.triLocalIdx
                     << " t=" << h.time << " dist=" << h.distance
                     << " pt=(" << h.point.x << "," << h.point.y << "," << h.point.z << ") nZ=" << h.normal.z << "\n";
        }
        // Inference summary across all hits
        size_t penCount = 0, nonPen = 0, walkNP = 0; float earliestNP = FLT_MAX; float minZ = FLT_MAX, maxZ = -FLT_MAX;
        for (const auto& h : outHits) {
            if (h.startPenetrating) ++penCount; else { ++nonPen; earliestNP = std::min(earliestNP, h.time); if (h.normal.z >= PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) ++walkNP; }
            minZ = std::min(minZ, h.point.z); maxZ = std::max(maxZ, h.point.z);
        }
        if (earliestNP == FLT_MAX) earliestNP = -1.0f;
        sweepLog << "  Summary: hits=" << outHits.size() << " pen=" << penCount << " nonPen=" << nonPen
                 << " walkableNP=" << walkNP << " earliestNP=" << earliestNP
                 << " zMin=" << (outHits.empty()?0:minZ) << " zMax=" << (outHits.empty()?0:maxZ) << "\n";
        sweepLog << "  Rejections: vmapProx=" << vmapRejectedByProximity2
                 << " terrainZWin=" << terrainRejectedByZWindow2 << "\n";
        flushSweepLog();
        return (int)outHits.size();
    }

    // No hits at all
    sweepLog << "  Summary: hits=0\n";
    sweepLog << "  ZWindow capMinZ=" << capMinZWorld << " capMaxZ=" << capMaxZWorld << " terrainTriCount=" << terrainTris.size() << "\n";
    flushSweepLog();
    return 0;
}