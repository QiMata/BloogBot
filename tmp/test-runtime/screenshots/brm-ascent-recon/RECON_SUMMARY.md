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

This is on the BRM south-face approach (XY near `brm_south_lo`'s ridge
sample at -7949.7,-1162.8 — same general area). The bake's smooth path
dips down to z=97 (Searing Gorge floor near the south face), then jumps
up by 27y in a single waypoint pair to z=124. The bot's NavigationPath
will stall at this jump; even a relaxed
`WAYPOINT_VERTICAL_REACH_TOLERANCE > 1.25y` would not absorb a 27y
jump.

**This is the live FG failure mode.** The four reverted attempts each
tried to address a different symptom (steep-slope filter, off-mesh
ascent), but the underlying cause is the bake string-pulling the
smooth path across a vertical cliff face the player physics cannot
traverse. The corridor at that index walks across cliff-face polys
that are in the bake but not physically continuous in-game.

### Implication for the Phase 2 fix surface

The recon does NOT support the targeted-poly-cull approach for fc_stall
that the earlier draft suggested. It DOES support a bake-side fix
focused on the BRM south-face cliff transition (around XY
(-7945,-1290) z=97→124). Candidate fix surfaces:

- **C.** **Per-tile bake fragmentation around the BRM south-face cliff
  band** — fragment the polygons that bridge z≈100 to z≈125 on the
  southwest BRM face into smaller pieces. Likely tiles `0x14F0` (BRM
  upper portal cluster) and adjacent. The mechanism is a per-tile `cs`
  / `ch` reduction or `maxSimplificationError` tightening for those
  specific tiles, so Recast's contour build doesn't emit the cliff
  edge as a single 27y vertical span.
- **D.** **Surface 3 revisited (per-tile `maxSteepSlopePolyZRange`
  bake post-process)** — the cliff-bridging polys may also be
  STEEP-classified (the brm_southnew sample at z=133 is STEEP). A
  per-tile post-process that fragments any STEEP poly with `zRange > 6y`
  would directly break the 27y jump. Surface 3 was the only never-tried
  surface in the four-attempt list; recon now has rendering evidence
  to justify it. Still needs design review per the docs.
- **E.** **Targeted PolyRef cull of the cliff-bridging polys at
  `brm_south_lo` neighborhood**, using the same `NavMeshTileEditor`
  pipeline as the BRM Round-3 work
  (`project_pfs_overhaul_006_brm_phase4_findings`). Requires identifying
  the specific polyrefs that span the 27y gap.

Property test
`SmoothPath_FcToUbrsPortal_NoUnreasonableZJump` is the
green-or-red gate for whichever surface is chosen. The
`Corridor_*MustReachTarget` tests are the secondary gates — they may
require a separate intra-tile connectivity fix in the BRM upper portal
cluster.

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
