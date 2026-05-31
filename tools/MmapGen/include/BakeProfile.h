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
// from TileWorker.cpp or MapBuilder.cpp. Iter 20 wires
// MakeBakeProfile(kTaurenM, false) into TileWorker.cpp::from_json(rcConfig) for
// cs/ch and removes the unconditional `config.ch = 0.1f` override in
// MapBuilder::buildTile that previously masked the from_json ch value
// (PFS-OVERHAUL-006 Cycle-16, now superseded by the Mononen-rule profile).

#pragma once

#include <cmath>
#include <string_view>

namespace MMAP {

// WoW world-grid constant in yards. One ADT-style map cell is this wide.
// Mirrors the GRID_SIZE constant from src/game/Maps/GridMapDefines.h. Kept
// local here so this header stays self-contained.
inline constexpr float kBakeGridSizeYards = 533.33333f;

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
inline BakeProfile MakeBakeProfile(const AgentProfile& agent, bool indoor = false)
{
    BakeProfile p{};
    p.cs                     = indoor ? agent.csIndoor() : agent.csOutdoor();
    p.ch                     = agent.ch(p.cs);
    p.walkableRadius         = static_cast<int>(std::ceil(agent.radius / p.cs));
    p.walkableHeight         = static_cast<int>(std::ceil(agent.height / p.ch));
    // floor is conservative for step-up: rounding down means Recast accepts
    // strictly fewer ledges as "step-uppable" than the agent can physically
    // climb. Matches the proposal's "walkableClimb_voxels = floor(maxClimb /
    // ch)" rule.
    p.walkableClimb          = static_cast<int>(std::floor(agent.maxClimbTerrain / p.ch));
    p.walkableSlopeAngle     = agent.maxSlopeDegrees;
    p.borderSize             = p.walkableRadius + 3;
    // Recast guideline: 12y is a reasonable max contour edge length;
    // walkableRadius * 8 is the original Mononen heuristic.
    p.maxEdgeLen             = static_cast<int>(12.0f / p.cs);
    p.maxSimplificationError = 1.3f;
    p.detailSampleDist       = p.cs * 6.0f;
    p.detailSampleMaxError   = p.ch * 1.0f;
    p.minRegionArea          = 20;  // TrinityCore default
    p.mergeRegionArea        = 40;  // TrinityCore default
    p.maxVertsPerPoly        = 6;   // Detour DT_VERTS_PER_POLYGON default
    // tileSize is the number of voxels per Recast tile side. The bake covers
    // kBakeGridSizeYards across ~25 internal subdivisions (vmangos
    // TILES_PER_MAP), so a Recast tile spans roughly (gridSize / 25) / cs.
    // Iter 20 does NOT wire BakeProfile.tileSize into rcConfig (the json
    // path still drives tileSize at MMAP::VERTEX_PER_TILE=80 per
    // PFS-OVERHAUL-006 Cycle-16). This value is informational for iter 21+.
    const float tileWorldYards = kBakeGridSizeYards / 25.0f;
    p.tileSize               = static_cast<int>(std::round(tileWorldYards / p.cs));
    return p;
}

// Reject malformed profiles before passing to Recast. The proposal's
// "reject if maxSimplificationError >= 1.5" guard plus basic sanity checks.
inline bool BakeProfileIsValid(const BakeProfile& p)
{
    if (p.cs <= 0.0f || p.ch <= 0.0f) return false;
    if (p.maxSimplificationError >= 1.5f) return false;
    if (p.walkableRadius < 1 || p.walkableHeight < 1 || p.walkableClimb < 0) return false;
    if (p.walkableSlopeAngle <= 0.0f || p.walkableSlopeAngle > 89.0f) return false;
    if (p.maxVertsPerPoly < 3) return false;
    if (p.minRegionArea < 1 || p.mergeRegionArea < p.minRegionArea) return false;
    return true;
}

} // namespace MMAP
