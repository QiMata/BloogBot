# Navigation.Physics.Tests Tasks

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
