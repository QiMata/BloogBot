# Audit — Test Isolation Violations (2026-05-14)

> **Snapshot date:** 2026-05-14 at commit `bba10488` (BRM ascent Phase 2 Surface H shipped).
> **Auditor:** `monorepo-reviewer` agent. Surfacing only — no autofixes.
> **Scope:** every test under `Tests/` exercised against the four-layer
> hierarchy in [`Spec/18_TERMINOLOGY.md`](../Spec/18_TERMINOLOGY.md):
> `Activity → Objective → Task → Action`. The rule under test:
> **tests must drive Activities, not Actions** (assert against bot
> behavior after world-state setup, not by remote-controlling the bot
> through raw `SendActionAsync` dispatches in the test body).

## Summary

| Category | Count | Severity |
|---|---:|---|
| A — direct `SendActionAsync` in test body | **97 sites across 52 files** (~117 total including helpers) | High |
| B — direct service construction in tests | 16 sites (`new BotRunnerService(...)`) + 1 (`DecisionEngineRuntime`) | Medium (mocked unit-test pattern; defensible) |
| C — direct protobuf channel in tests | 0 against StateManager | Clean |
| C-borderline — protobuf to SceneData/Pathfinding | 14 sites (4 SceneData, 5 SceneData integration, 5 pathfinding load) | Low (IPC-layer tests by design) |
| D — direct pathfinding-service in test body | 3 sites (`BotRunnerServiceTests.cs:188,201,215` — mocked) | Low (mocked) |

## Exempt by directive (DO NOT count as violations)

- `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` and partials
  (`.BotChat.cs`, `.Assertions.cs`, `.ServerManagement.cs`,
  `.ShodanLoadout.cs`, `.TestDirector.cs`)
- `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`
- Any `SendActionAsync` dispatch through `LiveBotFixture.ResolveBotRunnerActionTargets(...)`
- Shodan-targeted dispatches (production GM Liaison / test director — see
  [`Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md`](../../Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md))

## Category A — direct `SendActionAsync` in test body

The 97 sites cluster in `Tests/BotRunner.Tests/LiveValidation/`. Each
test dispatches a raw `ActionMessage` to a test bot account
(TESTBOT1/TESTBOT2/etc.) inside a `[Fact]` / `[Theory]` body or a
test-class-local helper, bypassing `DecisionEngine`. Full file list
preserved in the appendix; spot examples by family:

| Family | Representative file | Sites | Notes |
|---|---|---:|---|
| Long pathing | `LiveValidation/LongPathingTests.cs:189,297,683,836,995` | 5 | `ActionType.TravelTo` dispatch — high visibility because pathfinding is active |
| Movement parity | `LiveValidation/MovementParityTests.cs:266,272,624,626,655,814,823,824,831,832` | 10 | `StartMovement`/`StopMovement`/`SetFacing`/`Goto` paired to FG+BG — class-local helpers (`DispatchBothAsync`, `DispatchPairAsync`) live in the test file, not fixture |
| Taxi parity | `LiveValidation/TaxiTransportParityTests.cs` (12 sites) | 12 | Recording + flightmaster + taxi-node dispatch paired FG+BG |
| Combat | `LiveValidation/CombatLoopTests.cs:85,86` + `WandAttackTests.cs:98,150,176` + `CombatBgTests.cs:69` + `CombatFgTests.cs:69` | 6 | Direct `Attack`/`StopAttack` |
| Raids | `LiveValidation/RaidFormationTests.cs:55,76,99,110,133` + `Raids/RaidCoordinationTests.cs:82,153,184,201,213,241` | 11 | InviteToGroup / Convert / Disband |
| BGs | `Battlegrounds/{Ab,Av,Wsg}ObjectiveTests.cs` + `BattlegroundEntryTests.cs` + `BgInteractionTests.cs` + `CombatBgTests.cs` | ~15 | Objective interact + queue dispatch |
| Professions | `FishingProfessionTests.cs` (3) + `GatheringProfessionTests.cs` (2) + `CraftingProfessionTests.cs` (1) | 6 | StartFishing / interact / craft action |
| Economy | `AuctionHouseTests.cs` + `BankInteractionTests.cs` + `BankParityTests.cs` + `VendorBuySellTests.cs` (3) + `MailParityTests.cs` + `MailSystemTests.cs` | 8 | Interact / mail action |
| Quests | `IntegrationValidationTests.cs` (3) + `QuestTestSupport.cs:61` + `NpcInteractionTests.cs:347` | 5 | Quest interact / accept |
| Movement infra | `CornerNavigationTests.cs` (2) + `MovementSpeedTests.cs` + `NavigationTests.cs:279` + `TileBoundaryCrossingTests.cs` (2) + `TransportTests.cs:195` + `TravelPlannerTests.cs:73` | 8 | Raw goto / TravelTo |
| Inventory / loadout | `EquipmentEquipTests.cs:207` + `UnequipItemTests.cs` (3) + `BuffAndConsumableTests.cs` (2) + `ConsumableUsageTests.cs:72` + `MountEnvironmentTests.cs:256` | 8 | Equip / use item / mount |
| Other / direct dispatch | `AckCaptureTests.cs` (2) + `DeathCorpseRunTests.cs` (4) + `Dungeons/SummoningStoneTests.cs` (2) + `EconomyInteractionTests.cs` (2) + `GroupFormationTests.cs` (2) + `MageTeleportTests.cs` (2) + `MapTransitionTests.cs` + `PetManagementTests.cs` (2) + `Scenarios/TestScenarioRunner.cs` + `SpellCastOnTargetTests.cs` + `SpiritHealerTests.cs` + `TaxiTests.cs` (5) + `TradeTestSupport.cs` + `ChannelTests.cs` + `LootCorpseTests.cs` (3) | ~30 | |

`SendActionAndWaitAsync` (used at `CraftingProfessionTests.cs:146`) is
the same dispatcher family — same violation. ActionMessage construction
in test bodies: **122 occurrences of `new ActionMessage` across 58
files** under `LiveValidation/`.

## Category B — direct service construction

Borderline. No raw `new BotRunner(...)`, `new StateManagerWorker(...)`,
or `new ForegroundBotRunner(...)` / `new BackgroundBotRunner(...)` in
any test. The flagged sites construct `BotRunnerService` (a *service*
component used internally by BotRunner) with mocked dependencies:

- `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs:46,97,140,175,215,271,314,336,364,387,410,433,456,479,503,527` (16 sites)
- `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs:459`
- `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs:75`
- `Tests/BotRunner.Tests/BotRunnerServiceInventoryFallbackTests.cs:162`
- `Tests/BotRunner.Tests/BotRunnerServiceLoadoutDispatchTests.cs:268,298`
- `Tests/BotRunner.Tests/BotRunnerServiceFishingDispatchTests.cs:94`
- `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs:284`
- `Tests/PromptHandlingService.Tests/DecisionEngineRuntimeTests.cs:93` — `new DecisionEngineRuntime(...)`

These are legitimate unit-test patterns (mocked `IObjectManager` +
`IDependencyContainer`); they exercise service-internal logic in
isolation. They do NOT violate the spirit of the test-isolation rule
because they are not LIVE tests against a running bot. Classified as
"Category B — defensible unit-test pattern."

## Category C — direct protobuf channel

- **Against StateManager:** 0 sites. Clean.
- **Against SceneData / Pathfinding service** (IPC-layer tests, not LIVE-bot tests):
  - `Tests/BotRunner.Tests/IPC/StateManagerLoadTests.cs:37,167,342,471` — 4 sites (custom in-test `LoadTestServer` subclass + 3 clients; performance/load testing of the IPC pipeline itself)
  - `Tests/BotRunner.Tests/IPC/ProtobufSocketPipelineTests.cs:51,91,114,143,183,224,321,342,394,438,464` — 11 sites (custom `TestSocketServer` + 10 clients; protocol-contract tests)
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataTileLoadTests.cs:55,80,141,232` — 4 sites
  - `Tests/WoWSharpClient.Tests/Movement/SceneDataClientIntegrationTests.cs:21,206,224,244,266` — 5 sites
  - `Tests/PathfindingService.Tests/ProtobufSocketServerLoggingTests.cs:11,106` — 2 sites

These are IPC-layer tests by design — they verify the protobuf socket
contract itself, not bot behavior. NOT counted as violations.

## Category D — direct pathfinding-service in test body

- `Tests/BotRunner.Tests/BotRunnerServiceTests.cs:188,201,215` — 3 sites
  call `pathfindingClient.GetPath(...)` in `[Fact]` bodies to unit-test
  `BotRunnerService.ResolveNextWaypoint` against a mocked pathfinding
  client. The pathfinding client is a mock here, not the real service.
  Classified as **Low** severity.

No LiveValidation tests call PathfindingService methods directly in the
test body.

## Why the cluster exists

The LiveValidation suite was written as **"remote-control the bot to
perform action X, then assert"** rather than **"set up world state,
assign an Activity, and assert that DecisionEngine selects Activity Y."**
This was reasonable when the priority was building the action dispatch
substrate (still Phase 1 work, per
[`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`](../Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md)).
It becomes a liability once the runtime `IActivity` / `IObjective` layer
lands in Phase 2 ([`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](../Plan/03_PHASE2_ONDEMAND_ENGINE.md)
slot S2.0), because:

1. The remote-control pattern bypasses `DecisionEngine`, so it cannot
   detect DecisionEngine regressions.
2. It bypasses the `ActivityResolver`, so it cannot detect when a new
   Activity catalog row fails to wire to a Task family.
3. It bypasses the upcoming `IActivity.NextAction(snapshot)` pump, so it
   cannot detect when a sub-Objective fails to transition.

## Phase 1 vs Phase 2 + Phase 5.x test rewrite

Refactoring the 97 LiveValidation sites is **not** Phase 1 scope:

- **Phase 1 (today, in flight)** closes the IBotTask substrate and the
  ActionType dispatch surface. Today's remote-control tests verify that
  every Action dispatches correctly — that is *exactly* the contract
  Phase 1 needs to verify, even if the test shape inverts the layer
  ordering.
- **Phase 2 (slot S2.0)** lands the runtime `IActivity` / `IObjective`
  contracts. Until S2.0 lands there is no `IActivity` to declare against
  in a test, so the Activity-driven test pattern is not implementable.
- **Phase 5.x (NEW, opened by this audit)** rewrites the 97 sites to
  the Activity-driven pattern, once S2.0 lands. Tracked at
  [`Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md`](../Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md).

This audit is the input for that Phase 5.x slot. The rule itself
([WWoW CLAUDE.md → Test Isolation Rules](../../CLAUDE.md#test-isolation-rules))
applies immediately to **new** tests: new LiveValidation tests must
declare an Activity and assert against Task / snapshot state, not raw
ActionMessage dispatch.

## Appendix — full per-file list

(Truncated; see the original `monorepo-reviewer` agent transcript at
`C:\Users\lrhod\AppData\Local\Temp\claude\e--repos\b5cb00a6-823e-4014-b167-fc207c64f4c3\tasks\a00a37f15ca4bd3f4.output`
for the exhaustive 117-site enumeration. The category table above
preserves every distinct file with its violation count; the appendix
exists for line-by-line reference during the Phase 5.x refactor.)
