// BakeProfile.h - Phase 1 single-source-of-truth for Recast bake parameters.
//
// See docs/Plan/Pathfinding/OVERHAUL_PHASE1_PREP.md for design rationale and
// docs/Plan/Pathfinding/RECAST_PHYSICS_VALIDATED_OVERHAUL.md proposal Phase 1.
//
// Mononen rules (from Recast author Mikko Mononen, recastnavigation guidance):
//   cs = agent.radius * 0.5   (outdoor)   OR   agent.radius / 3   (indoor)
//   ch = cs * 0.5
//   walkableRadius = ceil(agent.radius / cs)
//   walkableHeight = ceil(agent.height / ch)
//   walkableClimb  = floor(agent.maxClimb / ch)
//
// This header is intentionally self-contained: it does not #include anything
// from TileWorker.cpp or MapBuilder.cpp, and the live build path does not
// reference it yet. Phase 1 follow-up iters wire MakeBakeProfile(kTaurenM)
// into TileWorker.cpp::from_json(rcConfig), replacing the hardcoded
// MMAP::BASE_UNIT_DIM cs/ch assignment.

#pragma once

#include <string_view>

namespace MMAP {

struct AgentProfile {
    std::string_view name;
    float radius;
    float height;
    float maxClimbTerrain;
    float maxClimbModel;
    float maxSlopeDegrees;

    constexpr float csOutdoor() const { return radius * 0.5f; }
    constexpr float csIndoor()  const { return radius / 3.0f; }
    constexpr float ch(float cs) const { return cs * 0.5f; }
};

// Default WoW agent profile: Tauren male (largest race, sets the worst-case
// envelope; smaller races just over-bake slightly).
//   radius          1.0247  - half-circumference of unit cylinder
//   height          2.625   - eye height for collision capsule
//   maxClimbTerrain 1.8     - matches physics-engine STEPUP_MAX_TERRAIN
//   maxClimbModel   1.8     - unified with terrain (WMO doors vary; pick max)
//   maxSlopeDegrees 60.0    - physics-engine MAX_SLOPE; fixes prior 75 degrees
//                             which let Recast emit walkable polys the runtime
//                             refused to traverse.
inline constexpr AgentProfile kTaurenM = {
    /* name */            "tauren_m",
    /* radius */          1.0247f,
    /* height */          2.625f,
    /* maxClimbTerrain */ 1.8f,
    /* maxClimbModel */   1.8f,
    /* maxSlopeDegrees */ 60.0f,
};

struct BakeProfile {
    float cs;
    float ch;
    int   tileSize;
    int   borderSize;
    int   walkableRadius;
    int   walkableHeight;
    int   walkableClimb;
    float walkableSlopeAngle;
    float maxSimplificationError;
    int   maxEdgeLen;
    int   maxVertsPerPoly;
    int   minRegionArea;
    int   mergeRegionArea;
    float detailSampleDist;
    float detailSampleMaxError;
    static constexpr const char* partitionType = "watershed";
};

// Derive a BakeProfile from an AgentProfile per Mononen rules.
//   indoor=false (default): cs = agent.radius * 0.5 (outdoor coarse)
//   indoor=true            : cs = agent.radius / 3   (finer for tight WMOs)
BakeProfile MakeBakeProfile(const AgentProfile& agent, bool indoor = false);

// Reject malformed profiles before passing to Recast. The proposal's
// "reject if maxSimplificationError >= 1.5" guard plus basic sanity checks.
bool BakeProfileIsValid(const BakeProfile& p);

} // namespace MMAP
