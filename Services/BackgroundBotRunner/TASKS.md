# BackgroundBotRunner Tasks

## Scope
- Directory: `Services/BackgroundBotRunner`
- Project: `BackgroundBotRunner.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: headless runner lifecycle, docker packaging, and FG/BG behavior parity through the shared BotRunner stack.

## Execution Rules
1. Keep changes scoped to the worker plus directly related startup/config call sites.
2. Every parity or lifecycle slice must leave a concrete validation command in `Session Handoff`.
3. Never blanket-kill repo processes; use repo-scoped cleanup or explicit PIDs only.
4. Archive completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
None.

Known remaining work in this owner: `0` items.

## Session Handoff
- Last updated: 2026-04-19
- Active task: `none`
- Last delta:
  - `BackgroundBotWorker.RebindAgentFactoryIfNeeded()` now binds the agent factory as soon as a fresh `WorldClient` instance exists, instead of waiting for `IsConnected=true`.
  - That keeps early battleground/friend/ignore opcode handlers registered during the world handshake, which the live battleground queue/entry reruns were missing on fresh worker startup.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - Closed `BBR-PAR-002`: Docker-backed BG gathering and NPC timing are green against the live vmangos stack.
  - Closed `BBR-DOCKER-001`: the supported Docker endpoint contract is green for pathfinding and scene-data, and the `WoWStateManager`-spawned BG worker path is covered by the live gathering/NPC runs.
  - Extended deterministic server-trigger parity to include stop-before-use sequencing: `ForceStopImmediate()` now blocks until `MSG_MOVE_STOP` is recorded before game-object use/cast packets can fire.
  - The current compose/docs do not define a separate standalone `BackgroundBotRunner` container; validation is therefore scoped to the supported Docker services plus WSM-spawned BG workers.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
    - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Herbalism_BG_GatherHerb" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=herbalism_bg_retry_try_again.trx"` -> `passed (1/1)`
    - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests.Vendor_VisitTask_FindsAndInteracts" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=npc_vendor_visit_docker_timing.trx"` -> `passed (1/1)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DockerServiceTests.PathfindingService_TcpConnect_Responds|FullyQualifiedName~DockerServiceTests.SceneDataService_TcpConnect_Responds" --logger "console;verbosity=minimal"` -> `passed (2/2)`
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - Next command:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
    - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
    - `Tests/BotRunner.Tests/Combat/GatheringRouteTaskTests.cs`
    - `Tests/BotRunner.Tests/LiveValidation/GatheringProfessionTests.cs`
    - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelection.cs`
    - `Tests/BotRunner.Tests/LiveValidation/GatheringRouteSelectionTests.cs`
    - `Exports/WoWSharpClient/Movement/MovementController.cs`
    - `Exports/WoWSharpClient/InventoryManager.cs`
    - `Exports/WoWSharpClient/SpellcastingManager.cs`
    - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
    - `Exports/BotRunner/Tasks/GatheringRouteTask.cs`
    - `Services/BackgroundBotRunner/TASKS.md`
- Pass result: `Early world-client agent binding is pinned and the AB background-only queue-entry rerun is green`
- Validation/tests run:
  - Same validation bundle listed above.
- Blockers: none.
