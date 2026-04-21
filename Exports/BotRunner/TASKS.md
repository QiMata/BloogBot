# BotRunner Tasks

## Scope
- Project: `Exports/BotRunner`
- Owns task orchestration for corpse-run, combat, gathering, questing, and shared navigation execution loops.
- Master tracker: `docs/TASKS.md`

## Execution Rules
1. Work the highest-signal unchecked task unless a blocker is recorded.
2. Keep live validation bounded and repo-scoped; never blanket-kill `dotnet` or `WoW.exe`.
3. Every navigation delta must land with focused deterministic tests before the next slice.
4. Update this file plus `docs/TASKS.md` in the same session as any shipped BotRunner delta.
5. `Session Handoff` must record `Pass result`, exact validation commands, files changed, and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/BotRunner/BotRunner.csproj` builds in `Release`.
- [x] `Tests/BotRunner.Tests` targeted filters run without restore.
- [x] Repo-scoped cleanup commands are available.

## Active Tasks

### BR-NAV-006 Prove path ownership through combat and movement-controller handoff
Known remaining work in this owner: `0` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [x] Session 188 redirect parity test proved FG/BG matched pause/resume packet timing with `Parity_Durotar_RoadPath_Redirect`. BG `SET_FACING` fix shipped so both clients emit `MSG_MOVE_SET_FACING` on mid-route direction changes.
- [x] Final live proof bundle (session 188): forced-turn Durotar, redirect, combat auto-attack, and corpse-run reclaim all pass on the same DLL baseline.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
### 2026-04-21 (P4.3)
- Pass result: `P4.3 LoadoutTask event-driven step advancement shipped`
- Last delta:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`: `LoadoutStep` gained `AttachExpectedAck`/`DetachExpectedAck` plus the `AckFired`/`MarkAckFired` plumbing. `LearnSpellStep`, `SetSkillStep`, and `AddItemStep` override `OnAttachExpectedAck` to install filtered subscriptions on `IWoWEventHandler.OnLearnedSpell` / `OnSkillUpdated` / `OnItemAddedToBag`. `LoadoutTask.Update` attaches all acks once on first tick, detaches per-step when `IsSatisfied` flips, and detaches everything on terminal (Ready/Failed). `_acksAttached` guards against double-subscribing on re-entry.
  - Polling still runs every tick; the event handle is an optional latency optimization that flips `IsSatisfied` on the very next `Update()` without waiting for the 100ms pacing tick. No SMSG-less command (`.levelup`, `.additemset`, `.use`) changed behavior.
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Files changed:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Commits: `8add32e9 feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Scope note: `P4.4` (correlation ids + `CommandAckEvent`) and `P4.5` (coordinator + test migration) are still open in `docs/TASKS.md` and were intentionally not started.
- Next command: `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`

### 2026-04-21 (P4.1/P4.2)
- Pass result: `P4.1/P4.2 BotRunner plumbing shipped`
- Last delta:
  - `BotRunnerService.Messages` now buffers learned/unlearned spell, skill-update, item-added, error, and system-message events through the shared FG/BG event surface.
  - `SnapshotChangeSignature` no longer counts recent chat/error buffer lengths, and `BotRunnerServiceSnapshotTests.Start_WhenOnlyDiagnosticMessagesChange_KeepsHeartbeatOnlyUntilHeartbeatInterval` pins the no-churn heartbeat behavior.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: 2026-04-20
- Active task: carry the now-green WSG desired-party/live-objective path forward into the next battleground objective slice.
- Last delta:
  - `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` now treats the local player as the fifth member when `PartyAgent.GroupSize == 4` / `GetGroupMembers().Count == 4`, matching the live `SMSG_GROUP_LIST` contract that excludes self. That fixes the Horde 5-player-party ceiling that previously prevented WSG leaders from converting to raid and inviting the last five queue members.
  - `BotRunnerServiceDesiredPartyTests` now pins that exact `PartyAgent` contract and verifies it still drives the current `IObjectManager.ConvertToRaid()` execution path.
  - `BgTestHelper.WaitForBotsAsync(...)` now prints the specific raw snapshot(s) missing from `AllBots` whenever live hydration stalls, which turns the old `19/20` aggregate into actionable account-level diagnostics.
  - The WSG objective scenarios now run as `WsgFlagCaptureObjectiveTests` and `WsgFullGameObjectiveTests` on separate fixture collections, so each destructive live scenario gets a fresh 20-bot roster instead of inheriting the previous match's transfer residue.
- Pass result: `BotRunner desired-party reconciliation is proven by green live WSG objective coverage`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.DesiredParty.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`
- Previous handoff notes:
  - `ActionDispatcher.JoinBattleground` now upserts `BattlegroundQueueTask` instead of unconditionally pushing, so repeated queue dispatch cannot stack duplicate battleground queue tasks on the BotRunner stack.
  - Added deterministic regression coverage in `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs` for both the first queue push and the duplicate-dispatch no-growth case.
  - The same battleground slice also depended on the AB fixture changes that moved the live queue/entry rerun fully onto background runners; the fresh live AB rerun passed after the earlier foreground-transfer crash.
  - `PathfindingClient` exposes a local short-horizon segment simulation hook backed by `NativeLocalPhysics.Step`.
  - `NavigationPath` now rejects service route segments that local physics proves climb onto the wrong route layer and repairs them through nearby same-layer detour candidates.
  - The repair path keeps strict local-physics/support/width checks for the short detour leg and avoids using the noisy downstream lateral-width probe as a veto on the longer ramp stitch-back leg.
  - Long service segments are no longer rejected solely because the short-horizon local simulation reports `hit_wall` when route-layer metrics remain consistent.
  - Corpse-run routes now advance close waypoints without the standard probe-corridor shortcut veto because `NavigationRoutePolicy.CorpseRun` deliberately disables probe heuristics.
  - The live Orgrimmar bank-to-auction-house route now arrives instead of looping back over the corner waypoint.
  - Foreground ghost forward input mitigation remains guarded and unit-covered. The opt-in FG corpse-run rerun now passes and restores strict-alive state.
- Pass result: `JoinBattleground queue-task upsert is pinned and the AB queue-entry rerun is green`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
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
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
