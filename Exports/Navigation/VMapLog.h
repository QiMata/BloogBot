// VMapLog.h - Logging utilities for VMAP system
#pragma once

#include <iostream>
#include <sstream>
#include <cstdint>

// Base legacy enable flags (kept as before – can still be controlled by undefining)
#define WARN_LOG
#define ERROR_LOG
#define INFO_LOG
#define DEBUG_LOG1
#define TRACE_LOG1
#define VECTOR3_LOG1
#define RAY_LOG1

#ifdef WARN_LOG
#define LOG_WARN(msg) do { \
        std::stringstream ss; \
        ss << "[WARN] " << msg; \
        std::cout << ss.str() << std::endl; \
    } while(0)
#else
#define LOG_WARN(msg)
#endif // WARN_LOG

#ifdef DEBUG_LOG
#define LOG_DEBUG(msg) do { \
        std::stringstream ss; \
        ss << "[DEBUG] " << msg; \
        std::cout << ss.str() << std::endl; \
    } while(0)
#else
#define LOG_DEBUG(msg)
#endif // DEBUG_LOG

#ifdef ERROR_LOG
#define LOG_ERROR(msg) do { \
        std::stringstream ss; \
        ss << "[ERROR] " << msg; \
        std::cout << ss.str() << std::endl; \
    } while(0)
#else
#define LOG_ERROR(msg)
#endif // ERROR_LOG

#ifdef INFO_LOG
#define LOG_INFO(msg) do { \
        std::stringstream ss; \
        ss << "[INFO] " << msg; \
        std::cout << ss.str() << std::endl; \
    } while(0)
#else
#define LOG_INFO(msg)
#endif // INFO_LOG

#ifdef TRACE_LOG
#define LOG_TRACE(msg) do { \
        std::stringstream ss; \
        ss << "[TRACE] " << msg; \
        std::cout << ss.str() << std::endl; \
    } while(0)
#else
#define LOG_TRACE(msg)
#endif // TRACE_LOG

#ifdef VECTOR3_LOG
#define LOG_VECTOR3(label, v) do { \
        std::cout << "[VECTOR3] " << label << ": (" \
                  << v.x << ", " << v.y << ", " << v.z << ")" << std::endl; \
    } while(0)
#else
#define LOG_VECTOR3(label, v)
#endif // VECTOR3_LOG

#ifdef RAY_LOG
#define LOG_RAY(label, r) do { \
        std::cout << "[RAY] " << label << ": origin(" \
                  << r.origin().x << ", " << r.origin().y << ", " << r.origin().z \
                  << ") dir(" << r.direction().x << ", " << r.direction().y << ", " \
                  << r.direction().z << ")" << std::endl; \
    } while(0)
#else
#define LOG_RAY(label, r)
#endif // RAY_LOG

// ===================== New structured physics logging =====================
// Levels: 0=ERR,1=INFO,2=DBG,3=TRACE (extendable)
extern int gPhysLogLevel;            // default configured at runtime
extern uint32_t gPhysLogMask;        // category bitmask

// Categories (bitmask)
enum PhysLogCat : uint32_t {
    PHYS_MOVE = 1u << 0,   // movement integration / mode switches
    PHYS_SURF = 1u << 1,   // ground / surface candidate logic
    PHYS_HEAD = 1u << 2,   // head clearance phases
    PHYS_CYL  = 1u << 3,   // cylinder-triangle intersection & sweeps
    PHYS_STEP = 1u << 4,   // step up / step down attempts
    PHYS_WALL = 1u << 5,   // wall slide / obstruction resolution
    PHYS_PERF = 1u << 6,   // perf timing blocks
    PHYS_ALL  = 0xFFFFFFFFu
};

// Helper inline converters (declared here; defined in one .cpp)
const char* PhysCatName(uint32_t cat);
const char* PhysLevelName(int lvl);

// Core macro; msg evaluated only when enabled
#define PHYS_LOG(lvl, cat, msg) do { \
    if ((gPhysLogMask & (cat)) && (lvl) <= gPhysLogLevel) { \
        std::stringstream ss__; \
        ss__ << "[PHYS][" << PhysLevelName(lvl) << "][" << PhysCatName(cat) << "] " << msg; \
        std::cout << ss__.str() << std::endl; \
    } \
} while(0)

// Convenience wrappers
#define PHYS_ERR(cat, msg)   PHYS_LOG(0, cat, msg)
#define PHYS_INFO(cat, msg)  PHYS_LOG(1, cat, msg)
#define PHYS_DBG(cat, msg)   PHYS_LOG(2, cat, msg)
#define PHYS_TRACE(cat, msg) PHYS_LOG(3, cat, msg)