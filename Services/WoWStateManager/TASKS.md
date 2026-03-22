<<<<<<< HEAD
﻿# WoWStateManager Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
State orchestration, snapshot serving, and action forwarding between tests and bots.

## Rules
- Execute directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Every forwarding/snapshot mismatch must be tracked here until closed.

## Active Priorities
1. Action forwarding reliability
- [ ] Verify no dropped/misordered actions during high-frequency test setup.
- [ ] Keep per-account FIFO delivery observable (queue depth, dequeue trace, dispatch correlation).
- [x] Ensure coordinator suppression windows never hide test-forwarded actions. Evidence: `INJECTING PENDING ACTION ... (coordinator suppressed 300s)` present for ReleaseCorpse/Goto/RetrieveCorpse in `tmp/deathcorpse_run_current.log`.

2. Snapshot correctness and diagnostics
- [ ] Keep snapshot query logs concise but sufficient for parity debugging.
- [ ] Ensure recent chat/error buffers are consistent and useful for command-response tracing.

3. Lifecycle and cleanup behavior
- [ ] Ensure startup clean-state behavior is deterministic across stale party/death states.
- [ ] Keep process lifecycle handling robust during repeated LiveValidation runs.

## Session Handoff
- Last bug/task closed: none (action forwarding visible, but corpse runback still stalls downstream in movement/task layers).
- Test evidence:
  - `tmp/deathcorpse_run_current.log` (2026-02-23): action forwarding observed for `ReleaseCorpse`, `Goto`, `RetrieveCorpse` with 300s suppression.
  - `tmp/combatloop_current.log` (2026-02-23): focused combat test pass baseline.
- Files changed: `Services/WoWStateManager/TASKS.md`
- Next task: keep forwarding diagnostics concise while validating no queue starvation during repeated corpse seed/runback retries.

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
Move completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md`.


=======
# WoWStateManager Tasks

Master tracker: `MASTER-SUB-020`

## Scope
- Directory: `Services/WoWStateManager`
- Project: `WoWStateManager.csproj`
- Focus: deterministic lifecycle/teardown, pathfinding readiness gating, and action/snapshot forwarding correctness for corpse-run and parity tests.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Execute tasks in order unless blocked by a recorded dependency.
2. Do not launch bot workers when pathfinding readiness is unknown/failed; use explicit not-ready semantics instead of silent fallback.
3. Never blanket-kill `dotnet`; cleanup must be PID-scoped to managed/test-launched processes only.
4. Every lifecycle change must include a simple validation command and teardown evidence in `Session Handoff`.
5. Archive completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md` in the same session.
6. Keep each pass scoped to `Services/WoWStateManager` plus direct references under `Tests/BotRunner.Tests` and `Tests/Tests.Infrastructure`.
7. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Startup still permits degraded pathfinding mode:
  - `Program.cs:247` and `Program.cs:311` log: `Proceeding without pathfinding. Navigation will fall back to direct movement.`
  - `StateManagerWorker.cs:474` logs: `PathfindingService is not ready - proceeding anyway but navigation may fail`.
- Duplicate bootstrap helpers remain in `Program.cs` with no runtime caller:
  - `EnsurePathfindingServiceIsAvailable` (`Program.cs:337`)
  - `LaunchPathfindingServiceExecutable` (`Program.cs:354`)
  - `WaitForPathfindingServiceToStart` (`Program.cs:401`)
- Stop path remains non-deterministic in single-account teardown:
  - `StopManagedService` uses fire-and-forget stop: `_ = System.Threading.Tasks.Task.Run(async () => await Service.StopAsync(...))` (`StateManagerWorker.cs:1259` region).
  - `StopAllManagedServices` already shows awaited ordering (`StateManagerWorker.cs:1276` onward), so teardown behavior is inconsistent.
- Pending action queue has no cap/expiry policy:
  - `_pendingActions` is a per-account `ConcurrentQueue<ActionMessage>` dictionary (`CharacterStateSocketListener.cs:25`).
  - enqueue/dequeue flow exists (`CharacterStateSocketListener.cs:150`, `:160`) but no bounded depth/TTL semantics.
- State-forwarding regression coverage is missing in scoped test projects:
  - `rg -n "EnqueueAction|HandleActionForward|CharacterStateSocketListener|StateManagerWorker"` returns no hits in `Tests/BotRunner.Tests` and `Tests/Tests.Infrastructure`.
  - harness exists at `Tests/Tests.Infrastructure/StateManagerTestClient.cs:68` (`ForwardActionAsync`) and `:73` (`ActionForwardRequest`), but contract assertions are not wired.
- Build baseline:
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore` -> success (`0 Error(s)`, `0 Warning(s)`).
  - environment note: `dumpbin` still missing on PATH in vcpkg app-local script stage output.

## P0 Active Tasks (Ordered)
1. [x] `WSM-MISS-001` Remove startup continuation when pathfinding service is unavailable.
- **Done (prior session).** PathfindingService readiness gate — fail-fast on unavailability.

2. [x] `WSM-MISS-002` Consolidate to one canonical pathfinding bootstrap flow.
- **Done (2026-02-27).** Dead helpers removed (~95 LOC).

3. [x] `WSM-MISS-003` Make `StopManagedService` teardown deterministic and awaited.
- **Done (prior session).** `StopManagedService` → `StopManagedServiceAsync` with awaited stop + timeout.

4. [x] `WSM-MISS-004` Add per-account action queue cap and stale-action expiry.
- **Done (2026-02-27).** Added `TimestampedAction` wrapper, `MaxPendingActionsPerAccount = 50` depth cap (drops oldest on overflow), `PendingActionTtl = 5 min` stale-action expiry (drops expired actions during dequeue). All drops are explicitly logged.

5. [x] `WSM-MISS-005` Add regression tests for action-forwarding contract.
- **Done (batch 14).** Added `ActionForwardingContractTests.cs` with 24 tests:
  - Proto round-trip: ActionForwardRequest preserves account/action/parameters/order; empty account name; StateChangeResponse result.
  - Dead/ghost detection: IsDeadOrGhostState via reflection — health=0, ghost flag, standState=dead, dead text in errors, null player, aggregated reasons.
  - EnqueueAction: drops SendChat when dead, accepts non-chat when dead, accepts SendChat when alive.
  - ActionType coverage: 6 action types round-trip through protobuf.
- Validation: 24/24 pass (`dotnet test --filter ActionForwardingContractTests|ForegroundObjectRegressionTests`).
- [x] Acceptance: regressions fail deterministically on dead/ghost filtering and proto contract drift.

### WSM-PAR-001 Research: Quest snapshot sync lag (linked: BRT-PAR-002)
- [ ] **Research.** Parity loop (BRT-PAR-001, 2026-02-28) found `Quest_AddCompleteAndRemove_AreReflectedInSnapshots` failing because quest state changes are not reflected in the snapshot in time for the assertion.
- Symptom: quest is added/completed via GM commands, but the snapshot snapshot does not reflect the change within the test's polling window.
- Next step: investigate snapshot pipeline latency for quest fields — determine if the issue is a polling interval, a missing `SMSG_QUEST_*` handler, or a delayed snapshot push from the BG client.
- Owner: `Services/WoWStateManager` (snapshot forwarding) + `Exports/WoWSharpClient` (quest packet handlers)
- Calibration link: N/A (not a physics/navigation issue)

## Simple Command Set
1. Build service: `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore`
2. Corpse-run scenario: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. State-forwarding slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StateManager|FullyQualifiedName~ActionForward|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-28
- Active task: all WoWStateManager tasks complete (WSM-MISS-001..005)
- Last delta: WSM-MISS-005 (24 action-forwarding contract tests in ActionForwardingContractTests.cs)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests -c Debug --filter ActionForwardingContractTests` — 24/24 pass
- Files changed:
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs` — new (24 tests)
  - `Services/WoWStateManager/TASKS.md`
- Next command: continue with next queue file
- Blockers: none
>>>>>>> cpp_physics_system
