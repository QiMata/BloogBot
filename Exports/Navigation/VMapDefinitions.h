// VMapDefinitions.h - Complete VMAP definitions
#pragma once

#include "PhysicsEngine.h"
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>

namespace VMAP
{
    // vMaNGOS VMAP format for WoW 1.12.1
    constexpr char VMAP_MAGIC[] = "VMAP_7.0";

    // Simple helpers
    inline bool IsValidHeight(float h) { return h > PhysicsConstants::INVALID_HEIGHT; }

    constexpr float LIQUID_TILE_SIZE = (533.333f / 128.f);

    enum VMAPLoadResult
    {
        VMAP_LOAD_RESULT_ERROR,
        VMAP_LOAD_RESULT_OK,
        VMAP_LOAD_RESULT_IGNORED,
    };

    enum ModelFlags
    {
        MOD_M2 = 1,
        MOD_WORLDSPAWN = 1 << 1,
        MOD_HAS_BOUND = 1 << 2,
        MOD_NO_BREAK_LOS = 1 << 3
    };

    // Helper functions
    inline bool readChunk(FILE* rf, char* dest, const char* compare, uint32_t len)
    {
        if (fread(dest, 1, len, rf) != len) return false;
        return memcmp(dest, compare, len) == 0;
    }

    inline uint32_t floatToRawIntBits(float f)
    {
        union { uint32_t ival; float fval; } temp;
        temp.fval = f;
        return temp.ival;
    }

    inline float intBitsToFloat(uint32_t i)
    {
        union { uint32_t ival; float fval; } temp;
        temp.ival = i;
        return temp.fval;
    }

    enum LiquidTypeMask
    {
        MAP_LIQUID_TYPE_NO_WATER = 0x00,
        MAP_LIQUID_TYPE_MAGMA = 0x01,
        MAP_LIQUID_TYPE_OCEAN = 0x02,
        MAP_LIQUID_TYPE_SLIME = 0x04,
        MAP_LIQUID_TYPE_WATER = 0x08,
        MAP_LIQUID_TYPE_DARK_WATER = 0x10,
        MAP_LIQUID_TYPE_ALL_LIQUIDS = 0xFF
    };

    inline uint32_t GetLiquidMask(uint32_t liquidType)
    {
        switch (liquidType)
        {
        case 0: return MAP_LIQUID_TYPE_WATER;
        case 1: return MAP_LIQUID_TYPE_OCEAN;
        case 2: return MAP_LIQUID_TYPE_MAGMA;
        case 3: return MAP_LIQUID_TYPE_SLIME;
        default: return MAP_LIQUID_TYPE_WATER;
        }
    }

    // Human-readable name for liqType (0-3)
    inline const char* GetLiquidTypeName(uint32_t liquidType)
    {
        switch (liquidType)
        {
        case 0: return "Water";
        case 1: return "Ocean";
        case 2: return "Magma";
        case 3: return "Slime";
        default: return "Unknown";
        }
    }

    // New: WMO liquid entry IDs used by vmangos exporter (1=Water, 2=Ocean, 3=Magma, 4=Slime, 21=Naxx Slime)
    enum WmoLiquidEntry : uint32_t
    {
        WMO_LIQUID_ENTRY_NONE = 0,
        WMO_LIQUID_ENTRY_WATER = 1,
        WMO_LIQUID_ENTRY_OCEAN = 2,
        WMO_LIQUID_ENTRY_MAGMA = 3,
        WMO_LIQUID_ENTRY_SLIME = 4,
        WMO_LIQUID_ENTRY_NAXX_SLIME = 21
    };

    // Map entry IDs to filter masks (treat 21 as SLIME)
    inline uint32_t GetLiquidMaskFromEntry(uint32_t entry)
    {
        switch (entry)
        {
        case WMO_LIQUID_ENTRY_WATER: return MAP_LIQUID_TYPE_WATER;
        case WMO_LIQUID_ENTRY_OCEAN: return MAP_LIQUID_TYPE_OCEAN;
        case WMO_LIQUID_ENTRY_MAGMA: return MAP_LIQUID_TYPE_MAGMA;
        case WMO_LIQUID_ENTRY_SLIME: return MAP_LIQUID_TYPE_SLIME;
        case WMO_LIQUID_ENTRY_NAXX_SLIME: return MAP_LIQUID_TYPE_SLIME;
        default: return MAP_LIQUID_TYPE_WATER;
        }
    }

    // Human-readable name for entry IDs
    inline const char* GetLiquidEntryName(uint32_t entry)
    {
        switch (entry)
        {
        case WMO_LIQUID_ENTRY_WATER: return "Water";
        case WMO_LIQUID_ENTRY_OCEAN: return "Ocean";
        case WMO_LIQUID_ENTRY_MAGMA: return "Magma";
        case WMO_LIQUID_ENTRY_SLIME: return "Slime";
        case WMO_LIQUID_ENTRY_NAXX_SLIME: return "Slime (Naxxramas)";
        default: return "Unknown";
        }
    }

    // New: helper to detect if a liquid type is an entry-id (vmangos exporter), not a 0..3 index
    inline bool IsLiquidEntryId(uint32_t t)
    {
        return t == WMO_LIQUID_ENTRY_WATER ||
               t == WMO_LIQUID_ENTRY_OCEAN ||
               t == WMO_LIQUID_ENTRY_MAGMA ||
               t == WMO_LIQUID_ENTRY_SLIME ||
               t == WMO_LIQUID_ENTRY_NAXX_SLIME;
    }

    // New: unified helpers to get mask/name regardless of representation
    inline uint32_t GetLiquidMaskUnified(uint32_t t)
    {
        return IsLiquidEntryId(t) ? GetLiquidMaskFromEntry(t) : GetLiquidMask(t);
    }

    inline const char* GetLiquidNameUnified(uint32_t t)
    {
        return IsLiquidEntryId(t) ? GetLiquidEntryName(t) : GetLiquidTypeName(t);
    }

    // File name helpers
    inline std::string getMapFileName(uint32_t mapId)
    {
        char buffer[256];
        snprintf(buffer, sizeof(buffer), "%03u.vmtree", mapId);
        return std::string(buffer);
    }

    inline std::string getTileFileName(uint32_t mapId, uint32_t tileX, uint32_t tileY)
    {
        char buffer[256];
        snprintf(buffer, sizeof(buffer), "%03u_%02u_%02u.vmtile", mapId, tileX, tileY);
        return std::string(buffer);
    }

    // Coordinate conversion
    inline float convertPositionX(float x)
    {
        float const mid = 0.5f * 64.0f * 533.33333333f;
        return mid - x;
    }

    inline float convertPositionY(float y)
    {
        float const mid = 0.5f * 64.0f * 533.33333333f;
        return mid - y;
    }

    inline float convertPositionZ(float z)
    {
        return z;
    }

    // Tile packing/unpacking
    inline uint32_t packTileID(uint32_t tileX, uint32_t tileY)
    {
        return (tileX << 16) | tileY;
    }

    inline void unpackTileID(uint32_t ID, uint32_t& tileX, uint32_t& tileY)
    {
        tileX = (ID >> 16);
        tileY = (ID & 0xFFFF);
    }
}