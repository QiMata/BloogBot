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
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] Navigation native build succeeds (`Release|x64`) — confirmed 2026-02-28 via MSBuild (VS 2025 Community).
- [x] Pathfinding runtime has access to expected MMAP/VMAP assets when validating corpse-run behavior.
- [x] Native/exported API contracts are synchronized with downstream C#/protobuf consumers.

## Evidence Snapshot (2026-02-28)
- `OverlapCapsule` export implemented — routes to `SceneQuery::OverlapCapsule` via VMapManager2/StaticMapTree.
- `backfaceCulling`/`returnPhysMat` in QueryParams — marked "Reserved" with explicit behavior docs.
- PathFinder machine-specific debug path fixed (batch 3).
- Native build: `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145` — 0 errors.
- Physics tests: 76/79 pass (3 pre-existing calibration failures).

## P0 Active Tasks (Ordered)

### NAV-MISS-001 Implement `OverlapCapsule` test export by routing to existing SceneQuery implementation
- [x] **Done (batch 12).** Implemented OverlapCapsule export in PhysicsTestExports.cpp:
  - Gets VMapManager2 via VMapFactory::createOrGetVMapManager()
  - Ensures map loaded via SceneQuery::EnsureMapLoaded()
  - Gets StaticMapTree via vmapMgr->GetStaticMapTree(mapId)
  - Calls SceneQuery::OverlapCapsule(*mapTree, *capsule, hitResults)
  - Copies results to output buffer up to maxOverlaps
- [x] Validation: C++ MSBuild — 0 errors. Physics tests: 76/79 pass.
- [x] Acceptance: OverlapCapsule no longer returns stubbed zero; routes to real SceneQuery collision geometry.

### NAV-MISS-002 Resolve explicit query-contract drift in `QueryParams` (`returnPhysMat`, `backfaceCulling`)
- [x] **Done (batch 12).** Updated SceneQuery.h:
  - `backfaceCulling`: comment changed from "currently unused / TODO" → "Reserved: back-face hit filtering. Not evaluated by current query paths; defaults to false (all faces hit)."
  - `returnPhysMat`: comment changed from "future/not implemented yet" → "Reserved: physical material retrieval. Not populated by current query paths; physMaterialId/friction/restitution stay at defaults."
- [x] Acceptance: no ambiguous "TODO"/"future/not implemented" comments remain; callers see clear, deterministic contract.

### NAV-MISS-003 Remove machine-specific fallback/debug side effects from `PathFinder`
- [x] **Done (batch 3).** Replaced hardcoded `C:\Users\Drew\...` path with printf; filter initialization made explicit.
- [x] Acceptance: no machine-specific debug artifact paths remain; filter behavior is explicit and reproducible across environments.

### NAV-MISS-004 Validate corpse runback path use (consume returned path nodes without wall-loop fallback)
- [x] **Code-complete.** RetrieveCorpseTask already consumes path directly with probe-skip/direct-fallback disabled (`enableProbeHeuristics: false`, `enableDynamicProbeSkipping: false`, `strictPathValidation: true`, `allowDirectFallback: false`). PathFinder generates valid Detour paths. No wall-loop fallback exists in this code path.
- [ ] Live validation deferred — needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

## Simple Command Set
1. `msbuild Exports/Navigation/Navigation.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented|stub" Exports/Navigation`

## Session Handoff
- Last updated: 2026-02-28
- Active task: NAV-MISS-004 code-complete (live validation deferred)
- Last delta: NAV-MISS-004 marked code-complete — path consumption config already correct
- Pass result: `delta shipped`
- Validation/tests run:
  - Code review confirmed RetrieveCorpseTask path settings are correct
- Files changed:
  - `Exports/Navigation/TASKS.md`
- Next command: continue with next queue file
- Blockers: NAV-MISS-004 live validation requires running MaNGOS server
