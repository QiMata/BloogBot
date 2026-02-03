#include "VMapLog.h"
#include <cstdlib>
#include <string>
#include <sstream>
#include <iostream>

// Default logging configuration
int gPhysLogLevel = 3;           // 0=ERR,1=INFO,2=DBG,3=TRACE
uint32_t gPhysLogMask = PHYS_ALL;

static int ParseLevel(const char* s)
{
    if (!s) return gPhysLogLevel;
    try {
        int v = std::stoi(std::string(s));
        if (v < 0) v = 0;
        return v;
    } catch (...) {
        return gPhysLogLevel;
    }
}

static uint32_t ParseMask(const char* s)
{
    if (!s) return gPhysLogMask;
    std::string str(s);
    try {
        // allow 0x hex or decimal
        size_t idx = 0;
        unsigned long v = std::stoul(str, &idx, 0);
        return static_cast<uint32_t>(v);
    } catch (...) {
        return gPhysLogMask;
    }
}

// Static initializer reads optional environment variables to override defaults.
struct VMapLogInit
{
    VMapLogInit()
    {
        const char* lvl = std::getenv("VMAP_PHYS_LOG_LEVEL");
        if (lvl) gPhysLogLevel = ParseLevel(lvl);
        const char* mask = std::getenv("VMAP_PHYS_LOG_MASK");
        if (mask) gPhysLogMask = ParseMask(mask);

        // Force-enable TRACE level and cylinder category to ensure diagnostics are visible during tests.
        // This overrides runtime env vars if they would disable the important TRACE output we added.
        if (gPhysLogLevel < 3) gPhysLogLevel = 3;
        gPhysLogMask |= PHYS_CYL; // ensure cylinder logs are always included

        // Optionally echo the runtime settings to stdout so tests can observe them
        std::ostringstream ss;
        ss << "[PHYS][INFO][INIT] gPhysLogLevel=" << gPhysLogLevel << " gPhysLogMask=0x" << std::hex << gPhysLogMask << std::dec;
        std::cout << ss.str() << std::endl;
    }
};

static VMapLogInit s_vmapLogInit;

const char* PhysLevelName(int lvl)
{
    switch (lvl)
    {
    case 0: return "ERR";
    case 1: return "INF";
    case 2: return "DBG";
    case 3: return "TRC";
    default: return "UNK";
    }
}

const char* PhysCatName(uint32_t cat)
{
    // Prefer single-category names; if multiple bits set, return a short combined label.
    if (cat == PHYS_MOVE) return "MOVE";
    if (cat == PHYS_SURF) return "SURF";
    if (cat == PHYS_HEAD) return "HEAD";
    if (cat == PHYS_CYL)  return "CYL";
    if (cat == PHYS_STEP) return "STEP";

    // fallback: build short name for multiple bits
    if (cat & PHYS_CYL) return "CYL";
    if (cat & PHYS_MOVE) return "MOVE";
    if (cat & PHYS_SURF) return "SURF";
    if (cat & PHYS_HEAD) return "HEAD";
    if (cat & PHYS_STEP) return "STEP";
    return "GEN";
}
