#include "VMapManager2.h"
#include "StaticMapTree.h"
#include "WorldModel.h"
#include "VMapDefinitions.h"
#include "VMapLog.h"
#include <sstream>
#include <iomanip>
#include <filesystem>
#include <iostream>
#include <fstream>
#include <algorithm>
#include <unordered_map>
#include <cmath>
#include <limits>
#include "ModelInstance.h"
#include "CoordinateTransforms.h"
#include "SceneQuery.h"
#include "CapsuleCollision.h"

namespace VMAP
{
    std::vector<SceneHit> VMapManager2::SweepCapsuleAll(unsigned int pMapId,
        const CapsuleCollision::Capsule& capsuleStart,
        const G3D::Vector3& dir,
        float distance,
        uint32_t includeMask) const
    {
        std::vector<SceneHit> out;
        auto it = iInstanceMapTrees.find(pMapId);
        if (it == iInstanceMapTrees.end() || it->second == nullptr)
            return out;
        StaticMapTree* tree = it->second;
        // Forward to SceneQuery implementation which operates on StaticMapTree
        QueryParams qp; qp.includeMask = includeMask;
        SceneQuery::SweepCapsule(*tree, capsuleStart, dir, distance, out, includeMask, qp);
        return out;
    }

    // Global model name to path mapping
    static std::unordered_map<std::string, std::string> modelNameToPath;
    static bool modelMappingLoaded = false;

    // Scan entire vmaps directory and build complete model mapping
    void BuildCompleteModelMapping(const std::string& basePath)
    {
        if (modelMappingLoaded)
        {
            return;
        }

        modelNameToPath.clear();

        // Helper function to add a model to our mapping
        auto addModel = [](const std::filesystem::path& path) {
            std::string fullPath = path.string();
            std::string filename = path.filename().string();

            // Normalize path separators
            std::replace(fullPath.begin(), fullPath.end(), '\\', '/');

            // Store with multiple key variations for robust lookup
            std::string lowerName = filename;
            std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(), ::tolower);
            modelNameToPath[lowerName] = fullPath;

            // Also store without extension
            size_t dotPos = lowerName.find_last_of('.');
            if (dotPos != std::string::npos)
            {
                std::string nameNoExt = lowerName.substr(0, dotPos);
                modelNameToPath[nameNoExt] = fullPath;

                // Store with different extensions for lookup
                modelNameToPath[nameNoExt + ".wmo"] = fullPath;
                modelNameToPath[nameNoExt + ".m2"] = fullPath;
                modelNameToPath[nameNoExt + ".mdx"] = fullPath;
                modelNameToPath[nameNoExt + ".mdl"] = fullPath;
            }

            // Also store original case
            modelNameToPath[filename] = fullPath;
            };

        try
        {
            // Recursively scan entire vmaps directory
            auto dirIt = std::filesystem::recursive_directory_iterator(basePath);
            auto endIt = std::filesystem::recursive_directory_iterator();

            while (dirIt != endIt)
            {
                const auto& entry = *dirIt;
                if (entry.is_regular_file())
                {
                    std::string ext = entry.path().extension().string();
                    std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);

                    if (ext == ".vmo")
                    {
                        addModel(entry.path());
                    }
                }
                ++dirIt;
            }

            // Enhanced breakdown by type
            int vmoCount = 0, dtreeCount = 0;
            auto mapIt = modelNameToPath.begin();
            while (mapIt != modelNameToPath.end()) {
                if (mapIt->second.find("GameObjectModels") != std::string::npos) dtreeCount++;
                else vmoCount++;
                ++mapIt;
            }

            // Also try to load GameObjectModels.dtree if it exists
            std::string dtreeFile = basePath + "GameObjectModels.dtree";
            if (std::filesystem::exists(dtreeFile))
            {
                FILE* rf = fopen(dtreeFile.c_str(), "rb");
                if (rf)
                {
                    char magic[8];
                    if (fread(magic, 1, 8, rf) == 8)
                    {
                        uint32_t numModels;
                        if (fread(&numModels, sizeof(uint32_t), 1, rf) == 1)
                        {
                            uint32_t i = 0;
                            while (i < numModels)
                            {
                                uint32_t fileId;
                                uint32_t nameLen;

                                if (fread(&fileId, sizeof(uint32_t), 1, rf) != 1) break;
                                if (fread(&nameLen, sizeof(uint32_t), 1, rf) != 1) break;

                                if (nameLen > 0 && nameLen < 512)
                                {
                                    std::vector<char> nameBuff(nameLen + 1, 0);
                                    if (fread(nameBuff.data(), 1, nameLen, rf) == nameLen)
                                    {
                                        std::string modelName(nameBuff.data());

                                        // Try to find the corresponding .vmo file
                                        std::stringstream ss;
                                        ss << basePath << "GameObjectModels/"
                                            << std::setfill('0') << std::setw(8) << fileId << ".vmo";

                                        std::string vmoPath = ss.str();
                                        if (std::filesystem::exists(vmoPath))
                                        {
                                            // Clean up model name
                                            size_t lastSlash = modelName.find_last_of("/\\");
                                            if (lastSlash != std::string::npos)
                                                modelName = modelName.substr(lastSlash + 1);

                                            std::string lowerName = modelName;
                                            std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(), ::tolower);

                                            modelNameToPath[lowerName] = vmoPath;
                                            modelNameToPath[modelName] = vmoPath;

                                            // Also without extension
                                            size_t dotPos = lowerName.find_last_of('.');
                                            if (dotPos != std::string::npos)
                                            {
                                                modelNameToPath[lowerName.substr(0, dotPos)] = vmoPath;
                                            }
                                        }
                                    }
                                }
                                ++i;
                            }
                        }
                    }
                    fclose(rf);
                }
            }
        }
        catch (const std::exception& e)
        {
            std::cerr << "Error building model mapping: " << e.what();
        }

        modelMappingLoaded = true;
    }

    // Resolve model name to actual file path
    std::string ResolveModelPath(const std::string& basePath, const std::string& modelName)
    {
        // Ensure mapping is built
        if (!modelMappingLoaded)
        {
            BuildCompleteModelMapping(basePath);
        }

        // Clean up the model name
        std::string searchName = modelName;

        // Remove any path components
        size_t lastSlash = searchName.find_last_of("/\\");
        if (lastSlash != std::string::npos)
            searchName = searchName.substr(lastSlash + 1);

        // Try lowercase lookup first
        std::string lowerName = searchName;
        std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(), ::tolower);

        auto it = modelNameToPath.find(lowerName);
        if (it != modelNameToPath.end())
        {
            if (std::filesystem::exists(it->second))
            {
                return it->second;
            }
        }

        // Try original case
        it = modelNameToPath.find(searchName);
        if (it != modelNameToPath.end())
        {
            if (std::filesystem::exists(it->second))
            {
                return it->second;
            }
        }

        // Try without extension
        size_t dotPos = lowerName.find_last_of('.');
        if (dotPos != std::string::npos)
        {
            std::string nameNoExt = lowerName.substr(0, dotPos);
            it = modelNameToPath.find(nameNoExt);
            if (it != modelNameToPath.end())
            {
                if (std::filesystem::exists(it->second))
                {
                    return it->second;
                }
            }
        }

        // Last resort - try direct paths
        std::vector<std::string> tryPaths = {
            basePath + searchName,
            basePath + lowerName,
            basePath + "GameObjectModels/" + searchName,
            basePath + "GameObjectModels/" + lowerName
        };

        // If it's a .wmo or .m2, try with .vmo extension
        if (dotPos != std::string::npos)
        {
            std::string nameNoExt = searchName.substr(0, dotPos);
            tryPaths.push_back(basePath + nameNoExt + ".vmo");
            tryPaths.push_back(basePath + "GameObjectModels/" + nameNoExt + ".vmo");
        }

        auto pathIt = tryPaths.begin();
        while (pathIt != tryPaths.end())
        {
            if (std::filesystem::exists(*pathIt))
            {
                return *pathIt;
            }
            ++pathIt;
        }

        return "";
    }

    // Constructor
    VMapManager2::VMapManager2()
    {
    }

    // Destructor
    VMapManager2::~VMapManager2()
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::~VMapManager2 (dtor)");
        try
        {
            auto treeIt = iInstanceMapTrees.begin();
            while (treeIt != iInstanceMapTrees.end())
            {
                delete treeIt->second;
                ++treeIt;
            }
            iInstanceMapTrees.clear();
            iLoadedModelFiles.clear();
            iLoadedMaps.clear();
        }
        catch (const std::exception& e)
        {
            std::cerr << "Exception in destructor: " << e.what();
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::~VMapManager2 (dtor)");
    }

    void VMapManager2::setBasePath(const std::string& path)
    {
        iBasePath = path;
        if (!iBasePath.empty() && iBasePath.back() != '/' && iBasePath.back() != '\\')
            iBasePath += "/";

        // Build the complete model mapping when base path is set
        BuildCompleteModelMapping(iBasePath);
    }

    void VMapManager2::initializeMap(uint32_t mapId)
    {
        if (iLoadedMaps.count(mapId) > 0)
        {
            return;
        }

        std::string mapFileName = getMapFileName(mapId);
        std::string fullPath = iBasePath + mapFileName;

        if (!std::filesystem::exists(fullPath))
        {
            return;
        }

        // Get file size
        auto fileSize = std::filesystem::file_size(fullPath);

        // Quick check if file is readable
        FILE* rf = fopen(fullPath.c_str(), "rb");
        if (!rf)
        {
            return;
        }
        fclose(rf);

        StaticMapTree* newTree = new StaticMapTree(mapId, iBasePath);

        if (newTree->InitMap(mapFileName, this))
        {
            iInstanceMapTrees[mapId] = newTree;
            iLoadedMaps.insert(mapId);
        }
        else
        {
            delete newTree;
        }
    }

    bool VMapManager2::isUnderModel(unsigned int pMapId, float x, float y, float z,
        float* outDist, float* inDist) const
    {
        // Silent: function is known-good; avoid noisy PERF tracing here
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = convertPositionToInternalRep(x, y, z);
            bool res = instanceTree->second->isUnderModel(pos, outDist, inDist);
            return res;
        }
        return false;
    }

    std::string VMapManager2::getMapFileName(unsigned int pMapId)
    {
        std::stringstream fname;
        fname << std::setfill('0') << std::setw(3) << pMapId << ".vmtree";
        return fname.str();
    }

    G3D::Vector3 VMapManager2::convertPositionToInternalRep(float x, float y, float z) const
    {
        // Removed verbose PERF logs
        G3D::Vector3 v = NavCoord::WorldToInternal(x, y, z);
        return v;
    }

    VMAPLoadResult VMapManager2::loadMap(const char* pBasePath, unsigned int pMapId, int x, int y)
    {
        // Suppress verbose ENTER/EXIT logging for loadMap
        if (pBasePath && strlen(pBasePath) > 0)
        {
            std::string oldPath = iBasePath;
            iBasePath = pBasePath;
            if (!iBasePath.empty() && iBasePath.back() != '/' && iBasePath.back() != '\\')
                iBasePath += "/";
            if (oldPath != iBasePath)
                BuildCompleteModelMapping(iBasePath);
        }
        if (!std::filesystem::exists(iBasePath))
        {
            return VMAP_LOAD_RESULT_ERROR;
        }
        if (!isMapInitialized(pMapId))
            initializeMap(pMapId);
        if (!isMapInitialized(pMapId))
        {
            return VMAP_LOAD_RESULT_IGNORED;
        }
        bool ok = _loadMap(pMapId, iBasePath, x, y);
        VMAPLoadResult r = ok ? VMAP_LOAD_RESULT_OK : VMAP_LOAD_RESULT_ERROR;
        return r;
    }

    void VMapManager2::unloadMap(unsigned int pMapId, int x, int y)
    {
        // No verbose logging; just unload the requested tile if present
        auto it = iInstanceMapTrees.find(pMapId);
        if (it != iInstanceMapTrees.end())
        {
            it->second->UnloadMapTile(static_cast<uint32_t>(x), static_cast<uint32_t>(y), this);
        }
    }

    void VMapManager2::unloadMap(unsigned int pMapId)
    {
        // No verbose logging; unload entire map and remove from caches
        auto it = iInstanceMapTrees.find(pMapId);
        if (it != iInstanceMapTrees.end())
        {
            it->second->UnloadMap(this);
            delete it->second;
            iInstanceMapTrees.erase(it);
            iLoadedMaps.erase(pMapId);
        }
    }

    bool VMapManager2::_loadMap(uint32_t pMapId, const std::string& basePath, uint32_t tileX, uint32_t tileY)
    {
        // Suppress verbose ENTER/EXIT logging for _loadMap
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            std::string mapFileName = getMapFileName(pMapId);
            std::string fullPath = basePath + mapFileName;
            if (!std::filesystem::exists(fullPath))
            {
                return false;
            }
            StaticMapTree* newTree = new StaticMapTree(pMapId, basePath);
            if (!newTree->InitMap(mapFileName, this))
            {
                delete newTree;
                return false;
            }
            iInstanceMapTrees[pMapId] = newTree;
            instanceTree = iInstanceMapTrees.find(pMapId);
        }
        bool tileOk = instanceTree->second->LoadMapTile(tileX, tileY, this);
        return tileOk;
    }

    bool VMapManager2::isInLineOfSight(unsigned int pMapId, float x1, float y1, float z1,
        float x2, float y2, float z2, bool ignoreM2Model)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::isInLineOfSight map=" << pMapId);
        if (!isLineOfSightCalcEnabled())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isInLineOfSight -> 1 (disabled)");
            return true;
        }
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos1 = convertPositionToInternalRep(x1, y1, z1);
            G3D::Vector3 pos2 = convertPositionToInternalRep(x2, y2, z2);
            bool r = instanceTree->second->isInLineOfSight(pos1, pos2, ignoreM2Model);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isInLineOfSight -> " << (r ? 1 : 0));
            return r;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isInLineOfSight -> 1 (no tree)");
        return true;
    }

    ModelInstance* VMapManager2::FindCollisionModel(unsigned int mapId, float x0, float y0, float z0,
        float x1, float y1, float z1)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::FindCollisionModel map=" << mapId);
        auto instanceTree = iInstanceMapTrees.find(mapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos1 = convertPositionToInternalRep(x0, y0, z0);
            G3D::Vector3 pos2 = convertPositionToInternalRep(x1, y1, z1);
            ModelInstance* m = instanceTree->second->FindCollisionModel(pos1, pos2);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCollisionModel -> " << (m ? "hit" : "null"));
            return m;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCollisionModel -> null (no tree)");
        return nullptr;
    }

    bool VMapManager2::getObjectHitPos(unsigned int pMapId, float x1, float y1, float z1,
        float x2, float y2, float z2,
        float& rx, float& ry, float& rz, float pModifyDist)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getObjectHitPos map=" << pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos1 = convertPositionToInternalRep(x1, y1, z1);
            G3D::Vector3 pos2 = convertPositionToInternalRep(x2, y2, z2);
            G3D::Vector3 resultPos;
            bool hit = instanceTree->second->getObjectHitPos(pos1, pos2, resultPos, pModifyDist);
            if (hit)
            {
                G3D::Vector3 world = NavCoord::InternalToWorld(resultPos);
                rx = world.x; ry = world.y; rz = world.z;
                PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getObjectHitPos -> 1");
                return true;
            }
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getObjectHitPos -> 0");
        return false;
    }

    float VMapManager2::getHeight(unsigned int pMapId, float x, float y, float z, float maxSearchDist)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getHeight map=" << pMapId);
        // Coordinate space notes for this query chain:
        // 1) (x,y,z) provided by caller are assumed WORLD space. In this project WORLD and INTERNAL share Z; only X/Y are mirrored.
        // 2) convertPositionToInternalRep -> NavCoord::WorldToInternal: internalPos = (MID - x, MID - y, z). This mirrors X/Y about global MapMid, Z unchanged.
        // 3) StaticMapTree::getHeight expects INTERNAL space position; it performs a downward ray entirely in INTERNAL space.
        // 4) During ray traversal each ModelInstance converts INTERNAL -> MODEL-LOCAL (inverse rotation & scale) for triangle tests.
        // 5) Intersection distance returned is converted back to INTERNAL distance (scale reapplied) and height computed as internalPos.z - hitDistance.
        // 6) Because Z is invariant between WORLD and INTERNAL, returning internal height directly yields correct WORLD Z.
        if (!isHeightCalcEnabled())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getHeight -> INVALID (disabled)");
            return PhysicsConstants::INVALID_HEIGHT;
        }
        float h = PhysicsConstants::INVALID_HEIGHT;
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = convertPositionToInternalRep(x, y, z); // WORLD -> INTERNAL (X/Y mirrored, Z preserved)
            // Modern raycast: only accept closest walkable hit (performed in INTERNAL space)
            h = instanceTree->second->getHeight(pos, maxSearchDist); // returns INTERNAL Z (== WORLD Z)
            if (!std::isfinite(h)) h = PhysicsConstants::INVALID_HEIGHT;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getHeight -> " << h);
        return h;
    }

    bool VMapManager2::getAreaInfo(unsigned int pMapId, float x, float y, float& z,
        uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getAreaInfo map=" << pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = NavCoord::WorldToInternal(x, y, z);
            bool res = instanceTree->second->getAreaInfo(pos, flags, adtId, rootId, groupId);
            if (res)
            {
                z = pos.z;
                PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getAreaInfo -> 1");
                return true;
            }
        }
        flags = 0; adtId = -1; rootId = -1; groupId = -1;
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getAreaInfo -> 0");
        return false;
    }

    bool VMapManager2::GetLiquidLevel(uint32_t pMapId, float x, float y, float z,
        uint8_t ReqLiquidTypeMask, float& level, float& floor, uint32_t& type) const
    {
        // PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::GetLiquidLevel map=" << pMapId
        //     << " worldPos=(" << x << "," << y << "," << z << ") reqMask=0x" << (unsigned)ReqLiquidTypeMask);

        // Ensure map has been initialized similar to getHeight path
        if (!isMapInitialized(pMapId))
        {
            PHYS_TRACE(PHYS_PERF, "[Liquid] Map not initialized; initializing map=" << pMapId);
            const_cast<VMapManager2*>(this)->initializeMap(pMapId);
        }

        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end() && instanceTree->second)
        {
            // Use the same conversion helper as getHeight to keep coordinate spaces consistent
            G3D::Vector3 pos = convertPositionToInternalRep(x, y, z);
            // PHYS_TRACE(PHYS_PERF, "[Liquid] InternalPos=(" << pos.x << "," << pos.y << "," << pos.z << ")");

            LocationInfo info; bool gotLoc = instanceTree->second->GetLocationInfo(pos, info);
            // PHYS_TRACE(PHYS_PERF, "[Liquid] GetLocationInfo gotLoc=" << (gotLoc?1:0) << " hitModel=" << (info.hitModel?1:0) << " groundZ=" << info.ground_Z);
            if (gotLoc && info.hitModel)
            {
                if (info.hitInstance)
                {
                    float liqH; bool liqOk = info.hitInstance->GetLiquidLevel(pos, const_cast<LocationInfo&>(info), liqH);
                    // PHYS_TRACE(PHYS_PERF, "[Liquid] hitInstance=1 liqOk=" << (liqOk?1:0));
                    if (liqOk)
                    {
                        uint32_t liqType = info.hitModel->GetLiquidType();
                        // New: support both entry-id and index representations
                        uint32_t liqMask = GetLiquidMaskUnified(liqType);
                        const char* liqName = GetLiquidNameUnified(liqType);
                        // PHYS_TRACE(PHYS_PERF, "[Liquid] liqH=" << liqH << " liqType=" << liqType << " (" << liqName << ") liqMask=0x" << std::hex << liqMask << std::dec << " reqMask=0x" << (unsigned)ReqLiquidTypeMask);
                        if ((liqMask & ReqLiquidTypeMask) != 0)
                        {
                            level = liqH;
                            floor = info.ground_Z;
                            type = liqType;
                            return true;
                        }
                    }
                }
            }
        }

        // PHYS_INFO(PHYS_PERF, "EXIT VMapManager2::GetLiquidLevel -> 0 (no match)");
        return false;
    }

    std::shared_ptr<WorldModel> VMapManager2::acquireModelInstance(const std::string& basepath, const std::string& filename)
    {
        try
        {
            std::lock_guard<std::shared_mutex> lock(m_modelsLock);
            auto it = iLoadedModelFiles.find(filename);
            if (it != iLoadedModelFiles.end())
            {
                return it->second;
            }
            if (!modelMappingLoaded)
                BuildCompleteModelMapping(basepath);
            std::string fullPath = ResolveModelPath(basepath, filename);
            if (fullPath.empty() || !std::filesystem::exists(fullPath))
            {
                return nullptr;
            }
            std::shared_ptr<WorldModel> wm = std::make_shared<WorldModel>();
            if (!wm->readFile(fullPath))
            {
                return nullptr;
            }
            iLoadedModelFiles[filename] = wm;
            return wm;
        }
        catch (const std::exception& e)
        {
            std::cerr << "Exception in acquireModelInstance: " << e.what();
            return nullptr;
        }
    }

} // namespace VMAP