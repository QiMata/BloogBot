# BackgroundBotRunner Tasks

## Scope
- Directory: `Services/BackgroundBotRunner`
- Master tracker: `MASTER-SUB-013`
- Project: `BackgroundBotRunner.csproj`
- Focus: BG command execution, movement lifecycle, and parity with FG during corpse/combat/gathering flows.
- Canonical corpse-run flow: `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
- Keep only unresolved work here; move completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md` in the same session.

## Execution Rules
1. Execute task IDs in order unless blocked.
2. Use source-scoped scans for `Services/BackgroundBotRunner` and directly related call sites only.
3. Every validation cycle must compare FG and BG in the same scenario run.
4. Enforce 10-minute max runtime for corpse-run validations with deterministic teardown evidence.
5. Never blanket-kill `dotnet`; cleanup must be repo-scoped and include only owned lingering processes.
6. If two consecutive passes produce no file delta, record blocker + exact next command in `Session Handoff` before switching files.
7. Every pass must write a one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Background service lifecycle currently starts bot runner but has no explicit stop hook in this worker:
  - `_botRunner.Start()` in [BackgroundBotWorker.cs](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/BackgroundBotWorker.cs:77).
  - No `StopAsync` override present in `BackgroundBotWorker.cs`; teardown is currently centered on `ResetAgentFactory()` in [BackgroundBotWorker.cs](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/BackgroundBotWorker.cs:264).
- Worker loop relies on `Task.Delay(100, stoppingToken)` and broad exception handling in [BackgroundBotWorker.cs](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/BackgroundBotWorker.cs:83) and [BackgroundBotWorker.cs](/E:/repos/Westworld of Warcraft/Services/BackgroundBotRunner/BackgroundBotWorker.cs:86).
- Corpse runback task already disables probe heuristics and direct fallback:
  - `enableProbeHeuristics: false`, `enableDynamicProbeSkipping: false`, `strictPathValidation: true` in [RetrieveCorpseTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/RetrieveCorpseTask.cs:24).
  - Waypoint retrieval uses `allowDirectFallback: false` in [RetrieveCorpseTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/RetrieveCorpseTask.cs:374).
- Corpse flow setup uses Orgrimmar teleport in live tests:
  - Step comment and teleport call in [DeathCorpseRunTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs:270) and [DeathCorpseRunTests.cs](/E:/repos/Westworld of Warcraft/Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs:272).
- Current retrieve-corpse internal timeout is `12` minutes in [RetrieveCorpseTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/RetrieveCorpseTask.cs:52), so external test timeout/cleanup policy remains mandatory.
- Action ingestion logs action type only (no explicit correlation ID field) in [BotRunnerService.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/BotRunnerService.cs:210).

## Environment Checklist
- [ ] PathfindingService is reachable and returning valid routes.
- [ ] No repo-owned stale BG process chain (`WoW.exe`, `WoWStateManager`, scoped `dotnet/testhost`) remains before launch.
- [ ] BG and FG test characters are available for the same scenario seed.

## P0 Active Tasks (Ordered)

### BBR-MISS-001 Harden action dispatch ordering and observability
- Problem: BG action processing currently logs action type but lacks deterministic correlation from ingress to completion.
- Target files:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
- Required change:
  - Add a stable action-correlation token through receive, dispatch, completion/failure logs.
  - Ensure queue ordering is observable and deterministic in logs for replay analysis.
  - Add regression assertions (or log-scrape checks) that fail on reordering/duplication.
- Validation command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
- Acceptance criteria:
  - Each dispatched BG action has one correlated start/end outcome and preserves FIFO order in diagnostics.

### BBR-MISS-002 Eliminate stuck-forward zero-displacement loops
- Problem: stalled movement loops can persist after release/runback transitions and waste full test windows.
- Target files:
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
- Required change:
  - Keep no-displacement/stale-forward detection deterministic and tied to runback/follow state.
  - Ensure recovery logic cannot oscillate between stop/restart without route progress.
  - Add a regression case that fails when forward intent persists without measurable displacement.
- Validation command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RetrieveCorpseTaskTests|FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria:
  - Corpse-run/follow scenarios do not exhibit repeated forward-run-in-place loops.

### BBR-MISS-003 Enforce deterministic lifecycle teardown on timeout/failure
- Problem: service lifecycle currently emphasizes connection reset but does not explicitly stop bot runner on host shutdown path in this worker.
- Target files:
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `run-tests.ps1`
- Required change:
  - Add deterministic stop/dispose path for `_botRunner` and owned subscriptions on cancellation/failure.
  - Ensure teardown logs include process name, PID, and stop result for owned processes only.
  - Verify timeout failure path always executes repo-scoped cleanup command.
- Validation command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`
- Acceptance criteria:
  - No repo-owned lingering BG client/state-manager process remains after timeout/failure scenarios.

### BBR-MISS-004 Validate path consumption for corpse runback
- Problem: runback path quality must be proven from service output through waypoint consumption, not inferred from high-level success/failure.
- Target files:
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Services/PathfindingService/**/*`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
- Required change:
  - Keep probe-skip/direct-fallback disabled for corpse runback path driving.
  - Log and assert waypoint progression against returned path corners.
  - Add explicit failure diagnostics for wall-impact/repeated waypoint non-improvement.
- Validation command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria:
  - In Orgrimmar corpse-run scenarios, BG follows valid route segments and reaches reclaim range without repeated wall-impact loops.

### BBR-MISS-005 Add parity regression checks for corpse/combat/gathering cycles
- Problem: parity drift is hard to triage without one-cycle FG vs BG comparison gates and shared diagnostics.
- Target files:
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
  - `Services/WoWStateManager/**/*`
- Required change:
  - Add/extend assertions that compare FG and BG movement cadence, ability usage, and packet behavior for the same seeded scenario.
  - Emit parity diagnostics that include movement deltas, selected spells/actions, and message timing.
  - Require PhysicsEngine calibration pass before parity signoff when movement drift appears.
- Validation command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria:
  - FG/BG parity regressions fail deterministically with actionable movement/spell/packet diagnostics.

## Simple Command Set
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`
4. `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`

## Session Handoff
- Last updated: 2026-02-25
- Last delta: added `MASTER-SUB-013` tracker, source-backed evidence lines, and concrete per-task implementation/validation/acceptance breakdowns.
- Pass result: `delta shipped`
- Next command: `Get-Content -Path 'Services/CppCodeIntelligenceMCP/TASKS.md' -TotalCount 320`
- Loop Break: if no file delta after two passes, record blocker and exact next command, then advance queue pointer in `docs/TASKS.md`.
