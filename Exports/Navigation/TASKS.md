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

### NAV-FISH-001 Fix Ratchet shoreline terrain sticking / no-LOS approach points
- [ ] Problem: the fishing live test now reaches the correct Ratchet hole from a named teleport, but the bot can still snag on shoreline terrain or end at a cast target with no clean LOS to fishable water before `FishingTask in_cast_range`.
- [ ] Target files: `Exports/Navigation/PhysicsCollideSlide.cpp`, `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PathFinder.cpp`, replay/log evidence from `FishingProfessionTests`.
- [x] Progress (2026-03-12 session 72): `PathFinder.cpp` now tries grounded lateral detour candidates before falling back to pure midpoint splitting, and deterministic native coverage now includes a Ratchet dock fishing-approach route (`-957.0,-3755.0,5.0 -> -956.2,-3775.0,0.0`) plus an obstructed direct-segment detour regression. This improves returned route shape, but it does not yet log planned-vs-executed shoreline drift or prove final cast LOS in the live task.
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
- [ ] Target files: `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/SceneQuery.cpp`, helper exports/tests.
- [ ] Required change:
  1. Reuse existing jump/fall/gap detection to classify candidate transitions.
  2. Emit quantitative metrics for climb height, gap distance, and drop height.
  3. Keep the classification consistent with actual movement execution, not a separate planner-only heuristic.
- [ ] Acceptance criteria: higher layers can ask the native stack what movement affordance a segment requires.

### NAV-OBJ-004 Local detour generation around collidable objects
- [ ] Problem: once a live object blocks an mmap path segment, we need a local workaround instead of failing the whole route immediately.
- [ ] Target files: `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/Navigation.cpp`, `Exports/Navigation/SceneQuery.cpp`.
- [x] Progress (2026-03-12 session 72): `PathFinder.cpp` now attempts grounded lateral detour candidates around blocked segments before falling back to midpoint refinement. Focused native coverage proves both the Ratchet shoreline fishing approach and a known obstructed direct segment now return multi-point walkable routes instead of trusting the blocked straight segment.
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
- Last updated: 2026-03-25 (session 189)
- Active task: `NAV-PAR-001` keep replacing non-binary-backed grounded query/slide heuristics until `CollisionStepWoW` matches the client’s merged-query plus post-`TestTerrain` wall/corner sequence
- Last delta:
  - Session 189 closed the first top-level `0x633840` mismatch instead of going deeper into the grounded helper immediately. Fresh disassembly captured in `docs/physics/0x633840_disasm.txt` shows the client checks the airborne helper (`test ah, 0x20`) before the swim helper (`test eax, 0x200000`), with grounded falling through only after both fail.
  - `PhysicsEngine.cpp` now enforces that same precedence in `StepV2`: `useAirbornePath` wins whenever airborne flags are present, even if `MOVEFLAG_SWIMMING` also overlaps on the same frame. Pure swim frames still route through `ProcessSwimMovement`.
  - Added deterministic regression `FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround`, which proves a dry-ground `FALLINGFAR | SWIMMING` frame descends like pure airborne motion and clears `MOVEFLAG_SWIMMING` in the output instead of being misrouted through the swim helper.
  - The rebuilt native DLL held the new precedence test, an existing jump-arc sanity check, the packet-backed swim replay, and the live redirect parity slice. This keeps the new branch-order cleanup isolated and green before the next grounded-helper pass.
  - Session 182 split the grounded `0x636100` helper choice in `PhysicsEngine.cpp`: `resolveWallSlide(...)` now treats the `0x635D80` horizontal correction and the `0x635C00` selected-plane projection as mutually exclusive branches instead of stacking both on sloped selected planes.
  - Session 182 also retargeted `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` to the promoted packet-backed elevator recording’s actual blocked window (`frames 11..19`) so the compact March 25 fixture remains the canonical upper-door regression.
  - Session 182 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL.
  - Session 180 shipped the missing selected-plane Z correction from the local `0x635C00` helper into `PhysicsEngine.cpp`: grounded wall resolution now carries a radius-clamped vertical correction from the primary blocker contact and uses that corrected support Z for the final `GetGroundZ(...)` query.
  - Session 180 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 179 shipped the smallest cleanly mapped `0x635D80` effect in `PhysicsEngine.cpp`: grounded wall resolution now adds the client’s `0.001f` horizontal pushout after the blocker-plane projection instead of leaving the resolved move exactly on the wall plane.
  - Session 179 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 178 corrected the grounded `0x636610` jump-table mapping in `PhysicsEngine.cpp`: the three-axis case now selects the lone axis from the minority orientation group, while the four-axis case zeroes the merged blocker vector.
  - Session 178 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 177 shipped one more binary-backed `0x636610` merge rule in `PhysicsEngine.cpp`: the grounded three-axis blocker case now zeroes the merged blocker vector instead of selecting the first surviving axis, matching the jump-table behavior seen in the local `WoW.exe` disassembly.
  - Session 177 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL, so the three-axis zero rule did not reopen the old false-wall or underground regressions.
  - Session 176 removed the grounded `score + 0.1` secondary-axis filter from `buildMergedBlockerNormal(...)`. The best opposing blocker axis still stays primary, but later distinct blocker axes now remain available to the existing `1 / 2 / 3+` merge path instead of being dropped by score threshold.
  - Session 176 also closed the stale wall-fixture blocker for the native parity loop: `PhysicsReplayTests` now has replay-backed terrain (`DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`), WMO (`BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls`), and dynamic-object (`PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`) wall regressions.
  - Session 175 attempted the next `0x6367B0` hypothesis by retrying grounded wall resolution once with the already-slid move, but that regressed `MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` to `3.26 y/s`; the change was reverted and recorded in `docs/physicsengine-calibration.md` as do-not-repeat.
  - Session 171 removed the remaining custom grounded wall-contact sort, added replay-backed regression `PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`, and corrected the local decomp note that `0x637330` is the vec3-negation helper rather than the grounded slide routine.
  - Session 172 corrected `0x6373B0` from “Collide” to the merged-AABB helper and updated grounded `CollisionStepWoW` so the wall query now uses the merged start/full/half-step `TestTerrainAABB` volume instead of accumulating synthetic full-step and half-step `SweepAABB` contacts.
- Pass result: `delta shipped`
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~FrameAheadIntegrationTests.JumpArc_FlatGround_PeakHeightMatchesPhysics|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `35 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `1 passed`
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs`
  - `docs/physics/0x633840_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
- Next command: `py -c "from capstone import *; f=open(r'D:/World of Warcraft/WoW.exe','rb'); f.seek(0x6334A0-0x400000); code=f.read(768); md=Cs(CS_ARCH_X86, CS_MODE_32); [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') or (i.address >= 0x633560 and i.mnemonic in ('ret','retn') and (_ for _ in ()).throw(SystemExit)) for i in md.disasm(code, 0x6334A0)]"`
- Blockers:
  - The exact grounded post-`TestTerrain` wall/corner resolution helper is still unresolved in the binary; the current stateless path now uses merged blocker-axis resolution on top of the correct merged query volume, but it still lacks the real `0x6334A0` walkability logic and the remaining `0x636100` return-code / movement-fraction bookkeeping around `0x635C00` / `0x635D80`.
  - Do not reintroduce the reverted two-pass remaining-move reprojection loop without new binary evidence; it is now a documented regression.
  - `0x6373B0` is closed as the merged-AABB helper; do not spend more time treating it as the missing collision/slide routine.
  - Verified replay-backed wall fixtures now exist, so the next native pass should use those fixtures instead of the stale Stormwind / RFC / Un'Goro coordinate probes.
  - Managed `MovementController` cadence and ownership parity still need a focused audit once the native grounded wall path is reduced to the client’s real post-query sequence.
