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

## Anomaly detection

The metric surface above gives us raw signals. **Anomaly detection** is
the layer that turns those signals into actionable alerts: a bot whose
XP/hr drops 4× below its level-band median, a `wwow.physics.parity_break_total`
spike, an unexpected `wwow.pathfinding.path.timeout_total` ramp, a
`wwow.statemanager.activity.failed_total` cluster on a single map.

### New metrics

```
wwow.statemanager.anomaly.detected_total{kind, severity, source}
wwow.statemanager.anomaly.active                                  // gauge
wwow.statemanager.anomaly.suppressed_total{kind, reason}          // when an anomaly was detected but masked
wwow.statemanager.anomaly.resolved_total{kind}                    // operator-acknowledged
```

### `AnomalyKind` enum (closed set; proto-mirrored)

```csharp
public enum AnomalyKind
{
    Unknown                       = 0,
    XpRateBelowMedian             = 1,  // bot's xp/hr < median * threshold for level band
    GoldRateBelowMedian           = 2,
    KillRateBelowMedian           = 3,
    DeathRateAboveMedian          = 4,
    StuckRateAboveMedian          = 5,
    PathTimeoutSpike              = 6,
    PhysicsParityBreakCluster     = 7,
    ActivityFailureCluster        = 8,  // > N failures of same activity in M minutes
    AdvisorTimeoutSpike           = 9,  // DecisionEngine NoAdvice rate above threshold
    AhListingsZeroGrowth          = 10, // economy AH listings flat for > M minutes
    ChatBudgetExhaustionPattern   = 11, // multiple bots hit per-channel cap simultaneously
    LoadoutStuckPattern           = 12, // > N bots stuck in LOADOUT_IN_PROGRESS for > M minutes
    SnapshotIngestLatencyP99Spike = 13,
    OndemandPoolExhaustionPattern = 14, // repeated RejectionCode.POOL_EXHAUSTED
    RosterDistanceRegression      = 15, // outcome.roster_distance_delta > 0 (anti-progressive)
}

public enum AnomalySeverity
{
    Info     = 0,  // log only
    Warning  = 1,  // surface to UI; operator can acknowledge
    Critical = 2,  // page on-call (when configured)
}

public enum AnomalySource
{
    RuleEngine = 0,
    OnnxModel  = 1,
}
```

`AnomalyKind` is mirrored to proto for the snapshot projection. New
values are proto-additive.

### `Anomaly` event surface

```protobuf
message Anomaly {
    AnomalyKind     kind             = 1;
    AnomalySeverity severity         = 2;
    AnomalySource   source           = 3;
    uint64          detected_at_ms   = 4;
    string          subject          = 5;   // account name | activity_id | map_id | empty
    string          metric_name      = 6;   // e.g. "wwow.statemanager.bot.xp_delta_total"
    float           observed_value   = 7;
    float           expected_value   = 8;
    float           threshold        = 9;
    string          rationale        = 10;  // operator-facing
    string          correlation_id   = 11;  // for log/trace correlation
    bool            acknowledged     = 12;
    uint64          acknowledged_at_ms = 13;
}
```

### Snapshot projection

Operator-visible anomalies surface on `WoWActivitySnapshot` via field 47
(continues after Spec/05's field 46):

```protobuf
repeated Anomaly active_anomalies = 47;   // ring buffer cap 16 — UI consumes for the Error triage panel
```

Tests assert on `snapshot.active_anomalies[]` (Test Isolation Rules);
direct reads of `AnomalyDetector` internal state are reserved to the
service that *produces* the projection.

## Failure-reason mapping

Anomaly-detection failures map onto [`Spec/12`](12_ERROR_TAXONOMY.md):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| Anomaly detector process down | `server_unavailable` | UI shows "anomaly detector offline" badge; no anomalies emitted |
| ONNX model load failure (Phase 3) | `catalog_invalid` | fall back to Phase-1 rules |
| Metric exporter scrape failure | `socket_disconnect_unexpected` | Prometheus alert; no anomaly emission until recovery |
| False-positive flood (rules misconfigured) | (no FailureReason; operator silences via UI) | Severity flips to `Info`; counter still increments |

No new Spec/12 values needed.

## ML integration — Anomaly detection

**Surface.** Anomaly detection is **observational ML**, not decision
ML — it watches the metric stream and emits `Anomaly` events; it does
NOT consume the Spec/20 advisor RPC surface (those are decision RPCs).
But it consumes the **Spec/20 §6 trace pipeline** as labeled-data
input. Three maturity phases mirror the Spec/20 §5 pattern:

| Phase | Source | `AnomalySource` |
|---|---|---|
| 1 — Heuristic | Per-metric static thresholds in `Config/anomaly-thresholds.json` (e.g. "xp/hr < median × 0.25 → XpRateBelowMedian.Warning") | `AnomalySource.RuleEngine` |
| 2 — Rules + lookup | Per-`(level_band, spec)` thresholds learned off-line from baseline traces; emitted as updated thresholds JSON | `AnomalySource.RuleEngine` |
| 3 — ONNX | `Services/DecisionEngineService/Models/anomaly/v1.onnx` is a time-series outlier model (e.g. isolation forest exported via skl2onnx) over rolling windows of `(metric_name, level_band, value)` tuples | `AnomalySource.OnnxModel` |

The Phase-3 model lives **inside** `DecisionEngineService` for
process-management reuse, but it does NOT extend the
`IDecisionEngineClient` interface — there is no per-tick `Get<...>Advice`
call. Instead, a periodic (1 Hz) `IAnomalyDetector` worker inside
DecisionEngineService scans the local metric subscriber + Spec/20 §6
trace stream and pushes detected anomalies into `Anomaly` events on the
`WoWStateManager` snapshot bus.

**Input feature vector** (Phase 3):

| Feature | Source |
|---|---|
| metric_name one-hot[64] | from the §Required-counters / histograms inventory |
| level_band one-hot[12] | bracket bins |
| value (last 5 1-minute samples) | Prometheus scrape |
| trace_outcome_completion_rate (rolling 5 min) | `tmp/test-runtime/traces/*/outcome.jsonl` aggregation |
| advice_log_count_total | snapshot.advice_log[] aggregation |
| ondemand_active_count | snapshot.ondemand_instances[] |

**Output shape.** A single `(AnomalyKind, AnomalySeverity, observed,
expected, threshold)` tuple per detected outlier. Emitted as an
`Anomaly` event AND incremented on
`wwow.statemanager.anomaly.detected_total`.

**Fail-soft fallback.** If Phase-3 ONNX is unavailable, the detector
runs Phase-1 rules from `Config/anomaly-thresholds.json`. If the
config file is also missing, the detector emits nothing — silent
fallback, since false negatives are preferred over false positives in
the alerting domain.

**Live-validation guard.** Replaying a healthy production trace
through the detector with all phases enabled MUST NOT emit any
`AnomalyKind.RosterDistanceRegression` events for outcomes that are
genuinely progressive (`outcome.roster_distance_delta ≤ 0`). The
detector cannot flip a healthy bot to anomalous; only genuine
regression triggers it. Asserted by
`AnomalyDetector_HealthyTraceProducesNoRegressionAnomaliesTest` in
the test surface.

## Dynamic-progressive invariant

Anomaly detection is the **observational guard** on the loop's
dynamic-progressive invariant. The detector's job is precisely to
fire when the invariant breaks:

- **AnomalyKind.RosterDistanceRegression** fires when
  `outcome.roster_distance_delta > 0` is observed (anti-progressive
  completion). This is the canary for any ML pass that drifts the
  population away from goals.
- **AnomalyKind.XpRateBelowMedian** / `GoldRateBelowMedian` /
  `KillRateBelowMedian` fire when bots fall behind their level-band
  median (the *expected* rate of progression).

Tests assert that the detector preserves these invariants:

1. **Dynamic.** Healthy bots at different level bands have different
   median rates; an anomaly detector that fires on level-15 bots' XP/hr
   when applied to level-50 bots is mis-configured. The detector MUST
   bucket by `level_band` before applying thresholds.
2. **Progressive.** A healthy trace (every outcome has
   `roster_distance_delta ≤ 0`) MUST produce zero
   `RosterDistanceRegression` anomalies. Conversely, an injected
   anti-progressive trace MUST trigger the detection.

Asserted at the test surface by
`Anomaly_DynamicProgressive_HealthyTraceClearMatchesRegressionTraceTriggers`.

## Plan-slot cross-reference

| Slot | Owns | Section |
|---|---|---|
| **(no slot yet — Plan follow-up)** | `Services/DecisionEngineService/Anomaly/AnomalyDetector.cs`, `Anomaly/AnomalyThresholds.cs`, `Config/anomaly-thresholds.json` | §Anomaly detection (production) |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that feeds Phase 2/3 baseline learning | §ML Phase 2/3 input |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | Snapshot projection — extend with `active_anomalies` field 47 | §Snapshot projection |
| (UI side) [`Plan/06`](../Plan/06_PHASE5_OBSERVABILITY.md) | Error triage panel renders `active_anomalies[]` | §Dashboard summary API mapping above |

The "no slot yet" row joins the Plan-follow-up roster (six orphan
services through pass 11; `AnomalyDetector` makes seven).

## Test surface

Contract tests live at
`Tests/BotRunner.Tests/Metrics/AnomalyDetectionContractTests.cs`.
Assertions go through `snapshot.active_anomalies[]` (field 47) per Test
Isolation Rules.

- **`AnomalyKindEnum_ContainsExpectedKindsAndIsClosedSet`** — the
  `AnomalyKind` enum has the 16 named values from §Anomaly detection
  and no unnamed gaps in the proto encoding.
- **`AnomalyDetector_StaticThresholdRulesFireOnSyntheticBadInput`** —
  given a synthetic metric stream where `wwow.statemanager.bot.xp_delta_total`
  for a level-15 bot is 4× below the configured median threshold, the
  next snapshot's `active_anomalies[]` contains exactly one
  `AnomalyKind.XpRateBelowMedian` entry with severity `Warning`.
- **`AnomalyDetector_LevelBandBucketing_NoCrossBandFalsePositive`** —
  a level-50 bot at typical level-50 XP/hr produces NO anomaly even
  when the level-15 median threshold would flag it.
- **`AnomalyDetector_HealthyTraceProducesNoRegressionAnomaliesTest`** —
  replaying a production trace where every `outcome.roster_distance_delta`
  is ≤ 0 produces ZERO `RosterDistanceRegression` anomalies.
- **`AnomalyDetector_RegressionTraceTriggersRegressionAnomaly`** —
  conversely, an injected trace with an `outcome.roster_distance_delta
  = +0.05` line triggers exactly one `RosterDistanceRegression`
  anomaly with `expected_value <= 0`, `observed_value == 0.05`.
- **`AnomalyDetector_FailSoftOnConfigMissing`** — when
  `Config/anomaly-thresholds.json` is absent, the detector emits no
  anomalies (silent fallback per §ML integration).
- **`Anomaly_DynamicProgressive_HealthyTraceClearMatchesRegressionTraceTriggersTest`** —
  the dynamic-progressive invariant. The same detector configuration,
  fed a healthy trace, produces zero `RosterDistanceRegression` events;
  fed an injected anti-progressive trace, produces ≥ 1. Asserts the
  detector is sensitive *only* to the invariant violation, not to
  metric noise.

## Existing code anchors

| Concept | File |
|---|---|
| Metrics registry wrapper | (to be added) `Exports/Telemetry/MetricsRegistry.cs` |
| Service defaults | `UI/Systems/Systems.ServiceDefaults/` |
| Prometheus config | (to be added) `docker/prometheus/prometheus.yml` |
| Grafana dashboards | (to be added) `docker/grafana/dashboards/` |
