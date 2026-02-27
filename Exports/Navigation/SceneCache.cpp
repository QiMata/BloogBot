// SceneCache.cpp - Pre-processed collision geometry cache.
// Extracts VMAP + ADT triangles into world-space flat arrays with 2D spatial index.

#include "SceneCache.h"
#include "VMapManager2.h"
#include "StaticMapTree.h"
#include "ModelInstance.h"
#include "WorldModel.h"
#include "MapLoader.h"
#include "CoordinateTransforms.h"
#include "SceneQuery.h"
#include <cstdio>
#include <cstring>
#include <algorithm>
#include <cmath>
#include <limits>
#include <unordered_set>

// ============================================================================
// FILE I/O
// ============================================================================

bool SceneCache::SaveToFile(const char* path) const
{
    FILE* f = fopen(path, "wb");
    if (!f) return false;

    // Header (64 bytes)
    uint32_t magic = FILE_MAGIC;
    uint32_t version = FILE_VERSION;
    uint32_t triCount = static_cast<uint32_t>(m_triangles.size());
    uint32_t triIdxCount = static_cast<uint32_t>(m_triIndices.size());
    uint32_t liqCellsX = m_liquidCellsX;
    uint32_t liqCellsY = m_liquidCellsY;
    uint32_t reserved = 0;

    fwrite(&magic, 4, 1, f);
    fwrite(&version, 4, 1, f);
    fwrite(&mapId, 4, 1, f);
    fwrite(&triCount, 4, 1, f);
    fwrite(&m_cellSize, 4, 1, f);
    fwrite(&m_cellsX, 4, 1, f);
    fwrite(&m_cellsY, 4, 1, f);
    fwrite(&triIdxCount, 4, 1, f);
    fwrite(&m_liquidCellSize, 4, 1, f);
    fwrite(&liqCellsX, 4, 1, f);
    fwrite(&liqCellsY, 4, 1, f);
    fwrite(&m_minX, 4, 1, f);
    fwrite(&m_minY, 4, 1, f);
    fwrite(&m_maxX, 4, 1, f);
    fwrite(&m_maxY, 4, 1, f);
    fwrite(&reserved, 4, 1, f);
    // = 16 * 4 = 64 bytes

    // Triangles
    if (triCount > 0)
        fwrite(m_triangles.data(), sizeof(SceneTri), triCount, f);

    // Spatial index
    uint32_t cellTotal = m_cellsX * m_cellsY;
    if (cellTotal > 0)
    {
        fwrite(m_cellStart.data(), 4, cellTotal, f);
        fwrite(m_cellCount.data(), 4, cellTotal, f);
    }
    if (triIdxCount > 0)
        fwrite(m_triIndices.data(), 4, triIdxCount, f);

    // Liquid grid
    fwrite(&m_liquidMinX, 4, 1, f);
    fwrite(&m_liquidMinY, 4, 1, f);
    uint32_t liqCount = liqCellsX * liqCellsY;
    if (liqCount > 0)
        fwrite(m_liquidGrid.data(), sizeof(LiquidCell), liqCount, f);

    fclose(f);
    return true;
}

SceneCache* SceneCache::LoadFromFile(const char* path)
{
    FILE* f = fopen(path, "rb");
    if (!f) return nullptr;

    // Header
    uint32_t magic, version, triCount, triIdxCount, liqCellsX, liqCellsY, reserved;
    auto* cache = new SceneCache();

    fread(&magic, 4, 1, f);
    fread(&version, 4, 1, f);
    if (magic != FILE_MAGIC || version != FILE_VERSION)
    {
        fclose(f);
        delete cache;
        return nullptr;
    }

    fread(&cache->mapId, 4, 1, f);
    fread(&triCount, 4, 1, f);
    fread(&cache->m_cellSize, 4, 1, f);
    fread(&cache->m_cellsX, 4, 1, f);
    fread(&cache->m_cellsY, 4, 1, f);
    fread(&triIdxCount, 4, 1, f);
    fread(&cache->m_liquidCellSize, 4, 1, f);
    fread(&liqCellsX, 4, 1, f);
    fread(&liqCellsY, 4, 1, f);
    fread(&cache->m_minX, 4, 1, f);
    fread(&cache->m_minY, 4, 1, f);
    fread(&cache->m_maxX, 4, 1, f);
    fread(&cache->m_maxY, 4, 1, f);
    fread(&reserved, 4, 1, f);

    cache->m_liquidCellsX = liqCellsX;
    cache->m_liquidCellsY = liqCellsY;

    // Triangles
    cache->m_triangles.resize(triCount);
    if (triCount > 0)
        fread(cache->m_triangles.data(), sizeof(SceneTri), triCount, f);

    // Spatial index
    uint32_t cellTotal = cache->m_cellsX * cache->m_cellsY;
    cache->m_cellStart.resize(cellTotal);
    cache->m_cellCount.resize(cellTotal);
    if (cellTotal > 0)
    {
        fread(cache->m_cellStart.data(), 4, cellTotal, f);
        fread(cache->m_cellCount.data(), 4, cellTotal, f);
    }
    cache->m_triIndices.resize(triIdxCount);
    if (triIdxCount > 0)
        fread(cache->m_triIndices.data(), 4, triIdxCount, f);

    // Liquid grid
    fread(&cache->m_liquidMinX, 4, 1, f);
    fread(&cache->m_liquidMinY, 4, 1, f);
    uint32_t liqCount = liqCellsX * liqCellsY;
    cache->m_liquidGrid.resize(liqCount);
    if (liqCount > 0)
        fread(cache->m_liquidGrid.data(), sizeof(LiquidCell), liqCount, f);

    fclose(f);
    return cache;
}

// ============================================================================
// EXTRACTION FROM LIVE VMAP + ADT DATA
// ============================================================================

SceneCache* SceneCache::Extract(uint32_t mapId,
                                VMAP::VMapManager2* vmapMgr,
                                MapLoader* mapLoader,
                                const ExtractBounds& bounds)
{
    auto* cache = new SceneCache();
    cache->mapId = mapId;

    float bMinX = bounds.minX, bMinY = bounds.minY;
    float bMaxX = bounds.maxX, bMaxY = bounds.maxY;
    bool hasBounds = !bounds.IsEmpty();

    // Track actual triangle extents for spatial index
    float actualMinX = 1e9f, actualMinY = 1e9f;
    float actualMaxX = -1e9f, actualMaxY = -1e9f;

    // 1) Extract VMAP model triangles to world space
    if (vmapMgr)
    {
        // Ensure map is loaded
        if (!vmapMgr->isMapInitialized(mapId))
            vmapMgr->initializeMap(mapId);

        const VMAP::StaticMapTree* mapTree = vmapMgr->GetStaticMapTree(mapId);
        if (mapTree)
        {
            const VMAP::ModelInstance* instances = mapTree->GetInstancesPtr();
            uint32_t instanceCount = mapTree->GetInstanceCount();

            for (uint32_t i = 0; i < instanceCount; ++i)
            {
                const VMAP::ModelInstance& mi = instances[i];
                if (!mi.iModel) continue;

                // Quick AABB filter: transform instance bounds to world space
                if (hasBounds)
                {
                    // Convert instance position (internal space) to world
                    G3D::Vector3 instPosW = NavCoord::InternalToWorld(mi.iPos);
                    // Rough radius from bound: use max of half-extents
                    G3D::Vector3 halfExt = (mi.iBound.high() - mi.iBound.low()) * 0.5f * mi.iScale;
                    float instRadius = std::max({halfExt.x, halfExt.y, halfExt.z});
                    // Check XY overlap with extraction bounds (conservative)
                    if (instPosW.x + instRadius < bMinX || instPosW.x - instRadius > bMaxX ||
                        instPosW.y + instRadius < bMinY || instPosW.y - instRadius > bMaxY)
                        continue;
                }

                // Get all mesh data from WorldModel (model-local vertices + indices)
                std::vector<G3D::Vector3> localVerts;
                std::vector<uint32_t> indices;
                if (!mi.iModel->GetAllMeshData(localVerts, indices))
                    continue;
                if (localVerts.empty() || indices.size() < 3)
                    continue;

                // Transform all vertices to world space once
                std::vector<G3D::Vector3> worldVerts(localVerts.size());
                for (size_t v = 0; v < localVerts.size(); ++v)
                {
                    // model-local → internal: scale, rotate, translate
                    G3D::Vector3 internal = mi.iRot * (localVerts[v] * mi.iScale) + mi.iPos;
                    // internal → world
                    worldVerts[v] = NavCoord::InternalToWorld(internal);
                }

                // Emit triangles (indices are flattened: 3 per triangle)
                for (size_t t = 0; t + 2 < indices.size(); t += 3)
                {
                    const G3D::Vector3& a = worldVerts[indices[t]];
                    const G3D::Vector3& b = worldVerts[indices[t + 1]];
                    const G3D::Vector3& c = worldVerts[indices[t + 2]];

                    // Bounds check on triangle
                    if (hasBounds)
                    {
                        float txMin = std::min({a.x, b.x, c.x});
                        float txMax = std::max({a.x, b.x, c.x});
                        float tyMin = std::min({a.y, b.y, c.y});
                        float tyMax = std::max({a.y, b.y, c.y});
                        if (txMax < bMinX || txMin > bMaxX ||
                            tyMax < bMinY || tyMin > bMaxY)
                            continue;
                    }

                    SceneTri st;
                    st.ax = a.x; st.ay = a.y; st.az = a.z;
                    st.bx = b.x; st.by = b.y; st.bz = b.z;
                    st.cx = c.x; st.cy = c.y; st.cz = c.z;
                    st.sourceType = 0; // VMAP
                    st.instanceId = mi.ID;
                    cache->m_triangles.push_back(st);

                    actualMinX = std::min(actualMinX, std::min({a.x, b.x, c.x}));
                    actualMinY = std::min(actualMinY, std::min({a.y, b.y, c.y}));
                    actualMaxX = std::max(actualMaxX, std::max({a.x, b.x, c.x}));
                    actualMaxY = std::max(actualMaxY, std::max({a.y, b.y, c.y}));
                }
            }
        }
    }

    // 2) Extract ADT terrain triangles (already in world space)
    if (mapLoader && mapLoader->IsInitialized())
    {
        float tMinX, tMinY, tMaxX, tMaxY;
        if (hasBounds)
        {
            tMinX = bMinX; tMinY = bMinY;
            tMaxX = bMaxX; tMaxY = bMaxY;
        }
        else
        {
            // Full map: use ADT grid extent
            // WoW ADT coords: approximately -17066 to +17066
            tMinX = -17067.0f; tMinY = -17067.0f;
            tMaxX = 17067.0f; tMaxY = 17067.0f;
        }

        std::vector<MapFormat::TerrainTriangle> terrainTris;
        mapLoader->GetTerrainTriangles(mapId, tMinX, tMinY, tMaxX, tMaxY, terrainTris);

        for (const auto& tw : terrainTris)
        {
            SceneTri st;
            st.ax = tw.ax; st.ay = tw.ay; st.az = tw.az;
            st.bx = tw.bx; st.by = tw.by; st.bz = tw.bz;
            st.cx = tw.cx; st.cy = tw.cy; st.cz = tw.cz;
            st.sourceType = 1; // ADT
            st.instanceId = 0;
            cache->m_triangles.push_back(st);

            actualMinX = std::min(actualMinX, std::min({tw.ax, tw.bx, tw.cx}));
            actualMinY = std::min(actualMinY, std::min({tw.ay, tw.by, tw.cy}));
            actualMaxX = std::max(actualMaxX, std::max({tw.ax, tw.bx, tw.cx}));
            actualMaxY = std::max(actualMaxY, std::max({tw.ay, tw.by, tw.cy}));
        }
    }

    if (cache->m_triangles.empty())
    {
        delete cache;
        return nullptr;
    }

    // Set bounds from actual triangle extents (with small padding)
    cache->m_minX = actualMinX - 1.0f;
    cache->m_minY = actualMinY - 1.0f;
    cache->m_maxX = actualMaxX + 1.0f;
    cache->m_maxY = actualMaxY + 1.0f;

    // 3) Sample liquid grid
    {
        float lMinX = cache->m_minX;
        float lMinY = cache->m_minY;
        float lMaxX = cache->m_maxX;
        float lMaxY = cache->m_maxY;
        cache->m_liquidMinX = lMinX;
        cache->m_liquidMinY = lMinY;
        cache->m_liquidCellsX = static_cast<uint32_t>(std::ceil((lMaxX - lMinX) / cache->m_liquidCellSize));
        cache->m_liquidCellsY = static_cast<uint32_t>(std::ceil((lMaxY - lMinY) / cache->m_liquidCellSize));

        uint32_t liqTotal = cache->m_liquidCellsX * cache->m_liquidCellsY;
        cache->m_liquidGrid.resize(liqTotal);

        for (uint32_t cy = 0; cy < cache->m_liquidCellsY; ++cy)
        {
            for (uint32_t cx = 0; cx < cache->m_liquidCellsX; ++cx)
            {
                float sampleX = lMinX + (cx + 0.5f) * cache->m_liquidCellSize;
                float sampleY = lMinY + (cy + 0.5f) * cache->m_liquidCellSize;
                float sampleZ = 5000.0f; // query from high altitude

                LiquidCell& cell = cache->m_liquidGrid[cy * cache->m_liquidCellsX + cx];
                cell.level = 0.0f;
                cell.type = 0;
                cell.flags = 0;
                std::memset(cell.pad, 0, sizeof(cell.pad));

                // Query liquid from SceneQuery (which checks both ADT and VMAP)
                auto info = SceneQuery::EvaluateLiquidAt(mapId, sampleX, sampleY, sampleZ);
                if (info.hasLevel)
                {
                    cell.level = info.level;
                    cell.type = info.type;
                    cell.flags = 0x01; // hasLevel
                    if (info.fromVmap)
                        cell.flags |= 0x02;
                }
            }
        }
    }

    // 4) Build spatial index
    cache->BuildSpatialIndex();

    fprintf(stderr, "[SceneCache] Extracted map %u: %zu triangles, %u x %u grid cells, %u x %u liquid cells\n",
            mapId, cache->m_triangles.size(), cache->m_cellsX, cache->m_cellsY,
            cache->m_liquidCellsX, cache->m_liquidCellsY);

    return cache;
}

// ============================================================================
// SPATIAL INDEX
// ============================================================================

void SceneCache::BuildSpatialIndex()
{
    float rangeX = m_maxX - m_minX;
    float rangeY = m_maxY - m_minY;
    if (rangeX <= 0 || rangeY <= 0) return;

    m_cellsX = std::max(1u, static_cast<uint32_t>(std::ceil(rangeX / m_cellSize)));
    m_cellsY = std::max(1u, static_cast<uint32_t>(std::ceil(rangeY / m_cellSize)));

    uint32_t totalCells = m_cellsX * m_cellsY;

    // Count triangles per cell
    std::vector<std::vector<uint32_t>> cellTriLists(totalCells);

    for (uint32_t ti = 0; ti < m_triangles.size(); ++ti)
    {
        const SceneTri& t = m_triangles[ti];
        float txMin = std::min({t.ax, t.bx, t.cx});
        float txMax = std::max({t.ax, t.bx, t.cx});
        float tyMin = std::min({t.ay, t.by, t.cy});
        float tyMax = std::max({t.ay, t.by, t.cy});

        // Clamp to grid bounds
        uint32_t cxMin = static_cast<uint32_t>(std::max(0.0f, (txMin - m_minX) / m_cellSize));
        uint32_t cxMax = static_cast<uint32_t>(std::max(0.0f, (txMax - m_minX) / m_cellSize));
        uint32_t cyMin = static_cast<uint32_t>(std::max(0.0f, (tyMin - m_minY) / m_cellSize));
        uint32_t cyMax = static_cast<uint32_t>(std::max(0.0f, (tyMax - m_minY) / m_cellSize));

        cxMax = std::min(cxMax, m_cellsX - 1);
        cyMax = std::min(cyMax, m_cellsY - 1);
        cxMin = std::min(cxMin, m_cellsX - 1);
        cyMin = std::min(cyMin, m_cellsY - 1);

        for (uint32_t cy = cyMin; cy <= cyMax; ++cy)
            for (uint32_t cx = cxMin; cx <= cxMax; ++cx)
                cellTriLists[cy * m_cellsX + cx].push_back(ti);
    }

    // Flatten into sorted arrays
    m_cellStart.resize(totalCells);
    m_cellCount.resize(totalCells);
    m_triIndices.clear();

    for (uint32_t ci = 0; ci < totalCells; ++ci)
    {
        m_cellStart[ci] = static_cast<uint32_t>(m_triIndices.size());
        m_cellCount[ci] = static_cast<uint32_t>(cellTriLists[ci].size());
        m_triIndices.insert(m_triIndices.end(), cellTriLists[ci].begin(), cellTriLists[ci].end());
    }
}

// ============================================================================
// QUERY METHODS
// ============================================================================

void SceneCache::QueryTrianglesInAABB(float minX, float minY, float maxX, float maxY,
                                      std::vector<CapsuleCollision::Triangle>& outTris,
                                      std::vector<uint32_t>* outInstanceIds) const
{
    outTris.clear();
    if (outInstanceIds) outInstanceIds->clear();
    if (m_cellsX == 0 || m_cellsY == 0) return;

    // Compute cell range
    int cxMin = static_cast<int>((minX - m_minX) / m_cellSize);
    int cxMax = static_cast<int>((maxX - m_minX) / m_cellSize);
    int cyMin = static_cast<int>((minY - m_minY) / m_cellSize);
    int cyMax = static_cast<int>((maxY - m_minY) / m_cellSize);

    cxMin = std::max(0, cxMin);
    cxMax = std::min(static_cast<int>(m_cellsX) - 1, cxMax);
    cyMin = std::max(0, cyMin);
    cyMax = std::min(static_cast<int>(m_cellsY) - 1, cyMax);

    // Deduplicate: track which triangles we've already added
    std::unordered_set<uint32_t> seen;

    for (int cy = cyMin; cy <= cyMax; ++cy)
    {
        for (int cx = cxMin; cx <= cxMax; ++cx)
        {
            uint32_t ci = cy * m_cellsX + cx;
            uint32_t start = m_cellStart[ci];
            uint32_t count = m_cellCount[ci];

            for (uint32_t j = 0; j < count; ++j)
            {
                uint32_t ti = m_triIndices[start + j];
                if (!seen.insert(ti).second) continue;

                const SceneTri& st = m_triangles[ti];

                CapsuleCollision::Triangle tri;
                tri.a = { st.ax, st.ay, st.az };
                tri.b = { st.bx, st.by, st.bz };
                tri.c = { st.cx, st.cy, st.cz };
                tri.doubleSided = false;
                tri.collisionMask = 0xFFFFFFFFu;
                outTris.push_back(tri);

                if (outInstanceIds)
                    outInstanceIds->push_back(st.instanceId);
            }
        }
    }
}

float SceneCache::GetGroundZ(float x, float y, float z, float maxSearchDist) const
{
    // Find the cell at (x,y) and check all triangles for vertical ray intersection
    if (m_cellsX == 0 || m_cellsY == 0)
        return -200000.0f;

    int cx = static_cast<int>((x - m_minX) / m_cellSize);
    int cy = static_cast<int>((y - m_minY) / m_cellSize);
    if (cx < 0 || cx >= static_cast<int>(m_cellsX) ||
        cy < 0 || cy >= static_cast<int>(m_cellsY))
        return -200000.0f;

    uint32_t ci = cy * m_cellsX + cx;
    uint32_t start = m_cellStart[ci];
    uint32_t count = m_cellCount[ci];

    float bestZ = -200000.0f;
    float bestErr = std::numeric_limits<float>::max();
    float zMax = z + 0.5f;         // accept slightly above
    float zMin = z - maxSearchDist; // search below

    for (uint32_t j = 0; j < count; ++j)
    {
        const SceneTri& st = m_triangles[m_triIndices[start + j]];

        // Quick AABB check: does triangle contain (x,y) in XY?
        float txMin = std::min({st.ax, st.bx, st.cx});
        float txMax = std::max({st.ax, st.bx, st.cx});
        float tyMin = std::min({st.ay, st.by, st.cy});
        float tyMax = std::max({st.ay, st.by, st.cy});
        if (x < txMin || x > txMax || y < tyMin || y > tyMax)
            continue;

        // Barycentric test: is (x,y) inside triangle in XY projection?
        float v0x = st.cx - st.ax, v0y = st.cy - st.ay;
        float v1x = st.bx - st.ax, v1y = st.by - st.ay;
        float v2x = x - st.ax, v2y = y - st.ay;

        float d00 = v0x * v0x + v0y * v0y;
        float d01 = v0x * v1x + v0y * v1y;
        float d02 = v0x * v2x + v0y * v2y;
        float d11 = v1x * v1x + v1y * v1y;
        float d12 = v1x * v2x + v1y * v2y;

        float denom = d00 * d11 - d01 * d01;
        if (std::fabs(denom) < 1e-12f) continue;

        float invDenom = 1.0f / denom;
        float u = (d11 * d02 - d01 * d12) * invDenom;
        float v = (d00 * d12 - d01 * d02) * invDenom;

        if (u < -1e-6f || v < -1e-6f || (u + v) > 1.0f + 1e-6f)
            continue;

        // Interpolate Z
        float triZ = st.az + u * (st.cz - st.az) + v * (st.bz - st.az);

        // Pick the surface closest to query Z (consistent with non-cached
        // SceneQuery::GetGroundZ which uses "closest to z" for multi-level).
        if (triZ >= zMin && triZ <= zMax) {
            float err = std::fabs(triZ - z);
            if (bestZ <= -200000.0f + 1.0f || err < bestErr) {
                bestZ = triZ;
                bestErr = err;
            }
        }
    }

    return bestZ;
}

LiquidCell SceneCache::GetLiquidAt(float x, float y) const
{
    LiquidCell empty{};
    if (m_liquidGrid.empty() || m_liquidCellsX == 0 || m_liquidCellsY == 0)
        return empty;

    int cx = static_cast<int>((x - m_liquidMinX) / m_liquidCellSize);
    int cy = static_cast<int>((y - m_liquidMinY) / m_liquidCellSize);

    if (cx < 0 || cx >= static_cast<int>(m_liquidCellsX) ||
        cy < 0 || cy >= static_cast<int>(m_liquidCellsY))
        return empty;

    return m_liquidGrid[cy * m_liquidCellsX + cx];
}
