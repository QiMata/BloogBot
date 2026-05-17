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

- [ ] `IDecisionEngineClient` implemented at `Exports/BotRunner/Clients/DecisionEngineClient.cs` with the four advisory methods from [`Spec/20_DECISION_ENGINE.md`](../Spec/20_DECISION_ENGINE.md).
- [ ] `IRewardSelector` consults `GetRewardAdviceAsync` and prefers high-confidence advice; falls back to first-valid on `NoAdvice`.
- [ ] `IActivityComposer.Compose(...)` consults `GetObjectiveAdviceAsync` when multiple Objectives tie on priority + travel cost; advice resolves the tie.
- [ ] `PvERotationTask` consults `GetRotationAdviceAsync` per cast window; uses advice when confidence > 0.5; falls back to per-class rotation logic otherwise.
- [ ] `PvERotationTask` consults `GetThreatAdviceAsync` when multiple aggressors of similar threat exist; advice drives target swap.
- [ ] `WoWActivitySnapshot.AdviceLog[]` projects rationale strings so tests + UI can verify why advice was applied.
- [ ] LiveValidation tests assert advice does **not** break correctness — a rotation under high-jitter advice still completes the encounter within budget.
- [ ] Three maturity phases (Trivial / Rules / ML) ship for at least the Reward advisor; Rotation + Threat + Objective ship at Trivial mode initially.

## Slots

### S10.0 — `IDecisionEngineClient` shim + transport

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 2 done.
- **Owned paths:** `Exports/BotRunner/Clients/DecisionEngineClient.cs`, `Exports/BotCommLayer/Models/ProtoDef/decision-engine.proto`, `Services/DecisionEngineService/Listeners/`
- **Spec contracts:** [`Spec/20_DECISION_ENGINE.md`](../Spec/20_DECISION_ENGINE.md)
- **Goal:** Land the four advisory protobuf RPCs + the C# client with 50 ms default timeout + `NoAdvice` fail-soft semantics.
- **Success criteria:** unit tests `DecisionEngineClientTests.NoAdvice_OnTimeout` + `NoAdvice_OnServiceDown` green.

### S10.1 — Reward selector advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:** `Exports/BotRunner/Activities/RewardSelector.cs`, `Tests/BotRunner.Tests/Activities/RewardSelectorTests.cs`
- **Goal:** Replace the trivial first-valid selector with one that consults `GetRewardAdviceAsync` and uses advice when confidence > 0.5. Maintain the always-picks invariant.
- **Success criteria:** `RewardSelectorTests.PrefersDecisionEngineAdvice` + `FallsBackOnNoAdvice` green.

### S10.2 — Objective composer tie-breaker

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0, Phase 2 `IActivityComposer` lands.
- **Owned paths:** `Services/WoWStateManager/Activities/Composers/*Composer.cs`
- **Goal:** When the composer's deterministic sort produces a tie on (band, weight, soonest-expiring, lowest-travel, fanout), consult `GetObjectiveAdviceAsync` for the tiebreaker. Deterministic fallback by Objective-id when advice is `NoAdvice`.

### S10.3 — Rotation advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:** `Exports/BotRunner/Tasks/PvERotationTask.cs`, `BotProfiles/*/Tasks/PvERotationTask.cs`
- **Goal:** Per-cast-window advisory call. Use `RecommendedSpellId` when confidence > 0.5; otherwise follow the per-class rotation. Variance from [`Spec/24`](../Spec/24_BEHAVIORAL_VARIATION.md) applies on top.

### S10.4 — Threat advisor wire

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:** `Exports/BotRunner/Tasks/PullStrategyTask.cs`, per-class tank PullTargetTask
- **Goal:** When multiple aggressors are within threat-swap window, advisory call resolves target choice. Tank rotations only.

### S10.5 — `AdviceLog` snapshot projection

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.1..S10.4
- **Owned paths:** `Exports/BotCommLayer/Models/ProtoDef/communication.proto`, `Exports/BotRunner/SnapshotBuilder.cs`
- **Goal:** Add `repeated AdviceEntry advice_log = N` to `WoWActivitySnapshot`. Each entry carries `(at, advisorKind, recommendedValue, confidence, applied, rationale)`. Bounded ring buffer (last 20 entries) to keep snapshot size reasonable.

### S10.6 — Mode-aware advisor activation

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.0
- **Owned paths:** `Config/decision-engine.json`, `Services/DecisionEngineService/ModelDescriptor.cs`
- **Goal:** Per-advisor `Mode` enum (Trivial / Rules / Ml) selectable via config. Hot-reloadable per [`Spec/14`](../Spec/14_CONFIG.md). Tests pin Trivial for determinism.

### S10.7 — Training-trace plumbing

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S10.5
- **Owned paths:** `Services/DecisionEngineService/Tracing/`, `tmp/test-runtime/traces/`
- **Goal:** Every live-validation test produces a `traces/<test-name>/<timestamp>.jsonl` carrying snapshot deltas + advice requests + outcomes. Pipeline-side (Python) is out of scope; the contract is "write traces, train against them out-of-band."

### S10.8 — LiveValidation for advisor wire

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S10.1..S10.7
- **Goal:** Two LiveValidation suites:
  - `DecisionEngine_RewardSelection_GuidedByAdvice` — a quest with a 3-choice reward has the advisor recommend index 2; bot picks 2 and the snapshot's `Player.Inventory` confirms.
  - `DecisionEngine_RotationFallback_OnServiceDown` — kill the DecisionEngineService process; the bot's rotation continues correctly (trivial fallback wins the encounter).

## Failure recovery

- **DecisionEngineService process not running** → `IDecisionEngineClient` returns `NoAdvice` for every call; bot stack is unaffected.
- **Model load failure on service startup** → service runs with `NoAdvice` for the affected advisor only; other advisors continue.
- **High-confidence advice contradicts correctness** (e.g. rotation advice tells a Warrior to cast a Mage spell) → the calling Task validates the recommendation against its known-spell list and drops to fallback on mismatch.

## Related specs

- [`Spec/20_DECISION_ENGINE.md`](../Spec/20_DECISION_ENGINE.md) — runtime contract.
- [`Spec/03_BOTRUNNER.md`](../Spec/03_BOTRUNNER.md) — reward selector context.
- [`Spec/05_PROGRESSION.md`](../Spec/05_PROGRESSION.md) — composer integration point.
- [`Spec/19_AOTA_RUNTIME.md`](../Spec/19_AOTA_RUNTIME.md) — `IActivity` / `IObjective` composition surface.
