<<<<<<< HEAD
﻿# Navigation.Physics.Tests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- This file owns calibration evidence and regression tests for movement parity.
- Corpse-run target stays `.tele name {NAME} Orgrimmar` with 10-minute guarded runtime.

## Scope
Directory: `Tests/Navigation.Physics.Tests`

## Initial Research Snapshot (2026-02-24)
1. Movement parity drift is still observed in runback logs as low/zero displacement loops with forward intent.
2. Current tests are not yet strong enough to prove frame-by-frame interpolation equivalence against FG traces.
3. `MovementController.cs` integration cadence can amplify native interpolation drift when replay timing is uneven.

## Active Priorities
1. Calibration baseline
- [ ] Build a shared FG vs BG replay corpus for corpse-run, combat pursuit, and gathering route segments.
- [ ] Add deterministic checks for per-frame position delta, velocity delta, and heading delta.
- [ ] Define fail thresholds for parity drift and enforce them in CI/local runs.

2. Frame-by-frame interpolation refinement
- [ ] Add targeted tests that fail on interpolation jitter/plateaus in consecutive frames.
- [ ] Validate interpolation behavior around turns, slope transitions, and stop-start motion.
- [ ] Correlate test failures directly to native `PhysicsEngine` frame integration issues.

3. Movement controller coordination
- [ ] Add tests that validate `MovementController.cs` frame dispatch cadence against native interpolation outputs.
- [ ] Verify command-to-movement latency does not create repeated zero-displacement frames.
- [ ] Record packet timing evidence for parity triage when failures occur.

4. Scenario gate support
- [ ] Provide calibration evidence updates for corpse-run validation cycles.
- [ ] Provide calibration evidence updates for combat/gathering parity cycles.

## Canonical Commands
1. Physics calibration suite:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

2. Movement-controller focused physics tests:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementController|FullyQualifiedName~FrameByFrame" --logger "console;verbosity=minimal"`

3. Corpse-run parity trigger:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.

## Session Handoff
- Last task completed:
- Validation/tests run:
- Files changed:
- Next task:

## Archive
Move completed items to `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md`.
=======
# Navigation.Physics.Tests Tasks

## Scope
- Directory: `Tests/Navigation.Physics.Tests`
- Project: `Navigation.Physics.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-023`)
- Local goal: make physics parity regressions deterministic, actionable, and fast to validate.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing fixture/data.
2. Keep every validation command one-line and runnable without custom wrappers.
3. Use `test.runsettings` for hard timeout enforcement on every command.
4. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
5. Archive completed IDs to `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md` in the same session.
6. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
7. Start each pass by running the previous `Session Handoff -> Next command` verbatim before any broader scan.
8. After shipping one local delta, set `Next command` to the next queue-file read command so one-by-one progression survives compaction.

## Environment Checklist (Run Before P0)
- [x] `Navigation.dll` is present for this test project (`Bot/$(Config)/net8.0/Navigation.dll`).
- [x] `WWOW_DATA_DIR` resolves to a root containing `maps/`, `vmaps/`, and `mmaps/` (auto-discovered from `AppContext.BaseDirectory` = `Bot/$(Config)/net8.0/`).
- [x] `Tests/Navigation.Physics.Tests/test.runsettings` is used (10-minute `TestSessionTimeout`, `TargetPlatform=x64`).

## Evidence Snapshot (2026-03-12)
- The physics calibration archive remains valid; replay parity work is still archived in `TASKS_ARCHIVE.md`.
- New native walkability coverage is now present for `ValidateWalkableSegment`:
  - zero-distance same-ground probe returns `Clear`
  - obstructed route returns a non-clear native classification
- A real Orgrimmar graveyard->center raw-path segment is now pinned here as a deterministic regression. The export now clears it only because the native validator was hardened to use capsule-footprint support probes plus a `PhysicsStepV2` fallback for straight-sweep false negatives.
- Whole-route Orgrimmar corpse-run coverage now also exists here. The route contract carries grounded segment ends forward between validations so the deterministic check matches how runtime movement actually consumes the path instead of trusting raw smooth-path Zs blindly.
- This project is now the deterministic owner for native segment-walkability diagnostics while `Exports/Navigation` hardens support-surface selection and native route shaping.

## P0 Active Tasks (Ordered)

No active tasks - all legacy P0 tasks remain completed. Current work is support coverage for native `NAV-OBJ-002`.

### Parity Routing (BRT-PAR-002)
BRT-PAR-001 parity loop (2026-02-28) found **no physics/navigation regressions**. All 4 live failures (gathering node visibility, FlightMaster NPC timing, quest snapshot sync, PathfindingService readiness) are routed to non-physics owners:
- World object visibility -> `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-001)
- NPC interaction timing -> `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-002)
- Quest snapshot sync -> `Services/WoWStateManager/TASKS.md` (WSM-PAR-001)
- PathfindingService readiness -> `Services/PathfindingService/TASKS.md` (PFS-PAR-001)

## Simple Command Set
1. Single project sweep: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Fast frame-loop verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
3. Teleport/fall verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
4. Native segment walkability focus: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-03-12 (session 72)
- Active task: keep native whole-route shaping regressions pinned while the service instruments Ratchet shoreline drift
- Last delta: `SegmentWalkabilityTests.cs` now covers two new native detour regressions: the Ratchet fishing shoreline route from the named-teleport dock anchor to the observed cast target, and a known obstructed direct segment that must reform into a walkable multi-point route. `PathFinder.cpp` now tries grounded lateral detour candidates before midpoint-only refinement, and the focused native slice now passes `7/7`.
- Pass result: `delta shipped`
- Files changed:
  - `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/PathFinder.cpp`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `7 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `104 passed, 1 skipped`
- Blockers: deterministic native detour coverage is now in place, but it still does not tell us whether remaining live Ratchet failures come from bad returned paths or drift after the path is issued. The next owner work is service-side shoreline request/response logging.
- Next command: `Get-Content Services/PathfindingService/PathfindingSocketServer.cs | Select-Object -Skip 140 -First 160`
>>>>>>> cpp_physics_system

## 2026-03-23 Session Note
- Pass result: `delta shipped`
- Local delta: added Undercity elevator replay transport coverage and fixed replay reset/state handling so transport entry/exit no longer pollute parity metrics.
- Validation:
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n` -> `29 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ElevatorRideV2_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~UndercityElevatorReplay_TransportAverageStaysWithinParityTarget" --logger "console;verbosity=detailed"` -> `2 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=detailed"` -> passed (`avg=0.0124y`, `p99=0.1279y`, `worst=2.2577y`)
- Files changed:
  - `Tests/Navigation.Physics.Tests/ElevatorScenarioTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
- Next command: `Get-Content Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs | Select-Object -Skip 100 -First 220`

## 2026-03-23 Session Note (swim parity)
- Pass result: `delta shipped`
- Local delta: added focused swim-path regressions for Durotar seabed collision and recorded water-entry damping, and the native swim replay now exercises real submerged collision instead of pure velocity integration.
- Validation:
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release` -> succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.DurotarRecording_WaterEntry_DampsHorizontalVelocity|FullyQualifiedName~FrameByFramePhysicsTests.DurotarSwimDescent_SeabedCollisionPreventsTerrainPenetration|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" -v n` -> `3 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~FrameByFramePhysicsTests.WestfallCoast_EnterWater_TransitionsToSwimming" -v n` -> `1 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementControllerPhysics" -v n` -> `29 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" -v n` -> passed
- Files changed:
  - `Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs`
- Next command: `Get-Content Tests/WoWSharpClient.Tests/Handlers/MovementPacketHandlerTests.cs | Select-Object -First 260`
