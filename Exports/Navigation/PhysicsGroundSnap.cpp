// PhysicsGroundSnap.cpp - Ground snapping and step detection implementation
#include "PhysicsGroundSnap.h"
#include "PhysicsShapeHelpers.h"
#include "PhysicsSelectHelpers.h"
#include "PhysicsEngine.h"
#include "VMapLog.h"
#include <sstream>
#include <algorithm>
#include <cfloat>

namespace PhysicsGroundSnap
{

bool TryStepUpSnap(
    uint32_t mapId,
    GroundSnapState& st,
    float r,
    float h,
    float maxUp)
{
    CapsuleCollision::Capsule capUp = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> upHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, capUp, G3D::Vector3(0,0,1), maxUp, upHits, playerFwd);
    
    const float walkableCosMinUp = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    
    const SceneHit* bestUp = nullptr; 
    float minUpDist = FLT_MAX;
    const SceneHit* bestUpPen = nullptr; 
    float bestUpPenZ = -FLT_MAX;
    
    for (const auto& hh : upHits) {
        if (!hh.hit) 
            continue;
        if (std::fabs(hh.normal.z) < walkableCosMinUp) 
            continue;
            
        if (!hh.startPenetrating) {
            if (hh.distance < 1e-6f) 
                continue;
            if (hh.distance < minUpDist) { 
                minUpDist = hh.distance; 
                bestUp = &hh; 
            }
        } else {
            if (hh.point.z > bestUpPenZ) { 
                bestUpPenZ = hh.point.z; 
                bestUpPen = &hh; 
            }
        }
    }
    
    const SceneHit* use = bestUp ? bestUp : bestUpPen;
    if (!use)
        return false;
        
    float nx = use->normal.x, ny = use->normal.y, nz = use->normal.z;
    float px = use->point.x,  py = use->point.y,  pz = use->point.z;
    
    float planeZ = pz;
    if (std::fabs(nz) > 1e-6f) {
        planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
    }
    
    const float snapEps = 1e-4f;
    float snapZ = planeZ + snapEps;
    float dz = snapZ - st.z;
    
    if (dz >= 0.0f && dz <= maxUp + snapEps + 1e-4f) {
        st.z = snapZ;
        // Refine Z with direct height query at exact XY (no capsule lateral offset bias)
        float preciseZ = SceneQuery::GetGroundZ(mapId, st.x, st.y, st.z, maxUp + 0.5f);
        if (preciseZ > PhysicsConstants::INVALID_HEIGHT &&
            preciseZ <= st.z + 0.1f && preciseZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT)
            st.z = preciseZ;
        st.isGrounded = true;
        st.vz = 0.0f;
        st.groundNormal = use->normal.directionOrZero();
        return true;
    }
    
    return false;
}

bool TryDownwardStepSnap(
    uint32_t mapId,
    GroundSnapState& st,
    float r,
    float h)
{
    bool snapped = false;
    
    CapsuleCollision::Capsule cap = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    G3D::Vector3 downDir(0, 0, -1);
    float settleDist = PhysicsConstants::STEP_DOWN_HEIGHT;
    std::vector<SceneHit> downHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, cap, downDir, settleDist, downHits, playerFwd);
    
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const float stepDownLimit = PhysicsConstants::STEP_DOWN_HEIGHT;
    const float snapEps = 1e-4f;
    const float maxAllowedPenDepth = 0.02f;

    struct Cand { const SceneHit* hit{nullptr}; float planeZ{0}; float snapZ{0}; float toi{0}; };
    std::vector<Cand> cands;
    cands.reserve(downHits.size());

    for (const auto& hhit : downHits) {
        if (!hhit.hit || hhit.startPenetrating) continue;
        if (std::fabs(hhit.normal.z) < walkableCosMin) continue;
        if (hhit.distance < 1e-6f) continue;

        float nx = hhit.normal.x, ny = hhit.normal.y, nz = hhit.normal.z;
        float px = hhit.point.x,  py = hhit.point.y,  pz = hhit.point.z;
        float planeZ = pz;
        if (std::fabs(nz) > 1e-6f) {
            planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
        }
        float snapZ = planeZ + snapEps;
        if (snapZ > st.z) snapZ = st.z;

        float dz = snapZ - st.z;
        if (dz > snapEps) continue;
        if (-dz > stepDownLimit + snapEps + 1e-4f) continue;

        cands.push_back(Cand{ &hhit, planeZ, snapZ, hhit.distance });
    }

    std::stable_sort(cands.begin(), cands.end(), [&](const Cand& a, const Cand& b) {
        if (std::fabs(a.planeZ - b.planeZ) > 1e-4f) return a.planeZ > b.planeZ;
        return a.toi < b.toi;
    });

    auto validate = [&](const Cand& c, float& outMaxPen) -> bool {
        CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, c.snapZ, r, h);
        std::vector<SceneHit> overlaps;
        SceneQuery::SweepCapsule(mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);
        outMaxPen = 0.0f;
        for (const auto& oh : overlaps) {
            if (!oh.startPenetrating) continue;
            outMaxPen = std::max(outMaxPen, std::max(0.0f, oh.penetrationDepth));
        }
        return outMaxPen <= maxAllowedPenDepth;
    };

    const Cand* best = nullptr;
    float bestMaxPen = FLT_MAX;
    for (const auto& c : cands) {
        float maxPen = 0.0f;
        if (validate(c, maxPen)) { best = &c; bestMaxPen = maxPen; break; }
    }
    if (!best && !cands.empty()) {
        for (const auto& c : cands) {
            float maxPen = 0.0f;
            (void)validate(c, maxPen);
            if (!best || maxPen < bestMaxPen) { best = &c; bestMaxPen = maxPen; }
        }
    }

    if (best && best->hit) {
        st.z = best->snapZ;
        // Refine Z with direct height query at exact XY
        float preciseZ = SceneQuery::GetGroundZ(mapId, st.x, st.y, st.z,
            PhysicsConstants::STEP_DOWN_HEIGHT);
        if (preciseZ > PhysicsConstants::INVALID_HEIGHT &&
            preciseZ <= st.z + 0.1f && preciseZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT)
            st.z = preciseZ;
        st.isGrounded = true;
        st.vz = 0.0f;
        st.groundNormal = best->hit->normal.directionOrZero();
        snapped = true;
    }
    
    // Try penetrating contacts if no non-penetrating found
    if (!snapped) {
        const SceneHit* bestPenWalk = nullptr; 
        float bestPenZ = FLT_MAX;
        for (const auto& hhit : downHits) {
            if (!hhit.startPenetrating) continue;
            if (std::fabs(hhit.normal.z) < walkableCosMin) continue;
            bool better = false;
            if (!bestPenWalk) better = true;
            else {
                if ((hhit.instanceId == 0) && (bestPenWalk->instanceId != 0)) better = true;
                else if ((hhit.instanceId == bestPenWalk->instanceId) && hhit.point.z < bestPenZ) better = true;
            }
            if (better) { bestPenWalk = &hhit; bestPenZ = hhit.point.z; }
        }
        if (bestPenWalk) {
            float nx = bestPenWalk->normal.x, ny = bestPenWalk->normal.y, nz = bestPenWalk->normal.z;
            float px = bestPenWalk->point.x,  py = bestPenWalk->point.y,  pz = bestPenWalk->point.z;
            float snapZ = pz;
            if (std::fabs(nz) > 1e-6f) {
                snapZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
            }
            float dz = snapZ - st.z;
            if (std::fabs(dz) <= stepDownLimit + 1e-4f) {
                CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, snapZ, r, h);
                std::vector<SceneHit> overlaps;
                SceneQuery::SweepCapsule(mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);
                float maxPen = 0.0f;
                for (const auto& oh : overlaps) {
                    if (!oh.startPenetrating) continue;
                    maxPen = std::max(maxPen, std::max(0.0f, oh.penetrationDepth));
                }
                if (maxPen <= maxAllowedPenDepth) {
                    st.z = snapZ;
                    // Refine Z with direct height query at exact XY
                    float preciseZ2 = SceneQuery::GetGroundZ(mapId, st.x, st.y, st.z,
                        PhysicsConstants::STEP_DOWN_HEIGHT);
                    if (preciseZ2 > PhysicsConstants::INVALID_HEIGHT &&
                        preciseZ2 <= st.z + 0.1f && preciseZ2 >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT)
                        st.z = preciseZ2;
                    st.isGrounded = true;
                    st.vz = 0.0f;
                    st.groundNormal = bestPenWalk->normal.directionOrZero();
                    snapped = true;
                }
            }
        }
    }
    
    return snapped;
}

bool VerticalSweepSnapDown(
    uint32_t mapId,
    GroundSnapState& st,
    float r,
    float h,
    float maxDown)
{
    CapsuleCollision::Capsule capProbe = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> downHits;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, capProbe, G3D::Vector3(0,0,-1), maxDown, downHits, playerFwd);
    
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    const SceneHit* bestNP = PhysSelect::FindEarliestWalkableNonPen(downHits, walkableCosMin);
    
    if (bestNP) {
        float nx = bestNP->normal.x, ny = bestNP->normal.y, nz = bestNP->normal.z;
        float px = bestNP->point.x,  py = bestNP->point.y,  pz = bestNP->point.z;
        float planeZ = pz;
        if (std::fabs(nz) > 1e-6f) {
            planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
        }
        const float snapEps = 1e-4f;
        float snapZ = planeZ + snapEps;
        float dz = snapZ - st.z;
        if (dz <= snapEps) {
            st.z = snapZ;
            // Refine Z with direct height query at exact XY
            float preciseZ = SceneQuery::GetGroundZ(mapId, st.x, st.y, st.z, maxDown + 0.5f);
            if (preciseZ > PhysicsConstants::INVALID_HEIGHT &&
                preciseZ <= st.z + 0.1f && preciseZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT)
                st.z = preciseZ;
            st.isGrounded = true;
            st.vz = 0.0f;
            st.groundNormal = bestNP->normal.directionOrZero();
            return true;
        }
    }
    return false;
}

float ApplyHorizontalDepenetration(
    uint32_t mapId,
    GroundSnapState& st,
    float r,
    float h,
    bool walkableOnly)
{
    CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> overlaps;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);
    
    G3D::Vector3 depen(0,0,0); 
    int penCount = 0;
    for (const auto& oh : overlaps) {
        if (!oh.startPenetrating) continue;
        if (walkableOnly && std::fabs(oh.normal.z) < PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z) continue;
        if (oh.region != SceneHit::CapsuleRegion::Side) continue;
        G3D::Vector3 nH(oh.normal.x, oh.normal.y, 0.0f);
        if (nH.magnitude() <= 1e-6f) continue;
        depen += nH.directionOrZero() * std::max(0.0f, oh.penetrationDepth);
        ++penCount;
    }
    if (penCount > 0 && depen.magnitude() > 1e-6f) {
        G3D::Vector3 push = depen.directionOrZero() * std::min(0.05f, depen.magnitude());
        st.x += push.x; 
        st.y += push.y;
        return push.magnitude();
    }
    return 0.0f;
}

float ApplyVerticalDepenetration(
    uint32_t mapId,
    GroundSnapState& st,
    float r,
    float h)
{
    CapsuleCollision::Capsule capHere = PhysShapes::BuildFullHeightCapsule(st.x, st.y, st.z, r, h);
    std::vector<SceneHit> overlaps;
    G3D::Vector3 playerFwd(std::cos(st.orientation), std::sin(st.orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, capHere, G3D::Vector3(0,0,0), 0.0f, overlaps, playerFwd);
    
    const float walkableCosMin = PhysicsConstants::DEFAULT_WALKABLE_MIN_NORMAL_Z;
    // SceneCache overlap normals are oriented by OrientNormalForOverlap: FROM capsule
    // center TOWARD triangle contact. For ground below the capsule center, the normal
    // points DOWNWARD (nz < 0). Use fabs(nz) for the walkable check and pick the
    // contact closest to feet (st.z) instead of "highest" — avoids snapping to overhead
    // WMO geometry when the capsule's top hemisphere overlaps walkways/ramps above.
    const SceneHit* bestUp = nullptr;
    float bestErr = FLT_MAX;
    for (const auto& oh : overlaps) {
        if (!oh.startPenetrating) continue;
        if (std::fabs(oh.normal.z) < walkableCosMin) continue;
        float err = std::fabs(oh.point.z - st.z);
        if (err < bestErr) { bestErr = err; bestUp = &oh; }
    }
    if (bestUp) {
        float nx = bestUp->normal.x, ny = bestUp->normal.y, nz = bestUp->normal.z;
        float px = bestUp->point.x,  py = bestUp->point.y,  pz = bestUp->point.z;
        float planeZ = pz;
        if (std::fabs(nz) > 1e-6f) {
            planeZ = pz - ((nx * (st.x - px) + ny * (st.y - py)) / nz);
        }
        const float snapEps = 1e-4f;
        float snapZ = planeZ + snapEps;
        float dz = snapZ - st.z;
        if (dz > 1e-6f) {
            st.z = snapZ;
            // Refine Z with direct height query at exact XY
            float preciseZ = SceneQuery::GetGroundZ(mapId, st.x, st.y, st.z,
                PhysicsConstants::STEP_DOWN_HEIGHT);
            if (preciseZ > PhysicsConstants::INVALID_HEIGHT &&
                preciseZ <= st.z + 0.1f && preciseZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT)
                st.z = preciseZ;
            st.isGrounded = true;
            st.vz = 0.0f;
            // Store ground normal with consistent upward orientation
            G3D::Vector3 gn = bestUp->normal.directionOrZero();
            if (gn.z < 0.0f) { gn.x = -gn.x; gn.y = -gn.y; gn.z = -gn.z; }
            st.groundNormal = gn;
            return dz;
        }
    }
    return 0.0f;
}

float HorizontalSweepAdvance(
    uint32_t mapId,
    float x, float y, float z,
    float orientation,
    float r,
    float h,
    const G3D::Vector3& dir,
    float dist)
{
    CapsuleCollision::Capsule capStart = PhysShapes::BuildFullHeightCapsule(x, y, z, r, h);
    std::vector<SceneHit> hits;
    G3D::Vector3 playerFwd(std::cos(orientation), std::sin(orientation), 0.0f);
    SceneQuery::SweepCapsule(mapId, capStart, dir, dist, hits, playerFwd);
    
    const SceneHit* earliest = nullptr; 
    float minDist = FLT_MAX;
    for (const auto& hh : hits) {
        if (!hh.hit || hh.startPenetrating) continue;
        if (hh.region != SceneHit::CapsuleRegion::Side) continue;
        if (hh.distance < 1e-6f) continue;
        if (hh.distance < minDist) { 
            minDist = hh.distance; 
            earliest = &hh; 
        }
    }
    if (earliest) 
        return std::max(0.0f, std::min(dist, minDist));
    return dist;
}

} // namespace PhysicsGroundSnap
