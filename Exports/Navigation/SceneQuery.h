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
    // New unified query parameter set (similar conceptually to UE4/UE5 query params).
    struct QueryParams
    {
        float inflation = 0.02f;            // Extra inflation (in world units) applied to broad-phase shape/AABB searches
        bool backfaceCulling = false;       // If true, ignore back-face hits (currently unused / TODO)
        uint32_t includeMask = 0xFFFFFFFFu; // Bitmask of collision channels to include (ANDed with per-instance mask)
        uint32_t excludeMask = 0u;          // Bitmask of collision channels to exclude (applied after include)
        std::vector<uint32_t> ignoreInstanceIds; // Instance/model IDs to ignore entirely
        bool returnFaceIndex = true;        // If false, triIndex will be set to -1 in results
        bool returnPhysMat = false;         // If true, (future) physical material retrieval enabled (not implemented yet)
        bool traceComplex = true;           // Whether to trace against complex geometry (always true for now)
    };

    struct SceneHit
    {
        bool hit = false;
        float distance = 0.0f; // Ray or sweep travel distance (TOI) or overlap depth
        float time = 0.0f;     // Normalized [0,1] fraction along the sweep/raycast when hit (0 if overlap/no hit)
        G3D::Vector3 normal = G3D::Vector3(0, 1, 0);
        G3D::Vector3 point = G3D::Vector3(0, 0, 0);
        uint32_t instanceId = 0; // ModelInstance::ID
        int triIndex = -1;       // Triangle index within model (if available)
        bool startPenetrating = false; // True if the sweep started already overlapping (t=0 overlap)
        bool normalFlipped = false; // True if we flipped normal to enforce upward-facing hemisphere
    };

    class SceneQuery
    {
    public:
        // Raycasts
        static bool RaycastSingle(const StaticMapTree& map,
                                  const G3D::Vector3& origin,
                                  const G3D::Vector3& dir,
                                  float maxDistance,
                                  SceneHit& outHit,
                                  const QueryParams& params = QueryParams());

        static int RaycastAll(const StaticMapTree& map,
                               const G3D::Vector3& origin,
                               const G3D::Vector3& dir,
                               float maxDistance,
                               std::vector<SceneHit>& outHits,
                               const QueryParams& params = QueryParams());

        // Overlaps
        static int OverlapCapsule(const StaticMapTree& map,
                                  const CapsuleCollision::Capsule& capsule,
                                  std::vector<SceneHit>& outOverlaps,
                                  uint32_t includeMask = 0xFFFFFFFFu,
                                  const QueryParams& params = QueryParams());

        static int OverlapSphere(const StaticMapTree& map,
                                 const G3D::Vector3& center,
                                 float radius,
                                 std::vector<SceneHit>& outOverlaps,
                                 uint32_t includeMask = 0xFFFFFFFFu,
                                 const QueryParams& params = QueryParams());

        static int OverlapBox(const StaticMapTree& map,
                              const G3D::AABox& box,
                              std::vector<SceneHit>& outOverlaps,
                              uint32_t includeMask = 0xFFFFFFFFu,
                              const QueryParams& params = QueryParams());

        // Sweeps (capsule)
        static bool SweepCapsuleSingle(const StaticMapTree& map,
                                       const CapsuleCollision::Capsule& capsuleStart,
                                       const G3D::Vector3& dir,
                                       float distance,
                                       SceneHit& outHit,
                                       uint32_t includeMask = 0xFFFFFFFFu,
                                       const QueryParams& params = QueryParams());

        static int SweepCapsuleAll(const StaticMapTree& map,
                                   const CapsuleCollision::Capsule& capsuleStart,
                                   const G3D::Vector3& dir,
                                   float distance,
                                   std::vector<SceneHit>& outHits,
                                   uint32_t includeMask = 0xFFFFFFFFu,
                                   const QueryParams& params = QueryParams());

        // Pure time-of-impact sweep (no resolution). Returns first blocking hit.
        // outHit.distance = distance traveled before impact; outHit.time = fraction along [0,distance].
        static bool SweepCapsuleTOI(const StaticMapTree& map,
                                    const CapsuleCollision::Capsule& capsuleStart,
                                    const G3D::Vector3& dir,
                                    float distance,
                                    SceneHit& outHit,
                                    uint32_t includeMask = 0xFFFFFFFFu,
                                    const QueryParams& params = QueryParams());

        // Debug helper: test all triangles of a specific instance against a world-space capsule and log any collisions.
        static int DebugTestInstanceCapsuleTriangles(const StaticMapTree& map, uint32_t instanceId, const CapsuleCollision::Capsule& capsuleWorld);
    };
}
