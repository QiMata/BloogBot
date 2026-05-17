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

- Timeout (default 50 ms) returns a `NoAdvice` instance.
- Service down returns `NoAdvice`.
- ML-model load failure returns `NoAdvice`.

`NoAdvice` carries no recommendation; the caller falls back to the
deterministic default (rotation default in `BotProfiles/<ClassSpec>/`,
trivial first-valid reward selection, composer's deterministic
topological sort).

Wire transport: protobuf over the same length-prefixed TCP framing as
the rest of the IPC stack. Port `5004` per
[`Spec/01_ARCHITECTURE.md`](01_ARCHITECTURE.md).

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
[`Spec/14_CONFIG.md`](14_CONFIG.md). Models are loaded in a sidecar
ONNX runtime; per-tick inference budget is 5 ms for rotation/threat,
15 ms for reward, 50 ms for objective.

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
test produces a structured trace
(`tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl`) carrying:

- snapshot deltas
- task-stack transitions
- advice requests + decisions
- outcomes (kill time, damage taken, reward picked, Objective
  completed)

DecisionEngineService's training-pipeline consumes these traces
(opt-in; off by default). The pipeline is Python-side and out of scope
for the C# service; the contract is just "write traces, point training
at them."

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

- **`DecisionEngineClientTests.NoAdvice_OnTimeout`** — service unreachable returns `NoAdvice`.
- **`DecisionEngineClientTests.NoAdvice_OnModelLoadFailure`** — missing ONNX file returns `NoAdvice`.
- **`DecisionEngineClientTests.AdviceAppearsInSnapshotAdviceLog`** — the bot's `WoWActivitySnapshot.AdviceLog[]` carries the rationale for tests / UI to verify.
- **`RewardSelectorTests.PrefersDecisionEngineAdvice`** — when advice confidence > 0.5, selector uses the recommended index.
- **`RewardSelectorTests.FallsBackOnNoAdvice`** — `NoAdvice` falls through to the trivial first-valid policy.

Live-validation tests for the wired path live alongside per-Activity
LiveValidation tests; the AdviceLog is asserted on alongside
Activity / Objective transitions.
