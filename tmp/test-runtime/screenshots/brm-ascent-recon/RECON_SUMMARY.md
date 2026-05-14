# BRM Ascent FG-Rendering Reconnaissance

> Generated 2026-05-13 (UTC) by `BrmAscentReconTests.BrmAscentRecon_TeleportAndCaptureMultiAngle`
> + `BrmAscentReconPolyrefDump.DumpReconPolyrefs`. Source coords and provenance
> in `recon-manifest.json`; per-coord bake state in `recon-polyrefs.json`;
> per-coord settle JSON beside each coord's PNGs.

This file is the canonical reconnaissance artifact for the FlameCrest →
BRM dungeon-entrance route. Authored after four reverted bake/runtime
attempts whose pattern was "bench-green, live-regress" — the goal here is
to invert the iteration loop so the FG client's actual rendering, not the
bake's claims about it, is the goalpost.

It is the input for the property tests in
`Tests/PathfindingService.Tests/WaypointGeneration/BrmAscentRenderingExpectations.cs`.

## Methodology

For each of 11 seeded coords (drawn from the four reverted attempts'
stall sites + `BrmDungeonRouteDiagnostic.Audit_BrmDungeonEndpoints_ResolveAndCorridor`
endpoint table), the recon:

1. Teleported the FG bot via `_bot.BotTeleportAsync(account=LPATHFG1, mapId=0, X, Y, Z)`.
2. Waited 2s for the client to settle.
3. Drove four cardinal yaws (0, π/2, π, 3π/2) via `.go xyzo X Y Z O 0`.
4. Captured the WoW client window via `Tests.Infrastructure.WindowCapture`
   (PrintWindow + PW_RENDERFULLCONTENT, focus-safe).
5. Wrote a paired JSON snapshot.

In parallel, the x64 sidecar `BrmAscentReconPolyrefDump` queried each
coord against `Navigation.dll`'s `GetPolyAtCoord` and `GetPolyFlagsForRef`
exports, against `D:\wwow-bot\test-data` (the same bake `D:\MaNGOS\data`
contains for the BRM-area tiles per memory `reference_wwow_data_dir`).

### Capture-side caveat (resolved)

The first recon attempt landed 0/44 PNGs. Root cause: the StateManager
log only emits ONE `WoW.exe started for account ...` line per launch but
auto-restarts WoW.exe on crash without re-emitting it, so the resolver
returned a stale PID. Fixed in
`BrmAscentReconTests.ResolveLiveWoWClientPid` — fall back to
`Process.GetProcessesByName("WoW")` and pick whichever WoW.exe has a
top-level visible window. Second attempt landed 44/44 PNGs.

The `fc_start` capture is degraded — the fallback grabbed a WoW.exe
that was sitting at the LoginScreen (likely a stranded non-test
WoW.exe instance on the desktop). All other 10 coords captured the
test's actual FG bot (Tauren Male LPATHFG1 InWorld at the requested
coord).

## Per-coord findings

Bake column: `Ground` = `flags=0x0001` `area=1`; `Steep` = `flags=0x0011`
`area=3` (`NAV_STEEP_SLOPES`); `dz` = |requestedZ − bake surface Z|.

| # | Coord | Bake | dz | Rendering observation | Verdict |
|---|---|---|---|---|---|
| 1 | `fc_start` (-7518.7,-2159.9,131.9) | Ground | 0.33y | LOGIN SCREEN (capture grabbed wrong WoW.exe). Other coords prove the bot logs in fine — this is a one-off recon-side artifact, not a bake claim. | INCONCLUSIVE — rendering not captured. Bake-only data: looks healthy. |
| 2 | `fc_stall` (-7519.0,-2100.4,130.3) | Ground | 0.07y | Bot wedged in dark rock geometry. yaw0/yaw90: black rock face filling frame. yaw180: orange Searing Gorge sky south, with vertical black rock wall on right. yaw270: river of lava flowing past. | **BAKE-WORLD MISMATCH.** Bake says walkable Ground at z=130.37 with no slope flag; rendering shows the coord sits inside a rock outcrop with lava on one flank. The FlameCrest → BRM corridor must NOT pass through this XY/Z. |
| 3 | `ruins_wall` (-7665,-1808,137) | Ground | 0.44y | Open desert terrain extending in all directions; bot stands on flat ground; no walls in immediate vicinity. | **WALKABLE OK.** Round-2's "wall-creep" hypothesis was wrong about this exact coord — the failure was movement controller wedged on a wall some yards away, not this coord itself. |
| 4 | `brm_south_lo` (-7949.7,-1162.8,170.8) | Ground | 0.44y | Bot on a high ridge near BRM south face; orange Searing Gorge desert visible south; rock face north (out of frame). | **WALKABLE OK.** Bot stands on a real ridge. The historical `(-7949.7,-1162.8,170.8)` UBRS-Round-2 stall site is geometrically standable; the failure was elsewhere on the ascent. |
| 5 | `brm_southnew` (-7825.4,-1129.2,133.8) | **Steep** | 1.38y | Bot in a stone corridor / cave entrance; orange torchlight + red BRM-style stone surfaces; no open exterior. | **WALKABLE on slope.** Bake correctly marks STEEP. The Round-3 stuck-recovery coord IS the entrance into BRM cave from the south — accessible, but on a steep ramp. |
| 6 | `brm_mid_lbrs` (-7647.1,-1197.1,225.2) | Ground | 0.54y | Bot INSIDE the BRM central chamber, on the upper bridge over the lava pit. Iron rails visible. yaw180: looking down across the lava chamber. | **WALKABLE OK** but **ROUTING MISMATCH.** This coord is INSIDE BRM at the upper bridge — not en route to the LBRS portal but already past it, geometrically. The smooth-path's `endWP` for FC→LBRS terminating here means the path ends ABOVE the lava chamber, not at the LBRS portal poly. |
| 7 | `brm_mid_bwl` (-7640,-1213.4,228.4) | Ground | 0.47y | Same upper bridge area as `brm_mid_lbrs`; bot on stone floor with lava chamber visible to north. Same poly per polyref data (`0x0001000014F02191`). | **WALKABLE OK** but **ROUTING MISMATCH.** Same as #6. |
| 8 | `ubrs_portal` (-7524,-1233,287) | Ground | 1.17y | Stone arch with green portal effect; "You must be at least level 45 to enter" tooltip — this is the literal UBRS portal. | **WALKABLE OK + PORTAL CONFIRMED.** Bake correctly resolves to a Ground poly at the literal UBRS portal. |
| 9 | `lbrs_portal` (-7531,-1226,286) | Ground | 0.18y | Stone door / portal frame with red glyph; literal LBRS portal entrance. | **WALKABLE OK + PORTAL CONFIRMED.** |
| 10 | `bwl_portal` (-7659,-1214,291) | **Steep** | 1.36y | Bot near "Quartermaster Lewis" NPC; gold-toned BWL hall geometry. | **WALKABLE on slope.** Bake STEEP-flags this coord but the surface is in fact walkable in-game (BWL approach geometry has real slopes the bake correctly classifies — the player physics still tolerates it). |
| 11 | `brd_portal` (-7187,-958,254) | **Steep** | 0.65y | BRD desert/cave-mouth landscape; brown/red stone cliffs; bot on walkable ground at the BRD entrance area. | **WALKABLE on slope.** Same as #10 — bake STEEP, rendering walkable. |

## Cross-coord findings

> **Important correction (post property-test baseline run):** an earlier
> draft of this summary hypothesized that the `fc_stall` polygon
> (`0x0001000015001ECA`) was on the FC→portal corridor and thus the
> headline phantom-poly to cull. The property tests
> `Corridor_FcTo*Portal_DoesNotPassThroughFcStallPoly` and
> `SmoothPath_FcTo*Portal_NoCliffWaypointsNearFcStall` ran GREEN against
> the current bake. fc_stall is NOT in the FC→{UBRS,LBRS,BWL,BRD}
> corridor or smooth path. The rendering still shows fc_stall is
> rock-wedged, but Detour's pathfinding correctly routes around it.
> Keeping the rendering observation (#2) for completeness, but removing
> it from the headline-finding list.

### Headline finding — corridor terminates one poly short of every portal

`Corridor_FcTo*Portal_TerminatesAtPortalPoly` runs RED for UBRS, LBRS,
and BWL on the current bake:

| Route | Corridor polys | Last poly | Portal poly |
|---|---|---|---|
| FC → UBRS | 316 | `0x0001000014F03909` | `0x0001000014F038A3` |
| FC → LBRS | 316 | `0x0001000014F03909` | `0x0001000014F034C5` |
| FC → BWL  | 275 | `0x0001000014F01E31` | `0x0001000014F01EA0` |
| FC → BRD  | (passes — terminus matches portal poly) | | |

All three failing terminus polygons are in the BRM upper portal cluster
tile (`0x14F0` = (47,29) ish). Detour solves a path that gets the bot
inside the cluster but stops on a polygon adjacent to the actual portal
poly. Rendering at `ubrs_portal` and `lbrs_portal` (entries #8 and #9)
confirms the literal portal coords are walkable — the bake has a poly,
but not connected to the corridor terminus.

This points to a small intra-tile connectivity gap inside the BRM upper
portal cluster — a single missing edge between the corridor terminus
poly and the portal poly. Likely fixable with a per-tile bake parameter
adjustment for tile `0x14F0` (probably `cs` finer or
`maxSimplificationError` smaller so the portal-area mesh keeps its
edge precision).

### Headline finding — smooth path has 58 unreasonable Z-jumps, worst is 27y on the BRM south face

`SmoothPath_FcToUbrsPortal_NoUnreasonableZJump` runs RED with 58
WP-to-WP vertical jumps exceeding `walkableClimb=1.8y`. The worst is at
waypoint index 663 (of 1129):

```
WP[662] = (-7945.7, -1289.2, 97.2)
WP[663] = (-7946.8, -1291.7, 124.4)   dz = 27.14y
```

`BrmAscentReconPolyrefDump.DumpZJumpZone` (`recon-jump-zone.json`)
localizes the offending polygon. **Both WP[662] and WP[663] resolve
to the SAME polygon `0x0001000014F002AC` in tile `0x14F` (poly
0x2AC).** A vertical column scan at the midpoint XY (-7946.25,
-1290.45) from z=90 to z=130 in 2y steps shows EVERY probe (z=90,
92, ..., 128) lands on the same polygon, with `surfaceZ=126.35` —
i.e. the polygon's actual walkable top surface is z=126, but it
spans at least 36y vertically (z=90 to z=126).

Polygon flags + area:
- `flagsHex=0x0002` = `NAV_MAGMA` only (NOT `NAV_GROUND`).
- `area=7` = `AREA_MAGMA` (lava).

The runtime filter `PathFinder::createFilter` sets
`includeFlags = NAV_GROUND` only, which SHOULD exclude this MAGMA
polygon from the corridor. But the smooth-path generator puts
WP[662] at z=97 below the polygon's surface — a synthetic in-space
waypoint that lands ABOVE the lava but BELOW any actual walkable
surface in that XY zone. WP[663] then jumps to z=124, near the
polygon's true surface at z=126.

**Live FG failure mode:** the bot's NavigationPath cannot accept a
27y vertical waypoint advance, so it stalls. Even a relaxed
`WAYPOINT_VERTICAL_REACH_TOLERANCE > 1.25y` cannot absorb 27y.
Even if it could, the bot would attempt to walk into a
near-vertical cliff face, which player physics rejects.

The four reverted attempts each tried to address a different symptom
(steep-slope filter, off-mesh ascent), but the underlying cause is
this single 36y-tall polygon producing an in-space waypoint at z=97
during smooth-path interpolation. The fix surface is bake-side
fragmentation of polygon `0x14F002AC` (or the bake-config knob that
prevented its fragmentation in the first place).

### Implication for the Phase 2 fix surface

The recon now points at one specific polygon: `0x14F002AC` in
Detour tile-bits `0x14F` (decimal 335). The (X, Y) tile-coord
mapping for index 335 needs to be derived from the navmesh's
bmin + tileSize at Phase 2 start; the polyRef itself is the
canonical handle that `NavMeshTileEditor` consumes regardless of
the tile-coord decoding.

Candidate fix surfaces, ordered by blast radius:

- **E.** **Targeted PolyRef cull of `0x14F002AC`** using the
  `NavMeshTileEditor` pipeline from
  `project_pfs_overhaul_006_brm_phase4_findings`. Smallest blast
  radius. Disconnects the 36y polygon entirely; Detour will route
  around. Risk: if the polygon is the only walkable surface in that
  XY column, the cull strands the BRM south face — but the recon
  shows `brm_south_lo` (-7949.7, -1162.8, 170.8) is a separate
  polygon at z=171 elsewhere on the ridge, so an alternate corridor
  should exist.
- **C.** **Per-tile `cs` / `ch` / `maxSimplificationError` tightening
  for tile (47, 29)** — Recast bakes finer-grained polygons that
  don't span 36y vertically. Wider blast radius (regenerates the
  whole tile's mesh). Risk: file-size growth, possible new
  cross-tile linking issues with neighbors at different `cs`.
- **D.** **Surface 3 (per-tile `maxSteepSlopePolyZRange` bake
  post-process)** — designed for STEEP polys; less directly
  applicable here because `0x14F002AC` is `area=7` MAGMA, not
  `area=3` STEEP. Could be generalized to a `maxAnyPolyZRange`
  post-process that fragments any polygon (regardless of area)
  whose vertical extent exceeds a threshold. Multi-cycle design
  work per the docs.

Property test `SmoothPath_FcToUbrsPortal_NoUnreasonableZJump` is
the green-or-red gate. `Corridor_*MustReachTarget` is the secondary
gate — likely needs a separate fix in the BRM upper portal cluster
tile.

**Phase 2 — Surface E targeted cull attempted, INCONCLUSIVE → REVERTED.**

Cull executed on `D:\wwow-bot\prod-data\mmaps\0004635.mmtile`:
`NavMeshTileEditor.exe ... --cull-polys 281475329039007` (polyref
`0x0001000015001A9F`, polyIdx 6815 = the prod-data fc_stall polygon
which has area=1 Ground but no detail-mesh surfaceZ — a phantom
walkable poly per the recon's wide search). `wwow-pathfinding`
restarted healthy.

**MaNGOS/data vs prod-data discrepancy discovered during validation.**
The `BrmAscentRenderingExpectations` property tests were authored
against MaNGOS/data polyrefs (recon ran with
`WWOW_DATA_DIR=D:\MaNGOS\data` set on the test process — the env
var supersedes `PathfindingValidationFixture`'s default test-data
preference because Navigation.dll resolves WWOW_DATA_DIR at first
P/Invoke). Live FG runs against prod-data (Docker `wwow-pathfinding`
serves `/data/mmaps` mounted from `D:\wwow-bot\prod-data\mmaps`).

Polyref state at the same XY differs between bakes:
- MaNGOS/data fc_stall: polyRef `0x0001000015001ECA` (the originally
  hypothesized headline poly)
- prod-data fc_stall: polyRef `0x0001000015001A9F`
- MaNGOS/data has the 36y MAGMA polygon `0x0001000014F002AC` at
  WP[663] of FC→UBRS smooth path. prod-data does NOT have a 27y
  jump — its worst smooth-path WP-to-WP dz is 2.61y at idx 259
  (-7673.9,-1746.2,136.0)→(-7679.0,-1747.5,133.4), still > 1.25y
  WAYPOINT_VERTICAL_REACH_TOLERANCE but order-of-magnitude smaller.
- prod-data has its own bugs: ubrs_portal coord
  (-7524,-1233,287) resolves to polyRef `0x0001000014F02CDF`
  (Ground) but with surfaceZ=null (no detail-mesh data). BWL
  corridor terminates one poly short of literal portal poly.

Live FG validation outcome: WoW.exe client CRASHED at
`(1337.3,-4645.1,42.8)` map=1 (Orgrimmar deck-lip area) ~88s
into the UBRS sub-test, BEFORE the bot's path query at FC could
be evaluated. Cascading "no FG target" failures on the other 3
sub-tests because the fixture state was disrupted. The crash
trace shows `[MovementController] Post-teleport ground snap
complete: ... groundZ=-200000.000` (the INVALID sentinel), which
is a known PFS-OVERHAUL Round-3 cliff-fall code path symptom.

**The crash is unrelated to the prod-data cull** (it happened on
map=1, the cull was on map=0). But it prevents observing whether
the Surface E cull would have helped. **Reverted** the prod-data
tile from backup; `wwow-pathfinding` restarted healthy.

Phase 2 candidate surfaces remaining:

- **C (per-tile cs/maxSimplificationError tweak)** — would require
  rebuilding both test-data and prod-data tiles via MmapGen.
- **Surface E retry on a different attempt** — same poly cull but
  the live test needs to clear the cross-map crash hazard first.
  May need EnsureCleanSlateAsync replacement or per-test fresh
  WoW.exe (instead of cross-test reuse).
- **Property test data-source awareness** — the four RED tests
  point at MaNGOS/data polyrefs. To validate Phase 2 against
  prod-data, the tests need either two parallel polyref constants
  (per-dataset) or a dynamic poly-discovery pattern.

The Phase 1 + diagnostic commits remain the foundational handoff;
Phase 2 needs more work before another cull attempt is
fairly evaluable.

### Secondary finding — interior BRM coords are on the route

`brm_mid_lbrs` and `brm_mid_bwl` both resolve to the SAME polygon
(`0x0001000014F02191`, area=1, Ground) and the rendering shows both at
the upper bridge of the BRM central chamber, INSIDE the mountain. This
is the smooth-path `endWP` for FC→LBRS/UBRS/BWL per the audit.

The path going from FC → BRM exterior → INTO the cave → onto the upper
bridge is geometrically valid (the in-game BRM cave is real, walkable,
and connects to the upper portals). The 4/4 live FG failure isn't
because the path is fundamentally wrong shape — it's because somewhere
between FC and the BRM cave entrance, the bot stalls. `fc_stall` is the
most likely site.

### Tertiary finding — three "Steep" coords are walkable in-game

`brm_southnew`, `bwl_portal`, and `brd_portal` all resolve to
`area=3 / NAV_STEEP_SLOPES` polygons. Rendering shows the FG bot
standing fine at all three. Surface 2 (runtime exclude
`NAV_STEEP_SLOPES`) was correct in principle for false-walkable steep
faces, but it must NOT exclude legitimate walkable steep terrain like
these coords. Per-coord rendering evidence supports a more
targeted bake-side fix (Surface 3 — `maxSteepSlopePolyZRange` to
fragment ONLY the tallest steep walls) over the blanket runtime exclude.

## Property tests authored from this recon

See
`Tests/PathfindingService.Tests/WaypointGeneration/BrmAscentRenderingExpectations.cs`.

Each test is rooted in one of the rendering observations above. They
are RED on the current bake — that is intentional. Each one closes
when the bake/runtime fix surface chosen in Phase 2 makes the
real-world geometry match the bake.

| Test | Rendering observation it codifies |
|---|---|
| `Walkable_RuinsWall_HasGroundPoly` | #3 — open desert at (-7665,-1808,137); bake must have Ground poly within walkableClimb. |
| `Walkable_BrmSouthLo_HasGroundPoly` | #4 — ridge surface at (-7949.7,-1162.8,170.8) is walkable; bake must have Ground poly within walkableClimb. |
| `Walkable_AllFourPortals_HaveGroundPoly` | #8/#9/#10/#11 — all four portal coords show walkable terrain. |
| `Corridor_FcToUbrsPortal_TerminatesAtPortalPoly` | #8 — corridor's last poly == nearest poly of UBRS portal coord. |
| `Corridor_FcToLbrsPortal_TerminatesAtPortalPoly` | #9 — same for LBRS. |
| `Corridor_FcToUbrsPortal_DoesNotPassThroughFcStallPoly` | #2 — fc_stall polyRef MUST NOT appear in the corridor (rendering shows rock-wedged). |
| `Corridor_FcToLbrsPortal_DoesNotPassThroughFcStallPoly` | #2 — same for LBRS. |
| `SmoothPath_FcToUbrsPortal_NoUnreasonableZJump` | implied by skill spec — every smooth-path WP-to-WP dz ≤ walkableClimb (1.8y). |
| `SmoothPath_FcToUbrsPortal_NoCliffWaypointsNearFcStall` | #2 — no smooth-path waypoint may land within 5y XY of fc_stall at z within ±5y. |

The four-portal `Corridor_MustReachTarget` tests are the headline
gates. The fc_stall avoidance tests are the diagnostic tests that
should turn green as a side effect of the corridor fix.

## Phase 2 direction implied by this recon

Reverted attempts:

- Surface 1 (terrain-only-52 walkableSlopeAngle): destroys legitimate
  thin model footing. **Not viable per #10/#11.**
- Surface 2 (runtime exclude NAV_STEEP_SLOPES): explodes A* to
  170-306s/query AND excludes legitimate walkable steep terrain.
  **Not viable per #10/#11 + perf evidence.**
- Surface 3 (per-tile maxSteepSlopePolyZRange bake post-process):
  never tried. Now the strongest candidate per the 27y Z-jump finding.
- Surface 4 (off-mesh ascent): managed pipeline isn't off-mesh-aware,
  and rendering shows the BRM cave IS walkable continuous geometry —
  no discrete edge that justifies an off-mesh entry.

The recon now points at **Surface 3 or its targeted-cull cousin
(Surface E above)** as the next iteration. The fault is at the BRM
south-face cliff transition, not at fc_stall (the earlier draft of
this summary was wrong about that — the property tests proved it).

Property tests as Phase 2 gates:

- `SmoothPath_FcToUbrsPortal_NoUnreasonableZJump` — must go from 58
  violations to 0 (the headline gate).
- `Corridor_FcTo{Ubrs,Lbrs,Bwl}Portal_TerminatesAtPortalPoly` — must
  flip from RED to green; needs intra-tile connectivity fix in the BRM
  upper portal cluster tile.

Walkable-baseline gates (already green) that must STAY green:

- `Walkable_RuinsWall_HasGroundPoly`
- `Walkable_BrmSouthLo_HasGroundPoly`
- `Walkable_AllFourPortals_HaveGroundPoly`

## Open questions

- The 27y Z-jump at smooth-path WP[663] is at XY (-7945,-1290), about
  130y SW of `brm_south_lo`'s sample at (-7949,-1162). A focused
  rendering recon at this exact XY (with z probes at 90, 100, 110, 120,
  130) would localize whether the cliff face has a real walkable
  ledge between z=97 and z=124 or is genuinely vertical. Next-cycle
  work; not blocking Phase 2.
- Tile address for the cliff-bridging polys: the corridor for FC→UBRS
  passes through tiles in the BRM south-face area before reaching the
  upper portal cluster (`0x14F0`). The exact tile of the WP[663]
  jump needs identification via `(polyRef >> 20) & 0xFFFFFFF` on the
  corridor poly at that index.
- The intra-tile connectivity gap in the BRM upper portal cluster
  (`Corridor_*MustReachTarget` failures) might be closed by a higher-
  fidelity `cs` for that tile. But a finer cs may also worsen the
  cliff string-pull. Phase 2 will need to balance.
- The `fc_start` rendering capture is degraded by the multi-WoW.exe
  artifact; future recon runs should kill non-test WoW.exe before
  starting OR run on a dedicated headless desktop session.

## Re-run recipe

```powershell
Push-Location 'E:\repos\Westworld of Warcraft'
$env:WWOW_BRM_ASCENT_RECON='1'
$env:WWOW_DATA_DIR='D:\MaNGOS\data'

# x64 sidecar (fast, ~10s) — refreshes recon-polyrefs.json
dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj `
  --configuration Release --no-build -m:1 -p:UseSharedCompilation=false `
  --filter "FullyQualifiedName~BrmAscentReconPolyrefDump" `
  --logger "console;verbosity=minimal"

# x86 FG recon (~3-4 min including LongPathingFixture init)
$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'
dotnet test Tests\BotRunner.Tests\BotRunner.Tests.csproj `
  --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false `
  --filter "FullyQualifiedName~BrmAscentReconTests" `
  --logger "console;verbosity=normal" `
  -- RunConfiguration.TestSessionTimeout=1800000
Pop-Location
```

After re-running, the existing PNGs and JSONs are overwritten in
`tmp/test-runtime/screenshots/brm-ascent-recon/`. Re-read the
screenshots and re-author this summary if observations change.

## 2026-05-14 — Phase 2 retry-prep + prod-data baseline (commit ad288945)

Three minimum-scope fixes landed to unblock fair Phase 2 evaluation.
None of them is a pathfinding fix; they remove obstacles that
prevented the 2026-05-14 Surface-E retry attempt (commit 125e6928)
from being fairly evaluated.

1. **Crash mitigation** in `FlameCrestToBrmDungeonEntrance`: skip the
   map=1 OG SafeZone teleport via `EnsureCleanSlateAsync(...,
   teleportToSafeZone:false)`. The Surface-E retry crashed WoW.exe
   inside that hop with the PFS-OVERHAUL Round-3 INVALID-groundZ
   sentinel (-200000.0) at (1337.3,-4645.1,42.8) — the OG deck-lip
   cliff-fall coord — whenever the bot's prior position (server-side
   saved logout) sat in that area. The next call teleports directly
   to FlameCrest on map=0, so the SafeZone hop was dead weight.

2. **`PathfindingValidationFixture` honors pre-set `WWOW_DATA_DIR`**:
   the previous `ResolveDataDir`/`ConfigureProcessDataDir` pair
   unconditionally overwrote whatever the caller set with `test-data`,
   so the documented recipe (`WWOW_DATA_DIR=prod-data dotnet test
   ...BrmAscentRenderingExpectations`) was a silent no-op — the
   test process loaded test-data tiles regardless. Fixed to honor
   a pre-set valid `WWOW_DATA_DIR` after the explicit
   `WWOW_VALIDATION_DATA_DIR` override.

3. **`BrmAscentRenderingExpectations` dynamic `FcStallPolyRef`
   discovery**: replaced the hardcoded `0x0001000015001ECA` (MaNGOS/
   data-only) with a `QueryPolyAtCoord` lookup against the loaded
   bake. Same XY resolves to a different polyref per bake.

### Prod-data baseline (after the three fixes)

`WWOW_DATA_DIR=D:\wwow-bot\prod-data dotnet test ...
BrmAscentRenderingExpectations` — log line confirms
`[Navigation] Loading map 0 tiles from: D:\wwow-bot\prod-data\mmaps\`,
so the fixture truly loaded prod-data this time.

| Gate | MaNGOS/data | prod-data |
|---|---|---|
| Walkable_RuinsWall_HasGroundPoly | ✅ | ✅ |
| Walkable_BrmSouthLo_HasGroundPoly | ✅ | ✅ |
| Walkable_AllFourPortals_HaveGroundPoly | ✅ | ❌ ubrs_portal Ground but `surfaceZ=null` |
| Corridor_FcToUbrsPortal_TerminatesAtPortalPoly | ❌ | ✅ **flipped GREEN** |
| Corridor_FcToLbrsPortal_TerminatesAtPortalPoly | ❌ | ✅ **flipped GREEN** |
| Corridor_FcToBrdPortal_TerminatesAtPortalPoly | ✅ | ✅ |
| Corridor_FcToBwlPortal_TerminatesAtPortalPoly | ❌ | ❌ corridor 1 poly short (`0x14F01877` vs portal `0x14F01B1B`) |
| Corridor_FcToUbrsPortal_DoesNotPassThroughFcStallPoly | ✅ | ✅ (resolved 0x...15001A9F) |
| Corridor_FcToLbrsPortal_DoesNotPassThroughFcStallPoly | ✅ | ✅ |
| Corridor_FcToBrdPortal_DoesNotPassThroughFcStallPoly | ✅ | ✅ |
| Corridor_FcToBwlPortal_DoesNotPassThroughFcStallPoly | ✅ | ✅ |
| SmoothPath_FcToUbrsPortal_NoUnreasonableZJump | ❌ 58 violations, worst 27.14y at idx 663 | ❌ **only 4 violations**, worst **2.61y** at idx 259 (-7676,-1747,135) |
| SmoothPath_FcToUbrsPortal_NoCliffWaypointsNearFcStall | ✅ | ✅ |
| SmoothPath_FcToLbrsPortal_NoCliffWaypointsNearFcStall | ✅ | ✅ |
| **Total** | **10/14** | **11/14** |

Prod-data lands 11/14 vs MaNGOS/data's 10/14, with two of MaNGOS/data's
RED gates (UBRS and LBRS portal-terminus) already GREEN there and the
SmoothPath dz dropping from 27.14y catastrophic to 2.61y manageable.
The MaNGOS/data baseline therefore over-stated the actual live-FG
problem surface by an order of magnitude.

### Surface E is a no-op on prod-data — RETIRED

The four `Corridor_*_DoesNotPassThroughFcStallPoly` gates pass against
both bakes. The corridors on prod-data don't include the fc_stall poly
(0x...15001A9F). Culling poly 6815 of `0004635.mmtile` would change
nothing in the live FG's smooth-path or corridor. Surface E is
retired; the previous "Surface E retry needed" framing was based on
the silent test-data overwrite hiding the prod-data corridor reality.

### Phase 2 candidate surfaces (revised)

The actual prod-data RED gates target three distinct geometry
defects, none of which Surface E addresses:

- **F. UBRS portal `surfaceZ=null` repair.** **ATTEMPTED 2026-05-14,
  REVERTED — see "Surface F NEGATIVE" section below.** `ubrs_portal`
  resolves to polyRef `0x0001000014F02CDF` flagged Ground, but
  Navigation.dll's `GetPolyAtCoord` returns `surfaceZ=null`. Diagnosis:
  the polygon's footprint is short of the queried XY (the polygon's
  nearest point is at (-7523.715,-1232.9683), 0.3y off the queried
  (-7524,-1233)). Tightening `maxSimplificationError` (default 1.8)
  was the recon's proposed knob. 0.5 overbuilt; 1.2 flipped the target
  gate GREEN but regressed two previously-GREEN corridor gates AND
  exploded SmoothPath A* runtime (>9min vs <100ms). Reverted; needs a
  different bake knob (candidates: per-tile `walkableErosionRadius=0.0`
  to extend polygon footprint toward source walkable boundary, or
  per-tile `cs`/`tileSize`/`maxVertsPerPoly=3` in the OG-zeppelin
  style).
- **G. BWL corridor terminus extension by one poly.** Corridor ends
  at `0x14F01877`, BWL portal poly is `0x14F01B1B`. Same tile cluster
  as F. Surface F's negative result implies the same maxSimplificationError
  knob won't close G either — a structurally different connectivity
  fix is needed. Next attack: per-tile `walkableErosionRadius=0.0` (or
  `walkableRadius` reduction localized to this tile) so the corridor
  terminus can reach into the portal poly's XY footprint.
- **H. Sub-3y smooth-path dz removal.** 4 WP-to-WP `dz > 1.8y`
  violations on FC→UBRS, worst 2.61y at idx 259 between
  `(-7673.9,-1746.2,136.0)` and `(-7679.0,-1747.5,133.4)`. These are
  in the Ruins-of-Thaurissan ascending corridor, not the BRM upper
  cluster. Either `WAYPOINT_VERTICAL_REACH_TOLERANCE` could absorb
  this with a small bump (1.25y → 3y) — but per the freeze rules,
  that's a managed-side hack — or a per-tile `walkableClimb`
  tightening to fragment the offending polys.

The MaNGOS/data 27y MAGMA polygon `0x0001000014F002AC` is a MaNGOS/
data-only artifact; not present on prod-data. The "fragment a single
36y polygon" framing is wrong for the live-FG problem.

### Surface F NEGATIVE result (2026-05-14)

Attempt: per-tile `maxSimplificationError` tightening on tile `3446`
(MmapGen CLI `--tile 34,46`, filename `0004634.mmtile` — the BRM upper
portal cluster). The recon localized the gate failures to detour tile
slot bits `0x14F`, which decodes (via world coord → tile XY math) to
tileX=34/tileY=46 for the entire FC→BRM corridor terminus + all four
portal polys.

Two iterations:

1. **mse=0.5** — bake aborted with `Too many vertices! (0x28f84)`.
   Tile NOT written. Detour's per-tile 16-bit vertex limit (65,535
   verts) was exceeded by ~2.5×. The same overbuild that the existing
   `_4029_README_erosion` config note already warns about for a 0.0y
   probe.
2. **mse=1.2** — bake completed cleanly. Tile size grew 3,020,100 →
   3,619,812 bytes (+20%). Promoted to both `D:\MaNGOS\data\mmaps` and
   `D:\wwow-bot\prod-data\mmaps`; `wwow-pathfinding` restarted healthy.

Property-test outcome (prod-data) for mse=1.2:

| Gate | Baseline (mse=1.8 default) | mse=1.2 attempt | Delta |
|---|---|---|---|
| Walkable_AllFourPortals_HaveGroundPoly | ❌ ubrs_portal surfaceZ=null | ✅ **flipped GREEN** | **+** |
| Corridor_FcToUbrsPortal_TerminatesAtPortalPoly | ✅ | ❌ terminates at 0x14F044FE, portal poly is 0x14F04474 | **−** |
| Corridor_FcToLbrsPortal_TerminatesAtPortalPoly | ✅ | ❌ terminates at 0x14F044FE, portal poly is 0x14F03FCA | **−** |
| Corridor_FcToBwlPortal_TerminatesAtPortalPoly | ❌ corridor 1 poly short | ❌ STILL 1 poly short (new polyIdxes) | (no change) |
| SmoothPath_FcToUbrsPortal_NoUnreasonableZJump | ❌ 4 violations, worst 2.61y at idx 259 | ❌ **hung >9min** (was ~80ms baseline) | catastrophic perf regression |
| **Aggregate** | **11/14** | **10/14 + A* perf explosion** | **net −1 gate + live-FG fatal latency** |

Diagnostic interpretation:

- Target gate (Walkable_AllFourPortals) DID flip GREEN — the tighter
  simplification did extend the ubrs_portal polygon's footprint to
  cover (-7524,-1233). The recon's hypothesis was correct on the
  precision direction.
- But the bake's denser polygon graph (corridor poly count 287→355
  on FC→UBRS) shifted EVERY polyIdx in the upper-cluster: the new
  portal polys are at different polyrefs (0x14F04474 vs 0x14F02CDF),
  and the new corridor terminuses (0x14F044FE) don't match. Two
  previously-GREEN corridor gates went RED as a side effect.
- SmoothPath A* runtime explosion (>9min) is fatal for live FG —
  bot path queries would stall the BotRunner. Even ignoring the
  property test failures, the latency alone disqualifies this bake.

Reverted. Restored both `D:\MaNGOS\data\mmaps\0004634.mmtile`
(3,020,100 bytes) and `D:\wwow-bot\prod-data\mmaps\0004634.mmtile`
(2,378,004 bytes) from
`tmp/test-runtime/visualization/pathfinding/brd/latest/backup/
0004634.before_surface_F_attempt_{mangos,prod}.mmtile`. Restarted
`wwow-pathfinding` healthy. Re-confirmed 11/14 prod-data baseline
unchanged.

`tools/MmapGen/config.json` keeps the `_3446_NEGATIVE_RESULT_surface_F`
README documenting the attempt, the overbuild threshold, and the
diagnostic for the next iteration's knob choice. The per-tile
`maxSimplificationError` override is reverted (only the baseline
`walkableErosionRadius=0.2` + `debugStageCropWow` remain).

Conclusion: `maxSimplificationError` is the WRONG knob for the
polygon-footprint-coverage problem. The polygon edges DO get tighter
to source geometry, but the cost is (a) every polyIdx in the tile
shifts (breaking previously-green corridor gates) and (b) A* explodes
on the denser graph. The polygon-footprint short of queried XY can
likely be addressed instead by lowering `walkableErosionRadius` to 0.0
for tile `3446` — eliminate the source-support erosion margin that
shrinks each walkable polygon by 0.2y. This would extend polygon
footprints WITHOUT splitting existing polys (so polyIdxes stay stable
and A* runtime stays bounded). The Detour header agentRadius=1.0247y
is preserved (no agent-capsule change). Next iteration should try
this on Surface G first since G is now the only un-attempted F-tile
gate that hasn't been ruled out.

### Re-run recipe (data-source aware)

```powershell
Push-Location 'E:\repos\Westworld of Warcraft'

# Property tests against PROD-DATA (what live FG actually runs against)
$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'
dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj `
  --configuration Release --no-build -m:1 -p:UseSharedCompilation=false `
  --filter "FullyQualifiedName~BrmAscentRenderingExpectations" `
  --logger "console;verbosity=normal"

# Property tests against MaNGOS/data (recon-baseline comparison)
$env:WWOW_DATA_DIR='D:\MaNGOS\data'
dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj `
  --configuration Release --no-build -m:1 -p:UseSharedCompilation=false `
  --filter "FullyQualifiedName~BrmAscentRenderingExpectations" `
  --logger "console;verbosity=normal"

Pop-Location
```

### 4-route live FG validation post-crash-mitigation (2026-05-14)

Recipe (per prompt):
```powershell
$env:WWOW_BRM_DUNGEON_TRAVEL_TEST='1'
$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'
$env:WWOW_DATA_DIR='D:\MaNGOS\data'
$env:WWOW_LONG_PATHING_TIMELINE='1'
dotnet test Tests\BotRunner.Tests\BotRunner.Tests.csproj `
  --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false `
  --filter "FullyQualifiedName~LongPathingTests.FlameCrestToBrmDungeonEntrance" `
  --logger "console;verbosity=normal" `
  -- RunConfiguration.TestSessionTimeout=2700000
```

Outcome (`tmp/test-runtime/results-pathfinding/brm_4route_after_crash_mitigation.trx`,
6m 22s total):

| Sub-test | Outcome | Final position | Duration |
|---|---|---|---|
| UBRS | wall-collision-creep stall | **(-7949.8, -1162.8, 170.8) — `brm_south_lo` ridge** | 3m 39s |
| BRD  | wall-collision-creep stall | (-7522.2, -2139.9, 132.5) — near FlameCrest | 34s |
| LBRS | wall-collision-creep stall | (-7525.6, -2147.0, 131.8) — near FlameCrest | 30s |
| BWL  | wall-collision-creep stall | (-7526.2, -2149.7, 131.7) — near FlameCrest | 25s |

**All four sub-tests reached the FlameCrest TravelTo dispatch
independently** — no cross-map crash, no "no FG target" cascade, no
WoW.exe death cluster. The crash-mitigation acceptance criterion is
met. UBRS even walked the bot ~1100y from FlameCrest to the BRM south
face ridge (the exact `brm_south_lo` recon coord (-7949.7,-1162.8,170.8)
from entry #4), which had no rendering objection on prod-data — proving
the prod-data corridor has substantial real-world fidelity that
MaNGOS/data lacked.

The remaining 4/4 RED is the original bake-side BRM ascent stall, not
the cross-map crash — the `SnapshotStallGuard` wall-collision-creep
detector caught each one at its true failure site. Per the revised
Phase 2 surface list above (F/G/H), the next attempt should target
the actual prod-data failure modes:

- F (UBRS portal surfaceZ=null): the bot stalls at the BRM south face
  before reaching the upper portal cluster, suggesting the corridor
  drops the bot at the cliff transition with no smooth landing.
- G (BWL corridor +1 poly): stall before leaving FlameCrest implies
  Detour's partial-path return at the corridor terminus seeds an
  unreachable smooth-path endpoint.
- H (sub-3y smooth-path dz at idx 259): the 2.61y vertical jump at
  (-7676,-1747,135) is in the Ruins-of-Thaurissan ascending corridor,
  ~150y SE of the BRM south face. Likely the actual stall trigger for
  three of the four sub-tests (BRD/LBRS/BWL all stalled within 8y of
  FlameCrest, before reaching this WP, but the corridor query may be
  failing earlier as a result of the same poly fragmentation issue).

Screenshot artifacts:
- `tmp/test-runtime/screenshots/long-pathing/Long-travel-wall-collision-creep-before-Flame-Crest-UBRS-portal-FG-physics-rejec-LPATHFG1-client-75924-win0-20260513_221217.png`
- `tmp/test-runtime/screenshots/long-pathing/Long-travel-wall-collision-creep-before-Flame-Crest-BRD-portal-FG-physics-reject-LPATHFG1-client-75924-win0-20260513_221254.png`
- `tmp/test-runtime/screenshots/long-pathing/Long-travel-wall-collision-creep-before-Flame-Crest-LBRS-portal-FG-physics-rejec-LPATHFG1-client-75924-win0-20260513_221328.png`
- `tmp/test-runtime/screenshots/long-pathing/Long-travel-wall-collision-creep-before-Flame-Crest-BWL-portal-FG-physics-reject-LPATHFG1-client-75924-win0-20260513_221354.png`

