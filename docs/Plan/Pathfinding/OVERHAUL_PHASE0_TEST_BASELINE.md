# Phase 0 D3 — Long-Pathing Test Baseline Manifest

> **Iter 4 of the Recast Physics-Validated Overhaul loop.**
> Built from loop-25/26 iter-1/2 evidence per kickoff's "If you must skip,
> use iter-2's evidence in NEXT_SESSION_LONGPATHING_ITER2_FINDINGS.md and
> the iter-2 memory entry as the captured state."
> Companions: [`OVERHAUL_LOOP_STATUS.md`](OVERHAUL_LOOP_STATUS.md),
> [`OVERHAUL_PHASE0_STALL_COORDS.md`](OVERHAUL_PHASE0_STALL_COORDS.md).

## Purpose

Capture each long-pathing live test's current state — pass/fail status,
failure mode, classification (pathfinding-class vs non-pathfinding-class),
and stall-coord polyref linkage — so iter 7+ can measure Phase 1-4 progress
against a frozen baseline.

## The four tracked tests

| # | Test | File:line | Class | Current | Linked stall |
|---|---|---|---|---|---|
| T1 | `CrossroadsToUndercity_UsesFlightAndZeppelin` | [LongPathingTests.cs:255](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs#L255) | Pathfinding-class | 🔴 FAIL | iter-2 OG-interior (1608.1,-4382.3,10.0) |
| T2 | `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` | [LongPathingTests.cs:157](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs#L157) | NON-pathfinding | 🔴 FAIL | — (transport schedule phase) |
| T3 | `OgZeppelin_BakeFixtureValidation` | [LongPathingTests.cs:2049](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs#L2049) | Pathfinding-class | 🟢 PASS (post-iter-2 revert) | OG zep deck (tile 40,29) |
| T4 | `BrmDungeon_BakeFixtureValidation` | [LongPathingTests.cs:2070](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs#L2070) | Pathfinding-class | 🟢 PASS | BRM interior |

State frozen as of commit `7ca9f84c` (the loop-26 iter-2 closeout — last
pathfinding work prior to the overhaul). The overhaul itself has not
changed any code in `tools/MmapGen/`, `Exports/Navigation`, or the
runtime `Services/PathfindingService` yet, so this baseline persists.

## Per-test detail

### T1 — `CrossroadsToUndercity_UsesFlightAndZeppelin`

**Status:** 🔴 FAIL — bot stalls at WoW `(1608.1, -4382.3, 10.0)` inside
an OG building sub-floor pocket on tile (40, 28).

**Evidence:**
- loop-26 iter-1 (commit `c19c51b8`) added an off-mesh bypass on tile
  (39, 28) for the east-wall stall at (1627.6, -4151.8, 36.9). Bot
  traversed 230y past that stall to the new (1608.1, -4382.3, 10.0)
  stall — multi-stall corridor exposed.
- loop-26 iter-2 attempted a second off-mesh on tile (40, 28). Off-mesh
  registered correctly, route shrank from 25 corners → 12 corners
  (StepUp 26.8° Clear instead of BlockedGeometry on segment 0). BUT
  the cull blast radius regressed `OgZeppelin_BakeFixtureValidation`
  (7 TELEPORT_FAILED + 1 boarding drift on the OG zep deck, tile 40,29).
  Tile (40, 28) directly borders tile (40, 29); cross-tile cull seam
  propagation broke OG zep. Reverted via backup `.preiter2-*.bak`;
  bake-fixture pair re-passed 2/2.

**Failure mode:** Wall-collision creep on bake-vs-physics gap. The smooth
path through `(1608.1, -4382.3, 10.0)` classifies as ALL Walk Clear under
`PathPhysicsProbe --detour-resolve --smooth`, but the bot stalls there
because the runtime PhysicsEngine sees a wall the navmesh filtered out.

**Stall polyref link:** Per iter-2 of the overhaul (commit `dce88162`),
`--cull-coord 1608.1,-4382.3,10.0 --cull-coord-z-radius 15
--cull-coord-xy-radius 2` finds **7 unique polyrefs** at this coord:
`281475310158323`, `281475310158328`, `281475310158332`, `281475310158334`,
`281475310158335`, `281475310158337`, `281475310158340`. The 7-poly
Z-stack is the classic loop-19 cull-pipeline-blocker signature: WMO
interior with overlapping walkable polygons at slightly different Z.

**Path to fix:** Phase 4 per-poly-per-edge sweep at bake time. The
existing polys will either be culled (their edges classify as Blocked
under capsule sweep) or inset-repaired (the proposal's Layer-3 repair
pass). After Phase 4 + 5 (runtime simplification), this test should pass.

### T2 — `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes`

**Status:** 🔴 FAIL — NON-pathfinding-class per guardrail 10.

**Evidence (loop-26 iter-2):** Bot staged at OG boarding coord
`(1320.142944, -4653.158691, 53.891945)` on map 1. TravelTo dispatched
toward Undercity. TravelTask correctly identified the route as Zeppelin
and emitted `[FG:CHAT] [TRAVEL_LEG] start index=0 type=Zeppelin map=1
end=(2066.9, 290.1, 97.0)`. **88 `[TRAVEL_TRANSPORT]` traces** across
5m19s of waiting — ALL with `phase=WaitingForArrival; transport=0x0;
near=0; displayNear={0|1}`. Across the 5m19s window the expected zep
entry **164871** (OG↔UC zep, displayId=3031) NEVER appeared in the
bot's `nearbyObjects` feed. The OG↔Grom'gol zep (entry **175080**, same
displayId=3031) was continuously visible and correctly rejected by
`TransportObjectIdentity.MatchesTransport`.

**Failure mode:** Bot's `ObjectManager` and snapshot path are working
correctly; the issue is server-side. vmangos `mangos.transports` entry
164871 has `period=360016ms` (`build=0`) / `356284ms` (`build=4695`);
whether vmangos's `period` is per-direction (12-min total) or full
round-trip (6-min total) determines whether the test's 7-min wait window
catches one OG-dock arrival. Likely the test was lucky in earlier runs.

**Test infra quirk:** The failure assertion "missed boarding" message
comes from `HasMissedBoardingDiagnostic` which scans
`RecentChatMessages` for `[TRAVEL_TRANSPORT_MISSED_BOARDING]`. The 88
traces this run contained ZERO such emits — the predicate picked up a
stale chat message from a prior test that lingered in the buffer.
Real failure is the 7-min wait timeout; stale chat fast-fails first.

**Path to fix (NOT this overhaul):** Per guardrail 10, extend
`OrgrimmarUndercityZeppelinDockWaitSeconds` from `120` to `540` at
[LongPathingTests.cs:139](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs#L139).
That's a tiny separate commit acceptable within Phase 5. The
overhaul does NOT need to attempt a bake fix for this test.

**Stall polyref link:** N/A — this is not a pathfinding failure.

### T3 — `OgZeppelin_BakeFixtureValidation`

**Status:** 🟢 PASS — 11/11 walkable + 1/1 hole as of post-iter-2 revert.

**Gating:** `WWOW_OG_ZEP_BAKE_FIXTURE=1` env var.

**Evidence:** loop-26 iter-2 caught a regression in this test under the
attempted iter-2 off-mesh (7 TELEPORT_FAILED + 1 boarding drift on
OG zep deck checkpoints). The revert restored 11/11 walkable + 1/1
hole — the bake-fixture protects the OG zep deck-edge geometry. Tiles
involved: `0012940.mmtile` (OG zep deck tile per loop-26 iter-1 memory
correction), neighbors at (40, 29).

**Pathfinding-class significance:** This is the **canary** — the OG zep
deck-lip geometry is where the loop-24 deck-edge stall first appeared,
and loop-17e + Cycle-16 + Cycle-17e all fought it. The fixture asserts
walkability at 11 specific checkpoint coords; any bake regression that
breaks the deck-edge polygon connectivity surfaces here.

**Stall polyref link:** Per OG zep CLAUDE.md guidance (commit `c19c51b8`
through `7ca9f84c` history), the canonical deck-edge stall is around
WoW `(1338.1, -4646.0, 51.6) → (1335.2, -4644.4, 53.5)`. This area is
already validated by the fixture; iter 5+ should add a per-stall coord
probe to the baseline for it.

### T4 — `BrmDungeon_BakeFixtureValidation`

**Status:** 🟢 PASS — 2/2 walkable + 0 holes.

**Gating:** `WWOW_BRM_BAKE_FIXTURE=1` env var.

**Pathfinding-class significance:** BRM dungeon interior bake validation.
Used as the secondary canary alongside T3. Has consistently passed across
loops 24-26 — no known stall in this area.

**Stall polyref link:** None — currently clean.

## Cross-tile failure adjacency map

A key loop-26 iter-2 learning, summarized: tile (X, Y) bake changes can
break adjacent tile bake-fixtures via cross-tile portal seam degradation,
EVEN IF the modification's endpoints are nowhere near the fixture coords.

| Tile | Holds | Adjacency risk |
|---|---|---|
| (39, 28) | iter-1 east-wall + loop-25 doodad-wall stalls | (40, 28) ortho, (40, 29) diag |
| (40, 28) | iter-2 OG-interior stall | (39, 28) ortho, (40, 29) ortho — **HIGH** (broke T3 in iter-2) |
| (40, 29) | OG zep deck (T3 checkpoints) | (39, 29), (40, 28), (41, 29) ortho |

Phase 1-3 work that modifies global bake parameters will touch all tiles
simultaneously — the cross-tile-seam validator from Phase 6 was designed
to catch exactly this class. Phase 4's per-tile validation pass produces
deterministic per-tile output, so cross-tile concerns surface only at
border polys, not interior — that's the right shape for the overhaul.

## Test classifications summary

- **Overhaul-fixable (pathfinding-class):** T1, T3, T4 → 3 of 4. The overhaul's
  Phase 4-5 work is the path to "T1 turns 🟢, T3 + T4 stay 🟢."
- **Out-of-scope (non-pathfinding-class):** T2 → 1 of 4. Per guardrail 10,
  the fix is a 1-line constant change at LongPathingTests.cs:139, NOT a
  bake/runtime change. Will be bundled into Phase 5's wrap-up commit.

## What the overhaul should NOT do for these tests

- Do NOT add off-mesh entries to address T1. The cull blast radius
  (loop-26 iter-2 negative result) makes this brittle, especially for
  tile (40, 28) adjacent to OG zep tile (40, 29).
- Do NOT modify `OrgrimmarUndercityZeppelinDockWaitSeconds` until Phase 5's
  wrap-up commit. Premature change masks Phase 4 progress signal.
- Do NOT skip the bake-fixture pair after ANY tile bake (guardrail 3).
  The iter-2 evidence proves it catches real regressions.

## Iter 5+ work spawned

- **Per-edge classification for T1's 7 stall polys.** PathPhysicsProbe-driven
  loop over each stall polyref's cardinal-edge offsets. ~30 min of work
  once the all-tiles sweep completes.
- **Per-edge classification for T3's OG zep deck-edge polys.** Same pattern
  at the canonical loop-24 deck-edge coord.
- **D4 go/no-go findings** — synthesizes D1+D2+D3 into one go/no-go for
  advancing to Phase 1. Should land in iter 6-7 after the all-tiles sweep
  finishes.
