# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. `P2` packet-handling / ACK parity is closed; add new deterministic coverage only when a new WoW.exe-backed gap is found.
2. Keep `AckParity`, `PacketFlowParity`, and `StateMachineParity` green on future packet/state changes.
3. Keep the movement-opcode sweep closed by adding coverage only when a new binary-backed non-cheat dispatch gap is discovered.
4. Keep BG server-packet movement triggers in the full `Category=MovementParity` bundle, covering `MovementHandler -> WoWSharpObjectManager -> MovementController`.

## Session Handoff
### 2026-04-28 (direct movement activity deterministic coverage)
- Pass result: `Focused WoWSharpClient movement/object deterministic slice passed 6/6`
- Last delta:
  - Added deterministic coverage for passive gameobject transport attach,
    map-object transport deck offsets, moving transport high GUID creation,
    self-GM knockback packet parsing without forced ACK, and high GUID
    object-update create blocks with packet type `None`.
  - The coverage intentionally separates gameobject transports from taxi
    spline movement.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_IdleNearGameObjectTransport_AttachesBeforePostTeleportGroundSnap|FullyQualifiedName~MovementControllerTests.Update_IdleNearMapObjectTransportDeck_AttachesWithZeppelinOriginOffset|FullyQualifiedName~ObjectManagerWorldSessionTests.DirectMonsterMove_MovingTransportHighGuid_CreatesGameObjectTransport|FullyQualifiedName~ObjectManagerWorldSessionTests.MessageMoveKnockBack_PrimesImpulseWithoutForceAck|FullyQualifiedName~ObjectUpdateMutationOrderTests.MovingTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject|FullyQualifiedName~ObjectUpdateMutationOrderTests.StaticTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/ObjectUpdateMutationOrderTests.cs`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `git status --short --branch`

### 2026-04-28 (tracker sweep after Stream 4 closeout)
- Pass result: `Post-Stream-4 tracker sweep found no unchecked parity tasks`
- Last delta:
  - Confirmed the transport object-update baseline entry is the current
    WoWSharpClient parity state; the earlier "transport research remains open"
    handoff is historical and superseded by
    `OrgrimmarZeppelinTransportBaselines_PinRouteObjectUpdateTrigger`.
  - Inspected the three untracked ACK corpus JSONs. They are valid
    `MSG_MOVE_TELEPORT_ACK` / `MSG_MOVE_WORLDPORT_ACK` live captures, but they
    do not add a new deterministic layout or timing shape to the committed
    corpus, so they remain untracked.
  - Corrected the physics audit wording so Stream 4 no longer reads as if
    transport/zeppelin capture remains open.
- Validation/checks run:
  - `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md` -> no matches.
  - ACK JSON inspection -> duplicate 20-byte teleport ACK and 4-byte worldport
    ACK corpus shapes; not promoted.
- Files changed:
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `docs/physics/bg_movement_parity_audit.md`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_category_latest.trx"`

### 2026-04-28 (transport packet-window object-update baselines)
- Pass result: `PostTeleportPacketWindowParityTests green with Orgrimmar zeppelin FG/BG object-update baselines (10/10)`
- Last delta:
  - Added `foreground_orgrimmar_zeppelin_transport_update_baseline.json` and
    `background_orgrimmar_zeppelin_transport_update_baseline.json`.
  - Added
    `OrgrimmarZeppelinTransportBaselines_PinRouteObjectUpdateTrigger`, which
    pins `transport_packet_window` capture on `SMSG_UPDATE_OBJECT` for route
    entry `164871`, verifies the FG raw payload contains
    `GAMEOBJECT_TYPE_ID = 15`, and proves FG/BG observe the same ordinary
    `SMSG_MONSTER_MOVE` sequence with no `SMSG_MONSTER_MOVE_TRANSPORT`.
  - `PacketPipeline` / `WorldClient` / `WoWClient` now expose decoded inbound
    packet payloads for the BG recorder's route-specific trigger logic.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (10/10; existing warnings; nonfatal dumpbin warning)`.
- Files changed:
  - `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Client/WoWClient.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - new transport packet-window fixtures
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-28 (transport packet-window trigger research)
- Pass result: `No WoWSharpClient baseline promoted; transport trigger research remains open`
- Last delta:
  - FG/BG packet-window recorders now recognize inbound
    `SMSG_MONSTER_MOVE_TRANSPORT` as `transport_packet_window`.
  - The opt-in live Orgrimmar/Undercity zeppelin probe staged both bots at the
    corrected transport point and waited one route cycle, but captured only
    `post_teleport_packet_window` staging fixtures. No committed
    `transport_packet_window` baseline was added.
  - Next parity work should identify the normal zeppelin movement trigger from
    live packet/object-update evidence before adding a WoWSharpClient fixture.
- Validation/tests run:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundPostTeleportWindowRecorderTests" --logger "console;verbosity=minimal"` -> `passed (6/6; existing nonfatal dumpbin warning)`.
  - Live zeppelin probe -> `test run successful; 1 skipped`, no
    `transport_packet_window` fixture under
    `tmp/test-runtime/zeppelin-transport-capture-20260428_02`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `rg -n "SMSG_MONSTER_MOVE|SMSG_COMPRESSED_UPDATE_OBJECT|OBJECT_FIELD_ENTRY|TransportGuid|MOVEFLAG_ONTRANSPORT" Exports/WoWSharpClient Services/ForegroundBotRunner Services/BackgroundBotRunner Tests/BotRunner.Tests/LiveValidation docs/physics -g "!**/bin/**" -g "!**/obj/**"`

### 2026-04-28 (FG worldport ACK + knockback packet-window baselines)
- Pass result: `PostTeleportPacketWindowParityTests green with worldport ACK and knockback baselines added (9/9)`
- Last delta:
  - Added `foreground_ek_to_kalimdor_worldport_ack_baseline.json`, captured
    from a live Eastern Kingdoms -> Kalimdor return transfer. The foreground
    transfer-pending window contains `SMSG_NEW_WORLD` followed by outbound
    `MSG_MOVE_WORLDPORT_ACK` at 1576ms with payload `DC000000`.
  - Added `foreground_knockback_baseline.json` and
    `background_knockback_baseline.json`, captured from Taragaman the
    Hungerer's real `Uppercut` knockback in Ragefire Chasm.
  - Added
    `ForegroundWorldportAckBaseline_PinsObservedAckInsideTransferWindow` and
    `KnockbackBaselines_PinFgAndBgAckShape`. The knockback oracle pins prompt
    `CMSG_MOVE_KNOCK_BACK_ACK`, FG jump vector shape, and BG's
    `MSG_MOVE_JUMP` / heartbeat / `MSG_MOVE_FALL_LAND` follow-up sequence.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MoveKnockBack|FullyQualifiedName~PendingKnockback|FullyQualifiedName~AckBinaryParityTests" --logger "console;verbosity=minimal"` -> `passed (46/46)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/StateMachineParityTests.cs`
  - new packet-window fixtures.
- Next command: `rg -n "TransportGuid|ON_TRANSPORT|SMSG_MONSTER_MOVE_TRANSPORT|TaxiTransportParityTests|TransportTests" Tests/BotRunner.Tests/LiveValidation Services docs/physics -g "!**/bin/**" -g "!**/obj/**"`

### 2026-04-28 (BG cross-map post-teleport baseline)
- Pass result: `PostTeleportPacketWindowParityTests green with BG cross-map baseline added (7/7)`
- Last delta:
  - Added `background_kalimdor_to_ek_cross_map_baseline.json`, captured live
    from BackgroundBotRunner during an Orgrimmar (Kalimdor) -> Ironforge
    (Eastern Kingdoms) hop.
  - Added `BackgroundCrossMapBaseline_PinsTransferPendingNewWorldShape`,
    which pins BG's transfer-pending window: `SMSG_TRANSFER_PENDING`,
    immediate zero-payload `MSG_MOVE_WORLDPORT_ACK`, `SMSG_NEW_WORLD`,
    destination object updates, and no `CMSG_SET_ACTIVE_MOVER`.
- Validation/tests run:
  - Initial sanity `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_kalimdor_to_ek_cross_map_baseline.json`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"`

### 2026-04-28 (BG post-teleport FALL_LAND parity baselines)
- Pass result: `PostTeleportPacketWindowParityTests green with refreshed live BG FALL_LAND baselines`
- Last delta:
  - `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence` now
    requires `MSG_MOVE_FALL_LAND` in the 10y live BG window.
  - `BackgroundHighDropBaseline_EmitsFallLand_AfterAirborneTeleportPriming`
    replaces the old current-bug oracle and pins the 100y extended-window
    landing packet.
  - Refreshed BG baselines:
    - `background_durotar_vertical_drop_baseline.json` -> FALL_LAND at 1253ms.
    - `background_durotar_high_drop_baseline.json` -> 10s window, FALL_LAND at 8357ms.
- Validation/tests run:
  - Initial sanity `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests"` -> `passed (6/6)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`
  - `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`
  - `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_high_drop_baseline.json`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"`

### 2026-04-24 (Wand Shoot protocol coverage)
- Pass result: `WoWSharpObjectManagerCombatTests green after BG wand Shoot fix`
- Last delta:
  - Added `StartWandAttack_WithSelectedTarget_SendsShootSpellAtUnit` to prove BG wand start emits `CMSG_CAST_SPELL` for Shoot (`5019`) with unit target flags and the selected target GUID.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
- Files changed:
  - `Tests/WoWSharpClient.Tests/WoWSharpObjectManagerCombatTests.cs`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

### 2026-04-21
- Pass result: `P4.1 deterministic handler/event coverage is green`
- Last delta:
  - Added deterministic coverage for learned/unlearned spell, skill-update, item-added, shared emitter, attack/inventory/spell error, and system-notification paths.
  - The new tests pin the exact BG handler/event bridge surface that `P4.3` will rely on, without starting any loadout-step behavior changes yet.
- Validation:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpEventEmitterTests|FullyQualifiedName~LootingNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientAttackErrorTests|FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientNotificationTests" --logger "console;verbosity=minimal"` -> `passed`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Agent/LootingNetworkAgentTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientAttackErrorTests.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/WorldClientNotificationTests.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpEventEmitterTests.cs`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
- Next command:
  - `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: `2026-04-19`
- Pass result: `Early battleground-status handler registration is pinned by deterministic factory coverage`
- Last delta:
  - Added `AgentFactoryTests.InitializeEssentialAgents_EagerlyRegistersEarlyWorldHandlers`, which proves the eager factory path now creates the Friend/Ignore/Battleground agents before the first login/world burst and registers the early battleground status opcodes deterministically.
  - That deterministic coverage supports the live battleground queue/entry stabilization slice: the background worker now binds the factory as soon as a new `WorldClient` exists, and the fresh AB queue/entry rerun stayed green once the fixture avoided the foreground transfer crash edge.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/Agent/AgentFactoryTests.cs`
    - `Exports/WoWSharpClient/Networking/ClientComponents/NetworkClientComponentFactory.cs`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
  - Next command:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
  - Synced the existing P2.4 replay/evidence work into the doc surface:
    - `ObjectUpdateMutationOrderTests` is green (`passed (4/4)`)
    - `0x466590_disasm.txt` now anchors the descriptor-walker ordering
    - `0x466C70_disasm.txt` now anchors the typed-storage switch and the no-separate-`CGPet_C` conclusion for packet-instantiated objects
  - That closes the remaining P2.4 tracker debt and, with the already-green parity bundles, closes the full P2 packet-parity effort.
  - Closed the remaining state-machine audit gap by adding parity-tagged tests for the documented root/unroot and knockback transitions:
    - `StateMachineParityTests.ForceMoveRootOpcodes_StageStateUntilDeferredFlush`
    - `StateMachineParityTests.MoveKnockBack_StagesImpulseUntilConsumedThenAcks`
  - `PacketFlowTraceFixture` now records `OnForceMoveUnroot` and dispatches `SMSG_FORCE_MOVE_UNROOT`, so the harness can assert both root-state branches rather than only the root edge.
  - Final regression gate for `P2.7.5` is now green:
    - `AckParity` -> `passed (29/29)`
    - `MovementParity` in `WoWSharpClient.Tests` -> `passed (32/32)`
    - `PacketFlowParity` -> `passed (8/8)`
    - `StateMachineParity` -> `passed (8/8)`
    - `MovementParity` in `Navigation.Physics.Tests` -> `passed (8/8)`
    - `NavigationPathTests` -> `passed (80/80)`
  - Added `StateMachineParityTests.ClientControlUpdate_LocalPlayer_FollowsCanControlAndBlocksReconcile`, which proves the local `canControl=false` edge persists across `ReconcilePlayerControlState()` until a matching `canControl=true` packet arrives.
  - The state-machine documentation set is now complete on the physics side:
    - `state_client_control.md`
    - `state_teleport.md`
    - `state_worldport.md`
    - `state_login.md`
    - `state_knockback.md`
    - `state_root.md`
  - Added `StateMachineParityTests.ClientControlUpdate_RemoteGuid_DoesNotChangeLocalControlState`, which pins the GUID significance recovered from `0x603EA0` / `0x5FA600`: remote-object control packets must not flip the local mover state.
  - Hardened `PacketFlowTraceFixture.SeedLocalPlayer(...)` so every parity trace clears stale pending world-entry and client-control lockout state on the shared singleton object manager before assertions run.
  - Added matching low-level `ObjectManagerWorldSessionTests` coverage so the same client-control rules are pinned outside the parity trace harness.
  - `NotifyTeleportIncoming_ClearsMovementFlagsToNone` now starts from a mixed grounded/air/swim state (`FORWARD | JUMPING | FALLINGFAR | SWIMMING`) and proves teleport entry clears every local movement bit, not just the old moving/turn mask.
  - `TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData` now proves the object manager no longer deadlocks `MSG_MOVE_TELEPORT_ACK` on a failed scene probe once pending updates are drained and `_needsGroundSnap` clears.
  - `StateMachineParityTests.MoveTeleport_AckWaitsForGroundSnap_ButNotSceneData` carries the same fix into the new parity-tagged state-machine bundle.
  - `PacketFlowParityTests.MoveTeleport_UpdatesPlayerState_ThenFlushesDeferredAck` now also pins the flag-clear side of the teleport transition by starting from airborne/swimming bits and asserting `MOVEFLAG_NONE` after dispatch.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectUpdateMutationOrderTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StateMachineParityTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PacketFlowParityTests|FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~NotifyTeleportIncoming_ClearsMovementFlagsToNone|FullyQualifiedName~TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData" --logger "console;verbosity=minimal"` -> `passed (13/13)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=PacketFlowParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=StateMachineParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
    - `Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs`
    - `Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs`
    - `Tests/WoWSharpClient.Tests/Parity/StateMachineParityTests.cs`
    - `Exports/WoWSharpClient/ClientControlUpdateArgs.cs`
    - `Exports/WoWSharpClient/Handlers/ClientControlHandler.cs`
    - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
    - `docs/physics/0x603EA0_disasm.txt`
    - `docs/physics/state_client_control.md`
    - `docs/physics/README.md`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
    - `docs/TASKS.md`
    - `Exports/WoWSharpClient/TASKS.md`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
  - Next command:
    - `rg -n "^- \\[ \\]" docs/TASKS.md -g '!**/TASKS_ARCHIVE.md'`
  - Added three deterministic `ObjectManagerWorldSessionTests` for the newly-wired packet gaps:
    - `EventEmitter_OnForceTimeSkipped_LocalPlayer_AdvancesMovementTimeBase`
    - `EventEmitter_OnCharacterJumpStart_LocalPlayer_SetsJumpingAndResetsFallTime`
    - `EventEmitter_OnCharacterFallLand_LocalPlayer_ClearsAirborneStateAndPreservesDirectionalIntent`
  - The assertions are anchored to the new packet-handling evidence:
    - `MSG_MOVE_TIME_SKIPPED` -> `0x603B40 -> 0x601560 -> 0x61AB90`
    - `MSG_MOVE_JUMP` -> `0x603BB0 -> 0x601580 -> 0x602B00 -> 0x617970 -> 0x7C6230 -> 0x7C61F0`
    - `MSG_MOVE_FALL_LAND` -> `0x603BB0 -> 0x601580 -> 0x602C20 -> 0x61A750`
  - Validation:
    - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the test build run.
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EventEmitter_OnForceTimeSkipped_LocalPlayer_AdvancesMovementTimeBase|FullyQualifiedName~EventEmitter_OnCharacterJumpStart_LocalPlayer_SetsJumpingAndResetsFallTime|FullyQualifiedName~EventEmitter_OnCharacterFallLand_LocalPlayer_ClearsAirborneStateAndPreservesDirectionalIntent" --logger "console;verbosity=minimal"` -> `passed (3/3)`.
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (26/26)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
    - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
  - Next command:
    - `rg -n "MSG_MOVE_SET_RAW_POSITION_ACK|CMSG_MOVE_FLIGHT_ACK|MOVE_SET_RAW_POSITION|FLIGHT_ACK" docs/WOW_EXE_PACKET_PARITY_PLAN.md docs/physics Exports/WoWSharpClient Tests/WoWSharpClient.Tests Services -g '!**/bin/**' -g '!**/obj/**'`
  - Added `WorldportAck_MatchesWoWExeBytes` to `Parity/AckBinaryParityTests.cs` and captured live `MSG_MOVE_WORLDPORT_ACK` fixtures via an FG cross-map teleport harness. The new fixtures (`20260417_161214_670_0001.json`, `20260417_161217_932_0002.json`) both prove the worldport ACK is just `DC000000`.
  - `AckParity` now passes for the live teleport/worldport corpus entries (`4/4` in the current corpus). The remaining P2.2 gap is fixture acquisition for the force-speed/root/flag/knockback/raw-position/flight ACK set.
  - Validation:
    - `if (Test-Path 'Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK') { Remove-Item -LiteralPath 'Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK' -Recurse -Force }; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_CAPTURE_ACK_CORPUS='1'; $env:WWOW_ACK_CORPUS_OUTPUT='E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus'; $env:WWOW_REPO_ROOT='E:/repos/Westworld of Warcraft'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AckCaptureTests.Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `$env:WWOW_REPO_ROOT='E:\repos\Westworld of Warcraft'; dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (4/4)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
    - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`
    - `Tests/WoWSharpClient.Tests/Parity/AckBinaryParityTests.cs`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK/20260417_161214_670_0001.json`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_WORLDPORT_ACK/20260417_161217_932_0002.json`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
    - `docs/TASKS.md`
  - Next command:
    - `rg -n "CMSG_FORCE_.*ACK|MSG_MOVE_SET_RAW_POSITION_ACK|CMSG_MOVE_FLIGHT_ACK" Exports/WoWSharpClient Tests Services -g '!**/bin/**' -g '!**/obj/**'`
  - Added `Parity/AckBinaryParityTests.cs` and the first `[Trait("Category", "AckParity")]` corpus-backed check. `TeleportAck_MatchesWoWExeBytes` loads raw WoW.exe fixture bytes and proves `MovementPacketHandler.BuildMoveTeleportAckPayload(...)` matches the captured `MSG_MOVE_TELEPORT_ACK` exactly.
  - Added the first live corpus entry under `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_TELEPORT_ACK/20260417_155147_750_0000.json`. The fixture was captured from foreground `NetClient::Send (0x005379A0)` during a live FG/BG parity run rather than synthesized in the test.
  - `MSG_MOVE_WORLDPORT_ACK` is still missing from the golden corpus. The current blocker is capture timing in FG startup, not the managed encoder itself, so the next P2.2 step is to move the recorder/hook earlier or trigger a flow that emits the opcode after subscription.
  - Validation:
    - `$env:WWOW_REPO_ROOT='E:\repos\Westworld of Warcraft'; dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (1/1)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
    - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/Parity/AckBinaryParityTests.cs`
    - `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_TELEPORT_ACK/20260417_155147_750_0000.json`
    - `Services/ForegroundBotRunner/Diagnostics/ForegroundAckCorpusRecorder.cs`
    - `Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`
    - `Tests/ForegroundBotRunner.Tests/ForegroundAckCorpusRecorderTests.cs`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
    - `docs/TASKS.md`
  - Next command:
    - `rg -n "MSG_MOVE_WORLDPORT_ACK|SMSG_NEW_WORLD|SMSG_TRANSFER_PENDING|ForegroundAckCorpusRecorder" Services/ForegroundBotRunner Exports/WoWSharpClient Tests -g '!**/bin/**' -g '!**/obj/**'`
  - Added deterministic parity-tagged stop/use ordering coverage for the BG interaction trigger path:
    - `ObjectManagerWorldSessionTests.ForceStopImmediate_BlocksStopPacketBeforeGameObjectUse` proves `ForceStopImmediate()` completes `MSG_MOVE_STOP` before `CMSG_GAMEOBJ_USE` can be recorded.
    - The coverage stays in `Category=MovementParity` with the existing `MovementHandler -> WoWSharpObjectManager -> MovementController` server-trigger bundle.
    - `SpellHandlerTests.HandleCastFailed_TryAgainReason_FiresNamedErrorMessage` pins `SMSG_CAST_FAILED` reason `0x7A` as `TRY_AGAIN`, matching VMaNGOS `SPELL_FAILED_TRY_AGAIN`.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests.HandleCastFailed" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Files changed:
    - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
    - `Tests/WoWSharpClient.Tests/Handlers/SpellHandlerTests.cs`
    - `Exports/WoWSharpClient/Movement/MovementController.cs`
    - `Exports/WoWSharpClient/InventoryManager.cs`
    - `Exports/WoWSharpClient/SpellcastingManager.cs`
    - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
    - `Tests/WoWSharpClient.Tests/TASKS_ARCHIVE.md`
    - `docs/TASKS.md`
  - Next command:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"`
  - Session 343 closed the centralized scene-data-service gap that was still leaking into live dungeon/raid entry:
  - Session 343 closed the centralized scene-data-service gap that was still leaking into live dungeon/raid entry:
    - `SceneTileSocketServer` now synthesizes missing tile responses from sibling `.scene` sources and returns success-empty only when the source scene has no geometry in that tile.
    - `SceneTileSocketServerTests` now cover failure-without-source, synthesize-and-cache-from-source, and empty-success-from-source on top of the earlier filename/header/cache tests.
    - `SceneDataClientIntegrationTests` now directly prove the Docker service can synthesize `409_31_33`, serve `409_30_33`, and satisfy the full Molten Core/Strath 3x3 entry neighborhoods.
    - Validation:
      - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneTileSocketServerTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
      - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map409_31_33_TileCanBeSynthesizedFromSceneSource|FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_Map409_30_33_TileReturnsSceneData|FullyQualifiedName~SceneDataClientIntegrationTests.LiveService_DungeonAndRaidEntryNeighborhoods_ReturnSceneData" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - Session 342 added direct deterministic coverage for the remaining Ratchet fishing protocol mismatch:
    - `WoWSharpObjectManagerCombatTests.CastSpell_FishingSpell_IgnoresSelectedTargetAndSendsNoTargetPayload` now proves fishing keeps the no-target cast payload instead of a destination payload.
    - The corresponding live compare passed on the split-service Docker stack after the fix, with BG now reaching the same cast/channel/loot packet milestones as FG.
    - Validation:
      - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
      - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`
  - Session 341 added combat-state regressions for the remaining mining-route blocker:
    - `WoWSharpObjectManagerCombatTests` now proves confirmed same-target melee does not resend `CMSG_ATTACKSWING`, pending-but-unconfirmed starts still retry after timeout, and stop clears the confirmation latch.
    - `SpellHandlerTests` now pin confirm-on-attack-start, confirm-on-attacker-state-update, and clear-on-attack-stop/cancel behavior.
    - `WorldClientAttackErrorTests` now verify attack-swing rejection opcodes clear both pending and confirmed melee state once handler context is wired.
    - Validation:
      - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests|FullyQualifiedName~SpellHandlerTests.HandleAttackStart_LocalPlayerConfirmsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleAttackStop_LocalPlayerClearsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleCancelCombat_LocalPlayerClearsTrackedAutoAttackState|FullyQualifiedName~SpellHandlerTests.HandleAttackerStateUpdate_OurSwingConfirmsPendingAutoAttack|FullyQualifiedName~WorldClientAttackErrorTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
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
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Dungeons.StratholmeLivingTests.STRAT_LIVE_GroupFormAndEnter" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=strath_living_entry_post_scene_service_source_fallback.trx"`

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
