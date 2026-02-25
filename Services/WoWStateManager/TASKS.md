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
1. [ ] `WSM-MISS-001` Remove startup continuation when pathfinding service is unavailable.
- Problem: startup currently proceeds with direct-movement fallback after pathfinding timeout/process-exit conditions.
- Target files: `Services/WoWStateManager/Program.cs`, `Services/WoWStateManager/StateManagerWorker.cs`.
- Required change: fail-fast or explicit not-ready mode that blocks bot launch until pathfinding is healthy.
- Validation command: `rg -n "Proceeding without pathfinding|proceeding anyway but navigation may fail" Services/WoWStateManager/Program.cs Services/WoWStateManager/StateManagerWorker.cs`
- Acceptance criteria: startup path cannot transition to active bot workers when pathfinding readiness fails.

2. [ ] `WSM-MISS-002` Consolidate to one canonical pathfinding bootstrap flow.
- Problem: `EnsurePathfindingServiceIsAvailable`, `LaunchPathfindingServiceExecutable`, and `WaitForPathfindingServiceToStart` are defined but not used by runtime startup.
- Target files: `Services/WoWStateManager/Program.cs`.
- Required change: either wire one canonical bootstrap/readiness flow or remove dead helper paths with explicit ownership semantics.
- Validation command: `rg -n "EnsurePathfindingServiceIsAvailable\\(|LaunchPathfindingServiceExecutable\\(|WaitForPathfindingServiceToStart\\(" Services/WoWStateManager/Program.cs`
- Acceptance criteria: only one startup/bootstrap pathway remains and is directly used by `Main`.

3. [ ] `WSM-MISS-003` Make `StopManagedService` teardown deterministic and awaited.
- Problem: fire-and-forget `Service.StopAsync` can leave lingering workers/processes during test timeout paths.
- Target files: `Services/WoWStateManager/StateManagerWorker.cs`.
- Required change: enforce bounded awaited order (`cancel -> StopAsync -> kill process -> await monitor task`) matching `StopAllManagedServices` semantics.
- Validation command: `rg -n "Task\\.Run\\(async \\(\\) => await Service\\.StopAsync|StopManagedService\\(|StopAllManagedServices\\(" Services/WoWStateManager/StateManagerWorker.cs`
- Acceptance criteria: no fire-and-forget stop call remains in managed-service shutdown logic.

4. [ ] `WSM-MISS-004` Add per-account action queue cap and stale-action expiry.
- Problem: `_pendingActions` can grow unbounded and replay stale commands long after relevance.
- Target files: `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`.
- Required change: introduce bounded depth + TTL/expiry policy with explicit logging on drop/expiry.
- Validation command: `rg -n "_pendingActions|ConcurrentQueue<ActionMessage>|EnqueueAction|pending=" Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
- Acceptance criteria: queue policy is deterministic and observable in logs/tests when cap/expiry rules trigger.

5. [ ] `WSM-MISS-005` Add regression tests for action-forwarding contract.
- Problem: no direct tests gate FIFO, dead/ghost suppression, or response semantics around action forward/query.
- Target files: `Tests/BotRunner.Tests`, `Tests/Tests.Infrastructure`.
- Required change: add contract tests using `StateManagerTestClient` for FIFO order, dead/ghost filtering, and snapshot query success/failure behavior.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StateManager|FullyQualifiedName~ActionForward" --logger "console;verbosity=minimal"`
- Acceptance criteria: regressions fail deterministically on action reorder/drop and invalid dead/ghost forwarding behavior.

## Simple Command Set
1. Build service: `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore`
2. Corpse-run scenario: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. State-forwarding slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StateManager|FullyQualifiedName~ActionForward|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
4. Repo cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: converted to execution-card format with refreshed startup/teardown evidence, queue-policy gaps, and deterministic validation commands.
- Next task: `WSM-MISS-001`
- Next command: `Get-Content -Path 'Tests/TASKS.md' -TotalCount 320`
- Blockers: none (live corpse-run execution intentionally deferred for this documentation-only queue pass).
