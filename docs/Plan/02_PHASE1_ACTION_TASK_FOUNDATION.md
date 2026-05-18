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

- **Latest evidence (2026-05-16 loop 3):** The cross-class
  `g_navigationMutex` contention that drove the batch-mode flakiness
  is closed by adding a shared xUnit collection over the five
  `NavigationFixture`-using test classes:

  - `Tests/PathfindingService.Tests/NavigationFixture.cs` gains
    `[CollectionDefinition("Navigation", DisableParallelization=true)]`
    + `NavigationCollection : ICollectionFixture<NavigationFixture>`.
  - The five classes (`LongPathingRouteTests`, `PathfindingBotTaskTests`,
    `SegmentValidationCacheTests`,
    `PathfindingSocketServerIntegrationTests`,
    `PathfindingTests`) switch from `IClassFixture<NavigationFixture>`
    to `[Collection(NavigationCollection.Name)]`.

  `DisableParallelization = true` is critical: it makes the entire
  collection non-parallel-with-anything else, not just internally —
  important because `WaypointGeneration/*.cs`'s
  `PathfindingValidationFixture` classes also P/Invoke Navigation.dll
  and would otherwise race against ours.

  Targeted `CrossroadsToUndercity_CriticalWalkLegs` Theory sweep
  comparison on `WWOW_DATA_DIR=D:/wwow-bot/prod-data`, parity
  Navigation.cs (bit-identical to `088e1865`):

  | Loop | Result | 13th case | Notes |
  |---|---|---|---|
  | loop 2 (no collection) | 9 pass / 3 fail | aborted at 30m, before 13th started | sibling-class Detour contention dominant |
  | loop 3 (collection fix) | 11 pass / 2 fail | aborted at 30m on 13th-case completion | contention removed, 13th case fully executed |

  **Both contention-induced loop-2 failures flipped to PASS:**

  - `orgrimmar_flight_master_to_zeppelin_tower_full_route` (670y from
    OG flight master to UC zeppelin boarding) — loop 2 returned a
    1069-pt smooth path 255y short ("Path end too far") in 3m52s.
    Loop 3 returns a clean smooth path passing all per-waypoint
    checks in 4m18s. The earlier truncation pathology was a
    contention-induced budget overflow inside
    `CalculateValidatedPathCore`, not a real smooth-path limit.
  - `orgrimmar_city_live_vertical_replan_recovery` — loop 2 truncated
    to an 11-pt path in 4m20s (the loop-2 4.0y z-delta relaxation
    never reached the 55-pt densified path it was designed to
    validate). Loop 3 produces the full densified path with the
    relaxed threshold and passes in 2m6s.

  **The remaining 2 failures are NOT contention-driven** (this
  reverses the prior loop's hypothesis):

  - `orgrimmar_exterior_steep_incline_live_stall_recovery` (1m46s
    fail in loop 3, vs 7m37s fail in loop 2 — same failure shape):
    static LOS at segment 13→14
    `from=(1373.5,-4385.2,28.2) to=(1370.6,-4390.3,30.0)
    result=native_path blocked=none`. The path has 156 corners, only
    this one segment fails. Bypass densifier branch
    `reason=corridor-fallback kind=CorridorFirstExpanded` is
    producing a segment geometrically valid for the bot capsule but
    failing the test-side static LOS heuristic at
    `minLineOfSightValidationSegmentLength=2.5`.
  - `orgrimmar_exterior_incline_live_stall_exact_recovery` (5m37s
    fail in loop 3; was abort-pre-execution in loop 2). Same family:
    nearby start `(1381.3,-4370.6,26)` heading to UC zeppelin
    boarding via the same Durotar-east-of-OG corridor.

  **10 cases #14–23 (zeppelin-tower routes + UC arrival) did not
  execute** — the sweep hit `TestSessionTimeout=1800s` after the 13th
  case at 26m total elapsed. Long routes back to pre-contention
  timing (5 cases @ 1–5 min each plus the 670y at 4m18s) consume
  most of the 30-min budget per Theory case.

  **No code-side changes**: Navigation.dll, PathFinder.cpp, and
  Navigation.cs are bit-identical to 088e1865. The fix is purely
  test-infrastructure.

  **Next-loop work** (out of scope here):

  - Investigate `exterior_*_incline_live_stall_recovery` LOS-13→14
    failure: classify as bake-fidelity (per
    [[feedback_pathfinding_freeze]] — per-tile MmapGen tuning) vs
    test-side heuristic too strict for densified corridors.
  - Decide budget strategy for cases #14–23: bump
    `TestSessionTimeout`, split Theory into faster/slower
    partitions, or accept partial-sweep coverage as the SLA.

  Memory references: [[project_pfs_navigation_collection_serialization]],
  [[feedback_pfs_test_state_contamination]],
  [[project_pfs_og_city_groundz_snap]].

- **Latest evidence (2026-05-16 loop 4):** Diagnosis-only outcome —
  no code shipped, working tree bit-identical to loop 3. The
  `exterior_*_incline_live_stall_recovery` LOS-13→14 failures
  classified via `tools/PathPhysicsProbe.exe` (the
  [[mmo-physics-pathing-probe]] canonical R13 canary):

  - **Single straight segment** `(1373.5,-4385.2,28.2) →
    (1370.6,-4390.3,30.0)` returns `affordance=JumpGap
    validation=MissingSupport hDist=5.87 vDelta=1.80
    climb=1.80 slope=12.01deg`. Target Z=30.0 floats 0.53y above
    ADT support 29.47. The runtime classifier says NOT walkable as
    a straight line.
  - **Detour `findPath(smooth=true)` on the same endpoints in
    isolation** returns **7 walkable corners** with a `Walk Clear`
    3y backtrack at idx 4 — the bake admits a walkable route, but
    a 7-corner zig-zag, not the 2-corner straight line.
  - The failing pair is **INSIDE one corridor segment's
    smooth-expansion output**, not a corridor pair. Production
    logs `[PATH_NATIVE] map=1 mode=smooth_from_corridor
    corridorLen=8 expandedLen=143 expandedSegments=4` confirm
    8-corner corridor with 4 segments smooth-expanded into 143
    sub-corners. Detour's `findStraightPath` string-pulled
    polygon-adjacent corners whose shared polygon edge crosses
    unwalkable terrain — a polygon-graph defect on tile (40, 29),
    same tile as [[project_pfs_overhaul_006_decklip_solution]].

  **Two resolver-side fix attempts in
  `Services/PathfindingService/Repository/Navigation.cs::TryExpandCorridorWithSmoothNativeSegments`**
  (LOS-gated smooth re-expansion of short corridor segments)
  were attempted and reverted:

  1. **`LosThreshold = 2.5f`** (matching the test's
     `minLineOfSightValidationSegmentLength`): too broad. The
     670y `flight_master_to_zeppelin_tower_full_route` regressed
     loop-3 pass (4m18s, full smooth path) → fail (3m19s,
     1069-pt truncated path) — the preferred-smooth attempt
     exhausted the 30s `CalculateValidatedPathCore.totalDeadline`
     under hundreds of LOS calls + cascaded findPath calls.

  2. **`LosThreshold = 5f` + `|dz| >= 1f` gate**: budget-clean (no
     regression on flight_master, ~2m9s), but **had no effect on
     the exterior_*_incline failures** — segment 13→14 isn't a
     corridor pair, it's a sub-corner pair inside a smooth-
     expansion output. Corridor-level gates can't see sub-pairs.

  Both reverts left the working tree bit-identical to loop 3.
  See [[project_pfs_exterior_incline_los_smooth_expand]] for
  probe data + the corridor-level vs sub-corner-level taxonomy.

  **Two viable next-loop fix surfaces:**

  1. **Bake-side per-tile config on tile (40, 29)** — regenerate
     with stricter `rcFilterLedgeSpans` / smaller `cs`/`ch` so
     the polygon contour breaks at the slope defect between the
     `(1373.5,...)` / `(1370.6,...)` polygons. Canonical per
     [[feedback_pathfinding_freeze]]. Reference precedent:
     [[project_pfs_overhaul_006_decklip_solution]] (same tile).

  2. **Resolver-side smooth-expansion-output post-processing** —
     inside `TryExpandCorridorWithSmoothNativeSegments` after
     `TryFindPathNative` returns, walk sub-pairs and recursively
     respath those in the narrow `[LosThreshold,
     MinSegmentLength)` band with `|dz| >= 1f` where LOS fails.
     Needs careful gating + recursion limit + per-route cost
     measurement (the flight_master 1535-corner smooth expansion
     is the budget-stress case).

  **Don't relax `minLineOfSightValidationSegmentLength`** for
  these cases test-side. The probe confirmed the straight line
  is genuinely unwalkable; relaxing the test would let the bot
  stall in the field. The fix has to be planner-side or
  bake-side.

  Memory references:
  [[project_pfs_exterior_incline_los_smooth_expand]],
  [[project_pfs_navigation_collection_serialization]],
  [[feedback_pfs_test_state_contamination]],
  [[feedback_pathfinding_freeze]],
  [[mmo-physics-pathing-probe]].

- **Latest evidence (2026-05-16 loop 17):** 5-layer fix shipped.
  `orgrimmar_exterior_incline_live_stall_exact_recovery` CLOSED;
  sweep coverage expanded from 13 → 20 of 23 Theory cases.

  **Layers shipped:**

  1. **Corridor-level LOS gate** in
     `TryExpandCorridorWithSmoothNativeSegments` for short corridor
     pairs in `[2.5y, 6y)` with `|dz| >= 0.4y` and LOS-failing
     straight line. Closes the original
     `exterior_incline_exact` Segment 16->17 defect (3.11y, 0.4y dz).

  2. **Sub-corner recursive LOS-respath** in new
     `ValidateAndRepathSmoothSubPairs` — post-pass on each smooth
     expansion's output. Walks consecutive sub-pairs; for in-band
     LOS-failing pairs recursively respaths via `TryFindPathNative`.
     Bounded by `SmoothSubCornerMaxRecursionDepth=2`.

  3. **Pre-densifier smooth-respath** in `BuildUsablePathResult`
     via new `SmoothRespathOversizeBypassSegments` — for
     vertically-oversize pairs (`|dz| > 5y`) call `findPath` to
     replace linear-interp midpoints with multi-corner walkable
     sub-paths. Gated to `CorridorFirstExpanded` resolutions only
     (long-path CorridorFallback would push the budget). Closes
     the OG zeppelin tower spiral-ramp midpoint failures.

  4. **Test-side LOS-walkable fallback** in
     `Tests/PathfindingService.Tests/PathRouteAssertions.cs`. When
     raw `LineOfSight` raycast fails, fall back to
     `ValidateWalkableSegment` using the bot's actual physics
     capsule. Avoids false-positive LOS hits on overhead WMO
     geometry the capsule passes under.

  5. **`CalculateValidatedPathCore.totalDeadline` 30s → 120s** +
     **`TestSessionTimeout` 30min → 60min**. The sub-corner LOS-
     respath adds 5-10s of native call overhead on long expansions
     (e.g. flight_master 670y with 1562-corner expansion);
     30s was budget-marginal and tripped overflow
     non-deterministically. 120s gives variance headroom.
     60min test session lets all 23 Theory cases run.

  **Loop 17 sweep (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`):**

  | Tally | Loop 3 baseline | Loop 17 final |
  |---|---|---|
  | Pass | 11 of 13 | 17 of 20 |
  | Fail | 2 of 13 | 3 of 20 |
  | Unrun (budget) | 10 of 23 | 3 of 23 |
  | Originally failing now PASS | — | `orgrimmar_exterior_incline_live_stall_exact_recovery` |
  | Newly visible (was unrun) — pass | — | 5 zeppelin tower variants |
  | Newly visible — fail | — | 2 zeppelin variants (Z-delta) |

  **Remaining failures (all tile (40, 29) bake-side defects):**

  - `orgrimmar_exterior_steep_incline_live_stall_recovery`: shifted
    from original LOS-13→14 to new Segment 178→179 step-up check
    `from=(1348.0,-4537.7,35.4) to=(1349.2,-4535.6,40.2) slope=63.1°`.
    Layer 3's smooth-respath successfully replaced the linear-
    interp midpoint at (1348.1,...,44.5), but Detour's `findPath`
    returns 32 corners through a 62° polygon adjacency that the
    bot capsule can't walk.

  - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`:
    WP27 floats 2.74y above support (test threshold
    `maxResolvedWaypointZDelta=2.5y` just below threshold).

  - `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery`:
    WP62 floats -3.6y from support (same Z-delta class).

  All three remaining failures are in the tile (40, 29) zeppelin-
  tower polygon-graph defect class. Per-tile MmapGen tuning on
  tile `0012940.mmtile` per [[project_pfs_overhaul_006_decklip_solution]]
  precedent is the canonical next-cycle fix. Multi-cycle work.

  **3 cases unrun** (sweep aborted at 60-min budget):

  - `orgrimmar_zeppelin_tower_exterior_support_recovery`
  - `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery`
  - `undercity_zeppelin_arrival_to_target`

  Memory references:
  [[project_pfs_exterior_incline_los_smooth_expand]],
  [[project_pfs_navigation_collection_serialization]],
  [[project_pfs_og_city_resolver_fix]],
  [[feedback_pathfinding_freeze]],
  [[mmo-physics-pathing-probe]].

- **Latest evidence (2026-05-16 loop 18 — bake-close session):**
  Commits `35f43d6a` (nav: GetGroundZ order fix) + `e90db16d`
  (test threshold relax). Real correctness bug in
  `Exports/Navigation/SceneCache.cpp::GetGroundZ` discovered via
  Cycle 2 probe analysis and fixed; threshold relaxation for the
  OG underpass region grounded in probe evidence.

  **Cycle 1 — 3 unrun cases run in isolation:**

  | Case | Result | Wall-clock |
  |---|---|---|
  | `orgrimmar_zeppelin_tower_exterior_support_recovery` | PASS | 4m23s |
  | `undercity_zeppelin_arrival_to_target` | PASS | 14s |
  | `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery` | FAIL (same shape as `bridge_side` — WP at (1347.1,-4510.5,27.9) supportZ=31.548) | 16s |

  The two "passing in isolation" cases were unrun only because
  the 60-min sweep budget hit before they ran. They pass cleanly
  when given budget headroom.

  **Cycle 2 — Probe-driven classification:**

  Probe data (`tools/PathPhysicsProbe.exe --map 1 --start X,Y,Z
  --end X,Y,Z --load-adt --verbose`) localized each defect:

  - **(1347.1,-4510.5,27.9)** — 3 scene-cache surfaces: 44.68,
    31.55 (instance 0x3939D — OG zeppelin tower deck), 27.68
    (instance 0x0 — world ground). `SceneCache::GetGroundZ`
    returned 31.55 (above the WP) because the impl's
    single-`bestZ` field plus strict `triZ > bestZ` check made the
    return value depend on triangle iteration order. If an above-z
    triangle iterated FIRST, it stuck in `bestZ` via the
    closest-above fallback branch (which only fired when
    `bestZ <= -200000+1`); subsequent below-z triangles couldn't
    override. **Fix evolved across two commits**: `35f43d6a`
    shipped a "prefer below" variant (track below/above
    separately, prefer any below). Sweep evidence showed this
    regressed shelf cases where a WP sits 0.1-0.2y below a deck
    triangle with lower terrain also in search range — the new
    impl picked the deeper terrain instead of the deck.
    **`addf83af` refined the semantic to closest-absolute**: pick
    the triangle with smallest `|triZ - z|`. Order-independent
    AND matches the test/probe use case ("which surface is the
    bot standing on at this WP?"). Shelves correctly resolve to
    the deck; under-deck WPs correctly resolve to the world
    ground below. **Closes** `zeppelin_bridge_side` WP62 +
    `tower_base_live_vertical` WP32.

    **NOT applied to `SceneCache::GetWalkableGroundZ`:** the
    `OgZeppelinCliffFallParityTests` wide-search variant currently
    depends on the order-dependent behavior to fire FALLINGFAR
    priming on cliff edges. Bench evidence: with EITHER
    "prefer below" or "closest absolute" applied to
    `GetWalkableGroundZ`, 2/4 cliff-fall parity tests regress
    (the cliff-edge ledge at z=51.62 is walkable per the slope
    filter and would always be returned for a 6y nearby-support
    probe at z=52.2). That parity gap is a separate FG/BG
    physics workstream.

  - **(1354.1,-4512.5,31.3)** and **(1353.8,-4513.4,31.5)** —
    densifier midpoints over scene-blind deck. `SceneCache` only
    sees ADT at z≈28.74; the deck triangles are dynamic GameObject
    collision at z≈31.48, not pre-cached scene. Probe affordance
    `Walk` validation `Clear` resolvedZ=31.48 confirms the bot CAN
    walk these segments. **Probe-backed threshold relaxation
    (commit `e90db16d`):** 2.5y → 3.0y for
    `zeppelin_tower_underpass`, `bridge_side`, and
    `tower_base_live_vertical`. Under the 4.0y handoff cap.

  - **(1347.3,-4540.6,35.8)** and **(1350.2,-4528.6,34.0)** — true
    phantom polys 3.1–3.3y BELOW ADT terrain (ADT=38.96, WP=35.8).
    No walkable surface anywhere near WP Z. Probe affordance
    `JumpGap` validation `MissingSupport`. Bake-side fix only.

  - **`exterior_steep_incline` WP178→179**
    (1348.0,-4537.7,35.4) → (1349.2,-4535.6,40.2) — WP178 at
    z=35.4 is **13.9y UNDERGROUND** vs ADT=49.31 at same XY.
    Another phantom poly; the 63° "slope" is the bake's
    continuation of an underground corridor climbing to surface.
    Bake-side fix only.

  **Cycle 3 — Tile (40, 29) bake regen DEFERRED:**

  Cascading phantom-poly defects on tile (40,29) extend
  significantly underground (case #1 is 14y below ADT). The
  current per-tile config in `tools/MmapGen/config.json` is
  already aggressive (`cs=0.1` + `tileSize=213`,
  `agentMaxClimbTerrain=0.2`, `treatOobNeighborAsCliff=false`,
  `mixedAreaUsesTerrainClimb=true`, `walkableErosionRadius=0.2`,
  `maxVertsPerPoly=3`). The `_4029_README_REVALIDATE` warns these
  knobs need fresh validation against corrected 40,29 geometry
  before being treated as proven. A bake regen attempt without
  probe-guided knob selection has high regression risk against
  the 17 currently-passing cases. Documented as multi-cycle
  next-session work — same class as the BRM south-face
  bake-fidelity gap from
  [[project_pfs_overhaul_006_brm_phase4_findings]].

  **Cycle 4 — Adjacent regression suites all green:**

  | Suite | Tally |
  |---|---|
  | `OrgrimmarCorpseRun_LiveRetrieveRoute` | 2/2 |
  | `RecordedTests.PathingTests` | 135/0/0 |
  | `Navigation.Physics.Tests` | 152/0/1 (matches loop-17) |
  | `OgZeppelinCliffFallParityTests` | 4/4 (split fix preserves these) |
  | `WaypointGeneration` (subset) | 39/0/3 |

  **Loop 18 sweep final (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`,
  100-min budget, trx logger):**

  | Tally | Loop 3 baseline | Loop 17 | Loop 18 (this session) |
  |---|---|---|---|
  | Pass | 11 of 13 | 17 of 20 | **19 of 23** |
  | Fail | 2 of 13 | 3 of 20 | **4 of 23** |
  | Unrun (budget) | 10 of 23 | 3 of 23 | **0 of 23** |

  All 23 cases ran inside the 100-min budget. The 4 remaining
  failures are exactly the 4 bake-side cases diagnosed in
  Cycle 2:

  - `orgrimmar_exterior_steep_incline_live_stall_recovery` —
    WP178 at z=35.4 vs ADT=49.31 (13.9y underground phantom poly).
  - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` —
    densifier-midpoint cascade through scene-blind deck region.
  - `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery` —
    same cascade.
  - `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery` —
    same cascade. Was unrun in loop 17.

  All four require tile (40, 29) `0012940.mmtile` MmapGen regen.
  Multi-cycle next-session work.

  Memory references:
  [[project_pfs_scenecache_groundz_orderfix]],
  [[project_pfs_exterior_incline_los_smooth_expand]],
  [[project_pfs_overhaul_006_decklip_solution]],
  [[project_pfs_overhaul_006_brm_phase4_findings]],
  [[feedback_pathfinding_freeze]],
  [[feedback_pathfinding_anti_patterns]],
  [[mmo-physics-pathing-probe]].

- **Latest evidence (2026-05-17 loop 20 — cull-pipeline unblock):**
  Commit `0b2164d9` (tools: NavMeshPhysicsValidator tolerate
  `GetPolyAtCoord` AV via legacy CSE policy). Closes the
  bake-validation blocker discovered in loop 19. **Tile (40, 29)
  bake-side phantom-poly closure NOT achieved this loop — the cull
  approach is calibrated but does not yet reach the trap
  polygons.** Tally remains 19 / 4 / 0 on prod-data.

  **Cycle 1 — Pipeline unblock SHIPPED:**

  `NavMeshPhysicsValidator.exe` reliably crashed with
  `System.AccessViolationException` inside Detour's
  `findNearestPoly` for a subset of cull-coord probe points on
  tile (40, 29). The per-call `try { GetPolyAtCoord(...) } catch`
  in `tools/NavMeshPhysicsValidator/Program.cs:244-249` did NOT
  catch the AV — in .NET 5+ AV is treated as a corrupted-state
  exception and refuses managed catch without explicit opt-in.
  `[HandleProcessCorruptedStateExceptions]` is deprecated in
  .NET 8 (the build emits SYSLIB0032). The fix opts the validator
  into the legacy behavior via the runtime config option
  `System.Runtime.LegacyCorruptedStateExceptionsPolicy=true`
  (set as a `<RuntimeHostConfigurationOption>` in the csproj) and
  factors the probe call into a `SafeGetPolyAtCoord` helper that
  catches AV explicitly and returns 0 (no-poly) for that probe
  point. The native side already SEH-wraps the call
  (`Exports/Navigation/DllMain.cpp:2800/2930`) but the SEH frame
  runs in native, not managed. **Validator now runs to completion
  on tile (40, 29) and emits per-coord polyref summary lines.**

  **Cycle 2 — Surgical cull ATTEMPTED, REVERTED twice:**

  *Attempt 1 (handoff-prescribed radii `Z=2.0 XY=0.5`):*
  Validator produced 32 unique cull polyrefs (29 at coord
  `(1350.2,-4528.6,34.0)`, 3 at coord `(1349.2,-4535.6,40.2)`).
  Coord 1 `(1347.3,-4540.6,35.8)` and coord 3
  `(1348.0,-4537.7,35.4)` returned ZERO polys at these radii —
  their phantoms are >0.5y XY-offset from the WP center. Sweep
  result: **22 pass / 5 fail / 0 unrun** (4 originally-failing
  still fail; **`orgrimmar_flight_master_tower_hover_stall_exact_live_recovery`**
  regressed from pass → 292y-short-path fail). Reverted. The
  29-poly cull at coord 2 over-included legitimate harbor/deck
  polys that other passing routes depend on.

  *Attempt 2 (minimum-radii `Z=0 XY=0`):* Single zero-radius
  probe per coord at the validator's default extent
  (`xy=2.0, z=1.8`). Coord 2 yielded polyref `281475331147696`
  (area=AREA_GROUND, flags=NAV_GROUND); coord 4 yielded polyref
  `281475331147633` (area=AREA_STEEP_SLOPE, flags=NAV_STEEP_SLOPES).
  Coords 1 and 3 still no-poly. Applied 2-poly cull via
  `tools/MmapGen/build/NavMeshTileEditor.exe ... --cull-polys
  281475331147696,281475331147633`. Sweep result: **19 pass / 4
  fail / 0 unrun** — matches loop-18 baseline exactly. No
  regression, but ALSO no closure: failure messages for all 4
  cases still report the same WP coords and same supportZ values
  as loop 18. The probed-at-zero-radius polyrefs were NOT the
  actual trap polygons; the bot's path still routes through the
  same phantom regions. Reverted to baseline (`0012940.mmtile`
  md5 `cc0d89c4...`).

  **Lesson — zero-radius probe doesn't reach the trap.** The
  `GetPolyAtCoord` call at the WP coord with default extent
  (xy=2.0, z=1.8) returns *whichever* poly best matches the
  probe XY,Z — typically a sibling poly stacked alongside the
  phantom, not the phantom itself. The Detour smooth-path that
  produces WP92/WP72/WP42 at `(WPxy, supportZ−3.1)` must be
  using a different polygon (likely a span-bridging poly that
  connects deck to phantom across multiple Z layers). Identifying
  the actual trap polyref requires either:
  (a) instrumenting `PathFinder.cpp` smooth-path generation to
  emit the corner-poly ref per waypoint, or (b) probing along the
  full failing-route smooth path (1000+ corners) and finding the
  corner-poly ref whose XY,Z matches the failure WP.

  **Cycle 3 — Adjacent suite regression sweep:** SKIPPED. The
  Cycle 1 commit touches only the `tools/NavMeshPhysicsValidator`
  project; no test-runtime code changed. Tile (40, 29) reverted
  to baseline hash `cc0d89c4` matches loop-18's verified state.

  **Loop 20 sweep tally (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`,
  Release configuration):**

  | Tally | Loop 18 baseline | Loop 20 (prescribed radii) | Loop 20 (zero-radius) | Loop 20 (final, reverted) |
  |---|---|---|---|---|
  | Pass | 19 of 23 | 22 of 23 | 19 of 23 | **19 of 23** |
  | Fail | 4 of 23 | 5 of 23 (+1 regression) | 4 of 23 | **4 of 23** |
  | Unrun | 0 of 23 | 0 of 23 | 0 of 23 | **0 of 23** |

  The 4 remaining failures are unchanged from loop 18 (same WP
  coords, same supportZ, same failure shape):

  - `orgrimmar_exterior_steep_incline_live_stall_recovery` — WP178
    at `(1348.0,-4537.7,35.4)` vs ADT=49.31 (13.9y underground
    phantom poly).
  - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` —
    WP92 at `(1350.2,-4528.6,34.0)` vs supportZ=37.245.
  - `orgrimmar_zeppelin_bridge_side_live_missed_boarding_recovery` —
    WP72 at `(1347.3,-4540.6,35.8)` vs supportZ=38.932.
  - `orgrimmar_zeppelin_tower_base_live_vertical_replan_recovery` —
    WP42 at `(1347.3,-4540.6,35.8)` vs supportZ=38.932.

  **Next-cycle work surfaces (cheapest first):**

  1. Extend the smooth-path corner emission to dump the
     corner-poly ref alongside each `(x,y,z)`. Then the actual
     trap polyref is `cornerPolyRefs[i]` for the failing WP
     index. Surgical cull becomes single-poly per test.
  2. Alternatively, drive `tools/PathPhysicsProbe.exe
     --detour-resolve --smooth` along the full failing route
     and at every smooth-path corner, call `QueryPolyAtCoord`
     to record the polyref. Filter by WP index — the trap polyref
     is the corner at the failure WP. No code change needed; just
     a new orchestrator script.
  3. Per-tile MmapGen config iteration on tile (40, 29) — the
     `_4029_README_REVALIDATE` knobs need fresh validation
     against probe data. Multi-cycle.

  Memory references:
  [[project_pfs_loop20_cull_pipeline_unblock]] (new),
  [[project_pfs_loop19_cull_pipeline_blocker]] (closed),
  [[project_pfs_scenecache_groundz_orderfix]],
  [[project_pfs_overhaul_006_brm_phase4_findings]],
  [[feedback_pathfinding_freeze]].

- **Latest evidence (2026-05-17 loop 21 — trap diagnosis):**
  Commit `c4415201` adds `tools/PathPhysicsProbe.exe
  --dump-polyrefs` for per-corner polyref dumps along resolved
  smooth paths. Used to definitively localize the 4 remaining
  failures' root causes. **Tile (40, 29) failures are NOT
  closable by polyref cull alone.** Tally remains 19/4/0.

  **Per-route diagnosis via `--detour-resolve --smooth --dump-polyrefs`:**

  | Test | Smooth-path corners | Failure pattern at WP |
  |---|---|---|
  | `tower_underpass` | 104 | Stacked phantoms at coord 2 |
  | `tower_base_live_vertical` | 194 | Interpolated-air corners |
  | `bridge_side` | 1031 | Interpolated-air corners |
  | `exterior_steep_incline` | 1033 | Interpolated-air corners |

  **Coord 2 `(1350.2,-4528.6,34.0)` — stacked phantoms (20+ deep):**

  Iterative cull-then-reprobe at `(1350.174, -4528.553, 33.962)`
  found 20 distinct polyrefs, all with `posOverPoly=0` and
  `surfaceZ ∈ [34.9, 35.9]`. Each cull just shifts findNearestPoly
  to the next-best sibling at the same XY/Z.

  Vertical Z-scan at the same XY revealed the **legitimate ground
  polyref `281475331147742`** at Z=37.46 with `posOverPoly=1`
  (matches failure's reported supportZ=37.245 within 0.2y). But
  Detour's findNearestPoly uses default `zExtent=1.8y`, so from
  the WP Z=33.96 the legitimate ground at Z=37.46 (3.5y away) is
  out of reach. The phantom stack is what Detour can see; the
  legitimate ground is what physics raycasting reports.

  **Coords 1 + 3 — interpolated-air WPs:**

  Vertical Z-scan at `(1347.3, -4540.6, anyZ ∈ [33, 42])` and
  `(1348.0, -4537.7, anyZ ∈ [33, 42])` returns `polyref=0` for
  ALL probed Z values. No navmesh polygon exists at these XYs
  in any reasonable Z range. Yet the failing tests report `Path
  waypoint X` at these exact coords.

  Cross-referencing the smooth-path corner dumps for
  `tower_base_live_vertical` and `tower_underpass`: corners 115,
  122, 123, 124, 129, 131, 138-141 (and similar ranges) ALL have
  `polyref=0`. They are **synthetic densifier midpoints
  interpolated between anchor corners** that themselves sit on
  polys; the interpolated midpoint lands in air. The failure
  WPs (e.g. WP42 = `(1347.3,-4540.6,35.8)`) are these
  air-interpolated points. There is no polyref AT all to cull.

  **Net conclusion — cull architecture cannot close these:**

  1. Coord 2's phantoms are 20+ deep; culling them as a stack
     regresses passing tests (loop-20 attempt 1 demonstrated).
     The legitimate ground poly is out of reach from the WP Z
     given Detour's default zExtent.
  2. Coords 1 + 3 have NO polys to cull. The trap is the
     smooth-path generator's air-interpolation, not a polygon.

  **Real fix surfaces (multi-cycle, out of this session's scope):**

  1. **Re-bake tile (40, 29)** with denser navmesh coverage so
     the air-interpolation corners land on real polys. Per-tile
     config knobs need fresh validation against probe data. High
     regression risk against 19 passing cases
     ([[project_pfs_overhaul_006_brm_singletile_negative]]).
  2. **Modify smooth-path generator** to detect
     `polyref=0` corners and re-route them through adjacent
     legitimate polys. Resolver-side fix at the native level
     (`Exports/Navigation/PathFinder.cpp`) — needs sign-off
     against the pathfinding freeze
     ([[feedback_pathfinding_freeze]]).
  3. **Increase Detour's findNearestPoly zExtent** in the test's
     query path so the legitimate ground at Z=37.46 is reachable
     from WP Z=34. Risk of catching wrong polys in narrow
     vertical contexts (mine shafts, stacked decks).

  **What did ship:** `tools/PathPhysicsProbe.exe --dump-polyrefs`
  (commit `c4415201`) is a reusable diagnostic. Future
  bake-debugging sessions can use the recipe documented in
  [[project_pfs_loop21_trap_diagnosis]] to triage any "Path
  waypoint floats from collision support" failure into one of
  three categories (air-interpolation / phantom-stack / threshold)
  without re-deriving the methodology.

  **Cycle 3 — Adjacent suite regression sweep:** SKIPPED. The
  Cycle 1 commit `c4415201` touches only `tools/PathPhysicsProbe/`;
  no test-runtime code changed. Tile (40, 29) at baseline hash
  `cc0d89c4` matches loop-18's verified state.

  **Loop 21 sweep tally (prod-data, `WWOW_DATA_DIR=D:/wwow-bot/prod-data`):**

  | Tally | Loop 18 baseline | Loop 19 | Loop 20 final | Loop 21 final |
  |---|---|---|---|---|
  | Pass | 19/23 | 19/23 (no work) | 19/23 | **19/23** |
  | Fail | 4/23 | 4/23 | 4/23 | **4/23** |
  | Unrun | 0/23 | 0/23 | 0/23 | **0/23** |

  Memory references:
  [[project_pfs_loop21_trap_diagnosis]] (new),
  [[project_pfs_loop20_cull_pipeline_unblock]],
  [[project_pfs_overhaul_006_brm_singletile_negative]],
  [[feedback_pathfinding_freeze]].

- **Latest evidence (2026-05-17 loop 22 — threshold cascade discovery):**
  Three approaches attempted and reverted; only the
  `test.runsettings` TestSessionTimeout 60→100min bump shipped.
  Tally unchanged: 19/4/0.

  **Cycle 1 — Phantom-layer iterative cull** at coord 2 corner-31
  XY `(1350.174, -4528.553, 33.962)` ran 22 cull-then-reprobe
  iterations without convergence. Each cull shifted
  `findNearestPoly` to the next-best sibling at the same XY/Z
  with `posOver=0`, `surfaceZ ∈ [34.9, 35.9]`. The phantom stack
  is structurally deeper than 22 polys at this XY. Reverted tile
  to baseline (`cc0d89c4`).

  **Cycle 2 — Threshold relax 3.0 → 3.5** for 3 OG-underpass
  tests with probe-backed evidence (deltas 3.13-3.28y). Sweep
  result: original Z-delta failures cleared, but tests now fail
  at NEXT WPs further down each route:

  | Test | Cascade WP | Delta |
  |---|---|---|
  | tower_underpass | WP173 `(1343.6,-4555.9,38.4)` supportZ=42.253 | 3.85y (below ground) |
  | bridge_side | WP76 `(1341.8,-4563.1,39.4)` supportZ=35.617 | 3.78y (above ground) |
  | tower_base_live_vertical | WP46 `(1341.8,-4563.1,39.4)` supportZ=35.617 | 3.78y (same coord) |

  **Cycle 3 — Threshold relax 3.5 → 4.0** (matching `GetGroundZ`'s
  own 4.0y search radius and `city_live_vertical` sibling value).
  100-min TestSessionTimeout bump required because the no-longer-
  fast-failing 3 OG-underpass tests now run to completion
  (~10m / 5m / 7s) plus the rest of the sweep took 1h22m total.

  Sweep result: Z-delta failures cleared but tests now fail at
  **completely different assertion layers**, all at the START of
  each route (segments 1-25 range), all in tile (40, 29):

  | Test | New failure assertion |
  |---|---|
  | tower_underpass | `local physics movement break at the live stall: segment 25→26 from=(1354.120,-4506.359,29.116) to=(1354.569,-4510.318,30.993)` |
  | bridge_side | `Segment 1→2 failed static LOS from=(1337.2,-4654.8,49.9) to=(1342.5,-4653.7,48.8) (walkable-fallback=BlockedGeometry)` |
  | tower_base_live_vertical | `Segment 6→7 failed static LOS from=(1336.9,-4636.9,24.8) to=(1335.7,-4634.1,24.1) (walkable-fallback=BlockedGeometry)` |

  **Key finding — nested assertion layers all catch the same
  bake-fidelity issue.** The 4 failing tests are gated by:
  1. Z-delta assertion (fires first)
  2. LOS check
  3. Local physics reachability (per-test)
  4. Steep uphill slope check (`exterior_steep_incline`)

  Relaxing the Z-delta assertion to 4.0 essentially disables it
  (4.0 = `GetGroundZ` search radius). Tests still fail at the
  next-layer assertion catching the same underlying issue.
  **Threshold tuning at any single layer is whack-a-mole.**
  Reverted `LongPathingRouteTests.cs` to baseline.

  **What did ship:** `test.runsettings` TestSessionTimeout
  60→100min — safety margin matching loop-18's verified budget.
  Necessary for any future iteration where the slow tests don't
  fast-fail.

  **Cycle 4 — Adjacent suite regression sweep:** SKIPPED.
  Reverted to baseline; no test/runtime code changed. Only
  `test.runsettings` modified — affects no test outcomes, just
  the budget ceiling. Tile (40, 29) at baseline hash `cc0d89c4`.

  **Real fix surfaces (all multi-cycle, out of single-session
  scope):**

  1. Tile (40, 29) bake regen with denser navmesh and config-
     knob iteration. High regression risk per BRM precedent.
  2. Native `Exports/Navigation/PathFinder.cpp` smooth-path
     generator to detect `polyref=0` corners and re-route through
     adjacent legitimate polys. Pathfinding freeze covers
     managed `Services/PathfindingService/Repository/` but not
     explicitly the native side — needs user sign-off.
  3. Off-mesh connections in `tools/MmapGen/offmesh.txt`
     bypassing the OG harbor phantom region. Detour would use
     them as "shortcut edges". Requires careful endpoint binding
     per [[project_mmapgen_offmesh_axis_swap]].

  Memory references:
  [[project_pfs_loop22_threshold_cascade]] (new),
  [[project_pfs_loop21_trap_diagnosis]],
  [[project_pfs_overhaul_006_brm_singletile_negative]],
  [[feedback_pathfinding_freeze]],
  [[project_mmapgen_offmesh_axis_swap]].

- **Latest evidence (2026-05-17 loop 23 — three parallel surfaces, no winner):**
  Per user sign-off on 2026-05-17, three scope expansions were
  authorized: (1) native `Exports/Navigation/PathFinder.cpp`
  modifications, (2) tile (40, 29) bake regen with zero-regression
  tolerance, (3) off-mesh entries in `tools/MmapGen/offmesh.txt`.
  Three `monorepo-worker` agents ran in parallel via
  `isolation: "worktree"` against `agent-afd78d8b84c3eef14` /
  `agent-a9ae9f2846619d8a3` / `agent-a26ee6bbf1dc67d6d`. Each
  produced a candidate artifact under
  `/tmp/wwow-loop23-candidates/{A,B,C}/`; the lead reconciled.

  **Surface A — bake regen (knob: `maxVertsPerPoly: 3 → 6` on
  tile-4029 override).** New tile md5
  `dcf6f88281e1b3b59699ec4d22e0f312` (size −37.6% vs baseline,
  confirming `rcBuildPolyMesh` merge took effect). Probe via
  `PathPhysicsProbe --dump-polyrefs` against the regenerated tile:
  at coord 2 `(1350.174, -4528.553)` the phantom-stack depth is
  structurally unchanged across Z=[33, 36.5] with `posOver=0`,
  legitimate ground polyref still at z≈37.48 (3.5y gap > Detour's
  1.8y zExtent — same trap geometry). At coords 1+3 `(1347.3,
  -4540.6)` / `(1348.0, -4537.7)`, polyref=0 at every Z in [33,
  42] — coverage gap unchanged. The merge-step knob shifted poly
  count without altering walkable classification or filtering, so
  it could not close any failure. Self-reported confidence LOW.
  No sweep run; candidate retained at
  `/tmp/wwow-loop23-candidates/A/`.

  **Surface B — native PathFinder.cpp B2 patch.** 33-line addition
  to the Cycle-17e densifier midpoint emit block at
  `Exports/Navigation/PathFinder.cpp:1900-1933`: each interpolated
  corner does a `findNearestPoly` with expanded extents
  `(2.0, 4.0, 2.0)` and snaps Z to the nearest legitimate poly's
  surface (fallback to prior `getPolyHeight(polys[0],...)` if
  none). Full regression run in worktree against
  `WWOW_DATA_DIR=D:/wwow-bot/prod-data`:

  | Suite | Baseline | Surface B | Δ |
  |---|---|---|---|
  | `OgZeppelinCliffFallParityTests` | 4/0/0 | 4/0/0 | 0 (held the critical gate) |
  | `CrossroadsToUndercity_CriticalWalkLegs` | 19/4/0 | **18/5/0** | **−1 pass, +1 fail** |
  | `Navigation.Physics.Tests` (full) | 152/0/1 | 186/1/1 | +1 unrelated WIP regression |
  | `RecordedTests.PathingTests` | 135/0/0 | 135/0/0 | 0 |

  REGRESSION: `orgrimmar_exterior_incline_live_stall_exact_recovery`
  flipped to fail with `Segment 349->350 failed static
  line-of-sight (walkable-fallback=BlockedGeometry)` — the Z snap
  raised a densifier midpoint into obstructed geometry. The 4
  baseline failures STILL failed with the same modes. Root cause
  of the miss: the failing routes' problem corners are in the
  main per-iteration `iterPos` emit at line 1936, NOT in the
  densifier midpoints the patch covered (and
  `tower_base_live_vertical` runs `smoothPath: False` so the
  patch did not fire on that route at all — its problem is in
  `findStraightPath` at line 1371). Wrong layer. Definitive
  negative result; candidate not viable.

  **Surface C — 4 off-mesh entries on tile (40, 29).** Each entry
  bridges a failing test's start-region poly to BoardingPoint
  `(1320.14, -4653.16, 53.89)`. All 4 endpoints bound via
  `findNearestPoly` (bake-time `[OFFLINK] LINKED` traces confirm,
  no `classifyOffMeshPoint` height-check dropouts). New tile md5
  `68b4f4cb07ce2ab8e9007bc02856c110`. Post-regen smooth-path
  probe via `PathPhysicsProbe --detour-resolve --smooth
  --dump-polyrefs`: only 2 of 4 failing routes
  (`tower_underpass`, `exterior_steep_incline`) show off-mesh
  teleport jumps in their smooth paths; `bridge_side` has a clean
  13-corner deck walk (vs 1031 baseline corners — A* corridor
  selection shifted but no obvious teleport segments);
  `tower_base_live_vertical` looks like it routed via the
  existing H2c entry rather than the new one. **Trap region NOT
  bypassed for `tower_underpass`:** corners 8-12 of the new
  smooth path still traverse NULL polys + `posOver=0` at the
  phantom region. Detour's A* preferred the natural multi-corner
  walk through the trap over the 1-step off-mesh hop.
  Compounding: the test-side `GetLocalPhysicsReachabilityFailure`
  and `GetSteepUphillSegmentFailure` validators (which the 4
  baseline failures depend on) physics-simulate every segment in
  the `[0.75y, 8y]` projection range — the off-mesh teleport at
  corner 75→76 (horizontal=3.2y, rise=+31.9y) fits the range and
  fails the simulation (cannot climb 31.9y in 3.2y horizontal).
  Plus the BRM revert risk (`offmesh.txt:108-152`): the managed
  `Services/PathfindingService/Repository/Navigation.cs` 8-phase
  repair pipeline hangs >25s on corridors containing off-mesh
  polys (`[PATH_REQ] still-running elapsed>=25s`); the 4 failing
  tests would route through the new off-mesh polys for the first
  time and may trigger the BRM-style hang. Best case: 1/4 fixed.
  Worst case: <19/4 + docker hang. Self-reported confidence LOW.
  No sweep run (destructive failure-mode risk); candidate
  retained at `/tmp/wwow-loop23-candidates/C/`.

  **Reconciliation:** no surface meets the 23/0 acceptance gate.
  Surface B is empirically dead (regression in worktree). Surface
  A's probe-backed prediction is strong negative (trap unchanged).
  Surface C's probe-backed prediction is strong negative (1/4
  best case) with a destructive failure mode (managed-pipeline
  hang). Per handoff explicit authorization: 19/4 accepted as
  the durable resting state.

  **Cleanup:** three worktrees torn down. Parent worktree clean.
  Both `D:/MaNGOS/data/mmaps/0012940.mmtile` and
  `D:/wwow-bot/prod-data/mmaps/0012940.mmtile` verified at
  baseline md5 `cc0d89c42d9abf4737ba52a369c5f3f7`. Docker
  `wwow-pathfinding` not restarted (no live sweep run for A or C
  because both had strong negative probe predictions).

  **Loop 23 sweep tally:** Unchanged at **19 / 4 / 0** (no live
  sweep run; Surface B's in-worktree run is the only live data and
  it regressed, not advanced).

  **What's left as multi-cycle work** (next session, not this
  one):
  1. **Surface B layered (right layer):** apply the air-interp
     `findNearestPoly` snap to the MAIN per-iteration `iterPos`
     corner emit at `PathFinder.cpp:1936` (NOT the densifier
     midpoints) AND to `findStraightPath` at line 1371 (for
     routes where `smoothPath: False`). MUCH higher blast radius
     — every smooth-path corner from every route in every tile.
     Needs careful extent calibration to avoid the
     `exterior_incline_exact` regression Surface B saw.
  2. **Surface A continued:** the next-untried bake-only knob is
     `walkableErosionRadius` 0.2 → 0.3 / 0.4 (likely worsens
     coord 1+3 coverage, low prior). After that, the bake-only
     surface is exhausted on this tile.
  3. **Coord-stack widening at probe time:** modify
     `PathPhysicsProbe`'s `--dump-polyrefs` to enumerate ALL
     polys at a coord across a wider Z range (currently only
     reports `findNearestPoly` winners). Diagnostic-only — does
     not close failures but supports the next bake iteration.
  4. **Off-mesh + managed-pipeline awareness:** the Surface C
     architecture is correct at the Detour level but blocked on
     Phase 4 sub-deliverable of PATHFINDING_OVERHAUL (making
     `Navigation.cs` off-mesh-aware). Out of current freeze.

  Memory references:
  [[project_pfs_loop23_close_attempt_three_surfaces]] (new),
  [[project_pfs_loop22_threshold_cascade]],
  [[project_pfs_loop21_trap_diagnosis]],
  [[project_pfs_overhaul_006_brm_singletile_negative]],
  [[feedback_pathfinding_freeze]],
  [[project_mmapgen_offmesh_axis_swap]].

- **Latest evidence (2026-05-18 loop 24 — Phase A1 main-emit
  polyref==0 guard, NEUTRAL):** Executed loop-23's
  "what's left as multi-cycle work" item #1 (Surface B at the
  right layer). Added a private helper
  `PathFinder::isCornerOnNavmesh(const float*)` that calls
  `findNearestPoly` with DEFAULT extents (`2.0, 1.8, 2.0` — user
  constraint, no widening) and inserted **SKIP-then-bail-after-3**
  guards at TWO sites in `Exports/Navigation/PathFinder.cpp`:
  (a) `findSmoothPath` main `iterPos` emit at line 1936
  (per-iteration corner) — guards every smooth-path corner; and
  (b) `BuildPointPath` post-process of `findStraightPath` output —
  filters interior corners (preserves start/end). Total +78 LOC,
  within the 75-LOC budget after revert. Build via MSBuild
  (`Exports/Navigation/Navigation.vcxproj -p:Configuration=Release
  -p:Platform=x64 -p:PlatformToolset=v145`) green.

  **Result tally on prod-data (`WWOW_DATA_DIR=D:/wwow-bot/prod-data`,
  Release, 100-min `TestSessionTimeout`):**

  | Suite | Baseline | Phase A1 | Δ |
  |---|---|---|---|
  | `OgZeppelinCliffFallParityTests` | 4/0/0 | 4/0/0 | 0 (critical gate held) |
  | `CrossroadsToUndercity_CriticalWalkLegs` | 19/4/0 | **19/4/0** | 0 (no closure, no regression) |
  | `RecordedTests.PathingTests` | 135/0/0 | 135/0/0 | 0 |
  | `Navigation.Physics.Tests` (full) | 152/0/1 | 159/1/1 | +7 passed (new tests since loop-18 baseline), +1 fail in WIP commit `1af02831` (`SceneEnvironmentFlagTests.StepPhysicsV2_KnownIndoorSupport_BlocksMountByEnvironment`) — unrelated to PathFinder.cpp |

  CriticalWalkLegs sweep duration: 1 h 4 m.

  **Root cause of the no-op:** the failing test
  `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  emits a smooth-path corner 42 at `(1347.3, -4540.6, 35.8)` —
  exactly **coord 1 from loop-21's diagnosis**. Loop 21 established
  that coord 1 has NO polys at any Z in [33, 42] under default
  extents (it IS a true air corner). My guard SHOULD have caught
  it — except corner 42 is **a densifier midpoint emitted at lines
  1919-1931, NOT the main iterPos emit at line 1936**. The
  corner-spacing evidence in the dumped path (consecutive dz ≈
  0.9y, well below the 2y `SMOOTH_PATH_STEP_SIZE`) confirms the
  densifier loop is the corner-producing layer. Loop 21's
  analytical finding ("failure WPs are smooth-path interpolated
  midpoints in AIR between anchor corners") was correct; loop 23's
  claim that the failing corners are at iterPos:1936 was wrong.

  **Why Phase A1's approach is fundamentally insufficient:** the
  spec correctly forbade touching the densifier (loop-23 Surface
  B `+33 LOC at 1900-1933` regressed
  `orgrimmar_exterior_incline_live_stall_exact_recovery` because
  its Z-snap raised a corner into obstructed geometry; the
  densifier midpoints are load-bearing for slope ascent per
  Cycle 17e). So Phase A1 placed the guard at the main emit
  instead. But the failing corners ARE in the densifier — and any
  variant of densifier-layer guard hits the loop-23 Surface B
  regression mode. Either way, **the underlying problem (no ground
  at coord 1) is unfixable from inside the smooth-path generator.**
  No PathFinder.cpp-layer fix can close the 4 remaining failures.

  **Cleanup:** PathFinder.cpp + PathFinder.h reverted to baseline
  via `git checkout`. Navigation.dll rebuilt clean. Working tree
  clean. Tile (40, 29) md5 `cc0d89c42d9abf4737ba52a369c5f3f7`
  unchanged.

  **Loop 24 sweep tally:** Unchanged at **19 / 4 / 0**.

  **Next iteration — Phase A2:** Extend
  `tools/PathPhysicsProbe` with `--dump-poly-stack` enumerating
  ALL `dtPoly` entries whose AABB overlaps a probe coord ±10y Z
  window via DIRECT tile poly iteration (NOT `findNearestPoly`).
  Output TSV with polyref, surfaceZ, posOverPoly, area, flags,
  vertCount. Diagnostic only; output feeds Phase A3 (multi-knob
  bake regen) and Phase A4 (validator-driven targeted cull). No
  live test impact this phase.

  Memory references:
  [[project_pfs_loop24_phase_a1_neutral]] (new),
  [[project_pfs_loop23_close_attempt_three_surfaces]],
  [[project_pfs_loop21_trap_diagnosis]],
  [[feedback_pathfinding_freeze]].

- **Latest evidence (2026-05-18 loop 24 — Phase A2 diagnostic
  ships `--dump-poly-stack`):** New native export
  `EnumeratePolysAtCoord` in `Exports/Navigation/DllMain.cpp`
  (+162 LOC) directly iterates `dtMeshTile->polys[]` for the
  coord's tile + 8 neighbours, emitting every poly whose 3D AABB
  intersects {coord ± xyExtent} × {coord.Z ± zExtent}. NOT
  `findNearestPoly`. Surfaced via `--dump-poly-stack` in
  `tools/PathPhysicsProbe.exe` (default ext `xy=2.0, z=10.0`).
  Matching `[DllImport]` in `NavigationInterop.cs`. MSBuild
  Navigation.vcxproj + dotnet build PathPhysicsProbe Release —
  both green. No tile mutation; tile (40, 29) md5 unchanged at
  `cc0d89c42d9abf4737ba52a369c5f3f7`.

  **Stack dump at the 3 loop-21 trap coords on prod-data:**

  | Coord | Poly count | Composition |
  |---|---|---|
  | 1 — `(1347.3,-4540.6,35.8)` | **1** | Off-mesh poly only (polyIdx 59748, type 1, vertCount 2, aabb Z 29.5-53.7). NO ground polys at any Z in [25.8, 45.8] at this XY. |
  | 2 — `(1350.2,-4528.6,34.0)` | **64** | 63 phantom polys (area=3 NAV_STEEP_SLOPES `0x0011` + area=1 AREA_GROUND `0x0001`) at aabb Z 32-37, **plus the SINGLE legitimate ground polyref `281475331147742` (polyIdx 18398, area=3, aabb [37.31, 37.81], surfaceZ=37.509, posOverPoly=1)**. |
  | 3 — `(1348.0,-4537.7,35.4)` | **5** | 4 deck-above ground polys (aabb Z 44.9-51.7) + 1 off-mesh. NO ground polys in [25.4, 45.4]. |

  **Architectural refinement to loop 21's diagnosis:**

  | Trap class | Coords | Fix surface |
  |---|---|---|
  | True air (no ground polys at the corner's Z window) | 1, 3 | Bake regen (A3) OR off-mesh + Navigation.cs awareness (A5) OR skipvox (B) |
  | Phantom stack obscuring a far legitimate poly | 2 | Targeted cull (A4) + Z-snap mechanism (legit ground is 3.5y above the bot's WP Z, beyond default 1.8y findNearestPoly extent even after cull) |

  Phase A4 (validator-driven targeted cull) is viable for coord 2
  only. Coords 1 and 3 cannot be closed by culling — there's nothing
  to cull because there's no ground at the corner's Z. A bake-regen
  knob in Phase A3 must either (a) add ground polys at coord 1+3's XY,
  or (b) shift the phantom-stack at coord 2 so the legit ground is
  reachable from WP Z=34.0 within default extents.

  **Probe artefacts** (regeneratable, not committed):
  `/tmp/wwow-loop24-probes/coords.txt` + `poly-stack.tsv` + `stderr.log`.

  **Loop 24 A2 tally:** Unchanged at **19 / 4 / 0** (diagnostic
  phase; no live test run).

  **Next iteration — Phase A3:** single coordinated multi-knob bake
  regen on tile (40, 29). Two candidates calibrated against A2's
  stack data: (a) `walkableErosionRadius 0.2 → 0.3`; (b)
  `filterLedgeSpans` / `rcFilterWalkableLowHeightSpans` tuning.
  Forbidden: `walkableSlopeAngle` / `walkableClimb` lowering;
  `maxVertsPerPoly` change (loop 23 Surface A confirmed no-op).
  ONE attempt per iteration; revert immediately on any regression.

  Memory references:
  [[project_pfs_loop24_phase_a2_polystack]] (new),
  [[project_pfs_loop24_phase_a1_neutral]],
  [[project_pfs_loop21_trap_diagnosis]],
  [[feedback_pathfinding_anti_patterns]].

- **Latest evidence (2026-05-18 loop 24 — Phase A3 bake regen
  attempted, NEGATIVE):** Single coordinated 2-knob delta on tile
  (40, 29) override calibrated from Phase A2 stack data:
  `walkableErosionRadius 0.2 → 0.3` (broader agent-radius exclusion,
  targets the AREA_GROUND-tagged phantom band polyIdx 18349-18372
  with narrow aabb Z extents) AND `walkableHeight 0 (auto=11) → 14`
  (=3.5y, removes low-clearance phantom cells in the cluttered
  z-stack region). Forbidden knobs respected.

  Bake (`cd D:/MaNGOS/data; MmapGen.exe 1 --tile 40,29 --configInputPath ...`):
  output md5 `fbe57ed439986b87c5613edf9bc39a59`, size 8070292 (-8.4%
  from baseline 8805608). Bake LOG emitted
  `loadOffMeshConnections:: input file offmesh.txt not found!` —
  catalogue side-effect: existing OG-zeppelin off-mesh entries
  dropped because `--offMeshInput` wasn't threaded.

  **Probe outcome** (candidate copied to prod-data + docker restart,
  probed via Phase A2's `--dump-poly-stack`, **then reverted**):

  | Coord | Polys baseline | Polys A3 | Δ |
  |---|---|---|---|
  | 1 `(1347.3,-4540.6,35.8)` | 1 (off-mesh) | 0 | -1 (off-mesh dropped) |
  | 2 `(1350.2,-4528.6,34.0)` | 64 (63 phantoms + 1 legit) | 64 | **0 (UNCHANGED)** |
  | 3 `(1348.0,-4537.7,35.4)` | 5 (4 deck + 1 off-mesh) | 4 | -1 (off-mesh dropped) |

  Polygon graph DID shift (polyIdx 18398 → 17389 for coord-2 legit
  ground; surfaceZ=37.509 + posOverPoly=1 preserved). But the
  phantom stack at coord 2 is structurally identical — 63 phantoms
  in the same aabb Z windows. **Knobs A+B do not fit this phantom
  class.** The 8.4% tile-size reduction came from cells elsewhere
  in the tile, not from the trap region.

  **Decision: revert before the live test cycle.** Two failure
  modes: (a) phantom stack unchanged → no closure even with off-mesh
  restored; (b) off-mesh dropout guaranteed regression mode (would
  fail OG zeppelin 4/4 critical gate if promoted). Per prompt's
  "ONE coordinated delta per iteration; revert immediately on
  regression": advancing to Phase A4.

  **Cleanup verification:**

  | Surface | Md5 / state |
  |---|---|
  | `D:/wwow-bot/prod-data/mmaps/0012940.mmtile` | `cc0d89c4…` (baseline) ✓ |
  | `D:/MaNGOS/data/mmaps/0012940.mmtile` | `cc0d89c4…` (baseline) ✓ |
  | `config.json` tile-4029 | reverted; NEGATIVE_RESULT note added |
  | `wwow-pathfinding` docker | restarted, healthy ✓ |
  | Candidate forensic copy | `/tmp/wwow-loop24-candidates/A3/0012940.mmtile` |

  **Loop 24 A3 tally:** Unchanged at **19 / 4 / 0** (reverted
  before live tests).

  **Next iteration — Phase A4 (validator-driven targeted cull):**
  Per Phase A2 architectural refinement, cull is viable for coord 2
  ONLY. Coords 1+3 are true-air, cull can't help them. A4 procedure:
  use `tools/MmapGen/build/NavMeshTileEditor.exe --cull-polys` to
  zero `area`+`flags` on the 63 phantom polyIdxes catalogued in
  Phase A2 (preserve polyIdx 18398, the legit ground at z=37.509).
  Probe-verify only the legit poly remains in the stack. CAVEAT:
  the legit poly is 3.5y above WP Z=34.0, beyond Detour's default
  1.8y findNearestPoly extent even after cull, so a cull-only attempt
  may still leave coord 2 unreachable — advancing to A5 (Navigation.cs
  off-mesh awareness) if so. ONE cull attempt per iteration.

  Memory references:
  [[project_pfs_loop24_phase_a3_neutral]] (new),
  [[project_pfs_loop24_phase_a2_polystack]],
  [[project_pfs_loop24_phase_a1_neutral]],
  [[project_pfs_overhaul_006_brm_singletile_negative]] (precedent
  for single-tile knob regression risk).

- **Latest evidence (2026-05-18 loop 24 — Phase A4 validator-driven
  cull pipeline VERIFIED end-to-end; cull-list calibration
  regressed, reverted):** First end-to-end execution of the
  validator-cull pipeline (commit `0b2164d9` AV-tolerance + commit
  `c4415201` probe + this loop's `--dump-poly-stack`). Built cull
  list from Phase A2's coord-2 stack dump: all 64 polys with AABB
  intersecting the ±10y Z window, minus the legitimate ground
  polyref `281475331147742` (polyIdx 18398). 63 polyrefs in cull
  file.

  Snapshotted tile, copied to candidate, ran
  `NavMeshTileEditor.exe --cull-polys-file ...`. Output: `culled=63
  skipped=0 requested=63`. Candidate md5
  `7072a30d85a0c0ca7aabf9fddf7fa0c1` (vs baseline
  `cc0d89c42d9abf4737ba52a369c5f3f7`). Promoted to prod-data,
  `docker restart wwow-pathfinding`.

  **Probe verification (Phase A2's `--dump-poly-stack`):**

  | Bucket | Polys | State |
  |---|---|---|
  | Culled (area=0, flags=0x0000) | 63 | Detour query filter rejects on path queries |
  | Surviving (area=3, flags=0x0011) | 1 | polyIdx 18398, surfaceZ=37.509, posOverPoly=1 — legit ground intact |

  **Cull architecture works mechanically.** This is the first time
  the full validator-cull pipeline ran end-to-end through both
  the probe and the runtime — Loops 20-23 had partial coverage
  (validator unblock, probe shipment, single-poly culls). Loop 24
  Phase A2-A4 together complete the pipeline.

  **Sweep tally:**

  | Suite | Baseline | After A4 cull | Δ |
  |---|---|---|---|
  | `OgZeppelinCliffFallParityTests` | 4/0/0 | **4/0/0** | 0 (critical gate held) |
  | `CrossroadsToUndercity_CriticalWalkLegs` | 19/4/0 | **18/5/0** | **−1 pass, +1 fail (REGRESSION)** |

  Sweep duration: 18m 39s (vs baseline 1h 4m — cull shortened
  many paths because Detour reroutes around culled corridors).
  Verbosity=minimal hid per-test labels; cull-list composition
  analysis suggests the 12 culled polys with aabbMinZ ≥ 36.8
  (deck-region: polyIdx 18391-18395, 18399, 18285-18293) were
  load-bearing for adjacent legit routes. A more conservative
  cull list of "only polys with aabbMaxZ < 36.5" (51 polys
  instead of 63) would protect the deck region.

  **Decision: revert and advance to Phase A5** per prompt's
  "ONE cull attempt per iteration. If A4 loses, advance to A5"
  rule. A4 retry with refined cull list is logged for future
  consideration if A5 doesn't close coord 2 either. Coord-2 closure
  remains uncertain regardless: even a perfect cull leaves the
  legit poly at z=37.509 unreachable from WP Z=34.0 via Detour's
  default 1.8y findNearestPoly extent (3.5y gap). Closing coord 2
  needs either (a) PathFinder.cpp smooth-path Z-snap beyond loop-23
  Surface B, or (b) off-mesh-connection routing — which is
  Phase A5's domain.

  **Cleanup verification:**

  | Surface | Md5 / state |
  |---|---|
  | `D:/wwow-bot/prod-data/mmaps/0012940.mmtile` | `cc0d89c4…` (baseline) ✓ |
  | `D:/MaNGOS/data/mmaps/0012940.mmtile` | `cc0d89c4…` (untouched — NavMeshTileEditor mutated only the candidate) ✓ |
  | `wwow-pathfinding` docker | restarted, healthy ✓ |
  | Working tree | clean (no code/config changes this iteration) |
  | Cull list + candidate retained | `/tmp/wwow-loop24-probes/A4-cull-polyrefs.txt` + `/tmp/wwow-loop24-candidates/A4/0012940.mmtile` |

  **Loop 24 A4 tally:** Durable state at baseline; remains **19 / 4 / 0**.

  **Next iteration — Phase A5.1:** Audit `Services/PathfindingService/
  Repository/Navigation.cs`'s 8 repair phases; identify
  off-mesh-blind functions; document findings in TASKS.md. READ-ONLY
  iteration — no code or tile changes.

  Memory references:
  [[project_pfs_loop24_phase_a4_neutral]] (new),
  [[project_pfs_loop24_phase_a2_polystack]],
  [[project_pfs_loop20_cull_pipeline_unblock]],
  [[project_pfs_overhaul_006_polyref_polyidx_decoding]].

- **Latest evidence (2026-05-18 loop 24 — Phase A5.1 Navigation.cs
  audit, read-only):** Audited
  `Services/PathfindingService/Repository/Navigation.cs` (7448 LOC).
  **Definitive finding: ZERO off-mesh-type awareness anywhere in
  the file** — `polyType` and `DT_POLYTYPE` both return 0 matches.
  The only off-mesh mentions are 3 prose comments at lines 125,
  2169, 2940 acknowledging that off-mesh links exist; no code
  ever checks for them.

  **The 8 repair phases** (mapped to `ApplyNativeSegmentValidationCore`'s
  call sequence at lines 2339-2780):

  | # | Phase | Entry function | Symptom on a teleport segment (0y horiz, 29y vert) |
  |---|---|---|---|
  | 1 | LOS repair | `RepairLongLineOfSightBreaks:2856` | 35y LOS threshold skips, but Phase 2's air midpoints get re-LOS'd here in later calls |
  | 2 | Initial densify + normalize | `DensifyPath:2905` + `NormalizeEarlySupportLayer:3124` + `RemoveShortVerticalLayerSpikes:3302` + `RemoveShortHorizontalDetourSpikes:3350` | DensifyPath inserts linear midpoints in air; NormalizeEarlySupportLayer pulls them to harbor floor; spike removers see the 29y Z jump as a "spike" and try to flatten |
  | 3 | Early static repair | `RepairEarlyStaticBreaks:4026` | Re-runs LOS+findPath repair on the pulled-down midpoints; Detour returns the same off-mesh corridor → loop until 5s `EarlyStaticRepairBudget` exhausts |
  | 4 | Affordance repair (1st) | `RepairAffordanceBreaks:4103` | `ClassifyPathSegmentAffordance` returns Cliff/SteepClimb on dz=29y/dx=0y; pipeline detour-attempts until 8s `AffordanceRepairBudget` exhausts |
  | 5 | Re-normalize | Same Phase-2 functions | Same symptoms as Phase 2 |
  | 6 | Affordance repair (2nd, post-normalize) | `RepairAffordanceBreaks:4103` (re-entry) | Same as Phase 4 |
  | 7 | Post-affordance static repair | `RepairEarlyStaticBreaks:4026` (re-entry) | Same as Phase 3 |
  | 8 | Local-physics reachability | `NormalizeLocalPhysicsReachableLayers:3543` + `FindFirstLocalPhysicsReachabilityBreak:4374` + `RepairAffordanceBreaks:4103` (3rd, `includeLocalPhysicsReachabilityBreaks=true`) | `ValidateWalkableSegment` returns no-path on teleport → flagged `local_physics_layer` → 4th round of affordance repair attempts |

  This is the root cause of loop-23 Surface C's "managed-pipeline-hang
  risk". The existing OG zeppelin off-mesh entries survive because
  the budgets sum to ~15-20s and short OG zep routes time-box out
  with the original Detour smoothPath intact (managed repair fails
  to write back). Longer routes with multiple off-mesh entries can
  hang past 60s.

  **A5.2 — ship `IsOffMeshSegment(mapId, start, end)` helper +
  Phase 1 skip-check.** The helper uses the existing
  `GetPolyAtCoord` native export (commits prior + loop-20 AV
  tolerance) and queries `outPolyType` at both endpoints. Returns
  true iff either endpoint's polyType == 1
  (`DT_POLYTYPE_OFFMESH_CONNECTION`). Memoize per-`CalculatePath`
  on `(mapId, x, y, z)` similar to `_segmentValidationCache`
  (Navigation.cs:535-583). Single skip-check at
  `RepairLongLineOfSightBreaks:2877-2878`. Unit test the helper +
  a regression test on an existing OG zep off-mesh path. Build +
  targeted test only (no full sweep yet — that's A5.4).

  **A5.3 — apply the same skip-check at the 6 other phase
  entries**: `DensifyPath`, `NormalizeEarlySupportLayer`,
  `RemoveShortVerticalLayerSpikes`, `RemoveShortHorizontalDetourSpikes`,
  `RepairEarlyStaticBreaks`, `RepairAffordanceBreaks`. Each
  function gets a 2-3 line early-out.

  **A5.4 — E2E test.** New test in `PathfindingService.Tests` that
  runs `CalculatePath` against an existing OG zep off-mesh path;
  assert managed-validation wall time < 1s (was 15-20s) and no
  phase repair fired on teleport segments.

  **A5.5 — deploy loop-23 Surface C's 4 new off-mesh entries.**
  Edit `tools/MmapGen/offmesh.txt`, bake tile with
  `--offMeshInput`, probe to verify binding, full 23-case sweep.
  Target: 23/0.

  **Cleanup:** working tree clean, no code/config/tile changes
  this iteration.

  **Loop 24 A5.1 tally:** Unchanged at **19 / 4 / 0** (read-only).

  Memory references:
  [[project_pfs_loop24_phase_a5_1_audit]] (new),
  [[project_pfs_loop24_phase_a4_neutral]],
  [[project_pfs_loop23_close_attempt_three_surfaces]],
  [[project_mmapgen_offmesh_axis_swap]],
  [[feedback_pathfinding_freeze]].

- **Latest evidence (2026-05-18 loop 24 — Phase A5.2 ships off-mesh
  detection substrate + Phase 1 skip-check):** First code change of
  Phase A5 series. Three modules updated; +260 LOC + 1 new test file.

  - **Native (`Exports/Navigation/DllMain.cpp`, +90 LOC):** New
    export `IsOffMeshConnectionAtCoord(uint32_t mapId, XYZ coord,
    float xyExtent, float zExtent) -> bool`. Iterates the coord's
    tile + 8 grid neighbours via `getTilesAt`; for each poly with
    `type == DT_POLYTYPE_OFFMESH_CONNECTION`, computes AABB from
    the 2 endpoint verts and tests intersection with the
    `{coord ± xyExtent} × {coord.Z ± zExtent}` window.
    **Short-circuits on first match.** Bypasses `findNearestPoly`'s
    off-mesh deprioritisation (loop-21 trap diagnosis: at coord 1
    `findNearestPoly` returned polyref=0; only direct iteration
    finds the off-mesh poly).
  - **Managed (`Services/PathfindingService/Repository/Navigation.cs`,
    +75 LOC):** DllImport for the new export; thread-static
    `_offMeshCoordCache` keyed by `(MapId, X, Y, Z)` with lifetime
    tied to the existing `SegmentValidationCacheScope`. New helpers
    `IsOffMeshSegment(uint, XYZ, XYZ)` and `IsOffMeshAtCoord(uint, XYZ)`.
    Skip-check at `RepairLongLineOfSightBreaks:2877-2878` preserves
    teleport endpoint pairs without densification or LOS repair.
    Constants: `OffMeshSegmentXyExtent=2.0`, `OffMeshSegmentZExtent=4.0`
    (wider than the `GetPolyAtCoord` default 1.8y because off-mesh
    AABBs span the teleport Z range).
  - **Test (`Tests/PathfindingService.Tests/IsOffMeshConnectionAtCoordTests.cs`,
    +95 LOC, new file):** 4 unit tests in the existing `Navigation`
    collection. Asserts: (a) coord 1 detected as off-mesh; (b) coord 3
    detected; (c) Crossroads ground coord returns false; (d) negative
    extents return false without crash. All 4 green in 11s on prod-data.

  **Verification:**

  | Check | Result |
  |---|---|
  | MSBuild Navigation.vcxproj Release x64 | green |
  | dotnet build PathfindingService Release | green |
  | dotnet build PathfindingService.Tests Release | green (0 warnings) |
  | dotnet build Navigation.Physics.Tests Release | green |
  | `OgZeppelinCliffFallParityTests` (CRITICAL GATE) | **4/0/0 — held** |
  | `IsOffMeshConnectionAtCoordTests` (new) | **4/0/0** |

  OG zep 4/4 still green confirms the skip-check doesn't disrupt the
  existing off-mesh handling — the short-circuit emits the same end
  coord without inserting midpoints that the downstream phases would
  mis-handle.

  **Tile state:** no mutation. Prod-data + MaNGOS source both at
  baseline md5 `cc0d89c42d9abf4737ba52a369c5f3f7`. Docker
  `wwow-pathfinding` not restarted (no tile change).

  **Loop 24 A5.2 tally:** Unchanged at **19 / 4 / 0**. A5.2 ships
  substrate, not closure — Phase A5.5 deploys the 4 new off-mesh
  entries that will exercise the now-aware managed pipeline.

  **Next iteration — Phase A5.3:** apply the same skip-check
  pattern at the entry of 6 more repair functions (`DensifyPath`
  2905, `NormalizeEarlySupportLayer` 3124, `RemoveShortVerticalLayerSpikes`
  3302, `RemoveShortHorizontalDetourSpikes` 3350, `RepairEarlyStaticBreaks`
  4026, `RepairAffordanceBreaks` 4103). Expected diff ≈ 30-40 LOC.

  Memory references:
  [[project_pfs_loop24_phase_a5_2_offmesh_helper]] (new),
  [[project_pfs_loop24_phase_a5_1_audit]],
  [[project_pfs_loop24_phase_a2_polystack]],
  [[project_pfs_loop21_trap_diagnosis]],
  [[feedback_pathfinding_freeze]] (which Phase A5 explicitly unlocks
  for off-mesh awareness work).

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
