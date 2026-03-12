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
- [ ] Live validation deferred - needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

### NAV-FISH-001 Fix Ratchet shoreline terrain sticking / no-LOS approach points
- [ ] Problem: the fishing live test now reaches the correct Ratchet hole from a named teleport, but the bot can still snag on shoreline terrain or end at a cast target with no clean LOS to fishable water before `FishingTask in_cast_range`.
- [ ] Target files: `Exports/Navigation/PhysicsCollideSlide.cpp`, `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PathFinder.cpp`, replay/log evidence from `FishingProfessionTests`.
- [ ] Required change:
  1. Reproduce the short Ratchet shoreline approach with planned-vs-executed waypoint evidence from the pathfinding owners.
  2. Fix corridor/collide-slide behavior so the returned short route does not strand the bot on terrain or at a no-LOS endpoint.
  3. Validate against the fishing-hole approach first, then reuse the same diagnostics for other sporadic live pathing failures.
- [ ] Acceptance criteria: the short Ratchet shoreline route consistently reaches a castable, LOS-valid position without terrain sticking or hover/fall artifacts.

### NAV-OBJ-001 Integrate request-scoped dynamic objects into native path validation
- [ ] Problem: `DynamicObjectRegistry` exists and is already used by physics/LOS, but native path generation and path-validation flows do not yet treat caller-supplied live objects as first-class blockers during route shaping.
- [ ] Target files: `Exports/Navigation/Navigation.cpp`, `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/SceneQuery.cpp`, `Exports/Navigation/DynamicObjectRegistry.*`.
- [ ] Required change:
  1. Accept request-scoped dynamic-object overlays from the service layer.
  2. Use those overlays during segment validation and candidate-route rejection.
  3. Keep the overlay lifecycle deterministic so two bots cannot pollute each other's native obstacle state.
- [ ] Acceptance criteria: native route validation can say "this mmap segment is blocked by live object X" instead of pretending the object is not there.

### NAV-OBJ-002 Add capsule-clearance and support-surface validation for candidate segments
- [ ] Problem: LOS alone is insufficient for walkability. We need to know whether the character capsule can clear the segment and whether the destination/support surface is actually usable.
- [ ] Target files: `Exports/Navigation/SceneQuery.cpp`, `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PhysicsCollideSlide.cpp`, native exports as needed.
- [x] Progress (2026-03-12 session 68): added native `ValidateWalkableSegment` in `DllMain.cpp`. It uses `HorizontalSweepAdvance`, support-surface checks, and overlap rejection to classify `Clear`, `BlockedGeometry`, `MissingSupport`, `StepUpTooHigh`, and `StepDownTooFar`. Focused native tests now cover the export directly.
- [x] Progress (2026-03-12 session 69): `SceneQuery.cpp` now exposes capsule-footprint support selection through `GetCapsuleSupportZ(...)`, `ValidateWalkableSegment` uses that probe plus looser overlap tolerance, and short false-negative straight sweeps can fall back to `PhysicsStepV2` so the validator matches real collide-and-slide movement better. The first real Orgrimmar graveyard->center raw-path segment now passes in deterministic native coverage.
- [ ] Required change:
  1. Add reusable segment validation helpers for capsule clearance, support surface, and obstacle squeeze cases.
  2. Use the same walkability thresholds as the physics engine (`STEP_HEIGHT`, slope, step-down limits).
  3. Expose enough native results for the service layer to classify the segment.
- [ ] Acceptance criteria: the native layer can distinguish "visible" from "walkable with this capsule."

### NAV-OBJ-003 Surface-transition affordance classification
- [ ] Problem: the engine has step/jump/fall substrate, but route planning does not yet tag transitions as step-up, jump-gap, safe-drop, unsafe-drop, or blocked.
- [ ] Target files: `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PhysicsThreePass.cpp`, `Exports/Navigation/SceneQuery.cpp`, helper exports/tests.
- [ ] Required change:
  1. Reuse existing jump/fall/gap detection to classify candidate transitions.
  2. Emit quantitative metrics for climb height, gap distance, and drop height.
  3. Keep the classification consistent with actual movement execution, not a separate planner-only heuristic.
- [ ] Acceptance criteria: higher layers can ask the native stack what movement affordance a segment requires.

### NAV-OBJ-004 Local detour generation around collidable objects
- [ ] Problem: once a live object blocks an mmap path segment, we need a local workaround instead of failing the whole route immediately.
- [ ] Target files: `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/Navigation.cpp`, `Exports/Navigation/SceneQuery.cpp`.
- [ ] Required change:
  1. Generate short detour candidates around dynamic blockers using clearance-aware probes.
  2. Reject detours that only pass LOS but fail capsule/support checks.
  3. Return the best repaired route for service-side smoothing/re-optimization.
- [ ] Acceptance criteria: temporary blockers produce a valid workaround route whenever one exists within a local search envelope.

## Simple Command Set
1. `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented|stub" Exports/Navigation`

## Session Handoff
- Last updated: 2026-03-12 (session 69)
- Active task: `NAV-OBJ-002` promote the hardened validator from short-segment fallback into native route shaping
- Last delta: `SceneQuery.cpp` now has `GetCapsuleSupportZ(...)`, and `DllMain.cpp` uses it inside `ValidateWalkableSegment` together with physics-aligned overlap tolerance and a `PhysicsStepV2` fallback for short false-negative straight sweeps. `SegmentWalkabilityTests.cs` now covers the first real Orgrimmar graveyard->center raw-path segment and it passes as `Clear`, so the immediate short-segment false-negative is fixed even though longer route shaping still belongs in `PathFinder.cpp`
- Pass result: `delta shipped`
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `3 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `100 passed, 1 skipped`
- Files changed:
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/DllMain.cpp`
  - `Exports/Navigation/TASKS.md`
- Next command: `Get-Content Exports/Navigation/PathFinder.cpp | Select-Object -First 260`
- Blockers: short-segment false-negatives are now covered, but the fallback still lives in `ValidateWalkableSegment` rather than native route generation. Longer multi-segment corpse-run routes still need `PathFinder.cpp` / `SceneQuery.cpp` shaping so the service does not rely on post-hoc repair alone.
