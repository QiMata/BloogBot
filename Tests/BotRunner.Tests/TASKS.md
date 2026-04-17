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
- Last updated: `2026-04-17`
- Pass result: `Configurable FG ACK probe harness closed the final three ACK corpus gaps and AckParity is green across all wired ACKs`
- Last delta:
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
