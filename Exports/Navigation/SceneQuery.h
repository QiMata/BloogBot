#pragma once

#include <vector>
#include <cstdint>
#include "Vector3.h"
#include "AABox.h"
#include "Ray.h"
#include "CapsuleCollision.h"

namespace VMAP { class StaticMapTree; class ModelInstance; class VMapManager2; }
class MapLoader; // forward declaration (global namespace)

// Unified query parameter set (from main branch - similar conceptually to UE4/UE5 query params)
// Placed in global namespace to align with SceneQuery/SceneHit architecture
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

// Feature type classification for precise TOI contact (face/edge/vertex)
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
    // TOI feature classification and physical material hints (optional)
    HitFeatureType featureType = HitFeatureType::Unknown; // What triangle feature was hit
    uint32_t physMaterialId = 0;      // Optional physical material ID (0 if unavailable)
    float staticFriction = 0.0f;      // Optional static friction coefficient (0 if unknown)
    float dynamicFriction = 0.0f;     // Optional dynamic friction coefficient (0 if unknown)
    float restitution = 0.0f;         // Optional restitution/bounciness (0 if unknown)
    // Capsule contact region along the axial segment
    enum class CapsuleRegion : uint8_t { Unknown = 0, Cap0 = 1, Side = 2, Cap1 = 3 };
    CapsuleRegion region = CapsuleRegion::Unknown;
    // Field documentation preserved from previous namespace-scoped definition
};

class SceneQuery
{
    public:
        // Diagnostic summary for capsule sweep and terrain triangles
        struct SweepResults
        {
            // Combined summary (VMAP + ADT hits appended)
            size_t hitCount = 0;
            size_t penCount = 0;
            size_t nonPenCount = 0;
            size_t walkableNonPen = 0;
            float earliestNonPen = -1.0f;
            float hitMinZ = 0.0f;
            float hitMaxZ = 0.0f;
            size_t uniqueInstanceCount = 0;

            // VMAP-only summary
            size_t vmapHitCount = 0;
            size_t vmapPenCount = 0;
            size_t vmapNonPenCount = 0;
            size_t vmapWalkableNonPen = 0;
            float vmapEarliestNonPen = -1.0f;
            float vmapHitMinZ = 0.0f;
            float vmapHitMaxZ = 0.0f;
            size_t vmapUniqueInstanceCount = 0;

            // ADT terrain diagnostics
            size_t terrainTriCount = 0;
            float terrainMinZ = 0.0f;
            float terrainMaxZ = 0.0f;
            // ADT overlap hits (penetrating) summary
            size_t adtPenetratingHitCount = 0;
            float adtHitMinZ = 0.0f;
            float adtHitMaxZ = 0.0f;

            // Selected standing placement using inflated radius (+0.02)
            bool standFound = false;
            float standZ = 0.0f;
            enum class StandSource { None, VMAP, ADT };
            StandSource standSource = StandSource::None;

            // Movement manifold built from collective triangle hits
            struct ContactPlane {
                G3D::Vector3 normal; // unit normal
                G3D::Vector3 point;  // point on plane (world)
                bool walkable;       // normal.z >= walkable threshold
                bool penetrating;    // came from start penetration
                StandSource source = StandSource::None; // origin of plane (VMAP or ADT)
                // Capsule region for the original contact(s) that formed this plane
                SceneHit::CapsuleRegion region = SceneHit::CapsuleRegion::Unknown;
            };
            std::vector<ContactPlane> planes;       // all contact planes considered
            std::vector<ContactPlane> walkablePlanes; // subset of planes that are walkable
            G3D::Vector3 slideDir;                  // projected movement direction along the primary plane
            bool slideDirValid = false;             // whether slideDir was computed
            ContactPlane primaryPlane;              // the plane chosen for movement resolution
            bool hasPrimaryPlane = false;

            // Extended manifold diagnostics
            G3D::Vector3 intersectionLineDir;       // when two walkable planes found, their intersection direction
            bool hasIntersectionLine = false;       // true if intersectionLineDir was computed
            float xyReduction = 1.0f;               // horizontal reduction factor when sliding on slope
            float suggestedXYDist = 0.0f;           // suggested XY travel distance respecting manifold projection
            int constraintIterations = 0;           // number of projection iterations that would be used
            float slopeClampThresholdZ = 0.5f; // threshold used for clamping (cos 60deg)

            // Continuous collision detection & depenetration diagnostics
            float minTOI = -1.0f;                   // minimum time of impact in [0,1]
            G3D::Vector3 depenetration;             // accumulated depenetration vector from penetrating contacts
            float depenetrationMagnitude = 0.0f;    // length of depenetration

            // Liquid diagnostics (start/end of sweep)
            bool liquidStartHasLevel = false;
            float liquidStartLevel = 0.0f;
            uint32_t liquidStartType = 0u;
            bool liquidStartFromVmap = false;
            bool liquidStartSwimming = false;

            bool liquidEndHasLevel = false;
            float liquidEndLevel = 0.0f;
            uint32_t liquidEndType = 0u;
            bool liquidEndFromVmap = false;
            bool liquidEndSwimming = false;

            // Source-specific liquid diagnostics (VMAP and ADT) at start and end
            // VMAP liquid
            bool vmapLiquidStartHasLevel = false;
            float vmapLiquidStartLevel = 0.0f;
            uint32_t vmapLiquidStartType = 0u;
            bool vmapLiquidStartSwimming = false;

            bool vmapLiquidEndHasLevel = false;
            float vmapLiquidEndLevel = 0.0f;
            uint32_t vmapLiquidEndType = 0u;
            bool vmapLiquidEndSwimming = false;

            // ADT liquid
            bool adtLiquidStartHasLevel = false;
            float adtLiquidStartLevel = 0.0f;
            uint32_t adtLiquidStartType = 0u;
            bool adtLiquidStartSwimming = false;

            bool adtLiquidEndHasLevel = false;
            float adtLiquidEndLevel = 0.0f;
            uint32_t adtLiquidEndType = 0u;
            bool adtLiquidEndSwimming = false;
        };

        // Diagnostics-driven sweep computation migrated from PhysicsEngine
        // Returns PhysicsEngine::SweepDiagnostics for the given capsule sweep parameters
        // (Requires including PhysicsEngine.h for the diagnostics type.)
        static SweepResults ComputeCapsuleSweep(
            uint32_t mapId,
            float x,
            float y,
            float z,
            float radius,
            float height,
            const G3D::Vector3& moveDir,
            float intendedDist);
        // One-time initialization for SceneQuery VMAP dependencies
        static void Initialize();

        // Inject an externally-managed MapLoader (for test contexts where
        // SceneQuery::Initialize() can't auto-discover the maps directory).
        static void SetMapLoader(MapLoader* loader) { m_mapLoader = loader; }

        // Unified liquid info result used by liquid evaluation helpers
        struct LiquidInfo
        {
            float level = 0.0f;
            uint32_t type = 0u;
            bool fromVmap = false;
            bool hasLevel = false;
            bool isSwimming = false;
        };

        // Map/VMAP helpers migrated from PhysicsEngine
        static void EnsureMapLoaded(uint32_t mapId);
        static float GetLiquidHeight(uint32_t mapId, float x, float y, float z, uint32_t& liquidType);
        static LiquidInfo EvaluateLiquidAt(uint32_t mapId, float x, float y, float z);

        // Overlaps - support optional QueryParams from main branch
        static int OverlapCapsule(const VMAP::StaticMapTree& map,
                                  const CapsuleCollision::Capsule& capsule,
                                  std::vector<SceneHit>& outOverlaps,
                                  uint32_t includeMask = 0xFFFFFFFFu,
                                  const QueryParams& params = QueryParams());

        static int OverlapSphere(const VMAP::StaticMapTree& map,
                                 const G3D::Vector3& center,
                                 float radius,
                                 std::vector<SceneHit>& outOverlaps,
                                 uint32_t includeMask = 0xFFFFFFFFu,
                                 const QueryParams& params = QueryParams());

        static int OverlapBox(const VMAP::StaticMapTree& map,
                              const G3D::AABox& box,
                              std::vector<SceneHit>& outOverlaps,
                              uint32_t includeMask = 0xFFFFFFFFu,
                              const QueryParams& params = QueryParams());

        // Refactored: accept mapId and use injected VMapManager to access map data
        // Includes optional QueryParams from main branch
        static int SweepCapsule(uint32_t mapId,
                                const CapsuleCollision::Capsule& capsuleStart,
                                const G3D::Vector3& dir,
                                float distance,
                                std::vector<SceneHit>& outHits,
                                const G3D::Vector3& playerForward,
                                const QueryParams& params = QueryParams());

        // Line of sight test combining VMAP and ADT terrain checks
        // Returns true if there is clear LOS between `from` and `to` on the given map
        static bool LineOfSight(uint32_t mapId, const G3D::Vector3& from, const G3D::Vector3& to);

        // Direct ground height query combining VMAP ray and ADT terrain interpolation.
        // Returns the highest valid ground Z at (x, y) within maxSearchDist below z.
        // More precise than capsule sweep for exact XY positions (no lateral contact offset).
        static float GetGroundZ(uint32_t mapId, float x, float y, float z, float maxSearchDist = 10.0f);

    private:
        inline static VMAP::VMapManager2* m_vmapManager = nullptr;
        inline static MapLoader* m_mapLoader = nullptr;
        inline static bool m_initialized = false;

        // BIH-based ground Z query: uses AABB overlap against the BIH tree to find
        // walkable triangles when getHeight's downward ray misses (e.g. WMO interiors).
        static float GetGroundZByBIH(const VMAP::StaticMapTree* map, float x, float y, float z, float maxSearchDist);
};
