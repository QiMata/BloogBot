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
    uint32_t sourceType;   // 0 = VMAP (WMO/M2), 1 = ADT terrain
    uint32_t instanceId;   // VMAP ModelInstance::ID, 0 for ADT
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
    void QueryTrianglesInAABB(float minX, float minY, float maxX, float maxY,
                              std::vector<CapsuleCollision::Triangle>& outTris,
                              std::vector<uint32_t>* outInstanceIds = nullptr) const;

    // Ground Z query via barycentric point-in-triangle on cached geometry.
    // Returns highest Z at (x,y) that is at or below z, within maxSearchDist.
    float GetGroundZ(float x, float y, float z, float maxSearchDist) const;

    // Liquid level at (x,y) from pre-sampled grid.
    LiquidCell GetLiquidAt(float x, float y) const;
    bool HasLiquidData() const { return !m_liquidGrid.empty(); }

    // Diagnostics
    size_t GetTriangleCount() const { return m_triangles.size(); }
    size_t GetCellCount() const { return m_cellsX * m_cellsY; }

private:
    // Collision geometry (world-space)
    std::vector<SceneTri> m_triangles;

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
    static constexpr uint32_t FILE_VERSION = 1;
};
