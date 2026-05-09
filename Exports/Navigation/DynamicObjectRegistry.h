#pragma once

#include <vector>
#include <unordered_map>
#include <map>
#include <cstdint>
#include <mutex>
#include <memory>
#include <string>
#include <utility>
#include "CapsuleCollision.h"
#include "AABox.h"
#include "Vector3.h"

namespace VMAP { class WorldModel; }

/// <summary>
/// Registry holding dynamic collision objects (elevators, doors, chests) that are
/// positioned at runtime and queried during capsule sweep / overlap tests.
///
/// Objects are keyed by GUID. Each object references a cached WorldModel loaded
/// from a .vmo file (the same format used by the vmap system for static models).
/// The model is resolved from displayId via the temp_gameobject_models index file
/// (displayId → modelName → .vmo path).
///
/// On position update, model-local triangles are transformed to world space using
/// the object's position and orientation.
///
/// Thread safety: all public methods lock a shared mutex.
/// </summary>
class DynamicObjectRegistry
{
public:
    static DynamicObjectRegistry* Instance();

    /// Initialize the displayId→model mapping from the gameobject models index file.
    /// Call this once after the vmaps base path is known.
    /// The file is typically "temp_gameobject_models" in the vmaps directory.
    bool LoadDisplayIdMapping(const std::string& vmapsBasePath);

    /// Register a dynamic object by its displayId. Loads the model .vmo file if not cached.
    /// Returns true if the model was successfully loaded and registered.
    bool RegisterObject(uint64_t guid, uint32_t entry, uint32_t displayId,
                        uint32_t mapId, float scale = 1.0f);

    /// Update the world position, orientation, and GO state of a registered object.
    /// Rebuilds world-space triangles from the cached model mesh.
    void UpdatePosition(uint64_t guid, float x, float y, float z, float orientation,
                        uint32_t goState = 0);

    /// Remove a single object by GUID.
    void Unregister(uint64_t guid);

    /// Remove all objects on a given map.
    void ClearMap(uint32_t mapId);

    /// Remove all registered objects (keeps model cache).
    void ClearAll();

    /// Query world-space triangles overlapping a world-space AABB on a given map.
    /// Appends matching triangles to outTriangles.
    void QueryTriangles(uint32_t mapId, const G3D::AABox& worldAABB,
                        std::vector<CapsuleCollision::Triangle>& outTriangles,
                        std::vector<uint32_t>* outInstanceIds = nullptr) const;

    /// Resolve a world-space contact point into local object space for a dynamic object.
    /// Returns false when the instance ID is unknown or no longer active.
    bool TryGetLocalPoint(uint32_t instanceId, const G3D::Vector3& worldPoint,
                          G3D::Vector3& outLocalPoint) const;

    /// Check whether a world-space segment intersects any registered dynamic object.
    /// Returns the nearest hit along the segment when multiple objects overlap.
    bool FindFirstIntersectingObject(
        uint32_t mapId,
        const G3D::Vector3& start,
        const G3D::Vector3& end,
        uint32_t* outInstanceId = nullptr,
        uint64_t* outGuid = nullptr,
        uint32_t* outDisplayId = nullptr) const;

    /// Returns number of registered objects.
    int Count() const;

    /// Returns number of cached model meshes.
    int CachedModelCount() const;

    /// Check if a displayId has a known model mapping.
    bool HasDisplayId(uint32_t displayId) const;

    /// Ensure an object with the given GUID is registered. If already registered,
    /// this is a no-op. If not, registers it by displayId (loads .vmo model if needed).
    /// Returns true if the object is registered (either existing or newly created).
    bool EnsureRegistered(uint64_t guid, uint32_t displayId, uint32_t mapId, float scale = 1.0f);

    // ----------------------------------------------------------------------
    // Phase 4 — variant scene-cache API.
    //
    // A "variant" is a named set of always-on or event-conditional GameObject
    // collision triangles, pre-baked per-tile by tools/SceneCacheBuilder.
    // The runtime composes the active variant set per request (the bake
    // produces base + per-variant deltas; the runtime loads the union).
    //
    // Variant triangles participate in QueryTriangles alongside per-instance
    // dynamic objects. For variant triangles, outInstanceIds reports the
    // sentinel kVariantInstanceId (TryGetLocalPoint / FindFirstIntersecting
    // return false for that sentinel — variant data has no per-instance frame).
    // ----------------------------------------------------------------------

    /// Sentinel runtime instance ID returned by QueryTriangles for variant
    /// triangles. Distinct from the per-instance ID range that starts at
    /// 0x80000001u.
    static constexpr uint32_t kVariantInstanceId = 0xFFFFFFFEu;

    /// Load one tile's variant scene-cache file produced by SceneCacheBuilder.
    /// File path is typically <dataRoot>/scene-cache/<map:03d><tileY:02d><tileX:02d>.<variant>.scenecache.
    /// Returns true on success. On format mismatch (magic/version/mapId/variantId)
    /// or read error, returns false and leaves any prior state for that tile
    /// untouched.
    bool LoadVariantSceneCache(uint32_t mapId, const std::string& variantId,
                               const std::string& sceneCachePath);

    /// Atomically remove every tile in the given (mapId, variantId) pool.
    void UnloadVariant(uint32_t mapId, const std::string& variantId);

    /// Remove every variant pool for the given map.
    void UnloadAllVariants(uint32_t mapId);

    /// Diagnostic — total triangle count across every variant pool registered
    /// for the given map.
    size_t VariantTriangleCount(uint32_t mapId) const;

private:
    DynamicObjectRegistry() = default;

    /// Cached model mesh data extracted from a .vmo file.
    /// Stored once per unique model name, shared across all instances.
    struct CachedModel
    {
        std::string modelName;
        std::vector<G3D::Vector3> localVertices;  // model-local vertices
        std::vector<uint32_t> localIndices;        // triangle index triples
        G3D::AABox localBounds;                     // model-local AABB
    };

    /// A placed dynamic object in the world.
    struct DynamicObject
    {
        uint64_t guid = 0;
        uint32_t entry = 0;
        uint32_t displayId = 0;
        uint32_t mapId = 0;
        uint32_t runtimeInstanceId = 0;
        float scale = 1.0f;
        uint32_t goState = 0;    // 0=closed/default, 1=open/active
        bool isDoorModel = false; // true if model name contains "door" (case-insensitive)

        // World transform
        float posX = 0, posY = 0, posZ = 0;
        float orientation = 0;

        // Reference to cached model data
        std::shared_ptr<CachedModel> model;

        // Pre-transformed world-space triangles (rebuilt on position change)
        std::vector<CapsuleCollision::Triangle> worldTriangles;
        G3D::AABox worldBounds;

        void RebuildWorldTriangles();
    };

    /// DisplayId mapping entry from temp_gameobject_models.
    struct DisplayIdEntry
    {
        std::string modelName;  // e.g., "Undeadelevator.m2"
        G3D::AABox bounds;      // model-local bounding box
    };

    /// One tile's slice of a variant pool — the contents of one .scenecache file.
    struct VariantTilePool
    {
        int tileX = 0;
        int tileY = 0;
        std::vector<CapsuleCollision::Triangle> triangles;
        G3D::AABox bounds;          // union of triangle vertices in this tile
        bool boundsValid = false;
    };

    /// All tiles for one (mapId, variantId) key. UnloadVariant erases this whole
    /// entry atomically; the per-tile granularity is for future variants whose
    /// footprint is sparse across the map.
    struct VariantPool
    {
        std::string variantId;
        std::map<std::pair<int, int>, VariantTilePool> tilesByXY;
        G3D::AABox poolBounds;       // union across all tiles
        bool poolBoundsValid = false;
        size_t totalTriangles = 0;
    };

    mutable std::mutex m_mutex;
    std::string m_vmapsBasePath;
    bool m_mappingLoaded = false;

    // displayId → model info (from temp_gameobject_models index)
    std::unordered_map<uint32_t, DisplayIdEntry> m_displayIdMap;

    // modelName → cached mesh data (loaded from .vmo files)
    std::unordered_map<std::string, std::shared_ptr<CachedModel>> m_modelCache;

    // guid → placed object instance
    std::unordered_map<uint64_t, DynamicObject> m_objects;
    std::unordered_map<uint32_t, uint64_t> m_instanceIdToGuid;
    uint32_t m_nextRuntimeInstanceId = 0x80000001u;

    // (mapId, variantId) → bulk variant pool (Phase 4).
    std::map<std::pair<uint32_t, std::string>, VariantPool> m_variantPools;

    /// Load and cache a model by its .vmo filename. Returns nullptr on failure.
    std::shared_ptr<CachedModel> LoadModel(const std::string& modelName);

    /// Create and cache a conservative fallback collision hull for display IDs
    /// missing from temp_gameobject_models.
    std::shared_ptr<CachedModel> CreateFallbackModel(uint32_t displayId);

    uint32_t AllocateRuntimeInstanceId();

    static DynamicObjectRegistry* s_instance;
};
