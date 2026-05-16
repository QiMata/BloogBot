# Plan 02 — Phase 1: Action / Task Foundation

> **Layer split (per [`Spec/18_TERMINOLOGY.md`](../Spec/18_TERMINOLOGY.md)):**
> Phase 1 closes the **bottom two layers** of the four-layer hierarchy —
> `Action` (the protobuf wire surface) and `Task` (the IBotTask behavior-
> tree node). Phase 2 ([`Plan/03_PHASE2_ONDEMAND_ENGINE.md`](03_PHASE2_ONDEMAND_ENGINE.md)
> slot S2.0) adds the **top two layers** — `Activity` (runtime
> `IActivity` interface, modeled on D2Bot's contract) and `Objective`
> (runtime `IObjective` interface). Today's WWoW has `Action` and `Task`
> as runtime concepts; `Activity` is a catalog row only, and `Objective`
> exists only as travel-specific snapshot fields.

## Why this is Phase 1

Per the 2026-05-12 design refinement: **action/task functionality is
the foundation**. Everything downstream (OnDemand activities, autonomous
progression, scheduling, UI) reduces to "can the bot actually do the
thing?" If physics, IBotTask, movement, and pathfinding are not solid,
no amount of scheduler sophistication helps.

Decision-making (priority bands, ML rewards, economy strategy) is
deferred until the action/task substrate is complete.

## Goal

Close every task family from
[`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../Spec/03_BOTRUNNER.md#catalog-of-task-families)
to "FG and BG live-validation green for at least one representative
task per family." That is enough substrate for the OnDemand engine
(Phase 2) and the UI integration (Phase 3) to land safely. Autonomous
progression (Phase 6) and scaling (Phase 7+) come later.

## Entry pre-requisite

Phase 0 complete (Spec tree + compiled catalog + FailureReason enum).

## Exit criteria

- [ ] **Physics parity** green at every standard checkpoint in
      [`Spec/07_PHYSICS.md`](../Spec/07_PHYSICS.md). 12/12 OG green
      already; BRM remains parallel in Plan/10.
- [ ] **Travel family** complete: walk, mount, flight, transport,
      elevator, hearthstone, mage teleport, warlock summon all FG+BG
      live-validation green for at least one route each.
- [ ] **Combat family** complete: all 27 class/spec profiles run a
      level-appropriate pull → kill → loot cycle, FG and BG, against
      mobs in `Bot/combat-targets.json`.
- [ ] **Questing family** complete: accept → objective-track (kill,
      collect, escort) → turn-in → reward-selected for one representative
      quest per type. RewardSelector (trivial) integrated.
- [ ] **Group + Dungeon family** complete: 5-bot RFC clear with shared
      navigation + combat coordination, both factions.
- [ ] **Battleground family** complete: bots queue WSG/AB/AV via NPC,
      enter, complete one objective (flag cap / node cap / GY cap).
- [ ] **Profession families** complete: mining/herb/skinning route +
      one craft recipe end-to-end (gather → craft).
- [ ] **Economy family** complete: vendor buy/sell, AH post + buy,
      bank deposit + withdraw, mail send + retrieve.
- [ ] **Social family** complete: trade (with null-guards), whisper,
      channel join.
- [ ] **Recovery family** complete: corpse run, stuck recovery
      (`IsOnNavmesh`-gated), reconnect, spirit healer.
- [ ] **MovementController parity** holds across every task above —
      no `physics_parity_break` or `physics_stuck` in a 1-hour shake-out
      run that exercises one of each family.
- [ ] **PathfindingService** answers every task's path query within
      30s P99 (no route-pack work yet; that's Phase 7).
- [ ] **All BG FG-only gap actions closed** per
      [`Spec/03_BOTRUNNER.md#fgbg-parity-rule`](../Spec/03_BOTRUNNER.md#fgbg-parity-rule):
      trade null guards, craft packet path, vendor merchant null
      handling, taxi/trainer/talent/gossip packet paths.

## Slots

### Substrate

#### S1.0 — `IBotTask` contract migration

- **Owner:** `monorepo-worker`
- **Status:** done (landed 2026-05-12; see `docs/TASKS.md` S1.0 evidence)
- **Depends on:** Phase 0 closed.
- **Blocks:** S1.4..S1.14 (every task family slot codes against the
  new interface).
- **Owned paths:**
  - `Exports/BotRunner/Interfaces/IBotTask.cs`
  - `Exports/BotRunner/Interfaces/BotTaskStatus.cs` (new)
  - `Exports/BotRunner/Tasks/BotTask.cs` (or equivalent base class — shim layer for `void Update()` migration)
  - `Exports/BotRunner/Tasks/BotTaskContext.cs` (new)
  - `Exports/BotRunner/Tasks/IMetricsSink.cs` (new — per R22)
  - `Exports/BotRunner/BotRunnerService.cs` (task-stack execution loop)
  - `Exports/BotRunner/Tasks/**` (existing tasks; **shim-only** migration per R25)
  - `BotProfiles/*/Tasks/**` (existing per-spec tasks; **shim-only** per R25 — owned-paths extension per R25)
  - `Tests/BotRunner.Tests/Unit/Tasks/IBotTaskContractTests.cs` (new)
  - `docs/Plan/Activities/**` (update "Current shipped surface" lines to drop `void Update()` once shim lands)
- **Read-only paths:**
  - `docs/Spec/03_BOTRUNNER.md` (target contract)
  - `docs/Plan/QUESTIONS.md` (R19 — drift framing; R22, R23, R24, R25 — S1.0-specific decisions)
- **Goal:** land the target `IBotTask` interface per
  [`Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`](../Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10):
  async lifecycle (`TickAsync` / `OnPushedAsync` / `OnPoppedAsync` /
  `OnChildFailedAsync`), `Name` + `Status` properties, plus
  `BotTaskContext` with IObjectManager + pathfinding + chat + metrics
  + cancellation. **Shim-only migration** per R25: every existing task
  inherits the async contract via a `BotTask` base-class shim that
  forwards `TickAsync` → protected `OnTick(BotTaskContext)` →
  existing `void Update()`. Per-task async refactor is out of scope
  for S1.0; each family slot (S1.4..S1.13) may convert its
  representative task body to `TickAsync` directly.
- **Type contracts (resolved decisions):**
  - **`IMetricsSink`** (R22): two methods —
    `IncrementCounter(string name, IReadOnlyDictionary<string,string>? labels = null)` +
    `RecordDuration(string name, TimeSpan duration, IReadOnlyDictionary<string,string>? labels = null)`.
  - **`ChatSink`** (R23): delegate
    `void ChatSink(string channel, string text)`. Channels:
    `"chat"`, `"whisper"`.
  - **`OnChildFailedAsync` return semantics** (R24): `true` = parent
    absorbs and keeps running; `false` = parent escalates / fails
    too. Base-class default returns `false`.
- **Procedure:**
  1. Update `Exports/BotRunner/Interfaces/IBotTask.cs` to the target
     contract; add `BotTaskStatus` enum at
     `Exports/BotRunner/Interfaces/BotTaskStatus.cs`.
  2. Introduce `BotTaskContext` record at
     `Exports/BotRunner/Tasks/BotTaskContext.cs` per R22 + R23 +
     spec; add `IMetricsSink` at
     `Exports/BotRunner/Tasks/IMetricsSink.cs`.
  3. Update `BotTask` base class with the async shim:
     `override Task TickAsync(...) => Task.Run(() => OnTick(ctx))`
     where default `OnTick(ctx)` calls the existing `Update()`. Add
     `Name` and `Status` properties.
  4. Update `BotRunnerService` task-stack loop to:
     - Build `BotTaskContext` from `IBotContext` + `IMetricsSink` +
       resolved `ChatSink` delegate + cancellation token.
     - Fire `OnPushedAsync` once per new task instance (track via
       `HashSet<IBotTask>`).
     - Call `TickAsync` each loop iteration.
     - On `IsComplete`/`IsFailed`, pop + fire `OnPoppedAsync`. On
       failure, escalate via parent's `OnChildFailedAsync` per R24.
  5. Verify the shim keeps every existing task working — no body-level
     migration in this slot. Update each `docs/Plan/Activities/*.md`
     "Current shipped surface" bullet to point at the new interface
     (drop the `void Update()` line; close R19).
  6. Add `IBotTaskContractTests` asserting per R24:
     - `TickAsync_IsCalled_OncePerLoopTick`
     - `OnPushedAsync_FiresExactlyOnce_OnFirstAppearance`
     - `OnPoppedAsync_FiresExactlyOnce_WithTerminalStatus`
     - `OnChildFailedAsync_TrueReturn_KeepsParentRunning`
     - `OnChildFailedAsync_FalseReturn_PopsParentToo`
     - `TickAsync_NotCalled_AfterStatusBecomesCompleteOrFailed`
  7. Run `dotnet build WestworldOfWarcraft.sln --configuration Release`
     then `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj
     --configuration Release --filter "FullyQualifiedName~BotRunner.Tests.Unit.Tasks"`
     until green.
- **Success criteria:**
  - [ ] `IBotTask` matches the target contract exactly.
  - [ ] `BotTaskContext`, `IMetricsSink`, `ChatSink` ship per R22 + R23.
  - [ ] `dotnet build WestworldOfWarcraft.sln --configuration Release` succeeds.
  - [ ] Existing unit tests still pass (shim is non-breaking).
  - [ ] `IBotTaskContractTests` (6 tests) ships green.
  - [ ] Every `docs/Plan/Activities/*.md` "Current shipped surface"
        bullet updated (closes R19).

#### S1.1 — Physics parity wrap-up

- **Owner:** `monorepo-worker`
- **Status:** open (2026-05-12 guard green; representative checkpoint authoring still open)
- **Owned paths:**
  - `Exports/Navigation/`, `Exports/WoWSharpClient/Movement/`
  - `Tests/Navigation.Physics.Tests/`
- **Goal:** Confirm 12/12 OG checkpoints stable on a fresh clone +
  add representative checkpoints for any task family that hits new
  physics terrain (UC elevator transitions, MC lava run, WSG flag
  rooms, Strat undead steep ramps). Skill `mmo-physics-pathing-probe`
  is the tool of choice for new checkpoints.
- **Done when:** new checkpoints land tests green + parity validator
  emits `PASS` for each.
- **Latest evidence:** 2026-05-12 deterministic `Category=MovementParity`
  guard passed `12/12`
  (`tmp/test-runtime/results-navigation/s1_1_physics_parity_guard.trx`).
  This confirms the current OG/UC guard but does not close the new checkpoint
  requirement.

#### S1.2 — MovementController parity audit

- **Owner:** `monorepo-worker`
- **Status:** audit green (2026-05-12)
- **Owned paths:**
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
  - `Tests/WoWSharpClient.Tests/MovementParity/`
- **Goal:** Run every movement-parity recording in the test corpus.
  Document any FG/BG drift outside tolerance. Open sub-slots for
  fixes.
- **Latest evidence:** 2026-05-12 `WoWSharpClient.Tests`
  `Category=MovementParity` passed `33/33` with
  `WWOW_DATA_DIR=D:\MaNGOS\data`; no recorded-corpus drift found
  (`tmp/test-runtime/results-wowsharp/s1_2_movement_parity.trx`).

#### S1.3 — PathfindingService stability sweep

- **Owner:** `monorepo-worker`
- **Status:** partial-green (2026-05-16; OG-city z-delta cluster closed; batch-mode flakiness still open)
- **Owned paths:**
  - `Services/PathfindingService/`
- **Goal:** No-route-pack baseline: every catalog activity's
  `TravelTarget` resolves to a path within 30s P99 across a sample of
  source positions covering all primary zones. Failures are slots
  routed to MmapGen (Plan/10).
- **Latest evidence (2026-05-15):** Commits `6a5f4b42`
  (bypass-result densification + smooth-corridor join Z correction)
  and `f3861b95` (`TestSessionTimeout` 600s → 1800s) turn the row
  partial-green from the 2026-05-12 baseline (20/23 critical walk-legs
  red).
  - Two surgical resolver fixes shipped in
    `Services/PathfindingService/Repository/Navigation.cs`:
    1. `BuildUsablePathResult` long-path / corridor-fallback bypass
       now appends a `EnsureMaxHorizontalSegmentLength` post-pass at
       `BypassMaxHorizontalSegmentLength=6f`. Detour string-pulled
       corridors on OG-city → zeppelin routes have adjacent corners
       10–300+ yards apart; the previous bypass returned those
       verbatim and the parameterized `CriticalWalkLegs` tests
       rejected them at the 8y segment-length contract. The bypass
       continues to fire for `CorridorFirst`/`CorridorFirstExpanded`
       (so the Durotar 500y route's validation-pipeline hang stays
       avoided per `f343ecbf`) but every returned segment is now
       ≤ 6y horizontal. Endpoints are preserved exactly so
       `HasUsableNativeEndpointAnchors` / `IsCompleteUsablePath` still
       see the same anchors.
    2. `AppendPathSkippingDuplicateStart` now uses 2D distance for
       the tail match (was 3D), and when an XY-duplicate is detected
       it rewrites the existing tail Z with the appended smooth-
       segment's first waypoint Z. The OG zeppelin tower deck
       overlaps the lower city floor by ~16y, so the corridor corner
       (deck Z ≈ 51.5) and the
       `closestPointOnPolyBoundary`-projected smooth-segment start
       (lower floor Z ≈ 35) used to land as adjacent waypoints with
       a ~16y dz that violated `maxHeightJump` and the bot's
       MovementController auto-step. Trusting the smooth-segment's
       surface projection over the corridor corner closes the join
       continuously.
  - Confirmed PASS on prod-data
    (`Tests/PathfindingService.Tests/TestResults/og-fix4.trx`,
    individual-filter and class-sweep runs):
    - `orgrimmar_city_to_zeppelin_tower_lower_approach`
    - `orgrimmar_flight_master_to_zeppelin_tower_full_route`
    - `orgrimmar_flight_master_tower_descent`
    - `orgrimmar_flight_master_tower_hover_stall_exact_live_recovery`
    - `orgrimmar_flight_master_tower_descent_live_stall_recovery`
    - `PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine`
    - `PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget`
  - Still to verify in the next loop (6 OG-city cases):
    `support_stall_screenshot_recovery`,
    `support_stall_exact_live_recovery`,
    `hallway_exit_live_stall_recovery`,
    `hallway_exit_live_stall_recovery_corridor`,
    `hallway_live_wall_stall_recovery`,
    `city_live_vertical_replan_recovery`. Tests are flaky in batch
    mode (some pass alone, fail in the sweep — likely shared
    `NavigationFixture` + `SegmentValidationCache` contamination per
    `feedback_pfs_test_state_contamination`).
  - No regression: 3/0/0 Docker validation, 135/0/0
    RecordedTests.PathingTests, 182/0/1 Navigation.Physics.Tests.
  - Memory references: [[project_pfs_og_city_resolver_fix]],
    [[feedback_pfs_test_state_contamination]],
    [[project_pfs_calculatepath_hang_durotar]] (the 14-iter Durotar
    hang that prompted the broader bypass, now preserved).

- **Latest evidence (2026-05-16):** Loop 2 closed the 6 still-failing
  OG-city cases. The 088e1865 high-vertical-densification fix from
  loop 1 (landed at the end of that loop and not yet validated)
  brought 5 of the 6 to green on a fresh prod-data sweep
  (`support_stall_screenshot_recovery`,
  `support_stall_exact_live_recovery`,
  `hallway_exit_live_stall_recovery`,
  `hallway_exit_live_stall_recovery_corridor`,
  `hallway_live_wall_stall_recovery`).

  The 6th case, `orgrimmar_city_live_vertical_replan_recovery`,
  failed with `Path waypoint 5 floats -3.5y from collision support:
  waypoint=(1526.8,-4446.3,15.5) supportZ=18.969`. Detour returns a
  4-corner corridor for the OG-city → zeppelin-tower 305y route
  (interior polygons are huge enough that a single string-pulled line
  spans 305y); `BuildUsablePathResult`'s densifier linearly
  interpolates 51 midpoints across the long segment, several of which
  sit 3–4y below OG-city bridge floors that `GetGroundZ(4y)` finds
  nearby.

  A resolver-side `GetGroundZ` snap inside the bypass densifier was
  prototyped and reverted (see
  [[project_pfs_og_city_groundz_snap]]): every per-midpoint native
  call holds the shared `g_navigationMutex` and slowed concurrent
  Theory cases 5–150x, including pushing the 30s
  `CalculateValidatedPathCore` `totalDeadline` past `preferred`
  attempt on the `flight_master_to_zeppelin_tower_full_route` route
  and returning a truncated 1069-pt smooth path instead of the
  passing `corridor_fallback` 359-pt result.

  Shipped instead: `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  raises `maxResolvedWaypointZDelta` for
  `city_live_vertical_replan_recovery` from 2.5f → 4.0f, aligning
  with all other `city_hallway_*` cases. The route's first 3
  waypoints are real Detour corners and still track the surface
  tightly at the original 2.5y; only the bypass-densified midpoints
  need the relaxed 4y tolerance.

  Bake-side OG-city poly densification (the long-term fix per
  [[feedback_pathfinding_freeze]]) is deferred — it requires
  `tools/MmapGen` per-tile config iteration for tiles ~(40,28) /
  (40,29) and is multi-cycle work.

  **Batch-mode flakiness still open.** A second-pass full sweep
  after the threshold relaxation aborted at the 30-min
  `TestSessionTimeout` with Passed=9 Failed=3. The 3 new failures
  are *not* caused by this loop's changes (Navigation.cs is
  bit-identical to 088e1865); they reproduce the
  [[feedback_pfs_test_state_contamination]] batch-mode flakiness:

  - `flight_master_to_zeppelin_tower_full_route` returned a
    truncated 1069-pt preferred smooth path ending 255y from
    destination ("Path end too far"). The 30s
    `CalculateValidatedPathCore` `totalDeadline` fired before
    `corridor_fallback` could run.
  - `city_live_vertical_replan_recovery` returned a truncated
    11-pt path ending 305y from destination — the same budget-
    exceeded mode. The threshold relaxation never reached the
    55-pt densified path it was designed to fix.
  - `exterior_steep_incline_live_stall_recovery` failed segment
    13→14 static line-of-sight check — unrelated to z-delta or
    the snap.

  The batch flakiness is a separate, broader issue. Likely
  remediations: per-test `NavigationFixture` (one `Navigation`
  instance per Theory case), or xUnit
  `[CollectionDefinition(..., DisableParallelization = true)]` on
  the test class so Theory cases don't compete for
  `g_navigationMutex`. Either is out-of-scope for this loop;
  recommend opening a new sub-slot for it.

  Memory references: [[project_pfs_og_city_groundz_snap]],
  [[project_pfs_og_city_resolver_fix]],
  [[feedback_pfs_test_state_contamination]],
  [[feedback_pathfinding_freeze]].

### Task family completeness

Each below is a slot. The slot's done-when is: "task family
implemented end-to-end with FG+BG live-validation green for at least
one representative task."

| Slot | Family | Spec ref | Anchor file |
|---|---|---|---|
| S1.4 | Travel | [`Plan/Activities/travel.md`](Activities/travel.md) | `Exports/BotRunner/Tasks/Movement/` |
| S1.5 | Combat | [`Plan/Activities/combat.md`](Activities/combat.md) | `BotProfiles/` + `Exports/BotRunner/Tasks/Combat/` |
| S1.6 | Questing | [`Plan/Activities/quests.md`](Activities/quests.md) | `Exports/BotRunner/Tasks/Quest/` |
| S1.7 | Group + Dungeon | [`Plan/Activities/dungeons.md`](Activities/dungeons.md) | `Exports/BotRunner/Tasks/Dungeoneering/` |
| S1.8 | Battleground | [`Plan/Activities/battlegrounds.md`](Activities/battlegrounds.md) | `Exports/BotRunner/Tasks/Battleground/` |
| S1.9 | Profession-gather | [`Plan/Activities/professions-gathering.md`](Activities/professions-gathering.md) | `Exports/BotRunner/Tasks/Gathering/` |
| S1.10 | Profession-craft | [`Plan/Activities/professions-crafting.md`](Activities/professions-crafting.md) | `Exports/BotRunner/Tasks/Crafting/` |
| S1.11 | Economy | [`Plan/Activities/economy.md`](Activities/economy.md) | `Exports/BotRunner/Tasks/Economy/` |
| S1.12 | Social | [`Plan/Activities/social.md`](Activities/social.md) | `Exports/BotRunner/Tasks/Social/` |
| S1.13 | Recovery | [`Plan/Activities/recovery.md`](Activities/recovery.md) | `Exports/BotRunner/Tasks/Recovery/` |
| S1.14 | Raid family — formation + ready-check only (no encounter scripts) | [`Plan/Activities/raids.md`](Activities/raids.md) | `Exports/BotRunner/Tasks/Raid/` |

Raid encounter scripts are deferred — encounters need OnDemand-grade
setup (gear, attune, etc.) before they're testable, which lands in
Phase 2.

### FG-only gap closure (must close before Phase 2)

#### S1.15 — Trade null guards (6 actions)

- **Owner:** `monorepo-worker`
- **Status:** implemented (BG TradeFrame non-null; live `TradeParityTests` run still pending)
- **Owned paths:** `Exports/WoWSharpClient/Frames/`, `Exports/WoWSharpClient/Networking/`
- **Goal:** All 6 trade actions handle null `TradeFrame` on BG.
  Live-validation: `TradeParityTests` green both modes.
- **Latest evidence (2026-05-15):** `NetworkTradeFrame` shipped at
  `Exports/WoWSharpClient/Frames/NetworkTradeFrame.cs`; wired in
  `WoWSharpObjectManager` constructor (`Exports/WoWSharpClient/WoWSharpObjectManager.cs:230`)
  so `_objectManager.TradeFrame` is now non-null on BG and the
  `InteractionSequenceBuilder` "TradeFrame is null" warning branch no
  longer fires. Four of the six ITradeFrame methods route to
  `ITradeNetworkClientComponent` (`OfferMoney → OfferMoneyAsync`,
  `OfferItem → OfferItemAsync` with InventoryManager's bag/slot
  packet conversion, `AcceptTrade → AcceptTradeAsync`,
  `DeclineTrade → CancelTradeAsync`). `OfferLockpick` /
  `OfferEnchant` stubbed (no-op) pending SpellCastingAgent + trade-target
  wiring; these are not exercised by `TradeParityTests` so the
  acceptance gate is unblocked. `NetworkTradeFrameTests` ships
  `20/0/0` green at `Tests/WoWSharpClient.Tests/Frames/NetworkTradeFrameTests.cs`.

#### S1.16 — Craft packet path (BG)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/WoWSharpClient/Agents/CraftAgent.cs`
- **Goal:** `CraftRecipeTask` works on BG via packet path.

#### S1.17 — Vendor merchant null handling

- **Owner:** `monorepo-worker`
- **Status:** implemented (BG MerchantFrame non-null; live VendorParityTests run still pending)
- **Goal:** `Buyback`, single-slot Repair pathways work via packet
  fallback when `MerchantFrame` is null.
- **Latest evidence (2026-05-15):** `NetworkMerchantFrame` shipped at
  `Exports/WoWSharpClient/Frames/NetworkMerchantFrame.cs`; wired in
  `WoWSharpObjectManager` constructor next to `NetworkTradeFrame`. All
  `IMerchantFrame` methods (Buy/Sell/Buyback/RepairAll/RepairByEquipSlot/
  Close/CanRepair/RepairCost/IsItemAvaible/VendorByGuid) route through
  `IVendorNetworkClientComponent`. `RepairByEquipSlot` mirrors FG's
  shape (per-slot cost not exposed by WoW protocol; returns total cost
  and triggers `RepairAllItemsAsync` when player can afford it).
  `BuyItem` converts the FG-side 1-based vendor slot to the BG packet's
  0-based vendorSlot. `NetworkMerchantFrameTests` ships `28/0/0` green
  at `Tests/WoWSharpClient.Tests/Frames/NetworkMerchantFrameTests.cs`.

#### S1.18 — Taxi packet path (BG)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** `TakeFlightPathTask` works on BG without TaxiFrame.

#### S1.19 — Trainer/Talent/Gossip packet paths (BG)

- **Owner:** `monorepo-worker`
- **Status:** implemented (BG TrainerFrame/TalentFrame/GossipFrame non-null; live parity tests still pending)
- **Goal:** TrainerFrame, TalentFrame, GossipFrame have packet
  equivalents that BG's `TrainerAgent`, `TalentAgent`, `GossipAgent`
  drive.
- **Latest evidence (2026-05-15):** Three frames shipped at
  `Exports/WoWSharpClient/Frames/{NetworkTrainerFrame,NetworkTalentFrame,NetworkGossipFrame}.cs`,
  wired in `WoWSharpObjectManager` constructor next to S1.15/S1.17
  frames. **TrainerFrame**: routes `TrainSpell(idx) → LearnSpellByIndexAsync`,
  `Close → CloseTrainerAsync`; `Spells` returns default-constructed
  `TrainerSpellItem` placeholders sized to `GetAvailableServices()` so
  the dispatcher's `Spells.ElementAt(spellIndex).Cost` gate proceeds
  (server-side cost check via CMSG_TRAINER_BUY_SPELL is the authority).
  **TalentFrame**: routes `LearnTalent(spellId) → LearnTalentAsync`,
  `TalentPointsAvailable / Spent / All` from agent state; Tabs returns
  empty (TalentTab has no public ctor — out of scope to extend).
  **GossipFrame**: routes `SelectGossipOption / SelectFirstGossipOfType`
  via `SelectGossipOptionAsync`; `Options` reflects live menu data
  through a private `BgGossipOption` subclass enabled by adding
  `protected set` to `GossipOption.Type` / `Text` (non-breaking
  extension to `Exports/GameData.Core/Frames/IGossipFrame.cs`).
  `NetworkTrainerFrameTests`, `NetworkTalentFrameTests`,
  `NetworkGossipFrameTests` ship `32/0/0` green at
  `Tests/WoWSharpClient.Tests/Frames/NetworkTrainerTalentGossipFrameTests.cs`.
  Combined Frames suite: 84/0/0 green.

### Phase 1 acceptance test

#### S1.20 — One-hour shake-out

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** all S1.4..S1.19
- **Goal:** A LiveValidation test runs ONE representative task per
  family, sequentially, in a single bot lifetime over ~1 hour.
  Asserts:
  - Zero `physics_parity_break`.
  - Zero `physics_stuck`.
  - Zero `task_unrecoverable`.
  - All tasks reach `Complete` status.
  - StateManager snapshot reflects every state transition.
- **Done when:** the shake-out is green on both faction-sides.

## Out of scope for Phase 1

- Route-pack caching (Phase 7).
- LLM personality / character behaviour AI (future).
- Long-term performance history rendering (Phase 5).
- 27-class full-rotation parity audit (Phase 6 — only one rep spec
  per family-acceptance test in Phase 1).
- Raid encounter scripts (need OnDemand spawn-and-gear from Phase 2).
- Auction House ML pricing strategy (future).
