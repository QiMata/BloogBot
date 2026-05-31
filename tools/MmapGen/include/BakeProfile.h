// BakeProfile.h — Phase 1 of the Recast Physics-Validated Overhaul.
//
// Single source of truth for ALL Recast bake parameters. Derives every
// knob from a single AgentProfile (race-keyed PhysicsEngine constants)
// per Mononen's "Recast Settings Uncovered" rules.
//
// Replaces the divergent BASE_UNIT_DIM = 0.2666f (TileWorker.cpp) +
// BASE_UNIT_DIM_MAP_BUILDER = 0.13f (MapBuilder.cpp) "Dont ask me why"
// situation with a single rule-derived BakeProfile per agent.
//
// References:
//   docs/Plan/Pathfinding/RECAST_PHYSICS_VALIDATED_OVERHAUL.md §2 Layer 1.
//   docs/Plan/Pathfinding/OVERHAUL_PHASE1_PREP.md (Phase 1 prep scope).
//   Mononen 2009 — http://digestingduck.blogspot.com/2009/08/recast-settings-uncovered.html

#pragma once

#include <cmath>
#include <string>

namespace MMAP
{

// ============================================================================
// AgentProfile — physics-engine-derived constants per race.
// ============================================================================
//
// Source of truth: Exports/Navigation/PhysicsTolerances.h. Initial values
// below match Tauren M (the largest WoW capsule per config.json _agentNotes:
// radius 1.0247, height 2.625). Other races can be added later as needed —
// the bake currently produces one mesh for the largest capsule and every
// other race uses that same mesh at runtime.

struct AgentProfile
{
    std::string name;
    float radius;          // y, capsule radius
    float height;          // y, capsule height
    float maxClimbTerrain; // y, vertical step-up on terrain
    float maxClimbModel;   // y, step-up on WMO/M2 transition
    float maxSlopeDegrees; // physics-engine MAX_SLOPE accept threshold

    // Mononen derivations. The proposal §2 Layer 1 uses cs = r/2 outdoor,
    // cs = r/3 indoor. Indoor is for city WMOs / dungeons.
    constexpr float csOutdoor() const { return radius * 0.5f; }
    constexpr float csIndoor()  const { return radius / 3.0f; }

    // Mononen's rule: ch = cs / 2 (halving voxel column height vs width).
    // The current MmapGen default ch = cs is the BIGGEST non-Mononen
    // compliance and explains the iter-2 stall's 7-poly Z-stack at WMO
    // interiors (per OVERHAUL_PHASE1_PREP.md).
    constexpr float ch(float cs) const { return cs * 0.5f; }
};

// Default Tauren M profile. Matches config.json's `default` block agent
// constants. PHYSICSENGINE_MAX_SLOPE = 60° per the proposal — currently
// MmapGen accepts walkableSlopeAngle=75° on terrain which is the second
// Mononen violation.
inline constexpr AgentProfile kTaurenM = {
    /* name */            "tauren_m",
    /* radius */          1.0247f,
    /* height */          2.625f,
    /* maxClimbTerrain */ 1.8f,    // PFS-OVERHAUL-006 default
    /* maxClimbModel */   1.8f,
    /* maxSlopeDegrees */ 60.0f,   // physics-engine MAX_SLOPE; was 75 in old bake
};


// ============================================================================
// BakeProfile — concrete Recast/Detour parameters derived from AgentProfile.
// ============================================================================
//
// One per (agent, environment) pair. The proposal §2 Layer 1 specifies these
// values; deviation requires updating both this header and the proposal doc.

struct BakeProfile
{
    // Voxelization
    float cs;                         // cell size (y) — r/2 outdoor, r/3 indoor
    float ch;                         // cell height (y) — cs/2 per Mononen
    int   tileSize;                   // voxel-side; computed so tile covers GRID_SIZE
    int   borderSize;                 // walkableRadius + 3

    // Agent footprint (in voxel units)
    int   walkableRadius;             // ceil(agent.radius / cs)
    int   walkableHeight;             // ceil(agent.height / ch)
    int   walkableClimb;              // floor(agent.maxClimbTerrain / ch)  — conservative
    float walkableSlopeAngle;         // = agent.maxSlopeDegrees (terrain + model unified)

    // Contour / mesh simplification
    float maxSimplificationError;     // 1.3 per Mononen; reject if >= 1.5
    int   maxEdgeLen;                 // walkableRadius * 8
    int   maxVertsPerPoly;            // 6 (DT_VERTS_PER_POLYGON; old default; was 3 in 4029)

    // Region sizing
    int   minRegionArea;              // 20  — TrinityCore default
    int   mergeRegionArea;            // 40  — TrinityCore default

    // Detail mesh sampling
    float detailSampleDist;           // cs * 6 (NOT cs * 16)
    float detailSampleMaxError;       // 0.5 (NOT 1.25)

    // Partitioning: watershed only. monotone/layers excluded per
    // _4029_NEGATIVE_RESULT_partition_layers_simplify13.
    static constexpr const char* partitionType = "watershed";
};


// ============================================================================
// MakeBakeProfile — single derivation function.
// ============================================================================
//
// indoor=false uses Mononen's r/2 outdoor cs (coarser, faster bake,
// suitable for open terrain). indoor=true uses r/3 (finer, more detail
// for WMO interiors). Default is outdoor for the global re-bake; per-tile
// indoor override is config-driven (TBD: replace config.json per-tile
// blocks with a per-tile indoor flag).
//
// Tile-size derivation: WoW's grid is 32x32 squares of side GRID_SIZE
// (= 533.33y per MapBuilder.h). Recast tiles are 25 voxels per side at
// the chosen cs, so tileSize = round(GRID_SIZE / (25 * cs)).

constexpr float kGridSize = 533.33333f;
constexpr int   kTileSubdivisions = 25;

inline BakeProfile MakeBakeProfile(const AgentProfile& agent, bool indoor = false)
{
    BakeProfile p{};
    p.cs                     = indoor ? agent.csIndoor() : agent.csOutdoor();
    p.ch                     = agent.ch(p.cs);
    p.tileSize               = static_cast<int>(std::round(kGridSize / (kTileSubdivisions * p.cs)));
    p.borderSize             = static_cast<int>(std::ceil(agent.radius / p.cs)) + 3;
    p.walkableRadius         = static_cast<int>(std::ceil(agent.radius / p.cs));
    p.walkableHeight         = static_cast<int>(std::ceil(agent.height / p.ch));
    p.walkableClimb          = static_cast<int>(std::floor(agent.maxClimbTerrain / p.ch));
    p.walkableSlopeAngle     = agent.maxSlopeDegrees;
    p.maxSimplificationError = 1.3f;
    p.maxEdgeLen             = p.walkableRadius * 8;
    p.maxVertsPerPoly        = 6;
    p.minRegionArea          = 20;
    p.mergeRegionArea        = 40;
    p.detailSampleDist       = p.cs * 6.0f;
    p.detailSampleMaxError   = 0.5f;
    return p;
}


// ============================================================================
// Sanity assertions usable at integration time.
// ============================================================================
//
// Phase 1 integration (Wiring BakeProfile into TileWorker.cpp) should add
// these as runtime asserts to catch the proposal's "reject anything above
// maxSimplificationError=1.5" rule and similar.

inline bool BakeProfileIsValid(const BakeProfile& p)
{
    if (p.cs <= 0.0f || p.ch <= 0.0f) return false;
    if (p.ch > p.cs)                  return false;                 // ch must be ≤ cs (Mononen)
    if (p.maxSimplificationError > 1.5f) return false;              // proposal §2 reject threshold
    if (p.walkableRadius < 1)         return false;
    if (p.walkableHeight < 1)         return false;
    if (p.walkableClimb < 0)          return false;
    if (p.tileSize < 16)              return false;
    return true;
}

} // namespace MMAP
