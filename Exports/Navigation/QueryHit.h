#pragma once

#include <cstdint>
#include "Vector3.h"

// Common unified scene query hit structure used by raycasts, sweeps and overlaps
struct QueryHit
{
    bool hit = false;
    float distance = 0.0f;          // Time of impact along a ray/sweep, or penetration depth for overlaps
    G3D::Vector3 normal = G3D::Vector3(0, 1, 0);
    G3D::Vector3 point = G3D::Vector3(0, 0, 0);
    uint32_t triIndex = 0;          // Triangle index within the source mesh, if available
    uint32_t instanceId = 0;        // Source instance id (model), if available
};
