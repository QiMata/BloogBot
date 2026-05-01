# Navigation Tasks

## Scope
- Project: `Exports/Navigation`
- Owns native pathfinding, collision queries, and physics integration consumed by pathfinding/physics services and tests.
- This file tracks first-party implementation gaps only (exclude third-party vendor TODOs under `Detour/` and `g3dlite/`).
- Master tracker: `MASTER-SUB-007`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep scans scoped to `Exports/Navigation` and related direct test projects only.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/Navigation/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to the next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] Navigation native build succeeds (`Release|x64`) - confirmed 2026-03-12 via MSBuild (VS 2025 Community).
- [x] Pathfinding runtime has access to expected MMAP/VMAP assets when validating corpse-run behavior.
- [x] Native/exported API contracts are synchronized with downstream C#/protobuf consumers.

## Evidence Snapshot (2026-02-28)
- `OverlapCapsule` export implemented - routes to `SceneQuery::OverlapCapsule` via `VMapManager2/StaticMapTree`.
- `backfaceCulling` / `returnPhysMat` in `QueryParams` are marked "Reserved" with explicit behavior docs.
- `PathFinder` machine-specific debug path fixed (batch 3).
- Native build: `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> 0 errors.
- Physics tests: 76/79 pass (3 pre-existing calibration failures).

## P0 Active Tasks (Ordered)

### NAV-PAR-001 PhysicsEngine parity with original WoW.exe grounded movement
- [x] Session 199: `SceneQuery::EnsureMapLoaded(...)` now upgrades legacy metadata-less `.scene` caches instead of treating them as the steady-state runtime path. It rebuilds the same cached bounds through `SceneCache::Extract(...)`, writes back a v2 cache, and loads the metadata-bearing result, which makes the normal production autoload path return the same frame-16 WMO-group blocker identity (`rootId=1150`, `groupId=3228`, `groupFlags=0x0000AA05`, `selectedMetadataSource=2`) that the fresh-extract proof already showed.
- [x] Session 198: `SceneCache` now preserves per-triangle WMO-group metadata on fresh extracts and through the deterministic `.scene` round-trip path. The packet-backed Undercity frame-16 blocker still selects instance `0x00003B34`, but a fresh bounded extract now proves that selected triangle is `rootId=1150`, `groupId=3228`, `groupFlags=0x0000AA05`, and `selectedMetadataSource=2` after unload/reload. Practical implication: no more MPQ extraction is needed for this blocker; the next runtime fix is getting the normal scene-load path onto the same metadata-bearing cache data.
- [x] Session 197: extended the selected-contact trace with `selectedResolvedModelFlags` and `selectedMetadataSource`, plus a best-effort child doodad match against the parent WMO's default `.doodads` set. The packet-backed Undercity frame-16 blocker still resolves as metadata source `1` (`parent instance`) with `resolvedModelFlags = 0x00000004`, which means post-hoc lookup on the collapsed contact is not enough; the next native fix has to preserve child WMO/M2 metadata earlier in `SceneCache` / `TestTerrainAABB`.
- [x] Session 196: extended the production-DLL grounded-wall trace seam to resolve selected-contact static metadata, which answered the open "more MPQ extraction?" question with binary-backed evidence. The packet-backed Undercity frame-16 blocker still resolves only to parent WMO instance `0x00003B34` with `instance/model flags = 0x00000004` and `rootWmoId = 1150`, while no WMO group match is found for the exact contact triangle. Practical implication: the current `SceneCache` / `TestTerrainAABB` path is preserving geometry but collapsing the deeper child WMO/M2 identity that `0x5FA550` appears to walk, so the next native parity unit is metadata preservation, not more raw triangle extraction.
- [x] Session 192: added a deterministic frame-15 Undercity upper-door contact probe around the production `Navigation.dll`. `QueryTerrainAABBContacts(...)` now exposes the merged `TestTerrainAABB` contact set, and the new tests prove the elevator deck support face is present at the failing frame with a signed downward normal and raw `walkable=0`. They also prove `0x6334A0` only promotes that face on its stateful path, which means the missing runtime piece is the binary selected-contact / `0xC4E544` state path, not a blanket `contact.walkable -> CheckWalkable` replacement.
- [x] Session 191: captured `0x6721B0` / `0x637330` in `docs/physics/0x6721B0_disasm.txt` and aligned `TestTerrainAABB` to emit signed box-relative contact normals instead of upward-flattened ones. The pure `0x6334A0` helper now consumes that signed contact feed, new deterministic orientation tests pin the behavior, and the focused grounded/runtime slices plus both live Durotar parity routes stayed green.
- [x] Session 190: disassembled `0x6334A0` `CheckWalkable`, captured its helper semantics in `docs/physics/0x6334A0_disasm.txt`, and added raw contact triangle/plane data plus a pure `WoWCollision::CheckWalkable(...)` helper with deterministic tests. A direct runtime hookup regressed the live Durotar parity routes and was reverted, so the shipped delta stops at binary evidence, test seams, and deterministic coverage until `TestTerrain` contact orientation / `0x637330` parity is fixed.
- [x] Session 189: top-level `0x633840` branch precedence documented and enforced in `StepV2`. Airborne now wins over swim when both states overlap, matching the binary's `0x633A29` -> `0x633B5E` order.
- [x] Session 188: Disassembled `0x6367B0` and implemented binary-backed retry loop (up to 5 iterations, exit < 1.0f yard). Also documented `0x636100` return codes and `0x636610` merge logic.
- [x] Session 188: Remaining heuristic thresholds audited against binary. `0x636610` uses integer jump-table; our float approximations match.
- [x] Build verified real wall regressions on terrain, WMO, and dynamic-object geometry.
- [x] All 30 `MovementControllerPhysics` + aggregate drift gate + wall replay fixtures green after retry loop implementation.

### NAV-MISS-001 Implement `OverlapCapsule` test export by routing to existing `SceneQuery` implementation
- [x] Done (batch 12). Implemented `OverlapCapsule` export in `PhysicsTestExports.cpp`:
  - gets `VMapManager2` via `VMapFactory::createOrGetVMapManager()`
  - ensures the map is loaded via `SceneQuery::EnsureMapLoaded()`
  - gets `StaticMapTree` via `vmapMgr->GetStaticMapTree(mapId)`
  - calls `SceneQuery::OverlapCapsule(*mapTree, *capsule, hitResults)`
  - copies results to the output buffer up to `maxOverlaps`
- [x] Validation: C++ MSBuild -> 0 errors. Physics tests: 76/79 pass.
- [x] Acceptance: `OverlapCapsule` no longer returns stubbed zero; it routes to real scene-query collision geometry.

### NAV-MISS-002 Resolve explicit query-contract drift in `QueryParams` (`returnPhysMat`, `backfaceCulling`)
- [x] Done (batch 12). Updated `SceneQuery.h`:
  - `backfaceCulling`: now documented as reserved back-face hit filtering
  - `returnPhysMat`: now documented as reserved physical-material retrieval
- [x] Acceptance: no ambiguous TODO/future comments remain; callers see a deterministic contract.

### NAV-MISS-003 Remove machine-specific fallback/debug side effects from `PathFinder`
- [x] Done (batch 3). Replaced hardcoded `C:\Users\Drew\...` path with printf; filter initialization made explicit.
- [x] Acceptance: no machine-specific debug artifact paths remain; filter behavior is explicit and reproducible across environments.

### NAV-MISS-004 Validate corpse runback path use (consume returned path nodes without wall-loop fallback)
- [x] Code-complete. `RetrieveCorpseTask` already consumes the path directly with probe-skip/direct-fallback disabled (`enableProbeHeuristics: false`, `enableDynamicProbeSkipping: false`, `strictPathValidation: true`, `allowDirectFallback: false`). `PathFinder` generates valid Detour paths. No wall-loop fallback exists in this code path.
- [x] Live validation passed (session 188): `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` green with navtrace ownership assertion.

## Simple Command Set
1. `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented|stub" Exports/Navigation`

## Session Handoff
- Last updated: 2026-05-01
- Pass result: `delta shipped; MMAP/Recast audit now proves current GO evidence and Tauren-clearance gap`
- Active task: `LPATH-CROSSROADS-UC` navmesh generation source-of-truth slice
- Last delta:
  - Added `tools/NavDataAudit` to inspect the generated nav data directly:
    `config.json`, selected Orgrimmar `.mmtile` Detour headers,
    `temp_gameobject_models`, `gameobject_spawns.json`, and `map1_build.log`.
  - Added `docs/physics/MMAP_NAVMESH_GENERATION.md` with the required
    Tauren Male generation settings: `agentRadius=1.0247`,
    `agentHeight=2.625`, `walkableRadius=4`, and `walkableHeight=11`.
  - The audit proves the current Orgrimmar GO inputs/build-log evidence are
    present, but generated tile headers still report `walkableRadius=0.2` and
    `walkableHeight=1.5`.
- Validation:
  - `dotnet build tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -v:minimal` -> `succeeded`
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data` -> `failed as expected; GO evidence passed, Tauren radius/height evidence failed`
- Next command:
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --no-restore -- D:/MaNGOS/data`

---

- Last updated: 2026-04-30
- Pass result: `delta shipped; deterministic Tauren Male Crossroads -> Undercity route suite passed`
- Active task: `LPATH-CROSSROADS-UC` native agent-aware path construction slice
- Last delta:
  - Added exported `FindPathForAgent(...)` and `Navigation::CalculatePathForAgent(...)` so callers can supply capsule radius/height per request.
  - `PathFinder` now carries the active capsule dimensions through smooth-path wall-clearance nudging, dynamic-overlay refinement, walkability segment validation, detour refinement, and simplification.
  - Replaced the old edge-snap nudge with Detour `findDistanceToWall(...)` clearance sampling guarded against non-finite wall/nearest-poly outputs.
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> `succeeded`
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routes_tauren_agent_collapsed_support.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`
- Next command:
  - `git status --short --branch`
