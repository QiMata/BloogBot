# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - deterministic server-packet movement parity hook

- [x] BG server-packet movement trigger behavior is now included in the deterministic full parity surface.
  - `Tests/WoWSharpClient.Tests` `Category=MovementParity` covers force-speed/root, server movement flag toggles, compressed trigger variants, knockback ACKs, and next-frame `MovementController` knockback consumption.
  - This closes the unit-level `MovementHandler -> ObjectManager -> MovementController` parity test gap for Background BotRunner without replacing the live FG/BG route bundle.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`

## Archived Snapshot (2026-04-15) - Docker live gathering/NPC and endpoint closeout

- [x] `BBR-PAR-002` Live gathering/NPC timing.
  - Docker-backed BG herbalism now handles the server `SPELL_FAILED_TRY_AGAIN` path by retrying the same visible node after the stop/use/cast sequence.
  - NPC vendor interaction timing is green against the same live Docker vmangos stack.
- [x] `BBR-DOCKER-001` Containerized worker validation.
  - The supported Docker endpoint contract is validated for `PathfindingService` and `SceneDataService`.
  - The current compose/docs do not expose a separate standalone `BackgroundBotRunner` container profile; live validation therefore proves the supported `WoWStateManager`-spawned BG worker path against Docker services.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Herbalism_BG_GatherHerb" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=herbalism_bg_retry_try_again.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NpcInteractionTests.Vendor_VisitTask_FindsAndInteracts" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=npc_vendor_visit_docker_timing.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DockerServiceTests.PathfindingService_TcpConnect_Responds|FullyQualifiedName~DockerServiceTests.SceneDataService_TcpConnect_Responds" --logger "console;verbosity=minimal"` -> `passed (2/2)`
