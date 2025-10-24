// StaticMapTree.cpp - Enhanced with cylinder collision support
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "VMapManager2.h"
#include "VMapDefinitions.h"
#include <fstream>
#include <iostream>
#include <algorithm>
#include <cstring>
#include <filesystem>
#include "VMapLog.h"
#include <vector>
#include "CapsuleCollision.h"
#include "CylinderCollision.h" // for CylinderHelpers walkable config
#include "CoordinateTransforms.h"

namespace VMAP
{
    class MapRayCallback
    {
    public:
        MapRayCallback(ModelInstance* val) : prims(val), hit(false) {}
        bool operator()(G3D::Ray const& ray, uint32_t entry, float& distance, bool pStopAtFirstHit = true, bool ignoreM2Model = false)
        {
            // Guard invalid indices and unloaded models
            if (!prims)
                return false;
            // We cannot know array length here, the caller ensures valid mapping via BIH, but also double-check model exists
            if (!prims[entry].iModel)
                return false;

            bool result = prims[entry].intersectRay(ray, distance, pStopAtFirstHit, ignoreM2Model);
            if (result)
            {
                hit = true;
                // Log the specific model hit and the hit position
                const ModelInstance& mi = prims[entry];
                G3D::Vector3 hitI = ray.origin() + ray.direction() * distance;           // internal/map coords
                G3D::Vector3 hitW = NavCoord::InternalToWorld(hitI);                      // convert to world for readability
                G3D::Vector3 instPosW = NavCoord::InternalToWorld(mi.iPos);
                const G3D::Vector3& rotDeg = mi.ModelSpawn::iRot;                         // Euler degrees from spawn
                PHYS_TRACE(PHYS_CYL, "Raycast hit model='" << mi.name << "' id=" << mi.ID
                    << " adt=" << mi.adtId
                    << " dist=" << distance
                    << " hitW=(" << hitW.x << "," << hitW.y << "," << hitW.z << ")"
                    << " instPosW=(" << instPosW.x << "," << instPosW.y << "," << instPosW.z << ")"
                    << " rotEulerDeg=(" << rotDeg.x << "," << rotDeg.y << "," << rotDeg.z << ")"
                    << " scale=" << mi.iScale);
            }
            return result;
        }
        bool didHit() const
        {
            return hit;
        }
    protected:
        ModelInstance* prims;
        bool hit;
        bool los;
    };

    // Lightweight static mesh view that exposes triangles overlapping a world-space AABB
    // using the map BIH for broad-phase and per-model mid-phase bounds queries.
    class StaticMeshView : public CapsuleCollision::TriangleMeshView
    {
    public:
        StaticMeshView(const BIH* tree, const ModelInstance* instances, uint32_t instanceCount)
            : m_tree(tree), m_instances(instances), m_instanceCount(instanceCount)
        {
            m_cache.reserve(1024);
        }

        void query(const CapsuleCollision::AABB& box, int* outIndices, int& count, int maxCount) const override
        {
            count = 0;
            m_cache.clear();
            if (!m_tree || !m_instances || m_instanceCount == 0 || !outIndices || maxCount <= 0)
                return;

            // Build world-space query AABox from CapsuleCollision::AABB
            G3D::Vector3 qlo(box.min.x, box.min.y, box.min.z);
            G3D::Vector3 qhi(box.max.x, box.max.y, box.max.z);
            G3D::AABox queryBox(qlo, qhi);

            // Broad-phase: BIH AABB query to gather candidate instance indices
            const uint32_t cap = std::min<uint32_t>(m_instanceCount, 16384);
            std::vector<uint32_t> instIdx(cap);
            uint32_t instCount = 0;
            if (!m_tree->QueryAABB(queryBox, instIdx.data(), instCount, cap) || instCount == 0)
                return;

            // Visit each candidate instance
            for (uint32_t k = 0; k < instCount; ++k)
            {
                uint32_t idx = instIdx[k];
                if (idx >= m_instanceCount)
                    continue;
                const ModelInstance& inst = m_instances[idx];
                if (!inst.iModel)
                    continue;
                // Extra cull with instance bound
                if (!inst.iBound.intersects(queryBox))
                    continue;

                // Transform query box corners to model space using inverse transform
                // p_model = iInvRot * ((p_world - iPos) * iInvScale)
                G3D::Vector3 wLo = queryBox.low();
                G3D::Vector3 wHi = queryBox.high();
                G3D::Vector3 corners[8] = {
                    G3D::Vector3(wLo.x, wLo.y, wLo.z),
                    G3D::Vector3(wHi.x, wLo.y, wLo.z),
                    G3D::Vector3(wLo.x, wHi.y, wLo.z),
                    G3D::Vector3(wHi.x, wHi.y, wLo.z),
                    G3D::Vector3(wLo.x, wLo.y, wHi.z),
                    G3D::Vector3(wHi.x, wLo.y, wHi.z),
                    G3D::Vector3(wLo.x, wHi.y, wHi.z),
                    G3D::Vector3(wHi.x, wHi.y, wHi.z)
                };
                // Initialize model-space bounds with first corner
                G3D::Vector3 c0 = inst.iInvRot * ((corners[0] - inst.iPos) * inst.iInvScale);
                G3D::AABox modelBox(c0, c0);
                for (int ci = 1; ci < 8; ++ci)
                {
                    G3D::Vector3 pm = inst.iInvRot * ((corners[ci] - inst.iPos) * inst.iInvScale);
                    modelBox.merge(pm);
                }

                // Mid-phase: gather triangles from model within modelBox
                std::vector<G3D::Vector3> vertices;
                std::vector<uint32_t> indices;
                bool haveBoundsData = inst.iModel->GetMeshDataInBounds(modelBox, vertices, indices);
                if (!haveBoundsData)
                {
                    // Fallback: get all and cull manually
                    if (!inst.iModel->GetAllMeshData(vertices, indices))
                        continue;
                }

                // Emit triangles: transform to world space and push into cache/outIndices
                auto emitTriangle = [&](const G3D::Vector3& a, const G3D::Vector3& b, const G3D::Vector3& c)
                {
                    // World = (model * iScale) * iInvRot + iPos
                    G3D::Vector3 wa = (a * inst.iScale) * inst.iInvRot + inst.iPos;
                    G3D::Vector3 wb = (b * inst.iScale) * inst.iInvRot + inst.iPos;
                    G3D::Vector3 wc = (c * inst.iScale) * inst.iInvRot + inst.iPos;
                    CapsuleCollision::Triangle T;
                    T.a = { wa.x, wa.y, wa.z };
                    T.b = { wb.x, wb.y, wb.z };
                    T.c = { wc.x, wc.y, wc.z };
                    T.doubleSided = false;
                    int triIndex = (int)m_cache.size();
                    m_cache.push_back(T);
                    if (count < maxCount)
                        outIndices[count++] = triIndex;
                };

                // Iterate index buffer by triplets
                size_t triCount = indices.size() / 3;
                for (size_t t = 0; t < triCount; ++t)
                {
                    uint32_t i0 = indices[t * 3 + 0];
                    uint32_t i1 = indices[t * 3 + 1];
                    uint32_t i2 = indices[t * 3 + 2];
                    if (i0 >= vertices.size() || i1 >= vertices.size() || i2 >= vertices.size())
                        continue;
                    const G3D::Vector3& a = vertices[i0];
                    const G3D::Vector3& b = vertices[i1];
                    const G3D::Vector3& c = vertices[i2];

                    if (!haveBoundsData)
                    {
                        // Manual cull: compute tri AABB in model space and test against modelBox
                        G3D::Vector3 lo = a.min(b).min(c);
                        G3D::Vector3 hi = a.max(b).max(c);
                        G3D::AABox triBox(lo, hi);
                        if (!triBox.intersects(modelBox))
                            continue;
                    }

                    // Emit transformed triangle
                    emitTriangle(a, b, c);
                    if (count >= maxCount)
                        break;
                }

                if (count >= maxCount)
                    break;
            }
        }

        const CapsuleCollision::Triangle& tri(int idx) const override
        {
            // Assume idx is valid as provided via query output
            return m_cache[idx];
        }

        int triangleCount() const override
        {
            return static_cast<int>(m_cache.size());
        }

    private:
        const BIH* m_tree;
        const ModelInstance* m_instances;
        uint32_t m_instanceCount;
        mutable std::vector<CapsuleCollision::Triangle> m_cache;
    };

    // Constructor
    StaticMapTree::StaticMapTree(uint32_t mapId, const std::string& basePath)
        : iMapID(mapId), iBasePath(basePath), iIsTiled(false),
        iTreeValues(nullptr), iNTreeValues(0)
    {
        if (!iBasePath.empty() && iBasePath.back() != '/' && iBasePath.back() != '\\')
            iBasePath += "/";
    }

    // Destructor
    StaticMapTree::~StaticMapTree()
    {
        UnloadMap(nullptr);
        delete[] iTreeValues;
    }

    // Initialize map from file with optional full preloading
    bool StaticMapTree::InitMap(const std::string& fname, VMapManager2* vm)
    {
        bool success = true;
        std::string fullPath = iBasePath + fname;
        FILE* rf = fopen(fullPath.c_str(), "rb");
        if (!rf)
            return false;

        char chunk[8];

        // 1. Read magic (8 bytes)
        if (!readChunk(rf, chunk, VMAP_MAGIC, 8))
            success = false;

        // 2. Read tiled flag
        char tiled = 0;
        if (success && fread(&tiled, sizeof(char), 1, rf) != 1)
            success = false;
        iIsTiled = bool(tiled);

        // 3. Read NODE chunk and BIH tree
        if (success && !readChunk(rf, chunk, "NODE", 4))
            success = false;

        if (success)
            success = iTree.readFromFile(rf);

        if (success)
        {
            iNTreeValues = iTree.primCount();

            if (iNTreeValues > 0)
            {
                iTreeValues = new ModelInstance[iNTreeValues];
            }
        }

        if (success && !readChunk(rf, chunk, "GOBJ", 4))
            success = false;

        // 5. Only non-tiled maps have spawns after GOBJ
        if (success && !iIsTiled)
        {
            ModelSpawn spawn;
            while (ModelSpawn::readFromFile(rf, spawn))
            {
                // Acquire model instance
                std::shared_ptr<WorldModel> model = nullptr;
                if (!spawn.name.empty())
                {
                    model = vm->acquireModelInstance(iBasePath, spawn.name);
                    if (model)
                        model->setModelFlags(spawn.flags);
                }

                uint32_t referencedVal;
                if (fread(&referencedVal, sizeof(uint32_t), 1, rf) != 1)
                    break;

                // Map file order index to ModelInstance index space if remap in BIH is used
                uint32_t mapped = iTree.mapObjectIndex(referencedVal);
                if (mapped == 0xFFFFFFFFu)
                    continue;

                if (!iLoadedSpawns.count(mapped))
                {
                    if (mapped >= iNTreeValues)
                    {
                        continue;
                    }

                    iTreeValues[mapped] = ModelInstance(spawn, model);
                    iLoadedSpawns[mapped] = 1;  // First reference
                }
                else
                {
                    ++iLoadedSpawns[mapped];
                }
            }
        }

        fclose(rf);

        // Keep your preload functionality if needed
        if (success && iIsTiled)
        {
            PreloadAllTiles(vm);
        }

        return success;
    }

    // New method to preload all tiles
    bool StaticMapTree::PreloadAllTiles(VMapManager2* vm)
    {
        if (!iIsTiled)
            return true;

        int tilesLoaded = 0;
        int tilesFailed = 0;

        // Scan for all possible tiles (64x64 grid for WoW)
        uint32_t x = 0;
        while (x < 64)
        {
            uint32_t y = 0;
            while (y < 64)
            {
                std::string tilefile = getTileFileName(iMapID, x, y);
                std::string fullPath = iBasePath + tilefile;

                // Check if tile exists
                if (std::filesystem::exists(fullPath))
                {
                    // Load the tile
                    if (LoadMapTile(x, y, vm))
                    {
                        tilesLoaded++;
                    }
                    else
                    {
                        tilesFailed++;
                        std::cerr << "[StaticMapTree] Failed to load tile " << tilefile << std::endl;
                    }
                }
                ++y;
            }
            ++x;
        }

        // Calculate memory usage
        size_t totalModels = 0;
        uint32_t i = 0;
        while (i < iNTreeValues)
        {
            if (iTreeValues[i].iModel)
                totalModels++;
            ++i;
        }

        return tilesFailed == 0;
    }

    // LoadMapTile implementation
    bool StaticMapTree::LoadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm)
    {
        if (!iIsTiled)
        {
            iLoadedTiles[packTileID(tileX, tileY)] = false;
            return true;
        }

        uint32_t tileID = packTileID(tileX, tileY);

        // Check if already loaded
        if (iLoadedTiles.find(tileID) != iLoadedTiles.end())
        {
            bool isLoaded = iLoadedTiles[tileID];
            return true;
        }

        // Build tile filename
        std::string tilefile = getTileFileName(iMapID, tileX, tileY);
        std::string fullPath = iBasePath + tilefile;

        // Check if file exists
        bool fileExists = std::filesystem::exists(fullPath);

        if (!fileExists)
        {
            iLoadedTiles[tileID] = false;
            return true;
        }

        // Get file size for validation
        auto fileSize = std::filesystem::file_size(fullPath);

        FILE* rf = fopen(fullPath.c_str(), "rb");
        if (!rf)
        {
            iLoadedTiles[tileID] = false;
            return false;
        }

        bool success = true;

        char chunk[8];

        // Read VMAP magic
        if (!readChunk(rf, chunk, VMAP_MAGIC, 8))
        {
            success = false;
        }

        if (success)
        {
            // Read number of model spawns in this tile
            uint32_t numSpawns;
            if (fread(&numSpawns, sizeof(uint32_t), 1, rf) != 1)
            {
                success = false;
            }
            else
            {
                // Read each spawn
                uint32_t i = 0;
                while (i < numSpawns && success)
                {
                    ModelSpawn spawn;
                    if (!ModelSpawn::readFromFile(rf, spawn))
                    {
                        success = false;
                        break;
                    }

                    // Read the tree index
                    uint32_t referencedVal;
                    if (fread(&referencedVal, sizeof(uint32_t), 1, rf) != 1)
                    {
                        success = false;
                        break;
                    }

                    // Check bounds
                    if (!iTreeValues)
                    {
                        success = false;
                        break;
                    }

                    // Map to BIH compact index space
                    uint32_t mapped = iTree.mapObjectIndex(referencedVal);
                    if (mapped == 0xFFFFFFFFu)
                    {
                        ++i;
                        continue;  // Skip invalid
                    }

                    if (mapped >= iNTreeValues)
                    {
                        ++i;
                        continue;  // Skip but don't fail completely
                    }

                    // Check if already loaded
                    if (!iLoadedSpawns.count(mapped))
                    {
                        // First time loading this tree index
                        std::shared_ptr<WorldModel> model = nullptr;

                        if (!spawn.name.empty())
                        {
                            model = vm->acquireModelInstance(iBasePath, spawn.name);

                            if (model)
                            {
                                model->setModelFlags(spawn.flags);
                            }
                        }

                        iTreeValues[mapped] = ModelInstance(spawn, model);
                        iLoadedSpawns[mapped] = 1;  // First reference
                    }
                    else
                    {
                        ++iLoadedSpawns[mapped];
                    }

                    ++i;
                }
            }
        }

        fclose(rf);

        if (success)
        {
            iLoadedTiles[tileID] = true;

            // Count loaded models in the entire tree
            int totalLoadedModels = 0;
            uint32_t i = 0;
            while (i < iNTreeValues)
            {
                if (iTreeValues[i].iModel)
                    totalLoadedModels++;
                ++i;
            }
        }

        return success;
    }

    void StaticMapTree::UnloadMapTile(uint32_t tileX, uint32_t tileY, VMapManager2* vm)
    {
        if (!iIsTiled)
            return;

        uint32_t tileID = packTileID(tileX, tileY);
        auto itr = iLoadedTiles.find(tileID);
        if (itr == iLoadedTiles.end())
            return;

        iLoadedTiles.erase(itr);
    }

    void StaticMapTree::UnloadMap(VMapManager2* vm)
    {
        if (iTreeValues)
        {
            uint32_t i = 0;
            while (i < iNTreeValues)
            {
                iTreeValues[i].setUnloaded();
                ++i;
            }
        }

        iLoadedTiles.clear();
        iLoadedSpawns.clear();
    }

    // Cylinder collision implementation
    CylinderIntersection StaticMapTree::IntersectCylinder(const Cylinder& cyl) const
    {
        CylinderIntersection result;

        if (!iTreeValues || iNTreeValues == 0)
        {
            return result;
        }

        MapCylinderCallback callback(iTreeValues, cyl);
        iTree.intersectPoint(cyl.getCenter(), callback);

        return callback.bestIntersection;
    }

    std::vector<CylinderSweepHit> StaticMapTree::SweepCylinder(const Cylinder& cyl,
        const G3D::Vector3& sweepDir, float sweepDistance) const
    {
        std::vector<CylinderSweepHit> allHits;

        if (!iTreeValues || iNTreeValues == 0)
        {
            return allHits;
        }

        // Create sweep bounds for broad phase
        G3D::AABox sweepBounds = cyl.getBounds();
        Cylinder endCyl(cyl.base + sweepDir * sweepDistance, cyl.axis, cyl.radius, cyl.height);
        sweepBounds.merge(endCyl.getBounds());

        // Gather candidate indices from BIH via AABB query
        const uint32_t cap = std::min<uint32_t>(iNTreeValues, 8192);
        std::vector<uint32_t> indices(cap);
        uint32_t count = 0;
        bool any = iTree.QueryAABB(sweepBounds, indices.data(), count, cap);
        if (!any || count == 0)
        {
            return allHits;
        }

        // Use callback to gather all hits from candidates
        MapCylinderSweepCallback callback(iTreeValues, cyl, sweepDir, sweepDistance);

        for (uint32_t i = 0; i < count; ++i)
        {
            uint32_t idx = indices[i];
            if (idx >= iNTreeValues)
                continue; // index validation
            if (!iTreeValues[idx].iModel)
                continue; // skip unloaded
            if (!iTreeValues[idx].getBounds().intersects(sweepBounds))
                continue; // extra cull

            // Invoke callback for this entry
            callback(cyl.base, idx);
        }

        // Sort hits by height (highest first)
        allHits = std::move(callback.allHits);
        std::sort(allHits.begin(), allHits.end());

        // Instance ids for diagnostics are already set on each hit's QueryHit by the callback
        return allHits;
    }

    bool StaticMapTree::CheckCylinderCollision(const Cylinder& cyl,
        float& outContactHeight, G3D::Vector3& outContactNormal,
        ModelInstance** outHitInstance) const
    {
        if (!iTreeValues || iNTreeValues == 0)
        {
            return false;
        }

        MapCylinderCallback callback(iTreeValues, cyl);
        iTree.intersectPoint(cyl.getCenter(), callback);

        if (callback.bestIntersection.hit)
        {
            outContactHeight = callback.bestIntersection.contactHeight;
            outContactNormal = callback.bestIntersection.contactNormal;
            if (outHitInstance)
                *outHitInstance = callback.hitInstance;
            return true;
        }

        return false;
    }

    bool StaticMapTree::CanCylinderFitAtPosition(const Cylinder& cyl, float tolerance) const
    {
        if (!iTreeValues || iNTreeValues == 0)
            return true;

        // Parameters
        const float FOOT_ALLOW = 0.20f;          // allowable floor penetration / contact band
        const float HEAD_CLEAR_MARGIN = 0.30f;   // space required above head
        const float walkableCosMin = VMAP::CylinderHelpers::GetWalkableCosMin(); // floor normal threshold (configurable)

        // First lightweight broad test (expanded radius only) to early accept empty space
        Cylinder broad(cyl.base, cyl.axis, cyl.radius + tolerance, cyl.height);
        CylinderIntersection quickHit = IntersectCylinder(broad);
        if (!quickHit.hit)
            return true;

        // Perform vertical sweep to collect all walkable / blocking surfaces within cylinder span.
        // Sweep from slightly above top downwards full height + small epsilon.
        float sweepDist = cyl.height + FOOT_ALLOW + 0.10f;
        Cylinder sweepCyl(G3D::Vector3(cyl.base.x, cyl.base.y, cyl.base.z + cyl.height + 0.05f), cyl.axis, cyl.radius + tolerance * 0.5f, cyl.height);
        std::vector<CylinderSweepHit> hits = SweepCylinder(sweepCyl, G3D::Vector3(0,0,-1), sweepDist);

        bool hasAcceptableFloor = false;
        bool blockingCeiling = false;
        float nearestCeilingRel = 9999.0f;
        float baseZ = cyl.base.z;

        for (const auto& h : hits)
        {
            float rel = h.height - baseZ; // relative height inside desired standing cylinder
            if (rel < -0.05f) continue;          // below cylinder base beyond tolerance
            if (rel > cyl.height + 0.05f) continue; // above the cylinder top

            if (rel <= FOOT_ALLOW && h.walkable && h.normal.z >= walkableCosMin)
            {
                hasAcceptableFloor = true; // acceptable supporting surface
                continue;
            }

            // Potential ceiling / obstruction if inside head region
            if (rel >= cyl.height - HEAD_CLEAR_MARGIN && h.normal.z <= 0.3f) // only treat mostly downward facing surfaces as ceiling
            {
                blockingCeiling = true;
                nearestCeilingRel = std::min(nearestCeilingRel, rel);
            }
        }

        // Fallback: if no explicit floor found but we have a quickHit with an upward normal somewhere below mid body, accept it as support.
        if (!hasAcceptableFloor && quickHit.hit)
        {
            float qRel = quickHit.contactHeight - baseZ;
            if (quickHit.contactNormal.z >= walkableCosMin && qRel >= -0.25f && qRel <= cyl.height * 0.6f)
            {
                hasAcceptableFloor = true; // treat as supporting (cliff edge / sparse geometry)
            }
            else if (qRel >= cyl.height - HEAD_CLEAR_MARGIN && quickHit.contactNormal.z <= 0.3f)
            {
                blockingCeiling = true;
                nearestCeilingRel = std::min(nearestCeilingRel, qRel);
            }
        }

        // Final permissive fallback: standing over empty space (no floor, no ceiling) -> allow; movement code will handle gravity separately.
        if (!hasAcceptableFloor && !blockingCeiling && hits.empty())
        {
            hasAcceptableFloor = true; // allow transition so player can start falling instead of being frozen
        }

        bool fit = hasAcceptableFloor && !blockingCeiling;

        LOG_INFO("CanCylinderFitAtPosition SWEEP baseZ=" << baseZ
            << " floor=" << (hasAcceptableFloor?1:0)
            << " blockCeil=" << (blockingCeiling?1:0)
            << " nearestCeilRel=" << (nearestCeilingRel==9999.0f? -1.0f:nearestCeilingRel)
            << " h=" << cyl.height
            << " r=" << cyl.radius
            << " quickRel=" << (quickHit.hit ? (quickHit.contactHeight - baseZ) : -999.0f)
            << " quickNz=" << (quickHit.hit ? quickHit.contactNormal.z : -1.0f)
            << " hits=" << hits.size());

        return fit;
    }

    bool StaticMapTree::FindCylinderWalkableSurface(const Cylinder& cyl,
        float currentHeight, float maxStepUp, float maxStepDown,
        float& outHeight, G3D::Vector3& outNormal) const
    {
        if (!iTreeValues || iNTreeValues == 0)
        {
            return false;
        }

        // Sweep downward to find surfaces
        G3D::Vector3 sweepDir(0, 0, -1);
        float sweepDistance = maxStepUp + maxStepDown;

        // Start sweep from above current position
        Cylinder sweepCyl(
            G3D::Vector3(cyl.base.x, cyl.base.y, currentHeight + maxStepUp),
            cyl.axis, cyl.radius, cyl.height
        );

        std::vector<CylinderSweepHit> hits = SweepCylinder(sweepCyl, sweepDir, sweepDistance);

        // Find best walkable surface using CylinderHelpers
        return CylinderCollision::FindBestWalkableSurface(
            cyl, hits, currentHeight, maxStepUp, maxStepDown, outHeight, outNormal
        );
    }

    void StaticMapTree::GetCylinderCollisionCandidates(const Cylinder& cyl,
        std::vector<ModelInstance*>& outInstances) const
    {
        outInstances.clear();

        if (!iTreeValues || iNTreeValues == 0)
        {
            return;
        }

        // Use BIH AABB query directly around cylinder bounds
        G3D::AABox bounds = cyl.getBounds();
        const uint32_t cap = std::min<uint32_t>(iNTreeValues, 8192);
        std::vector<uint32_t> indices(cap);
        uint32_t count = 0;
        bool any = iTree.QueryAABB(bounds, indices.data(), count, cap);
        if (!any || count == 0)
            return;

        outInstances.reserve(count);
        for (uint32_t i = 0; i < count; ++i)
        {
            uint32_t idx = indices[i];
            if (idx >= iNTreeValues)
                continue; // index validation to avoid OOB
            if (!iTreeValues[idx].iModel)
                continue; // skip unloaded/missing
            if (!iTreeValues[idx].getBounds().intersects(bounds))
                continue; // extra cull using instance bounds

            outInstances.push_back(&iTreeValues[idx]);
        }
    }

    // All original query methods remain the same with null checks
    bool StaticMapTree::isInLineOfSight(const G3D::Vector3& pos1, const G3D::Vector3& pos2, bool ignoreM2Model) const
    {
        if (!iTreeValues || iNTreeValues == 0)
        {
            return true;
        }

        float maxDist = (pos2 - pos1).magnitude();
        if (maxDist < 0.001f)
        {
            return true;
        }

        G3D::Ray ray = G3D::Ray::fromOriginAndDirection(pos1, (pos2 - pos1) / maxDist);

        float intersectDist = maxDist;
        bool hit = getIntersectionTime(ray, intersectDist, true, ignoreM2Model);

        return !hit;
    }

    bool StaticMapTree::getObjectHitPos(const G3D::Vector3& pos1, const G3D::Vector3& pos2,
        G3D::Vector3& resultHitPos, float modifyDist) const
    {
        if (!iTreeValues || iNTreeValues == 0)
        {
            resultHitPos = pos2;
            return false;
        }

        float maxDist = (pos2 - pos1).magnitude();
        if (maxDist < 0.001f)
        {
            resultHitPos = pos2;
            return false;
        }

        G3D::Vector3 dir = (pos2 - pos1) / maxDist;
        G3D::Ray ray = G3D::Ray::fromOriginAndDirection(pos1, dir);

        float distance = maxDist;
        if (getIntersectionTime(ray, distance, true, false))
        {
            resultHitPos = pos1 + dir * distance;

            if (modifyDist > 0 && distance > modifyDist)
            {
                resultHitPos = pos1 + dir * (distance - modifyDist);
            }
            return true;
        }

        resultHitPos = pos2;
        return false;
    }

    float StaticMapTree::getHeight(const G3D::Vector3& pos, float maxSearchDist) const
    {
        float height = -G3D::inf();

        if (!iTreeValues || iNTreeValues == 0)
        {
            return height;
        }

        // Count loaded models for debugging
        int loadedCount = 0;
        int checkedCount = 0;
        uint32_t i = 0;
        while (i < iNTreeValues)
        {
            if (iTreeValues[i].iModel)
                loadedCount++;
            ++i;
        }

        // The ray shoots downward from above
        G3D::Vector3 rayStart = pos;
        G3D::Ray ray(rayStart, G3D::Vector3(0, 0, -1));
        float distance = maxSearchDist * 2;

        float originalDistance = distance;
        if (getIntersectionTime(ray, distance, false, false))
        {
            height = pos.z - distance;
        }

        return height;
    }

    bool StaticMapTree::getAreaInfo(G3D::Vector3& pos, uint32_t& flags, int32_t& adtId,
        int32_t& rootId, int32_t& groupId) const
    {
        // Define the callback class for area info collection
        class AreaInfoCallback
        {
        public:
            AreaInfoCallback(ModelInstance* val) : prims(val) {}

            void operator()(const G3D::Vector3& point, uint32_t entry)
            {
                if (!prims || !prims[entry].iModel)
                    return;
                prims[entry].intersectPoint(point, aInfo);
            }

            ModelInstance* prims;
            AreaInfo aInfo;
        };

        AreaInfoCallback intersectionCallBack(iTreeValues);
        iTree.intersectPoint(pos, intersectionCallBack);

        if (intersectionCallBack.aInfo.result)
        {
            flags = intersectionCallBack.aInfo.flags;
            adtId = intersectionCallBack.aInfo.adtId;
            rootId = intersectionCallBack.aInfo.rootId;
            groupId = intersectionCallBack.aInfo.groupId;
            pos.z = intersectionCallBack.aInfo.ground_Z;
            return true;
        }

        return false;
    }

    bool StaticMapTree::GetLocationInfo(const G3D::Vector3& pos, LocationInfo& info) const
    {
        if (!iTreeValues || iNTreeValues == 0)
        {
            return false;
        }

        // Define callback for point location query
        class LocationInfoCallback
        {
        public:
            LocationInfoCallback(ModelInstance* val) : prims(val), found(false) {}

            void operator()(const G3D::Vector3& point, uint32_t entry)
            {
                if (!prims || !prims[entry].iModel)
                {
                    return;
                }

                // Check if this model can provide location info
                if (prims[entry].GetLocationInfo(point, tempInfo))
                {
                    found = true;
                }
            }

            ModelInstance* prims;
            LocationInfo tempInfo;
            bool found;
        };

        // Use BIH tree to find relevant models at this position
        LocationInfoCallback callback(iTreeValues);
        iTree.intersectPoint(pos, callback);

        if (callback.found)
        {
            info = callback.tempInfo;
            return true;
        }

        return false;
    }

    bool StaticMapTree::getIntersectionTime(G3D::Ray const& pRay, float& pMaxDist, bool pStopAtFirstHit, bool ignoreM2Model) const
    {
        float distance = pMaxDist;
        MapRayCallback intersectionCallBack(iTreeValues);
        iTree.intersectRay(pRay, intersectionCallBack, distance, pStopAtFirstHit, ignoreM2Model);
        if (intersectionCallBack.didHit())
            pMaxDist = distance;
        return intersectionCallBack.didHit();
    }

    uint32_t StaticMapTree::packTileID(uint32_t tileX, uint32_t tileY)
    {
        return (tileX << 16) | tileY;
    }

    void StaticMapTree::unpackTileID(uint32_t ID, uint32_t& tileX, uint32_t& tileY)
    {
        tileX = (ID >> 16);
        tileY = (ID & 0xFFFF);
    }

    std::string StaticMapTree::getTileFileName(uint32_t mapID, uint32_t tileX, uint32_t tileY)
    {
        char buffer[256];
        snprintf(buffer, sizeof(buffer), "%03u_%02u_%02u.vmtile", mapID, tileX, tileY);
        return std::string(buffer);
    }

    bool StaticMapTree::CanLoadMap(const std::string& vmapPath, uint32_t mapID, uint32_t tileX, uint32_t tileY)
    {
        std::string fileName = vmapPath + getTileFileName(mapID, tileX, tileY);
        FILE* rf = fopen(fileName.c_str(), "rb");
        if (!rf)
            return false;

        fclose(rf);
        return true;
    }

    bool StaticMapTree::isTiled() const
    {
        return iIsTiled;
    }

    uint32_t StaticMapTree::numLoadedTiles() const
    {
        return iLoadedTiles.size();
    }

    bool StaticMapTree::isUnderModel(G3D::Vector3& pos, float* outDist, float* inDist) const
    {
        if (!iTreeValues || iNTreeValues == 0)
            return false;

        G3D::Ray ray(pos, G3D::Vector3(0, 0, 1));
        float maxDist = 100.0f;

        auto callback = [this](const G3D::Ray& r, uint32_t idx,
            float& d, bool stopAtFirst, bool ignoreM2) -> bool
            {
                if (!iTreeValues || idx >= iNTreeValues || !iTreeValues[idx].iModel)
                    return false;

                return iTreeValues[idx].intersectRay(r, d, stopAtFirst, ignoreM2);
            };

        float distance = maxDist;
        iTree.intersectRay(ray, callback, distance, true, false);

        if (distance < maxDist)
        {
            if (outDist)
                *outDist = distance;
            if (inDist)
                *inDist = 0.0f;
            return true;
        }

        return false;
    }

    ModelInstance* StaticMapTree::FindCollisionModel(const G3D::Vector3& pos1, const G3D::Vector3& pos2)
    {
        if (!iTreeValues || iNTreeValues == 0)
            return nullptr;

        float maxDist = (pos2 - pos1).magnitude();
        if (maxDist < 0.001f)
            return nullptr;

        G3D::Ray ray = G3D::Ray::fromOriginAndDirection(pos1, (pos2 - pos1) / maxDist);

        ModelInstance* hitModel = nullptr;
        float closestDist = maxDist;

        uint32_t i = 0;
        while (i < iNTreeValues)
        {
            if (iTreeValues[i].iModel)
            {
                float dist = maxDist;
                if (iTreeValues[i].intersectRay(ray, dist, true, false))
                {
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        hitModel = &iTreeValues[i];
                    }
                }
            }
            ++i;
        }

        return hitModel;
    }

#ifdef MMAP_GENERATOR
    void StaticMapTree::getModelInstances(ModelInstance*& models, uint32_t& count)
    {
        models = iTreeValues;
        count = iNTreeValues;
    }
#endif

} // namespace VMAP