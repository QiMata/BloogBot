# Spec 20 — DecisionEngineService

> **What this spec is.** The runtime contract for
> `Services/DecisionEngineService/` — the ML-augmented advisory layer
> that ships alongside but separate from the deterministic Activity /
> Objective / Task / Action stack. Phase 10
> ([`Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md))
> wires DecisionEngineService into the runtime composer and selectors.

## 1. Identity

DecisionEngineService is **advisory, not authoritative.** Every
recommendation returns to the BotRunner / StateManager caller for the
final decision; the caller may discard the advice without consequence.
This separation keeps the deterministic Activity stack testable while
the ML layer evolves.

Three call sites consume DecisionEngine advice:

1. **`IActivityComposer.Compose(...)`** — the planner's Objective
   sequencing (which Objective comes next when multiple are eligible).
2. **`IRewardSelector`** — quest / loot / vendor reward choice when
   the `RewItemId{1..4}` / `RewChoiceItemId{1..6}` lists hold >1
   option.
3. **Combat rotation tasks** — `PvERotationTask` and per-class spec
   tasks call DecisionEngine for spell-pick and threat-target
   recommendations.

## 2. Service surface

```csharp
public interface IDecisionEngineClient
{
    Task<RotationAdvice>     GetRotationAdviceAsync(RotationContext ctx, CancellationToken ct);
    Task<ThreatAdvice>       GetThreatAdviceAsync(ThreatContext ctx, CancellationToken ct);
    Task<RewardAdvice>       GetRewardAdviceAsync(RewardContext ctx, CancellationToken ct);
    Task<ObjectiveAdvice>    GetObjectiveAdviceAsync(ObjectiveContext ctx, CancellationToken ct);
}
```

All four calls are **fail-soft**:

| Failure mode | Detection | `NoAdvice` cause | Surface |
|---|---|---|---|
| Wire timeout | client-side `CancellationTokenSource` at `decision-engine.json:timeoutMs` (default 50) | `AdviceError.Timeout` | `AdviceLog[].used_index = 0xFFFFFFFE` |
| Service unreachable | TCP connect fails or socket dropped | `AdviceError.ServiceDown` | `AdviceLog[].used_index = 0xFFFFFFFD` |
| Model not loaded | service returns `MODEL_UNLOADED` status | `AdviceError.ModelUnloaded` | `AdviceLog[].used_index = 0xFFFFFFFC` |
| Model inference failure | service returns `INFERENCE_ERROR` status | `AdviceError.InferenceFailed` | `AdviceLog[].used_index = 0xFFFFFFFB` |
| Confidence below floor | client-side `confidence < 0.5` cutoff | `AdviceError.LowConfidence` | `AdviceLog[].used_index = 0xFFFFFFFA` |

`NoAdvice` carries no recommendation; the caller falls back to the
deterministic default (rotation default in `BotProfiles/<ClassSpec>/`,
trivial first-valid reward selection, composer's deterministic
topological sort, threat tank-default).

Wire transport: protobuf over the same length-prefixed TCP framing as
the rest of the IPC stack. Port `5004` per
[`Spec/01_ARCHITECTURE.md`](01_ARCHITECTURE.md). Proto file at
`Exports/BotCommLayer/Models/ProtoDef/decision-engine.proto` (created by
Plan/14 slot S10.0).

### 2.1 Proto wire shapes

Field numbers below are normative. Plan/14 slot S10.0 writes the
`decision-engine.proto` file with exactly these numbers.

```protobuf
syntax = "proto3";
package decision_engine.v1;

import "communication.proto";   // for game.WoWPlayer, ObjectiveType, etc.

// --- Request envelope ---
message AdviceRequest {
    oneof body {
        RotationContext  rotation  = 1;
        ThreatContext    threat    = 2;
        RewardContext    reward    = 3;
        ObjectiveContext objective = 4;
    }
    uint64 request_id  = 10;     // monotonic; echoed in response for trace correlation
    uint64 issued_at_ms = 11;    // client clock; used for round-trip metric
    string requester_account = 12;  // bot account name, for trace pivoting
}

// --- Response envelope ---
message AdviceResponse {
    oneof body {
        RotationAdvice  rotation  = 1;
        ThreatAdvice    threat    = 2;
        RewardAdvice    reward    = 3;
        ObjectiveAdvice objective = 4;
        NoAdvice        no_advice = 5;
    }
    uint64 request_id    = 10;
    uint64 served_at_ms  = 11;
    AdvisorMode mode_used = 12;  // which maturity tier actually answered
    string model_version  = 13;  // when mode_used = ML; else empty
}

enum AdvisorMode {
    ADVISOR_MODE_TRIVIAL = 0;    // hand-rolled heuristic
    ADVISOR_MODE_RULES   = 1;    // rules + lookup table
    ADVISOR_MODE_ML      = 2;    // ONNX inference
}

message NoAdvice {
    AdviceError error = 1;
    string detail     = 2;       // optional human-readable diagnostic
}

enum AdviceError {
    ADVICE_OK              = 0;  // (not used on the wire; sentinel)
    ADVICE_TIMEOUT         = 1;
    ADVICE_SERVICE_DOWN    = 2;
    ADVICE_MODEL_UNLOADED  = 3;
    ADVICE_INFERENCE_FAILED = 4;
    ADVICE_LOW_CONFIDENCE  = 5;
    ADVICE_CONTEXT_INVALID = 6;  // service rejected the request shape
}

// --- Context messages (one per advisor) ---
message RotationContext {
    uint32 bot_level         = 1;
    uint32 bot_class         = 2;
    uint32 bot_spec          = 3;     // derived from talent tree
    uint32 current_hp_pct    = 4;     // 0..100
    uint32 current_mana_pct  = 5;     // 0..100
    uint32 target_hp_pct     = 6;
    uint32 target_level      = 7;
    uint32 target_creature_entry = 8; // 0 for player
    repeated uint32 known_spell_ids = 9;
    repeated uint32 active_aura_ids = 10;
    repeated uint32 target_aura_ids = 11;
    uint32 gcd_remaining_ms  = 12;
    uint32 cast_window_ms    = 13;    // budget for this advice
}

message ThreatContext {
    uint32 bot_level             = 1;
    uint32 bot_class             = 2;
    uint64 current_target_guid   = 3;
    repeated uint64 candidate_guids = 4;
    repeated uint32 candidate_threats = 5; // parallel to candidate_guids
    repeated uint32 candidate_hp_pct = 6;
    repeated uint32 candidate_levels = 7;
    repeated bool candidate_is_caster = 8;
    repeated bool candidate_is_healer = 9;
    uint64 group_tank_guid       = 10;
}

message RewardContext {
    uint32 bot_level                 = 1;
    uint32 bot_class                 = 2;
    uint32 bot_spec                  = 3;
    uint32 quest_entry               = 4;
    repeated uint32 reward_item_ids  = 5;
    repeated uint32 reward_item_quality = 6;   // parallel
    repeated uint32 reward_item_slot = 7;
    repeated uint32 reward_item_sell_price = 8;
    repeated uint32 currently_equipped_in_slot = 9;
    uint32 active_spec_role          = 10;     // tank/heal/melee-dps/ranged-dps/caster-dps
}

message ObjectiveContext {
    uint32 bot_level     = 1;
    uint32 bot_class     = 2;
    uint32 bot_race      = 3;
    repeated float bot_position = 4;             // [x, y, z]
    uint32 current_zone_id = 5;
    uint32 current_map_id  = 6;
    uint32 inventory_value_copper = 7;
    repeated string tied_objective_ids = 8;      // up to 8
    repeated ObjectiveType tied_objective_types = 9;
    repeated float tied_objective_costs = 10;
    repeated uint32 tied_unlock_fanout = 11;
    repeated float roster_goal_distance = 12;    // 8 floats per Spec/05
}

// --- Advice messages (responses) ---
message RotationAdvice {
    int32 recommended_spell_id = 1;   // -1 = no recommendation; client treats as NoAdvice
    float confidence            = 2;
    string rationale            = 3;
}

message ThreatAdvice {
    fixed64 focus_target_guid = 1;        // 0 = no recommendation
    repeated fixed64 avoid_target_guids = 2;
    float confidence          = 3;
    string rationale          = 4;
}

message RewardAdvice {
    int32 recommended_choice_index = 1;   // -1 = no recommendation
    float confidence                = 2;
    string rationale                = 3;
}

message ObjectiveAdvice {
    string recommended_objective_id = 1;  // empty = no recommendation
    float confidence                 = 2;
    string rationale                 = 3;
}
```

`fixed64` for GUIDs avoids varint overhead on 8-byte WoW object handles.
The `oneof` envelope keeps both directions discriminated-union; new
advisors are additive by extending the `oneof` (next free tag 5/6 on
each side).

## 3. Advice shapes

```csharp
public sealed record RotationAdvice(
    int? RecommendedSpellId,            // null = no recommendation
    float Confidence,                     // 0..1
    string Rationale);

public sealed record ThreatAdvice(
    ulong? FocusTargetGuid,              // null = no recommendation
    IReadOnlyList<ulong> AvoidTargetGuids,
    float Confidence,
    string Rationale);

public sealed record RewardAdvice(
    int? RecommendedChoiceIndex,         // null = no recommendation
    float Confidence,
    string Rationale);

public sealed record ObjectiveAdvice(
    string? RecommendedObjectiveId,      // null = no recommendation
    float Confidence,
    string Rationale);
```

The `Rationale` string is for operator-facing tracing — it appears in
the `WoWActivitySnapshot.AdviceLog[]` projection so the UI can show
"why did the bot pick that".

## 4. Model lifecycle

DecisionEngineService loads models from
`Services/DecisionEngineService/Models/<modelKind>/<version>.onnx`.
Versions are managed by:

```csharp
public sealed record ModelDescriptor(
    string Kind,                         // "rotation" | "threat" | "reward" | "objective"
    string Version,                       // semver
    string FilePath,
    DateTime LoadedAt,
    bool Enabled);
```

Hot-reload pattern: a config change broadcast on
`Config/decision-engine.json` triggers reload; per
[`Spec/14_CONFIG.md`](14_CONFIG.md). The config-subscriber scope is
`"DecisionEngine.Models"`; the `ConfigChangedEvent` payload is a
serialized `DecisionEngineConfig` message. Models are loaded in a
sidecar ONNX runtime; per-tick inference budget is 5 ms for
rotation/threat, 15 ms for reward, 50 ms for objective.

### 4.1 `Config/decision-engine.json` schema

```jsonc
{
  "transport": {
    "port": 5004,
    "timeoutMs": 50,                 // client-side request timeout
    "maxConcurrentRequests": 64      // bounded server-side queue
  },
  "advisors": {
    "rotation": { "mode": "Trivial", "modelVersion": null, "budgetMs": 5 },
    "threat":   { "mode": "Trivial", "modelVersion": null, "budgetMs": 5 },
    "reward":   { "mode": "Rules",   "modelVersion": null, "budgetMs": 15 },
    "objective":{ "mode": "Trivial", "modelVersion": null, "budgetMs": 50 }
  },
  "telemetry": {
    "traceEnabled": false,
    "traceDir": "tmp/test-runtime/traces",
    "ringBufferEntries": 8           // matches WoWActivitySnapshot.advice_log cap
  }
}
```

JSON Schema lives at `Config/schema/decision-engine.schema.json` per
[`Spec/14_CONFIG.md §1`](14_CONFIG.md). Hot-reload is supported for
`advisors.*.mode`, `advisors.*.modelVersion`, `telemetry.*`. Changing
`transport.port` requires a service restart per
[`Spec/14_CONFIG.md §3`](14_CONFIG.md).

### 4.2 ONNX feature tensor shapes (per advisor)

Each model takes a `float32` input tensor with the shape below. Output
shapes are scalar `(choice_index, confidence)` pairs, also `float32`.
Names match the proto context message fields.

| Advisor | Input shape | Output shape | Decision domain |
|---|---|---|---|
| Rotation | `[1, 64]` (level, class, spec, hp/mana/target-hp, padded `known_spell_ids[0..23]`, `active_aura_ids[0..15]`, `target_aura_ids[0..15]`, gcd_ms, cast_window_ms) | `[1, 2]` → (spell_id_index, confidence). `spell_id_index` indexes into `known_spell_ids`. | Discrete pick over `known_spell_ids` |
| Threat | `[1, 80]` (bot_level, bot_class, current_target_guid_hash, then 8 candidate stripes of 9 fields each padded) | `[1, 9]` → (focus_candidate_index, 8 avoid-mask bits) | Discrete pick over candidates |
| Reward | `[1, 56]` (level, class, spec, quest_entry, 4 reward stripes of 12 fields + active_spec_role + padding) | `[1, 2]` → (choice_index, confidence) | Discrete pick over 4 rewards |
| Objective | `[1, 96]` (level, class, race, position[3], zone_id, map_id, inventory_value_copper, 8 tied-objective stripes of 4 fields each, 8 roster-goal-distance floats, padding) | `[1, 9]` → (tied_index, confidence) | Discrete pick over up to 8 tied objectives |

Padded slots use `0.0` for floats and `0` for indexed enums; the
service is responsible for normalizing context to fixed-width tensors.
The contract is **service-side only** — `IDecisionEngineClient` never
sees the tensor.

## 5. Three maturity phases per advisor

Each of the four advisors evolves through three phases (matches the
[`Spec/03_BOTRUNNER.md#reward-selection`](03_BOTRUNNER.md#reward-selection)
phasing already documented for the reward selector):

| Phase | Source | Status |
|---|---|---|
| 1 — Trivial | Hand-rolled heuristic in `Services/DecisionEngineService/Heuristics/` | ships with Phase 10 entry |
| 2 — Rules + lookup table | Authored decision table per class/spec (BotProfiles input) | ships with Phase 10 mid |
| 3 — ML-augmented | ONNX inference against labeled gameplay traces | ships with Phase 10 exit |

A `Mode` enum on each advisor lets the operator pick the maturity
explicitly. Default is the highest available; tests can pin lower
modes for determinism.

## 6. Training-data pipeline

Tied to [`Spec/13_TESTING.md`](13_TESTING.md). Every live-validation
test produces a structured trace at
`tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl` (one JSON
object per line). The schema is normative; Plan/14 slot S10.7 writes
the producer.

### 6.1 Trace line schema

```jsonc
{
  "ts": 1716220800123,           // unix-ms
  "kind": "snapshot" | "advice_request" | "advice_response" | "objective_transition" | "task_terminal" | "outcome",
  "request_id": 0,               // 0 when kind != advice_*; correlates request/response
  "bot_account": "TESTBOT1",
  "snapshot_seq": 12345,         // WoWActivitySnapshot.snapshotSequence

  // kind=snapshot
  "snapshot_delta": {
    "current_activity_id": "dungeon.ubrs",
    "current_objective_id": "ubrs.reach-flame-crest",
    "hp_pct": 92, "mana_pct": 71, "position": [-7949.7, -1162.8, 170.8],
    "level": 60
  },

  // kind=advice_request
  "advisor": "rotation" | "threat" | "reward" | "objective",
  "context": { /* the matching *Context proto serialized as JSON */ },

  // kind=advice_response
  "mode_used": "Trivial" | "Rules" | "Ml",
  "model_version": "v1.2.0",
  "advice": { /* the matching *Advice proto serialized as JSON */ },
  "error": null | "Timeout" | "ServiceDown" | "ModelUnloaded" | "InferenceFailed" | "LowConfidence" | "ContextInvalid",
  "used_by_caller": true,        // false when caller dropped advice (e.g. id outside tie set)

  // kind=objective_transition
  "from_objective_id": "ubrs.reach-flame-crest",
  "to_objective_id": "ubrs.enter-instance-portal",

  // kind=task_terminal
  "task_name": "TravelToTask",
  "terminal": "Complete" | "Failed" | "Aborted",
  "reason": "task_timeout",      // FailureReason enum value when terminal != Complete

  // kind=outcome (end-of-Activity)
  "activity_id": "dungeon.ubrs",
  "completion": "complete" | "failed" | "aborted",
  "wall_clock_ms": 1842000,
  "xp_gained": 12500,
  "gear_slots_filled": 1,
  "gold_delta_copper": 45612,
  "roster_distance_delta": -1.8  // Spec/05 RosterPlanner distance change (NEGATIVE means progress)
}
```

Trace files are append-only and **never** read by the C# service.
DecisionEngineService writes them; the (out-of-process, Python)
training pipeline consumes them. The C# contract is just "produce
correct trace lines."

### 6.2 Trace correctness contract

Plan/14 slot S10.7 asserts:

1. Every `advice_request` has exactly one matching `advice_response`
   with the same `request_id`.
2. Every `objective_transition` is preceded by a `snapshot` whose
   `snapshot_delta.current_objective_id` equals `from_objective_id`.
3. Every test that produces ≥1 `outcome` line writes a
   `roster_distance_delta` ≤ 0 (the dynamic-progressive invariant from
   [`Spec/19 §10`](19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant)
   applied to the trace surface).

The contract is enforced by `TraceFileContractTests` listed in §9.

## 7. Why this is its own spec

Three reasons:

1. **The service already exists in code** (`Services/DecisionEngineService/`)
   but had no canonical spec; references were scattered across the
   Architecture, Progression, and BotRunner specs.
2. **The advisory-vs-authoritative split matters.** Without this spec,
   future work may slide DecisionEngine into the deterministic stack
   and lose the testability of the Activity layer.
3. **Phase 10 needs a stable contract to bind to.** Lifting it here
   prevents Phase 10's slots from drifting if the implementation
   evolves.

## 8. Existing code anchors

| Concept | File |
|---|---|
| Service entry | `Services/DecisionEngineService/Program.cs` |
| ML model loader | `Services/DecisionEngineService/MLModel.cs` |
| Engine entry | `Services/DecisionEngineService/DecisionEngine.cs` |
| Caller from BotRunner | `Exports/BotRunner/Clients/DecisionEngineClient.cs` (planned) |
| Reward selector consumer | `Exports/BotRunner/Activities/RewardSelector.cs` |

## 9. Test surface

Unit / contract tests (Plan/14 slot S10.0 + S10.5 own the bulk):

- **`DecisionEngineClientTests.NoAdvice_OnTimeout`** — service unreachable
  returns `NoAdvice` with `AdviceError.Timeout`. Slot S10.0.
- **`DecisionEngineClientTests.NoAdvice_OnServiceDown`** — TCP connect
  failure returns `NoAdvice` with `AdviceError.ServiceDown`. Slot S10.0.
- **`DecisionEngineClientTests.NoAdvice_OnModelLoadFailure`** — service
  reports `MODEL_UNLOADED` → returns `NoAdvice` with
  `AdviceError.ModelUnloaded`. Slot S10.0.
- **`DecisionEngineClientTests.NoAdvice_OnLowConfidence`** — service
  returns `Confidence = 0.3` → client treats as `NoAdvice` with
  `AdviceError.LowConfidence`. Slot S10.0.
- **`DecisionEngineClientTests.AdviceAppearsInSnapshotAdviceLog`** — the
  bot's `WoWActivitySnapshot.advice_log[]` (Spec/19 field 36) carries the
  rationale for tests / UI to verify. Slot S10.5.
- **`RewardSelectorTests.PrefersDecisionEngineAdvice`** — when advice
  confidence ≥ 0.5, selector uses the recommended index. Slot S10.1.
- **`RewardSelectorTests.FallsBackOnNoAdvice`** — `NoAdvice` falls
  through to the trivial first-valid policy. Slot S10.1.
- **`RewardSelectorTests.RejectsAdviceForUnknownChoiceIndex`** — advice
  with `RecommendedChoiceIndex` outside the actual choice list is
  discarded; trivial fallback applies. Slot S10.1.
- **`TraceFileContractTests.AdviceRequestHasMatchingResponse`** — every
  `advice_request` line in a `.jsonl` trace has a matching
  `advice_response` with the same `request_id`. Slot S10.7.
- **`TraceFileContractTests.ObjectiveTransitionPrecededBySnapshot`** —
  every `objective_transition` line is preceded by a `snapshot` line
  whose `current_objective_id` matches `from_objective_id`. Slot S10.7.
- **`DecisionEngine_DynamicProgressive_RosterDistanceDeltaIsNonPositiveTest`** —
  the dynamic-progressive invariant for the trace surface: every
  `outcome` line in any production-grade test trace has
  `roster_distance_delta ≤ 0`. Slot S10.7. See §10 below for the
  rationale.

Live-validation tests for the wired path live alongside per-Activity
LiveValidation tests; the `advice_log[]` is asserted on alongside
Activity / Objective transitions:

- **`DecisionEngine_RewardSelection_GuidedByAdvice`** — Plan/14 S10.8;
  a quest with a 3-choice reward has the advisor recommend index 2;
  bot picks 2 and the snapshot's `Player.Inventory` confirms.
- **`DecisionEngine_RotationFallback_OnServiceDown`** — Plan/14 S10.8;
  kill the DecisionEngineService process; the bot's rotation continues
  correctly via the trivial fallback path.

## 10. Dynamic-progressive invariant

DecisionEngine advice is **advisory and tiebreaker-only**. Per the
dynamic-progressive invariant in
[`Spec/19 §10`](19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant),
all four advisors must preserve both properties of the bot's
trajectory:

1. **Dynamic.** Advice that responds to snapshot inputs MUST produce
   different recommendations when the snapshot differs in
   advisor-relevant ways. Equivalent snapshots MUST produce equivalent
   advice (deterministic given fixed `mode_used` and `model_version`).
2. **Progressive.** Replaying any production trace with all four
   advisors **forced to `NoAdvice`** MUST still complete the Activity
   and produce a `roster_distance_delta ≤ 0` outcome line. This is the
   correctness guard for ML: if advice is removed entirely, the
   deterministic stack still closes the goal distance.

The `DecisionEngine_DynamicProgressive_RosterDistanceDeltaIsNonPositiveTest`
asserts this on every production-grade trace; the contract is the only
hard barrier against an over-eager ML pass that picks "interesting" but
non-progressive options.

## 11. Plan-slot cross-reference

| Slot | Owns | Section in this spec |
|---|---|---|
| [`Plan/14/S10.0`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s100--idecisionengineclient-shim--transport) | `IDecisionEngineClient`, `decision-engine.proto` | §2, §2.1, §9 tests 1-4 |
| [`Plan/14/S10.1`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s101--reward-selector-advisor-wire) | `RewardSelector.cs`, `RewardSelectorTests.cs` | §9 tests 6-8 |
| [`Plan/14/S10.2`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) | per-family `*Composer.cs` | §3, §4.2 Objective row |
| [`Plan/14/S10.3`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s103--rotation-advisor-wire) | `PvERotationTask.cs` | §4.2 Rotation row |
| [`Plan/14/S10.4`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s104--threat-advisor-wire) | `PullStrategyTask.cs` | §4.2 Threat row |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | `WoWActivitySnapshot.advice_log` (field 36 per Spec/19 §5) | §9 test 5 |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine.json`, `ModelDescriptor.cs` | §4, §4.1 |
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | `tmp/test-runtime/traces/`, `Tracing/` | §6, §9 tests 9-11 |
| [`Plan/14/S10.8`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire) | LiveValidation suite | §9 live-validation tests |
