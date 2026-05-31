# Activities — Recovery

Foundational. Every other activity assumes recovery flows work. This
covers death, corpse runs, stuck recovery, disconnect/reconnect, and
lease return.

## Task families

| Task | Status |
|---|---|
| `ReleaseCorpseTask` | done |
| `RetrieveCorpseTask` | done |
| `SpiritHealerTask` | not-started — resurrect via spirit healer when corpse-run impossible |
| `StuckRecoveryTask` | partial — needs `IsOnNavmesh` gating per Plan/09 |
| `ReconnectTask` | partial |
| `LeaseReturnTask` | not-started — execute return objective on lease release |
| `LowDurabilityTask` | not-started — route to repair vendor |
| `LowInventoryTask` | not-started — empty inventory at vendor/mail/bank when full |
| `LowGoldTask` | not-started — switch to gold farm when below threshold |

## Task specifications

> Phase 0 / S0.8.11 precision blocks for the five `Spec/03_BOTRUNNER.md#catalog-of-task-families`
> Recovery entries (`ReleaseCorpseTask`, `RetrieveCorpseTask`,
> `StuckRecoveryTask`, `ReconnectTask`, `SpiritHealerTask`). A Phase 1
> worker reading any block has enough to implement (or finish) the task
> without a separate investigation pass.
>
> **Interface drift note.** `Spec/03_BOTRUNNER.md` documents `IBotTask`
> as `TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync`. The shipped interface at
> `Exports/BotRunner/Interfaces/IBotTask.cs` is now the Phase 1 target
> contract (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
> `OnChildFailedAsync` + `Name` + `Status`). The `BotTask` base class
> (`Exports/BotRunner/Tasks/BotTask.cs`) ships the S1.0 shim per R25:
> `TickAsync` → `OnTick` → legacy `Update()` body. Existing recovery
> tasks keep their `Update()` body unchanged; per-family async refactor
> lands under S1.13 Recovery. See R19/R25 in `Plan/QUESTIONS.md`.
>
> **Snapshot-contract scope.** Tasks do not directly mutate
> `WoWActivitySnapshot`; that proto is built by
> `Exports/BotRunner/SnapshotBuilder.cs` from `IObjectManager` state
> plus the top of the task stack. "Reads" lists snapshot fields the
> task consumes (via the equivalent `IObjectManager` property today).
> "Writes" lists fields whose value changes as a *side effect* of the
> task running (so tests poll the right field).

### ReleaseCorpseTask

- **Class declaration:** `BotRunner.Tasks.ReleaseCorpseTask` at
  `Exports/BotRunner/Tasks/ReleaseCorpseTask.cs` (note: shipped at
  `Tasks/`, NOT `Tasks/Recovery/` — the slot brief's path hint
  predates the actual file). Inherits `BotTask` and implements
  `IBotTask`. **Status:** done.
- **Public surface — current shipped:**
  - `public ReleaseCorpseTask(IBotContext botContext)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).; body calls
    `ObjectManager.ReleaseSpirit()` then `PopTask("ReleaseSent")` —
    fire-and-forget, one tick.
- **Public surface — target (Phase 1, after S1.0):** Four-method async
  contract per
  [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)
  (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`).
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* none — the task is unconditional;
    the caller is responsible for verifying corpse state via
    `DeathStateDetection.IsCorpse(player)` (which reads
    `player.PlayerFlags` `PLAYER_FLAGS_GHOST=0x10`, `player.Bytes1[0]`
    `UNIT_STAND_STATE_DEAD=7`, and `player.Health`) before pushing.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.PlayerFlags` (the server sets the
    `PLAYER_FLAGS_GHOST` bit after processing `CMSG_REPOP_REQUEST`);
    `WoWActivitySnapshot.player.Position` is teleported to the nearest
    graveyard by the server; `WoWActivitySnapshot.player.Health`
    transitions 0 → 1 (ghost form HP). Task-stack top clears on the
    same tick via `PopTask("ReleaseSent")`.
- **BG protocol footprint:**
  `Opcode.CMSG_REPOP_REQUEST` with an empty payload, sent by
  `WoWSharpObjectManager.ReleaseSpirit()` at
  `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs:1185`
  (which delegates to `IDeadActorNetworkClientComponent.ReleaseSpiritAsync`
  at `Exports/WoWSharpClient/Networking/ClientComponents/DeadActorClientComponent.cs:69`).
  No direct framing inside the task.
- **FG memory footprint:**
  - `IObjectManager.ReleaseSpirit()` (declared at
    `Exports/GameData.Core/Interfaces/IObjectManager.cs:183`). FG
    implementation invokes the in-game release path (Lua
    `RepopMe()` via the FG `LuaCall` channel) rather than the
    `CMSG_REPOP_REQUEST` packet.
  - No other `IObjectManager` member is read in `Update()`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer`
  at `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs:52`
  exercises the `ObjectiveType.ReleaseCorpse → ReleaseCorpseTask` path end
  to end; also exercised by
  `BotRunner.Tests.LiveValidation.SpiritHealerTests.SpiritHealer_Resurrect_PlayerAliveWithSickness`
  at `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs:36`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~DeathCorpseRunTests"`
- **Catalog `TaskFamily` claim:** `Recovery`. Pushed on death by every
  family that exposes the bot to mob damage (Combat, Dungeoneering,
  Raid, BG, Questing, Gathering, World-event); the
  `ObjectiveType.ReleaseCorpse` enum is mapped at
  `Exports/BotRunner/BotRunnerService.ActionMapping.cs:79`.

### RetrieveCorpseTask

- **Class declaration:** `BotRunner.Tasks.RetrieveCorpseTask` at
  `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`. Inherits `BotTask`
  and implements `IBotTask`. **Status:** done.
- **Public surface — current shipped:**
  - `public RetrieveCorpseTask(IBotContext botContext, Position corpsePosition)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only).
  - `protected override NavigationTraceSnapshot? GetDiagnosticNavigationTraceSnapshot()`
  - Internal state: `_navPath` (created via
    `NavigationPathFactory.Create(..., NavigationRoutePolicy.CorpseRun)`),
    `_startTime`, `_lastReclaimAttempt`, `_lastCooldownLog`,
    `_noPathSinceUtc`, `_nonGhostSinceUtc`, `_lastWaypointDriveLogUtc`,
    `_lastTickDiagUtc`, `_stoppedForRetrieval`, `_triedSpiritHealer`.
  - Timing constants: `TaskTimeout = 12min`, `NoPathTimeout = 30s`,
    `ReclaimRetryInterval = 2s`, `CooldownLogInterval = 5s`,
    `ServerReclaimRadius3D = 39f` (MaNGOS `CORPSE_RECLAIM_RADIUS`),
    `ReclaimSafetyMargin = 5f`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async
  contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)
  (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`).
- **Snapshot contract:**
  - *Reads (via `IObjectManager`):* `player.Position`, `player.MapId`,
    `player.TransportGuid`, `player.Health`, `player.MaxHealth`,
    `player.Facing`, `player.PlayerFlags` (`PLAYER_FLAGS_GHOST`),
    `player.Bytes1` (`UNIT_STAND_STATE_DEAD`), `player.InGhostForm`,
    `player.CorpseRecoveryDelaySeconds`, plus the `Units` enumeration
    for the nearby-spirit-healer fallback (filters by
    `Name.Contains("Spirit Healer")` within 50y and corpse distance
    `> 200y`). These project into `WoWActivitySnapshot.player` and
    `WoWActivitySnapshot.movementData`.
  - *Writes/mutates (observable via snapshot):*
    `WoWActivitySnapshot.player.Position` (driven by repeated
    `ObjectManager.MoveToward(waypoint)` calls along the corpse-run
    path), `WoWActivitySnapshot.movementData` movement-flag deltas
    from `MSG_MOVE_*`, `WoWActivitySnapshot.player.PlayerFlags`
    clears `PLAYER_FLAGS_GHOST` after `CMSG_RECLAIM_CORPSE` succeeds,
    `WoWActivitySnapshot.player.Health` transitions from 1 → full HP
    on resurrect, and the task-stack top clears via `PopTask(...)`
    with reason `AliveAfterRetrieve` | `Timeout` | `NoPathTimeout` |
    `GhostStateUnavailable` | `NoLongerDeadOrGhost` | `PlayerUnavailable`.
- **BG protocol footprint:**
  - `Opcode.CMSG_RECLAIM_CORPSE` (8-byte payload = player GUID) sent
    by `WoWSharpObjectManager.RetrieveCorpse()` at
    `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs:1193`
    (delegates to
    `IDeadActorNetworkClientComponent.ResurrectAtCorpseAsync` at
    `DeadActorClientComponent.cs:99`).
  - `Opcode.CMSG_SPIRIT_HEALER_ACTIVATE` indirectly via
    `spiritHealer.Interact()` when corpse is `> 200y` and a
    `Spirit Healer` unit is within 50y (declared at
    `IDeadActorNetworkClientComponent.cs:85` /
    `DeadActorClientComponent.cs:188`).
  - Movement opcodes fan out from `ObjectManager.MoveToward` /
    `ObjectManager.StopAllMovement` /
    `ObjectManager.ForceStopImmediate`: `MSG_MOVE_HEARTBEAT`,
    `MSG_MOVE_START_FORWARD`, `MSG_MOVE_STOP`, `MSG_MOVE_SET_FACING`,
    declared in `Exports/GameData.Core/Enums/Opcode.cs`.
- **FG memory footprint:**
  - `IObjectManager.Player` (position, map id, transport guid,
    health/maxHealth, facing, `PlayerFlags`, `Bytes1`,
    `InGhostForm`, `CorpseRecoveryDelaySeconds`).
  - `IObjectManager.Units` enumeration (for the spirit-healer
    fallback).
  - `IObjectManager.RetrieveCorpse()` (declared at
    `Exports/GameData.Core/Interfaces/IObjectManager.cs:184`); FG
    invokes the in-game reclaim path (Lua `RetrieveCorpse()`).
  - `IObjectManager.MoveToward(Position)`,
    `IObjectManager.StopAllMovement()`,
    `IObjectManager.ForceStopImmediate()`.
  - `IWoWUnit.Interact()` on the spirit healer for the long-distance
    fallback.
  - Pathfinding queries go through the BotRunner-owned
    `_navPath` (`NavigationPathFactory.Create(...)` with
    `NavigationRoutePolicy.CorpseRun`) which calls the
    `Container.PathfindingClient`.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer`
  at `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs:52`
  (Razor Hill `(340, -4686, 19.5)` death → release → graveyard
  teleport → `RetrieveCorpse` dispatch → run-back → reclaim within
  `MaxRecoverySeconds = 120`). Also exercised by
  `SpiritHealerTests` when the bot opts into spirit-healer rescue.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~DeathCorpseRunTests"`
- **Catalog `TaskFamily` claim:** `Recovery`. Mapped from
  `Communication.ObjectiveType.RetrieveCorpse → CharacterAction.RetrieveCorpse`
  at `Exports/BotRunner/BotRunnerService.ActionMapping.cs:80`. Pushed
  by every family that can leave the bot in ghost form (same
  catalog set as `ReleaseCorpseTask`).

### StuckRecoveryTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Recovery/StuckRecoveryTask.cs`.
  **Status:** `not-started` as a task — there is no
  `StuckRecoveryTask.cs` today. The shipped behavior is split between
  `Exports/WoWSharpClient/Movement/MovementController.cs`
  (detection + escalation via the
  `OnStuckRecoveryRequired(int level, Position pos)` event at
  `MovementController.cs:176`, levels 1–3 — declared but **never
  subscribed** in `Exports/BotRunner` today) and the in-path replan
  that `Exports/BotRunner/Movement/NavigationPath.cs` performs in
  `ObserveMovementStuckRecovery(...)` at line 2024 (forced
  `CalculatePath(... reason: NavigationTraceReason.MovementStuckRecovery)`
  plus `PromoteWaypointAfterStuckRecovery`). The
  `MovementStuckRecoveryGeneration` counter exposed by
  `WoWSharpObjectManager` is the integration point a future task
  consumes (already wired in
  `NavigationPathFactory` and `TravelTask.cs:935`).
- **Public surface — current shipped:** none — planned anchor:
  `Exports/BotRunner/Tasks/Recovery/StuckRecoveryTask.cs`. Current
  callers depend on the `MovementController` event +
  `NavigationPath.ObserveMovementStuckRecovery` flow described above
  and on the `BotRunner.NavigationPath.MovementStuckRecovery` trace
  reason constant (`NavigationPath.cs:121`).
- **Public surface — target (Phase 1, after S1.0):** Four-method async
  contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)
  (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`).
- **Public surface (planned task-specific shape):**
  - `public StuckRecoveryTask(IBotContext botContext, int stuckLevel, Position stuckPosition)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under S1.13 Recovery (S1.0/R25, shim-only).
  - Internal state:
    `enum RecoveryStrategy { Replan, Backstep, NavmeshSnap, JumpRecover, AbortAndReport }`,
    plus a `_navmeshGated` flag set from a new
    `IObjectManager.IsOnNavmesh(Position)` probe (proposed in
    Plan/09 — `IsOnNavmesh` is referenced in
    `docs/Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md:52` and
    `docs/Plan/10_PARALLEL_BRM_BAKE.md:59` as the gate that prevents
    `JumpRecover` from depositing the bot off the mesh).
  - Composition rule: subscribed by `BotRunnerService` to the
    `MovementController.OnStuckRecoveryRequired` event; the handler
    pushes a `StuckRecoveryTask` instance once per generation tick of
    `ObjectManager.MovementStuckRecoveryGeneration`.
- **Snapshot contract (planned):**
  - *Reads:* `player.Position`, `player.MapId`, `player.MovementFlags`
    (to distinguish `FORWARD|JUMPING` from idle stalls),
    `player.TransportGuid`, plus the new
    `IObjectManager.IsOnNavmesh(Position)` probe and the
    `MovementController.OnStuckRecoveryRequired` level (1/2/3).
    Physics fields `PhysicsHitWall` / `PhysicsWallNormal2D` /
    `PhysicsBlockedFraction` (BG-only sentinels per
    `project_pfs_overhaul_006_fg_objectmanager_sentinels.md`).
  - *Writes/mutates:*
    `WoWActivitySnapshot.movementData` (recovery sends
    `MSG_MOVE_STOP` → `MSG_MOVE_JUMP` → `MSG_MOVE_START_FORWARD`
    when `JumpRecover` is gated through), and the task-stack top
    clears with reason `Recovered` | `EscalatedToLevel3` |
    `OffNavmeshAbort` | `Timeout`. On L3 it emits
    `physics_stuck` per `docs/Spec/07_PHYSICS.md` so the parent task
    can cancel and lease-reclaim per the Failure-recovery section
    below.
- **BG protocol footprint (planned):** no new opcodes — recovery
  reuses `MSG_MOVE_STOP`, `MSG_MOVE_JUMP`, `MSG_MOVE_START_FORWARD`,
  `MSG_MOVE_SET_FACING`, `MSG_MOVE_HEARTBEAT` (all in
  `Exports/GameData.Core/Enums/Opcode.cs`). Backstep / replan is
  driven by `ObjectManager.MoveToward(...)` on a fresh
  `NavigationPath` waypoint.
- **FG memory footprint (planned):**
  - `IObjectManager.Player` (position, map id, movement flags,
    transport guid).
  - `IObjectManager.MovementStuckRecoveryGeneration` (existing
    counter at the `IObjectManager` surface; already consumed by
    `NavigationPath.ObserveMovementStuckRecovery` at line 2024 and
    by `TravelTask.cs:935`).
  - New `IObjectManager.IsOnNavmesh(Position)` probe (Plan/09
    surface; gate prevents `JumpRecover` from depositing bot off
    the mesh — `docs/Plan/10_PARALLEL_BRM_BAKE.md:59`).
  - `MovementController.OnStuckRecoveryRequired` event (declared at
    `Exports/WoWSharpClient/Movement/MovementController.cs:176`)
    and `MovementController.NotifyExternalStuckRecoveryApplied(...)`
    (line 1706) so the controller can suppress the next stale-forward
    fire after recovery is applied.
  - No `LuaCall`; recovery on FG is purely physics-state + movement
    packets.
- **Test anchor:** **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/StuckRecoveryTests.cs::StuckRecovery_OffPathStall_RecoversOrAborts`.
  No such class exists today. Current adjacent coverage is the
  `MovementController` parity recordings in
  `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  and the `NavigationPath` unit slice in
  `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs` (both
  exercise the *signal* path, not the *task* push path). Mark task
  tests `not-started`.
- **Catalog `TaskFamily` claim:** `Recovery`. Pushed by
  `BotRunnerService` when any running task is stalled (any family
  that drives `MoveToward` can trip the L1/L2/L3 escalation —
  Travel, Combat pull, Questing escort, Dungeoneering pull,
  Gathering route, BG objective).

### ReconnectTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Recovery/ReconnectTask.cs`. **Status:**
  `not-started` as a task — there is no `ReconnectTask.cs` today.
  The shipped reconnect path is split across the StateManager
  process-supervision loop in
  `Services/WoWStateManager/StateManagerWorker.BotManagement.cs`
  (which monitors the bot child process, applies a
  `MinRelaunchInterval = 1min` backoff via `_lastLaunchTimes`, and
  re-launches the BG or FG runner from scratch — there is **no
  `Reconnect` substring in that file**) and the WoWSharpClient
  login state machine reached during the relaunch. The "partial"
  status in the family table reflects this gap: the bot survives a
  disconnect by being relaunched, not by an in-process reconnect
  task.
- **Public surface — current shipped:** none — planned anchor:
  `Exports/BotRunner/Tasks/Recovery/ReconnectTask.cs`. Current
  callers depend on the StateManager relaunch loop in
  `StateManagerWorker.BotManagement.cs` and on the
  `WoWSharpClient.NetworkTests` reconnect-policy assertions in
  `Tests/WoWSharpClient.NetworkTests`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async
  contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)
  (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`).
- **Public surface (planned task-specific shape):**
  - `public ReconnectTask(IBotContext botContext, string lastActivitySnapshotJson)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only)..
  - Internal state:
    `enum ReconnectState { DetectDisconnect, AwaitBackoff, AuthLogin, RealmList, WorldEnter, RestoreActivity, Failed }`,
    plus a `_loginAttempts` counter that escalates to
    `login_failed` after 3 consecutive failures (per the
    Failure-recovery section below).
  - Composition rule: pushed by `BotRunnerService` (or by
    StateManager via an `ObjectiveType.Reconnect` dispatch) when the
    WoWSharpClient world socket reports a disconnect; the task ends
    by restoring `AssignedActivity` from the last persisted snapshot
    (Spec 12 — `WorldEntryHydration` is the existing nearest
    cousin at `Exports/BotRunner/WorldEntryHydration.cs`).
- **Snapshot contract (planned):**
  - *Reads:* `IObjectManager.IsConnected` (target — not yet on the
    interface), `WoWSharpClient` connection events / `IsDead` from
    `IDeadActorNetworkClientComponent`, the last
    `WoWActivitySnapshot` persisted under
    `StateManagerSettings.AssignedActivity` /
    `CharacterSettings.AssignedActivity` (so the bot resumes the
    same activity post-reconnect — see
    [Spec/03_BOTRUNNER.md#configuration](../Spec/03_BOTRUNNER.md#configuration)),
    and the `MinRelaunchInterval = 1min` backoff window owned by
    `StateManagerWorker.BotManagement.cs:35`.
  - *Writes/mutates:* `WoWActivitySnapshot.connected` (Spec 12
    target — wire it up under S1.0); on success, restores
    `WoWActivitySnapshot.player.Position` /
    `WoWActivitySnapshot.player.MapId` from the post-world-enter
    state; task-stack top clears with `PopTask("Reconnected")` |
    `PopTask("LoginFailed")` (the latter signals StateManager to
    cool-down).
- **BG protocol footprint (planned):**
  - Auth: `CMD_AUTH_LOGON_CHALLENGE` →
    `CMD_AUTH_LOGON_PROOF` (SRP6) →
    `CMD_REALM_LIST` (declared in
    `Exports/WoWSharpClient/Client/` per
    [`docs/server-protocol/`](../../docs/server-protocol/)).
  - World: `CMSG_AUTH_SESSION` (post-realm),
    `CMSG_CHAR_ENUM` → `CMSG_PLAYER_LOGIN` → world-enter
    handshake (`SMSG_LOGIN_VERIFY_WORLD`, `SMSG_ACCOUNT_DATA_TIMES`,
    `SMSG_INITIAL_SPELLS`, `SMSG_INITIAL_FACTIONS` consumers in
    `WoWSharpClient`). All declared in
    `Exports/GameData.Core/Enums/Opcode.cs`.
  - Movement settle: `MSG_MOVE_HEARTBEAT` on first physics tick after
    `WorldEntryHydration` completes.
- **FG memory footprint (planned):**
  - FG-side reconnect is **out of scope** for the in-process task:
    when the FG WoW.exe disconnects, the StateManager monitoring task
    (`StateManagerWorker.BotManagement.cs`, 5s health interval / 60s
    orphan kill) re-launches WoW.exe and re-injects Loader.dll. The
    `ReconnectTask` therefore targets the **BG** runner.
  - For the BG path, the task drives
    `WoWSharpClient` connect/login (no `IObjectManager` member calls
    until world-enter completes), then resumes normal
    `IObjectManager` reads identical to the originating task.
- **Test anchor:** **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/ReconnectTests.cs::Reconnect_AfterMidActivityDisconnect_ResumesAssignedActivity`.
  No such class exists today. Current adjacent coverage:
  reconnect-policy unit tests in `Tests/WoWSharpClient.NetworkTests`
  (socket/reconnect policy at the transport layer only — does not
  exercise the task-push surface). Mark task tests `not-started`.
- **Catalog `TaskFamily` claim:** `Recovery`. Pushed by every other
  family because every task can experience a disconnect — the
  Phase-1 acceptance test (`S1.20 — One-hour shake-out`,
  `Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md:216`) explicitly asserts
  the AH-session-disconnect-during-Economy case (Slot
  [SRec.8](#srec8--livevalidation-recovery) bullet 4).

### SpiritHealerTask

- **Class declaration:** **Planned anchor:**
  `Exports/BotRunner/Tasks/Recovery/SpiritHealerTask.cs`. **Status:**
  `not-started` as a dedicated task — there is no
  `SpiritHealerTask.cs` today. The shipped behavior is an inlined
  fallback inside `RetrieveCorpseTask.Update()` (the
  `corpseHorizontalDistance > 200f && !_triedSpiritHealer` branch at
  `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs:202–218`) that
  picks the nearest unit with `Name.Contains("Spirit Healer")` within
  50y and calls `spiritHealer.Interact()`. The packet path
  (`CMSG_SPIRIT_HEALER_ACTIVATE`) is fully implemented in
  `Exports/WoWSharpClient/Networking/ClientComponents/DeadActorClientComponent.cs:188`
  (`ResurrectWithSpiritHealerAsync(ulong spiritHealerGuid)`); the
  task is the missing orchestration that calls it deliberately.
- **Public surface — current shipped:** none — planned anchor:
  `Exports/BotRunner/Tasks/Recovery/SpiritHealerTask.cs`. Inlined
  surface today: the `_triedSpiritHealer` branch inside
  `RetrieveCorpseTask`.
- **Public surface — target (Phase 1, after S1.0):** Four-method async
  contract per [Spec/03_BOTRUNNER.md](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10)
  (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync` + `Name` + `Status` + `BotTaskContext`).
- **Public surface (planned task-specific shape):**
  - `public SpiritHealerTask(IBotContext botContext, ulong? preferredSpiritHealerGuid = null)`
  - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); concrete task body lives in `Update()`. Per-family async refactor lands under the matching S1.4..S1.13 family slot (S1.0/R25, shim-only)..
  - Internal state:
    `enum SpiritHealerState { LocateNpc, ApproachNpc, RequestResurrect, AwaitAlive, AbortNoHealer }`,
    accepts the cost of 25% durability loss + rez sickness aura
    (per existing `RetrieveCorpseTask:198–217` comment).
  - Composition rule: pushed by `RetrieveCorpseTask` (or by
    `BotRunnerService` directly) when corpse is unreachable —
    instance with closed door, contested zone, prolonged stall,
    `> 200y` horizontal distance — and a `Spirit Healer` unit
    (NPC flag `0x20`) is within 50y. On L3 stuck or 3× NoPathTimeout
    from `RetrieveCorpseTask`, this is the documented fallback per
    `SRec.1` below.
- **Snapshot contract (planned):**
  - *Reads:* `player.Position`, `player.MapId`, `player.PlayerFlags`
    (`PLAYER_FLAGS_GHOST`), `player.InGhostForm`, `player.Health`,
    `IObjectManager.Units` filtered by NPC flag `0x20`
    (`NpcFlagSpiritHealer` per
    `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs:24`),
    spirit-healer GUID + position.
  - *Writes/mutates:* `WoWActivitySnapshot.player.PlayerFlags`
    clears `PLAYER_FLAGS_GHOST` after the server processes
    `CMSG_SPIRIT_HEALER_ACTIVATE`,
    `WoWActivitySnapshot.player.Health` transitions from 1 → 50%
    (vanilla rez), the rez-sickness aura `15007` appears on
    `WoWActivitySnapshot.player.Auras`, durability drops 25% on every
    equipped slot (observable via `WoWActivitySnapshot.player.Items`
    durability fields). Task-stack top clears with
    `PopTask("ResurrectedAtHealer")` |
    `PopTask("NoHealerInRange")` | `PopTask("PlayerUnavailable")`.
- **BG protocol footprint (planned):**
  - `Opcode.CMSG_SPIRIT_HEALER_ACTIVATE` (8-byte payload =
    spirit-healer GUID) via
    `IDeadActorNetworkClientComponent.ResurrectWithSpiritHealerAsync`
    at `DeadActorClientComponent.cs:188`. The
    `IObjectManager` surface needed today is `IWoWUnit.Interact()` on
    the spirit-healer unit (the inlined branch in
    `RetrieveCorpseTask` already uses this).
  - `CMSG_RESURRECT_RESPONSE` with accept flag (declared at
    `IDeadActorNetworkClientComponent.cs:69`,
    `DeadActorClientComponent.cs:127`) when the server-side
    spirit-healer flow surfaces a resurrection-request prompt.
  - Approach: movement opcodes inherited from `GoToTask` /
    `ObjectManager.MoveToward(...)`.
- **FG memory footprint (planned):**
  - `IObjectManager.Player` (position, map id, ghost flags,
    health/maxHealth, `InGhostForm`, `Auras`).
  - `IObjectManager.Units` enumeration filtered to NPC flag
    `0x20` (Spirit Healer).
  - `IWoWUnit.Interact()` on the spirit healer (FG path goes
    through gossip-menu confirmation; BG path packets through
    `CMSG_SPIRIT_HEALER_ACTIVATE`).
  - `IObjectManager.MoveToward(...)` /
    `IObjectManager.StopAllMovement()` for the approach leg.
  - No `LuaCall` invocations beyond what `IWoWUnit.Interact()` does
    on FG today.
- **Test anchor:**
  `BotRunner.Tests.LiveValidation.SpiritHealerTests.SpiritHealer_Resurrect_PlayerAliveWithSickness`
  at `Tests/BotRunner.Tests/LiveValidation/SpiritHealerTests.cs:36`.
  Currently asserts the inlined-fallback flow via
  `ObjectiveType.ReleaseCorpse` then waits for the spirit healer to
  resurrect — the test will switch to dispatching a dedicated
  `SpiritHealerTask` once it ships. Mark per-task coverage
  `partial` (the live path is exercised; the task push surface is
  `not-started`).
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~SpiritHealerTests"`
- **Catalog `TaskFamily` claim:** `Recovery`. Pushed as the fallback
  when `RetrieveCorpseTask` reports `NoPathTimeout` or when corpse is
  > 200y away (current heuristic); pushed by any family that left
  the bot dead in a location where corpse-runback is impossible
  (Dungeoneering, Raid, BG, World-event).

## Slots

### SRec.1 — `SpiritHealerTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Recovery/SpiritHealerTask.cs`
- **Goal:** When corpse is unreachable (instance with closed door,
  contested zone, prolonged stall), bot accepts spirit healer
  resurrection. Cost: rez sickness.

### SRec.2 — `StuckRecoveryTask` hardening

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S9.2 (Plan/09 — jump policy)
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Recovery/StuckRecoveryTask.cs`

### SRec.3 — `ReconnectTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** On disconnect, bot reconnects via login state machine,
  resumes activity from snapshot.

### SRec.4 — `LeaseReturnTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S3.3

### SRec.5 — `LowDurabilityTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Below `LowDurabilityThreshold` (default 40%), the
  `ProgressionPlanner` Survival band routes the bot to the nearest
  repair vendor.

### SRec.6 — `LowInventoryTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Above `InventoryFullThreshold` (default 95%), the
  Survival band routes the bot to vendor/bank/mail to free slots.

### SRec.7 — `LowGoldTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** Below `GoldTargetCopper × 0.5`, the Gold band engages and
  bot farms gold (instance runs for greens, gathering routes, AH
  flipping per `EconomyCoordinator` advice).

### SRec.8 — LiveValidation (recovery)

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Goal:** Inject failure conditions:
  - Bot dies in dungeon → assert corpse run completes.
  - Bot inventory full mid-gather → assert vendor visit.
  - Bot gold = 0 mid-activity → assert progression band shift.
  - Bot disconnect during AH session → assert reconnect + resume.

## Failure recovery (meta)

When recovery fails:

- **Repeated stuck after stuck-recovery** → emit
  `physics_stuck` × 3 → cancel current task, lease reclaim,
  flag as `bot_crash`-adjacent.
- **Spirit healer unavailable** (no SH in zone) → kill the bot via
  `.kick` and let StateManager relaunch the session.
- **Login fails 3× in a row** → flag as `login_failed` cluster; do
  not relaunch until cool-down.
