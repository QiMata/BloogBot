# Tests Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- This file coordinates all test-suite task files and enforces master ordering.
- Current priority is corpse-run stability, then combat/gathering parity loops.

## Coordination Rules
1. Keep run commands simple and one-line.
2. Never blanket-kill `dotnet`; only stop repo-scoped `dotnet` and `testhost*` processes.
3. Every timeout path must emit teardown evidence.
4. Archive completed items to `Tests/TASKS_ARCHIVE.md` in the same session.

## Current P0: Corpse-Run
1. Setup and flow
- [ ] Re-run BG and FG corpse-run validation and capture fresh evidence.

2. Runtime and cleanup
- [ ] Keep test timeout policy at 10 minutes for corpse-run validation.
- [ ] Enforce teardown guard for timeout/failure/cancel.
- [ ] Confirm no lingering repo-scoped clients/managers remain after run completion.

3. Assertions
- [ ] Verify BG and FG both satisfy `alive -> dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive`.
- [ ] Verify retrieve/resurrect happens only after reclaim delay reaches zero.

## Current P1: Combat and Gathering Iteration
1. Combat parity
- [ ] Run FG/BG `CombatLoopTests` in same cycle and compare movement/spell/packet behavior.
- [ ] File mismatch findings as paired `research + implementation` tasks in owning project task files.

2. Gathering/mining parity
- [ ] Run FG/BG `GatheringProfessionTests` (and mining-focused coverage) in same cycle.
- [ ] Compare route, approach vector, gather interaction timing, and recovery from interruptions.
- [ ] File mismatch findings as paired `research + implementation` tasks.

3. Physics gate
- [ ] Run physics calibration checks whenever FG/BG movement parity drifts.
- [ ] Track resulting fixes in `Tests/Navigation.Physics.Tests/TASKS.md` and `Exports/Navigation/TASKS.md`.

## Subproject Task Files
- `Tests/BotRunner.Tests/TASKS.md`
- `Tests/Tests.Infrastructure/TASKS.md`
- `Tests/WoWSharpClient.Tests/TASKS.md`
- `Tests/PathfindingService.Tests/TASKS.md`
- `Tests/Navigation.Physics.Tests/TASKS.md`
- `Tests/PromptHandlingService.Tests/TASKS.md`
- `Tests/WowSharpClient.NetworkTests/TASKS.md`
- `Tests/RecordedTests.Shared.Tests/TASKS.md`
- `Tests/RecordedTests.PathingTests.Tests/TASKS.md`
- `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md`
- `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md`
- `Tests/WWoW.Tests.Infrastructure/TASKS.md`
- `Tests/WoWSimulation/TASKS.md`

## Canonical Commands
1. Corpse-run focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Combat focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

3. Gathering focus:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

4. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Handoff Fields
- Last test class audited:
- Command evidence log:
- Next class/task:

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match command line/process tree).
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
Move completed items to `Tests/TASKS_ARCHIVE.md`.

## Behavior Cards
1. CrossSuiteParityGateExecution
- [ ] Behavior: test orchestration runs corpse, combat, and gathering parity suites under shared timeout and teardown guardrails.
- [ ] FG Baseline: FG side of each suite finishes with player-like completion signals and deterministic evidence capture.
- [ ] BG Target: BG side of each suite mirrors FG outcomes and records movement/spell/packet parity evidence in the same cycle.
- [ ] Implementation Targets: `Tests/**/*.cs`, `run-tests.ps1`, `docs/BEHAVIOR_MATRIX.md`.
- [ ] Simple Command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: all selected suites run in one cycle with deterministic teardown evidence and clearly triaged FG/BG parity deltas.
- [ ] If Fails: add `Research:CrossSuiteParityGap::<suite>` and `Implement:CrossSuiteParityFix::<suite>` tasks mapped to owning project TASKS files.

## Continuation Instructions
1. Start with the highest-priority unchecked item in this file.
2. Execute one simple validation command for the selected behavior.
3. Log evidence and repo-scoped teardown results in Session Handoff.
4. Move completed items to the local TASKS_ARCHIVE.md in the same session.
5. Update docs/BEHAVIOR_MATRIX.md status for this behavior before handing off.
