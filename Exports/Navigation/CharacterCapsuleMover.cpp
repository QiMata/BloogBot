#include "CharacterCapsuleMover.h"

namespace VMAP
{
    using namespace CapsuleCollision;

    CharacterCapsuleMover::CharacterCapsuleMover()
    {
        m_capsule = Capsule{ Vec3(0,0,0), Vec3(0,1,0), 0.4f };
        m_velocity = Vector3(0,0,0);
        m_grounded = false;
        m_lastHit = Hit();
        m_cfg = CharacterCapsuleConfig();
    }

    void CharacterCapsuleMover::SetPose(const Vector3& base, const CharacterCapsuleConfig& cfg)
    {
        m_cfg = cfg;
        // Up must be normalized for stable behavior (Vector3 helper)
        Vector3 upN = CapsuleCollision::NormalizeSafe(cfg.up, Vector3(0,1,0));
        m_capsule.p0 = ToCC(base);
        m_capsule.p1 = ToCC(base + upN * cfg.height);
        m_capsule.r = cfg.radius;
        m_velocity = Vector3(0,0,0);
        m_grounded = false;
        m_lastHit = Hit();
    }

    bool CharacterCapsuleMover::Tick(const TriangleMeshView& mesh, const Vector3& desiredVelocity, const Vector3& gravity, float dt)
    {
        m_grounded = false;
        m_lastHit = Hit();
        bool collided = false;

        // WickedEngine-style: integrate intended motion with CCD and slide along contacts
        Vec3 vel_step = ToCC(desiredVelocity * dt);
        Vec3 tmpVel = vel_step; // will be adjusted by slide in CCD

        if (moveCapsuleWithCCD(m_capsule, tmpVel, mesh, m_cfg.resolve, m_cfg.ccdSubsteps))
        {
            collided = true;
            // Find a representative last hit within current capsule AABB
            Hit h; int scratchIdx[256]; int count = 0;
            AABB box = aabbFromCapsule(m_capsule);
            const int cap = (int)(sizeof(scratchIdx)/sizeof(scratchIdx[0]));
            mesh.query(box, scratchIdx, count, cap);
            for (int i = 0; i < count; ++i)
            {
                const Triangle& T = mesh.tri(scratchIdx[i]);
                Hit th;
                if (intersectCapsuleTriangle(m_capsule, T, th)) { h = th; break; }
            }
            m_lastHit = h;
        }
        // Update velocity after sliding for caller visibility
        m_velocity = Vector3(tmpVel.x, tmpVel.y, tmpVel.z);

        // Gravity pass: apply g = gravity*dt and resolve potential ground contact
        if (dt > 0.0f)
        {
            Vector3 gstep = gravity * dt;
            if (gstep.x != 0 || gstep.y != 0 || gstep.z != 0)
            {
                Vec3 gVel = ToCC(gstep);
                Vec3 gv = gVel;
                if (moveCapsuleWithCCD(m_capsule, gv, mesh, m_cfg.resolve, m_cfg.ccdSubsteps))
                {
                    collided = true;
                    // Query nearby triangles, choose the first intersecting as ground candidate
                    Hit gh; int scratchIdx[256]; int count = 0;
                    AABB box = aabbFromCapsule(m_capsule);
                    const int cap = (int)(sizeof(scratchIdx)/sizeof(scratchIdx[0]));
                    mesh.query(box, scratchIdx, count, cap);
                    for (int i = 0; i < count; ++i)
                    {
                        const Triangle& T = mesh.tri(scratchIdx[i]);
                        Hit th;
                        if (intersectCapsuleTriangle(m_capsule, T, th)) { gh = th; break; }
                    }
                    if (gh.hit) m_lastHit = gh;

                    // Ground check via normal vs up cosine
                    Vec3 upNcc = Vec3::normalizeSafe(m_cfg.resolve.up, Vec3(0,1,0));
                    float c = gh.hit ? Vec3::dot(gh.normal, upNcc) : -1.0f;
                    if (gh.hit && c >= m_cfg.resolve.groundCosMin)
                    {
                        Vector3 upN = ToV3(upNcc);
                        float vn = m_velocity.x*upN.x + m_velocity.y*upN.y + m_velocity.z*upN.z;
                        m_velocity = m_velocity - upN * vn; // remove vertical component
                        m_grounded = true;
                    }
                }
            }
        }

        return collided;
    }
}
