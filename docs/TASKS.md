# Master Tasks - Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/TASKS_ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** - `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Clear repo-scoped WoW/test processes before building.** DLL injection locks output files.
8. **Previous phases archived** - see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-07)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1441 | 0 | 1 | -4 removed SetSceneSliceMode tests |
| Navigation.Physics.Tests | 678 | 2 | 1 | -1 removed SceneSliceModeTests, 2 pre-existing elevator |
| BotRunner.Tests (unit) | 308 | 0 | 2 | Confirmed |

---

## P1 - Alterac Valley 40v40 Integration (TOP PRIORITY)

### Context
Single AV test: `AV_FullMatch_EnterPrepQueueMountAndReachObjective` (80 bots, 40v40).
Fixture: `AlteracValleyFixture` / `AlteracValleyCollection`. Dedicated AV accounts (`AVBOT1-40`, `AVBOTA1-40`), all BG bots.
Honor rank 15 set in DB for all 80 AV accounts. mangosd config updated: Alterac.MinPlayersInQueue=1, InitMaxPlayers=40.

### Completed
- [x] P1.1 **Level bug** - `.levelup` computes delta from current level
- [x] P1.2 **Anticheat rejection** - AV prep skips raid formation; bots queue individually
- [x] P1.3 **Single test** - consolidated 7 AV tests into one full-pipeline test
- [x] P1.4 **Coordinator flow** - confirmed working: WaitingForBots -> QueueForBattleground
- [x] P1.5 **PvP rank** - honor_highest_rank=15 set in DB for all 80 characters
- [x] P1.7 **PvP gear equip** - Changed to fire-and-forget (equip was blocking 18s+ per bot). Removed invalid `.modify honor rank` command (doesn't exist in VMaNGOS)
- [x] P1.8 **Alliance teleport fall** - FIXED (Z+3 removed for indoor Stormwind)
- [x] P1.9 **BG queue pop** - BG coordinator transitions through all states. 73-74/80 bots enter AV map 30. VMaNGOS AV config fixed: Alterac.MinPlayersInQueue=1, InitMaxPlayers=40, min_players_per_team=1 in DB
- [x] P1.10 **Enter world tolerance** - MinimumBotCount override accepts 78/80 for FG stragglers. All >= checks fixed
- [x] P1.11 **Coordinator timeout** - 90s timeout for WaitingForBots so pipeline proceeds with >=75% staged
- [x] P1.12 **High Warlord / Grand Marshal** - Leaders have HW Battle Axe (18831) / GM Claymore (18876) + Warlord/FM armor sets. DB rank 15. All bots now BG (headless) to avoid FG crashes.
- [x] P1.6-resolved **FG bots removed** - All AV bots BG. FG crash/CharacterSelect issues no longer block the pipeline.
- [x] P1.mount **Mount via .cast GM command** - UseItem and CastSpell actions failed for GM-added items. `.gm on` + `.targetself` + `.cast 23509/23510` works. 68/80 bots mount successfully.
- [x] P1.16 **Goto action persistence** - Repeated `Goto` dispatches now upsert a single persistent `GoToTask` (push/retarget/duplicate-skip) instead of stacking fresh tasks each poll cycle. Deterministic coverage: `BotRunnerServiceGoToDispatchTests` (4/4).
- [x] P1.15 **Scene tiles for ALL maps** - Generated 695 scene tiles across 34 maps (was 142/5 maps). Includes Emerald Dream (169, 256 tiles). Docker scene-data-service redeployed with full coverage. Fixed brute-force tile discovery offset bug (36->44 bytes).
- [x] P1.13 **Equip items systemic failure** - by-id equip/use fallback now probes backpack + equipped bag slots (`0..15`, `1..4 x 0..19`). Full AV integration pass completed with no `[LOADOUT-WARN]` output in run artifact.
- [x] P1.14 **8 straggler bots** - coordinator restage + settle window closed the queue-entry gap; latest AV pass reached `BG-SETTLE bestOnBg=80` and `bg=80,off=0` before objective push.

### Open
- None.

---

## R1-R10 - Archived (see docs/ARCHIVE.md)

---

## Deferred Issues

| # | Issue | Details |
|---|-------|---------|
| D1 | **Alliance faction bots** | StateManager doesn't launch AVBOTA* accounts - needs settings support |
| D2 | **BG queue pop** | AB/AV queued but never popped - server-side BG matching config |
| D3 | **WSG transfer stalls** | 8/20 bots didn't complete map transfer |
| D4 | **Elevator physics** | 2 pre-existing Navigation.Physics.Tests failures |
| D5 | **OrgBankToAH navigation** | CornerNavigationTests timeout - pathfinding stall in tight Org geometry |

---

## Canonical Commands

```bash
# Repo-scoped process inspection/cleanup only
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly

# Keep dotnet cache/temp + test artifacts on repo drive
$env:DOTNET_CLI_HOME='E:\repos\Westworld of Warcraft\tmp\dotnethome'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:WWOW_REPO_ROOT='E:\repos\Westworld of Warcraft'
$env:WWOW_TEST_RUNTIME_ROOT='E:\repos\Westworld of Warcraft\tmp\test-runtime'
$env:VSTEST_RESULTS_DIRECTORY='E:\repos\Westworld of Warcraft\tmp\test-runtime\results'
$env:TEMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'
$env:TMP='E:\repos\Westworld of Warcraft\tmp\test-runtime\temp'

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# Docker rebuild + deploy
docker compose -f docker-compose.vmangos-linux.yml build scene-data-service
docker compose -f docker-compose.vmangos-linux.yml up -d scene-data-service
```

---

## Session Handoff (2026-04-09)

- Completed:
  - Closed `P1.13` with live AV validation after inventory fallback hardening; run artifact contains no `[LOADOUT-WARN]` output.
  - Closed `P1.14` via coordinator restage + settle-window flow; run artifact reached `BG-SETTLE bg=80,off=0`.
  - Archived resolved `P1.13`, `P1.14`, and stale-open `P1.15` from the Open list.
- Validation:
  - `rg -n "BG-SETTLE|AV:Mount|AV:HordeObjective|AV:AllianceObjective|offBgAtSuccess" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> shows `bestOnBg=80`, `bg=80,off=0`, `mounted=77/70`, `HordeObjective near=30`, `AllianceObjective near=40`.
  - `rg -n "\[LOADOUT-WARN\]" tmp/test-runtime/results-live/av_iteration_20260409_objective_tolerance60.trx` -> no matches.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 1 -SkipBuild -TestTimeoutMinutes 2` -> failed at pre-existing `Navigation.Physics.Tests.DllAvailabilityTests.NavigationDll_ShouldLoadAndInitializePhysics`.
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BgTestHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorStrictCountTests.cs`
  - `run-tests.ps1`
  - `Tests/Tests.Infrastructure/TestRuntimePaths.cs`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_iteration_rerun.trx"`

## Session Handoff (2026-04-09 - Dedicated Battleground Pools)

- Completed:
  - Switched battleground fixtures to dedicated non-overlapping account pools for AV/WSG/AB. AV horde leader is now `AVBOT1` and AB horde leader is now `ABBOT1` (no shared `TESTBOT1` across battlegrounds).
  - Added battleground launch-prep reuse policy: preserve existing characters when any account character matches configured race/class/gender, preventing unnecessary erase/recreate cycles and helping retain PvP ranks.
  - Added deterministic guardrails that assert battleground account pools remain disjoint and the preserve-existing-character policy stays enabled for battleground fixtures.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CoordinatorFixtureBaseTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results` -> `passed (31/31)`.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBaseTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.WSG_QueueAndEnterBattleground|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live`

## Session Handoff (2026-04-09 - FG Lua Error Capture for AV Leader Stability)

- Completed:
  - Added state-safe foreground Lua error capture (`WWOW_LUA_ERROR_BUFFER`) and removed the old post-world-entry `seterrorhandler(function() end)` suppression path.
  - Wired deterministic Lua-error draining into FG realm wizard and character-create flows so each critical Lua call/query logs contextual errors (`realmwizard.*`, `charselect.create.*`).
  - Added targeted unit coverage for the new diagnostics helper and capture-callback wiring.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FgCharacterSelectScreenTests|FullyQualifiedName~FgRealmSelectScreenTests|FullyQualifiedName~LuaErrorDiagnosticsTests" --logger "console;verbosity=minimal"` -> `passed (14/14)`.
- Files changed:
  - `Services/ForegroundBotRunner/Diagnostics/LuaErrorDiagnostics.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs`
  - `Services/ForegroundBotRunner/Frames/FgCharacterSelectScreen.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
  - `Tests/ForegroundBotRunner.Tests/FgRealmSelectScreenTests.cs`
  - `Tests/ForegroundBotRunner.Tests/FgCharacterSelectScreenTests.cs`
  - `Tests/ForegroundBotRunner.Tests/LuaErrorDiagnosticsTests.cs`
  - `docs/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_fg_lua_capture_rerun.trx"`

## Session Handoff (2026-04-09 - FG New Account Realm Wizard Stabilization)

- Completed:
  - Removed realm-wizard action fallback sweeps (`_G`/global frame iteration) and kept automation state-based with explicit named controls (`English` -> `Suggest Realm` -> `Okay/Accept`).
  - Kept deterministic handoff detection from realm wizard to empty character select via glue/login state (`charselect`) instead of Lua fallback sweeps.
  - Revalidated FG first-login world entry with dedicated new-account/new-character live runs.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FgRealmSelectScreenTests|FullyQualifiedName~FgCharacterSelectScreenTests|FullyQualifiedName~ForegroundBotWorkerWorldEntryCinematicTests|FullyQualifiedName~LuaErrorDiagnosticsTests" --logger "console;verbosity=minimal"` -> `passed (21/21)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.ForegroundNewAccountFlowTests.NewAccount_NewCharacter_EntersWorld" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=fg_new_account_flow_no_sweep.trx"` -> `passed (1/1)`.
  - Stability reruns:
    - `...LogFileName=fg_new_account_flow_latest.trx` -> in-world after `129.8s`.
    - `...LogFileName=fg_new_account_flow_rerun1.trx` -> in-world after `122.5s`.
    - `...LogFileName=fg_new_account_flow_rerun2.trx` -> in-world after `121.7s`.
    - `...LogFileName=fg_new_account_flow_no_sweep.trx` -> in-world after `116.9s`.
  - Artifact paths were pinned to repo-local temp/runtime dirs (`tmp/dotnethome`, `tmp/test-runtime`) for this validation pass.
- Files changed:
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs`
  - `Tests/ForegroundBotRunner.Tests/FgRealmSelectScreenTests.cs`
  - `docs/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/ForegroundBotRunner/TASKS_ARCHIVE.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_fg_post_realm_stabilization.trx"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-001/002 Closure)

- Completed:
  - Closed `BR-NAV-001` conservative overlay filter in BotRunner (`PathfindingOverlayBuilder`) and archived the completed item from active BotRunner tasks.
  - Closed `BR-NAV-002` by threading movement capabilities and route-policy settings through a shared `NavigationPathFactory` (`Standard`/`CorpseRun`) across BotRunner call sites, then archived the completed item from active BotRunner tasks.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (8/8)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (66/66)`.
- Files changed:
  - `Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs`
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/Movement/TargetPositioningService.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingOverlayBuilderTests.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathFactoryTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_TraceRecordsStallDrivenReplanReason|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_TraceRecordsMovementStuckRecoveryReplanReason" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-003 Closure)

- Completed:
  - Closed `BR-NAV-003` with explicit dynamic-blocker evidence driven replanning (`dynamic_blocker_observed`) so blocked segments trigger planned forced recalculation before long stall loops.
  - Archived completed `BR-NAV-003` out of the active BotRunner task list (`Exports/BotRunner/TASKS.md` -> `Exports/BotRunner/TASKS_ARCHIVE.md`).
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (73/73)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathAffordance|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-004 Slice 1)

- Completed:
  - Closed `BR-NAV-004` first slice by teaching `NavigationPath` (movement/path consumer) to reject unsupported cliff-heavy routes and prefer cheaper supported alternates when available.
  - Archived the completed first slice in `Exports/BotRunner/TASKS_ARCHIVE.md`; active BotRunner work now continues with the second `BR-NAV-004` slice (surfacing affordances to higher-level task logic).
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (61/61)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (74/74)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathAffordance|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"`

## Session Handoff (2026-04-09 - BR-NAV-005 Universal Stuck Ownership)

- Completed:
  - Enforced ownership rule: removed task-level unstuck/recovery behavior so stuck detection/recovery ownership stays in movement-layer `IObjectManager` implementations.
  - `GatheringRouteTask` no longer uses candidate/node stuck-recovery budgets from `MovementStuckRecoveryGeneration`.
  - `FishingTask` search-walk no longer consumes `MovementStuckRecoveryGeneration` to skip probe legs.
  - `RetrieveCorpseTask` no longer executes task-owned stall recovery maneuvers (turn/jump/strafe pulses); it now remains path/no-path timeout driven.
  - `NavigationPathFactory` no longer implicitly binds `MovementStuckRecoveryGeneration`; BotRunner callers now construct navigation paths without passing stuck-generation providers.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_universal_stuck_ownership.trx"` -> `failed (1/1)`; runtime still loops on `candidate_timeout` with repeated `STUCK-L2` signals.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/TargetPositioningService.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `rg -n "candidate_timeout|STUCK-L2|MoveToward preserving airborne steering only" "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\mining_bg_gather_route_post_universal_stuck_ownership.trx"`

## Session Handoff (2026-04-09 - BotRunner BR-NAV-004 Slice 2)

- Completed:
  - Closed `BR-NAV-004` second slice by surfacing explicit route-affordance decisions from `NavigationPath` to higher-level task diagnostics.
  - Added `NavigationRouteDecision` to `NavigationTraceSnapshot` so each route plan records support status, max affordance, estimated cost, alternate-route evaluation/selection, and endpoint-retarget outcome.
  - `GoToTask` now emits plan-scoped route summaries (`[GOTO_ROUTE]`) to diagnostics/Serilog, and `RetrieveCorpseTask` summary formatting now includes the surfaced route decision.
  - Archived completed `BR-NAV-004` second slice in `Exports/BotRunner/TASKS_ARCHIVE.md`; `Exports/BotRunner/TASKS.md` now moves on to `BR-NAV-005`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests|FullyQualifiedName~RetrieveCorpseTaskTests.FormatNavigationTraceSummary_IncludesKeyFieldsAndTruncatesPathsAndSamples" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (69/69)`.
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/GoToTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

## Session Handoff (2026-04-09 - BR-NAV-005 Movement/Route Ownership Alignment)

- Completed:
  - Enforced parity ownership split: `MovementController` no longer performs stuck-time waypoint reselection/escape routing; it now signals stuck recovery and leaves route ownership to BotRunner.
  - Removed BotRunner path-execution handoff into `MovementController`:
    - `BotTask.TryNavigateToward(...)` now issues only `MoveToward(waypoint)`.
    - `FishingTask.TryFollowSearchWaypointPath(...)` now issues only `MoveToward(nextWaypoint)`.
  - Updated deterministic tests to match the ownership model.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` -> `passed (64/64)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (65/65)`.
  - Live mining reruns (all failed):  
    - `...LogFileName=mining_bg_gather_route_post_local_delta_cap.trx`
    - `...LogFileName=mining_bg_gather_route_post_mc_route_ownership_shift.trx`
    - `...LogFileName=mining_bg_gather_route_post_botrunner_route_ownership.trx`
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `rg -n "Stuck recovery promoted active waypoint|STUCK-L3|candidate_timeout" E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\mining_bg_gather_route_post_botrunner_route_ownership.trx`

## Session Handoff (2026-04-09 - MovementController Single-Target Parity Contract)

- Completed:
  - Enforced parity ownership boundary: `MovementController` now holds only a single steering target and does not execute waypoint/corridor policy.
  - `SetPath(...)` is now a legacy compatibility shim that stores path head only; `SetTargetWaypoint(...)` stores one target; stale-forward `L2` escalation no longer mutates waypoint selection.
  - Updated deterministic `MovementControllerTests` to reflect callback-only route ownership.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests"` -> `passed (159/159)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed (72/72)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=mining_bg_gather_route_post_single_target_mc.trx"`

## Session Handoff (2026-04-09 - Remove Legacy Route APIs from MovementController Boundary)

- Completed:
  - Finalized the ownership boundary from the shared contract side: removed `IObjectManager.SetNavigationPath(...)` and removed `WoWSharpObjectManager` forwarding to `MovementController.SetPath(...)`.
  - Removed `MovementController.SetPath(...)`; controller now accepts only `SetTargetWaypoint(...)` as a single steering hint while BotRunner remains the route/corridor owner.
  - Updated deterministic tests in WoWSharpClient, Navigation.Physics, and BotRunner to the single-target contract.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~MovementControllerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (164/164)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (73/73)`.
- Files changed:
  - `Exports/GameData.Core/Interfaces/IObjectManager.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerIntegrationTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs`
  - `Tests/Navigation.Physics.Tests/MovementControllerIpcParityTests.cs`
  - `Tests/BotRunner.Tests/Combat/AtomicBotTaskTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_setnavigationpath_removal.trx"`

## Session Handoff (2026-04-09 - MovementController Parity-Only Stuck Signaling)

- Completed:
  - Enforced the strict parity contract: `MovementController` no longer mutates movement state or steering target during stale-forward recovery.
  - Removed in-controller forced strafe/forced-recovery mutation; stale-forward now emits caller-facing stuck escalation signals only.
  - Updated deterministic stale-forward tests to assert callback-only behavior and unchanged movement/waypoint state.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerIntegrationTests|FullyQualifiedName~ObjectManagerWorldSessionTests" --logger "console;verbosity=minimal"` -> `passed (158/158)`.
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (74/74)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_mc_parity_only_stuck_signals.trx"`
