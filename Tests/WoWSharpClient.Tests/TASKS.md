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
- Last updated: `2026-03-25 (session 187)`
- Pass result: `delta shipped`
- Last delta:
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
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; live stop-edge parity confirmed with final outbound `MSG_MOVE_STOP` from both clients
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWClientTests.SendMovementOpcodeAsync_FiresMovementOpcodeSent|FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~MovementControllerRecordedFrameTests.IsRecording_CapturesPhysicsFramesWithPacketMetadata|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `13 passed`
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release` -> succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `45 passed`
- Files changed:
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
  - `Get-Content Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs | Select-Object -Skip 320 -First 220`

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
