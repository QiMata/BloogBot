# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - `WSM-PAR-001` Quest Snapshot Sync Evidence Closeout

- [x] Traced and revalidated quest-state propagation between WoWSharpClient quest updates, BotRunner snapshot publication, and StateManager query assertions.
- Completion notes:
  - No WoWStateManager runtime code changed for this item.
  - Current live evidence shows GM-driven quest add/complete/remove transitions propagate through the snapshot pipeline within the existing polling windows.
  - Artifact: `tmp/test-runtime/results-live/quest_snapshot_wsm_par_rerun.trx`.
  - Evidence includes `[FG] After add: QuestLog1=786 QuestLog2=0 QuestLog3=0`, `[BG] After add: QuestLog1=786 QuestLog2=4 QuestLog3=0`, successful `.quest complete 786`, successful `.quest remove 786`, and a passing test result.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.QuestInteractionTests.Quest_AddCompleteAndRemove_AreReflectedInSnapshots" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=quest_snapshot_wsm_par_rerun.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - `WSM-BOOT-001` MaNGOS Host Auto-Launch Opt-In

- [x] Re-checked and removed the remaining default assumption that local `C:\Mangos\server` processes are host-launched.
- Completion notes:
  - `MangosServerOptions` now defaults to `AutoLaunch=false` and an empty `MangosDirectory`.
  - `Services/WoWStateManager/appsettings.json` and `Tests/BotRunner.Tests/appsettings.test.json` now disable `MangosServer:AutoLaunch` by default and no longer carry a default host MaNGOS directory.
  - `MangosServerBootstrapper` returns early when auto-launch is explicitly enabled without `MangosServer:MangosDirectory`.
  - `docs/DOCKER_STACK.md` and `docs/TECHNICAL_NOTES.md` now state that Docker `realmd`/`mangosd` ownership is the default and Windows host MaNGOS process launch is legacy opt-in.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MangosServerBootstrapperTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal"` -> `passed (24/24)`

## Archived Snapshot (2026-04-15) - Deferred D3 WSG Coordinator Evidence Closeout

- [x] Closed deferred `D3` for StateManager/BG coordinator map-transfer behavior by current WSG live evidence.
- Completion notes:
  - No WoWStateManager runtime code changed in this closeout.
  - The WSG live proof reached `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[WSG:Final] onWsg=20, totalSnapshots=20`.
  - This confirms the existing coordinator queue/invite/map-transfer path now covers WSG after the earlier AB and AV queue-entry proofs.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WarsongGulchTests.WSG_PreparedRaid_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_transfer_d3_rerun.trx"` -> `passed (1/1)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`

## Archived Snapshot (2026-04-15) - Deferred D1/D2 Evidence Closeout

- [x] Closed deferred `D1` for WoWStateManager launch ordering by evidence and regression coverage.
- [x] Closed deferred `D2` for StateManager/BG coordinator queue-entry behavior by current AB and prior AV live evidence.
- Completion notes:
  - No WoWStateManager runtime code changed in this closeout.
  - The new launch-order regression loads the real AV config and proves all `AVBOTA1-40` runnable Alliance accounts remain in `StateManagerWorker.OrderLaunchSettings(...)`.
  - The AB live proof reached `BG_COORD: All 20 bots queued`, `BG_COORD: 20/20 bots on BG map`, and `[AB:BG] 20/20 bots on BG map`; AV remains covered by the existing full-match proof with `BG-SETTLE bg=80,off=0`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~WoWStateManagerLaunchThrottleTests" --logger "console;verbosity=minimal"` -> `passed (20/20)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_queue_entry_d2_after_ab_10v10_single_fg.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-03) - `WSM-HOST-001` complete

- [x] Validate `WoWStateManager` as a host-side process against containerized `vmangos-server` dependencies.
- [x] Verify spawned BG workers inherit docker-safe endpoint overrides while `WoWStateManager` remains outside Docker.
- Completion notes:
  - `WoWStateManager` no longer launches or stops `PathfindingService`/`SceneDataService`; it only performs external readiness checks and continues with WoW-client orchestration.
  - Windows compose now runs `pathfinding-service` and `scene-data-service` as separate containers, and `background-bot-runner` is wired to both endpoints.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`

## Archived Snapshot (2026-04-03) - RFC coordinator-only lifecycle

- [x] Keep RFC fixture prep outside `DungeoneeringCoordinator`.
- [x] Ensure StateManager only uses the RFC coordinator for coordination after prep is complete.
- Completion notes:
  - `CharacterStateSocketListener` now constructs `DungeoneeringCoordinator` without the old prep-skip toggle.
  - `RfcBotFixture` disables the coordinator during prep and re-enables it only after revive/level/spell/gear/Orgrimmar staging has completed.
  - `DungeoneeringCoordinator` now leaves `WaitingForBots` directly for `FormGroup_Inviting`, which keeps the RFC coordinator path out of the old prep-action flow.

## Archived Snapshot (2026-02-24 19:43:32) - Services/WoWStateManager/TASKS.md

- [x] Ensure coordinator suppression windows never hide test-forwarded actions. Evidence: `INJECTING PENDING ACTION ... (coordinator suppressed 300s)` present for ReleaseCorpse/Goto/RetrieveCorpse in `tmp/deathcorpse_run_current.log`.
