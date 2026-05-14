# Plan 12 — Parallel: Test Isolation Refactor

> **Parallel phase.** Runs once Phase 2 slot S2.0 (`IActivity` +
> `IObjective` runtime contracts; see
> [`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](03_PHASE2_ONDEMAND_ENGINE.md))
> lands. Until then, the rule applies to **new tests only**, not to
> the existing audit population.

## Goal

Refactor the 97 Category-A `LiveValidation/` test sites catalogued in
[`docs/Audits/test-isolation-audit.md`](../Audits/test-isolation-audit.md)
from **"remote-control the bot to perform Action X, then assert"** to
**"set up world state, assign an Activity, and assert that the bot's
Activity / Objective / Task / Action stack matches the expected
trajectory."**

Goes hand-in-hand with the four-layer hierarchy in
[`Spec/18_TERMINOLOGY.md`](../Spec/18_TERMINOLOGY.md). Closes the gap
between the Phase 0 spec ("Tests assert via StateManager APIs" —
Decision R8) and the actual test population.

## Entry pre-requisite

Phase 2 slot S2.0 complete: `IActivity` and `IObjective` interfaces
land in `Exports/BotRunner/Activities/` and `WoWActivitySnapshot` gains
`current_activity_id` / `current_objective_id` /
`current_objective_type` fields.

## Exit criteria

- [ ] Every Category-A site in
      [`docs/Audits/test-isolation-audit.md`](../Audits/test-isolation-audit.md)
      either:
      - rewritten to declare an `IActivity`, drive it through the
        BotRunner, and assert on snapshot `current_activity_id` /
        `current_objective_id` / Task-stack progression, OR
      - moved to `Tests/BotRunner.Tests/ActionDispatch/` (renamed from
        `LiveValidation/` for the cases that legitimately need to
        verify a single Action shape in isolation), OR
      - documented in the audit as a permanent exception with a
        one-paragraph rationale.
- [ ] A Roslyn analyzer (or xUnit-collection-based guard) flags new
      `_bot.SendActionAsync(...)` calls outside `LiveBotFixture*` and
      the new `ActionDispatch/` folder. CI fails on a violation.
- [ ] All BG-targeted test refactor passes physics-parity regression
      (rule R13 — physics parity comes before pathfinding).

## Why this is parallel, not Phase 5.x

Phase 5 ([`Plan/06_PHASE5_OBSERVABILITY.md`](06_PHASE5_OBSERVABILITY.md))
is about metrics + Grafana + long-term history. Putting the test
refactor inside Phase 5 would block observability work behind a
multi-cycle test rewrite. Running it parallel keeps both unblocked.

## Slots

### S12.1 — Roslyn analyzer or xUnit guard

- **Owner:** `monorepo-worker`
- **Status:** open (Phase-1 acceptable — analyzer can land BEFORE S2.0)
- **Owned paths:**
  - `Tests/Analyzers/TestIsolation.Analyzer/` (new project)
  - `Tests/BotRunner.Tests/.editorconfig` — opt-in to analyzer warnings
- **Goal:** Statically detect new violations of the test-isolation
  rule. Initial implementation: an xUnit collection fixture that runs
  before every assembly and grep-asserts that no test file under
  `Tests/BotRunner.Tests/LiveValidation/**` contains
  `_bot.SendActionAsync(` outside `LiveBotFixture*`. Upgrade to a
  Roslyn analyzer in a later slot if the grep-fixture proves too
  imprecise.
- **Acceptance:** intentional violation produces a CI failure with
  the rule citation pointing at
  [WWoW CLAUDE.md → Test Isolation Rules](../../CLAUDE.md#test-isolation-rules).
  Existing Category-A sites are GRANDFATHERED via an explicit allowlist
  file (`Tests/BotRunner.Tests/.test-isolation-allowlist.json`) that
  this slot's PR populates from the audit. The allowlist shrinks slot-
  by-slot in S12.2..S12.5 as tests are rewritten.

### S12.2 — Travel + pathfinding family rewrite

- **Owner:** `monorepo-worker`
- **Status:** blocked on S2.0 + S12.1
- **Owned paths:** `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`,
  `MovementParityTests.cs`, `CornerNavigationTests.cs`,
  `TaxiTransportParityTests.cs`, `TaxiTests.cs`,
  `TileBoundaryCrossingTests.cs`, `TransportTests.cs`,
  `MapTransitionTests.cs`, `MountEnvironmentTests.cs`,
  `NavigationTests.cs`, `MovementSpeedTests.cs`, `TravelPlannerTests.cs`
- **Goal:** Highest-visibility refactor (pathfinding work is active).
  Rewrite ~35 sites to declare `Travel` Activities with target
  Objectives, then assert on `current_activity_id` /
  `current_objective_id` / Task-stack snapshot progression rather than
  driving raw `TravelTo` / `StartMovement` / `Goto` dispatches.

### S12.3 — Combat + raid + BG family rewrite

- **Owner:** `monorepo-worker`
- **Status:** blocked on S2.0 + S12.1
- **Owned paths:** `LiveValidation/Combat*Tests.cs`, `RaidFormationTests.cs`,
  `Raids/RaidCoordinationTests.cs`,
  `Battlegrounds/*ObjectiveTests.cs`, `BattlegroundEntryTests.cs`,
  `BgInteractionTests.cs`, `CombatBgTests.cs`, `CombatFgTests.cs`,
  `CombatLoopTests.cs`, `LootCorpseTests.cs`, `WandAttackTests.cs`,
  `SpellCastOnTargetTests.cs`, `MageTeleportTests.cs`,
  `PetManagementTests.cs`
- **Goal:** ~32 sites. Mirror S12.2 pattern.

### S12.4 — Professions + economy + social family rewrite

- **Owner:** `monorepo-worker`
- **Status:** blocked on S2.0 + S12.1
- **Owned paths:** `LiveValidation/{Fishing,Gathering,Crafting}ProfessionTests.cs`,
  `{Auction,Bank,BankParity,Vendor,Mail,MailParity}*.cs`,
  `BuffAndConsumableTests.cs`, `ConsumableUsageTests.cs`,
  `EquipmentEquipTests.cs`, `UnequipItemTests.cs`,
  `GroupFormationTests.cs`, `ChannelTests.cs`,
  `EconomyInteractionTests.cs`
- **Goal:** ~20 sites.

### S12.5 — Tail family rewrite

- **Owner:** `monorepo-worker`
- **Status:** blocked on S2.0 + S12.1
- **Owned paths:** `LiveValidation/{AckCapture,DeathCorpseRun,
  IntegrationValidation,QuestTestSupport,NpcInteraction,
  Scenarios/TestScenarioRunner,SpiritHealer,TradeTestSupport}.cs`,
  `Dungeons/SummoningStoneTests.cs`
- **Goal:** Final ~10 sites + allowlist drains to empty. The S12.1
  analyzer becomes load-bearing once the allowlist is empty.

## Non-goals

- **Not in scope:** rewriting the Category-B `BotRunnerService*Tests.cs`
  unit tests (16 sites). Those are legitimate unit-test patterns with
  mocked dependencies; they do not violate the spirit of the rule.
- **Not in scope:** consolidating the IPC-layer tests
  (`StateManagerLoadTests`, `ProtobufSocketPipelineTests`,
  `SceneDataTileLoadTests`). Those exercise protocol contracts by
  design.
- **Not in scope:** Roslyn analyzer infrastructure for the WHOLE
  monorepo (D2Bot, EQBot, etc.). S12.1 is WWoW-only; cross-game
  enforcement is parallel skill work tracked in
  [`Plan/11_PARALLEL_SKILL_REFINEMENT.md`](11_PARALLEL_SKILL_REFINEMENT.md).
