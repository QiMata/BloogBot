// tools/SceneCacheBuilder — Phase 4 GO variants
//
// Produces per-tile binary scene-cache files containing the world-space
// collision triangles of every server-spawned GameObject in a given variant's
// spawn set. The runtime ingests these into DynamicObjectRegistry's
// per-(mapId, variantId) triangle pool, where they participate in
// SceneQuery::GetGroundZ and capsule overlap queries.
//
// Inputs
//   gameobject_spawns/<variant>.json     (produced by GameObjectExporter --variant)
//   <vmaps>/temp_gameobject_models       (displayId -> model name + bounds index)
//   <vmaps>/<modelName>.vmo              (per-model collision mesh)
//
// Output
//   <out>/<map:03d><tileY:02d><tileX:02d>.<variant>.scenecache
//
// File format (little-endian, native float layout)
//   bytes 0-3    magic = 'SCNC'
//   bytes 4-7    version = 1
//   bytes 8-11   mapId
//   bytes 12-15  tileX
//   bytes 16-19  tileY
//   bytes 20-23  variantNameLen
//   bytes 24+    variantName (variantNameLen bytes, ASCII, no terminator)
//   next 4       triangleCount
//   then         triangleCount * 9 floats (a.xyz, b.xyz, c.xyz; world-space)
//
// CLI
//   SceneCacheBuilder <mapId>
//                     [--variant <name>]   default: base
//                     [--tile X,Y[;X,Y...]] limit output to specific tiles
//                     [--spawns <path>]    default: gameobject_spawns/<variant>.json
//                     [--vmaps <path>]     default: ./vmaps
//                     [--out <dir>]        default: ./scene-cache
//                     [--silent]
//
// The transform applied to every model triangle matches
// DynamicObjectRegistry::RebuildWorldTriangles (Exports/Navigation):
// scale -> rotate around Z (orientation radians) -> translate.

// MMAP_GENERATOR is defined by CMake (see tools/MmapGen/CMakeLists.txt).

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <map>
#include <memory>
#include <set>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

#include <nlohmann/json.hpp>

#include "WorldModel.h"

namespace fs = std::filesystem;
using nlohmann::json;
using G3D::Vector3;

namespace
{
    constexpr float GRID_SIZE = 533.33333f;
    constexpr float ORIGIN = 32.0f * GRID_SIZE;        // 17066.66
    constexpr uint32_t SCNC_MAGIC = 0x434E4353;        // 'SCNC' little-endian
    constexpr uint32_t SCNC_VERSION = 1;

    struct Spawn
    {
        uint32_t displayId;
        float x, y, z;
        float orientation;
        float scale;
    };

    struct DisplayInfo
    {
        std::string modelName;
        Vector3 boundsMin;
        Vector3 boundsMax;
    };

    struct ModelMesh
    {
        std::vector<Vector3> vertices;
        std::vector<uint32_t> indices;
    };

    struct ProgramOptions
    {
        uint32_t mapId = UINT32_MAX;
        std::string variant = "base";
        std::set<std::pair<int, int>> tileFilter;   // empty = no filter
        std::string spawnsPath;
        std::string vmapsDir = "vmaps";
        std::string outputDir = "scene-cache";
        bool silent = false;
    };

    void printUsage()
    {
        std::cerr <<
            "Usage:\n"
            "  SceneCacheBuilder <mapId> [--variant <name>] [--tile X,Y[;X,Y...]]\n"
            "                    [--spawns <path>] [--vmaps <path>] [--out <dir>] [--silent]\n"
            "\n"
            "Defaults:\n"
            "  --variant base\n"
            "  --spawns  gameobject_spawns/<variant>.json\n"
            "  --vmaps   ./vmaps\n"
            "  --out     ./scene-cache\n";
    }

    bool parseTileList(const std::string& s, std::set<std::pair<int, int>>& out)
    {
        // Format: "X,Y" or "X,Y;X,Y;..."
        std::stringstream ss(s);
        std::string item;
        while (std::getline(ss, item, ';'))
        {
            if (item.empty()) continue;
            auto comma = item.find(',');
            if (comma == std::string::npos) return false;
            try
            {
                int x = std::stoi(item.substr(0, comma));
                int y = std::stoi(item.substr(comma + 1));
                out.emplace(x, y);
            }
            catch (...) { return false; }
        }
        return !out.empty();
    }

    bool loadDisplayIdMapping(const std::string& vmapsBasePath,
                              std::unordered_map<uint32_t, DisplayInfo>& out)
    {
        std::string indexPath = vmapsBasePath + "/temp_gameobject_models";
        if (!fs::exists(indexPath))
        {
            std::cerr << "[SceneCacheBuilder] Index file not found: " << indexPath << "\n";
            return false;
        }

        FILE* rf = fopen(indexPath.c_str(), "rb");
        if (!rf) return false;

        int count = 0;
        while (true)
        {
            uint32_t displayId, nameLen;
            if (fread(&displayId, sizeof(uint32_t), 1, rf) != 1) break;
            if (fread(&nameLen, sizeof(uint32_t), 1, rf) != 1) break;
            if (nameLen == 0 || nameLen > 500)
            {
                std::cerr << "[SceneCacheBuilder] Bad nameLen=" << nameLen
                          << " at entry " << count << "\n";
                break;
            }

            std::vector<char> nameBuf(nameLen + 1, 0);
            if (fread(nameBuf.data(), 1, nameLen, rf) != nameLen) break;

            float bbox[6];
            if (fread(bbox, sizeof(float), 6, rf) != 6) break;

            DisplayInfo info;
            info.modelName = std::string(nameBuf.data());
            info.boundsMin = Vector3(bbox[0], bbox[1], bbox[2]);
            info.boundsMax = Vector3(bbox[3], bbox[4], bbox[5]);
            out[displayId] = std::move(info);
            ++count;
        }
        fclose(rf);
        return count > 0;
    }

    bool loadSpawns(const std::string& path, uint32_t mapId, std::vector<Spawn>& out)
    {
        if (!fs::exists(path))
        {
            std::cerr << "[SceneCacheBuilder] Spawns file not found: " << path << "\n";
            return false;
        }

        std::ifstream in(path);
        if (!in.good()) return false;

        json doc;
        try { in >> doc; }
        catch (const std::exception& ex)
        {
            std::cerr << "[SceneCacheBuilder] JSON parse error in " << path
                      << ": " << ex.what() << "\n";
            return false;
        }

        std::string mapKey = std::to_string(mapId);
        if (!doc.contains(mapKey))
        {
            std::cerr << "[SceneCacheBuilder] No spawns for map " << mapKey
                      << " in " << path << "\n";
            return true;  // empty result is OK; we just write zero tiles
        }

        for (auto& sp : doc[mapKey])
        {
            Spawn s;
            s.displayId   = sp.value("displayId", 0u);
            s.x           = sp.value("x", 0.0f);
            s.y           = sp.value("y", 0.0f);
            s.z           = sp.value("z", 0.0f);
            s.orientation = sp.value("o", 0.0f);
            s.scale       = sp.value("s", 1.0f);
            if (s.scale <= 0.0f) s.scale = 1.0f;
            if (s.displayId == 0) continue;
            out.push_back(s);
        }
        return true;
    }

    std::shared_ptr<ModelMesh> loadVmoModel(const std::string& vmapsBasePath,
                                            const std::string& modelName)
    {
        // Try modelName + ".vmo", then lowercase, then case-insensitive scan
        std::string vmoPath = vmapsBasePath + "/" + modelName + ".vmo";
        if (!fs::exists(vmoPath))
        {
            std::string lower = modelName;
            std::transform(lower.begin(), lower.end(), lower.begin(),
                           [](unsigned char c) { return (char)std::tolower(c); });
            vmoPath = vmapsBasePath + "/" + lower + ".vmo";
        }
        if (!fs::exists(vmoPath))
        {
            std::string targetLower = modelName + ".vmo";
            std::transform(targetLower.begin(), targetLower.end(), targetLower.begin(),
                           [](unsigned char c) { return (char)std::tolower(c); });
            vmoPath.clear();
            std::error_code ec;
            for (const auto& entry : fs::directory_iterator(vmapsBasePath, ec))
            {
                if (!entry.is_regular_file()) continue;
                std::string fn = entry.path().filename().string();
                std::string fnLower = fn;
                std::transform(fnLower.begin(), fnLower.end(), fnLower.begin(),
                               [](unsigned char c) { return (char)std::tolower(c); });
                if (fnLower == targetLower) { vmoPath = entry.path().string(); break; }
            }
        }

        if (vmoPath.empty() || !fs::exists(vmoPath)) return nullptr;

        VMAP::WorldModel wm;
        if (!wm.readFile(vmoPath)) return nullptr;

        std::vector<VMAP::GroupModel> groups;
        wm.getGroupModels(groups);

        auto mesh = std::make_shared<ModelMesh>();
        for (auto& group : groups)
        {
            std::vector<Vector3> tempVerts;
            std::vector<VMAP::MeshTriangle> tempTris;
            VMAP::WmoLiquid* liquid = nullptr;
            group.getMeshData(tempVerts, tempTris, liquid);

            uint32_t vertOffset = (uint32_t)mesh->vertices.size();
            mesh->vertices.insert(mesh->vertices.end(), tempVerts.begin(), tempVerts.end());
            mesh->indices.reserve(mesh->indices.size() + tempTris.size() * 3);
            for (auto& t : tempTris)
            {
                mesh->indices.push_back(vertOffset + t.idx0);
                mesh->indices.push_back(vertOffset + t.idx1);
                mesh->indices.push_back(vertOffset + t.idx2);
            }
        }
        if (mesh->vertices.empty() || mesh->indices.empty()) return nullptr;
        return mesh;
    }

    // WoW (X, Y) -> MmapGen (tileX, tileY).
    //   tileX = floor((ORIGIN - WoW.Y) / GRID_SIZE)
    //   tileY = floor((ORIGIN - WoW.X) / GRID_SIZE)
    inline int worldYtoTileX(float wy) { return (int)std::floor((ORIGIN - wy) / GRID_SIZE); }
    inline int worldXtoTileY(float wx) { return (int)std::floor((ORIGIN - wx) / GRID_SIZE); }

    void appendTriangles(const Spawn& spawn, const ModelMesh& mesh,
                         const DisplayInfo& display,
                         std::map<std::pair<int, int>, std::vector<float>>& tilesOut)
    {
        const float cosO = std::cos(spawn.orientation);
        const float sinO = std::sin(spawn.orientation);

        // Transform every vertex once so triangle assembly is a simple lookup.
        std::vector<Vector3> world(mesh.vertices.size());
        Vector3 worldMin, worldMax;
        for (size_t i = 0; i < mesh.vertices.size(); ++i)
        {
            const Vector3& v = mesh.vertices[i];
            const float sx = v.x * spawn.scale;
            const float sy = v.y * spawn.scale;
            const float sz = v.z * spawn.scale;
            const float wx = sx * cosO - sy * sinO + spawn.x;
            const float wy = sx * sinO + sy * cosO + spawn.y;
            const float wz = sz + spawn.z;
            world[i] = Vector3(wx, wy, wz);
            if (i == 0) { worldMin = worldMax = world[i]; }
            else
            {
                worldMin = worldMin.min(world[i]);
                worldMax = worldMax.max(world[i]);
            }
        }

        // Tile range from world AABB (X/Y; vertical Z is irrelevant).
        const int tileXMin = std::max(0, std::min(63, worldYtoTileX(worldMax.y)));
        const int tileXMax = std::max(0, std::min(63, worldYtoTileX(worldMin.y)));
        const int tileYMin = std::max(0, std::min(63, worldXtoTileY(worldMax.x)));
        const int tileYMax = std::max(0, std::min(63, worldXtoTileY(worldMin.x)));

        // Pre-pack triangles into a flat float array (9 per tri).
        std::vector<float> packed;
        packed.reserve(mesh.indices.size() * 3);
        for (size_t t = 0; t + 2 < mesh.indices.size(); t += 3)
        {
            const Vector3& a = world[mesh.indices[t]];
            const Vector3& b = world[mesh.indices[t + 1]];
            const Vector3& c = world[mesh.indices[t + 2]];
            packed.push_back(a.x); packed.push_back(a.y); packed.push_back(a.z);
            packed.push_back(b.x); packed.push_back(b.y); packed.push_back(b.z);
            packed.push_back(c.x); packed.push_back(c.y); packed.push_back(c.z);
        }
        if (packed.empty())
        {
            (void)display;  // not currently used for filtering; reserved for future bbox-based skip
            return;
        }

        for (int ty = tileYMin; ty <= tileYMax; ++ty)
        {
            for (int tx = tileXMin; tx <= tileXMax; ++tx)
            {
                auto& bucket = tilesOut[{tx, ty}];
                bucket.insert(bucket.end(), packed.begin(), packed.end());
            }
        }
    }

    bool writeSceneCache(const std::string& path,
                         uint32_t mapId, int tileX, int tileY,
                         const std::string& variantName,
                         const std::vector<float>& triFloats)
    {
        FILE* wf = fopen(path.c_str(), "wb");
        if (!wf)
        {
            std::cerr << "[SceneCacheBuilder] Failed to open output: " << path << "\n";
            return false;
        }

        const uint32_t magic = SCNC_MAGIC;
        const uint32_t version = SCNC_VERSION;
        const uint32_t mid = mapId;
        const uint32_t tx = (uint32_t)tileX;
        const uint32_t ty = (uint32_t)tileY;
        const uint32_t nameLen = (uint32_t)variantName.size();
        const uint32_t triCount = (uint32_t)(triFloats.size() / 9);

        bool ok = true;
        ok = ok && fwrite(&magic,   sizeof(uint32_t), 1, wf) == 1;
        ok = ok && fwrite(&version, sizeof(uint32_t), 1, wf) == 1;
        ok = ok && fwrite(&mid,     sizeof(uint32_t), 1, wf) == 1;
        ok = ok && fwrite(&tx,      sizeof(uint32_t), 1, wf) == 1;
        ok = ok && fwrite(&ty,      sizeof(uint32_t), 1, wf) == 1;
        ok = ok && fwrite(&nameLen, sizeof(uint32_t), 1, wf) == 1;
        if (ok && nameLen > 0)
            ok = fwrite(variantName.data(), 1, nameLen, wf) == nameLen;
        ok = ok && fwrite(&triCount, sizeof(uint32_t), 1, wf) == 1;
        if (ok && triCount > 0)
            ok = fwrite(triFloats.data(), sizeof(float), triFloats.size(), wf) == triFloats.size();

        fclose(wf);
        if (!ok) std::cerr << "[SceneCacheBuilder] Write failed: " << path << "\n";
        return ok;
    }

    bool parseArgs(int argc, char** argv, ProgramOptions& opts)
    {
        bool gotMap = false;
        for (int i = 1; i < argc; ++i)
        {
            std::string a = argv[i];
            if (a == "--variant" && i + 1 < argc) { opts.variant = argv[++i]; }
            else if (a == "--tile" && i + 1 < argc)
            {
                if (!parseTileList(argv[++i], opts.tileFilter))
                {
                    std::cerr << "Invalid --tile value\n"; return false;
                }
            }
            else if (a == "--spawns" && i + 1 < argc) { opts.spawnsPath = argv[++i]; }
            else if (a == "--vmaps"  && i + 1 < argc) { opts.vmapsDir   = argv[++i]; }
            else if (a == "--out"    && i + 1 < argc) { opts.outputDir  = argv[++i]; }
            else if (a == "--silent") { opts.silent = true; }
            else if (a == "--help" || a == "-h") { printUsage(); std::exit(0); }
            else if (!a.empty() && a[0] == '-')
            {
                std::cerr << "Unknown flag: " << a << "\n"; return false;
            }
            else if (!gotMap)
            {
                try { opts.mapId = (uint32_t)std::stoul(a); gotMap = true; }
                catch (...) { std::cerr << "Invalid mapId: " << a << "\n"; return false; }
            }
            else
            {
                std::cerr << "Unexpected argument: " << a << "\n"; return false;
            }
        }
        if (!gotMap) { std::cerr << "Missing required <mapId>\n"; return false; }

        if (opts.variant.empty() || opts.variant.find('/') != std::string::npos
            || opts.variant.find('\\') != std::string::npos)
        {
            std::cerr << "Invalid --variant: must be non-empty without path separators\n";
            return false;
        }
        if (opts.spawnsPath.empty())
            opts.spawnsPath = "gameobject_spawns/" + opts.variant + ".json";
        return true;
    }
}

int main(int argc, char** argv)
{
    ProgramOptions opts;
    if (!parseArgs(argc, argv, opts)) { printUsage(); return 2; }

    if (!opts.silent)
        std::cout << "[SceneCacheBuilder] map=" << opts.mapId
                  << " variant=" << opts.variant
                  << " spawns=" << opts.spawnsPath
                  << " vmaps=" << opts.vmapsDir
                  << " out=" << opts.outputDir << "\n";

    std::unordered_map<uint32_t, DisplayInfo> displayMap;
    if (!loadDisplayIdMapping(opts.vmapsDir, displayMap))
    {
        std::cerr << "[SceneCacheBuilder] Failed to load temp_gameobject_models\n";
        return 3;
    }
    if (!opts.silent)
        std::cout << "[SceneCacheBuilder] Loaded " << displayMap.size()
                  << " displayId mappings\n";

    std::vector<Spawn> spawns;
    if (!loadSpawns(opts.spawnsPath, opts.mapId, spawns))
    {
        std::cerr << "[SceneCacheBuilder] Failed to load spawns\n";
        return 4;
    }
    if (!opts.silent)
        std::cout << "[SceneCacheBuilder] Loaded " << spawns.size()
                  << " spawns for map " << opts.mapId << "\n";

    // Lazy-load + cache models. Skip displayIds whose model is unresolvable.
    std::unordered_map<std::string, std::shared_ptr<ModelMesh>> meshCache;
    std::map<std::pair<int, int>, std::vector<float>> tilesOut;
    int processed = 0, skipped = 0;
    for (const Spawn& spawn : spawns)
    {
        auto it = displayMap.find(spawn.displayId);
        if (it == displayMap.end()) { ++skipped; continue; }

        const std::string& modelName = it->second.modelName;
        auto mit = meshCache.find(modelName);
        if (mit == meshCache.end())
        {
            auto mesh = loadVmoModel(opts.vmapsDir, modelName);
            mit = meshCache.emplace(modelName, mesh).first;
            if (!mesh && !opts.silent)
                std::cout << "[SceneCacheBuilder] (no .vmo for displayId="
                          << spawn.displayId << " model=" << modelName << ")\n";
        }
        if (!mit->second) { ++skipped; continue; }

        appendTriangles(spawn, *mit->second, it->second, tilesOut);
        ++processed;
    }

    if (!opts.silent)
        std::cout << "[SceneCacheBuilder] Processed " << processed
                  << " spawns, skipped " << skipped
                  << " (model missing). Cached " << meshCache.size() << " models.\n";

    // Apply --tile filter if given.
    if (!opts.tileFilter.empty())
    {
        std::map<std::pair<int, int>, std::vector<float>> filtered;
        for (auto& kv : tilesOut)
            if (opts.tileFilter.count(kv.first))
                filtered.emplace(kv.first, std::move(kv.second));
        tilesOut.swap(filtered);
    }

    fs::create_directories(opts.outputDir);
    int filesWritten = 0;
    uint64_t totalTris = 0;
    for (auto& kv : tilesOut)
    {
        const int tx = kv.first.first;
        const int ty = kv.first.second;
        const auto& triFloats = kv.second;
        if (triFloats.empty()) continue;

        char nameBuf[64];
        std::snprintf(nameBuf, sizeof(nameBuf), "%03u%02d%02d.%s.scenecache",
                      opts.mapId, ty, tx, opts.variant.c_str());
        std::string outPath = opts.outputDir + "/" + nameBuf;

        if (!writeSceneCache(outPath, opts.mapId, tx, ty, opts.variant, triFloats))
            return 5;
        ++filesWritten;
        totalTris += triFloats.size() / 9;
    }

    if (!opts.silent)
        std::cout << "[SceneCacheBuilder] Wrote " << filesWritten
                  << " tile files (" << totalTris << " triangles total) for map="
                  << opts.mapId << " variant=" << opts.variant << " to "
                  << opts.outputDir << "\n";
    return 0;
}
