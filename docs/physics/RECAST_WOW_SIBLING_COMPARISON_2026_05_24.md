# 2026-05-24 Recast And WoW Sibling Comparison Memo

Verified on 2026-05-24. This memo uses primary sources only and is meant to
answer the "stop generic tuning" handoff for WWoW tile `1:40,29`.

## Scope

- Questions answered:
  - what upstream Recast actually recommends for the major bake knobs
  - what sibling WoW generators currently default to
  - what map-local overrides sibling projects already accept
  - which candidate fixes are compatible with WWoW's current `.mmtile` +
    Detour loader/runtime, and which ones are not
- Local constraint:
  - tile `1:40,29` is not primarily a smooth-path problem
  - the remaining six `CriticalWalkLegs` reds are still stacked-support /
    trapped-basin problems
  - post-path repair stays an anti-pattern

## Upstream Recast Guidance

These are the official upstream starting points, not WWoW prescriptions.

| Setting | Upstream guidance | WWoW read for WoW-like layered geometry |
|---|---|---|
| `cs` / cell size | `rcConfig` docs say start around `r/2` or `r/3`. | Use this as a floor for horizontal feature capture, not a magic target. If a deck lip, rope, or hallway support aliases out at raster time, no later contour/Detour tuning can recover it. |
| `ch` / cell height | `rcConfig` docs say `ch` is often `cs/2` or smaller, and should not exceed `cs`. | Keep `ch` tied to real step/slope evidence. Raising `ch` can merge vertical ambiguity, but it also coarsens support separation. |
| `walkableRadius` | `ceil(r / cs)` is the normal setting. Recast explicitly allows `0`, but says that is not recommended unless runtime edge collision is handled separately. | For WWoW, `walkableRadius=0` is only a serious branch if we are prepared to add runtime capsule-vs-edge checks. It is not a free bake-only fix. |
| `walkableClimb` | `ceil(maxClimb / ch)`. | Must stay physics-derived. If a branch only works by inflating climb beyond client evidence, it is not promotable. |
| `walkableHeight` | `ceil(h / ch)`. | Same rule: derive from the client capsule / standing clearance contract. |
| `walkableSlopeAngle` | practical upper limit is usually about `85` degrees. | Not a cleanup knob for stacked floors. Use it for actual walkable terrain limits only. |
| `maxEdgeLen` | upstream API text says a good starting value is about `walkableRadius * 8`. | Good global guidance, but our current `40,29` problem is not "one more generic edge split." If used, use locally and prove stage movement. |
| `maxSimplificationError` | docs say good values are usually `1.1` to `1.5`, with `1.3` as a common start. | Important caution: this is exactly why `1.3` had to be tested, and WWoW already proved that **global** `1.3` is bad on `40,29`. Keep any `1.3` use local and stage-backed only. |
| `maxVertsPerPoly` | must stay `>= 3`, and if the output is loaded by Detour the upper bound is `DT_VERTS_PER_POLYGON`. | Loader-compatible, but WWoW already proved that pushing this tile to `4` or `6` regresses the deck crop. |

## Current Sibling WoW Generator Defaults And Override Surfaces

### AzerothCore current published defaults

AzerothCore's current Doxygen docs publish the cleanest numeric baseline:

- `walkableSlopeAngle = 60.0f`
- `walkableRadius = 2`
- `walkableHeight = 6`
- `walkableClimb = 6`
- `vertexPerMapEdge = 2000`
- `vertexPerTileEdge = 80`
- `maxSimplificationError = 1.8f`

AzerothCore also documents current config resolution that accepts:

- global mesh settings
- per-map overrides
- per-tile overrides
- override fields for:
  - `walkableSlopeAngle`
  - `walkableRadius`
  - `walkableHeight`
  - `walkableClimb`
  - map-level `cellSizeHorizontal`
  - map-level `cellSizeVertical`

That matters because it proves that **surgical map/tile overrides are a normal
current WoW-generator practice**, not a WWoW-only divergence.

### TrinityCore accepted map-local overrides

The most direct current public evidence is TrinityCore discussion
`#26868`, where a maintainer points to existing map-specific generator edits:

- map `562` / Blade's Edge Arena:
  - `walkableRadius = 0`
  - reason given: lets actors walk on the ropes to the pillars
- map `48` / Blackfathom Deeps:
  - `ch *= 2`
  - reason given: reduce underground levels

That is valuable for WWoW because it shows two specific override families that
other WoW projects already consider legitimate:

- zero-radius bake for narrow supports
- coarser vertical quantization for stacked underground ambiguity

### vmangos current generator shape

Current vmangos still follows the classic WoW mmap flow:

- terrain/map load
- vmap load
- off-mesh load
- then Recast stages

Its current public `TileWorker.cpp` also still exposes configuration-driven
defaults such as:

- `maxSimplificationError = 1.8f`
- `walkableSlopeAngle = 75.0f`
- `walkableSlopeAngleVMaps = 61.0f`

The important cross-project conclusion is not that vmangos has the "right"
numbers for WWoW. It is that the sibling generators are still all variations on
the same Recast/Detour pipeline. None of them are doing a fundamentally
different layered-support solve that WWoW can just copy in.

## What The Sibling Data Actually Suggests For WWoW

### Not a good conclusion

"Import sibling defaults" is not a real recommendation.

Why:

- WWoW tile `40,29` already has strong local negative evidence against generic
  global knob churn.
- TrinityCore/AzerothCore/vmangos do not give us a proven "stacked Orgrimmar
  hallway support" fix out of the box.
- WWoW's geometry contract is richer than stock sibling generators because the
  bake includes server-spawned GO geometry. That makes blind value import even
  less trustworthy.

### Good conclusion

The sibling sources support **surgical, map-local, stage-backed fixes**.

That is the usable lesson:

- local contour surgery is acceptable if it stays within Recast's contour ->
  polymesh contract
- local `ch` overrides are acceptable if the failure is truly vertical
  quantization
- local `walkableRadius=0` is acceptable only if WWoW is ready for runtime
  edge-collision consequences

## Candidate Solution Families For WWoW Tile 1:40,29

At least three families were required. These are ordered by current signal.

### 1. Local contour resimplification around `1523.800,-4425.900,17.100`

Why it is justified:

- official Recast makes `maxSimplificationError` a **contour-stage** decision
- upstream `simplifyContour` uses RDP simplification plus optional edge
  tessellation on wall/area edges
- WWoW's strongest prior clue was exactly a contour-shape problem: default
  support contour too coarse, fully raw contour too fragmented

Compatibility:

- fully compatible with current `.mmtile` format
- fully compatible with current Detour loader/runtime
- no migration required

Current status:

- implemented in WWoW as `ResimplifyRawAnchorSupportContours(...)`
- first experiment did **not** beat the prior raw+preserve branch; see
  "Newest targeted experiment" below

### 2. Source-support classification / lower-competitor tightening

Why it is justified:

- the newest manifests still show `1523.8` failing at
  `finalDetour / lower_competitor_dominant`
- `1364.867,-4374.000,26.109` still fails at
  `finalDetour / winner_component_trapped`
- that means the most durable remaining failure surface is now "bad final basin
  wins" more than "support vanished before contours"

Compatibility:

- fully compatible with current `.mmtile` format
- fully compatible with current Detour loader/runtime

Practical shape:

- improve source-support labeling before contour/poly build
- or improve final support/lower classification without post-route repair
- must be stage-manifest backed, not inferred from route logs alone

### 3. Sibling-style map-local override branch

Why it is justified:

- TrinityCore already documents this family as accepted practice
- AzerothCore already exposes current map/tile override resolution

Candidate sub-branches:

- local `ch` override if a targeted stage manifest proves vertical aliasing is
  the real failure
- local `walkableRadius=0` if a targeted proof shows the bake only loses the
  narrow support due to erosion

Compatibility:

- local `ch` override: tile-format compatible, runtime compatible
- local `walkableRadius=0`: tile-format compatible, but **runtime incompatible
  with current assumptions** unless WWoW adds explicit capsule-edge collision

### 4. Alternative runtime/generator strategy

Only one serious official alternative surfaced in the primary sources: staying
inside Recast/Detour but moving to a different runtime/storage stack such as
`DetourTileCache`.

Why it is not the next WWoW move:

- upstream describes `DetourTileCache` as a streaming/open-world/runtime
  feature, not a baked solution to stacked support ambiguity
- it would require loader/runtime/tooling work
- it does not directly solve "which of two vertically ambiguous supports should
  survive as the winning basin"

Compatibility:

- requires format/runtime/tooling changes
- not a near-term fix for `40,29`

## Newest Targeted Experiment

### Code surface corrected and expanded

WWoW still uses upstream-style contour logic in
`tools/MmapGen/contrib/mmap/src/TileWorker.cpp`, but the first 2026-05-24
resimplify write-up had an important bug:

- `RestoreRawAnchorSupportContours(...)` sets `contour.nverts = contour.nrverts`
- `ResimplifyRawAnchorSupportContours(...)` originally still skipped when
  `contour.nrverts <= contour.nverts`
- practical consequence:
  - the earlier `og_4029_prepoly_resimplify_1523_mse13_v1` branch never
    actually re-ran simplification after raw restore

The corrected surface now includes:

- `prePolyResimplifyAnchorSupportMaxError`
- `prePolyResimplifyAnchorSupportMaxEdgeLen`
- `prePolyResimplifyAnchorSupportTessellateWallEdges`
- `prePolyResimplifyAnchorSupportTessellateAreaEdges`
- `[CONTOUR-ANCHOR-RESIMPLIFY-CANDIDATE]` diagnostics
- fixed raw-contour guards in both restore/resimplify helpers

### Exact commands and branches

- Build:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
- Real bug-fixed local `1.3` branch, wall tessellation off:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_notess_v3' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_notess.json'`
- Focused tests for the bug-fixed `1.3` branch:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_mse13_notess_v3_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
- Full tests for the bug-fixed `1.3` branch:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_mse13_notess_v3.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
- Equivalent bug-fixed `1.3` branch, wall tessellation on:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_v2' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_diag.json'`
- Local `maxEdgeLen` isolation:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_edge24_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_edge24.json'`
- Tight-end upstream-range `1.1` branch:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse11_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse11.json'`
- Focused tests for the `1.1` branch:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_mse11_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
- Full tests for the `1.1` branch:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_mse11_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
- Restore command:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_resimplify_bugfix_iteration_20260524' -DataDir 'D:\wwow-bot\test-data'`

### Observed results

- Diagnostic proof from the real branch:
  - `[CONTOUR-ANCHOR-RAW] anchor=(1523.800,-4425.900,17.100) contour=1 region=8 verts=19->448`
- Bug-fixed local `1.3` branches:
  - artifacts:
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse13_notess_v3-20260524T230226Z/`
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse13_v2-20260524T230551Z/`
  - raw contour simplification:
    - `448 -> 21`
  - saved tile hash:
    - `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
  - focused:
    - `7/7`
  - full `CriticalWalkLegs`:
    - `17/23`
  - important read:
    - wall-edge tessellation on vs off produced the same candidate and the same
      serialized tile
- Local `maxEdgeLen` isolation:
  - artifact:
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse13_edge24_v1-20260524T231222Z/`
  - candidate:
    - still `448 -> 21`
  - saved tile hash:
    - still `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
  - extra tests:
    - not rerun, because the serialized tile matched the already-validated
      bug-fixed `1.3` branch exactly
- Tight-end upstream-range `1.1` branch:
  - artifact:
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse11_v1-20260524T231456Z/`
  - raw contour simplification:
    - `448 -> 22`
  - saved tile hash:
    - `089DBEC002F4D8DF9BDBD091D32F659364F958C40F50E04F9D95357EDDD39FAD`
  - focused:
    - `7/7`
  - full `CriticalWalkLegs`:
    - `17/23`
  - same six remaining reds:
    - `orgrimmar_city_live_vertical_replan_recovery`
    - `orgrimmar_city_hallway_live_wall_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_exterior_incline_live_stall_exact_recovery`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`

### Current stage authority after the corrected branches

Use the newest manifest files as authority. The earlier same-day `v1` prose is
obsolete because that branch never actually re-simplified the raw contour.

- Bug-fixed `F02666...` branch:
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1522.500,-4424.100,17.000` ->
    no first-bad stage
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Bug-fixed `089DBE...` branch:
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1522.500,-4424.100,17.000` ->
    no first-bad stage
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`
- Restored baseline `A01DEE...` branch:
  - `1523.800,-4425.900,17.100` ->
    `finalDetour / lower_competitor_dominant`
  - `1364.867,-4374.000,26.109` ->
    `finalDetour / winner_component_trapped`

### Interpretation and next moves

1. The earlier "local resimplify no-op" conclusion was wrong because of the
   raw-restore skip guard bug; the corrected branches are now the real contour
   evidence.
2. The contour-family learning is still clear:
   - default simplified support contour was effectively too coarse
   - raw-preserved contour was effectively too fragmented
   - upstream-style local resimplify at `1.3` and `1.1` still collapses to a
     near-coarse `21/22`-vertex contour and does not change the route set
3. Local `maxEdgeLen` reduction to `24` was a no-op for this contour, so that
   knob is not the missing intermediate surface here.
4. The next promotable branch should pivot to:
   - explicit local contour preservation / custom local simplification around
     `1523.8`
   - or source-support / lower-competitor classification work
5. Do not spend another loop on generic upstream resimplify knob churn inside
   this same family without a new geometric hypothesis.

### 2026-05-25 UTC: local raw-window contour reinjection follow-up

- New native experiment surface in `TileWorker.cpp`:
  - `prePolyResimplifyAnchorSupportLocalPreserveRadius`
  - helper `InjectAnchorLocalRawVertices(...)`
  - refactor helper `FinalizeAnchorContourFlags(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - radius `3.0` bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r3.json'`
  - radius `3.0` focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_localraw_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - radius `3.0` full tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_localraw_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - radius `6.0` bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r6.json'`
  - radius `6.0` focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_localraw_r6_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - radius `6.0` full tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_localraw_r6_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_localraw_window_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Results to preserve:
  - radius `3.0` artifact:
    `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r3_v1-20260525T001650Z/`
  - radius `3.0` contour/log facts:
    - `[CONTOUR-ANCHOR-LOCAL-RAW] ... injectedRawVerts=25 preserveRadius=3.000`
    - `[CONTOUR-ANCHOR-RESIMPLIFY-CANDIDATE] ... candidateVerts=46`
    - hash:
      `F076A6FA0974755EA1F8384BB3C2154E064804EDD8604001030F6C6D637C2DC5`
    - focused:
      `7/7`
    - full:
      `17/23`
    - manifest:
      - `1523.800,-4425.900,17.100` still ->
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` still ->
        no first-bad stage
      - `1364.867,-4374.000,26.109` still ->
        `finalDetour / winner_component_trapped`
    - critical cull proof:
      - `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=2 lowerFringeCulled=0 supportBandCandidates=2`
  - radius `6.0` artifact:
    `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r6_v1-20260525T002119Z/`
  - radius `6.0` contour/log facts:
    - `[CONTOUR-ANCHOR-LOCAL-RAW] ... injectedRawVerts=124 preserveRadius=6.000`
    - `[CONTOUR-ANCHOR-RESIMPLIFY-CANDIDATE] ... candidateVerts=145`
    - hash:
      `5997F2588CE58B979CE0CC8C199076F7C5A979284C2AEFFB837E99377A21E459`
    - focused:
      `7/7`
    - full:
      `17/23`
    - manifest regression:
      - `1522.500,-4424.100,17.000` ->
        `finalDetour / support_footprint_missed_anchor`
    - route-quality regression:
      - `orgrimmar_city_hallway_live_wall_stall_recovery` shifted deeper to
        `(1514.0,-4426.5,20.2)` instead of the prior hallway stall shape
    - critical cull proof:
      - `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=14 lowerFringeCulled=0 supportBandCandidates=14`
- Interpretation:
  - local raw-window reinjection is now a bounded negative family
  - radius `3.0` proves that creating a real intermediate contour
    (`448 -> 46`) is still not enough when the final support footprint does not
    reach the bad anchor
  - radius `6.0` proves that "more local raw detail" is not a monotonic
    improvement; it widened the contour but regressed the nearby hallway anchor
  - the decisive signal is the bake-side cull skip: `1523.8` still has
    `supports=0` and `lowerFringeCulled=0` even when support-band fragments
    survive nearby, so the missing fix surface is support-footprint /
    overlap / earlier classification, not more generic contour density
  - next promotable work should pivot away from this contour-detail family and
    toward source-support / lower-competitor footprint handling, with a
    research-backed local `ch` branch as the only sibling-style override still
    worth testing here

### 2026-05-25 UTC: support-gap finalDetour follow-up

- New native experiment surface in `TileWorker.cpp`:
  - `postDetourCullAnchorPolyStacksSupportGap2D`
  - helper `GetDetourBoundsGap2D(...)`
- Exact commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - gap `1.0` bake:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_support_gap1_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_anchor_support_gap1.json'`
  - focused tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_support_gap1_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
  - full tests:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_anchor_support_gap1_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_support_gap1_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
- Results:
  - artifact:
    `tmp/bake-sweeps/og_4029_anchor_support_gap1_v1-20260525T005200Z/`
  - hash:
    `33F6D5DA3189CF1985120B247D23C9EF0C978995B10FF79C90A65DB5ABFE991D`
  - focused:
    `7/7`
  - full:
    `17/23`
  - decisive bake-side proof:
    - `1523.8` changed from
      `lowerFringeCulled=0`
      to
      `lowerFringeCulled=2`
    - new log:
      `[DT-ANCHOR-CULL-SKIP] ... lowerFringeCulled=2 ... bestSupportGap2D=0.300`
  - stage authority stayed flat:
    - `1523.800,-4425.900,17.100` still ->
      `finalDetour / lower_competitor_dominant`
    - `1522.500,-4424.100,17.000` stayed ->
      no first-bad stage
    - the same six `CriticalWalkLegs` reds remained
- Interpretation:
  - this proves the finalDetour cull can reach a small lower fringe around
    `1523.8`, but that fringe is not the dominant surviving basin
  - the support-gap surface is therefore useful instrumentation and a bounded
    experiment, not a promotable fix at `1.0`
  - the next serious branch needs to move earlier again, into
    source-support / compact-heightfield footprint handling

### 2026-05-25 UTC: support-footprint follow-up

- Two next branches were tested against that same footprint hypothesis:
  - raw+preserve contour + support-gap:
    `EFD2DCE534EFB2A9039447DFBE84C6F695701C507ED60DC0592C71752EB783FD`
  - `anchorSourceSupportFloorSlackBelow = 0.35`:
    `CD5F1EB58003C4326D03B8A638EA154AF2855F3547520000AE39E45E59163FE0`
- Both kept focused `7/7` and full `17/23`.
- The sibling-style lesson is important:
  - even though TrinityCore/AzerothCore-style local overrides are legitimate as
    a category, this tile is now a concrete example of when **not** to keep
    widening a generic local slack override
  - the `0.35` support-floor widening reduced the useful support-band evidence
    at `1523.8` and regressed sibling anchors, so it is not a promotable
    "map-local override" for WWoW's WoW-geometry contract
- The stronger compatible direction remains:
  - exact-neighborhood support-footprint work that preserves `.mmtile` /
    Detour compatibility
  - or earlier source-support classification before contour/poly loss

### 2026-05-25 UTC: raster patch contour-loss follow-up

- WWoW next tried a fully loader-compatible bake-side bridge:
  `preRasterizeAnchorSupportPatchCoordsWow` +
  `preRasterizeAnchorSupportPatchHalfExtent`.
- The useful sibling-style lesson is not "ship a raster patch":
  - raster patch only kept the stable baseline hash
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - but it changed the stage answer for `1523.8` earlier in the bake:
    `median` and `regions` now had `supportCell=true`
  - the same recovered footprint then died again at `contours`
- Combining the raster patch with WWoW's raw+preserve contour carry landed
  exactly on the old non-promotable shard hash
  `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`.
- Practical sibling comparison update:
  - this tile is now a concrete example of a loader-compatible idea that is
    valuable as proof even when it is not yet a ship candidate
  - the next promising compatible family is local contour-builder preservation
    for a recovered source-backed footprint, not more generic per-tile slack
    overrides and not more finalDetour-only tuning

### 2026-05-25 UTC: contour-band boundary carry follow-up

- WWoW then tried the next contour-local shape on top of that raster proof:
  `prePolyResimplifyAnchorSupportBandBoundaryRadius`.
- This branch was selected from official Recast behavior, not guesswork:
  - `rcBuildContours(...)` documents that raw contours match the region
    outlines exactly, while the simplified contour only guarantees mandatory
    portal/area vertices:
    https://recastnav.com/group__recast.html
  - upstream `simplifyContour(...)` first seeds points where region or area
    transitions occur, then falls back to coarse seeds before its error-based
    splits:
    https://raw.githubusercontent.com/recastnavigation/recastnavigation/main/Recast/Source/RecastContour.cpp
- Exact experiment:
  - variant:
    `og_4029_raster_support_patch06_boundary_seed_r3_v1`
  - full-rerun artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_seed_r3_v1_fullrerun-20260525T024605Z/`
  - changed hash:
    `E58B0DF11E71196123A377094B4A41710238591B8D454352BDF93B7C825D424F`
  - focused:
    `3/7`
  - full:
    `20/23`
- The important negative proof is where the helper fired:
  - raw restore expanded three same-band contours near `1523.8`:
    `13 -> 226`, `11 -> 158`, and `3 -> 10`
  - the boundary carry then injected `3` verts on contour `1 / region 8` and
    `2` verts on contour `3 / region 7`
  - the anchor-stage answer still stayed
    `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - but the deck crop regressed back into the old bridge/trim failure class
- Practical sibling update:
  - this is the first strong proof that a contour-local preservation idea can
    still be the wrong *semantic scope*
  - the next compatible branch should isolate the single recovered
    contour/region that actually touches the raster-patch neighborhood, not all
    same-band contours intersecting the anchor window
  - in sibling ports, this is the boundary between a promotable local contour
    override and just another hidden global knob in disguise
  - follow-up later the same night on `2026-05-25` UTC: WWoW added an explicit
    single-contour selector surface
    (`prePolySupportContourSelectionMode`, with the older
    `prePolySelectAnchorContainingSupportContourOnly=true` preserved as a
    legacy alias) and isolated the two plausible contours one at a time:
    - branch `og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1`
      saved hash
      `5FE8640E4B7D756F74DBCA47952345F8A06507C6C81BA330E400092228399340`
    - branch
      `og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1`
      saved hash
      `84C09EFE50E2E04114DCF3A4F218A1DBF29E4E6F8776680CC966B47D2ADFB856`
    - both branches still regressed to focused `3/7`, full `20/23`, and still
      left `1523.8` at `finalDetour / lower_competitor_dominant`
    - the decisive log proof was that the selector really did isolate the two
      plausible candidates one at a time:
      `contour 3 / region 7 containsAnchor=1 closestDistance2D=0.200`
      versus
      `contour 1 / region 8 containsAnchor=0 closestDistance2D=0.836`
    Practical rule: once both the literal anchor-containing contour and the
    nearest non-containing contour fail in isolation, stop spending cycles only
    on contour choice. The next compatible retry needs a different local
    contour preservation / simplification shape, not just a different contour
    selector.

  - follow-up later on `2026-05-25` UTC: WWoW then tried the midpoint between
    the too-sparse boundary carry and the too-broad local-raw carry by adding
    `prePolyResimplifyAnchorSupportBandLocalPreserveRadius`
    (support-band-local raw-vertex reinjection around the recovered anchor
    footprint)
    - branch `og_4029_raster_support_patch06_band_local_anchoronly_r6_v1`
      saved hash
      `B9E24E82A964DDFD4E7EB10B8401CFB645681DB2EF0ECAF3D784D26B7AA2981A`
    - focused tests regressed to `3/7`; full `CriticalWalkLegs` regressed to
      `20/23`
    - decisive log proof on the selected anchor-containing contour was:
      `11 -> 158 -> 34`, with `preservedSupportBandRawVerts=23`, yet
      `1523.8` still ended at `finalDetour / lower_competitor_dominant`
    Practical rule: boundary-only carry is too sparse, full local-raw carry is
    too broad, and this support-band-local midpoint is still not enough. The
    next serious retry should move earlier into `rcBuildContours(...)` or
    another contour-builder shape instead of spending more time on the same
    post-contour reinjection family.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then tried that
    "move earlier" idea in the smallest possible way by seeding the recovered
    support-band boundary into the local simplifier's mandatory-point phase via
    `prePolyResimplifyAnchorSupportBandBoundarySeedRadius`
    - branch `og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1`
      saved hash
      `EB6F72B9E86E550DB277BA767D2BCB07D5C99337E729191B0C52378CF487DADC`
    - focused tests regressed to `3/7`; full `CriticalWalkLegs` regressed to
      `20/23`
    - decisive log proof on the selected anchor-containing contour was:
      `11 -> 158`, then
      `[CONTOUR-ANCHOR-BAND-SEED] ... seededBoundaryVerts=4 seedRadius=3.000`,
      yet the candidate still collapsed to
      `158 -> 13`
    Practical rule: if an upstream-style early seed of the same recovered
    support-band boundary still collapses to the same coarse candidate and the
    same route set, stop iterating only on boundary-seed timing. The next
    compatible retry needs a different contour-builder shape, not just an
    earlier insertion point for the same boundary endpoints.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then took the
    research-backed local-`ch` fallback in the finer direction
    - branch `og_4029_raster_support_patch06_ch005_v1`
      saved hash
      `4E8C3C6AF492AAA995044BD30345E3A2DB2BDEAA64B1D96D6E6332A2513EC4B9`
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_ch005_v1-20260525T202957Z/`
    - focused/full regressed further to
      `4/7`, `17/23`
    - decisive proof:
      Recast's `rcConfig` docs say smaller `ch` increases vertical raster
      precision and that `walkableClimb` is derived from `ch`, while TrinityCore's
      current mmaps discussion shows a real map-local `config.ch *= 2` override,
      so this was a valid sibling-style fallback rather than folklore
    - decisive result:
      the saved tile changed dramatically
      (`8775316 -> 2398200`, delta `-6377116` bytes), but `1523.8` still kept
      `contours supportCandidateCount=1`,
      `polymesh supportCandidateCount=2`,
      `finalDetour supportCandidateCount=0`, and therefore still ended at
      `finalDetour / lower_competitor_dominant`
    Practical rule: finer local `ch` is a strong bounded negative for this
    tile. If local `ch` stays on the table after contour-family exhaustion, the
    next compatible retry is the coarser sibling-style direction, not more
    "smaller `ch` adds precision" churn.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then closed that
    coarser sibling-style direction too
    - branch `og_4029_raster_support_patch06_ch020_v1`
      saved hash
      `55E5288EC5464DACC1BC696B70BBA6F0A8F808B29A97BAA9A7FA47F266C8A428`
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_ch020_v1-20260525T204524Z/`
    - focused/full snapped back to
      `3/7`, `20/23`
    - decisive result:
      the tile still changed dramatically
      (`8775316 -> 2434340`, delta `-6340976` bytes), but `1523.8` still kept
      `contours supportCandidateCount=1`,
      `polymesh supportCandidateCount=2`,
      `finalDetour supportCandidateCount=0`, and therefore still ended at
      `finalDetour / lower_competitor_dominant`
    Practical rule: the local-`ch` fallback is now exhausted in both
    directions for this anchor. A future compatible retry needs a different
    contour/source-shape change, not more `ch` churn.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then closed the
    last obvious pre-polymesh carry retry by swapping only the selected
    anchor-containing contour back to its full raw `rverts` payload before
    `rcBuildPolyMesh()`
    - upstream basis:
      Recast's `rcContour` docs define `rverts` as raw contour data and
      `verts` as the simplified contour, while `rcBuildContours()` says the
      raw contours match the region outlines exactly
    - new surface:
      `CarrySelectedRawAnchorSupportContours(...)` plus
      `prePolyCarrySelectedRawAnchorSupportCoordsWow`
    - branch `og_4029_raster_support_patch06_fullraw_anchoronly_v1`
      saved hash
      `1B0620C72AC82213750CB15175DC509BD1B55D77F99827DD911E2AB9EF1C11D3`
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_fullraw_anchoronly_v1-20260525T210253Z/`
    - focused/full regressed to
      `3/7`, `19/23`
    - decisive proof:
      `[CONTOUR-ANCHOR-FULL-RAW-CARRY] carried 147 raw contour vertex(s) across 1 contour(s)`,
      i.e. the selected contour was reopened from `11` simplified vertices
      back to its full `158` raw vertices before polymesh
    - decisive result:
      `1523.8` still kept the same final answer
      `finalDetour / lower_competitor_dominant`, while route quality got worse
      into a `1037`-point flightmaster path, a new hallway wall stall, and a
      direct `no_path` on the underpass exact recovery
    Practical rule: once the selected contour's full raw `rverts` payload
    still fails before `rcBuildPolyMesh()`, stop widening the same
    pre-polymesh raw-carry family. The next compatible retry has to change the
    contour-builder shape itself inside or before `rcBuildContours()`.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then tried the
    remaining earlier boundary-shape retry by seeding only the support-band
    boundary crossings during `rcBuildContours()` simplification itself
    - new surface:
      `boundarySeedRadiusCells` on
      `rcAnchorContourSimplifyOverride`,
      `buildAnchorSupportBandBoundaryVertexMask(...)`,
      `seedAnchorSupportBandBoundaryVertices(...)`, and
      `contourBuildSeedAnchorSupportBandBoundaryRadius`
    - branch
      `og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1`
      saved hash
      `3F9EB2930393D48E13B28267D6C11B0E9C0D5282C488D9CE8CC4403FB6C269E4`
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1-20260525T212238Z/`
    - focused/full stayed:
      `3/7`, `20/23`
    - decisive proof:
      `[CONTOUR-BUILD-ANCHOR-SEED] region=7 rawVerts=158 simplifiedVerts=11 seededBoundaryVerts=2 seededSupportBandRawVerts=0 matchedOverrides=1`
    - decisive result:
      the earliest boundary-only seed fired on the right contour but still left
      the same `11` simplified vertices and the same
      `1523.8 -> finalDetour / lower_competitor_dominant` answer
    Practical rule: once the earliest boundary-only seed fires and the
    selected contour still keeps the same simplified vertex count, stop
    iterating on that sparse boundary-only family. The next compatible retry
    needs a denser contour-builder reshape or an even earlier raw-contour /
    region / source-stage change.
  - follow-up later the same night on `2026-05-25` UTC: WWoW then closed the
    "wrong patch center" hypothesis by moving the raster support patch from the
    anchor projection onto the resolved source-support footprint itself.
    - new surface:
      `preRasterizeAnchorSupportPatchCenterMode` plus support-point XY tracking
      in `AnchorSourceSupportProbe`
    - branch
      `og_4029_raster_support_patch06_center_support_anchoronly_v1`
      saved hash
      `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_center_support_anchoronly_v1-20260525T214345Z/`
    - focused/full stayed:
      `3/7`, `20/23`
    - decisive proof:
      `[SRC-ANCHOR-SUPPORT] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) ... dist2D=0.306 inside=0`
      and
      `[HF-ANCHOR-SUPPORT-PATCH] ... center=(1523.668,-4426.176,17.704) centerMode=resolvedSupportPoint halfExtent=0.600 ...`
    - decisive result:
      even after centering the patch on the resolved support point, the
      earliest surviving support component still stayed `0.5315y` away and
      `1523.8` still ended at
      `finalDetour / lower_competitor_dominant`
    Practical rule: patch placement alone is not the missing lever. The next
    compatible retry should change the local raster patch shape itself, not
    just move its center or revisit contour timing.

## Restore State

At the end of this corrected loop, `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
was restored to the stable baseline:

- restore artifact:
  - `tmp/bake-sweeps/og_4029_restore_after_center_support_iteration_20260525-20260525T214839Z/`
- restored hash:
  - `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`

## Sources

Accessed on 2026-05-24 unless the page itself states a generated date.

- Recast `rcConfig` docs:
  - https://recastnav.com/structrcConfig.html
- Recast official repo README / module overview:
  - https://github.com/recastnavigation/recastnavigation
- Recast official sample/discussion showing partition tradeoffs and standard
  build order:
  - https://github.com/recastnavigation/recastnavigation/discussions/583
- Recast official `rcBuildContours(...)` docs:
  - https://recastnav.com/group__recast.html
- Recast official `rcContour` raw-vs-simplified struct docs:
  - https://recastnav.com/structrcContour.html
- Recast official contour simplifier source:
  - https://raw.githubusercontent.com/recastnavigation/recastnavigation/main/Recast/Source/RecastContour.cpp
- AzerothCore current global mmaps defaults:
  - https://www.azerothcore.org/doxygen/db/dca/structMMAP_1_1Config_1_1GlobalConfig.html
- AzerothCore current config resolution:
  - https://www.azerothcore.org/doxygen/d8/da7/classMMAP_1_1Config.html
- AzerothCore current tile override surface:
  - https://www.azerothcore.org/doxygen/d3/dbc/structMMAP_1_1Config_1_1TileOverride.html
- TrinityCore discussion documenting accepted map-local overrides:
  - https://github.com/TrinityCore/TrinityCore/discussions/26868
- vmangos current `TileWorker.cpp`:
  - https://github.com/vmangos/core/blob/development/contrib/mmap/src/TileWorker.cpp
