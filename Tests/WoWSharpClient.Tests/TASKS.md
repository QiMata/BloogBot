# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded directional remote-unit packet fixtures so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Keep remote extrapolation work focused on fixture-backed parity gaps; deterministic math thresholds and basis handling are already covered here.
3. Keep the movement-opcode sweep closed by adding coverage only when a new binary-backed non-cheat dispatch gap is discovered.

## Session Handoff
- Last updated: `2026-04-08 (session 300)`
- Pass result: `scene-data tile presence checks added for previously missing city-side startup tiles`
- Last delta:
  - Session 300 added direct live tile assertions in `SceneDataClientIntegrationTests` for the previously missing startup keys:
    - `LiveService_Map0_48_32_TileExists`
    - `LiveService_Map1_28_41_TileExists`
  - Both tests use `ProtobufSocketClient<SceneTileRequest, SceneTileResponse>` and verify `Success=true` with `TriangleCount>0` on the exact tile key, instead of relying on neighborhood refresh success alone.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map0_48_32_TileExists|FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map1_28_41_TileExists" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - Session 296 added direct deterministic coverage for the scene-slice client transport seam. `SceneDataClientTests.EnsureSceneDataAround_SuppressesImmediateRetryAfterFailure` and `EnsureSceneDataAround_RetriesAfterBackoffExpires` now prove the client applies a short retry backoff after a failed request instead of hammering the socket every frame.
  - Kept the controller-side slice assertions green in the same focused run: `MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode` and `Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure`.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - Session 295 added deterministic coverage for the pure-local fallback that remained unpinned after the earlier AV hover fixes. `ObjectManagerWorldSessionTests.EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController` proves the object manager now constructs `MovementController` when `useLocalPhysics=true` even with no remote/shared physics client and no scene client.
  - Kept the earlier guard in place with `Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient`, so the local-preloaded path stays pure-local instead of quietly collapsing back to shared physics.
  - Practical implication: when `SceneDataService` is down, BG runners still get a local per-frame physics loop and can fall/settle instead of hovering because no controller was created.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient|FullyQualifiedName~ObjectManagerWorldSessionTests.EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - Session 294 added deterministic coverage for the two live AV hover signatures that remained after the earlier scene-refresh fix. `MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall` proves a post-teleport no-ground frame keeps the bot descending during the settle grace window, and `Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling` proves the controller rejects overhead support and stays in `FALLINGFAR`.
  - Practical implication: this suite now pins the exact controller behaviors needed to keep BG bots from freezing above the AV battlemasters while local scene data or collision settle catches up.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin4 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~MovementControllerTests.Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - Session 293 added deterministic coverage for the next local-physics memory/scaling risk. `MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode` proves the scene-backed controller enables thin-scene-slice mode at construction time, `Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode` proves native local controllers without scene data clear the mode, and `Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure` continues to prove the hover fix on refresh misses.
  - Practical implication: the BG local path is now explicitly pinned to nearby injected geometry instead of being able to drift back into hidden full-map native data loads during AV launch pressure, while remote/shared controllers still avoid a hard native-DLL dependency.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin3 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Session 291 added deterministic coverage for the remaining scene-backed hover bug. `MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure` now forces `SceneDataClient` to miss a refresh and proves the controller still runs the local native step, enters `FALLINGFAR`, and updates the Z position instead of freezing in place.
  - Added the supporting internal `SceneDataClient` test seam so the refresh-miss path can be exercised without a live socket.
  - Validation for this delta used an isolated output directory because the shared `Bot\Release\net8.0` tree was still locked by active AV/background processes from the live swarm.
  - Session 188 replaced the stale deterministic assumption that `SendFacingUpdate(...)` must emit a pre-facing heartbeat. The new binary audit of `WoW.exe` `0x60E1EA` shows the explicit facing send is gated by the float at `0x80C408` (`0.1f`) and falls directly into the movement send helper without a synthetic heartbeat.
  - `MovementControllerTests` now pin the corrected send behavior with `SendFacingUpdate_StandingStill_SendsSetFacingOnly` and `SendFacingUpdate_AfterMovement_SendsSetFacingOnly`.
  - `ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket` now uses a true sub-threshold delta (`0.08 rad`) so the deterministic object-manager coverage matches the same `0.1 rad` binary gate the runtime now uses.
  - The managed-facing live proof stayed green on rerun: the forced-turn Durotar route still captures `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` from both FG and BG, so the remaining live mismatch is native Z drift rather than packet-ordering drift.
  - Session 187 added deterministic coverage for the stop-edge parity fix instead of leaving it live-only. `MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame` proves a queued airborne stop clears forward intent and sends `MSG_MOVE_STOP` on the first grounded update.
  - The same session also relied on new BotRunner-side deterministic coverage for the arrival rule that was holding the forced-turn route open: `GoToArrivalTests` now pin horizontal arrival tolerance plus the exhausted-path 2D recalc guard.
  - The forced-turn live Durotar route is now closed through the stop edge as well as the start edge. The remaining deterministic gap is no longer stop ordering; it is any test coverage needed by the next pause/resume ownership fix once that live slice exposes one.
  - Session 186 added deterministic coverage for the new live parity plumbing instead of relying only on the live route. `WoWClientTests.SendMovementOpcodeAsync_FiresMovementOpcodeSent` now proves the BG packet-sidecar event fires from the send path, `MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly` covers the new movement-start helper, and `ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent` proves the object manager uses that helper on the start edge.
  - The remaining movement-opcode gap is now narrower and better evidenced: the forced-turn live Durotar route proves both the shared `SET_FACING -> START_FORWARD` opening edge and the bounded stop edge, so this owner no longer needs more deterministic work for stop ordering on that slice. The remaining live gap is the pause/resume ownership slice.
  - Session 181 stabilized the foreground recording source the packet-parity harness depends on. FG `StartMovement` / `StopMovement` now dispatch the native `SetControlBit(...)` path on WoW's main thread, which cleared the prior `SetControlBitSafeFunction(...)` `NullReferenceException` during automated captures instead of relying on Lua fallback.
  - The canonical packet-backed Undercity fixtures were promoted to the stronger final March 25 captures: `Urgzuga_Undercity_2026-03-25_10-00-52` for the underground lower route and `Urgzuga_Undercity_2026-03-25_10-01-09` for the west elevator up-ride. Older superseded Urgzuga Undercity attempts were pruned from the canonical corpus.
  - `RecordingMaintenance capture` now auto-cleans duplicate `Bot/*/Recordings` output trees after each run, which keeps the replay-backed parity loop from reintroducing the large debug-output copy after every FG capture session.
  - Session 176 converted the recorded-frame movement parity harness from "packet-aware but usually deferred" into a real FG/BG proof. The test now requires a clean grounded forward segment with a grounded stop frame, creates synthetic preroll when the capture starts mid-run, and executes the stop transition so `START_FORWARD` / heartbeat / `STOP` can be compared against real FG packets.
  - Fresh packet-backed captures from the canonical recording corpus are now exercised directly by this suite. `RecordedFrames_WithPackets_OpcodeSequenceParity` selects `Urgzuga_Durotar_2026-03-25_03-07-08` and shows the expected FG/BG distribution on a straight run instead of falling back to packetless legacy data.
  - Updated `MovementControllerTests` timing coverage to match the packet-backed FG cadence of ~500ms while moving. The stale 100ms controller assumption is gone from the deterministic timing tests.
  - The remaining movement-opcode gap is no longer fixture availability; it is exact live FG/BG pause/resume timing and state ownership on matched corpse-run / combat travel traces.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_RunsRemotePhysicsEveryIdleFrame|FullyQualifiedName~MovementControllerTests.Update_KeepsLocalPhysicsActiveWhileIdle|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_IdleFreefallStillAppliesPhysics" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_StandingStill_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_AfterMovement_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> first rerun `failed` at stop-edge delta `609ms`; immediate rerun `passed (1/1)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; live stop-edge parity confirmed with final outbound `MSG_MOVE_STOP` from both clients
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWClientTests.SendMovementOpcodeAsync_FiresMovementOpcodeSent|FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~MovementControllerRecordedFrameTests.IsRecording_CapturesPhysicsFramesWithPacketMetadata|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `13 passed`
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release` -> succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `45 passed`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataClientTests.cs`
  - `Exports/WoWSharpClient/Movement/SceneDataClient.cs`
  - `Exports/BotCommLayer/ProtobufSocketClient.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Client/WoWClientTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tools/RecordingMaintenance/Program.cs`
  - `Services/ForegroundBotRunner/MovementScenarioRunner.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tests/ForegroundBotRunner.Tests/MovementScenarioRunnerTests.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerRecordedFrameTests.cs`
- Next command:
  - `$env:WWOW_DATA_DIR='E:\repos\Westworld of Warcraft\Data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"`

## Prior Session
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added observer-state coverage for the remaining non-cheat Vanilla movement rebroadcasts discovered in the dispatch-table sweep: swim start/stop plus pitch start/stop/set.
  - Remote-unit tests now prove those packets update `MOVEFLAG_SWIMMING` and `SwimPitch` through the same managed path the object manager uses at runtime.
  - `WorldClient` bridge-registration coverage now includes the new swim/pitch opcodes, reducing the outstanding movement-opcode work to future binary-backed discoveries rather than known gaps.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObserverMovementPitchOpcodes_UpdateRemoteUnitSwimPitch" -v n` -> `16 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1346 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
