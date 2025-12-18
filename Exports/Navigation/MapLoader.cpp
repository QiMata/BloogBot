// MapLoader.cpp - Complete vMaNGOS-style map loader with separate load methods and detailed logging
#include "MapLoader.h"
#include "VMapDefinitions.h"
#include "CapsuleCollision.h"
#include "VMapLog.h" // added for logging
#include <fstream>
#include <iostream>
#include <iomanip>
#include <sstream>
#include <cmath>
#include <filesystem>
#include <cstring>
#include <algorithm>
#include <map>
#include <set>

using namespace MapFormat;

// Hole detection tables for terrain holes
static uint16_t holetab_h[4] = { 0x1111, 0x2222, 0x4444, 0x8888 };
static uint16_t holetab_v[4] = { 0x000F, 0x00F0, 0x0F00, 0xF000 };

// ==================== GridMap Implementation ====================

GridMap::~GridMap()
{
    unloadData();
}

bool GridMap::loadAreaData(FILE* in, uint32_t offset, uint32_t size)
{
    if (fseek(in, offset, SEEK_SET) != 0)
    {
        return false;
    }

    // Check for AREA header or raw data
    char peekBuffer[8];
    if (fread(peekBuffer, 1, 8, in) != 8)
    {
        return false;
    }

    fseek(in, offset, SEEK_SET); // Reset

    uint32_t possibleMagic = *reinterpret_cast<uint32_t*>(peekBuffer);
    uint32_t expectedAreaMagic = *reinterpret_cast<const uint32_t*>(MAP_AREA_MAGIC);

    if (possibleMagic == expectedAreaMagic)
    {
        // Has AREA header
        MapAreaHeader areaHeader;
        if (fread(&areaHeader, sizeof(MapAreaHeader), 1, in) != 1)
        {
            return false;
        }

        m_areaHeader = new MapAreaHeader(areaHeader);

        if (!(areaHeader.flags & MAP_AREA_NO_AREA))
        {
            m_areaMap = new uint16_t[16 * 16];
            if (fread(m_areaMap, sizeof(uint16_t), 16 * 16, in) != 16 * 16)
            {
                delete[] m_areaMap;
                m_areaMap = nullptr;
                return false;
            }
        }
    }
    else
    {
        if (size >= 16 * 16 * sizeof(uint16_t))
        {
            m_areaMap = new uint16_t[16 * 16];
            fseek(in, offset, SEEK_SET); // Reset to offset
            if (fread(m_areaMap, sizeof(uint16_t), 16 * 16, in) != 16 * 16)
            {
                delete[] m_areaMap;
                m_areaMap = nullptr;
                return false;
            }
        }
    }

    return true;
}

bool GridMap::loadHeightData(FILE* in, uint32_t offset, uint32_t size)
{
    if (fseek(in, offset, SEEK_SET) != 0)
    {
        return false;
    }

    // Peek at first 16 bytes to determine format
    char peekBuffer[16];
    if (fread(peekBuffer, 1, 16, in) != 16)
    {
        return false;
    }
    fseek(in, offset, SEEK_SET); // Reset position

    // Check if it's a MHGT header or raw data
    uint32_t possibleMagic = *reinterpret_cast<uint32_t*>(peekBuffer);
    uint32_t expectedHeightMagic = *reinterpret_cast<const uint32_t*>(MAP_HEIGHT_MAGIC);

    bool hasHeader = (possibleMagic == expectedHeightMagic);

    if (hasHeader)
    {
        // Read the header
        MapHeightHeader heightHeader;
        if (fread(&heightHeader, sizeof(MapHeightHeader), 1, in) != 1)
        {
            return false;
        }

        m_heightHeader = new MapHeightHeader(heightHeader);
        m_gridHeight = heightHeader.gridHeight;

        if (!(heightHeader.flags & MAP_HEIGHT_NO_HEIGHT))
        {
            // Read data based on flags
            if (heightHeader.flags & MAP_HEIGHT_AS_INT16)
            {
                m_uint16_V9 = new uint16_t[V9_SIZE_SQ];
                m_uint16_V8 = new uint16_t[V8_SIZE_SQ];

                if (fread(m_uint16_V9, sizeof(uint16_t), V9_SIZE_SQ, in) != V9_SIZE_SQ ||
                    fread(m_uint16_V8, sizeof(uint16_t), V8_SIZE_SQ, in) != V8_SIZE_SQ)
                {
                    delete[] m_uint16_V9;
                    delete[] m_uint16_V8;
                    m_uint16_V9 = nullptr;
                    m_uint16_V8 = nullptr;
                    return false;
                }

                m_gridIntHeightMultiplier = (heightHeader.gridMaxHeight - heightHeader.gridHeight) / 65535;
                m_gridGetHeight = &GridMap::getHeightFromUint16;
            }
            else if (heightHeader.flags & MAP_HEIGHT_AS_INT8)
            {
                m_uint8_V9 = new uint8_t[V9_SIZE_SQ];
                m_uint8_V8 = new uint8_t[V8_SIZE_SQ];

                if (fread(m_uint8_V9, sizeof(uint8_t), V9_SIZE_SQ, in) != V9_SIZE_SQ ||
                    fread(m_uint8_V8, sizeof(uint8_t), V8_SIZE_SQ, in) != V8_SIZE_SQ)
                {
                    delete[] m_uint8_V9;
                    delete[] m_uint8_V8;
                    m_uint8_V9 = nullptr;
                    m_uint8_V8 = nullptr;
                    return false;
                }

                m_gridIntHeightMultiplier = (heightHeader.gridMaxHeight - heightHeader.gridHeight) / 255;
                m_gridGetHeight = &GridMap::getHeightFromUint8;
            }
            else
            {
                m_V9 = new float[V9_SIZE_SQ];
                m_V8 = new float[V8_SIZE_SQ];

                if (fread(m_V9, sizeof(float), V9_SIZE_SQ, in) != V9_SIZE_SQ ||
                    fread(m_V8, sizeof(float), V8_SIZE_SQ, in) != V8_SIZE_SQ)
                {
                    delete[] m_V9;
                    delete[] m_V8;
                    m_V9 = nullptr;
                    m_V8 = nullptr;
                    return false;
                }

                m_gridGetHeight = &GridMap::getHeightFromFloat;
            }
        }
        else
        {
            m_gridGetHeight = &GridMap::getHeightFromFlat;
        }
    }
    else
    {
        size_t expectedV9Size = V9_SIZE_SQ * sizeof(float);
        size_t expectedV8Size = V8_SIZE_SQ * sizeof(float);
        size_t expectedTotalSize = expectedV9Size + expectedV8Size;

        // Allocate arrays
        m_V9 = new float[V9_SIZE_SQ];
        m_V8 = new float[V8_SIZE_SQ];

        for (int i = 0; i < V9_SIZE_SQ; i++)
            m_V9[i] = INVALID_HEIGHT;
        for (int i = 0; i < V8_SIZE_SQ; i++)
            m_V8[i] = INVALID_HEIGHT;

        // Read as much data as available
        fseek(in, offset, SEEK_SET); // Reset to offset
        size_t v9BytesToRead = std::min((size_t)size, expectedV9Size);
        size_t v9FloatsRead = fread(m_V9, sizeof(float), v9BytesToRead / sizeof(float), in);

        if (size > expectedV9Size)
        {
            size_t v8BytesToRead = std::min((size_t)(size - expectedV9Size), expectedV8Size);
            size_t v8FloatsRead = fread(m_V8, sizeof(float), v8BytesToRead / sizeof(float), in);
        }

        m_gridGetHeight = &GridMap::getHeightFromFloat;

        // Create a default header
        m_heightHeader = new MapHeightHeader();
        m_heightHeader->fourcc = *reinterpret_cast<const uint32_t*>(MAP_HEIGHT_MAGIC);
        m_heightHeader->flags = 0; // float format
        m_heightHeader->gridHeight = 0.0f;
        m_heightHeader->gridMaxHeight = 100.0f;
        m_gridHeight = 0.0f;

        // Analyze the data
        int validCount = 0, invalidCount = 0, zeroCount = 0;
        float minHeight = 100000.0f, maxHeight = -100000.0f;
        float sum = 0.0f;

        for (int i = 0; i < V9_SIZE_SQ; i++)
        {
            if (m_V9[i] > INVALID_HEIGHT && m_V9[i] < 10000.0f)
            {
                validCount++;
                if (m_V9[i] < minHeight) minHeight = m_V9[i];
                if (m_V9[i] > maxHeight) maxHeight = m_V9[i];
                sum += m_V9[i];

                if (m_V9[i] == 0.0f) zeroCount++;
            }
            else
            {
                invalidCount++;
            }
        }
    }

    return true;
}

bool GridMap::loadHolesData(FILE* in, uint32_t offset, uint32_t size)
{
    if (fseek(in, offset, SEEK_SET) != 0)
    {
        return false;
    }

    // FIX: Holes should be 64 uint16 values (8x8 grid), not 8
    const int HOLES_COUNT = 64;  // 8x8 grid
    if (size < HOLES_COUNT * sizeof(uint16_t))
    {
        return false;
    }

    m_holes = new uint16_t[HOLES_COUNT];
    if (fread(m_holes, sizeof(uint16_t), HOLES_COUNT, in) != HOLES_COUNT)
    {
        delete[] m_holes;
        m_holes = nullptr;
        return false;
    }

    return true;
}

bool GridMap::loadLiquidData(FILE* in, uint32_t offset, uint32_t size)
{
    if (fseek(in, offset, SEEK_SET) != 0)
    {
        return false;
    }

    // Check for MLIQ header or raw data
    char peekBuffer[8];
    if (fread(peekBuffer, 1, 8, in) != 8)
    {
        return false;
    }
    fseek(in, offset, SEEK_SET); // Reset

    uint32_t possibleMagic = *reinterpret_cast<uint32_t*>(peekBuffer);
    uint32_t expectedLiquidMagic = *reinterpret_cast<const uint32_t*>(MAP_LIQUID_MAGIC);

    if (possibleMagic == expectedLiquidMagic)
    {
        // Has MLIQ header
        MapLiquidHeader liquidHeader;
        if (fread(&liquidHeader, sizeof(MapLiquidHeader), 1, in) != 1)
        {
            return false;
        }

        m_liquidHeader = new MapLiquidHeader(liquidHeader);

        if (!(liquidHeader.flags & MAP_LIQUID_NO_TYPE))
        {
            uint32_t liquidSize = liquidHeader.width * liquidHeader.height;
            if (liquidSize > 0 && liquidSize < 1000000)  // Sanity check
            {
                m_liquidEntry = new uint16_t[16 * 16];
                m_liquidFlags = new uint8_t[16 * 16];

                if (fread(m_liquidEntry, sizeof(uint16_t), 16 * 16, in) != 16 * 16 ||
                    fread(m_liquidFlags, sizeof(uint8_t), 16 * 16, in) != 16 * 16)
                {
                    delete[] m_liquidEntry;
                    delete[] m_liquidFlags;
                    m_liquidEntry = nullptr;
                    m_liquidFlags = nullptr;
                    return false;
                }
            }
        }

        if (!(liquidHeader.flags & MAP_LIQUID_NO_HEIGHT))
        {
            uint32_t liquidHeightSize = liquidHeader.width * liquidHeader.height;
            if (liquidHeightSize > 0 && liquidHeightSize < 1000000)  // Sanity check
            {
                m_liquidHeight = new float[liquidHeightSize];
                if (fread(m_liquidHeight, sizeof(float), liquidHeightSize, in) != liquidHeightSize)
                {
                    delete[] m_liquidHeight;
                    m_liquidHeight = nullptr;
                    return false;
                }
            }
        }
    }

    return true;
}

bool GridMap::loadData(const std::string& filename)
{
    // Unload any existing data
    unloadData();

    // Check if file exists
    if (!std::filesystem::exists(filename))
    {
        return false;
    }

    // Open file using FILE* (vMaNGOS style)
    FILE* in = fopen(filename.c_str(), "rb");
    if (!in)
    {
        return false;
    }

    // Get file size
    fseek(in, 0, SEEK_END);
    long fileSize = ftell(in);
    fseek(in, 0, SEEK_SET);

    // Read header
    MapFileHeader header;
    if (fread(&header, sizeof(MapFileHeader), 1, in) != 1)
    {
        fclose(in);
        return false;
    }

    uint8_t* headerBytes = reinterpret_cast<uint8_t*>(&header);

    // Verify magic
    uint32_t expectedMapMagic = *reinterpret_cast<const uint32_t*>(MAP_MAGIC);
    uint32_t expectedVersionMagic = *reinterpret_cast<const uint32_t*>(MAP_VERSION_MAGIC);

    if (header.mapMagic != expectedMapMagic || header.versionMagic != expectedVersionMagic)
    {
        fclose(in);
        return false;
    }

    // Load each section using separate methods (vMaNGOS style)
    bool success = true;

    // Load area data
    if (header.areaMapOffset > 0 && header.areaMapSize > 0)
    {
        if (!loadAreaData(in, header.areaMapOffset, header.areaMapSize))
        {
            success = false;
        }
    }

    // Load holes data
    if (success && header.holesOffset > 0 && header.holesSize > 0)
    {
        if (!loadHolesData(in, header.holesOffset, header.holesSize))
        {
            std::cout << "[GridMap] ERROR: Failed to load holes data!" << std::endl;
        }
    }

    // Load height data
    if (success && header.heightMapOffset > 0)
    {
        if (header.heightMapSize == 0)
        {
            // Create a minimal height header for flat terrain
            m_heightHeader = new MapHeightHeader();
            m_heightHeader->fourcc = *reinterpret_cast<const uint32_t*>(MAP_HEIGHT_MAGIC);
            m_heightHeader->flags = MAP_HEIGHT_NO_HEIGHT;
            m_heightHeader->gridHeight = 0.0f;
            m_heightHeader->gridMaxHeight = 0.0f;
            m_gridHeight = 0.0f;
            m_gridGetHeight = &GridMap::getHeightFromFlat;
        }
        else if (!loadHeightData(in, header.heightMapOffset, header.heightMapSize))
        {
            std::cout << "[GridMap] ERROR: Failed to load height data!" << std::endl;
            success = false;
        }
    }
    else
    {
        // Create a minimal height header for flat terrain
        m_heightHeader = new MapHeightHeader();
        m_heightHeader->fourcc = *reinterpret_cast<const uint32_t*>(MAP_HEIGHT_MAGIC);
        m_heightHeader->flags = MAP_HEIGHT_NO_HEIGHT;
        m_heightHeader->gridHeight = 0.0f;
        m_heightHeader->gridMaxHeight = 0.0f;
        m_gridHeight = 0.0f;
        m_gridGetHeight = &GridMap::getHeightFromFlat;
    }

    // Load liquid data
    if (success && header.liquidMapOffset > 0 && header.liquidMapSize > 0)
    {
        if (!loadLiquidData(in, header.liquidMapOffset, header.liquidMapSize))
        {
            std::cout << "[GridMap] WARNING: Failed to load liquid data (non-fatal)" << std::endl;
        }
    }

    fclose(in);

    return success;
}

void GridMap::unloadData()
{
    delete m_heightHeader;
    m_heightHeader = nullptr;

    delete m_liquidHeader;
    m_liquidHeader = nullptr;

    delete m_areaHeader;
    m_areaHeader = nullptr;

    delete[] m_V9;
    m_V9 = nullptr;

    delete[] m_V8;
    m_V8 = nullptr;

    delete[] m_liquidHeight;
    m_liquidHeight = nullptr;

    delete[] m_liquidFlags;
    m_liquidFlags = nullptr;

    delete[] m_liquidEntry;
    m_liquidEntry = nullptr;

    delete[] m_areaMap;
    m_areaMap = nullptr;

    delete[] m_holes;
    m_holes = nullptr;

    m_gridGetHeight = nullptr;
}

float GridMap::getHeight(float x, float y) const
{
    if (!m_gridGetHeight)
    {
        return INVALID_HEIGHT;
    }

    float height = (this->*m_gridGetHeight)(x, y);

    return height;
}

float GridMap::getHeightFromFloat(float x, float y) const
{
    if (!m_V9 || !m_V8)
    {
        return INVALID_HEIGHT;
    }

    // vMaNGOS transformation - this expects WORLD coordinates
    float orig_x = x;
    float orig_y = y;
    x = MAP_RESOLUTION * (32 - x / SIZE_OF_GRIDS);
    y = MAP_RESOLUTION * (32 - y / SIZE_OF_GRIDS);

    int x_int = (int)x;
    int y_int = (int)y;
    float x_frac = x - x_int;  // Get fractional part
    float y_frac = y - y_int;  // Get fractional part
    x_int &= (MAP_RESOLUTION - 1);  // Wrap to 0-127 range
    y_int &= (MAP_RESOLUTION - 1);  // Wrap to 0-127 range

    if (isHole(x_int, y_int))
    {
        return INVALID_HEIGHT;
    }

    // Debug: Show the actual array indices we're about to use
    int v9_idx1 = x_int * 129 + y_int;
    int v9_idx2 = (x_int + 1) * 129 + y_int;
    int v9_idx3 = x_int * 129 + (y_int + 1);
    int v9_idx4 = (x_int + 1) * 129 + (y_int + 1);
    int v8_idx = x_int * 128 + y_int;

    float a, b, c;

    // Select triangle and calculate coefficients
    if (x_frac + y_frac < 1)
    {
        if (x_frac > y_frac)
        {
            // Triangle 1 (h1, h2, h5 points)
            float h1 = m_V9[v9_idx1];
            float h2 = m_V9[v9_idx2];
            float h5 = m_V8[v8_idx];
            a = h2 - h1;
            b = h5 - h1 - h2;
            c = h1;
        }
        else
        {
            // Triangle 2 (h1, h3, h5 points)
            float h1 = m_V9[v9_idx1];
            float h3 = m_V9[v9_idx3];
            float h5 = m_V8[v8_idx];
            a = h5 - h1 - h3;
            b = h3 - h1;
            c = h1;
        }
    }
    else
    {
        if (x_frac > y_frac)
        {
            // Triangle 3 (h2, h4, h5 points)
            float h2 = m_V9[v9_idx2];
            float h4 = m_V9[v9_idx4];
            float h5 = m_V8[v8_idx];
            a = h2 + h4 - h5;
            b = h4 - h2;
            c = h5 - h4;
        }
        else
        {
            // Triangle 4 (h3, h4, h5 points)
            float h3 = m_V9[v9_idx3];
            float h4 = m_V9[v9_idx4];
            float h5 = m_V8[v8_idx];
            a = h4 - h3;
            b = h3 + h4 - h5;
            c = h5 - h4;
        }
    }

    // Calculate height
    float result = a * x_frac + b * y_frac + c;

    return result;
}

float GridMap::getHeightFromUint16(float x, float y) const
{
    if (!m_uint16_V9 || !m_uint16_V8)
    {
        return m_gridHeight;
    }

    // FIX: Don't transform coordinates - they're already tile-local
    float grid_x = x / GRID_PART_SIZE;
    float grid_y = y / GRID_PART_SIZE;

    int x_int = (int)grid_x;
    int y_int = (int)grid_y;
    float x_frac = grid_x - x_int;
    float y_frac = grid_y - y_int;

    if (x_int < 0 || x_int >= V8_SIZE || y_int < 0 || y_int >= V8_SIZE)
    {
        return INVALID_HEIGHT;
    }

    if (isHole(x_int, y_int))
    {
        return INVALID_HEIGHT;
    }

    // FIX: CORRECTED INDEXING - vMaNGOS style
    int idx0 = x_int * V9_SIZE + y_int;
    int idx1 = (x_int + 1) * V9_SIZE + y_int;
    int idx2 = x_int * V9_SIZE + (y_int + 1);
    int idx3 = (x_int + 1) * V9_SIZE + (y_int + 1);

    // Convert uint16 to float heights
    float h0 = m_uint16_V9[idx0] * m_gridIntHeightMultiplier + m_gridHeight;
    float h1 = m_uint16_V9[idx1] * m_gridIntHeightMultiplier + m_gridHeight;
    float h2 = m_uint16_V9[idx2] * m_gridIntHeightMultiplier + m_gridHeight;
    float h3 = m_uint16_V9[idx3] * m_gridIntHeightMultiplier + m_gridHeight;

    // Bilinear interpolation
    float h_top = h0 + (h1 - h0) * x_frac;
    float h_bottom = h2 + (h3 - h2) * x_frac;
    float result = h_top + (h_bottom - h_top) * y_frac;

    return result;
}

float GridMap::getHeightFromUint8(float x, float y) const
{
    if (!m_uint8_V9 || !m_uint8_V8)
    {
        return m_gridHeight;
    }

    // FIX: Don't transform coordinates - they're already tile-local
    float grid_x = x / GRID_PART_SIZE;
    float grid_y = y / GRID_PART_SIZE;

    int x_int = (int)grid_x;
    int y_int = (int)grid_y;
    float x_frac = grid_x - x_int;
    float y_frac = grid_y - y_int;

    if (x_int < 0 || x_int >= V8_SIZE || y_int < 0 || y_int >= V8_SIZE)
    {
        return INVALID_HEIGHT;
    }

    if (isHole(x_int, y_int))
    {
        return INVALID_HEIGHT;
    }

    // FIX: CORRECTED INDEXING - vMaNGOS style
    int idx0 = x_int * V9_SIZE + y_int;
    int idx1 = (x_int + 1) * V9_SIZE + y_int;
    int idx2 = x_int * V9_SIZE + (y_int + 1);
    int idx3 = (x_int + 1) * V9_SIZE + (y_int + 1);

    // Convert uint8 to float heights
    float h0 = m_uint8_V9[idx0] * m_gridIntHeightMultiplier + m_gridHeight;
    float h1 = m_uint8_V9[idx1] * m_gridIntHeightMultiplier + m_gridHeight;
    float h2 = m_uint8_V9[idx2] * m_gridIntHeightMultiplier + m_gridHeight;
    float h3 = m_uint8_V9[idx3] * m_gridIntHeightMultiplier + m_gridHeight;

    // Bilinear interpolation
    float h_top = h0 + (h1 - h0) * x_frac;
    float h_bottom = h2 + (h3 - h2) * x_frac;
    float result = h_top + (h_bottom - h_top) * y_frac;

    return result;
}

float GridMap::getHeightFromFlat(float /*x*/, float /*y*/) const
{
    return m_gridHeight;
}

bool GridMap::isHole(int row, int col) const
{
    if (!m_holes)
        return false;

    int cellRow = row / 8;     // 8 squares per cell
    int cellCol = col / 8;
    int holeRow = row % 8 / 2;
    int holeCol = (col - (cellCol * 8)) / 2;

    if (cellRow >= 8 || cellCol >= 8)
        return false;

    uint16_t hole = m_holes[cellRow * 8 + cellCol];  // Row-major order
    bool isHolePos = (hole & holetab_h[holeCol] & holetab_v[holeRow]) != 0;

    return isHolePos;
}

float GridMap::getLiquidLevel(float x, float y) const
{
    if (!m_liquidHeader || !m_liquidHeight)
        return VMAP::VMAP_INVALID_LIQUID_HEIGHT; // unified sentinel

    x = MAP_RESOLUTION * (32 - x / SIZE_OF_GRIDS);
    y = MAP_RESOLUTION * (32 - y / SIZE_OF_GRIDS);

    int cx_int = ((int)x & (MAP_RESOLUTION - 1)) - m_liquidHeader->offsetY;
    int cy_int = ((int)y & (MAP_RESOLUTION - 1)) - m_liquidHeader->offsetX;

    if (cx_int < 0 || cx_int >= m_liquidHeader->height)
        return VMAP::VMAP_INVALID_LIQUID_HEIGHT;

    if (cy_int < 0 || cy_int >= m_liquidHeader->width)
        return VMAP::VMAP_INVALID_LIQUID_HEIGHT;

    return m_liquidHeight[cx_int * m_liquidHeader->width + cy_int];
}

uint8_t GridMap::getLiquidType(float x, float y) const
{
    if (!m_liquidFlags)
        return m_liquidHeader ? static_cast<uint8_t>(m_liquidHeader->liquidType) : VMAP::MAP_LIQUID_TYPE_NO_WATER;

    x = 16 * (32 - x / SIZE_OF_GRIDS);
    y = 16 * (32 - y / SIZE_OF_GRIDS);
    int lx = (int)x & 15;
    int ly = (int)y & 15;
    return m_liquidFlags[lx * 16 + ly];
}

uint16_t GridMap::getArea(float x, float y) const
{
    if (!m_areaMap)
        return m_areaHeader ? m_areaHeader->gridArea : 0;

    x = 16 * (32 - x / SIZE_OF_GRIDS);
    y = 16 * (32 - y / SIZE_OF_GRIDS);
    int lx = (int)x & 15;
    int ly = (int)y & 15;
    return m_areaMap[lx * 16 + ly];
}

// Helper to sample V9 heights regardless of storage type
float GridMap::sampleV9Height(int xi, int yi) const
{
    if (xi < 0 || xi >= V9_SIZE || yi < 0 || yi >= V9_SIZE)
        return INVALID_HEIGHT;

    int idx = xi * V9_SIZE + yi;
    if (m_V9)
        return m_V9[idx];
    if (m_uint16_V9)
        return m_uint16_V9[idx] * m_gridIntHeightMultiplier + m_gridHeight;
    if (m_uint8_V9)
        return m_uint8_V9[idx] * m_gridIntHeightMultiplier + m_gridHeight;
    return INVALID_HEIGHT;
}

// Helper to sample V8 (center) heights regardless of storage type
float GridMap::sampleV8Center(int xi, int yi) const
{
    if (xi < 0 || xi >= V8_SIZE || yi < 0 || yi >= V8_SIZE)
        return INVALID_HEIGHT;
    int idx = xi * V8_SIZE + yi;
    if (m_V8)
        return m_V8[idx];
    if (m_uint16_V8)
        return (m_uint16_V8[idx] * m_gridIntHeightMultiplier + m_gridHeight);
    if (m_uint8_V8)
        return (m_uint8_V8[idx] * m_gridIntHeightMultiplier + m_gridHeight);
    return INVALID_HEIGHT;
}

// New: compute surface normal at world position (returns false if invalid / hole)
bool GridMap::getNormal(float x, float y, float& nx, float& ny, float& nz) const
{
    // Only support float height for now (expand as needed)
    if (!m_V9 || !m_V8)
        return false;

    // vMaNGOS transformation - expects WORLD coordinates
    float tx = MAP_RESOLUTION * (32 - x / SIZE_OF_GRIDS);
    float ty = MAP_RESOLUTION * (32 - y / SIZE_OF_GRIDS);

    int x_int = (int)tx;
    int y_int = (int)ty;
    float x_frac = tx - x_int;
    float y_frac = ty - y_int;
    x_int &= (MAP_RESOLUTION - 1);
    y_int &= (MAP_RESOLUTION - 1);

    if (isHole(x_int, y_int))
        return false;

    // Find triangle vertices in world coordinates
    // Each grid square is split into two triangles (see getHeightFromFloat)
    // Compute the three vertices of the triangle containing (x, y)
    float wx0 = x_int * GRID_PART_SIZE;
    float wy0 = y_int * GRID_PART_SIZE;
    float wx1 = (x_int + 1) * GRID_PART_SIZE;
    float wy1 = (y_int + 1) * GRID_PART_SIZE;

    // Indices for V9 and V8
    int v9_idx1 = x_int * 129 + y_int;
    int v9_idx2 = (x_int + 1) * 129 + y_int;
    int v9_idx3 = x_int * 129 + (y_int + 1);
    int v9_idx4 = (x_int + 1) * 129 + (y_int + 1);
    int v8_idx = x_int * 128 + y_int;

    CapsuleCollision::Vec3 a, b, c;
    if (x_frac + y_frac < 1)
    {
        if (x_frac > y_frac)
        {
            // Triangle 1: (h1, h2, h5)
            a = CapsuleCollision::Vec3(wx0, wy0, m_V9[v9_idx1]);
            b = CapsuleCollision::Vec3(wx1, wy0, m_V9[v9_idx2]);
            c = CapsuleCollision::Vec3((wx0 + wx1) * 0.5f, (wy0 + wy1) * 0.5f, m_V8[v8_idx]);
        }
        else
        {
            // Triangle 2: (h1, h3, h5)
            a = CapsuleCollision::Vec3(wx0, wy0, m_V9[v9_idx1]);
            b = CapsuleCollision::Vec3(wx0, wy1, m_V9[v9_idx3]);
            c = CapsuleCollision::Vec3((wx0 + wx1) * 0.5f, (wy0 + wy1) * 0.5f, m_V8[v8_idx]);
        }
    }
    else
    {
        if (x_frac > y_frac)
        {
            // Triangle 3: (h2, h4, h5)
            a = CapsuleCollision::Vec3(wx1, wy0, m_V9[v9_idx2]);
            b = CapsuleCollision::Vec3(wx1, wy1, m_V9[v9_idx4]);
            c = CapsuleCollision::Vec3((wx0 + wx1) * 0.5f, (wy0 + wy1) * 0.5f, m_V8[v8_idx]);
        }
        else
        {
            // Triangle 4: (h3, h4, h5)
            a = CapsuleCollision::Vec3(wx0, wy1, m_V9[v9_idx3]);
            b = CapsuleCollision::Vec3(wx1, wy1, m_V9[v9_idx4]);
            c = CapsuleCollision::Vec3((wx0 + wx1) * 0.5f, (wy0 + wy1) * 0.5f, m_V8[v8_idx]);
        }
    }

    // Compute normal (right-handed, z-up)
    CapsuleCollision::Vec3 ab = b - a;
    CapsuleCollision::Vec3 ac = c - a;
    CapsuleCollision::Vec3 n = CapsuleCollision::Vec3::cross(ab, ac);
    n = CapsuleCollision::Vec3::normalizeSafe(n, CapsuleCollision::Vec3(0, 0, 1));
    nx = n.x;
    ny = n.y;
    nz = n.z;
    return true;
}

// ---- New: terrain triangle extraction ----
void GridMap::getTerrainTriangles(std::vector<TerrainTriangle>& out) const
{
    // Support all storage formats; require that any V9/V8 exist
    if (!(m_V9 || m_uint16_V9 || m_uint8_V9) || !(m_V8 || m_uint16_V8 || m_uint8_V8))
        return;

    auto pushUpward = [&](float ax, float ay, float az,
                          float bx, float by, float bz,
                          float cx, float cy, float cz) {
        // Compute normal z to ensure upward orientation
        float abx = bx - ax, aby = by - ay, abz = bz - az;
        float acx = cx - ax, acy = cy - ay, acz = cz - az;
        float nx = aby * acz - abz * acy;
        float ny = abz * acx - abx * acz;
        float nz = abx * acy - aby * acx;
        if (nz < 0.0f)
            out.push_back({ ax, ay, az, cx, cy, cz, bx, by, bz }); // swap to flip
        else
            out.push_back({ ax, ay, az, bx, by, bz, cx, cy, cz });
    };

    // Iterate map squares (128x128). For each square, emit two tris unless it's a hole.
    for (int xi = 0; xi < V8_SIZE; ++xi)
    {
        for (int yi = 0; yi < V8_SIZE; ++yi)
        {
            if (isHole(xi, yi))
                continue;
            float wx0 = xi * GRID_PART_SIZE;
            float wy0 = yi * GRID_PART_SIZE;
            float wx1 = (xi + 1) * GRID_PART_SIZE;
            float wy1 = (yi + 1) * GRID_PART_SIZE;

            // Heights sampled using storage-agnostic helpers
            float h1 = sampleV9Height(xi,     yi    );
            float h2 = sampleV9Height(xi + 1, yi    );
            float h3 = sampleV9Height(xi,     yi + 1);
            float h4 = sampleV9Height(xi + 1, yi + 1);
            float h5 = sampleV8Center(xi, yi);
            if (h1 <= INVALID_HEIGHT || h2 <= INVALID_HEIGHT ||
                h3 <= INVALID_HEIGHT || h4 <= INVALID_HEIGHT ||
                h5 <= INVALID_HEIGHT)
            {
                continue;
            }

            float cx = (wx0 + wx1) * 0.5f;
            float cy = (wy0 + wy1) * 0.5f;
            // Emit with ensured upward normal
            pushUpward(wx0, wy0, h1, wx1, wy0, h2, cx, cy, h5);
            pushUpward(wx0, wy0, h1, wx0, wy1, h3, cx, cy, h5);
            pushUpward(wx1, wy0, h2, wx1, wy1, h4, cx, cy, h5);
            pushUpward(wx0, wy1, h3, wx1, wy1, h4, cx, cy, h5);
        }
    }
}

void GridMap::getTerrainTrianglesInAABB(float minX, float minY, float maxX, float maxY,
                                        std::vector<TerrainTriangle>& out) const
{
    // Support all storage formats; require that any V9/V8 exist
    if (!(m_V9 || m_uint16_V9 || m_uint8_V9) || !(m_V8 || m_uint16_V8 || m_uint8_V8))
        return;

    auto pushUpward = [&](float ax, float ay, float az,
                          float bx, float by, float bz,
                          float cx, float cy, float cz) {
        float abx = bx - ax, aby = by - ay, abz = bz - az;
        float acx = cx - ax, acy = cy - ay, acz = cz - az;
        float nz = abx * acy - aby * acx;
        if (nz < 0.0f)
            out.push_back({ ax, ay, az, cx, cy, cz, bx, by, bz });
        else
            out.push_back({ ax, ay, az, bx, by, bz, cx, cy, cz });
    };

    // Clamp to tile-local bounds [0, GRID_SIZE]
    float x0 = std::max(0.0f, minX);
    float y0 = std::max(0.0f, minY);
    float x1 = std::min(GRID_SIZE, maxX);
    float y1 = std::min(GRID_SIZE, maxY);
    if (x0 >= x1 || y0 >= y1) return;

    int xi0 = std::max(0, (int)std::floor(x0 / GRID_PART_SIZE));
    int yi0 = std::max(0, (int)std::floor(y0 / GRID_PART_SIZE));
    int xi1 = std::min(V8_SIZE - 1, (int)std::floor(x1 / GRID_PART_SIZE));
    int yi1 = std::min(V8_SIZE - 1, (int)std::floor(y1 / GRID_PART_SIZE));

    for (int xi = xi0; xi <= xi1; ++xi)
    {
        for (int yi = yi0; yi <= yi1; ++yi)
        {
            if (isHole(xi, yi))
                continue;

            float wx0 = xi * GRID_PART_SIZE;
            float wy0 = yi * GRID_PART_SIZE;
            float wx1 = (xi + 1) * GRID_PART_SIZE;
            float wy1 = (yi + 1) * GRID_PART_SIZE;

            // Heights sampled using storage-agnostic helpers
            float h1 = sampleV9Height(xi,     yi    );
            float h2 = sampleV9Height(xi + 1, yi    );
            float h3 = sampleV9Height(xi,     yi + 1);
            float h4 = sampleV9Height(xi + 1, yi + 1);
            float h5 = sampleV8Center(xi, yi);
            if (h1 <= INVALID_HEIGHT || h2 <= INVALID_HEIGHT ||
                h3 <= INVALID_HEIGHT || h4 <= INVALID_HEIGHT ||
                h5 <= INVALID_HEIGHT)
            {
                continue;
            }

            auto overlaps = [&](float ax, float ay, float bx, float by, float cx, float cy) {
                float minTx = std::min({ ax, bx, cx });
                float minTy = std::min({ ay, by, cy });
                float maxTx = std::max({ ax, bx, cx });
                float maxTy = std::max({ ay, by, cy });
                return !(maxTx < minX || maxTy < minY || minTx > maxX || minTy > maxY);
            };

            float cx = (wx0 + wx1) * 0.5f;
            float cy = (wy0 + wy1) * 0.5f;
            if (overlaps(wx0, wy0, wx1, wy0, cx, cy))
                pushUpward(wx0, wy0, h1, wx1, wy0, h2, cx, cy, h5);
            if (overlaps(wx0, wy0, wx0, wy1, cx, cy))
                pushUpward(wx0, wy0, h1, wx0, wy1, h3, cx, cy, h5);
            if (overlaps(wx1, wy0, wx1, wy1, cx, cy))
                pushUpward(wx1, wy0, h2, wx1, wy1, h4, cx, cy, h5);
            if (overlaps(wx0, wy1, wx1, wy1, cx, cy))
                pushUpward(wx0, wy1, h3, wx1, wy1, h4, cx, cy, h5);
        }
    }
}

// ==================== MapLoader Implementation ====================

MapLoader::MapLoader()
{

}

MapLoader::~MapLoader()
{
    Shutdown();
}

bool MapLoader::Initialize(const std::string& dataPath)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_initialized)
    {
        return true;
    }

    m_dataPath = dataPath;
    if (!m_dataPath.empty() && m_dataPath.back() != '/' && m_dataPath.back() != '\\')
        m_dataPath += '/';

    m_initialized = true;

    return true;
}

void MapLoader::Shutdown()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    m_loadedTiles.clear();
    m_initialized = false;
}

std::string MapLoader::getMapFileName(uint32_t mapId, uint32_t x, uint32_t y) const
{
    std::stringstream ss;
    ss << m_dataPath << std::setfill('0') << std::setw(3) << mapId
        << std::setw(2) << x << std::setw(2) << y << ".map";
    return ss.str();
}

uint64_t MapLoader::makeKey(uint32_t mapId, uint32_t x, uint32_t y) const
{
    return ((uint64_t)mapId << 32) | ((uint64_t)x << 16) | (uint64_t)y;
}

void MapLoader::worldToGridCoords(float worldX, float worldY, uint32_t& gridX, uint32_t& gridY) const
{
    gridX = static_cast<uint32_t>((CENTER_GRID_ID - worldY / GRID_SIZE));
    gridY = static_cast<uint32_t>((CENTER_GRID_ID - worldX / GRID_SIZE));
}

bool MapLoader::LoadMapTile(uint32_t mapId, uint32_t x, uint32_t y)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    uint64_t key = makeKey(mapId, x, y);
    if (m_loadedTiles.find(key) != m_loadedTiles.end())
    {
        return true;
    }

    std::string filename = getMapFileName(mapId, x, y);

    if (!std::filesystem::exists(filename))
    {
        return false;
    }

    auto gridMap = std::make_unique<GridMap>();
    if (!gridMap->loadData(filename))
    {
        return false;
    }

    m_loadedTiles[key] = std::move(gridMap);

    return true;
}

void MapLoader::UnloadMapTile(uint32_t mapId, uint32_t x, uint32_t y)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    m_loadedTiles.erase(makeKey(mapId, x, y));
}

void MapLoader::UnloadAllTiles()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    m_loadedTiles.clear();
}

void MapLoader::computeTileOrigin(uint32_t gridY, uint32_t gridX, float& originX, float& originY) const
{
    // gridY corresponds to world X axis inversion; gridX to world Y axis inversion
    float tileMaxWorldX = (CENTER_GRID_ID - static_cast<float>(gridY)) * GRID_SIZE;
    float tileMaxWorldY = (CENTER_GRID_ID - static_cast<float>(gridX)) * GRID_SIZE;
    originX = tileMaxWorldX - GRID_SIZE;
    originY = tileMaxWorldY - GRID_SIZE;
}

void MapLoader::worldAABBToTileLocal(float minX, float minY, float maxX, float maxY,
                                     float originX, float originY,
                                     float& localMinX, float& localMinY, float& localMaxX, float& localMaxY) const
{
    localMinX = std::max(0.0f, minX - originX);
    localMinY = std::max(0.0f, minY - originY);
    localMaxX = std::min(GRID_SIZE, maxX - originX);
    localMaxY = std::min(GRID_SIZE, maxY - originY);
}

float MapLoader::GetHeight(uint32_t mapId, float x, float y)
{
    // Detailed diagnostics: capture grid coords and tile origin for correlation with GetTerrainTriangles
    uint32_t gridX, gridY;
    worldToGridCoords(x, y, gridX, gridY);

    int cellX, cellY; float h, h1, h2, h3, h4, h5;
    bool ok = SampleHeightAndSquare(mapId, x, y, cellX, cellY, h, h1, h2, h3, h4, h5);
    if (!ok)
    {
        // std::cout << std::fixed << std::setprecision(3)
        //           << "[TerrainHeight] map=" << mapId
        //           << " x=" << x << " y=" << y
        //           << " gridX=" << gridX << " gridY=" << gridY
        //           << " status=NO_HEIGHT" << std::endl;
        return INVALID_HEIGHT;
    }

    // Recompute fractional cell coords (same logic as SampleHeightAndSquare) for logging
    float fracX, fracY; int tmpCellX, tmpCellY;
    worldToCellIndices(x, y, tmpCellX, tmpCellY, fracX, fracY);
    // (cellX should match tmpCellX; cellY should match tmpCellY) but we log both for verification

    // Compute tile origin (lower bound world corner) to build world-space triangle vertices
    float tileOriginX, tileOriginY; computeTileOrigin(gridY, gridX, tileOriginX, tileOriginY);

    // Derive local square (tile-local) coordinate extents
    // Each cell index maps to tile-local position = index * GRID_PART_SIZE
    float localX0 = cellX * GRID_PART_SIZE;
    float localY0 = cellY * GRID_PART_SIZE;
    float localX1 = (cellX + 1) * GRID_PART_SIZE;
    float localY1 = (cellY + 1) * GRID_PART_SIZE;

    // World-space square corners
    float wAx0 = localX0 + tileOriginX; float wAy0 = localY0 + tileOriginY; // bottom-left (h1)
    float wBx0 = localX1 + tileOriginX; float wBy0 = localY0 + tileOriginY; // bottom-right (h2)
    float wCx0 = localX0 + tileOriginX; float wCy1 = localY1 + tileOriginY; // top-left (h3)
    float wDx1 = localX1 + tileOriginX; float wDy1 = localY1 + tileOriginY; // top-right (h4)
    float wExC = (localX0 + localX1) * 0.5f + tileOriginX; // center X (h5)
    float wEyC = (localY0 + localY1) * 0.5f + tileOriginY; // center Y (h5)

    // Determine triangle selection & interpolation coefficients (duplicate of SampleHeightAndSquare logic)
    int triSel; float aCoef, bCoef, cCoef; float reconHeight;
    if (fracX + fracY < 1.0f)
    {
        if (fracX > fracY)
        {
            triSel = 1; // h1,h2,h5
            aCoef = h2 - h1; bCoef = h5 - h1 - h2; cCoef = h1;
        }
        else
        {
            triSel = 2; // h1,h3,h5
            aCoef = h5 - h1 - h3; bCoef = h3 - h1; cCoef = h1;
        }
    }
    else
    {
        if (fracX > fracY)
        {
            triSel = 3; // h2,h4,h5
            aCoef = h2 + h4 - h5; bCoef = h4 - h2; cCoef = h5 - h4;
        }
        else
        {
            triSel = 4; // h3,h4,h5
            aCoef = h4 - h3; bCoef = h3 + h4 - h5; cCoef = h5 - h4;
        }
    }
    reconHeight = aCoef * fracX + bCoef * fracY + cCoef; // should match h

    // Prepare triangle vertex world positions based on triSel
    // Order A,B,C corresponds to interpolation logic used above
    float tAx, tAy, tAz, tBx, tBy, tBz, tCx, tCy, tCz;
    switch (triSel)
    {
    case 1: // h1 (A), h2 (B), h5 (C)
        tAx = wAx0; tAy = wAy0; tAz = h1;
        tBx = wBx0; tBy = wBy0; tBz = h2;
        tCx = wExC; tCy = wEyC; tCz = h5;
        break;
    case 2: // h1 (A), h3 (B), h5 (C)
        tAx = wAx0; tAy = wAy0; tAz = h1;
        tBx = wCx0; tBy = wCy1; tBz = h3;
        tCx = wExC; tCy = wEyC; tCz = h5;
        break;
    case 3: // h2 (A), h4 (B), h5 (C)
        tAx = wBx0; tAy = wBy0; tAz = h2;
        tBx = wDx1; tBy = wDy1; tBz = h4;
        tCx = wExC; tCy = wEyC; tCz = h5;
        break;
    case 4: // h3 (A), h4 (B), h5 (C)
        tAx = wCx0; tAy = wCy1; tAz = h3;
        tBx = wDx1; tBy = wDy1; tBz = h4;
        tCx = wExC; tCy = wEyC; tCz = h5;
        break;
    default:
        tAx = tAy = tAz = tBx = tBy = tBz = tCx = tCy = tCz = 0.0f;
        break;
    }

    // Emit consolidated diagnostic line (disabled per request)
    // LOG_INFO(std::fixed << std::setprecision(3)
    //     << "[TerrainHeight] map=" << mapId
    //     << " x=" << x << " y=" << y
    //     << " gridX=" << gridX << " gridY=" << gridY
    //     << " cellX=" << cellX << " cellY=" << cellY
    //     << " fracX=" << fracX << " fracY=" << fracY
    //     << " triSel=" << triSel
    //     << " h=" << h
    //     << " h1=" << h1 << " h2=" << h2 << " h3=" << h3 << " h4=" << h4 << " h5"
    //     << " a=" << aCoef << " b=" << bCoef << " c=" << cCoef
    //     << " recon=" << reconHeight);

    // Emit triangle vertices for correlation with TerrainTriSample (world space)
    // std::cout << std::fixed << std::setprecision(3)
    //           << "[TerrainHeightTri] map=" << mapId
    //           << " triSel=" << triSel
    //           << " A=(" << tAx << "," << tAy << "," << tAz << ")"
    //           << " B=(" << tBx << "," << tBy << "," << tBz << ")"
    //           << " C=(" << tCx << "," << tCy << "," << tCz << ")" << std::endl;
    LOG_DEBUG(std::fixed << std::setprecision(3)
        << "[TerrainHeightTri] map=" << mapId
        << " triSel=" << triSel
        << " A=(" << tAx << "," << tAy << "," << tAz << ")"
        << " B=(" << tBx << "," << tBy << "," << tBz << ")"
        << " C=(" << tCx << "," << tCy << "," << tCz << ")");

    return h;
}

float MapLoader::GetLiquidLevel(uint32_t mapId, float x, float y)
{
    uint32_t gridX, gridY;
    worldToGridCoords(x, y, gridX, gridY);

    if (!LoadMapTile(mapId, gridY, gridX))
    {
        return VMAP::VMAP_INVALID_LIQUID_HEIGHT;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_loadedTiles.find(makeKey(mapId, gridY, gridX));
    if (it == m_loadedTiles.end())
    {
        return VMAP::VMAP_INVALID_LIQUID_HEIGHT;
    }

    float liquidLevel = it->second->getLiquidLevel(x, y);

    return liquidLevel;
}

uint8_t MapLoader::GetLiquidType(uint32_t mapId, float x, float y)
{
    uint32_t gridX, gridY;
    worldToGridCoords(x, y, gridX, gridY);

    if (!LoadMapTile(mapId, gridY, gridX))
    {
        return VMAP::MAP_LIQUID_TYPE_NO_WATER;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_loadedTiles.find(makeKey(mapId, gridY, gridX));
    if (it == m_loadedTiles.end())
    {
        return VMAP::MAP_LIQUID_TYPE_NO_WATER;
    }

    uint8_t liquidType = it->second->getLiquidType(x, y);

    return liquidType;
}

uint16_t MapLoader::GetAreaId(uint32_t mapId, float x, float y)
{
    uint32_t gridX, gridY;
    worldToGridCoords(x, y, gridX, gridY);

    if (!LoadMapTile(mapId, gridY, gridX))
    {
        return 0;
    }

    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_loadedTiles.find(makeKey(mapId, gridY, gridX));
    if (it == m_loadedTiles.end())
    {
        return 0;
    }

    uint16_t areaId = it->second->getArea(x, y);

    return areaId;
}

size_t MapLoader::GetLoadedTileCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_loadedTiles.size();
}

bool MapLoader::IsTileLoaded(uint32_t mapId, uint32_t x, uint32_t y) const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_loadedTiles.find(makeKey(mapId, x, y)) != m_loadedTiles.end();
}

// ---- New: terrain triangle extraction ----
bool MapLoader::GetTerrainTriangles(uint32_t mapId, float minX, float minY, float maxX, float maxY,
                                   std::vector<MapFormat::TerrainTriangle>& out)
{
    if (!m_initialized)
    {
        std::cout << "[TerrainQuery] ERROR: MapLoader not initialized" << std::endl;
        return false;
    }

    uint32_t gx0, gy0, gx1, gy1;
    worldToGridCoords(minX, minY, gx0, gy0);
    worldToGridCoords(maxX, maxY, gx1, gy1);

    uint32_t minGX = std::min(gx0, gx1);
    uint32_t maxGX = std::max(gx0, gx1);
    uint32_t minGY = std::min(gy0, gy1);
    uint32_t maxGY = std::max(gy0, gy1);

    size_t before = out.size();
    size_t tilesVisited = 0, tilesLoaded = 0, tilesMissing = 0;
    size_t trisOut = 0;

    for (uint32_t tileY = minGY; tileY <= maxGY; ++tileY)
    {
        for (uint32_t tileX = minGX; tileX <= maxGX; ++tileX)
        {
            ++tilesVisited;
            bool loadedOk = LoadMapTile(mapId, tileY, tileX);

            if (!loadedOk)
            {
                ++tilesMissing;
                continue;
            }
            ++tilesLoaded;

            std::lock_guard<std::mutex> lock(m_mutex);
            auto it = m_loadedTiles.find(makeKey(mapId, tileY, tileX));
            if (it == m_loadedTiles.end())
            {
                std::cout << "[TerrainQueryTile] not in cache after load key(map, y, x)=" << mapId << "," << tileY << "," << tileX << std::endl;
                ++tilesMissing;
                continue;
            }
            GridMap* tile = it->second.get();
            if (!tile)
            {
                std::cout << "[TerrainQueryTile] null GridMap ptr for tile y=" << tileY << " x=" << tileX << std::endl;
                ++tilesMissing;
                continue;
            }

            // Compute tile origin (lower bound world corner)
            float tileMaxWorldX = (CENTER_GRID_ID - static_cast<float>(tileY)) * GRID_SIZE;
            float tileMaxWorldY = (CENTER_GRID_ID - static_cast<float>(tileX)) * GRID_SIZE;
            float tileOriginX = tileMaxWorldX - GRID_SIZE;
            float tileOriginY = tileMaxWorldY - GRID_SIZE;

            // Convert world AABB to tile-local space (non-inverted for logging)
            float localMinX = minX - tileOriginX;
            float localMinY = minY - tileOriginY;
            float localMaxX = maxX - tileOriginX;
            float localMaxY = maxY - tileOriginY;
            localMinX = std::max(0.0f, localMinX); localMinY = std::max(0.0f, localMinY);
            localMaxX = std::min(GRID_SIZE, localMaxX); localMaxY = std::min(GRID_SIZE, localMaxY);

            // Invert local coordinates to match GetHeight indexing (cell 0 near tile upper bound)
            float invMinX = GRID_SIZE - localMaxX;
            float invMaxX = GRID_SIZE - localMinX;
            float invMinY = GRID_SIZE - localMaxY;
            float invMaxY = GRID_SIZE - localMinY;

            // Clamp inverted ranges
            invMinX = std::max(0.0f, invMinX); invMinY = std::max(0.0f, invMinY);
            invMaxX = std::min(GRID_SIZE, invMaxX); invMaxY = std::min(GRID_SIZE, invMaxY);

            int xi0 = std::max(0, (int)std::floor(invMinX / GRID_PART_SIZE));
            int yi0 = std::max(0, (int)std::floor(invMinY / GRID_PART_SIZE));
            int xi1 = std::min(V8_SIZE - 1, (int)std::floor(invMaxX / GRID_PART_SIZE));
            int yi1 = std::min(V8_SIZE - 1, (int)std::floor(invMaxY / GRID_PART_SIZE));

            size_t addedThisTile = 0;
            for (int xi = xi0; xi <= xi1; ++xi)
            {
                for (int yi = yi0; yi <= yi1; ++yi)
                {
                    float h1, h2, h3, h4, h5;
                    if (!tile->getSquareHeights(xi, yi, h1, h2, h3, h4, h5))
                        continue;

                    // Inverted local positions
                    float invX0 = xi * GRID_PART_SIZE;
                    float invY0 = yi * GRID_PART_SIZE;
                    float invX1 = (xi + 1) * GRID_PART_SIZE;
                    float invY1 = (yi + 1) * GRID_PART_SIZE;

                    // Map to world: world = origin + (GRID_SIZE - local)
                    float wA0 = tileOriginX + (GRID_SIZE - invX0);
                    float wB0 = tileOriginX + (GRID_SIZE - invX1);
                    float wY0 = tileOriginY + (GRID_SIZE - invY0);
                    float wY1 = tileOriginY + (GRID_SIZE - invY1);
                    float wCX = tileOriginX + (GRID_SIZE - (invX0 + invX1) * 0.5f);
                    float wCY = tileOriginY + (GRID_SIZE - (invY0 + invY1) * 0.5f);

                    auto pushUpward = [&](float ax, float ay, float az,
                                          float bx, float by, float bz,
                                          float cx, float cy, float cz) {
                        float abx = bx - ax, aby = by - ay, abz = bz - az;
                        float acx = cx - ax, acy = cy - ay, acz = cz - az;
                        float nz = abx * acy - aby * acx;
                        if (nz < 0.0f)
                            out.push_back({ ax, ay, az, cx, cy, cz, bx, by, bz });
                        else
                            out.push_back({ ax, ay, az, bx, by, bz, cx, cy, cz });
                        ++trisOut; ++addedThisTile;
                    };

                    // Four triangles (matching GetHeight order after inversion)
                    pushUpward(wA0, wY0, h1, wB0, wY0, h2, wCX, wCY, h5); // (h1,h2,h5)
                    pushUpward(wA0, wY0, h1, wA0, wY1, h3, wCX, wCY, h5); // (h1,h3,h5)
                    pushUpward(wB0, wY0, h2, wB0, wY1, h4, wCX, wCY, h5); // (h2,h4,h5)
                    pushUpward(wA0, wY1, h3, wB0, wY1, h4, wCX, wCY, h5); // (h3,h4,h5)
                }
            }
        }
    }

    bool any = out.size() > before;

    return any;
}

bool GridMap::getSquareHeights(int xi, int yi, float& h1, float& h2, float& h3, float& h4, float& h5) const
{
    if (xi < 0 || xi >= V8_SIZE || yi < 0 || yi >= V8_SIZE)
        return false;
    if (isHole(xi, yi))
        return false;
    h1 = sampleV9Height(xi,     yi    );
    h2 = sampleV9Height(xi + 1, yi    );
    h3 = sampleV9Height(xi,     yi + 1);
    h4 = sampleV9Height(xi + 1, yi + 1);
    h5 = sampleV8Center(xi, yi);
    if (h1 <= INVALID_HEIGHT || h2 <= INVALID_HEIGHT || h3 <= INVALID_HEIGHT ||
        h4 <= INVALID_HEIGHT || h5 <= INVALID_HEIGHT)
        return false;
    return true;
}

void MapLoader::worldToCellIndices(float x, float y, int& cellX, int& cellY, float& fracX, float& fracY) const
{
    float tx = MAP_RESOLUTION * (32 - x / SIZE_OF_GRIDS);
    float ty = MAP_RESOLUTION * (32 - y / SIZE_OF_GRIDS);
    cellX = ((int)tx) & (MAP_RESOLUTION - 1);
    cellY = ((int)ty) & (MAP_RESOLUTION - 1);
    fracX = tx - (int)tx;
    fracY = ty - (int)ty;
}

bool MapLoader::SampleHeightAndSquare(uint32_t mapId, float x, float y, int& cellX, int& cellY,
                                      float& outHeight, float& h1, float& h2, float& h3, float& h4, float& h5)
{
    uint32_t gridX, gridY;
    worldToGridCoords(x, y, gridX, gridY);
    if (!LoadMapTile(mapId, gridY, gridX))
        return false;
    std::lock_guard<std::mutex> lock(m_mutex);
    auto it = m_loadedTiles.find(makeKey(mapId, gridY, gridX));
    if (it == m_loadedTiles.end())
        return false;
    GridMap* tile = it->second.get();
    if (!tile)
        return false;

    float fracX, fracY;
    worldToCellIndices(x, y, cellX, cellY, fracX, fracY);

    if (!tile->getSquareHeights(cellX, cellY, h1, h2, h3, h4, h5))
    {
        outHeight = INVALID_HEIGHT;
        return false;
    }

    // Reproduce getHeightFromFloat triangle interpolation logic (matching GridMap::getHeightFromFloat)
    if (fracX + fracY < 1.0f)
    {
        if (fracX > fracY)
        {
            // h1, h2, h5
            float a = h2 - h1;
            float b = h5 - h1 - h2;
            float c = h1;
            outHeight = a * fracX + b * fracY + c;
        }
        else
        {
            // h1, h3, h5
            float a = h5 - h1 - h3;
            float b = h3 - h1;
            float c = h1;
            outHeight = a * fracX + b * fracY + c;
        }
    }
    else
    {
        if (fracX > fracY)
        {
            // h2, h4, h5
            float a = h2 + h4 - h5;
            float b = h4 - h2;
            float c = h5 - h4;
            outHeight = a * fracX + b * fracY + c;
        }
        else
        {
            // h3, h4, h5
            float a = h4 - h3;
            float b = h3 + h4 - h5;
            float c = h5 - h4;
            outHeight = a * fracX + b * fracY + c;
        }
    }

    return true;
}