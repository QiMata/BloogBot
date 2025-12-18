#pragma once

#include <cstdint>
#include "Vector3.h"

namespace PhysicsHelpers
{
    struct Intent
    {
        G3D::Vector3 dir;      // normalized planar desired direction (xy, z=0)
        bool hasInput = false;  // any movement key
        bool jumpRequested = false; // jump flag present
    };

    // Build movement intent from raw flags and orientation. Pure function, no engine dependencies.
    Intent BuildMovementIntent(uint32_t moveFlags, float orientation);
}
