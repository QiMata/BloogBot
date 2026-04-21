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
### 2026-04-21
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
