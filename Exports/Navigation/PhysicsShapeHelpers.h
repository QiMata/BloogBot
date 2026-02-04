// PhysicsShapeHelpers.h - small helpers to build common capsules used in StepV2
#pragma once

#include "CapsuleCollision.h"

namespace PhysShapes
{
    // Full-height capsule centered on XY using feet Z and character height
    inline CapsuleCollision::Capsule BuildFullHeightCapsule(float x, float y, float zFeet,
                                                            float radius, float height)
    {
        CapsuleCollision::Capsule cap;
        float capBottom = zFeet + radius;
        float capTop    = zFeet + height - radius;
        cap.p0 = CapsuleCollision::Vec3(x, y, capBottom);
        cap.p1 = CapsuleCollision::Vec3(x, y, capTop);
        cap.r = radius;
        return cap;
    }
}
