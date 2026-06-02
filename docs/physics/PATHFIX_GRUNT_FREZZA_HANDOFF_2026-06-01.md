# PATHFIX HANDOFF — Grunt#1 → Frezza (OG Zeppelin Tower), 2026-06-01

> Turnkey handoff for the abandoned-RED `DeckLipClimbFromGruntToLiteralFrezza`
> long-pathing test. Produced by a probe-driven diagnosis + a 6-agent
> adversarial-verification Workflow. **Read this before touching `NavigationPath.cs`,
> `Navigation.cs`, the bake, or adding any caller-side acceptance rule.**
>
> Companion framework artifacts (monorepo `E:\repos`, `develop`):
> - Skill: `.claude/skills/mmo-physics-pathing-probe/SKILL.md` → **Recipe 2**
> - Lesson: `docs/PCE_IMPLEMENTATION_LESSONS.md` → **L70**
> - Contract: `docs/PATHFINDING_COLLISION_CONTRACT.md`

## TL;DR

The bake mesh and the runtime physics **already solve** Grunt#1→Frezza cleanly.
The live failure is a **managed-layer defect**, not a bake or physics defect, and
the ~15 caller-side acceptance-rule relaxations committed 2026-05-27 (5346cd78 …
0689fb5d, 57832873, b3c107ba) were band-aiding the **wrong surface** — which is why
the test stayed RED. **Do not add a 16th relaxation.**

## Evidence (reproducible, headless — prebuilt probe, no live server)

```powershell
cd "E:\repos\Westworld of Warcraft"; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'
$exe='.\Bot\Release\net8.0\PathPhysicsProbe.exe'
# Grunt#1 = (1332.76,-4633.40,24.0783)  Frezza = (1331.11,-4649.45,53.6269)  map 1
& $exe --map 1 --start 1332.76,-4633.40,24.0783 --end 1331.11,-4649.45,53.6269 --detour-resolve --smooth
& $exe --map 1 --start 1332.76,-4633.40,24.0783 --end 1331.11,-4649.45,53.6269 --detour-resolve
& $exe --map 1 --start 1353.1,-4525.3,34.6      --end 1331.11,-4649.45,53.6269 --detour-resolve --smooth
```

| Probe | Result |
|---|---|
| smooth detour-resolve Grunt1→Frezza | **128 corners / 127 segs, 108 Walk + 19 StepUp, ZERO Blocked, exit 1.** Monotonic +29.55y climb, ends at Frezza via the deck off-mesh link (base tile (10,26) p=(-4653.50,53.61,1329.88), `LINKED dxz^2=0.00`). |
| raw detour-resolve Grunt1→Frezza | 57 corners / 56 segs, 14 Walk + 42 StepUp, **ZERO Blocked**, exit 1. |
| smooth detour-resolve FROM live stall (1353,-4525,35) | only 5 corners; segs 1-3 `Vertical/Clear` at 79–86° with drops 2.55/6.11/7.98 → degenerate near-vertical scramble. Same degenerate stub even when the target is a known-good interior corner → the stall point is genuinely **off-corridor**. |
| naive single straight Grunt1→Frezza (no resolve) | `Blocked/BlockedGeometry` (expected — a 16y straight line through the tower wall). The RESOLVED route is what is walkable. |
| live timeline (R16) | bot ended at `(1352.77,-4525.89,34.89)` ~126y from Frezza, `reason=path_unavailable resolution=no_route blocked=1.00`; PNG shows the Tauren wall-pressed against the tower exterior. |

`--detour-resolve` calls the **same native `Navigation.dll` `FindPath`** the live
service calls (`tools/PathPhysicsProbe/Program.cs:149`). Probe-clean ⇒ bake +
runtime physics AGREE on this corridor.

## Root cause (source-confirmed)

1. `Services/PathfindingService/Repository/Navigation.cs` → `CalculateRawPath` runs
   **`TryFindInteriorProjectionGap`** (`Navigation.cs:2092`). It walks the interior
   smooth corners and calls `GetPolyAtCoord` with XY extent `max(agentRadius, 2.0)`
   and **Z extent `NativePathInteriorProjectionZExtent = 1.8`** (consts `Navigation.cs:87-89`).
   The off-mesh-link-densified climb corners sit over a navmesh **no-poly gap by
   design** (that is what the off-mesh link is), so a corner returns no-poly →
   `blockedReason="interior_projection:98"`, `blockedSegmentIndex=97`.
   **This check lacks the off-mesh-link-AABB-containment skip** that the loop-24 S1.3
   close-out already added to `PathRouteAssertions` / `LongPathingRouteTests` for
   exactly this failure mode.
2. `Exports/BotRunner/Movement/NavigationPath.cs` → `SelectServiceSeedPath` →
   `TryGetProjectionBlockedPrefix` **truncates** the route at `blockedSegmentIndex+1`
   corners, stranding the bot ~126y short, where re-planning only finds the
   degenerate exterior scramble. The 15 relaxations all tweaked acceptance rules
   around this truncation symptom.

## REFUTED — do not re-chase this

A reviewer blamed the **Tauren capsule radius** (service `FindPathForAgent` r=1.0247
vs probe `FindPath` r=0.3064) for the exterior-vs-interior route divergence. Source
disproves it: `PathFinder::m_capsuleRadius` is **set but never read** in route
resolution (`calculate()→updateFilter()` is area/swimming-flag only; `dtQueryFilter`
is radius-independent; `DllMain.cpp:2236-2239` says the agent capsule is "advisory
only"). Re-running the probe with `--radius 1.0247 --height 2.625` gave a
**byte-identical** interior route. Radius affects only per-segment classification.

## OPEN QUESTION — resolve FIRST

The saved service dump (`tmp/test-runtime/results-pathfinding/waypointdump_grunt_frezza_vs_snurk_20260527_iter9a.trx`)
shows a **144-corner EXTERIOR** route (out the 1304,-4553 lane, up to 1357,-4516)
while the probe gives a **128-corner INTERIOR** route for the same nominal endpoints.
Since radius is route-irrelevant, this MUST be a **different query input**. **Capture
the EXACT start/end the live service requested** (live bot pose at route-request
time). If the live START is already off-corridor (~1352,-4527), the real defect is
**upstream travel / leg-stitching** that drifted the bot off-corridor BEFORE the
Frezza approach — fix that, not the projection check.

## Fix surface (under the pathfinding freeze)

**Forbidden:** another caller-side acceptance relaxation in `NavigationPath.cs`;
any NEW query-time managed repair to mask a bake output; loosening a runtime
threshold.

**Option A (recommended, surgical, matches the established loop-24 pattern):**
make `TryFindInteriorProjectionGap` (and any sibling poly-presence projection check)
**skip corners contained in a known off-mesh-link connection's AABB**. This corrects
a false-positive in an existing validity check (removing an incorrect rejection) —
it is not new repair. Reuse the off-mesh-AABB-containment skip already present in
`PathRouteAssertions` / `LongPathingRouteTests.GetLocalPhysicsReachabilityFailure`.

**Option B (if A is insufficient):** in `tools/MmapGen`, make the deck-ascent
off-mesh-link gap navmesh-representable (or fix the off-mesh-link definition so the
1.8y/2.0y projection window straddles real anchor polys). Heavier; requires a
rebake of the promoted tile and re-validation that it does not over-prune other
checkpoints.

**Separately:** the hand-picked deck-edge segment
`(1338.1,-4646.0,51.6)→(1335.2,-4644.4,53.5)` is a REAL polygon-graph 2D-adjacent-
across-Z defect (deck z=53.54 over platform z=51.49) but is **NOT on the resolved
route** — address it in MmapGen only if it resurfaces as the next blocker once the
climb is poly-covered. It is not what blocks this route.

## Validation gate (all required for DoD — currently BLOCKED headless)

1. **Unblock the build:** `global.json` pins SDK **8.0.100** (`rollForward=latestFeature`);
   only 9.x/10.x are installed, so every `dotnet build/test` fails. Install a .NET 8
   SDK (latest 8.0.x feature band). **Do NOT edit the checked-in `global.json`** —
   WWoW tracks externally-owned `QiMata/BloogBot`.
2. **Unit test the fix:** add a deterministic test that an off-mesh-link gap corner
   does NOT emit `interior_projection`, and that the Grunt1→Frezza service path is
   no longer truncated. Run the `WaypointDumpDiagnostic` / `NavigationPathTests`
   focused batch green.
3. **Re-probe:** smooth detour-resolve Grunt1→Frezza must still be 0-Blocked AND the
   service path must no longer emit `interior_projection:98`.
4. **Live gate (R3/R11/R16):** run `DeckLipClimbFromGruntToLiteralFrezza` on the live
   StateManager+WoW+MaNGOS stack to GREEN. Capture screenshots at start / before the
   deck climb / on success and READ them — confirm the bot reaches Frezza on the deck
   via pure Detour, not by a managed repair patching the output.

## Process notes

- Probe + `Navigation.dll`/`Physics.dll` are prebuilt at `Bot/Release/net8.0/`
  (2026-05-22; native classifier unaffected by the 2026-05-27 caller C# churn).
- Data dir for the promoted tile: `D:\wwow-bot\test-data` (tile `0012940.mmtile`).
- Workflow transcript: `tmp/test-runtime/pathprobe/wwow-pathfix-verify.workflow.js`;
  raw probe TSVs under `tmp/test-runtime/pathprobe/`.

## UPDATE 2026-06-01 (live-validated on the running stack)

Built with a local .NET 8 SDK (`E:\dotnet8`, since global.json pins 8.0.100 and none
was installed) and ran `DeckLipClimbFromGruntToLiteralFrezza` against the live
vmangos+realmd+maria stack (all healthy). Three findings that REVISE this handoff:

1. **The open question is RESOLVED — the live service now returns the CLEAN route.**
   The live diag shows `[NAV_PATH] service-request exit corners=128 result=raw_detour
   blockedIndex=null blockedReason=none` for the literal Frezza target from the Grunt
   base — i.e. the same clean 128-corner interior route the probe produces, with **no
   `interior_projection` block**. The stale 2026-05-27 exterior-144 / interior_projection:98
   dump no longer reproduces. **So Option A (off-mesh-AABB skip in
   `TryFindInteriorProjectionGap`) is NOT needed for THIS live run** — keep it as a
   latent hardening only if a future query re-triggers the false projection block.

2. **A FALSE-GREEN test bug was found and fixed (R16).** The test first reported PASS,
   but the `03-final` snapshot + screenshot showed the bot at the **tower base**
   (1328.2,-4635.3,**z=24.8**) ~29y below Frezza. Cause: the arrival check accepted a
   `[TRAVEL_LEG] complete reason=walk_arrived` message from the **whole RecentChatMessages
   ring with no proximity/delta guard**, so a stale `walk_arrived` declared arrival at
   the base ~3s in, short-circuiting the climb. Fix (committed): arrival now requires the
   bot to actually be near Frezza (dist2D≤6/|dz|≤4 primary; a `walk_arrived` only counts
   when it is NEW since baseline AND within dist2D≤12/|dz|≤6). With the fix the test is
   honestly **RED**.

3. **The ACTUAL live blocker is caller-side base→climb consumption, NOT the off-mesh
   projection.** Honest run: the bot auto-advances the first ~6 near-coincident
   base waypoints (idx 2→4→6, all clustered at x1326-1336 / y~-4634 / **z~24**, within
   ~3y — the smooth-path start-jitter cluster) by `reason=in-radius` **without really
   moving**, then stalls at the base with `physics-read … hitWall=False blocked=1.00
   normal=(0.00,0.00)` (a SEMANTIC block, no physical wall) and never commits to the
   first real climb StepUp. Final tick `pos=(1335.0,-4636.5,24.3) tree=Success
   tasks=2(TravelTask)`. **The bot never reaches the off-mesh-link gap**, so the climb
   itself is untested live.

### Revised next target (NOT a 16th caller relaxation)

Root-cause the base→climb consumption stall on the clean 128-route:
- Why does `GetNextWaypoint`/the NAV_EXEC waypoint-query keep returning an EARLY/backward
  base waypoint (e.g. (1326.8,-4634.8,23.8)) after idx has advanced to 6, instead of the
  first climb StepUp (~idx 9, (1335.5,-4635.9,24.7)→(1336.2,-4634.0,24.7))?
- Is the smooth-path start-jitter cluster (≥6 near-coincident base corners) itself the
  problem (route-quality at the START)? If so the durable fix is smooth-path start
  de-duplication in `tools/MmapGen`/`PathFinder.cpp` smoothing, NOT caller acceptance
  tweaks.
- What is `blocked=1.00 normal=(0,0,0)` actually measuring when `hitWall=False`? A
  semantic block with no wall + currentSpeed≈0 at the base is the same shape as the
  loop-25 D2/D3 `vertical_layer_mismatch`. Confirm whether the bot is failing to issue a
  StepUp/forward intent at the base→ramp transition.
Validate any fix with the (now-honest) live test + R16 screenshots before claiming green.

### Repro commands used

```powershell
$env:DOTNET_ROOT='E:\dotnet8'; $env:PATH='E:\dotnet8;'+$env:PATH
cd "E:\repos\Westworld of Warcraft"
$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'
$env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='2'
[Environment]::SetEnvironmentVariable('WWOW_LONG_PATHING_SETTINGS_PATH', $null, 'Process')
dotnet build Tests\BotRunner.Tests\BotRunner.Tests.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet test  Tests\BotRunner.Tests\BotRunner.Tests.csproj -c Release --no-build --no-restore -m:1 `
  -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" `
  --logger "console;verbosity=minimal" -- RunConfiguration.TestSessionTimeout=1200000
```
Diag: `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`; screenshots:
`tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`.

## UPDATE 2 (2026-06-01, later) — base→climb ROOT-CAUSED + FIXED; bot now climbs z24→z42; next blocker = mid-climb disconnect

After fixing the false-green (UPDATE 1), the honest RED localized the real blocker and it
was root-caused + fixed with a unit + live-validated change.

**Root cause (FIXED): vertical-blind walk-leg arrival.** `TravelTask.CanCompleteWalkLeg`
returned `true` unconditionally for a non-transport walk leg, so `TryGetWalkLegArrival`
completed the leg on **2D distance ≤ 15y alone**. Frezza (z=53.6) is ~14.5y DUE-SOUTH of
the Grunt base (z=24), so at the base the 2D distance is ~14.8y ≤ 15y → the leg
**false-completed at the base** (~30y below the deck), exhausting the route and dumping the
bot into the line-178 `route-exhausted` branch which calls `TryNavigateToward(_targetPosition)`
with the **default Standard policy** (no LongTravel corridor/off-mesh, and no immediate
diagnostics — the "12s silence"). The bot then sat frozen at the base.

**Fix (committed, freeze-aligned — an arrival-VERIFICATION fix per R2/R3, NOT a route
relaxation):** added `WalkLegVerticalArrivalTolerance = 6.0f` (matches the transport vertical
tolerance) and changed `CanCompleteWalkLeg` non-transport branch to
`return verticalDelta <= WalkLegVerticalArrivalTolerance;`. A walk leg now only "arrives"
when the bot is within the 2D radius AND on roughly the same vertical layer.

**Validated two ways:**
- Unit: `Tests/BotRunner.Tests/Travel/TravelTaskTests.cs` →
  `Update_TargetDirectlyAboveWithinHorizontalRadius_DoesNotCompleteWalkLegAtBase` (new guard);
  full `TravelTaskTests` 15/15 green (transport-handoff completion tests unaffected).
- Live: the bot now **climbs the spiral ramp z24→z42** (idx0→~60 of 128, each `afford=StepUp`,
  `leg=0/1` — no premature completion), vs previously stalling at the base. ~60% of the climb.

**NEXT BLOCKER (new, distinct): mid-climb client disconnect/reset at ~z42 on the upper spiral.**
At ~z41.9 (idx60) the FG client returns to `LoginScreen` mid-run (a second `[TICK#1]
screen=LoginScreen map=0` ~3s after reaching z42), then re-enters world at the same z=41.9
with the objective LOST → `IdleTask` → the 20s stuck-guard fails the test. No WER crash dump
was produced → most likely a **server-side disconnect** (vmangos movement validation/anti-cheat
kicking the StepUp spiral climb), not a hard client crash. This is a movement-emission / anti-cheat
parity problem (see `mmo-movement-diagnostics` / `mmo-fg-client-re` / `crash-cluster-triage`),
a deeper and distinct surface from the arrival fix — do NOT band-aid it.

Revised next target: capture WHY the client disconnects at ~z42 — pair the FG screenshot
timeline with the bot's outbound movement packets on the StepUp climb vs what vmangos accepts
(speed/Z-delta validation), and check the vmangos `wow-mangosd` container log for a kick
reason around the disconnect timestamp. Secondary latent issue worth fixing: the line-178
`route-exhausted` fallback uses Standard policy — should use `NavigationRoutePolicy.LongTravel`
so a legitimate near-target route exhaustion can still drive vertical/off-mesh nav.

## UPDATE 3 (2026-06-01, later) — the z42 "disconnect" was 2 distinct issues; bot now climbs base→deck-lip (z47, 80%); TRUE final blocker isolated = deck-lip physics slide-back

The "~z42 disconnect" decomposed into THREE more fixes (each live-isolated), peeling the onion
to the original deck-lip problem:

1. **Disabled-hooks reset symptom (FIXED, test-side).** DeckLip is SAME-MAP, but it
   unconditionally called `DisableForegroundPacketHooksForCrossMapTransfers()` (meant only for
   cross-map transfers). With hooks disabled the client dropped to LoginScreen mid-climb
   (auto-relog, objective lost). Commenting that out for this same-map test removed the reset
   (no WoW crash dump was ever produced — it was never a hard crash).

2. **Top-level arrival was ALSO vertical-blind (FIXED).** `TravelTask.Update` (line ~163)
   completed on **3D distance ≤ `_arrivalRadius`** — at z41, ~8y horizontal + ~12.5y below
   Frezza = ~15y 3D ≤ 15 → `[TRAVEL_COMPLETE] dist=15.0`, pop→Idle. Fixed to require horizontal
   proximity AND `verticalDelta ≤ WalkLegVerticalArrivalTolerance`.

3. **The same-map travel arrival radius (15y) was far too loose (FIXED, BLAST RADIUS).**
   `ActionDispatcher.cs` `sameMapTravelArrivalTolerance` was `15f` (overriding
   `TravelTask.DefaultArrivalRadius=5f`), so TravelTo "arrived" ~14y short / on the ramp ~6y
   below the deck (z47.8). Tightened to `5f` (reach the target / NPC interaction range).
   **⚠️ BLAST RADIUS: this affects EVERY same-map `TravelTo`** — bots now must reach within 5y
   instead of 15y. Validate against the full long-pathing / LiveValidation suite before merge;
   some routes whose final approach only gets within ~10y may need their own attention (that is
   correct behavior surfacing, but it is a behavior change).

**Result:** the bot now climbs the OG zeppelin spiral from the **base (z24) to the deck-lip
(z47.4, idx102/128 ≈ 80% of the route)** — every segment `afford=StepUp`, no false completion,
no disconnect (hooks enabled). HUGE progress from the original base-stall.

**TRUE FINAL BLOCKER (isolated, deep — the original deck-lip problem):** at z47-48 the bot
`reason=stalled_near_waypoint`, oscillates, and **slides back to z41**, where the stuck-guard
fires. Probing the final stretch FROM the live stall point
(`PathPhysicsProbe --map 1 --start 1347.0,-4652.1,47.4 --end 1331.11,-4649.45,53.6269
--detour-resolve --smooth`) returns **30 corners, ALL `Walk`/`StepUp`/`Clear`, ZERO `Blocked`**,
climbing cleanly z47→53.6 onto the deck to Frezza. So the route + static physics say the
deck-lip IS traversable — but the LIVE continuous swept-capsule motion **can't sustain the steep
climb and slides back**. This is the **B3-class static-vs-swept divergence** (static 8-sample
probe ≠ live continuous sweep), a PhysicsEngine slope/step-up / movement-execution issue on the
steep deck-lip — NOT bake, NOT arrival, NOT route. This is the deep physics core (the original
reason this test exists), now cleanly isolated with everything else stripped away.

Next (deep, do NOT band-aid): `mmo-movement-diagnostics` — instrument the bot's per-tick
movement on the z47→53.6 deck-lip (forward/StepUp intent vs actual Z delta vs slide-back),
compare the live swept-capsule behavior to the static probe's `StepUp/Clear` verdict, and fix
the slope/step-up handling in `Exports/Navigation/PhysicsEngine.cpp` so the bot can sustain a
steep StepUp climb without sliding back. Validate with the (honest) DeckLip live test +
screenshots.

## UPDATE 4 (2026-06-01, later) — it is NOT physics; it is an INVALID (non-monotonic) smoothPath route at the deck-lip

Verifying whether the deck-lip waypoints are actually a valid route (rather than assuming a
physics wall) found the real cause. The route is `Walk`/`StepUp`/`Clear` **segment-by-segment**,
but the SEQUENCE oscillates:

- **The bot follows the SMOOTH path; it is non-monotonic at the deck-lip.** Probe dump of the
  full Grunt→Frezza smooth route, segments idx86-101, oscillate
  z47.04→46.76→46.47→46.83→47.18→46.74→46.29→46.69→47.09→46.75→46.41 — **down-up-down-up by
  ±0.4y in a tight ~2y XY cluster** (1346-1348, -4650..-4652), not climbing.
- **Quantified (probe, same endpoints):** SMOOTH = 127 segs, **16 DOWN segments, 5.7y of backward
  climb**; RAW (no smoothing) = 56 segs, **1 DOWN segment, 0.6y**, and the RAW deck-lip is
  perfectly monotonic (z45→46→47→48→50.6 consecutive StepUps). **smoothPath introduces the jitter;
  the underlying bake/raw path is clean.**
- **Why the bot fails:** following the oscillating smooth corners it is steered backward/down,
  makes no net progress (`reason=stalled_near_waypoint`), and slides back down the steep ramp
  to z41 → stuck-guard. NOT a slope/step-up physics wall (raw climbs it fine), NOT the bake.

**Mechanism (native `findSmoothPath`, `Exports/Navigation/PathFinder.cpp:1750-1926`).** The OG
zeppelin tower is a SPIRAL — the same XY stacks multiple Z layers (lower wrap ~24, mid ~35,
upper ~47). In the smoothing loop, `moveAlongSurface` (1822) slides a fixed `SMOOTH_PATH_STEP_SIZE
= 2.0f` step toward the steer target and `getPolyHeight(polys[0], result, &result[1])` (1825)
resolves Z; on the tight spiral curve / overlapping layers this intermittently resolves to a
lower wrap (or overshoots the curve and comes back), dropping Z, then recovers next step → the
down-up oscillation. The custom densification block (1888-1920) even densifies the `-dz` (down)
hops (`-dz > MAX_SMOOTH_PATH_SEGMENT_Z_DELTA`), amplifying it. The raw `findStraightPath` corners
are not densified, so they never hop → monotonic.

**Recommended fix (native, on the smoothPath OUTPUT — this is the correct surface; bake is fine,
do NOT band-aid in the managed consumer):**
1. **First instrument** (native, behind a diag flag) the smoothPath loop to log per-step
   `iterPos`, `steerPos`, `result`, `getPolyHeight` Z, and the chosen poly at the deck-lip, to
   CONFIRM layer-hop vs step-overshoot before changing the algorithm.
2. Then EITHER (a) add a forward-progress / monotonic-Z guard so a smooth corner that regresses
   toward the path start (or drops Z while the segment is net-climbing toward a higher target on
   the spiral) is rejected/clamped to the surface of the climbing layer — narrowly scoped to the
   micro-oscillation signature so genuine descents are unaffected; OR (b) reduce/curve-adapt the
   step size on tight-curvature corridors. Validate with the probe (smooth route DOWN-segment
   count must drop to ~raw's) THEN the live DeckLip test (bot must climb past z48 onto the deck to
   Frezza, 2D≤5) + R16 screenshots.

**Route-validity check (portable, add to the probe workflow):** a route can be
`Walk/StepUp/Clear` on EVERY segment yet still be un-followable if the SEQUENCE is non-monotonic
(oscillates backward/down). Always check route VALIDITY (count DOWN segments / net-progress
monotonicity), not just per-segment affordance. Compare `--detour-resolve` (raw) vs
`--detour-resolve --smooth`: if smooth has many DOWN segments where raw is monotonic, the
defect is in `smoothPath` densification, not the bake or physics.

## UPDATE 5 (2026-06-02) — deck-lip smoothPath OSCILLATION FIXED (native, live-confirmed); failure MOVED to a slide-back after the walk-leg completes at z47.9

UPDATE 4's diagnosis was confirmed AND fixed. The native `findSmoothPath` oscillation is gone
(probe + live), and the live failure has moved to a distinct, deeper layer.

### Root cause (confirmed via native per-iteration instrumentation)
Instrumenting the `findSmoothPath` smoothing loop (gated `WWOW_SMOOTH_DIAG`, since removed) showed
the walk falls into a **period-2 limit cycle** at the deck-lip: `moveAlongSurface`'s fixed 2.0y step
bounces `iterPos` between two mutually-reachable corridor anchors A (z47, poly 146630) and B (z46,
poly 1470xx) while `fixupCorridor` REWINDS the corridor (remaining poly count `npolys` oscillates
66<->72), and the per-iteration store emits the A<->B ping-pong (z47<->z46) the bot stalls on. It is
NOT a step-overshoot you can cap (UPDATE 4's first instinct): capping the step to land on the steer
corner STARVES corridor consumption (the 2.0y step is load-bearing for draining `npolys`->0) and spins
to a 4095-corner truncation that drifts DOWN to z30. The walk genuinely oscillates; only the EMITTED
waypoints must change.

### Fix (native, on the smoothPath OUTPUT — the correct surface; bake/physics untouched)
`Exports/Navigation/PathFinder.cpp::findSmoothPath` — an EMISSION guard with retroactive cycle
suppression. The WALK (step size, `moveAlongSurface`, `fixupCorridor`, `getPolyHeight`) is BYTE-
UNCHANGED, so corridor drain + loop termination are preserved (cannot spin). Per iteration we compute
the remaining corridor-arc distance to target (`remLen`, one O(N) `findStraightPath`); a non-progress
corner (remLen not a new minimum, beyond `SMOOTH_PATH_SLOP`) is BUFFERED, not emitted. When progress
resumes, a SHORT buffered run (<= `SUPPRESS_RUN_MAX`=6) is a benign path bend (corridor re-sync) and is
FLUSHED so its chord stays walkable; a LONG run is the deck-lip limit cycle and is DISCARDED so the
oscillation collapses. Duration cleanly separates them (benign bends <=3 iters, the cycle ~12); their
remLen magnitudes overlap, so magnitude alone cannot. A hard iteration cap (`maxSmoothPathSize`)
truncates+replans a non-escaping cycle instead of spinning. (A naive global remLen-min guard WITHOUT
the retroactive buffer over-pruned benign BASE bends and cut a bend into a wall = 1 Blocked chord at
z24 — the buffer is what keeps healthy bends.)

### Validation (probe + live + regression guard)
- Probe `Grunt#1->Frezza --detour-resolve --smooth`: oscillation collapsed from 16 DOWN / 5.66y backward
  to **4 DOWN / 1.05y** (the residual is legitimate sub-0.3y base micro-terrain + the final Frezza
  descent), **0 Blocked**, reaches Frezza (z53.63). RAW (findStraightPath, same corridor) stays 56 segs /
  DOWN=1. Regression routes unaffected: the guard does NOTHING on the flight-master descent / full-climb
  (0 suppressions — every corner emits; byte-identical to baseline), and their z32 end is the pre-existing
  Detour partial-corridor terminus, not this change.
- Live `DeckLipClimbFromGruntToLiteralFrezza`: the bot now climbs **monotonically z24 -> z47.9** through the
  whole spiral with NO oscillation (the `[TRAVEL_WALK_NAV]` waypoint windows are monotonic z45->48 at the
  deck-lip, vs the old z47<->46 ping-pong). R16 screenshots confirm the Tauren on the upper wooden ramp.
- Regression guard committed: `Tests/PathfindingService.Tests/DeckLipRawPathContractTests.cs ->
  CalculateRawPath_DeckLipGruntBaseToLiteralFrezza_SmoothRouteDoesNotOscillate` asserts the SMOOTH route
  has <=8 DOWN segments and <=3y backward climb (pre-fix bug = 16 / 5.66y); passes at 4 DOWN / 1.05y.

### TRUE NEXT BLOCKER (new, distinct — the failure MOVED): slide-back after the walk-leg completes at z47.9
The live test is still RED, but for a DIFFERENT reason. After the clean monotonic climb to z47.9 the walk
leg completes (`[TRAVEL_LEG] complete reason=walk_arrived dist=14.3 dz=5.9 radius=15.0`), the bot replans
the final 30-corner stretch to Frezza, and IN THE REPLAN GAP it **slides back z47.7 -> z41.4** down the
steep deck-lip ramp and then **cannot re-climb** from z41 (`FG physics rejects forward movement,
currentSpeed=0.00, flags=0x1`, creep-window 15s -> stuck-guard fail). So the smooth oscillation (UPDATE 4)
is no longer what blocks the route; the blocker is now the steep deck-lip ramp + the leg-completion
micro-gap — i.e. the **static-vs-swept physics divergence UPDATE 3 isolated** (the static 8-sample probe
says StepUp/walkable, but the live continuous swept capsule cannot HOLD/sustain the steep climb when
forward intent pauses, and re-acquiring it from z41 yields currentSpeed=0). This is the deep physics
core, a different surface from the smooth path.

Next (do NOT band-aid managed arrival — R13/freeze): instrument the per-tick FG movement on the
z41->z53.6 deck-lip ramp (forward/StepUp intent vs actual Z delta vs slide-back, and why re-climb from
z41 yields currentSpeed=0), and fix the slope/step-up sustain in `Exports/Navigation/PhysicsEngine.cpp`
(or the FG movement-emission). Candidate trigger to examine: the `walk_arrived radius=15.0` leg-completion
fires ~14y short of Frezza and drops forward drive on the steep ramp — but the fix surface is the physics
(hold/sustain the climb and re-acquire from z41), NOT the managed arrival radius. Validate with the
(honest) live DeckLip test + R16 screenshots.

## UPDATE 6 (2026-06-02) — slide-back LOCALIZED to NATIVE physics re-grounding on the overlapping spiral (drive-gap support loss); not managed, not the swept climb itself

Iteration-B localization of the UPDATE-5 slide-back, cross-checked by `codex:rescue` reading the native
physics. The defect is in `Exports/Navigation/PhysicsEngine.cpp` (the FG swept engine `StepV2` /
`CollisionStepWoW`), NOT in the managed `MovementController.cs` and NOT in the static classifier.

### Evidence
- Static probe (native FindPath + 8-sample classifier, same DLL) FROM the live stuck point z41 AND from
  the peak z47.7 to Frezza: **0 Blocked, all Walk/StepUp, reaches z53.63** — the static classifier says the
  ramp is fully climbable.
- Live stuck state: `[NAV_EXEC] physics-read exit hitWall=False blocked=1.00 normal=(0.00,0.00)` repeating
  ~180ms with forward intent set (`flags=0x1`), currentSpeed=0. Per `PhysicsEngine.h:313` `wallBlockedFraction`
  DEFAULTS to 1.0, so `blocked=1.00 / no wall / no normal` means **"zero forward progress without an accepted
  wall contact"** — a SEMANTIC stall, not a geometric wall.
- The swept capsule DID climb z24 -> z47.9 under continuous drive, so the swept physics CAN climb the ramp.

### Root (codex diagnosis, code-verified)
**Drive-gap support loss on the overlapping spiral.** When the leg false-completes at z47.9 and forward drive
drops, the **grounded-IDLE branch (`PhysicsEngine.cpp:5988-5998`) does a raw `SceneQuery::GetGroundZ` at the
current XY** and snaps `st.z` to it within a `STEP_HEIGHT(2.125)+STEP_DOWN_HEIGHT` window. The OG tower is a
vertical spiral — the SAME XY stacks z24/28/37/47/53 (confirmed by `--dump-poly-stack`: ~64 polys / ≥7 Z-wraps
at one XY) — so the idle re-grounding can drop the bot to a LOWER wrap. From the lower layer the forward
`CollisionStepWoW` re-enters from a corrupted support/Z state and stalls (blocked=1.00). The `_stepUpBaseZ/
_stepUpAge` state machine is a RED HERRING — `StepV2` outputs `stepUpBaseZ=INVALID / stepUpAge=0` every tick
(`PhysicsEngine.cpp:~6667`) and `ApplyPhysicsResult` does not persist it.

### Fix surface (native physics; NOT arrival — R13/freeze)
Add **ground-stick on walkable slopes**: in the `StepV2` idle / no-horizontal-input path, prefer the bot's
CURRENT/previous walkable support (a walkable-ground query near `prevGroundZ`, or AABB contact selection) over
a raw center `GetGroundZ` that drops to a lower spiral layer; and guard `CollisionStepWoW`'s pre-sweep ground
snap so it does not snap down from a valid walkable support before the first forward frame. Do NOT change
`radius=15`, `STEP_HEIGHT`, or slope thresholds — match the real client (players don't slide down walkable
slopes and can step up from rest). The live proof that the swept CAN climb when it stays on the correct
support means: preserve the support across the drive-gap and the climb should complete.

### Iteration-C next step (BEFORE editing this load-bearing engine)
Confirm the EXACT slide mechanism with a live per-tick physics trace (mmo-movement-diagnostics): instrument
the `StepV2` grounded-idle branch + the grounding decision + `CollisionStepWoW` to log per-tick
(grounded/airborne, idleGroundZ chosen vs prevGroundZ, st.z, blockedFraction) during the deck-lip slide +
stuck. Disambiguate **idle-snap-down-the-staircase** (idle branch re-grounds to a lower wrap) vs a
**FALLINGFAR fall** (the steep deck-lip filtered as non-walkable -> airborne -> falls to z41). Then implement
the targeted ground-stick fix, re-probe, and run the live DeckLip test + READ the success screenshot (R16).

## UPDATE 7 (2026-06-02) — iteration-C diagnostic-access map; slide-back is the deep physics core (multi-cycle); decisive next-step recipe

The slide-back native root (UPDATE 6) holds. Iteration C scoped HOW to get the confirming per-tick trace
before editing the load-bearing engine, and found every trace path needs an FG-bot instrument+rebuild+live
cycle (the bitness/host-topology must be pinned first). This is genuinely the deep physics core UPDATE 3
isolated — a multi-cycle RE effort, distinct from the now-fixed smooth-oscillation mission.

### Diagnostic-access findings (so the next session doesn't re-derive them)
- The native physics logger exists (`Exports/Navigation/VMapLog.*`: `PHYS_INFO(PHYS_MOVE,...)`, env-gated via
  `VMAP_PHYS_LOG_LEVEL` / `VMAP_PHYS_LOG_MASK`, sink = `std::cout`). The per-tick `[StepV2] OutputSummary`
  (`PhysicsEngine.cpp:6788-6796`) logs `pos / flags / groundZ`, and `[Intent]` (`:5734-5744`) logs input. BUT
  the FG bot's native `std::cout` is NOT captured in the live-test output (0 `[PHYS]` lines; only
  `[StateManager-OUT]` / `[FG:CHAT]`). => enabling it is useless without a FILE sink (native rebuild).
- The managed `MovementController` already captures a full per-tick `PhysicsFrameRecord`
  (`MovementController.cs:1519-1573`: `PosZ, RawPosZ, PhysicsGroundZ, PrevGroundZ, IsFalling, MovementFlags,
  BlockedFraction, ZDeltaFromPrev, steerZ`) — but only when `IsRecording=true` (opt-in, default off) and it is
  never dumped; the records live in the FG-bot PROCESS, unreachable from the test process. => needs an
  env-gated file-dump added to the FG bot (managed rebuild of the FG runner) — lighter than native (no
  bitness), and is the RECOMMENDED trace path.
- Bitness: `WoWSharpClient` (hosts `MovementController` + `NativeLocalPhysics`) is AnyCPU and loads
  `Navigation.dll` at the FG-bot HOST bitness. The FG bot uses LOCAL physics (`[NAV_PATH] local-physics`).
  Confirm the FG-runner exe bitness before any NATIVE physics rebuild; build BOTH x64 and x86 to be safe
  (x64 first, then x86 — shared intermediates corrupt if parallel).

### Confirmed root + fix surface (carry-over from UPDATE 6, codex-verified)
Drive-gap support loss: when the leg false-completes at z47.9 and forward drive drops, the grounded-idle
branch (`PhysicsEngine.cpp:5984-5998`) does a raw `SceneQuery::GetGroundZ` and either snaps `st.z` down a wrap
or fails the `[st.z-STEP_DOWN_HEIGHT, st.z+STEP_HEIGHT]` acceptance and drops grounding -> the bot leaves the
z47 ramp for a lower spiral wrap (slide to z41), then forward `CollisionStepWoW` stalls from the corrupted
support (`blocked=1.00 / no wall / no normal` = native zero-progress default). Static classifier says the ramp
from z41 AND z47.7 is 0-Blocked/StepUp/climbable, so the swept side is the defect. Fix = native ground-stick:
idle branch prefers the bot's CURRENT walkable support anchored near `prevGroundZ` (don't pick a lower wrap),
and guard `CollisionStepWoW`'s pre-sweep snap. NOT the managed arrival radius; do NOT loosen STEP_HEIGHT/slope.

### Decisive next-step recipe (iteration D)
1. Add an env-gated per-tick file dump to the FG bot: when `WWOW_PHYS_TRACE_FILE` is set, append the
   `PhysicsFrameRecord` fields (gameTime, pos.xyz, rawZ, groundZ, prevGroundZ, flags, falling, blocked, steerZ)
   to that file from `MovementController.CapturePhysicsFrameRecord` (before the `IsRecording` early-return).
   Rebuild the FG runner (managed). Run the live DeckLip test with the env var; READ the trace at the z47->z41
   slide window to CONFIRM idle-snap-down vs FALLINGFAR-fall and the exact `groundZ` the idle query returns.
2. Implement the native ground-stick fix per the confirmed mechanism; rebuild x64 (+x86); re-probe other OG
   routes (load-bearing); run the live DeckLip test to GREEN (bot ON the deck at Frezza, 2D<=6, z~53.6) and
   READ the success screenshot (R16). Remove the trace instrumentation (R12). Commit + UPDATE 8.

### Status framing for the owner
The loop's EXPLICIT mission — the UPDATE-4 smoothPath oscillation — is FIXED + committed (6ec1355f). The
slide-back is a separate, deeper FG-swept-physics layer (UPDATE 3's "static-vs-swept divergence"), now cleanly
localized to `PhysicsEngine.cpp` with a known fix surface. It is a multi-cycle effort (instrument FG -> confirm
-> native fix -> broad-movement regression validation), best run as a focused physics session rather than
fast loop ticks. The handoff above is turnkey for that session.

## UPDATE 8 (2026-06-02) — DIRECTION PIVOT (owner): navmesh-first. Removed the physics-wall-feedback crutch; the real deck-lip fix is the Recast bake (proper LAYERS partitioning), NOT physics/smoothPath band-aids

The owner reset the approach: the FG bot drives the ORIGINAL WoW.exe client (control-bit writes -> WoW.exe's
own physics), so the FG runner needs NO simulated-physics fix. Parity: "if FG can't run up it, BG can't
either" — and any `PhysicsEngine.cpp` change MUST be grounded in decompiled WoW.exe, never invented. The goal:
**use Recast properly so the mmaps are accurate and Detour always produces valid paths the bot just follows.**

### Diagnosis correction (why the prior physics chase was the wrong layer)
The live `[NAV_EXEC] physics-read blocked=1.00 normal=(0,0)` was NEVER real physics — the FG ObjectManager
never overrode `PhysicsHitWall/PhysicsBlockedFraction/PhysicsWallNormal2D`, so the nav read the
`IObjectManager` DEFAULTS (`false / 1.0f / (0,0)`) every tick (IObjectManager.cs:193-195). The whole run-time
wall-feedback/avoidance subsystem was a crutch over an inaccurate navmesh.

### DONE + committed `6fa84c4a` (pushed): removed the run-time crutch (R18 full removal)
Excised the chain native swept output -> `MovementController.Last{HitWall,BlockedFraction,WallNormal}` ->
`IObjectManager.Physics*` props -> `NavigationPath.GetNextWaypoint` wall-avoidance (deflection + wall-stuck
repath + avoidance-waypoint); deleted `PhysicsStateHelper`, the `[NAV_EXEC] physics-read` diag, the
`PhysicsFrameRecord` wall fields + DiagnosticsRecorder columns, and the wall-specific tests. Core Detour
path-following (next-waypoint, advance-on-reach, recalc-on-exhaust, fallback, transport-hold, non-wall
stalled-near-waypoint) preserved. Build green; NavigationPath/TravelTask tests 164/0; 3-lens adversarial review
clean. KEPT (scaffolding, remove after navmesh is accurate): the plan-time corridor-walkability validation
(`PathfindingClient.IsBlockingWallContact`, strict `Compatible=!hitWall` at PathfindingClient.cs:400; server
`Navigation.cs:4113`) + native `PhysicsOutput` fields — same KIND of physics-wall compensation but plan-time;
it currently guards against committing to a mesh-walkable-but-geometry-blocked corridor, i.e. it masks navmesh
inaccuracy, so it becomes redundant once the bake is accurate.

### The REAL deck-lip fix = the Recast bake (root-caused, plan ready)
The OG tower is a vertical spiral: the same XY stacks z24/28/37/47/53. `--dump-poly-stack` at (1347.3,-4650.6)
shows ~64 polys / >=7 Z-wraps SURVIVING — Recast's single-layer heightfield (watershed partition + contour
simplification) BRIDGES the stacked surfaces into one merged mesh, so Detour string-pulls/smooths across
non-traversable wraps. `tools/MmapGen/config.json` tile `"4029"` is a ~190-line GRAVEYARD of per-coordinate
"cull" band-aids (`postDetourCullShadowedLedges/Pockets`, `preRegionCull*`, `postDetourCullAnchorPolyStacks`
with 13 hardcoded coords, `anchorStageManifestCoordsWow` with ~35 deck-lip coords, `anchorRouteTargetsWow`)
trying to delete the overlap after the fact — its own notes record the stack "UNCHANGED at 63 phantoms"
(`_4029_NEGATIVE_RESULT_loop24_A3`). It also lowered `agentMaxClimbTerrain` to 0.2 (vs harvested 1.8), which
OVER-fragments the legit ramp while doing NOTHING about the overlap (overlap is not a step-height problem;
`_4029_README_climb` records restoring 1.8 regressed the ramp — because it was relied on as a band-aid).

**Proper-Recast fix (VERIFIED config-only — no native rebuild): per-tile `partitionType:"layers"`.** Recast's
`rcBuildLayerRegions` (TileWorker.cpp:11507-11509, fully plumbed; `partitionType` read at :10156) is the
purpose-built tool for vertically-stacked surfaces — each spiral wrap becomes its own layer/region, same-XY
wraps STOP merging. The prior `layers` attempt failed only because it was coupled with `maxSimplificationError`
+ `maxVertsPerPoly` churn (`_4029_NEGATIVE_RESULT_partition_layers_simplify13`); it must be retried CLEAN.
Optional cheap adjunct: ensure `RC_CONTOUR_TESS_WALL_EDGES` on this tile (verify the vendored
`rcBuildContours` default at :11549 — stock Recast defaults it ON; the prior plan's "omitted" claim is
unverified).

### Turnkey recipe (iteration: navmesh accuracy)
1. Rewrite the `"4029"` block CLEAN: `cs:0.1, tileSize:213, partitionType:"layers"` + DELETE the
   `agentMaxClimbTerrain/agentMaxClimbModelTerrainTransition` overrides (back to harvested 1.8/1.2) + RETIRE the
   `postDetourCull*`/`preRegionCull*`/anchor-coord band-aids. (Keep a backup; expect to retire culls
   INCREMENTALLY, re-running `probe-routes.ps1` after each, since some also target OG city/hallway dead-ends.)
2. `tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant og-layers-clean -DataDir D:\wwow-bot\test-data`
   (bake-report.json: exit 0, afterLen non-null for 0012940).
3. PRIMARY GATE (probe oracle, `WWOW_DATA_DIR=D:\wwow-bot\test-data`): RAW `Grunt1->Frezza --detour-resolve`
   stays valid + monotonic (<=1 DOWN); `--smooth` reaches Frezza z53.63 with 0 Blocked and NO densification
   needed; `--dump-poly-stack` at (1347.3,-4650.6) collapses from ~64 polys/7 wraps to the local wrap.
4. NO-REGRESSION: `tools\scripts\probe-routes.ps1 -RouteManifest tools\scripts\routes\og-zeppelin.json`
   (FlightMasterDescentControl + OgFrezzaToBoardingPosition green; ClimbOrgrimmarTowerToFrezza no worse).
   Unit baseline `run-pathfinding-tests.ps1 -TestSet unit` (213+7) + `DeckLipRawPathContractTests`.
5. EXPECT 4029 poly-ID churn (layers reorders every polyRef) -> rebaseline any 4029-specific poly-ref tests
   (`_3446_NEGATIVE_RESULT_surface_F/G` is the ID-churn precedent). Re-probe the 40,29<->40,28 tile seam
   (full ClimbOrgrimmarTowerToFrezza) for cross-tile linking (4029 cs=0.1 vs neighbor default cs).
6. Promote: `tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "40,29"`; `docker restart wwow-pathfinding
   wwow-scene-data`. Then re-run the live `DeckLipClimbFromGruntToLiteralFrezza` (FG) + READ the screenshot (R16).
7. AFTER the navmesh is accurate: remove the plan-time corridor-validation remnant (PathfindingClient.cs:400
   `Compatible=!hitWall` first, then the server `hit_wall` reachability gating) — it is now masking nothing.

### Honesty note
The live DeckLip test's UPDATE 6-7 slide-back is a DOWNSTREAM consumer of the bad mesh (the idle re-grounding
snaps onto the overlapping wraps). The layered bake removes that surface, so it is complementary — a fully-green
FG live test MIGHT still need a `PhysicsEngine.cpp` ground-stick fix, but per the parity rule that change must
come from decompiled WoW.exe, and the bake-accuracy is the necessary, correct, band-aid-free surface to fix FIRST.
