# Spec 10 — Metrics

## Authority

`System.Diagnostics.Metrics` is the in-process API. OpenTelemetry exports
to Prometheus. The WPF Dashboard reads aggregated values through
StateManager summary APIs (per decision-of-record #2).

## Naming

```
wwow.<service>.<domain>.<name>
```

- `service` ∈ {`statemanager`, `botrunner`, `pathfinding`, `scenedata`,
  `decisionengine`, `prompt`, `ui`, `tests`}.
- `domain` is a logical area: `bot`, `activity`, `lease`, `path`,
  `scene`, `task`, `snapshot`, `socket`, `log`, `physics`.
- `name` is the metric.

Examples: `wwow.statemanager.activity.completed_total`,
`wwow.pathfinding.path.duration_seconds`.

## Required common labels

Every metric carries:

- `service` — emitting service.
- `instance` — process instance id (host + pid).
- `environment` — `dev` | `staging` | `prod`.

Activity-related metrics additionally carry:

- `activity` — catalog `Activity` (e.g. `Dungeon`).
- `location` — catalog `Location` (e.g. `WailingCaverns`).
- `level_range` — catalog `LevelRange` (e.g. `17-24`).
- `activity_id` — catalog `Id` for high-cardinality drill-down.

Bot-related metrics additionally carry:

- `account` — only on metrics with bounded per-account cardinality.
- `bot_role` — `Tank` | `Healer` | `Dps` | `Support`.
- `bot_mode` — `Foreground` | `Background`.
- `faction` — `Horde` | `Alliance`.

Failure metrics additionally carry:

- `result` — `success` | `failure` | `cancelled` | `timeout`.
- `reason` — from the normalized error taxonomy
  (see [`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md)).

## Cardinality budget

| Label | Max distinct values | Notes |
|---|---|---|
| `service` | 10 | Bounded by service count |
| `instance` | 200 | Bounded by process count |
| `environment` | 4 | dev/staging/prod/test |
| `activity` | 20 | Catalog family list |
| `location` | 100 | Catalog locations |
| `level_range` | 12 | Bracket bins |
| `activity_id` | 200 | Catalog row count |
| `account` | 5000 | Bot roster cap |
| `bot_role` | 4 | |
| `bot_mode` | 2 | |
| `faction` | 2 | |
| `result` | 4 | |
| `reason` | 30 | Taxonomy size |
| `map` | 100 | Map IDs |

**Never label** raw coordinates, exception messages, path request ids
(except in debug-only metrics gated by
`HighCardinalityDebugMetrics`), or player names.

## Required counters

| Metric | Purpose |
|---|---|
| `wwow.statemanager.bot.launch_total` | Bot launches by result + mode |
| `wwow.statemanager.bot.disconnect_total` | Disconnects by reason + account |
| `wwow.statemanager.action.dispatch_total` | Actions sent by action type + result |
| `wwow.statemanager.snapshot.ingest_total` | Snapshots processed by result |
| `wwow.statemanager.activity.request_total` | Activity requests by source (human/auto) |
| `wwow.statemanager.activity.rejected_total` | Legality failures by reason |
| `wwow.statemanager.activity.completed_total` | Completions by activity/location |
| `wwow.statemanager.activity.failed_total` | Failures by reason |
| `wwow.statemanager.lease.reserved_total` | Lease reservations |
| `wwow.statemanager.lease.released_total` | Lease releases by outcome |
| `wwow.statemanager.lease.reclaimed_total` | Forced releases (timeout/crash) |
| `wwow.botrunner.task.started_total` | Task starts by task type |
| `wwow.botrunner.task.failed_total` | Task failures by reason |
| `wwow.botrunner.action.acked_total` | ACKs sent by action type |
| `wwow.botrunner.transport.missed_total` | Missed boat/zeppelin/elevator |
| `wwow.pathfinding.path.request_total` | Path requests by result + source |
| `wwow.pathfinding.path.timeout_total` | Timeouts |
| `wwow.pathfinding.routepack.hit_total` | Route-pack hits by pack id + result |
| `wwow.pathfinding.routepack.bypass_total` | Route-pack bypass by reason |
| `wwow.scenedata.request_total` | Scene queries by query type + result |
| `wwow.socket.connect_total` | Service socket connects |
| `wwow.socket.disconnect_total` | Service socket disconnects by reason |
| `wwow.log.suppressed_total` | Log events suppressed by category |
| `wwow.physics.parity_break_total` | FG/BG parity break by kind |

## Required histograms

| Metric | Purpose |
|---|---|
| `wwow.statemanager.snapshot.ingest_duration_seconds` | Snapshot processing latency |
| `wwow.statemanager.action.ack_duration_seconds` | Dispatch → ACK latency |
| `wwow.statemanager.activity.queue_duration_seconds` | Request → group ready |
| `wwow.statemanager.activity.duration_seconds` | Runtime per activity |
| `wwow.botrunner.task.duration_seconds` | Task wall time |
| `wwow.botrunner.login.duration_seconds` | Login time by mode |
| `wwow.pathfinding.path.duration_seconds` | Path request latency |
| `wwow.pathfinding.routepack.warmup_duration_seconds` | Startup warmup cost |
| `wwow.scenedata.request.duration_seconds` | Scene query latency |
| `wwow.physics.parity.dz` | FG/BG settle-Z delta per checkpoint |

## Required gauges

| Metric | Purpose |
|---|---|
| `wwow.statemanager.bots.online` | Online bots (faction/mode) |
| `wwow.statemanager.bots.available` | Bots available for leases |
| `wwow.statemanager.activity.active` | Active activities |
| `wwow.statemanager.lease.active` | Active leases |
| `wwow.pathfinding.queue.depth` | Pending path requests |
| `wwow.pathfinding.routepack.entries` | Route packs loaded |
| `wwow.scenedata.cache.entries` | Scene cache pressure |
| `wwow.statemanager.snapshot.cache_entries` | Snapshot cache size |

## Long-term history requirement

Per 2026-05-12 design (Q19): the operator wants to see how the
automated bots have been doing **over the last few hours** —
progression, combat, economics, travel. The UI's "Long-term
performance history" panel
([`Spec/09_UI.md`](09_UI.md#long-term-performance-history-panel))
renders this.

Required time-series resolutions:

- 1-minute samples for the last 6 hours (rolling).
- 1-hour samples for the last 7 days (rolling).
- 1-day samples for the last 90 days (rolling).

This is what Prometheus + Grafana ship by default; we just configure
retention. StateManager exposes its own in-process counters via the
summary APIs so the UI never has to query Prometheus directly.

Domain-specific aggregates (operator-critical):

| Aggregate | Source metric | Window |
|---|---|---|
| XP/hour gained per faction × level band | `wwow.statemanager.bot.xp_delta_total` | rolling 1h |
| Gold earned per bot per hour | `wwow.statemanager.bot.gold_delta_total` | rolling 1h |
| Gear-acquisition rate | `wwow.statemanager.bot.gear_acquired_total` | rolling 6h |
| Kills per hour per spec | `wwow.statemanager.combat.kill_total` | rolling 1h |
| Death rate per zone | `wwow.statemanager.combat.death_total` | rolling 6h |
| AH listing count over time | `wwow.statemanager.ah.listings` | rolling 24h |
| Average travel-leg duration | `wwow.statemanager.travel.leg_duration_seconds` | rolling 1h |
| Stuck incidents | `wwow.statemanager.stuck_total` | rolling 24h |
| mangosd CPU/memory | exporter (cAdvisor or node_exporter) | rolling 1h |

The UI panel pulls these via `StateManager.GetPerformanceHistory(window)`.

## Endpoint

Each HTTP-capable service exposes `/metrics` (Prometheus text format).
Socket-only services (`PathfindingService`, `SceneDataService`,
`BackgroundBotRunner`) host a tiny in-process HTTP listener for
`/metrics` (port `<base> + 1000`; e.g. PFS = 5001, metrics = 6001).

A Prometheus scrape config ships in
`docker/prometheus/prometheus.yml`. Grafana dashboards live in
`docker/grafana/dashboards/`.

## Required derived counters for the history panel

These are populated by `WoWStateManager` from snapshot deltas (not the
bot processes — keeps cardinality bounded):

```
wwow.statemanager.bot.xp_delta_total{faction, level_band, spec}
wwow.statemanager.bot.gold_delta_total{account}
wwow.statemanager.bot.gear_acquired_total{slot, source}    // source ∈ {vendor, ah, loot, mail}
wwow.statemanager.combat.kill_total{spec, zone}
wwow.statemanager.combat.death_total{zone, cause}
wwow.statemanager.ah.listings                                 // gauge
wwow.statemanager.travel.leg_duration_seconds{leg_type}      // histogram
wwow.statemanager.stuck_total{zone}
```

These are derived counters, not raw — they aggregate snapshot deltas at
StateManager. The bots themselves do NOT emit per-tick XP/gold metrics
(would explode cardinality at 3000 bots).

## Suppression and sampling

- **HighCardinalityDebugMetrics** is the master switch for any metric
  with `account`, `request_id`, `correlation_id`, etc. on its labels.
  Default off.
- **Sampled metrics** declare their sampling ratio in the metric name
  via tag `sample_ratio=0.01`. Implementation uses Reservoir
  sampling.

## Dashboard summary API mapping

The Dashboard panels read aggregated values from StateManager summary
APIs, not directly from Prometheus. Mapping:

| Panel | API | Metrics aggregated |
|---|---|---|
| Service health | `GetServiceHealth` | Last seen / git_sha / health endpoint |
| Bot population | `GetBotsSummary` | `wwow.statemanager.bots.online` (filtered) |
| Activities | `GetActivitiesSummary` | `*.activity.*` (counts + queue) |
| Leases | `GetLeasesSummary` | `*.lease.*` |
| Pathfinding | `GetPathfindingSummary` | `wwow.pathfinding.*` (rate + P99) |
| Error triage | `GetErrorsSummary` | `*_total{result="failure"}` grouped by reason |
| Logging health | `GetLoggingHealth` | `wwow.log.suppressed_total` |

Power users can query Prometheus / Grafana directly using the names
above. The UI does not embed Grafana.

## Existing code anchors

| Concept | File |
|---|---|
| Metrics registry wrapper | (to be added) `Exports/Telemetry/MetricsRegistry.cs` |
| Service defaults | `UI/Systems/Systems.ServiceDefaults/` |
| Prometheus config | (to be added) `docker/prometheus/prometheus.yml` |
| Grafana dashboards | (to be added) `docker/grafana/dashboards/` |
