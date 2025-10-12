#include "CharacterCapsuleMover.h"
#include <algorithm>
#include <cmath>

namespace VMAP
{
    using namespace CapsuleCollision;

    static inline float length(const G3D::Vector3& v) { return std::sqrt(v.x*v.x + v.y*v.y + v.z*v.z); }
    static inline G3D::Vector3 normalizeSafe(const G3D::Vector3& v, const G3D::Vector3& def = {0,1,0})
    {
        float len = length(v);
        if (len > 1e-6f) return v * (1.0f / len);
        return def;
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
        m_velocity = Vector3(0,0,0);
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
        m_velocity = Vector3(0,0,0);
        m_grounded = false;
        m_lastHit = SceneHit();
    }

    bool CharacterCapsuleMover::SweepAndSlide(const StaticMapTree& map, CapsuleCollision::Capsule& C,
                                              Vector3& inOutStep, SceneHit& outHit) const
    {
        outHit = SceneHit();
        float dist = length(inOutStep);
        if (dist <= 1e-6f)
            return false;

        Vector3 dir = inOutStep * (1.0f / dist);
        SceneHit h1;
        if (!SceneQuery::SweepCapsuleSingle(map, C, dir, dist, h1))
        {
            // No hit, advance fully
            C.p0 += ToCC(inOutStep);
            C.p1 += ToCC(inOutStep);
            return false;
        }

        // Advance to first impact
        float d1 = std::max(0.0f, h1.distance);
        Vector3 adv1 = dir * d1;
        C.p0 += ToCC(adv1);
        C.p1 += ToCC(adv1);
        outHit = h1;

        float remaining = std::max(0.0f, dist - d1);
        if (remaining <= 1e-5f)
        {
            // Minimal pop out
            CapsuleCollision::Hit ch; ch.hit = true; ch.depth = 0.0005f; ch.normal = ToCC(h1.normal); ch.point = ToCC(h1.point);
            CapsuleCollision::Vec3 dummy = ToCC(Vector3(0,0,0));
            CapsuleCollision::resolveCapsuleHit(C, ch, dummy, m_cfg.resolve);
            inOutStep = adv1;
            return true;
        }

        // Slide remaining along the contact plane
        Vector3 slideDir = projectOntoPlane(dir, h1.normal);
        float slideLen = length(slideDir);
        if (slideLen <= 1e-6f)
        {
            inOutStep = adv1; // no slide possible
            return true;
        }
        slideDir = slideDir * (1.0f / slideLen);

        SceneHit h2;
        if (!SceneQuery::SweepCapsuleSingle(map, C, slideDir, remaining, h2))
        {
            // Advance fully along slide
            Vector3 adv2 = slideDir * remaining;
            C.p0 += ToCC(adv2);
            C.p1 += ToCC(adv2);
            inOutStep = adv1 + adv2;
            return true;
        }

        // Second impact during slide
        float d2 = std::max(0.0f, h2.distance);
        Vector3 adv2 = slideDir * d2;
        C.p0 += ToCC(adv2);
        C.p1 += ToCC(adv2);
        outHit = h2; // return the most recent hit

        // Minimal pop out on second contact
        CapsuleCollision::Hit ch2; ch2.hit = true; ch2.depth = 0.0005f; ch2.normal = ToCC(h2.normal); ch2.point = ToCC(h2.point);
        CapsuleCollision::Vec3 dummy2 = ToCC(Vector3(0,0,0));
        CapsuleCollision::resolveCapsuleHit(C, ch2, dummy2, m_cfg.resolve);

        inOutStep = adv1 + adv2;
        return true;
    }

    bool CharacterCapsuleMover::Tick(const StaticMapTree& map, const Vector3& desiredVelocity, const Vector3& gravity, float dt)
    {
        m_grounded = false;
        m_lastHit = SceneHit();
        bool collided = false;

        // Intended motion step using CCD substeps routed via SceneQuery sweeps
        Vector3 totalStep = desiredVelocity * dt;
        Vector3 perStep = (std::max(1, m_cfg.ccdSubsteps) > 0) ? totalStep * (1.0f / std::max(1, m_cfg.ccdSubsteps)) : totalStep;
        Vector3 dispAccum(0,0,0);
        for (int i = 0; i < std::max(1, m_cfg.ccdSubsteps); ++i)
        {
            if (length(perStep) <= 1e-6f) continue;
            // capture before
            Vector3 before(m_capsule.p0.x, m_capsule.p0.y, m_capsule.p0.z);
            Vector3 step = perStep;
            SceneHit h;
            bool hit = SweepAndSlide(map, m_capsule, step, h);
            Vector3 after(m_capsule.p0.x, m_capsule.p0.y, m_capsule.p0.z);
            dispAccum += (after - before);
            if (hit) { m_lastHit = h; collided = true; }
        }

        // Update displacement after sliding for caller visibility (excluding gravity)
        m_velocity = dispAccum;

        // Gravity pass
        if (dt > 0.0f)
        {
            Vector3 gstep = gravity * dt;
            if (length(gstep) > 1e-6f)
            {
                // capture before for possible external use; we keep m_velocity as horizontal-only
                SceneHit gh;
                bool ghit = SweepAndSlide(map, m_capsule, gstep, gh);
                if (ghit) { m_lastHit = gh; collided = true; }

                // Ground test via normal vs up cosine
                Vector3 upN = normalizeSafe(m_cfg.up, Vector3(0,1,0));
                float c = (ghit ? (gh.normal.x*upN.x + gh.normal.y*upN.y + gh.normal.z*upN.z) : -1.0f);
                if (ghit && c >= m_cfg.resolve.groundCosMin)
                {
                    // Remove vertical component from horizontal velocity and mark grounded
                    float vn = m_velocity.x*upN.x + m_velocity.y*upN.y + m_velocity.z*upN.z;
                    m_velocity = m_velocity - upN * vn;
                    m_grounded = true;
                }
            }
        }

        return collided;
    }
}
