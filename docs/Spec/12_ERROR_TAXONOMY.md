# Spec 12 — Error Taxonomy

## Purpose

A small fixed enum to which every failure normalizes before it touches
metrics or logs. Stable time-series come from stable reason labels.

## The enum

```csharp
public enum FailureReason
{
    // --- pathfinding / scene ---
    path_timeout,
    no_path,
    routepack_bypass,
    routepack_invalidated,
    scene_data_missing,
    scene_data_stale,

    // --- transport ---
    transport_missed,
    transport_wrong_entry,
    transport_boarding_failed,
    transport_disembark_failed,

    // --- map transfer ---
    map_transfer_timeout,
    map_transfer_rejected,

    // --- socket / IPC ---
    socket_connect_failed,
    socket_disconnect_expected,
    socket_disconnect_unexpected,
    socket_frame_truncated,
    handshake_mismatch,

    // --- bot lifecycle ---
    login_failed,
    auth_proof_failed,
    realm_select_failed,
    char_select_failed,
    enter_world_failed,
    bot_crash,
    bot_disconnected,

    // --- physics ---
    physics_parity_break,
    physics_underground,
    physics_overhead_snap,
    physics_stuck,

    // --- task execution ---
    task_timeout,
    task_precondition_failed,
    task_cancelled,
    task_unrecoverable,

    // --- activity legality ---
    illegal_activity_request,
    missing_role,
    missing_attunement,
    missing_key,
    missing_flight_path,
    missing_reputation,
    missing_level,
    lockout_active,
    faction_unreachable,

    // --- inventory / gear ---
    inventory_full,
    durability_broken,
    item_unavailable,

    // --- server ---
    server_capability_missing,
    server_unavailable,
    server_rejected,

    // --- catalog ---
    catalog_drift,
    catalog_invalid,

    // --- AOTA runtime (Spec/19 §8 mapping; added by spec-fill-loop pass 15) ---
    objective_end_state_unreachable,

    // --- social fabric (Spec/21 §10; added by spec-fill-loop pass 15) ---
    mail_recipient_invalid,
    chat_denylist_rejection,
    social_channel_join_failed,

    // --- world cycles (Spec/22 §10; added by spec-fill-loop pass 15) ---
    world_buff_window_missed,

    // --- OnDemand launcher (Spec/23 §11; added by spec-fill-loop pass 15) ---
    ondemand_stage_timeout,
}
```

The enum is in `Exports/GameData.Core/Enums/FailureReason.cs`.

## Mapping rules

- **Map every catch / failure path to one of these** at the boundary
  (the point where the failure becomes a metric or log line).
- **Never invent a new reason** in code; add it to the enum first.
- **`Detail` is the human-readable string.** It does not appear in
  metric labels; it appears in log messages.

```csharp
throw new BotTaskFailedException(
    FailureReason.transport_missed,
    detail: $"Zeppelin OG→UC departed at {ts:HH:mm:ss} before boarding window opened");
```

## Adding a new reason

1. Edit `FailureReason.cs` to add the new value.
2. Update this doc.
3. Add a mapping in any code that previously surfaced this failure as
   a string.
4. Update test coverage to assert the new reason on the expected path.
5. Bump the catalog test that enforces 1:1 mapping between enum and
   doc.

## Reason vs. result

`result` is `success | failure | cancelled | timeout`. `reason` is set
only when `result != success`. Metrics carry both labels.

## Per-reason mapping table

For each new value added by the spec-fill loop, the originating spec
section is the authoritative source for *when* the reason fires:

| Reason | Owning spec | Section | Detail-string convention |
|---|---|---|---|
| `objective_end_state_unreachable` | [`Spec/19 §8`](19_AOTA_RUNTIME.md#8-failure-reason-mapping) | Objective-end-state predicate cannot be satisfied for current snapshot | `"<activity_id>:<objective_id> EndState=<label>"` |
| `mail_recipient_invalid` | [`Spec/21 §10`](21_SOCIAL_FABRIC.md#10-failure-reason-mapping) | `MAIL_ERR_RECIPIENT_NOT_FOUND` (server code 37) OR recipient on denylist | `"recipient=<name> reason=<server_code|denylist>"` |
| `chat_denylist_rejection` | [`Spec/21 §10`](21_SOCIAL_FABRIC.md#10-failure-reason-mapping) | `ChatPostFilter` regex trip post-template-substitution | `"channel=<channel> trip_pattern=<regex_index>"` |
| `social_channel_join_failed` | [`Spec/21 §10`](21_SOCIAL_FABRIC.md#10-failure-reason-mapping) | Channel join exponential-backoff cap exceeded | `"channel=<channel> attempts=<n>"` |
| `world_buff_window_missed` | [`Spec/22 §10`](22_WORLD_CYCLES.md#10-failure-reason-mapping) | Bot chose to pivot toward a buff source and arrived after the decay window closed | `"buff=<name> arrived_at=<ts> window_close=<ts>"` |
| `ondemand_stage_timeout` | [`Spec/23 §11`](23_ONDEMAND_API.md#11-failure-reason-mapping) | `OnDemandActivityStageEvent.staging_timeout_secs` exceeded for any stage | `"instance=<id> stage=<Stage> elapsed=<ms>"` |

The seventh deferred value `objective_decision_engine_rejected` (Spec/19
§8 follow-up) is **not added** this pass — it only matters if Phase-3
ML ever returns a hard veto (a `confidence < 0` sentinel), which is
implementation-deferred per [`Spec/20 §3`](20_DECISION_ENGINE.md#3-advice-shapes).
Re-evaluate when ML phase 3 ships.

## Failure-cause clustering — ML integration

**Surface.** Failure-cause clustering is **observational ML** (same
pattern as anomaly detection in [`Spec/10 §Anomaly detection`](10_METRICS.md#anomaly-detection)) — it consumes
the Spec/20 §6 trace pipeline + the `wwow.*_total{result="failure",reason=...}`
metric stream and emits *clusters* of failures that share a root cause
beyond what the static `FailureReason` enum can express. It does NOT
extend the seven Spec/20 advisor RPCs.

**Why clustering matters here.** The `FailureReason` enum is
deliberately coarse (52 values total post pass 15). Detail strings
carry the fine-grained context but are unbounded cardinality. A
clustering pass over `(reason, detail)` tuples produces operator-
visible groupings like:

- `(transport_missed, "zeppelin OG→UC departed before boarding")` × 47
  failures last hour → cluster `Transport.OgUcZeppelinTimingDrift`.
- `(physics_parity_break, "BG settle Z=42.29 FG=53.32")` × 12 → cluster
  `Physics.OgCliffFallParity`.
- `(task_timeout, "fishing pool not found at Ratchet")` × 8 → cluster
  `Resource.RatchetFishingDrought`.

Operators get an aggregate signal instead of 47 individual error logs.

### Three maturity phases (matches Spec/20 §5)

| Phase | Source | Output |
|---|---|---|
| 1 — Heuristic | Group by `(reason, detail-token-trigram-hash)` — bag-of-trigrams over the detail string, hashed into 256 buckets | `FailureCluster` with same-bucket failures |
| 2 — Rules + lookup | `Config/decision-engine/failure-clusters.json` declares known cluster patterns (regex over detail) with friendly names | Named clusters override Phase-1 bucket ids |
| 3 — ONNX | `Services/DecisionEngineService/Models/failure_cluster/v1.onnx` — sentence-embedding-style model trained on labeled traces; cosine-similarity clustering on the embedding space | Phase-3 clusters span across `reason` values when root cause overlaps |

**Input feature vector** (Phase 3):

| Feature | Source |
|---|---|
| reason one-hot[52] | `FailureReason` enum value |
| detail bag-of-trigrams[256] | hashed `detail` string |
| activity_id one-hot[200] | originating Activity (catalog) |
| zone_id | originating zone |
| time_of_day_bin one-hot[24] | hour bucket |
| nearby_failures_recent[8] | counts of other `reason` values in last 5 min for same bot |

**Output shape.** `FailureCluster { cluster_id, friendly_name,
member_failure_ids[], representative_detail, confidence }`. Emitted
on the `Anomaly` event surface (Spec/10) when a cluster grows past
`Config/anomaly-thresholds.json:failure_cluster_threshold` (default 5
members in a 10-minute window) — at which point an
`AnomalyKind.ActivityFailureCluster` event fires.

**Fail-soft fallback.** If clustering is offline, individual failures
still emit normally on `wwow.*_total{reason}` counters — operators
see raw errors, just without aggregation.

**Live-validation guard.** Replaying a synthetic trace with 47
identical-root-cause failures MUST produce exactly 1 `FailureCluster`
event (not 47 individual `Anomaly` events). Asserted at the test
surface by `FailureClustering_BackpressureSingleClusterPerRootCauseTest`.

## Dynamic-progressive invariant

The `FailureReason` enum is the **observational vocabulary** for
non-progressive events. The dynamic-progressive invariant from
[`Spec/19 §10`](19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant)
manifests here as:

1. **Dynamic.** Different bot contexts (class / level / faction /
   zone) MUST produce *different distributions* of `FailureReason`
   values when something goes wrong — a level-15 questing bot
   stalling produces `task_timeout` + `missing_attunement=no` while a
   level-60 raid candidate stalling produces `lockout_active` /
   `missing_attunement=yes`. Identical contexts produce identical
   reasons (deterministic mapping rules per the per-reason table).
2. **Progressive.** A `FailureReason` value should NEVER appear on an
   `outcome` line where `roster_distance_delta ≤ 0`. Failures
   correlate with anti-progression; if a bot's `outcome` shows
   *progress* AND *failure reason*, the mapping is mis-applied (likely
   the failure was recovered from, in which case `result="success"`
   and `reason` should be unset). Asserted via
   `FailureReason_DynamicProgressive_FailureReasonAbsentOnProgressiveOutcomesTest`.

## Plan-slot cross-reference

| Slot | Owns | Section |
|---|---|---|
| **(no slot yet — Plan follow-up)** | `Exports/GameData.Core/Enums/FailureReason.cs` (the actual enum file) | §The enum |
| **(no slot yet — Plan follow-up)** | `Services/DecisionEngineService/Anomaly/FailureClusterer.cs`, `Config/decision-engine/failure-clusters.json` | §Failure-cause clustering |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that produces `task_terminal` lines per Spec/20 §6.1 — Phase 2/3 clustering input | §ML integration |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | Emits `Anomaly { kind=ActivityFailureCluster, ... }` via the `active_anomalies` field 47 | §ML integration |

The "no slot yet" rows join the Plan-follow-up roster — eight orphan
services through pass 15 (six from pass 11 + AnomalyDetector from
pass 14 + FailureClusterer here).

## Test surface

Contract tests live at
`Tests/BotRunner.Tests/Spec/FailureReasonCatalogTests.cs` (existing
catalog test extended) plus
`Tests/BotRunner.Tests/Metrics/FailureClusteringContractTests.cs` (new).
All assertions go through `snapshot.active_anomalies[]` (field 47 per
Spec/10) and `wwow.*_total{reason}` counter values.

- **`FailureReasonCatalog_EnumValuesMatchDocTable`** — every value in
  `FailureReason` appears in this doc's §The enum + §Per-reason
  mapping table; every doc row references a real enum value. 1:1
  mapping enforced per §Adding a new reason step 5.
- **`FailureReasonCatalog_NewPass15ValuesPresent`** — the 6 values
  added by this pass (`objective_end_state_unreachable`,
  `mail_recipient_invalid`, `chat_denylist_rejection`,
  `social_channel_join_failed`, `world_buff_window_missed`,
  `ondemand_stage_timeout`) are present in the enum AND the doc table.
- **`FailureReasonMapping_TransportMissedDetailFormat`** — a synthetic
  `BotTaskFailedException(transport_missed, "...")` produces a metric
  label `reason=transport_missed` matching the detail-string convention
  in this doc.
- **`FailureClustering_BackpressureSingleClusterPerRootCauseTest`** —
  47 synthetic failures with identical `(reason, detail_pattern)`
  produce exactly 1 `Anomaly { kind=ActivityFailureCluster }` event;
  not 47.
- **`FailureClustering_ConfigDriftRulesOverridePhase1Buckets`** —
  when `Config/decision-engine/failure-clusters.json` declares a
  regex-based cluster with a friendly name, matching detail strings
  surface that name on the `Anomaly.subject` field instead of a
  Phase-1 bucket id.
- **`FailureClustering_FailSoftOnConfigMissing`** — when the cluster
  config is absent, individual failures still emit on
  `wwow.*_total{reason}` counters; no clustering, no crashes.
- **`FailureReason_DynamicProgressive_FailureReasonAbsentOnProgressiveOutcomesTest`** —
  the dynamic-progressive invariant. For any trace `outcome` line
  where `roster_distance_delta ≤ 0` AND `completion="complete"`, the
  same trace MUST NOT have a `task_terminal` line for the same
  Activity with a `reason` ≠ `null`. Failure + progress are mutually
  exclusive on a single completion.

## Existing code anchors

| Concept | File |
|---|---|
| Failure enum (to be added) | `Exports/GameData.Core/Enums/FailureReason.cs` |
| Exception type (to be added) | `Exports/GameData.Core/Exceptions/BotTaskFailedException.cs` |
| Test enforcement | `Tests/BotRunner.Tests/Spec/FailureReasonCatalogTests.cs` |
