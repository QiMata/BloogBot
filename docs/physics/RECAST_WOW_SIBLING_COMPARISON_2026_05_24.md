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

### Code surface added

WWoW now has a local contour-resimplify surface in
`tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:

- `prePolyResimplifyAnchorSupportMaxError`
- `prePolyResimplifyAnchorSupportMaxEdgeLen`
- `ResimplifyRawAnchorSupportContours(...)`

The implementation intentionally reuses upstream Recast contour logic instead of
inventing a custom simplifier.

### Exact branch and commands

- Build:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
- Bake:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13.json'`
- Focused tests:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_mse13_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
- Full tests:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_mse13_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`

### Observed result

- artifact dir:
  - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse13_v1-20260524T151711Z/`
- saved tile hash:
  - `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`
- focused tests:
  - `7/7` passed
- full `CriticalWalkLegs`:
  - `17/23` passed
  - same six remaining reds

Important negative result:

- bake log showed:
  - `[CONTOUR-ANCHOR-RAW] restored raw vertices on 1 support contour(s)`
  - `[CONTOUR-ANCHOR-PRESERVE] preserved 207 border vertex(s) across 1 contour(s)`
- bake log did **not** show any
  `[CONTOUR-ANCHOR-RESIMPLIFY] anchor=... verts=A->B ...` replacement line
- practical read:
  - this local `maxError=1.3` branch did not find a better intermediate
    contour than the already-restored raw contour
  - it therefore serialized to the **same hash as the earlier raw+preserve
    branch**, not a new winning geometry branch

## Current Stage Authority After The Newest Branch

Use the newest manifest files as the current proof surface, even if older same-
day notes describe an earlier stage interpretation.

### Experimental branch `52D99...`

From
`tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_mse13_v1-20260524T151711Z/analysis/map0012940_anchor_stage_summary.json`:

- `1523.800,-4425.900,17.100`
  - `finalDetour / lower_competitor_dominant`
- `1522.500,-4424.100,17.000`
  - `finalDetour / support_footprint_missed_anchor`
- `1364.867,-4374.000,26.109`
  - `finalDetour / winner_component_trapped`
- `1381.300,-4370.600,26.000`
  - routeable at final Detour
- `1355.600,-4522.300,33.100`
  - routeable at final Detour

### Restored baseline `A01DEE...`

From
`tmp/bake-sweeps/og_4029_restore_after_resimplify_iteration_20260524-20260524T152623Z/analysis/map0012940_anchor_stage_summary.json`:

- `1523.800,-4425.900,17.100`
  - still `finalDetour / lower_competitor_dominant`
- `1364.867,-4374.000,26.109`
  - still `finalDetour / winner_component_trapped`

Interpretation:

- the new local resimplify branch did **not** move the `1523.8` failure deeper
  than the current baseline
- the clean remaining difference is not "which stage first loses `1523.8`"
  anymore
- the next useful branch needs to change the final-basin competition itself, or
  explicitly preserve a better intermediate contour than the one upstream
  simplification currently produces

Historical note:

- earlier same-day notes that described `1523.8` as moving from
  `polymesh / upper_support_lost` to `finalDetour / lower_competitor_dominant`
  were still useful for identifying the contour surface
- for **current** follow-up work, use the newest manifest files above as
  authority

## Recommended Next Moves

1. Keep the new local contour-resimplify code surface in tree, but do not
   re-run the exact `maxError=1.3` branch blindly. Its no-op replacement log +
   repeated hash already make it a useful negative result.
2. Next contour branch should explicitly target a mid-complexity contour for
   the `1523.8` support band, not just "raw contour plus normal simplify."
3. In parallel, prefer source-support / lower-competitor classification work
   over any new global knob sweep.
4. Only test sibling-style `ch` or `walkableRadius=0` overrides if the stage
   manifest proves the failure is truly vertical aliasing or erosion. Do not
   treat those knobs as generic cleanup.

## Restore State

At the end of this loop, `D:\wwow-bot\test-data\mmaps\0012940.mmtile` was
restored to the stable baseline:

- restore command:
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_resimplify_iteration_20260524' -DataDir 'D:\wwow-bot\test-data'`
- restore artifact:
  - `tmp/bake-sweeps/og_4029_restore_after_resimplify_iteration_20260524-20260524T152623Z/`
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
