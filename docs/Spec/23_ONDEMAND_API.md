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
contract (port 8088), through two methods on
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

## 10. Test surface

- **`OnDemandApiTests.RequestActivity_HappyPath_TransitionsToEngaged`** — every stage transitions Succeeded; `expected_engaged_at` is realistic.
- **`OnDemandApiTests.RequestActivity_PoolExhausted_RejectsWithAlternatives`** — pool full returns `POOL_EXHAUSTED` and lists nearby Activity catalog ids.
- **`OnDemandApiTests.MonitorActivity_StreamsAllStageTransitions`** — UI subscriber receives REQUESTED → DONE in order with no gaps.
- **`OnDemandApiTests.CancelActivityInstance_DuringTravelling_StopsLaunch`** — cancel mid-stage transitions to TEARDOWN within 5 s.
- **`OnDemandApiTests.RateLimit_BlocksAfterFifthRequest`** — 6th human request in 10 min returns `RATE_LIMITED` (added to RejectionCode in this spec PR).
- **`WhisperParserTests.RunRfc_MapsToDungeonRagefireChasmRequest`** — Shodan whisper `!run rfc` resolves to the correct catalog id.
