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
### 2026-04-28 (route-specific transport packet-window trigger)
- Pass result: `Background recorder captures Orgrimmar zeppelin transport windows from route-specific object-update evidence`
- Last delta:
  - `BackgroundPostTeleportWindowRecorder` now consumes
    `WoWClient.PacketReceivedDetailed` so it can classify decoded BG inbound
    payloads with the shared `PostTeleportWindowTriggerClassifier`.
  - The recorder still accepts `SMSG_MONSTER_MOVE_TRANSPORT`, and now also
    accepts configured-entry ordinary `SMSG_MONSTER_MOVE` mover GUIDs and
    `SMSG_UPDATE_OBJECT` / `SMSG_COMPRESSED_UPDATE_OBJECT` payload evidence.
  - Live probe with `WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES=164871` promoted the
    background source
    `tmp/test-runtime/zeppelin-transport-capture-20260428_03/background_20260428_175423_657.json`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - Live zeppelin probe with `WWOW_TRANSPORT_PACKET_WINDOW_ENTRIES=164871` -> `passed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-28 (transport packet-window trigger research)
- Pass result: `Background recorder recognizes the candidate transport opcode; live zeppelin route did not emit it`
- Last delta:
  - `BackgroundPostTeleportWindowRecorder` now opens
    `transport_packet_window` on inbound `SMSG_MONSTER_MOVE_TRANSPORT`.
  - The opt-in FG/BG Orgrimmar/Undercity zeppelin probe staged the BG target
    at the corrected MaNGOS Durotar zeppelin point (`1340.98, -4638.58,
    53.5445`) with transport entry `164871`.
  - The route produced only BG post-teleport staging fixtures and skipped after
    one route cycle with no `transport_packet_window`; next work should find
    the normal zeppelin trigger before promoting a BG baseline.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - Live zeppelin probe -> `test run successful; 1 skipped`, no background
    `transport_packet_window` fixture under
    `tmp/test-runtime/zeppelin-transport-capture-20260428_02`.
- Files changed:
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: `rg -n "SMSG_MONSTER_MOVE|SMSG_COMPRESSED_UPDATE_OBJECT|OBJECT_FIELD_ENTRY|TransportGuid|MOVEFLAG_ONTRANSPORT" Exports/WoWSharpClient Services/ForegroundBotRunner Services/BackgroundBotRunner Tests/BotRunner.Tests/LiveValidation docs/physics -g "!**/bin/**" -g "!**/obj/**"`

### 2026-04-28 (packet-window knockback scenario)
- Pass result: `Background packet-window recorder captures the Taragaman knockback window and BG emits ACK/JUMP/HEARTBEAT/FALL_LAND`
- Last delta:
  - `BackgroundPostTeleportWindowRecorder` now labels recorder windows by
    scenario and can open on transfer triggers, outbound
    `MSG_MOVE_WORLDPORT_ACK`, and inbound `SMSG_MOVE_KNOCK_BACK`.
  - `ForegroundAndBackground_Knockback_CapturesPacketWindows` produced the
    background Taragaman `Uppercut` knockback window used by the parity oracle.
  - The promoted BG fixture records
    `SMSG_MOVE_KNOCK_BACK -> CMSG_MOVE_KNOCK_BACK_ACK -> MSG_MOVE_JUMP ->
    MSG_MOVE_HEARTBEAT -> MSG_MOVE_FALL_LAND`.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_POST_TELEPORT_WINDOW='1'; $env:WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW='1'; $env:WWOW_POST_TELEPORT_WINDOW_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/knockback-capture-20260428_09'; $env:WWOW_BG_POST_TELEPORT_OUTPUT='E:/repos/Westworld of Warcraft/tmp/test-runtime/knockback-capture-20260428_09'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_LOG_LEVEL='Information'; $env:WWOW_FILE_LOG_LEVEL='Information'; $env:WWOW_CONSOLE_LOG_LEVEL='Warning'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundAndBackground_Knockback_CapturesPacketWindows" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fg_bg_knockback_window_09.trx"` -> `passed (1/1)`.
- Files changed:
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: `rg -n "TransportGuid|ON_TRANSPORT|SMSG_MONSTER_MOVE_TRANSPORT|TaxiTransportParityTests|TransportTests" Tests/BotRunner.Tests/LiveValidation Services docs/physics -g "!**/bin/**" -g "!**/obj/**"`

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
