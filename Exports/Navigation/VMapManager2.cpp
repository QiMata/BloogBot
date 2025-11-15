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

namespace VMAP
{
    // Convenience downward sweep for walkable surfaces around current height
    std::vector<CylinderSweepHit> VMapManager2::SweepForWalkableSurfaces(unsigned int pMapId,
        const Cylinder& baseCylinder, float currentHeight, float maxStepUp, float maxStepDown) const
    {
        // Start slightly above potential step-up so we can capture both step-up and step-down surfaces
        float startOffset = std::max(0.1f, maxStepUp);
        float sweepDist = std::max(0.25f, maxStepUp + maxStepDown);
        Cylinder sweepCyl(G3D::Vector3(baseCylinder.base.x, baseCylinder.base.y, currentHeight + startOffset),
            baseCylinder.axis, baseCylinder.radius, baseCylinder.height);
        return SweepCylinder(pMapId, sweepCyl, G3D::Vector3(0,0,-1), sweepDist);
    }

    // Get height using cylinder for more accurate ground detection
    float VMapManager2::GetCylinderHeight(unsigned int pMapId, float x, float y, float z,
        float cylinderRadius, float cylinderHeight, float maxSearchDist)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::GetCylinderHeight map=" << pMapId << " pos=("<<x<<","<<y<<","<<z<<") r="<<cylinderRadius<<" h="<<cylinderHeight<<" dist="<<maxSearchDist);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetCylinderHeight -> INVALID (no tree)");
            return PhysicsConstants::INVALID_HEIGHT;
        }

        // Build a cylinder centered around the query position and sweep downward
        Cylinder worldCyl(G3D::Vector3(x, y, z + maxSearchDist * 0.5f),
            G3D::Vector3(0, 0, 1), cylinderRadius, cylinderHeight);

        std::vector<CylinderSweepHit> hits = SweepCylinder(
            pMapId, worldCyl, G3D::Vector3(0, 0, -1), maxSearchDist);

        // Log sweep results for diagnostics
        if (!hits.empty())
        {
            PHYS_TRACE(PHYS_SURF, "[CylHeight] sweep hits=" << hits.size()
                << " pos=(" << x << "," << y << "," << z << ") r=" << cylinderRadius
                << " h=" << cylinderHeight << " dist=" << maxSearchDist);
            size_t toLog = std::min<size_t>(hits.size(), 8);
            for (size_t i = 0; i < toLog; ++i)
            {
                const auto& h = hits[i];
                PHYS_TRACE(PHYS_SURF, "  hit[" << i << "] tri=" << h.triangleIndex
                    << " toi=" << h.q.distance
                    << " h=" << h.height
                    << " nZ=" << h.normal.z
                    << " walkable=" << (h.walkable ? 1 : 0)
                    << " pos=(" << h.position.x << "," << h.position.y << "," << h.position.z << ")");
            }
        }

        if (!hits.empty())
        {
            for (const auto& h : hits)
            {
                if (h.walkable)
                {
                    PHYS_TRACE(PHYS_SURF, "[CylHeight] selected tri=" << h.triangleIndex
                        << " h=" << h.height << " nZ=" << h.normal.z
                        << " toi=" << h.q.distance);
                    PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetCylinderHeight -> " << h.height);
                    return h.height;
                }
            }
            PHYS_TRACE(PHYS_SURF, "[CylHeight] no walkable surface among hits, falling back");
        }

        // Fallback to regular height check
        float fallback = getHeight(pMapId, x, y, z, maxSearchDist);
        PHYS_TRACE(PHYS_SURF, "[CylHeight] fallback height=" << fallback);
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetCylinderHeight -> " << fallback);
        return fallback;
    }

    void VMapManager2::GetCylinderCollisionCandidates(unsigned int pMapId, const Cylinder& worldCylinder,
        std::vector<ModelInstance*>& outInstances) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::GetCylinderCollisionCandidates map="<<pMapId
            <<" base=("<<worldCylinder.base.x<<","<<worldCylinder.base.y<<","<<worldCylinder.base.z<<") r="<<worldCylinder.radius<<" h="<<worldCylinder.height);
        outInstances.clear();
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetCylinderCollisionCandidates (no tree)");
            return;
        }

        Cylinder internalCyl = ConvertCylinderToInternal(worldCylinder);
        instanceTree->second->GetCylinderCollisionCandidates(internalCyl, outInstances);
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetCylinderCollisionCandidates count="<<outInstances.size());
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

    // Cylinder collision implementations
    Cylinder VMapManager2::ConvertCylinderToInternal(const Cylinder& worldCylinder) const
    {
        // Removed verbose PERF logs
        G3D::Vector3 internalBase = convertPositionToInternalRep(
            worldCylinder.base.x, worldCylinder.base.y, worldCylinder.base.z);

        // Axis doesn't need position conversion, just keep direction
        return Cylinder(internalBase, worldCylinder.axis, worldCylinder.radius, worldCylinder.height);
    }

    Cylinder VMapManager2::ConvertCylinderToWorld(const Cylinder& internalCylinder) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::ConvertCylinderToWorld baseI=("<<internalCylinder.base.x<<","<<internalCylinder.base.y<<","<<internalCylinder.base.z<<")");
        // Convert back from internal representation to world coordinates
        G3D::Vector3 worldBase = NavCoord::InternalToWorld(internalCylinder.base);

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::ConvertCylinderToWorld baseW=("<<worldBase.x<<","<<worldBase.y<<","<<worldBase.z<<")");
        return Cylinder(worldBase, internalCylinder.axis,
            internalCylinder.radius, internalCylinder.height);
    }

    CylinderIntersection VMapManager2::IntersectCylinder(unsigned int pMapId,
        const Cylinder& worldCylinder) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::IntersectCylinder map="<<pMapId);
        CylinderIntersection result;

        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            Cylinder internalCyl = ConvertCylinderToInternal(worldCylinder);
            result = instanceTree->second->IntersectCylinder(internalCyl);

            // Convert contact point back to world coordinates if hit
            if (result.hit)
            {
                result.contactPoint = NavCoord::InternalToWorld(result.contactPoint);
            }
        }

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::IntersectCylinder hit="<<(result.hit?1:0)<<" h="<<result.contactHeight);
        return result;
    }

    std::vector<CylinderSweepHit> VMapManager2::SweepCylinder(unsigned int pMapId,
        const Cylinder& worldCylinder, const G3D::Vector3& sweepDir, float sweepDistance) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::SweepCylinder map="<<pMapId<<" dist="<<sweepDistance);
        std::vector<CylinderSweepHit> hits;

        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            Cylinder internalCyl = ConvertCylinderToInternal(worldCylinder);

            // Note: sweep direction doesn't need position conversion, just invert X and Y
            G3D::Vector3 internalSweepDir = NavCoord::WorldDirToInternal(sweepDir);

            hits = instanceTree->second->SweepCylinder(internalCyl, internalSweepDir, sweepDistance);

            // Convert all hit data coherently to world space (Fix 1)
            for (auto& h : hits)
            {
                // Preserve internal diagnostics
                float internalHeight = h.height;
                G3D::Vector3 internalNormal = h.normal;

                // Position conversion first
                h.position = NavCoord::InternalToWorld(h.position);

                // Height: use converted position.z for world height
                h.height = h.position.z;

                // Normal: convert orientation (invert X/Y as needed by InternalDirToWorld)
                h.normal = NavCoord::InternalDirToWorld(internalNormal);

                // Re-evaluate walkable on world-space normal
                h.walkable = VMAP::CylinderHelpers::IsWalkableSurface(h.normal);

                PHYS_TRACE(PHYS_CYL, "[SweepConv] tri="<<h.triangleIndex<<" inst="<<h.q.instanceId
                    <<" toi="<<h.q.distance<<" hInt="<<internalHeight<<" hW="<<h.height
                    <<" nZInt="<<internalNormal.z<<" nZW="<<h.normal.z<<" walk="<<(h.walkable?1:0));
            }
        }

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::SweepCylinder hits="<<hits.size());
        return hits;
    }

    bool VMapManager2::CheckCylinderCollision(unsigned int pMapId, const Cylinder& worldCylinder,
        float& outContactHeight, G3D::Vector3& outContactNormal, ModelInstance** outHitInstance) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::CheckCylinderCollision map="<<pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            Cylinder internalCyl = ConvertCylinderToInternal(worldCylinder);

            bool hit = instanceTree->second->CheckCylinderCollision(
                internalCyl, outContactHeight, outContactNormal, outHitInstance);

            if (hit)
            {
                // Normal direction needs to be inverted for X and Y
                outContactNormal = NavCoord::InternalDirToWorld(outContactNormal);
            }

            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CheckCylinderCollision -> "<<(hit?1:0));
            return hit;
        }

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CheckCylinderCollision -> 0 (no tree)");
        return false;
    }

    static inline const char* ClassifyRegion(float rel, float headStart)
    {
        if (rel >= headStart) return "head";
        if (rel <= 0.25f) return "feet";
        return "body";
    }

    static inline const char* RejectReason(float rel, float headStart, float nZ)
    {
        // Mirror acceptance rules in both Fit and Move checks
        const bool feetBand = (rel >= -0.05f && rel <= 0.25f);
        const bool belowHead = (rel < headStart);
        if (!belowHead && nZ >= 0.0f)
            return "ceiling/head intrusion";
        if (feetBand && nZ < 0.55f)
            return "feet support band but slope too steep (nZ<0.55)";
        if (belowHead && nZ < 0.70f)
            return "side penetration or steep face (nZ<0.70)";
        if (nZ < 0.0f)
            return "underside/negative normal";
        return "blocked (unspecified condition)";
    }

    bool VMapManager2::CanCylinderFitAtPosition(unsigned int pMapId,
        const Cylinder& worldCylinder, float tolerance) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::CanCylinderFitAtPosition map="<<pMapId<<" tol="<<tolerance);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderFitAtPosition -> 1 (no tree)");
            return true; // no map collision system
        }

        // Movement check is looser: accept floor contact, only reject if a surface intrudes in upper body (ceiling) or side penetration.
        Cylinder internal = ConvertCylinderToInternal(worldCylinder);
        // Slight radius expansion for conservative side test
        Cylinder expanded(internal.base, internal.axis, internal.radius + tolerance, internal.height);
        CylinderIntersection inter = instanceTree->second->IntersectCylinder(expanded);
        if (!inter.hit)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderFitAtPosition -> 1 (free)");
            return true; // free space
        }

        float rel = inter.contactHeight - expanded.base.z;
        const float HEAD_REGION_START = expanded.height * 0.7f; // upper 30% is head/shoulder region
        // If contact is within a small band near the feet treat as acceptable support
        if (rel >= -0.05f && rel <= 0.25f && inter.contactNormal.z >= 0.55f)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderFitAtPosition -> 1 (feet support)");
            return true;
        }
        // If contact normal is mostly vertical and below head region treat as support (e.g., slope)
        if (rel < HEAD_REGION_START && inter.contactNormal.z >= 0.70f)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderFitAtPosition -> 1 (slope support)");
            return true;
        }

        // Otherwise treat as blocking (wall or low ceiling) - emit diagnostics about nearby instances
        LOG_INFO("[VMAP][FitReject] map=" << pMapId
            << " base=(" << worldCylinder.base.x << "," << worldCylinder.base.y << "," << worldCylinder.base.z << ")"
            << " h=" << worldCylinder.height << " r=" << worldCylinder.radius
            << " tol=" << tolerance << " expR=" << (worldCylinder.radius + tolerance)
            << " rel=" << rel << " nZ=" << inter.contactNormal.z
            << " pen=" << inter.penetrationDepth << " tri=" << inter.triIndex
            << " region=" << ClassifyRegion(rel, HEAD_REGION_START)
            << " reason=" << RejectReason(rel, HEAD_REGION_START, inter.contactNormal.z)
            << " cosMin=" << VMAP::CylinderHelpers::GetWalkableCosMin());

        // Log exact blocking instance if available
        float ch = 0.0f; G3D::Vector3 n(0,0,1); ModelInstance* hitInst = nullptr;
        if (instanceTree->second->CheckCylinderCollision(expanded, ch, n, &hitInst) && hitInst)
        {
            std::string resolvedPath = ResolveModelPath(iBasePath, hitInst->name);
            LOG_INFO("    blocking name='" << hitInst->name << "' id=" << hitInst->ID << " adt=" << hitInst->adtId
                << " contactH=" << ch << " nZ=" << n.z);
            if (!resolvedPath.empty())
                LOG_INFO("    file='" << resolvedPath << "'");
        }

        std::vector<ModelInstance*> nearby;
        instanceTree->second->GetCylinderCollisionCandidates(expanded, nearby);
        size_t cap = std::min<size_t>(nearby.size(), 6);
        for (size_t i = 0; i < cap; ++i)
        {
            const ModelInstance* mi = nearby[i];
            const auto& b = mi->getBounds();
            auto lo = b.low(); auto hi = b.high();
            LOG_INFO("    inst[" << i << "] name='" << mi->name << "' id=" << mi->ID << " adt=" << mi->adtId
                << " boundsLo=(" << lo.x << "," << lo.y << "," << lo.z << ")"
                << " hi=(" << hi.x << "," << hi.y << "," << hi.z << ")");
        }

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderFitAtPosition -> 0");
        return false;
    }

    bool VMapManager2::FindCylinderWalkableSurface(unsigned int pMapId, const Cylinder& worldCylinder,
        float currentHeight, float maxStepUp, float maxStepDown,
        float& outHeight, G3D::Vector3& outNormal) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::FindCylinderWalkableSurface map="<<pMapId
            <<" curZ="<<currentHeight<<" up="<<maxStepUp<<" down="<<maxStepDown);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 0 (no tree)");
            return false;
        }

        // Search window
        float xW = worldCylinder.base.x;
        float yW = worldCylinder.base.y;
        float zCastStartW = currentHeight + std::max(0.1f, maxStepUp);
        float searchDist = std::max(0.25f, maxStepUp + maxStepDown);

        // 1) Prefer a coherent plane from swept-cylinder hits under the capsule at this XY
        const float cosMin = VMAP::CylinderHelpers::GetWalkableCosMin();
        const float bandEps = std::max(0.05f, std::min(0.35f, worldCylinder.radius * 0.5f));

        Cylinder sweepCyl(G3D::Vector3(xW, yW, zCastStartW), G3D::Vector3(0,0,1), worldCylinder.radius, worldCylinder.height);
        auto hits = SweepCylinder(pMapId, sweepCyl, G3D::Vector3(0,0,-1), searchDist);

        // Collect basic stats only; suppress per-hit and detailed logs
        int rejNotWalk = 0, rejSteep = 0, rejRange = 0, acc = 0;
        struct GroupAgg { float maxH = -std::numeric_limits<float>::infinity(); G3D::Vector3 nSum = {0,0,0}; int count = 0; };
        std::unordered_map<uint64_t, GroupAgg> groups;
        auto makeKey = [&](uint32_t instId, float h) -> uint64_t {
            int band = (int)std::floor(h / bandEps + 0.5f);
            uint64_t k = ((uint64_t)instId << 32) | (uint32_t)(band & 0x7fffffff);
            return k;
        };

        for (const auto& h : hits)
        {
            if (!h.walkable) { ++rejNotWalk; continue; }
            if (h.normal.z < cosMin) { ++rejSteep; continue; }
            float d = h.height - currentHeight;
            if (d > maxStepUp + 1e-3f || d < -maxStepDown - 1e-3f) { ++rejRange; continue; }
            uint32_t instId = h.q.instanceId;
            uint64_t key = makeKey(instId, h.height);
            auto& g = groups[key];
            g.maxH = std::max(g.maxH, h.height);
            g.nSum = G3D::Vector3(g.nSum.x + h.normal.x, g.nSum.y + h.normal.y, g.nSum.z + h.normal.z);
            g.count++;
            ++acc;
        }

        float bestH = -std::numeric_limits<float>::infinity();
        G3D::Vector3 bestN(0,0,1);
        bool haveGroup = false;
        for (auto& kv : groups)
        {
            const GroupAgg& g = kv.second;
            if (g.count <= 0) continue;
            if (g.maxH > bestH)
            {
                bestH = g.maxH;
                G3D::Vector3 n = g.nSum;
                float len = n.magnitude();
                if (len > 1e-6f) n = n / len; else n = G3D::Vector3(0,0,1);
                if (n.z < 0.0f) n = -n; // upward hemisphere
                bestN = n;
                haveGroup = true;
            }
        }

        // Single summary log for the sweep phase (no spam)
        PHYS_TRACE(PHYS_SURF, "[FindSurf] hits=" << hits.size()
            << " acc=" << acc
            << " rejNW=" << rejNotWalk
            << " rejSteep=" << rejSteep
            << " rejRange=" << rejRange
            << " groups=" << groups.size()
            << " x=" << xW << " y=" << yW
            << " zStart=" << zCastStartW
            << " dist=" << searchDist
            << " bandEps=" << bandEps
            << " cosMin=" << cosMin);

        if (haveGroup)
        {
            outHeight = bestH;
            outNormal = bestN;
            PHYS_TRACE(PHYS_SURF, "[FindSurf][Summary] method=Sweep topH=" << outHeight << " nZ=" << outNormal.z);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 1 h="<<outHeight);
            return true;
        }

        // 2) Fallback: use downward ray height for Z, then try to derive normal from nearby sweep hits
        // Build internal-space cast origin at (x,y,zCastStart)
        G3D::Vector3 castStartI = convertPositionToInternalRep(xW, yW, zCastStartW);
        float hI = instanceTree->second->getHeight(castStartI, searchDist);
        if (!std::isfinite(hI))
        {
            PHYS_TRACE(PHYS_SURF, "[FindSurf][Summary] method=None (no height) hits=" << hits.size()
                << " acc=" << acc << " groups=" << groups.size());
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 0 (no height)");
            return false;
        }

        G3D::Vector3 hw = NavCoord::InternalToWorld(G3D::Vector3(castStartI.x, castStartI.y, hI));
        outHeight = hw.z;

        float diff = outHeight - currentHeight;
        if (diff > maxStepUp + 1e-3f || diff < -maxStepDown - 1e-3f)
        {
            PHYS_TRACE(PHYS_SURF, "[FindSurf][Summary] method=Ray out-of-range diff=" << diff
                << " hits=" << hits.size() << " acc=" << acc);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 0 (range)");
            return false;
        }

        // Derive normal from sweep hits closest to the chosen height band, avoid cross-model ray mixing
        if (!hits.empty())
        {
            float bestAbs = std::numeric_limits<float>::max();
            G3D::Vector3 nPick(0,0,1);
            for (const auto& h : hits)
            {
                if (!h.walkable || h.normal.z < cosMin) continue;
                float a = std::abs(h.height - outHeight);
                if (a <= bandEps && a < bestAbs)
                {
                    bestAbs = a; nPick = h.normal;
                }
            }
            if (bestAbs < std::numeric_limits<float>::max())
            {
                if (nPick.z < 0.0f) nPick = -nPick;
                outNormal = nPick;
                PHYS_TRACE(PHYS_SURF, "[FindSurf][Summary] method=Ray+SweepN h=" << outHeight << " nZ=" << outNormal.z << " |dh|=" << bestAbs);
                PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 1 h="<<outHeight);
                return true;
            }
        }

        // Last resort: default up normal if nothing else; avoid mixing offset rays
        // Provide diagnostics explaining why we had to fall back
        std::string fallbackReason;
        if (hits.empty())
            fallbackReason = "no sweep hits";
        else if (acc == 0)
            fallbackReason = "no walkable sweep hits in step window";
        else
            fallbackReason = "unable to derive normal: no walkable hit within bandEps (" + std::to_string(bandEps) + ") near height or nZ<cosMin (" + std::to_string(cosMin) + ")";
        outNormal = G3D::Vector3(0,0,1);
        PHYS_TRACE(PHYS_SURF, "[FindSurf][Summary] method=FallbackUp h=" << outHeight << " nZ=" << outNormal.z << " reason=" << fallbackReason);
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCylinderWalkableSurface -> 1 h="<<outHeight);
        return true;
    }

    bool VMapManager2::CanCylinderMoveAtPosition(unsigned int pMapId, const Cylinder& worldCylinder, float tolerance) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::CanCylinderMoveAtPosition map="<<pMapId<<" tol="<<tolerance);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderMoveAtPosition -> 1 (no tree)");
            return true; // no map collision system
        }

        // Movement check is looser: accept floor contact, only reject if a surface intrudes in upper body (ceiling) or side penetration.
        Cylinder internal = ConvertCylinderToInternal(worldCylinder);
        // Slight radius expansion for conservative side test
        Cylinder expanded(internal.base, internal.axis, internal.radius + tolerance, internal.height);
        CylinderIntersection inter = instanceTree->second->IntersectCylinder(expanded);
        if (!inter.hit)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderMoveAtPosition -> 1 (free)");
            return true; // free space
        }

        float rel = inter.contactHeight - expanded.base.z;
        const float HEAD_REGION_START = expanded.height * 0.7f; // upper 30% is head/shoulder region
        // If contact is within a small band near the feet treat as acceptable support
        if (rel >= -0.05f && rel <= 0.25f && inter.contactNormal.z >= 0.55f)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderMoveAtPosition -> 1 (feet support)");
            return true;
        }
        // If contact normal is mostly vertical and below head region treat as support (e.g., slope)
        if (rel < HEAD_REGION_START && inter.contactNormal.z >= 0.70f)
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderMoveAtPosition -> 1 (slope support)");
            return true;
        }
        // Otherwise treat as blocking (wall or low ceiling)

        LOG_DEBUG("[VMAP][MoveReject] map=" << pMapId
            << " base=(" << worldCylinder.base.x << "," << worldCylinder.base.y << "," << worldCylinder.base.z << ")"
            << " h=" << worldCylinder.height << " r=" << worldCylinder.radius
            << " tol=" << tolerance << " expR=" << (worldCylinder.radius + tolerance)
            << " rel=" << rel << " nZ=" << inter.contactNormal.z
            << " pen=" << inter.penetrationDepth << " tri=" << inter.triIndex
            << " region=" << ClassifyRegion(rel, HEAD_REGION_START)
            << " reason=" << RejectReason(rel, HEAD_REGION_START, inter.contactNormal.z)
            << " cosMin=" << VMAP::CylinderHelpers::GetWalkableCosMin());

        // Log exact blocking instance if available (debug-level to avoid spam)
        float ch = 0.0f; G3D::Vector3 n(0,0,1); ModelInstance* hitInst = nullptr;
        if (instanceTree->second->CheckCylinderCollision(expanded, ch, n, &hitInst) && hitInst)
        {
            LOG_DEBUG("    name='" << hitInst->name << "' id=" << hitInst->ID
                << " adt=" << hitInst->adtId << " contactH=" << ch << " nZ=" << n.z);
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::CanCylinderMoveAtPosition -> 0");
        return false;
    }

    bool VMapManager2::DumpSurfacePatch(unsigned int pMapId, float x, float y, float z,
        float patchHalfXY, float patchHalfZ, int maxTrianglesToLog) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::DumpSurfacePatch map="<<pMapId<<" pos=("<<x<<","<<y<<","<<z<<")");
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree == iInstanceMapTrees.end()) return false;

        // Build internal-space AABB around the patch
        G3D::Vector3 loW(x - patchHalfXY, y - patchHalfXY, z - patchHalfZ);
        G3D::Vector3 hiW(x + patchHalfXY, y + patchHalfXY, z + patchHalfZ);
        G3D::Vector3 loI = NavCoord::WorldToInternal(loW);
        G3D::Vector3 hiI = NavCoord::WorldToInternal(hiW);
        G3D::Vector3 qLo = loI.min(hiI);
        G3D::Vector3 qHi = loI.max(hiI);
        G3D::AABox queryBox(qLo, qHi);

        const BIH* tree = instanceTree->second->GetBIHTree();
        const ModelInstance* instances = instanceTree->second->GetInstancesPtr();
        uint32_t instCount = instanceTree->second->GetInstanceCount();
        if (!tree || !instances || instCount == 0) return false;

        // Gather candidate instances via BIH
        const uint32_t cap = std::min<uint32_t>(instCount, 8192);
        std::vector<uint32_t> instIdx(cap);
        uint32_t cnt = 0;
        if (!tree->QueryAABB(queryBox, instIdx.data(), cnt, cap) || cnt == 0) return false;

        struct SampleTri { G3D::Vector3 a,b,c,n; int inst; int local; };
        std::vector<SampleTri> tris;

        for (uint32_t k = 0; k < cnt; ++k)
        {
            uint32_t idx = instIdx[k];
            if (idx >= instCount) continue;
            const ModelInstance& inst = instances[idx];
            if (!inst.iModel) continue;
            if (!inst.iBound.intersects(queryBox)) continue;

            // Transform query box corners to model space and expand a bit
            G3D::Vector3 wLo = queryBox.low();
            G3D::Vector3 wHi = queryBox.high();
            G3D::Vector3 corners[8] = {
                {wLo.x, wLo.y, wLo.z}, {wHi.x, wLo.y, wLo.z}, {wLo.x, wHi.y, wLo.z}, {wHi.x, wHi.y, wLo.z},
                {wLo.x, wLo.y, wHi.z}, {wHi.x, wLo.y, wHi.z}, {wLo.x, wHi.y, wHi.z}, {wHi.x, wHi.y, wHi.z}
            };
            G3D::Vector3 c0 = inst.iInvRot * ((corners[0] - inst.iPos) * inst.iInvScale);
            G3D::AABox modelBox(c0, c0);
            for (int ci=1;ci<8;++ci)
                modelBox.merge(inst.iInvRot * ((corners[ci] - inst.iPos) * inst.iInvScale));
            G3D::Vector3 mInfl(0.03f,0.03f,0.03f);
            modelBox = G3D::AABox(modelBox.low()-mInfl, modelBox.high()+mInfl);

            std::vector<G3D::Vector3> vertices; std::vector<uint32_t> indices;
            bool have = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
            if (!have) { if (!inst.iModel->GetAllMeshData(vertices, indices)) continue; }
            size_t triCount = indices.size()/3;
            for (size_t t=0;t<triCount;++t)
            {
                uint32_t i0 = indices[t*3+0], i1 = indices[t*3+1], i2 = indices[t*3+2];
                if (i0>=vertices.size()||i1>=vertices.size()||i2>=vertices.size()) continue;
                const G3D::Vector3& a = vertices[i0];
                const G3D::Vector3& b = vertices[i1];
                const G3D::Vector3& c = vertices[i2];
                // Cull using modelBox when we fetched all
                if (!have)
                {
                    G3D::Vector3 lo = a.min(b).min(c), hi = a.max(b).max(c);
                    if (!G3D::AABox(lo,hi).intersects(modelBox)) continue;
                }
                // Transform to internal space
                G3D::Vector3 wa = (a*inst.iScale)*inst.iRot + inst.iPos;
                G3D::Vector3 wb = (b*inst.iScale)*inst.iRot + inst.iPos;
                G3D::Vector3 wc = (c*inst.iScale)*inst.iRot + inst.iPos;
                // Quick reject if triangle outside the patch box by a small epsilon
                G3D::AABox triBox(wa,wa); triBox.merge(wb); triBox.merge(wc);
                if (!triBox.intersects(queryBox)) continue;
                // Compute oriented normal (upward hemisphere)
                G3D::Vector3 n = VMAP::CylinderHelpers::CalculateTriangleNormalOriented(wa,wb,wc);
                tris.push_back({wa,wb,wc,n,(int)idx,(int)t});
                if ((int)tris.size() >= maxTrianglesToLog) break;
            }
            if ((int)tris.size() >= maxTrianglesToLog) break;
        }

        if (tris.empty()) { PHYS_TRACE(PHYS_SURF, "[SurfPatch] none"); PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::DumpSurfacePatch -> 0"); return false; }

        // Fit a plane z = ax + by + c with least squares over triangle vertices
        double Sxx=0,Sxy=0,Sxz=0,Sx=0,Syy=0,Syz=0,Sy=0,Sn=0;
        for (const auto& t : tris)
        {
            const G3D::Vector3 pts[3] = { t.a,t.b,t.c };
            for (int i=0;i<3;++i)
            {
                double X=pts[i].x, Y=pts[i].y, Z=pts[i].z;
                Sxx+=X*X; Sxy+=X*Y; Sxz+=X*Z; Sx+=X; Syy+=Y*Y; Syz+=Y*Z; Sy+=Y; Sn+=1.0;
            }
        }
        // Solve normal equations for a,b,c using Cramer's rule for small 3x3
        auto det3 = [](double a1,double a2,double a3,double b1,double b2,double b3,double c1,double c2,double c3){
            return a1*(b2*c3-b3*c2) - a2*(b1*c3-b3*c1) + a3*(b1*c2-b2*c1);
        };
        double A11=Sxx, A12=Sxy, A13=Sx;
        double A21=Sxy, A22=Syy, A23=Sy;
        double A31=Sx,  A32=Sy,  A33=Sn;
        double B1=Sxz, B2=Syz, B3=0; // sum Z is in RHS for c: ?Z, but we use 0 and incorporate below
        double Sz=0; for (const auto& t : tris){ Sz += t.a.z + t.b.z + t.c.z; }
        B3 = Sz;
        double D = det3(A11,A12,A13,A21,A22,A23,A31,A32,A33);
        double Dx = det3(B1,A12,A13,B2,A22,A23,B3,A32,A33);
        double Dy = det3(A11,B1,A13,A21,B2,A23,A31,B3,A33);
        double Dz = det3(A11,A12,B1,A21,A22,B2,A31,A32,B3);
        double a = (std::abs(D) > 1e-12) ? Dx/D : 0.0;
        double b = (std::abs(D) > 1e-12) ? Dy/D : 0.0;
        double c = (std::abs(D) > 1e-12) ? Dz/D : z; // default near z if ill-conditioned
        // Plane normal from z = a x + b y + c is N = (-a, -b, 1), normalized
        G3D::Vector3 nFit(-float(a), -float(b), 1.0f);
        float nLen = nFit.magnitude(); if (nLen > 0.0001f) nFit = nFit / nLen; else nFit = G3D::Vector3(0,0,1);

        PHYS_TRACE(PHYS_SURF, "[SurfPatch] tris=" << tris.size() << " fitN.z=" << nFit.z << " boxXY=±" << patchHalfXY << " boxZ=±" << patchHalfZ);
        int limit = std::min<int>((int)tris.size(), maxTrianglesToLog);
        for (int i=0;i<limit;++i)
        {
            const auto& t = tris[i];
            auto w2 = [&](const G3D::Vector3& p){ G3D::Vector3 W = NavCoord::InternalToWorld(p); return std::to_string(W.x)+","+std::to_string(W.y)+","+std::to_string(W.z); };
            PHYS_TRACE(PHYS_SURF, "    tri i="<<i<<" inst="<<t.inst<<" local="<<t.local
                << " nZ="<<t.n.z
                << " aW=("<<w2(t.a)<<") bW=("<<w2(t.b)<<") cW=("<<w2(t.c)<<")");
        }

        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::DumpSurfacePatch -> 1");
        return true;
    }

    bool VMapManager2::isUnderModel(unsigned int pMapId, float x, float y, float z,
        float* outDist, float* inDist) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::isUnderModel map="<<pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = convertPositionToInternalRep(x, y, z);
            bool res = instanceTree->second->isUnderModel(pos, outDist, inDist);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isUnderModel -> "<<(res?1:0));
            return res;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isUnderModel -> 0 (no tree)");
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
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::isInLineOfSight map="<<pMapId);
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
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isInLineOfSight -> "<<(r?1:0));
            return r;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::isInLineOfSight -> 1 (no tree)");
        return true;
    }

    ModelInstance* VMapManager2::FindCollisionModel(unsigned int mapId, float x0, float y0, float z0,
        float x1, float y1, float z1)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::FindCollisionModel map="<<mapId);
        auto instanceTree = iInstanceMapTrees.find(mapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos1 = convertPositionToInternalRep(x0, y0, z0);
            G3D::Vector3 pos2 = convertPositionToInternalRep(x1, y1, z1);
            ModelInstance* m = instanceTree->second->FindCollisionModel(pos1, pos2);
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCollisionModel -> "<<(m?"hit":"null"));
            return m;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::FindCollisionModel -> null (no tree)");
        return nullptr;
    }

    bool VMapManager2::getObjectHitPos(unsigned int pMapId, float x1, float y1, float z1,
        float x2, float y2, float z2,
        float& rx, float& ry, float& rz, float pModifyDist)
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getObjectHitPos map="<<pMapId);
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
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getHeight map="<<pMapId);
        if (!isHeightCalcEnabled())
        {
            PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getHeight -> INVALID (disabled)");
            return PhysicsConstants::INVALID_HEIGHT;
        }
        float h = PhysicsConstants::INVALID_HEIGHT;
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = convertPositionToInternalRep(x, y, z);
            h = instanceTree->second->getHeight(pos, maxSearchDist);
            if (!std::isfinite(h)) h = PhysicsConstants::INVALID_HEIGHT;
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::getHeight -> "<<h);
        return h;
    }

    bool VMapManager2::getAreaInfo(unsigned int pMapId, float x, float y, float& z,
        uint32_t& flags, int32_t& adtId, int32_t& rootId, int32_t& groupId) const
    {
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::getAreaInfo map="<<pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = NavCoord::WorldToInternal(x,y,z);
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
        PHYS_TRACE(PHYS_PERF, "ENTER VMapManager2::GetLiquidLevel map="<<pMapId);
        auto instanceTree = iInstanceMapTrees.find(pMapId);
        if (instanceTree != iInstanceMapTrees.end())
        {
            G3D::Vector3 pos = NavCoord::WorldToInternal(x,y,z);
            LocationInfo info;
            if (instanceTree->second->GetLocationInfo(pos, info) && info.hitModel)
            {
                float liqH;
                if (info.hitInstance && info.hitInstance->GetLiquidLevel(pos, const_cast<LocationInfo&>(info), liqH))
                {
                    uint32_t liqType = info.hitModel->GetLiquidType();
                    uint32_t liqMask = GetLiquidMask(liqType);
                    if (ReqLiquidTypeMask & liqMask)
                    {
                        level = liqH; floor = info.ground_Z; type = liqType;
                        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetLiquidLevel -> 1");
                        return true;
                    }
                }
            }
        }
        PHYS_TRACE(PHYS_PERF, "EXIT VMapManager2::GetLiquidLevel -> 0");
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