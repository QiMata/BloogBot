// PhysicsSelectHelpers.h - common hit selection helpers used by StepV2
#pragma once

#include <vector>
#include "SceneQuery.h" // for SceneHit

namespace PhysSelect
{
    inline const SceneHit* FindEarliestWalkableNonPen(const std::vector<SceneHit>& hits, float walkableCosMin)
    {
        for (size_t i = 0; i < hits.size(); ++i) {
            const auto& h = hits[i];
            if (h.startPenetrating) continue;
            if (h.normal.z < walkableCosMin) continue;
            return &h;
        }
        return nullptr;
    }

    inline const SceneHit* HighestPenetratingUpward(const std::vector<SceneHit>& hits)
    {
        const SceneHit* bestPen = nullptr;
        float bestZ = -FLT_MAX;
        for (const auto& h : hits) {
            if (!h.startPenetrating) continue;
            if (h.normal.z < 0.0f) continue;
            if (h.point.z > bestZ) { bestZ = h.point.z; bestPen = &h; }
        }
        return bestPen;
    }
}
