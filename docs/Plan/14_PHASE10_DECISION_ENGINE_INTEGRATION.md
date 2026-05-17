# Plan 14 — Phase 10: Decision-Engine integration

> **Goal.** Wire the existing `Services/DecisionEngineService/` into
> the runtime `IActivity` / `IObjective` composer, the reward selector,
> and the per-class combat rotations. End-state: every advisory call
> site has a working `IDecisionEngineClient` invocation that fails
> soft when the service is unavailable.
>
> **Entry pre-requisite.** Phase 2 done (S2.0 `IActivity` / `IObjective`
> contracts live). Phase 10 builds on top.

## Exit criteria

- [ ] `IDecisionEngineClient` implemented at `Exports/BotRunner/Clients/DecisionEngineClient.cs` with the **seven** advisory methods from [`Spec/20_DECISION_ENGINE.md §2`](../Spec/20_DECISION_ENGINE.md#2-service-surface): rotation, threat, reward, objective (original), plus chat_template, activity_request, personality_cluster (added by Specs 21/23/24 deepening passes).
- [ ] `IRewardSelector` consults `GetRewardAdviceAsync` and prefers high-confidence advice; falls back to first-valid on `NoAdvice`.
- [ ] `IActivityComposer.Compose(...)` consults `GetObjectiveAdviceAsync` when multiple Objectives tie on priority + travel cost; advice resolves the tie.
- [ ] `PvERotationTask` consults `GetRotationAdviceAsync` per cast window; uses advice when confidence > 0.5; falls back to per-class rotation logic otherwise.
- [ ] `PvERotationTask` consults `GetThreatAdviceAsync` when multiple aggressors of similar threat exist; advice drives target swap.
- [ ] `IChatGenerator` consults `GetChatTemplateAdviceAsync` for template selection per [`Spec/21 §11`](../Spec/21_SOCIAL_FABRIC.md#11-ml-integration); falls back to round-robin heuristic on `NoAdvice`.
- [ ] `OnDemandActivitiesModeHandler` consults `GetActivityRequestAdviceAsync` for whisper-shorthand disambiguation per [`Spec/23 §12`](../Spec/23_ONDEMAND_API.md#12-ml-integration--request-disambiguation); falls back to static-table default or `RejectionCode.AMBIGUOUS_REQUEST` on `NoAdvice`.
- [ ] `IPersonalityFactory.Create(accountName)` consults `GetPersonalityClusterAdviceAsync` at profile-generation time per [`Spec/24 §11`](../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering); falls back to uniform `PersonalityMix` sampling on `NoAdvice`.
- [ ] `WoWActivitySnapshot.advice_log[]` (proto field 36 per [`Spec/19 §5`](../Spec/19_AOTA_RUNTIME.md#5-snapshot-projection)) projects rationale strings so tests + UI can verify why advice was applied. Ring buffer cap 8.
- [ ] LiveValidation tests assert advice does **not** break correctness — a rotation under high-jitter advice still completes the encounter within budget; replaying any trace with all advisors forced to `NoAdvice` still produces `roster_distance_delta ≤ 0` outcomes per [`Spec/20 §10`](../Spec/20_DECISION_ENGINE.md#10-dynamic-progressive-invariant).
- [ ] Three maturity phases (Trivial / Rules / ML) ship for at least the Reward advisor; the other six ship at Trivial mode initially.

## Slots

### S10.0 — `IDecisionEngineClient` shim + transport

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 2 done.
- **Owned paths:**
  - `Exports/BotRunner/Clients/DecisionEngineClient.cs`
  - `Exports/BotCommLayer/Models/ProtoDef/decision-engine.proto`
  - `Services/DecisionEngineService/Listeners/DecisionEngineSocketListener.cs`
- **Read-only paths:** `Exports/BotCommLayer/ProtobufSocketServer.cs` (length-prefix framing reuse), `Services/DecisionEngineService/Program.cs`.
- **Spec contracts:** [`Spec/20_DECISION_ENGINE.md §2`](../Spec/20_DECISION_ENGINE.md#2-service-surface), [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) (proto wire shape), [`Spec/01_ARCHITECTURE.md`](../Spec/01_ARCHITECTURE.md) (port 5004).
- **Goal:** Land the **seven** advisory protobuf RPCs (`AdviceRequest`/`AdviceResponse` `oneof` envelopes per Spec/20 §2.1) + the C# client with 50 ms default timeout + `NoAdvice` fail-soft semantics. Six error codes (`AdviceError`) surface on the wire.
- **Procedure:**
  1. Generate `decision-engine.proto` from Spec/20 §2.1 normative shapes; preserve field numbers 1-13 on envelopes and tags 1-8 on `oneof body`.
  2. Implement `DecisionEngineClient` with per-call `CancellationTokenSource(timeoutMs)` and try/catch → `NoAdvice` mapping.
  3. Wire `DecisionEngineSocketListener` on port 5004 using the existing length-prefix framing from `ProtobufSocketServer`.
  4. Implement client-side confidence floor (default 0.5) — service-level advice below floor is silently mapped to `NoAdvice` with `AdviceError.ADVICE_LOW_CONFIDENCE`.
- **Success criteria:** unit tests `DecisionEngineClientTests.NoAdvice_OnTimeout`, `NoAdvice_OnServiceDown`, `NoAdvice_OnModelLoadFailure`, `NoAdvice_OnLowConfidence` all green (defined in [`Tests/BotRunner.Tests/Clients/DecisionEngineClientTests.cs`](../../Tests/BotRunner.Tests/Clients/DecisionEngineClientTests.cs)).
- **Failure modes:**
  - Proto schema drift between client and service → service rejects with `AdviceError.ADVICE_CONTEXT_INVALID`; client treats as `NoAdvice`.
  - Port 5004 already bound → service refuses to start; client treats every request as `NoAdvice.ServiceDown`.
- **ML integration sub-bullet:** This slot IS the ML integration substrate. All seven advisors ride this transport.

### S10.1 — Reward selector advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:**
  - `Exports/BotRunner/Activities/RewardSelector.cs`
  - `Tests/BotRunner.Tests/Activities/RewardSelectorTests.cs`
- **Read-only paths:** `Exports/GameData.Core/Models/Activities/RewardDefinition.cs`, `Spec/20`, `Spec/03 §reward-selection`.
- **Spec contracts:** [`Spec/03_BOTRUNNER.md#reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection), [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) `RewardContext` / `RewardAdvice`.
- **Goal:** Replace the trivial first-valid selector with one that consults `GetRewardAdviceAsync` and uses advice when confidence > 0.5. Maintain the always-picks invariant (a reward is ALWAYS selected; `NoAdvice` falls through to trivial).
- **Procedure:**
  1. Build `RewardContext` from `RewardDefinition` + `Player.Inventory` + `CharacterRosterGoal`.
  2. Call `GetRewardAdviceAsync`; on advice with confidence ≥ 0.5 AND `RecommendedChoiceIndex` within `reward_item_ids` count, return that index.
  3. Otherwise fall through to "first-valid" deterministic policy.
  4. Emit an `AdviceLogEntry` via `SnapshotBuilder` (S10.5 owns the emission).
- **Success criteria:** `RewardSelectorTests.PrefersDecisionEngineAdvice` + `FallsBackOnNoAdvice` + `RejectsAdviceForUnknownChoiceIndex` green.
- **Failure modes:** advice with out-of-range index → drop to trivial; advice for a quest the bot already turned in → drop to trivial.
- **ML integration sub-bullet:** Phase 1 = first-valid (this slot). Phase 2 = rules table at `Config/decision-engine/reward-rules.json` (S10.6). Phase 3 = ONNX inference (S10.7 trace-trained).

### S10.2 — Objective composer tie-breaker

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0, Phase 2 `IActivityComposer` lands (slot S2.0).
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Composers/*Composer.cs` (per-family)
  - `Services/WoWStateManager/Activities/Composers/ObjectiveTieBreaker.cs` (new shared helper)
- **Read-only paths:** `Services/WoWStateManager/Activities/Composers/IActivityComposer.cs` (S2.0), `Spec/19 §9`, `Spec/22 §11` (buff-window pivot reuses this surface).
- **Spec contracts:** [`Spec/19_AOTA_RUNTIME.md §9`](../Spec/19_AOTA_RUNTIME.md#9-ml-integration--composer-tiebreaker), [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) `ObjectiveContext` / `ObjectiveAdvice`, [`Spec/22 §11`](../Spec/22_WORLD_CYCLES.md#11-ml-integration--buff-window-pivot).
- **Goal:** When the composer's deterministic sort produces a tie on (band, weight, soonest-expiring, lowest-travel, fanout), consult `GetObjectiveAdviceAsync` for the tiebreaker. Deterministic fallback by Objective-id (lex sort) when advice is `NoAdvice` or the recommended id is outside the tied set. Reused as the buff-window pivot mechanism per Spec/22 §11.
- **Procedure:**
  1. After deterministic sort, detect tied head — Objectives sharing the same priority key tuple.
  2. If `|tied| ≥ 2`, build `ObjectiveContext` from snapshot + tied set (cap at 8 entries).
  3. Call `GetObjectiveAdviceAsync`; validate `RecommendedObjectiveId` ∈ `tied_objective_ids`.
  4. On validation success, splice the recommended id to the head; on failure, lex-sort tied set and use first.
- **Success criteria:** `ObjectiveComposer_NoAdvice_FallsThroughToDeterministicTieBreak` + `ObjectiveComposer_AdviceOutsideTieSet_IsIgnored` green; composed sequence is stable across re-runs given identical inputs and `Mode=Trivial`.
- **Failure modes:** advice latency exceeds composer budget → composer continues with deterministic tie-break; `AdviceLogEntry.used_index=0xFFFFFFFE`.
- **ML integration sub-bullet:** Phase 1 heuristic = lex-sort (this slot). Phase 2 = `Config/decision-engine/objective-tie-rules.json` (S10.6). Phase 3 = ONNX (S10.7).

### S10.3 — Rotation advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:**
  - `Exports/BotRunner/Tasks/PvERotationTask.cs`
  - `BotProfiles/*/Tasks/PvERotationTask.cs` (per-class spec)
- **Read-only paths:** `BotProfiles/*` (rotation correctness reference), `Spec/24` (jitter knobs).
- **Spec contracts:** [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) `RotationContext` / `RotationAdvice`, [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) `IntraRotationJitterMs` row.
- **Goal:** Per-cast-window advisory call. Use `RecommendedSpellId` when confidence ≥ 0.5 AND the spell id is in `RotationContext.known_spell_ids`; otherwise follow the per-class rotation. Variance from [`Spec/24`](../Spec/24_BEHAVIORAL_VARIATION.md) applies on top of either path.
- **Procedure:**
  1. Inside `PvERotationTask.PickNextSpell(...)`, before consulting the per-class table, build `RotationContext` from current snapshot + target snapshot.
  2. Call `GetRotationAdviceAsync` with a 5 ms budget; on advice with confidence ≥ 0.5 AND spell ∈ known set, use it.
  3. Otherwise call into the existing per-class `RotationLookup.PickNextSpell(...)`.
  4. Always apply Spec/24 `IntraRotationJitterMs` between picks.
- **Success criteria:** `PvERotationTaskTests.AdviceRecommendsKnownSpell_Used` green; rotation correctness regression suite still green (no class regression).
- **Failure modes:** advice recommends spell not in `known_spell_ids` (cross-class confusion) → drop to per-class table; advice during GCD → ignored.
- **ML integration sub-bullet:** Phase 1 = per-class hand-rolled rotation. Phase 2 = `Config/decision-engine/rotation-rules.json` keyed by `(class, spec, encounter_kind)`. Phase 3 = per-class ONNX trained on labeled traces.

### S10.4 — Threat advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:**
  - `Exports/BotRunner/Tasks/PullStrategyTask.cs`
  - `BotProfiles/Warrior*/Tasks/PullTargetTask.cs` (Prot only)
  - `BotProfiles/Druid*/Tasks/PullTargetTask.cs` (Feral-tank only)
  - `BotProfiles/Paladin*/Tasks/PullTargetTask.cs` (Prot only)
- **Read-only paths:** `Exports/BotRunner/Combat/ThreatTable.cs`, `Spec/24` `ReactionTimeJitterMs` knob.
- **Spec contracts:** [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) `ThreatContext` / `ThreatAdvice`.
- **Goal:** When multiple aggressors are within the threat-swap window (top-2 within `ThreatTable.SwapThreshold`), the advisory call resolves the target swap. Tank rotations only — DPS rotations follow the existing threat table without advice.
- **Procedure:**
  1. Detect threat-swap window in `PullStrategyTask.Tick(...)`.
  2. Build `ThreatContext` with up to 8 candidate GUIDs + parallel threat / hp / level / is_caster / is_healer arrays.
  3. Call `GetThreatAdviceAsync`; on advice, validate `FocusTargetGuid` is in `candidate_guids`.
  4. On validation success, swap; on failure, follow the existing `ThreatTable.PickTop()`.
- **Success criteria:** `PullStrategyTaskTests.AdviceSwapsToRecommendedTarget` green; existing threat-table regression suite still green.
- **Failure modes:** advice for a candidate not in window → drop to ThreatTable; advice latency exceeds 5 ms budget → drop to ThreatTable; `FocusTargetGuid=0` → drop.
- **ML integration sub-bullet:** Phase 1 = `ThreatTable.PickTop` (this slot). Phase 2 = rules table keyed by `(boss_entry, encounter_phase)`. Phase 3 = per-encounter ONNX.

### S10.5 — `AdviceLog` snapshot projection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.1..S10.4, S10.9, S10.10, S10.11 (any wire-slot that emits advice).
- **Owned paths:**
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto` (add fields per Spec/19 §5)
  - `Exports/BotRunner/SnapshotBuilder.cs`
- **Read-only paths:** `Spec/19 §5` (normative proto field numbers and `AdviceLogEntry` shape).
- **Spec contracts:** [`Spec/19 §5`](../Spec/19_AOTA_RUNTIME.md#5-snapshot-projection) (field 36 = `repeated AdviceLogEntry advice_log`).
- **Goal:** Add `repeated AdviceLogEntry advice_log = 36` to `WoWActivitySnapshot` per Spec/19 §5 normative shape; each entry carries `(advisor, rationale, confidence, used_index, timestamp)`. Bounded ring buffer cap **8** per Spec/19 (NOT 20 — the prior draft of this slot specified 20 which is out of date).
- **Procedure:**
  1. Extend `communication.proto` with `AdviceLogEntry` message and field 36 on `WoWActivitySnapshot`; preserve all existing field numbers.
  2. `SnapshotBuilder` reads from a bot-local `RingBuffer<AdviceLogEntry>(capacity=8)`; on snapshot build, copies the buffer contents.
  3. `used_index` sentinels: `0xFFFFFFFF` = advice received but discarded by caller; `0xFFFFFFFE..0xFFFFFFFA` = `AdviceError.*` per Spec/20 §2 table.
  4. Wire-slot tasks (S10.1, S10.2, S10.3, S10.4, S10.9, S10.10, S10.11) push to the bot-local buffer after every advice call.
- **Success criteria:** `DecisionEngineClientTests.AdviceAppearsInSnapshotAdviceLog` green; buffer never exceeds cap; recent-most entry visible within the next snapshot tick.
- **Failure modes:** buffer overflow → oldest entries silently evicted (FIFO); per Spec/19 this is the expected behavior, not a failure.
- **ML integration sub-bullet:** none directly. This slot is the projection substrate that all advisor wires write to. Trace pipeline (S10.7) reads from these logs.

### S10.6 — Mode-aware advisor activation

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:**
  - `Config/decision-engine.json`
  - `Config/schema/decision-engine.schema.json`
  - `Services/DecisionEngineService/ModelDescriptor.cs`
  - `Services/DecisionEngineService/Heuristics/` (Phase-1 implementations per advisor)
  - `Config/decision-engine/rotation-rules.json`, `objective-tie-rules.json`, `reward-rules.json`, `threat-rules.json`, `chat-template-rules.json`, `activity-request-rules.json`, `personality-cluster-rules.json` (Phase-2 lookup tables)
- **Read-only paths:** `Spec/14_CONFIG.md` (hot-reload semantics), `Spec/20 §4.1` (config schema).
- **Spec contracts:** [`Spec/14_CONFIG.md`](../Spec/14_CONFIG.md), [`Spec/20 §4`](../Spec/20_DECISION_ENGINE.md#4-model-lifecycle), [`Spec/20 §4.1`](../Spec/20_DECISION_ENGINE.md#41-configdecision-enginejson-schema).
- **Goal:** Per-advisor `AdvisorMode` enum (Trivial / Rules / Ml) selectable via config. Hot-reloadable per [`Spec/14`](../Spec/14_CONFIG.md). Tests pin Trivial for determinism. All seven advisors share the same enum; default is Trivial except Reward (Rules per Plan exit criterion).
- **Procedure:**
  1. Write `Config/decision-engine.schema.json` matching Spec/20 §4.1.
  2. Implement `ConfigSubscriber` for scope `"DecisionEngine.Models"`; on `ConfigChangedEvent`, reload the seven `ModelDescriptor` records.
  3. Implement seven Phase-1 heuristics under `Heuristics/<advisor>Heuristic.cs`.
  4. Implement seven Phase-2 rule-table parsers; the rule files are optional — missing file = Phase-1 still works.
  5. Document the Phase-3 ONNX path in `Models/<advisor>/v1.onnx` (model file is out of scope for this slot — that's the trace-trained pipeline).
- **Success criteria:** `ModeAwareActivationTests.HotReloadFlipsRotationFromTrivialToRules` green; config schema validation rejects unknown mode strings.
- **Failure modes:** missing rule file when `Mode=Rules` → fall back to Phase-1 heuristic AND log a warning; missing ONNX when `Mode=Ml` → fall back to Phase-2 (or Phase-1 if no rules file).
- **ML integration sub-bullet:** This slot IS the mode-flip substrate for all seven advisors. The actual ML model files are produced by the out-of-process trace-trained pipeline; this slot just loads them.

### S10.7 — Training-trace plumbing

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.5
- **Owned paths:**
  - `Services/DecisionEngineService/Tracing/TraceWriter.cs`
  - `Services/DecisionEngineService/Tracing/TraceFileContractTests.cs` (test wiring)
  - `tmp/test-runtime/traces/` (output directory; gitignored)
- **Read-only paths:** `Spec/20 §6.1` (trace JSONL schema), `Spec/20 §6.2` (correctness contract).
- **Spec contracts:** [`Spec/20 §6`](../Spec/20_DECISION_ENGINE.md#6-training-data-pipeline), [`Spec/13_TESTING.md`](../Spec/13_TESTING.md).
- **Goal:** Every live-validation test produces a `traces/<test-name>/<timestamp>.jsonl` carrying snapshot deltas + advice requests + outcomes per the Spec/20 §6.1 schema. Pipeline-side (Python) is out of scope; the contract is "write trace lines per schema, train against them out-of-band."
- **Procedure:**
  1. Implement `TraceWriter` that appends one JSON line per event; thread-safe append.
  2. Trigger lifecycle: test fixture opens a file at test start, closes at test end (or session end on crash).
  3. Six `kind` values per Spec/20 §6.1: `snapshot`, `advice_request`, `advice_response`, `objective_transition`, `task_terminal`, `outcome`.
  4. Trace-correctness contract tests enforce request/response pairing and snapshot-precedes-objective-transition invariants per Spec/20 §6.2.
- **Success criteria:** `TraceFileContractTests.AdviceRequestHasMatchingResponse` + `TraceFileContractTests.ObjectiveTransitionPrecededBySnapshot` green; trace files appear in `tmp/test-runtime/traces/` after every LiveValidation run.
- **Failure modes:** disk full → trace writes fail silently; bot continues; emit warning. Schema drift between trace producer and consumer → consumer-side Python pipeline detects; not our concern.
- **ML integration sub-bullet:** This slot produces the labeled-data substrate that all Phase-3 ONNX models train on. No runtime ML in this slot itself.

### S10.8 — LiveValidation for advisor wire

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S10.1..S10.7, S10.9, S10.10, S10.11
- **Owned paths:**
  - `Tests/BotRunner.Tests/LiveValidation/DecisionEngine/` (new folder)
- **Read-only paths:** `Spec/20`, all wire-slot consumers.
- **Spec contracts:** [`Spec/20 §9`](../Spec/20_DECISION_ENGINE.md#9-test-surface), [`Spec/20 §10`](../Spec/20_DECISION_ENGINE.md#10-dynamic-progressive-invariant).
- **Goal:** One LiveValidation suite per advisor wire path:
  - `DecisionEngine_RewardSelection_GuidedByAdvice` — a quest with a 3-choice reward has the advisor recommend index 2; bot picks 2 and the snapshot's `Player.Inventory` confirms.
  - `DecisionEngine_RotationFallback_OnServiceDown` — kill the DecisionEngineService process; the bot's rotation continues correctly (Trivial fallback wins the encounter).
  - `DecisionEngine_ObjectiveTieBreak_AppearsInSnapshot` — composer tie produces an `AdviceLogEntry` with `advisor="objective"` in the next snapshot.
  - `DecisionEngine_ChatTemplate_FallsBackOnCandidateMismatch` (S10.9 partner) — advisor returns invalid template id; bot falls back to heuristic and emits a valid template.
  - `DecisionEngine_ActivityRequest_AmbiguousWhisperReturnsAmbiguousRequest` (S10.10 partner) — Shodan whisper `!run brd` with NoAdvice produces `RejectionCode.AMBIGUOUS_REQUEST` in `snapshot.recent_ondemand_echoes`.
  - `DecisionEngine_PersonalityCluster_AdvisorRespectsAvailableSet` (S10.11 partner) — advisor returns out-of-set cluster id; factory falls back to uniform sampling and `snapshot.personality.cluster_id` is empty.
  - `DecisionEngine_DynamicProgressive_AllAdvisorsForcedToNoAdvice` — replay any production trace with all seven advisors pinned to `Mode=Trivial` AND service stubbed to return `NoAdvice`; assert `roster_distance_delta ≤ 0` on every `outcome` line. This is the Spec/20 §10 invariant elevated to a live test.
- **Procedure:**
  1. Each test stages bots via `LiveBotFixture.StageBotRunner*Async` per Test Isolation Rules.
  2. DecisionEngineService is launched / stopped via the StateManager process-control surface (NOT direct `Process.Kill`).
  3. Traces produced under `tmp/test-runtime/traces/DecisionEngine_*` for off-line Spec/20 §6 consumption.
- **Success criteria:** all seven tests green on `Westworld-Test`; trace files match Spec/20 §6.2 correctness contract.
- **Failure modes:** flake-prone tests (advice timing-dependent) → bump `timeoutMs` in test config NOT in production config.
- **ML integration sub-bullet:** This slot's `DecisionEngine_DynamicProgressive_*` test IS the correctness guard for the entire ML surface — it proves removing advice cannot break the deterministic-progressive stack.

### S10.9 — ChatTemplate advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0, Plan/15 S11.1 (`IChatGenerator`).
- **Owned paths:**
  - `Exports/BotRunner/Social/TemplateChatGenerator.cs` (advice consumer)
  - `Services/DecisionEngineService/Heuristics/ChatTemplateHeuristic.cs` (Phase-1)
  - `Services/DecisionEngineService/Servers/ChatTemplateAdviceHandler.cs` (service-side dispatch)
- **Read-only paths:** `Bot/chat-templates/**` (candidate template library), `Spec/21`, `Spec/24` `ChattyLevel` knob.
- **Spec contracts:** [`Spec/20 §2.2`](../Spec/20_DECISION_ENGINE.md#22-chattemplate-advisor), [`Spec/20 §2.1`](../Spec/20_DECISION_ENGINE.md#21-proto-wire-shapes) `ChatTemplateContext` / `ChatTemplateAdvice`, [`Spec/21 §11`](../Spec/21_SOCIAL_FABRIC.md#11-ml-integration).
- **Goal:** Wire `IChatGenerator.GeneratePlanAsync(...)` to consult `GetChatTemplateAdviceAsync`. Phase-1 heuristic: round-robin among candidates with lowest `candidate_recent_use_count`. Returned plan carries advisor rationale.
- **Procedure:**
  1. Implement `ChatTemplateHeuristic` over the round-robin policy.
  2. Implement `ChatTemplateAdviceHandler` on the service side dispatching the heuristic when `Mode=Trivial`.
  3. Wire `TemplateChatGenerator.GeneratePlanAsync` to call the client; on advice with confidence ≥ 0.5 AND template id ∈ candidate set, use it; otherwise fall through to heuristic.
  4. Emit `AdviceLogEntry` with `advisor="chat_template"` to bot-local buffer (S10.5).
- **Success criteria:** `SocialFabricContractTests.ChatTemplate_AdvisorRespectsCandidateSet` green; chat outputs still satisfy `_denylist.txt` post-filter (Plan/15 S11.12).
- **Failure modes:** advice picks a template id no longer in the library (race with hot-reloaded `Bot/chat-templates/`) → drop to heuristic.
- **ML integration sub-bullet:** Phase 1 heuristic (this slot). Phase 2 rules at `Config/decision-engine/chat-template-rules.json` (S10.6). Phase 3 ONNX trained on labeled chat-engagement traces.

### S10.10 — ActivityRequest advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0, Plan/03 S2.6 (`OnDemandActivitiesModeHandler`).
- **Owned paths:**
  - `Services/WoWStateManager/Modes/OnDemandActivitiesModeHandler.cs` (advice consumer; modify, do not break Plan/03 owner)
  - `Services/DecisionEngineService/Heuristics/ActivityRequestHeuristic.cs` (Phase-1)
  - `Services/DecisionEngineService/Servers/ActivityRequestAdviceHandler.cs`
- **Read-only paths:** `Config/whisper-parser.json`, `Spec/23 §5`, `IActivityCatalog`.
- **Spec contracts:** [`Spec/20 §2.3`](../Spec/20_DECISION_ENGINE.md#23-activityrequest-advisor), [`Spec/23 §12`](../Spec/23_ONDEMAND_API.md#12-ml-integration--request-disambiguation).
- **Goal:** Wire `OnDemandActivitiesModeHandler.OnExternalActivityRequestAsync` to consult `GetActivityRequestAdviceAsync` when the whisper-parser candidate set has ≥2 entries. Phase-1 heuristic: prefer lowest-level-range candidate that contains `requesting_human_level`. Fall-soft to `RejectionCode.AMBIGUOUS_REQUEST` on advisor `NoAdvice` when no clear default exists.
- **Procedure:**
  1. Implement `ActivityRequestHeuristic` using static `Config/whisper-parser.json` defaults + level-band tiebreaker.
  2. In the mode handler, after the static parser pass, if `|candidates| ≥ 2`, call the advisor.
  3. On confident advice within candidate set: accept that id; on `NoAdvice` AND no clear static default: reject with `AMBIGUOUS_REQUEST` + populated `suggested_alternatives`.
  4. Emit `AdviceLogEntry` with `advisor="activity_request"`.
- **Success criteria:** `OnDemandApiContractTests.WhisperParser_AmbiguousBrd_AdvisorPicksByContext` + `WhisperParser_AmbiguousBrd_ReturnsAmbiguousRequest` green.
- **Failure modes:** advice picks an Activity id not in the catalog (race with hot-reload) → drop to `AMBIGUOUS_REQUEST`.
- **ML integration sub-bullet:** Phase 1 heuristic = `Config/whisper-parser.json` + level-band tiebreaker. Phase 2 rules at `Config/decision-engine/activity-request-rules.json` keyed by `(verb, zone, level_band)`. Phase 3 ONNX trained on labeled operator-confirmation traces.

### S10.11 — PersonalityCluster advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0, Plan/16 S12.1 (`IPersonalityFactory`).
- **Owned paths:**
  - `Exports/BotRunner/Personality/PersonalityFactory.cs` (advice consumer; modify, do not break Plan/16 owner)
  - `Services/DecisionEngineService/Heuristics/PersonalityClusterHeuristic.cs` (Phase-1)
  - `Services/DecisionEngineService/Servers/PersonalityClusterAdviceHandler.cs`
- **Read-only paths:** `Config/personalities.json`, `Spec/24`.
- **Spec contracts:** [`Spec/20 §2.4`](../Spec/20_DECISION_ENGINE.md#24-personalitycluster-advisor), [`Spec/24 §11`](../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering).
- **Goal:** Wire `IPersonalityFactory.Create(accountName)` to consult `GetPersonalityClusterAdviceAsync` at profile-generation time (one-shot per bot lifetime). Phase-1 heuristic: uniform sample over `Config/personalities.json` `PersonalityMix`. Cluster id biases the `HashStableRandom`-derived knob sampling.
- **Procedure:**
  1. Implement `PersonalityClusterHeuristic` as uniform `PersonalityMix` sampling; returns empty `recommended_cluster_id` (cluster_id only meaningful when ONNX is loaded).
  2. Factory's `Create(accountName)` calls the advisor before the per-knob PRNG sampling; advisor result chooses a cluster centroid that biases each knob's sample range.
  3. On `NoAdvice` or out-of-set cluster id, factory falls back to uniform mix; `snapshot.personality.cluster_id` is empty.
  4. Emit `AdviceLogEntry` with `advisor="personality_cluster"`.
- **Success criteria:** `PersonalityContractTests.PersonalityCluster_AdvisorOutsideAvailableSet_FallsBackToUniform` green; `DeterministicFromAccountName` invariant still holds (same accountName + same advisor output = same profile).
- **Failure modes:** advice returns a cluster centroid that pushes a knob outside its `Spec/24 §2` range → factory clamps to the range AND logs the trip.
- **ML integration sub-bullet:** Phase 1 = uniform mix (this slot). Phase 2 rules at `Config/decision-engine/personality-cluster-rules.json` keyed by `(class, race, faction, target_level)`. Phase 3 ONNX trained on labeled real-player population traces; off-line tool only — at runtime this is a one-shot inference per bot.

## Dynamic-progressive invariant

Per Spec/20 §10, the entire seven-advisor surface MUST preserve the
dynamic-progressive trajectory property:

1. **Dynamic.** Each advisor responds to its input context such that
   different inputs in advisor-relevant ways MAY produce different
   recommendations. Equivalent inputs produce equivalent advice
   (deterministic given fixed `mode_used`).
2. **Progressive.** Replaying any production trace with all seven
   advisors forced to `NoAdvice` MUST still produce
   `outcome.roster_distance_delta ≤ 0`. The deterministic stack
   closes goal distance without ML; advice only nudges direction.

Asserted by the slot S10.8 LiveValidation test
`DecisionEngine_DynamicProgressive_AllAdvisorsForcedToNoAdvice`. Any
PR that introduces a new advisor MUST add a corresponding
`<Advisor>_DynamicProgressive_<Property>Test` to its wire slot.

## Plan-slot cross-reference

Which Plan/14 slot ships which Spec/20 surface section:

| Spec/20 § | Plan/14 slot |
|---|---|
| §2 IDecisionEngineClient (7 RPCs) | S10.0 |
| §2.1 proto wire shapes | S10.0 |
| §2.2 ChatTemplate advisor | S10.9 (consumer also Plan/15 S11.1) |
| §2.3 ActivityRequest advisor | S10.10 (consumer also Plan/03 S2.6) |
| §2.4 PersonalityCluster advisor | S10.11 (consumer also Plan/16 S12.1) |
| §3 advice shapes | S10.0 + S10.1/2/3/4/9/10/11 emit them |
| §4 model lifecycle | S10.6 |
| §4.1 config schema | S10.6 |
| §4.2 ONNX tensor shapes | service-side ONNX runtime; not a Plan slot |
| §5 maturity phases | S10.6 (mode flip) + per-wire-slot ML sub-bullets |
| §6 trace pipeline | S10.7 |
| §6.1 trace JSONL schema | S10.7 |
| §6.2 trace correctness contract | S10.7 tests |
| §9 test surface | S10.0 unit tests + S10.8 LiveValidation |
| §10 dynamic-progressive invariant | S10.8 |
| §11 plan-slot cross-reference (mirror) | this table |

## Failure recovery

- **DecisionEngineService process not running** → `IDecisionEngineClient` returns `NoAdvice` for every call; bot stack is unaffected.
- **Model load failure on service startup** → service runs with `NoAdvice` for the affected advisor only; other advisors continue.
- **High-confidence advice contradicts correctness** (e.g. rotation advice tells a Warrior to cast a Mage spell) → the calling Task validates the recommendation against its known-spell list and drops to fallback on mismatch.

## Related specs

- [`Spec/20_DECISION_ENGINE.md`](../Spec/20_DECISION_ENGINE.md) — runtime contract.
- [`Spec/03_BOTRUNNER.md`](../Spec/03_BOTRUNNER.md) — reward selector context.
- [`Spec/05_PROGRESSION.md`](../Spec/05_PROGRESSION.md) — composer integration point.
- [`Spec/19_AOTA_RUNTIME.md`](../Spec/19_AOTA_RUNTIME.md) — `IActivity` / `IObjective` composition surface.
