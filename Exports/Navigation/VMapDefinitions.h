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

    // Unified sentinel for no-liquid level across VMAP and ADT
    constexpr float VMAP_INVALID_LIQUID_HEIGHT = -500.0f;
    inline bool IsValidLiquidLevel(float h) { return std::isfinite(h) && h > VMAP_INVALID_LIQUID_HEIGHT; }

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

    // Consolidated WMO liquid entry IDs (match GameData.Core.Enums.LiquidType)
    enum WmoLiquidEntry : uint32_t
    {
        WMO_LIQUID_ENTRY_WATER = 1,
        WMO_LIQUID_ENTRY_OCEAN = 2,
        WMO_LIQUID_ENTRY_MAGMA = 3,
        WMO_LIQUID_ENTRY_SLIME = 4,
        WMO_LIQUID_ENTRY_NAXXRAMAS_SLIME = 21
    };

    enum LiquidType : uint32_t
    {
        LIQUID_TYPE_NO_WATER = 0, 
        LIQUID_TYPE_WATER = 1,
        LIQUID_TYPE_OCEAN = 2,
        LIQUID_TYPE_MAGMA = 3,
        LIQUID_TYPE_SLIME = 4,
        LIQUID_TYPE_NAXXRAMAS_SLIME = 5
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
        case 0: return "None";
        case 1: return "Water";
        case 2: return "Ocean";
        case 3: return "Magma";
        case 4: return "Slime";
        case 5: return "Naxxramas Slime";
        default: return "Unknown";
        }
    }

    inline uint32_t GetLiquidMaskFromEntry(uint32_t entry)
    {
        switch (entry)
        {
        case WMO_LIQUID_ENTRY_WATER: return MAP_LIQUID_TYPE_WATER;
        case WMO_LIQUID_ENTRY_OCEAN: return MAP_LIQUID_TYPE_OCEAN;
        case WMO_LIQUID_ENTRY_MAGMA: return MAP_LIQUID_TYPE_MAGMA;
        case WMO_LIQUID_ENTRY_SLIME: return MAP_LIQUID_TYPE_SLIME;
        case WMO_LIQUID_ENTRY_NAXXRAMAS_SLIME: return MAP_LIQUID_TYPE_SLIME;
        default: return MAP_LIQUID_TYPE_NO_WATER;
        }
    }

    // Helper to detect if a liquid type is an entry-id (vmangos exporter), not a 0..3 index
    inline bool IsLiquidEntryId(uint32_t t)
    {
        return t == WMO_LIQUID_ENTRY_WATER ||
               t == WMO_LIQUID_ENTRY_OCEAN ||
               t == WMO_LIQUID_ENTRY_MAGMA ||
               t == WMO_LIQUID_ENTRY_SLIME ||
               t == WMO_LIQUID_ENTRY_NAXXRAMAS_SLIME;
    }

    // Unified helpers to get mask/name regardless of representation
    inline uint32_t GetLiquidMaskUnified(uint32_t t)
    {
        return GetLiquidMaskFromEntry(t);
    }

    inline uint32_t GetLiquidEnumUnified(uint32_t t, bool isVmap)
    {
        // If already a known WMO entry id, return as-is
        if (isVmap)
        {
            switch (t)
            {
                case 1: return LIQUID_TYPE_WATER;
                case 2: return LIQUID_TYPE_OCEAN;
                case 3: return LIQUID_TYPE_MAGMA;
                case 4: return LIQUID_TYPE_SLIME;
                case 21: return LIQUID_TYPE_NAXXRAMAS_SLIME;
                default: return LIQUID_TYPE_NO_WATER;
            }
        }

        // Accept ADT indices (0..3)
        switch (t)
        {
            case 0: return LIQUID_TYPE_NO_WATER;  // index 0
            case 1: return LIQUID_TYPE_MAGMA;  // index 1
            case 2: return LIQUID_TYPE_OCEAN;  // index 2
            case 4: return LIQUID_TYPE_SLIME;  // index 3
            case 8: return LIQUID_TYPE_WATER;
            default: return LIQUID_TYPE_NO_WATER;
        }
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