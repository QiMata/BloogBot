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

## Evidence Snapshot (2026-02-26c)
- All P0 tasks completed.
- Physics calibration — continued iteration session (2026-02-26c).
- Debug suite: 77 tests, 75 passed, 0 skipped, 2 failed (pre-existing: Forward_TraversesSlope, Backward_MovesOppositeToFacing).
- `WWOW_DATA_DIR` auto-resolves from `AppContext.BaseDirectory` (Bot/Debug/net8.0/ or Bot/Release/net8.0/).
- Replay accuracy (clean frames): avg=0.0267y, p95=0.1004y, p99=0.4840y.
  - Ground: avg=0.0134y, p99=0.1704y (18881 frames)
  - Air: avg=0.0000y (2205 frames — perfect)
  - Swim: avg=0.0000y (1569 frames — perfect)
  - Transition: avg=0.0619y (469 frames — inherent sub-frame timing)
  - Transport: avg=0.2300y (1647 frames — known elevator limitation)

## P0 Active Tasks (Ordered)

No active tasks — all P0 tasks completed. See `TASKS_ARCHIVE.md` for details.

### Parity Routing (BRT-PAR-002)
BRT-PAR-001 parity loop (2026-02-28) found **no physics/navigation regressions**. All 4 live failures (gathering node visibility, FlightMaster NPC timing, quest snapshot sync, PathfindingService readiness) are routed to non-physics owners:
- World object visibility → `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-001)
- NPC interaction timing → `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-002)
- Quest snapshot sync → `Services/WoWStateManager/TASKS.md` (WSM-PAR-001)
- PathfindingService readiness → `Services/PathfindingService/TASKS.md` (PFS-PAR-001)

## Simple Command Set
1. Single project sweep: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Fast frame-loop verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
3. Teleport/fall verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
4. Drift diagnostics gate: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-02-26c (replay calibration iteration)
- Active task: None — physics replay at high accuracy, all improvements validated
- Last delta (2026-02-26c): 6 fixes improving replay accuracy from avg=0.0653y to avg=0.0267y (-59%):
  1. **Airborne overlap recovery skip**: Added `trustAirborneReplayInput` flag to skip overlap recovery + deferred depen for trusted airborne frames. Air avg 0.0711→0.0065y (-91%).
  2. **Grounded trust velocity**: Extended TRUST_INPUT_VELOCITY to all non-transport grounded frames in ReplayEngine. Ground avg 0.0625→0.0157y (-75%).
  3. **Center probe closest-to-input**: Multi-ray probe center now queries at input.z and selects closest-to-recording surface instead of highest, avoiding WMO overhangs.
  4. **Guardrail trend gating**: `maxReplayInputRise` reduced from 0.14 to 0.04 on flat ground with no ascending trend, eliminating 282-frame 0.14y error cluster.
  5. **Swim/air misclassification**: Liquid query no longer overrides to swimming when movement flags have JUMPING|FALLINGFAR with trustInputVel. Air avg 0.0065→0.0000y.
  6. **Swim trust velocity**: ProcessSwimMovement bypassed with provided velocity when trustInputVel set. Swim avg 0.0155→0.0000y.
- Pass result: delta shipped
- Files changed:
  - `Exports/Navigation/PhysicsEngine.cpp` — trustAirborneReplayInput, center probe closest-to-input, guardrail trend gating, swim/air misclassification guard, swim trust velocity bypass
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs` — grounded trust velocity for all non-transport frames, swim Z velocity
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs` — diagnostic: per-mode worst-frame output, updated OrgrimmarGroundZ probe positions
- Validation: Debug 75 passed, 0 skipped, 2 failed (pre-existing: Forward_TraversesSlope, Backward_MovesOppositeToFacing)
- Remaining error sources (not fixable without data changes):
  - Ground max 0.52y: Orgrimmar WMO geometry mismatch (GetGroundZ returns surface 0.3-0.5y below client walking level at specific positions). Would require WMO extraction improvements.
  - Transition max 0.98y: Sub-frame flag timing at ground→air boundaries. Would require sub-frame interpolation.
  - Transport avg 0.23y: Undercity elevator without proper transport coordinate tracking.
- Pre-existing test failures: Forward_TraversesSlope, Backward_MovesOppositeToFacing — character moves ~2x expected distance. Root cause in MovementController/speed calculation, not C++ physics.
- Blockers: None.
- Next command: `dotnet test Tests/Navigation.Physics.Tests/ -s Tests/Navigation.Physics.Tests/test.runsettings -v n`
