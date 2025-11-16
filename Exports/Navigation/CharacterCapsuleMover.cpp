#include "CharacterCapsuleMover.h"
#include <algorithm>
#include <cmath>
#include "CylinderCollision.h" // for CylinderHelpers::WalkableCosScope

namespace VMAP
{
    using namespace CapsuleCollision;

    static inline float length(const G3D::Vector3& v) { return std::sqrt(v.x*v.x + v.y*v.y + v.z*v.z); }
    static inline G3D::Vector3 normalizeSafe(const G3D::Vector3& v, const G3D::Vector3& def = {0,1,0})
    {
        float len = length(v);
        return (len > 1e-6f) ? v * (1.0f / len) : def;
    }

    static inline G3D::Vector3 projectOntoPlane(const G3D::Vector3& v, const G3D::Vector3& n)
    {
        G3D::Vector3 nn = normalizeSafe(n);
        float vn = v.x*nn.x + v.y*nn.y + v.z*nn.z;
        return v - nn * vn;
    }

    CharacterCapsuleMover::CharacterCapsuleMover()
    {
        m_capsule = Capsule{ Vec3(0,0,0), Vec3(0,1,0), 0.4f };
        m_grounded = false;
        m_lastHit = SceneHit();
        m_cfg = CharacterCapsuleConfig();
    }

    void CharacterCapsuleMover::SetPose(const Vector3& base, const CharacterCapsuleConfig& cfg)
    {
        m_cfg = cfg;
        Vector3 upN = normalizeSafe(cfg.up, Vector3(0,1,0));
        m_capsule.p0 = ToCC(base);
        m_capsule.p1 = ToCC(base + upN * cfg.height);
        m_capsule.r = cfg.radius;
        m_grounded = false;
        m_lastHit = SceneHit();
    }

    bool CharacterCapsuleMover::SweepAndSlide(const StaticMapTree& map, CapsuleCollision::Capsule& C,
                                              Vector3& inOutStep, SceneHit& outHit) const
    {
        outHit = SceneHit();
        float totalDist = length(inOutStep);
        if (totalDist <= 1e-6f)
            return false;

        // Multi-plane manifold: collect up to 4 unique contact normals
        CapsuleCollision::Vec3 manifold[4];
        int manifoldCount = 0;

        Vector3 totalAdv(0,0,0);
        Vector3 rem = inOutStep; // remaining displacement to realize
        bool collided = false;

        // Iterate gathering up to 4 planes and projecting remaining displacement against all simultaneously
        for (int iter = 0; iter < 4; ++iter)
        {
            float dist = length(rem);
            if (dist <= 1e-6f)
                break;

            Vector3 dir = rem * (1.0f / dist);
            SceneHit h;
            if (!SceneQuery::SweepCapsuleTOI(map, C, dir, dist, h, m_cfg.collisionMask))
            {
                // No hit along this segment: advance fully and finish
                C.p0 += ToCC(rem); C.p1 += ToCC(rem);
                totalAdv += rem;
                break;
            }

            // Advance to hit
            float d = std::max(0.0f, h.distance);
            Vector3 adv = dir * d;
            C.p0 += ToCC(adv); C.p1 += ToCC(adv);
            totalAdv += adv;
            outHit = h; collided = true;

            // Compute remaining distance after the hit
            float remainingLen = std::max(0.0f, dist - d);

            // Step-up attempt: if initial horizontal sweep hits and obstacle might be low, try to raise then continue forward
            if (iter == 0 && remainingLen > 1e-6f && m_cfg.stepHeight > 1e-6f)
            {
                Vector3 upN = normalizeSafe(m_cfg.up, Vector3(0,1,0));
                float horizCos = std::fabs(dir.x*upN.x + dir.y*upN.y + dir.z*upN.z);
                if (horizCos < 0.3f) // mostly horizontal intent
                {
                    SceneHit upHit;
                    bool upBlocked = SceneQuery::SweepCapsuleTOI(map, C, upN, m_cfg.stepHeight, upHit, m_cfg.collisionMask);
                    if (!upBlocked)
                    {
                        // Probe forward from the raised pose
                        CapsuleCollision::Capsule Craised = C;
                        G3D::Vector3 raiseVec = upN * m_cfg.stepHeight;
                        Craised.p0 += ToCC(raiseVec);
                        Craised.p1 += ToCC(raiseVec);

                        SceneHit fwdHit;
                        if (!SceneQuery::SweepCapsuleTOI(map, Craised, dir, remainingLen, fwdHit, m_cfg.collisionMask))
                        {
                            // Commit the vertical raise and the forward advance
                            C = Craised;
                            C.p0 += ToCC(dir * remainingLen);
                            C.p1 += ToCC(dir * remainingLen);
                            totalAdv += raiseVec + dir * remainingLen;

                            // Finish this sweep step
                            rem = Vector3(0,0,0);
                            break;
                        }
                    }
                }
            }

            // Add normal to manifold and resolve tiny penetration by slack
            manifoldCount = CapsuleCollision::manifoldAddNormal(manifold, manifoldCount, 4, ToCC(h.normal));
            CapsuleCollision::Hit ch; ch.hit = true; ch.depth = 0.0f; ch.normal = ToCC(h.normal); ch.point = ToCC(h.point);
            CapsuleCollision::Vec3 dummy(0,0,0);
            CapsuleCollision::resolveCapsuleHit(C, ch, dummy, m_cfg.resolve);

            // Remaining displacement along projected manifold direction
            if (remainingLen <= 1e-6f)
                break;

            Vector3 remAfter = dir * remainingLen;
            CapsuleCollision::Vec3 remCC = ToCC(remAfter);
            // Sequential manifold projection without magnitude preservation
            CapsuleCollision::Vec3 remProjCC = CapsuleCollision::projectVelocityAgainstNormals(remCC, manifold, manifoldCount, 3, false);
            rem = ToV3(remProjCC);

            // If projection kills movement, stop
            if (length(rem) <= 1e-6f)
                break;
        }

        // Output realized step
        inOutStep = totalAdv;
        return collided;
    }

    bool CharacterCapsuleMover::Tick(const StaticMapTree& map, const Vector3& velocity, const Vector3& gravity, float dt)
    {
        // Ensure any walkable-slope queries honoring CylinderHelpers use the per-character setting
        VMAP::CylinderHelpers::WalkableCosScope slopeScope(m_cfg.walkableSlopeCos);

        m_grounded = false;
        m_lastHit = SceneHit();
        bool collided = false;

        // Initial depenetration: resolve discrete overlaps before any movement
        {
            const int kMaxIters = 8;
            for (int iter = 0; iter < kMaxIters; ++iter)
            {
                std::vector<SceneHit> overlaps;
                int count = SceneQuery::OverlapCapsule(map, m_capsule, overlaps, m_cfg.collisionMask);
                if (count <= 0)
                    break;

                // Pick the deepest overlap
                const SceneHit* best = nullptr;
                for (const auto& h : overlaps)
                {
                    if (!best || h.distance > best->distance)
                        best = &h;
                }
                if (!best || best->distance <= 0.0f)
                    break;

                // Convert to collision hit and apply correction (no velocity change during depenetration)
                CapsuleCollision::Hit ch; ch.hit = true; ch.depth = best->distance; ch.normal = ToCC(best->normal); ch.point = ToCC(best->point);
                CapsuleCollision::Vec3 dummy(0,0,0);
                if (!CapsuleCollision::resolveCapsuleHit(m_capsule, ch, dummy, m_cfg.resolve))
                    break;
                collided = true;
                m_lastHit = *best;

                // If the remaining penetration is tiny, stop
                if (best->distance <= (m_cfg.resolve.contactOffset + CapsuleCollision::LARGE_EPS))
                    break;
            }
        }

        // Horizontal movement from supplied velocity
        Vector3 totalStep = velocity * dt;
        Vector3 perStep = (std::max(1, m_cfg.ccdSubsteps) > 0) ? totalStep * (1.0f / std::max(1, m_cfg.ccdSubsteps)) : totalStep;
        for (int i = 0; i < std::max(1, m_cfg.ccdSubsteps); ++i)
        {
            if (length(perStep) <= 1e-6f) continue;
            Vector3 step = perStep;
            SceneHit h;
            bool hit = SweepAndSlide(map, m_capsule, step, h);
            if (hit) { m_lastHit = h; collided = true; }
        }

        // After horizontal displacement, perform a short downward sweep to snap to ground and evaluate slope
        {
            Vector3 upN = normalizeSafe(m_cfg.up, Vector3(0,1,0));
            Vector3 downDir = upN * -1.0f;
            // Small snap range: allow contactOffset + small slack
            float snapDist = std::max(0.0f, m_cfg.resolve.contactOffset + 0.05f);
            if (snapDist > 1e-6f)
            {
                SceneHit dh;
                if (SceneQuery::SweepCapsuleTOI(map, m_capsule, downDir, snapDist, dh, m_cfg.collisionMask))
                {
                    float adv = std::max(0.0f, dh.distance);
                    m_capsule.p0 += ToCC(downDir * adv);
                    m_capsule.p1 += ToCC(downDir * adv);
                    m_lastHit = dh; collided = true;

                    // Evaluate slope using normal vs up
                    float c = dh.normal.x*upN.x + dh.normal.y*upN.y + dh.normal.z*upN.z;
                    if (c >= m_cfg.walkableSlopeCos)
                    {
                        m_grounded = true;
                    }
                }
            }
        }

        // Gravity pass
        if (dt > 0.0f)
        {
            Vector3 gstep = gravity * dt;
            if (length(gstep) > 1e-6f)
            {
                SceneHit gh;
                bool ghit = SweepAndSlide(map, m_capsule, gstep, gh);
                if (ghit) { m_lastHit = gh; collided = true; }

                // Ground test via normal vs up cosine (use configured walkableSlopeCos)
                Vector3 upN = normalizeSafe(m_cfg.up, Vector3(0,1,0));
                float c = (ghit ? (gh.normal.x*upN.x + gh.normal.y*upN.y + gh.normal.z*upN.z) : -1.0f);
                if (ghit && c >= m_cfg.walkableSlopeCos)
                {
                    m_grounded = true;
                }
            }
        }

        return collided;
    }
}
