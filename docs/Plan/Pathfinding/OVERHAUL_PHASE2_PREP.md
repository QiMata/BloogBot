# Phase 2 Prep — Recast/Detour Vendor Upgrade Scope

> **Iter 6 of the Recast Physics-Validated Overhaul loop.**
> Read-only inventory of the recastnavigation vendor surface that Phase 2
> upgrades to v1.6.0. Per kickoff guardrail 1, no production code touched
> in Phase 0.

## Major surprise: TWO Detour copies, divergent

The proposal's Phase 2 section says: "Diff our current
`tools/MmapGen/dep/recastnavigation/` against upstream v1.6.0... Replace
verbatim... Verify `Exports/Navigation` still builds and links against
the new Detour ABI." This wording implies one vendored Detour shared
between bake and runtime. The actual repo state:

| Copy | LOC | Detour | Recast | Path |
|---|---|---|---|---|
| Bake-time | 20,270 | yes | yes | [`tools/MmapGen/dep/recastnavigation/`](../../../tools/MmapGen/dep/recastnavigation/) |
| Runtime | 9,383 | yes | no | [`Exports/Navigation/Detour/`](../../../Exports/Navigation/Detour/) |

`diff -rq` between the two Detour directories shows **EVERY file
differs**:
- All 9 header files in `Detour/Include/` differ.
- All 6 source files in `Detour/Source/` differ.
- Runtime has 1 extra file pair: `DetourPathCorridor.{h,cpp}` (the
  corridor-traversal helper Phase 5 deletes as part of the
  `NavigationPath.ShouldPreferAlternatePath` removal).
- Bake-time has 1 extra: `DetourAssert.cpp` (assert overrides for
  MmapGen-specific debug output).

Both copies advertise `DT_NAVMESH_VERSION = 7` but their
implementations differ — so the wire format is "compatible by accident"
not by guarantee. The current bake/runtime pair works because both
copies happen to be backwards-compatible with the same on-disk tile
format, not because they're version-locked.

**Phase 2's actual scope is bigger than the proposal stated:** both
copies must be replaced with v1.6.0 Detour, and the divergence between
them is itself a latent bug that v1.6.0 eliminates by making the
version single-sourced.

## Current vendor lineage

`tools/MmapGen/dep/recastnavigation/recastnavigation.diff` is a 104-line
patch dated **2014-06-20** by jackpoz (TrinityCore contributor). The
header reads:

```
From: jackpoz <giacomopoz@gmail.com>
Date: Fri, 20 Jun 2014 23:15:04 +0200
Subject: [PATCH] Add custom trinitycore changes

 Detour/Include/DetourNavMesh.h       | 24 ++++++++++++++++++------
 Detour/Source/DetourNavMeshQuery.cpp |  4 ++--
 Recast/Include/Recast.h              |  4 ++--
 3 files changed, 22 insertions(+), 10 deletions(-)
```

So **the bake-time vendor is recastnavigation from ~2014** (jackpoz's
TrinityCore fork) with the 104-line diff applied on top. v1.6.0 was
released May 2024 — **~10 years of upstream fixes accumulated**.

The runtime `Exports/Navigation/Detour/` lineage is undocumented but
based on the file-line-number drift (`DT_NAVMESH_VERSION` at line 73
vs the bake-time copy's line 85) it's also pre-2014 with different
patches.

## Critical custom changes to preserve

The 2014 jackpoz diff enables `DT_POLYREF64` by default:

```c
//#define DT_POLYREF64 1  // upstream comment, disabled
#define DT_POLYREF64 1     // jackpoz: force-enable
```

And adds a cross-platform `uint64_d` typedef hack. **v1.6.0 likely
supports DT_POLYREF64 cleanly as a build define** — Phase 2 verifies
this and switches from "patched header" to "compile-time -DDT_POLYREF64".

If `DT_POLYREF64` is OFF (32-bit polyref): we lose addressing for
worlds >100 maps. WoW Vanilla has 41+ maps; with future content
expansion the 32-bit polyref would saturate. KEEP `DT_POLYREF64`.

## DetourPathCorridor — defer to Phase 5

`Exports/Navigation/Detour/Include/DetourPathCorridor.h` +
`Source/DetourPathCorridor.cpp` are the corridor-traversal helper that
the runtime's `NavigationPath.ShouldPreferAlternatePath` depends on.
The proposal §3 Phase 5 explicitly deletes the
corridor-fallback machinery — so `DetourPathCorridor.*` is **deleted in
Phase 5**, NOT Phase 2.

Phase 2's job for these files: include them unchanged in the runtime
v1.6.0 vendor copy (if v1.6.0 still ships them), keep building. Phase
5 deletes them in the same commit that deletes the corridor-fallback
caller.

## Exports/Navigation native consumers — API audit

Phase 2 must verify that the runtime's C++ wrappers continue to
compile and link. Key entrypoints from [`Exports/Navigation/PathFinder.cpp`](../../../Exports/Navigation/PathFinder.cpp)
and [`DllMain.cpp`](../../../Exports/Navigation/DllMain.cpp) (read in
iter 1 audit; not re-listed here) call:

- `dtNavMeshQuery::findPath` — stable signature 1.5→1.6.0.
- `dtNavMeshQuery::findStraightPath` — stable.
- `dtNavMeshQuery::findNearestPoly` — **changed signature in 1.6.0**:
  new `isOverPoly` out-parameter (proposal §1.2). Both copies must
  add the new arg site.
- `dtNavMesh::tileAt`, `getTileByRef`, polyRef encode/decode — stable.
- `dtRaycast` — stable.

ABI changes that affect tile binary format (DT_NAVMESH_VERSION bump
1.6.0?) require re-baking all 41 maps. Phase 1's full re-bake already
does this, so the version bump is free if it lands before Phase 1
completes.

## Estimated Phase 2 scope

| Work unit | Wall-clock | Iters |
|---|---|---|
| Download upstream v1.6.0 + verify against repo hash | 0.5 hr | 1 |
| Replace `tools/MmapGen/dep/recastnavigation/` (20K LOC) verbatim; reapply DT_POLYREF64 as build define | 2-3 hr | 1 |
| Replace `Exports/Navigation/Detour/` (9.4K LOC) with v1.6.0 Detour subset; keep DetourPathCorridor.* untouched | 2-3 hr | 1 |
| Resolve `findNearestPoly`-style API breakage in PathFinder.cpp / DllMain.cpp | 1-2 hr | 1 |
| Build + link `Exports/Navigation` (native + managed P/Invoke) | 1-2 hr | 1 |
| Phase 0 probe re-run on representative tiles; compare tile-border Blocked-poly ratios | 1-2 hr | 1 |

**Phase 2 total estimate:** 8-13 hr wall-clock, 4-6 iters. Runs only
after Phase 1 completes (sequencing per proposal §7).

## Phase 2 acceptance criteria (per proposal §3)

- `tools/MmapGen/dep/recastnavigation/` is upstream v1.6.0 verbatim.
- `Exports/Navigation` builds + links.
- PathfindingService runtime queries succeed (managed P/Invoke layer
  built against new DllMain.cpp + native rebuild).
- Phase-0 probe shows tile-border Blocked polygons (within `borderSize`
  voxels of tile edge) drop ≥50%.

## What this iter does NOT find

This prep doc deliberately does NOT download v1.6.0 yet — Phase 0 is
read-only. The expected workflow when Phase 2 starts:

1. `git clone https://github.com/recastnavigation/recastnavigation.git
   --depth 1 --branch v1.6.0 /tmp/recast-1.6.0` (or download tarball).
2. Diff the vendored files against the upstream subset for change-set
   review (the 10-year delta will be substantial; this is the
   review-and-merge step).
3. Replace in-place; preserve `tools/MmapGen/dep/recastnavigation/CMakeLists.txt`
   build glue; preserve the DT_POLYREF64 enabling as a CMake define.

## Phase 2 risk flags

| Risk | Likelihood | Mitigation |
|---|---|---|
| v1.6.0 changes Detour wire format (DT_NAVMESH_VERSION ≠ 7) | High (10 years of changes) | Phase 1's full re-bake already produces all-new tiles; just verify both copies write the same new version. |
| v1.6.0 doesn't support DT_POLYREF64 cleanly as build define | Low | If forced to inline the typedef hack, treat as a maintained patch in `recastnavigation.diff`. |
| `Exports/Navigation/Detour/`'s untracked custom changes (no `.diff` file documents the deltas) break under v1.6.0 | Medium | Phase 2's first commit must catalogue every custom change in both copies; lost-and-found discovery work. |
| `DetourPathCorridor.*` API depends on internal Detour types that v1.6.0 changed | Medium | Defer: Phase 5 deletes these anyway. If v1.6.0 build breaks here, mark them `#if 0` until Phase 5 deletes. |
| Runtime managed P/Invoke ([`Tests/Navigation.Physics.Tests/NavigationInterop.cs`](../../../Tests/Navigation.Physics.Tests/NavigationInterop.cs)) has C export signature changes | Low | NavigationInterop.cs only consumes C-export-style signatures (DllImport), insulated from Detour C++ API churn. |
