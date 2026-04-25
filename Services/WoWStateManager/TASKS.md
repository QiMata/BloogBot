# WoWStateManager Tasks

## Scope
- Directory: `Services/WoWStateManager`
- Project: `WoWStateManager.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: lifecycle orchestration, snapshot/action forwarding, docker-aware service bootstrap, and spawned bot-worker parity.

## Execution Rules
1. Keep changes scoped to `Services/WoWStateManager` plus direct consumers/tests.
2. Never blanket-kill `dotnet` or `WoW.exe`; use repo-scoped cleanup or explicit PIDs only.
3. Every lifecycle/bootstrap change must include a concrete validation command in `Session Handoff`.
4. Archive completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md` in the same session when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
Known remaining work in this owner: `0` items.

## Session Handoff
### 2026-04-24 (Shodan equipment/wand config slice)
- Last updated: 2026-04-24
- Active task: none - this slice only updated live-validation config rosters for the Shodan migration.
- Last delta:
  - `Equipment.config.json` now uses dedicated equipment action accounts: `EQUIPFG1` Foreground Orc Warrior and `EQUIPBG1` Background Orc Warrior, with SHODAN as Background Gnome Mage director.
  - Added `Wand.config.json` for wand-specific action ownership: `TRMAF5` Foreground Troll Mage and `TRMAB5` Background Troll Mage receive wand actions, while SHODAN remains director-only.
  - The new fixture guard `AssertConfiguredCharactersMatchAsync(...)` reads the selected config and verifies the live DB account/character class, race, and gender before the test dispatches BotRunner actions.
- Pass result: `Equipment/Wand shared-launch configs validated by passing live slice (2/2)`
- Validation/tests run:
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> confirmed MaNGOS and split services were already running.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.` before and after live runs.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~WandAttackTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=equipment_wand_action_plan_fresh8.trx" *> "tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt"` -> `passed (2/2)`.
- Files changed:
  - `Services/WoWStateManager/Settings/Configs/Equipment.config.json`
  - `Services/WoWStateManager/Settings/Configs/Wand.config.json`
  - `Services/WoWStateManager/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

### 2026-04-24 (Pending action heartbeat readiness)
- Last updated: 2026-04-24
- Active task: none - the simplified Ratchet `StartFishing` regression is fixed and covered.
- Last delta:
  - `CharacterStateSocketListener` now treats queued external/test actions as one-shot work that must only be drained when the bot is currently actionable: `ScreenState=InWorld`, `ConnectionState=BotInWorld`, `IsObjectManagerValid=true`, and `IsMapTransition=false`.
  - Heartbeat requests with readiness fields use the heartbeat's current readiness instead of the aliased cached full snapshot. Older heartbeats without readiness fields still fall back to the cached response snapshot.
  - If a pending action is present but the bot is not actionable, the action remains queued and the listener logs a debug deferral instead of returning an action that BotRunner can drop during a transition-skip loop.
- Pass result: `FG and BG StartFishing delivery green in the one-roster Ratchet live run; ActionForwardingContract transition-deferral coverage green`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `docs/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `git status --short`

### Previous handoff (2026-04-22)
- Last updated: 2026-04-22 (P5.1)
- Active task: none — P5.1 loadout ACK consumption shipped; next narrow slice
  would extend ACK-driven short-circuit into `HandleQueueForBattleground` if a
  concrete failure driver shows up.
- Last delta:
  - `BattlegroundCoordinator.HandleApplyingLoadouts` now pre-stamps each
    dispatched `ApplyLoadout` with `bg-coord:loadout:<account>:<guid>` and
    records it in `_loadoutCorrelationIds`. `RecordLoadoutProgressFromSnapshots`
    consults `LastAck(correlationId, snapshots)` before snapshot.LoadoutStatus,
    so terminal ACKs (Success/Failed/TimedOut) resolve the account even when
    LoadoutStatus never flips (pre-task rejection, step TimedOut).
  - `LastAckStatus` factored into `LastAck` + thin `LastAckStatus` wrapper.
    Coordinator consumers get the failure reason; the `P4.5.1` status-only
    callers (including `BattlegroundCoordinatorAckTests`) keep working.
  - No changes to `CharacterStateSocketListener` — `StampDispatchCorrelationId`
    already respects non-empty ids, so the coordinator stamp survives
    end-to-end.
- Pass result: `P5.1 loadout ACK consumption green (22/22 BattlegroundCoordinator tests)`
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release -v minimal` -> `succeeded (1062 pre-existing warnings, 0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BattlegroundCoordinator" -v minimal` -> `passed (22/22)`
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/BattlegroundCoordinatorLoadoutTests.cs`
  - `docs/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `rg -n "AssertCommandSucceeded|AssertTraceCommandSucceeded" Tests/BotRunner.Tests/LiveValidation`

### Previous handoff (2026-04-20)
- Last updated: 2026-04-20
- Active task: roll the now-proven desired-party queue contract into the next battleground objective slice.
- Last delta:
  - The previously shipped desired-party contract is now proven live for WSG. With the BotRunner-side party-size fix in place, Horde leaders convert to raid correctly, StateManager continues publishing faction desired-party membership, and the runtime queue flow reaches both the single-capture and full-game objective completions.
  - No new `Services/WoWStateManager` runtime code changed in this follow-up pass; this slice closed the validation gap against the existing `CharacterStateSocketListener` / `BattlegroundCoordinator` behavior.
  - The WSG objective scenarios now run on separate fresh fixture collections so each destructive live objective starts from a clean roster instead of inheriting the previous full match's transition residue.
- Pass result: `StateManager desired battleground party-state publishing is now proven by green live WSG objective coverage`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
- Files changed:
  - `Services/WoWStateManager/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`
- Previous handoff notes:
- Last delta:
  - Closed `WSM-PAR-001` by current live evidence. `QuestInteractionTests.Quest_AddCompleteAndRemove_AreReflectedInSnapshots` passed and proved GM-driven quest add/complete/remove state propagates from WoWSharpClient quest-log updates through BotRunner snapshot serialization into StateManager query responses.
  - Evidence artifact: `tmp/test-runtime/results-live/quest_snapshot_wsm_par_rerun.trx`, including `[FG] After add: QuestLog1=786 QuestLog2=0 QuestLog3=0`, `[BG] After add: QuestLog1=786 QuestLog2=4 QuestLog3=0`, successful `.quest complete 786`, successful `.quest remove 786`, and a passing result.
  - No WoWStateManager runtime code changed for `WSM-PAR-001`; the latency item was stale relative to current snapshot behavior.
  - Completed `WSM-BOOT-001`. `MangosServerOptions` now defaults to `AutoLaunch=false` with no default `C:\Mangos\server` directory, and `Services/WoWStateManager/appsettings.json` plus `Tests/BotRunner.Tests/appsettings.test.json` disable MaNGOS auto-launch by default.
  - `MangosServerBootstrapper` now skips immediately if auto-launch is explicitly enabled without `MangosServer:MangosDirectory`, so an incomplete opt-in cannot fall through into host process launch assumptions.
  - Added `MangosServerBootstrapperTests` to pin the external-server default and the no-directory opt-in guard.
  - Updated Docker stack and technical notes docs: Docker `realmd`/`mangosd` are the default ownership path; Windows host MaNGOS process launch is legacy opt-in only.
  - Deferred `D3` is closed for StateManager/BG coordinator behavior by current WSG evidence: `WSG_PreparedRaid_QueueAndEnterBattleground` reached `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[WSG:Final] onWsg=20, totalSnapshots=20`.
  - No WoWStateManager code changed in this D3 closeout; the pass verified the existing coordinator queue/invite/map-transfer path after the earlier AB/AV queue-entry work.
  - Deferred `D1` is closed by evidence from the StateManager launch path: the real `AlteracValley.config.json` includes the `AVBOTA1-40` Alliance roster, and the new `WoWStateManagerLaunchThrottleTests.AlteracValleySettings_IncludeAllianceAccountsInLaunchOrder` regression proves all 40 runnable Alliance accounts remain in `StateManagerWorker.OrderLaunchSettings(...)`.
  - Deferred `D2` is closed for StateManager/BG coordinator behavior by current AB evidence plus prior AV evidence: AB reached `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[AB:BG] 20/20 bots on BG map`; AV remains covered by the earlier full-match pass with `BG-SETTLE bg=80,off=0`.
  - No WoWStateManager code changed in this D1/D2 closeout; the change is fixture/test coverage proving the existing launch and queue coordinator behavior.
  - Session 300 tightened `BattlegroundCoordinator` queue/invite behavior for AV stragglers: queue and invite phases now restage off-position accounts with throttled `Goto` actions before issuing join/accept retries.
  - Added queue-settle orchestration in AV live validation so the coordinator remains active through delayed invite pops, then objective push starts from a settled in-map roster.
  - Live AV proof now reaches `BG-SETTLE bg=80,off=0` and passes `AV_FullMatch_EnterPrepQueueMountAndReachObjective`.
  - Session 299 validated the current ownership split end-to-end on the live Linux service stack: `pathfinding-service` and `scene-data-service` are running separately, reachable on `5001`/`5003`, and `WoWStateManager` remains host-side and process-scope-limited to WoW client workers.
  - Confirmed there is no remaining StateManager lifecycle ownership over `PathfindingService`/`SceneDataService`; startup and worker loops only probe/report dependency availability while continuing launch orchestration.
  - Current test status on this branch: `run-tests.ps1 -Layer 4 -SkipBuild` passed; a full `BotRunner.Tests` `LiveValidation` sweep was started and then interrupted by user request before completion, so the live matrix remains in-progress.
  - Validation:
    - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> `pathfinding-service` and `scene-data-service` both running with expected host ports
    - `docker logs --tail 80 pathfinding-service` -> map preload from mounted `/wwow-data`
    - `docker logs --tail 80 scene-data-service` -> ready on `0.0.0.0:5003` with initialized map set
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
  - Session 298 removed the remaining StateManager startup gate that still blocked bot launch on `PathfindingService` readiness. `StateManagerWorker.ApplyDesiredWorkerState(...)` now treats `PathfindingService` and `SceneDataService` as external dependencies: it probes both endpoints for diagnostics, logs readiness/unavailability, and continues launching configured WoW clients either way.
  - This aligns runtime behavior with the ownership split: `Program.Main` and worker startup now both use warn-only external-dependency semantics rather than launch gating for split services that StateManager no longer manages.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - Session 297 completed `WSM-HOST-001`. `WoWStateManager` no longer launches or kills `PathfindingService`/`SceneDataService`; `Program.Main` now treats both as external dependencies, performs bounded readiness checks only, and then proceeds to client orchestration.
  - `docker-compose.windows.yml` now splits the runtime services into separate containers (`pathfinding-service` and `scene-data-service`) and wires `background-bot-runner` to both endpoints.
  - `Services/WoWStateManager/appsettings.Docker.json` now includes `SceneDataService` host/port so spawned BG workers inherit the same split-service defaults.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`
  - Session 296 updated the scene-data bring-up contract to match the BG worker's deferred slice client. `WoWStateManager` still launches `SceneDataService`, but its timeout warning no longer claims BG runners will definitely fall back to local preloaded-map physics; the warning now states that bots will still launch and retry scene-slice acquisition on demand once the service becomes available.
  - Practical implication for this owner: a late `SceneDataService` no longer implies a permanent runtime downgrade. The old session-295 log line (`without scene slices`) is historical, not current behavior.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BackgroundPhysicsModeResolverTests|FullyQualifiedName~BotRunner.Tests.IPC.ProtobufSocketPipelineTests.DeferredConnect_ClientCanBeConstructedBeforeServerStarts" --logger "console;verbosity=minimal"` -> `passed (14/14)`
  - Session 296 finished the RFC coordinator cleanup tracked in the master file. `CharacterStateSocketListener` now constructs `DungeoneeringCoordinator` without the old prep-skip toggle, and the RFC test fixture disables the coordinator during prep so StateManager only coordinates group formation/RFC entry after fixture staging is done.
  - The coordinator itself now transitions from `WaitingForBots` straight into `FormGroup_Inviting`, which keeps the RFC path out of the old prep-action flow even if the coordinator comes online early.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CoordinatorStrictCountTests|FullyQualifiedName~CoordinatorFixtureBaseTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - Session 295 changed `SceneDataService` bootstrap from a hard startup gate into a best-effort dependency. `WoWStateManager` still launches the service, but `WaitForSceneDataService()` now gives it only a short `2.5s` window before continuing so BG workers can fall back to pure local preloaded-map physics instead of stalling fixture startup for two minutes.
  - Live AV proof moved accordingly: `WoWStateManager` now reaches `READY` with `SceneDataService` unavailable, and the latest full AV first-objective rerun no longer fails at the old scene-service startup skip. It still stalls earlier at `40/80` during bring-up, so the remaining blocker is launch pressure / alliance-wave startup, not the scene-data gate.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
    - `$env:WWOW_BOT_OUTPUT_DIR='E:\repos\Westworld of Warcraft\Bot\Release\net8.0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"` -> `failed` at `[AV:EnterWorld] STALE - bot count stopped at 40/80 for 123s`; `WoWStateManager` reached port `8088` and logged `SceneDataService did not become available ... Background bots will use local Navigation.dll physics without scene slices.`
  - Session 294 extended the spawned-BG endpoint contract to include `SceneDataService__IpAddress` / `SceneDataService__Port`. `StateManagerWorker.StartBackgroundBotWorker(...)` now forwards those values from config or the `WWOW_SCENE_DATA_*` env vars, so BG runners can stay on the scene-backed local physics path instead of launching without a scene endpoint.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release -o E:\tmp\isolated-wowstatemanager\bin --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `WoWStateManager` is now treated as host-side by design because it must launch local `WoW.exe` clients; the Windows compose stack should no longer include a `wow-state-manager` container.
  - Kept the idle host-side `WoWStateManager` path in place with `MangosServer__AutoLaunch=false` and `WWOW_SETTINGS_OVERRIDE=StateManagerSettings.Idle.json`.
  - Updated the stack docs so the containerized pieces stay `vmangos-server` / `pathfinding-service`, while `WoWStateManager` remains outside Docker.
- Pass result: `WSM-PAR-001 and WSM-BOOT-001 closed; owner backlog is empty`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.QuestInteractionTests.Quest_AddCompleteAndRemove_AreReflectedInSnapshots" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=quest_snapshot_wsm_par_rerun.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MangosServerBootstrapperTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (24/24)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CoordinatorStrictCountTests.BattlegroundCoordinator_WaitForInvite_RetriesJoinThenAcceptForOffMapAccountsAfterDelay|FullyQualifiedName~CoordinatorStrictCountTests.BattlegroundCoordinator_QueuePhase_RestagesUnstagedMembersBeforeJoin|FullyQualifiedName~CoordinatorStrictCountTests.BattlegroundCoordinator_DoesNotAdvanceToInBattleground_UntilEveryBotEntered" --logger "console;verbosity=minimal"` -> `passed`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_iteration_20260409_objective_tolerance60.trx"` -> `passed (1/1)`.
  - `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"` -> confirms split external services are online
  - `docker logs --tail 80 pathfinding-service` -> confirms pathfinding preload activity
  - `docker logs --tail 80 scene-data-service` -> confirms scene-data ready state
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -Layer 4 -SkipBuild -TestTimeoutMinutes 15` -> `passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LiveValidation" --logger "console;verbosity=minimal"` -> `interrupted by user`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateManagerTestClientTimeoutTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~SceneDataServiceAssemblyTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_queue_entry_d2_after_ab_10v10_single_fg.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_transfer_d3_rerun.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Services/WoWStateManager/Coordination/BattlegroundCoordinator.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorStrictCountTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Services/WoWStateManager/MangosServerOptions.cs`
  - `Services/WoWStateManager/MangosServerBootstrapper.cs`
  - `Services/WoWStateManager/appsettings.json`
  - `Tests/BotRunner.Tests/MangosServerBootstrapperTests.cs`
  - `Tests/BotRunner.Tests/appsettings.test.json`
  - `docs/DOCKER_STACK.md`
  - `docs/TECHNICAL_NOTES.md`
  - `Services/WoWStateManager/TASKS.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
  - `docs/DOCKER_STACK.md`
  - `Services/README.md`
  - `Services/WoWStateManager/StateManagerWorker.cs`
  - `Services/WoWStateManager/Program.cs`
  - `Services/WoWStateManager/appsettings.json`
  - `Services/WoWStateManager/appsettings.Docker.json`
  - `Services/WoWStateManager/TASKS.md`
  - `Services/WoWStateManager/TASKS_ARCHIVE.md`
  - `Services/SceneDataService/Dockerfile`
  - `docker-compose.windows.yml`
  - `docs/DOCKER_STACK.md`
- Next command: `rg -n "^- \[ \]|\[ \] Problem|Active task:" docs/TASKS.md Services/WoWStateManager/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Exports/BotRunner/TASKS.md`
- Blockers: none currently task-tracked in this owner.
