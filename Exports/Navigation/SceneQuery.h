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

    // New: feature type classification for precise TOI contact (face/edge/vertex)
    enum class HitFeatureType : uint8_t { Unknown = 0, Face = 1, Edge = 2, Vertex = 3 };

    struct SceneHit
    {
        bool hit = false; // True if an intersection occurred
        float distance = 0.0f; // Ray or sweep travel distance (TOI) or overlap depth
        float time = 0.0f;     // Normalized [0,1] fraction along the sweep/raycast when hit (0 if overlap/no hit)
        float penetrationDepth = 0.0f; // Penetration depth for sweep/overlap. 0 for raycast, >0 for overlaps/sweeps.
        G3D::Vector3 normal = G3D::Vector3(0, 1, 0); // Contact normal at intersection point (world space)
        G3D::Vector3 point = G3D::Vector3(0, 0, 0); // Contact point in world space
        int triIndex = -1;       // Triangle index within model (if available)
        G3D::Vector3 barycentric = G3D::Vector3(0, 0, 0); // Barycentric coordinates of hit point on triangle (u,v,w)
        uint32_t instanceId = 0; // ModelInstance::ID
        bool startPenetrating = false; // True if the sweep started already overlapping (t=0 overlap)
        bool normalFlipped = false; // True if we flipped normal to enforce upward-facing hemisphere
        // New: TOI feature classification and physical material hints (optional)
        HitFeatureType featureType = HitFeatureType::Unknown; // What triangle feature was hit
        uint32_t physMaterialId = 0;      // Optional physical material ID (0 if unavailable)
        float staticFriction = 0.0f;      // Optional static friction coefficient (0 if unknown)
        float dynamicFriction = 0.0f;     // Optional dynamic friction coefficient (0 if unknown)
        float restitution = 0.0f;         // Optional restitution/bounciness (0 if unknown)
        //
        // Field documentation:
        // hit: True if intersection occurred
        // distance: Distance along ray/sweep to intersection, or penetration depth for overlaps
        // time: Fraction [0,1] along the query path where hit occurred
        // penetrationDepth: Depth of penetration for sweep/overlap queries
        // normal: Contact normal at intersection (world space)
        // point: Contact point (world space)
        // triIndex: Index of triangle hit in mesh
        // barycentric: Barycentric coordinates (u,v,w) of hit point on triangle
        // instanceId: ID of model instance hit
        // startPenetrating: True if query started in penetration
        // normalFlipped: True if normal was flipped to face upward
        // featureType: Triangle feature classification (face/edge/vertex)
        // physMaterialId/staticFriction/dynamicFriction/restitution: optional material data if available
    };

    class SceneQuery
    {
    public:
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

        static int SweepCapsule(const StaticMapTree& map,
                                   const CapsuleCollision::Capsule& capsuleStart,
                                   const G3D::Vector3& dir,
                                   float distance,
                                   std::vector<SceneHit>& outHits,
                                   uint32_t includeMask = 0xFFFFFFFFu,
                                   const QueryParams& params = QueryParams());
    };
}
