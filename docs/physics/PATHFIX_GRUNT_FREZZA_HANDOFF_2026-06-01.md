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
