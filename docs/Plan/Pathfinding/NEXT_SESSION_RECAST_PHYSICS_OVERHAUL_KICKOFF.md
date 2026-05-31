# Kickoff prompt — Recast physics-validated overhaul, Phase 0

> Copy the text in the fenced block below into a fresh Claude Code session opened in `e:/repos/Westworld of Warcraft/` (branch `main`). Do NOT paste this header. The prompt is fully self-contained — it tells the new agent what to read, what to build, and what success looks like for the first session.

---

```
You are picking up a major pathfinding overhaul for the BloogBot / Westworld
of Warcraft repo. The user has decided — based on the proposal at
docs/Plan/Pathfinding/RECAST_PHYSICS_VALIDATED_OVERHAUL.md — to rebuild
the navmesh generator so that mmap output is physics-valid by construction,
runtime does only thin Detour queries (no repair, no fallback), and the
existing 5,600-LOC managed repair pipeline goes away.

Your job this session is PHASE 0 of that plan: establish ground truth.
You will NOT change parameters, vendor versions, or runtime code. You WILL
build the validation harness that proves every later phase actually
improved things.

================================================================
READ FIRST (in this order, do not skip)
================================================================

1.  docs/Plan/Pathfinding/RECAST_PHYSICS_VALIDATED_OVERHAUL.md
    The proposal. Sections 0-5 define the problem, the three-layer
    solution, the phased plan. Section 3 Phase 0 is your scope this
    session.

2.  docs/Plan/Pathfinding/NEXT_SESSION_LONGPATHING_ITER2_FINDINGS.md
    The most recent baseline. iter-2 of loop-26 is the last work done
    on this surface. Two findings:
      Failure A: CrossroadsToUndercity off-mesh whack-a-mole hit a
        regression on tile (40,28) — reverted, commit 7ca9f84c.
      Failure B: OrgrimmarToUndercity confirmed non-pathfinding
        (vmangos transport schedule phase). Out of scope for this
        overhaul; the fix is extending OrgrimmarUndercityZeppelinDockWaitSeconds
        from 120 to 540 in LongPathingTests.cs:139.

3.  docs/physics/PATHFINDING_OVERHAUL.md
    The 2026-05-06 ADR / freeze contract. This overhaul is its
    Phase-2-through-5 concrete implementation.

4.  CLAUDE.md (root) + Westworld of Warcraft/CLAUDE.md (repo-specific).
    Load-bearing rules R13 (validate scene-data → physics parity →
    pathfinding), R15 (commit + push every iteration), R16 (screenshots
    for Task/Action live tests), R18 (no deprecation, always full
    removal — Phase 5 of the overhaul deletes Navigation.cs entirely).

5.  Memory under C:/Users/lrhod/.claude/projects/e--repos/memory/:
      project_pfs_loop26_iter2_og_interior_offmesh_regressed.md
      pfs-loop26-iter1-og-east-offmesh.md
      pfs-loop24-close-out-win.md
      project_pathfinding_tile_coords.md  (READ WITH SUSPICION — the
        concrete numbers are right but the explanatory text inverts
        which WoW axis derives which MmapGen tile axis; trust the
        iter-1 memory's correction)

6.  tools/MmapGen/config.json — skim only. ~280 lines of per-tile
    Mononen-violating overrides that the overhaul makes obsolete.
    Do not edit it.

7.  tools/MmapGen/offmesh.txt — 2 active WWoW entries (iter-1 east-wall
    + loop-24 deck-edge seam), plus inherited vmangos defaults.

8.  tools/PathPhysicsProbe/Program.cs — the existing single-route probe
    tool. You will model Phase 0's new probe on this but operate
    over WHOLE TILES, not single routes.

================================================================
WHAT TO BUILD THIS SESSION
================================================================

Phase 0 is FOUR deliverables, in dependency order:

DELIVERABLE 1 — tools/PhysicsValidationProbe (new C++ tool)

  Build a standalone executable that takes a .mmtile file + map ID
  and reports per-polygon physics validation in MEASUREMENT MODE.
  Reuse:
    - The PhysicsEngine link pattern from PathPhysicsProbe (which
      already links Exports/Navigation modules)
    - The Detour navmesh load path from Exports/Navigation/PathFinder.cpp
    - The AgentProfile concept (race → capsule radius, height,
      step-up height, max slope cone). Default to Tauren Male
      (radius=1.0247, height=2.625) matching PathPhysicsProbe.

  Algorithm (measurement only — write nothing):
    For each polygon P in the loaded dtTile:
      For each edge E in P (boundary edges only):
        Sample N points along E, N = ceil(edge_length / (capsule_radius * 2))
        For each sample point S:
          cap_pos = S projected to P.surface + (P.normal * 0.05)  // ground bias
          result = PhysicsEngine.SweepCapsule(
              start = cap_pos,
              direction = E.tangent,
              length = capsule_radius,
              race = agent.race)
          Classify result.affordance: Walk / StepUp / SteepClimb /
            Drop / Cliff / Vertical / JumpGap / SafeDrop /
            UnsafeDrop / Blocked
        edge_affordance = worst_of(samples)  // worst = Blocked > UnsafeDrop > ... > Walk
      poly_affordance = worst_of(edges)

  Output: JSON to stdout or --report path, one line per polygon:
    {
      "polyIdx": 493,
      "polyRef": "0x1000013E001ED",
      "areaType": 1,
      "centroid": [1608.1, -4382.3, 10.476],
      "vertCount": 6,
      "edges": [
        {
          "edgeIdx": 0,
          "affordance": "Blocked",
          "samples": [{"xyz": [...], "affordance": "Walk"}, ...]
        },
        ...
      ],
      "polyAffordance": "Blocked"
    }

  Per-tile summary at end:
    {
      "tile": "0012840",
      "mapId": 1,
      "tileX": 40, "tileY": 28,
      "polyCount": 657,
      "affordanceHistogram": {
        "Walk": 423, "StepUp": 89, "SteepClimb": 12,
        "Blocked": 78, "Cliff": 4, ...
      },
      "topWorstPolys": [<top 10 by sample count of Blocked edges>],
      "validationTimeMs": 182
    }

  CLI:
    PhysicsValidationProbe --map M --tile X,Y [--race tauren_m]
      [--report path/to/output.json] [--samples-per-edge N]
      [--verbose]
    PhysicsValidationProbe --map M --all-tiles
      [--out-dir path/to/dir]  (writes one JSON per tile)

  Build location: tools/PhysicsValidationProbe/ (parallels
  tools/PathPhysicsProbe/). Output: Bot/Release/net8.0/PhysicsValidationProbe.exe
  (matching PathPhysicsProbe convention).

  NOTE: this is a C++ tool because PhysicsEngine is C++. Use the same
  CMake or MSBuild build glue PathPhysicsProbe uses; copy its
  CMakeLists.txt / .vcxproj as a starting point.

DELIVERABLE 2 — Baseline report

  Run PhysicsValidationProbe --all-tiles on map 1 (Kalimdor)
  and map 0 (Eastern Kingdoms). These are the two maps where every
  current failing live test lives.

  Aggregate the per-tile JSON into:
    tmp/iter-overhaul-phase0/baseline-map0.json
    tmp/iter-overhaul-phase0/baseline-map1.json
    tmp/iter-overhaul-phase0/baseline-summary.md  (human-readable)

  The summary.md should show:
    - Global affordance histogram (sum across all tiles per map)
    - Top 20 worst tiles per map (highest Blocked-edge ratio)
    - Specific lookup for the 3 known stall coords:
        WoW (1627.6, -4151.8, 36.9)  — iter-1 east-wall stall
        WoW (1608.1, -4382.3, 10.0)  — iter-2 OG-interior stall
        WoW (1615.3, -4240.85, ~45)  — loop-25 doodad-wall stall
      For each: which tile, which polyIdx, what does the probe
      currently classify its edges as?
    - Cross-tile-seam stats: polys within borderSize voxels of tile
      edge — what fraction are Blocked? (Tile-seam ghost ledges
      are a known class.)

  This is the BASELINE. Every later phase's success metric is the
  diff against this report.

DELIVERABLE 3 — Test-failure baseline manifest

  Capture the current state of the 4 long-pathing live tests so
  later phases can measure improvement.

  For each test:
    - CrossroadsToUndercity_UsesFlightAndZeppelin
    - OrgrimmarToUndercityZeppelin_BoardsAndDeplanes
    - OgZeppelin_BakeFixtureValidation
    - BrmDungeon_BakeFixtureValidation

  Document in tmp/iter-overhaul-phase0/test-baseline.md:
    - Current pass/fail status (we have this from iter-2 evidence
      for CrossroadsToUndercity + OrgrimmarToUndercity; both FAIL)
    - The exact failure mode (assert message, screenshot path,
      stall coord if applicable)
    - Whether the failure mode is pathfinding-class (overhaul-fixable)
      or non-pathfinding-class (separate work)
    - For each failure, the polyIdx + tile from Deliverable 2's
      baseline report at the stall coord

  Do NOT actually re-run the tests this session unless they are
  fast (under 2 min each). The bake-fixture pair takes ~3-4 min;
  the long-pathing tests take 5-8 min each. Total budget for
  running tests this session: 30 min. If you must skip, use
  iter-2's evidence in NEXT_SESSION_LONGPATHING_ITER2_FINDINGS.md
  and the iter-2 memory entry as the captured state.

DELIVERABLE 4 — Findings + go/no-go recommendation

  Write tmp/iter-overhaul-phase0/findings.md summarizing:
    - Does the baseline confirm or refute the design's hypothesis?
      Specifically:
        * Do the 3 known stall coords show Blocked edges in the probe?
          (If YES, the overhaul's Layer-3 algorithm WILL find them.
           If NO, the probe's sampling resolution is too coarse OR the
           PhysicsEngine link is buggy.)
        * What's the global Blocked-poly ratio? Is it within the
          design's expected 20-30% baseline?
        * Are there entire tiles where >50% of polys are Blocked?
          (That would suggest Layer-2 vmap extraction fixes are
          the dominant lever for those tiles.)
    - Bake-time budget validation: how long did probing 1 map take?
      Extrapolate to full re-bake (41 maps). Is it within the
      design's 4-hour-single-thread budget?
    - Top 3 risks identified from the baseline.
    - Recommended Phase 1 starting tile (not the worst — pick a
      tile with clear before/after signal where Layer-1 parameter
      changes should show measurable improvement).

================================================================
GUARDRAILS — DO NOT VIOLATE
================================================================

1. DO NOT modify ANY production code this session. No edits to
   Exports/Navigation, Services/PathfindingService, Services/SceneDataService,
   Exports/BotRunner, or anything under Services/. The only writes
   should be:
     - New files under tools/PhysicsValidationProbe/
     - Build glue (CMakeLists, .vcxproj, .sln entries) for the new tool
     - New files under tmp/iter-overhaul-phase0/
     - New files under docs/Plan/Pathfinding/ if you want to capture
       additional notes (the proposal doc already exists, do not
       overwrite it)

2. DO NOT bake any tiles, do not promote any mmaps, do not restart
   docker containers. Phase 0 is read-only against the existing
   bake.

3. DO NOT delete or modify tools/MmapGen/offmesh.txt. The active
   entries stay. The reverted commented entries stay.

4. DO NOT delete or modify tools/MmapGen/config.json. The per-tile
   overrides and _NEGATIVE_RESULT blocks are institutional memory.
   Phase 1 will clean them up; not this session.

5. Per R18: if you write any throwaway scaffolding (a debug print,
   a sanity-check test), delete it before committing. No "// removed
   in phase 0" comments.

6. Per R15: commit AND push when done, even if Phase 0 surfaces
   findings that suggest changing the overall plan. Negative results
   commit too. Use a clear message:
     "perf(pathfinding): phase 0 baseline — PhysicsValidationProbe + reports"

7. If the PhysicsEngine link into the new probe surfaces include
   cycles or build problems, STOP and surface them. Do not paper
   over them with #ifdef gymnastics. Phase 4 of the overhaul depends
   on a clean link, so if it's broken at Phase 0 we want to know now,
   not after Phases 1-3 are done.

8. If the baseline report shows that the 3 known stall coords do NOT
   correspond to Blocked-classified polys in the probe, STOP before
   writing Deliverable 4 and investigate. Either the probe is broken
   or the design's assumption that "physics edge sweep would have
   caught these" is wrong — both are session-ending findings.

9. If you find that PathPhysicsProbe's existing physics-engine link
   ALREADY does substantial per-edge classification (some of this
   logic may exist already in --enumerate-static-collision or
   similar), reuse it. Do not reimplement. Read its code thoroughly
   before writing the new tool.

10. Use TodoWrite to track the 4 deliverables. Mark each in_progress
    when starting, completed only when its acceptance criterion is
    met.

================================================================
ACCEPTANCE CRITERIA FOR THIS SESSION
================================================================

This session is complete when ALL of these are true:

  [ ] tools/PhysicsValidationProbe/ exists, builds, and runs.
  [ ] PhysicsValidationProbe.exe runs on at least one tile of
      map 1 and produces well-formed JSON output.
  [ ] tmp/iter-overhaul-phase0/baseline-map0.json and
      baseline-map1.json exist (even if incomplete; partial coverage
      is acceptable if total time exceeds 1 hour).
  [ ] tmp/iter-overhaul-phase0/baseline-summary.md exists with
      affordance histograms and the 3-stall-coord lookup.
  [ ] tmp/iter-overhaul-phase0/test-baseline.md exists capturing
      the 4 long-pathing tests' current state.
  [ ] tmp/iter-overhaul-phase0/findings.md exists with the go/no-go
      recommendation.
  [ ] One git commit + push.

DO NOT proceed to Phase 1 (parameter overhaul) in this session.
Phase 0 ends with the baseline + go/no-go. If the user wants to
continue, they will start a new session with a Phase 1 prompt.

================================================================
FIRST ACTION
================================================================

Read the proposal doc end-to-end. Then read PathPhysicsProbe's
source. Then start Deliverable 1 (the new probe). Use the parallel
Agent tool to read multiple source files at once if useful.

If at any point you realize the proposal's design has a fatal flaw
you can prove with evidence from the codebase (not speculation),
STOP and surface to the user. Better to redesign at Phase 0 than
to discover the flaw at Phase 4 after weeks of work.

Good luck.
```

---

> End of kickoff prompt. The next session should be able to run the prompt block above standalone without any preamble from this file or from prior conversation.
