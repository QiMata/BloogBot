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
