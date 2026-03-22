<<<<<<< HEAD
﻿# BackgroundBotRunner Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
Headless runner integration and behavior alignment with shared BotRunner tasks.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep command/action handling deterministic and observable.

## Active Priorities
1. Runner action execution parity
- [ ] Ensure forwarded actions are executed in order and reflected in snapshots quickly.
- [ ] Keep long-running actions from being interrupted by unrelated movement/coordinator behavior.
- [ ] Investigate follow-loop `Goto` behavior where BG can remain `MOVEFLAG_FORWARD` (`flags=0x1`) with zero displacement after teleport/release transitions.

2. Command-response observability
- [ ] Keep logs sufficient to map dispatched commands to server responses during tests.

## Session Handoff
- Last parity issue closed:
  - None in `Services/BackgroundBotRunner` code this session; parity evidence gathered through live `DeathCorpseRunTests` logs.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"`
  - Result set this session: one skipped rerun, one pass (~2m10s), one fail (FG corpse-run intermittent stall), then one pass (~2m10s).
  - BG evidence now includes explicit `Goto` no-route warnings (`[GOTO] No route ...`) after pathfinding-driven `Goto` rollout; this reduces hidden stuck-forward loops but needs retry/log tuning.
- Files changed:
  - none in `Services/BackgroundBotRunner/*` (work landed in `Exports/WoWSharpClient/*` and shared BotRunner flow).
- Next task:
  - Add targeted BG follow-loop diagnostics linking dispatched `Goto` actions to path query results and movement-controller displacement (`step > 0`) expectations.

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
Move completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md`.


=======
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

### BBR-PAR-001 Improved: World object visibility — gathering node detection timing (linked: BRT-PAR-002)
- [x] **Diagnostics shipped (2026-02-28).** Research confirmed the 40y snapshot filter (BotRunnerService.Snapshot.cs:233-234) is NOT the root cause — player teleports to spawn location, so distance is ~0y. Real issue is server-side visibility timing after `.respawn` command.
- Fix: Increased respawn delay (1500→3000ms), detection loop timeout (10→15s), added first-scan diagnostic dump (NearbyObjects count + first 10 GOs with entry/guid/displayId/position).
- [ ] Live validation needed: run `dotnet test --filter "GatheringProfessionTests"` with live MaNGOS to confirm improved timing resolves herb/mining node detection.
- Files: `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`

### BBR-PAR-002 Research: NPC interaction timing — FlightMaster discovery (linked: BRT-PAR-002)
- [ ] **Research.** `FlightMaster_DiscoverNodes` failed in parity loop — NPC interaction timing issue where the BG client does not detect or interact with the FlightMaster NPC reliably.
- Next step: trace `SMSG_UPDATE_OBJECT` NPC creation packets in FG vs BG to compare NPC visibility timing and interaction readiness.
- Owner: `Services/BackgroundBotRunner` (BG NPC interaction)

## Simple Command Set
1. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathingAndOverlapTests|FullyQualifiedName~Orgrimmar" --logger "console;verbosity=minimal"`
4. `Get-CimInstance Win32_Process | Where-Object { ($_.Name -in @('WoW.exe','WoWStateManager.exe','dotnet.exe','testhost.exe','testhost.net9.0.exe')) -and $_.CommandLine -like '*Westworld of Warcraft*' } | Select-Object Name,ProcessId,CommandLine`

## Session Handoff
- Last updated: 2026-02-28
- Active task: BBR-PAR-001 diagnostics shipped; BBR-PAR-002 still open.
- Last delta: BBR-PAR-001 timing/diagnostics improvements to GatheringProfessionTests (respawn delay 1500→3000ms, detection loop 10→15s, first-scan diagnostic dump). Root cause confirmed as server tick timing, not 40y filter.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release` — 0 errors (63 warnings)
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs` — timing + diagnostic improvements
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests" --blame-hang --blame-hang-timeout 10m` (live validation)
- Blockers: BBR-MISS-002/004/005 live validation requires running MaNGOS server
>>>>>>> cpp_physics_system
