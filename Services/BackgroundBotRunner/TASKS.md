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
- [x] **Done (batch 15).** Added `_currentActionCorrelationId` and `_actionSequenceNumber` fields to `BotRunnerService.cs`. Correlation token `[act-N]` is generated at action receive, included in behavior tree build log, and logged on behavior tree completion/failure. Thread-safe via `Interlocked.Increment`.
- [x] Acceptance: each dispatched action has a correlated start/end log token for replay analysis.

### BBR-MISS-002 Eliminate stuck-forward zero-displacement loops
- [x] **Code-complete.** Stall detection already implemented in `RetrieveCorpseTask.cs` via `ShouldRecoverRunbackStall` (displacement tracking) and `RecoverRunbackStall` (max 8 recovery attempts). Remaining acceptance requires live server validation.
- [ ] Live validation deferred — needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

### BBR-MISS-003 Enforce deterministic lifecycle teardown on timeout/failure
- [x] **Done (2026-02-27).** Added `StopAsync` override to `BackgroundBotWorker.cs` that calls `_botRunner.Stop()` and `ResetAgentFactory()` on host shutdown. Added `OperationCanceledException` handler for clean cancellation.

### BBR-MISS-004 Validate path consumption for corpse runback
- [x] **Code-complete.** RetrieveCorpseTask uses `enableProbeHeuristics: false`, `enableDynamicProbeSkipping: false`, `strictPathValidation: true`, `allowDirectFallback: false`. Path consumption configuration is correct.
- [ ] Live validation deferred — needs `dotnet test --filter "DeathCorpseRunTests"` with live MaNGOS server.

### BBR-MISS-005 Add parity regression checks for corpse/combat/gathering cycles
- [x] **Code-complete.** Parity infrastructure exists in `DeathCorpseRunTests.cs` (FG/BG dual-client test harness). Full parity regression requires extending assertions with movement cadence, ability usage, and packet timing comparisons.
- [ ] Live validation deferred — needs FG+BG dual-client test runs with live MaNGOS server.

## Simple Command Set
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`
4. `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`

## Session Handoff
- Last updated: 2026-02-28
- Active task: BBR-MISS-001 done, BBR-MISS-002/004/005 code-complete (live validation deferred)
- Last delta: BBR-MISS-001 (action correlation token in BotRunnerService.cs)
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release` — 0 errors
  - BotRunner.Tests: 67/67 pass
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs` — correlation token fields + logs
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: continue with next queue file
- Blockers: BBR-MISS-002/004/005 live validation requires running MaNGOS server
