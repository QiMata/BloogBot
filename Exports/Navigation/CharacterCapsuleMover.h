#pragma once

#include "CapsuleCollision.h"
#include "SceneQuery.h"
#include "StaticMapTree.h"
#include "Vector3.h"

namespace VMAP
{
    // Adapter-friendly alias to engine vector
    using Vector3 = G3D::Vector3;

    struct CharacterCapsuleConfig {
        float height = 1.8f;
        float radius = 0.4f;
        Vector3 up = Vector3(0, 1, 0);
        int ccdSubsteps = 5;
        float walkableSlopeCos = 0.5f; // cos(60deg) — default WoW-like slope limit
        float stepHeight = 0.5f; // maximum vertical step-up height to attempt on low obstacles
        uint32_t collisionMask = 0xFFFFFFFFu; // mask from higher-level systems to filter SceneQuery
        CapsuleCollision::ResolveConfig resolve; // default constructed
    };

    // Stateless sweep/slide helper; velocity supplied per Tick (not stored between calls)
    class CharacterCapsuleMover {
    public:
        CharacterCapsuleMover();

        // base = feet position; capsule = [p0 = base, p1 = base + up * height]
        void SetPose(const Vector3& base, const CharacterCapsuleConfig& cfg);

        // Advance capsule using provided velocity (horizontal intent) and gravity (vertical). Velocity is not retained.
        bool Tick(const StaticMapTree& map,
                  const Vector3& velocity,
                  const Vector3& gravity,
                  float dt);

        bool IsGrounded() const { return m_grounded; }
        const SceneHit& LastHit() const { return m_lastHit; }
        CapsuleCollision::Capsule GetCapsule() const { return m_capsule; }

    private:
        static inline CapsuleCollision::Vec3 ToCC(const Vector3& v) { return CapsuleCollision::Vec3(v.x, v.y, v.z); }
        static inline Vector3 ToV3(const CapsuleCollision::Vec3& v) { return Vector3(v.x, v.y, v.z); }

        // Single sweep and slide step using SceneQuery facade
        bool SweepAndSlide(const StaticMapTree& map, CapsuleCollision::Capsule& C,
                           Vector3& inOutStep, SceneHit& outHit) const;

        CapsuleCollision::Capsule m_capsule;
        bool m_grounded = false;
        SceneHit m_lastHit;
        CharacterCapsuleConfig m_cfg;
    };
}
