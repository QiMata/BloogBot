#pragma once

#include "CapsuleCollision.h"
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
        CapsuleCollision::ResolveConfig resolve; // default constructed
    };

    // Non-physics character controller that follows WickedEngine pattern:
    //  - Discrete step + sliding using capsule CCD in substeps
    //  - Separate horizontal move and gravity pass
    //  - No dynamic allocations during Tick()
    class CharacterCapsuleMover {
    public:
        CharacterCapsuleMover();

        // base = feet position; capsule = [p0 = base, p1 = base + up * height]
        void SetPose(const Vector3& base, const CharacterCapsuleConfig& cfg);

        // Move using desired velocity, then apply gravity. dt is in seconds.
        // Returns true if any collision happened during this tick.
        bool Tick(const CapsuleCollision::TriangleMeshView& mesh,
                  const Vector3& desiredVelocity,
                  const Vector3& gravity,
                  float dt);

        bool IsGrounded() const { return m_grounded; }
        CapsuleCollision::Hit LastHit() const { return m_lastHit; }
        CapsuleCollision::Capsule GetCapsule() const { return m_capsule; }

    private:
        static inline CapsuleCollision::Vec3 ToCC(const Vector3& v) { return CapsuleCollision::Vec3(v.x, v.y, v.z); }
        static inline Vector3 ToV3(const CapsuleCollision::Vec3& v) { return Vector3(v.x, v.y, v.z); }

        CapsuleCollision::Capsule m_capsule;
        Vector3 m_velocity; // last tick displacement after sliding (world space)
        bool m_grounded = false;
        CapsuleCollision::Hit m_lastHit;
        CharacterCapsuleConfig m_cfg;
    };
}
