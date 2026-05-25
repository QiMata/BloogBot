#include "TileWorker.h"
#include "MapBuilder.h"
#include "Maps/GridMapDefines.h"
#include "DetourNavMeshQuery.h"
#include "RecastAlloc.h"
#include <algorithm>
#include <array>
#include <cfloat>
#include <cmath>
#include <cstdlib>
#include <cstdio>
#include <fstream>
#include <memory>
#include <mutex>
#include <limits>
#include <set>
#include <string>
#include <unordered_set>
#include <unordered_map>
#include <vector>

using namespace VMAP;

struct GameObjectModelInfo
{
    std::string modelName;
    float minX;
    float minY;
    float minZ;
    float maxX;
    float maxY;
    float maxZ;
};

struct GameObjectSpawn
{
    uint32 mapId;
    uint32 displayId;
    float x;
    float y;
    float z;
    float orientation;
    float scale;
};

struct GameObjectBakeStats
{
    int candidateSpawns = 0;
    int bakedSpawns = 0;
    int missingModels = 0;
    int addedVertices = 0;
    int addedTriangles = 0;
};

static std::unordered_map<uint32, GameObjectModelInfo> s_gameObjectModels;
static std::unordered_map<uint32, std::vector<GameObjectSpawn>> s_gameObjectSpawnsByMap;
static std::unordered_map<uint32, std::shared_ptr<VMAP::WorldModel>> s_gameObjectWorldModelCache;
static std::unordered_set<uint32> s_gameObjectGeometryBackedDisplayIds;
static std::once_flag s_gameObjectLoadOnce;
static std::mutex s_gameObjectModelCacheMutex;

static float JsonFloatOrDefault(const nlohmann::json& obj, const char* name, float defaultValue)
{
    auto it = obj.find(name);
    if (it == obj.end() || !it->is_number())
        return defaultValue;

    return it->get<float>();
}

static uint32 JsonUIntOrDefault(const nlohmann::json& obj, const char* name, uint32 defaultValue)
{
    auto it = obj.find(name);
    if (it == obj.end() || !it->is_number_unsigned())
        return defaultValue;

    return it->get<uint32>();
}

static void LoadGameObjectModelBounds()
{
    std::ifstream file("vmaps/temp_gameobject_models", std::ios::binary);
    if (!file)
        return;

    while (file)
    {
        uint32 displayId = 0;
        uint32 pathLength = 0;
        file.read(reinterpret_cast<char*>(&displayId), sizeof(displayId));
        file.read(reinterpret_cast<char*>(&pathLength), sizeof(pathLength));
        if (!file || pathLength > 1024)
            break;

        std::string modelName(pathLength, '\0');
        if (pathLength > 0)
            file.read(&modelName[0], pathLength);

        GameObjectModelInfo model{};
        model.modelName = modelName;
        file.read(reinterpret_cast<char*>(&model.minX), sizeof(float));
        file.read(reinterpret_cast<char*>(&model.minY), sizeof(float));
        file.read(reinterpret_cast<char*>(&model.minZ), sizeof(float));
        file.read(reinterpret_cast<char*>(&model.maxX), sizeof(float));
        file.read(reinterpret_cast<char*>(&model.maxY), sizeof(float));
        file.read(reinterpret_cast<char*>(&model.maxZ), sizeof(float));
        if (!file)
            break;

        s_gameObjectModels[displayId] = model;
    }

    printf("  Loaded %zu gameobject model mappings from temp_gameobject_models\n", s_gameObjectModels.size());
}

static void LoadGameObjectSpawns()
{
    std::ifstream file("gameobject_spawns.json");
    std::string resolvedPath = "gameobject_spawns.json";
    if (!file)
    {
        const char* vmangosDataDir = std::getenv("WWOW_VMANGOS_DATA_DIR");
        if (vmangosDataDir && *vmangosDataDir)
        {
            resolvedPath = std::string(vmangosDataDir) + "/gameobject_spawns.json";
            file.open(resolvedPath.c_str());
        }
    }

    if (!file)
        return;

    nlohmann::json root;
    file >> root;

    uint32 total = 0;
    for (auto it = root.begin(); it != root.end(); ++it)
    {
        const uint32 mapId = static_cast<uint32>(std::stoul(it.key()));
        if (!it->is_array())
            continue;

        auto& spawns = s_gameObjectSpawnsByMap[mapId];
        for (const auto& item : *it)
        {
            GameObjectSpawn spawn{};
            spawn.mapId = mapId;
            spawn.displayId = JsonUIntOrDefault(item, "displayId", 0);
            spawn.x = JsonFloatOrDefault(item, "x", 0.0f);
            spawn.y = JsonFloatOrDefault(item, "y", 0.0f);
            spawn.z = JsonFloatOrDefault(item, "z", 0.0f);
            spawn.orientation = JsonFloatOrDefault(item, "o", 0.0f);
            spawn.scale = JsonFloatOrDefault(item, "s", 1.0f);
            spawns.push_back(spawn);
            ++total;
        }
    }

    printf("  Loaded %u gameobject spawns across %zu maps from %s\n", total, s_gameObjectSpawnsByMap.size(), resolvedPath.c_str());
}

static void EnsureGameObjectDataLoaded()
{
    std::call_once(s_gameObjectLoadOnce, []()
    {
        LoadGameObjectModelBounds();
        LoadGameObjectSpawns();
    });
}

static bool IntersectsRecast2D(float minX, float maxX, float minZ, float maxZ, const rcConfig& config)
{
    return maxX >= config.bmin[0]
        && minX <= config.bmax[0]
        && maxZ >= config.bmin[2]
        && minZ <= config.bmax[2];
}

static void ComputeRotatedAabb(const GameObjectSpawn& spawn, const GameObjectModelInfo& model,
                               float& minX, float& maxX, float& minY, float& maxY, float& minZ, float& maxZ)
{
    const float scale = spawn.scale <= 0.0f ? 1.0f : spawn.scale;
    const float c = std::cos(spawn.orientation);
    const float s = std::sin(spawn.orientation);

    minX = FLT_MAX;
    minY = FLT_MAX;
    maxX = -FLT_MAX;
    maxY = -FLT_MAX;

    const float xs[2] = { model.minX * scale, model.maxX * scale };
    const float ys[2] = { model.minY * scale, model.maxY * scale };
    for (float lx : xs)
    {
        for (float ly : ys)
        {
            const float wx = spawn.x + (lx * c - ly * s);
            const float wy = spawn.y + (lx * s + ly * c);
            minX = std::min(minX, wx);
            maxX = std::max(maxX, wx);
            minY = std::min(minY, wy);
            maxY = std::max(maxY, wy);
        }
    }

    minZ = spawn.z + model.minZ * scale;
    maxZ = spawn.z + model.maxZ * scale;
}

static bool HasSuffix(const std::string& value, const std::string& suffix)
{
    return value.size() >= suffix.size()
        && value.compare(value.size() - suffix.size(), suffix.size(), suffix) == 0;
}

static std::shared_ptr<WorldModel> LoadGameObjectWorldModel(uint32 displayId, const GameObjectModelInfo& model)
{
    std::lock_guard<std::mutex> lock(s_gameObjectModelCacheMutex);

    auto cached = s_gameObjectWorldModelCache.find(displayId);
    if (cached != s_gameObjectWorldModelCache.end())
        return cached->second;

    std::vector<std::string> candidates;
    if (!HasSuffix(model.modelName, ".vmo"))
        candidates.push_back("vmaps/" + model.modelName + ".vmo");
    candidates.push_back("vmaps/" + model.modelName);

    for (const std::string& candidate : candidates)
    {
        std::shared_ptr<WorldModel> worldModel(new WorldModel());
        if (worldModel->readFile(candidate))
        {
            s_gameObjectWorldModelCache[displayId] = worldModel;
            return worldModel;
        }
    }

    s_gameObjectWorldModelCache[displayId] = nullptr;
    return nullptr;
}

static bool HasBakedGameObjectGeometry(uint32 displayId)
{
    std::lock_guard<std::mutex> lock(s_gameObjectModelCacheMutex);
    return s_gameObjectGeometryBackedDisplayIds.find(displayId) != s_gameObjectGeometryBackedDisplayIds.end();
}

static void MarkBakedGameObjectGeometry(uint32 displayId)
{
    std::lock_guard<std::mutex> lock(s_gameObjectModelCacheMutex);
    s_gameObjectGeometryBackedDisplayIds.insert(displayId);
}

static bool IntersectsTileAabb(
    uint32 tileX,
    uint32 tileY,
    float minX,
    float maxX,
    float minY,
    float maxY)
{
    // Mmap tiles are indexed as (tileX <- WoW.Y, tileY <- WoW.X). Keep the
    // filter in world-space, but swap the tile axes before comparing against
    // the spawn/model AABB in WoW coordinates.
    const float tileMaxX = (32 - int(tileY)) * MMAP::GRID_SIZE;
    const float tileMinX = tileMaxX - MMAP::GRID_SIZE;
    const float tileMaxY = (32 - int(tileX)) * MMAP::GRID_SIZE;
    const float tileMinY = tileMaxY - MMAP::GRID_SIZE;

    return maxX >= tileMinX
        && minX <= tileMaxX
        && maxY >= tileMinY
        && minY <= tileMaxY;
}

static void AppendGameObjectModelGeometry(
    const GameObjectSpawn& spawn,
    const GameObjectModelInfo& modelInfo,
    WorldModel& worldModel,
    MMAP::MeshData& meshData,
    GameObjectBakeStats& stats)
{
    std::vector<GroupModel> groupModels;
    worldModel.getGroupModels(groupModels);

    const bool isM2 = modelInfo.modelName.find(".m2") != modelInfo.modelName.npos
        || modelInfo.modelName.find(".M2") != modelInfo.modelName.npos;
    const float scale = spawn.scale <= 0.0f ? 1.0f : spawn.scale;
    const float cosO = std::cos(spawn.orientation);
    const float sinO = std::sin(spawn.orientation);

    for (std::vector<GroupModel>::iterator it = groupModels.begin(); it != groupModels.end(); ++it)
    {
        std::vector<G3D::Vector3> tempVertices;
        std::vector<MeshTriangle> tempTriangles;
        WmoLiquid* liquid = nullptr;

        (*it).getMeshData(tempVertices, tempTriangles, liquid);
        if (tempVertices.empty() || tempTriangles.empty())
            continue;

        const int offset = meshData.solidVerts.size() / 3;
        for (std::vector<G3D::Vector3>::const_iterator vertex = tempVertices.begin(); vertex != tempVertices.end(); ++vertex)
        {
            const float sx = vertex->x * scale;
            const float sy = vertex->y * scale;
            const float sz = vertex->z * scale;

            const float wx = spawn.x + (sx * cosO - sy * sinO);
            const float wy = spawn.y + (sx * sinO + sy * cosO);
            const float wz = spawn.z + sz;

            meshData.solidVerts.append(wx);
            meshData.solidVerts.append(wz);
            meshData.solidVerts.append(wy);
        }

        MMAP::TerrainBuilder::copyIndices(tempTriangles, meshData.solidTris, offset, isM2);

        stats.addedVertices += int(tempVertices.size());
        stats.addedTriangles += int(tempTriangles.size());
    }
}

static GameObjectBakeStats BakeGameObjectModelsIntoMesh(uint32 mapId, uint32 tileX, uint32 tileY, MMAP::MeshData& meshData)
{
    EnsureGameObjectDataLoaded();

    GameObjectBakeStats stats;
    auto mapIt = s_gameObjectSpawnsByMap.find(mapId);
    if (mapIt == s_gameObjectSpawnsByMap.end())
        return stats;

    const int firstGameObjectTriangle = meshData.solidTris.size() / 3;

    for (const GameObjectSpawn& spawn : mapIt->second)
    {
        auto modelIt = s_gameObjectModels.find(spawn.displayId);
        if (modelIt == s_gameObjectModels.end())
            continue;

        float minX, maxX, minY, maxY, minZ, maxZ;
        ComputeRotatedAabb(spawn, modelIt->second, minX, maxX, minY, maxY, minZ, maxZ);
        if (!IntersectsTileAabb(tileX, tileY, minX, maxX, minY, maxY))
            continue;

        ++stats.candidateSpawns;
        std::shared_ptr<WorldModel> worldModel = LoadGameObjectWorldModel(spawn.displayId, modelIt->second);
        if (!worldModel)
        {
            ++stats.missingModels;
            continue;
        }

        const int trianglesBefore = stats.addedTriangles;
        AppendGameObjectModelGeometry(spawn, modelIt->second, *worldModel, meshData, stats);
        if (stats.addedTriangles > trianglesBefore)
        {
            ++stats.bakedSpawns;
            MarkBakedGameObjectGeometry(spawn.displayId);
        }
    }

    const int lastGameObjectTriangle = meshData.solidTris.size() / 3;
    meshData.AddModelTriangleRange(firstGameObjectTriangle, lastGameObjectTriangle);
    return stats;
}

static int MarkGameObjectAreas(rcContext* context, uint32 mapId, uint32 tileX, uint32 tileY,
                               const rcConfig& config, rcCompactHeightfield& compactHeightfield)
{
    EnsureGameObjectDataLoaded();

    auto mapIt = s_gameObjectSpawnsByMap.find(mapId);
    if (mapIt == s_gameObjectSpawnsByMap.end())
        return 0;

    int marked = 0;
    for (const GameObjectSpawn& spawn : mapIt->second)
    {
        auto boundsIt = s_gameObjectModels.find(spawn.displayId);
        if (boundsIt == s_gameObjectModels.end())
            continue;

        if (HasBakedGameObjectGeometry(spawn.displayId))
            continue;

        float minX, maxX, minY, maxY, minZ, maxZ;
        ComputeRotatedAabb(spawn, boundsIt->second, minX, maxX, minY, maxY, minZ, maxZ);

        // Recast/Detour stores WoW world positions as (Y, Z, X). Gameobject
        // spawn data is exported in server/world coordinates (X, Y, Z), so
        // the horizontal AABB must be swizzled before marking compact spans.
        const float recastMinX = minY;
        const float recastMaxX = maxY;
        const float recastMinZ = minX;
        const float recastMaxZ = maxX;
        if (!IntersectsRecast2D(recastMinX, recastMaxX, recastMinZ, recastMaxZ, config))
            continue;

        const float padding = 0.05f;
        float recastMin[3] = { recastMinX - padding, minZ - padding, recastMinZ - padding };
        float recastMax[3] = { recastMaxX + padding, maxZ + padding, recastMaxZ + padding };
        rcMarkBoxArea(context, recastMin, recastMax, RC_NULL_AREA, compactHeightfield);
        ++marked;
    }

    return marked;
}

// [WWoW-DIVERGENCE] 2026-05-07: --debug-heightfield X,Y diagnostic helpers.
// Dump per-stage span flags for one heightfield column. Both helpers are
// no-ops unless cx,cy are non-negative AND inside the heightfield bounds.
// The output goes to stdout, prefixed with [DBG-HF] for grep-friendliness.
inline static void dumpHeightfieldColumn(const char* stage, int cx, int cy, const rcHeightfield& hf)
{
    if (cx < 0 || cy < 0 || cx >= hf.width || cy >= hf.height)
        return;
    rcSpan* s = hf.spans[cx + cy * hf.width];
    if (!s)
    {
        printf("[DBG-HF] stage=%-16s col=(%d,%d) EMPTY\n", stage, cx, cy);
        return;
    }
    int idx = 0;
    for (; s; s = s->next, ++idx)
    {
        const float worldBot = hf.bmin[1] + (float)s->smin * hf.ch;
        const float worldTop = hf.bmin[1] + (float)s->smax * hf.ch;
        printf("[DBG-HF] stage=%-16s col=(%d,%d) span#=%d bot=%u top=%u area=%u (worldY=%.3f..%.3f)\n",
               stage, cx, cy, idx, (unsigned)s->smin, (unsigned)s->smax, (unsigned)s->area, worldBot, worldTop);
    }
}

inline static void dumpCompactHeightfieldColumn(const char* stage, int cx, int cy, const rcCompactHeightfield& chf)
{
    if (cx < 0 || cy < 0 || cx >= chf.width || cy >= chf.height)
        return;
    const rcCompactCell& cell = chf.cells[cx + cy * chf.width];
    if (cell.count == 0)
    {
        printf("[DBG-HF] stage=%-16s col=(%d,%d) EMPTY (chf)\n", stage, cx, cy);
        return;
    }
    for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
    {
        const rcCompactSpan& s = chf.spans[i];
        const unsigned char area = chf.areas[i];
        const float worldBot = chf.bmin[1] + (float)s.y * chf.ch;
        const float worldTop = chf.bmin[1] + (float)(s.y + s.h) * chf.ch;
        printf("[DBG-HF] stage=%-16s col=(%d,%d) span#=%u bot=%u top=%u area=%u (worldY=%.3f..%.3f)\n",
               stage, cx, cy, (unsigned)(i - cell.index), (unsigned)s.y, (unsigned)(s.y + s.h), (unsigned)area, worldBot, worldTop);
    }
}

struct DebugStageCrop
{
    bool enabled = false;
    float minRecastX = 0.0f;
    float maxRecastX = 0.0f;
    float minRecastZ = 0.0f;
    float maxRecastZ = 0.0f;
    float minHeight = 0.0f;
    float maxHeight = 0.0f;
};

static DebugStageCrop ParseDebugStageCropWow(const json& wowCrop)
{
    DebugStageCrop crop;
    if (!wowCrop.is_array() || wowCrop.size() != 6)
        return crop;

    const float minWowX = std::min(wowCrop[0].get<float>(), wowCrop[3].get<float>());
    const float maxWowX = std::max(wowCrop[0].get<float>(), wowCrop[3].get<float>());
    const float minWowY = std::min(wowCrop[1].get<float>(), wowCrop[4].get<float>());
    const float maxWowY = std::max(wowCrop[1].get<float>(), wowCrop[4].get<float>());
    const float minWowZ = std::min(wowCrop[2].get<float>(), wowCrop[5].get<float>());
    const float maxWowZ = std::max(wowCrop[2].get<float>(), wowCrop[5].get<float>());

    // Generator/Recast horizontal coordinates are stored as (WoW Y, WoW X).
    crop.minRecastX = minWowY;
    crop.maxRecastX = maxWowY;
    crop.minRecastZ = minWowX;
    crop.maxRecastZ = maxWowX;
    crop.minHeight = minWowZ;
    crop.maxHeight = maxWowZ;
    crop.enabled = true;
    return crop;
}

static std::vector<DebugStageCrop> ReadDebugStageCrops(const json& jsonTileConfig)
{
    std::vector<DebugStageCrop> crops;

    auto singleCrop = jsonTileConfig.find("debugStageCropWow");
    if (singleCrop != jsonTileConfig.end())
    {
        DebugStageCrop crop = ParseDebugStageCropWow(*singleCrop);
        if (crop.enabled)
            crops.push_back(crop);
    }

    auto multiCrop = jsonTileConfig.find("debugStageCropsWow");
    if (multiCrop != jsonTileConfig.end() && multiCrop->is_array())
    {
        for (const json& wowCrop : *multiCrop)
        {
            DebugStageCrop crop = ParseDebugStageCropWow(wowCrop);
            if (crop.enabled)
                crops.push_back(crop);
        }
    }

    return crops;
}

static bool IntersectsDebugCrop2D(float minX, float maxX, float minZ, float maxZ, const DebugStageCrop& crop)
{
    if (!crop.enabled)
        return false;

    return maxX >= crop.minRecastX
        && minX <= crop.maxRecastX
        && maxZ >= crop.minRecastZ
        && minZ <= crop.maxRecastZ;
}

static bool IntersectsDebugCropHeight(float minHeight, float maxHeight, const DebugStageCrop& crop)
{
    return maxHeight >= crop.minHeight && minHeight <= crop.maxHeight;
}

static FILE* OpenDebugCsv(const char* fileName, const char* header)
{
    bool exists = false;
    {
        std::ifstream probe(fileName, std::ios::binary);
        exists = probe.good();
    }

    FILE* file = fopen(fileName, exists ? "ab" : "wb");
    if (file && !exists)
        fprintf(file, "%s\n", header);
    return file;
}

static void ResetDebugStageFiles(uint32 mapID, uint32 tileX, uint32 tileY)
{
    char fileName[256];
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_heightfield_spans.csv", mapID, tileY, tileX);
    std::remove(fileName);
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_compact_spans.csv", mapID, tileY, tileX);
    std::remove(fileName);
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_contours.csv", mapID, tileY, tileX);
    std::remove(fileName);
}

static void WriteHeightfieldStageCsv(const char* stage, uint32 mapID, uint32 tileX, uint32 tileY,
                                     int subX, int subY, const std::vector<DebugStageCrop>& crops, const rcHeightfield& hf)
{
    if (crops.empty())
        return;

    char fileName[256];
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_heightfield_spans.csv", mapID, tileY, tileX);
    FILE* file = OpenDebugCsv(fileName, "stage,map,tileX,tileY,subX,subY,cropIndex,cellX,cellY,spanIndex,recastX,recastZ,minHeight,maxHeight,area");
    if (!file)
        return;

    for (size_t cropIndex = 0; cropIndex < crops.size(); ++cropIndex)
    {
        const DebugStageCrop& crop = crops[cropIndex];
        for (int y = 0; y < hf.height; ++y)
        {
            const float cellMinZ = hf.bmin[2] + y * hf.cs;
            const float cellMaxZ = cellMinZ + hf.cs;
            for (int x = 0; x < hf.width; ++x)
            {
                const float cellMinX = hf.bmin[0] + x * hf.cs;
                const float cellMaxX = cellMinX + hf.cs;
                if (!IntersectsDebugCrop2D(cellMinX, cellMaxX, cellMinZ, cellMaxZ, crop))
                    continue;

                int spanIndex = 0;
                for (rcSpan* span = hf.spans[x + y * hf.width]; span; span = span->next, ++spanIndex)
                {
                    const float minHeight = hf.bmin[1] + (float)span->smin * hf.ch;
                    const float maxHeight = hf.bmin[1] + (float)span->smax * hf.ch;
                    if (!IntersectsDebugCropHeight(minHeight, maxHeight, crop))
                        continue;

                    fprintf(file, "%s,%u,%u,%u,%d,%d,%zu,%d,%d,%d,%f,%f,%f,%f,%u\n",
                            stage, mapID, tileX, tileY, subX, subY, cropIndex, x, y, spanIndex,
                            cellMinX + hf.cs * 0.5f, cellMinZ + hf.cs * 0.5f,
                            minHeight, maxHeight, (unsigned)span->area);
                }
            }
        }
    }

    fclose(file);
}

static void WriteCompactHeightfieldStageCsv(const char* stage, uint32 mapID, uint32 tileX, uint32 tileY,
                                            int subX, int subY, const std::vector<DebugStageCrop>& crops, const rcCompactHeightfield& chf)
{
    if (crops.empty())
        return;

    char fileName[256];
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_compact_spans.csv", mapID, tileY, tileX);
    FILE* file = OpenDebugCsv(fileName, "stage,map,tileX,tileY,subX,subY,cropIndex,cellX,cellY,spanIndex,recastX,recastZ,minHeight,maxHeight,area,region,distance,connections");
    if (!file)
        return;

    for (size_t cropIndex = 0; cropIndex < crops.size(); ++cropIndex)
    {
        const DebugStageCrop& crop = crops[cropIndex];
        for (int y = 0; y < chf.height; ++y)
        {
            const float cellMinZ = chf.bmin[2] + y * chf.cs;
            const float cellMaxZ = cellMinZ + chf.cs;
            for (int x = 0; x < chf.width; ++x)
            {
                const float cellMinX = chf.bmin[0] + x * chf.cs;
                const float cellMaxX = cellMinX + chf.cs;
                if (!IntersectsDebugCrop2D(cellMinX, cellMaxX, cellMinZ, cellMaxZ, crop))
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    const rcCompactSpan& span = chf.spans[i];
                    const float minHeight = chf.bmin[1] + (float)span.y * chf.ch;
                    const float maxHeight = chf.bmin[1] + (float)(span.y + span.h) * chf.ch;
                    if (!IntersectsDebugCropHeight(minHeight, maxHeight, crop))
                        continue;

                    const unsigned short distance = chf.dist ? chf.dist[i] : 0;
                    fprintf(file, "%s,%u,%u,%u,%d,%d,%zu,%d,%d,%u,%f,%f,%f,%f,%u,%u,%u,%u\n",
                            stage, mapID, tileX, tileY, subX, subY, cropIndex, x, y, (unsigned)(i - cell.index),
                            cellMinX + chf.cs * 0.5f, cellMinZ + chf.cs * 0.5f,
                            minHeight, maxHeight, (unsigned)chf.areas[i], (unsigned)span.reg, (unsigned)distance, (unsigned)span.con);
                }
            }
        }
    }

    fclose(file);
}

static void WriteContourStageCsv(uint32 mapID, uint32 tileX, uint32 tileY, int subX, int subY,
                                 const std::vector<DebugStageCrop>& crops, const rcContourSet& contours)
{
    if (crops.empty())
        return;

    char fileName[256];
    sprintf(fileName, "meshes/map%03u%02u%02u_stage_contours.csv", mapID, tileY, tileX);
    FILE* file = OpenDebugCsv(fileName, "map,tileX,tileY,subX,subY,cropIndex,contourIndex,vertexIndex,recastX,recastY,recastZ,area,region");
    if (!file)
        return;

    for (size_t cropIndex = 0; cropIndex < crops.size(); ++cropIndex)
    {
        const DebugStageCrop& crop = crops[cropIndex];
        for (int i = 0; i < contours.nconts; ++i)
        {
            const rcContour& contour = contours.conts[i];
            for (int v = 0; v < contour.nverts; ++v)
            {
                const int* cv = &contour.verts[v * 4];
                const float recastX = contours.bmin[0] + cv[0] * contours.cs;
                const float recastY = contours.bmin[1] + cv[1] * contours.ch;
                const float recastZ = contours.bmin[2] + cv[2] * contours.cs;
                if (!IntersectsDebugCrop2D(recastX, recastX, recastZ, recastZ, crop)
                    || !IntersectsDebugCropHeight(recastY, recastY, crop))
                    continue;

                fprintf(file, "%u,%u,%u,%d,%d,%zu,%d,%d,%f,%f,%f,%u,%u\n",
                        mapID, tileX, tileY, subX, subY, cropIndex, i, v,
                        recastX, recastY, recastZ, (unsigned)contour.area, (unsigned)contour.reg);
            }
        }
    }

    fclose(file);
}

inline static void calcTriNormal(const float* v0, const float* v1, const float* v2, float* norm)
{
    float e0[3], e1[3];
    rcVsub(e0, v1, v0);
    rcVsub(e1, v2, v0);
    rcVcross(norm, e0, e1);
    rcVnormalize(norm);
}

inline static unsigned int nextPow2(unsigned int v)
{
    v--;
    v |= v >> 1;
    v |= v >> 2;
    v |= v >> 4;
    v |= v >> 8;
    v |= v >> 16;
    v++;
    return v;
}

inline static unsigned int ilog2(unsigned int v)
{
    unsigned int r;
    unsigned int shift;
    r = (v > 0xffff) << 4; v >>= r;
    shift = (v > 0xff) << 3; v >>= shift; r |= shift;
    shift = (v > 0xf) << 2; v >>= shift; r |= shift;
    shift = (v > 0x3) << 1; v >>= shift; r |= shift;
    r |= (v >> 1);
    return r;
}

static void filterRemoveUselessAreas(rcHeightfield& filter)
{
    const int w = filter.width;
    const int h = filter.height;
    for (int y = 0; y < h; ++y)
        for (int x = 0; x < w; ++x)
            for (rcSpan* span = filter.spans[x + y * w]; span; span = span->next)
                switch (span->area)
                {
                case AREA_GROUND_MODEL:
                    span->area = AREA_GROUND;
                    break;
                case AREA_STEEP_SLOPE_MODEL:
                    span->area = AREA_STEEP_SLOPE;
                    break;
                }
}

static void filterWalkableLowHeightSpansWith(rcHeightfield& filter, rcHeightfield& out, int min, int max)
{
    const int w = out.width;
    const int h = out.height;

    // Remove walkable flag from spans which do not have enough
    // space above them for the agent to stand there.
    for (int y = 0; y < h; ++y)
    {
        for (int x = 0; x < w; ++x)
        {
            for (rcSpan* spanOut = out.spans[x + y * w]; spanOut; spanOut = spanOut->next)
                for (rcSpan* spanFilter = filter.spans[x + y * w]; spanFilter; spanFilter = spanFilter->next)
                    if (spanOut->area == AREA_GROUND) // No steep slopes here.
                    {
                        const int bot = (int)(spanOut->smax);
                        const int top = (int)(spanFilter->smin);
                        if ((top - bot) <= max && (top - bot) >= 0)
                        {
                            if ((top - bot) >= min)
                                spanOut->area = spanFilter->area;
                            else if (spanFilter->area == AREA_WATER)
                                spanOut->area = AREA_WATER_TRANSITION;
                        }
                    }
        }
    }
}

static bool IsModelArea(int area)
{
    switch (area)
    {
    case AREA_GROUND_MODEL:
    case AREA_STEEP_SLOPE_MODEL:
        return true;
    }
    return false;
}

// PFS-OVERHAUL-006 / Phase 6 (2026-05-07) — two per-tile knobs:
//
//   treatOobNeighborAsCliff (default true):
//     Legacy behavior treated an out-of-tile-bounds neighbor as a span at
//     -infinity, forcing minNeighborHeight to a value that always triggers
//     ledge rejection. That's correct for terrain whose far-side really does
//     drop off (Thousand Needles bridges), but wrong for thin shelf terrain
//     bordering OOB water (the OG zeppelin dock). Setting this to false
//     leaves OOB neighbors as "no constraint": the dock cell's in-tile
//     neighbors decide its fate. Only opt in for tiles that are not at the
//     real map perimeter (water/voids around an interior tile are fine).
//
//   mixedAreaUsesTerrainClimb (default false):
//     Legacy behavior used the tighter ~1.2y model-transition climb whenever
//     a span's neighbors mixed terrain and model areas. Decorations on top
//     of the dock (Burningmidtree, Cage03, Stormwindcrate, Darnassusstreetlamp)
//     flip IsModelArea on the neighbor side and force the dock-edge cell to
//     fail ledge filtering even though the agent walks ON terrain. Setting
//     this to true keeps using terrain climb (1.8y) when the CURRENT span is
//     terrain — agent feet are on the dock, decorations sit above. Wall-top
//     model spans keep the legacy transition climb (the guard checks the
//     current span's area, not just the transition flag).
static void filterLedgeSpans(const int walkableHeight, const int walkableClimbTransition, const int walkableClimbTerrain,
    rcHeightfield& heightfield,
    bool treatOobNeighborAsCliff = true,
    bool mixedAreaUsesTerrainClimb = false)
{
    const int w = heightfield.width;
    const int h = heightfield.height;
    const int MAX_HEIGHT = 0xffff;

    for (int y = 0; y < h; ++y)
    {
        for (int x = 0; x < w; ++x)
        {
            for (rcSpan* span = heightfield.spans[x + y * w]; span; span = span->next)
            {
                // Skip non walkable spans.
                if (span->area == RC_NULL_AREA)
                    continue;

                const int bot = (int)(span->smax);
                const int top = span->next ? (int)(span->next->smin) : MAX_HEIGHT;

                // Find neighbours minimum height.
                int minNeighborHeight = MAX_HEIGHT;

                // Min and max height of accessible neighbours.
                int accessibleNeighborMinHeight = span->smax;
                int accessibleNeighborMaxHeight = span->smax;
                bool hasAllNbTerrain = true;
                bool hasAllNbModel = true;

                for (int dir = 0; dir < 4; ++dir)
                {
                    int dx = x + rcGetDirOffsetX(dir);
                    int dy = y + rcGetDirOffsetY(dir);
                    // Skip neighbours which are out of bounds.
                    if (dx < 0 || dy < 0 || dx >= w || dy >= h)
                    {
                        // Legacy: treat OOB as a span at -infinity (Thousand Needles bridges
                        // safety). When treatOobNeighborAsCliff=false the OOB direction
                        // contributes nothing — the cell's in-tile neighbors decide the cliff
                        // verdict. Used for thin shelf terrain like the OG zeppelin dock that
                        // borders OOB water columns.
                        if (treatOobNeighborAsCliff)
                            minNeighborHeight = rcMin(minNeighborHeight, -walkableClimbTerrain - bot);
                        continue;
                    }

                    // From minus infinity to the first span.
                    rcSpan* neighborSpan = heightfield.spans[dx + dy * w];
                    int nbot = -walkableClimbTerrain;
                    int ntop = neighborSpan ? (int)neighborSpan->smin : MAX_HEIGHT;
                    // Skip neighbour if the gap between the spans is too small.
                    if (rcMin(top, ntop) - rcMax(bot, nbot) > walkableHeight)
                        minNeighborHeight = rcMin(minNeighborHeight, nbot - bot);

                    // Rest of the spans.
                    for (neighborSpan = heightfield.spans[dx + dy * w]; neighborSpan; neighborSpan = neighborSpan->next)
                    {
                        if (neighborSpan->area == RC_NULL_AREA)
                            continue;
                        nbot = (int)neighborSpan->smax;
                        ntop = neighborSpan->next ? (int)neighborSpan->next->smin : MAX_HEIGHT;
                        // Skip neightbour if the gap between the spans is too small.
                        if (rcMin(top, ntop) - rcMax(bot, nbot) > walkableHeight)
                        {
                            minNeighborHeight = rcMin(minNeighborHeight, nbot - bot);
                            // Find min/max accessible neighbour height.
                            if (rcAbs(nbot - bot) <= walkableClimbTerrain)
                            {
                                if (nbot < accessibleNeighborMinHeight) accessibleNeighborMinHeight = nbot;
                                if (nbot > accessibleNeighborMaxHeight) accessibleNeighborMaxHeight = nbot;
                                if (!IsModelArea(neighborSpan->area))
                                    hasAllNbModel = false;
                                else
                                    hasAllNbTerrain = false;
                            }
                        }
                    }
                }

                // The current span is close to a ledge if the drop to any
                // neighbour span is less than the walkableClimb.
                bool modelToTerrainTransition = (IsModelArea(span->area) && !hasAllNbModel) || (!IsModelArea(span->area) && !hasAllNbTerrain);
                int currentMaxClimb = walkableClimbTerrain;
                // Model -> Terrain or Terrain -> Model
                // Legacy: tighter transition climb whenever neighbors mix areas. With
                // mixedAreaUsesTerrainClimb=true, a TERRAIN-current span keeps the
                // 1.8y terrain climb even when neighbors include models — the agent
                // walks on terrain, decorations only sit above. Wall-top model spans
                // (IsModelArea(span->area)) still get the tighter transition climb.
                if (modelToTerrainTransition && !(mixedAreaUsesTerrainClimb && !IsModelArea(span->area)))
                    currentMaxClimb = walkableClimbTransition;
                if (minNeighborHeight < -currentMaxClimb)
                    span->area = RC_NULL_AREA;


                // If the difference between all neighbours is too large,
                // we are at steep slope, mark the span as it
                else if ((accessibleNeighborMaxHeight - accessibleNeighborMinHeight) > currentMaxClimb)
                {
                    if (modelToTerrainTransition)
                        span->area = RC_NULL_AREA;
                    else
                        span->area = AREA_STEEP_SLOPE;
                }
            }
        }
    }
}

namespace
{
constexpr float STEEP_SURFACE_PRUNE_EPSILON_DEGREES = 1.0f;
constexpr float STEEP_SURFACE_PRUNE_MIN_TRIANGLE_NORMAL = 1e-5f;
constexpr float STEEP_SURFACE_PRUNE_MIN_STEEP_AREA_RATIO = 0.6f;
constexpr float MIXED_WALL_POLY_MIN_STEEP_AREA_RATIO = 0.08f;
constexpr float MIXED_WALL_POLY_MIN_Z_RANGE = 1.0f;
constexpr float MIXED_WALL_POLY_MIN_EDGE_LENGTH_2D = 6.0f;
constexpr float MIXED_WALL_POLY_MIN_AREA_2D = 8.0f;
constexpr float MIXED_WALL_POLY_CLIMB_OVERSHOOT = 0.15f;
constexpr float DETOUR_FINAL_CULL_MAX_SLOPE_DEGREES = 50.0f;
constexpr float SHADOWED_LEDGE_MAX_AREA_2D = 20.0f;
constexpr float SHADOWED_LEDGE_MAX_EDGE_2D = 7.0f;
constexpr float SHADOWED_LEDGE_MAX_Z_RANGE = 1.5f;
constexpr float SHADOWED_LEDGE_MIN_VERTICAL_GAP = 1.0f;
constexpr float SHADOWED_LEDGE_MAX_VERTICAL_GAP = 4.0f;
constexpr float SHADOWED_LEDGE_MIN_OVERLAP_AREA_2D = 0.5f;
constexpr float SHADOWED_LEDGE_MIN_OVERLAP_RATIO = 0.35f;
constexpr float SHADOWED_LEDGE_COMPONENT_MAX_AREA_2D = 80.0f;
constexpr float SHADOWED_LEDGE_COMPONENT_MAX_Z_RANGE = 2.5f;
constexpr float SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_AREA_2D = 10.0f;
constexpr float SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_EDGE_2D = 6.5f;
constexpr float SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_Z_RANGE = 0.75f;
constexpr int SHADOWED_LEDGE_FALLBACK_MAX_CULLS_PER_COMPONENT = 4;
constexpr float SHADOWED_POCKET_MAX_AREA_2D = 40.0f;
constexpr float SHADOWED_POCKET_MAX_EDGE_2D = 10.0f;
constexpr float SHADOWED_POCKET_MAX_Z_RANGE = 5.5f;
constexpr float SHADOWED_POCKET_MIN_VERTICAL_GAP = 0.15f;
constexpr float SHADOWED_POCKET_MAX_VERTICAL_GAP = 5.0f;
constexpr float SHADOWED_POCKET_MIN_OVERLAP_AREA_2D = 0.75f;
constexpr float SHADOWED_POCKET_MIN_OVERLAP_RATIO = 0.40f;
constexpr float SHADOWED_POCKET_COMPONENT_MAX_AREA_2D = 150.0f;
constexpr float SHADOWED_POCKET_COMPONENT_MAX_Z_RANGE = 6.0f;
constexpr float STACKED_SLIVER_MAX_Z_RANGE = 1.0f;
constexpr float STACKED_SLIVER_MAX_AREA_2D = 18.0f;
constexpr float STACKED_SLIVER_MAX_EDGE_2D = 7.0f;
constexpr float STACKED_SLIVER_MIN_OVERLAP_AREA_2D = 0.5f;
constexpr float STACKED_SLIVER_MIN_OVERLAP_RATIO = 0.35f;
constexpr float STACKED_SLIVER_MIN_SUPPORT_DELTA = -0.25f;
constexpr float STACKED_SLIVER_MAX_CLOSEST_SUPPORT_DISTANCE_2D = 1.0f;
constexpr float ANCHOR_POLY_STACK_MIN_SUPPORT_DELTA = 0.0f;
constexpr float ANCHOR_POLY_STACK_SUPPORT_EPSILON = 0.05f;
constexpr float ANCHOR_LOWER_FRINGE_MAX_TOP_DELTA = 0.05f;
constexpr float ANCHOR_UPPER_FRINGE_MIN_TOP_DELTA = 0.15f;
constexpr float STACKED_SLIVER_MAX_SUPPORT_DELTA = 0.5f;
constexpr float ANCHOR_ROUTE_TARGET_NEAREST_XY_EXTENT = 5.0f;
constexpr float ANCHOR_ROUTE_TARGET_NEAREST_Z_EXTENT = 10.0f;
constexpr float ANCHOR_ROUTE_TARGET_MAX_HEIGHT_ABOVE = 3.0f;
constexpr int ANCHOR_ROUTE_QUERY_MAX_NODES = 32768;
constexpr int ANCHOR_ROUTE_MAX_PATH_POLYS = 4096;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_MIN_AVERAGE_SLOPE_DEGREES = 35.0f;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_MAX_EDGE_2D = 4.5f;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_MAX_AREA_2D = 6.0f;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_MAX_Z_RANGE = 3.25f;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_AREA_2D = 18.0f;
constexpr float OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_Z_RANGE = 3.5f;

struct OffMeshAnchorSteepTrimSettings
{
    float minAverageSlopeDegrees = OFFMESH_ANCHOR_STEEP_TRIM_MIN_AVERAGE_SLOPE_DEGREES;
    float maxEdge2D = OFFMESH_ANCHOR_STEEP_TRIM_MAX_EDGE_2D;
    float maxArea2D = OFFMESH_ANCHOR_STEEP_TRIM_MAX_AREA_2D;
    float maxZRange = OFFMESH_ANCHOR_STEEP_TRIM_MAX_Z_RANGE;
    float componentMaxArea2D = OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_AREA_2D;
    float componentMaxZRange = OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_Z_RANGE;
};

struct DetailPolyDiagnostics
{
    float totalSurfaceArea = 0.0f;
    float steepSurfaceArea = 0.0f;
    float weightedUpComponent = 0.0f;
    float maxSlopeDegrees = 0.0f;
    float minY = std::numeric_limits<float>::max();
    float maxY = -std::numeric_limits<float>::max();
    float maxEdge2D = 0.0f;
    float horizontalArea2D = 0.0f;
};

static int GetPolyVertCount(const rcPolyMesh& mesh, const int polyIndex)
{
    const unsigned short* poly = &mesh.polys[polyIndex * mesh.nvp * 2];
    int vertCount = 0;
    while (vertCount < mesh.nvp && poly[vertCount] != RC_MESH_NULL_IDX)
        ++vertCount;
    return vertCount;
}

static void GetPolyWorldVert(const rcPolyMesh& mesh, const int polyIndex, const int polyVertIndex, float out[3])
{
    const unsigned short* poly = &mesh.polys[polyIndex * mesh.nvp * 2];
    const unsigned short vertIndex = poly[polyVertIndex];
    const unsigned short* vert = &mesh.verts[vertIndex * 3];
    out[0] = mesh.bmin[0] + vert[0] * mesh.cs;
    out[1] = mesh.bmin[1] + vert[1] * mesh.ch;
    out[2] = mesh.bmin[2] + vert[2] * mesh.cs;
}

static const float* GetDetailTriVertex(const rcPolyMesh& mesh, const rcPolyMeshDetail& detailMesh, const int polyIndex,
    const unsigned int* detail, const int polyVertCount, const unsigned char triVertIndex, float tempStorage[3])
{
    if (triVertIndex < polyVertCount)
    {
        GetPolyWorldVert(mesh, polyIndex, triVertIndex, tempStorage);
        return tempStorage;
    }

    return &detailMesh.verts[(detail[0] + (triVertIndex - polyVertCount)) * 3];
}

static float GetPolyMaxEdge2D(const rcPolyMesh& mesh, const int polyIndex, const int polyVertCount)
{
    float maxEdge2D = 0.0f;
    float vertexA[3];
    float vertexB[3];
    for (int vertexIndex = 0; vertexIndex < polyVertCount; ++vertexIndex)
    {
        GetPolyWorldVert(mesh, polyIndex, vertexIndex, vertexA);
        GetPolyWorldVert(mesh, polyIndex, (vertexIndex + 1) % polyVertCount, vertexB);
        const float deltaX = vertexB[0] - vertexA[0];
        const float deltaZ = vertexB[2] - vertexA[2];
        maxEdge2D = rcMax(maxEdge2D, sqrtf(deltaX * deltaX + deltaZ * deltaZ));
    }

    return maxEdge2D;
}

static float GetPolyHorizontalArea2D(const rcPolyMesh& mesh, const int polyIndex, const int polyVertCount)
{
    float twiceArea = 0.0f;
    float vertexA[3];
    float vertexB[3];
    for (int vertexIndex = 0; vertexIndex < polyVertCount; ++vertexIndex)
    {
        GetPolyWorldVert(mesh, polyIndex, vertexIndex, vertexA);
        GetPolyWorldVert(mesh, polyIndex, (vertexIndex + 1) % polyVertCount, vertexB);
        twiceArea += (vertexA[0] * vertexB[2]) - (vertexB[0] * vertexA[2]);
    }

    return fabsf(twiceArea) * 0.5f;
}

static DetailPolyDiagnostics AnalyzeDetailPoly(const rcPolyMesh& mesh, const rcPolyMeshDetail& detailMesh,
    const int polyIndex, const float maxSlopeDegrees)
{
    DetailPolyDiagnostics diagnostics;
    const int polyVertCount = GetPolyVertCount(mesh, polyIndex);
    if (polyVertCount < 3)
        return diagnostics;

    diagnostics.maxEdge2D = GetPolyMaxEdge2D(mesh, polyIndex, polyVertCount);
    diagnostics.horizontalArea2D = GetPolyHorizontalArea2D(mesh, polyIndex, polyVertCount);

    float polyVertex[3];
    for (int vertexIndex = 0; vertexIndex < polyVertCount; ++vertexIndex)
    {
        GetPolyWorldVert(mesh, polyIndex, vertexIndex, polyVertex);
        diagnostics.minY = rcMin(diagnostics.minY, polyVertex[1]);
        diagnostics.maxY = rcMax(diagnostics.maxY, polyVertex[1]);
    }

    if (polyIndex >= detailMesh.nmeshes)
        return diagnostics;

    const unsigned int* detail = &detailMesh.meshes[polyIndex * 4];
    const unsigned int triBase = detail[2];
    const unsigned int triCount = detail[3];

    for (unsigned int triIndex = 0; triIndex < triCount; ++triIndex)
    {
        const unsigned char* tri = &detailMesh.tris[(triBase + triIndex) * 4];
        float tempStorage[3][3];
        const float* triVerts[3];
        for (int vertIndex = 0; vertIndex < 3; ++vertIndex)
            triVerts[vertIndex] = GetDetailTriVertex(mesh, detailMesh, polyIndex, detail, polyVertCount, tri[vertIndex], tempStorage[vertIndex]);

        float edgeAB[3];
        float edgeAC[3];
        float normal[3];
        rcVsub(edgeAB, triVerts[1], triVerts[0]);
        rcVsub(edgeAC, triVerts[2], triVerts[0]);
        rcVcross(normal, edgeAB, edgeAC);

        const float normalLength = sqrtf(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
        if (normalLength <= STEEP_SURFACE_PRUNE_MIN_TRIANGLE_NORMAL)
            continue;

        const float area = normalLength * 0.5f;
        const float upComponent = rcClamp(fabsf(normal[1]) / normalLength, 0.0f, 1.0f);
        const float slopeDegrees = acosf(upComponent) * (180.0f / RC_PI);
        diagnostics.totalSurfaceArea += area;
        diagnostics.weightedUpComponent += upComponent * area;
        diagnostics.maxSlopeDegrees = rcMax(diagnostics.maxSlopeDegrees, slopeDegrees);
        if (slopeDegrees > maxSlopeDegrees + STEEP_SURFACE_PRUNE_EPSILON_DEGREES)
            diagnostics.steepSurfaceArea += area;

        for (int vertIndex = 0; vertIndex < 3; ++vertIndex)
        {
            diagnostics.minY = rcMin(diagnostics.minY, triVerts[vertIndex][1]);
            diagnostics.maxY = rcMax(diagnostics.maxY, triVerts[vertIndex][1]);
        }
    }

    if (diagnostics.minY == std::numeric_limits<float>::max())
    {
        diagnostics.minY = 0.0f;
        diagnostics.maxY = 0.0f;
    }

    return diagnostics;
}

static bool ShouldCullSuspiciousPoly(const rcPolyMesh& mesh, const rcPolyMeshDetail& detailMesh, const int polyIndex,
    const float maxSlopeDegrees, const float maxClimbWorld)
{
    const DetailPolyDiagnostics diagnostics = AnalyzeDetailPoly(mesh, detailMesh, polyIndex, maxSlopeDegrees);
    if (diagnostics.totalSurfaceArea <= 0.0f)
        return false;

    const float averageUpComponent = rcClamp(diagnostics.weightedUpComponent / diagnostics.totalSurfaceArea, 0.0f, 1.0f);
    const float averageSlopeDegrees = acosf(averageUpComponent) * (180.0f / RC_PI);
    const float steepAreaRatio = diagnostics.steepSurfaceArea / diagnostics.totalSurfaceArea;
    const float verticalRange = diagnostics.maxY - diagnostics.minY;

    const bool overwhelminglySteep =
        averageSlopeDegrees > maxSlopeDegrees + STEEP_SURFACE_PRUNE_EPSILON_DEGREES ||
        steepAreaRatio >= STEEP_SURFACE_PRUNE_MIN_STEEP_AREA_RATIO;

    const bool mixedTallWallApron =
        steepAreaRatio >= MIXED_WALL_POLY_MIN_STEEP_AREA_RATIO &&
        diagnostics.maxSlopeDegrees > maxSlopeDegrees + STEEP_SURFACE_PRUNE_EPSILON_DEGREES &&
        verticalRange >= rcMax(MIXED_WALL_POLY_MIN_Z_RANGE, maxClimbWorld + MIXED_WALL_POLY_CLIMB_OVERSHOOT) &&
        (diagnostics.maxEdge2D >= MIXED_WALL_POLY_MIN_EDGE_LENGTH_2D ||
            diagnostics.horizontalArea2D >= MIXED_WALL_POLY_MIN_AREA_2D);

    return overwhelminglySteep || mixedTallWallApron;
}

static int CullSuspiciousMixedWallPolys(rcPolyMesh& mesh, const rcPolyMeshDetail& detailMesh,
    const float maxSlopeDegrees, const float maxClimbWorld)
{
    int culled = 0;
    for (int polyIndex = 0; polyIndex < mesh.npolys; ++polyIndex)
    {
        if (mesh.areas[polyIndex] != AREA_GROUND)
            continue;

        if (!ShouldCullSuspiciousPoly(mesh, detailMesh, polyIndex, maxSlopeDegrees, maxClimbWorld))
            continue;

        mesh.areas[polyIndex] = AREA_NONE;
        mesh.flags[polyIndex] = 0;
        ++culled;
    }

    return culled;
}

struct DetourPolyDiagnostics
{
    float totalSurfaceArea = 0.0f;
    float steepSurfaceArea = 0.0f;
    float weightedUpComponent = 0.0f;
    float maxSlopeDegrees = 0.0f;
    float minX = std::numeric_limits<float>::max();
    float minY = std::numeric_limits<float>::max();
    float minZ = std::numeric_limits<float>::max();
    float maxX = -std::numeric_limits<float>::max();
    float maxY = -std::numeric_limits<float>::max();
    float maxZ = -std::numeric_limits<float>::max();
    float maxEdge2D = 0.0f;
    float horizontalArea2D = 0.0f;
};

static float GetDetourPolyMaxEdge2D(const dtMeshTile& tile, const dtPoly& poly)
{
    float maxEdge2D = 0.0f;
    for (int vertexIndex = 0; vertexIndex < poly.vertCount; ++vertexIndex)
    {
        const float* vertexA = &tile.verts[poly.verts[vertexIndex] * 3];
        const float* vertexB = &tile.verts[poly.verts[(vertexIndex + 1) % poly.vertCount] * 3];
        const float deltaX = vertexB[0] - vertexA[0];
        const float deltaZ = vertexB[2] - vertexA[2];
        maxEdge2D = rcMax(maxEdge2D, sqrtf(deltaX * deltaX + deltaZ * deltaZ));
    }

    return maxEdge2D;
}

static float GetDetourPolyHorizontalArea2D(const dtMeshTile& tile, const dtPoly& poly)
{
    float twiceArea = 0.0f;
    for (int vertexIndex = 0; vertexIndex < poly.vertCount; ++vertexIndex)
    {
        const float* vertexA = &tile.verts[poly.verts[vertexIndex] * 3];
        const float* vertexB = &tile.verts[poly.verts[(vertexIndex + 1) % poly.vertCount] * 3];
        twiceArea += (vertexA[0] * vertexB[2]) - (vertexB[0] * vertexA[2]);
    }

    return fabsf(twiceArea) * 0.5f;
}

static const float* GetDetourDetailTriVertex(const dtMeshTile& tile, const dtPoly& poly, const dtPolyDetail& detail,
    const unsigned char triVertIndex, float tempStorage[3])
{
    if (triVertIndex < poly.vertCount)
        return &tile.verts[poly.verts[triVertIndex] * 3];

    const unsigned int detailVertIndex = detail.vertBase + (triVertIndex - poly.vertCount);
    if (detailVertIndex < (unsigned int)tile.header->detailVertCount)
        return &tile.detailVerts[detailVertIndex * 3];

    tempStorage[0] = 0.0f;
    tempStorage[1] = 0.0f;
    tempStorage[2] = 0.0f;
    return tempStorage;
}

static DetourPolyDiagnostics AnalyzeDetourPoly(const dtMeshTile& tile, const int polyIndex, const float maxSlopeDegrees)
{
    DetourPolyDiagnostics diagnostics;
    if (!tile.header || polyIndex < 0 || polyIndex >= tile.header->polyCount)
        return diagnostics;

    const dtPoly& poly = tile.polys[polyIndex];
    if (poly.vertCount < 3)
        return diagnostics;

    diagnostics.maxEdge2D = GetDetourPolyMaxEdge2D(tile, poly);
    diagnostics.horizontalArea2D = GetDetourPolyHorizontalArea2D(tile, poly);

    for (int vertexIndex = 0; vertexIndex < poly.vertCount; ++vertexIndex)
    {
        const float* polyVertex = &tile.verts[poly.verts[vertexIndex] * 3];
        diagnostics.minX = rcMin(diagnostics.minX, polyVertex[0]);
        diagnostics.minY = rcMin(diagnostics.minY, polyVertex[1]);
        diagnostics.minZ = rcMin(diagnostics.minZ, polyVertex[2]);
        diagnostics.maxX = rcMax(diagnostics.maxX, polyVertex[0]);
        diagnostics.maxY = rcMax(diagnostics.maxY, polyVertex[1]);
        diagnostics.maxZ = rcMax(diagnostics.maxZ, polyVertex[2]);
    }

    if (polyIndex >= tile.header->detailMeshCount)
        return diagnostics;

    const dtPolyDetail& detail = tile.detailMeshes[polyIndex];
    for (unsigned int triIndex = 0; triIndex < detail.triCount; ++triIndex)
    {
        const unsigned char* tri = &tile.detailTris[(detail.triBase + triIndex) * 4];
        float tempStorage[3][3];
        const float* triVerts[3];
        for (int vertIndex = 0; vertIndex < 3; ++vertIndex)
            triVerts[vertIndex] = GetDetourDetailTriVertex(tile, poly, detail, tri[vertIndex], tempStorage[vertIndex]);

        float edgeAB[3];
        float edgeAC[3];
        float normal[3];
        rcVsub(edgeAB, triVerts[1], triVerts[0]);
        rcVsub(edgeAC, triVerts[2], triVerts[0]);
        rcVcross(normal, edgeAB, edgeAC);

        const float normalLength = sqrtf(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
        if (normalLength <= STEEP_SURFACE_PRUNE_MIN_TRIANGLE_NORMAL)
            continue;

        const float area = normalLength * 0.5f;
        const float upComponent = rcClamp(fabsf(normal[1]) / normalLength, 0.0f, 1.0f);
        const float slopeDegrees = acosf(upComponent) * (180.0f / RC_PI);
        diagnostics.totalSurfaceArea += area;
        diagnostics.weightedUpComponent += upComponent * area;
        diagnostics.maxSlopeDegrees = rcMax(diagnostics.maxSlopeDegrees, slopeDegrees);
        if (slopeDegrees > maxSlopeDegrees + STEEP_SURFACE_PRUNE_EPSILON_DEGREES)
            diagnostics.steepSurfaceArea += area;

        for (int vertIndex = 0; vertIndex < 3; ++vertIndex)
        {
            diagnostics.minX = rcMin(diagnostics.minX, triVerts[vertIndex][0]);
            diagnostics.minY = rcMin(diagnostics.minY, triVerts[vertIndex][1]);
            diagnostics.minZ = rcMin(diagnostics.minZ, triVerts[vertIndex][2]);
            diagnostics.maxX = rcMax(diagnostics.maxX, triVerts[vertIndex][0]);
            diagnostics.maxY = rcMax(diagnostics.maxY, triVerts[vertIndex][1]);
            diagnostics.maxZ = rcMax(diagnostics.maxZ, triVerts[vertIndex][2]);
        }
    }

    if (diagnostics.minY == std::numeric_limits<float>::max())
    {
        diagnostics.minX = 0.0f;
        diagnostics.minY = 0.0f;
        diagnostics.minZ = 0.0f;
        diagnostics.maxX = 0.0f;
        diagnostics.maxY = 0.0f;
        diagnostics.maxZ = 0.0f;
    }

    return diagnostics;
}

static float GetAverageSlopeDegrees(const DetourPolyDiagnostics& diagnostics)
{
    if (diagnostics.totalSurfaceArea <= 0.0f)
        return 0.0f;

    const float averageUpComponent = rcClamp(diagnostics.weightedUpComponent / diagnostics.totalSurfaceArea, 0.0f, 1.0f);
    return acosf(averageUpComponent) * (180.0f / RC_PI);
}

static bool ShouldCullDetourPoly(const DetourPolyDiagnostics& diagnostics, const float maxClimbWorld)
{
    if (diagnostics.totalSurfaceArea <= 0.0f)
        return false;

    const float averageSlopeDegrees = GetAverageSlopeDegrees(diagnostics);
    const float steepAreaRatio = diagnostics.steepSurfaceArea / diagnostics.totalSurfaceArea;
    const float verticalRange = diagnostics.maxY - diagnostics.minY;

    const bool overwhelminglySteep =
        averageSlopeDegrees > DETOUR_FINAL_CULL_MAX_SLOPE_DEGREES + STEEP_SURFACE_PRUNE_EPSILON_DEGREES ||
        steepAreaRatio >= STEEP_SURFACE_PRUNE_MIN_STEEP_AREA_RATIO;

    const bool mixedTallWallApron =
        steepAreaRatio >= MIXED_WALL_POLY_MIN_STEEP_AREA_RATIO &&
        diagnostics.maxSlopeDegrees > DETOUR_FINAL_CULL_MAX_SLOPE_DEGREES + STEEP_SURFACE_PRUNE_EPSILON_DEGREES &&
        verticalRange >= rcMax(MIXED_WALL_POLY_MIN_Z_RANGE, maxClimbWorld + MIXED_WALL_POLY_CLIMB_OVERSHOOT) &&
        (diagnostics.maxEdge2D >= MIXED_WALL_POLY_MIN_EDGE_LENGTH_2D ||
            diagnostics.horizontalArea2D >= MIXED_WALL_POLY_MIN_AREA_2D);

    return overwhelminglySteep || mixedTallWallApron;
}

static float GetDetourBoundsOverlapArea2D(const DetourPolyDiagnostics& candidate, const DetourPolyDiagnostics& other)
{
    const float overlapX = rcMin(candidate.maxX, other.maxX) - rcMax(candidate.minX, other.minX);
    if (overlapX <= 0.0f)
        return 0.0f;

    const float overlapZ = rcMin(candidate.maxZ, other.maxZ) - rcMax(candidate.minZ, other.minZ);
    if (overlapZ <= 0.0f)
        return 0.0f;

    return overlapX * overlapZ;
}

static float GetDetourBoundsGap2D(const DetourPolyDiagnostics& candidate, const DetourPolyDiagnostics& other)
{
    float gapX = 0.0f;
    if (candidate.maxX < other.minX)
        gapX = other.minX - candidate.maxX;
    else if (other.maxX < candidate.minX)
        gapX = candidate.minX - other.maxX;

    float gapZ = 0.0f;
    if (candidate.maxZ < other.minZ)
        gapZ = other.minZ - candidate.maxZ;
    else if (other.maxZ < candidate.minZ)
        gapZ = candidate.minZ - other.maxZ;

    return sqrtf(gapX * gapX + gapZ * gapZ);
}

static bool IsShadowedLedgeCandidate(const DetourPolyDiagnostics& diagnostics)
{
    return diagnostics.totalSurfaceArea > 0.0f &&
        diagnostics.horizontalArea2D <= SHADOWED_LEDGE_MAX_AREA_2D &&
        diagnostics.maxEdge2D <= SHADOWED_LEDGE_MAX_EDGE_2D &&
        (diagnostics.maxY - diagnostics.minY) <= SHADOWED_LEDGE_MAX_Z_RANGE;
}

static bool IsShadowedPocketCandidate(const DetourPolyDiagnostics& diagnostics)
{
    return diagnostics.totalSurfaceArea > 0.0f &&
        diagnostics.horizontalArea2D <= SHADOWED_POCKET_MAX_AREA_2D &&
        diagnostics.maxEdge2D <= SHADOWED_POCKET_MAX_EDGE_2D &&
        (diagnostics.maxY - diagnostics.minY) <= SHADOWED_POCKET_MAX_Z_RANGE;
}

static bool IsOffMeshAnchorSteepTrimCandidate(const DetourPolyDiagnostics& diagnostics,
    const OffMeshAnchorSteepTrimSettings& settings)
{
    return diagnostics.totalSurfaceArea > 0.0f &&
        GetAverageSlopeDegrees(diagnostics) >= settings.minAverageSlopeDegrees &&
        diagnostics.maxEdge2D <= settings.maxEdge2D &&
        diagnostics.horizontalArea2D <= settings.maxArea2D &&
        (diagnostics.maxY - diagnostics.minY) <= settings.maxZRange;
}

static bool IsWalkableLandPoly(const dtPoly& poly)
{
    return poly.getType() == DT_POLYTYPE_GROUND &&
        poly.flags != 0 &&
        (poly.flags & NAV_GROUND) != 0 &&
        poly.getArea() != AREA_NONE;
}

struct AnchorPolyStackCoord
{
    float wowX = 0.0f;
    float wowY = 0.0f;
    float wowZ = 0.0f;
};

struct AnchorRouteTarget
{
    AnchorPolyStackCoord source;
    AnchorPolyStackCoord target;
    std::string label;
};

struct AnchorPolyStackProbeResult
{
    bool posOverPoly = false;
    bool hasClosestPoint = false;
    bool hasSurfaceZ = false;
    float closestPoint[3] = { 0.0f, 0.0f, 0.0f };
    float closestDistance2D = std::numeric_limits<float>::max();
    float surfaceZ = std::numeric_limits<float>::quiet_NaN();
};

struct AnchorSourceSupportProbe
{
    AnchorPolyStackCoord anchor;
    bool found = false;
    float supportY = 0.0f;
    float distance2D = std::numeric_limits<float>::max();
    int triIndex = -1;
    MMAP::MeshTriangleSource source = MMAP::MeshTriangleSource::Terrain;
    bool projectedInside = false;
    bool borrowed = false;
    AnchorPolyStackCoord borrowedFrom;
};

struct AnchorSupportBandTuning
{
    float supportFloorSlackBelow = 0.20f;
    float supportFloorSlackAbove = 0.35f;
    float competingLowerFloorMinDrop = 0.25f;
};

struct Point2DXZ
{
    float x = 0.0f;
    float z = 0.0f;
};

static float GetAnchorSupportFloorMinY(const AnchorSourceSupportProbe& support, const AnchorSupportBandTuning& tuning)
{
    return support.supportY - tuning.supportFloorSlackBelow;
}

static float GetAnchorSupportFloorMaxY(const AnchorSourceSupportProbe& support,
    const float supportZTolerance, const AnchorSupportBandTuning& tuning)
{
    return support.supportY + std::max(tuning.supportFloorSlackAbove, supportZTolerance);
}

static bool IsAnchorCompetingLowerFloor(const float spanFloor,
    const AnchorSourceSupportProbe& support, const AnchorSupportBandTuning& tuning)
{
    return spanFloor < support.supportY - tuning.competingLowerFloorMinDrop;
}

static std::string FormatAnchorStageId(const AnchorPolyStackCoord& anchor)
{
    char buffer[96];
    sprintf(buffer, "%.3f,%.3f,%.3f", anchor.wowX, anchor.wowY, anchor.wowZ);
    return std::string(buffer);
}

static std::string FormatPolyRefHex(const dtPolyRef polyRef)
{
    char buffer[32];
    sprintf(buffer, "0x%llX", static_cast<unsigned long long>(polyRef));
    return std::string(buffer);
}

static bool AreAnchorCoordsEquivalent(const AnchorPolyStackCoord& lhs, const AnchorPolyStackCoord& rhs,
    const float epsilon = 0.01f)
{
    return fabsf(lhs.wowX - rhs.wowX) <= epsilon &&
        fabsf(lhs.wowY - rhs.wowY) <= epsilon &&
        fabsf(lhs.wowZ - rhs.wowZ) <= epsilon;
}

static bool PointInPolygonXZ(const std::vector<Point2DXZ>& polygon, const float x, const float z)
{
    if (polygon.size() < 3)
        return false;

    bool inside = false;
    for (size_t i = 0, j = polygon.size() - 1; i < polygon.size(); j = i++)
    {
        const Point2DXZ& vi = polygon[i];
        const Point2DXZ& vj = polygon[j];
        const bool intersects =
            ((vi.z > z) != (vj.z > z)) &&
            (x < (vj.x - vi.x) * (z - vi.z) / ((vj.z - vi.z) + 1.0e-6f) + vi.x);
        if (intersects)
            inside = !inside;
    }

    return inside;
}

static bool TryProjectPointToTriangleXZ(const float a[3], const float b[3], const float c[3],
    const float px, const float pz, float& projectedY)
{
    const float v0x = b[0] - a[0];
    const float v0z = b[2] - a[2];
    const float v1x = c[0] - a[0];
    const float v1z = c[2] - a[2];
    const float v2x = px - a[0];
    const float v2z = pz - a[2];

    const float denom = v0x * v1z - v1x * v0z;
    if (fabsf(denom) < 1.0e-6f)
        return false;

    const float invDenom = 1.0f / denom;
    const float v = (v2x * v1z - v1x * v2z) * invDenom;
    const float w = (v0x * v2z - v2x * v0z) * invDenom;
    const float u = 1.0f - v - w;
    if (u < -1.0e-4f || v < -1.0e-4f || w < -1.0e-4f)
        return false;

    projectedY = u * a[1] + v * b[1] + w * c[1];
    return true;
}

static void ClosestPointOnSegmentXZ(const float ax, const float az, const float ay,
    const float bx, const float bz, const float by,
    const float px, const float pz,
    float& outX, float& outZ, float& outY)
{
    const float abX = bx - ax;
    const float abZ = bz - az;
    const float len2 = abX * abX + abZ * abZ;
    float t = 0.0f;
    if (len2 > 1.0e-6f)
    {
        t = ((px - ax) * abX + (pz - az) * abZ) / len2;
        if (t < 0.0f)
            t = 0.0f;
        else if (t > 1.0f)
            t = 1.0f;
    }

    outX = ax + abX * t;
    outZ = az + abZ * t;
    outY = ay + (by - ay) * t;
}

static bool TryResolveTriangleSupportY(const float a[3], const float b[3], const float c[3],
    const float anchorX, const float anchorZ, const float xyExtent,
    float& supportY, float& distance2D, bool& projectedInside)
{
    projectedInside = false;
    supportY = 0.0f;
    distance2D = std::numeric_limits<float>::max();

    float projectedY = 0.0f;
    if (TryProjectPointToTriangleXZ(a, b, c, anchorX, anchorZ, projectedY))
    {
        projectedInside = true;
        supportY = projectedY;
        distance2D = 0.0f;
        return true;
    }

    float bestX = 0.0f;
    float bestZ = 0.0f;
    float bestY = 0.0f;
    float bestDistance2D = std::numeric_limits<float>::max();

    auto testEdge = [&](const float* p0, const float* p1)
    {
        float edgeX = 0.0f;
        float edgeZ = 0.0f;
        float edgeY = 0.0f;
        ClosestPointOnSegmentXZ(p0[0], p0[2], p0[1], p1[0], p1[2], p1[1], anchorX, anchorZ, edgeX, edgeZ, edgeY);
        const float dx = edgeX - anchorX;
        const float dz = edgeZ - anchorZ;
        const float dist = sqrtf(dx * dx + dz * dz);
        if (dist < bestDistance2D)
        {
            bestDistance2D = dist;
            bestX = edgeX;
            bestZ = edgeZ;
            bestY = edgeY;
        }
    };

    testEdge(a, b);
    testEdge(b, c);
    testEdge(c, a);

    if (bestDistance2D > xyExtent)
        return false;

    supportY = bestY;
    distance2D = bestDistance2D;
    return true;
}

static const char* MeshTriangleSourceName(const MMAP::MeshTriangleSource source)
{
    switch (source)
    {
    case MMAP::MeshTriangleSource::VMap:
        return "vmap";
    case MMAP::MeshTriangleSource::GameObject:
        return "gameobject";
    case MMAP::MeshTriangleSource::Terrain:
    default:
        return "terrain";
    }
}

static std::vector<AnchorSourceSupportProbe> ResolveAnchorSourceSupportProbes(
    const MMAP::MeshData& meshData,
    const float* tVerts,
    const int* tTris,
    const unsigned char* areas,
    const int tTriCount,
    const float xyExtent,
    const float supportZTolerance,
    const std::vector<AnchorPolyStackCoord>& anchorCoords,
    const bool allowNeighborBorrow,
    const bool logDiagnostics)
{
    std::vector<AnchorSourceSupportProbe> probes;
    probes.reserve(anchorCoords.size());
    if (!tVerts || !tTris || !areas || anchorCoords.empty())
        return probes;

    constexpr float kSupportFloorBelowSlack = 0.35f;
    constexpr float kSupportFloorAboveSlack = 0.75f;
    const float borrowRadius2D = xyExtent + 0.25f;

    for (const AnchorPolyStackCoord& anchor : anchorCoords)
    {
        AnchorSourceSupportProbe best;
        best.anchor = anchor;
        const float anchorRecastX = anchor.wowY;
        const float anchorRecastZ = anchor.wowX;
        float bestScore = FLT_MAX;

        for (int triIndex = 0; triIndex < tTriCount; ++triIndex)
        {
            const unsigned char area = areas[triIndex];
            if (area != AREA_GROUND && area != AREA_GROUND_MODEL)
                continue;

            const int* tri = &tTris[triIndex * 3];
            const float* a = &tVerts[tri[0] * 3];
            const float* b = &tVerts[tri[1] * 3];
            const float* c = &tVerts[tri[2] * 3];

            const float minX = std::min(a[0], std::min(b[0], c[0]));
            const float maxX = std::max(a[0], std::max(b[0], c[0]));
            const float minZ = std::min(a[2], std::min(b[2], c[2]));
            const float maxZ = std::max(a[2], std::max(b[2], c[2]));
            if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
                maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
            {
                continue;
            }

            float supportY = 0.0f;
            float distance2D = std::numeric_limits<float>::max();
            bool projectedInside = false;
            if (!TryResolveTriangleSupportY(a, b, c, anchorRecastX, anchorRecastZ, xyExtent, supportY, distance2D, projectedInside))
                continue;

            const float delta = supportY - anchor.wowZ;
            if (delta < -kSupportFloorBelowSlack || delta > supportZTolerance + kSupportFloorAboveSlack)
                continue;

            const float deltaPenalty = delta >= 0.0f ? delta : 4.0f + fabsf(delta);
            const float insidePenalty = projectedInside ? 0.0f : 1.5f;
            const float score = deltaPenalty * 4.0f + distance2D + insidePenalty;
            if (!best.found || score < bestScore)
            {
                best.found = true;
                best.supportY = supportY;
                best.distance2D = distance2D;
                best.triIndex = triIndex;
                best.source = meshData.SourceForTriangle(triIndex);
                best.projectedInside = projectedInside;
                bestScore = score;
            }
        }

        probes.push_back(best);
    }

    if (allowNeighborBorrow)
    {
        for (AnchorSourceSupportProbe& probe : probes)
        {
            if (probe.found)
                continue;

            const AnchorSourceSupportProbe* bestBorrow = nullptr;
            float bestBorrowScore = FLT_MAX;
            for (const AnchorSourceSupportProbe& candidate : probes)
            {
                if (!candidate.found || AreAnchorCoordsEquivalent(candidate.anchor, probe.anchor))
                    continue;

                const float deltaX = candidate.anchor.wowX - probe.anchor.wowX;
                const float deltaY = candidate.anchor.wowY - probe.anchor.wowY;
                const float anchorDistance2D = sqrtf(deltaX * deltaX + deltaY * deltaY);
                if (anchorDistance2D > borrowRadius2D)
                    continue;

                const float supportDelta = candidate.supportY - probe.anchor.wowZ;
                if (supportDelta < -kSupportFloorBelowSlack || supportDelta > supportZTolerance + kSupportFloorAboveSlack)
                    continue;

                const float insidePenalty = candidate.projectedInside ? 0.0f : 0.5f;
                const float score = anchorDistance2D + candidate.distance2D + insidePenalty;
                if (!bestBorrow || score < bestBorrowScore)
                {
                    bestBorrow = &candidate;
                    bestBorrowScore = score;
                }
            }

            if (!bestBorrow)
                continue;

            probe.found = true;
            probe.supportY = bestBorrow->supportY;
            probe.distance2D = bestBorrow->distance2D +
                sqrtf((bestBorrow->anchor.wowX - probe.anchor.wowX) * (bestBorrow->anchor.wowX - probe.anchor.wowX) +
                    (bestBorrow->anchor.wowY - probe.anchor.wowY) * (bestBorrow->anchor.wowY - probe.anchor.wowY));
            probe.triIndex = bestBorrow->triIndex;
            probe.source = bestBorrow->source;
            probe.projectedInside = false;
            probe.borrowed = true;
            probe.borrowedFrom = bestBorrow->anchor;
        }
    }

    if (logDiagnostics)
    {
        for (const AnchorSourceSupportProbe& probe : probes)
        {
            if (probe.found)
            {
                if (probe.borrowed)
                {
                    printf("[SRC-ANCHOR-SUPPORT] anchor=(%.3f,%.3f,%.3f) supportY=%.3f delta=%.3f tri=%d source=%s dist2D=%.3f inside=%d borrowed=1 borrowedFrom=(%.3f,%.3f,%.3f)\n",
                        probe.anchor.wowX, probe.anchor.wowY, probe.anchor.wowZ,
                        probe.supportY, probe.supportY - probe.anchor.wowZ, probe.triIndex,
                        meshData.SourceNameForTriangle(probe.triIndex),
                        probe.distance2D, probe.projectedInside ? 1 : 0,
                        probe.borrowedFrom.wowX, probe.borrowedFrom.wowY, probe.borrowedFrom.wowZ);
                }
                else
                {
                    printf("[SRC-ANCHOR-SUPPORT] anchor=(%.3f,%.3f,%.3f) supportY=%.3f delta=%.3f tri=%d source=%s dist2D=%.3f inside=%d\n",
                        probe.anchor.wowX, probe.anchor.wowY, probe.anchor.wowZ,
                        probe.supportY, probe.supportY - probe.anchor.wowZ, probe.triIndex,
                        meshData.SourceNameForTriangle(probe.triIndex),
                        probe.distance2D, probe.projectedInside ? 1 : 0);
                }
            }
            else
            {
                printf("[SRC-ANCHOR-SUPPORT] anchor=(%.3f,%.3f,%.3f) supportY=none\n",
                    probe.anchor.wowX, probe.anchor.wowY, probe.anchor.wowZ);
            }
        }
    }

    return probes;
}

static bool IntersectsAnchorWindow(const DetourPolyDiagnostics& diagnostics,
    const float detourAnchorX, const float detourAnchorY, const float detourAnchorZ,
    const float xyExtent, const float zExtent)
{
    if (diagnostics.maxX < detourAnchorX - xyExtent || diagnostics.minX > detourAnchorX + xyExtent)
        return false;
    if (diagnostics.maxZ < detourAnchorZ - xyExtent || diagnostics.minZ > detourAnchorZ + xyExtent)
        return false;
    if (diagnostics.maxY < detourAnchorY - zExtent || diagnostics.minY > detourAnchorY + zExtent)
        return false;

    return true;
}

static bool IntersectsAnchorSupportBand(const DetourPolyDiagnostics& diagnostics,
    const float supportBandMinY, const float supportBandMaxY)
{
    return diagnostics.maxY >= supportBandMinY && diagnostics.minY <= supportBandMaxY;
}

static AnchorPolyStackProbeResult ProbeAnchorPolyAtCoord(dtNavMeshQuery& query, const dtPolyRef polyRef, const float detourPos[3])
{
    AnchorPolyStackProbeResult result;

    float closest[3] = { 0.0f, 0.0f, 0.0f };
    bool posOverPoly = false;
    const dtStatus closestStatus = query.closestPointOnPoly(polyRef, detourPos, closest, &posOverPoly);
    if (dtStatusSucceed(closestStatus))
    {
        result.hasClosestPoint = true;
        rcVcopy(result.closestPoint, closest);
        const float deltaX = closest[0] - detourPos[0];
        const float deltaZ = closest[2] - detourPos[2];
        result.closestDistance2D = sqrtf(deltaX * deltaX + deltaZ * deltaZ);
        if (posOverPoly)
            result.posOverPoly = true;
    }

    float height = 0.0f;
    dtStatus heightStatus = query.getPolyHeight(polyRef, detourPos, &height);
    if (dtStatusFailed(heightStatus) && dtStatusSucceed(closestStatus))
        heightStatus = query.getPolyHeight(polyRef, closest, &height);

    if (dtStatusSucceed(heightStatus))
    {
        result.hasSurfaceZ = true;
        result.surfaceZ = height;
    }

    return result;
}

static bool TryGetAnchorProbeSupportSurfaceZ(const AnchorPolyStackProbeResult& probe, float& surfaceZ, bool& usedClosestFallback,
    const bool allowNearestPointFallbackBeyondSliver = false)
{
    if (probe.hasSurfaceZ)
    {
        surfaceZ = probe.surfaceZ;
        usedClosestFallback = false;
        return true;
    }

    if (probe.hasClosestPoint &&
        (allowNearestPointFallbackBeyondSliver || probe.closestDistance2D <= STACKED_SLIVER_MAX_CLOSEST_SUPPORT_DISTANCE_2D))
    {
        surfaceZ = probe.closestPoint[1];
        usedClosestFallback = true;
        return true;
    }

    usedClosestFallback = false;
    return false;
}

static bool IsAnchorSupportSurfaceDeltaValid(const float surfaceDelta, const float supportZTolerance)
{
    return surfaceDelta >= -ANCHOR_POLY_STACK_SUPPORT_EPSILON &&
        surfaceDelta <= supportZTolerance;
}

static const AnchorSourceSupportProbe* FindAnchorSourceSupportProbe(
    const std::vector<AnchorSourceSupportProbe>& sourceSupports,
    const AnchorPolyStackCoord& anchor)
{
    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (fabsf(support.anchor.wowX - anchor.wowX) > 0.001f ||
            fabsf(support.anchor.wowY - anchor.wowY) > 0.001f ||
            fabsf(support.anchor.wowZ - anchor.wowZ) > 0.001f)
        {
            continue;
        }

        return &support;
    }

    return nullptr;
}

static std::vector<AnchorPolyStackCoord> ParseAnchorCoords(const json& config, const char* key)
{
    std::vector<AnchorPolyStackCoord> coords;

    auto it = config.find(key);
    if (it == config.end() || !it->is_array())
        return coords;

    for (const json& entry : *it)
    {
        if (!entry.is_array() || entry.size() != 3 ||
            !entry[0].is_number() || !entry[1].is_number() || !entry[2].is_number())
        {
            continue;
        }

        AnchorPolyStackCoord coord;
        coord.wowX = entry[0].get<float>();
        coord.wowY = entry[1].get<float>();
        coord.wowZ = entry[2].get<float>();
        coords.push_back(coord);
    }

    return coords;
}

static std::vector<AnchorPolyStackCoord> ParseAnchorCoordsWithFallback(
    const json& config,
    const char* primaryKey,
    const char* fallbackKey)
{
    std::vector<AnchorPolyStackCoord> coords = ParseAnchorCoords(config, primaryKey);
    if (!coords.empty() || !fallbackKey)
        return coords;

    return ParseAnchorCoords(config, fallbackKey);
}

static bool TryParseAnchorCoordJson(const json& entry, AnchorPolyStackCoord& coord)
{
    if (!entry.is_array() || entry.size() != 3 ||
        !entry[0].is_number() || !entry[1].is_number() || !entry[2].is_number())
    {
        return false;
    }

    coord.wowX = entry[0].get<float>();
    coord.wowY = entry[1].get<float>();
    coord.wowZ = entry[2].get<float>();
    return true;
}

static std::vector<AnchorPolyStackCoord> ParseAnchorPolyStackCoords(const json& config)
{
    return ParseAnchorCoords(config, "postDetourCullAnchorPolyStacksCoordsWow");
}

static std::vector<AnchorPolyStackCoord> ParseAnchorCompactWorkCoords(const json& config)
{
    return ParseAnchorCoordsWithFallback(
        config,
        "preRegionAnchorCoordsWow",
        "postDetourCullAnchorPolyStacksCoordsWow");
}

static std::vector<AnchorPolyStackCoord> ParseAnchorStageManifestCoords(const json& config)
{
    return ParseAnchorCoords(config, "anchorStageManifestCoordsWow");
}

static std::vector<AnchorPolyStackCoord> ParsePrePolyPreserveAnchorSupportCoords(const json& config)
{
    return ParseAnchorCoords(config, "prePolyPreserveAnchorSupportCoordsWow");
}

static std::vector<AnchorPolyStackCoord> ParsePrePolyUseRawAnchorSupportContours(const json& config)
{
    return ParseAnchorCoords(config, "prePolyUseRawAnchorSupportContoursWow");
}

static std::vector<AnchorRouteTarget> ParseAnchorRouteTargets(const json& config)
{
    std::vector<AnchorRouteTarget> routeTargets;

    auto it = config.find("anchorRouteTargetsWow");
    if (it == config.end() || !it->is_array())
        return routeTargets;

    for (const json& entry : *it)
    {
        if (!entry.is_object())
            continue;

        auto sourceIt = entry.find("source");
        auto targetIt = entry.find("target");
        if (sourceIt == entry.end() || targetIt == entry.end())
            continue;

        AnchorRouteTarget routeTarget;
        if (!TryParseAnchorCoordJson(*sourceIt, routeTarget.source) ||
            !TryParseAnchorCoordJson(*targetIt, routeTarget.target))
        {
            continue;
        }

        auto labelIt = entry.find("label");
        if (labelIt != entry.end() && labelIt->is_string())
            routeTarget.label = labelIt->get<std::string>();
        else
            routeTarget.label = FormatAnchorStageId(routeTarget.target);

        routeTargets.push_back(routeTarget);
    }

    return routeTargets;
}

static std::vector<AnchorPolyStackCoord> MergeUniqueAnchorCoords(
    const std::vector<AnchorPolyStackCoord>& primary,
    const std::vector<AnchorPolyStackCoord>& extras)
{
    std::vector<AnchorPolyStackCoord> merged = primary;
    std::unordered_set<std::string> seenIds;
    seenIds.reserve(primary.size() + extras.size());
    for (const AnchorPolyStackCoord& coord : primary)
        seenIds.insert(FormatAnchorStageId(coord));

    for (const AnchorPolyStackCoord& coord : extras)
    {
        const std::string id = FormatAnchorStageId(coord);
        if (!seenIds.insert(id).second)
            continue;

        merged.push_back(coord);
    }

    return merged;
}

static std::vector<const AnchorRouteTarget*> FindAnchorRouteTargets(
    const std::vector<AnchorRouteTarget>& routeTargets,
    const AnchorPolyStackCoord& anchor)
{
    std::vector<const AnchorRouteTarget*> matches;
    for (const AnchorRouteTarget& routeTarget : routeTargets)
    {
        if (AreAnchorCoordsEquivalent(routeTarget.source, anchor))
            matches.push_back(&routeTarget);
    }

    return matches;
}

// [WWoW-DIVERGENCE] 2026-05-22/23: pre-region compact-heightfield cleanup for
// anchor-proven stacked slabs. The dead-end OG hallway/vertical/exterior probes
// showed that the bad competing layer already exists at buildCHF/median time as
// an immediate multi-floor stack in the same cell. The compact-heightfield span
// floor (span.y) is the walkable support surface; span.y + span.h is the
// clearance ceiling to the next obstacle, not the standable floor. Prefer the
// local support floor closest to the anchor and cull lower competing floors in
// the same cell so they never become regions/contours/polys.
static int CullAnchorUpperCompactSpans(rcCompactHeightfield& chf,
    const float xyExtent, const float supportZTolerance,
    const std::vector<AnchorPolyStackCoord>& anchorCoords,
    const bool logDiagnostics)
{
    if (!chf.cells || !chf.spans || !chf.areas || anchorCoords.empty())
        return 0;

    constexpr float kSupportFloorBelowSlack = 0.15f;
    constexpr float kCompetingLowerFloorMinDrop = 0.25f;
    int culled = 0;

    for (const AnchorPolyStackCoord& anchor : anchorCoords)
    {
        const float anchorRecastX = anchor.wowY;
        const float anchorRecastZ = anchor.wowX;
        const float supportFloorMinY = anchor.wowZ - kSupportFloorBelowSlack;
        const float supportFloorMaxY = anchor.wowZ + supportZTolerance;

        int supportCells = 0;
        int anchorCulled = 0;

        for (int y = 0; y < chf.height; ++y)
        {
            const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < chf.width; ++x)
            {
                const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                if (cell.count < 2)
                    continue;

                int bestSupportSpanIndex = -1;
                float bestSupportFloor = 0.0f;
                float bestSupportScore = FLT_MAX;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanFloor < supportFloorMinY || spanFloor > supportFloorMaxY)
                        continue;

                    const float delta = spanFloor - anchor.wowZ;
                    // Prefer surfaces at/above the anchor first. If none exist,
                    // fall back to the closest slightly-below floor.
                    const float score =
                        (delta >= 0.0f ? 0.0f : 1000.0f) +
                        fabsf(delta);
                    if (score < bestSupportScore)
                    {
                        bestSupportScore = score;
                        bestSupportFloor = spanFloor;
                        bestSupportSpanIndex = static_cast<int>(i);
                    }
                }

                if (bestSupportSpanIndex < 0)
                    continue;

                ++supportCells;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (static_cast<int>(i) == bestSupportSpanIndex)
                        continue;
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanBottom = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanBottom >= bestSupportFloor - kCompetingLowerFloorMinDrop)
                        continue;

                    chf.areas[i] = RC_NULL_AREA;
                    ++culled;
                    ++anchorCulled;
                }
            }
        }

        if (logDiagnostics)
        {
            printf("[CHF-ANCHOR-CULL] anchor=(%.3f,%.3f,%.3f) supportCells=%d culled=%d\n",
                anchor.wowX, anchor.wowY, anchor.wowZ, supportCells, anchorCulled);
        }
    }

    return culled;
}

// [WWoW-DIVERGENCE] 2026-05-23: source-geometry-backed compact-span cleanup.
// The older anchor compact cull chooses the preferred support floor from the
// already-quantized compact spans, which can preserve the wrong basin when the
// contaminated lower layer wins locally. This pass instead samples the original
// classified source triangles near the verified anchor coords and uses that
// support floor to null lower competing compact spans before region building.
static int CullAnchorSourceSupportCompactSpans(rcCompactHeightfield& chf,
    const float xyExtent, const float supportZTolerance, const bool fallbackToWindowSupport,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports,
    const bool logDiagnostics)
{
    if (!chf.cells || !chf.spans || !chf.areas || sourceSupports.empty())
        return 0;

    constexpr float kFallbackCullRadius = 0.85f;

    struct AnchorWindowCell
    {
        int x = 0;
        int y = 0;
        bool hasSupportSpan = false;
        float bestSupportFloor = 0.0f;
        float cellDistance2D = 0.0f;
    };

    int culled = 0;
    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        if (anchorRecastX < chf.bmin[0] - xyExtent || anchorRecastX > chf.bmax[0] + xyExtent ||
            anchorRecastZ < chf.bmin[2] - xyExtent || anchorRecastZ > chf.bmax[2] + xyExtent)
        {
            continue;
        }
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);
        const bool allowFallbackBasinCull = support.projectedInside || support.distance2D <= 0.5f;

        std::vector<AnchorWindowCell> windowCells;
        int supportCells = 0;
        int anchorCulled = 0;
        int fallbackCells = 0;
        for (int y = 0; y < chf.height; ++y)
        {
            const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < chf.width; ++x)
            {
                const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                if (cell.count < 1)
                    continue;

                const float dxCell = cellCenterX - anchorRecastX;
                const float dzCell = cellCenterZ - anchorRecastZ;
                const float cellDistance2D = sqrtf(dxCell * dxCell + dzCell * dzCell);

                AnchorWindowCell windowCell;
                windowCell.x = x;
                windowCell.y = y;
                windowCell.cellDistance2D = cellDistance2D;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY)
                    {
                        windowCell.hasSupportSpan = true;
                        windowCell.bestSupportFloor = std::max(windowCell.bestSupportFloor, spanFloor);
                    }
                }

                windowCells.push_back(windowCell);

                if (!windowCell.hasSupportSpan)
                {
                    if (!allowFallbackBasinCull || cellDistance2D > kFallbackCullRadius)
                        continue;

                    int cellCulled = 0;
                    for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                    {
                        if (chf.areas[i] == RC_NULL_AREA)
                            continue;

                        const rcCompactSpan& span = chf.spans[i];
                        const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                        if (!IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning))
                            continue;

                        chf.areas[i] = RC_NULL_AREA;
                        ++culled;
                        ++anchorCulled;
                        ++cellCulled;
                    }

                    if (cellCulled > 0)
                        ++fallbackCells;
                    continue;
                }

                ++supportCells;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanFloor >= windowCell.bestSupportFloor - supportBandTuning.competingLowerFloorMinDrop)
                        continue;

                    chf.areas[i] = RC_NULL_AREA;
                    ++culled;
                    ++anchorCulled;
                }
            }
        }

        if (fallbackToWindowSupport && supportCells > 0)
        {
            for (const AnchorWindowCell& windowCell : windowCells)
            {
                if (windowCell.hasSupportSpan)
                    continue;

                const rcCompactCell& cell = chf.cells[windowCell.x + windowCell.y * chf.width];
                int cellCulled = 0;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (!IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning))
                        continue;

                    chf.areas[i] = RC_NULL_AREA;
                    ++culled;
                    ++anchorCulled;
                    ++cellCulled;
                }

                if (cellCulled > 0)
                    ++fallbackCells;
            }
        }

        if (logDiagnostics)
        {
            printf("[CHF-SRC-ANCHOR-CULL] anchor=(%.3f,%.3f,%.3f) supportY=%.3f source=%d supportCells=%d fallbackCells=%d culled=%d\n",
                support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                support.supportY, (int)support.source, supportCells, fallbackCells, anchorCulled);
        }
    }

    return culled;
}

// [WWoW-DIVERGENCE] 2026-05-23: source-backed support preservation after
// rcErodeWalkableArea. Stage manifests on tile 1:40,29 showed one red hallway
// anchor keeping ~80 source-matching support cells through buildCHF but only 8
// after erode, then flipping to a lower-only dominant component at median.
// Restore only the compact spans that (a) were walkable before erode, (b) were
// removed by erode, and (c) still match the verified source-support floor band
// near the anchor. This keeps the proof/fix surface bake-side and local.
static int RestoreAnchorSourceSupportCompactSpansAfterErode(rcCompactHeightfield& chf,
    const std::vector<unsigned char>& preErodeAreas,
    const float xyExtent,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports,
    const bool logDiagnostics)
{
    if (!chf.cells || !chf.spans || !chf.areas || sourceSupports.empty() || preErodeAreas.size() != chf.spanCount)
        return 0;

    constexpr float kMaxSourceDistance2D = 0.50f;

    int restored = 0;
    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (!support.found)
            continue;
        if (!support.projectedInside && support.distance2D > kMaxSourceDistance2D)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        if (anchorRecastX < chf.bmin[0] - xyExtent || anchorRecastX > chf.bmax[0] + xyExtent ||
            anchorRecastZ < chf.bmin[2] - xyExtent || anchorRecastZ > chf.bmax[2] + xyExtent)
        {
            continue;
        }

        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = support.supportY + supportBandTuning.supportFloorSlackAbove;
        int restoredCells = 0;
        int restoredSpans = 0;

        for (int y = 0; y < chf.height; ++y)
        {
            const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < chf.width; ++x)
            {
                const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                bool cellRestored = false;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] != RC_NULL_AREA || preErodeAreas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanFloor < supportFloorMinY || spanFloor > supportFloorMaxY)
                        continue;

                    chf.areas[i] = preErodeAreas[i];
                    ++restored;
                    ++restoredSpans;
                    cellRestored = true;
                }

                if (cellRestored)
                    ++restoredCells;
            }
        }

        if (logDiagnostics)
        {
            printf("[CHF-SRC-RESTORE] anchor=(%.3f,%.3f,%.3f) supportY=%.3f source=%d restoredCells=%d restoredSpans=%d\n",
                support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                support.supportY, (int)support.source, restoredCells, restoredSpans);
        }
    }

    return restored;
}

static void LogAnchorSourceSupportHeightfieldStage(const char* stage, const rcHeightfield& hf,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports)
{
    if (!hf.spans || sourceSupports.empty())
        return;

    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        if (anchorRecastX < hf.bmin[0] - xyExtent || anchorRecastX > hf.bmax[0] + xyExtent ||
            anchorRecastZ < hf.bmin[2] - xyExtent || anchorRecastZ > hf.bmax[2] + xyExtent)
        {
            continue;
        }
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        int supportCells = 0;
        int supportSpans = 0;
        int lowerCells = 0;
        int lowerSpans = 0;
        float bestSupportDelta = std::numeric_limits<float>::max();

        for (int y = 0; y < hf.height; ++y)
        {
            const float cellCenterZ = hf.bmin[2] + (y + 0.5f) * hf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < hf.width; ++x)
            {
                const float cellCenterX = hf.bmin[0] + (x + 0.5f) * hf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                bool cellHasSupport = false;
                bool cellHasLower = false;
                for (const rcSpan* span = hf.spans[x + y * hf.width]; span; span = span->next)
                {
                    if (span->area == RC_NULL_AREA)
                        continue;

                    const float spanFloor = hf.bmin[1] + (float)span->smax * hf.ch;
                    if (spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY)
                    {
                        cellHasSupport = true;
                        ++supportSpans;
                        bestSupportDelta = std::min(bestSupportDelta, fabsf(spanFloor - support.supportY));
                    }
                    else if (IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning))
                    {
                        cellHasLower = true;
                        ++lowerSpans;
                    }
                }

                if (cellHasSupport)
                    ++supportCells;
                if (cellHasLower)
                    ++lowerCells;
            }
        }

        printf("[HF-SRC-ANCHOR] stage=%s anchor=(%.3f,%.3f,%.3f) supportY=%.3f source=%s supportCells=%d supportSpans=%d lowerCells=%d lowerSpans=%d bestDelta=%.3f\n",
            stage,
            support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
            support.supportY, MeshTriangleSourceName(support.source),
            supportCells, supportSpans, lowerCells, lowerSpans,
            bestSupportDelta == std::numeric_limits<float>::max() ? -1.0f : bestSupportDelta);
    }
}

static void LogAnchorSourceSupportCompactStage(const char* stage, const rcCompactHeightfield& chf,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports)
{
    if (!chf.cells || !chf.spans || !chf.areas || sourceSupports.empty())
        return;

    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        int supportCells = 0;
        int supportSpans = 0;
        int lowerCells = 0;
        int lowerSpans = 0;
        float bestSupportDelta = std::numeric_limits<float>::max();

        for (int y = 0; y < chf.height; ++y)
        {
            const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < chf.width; ++x)
            {
                const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                bool cellHasSupport = false;
                bool cellHasLower = false;
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                    if (spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY)
                    {
                        cellHasSupport = true;
                        ++supportSpans;
                        bestSupportDelta = std::min(bestSupportDelta, fabsf(spanFloor - support.supportY));
                    }
                    else if (IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning))
                    {
                        cellHasLower = true;
                        ++lowerSpans;
                    }
                }

                if (cellHasSupport)
                    ++supportCells;
                if (cellHasLower)
                    ++lowerCells;
            }
        }

        printf("[CHF-SRC-ANCHOR] stage=%s anchor=(%.3f,%.3f,%.3f) supportY=%.3f source=%s supportCells=%d supportSpans=%d lowerCells=%d lowerSpans=%d bestDelta=%.3f\n",
            stage,
            support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
            support.supportY, MeshTriangleSourceName(support.source),
            supportCells, supportSpans, lowerCells, lowerSpans,
            bestSupportDelta == std::numeric_limits<float>::max() ? -1.0f : bestSupportDelta);
    }
}

static void LogAnchorSourceSupportCompactComponents(const char* stage, const rcCompactHeightfield& chf,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports)
{
    if (!chf.cells || !chf.spans || !chf.areas || sourceSupports.empty())
        return;

    struct WindowSpan
    {
        unsigned int globalIndex = 0;
        int x = 0;
        int y = 0;
        float floor = 0.0f;
        float distance2D = 0.0f;
        bool supportRange = false;
        bool competingLower = false;
    };

    struct ComponentSummary
    {
        int spanCount = 0;
        int cellCount = 0;
        int supportSpanCount = 0;
        int lowerSpanCount = 0;
        float minFloor = std::numeric_limits<float>::max();
        float maxFloor = -std::numeric_limits<float>::max();
        float minDistance2D = std::numeric_limits<float>::max();
        bool touchesBoundary = false;
        bool containsAnchorCell = false;
    };

    for (const AnchorSourceSupportProbe& support : sourceSupports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        if (anchorRecastX < chf.bmin[0] - xyExtent || anchorRecastX > chf.bmax[0] + xyExtent ||
            anchorRecastZ < chf.bmin[2] - xyExtent || anchorRecastZ > chf.bmax[2] + xyExtent)
        {
            continue;
        }

        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        std::vector<WindowSpan> windowSpans;
        std::vector<int> localByGlobalSpan(chf.spanCount, -1);
        int minWindowX = chf.width;
        int maxWindowX = -1;
        int minWindowY = chf.height;
        int maxWindowY = -1;

        for (int y = 0; y < chf.height; ++y)
        {
            const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
            if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
                continue;

            for (int x = 0; x < chf.width; ++x)
            {
                const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
                if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                    continue;

                const rcCompactCell& cell = chf.cells[x + y * chf.width];
                if (cell.count < 1)
                    continue;

                minWindowX = std::min(minWindowX, x);
                maxWindowX = std::max(maxWindowX, x);
                minWindowY = std::min(minWindowY, y);
                maxWindowY = std::max(maxWindowY, y);

                const float dxCell = cellCenterX - anchorRecastX;
                const float dzCell = cellCenterZ - anchorRecastZ;
                const float cellDistance2D = sqrtf(dxCell * dxCell + dzCell * dzCell);
                for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (chf.areas[i] == RC_NULL_AREA)
                        continue;

                    const rcCompactSpan& span = chf.spans[i];
                    const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;

                    WindowSpan windowSpan;
                    windowSpan.globalIndex = i;
                    windowSpan.x = x;
                    windowSpan.y = y;
                    windowSpan.floor = spanFloor;
                    windowSpan.distance2D = cellDistance2D;
                    windowSpan.supportRange = spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY;
                    windowSpan.competingLower = IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning);
                    localByGlobalSpan[i] = static_cast<int>(windowSpans.size());
                    windowSpans.push_back(windowSpan);
                }
            }
        }

        if (windowSpans.empty())
            continue;

        std::vector<unsigned char> visited(windowSpans.size(), 0);
        std::vector<ComponentSummary> components;
        std::vector<int> stack;
        std::unordered_set<int> componentCells;
        stack.reserve(windowSpans.size());
        componentCells.reserve(windowSpans.size());

        for (size_t localIndex = 0; localIndex < windowSpans.size(); ++localIndex)
        {
            if (visited[localIndex])
                continue;

            ComponentSummary component;
            componentCells.clear();
            stack.clear();
            stack.push_back(static_cast<int>(localIndex));
            visited[localIndex] = 1;

            while (!stack.empty())
            {
                const int currentLocalIndex = stack.back();
                stack.pop_back();

                const WindowSpan& current = windowSpans[currentLocalIndex];
                const rcCompactSpan& currentSpan = chf.spans[current.globalIndex];
                component.spanCount++;
                component.minFloor = std::min(component.minFloor, current.floor);
                component.maxFloor = std::max(component.maxFloor, current.floor);
                component.minDistance2D = std::min(component.minDistance2D, current.distance2D);
                if (current.supportRange)
                    component.supportSpanCount++;
                if (current.competingLower)
                    component.lowerSpanCount++;
                if (current.distance2D <= chf.cs)
                    component.containsAnchorCell = true;
                if (current.x == minWindowX || current.x == maxWindowX || current.y == minWindowY || current.y == maxWindowY)
                    component.touchesBoundary = true;
                componentCells.insert(current.x + current.y * chf.width);

                for (int dir = 0; dir < 4; ++dir)
                {
                    const int connection = rcGetCon(currentSpan, dir);
                    if (connection == RC_NOT_CONNECTED)
                        continue;

                    const int nx = current.x + rcGetDirOffsetX(dir);
                    const int ny = current.y + rcGetDirOffsetY(dir);
                    if (nx < minWindowX || nx > maxWindowX || ny < minWindowY || ny > maxWindowY)
                        continue;

                    const rcCompactCell& neighborCell = chf.cells[nx + ny * chf.width];
                    const unsigned int neighborGlobalIndex = neighborCell.index + static_cast<unsigned int>(connection);
                    if (neighborGlobalIndex >= chf.spanCount)
                        continue;

                    const int neighborLocalIndex = localByGlobalSpan[neighborGlobalIndex];
                    if (neighborLocalIndex < 0 || visited[neighborLocalIndex])
                        continue;

                    visited[neighborLocalIndex] = 1;
                    stack.push_back(neighborLocalIndex);
                }
            }

            component.cellCount = static_cast<int>(componentCells.size());
            components.push_back(component);
        }

        std::sort(components.begin(), components.end(),
            [](const ComponentSummary& left, const ComponentSummary& right)
            {
                if ((left.containsAnchorCell ? 1 : 0) != (right.containsAnchorCell ? 1 : 0))
                    return left.containsAnchorCell > right.containsAnchorCell;
                if ((left.supportSpanCount > 0 ? 1 : 0) != (right.supportSpanCount > 0 ? 1 : 0))
                    return left.supportSpanCount > right.supportSpanCount;
                if (fabsf(left.minDistance2D - right.minDistance2D) > 0.001f)
                    return left.minDistance2D < right.minDistance2D;
                return left.spanCount > right.spanCount;
            });

        printf("[CHF-SRC-COMP] stage=%s anchor=(%.3f,%.3f,%.3f) supportY=%.3f source=%s components=%u\n",
            stage,
            support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
            support.supportY, MeshTriangleSourceName(support.source),
            (unsigned int)components.size());

        const size_t maxComponentsToLog = std::min<size_t>(components.size(), 6);
        for (size_t index = 0; index < maxComponentsToLog; ++index)
        {
            const ComponentSummary& component = components[index];
            printf("[CHF-SRC-COMP] stage=%s anchor=(%.3f,%.3f,%.3f) comp=%u spans=%d cells=%d supportSpans=%d lowerSpans=%d minFloor=%.3f maxFloor=%.3f minDist=%.3f touchesBoundary=%d containsAnchor=%d\n",
                stage,
                support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                (unsigned int)index,
                component.spanCount,
                component.cellCount,
                component.supportSpanCount,
                component.lowerSpanCount,
                component.minFloor,
                component.maxFloor,
                component.minDistance2D,
                component.touchesBoundary ? 1 : 0,
                component.containsAnchorCell ? 1 : 0);
        }
    }
}

static json BuildAnchorSourceSupportJson(const AnchorSourceSupportProbe& support)
{
    return
    {
        { "found", support.found },
        { "supportY", support.found ? support.supportY : 0.0f },
        { "distance2D", support.found ? support.distance2D : -1.0f },
        { "triIndex", support.found ? support.triIndex : -1 },
        { "source", support.found ? MeshTriangleSourceName(support.source) : "none" },
        { "projectedInside", support.found && support.projectedInside },
        { "borrowed", support.found && support.borrowed },
        { "borrowedFromAnchorId", support.found && support.borrowed ? FormatAnchorStageId(support.borrowedFrom) : "" },
        { "deltaFromAnchorZ", support.found ? support.supportY - support.anchor.wowZ : 0.0f },
    };
}

static json BuildBaseAnchorStageJson(const char* stageName, const char* kind, const AnchorSourceSupportProbe& support)
{
    json stage =
    {
        { "name", stageName },
        { "kind", kind },
        { "upperSupportExists", false },
        { "lowerCompetitorExists", false },
        { "dominantLowerCandidate", false },
        { "supportCandidateCount", 0 },
        { "lowerCandidateCount", 0 },
    };

    if (!support.found)
        stage["unprovenReason"] = "no_source_support_probe";

    return stage;
}

static json BuildHeightfieldAnchorStageSummary(const char* stageName, const rcHeightfield& hf,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const AnchorSourceSupportProbe& support)
{
    json stage = BuildBaseAnchorStageJson(stageName, "heightfield", support);
    stage["supportCells"] = 0;
    stage["supportSpans"] = 0;
    stage["lowerCells"] = 0;
    stage["lowerSpans"] = 0;
    stage["bestSupportDelta"] = -1.0f;

    if (!hf.spans || !support.found)
        return stage;

    const float anchorRecastX = support.anchor.wowY;
    const float anchorRecastZ = support.anchor.wowX;
    const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
    const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

    int supportCells = 0;
    int supportSpans = 0;
    int lowerCells = 0;
    int lowerSpans = 0;
    float bestSupportDelta = std::numeric_limits<float>::max();

    for (int y = 0; y < hf.height; ++y)
    {
        const float cellCenterZ = hf.bmin[2] + (y + 0.5f) * hf.cs;
        if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
            continue;

        for (int x = 0; x < hf.width; ++x)
        {
            const float cellCenterX = hf.bmin[0] + (x + 0.5f) * hf.cs;
            if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                continue;

            bool cellHasSupport = false;
            bool cellHasLower = false;
            for (const rcSpan* span = hf.spans[x + y * hf.width]; span; span = span->next)
            {
                if (span->area == RC_NULL_AREA)
                    continue;

                const float spanFloor = hf.bmin[1] + (float)span->smax * hf.ch;
                if (spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY)
                {
                    cellHasSupport = true;
                    ++supportSpans;
                    bestSupportDelta = std::min(bestSupportDelta, fabsf(spanFloor - support.supportY));
                }
                else if (IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning))
                {
                    cellHasLower = true;
                    ++lowerSpans;
                }
            }

            if (cellHasSupport)
                ++supportCells;
            if (cellHasLower)
                ++lowerCells;
        }
    }

    stage["upperSupportExists"] = supportCells > 0;
    stage["lowerCompetitorExists"] = lowerCells > 0;
    stage["supportCandidateCount"] = supportSpans;
    stage["lowerCandidateCount"] = lowerSpans;
    stage["supportCells"] = supportCells;
    stage["supportSpans"] = supportSpans;
    stage["lowerCells"] = lowerCells;
    stage["lowerSpans"] = lowerSpans;
    stage["bestSupportDelta"] = bestSupportDelta == std::numeric_limits<float>::max() ? -1.0f : bestSupportDelta;
    return stage;
}

static json BuildCompactAnchorStageSummary(const char* stageName, const rcCompactHeightfield& chf,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const AnchorSourceSupportProbe& support, const bool includeComponents)
{
    json stage = BuildBaseAnchorStageJson(stageName, "compact", support);
    stage["supportCells"] = 0;
    stage["supportSpans"] = 0;
    stage["lowerCells"] = 0;
    stage["lowerSpans"] = 0;
    stage["bestSupportDelta"] = -1.0f;

    if (!chf.cells || !chf.spans || !chf.areas || !support.found)
        return stage;

    const float anchorRecastX = support.anchor.wowY;
    const float anchorRecastZ = support.anchor.wowX;
    if (anchorRecastX < chf.bmin[0] - xyExtent || anchorRecastX > chf.bmax[0] + xyExtent ||
        anchorRecastZ < chf.bmin[2] - xyExtent || anchorRecastZ > chf.bmax[2] + xyExtent)
    {
        stage["unprovenReason"] = "anchor_outside_compact_window";
        return stage;
    }

    const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
    const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

    int supportCells = 0;
    int supportSpans = 0;
    int lowerCells = 0;
    int lowerSpans = 0;
    float bestSupportDelta = std::numeric_limits<float>::max();

    struct WindowSpan
    {
        unsigned int globalIndex = 0;
        int x = 0;
        int y = 0;
        float floor = 0.0f;
        float distance2D = 0.0f;
        bool supportRange = false;
        bool competingLower = false;
    };

    struct ComponentSummary
    {
        int spanCount = 0;
        int cellCount = 0;
        int supportSpanCount = 0;
        int lowerSpanCount = 0;
        float minFloor = std::numeric_limits<float>::max();
        float maxFloor = -std::numeric_limits<float>::max();
        float minDistance2D = std::numeric_limits<float>::max();
        bool touchesBoundary = false;
        bool containsAnchorCell = false;
        std::set<unsigned short> regionIds;
    };

    std::vector<WindowSpan> windowSpans;
    std::vector<int> localByGlobalSpan(includeComponents ? chf.spanCount : 0, -1);
    int minWindowX = chf.width;
    int maxWindowX = -1;
    int minWindowY = chf.height;
    int maxWindowY = -1;

    for (int y = 0; y < chf.height; ++y)
    {
        const float cellCenterZ = chf.bmin[2] + (y + 0.5f) * chf.cs;
        if (fabsf(cellCenterZ - anchorRecastZ) > xyExtent)
            continue;

        for (int x = 0; x < chf.width; ++x)
        {
            const float cellCenterX = chf.bmin[0] + (x + 0.5f) * chf.cs;
            if (fabsf(cellCenterX - anchorRecastX) > xyExtent)
                continue;

            const rcCompactCell& cell = chf.cells[x + y * chf.width];
            bool cellHasSupport = false;
            bool cellHasLower = false;
            if (cell.count < 1)
                continue;

            minWindowX = std::min(minWindowX, x);
            maxWindowX = std::max(maxWindowX, x);
            minWindowY = std::min(minWindowY, y);
            maxWindowY = std::max(maxWindowY, y);

            const float dxCell = cellCenterX - anchorRecastX;
            const float dzCell = cellCenterZ - anchorRecastZ;
            const float cellDistance2D = sqrtf(dxCell * dxCell + dzCell * dzCell);
            for (unsigned int i = cell.index; i < cell.index + cell.count; ++i)
            {
                if (chf.areas[i] == RC_NULL_AREA)
                    continue;

                const rcCompactSpan& span = chf.spans[i];
                const float spanFloor = chf.bmin[1] + (float)span.y * chf.ch;
                const bool supportRange = spanFloor >= supportFloorMinY && spanFloor <= supportFloorMaxY;
                const bool competingLower = IsAnchorCompetingLowerFloor(spanFloor, support, supportBandTuning);

                if (supportRange)
                {
                    cellHasSupport = true;
                    ++supportSpans;
                    bestSupportDelta = std::min(bestSupportDelta, fabsf(spanFloor - support.supportY));
                }
                else if (competingLower)
                {
                    cellHasLower = true;
                    ++lowerSpans;
                }

                if (includeComponents)
                {
                    WindowSpan windowSpan;
                    windowSpan.globalIndex = i;
                    windowSpan.x = x;
                    windowSpan.y = y;
                    windowSpan.floor = spanFloor;
                    windowSpan.distance2D = cellDistance2D;
                    windowSpan.supportRange = supportRange;
                    windowSpan.competingLower = competingLower;
                    localByGlobalSpan[i] = static_cast<int>(windowSpans.size());
                    windowSpans.push_back(windowSpan);
                }
            }

            if (cellHasSupport)
                ++supportCells;
            if (cellHasLower)
                ++lowerCells;
        }
    }

    stage["upperSupportExists"] = supportCells > 0;
    stage["lowerCompetitorExists"] = lowerCells > 0;
    stage["supportCandidateCount"] = supportSpans;
    stage["lowerCandidateCount"] = lowerSpans;
    stage["supportCells"] = supportCells;
    stage["supportSpans"] = supportSpans;
    stage["lowerCells"] = lowerCells;
    stage["lowerSpans"] = lowerSpans;
    stage["bestSupportDelta"] = bestSupportDelta == std::numeric_limits<float>::max() ? -1.0f : bestSupportDelta;

    if (!includeComponents || windowSpans.empty())
        return stage;

    std::vector<unsigned char> visited(windowSpans.size(), 0);
    std::vector<ComponentSummary> components;
    std::vector<int> stack;
    std::unordered_set<int> componentCells;
    stack.reserve(windowSpans.size());
    componentCells.reserve(windowSpans.size());

    for (size_t localIndex = 0; localIndex < windowSpans.size(); ++localIndex)
    {
        if (visited[localIndex])
            continue;

        ComponentSummary component;
        componentCells.clear();
        stack.clear();
        stack.push_back(static_cast<int>(localIndex));
        visited[localIndex] = 1;

        while (!stack.empty())
        {
            const int currentLocalIndex = stack.back();
            stack.pop_back();

            const WindowSpan& current = windowSpans[currentLocalIndex];
            const rcCompactSpan& currentSpan = chf.spans[current.globalIndex];
            component.spanCount++;
            component.minFloor = std::min(component.minFloor, current.floor);
            component.maxFloor = std::max(component.maxFloor, current.floor);
            component.minDistance2D = std::min(component.minDistance2D, current.distance2D);
            if (current.supportRange)
                component.supportSpanCount++;
            if (current.competingLower)
                component.lowerSpanCount++;
            if (current.distance2D <= chf.cs)
                component.containsAnchorCell = true;
            if (current.x == minWindowX || current.x == maxWindowX || current.y == minWindowY || current.y == maxWindowY)
                component.touchesBoundary = true;
            componentCells.insert(current.x + current.y * chf.width);
            if (currentSpan.reg != 0)
                component.regionIds.insert(currentSpan.reg);

            for (int dir = 0; dir < 4; ++dir)
            {
                const int connection = rcGetCon(currentSpan, dir);
                if (connection == RC_NOT_CONNECTED)
                    continue;

                const int nx = current.x + rcGetDirOffsetX(dir);
                const int ny = current.y + rcGetDirOffsetY(dir);
                if (nx < minWindowX || nx > maxWindowX || ny < minWindowY || ny > maxWindowY)
                    continue;

                const rcCompactCell& neighborCell = chf.cells[nx + ny * chf.width];
                const unsigned int neighborGlobalIndex = neighborCell.index + static_cast<unsigned int>(connection);
                if (neighborGlobalIndex >= chf.spanCount)
                    continue;

                const int neighborLocalIndex = localByGlobalSpan[neighborGlobalIndex];
                if (neighborLocalIndex < 0 || visited[neighborLocalIndex])
                    continue;

                visited[neighborLocalIndex] = 1;
                stack.push_back(neighborLocalIndex);
            }
        }

        component.cellCount = static_cast<int>(componentCells.size());
        components.push_back(component);
    }

    std::sort(components.begin(), components.end(),
        [](const ComponentSummary& left, const ComponentSummary& right)
        {
            if ((left.containsAnchorCell ? 1 : 0) != (right.containsAnchorCell ? 1 : 0))
                return left.containsAnchorCell > right.containsAnchorCell;
            if ((left.supportSpanCount > 0 ? 1 : 0) != (right.supportSpanCount > 0 ? 1 : 0))
                return left.supportSpanCount > right.supportSpanCount;
            if (fabsf(left.minDistance2D - right.minDistance2D) > 0.001f)
                return left.minDistance2D < right.minDistance2D;
            return left.spanCount > right.spanCount;
        });

    bool supportContainsAnchor = false;
    bool lowerContainsAnchor = false;
    bool anySupportComponent = false;
    json componentArray = json::array();
    for (size_t index = 0; index < components.size(); ++index)
    {
        const ComponentSummary& component = components[index];
        if (component.supportSpanCount > 0)
            anySupportComponent = true;
        if (component.containsAnchorCell && component.supportSpanCount > 0)
            supportContainsAnchor = true;
        if (component.containsAnchorCell && component.supportSpanCount == 0 && component.lowerSpanCount > 0)
            lowerContainsAnchor = true;

        json regionIds = json::array();
        for (const unsigned short regionId : component.regionIds)
            regionIds.push_back(regionId);

        componentArray.push_back(
            {
                { "componentIndex", static_cast<int>(index) },
                { "spanCount", component.spanCount },
                { "cellCount", component.cellCount },
                { "supportSpanCount", component.supportSpanCount },
                { "lowerSpanCount", component.lowerSpanCount },
                { "minFloor", component.minFloor == std::numeric_limits<float>::max() ? 0.0f : component.minFloor },
                { "maxFloor", component.maxFloor == -std::numeric_limits<float>::max() ? 0.0f : component.maxFloor },
                { "minDistance2D", component.minDistance2D == std::numeric_limits<float>::max() ? -1.0f : component.minDistance2D },
                { "touchesBoundary", component.touchesBoundary },
                { "containsAnchorCell", component.containsAnchorCell },
                { "regionIds", regionIds },
            });
    }

    stage["supportContainsAnchorCell"] = supportContainsAnchor;
    stage["lowerContainsAnchorCell"] = lowerContainsAnchor;
    stage["dominantLowerCandidate"] = lowerContainsAnchor && anySupportComponent && !supportContainsAnchor;
    stage["components"] = componentArray;
    return stage;
}

static json BuildContourAnchorStageSummary(const rcContourSet& contours,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const AnchorSourceSupportProbe& support)
{
    json stage = BuildBaseAnchorStageJson("contours", "contours", support);
    stage["contours"] = json::array();

    if (!contours.conts || !support.found)
        return stage;

    const float anchorRecastX = support.anchor.wowY;
    const float anchorRecastZ = support.anchor.wowX;
    const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
    const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

    bool supportContainsAnchor = false;
    bool lowerContainsAnchor = false;
    int supportCount = 0;
    int lowerCount = 0;

    for (int contourIndex = 0; contourIndex < contours.nconts; ++contourIndex)
    {
        const rcContour& contour = contours.conts[contourIndex];
        if (contour.nverts < 3)
            continue;

        std::vector<Point2DXZ> polygon;
        polygon.reserve(contour.nverts);
        float minX = std::numeric_limits<float>::max();
        float maxX = -std::numeric_limits<float>::max();
        float minY = std::numeric_limits<float>::max();
        float maxY = -std::numeric_limits<float>::max();
        float minZ = std::numeric_limits<float>::max();
        float maxZ = -std::numeric_limits<float>::max();
        for (int vertIndex = 0; vertIndex < contour.nverts; ++vertIndex)
        {
            const int* cv = &contour.verts[vertIndex * 4];
            const float recastX = contours.bmin[0] + cv[0] * contours.cs;
            const float recastY = contours.bmin[1] + cv[1] * contours.ch;
            const float recastZ = contours.bmin[2] + cv[2] * contours.cs;
            polygon.push_back({ recastX, recastZ });
            minX = std::min(minX, recastX);
            maxX = std::max(maxX, recastX);
            minY = std::min(minY, recastY);
            maxY = std::max(maxY, recastY);
            minZ = std::min(minZ, recastZ);
            maxZ = std::max(maxZ, recastZ);
        }

        if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
            maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
        {
            continue;
        }

        const bool containsAnchorProjection = PointInPolygonXZ(polygon, anchorRecastX, anchorRecastZ);
        const bool supportBand = maxY >= supportFloorMinY && minY <= supportFloorMaxY;
        const bool competingLower = maxY < support.supportY - supportBandTuning.competingLowerFloorMinDrop;
        if (!containsAnchorProjection && !supportBand && !competingLower)
            continue;

        if (supportBand)
            ++supportCount;
        if (competingLower)
            ++lowerCount;
        if (containsAnchorProjection && supportBand)
            supportContainsAnchor = true;
        if (containsAnchorProjection && competingLower)
            lowerContainsAnchor = true;

        stage["contours"].push_back(
            {
                { "contourIndex", contourIndex },
                { "regionId", static_cast<unsigned int>(contour.reg) },
                { "area", static_cast<unsigned int>(contour.area) },
                { "vertexCount", contour.nverts },
                { "containsAnchorProjection", containsAnchorProjection },
                { "supportBand", supportBand },
                { "competingLower", competingLower },
                { "minX", minX },
                { "maxX", maxX },
                { "minY", minY },
                { "maxY", maxY },
                { "minZ", minZ },
                { "maxZ", maxZ },
            });
    }

    stage["upperSupportExists"] = supportCount > 0;
    stage["lowerCompetitorExists"] = lowerCount > 0;
    stage["supportCandidateCount"] = supportCount;
    stage["lowerCandidateCount"] = lowerCount;
    stage["supportContainsAnchorProjection"] = supportContainsAnchor;
    stage["lowerContainsAnchorProjection"] = lowerContainsAnchor;
    stage["dominantLowerCandidate"] = lowerContainsAnchor && supportCount > 0 && !supportContainsAnchor;
    return stage;
}

static json BuildPolyMeshAnchorStageSummary(const rcPolyMesh& mesh,
    const float xyExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const AnchorSourceSupportProbe& support)
{
    json stage = BuildBaseAnchorStageJson("polymesh", "polymesh", support);
    stage["polys"] = json::array();

    if (!mesh.polys || !mesh.verts || !support.found)
        return stage;

    const float anchorRecastX = support.anchor.wowY;
    const float anchorRecastZ = support.anchor.wowX;
    const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
    const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

    bool supportContainsAnchor = false;
    bool lowerContainsAnchor = false;
    int supportCount = 0;
    int lowerCount = 0;

    for (int polyIndex = 0; polyIndex < mesh.npolys; ++polyIndex)
    {
        if (mesh.areas[polyIndex] == RC_NULL_AREA)
            continue;

        const unsigned short* poly = &mesh.polys[polyIndex * mesh.nvp * 2];
        std::vector<Point2DXZ> polygon;
        polygon.reserve(mesh.nvp);
        float minX = std::numeric_limits<float>::max();
        float maxX = -std::numeric_limits<float>::max();
        float minY = std::numeric_limits<float>::max();
        float maxY = -std::numeric_limits<float>::max();
        float minZ = std::numeric_limits<float>::max();
        float maxZ = -std::numeric_limits<float>::max();

        for (int vertIndex = 0; vertIndex < mesh.nvp; ++vertIndex)
        {
            const unsigned short vertex = poly[vertIndex];
            if (vertex == RC_MESH_NULL_IDX)
                break;

            const unsigned short* v = &mesh.verts[vertex * 3];
            const float recastX = mesh.bmin[0] + v[0] * mesh.cs;
            const float recastY = mesh.bmin[1] + v[1] * mesh.ch;
            const float recastZ = mesh.bmin[2] + v[2] * mesh.cs;
            polygon.push_back({ recastX, recastZ });
            minX = std::min(minX, recastX);
            maxX = std::max(maxX, recastX);
            minY = std::min(minY, recastY);
            maxY = std::max(maxY, recastY);
            minZ = std::min(minZ, recastZ);
            maxZ = std::max(maxZ, recastZ);
        }

        if (polygon.size() < 3)
            continue;

        if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
            maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
        {
            continue;
        }

        const bool containsAnchorProjection = PointInPolygonXZ(polygon, anchorRecastX, anchorRecastZ);
        const bool supportBand = maxY >= supportFloorMinY && minY <= supportFloorMaxY;
        const bool competingLower = maxY < support.supportY - supportBandTuning.competingLowerFloorMinDrop;
        if (!containsAnchorProjection && !supportBand && !competingLower)
            continue;

        if (supportBand)
            ++supportCount;
        if (competingLower)
            ++lowerCount;
        if (containsAnchorProjection && supportBand)
            supportContainsAnchor = true;
        if (containsAnchorProjection && competingLower)
            lowerContainsAnchor = true;

        stage["polys"].push_back(
            {
                { "polyIndex", polyIndex },
                { "regionId", static_cast<unsigned int>(mesh.regs[polyIndex]) },
                { "area", static_cast<unsigned int>(mesh.areas[polyIndex]) },
                { "vertexCount", static_cast<int>(polygon.size()) },
                { "containsAnchorProjection", containsAnchorProjection },
                { "supportBand", supportBand },
                { "competingLower", competingLower },
                { "minX", minX },
                { "maxX", maxX },
                { "minY", minY },
                { "maxY", maxY },
                { "minZ", minZ },
                { "maxZ", maxZ },
            });
    }

    stage["upperSupportExists"] = supportCount > 0;
    stage["lowerCompetitorExists"] = lowerCount > 0;
    stage["supportCandidateCount"] = supportCount;
    stage["lowerCandidateCount"] = lowerCount;
    stage["supportContainsAnchorProjection"] = supportContainsAnchor;
    stage["lowerContainsAnchorProjection"] = lowerContainsAnchor;
    stage["dominantLowerCandidate"] = lowerContainsAnchor && supportCount > 0 && !supportContainsAnchor;
    return stage;
}

// [WWoW-DIVERGENCE] 2026-05-24: support-backed OG hallway/city contours can
// survive rcBuildContours() and then disappear when rcBuildPolyMesh() removes
// border vertices. Preserve only the border vertices on support-band contours
// for explicitly configured anchors so those contours survive the later
// border-vertex removal pass without broad global knob changes.
static int PreserveAnchorSupportContourBorderVertices(
    rcContourSet& contours,
    const float xyExtent,
    const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& supports,
    const bool logDiagnostics)
{
    if (!contours.conts || supports.empty())
        return 0;

    int preservedVertexCount = 0;
    int preservedContourCount = 0;

    for (const AnchorSourceSupportProbe& support : supports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        for (int contourIndex = 0; contourIndex < contours.nconts; ++contourIndex)
        {
            rcContour& contour = contours.conts[contourIndex];
            if (contour.nverts < 3)
                continue;

            float minX = std::numeric_limits<float>::max();
            float maxX = -std::numeric_limits<float>::max();
            float minY = std::numeric_limits<float>::max();
            float maxY = -std::numeric_limits<float>::max();
            float minZ = std::numeric_limits<float>::max();
            float maxZ = -std::numeric_limits<float>::max();
            for (int vertIndex = 0; vertIndex < contour.nverts; ++vertIndex)
            {
                const int* cv = &contour.verts[vertIndex * 4];
                const float recastX = contours.bmin[0] + cv[0] * contours.cs;
                const float recastY = contours.bmin[1] + cv[1] * contours.ch;
                const float recastZ = contours.bmin[2] + cv[2] * contours.cs;
                minX = std::min(minX, recastX);
                maxX = std::max(maxX, recastX);
                minY = std::min(minY, recastY);
                maxY = std::max(maxY, recastY);
                minZ = std::min(minZ, recastZ);
                maxZ = std::max(maxZ, recastZ);
            }

            if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
                maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
            {
                continue;
            }

            const bool supportBand = maxY >= supportFloorMinY && minY <= supportFloorMaxY;
            if (!supportBand)
                continue;

            bool preservedThisContour = false;
            for (int vertIndex = 0; vertIndex < contour.nverts; ++vertIndex)
            {
                int& flags = contour.verts[vertIndex * 4 + 3];
                if ((flags & RC_BORDER_VERTEX) == 0 || (flags & RC_PRESERVE_BORDER_VERTEX) != 0)
                    continue;

                flags |= RC_PRESERVE_BORDER_VERTEX;
                ++preservedVertexCount;
                preservedThisContour = true;
            }

            if (preservedThisContour)
            {
                ++preservedContourCount;
                if (logDiagnostics)
                {
                    printf("[CONTOUR-ANCHOR-PRESERVE] anchor=(%.3f,%.3f,%.3f) contour=%d region=%u preservedBorderVerts=%d\n",
                        support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                        contourIndex, static_cast<unsigned>(contour.reg), contour.nverts);
                }
            }
        }
    }

    if (preservedVertexCount > 0)
    {
        printf("[CONTOUR-ANCHOR-PRESERVE] preserved %d border vertex(s) across %d contour(s)\n",
            preservedVertexCount, preservedContourCount);
    }

    return preservedVertexCount;
}

// [WWoW-DIVERGENCE] 2026-05-24: if a support contour survives rcBuildContours()
// but the simplified contour is much coarser than the raw contour, the
// subsequent rcBuildPolyMesh() triangulation can erase the source-backed upper
// support band entirely. Restoring the raw contour is a focused way to test
// whether the loss happens in contour simplification rather than in later
// polygon merging/culling.
static int RestoreRawAnchorSupportContours(
    rcContourSet& contours,
    const float xyExtent,
    const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& supports,
    const bool logDiagnostics)
{
    if (!contours.conts || supports.empty())
        return 0;

    int restoredContourCount = 0;

    for (const AnchorSourceSupportProbe& support : supports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        for (int contourIndex = 0; contourIndex < contours.nconts; ++contourIndex)
        {
            rcContour& contour = contours.conts[contourIndex];
            if (contour.nrverts < 3 || !contour.verts || !contour.rverts)
                continue;

            float minX = std::numeric_limits<float>::max();
            float maxX = -std::numeric_limits<float>::max();
            float minY = std::numeric_limits<float>::max();
            float maxY = -std::numeric_limits<float>::max();
            float minZ = std::numeric_limits<float>::max();
            float maxZ = -std::numeric_limits<float>::max();
            for (int vertIndex = 0; vertIndex < contour.nverts; ++vertIndex)
            {
                const int* cv = &contour.verts[vertIndex * 4];
                const float recastX = contours.bmin[0] + cv[0] * contours.cs;
                const float recastY = contours.bmin[1] + cv[1] * contours.ch;
                const float recastZ = contours.bmin[2] + cv[2] * contours.cs;
                minX = std::min(minX, recastX);
                maxX = std::max(maxX, recastX);
                minY = std::min(minY, recastY);
                maxY = std::max(maxY, recastY);
                minZ = std::min(minZ, recastZ);
                maxZ = std::max(maxZ, recastZ);
            }

            if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
                maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
            {
                continue;
            }

            const bool supportBand = maxY >= supportFloorMinY && minY <= supportFloorMaxY;
            if (!supportBand)
                continue;

            const int simplifiedVertexCount = contour.nverts;
            int* replacementVerts = static_cast<int*>(rcAlloc(sizeof(int) * contour.nrverts * 4, RC_ALLOC_PERM));
            if (!replacementVerts)
                continue;

            memcpy(replacementVerts, contour.rverts, sizeof(int) * contour.nrverts * 4);
            rcFree(contour.verts);
            contour.verts = replacementVerts;
            contour.nverts = contour.nrverts;
            ++restoredContourCount;

            if (logDiagnostics)
            {
                printf("[CONTOUR-ANCHOR-RAW] anchor=(%.3f,%.3f,%.3f) contour=%d region=%u verts=%d->%d\n",
                    support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                    contourIndex, static_cast<unsigned>(contour.reg), simplifiedVertexCount, contour.nrverts);
            }
        }
    }

    if (restoredContourCount > 0)
    {
        printf("[CONTOUR-ANCHOR-RAW] restored raw vertices on %d support contour(s)\n", restoredContourCount);
    }

    return restoredContourCount;
}

static float DistancePtSeg2D(const int x, const int z,
    const int px, const int pz,
    const int qx, const int qz)
{
    float pqx = static_cast<float>(qx - px);
    float pqz = static_cast<float>(qz - pz);
    float dx = static_cast<float>(x - px);
    float dz = static_cast<float>(z - pz);
    float d = pqx * pqx + pqz * pqz;
    float t = pqx * dx + pqz * dz;
    if (d > 0.0f)
        t /= d;
    if (t < 0.0f)
        t = 0.0f;
    else if (t > 1.0f)
        t = 1.0f;

    dx = px + t * pqx - x;
    dz = pz + t * pqz - z;
    return dx * dx + dz * dz;
}

static void SimplifyAnchorContour(
    const std::vector<int>& points,
    std::vector<int>& simplified,
    const float maxError,
    const int maxEdgeLen,
    const int buildFlags)
{
    const int pointCount = static_cast<int>(points.size()) / 4;
    if (pointCount < 3)
        return;

    bool hasConnections = false;
    for (int i = 0; i < static_cast<int>(points.size()); i += 4)
    {
        if ((points[i + 3] & RC_CONTOUR_REG_MASK) != 0)
        {
            hasConnections = true;
            break;
        }
    }

    if (hasConnections)
    {
        for (int i = 0; i < pointCount; ++i)
        {
            const int ii = (i + 1) % pointCount;
            const bool differentRegs =
                (points[i * 4 + 3] & RC_CONTOUR_REG_MASK) != (points[ii * 4 + 3] & RC_CONTOUR_REG_MASK);
            const bool areaBorders =
                (points[i * 4 + 3] & RC_AREA_BORDER) != (points[ii * 4 + 3] & RC_AREA_BORDER);
            if (differentRegs || areaBorders)
            {
                simplified.push_back(points[i * 4 + 0]);
                simplified.push_back(points[i * 4 + 1]);
                simplified.push_back(points[i * 4 + 2]);
                simplified.push_back(i);
            }
        }
    }

    if (simplified.empty())
    {
        int llx = points[0];
        int lly = points[1];
        int llz = points[2];
        int lli = 0;
        int urx = points[0];
        int ury = points[1];
        int urz = points[2];
        int uri = 0;
        for (int i = 0; i < static_cast<int>(points.size()); i += 4)
        {
            const int x = points[i + 0];
            const int y = points[i + 1];
            const int z = points[i + 2];
            if (x < llx || (x == llx && z < llz))
            {
                llx = x;
                lly = y;
                llz = z;
                lli = i / 4;
            }
            if (x > urx || (x == urx && z > urz))
            {
                urx = x;
                ury = y;
                urz = z;
                uri = i / 4;
            }
        }

        simplified.push_back(llx);
        simplified.push_back(lly);
        simplified.push_back(llz);
        simplified.push_back(lli);

        simplified.push_back(urx);
        simplified.push_back(ury);
        simplified.push_back(urz);
        simplified.push_back(uri);
    }

    for (int i = 0; i < static_cast<int>(simplified.size()) / 4; )
    {
        const int ii = (i + 1) % (static_cast<int>(simplified.size()) / 4);

        int ax = simplified[i * 4 + 0];
        int az = simplified[i * 4 + 2];
        const int ai = simplified[i * 4 + 3];

        int bx = simplified[ii * 4 + 0];
        int bz = simplified[ii * 4 + 2];
        const int bi = simplified[ii * 4 + 3];

        float maxd = 0.0f;
        int maxi = -1;
        int ci;
        int cinc;
        int endi;

        if (bx > ax || (bx == ax && bz > az))
        {
            cinc = 1;
            ci = (ai + cinc) % pointCount;
            endi = bi;
        }
        else
        {
            cinc = pointCount - 1;
            ci = (bi + cinc) % pointCount;
            endi = ai;
            std::swap(ax, bx);
            std::swap(az, bz);
        }

        if ((points[ci * 4 + 3] & RC_CONTOUR_REG_MASK) == 0 || (points[ci * 4 + 3] & RC_AREA_BORDER))
        {
            while (ci != endi)
            {
                const float d = DistancePtSeg2D(points[ci * 4 + 0], points[ci * 4 + 2], ax, az, bx, bz);
                if (d > maxd)
                {
                    maxd = d;
                    maxi = ci;
                }
                ci = (ci + cinc) % pointCount;
            }
        }

        if (maxi != -1 && maxd > (maxError * maxError))
        {
            simplified.resize(simplified.size() + 4);
            const int simplifiedCount = static_cast<int>(simplified.size()) / 4;
            for (int j = simplifiedCount - 1; j > i; --j)
            {
                simplified[j * 4 + 0] = simplified[(j - 1) * 4 + 0];
                simplified[j * 4 + 1] = simplified[(j - 1) * 4 + 1];
                simplified[j * 4 + 2] = simplified[(j - 1) * 4 + 2];
                simplified[j * 4 + 3] = simplified[(j - 1) * 4 + 3];
            }
            simplified[(i + 1) * 4 + 0] = points[maxi * 4 + 0];
            simplified[(i + 1) * 4 + 1] = points[maxi * 4 + 1];
            simplified[(i + 1) * 4 + 2] = points[maxi * 4 + 2];
            simplified[(i + 1) * 4 + 3] = maxi;
        }
        else
        {
            ++i;
        }
    }

    if (maxEdgeLen > 0 && (buildFlags & (RC_CONTOUR_TESS_WALL_EDGES | RC_CONTOUR_TESS_AREA_EDGES)) != 0)
    {
        for (int i = 0; i < static_cast<int>(simplified.size()) / 4; )
        {
            const int ii = (i + 1) % (static_cast<int>(simplified.size()) / 4);

            const int ax = simplified[i * 4 + 0];
            const int az = simplified[i * 4 + 2];
            const int ai = simplified[i * 4 + 3];

            const int bx = simplified[ii * 4 + 0];
            const int bz = simplified[ii * 4 + 2];
            const int bi = simplified[ii * 4 + 3];

            int maxi = -1;
            const int ci = (ai + 1) % pointCount;

            bool tess = false;
            if ((buildFlags & RC_CONTOUR_TESS_WALL_EDGES) && (points[ci * 4 + 3] & RC_CONTOUR_REG_MASK) == 0)
                tess = true;
            if ((buildFlags & RC_CONTOUR_TESS_AREA_EDGES) && (points[ci * 4 + 3] & RC_AREA_BORDER))
                tess = true;

            if (tess)
            {
                const int dx = bx - ax;
                const int dz = bz - az;
                if (dx * dx + dz * dz > maxEdgeLen * maxEdgeLen)
                {
                    const int n = bi < ai ? (bi + pointCount - ai) : (bi - ai);
                    if (n > 1)
                    {
                        if (bx > ax || (bx == ax && bz > az))
                            maxi = (ai + n / 2) % pointCount;
                        else
                            maxi = (ai + (n + 1) / 2) % pointCount;
                    }
                }
            }

            if (maxi != -1)
            {
                simplified.resize(simplified.size() + 4);
                const int simplifiedCount = static_cast<int>(simplified.size()) / 4;
                for (int j = simplifiedCount - 1; j > i; --j)
                {
                    simplified[j * 4 + 0] = simplified[(j - 1) * 4 + 0];
                    simplified[j * 4 + 1] = simplified[(j - 1) * 4 + 1];
                    simplified[j * 4 + 2] = simplified[(j - 1) * 4 + 2];
                    simplified[j * 4 + 3] = simplified[(j - 1) * 4 + 3];
                }
                simplified[(i + 1) * 4 + 0] = points[maxi * 4 + 0];
                simplified[(i + 1) * 4 + 1] = points[maxi * 4 + 1];
                simplified[(i + 1) * 4 + 2] = points[maxi * 4 + 2];
                simplified[(i + 1) * 4 + 3] = maxi;
            }
            else
            {
                ++i;
            }
        }
    }

}

static void RemoveAnchorContourDegenerateSegments(std::vector<int>& simplified)
{
    auto vertsEqualXz = [&](const int lhsIndex, const int rhsIndex)
    {
        return simplified[lhsIndex * 4 + 0] == simplified[rhsIndex * 4 + 0] &&
            simplified[lhsIndex * 4 + 2] == simplified[rhsIndex * 4 + 2];
    };

    int pointCount = static_cast<int>(simplified.size()) / 4;
    for (int i = 0; i < pointCount; ++i)
    {
        const int ni = (i + 1 < pointCount) ? (i + 1) : 0;
        if (!vertsEqualXz(i, ni))
            continue;

        for (int j = i; j < pointCount - 1; ++j)
        {
            simplified[j * 4 + 0] = simplified[(j + 1) * 4 + 0];
            simplified[j * 4 + 1] = simplified[(j + 1) * 4 + 1];
            simplified[j * 4 + 2] = simplified[(j + 1) * 4 + 2];
            simplified[j * 4 + 3] = simplified[(j + 1) * 4 + 3];
        }
        simplified.resize(simplified.size() - 4);
        --pointCount;
        --i;
    }
}

static void FinalizeAnchorContourFlags(
    const std::vector<int>& points,
    std::vector<int>& simplified)
{
    const int pointCount = static_cast<int>(points.size()) / 4;
    if (pointCount < 3)
        return;

    for (int i = 0; i < static_cast<int>(simplified.size()) / 4; ++i)
    {
        const int ai = (simplified[i * 4 + 3] + 1) % pointCount;
        const int bi = simplified[i * 4 + 3];
        simplified[i * 4 + 3] =
            (points[ai * 4 + 3] & (RC_CONTOUR_REG_MASK | RC_AREA_BORDER)) |
            (points[bi * 4 + 3] & RC_BORDER_VERTEX);
    }
}

// [WWoW-DIVERGENCE] 2026-05-24: the fully raw 448-vertex support contour is
// too fragmented, while upstream-style resimplify falls back to a near-coarse
// 21/22-vertex contour. Preserve all raw contour vertices only within a small
// local window around the configured anchor so we can hold detail near the
// bad hallway footprint without exploding the entire contour back to raw.
static int InjectAnchorLocalRawVertices(
    const std::vector<int>& points,
    std::vector<int>& simplified,
    const float anchorCellX,
    const float anchorCellZ,
    const float preserveRadiusCells)
{
    if (preserveRadiusCells <= 0.0f)
        return 0;

    const int pointCount = static_cast<int>(points.size()) / 4;
    const int simplifiedCount = static_cast<int>(simplified.size()) / 4;
    if (pointCount < 3 || simplifiedCount < 2)
        return 0;

    const float preserveRadiusSq = preserveRadiusCells * preserveRadiusCells;
    auto rawPointWithinWindow = [&](const int rawIndex)
    {
        const float dx = static_cast<float>(points[rawIndex * 4 + 0]) - anchorCellX;
        const float dz = static_cast<float>(points[rawIndex * 4 + 2]) - anchorCellZ;
        return dx * dx + dz * dz <= preserveRadiusSq;
    };

    std::vector<int> expanded;
    expanded.reserve(points.size());

    auto appendRawIndex = [&](const int rawIndex)
    {
        if (!expanded.empty())
        {
            const int lastIndex = static_cast<int>(expanded.size()) / 4 - 1;
            if (expanded[lastIndex * 4 + 0] == points[rawIndex * 4 + 0] &&
                expanded[lastIndex * 4 + 2] == points[rawIndex * 4 + 2])
            {
                return false;
            }
        }

        expanded.push_back(points[rawIndex * 4 + 0]);
        expanded.push_back(points[rawIndex * 4 + 1]);
        expanded.push_back(points[rawIndex * 4 + 2]);
        expanded.push_back(rawIndex);
        return true;
    };

    int injectedVertexCount = 0;
    for (int i = 0; i < simplifiedCount; ++i)
    {
        const int ii = (i + 1) % simplifiedCount;
        const int startRawIndex = simplified[i * 4 + 3];
        const int endRawIndex = simplified[ii * 4 + 3];

        appendRawIndex(startRawIndex);
        int rawIndex = (startRawIndex + 1) % pointCount;
        while (rawIndex != endRawIndex)
        {
            if (rawPointWithinWindow(rawIndex) && appendRawIndex(rawIndex))
                ++injectedVertexCount;

            rawIndex = (rawIndex + 1) % pointCount;
        }
    }

    if (!expanded.empty())
        simplified.swap(expanded);

    return injectedVertexCount;
}

// [WWoW-DIVERGENCE] 2026-05-24: the raw-restored 1523.8 support contour proves
// the upper floor can survive contour generation, but the fully raw contour
// over-fragments into final support shards. Re-run Recast's own contour
// simplifier locally on just the selected raw-restored support contours so we
// can test a middle ground between the coarse default contour and the raw
// contour without changing global tile simplification.
static int ResimplifyRawAnchorSupportContours(
    rcContourSet& contours,
    const float xyExtent,
    const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const std::vector<AnchorSourceSupportProbe>& supports,
    const float maxError,
    const int maxEdgeLen,
    const float localPreserveRadius,
    const int buildFlags,
    const bool logDiagnostics)
{
    if (!contours.conts || supports.empty() || maxError < 0.0f)
        return 0;

    int resimplifiedContourCount = 0;
    int removedVertexCount = 0;
    std::vector<unsigned char> processed(static_cast<size_t>(contours.nconts), 0);

    for (const AnchorSourceSupportProbe& support : supports)
    {
        if (!support.found)
            continue;

        const float anchorRecastX = support.anchor.wowY;
        const float anchorRecastZ = support.anchor.wowX;
        const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
        const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);

        for (int contourIndex = 0; contourIndex < contours.nconts; ++contourIndex)
        {
            if (processed[static_cast<size_t>(contourIndex)] != 0)
                continue;

            rcContour& contour = contours.conts[contourIndex];
            if (contour.nrverts < 3 || !contour.verts || !contour.rverts)
                continue;

            float minX = std::numeric_limits<float>::max();
            float maxX = -std::numeric_limits<float>::max();
            float minY = std::numeric_limits<float>::max();
            float maxY = -std::numeric_limits<float>::max();
            float minZ = std::numeric_limits<float>::max();
            float maxZ = -std::numeric_limits<float>::max();
            for (int vertIndex = 0; vertIndex < contour.nverts; ++vertIndex)
            {
                const int* cv = &contour.verts[vertIndex * 4];
                const float recastX = contours.bmin[0] + cv[0] * contours.cs;
                const float recastY = contours.bmin[1] + cv[1] * contours.ch;
                const float recastZ = contours.bmin[2] + cv[2] * contours.cs;
                minX = std::min(minX, recastX);
                maxX = std::max(maxX, recastX);
                minY = std::min(minY, recastY);
                maxY = std::max(maxY, recastY);
                minZ = std::min(minZ, recastZ);
                maxZ = std::max(maxZ, recastZ);
            }

            if (maxX < anchorRecastX - xyExtent || minX > anchorRecastX + xyExtent ||
                maxZ < anchorRecastZ - xyExtent || minZ > anchorRecastZ + xyExtent)
            {
                continue;
            }

            const bool supportBand = maxY >= supportFloorMinY && minY <= supportFloorMaxY;
            if (!supportBand)
                continue;

            std::vector<int> rawPoints(
                contour.rverts,
                contour.rverts + static_cast<size_t>(contour.nrverts) * 4);
            std::vector<int> simplified;
            simplified.reserve(rawPoints.size());
            SimplifyAnchorContour(rawPoints, simplified, maxError, maxEdgeLen, buildFlags);
            int localInjectedVertexCount = 0;
            if (localPreserveRadius > 0.0f)
            {
                const float anchorCellX = (anchorRecastX - contours.bmin[0]) / contours.cs;
                const float anchorCellZ = (anchorRecastZ - contours.bmin[2]) / contours.cs;
                const float preserveRadiusCells = localPreserveRadius / contours.cs;
                localInjectedVertexCount = InjectAnchorLocalRawVertices(
                    rawPoints, simplified, anchorCellX, anchorCellZ, preserveRadiusCells);
            }
            RemoveAnchorContourDegenerateSegments(simplified);
            FinalizeAnchorContourFlags(rawPoints, simplified);

            const int simplifiedVertexCount = static_cast<int>(simplified.size()) / 4;
            if (logDiagnostics)
            {
                printf("[CONTOUR-ANCHOR-RESIMPLIFY-CANDIDATE] anchor=(%.3f,%.3f,%.3f) contour=%d region=%u rawVerts=%d candidateVerts=%d maxError=%.3f maxEdgeLen=%d buildFlags=0x%02X\n",
                    support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                    contourIndex, static_cast<unsigned>(contour.reg),
                    contour.nrverts, simplifiedVertexCount,
                    maxError, maxEdgeLen, static_cast<unsigned>(buildFlags));
                if (localInjectedVertexCount > 0)
                {
                    printf("[CONTOUR-ANCHOR-LOCAL-RAW] anchor=(%.3f,%.3f,%.3f) contour=%d region=%u injectedRawVerts=%d preserveRadius=%.3f\n",
                        support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                        contourIndex, static_cast<unsigned>(contour.reg),
                        localInjectedVertexCount, localPreserveRadius);
                }
            }
            if (simplifiedVertexCount < 3 || simplifiedVertexCount >= contour.nverts)
                continue;

            int* replacementVerts = static_cast<int*>(rcAlloc(sizeof(int) * simplified.size(), RC_ALLOC_PERM));
            if (!replacementVerts)
                continue;

            memcpy(replacementVerts, simplified.data(), sizeof(int) * simplified.size());
            const int priorVertexCount = contour.nverts;
            rcFree(contour.verts);
            contour.verts = replacementVerts;
            contour.nverts = simplifiedVertexCount;
            processed[static_cast<size_t>(contourIndex)] = 1;
            ++resimplifiedContourCount;
            removedVertexCount += (priorVertexCount - simplifiedVertexCount);

            if (logDiagnostics)
            {
                printf("[CONTOUR-ANCHOR-RESIMPLIFY] anchor=(%.3f,%.3f,%.3f) contour=%d region=%u verts=%d->%d maxError=%.3f maxEdgeLen=%d\n",
                    support.anchor.wowX, support.anchor.wowY, support.anchor.wowZ,
                    contourIndex, static_cast<unsigned>(contour.reg),
                    priorVertexCount, simplifiedVertexCount, maxError, maxEdgeLen);
            }
        }
    }

    if (resimplifiedContourCount > 0)
    {
        printf("[CONTOUR-ANCHOR-RESIMPLIFY] resimplified %d contour(s), removed %d vertex(s), maxError=%.3f maxEdgeLen=%d\n",
            resimplifiedContourCount, removedVertexCount, maxError, maxEdgeLen);
    }

    return resimplifiedContourCount;
}

struct FinalDetourGroundComponentInfo
{
    int componentId = -1;
    int polyCount = 0;
    float totalHorizontalArea2D = 0.0f;
};

struct AnchorRouteTargetResolution
{
    const AnchorRouteTarget* routeTarget = nullptr;
    bool resolved = false;
    dtPolyRef polyRef = 0;
    float closestPoint[3] = { 0.0f, 0.0f, 0.0f };
    float closestDistance2D = -1.0f;
};

struct FinalDetourComponentRouteability
{
    int componentId = -1;
    int representativePolyIndex = -1;
    dtPolyRef representativePolyRef = 0;
    bool representativePosOverPoly = false;
    float representativeClosestDistance2D = std::numeric_limits<float>::max();
    float representativePoint[3] = { 0.0f, 0.0f, 0.0f };
    bool hasRepresentativePoint = false;
    bool routeableToAnyTarget = false;
    int routeableTargetCount = 0;
    json routeTargets = json::array();
};

static void BuildFinalDetourGroundComponents(dtNavMesh& navMesh, const dtMeshTile& tile,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask,
    std::vector<int>& componentIds, std::vector<FinalDetourGroundComponentInfo>& components)
{
    componentIds.assign(tile.header ? tile.header->polyCount : 0, -1);
    components.clear();

    if (!tile.header)
        return;

    std::vector<unsigned char> visited(tile.header->polyCount, 0);
    std::vector<int> pending;

    for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
    {
        if (visited[polyIndex] || !liveGroundMask[polyIndex] || !IsWalkableLandPoly(tile.polys[polyIndex]))
            continue;

        FinalDetourGroundComponentInfo component;
        component.componentId = static_cast<int>(components.size());
        pending.clear();
        pending.push_back(polyIndex);
        visited[polyIndex] = 1;

        while (!pending.empty())
        {
            const int currentPolyIndex = pending.back();
            pending.pop_back();

            componentIds[currentPolyIndex] = component.componentId;
            ++component.polyCount;
            component.totalHorizontalArea2D += diagnostics[currentPolyIndex].horizontalArea2D;

            const dtPoly& poly = tile.polys[currentPolyIndex];
            const dtPolyRef currentPolyRef = navMesh.getPolyRefBase(&tile) | static_cast<dtPolyRef>(currentPolyIndex);
            for (int edgeIndex = 0; edgeIndex < poly.vertCount; ++edgeIndex)
            {
                const unsigned short neighbor = poly.neis[edgeIndex];
                if (neighbor == 0 || (neighbor & DT_EXT_LINK) != 0)
                    continue;

                const int neighborIndex = static_cast<int>(neighbor) - 1;
                if (neighborIndex < 0 || neighborIndex >= tile.header->polyCount)
                    continue;

                if (visited[neighborIndex] || !liveGroundMask[neighborIndex] || !IsWalkableLandPoly(tile.polys[neighborIndex]))
                    continue;

                visited[neighborIndex] = 1;
                pending.push_back(neighborIndex);
            }

            for (unsigned int linkIndex = poly.firstLink; linkIndex != DT_NULL_LINK; linkIndex = tile.links[linkIndex].next)
            {
                const dtLink& link = tile.links[linkIndex];
                if (link.ref == 0 || link.ref == currentPolyRef)
                    continue;

                const dtMeshTile* linkedTile = nullptr;
                const dtPoly* linkedPoly = nullptr;
                if (dtStatusFailed(navMesh.getTileAndPolyByRef(link.ref, &linkedTile, &linkedPoly)) || linkedTile != &tile || !linkedPoly)
                    continue;

                const int neighborIndex = static_cast<int>(linkedPoly - tile.polys);
                if (neighborIndex < 0 || neighborIndex >= tile.header->polyCount)
                    continue;

                if (visited[neighborIndex] || !liveGroundMask[neighborIndex] || !IsWalkableLandPoly(tile.polys[neighborIndex]))
                    continue;

                visited[neighborIndex] = 1;
                pending.push_back(neighborIndex);
            }
        }

        components.push_back(component);
    }
}

static dtQueryFilter BuildGroundOnlyFilter()
{
    dtQueryFilter filter;
    filter.setIncludeFlags(NAV_GROUND);
    filter.setExcludeFlags(0);
    return filter;
}

static bool TryFindWalkableGroundPolyAtCoord(dtNavMeshQuery& query, const float detourPos[3], dtPolyRef& polyRef,
    float closestPoint[3], float& closestDistance2D)
{
    const float extents[3] =
    {
        ANCHOR_ROUTE_TARGET_NEAREST_XY_EXTENT,
        ANCHOR_ROUTE_TARGET_NEAREST_Z_EXTENT,
        ANCHOR_ROUTE_TARGET_NEAREST_XY_EXTENT
    };
    dtQueryFilter filter = BuildGroundOnlyFilter();
    if (dtStatusFailed(query.findNearestPoly(detourPos, extents, &filter, &polyRef, closestPoint)) || polyRef == 0)
        return false;

    if (closestPoint[1] > detourPos[1] + ANCHOR_ROUTE_TARGET_MAX_HEIGHT_ABOVE)
        return false;

    const float deltaX = closestPoint[0] - detourPos[0];
    const float deltaZ = closestPoint[2] - detourPos[2];
    closestDistance2D = sqrtf(deltaX * deltaX + deltaZ * deltaZ);
    return true;
}

static std::vector<AnchorRouteTargetResolution> ResolveAnchorRouteTargetsForAnchor(
    dtNavMeshQuery& query,
    const AnchorPolyStackCoord& anchor,
    const std::vector<AnchorRouteTarget>& routeTargets)
{
    std::vector<AnchorRouteTargetResolution> resolvedTargets;
    const std::vector<const AnchorRouteTarget*> matches = FindAnchorRouteTargets(routeTargets, anchor);
    resolvedTargets.reserve(matches.size());

    for (const AnchorRouteTarget* routeTarget : matches)
    {
        AnchorRouteTargetResolution resolved;
        resolved.routeTarget = routeTarget;
        const float detourTargetPos[3] = { routeTarget->target.wowY, routeTarget->target.wowZ, routeTarget->target.wowX };
        resolved.resolved = TryFindWalkableGroundPolyAtCoord(
            query, detourTargetPos, resolved.polyRef, resolved.closestPoint, resolved.closestDistance2D);
        resolvedTargets.push_back(resolved);
    }

    return resolvedTargets;
}

static void EvaluateFinalDetourAnchorComponentRouteability(dtNavMeshQuery& query,
    const AnchorPolyStackCoord& anchor,
    const std::vector<AnchorRouteTarget>& routeTargets,
    const std::vector<int>& windowPolyIndices,
    const std::vector<AnchorPolyStackProbeResult>& probeResults,
    const std::vector<int>& componentIds,
    const dtPolyRef tileRefBase,
    std::unordered_map<int, FinalDetourComponentRouteability>& routeabilityByComponent,
    std::vector<AnchorRouteTargetResolution>& resolvedTargets)
{
    routeabilityByComponent.clear();
    resolvedTargets = ResolveAnchorRouteTargetsForAnchor(query, anchor, routeTargets);
    if (resolvedTargets.empty())
        return;

    for (size_t probeIndex = 0; probeIndex < windowPolyIndices.size() && probeIndex < probeResults.size(); ++probeIndex)
    {
        const int polyIndex = windowPolyIndices[probeIndex];
        if (polyIndex < 0 || polyIndex >= static_cast<int>(componentIds.size()))
            continue;

        const int componentId = componentIds[polyIndex];
        if (componentId < 0)
            continue;

        FinalDetourComponentRouteability& routeability = routeabilityByComponent[componentId];
        if (routeability.componentId < 0)
            routeability.componentId = componentId;

        const AnchorPolyStackProbeResult& probe = probeResults[probeIndex];
        const float candidateDistance = probe.hasClosestPoint ? probe.closestDistance2D : std::numeric_limits<float>::max();
        const bool preferCandidate =
            !routeability.hasRepresentativePoint ||
            (probe.posOverPoly && !routeability.representativePosOverPoly) ||
            (probe.posOverPoly == routeability.representativePosOverPoly &&
                candidateDistance < routeability.representativeClosestDistance2D);
        if (!preferCandidate)
            continue;

        routeability.representativePolyIndex = polyIndex;
        routeability.representativePolyRef = tileRefBase | static_cast<dtPolyRef>(polyIndex);
        routeability.representativePosOverPoly = probe.posOverPoly;
        routeability.representativeClosestDistance2D = candidateDistance;
        routeability.hasRepresentativePoint = false;
        if (probe.hasClosestPoint)
        {
            rcVcopy(routeability.representativePoint, probe.closestPoint);
            routeability.hasRepresentativePoint = true;
        }
    }

    dtQueryFilter filter = BuildGroundOnlyFilter();
    std::vector<dtPolyRef> path(ANCHOR_ROUTE_MAX_PATH_POLYS);
    for (auto& entry : routeabilityByComponent)
    {
        FinalDetourComponentRouteability& routeability = entry.second;
        routeability.routeTargets = json::array();

        if (!routeability.hasRepresentativePoint || routeability.representativePolyRef == 0)
            continue;

        for (const AnchorRouteTargetResolution& target : resolvedTargets)
        {
            json targetJson =
            {
                { "label", target.routeTarget ? target.routeTarget->label : "" },
                { "targetId", target.routeTarget ? FormatAnchorStageId(target.routeTarget->target) : "" },
                { "resolved", target.resolved },
                { "targetPolyRef", target.resolved ? FormatPolyRefHex(target.polyRef) : "" },
                { "targetClosestDistance2D", target.resolved ? target.closestDistance2D : -1.0f },
                { "pathPolyCount", 0 },
                { "reachesTarget", false },
                { "statusHex", "" },
            };

            if (!target.resolved)
            {
                routeability.routeTargets.push_back(targetJson);
                continue;
            }

            int pathCount = 0;
            const dtStatus pathStatus = query.findPath(
                routeability.representativePolyRef,
                target.polyRef,
                routeability.representativePoint,
                target.closestPoint,
                &filter,
                path.data(),
                &pathCount,
                ANCHOR_ROUTE_MAX_PATH_POLYS);
            const bool reachesTarget = pathCount > 0 && path[pathCount - 1] == target.polyRef;

            char statusHex[32];
            sprintf(statusHex, "0x%X", static_cast<unsigned int>(pathStatus));
            targetJson["pathPolyCount"] = pathCount;
            targetJson["reachesTarget"] = reachesTarget;
            targetJson["statusHex"] = statusHex;

            if (reachesTarget)
            {
                routeability.routeableToAnyTarget = true;
                ++routeability.routeableTargetCount;
            }

            routeability.routeTargets.push_back(targetJson);
        }
    }
}

static json BuildFinalDetourAnchorStageSummary(dtNavMesh& navMesh, const dtMeshTile& tile,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask,
    const float xyExtent, const float zExtent, const float supportZTolerance,
    const AnchorSupportBandTuning& supportBandTuning,
    const AnchorSourceSupportProbe& support,
    const std::vector<AnchorRouteTarget>& routeTargets)
{
    json stage = BuildBaseAnchorStageJson("finalDetour", "final-detour", support);
    stage["candidates"] = json::array();

    if (!tile.header || !support.found)
        return stage;

    std::unique_ptr<dtNavMeshQuery, decltype(&dtFreeNavMeshQuery)> query(dtAllocNavMeshQuery(), &dtFreeNavMeshQuery);
    if (!query || dtStatusFailed(query->init(&navMesh, ANCHOR_ROUTE_QUERY_MAX_NODES)))
    {
        stage["unprovenReason"] = "query_init_failed";
        return stage;
    }

    const float detourPos[3] = { support.anchor.wowY, support.anchor.wowZ, support.anchor.wowX };
    const float supportFloorMinY = GetAnchorSupportFloorMinY(support, supportBandTuning);
    const float supportFloorMaxY = GetAnchorSupportFloorMaxY(support, supportZTolerance, supportBandTuning);
    const dtPolyRef tileRefBase = navMesh.getPolyRefBase(&tile);
    std::vector<int> componentIds;
    std::vector<FinalDetourGroundComponentInfo> components;
    BuildFinalDetourGroundComponents(navMesh, tile, diagnostics, liveGroundMask, componentIds, components);

    bool supportContainsAnchor = false;
    bool lowerContainsAnchor = false;
    int supportCount = 0;
    int supportBandCount = 0;
    int lowerCount = 0;
    std::unordered_set<int> supportComponentIds;
    std::unordered_set<int> lowerComponentIds;
    std::unordered_map<dtPolyRef, json> candidateByRef;
    std::vector<dtPolyRef> candidateOrder;
    std::vector<int> windowPolyIndices;
    std::vector<AnchorPolyStackProbeResult> windowProbeResults;

    for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
    {
        if (!liveGroundMask[polyIndex])
            continue;

        const dtPoly& poly = tile.polys[polyIndex];
        if (!IsWalkableLandPoly(poly))
            continue;

        if (!IntersectsAnchorWindow(diagnostics[polyIndex], support.anchor.wowY, support.anchor.wowZ, support.anchor.wowX, xyExtent, zExtent))
            continue;

        const dtPolyRef polyRef = tileRefBase | static_cast<dtPolyRef>(polyIndex);
        const AnchorPolyStackProbeResult probe = ProbeAnchorPolyAtCoord(*query, polyRef, detourPos);
        windowPolyIndices.push_back(polyIndex);
        windowProbeResults.push_back(probe);
        float supportSurfaceZ = 0.0f;
        bool usedClosestFallback = false;
        const bool hasSurface = TryGetAnchorProbeSupportSurfaceZ(probe, supportSurfaceZ, usedClosestFallback);
        const bool supportBand = IntersectsAnchorSupportBand(diagnostics[polyIndex], supportFloorMinY, supportFloorMaxY);
        const bool supportCandidate = (hasSurface && supportSurfaceZ >= supportFloorMinY && supportSurfaceZ <= supportFloorMaxY) ||
            (probe.posOverPoly && supportBand);
        const bool lowerCandidate = hasSurface && IsAnchorCompetingLowerFloor(supportSurfaceZ, support, supportBandTuning);

        if (supportBand)
            ++supportBandCount;
        if (supportCandidate)
            ++supportCount;
        if (lowerCandidate)
            ++lowerCount;
        const int componentId =
            polyIndex >= 0 && polyIndex < static_cast<int>(componentIds.size())
                ? componentIds[polyIndex]
                : -1;
        const FinalDetourGroundComponentInfo* componentInfo =
            componentId >= 0 && componentId < static_cast<int>(components.size())
                ? &components[componentId]
                : nullptr;
        if (supportCandidate && componentId >= 0)
            supportComponentIds.insert(componentId);
        if (lowerCandidate && componentId >= 0)
            lowerComponentIds.insert(componentId);
        if (probe.posOverPoly && supportCandidate)
            supportContainsAnchor = true;
        else if (probe.posOverPoly && supportBand)
            supportContainsAnchor = true;
        if (probe.posOverPoly && lowerCandidate)
            lowerContainsAnchor = true;

        json candidate =
        {
            { "polyIndex", polyIndex },
            { "polyRef", FormatPolyRefHex(polyRef) },
            { "posOverPoly", probe.posOverPoly },
            { "hasSurfaceZ", probe.hasSurfaceZ },
            { "hasClosestPoint", probe.hasClosestPoint },
            { "closestDistance2D", probe.hasClosestPoint ? probe.closestDistance2D : -1.0f },
            { "surfaceZ", hasSurface ? supportSurfaceZ : 0.0f },
            { "surfaceFromClosestFallback", hasSurface && usedClosestFallback },
            { "supportBandCandidate", supportBand },
            { "supportCandidate", supportCandidate },
            { "competingLower", lowerCandidate },
            { "minY", diagnostics[polyIndex].minY },
            { "maxY", diagnostics[polyIndex].maxY },
            { "horizontalArea2D", diagnostics[polyIndex].horizontalArea2D },
            { "maxEdge2D", diagnostics[polyIndex].maxEdge2D },
            { "containsAnchorProjection", probe.posOverPoly },
            { "componentId", componentId },
            { "componentPolyCount", componentInfo ? componentInfo->polyCount : 0 },
            { "componentArea2D", componentInfo ? componentInfo->totalHorizontalArea2D : 0.0f },
        };
        candidateByRef[polyRef] = candidate;
        candidateOrder.push_back(polyRef);
    }

    std::unordered_map<int, FinalDetourComponentRouteability> routeabilityByComponent;
    std::vector<AnchorRouteTargetResolution> resolvedTargets;
    EvaluateFinalDetourAnchorComponentRouteability(
        *query, support.anchor, routeTargets, windowPolyIndices, windowProbeResults,
        componentIds, tileRefBase, routeabilityByComponent, resolvedTargets);

    int resolvedRouteTargetCount = 0;
    stage["routeTargets"] = json::array();
    for (const AnchorRouteTargetResolution& target : resolvedTargets)
    {
        if (target.resolved)
            ++resolvedRouteTargetCount;

        stage["routeTargets"].push_back(
            {
                { "label", target.routeTarget ? target.routeTarget->label : "" },
                { "targetId", target.routeTarget ? FormatAnchorStageId(target.routeTarget->target) : "" },
                { "resolved", target.resolved },
                { "targetPolyRef", target.resolved ? FormatPolyRefHex(target.polyRef) : "" },
                { "targetClosestDistance2D", target.resolved ? target.closestDistance2D : -1.0f },
            });
    }

    int routeableCandidateCount = 0;
    int routeableSupportCandidateCount = 0;
    int routeableLowerCandidateCount = 0;
    std::unordered_set<int> routeableSupportComponentIds;
    for (const dtPolyRef polyRef : candidateOrder)
    {
        json& candidate = candidateByRef[polyRef];
        const int componentId = candidate.value("componentId", -1);
        const auto routeabilityIt = routeabilityByComponent.find(componentId);
        const FinalDetourComponentRouteability* routeability =
            routeabilityIt != routeabilityByComponent.end() ? &routeabilityIt->second : nullptr;
        const bool routeableToAnyTarget = routeability ? routeability->routeableToAnyTarget : false;
        const int routeableTargetCount = routeability ? routeability->routeableTargetCount : 0;
        candidate["routeableToAnyTarget"] = routeableToAnyTarget;
        candidate["routeableTargetCount"] = routeableTargetCount;
        candidate["routeTargets"] = routeability ? routeability->routeTargets : json::array();

        if (routeableToAnyTarget)
        {
            ++routeableCandidateCount;
            if (candidate.value("supportCandidate", false))
            {
                ++routeableSupportCandidateCount;
                if (componentId >= 0)
                    routeableSupportComponentIds.insert(componentId);
            }
            if (candidate.value("competingLower", false))
                ++routeableLowerCandidateCount;
        }

        stage["candidates"].push_back(candidate);
    }

    stage["upperSupportExists"] = supportBandCount > 0;
    stage["lowerCompetitorExists"] = lowerCount > 0;
    stage["supportCandidateCount"] = supportCount;
    stage["supportBandCandidateCount"] = supportBandCount;
    stage["lowerCandidateCount"] = lowerCount;
    stage["supportContainsAnchorProjection"] = supportContainsAnchor;
    stage["lowerContainsAnchorProjection"] = lowerContainsAnchor;
    stage["supportComponentCount"] = static_cast<int>(supportComponentIds.size());
    stage["lowerComponentCount"] = static_cast<int>(lowerComponentIds.size());
    stage["routeTargetCount"] = static_cast<int>(resolvedTargets.size());
    stage["resolvedRouteTargetCount"] = resolvedRouteTargetCount;
    stage["routeableCandidateCount"] = routeableCandidateCount;
    stage["routeableSupportCandidateCount"] = routeableSupportCandidateCount;
    stage["routeableLowerCandidateCount"] = routeableLowerCandidateCount;
    stage["routeableSupportComponentCount"] = static_cast<int>(routeableSupportComponentIds.size());

    const float nearestExtents[3] = { xyExtent, zExtent, xyExtent };
    dtQueryFilter filter;
    dtPolyRef nearestRef = 0;
    float nearestPoint[3] = { 0.0f, 0.0f, 0.0f };
    if (dtStatusSucceed(query->findNearestPoly(detourPos, nearestExtents, &filter, &nearestRef, nearestPoint)) && nearestRef != 0)
    {
        const AnchorPolyStackProbeResult winnerProbe = ProbeAnchorPolyAtCoord(*query, nearestRef, detourPos);
        float winnerSurfaceZ = 0.0f;
        bool winnerFallback = false;
        const bool winnerHasSurface = TryGetAnchorProbeSupportSurfaceZ(
            winnerProbe, winnerSurfaceZ, winnerFallback, true);
        const int winnerPolyIndex = nearestRef >= tileRefBase
            ? static_cast<int>(nearestRef - tileRefBase)
            : -1;
        const int winnerComponentId =
            winnerPolyIndex >= 0 && winnerPolyIndex < static_cast<int>(componentIds.size())
                ? componentIds[winnerPolyIndex]
                : -1;
        const FinalDetourGroundComponentInfo* winnerComponent =
            winnerComponentId >= 0 && winnerComponentId < static_cast<int>(components.size())
                ? &components[winnerComponentId]
                : nullptr;
        const auto winnerRouteabilityIt = routeabilityByComponent.find(winnerComponentId);
        const FinalDetourComponentRouteability* winnerRouteability =
            winnerRouteabilityIt != routeabilityByComponent.end() ? &winnerRouteabilityIt->second : nullptr;
        const bool winnerSupportBand =
            winnerPolyIndex >= 0 && winnerPolyIndex < tile.header->polyCount &&
            IntersectsAnchorSupportBand(diagnostics[winnerPolyIndex], supportFloorMinY, supportFloorMaxY);
        const bool winnerSupport = (winnerHasSurface && winnerSurfaceZ >= supportFloorMinY && winnerSurfaceZ <= supportFloorMaxY) ||
            (winnerProbe.posOverPoly && winnerSupportBand);
        const bool winnerLower = winnerHasSurface && IsAnchorCompetingLowerFloor(winnerSurfaceZ, support, supportBandTuning);
        stage["finalWinner"] =
        {
            { "polyRef", FormatPolyRefHex(nearestRef) },
            { "posOverPoly", winnerProbe.posOverPoly },
            { "closestDistance2D", winnerProbe.hasClosestPoint ? winnerProbe.closestDistance2D : -1.0f },
            { "surfaceZ", winnerHasSurface ? winnerSurfaceZ : 0.0f },
            { "surfaceFromClosestFallback", winnerHasSurface && winnerFallback },
            { "supportBandCandidate", winnerSupportBand },
            { "supportCandidate", winnerSupport },
            { "competingLower", winnerLower },
            { "componentId", winnerComponentId },
            { "componentPolyCount", winnerComponent ? winnerComponent->polyCount : 0 },
            { "componentArea2D", winnerComponent ? winnerComponent->totalHorizontalArea2D : 0.0f },
            { "routeableToAnyTarget", winnerRouteability ? winnerRouteability->routeableToAnyTarget : false },
            { "routeableTargetCount", winnerRouteability ? winnerRouteability->routeableTargetCount : 0 },
            { "routeTargets", winnerRouteability ? winnerRouteability->routeTargets : json::array() },
        };
        stage["finalWinnerComponentId"] = winnerComponentId;
        stage["finalWinnerRouteableToAnyTarget"] = winnerRouteability ? winnerRouteability->routeableToAnyTarget : false;
        stage["dominantLowerCandidate"] = winnerLower && supportBandCount > 0;
    }

    return stage;
}

static void MergeAnchorStageSummary(json& existing, const json& incoming)
{
    const auto mergeBool = [&](const char* name)
    {
        const bool current = existing.value(name, false);
        const bool next = incoming.value(name, false);
        existing[name] = current || next;
    };

    const auto mergeCount = [&](const char* name)
    {
        const int current = existing.value(name, 0);
        const int next = incoming.value(name, 0);
        existing[name] = current + next;
    };

    const auto mergeFloatMin = [&](const char* name)
    {
        const float current = existing.value(name, -1.0f);
        const float next = incoming.value(name, -1.0f);
        if (current < 0.0f)
            existing[name] = next;
        else if (next < 0.0f)
            existing[name] = current;
        else
            existing[name] = std::min(current, next);
    };

    mergeBool("upperSupportExists");
    mergeBool("lowerCompetitorExists");
    mergeBool("dominantLowerCandidate");
    mergeBool("supportContainsAnchorCell");
    mergeBool("lowerContainsAnchorCell");
    mergeBool("supportContainsAnchorProjection");
    mergeBool("lowerContainsAnchorProjection");
    mergeCount("supportCandidateCount");
    mergeCount("lowerCandidateCount");
    mergeCount("supportCells");
    mergeCount("supportSpans");
    mergeCount("lowerCells");
    mergeCount("lowerSpans");
    mergeFloatMin("bestSupportDelta");

    for (const char* arrayName : { "components", "contours", "polys", "candidates" })
    {
        if (!incoming.contains(arrayName) || !incoming[arrayName].is_array())
            continue;

        if (!existing.contains(arrayName) || !existing[arrayName].is_array())
            existing[arrayName] = json::array();

        for (const json& entry : incoming[arrayName])
            existing[arrayName].push_back(entry);
    }

    if (incoming.contains("finalWinner") && !existing.contains("finalWinner"))
        existing["finalWinner"] = incoming["finalWinner"];

    if (incoming.contains("unprovenReason") && !existing.contains("unprovenReason"))
        existing["unprovenReason"] = incoming["unprovenReason"];
}

static void MergeAnchorStageIntoManifest(json& anchorStages, const json& incomingStage)
{
    const std::string stageName = incomingStage.value("name", "");
    if (stageName.empty())
        return;

    for (json& existingStage : anchorStages)
    {
        if (existingStage.value("name", "") == stageName)
        {
            MergeAnchorStageSummary(existingStage, incomingStage);
            return;
        }
    }

    anchorStages.push_back(incomingStage);
}

// [WWoW-DIVERGENCE] 2026-05-22: targeted final-tile cleanup for stacked dead-end
// slabs proven by probe coordinates. This is bake-time only and intentionally
// tile-local: when an anchor coordinate has at least one real support polygon at
// the expected surface height, disable competing tiny overlapping ground slabs in
// the same local stack that cannot actually support that anchor.
static int CullAnchorPolyStacks(dtNavMesh& navMesh, const dtMeshTile& tile,
    const std::vector<DetourPolyDiagnostics>& diagnostics, std::vector<unsigned char>& liveGroundMask,
    const float xyExtent, const float zExtent, const float supportZTolerance,
    const float supportGap2D,
    const std::vector<AnchorPolyStackCoord>& anchorCoords,
    const std::vector<AnchorSourceSupportProbe>& sourceSupports,
    const bool trimAnchorTrappedComponents,
    const std::vector<AnchorRouteTarget>& routeTargets)
{
    if (!tile.header || anchorCoords.empty())
        return 0;

    std::unique_ptr<dtNavMeshQuery, decltype(&dtFreeNavMeshQuery)> query(dtAllocNavMeshQuery(), &dtFreeNavMeshQuery);
    if (!query)
    {
        printf("[DT-ANCHOR-CULL] tile=%d,%d query allocation failed\n", tile.header->x, tile.header->y);
        return 0;
    }

    const int maxNodes = 4096;
    if (dtStatusFailed(query->init(&navMesh, maxNodes)))
    {
        printf("[DT-ANCHOR-CULL] tile=%d,%d query init failed maxNodes=%d\n", tile.header->x, tile.header->y, maxNodes);
        return 0;
    }

    const dtPolyRef tileRefBase = navMesh.getPolyRefBase(&tile);
    int culled = 0;

    for (const AnchorPolyStackCoord& anchor : anchorCoords)
    {
        const float detourPos[3] = { anchor.wowY, anchor.wowZ, anchor.wowX };
        const AnchorSourceSupportProbe* sourceSupport = FindAnchorSourceSupportProbe(sourceSupports, anchor);
        const bool hasSourceSupport = sourceSupport && sourceSupport->found;
        const float supportReferenceY = hasSourceSupport ? sourceSupport->supportY : anchor.wowZ;
        const float supportBandMinY = supportReferenceY + ANCHOR_POLY_STACK_MIN_SUPPORT_DELTA - 0.35f;
        const float supportBandMaxY = supportReferenceY + supportZTolerance + 0.75f;
        const float lowerCompetitorMaxTopY = supportReferenceY - 0.25f;

        std::vector<int> windowPolyIndices;
        std::vector<AnchorPolyStackProbeResult> probeResults;
        std::vector<unsigned char> supportMask(tile.header->polyCount, 0);
        std::vector<unsigned char> supportBandMask(tile.header->polyCount, 0);
        int exactSupportCount = 0;
        int closestFallbackSupportCount = 0;

        for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
        {
            if (!liveGroundMask[polyIndex])
                continue;

            const dtPoly& poly = tile.polys[polyIndex];
            if (!IsWalkableLandPoly(poly))
                continue;

            if (!IntersectsAnchorWindow(diagnostics[polyIndex], anchor.wowY, anchor.wowZ, anchor.wowX, xyExtent, zExtent))
                continue;

            windowPolyIndices.push_back(polyIndex);
            probeResults.push_back(ProbeAnchorPolyAtCoord(*query, tileRefBase | static_cast<dtPolyRef>(polyIndex), detourPos));

            const AnchorPolyStackProbeResult& probe = probeResults.back();
            const bool supportBandCandidate = IntersectsAnchorSupportBand(diagnostics[polyIndex], supportBandMinY, supportBandMaxY);
            if (supportBandCandidate)
                supportBandMask[polyIndex] = 1;

            float supportSurfaceZ = 0.0f;
            bool usedClosestFallback = false;
            if (!TryGetAnchorProbeSupportSurfaceZ(probe, supportSurfaceZ, usedClosestFallback))
            {
                if (probe.posOverPoly && supportBandCandidate)
                    supportMask[polyIndex] = 1;
                continue;
            }

            const bool supportCandidate =
                (supportSurfaceZ >= supportBandMinY && supportSurfaceZ <= supportBandMaxY) ||
                (probe.posOverPoly && supportBandCandidate);
            if (supportCandidate)
            {
                supportMask[polyIndex] = 1;
                if (usedClosestFallback)
                    ++closestFallbackSupportCount;
                else
                    ++exactSupportCount;
            }
        }

        int supportCount = 0;
        for (const int polyIndex : windowPolyIndices)
            supportCount += supportMask[polyIndex] ? 1 : 0;

        if (supportCount == 0)
        {
            std::vector<int> upperFringeCandidates;
            for (const int polyIndex : windowPolyIndices)
            {
                if (!liveGroundMask[polyIndex] || !IsWalkableLandPoly(tile.polys[polyIndex]))
                    continue;

                const DetourPolyDiagnostics& polyDiagnostics = diagnostics[polyIndex];
                if (polyDiagnostics.maxY < anchor.wowZ + ANCHOR_UPPER_FRINGE_MIN_TOP_DELTA)
                    continue;
                if (polyDiagnostics.minY > anchor.wowZ + supportZTolerance + 0.75f)
                    continue;

                upperFringeCandidates.push_back(polyIndex);
            }

            int lowerFringeCulled = 0;
            std::vector<int> supportBandCandidates;
            if (hasSourceSupport)
            {
                for (const int polyIndex : windowPolyIndices)
                {
                    if (supportBandMask[polyIndex] && liveGroundMask[polyIndex] && IsWalkableLandPoly(tile.polys[polyIndex]))
                        supportBandCandidates.push_back(polyIndex);
                }
            }

            const std::vector<int>& overlapSupportCandidates =
                !supportBandCandidates.empty() ? supportBandCandidates : upperFringeCandidates;
            float bestSupportGap2D = std::numeric_limits<float>::max();
            if (!overlapSupportCandidates.empty())
            {
                for (const int candidateIndex : windowPolyIndices)
                {
                    if (!liveGroundMask[candidateIndex])
                        continue;

                    dtPoly& candidatePoly = tile.polys[candidateIndex];
                    if (!IsWalkableLandPoly(candidatePoly))
                        continue;

                    const DetourPolyDiagnostics& candidateDiagnostics = diagnostics[candidateIndex];
                    if (candidateDiagnostics.maxY > lowerCompetitorMaxTopY)
                        continue;

                    const float minOverlapArea = rcMax(
                        STACKED_SLIVER_MIN_OVERLAP_AREA_2D,
                        rcMin(candidateDiagnostics.horizontalArea2D * STACKED_SLIVER_MIN_OVERLAP_RATIO, 4.0f));

                    bool overlapsUpperFringe = false;
                    for (const int upperIndex : overlapSupportCandidates)
                    {
                        if (upperIndex == candidateIndex || !liveGroundMask[upperIndex])
                            continue;

                        const float overlapArea = GetDetourBoundsOverlapArea2D(candidateDiagnostics, diagnostics[upperIndex]);
                        if (overlapArea >= minOverlapArea)
                        {
                            overlapsUpperFringe = true;
                            bestSupportGap2D = 0.0f;
                            break;
                        }

                        if (supportGap2D > 0.0f)
                        {
                            const float supportGap = GetDetourBoundsGap2D(candidateDiagnostics, diagnostics[upperIndex]);
                            bestSupportGap2D = rcMin(bestSupportGap2D, supportGap);
                            if (supportGap <= supportGap2D)
                            {
                                overlapsUpperFringe = true;
                                break;
                            }
                        }

                    }

                    if (!overlapsUpperFringe)
                        continue;

                    candidatePoly.flags = 0;
                    candidatePoly.setArea(AREA_NONE);
                    liveGroundMask[candidateIndex] = 0;
                    ++culled;
                    ++lowerFringeCulled;
                }
            }

            int posOverCount = 0;
            int surfacedCount = 0;
            int nearClosestCount = 0;
            int supportBandCandidateCount = 0;
            float closestDistance2DMin = std::numeric_limits<float>::max();
            int bestSurfacePolyIndex = -1;
            float bestSurfaceZ = 0.0f;
            float bestSurfaceDelta = std::numeric_limits<float>::max();
            int bestSurfacePosOver = 0;
            for (size_t probeIndex = 0; probeIndex < probeResults.size(); ++probeIndex)
            {
                const AnchorPolyStackProbeResult& probe = probeResults[probeIndex];
                if (supportBandMask[windowPolyIndices[probeIndex]])
                    ++supportBandCandidateCount;
                if (probe.posOverPoly)
                    ++posOverCount;
                if (probe.hasSurfaceZ)
                    ++surfacedCount;
                if (probe.hasClosestPoint)
                {
                    closestDistance2DMin = rcMin(closestDistance2DMin, probe.closestDistance2D);
                    if (probe.closestDistance2D <= STACKED_SLIVER_MAX_CLOSEST_SUPPORT_DISTANCE_2D)
                        ++nearClosestCount;
                }

                float candidateSurfaceZ = 0.0f;
                bool usedClosestFallback = false;
                if (!TryGetAnchorProbeSupportSurfaceZ(probe, candidateSurfaceZ, usedClosestFallback))
                    continue;

                const float surfaceDelta = candidateSurfaceZ - supportReferenceY;
                if (fabsf(surfaceDelta) < fabsf(bestSurfaceDelta))
                {
                    bestSurfaceDelta = surfaceDelta;
                    bestSurfaceZ = candidateSurfaceZ;
                    bestSurfacePolyIndex = windowPolyIndices[probeIndex];
                    bestSurfacePosOver = probe.posOverPoly ? 1 : 0;
                }
            }

            if (closestDistance2DMin == std::numeric_limits<float>::max())
                closestDistance2DMin = -1.0f;
            if (bestSupportGap2D == std::numeric_limits<float>::max())
                bestSupportGap2D = -1.0f;
            if (bestSurfaceDelta == std::numeric_limits<float>::max())
                bestSurfaceDelta = 0.0f;

            printf("[DT-ANCHOR-CULL-SKIP] tile=%d,%d anchor=(%.3f,%.3f,%.3f) window=%zu supports=0 upperFringe=%zu lowerFringeCulled=%d posOver=%d surfaced=%d nearClosest=%d supportBandCandidates=%d closest2DMin=%.3f bestSupportGap2D=%.3f bestSurfacePoly=%d bestSurfaceZ=%.3f bestSurfaceDelta=%.3f bestSurfacePosOver=%d\n",
                tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ,
                windowPolyIndices.size(), upperFringeCandidates.size(), lowerFringeCulled,
                posOverCount, surfacedCount, nearClosestCount,
                supportBandCandidateCount, closestDistance2DMin, bestSupportGap2D,
                bestSurfacePolyIndex, bestSurfaceZ, bestSurfaceDelta, bestSurfacePosOver);
            continue;
        }

        int anchorCulled = 0;
        for (size_t candidateListIndex = 0; candidateListIndex < windowPolyIndices.size(); ++candidateListIndex)
        {
            const int candidateIndex = windowPolyIndices[candidateListIndex];
            if (!liveGroundMask[candidateIndex] || supportMask[candidateIndex])
                continue;

            const dtPoly& candidatePoly = tile.polys[candidateIndex];
            if (!IsWalkableLandPoly(candidatePoly))
                continue;

            if (!IntersectsAnchorSupportBand(diagnostics[candidateIndex], supportBandMinY, supportBandMaxY))
                continue;

            const AnchorPolyStackProbeResult& probe = probeResults[candidateListIndex];
            float candidateSurfaceZ = 0.0f;
            bool usedClosestFallback = false;
            const bool hasCandidateSurface = TryGetAnchorProbeSupportSurfaceZ(probe, candidateSurfaceZ, usedClosestFallback);
            const bool mismatchedSupport =
                !hasCandidateSurface ||
                candidateSurfaceZ < supportBandMinY ||
                candidateSurfaceZ > supportBandMaxY;
            if (!mismatchedSupport)
                continue;

            bool overlapsSupport = false;
            const float minOverlapArea = rcMax(
                STACKED_SLIVER_MIN_OVERLAP_AREA_2D,
                rcMin(diagnostics[candidateIndex].horizontalArea2D * STACKED_SLIVER_MIN_OVERLAP_RATIO, 4.0f));

            for (const int supportIndex : windowPolyIndices)
            {
                if (!supportMask[supportIndex])
                    continue;

                if (GetDetourBoundsOverlapArea2D(diagnostics[candidateIndex], diagnostics[supportIndex]) < minOverlapArea)
                    continue;

                overlapsSupport = true;
                break;
            }

            if (!overlapsSupport)
                continue;

            dtPoly& candidatePolyMutable = const_cast<dtPoly&>(candidatePoly);
            candidatePolyMutable.flags = 0;
            candidatePolyMutable.setArea(AREA_NONE);
            liveGroundMask[candidateIndex] = 0;
            ++culled;
            ++anchorCulled;
        }

        int routeCulled = 0;
        if (trimAnchorTrappedComponents && !routeTargets.empty())
        {
            std::vector<int> componentIds;
            std::vector<FinalDetourGroundComponentInfo> components;
            BuildFinalDetourGroundComponents(navMesh, tile, diagnostics, liveGroundMask, componentIds, components);

            std::unique_ptr<dtNavMeshQuery, decltype(&dtFreeNavMeshQuery)> routeQuery(dtAllocNavMeshQuery(), &dtFreeNavMeshQuery);
            if (!routeQuery)
            {
                printf("[DT-ANCHOR-ROUTE] tile=%d,%d anchor=(%.3f,%.3f,%.3f) query allocation failed\n",
                    tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ);
                printf("[DT-ANCHOR-CULL] tile=%d,%d anchor=(%.3f,%.3f,%.3f) window=%zu supports=%d exact=%d closest=%d culled=%d routeCulled=%d\n",
                    tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ,
                    windowPolyIndices.size(), supportCount, exactSupportCount, closestFallbackSupportCount, anchorCulled, routeCulled);
                continue;
            }

            if (dtStatusFailed(routeQuery->init(&navMesh, ANCHOR_ROUTE_QUERY_MAX_NODES)))
            {
                printf("[DT-ANCHOR-ROUTE] tile=%d,%d anchor=(%.3f,%.3f,%.3f) query init failed maxNodes=%d\n",
                    tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ, ANCHOR_ROUTE_QUERY_MAX_NODES);
                printf("[DT-ANCHOR-CULL] tile=%d,%d anchor=(%.3f,%.3f,%.3f) window=%zu supports=%d exact=%d closest=%d culled=%d routeCulled=%d\n",
                    tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ,
                    windowPolyIndices.size(), supportCount, exactSupportCount, closestFallbackSupportCount, anchorCulled, routeCulled);
                continue;
            }

            std::unordered_map<int, FinalDetourComponentRouteability> routeabilityByComponent;
            std::vector<AnchorRouteTargetResolution> resolvedTargets;
            EvaluateFinalDetourAnchorComponentRouteability(
                *routeQuery, anchor, routeTargets, windowPolyIndices, probeResults, componentIds, tileRefBase,
                routeabilityByComponent, resolvedTargets);

            int resolvedRouteTargetCount = 0;
            std::unordered_set<int> routeableSupportComponentIds;
            std::vector<int> routeableSupportPolyIndices;
            for (const AnchorRouteTargetResolution& target : resolvedTargets)
            {
                if (target.resolved)
                    ++resolvedRouteTargetCount;
            }

            for (const int polyIndex : windowPolyIndices)
            {
                if (!liveGroundMask[polyIndex] || !supportMask[polyIndex])
                    continue;

                if (polyIndex < 0 || polyIndex >= static_cast<int>(componentIds.size()))
                    continue;

                const int componentId = componentIds[polyIndex];
                if (componentId < 0)
                    continue;

                const auto routeabilityIt = routeabilityByComponent.find(componentId);
                if (routeabilityIt == routeabilityByComponent.end() || !routeabilityIt->second.routeableToAnyTarget)
                    continue;

                routeableSupportComponentIds.insert(componentId);
                routeableSupportPolyIndices.push_back(polyIndex);
            }

            if (!routeableSupportComponentIds.empty())
            {
                for (size_t candidateListIndex = 0; candidateListIndex < windowPolyIndices.size(); ++candidateListIndex)
                {
                    const int candidateIndex = windowPolyIndices[candidateListIndex];
                    if (!liveGroundMask[candidateIndex])
                        continue;

                    dtPoly& candidatePoly = tile.polys[candidateIndex];
                    if (!IsWalkableLandPoly(candidatePoly))
                        continue;

                    if (candidateIndex < 0 || candidateIndex >= static_cast<int>(componentIds.size()))
                        continue;

                    const int componentId = componentIds[candidateIndex];
                    if (componentId < 0)
                        continue;

                    const auto routeabilityIt = routeabilityByComponent.find(componentId);
                    if (routeabilityIt == routeabilityByComponent.end() || routeabilityIt->second.routeableToAnyTarget)
                        continue;

                    const bool candidateIsSupport = supportMask[candidateIndex] != 0;
                    bool overlapsRouteableSupport = candidateIsSupport;
                    if (!overlapsRouteableSupport)
                    {
                        const float minOverlapArea = rcMax(
                            STACKED_SLIVER_MIN_OVERLAP_AREA_2D,
                            rcMin(diagnostics[candidateIndex].horizontalArea2D * STACKED_SLIVER_MIN_OVERLAP_RATIO, 4.0f));
                        for (const int supportIndex : routeableSupportPolyIndices)
                        {
                            if (supportIndex == candidateIndex || !liveGroundMask[supportIndex])
                                continue;

                            if (GetDetourBoundsOverlapArea2D(diagnostics[candidateIndex], diagnostics[supportIndex]) < minOverlapArea)
                                continue;

                            overlapsRouteableSupport = true;
                            break;
                        }
                    }

                    if (!overlapsRouteableSupport)
                        continue;

                    candidatePoly.flags = 0;
                    candidatePoly.setArea(AREA_NONE);
                    liveGroundMask[candidateIndex] = 0;
                    ++culled;
                    ++anchorCulled;
                    ++routeCulled;
                }
            }

            if (!resolvedTargets.empty())
            {
                printf("[DT-ANCHOR-ROUTE] tile=%d,%d anchor=(%.3f,%.3f,%.3f) targets=%zu resolved=%d routeableSupportComponents=%zu routeCulled=%d\n",
                    tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ,
                    resolvedTargets.size(), resolvedRouteTargetCount, routeableSupportComponentIds.size(), routeCulled);
            }
        }

        printf("[DT-ANCHOR-CULL] tile=%d,%d anchor=(%.3f,%.3f,%.3f) window=%zu supports=%d exact=%d closest=%d culled=%d routeCulled=%d\n",
            tile.header->x, tile.header->y, anchor.wowX, anchor.wowY, anchor.wowZ,
            windowPolyIndices.size(), supportCount, exactSupportCount, closestFallbackSupportCount, anchorCulled, routeCulled);
    }

    return culled;
}

static bool HasShadowingUpperGroundPoly(const dtMeshTile& tile, const int polyIndex,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask)
{
    const DetourPolyDiagnostics& candidate = diagnostics[polyIndex];
    const float minOverlapArea = rcMax(
        SHADOWED_LEDGE_MIN_OVERLAP_AREA_2D,
        rcMin(candidate.horizontalArea2D * SHADOWED_LEDGE_MIN_OVERLAP_RATIO, 3.0f));

    for (int otherIndex = 0; otherIndex < tile.header->polyCount; ++otherIndex)
    {
        if (otherIndex == polyIndex || !liveGroundMask[otherIndex])
            continue;

        const DetourPolyDiagnostics& other = diagnostics[otherIndex];
        const float verticalGap = other.minY - candidate.maxY;
        if (verticalGap < SHADOWED_LEDGE_MIN_VERTICAL_GAP || verticalGap > SHADOWED_LEDGE_MAX_VERTICAL_GAP)
            continue;

        if (GetDetourBoundsOverlapArea2D(candidate, other) < minOverlapArea)
            continue;

        return true;
    }

    return false;
}

static bool HasShadowingUpperGroundPocketPoly(const dtMeshTile& tile, const int polyIndex,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask)
{
    const DetourPolyDiagnostics& candidate = diagnostics[polyIndex];
    const float minOverlapArea = rcMax(
        SHADOWED_POCKET_MIN_OVERLAP_AREA_2D,
        rcMin(candidate.horizontalArea2D * SHADOWED_POCKET_MIN_OVERLAP_RATIO, 6.0f));

    for (int otherIndex = 0; otherIndex < tile.header->polyCount; ++otherIndex)
    {
        if (otherIndex == polyIndex || !liveGroundMask[otherIndex])
            continue;

        const DetourPolyDiagnostics& other = diagnostics[otherIndex];
        const float verticalGap = other.minY - candidate.maxY;
        if (verticalGap < SHADOWED_POCKET_MIN_VERTICAL_GAP || verticalGap > SHADOWED_POCKET_MAX_VERTICAL_GAP)
            continue;

        if (GetDetourBoundsOverlapArea2D(candidate, other) < minOverlapArea)
            continue;

        return true;
    }

    return false;
}

struct ShadowedLedgeComponent
{
    std::vector<int> members;
    float totalHorizontalArea2D = 0.0f;
    float minHeight = std::numeric_limits<float>::max();
    float maxHeight = -std::numeric_limits<float>::max();
    bool hasShadowingUpperGround = false;
};

static ShadowedLedgeComponent CollectShadowedLedgeComponent(const dtMeshTile& tile, const int startPolyIndex,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask,
    std::vector<unsigned char>& visitedMask)
{
    ShadowedLedgeComponent component;
    std::vector<int> pending;
    pending.push_back(startPolyIndex);
    visitedMask[startPolyIndex] = 1;

    while (!pending.empty())
    {
        const int polyIndex = pending.back();
        pending.pop_back();
        const dtPoly& poly = tile.polys[polyIndex];
        const DetourPolyDiagnostics& polyDiagnostics = diagnostics[polyIndex];

        component.members.push_back(polyIndex);
        component.totalHorizontalArea2D += polyDiagnostics.horizontalArea2D;
        component.minHeight = rcMin(component.minHeight, polyDiagnostics.minY);
        component.maxHeight = rcMax(component.maxHeight, polyDiagnostics.maxY);
        component.hasShadowingUpperGround = component.hasShadowingUpperGround ||
            HasShadowingUpperGroundPoly(tile, polyIndex, diagnostics, liveGroundMask);

        for (int edgeIndex = 0; edgeIndex < poly.vertCount; ++edgeIndex)
        {
            const unsigned short neighbor = poly.neis[edgeIndex];
            if (neighbor == 0 || (neighbor & DT_EXT_LINK) != 0)
                continue;

            const int neighborIndex = static_cast<int>(neighbor) - 1;
            if (neighborIndex < 0 || neighborIndex >= tile.header->polyCount)
                continue;

            if (visitedMask[neighborIndex] || !liveGroundMask[neighborIndex])
                continue;

            if (!IsShadowedLedgeCandidate(diagnostics[neighborIndex]))
                continue;

            visitedMask[neighborIndex] = 1;
            pending.push_back(neighborIndex);
        }
    }

    if (component.minHeight == std::numeric_limits<float>::max())
        component.minHeight = 0.0f;
    if (component.maxHeight == -std::numeric_limits<float>::max())
        component.maxHeight = 0.0f;

    return component;
}

static bool IsTinyShadowedLedgeFallbackCandidate(const DetourPolyDiagnostics& memberDiagnostics)
{
    const float memberHeightRange = memberDiagnostics.maxY - memberDiagnostics.minY;
    if (memberDiagnostics.horizontalArea2D > SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_AREA_2D)
        return false;

    if (memberDiagnostics.maxEdge2D > SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_EDGE_2D)
        return false;

    if (memberHeightRange > SHADOWED_LEDGE_FALLBACK_MEMBER_MAX_Z_RANGE)
        return false;

    return true;
}

static bool OverlapsShadowedLedgeSeedCandidate(const DetourPolyDiagnostics& seedDiagnostics,
    const DetourPolyDiagnostics& memberDiagnostics)
{
    const float minOverlapArea = rcMax(
        SHADOWED_LEDGE_MIN_OVERLAP_AREA_2D,
        rcMin(seedDiagnostics.horizontalArea2D * SHADOWED_LEDGE_MIN_OVERLAP_RATIO, 3.0f));
    return GetDetourBoundsOverlapArea2D(seedDiagnostics, memberDiagnostics) >= minOverlapArea;
}

struct ShadowedPocketComponent
{
    std::vector<int> members;
    float totalHorizontalArea2D = 0.0f;
    float minHeight = std::numeric_limits<float>::max();
    float maxHeight = -std::numeric_limits<float>::max();
    bool hasShadowingUpperGround = false;
};

static ShadowedPocketComponent CollectShadowedPocketComponent(const dtMeshTile& tile, const int startPolyIndex,
    const std::vector<DetourPolyDiagnostics>& diagnostics, const std::vector<unsigned char>& liveGroundMask,
    std::vector<unsigned char>& visitedMask)
{
    ShadowedPocketComponent component;
    std::vector<int> pending;
    pending.push_back(startPolyIndex);
    visitedMask[startPolyIndex] = 1;

    while (!pending.empty())
    {
        const int polyIndex = pending.back();
        pending.pop_back();
        const dtPoly& poly = tile.polys[polyIndex];
        const DetourPolyDiagnostics& polyDiagnostics = diagnostics[polyIndex];

        component.members.push_back(polyIndex);
        component.totalHorizontalArea2D += polyDiagnostics.horizontalArea2D;
        component.minHeight = rcMin(component.minHeight, polyDiagnostics.minY);
        component.maxHeight = rcMax(component.maxHeight, polyDiagnostics.maxY);
        component.hasShadowingUpperGround = component.hasShadowingUpperGround ||
            HasShadowingUpperGroundPocketPoly(tile, polyIndex, diagnostics, liveGroundMask);

        for (int edgeIndex = 0; edgeIndex < poly.vertCount; ++edgeIndex)
        {
            const unsigned short neighbor = poly.neis[edgeIndex];
            if (neighbor == 0 || (neighbor & DT_EXT_LINK) != 0)
                continue;

            const int neighborIndex = static_cast<int>(neighbor) - 1;
            if (neighborIndex < 0 || neighborIndex >= tile.header->polyCount)
                continue;

            if (visitedMask[neighborIndex] || !liveGroundMask[neighborIndex])
                continue;

            if (!IsShadowedPocketCandidate(diagnostics[neighborIndex]))
                continue;

            visitedMask[neighborIndex] = 1;
            pending.push_back(neighborIndex);
        }
    }

    if (component.minHeight == std::numeric_limits<float>::max())
        component.minHeight = 0.0f;
    if (component.maxHeight == -std::numeric_limits<float>::max())
        component.maxHeight = 0.0f;

    return component;
}

struct OffMeshAnchorSteepTrimComponent
{
    std::vector<int> members;
    float totalHorizontalArea2D = 0.0f;
    float minHeight = std::numeric_limits<float>::max();
    float maxHeight = -std::numeric_limits<float>::max();
};

static OffMeshAnchorSteepTrimComponent CollectOffMeshAnchorSteepTrimComponent(const dtMeshTile& tile,
    const int startPolyIndex, const std::vector<DetourPolyDiagnostics>& diagnostics,
    const std::vector<unsigned char>& liveGroundMask, std::vector<unsigned char>& visitedMask,
    const OffMeshAnchorSteepTrimSettings& settings)
{
    OffMeshAnchorSteepTrimComponent component;
    std::vector<int> pending;
    pending.push_back(startPolyIndex);
    visitedMask[startPolyIndex] = 1;

    while (!pending.empty())
    {
        const int polyIndex = pending.back();
        pending.pop_back();
        const dtPoly& poly = tile.polys[polyIndex];
        const DetourPolyDiagnostics& polyDiagnostics = diagnostics[polyIndex];

        component.members.push_back(polyIndex);
        component.totalHorizontalArea2D += polyDiagnostics.horizontalArea2D;
        component.minHeight = rcMin(component.minHeight, polyDiagnostics.minY);
        component.maxHeight = rcMax(component.maxHeight, polyDiagnostics.maxY);

        for (int edgeIndex = 0; edgeIndex < poly.vertCount; ++edgeIndex)
        {
            const unsigned short neighbor = poly.neis[edgeIndex];
            if (neighbor == 0 || (neighbor & DT_EXT_LINK) != 0)
                continue;

            const int neighborIndex = static_cast<int>(neighbor) - 1;
            if (neighborIndex < 0 || neighborIndex >= tile.header->polyCount)
                continue;

            if (visitedMask[neighborIndex] || !liveGroundMask[neighborIndex])
                continue;

            if (!IsOffMeshAnchorSteepTrimCandidate(diagnostics[neighborIndex], settings))
                continue;

            visitedMask[neighborIndex] = 1;
            pending.push_back(neighborIndex);
        }
    }

    if (component.minHeight == std::numeric_limits<float>::max())
        component.minHeight = 0.0f;
    if (component.maxHeight == -std::numeric_limits<float>::max())
        component.maxHeight = 0.0f;

    return component;
}

static int FindOffMeshStartLandingPolyIndex(const dtNavMesh& navMesh, const dtMeshTile& tile,
    const dtOffMeshConnection& connection)
{
    if (!tile.header || connection.poly >= tile.header->polyCount)
        return -1;

    const dtPoly& offMeshPoly = tile.polys[connection.poly];
    for (unsigned int linkIndex = offMeshPoly.firstLink; linkIndex != DT_NULL_LINK; linkIndex = tile.links[linkIndex].next)
    {
        const dtLink& link = tile.links[linkIndex];
        if (link.edge != 0)
            continue;

        const dtMeshTile* linkedTile = nullptr;
        const dtPoly* linkedPoly = nullptr;
        if (dtStatusFailed(navMesh.getTileAndPolyByRef(link.ref, &linkedTile, &linkedPoly)) || linkedTile != &tile || !linkedPoly)
            continue;

        return static_cast<int>(linkedPoly - tile.polys);
    }

    return -1;
}

static int CullSuspiciousDetourPolys(dtNavMesh& navMesh, dtMeshTile& tile, const float maxClimbWorld,
    const bool trimShadowedLedges, const bool trimOffMeshAnchorSteepTrim, const bool trimShadowedPockets,
    const OffMeshAnchorSteepTrimSettings& offMeshAnchorSteepTrimSettings,
    const bool trimAnchorPolyStacks, const float anchorPolyStackXyExtent, const float anchorPolyStackZExtent,
    const float anchorPolyStackSupportZTolerance, const float anchorPolyStackSupportGap2D,
    const std::vector<AnchorPolyStackCoord>& anchorPolyStackCoords,
    const std::vector<AnchorSourceSupportProbe>& anchorSourceSupports,
    const bool trimAnchorTrappedComponents,
    const std::vector<AnchorRouteTarget>& anchorRouteTargets)
{
    if (!tile.header)
        return 0;

    std::vector<DetourPolyDiagnostics> diagnostics(tile.header->polyCount);
    std::vector<unsigned char> liveGroundMask(tile.header->polyCount, 0);
    for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
    {
        const dtPoly& poly = tile.polys[polyIndex];
        if (poly.getType() != DT_POLYTYPE_GROUND || poly.vertCount < 3)
            continue;

        diagnostics[polyIndex] = AnalyzeDetourPoly(tile, polyIndex, DETOUR_FINAL_CULL_MAX_SLOPE_DEGREES);
        if (IsWalkableLandPoly(poly))
            liveGroundMask[polyIndex] = 1;
    }

    int culled = 0;
    for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
    {
        dtPoly& poly = tile.polys[polyIndex];
        if (!liveGroundMask[polyIndex] || !IsWalkableLandPoly(poly))
            continue;

        if (!ShouldCullDetourPoly(diagnostics[polyIndex], maxClimbWorld))
            continue;

        poly.flags = 0;
        poly.setArea(AREA_NONE);
        liveGroundMask[polyIndex] = 0;
        ++culled;
    }

    if (trimOffMeshAnchorSteepTrim)
    {
        std::vector<unsigned char> visitedMask(tile.header->polyCount, 0);
        for (int offMeshIndex = 0; offMeshIndex < tile.header->offMeshConCount; ++offMeshIndex)
        {
            const dtOffMeshConnection& connection = tile.offMeshCons[offMeshIndex];
            const int landingPolyIndex = FindOffMeshStartLandingPolyIndex(navMesh, tile, connection);
            if (landingPolyIndex < 0 || landingPolyIndex >= tile.header->polyCount)
                continue;

            if (visitedMask[landingPolyIndex] || !liveGroundMask[landingPolyIndex])
                continue;

            if (!IsOffMeshAnchorSteepTrimCandidate(diagnostics[landingPolyIndex], offMeshAnchorSteepTrimSettings))
                continue;

            const OffMeshAnchorSteepTrimComponent component = CollectOffMeshAnchorSteepTrimComponent(
                tile, landingPolyIndex, diagnostics, liveGroundMask, visitedMask, offMeshAnchorSteepTrimSettings);

            const float componentHeightRange = component.maxHeight - component.minHeight;
            const bool componentTooLarge =
                component.totalHorizontalArea2D > offMeshAnchorSteepTrimSettings.componentMaxArea2D ||
                componentHeightRange > offMeshAnchorSteepTrimSettings.componentMaxZRange;
            if (componentTooLarge)
            {
                printf("[DT-OFFMESH-TRIM] tile=%d,%d offMesh=%d landingPoly=%d members=%zu area=%.2f zRange=%.2f action=skip-too-large\n",
                    tile.header->x, tile.header->y, offMeshIndex, landingPolyIndex,
                    component.members.size(), component.totalHorizontalArea2D, componentHeightRange);
                continue;
            }

            int componentCulled = 0;
            for (const int memberIndex : component.members)
            {
                dtPoly& memberPoly = tile.polys[memberIndex];
                if (!liveGroundMask[memberIndex] || !IsWalkableLandPoly(memberPoly))
                    continue;

                memberPoly.flags = 0;
                memberPoly.setArea(AREA_NONE);
                liveGroundMask[memberIndex] = 0;
                ++culled;
                ++componentCulled;
            }

            if (componentCulled > 0)
            {
                printf("[DT-OFFMESH-TRIM] tile=%d,%d offMesh=%d landingPoly=%d members=%zu area=%.2f zRange=%.2f culled=%d\n",
                    tile.header->x, tile.header->y, offMeshIndex, landingPolyIndex,
                    component.members.size(), component.totalHorizontalArea2D, componentHeightRange, componentCulled);
            }
        }
    }

    if (trimShadowedLedges)
    {
        std::vector<unsigned char> visitedMask(tile.header->polyCount, 0);
        for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
        {
            dtPoly& poly = tile.polys[polyIndex];
            if (visitedMask[polyIndex] || !liveGroundMask[polyIndex] || !IsWalkableLandPoly(poly))
                continue;

            if (!IsShadowedLedgeCandidate(diagnostics[polyIndex]))
                continue;

            const ShadowedLedgeComponent component = CollectShadowedLedgeComponent(
                tile, polyIndex, diagnostics, liveGroundMask, visitedMask);

            const float componentHeightRange = component.maxHeight - component.minHeight;
            const bool componentTooWide = component.totalHorizontalArea2D > SHADOWED_LEDGE_COMPONENT_MAX_AREA_2D;
            const bool componentTooTall = componentHeightRange > SHADOWED_LEDGE_COMPONENT_MAX_Z_RANGE;
            if (!component.hasShadowingUpperGround || componentTooWide || componentTooTall)
            {
                if (component.hasShadowingUpperGround)
                {
                    const DetourPolyDiagnostics& seedDiagnostics = diagnostics[polyIndex];
                    printf("[DT-SHADOW-LEDGE-SKIP] tile=%d,%d seedPoly=%d members=%zu area=%.2f zRange=%.2f shadow=1 tooWide=%d tooTall=%d\n",
                        tile.header->x, tile.header->y, polyIndex, component.members.size(),
                        component.totalHorizontalArea2D, componentHeightRange,
                        componentTooWide ? 1 : 0, componentTooTall ? 1 : 0);

                    std::vector<int> fallbackCandidates;
                    fallbackCandidates.reserve(component.members.size());
                    for (const int memberIndex : component.members)
                    {
                        const dtPoly& memberPoly = tile.polys[memberIndex];
                        if (!liveGroundMask[memberIndex] || !IsWalkableLandPoly(memberPoly))
                            continue;

                        const DetourPolyDiagnostics& memberDiagnostics = diagnostics[memberIndex];
                        if (!IsTinyShadowedLedgeFallbackCandidate(memberDiagnostics))
                            continue;

                        if (!OverlapsShadowedLedgeSeedCandidate(seedDiagnostics, memberDiagnostics))
                            continue;

                        if (!HasShadowingUpperGroundPoly(tile, memberIndex, diagnostics, liveGroundMask))
                            continue;

                        fallbackCandidates.push_back(memberIndex);
                    }

                    std::sort(fallbackCandidates.begin(), fallbackCandidates.end(),
                        [&](const int lhs, const int rhs)
                        {
                            const DetourPolyDiagnostics& left = diagnostics[lhs];
                            const DetourPolyDiagnostics& right = diagnostics[rhs];
                            if (left.horizontalArea2D != right.horizontalArea2D)
                                return left.horizontalArea2D > right.horizontalArea2D;
                            if (left.maxEdge2D != right.maxEdge2D)
                                return left.maxEdge2D > right.maxEdge2D;
                            return lhs < rhs;
                        });

                    int fallbackCulled = 0;
                    for (const int memberIndex : fallbackCandidates)
                    {
                        if (fallbackCulled >= SHADOWED_LEDGE_FALLBACK_MAX_CULLS_PER_COMPONENT)
                            break;

                        dtPoly& memberPoly = tile.polys[memberIndex];
                        if (!liveGroundMask[memberIndex] || !IsWalkableLandPoly(memberPoly))
                            continue;

                        memberPoly.flags = 0;
                        memberPoly.setArea(AREA_NONE);
                        liveGroundMask[memberIndex] = 0;
                        ++culled;
                        ++fallbackCulled;
                    }

                    if (fallbackCulled > 0)
                    {
                        printf("[DT-SHADOW-LEDGE-FALLBACK] tile=%d,%d seedPoly=%d members=%zu area=%.2f zRange=%.2f culled=%d\n",
                            tile.header->x, tile.header->y, polyIndex, component.members.size(),
                            component.totalHorizontalArea2D, componentHeightRange, fallbackCulled);
                    }
                }
                continue;
            }

            int componentCulled = 0;
            for (const int memberIndex : component.members)
            {
                dtPoly& memberPoly = tile.polys[memberIndex];
                if (!liveGroundMask[memberIndex] || !IsWalkableLandPoly(memberPoly))
                    continue;

                memberPoly.flags = 0;
                memberPoly.setArea(AREA_NONE);
                liveGroundMask[memberIndex] = 0;
                ++culled;
                ++componentCulled;
            }

            if (componentCulled > 0)
            {
                printf("[DT-SHADOW-LEDGE-CULL] tile=%d,%d seedPoly=%d members=%zu area=%.2f zRange=%.2f culled=%d\n",
                    tile.header->x, tile.header->y, polyIndex, component.members.size(),
                    component.totalHorizontalArea2D, componentHeightRange, componentCulled);
            }
        }
    }

    if (trimShadowedPockets)
    {
        std::vector<unsigned char> visitedMask(tile.header->polyCount, 0);
        for (int polyIndex = 0; polyIndex < tile.header->polyCount; ++polyIndex)
        {
            dtPoly& poly = tile.polys[polyIndex];
            if (visitedMask[polyIndex] || !liveGroundMask[polyIndex] || !IsWalkableLandPoly(poly))
                continue;

            if (!IsShadowedPocketCandidate(diagnostics[polyIndex]))
                continue;

            const ShadowedPocketComponent component = CollectShadowedPocketComponent(
                tile, polyIndex, diagnostics, liveGroundMask, visitedMask);

            const float componentHeightRange = component.maxHeight - component.minHeight;
            if (!component.hasShadowingUpperGround ||
                component.totalHorizontalArea2D > SHADOWED_POCKET_COMPONENT_MAX_AREA_2D ||
                componentHeightRange > SHADOWED_POCKET_COMPONENT_MAX_Z_RANGE)
                continue;

            for (const int memberIndex : component.members)
            {
                dtPoly& memberPoly = tile.polys[memberIndex];
                if (!liveGroundMask[memberIndex] || !IsWalkableLandPoly(memberPoly))
                    continue;

                memberPoly.flags = 0;
                memberPoly.setArea(AREA_NONE);
                liveGroundMask[memberIndex] = 0;
                ++culled;
            }
        }
    }

    if (trimAnchorPolyStacks)
    {
        culled += CullAnchorPolyStacks(
            navMesh, tile, diagnostics, liveGroundMask,
            anchorPolyStackXyExtent, anchorPolyStackZExtent, anchorPolyStackSupportZTolerance,
            anchorPolyStackSupportGap2D,
            anchorPolyStackCoords, anchorSourceSupports,
            trimAnchorTrappedComponents, anchorRouteTargets);
    }

    return culled;
}
}

static void from_json(const json& j, rcConfig& config)
{
    config.tileSize               = MMAP::VERTEX_PER_TILE;
    config.borderSize             = j["borderSize"].get<int>();
    config.cs                     = MMAP::BASE_UNIT_DIM;
    config.ch                     = MMAP::BASE_UNIT_DIM;
    config.walkableSlopeAngle     = j["walkableSlopeAngle"].get<float>();
    config.walkableHeight         = j["walkableHeight"].get<int>();
    config.walkableClimb          = j["walkableClimb"].get<int>();
    config.walkableRadius         = j["walkableRadius"].get<int>();
    config.maxEdgeLen             = j["maxEdgeLen"].get<int>();
    config.maxSimplificationError = j["maxSimplificationError"].get<float>();
    config.minRegionArea          = rcSqr(j["minRegionArea"].get<int>());
    config.mergeRegionArea        = rcSqr(j["mergeRegionArea"].get<int>());
    config.maxVertsPerPoly        = j.value("maxVertsPerPoly", DT_VERTS_PER_POLYGON);
    if (config.maxVertsPerPoly < 3)
        config.maxVertsPerPoly = 3;
    if (config.maxVertsPerPoly > DT_VERTS_PER_POLYGON)
        config.maxVertsPerPoly = DT_VERTS_PER_POLYGON;
    config.detailSampleDist       = j["detailSampleDist"].get<float>();
    config.detailSampleMaxError   = j["detailSampleMaxError"].get<float>();
}

enum class RegionPartitionType
{
    Watershed,
    Monotone,
    Layers,
};

static RegionPartitionType ParseRegionPartitionType(const json& j)
{
    std::string partitionType = "watershed";
    auto partitionTypeIt = j.find("partitionType");
    if (partitionTypeIt != j.end())
    {
        if (partitionTypeIt->is_string())
            partitionType = partitionTypeIt->get<std::string>();
        else
            printf("[CONFIG] partitionType present but not a string: %s\n", partitionTypeIt->dump().c_str());
    }

    if (partitionType == "watershed")
        return RegionPartitionType::Watershed;
    if (partitionType == "monotone")
        return RegionPartitionType::Monotone;
    if (partitionType == "layers")
        return RegionPartitionType::Layers;

    printf("[CONFIG] Unknown partitionType='%s'; falling back to watershed\n", partitionType.c_str());
    return RegionPartitionType::Watershed;
}

static const char* ToString(RegionPartitionType partitionType)
{
    switch (partitionType)
    {
        case RegionPartitionType::Watershed:
            return "watershed";
        case RegionPartitionType::Monotone:
            return "monotone";
        case RegionPartitionType::Layers:
            return "layers";
    }

    return "watershed";
}

namespace MMAP
{
    void TileWorker::WorkerThread()
    {
        while (true)
        {
            if (m_mapBuilder->m_cancel.load())
            {
                return;
            }

            TileInfo tileInfo;
            if (m_mapBuilder->m_tileQueue.WaitAndPop(tileInfo))
            {
                if (m_mapBuilder->m_cancel.load())
                {
                    return;
                }

                dtNavMesh* navMesh = dtAllocNavMesh();
                if (!navMesh->init(&tileInfo.m_navMeshParams))
                {
                    printf("[Map %03i] Failed creating navmesh for tile %i,%i !\n", tileInfo.m_mapId, tileInfo.m_tileX, tileInfo.m_tileY);
                    dtFreeNavMesh(navMesh);
                    return;
                }

                buildTile(tileInfo.m_mapId, tileInfo.m_tileX, tileInfo.m_tileY, navMesh, tileInfo.m_curTile, tileInfo.m_tileCount, tileInfo.m_forceRebuild);
                dtFreeNavMesh(navMesh);
            }
            else
            {
                return;
            }
        }
    }

    bool TileWorker::duDumpPolyMeshToObj(rcPolyMesh& pmesh, uint32 mapID, uint32 tileY, uint32 tileX)
    {
        char fname[256];
        sprintf(fname, "meshes/map%03u%02u%02unavmesh.obj", mapID, tileY, tileX);
        FILE* objFile = fopen(fname, "wb");
        if (!objFile)
        {
            printf("duDumpPolyMeshToObj: Can't open file.\n");
            return false;
        }

        const int nvp = pmesh.nvp;
        const float cs = pmesh.cs;
        const float ch = pmesh.ch;
        const float* orig = pmesh.bmin;

        fprintf(objFile, "# MMAP Navmesh\n");
        fprintf(objFile, "o NavMesh\n");
        //fprintf(objFile, "mltlib colors.mtl\n");//Load materials file for coloring the mesh - TODO: add coloring for climblimits etc 

        fprintf(objFile, "\n");

        for (int i = 0; i < pmesh.nverts; ++i)
        {
            const unsigned short* v = &pmesh.verts[i * 3];
            const float x = orig[0] + v[0] * cs;
            const float y = orig[1] + (v[1] + 1) * ch + 0.1f;
            const float z = orig[2] + v[2] * cs;

            fprintf(objFile, "v %f %f %f\n", x, y, z);
        }

        fprintf(objFile, "\n");

        for (int i = 0; i < pmesh.npolys; ++i)
        {
            const unsigned short* p = &pmesh.polys[i * nvp * 2];
            for (int j = 2; j < nvp; ++j)
            {
                if (p[j] == RC_MESH_NULL_IDX) break;
                fprintf(objFile, "f %d %d %d\n", p[0] + 1, p[j - 1] + 1, p[j] + 1);
            }
        }
        fclose(objFile);

        return true;
    }

    bool TileWorker::duDumpPolyMeshDetailToObj(rcPolyMeshDetail& dmesh, uint32 mapID, uint32 tileY, uint32 tileX)
    {

        char fname[256];
        sprintf(fname, "meshes/map%03u%02u%02unavmeshdetail.obj", mapID, tileY, tileX);
        FILE* objFile = fopen(fname, "wb");

        if (!objFile)
        {
            printf("duDumpPolyMeshDetailToObj: Can't open file.\n");
            return false;
        }

        fprintf(objFile, "# MMAP Navmesh\n");
        fprintf(objFile, "o NavMesh\n");

        fprintf(objFile, "\n");

        for (int i = 0; i < dmesh.nverts; ++i)
        {
            const float* v = &dmesh.verts[i * 3];
            fprintf(objFile, "v %f %f %f\n", v[0], v[1], v[2]);
        }

        fprintf(objFile, "\n");

        for (int i = 0; i < dmesh.nmeshes; ++i)
        {
            const unsigned int* m = &dmesh.meshes[i * 4];
            const unsigned int bverts = m[0];
            const unsigned int btris = m[2];
            const unsigned int ntris = m[3];
            const unsigned char* tris = &dmesh.tris[btris * 4];
            for (unsigned int j = 0; j < ntris; ++j)
            {
                fprintf(objFile, "f %d %d %d\n",
                    (int)(bverts + tris[j * 4 + 0]) + 1,
                    (int)(bverts + tris[j * 4 + 1]) + 1,
                    (int)(bverts + tris[j * 4 + 2]) + 1);
            }
        }
        fclose(objFile);

        return true;
    }

    bool TileWorker::shouldSkipTile(uint32 mapID, uint32 tileX, uint32 tileY)
    {
        char fileName[255];
        sprintf(fileName, "mmaps/%03u%02i%02i.mmtile", mapID, tileY, tileX);
        FILE* file = fopen(fileName, "rb");
        if (!file)
        {
            return false;
        }

        MmapTileHeader header;
        int count = fread(&header, sizeof(MmapTileHeader), 1, file);
        fclose(file);
        if (count != 1)
        {
            return false;
        }

        if (header.mmapMagic != MMAP_MAGIC || header.dtVersion != uint32(DT_NAVMESH_VERSION))
        {
            return false;
        }

        if (header.mmapVersion != MMAP_VERSION)
        {
            return false;
        }

        return true;
    }

    void TileWorker::buildTile(uint32 mapID, uint32 tileX, uint32 tileY, dtNavMesh* navMesh, uint32 curTile, uint32 tileCount, bool forceRebuild)
    {
        if (!forceRebuild && shouldSkipTile(mapID, tileX, tileY))
        {
            return;
        }

        printf("[Map %03i] Building tile [%02u,%02u] (%02u / %02u)    \n", mapID, tileX, tileY, curTile, tileCount);

        MeshData meshData;

        // get heightmap data
        m_terrainBuilder->loadMap(mapID, tileX, tileY, meshData);

        // remove unused vertices
        TerrainBuilder::cleanVertices(meshData.solidVerts, meshData.solidTris);
        TerrainBuilder::cleanVertices(meshData.liquidVerts, meshData.liquidTris);

        m_terrainBuilder->loadVMap(mapID, tileX, tileY, meshData); // get model data
        GameObjectBakeStats gameObjectBakeStats = BakeGameObjectModelsIntoMesh(mapID, tileX, tileY, meshData);
        if (gameObjectBakeStats.candidateSpawns || gameObjectBakeStats.bakedSpawns || gameObjectBakeStats.missingModels)
        {
            printf("[GO] map=%u tile=%u,%u: baked %d gameobject model(s), triangles=%d vertices=%d candidates=%d missing=%d\n",
                   mapID, tileX, tileY,
                   gameObjectBakeStats.bakedSpawns,
                   gameObjectBakeStats.addedTriangles,
                   gameObjectBakeStats.addedVertices,
                   gameObjectBakeStats.candidateSpawns,
                   gameObjectBakeStats.missingModels);
        }
        //TerrainBuilder::cleanVertices(meshData.solidVerts, meshData.solidTris);

        // if there is no data, give up now
        if (!meshData.solidVerts.size() && !meshData.liquidVerts.size())
            return;
        // gather all mesh data for final data check, and bounds calculation
        G3D::Array<float> allVerts;
        allVerts.append(meshData.liquidVerts);
        allVerts.append(meshData.solidVerts);

        if (!allVerts.size())
            return;

        // get bounds of current tile
        float bmin[3], bmax[3];
        m_mapBuilder->getTileBounds(tileX, tileY, allVerts.getCArray(), allVerts.size() / 3, bmin, bmax);

        // offmesh.txt (verbose output only for single tile builds)
        m_terrainBuilder->loadOffMeshConnections(mapID, tileX, tileY, meshData, m_mapBuilder->m_offMeshFilePath, tileCount == 1);

        // build navmesh tile
        buildMoveMapTile(mapID, tileX, tileY, meshData, bmin, bmax, navMesh);
        m_terrainBuilder->unloadVMap(mapID, tileX, tileY);
    }

    void TileWorker::buildMoveMapTile(uint32 mapID, uint32 tileX, uint32 tileY, MeshData& meshData, float bmin[3], float bmax[3], dtNavMesh* navMesh)
    {
        // console output
        char tileString[20];
        sprintf(tileString, "[Map %03i] [%02i,%02i]: ", mapID, tileX, tileY);
        printf("%s Building movemap tiles...                          \r", tileString);

        IntermediateValues iv;

        float* tVerts = meshData.solidVerts.getCArray();
        int tVertCount = meshData.solidVerts.size() / 3;
        int* tTris = meshData.solidTris.getCArray();
        int tTriCount = meshData.solidTris.size() / 3;

        float* lVerts = meshData.liquidVerts.getCArray();
        int lVertCount = meshData.liquidVerts.size() / 3;
        int* lTris = meshData.liquidTris.getCArray();
        int lTriCount = meshData.liquidTris.size() / 3;
        uint8* lTriAreas = meshData.liquidType.getCArray();

        rcConfig config;
        memset(&config, 0, sizeof(rcConfig));
        json jsonTileConfig = getTileConfig(mapID, tileX, tileY);
        const std::vector<DebugStageCrop> debugStageCrops = ReadDebugStageCrops(jsonTileConfig);
        if (m_debug && !debugStageCrops.empty())
            ResetDebugStageFiles(mapID, tileX, tileY);
        int const quickFromConfig = jsonTileConfig["quick"].get<int>();
        if (quickFromConfig >= 0)
            m_quick = quickFromConfig == 0 ? false : true;
        config = jsonTileConfig;

        rcVcopy(config.bmin, bmin);
        rcVcopy(config.bmax, bmax);

        bool continent = (mapID <= 1);
        // Should be able to pass here .go xyz -4930 -999 502 0
        float agentHeight = 1.5f;
        float agentRadius = 0.2f; // Check here: .go xyz -4985 -861 501 0
        // Fences should not be passable
        // PFS-OVERHAUL-006 Cycle 17d (2026-05-08): made these per-tile-overridable
        // via JSON ("agentMaxClimbModelTerrainTransition", "agentMaxClimbTerrain")
        // so individual tiles can tighten the cell-connectivity step-up limit when
        // the bake's claim (1.8y) exceeds what runtime physics can actually traverse
        // without a jump (e.g., the OG zeppelin tower's 1.84y deck-lip wall).
        // Default values match the harvested-from-client baseline; do not lower
        // globally — only per-tile when the geometry is provably runtime-impassable.
        float agentMaxClimbModelTerrainTransition = 1.2f;
        float agentMaxClimbTerrain = 1.8f;
        if (jsonTileConfig.contains("agentMaxClimbModelTerrainTransition"))
            agentMaxClimbModelTerrainTransition = jsonTileConfig["agentMaxClimbModelTerrainTransition"].get<float>();
        if (jsonTileConfig.contains("agentMaxClimbTerrain"))
            agentMaxClimbTerrain = jsonTileConfig["agentMaxClimbTerrain"].get<float>();

        if (!continent)
            agentRadius = 0.3f;

        // BEGIN WWoW divergence (Phase 2 of PATHFINDING_OVERHAUL).
        // Honor `agentRadius` / `agentHeight` from the per-map config so
        // continent tiles can be baked for the Tauren Male capsule
        // (agentRadius=1.0247, agentHeight=2.625) instead of the vmangos
        // default (0.2 / 1.5). The existing baked tiles in D:/MaNGOS/data/
        // were produced by an externally-patched MoveMapGenerator that did
        // the same thing; this brings our in-tree generator to parity.
        // See `docs/physics/PATHFINDING_OVERHAUL.md` and `tools/MmapGen/AGENTS.md`.
        if (jsonTileConfig.contains("agentRadius"))
            agentRadius = jsonTileConfig["agentRadius"].get<float>();
        if (jsonTileConfig.contains("agentHeight"))
            agentHeight = jsonTileConfig["agentHeight"].get<float>();
        // END WWoW divergence

        // BEGIN WWoW divergence (PFS-OVERHAUL-006): unified ch=0.1m for both
        // continents and instances. The 0.25m continent default was producing
        // coarse Z quantization on multi-floor structures (zeppelin towers,
        // multi-floor inns, spiral ramps), forcing per-tile overrides. With
        // walkableHeight / walkableClimb auto-derived from agentHeight /
        // agentMaxClimbModelTerrainTransition divided by ch (lines 483-486),
        // changing ch automatically rescales those filters to preserve the
        // intended world-unit clearance / climb. Tile size grows ~1-3% on
        // tiles with sparse vertical structure (most continent terrain) and
        // up to ~2.5x on dense multi-floor tiles (acceptable trade for
        // bake fidelity). Per-tile `ch` and `cs` overrides honored below
        // for tiles that need different precision (rare). cs override is
        // critical for tiles with thin overhanging structures (e.g., the
        // OG zeppelin tower's deck edge) where coarse 0.27m horizontal
        // voxels miss the deck triangle's true XY footprint, causing
        // rcFilterWalkableLowHeightSpans to NOT mark under-deck cells as
        // unwalkable, resulting in 2D-adjacent polygons at different Z.
        config.ch = 0.1f;
        if (jsonTileConfig.contains("ch"))
            config.ch = jsonTileConfig["ch"].get<float>();
        if (jsonTileConfig.contains("cs"))
            config.cs = jsonTileConfig["cs"].get<float>();
        // PFS-OVERHAUL-006 Cycle 16 (2026-05-08): when `cs` is overridden small (e.g. 0.1)
        // the bake must keep TILES_PER_MAP*tileSize*cs >= 533.33y to cover the full map-tile.
        // Default tileSize=80 with cs=0.2666 yields 25*80*0.2666 = 533.2y. With cs=0.1 the
        // same tileSize=80 only covers 200y, leaving the rest of the tile unbaked. The
        // per-tile override `tileSize` lets a fine-cs tile compensate (e.g. tileSize=213
        // with cs=0.1 → 25*213*0.1 = 532.5y).
        if (jsonTileConfig.contains("tileSize"))
            config.tileSize = jsonTileConfig["tileSize"].get<int>();
        // END WWoW divergence

        if (config.walkableHeight == 0)
            config.walkableHeight = (int)ceilf(agentHeight / config.ch);
        if (config.walkableClimb == 0)
            config.walkableClimb = (int)floorf(agentMaxClimbModelTerrainTransition / config.ch); // For models
        uint32 walkableClimbTerrain = (int)floorf(agentMaxClimbTerrain / config.ch);
        uint32 walkableClimbModelTransition = (int)floorf(agentMaxClimbModelTerrainTransition / config.ch);
        if (config.walkableRadius == 0)
            config.walkableRadius = (int)ceilf(agentRadius / config.cs);
        int walkableErosionRadius = config.walkableRadius;
        // [WWoW-DIVERGENCE] WoW lets character feet/capsules overhang many
        // WMO deck edges that Recast would reject if floor support erosion is
        // tied to the full collision capsule. Keep the Detour/header capsule
        // at agentRadius, but allow pathological tiles to use a smaller
        // source-support radius for rcErodeWalkableArea.
        if (jsonTileConfig.contains("walkableErosionRadius"))
        {
            const float erosionRadiusWorld = jsonTileConfig["walkableErosionRadius"].get<float>();
            if (erosionRadiusWorld >= 0.0f)
                walkableErosionRadius = (int)ceilf(erosionRadiusWorld / config.cs);
        }
        if (jsonTileConfig.contains("walkableErosionRadiusCells"))
        {
            const int erosionRadiusCells = jsonTileConfig["walkableErosionRadiusCells"].get<int>();
            if (erosionRadiusCells >= 0)
                walkableErosionRadius = erosionRadiusCells;
        }
        if (walkableErosionRadius < 0)
            walkableErosionRadius = 0;
        if (config.maxEdgeLen == 0)
            config.maxEdgeLen = (int)(12 / config.cs);
        if (config.borderSize == 0)
            config.borderSize = config.walkableRadius + 3;
        if (walkableErosionRadius != config.walkableRadius)
        {
            printf("[ERODE] map=%u tile=%u,%u: agentRadius=%.4f walkableRadiusCells=%d erosionRadiusCells=%d cs=%.4f\n",
                   mapID, tileX, tileY, agentRadius, config.walkableRadius, walkableErosionRadius, config.cs);
        }

        config.width = config.tileSize + config.borderSize * 2;
        config.height = config.tileSize + config.borderSize * 2;

        int inWaterGround = config.walkableHeight;
        int stepForGroundInheriteWater = (int)ceilf(30.0f / config.ch);
        int gameObjectMarks = 0;
        const bool trimAnchorSourceSupportCompactSpans = jsonTileConfig["preRegionCullAnchorSourceSupportCompetingSpans"].get<bool>();
        const bool trimAnchorSourceSupportCompactSpansBeforeMedian =
            jsonTileConfig["preMedianCullAnchorSourceSupportCompetingSpans"].get<bool>();
        const bool restoreAnchorSourceSupportAfterErode = jsonTileConfig["preRegionRestoreAnchorSourceSupportAfterErode"].get<bool>();
        const bool anchorSourceSupportFallbackToWindow = jsonTileConfig["preRegionCullAnchorSourceSupportFallbackToWindow"].get<bool>();
        const bool borrowMissingAnchorSourceSupportFromNeighbors =
            jsonTileConfig.value("borrowMissingAnchorSourceSupportFromNeighbors", false);
        const bool writeAnchorStageManifest = jsonTileConfig.value("writeAnchorStageManifest", false);
        const bool logAnchorStageDiagnostics = jsonTileConfig.value("logAnchorStageDiagnostics", false);
        const bool trimAnchorPolyStacks = jsonTileConfig["postDetourCullAnchorPolyStacks"].get<bool>();
        const bool trimAnchorTrappedComponents = jsonTileConfig.value("postDetourCullAnchorTrappedComponents", false);
        const float anchorSourceSupportXyExtent = JsonFloatOrDefault(
            jsonTileConfig, "postDetourCullAnchorPolyStacksXyExtent", 2.0f);
        const float anchorSourceSupportZExtent = JsonFloatOrDefault(
            jsonTileConfig, "postDetourCullAnchorPolyStacksZExtent", 10.0f);
        const float anchorSourceSupportZTolerance = JsonFloatOrDefault(
            jsonTileConfig, "postDetourCullAnchorPolyStacksSupportZTolerance", 1.0f);
        AnchorSupportBandTuning anchorSupportBandTuning;
        anchorSupportBandTuning.supportFloorSlackBelow = JsonFloatOrDefault(
            jsonTileConfig, "anchorSourceSupportFloorSlackBelow", 0.20f);
        const bool trimAnchorUpperCompactSpans = jsonTileConfig["preRegionCullAnchorUpperCompactSpans"].get<bool>();
        const std::vector<AnchorPolyStackCoord> anchorCompactWorkCoords =
            (trimAnchorSourceSupportCompactSpans || trimAnchorSourceSupportCompactSpansBeforeMedian ||
                restoreAnchorSourceSupportAfterErode || writeAnchorStageManifest || trimAnchorUpperCompactSpans)
                ? ParseAnchorCompactWorkCoords(jsonTileConfig)
                : std::vector<AnchorPolyStackCoord>();
        const std::vector<AnchorPolyStackCoord> anchorPolyStackCoords =
            (trimAnchorPolyStacks || writeAnchorStageManifest)
                ? ParseAnchorPolyStackCoords(jsonTileConfig)
                : std::vector<AnchorPolyStackCoord>();
        const std::vector<AnchorPolyStackCoord> anchorManifestExtraCoords =
            writeAnchorStageManifest
                ? ParseAnchorStageManifestCoords(jsonTileConfig)
                : std::vector<AnchorPolyStackCoord>();
        const std::vector<AnchorPolyStackCoord> prePolyPreserveAnchorSupportCoords =
            ParsePrePolyPreserveAnchorSupportCoords(jsonTileConfig);
        const std::vector<AnchorPolyStackCoord> prePolyUseRawAnchorSupportContours =
            ParsePrePolyUseRawAnchorSupportContours(jsonTileConfig);
        const float prePolyResimplifyAnchorSupportMaxError = JsonFloatOrDefault(
            jsonTileConfig, "prePolyResimplifyAnchorSupportMaxError", -1.0f);
        const int prePolyResimplifyAnchorSupportMaxEdgeLen =
            jsonTileConfig.value("prePolyResimplifyAnchorSupportMaxEdgeLen", -1);
        const float prePolyResimplifyAnchorSupportLocalPreserveRadius = JsonFloatOrDefault(
            jsonTileConfig, "prePolyResimplifyAnchorSupportLocalPreserveRadius", -1.0f);
        const bool prePolyResimplifyAnchorSupportTessellateWallEdges =
            jsonTileConfig.value("prePolyResimplifyAnchorSupportTessellateWallEdges", true);
        const bool prePolyResimplifyAnchorSupportTessellateAreaEdges =
            jsonTileConfig.value("prePolyResimplifyAnchorSupportTessellateAreaEdges", false);
        const std::vector<AnchorPolyStackCoord> anchorManifestBaseCoords =
            writeAnchorStageManifest
                ? MergeUniqueAnchorCoords(anchorCompactWorkCoords, anchorPolyStackCoords)
                : std::vector<AnchorPolyStackCoord>();
        const std::vector<AnchorPolyStackCoord> anchorManifestCoords =
            writeAnchorStageManifest
                ? MergeUniqueAnchorCoords(anchorManifestBaseCoords, anchorManifestExtraCoords)
                : std::vector<AnchorPolyStackCoord>();
        const std::vector<AnchorRouteTarget> anchorRouteTargets =
            (writeAnchorStageManifest || trimAnchorTrappedComponents)
                ? ParseAnchorRouteTargets(jsonTileConfig)
                : std::vector<AnchorRouteTarget>();
        const float walkableSlopeAngleTerrain = config.walkableSlopeAngle;
        const float walkableSlopeAngleVMaps = jsonTileConfig["walkableSlopeAngleVMaps"].get<float>();
        const float playerClimbLimit = cosf(52.0f / 180.0f * RC_PI);
        const float maxClimbLimitTerrain = cosf(walkableSlopeAngleTerrain / 180.0f * RC_PI);
        const float maxClimbLimitVmaps = cosf(walkableSlopeAngleVMaps / 180.0f * RC_PI);
        std::vector<unsigned char> rasterAreas(tTriCount, AREA_NONE);
        float norm[3];
        for (int triIndex = 0; triIndex < tTriCount; ++triIndex)
        {
            const int* tri = &tTris[triIndex * 3];
            calcTriNormal(&tVerts[tri[0] * 3], &tVerts[tri[1] * 3], &tVerts[tri[2] * 3], norm);
            const bool terrain = meshData.IsTerrainTriangle(triIndex);
            float climbHardLimit = terrain ? maxClimbLimitTerrain : maxClimbLimitVmaps;
            if (norm[1] > playerClimbLimit)
                rasterAreas[triIndex] = AREA_GROUND;
            else if (norm[1] > climbHardLimit)
                rasterAreas[triIndex] = AREA_STEEP_SLOPE;

            if (!terrain)
            {
                switch (rasterAreas[triIndex])
                {
                case AREA_GROUND:
                    rasterAreas[triIndex] = AREA_GROUND_MODEL;
                    break;
                case AREA_STEEP_SLOPE:
                    rasterAreas[triIndex] = AREA_STEEP_SLOPE_MODEL;
                    break;
                }
            }

            if (!terrain && rasterAreas[triIndex] && !m_quick)
            {
                float verts[9];
                for (int corner = 0; corner < 3; ++corner)
                    for (int coord = 0; coord < 3; ++coord)
                        verts[3 * corner + coord] = (5 * tVerts[tri[corner] * 3 + coord] + tVerts[tri[(corner + 1) % 3] * 3 + coord] + tVerts[tri[(corner + 2) % 3] * 3 + coord]) / 7;

                if (m_terrainBuilder->IsUnderMap(&verts[0]) && m_terrainBuilder->IsUnderMap(&verts[3]) && m_terrainBuilder->IsUnderMap(&verts[6]))
                    rasterAreas[triIndex] = AREA_NONE;
            }
        }
        const std::vector<AnchorSourceSupportProbe> anchorSourceSupports =
            (trimAnchorSourceSupportCompactSpans || restoreAnchorSourceSupportAfterErode)
                ? ResolveAnchorSourceSupportProbes(
                    meshData, tVerts, tTris, rasterAreas.data(), tTriCount,
                    anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorCompactWorkCoords,
                    borrowMissingAnchorSourceSupportFromNeighbors,
                    logAnchorStageDiagnostics)
                : std::vector<AnchorSourceSupportProbe>();
        const std::vector<AnchorSourceSupportProbe> anchorManifestSupports =
            writeAnchorStageManifest
                ? ResolveAnchorSourceSupportProbes(
                    meshData, tVerts, tTris, rasterAreas.data(), tTriCount,
                    anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorManifestCoords,
                    borrowMissingAnchorSourceSupportFromNeighbors,
                    logAnchorStageDiagnostics)
                : std::vector<AnchorSourceSupportProbe>();
        const std::vector<AnchorSourceSupportProbe> prePolyPreserveAnchorSupportProbes =
            !prePolyPreserveAnchorSupportCoords.empty()
                ? ResolveAnchorSourceSupportProbes(
                    meshData, tVerts, tTris, rasterAreas.data(), tTriCount,
                    anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, prePolyPreserveAnchorSupportCoords,
                    borrowMissingAnchorSourceSupportFromNeighbors,
                    logAnchorStageDiagnostics)
                : std::vector<AnchorSourceSupportProbe>();
        const std::vector<AnchorSourceSupportProbe> prePolyUseRawAnchorSupportProbes =
            !prePolyUseRawAnchorSupportContours.empty()
                ? ResolveAnchorSourceSupportProbes(
                    meshData, tVerts, tTris, rasterAreas.data(), tTriCount,
                    anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, prePolyUseRawAnchorSupportContours,
                    borrowMissingAnchorSourceSupportFromNeighbors,
                    logAnchorStageDiagnostics)
                : std::vector<AnchorSourceSupportProbe>();
        json anchorStageManifest;
        if (writeAnchorStageManifest && !anchorManifestSupports.empty())
        {
            anchorStageManifest =
            {
                { "schemaVersion", 1 },
                { "mapId", mapID },
                { "tileX", tileX },
                { "tileY", tileY },
                { "configKey", std::to_string(tileX) + std::to_string(tileY) },
                { "analysisWindow",
                    {
                        { "xyExtent", anchorSourceSupportXyExtent },
                        { "zExtent", anchorSourceSupportZExtent },
                        { "supportZTolerance", anchorSourceSupportZTolerance },
                    }
                },
                { "anchors", json::array() },
            };

            for (const AnchorSourceSupportProbe& manifestSupport : anchorManifestSupports)
            {
                anchorStageManifest["anchors"].push_back(
                    {
                        { "id", FormatAnchorStageId(manifestSupport.anchor) },
                        { "label", FormatAnchorStageId(manifestSupport.anchor) },
                        { "wowX", manifestSupport.anchor.wowX },
                        { "wowY", manifestSupport.anchor.wowY },
                        { "wowZ", manifestSupport.anchor.wowZ },
                        { "sourceSupport", BuildAnchorSourceSupportJson(manifestSupport) },
                        { "stages", json::array() },
                    });
            }
        }

        auto appendHeightfieldManifestStage = [&](const char* stageName, const rcHeightfield& hf)
        {
            if (!writeAnchorStageManifest)
                return;

            for (size_t anchorIndex = 0; anchorIndex < anchorManifestSupports.size(); ++anchorIndex)
            {
                MergeAnchorStageIntoManifest(
                    anchorStageManifest["anchors"][anchorIndex]["stages"],
                    BuildHeightfieldAnchorStageSummary(
                        stageName,
                        hf,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        anchorManifestSupports[anchorIndex]));
            }
        };

        auto appendCompactManifestStage = [&](const char* stageName, const rcCompactHeightfield& chf, const bool includeComponents)
        {
            if (!writeAnchorStageManifest)
                return;

            for (size_t anchorIndex = 0; anchorIndex < anchorManifestSupports.size(); ++anchorIndex)
            {
                MergeAnchorStageIntoManifest(
                    anchorStageManifest["anchors"][anchorIndex]["stages"],
                    BuildCompactAnchorStageSummary(
                        stageName,
                        chf,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        anchorManifestSupports[anchorIndex],
                        includeComponents));
            }
        };

        auto appendContourManifestStage = [&](const rcContourSet& contours)
        {
            if (!writeAnchorStageManifest)
                return;

            for (size_t anchorIndex = 0; anchorIndex < anchorManifestSupports.size(); ++anchorIndex)
            {
                MergeAnchorStageIntoManifest(
                    anchorStageManifest["anchors"][anchorIndex]["stages"],
                    BuildContourAnchorStageSummary(
                        contours,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        anchorManifestSupports[anchorIndex]));
            }
        };

        auto appendPolyMeshManifestStage = [&](const rcPolyMesh& mesh)
        {
            if (!writeAnchorStageManifest)
                return;

            for (size_t anchorIndex = 0; anchorIndex < anchorManifestSupports.size(); ++anchorIndex)
            {
                MergeAnchorStageIntoManifest(
                    anchorStageManifest["anchors"][anchorIndex]["stages"],
                    BuildPolyMeshAnchorStageSummary(
                        mesh,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        anchorManifestSupports[anchorIndex]));
            }
        };

        // allocate subregions : tiles
        Tile* tiles = new Tile[TILES_PER_MAP * TILES_PER_MAP];

        // Initialize per tile config.
        rcConfig tileCfg;
        memcpy(&tileCfg, &config, sizeof(rcConfig));
        tileCfg.width = config.tileSize + config.borderSize * 2;
        tileCfg.height = config.tileSize + config.borderSize * 2;

        // build all tiles
        for (int y = 0; y < TILES_PER_MAP; ++y)
        {
            for (int x = 0; x < TILES_PER_MAP; ++x)
            {
                Tile& tile = tiles[x + y * TILES_PER_MAP];
                Tile liquidsTile;

                // Calculate the per tile bounding box.
                tileCfg.bmin[0] = config.bmin[0] + x * float(config.tileSize * config.cs);
                tileCfg.bmin[2] = config.bmin[2] + y * float(config.tileSize * config.cs);
                tileCfg.bmax[0] = config.bmin[0] + (x + 1) * float(config.tileSize * config.cs);
                tileCfg.bmax[2] = config.bmin[2] + (y + 1) * float(config.tileSize * config.cs);

                tileCfg.bmin[0] -= tileCfg.borderSize * tileCfg.cs;
                tileCfg.bmin[2] -= tileCfg.borderSize * tileCfg.cs;
                tileCfg.bmax[0] += tileCfg.borderSize * tileCfg.cs;
                tileCfg.bmax[2] += tileCfg.borderSize * tileCfg.cs;

                // NOSTALRIUS - MMAPS TILE GENERATION
                /// 1. Alloc heightfield for walkable areas
                tile.solid = rcAllocHeightfield();
                if (!tile.solid || !rcCreateHeightfield(m_rcContext, *tile.solid, tileCfg.width, tileCfg.height, tileCfg.bmin, tileCfg.bmax, tileCfg.cs, tileCfg.ch))
                {
                    printf("%s Failed building heightfield!                       \n", tileString);
                    continue;
                }

                // [WWoW-DIVERGENCE] 2026-05-07: --debug-heightfield diagnostic.
                // Convert WoW (X, Y) to a recast cell index for THIS sub-tile.
                // Generator/Recast horizontal coordinates are stored as
                // (recast X <- WoW Y, recast Z <- WoW X). Only the sub-tile
                // whose heightfield contains the requested column emits the
                // detailed per-stage span dumps below.
                int dbgCellX = -1, dbgCellY = -1;
                if (m_debugWoWSet)
                {
                    const float recastX = m_debugWoWY;
                    const float recastZ = m_debugWoWX;
                    dbgCellX = (int)std::floor((recastX - tile.solid->bmin[0]) / tileCfg.cs);
                    dbgCellY = (int)std::floor((recastZ - tile.solid->bmin[2]) / tileCfg.cs);
                    const bool inRange = (dbgCellX >= 0 && dbgCellX < tile.solid->width &&
                                          dbgCellY >= 0 && dbgCellY < tile.solid->height);
                    printf("[DBG-HF] tile=(%u,%u) sub=(%d,%d) wow=(%.3f,%.3f) cell=(%d,%d) inRange=%d bmin=(%.3f,%.3f,%.3f) bmax=(%.3f,%.3f,%.3f) cs=%.4f ch=%.4f w=%d h=%d border=%d\n",
                           tileX, tileY, x, y,
                           m_debugWoWX, m_debugWoWY, dbgCellX, dbgCellY, inRange ? 1 : 0,
                           tile.solid->bmin[0], tile.solid->bmin[1], tile.solid->bmin[2],
                           tile.solid->bmax[0], tile.solid->bmax[1], tile.solid->bmax[2],
                           tileCfg.cs, tileCfg.ch, tile.solid->width, tile.solid->height, tileCfg.borderSize);
                    if (!inRange)
                    {
                        dbgCellX = -1; dbgCellY = -1; // helpers no-op on negative
                    }
                }

                /// 2. Generate heightfield for water. Put all liquid geometry there
                // We need to build liquid heighfield to set poly swim flag under.
                liquidsTile.solid = rcAllocHeightfield();
                if (!liquidsTile.solid || !rcCreateHeightfield(m_rcContext, *liquidsTile.solid, tileCfg.width, tileCfg.height, tileCfg.bmin, tileCfg.bmax, tileCfg.cs, tileCfg.ch))
                {
                    printf("%s Failed building liquids heightfield!            \n", tileString);
                    continue;
                }
                rcRasterizeTriangles(m_rcContext, lVerts, lVertCount, lTris, lTriAreas, lTriCount, *liquidsTile.solid, 0);

                /// 3. Triangle walkability was already classified once for the
                // full tile above; the per-subtile bake reuses that stable
                // classification so the stage manifest can aggregate by tile.
                /// 4. Every triangle is correctly marked now, we can rasterize everything.
                // 2026-05-21 upstream Recast sync: the old local SortAndRasterizeTriangles
                // wrapper was retired with the vendor upgrade. Use upstream
                // rcRasterizeTriangles directly to keep the bake pipeline on the
                // migrated Recast surface.
                rcRasterizeTriangles(m_rcContext, tVerts, tVertCount, tTris, rasterAreas.data(), tTriCount, *tile.solid, 0);
                dumpHeightfieldColumn("rasterize", dbgCellX, dbgCellY, *tile.solid);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("rasterize", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("rasterize", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("rasterize", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 5. Don't walk over too high Obstacles.
                // We can pass higher terrain obstacles, or model obstacles.
                // But for terrain->vmap->terrain kind of obstacles, it's harder to climb.
                // (Why? No idea, ask Blizzard. Empirically confirmed on retail)
                // 5.1 walkableClimbTerrain >= walkableClimbModelTransition so do it first
                rcFilterLowHangingWalkableObstacles(m_rcContext, walkableClimbTerrain, *tile.solid);
                dumpHeightfieldColumn("filterLowHanging", dbgCellX, dbgCellY, *tile.solid);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("filterLowHanging", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("filterLowHanging", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("filterLowHanging", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);
                // 5.2 maps <-> vmaps transition
                // PFS-OVERHAUL-006 / Phase 6: pull per-tile ledge-filter knobs from JSON.
                // Defaults (true / false) preserve legacy on tiles that don't opt in.
                const bool treatOobNeighborAsCliff   = jsonTileConfig["treatOobNeighborAsCliff"].get<bool>();
                const bool mixedAreaUsesTerrainClimb = jsonTileConfig["mixedAreaUsesTerrainClimb"].get<bool>();
                filterLedgeSpans(tileCfg.walkableHeight, walkableClimbModelTransition, walkableClimbTerrain, *tile.solid,
                                 treatOobNeighborAsCliff, mixedAreaUsesTerrainClimb);
                //rcFilterLedgeSpans(m_rcContext, tileCfg.walkableHeight, walkableClimbTerrain, *tile.solid); // Default recast code
                dumpHeightfieldColumn("filterLedge", dbgCellX, dbgCellY, *tile.solid);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("filterLedge", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("filterLedge", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("filterLedge", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 6. Now we are happy because we have the correct flags.
                // Set's cleanup tmp flags used by the generator, so we don't have a too
                // complicated navmesh in the end.
                // (We dont care if a poly comes from Terrain or Model at runtime)
                filterRemoveUselessAreas(*tile.solid);
                dumpHeightfieldColumn("removeUseless", dbgCellX, dbgCellY, *tile.solid);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("removeUseless", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("removeUseless", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("removeUseless", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);
                rcFilterWalkableLowHeightSpans(m_rcContext, tileCfg.walkableHeight, *tile.solid);
                dumpHeightfieldColumn("filterLowHeight", dbgCellX, dbgCellY, *tile.solid);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("filterLowHeight", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("filterLowHeight", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("filterLowHeight", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 7. Let's process water now.
                // When water is not deep, we have a transition area (AREA_WATER_TRANSITION)
                // Both ground and water creatures can be there.
                // Otherwise, the terrain in deeper waters is considered as actual swim/water terrain.
                filterWalkableLowHeightSpansWith(*liquidsTile.solid, *tile.solid, inWaterGround, stepForGroundInheriteWater);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportHeightfieldStage("waterInheritance", *tile.solid, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendHeightfieldManifestStage("waterInheritance", *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("waterInheritance", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 8. Now let's move on with the last and more generic steps of navmesh generation.
                // compact heightfield spans
                tile.chf = rcAllocCompactHeightfield();
                if (!tile.chf || !rcBuildCompactHeightfield(m_rcContext, tileCfg.walkableHeight, walkableClimbTerrain, *tile.solid, *tile.chf))
                {
                    printf("%s Failed compacting heightfield!                     \n", tileString);
                    continue;
                }
                dumpCompactHeightfieldColumn("buildCHF", dbgCellX, dbgCellY, *tile.chf);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportCompactStage("buildCHF", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendCompactManifestStage("buildCHF", *tile.chf, false);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("buildCHF", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                gameObjectMarks += MarkGameObjectAreas(m_rcContext, mapID, tileX, tileY, tileCfg, *tile.chf);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportCompactStage("markGameObjects", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendCompactManifestStage("markGameObjects", *tile.chf, false);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("markGameObjects", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                // build polymesh intermediates
                const std::vector<unsigned char> preErodeAreas =
                    restoreAnchorSourceSupportAfterErode
                        ? std::vector<unsigned char>(tile.chf->areas, tile.chf->areas + tile.chf->spanCount)
                        : std::vector<unsigned char>();
                if (!rcErodeWalkableArea(m_rcContext, walkableErosionRadius, *tile.chf))
                {
                    printf("%s Failed eroding area!                               \n", tileString);
                    continue;
                }
                dumpCompactHeightfieldColumn("erode", dbgCellX, dbgCellY, *tile.chf);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportCompactStage("erode", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendCompactManifestStage("erode", *tile.chf, false);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("erode", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                if (restoreAnchorSourceSupportAfterErode)
                {
                    const int restoredSourceSupportSpans = RestoreAnchorSourceSupportCompactSpansAfterErode(
                        *tile.chf,
                        preErodeAreas,
                        anchorSourceSupportXyExtent,
                        anchorSupportBandTuning,
                        anchorSourceSupports,
                        logAnchorStageDiagnostics);
                    if (restoredSourceSupportSpans > 0)
                    {
                        printf("[CHF-SRC-RESTORE] map=%u tile=%u,%u: restored %d compact span(s) after erode\n",
                            mapID, tileX, tileY, restoredSourceSupportSpans);
                    }
                    if (m_debug)
                        WriteCompactHeightfieldStageCsv("anchorSourceSupportRestore", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);
                }

                if (trimAnchorSourceSupportCompactSpansBeforeMedian)
                {
                    const int preMedianCulledSourceSupportCompactSpans = CullAnchorSourceSupportCompactSpans(
                        *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance,
                        anchorSourceSupportFallbackToWindow, anchorSupportBandTuning, anchorSourceSupports,
                        logAnchorStageDiagnostics);
                    if (preMedianCulledSourceSupportCompactSpans > 0)
                    {
                        printf("[CHF-SRC-ANCHOR-CULL-PREMEDIAN] map=%u tile=%u,%u: nulled %d compact span(s) from source support floors before median\n",
                            mapID, tileX, tileY, preMedianCulledSourceSupportCompactSpans);
                    }
                    if (logAnchorStageDiagnostics)
                    {
                        LogAnchorSourceSupportCompactStage("anchorSourceSupportCullPreMedian", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                        LogAnchorSourceSupportCompactComponents("anchorSourceSupportCullPreMedian", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                    }
                    if (m_debug)
                        WriteCompactHeightfieldStageCsv("anchorSourceSupportCullPreMedian", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);
                }

                if (!rcMedianFilterWalkableArea(m_rcContext, *tile.chf))
                {
                    printf("%s Failed filtering area!                             \n", tileString);
                    continue;
                }
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportCompactStage("median", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                if (trimAnchorSourceSupportCompactSpans && logAnchorStageDiagnostics)
                    LogAnchorSourceSupportCompactComponents("median", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                appendCompactManifestStage("median", *tile.chf, true);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("median", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                if (trimAnchorSourceSupportCompactSpans)
                {
                    const int culledSourceSupportCompactSpans = CullAnchorSourceSupportCompactSpans(
                        *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance,
                        anchorSourceSupportFallbackToWindow, anchorSupportBandTuning, anchorSourceSupports,
                        logAnchorStageDiagnostics);
                    if (culledSourceSupportCompactSpans > 0)
                    {
                        printf("[CHF-SRC-ANCHOR-CULL] map=%u tile=%u,%u: nulled %d compact span(s) from source support floors before regions\n",
                            mapID, tileX, tileY, culledSourceSupportCompactSpans);
                    }
                    if (logAnchorStageDiagnostics)
                    {
                        LogAnchorSourceSupportCompactStage("anchorSourceSupportCull", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                        LogAnchorSourceSupportCompactComponents("anchorSourceSupportCull", *tile.chf, anchorSourceSupportXyExtent, anchorSourceSupportZTolerance, anchorSupportBandTuning, anchorSourceSupports);
                    }
                    if (m_debug)
                        WriteCompactHeightfieldStageCsv("anchorSourceSupportCull", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);
                }

                if (trimAnchorUpperCompactSpans)
                {
                    const float anchorPolyStackXyExtent = JsonFloatOrDefault(
                        jsonTileConfig, "postDetourCullAnchorPolyStacksXyExtent", 2.0f);
                    const float anchorPolyStackSupportZTolerance = JsonFloatOrDefault(
                        jsonTileConfig, "postDetourCullAnchorPolyStacksSupportZTolerance", 1.0f);
                    const int culledCompactSpans = CullAnchorUpperCompactSpans(
                        *tile.chf, anchorPolyStackXyExtent, anchorPolyStackSupportZTolerance, anchorCompactWorkCoords,
                        logAnchorStageDiagnostics);
                    if (culledCompactSpans > 0)
                    {
                        printf("[CHF-ANCHOR-CULL] map=%u tile=%u,%u: nulled %d compact span(s) before regions\n",
                            mapID, tileX, tileY, culledCompactSpans);
                    }
                    if (m_debug)
                        WriteCompactHeightfieldStageCsv("anchorCompactCull", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);
                }

                const RegionPartitionType partitionType = ParseRegionPartitionType(jsonTileConfig);
                if (partitionType == RegionPartitionType::Watershed)
                {
                    if (!rcBuildDistanceField(m_rcContext, *tile.chf))
                    {
                        printf("%s Failed building distance field!                    \n", tileString);
                        continue;
                    }

                    if (!rcBuildRegions(m_rcContext, *tile.chf, tileCfg.borderSize, tileCfg.minRegionArea, tileCfg.mergeRegionArea))
                    {
                        printf("%s Failed building watershed regions!                 \n", tileString);
                        continue;
                    }
                }
                else if (partitionType == RegionPartitionType::Monotone)
                {
                    if (!rcBuildRegionsMonotone(m_rcContext, *tile.chf, tileCfg.borderSize, tileCfg.minRegionArea, tileCfg.mergeRegionArea))
                    {
                        printf("%s Failed building monotone regions!                  \n", tileString);
                        continue;
                    }
                }
                else
                {
                    if (!rcBuildLayerRegions(m_rcContext, *tile.chf, tileCfg.borderSize, tileCfg.minRegionArea))
                    {
                        printf("%s Failed building layer regions!                     \n", tileString);
                        continue;
                    }
                }
                printf("[REGION] map=%u tile=%u,%u partition=%s\n", mapID, tileX, tileY, ToString(partitionType));
                appendCompactManifestStage("regions", *tile.chf, true);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("regions", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                tile.cset = rcAllocContourSet();
                if (!tile.cset || !rcBuildContours(m_rcContext, *tile.chf, tileCfg.maxSimplificationError, tileCfg.maxEdgeLen, *tile.cset))
                {
                    printf("%s Failed building contours!                          \n", tileString);
                    continue;
                }
                appendContourManifestStage(*tile.cset);
                if (m_debug)
                    WriteContourStageCsv(mapID, tileX, tileY, x, y, debugStageCrops, *tile.cset);

                if (!prePolyUseRawAnchorSupportProbes.empty())
                {
                    RestoreRawAnchorSupportContours(
                        *tile.cset,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        prePolyUseRawAnchorSupportProbes,
                        logAnchorStageDiagnostics);
                }

                if (!prePolyUseRawAnchorSupportProbes.empty() && prePolyResimplifyAnchorSupportMaxError >= 0.0f)
                {
                    const int resimplifyMaxEdgeLen =
                        prePolyResimplifyAnchorSupportMaxEdgeLen >= 0
                            ? prePolyResimplifyAnchorSupportMaxEdgeLen
                            : tileCfg.maxEdgeLen;
                    int resimplifyBuildFlags = 0;
                    if (prePolyResimplifyAnchorSupportTessellateWallEdges)
                        resimplifyBuildFlags |= RC_CONTOUR_TESS_WALL_EDGES;
                    if (prePolyResimplifyAnchorSupportTessellateAreaEdges)
                        resimplifyBuildFlags |= RC_CONTOUR_TESS_AREA_EDGES;
                    ResimplifyRawAnchorSupportContours(
                        *tile.cset,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        prePolyUseRawAnchorSupportProbes,
                        prePolyResimplifyAnchorSupportMaxError,
                        resimplifyMaxEdgeLen,
                        prePolyResimplifyAnchorSupportLocalPreserveRadius,
                        resimplifyBuildFlags,
                        logAnchorStageDiagnostics);
                }

                if (!prePolyPreserveAnchorSupportProbes.empty())
                {
                    PreserveAnchorSupportContourBorderVertices(
                        *tile.cset,
                        anchorSourceSupportXyExtent,
                        anchorSourceSupportZTolerance,
                        anchorSupportBandTuning,
                        prePolyPreserveAnchorSupportProbes,
                        logAnchorStageDiagnostics);
                }

                // build polymesh
                tile.pmesh = rcAllocPolyMesh();
                if (!tile.pmesh || !rcBuildPolyMesh(m_rcContext, *tile.cset, tileCfg.maxVertsPerPoly, *tile.pmesh))
                {
                    printf("%s Failed building polymesh!                          \n", tileString);
                    continue;
                }
                appendPolyMeshManifestStage(*tile.pmesh);

                tile.dmesh = rcAllocPolyMeshDetail();
                if (!tile.dmesh || !rcBuildPolyMeshDetail(m_rcContext, *tile.pmesh, *tile.chf, tileCfg.detailSampleDist, tileCfg.detailSampleMaxError, *tile.dmesh))
                {
                    printf("%s Failed building polymesh detail!                   \n", tileString);
                    continue;
                }

                // free those up
                // we may want to keep them in the future for debug
                // but right now, we don't have the code to merge them
                rcFreeHeightField(tile.solid);
                tile.solid = nullptr;
                rcFreeCompactHeightfield(tile.chf);
                tile.chf = nullptr;
                rcFreeContourSet(tile.cset);
                tile.cset = nullptr;
            }
        }

        if (gameObjectMarks > 0)
            printf("[GO] map=%u tile=%u,%u: marked %d fallback gameobject span box(es)\n", mapID, tileX, tileY, gameObjectMarks);

        // merge per tile poly and detail meshes
        rcPolyMesh** pmmerge = new rcPolyMesh * [TILES_PER_MAP * TILES_PER_MAP];
        rcPolyMeshDetail** dmmerge = new rcPolyMeshDetail * [TILES_PER_MAP * TILES_PER_MAP];

        int nmerge = 0;
        for (int y = 0; y < TILES_PER_MAP; ++y)
        {
            for (int x = 0; x < TILES_PER_MAP; ++x)
            {
                Tile& tile = tiles[x + y * TILES_PER_MAP];
                if (tile.pmesh)
                {
                    pmmerge[nmerge] = tile.pmesh;
                    dmmerge[nmerge] = tile.dmesh;
                    nmerge++;
                }
            }
        }

        iv.polyMesh = rcAllocPolyMesh();
        if (!iv.polyMesh)
        {
            delete[] tiles;
            delete[] pmmerge;
            delete[] dmmerge;
            printf("%s alloc iv.polyMesh FAILED!                          \r", tileString);
            return;
        }
        rcMergePolyMeshes(m_rcContext, pmmerge, nmerge, *iv.polyMesh);

        iv.polyMeshDetail = rcAllocPolyMeshDetail();
        if (!iv.polyMeshDetail)
        {
            printf("%s alloc m_dmesh FAILED!                              \r", tileString);
            delete[] tiles;
            delete[] pmmerge;
            delete[] dmmerge;
            return;
        }
        rcMergePolyMeshDetails(m_rcContext, dmmerge, nmerge, *iv.polyMeshDetail);

        const int culledSuspiciousPolys = CullSuspiciousMixedWallPolys(
            *iv.polyMesh,
            *iv.polyMeshDetail,
            config.walkableSlopeAngle,
            agentMaxClimbTerrain);
        if (culledSuspiciousPolys > 0)
        {
            printf("[POLY-CULL] map=%u tile=%u,%u: disabled %d suspicious mixed-wall polygon(s)\n",
                mapID, tileX, tileY, culledSuspiciousPolys);
        }

        // free things up
        delete[] pmmerge;
        delete[] dmmerge;
        delete[] tiles;

        // set polygons as walkable
        // TODO: special flags for DYNAMIC polygons, ie surfaces that can be turned on and off
        for (int i = 0; i < iv.polyMesh->npolys; ++i)
            if (iv.polyMesh->areas[i] != RC_NULL_AREA)
            {
                switch (iv.polyMesh->areas[i] & 0xF)
                {
                case AREA_NONE:
                    break;
                case AREA_GROUND:
                    iv.polyMesh->flags[i] |= NAV_GROUND;
                    break;
                case AREA_STEEP_SLOPE:
                    iv.polyMesh->flags[i] |= (NAV_GROUND | NAV_STEEP_SLOPES);
                    break;
                case AREA_WATER_TRANSITION:
                    iv.polyMesh->flags[i] |= (NAV_GROUND | NAV_WATER);
                    break;
                case AREA_WATER:
                    iv.polyMesh->flags[i] |= NAV_WATER;
                    break;
                case AREA_MAGMA:
                    iv.polyMesh->flags[i] |= NAV_MAGMA;
                    break;
                case AREA_SLIME:
                    iv.polyMesh->flags[i] |= NAV_SLIME;
                    break;
                default:
                    iv.polyMesh->flags[i] |= 0x1;
                    //printf("%s uses unknown area %u     \n", tileString, iv.polyMesh->areas[i]);
                    break;
                }
            }

        // setup mesh parameters
        dtNavMeshCreateParams params;
        memset(&params, 0, sizeof(params));
        params.verts = iv.polyMesh->verts;
        params.vertCount = iv.polyMesh->nverts;
        params.polys = iv.polyMesh->polys;
        params.polyAreas = iv.polyMesh->areas;
        params.polyFlags = iv.polyMesh->flags;
        params.polyCount = iv.polyMesh->npolys;
        params.nvp = iv.polyMesh->nvp;
        params.detailMeshes = iv.polyMeshDetail->meshes;
        params.detailVerts = iv.polyMeshDetail->verts;
        params.detailVertsCount = iv.polyMeshDetail->nverts;
        params.detailTris = iv.polyMeshDetail->tris;
        params.detailTriCount = iv.polyMeshDetail->ntris;

        params.offMeshConVerts = meshData.offMeshConnections.getCArray();
        params.offMeshConCount = meshData.offMeshConnections.size() / 6;
        params.offMeshConRad = meshData.offMeshConnectionRads.getCArray();
        params.offMeshConDir = meshData.offMeshConnectionDirs.getCArray();
        params.offMeshConAreas = meshData.offMeshConnectionsAreas.getCArray();
        params.offMeshConFlags = meshData.offMeshConnectionsFlags.getCArray();

        params.walkableHeight = agentHeight;
        params.walkableRadius = agentRadius;
        params.walkableClimb = agentMaxClimbTerrain;
        params.tileX = (((bmin[0] + bmax[0]) / 2) - navMesh->getParams()->orig[0]) / GRID_SIZE;
        params.tileY = (((bmin[2] + bmax[2]) / 2) - navMesh->getParams()->orig[2]) / GRID_SIZE;
        params.tileLayer = 0;
        params.buildBvTree = true;
        rcVcopy(params.bmin, bmin);
        rcVcopy(params.bmax, bmax);
        params.cs = config.cs;
        params.ch = config.ch;

        // will hold final navmesh
        unsigned char* navData = nullptr;
        int navDataSize = 0;

        do
        {
            // these values are checked within dtCreateNavMeshData - handle them here
            // so we have a clear error message
            if (params.nvp > DT_VERTS_PER_POLYGON)
            {
                printf("%s Invalid verts-per-polygon value!                   \n", tileString);
                continue;
            }
            if (params.vertCount >= 0xffff)
            {
                printf("%s Too many vertices! (0x%8x)                         \n", tileString, params.vertCount);
                exit(0);
                continue;
            }
            if (!params.vertCount || !params.verts)
            {
                // occurs mostly when adjacent tiles have models
                // loaded but those models don't span into this tile

                // message is an annoyance
                //printf("%sNo vertices to build tile!              \n", tileString);
                continue;
            }
            if (!params.polyCount || !params.polys)
            {
                // we have flat tiles with no actual geometry - don't build those, its useless
                // keep in mind that we do output those into debug info
                // drop tiles with only exact count - some tiles may have geometry while having less tiles
                printf("%s No polygons to build on tile!                      \n", tileString);
                continue;
            }
            if (!params.detailMeshes || !params.detailVerts || !params.detailTris)
            {
                printf("%s No detail mesh to build tile!                      \n", tileString);
                continue;
            }

            printf("%s Building navmesh tile...                           \r", tileString);
            if (!dtCreateNavMeshData(&params, &navData, &navDataSize))
            {
                printf("%s Failed building navmesh tile!                      \n", tileString);
                continue;
            }

            dtTileRef tileRef = 0;
            printf("%s Adding tile to navmesh...                          \r", tileString);
            // DT_TILE_FREE_DATA tells detour to unallocate memory when the tile
            // is removed via removeTile()
            dtStatus dtResult = navMesh->addTile(navData, navDataSize, DT_TILE_FREE_DATA, 0, &tileRef);
            if (!tileRef || dtStatusFailed(dtResult))
            {
                printf("%s Failed adding tile to navmesh (0x%x)               \n", tileString, dtResult);
                continue;
            }

            const dtMeshTile* addedTileConst = navMesh->getTileByRef(tileRef);
            if (addedTileConst)
            {
                dtMeshTile* addedTile = const_cast<dtMeshTile*>(addedTileConst);
                const bool trimShadowedLedges = jsonTileConfig["postDetourCullShadowedLedges"].get<bool>();
                const bool trimOffMeshAnchorSteepTrim = jsonTileConfig["postDetourCullOffMeshAnchorSteepTrim"].get<bool>();
                const bool trimShadowedPockets = jsonTileConfig["postDetourCullShadowedPockets"].get<bool>();
                OffMeshAnchorSteepTrimSettings offMeshAnchorSteepTrimSettings;
                offMeshAnchorSteepTrimSettings.minAverageSlopeDegrees = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimMinAverageSlopeDegrees",
                    OFFMESH_ANCHOR_STEEP_TRIM_MIN_AVERAGE_SLOPE_DEGREES);
                offMeshAnchorSteepTrimSettings.maxEdge2D = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimMaxEdge2D",
                    OFFMESH_ANCHOR_STEEP_TRIM_MAX_EDGE_2D);
                offMeshAnchorSteepTrimSettings.maxArea2D = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimMaxArea2D",
                    OFFMESH_ANCHOR_STEEP_TRIM_MAX_AREA_2D);
                offMeshAnchorSteepTrimSettings.maxZRange = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimMaxZRange",
                    OFFMESH_ANCHOR_STEEP_TRIM_MAX_Z_RANGE);
                offMeshAnchorSteepTrimSettings.componentMaxArea2D = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimComponentMaxArea2D",
                    OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_AREA_2D);
                offMeshAnchorSteepTrimSettings.componentMaxZRange = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullOffMeshAnchorSteepTrimComponentMaxZRange",
                    OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_Z_RANGE);
                const float anchorPolyStackXyExtent = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullAnchorPolyStacksXyExtent", 2.0f);
                const float anchorPolyStackZExtent = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullAnchorPolyStacksZExtent", 10.0f);
                const float anchorPolyStackSupportZTolerance = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullAnchorPolyStacksSupportZTolerance", 1.0f);
                const float anchorPolyStackSupportGap2D = JsonFloatOrDefault(
                    jsonTileConfig, "postDetourCullAnchorPolyStacksSupportGap2D", 0.0f);
                if (trimAnchorPolyStacks)
                {
                    printf("[DT-ANCHOR-CULL] map=%u tile=%u,%u config enabled coords=%zu xy=%.2f z=%.2f supportTol=%.2f supportGap=%.2f trapped=%d routeTargets=%zu\n",
                        mapID, tileX, tileY, anchorPolyStackCoords.size(), anchorPolyStackXyExtent,
                        anchorPolyStackZExtent, anchorPolyStackSupportZTolerance, anchorPolyStackSupportGap2D,
                        trimAnchorTrappedComponents ? 1 : 0, anchorRouteTargets.size());
                }
                const int culledDetourPolys = CullSuspiciousDetourPolys(
                    *navMesh, *addedTile, agentMaxClimbTerrain,
                    trimShadowedLedges, trimOffMeshAnchorSteepTrim, trimShadowedPockets,
                    offMeshAnchorSteepTrimSettings,
                    trimAnchorPolyStacks, anchorPolyStackXyExtent, anchorPolyStackZExtent,
                    anchorPolyStackSupportZTolerance, anchorPolyStackSupportGap2D,
                    anchorPolyStackCoords, anchorSourceSupports,
                    trimAnchorTrappedComponents, anchorRouteTargets);
                if (culledDetourPolys > 0)
                {
                    printf("[DT-POLY-CULL] map=%u tile=%u,%u: disabled %d final suspicious Detour polygon(s)\n",
                        mapID, tileX, tileY, culledDetourPolys);
                }

                if (writeAnchorStageManifest && !anchorManifestSupports.empty())
                {
                    std::vector<DetourPolyDiagnostics> finalDiagnostics(addedTile->header->polyCount);
                    std::vector<unsigned char> finalLiveGroundMask(addedTile->header->polyCount, 0);
                    for (int polyIndex = 0; polyIndex < addedTile->header->polyCount; ++polyIndex)
                    {
                        const dtPoly& poly = addedTile->polys[polyIndex];
                        if (poly.getType() != DT_POLYTYPE_GROUND || poly.vertCount < 3)
                            continue;

                        finalDiagnostics[polyIndex] = AnalyzeDetourPoly(*addedTile, polyIndex, DETOUR_FINAL_CULL_MAX_SLOPE_DEGREES);
                        if (IsWalkableLandPoly(poly))
                            finalLiveGroundMask[polyIndex] = 1;
                    }

                    for (size_t anchorIndex = 0; anchorIndex < anchorManifestSupports.size(); ++anchorIndex)
                    {
                        anchorStageManifest["anchors"][anchorIndex]["stages"].push_back(
                            BuildFinalDetourAnchorStageSummary(
                                *navMesh,
                                *addedTile,
                                finalDiagnostics,
                                finalLiveGroundMask,
                                anchorSourceSupportXyExtent,
                                anchorSourceSupportZExtent,
                                anchorSourceSupportZTolerance,
                                anchorSupportBandTuning,
                                anchorManifestSupports[anchorIndex],
                                anchorRouteTargets));
                    }
                }
            }

            // file output
            char fileName[255];
            sprintf(fileName, "mmaps/%03u%02i%02i.mmtile", mapID, tileY, tileX);
            FILE* file = fopen(fileName, "wb");
            if (!file)
            {
                char message[1024];
                sprintf(message, "[Map %03i] Failed to open %s for writing!             \n", mapID, fileName);
                perror(message);
                navMesh->removeTile(tileRef, nullptr, nullptr);
                continue;
            }

            printf("%s Writing to file...                                 \r", tileString);

            // write header
            MmapTileHeader header;
            header.size = uint32(navDataSize);
            header.usesLiquids = m_terrainBuilder->usesLiquids() ? 1 : 0;
            fwrite(&header, sizeof(MmapTileHeader), 1, file);

            // write data
            fwrite(navData, sizeof(unsigned char), navDataSize, file);
            fclose(file);

            if (writeAnchorStageManifest && !anchorManifestSupports.empty())
            {
                anchorStageManifest["outputTileFileName"] = fileName;
                anchorStageManifest["outputTileSize"] = navDataSize;
                anchorStageManifest["postDetourCullSummary"] =
                {
                    { "shadowedLedgesEnabled", jsonTileConfig["postDetourCullShadowedLedges"].get<bool>() },
                    { "offMeshAnchorSteepTrimEnabled", jsonTileConfig["postDetourCullOffMeshAnchorSteepTrim"].get<bool>() },
                    { "shadowedPocketsEnabled", jsonTileConfig["postDetourCullShadowedPockets"].get<bool>() },
                    { "anchorPolyStacksEnabled", jsonTileConfig["postDetourCullAnchorPolyStacks"].get<bool>() },
                    { "anchorTrappedComponentsEnabled", jsonTileConfig.value("postDetourCullAnchorTrappedComponents", false) },
                };

                char manifestFileName[256];
                sprintf(manifestFileName, "meshes/map%03u%02u%02u_anchor_stage_manifest.json", mapID, tileY, tileX);
                std::ofstream manifestFile(manifestFileName, std::ios::binary | std::ios::trunc);
                if (manifestFile)
                {
                    manifestFile << anchorStageManifest.dump(2);
                }
                else
                {
                    printf("[ANCHOR-STAGE] map=%u tile=%u,%u failed to write manifest %s\n",
                        mapID, tileX, tileY, manifestFileName);
                }
            }

            if (m_debug)
            {
                //Generate 3D obj files
                //VMAP
                iv.generateObjFile(mapID, tileX, tileY, meshData);

                //MMAP  
                duDumpPolyMeshDetailToObj(*iv.polyMeshDetail, mapID, tileY, tileX);
                duDumpPolyMeshToObj(*iv.polyMesh, mapID, tileY, tileX);

                iv.writeIV(mapID, tileX, tileY);
                // Write navmesh data
                char fname[256];
                sprintf(fname, "meshes/map%03u%02u%02u.nav", mapID, tileY, tileX);
                FILE* file = fopen(fname, "wb");
                if (file)
                {
                    fwrite(&navDataSize, sizeof(uint32), 1, file);
                    fwrite(navData, sizeof(unsigned char), navDataSize, file);
                    fclose(file);
                }
            }
            // now that tile is written to disk, we can unload it
            navMesh->removeTile(tileRef, nullptr, nullptr);
        } while (0);
    }

    json TileWorker::getDefaultConfig()
    {
        return
        {
            { "borderSize",              0     }, // placeholder
            { "detailSampleDist",        2.0f  },
            { "detailSampleMaxError",    0.5f  },
            { "maxEdgeLen",              0     }, // placeholder
            { "maxVertsPerPoly",         DT_VERTS_PER_POLYGON },
            { "maxSimplificationError",  1.8f  },
            { "partitionType",           "watershed" },
            { "mergeRegionArea",         10    },
            { "minRegionArea",           30    },
            { "walkableClimb",           0     }, // placeholder
            { "walkableHeight",          0     }, // placeholder
            { "walkableRadius",          0     }, // placeholder
            { "walkableErosionRadius",   -1.0f }, // world units; -1 uses walkableRadius
            { "walkableErosionRadiusCells", -1  }, // cells; overrides world-unit erosion radius when >= 0
            { "walkableSlopeAngle",      75.0f }, // slope terrain
            { "walkableSlopeAngleVMaps", 61.0f }, // slope model (WMO...)
            { "quick",                   -1    }, // skip 'undermesh removal'
            // PFS-OVERHAUL-006 / Phase 6: per-tile filterLedgeSpans overrides.
            // Defaults preserve legacy behavior on every tile that does not opt in.
            // See filterLedgeSpans for what each flag controls.
            { "treatOobNeighborAsCliff",  true  },
            { "mixedAreaUsesTerrainClimb", false },
            { "postDetourCullShadowedLedges", false },
            { "postDetourCullOffMeshAnchorSteepTrim", false },
            { "postDetourCullOffMeshAnchorSteepTrimMinAverageSlopeDegrees", OFFMESH_ANCHOR_STEEP_TRIM_MIN_AVERAGE_SLOPE_DEGREES },
            { "postDetourCullOffMeshAnchorSteepTrimMaxEdge2D", OFFMESH_ANCHOR_STEEP_TRIM_MAX_EDGE_2D },
            { "postDetourCullOffMeshAnchorSteepTrimMaxArea2D", OFFMESH_ANCHOR_STEEP_TRIM_MAX_AREA_2D },
            { "postDetourCullOffMeshAnchorSteepTrimMaxZRange", OFFMESH_ANCHOR_STEEP_TRIM_MAX_Z_RANGE },
            { "postDetourCullOffMeshAnchorSteepTrimComponentMaxArea2D", OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_AREA_2D },
            { "postDetourCullOffMeshAnchorSteepTrimComponentMaxZRange", OFFMESH_ANCHOR_STEEP_TRIM_COMPONENT_MAX_Z_RANGE },
            { "postDetourCullShadowedPockets", false },
            { "preMedianCullAnchorSourceSupportCompetingSpans", false },
            { "preRegionCullAnchorSourceSupportCompetingSpans", false },
            { "preRegionRestoreAnchorSourceSupportAfterErode", false },
            { "preRegionCullAnchorSourceSupportFallbackToWindow", false },
            { "anchorSourceSupportFloorSlackBelow", 0.20f },
            { "preRegionCullAnchorUpperCompactSpans", false },
            { "borrowMissingAnchorSourceSupportFromNeighbors", false },
            { "preRegionAnchorCoordsWow", json::array() },
            { "postDetourCullAnchorPolyStacks", false },
            { "postDetourCullAnchorTrappedComponents", false },
            { "postDetourCullAnchorPolyStacksXyExtent", 2.0f },
            { "postDetourCullAnchorPolyStacksZExtent", 10.0f },
            { "postDetourCullAnchorPolyStacksSupportZTolerance", 1.0f },
            { "postDetourCullAnchorPolyStacksCoordsWow", json::array() },
            { "anchorStageManifestCoordsWow", json::array() },
            { "anchorRouteTargetsWow", json::array() },
            { "writeAnchorStageManifest", false },
            { "logAnchorStageDiagnostics", false },
        };
    }

    static void MergeConfigValues(json& target, const json& source)
    {
        if (!source.is_object())
            return;

        for (auto it = source.begin(); it != source.end(); ++it)
            target[it.key()] = it.value();
    }

    json TileWorker::getMapIdConfig(uint32 mapId)
    {
        std::string key = std::to_string(mapId);

        json config = getDefaultConfig();
        if (m_config.find(key) != m_config.end())
            MergeConfigValues(config, m_config.at(key));

        return config;
    }

    json TileWorker::getTileConfig(uint32 mapId, uint32 tileX, uint32 tileY)
    {
        std::string key = std::to_string(tileX) + std::to_string(tileY);

        json config = getMapIdConfig(mapId);
        if (config.find(key) != config.end())
        {
            const json tileOverrides = config.at(key);
            MergeConfigValues(config, tileOverrides);
        }

        for (json::iterator it = config.begin(); it != config.end();) {
            if ((*it).is_object())
                it = config.erase(it);
            else
                ++it;
        }

        return config;
    }
}
