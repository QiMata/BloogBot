# WWoW Autonomous — Progression Layer Plan (Phase C companion)

> **Read this before implementing any `C.*` row.** It is the
> "what already exists vs. what's missing" map for the autonomous
> planner — the layer that *chooses which Activity a bot pursues next to
> advance a long-term goal*. Phase C is **complete + consolidate +
> replace the stub**, NOT build-from-scratch.
>
> Pairs with [`CHURN_TRACKER.md`](CHURN_TRACKER.md) Phase C and
> [`00_DEFINITION_OF_DONE.md`](00_DEFINITION_OF_DONE.md) §3-4.
>
> **Last updated:** 2026-05-28.

---

## 1. The one-sentence gap

The domain knowledge is fully written in
[`../../leveling-guide/decision-engine/`](../../leveling-guide/decision-engine/)
but **no code reads it** — the runtime planner
(`Services/WoWStateManager/Progression/ProgressionPlanner.cs`) is a
hardcoded 8-priority stub that returns `null` ~95% of the time (every
source-resolver is a `// TODO`), so today a bot effectively
self-directs (grind in the current zone) instead of pursuing a planned
goal. Phase C builds the consumer.

This is the **same gap the sibling FFXI project had** ("the markdown
exists but has no runtime consumer") — and the same fix shape: a
`CharacterProgression` model + a completion predicate + an unlock-graph
runtime + a data-driven planner that implements the guide's
`PickNextAction`.

---

## 2. What already exists (do not rebuild)

| Asset | Where | State | Note |
|---|---|---|---|
| Decision-engine **data** | `docs/leveling-guide/decision-engine/` | **complete markdown, no consumer** | `README.md` has the `PickNextAction` pseudocode; `state-flags.md` (snapshot field schema); `unlock-graph.md` (prereq DAG, GATE/SOFT); `leveling-priority.md` (5 bands: Survival 1000-1999 / Class-identity 800-999 / Critical-path 600-799 / Optimal-questing 400-599 / Background 100-399); `per-bracket-actions/01..06` (action menus per level range). Content files (`classes/`, `zones/`, …) end with a `## Decision-Engine Rules` section in the README's rule shape. |
| `ProgressionPlanner` | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` | **hardcoded stub** | `GetNextAction(WoWActivitySnapshot, CharacterBuildConfig?) → ObjectiveMessage?`. 8 priority branches (survival/train/gear/rep/mount/gold/prof/quest). `ResolveGearSource` / `ResolveRepSource` / `GetProfessionLevel` are stubs that return `null`/`0`; the prof + quest branches are `// TODO`. Returns `null` (bot self-directs) almost always. Tests at `Tests/BotRunner.Tests/Progression/ProgressionPlannerTests.cs`. |
| `CharacterBuildConfig` | `Services/WoWStateManager/Settings/CharacterBuildConfig.cs` | **static config** | Per-character GOAL container: `TargetGearSet` (slot/item/priority), `ReputationGoals`, `MountGoal`, `SkillPriorities`, `QuestChains`, `GoldTargetCopper`. This is the *desired* end-state input — NOT the dynamic completion-state model. |
| `DecisionEngineService` | `Services/DecisionEngineService/` | **orphaned** | Standalone ML.NET + SQLite predictor on port 5004; watches training `.bin` files + retrains. **Not wired to StateManager at runtime** and does not read the `decision-engine/` markdown. |
| 7-advisor RPC surface | `docs/Spec/20_DECISION_ENGINE.md §2` | **spec-only** | rotation / threat / reward / objective / chat_template / activity_request / personality_cluster. No `IDecisionEngineClient` shim exists (`Plan/14 S10.0` open). |
| `RewardSelector` | only `Tests/BotRunner.Tests/Activities/RewardSelectorContractTests.cs` | **impl absent** | Contract test exists; the selector impl + advisor consult (`Plan/14 S10.1`) do not. |
| `ProgressionPlanner` wiring | `CharacterStateSocketListener` (StateManager) | exists | Calls `GetNextAction` when no combat/dungeon action is queued; if non-null, injects as the bot's current Objective (R17-correct: SM selects the Objective, BotRunner decomposes it). |

## 3. What is missing (Phase C builds this)

| Missing | Tracker row | Target file |
|---|---|---|
| `CharacterProgression` dynamic model + snapshot projection | `C.statemodel` | `Exports/GameData.Core/Progression/CharacterProgression.cs` (+ additive `[ProtoMember]` on the snapshot) |
| `IsCharacterComplete` + per-tier sub-predicates | `C.progression` | `Exports/GameData.Core/Progression/CharacterCompletionPredicate.cs` |
| Per-spec gear target enumeration | `C.itemset` | `docs/Plan/Autonomous/ITEM_TARGETS.md` |
| Unlock-graph runtime + markdown loader | `C.unlock-runtime` | `Exports/GameData.Core/Progression/UnlockGraph.cs` |
| Data-driven planner (replace the stub) | `C.goalplanner` | `Services/WoWStateManager/Progression/ProgressionPlanner.cs` (rewrite) |
| `IDecisionEngineClient` shim + `NoAdvice` default | `C.advisor-wire` | `Exports/BotRunner/Clients/DecisionEngineClient.cs` + `RewardSelector` impl |

---

## 4. `C.statemodel` — the `CharacterProgression` model (3 iters)

Create `Exports/GameData.Core/Progression/`. Add a `CharacterProgression`
record/class populated from `WoWActivitySnapshot`, covering the fields
[`state-flags.md`](../../leveling-guide/decision-engine/state-flags.md)
enumerates:

- identity: `Level`, `Class`, `Spec`, `Faction`, `Race`
- build: `TalentsSpent` (per tree), spec-complete flag
- gear: per-slot filled + a coarse `GearTier` (Starter/Dungeon/PreRaid/Raid)
- professions: per-profession skill (gather + craft) and `*At300`
- attunements: `MoltenCore`, `Onyxia`, `Bwl`, `Naxx` (bool)
- reputations: standing per needed faction (resolve faction-ids by name
  against `mangos.faction_template`, read-only; honor the `⚠ Unverified`
  flags in `QUESTIONS.md` Q-S0.9.5)
- travel: riding tier (`None`/`Apprentice`/`Journeyman`), homepoints
- currency: `GoldCopper`

Rules:
- **Additive `[ProtoMember]` only** (R10 — never renumber). Land the
  proto + the FG mirror + BG handler population.
- Resolve ids by **name** via `IGameDatabase` / read-only MaNGOS — no
  hardcoded ids.
- Do not duplicate fields the snapshot already exposes; project from
  them.

**Accept (`UNIT:CharacterProgressionTests`):** fields round-trip
protobuf + read by predicate helpers; present on a live single-bot
snapshot.

---

## 5. `C.progression` / `C.unlock-runtime` (the predicate + the DAG)

- **`C.progression`** — `CharacterCompletionPredicate.cs` with
  `IsCharacterComplete(CharacterProgression)` exactly as
  [`00_DEFINITION_OF_DONE.md` §3](00_DEFINITION_OF_DONE.md) and per-tier
  sub-predicates (`IsLevel60` / `IsSpecComplete` / `IsPreRaidGeared` /
  `IsProfessionMaxed` / `IsAttuned` / `HasRiding` /
  `NeededReputationsAtGate`). A separate `IsCharacterFullyMaxed()` holds
  the §5 stretch tiers. **Compose** the model helpers; do not duplicate.
  Accept: false on the §2.1 initial fixture, true on §2.2 terminal, +
  one per-tier fixture flipping a single missing dimension.

- **`C.unlock-runtime`** — `UnlockGraph.cs` + a markdown-table loader for
  [`unlock-graph.md`](../../leveling-guide/decision-engine/unlock-graph.md).
  Parse the `Source | Type | → | Sink` rows; distinguish **GATE** (hard
  prereq) from **SOFT** (advisory). Expose `IsAcyclic()` +
  `ArePrerequisitesMet(node, CharacterProgression)` where node predicates
  call the `C.progression` sub-predicates. Make the markdown available at
  runtime (embedded resource recommended — the guide is the only
  authoritative rule source). Accept: `UnlockGraphTests` — acyclic + gate
  eval correct per fixture (L1 fresh: leveling tier open / endgame
  closed).

`C.itemset` (the per-spec gear enumeration `ITEM_TARGETS.md`) feeds
`IsPreRaidGeared` + the `B.equipment` family; build it from the class
guides' gear sections with id-resolvable item names.

---

## 6. `C.goalplanner` — replace the stub (5 iters, R18)

**Rewrite** `ProgressionPlanner.GetNextAction` to implement the
[`decision-engine/README.md`](../../leveling-guide/decision-engine/README.md)
`PickNextAction` pseudocode:

```text
function PickNextAction(progression):
    if survival-interrupt(progression): return that          # P0 (band 1000-1999)
    eligible = []
    for action in CurrentBracket(progression.Level).Actions:  # per-bracket-actions/NN
        if action.Preconditions(progression, unlockGraph):    # GATE prereqs met
            eligible.append((action, action.Priority(progression)))  # leveling-priority bands
    if eligible empty: return GrindMobs(OptimalGrindZone)
    return eligible.SortBy(priority desc, deterministic_id asc).First()
```

R18 deletes (same commit): the 8 hardcoded priority branches + the
stubbed `ResolveGearSource` / `ResolveRepSource` / `GetProfessionLevel` /
`GetStandingThreshold` / `IsAtTierBoundary` helpers. **Keep** the SM-side
`ObjectiveMessage?`-returning shape + the `CharacterStateSocketListener`
wiring — the planner stays StateManager-side (R17: it SELECTS the
Objective; BotRunner decomposes it via `B.composer`). The selected
Objective dispatches into the `IActivityComposer` (`B.composer` / `S2.0`)
so the chosen Activity actually runs.

Terminate when `IsCharacterComplete(progression)` (the bot's long-term
goal is met → it falls through to background/economy maintenance, never
idle).

**Accept:** `UNIT:ProgressionPlannerTests` (next-action correct per
fixture: L1 → a leveling action, L40-no-mount → the mount path,
L60-gear-gap → a dungeon) **+** `LIVE:` one bot advances ≥1 unlock-graph
tier unattended.

---

## 7. `C.advisor-wire` — the ML bridge (in-scope: wire + NoAdvice only)

Build the `IDecisionEngineClient` shim (`Plan/14 S10.0`) over the
port-5004 protobuf transport exposing the 7 advisors (`Spec/20`), with a
`NoAdvice` default so the planner makes correct **deterministic**
choices and "advisors off" still completes within budget (`Plan/14`
correctness invariant). Implement the `RewardSelector` (impl absent
today) to consult the reward advisor when confidence ≥ 0.5
(`Plan/14 S10.1`). The full ONNX-trained advisor maturity is **stretch**
(§5) — this row just lands the wire + the no-op default so the rest of
the system is advisor-ready.

**Accept:** `UNIT:` advisor-wire tests + a replay with
`advisors=NoAdvice` produces `roster_distance_delta ≤ 0` (no correctness
regression).

---

## 8. Why this ordering

`C.statemodel` is pure-unit and depends only on `A.4` — it is the safest
churn and everything else in C builds on it. `C.progression` +
`C.unlock-runtime` are unit-gated logic on top of the model.
`C.goalplanner` is the integration point and needs `B.composer` (to
dispatch into) + `B.solo-xp` (so the chosen leveling action can actually
run). `C.advisor-wire` is last and bridges to the Phase D roster loop +
the ML stretch. Do them in tracker order.
