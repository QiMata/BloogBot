# Navigation Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Local scope: native movement and physics behavior parity.
- Current priority: remove frame-by-frame interpolation drift impacting corpse/combat/gathering parity.

## Purpose
Track native physics/pathfinding tasks for WoW 1.12.1 behavior parity.

## Initial Research Snapshot (2026-02-24)
1. Corpse-run evidence shows BG no-displacement loops while movement intent is present.
2. Interpolation drift can appear as repeated micro-steps or plateaus around turn/slope transitions.
3. Managed dispatch cadence in `Exports/WoWSharpClient/Movement/MovementController.cs` must be tuned with native integration timing.

## Active Priorities
1. PhysicsEngine frame-by-frame interpolation
- [ ] Refine interpolation in `Exports/Navigation/PhysicsEngine.cpp` to reduce jitter and zero-delta plateaus.
- [ ] Validate frame integration across slope changes, near-obstacle turns, and stop-start transitions.
- [ ] Add instrumentation for pre/post integration position and velocity deltas.

2. MovementController coordination
- [ ] Align `MovementController.cs` frame dispatch cadence to native interpolation expectations.
- [ ] Ensure command dispatch timing does not create false stall detection in runback paths.
- [ ] Validate packet/movement cadence parity against FG traces.

3. Parity scenario support
- [ ] Re-run corpse-run with Orgrimmar setup and verify no teleport-like movement shortcuts.
- [ ] Support combat/gathering parity investigations with native movement diagnostics.
- [ ] Add paired `research + implementation` tasks for every newly found movement divergence.

## Canonical Commands
1. Native physics calibration:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

2. Corpse-run live validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last physics/parity run:
- Native files changed:
- Validation command used:
- Next task:

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

## Archive
Move completed items to `Exports/Navigation/TASKS_ARCHIVE.md`.
