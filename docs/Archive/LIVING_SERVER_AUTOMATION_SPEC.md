# Living Server Automation Spec

## Purpose

This spec defines the target state for WWoW as a living Vanilla 1.12.1 server:
bots autonomously progress toward account and roster goals, while human
characters can request on-demand activities and play with coordinated bots using
legal game parameters.

The spec is intentionally implementation-ready but not an implementation plan
for this turn. It works backwards from the final server state into decisions,
contracts, services, metrics, logging, and phased work.

## Source Baseline

Local research sources:

- `docs/WESTWORLD_ARCHITECTURE.md` - 3000-bot living-server vision, StateManager
  and DecisionEngine responsibilities.
- `docs/leveling-guide/README.md` - WoW 1.12.1 gameplay knowledge base, content
  taxonomy, source priority, and DecisionEngine consumption rules.
- `docs/leveling-guide/decision-engine/*` - action selection bands, unlock
  graph, state flags, and per-bracket action menus.
- `docs/TRAVEL_PLANNING.md` - multi-modal travel and generated static route
  pack direction.
- `docs/TECHNICAL_NOTES.md` - current scale limits and target 3000-bot
  architecture notes.

External platform references:

- [Microsoft ASP.NET Core metrics documentation](https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics):
  .NET/OpenTelemetry metrics can expose a Prometheus scrape endpoint.
- [OpenTelemetry .NET metrics documentation](https://opentelemetry.io/docs/languages/dotnet/metrics/):
  use `System.Diagnostics.Metrics` and OpenTelemetry meters/exporters as the
  shared instrumentation model.
- [Docker logging driver configuration](https://docs.docker.com/engine/logging/configure/):
  default `json-file` has no rotation by default; Docker recommends the
  `local` logging driver for efficient local rotated logs when it fits the
  deployment.

## Final State

The final server has four observable properties:

1. Human players can request a legal on-demand activity by activity, location,
   level range, faction, role needs, and optional constraints. The system
   chooses and coordinates bots so the human can participate in the activity.
2. Bots continuously progress when not serving a human request. They level,
   travel, train, gear, farm, trade, queue battlegrounds, run dungeons and
   raids, unlock attunements, and supply the economy.
3. Pathfinding, scene-data, state, and bot execution services scale without
   turning path requests, snapshots, connect/disconnect churn, or diagnostic
   logs into the bottleneck.
4. Known failure modes are counted and ranked. The next engineering task can be
   chosen from metrics instead of from anecdote.

## Non-Negotiable Rules

- Production navigation must stay generated-data owned. No route-specific
  production waypoint scripts, blocker guards, live-position guards, clearance
  cylinders, or hardcoded coordinate hacks.
- StateManager owns orchestration. BotRunner owns execution of assigned tasks.
  PathfindingService and SceneDataService own world-data answers.
- A bot must not be assigned an activity that is illegal for its faction, class,
  level, attunement, lockout, transport access, discovered flight paths, or
  server capability.
- On-demand activity assignments must be reversible. A bot borrowed for a human
  request returns to its progression plan with explicit state and inventory
  cleanup.
- Logging and metrics must be cheap enough to leave enabled in normal operation.
  Deep diagnostics are opt-in by category, account, activity id, and duration.

## On-Demand Activity Model

The required public shape is:

```text
OnDemandActivity {
  Activity,
  Location,
  LevelRange
}
```

Internally, the scheduler needs a richer manifest:

```text
ActivityDefinition {
  Id,
  Activity,
  Location,
  LevelRange,
  FactionPolicy,
  MinPlayers,
  MaxPlayers,
  RoleTemplate,
  LegalParams,
  EntryRequirements,
  TravelTarget,
  ExpectedDuration,
  Rewards,
  ProgressionTags,
  HumanJoinPolicy,
  BotSelectionPolicy
}
```

`Activity`, `Location`, and `LevelRange` remain the human-facing index. The
extra fields let StateManager prove the request is legal and assemble bots.

## Initial On-Demand Activity Catalog

This table is the first catalog cut. The exact rows should be generated from
`docs/leveling-guide/` into a machine-readable manifest later.

| Activity | Location | Level Range |
|---|---|---|
| Starter questing | Elwynn Forest | 1-10 |
| Starter questing | Dun Morogh | 1-10 |
| Starter questing | Teldrassil | 1-10 |
| Starter questing | Durotar | 1-10 |
| Starter questing | Tirisfal Glades | 1-10 |
| Starter questing | Mulgore | 1-10 |
| Zone questing | Westfall | 9-18 |
| Zone questing | Loch Modan | 10-19 |
| Zone questing | Darkshore | 10-20 |
| Zone questing | Silverpine Forest | 10-20 |
| Zone questing | The Barrens | 10-25 |
| Zone questing | Redridge Mountains | 15-25 |
| Zone questing | Ashenvale | 18-30 |
| Zone questing | Duskwood | 18-30 |
| Zone questing | Wetlands | 20-30 |
| Zone questing | Hillsbrad Foothills | 20-30 |
| Zone questing | Stonetalon Mountains | 16-27 |
| Zone questing | Thousand Needles | 25-35 |
| Zone questing | Desolace | 28-38 |
| Zone questing | Arathi Highlands | 30-40 |
| Zone questing | Stranglethorn Vale | 30-45 |
| Zone questing | Dustwallow Marsh | 35-45 |
| Zone questing | Badlands | 35-45 |
| Zone questing | Tanaris | 40-50 |
| Zone questing | Feralas | 40-50 |
| Zone questing | Searing Gorge | 43-50 |
| Zone questing | Azshara | 45-55 |
| Zone questing | The Hinterlands | 30-45 |
| Zone questing | Felwood | 48-55 |
| Zone questing | Un'Goro Crater | 48-55 |
| Zone questing | Western Plaguelands | 50-60 |
| Zone questing | Eastern Plaguelands | 53-60 |
| Zone questing | Burning Steppes | 50-58 |
| Zone questing | Winterspring | 55-60 |
| Zone questing | Silithus | 55-60 |
| Dungeon | Ragefire Chasm | 13-18 |
| Dungeon | Wailing Caverns | 17-24 |
| Dungeon | Deadmines | 17-26 |
| Dungeon | Shadowfang Keep | 22-30 |
| Dungeon | Blackfathom Deeps | 20-30 |
| Dungeon | Razorfen Kraul | 24-34 |
| Dungeon | Gnomeregan | 29-38 |
| Dungeon | Razorfen Downs | 35-45 |
| Dungeon | Uldaman | 41-51 |
| Dungeon | Zul'Farrak | 44-54 |
| Dungeon | Maraudon | 46-55 |
| Dungeon | Sunken Temple | 50-56 |
| Dungeon | Blackrock Depths | 52-60 |
| Dungeon | Lower Blackrock Spire | 55-60 |
| Dungeon | Upper Blackrock Spire | 58-60 |
| Dungeon | Dire Maul East | 55-60 |
| Dungeon | Dire Maul West | 55-60 |
| Dungeon | Dire Maul North | 55-60 |
| Dungeon | Scholomance | 58-60 |
| Dungeon | Stratholme Undead | 58-60 |
| Dungeon | Stratholme Live | 58-60 |
| Raid | Zul'Gurub | 60 |
| Raid | Ruins of Ahn'Qiraj | 60 |
| Raid | Molten Core | 60 |
| Raid | Onyxia's Lair | 60 |
| Raid | Blackwing Lair | 60 |
| Raid | Temple of Ahn'Qiraj | 60 |
| Raid | Naxxramas | 60 |
| Battleground | Warsong Gulch | 10-60 |
| Battleground | Arathi Basin | 20-60 |
| Battleground | Alterac Valley | 51-60 |
| Profession farming | Mining route | 1-60 |
| Profession farming | Herbalism route | 1-60 |
| Profession farming | Skinning route | 1-60 |
| Profession leveling | City trainer and recipe loop | 5-60 |
| Economy | Auction house restock | 1-60 |
| Economy | Vendor, repair, bank, mail loop | 1-60 |
| Reputation grind | Timbermaw Hold | 48-60 |
| Reputation grind | Argent Dawn | 50-60 |
| Reputation grind | Cenarion Circle | 55-60 |
| Reputation grind | Thorium Brotherhood | 50-60 |
| Reputation grind | Zandalar Tribe | 60 |
| Attunement | Molten Core attunement | 55-60 |
| Attunement | Onyxia Horde chain | 55-60 |
| Attunement | Onyxia Alliance chain | 55-60 |
| Attunement | Blackwing Lair attunement | 58-60 |
| Attunement | Naxxramas attunement | 60 |
| World event | STV Fishing Extravaganza | 30-60 |
| World boss | Azuregos | 60 |
| World boss | Lord Kazzak | 60 |
| World boss | Emerald Dragons | 60 |

## Activity Legality

Each activity request must be normalized into a legal plan:

- Level is inside the activity range and inside the requested bracket.
- Faction can reach the location and use the entrance or queue.
- Dungeon and raid entries satisfy key, attunement, lockout, and group-size
  rules.
- Battlegrounds satisfy bracket, faction balance, queue size, and party limits.
- Transport and flight requirements are available to each selected bot, or the
  planner can insert legal discovery/travel steps.
- Human player role and level are included in the role template, not ignored.
- Server capability flags can suppress incomplete VMaNGOS content.

Illegal requests should return a structured rejection:

```text
ActivityRejected {
  Reason,
  MissingRequirements[],
  SuggestedAlternatives[]
}
```

## Progression Automation

Every bot has a progression plan with terminal goals from
`docs/leveling-guide/README.md`:

- level to 60,
- class kit and class quests complete,
- professions distributed across the account,
- important reputations and attunements complete,
- mount and gold targets met,
- PvP rank goals pursued,
- role-appropriate gear acquired.

Progression uses three planning layers:

1. `RosterPlanner`: long-horizon account goals. It decides which characters
   should exist and which role/spec/profession/gear goals they carry.
2. `ProgressionPlanner`: per-character next objective. It consumes snapshots,
   unlock graph, priority bands, economy state, lockouts, and current activity
   assignments.
3. `ActivityScheduler`: multi-character coordination. It reserves bots for
   dungeons, raids, BGs, economy loops, and human on-demand requests.

The scheduler treats human activity requests as high priority but bounded
leases. A bot lease records:

```text
BotLease {
  ActivityId,
  HumanRequesterGuid?,
  BotAccount,
  Role,
  StartTime,
  MaxDuration,
  ReturnObjective,
  CleanupPolicy
}
```

## StateManager Topology

Target decision: one logical StateManager should own global orchestration first.
It may be implemented as one process for the initial living server. Later, it
can partition internally or externally without changing public contracts.

StateManager responsibilities:

- Launch and monitor all bot runners.
- Maintain the authoritative in-memory roster snapshot cache.
- Host the activity registry and human request API.
- Own bot leases and prevent double assignment.
- Dispatch high-level actions to BotRunner.
- Coordinate group, raid, battleground, trade, and economy activities.
- Publish metrics and structured activity traces.

Scale-out option:

- Active/passive StateManager for failover before active/active.
- If active/active becomes necessary, partition by account hash for snapshots
  and use a single activity-lease store for cross-partition coordination.
- Avoid zone-sharded StateManagers until cross-zone group travel is stable.

## Human On-Demand API

The first API can be HTTP or socket based. The contract should be transport
agnostic:

```text
ListActivities(filter)
RequestActivity(activity, location, levelRange, params, humanCharacter)
GetActivityStatus(activityInstanceId)
CancelActivity(activityInstanceId)
```

Example:

```json
{
  "activity": "Dungeon",
  "location": "Wailing Caverns",
  "levelRange": "17-24",
  "params": {
    "faction": "Horde",
    "humanRole": "Dps",
    "lootPolicy": "NeedBeforeGreed",
    "startWhenReady": true
  }
}
```

The response should include selected bots, missing role wait state, travel ETA,
and any legality warnings.

## WoWStateManagerUI Dashboard

`WoWStateManagerUI` should make the existing Dashboard tab the operator surface
for both observability and on-demand activity control. Do not create a separate
metrics tab unless the Dashboard becomes too dense after implementation.

Dashboard responsibilities:

- Show service health and server readiness.
- Show living-server metrics from StateManager and the supporting services.
- Load, inspect, edit, validate, and save on-demand activity configurations.
- Start, pause, cancel, and inspect activity instances.
- Show selected bots, missing roles, travel ETA, legality failures, and active
  bot leases for each activity.
- Surface the highest-count known errors with enough labels to decide the next
  engineering fix.

The UI should not be the authoritative scheduler or metrics registry. It is a
desktop operator console. StateManager owns activity state, legality,
scheduling, and persisted activity definitions; the UI edits those definitions
through StateManager APIs and renders the resulting state.

### Dashboard Metrics Panels

The Dashboard tab should include these panels:

| Panel | Data |
|---|---|
| Server health | realmd, mangosd, SOAP, StateManager, PathfindingService, SceneDataService |
| Bot population | online, available, leased, disconnected, by faction/mode/level band |
| Activity operations | active requests, queued requests, completed/failed counts, queue duration |
| On-demand activities | catalog entries, enabled state, selected config, validation status |
| Pathfinding | queue depth, latency, timeouts, route-pack hits/bypasses |
| Connections | connect/disconnect counters by service and normalized reason |
| Error triage | top error counters by service/activity/reason |
| Logging health | suppressed log counts and noisy categories |

### Activity Config Editing

On-demand activities should be editable as structured configs, not loose JSON
text first. The UI can include an advanced raw JSON view later, but the normal
flow should be form/grid based:

```text
ActivityConfig {
  Id,
  Enabled,
  Activity,
  Location,
  LevelRange,
  FactionPolicy,
  RoleTemplate,
  MinPlayers,
  MaxPlayers,
  LegalParams,
  EntryRequirements,
  TravelTarget,
  BotSelectionPolicy,
  HumanJoinPolicy,
  ExpectedDuration,
  Tags
}
```

UI actions:

- `LoadActivityCatalog`
- `ValidateActivityConfig`
- `SaveActivityConfig`
- `EnableActivity`
- `DisableActivity`
- `DuplicateActivityConfig`
- `RequestActivity`
- `CancelActivity`

Validation must happen server-side in StateManager so CLI/API/UI callers all
get the same legality result. The UI may run client-side field validation for
immediacy, but it must still display the authoritative StateManager validation
result before saving or launching an activity.

## Bot Selection

Selection scores candidates by:

- legality,
- role fit,
- level fit,
- current activity interruptibility,
- travel ETA,
- gear durability and consumables,
- class utility,
- progression opportunity,
- human preference,
- recent failure count for that bot/activity pair.

Hard gates remove candidates. Scoring chooses among legal candidates. This
prevents a high-level bot from repeatedly filling low-value activity slots when
a level-appropriate bot can progress there.

## Pathfinding And Scene Data Scale

Target decision: PathfindingService and SceneDataService should be horizontally
scalable, but clients should see one logical endpoint.

Required capabilities:

- generated route-pack cache for static high-traffic legs,
- route-pack prewarm from the activity catalog,
- route request de-duplication for identical in-flight requests,
- cancellation and deadline propagation,
- per-map and per-tile nav-data signatures,
- dynamic overlay compatibility checks,
- path result cache keyed by map, capsule, policy, start/end projection, and
  dynamic overlay signature,
- scene tile cache with bounded memory and per-map eviction,
- batch physics stepping for many BG clients,
- request budgets and queue metrics.

Initial topology:

- one `wwow-pathfinding` and one `wwow-scene-data` for development,
- optional N replicas behind a simple client-side shard key:
  `hash(accountName) % replicaCount`,
- route-pack manifests mounted read-only or generated on startup with warmup
  metrics.

Final topology:

- Pathfinding replicas shard by account or map.
- Hot map/corridor packs are warmed before opening the activity queue.
- ActivityScheduler can ask for route estimates in batches before selecting
  bots.
- SceneDataService exposes cheap static object and transport stop queries so
  StateManager does not scrape live clients for static facts.

## Metrics

WWoW should use `System.Diagnostics.Metrics` as the in-process API and expose
Prometheus-compatible metrics through OpenTelemetry where HTTP endpoints exist.
For non-HTTP socket services, add a small metrics endpoint or sidecar endpoint.
The WoWStateManagerUI Dashboard consumes metrics through StateManager summary
APIs first, and may optionally read Prometheus/OTLP-backed APIs later. The UI
may emit UI-local metrics, but those are separate from living-server authority.

Metric naming:

```text
wwow.<service>.<domain>.<name>
```

Required common labels:

- `service`,
- `instance`,
- `environment`,
- `account`,
- `bot_role`,
- `activity`,
- `location`,
- `level_range`,
- `map`,
- `result`,
- `reason`.

Keep label cardinality bounded. Do not label by raw coordinates, exception
message, path request id, or player name unless the metric is explicitly
debug-only and disabled by default.

### Core Counters

| Metric | Type | Purpose |
|---|---|---|
| `wwow_statemanager_bot_launch_total` | counter | Bot launch attempts by result and mode |
| `wwow_statemanager_bot_disconnect_total` | counter | Disconnects by account, reason, and service |
| `wwow_statemanager_action_dispatch_total` | counter | Actions sent by action type and result |
| `wwow_activity_request_total` | counter | Human and automated activity requests |
| `wwow_activity_rejected_total` | counter | Activity legality failures by reason |
| `wwow_activity_completed_total` | counter | Activity completions by activity/location |
| `wwow_activity_failed_total` | counter | Activity failures by normalized reason |
| `wwow_progression_objective_selected_total` | counter | Planner decisions by objective type |
| `wwow_pathfinding_request_total` | counter | Path requests by result and source |
| `wwow_pathfinding_timeout_total` | counter | Native/route-pack timeout count |
| `wwow_pathfinding_routepack_hit_total` | counter | Route-pack hits by pack id and result |
| `wwow_pathfinding_routepack_bypass_total` | counter | Route-pack bypass by reason |
| `wwow_scenedata_request_total` | counter | Scene queries by query type and result |
| `wwow_bot_task_started_total` | counter | BotTask starts by task type |
| `wwow_bot_task_failed_total` | counter | BotTask failures by normalized reason |
| `wwow_transport_missed_total` | counter | Missed boat/zeppelin/elevator events |
| `wwow_socket_connect_total` | counter | Service socket connects by peer service |
| `wwow_socket_disconnect_total` | counter | Service socket disconnects by reason |
| `wwow_log_suppressed_total` | counter | Repeated log events suppressed by category |

### Histograms

| Metric | Type | Purpose |
|---|---|---|
| `wwow_activity_queue_duration_seconds` | histogram | Time from request to group ready |
| `wwow_activity_duration_seconds` | histogram | Runtime per activity |
| `wwow_pathfinding_duration_seconds` | histogram | Path request latency |
| `wwow_routepack_warmup_duration_seconds` | histogram | Startup warmup cost |
| `wwow_snapshot_ingest_duration_seconds` | histogram | Snapshot processing latency |
| `wwow_action_ack_duration_seconds` | histogram | Dispatch to ACK latency |
| `wwow_bot_login_duration_seconds` | histogram | Login time by mode |

### Gauges

| Metric | Type | Purpose |
|---|---|---|
| `wwow_bots_online` | gauge | Online bots by faction and mode |
| `wwow_bots_available` | gauge | Bots available for leases |
| `wwow_activity_active` | gauge | Active activities |
| `wwow_pathfinding_queue_depth` | gauge | Pending path requests |
| `wwow_scene_cache_entries` | gauge | Scene cache pressure |
| `wwow_snapshot_cache_entries` | gauge | StateManager snapshot cache size |

## Error Taxonomy

All failures should normalize to a small enum before metrics/logging:

- `path_timeout`
- `no_path`
- `routepack_bypass`
- `transport_missed`
- `transport_wrong_entry`
- `transport_boarding_failed`
- `map_transfer_timeout`
- `socket_connect_failed`
- `socket_disconnect_expected`
- `socket_disconnect_unexpected`
- `login_failed`
- `bot_crash`
- `task_timeout`
- `illegal_activity_request`
- `missing_role`
- `missing_attunement`
- `missing_key`
- `missing_flight_path`
- `inventory_full`
- `durability_broken`
- `server_capability_missing`

This taxonomy is more important than the first dashboard. It creates stable
time-series data so the most serious defects are visible.

## Logging Policy

Every container and service needs a logging profile:

```text
LoggingProfile {
  DefaultLevel,
  CategoryOverrides,
  DiagnosticCategories,
  BurstSuppression,
  CorrelationFields,
  ContainerDriver,
  Retention
}
```

Required structured fields:

- `service`
- `instance`
- `account`
- `character`
- `activity_id`
- `activity`
- `location`
- `map`
- `task`
- `path_request_id`
- `transport_entry`
- `correlation_id`
- `result`
- `reason`

Noise reduction rules:

- Repeated connect/disconnect messages should be Debug when they are expected
  lifecycle churn and Warning only when they exceed a rate threshold or carry an
  unexpected reason.
- Pathfinding diagnostic route dumps are disabled by default and enabled by
  category or sampled by failure.
- Snapshot dumps are disabled by default and replaced by compact structured
  state summaries.
- Docker containers use rotated logs. Prefer Docker `local` logging driver
  where supported; otherwise configure `json-file` `max-size` and `max-file`.
- Service startup logs should include version, git SHA, config profile,
  data-root signature, and exposed endpoints once.

Container evaluation checklist:

| Container | Logging decision needed |
|---|---|
| `wow-realmd` | Route logs to mounted storage, suppress normal auth heartbeat noise, retain auth failures |
| `wow-mangosd` | Keep world errors, SOAP command failures, DB reconnects; suppress routine movement/chat spam |
| `wwow-pathfinding` | Keep startup nav signatures, route-pack warmup summary, timeout/errors; gate route dumps |
| `wwow-scene-data` | Keep cache load and fatal data errors; sample high-volume tile queries |
| `background-bot-runner` | Keep login/task/action failures; suppress normal snapshot heartbeat |
| `WoWStateManager` | Keep bot lifecycle, leases, activity state, dispatch failures; normalize connect/disconnect churn |

## Config Surface

Minimum configuration needed:

```json
{
  "LivingServer": {
    "EnableAutomatedProgression": true,
    "EnableOnDemandActivities": true,
    "MaxConcurrentActivities": 20,
    "BotLeaseDefaultMinutes": 90,
    "PreferSingleStateManager": true
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
  }
}
```

## Data Products

The spec requires these generated artifacts:

1. `ActivityCatalog.json` - generated from `docs/leveling-guide/`.
2. `ProgressionGraph.json` - generated from unlock graph, class files,
   attunements, professions, reputations, and raids.
3. `RoutePackManifest.json` - generated from travel graph hot edges and
   activity entry routes.
4. `ServerCapabilityManifest.json` - declares which VMaNGOS features are
   available.
5. `MetricsCatalog.md` - generated or checked from code meters.
6. `LoggingProfileCatalog.md` - lists category levels and diagnostics toggles.
7. `DashboardMetricsView.json` - StateManager-owned summary model optimized for
   the WoWStateManagerUI Dashboard tab.
8. `ActivityConfigSchema.json` - schema for UI/API validation and editing of
   on-demand activity configs.

## Implementation Phases

### Phase 0 - Spec hardening

- Add this spec.
- Convert the activity table into an initial `ActivityCatalog.json`.
- Add tests that validate catalog rows have legal `Activity`, `Location`, and
  `LevelRange`.

### Phase 1 - Observability foundation

- Add a tiny metrics registry wrapper over `System.Diagnostics.Metrics`.
- Add common meters to StateManager, BotRunner, PathfindingService, and
  SceneDataService.
- Expose `/metrics` where services have HTTP hosting; add a tiny metrics
  endpoint for socket-only services.
- Add Docker logging options and service logging profiles.
- Normalize connect/disconnect logs and counters.

### Phase 2 - Activity registry and legality

- Add `ActivityDefinition` models.
- Generate/load the activity catalog.
- Add legality validation and structured rejection.
- Add deterministic tests for representative legal/illegal requests.
- Add StateManager APIs for loading, validating, saving, enabling, and
  disabling activity configs.

### Phase 3 - Bot lease scheduler

- Add bot leases and return objectives.
- Add bot selection scoring.
- Add single-StateManager coordination for dungeon and BG requests.
- Add activity status API.
- Add Dashboard summary API for metrics, activities, leases, and top errors.

### Phase 3.5 - WoWStateManagerUI Dashboard

- Extend the existing Dashboard tab with metrics panels.
- Add activity catalog loading and config editing views inside the Dashboard
  tab.
- Show authoritative StateManager validation results before save or launch.
- Add UI tests for activity config view-model validation, load/save command
  state, and dashboard metric mapping.

### Phase 4 - Automated progression loop

- Convert leveling-guide priority bands into planner policy.
- Add progression objective selection metrics and traces.
- Integrate account-level roster goals.
- Make progression interruptible by human leases.

### Phase 5 - Pathfinding and scene scale

- Prewarm route packs from activity catalog hot paths.
- Add path request de-duplication and queue metrics.
- Add pathfinding/scene-data replica support behind client-side sharding.
- Add batch route-estimate API for scheduler candidate scoring.

### Phase 6 - Living server load

- Run staged load tests:
  - 50 bots,
  - 200 bots,
  - 500 bots,
  - 1000 bots,
  - 3000 bots.
- Gate each step on snapshot latency, path latency, disconnect rate, activity
  success rate, CPU, memory, and log volume.

## Acceptance Criteria

The living-server spec is satisfied when:

- A human can list on-demand activities by activity/location/level range.
- WoWStateManagerUI Dashboard can load, edit, validate, save, enable, disable,
  request, and cancel on-demand activity configs through StateManager APIs.
- A human can request at least one dungeon, one battleground, one zone questing
  session, and one profession/economy activity.
- StateManager selects legal bots, forms the activity, tracks status, and
  returns bots to progression.
- Automated progression continues without human requests.
- Metrics expose activity failures, path failures, disconnects, task failures,
  and latency histograms, and the Dashboard tab renders the operator-critical
  subset.
- Docker logs are rotated and normal operation is quiet enough that warnings
  mean something.
- The highest-volume path and scene requests have caches, route packs, or
  sharding plans with measured queue depth and latency.

## Open Decisions

1. Should `ActivityCatalog.json` be generated at build time from markdown or
   checked in as source of truth with markdown as commentary?
2. Should the Dashboard read metrics only through StateManager summary APIs, or
   should it also support direct Prometheus HTTP API queries for advanced users?
3. Should Prometheus scraping be first-class in Docker Compose now, or should
   services expose `/metrics` first and leave Prometheus/Grafana compose files
   for the next slice?
4. How many bots can one `BackgroundBotRunner` process host after singleton
   removal, and is that required before activity scheduling is useful?
5. Which activities are legal for a human in a mixed FG/BG group when the human
   is not one of the managed bot accounts?
