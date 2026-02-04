#pragma once
#include <cstdint>
#include "Vector3.h"

namespace PhysicsLiquid
{
    struct LiquidInfo
    {
        float level = 0.0f;
        uint32_t type = 0u; // unified type id
        bool fromVmap = false;
        bool hasLevel = false;
        bool isSwimming = false;
    };

    // Pure evaluation given optional VMAP and ADT inputs.
    // Pass vmapHasLevel=true when VMAP provided a valid level; adtHasLevel=true for ADT.
    // Unified type resolver should map source-specific types to a shared enum.
    LiquidInfo Evaluate(float z,
                        bool vmapHasLevel,
                        float vmapLevel,
                        uint32_t vmapTypeUnified,
                        bool adtHasLevel,
                        float adtLevel,
                        uint32_t adtTypeUnified,
                        uint32_t waterUnifiedType /* e.g., LIQUID_TYPE_WATER */);
}
