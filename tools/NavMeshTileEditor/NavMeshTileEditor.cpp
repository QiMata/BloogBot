// NavMeshTileEditor — Slice B R2 of the physics-validated mmap pipeline.
//
// Modifies a baked .mmtile file in-place: for each polyref in --cull-polys,
// sets the corresponding dtPoly's area bits to 0 (DT_NULL_AREA). Detour's
// dtQueryFilter::isValidPoly excludes polys with area 0 from path queries
// by default, so the runtime can no longer route through them.
//
// Used downstream of NavMeshPhysicsValidator: validator identifies polyrefs
// that fail runtime physics, this tool culls them. Together they implement
// the "physics validation in our mmap generation" mandate — the bake's
// Recast-heuristic walkability gets corrected by the actual physics engine
// before deployment.
//
// CLI:
//   NavMeshTileEditor <mmtilePath> --cull-polys ref1,ref2,ref3...
//   NavMeshTileEditor <mmtilePath> --cull-polys-file <path>   (one polyref per line)
//   NavMeshTileEditor <mmtilePath> --dry-run                  (no write, just report)
//
// File format (per Westworld of Warcraft/docs/physics/MMAP_FORMAT.md):
//   bytes 0-19  : MmapTileHeader (mmapMagic, dtVersion, mmapVersion, size, usesLiquids)
//   bytes 20+   : dtMeshTile binary, starting with dtMeshHeader at offset 20
//
// dtMeshHeader (DetourNavMesh.h:261, ~100 bytes):
//   ints: magic, version, x, y, layer, userId, polyCount, vertCount,
//         maxLinkCount, detailMeshCount, detailVertCount, detailTriCount,
//         bvNodeCount, offMeshConCount, offMeshBase
//   floats: walkableHeight, walkableRadius, walkableClimb,
//           bmin[3], bmax[3], bvQuantFactor
//
// Layout after header:
//   verts[]:   vertCount × 12 bytes (3 floats)
//   polys[]:   polyCount × 32 bytes (dtPoly struct, areaAndtype at offset 31)
//
// PolyRef decoding (DT_POLYREF64, salt:16|tile:28|poly:20):
//   polyIdx = polyref & ((1ull << 20) - 1)

#include <cstdint>
#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <system_error>
#include <vector>

#include "DetourNavMesh.h"  // for sizeof(dtMeshHeader), sizeof(dtPoly)

namespace
{
    constexpr int MmapTileHeaderSize = 20;
    constexpr uint64_t PolyMask = ((uint64_t)1 << 20) - 1;  // DT_POLYREF64 poly bits

    void usage()
    {
        std::cerr <<
            "Usage:\n"
            "  NavMeshTileEditor <mmtilePath> --cull-polys ref1,ref2,...\n"
            "  NavMeshTileEditor <mmtilePath> --cull-polys-file <path>   (one polyref per line)\n"
            "  NavMeshTileEditor <mmtilePath> --cull-polyidx-range MIN,MAX  (cull polyIdx in [MIN,MAX])\n"
            "  NavMeshTileEditor <mmtilePath> --dry-run                  (report only, no write)\n";
    }

    std::vector<uint64_t> parsePolyRefList(const std::string& csv)
    {
        std::vector<uint64_t> out;
        std::stringstream ss(csv);
        std::string item;
        while (std::getline(ss, item, ','))
        {
            if (item.empty()) continue;
            try { out.push_back(std::stoull(item)); }
            catch (...) {
                std::cerr << "[NavMeshTileEditor] bad polyref token '" << item << "'\n";
            }
        }
        return out;
    }

    std::vector<uint64_t> parsePolyRefFile(const std::string& path)
    {
        std::vector<uint64_t> out;
        std::ifstream in(path);
        if (!in) {
            std::cerr << "[NavMeshTileEditor] cannot open polyrefs file: " << path << "\n";
            return out;
        }
        std::string line;
        while (std::getline(in, line))
        {
            if (line.empty() || line[0] == '#') continue;
            try { out.push_back(std::stoull(line)); }
            catch (...) {
                std::cerr << "[NavMeshTileEditor] bad polyref line '" << line << "'\n";
            }
        }
        return out;
    }
}

int main(int argc, char** argv)
{
    if (argc < 2) { usage(); return 2; }

    std::string mmtilePath = argv[1];
    std::vector<uint64_t> cullList;
    int rangeMin = -1, rangeMax = -1;
    bool dryRun = false;

    for (int i = 2; i < argc; ++i)
    {
        std::string a = argv[i];
        if (a == "--cull-polys" && i + 1 < argc)
            cullList = parsePolyRefList(argv[++i]);
        else if (a == "--cull-polys-file" && i + 1 < argc)
            cullList = parsePolyRefFile(argv[++i]);
        else if (a == "--cull-polyidx-range" && i + 1 < argc)
        {
            // Parse "MIN,MAX"
            std::string r = argv[++i];
            auto comma = r.find(',');
            if (comma == std::string::npos)
            {
                std::cerr << "[NavMeshTileEditor] --cull-polyidx-range expects MIN,MAX\n";
                return 2;
            }
            try
            {
                rangeMin = std::stoi(r.substr(0, comma));
                rangeMax = std::stoi(r.substr(comma + 1));
            }
            catch (...)
            {
                std::cerr << "[NavMeshTileEditor] bad --cull-polyidx-range value '" << r << "'\n";
                return 2;
            }
            if (rangeMin > rangeMax)
            {
                std::cerr << "[NavMeshTileEditor] --cull-polyidx-range MIN must be <= MAX\n";
                return 2;
            }
        }
        else if (a == "--dry-run")
            dryRun = true;
        else if (a == "--help" || a == "-h") { usage(); return 0; }
        else { std::cerr << "[NavMeshTileEditor] unknown arg: " << a << "\n"; usage(); return 2; }
    }

    if (cullList.empty() && rangeMin < 0)
    {
        std::cerr << "[NavMeshTileEditor] no polyrefs or polyidx-range to cull "
                     "(use --cull-polys / --cull-polys-file / --cull-polyidx-range)\n";
        return 2;
    }

    // Load the entire file into memory.
    std::ifstream in(mmtilePath, std::ios::binary | std::ios::ate);
    if (!in)
    {
        std::cerr << "[NavMeshTileEditor] cannot open " << mmtilePath << "\n";
        return 3;
    }
    std::streamsize fileSize = in.tellg();
    in.seekg(0);
    std::vector<uint8_t> buf(static_cast<size_t>(fileSize));
    if (!in.read(reinterpret_cast<char*>(buf.data()), fileSize))
    {
        std::cerr << "[NavMeshTileEditor] short read on " << mmtilePath << "\n";
        return 3;
    }
    in.close();

    if (buf.size() < MmapTileHeaderSize + sizeof(dtMeshHeader))
    {
        std::cerr << "[NavMeshTileEditor] file too small to contain mmap+navmesh headers\n";
        return 3;
    }

    // dtMeshHeader sits right after the 20-byte MmapTileHeader.
    const dtMeshHeader* header = reinterpret_cast<const dtMeshHeader*>(buf.data() + MmapTileHeaderSize);

    // Sanity: Detour writes the magic 'DNAV' (0x444E4156). On platforms where
    // the bytes are reordered or the file is corrupt, this check catches it
    // before we start writing into the wrong offsets.
    if (header->magic != DT_NAVMESH_MAGIC)
    {
        std::cerr << "[NavMeshTileEditor] bad dtMeshHeader magic 0x" << std::hex << header->magic
                  << " (expected 0x" << DT_NAVMESH_MAGIC << ")\n";
        return 3;
    }
    if (header->version != DT_NAVMESH_VERSION)
    {
        std::cerr << "[NavMeshTileEditor] dtMeshHeader version " << header->version
                  << " != expected " << DT_NAVMESH_VERSION << "\n";
        return 3;
    }

    const int polyCount = header->polyCount;
    const int vertCount = header->vertCount;
    std::cout << "[NavMeshTileEditor] tile (" << header->x << "," << header->y
              << ") layer=" << header->layer
              << " polys=" << polyCount << " verts=" << vertCount << "\n";

    // polys offset = MmapTileHeader + dtMeshHeader + verts (3 floats per vert).
    const size_t polysOffset = MmapTileHeaderSize
        + sizeof(dtMeshHeader)
        + static_cast<size_t>(vertCount) * 3 * sizeof(float);

    if (polysOffset + static_cast<size_t>(polyCount) * sizeof(dtPoly) > buf.size())
    {
        std::cerr << "[NavMeshTileEditor] computed polys end past file end "
                  << "(polysOffset=" << polysOffset
                  << " polyCount=" << polyCount
                  << " sizeof(dtPoly)=" << sizeof(dtPoly)
                  << " fileSize=" << buf.size() << ")\n";
        return 3;
    }

    dtPoly* polys = reinterpret_cast<dtPoly*>(buf.data() + polysOffset);

    int culled = 0, skippedOutOfRange = 0;

    // Range cull: zeros area+flags for every poly in [rangeMin, rangeMax].
    // Used for WMO-interior trap clusters where individual polyref probing
    // misses polys at slightly different XY/Z. Polygon indices in a Detour
    // tile are typically contiguous within a WMO, so a range catches the
    // whole cluster.
    if (rangeMin >= 0)
    {
        const int hi = rangeMax < polyCount ? rangeMax : polyCount - 1;
        for (int idx = rangeMin; idx <= hi; ++idx)
        {
            const unsigned char prevArea = polys[idx].getArea();
            const unsigned short prevFlags = polys[idx].flags;
            polys[idx].setArea(0);
            polys[idx].flags = 0;
            std::cout << "[NavMeshTileEditor]   range-cull polyIdx=" << idx
                      << " areaWas=" << static_cast<int>(prevArea)
                      << " flagsWas=0x" << std::hex << prevFlags << std::dec << "\n";
            ++culled;
        }
        if (rangeMax >= polyCount)
            std::cerr << "[NavMeshTileEditor]   note: --cull-polyidx-range MAX=" << rangeMax
                      << " clamped to polyCount-1=" << (polyCount - 1) << "\n";
    }

    for (uint64_t ref : cullList)
    {
        const uint64_t idx = ref & PolyMask;
        if (idx >= static_cast<uint64_t>(polyCount))
        {
            std::cerr << "[NavMeshTileEditor]   skip ref=" << ref
                      << " (polyIdx=" << idx << " >= polyCount=" << polyCount << ")\n";
            ++skippedOutOfRange;
            continue;
        }
        const unsigned char prevArea = polys[idx].getArea();
        const unsigned char polyType = polys[idx].getType();
        const unsigned short prevFlags = polys[idx].flags;

        // Cull strategy: zero BOTH area AND flags. Detour's default
        // dtQueryFilter::passFilter rejects polygons via the FLAGS
        // (`(poly->flags & m_includeFlags) != 0`), NOT via area. Setting
        // area=0 alone is a no-op against the default filter — empirically
        // verified by BRM live theory continuing to route through area=0
        // polys after the first cull pass. Zeroing flags exits the polygon
        // from any filter that doesn't whitelist flags=0 explicitly.
        // Area=0 is set as a belt-and-suspenders for filters that DO
        // check area + cost.
        polys[idx].setArea(0);
        polys[idx].flags = 0;

        std::cout << "[NavMeshTileEditor]   cull ref=" << ref
                  << " polyIdx=" << idx
                  << " areaWas=" << static_cast<int>(prevArea)
                  << " flagsWas=0x" << std::hex << prevFlags << std::dec
                  << " type=" << static_cast<int>(polyType) << "\n";
        ++culled;
    }

    std::cout << "[NavMeshTileEditor] culled=" << culled
              << " skipped=" << skippedOutOfRange
              << " requested=" << cullList.size() << "\n";

    if (dryRun)
    {
        std::cout << "[NavMeshTileEditor] --dry-run: not writing back\n";
        return 0;
    }

    // Write back atomically: write to <path>.tmp, fsync, rename.
    const std::string tmpPath = mmtilePath + ".tmp";
    {
        std::ofstream out(tmpPath, std::ios::binary | std::ios::trunc);
        if (!out)
        {
            std::cerr << "[NavMeshTileEditor] cannot open temp file " << tmpPath << "\n";
            return 4;
        }
        out.write(reinterpret_cast<const char*>(buf.data()), static_cast<std::streamsize>(buf.size()));
        if (!out)
        {
            std::cerr << "[NavMeshTileEditor] short write to " << tmpPath << "\n";
            return 4;
        }
    }

    // Replace the original. std::filesystem::rename does an atomic replace
    // on POSIX but MSVC's std::rename refuses to overwrite an existing file
    // (errno EEXIST=17). std::filesystem::rename routes through MoveFileEx
    // with REPLACE_EXISTING on Windows when the source/target are on the
    // same volume. Fall back to remove+rename if rename still fails.
    std::error_code ec;
    std::filesystem::rename(tmpPath, mmtilePath, ec);
    if (ec)
    {
        std::filesystem::remove(mmtilePath, ec);  // ignore "file not found"
        std::filesystem::rename(tmpPath, mmtilePath, ec);
        if (ec)
        {
            std::cerr << "[NavMeshTileEditor] rename " << tmpPath << " → " << mmtilePath
                      << " failed: " << ec.message() << "\n";
            return 4;
        }
    }

    std::cout << "[NavMeshTileEditor] wrote " << mmtilePath << "\n";
    return 0;
}
