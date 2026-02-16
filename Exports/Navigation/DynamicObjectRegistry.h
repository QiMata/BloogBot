#pragma once

#include <vector>
#include <unordered_map>
#include <cstdint>
#include <mutex>
#include <memory>
#include <string>
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
                        std::vector<CapsuleCollision::Triangle>& outTriangles) const;

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

    mutable std::mutex m_mutex;
    std::string m_vmapsBasePath;
    bool m_mappingLoaded = false;

    // displayId → model info (from temp_gameobject_models index)
    std::unordered_map<uint32_t, DisplayIdEntry> m_displayIdMap;

    // modelName → cached mesh data (loaded from .vmo files)
    std::unordered_map<std::string, std::shared_ptr<CachedModel>> m_modelCache;

    // guid → placed object instance
    std::unordered_map<uint64_t, DynamicObject> m_objects;

    /// Load and cache a model by its .vmo filename. Returns nullptr on failure.
    std::shared_ptr<CachedModel> LoadModel(const std::string& modelName);

    static DynamicObjectRegistry* s_instance;
};
