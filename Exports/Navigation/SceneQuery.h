#pragma once

#include <vector>
#include <cstdint>
#include "Vector3.h"
#include "AABox.h"
#include "Ray.h"
#include "CapsuleCollision.h"

namespace VMAP { class StaticMapTree; class ModelInstance; }

namespace VMAP
{
    struct SceneHit
    {
        bool hit = false;
        float distance = 0.0f; // Ray or sweep travel distance (TOI)
        G3D::Vector3 normal = G3D::Vector3(0, 1, 0);
        G3D::Vector3 point = G3D::Vector3(0, 0, 0);
        uint32_t instanceId = 0; // ModelInstance::ID
        int triIndex = -1;       // Triangle index within model (if available)
    };

    class SceneQuery
    {
    public:
        // Raycasts
        static bool RaycastSingle(const StaticMapTree& map,
                                  const G3D::Vector3& origin,
                                  const G3D::Vector3& dir,
                                  float maxDistance,
                                  SceneHit& outHit);

        static int RaycastAll(const StaticMapTree& map,
                               const G3D::Vector3& origin,
                               const G3D::Vector3& dir,
                               float maxDistance,
                               std::vector<SceneHit>& outHits);

        // Overlaps
        static int OverlapCapsule(const StaticMapTree& map,
                                  const CapsuleCollision::Capsule& capsule,
                                  std::vector<SceneHit>& outOverlaps);

        static int OverlapSphere(const StaticMapTree& map,
                                 const G3D::Vector3& center,
                                 float radius,
                                 std::vector<SceneHit>& outOverlaps);

        static int OverlapBox(const StaticMapTree& map,
                              const G3D::AABox& box,
                              std::vector<SceneHit>& outOverlaps);

        // Sweeps (capsule)
        static bool SweepCapsuleSingle(const StaticMapTree& map,
                                       const CapsuleCollision::Capsule& capsuleStart,
                                       const G3D::Vector3& dir,
                                       float distance,
                                       SceneHit& outHit);

        static int SweepCapsuleAll(const StaticMapTree& map,
                                   const CapsuleCollision::Capsule& capsuleStart,
                                   const G3D::Vector3& dir,
                                   float distance,
                                   std::vector<SceneHit>& outHits);
    };
}
