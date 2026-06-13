# Deck-Lip Arrival (Grunt→Frezza) — Diagnosis Handoff

## ✅ RESOLVED 2026-06-04 — it was the CONSUMER (walk-leg arrival), not the bake
The navmesh is sound (lip→deck connects via the NE-junction spiral — proven by
`tools/scripts/navmesh_view.py` connectivity flood-fill + 2 FindPath methods; the
earlier "disjoint islands" workflow claim conflated region-label with
connectivity and was WRONG). The live failure was the `TravelTask` walk-leg
arrival firing on **2D-proximity-to-the-goal while the bot was still below the
deck**: at z48 the bot is within `WalkLegArrivalRadius=15`y horizontally and
`WalkLegVerticalArrivalTolerance=6`y vertically of Frezza, so the leg "arrived"
~14y short and 5.6y below the deck and handed off to the Standard-policy
close-range approach, which drove straight at Frezza's XY (west) into the deck
overhang dead-end (stall z50.5 / fall z41). This is the "gets close, takes a
shortcut instead of walking the full path" bug.

FIX (Exports/BotRunner/Tasks/Travel/TravelTask.cs): new `WalkLegFinalArrivalRadius`
= 5y used ONLY for plain (non-transport) walk-leg COMPLETION (drive the navmesh
path all the way onto the deck to near Frezza before handing off), plus
`WalkLegVerticalArrivalTolerance 6→2` (don't "arrive" off the destination's tier
— e.g. on the lip/tongue below the deck). SCOPED: `WalkLegArrivalRadius` stays
15y for transport-handoff detection, flight-master approach, and
transport/elevator EXIT arrival, so those flows are unaffected (the first shipped
cut tightened the shared 15y constant globally — corrected to a dedicated
plain-leg constant). Both are refinements of the existing 2026-06-01 vertical-tier
guard; no bake/navmesh change, no off-mesh link, no physics step-up change.
Live-verified 6/6 across the unscoped + scoped versions: bot climbs the spiral to
the deck (z53.54), stops ~4.3–4.7y from Frezza, fallTime=0, screenshot shows it on
the deck. (A bisect showed vert-alone is NOT clean — it passed the flag but the
03-final FELL; the radius tightening is load-bearing.) Earlier-iteration
diagnosis below retained for history.

Branch: `fix/decklip-arrival-false-green` (PR #65). Current test:
`LongPathingTests.DeckLipClimbFromGruntToZeppelinBoardingPlatform`
(`WWOW_DECKLIP_DIRECT_BOARDING_TEST=1`) targets the waiting platform beyond
Frezza, Detour poly `0x1000015201B41` (`polyIdx=6977`). Tile 4029 = map1 tile
40,29 = `mmaps/0012940.mmtile`.

## What is solid (committed 545a782e — keep)
- The **fall** is fixed by the Z-banded source-support erosion. Live baseline
  (committed tile, 3 runs 16:55/17:04/19:44): the bot reliably climbs the spiral
  to **~z50.5 with `fallTime=0`** and stalls at the under-deck tongue
  `(1337.56,-4650.75,50.47)`. No fall.

## The stall, precisely located (this session's analysis)
- The OG zeppelin spiral ramp's top wraps physically continue **WEST under
  Frezza's deck overhang**. Deck = region 6 (z53.5–54.4, WoW X≈1322–1337) sits
  directly above the ramp tongue (regions 11 & 12, z~50.5) with an unclimbable
  ~3y gap and no walkable bridge up.
- The tongue is **real, well-supported, gently-sloped WMO floor** (instance
  234682, up-facing tris normalZ 0.92–0.99). Per-stage compact-span CSV proves
  it passes the slope test, `filterLedgeSpans` (rasterize==filterLedge there),
  low-height, AND erosion — it's interior-connected and ~6–7y wide at the stall
  row (X1333.5–1342). **No standard Recast filter rejects it**, and full-capsule
  erosion only bites ~1y off the far-west void edge, never the stall point. So
  the "thin slab that should be filtered" hypothesis is **refuted** — it is valid
  walkable geometry, just a gameplay dead-end.
- The legit climb corridor + NE-junction→deck route is the **east** half of the
  same surface (X≥1340). Probe `--detour-resolve` and `--smooth` from Grunt BOTH
  reach Frezza (z53.63, 0 Blocked) via the NE junction on the committed tile.

## The attempted fix and why it REGRESSED (do not repeat as-is)
- Added `cullWalkableCompactSpansInBoxes` (TileWorker.cpp) — a tile-scoped,
  JSON-gated (`navCullBoxesWow`) cull of walkable compact spans in a WoW AABB,
  run post-erosion+median (no erosion re-widening). Box used:
  `[1330,-4658,48.7, 1339.3,-4645,52.0]` → culled 2411 spans.
- Navmesh-level result: **all 4 probe gates PASS** — tongue poly gone (0 polys
  at the stall point), Grunt→Frezza raw+smooth still reach z53.63 0-blocked via
  the NE junction, dock routes baseline (clean=0 step=3 blocked=2), tile 3.27 MB.
- **Live result: consistent REGRESSION (2/2 runs).** The bot climbs to z45–46
  then **freefalls** (`fallTime` huge) to z41 and stalls — WORSE than the z50.5
  baseline. The fall originates near X1346/z46, a spot the cull box never
  touches → the cull changed the bot's **path**, not the surface under it.

## The load-bearing insight (start here next time)
- **The under-deck tongue IS the live bot's actual ascent route.** Pre-cull the
  bot climbs region-11 *westward* onto the tongue (X1347/z45.76 → X1338.69/z50.16
  → stall). The live `NavigationPath` (with its layer-repair / cliff-reroute /
  local-physics passes) **avoids the NE-junction→deck transition** that the
  offline probe takes cleanly, and uses the tongue instead. Removing the tongue
  does NOT make the live path fall back to the NE route — cliff-reroute walks the
  bot off the ramp.
- Therefore the user's premise ("delete the bad poly → bot takes the good route")
  is **incomplete**. The real blocker is *why the live path won't traverse the
  NE-junction→deck transition* (likely it's seen as a cliff / unclimbable step —
  cf. the known "pre-existing deck-lip ~1.75y step-up", og-zeppelin.json
  `ClimbOrgrimmarTowerToFrezza`). Fixing the bake to remove the tongue is only
  correct if the NE route is simultaneously made live-traversable.

## ITER 2 (2026-06-04) — the navmesh PATH is proven correct; the drift is a consumer reaction to local geometry
- **Probe from the z47.5 hand-off point** `(1345.4,-4652.6,47.5)` → Frezza
  (`--detour-resolve --smooth`): **28 segs, 0 Blocked**, stays on the corridor
  (min X at z49–52 = **1339.97**, never the tongue X1337), climbs the NE junction
  to the deck (z54.06) and walks to Frezza (z53.63). So the navmesh path is
  correct **from every start point**, not just from Grunt.
- **Live trace** (baseline diag + poll `[TRAVEL_WALK_NAV]` window dumps): it is a
  SINGLE leg; the walk-nav drive follows the 95-corner path correctly to **idx≈68
  (z47.5–48.3, X1344–1348)** — last waypoint-query at z47.5 correctly points NE to
  (1344.1,-4653.9,48.6), hdgErr=0.23, moving fine. Then **ALL NAV/TRAVEL logging
  stops for ~4.2 s** (the "leg transition") while the bot moves to the tongue
  (1337.6,-4650.8,50.5). No existing log captures the drift window.
- **Mechanism (consumer, NavigationPath.cs):** `GetNextWaypoint` has many
  `ResolveDirectFallback` exits (aim straight at the destination = Frezza, west)
  and `TryReplanFromNearVerticalLayerMismatch` →
  `TryPromoteLongTravelDestinationProgressWaypoint` (promote a waypoint *toward
  Frezza*). At the stall the path recalcs to `waypointCount=1`
  (`reason=stalled_near_waypoint`). The drift toward Frezza's XY (west) lands on
  the tongue (baseline) or the void (iter-1 cull → fall).
- **The bake↔consumer bridge (the only real bake-side lever):**
  `TryReplanFromNearVerticalLayerMismatch` only SUPPRESSES the destination-ward
  promotion when `IsUphillLayerProgression(...) && PreservesWalkableCorridor(...)`.
  `PreservesWalkableCorridor` = local-physics-consistent
  (`MaxUpwardRouteZDelta <= LOCAL_PHYSICS_ROUTE_LAYER_REJECT_Z_DELTA=5`) **&&**
  collision-support **&&** `IsSegmentWideEnoughForCharacter` **&&**
  `HasWalkableNavmeshSamples`. All four are NAVMESH-determined. So if the corridor
  near z48–52 is too narrow, has a too-tall poly Z-step, or lacks collision
  support, the consumer treats it as a layer mismatch and promotes the bot toward
  Frezza (→ tongue). A tile-scoped bake refinement (wider + finer-Z-stepped
  corridor / making the climb a clean uphill layer progression) keeps these true
  and the bot on the corridor — WITHOUT touching the consumer.
- **Constraint reality:** the user forbids consumer changes, but the navmesh PATH
  is already correct. The bake fix must therefore target the LOCAL geometry the
  consumer samples (`PreservesWalkableCorridor`/`IsUphillLayerProgression`), not
  the global path. The exact failing predicate is not yet pinned — it needs
  instrumentation (next).

## ITER 3 (2026-06-04) — ROOT CAUSE CONFIRMED LIVE; it is NOT a bake issue
Temporary `[DECKLIP_DIAG]` instrumentation (since reverted, tree clean) in
`TravelTask.ExecuteWalkLeg` + the legs-exhausted final-approach branch produced
an **irrefutable live trace**:
- `walkleg-arrival ... pos=(1344.4,-4653.7,48.0) legEnd=(1331.1,-4649.5,53.6)
  dist=13.96 vDelta=5.62 radius=15.0 arrived=True` — the previous tick (z47.1,
  vDelta=6.50) was `arrived=False`. So **`walk_arrived` fires at z48.0**, the
  instant the bot rises to <6y below the deck while <15y horizontally — **~14y
  short of Frezza and 5.6y below the deck**.
- Immediately after: 93 ticks of `final-approach-to-target ... legsExhausted
  route=1` (the same-map branch `TryNavigateToward(_targetPosition)`, Standard
  policy, **no NAV_EXEC** — that is why the logs went silent at z47.5), drifting
  the bot from (1344.3,-4653.7,48.0) **west+down to (1337.4,-4649.4,41.2)** (this
  run it fell; other runs it stalls on the tongue at z50.5 — same mechanism).

**Root cause (code-confirmed, `TravelTask.cs`):** `ExecuteWalkLeg` completes the
walk leg via `TryGetWalkLegArrival` = `distance2D <= WalkLegArrivalRadius(15) &&
verticalDelta <= WalkLegVerticalArrivalTolerance(6)`. The OG spiral physically
passes within 14y/5.6y of Frezza at z48, so the walk leg "arrives" there, **6y
below the deck**. With the legs exhausted, `Execute` falls to
`TryNavigateToward(_targetPosition)` (Standard policy) which drives the bot
straight at Frezza's XY (west) — onto the under-deck tongue (stall) or off the
ramp (fall). **This trigger is pure 2D distance + vertical delta — NOT
navmesh-sampled — so NO bake change can prevent it.** Had `walk_arrived` not
fired early, the LongTravel walk leg (which WAS correctly following the 95-corner
corridor path) would have climbed to the deck and reached Frezza.

**Conclusion: the deck-lip failure cannot be fixed in the bake.** The navmesh is
correct (iter 2). The fix must be one of (all CONSUMER, which the mission
forbids): (a) make the walk-leg arrival deck-tier-aware / tighten
`WalkLegVerticalArrivalTolerance` so the leg doesn't "arrive" 5.6y below the
target; (b) have `CrossMapRouter.PlanRoute` emit a leg to the NE-junction deck
entry before the final Frezza leg; (c) make the legs-exhausted final approach use
the LongTravel corridor policy instead of a direct Standard drive. This requires
the user to relax the "do not touch the consumer" directive for this case.

## Next-iteration plan (iter 3 — SUPERSEDED by the confirmation above)
0. **Instrument first (temporary diagnostic, not a band-aid):** add log lines in
   `TryReplanFromNearVerticalLayerMismatch` (which predicate of
   `PreservesWalkableCorridor` returns false, and the promoted waypoint),
   `ResolveDirectFallback`, and the corridor-drift replan. Re-run the baseline
   live test, capture the **drift window** (idx≈68→tongue), and identify WHICH
   navmesh-sampled check fails at the exact drift point. Then revert the logging.
1. Diagnose the **NE-junction→deck transition** the live path avoids: trace the
   second leg (post-z47.5) — `NAV_EXEC`/`NAV_TELEM` stop at the leg transition,
   so add/inspect second-leg telemetry. Use `mmo-movement-diagnostics`.
   Compare offline probe (`--detour-resolve --smooth`, StepUp Clear at z52→53.6)
   vs the live cliff-reroute's verdict on that same step.
2. If the NE→deck step is the live blocker: bake a gentler/multi-step navmesh
   ramp at the junction (detail-mesh / step grading) so the live path follows it
   — WITHOUT raising physics step-up tolerance and WITHOUT an off-mesh link.
3. Only after the NE route is live-traversable, re-enable the tongue cull
   (`navCullBoxesWow`) so the bot can't drift onto the dead-end.

## Tooling notes
- The `cullWalkableCompactSpansInBoxes` mechanism is kept INERT (empty
  `navCullBoxesWow`); the bake is byte-identical to committed. Populate the box
  list to re-enable.
- Probe stderr is wrapped by PowerShell as exit-1 (cosmetic); capture stdout to a
  file. MmapGen 255 exit under `2>&1 | Select-String` is likewise cosmetic — the
  "Generated file" line is the real success signal.
- Verify live results from the timeline sidecars
  `tmp/.../DeckLipClimbFromGruntToLiteralFrezza/02-climb-poll-*.json`
  (`snapshot.position`, `fallTime`) + READ the .png (R16); the test prints
  "Duration: < 1 ms" even on real runs.
