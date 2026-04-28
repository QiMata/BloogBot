# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. `P2` packet-handling / ACK parity is closed; only reopen this surface when a new WoW.exe-backed gap is found.
2. Keep the `AckParity` / `PacketFlowParity` / `StateMachineParity` bundles green on future protocol changes.
3. Keep the movement opcode sweep closed by only adding new bridge/application handlers when a binary-backed non-cheat gap is found.

## MovementController Parity Backlog
Known remaining work in this owner: `0` items.

- [x] `WSC-PAR-03` Redirect parity test captures matched FG/BG traces with packet sidecars (session 188).
- [x] `WSC-PAR-04` BG `SET_FACING` on mid-route redirects: removed `!wasHorizontallyMoving` guard so BG sends `MSG_MOVE_SET_FACING` during movement, matching FG. Deterministic test `MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect` pins the fix (session 188).
- [x] `WSC-PAR-05` `MSG_MOVE_SET_FACING` packet timing now matches WoW.exe send semantics: removed the synthetic pre-facing heartbeat from `MovementController.SendFacingUpdate(...)` and replaced the idle/mid-move dampening split with the binary-backed `0.1 rad` gate from `0x60E1EA` / `0x80C408` (session 188).
- [x] `WSC-PAR-06` BG server-packet trigger parity is part of the deterministic movement bundle: force-speed/root/flag-toggle/compressed-trigger tests plus knockback `ObjectManager -> MovementController` consumption now run under `Category=MovementParity` (2026-04-15).
- [x] `WSC-PAR-07` BG stop/use/cast packet trigger parity is part of the deterministic movement bundle: `ForceStopImmediate()` synchronously records `MSG_MOVE_STOP` before game-object use/cast packets, and server `0x7A` cast failure is named `TRY_AGAIN` (2026-04-15).

## Session Handoff
### 2026-04-28 (direct movement activity support)
- Pass result: `BG jump, knockback, moving-transport object creation, and passive gameobject-transport attach support passed deterministic and live parity validation`
- Last delta:
  - Added BG `Jump()` movement dispatch and `MSG_MOVE_KNOCK_BACK` handling for
    self-GM knockback packets that should not force an ACK.
  - Moving-transport high GUID monster-move/object-update paths now create
    gameobject transport state for route entries such as `164871`.
  - `MovementController` can passively attach to nearby gameobject transports,
    with broader map-object transport deck offsets for zeppelins and tighter
    elevator ranges for normal transports.
  - Taxi splines are not classified as transports; the transport handling is
    limited to gameobject transport high GUID/type evidence.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_IdleNearGameObjectTransport_AttachesBeforePostTeleportGroundSnap|FullyQualifiedName~MovementControllerTests.Update_IdleNearMapObjectTransportDeck_AttachesWithZeppelinOriginOffset|FullyQualifiedName~ObjectManagerWorldSessionTests.DirectMonsterMove_MovingTransportHighGuid_CreatesGameObjectTransport|FullyQualifiedName~ObjectManagerWorldSessionTests.MessageMoveKnockBack_PrimesImpulseWithoutForceAck|FullyQualifiedName~ObjectUpdateMutationOrderTests.MovingTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject|FullyQualifiedName~ObjectUpdateMutationOrderTests.StaticTransportHighGuidCreateBlock_WithPacketTypeNone_CreatesGameObject" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=movement_parity_direct_actions_full_04.trx"` -> `passed (5/5; duration 2m41s)`.
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Handlers/ObjectUpdateHandler.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `git status --short --branch`

### 2026-04-28 (BG inbound payload event for transport trigger)
- Pass result: `BG packet-window recorder can classify route-specific transport object updates and the transport parity oracle passed`
- Last delta:
  - `PacketPipeline` now raises `PacketRoutedDetailed` with decoded inbound
    payloads.
  - `WorldClient` exposes the detailed inbound payload event, and `WoWClient`
    forwards it from the concrete world client.
  - This supports BG transport packet-window classification without widening
    triggers to every object update.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings; nonfatal dumpbin warning)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (10/10; existing warnings; nonfatal dumpbin warning)`.
- Files changed:
  - `Exports/WoWSharpClient/Networking/Implementation/PacketPipeline.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Client/WoWClient.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `rg -n "^- \[ \]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Tests/WoWSharpClient.Tests/TASKS.md Services/ForegroundBotRunner/TASKS.md Services/BackgroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md Exports/BotRunner/TASKS.md`

### 2026-04-28 (BG knockback jump-state parity)
- Pass result: `Knockback queue consumption now preserves binary-backed jump state and the focused deterministic parity slice passed`
- Last delta:
  - `EventEmitter_OnForceMoveKnockBack(...)` now stages server knockback as
    `MOVEFLAG_JUMPING`, clears `MOVEFLAG_FALLINGFAR`, preserves directional
    movement intent, resets fall time, and primes the jump block from the
    server-provided horizontal/vertical values.
  - `MovementController` consumes the pending knockback vector on the next
    physics tick, preserving primed jump fields for ACK parity when present
    and deriving them only as a fallback.
  - Updated object-manager, packet-flow, state-machine, and movement tests to
    pin jumping-not-falling state, preserved directional flags, and staged
    ACK-after-consume behavior.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MoveKnockBack|FullyQualifiedName~PendingKnockback|FullyQualifiedName~AckBinaryParityTests" --logger "console;verbosity=minimal"` -> `passed (46/46)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - deterministic WoWSharpClient parity tests
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `rg -n "TryConsumePendingKnockback|EventEmitter_OnForceMoveKnockBack|JumpHorizontalSpeed|CMSG_MOVE_KNOCK_BACK_ACK" Exports/WoWSharpClient Tests/WoWSharpClient.Tests -g "!**/bin/**" -g "!**/obj/**"`

### 2026-04-28 (BG post-teleport FALL_LAND parity)
- Pass result: `Stream 2E.3 closed; live BG emits MSG_MOVE_FALL_LAND for same-map airborne teleports`
- Last delta:
  - `MovementController` primes same-map airborne teleport destinations with
    `MOVEFLAG_FALLINGFAR` before the first `NativeLocalPhysics` tick when the
    post-teleport ground probe finds support well below the teleport Z.
  - Nearby-support snaps are preserved; the probe runs once per reset and does
    not add `_needsGroundSnap` to teleport ACK readiness.
  - Diagnostic first-frame logging remains scoped to the first post-reset
    ground-snap frames for future capture work.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Update_PostTeleport_AirborneDestinationPrimesFallingBeforeFirstPhysicsStep|FullyQualifiedName~Update_PostTeleport_NearbySupportBelowTeleportTarget_SnapsToNearbyGround|FullyQualifiedName~Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~Update_TeleportWithGroundSnap_RunsPhysics" --logger "console;verbosity=minimal"` -> `passed (4/4)`.
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PostTeleportPacketWindowParityTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
- Files changed:
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `rg -n "PrimeAirborneTeleportFallIfNeeded|POST_TELEPORT_AIRBORNE_GROUND_SEARCH_DISTANCE|_airborneTeleportProbeCompleted" Exports/WoWSharpClient/Movement/MovementController.cs Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`

### 2026-04-26 (BG trade accept protocol follow-up)
- Pass result: `TradeNetworkClientComponent final-accept semantics are pinned; Shodan trade validation passed foreground parity and documented the remaining BG-to-FG server completion gap`
- Last delta:
  - `AcceptTradeAsync` now sends `CMSG_BEGIN_TRADE` only while a pending trade invitation is known; once the trade window is open or this client initiated the trade, it sends final `CMSG_ACCEPT_TRADE`.
  - Added deterministic trade-network coverage for pending-invite begin-trade and initiator final-accept behavior.
  - The remaining live BG-to-FG transfer gap is not a packet-routing ACK failure: all trade actions returned `Success`, but MaNGOS left item/copper with the BG initiator.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradeNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`.
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_fg_shodan_final.trx"` -> `passed (3), skipped (1)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_shodan_anchor.trx"` -> `failed with known Ratchet anchor instability: FG loot_window_timeout / max_casts_reached`.
- Files changed:
  - `Exports/WoWSharpClient/Networking/ClientComponents/I/ITradeNetworkClientComponent.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/TradeNetworkClientComponent.cs`
  - `Tests/WoWSharpClient.Tests/Agent/TradeNetworkAgentTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `rg -n "^- \\[ \\]" docs/TASKS.md Tests/BotRunner.Tests/TASKS.md Services/WoWStateManager/TASKS.md Exports/BotRunner/TASKS.md Services/ForegroundBotRunner/TASKS.md Exports/WoWSharpClient/TASKS.md`

### 2026-04-25 (BG trade item packet mapping)
- Pass result: `BG trade cancel passes under Shodan; BG item-offer packet coordinates corrected for future transfer proof`
- Last delta:
  - `InventoryManager.SetTradeItemAsync(...)` now maps logical backpack coordinates to the vanilla packet form used by trade item offers (`bag 0` -> `0xFF`, `slot 0` -> `23`).
  - This fixed the BG packet-side item offer seen in trading probes; the committed live trading slice still skips item/gold transfer because the foreground responder ACKs `AcceptTrade` as `Failed/behavior_tree_failed`.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> `1 passed, 3 skipped`.
  - Earlier BG transfer probe after the packet-map fix reached the BG item/gold transfer path before the foreground `AcceptTrade` ACK gap was isolated.
- Files changed:
  - `Exports/WoWSharpClient/InventoryManager.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"`

### 2026-04-24 (BG wand Shoot dispatch)
- Pass result: `WoWSharpClient wand Shoot packet coverage green`
- Last delta:
  - `SpellcastingManager.StartWandAttack()` now dispatches Shoot spell id `5019` directly instead of relying on a missing spell-name lookup. This fixed the background wand path where StateManager action forwarding returned Success but the client logged `Spell 'Shoot' not found in known spells or SpellData lookup`.
  - Added `WoWSharpObjectManagerCombatTests.StartWandAttack_WithSelectedTarget_SendsShootSpellAtUnit` to pin `CMSG_CAST_SPELL` with target flags `0x0002` and the selected target GUID.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~WandAttackTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=equipment_wand_action_plan_fresh8.trx" *> "tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt"` -> `passed (2/2)`.
- Files changed:
  - `Exports/WoWSharpClient/SpellcastingManager.cs`
  - `Tests/WoWSharpClient.Tests/WoWSharpObjectManagerCombatTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

### 2026-04-21
- Pass result: `P4.1 BG SMSG-to-event parity is green`
- Last delta:
  - `WoWSharpEventEmitter`, `SpellHandler`, `WorldClient`, `WoWSharpObjectManager`, and `LootingNetworkClientComponent` now surface learned/unlearned spell, skill-update, item-added, attack/inventory/spell failure, and notification events through `IWoWEventHandler`.
  - That closes the silent BG state-change gap that the next `P4.3` loadout-step work will consume.
- Validation:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~WoWSharpEventEmitterTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpEventEmitterTests|FullyQualifiedName~LootingNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientAttackErrorTests|FullyQualifiedName~SpellHandlerTests" --logger "console;verbosity=minimal"` -> `passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WorldClientNotificationTests" --logger "console;verbosity=minimal"` -> `passed`
- Files changed:
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs`
  - `Exports/WoWSharpClient/TASKS.md`
- Next command:
  - `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: `2026-04-19`
- Pass result: `Early battleground-status handler registration is pinned for fresh WorldClient startup`
- Last delta:
  - `NetworkClientComponentFactory.InitializeEssentialAgents()` now eagerly constructs `FriendAgent`, `IgnoreAgent`, and `BattlegroundAgent` alongside the earlier CharacterInit/Party/Looting/GameObject essentials.
  - Added `AgentFactoryTests.InitializeEssentialAgents_EagerlyRegistersEarlyWorldHandlers`, which proves the factory registers `SMSG_FRIEND_LIST`, `SMSG_FRIEND_STATUS`, `SMSG_IGNORE_LIST`, `SMSG_BATTLEFIELD_STATUS`, `SMSG_BATTLEFIELD_LIST`, and `SMSG_GROUP_JOINED_BATTLEGROUND` before the first world-login packet burst can race past handler creation.
  - This early-registration fix pairs with the BackgroundBotWorker world-client rebind change and underpins the now-passing live AB queue/entry rerun.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AgentFactoryTests" --logger "console;verbosity=minimal"` -> `passed (101/101)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
    - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - Files changed:
    - `Exports/WoWSharpClient/Networking/ClientComponents/NetworkClientComponentFactory.cs`
    - `Tests/WoWSharpClient.Tests/Agent/AgentFactoryTests.cs`
    - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
    - `Exports/WoWSharpClient/TASKS.md`
  - Next command:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
  - Synced the previously-landed P2.4 work into the evidence/docs surface:
    - added `docs/physics/0x466590_disasm.txt`
    - added `docs/physics/0x466C70_disasm.txt`
    - updated `cgobject_layout.md`, `csharp_object_field_audit.md`, and `smsg_update_object_handler.md`
  - New hard findings from those captures:
    - `0x466590` walks descriptor fields in ascending descriptor-index order and forwards each present field through `0x466A00 -> 0x6142E0`
    - `0x466C70` only instantiates typed storage cases `0..7`, so there is no separate packet-instantiated `CGPet_C` branch in the update-object create path
  - `ObjectUpdateMutationOrderTests` is already covering the required P2.4 replay set and is green (`passed (4/4)`), so P2.4 and the full P2 packet-parity track are now closed.
  - Closed the remaining P2.6 audit gap by extending `StateMachineParityTests` over the documented root/unroot and knockback transitions.
  - `PacketFlowTraceFixture` now records `OnForceMoveUnroot` and dispatches `SMSG_FORCE_MOVE_UNROOT`, so the parity harness covers both sides of the root state machine instead of only the root edge.
  - Added parity-tagged coverage:
    - `StateMachineParityTests.ForceMoveRootOpcodes_StageStateUntilDeferredFlush`
    - `StateMachineParityTests.MoveKnockBack_StagesImpulseUntilConsumedThenAcks`
  - Final regression gate for `P2.7.5` is now green:
    - `AckParity` -> `passed (29/29)`
    - `MovementParity` in `WoWSharpClient.Tests` -> `passed (32/32)`
    - `PacketFlowParity` -> `passed (8/8)`
    - `StateMachineParity` -> `passed (8/8)`
    - `MovementParity` in `Navigation.Physics.Tests` -> `passed (8/8)`
    - `NavigationPathTests` -> `passed (80/80)`
  - Added `ClientControlUpdateArgs` and rewired `ClientControlHandler` / `WoWSharpEventEmitter` so `SMSG_CLIENT_CONTROL_UPDATE` now carries the packet GUID and `canControl` bit instead of being reduced to a parameterless event.
  - Added the remaining P2.6.1 state docs from existing evidence:
    - `docs/physics/state_teleport.md`
    - `docs/physics/state_worldport.md`
    - `docs/physics/state_login.md`
    - `docs/physics/state_knockback.md`
    - `docs/physics/state_root.md`
  - `docs/physics/0x603EA0_disasm.txt` and `docs/physics/state_client_control.md` now pin the WoW.exe behavior: `0x603EA0` reads packed GUID + control byte and calls `0x5FA600`, which toggles bit `0x400` in `[object + 0xC58]` and only propagates the follow-up global update for the active mover.
  - `WoWSharpObjectManager.EventEmitter_OnClientControlUpdate(...)` now ignores non-local GUIDs, stores an explicit lockout on `canControl=false`, clears that lockout only on `canControl=true`, and blocks `ReconcilePlayerControlState()` from immediately undoing the server's lockout packet.
  - Added deterministic coverage:
    - `StateMachineParityTests.ClientControlUpdate_LocalPlayer_FollowsCanControlAndBlocksReconcile`
    - `StateMachineParityTests.ClientControlUpdate_RemoteGuid_DoesNotChangeLocalControlState`
    - `ObjectManagerWorldSessionTests.ClientControlUpdate_LocalPlayer_TracksCanControlAndBlocksReconcile`
    - `ObjectManagerWorldSessionTests.ClientControlUpdate_RemoteGuid_DoesNotAffectLocalControl`
  - `TryFlushPendingTeleportAck()` no longer depends on `_sceneDataClient.EnsureSceneDataAround(...)`. The only binary-backed teleport ACK fact we currently have is the deferred `0x602FB0` gate (`0x468570`); tying ACK send to BG scene-tile availability had no VA support and created an indefinite stall when tiles were missing.
  - `NotifyTeleportIncoming(...)` already clears the full local movement state to `MOVEFLAG_NONE`; the deterministic coverage now starts from `FORWARD | JUMPING | FALLINGFAR | SWIMMING` so the old partial-mask regression cannot silently reappear.
  - Added state-machine/packet-flow regression coverage for the teleport edge:
    - `StateMachineParityTests.MoveTeleport_AckWaitsForGroundSnap_ButNotSceneData`
    - `PacketFlowParityTests.MoveTeleport_UpdatesPlayerState_ThenFlushesDeferredAck`
    - `ObjectManagerWorldSessionTests.TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData`
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
    - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
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
    - `docs/TASKS.md`
    - `Exports/WoWSharpClient/TASKS.md`
    - `Tests/WoWSharpClient.Tests/TASKS.md`
  - Next command:
    - `rg -n "^- \\[ \\]" docs/TASKS.md -g '!**/TASKS_ARCHIVE.md'`
  - `WoWSharpObjectManager` now subscribes to `OnCharacterJumpStart` and `OnCharacterFallLand`, and the movement partial applies the local-player parity fix directly from the binary-backed event paths.
  - `MSG_MOVE_TIME_SKIPPED` now advances the BG movement timestamp base instead of being silently dropped. The evidence chain is `0x603B40 -> 0x601560 -> 0x61AB90`, where `0x61AB90` adds the packet delta into the movement component's `+0xAC` accumulator.
  - `MSG_MOVE_JUMP` now forces the local player into airborne state and zeroes the local fall timer, matching `0x603BB0 -> 0x601580 -> 0x602B00 -> 0x617970 -> 0x7C6230 -> 0x7C61F0`.
  - `MSG_MOVE_FALL_LAND` now clears `MOVEFLAG_JUMPING` / `MOVEFLAG_FALLINGFAR` and resets the local fall timer when the landing event arrives, covering the in-control suppression case on top of the packet path `0x603BB0 -> 0x601580 -> 0x602C20 -> 0x61A750`.
  - Added `docs/physics/msg_move_time_skipped_jump_land.md` to document the new packet-handling VAs and the inferred meaning of the `+0xAC` time accumulator.
  - Validation:
    - `tasklist /FI "IMAGENAME eq WoW.exe" /FO LIST` -> no running `WoW.exe` before the test build pass.
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EventEmitter_OnForceTimeSkipped_LocalPlayer_AdvancesMovementTimeBase|FullyQualifiedName~EventEmitter_OnCharacterJumpStart_LocalPlayer_SetsJumpingAndResetsFallTime|FullyQualifiedName~EventEmitter_OnCharacterFallLand_LocalPlayer_ClearsAirborneStateAndPreservesDirectionalIntent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=AckParity" --logger "console;verbosity=minimal"` -> `passed (26/26)`
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (32/32)`
    - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (8/8)`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - Files changed:
    - `Exports/WoWSharpClient/Movement/MovementController.cs`
    - `Exports/WoWSharpClient/Utils/WorldTimeTracker.cs`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
    - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
    - `docs/physics/msg_move_time_skipped_jump_land.md`
    - `docs/physics/README.md`
    - `Exports/WoWSharpClient/TASKS.md`
  - Next command:
    - `rg -n "MSG_MOVE_SET_RAW_POSITION_ACK|CMSG_MOVE_FLIGHT_ACK|MOVE_SET_RAW_POSITION|FLIGHT_ACK" docs/WOW_EXE_PACKET_PARITY_PLAN.md docs/physics Exports/WoWSharpClient Tests/WoWSharpClient.Tests Services -g '!**/bin/**' -g '!**/obj/**'`
  - Session 342 closed the remaining Ratchet packet-sequence blocker:
  - Session 342 closed the remaining Ratchet packet-sequence blocker:
    - `SpellcastingManager.CastSpell(...)` no longer forces fishing through `CastSpellAtLocation(...)`; fishing now keeps the no-target `CMSG_CAST_SPELL` payload shape.
    - `WoWSharpObjectManagerCombatTests.CastSpell_FishingSpell_IgnoresSelectedTargetAndSendsNoTargetPayload` pins the corrected packet contract deterministically.
    - The latest live compare now shows BG reaching the same cast/channel/loot packet milestones as FG (`SMSG_SPELL_GO`, `MSG_CHANNEL_START`, `SMSG_GAMEOBJECT_CUSTOM_ANIM`, `CMSG_GAMEOBJ_USE`, `SMSG_LOOT_RESPONSE`).
    - Validation:
      - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`.
      - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`.
  - Session 341 closed the combat-side blocker behind the remaining mining-route timeout:
    - `SpellcastingManager` now remembers confirmed melee auto-attack per target and treats repeated same-target `StartMeleeAttack()` calls as a no-op after `SMSG_ATTACKSTART` / `ATTACKER_STATE_UPDATE` has already confirmed the swing.
    - Rejection, stop, and cancel paths now clear both pending and confirmed melee-start state so retries still occur when the server actually invalidates the engage.
    - Added deterministic regression coverage in `WoWSharpObjectManagerCombatTests`, `SpellHandlerTests`, and `WorldClientAttackErrorTests`.
    - Validation:
      - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests|FullyQualifiedName~SpellHandlerTests.HandleAttackStart_LocalPlayerConfirmsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleAttackStop_LocalPlayerClearsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleCancelCombat_LocalPlayerClearsTrackedAutoAttackState|FullyQualifiedName~SpellHandlerTests.HandleAttackerStateUpdate_OurSwingConfirmsPendingAutoAttack|FullyQualifiedName~WorldClientAttackErrorTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`.
      - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (99/99)`.
      - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_melee_confirm_fix.trx"` -> `passed (1/1)`.
  - Session 308 tightened parity ownership per BG contract:
    - Removed in-controller stale-forward forced recovery behavior (no movement-flag rewrites, no forced strafe arming, no steering-target clearing as recovery policy).
    - `ObserveStaleForwardAndRecover(...)` now emits severity callbacks only; BotRunner/upper layers keep recovery ownership.
  - Updated deterministic coverage to pin callback-only behavior:
    - Replaced stale-forward tests that previously expected in-controller waypoint/flag mutation.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerIntegrationTests|FullyQualifiedName~ObjectManagerWorldSessionTests" --logger "console;verbosity=minimal"` -> `passed (158/158)`.
    - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AtomicBotTaskTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (74/74)`.
  - Session 307 removed remaining route-shaped movement APIs to enforce BotRunner ownership:
    - Removed `SetPath(...)` from `MovementController` (single-target only via `SetTargetWaypoint(...)`).
    - Removed `SetNavigationPath(...)` from `IObjectManager` and `WoWSharpObjectManager`.
  - Updated deterministic suites for the API boundary:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~MovementControllerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (164/164)`.
    - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests" --logger "console;verbosity=minimal"` -> `passed (40/40)`.
  - Session 306 removed the remaining in-controller route-policy behavior:
    - `SetTargetWaypoint(...)` now stores exactly one steering target.
    - `SetPath(...)` is a legacy compatibility shim that stores only the path head as a steering hint.
    - Removed internal corridor/nearest/equivalence selection helpers.
    - `ObserveStaleForwardAndRecover(...)` Level 2 now escalates to caller without mutating waypoint selection.
  - Updated contract comments in `WoWSharpObjectManager.SetNavigationPath(...)` to reflect route ownership at BotRunner layer.
  - Deterministic coverage remained green after contract update:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests"` -> `passed (159/159)`.
    - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysicsTests|FullyQualifiedName~MovementControllerIpcParityTests"` -> `passed (40/40)`.
  - Session 305 removed remaining stuck-time path ownership from `MovementController`:
    - `ObserveStaleForwardAndRecover(...)` Level 2 no longer selects alternate waypoints.
    - Level 3 no longer redirects to internal escape waypoints; it now escalates to caller recovery with forced strafe.
    - Local auto-advance waypoint execution was removed from `ApplyPhysicsResult(...)`.
  - Deterministic movement coverage was updated for the ownership model and remains green:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests" --logger "console;verbosity=minimal"` -> `passed (64/64)`.
  - Session 296 hardened the scene-slice client contract rather than the controller math itself. `SceneDataClient` now defers its initial socket connect, uses a bounded connect budget on the first region request, and applies a short retry backoff after request failures so late `SceneDataService` bring-up does not permanently force BG runners off the intended thin-slice path.
  - Session 296 hardened the scene-slice client contract rather than the controller math itself. `SceneDataClient` now defers its initial socket connect, uses a bounded connect budget on the first region request, and applies a short retry backoff after request failures so late `SceneDataService` bring-up does not permanently force BG runners off the intended thin-slice path.
  - `MovementController` still pins the scene-backed local path to thin-scene-slice mode whenever a `SceneDataClient` is present, but the client can now exist before the service is listening instead of paying the old blocking startup connect.
  - Added deterministic coverage in `SceneDataClientTests` for the new failure/backoff behavior, and kept the earlier scene-slice controller assertions green (`Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode`, `Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure`).
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SceneDataClientTests|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - Session 295 closed the pure-local fallback hole behind the remaining AV hover reports. `WoWSharpObjectManager.Initialize(...)` now remembers `useLocalPhysics`, and `InitializeMovementController()` now creates `MovementController` whenever local physics is requested, even if both `_physicsClient` and `_sceneDataClient` are null.
  - Practical implication: when `SceneDataService` is unavailable and BG runners fall back to preloaded local `Navigation.dll` physics, they no longer skip controller construction and hover forever without per-frame gravity/collision updates.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.Initialize_UseLocalPhysicsWithoutSceneData_DoesNotFallbackToPathfindingClient|FullyQualifiedName~ObjectManagerWorldSessionTests.EnterWorld_UseLocalPhysicsWithoutSceneData_InitializesMovementController" --logger "console;verbosity=minimal"` -> `passed (2/2)`
    - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - Session 294 fixed the remaining managed AV hover path in `MovementController`. Post-teleport settle now gives real airborne grace when physics reports no ground below the teleport target, instead of immediately snapping back to `teleportZ` and clearing `FALLINGFAR`.
  - The same settle path now rejects support surfaces that project the player above the teleport target during the grace window. Rather than accepting that contact and finalizing the snap, the controller clears grounded continuity and keeps the bot in falling motion so the next frame can continue descending once scene data/physics stabilizes.
  - Deterministic coverage was expanded with `MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall` and `Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling`, both run from an isolated output tree because the shared `Bot\Release\net8.0` path remains busy during the AV swarm.
  - Validation:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin4 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_PostTeleport_NoGroundBelow_AllowsGraceFall|FullyQualifiedName~MovementControllerTests.Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - Session 293 added native thin-scene-slice control for the scene-backed local physics path. `MovementController` now enables `NativeLocalPhysics.SetSceneSliceMode(true)` whenever a `SceneDataClient` is present, disables it for native local controllers that do not have scene data, and leaves remote/shared-physics controllers alone so they do not require `Navigation.dll` at construction time.
  - Added `SetSceneSliceMode(...)` interop in `NativePhysicsInterop` / `NativeLocalPhysics`, plus deterministic constructor-time assertions in `MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode` and `Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode`.
  - Validation stayed on isolated output directories because the shared `Bot\Release\net8.0` tree was still contended by the active AV/background swarm:
    - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin3 --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithSceneDataClient_EnablesSceneSliceMode|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_WithoutSceneDataClient_DisablesSceneSliceMode" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - Session 291 removed the last local hover regression in the scene-backed path. `MovementController.RunPhysics(...)` no longer synthesizes a hold-position output when `EnsureLocalSceneDataFresh()` fails; it now continues into `NativeLocalPhysics.Step(...)` so BG bots still fall and settle on the currently loaded local scene cache.
  - Added an internal `SceneDataClient` test constructor plus `TestEnsureSceneDataAroundOverride`, which lets deterministic coverage force scene refresh failures without opening a real socket.
  - Added `MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure`, which proves the local native path still produces falling motion and a movement packet when the scene refresh misses.
  - Standard `Bot\Release\net8.0` builds were blocked by active AV-process file locks, so validation for this delta used isolated `-o E:\tmp\...` output directories instead of the shared repo output tree.
  - Session 290 removed the runtime `LocalPhysicsClient` layer. `MovementController` now executes local `Navigation.dll` physics directly through `NativeLocalPhysics` whenever a `SceneDataClient` is provided, while `WoWSharpObjectManager.Initialize(...)` still falls back to `PathfindingClient` for legacy/shared callers that do not opt into scene slices.
  - The new local native path now marshals `NearbyObjects` into the native `PhysicsInput`, so transports and nearby collidable game objects are available to local collision exactly where BG movement needs them. Deterministic coverage was added in `MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput`.
  - Idle physics remains active every frame, so air teleports and support loss still settle correctly even when `MOVEFLAG_NONE`.
  - Session 188 re-audited the managed movement send path against `WoW.exe` instead of keeping the earlier heuristic heartbeat/facing logic. `0x60E1EA` gates explicit facing packets on the float at `0x80C408`, which reads as `0.1f`; the surrounding send path falls straight into the movement send helper without a synthetic `MSG_MOVE_HEARTBEAT` before `MSG_MOVE_SET_FACING`.
  - `MovementController.SendFacingUpdate(...)` now sends only `MSG_MOVE_SET_FACING`, records the opcode into the per-frame diagnostics, and still updates `_lastPacketTime` / `_lastPacketPosition` from the sent frame. This removes BG behavior that was not present in the binary.
  - `WoWSharpObjectManager.MoveToward(position, facing)` no longer uses the old split thresholds (`0.02f` idle, `0.20f` mid-move). Local facing updates happen on any real delta, while the explicit `MSG_MOVE_SET_FACING` send is now gated only by the binary-backed `0.1f` threshold.
  - Deterministic coverage was updated to pin the new semantics: `MovementControllerTests` now prove `SendFacingUpdate(...)` emits only `MSG_MOVE_SET_FACING`, and `ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket` proves an in-motion facing delta below `0.1 rad` stays local-only.
  - Live proof stayed green after rerun: `Parity_Durotar_RoadPath_Redirect` still passes with the new opening packet ordering, and the forced-turn `Parity_Durotar_RoadPath_TurnStart` route still captures the shared FG/BG `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD` opening pair. The remaining divergence on that route is native physics drift (`FALLINGFAR` churn / Z bounce), not managed facing timing.
  - Session 187 closed the old live stop-tail mismatch instead of collecting more traces. `WoWSharpObjectManager.StopAllMovement()` now queues a grounded stop whenever BG is airborne, and `MovementController.RequestGroundedStop()` clears forward intent on the first grounded frame so BG sends the final `MSG_MOVE_STOP` at the real stop edge instead of carrying `FORWARD` through the target.
  - Session 187 also removed the BotRunner-side arrival orbit that was keeping FG open on the same route. `BuildGoToSequence(...)` now uses horizontal arrival checks, and `NavigationPath` uses `DistanceTo2D(...)` consistently when deciding whether an exhausted path still needs recalculation at the destination.
  - Deterministic coverage was added with `MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame`, and the forced-turn Durotar live parity route now proves both clients end on outbound `MSG_MOVE_STOP` with no late outbound `SET_FACING` after the opening pair.
  - The remaining WoWSharpClient-owned gap is no longer the stop edge. It is the pause/resume and corridor-handoff slice on the same matched FG/BG route segment, plus any controller-ordering fix that slice exposes.
  - Session 186 added a dedicated BG movement-start facing send path instead of reusing the stationary heartbeat-facing path. `MovementController.SendMovementStartFacingUpdate(...)` now emits a bare `MSG_MOVE_SET_FACING`, and `WoWSharpObjectManager.MoveToward(position, facing)` uses it only on the horizontal movement start edge.
  - Session 186 also added a stable BG packet sidecar by exposing `WoWClient.MovementOpcodeSent` and wiring `BackgroundPacketTraceRecorder` through `BackgroundBotWorker`. Live parity runs now write `packets_TESTBOT2.csv` alongside the existing FG `packets_TESTBOT1.csv`.
  - Deterministic coverage was expanded with `WoWClientTests.SendMovementOpcodeAsync_FiresMovementOpcodeSent`, `MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly`, and `ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent`.
  - `MovementParityTests` now rejects stationary passes by requiring meaningful travel from both FG and BG, and it has a new forced-turn Durotar route that sets both bots to the same wrong initial facing before `Goto`.
  - The new live forced-turn Durotar capture closes the old “facing-correction ordering” uncertainty: FG and BG both emit `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`. The remaining WoWSharpClient live gap is the route tail from that same capture, where BG emits `MSG_MOVE_STOP` while FG settles later with a different late-facing/heartbeat sequence.
  - Session 184 turned the BG corpse-run live slice into controller-ownership evidence instead of a plain pass/fail harness check. `BotRunnerService.Diagnostics` now emits stable `navtrace_<account>.json` sidecars, and `DeathCorpseRunTests` verified a live BG corpse-run captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, `PlanVersion=1`, and `LastResolution=waypoint` in `navtrace_TESTBOT2.json`.
  - That closes the "can BG expose live corridor ownership state?" part of the managed parity loop. Session 187 later closed the stop-edge portion of that live gap, leaving pause/resume timing and corridor ownership as the remaining matched-route evidence still needed.
  - Session 183 re-ran the previously stale live BG proof slice without changing controller code: `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` now passes cleanly on the current environment, so corpse-run reclaim timing is no longer the active managed blocker.
  - Session 183 also re-ran `CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver`, which still passes. That keeps the post-mining controller proof green across both corpse-run reclaim and combat-travel segments.
  - With the live proof slice green again, the remaining WoWSharpClient parity gap is no longer harness stability. After session 187, it is the missing paired FG/BG packet/trace evidence for pause/resume timing and corridor ownership on matched route segments.
  - Session 181 fixed the remaining foreground recording blocker instead of masking it with Lua fallback. `ObjectManager.StartMovement(...)` / `StopMovement(...)` now dispatch `SetControlBit(...)` through `ThreadSynchronizer.RunOnMainThread(...)`, which cleared the repeated `SetControlBitSafeFunction(...)` `NullReferenceException` seen in `injection_firstchance.log` during automated captures.
  - The automated Undercity recording path now completes the lower-route and west-elevator-up scenarios with native control-bit movement, yielding stronger packet-backed fixtures: `Urgzuga_Undercity_2026-03-25_10-00-52` (`14` frames, `98` packets) and `Urgzuga_Undercity_2026-03-25_10-01-09` (`24` frames, `125` packets). These supersede the earlier `03-01-*` pair in the canonical parity corpus.
  - `RecordingMaintenance capture` now auto-cleans duplicate `Bot/*/Recordings` output trees after each run, and the stale intermediate `Urgzuga_Undercity` capture attempts were pruned from the canonical test corpus so replay calibration stays focused on the final packet-backed fixtures.
  - Session 176 replaced the stale heartbeat assumption with packet-backed FG evidence instead of more synthetic timing guesses. `MovementController` now uses a ~500ms moving heartbeat cadence, which matches the fresh PacketLogger-backed Durotar and Undercity captures added to the canonical recording corpus.
  - `MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity` now filters to grounded forward runs with a clean stop frame, creates synthetic preroll when a capture begins mid-run, and executes the stop transition. The harness now selects `Urgzuga_Durotar_2026-03-25_03-07-08` and proves real FG/BG `START_FORWARD` / heartbeat / `STOP` parity instead of deferring for `Packets=0`.
  - Added fast replay-backed proof in `Navigation.Physics.Tests` for the new compact packet-backed recordings: flat Durotar travel, Undercity lower-route underground seating, and the west elevator up-ride from lower Undercity to the upper deck.
  - The remaining WoWSharpClient parity work is now live paired FG/BG tracing on corpse-run and combat-travel segments so pause/resume timing and corridor ownership can be compared against the client on identical routes.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -o E:\tmp\isolated-wowsharp-tests\bin --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.Update_RunsRemotePhysicsEveryIdleFrame|FullyQualifiedName~MovementControllerTests.Update_KeepsLocalPhysicsActiveWhileIdle|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_IdleFreefallStillAppliesPhysics" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release -o E:\tmp\isolated-background-botrunner\bin --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests.Update_RunsRemotePhysicsEveryIdleFrame|FullyQualifiedName~MovementControllerTests.Update_KeepsLocalPhysicsActiveWhileIdle|FullyQualifiedName~MovementControllerTests.Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput|FullyQualifiedName~MovementControllerTests.Update_IdleAirTeleportDoesNotSkipRemotePhysics|FullyQualifiedName~MovementControllerTests.Update_IdleFreefallStillAppliesPhysics" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_StandingStill_SendsSetFacingOnly|FullyQualifiedName~MovementControllerTests.SendFacingUpdate_AfterMovement_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SendsSetFacingOnRedirect|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_AlreadyMovingForward_SubThresholdFacingChange_NoSetFacingPacket|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> first rerun `failed` at stop-edge delta `609ms` from existing native drift; immediate rerun `passed (1/1)`
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GoToArrivalTests|FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (61/61)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests.RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame|FullyQualifiedName~MovementControllerTests.SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent|FullyQualifiedName~MovementControllerTests.SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`; no late outbound `SET_FACING`, both clients terminate on outbound `MSG_MOVE_STOP`, stop-edge delta `50ms`
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWClientTests.SendMovementOpcodeAsync_FiresMovementOpcodeSent|FullyQualifiedName~MovementControllerTests.SendMovementStartFacingUpdate_SendsSetFacingOnly|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveTowardWithFacing_IdleInControl_SendsSetFacingBeforeForwardIntent|FullyQualifiedName~MovementControllerRecordedFrameTests.IsRecording_CapturesPhysicsFramesWithPacketMetadata|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `dotnet build Services/BackgroundBotRunner/BackgroundBotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath" --logger "console;verbosity=normal"` -> `passed (1/1)`; confirmed stable `packets_TESTBOT2.csv` output
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)` twice; matched live start edge now shows FG/BG `MSG_MOVE_SET_FACING -> MSG_MOVE_START_FORWARD`, and the second run captured the remaining stop-tail mismatch
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RecordingArtifactHelperTests" --logger "console;verbosity=minimal"` -> `passed (2/2)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=normal"` -> `passed (1/1)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementScenarioRunnerTests|FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `13 passed`
  - `dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios 13_undercity_lower_route,14_undercity_elevator_west_up --timeout-minutes 8 --configuration Release` -> succeeded; produced `Urgzuga_Undercity_2026-03-25_10-00-52` and `Urgzuga_Undercity_2026-03-25_10-01-09`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~MovementControllerRecordedFrameTests.RecordedFrames_WithPackets_OpcodeSequenceParity" --logger "console;verbosity=minimal"` -> `45 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysicsTests.Forward_FlatTerrain_PacketTimingAndPositionDeltas|FullyQualifiedName~MovementControllerPhysicsTests.HeartbeatInterval_500ms" --logger "console;verbosity=minimal"` -> `2 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.PacketBackedFlatRun_FrameByFrame_PositionMatchesRecording|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityLowerRoute_ReplayRemainsUnderground|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=detailed"` -> `3 passed`
- Files changed:
  - `Exports/BotCommLayer/ProtobufSocketClient.cs`
  - `Exports/WoWSharpClient/Movement/SceneDataClient.cs`
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataClientTests.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/Movement/NativeLocalPhysics.cs`
  - `Exports/WoWSharpClient/Movement/NativePhysicsInterop.cs`
  - `Exports/WoWSharpClient/Movement/SceneDataClient.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/WoWSharpClient/Client/WoWClient.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Services/BackgroundBotRunner/Diagnostics/BackgroundPacketTraceRecorder.cs`
  - `Services/BackgroundBotRunner/BackgroundBotWorker.cs`
  - `Tests/BotRunner.Tests/Movement/GoToArrivalTests.cs`
  - `Tests/WoWSharpClient.Tests/Client/WoWClientTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/BotRunnerService.Diagnostics.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelper.cs`
  - `Tests/BotRunner.Tests/LiveValidation/RecordingArtifactHelperTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/MovementParityTests.cs`
  - `Exports/WoWSharpClient/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AlteracValleyTests.AV_FullyPreparedRaids_MountAndReachFirstObjective" --logger "console;verbosity=normal"`
