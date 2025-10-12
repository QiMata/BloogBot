// BIH.cpp - Fixed for vMaNGOS format
#include "BIH.h"
#include "VMapDefinitions.h"
#include <cstdio>
#include <algorithm>
#include <limits>
#include <iostream>

BIH::BIH()
{
    init_empty();
}

void BIH::init_empty()
{
    tree.clear();
    objects.clear();
    bounds = G3D::AABox();
    m_remap.clear();
    m_useRemap = false;
    m_primCountCached = 0;
    // create space for the first node
    tree.push_back(static_cast<uint32_t>(3 << 30)); // dummy leaf
    tree.insert(tree.end(), 2, 0);
}

bool BIH::readFromFile(FILE* rf)
{
    if (!rf)
    {
        std::cerr << "[BIH] ERROR: NULL file pointer" << std::endl;
        return false;
    }

    // Read bounding box (6 floats)
    float boundsData[6];
    if (fread(boundsData, sizeof(float), 6, rf) != 6)
    {
        std::cerr << "[BIH] ERROR: Failed to read bounding box" << std::endl;
        return false;
    }

    G3D::Vector3 lo(boundsData[0], boundsData[1], boundsData[2]);
    G3D::Vector3 hi(boundsData[3], boundsData[4], boundsData[5]);

    bounds = G3D::AABox(lo, hi);

    uint32_t treeSize;
    if (fread(&treeSize, sizeof(uint32_t), 1, rf) != 1)
    {
        std::cerr << "[BIH] ERROR: Failed to read tree size" << std::endl;
        return false;
    }

    // Read tree data
    tree.clear();
    tree.resize(treeSize);
    if (treeSize > 0 && fread(&tree[0], sizeof(uint32_t), treeSize, rf) != treeSize)
    {
        std::cerr << "[BIH] ERROR: Failed to read tree data" << std::endl;
        return false;
    }

    uint32_t count;
    if (fread(&count, sizeof(uint32_t), 1, rf) != 1)
    {
        std::cerr << "[BIH] ERROR: Failed to read object count" << std::endl;
        return false;
    }

    // Read object indices
    objects.clear();
    objects.resize(count);
    if (count > 0 && fread(&objects[0], sizeof(uint32_t), count, rf) != count)
    {
        std::cerr << "[BIH] ERROR: Failed to read object indices" << std::endl;
        return false;
    }

    // Build safety metadata: compute prim count as (maxId + 1), handle empty case.
    uint32_t maxId = 0;
    for (uint32_t v : objects)
        if (v > maxId) maxId = v;

    if (objects.empty())
    {
        m_primCountCached = 0;
    }
    else
    {
        // Ensure capacity for referenced indices. This avoids overflow when callers index iTreeValues.
        // We do not compact here, we keep original IDs as the external space.
        m_primCountCached = maxId + 1;
    }

    // Prepare identity remap so mapObjectIndex() can be used consistently and bounds-checked.
    m_remap.assign(m_primCountCached == 0 ? 1u : m_primCountCached, 0xFFFFFFFFu);
    for (uint32_t v : objects)
    {
        if (v < m_remap.size())
            m_remap[v] = v; // identity by default
    }
    m_useRemap = false; // currently identity; can be toggled if a future format requires compacting

    return true;
}

uint32_t BIH::mapObjectIndex(uint32_t original) const
{
    if (!m_useRemap)
    {
        // Identity mapping with bounds check
        return (original < m_primCountCached) ? original : 0xFFFFFFFFu;
    }

    if (original >= m_remap.size())
        return 0xFFFFFFFFu;

    return m_remap[original];
}