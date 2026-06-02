#include "TileWorker.h"
#include "MapBuilder.h"
#include "Maps/GridMapDefines.h"
#include "DetourNavMeshQuery.h"
#include "RecastAlloc.h"
#include "BakeProfile.h"
#include <algorithm>
#include <array>
#include <cctype>
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

static std::string JsonStringOrDefault(const nlohmann::json& obj, const char* name, const char* defaultValue)
{
    auto it = obj.find(name);
    if (it == obj.end() || !it->is_string())
        return defaultValue;

    return it->get<std::string>();
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

static rcConfig BuildTileRasterConfig(const rcConfig& config, const int tileX, const int tileY)
{
    rcConfig tileConfig;
    memcpy(&tileConfig, &config, sizeof(rcConfig));
    tileConfig.width = config.tileSize + config.borderSize * 2;
    tileConfig.height = config.tileSize + config.borderSize * 2;

    tileConfig.bmin[0] = config.bmin[0] + tileX * float(config.tileSize * config.cs);
    tileConfig.bmin[2] = config.bmin[2] + tileY * float(config.tileSize * config.cs);
    tileConfig.bmax[0] = config.bmin[0] + (tileX + 1) * float(config.tileSize * config.cs);
    tileConfig.bmax[2] = config.bmin[2] + (tileY + 1) * float(config.tileSize * config.cs);

    tileConfig.bmin[0] -= tileConfig.borderSize * tileConfig.cs;
    tileConfig.bmin[2] -= tileConfig.borderSize * tileConfig.cs;
    tileConfig.bmax[0] += tileConfig.borderSize * tileConfig.cs;
    tileConfig.bmax[2] += tileConfig.borderSize * tileConfig.cs;
    return tileConfig;
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

static void from_json(const json& j, rcConfig& config)
{
    // Phase 1 iter 24: cs/ch derived from MakeBakeProfile per Mononen rules
    // (cs = r/2 outdoor, ch = cs/2). Replaces the prior MMAP::BASE_UNIT_DIM
    // (0.2666) cs/ch hardcode which violated the ch=cs/2 rule. tileSize,
    // borderSize, and walkable* remain on the json + auto-derive path for
    // bounded scope in iter 24; iter 25+ may unify them through BakeProfile.
    const auto bakeProfile = MMAP::MakeBakeProfile(MMAP::kTaurenM, /*indoor=*/false);
    config.tileSize               = MMAP::VERTEX_PER_TILE;
    config.borderSize             = j["borderSize"].get<int>();
    config.cs                     = bakeProfile.cs;
    config.ch                     = bakeProfile.ch;
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
        // See `docs/Archive/PATHFINDING_OVERHAUL.md` and `tools/MmapGen/AGENTS.md`.
        if (jsonTileConfig.contains("agentRadius"))
            agentRadius = jsonTileConfig["agentRadius"].get<float>();
        if (jsonTileConfig.contains("agentHeight"))
            agentHeight = jsonTileConfig["agentHeight"].get<float>();
        // END WWoW divergence

        // Phase 1 iter 24: cs/ch arrive from from_json via MakeBakeProfile
        // (Mononen rule cs=r/2 outdoor, ch=cs/2). The prior PFS-OVERHAUL-006
        // Cycle-16 unconditional `config.ch = 0.1f` override is removed because
        // it masked the from_json ch value, defeating any Mononen-rule tuning.
        //
        // Per-tile json "cs"/"ch" overrides are still honored for the rare
        // tiles that need a different precision than the default outdoor
        // profile (e.g. tile 4029 OG zep deck-edge with cs=0.1 fine horizontal).
        //
        // CRITICAL (iter 23 audit finding): some per-tile blocks (notably
        // 4029) override cs but NOT ch. Pre-iter-24 the unconditional
        // `config.ch = 0.1f` masked this — those tiles ran cs=override,
        // ch=0.1. After removing that override, a per-tile cs-only override
        // would inherit the from_json Mononen ch (0.2562) producing a
        // mismatched ratio (e.g. cs=0.1, ch=0.2562, ratio 2.56) that explodes
        // the polymesh vertex count. To preserve the Mononen ch=cs/2 rule
        // PER-TILE, auto-derive ch=cs/2 when a per-tile cs override is
        // applied without a matching ch override.
        const bool perTileChOverride = jsonTileConfig.contains("ch");
        const bool perTileCsOverride = jsonTileConfig.contains("cs");
        if (perTileChOverride)
            config.ch = jsonTileConfig["ch"].get<float>();
        if (perTileCsOverride)
        {
            config.cs = jsonTileConfig["cs"].get<float>();
            if (!perTileChOverride)
                config.ch = config.cs * 0.5f;  // Mononen ch=cs/2 per-tile auto-derive
        }
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

                /// 4. Every triangle is correctly marked now, we can rasterize everything.
                // 2026-05-21 upstream Recast sync: the old local SortAndRasterizeTriangles
                // wrapper was retired with the vendor upgrade. Use upstream
                // rcRasterizeTriangles directly to keep the bake pipeline on the
                // migrated Recast surface.
                rcRasterizeTriangles(m_rcContext, tVerts, tVertCount, tTris, rasterAreas.data(), tTriCount, *tile.solid, 0);
                dumpHeightfieldColumn("rasterize", dbgCellX, dbgCellY, *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("rasterize", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 5. Don't walk over too high Obstacles.
                // We can pass higher terrain obstacles, or model obstacles.
                // But for terrain->vmap->terrain kind of obstacles, it's harder to climb.
                // (Why? No idea, ask Blizzard. Empirically confirmed on retail)
                // 5.1 walkableClimbTerrain >= walkableClimbModelTransition so do it first
                rcFilterLowHangingWalkableObstacles(m_rcContext, walkableClimbTerrain, *tile.solid);
                dumpHeightfieldColumn("filterLowHanging", dbgCellX, dbgCellY, *tile.solid);
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
                if (m_debug)
                    WriteHeightfieldStageCsv("filterLedge", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 6. Now we are happy because we have the correct flags.
                // Set's cleanup tmp flags used by the generator, so we don't have a too
                // complicated navmesh in the end.
                // (We dont care if a poly comes from Terrain or Model at runtime)
                filterRemoveUselessAreas(*tile.solid);
                dumpHeightfieldColumn("removeUseless", dbgCellX, dbgCellY, *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("removeUseless", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);
                rcFilterWalkableLowHeightSpans(m_rcContext, tileCfg.walkableHeight, *tile.solid);
                dumpHeightfieldColumn("filterLowHeight", dbgCellX, dbgCellY, *tile.solid);
                if (m_debug)
                    WriteHeightfieldStageCsv("filterLowHeight", mapID, tileX, tileY, x, y, debugStageCrops, *tile.solid);

                /// 7. Let's process water now.
                // When water is not deep, we have a transition area (AREA_WATER_TRANSITION)
                // Both ground and water creatures can be there.
                // Otherwise, the terrain in deeper waters is considered as actual swim/water terrain.
                filterWalkableLowHeightSpansWith(*liquidsTile.solid, *tile.solid, inWaterGround, stepForGroundInheriteWater);
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
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("buildCHF", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                gameObjectMarks += MarkGameObjectAreas(m_rcContext, mapID, tileX, tileY, tileCfg, *tile.chf);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("markGameObjects", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                // build polymesh intermediates
                if (!rcErodeWalkableArea(m_rcContext, walkableErosionRadius, *tile.chf))
                {
                    printf("%s Failed eroding area!                               \n", tileString);
                    continue;
                }
                dumpCompactHeightfieldColumn("erode", dbgCellX, dbgCellY, *tile.chf);
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("erode", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                if (!rcMedianFilterWalkableArea(m_rcContext, *tile.chf))
                {
                    printf("%s Failed filtering area!                             \n", tileString);
                    continue;
                }
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("median", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

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
                if (m_debug)
                    WriteCompactHeightfieldStageCsv("regions", mapID, tileX, tileY, x, y, debugStageCrops, *tile.chf);

                tile.cset = rcAllocContourSet();
                if (!tile.cset ||
                    !rcBuildContours(m_rcContext, *tile.chf, tileCfg.maxSimplificationError, tileCfg.maxEdgeLen, *tile.cset))
                {
                    printf("%s Failed building contours!                          \n", tileString);
                    continue;
                }
                if (m_debug)
                    WriteContourStageCsv(mapID, tileX, tileY, x, y, debugStageCrops, *tile.cset);

                // build polymesh
                tile.pmesh = rcAllocPolyMesh();
                if (!tile.pmesh || !rcBuildPolyMesh(m_rcContext, *tile.cset, tileCfg.maxVertsPerPoly, *tile.pmesh))
                {
                    printf("%s Failed building polymesh!                          \n", tileString);
                    continue;
                }

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
            { "detailSampleDist",        1.6f  }, // Phase 1: cs * 6 with cs=BASE_UNIT_DIM
            { "detailSampleMaxError",    0.5f  },
            { "maxEdgeLen",              0     }, // placeholder
            { "maxVertsPerPoly",         DT_VERTS_PER_POLYGON },
            { "maxSimplificationError",  1.3f  }, // Phase 1: Mononen target; proposal rejects >=1.5
            { "partitionType",           "watershed" },
            { "mergeRegionArea",         40    }, // Phase 1: TrinityCore default; old 10 too small
            { "minRegionArea",           20    }, // Phase 1: TrinityCore default; old 30 too large
            { "walkableClimb",           0     }, // placeholder
            { "walkableHeight",          0     }, // placeholder
            { "walkableRadius",          0     }, // placeholder
            { "walkableErosionRadius",   -1.0f }, // world units; -1 uses walkableRadius
            { "walkableErosionRadiusCells", -1  }, // cells; overrides world-unit erosion radius when >= 0
            { "walkableSlopeAngle",      60.0f }, // Phase 1: physics MAX_SLOPE; was 75 (over-permissive)
            { "walkableSlopeAngleVMaps", 60.0f }, // Phase 1: unified with terrain at physics MAX_SLOPE
            { "quick",                   -1    }, // skip 'undermesh removal'
            // PFS-OVERHAUL-006 / Phase 6: per-tile filterLedgeSpans overrides.
            // Defaults preserve legacy behavior on every tile that does not opt in.
            // See filterLedgeSpans for what each flag controls.
            { "treatOobNeighborAsCliff",  true  },
            { "mixedAreaUsesTerrainClimb", false },
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
