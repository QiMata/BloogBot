# Spec 02 — StateManager

## Responsibilities

StateManager is the single owner of:

- Bot launch, monitoring, and disconnect/reconnect handling.
- Realm selection (test vs prod — see
  [`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md)).
- Account/character lifecycle (create via SOAP, delete, recycle).
- Mode dispatch (`Test` / `Automated` / `OnDemandActivities`).
- Activity registry + legality validation.
- **OnDemand Activity launcher** — spawn pool bots, gear them,
  circumvent normal gameplay restrictions per the activity's config,
  party them, teleport to staging area, hand off to human.
- Snapshot cache (authoritative roster state).
- Metrics aggregation + dashboard summary APIs (long-term history,
  not just live state).
- Persistent config storage + hot-reload broadcast.

StateManager does **not** own:

- Game memory reads, packet IO, world geometry.
- Behavior tree execution (BotRunner's job).
- Direct game-client lifecycle (FG injection is FG's job; BG is a bot's
  own protocol session).

## What StateManager does NOT do anymore (decisions of record)

These were considered and dropped:

- **No lease ledger.** Bots are always on. Autonomous-progression bots
  run their behavior trees continuously. OnDemand bots come from a
  reserved pool that is siloed from progression. There is no
  "borrowed" state to track. See
  [`Spec/16_REALMS_AND_ACCOUNTS.md#ondemand-reserved-pool`](16_REALMS_AND_ACCOUNTS.md#ondemand-reserved-pool).
- **No return-objective bookkeeping.** Autonomous bots resume their
  own behavior tree when not in an OnDemand instance; OnDemand bots
  are ephemeral.
- **No coordinator-side scheduler for AV/MC/etc.** Those are part of
  the autonomous decision engine — bots organically form groups based
  on their own progression decisions. The OnDemand launcher is the
  only "scheduler" StateManager runs.

## Modes

There are exactly three modes. Determined per-config-file by the
`Mode` field at the JSON root, with backward-compatible bare-array
config defaulting to `Test`.

| Mode | Behavior |
|---|---|
| `Test` | StateManager waits for explicit `ObjectiveMessage` dispatch from a test fixture. No automatic loadout, no automatic activity start. Used by every `BotRunner.Tests` LiveValidation test. |
| `Automated` | At world-entry, StateManager auto-dispatches `APPLY_LOADOUT` then parses `AssignedActivity` and starts the corresponding activity. Bots self-progress through `Loadout` → `AssignedActivity` → `NextActivities[]`. |
| `OnDemandActivities` | Adds external request handling on top of `Automated`. Shodan listens for in-game whispers (`!fish ratchet`, `!group rfc 15-20`); the WPF UI submits requests over IPC. The mode handler treats human requests as high-priority leases. |

The `Mode` field is the contract. Adding a fourth mode requires a spec PR.

### Mode handler interface

```csharp
public interface IStateManagerModeHandler
{
    StateManagerMode Mode { get; }

    Task OnWorldEntryAsync(CharacterSettings character, CancellationToken ct);
    Task OnSnapshotAsync(CharacterSettings character,
                        WoWActivitySnapshot snapshot,
                        CancellationToken ct);
    Task OnExternalActivityRequestAsync(string requestingPlayer,
                                        string activityDescriptor,
                                        CancellationToken ct);
}
```

Existing implementations live at `Services/WoWStateManager/Modes/` —
`TestModeHandler.cs` and `AutomatedModeHandler.cs` shipped. The third
handler, `OnDemandActivitiesModeHandler.cs`, is Phase 2 work
(see [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)).

## Config schema

The root of `StateManagerSettings.json` (and every `Settings/Configs/*.json`):

```json
{
  "Mode": "OnDemandActivities",
  "ServerCapabilities": {
    "Naxx": true, "AQ40": true, "BWL": true, "MC": true,
    "Ony": true, "ZG": true, "AQ20": true,
    "Battlegrounds": ["WSG", "AB", "AV"]
  },
  "LivingServer": {
    "EnableAutomatedProgression": true,
    "EnableOnDemandActivities": true,
    "MaxConcurrentActivities": 20,
    "BotLeaseDefaultMinutes": 90,
    "AVRosterEnabled": true
  },
  "Metrics": {
    "Enabled": true,
    "PrometheusEndpoint": "/metrics",
    "HighCardinalityDebugMetrics": false
  },
  "Logging": {
    "Profile": "Normal",
    "EnablePathDiagnostics": false,
    "EnableSnapshotDumps": false,
    "ConnectionChurnRateLimitPerMinute": 10
  },
  "Pathfinding": {
    "ReplicaCount": 1,
    "RoutePackWarmup": true,
    "RequestTimeoutSeconds": 30,
    "QueueLimit": 500
  },
  "Characters": [
    {
      "AccountName": "PROD001",
      "FactionSide": "Horde",
      "Role": "DPS",
      "Loadout": { "TargetLevel": 60, "GearProfile": "FuryWarriorPreRaid", ... },
      "AssignedActivity": null,
      "ProgressionPriority": "default",
      "Mode": "Automated"
    }
  ]
}
```

Per-character `Mode` overrides the file-level `Mode`. Default if omitted
follows the file root.

## OnDemand Activity launcher

The OnDemand launcher is StateManager's *only* scheduler. It runs in
response to operator clicks (via the UI) or whispered commands (via
Shodan in OnDemand mode). It does NOT preempt autonomous progression
bots; it operates over the reserved pool only.

The launcher's per-request shape:

```text
OnDemandActivityInstance {
  InstanceId,                  // GUID
  ActivityId,                  // catalog row reference
  HumanGuid?,                  // GUID of requesting human character (may be null for test)
  HumanRole,                   // Member | Leader | Observer (from activity config)
  PoolBots[],                  // accounts assigned to this instance
  Stage,                       // Spawning | Outfitting | Partying | Travelling | Engaged | TearDown | Done
  StagingPosition,             // world coords + map
  Config,                      // ActivityConfig - per-row config object (loadout, gear, prereqs, lockout-skip, etc.)
  Started,
  Ended?,
  TerminationReason?           // FailureReason | null
}
```

Stages:

1. **Spawning.** Reserve N pool accounts (per role template). Create
   or recycle characters that match faction/class/spec/level demands.
2. **Outfitting.** Apply `LoadoutSpec` to each bot: GM-set level,
   `.reset talents` + apply build, `.learn` spells, `.setskill`, gear
   via vendor purchase or `.additem`. Bypass progression checks (the
   instance is siloed).
3. **Partying.** Group invite + accept; raid format for 10/20/40-man;
   raid-leader/group-leader assignment per `HumanJoinPolicy`.
4. **Travelling.** Teleport every bot directly to the staging
   coordinate via `.tele` SOAP (no walked travel — siloed instances
   skip the realistic travel path).
5. **Engaged.** Hand party leader to human (unless config says
   "Bot Raid Leader"). Bots execute their activity tasks (combat,
   role positioning) under the human's lead. StateManager monitors
   liveness; no autonomous re-planning.
6. **TearDown.** On human disengage / activity completion / human
   request: log out bots, delete or recycle the characters, free pool
   slots.

Operations:

- `LaunchAsync(activityId, humanGuid, humanLevel, params)` — runs
  the whole stage progression; returns when stage transitions to
  `Engaged` (so the UI can show the human "your group is ready").
- `MonitorAsync(instanceId)` — emits stage-transition events; the UI
  subscribes.
- `TerminateAsync(instanceId, reason)` — operator-initiated stop.

The launcher emits `wwow.statemanager.ondemand.*` metrics. Active
instances are visible in the UI's Activities panel.

## Topology and scale

### Starting topology

One `WoWStateManager.exe` process. One `wwow-pathfinding` container. One
`wwow-scene-data` container. N `BackgroundBotRunner.exe` processes hosting
≥ 1 bot each.

### Scaling approach (iterative)

The user's stance (2026-05-12 design refinement): we are **not optimizing
for 3000 bots up front**. The first scale target is **80 concurrent
bots** (Alterac Valley capacity, the largest OnDemand activity). After
that, we iterate:

1. Measure what the current architecture supports.
2. Identify the bottleneck (snapshot ingest? path queue? mangosd? DB?).
3. Optimize the cheapest fix.
4. Re-measure.
5. Repeat until we know where the hardware-budget ceiling actually
   sits.

Hardware is purchased *after* the architecture is fine-tuned. The
load-test phase produces a "what's possible on this hardware" report,
not "scale to 3000 or fail."

Initial measurements should report:

- Snapshot ingest P50/P95/P99 per bot count band.
- Pathfinding queue depth + latency.
- mangosd CPU, memory, disconnect rate.
- MaNGOS DB write IOPS + connection count.
- StateManager CPU, memory.

### Scale-out options (kept for future reference)

When the iterative loop hits a single-StateManager ceiling, options are:

1. Active/passive failover. One active SM + one warm standby.
2. Active/active by account hash. `hash(accountName) % stateManagerCount`
   partitions bots across SMs.
3. Active/active by zone. Avoid until cross-zone travel is rock solid.

Pick the simplest option that clears the measured bottleneck.

## Coordinators

Coordinators are StateManager subsystems that handle multi-bot
orchestration for an OnDemand activity family. They are *not* part of
BotRunner. They drive the OnDemand Activity launcher's stage progression
for activities that need family-specific behavior.

Existing (in `Services/WoWStateManager/Coordination/`):

- `BattlegroundCoordinator.cs` — BG queue + entry semantics for AV/AB/WSG.
- `DungeoneeringCoordinator.cs` — group form, dungeon entry, encounter
  flow.
- `CombatCoordinator.cs` — multi-bot combat (group composition).

Required additions (when the corresponding activity family is wired):

- `RaidCoordinator` — 10/20/40-man formation, subgroups, ready check.
- `QuestCoordinator` — quest-chain assignment, shared-objective coord.
- `WorldEventCoordinator` — STV Fishing Extravaganza, holidays.

Each coordinator implements:

```csharp
public interface IActivityCoordinator
{
    string ActivityFamily { get; }            // "Dungeon" | "Raid" | "Battleground" | ...
    bool CanHandle(ActivityDefinition def);
    Task<OnDemandActivityInstance> LaunchAsync(ActivityDefinition def,
                                                IReadOnlyList<PoolBot> pool,
                                                ActivityParams parameters,
                                                CancellationToken ct);
    Task<ActivityOutcome> AwaitCompletionAsync(OnDemandActivityInstance instance,
                                                CancellationToken ct);
    Task CancelAsync(OnDemandActivityInstance instance, string reason);
}
```

Coordinators consult per-activity `Config/activities/<id>.json` for
behavior tuning (loot policy on the autonomous server, role overrides,
loadout overrides) — but for OnDemand, the activity config drives every
spawn-and-circumvent decision.

## Snapshot processing

`WoWActivitySnapshot` (proto) is the bot's tick output. StateManager:

1. Validates the proto against the schema.
2. Updates `_snapshotCache[accountName]` atomically.
3. Computes per-bot deltas (position, level, gold, lockouts).
4. Calls `IStateManagerModeHandler.OnSnapshotAsync(...)`.
5. Emits metrics: `wwow_snapshot_ingest_duration_seconds`,
   `wwow_snapshot_cache_entries`.

Snapshots **do not carry full enemy/object payloads.** Bots expose those
through their local `IObjectManager`. The snapshot contains:

- Position, map, transport state, movement flags.
- Health/power, level, XP.
- Inventory delta hashes (full inventory only on request via
  `RequestInventoryAsync`).
- Quest log delta, lockout state, auras.
- Skills, talents, reputation.
- Current activity instance + lease id.
- Lifecycle (login, world-entry, logout, crashed).

See [`Spec/08_PROTOCOLS.md`](08_PROTOCOLS.md) for the proto layout.

## Dashboard summary API

StateManager exposes summary APIs over port 8088 (existing
`StateManagerListener`). The Dashboard polls these every 1–5 s
(configurable). Endpoints (all protobuf, no HTTP):

- `GetBotsSummary` — counts by faction/level-band/mode/status.
- `GetActivitiesSummary` — active/queued/completed/failed counts; queue
  depth.
- `GetLeasesSummary` — current leases by activity/role.
- `GetErrorsSummary` — top-N normalized errors with counts (from the
  taxonomy in [`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md)).
- `GetServiceHealth` — realmd, mangosd, SOAP, pathfinding, scenedata.
- `GetActivityCatalog` — full catalog (versioned).
- `GetActivityConfig(activityId)` — current config row.
- `SaveActivityConfig(payload)` — atomic; emits ConfigChanged event.
- `RequestActivity(payload)` — submit on-demand request.
- `CancelActivityInstance(instanceId)`.
- `GetMetricsCatalog` — list of available metrics (for power users).

## Hot reload

When a config changes (UI write or file watcher):

1. StateManager schema-validates the new payload.
2. On success, atomically replaces the file (`tmp` + rename).
3. Broadcasts `ConfigChangedEvent` over IPC.
4. Subscribers (BotRunner, services) reload only the affected sections.
5. Each subscriber ACKs within `HotReloadAckTimeout` (default 2 s).
6. If any subscriber fails ACK, StateManager rolls back the file.

See [`Spec/14_CONFIG.md`](14_CONFIG.md) for the protocol detail and the
list of reloadable sections.

## Failure handling

- **Bot disconnect mid-activity** → coordinator decides: replace the bot
  with another lease (BG, dungeon) or fail the instance (raid; replacements
  cost more than the activity is worth at this scale).
- **Coordinator timeout** → instance cancels, leases reclaim, error logged
  with normalized reason from [`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md).
- **WoW.exe crash (FG)** → StateManager restarts the FG bot, but the
  crash is logged with stack info and queued as a hardening task. Crashes
  are bugs.
- **Server (mangosd) crash** → all bots disconnect; StateManager waits
  on `wow-mangosd` health, then triggers reconnect storm with a
  rate-limited login schedule.

## Existing code anchors

| Concept | File |
|---|---|
| Settings load + mode parse | `Services/WoWStateManager/Settings/StateManagerSettings.cs` |
| Mode handlers | `Services/WoWStateManager/Modes/*ModeHandler.cs` |
| Coordinators | `Services/WoWStateManager/Coordination/*Coordinator.cs` |
| Progression planners | `Services/WoWStateManager/Progression/*.cs` |
| IPC listeners | `Services/WoWStateManager/Listeners/StateManagerSocketListener.cs` |
| Snapshot ingest | `Services/WoWStateManager/StateManagerWorker.SnapshotProcessing.cs` |
| Bot lifecycle | `Services/WoWStateManager/StateManagerWorker.BotManagement.cs` |
| MaNGOS bootstrap | `Services/WoWStateManager/MangosServerBootstrapper.cs` |
| OnDemand launcher (to be added) | `Services/WoWStateManager/OnDemand/OnDemandActivityLauncher.cs` |
| Pool manager (to be added) | `Services/WoWStateManager/OnDemand/ReservedPoolManager.cs` |
| Account/character provisioning (to be added) | `Services/WoWStateManager/OnDemand/AccountProvisioner.cs` |
