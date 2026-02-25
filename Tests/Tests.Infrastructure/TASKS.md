# Tests.Infrastructure Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- This file owns fixture and process-lifecycle safeguards used by live tests.
- Current priority is preventing lingering clients/managers during long-running scenarios.

## Scope
Shared test fixture and client infrastructure for live/integration orchestration.

## Active Priorities
1. Hard teardown on abnormal exits
- [ ] Ensure timeout/failure/cancel paths always stop repo-scoped lingering `WoWStateManager`, test-launched `WoW`, `dotnet`, and `testhost*`.
- [ ] Keep deterministic teardown order (`WoWStateManager` -> child clients -> repo-scoped `dotnet/testhost*`).
- [ ] Emit teardown summary with process name, PID, and stop result.

2. Fixture determinism
- [ ] Keep setup snapshot-driven with minimal command count.
- [ ] Ensure cleanup executes even when a test aborts mid-run.

3. Corpse-run support
- [ ] Support 10-minute execution windows for `DeathCorpseRunTests`.
- [ ] Preserve cleanup guarantees even when the full timeout window is consumed.

4. Diagnostic control
- [ ] Add optional visible console/window mode for local debugging.
- [ ] Keep default headless behavior for CI and unattended runs.

## Canonical Verification Commands
1. Focused corpse-run:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Repo-scoped process audit:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses`

3. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last infra fix:
- Validation/tests run:
- Files changed:
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
Move completed items to `Tests/Tests.Infrastructure/TASKS_ARCHIVE.md`.

## Behavior Cards
1. TestsInfrastructureTeardownGuardParity
- [ ] Behavior: test infrastructure guarantees timeout/cancel/failure teardown stops only repo-scoped processes and managers.
- [ ] FG Baseline: FG teardown path reliably disposes launched processes and reports PID outcomes without collateral kills.
- [ ] BG Target: BG teardown path applies identical repo-scoped shutdown rules and emits equivalent PID evidence.
- [ ] Implementation Targets: `Tests/Tests.Infrastructure/**/*.cs`, `run-tests.ps1`, `Tests/BotRunner.Tests/**/*.cs`.
- [ ] Simple Command: `dotnet test Tests/Tests.Infrastructure/Tests.Infrastructure.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- [ ] Acceptance: infrastructure tests prove deterministic cleanup and explicitly reject blanket `dotnet` termination behavior.
- [ ] If Fails: add `Research:InfraTeardownScopeGap::<scenario>` and `Implement:InfraTeardownScopeFix::<scenario>` tasks with PID evidence.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
