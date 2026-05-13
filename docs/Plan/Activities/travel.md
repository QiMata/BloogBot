# Activities — Travel (Multi-Modal)

Foundational. Every other activity depends on travel reaching the
correct entrance. See [`Reference/TRAVEL_PLANNING.md`](../../Reference/TRAVEL_PLANNING.md)
for the existing infrastructure.

## Required task families

| Task | Status | Anchor |
|---|---|---|
| `GoToTask` | done | `Exports/BotRunner/Tasks/GoToTask.cs` |
| `MountAndGoToTask` | not-started | planned — see Task specifications |
| `TakeFlightPathTask` | partial | `Exports/BotRunner/Tasks/Travel/TakeFlightPathTask.cs` |
| `BoardTransportTask` (boat, zeppelin) | partial | embedded in `TravelTask.ExecuteTransportLeg` + `Exports/BotRunner/Movement/TransportWaitingLogic.cs` |
| `ElevatorRideTask` | partial | embedded in `TravelTask.ExecuteTransportLeg` (TransitionType.Elevator) |
| `UseHearthstoneTask` | partial | `Exports/BotRunner/Tasks/Travel/UseHearthstoneTask.cs` (no Loadout integration yet) |
| `MageTeleportTask` | partial | `Exports/BotRunner/Tasks/Travel/MageTeleportTask.cs` (spell catalogue in `Exports/BotRunner/Travel/MageTeleportData.cs`) |
| `WarlockSummonTask` | partial | `Exports/BotRunner/Tasks/Travel/WarlockSummonTask.cs` (helpers + accept stubs only) |
| `EnterPortalTask` | partial | embedded in `TravelTask.ExecutePortalLeg` (TransitionType.DungeonPortal) |
| `TravelTask` (orchestrator) | done | `Exports/BotRunner/Tasks/Travel/TravelTask.cs` + `Exports/BotRunner/Movement/CrossMapRouter.cs` + `Exports/BotRunner/Movement/MapTransitionGraph.cs` |

## Missing static data

| Item | Source |
|---|---|
| Deeprun Tram endpoints | hand-author (1 row) |
| Mage teleport/portal spell IDs | `mangos.spell_template` |
| Innkeeper locations (~30) | `mangos.creature_template` filtered by `npcflags & INNKEEPER` |
| Graveyard positions | `mangos.game_graveyard` |
| Dungeon/raid portals (~25) | `mangos.areatrigger_teleport` |
| Named location resolver | manual JSON in `Bot/named-locations.json` |

## Task specifications

> Phase 0 / S0.8.1 precision blocks. One entry per task in the family
> table above. A Phase 1 worker reading any block has enough to
> implement (or finish) the task without a separate investigation pass.
>
> **Interface drift note.** `Spec/03_BOTRUNNER.md` documents
> `IBotTask` as `TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync`. The shipped interface at
> `Exports/BotRunner/Interfaces/IBotTask.cs` is now the Phase 1 target
> contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync` + `Name` + `Status`). The `BotTask` base class
> (`Exports/BotRunner/Tasks/BotTask.cs`) ships the S1.0 shim per R25:
> `TickAsync` → `OnTick` → legacy `Update()` body. Existing travel
> tasks keep their `Update()` body unchanged; per-family async refactor
> lands under S1.4 Travel. See R19/R25 in `Plan/QUESTIONS.md`.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by
> `Exports/BotRunner/SnapshotBuilder.cs` from `IObjectManager` state +
> the top of the task stack. "Reads" lists the snapshot fields the
> task is expected to consume (via the equivalent `IObjectManager`
> property today). "Writes" lists the snapshot fields whose value
> changes as a *side effect* of the task running (so tests poll the
> right field). `TravelObjective` (`communication.proto:316`) is the
> singular travel-input field.

### GoToTask

- **Class declaration:** `BotRunner.Tasks.GoToTask` at
  `Exports/BotRunner/Tasks/GoToTask.cs` (note: shipped at
  `Tasks/`, NOT `Tasks/Movement/` — the slot brief's path hint is
  out of date). Inherits `BotTask` and implements `IBotTask`.
  **Status:** done.
- **Public surface — current shipped:**
  - `public GoToTask(IBotContext botContext, float x, float y, float z, float tolerance = 3f)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `internal Position Target { get; }`
  - `internal float Tolerance { get; }`
  - `internal bool MatchesTarget(Position target, float tolerance)`
  - `internal void Retarget(Position target, float tolerance)`
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):*
    `player.Position`, `player.MapId`, `player.RunSpeed`,
    `player.TransportGuid`,
    `WoWSharpObjectManager.PhysicsHitWall`,
    `WoWSharpObjectManager.PhysicsWallNormal2D`,
    `WoWSharpObjectManager.PhysicsBlockedFraction`.
    These project into `WoWActivitySnapshot.player` and
    `WoWActivitySnapshot.movementData`.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (driven by
    `ObjectManager.MoveToward`), `WoWActivitySnapshot.movementData`
    movement-flag/heading deltas, and the task-stack top (clears on
    `PopTask("arrived")` / `PopTask("no_path_timeout")`).
- **BG protocol footprint:** no opcodes sent directly by `GoToTask`.
  Movement is delegated to
  `WoWSharpObjectManager.MoveToward(Position, float)` /
  `StopAllMovement()` which fan out to the standard BG movement opcode
  set (`MSG_MOVE_HEARTBEAT`, `MSG_MOVE_START_FORWARD`,
  `MSG_MOVE_STOP`, `MSG_MOVE_SET_FACING`, `MSG_MOVE_JUMP` when
  triggered). All opcodes are declared in
  `Exports/GameData.Core/Enums/Opcode.cs`.
- **FG memory footprint:**
  - `IObjectManager.Player` (position, run speed, map id, transport
    guid, movement flags).
  - `WoWSharpObjectManager.PhysicsHitWall` /
    `PhysicsWallNormal2D` / `PhysicsBlockedFraction` (BG path only —
    sentinel defaults on FG, see memo
    `project_pfs_overhaul_006_fg_objectmanager_sentinels.md`).
  - `IObjectManager.MoveToward(Position, float facing)`,
    `IObjectManager.StopAllMovement()`.
  - No `LuaCall` invocations. Pathfinding queries go through
    `Container.PathfindingClient` /
    `BotRunner.Movement.NavigationPathFactory.Create(...)`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.NavigationTests.Navigation_ShortPath_ArrivesAtDestination`
  at `Tests/BotRunner.Tests/LiveValidation/NavigationTests.cs:44`;
  also exercised by `Navigation_LongPath_ArrivesAtDestination` /
  `Navigation_LongPath_ZTrace_FGvsBG` in the same file.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~NavigationTests.Navigation_ShortPath_ArrivesAtDestination"`
- **Catalog `TaskFamily` claim:** `Travel`. Underlies every catalog
  row in `Plan/Activities/00_INDEX.md` because every other Travel
  task pushes a `GoToTask` (or routes through `TravelTask`'s walk
  legs which call `TryNavigateToward`, the `BotTask`-base equivalent).

### MountAndGoToTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Travel/MountAndGoToTask.cs`. **Status:**
  `not-started` (no class exists; mounting today is wrapped by
  `Exports/BotRunner/Helpers/MountUsageGuard.cs` and a separate
  `Exports/BotRunner/Tasks/Progression/MountAcquisitionTask.cs` which
  does NOT compose with `GoToTask`). The "done" row in this family
  file's earlier "MountAndGoToTask" entry was aspirational.
- **Public surface — current shipped:** none — planned anchor: `Exports/BotRunner/Tasks/Travel/MountAndGoToTask.cs`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  - `public MountAndGoToTask(IBotContext botContext, float x, float y, float z, float tolerance = 3f, uint preferredMountSpellId = 0)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under S1.4 Travel (S1.0/R25, shim-only).
  - Internal state: `enum MountState { CheckMount, CastMount, GoTo, Dismount }`.
  - Composition rule: push a `GoToTask` once `_mounted` and the
    `MountUsageGuard.TryGetBlockedReasonForMountAttempt(...)` check
    returns clear; pop self when the inner `GoToTask` pops with
    reason `arrived`.
- **Snapshot contract (planned):**
  - *Reads:* same set as `GoToTask` plus `player.IsMounted`,
    `IObjectManager.PhysicsAllowsMountByEnvironment` (the mount-guard
    gate), `Inventory` mount-item presence via
    `IObjectManager.Items`.
  - *Writes/mutates:* `WoWActivitySnapshot.player.IsMounted` toggles
    via `CMSG_CAST_SPELL` (mount-summon) on push and on pop (or via
    `MSG_MOVE_FALL_LAND` / `CMSG_CANCEL_AURA` on dismount). Movement
    deltas mirror `GoToTask`.
- **BG protocol footprint (planned):**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` to summon the mount
    (`SpellcastingManager.CastSpell` → `WoWClient.SendMSGPackedAsync`).
  - `Opcode.CMSG_CANCEL_AURA` to dismount when the task pops (or
    `Opcode.CMSG_CANCEL_MOUNT_AURA` if the realm uses the dedicated
    opcode).
  - Plus the standard movement opcodes inherited from `GoToTask`.
- **FG memory footprint (planned):**
  - `IObjectManager.Player.IsMounted`, `IObjectManager.Items`
    (mount-item iteration), `IObjectManager.IsSpellReady(spellName)`.
  - `IObjectManager.CastSpell(spellName)` /
    `IObjectManager.UseItem(item)` (FG paths via
    `BloogBot.WoW.Frames.SpellbookFrame` /
    `Tasks/UseItemTask.cs`).
  - `MountUsageGuard.TryGetBlockedReasonForMountSpell(...)` /
    `...ForMountItem(...)` to short-circuit indoor / combat /
    already-mounted states.
  - No `LuaCall` required for vanilla 1.12 mount summoning
    (cast-by-spell-name through `SpellcastingManager` covers FG + BG).
- **Test anchor:** **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/MountTravelTests.cs::MountAndGoTo_OutdoorRide_ArrivesMountedAndDismountsAtTarget`.
  Currently no class targets `MountAndGoToTask`; the closest
  exercised path is
  `Tests/BotRunner.Tests/LiveValidation/MountEnvironmentTests.cs`
  which validates `MountUsageGuard` alone. Mark tests `not-started`.
- **Catalog `TaskFamily` claim:** `Travel`. Targets every catalog row
  where `LevelRange.Min >= 40` (apprentice-riding gating) and
  `TravelTarget` is outdoor — primarily zone-questing rows in
  `quest.zone.*` plus profession-farming rows under
  `prof.mining-route` / `prof.herbalism-route` /
  `prof.skinning-route`.

### TakeFlightPathTask

- **Class declaration:** `BotRunner.Tasks.Travel.TakeFlightPathTask`
  at `Exports/BotRunner/Tasks/Travel/TakeFlightPathTask.cs`.
  **Status:** partial (FG `TaxiFrame` short-circuit landed; BG
  `FlightMasterAgent` integration is the open piece, plus end-to-end
  coverage at all 48 nodes).
- **Public surface — current shipped:**
  - `public TakeFlightPathTask(IBotContext context, uint sourceNodeId, uint destinationNodeId)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `private enum FlightState { FindFlightMaster, NavigateToFM, InteractWithFM, WaitForTaxiWindow, ActivateFlight, InFlight, Complete }`
  - `private void Pop()` (private — pops via `BotContext.BotTasks.Pop()`)
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId`,
    `IObjectManager.Units` filtered by `NpcFlags &
    UNIT_NPC_FLAG_FLIGHTMASTER (0x2000)`, `IObjectManager.TaxiFrame`
    (FG-only), `player.IsMounted`.
    Snapshot fields: `WoWActivitySnapshot.player.Position`,
    `.nearbyUnits` (the flight master), `.movementData` (for
    in-flight detection).
  - *Writes/mutates:* `WoWActivitySnapshot.movementData.MovementFlags`
    gains `MOVEFLAG_ONTRANSPORT`/`MOVEFLAG_FLYING` during the flight
    leg; `WoWActivitySnapshot.player.Position` traces the taxi path;
    the task pops when `_stationaryTicks >= 6` after `inFlight=true`.
- **BG protocol footprint:**
  - `Opcode.CMSG_GOSSIP_HELLO = 0x17B` (via
    `FlightMasterNetworkClientComponent` line 139,
    `Exports/WoWSharpClient/Networking/ClientComponents/FlightMasterNetworkClientComponent.cs`).
  - `Opcode.CMSG_TAXINODE_STATUS_QUERY = 0x6A4` (same component,
    line 165).
  - `Opcode.CMSG_TAXIQUERYAVAILABLENODES = 0x250` (line 191).
  - `Opcode.CMSG_ACTIVATETAXI = 0x1AD` (line 234).
  - `Opcode.CMSG_ACTIVATETAXIEXPRESS = 0x3D7` (line 257; multi-hop
    variant — used when the routing engine produces a multi-segment
    flight plan).
- **FG memory footprint:**
  - `IObjectManager.Units` (LINQ filter by `NpcFlags`).
  - `IWoWUnit.Interact()` (right-click — sends
    `CMSG_GOSSIP_HELLO` for the gossip flight-master).
  - `IObjectManager.TaxiFrame` (FG `TaxiFrame.SelectNodeByNumber(int)`
    short-circuit).
  - `IObjectManager.MoveToward(Position)` /
    `IObjectManager.StopMovement(ControlBits)`.
  - No direct `LuaCall`; the FG path piggy-backs on the native
    `TaxiFrame` rather than `TaxiNodeOnButtonEnter` Lua.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.TaxiTransportParityTests.Taxi_Ride_FgBgParity`
  at `Tests/BotRunner.Tests/LiveValidation/TaxiTransportParityTests.cs:54`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TaxiTransportParityTests.Taxi_Ride_FgBgParity"`
  - **Coverage gap:** the existing test exercises a single hop;
    "all 48 nodes" coverage is `not-started`. Planned anchor test:
    `TaxiTransportParityTests::Taxi_AllNodes_FgBgParity` (parameterised).
- **Catalog `TaskFamily` claim:** `Travel`. Activated when
  `CrossMapRouter` returns a leg with
  `TransitionType.FlightPath`; covers all cross-zone catalog rows
  reachable by flight (most `quest.zone.*`, all `dungeon.*` outside
  the starter zones, `raid.*`, `attune.*` lead-in legs).

### BoardTransportTask

- **Class declaration:** No standalone class. Behaviour is implemented
  inline in `TravelTask.ExecuteTransportLeg` (lines 502–605 of
  `Exports/BotRunner/Tasks/Travel/TravelTask.cs`) plus
  `BotRunner.Movement.TransportWaitingLogic` at
  `Exports/BotRunner/Movement/TransportWaitingLogic.cs`. The slot
  brief lists `BoardTransportTask` for future extraction.
  **Status:** partial — the state machine is in place; promotion to a
  dedicated `IBotTask` is open work.
- **Public surface — current shipped:** inline implementation via `TravelTask.ExecuteTransportLeg(IWoWLocalPlayer player, RouteLeg leg, float elapsedSec, DateTime now)` plus `TransportWaitingLogic(TransportData.Transport transport, TransportData.TransportStop boardStop, TransportData.TransportStop exitStop)` exposing `Position? Update(Position playerPos, ulong playerTransportGuid, IReadOnlyList<DynamicObjectProto> nearbyObjects, float elapsedSec, uint mapId, bool onTransport)`, `TransportPhase CurrentPhase { get; }`, `bool MissedBoardingAttempt { get; }`, `static bool IsNativeOffMeshBoardingEnabled()`. No standalone `BoardTransportTask` class exists (planned anchor: `Exports/BotRunner/Tasks/Travel/BoardTransportTask.cs`).
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):** planned `BoardTransportTask` extraction: constructor `(IBotContext context, TransportData.Transport transport, TransportData.TransportStop boardStop, TransportData.TransportStop exitStop)`; inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`). Per-family async refactor lands under S1.4 Travel (S1.0/R25, shim-only).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` / `.MapId` /
    `.TransportGuid` / `.MovementFlags` (for
    `MOVEFLAG_ONTRANSPORT`),
    `IObjectManager.GameObjects` (the boat/zeppelin GO via
    `TransportObjectIdentity.MatchesTransport`),
    `Container.PathfindingClient` (for boarding-approach waypoints).
  - *Writes/mutates:* `WoWActivitySnapshot.player.TransportGuid` (set
    when the bot boards, cleared on exit),
    `WoWActivitySnapshot.movementData.MovementFlags` (gains/loses
    `MOVEFLAG_ONTRANSPORT`),
    `WoWActivitySnapshot.player.MapId` (changes on cross-continent
    transports e.g. Boat_RatchetToBootyBay),
    `WoWActivitySnapshot.player.Position` (jumps with the transport).
- **BG protocol footprint:**
  - No transport-specific CMSG opcode for boarding (a vanilla boat/
    zeppelin auto-boards on `AURA_AREATRIGGER` server-side once the
    player position overlaps the GO's parented transport zone).
  - The movement opcodes that fire while the bot is *on* the
    transport carry the transport offset:
    `MSG_MOVE_HEARTBEAT`, `MSG_MOVE_START_FORWARD`,
    `MSG_MOVE_STOP` (all in `Exports/GameData.Core/Enums/Opcode.cs`)
    with the `MOVEFLAG_ONTRANSPORT` bit set in
    `MovementInfo.MovementFlags`.
  - Cross-continent transport additionally fires
    `SMSG_NEW_WORLD` server→client (handled, not sent).
- **FG memory footprint:**
  - `IObjectManager.GameObjects` filtered by display id /
    `TransportObjectIdentity` (e.g. OG zeppelin display 3015 maps to
    Frezza's zeppelin per memo
    `reference_og_zeppelin_layout.md`).
  - `IObjectManager.Player.TransportGuid` (parented GUID — non-zero
    while on transport).
  - `PathfindingOverlayBuilder.BuildNearbyObjects` to pass the
    transport set into the waiting logic.
  - `IObjectManager.MoveToward` / `StopAllMovement` for the
    boarding/dismounting walk legs.
  - No `LuaCall`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.TransportTests.Zeppelin_OrgToUndercity`
  at `Tests/BotRunner.Tests/LiveValidation/TransportTests.cs:49`,
  plus `Boat_RatchetToBootyBay` (line 103) and
  `Boat_MenethilToTheramore` (line 127) in the same file. Cross-FG/BG
  parity exercised by
  `TaxiTransportParityTests.Transport_Board_FgBgParity` (line 120)
  and `Transport_CrossContinent_FgBgParity` (line 204).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TransportTests.Zeppelin_OrgToUndercity"`
- **Catalog `TaskFamily` claim:** `Travel`. Used by every catalog row
  whose `TravelTarget` requires a cross-continent transport hop —
  most prominently `dungeon.ragefire-chasm` (Org-only), `boss.kazzak`
  (Stormwind→Blasted Lands chain), and any `quest.zone.*` row that
  hands off to Org/Undercity zeppelins, the Theramore/Menethil boat,
  or the Booty Bay/Ratchet boat.

### ElevatorRideTask

- **Class declaration:** No standalone class. Implemented inline in
  `TravelTask.ExecuteTransportLeg` when `leg.Type ==
  TransitionType.Elevator` (`Exports/BotRunner/Tasks/Travel/TravelTask.cs`
  lines 206–209 dispatch, 502–605 body), with elevator-specific
  branches inside `TransportWaitingLogic` and the elevator-detect
  helper `TransportData.DetectElevatorCrossing(uint mapId, Position
  from, Position to)` (used by `GoToTask.HasArrived`,
  `Exports/BotRunner/Tasks/GoToTask.cs:266`).
  **Status:** partial — UC + IF Deeprun Tram + Thunder Bluff exercised
  but flaky on some races per the family table.
  **Planned anchor (extraction):**
  `Exports/BotRunner/Tasks/Travel/ElevatorRideTask.cs`.
- **Public surface — current shipped:** inline behaviour in `TravelTask.ExecuteTransportLeg` when `leg.Type == TransitionType.Elevator` plus `TransportData.DetectElevatorCrossing(uint mapId, Position from, Position to)`. No standalone `ElevatorRideTask` class today (planned anchor: `Exports/BotRunner/Tasks/Travel/ElevatorRideTask.cs`).
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):**
  - `public ElevatorRideTask(IBotContext context,
    TransportData.Transport elevator,
    TransportData.TransportStop boardStop,
    TransportData.TransportStop exitStop)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under S1.4 Travel (S1.0/R25, shim-only).
  - Reuses existing `TransportWaitingLogic` constructor and phase
    state machine (`TransportPhase.Waiting → Boarding → Riding →
    Complete`).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` (Z-delta drives the
    "elevator at top/bottom" detection),
    `IObjectManager.Player.TransportGuid` (becomes non-zero when the
    elevator parents the player), `IObjectManager.GameObjects` (the
    elevator GO and its parented mover).
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.Position.Z` (changes by tens of
    yards),
    `WoWActivitySnapshot.player.TransportGuid` (on/off through the
    ride),
    `WoWActivitySnapshot.movementData.MovementFlags`
    (`MOVEFLAG_ONTRANSPORT`).
- **BG protocol footprint:** identical to `BoardTransportTask` —
  movement opcodes with `MOVEFLAG_ONTRANSPORT` set; no
  elevator-specific CMSG opcode. The arrival/exit gate is the
  `WalkLegArrivalRadius` check inside
  `TravelTask.HasReachedTransportExit` plus the
  `WalkLegTransportVerticalArrivalTolerance` /
  `WalkLegNativeOffMeshTransportVerticalArrivalTolerance` gates.
- **FG memory footprint:**
  - `IObjectManager.GameObjects` filtered by elevator GO entry
    (`TransportData.ElevatorEntries` — Undercity, IF tram,
    Thunder Bluff, Darnassus, Stormwind).
  - `IObjectManager.Player.TransportGuid`,
    `IObjectManager.Player.MovementFlags`.
  - `IObjectManager.MoveToward(Position)` /
    `StopAllMovement()` for board-zone arrival.
  - No `LuaCall`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.TransportTests.Elevator_Undercity`
  at `Tests/BotRunner.Tests/LiveValidation/TransportTests.cs:145`.
  Sibling coverage: `Elevator_FullRide_Undercity` (line 186),
  `Elevator_ThunderBluff` (line 280),
  `DeeprunTram_IFToSW` (line 304).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TransportTests.Elevator_Undercity"`
- **Catalog `TaskFamily` claim:** `Travel`. Required for any catalog
  row whose `TravelTarget` involves the Undercity throne lift, the
  IF/SW Deeprun Tram, or the Thunder Bluff elevator (city-loop economy
  rows like `econ.vendor-loop` and Horde reputation rows that staging
  through UC e.g. `attune.ony-horde`).

### UseHearthstoneTask

- **Class declaration:**
  `BotRunner.Tasks.Travel.UseHearthstoneTask` at
  `Exports/BotRunner/Tasks/Travel/UseHearthstoneTask.cs`.
  **Status:** partial — the task casts `Hearthstone` and pops on
  teleport; the *route planner* still has no integration that picks
  the hearthstone leg when the bind matches (`ST.1` in the slot list).
- **Public surface — current shipped:**
  - `public UseHearthstoneTask(IBotContext context)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `private enum HearthState { FindItem, StopAndCast, WaitForCast, DetectTeleport, Complete }`
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.IsInCombat` (cancel gate),
    `IObjectManager.Player.Position` / `.MapId` (start position +
    teleport detection),
    `IObjectManager.Player.IsChanneling` / `.IsCasting` (interrupt
    detection), `IObjectManager.Items` (hearthstone presence;
    `HearthstoneItemId = 6948`).
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.Position` (jump >100 yards),
    `WoWActivitySnapshot.player.MapId` (cross-continent bind),
    `WoWActivitySnapshot.player.IsCasting` (rises during the 10s
    cast, clears on completion or interrupt).
- **BG protocol footprint:**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` — the task uses
    `IObjectManager.CastSpell("Hearthstone")`, which routes through
    `SpellcastingManager.CastSpell` (`Exports/WoWSharpClient/SpellcastingManager.cs`).
  - Alternative (per the class-level summary) is
    `Opcode.CMSG_USE_ITEM = 0x0AB` via `ItemUseNetworkClientComponent`
    using the hearthstone's bag/slot. The shipped path is the spell
    cast, not the item use; deciding between the two is a Phase 1
    contract slot.
- **FG memory footprint:**
  - `IObjectManager.Player.IsInCombat`, `.IsChanneling`, `.IsCasting`.
  - `IObjectManager.Items` (LINQ scan for `ItemId == 6948`).
  - `IObjectManager.StopMovement(ControlBits)`.
  - `IObjectManager.CastSpell(string spellName)`.
  - No `LuaCall`.
- **Test anchor:** **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/HearthstoneTests.cs::Hearthstone_BoundToInn_TeleportsToBindMap`.
  No existing test exercises `UseHearthstoneTask` directly; the
  closest related coverage is
  `BgPostTeleportStabilizationTests::BgBot_TeleportAboveGround_FallingFlagsClearAndPositionStabilizesWithinBound`
  (`Tests/BotRunner.Tests/LiveValidation/BgPostTeleportStabilizationTests.cs:78`)
  which validates post-teleport physics but does not invoke
  `UseHearthstoneTask`. Mark `tests` row `not-started`.
- **Catalog `TaskFamily` claim:** `Travel`. Path candidate for any
  catalog row whose `TravelTarget` matches the character's current
  hearthstone bind (most commonly the starter-quest rows
  `quest.starter.*` once a bot binds in a capital, and the
  `econ.vendor-loop` row).

### MageTeleportTask

- **Class declaration:**
  `BotRunner.Tasks.Travel.MageTeleportTask` at
  `Exports/BotRunner/Tasks/Travel/MageTeleportTask.cs`.
  Static data: `BotRunner.Travel.MageTeleportData` at
  `Exports/BotRunner/Travel/MageTeleportData.cs`.
  **Status:** partial — task implementation lands; route planner
  integration (`ST.2`) is open work.
- **Public surface — current shipped:**
  - `public MageTeleportTask(IBotContext context, uint spellId, string spellName)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `public static MageTeleportTask? ForDestination(IBotContext context, string destinationName)` — returns null if `MageTeleportData` has no row for the destination.
  - `private enum TeleState { Check, StopAndCast, WaitForCast, DetectTeleport, Complete }`
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Class` (must equal `Class.Mage`),
    `IObjectManager.IsSpellReady(spellName)`,
    `IObjectManager.Player.IsInCombat` (cancel gate),
    `.IsCasting` / `.IsChanneling` (interrupt detection),
    `.Position` / `.MapId`.
  - *Writes/mutates:* same as `UseHearthstoneTask` — position jump,
    map change, IsCasting rise/fall.
- **BG protocol footprint:**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` via
    `SpellcastingManager.CastSpell(spellName)` →
    `WoWClient.SendMSGPackedAsync` (Spellcasting line 229/256/279).
  - No conjure (`Teleport: <city>` is a memorised spell, no reagent
    in 1.12.1 vanilla `mangos.spell_template`).
- **FG memory footprint:**
  - `IObjectManager.Player.Class`,
    `IObjectManager.IsSpellReady(string)`,
    `.IsInCombat`, `.IsCasting`, `.IsChanneling`, `.Position`,
    `.MapId`.
  - `IObjectManager.StopMovement(ControlBits)` and
    `IObjectManager.CastSpell(string)`.
  - No `LuaCall`. `MageTeleportData.GetAllSpells()` is consulted at
    factory time (`ForDestination`).
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.MageTeleportTests.MageTeleport_Horde_OrgrimmarArrival`
  at `Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs:84`.
  Sibling coverage: `MageTeleport_Alliance_StormwindArrival` (line 117),
  `MagePortal_PartyTeleported` (line 178),
  `MageAllCityTeleports` (line 207).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MageTeleportTests.MageTeleport_Horde_OrgrimmarArrival"`
- **Catalog `TaskFamily` claim:** `Travel`. Used by mage-class bots
  for any catalog row whose `TravelTarget.NamedLocation` matches a
  capital-city teleport entry in `MageTeleportData` (Stormwind,
  Ironforge, Darnassus, Orgrimmar, Undercity, Thunder Bluff) plus
  the portal variant used to ferry teammates.

### WarlockSummonTask

- **Class declaration:**
  `BotRunner.Tasks.Travel.WarlockSummonTask` at
  `Exports/BotRunner/Tasks/Travel/WarlockSummonTask.cs`.
  Sibling helper: `MeetingStoneSummonTask` (next entry) for the
  meeting-stone variant.
  **Status:** partial — the warlock-side cast lands, but the helper
  click + remote-accept loop (`WaitForHelpers`, `WaitForAccept`) is
  still stubbed with time-based advances (see `// TODO` lines 86–87
  and 102 of the source).
- **Public surface — current shipped:**
  - `public WarlockSummonTask(IBotContext context, ulong targetPlayerGuid)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `private enum SummonState { CheckPrereqs, CastRitual, WaitForHelpers, WaitForAccept, Complete }`
  - Constants: `RitualOfSummoningSpellId = 698`,
    `SoulShardItemId = 6265`,
    `RitualTimeoutMs = 30_000`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Class` (must equal
    `Class.Warlock`), `IObjectManager.Items` (soul-shard presence).
  - *Writes/mutates:* The summon target's
    `WoWActivitySnapshot.player.Position` (delivered party member);
    on the summoner, `IsCasting` rises during the ritual cast and
    clears on completion.
- **BG protocol footprint:**
  - `Opcode.CMSG_CAST_SPELL = 0x12E` (the warlock casts
    `Ritual of Summoning` via
    `IObjectManager.CastSpell("Ritual of Summoning")`).
  - `Opcode.CMSG_SUMMON_RESPONSE = 0x2AC` (the *summoned target*
    responds with accept/decline — handled on the target's bot,
    not on the summoner; tracked here for the end-to-end test
    contract).
- **FG memory footprint:**
  - `IObjectManager.Player.Class`,
    `IObjectManager.Items` (LINQ scan for
    `ItemId == 6265`).
  - `IObjectManager.StopMovement(ControlBits)`,
    `IObjectManager.CastSpell("Ritual of Summoning")`.
  - **Open:** helper-click and summoned-target accept paths need to
    consume `SMSG_SUMMON_REQUEST` (server→target) and emit
    `CMSG_SUMMON_RESPONSE` on the target's bot — these are TODO
    items.
  - No `LuaCall`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.SummoningTests.WarlockSummon_RitualOfSummoning`
  at `Tests/BotRunner.Tests/LiveValidation/SummoningTests.cs:35`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~SummoningTests.WarlockSummon_RitualOfSummoning"`
- **Catalog `TaskFamily` claim:** `Travel`. Used when a raid/dungeon
  catalog row (`dungeon.*`, `raid.*`) has a warlock candidate in the
  forming group and one or more remaining members are slow to reach
  the meeting stone. Required for `attune.mc` / `attune.bwl` etc.
  on the inbound leg when MC / BWL summoning-stone arrival lags.

### EnterPortalTask

- **Class declaration:** No standalone class. Implemented inline in
  `TravelTask.ExecutePortalLeg(IWoWLocalPlayer player, RouteLeg leg)`
  at `Exports/BotRunner/Tasks/Travel/TravelTask.cs:607-618`.
  **Status:** partial — dungeon entrances exercised via `TravelTask`;
  raid portals (Onyxia, BWL, MC, AQ, Naxx) lack acceptance coverage
  and rely on attunement state being right.
  **Planned anchor (extraction):**
  `Exports/BotRunner/Tasks/Travel/EnterPortalTask.cs`.
- **Public surface — current shipped:** inline implementation via `private void TravelTask.ExecutePortalLeg(IWoWLocalPlayer player, RouteLeg leg)`. No standalone `EnterPortalTask` class today (planned anchor: `Exports/BotRunner/Tasks/Travel/EnterPortalTask.cs`).
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Public surface (planned task-specific shape):** planned `EnterPortalTask` extraction: `public EnterPortalTask(IBotContext context, RouteLeg portalLeg)`; inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`). Per-family async refactor lands under S1.4 Travel (S1.0/R25, shim-only).
- **Snapshot contract:**
  - *Reads:* `IObjectManager.Player.Position` (must be within 5y of
    `leg.Start` to step into the portal),
    `IObjectManager.Player.MapId` (changes after the loading
    screen completes).
  - *Writes/mutates:*
    `WoWActivitySnapshot.player.MapId` (instance-id change),
    `WoWActivitySnapshot.player.Position` (new instance spawn
    point), `WoWActivitySnapshot.isMapTransition` (true during the
    loading screen).
- **BG protocol footprint:**
  - No CMSG opcode is sent — the portal is an `areatrigger_teleport`
    row (`mangos.areatrigger_teleport`); the server fires
    `SMSG_NEW_WORLD` when the player crosses the trigger volume. The
    inbound movement is the standard `MSG_MOVE_HEARTBEAT` /
    `MSG_MOVE_START_FORWARD` cascade.
  - Cross-instance handshake afterward:
    `Opcode.CMSG_MOVE_WORLDPORT_ACK` (client acknowledges the new
    world; handled in
    `Exports/WoWSharpClient/Handlers/` movement handlers).
- **FG memory footprint:**
  - `IObjectManager.Player.Position` / `.MapId`.
  - `IObjectManager.MoveToward(Position)` (the inline call inside
    `ExecutePortalLeg` uses `TryNavigateToward(..., allowDirectFallback: true)`).
  - No `LuaCall`.
- **Test anchor:** Current coverage piggy-backs on
  `BotRunner.Tests.LiveValidation.MapTransitionTests.MapTransition_DeeprunTramBounce_ClientSurvives`
  (`Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs:37`)
  for the post-teleport stabilisation contract, and
  `RagefireChasmTests` for the dungeon-portal end-to-end loop.
  **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/EnterPortalTests.cs::EnterPortal_AllDungeonEntrances_ArrivesInside`
  and `...::EnterPortal_RaidEntrances_RespectsAttunement` for the
  raid-portal coverage gap.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MapTransitionTests.MapTransition_DeeprunTramBounce_ClientSurvives"`
- **Catalog `TaskFamily` claim:** `Travel`. Every `dungeon.*` and
  `raid.*` catalog row whose `TravelTarget` is the *outside* portal
  coordinates (Stockades, Wailing Caverns, Deadmines, BRD, BRS, MC,
  BWL, Naxxramas, etc. — full list in
  `mangos.areatrigger_teleport`).

### TravelTask

- **Class declaration:** `BotRunner.Tasks.Travel.TravelTask` at
  `Exports/BotRunner/Tasks/Travel/TravelTask.cs`. Supporting
  pieces: `BotRunner.Movement.CrossMapRouter` at
  `Exports/BotRunner/Movement/CrossMapRouter.cs`,
  `BotRunner.Movement.MapTransitionGraph` at
  `Exports/BotRunner/Movement/MapTransitionGraph.cs`,
  `BotRunner.Movement.TransportWaitingLogic`,
  `BotRunner.Travel.LocationResolver` at
  `Exports/BotRunner/Travel/LocationResolver.cs`.
  **Status:** done (orchestrator scaffolding); per-leg-type
  correctness depends on the sub-tasks above.
- **Public surface — current shipped:**
  - `public TravelTask(IBotContext context, uint targetMapId,
    Position targetPos, TravelOptions? options = null,
    float arrivalRadius = DefaultArrivalRadius,
    Func<DateTime>? utcNowProvider = null)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `internal uint TargetMapId { get; }`
  - `internal Position TargetPosition { get; }`
  - `internal float ArrivalRadius { get; }`
  - `internal bool MatchesTarget(uint targetMapId, Position targetPosition, float arrivalRadius)`
  - `internal void Retarget(Position targetPosition, float arrivalRadius)`
  - Companion record: `BotRunner.Tasks.Travel.TravelOptions` at
    `Exports/BotRunner/Tasks/Travel/TravelOptions.cs`
    (`AllowHearthstone`, `AllowClassTeleport`, `AllowFlightPath`,
    `PlayerFaction`, `DiscoveredFlightNodes`,
    `HearthstoneBindMapId`, `HearthstoneBindPosition`,
    `HearthstoneCooldownRemainingSec`).
- **Public surface — target (Phase 1, after S1.0):** Four-method async contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10) (TickAsync / OnPushedAsync / OnPoppedAsync / OnChildFailedAsync + Name + Status + BotTaskContext).
- **Snapshot contract:**
  - *Reads:* `WoWActivitySnapshot.travel_objective`
    (`communication.proto:316` — `target_map_id`,
    `target_position`, `target_location_name`, `allow_hearthstone`,
    `allow_class_teleport`),
    `IObjectManager.Player.Position` / `.MapId` /
    `.TransportGuid` / `.MovementFlags` / `.RunSpeed`,
    `IObjectManager.Units` (flight-master discovery),
    `IObjectManager.IsInFlight`,
    `IObjectManager.GameObjects` (transport detection via
    `PathfindingOverlayBuilder.BuildNearbyObjects`).
  - *Writes/mutates:* `WoWActivitySnapshot.player.Position` /
    `.MapId` end up at `_targetPosition` on
    `PopTask("travel_complete")`; the task emits structured
    diagnostic messages via `BotContext.AddDiagnosticMessage`
    (`[TRAVEL_PLAN] legs=...`, `[TRAVEL_LEG] start ...`,
    `[TRAVEL_WALK_STALL]`, `[TRAVEL_TRANSPORT_MISSED_BOARDING]`,
    `[TRAVEL_FLIGHT_RETRY]`, `[TRAVEL_FAILED]`) which surface as
    `WoWActivitySnapshot.recentErrors` /
    `WoWActivitySnapshot.recentChatMessages` entries when
    `SnapshotBuilder` mirrors them.
- **BG protocol footprint:** the orchestrator itself sends no
  opcodes; it composes the four leg types whose opcode sets are:
  - `TransitionType.Walk` → standard movement opcode set inherited
    from `GoToTask` (`MSG_MOVE_HEARTBEAT`, etc.).
  - `TransitionType.FlightPath` → opcodes listed under
    `TakeFlightPathTask`.
  - `TransitionType.Boat | Zeppelin | Elevator` → opcodes listed
    under `BoardTransportTask` / `ElevatorRideTask`.
  - `TransitionType.DungeonPortal` → opcodes listed under
    `EnterPortalTask`.
- **FG memory footprint:**
  - `IObjectManager.Player`,
    `IObjectManager.Units`,
    `IObjectManager.GameObjects`,
    `IObjectManager.IsInFlight`,
    `IObjectManager.MoveToward` / `StopAllMovement`,
    `IObjectManager.ActivateFlightAsync(ulong flightMasterGuid,
    uint destinationNodeId, CancellationToken ct)`,
    `IObjectManager.MovementStuckRecoveryGeneration`,
    `IObjectManager.PhysicsWallNormal2D` /
    `PhysicsHitWall` / `PhysicsBlockedFraction` /
    `PhysicsFrozenDebugInfo`.
  - No `LuaCall`. Diagnostics emit through `BotRunnerService.DiagLog`
    + `BotContext.AddDiagnosticMessage`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.TravelPlannerTests.TravelTo_Crossroads_BotStartsMoving`
  at `Tests/BotRunner.Tests/LiveValidation/TravelPlannerTests.cs:38`.
  Sibling coverage:
  `TravelTo_Crossroads_PositionApproachesDestination` (line 49),
  `TravelTo_ShortWalk_WithinOrgrimmar` (line 60),
  `TravelTo_CrossZone_MapStaysKalimdor` (line 101).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TravelPlannerTests.TravelTo_Crossroads_BotStartsMoving"`
- **Catalog `TaskFamily` claim:** `Travel`. The default
  `TaskFamily = "Travel"` value for *every* catalog row in
  `Plan/Activities/00_INDEX.md`; `TravelTask` is the entry-point
  task pushed by `ActivityResolver` whenever `ActivityDefinition.TravelTarget`
  has a non-zero `MapId`.

## Slots

### ST.1 — Hearthstone integration

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Movement/UseHearthstoneTask.cs`
  - `Exports/BotRunner/Movement/CrossMapRouter.cs` (additions)
- **Goal:** `CharacterBuildConfig` carries hearthstone bind. Route
  planner uses hearthstone when it shaves > 5 min off ETA and the
  bind matches the leg target.

### ST.2 — Mage teleport / portal

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Movement/MageTeleportTask.cs`
  - `Exports/BotRunner/Movement/MagePortalCatalog.cs`
- **Goal:** Mage bots offer portal at city portals; bots requesting
  travel can request a portal (1g tip transaction).

### ST.3 — Warlock summon

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Movement/WarlockSummonTask.cs`
- **Goal:** Summon chain works across the activity scheduler so a
  party member arriving at a raid stone summons remaining members.

### ST.4 — Deeprun Tram

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Movement/TransportData.cs` (additions)
  - `Exports/BotRunner/Movement/MapTransitionGraph.cs` (additions)

### ST.5 — Dungeon/raid portal data

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Movement/PortalCatalog.cs`

### ST.6 — Named location resolver

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Bot/named-locations.json`
  - `Exports/BotRunner/Movement/NamedLocationResolver.cs`
- **Goal:** Capital cities, quest hubs, innkeepers, graveyards
  resolve from name → `(map, position)` for ActivityDefinition
  `TravelTarget` fields.

### ST.7 — Cross-faction transport map gaps

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Ensure every faction can reach every catalog activity
  zone via a known travel plan. If unreachable, the activity must
  flag `RequireFactionMatch` and the scheduler must filter.

### ST.8 — Travel LiveValidation suite

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** ST.1..ST.7
- **Goal:** A LiveValidation test per travel mode, end-to-end with
  StateManager assertions on transport state, mode entry, mode exit.

## Failure recovery

- **Missed transport** → `TransportWaitingLogic` waits for next
  schedule; emits `wwow.botrunner.transport.missed_total{transport=...}`
  metric; replans after 2 missed schedules in a row.
- **Stuck mid-leg** → `StuckRecoveryTask` (see
  [`recovery.md`](recovery.md)) replan from current position.
- **Crashed/disconnected** → reconnect, restore lease state from
  StateManager, resume travel from snapshot.
