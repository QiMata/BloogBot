# Plan 16 — Phase 12: Behavioral variation

> **Goal.** Ship the `PersonalityProfile` per-bot knobs that satisfy
> Vision acceptance criterion 4 — a passive observer of the population
> cannot pick out the bots by behavioral signatures.
>
> **Entry pre-requisite.** Phase 11 done (social fabric is in place;
> chat and AH-posting variance is the most visible knob). Phase 1
> done (`IBotTask` substrate stable enough to absorb jitter).

## Exit criteria

- [ ] `PersonalityProfile` record + `IPersonalityFactory` ship per [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md).
- [ ] Every personality knob has at least one consumer wired:
  - Timing knobs in `BotTask.OnPushedAsync`, `PvERotationTask`, `LootCorpseTask`, `InteractWithNpcTask`.
  - Route knobs in `PathfindingClient` post-processor + `KillObjectiveTask.PickHotspot`.
  - Economy knobs in `AuctionHousePostTask`.
  - Social knobs in `WhisperReplyHandler`, `IChatGenerator`.
  - Reward knob in `IRewardSelector`.
- [ ] Mix config `Config/personalities.json` hot-reloadable; default mix Quiet/Normal/Talkative 45/45/10.
- [ ] Operator UI exposes per-bot personality editor; overrides persist in `Config/character-overrides.json`.
- [ ] `Deterministic` profile available for unit tests; live-validation tests opt into deterministic mode.
- [ ] Distribution test: over 1000-bot mix sample, knob distributions match config ±5%.
- [ ] Correctness invariant test: high-jitter bot still completes a representative combat / quest / travel Objective within the budget.
- [ ] Snapshot projection: `WoWActivitySnapshot.personality` (proto field 45 per [`Spec/24 §9`](../Spec/24_BEHAVIORAL_VARIATION.md#9-snapshot-projection)) carries `PersonalityProj` with `personality_hash` + `cluster_id` so tests can verify the right personality applied.
- [ ] PersonalityCluster advisor (Plan/14 S10.11) wired at profile-generation time so per-bot variation can be drawn from learned real-player clusters when an ONNX model is loaded; fail-soft to uniform `PersonalityMix` sampling.

## Slots

### S12.1 — `PersonalityProfile` record + factory

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/GameData.Core/Models/Personality/PersonalityProfile.cs`
  - `Exports/GameData.Core/Models/Personality/ChattyLevel.cs`
  - `Exports/GameData.Core/Models/Personality/RewardPriority.cs`
  - `Exports/BotRunner/Personality/IPersonalityFactory.cs`
  - `Exports/BotRunner/Personality/PersonalityFactory.cs`
  - `Exports/BotRunner/Personality/HashStableRandom.cs`
- **Read-only paths:** `Spec/24 §2`, `Spec/24 §3`, `Spec/24 §7` (Deterministic singleton), `Config/personalities.json` (S12.8 owner).
- **Spec contracts:** [`Spec/24_BEHAVIORAL_VARIATION.md §2`](../Spec/24_BEHAVIORAL_VARIATION.md#2-personality-knobs), [`Spec/24 §3`](../Spec/24_BEHAVIORAL_VARIATION.md#3-generation-policy), [`Spec/24 §11`](../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering), [`Spec/20 §2.4`](../Spec/20_DECISION_ENGINE.md#24-personalitycluster-advisor).
- **Goal:** Implement the record + `HashStableRandom(accountName)` PRNG + `PersonalityFactory.Create(accountName)`. Ship `PersonalityProfile.Deterministic` as a static singleton. Factory **must** call `IDecisionEngineClient.GetPersonalityClusterAdviceAsync(...)` at profile generation time per Spec/24 §11 (one-shot, not per-tick); fall-soft to uniform `PersonalityMix` sampling on `NoAdvice`.
- **Procedure:**
  1. Define `PersonalityProfile` record with the 20 fields from Spec/24 §2; mark all fields `init` to enforce immutability.
  2. Implement `HashStableRandom` over a SHA-256(accountName) seed yielding a deterministic `System.Random` substitute.
  3. `PersonalityFactory.Create(accountName)`:
     - Build `PersonalityClusterContext` from accountName + `CharacterRosterGoal` snapshot inputs.
     - Call `GetPersonalityClusterAdviceAsync` with a 500 ms budget (Spec/20 §4.1 `personality_cluster.budgetMs`).
     - On `NoAdvice` or out-of-set cluster id, use the Phase-1 uniform-mix heuristic.
     - On valid cluster id, bias each knob's sample range by the cluster centroid (centroid file loaded by Plan/14 S10.11 service-side).
  4. Emit `AdviceLogEntry` with `advisor="personality_cluster"` to bot-local buffer (Plan/14 S10.5).
  5. `PersonalityProfile.Deterministic` is a frozen singleton with all jitters at 0 and all flags false; tests opt in via `PersonalityFactory.UseDeterministicForTests(...)`.
- **Success criteria:** `PersonalityContractTests.PersonalityFactory_DeterministicFromAccountName` green; advisor wire test green; `Deterministic` profile produces zero variance across runs.
- **Failure modes:** PRNG seed collision (2 accountNames hashing to same seed) → astronomically unlikely; not handled.
- **ML integration sub-bullet:** This slot wires the Spec/20 §2.4 advisor (Plan/14 S10.11). Phase 1 = uniform `PersonalityMix`. Phase 2 = `Config/decision-engine/personality-cluster-rules.json`. Phase 3 = `Models/personality_cluster/v1.onnx` trained on labeled real-player traces (off-line). Mix-distribution learner (tracker ML hook for this row) is the off-line tool that authors the Phase-2/3 inputs from Spec/20 §6 traces.

### S12.2 — `BotTaskContext` carries `Personality`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1
- **Owned paths:**
  - `Exports/BotRunner/Tasks/BotTaskContext.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs` (add `Jitter(string knob)` helper)
- **Read-only paths:** `Spec/24 §6` (variance application via base class).
- **Spec contracts:** [`Spec/24 §6`](../Spec/24_BEHAVIORAL_VARIATION.md#6-variance-application-via-task-base-class).
- **Goal:** Add `PersonalityProfile Personality { get; }` to the context; populate from `IPersonalityFactory.Create(accountName)` at BotRunner construction. Add `Jitter(string knob)` helper on `BotTask` base returning `TimeSpan.FromMilliseconds(ctx.Personality.GetMs(knob))`.
- **Procedure:**
  1. Extend `BotTaskContext` with `PersonalityProfile Personality { get; init; }`.
  2. `BotRunnerService` constructor calls `IPersonalityFactory.Create(account.Name)` once and stores; passed to every Task it creates.
  3. `BotTask.Jitter(string knob)` reflection-or-switch-statement returning the named knob's value.
  4. Override-aware: when an operator override exists for this account (S12.9), `BotTaskContext.Personality` reflects the override, not the factory output.
- **Success criteria:** unit test `BotTaskContextTests.PersonalityFlowsToChildTasks` green; every BotTask test that needs determinism uses `PersonalityProfile.Deterministic`.
- **Failure modes:** factory throws → BotRunner fails to start with explicit error (do NOT silently fall to Deterministic in production).
- **ML integration sub-bullet:** none — pure plumbing slot.

### S12.3 — Timing-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2
- **Owned paths:**
  - `Exports/BotRunner/Tasks/BotTask.cs` (OnPushedAsync ReactionTime)
  - `Exports/BotRunner/Tasks/Combat/PvERotationTask.cs`
  - `Exports/BotRunner/Tasks/LootCorpseTask.cs`
  - `Exports/BotRunner/Tasks/InteractWithNpcTask.cs`
  - `Exports/BotRunner/Tasks/Travel/GoToTask.cs`
- **Read-only paths:** `Spec/24 §2` knob ranges, `Spec/24 §4` consumer table.
- **Spec contracts:** [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands).
- **Goal:** Wire 5 timing knobs into the listed tasks per Spec/24 §4:
  - `ReactionTimeJitterMs` → `BotTask.OnPushedAsync` initial wait (50-250 ms).
  - `IntraRotationJitterMs` → `PvERotationTask` between cast windows (20-80 ms).
  - `WaypointPatienceMs` → `GoToTask` stuck-detection threshold (1500-4000 ms).
  - `PostKillLootDelayMs` → `LootCorpseTask` initial wait (300-1200 ms).
  - `NpcInteractApproachMs` → `InteractWithNpcTask` post-arrival wait (500-1500 ms).
- **Procedure:**
  1. Each consumer task replaces its hard-coded delay with `await Task.Delay(Jitter("<knob-name>"), ct)`.
  2. Document each call site with a `// Spec/24 §4 row` comment per CLAUDE.md "non-obvious WHY" guidance.
- **Success criteria:** `PersonalityIntegration_RotationCorrectnessUnaffectedByJitter` green; deterministic tests still produce zero-variance traces (use `PersonalityProfile.Deterministic`).
- **Failure modes:** knob value somehow exceeds Spec/24 range → `BotTaskContext` clamps to range with a debug-log warning (defensive; should be caught at factory).
- **ML integration sub-bullet:** none — knob values come from S12.1 factory (which IS the ML consumer site).

### S12.4 — Route-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2
- **Owned paths:**
  - `Exports/BotRunner/Clients/PathfindingClient.cs` (post-processor; **read-only on Services/PathfindingService — pathfinding freeze active**)
  - `Exports/BotRunner/Tasks/Questing/QuestingTask.cs`
  - `Exports/BotRunner/Tasks/Combat/KillObjectiveTask.cs`
- **Read-only paths:** `Services/PathfindingService/` (FREEZE per CLAUDE.md rule R4), `tools/MmapGen/` (mesh fixes go here, not here), `Spec/24 §4` route rows.
- **Spec contracts:** [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands), pathfinding-freeze guardrail per [`CLAUDE.md`](../../CLAUDE.md).
- **Goal:** `PathfindingClient` post-processor adds lateral wiggle within `RouteWiggleRadiusYd` (constrained to walkable navmesh). `KillObjectiveTask.PickHotspot` applies `HotspotShuffleStrength` to the candidate ranking. `GroupQuestPreferenceWeight` consumed by `QuestingTask` Objective scoring (favors Group=1 quests).
- **Procedure:**
  1. `PathfindingClient` runs the raw Detour result through a `LateralWiggle(radius)` post-processor that perturbs each waypoint's `(x, y)` by a sample uniformly in `[-radius, +radius]`, then snaps back to navmesh via `dtNavMeshQuery.findNearestPoly(...)`. **No changes to `Services/PathfindingService`**.
  2. `KillObjectiveTask.PickHotspot` re-ranks the candidate hotspot list by mixing the deterministic-sort key with a per-bot stable-randomness term scaled by `HotspotShuffleStrength`.
  3. `QuestingTask.ScoreObjective` adds `GroupQuestPreferenceWeight * (quest.IsGroupOnly ? 1 : 0)` to the score.
- **Success criteria:** `RouteWiggle_RouteVariesByPersonality` green; route still completes (lateral wiggle never pushes off-mesh).
- **Failure modes:** wiggle pushes waypoint into a navmesh hole → `findNearestPoly` snaps back; if no near poly found, fall back to the un-wiggled waypoint (defensive).
- **ML integration sub-bullet:** none — wiggle/shuffle is mechanical, not learned.

### S12.5 — Economy-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 11 mail/AH tasks land.
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Economy/AuctionHousePostTask.cs`
  - `Exports/BotRunner/Tasks/Economy/VendorRepairTask.cs`
- **Read-only paths:** `Spec/24 §4` Economy rows, `IAuctionHouseFrame` (existing AH wire).
- **Spec contracts:** [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) Economy rows.
- **Goal:** Consume `AhPostingUnderscutPercent` (-5..+10) in `AuctionHousePostTask.PriceCalculator`; consume `AhPostingCadenceMin` (30..120) in the `econ.ah-restock` Activity between-cycle delay; consume `VendorJunkAggressively` (bool) in `VendorRepairTask` (when true, sells item quality ≤ Common; when false, only sells item quality = Junk).
- **Procedure:**
  1. `PriceCalculator.Compute(observedMinPrice, underscutPercent)` returns `observedMinPrice * (1 + underscutPercent/100.0)`.
  2. `econ.ah-restock` composer reads `AhPostingCadenceMin` and sets the inter-cycle linger duration.
  3. `VendorRepairTask.PickSellableItems` filters by `VendorJunkAggressively`.
- **Success criteria:** `AhPosting_UnderscutPercentRespected` green; bots with negative underscut produce lower-than-min postings.
- **Failure modes:** observed AH min absent (item not on AH) → fall back to vendor sell price + 10% (no advice, no jitter).
- **ML integration sub-bullet:** none — knobs are deterministic from S12.1 factory.

### S12.6 — Social-knob consumers

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 11 chat tasks land (Plan/15 S11.1 + S11.10 + S11.11).
- **Owned paths:**
  - `Exports/BotRunner/Social/WhisperReplyHandler.cs` (extend, do not break Plan/15 S11.10 owner)
  - `Exports/BotRunner/Social/TemplateChatGenerator.cs` (extend, do not break Plan/15 S11.1 owner)
  - `Exports/BotRunner/Social/PostBudgetTracker.cs` (extend, do not break Plan/15 S11.11 owner; respect personality variance ±2)
- **Read-only paths:** `Spec/24 §4` Social rows, `Spec/21 §3.2` rate-budget caps.
- **Spec contracts:** [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands), [`Spec/21 §3.2`](../Spec/21_SOCIAL_FABRIC.md#32-message-rate-budget).
- **Goal:** `WhisperReplyDelayMs` consumed in `WhisperReplyHandler.ScheduleReply`; `ChattyLevel` consumed in `TemplateChatGenerator.ShouldGenerateForChannel` (Quiet=skip 80%, Normal=skip 30%, Talkative=skip 0%); `JoinWorldChatChannels` (bool) and `AcceptRandomGuildInvites` (bool) consumed in their respective handlers; `PostBudgetTracker` caps reflect `ChattyLevel` variance ±2 per Spec/21 §3.2.
- **Procedure:**
  1. Each consumer task reads its specific knob via `BotTaskContext.Personality`; no shared mutable state.
  2. `ShouldGenerateForChannel(channel)` returns false probabilistically based on `ChattyLevel` (deterministic given a fixed snapshot tick — uses `HashStableRandom(accountName + channel + tickId)`).
  3. `PostBudgetTracker` caps = base cap (Spec/21 §3.2) + (ChattyLevel - Normal) * 2 clamped to non-negative.
- **Success criteria:** `WhisperReply_RespondsWithinSla` green; `PersonalityFactory_MixConfigRespectsConfiguredDistribution` shows chatty distribution from S12.1.
- **Failure modes:** Plan/15 S11.10 / S11.1 / S11.11 not landed → S12.6 has nothing to extend; this slot waits.
- **ML integration sub-bullet:** none — Plan/15 owns the advisor consumer surface; this slot just extends it with personality variance.

### S12.7 — Reward-knob consumer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.2, Phase 10 reward selector advisor lands (Plan/14 S10.1).
- **Owned paths:**
  - `Exports/BotRunner/Activities/RewardSelector.cs` (extend, do not break Plan/14 S10.1 owner)
- **Read-only paths:** `Spec/24 §2` `RewardPriority` enum, `Spec/03 §reward-selection`.
- **Spec contracts:** [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) Reward row, [`Spec/03 §reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection).
- **Goal:** `RewardPriority` knob feeds into the deterministic fallback path when DecisionEngine returns `NoAdvice`. Knob values: `Bis` (best-in-slot scoring), `HighestVendor` (gold maximization), `StatSpread` (avoid double-up on a stat).
- **Procedure:**
  1. `RewardSelector.SelectQuestRewardFallback(rewardSet, snapshot, personality)` switches on `personality.RewardPriority` to choose scorer.
  2. `Bis`: BiS-table lookup keyed by `(class, spec, slot)`; tied scores fall through to vendor price.
  3. `HighestVendor`: pick max `vendor_sell_price`.
  4. `StatSpread`: compute per-stat coverage from currently-equipped items; pick the reward that increases coverage on the lowest-coverage stat.
- **Success criteria:** `RewardSelectorTests.UsesRewardPriorityKnob_OnNoAdvice` green per personality variant.
- **Failure modes:** BiS table missing entry (rare class+slot combo) → fall through to HighestVendor.
- **ML integration sub-bullet:** No new advisor. Plan/14 S10.1 owns the advisor consumer; this slot owns the deterministic fallback that personality variance feeds.

### S12.8 — `Config/personalities.json` hot-reload

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Config/personalities.json`
  - `Config/schema/personalities.schema.json`
  - `Services/WoWStateManager/Config/PersonalityConfig.cs`
- **Read-only paths:** `Spec/14 §2-3` (hot-reload protocol), `Spec/24 §3` (config shape).
- **Spec contracts:** [`Spec/14_CONFIG.md §3`](../Spec/14_CONFIG.md), [`Spec/24 §3`](../Spec/24_BEHAVIORAL_VARIATION.md#3-generation-policy).
- **Goal:** Mix config + hot-reload per Spec/14. On reload, IDLE bots' next factory call uses the new mix; ACTIVE bots keep their original personality until they next log in (no mid-session personality flips).
- **Procedure:**
  1. Write JSON schema covering `PersonalityMix` + `AhPostingAggressiveSharePercent` + `GroupQuestPreferenceMean`.
  2. `PersonalityConfig` registers as `IConfigSubscriber` for scope `"Personality"`.
  3. On `ConfigChangedEvent`, validate against schema; reject with `restart_required=false` on validation failure.
  4. New config applies to future `PersonalityFactory.Create` calls only.
- **Success criteria:** `PersonalityFactory_MixConfigRespectsConfiguredDistribution` green; hot-reload test shows new bots pick up the new mix within 1 s of config change.
- **Failure modes:** invalid JSON → config rollback (Spec/14 §3 step 8); active bots unaffected. Schema-fail → reject with detailed error.
- **ML integration sub-bullet:** **Mix-distribution learner** (tracker ML hook for this row) is the off-line tool that updates `Config/personalities.json` based on real-player population trace exports. The tool reads `tmp/test-runtime/traces/*/outcome.jsonl` for distinct `accountName` rows and fits a Gaussian mixture over chat-cadence and AH-cadence axes; output is a proposed `PersonalityMix` PR for human review. NOT auto-applied.

### S12.9 — Per-bot operator override

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1, S12.8
- **Owned paths:**
  - `UI/WoWStateManagerUI/Views/PersonalityEditor.xaml`
  - `UI/WoWStateManagerUI/ViewModels/PersonalityEditorViewModel.cs`
  - `Config/character-overrides.json`
  - `Config/schema/character-overrides.schema.json`
  - `Services/WoWStateManager/Config/CharacterOverrideStore.cs`
- **Read-only paths:** `Spec/24 §8`, `Spec/14_CONFIG.md` (hot-reload per-character), Plan/04 UI host.
- **Spec contracts:** [`Spec/24 §8`](../Spec/24_BEHAVIORAL_VARIATION.md#8-operator-overrides), [`Spec/14 §6`](../Spec/14_CONFIG.md).
- **Goal:** UI panel for per-bot knob editing. Overrides persist + take effect on the bot's next snapshot tick. Validation rejects out-of-range knob values at config-save time.
- **Procedure:**
  1. UI view binds to `PersonalityEditorViewModel`; one numeric/bool control per knob.
  2. On Save, ViewModel serializes to a `CharacterOverride` record and writes via `CharacterOverrideStore.SaveAsync(...)`.
  3. Store validates against `character-overrides.schema.json`; rejects out-of-range with a UI error toast.
  4. Successful save triggers `ConfigChangedEvent` scoped to `"Personality.<accountName>"`; the affected BotRunner reloads its `BotTaskContext.Personality`.
  5. `snapshot.personality.operator_override_present` flips to true on the next snapshot tick.
- **Success criteria:** `OperatorOverride_PinsKnobValue` green; UI validation test rejects `AhPostingCadenceMin=10` (below Spec/24 §2 range of 30..120).
- **Failure modes:** save while bot offline → override persisted; takes effect on next login (no error).
- **ML integration sub-bullet:** none — operator override is the escape hatch when ML produces unwanted variance.

### S12.10 — Snapshot projection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S12.1
- **Owned paths:**
  - `Exports/BotCommLayer/Models/ProtoDef/communication.proto` (add `PersonalityProj` + field 45)
  - `Exports/BotRunner/SnapshotBuilder.cs`
- **Read-only paths:** `Spec/24 §9` (normative proto shape).
- **Spec contracts:** [`Spec/24 §9`](../Spec/24_BEHAVIORAL_VARIATION.md#9-snapshot-projection).
- **Goal:** Add `PersonalityProj personality = 45` to `WoWActivitySnapshot` per Spec/24 §9 normative shape. Tests verify the expected personality applied via this field. NOT `Player.PersonalityHash` — top-level on `WoWActivitySnapshot` per the Spec.
- **Procedure:**
  1. Extend `communication.proto` with `PersonalityProj` message (8 fields per Spec/24 §9) and `personality = 45` on `WoWActivitySnapshot`. Preserve existing field numbers.
  2. `SnapshotBuilder.BuildPersonality(personality, overridePresent)` populates the projection: `personality_hash` = SHA-256(...) → uint64, `cluster_id` from S12.1 advisor result, enum knobs, bool flags, override flag.
  3. Rebuild dependent C# projects (`BotCommLayer`, `BotRunner`); regenerate proto bindings.
- **Success criteria:** `PersonalityFactory_DeterministicFromAccountName` green (asserts via this field); proto regression tests pass.
- **Failure modes:** field-number collision with another in-flight Plan slot → coordination needed. Tracker passes 1-9 reserved fields 33-44 (Spec/19/21/22/23 deltas); field 45 is free for Spec/24.
- **ML integration sub-bullet:** none — projection is read-only out of personality state.

### S12.11 — Distribution + correctness LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S12.1..S12.10
- **Owned paths:**
  - `Tests/BotRunner.Tests/LiveValidation/Personality/` (new folder)
- **Read-only paths:** all S12.1-S12.10 implementations, `Spec/24`.
- **Spec contracts:** [`Spec/13_TESTING.md`](../Spec/13_TESTING.md), [`Spec/24 §14`](../Spec/24_BEHAVIORAL_VARIATION.md#14-test-surface).
- **Goal:** Land three LiveValidation suites:
  - `PersonalityDistributionTests` — 1000-bot factory sample asserts `snapshot.personality.chatty_level` distribution matches `Config/personalities.json` ±5% per knob; iterate all enum knobs (ChattyLevel, RewardPriority).
  - `PersonalityCorrectnessTests` — high-jitter Fury Warrior bot still kills its level-appropriate target within 1.5× expected encounter duration; high-jitter quest-completing bot still finishes a sample quest within budget.
  - `BehavioralVariation_DynamicProgressive_TimingDivergesButOutcomeIsStableTest` — Spec/24 §12 invariant.
- **Procedure:**
  1. Stage 1000 bots via `LiveBotFixture.StageBotRunnerBatchAsync(accountNames=...)`.
  2. Each test records snapshots into `tmp/test-runtime/traces/Personality_*/`.
  3. Distribution test bins snapshots by knob enum value; asserts ±5% bin counts.
  4. Correctness tests stage Fury Warrior + sample quest bot with `PersonalityProfile.Deterministic` baseline + `personality.IntraRotationJitterMs=80` variant; compare wall-clock.
- **Success criteria:** all 3 tests green on `Westworld-Test`.
- **Failure modes:** sample variance → bump sample to 2000 (factory is deterministic; test failure indicates real config drift).
- **ML integration sub-bullet:** The `BehavioralVariation_DynamicProgressive_*` test IS the correctness guard ensuring personality-cluster ML cannot break outcomes.

## Dynamic-progressive invariant

Per [`Spec/24 §12`](../Spec/24_BEHAVIORAL_VARIATION.md#12-dynamic-progressive-invariant),
Phase 12's variation MUST satisfy:

1. **Dynamic.** Two bots with different `AccountName` (and therefore
   different deterministic-PRNG personalities) running the same
   Activity MUST emit different per-tick `(ObjectiveType, dispatchedAtMs)`
   timing signatures. Asserted via the trace surface — not by reading
   internal timer state.
2. **Progressive.** Variation MUST NOT degrade Activity outcomes: a
   high-jitter bot completes the Activity within `1.5 × deterministic_
   baseline_wall_clock`. `roster_distance_delta` per Activity is ≤ 0
   regardless of personality.

Asserted at slot S12.11 via
`BehavioralVariation_DynamicProgressive_TimingDivergesButOutcomeIsStableTest`.

## ML integration umbrella

Plan/16 consumes Plan/14 S10.11 (PersonalityCluster advisor) at the
one-shot profile-generation moment per bot (Spec/24 §11). The advisor
returns a cluster centroid that biases the deterministic
`HashStableRandom(accountName)` knob sampling.

**Mix-distribution learner** (tracker ML hook for this row) is the
off-line tool described in S12.8's ML sub-bullet — fits a Gaussian
mixture over real-player population traces and proposes
`Config/personalities.json` updates as PRs for human review. The tool
ALSO produces the cluster-centroid file that Plan/14 S10.11 service-
side loads when `Mode=Rules` is selected.

Three maturity phases for the cluster advisor (mirrors Spec/20 §5):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | Uniform `PersonalityMix` sampling | S12.1 (this Plan) |
| 2 — Rules + lookup | `Config/decision-engine/personality-cluster-rules.json` | Plan/14 S10.6 + this Plan S12.8 mix-learner output |
| 3 — ONNX | `Models/personality_cluster/v1.onnx` | Plan/14 S10.6 (Mode=Ml flip) |

The mix-distribution learner is **not** a runtime advisor — it runs
Python-side, off-line, and its output is one of two artifacts: an
updated `Config/personalities.json` mix (S12.8) or a new ONNX cluster
model (Plan/14 S10.11 consumer). Either way the runtime contract is
unchanged.

## Plan-slot cross-reference

| Slot | Spec contracts |
|---|---|
| S12.1 | [`Spec/24 §2`](../Spec/24_BEHAVIORAL_VARIATION.md#2-personality-knobs), [`Spec/24 §3`](../Spec/24_BEHAVIORAL_VARIATION.md#3-generation-policy), [`Spec/24 §11`](../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering), [`Spec/20 §2.4`](../Spec/20_DECISION_ENGINE.md#24-personalitycluster-advisor) |
| S12.2 | [`Spec/24 §6`](../Spec/24_BEHAVIORAL_VARIATION.md#6-variance-application-via-task-base-class) |
| S12.3 | [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) (timing rows) |
| S12.4 | [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) (route rows), [`CLAUDE.md`](../../CLAUDE.md) pathfinding freeze |
| S12.5 | [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) (economy rows) |
| S12.6 | [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) (social rows), [`Spec/21 §3.2`](../Spec/21_SOCIAL_FABRIC.md#32-message-rate-budget) |
| S12.7 | [`Spec/24 §4`](../Spec/24_BEHAVIORAL_VARIATION.md#4-where-each-knob-lands) (reward row), [`Spec/03 §reward-selection`](../Spec/03_BOTRUNNER.md#reward-selection) |
| S12.8 | [`Spec/14 §3`](../Spec/14_CONFIG.md), [`Spec/24 §3`](../Spec/24_BEHAVIORAL_VARIATION.md#3-generation-policy) |
| S12.9 | [`Spec/24 §8`](../Spec/24_BEHAVIORAL_VARIATION.md#8-operator-overrides), [`Spec/14 §6`](../Spec/14_CONFIG.md) |
| S12.10 | [`Spec/24 §9`](../Spec/24_BEHAVIORAL_VARIATION.md#9-snapshot-projection) |
| S12.11 | [`Spec/13_TESTING.md`](../Spec/13_TESTING.md), [`Spec/24 §14`](../Spec/24_BEHAVIORAL_VARIATION.md#14-test-surface), [`Spec/24 §12`](../Spec/24_BEHAVIORAL_VARIATION.md#12-dynamic-progressive-invariant) |
| (Plan/14 S10.11) | [`Spec/20 §2.4`](../Spec/20_DECISION_ENGINE.md#24-personalitycluster-advisor) advisor wire that S12.1 consumes |

## Failure recovery

- **Personality config invalid** (bad JSON) → fall back to `Deterministic` for every bot; emit error log.
- **Operator override conflicts** (out-of-range knob value) → reject at config-save time with a schema validation error.
- **Distribution test fails on a CI run** (random-sample variance) → re-run with a larger sample (factory is deterministic; the test should be deterministic too — likely a config drift).

## Related specs

- [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md) — contract.
- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — Phase 11 surfaces consumed.
- [`Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md`](14_PHASE10_DECISION_ENGINE_INTEGRATION.md) — reward-knob hand-off to DecisionEngine.
