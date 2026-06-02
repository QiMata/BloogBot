# Spec 23 — OnDemand API surface

> **What this spec is.** The full request DSL, response shapes, and
> rejection codes for the OnDemand Activity launcher. Today the
> contract is sketched in
> [`Spec/00_VISION.md#on-demand-activity-contract`](00_VISION.md#on-demand-activity-contract)
> and [`Spec/02_STATEMANAGER.md#ondemand-activity-launcher`](02_STATEMANAGER.md#ondemand-activity-launcher);
> this spec is the consolidated request/response surface that the UI
> (slot S2.6 mode handler) and Shodan whisper parser both bind to.

## 1. Transport

The OnDemand API rides the **existing** length-prefixed protobuf-on-TCP
contract (port 9000), through two methods on
`Services/WoWStateManager/Listeners/StateManagerSocketListener.cs`:

| Method | Direction | Wire payload |
|---|---|---|
| `RequestActivity` | UI → StateManager | `OnDemandActivityRequest` proto |
| `MonitorActivity` | UI → StateManager (streaming) | `OnDemandActivityStageEvent` proto |
| `CancelActivityInstance` | UI → StateManager | `CancelActivityRequest` proto |

No HTTP. No REST. The WPF UI talks directly to StateManager.

## 2. `OnDemandActivityRequest` proto

```proto
message OnDemandActivityRequest {
  string request_id            = 1;   // client-generated UUID
  string activity_id           = 2;   // catalog row id; required
  uint64 requesting_human_guid = 3;   // 0 = no human (operator test)
  uint32 requesting_human_level = 4;  // for level-scaling decisions
  Faction requesting_faction   = 5;   // for cross-faction reach

  HumanRole human_role         = 6;   // Leader | Member | Observer
  RolePreference role_pref     = 7;   // preferred role; not authoritative

  LootPolicyOverride loot_override = 8;
  bool gear_human              = 9;   // Outfitting will gear the human too
  bool skip_lockouts           = 10;  // default true for OnDemand
  bool bot_raid_leader         = 11;  // overrides human Leader for raids

  uint32 staging_timeout_secs  = 12;  // default 600
  uint32 human_idle_timeout_secs = 13; // default 300 dungeons / 900 raids

  string operator_note         = 14;  // for audit log

  // Activity-specific parameters, opaque to the launcher
  google.protobuf.Any activity_parameters = 15;
}
```

Closed-set enums:

```proto
enum HumanRole       { MEMBER = 0; LEADER = 1; OBSERVER = 2; }
enum RolePreference  { ANY = 0; TANK = 1; HEALER = 2; DPS = 3; }
enum LootPolicyOverride {
    USE_ACTIVITY_DEFAULT = 0;
    FFA = 1;
    GROUP_LOOT = 2;
    NEED_BEFORE_GREED = 3;
    MASTER_LOOT = 4;
}
```

## 3. Response shape

```proto
message OnDemandActivityResponse {
  string request_id           = 1;
  bool accepted               = 2;
  string instance_id          = 3;   // empty when not accepted
  RejectionReason rejection   = 4;   // set when not accepted
  repeated FixupAction fixups = 5;   // when accepted; informational

  google.protobuf.Timestamp accepted_at = 6;
  google.protobuf.Timestamp expected_engaged_at = 7;
}

message RejectionReason {
  RejectionCode code     = 1;
  string detail          = 2;        // human-readable
  repeated string suggested_alternatives = 3;  // catalog ids
}

enum RejectionCode {
  // Hard rejections - no fixup possible
  UNKNOWN_ACTIVITY        = 0;
  SERVER_DISABLED         = 1;       // server caps say "Naxx: false"
  POOL_EXHAUSTED          = 2;       // reserved pool can't fill role template
  CONCURRENCY_LIMIT       = 3;       // MaxConcurrentActivities exceeded
  HUMAN_CROSS_FACTION_NO_PATH = 4;   // human cannot reach the staging coord
  CONFIG_MISSING          = 5;       // no Config/activities/<id>.json file

  // Soft rejections - launcher could try harder
  ACTIVITY_TIME_BLOCKED   = 6;       // outside game-event window (Spec/22)
  MAINTENANCE_WINDOW      = 7;       // server in scheduled maintenance

  // Rate-limit rejection (Spec/23 §6)
  RATE_LIMITED            = 8;       // requesting_human hit the 5-per-10-min cap

  // Ambiguity rejection (Spec/23 §11 ML integration)
  AMBIGUOUS_REQUEST       = 9;       // whisper parser produced multiple candidates
                                     //   and the advisor returned NoAdvice
                                     //   below confidence; suggested_alternatives
                                     //   is populated with the candidate set
}
```

`FixupAction` enumerates the GM-command operations the launcher will
apply during Outfitting. It is **informational** for the UI ("we will
do these things") so the operator can audit the side effects before
the Activity starts.

```proto
message FixupAction {
  FixupKind kind    = 1;
  string target     = 2;             // bot accountName or "HUMAN"
  string detail     = 3;
  string gm_command = 4;             // ".character level 60", ".reset lockouts", ...
}

enum FixupKind {
  LEVEL_UP        = 0;
  RESET_TALENTS   = 1;
  LEARN_RECIPES   = 2;
  LEARN_SPELLS    = 3;
  SET_SKILL       = 4;
  ADD_ITEM        = 5;
  RESET_LOCKOUTS  = 6;
  MODIFY_REP      = 7;
  TELEPORT        = 8;
  RESET_HONOR     = 9;
  SET_RIDING      = 10;
  BIND_HEARTH     = 11;
}
```

## 4. Monitoring stream

`MonitorActivity` is a server-streaming RPC. The UI subscribes once
per accepted instance and receives stage-transition events:

```proto
message OnDemandActivityStageEvent {
  string instance_id = 1;
  Stage  stage       = 2;
  google.protobuf.Timestamp at = 3;
  StageStatus status = 4;            // InProgress | Succeeded | Failed
  string detail      = 5;
  FailureReason failure_reason = 6;
}

enum Stage {
  REQUESTED   = 0;
  LEGALITY    = 1;
  SPAWNING    = 2;
  OUTFITTING  = 3;
  PARTYING    = 4;
  TRAVELLING  = 5;
  ENGAGED     = 6;
  TEARDOWN    = 7;
  DONE        = 8;
}
```

Tests and the UI assert on the `(stage, status)` transition sequence;
a hung stage exceeding the `staging_timeout_secs` automatically
publishes a `FAILED` event with `FailureReason.STAGE_TIMEOUT` (from
[`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md)).

## 5. Shodan whisper parser

The `OnDemandActivitiesModeHandler` accepts in-game whispers to the
Shodan GM Liaison character (see WWoW `CLAUDE.md → Shodan`). The
whisper DSL maps to `OnDemandActivityRequest`:

```
Whisper format:
   !<verb> <activityShorthand> [params...]

Verbs:
   !group  - request a group form (humanRole=LEADER by default)
   !run    - request a dungeon clear (humanRole=MEMBER)
   !raid   - request a raid form (humanRole=MEMBER)
   !bg     - request a BG queue + entry
   !fish   - request a fishing-spot bot
   !port   - request a mage portal escort
   !summon - request a warlock-summon service

Examples:
   !run rfc            → activity_id=dungeon.ragefire-chasm, humanRole=MEMBER
   !raid mc            → activity_id=raid.mc
   !bg wsg             → activity_id=bg.warsong-gulch
   !fish ratchet       → activity_id=prof.fishing-route, params=Ratchet
   !port ironforge     → social.mage-port, params=IF destination
```

The parser table lives at `Config/whisper-parser.json` and is
hot-reloadable.

## 6. Rate limiting

Per-human rate limit: 5 OnDemand requests / 10 minutes. Operators
(`Shodan`-authorized accounts) are exempt.

Per-realm concurrency: `MaxConcurrentActivities` in the StateManager
config caps simultaneous active OnDemand instances. Default 20;
configurable.

Pool-exhaustion errors include the `suggested_alternatives` field
(catalog rows that would satisfy a similar Activity-family request at
the same level range with current pool availability).

## 7. Audit log

Every `RequestActivity` call appends a row to
`Tmp/audit/ondemand-<date>.jsonl`:

```json
{
  "ts": "2026-05-17T18:42:00Z",
  "requestId": "8c2e...",
  "requestingHuman": "GuildmasterFred",
  "activityId": "dungeon.upper-blackrock-spire",
  "accepted": true,
  "instanceId": "f4a1...",
  "fixupCount": 12,
  "stageProgression": [
    { "stage": "SPAWNING",   "at": "...", "status": "Succeeded" },
    { "stage": "OUTFITTING", "at": "...", "status": "Succeeded" },
    { "stage": "PARTYING",   "at": "...", "status": "Succeeded" },
    { "stage": "TRAVELLING", "at": "...", "status": "Succeeded" },
    { "stage": "ENGAGED",    "at": "...", "status": "Succeeded" }
  ]
}
```

The audit log feeds the metrics dashboard's "OnDemand requests today"
panel and is the long-term record for operator-visible activity.

## 8. Failure semantics

- **Bot disconnect during ENGAGED:** the launcher attempts pool
  replacement (other reserved-pool bot of matching role); fails over
  to instance cancellation if no replacement is available.
- **Human disconnect during ENGAGED:** launcher honors
  `human_idle_timeout_secs`; teardown starts at the timeout.
- **Activity completion:** launcher transitions to `TEARDOWN`
  automatically on completion signal (boss kill / BG end / quest
  turn-in).
- **Operator cancel:** `CancelActivityInstance(instanceId, reason)`
  transitions stage to `TEARDOWN` regardless of current stage.

## 9. UI / Shodan integration paths

| Caller | Entry point | Slot |
|---|---|---|
| WPF UI | `OnDemandRequestPanel.cs` (Phase 3 slot in `Plan/04_PHASE3_UI_DEFAULT.md`) | UI-side |
| Shodan whisper | `OnDemandActivitiesModeHandler.OnExternalActivityRequestAsync` | Phase 2 slot S2.6 |
| Test fixture | `OnDemandFixture.RequestAsync(...)` helper | Tests/BotRunner.Tests/OnDemand/ |

## 10. Snapshot projection

OnDemand state surfaces on `WoWActivitySnapshot` via two additive
proto fields (continuing after Spec/22 fields 40-42):

```protobuf
message OnDemandInstanceProj {
    string instance_id      = 1;
    string activity_id      = 2;   // catalog id
    Stage  stage            = 3;   // Spec/23 §4 enum
    StageStatus status      = 4;
    uint64 stage_entered_at_ms = 5;
    uint64 staging_timeout_secs = 6;   // echoed from the request
    uint64 human_idle_timeout_secs = 7;
    string requesting_human_account = 8;
    uint32 fixup_count      = 9;       // number of FixupAction the launcher applied
}

message OnDemandRequestEcho {
    string request_id          = 1;
    bool   accepted            = 2;
    RejectionCode rejection_code = 3;  // unset when accepted
    uint64 received_at_ms      = 4;
}

// New fields on WoWActivitySnapshot:
repeated OnDemandInstanceProj ondemand_instances    = 43;  // active instances this bot is part of
repeated OnDemandRequestEcho  recent_ondemand_echoes = 44; // ring buffer cap 8 (per-bot view of requests
                                                            //                   relevant to this bot)
```

Tests assert on these snapshot fields rather than poking
`OnDemandActivityLauncher` internals. The `recent_ondemand_echoes`
ring buffer is the only way a test verifies a rejection reached the
launcher with the expected code (per Test Isolation Rules).

## 11. Failure-reason mapping

OnDemand failures map onto Spec/12's `FailureReason` enum:

| Failure | Spec/12 reason | Notes |
|---|---|---|
| Stage exceeds `staging_timeout_secs` | `task_timeout` (with detail `ondemand_stage_timeout`) | one new follow-up: `ondemand_stage_timeout` standalone reason |
| Bot disconnect during ENGAGED | `bot_disconnected` | exists |
| Pool replacement available but join failed | `task_unrecoverable` | logged with replacement attempt count |
| Human disconnect past `human_idle_timeout_secs` | `task_cancelled` | with detail `human_idle_timeout` |
| Server config disabled the activity | `server_capability_missing` | exists |
| `Config/activities/<id>.json` missing | `catalog_invalid` | exists |
| Pool exhausted | (no FailureReason — `RejectionCode.POOL_EXHAUSTED` at the request layer; never reaches Task layer) | |
| Rate-limit hit | (no FailureReason — `RejectionCode.RATE_LIMITED` at the request layer) | |

One new value **needed** in Spec/12 (already referenced in §4 as
`FailureReason.STAGE_TIMEOUT` but not in the enum):

- `ondemand_stage_timeout` *(new)* — a stage took longer than its
  configured budget. Distinct from `task_timeout` because the timeout
  is per-stage and the metric label is more specific.

Logged as a follow-up; row-15 Spec/12 pass MUST add it.

## 12. ML integration — Request disambiguation

**Surface.** When the whisper parser at §5 maps a verb+shorthand to a
candidate set with `|candidate_activity_ids| ≥ 2`, the
`OnDemandActivitiesModeHandler.OnExternalActivityRequestAsync` consults
`IDecisionEngineClient.GetActivityRequestAdviceAsync(ActivityRequestContext, ct)`
([`Spec/20 §2.3`](20_DECISION_ENGINE.md#23-activityrequest-advisor)).
The advisor returns the catalog id it believes the operator meant.

**Why advisory not authoritative.** The deterministic fallback is the
parser's existing static table at `Config/whisper-parser.json` —
shorthand `brd` defaults to `dungeon.brd-upper` by alphabetical id. The
advisor's job is to nudge the disambiguation toward what the operator
*meant* given their level, faction, current zone, and (later phase) past
request history. Operator can always re-whisper with the explicit id
to override.

**Input feature vector.** `ActivityRequestContext` from
[`Spec/20 §2.1`](20_DECISION_ENGINE.md#21-proto-wire-shapes). Service-
side tensor shape is `[1, 72]` per
[`Spec/20 §4.2`](20_DECISION_ENGINE.md#42-onnx-feature-tensor-shapes-per-advisor).

**Output shape.** `ActivityRequestAdvice` from Spec/20 §2.1.

**Three maturity phases** per [`Spec/20 §5`](20_DECISION_ENGINE.md):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | `Config/whisper-parser.json` plus "prefer lowest level-range that contains requesting_human_level" tiebreaker | Plan/03 slot S2.6 |
| 2 — Rules + lookup | `Config/decision-engine/activity-request-rules.json` per `(verb, requester_zone, requester_level_band)` precedence | Plan/14 slot S10.6 |
| 3 — ONNX | `Services/DecisionEngineService/Models/activity_request/v1.onnx`; trained on labeled traces under `tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl` carrying operator confirmations | Plan/14 slot S10.6 (Mode=Ml flip) |

**Fail-soft fallback.** When advice is `NoAdvice`, low confidence, or
recommends an id outside the candidate set, the parser **either** falls
back to the static-table default **or** — when the candidate set has
≥2 entries and no single high-confidence answer exists — returns
`RejectionCode.AMBIGUOUS_REQUEST` with the candidates listed in
`suggested_alternatives`. The UI then prompts the operator to pick
explicitly.

**Live-validation guard.** Replaying a whisper-disambiguation trace
with the advisor forced to `NoAdvice` MUST produce one of:
(a) an `accepted=true` response when the static-table default suffices,
(b) a `RejectionCode.AMBIGUOUS_REQUEST` response with the operator
follow-up sequence intact. The advisor MUST NOT cause silent
mis-routing — the launcher always knows when it's guessing.

## 13. Dynamic-progressive invariant

OnDemand requests MUST satisfy both properties on every accepted
launch:

1. **Dynamic.** Two operators with different
   `(requesting_human_guid, requesting_human_level, requesting_faction,
   requesting_human_zone)` issuing the *same* whisper text MUST
   sometimes produce different accepted Activity IDs (when the
   candidate set is ambiguous and the advisor disambiguates by context).
   Identical inputs (including no advice / fixed `mode_used`) produce
   identical IDs.
2. **Progressive.** Every accepted OnDemand launch MUST measurably
   close *someone's* `RosterPlanner.Distance` — either the human
   player's (gear-slot fill, attunement step, rep tier, mount tier) or
   the participating bots' (their own roster goals, since pool bots
   also have `CharacterRosterGoal`). Cosmetic-only launches (e.g. an
   accidental BG queue when the bot is already capped on honor rank)
   are still accepted, but the trace-side
   `roster_distance_delta_aggregate` MUST be ≤ 0 across the involved
   bot set when summed.

Asserted via
`OnDemandApi_DynamicProgressive_AmbiguousWhisperResolvesPerContextTest`
in §14.

## 14. Plan-slot cross-reference

| Slot | Owns | Section |
|---|---|---|
| [`Plan/03/S2.5`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s25--ondemandactivitylauncher) | `OnDemandActivityLauncher.cs` | §1, §3, §4 |
| [`Plan/03/S2.6`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s26--ondemandactivitiesmodehandler) | `OnDemandActivitiesModeHandler.cs` (whisper handler) | §5, §12 |
| [`Plan/03/S2.7`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s27--per-activity-config-files) | `Config/activities/<id>.json` | §3 `CONFIG_MISSING` |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | Snapshot projection for §10 fields 43-44 | §10 |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine/activity-request-rules.json`, ONNX | §12 Phase 2 / Phase 3 |
| (UI side) [`Plan/04/P3.x`](../Plan/04_PHASE3_UI_DEFAULT.md) | `OnDemandRequestPanel.cs` | §9 UI row |

## 15. Test surface

Contract tests live at
`Tests/BotRunner.Tests/OnDemand/OnDemandApiContractTests.cs`. All
`Skip("contract pending S<phase>.<n>")` until the matching slot lands.
Assertions go through `WoWActivitySnapshot.ondemand_instances[]` and
`recent_ondemand_echoes[]` per the Test Isolation Rules.

- **`RequestActivity_HappyPath_TransitionsToEngaged`** — every stage
  transitions `Succeeded`; `snapshot.ondemand_instances[0].stage`
  walks through REQUESTED → LEGALITY → SPAWNING → OUTFITTING →
  PARTYING → TRAVELLING → ENGAGED with no `Failed` along the way.
  Slot S2.5.
- **`RequestActivity_PoolExhausted_RejectsWithAlternatives`** — pool
  full returns `RejectionCode.POOL_EXHAUSTED` and the response carries
  `suggested_alternatives` with ≥1 nearby catalog id. Asserted via
  `snapshot.recent_ondemand_echoes[0].rejection_code`. Slot S2.5.
- **`MonitorActivity_StreamsAllStageTransitions`** — UI subscriber
  receives REQUESTED → DONE in order with no gaps. Asserted via the
  monotonic `stage_entered_at_ms` sequence in
  `snapshot.ondemand_instances[]`. Slot S2.5.
- **`CancelActivityInstance_DuringTravelling_StopsLaunch`** — cancel
  mid-stage transitions to TEARDOWN within 5 s. Slot S2.5.
- **`RateLimit_BlocksAfterFifthRequest`** — 6th human request in 10
  min returns `RejectionCode.RATE_LIMITED`. Slot S2.5.
- **`WhisperParser_RunRfc_MapsToDungeonRagefireChasmRequest`** —
  Shodan whisper `!run rfc` resolves to `dungeon.ragefire-chasm`.
  Slot S2.6.
- **`WhisperParser_AmbiguousBrd_ReturnsAmbiguousRequest`** — Shodan
  whisper `!run brd` with the advisor returning `NoAdvice` and the
  candidate set of size ≥2 produces `RejectionCode.AMBIGUOUS_REQUEST`
  with all candidates listed in `suggested_alternatives`. Slot S2.6
  + S10.6.
- **`WhisperParser_AmbiguousBrd_AdvisorPicksByContext`** — same
  whisper but advisor returns `ActivityRequestAdvice` with
  `RecommendedActivityId="dungeon.brd-lower"` confidence ≥ 0.5; the
  launcher accepts and `snapshot.advice_log` records the rationale.
  Slot S2.6 + S10.6.
- **`OnDemandApi_DynamicProgressive_AmbiguousWhisperResolvesPerContextTest`** —
  the dynamic-progressive invariant from §13. Two synthetic operator
  contexts that differ in `(requesting_human_level, requesting_faction,
  requesting_human_zone)` issuing the *same* `!run brd` whisper MUST
  resolve to either the same Activity (when context dominates) or
  different Activities (when context tilts the advisor). In either
  case the resolved Activity's `roster_distance_delta_aggregate`
  across the involved bot set MUST be ≤ 0 on completion. Slot S2.6 +
  S10.6.
