#include "PhysicsHelpers.h"
#include "PhysicsBridge.h" // for MovementFlags bit definitions
#include <cmath>

namespace PhysicsHelpers
{
    Intent BuildMovementIntent(uint32_t moveFlags, float orientation)
    {
        Intent intent{};
        float c = std::cos(orientation);
        float s = std::sin(orientation);
        float dirX = 0.0f, dirY = 0.0f;

        // Use shared MovementFlags from PhysicsBridge.h to avoid mismatches
        if (moveFlags & MOVEFLAG_FORWARD)      { dirX += c;  dirY += s; }
        if (moveFlags & MOVEFLAG_BACKWARD)     { dirX -= c;  dirY -= s; }
        if (moveFlags & MOVEFLAG_STRAFE_LEFT)  { dirX += s;  dirY -= c; }
        if (moveFlags & MOVEFLAG_STRAFE_RIGHT) { dirX -= s;  dirY += c; }

        float mag = std::sqrt(dirX * dirX + dirY * dirY);
        if (mag > 0.0001f) { dirX /= mag; dirY /= mag; intent.hasInput = true; }

        intent.dir = G3D::Vector3(dirX, dirY, 0.0f);
        intent.jumpRequested = (moveFlags & MOVEFLAG_JUMPING) != 0;
        return intent;
    }
}
