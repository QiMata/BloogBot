# Phase 5 Prep — Runtime Simplification Deletion Inventory

> **Iter 9 of the Recast Physics-Validated Overhaul loop.**
> Read-only inventory of the managed-repair + corridor-fallback + cache
> machinery that Phase 5 deletes after Phase 4 ships physics-validated
> bake-time output. Per kickoff guardrail 1, no code touched in Phase 0.

## Deletion target table

| File | LOC | Phase 5 action | Caller blast radius |
|---|---|---|---|
| [`Services/PathfindingService/Repository/Navigation.cs`](../../../Services/PathfindingService/Repository/Navigation.cs) | **7,697** | Replace with ~50-LOC thin Detour wrapper. **421 mentions of "Repair"** — entire file IS the repair pipeline (8 named repair phases: DynamicOverlay, EarlyStatic, StaticRoutePack, PostAffordance, OverlayStraight, BoundedCorridor static + LocalPhysics, LongSegment). | self-contained; replace via the new wrapper class |
| [`Exports/BotRunner/Movement/NavigationPath.cs`](../../../Exports/BotRunner/Movement/NavigationPath.cs) | **5,647** | Delete `ShouldPreferAlternatePath`, `IsRouteSupported`, corridor-fallback machinery. **39 mentions** of those terms; deeply embedded. | self-contained; uses are all internal |
| [`Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`](../../../Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs) | 901 | Proposal: "may delete... if perf measurements disagree, keep, but expect to delete" | Consumers: `PathfindingSocketServer`, `NavigationPathFactory` (×2), `TravelTask`, 2 test files |
| [`Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs`](../../../Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs) | 140 | Proposal: "may delete UNLESS needed for transports/elevators — research separately" | Consumers: `NavigationPath`, `PathfindingSocketServer`, `TravelTask`, 1 test file |
| [`Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs) | (8 SnapshotStallGuard mentions) | Delete `SnapshotStallGuard` usages (proposal: "physics-valid paths don't creep"). | self-contained in test file |

**Net deletion target: ≥13,000 LOC of pathfinding code** (vs proposal's
≥5,000 LOC estimate). The proposal cited "5,600 LOC" for Navigation.cs
specifically; actual file is 7,697 LOC and 421 Repair-mentions show
"5,600 LOC repair pipeline" was a rough lower-bound.

## Big surprise: Navigation.cs is 7,697 LOC, not 5,600

Proposal §0 and §5 both cite "Services/PathfindingService/Repository/
Navigation.cs's 5,600-LOC repair pipeline". The actual file is
**7,697 LOC**. The proposal author likely measured an earlier snapshot
or referred to specifically the repair section (excluding helpers). Either
way, **Phase 5's net deletion target should be revised upward**:

```
git diff --stat HEAD~N proposed deletion estimate:
   Services/PathfindingService/Repository/Navigation.cs |  -7,650
   Exports/BotRunner/Movement/NavigationPath.cs         |  -3,500 (estimate; reduces to ~2,000 LOC core path wrapper)
   Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs | -901 (if perf allows)
   Exports/BotRunner/Movement/PathfindingOverlayBuilder.cs        | -140
                                                          --------
                                                          ~-12,000
```

Proposal §5 acceptance criterion says "net deletion ≥5,000 LOC verified
via `git diff --stat`". Actual achievable: ≥10,000 LOC easily, possibly
≥12,000. **D4 should communicate this as a positive correction** —
Phase 5's simplification value is materially bigger than the proposal
estimated.

## SnapshotStallGuard is test infrastructure, not runtime

Proposal §3 Phase 5 step 4 reads: "Delete the runtime `SnapshotStallGuard`
collision-creep detector (no longer needed; physics-valid paths don't
creep into walls)." But `grep -rln "class SnapshotStallGuard"` matches
**zero runtime files** — the only file with `SnapshotStallGuard` mentions
is [`LongPathingTests.cs`](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs)
(8 mentions, all in test setup code).

So SnapshotStallGuard is a **test helper class**, not a runtime
collision-creep detector. The deletion target is real — Phase 5 removes
its uses from the live tests — but the framing is different from what
the proposal said. Two implications:

1. The deletion doesn't reduce runtime LOC (it removes test instrumentation).
2. The test reliability claim ("physics-valid paths don't creep") is
   verified by passing the 4 long-pathing tests post-deletion, NOT by
   removing a runtime safety net.

## Caller blast radius details

`ShouldPreferAlternatePath` / `IsRouteSupported`:
- ONLY referenced inside `NavigationPath.cs` itself. **Clean self-contained
  deletion.**

`StaticRoutePackCache`:
- `Services/PathfindingService/PathfindingSocketServer.cs` (live route serving)
- `Exports/BotRunner/Helpers/NavigationPathFactory.cs`
- `Exports/BotRunner/Movement/NavigationPathFactory.cs`
- `Exports/BotRunner/Tasks/Travel/TravelTask.cs` (the major consumer)
- `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
- `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
- `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`

Real consumers across both layers (service + bot-runner) — deletion
requires touching ~6 files. Decision deferred to Phase 5 mid-iter:
**measure p50 query latency post-Phase-4** and decide.

`PathfindingOverlayBuilder`:
- `Exports/BotRunner/Movement/NavigationPath.cs` (consumer)
- `Services/PathfindingService/PathfindingSocketServer.cs` (consumer)
- `Exports/BotRunner/Tasks/Travel/TravelTask.cs` (consumer)
- `Tests/BotRunner.Tests/Movement/PathfindingOverlayBuilderTests.cs`

Same blast radius as StaticRoutePackCache. Per proposal "UNLESS needed
for transports/elevators" — TravelTask is the transport-handling task,
so PathfindingOverlayBuilder's role in transport routing must be
researched before deletion. **Conservative path: keep PathfindingOverlayBuilder
in Phase 5, delete in a Phase 6 follow-up if transports unaffected.**

## Phase 5 acceptance criteria (per proposal §3)

- Navigation.cs's repair pipeline deleted (per R18 — same commit as
  thin-wrapper replacement).
- NavigationPath.ShouldPreferAlternatePath / corridor-fallback /
  IsRouteSupported deleted.
- SnapshotStallGuard deleted (from tests).
- Net deletion ≥5,000 LOC via `git diff --stat` (achievable: ≥10,000-12,000).
- All 4 long-pathing live tests pass.
- p50 path query latency ≤5ms; p99 ≤50ms.

## Phase 5 sequencing

Per proposal §7: Phase 5 depends on Phase 4. Concretely:

1. Phase 4 ships physics-validated tiles + the `NAV_AREA_GROUND_CONFIRMED/
   STEPUP/REPAIRED` area types.
2. Phase 5 starts only after Phase 4's full re-bake produces tiles with
   `Blocked=0` poly count (verified by Phase 0 probe re-run).
3. Phase 5's thin Detour wrapper consumes the new area types via
   `dtQueryFilter`; cannot ship without them.

## Phase 5 scope estimate

| Work unit | Wall-clock | Iters |
|---|---|---|
| Write new thin Navigation wrapper (~50 LOC); copy public API surface | 2-3 hr | 1 |
| Wire wrapper into PathfindingService DI + verify build | 1-2 hr | 1 |
| Run full BotRunner.Tests + PathfindingService.Tests suites (pre-deletion green gate) | 1-2 hr | 1 |
| **Delete Navigation.cs repair pipeline + corridor-fallback in single R18 commit** | 4-6 hr | 1-2 |
| Run full test suites + 4 long-pathing live tests; verify all green | 2-4 hr (incl. live runs) | 1-2 |
| Delete `StaticRoutePackCache` (if perf allows) + `PathfindingOverlayBuilder` (if research clears) | 2-3 hr | 1-2 |
| Measure p50/p99 path query latency | 1 hr | 1 |
| Apply OG-UC zep test guardrail-10 fix (`OrgrimmarUndercityZeppelinDockWaitSeconds 120→540`) | 0.5 hr | 1 |

**Phase 5 total: 13-21 hr wall-clock, 7-11 iters.**

## Phase 5 risk flags

| Risk | Likelihood | Mitigation |
|---|---|---|
| Thin Detour wrapper produces query results that fail one of the 4 long-pathing tests despite Phase 4's clean bake | Medium | Phase 5 starts with all 4 tests passing (Phase 4 exit criterion); deletion regression = revert commit per guardrail 4. |
| `StaticRoutePackCache` deletion regresses query latency | Medium | Measure first; keep if needed. Decision deferred to Phase 5 mid-iter. |
| `PathfindingOverlayBuilder` deletion breaks transports/elevators | Medium-high | Per proposal: research first; keep through Phase 5, delete in Phase 6 if safe. |
| Bot tests outside `LongPathingTests` rely on `SnapshotStallGuard`/`ShouldPreferAlternatePath` behavior | Low | Full BotRunner.Tests + PathfindingService.Tests run before AND after deletion per guardrail 4. |
| Hidden caller of Navigation.cs's repair methods | Medium | Grep + compile check before deletion; if found, deletion does NOT ship until caller migrated. |

## Phase 5 pre-conditions

- Phase 4 done (physics-validated tiles, new `NAV_AREA_*` constants).
- All 4 long-pathing live tests pass under the Phase-4 bake.
- D4 with `go` recommendation through to Phase 5.

## OG-UC zeppelin fix bundling

Per guardrail 10, the `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` test
(T2 in D3 manifest) is NON-pathfinding-class and won't pass via overhaul
work. Its fix is a 1-line constant change:

```csharp
// Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs:139
private const int OrgrimmarUndercityZeppelinDockWaitSeconds = 540; // was 120
```

Bundle into one of Phase 5's commits (not its own iter). Either:
- The thin-wrapper commit (good: associates the test fix with the test-
  suite green check)
- The PathfindingOverlayBuilder deletion commit
- A dedicated tiny commit between the major Phase 5 commits

## What this iter does NOT do

- No code modification.
- No call-graph deep analysis (Phase 5 first iter does this).
- No grep for hidden DI registrations of Navigation.cs (Phase 5 first
  iter task before any deletion).
