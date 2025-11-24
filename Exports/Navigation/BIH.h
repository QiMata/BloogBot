// BIH.h
#pragma once

#include <vector>
#include <cstdint>
#include <limits>
#include "AABox.h"
#include "Ray.h"

class BIH
{
public:
    static const int MAX_STACK_SIZE = 64;

    struct StackNode
    {
        uint32_t node;
        float tnear;
        float tfar;
    };

public:
    BIH();

    // Nodes array and per-leaf object references.
    // Invariant: values stored in `objects` are indices that must map 1:1 to the
    // ModelInstance array (StaticMapTree::iTreeValues). They are either direct
    // indices [0..N-1] or original file IDs that are remapped to a dense range
    // during readFromFile(). All query methods (intersectRay/intersectPoint/QueryAABB)
    // will return indices already remapped to this dense [0..primCount()-1] range.
    std::vector<uint32_t> tree;
    std::vector<uint32_t> objects;
    G3D::AABox bounds;

    // Default copy and move operations work fine with vectors and AABox
    BIH(const BIH&) = default;
    BIH& operator=(const BIH&) = default;
    BIH(BIH&&) = default;
    BIH& operator=(BIH&&) = default;
    ~BIH() = default;

    // Build the BIH from primitives
    template<class BoundsFunc, class PrimArray>
    void build(PrimArray const& primitives, BoundsFunc& getBounds, uint32_t leafSize = 3, bool printStats = false);

    // File I/O
    bool readFromFile(FILE* rf);

    // Query methods
    uint32_t primCount() const { return m_primCountCached; }
    const G3D::AABox& getBounds() const { return bounds; }

    // Ray intersection
    template<typename RayCallback>
    void intersectRay(const G3D::Ray& r, RayCallback& intersectCallback,
        float& maxDist, bool stopAtFirst = true, bool ignoreM2Model = false) const;

    // Point intersection
    template<typename IsectCallback>
    void intersectPoint(const G3D::Vector3& p, IsectCallback& intersectCallback) const;

    // AABB query: gather object indices whose leaves are visited by AABox query
    bool QueryAABB(const G3D::AABox& query, uint32_t* outIndices, uint32_t& outCount, uint32_t maxCount) const;

    // Expose remap use and mapping for callers reading spawn indices from file
    // so they can map original file IDs to ModelInstance indices.
    bool usesRemap() const { return m_useRemap; }
    uint32_t mapObjectIndex(uint32_t original) const;

private:
    void init_empty();

    // If the file's object IDs are not a dense [0..N-1] range, we build a remap
    // from original ID -> compact index. When not needed, this stays disabled.
    std::vector<uint32_t> m_remap;       // size = maxOriginalId+1, value = compact or 0xFFFFFFFF
    bool m_useRemap = false;
    uint32_t m_primCountCached = 0;      // equals number of ModelInstance slots required
};

#include "BIH.inl"