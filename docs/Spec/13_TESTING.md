# Spec 13 — Testing

## The contract

**Tests assert through StateManager.** Every behavior test:

1. Configures bots via per-test `*.config.json` referencing the
   `Westworld-Test` realm and named accounts/characters from
   [`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md).
2. **Launches `WoWStateManagerUI`** as the test fixture's host process.
   The UI silently starts StateManager and connects (per
   [`Spec/09_UI.md`](09_UI.md)). The fixture obtains the same
   `IStateManagerClient` interface the UI uses and subscribes to the
   same protobuf summary stream. Tests observe live stats exactly the
   way the operator does.
3. Drives state by sending `ObjectiveMessage`s through StateManager or by
   waiting for the mode handler to dispatch them.
4. Asserts on the resulting `WoWActivitySnapshot` polled from
   StateManager.

A test that asserts on internal BotRunner state without going through
the StateManager loop is wrong and must be rewritten.

Screenshots and JSON reports are **developer aids only** — they are not
the pass/fail signal. The assertions are.

## UI as the test host

Per the 2026-05-12 design:

- `WoWStateManagerUI.exe` is the **solution's default startup project**
  and **the test fixture's host process**.
- Tests instantiate `WoWStateManagerUIFixture` (new), which boots the
  UI in a hidden-window mode, lets it spawn StateManager, then exposes
  the `IStateManagerClient` to the test.
- The UI continues running through the test suite. Operators watching
  the UI window during a test run see the same metrics the test
  fixture is asserting against.
- This is also how the UI's connection / panels get exercised on every
  test run — no separate UI integration test required.

`LiveBotFixture` migrates to inherit from `WoWStateManagerUIFixture` so
that all existing LiveValidation tests automatically pick up the
UI-host model.

## Test layers

| Layer | Goal | Server needed? | Speed |
|---|---|---|---|
| Unit | Pure logic, no IPC | No | ms |
| Contract | Proto round-trip, schema | No | ms |
| Component | Service in isolation | No (mocks) | ms–s |
| Integration | Multi-service IPC, single bot | Yes (Docker) | s |
| LiveValidation | Full StateManager + bot + server | Yes | seconds–minutes |
| Load | 50–3000 bot scale | Yes | hours |

## Polling contract

Every live test waits on **predicates with tight timeouts**:

```csharp
await bot.WaitForSnapshotConditionAsync(
    accountName,
    snap => snap.LoadoutStatus == LoadoutStatus.Complete
         && snap.Position.MapId == expectedMap,
    timeout: TimeSpan.FromMinutes(2),
    progressLabel: "automated-loadout-and-travel",
    cancellationToken);
```

Required by every wait:

- A tight `timeout` (not the test-class-level timeout).
- A `progressLabel` for log/screenshot artifact naming.
- A **crash check** that fast-fails on `bot.IsCrashed`.
- A **disconnect check** that fast-fails on
  `snapshot.Lifecycle == Disconnected` unless the test expects it.
- A **final state dump** on failure (screenshot, JSON snapshot, log
  tail) for triage.

No `Thread.Sleep`. No fixed-iteration loops. No "wait N seconds and
check". Always condition.

## Test skip policy

Tests must NEVER skip for "resource not found":

- **Walking to find resources IS the test.** Wider search routes
  validate navigation.
- **Do NOT spawn resources** (no `.gobject add`, no synthetic nodes).
- **Do NOT use `Skip.If` for resource detection failures.** Use
  `Assert.True` / `Assert.Fail`.
- **Acceptable skips:** fixture readiness (bot not connected), known
  client bugs (with crash ID).
- **Not acceptable:** "no pool at Ratchet (respawn timer)", "no nodes
  spawned", "no mob found".

## FG/BG parity tests

Every action with both an FG path and a BG path has a parity test:

```csharp
[Theory]
[InlineData(BotExecutionMode.Foreground)]
[InlineData(BotExecutionMode.Background)]
public async Task SpellCast_HeroicStrike_LandsOnTarget(BotExecutionMode mode)
{
    // shared body asserts same snapshot outcomes for both modes
}
```

Recording-driven physics tests (`Tests/Navigation.Physics.Tests/`) use
FG recordings as authority and assert BG matches within tolerance.

## Shodan rules

Shodan is the production GM liaison + test director. Tests must:

- **Never dispatch `ObjectiveType.*` against the Shodan account.**
- **Never assert on Shodan's snapshot for behavior validation.**
- Use `LiveBotFixture.ResolveBotRunnerActionTargets()` to resolve the
  non-Shodan test bots.
- All `StageBotRunner*Async` helpers throw if Shodan is the target.
- Every Shodan-shaped test logs:
  `[ACTION-PLAN] SHODAN <category>: director only, no <feature> dispatch.`

## Live test fixture contract

`Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` is the
fixture used by LiveValidation tests:

- Owns lifecycle (bot launch, server check, cleanup).
- StateManager owns coordination (mode dispatch, snapshot ingest).
- BotRunner owns execution (behavior trees, IObjectManager calls).
- Object managers expose game state.

These boundaries are R6 in the monorepo CLAUDE.md and they are enforced
in fixture code via assertions.

## GM command policy

Tests use SOAP (port 7878) for GM commands. **Never edit MaNGOS MySQL
directly** for character/server mutation.

- `LiveBotFixture.ExecuteGMCommandAsync()` — SOAP.
- `LiveBotFixture.SendGmChatCommandAsync()` — via bot chat (Shodan).
- Read-only MySQL queries (e.g. `playercreateinfo_item`) are
  acceptable.
- **No `.gm on` in tests.** Account-level GM access only.
- `.reset` subcommands strip test state between runs:
  `.reset honor|level|spells|stats|talents|items|all`.

## Test staging mode — OnDemand-equivalent by default

The vast majority of LiveValidation tests **stage state with GM
commands** before asserting on a specific gameplay surface — this
mirrors the OnDemand circumvention pattern from
[`Spec/04 §OnDemand vs Autonomous — siloed`](04_ACTIVITIES.md#ondemand-vs-autonomous--siloed).
The staging mode is correct: tests are not trying to prove that real
progression works (that's the autonomous side's job — see below);
they're proving that a specific gameplay surface (fishing, AH,
raid-formation, BG queue, dungeon clear) works **once the bot is in
the right pre-condition**.

Two staging modes:

| Mode | Used by | Pre-condition setup |
|---|---|---|
| **OnDemand-equivalent** (default) | ~95% of LiveValidation tests | `.character level <N>`, `.reset items`, `.additem`, `.modify reputation`, `.tele`, `.learn`, `.setskill` issued by `LiveBotFixture` helpers (`StageBotRunner*Async`, `EnsureShodanAdminLoadoutAsync`, `EnsureLevelAtLeastAsync`, `ApplyLoadoutAsync`). Bot lands in the desired state in seconds. |
| **Autonomous-progression** | Tests under [`Tests/BotRunner.Tests/LiveValidation/Progression/`](../../Tests/BotRunner.Tests/LiveValidation/Progression/) | NO `.character level` / NO `.additem` / NO `.modify reputation`. Test resets the character to a true L1 baseline (`.reset all`) and exercises `ProgressionPlanner.PickNextObjective` + the composer + the IBotTask chain end-to-end. The bot reaches L60 through real quest XP, real loot, real AH purchases — over simulated hours. The accelerated `Westworld-Test` realm timers (per [`Spec/16`](16_REALMS_AND_ACCOUNTS.md)) make this tractable in CI. |

### Why the two-mode split is load-bearing

The OnDemand-equivalent path lets the suite stay fast — each test
runs in seconds-to-minutes, not hours. But it **does not exercise**:

- `ProgressionPlanner.PickNextObjective(...)` actually composing an
  Objective list from a real L1 snapshot.
- The composer's quest-chain DAG walk per
  [`aota/04`](../architecture/aota/04_QUEST_CHAINS.md).
- The cheapest-source learner per [`aota/05 §9`](../architecture/aota/05_ITEM_REQUIREMENTS.md#9-ml-aided-cheapest-source-learner)
  resolving real item gates with no GM-applied inventory shortcut.
- The dynamic-progressive invariant from
  [`Spec/05 §RosterPlanner.Distance`](05_PROGRESSION.md#rosterplannerdistance--the-canonical-progression-metric)
  closing real axis distance over an Activity sequence.

The autonomous-progression tests probe those surfaces specifically.
**One autonomous test per major bracket** (L1→10 starter, L10→25
Westfall/Barrens-equivalent, L25→40 Stranglethorn-equivalent,
L40→58 Plaguelands, L58→60 attunement push) is sufficient — the
gameplay-surface tests (run in OnDemand-equivalent mode) cover the
individual surfaces.

### The pivotal autonomous test: fresh-L1 first-Objective

The starting test of the autonomous-progression suite asserts that
**a freshly-created L1 character whose `CharacterRosterGoal` points
at L60 BiS + mounts + achievements** has its
`ProgressionPlanner.PickNextObjective` return a *quest pickup*
Objective (not a gear-chase, not a raid attempt, not an
attune-step). This is the canonical "the autonomous loop knows where
to start" assertion. See
[`Tests/BotRunner.Tests/LiveValidation/Progression/AutonomousFreshL1ProgressionTests.cs`](../../Tests/BotRunner.Tests/LiveValidation/Progression/AutonomousFreshL1ProgressionTests.cs).

## Test categories (existing)

| Category | Project | Coverage |
|---|---|---|
| Basic Loop | BotRunner.Tests | Login, snapshot, teleport, level, units |
| Character Lifecycle | BotRunner.Tests | Create, items, death/revive |
| Combat | BotRunner.Tests | Melee/ranged/stop/distance |
| Consumables | BotRunner.Tests | Use item + buff check |
| Crafting | BotRunner.Tests | Learn + craft via packet path |
| Death/Corpse | BotRunner.Tests | Release/retrieve, ghost run |
| Economy | BotRunner.Tests | Bank, AH, mail, vendor buy/sell |
| Equipment | BotRunner.Tests | Equip/unequip |
| Fishing | BotRunner.Tests | Full fishing loop |
| Gathering | BotRunner.Tests | Mining/herbalism |
| Group | BotRunner.Tests | Invite + accept + cleanup |
| NPC Interaction | BotRunner.Tests | Vendor, trainer, flight master |
| Navigation | BotRunner.Tests | Short + city + cross-map |
| Quest | BotRunner.Tests | Add, complete, remove |
| Talent | BotRunner.Tests | Learn via GM, spell in snapshot |
| Loot | BotRunner.Tests | Kill → loot → verify inventory |
| Spell Cast | BotRunner.Tests | Heroic Strike on mob, verify HP delta |
| Buff Dismiss | BotRunner.Tests | Apply + dismiss |
| Movement parity | WoWSharpClient.Tests | 30+ recorded sessions |
| Physics | Navigation.Physics.Tests | Walkable, climb, jump, fall, swim |
| Packet handlers | WoWSharpClient.Tests | 937+ opcode tests |
| Pathfinding | PathfindingService.Tests | Path + route-pack |
| Recorded path replay | RecordedTests.PathingTests.Tests | Deterministic replay |
| Mock server | WoWSimulation.Tests | Mock MaNGOS |

## Required new test families (Phase 2+)

For every catalog activity row, there must be at least one
LiveValidation test that:

1. Configures a roster sufficient to fill the role template.
2. Submits the activity request via StateManager.
3. Polls until lifecycle reaches `Completed` or `Failed`.
4. Asserts:
   - Bots travelled to `TravelTarget`.
   - Group formed with the right composition.
   - Activity-specific success condition (e.g. dungeon final boss
     killed, BG match completed, fishing pool exhausted).
5. Asserts post-completion state (lease released, bots returned to
   progression).

These are added by the activity-family slots in
[`Plan/Activities/`](../Plan/Activities/).

## Training-trace capture

Every **LiveValidation** test produces a structured JSONL trace per
[`Spec/20 §6.1`](20_DECISION_ENGINE.md#61-trace-line-schema) at:

```
tmp/test-runtime/traces/<TestClass.TestMethod>/<timestamp>.jsonl
```

This is the ML pipeline's labeled-data substrate — Spec/20 §5 Phase-3
ONNX models for the seven advisors all train against these traces.
The capture is **mandatory for LiveValidation** and **off for unit /
contract / component** tests (those layers are too low to produce
meaningful (snapshot, decision, outcome) tuples).

### Opt-out

A test can opt out via an attribute:

```csharp
[Fact, NoTrainingTrace("explicit reason; e.g. fixture-stress test that produces no advice calls")]
public async Task SomeFixtureLevelTest() { ... }
```

The default-on policy is intentional — every accidental trace
omission costs the training corpus a data point. Opt-out requires a
human-readable reason that the trace harness logs at fixture
teardown.

### Trace writer lifecycle

`WoWStateManagerUIFixture` (the new test host per §UI as the test
host) opens a per-test `TraceWriter` at fixture init and flushes it
on teardown. The writer:

1. Subscribes to the StateManager snapshot stream and writes
   `kind="snapshot"` lines on each tick.
2. Subscribes to `IDecisionEngineClient` request/response events and
   writes `kind="advice_request"` + `kind="advice_response"` lines.
3. Subscribes to `IActivity.NextObjective` transitions and writes
   `kind="objective_transition"` lines.
4. Subscribes to `IBotTask` terminal callbacks (Phase 1 substrate)
   and writes `kind="task_terminal"` lines.
5. On `IActivity.OnComplete` (or test-end if Activity never reached
   completion), writes a final `kind="outcome"` line carrying
   `wall_clock_ms`, `xp_gained`, `gear_slots_filled`,
   `gold_delta_copper`, and the critical
   `roster_distance_delta` field (Spec/05 `RosterPlanner.Distance`
   delta).

### Correctness contract

The trace writer enforces Spec/20 §6.2 invariants at fixture teardown:

1. Every `advice_request` line has exactly one matching
   `advice_response` with the same `request_id`.
2. Every `objective_transition` line is preceded by a `snapshot` line
   whose `current_objective_id` equals `from_objective_id`.
3. The final `outcome` line's `roster_distance_delta ≤ 0` when
   `completion="complete"`.

A failing contract assertion is a **test failure** (not just a
warning). The trace itself is corrupt evidence.

### What does NOT go into traces

- Raw chat strings (Spec/21 §8 anti-griefing posture; templates are
  hashed before write).
- Bot account passwords or session tokens.
- Player names from foreign accounts (only the test bot's own account
  name is captured).
- High-cardinality coordinates beyond 1-meter precision (per Spec/10
  cardinality budget; positions are rounded).

### File rotation + cleanup

`tmp/test-runtime/traces/` is git-ignored. CI uploads the directory
as a build artifact on test failure; on pass, the local directory is
kept for off-line training pulls. There is no automatic cleanup —
disk pressure is the operator's signal to prune.

## Failure-reason mapping

Test-fixture failures map onto [`Spec/12`](12_ERROR_TAXONOMY.md):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| Snapshot polling timeout | `task_timeout` | with detail `"<predicate-label> <elapsed>"` |
| Bot crash during test | `bot_crash` | fixture auto-attaches WER mini-dump path |
| Disconnect mid-test | `bot_disconnected` | test fails fast unless the disconnect is the explicit assertion target |
| Trace correctness contract violation | `catalog_invalid` (with detail `trace_contract_<which>`) | fail-test-and-keep-trace as evidence |

No new Spec/12 values needed; row-15 added the broader six.

## ML integration — Test surface as labeled-data origin

**Surface.** This spec is the **producer** for the Spec/20 §6 trace
pipeline. The seven Spec/20 advisor RPCs all consume labeled data
that originates here — without LiveValidation tests, there is no
training corpus.

**No new advisor.** The test surface is observational, not decisional.
It writes traces; ML pipelines read them.

**Three maturity phases** per the trace consumer (mirrors Spec/20 §5):

| Phase | Source | Test-side responsibility |
|---|---|---|
| 1 — Heuristic | All seven advisors hand-rolled per their owning spec | Trace just records; no model exists yet |
| 2 — Rules + lookup | Per-advisor `Config/decision-engine/*-rules.json` populated from trace aggregation | Trace contributes to operator-curated rule files |
| 3 — ONNX | Per-advisor `Models/<advisor>/v<n>.onnx` trained on labeled traces | Trace IS the training input |

**Input feature vector for the off-line trainer.** A trace JSONL file
maps directly onto the ONNX feature shapes in
[`Spec/20 §4.2`](20_DECISION_ENGINE.md#42-onnx-feature-tensor-shapes-per-advisor) —
the JSONL `advice_request.context` field deserializes to the proto
`<Advisor>Context` message that becomes the input tensor.

**Output.** Off-line Python tooling produces an `.onnx` file under
`Services/DecisionEngineService/Models/<advisor>/v<n>.onnx`. The C#
runtime loads it via `ModelDescriptor` per [`Spec/20 §4`](20_DECISION_ENGINE.md#4-model-lifecycle).

**Fail-soft fallback.** If trace capture is broken (writer crashes,
disk full, etc.), the test STILL passes its behavioral assertions —
the trace is supplementary evidence, not the pass/fail signal (per
§The contract). Trainer pipelines downstream just skip the affected
test until the next clean run.

**Live-validation guard.** A "Phase-1 baseline" trace replay
(`Tests/BotRunner.Tests/Metrics/TrainingTraceCaptureContractTests.cs`
below) asserts that the corpus of currently-passing LiveValidation
tests produces a non-zero count of `kind="outcome"` lines with
`roster_distance_delta ≤ 0`. A regression that strips outcome
emission silently is caught by this guard.

## Dynamic-progressive invariant

The trace pipeline is the **test-side enforcement layer** for the
loop's dynamic-progressive invariant. Per the per-spec invariants
across Spec/19/20/21/22/23/24/05, every LiveValidation trace's
`outcome` line MUST carry `roster_distance_delta ≤ 0` for
`completion="complete"` outcomes.

The §Training-trace capture correctness contract enforces this at
fixture teardown — a trace whose final `outcome.roster_distance_delta
> 0` AND `completion="complete"` fails the test (the activity
*looked* successful but did not actually advance any roster goal,
which means either the activity is cosmetic or `RosterPlanner.Distance`
is mis-computed; either case is a regression).

Asserted by
`Testing_DynamicProgressive_LiveValidationProducesNonPositiveRosterDeltaTest`
in §Test surface below.

## Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/14/S10.7`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | `TraceWriter.cs`, `tmp/test-runtime/traces/` directory contract, trace-correctness-contract tests | §Training-trace capture |
| [`Plan/14/S10.5`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s105--advicelog-snapshot-projection) | Snapshot field 36 `advice_log[]` that traces re-emit | §Trace writer lifecycle step 2 |
| [`Plan/14/S10.8`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire) | LiveValidation suite that exercises trace capture | §Live test fixture contract |
| [`Plan/03`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md) Phase 2 OnDemand slots | Per-Activity LiveValidation tests that produce per-Activity traces | §Required new test families |
| **(no slot yet)** | `WoWStateManagerUIFixture` (new test host) | §UI as the test host |

The "no slot yet" row joins the Plan-follow-up roster (9 orphan
services through this pass: 6 from pass 11 + AnomalyDetector pass 14
+ FailureClusterer pass 15 + WoWStateManagerUIFixture).

## Test surface (trace capture itself)

Contract tests live at
`Tests/BotRunner.Tests/Metrics/TrainingTraceCaptureContractTests.cs`.
Tests assert against the trace JSONL surface, not internal
`TraceWriter` state.

- **`TraceCapture_LiveValidationFixtureEmitsOutcomeLine`** — running
  a representative LiveValidation test produces a JSONL file in
  `tmp/test-runtime/traces/<TestClass.TestMethod>/<timestamp>.jsonl`
  containing at least one `kind="outcome"` line.
- **`TraceCapture_NoTrainingTraceAttribute_SkipsCapture`** — a test
  decorated with `[NoTrainingTrace("reason")]` produces no JSONL
  file (or an empty file with a single `kind="opt-out"` marker line).
- **`TraceCapture_AdviceRequestPairsWithResponse`** — for every
  `advice_request` line in a captured trace, exactly one
  `advice_response` exists with the same `request_id`. (Spec/20
  §6.2 correctness contract enforced at the test surface.)
- **`TraceCapture_ObjectiveTransitionFollowsSnapshot`** — every
  `objective_transition` line is preceded by a `snapshot` line whose
  `current_objective_id` equals `from_objective_id`.
- **`TraceCapture_OutcomeRosterDistanceDeltaPresent`** — every
  `outcome` line has a non-null numeric `roster_distance_delta` field
  (even when the value is 0).
- **`Testing_DynamicProgressive_LiveValidationProducesNonPositiveRosterDeltaTest`** —
  scan all `tmp/test-runtime/traces/*/*.jsonl` from the last
  representative-suite run; every `kind="outcome"` line with
  `completion="complete"` MUST have `roster_distance_delta ≤ 0`.
  Cosmetic-only completions (delta = 0) are allowed but a strictly-
  positive delta is a regression.

## Existing code anchors

| Concept | File |
|---|---|
| Live fixture | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` |
| Test director helpers | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs` |
| Live logs | `TestResults/LiveLogs/<test>.log` (overwritten per run, not git-tracked) |
| Latest results | `TestResults/latest/` |
| MaNGOS server fixture | `Tests/Tests.Infrastructure/MangosServerFixture.cs` |
| Shodan policy | `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md` |
| Test patterns (monorepo) | [`../../docs/TEST_PATTERNS.md`](../../docs/TEST_PATTERNS.md) |
| Screenshots contract | [`../../docs/TEST_SCREENSHOTS.md`](../../docs/TEST_SCREENSHOTS.md) |
