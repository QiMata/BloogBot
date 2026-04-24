# BotRunner.Tests Tasks

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: keep BotRunner deterministic tests and live-validation assertions aligned with current FG/BG runtime parity.

## Execution Rules
1. Do not run live validation until the remaining code-only parity work is complete.
2. Prefer compile-only or deterministic test slices when the change only touches live-validation assertions.
3. Keep assertions snapshot-driven; do not reintroduce direct DB validation or FG/BG-specific skip logic for fields that now exist in both models.
4. Use repo-scoped cleanup only; never blanket-kill `dotnet` or `WoW.exe`.
5. Update this file in the same session as any BotRunner test delta.

## Active Priorities
1. Live-validation expectation cleanup
- [x] Remove stale FG coinage stub assumptions from mail/trainer live assertions now that `WoWPlayer.Coinage` is descriptor-backed.
- [x] Sweep remaining live-validation suites for FG/BG divergence assumptions that are no longer true.
- [x] Use dedicated non-overlapping battleground account pools (`AV*`, `WSG*`, `AB*`) and preserve matching existing characters at launch so PvP-rank-bearing battleground characters are reused instead of erased/recreated.
- [x] Keep authoritative staged local-pool activation/visibility attribution explicit on nondeterministic Ratchet reruns. The live harness now carries the staged outcome through to the final assertion path, including the direct child-pool probe fallback case.

2. Alterac Valley live-validation expansion
- [x] Reduce `BackgroundBotRunner` per-instance memory / launch pressure enough for AV to bring all `80` accounts in-world; latest AV run settled to `bg=80,off=0` before objective push.
- [x] Get the AV first-objective live slice green; `AV_FullMatch_EnterPrepQueueMountAndReachObjective` now passes with `HordeObjective near=30` and `AllianceObjective near=40`.

3. Final validation prep
- [x] Ran the final live-validation chunk after the remaining parity implementation work was closed.
- [x] Collected the final core live-validation evidence with the updated FG recorder baseline.

4. Movement/controller parity coverage
Known remaining work in this owner: `0` items.
- [x] Added deterministic coverage for the persistent `BADFACING` retry window that was holding the candidate `3/15` mining route in stationary combat.
- [x] Added targeted BG corpse-run coverage for live waypoint ownership: `DeathCorpseRunTests` now asserts the emitted `navtrace_<account>.json` captured `RetrieveCorpseTask` ownership and a non-null `TraceSnapshot`, with deterministic helper tests covering stable recording-file lookup/cleanup.
- [x] Session 188: `Parity_Durotar_RoadPath_Redirect` proves pause/resume packet ordering. BG `SET_FACING` on mid-route redirects now matches FG. Full live proof bundle green.

## Simple Command Set
1. Build:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`

2. Deterministic snapshot/protobuf slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`

3. Final live-validation chunk after code-only parity closes:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m`

4. Live movement parity bundle on Docker scene data:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=movement_parity_category_latest.trx"`

5. Scene-data service deterministic slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneTileSocketServerTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"`

## Session Handoff
### 2026-04-24 (Tier 1 slice 13 - pending-action readiness gate)
- Pass result: `single-launch Ratchet fishing passes with no AssignedActivity workaround; FG and BG both receive StartFishing and report fishing_loot_success`
- Last delta:
  - Added `ActionForwardingContractTests` coverage that pins pending action behavior across heartbeat/full-snapshot readiness boundaries: ready heartbeats deliver, transition heartbeats defer, and non-actionable full snapshots defer until a later ready snapshot.
  - The live regression is proven fixed by `fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat`: FG receives `StartFishing`, pushes `FishingTask`, reports `update_entered` and `activity_start`, then loots successfully; BG repeats the same flow in the same `Fishing.config.json` launch.
  - The test remains action-driven. `Fishing.config.json` still does not assign `Fishing[Ratchet]` to TESTBOT1/TESTBOT2, and the pathfinding-first cast-position fix remains intact.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> required MaNGOS plus pathfinding/scene-data services were running/healthy.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt"` -> `passed (1/1)` in `4m 48s`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Artifacts:
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt`
  - `D:\World of Warcraft\logs\botrunner_TESTBOT1.diag.log`
  - `Bot/Release/net8.0/logs/botrunner_TESTBOT2.diag.log`
- Files changed:
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
- Next command: `git status --short`

### 2026-04-24 (Tier 1 slice 12 - single-launch Ratchet fishing; BG LOS fix via pathfinding-first cast source)
- Pass result: `build green; deterministic slice green; single-launch Ratchet fishing is green on two consecutive focused reruns after restoring pathfinding-first cast selection for both bots; a third rerun stalled upstream in Shodan pool staging`
- Last delta:
  - `FishingProfessionTests.cs` now runs the Ratchet proof from one shared `Fishing.config.json` roster. FG, BG, and Shodan launch together once; Shodan stages a close pool; the test dispatches `ActionType.StartFishing` to FG with `["Ratchet", 1, 2628]`, waits for `fishing_loot_success`, re-stages, then dispatches the same action to BG.
  - `Fishing.config.json` no longer assigns `Fishing[Ratchet]` to TESTBOT1 or TESTBOT2, so both bots stay idle until the test sends the action. The dedicated `Fishing.ShodanOnly.config.json` roster is gone.
  - `ActionDispatcher.StartFishing` now forwards `location`, `useGmCommands`, and `masterPoolId` into `FishingTask`, and `BotRunnerServiceFishingDispatchTests` cover both the new metadata-aware payload and the legacy waypoint-only payload.
  - `FishingTask.TryResolveCastPosition(...)` is pathfinding-first again. The current user-reported BG LOS screenshot matched the logs exactly: BG was reacquiring `castSource=native` at `distance≈18.2` and casting into the pier. With the pathfinding-first selector restored, both FG and BG reacquire `castSource=pathfinding` and finish from the same ~16y pier-edge standoff.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> verified the required MaNGOS + scene/pathfinding services were `Up`/`healthy` before live validation.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_1.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt"` -> `passed (1/1)` in `20.8074m`; one TESTBOT1 `WoW.exe` launch, one fixture-ready line, one initial custom-settings restart, FG `castSource=pathfinding` -> `fishing_loot_success`, BG `castSource=pathfinding` -> `fishing_loot_success`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_2.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt"` -> `passed (1/1)`; TRX again shows FG/BG `castSource=pathfinding` and both `fishing_loot_success`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_3.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_3.console.txt"` -> `shell timed out after 30m`; the console stopped progressing in `FISHING-WAKE-*` during Shodan pool staging before any fishing action dispatch. Follow-up `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` stopped the lingering repo-scoped processes.
- Artifacts:
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.trx`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.trx`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt`
  - `tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_3.console.txt`
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Services/WoWStateManager/Settings/Configs/Fishing.config.json`
  - `Services/WoWStateManager/Settings/Configs/Fishing.ShodanOnly.config.json` (deleted)
  - `Tests/BotRunner.Tests/BotRunnerServiceFishingDispatchTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/FishingProfessionTests.md`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_4.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_4.console.txt"`

### 2026-04-24 (Tier 1 slice 11 - Shodan staged Ratchet fishing stabilized 4x green)
- Pass result: `build green; deterministic live-fixture slices green; the focused Ratchet fishing slice is now green in four consecutive formal reruns`
- Last delta:
  - `FishingProfessionTests.cs` now isolates the validation phases instead of launching both fishing bots against the same relocated pool. The test stages a close pool with `Fishing.ShodanOnly.config.json`, restarts into a runtime-generated FG-only fishing config, waits for FG `fishing_loot_success`, re-stages with Shodan, then repeats the same flow with a runtime-generated BG-only fishing config.
  - `LiveBotFixture.ServerManagement.cs` now repairs previously relocated Barrens pool children before every staging round. `RestoreBarrensFishingPoolBaselineAsync(...)` queries master pool `2628`, detects split child pairs, and uses `.gobject move` to snap a moved child back onto its sibling anchor so each run starts from a clean pool map.
  - The master-pool site query now anchors on a single child GUID per sub-pool instead of mixing `MIN(x)` and `MIN(y)` from diverged children, which had been fabricating impossible hybrid coordinates after relocation fallback changed the DB.
  - Relocation fallback now prefers pier-reachable pool `2627` instead of `2620`. In live evidence, relocating active child `19480` from sub-pool `2621` onto `2627` consistently produced a selectable close pool and avoided the FG `loot_window_timeout` loop that kept happening when the fallback targeted `2620`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (31/31)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_target2627_probe.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_1.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_2.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_3.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_final_rerun_after_fixture_change.trx"` -> `passed (1/1)` (4th consecutive pass; duration 22m28s; 2 full Shodan -> FG -> Shodan -> BG staging cycles with two `FISHING-BASELINE` repairs and two `FISHING-RELOCATE` moves onto pool 2627)
  - Live evidence markers from all four final runs:
    - `FISHING-BASELINE` repaired moved children `19480`/`19485` before each Shodan stage.
    - `FISHING-RELOCATE` targeted pool `2627` and moved active child `19480` from pool `2621` onto the south-pier site.
    - `Passed BotRunner.Tests.LiveValidation.FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool` appears in `fishing_final_1.console.txt`, `fishing_final_2.console.txt`, `fishing_final_3.console.txt`, and `fishing_final_rerun_after_fixture_change.console.txt`.
- Artifacts:
  - `tmp/test-runtime/results-live/fishing_target2627_probe.trx`
  - `tmp/test-runtime/results-live/fishing_final_1.trx`
  - `tmp/test-runtime/results-live/fishing_final_2.trx`
  - `tmp/test-runtime/results-live/fishing_final_3.trx`
  - `tmp/test-runtime/results-live/fishing_final_rerun_after_fixture_change.trx`
  - `tmp/test-runtime/results-live/fishing_final_1.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_2.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_3.console.txt`
  - `tmp/test-runtime/results-live/fishing_final_rerun_after_fixture_change.console.txt`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: none queued; Ratchet fishing slice is stable at 4 consecutive green reruns and is ready to be archived on the next tracker sweep.

### 2026-04-23 (Tier 1 slice 10 - Shodan idle + equipped admin loadout)
- Pass result: `build green; Shodan no longer self-starts fishing and now equips the full admin mage loadout, but the focused Ratchet slice is still red because the pool verifier cannot see close active children`
- Last delta:
  - Fixed an env-var inheritance bug in `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`. `TESTBOT1` foreground launches left `WWOW_ASSIGNED_ACTIVITY=Fishing[Ratchet]` in process-global state, and the next background bot launch inherited it. `StartBackgroundBotWorker(...)` now removes optional vars when absent and `StartForegroundBotRunner(...)` now clears the same optional globals when null, so `UseGmCommands=true` without `AssignedActivity` leaves Shodan idle instead of auto-running `FishingTask`.
  - Added `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs` and switched `FishingProfessionTests` to call `EnsureShodanAdminLoadoutAsync(...)`. The helper levels Shodan to 60, resets items, learns wand proficiency (`5019`), `.additem`s a validated mage BIS list, then dispatches `ActionType.EquipItem` for each slot and waits until the item leaves the bag snapshot. This proves the gear is equipped instead of merely added.
  - Corrected the live-validated item list after the first run exposed a bad neck-slot ID. Final equipped set: Frostfire armor (`22498/22499/22496/22503/22501/22502/22497/22500`), neck `23058`, cloak `22731`, rings `23062/23031`, trinkets `23046/19379`, main-hand `22589`, ranged `22820`. No fishing pole is part of the setup.
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `0 errors`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_idle_check.trx"` -> `failed (1/1)`; proved Shodan stopped self-starting fishing, but the old loadout list failed on an invalid neck-slot item id.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_loadout_fix.trx"` -> `failed (1/1)`; TRX contains `[SHODAN-LOADOUT] Added and equipped 16 BIS items for 'SHODAN'.` and no Shodan-owned `FishingTask` activity, but `FISHING-ENSURE` still returns `float.MaxValue` for the closest active pool and FG times out waiting for a pier-reachable pool.
  - Artifacts: `tmp/test-runtime/results-live/fishing_shodan_idle_check.trx`, `tmp/test-runtime/results-live/fishing_shodan_loadout_fix.trx`.
- Files changed:
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ShodanLoadout.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Open work for next session:
  - Replace the `.pool spawns 2628`-based verifier in `LiveBotFixture.ServerManagement.cs`. In the current live capture path only the summary line is surfacing, so `GetClosestActivePoolDistanceAsync(...)` never sees child coordinates and always returns `float.MaxValue`.
  - Rework the stage verification around authoritative nearby-object checks, likely `.gobject select` on known master-pool-2628 spawn coordinates after `WaitForTeleportSettledAsync(...)`, then move the staging step ahead of the fishing-bot start so FG/BG do not lock the wrong pool before Shodan rotates the master pool.
- Next command (after the verifier rework): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_pool_verifier_rework.trx" *> "tmp/test-runtime/results-live/fishing_shodan_pool_verifier_rework.console.txt"`

### 2026-04-23 (Tier 1 slice 9 - pathfinding-first cast resolver; both bots cast from pier edge)
- Pass result: `build green; focused live Ratchet slice still red overall (empty loot window), but positioning is perfect for both bots \u2014 user visual confirmation that FG and BG stand on the pier edge with fishing lines cast into the water`
- Last delta:
  - Raised `MaxPoolLockDistance` from `45f` to `80f` (matches `FishingPoolDetectRange`) so pools visible at the teleport landing are locked immediately and the 8-direction radial search walk never runs. That search walk was the root cause of both bots' navigation failure modes (FG climbed town structures east of the dock; BG wandered off the pier into water).
  - Gated `CanDirectSearchWalkFallback` and `CanSearchWaypointStraightProbePath` on `SupportsNativeLocalPhysicsQueries`. FG's `TryHasLineOfSight` always returned `true` with no local physics, which previously let the bot walk any Z-matching short stride off a dock lip. FG now requires a navmesh path for every move.
  - Rewrote the cast-position resolver to be pathfinding-first for both FG and BG. `TryResolveCastViaPathfinding` asks `PathfindingClient.GetPath(player, pool)` for a navmesh route, then scans path segments from the pool end backward, interpolating on the first segment that brackets `IdealCastingDistanceFromPool = 18f` (the bobber landing distance) so the bot ends up exactly where the bobber lands on the pool. Falls back to the in-range node closest to 18y, then the endpoint. The native sphere-sweep finder stays as a secondary source for BG when pathfinding declines.
  - Added `IdealCastingDistanceFromPool` constant (`18f`). Kept `MinCastingDistance`/`MaxCastingDistance` gates so odd pathfinding results can still be discarded.
- Validation/tests run:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `war-scenedata`, `pathfinding-service` all `Up (healthy)`.
  - Build (Release, all five projects) -> `0 errors`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test ... fishing_bobber_landing_distance.trx ...` -> `failed (1/1)`; positioning is clean:
    - BG: `pool_acquired distance=52.4` -> `cast_position_resolved source=pathfinding pos=(-975.0,-3792.8,5.8) edgeDist=18.0 los=True` -> `approaching_pool` with `playerZ` 5.2-6.9 (on dock) -> `cast_position_arrived distance=16.0 edgeDist=18.0 los=True` -> `cast_started attempt=1`. No `fell_off_pier`, no `player_swimming`.
    - FG: same cast position, `playerZ` 5.1-5.6 throughout, `cast_position_arrived distance=15.8 edgeDist=18.0 los=True` -> `cast_started attempt=1`. No search walk, no `fell_off_pier`, no `player_swimming`.
  - Artifacts: `tmp/test-runtime/results-live/fishing_bobber_landing_distance.trx`, `.console.txt`.
  - Visual: user screenshot showing both TESTBOT1 and TESTBOT2 standing on the pier edge with rods extended and bobbers in the water.
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Open work for next session:
  - Loot window opens (`loot_window_open count=1 coins=0 items=[]`) but with no fish. The bobber is landing adjacent to the pool, not on it. Likely fixes: (a) tighten the facing so `atan2(pool - standoff)` is computed against the actual bobber landing rather than pool center, (b) verify the `BobberLandingDistance` constant matches real Vanilla client behavior (the sphere-sweep finder uses `18f`; if the client's actual bobber throw is closer to 20-22y, the standoff should shift), (c) after `cast_position_arrived`, issue a `SetFacing` that includes a small vertical aim component since the pool sits 5y below the player and the bobber trajectory is parabolic.
  - Keep pathfinding-first. Do not revert to the native-first resolver; the native standoff sits on the dock edge where both physics models slide off.
- Next command (focused live re-run after a bobber-aim tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_bobber_aim_into_pool.trx" *> "tmp/test-runtime/results-live/fishing_bobber_aim_into_pool.console.txt"`

### 2026-04-23 (Tier 1 slice 8 - phase-gated fell_off_pier; BG Ratchet fishing green)
- Pass result: `build green; focused live Ratchet slice still red overall but BG now completes end to end (search_walk_found_pool -> cast_position_resolved -> cast_position_arrived -> cast_started -> fishing_loot_success lootItems=[6361]); FG now fails on a separate player_swimming_approach guard, not fell_off_pier`
- Last delta:
  - Phase-gated the `fell_off_pier` guard in `Exports/BotRunner/Tasks/FishingTask.cs`. Added `_reachedApproachLevelForActivePool` plus constants `FellOffPierOnApproachZTolerance` and `FellOffPierZThreshold`. The drop-below-approach check now only fires after we have observed the player within `1.5` yards of the approach Z for the current active pool, so a resolver that picks a dock-top standoff while the bot is still at water level no longer pops the task on the first tick. Latch resets via the existing `ClearCastPositionCache` path (used by both `TrackActivePool` pool-change resets and `RetryFromPool`).
  - No other behavior changes. Local-physics split from slice 7 is untouched, no `PathfindingClient.GetGroundZ` / `IsInLineOfSight` resurrection, no new `Navigation.dll` P/Invokes, no duplicate activity class, no hardcoded Ratchet coordinates.
- Validation/tests run:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, `war-scenedata`, `pathfinding-service` all `Up 2 days (healthy)`.
  - Build (Release, all five projects: `Exports/BotRunner`, `Services/WoWStateManager`, `Services/BackgroundBotRunner`, `Services/ForegroundBotRunner`, `Tests/BotRunner.Tests`) -> `0 errors`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry_after_pier_tweak.trx" *> "tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.console.txt"` -> `failed (1/1)`; artifacts: `tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.trx`, `.console.txt`.
  - Live result: BG is fully green end to end — `search_walk_found_pool guid=0xF11002C1AF004C1E entry=180655 distance=45.0 waypoint=5/8` -> `cast_position_resolved pos=(-968.1,-3783.4,6.6) facing=4.63 edgeDist=22.5 los=True` -> steady `approaching_pool` progression with playerZ climbing 5.0 -> 5.5 (no `fell_off_pier` fires) -> `cast_position_arrived distance=24.6 edgeDist=22.5 los=True` -> `cast_started attempt=1 spell=18248` -> `loot_window_open` -> `loot_bag_delta items=[6361]` -> `fishing_loot_success`. FG still fails, but now on `retry reason=player_swimming_approach` -> `pop reason=player_swimming` because FG's search walk takes it into deeper water at Z≈0 / -1; `IsSwimming` fires before the pier phase gate has anything to evaluate.
- Files changed:
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Open work for next session:
  - FG Ratchet fishing is now the lone blocker for the dual-bot slice. Likely approaches: (a) filter the default radial search waypoints to reject candidates whose support Z is below a reasonable dock floor (so FG's south-west sweep doesn't end up in deep water), (b) let the bot take a few approach ticks while swimming before popping — e.g. re-route to the resolved cast position's X/Y and allow the server-side swim/stand transition to update `IsSwimming`, (c) add a light instrumentation pass that prints FG support-Z of `travelTarget` per waypoint before any behavior change.
  - Keep the current phase-gate as-is. If a future regression brings fell_off_pier back as the dominant failure mode, flipping the latch condition is the right lever, not reverting.
- Next command (focused live re-run after an FG swim-approach tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_fg_swim_recovery.trx" *> "tmp/test-runtime/results-live/fishing_fg_swim_recovery.console.txt"`

### 2026-04-22 (Tier 1 slice 7 - post-wrapper-removal validation)
- Pass result: `build green; deterministic wrapper-removal coverage green; live Ratchet slice restored to the dock-navigation blocker`
- Last delta:
  - Validated `2597067d` instead of treating it as a purely mechanical cleanup. The live slice no longer regresses early after the `PathfindingClient` wrapper removal, and the `LineOfSight` ABI crash from `91cbd44a` stayed fixed: no `AccessViolationException` returned in the new FG/BG proof.
  - Added `BotRunner.Helpers.LocalPhysicsSupport` and threaded `supportsNativeLocalPhysicsQueries` into both `NavigationPathFactory` call sites plus `FishingTask`. BG / scene-data-backed managers still use `WoWSharpClient.Movement.NativeLocalPhysics` directly, but FG managers now decline GroundZ / LOS local-physics queries the same way the deleted wrappers effectively did. This restores the old runtime behavior split without reintroducing `PathfindingClient.GetGroundZ`, `PathfindingClient.IsInLineOfSight`, or duplicate `Navigation.dll` P/Invokes.
  - Fixed the deterministic test fallout from the static `NativeLocalPhysics` override conversion: `DelegatePathfindingClient` now implements `GetPathResult(...)`, `GoToArrivalTests` now installs `NativeLocalPhysics.TestGetGroundZOverride`, and the stall-detection benchmark test was updated to exercise the current `NavigationPath` stalled-near-waypoint recovery path.
- Validation/tests run:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps` -> verified `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were up / healthy.
  - Build: rebuilt `Exports/BotRunner`, `Services/WoWStateManager`, `Services/BackgroundBotRunner`, `Services/ForegroundBotRunner`, and `Tests/BotRunner.Tests` clean (`0 errors`).
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~PathfindingPerformanceTests|FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~BotRunnerServiceTests" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-deterministic" --logger "trx;LogFileName=post_wrapper_removal_unit.trx"` -> `passed (195/195)`; artifact: `tmp/test-runtime/results-deterministic/post_wrapper_removal_unit.trx`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~BotRunnerServiceTests" --logger "console;verbosity=minimal"` -> `passed (194/194)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingPerformanceTests.GetNextWaypoint_LOSStringPull_SkipsIntermediateWaypoints|FullyQualifiedName~PathfindingPerformanceTests.GetNextWaypoint_StallDetection_TriggersRecalculation" --logger "console;verbosity=minimal"` -> `passed (2/2)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -ListRepoScopedProcesses` -> found repo-scoped leftovers from an earlier timed-out deterministic pass (`dotnet.exe` PID `31400`, `testhost.x86.exe` PID `11752`).
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> cleaned only those repo-scoped processes.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_post_wrapper_removal.trx" *> "tmp/test-runtime/results-live/fishing_post_wrapper_removal.console.txt"` -> `failed (1/1)`; artifacts: `tmp/test-runtime/results-live/fishing_post_wrapper_removal.trx`, `tmp/test-runtime/results-live/fishing_post_wrapper_removal.console.utf8.txt`.
  - Live result: FG is back to the pre-wrapper-removal search-walk shape (`probe_rejected` / `path` / `direct` / `navigate` / `arrived` markers) before `search_walk_exhausted`; BG reaches `search_walk_found_pool guid=0xF11002C1AF004C1E entry=180655`, resolves `cast_position_resolved pos=(-970.2,-3785.9,6.6) facing=4.73 edgeDist=25.5 los=False`, then still aborts on `fell_off_pier playerZ=2.8 approachZ=6.6`.
- Files changed:
  - `Exports/BotRunner/Helpers/LocalPhysicsSupport.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/BotRunner/Helpers/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/NavigationPathFactory.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/Movement/PathfindingPerformanceTests.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
- Open work for next session:
  - Treat the remaining blocker as a dock-navigation issue, not a wrapper-removal issue. The most likely productive tweak is still in `FishingTask.MoveToFishingPool`: either relax the `fell_off_pier` guard during the initial climb back onto the dock, or make the search-walk accept only a waypoint whose support Z matches the resolved cast-position Z before the pool is considered acquired.
  - FG still ends in `search_walk_exhausted`, but it is no longer the early wrapper-removal regression. If we need one more instrumentation-only pass before behavior changes, logging FG `mode=` plus support-Z data around the rejected/stalled waypoints should tell us whether the remaining problem is navmesh rejection or bad local-physics support.
- Next command (focused live re-run after one dock-navigation tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry_after_pier_tweak.trx" *> "tmp/test-runtime/results-live/fishing_dock_navigation_retry_after_pier_tweak.console.txt"`

### 2026-04-22 (Tier 1 slice 6 - inline Ratchet activity into FishingTask)
- Pass result: `build green; LineOfSight ABI fix lands; activity refactor lands; live Ratchet still failing on navigation, not on the cast resolver itself`
- Last delta:
  - Diagnosed the prior failure as a critical P/Invoke ABI mismatch in `FishingCastPositionFinder.LineOfSightNative` — the C++ export takes `XYZ` structs by value, the C# declaration was sending seven loose floats, and the resulting stack mismatch raised `System.AccessViolationException` and crashed the StateManager host on the first finder call. Switched the P/Invoke to the same `XYZ`-by-value pattern that `NativePhysicsInterop` and `PathfindingService` already use; the cast-position resolver now returns a real result (BG bot resolved `(-971.9,-3771.7,6.0)` and arrived at it).
  - Refactored away `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs` and the entire `IActivity` interface per "no individual activity files" + ".tele name <name> Ratchet" guidance. `ActivityResolver.Resolve` now returns `IBotTask` directly, and `FishingTask` itself owns the full sequence: GM-command outfit setup (`.additem`/`.learn`/`.setskill`/`.pool update`), then `.tele name <character> <location>` (with a self-form `.tele <location>` fallback when the character name is not yet populated, e.g. BG), then the existing fishing flow. The master-pool-id table (Ratchet -> 2628) lives in the resolver, not in a per-location class.
  - Removed the `zDelta>2` gate so the cast-position finder always tries to resolve a pier-top standoff before falling back to the legacy shoreline path, and added a `cast_position_unresolved` diagnostic for the null case so we can see when the sweep finds no edge.
  - Added a generic 8-direction radial search-walk fallback (`BuildDefaultSearchWaypoints`, ~28y radius) so a `FishingTask` dispatched with no explicit waypoints (the new common case) can still find pools that fall outside the immediate gameobject window from a named landmark like Ratchet town.
  - Updated the live test marker from `[ACTIVITY] FishingAtRatchet start` to `[TASK] FishingTask activity_start`.
- Validation/tests run:
  - Build: rebuilt all five projects (`Exports/BotRunner`, `Services/WoWStateManager`, `Services/BackgroundBotRunner`, `Services/ForegroundBotRunner`, `Tests/BotRunner.Tests`) clean (`0 errors`).
  - Native probes (PowerShell `Add-Type` against `Bot/Release/net8.0/Navigation.dll`): `GetGroundZ` returns dock surface (~5.4-5.6) at `(-958..-965, -3768..-3771)` and water bottom (~-8) just one yard south, so the Ratchet pier is genuinely ~1y wide here; `GetGroundZ(-957.18,-3778.92)` was the closest bay pool spawn at ~24y from the `.tele Ratchet` landing point.
  - Live focused slice (`fishing_search_walk_fallback`): both bots emit `activity_start`, finish outfit, dispatch `.tele Ratchet`, generate the 8-waypoint radial fallback, and BG actually reaches `search_walk_found_pool guid=...180655 distance=44.8` followed by `cast_position_resolved pos=(-968.8,-3783.5,6.6) edgeDist=22.5 los=False`. From there the navigation drops the bot through to terrain Z=2.8 and `fell_off_pier` aborts the task; FG never makes it through the search ring (multiple `search_walk_stalled` events). So the cast-resolver is doing its job — what's left is keeping the bot on the dock during the approach.
- Files changed:
  - `Exports/BotRunner/Activities/ActivityResolver.cs` (rewritten)
  - `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs` (deleted)
  - `Exports/BotRunner/Activities/IActivity.cs` (deleted)
  - `Exports/BotRunner/BotRunnerService.cs` (resolver call site)
  - `Exports/BotRunner/Combat/FishingData.cs` (added `FishingPoleItemId`/`NightcrawlerBaitItemId`)
  - `Exports/BotRunner/Tasks/FishingCastPositionFinder.cs` (P/Invoke ABI fix)
  - `Exports/BotRunner/Tasks/FishingTask.cs` (new outfit/travel phases, default search waypoints, diagnostics)
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs` (activity_start marker)
- Commits pushed: `91cbd44a fix(fishing): inline Ratchet activity into FishingTask and fix LineOfSight ABI`, `884772bd feat(fishing): generic radial search-walk fallback when no waypoints provided`.
- Open work for next session:
  - Keep the bot on top of the pier during the approach to the resolved cast position. The cast resolver picks Z=6.6 (dock surface), but when the search-walk drops the bot at terrain Z=3.7 on the dock-side approach, the navigation routes it through water (`playerZ` drops to 2.8) and the `fell_off_pier` guard pops the task. Likely fixes: (a) require the search-walk to land on a waypoint whose ground Z matches the cast position before declaring the pool acquired, (b) shrink/relax the `fell_off_pier` guard so the bot can recover during a brief Z dip, (c) refine the radial waypoints to prefer dock-surface Z probes over straight-line cardinal points.
  - FG-side `search_walk_stalled` on multiple radial waypoints suggests FG's WoW.exe physics is rejecting the path probes. Worth instrumenting `mode=` on FG separately to confirm whether it's a navmesh issue or a physics drop.
- Next command (focused live re-run after a navigation tweak): `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dock_navigation_retry.trx"`

### 2026-04-22 (Tier 1 slice 5 - Ratchet activity cast-position sweep)
- Pass result: `build green; focused Ratchet fishing live proof failed`
- Last delta:
  - Carried forward the new activity plumbing (`UseGmCommands`, `AssignedActivity`, env-var forwarding, `ActivityResolver`) and kept `Fishing.config.json` assigning `Fishing[Ratchet]` to both TESTBOT1 (FG) and TESTBOT2 (BG).
  - Removed the Ratchet hardcoded waypoint array from `FishingAtRatchetActivity`, added the allowed `.pool update 2628` outfit tick plus `[ACTIVITY] FishingAtRatchet pool_refresh_dispatched master=2628`, and now push `FishingTask` with no geometry baked into the activity.
  - Added `FishingCastPositionFinder` (direct `Navigation.dll` `GetGroundZ` / `LineOfSight` sphere-sweep) and integrated its per-pool cache/facing path into `FishingTask`, including cache clears on retry / pool change / pop.
  - Remaining failure mode: the live run never emitted `[TASK] FishingTask cast_position_resolved`, so both bots kept falling back to the legacy `in_cast_range` shoreline path and repeated `loot_window_timeout`; FG also hit `approach_stalled` and one `fell_off_pier`.
- Validation/tests run:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}" | Select-String -Pattern 'mangos|realm|maria|scene|pathfind' | ForEach-Object { $_.Line }` -> `scene-data-service`, `war-scenedata`, `mangosd`, `realmd`, `pathfinding-service`, `maria-db` all `Up ... (healthy)`.
  - `dotnet build Exports/BotRunner/BotRunner.csproj -c Release -v minimal`; `dotnet build Services/WoWStateManager/WoWStateManager.csproj -c Release -v minimal`; `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj -c Release -v minimal`; `dotnet build Services/ForegroundBotRunner/ForegroundBotRunner.csproj -c Release -v minimal`; `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> all succeeded (`0` errors).
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_sphere_sweep.trx" *> "tmp/test-runtime/results-live/fishing_sphere_sweep.console.txt"; exit $LASTEXITCODE` -> `failed (1 test, 1 failure)` in `3m 52s`; see `tmp/test-runtime/results-live/fishing_sphere_sweep.console.txt`.
  - `PowerShell Add-Type Navigation probe in Bot/Release/net8.0` -> `GetGroundZ(-960, -3770)=5.566`, `GetGroundZ(-955, -3782)=-8.182`, `GetGroundZ(-949.932, -3766.883)=3.703`; native DLL loading is not the blocker.
- Files changed:
  - `Services/WoWStateManager/Settings/CharacterSettings.cs`
  - `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  - `Services/WoWStateManager/StateManagerWorker.cs`
  - `Services/WoWStateManager/Settings/Configs/Fishing.config.json`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Services/ForegroundBotRunner/ForegroundBotWorker.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/Activities/IActivity.cs`
  - `Exports/BotRunner/Activities/ActivityResolver.cs`
  - `Exports/BotRunner/Activities/FishingAtRatchetActivity.cs`
  - `Exports/BotRunner/Tasks/FishingCastPositionFinder.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_sphere_sweep_retry.trx"`

### 2026-04-22 (Tier 1 slice 4 - dual-bot Ratchet staged-pool fishing)
- Pass result: `slice shipped; FG+BG staged-pool fishing live proof green`
- Last delta:
  - Replaced `Fishing_CatchFish_BgAndFg_RatchetPierOpenWaterPath` with `Fishing_CatchFish_BgAndFg_RatchetStagedPool`, which asserts both FG and BG are present + hydrated in `LiveBotFixture.AllBots` pre- and post-prep, stages both bots at Ratchet via `PrepareRatchetFishingStageAsync` (DB spawn query + natural respawn wait + visible-pool confirmation) and dispatches the task-owned `ActionType.StartFishing` path for each bot. `AssertFishingResult` already enforces `pool_acquired`, cast-range arrival, channel/bobber observation, and a newly looted item — shoreline/open-water direct-cast shortcuts are no longer permitted.
  - Deleted the pier open-water direct-cast support: `RunPierOpenWaterFishingWithPacketRecordingAsync`, `RunPierOpenWaterFishingAsync`, `AssertDirectFishingResult`, `AssertDirectFishingPathAndCastAttempt`, `AssertDirectFishingCastPacketsRecorded`, `FormatDirectFishingFailureContext`, `BuildRatchetPierCastCandidates`, `TryDirectFishingCastAsync`, `TryEnsureRatchetPierCastProbeReady`, `EnsureTestNavigationDllResolverRegistered`, `ResolveNavigationDllForTests`, `WaitForPositionSettledAsync`, `MoveToFishingWaypointAsync*`, `WaitForGoToArrivalMessageAsync`, `WaitForFacingSettledAsync`, `WaitForCastReadySnapshotAsync`, `WaitForFishingPoleEquippedAsync`, `CalculateFacingToPoint/Delta`, `NormalizeAngleRadians`, `FacingDeltaRadians`, `GetMainhandGuid`, `MakeSetFacing`, `MakeGoto`, the `DirectFishingRunResult` / `DirectFishingCastCandidate` / `FerryCastTargetSpec` / `DirectFishingCastAttemptResult` / `PositionWaitResult` / `GoToArrivalWaitResult` / `WaypointMoveResult` record types, the pier/ratchet-known-pool constants, the `Navigation` P/Invokes (`SetDataDirectory`/`PreloadMap`/`GetGroundZ`/`LineOfSight`), and the now-unused `System.Reflection` / `System.Runtime.InteropServices` / `BotRunner.Native` usings. `FishingProfessionTests` is now `1832` lines (was `3023`).
  - Updated the file header comment so `Fishing_CatchFish_BgAndFg_RatchetStagedPool` is described as the authoritative dual-bot staged-pool proof and `Fishing_CaptureForegroundPackets_RatchetStagingCast` is explicitly the focused FG packet-trace baseline.
- Validation/tests run:
  - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> `INFO: No tasks are running which match the specified criteria.`
  - `docker ps --format "{{.Names}} {{.Status}}"` -> `mangosd Up 46 hours (healthy)`, `realmd Up 46 hours (healthy)`, `maria-db Up 46 hours (healthy)`, `scene-data-service Up 46 hours (healthy)`, `pathfinding-service Up 46 hours (healthy)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 warnings, 0 errors)` in `26s`
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_dual_bot_ratchet_followup.trx"` -> `passed (1/1)` in `1m 49s` (total run `2m 54s`). Both TESTBOT1 (FG/Gargandurooj) and TESTBOT2 (BG/Thokzugshvrg) staged at the Ratchet packet-capture dock and satisfied the `AssertFishingResult` contract on the real off-shore pool.
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_full_class_after_dual_bot_cleanup.trx"`

### 2026-04-22 (Tier 1 slice 3 - natural fishing pool wait)
- Pass result: `slice shipped; focused staged fishing live proof green`
- Last delta:
  - Replaced the forced fishing-pool refresh path in `FishingProfessionTests` with a natural nearby-pool wait driven by `snapshot.MovementData.NearbyGameObjects`, using a `5` minute budget after clearing nearby respawn timers.
  - Added one alternate named-tele retry path for the staged fishing prep: when Ratchet exhausts the natural wait budget, the test now chooses the best DB-backed coastal fallback from `BootyBay`, `Auberdine`, or `Azshara` and retries exactly once.
  - Added extra nearby-gameobject diagnostics for staged fishing failures and updated the fishing respawn-timer helper comment in `LiveBotFixture.ServerManagement.cs` to match the new no-forced-refresh flow.
- Validation/tests run:
  - `forced fishing refresh grep over FishingProfessionTests + LiveBotFixture.ServerManagement` -> `no matches`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)`
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CaptureForegroundPackets_RatchetStagingCast" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_natural_respawn_slice3.trx"` -> `passed (1/1)` in `1.7826m`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/FishingProfessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.ServerManagement.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_full_class_followup.trx"`

### 2026-04-22 (Tier 1 slice 2 - fresh-account combat arena fixtures)
- Pass result: `slice shipped; CombatBgTests retry green after fresh-BG hydration fix; CombatFgTests green`
- Last delta:
  - Replaced the Tier-1 combat suites with dedicated fresh-account arena rosters/configs (`BGONLY*`, `FGONLY*`) and new `CoordinatorFixtureBase`-backed prep fixtures that stage both bots at the Valley of Trials boar cluster.
  - Rewrote `CombatBgTests` and `CombatFgTests` to share the `CombatLoopTests`-style proximity pattern: find one boar visible to both attackers, dispatch one `StartMeleeAttack` per bot, wait for snapshot-confirmed death, and assert every attacker survives.
  - Deleted the old shared combat helper path plus the legacy BG/FG combat fixture + collection files, and updated `LootCorpseTests` to use the new BG arena fixture so the test project still compiles after that deletion.
  - Hardened `LiveBotFixture.InitializeAsync()` with periodic DB character-name reseeding during the initial in-world wait; the first BG-only live run exposed that fresh headless rosters can reach `InWorld` before `CharacterName` hydrates.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (0 warnings, 0 errors)`
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2.trx"` -> `skipped (1)`; fresh BG-only hydration stalled with blank `CharacterName`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)` after the initial-hydration reseed fix
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_bg_arena_slice2_retry.trx"` -> `passed (1/1)`
  - `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatFgTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=combat_fg_arena_slice2.trx"` -> `passed (1/1)`
  - `legacy Tier-1 combat helper/fixture grep across Tests` -> `no matches`
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/CombatBg.config.json`
  - `Services/WoWStateManager/Settings/Configs/CombatFg.config.json`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatBgTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `WWOW_DATA_DIR='D:/MaNGOS/data' dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_natural_respawn_slice3.trx"`

### 2026-04-22 (Tier 1 slice 1 - no runtime GM toggles)
- Pass result: `slice shipped; build green; focused MageTeleport live proof blocked twice on Horde teleport arrival`
- Last delta:
  - Removed every active live runtime-GM-toggle dispatch/helper in the test suite, including the legacy Tier-1 combat helper/observer path, `IntegrationValidationTests`, `MageTeleportTests`, and the AV mount-prep path.
  - `AlteracValleyFixture.MountRaidForFirstObjectiveAsync()` now applies mount auras through SOAP (`.aura <mountSpellId> <characterName>`) instead of toggling runtime GM mode.
  - `MageTeleport_Horde_OrgrimmarArrival` now uses the real learned `CastSpell` path with teleport runes instead of GM `.cast`, but the Horde live proof still fails independently with `Spell error for 3567`.
  - Updated stale comments/docs/test data so the runtime-GM-toggle grep over `Tests Services Exports` now only hits the allowed rule docs.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1065 warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on.trx"` -> `failed (2 passed, 1 failed, 1 skipped); Horde Orgrimmar arrival did not complete`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (85 warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MageTeleportTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mage_teleport_no_gm_on_retry.trx"` -> `failed again (2 passed, 1 failed, 1 skipped); Horde path logged "Spell error for 3567" and never satisfied the Orgrimmar arrival assertion`
- Files changed:
  - `Services/WoWStateManager/Settings/CharacterSettings.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatArenaFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CombatFgTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FIXTURE_LIFECYCLE.md`
  - `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RagefireChasmTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Scenarios/TestScenario.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Scenarios/TestScenarioRunner.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CombatLoopTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/LootCorpseTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/RecordedTests.PathingTests.Tests/PathingTestDefinitionTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "CombatArenaFixture|CombatLoopTests|MageTeleportTests|AlteracValleyFixture" Tests/BotRunner.Tests/LiveValidation Services/WoWStateManager/Settings/Configs`

### 2026-04-22 (P5.1)
- Pass result: `P5.1 coordinator ACK consumption green (BattlegroundCoordinatorLoadoutTests 11/11, full BattlegroundCoordinator* 22/22)`
- Last delta:
  - `BattlegroundCoordinatorLoadoutTests` now pin P5.1 behavior: ApplyLoadout
    actions carry a coordinator-stamped `bg-coord:loadout:<account>:<guid>`
    correlation id; Success/Failed/TimedOut ACKs resolve accounts without
    requiring `snapshot.LoadoutStatus` to flip; Pending ACKs leave the
    coordinator waiting on the terminal signal.
  - `BattlegroundCoordinator` no longer leaves `LastAckStatus` as a test-only
    helper — `RecordLoadoutProgressFromSnapshots` closes the pre-task-
    rejection gap (`loadout_task_already_active`, `unsupported_action`) and
    the step-TimedOut gap on the active ACK ring.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BattlegroundCoordinator" -v minimal` -> `passed (22/22)`
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorLoadoutTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "AssertCommandSucceeded|AssertTraceCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

### 2026-04-21 (P4.5)
- Pass result: `P4.5 coordinator + test migration to structured ACKs shipped; Phase P4 closed`
- Last delta:
  - `BattlegroundCoordinatorAckTests` pins the new static `BattlegroundCoordinator.LastAckStatus(correlationId, snapshots)` contract: null for missing ids, Pending propagation, terminal-beats-Pending precedence, failed-with-reason, and cross-snapshot correlation scan.
  - `LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync` stamps a `test:<account>:<seq>` correlation id on every tracked dispatch and surfaces the matching `CommandAckEvent` as `GmChatCommandTrace.AckStatus` / `AckFailureReason`.
  - `LiveBotFixture.AssertTraceCommandSucceeded` is the new ACK-first shared assertion; `IntegrationValidationTests` and `TalentAllocationTests` `AssertCommandSucceeded` helpers now delegate to it. The legacy `ContainsCommandRejection` fallback stays for not-yet-migrated call sites.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests|FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~ActionForwardingContractTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorAckTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Assertions.cs`
  - `Tests/BotRunner.Tests/LiveValidation/IntegrationValidationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/TalentAllocationTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Commits:
  - `4c39065c` `feat(coord): P4.5.1 add LastAckStatus helper on BattlegroundCoordinator`
  - `e8306a9f` `test(botrunner): P4.5.2/P4.5.3 expose AckStatus in GmChatCommandTrace`
- Next command: `rg -n "^- \\[ \\]|Active task:" docs/TASKS.md`

### 2026-04-21 (P4.4)
- Pass result: `P4.4 structured ACK coverage is green`
- Last delta:
  - `ActionForwardingContractTests` now pin `ActionMessage.CorrelationId` and `WoWActivitySnapshot.RecentCommandAcks` proto round-trips, plus the `CharacterStateSocketListener` delivery path that stamps a missing correlation id before the bot sees the action.
  - `BotRunnerServiceSnapshotTests` now prove that changing the ACK ring count forces an immediate full snapshot instead of waiting for the heartbeat interval.
  - `BotRunnerServiceLoadoutDispatchTests` now prove a correlated `ApplyLoadout` emits top-level + per-step ACKs on success and that a duplicate correlated `ApplyLoadout` fails the duplicate request without clobbering the original loadout ACK.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~LoadoutSpecConverterTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
- Files changed:
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceLoadoutDispatchTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Next command:
  - `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

### 2026-04-21 (P4.3)
- Pass result: `P4.3 LoadoutTaskExecutorTests event-driven coverage is green`
- Last delta:
  - `LoadoutTaskExecutorTests.Harness` now wires a `Mock<IWoWEventHandler>` into `IBotContext.EventHandler` and exposes a `SuppressFakeServer` flag so individual tests can drive advancement solely through events.
  - Added 10 new unit tests that exercise P4.3 behavior: per-step ack filtering (wrong spell id / skill value below target ignored), ack-driven `IsSatisfied` short-circuit, detach removes subscription, attach is idempotent, null event handler is a safe no-op, task advances on the very next `Update()` without a pacing sleep when a matching event fires, single-step plan completes to `Ready` without pacing, polling fallback still reaches `Ready` when no event fires, terminal-state detach is safe, per-step detach releases the previous step's subscription while leaving the active step subscribed.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Files changed:
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Commits: `8add32e9 feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Next command:
  - `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`
- Previous handoff preserved below.

### 2026-04-21 (P4.1/P4.2)
- Pass result: `P4.1/P4.2 BotRunner snapshot coverage is green`
- Last delta:
  - Added snapshot-buffer assertions for the new `[SKILL]`, `[UI]`, `[ERROR]`, and `[SYSTEM]` message sources in `BotRunnerServiceSnapshotTests`.
  - Added the gated heartbeat regression test that proves diagnostic message churn stays heartbeat-only until the 2-second interval elapses.
  - Confirmed `GetDeltaMessages(...)` remains heartbeat-safe because it diffs by message content against the last full-snapshot baseline instead of assuming per-tick delivery.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Files changed:
  - `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: `2026-04-20`
- Pass result: `WSG desired-party/objective coverage is green deterministically and live`
- Last delta:
  - Fixed the live Horde roster stall by correcting `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` for the `PartyAgent` contract where `GroupSize`/`GetGroupMembers()` report the other four members of a full 5-player party but exclude the local leader. That lets the leader convert to raid before inviting the remaining WSG members.
  - Updated `BotRunnerServiceDesiredPartyTests` to pin that `PartyAgent`-reported full-party case while still asserting the current `IObjectManager.ConvertToRaid()` dispatch path.
  - Extended `BgTestHelper.WaitForBotsAsync(...)` to print the exact raw snapshot(s) missing from `AllBots` whenever live hydration stalls, so future `19/20` failures identify the real account immediately.
  - Split the destructive WSG objective scenarios into separate fixture collections (`WsgFlagCaptureObjectiveTests` / `WsgFullGameObjectiveTests`) so each live objective run starts from a fresh 20-bot WSG fixture instead of inheriting state from the previous full match.
  - `WarsongGulchFixture` now exposes `ResetTrackedBattlegroundStateAsync(...)`, which the objective prep path can use before the live readiness wait.
  - Deterministic slice: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`.
  - Fresh live WSG proofs:
    - `wsg_fullgame_after_group_size_fix_20260421_0210.trx` -> `passed (1/1)`
    - `wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx` -> `passed (1/1)`
    - `wsg_objective_split_fixtures_20260421_0337.trx` -> `passed (2/2)`
- Files changed:
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`
- Previous handoff notes:
  - Added `BotRunnerServiceBattlegroundDispatchTests` to pin the `JoinBattleground` dispatch contract: the first dispatch pushes exactly one `BattlegroundQueueTask`, and a duplicate dispatch leaves the task stack at size `1`.
  - `ArathiBasinFixture` now keeps both leaders on background runners, extends the cold-start enter-world window to `12m/4m`, disables the launch throttle for the 20-bot roster, and uses the ground-level Stormwind AB battlemaster Z instead of the old Champion's Hall upper-floor offset.
  - `ab_queue_entry_alliance_groundlevel_recheck.trx` exposed the remaining harness issue: `ABBOT1` crashed during foreground battleground transfer. `ab_queue_entry_background_only_recheck.trx` then passed after the fixture stayed background-only end-to-end.
  - Validation:
    - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were running for the live reruns.
    - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the deterministic/live reruns.
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
    - `powershell -ExecutionPolicy Bypass -File ./run-tests.ps1 -CleanupRepoScopedOnly` -> repo-scoped cleanup completed before each live run.
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_alliance_groundlevel_recheck.trx"` -> `failed` with `[AB:BG] CRASHED`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - Files changed:
    - `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs`
    - `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`
    - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`
    - `Tests/BotRunner.Tests/TASKS.md`
    - `Exports/BotRunner/ActionDispatcher.cs`
    - `Exports/WoWSharpClient/Networking/ClientComponents/NetworkClientComponentFactory.cs`
    - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
    - `Tests/WoWSharpClient.Tests/Agent/AgentFactoryTests.cs`
  - Next command:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
  - Reused `Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled` unchanged to finish the corpus. The final server debug syntax is `.debug send opcode`, and the payload file is `/home/vmangos/opcode.txt` inside the running `mangosd` container.
  - That source-backed debug path captured representative `CMSG_FORCE_TURN_RATE_CHANGE_ACK` and `CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK` fixtures, while `.targetself` plus `.knockback 5 5` captured `CMSG_MOVE_KNOCK_BACK_ACK`. This closes the last live-capture gap without adding one-off test methods.
  - Validation:
    - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the test compile runs.
    - `docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}"` -> `mangosd`, `realmd`, `maria-db`, `scene-data-service`, and `pathfinding-service` were running.
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_ACK_CORPUS='1'; $env:WWOW_ACK_CORPUS_OUTPUT='E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; $env:WWOW_ACK_CAPTURE_GM_COMMAND='.debug send opcode'; $env:WWOW_ACK_CAPTURE_EXPECTED_OPCODES='CMSG_FORCE_TURN_RATE_CHANGE_ACK'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests.Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_ACK_CORPUS='1'; $env:WWOW_ACK_CORPUS_OUTPUT='E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; $env:WWOW_ACK_CAPTURE_GM_COMMAND='.debug send opcode'; $env:WWOW_ACK_CAPTURE_EXPECTED_OPCODES='CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests.Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_ACK_CORPUS='1'; $env:WWOW_ACK_CORPUS_OUTPUT='E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; $env:WWOW_ACK_CAPTURE_PREP_GM_COMMANDS='.targetself'; $env:WWOW_ACK_CAPTURE_GM_COMMAND='.knockback 5 5'; $env:WWOW_ACK_CAPTURE_EXPECTED_OPCODES='CMSG_MOVE_KNOCK_BACK_ACK'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests.Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (26/26)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/CMSG_FORCE_SWIM_BACK_SPEED_CHANGE_ACK/`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/CMSG_FORCE_TURN_RATE_CHANGE_ACK/`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/CMSG_MOVE_KNOCK_BACK_ACK/`
    - `Tests/BotRunner.Tests/TASKS.md`
  - Next command:
    - `rg -n "Q1|Q2|Q3|Q4|Q5|G1|knockback ACK race|defer" docs/WOW_EXE_PACKET_PARITY_PLAN.md docs/physics Exports/WoWSharpClient Tests/BotRunner.Tests -g '!**/bin/**' -g '!**/obj/**'`
  - Added `LiveValidation/AckCaptureTests.cs` with `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled`. The harness stages the FG bot in Orgrimmar, teleports it across maps to Ironforge, waits for the FG snapshot to settle in-world, and when `WWOW_CAPTURE_ACK_CORPUS=1` asserts that `MSG_MOVE_WORLDPORT_ACK` fixtures appear under the repo corpus directory.
  - Live execution with the ACK-corpus env vars enabled produced two `MSG_MOVE_WORLDPORT_ACK` fixtures (`DC000000`) while the FG client remained stable through both cross-map teleports. This closes the P2.2 worldport-capture blocker without changing the existing FG hook timing.
  - Validation:
    - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the build/run.
    - `docker ps --format "table {{.Names}}\t{{.Status}}"` -> `mangosd`, `realmd`, `scene-data-service`, `war-scenedata`, and `pathfinding-service` were healthy/running.
    - `if (Test-Path 'Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK') { Remove-Item -LiteralPath 'Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK' -Recurse -Force }; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_ACK_CORPUS='1'; $env:WWOW_ACK_CORPUS_OUTPUT='E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests.Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `$env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK/20260417_161214_670_0001.json`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK/20260417_161217_932_0002.json`
    - `Tests/WoWSharpClient.Tests/Parity/AckBinaryParityTests.cs`
    - `Tests/BotRunner.Tests/TASKS.md`
  - Next command:
    - `rg -n "CMSG_FORCE_.*ACK|MSG_MOVE_SET_RAW_POSITION_ACK|CMSG_MOVE_FLIGHT_ACK" Exports/WoWSharpClient Tests Services -g '!**/bin/**' -g '!**/obj/**'`
  - Added `NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour`.
  - Added `NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy` to pin the valid-ramp case that the lateral-width probe can falsely reject.
  - Added long-horizon local-physics `hit_wall` coverage so long service segments are not rejected when route-layer metrics remain consistent, while short blocked legs still reject.
  - Added probe-disabled close-waypoint advancement coverage for corpse-run routes.
  - Re-ran the full deterministic `NavigationPathTests` surface after the local-physics repair.
  - Revalidated the live Orgrimmar bank-to-auction-house corner route and captured a passing TRX.
  - Re-ran the opt-in foreground corpse-run test. It did not crash WoW.exe and now restores strict-alive state.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RejectsShortLocalPhysicsHitWall|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_after_corpse_probe_policy.trx"` -> `passed (1/1)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`
- Files changed:
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/docs/CRASH_INVESTIGATION.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/DeathCorpseRunTests.md`
  - `Tests/BotRunner.Tests/LiveValidation/docs/TEST_EXECUTION_MODES.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`
- Next command:
  - `rg -n "^- \[ \]" --glob TASKS.md`
- Highest-priority unresolved issue in this owner:
  - None.
