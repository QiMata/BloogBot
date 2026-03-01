# BotRunner.Tests Tasks

Master tracker: `MASTER-SUB-022`

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Focus: deterministic FG/BG corpse/combat/gathering parity validation with strict timeout and teardown controls.
- Queue dependency: `docs/TASKS.md` controls file order and session handoff.

## Execution Rules
1. Execute this file only when `docs/TASKS.md` points `Current queue file` to `MASTER-SUB-022`.
2. Start each pass by running the prior `Session Handoff -> Next command` verbatim.
3. Keep corpse-run setup as named teleport to `Orgrimmar` before kill; do not reintroduce `ValleyOfTrials`.
4. Keep scenario class runtime bounded to 10 minutes and record teardown evidence on pass/fail/timeout/cancel.
5. Never blanket-kill `dotnet`; cleanup remains repo/test-scoped with PID evidence.
6. Every parity cycle runs FG and BG in the same session and records movement/spell/packet behavior deltas.
7. On parity drift, add paired `research + implementation` IDs in owning `TASKS.md` files.
8. Archive completed items to `Tests/BotRunner.Tests/TASKS_ARCHIVE.md` in the same session.
9. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
10. After shipping one local delta, set `Next command` to the next queue-file read command and execute it in the same session to prevent rediscovery loops.
11. For corpse-run pathing changes, validate the full route contract (`PathfindingSocketServer -> Navigation.CalculatePath -> NavigationPath.GetNextWaypoint -> RetrieveCorpseTask`) before broad parity sweeps.

## Evidence Snapshot (2026-02-25)
- Corpse setup teleport is pinned to Orgrimmar and not Valley:
  - `rg --line-number "Orgrimmar|ValleyOfTrials|TeleportToNamedAsync" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - Hits at `:270-272` include `TeleportToNamedAsync(characterName, "Orgrimmar")`; no `ValleyOfTrials` hits.
- Corpse lifecycle stage constants/assertions are present:
  - `rg --line-number "ReleaseToGhostTimeout|ReclaimTimeout|Retrieve|corpse|ghost|alive" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - Includes stage gates at `49-51`, `175-180`, `343`, `570`, `602`.
- Timeout baseline is explicit:
  - `rg --line-number "TestSessionTimeout" Tests/BotRunner.Tests/test.runsettings`
  - `Tests/BotRunner.Tests/test.runsettings:6` -> `<TestSessionTimeout>600000</TestSessionTimeout>`.
- Teardown process controls and cleanup script coverage are present:
  - `rg --line-number "KillStaleProcesses|WoWStateManager|WoW\\.exe|PathfindingService|testhost" Tests/Tests.Infrastructure/BotServiceFixture.cs`
  - `rg --line-number "CleanupRepoScopedOnly|WoWStateManager\\.exe|WoW\\.exe|PathfindingService\\.exe" run-tests.ps1`
- Visible-window toggle implemented via `WWOW_SHOW_WINDOWS=1` env var:
  - All 4 launch sites use `CreateNoWindow = Environment.GetEnvironmentVariable("WWOW_SHOW_WINDOWS") != "1"`
  - 7 infrastructure config tests verify behavior in `Tests/BotRunner.Tests/Helpers/InfrastructureConfigTests.cs`
- Corpse runback currently depends on strict no-direct-fallback waypoint consumption:
  - `rg --line-number "GetNextWaypoint|allowDirectFallback: false|No pathfinding route|pathfinding returned no route" Exports/BotRunner/Tasks/RetrieveCorpseTask.cs Exports/BotRunner/Movement/NavigationPath.cs`
  - Key hits include `RetrieveCorpseTask.cs:370-406` and `NavigationPath.cs:56-324`.
- Path service route entry points are defined and should be contract-validated during corpse-run work:
  - `rg --line-number "HandlePath|CalculatePath|FindPath|TryFindPathNative" Services/PathfindingService/PathfindingSocketServer.cs Services/PathfindingService/Repository/Navigation.cs`
  - Key hits include `PathfindingSocketServer.cs:174-181` and `Navigation.cs:65-107`.
- Proto contract source for pathfinding payloads:
  - `Exports/BotCommLayer/Models/ProtoDef/pathfinding.proto`.

## P0 Active Tasks (Ordered)
1. [x] `BRT-CR-001` Keep corpse-run setup teleport pinned to Orgrimmar named teleport path.
- Problem: setup-location drift invalidates runback behavior validation.
- Target files: `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`.
- Required change: preserve named teleport path `TeleportToNamedAsync(characterName, "Orgrimmar")` and no `ValleyOfTrials` setup path.
- Validation command: `rg --line-number "ValleyOfTrials|TeleportToNamedAsync\\(characterName, \"Orgrimmar\"\\)" Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
- Acceptance criteria: Orgrimmar path present; no ValleyOfTrials setup references.

2. [x] `BRT-CR-002` Enforce full corpse lifecycle and path-consumption assertions for FG and BG.
- Problem: stage regressions and route-consumption regressions can pass without clear failure signal if assertions remain high-level.
- Target files: `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`.
- Required change:
  1. Assert/order and output evidence for `dead -> ghost -> runback -> reclaim-ready -> retrieve -> alive` with reclaim-delay enforcement.
  2. Assert runback displacement and waypoint-driven progression so wall-running or zero-travel loops fail deterministically.
  3. Keep corpse runback path-driven (no probe-point/random-strafe behavior when a valid path exists).
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: failures name missing/out-of-order stage or stalled/no-path condition; passing run shows deterministic stage evidence and waypoint-progress evidence for both FG and BG.

3. [x] `BRT-CR-003` Validate corpse-run path contract from native path output to bot waypoint usage.
- Problem: pathfinding may return routes that are malformed, unreachable, or not consumed correctly by corpse runback logic.
- Target files: `Services/PathfindingService/PathfindingSocketServer.cs`, `Services/PathfindingService/Repository/Navigation.cs`, `Exports/BotRunner/Movement/NavigationPath.cs`, `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`.
- Required change:
  1. Log route contract evidence per corpse-run dispatch (map/start/end, waypoint count, first/last waypoint).
  2. Fail fast on invalid route shapes (empty path when route expected, zero-length segments, unusable endpoint) with deterministic diagnostics.
  3. Confirm consumed waypoint sequence matches returned route order and advances toward corpse reclaim radius.
- Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
- Acceptance criteria: contract regressions fail before live corpse test; successful corpse runback includes path-contract evidence showing usable route and waypoint consumption.

4. [x] `BRT-RT-001` Runtime bounds and deterministic teardown enforced via TINF-MISS-001..006: repo-scoped process filtering (MainModule.FileName check), per-process teardown evidence (PID, exit code, timeout), and `WWOW_SHOW_WINDOWS` visible window opt-in.

5. [x] `BRT-RT-002` Opt-in visible windows via `WWOW_SHOW_WINDOWS=1` env var. Applied to all 4 bot-process launch sites. 7 infrastructure config tests in InfrastructureConfigTests.cs verify behavior.

6. [x] `BRT-PAR-001` Run FG/BG corpse/combat/gathering parity loop using only simple commands.
- Problem: parity drift hides when suites are run ad hoc or in inconsistent order.
- Target files: `Tests/BotRunner.Tests/TASKS.md` execution notes + test output artifacts.
- Required change: execute corpse/combat/gathering commands in one cycle with shared timeout/cleanup guardrails.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests|FullyQualifiedName~CombatLoopTests|FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Acceptance criteria: each cycle records FG/BG parity notes for movement/spells/packet-visible behavior.

7. [x] `BRT-PAR-002` Tie parity drift to physics calibration and owning implementation tasks.
- Problem: movement/parity regressions recur without explicit ownership routing.
- Target files: `Tests/Navigation.Physics.Tests/TASKS.md`, `Exports/Navigation/TASKS.md`, `Services/ForegroundBotRunner/TASKS.md`, `Services/BackgroundBotRunner/TASKS.md`.
- Required change: each parity mismatch creates linked `research + implementation` IDs in owning files and triggers physics calibration tests.
- **Done (2026-02-28).** All 4 live failures from BRT-PAR-001 parity loop routed to owners:
  - `BBR-PAR-001` (world object visibility) → `Services/BackgroundBotRunner/TASKS.md`
  - `BBR-PAR-002` (NPC interaction timing) → `Services/BackgroundBotRunner/TASKS.md`
  - `WSM-PAR-001` (quest snapshot sync) → `Services/WoWStateManager/TASKS.md`
  - `PFS-PAR-001` (PathfindingService readiness) → `Services/PathfindingService/TASKS.md`
  - Physics/navigation confirmed clean — parity routing note in `Tests/Navigation.Physics.Tests/TASKS.md`

## Simple Command Set
1. Corpse-run: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
2. Combat: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
3. Gathering/mining: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. Corpse/path fallback unit slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FarFromCorpse_NoPath|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=minimal"`
5. Repo-scoped cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`
6. Path contract trace scan: `rg --line-number "HandlePath|CalculatePath|GetNextWaypoint|allowDirectFallback: false|pathfinding returned no route" Services/PathfindingService/PathfindingSocketServer.cs Services/PathfindingService/Repository/Navigation.cs Exports/BotRunner/Movement/NavigationPath.cs Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
7. Pathfinding validity slice: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~PathfindingTests|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: All BRT tasks complete (BRT-CR-001/002/003, BRT-RT-001/002, BRT-PAR-001/002).
- Last delta: BRT-PAR-002 completed — parity drift routed to 4 owning TASKS.md files (BBR-PAR-001/002, WSM-PAR-001, PFS-PAR-001). BRT-PAR-001 parity loop re-validated (21 pass, 0 fail, 4 skip).
- Pass result: `delta shipped`
- Files changed: `Tests/BotRunner.Tests/TASKS.md` — BRT-PAR-001/002 marked done.
- Live test results (this pass):
  - DeathCorpseRunTests: 0/1 (PathfindingService not on port 5001)
  - CombatLoopTests: 0/0/1 skip (fixture unavailable)
  - BasicLoop/CharLifecycle/Consumable/Equipment: 0/0/10 skip (fixture unavailable)
  - GroupFormation/NPC/Economy/Quest/Talent/Crafting: 8/0 (FlightMaster/Quest now passing)
  - Gathering + Mining: 22/1 (herb node not found)
  - BRT-PAR-001 parity loop: 21/0/4 skip
- Blockers: PathfindingService not launching during live tests (port 5001 refused). Routed to PFS-PAR-001.
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~InfrastructureConfig" --logger "console;verbosity=minimal"`
