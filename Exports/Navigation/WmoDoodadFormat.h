#pragma once

// WmoDoodadFormat.h - File format for WMO doodad placement data.
// Stores which M2 models are placed inside each WMO and where.
// Written by ExtractWmoDoodads (from MPQ), read by SceneCache::Extract.
//
// File layout (.doodads):
//   Header:  "WMDD_1.0"  (8 bytes magic)
//            uint32 setCount
//            uint32 spawnCount
//            uint32 nameTableSize
//   Sets:    DoodadSet[setCount]
//   Spawns:  DoodadSpawn[spawnCount]
//   Names:   char[nameTableSize]  (null-terminated M2 filenames)

#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <fstream>

namespace WmoDoodad
{

static constexpr char FILE_MAGIC[8] = { 'W','M','D','D','_','1','.','0' };

#pragma pack(push, 1)

struct FileHeader
{
    char magic[8];
    uint32_t setCount;
    uint32_t spawnCount;
    uint32_t nameTableSize;
};

// Matches WMO MODS chunk layout
struct DoodadSet
{
    char name[20];
    uint32_t startIndex;   // first spawn index in this set
    uint32_t count;        // number of spawns in this set
    uint32_t pad;
};

// Matches WMO MODD chunk layout
struct DoodadSpawn
{
    uint32_t nameOffset;   // byte offset into name table (24-bit in WMO, expanded here)
    float posX, posY, posZ;
    float rotX, rotY, rotZ, rotW;  // quaternion
    float scale;
};

#pragma pack(pop)

// Complete doodad data for one WMO
struct DoodadFile
{
    std::vector<DoodadSet> sets;
    std::vector<DoodadSpawn> spawns;
    std::vector<char> nameTable;  // concatenated null-terminated M2 filenames

    // Get the M2 model filename for a spawn
    const char* GetSpawnName(const DoodadSpawn& spawn) const
    {
        if (spawn.nameOffset >= nameTable.size()) return nullptr;
        return &nameTable[spawn.nameOffset];
    }

    bool Write(const std::string& path) const
    {
        std::ofstream out(path, std::ios::binary);
        if (!out) return false;

        FileHeader hdr{};
        std::memcpy(hdr.magic, FILE_MAGIC, 8);
        hdr.setCount = static_cast<uint32_t>(sets.size());
        hdr.spawnCount = static_cast<uint32_t>(spawns.size());
        hdr.nameTableSize = static_cast<uint32_t>(nameTable.size());
        out.write(reinterpret_cast<const char*>(&hdr), sizeof(hdr));

        if (!sets.empty())
            out.write(reinterpret_cast<const char*>(sets.data()),
                      sets.size() * sizeof(DoodadSet));
        if (!spawns.empty())
            out.write(reinterpret_cast<const char*>(spawns.data()),
                      spawns.size() * sizeof(DoodadSpawn));
        if (!nameTable.empty())
            out.write(nameTable.data(), nameTable.size());

        return out.good();
    }

    static bool Read(const std::string& path, DoodadFile& out)
    {
        std::ifstream in(path, std::ios::binary);
        if (!in) return false;

        FileHeader hdr{};
        in.read(reinterpret_cast<char*>(&hdr), sizeof(hdr));
        if (!in || std::memcmp(hdr.magic, FILE_MAGIC, 8) != 0)
            return false;

        out.sets.resize(hdr.setCount);
        out.spawns.resize(hdr.spawnCount);
        out.nameTable.resize(hdr.nameTableSize);

        if (hdr.setCount > 0)
            in.read(reinterpret_cast<char*>(out.sets.data()),
                    hdr.setCount * sizeof(DoodadSet));
        if (hdr.spawnCount > 0)
            in.read(reinterpret_cast<char*>(out.spawns.data()),
                    hdr.spawnCount * sizeof(DoodadSpawn));
        if (hdr.nameTableSize > 0)
            in.read(out.nameTable.data(), hdr.nameTableSize);

        return in.good() || in.eof();
    }
};

// Name normalization helpers (match MaNGOS vmap_extractor conventions)

// Get just the filename from a path (strip directory)
inline const char* GetPlainName(const char* path)
{
    const char* p = strrchr(path, '\\');
    if (p) return p + 1;
    p = strrchr(path, '/');
    if (p) return p + 1;
    return path;
}

// fixnamen: CamelCase the name (uppercase after non-alpha, lowercase after alpha)
inline void FixNameCase(char* name, size_t len)
{
    if (len < 3) return;
    for (size_t i = 0; i < len - 3; i++)
    {
        if (i > 0 && name[i] >= 'A' && name[i] <= 'Z' && isalpha(name[i - 1]))
            name[i] |= 0x20;
        else if ((i == 0 || !isalpha(name[i - 1])) && name[i] >= 'a' && name[i] <= 'z')
            name[i] &= ~0x20;
    }
    // extension in lowercase
    for (size_t i = len - 3; i < len; i++)
        name[i] |= 0x20;
}

// fixname2: replace spaces with underscores
inline void FixNameSpaces(char* name, size_t len)
{
    if (len < 3) return;
    for (size_t i = 0; i < len - 3; i++)
    {
        if (name[i] == ' ')
            name[i] = '_';
    }
}

// Normalize a doodad model path to match the vmaps/ filename convention.
// Input:  "World\\Azeroth\\Buildings\\Orgrimmar\\OrgrimmarWallGate.mdx"
// Output: "Orgrimmarwallgate.m2"
inline std::string NormalizeDoodadName(const char* rawPath)
{
    char buf[512];
    const char* plain = GetPlainName(rawPath);
    size_t len = strlen(plain);
    if (len >= sizeof(buf)) len = sizeof(buf) - 1;
    memcpy(buf, plain, len);
    buf[len] = '\0';

    FixNameCase(buf, len);
    FixNameSpaces(buf, len);

    // Convert .mdx/.mdl extension to .m2
    if (len > 3)
    {
        char* ext = &buf[len - 4];
        if (_stricmp(ext, ".mdx") == 0 || _stricmp(ext, ".mdl") == 0)
        {
            ext[1] = 'm';
            ext[2] = '2';
            ext[3] = '\0';
            len -= 1;
        }
    }

    return std::string(buf, len);
}

} // namespace WmoDoodad
