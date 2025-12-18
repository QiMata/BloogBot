#include "PhysicsLiquidHelpers.h"

namespace PhysicsLiquid
{
    LiquidInfo Evaluate(float z,
                        bool vmapHasLevel,
                        float vmapLevel,
                        uint32_t vmapTypeUnified,
                        bool adtHasLevel,
                        float adtLevel,
                        uint32_t adtTypeUnified,
                        uint32_t waterUnifiedType)
    {
        LiquidInfo info{};
        bool useVmap = vmapHasLevel;
        info.fromVmap = useVmap;
        info.level = useVmap ? vmapLevel : adtLevel;
        info.type = useVmap ? vmapTypeUnified : adtTypeUnified;
        info.hasLevel = useVmap ? vmapHasLevel : adtHasLevel;
        if (info.hasLevel) {
            float immersion = info.level - z;
            info.isSwimming = immersion > 0.0f && info.type == waterUnifiedType;
        }
        return info;
    }
}
