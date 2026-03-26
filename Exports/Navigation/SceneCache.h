#pragma once

// SceneCache.h - Pre-processed collision geometry cache for fast physics loading.
// Analogous to mmaps/ (pre-computed nav meshes from vmaps/), but for collision queries.
// Stores world-space triangles + 2D spatial index, loaded from flat binary .scene files.

#include <vector>
#include <cstdint>
#include <string>
#include "CapsuleCollision.h"

// Forward declarations
namespace VMAP { class VMapManager2; }
class MapLoader;

// Triangle stored in SceneCache (world-space, pre-transformed)
struct SceneTri
{
    float ax, ay, az;
    float bx, by, bz;
    float cx, cy, cz;
    uint32_t sourceType;   // 0 = static VMAP (WMO group / standalone M2), 1 = ADT terrain, 2 = WMO doodad M2
    uint32_t instanceId;   // VMAP ModelInstance::ID, 0 for ADT
};

struct SceneTriMetadata
{
    uint32_t sourceType = 0;
    uint32_t instanceId = 0;
    uint32_t instanceFlags = 0;
    uint32_t modelFlags = 0;
    uint32_t groupFlags = 0;
    int32_t rootId = -1;
    int32_t groupId = -1;
};

// Liquid sample in the SceneCache grid
struct LiquidCell
{
    float level;
    uint32_t type;     // MAP_LIQUID_TYPE_* flags
    uint8_t flags;     // 0x01 = hasLevel, 0x02 = fromVMAP
    uint8_t pad[3];    // alignment padding
};

// SceneCache: pre-processed collision geometry with spatial index.
// Can be serialized to/from .scene files for fast loading.
class SceneCache
{
public:
    SceneCache() = default;
    ~SceneCache() = default;

    uint32_t mapId = 0;

    // --- File I/O ---

    // Save to binary .scene file
    bool SaveToFile(const char* path) const;

    // Load from binary .scene file (returns nullptr on failure)
    static SceneCache* LoadFromFile(const char* path);

    // --- Extraction from live VMAP + ADT data ---

    // Extract collision geometry for a map.
    // If bounds are provided (non-zero), only extracts geometry within XY bounds.
    // If bounds are zero/default, extracts the entire map.
    // Requires VMAP and MapLoader to be initialized (slow, one-time operation).
    struct ExtractBounds
    {
        float minX = 0, minY = 0, maxX = 0, maxY = 0;
        bool IsEmpty() const { return minX == 0 && minY == 0 && maxX == 0 && maxY == 0; }
    };

    static SceneCache* Extract(uint32_t mapId,
                               VMAP::VMapManager2* vmapMgr,
                               MapLoader* mapLoader,
                               const ExtractBounds& bounds = ExtractBounds());

    // --- Query interface (matches what SweepCapsule/GetGroundZ need) ---

    // Returns world-space triangles whose XY AABB overlaps the query box.
    // outTris receives CapsuleCollision::Triangle ready for narrow-phase tests.
    // outInstanceIds (optional) receives per-triangle instance IDs.
    // outSourceTypes (optional) receives per-triangle SceneTri::sourceType values.
    // outMetadata (optional) receives the richer extraction-time metadata when available.
    void QueryTrianglesInAABB(float minX, float minY, float maxX, float maxY,
                              std::vector<CapsuleCollision::Triangle>& outTris,
                              std::vector<uint32_t>* outInstanceIds = nullptr,
                              std::vector<uint32_t>* outSourceTypes = nullptr,
                              std::vector<SceneTriMetadata>* outMetadata = nullptr) const;

    // Ground Z query via barycentric point-in-triangle on cached geometry.
    // Returns highest Z at (x,y) that is at or below z, within maxSearchDist.
    float GetGroundZ(float x, float y, float z, float maxSearchDist) const;

    // Liquid level at (x,y) from pre-sampled grid.
    LiquidCell GetLiquidAt(float x, float y) const;
    bool HasLiquidData() const { return !m_liquidGrid.empty(); }

    // Diagnostics
    size_t GetTriangleCount() const { return m_triangles.size(); }
    size_t GetCellCount() const { return m_cellsX * m_cellsY; }
    bool HasTriangleMetadata() const { return m_triangleMetadata.size() == m_triangles.size() && !m_triangleMetadata.empty(); }
    ExtractBounds GetExtractBounds() const
    {
        ExtractBounds bounds;
        bounds.minX = m_minX;
        bounds.minY = m_minY;
        bounds.maxX = m_maxX;
        bounds.maxY = m_maxY;
        return bounds;
    }

private:
    // Collision geometry (world-space)
    std::vector<SceneTri> m_triangles;
    std::vector<SceneTriMetadata> m_triangleMetadata;

    // 2D uniform grid spatial index
    float m_cellSize = 4.0f;
    float m_minX = 0, m_minY = 0, m_maxX = 0, m_maxY = 0;
    uint32_t m_cellsX = 0, m_cellsY = 0;
    std::vector<uint32_t> m_cellStart;   // per cell: offset into m_triIndices
    std::vector<uint32_t> m_cellCount;   // per cell: count of triangles
    std::vector<uint32_t> m_triIndices;  // triangle indices sorted by cell

    // Liquid grid
    float m_liquidCellSize = 4.17f;      // matches ADT liquid resolution
    float m_liquidMinX = 0, m_liquidMinY = 0;
    uint32_t m_liquidCellsX = 0, m_liquidCellsY = 0;
    std::vector<LiquidCell> m_liquidGrid;

    // Build spatial index from m_triangles (called after extraction or load)
    void BuildSpatialIndex();

    // File format magic and version
    static constexpr uint32_t FILE_MAGIC = 0x454E4353;   // "SCNE"
    static constexpr uint32_t FILE_VERSION = 2;  // bump when scene cache format changes
};
