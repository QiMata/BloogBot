# PathfindingService Tasks

> **FREEZE NOTICE — 2026-05-06.** This service is in an architectural freeze
> while the [pathfinding overhaul](../../docs/physics/PATHFINDING_OVERHAUL.md)
> moves authority for routes from the managed repair pipeline to the in-tree
> [tools/MmapGen](../../tools/MmapGen/) navmesh generator.
>
> **Don't:** add new repair phases to `Repository/Navigation.cs`, new route-pack
> seeds, new `RouteResultCache` keys, new `PathAffordanceClassifier` categories,
> or new per-spot `LongPathingRouteTests`. Don't extend BotRunner
> `BoardingPosition` / `ApproachPosition` / `WalkLegTransportArrivalRadius`
> hand-tuned constants.
>
> **Do:** stability fixes only. New navigation work (transports, GO bake,
> capsule rules) belongs in `tools/MmapGen/` and gets the runtime fix for free
> via tile regeneration. See the overhaul doc for the full freeze contract +
> exit criteria per phase.
>
> The Codex-handoff zeppelin/lip slice (TransportWaitingLogic /
> TravelTask boarding-position fudging) is a symptom; do not extend it. Phase
> 3 of the overhaul replaces it with a single `dtOffMeshConnection` baked at
> generation time.

## Scope
- Directory: `Services/PathfindingService`
- Project: `PathfindingService.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: deterministic path-response contracts, object-aware diagnostics, and dockerized runtime packaging.

## Execution Rules
1. Keep runtime routing on native path output unless a task explicitly changes the contract.
2. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
3. Every pathing or packaging slice must end with at least one focused build/test command in `Session Handoff`.
4. Archive completed items to `Services/PathfindingService/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.
6. **(2026-05-06 freeze)** Honor the freeze contract above. New work outside the freeze surface goes in `tools/MmapGen/` and references the overhaul doc.

## Pathfinding Overhaul (active)

10. `PFS-OVERHAUL-001` Phase 1 - Freeze + MmapGen scaffold
- [x] Architectural decision documented in `docs/physics/PATHFINDING_OVERHAUL.md`.
- [x] `tools/MmapGen/` staged with vmangos sources, unlinked from upstream Github.
- [x] Top-level `tools/MmapGen/CMakeLists.txt` + scaffold target wired into root `CMakeLists.txt`.
- [x] `tools/MmapGen/AGENTS.md`, `CLAUDE.md`, `README.md`, `NOTICE.md`, `offmesh.txt`, `config.json`, `build-mmapgen.ps1` written.
- [x] `docs/physics/MMAP_FORMAT.md` (loader contract spec) and `docs/physics/CPP_PATHFINDING_SERVICE_PLAN.md` (Phase 6 native rewrite + dtCrowd plan) written.
- [x] Freeze annotation added to this file and to `docs/TASKS.md`.

11. `PFS-OVERHAUL-002` Phase 2 - MmapGen build bring-up
- [x] Wire actual targets in `tools/MmapGen/CMakeLists.txt`: Recast, Detour (with `DT_POLYREF64`), `zlib_mmap`, `g3dlite_mmap` (upstream parity source list), minimal `shared_mmap` + `framework_mmap`, `vmap` (excluding `GameObjectModel.cpp` + `DynamicTree.cpp`), `MmapGen` exe. Build via Ninja under VS 18 (2026) Community / MSVC v145 (vcvarsall amd64). `/MD` CRT to match `Exports/Navigation`.
- [x] `MmapGen.exe` builds (`tools/MmapGen/build/MmapGen.exe`, 506,368 bytes). Args parser exercised; `--tile X,Y` and `--silent` work; `--configInputPath` and `--offMeshInput` parsed correctly. `--help` is not a recognized flag (vmangos generator never had one); pass an invalid arg with `--silent` to print usage and exit.
- [x] Single-tile regen byte-identical (mod timestamps) to the external `D:/MaNGOS/source/bin/MoveMapGenerator.exe` for the same inputs (`--tile 29,40`, `tools/MmapGen/config.json` flat schema, `tools/MmapGen/offmesh.txt` with the OG-UC zeppelin entry). SHA256 `E2D0ED93D12C644E943CAE0430B6A1CB81A8AE474DD61C605D25F01A810EE5E3` for both `0014029.mmtile` (1,450,600 bytes), and SHA256 `3D468B7296BB1A7D8A6F5C54D67075157D24EE481564A945EF25BE3381648CA2` for `001.mmap` (28 bytes, identical to the historical baseline).
- [x] `tools/NavDataAudit` core tile-format and capsule gates green for the regenerated tile (Detour wrapper magic/version/size, payload version, `walkableRadius=1.0247`, `walkableHeight=2.6250`, config map agentRadius/agentHeight pass). The audit's `[GO] map=1 tile=29,40: baked ...` build-log marker remains a known phase-4 instrumentation gap; the existing externally-built `map1_build.log` only emits `[GO] ... loaded ...` lines, so this gate cannot be met without further generator instrumentation. Tracked under PFS-OVERHAUL-004.

  **WWoW divergence applied during Phase 2** (each documented in source comments):
  - `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` line ~462: read `agentRadius` and `agentHeight` from `getTileConfig(...)` so the per-map config can drive the Tauren capsule. Upstream vmangos hardcodes `0.2 / 1.5` which made the bake mismatch the existing `D:/MaNGOS/data/` tile capsule; the externally-patched `MoveMapGenerator.exe` already has this read.
  - `tools/MmapGen/src/stubs/utf8printf_stub.cpp` (new file): minimal printf-forwarding stubs so `shared_mmap.lib` resolves without pulling in `Util.cpp`'s IO/utf8cpp/MersenneTwister fan-out. ASCII-only console output; swap for the real implementation if MmapGen ever needs localized strings.
  - `tools/MmapGen/dep/windows/include/zlib/{zlib.h,zconf.h}`: copied from `tmp/reference/vmangos-core/dep/windows/include/zlib/` (vmangos's `dep/src/zlib/CMakeLists.txt` already references this exact path). Required because g3dlite's `BinaryInput.cpp` / `BinaryOutput.cpp` / `Crypto.cpp` link against zlib.
  - `tools/MmapGen/build-mmapgen.ps1`: Ninja generator under VS 18 (2026) Community vcvarsall.bat amd64 environment (CMake 4.1 doesn't yet have a `Visual Studio 18 2026` generator); pre-prepends VS Installer dir to PATH and tolerates harmless vcvarsall stderr; uses a temp .cmd shim to avoid PowerShell quoting fragility around the `cmd /c "vcvarsall && set"` invocation.

  **Evidence:**
  - Build log: `tmp/test-runtime/mmapgen-build/20260506T181311Z.log` (104/104 ninja steps green, 506,368-byte MmapGen.exe).
  - Rebuild after Tauren patch: `tmp/test-runtime/mmapgen-build/rebuild-after-tauren-patch-20260506T182834Z.log` (incremental, 2/2 steps green).
  - Tile parity backup directory: `D:/MaNGOS/data/mmaps/phase2-parity-backup-20260506T181653Z/` containing `0014029.mmapgen-tauren.mmtile`, `0014029.external-tauren.mmtile`, `001.mmapgen.mmap`, `001.external.mmap`, `001.original.mmap` plus the original `0014029.mmtile` and `0012940.mmtile`.
  - NavDataAudit pass log + manifest: `tmp/test-runtime/results-navigation/phase2_parity_tauren_20260506T182943Z.audit.log` + `phase2_parity_tauren_20260506T182943Z.json`.
  - Generator regen log: `tmp/test-runtime/mmapgen-build/regen-mmapgen-tile-1-29-40-20260506T181716Z.log`.

  **Next command:** Phase 3 - validate that offmesh.txt's `1 29,40 ...` seed lives in the right MaNGOS tile coordinate frame (the seed treats tile coords as `tileX,tileY` per offmesh.txt grammar; cross-check with the runtime via a single-tile load test before regenerating the OG/UC dock cluster).

12. `PFS-OVERHAUL-003` Phase 3 - OG↔UC zeppelin off-mesh pilot
- [x] Validate the OG↔UC zeppelin off-mesh entry seeded in `tools/MmapGen/offmesh.txt` against MmapGen's tile-coord frame. Tile (`tileX=29, tileY=40`) world bounds (Recast X 1066.67..1600.0, Recast Z -4800..-4266.67) contain both off-mesh endpoints (1338.10, -4646.00, 51.60) and (1320.14, -4653.16, 53.89), so the seed lands in the right tile. Note: `docs/physics/MMAP_FORMAT.md` §3 currently labels this tile `(tileX=40, tileY=29)` because the doc's `tileX = floor((maxX - worldY)/GRID_SIZE)` formula uses MaNGOS-side axis conventions; MmapGen's runtime convention (per `MapBuilder::getTileBounds` line 418-421 with `(32 - tileX) * GRID_SIZE` indexing Recast X) matches the offmesh.txt seed and the `mmaps/<map><tileY:02d><tileX:02d>.mmtile` filename order. The doc would benefit from a follow-up clarification, but the seed itself is correct.
- [x] Regenerate map 1 tiles 28-30 / 39-41 (Orgrimmar dock cluster, 9 tiles) with the OG↔UC off-mesh entry baked into tile (29,40). NavDataAudit capsule + format gates green for all 9 tiles. Nav-data signature updated; remaining audit failure is the `[GO] map=1 tile=X,Y: baked ...` build-log marker which is custom Phase-4 instrumentation that even the existing externally-built `map1_build.log` does not emit (it has `loaded` markers but not `baked` markers).
- [x] Regenerate map 0 tiles 27-30 / 30-32 (Undercity arrival cluster, 12 tiles). NavDataAudit capsule + format gates green for all 12 tiles. Same Phase-4 GO-bake instrumentation gap. Note: `tools/MmapGen/offmesh.txt` does not yet have a Tirisfal-side entry for the UC zeppelin tower disembark, so this cluster regen is purely a capsule + lineage refresh; the actual UC arrival off-mesh authoring belongs to the next iteration.
- [x] Add `Tests/PathfindingService.Tests/LongPathingRouteTests.OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` proving the returned `dtPath` includes a `DT_OFFMESH_CON_BIDIR` polygon, with no managed repair invoked. Test parses the on-disk `.mmtile` directly (managed binary parse of the wrapper + `dtMeshHeader` + `dtOffMeshConnection` table — no new P/Invoke surface), then issues `_navigation.CalculateValidatedPath(...)` and snapshots all six `NavigationPerformanceMetrics` repair counters (LongLOS / StaticWall / SteepAffordance / LocalPhysicsLayer / SegmentValidation / DynamicOverlay). **Test green (2026-05-06)** at `tmp/test-runtime/results-pathfinding/phase3_offmesh_pilot_walkable_snap.trx` after two compounding fixes: (1) the WWoW divergence in `TerrainBuilder.cpp::loadOffMeshConnections` (axis swap), (2) snapping the offmesh.txt seeds from the screenshot-derived z=51.60/53.89 (which fell below the walkable mesh floor and were dropped by Detour's height check) to the walkable-mesh-aligned z=96.29/98.54 anchors that match existing detail verts in tile (1, 29, 40). PROOF A asserts `offMeshConCount=2` with both connections bidirectional in the regenerated tile; PROOF B asserts a 9-corner native path between the anchors with all six repair counters staying at zero.
- [~] Stand up Docker `wwow-pathfinding` against the regenerated data (rebuild + redeploy). Then rerun `LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin` and prove the zeppelin leg is a single Detour query, not a TransportWaitingLogic / TravelTask hand-off dance. Use `WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1` so the test reuses the freshly-built service container. **Docker side done 2026-05-06**: `docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding` → image `world-of-warcraft-wwow-pathfinding:latest` (manifest sha256:f8133a0ecbb28f72fc2437001675f3463c027bc85205fbed70e2c2755e6a996e); `up -d wwow-pathfinding` → `IsReady=true`, `StatusMessage=Ready - navigation initialized`. **Live test side blocked on BotRunner**: The live test failed at the OG↔UC boarding gap with bot final pos (1338.1, -4646.0, 51.6) — the OLD screenshot-derived anchor that `BotRunner.TransportWaitingLogic` / `TravelTask` still target via hardcoded boarding/approach constants. The new off-mesh edge in the mesh anchors the boarding at z=96.29/98.54, which BotRunner does not yet route to. This is the surface the freeze contract has flagged for Phase 5 retirement. Phase 3's mesh-side claim is fully proven; the live end-to-end requires the BotRunner constants to follow the mesh's new authority. See "Live test outcome" in the Session Handoff entry for the full snapshot + screenshot path. Marked `[~]` (partial) rather than `[x]` because the test didn't pass for "a reason other than the OG↔UC boarding gap" per the user's Phase 3 acceptance criteria; instead it failed AT that gap, with the mesh side fixed but BotRunner side still hand-tuned. Trx: `tmp/test-runtime/results-live/phase3_live_offmesh_after_walkable_snap.trx`.

  **WWoW divergence: TerrainBuilder offmesh axis swap (fixed 2026-05-06).** Upstream vmangos `TerrainBuilder.cpp::loadOffMeshConnections` lines 1058-1064 emits offmesh.txt's WoW (X, Y, Z) coords into `meshData.offMeshConnections` with the swap (Y, Z, X), but every other consumer in MmapGen (`solidVerts`/`liquidVerts` swap at lines 205-207; `MapBuilder::getTileBounds` axis 0 = WoW X via `(32 - tileX) * GRID_SIZE`, axis 2 = WoW Y via `(32 - tileY) * GRID_SIZE`) uses the consistent (X, Z, Y) swap. With the upstream swap leaking, `dtCreateNavMeshData::classifyOffMeshPoint` compared `pt[0] = WoW Y` against `bmin/bmax[0] = WoW X bounds` — a different axis — and silently dropped every off-mesh entry. Patched in `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp` (BEGIN/END WWoW divergence block) to use the (X, Z, Y) swap that matches the rest of the generator. Verified by `maxLinkCount` delta on regenerated tile (1, 29, 40): pre-fix `maxLinkCount=31856 / offMeshConCount=0`, post-fix-only `maxLinkCount=31860` (+4 = 2 connections × 2 endpoints passing the X/Z classifier, but both starts re-zeroed by the height check at z=51-54 < hmin=72). Then after the seed walkable-snap to z=96.29/98.54: `maxLinkCount=31864 / offMeshConCount=2` with both `flags=0x01` (DT_OFFMESH_CON_BIDIR). Tile size 1450600 → 1450912 bytes (+312 from the 2 extra polys + 4 verts + 2 detail meshes + 4 BV nodes + 2 off-mesh connections + extra link slots).

  **Anchor walkable-snap (2026-05-06).** The offmesh.txt OG↔UC zeppelin seed was originally at the screenshot-derived approach (1338.10, -4646.00, 51.60) and deck (1320.142944, -4653.158691, 53.891945) anchors. These are below the walkable mesh floor in tile (1, 29, 40) (`polyMeshDetail` Y range [72.29, 279.29] per a managed `.mmtile` binary parse), and Detour's `dtCreateNavMeshData::classifyOffMeshPoint` height check (`DetourNavMeshBuilder.cpp:344-348`) silently drops any off-mesh start whose Z lies outside `[hmin - walkableClimb, hmax + walkableClimb] = [70.49, 281.09]`. The original anchors appear to have been the bot's recorded ground positions when stalled, not the actual walkable upper-platform / gangplank elevations. Snapped to the closest existing walkable detail verts at (1330.66, -4656.03, 96.29) and (1315.33, -4650.00, 98.54) — XY distance ~16 units (plausible OG zeppelin gangplank length) and both directly on the polyMesh, so the bake survives the height check. Phase 4 follow-up: validate these anchors in-game via screenshot evidence and adjust if the actual gangplank end is at a different XY/Z; the current anchors are the best fit to the existing walkable mesh.

  **Phase 3 evidence so far (2026-05-06):**
  - Kalimdor regen log: `tmp/test-runtime/mmapgen-build/phase3-kalimdor-regen-20260506T183358Z.log` (9 tiles, ~4 min total at single-thread).
  - Kalimdor backup: `D:/MaNGOS/data/mmaps/phase3-kalimdor-cluster-backup-20260506T183358Z/` (9 `.mmtile.original` files).
  - Kalimdor NavDataAudit manifest: `tmp/test-runtime/results-navigation/phase3_kalimdor_cluster_20260506T183824Z.json` — nav-data signature `B7D292665CEC3284142F06F8356FB8CE99A5482F9B58EEF492A6933B58A837BD`.
  - EK regen log: `tmp/test-runtime/mmapgen-build/phase3-ek-regen-20260506T183918Z.log` (12 tiles, ~3 min total).
  - EK backup: `D:/MaNGOS/data/mmaps/phase3-ek-cluster-backup-20260506T183918Z/`.
  - EK NavDataAudit manifest: `tmp/test-runtime/results-navigation/phase3_ek_cluster_20260506T184202Z.json`.

  **Next command:** Author the off-mesh proof test. The minimum viable test path:
  ```
  $env:WWOW_DATA_DIR='D:\MaNGOS\data'
  dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink" --logger "trx;LogFileName=phase3_offmesh_pilot.trx" --results-directory tmp/test-runtime/results-pathfinding
  ```
  Followed by Docker rebuild + the live `CrossroadsToUndercity_UsesFlightAndZeppelin` test.

13. `PFS-OVERHAUL-004` Phase 4 - Transport pass + GO bake fidelity sweep
- [ ] **Off-mesh shortcut routing (replaces the prior "OG dock-level walkability investigation" item).** The walkable mesh in tile (1, 29, 40) DOES extend to z=6-23 along the dock (proven by the live test's walk-nav trace going through that elevation range), so the dock IS walkable — my earlier conclusion about a z=72 elevation floor came from a polyMeshDetail-only sample that didn't include all polyMesh verts. The Phase 3 off-mesh edges from upper platform to ApproachPosition (`(1330.66,-4656.03,96.29)` ↔ `(1338.10,-4646.00,51.60)`) are baked but functionally unused at runtime: Detour finds a natural walkable corridor through OG city's sea-level dock that's apparently shorter (or at least preferred) over my off-mesh shortcut. The 470-waypoint walk path takes 12+ minutes which is longer than the zeppelin's dock window. To unblock the live test from the mesh side, either (a) tune the off-mesh radius / cost / area type so Detour prefers the upper-platform shortcut (the path "upper platform → off-mesh → ApproachPosition" is geometrically much shorter than the sea-level walk), (b) add additional off-mesh anchors at intermediate flight-master-to-zeppelin-tower-top positions so the upper route is preferred, or (c) tighten capsule rules so the sea-level dock walk is no longer reachable for Tauren (forcing the upper route). All three are mesh-level changes consistent with the freeze contract. Use the existing `LongPathingRouteTests.OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` PROOF A + a new PROOF C ("path query from flight-master XY to ApproachPosition has fewer than N corners" or "path traverses the upper platform poly") as gates.
- [ ] **Add `[GO] map=<id> tile=<x>,<y>: baked ...` build-log marker emission** to `MapBuilder::buildGameObject(...)` (or wherever the GO bake happens). The format is in `tools/NavDataAudit`'s manifest (`gameObjectBake.requiredLogMarker`); the existing externally-built logs only emit `loaded` markers and the audit currently fails this gate for every regenerated tile (PFS-OVERHAUL-002 / -003 evidence). With this in place, Phase 3 cluster regen audits go fully green.
- [ ] Author off-mesh entries for remaining classic transports (Grom'gol↔Orgrimmar zeppelin, UC↔Tirisfal elevator, Booty Bay↔Ratchet boat, Menethil↔Theramore, Menethil↔Auberdine, etc.). Coordinates from `TransportData.cs` and screenshot evidence.
- [ ] Author Tirisfal-side disembark off-mesh entries in `tools/MmapGen/offmesh.txt` for the OG↔UC zeppelin tower on map 0 (currently no map-0 entry; the prior session noted this gap). Pair with the Phase 3 regen of map 0 tiles 27-30 / 30-32 once dock walkability is fixed on both sides.
- [ ] Audit `MapBuilder::buildGameObject(...)` GO bake fidelity vs `gameobject_spawns.json`. Regenerate maps 0 and 1 in full.
- [ ] Replace per-spot `LongPathingRouteTests` with capsule-walkability property tests.
- [ ] **Doc fix:** `docs/physics/MMAP_FORMAT.md` §3 currently uses MaNGOS-side axis conventions (`tileX = floor((maxX - worldY)/GRID_SIZE)`, "yes, swap") while MmapGen's runtime conventions match the offmesh.txt grammar and the `MapBuilder::getTileBounds`. The doc and source disagreed silently for the entire freeze period until 2026-05-06. Rewrite §3 to match the source; cite memory/`project_pathfinding_tile_coords.md`.

14. `PFS-OVERHAUL-005` Phase 5 - Managed repair retirement
- [ ] Disable `Navigation.cs` repair phases one-by-one behind a feature flag; delete each after it proves green.
- [ ] Delete `StaticRoutePackCache.cs`, `PathAffordanceClassifier.cs`, repair candidate enumeration. Target: `Navigation.cs` < 500 LOC.
- [ ] Delete `RepairLocalPhysicsReachabilityBreaks` and per-segment `PhysicsStepV2` interop in the query path.
- [ ] Delete BotRunner `TransportWaitingLogic` boarding-position constants (becomes "follow off-mesh link").
- [ ] Reduce `LongPathingRouteTests` to property-test set.

15. `PFS-OVERHAUL-006` Phase 6 - Native PathfindingService rewrite + dtCrowd
- [ ] See `docs/physics/CPP_PATHFINDING_SERVICE_PLAN.md` for full breakdown (6.0 wire parity, 6.1 native pathfinding parity, 6.2 dtTileCache obstacles, 6.3 selective dtCrowd, 6.4 sharding).
- [ ] Target: 3000-bot p99 path query <50 ms; .NET `Services/PathfindingService` archived.

## Active Priorities
1. `PFS-OBJ-001` Object-aware routing contract
- [x] Close the caller-adoption slice so higher-level BotRunner navigation consumes `GetPathResult(...)` and reacts to service-side blocked reasons instead of relying only on corners-only responses.

2. `PFS-LIVE-001` Live integration sweep
- [x] Run the full `LiveValidation` namespace against the current split-service Linux stack and capture the first complete pass/fail matrix without interruption.

3. `PFS-DOCKER-001` Containerized runtime validation
- [x] Split Docker topology so `PathfindingService` and `SceneDataService` run as separate Windows services with BG endpoint wiring.
- [x] Validate split Linux containers against mounted `WWOW_DATA_DIR` nav/scene data and capture readiness evidence.

4. `PFS-ROUTEPACK-001` Generated static route packs
- [x] Design and prototype PathfindingService-owned route-pack lookup for
  static long legs. Route packs must be generated from Navigation.dll/Detour
  output, validated with race/gender capsule metadata, keyed by nav-data and
  route-algorithm signatures, and bypassed when dynamic overlays or corridor
  validation make the cached route unsafe.

5. `PFS-ROUTEPACK-002` Lower-incline live recovery pack
- [x] Extend the generated route-pack/warmup strategy so the live lower-layer
  Orgrimmar recovery request from `(1363.9,-4377.8,26.1)` toward the
  Orgrimmar -> Undercity gangplank can be answered without falling back to a
  slow or hanging native service request. The prior target was
  `(1341.0,-4638.6,53.5)`; the current screenshot-derived dock target is
  `(1320.142944,-4653.158691,53.891945)`. Keep this as generated Navigation
  output or generic recurring-recovery warmup, not a production detour script.
  Deterministic on-demand route-pack coverage, direct upper-deck
  local-physics recovery, and the fresh live Crossroads -> Undercity rerun now
  prove the route reaches the Orgrimmar dock/zeppelin area without the prior
  lower-incline slow native fallback.

6. `PFS-CACHE-001` PathfindingService-owned route result caching
- [x] Add a general static-overlay route result cache in PathfindingService
  with fuzzy quantized request keys, nav-data and route-algorithm signatures,
  in-flight request coalescing, short negative TTLs, conservative
  dynamic-overlay bypass, and cache/coalescing metrics. Route packs and native
  validated paths still return through the normal path contract.

7. `PFS-METRICS-001` Navigation performance instrumentation
- [x] Add service-owned metrics/logs for path resolver attempts, native
  `FindPathForAgent`, corridor query timing, managed validation timing, repair
  counters, blocked/no-path outcomes, and slow request counts without adding
  noisy per-segment logs.

8. `PFS-SOCKET-LOG-001` Clean EOF logging
- [x] Treat a clean client close after a complete protobuf request/response as
  normal socket lifecycle, while preserving warnings for truncated mid-frame
  payloads.

9. `PFS-ROUTEPACK-003` Bounded route-pack generation
- [x] Add a per-seed route-pack generation timeout so a slow native
  Navigation-backed pack becomes a fast unavailable pack instead of blocking
  service startup or deterministic tests for a full session timeout.

## Session Handoff
- Last updated: 2026-05-07 (Phase 5.3.5: NPC-anchor ApproachPosition + walk-leg radius fix + un-gated nav predicate + focused sub-test landed; user reframed gap as corner-cutting / Facing-based completion + 11-phase test breakdown)

### 2026-05-07 — Phase 5.3.5 outcome (NPC-anchor + corner-cutting investigation; user reframed for Phase 5.3.6)
- **User-driven reframings landed this session**:
  1. **NPC-anchor ApproachPosition**: queried MaNGOS DB and identified
     **Frezza** (Zeppelin Master, NPC 9564) at `(1331.11, -4649.45, 53.6269)`
     on map 1 — same Z tier as `BoardingPosition` z=53.89, NOT the prior
     wrong-tier z=51.6 city ground point. Updated
     [Exports/BotRunner/Movement/TransportData.cs:182](../../Exports/BotRunner/Movement/TransportData.cs)
     `OG.ApproachPosition` to Frezza's coords.
  2. **Walk-leg arrival radius scales with BoardStop.BoardingRadius when flag set**:
     prior `WalkLegTransportArrivalRadius=4f` was too tight for the new geometry
     (bot's natural ramp-top arrival is ~7.78y from Frezza, within
     `BoardingRadius=12f` but outside 4y). `TravelTask.cs::GetWalkLegArrivalRadius`
     now reads `nextLeg.BoardStop.BoardingRadius` when
     `IsNativeOffMeshBoardingEnabled()`.
  3. **Un-gated `ShouldNavigateToConfiguredScheduledTransportBoarding`**: this
     predicate uses `NavigationRoutePolicy.LongTravel` (no corner-cutting).
     The natural cascade fallback (`TryNavigateToward(waypoint,
     allowDirectFallback: true)`) uses `Standard` policy with
     `EnableDynamicProbeSkipping=true` — auto-skips waypoints. Un-gating
     this predicate routes the boarding-phase nav through tight
     LongTravel-policy Detour for strict corner traversal.
- **Pre-flight smoke tests** in
  [Tests/PathfindingService.Tests/LongPathingRouteTests.cs](../../Tests/PathfindingService.Tests/LongPathingRouteTests.cs):
  `OrgrimmarFlightMasterToFrezzaSpawn_PathExists` (278 polys GREEN — wooden
  ramp UP IS bake-walkable) and `OrgrimmarFrezzaSpawnToBoardingPosition_PathExists`
  (5 polys GREEN — same-deck short hop). Test fixtures updated
  (CrossMapRouterTests, TransportWaitingLogicTests, TravelTaskTests); 87/87
  unit tests pass.
- **Live test (`phase5_3_5_live_v3.trx`)**: FAILED at 10m6s. Bot stalled at
  `(1337.6,-4650.7,50.5)` — same coord as prior runs. Walk-leg never completed.
  Failure: "the bot missed boarding before the transport left."
- **User reframed the gap (load-bearing for next session)**: "Tauren stops
  going up the ramp way early because it's auto-completing waypoints that it
  shouldn't. We might always want to use 'Facing' in determining if we can
  call a waypoint 'done'." The corner-cutting is in
  [Exports/BotRunner/Movement/NavigationPath.cs::AdvanceReachableWaypoints](../../Exports/BotRunner/Movement/NavigationPath.cs)
  — specifically the look-ahead skip loop (lines 1065-1092) and
  `TryLosSkipAhead` helper (lines 1094-1096). Even with LongTravel disabling
  `EnableDynamicProbeSkipping`, the in-loop `CanTreatWaypointAsReached` fires
  on radius-only check.
- **User test-breakdown directive**: decompose monolithic
  `CrossroadsToUndercity_UsesFlightAndZeppelin` into 11 phase-isolated
  sub-tests:
  1. Take flight master ✓ (already covered)
  2. Detect when landed ✓ (already covered)
  3. Run down tower ✓ (works)
  4. Cross OG (works; could be smoother — defer)
  5. **Climb zeppelin tower** ❌ FAILING — top priority for Phase 5.3.6
  6. Board zeppelin ❓ (downstream of #5)
  7. Ride zeppelin ❓ (downstream of #6)
  8. Deplane zeppelin ❓
  9. Path tower→UC ❓
  10. Use elevators ❓
  11. Step off elevator + path to final ❓
- **Lead's deliverables this session**:
  1. **`ClimbOrgrimmarZeppelinTowerRampToFrezza` sub-test** in
     `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs` — gated on
     `WWOW_OG_RAMP_CLIMB_TEST=1`. Teleports bot to OG flight master tower top,
     dispatches TravelTo UC, asserts arrival within 12y of Frezza, 20s tight
     stuck-guard + 90s test budget. **First implemented sub-test in the
     11-phase breakdown.**
  2. **Fail-fast `boardingStuckGuard`** added to the boarding-evidence poll
     loop in the full live test — `SnapshotStallGuard` with 30s timeout, 1.5y
     movement threshold, fires `FailWithScreenshot` on stall. Should reduce
     debug cycles from 7+ minutes to ~1 minute on the boarding-stall failure
     mode.
- **Sub-test outcome (`phase5_3_5_climb_ramp.trx`)**: FAILED at 90s budget.
  **NEW failure mode discovered**: bot teleported to (1677,-4315,62),
  descended OG flight master tower normally (z=62→7), then walked
  **WEST/NORTHWEST** instead of SOUTHWEST. Final position
  `(1365,-4382,26)` is **267y NORTH** and 27y BELOW Frezza. The pre-flight
  said the 278-poly path exists, but the runtime path the bot follows goes
  the wrong direction. Possible causes:
  - LongTravel's `RequireVerticalWaypointArrival=true` may be making some
    corners unreachable when the bot's Z drops below the corner's Z
    minus `WAYPOINT_VERTICAL_REACH_TOLERANCE=1.25f`, causing repeated re-plans
    that push the bot off-route.
  - The smooth-path corners may pass through OG city polygons whose
    elevations differ subtly from the bot's actual Z, causing
    `CanTreatWaypointAsReached` to false-negative repeatedly.
  - The path may be correct but the bot's smoothing follows it
    imprecisely (corner-cutting is the most-mentioned culprit but doesn't
    fully explain a 267y NORTH deviation).

### Next session (Phase 5.3.6 — Facing-based waypoint completion + corner XYZ inspection)
- **Spawn Plan agent FIRST** before touching `NavigationPath.cs`. Design:
  - Facing-based corner completion: a waypoint is only "done" when bot is
    BOTH within `WAYPOINT_REACH_DISTANCE`/`effectiveRadius` AND facing
    roughly toward the next waypoint (e.g. heading-to-next-corner angle
    within ±60° tolerance). Modify `CanTreatWaypointAsReached` and the
    look-ahead skip loop in `AdvanceReachableWaypoints`.
  - Add a P/Invoke wrapper for the existing `FindPathCorridor` C export
    (or add a new `FindPathCornersForAgent` export) so the corner XYZ
    list Detour returns can be inspected from PathfindingService.Tests.
    Without this, debugging "why is the bot going wrong direction"
    requires inspecting Detour's actual path mathematically.
- **Validation**: re-run `ClimbOrgrimmarZeppelinTowerRampToFrezza` with the
  new corner-completion logic. Stuck-guard fires fast on failure.
  90s budget is plenty for fast iteration.
- **Then**: implement remaining sub-tests (#6 Board, #7 Ride, #8 Deplane,
  etc.) one at a time as each phase becomes diagnosable.
- **Don't run the monolithic `CrossroadsToUndercity_UsesFlightAndZeppelin`
  for diagnostic purposes** — it's now too coarse-grained. Reserve it for
  end-to-end regression once all sub-phases are green.

### 2026-05-07 — Phase 5.3.4 outcome (BotRunner Boarding-phase Detour gate landed; live test reveals next layer)

### 2026-05-07 — Phase 5.3.4 outcome (BotRunner Boarding-phase Detour gate landed; live test reveals next layer)
- **Phase 5.3.4 fix landed (one-line)**:
  [Exports/BotRunner/Tasks/Travel/TravelTask.cs:1065-1068](../../Exports/BotRunner/Tasks/Travel/TravelTask.cs)
  — `ShouldDirectBoardScheduledTransport` now gates on
  `!TransportWaitingLogic.IsNativeOffMeshBoardingEnabled()`. Symmetric with
  the four prior Phase 5 predicate gates. With flag set, `phase==Boarding`
  no longer routes to raw `MoveToward(BoardingPosition)`; the cascade
  falls through to predicate 6 `TryNavigateToward(BoardingPosition,
  allowDirectFallback: true)` (Detour-driven navigation with direct
  fallback, the standard navigator).
- **Two pre-flight smoke tests landed in
  [Tests/PathfindingService.Tests/LongPathingRouteTests.cs](../../Tests/PathfindingService.Tests/LongPathingRouteTests.cs)**:
  - `OrgrimmarFlightMasterToBoardingPosition_PathIncludesOffMeshConnection`
    (long-hop, flight master → BoardingPosition) — **RED** with
    `polyCount=281 offMeshPolyCount=0`; Detour finds a long ground-only
    path that bypasses offmesh edge #4. **MARKED `[Fact(Skip=...)]`** with
    explanatory message — kept as durable diagnostic. This RED outcome
    KILLED the original Plan-agent option (i) walk-endpoint shift before
    any BotRunner code was touched.
  - `OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage`
    (short-hop, ApproachPosition → BoardingPosition, the final 18 yards)
    — **GREEN** with `polyCount=11 offMeshPolyCount=0`; Detour finds an
    11-poly ground-only path. This GREEN outcome enabled the simpler
    option (iii) (gate `ShouldDirectBoardScheduledTransport` so the
    Detour-driven navigator runs in Boarding phase).
- **Unit tests**: 87/87 passed
  (`TransportWaitingLogicTests` + `TravelTaskTests` + `CrossMapRouterTests`).
  Trx: `tmp/test-runtime/results-live/phase5_3_4_unit.trx`.
- **Live test outcome**: `tmp/test-runtime/results-live/phase5_3_4_live.trx`
  — `CrossroadsToUndercity_UsesFlightAndZeppelin` with
  `WWOW_OFFMESH_NATIVE_BOARDING=1` + `WWOW_LONG_PATHING_TIMELINE=1`.
  Duration **12m 33s, FAILED** at the boarding-window timeout.
  - **Gate verification (positive)**: `[TRAVEL_TRANSPORT]` waypoint
    switched from `(1338.1,-4646.0,51.6)` (ApproachPosition) to
    `(1320.1,-4653.2,53.9)` (BoardingPosition) once `phase=Boarding`
    began — confirming `ShouldDirectBoardScheduledTransport` was
    correctly suppressed and the cascade fell through to the
    Detour-driven navigator.
  - **Outcome (negative)**: bot moved ~5 yards SOUTH from
    `(1338.1,-4646.0,51.6)` to `(1337.6,-4650.7,50.5)` (NOT southwest
    toward BoardingPosition), Z dropped 51.6→50.5 (slight regression),
    then stalled at the same coordinate as the prior 25-min raw-MoveToward
    stall. `currentSpeed=0`, `transport=0x0`, `isOnTransport=false`.
  - **Final assertion**: `[boarding lost] failure: pos=(1338.0,-4649.6,50.7)
    distToUndercity=4897.7 transport=0x0`.
  - Timeline: `tmp/test-runtime/screenshots/long-pathing/timeline/CrossroadsToUndercity_UsesFlightAndZeppelin/`
    (62 paired PNG+JSON entries, suffix 20260506T231...).
- **Reframed conclusion (load-bearing for next session)**: Detour's 11-poly
  ground path between Approach and Boarding leads through physically-blocked
  geometry. The polygons are Detour-walkable per the bake's walkable rules,
  but the Tauren capsule (radius=0.97, height=2.625) cannot physically
  traverse them — likely the OG zeppelin tower's central pillar/wall ring,
  static collision the bake didn't capture. Same "Detour-walkable but
  bot-physically-blocked" phenomenon as the z=96 phantom polygons, but at
  z=51 instead of z=96. The single-line gate is a real improvement (raw
  MoveToward removed from the boarding path), but the underlying
  physical-walkability issue is unresolved.

### Next session (Phase 5.3.5 — Detour corner inspection + tile bake fix)
- **First**: add a P/Invoke wrapper for the existing `FindPathCorridor` C
  export (or add a new `FindPathCornersForAgent` C export), so the corner
  XYZ list Detour returns can be inspected from PathfindingService.Tests.
  This will identify exactly which polygons in the 11-poly Approach →
  Boarding path are physically blocked. The corner sequence reveals
  whether Detour is routing south (toward water), west (into the tower
  wall), or northwest (around the tower) — the 5y south + 1y down
  movement during the live test suggests a south-routing path that hits
  the dock's south edge or tower base.
- **Second**: based on the corner inspection, decide between:
  - (i) **Bake-time fix**: regenerate tile (1, 29, 40) with stricter
    walkable rules that exclude the non-physically-walkable polygons.
    Likely requires GO bake fidelity work for the zeppelin tower's
    pillar/wall geometry — `MapBuilder::buildGameObject` audit of the
    OG tower GO_TYPE_TRANSPORT or its wooden support gameobjects.
  - (ii) **Authoritative off-mesh override**: add a new offmesh.txt
    entry whose START is at ApproachPosition's XY (z=51.60). But this
    would be dropped at bake time (below the z=70.5 floor). Would
    require lowering the bake-time `walkableClimb` for this tile, which
    affects physics for ALL polys in the tile — non-trivial.
  - (iii) **Detour cost manipulation**: bias the area-cost on the
    11-poly ground path so Detour prefers the existing offmesh edge #4
    (the z=96 phantom-snap-to-z=65 → z=53.89 path that's known
    Detour-traversable). Cleanest if achievable; requires understanding
    Detour's area-type model and how the bake assigns area types to
    these polys.
- **Spawn Plan agent** before any of those — each option has different
  blast radii, and the corner-inspection evidence will inform which is
  right. None are simple.

### 2026-05-07 — Phase 5.3 outcome (FG anchor verification — case (b): live gap identified at BotRunner Boarding phase, not the mesh)

### 2026-05-07 — Phase 5.3 outcome (FG anchor verification — case (b): live gap identified at BotRunner Boarding phase, not the mesh)
- **Phase 5.3.1 FG verification landed**. New test method
  `BotRunner.Tests.LiveValidation.LongPathingTests.OgZeppelinDeckAnchorVerification`
  in [Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs](../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs)
  — gated on `WWOW_OG_DECK_ANCHOR_VERIFY=1`, teleports the configured Tauren-Male
  FG bot through 7 candidate world points around the OG zeppelin tower and writes
  paired PNG+JSON capture records under
  `tmp/test-runtime/screenshots/long-pathing/timeline/OgZeppelinDeckAnchorVerification/`.
  Reuses existing `CaptureTimelineCheckpoint`, `BotTeleportAsync`,
  `RefreshSnapshotsAsync`, `GetSnapshotAsync`, `ResolveTimelineDirectory`,
  `EnvironmentVariableScope` helpers — fully additive, gated on a new env var.
  New const `OgDeckAnchorVerifyEnvVar = "WWOW_OG_DECK_ANCHOR_VERIFY"`.
- **Test outcome**: 1 passed, 44s. Trx
  `tmp/test-runtime/results-live/phase5_anchor_verify.trx`. 7 capture records
  produced (paired PNG + JSON each).
- **Per-anchor /gps-verified findings** (settled position after teleport + 4s
  fall-settle):
  - **C1** target `(1330.66,-4656.03,96.29)` settled `(1330.66,-4656.03,65.65)`
    — bot fell 31 units. The z=96 polygon is bake-walkable per H2d but
    PHANTOM (no real surface; bot snaps down to a real walkable polygon at
    z=65.65). `currentSpeed=0`, `isOnTransport=false` — z=65.65 IS a real
    static walkable surface, but it's BELOW the tile's bake-time off-mesh
    floor (~z=70.5), so it cannot be a START anchor.
  - **C2** target `(1315.33,-4650.00,98.54)` — bot landed inside a passing
    zeppelin GO (`isOnTransport=true`). Inconclusive but suggests z=98.54 is
    in the airborne-zeppelin altitude band. Likely also phantom in static mesh.
  - **C3** ApproachPosition `(1338.10,-4646.00,51.60)` settled
    `(1338.10,-4646.00,51.60)` — dz=0.0, REAL walkable surface.
  - **C4** BoardingPosition `(1320.14,-4653.16,53.89)` settled
    `(1320.14,-4653.16,53.89)` — dz=0.0, REAL walkable surface.
  - **C5** target `(1318.107,-4658.047,71.86)` — bot teleported INTO the
    moving zeppelin GO. transportGuid=2287828610704376839
    (=`0x1FC0000000028407`, the OG↔UC zeppelin), transport-local
    Z=`-16.51` matches `TransportData.OG.TransportBoardingOffset.Z=-16.398277`
    exactly. **Proves**: deck post-attachment is at world Z = GO_pivot_Z +
    BoardingOffset.Z = 71.8 + (-16.4) ≈ 55.4 ≈ 53.89. **The configured
    BoardingPosition z=53.89 IS the correct attached-deck height.**
  - **C6** target `(1325.00,-4649.00,65.00)` settled
    `(1325.00,-4649.00,54.10)` — fell 10.9 units to lower dock. NO walkable
    polygon at z=65 in this XY.
  - **C7** target `(1322.00,-4651.00,70.00)` settled
    `(1322.00,-4651.00,53.86)` — fell 16.1 units to lower dock. NO walkable
    polygon at z=70 in this XY.
- **Reframed conclusion (load-bearing)**: the existing offmesh.txt entries
  cannot be improved — no real walkable surface exists above the bake-time
  floor (~z=70.5) near the OG zeppelin tower:
  - C1's real z=65.65 platform is BELOW the floor, can't be a START anchor.
  - C6/C7 prove there's no walkable polygon at z=65-70 between Approach and
    Boarding.
  - The existing z=96 phantom polygons are ABOVE the floor (so they bake)
    and Detour's `findNearestPoly` auto-snaps them to the z=65.65 real
    surface at runtime. The existing offmesh edge #4 (z=96 → z=53.89) is
    therefore Detour-traversable in practice.
  - BoardingPosition z=53.89 IS correctly placed (C5 confirms via transport-
    offset math).
  **The Phase 5 stall gap is NOT a mesh problem.** It's that BotRunner's
  Boarding phase calls raw `MoveToward(BoardingPosition)` instead of Detour,
  and there is a non-walkable physical barrier between ApproachPosition
  (1338.10,-4646.00,51.60) and BoardingPosition (1320.14,-4653.16,53.89) —
  likely the OG zeppelin tower's central pillar/wall ring. The 18-yard XY
  gap with 3.4-yard Z climb cannot be physically walked; it can only be
  Detour-routed via offmesh edge #4 (z=51.60 → z=96 phantom → snap to z=65 →
  off-mesh down to z=53.89).
- **Done criteria for Phase 5.3**: matches case (b) — "Live test still
  fails but the timeline screenshots + snapshot records identify a SPECIFIC
  remaining gap." The specific gap is now: **BotRunner Boarding-phase logic
  does not invoke Detour, so it cannot use the existing off-mesh edge to
  bypass the physical barrier between Approach and Boarding.**
- **No tile regeneration this session.** The existing tile (1, 29, 40) is
  already maximally functional given the bake-time floor constraint.
- **No offmesh.txt edits this session.** Removing the redundant reverse
  entry (#2 at offmesh.txt:49) or the H2c dropped entry (#6 at :98) is
  technically safe (both are no-ops at bake; binary tile unchanged), but
  defer cleanup to a future session paired with a regen+H2d-gate-rerun for
  safety.

### Next session (Phase 5.3.4 — BotRunner Boarding-phase Detour gate)
- The actual fix is no longer mesh-side. It's BotRunner-side. Gate
  `ShouldDirectBoardScheduledTransport` on `IsNativeOffMeshBoardingEnabled()`
  (currently in `Exports/BotRunner/Tasks/Travel/TravelTask.cs:1065-1067`).
  But gate-off ALONE is insufficient — the bot would sit at ApproachPosition
  with no boarding driver. The fix needs ALSO ONE of:
  - **(i) Extend the walk-leg endpoint to BoardingPosition when
    `WWOW_OFFMESH_NATIVE_BOARDING=1`.** When the flag is set, the walk leg
    targets `BoardingPosition` directly instead of `ApproachPosition`. The
    Detour navigator routes via offmesh edge #4 (1330.66,-4656.03,96.29 →
    1320.14,-4653.16,53.89), bringing the bot to BoardingPosition. The
    Boarding phase then becomes "wait for transport attachment" (no movement
    driver needed because the bot is already AT the deck). **Recommended.**
    Cleaner fit with the freeze contract — no new BoardingPosition constants,
    just the walk endpoint shifts to use the existing config differently.
  - **(ii) Make DirectBoardScheduledTransport use Detour-based navigation
    instead of raw MoveToward.** Bigger surgery; touches the boarding-phase
    state machine. Avoid unless (i) is somehow blocked.
- **Validation plan**: re-run live `CrossroadsToUndercity_UsesFlightAndZeppelin`
  with `WWOW_OFFMESH_NATIVE_BOARDING=1` and the new walk-endpoint shift.
  Expected outcome: bot's natural Detour path now goes from city ground →
  off-mesh up to z=65 phantom → off-mesh down to z=53.89 → at BoardingPosition →
  attaches to docked zeppelin → boards. The 25-min stall at z=50.5 should
  disappear. Re-run with `WWOW_LONG_PATHING_TIMELINE=1` for FG evidence.
- **Spawn Plan agent** before touching TravelTask.cs — the walk-endpoint
  shift has implications for how `ApproachPosition` vs `BoardingPosition`
  are consumed across the walk-leg / boarding-phase boundary, and the Plan
  should design a minimal-blast-radius gate that doesn't break the legacy
  (flag-off) behavior.
- **Open mesh-side cleanup (deferred to a future regen session)**: remove
  redundant reverse entry #2 (offmesh.txt:49) and dropped-at-bake H2c entry
  #6 (offmesh.txt:98). Both are no-ops at bake (binary tile unchanged) but
  carry diagnostic value as documented experiments. Future regen+H2d-gate
  session can decide whether to clean or keep.

### 2026-05-07 — Phase 5 outcome (native off-mesh boarding flag landed, walk-leg now flows through Detour, gap is now at the deck-level boarding ascent)

### 2026-05-07 — Phase 5 outcome (native off-mesh boarding flag landed, walk-leg now flows through Detour, gap is now at the deck-level boarding ascent)
- **Phase 5 BotRunner short-circuit landed (PFS-OVERHAUL-005)**. New env var
  `WWOW_OFFMESH_NATIVE_BOARDING=1` suppresses the four hand-tuned
  `BoardingPosition` nudges so navigation flows through Detour's natural
  off-mesh corridor instead of short-circuiting to the gangplank deck.
  Code touched (working tree dirty, no commits yet):
  - `Exports/BotRunner/Movement/TransportWaitingLogic.cs`:
    - Added `internal static bool IsNativeOffMeshBoardingEnabled()` reading
      `WWOW_OFFMESH_NATIVE_BOARDING == "1"` from env vars.
    - `IsAtConfiguredBoardingPosition` returns false when flag set
      (skips early `Approaching → WaitingForArrival` trigger via BoardingPosition).
    - `ShouldUseConfiguredBoardingWaypoint` returns false when flag set
      (skips late-stage BoardingPosition return in `HandleWaitingForArrival`).
    - `HandleWaitingForArrival`'s pre-stability `BoardingPosition ?? NavigationPosition`
      fallback returns `NavigationPosition` when flag set.
  - `Exports/BotRunner/Tasks/Travel/TravelTask.cs`:
    - `ShouldNavigateToConfiguredScheduledTransportBoarding` returns false
      when flag set (skips `TryNavigateToward(allowDirectFallback:false, LongTravel)`
      shortcut that bypassed the standard nav policy).
    - `ShouldDirectCommitToConfiguredScheduledTransportBoarding` returns false
      when flag set (skips `DirectBoardScheduledTransport` close-range commit).
- **Unit test gate**: 57 `TransportWaitingLogicTests` all GREEN with the flag
  default-off (env var unset). Trx:
  `tmp/test-runtime/results-live/phase5_unit_default_flag.trx`.
- **Live test outcome**: `LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin`
  with `WWOW_OFFMESH_NATIVE_BOARDING=1` ran 7m 51s. Trx:
  `tmp/test-runtime/results-live/phase5_native_boarding.trx`. Timeline
  artifacts: `tmp/test-runtime/screenshots/long-pathing/timeline/CrossroadsToUndercity_UsesFlightAndZeppelin/`
  (phases 01..09a-poll-00310, ~310 polls × 5s ≈ 25min of stalled boarding).
  - **What now works**: Flight Crossroads → Orgrimmar PASSED
    (`reason=flight_arrived dist=0.9`). Walk leg from OG flight master
    `(1677,-4315,62)` to ApproachPosition `(1338.1,-4646.0,51.6)` PASSED
    (`reason=walk_arrived target=end dist=2.9 dz=0.6 radius=4.0`). The
    Phase 5 short-circuit is doing its job: standard Detour pathfinding now
    routes the bot to the configured walk endpoint without the
    ApproachPosition → BoardingPosition nudge.
  - **What now fails (deck-level gap)**: After walk-leg complete, bot enters
    `TransportWaitingLogic` at world position `(1337.6,-4650.7,50.5)`. State
    progresses Approaching → WaitingForArrival → Boarding. Zeppelin GameObject
    `0x1FC0000000028407` (entry 164871, displayId 3031) detected at
    `(1318.1,-4653.6,71.8)` and stable. State machine hands off to
    `DirectBoardScheduledTransport` with waypoint `(1320.1,-4653.2,53.9)`
    (the BoardingPosition). Bot oscillates `MoveToward` for ~25min but
    `currentSpeed=0` throughout — bot never physically reaches z=53.9 from
    z=50.5. Final assertion: `[boarding lost] failure: map=1
    pos=(1338.0,-4649.6,50.7) distToUndercity=4897.7 transport=0x0`.
  - **Root cause**: the existing offmesh.txt entries connect upper platform
    z=96.29 ↔ ApproachPosition z=51.60 / BoardingPosition z=53.89. They let
    Detour route DOWN from z=96 to z=51-53, but the bot's natural ground
    walk reaches z=51 directly without ever hitting z=96 (PROOF C confirmed
    this — Detour prefers the ground path). The actual physical boarding
    gap is from the lower OG dock (z=50-53) UP to the gangplank DECK
    (z=71.8 — where the zeppelin model attaches). No off-mesh edge bridges
    that gap. The legacy nudge worked because `DirectBoardScheduledTransport`
    short-circuited the navigator and tried to MoveToward the deck via direct
    movement; with the flag set, that short-circuit (specifically
    `ShouldDirectCommitToConfiguredScheduledTransportBoarding`) is bypassed,
    but `ShouldDirectBoardScheduledTransport` (gated on
    `_transportLogic.CurrentPhase == Boarding`, NOT on the Phase 5 flag)
    still fires — and even with MoveToward driving, the bot can't physically
    climb from z=50 to z=53.9 in the world geometry without using the
    gangplank, which is at z=71.8.
  - **Phase 5 done criteria**: matches case (b) — "Live test still fails but
    the timeline screenshots + snapshot records identify a SPECIFIC remaining
    gap." Specific gap: there is no off-mesh edge from the lower OG dock
    walkable polygons (z=50-53) up to the actual zeppelin gangplank deck
    (z=71.8). The previous Phase 3 anchors at z=96.29 were derived from the
    polyMeshDetail vert sampling and likely correspond to a tower roof or
    structure top, NOT the gangplank deck. Per `mmo-movement-diagnostics`
    skill, an FG screenshot at the off-mesh START anchor + in-game `/gps`
    verification is required to re-snap the anchor to the actual gangplank
    deck position.

### Next session (Phase 5.3 anchor re-snap)
- The H2d gate (`OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection`)
  asserts `Linked >= 4` and was GREEN at `total=5 linked=4` per the prior
  H2d session. The mesh-side off-mesh authoring works; the issue is anchor
  PLACEMENT, not linkage. Don't re-instrument Detour. Don't extend the
  managed repair pipeline.
- Spawn `Plan` agent before touching `tools/MmapGen/offmesh.txt` — anchor
  re-snap requires FG screenshot evidence per the freeze contract and the
  `mmo-movement-diagnostics` skill rule. The Plan should cover:
  1. FG capture at the moment the bot walks to the existing
     ApproachPosition (1338.1, -4646.0, 51.6) AND when the bot is at
     each of the existing upper-platform off-mesh START anchors
     (1330.66, -4656.03, 96.29) and (1315.33, -4650.00, 98.54). Use
     `WindowCapture` + `BotTeleportAsync` to position the bot at each
     candidate world point and screenshot the result. The screenshots
     determine whether z=96 is on the gangplank deck, on a tower roof,
     or floating in air.
  2. If z=96 anchors are wrong: re-snap to the actual gangplank deck
     coordinates (likely near the zeppelin GO position
     (1318.1, -4653.6, 71.8) — the dynamic GO position is the truth for
     where the deck IS at any given moment, but the static deck post-
     attachment offset can be derived from
     `TransportData.OrgrimmarUndercityZeppelin.OG.TransportBoardingOffset
     = (-12.580913, -7.983256, -16.398277)` per `TransportData.cs:182`).
  3. Update `tools/MmapGen/offmesh.txt` with the corrected anchors.
     Backup tile (1, 29, 40) under
     `D:/MaNGOS/data/mmaps/phase5-anchor-resnap-backup-<UTC>/` first, then
     single-tile regen via `MmapGen.exe 1 --tile 29,40 --silent
     --threads 1`.
  4. Re-run the H2d gate test to confirm the new anchors still link
     (`OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection`
     should still pass `Linked >= 4` after the re-snap).
  5. Re-run the live `CrossroadsToUndercity_UsesFlightAndZeppelin` with
     `WWOW_OFFMESH_NATIVE_BOARDING=1` and the new tile. Expected outcome:
     bot's natural Detour path now includes the off-mesh hop from
     ground/dock UP to gangplank deck (z=71.8) where the zeppelin attaches.
- Open question for next session: should `ShouldDirectBoardScheduledTransport`
  ALSO gate on the Phase 5 flag? Currently it fires unconditionally when
  `phase == Boarding && !Elevator`. With the flag set, the bot enters Boarding
  phase but `DirectBoardScheduledTransport`'s direct MoveToward can't
  physically climb the gangplank wall. If the off-mesh anchors are corrected
  to land ON the deck (z=71.8), the bot will be on the deck when Boarding
  begins and DirectBoardScheduledTransport's small final adjustment to
  BoardingPosition (z=53.9 / TransportBoardingOffset) might be wrong (because
  z=53.9 was the legacy "deck height" estimate that doesn't match the real
  deck z=71.8). Decide after the anchor re-snap and re-run.

### 2026-05-07 — Phase 4 H2c outcome (definitive load-bearing finding)
- Authored intra-tile entry in `tools/MmapGen/offmesh.txt`:
  `1 29,40 (1356.8 -4501.3 29.44) (1320.14 -4653.16 53.89) 4.0`. Both
  endpoints inside tile (1, 29, 40) bounds — eliminates the cross-tile
  linkage variable that H2a left open.
- Backed up + regenerated tile (1, 29, 40):
  - Backup: `D:/MaNGOS/data/mmaps/phase4-h2c-backup-20260507T005007Z/0014029.mmtile.original`.
  - Regen log: `loadOffMeshConnections` found all 5 entries (4 prior + H2c).
- Added new test `OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection`
  in `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` mirroring
  the existing H2b smoke test pattern.
- **H2c result**: `success=True totalPolyCount=117 offMeshPolyCount=0`.
  Even with both endpoints inside the SAME tile, Detour does NOT include
  the off-mesh polygon in the corridor.
- Companion test outcomes (single run):
  - `OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` (PROOF A/B): GREEN.
  - `OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection`
    (H2b smoke on z=96/98 anchors): RED, `polyCount=5 offMeshPolyCount=0`.
  - `OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut`
    (PROOF C): RED, `polyCount=296 offMeshPolyCount=0`.
  - `OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection`
    (H2c): RED, `polyCount=117 offMeshPolyCount=0`.
  - Trx: `tmp/test-runtime/results-pathfinding/phase4_h2c_intra_tile.trx`.
- **Conclusion**: cross-tile linkage is NOT the blocker.
  `dtNavMesh::connectExtOffMeshLinks` is failing for every OG↔UC anchor
  regardless of authoring strategy (intra-tile, cross-tile, sea-level,
  upper-platform). The off-mesh polygons exist in the tile binary
  (PROOF A confirms) but are never linked into the runtime nav graph.

### Next session (H2d — Detour instrumentation)
- Spawn `Plan` agent FIRST to design the fprintf placements + the
  `DT_OFFMESH_LINK_DIAGNOSTICS` `#ifdef` toggle. Don't write Detour edits
  blindly — `Exports/Navigation/Detour/Source/DetourNavMesh.cpp` is 1500+
  lines of upstream code and the WWoW divergence comments must respect it.
- Target: instrument `dtNavMesh::connectExtOffMeshLinks` (around lines
  800-900) to log every off-mesh endpoint that fails `findNearestPoly`,
  including poly index, tile (X,Y), input position, search extents, result
  polyRef, and the final link status per endpoint.
- Wrap new fprintf lines in `#ifdef DT_OFFMESH_LINK_DIAGNOSTICS` and add
  the flag to Navigation.vcxproj's preprocessor definitions for x64
  Release ONLY (keep the BotRunner.Tests x86 path quiet).
- Rebuild Navigation.dll x64 Release via:
  `"$MSBUILD" "e:/repos/Westworld of Warcraft/Exports/Navigation/Navigation.vcxproj" -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`.
- Re-run any of the failing smoke tests; capture stderr to
  `tmp/test-runtime/mmapgen-build/phase4-h2d-offlink-diag.log`.
- Likely candidates for the failure: (a) `findNearestPoly` height extents
  too small; (b) DT_POLYREF64 / poly-side encoding mismatch in the
  generator's stored connection records; (c) tile salt/version mismatch
  between bake and runtime.

### Prior handoff (preserved)
- Active task: `PFS-OVERHAUL-004` Phase 4 navmesh tuning — **H2b polygon-
  list inspection helper landed and load-bearing finding documented**.
  New `FindPathPolygonsForAgent` C export in `Exports/Navigation/DllMain.cpp`
  + managed P/Invoke wrapper in
  `Tests/PathfindingService.Tests/NavigationInterop.cs` + smoke test
  (`OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection`)
  + PROOF C augmented with polygon-type assertion. The smoke test on the
  canonical Phase-3 proof anchors (upper platform ↔ gangplank-end) returns
  `polyCount=5 offMeshPolyCount=0`: **even between its OWN anchors, Detour's
  findPath does not traverse the off-mesh polygon**. PROOF C against the
  canonical radius=4 mesh: `polyCount=296 offMeshPolyCount=0`. **H2a (sea-
  level intermediate anchor at (1604.8,-4425.6,10.36) → (1320.14,-4653.16,53.89)
  in tile (1, 28, 40))**: regen succeeded (tile size 2,021,208 → 2,021,332
  bytes; loadOffMeshConnections logged the entry), but PROOF C is **still**
  `polyCount=296 offMeshPolyCount=0`. The H2a off-mesh polygon was baked
  into the tile but Detour's findPath does not include it either —
  conclusion: **the off-mesh polygons are dangling in the navmesh data,
  present in offMeshConCount but never linked into the runtime nav graph
  by `dtNavMesh::connectExtOffMeshLinks`**. Suspected root cause: the
  endpoints sit too far above (Phase-3 z=96/98) or across tile boundaries
  (H2a start tile 28, end tile 29) for `connectExtOffMeshLinks`'s
  `findNearestPoly` snap to bridge them to a ground polygon. PROOF A
  re-ran green after H2a regen: tile (1, 29, 40) untouched and intact.
  Next-session unblock candidates ranked at the bottom of this entry.
- Last delta:
  - Added `FindPathPolygonsForAgent(uint32_t mapId, XYZ start, XYZ end,
    float agentRadius, float agentHeight, uint64_t* outPolyRefs, uint8_t*
    outPolyTypes, int maxOut, int* outCount)` C export to
    `Exports/Navigation/DllMain.cpp` (immediately after `FindPathCorridor`
    at line 2117). Mirrors `FindPathCorridor`'s lock + WoW→Detour swap +
    `findNearestPoly` retry pattern; calls `query->findPath(...)` then
    `navMesh->getTileAndPolyByRef(ref, &tile, &poly)` for each polyRef and
    writes `poly->getType()` (0=`DT_POLYTYPE_GROUND`, 1=
    `DT_POLYTYPE_OFFMESH_CONNECTION`) into `outPolyTypes`. Returns false on
    `findNearestPoly`/`findPath` failure or null/zero buffers; on success
    `*outCount` is the full polyCount and the caller's buffers are filled
    up to min(polyCount, maxOut). Inside the existing
    `#ifndef PHYSICS_DLL_ONLY` block at lines 1748-2372. Test-only
    diagnostic export — does not modify any runtime code path or change
    behavior of existing exports. Justified under the Phase 3/4 freeze-
    contract carve-out per `mmo-movement-diagnostics` skill rule "Don't
    tune off-mesh radius / area cost without a polygon-list inspection
    helper".
  - Added `Tests/PathfindingService.Tests/NavigationInterop.cs` with
    `NavigationInterop.QueryPathPolygons(mapId, start, end, agentRadius,
    agentHeight, maxOut=740)` returning a `PolygonPathResult` record
    (Success, TotalPolyCount, PolyRefs[], PolyTypes[], plus
    `ContainsOffMeshPoly` and `OffMeshPolyCount` helpers). Polytype enum
    matches Detour's: `Ground=0, OffMeshConnection=1, Unknown=0xFF`.
  - Built Navigation.dll x64 Release via MSBuild
    (`MSBuild Exports/Navigation/Navigation.vcxproj -p:Configuration=Release
    -p:Platform=x64 -p:PlatformToolset=v145`); landed at
    `Bot/Release/net8.0/Navigation.dll` (911,872 bytes, 2026-05-06 18:30 UTC).
    PathfindingService.Tests rebuilt; test bin lives in the same
    `Bot/Release/net8.0/` directory so the runtime auto-picks the new DLL.
  - Added `OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection`
    smoke test in `LongPathingRouteTests.cs`. Queries between the two Phase-3
    proof anchors (upper-platform z=96.29 ↔ gangplank-end z=98.54) and
    asserts at least one `DT_POLYTYPE_OFFMESH_CONNECTION` polygon in
    Detour's corridor. **Result: failed** with `polyCount=5
    offMeshPolyCount=0` — all 5 polys are `Ground`. The off-mesh polygon
    exists in the tile (PROOF A still green: `offMeshConCount=4` all
    bidirectional) but Detour's `findPath` does not traverse it even
    between its own anchor endpoints.
  - Augmented `PROOF C — OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut`
    with a polygon-list assertion alongside the existing corner-Z + corner-
    count heuristics. Diagnostic dump (head 20 polys + zRange + head 15 +
    tail 5 corners) emits before assertions so test output retains
    information regardless of which assertion fires first. **Result on
    canonical radius=4 mesh: failed identically to before** —
    `corners=477 maxZ=62.15 zRange=[5.67, 62.15] polyCount=296
    offMeshPolyCount=0`. The augmentation now provides the load-bearing
    mechanistic signal: the path is 296 polys all type=Ground; the
    upper-platform off-mesh polygon never enters the corridor.
  - Authored Phase 4 H2a sea-level shortcut entry in
    `tools/MmapGen/offmesh.txt`:
    `1 28,40 (1604.80 -4425.60 10.36) (1320.14 -4653.16 53.89) 4.0`. Backed
    up tile (1, 28, 40) to
    `D:/MaNGOS/data/mmaps/phase4-h2a-backup-20260506T223737Z/0014028.mmtile.original`
    (2,021,208 bytes). Single-tile regen via `MmapGen.exe 1 --tile 28,40
    --silent --threads 1 --offMeshInput tools/MmapGen/offmesh.txt
    --configInputPath tools/MmapGen/config.json` from cwd `D:/MaNGOS/data`.
    Regen log:
    `tmp/test-runtime/mmapgen-build/phase4-h2a-tile-28-40-20260506T223758Z.log`
    (`loadOffMeshConnections:: Found offmesh connection for map 1 tile
    [28,40]: (1604.80 -4425.60 10.36) -> (1320.14 -4653.16 53.89) size 4.00`).
    Tile size grew 2,021,208 → 2,021,332 bytes (+124, consistent with one
    new off-mesh polygon + a few extra detail verts/links).
  - Re-ran PROOF A + PROOF C + smoke test against the H2a-regenerated mesh.
    PROOF A green (tile (29, 40) still has 4 off-mesh entries, no managed
    repair). PROOF C still red with **identical** `polyCount=296
    offMeshPolyCount=0` numbers. Smoke test still red (tile (29, 40)
    untouched). The H2a off-mesh polygon is in the tile per the regen log
    but Detour does not include it in the path corridor between its own
    or any wider endpoints.
  - Working tree dirty across `Exports/Navigation/DllMain.cpp`,
    `Tests/PathfindingService.Tests/NavigationInterop.cs` (new),
    `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` (PROOF C
    augmentation + new smoke test), `tools/MmapGen/offmesh.txt` (H2a entry),
    plus prior session deltas (`Tests/Tests.Infrastructure/`,
    `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`). No commit
    per user preference.
- Pass result: `Phase 4 H2b helper landed and revealed a deeper off-mesh link-creation issue. The polygon-list inspection helper (FindPathPolygonsForAgent C export + NavigationInterop wrapper) is operational and produces correct DT_POLYTYPE bytes (verified: 5/5 polys returned correctly typed as Ground in the dangling-poly case). The smoke test against the Phase-3 proof anchors fails with offMeshPolyCount=0 — proving that even between its OWN anchor endpoints Detour does not traverse the upper-platform off-mesh polygon. PROOF C against canonical radius=4 mesh fails identically with corners=477 maxZ=62.15 polyCount=296 offMeshPolyCount=0. H2a (sea-level cross-tile shortcut authored as 1 28,40 (1604.8 -4425.6 10.36) (1320.14 -4653.16 53.89) 4.0) was regenerated successfully but produces the same offMeshPolyCount=0 outcome. Conclusion: the off-mesh polygons are baked into the .mmtile data (PROOF A confirms offMeshConCount and binary layout) but they are never LINKED into the runtime nav graph by dtNavMesh::connectExtOffMeshLinks during tile loading — they exist as standalone polygons not connected to any ground polygon. Hypotheses 2 and 3 from the prior matrix are NOT the right framing; the issue is at the link-creation layer, not at radius/area-cost tuning. PROOF A re-ran green after H2a regen, confirming tile (29, 40) is untouched. Next session needs to investigate Detour's runtime link creation (likely connectExtOffMeshLinks's findNearestPoly extents or cross-tile link logic) — see "Next session unblock candidates" below.`
- Validation/tests run:
  - PROOF C + smoke test (canonical Phase-3 mesh, radius=4, before H2a):
    `dotnet test ... --filter "FullyQualifiedName~OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection|FullyQualifiedName~OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut" --logger "trx;LogFileName=phase4_h2b_polylist.trx"` →
    Both failed; PROOF C polyCount=296 offMeshPolyCount=0; smoke polyCount=5
    offMeshPolyCount=0. trx:
    `tmp/test-runtime/results-pathfinding/phase4_h2b_polylist.trx`.
  - H2a tile (1, 28, 40) regen:
    `cd D:\MaNGOS\data; & "e:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe" 1 --tile 28,40 --silent --threads 1 --offMeshInput "e:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt" --configInputPath "e:\repos\Westworld of Warcraft\tools\MmapGen\config.json"`
    → success; tile size 2,021,332 (+124). Log:
    `tmp/test-runtime/mmapgen-build/phase4-h2a-tile-28-40-20260506T223758Z.log`.
  - PROOF A + PROOF C + smoke test (H2a-regenerated mesh):
    Same dotnet test command with `--logger "trx;LogFileName=phase4_h2a_post_regen.trx"`
    → PROOF A passed; PROOF C + smoke failed identically to canonical run.
    trx: `tmp/test-runtime/results-pathfinding/phase4_h2a_post_regen.trx`.
- Evidence:
  - `Exports/Navigation/DllMain.cpp` — new `FindPathPolygonsForAgent` C
    export between `FindPathCorridor` and `CorridorUpdate`.
  - `Bot/Release/net8.0/Navigation.dll` — rebuilt x64 Release, 911,872
    bytes, 2026-05-06 18:30 UTC.
  - `Tests/PathfindingService.Tests/NavigationInterop.cs` — new managed
    wrapper + `PolygonPathResult` record + `PolyType` enum.
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` — augmented
    PROOF C (polygon-list diagnostic + assertion); new
    `OrgrimmarUpperPlatformToGangplankEnd_PolygonListIncludesOffMeshConnection`
    smoke test.
  - `tools/MmapGen/offmesh.txt` — H2a entry
    `1 28,40 (1604.80 -4425.60 10.36) (1320.14 -4653.16 53.89) 4.0` plus
    extended Phase-4 outcome comment block.
  - `D:/MaNGOS/data/mmaps/phase4-h2a-backup-20260506T223737Z/0014028.mmtile.original`
    (rollback target if next session wants to restore the canonical state).
  - `tmp/test-runtime/mmapgen-build/phase4-h2a-tile-28-40-20260506T223758Z.log`
    (H2a regen log; offmesh.txt entry parsed correctly).
  - `tmp/test-runtime/results-pathfinding/phase4_h2b_polylist.trx`
    (smoke + PROOF C against canonical radius=4 mesh).
  - `tmp/test-runtime/results-pathfinding/phase4_h2a_post_regen.trx`
    (PROOF A + smoke + PROOF C against H2a-regenerated mesh).
- Memory updated:
  - `C:/Users/lrhod/.claude/projects/e--repos/memory/project_mmapgen_offmesh_axis_swap.md`
    appended the H2b helper outcome and the off-mesh link-creation
    diagnosis so the next session resumes mid-investigation.
- Live test (timeline-enabled): NOT RE-RUN this session. PROOF C never
  passed — H2a didn't change Detour's preference. Re-running the live
  test would produce the same OG↔UC boarding gap failure as the prior
  session. Defer to after the link-creation investigation.
- Next-session unblock candidates (ranked):
  - **Branch H2c: intra-tile off-mesh authoring (highest priority).** The
    H2a entry crosses tile boundaries (start tile (28, 40), end tile
    (29, 40)). Detour's `dtNavMesh::connectExtOffMeshLinks` may not link
    cross-tile endpoints reliably — the off-mesh polygon is stored in
    the start tile and links its START side to a ground poly in (28, 40),
    but the END side requires `findConnectingPolys` to bridge into
    (29, 40)'s tile. Author an H2a variant entirely within tile (29, 40):
    e.g. `1 29,40 (1356.8 -4501.3 29.44) (1320.14 -4653.16 53.89) 4.0`
    — both endpoints inside tile (29, 40)'s bounds (Recast X 1066.67..1600.0,
    Recast Z -4800..-4266.67). The start coord is the canonical
    intermediate from the existing `orgrimmar_city_to_zeppelin_tower_lower_approach`
    walkable-leg test (`LongPathingRouteTests.cs:71`). Single-tile regen
    of (1, 29, 40), then re-run the smoke test against THAT pair (not the
    z=96/98 anchors) to prove an intra-tile off-mesh polygon DOES get
    linked. If smoke passes, the cross-tile linkage is the real blocker
    and we have a path forward; if smoke fails, the issue is deeper
    (maybe a Detour version-7 quirk, or our off-mesh radius really is
    too small for `connectExtOffMeshLinks`'s findNearestPoly snap).
  - **Branch H2d: instrument `dtNavMesh::connectExtOffMeshLinks`.** Add a
    fprintf in `Exports/Navigation/Detour/Source/DetourNavMesh.cpp`
    around line 800-900 (the `connectExtOffMeshLinks` body) to log every
    off-mesh endpoint that fails to find a ground poly via
    `findNearestPoly`. Build a custom Navigation.dll, run it against the
    canonical mesh, and capture the [LINK] lines for the OG↔UC anchors.
    Higher confidence than H2c but more invasive — touches Detour
    sources rather than the off-mesh seed list.
  - **Branch H3: area-cost investigation (lowest priority now).** Per
    `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp:1083` the off-mesh
    poly's area is `0xFF` which Detour packs to 6 bits = 63. Default
    `dtQueryFilter::m_areaCost[63] = 1.0f`. Confirm via the polygon-list
    helper that off-mesh polys appear in the corridor (currently they
    don't, so this is moot until link-creation is fixed). After H2c
    proves intra-tile off-mesh polys ARE linked, H3 becomes the next
    knob if the cross-tile case still doesn't help.
  - **Branch B (Phase 5): `TransportWaitingLogic` retirement.** Refactor
    BotRunner to explicitly target the off-mesh anchor when destination
    is `ApproachPosition`/`BoardingPosition`. Independent of the link-
    creation issue but heavier-effort. The freeze contract has flagged
    this surface; if the link-creation investigation stalls, this is
    the workaround that delivers a green live test fastest. See
    `PFS-OVERHAUL-005`.
- Next command (recommended):
  ```powershell
  # H2c: intra-tile off-mesh in tile (29, 40). First add to offmesh.txt:
  #   1 29,40 (1356.8 -4501.3 29.44) (1320.14 -4653.16 53.89) 4.0
  # then back up + regen tile (1, 29, 40):
  $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
  $bd = "D:\MaNGOS\data\mmaps\phase4-h2c-backup-$stamp"
  New-Item -ItemType Directory -Force -Path $bd | Out-Null
  Copy-Item "D:\MaNGOS\data\mmaps\0014029.mmtile" "$bd\0014029.mmtile.original"
  Set-Location D:\MaNGOS\data
  & "e:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe" 1 --tile 29,40 --silent --threads 1 `
    --offMeshInput "e:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt" `
    --configInputPath "e:\repos\Westworld of Warcraft\tools\MmapGen\config.json"
  # Then add a smoke test for the new (1356.8,-4501.3,29.44) → BoardingPosition pair
  # using NavigationInterop.QueryPathPolygons and re-run.
  ```

---
- Previous handoff (2026-05-06, Phase 4 diagnostic infrastructure): retained below for reference.
- Last updated: 2026-05-06 (Phase 4 diagnostic infrastructure landed; experiment matrix Hypothesis 1 ruled out; remaining work needs polygon-list inspection helper)
- Active task: `PFS-OVERHAUL-004` Phase 4 navmesh tuning — **Phase 4
  diagnostic infrastructure landed** (focus-safe `WindowCapture` port,
  `CaptureTimelineCheckpoint` wired at every phase boundary in the live
  `CrossroadsToUndercity_UsesFlightAndZeppelin` test, new `PROOF C`
  corner-Z + corner-count gate in `LongPathingRouteTests`). **Experiment
  matrix Hypothesis 1 (off-mesh radius 4.0 → 12.0) ruled out** — PROOF C
  still failed with `maxZ=62.15` across 477 corners; raising the radius
  did not change Detour's preference for the sea-level corridor.
  Hypothesis 2 (off-mesh start anchor in a disconnected polygon island)
  and Hypothesis 3 (off-mesh area-cost too high) need a polygon-list
  inspection helper to test directly — the corner-XYZ heuristic in
  PROOF C cannot distinguish "reached the off-mesh poly but didn't
  cross it" from "never reached the off-mesh poly at all". Phase 3
  mesh-side claim still proven by PROOF A + PROOF B pilot test
  (re-verified green after reverting tile to canonical radius=4 state).
- Last delta:
  - Ported FFXI's focus-safe `WindowCapture.CaptureWindow` helper to
    `Tests/Tests.Infrastructure/WindowCapture.cs`. Uses
    `PrintWindow(... PW_RENDERFULLCONTENT)` instead of the existing
    `Graphics.CopyFromScreen` + `SetForegroundWindow` + `HWND_TOPMOST`
    dance, so it can capture the WoW client window without stealing
    focus during live multi-minute tests. Adds
    `WindowCapture.GetTopLevelWindowsForProcess(pid)` and
    `WindowCapture.FindWoWClientWindow(pid)` so callers can pick the
    right HWND via class name (`GxWindowClassD3d`) without
    re-implementing the EnumWindows filter. Mirrors the FFXI API
    surface for cross-repo reuse per the new
    `mmo-movement-diagnostics` skill. Required adding
    `System.Drawing.Common 8.0.12` to `Tests.Infrastructure.csproj`;
    project remains `net8.0` so the existing net8.0 PathfindingService
    and Navigation.Physics test projects keep compiling. The
    `#pragma warning disable CA1416` suppresses the cross-platform
    Windows-only warning (tests run on Windows).
  - Added `CaptureTimelineCheckpoint(testName, phase, account, snapshot)`
    helper to `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`.
    Gated on `WWOW_LONG_PATHING_TIMELINE=1`. Each call writes a paired
    `.png` (via the new focus-safe helper) and `.json` (player XYZ,
    facing, currentSpeed, runSpeed, movementFlags, isOnTransport,
    transportGuid, transportOffset, fallTime, splineFlags,
    currentAction, last 5 RecentChatMessages, currentMapId, ISO 8601
    UTC timestamp) to
    `tmp/test-runtime/screenshots/long-pathing/timeline/<testName>/<phase>-<account>-<UTC>.{png,json}`.
    Wired into `CrossroadsToUndercity_UsesFlightAndZeppelin` at
    11 phase boundaries (`01-flight-master-discovered` through
    `11-saw-undercity-pathfinding-walk`) plus periodic captures every
    10 polls (~5s) inside two long-running poll predicates
    (`07a-orgrimmar-walk-poll-NNNNN`, `09a-zeppelin-evidence-poll-NNNNN`),
    and at stall/blocker fire (`07b/07c`).
  - Added `PROOF C — OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut`
    in `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`. Path-
    queries from OG flight master tower top `(1677.0, -4315.0, 62.0)` to
    OG-UC zeppelin deck approach point `(1338.1, -4646.0, 51.6)` with
    the Tauren Male capsule, asserts (a) ≥1 corner with `Z ≥ 80`
    (proves traversal of the OG zeppelin tower upper platform where
    the off-mesh anchor sits at z=96.29) and (b) `path.Length ≤ 50`
    (proves a tight corridor, not the prior 470-waypoint sea-level
    walk). Both assertions are corner-XYZ heuristics — not direct
    polygon-ref inspection. The freeze-contract-permitted polygon-list
    helper (Navigation.dll `FindPathPolygonsForAgent` export) is the
    next infra piece that would let PROOF C distinguish "off-mesh
    polygon present in corridor" vs "corridor goes elsewhere with
    matching height profile."
  - Ran PROOF C baseline (canonical Phase-3 state, radius=4.0): **failed
    as expected** with `result=repaired_affordance corners=469
    zRange=[5.67, 62.15]`. The path starts at the flight master tower
    top (z=62) and immediately descends to OG city sea level (z=5-23),
    proving Detour does not currently use the upper-platform off-mesh
    shortcut.
  - Ran experiment matrix Hypothesis 1 (radius 4.0 → 12.0 on all four
    OG↔UC anchors). Edited `tools/MmapGen/offmesh.txt`, regenerated
    tile (1, 29, 40) only, re-ran PROOF C: **same outcome —
    `maxZ=62.15 corners=477 zRange=[5.67, 62.15]`**. Bumping the
    off-mesh radius does NOT change Detour's preference for the
    sea-level corridor. This rules out "Detour's findNearestPoly is
    missing the off-mesh start because the bot is outside a 4-yard
    window" as the cause. Restored tile to canonical radius=4 state
    from backup `D:/MaNGOS/data/mmaps/phase4-h1-radius12-backup-20260506T213341Z/0014029.mmtile.original`
    and reverted offmesh.txt seed-line `size` values to 4.0; pilot
    PROOF A + PROOF B re-ran green (10s).
  - Working tree dirty across `Tests/Tests.Infrastructure/`,
    `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`,
    `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`,
    `tools/MmapGen/offmesh.txt`. No commit; user prefers explicit
    commit requests.
- Pass result: `Phase 4 diagnostic infrastructure landed (WindowCapture port + timeline helper + PROOF C). Experiment matrix H1 (radius bump) ruled out. Phase 3 still proven (PROOF A + PROOF B re-green). Phase 4 H2/H3 require polygon-list inspection helper or direct on-mesh anchor authoring along the natural walk path. Live end-to-end still blocked at OG↔UC boarding gap; further unblock via Phase 4 (H2 connectivity via natural-walk anchors) or Phase 5 (BotRunner retirement).`
- Live test (timeline-enabled): NOT RE-RUN this session. The new
  `WWOW_LONG_PATHING_TIMELINE=1` wiring is in place but a live test
  rerun was deferred — H1 experiment showed no change in mesh-side
  preference, so a live rerun would produce the same failure as the
  prior session at the OG↔UC boarding gap with the addition of a
  populated timeline directory. Next agent should either run the live
  test with timeline enabled to gather the diagnostic baseline OR
  proceed directly to H2 (intermediate anchors on the natural walk
  path) and re-test PROOF C first.
- Validation/tests run:
  - PROOF C baseline (radius=4 canonical Phase 3 state):
    `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut" --logger "trx;LogFileName=phase4_proof_c_baseline_radius_4.trx" --results-directory tmp/test-runtime/results-pathfinding` →
    `Failed (1/1) maxZ=62.15 corners=469 zRange=[5.67,62.15]`. Expected
    failure — sea-level corridor preferred.
  - H1 single-tile regen (radius=12):
    `cd D:\MaNGOS\data; & e:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe 1 --tile 29,40 --silent --threads 1 --offMeshInput e:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt --configInputPath e:\repos\Westworld of Warcraft\tools\MmapGen\config.json` →
    Tile size 1451224 bytes (unchanged from radius=4; the radius is
    metadata in the dtOffMeshConnection struct). All four off-mesh
    seeds emit `loadOffMeshConnections:: Found offmesh connection ...
    size 12.00`. Log: `tmp/test-runtime/mmapgen-build/phase4-h1-radius12-tile-29-40-20260506T213400Z.log`.
  - PROOF C with H1 mesh (radius=12):
    Same dotnet test command as baseline →
    `Failed (1/1) maxZ=62.15 corners=477 zRange=[5.67,62.15]`. Same
    sea-level corridor preferred. **Hypothesis 1 ruled out.**
  - PROOF A + PROOF B after restoring radius=4 tile from backup:
    `dotnet test ... --filter "FullyQualifiedName~OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink"` →
    `Passed (1/1) [10s]`. Confirms revert was clean.
- Evidence:
  - `Tests/Tests.Infrastructure/WindowCapture.cs` (new, ~250 LOC).
  - `Tests/Tests.Infrastructure/Tests.Infrastructure.csproj` (added
    `System.Drawing.Common` PackageReference).
  - `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs`
    (`CaptureTimelineCheckpoint` helper + 11 phase-boundary calls + 2
    in-poll periodic-capture loops + 2 stall/blocker fire-time captures).
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` (new
    `OrgrimmarFlightMasterToApproach_PrefersUpperPlatformOffMeshShortcut`
    PROOF C test method).
  - `tools/MmapGen/offmesh.txt` (radii reverted to 4.0; experiment
    matrix outcome documented in trailing comment).
  - `D:/MaNGOS/data/mmaps/phase4-h1-radius12-backup-20260506T213341Z/0014029.mmtile.original`
    (radius=4 baseline, restored to live as the canonical state).
  - `tmp/test-runtime/results-pathfinding/phase4_proof_c_baseline_radius_4.trx`
    (PROOF C baseline failure evidence).
  - `tmp/test-runtime/results-pathfinding/phase4_proof_c_h1_radius_12.trx`
    (PROOF C with radius=12 mesh — same failure mode).
  - `tmp/test-runtime/results-pathfinding/phase4_proof_a_after_revert.trx`
    (PROOF A green after radius=4 restore, confirms revert is clean).
  - `tmp/test-runtime/mmapgen-build/phase4-h1-radius12-tile-29-40-20260506T213400Z.log`
    (H1 regen log).
- Memory updated:
  - `C:\Users\lrhod\.claude\projects\e--repos\memory\project_mmapgen_offmesh_axis_swap.md`
    appended H1 outcome and the polygon-list-helper-needed conclusion
    so the next session can resume mid-experiment.
- Next command: pick up the PFS-OVERHAUL-004 Phase 4 navmesh-tuning
  matrix from H2/H3. The fastest unblock options ranked:
  - **Branch H2a (intermediate anchor on natural walk path).** Add an
    off-mesh entry from a known walkable point along the bot's
    sea-level walk (e.g. `(1604.8, -4425.6, 10.36)` — used by the
    existing `orgrimmar_flight_master_tower_descent` walkable-leg test
    in `LongPathingRouteTests`) directly to the boarding deck
    `(1320.142944, -4653.158691, 53.891945)`. This goes in tile
    (1, 28, 40) (compute: tileX = 32 - ceil(1604.8/533.33) = 28; tileY
    = 32 - ceil(-4425.6/533.33) = 40). Single-tile regen, then re-run
    PROOF C. If it passes, the issue was upper-platform anchor
    unreachability; the live test should also unblock.
  - **Branch H2b (Navigation.dll polygon-list inspection helper).** Add
    `FindPathPolygonsForAgent(uint32_t mapId, XYZ start, XYZ end,
    float agentRadius, float agentHeight, bool smoothPath,
    uint64_t* outPolyRefs, uint8_t* outPolyTypes, int maxOut,
    int* outCount)` C export to `Exports/Navigation/DllMain.cpp`
    (or `PathFinder.cpp`). Returns the polygon-ref list `findPath`
    walks plus a per-poly type byte
    (`DT_POLYTYPE_GROUND` vs `DT_POLYTYPE_OFFMESH_CONNECTION`).
    Managed P/Invoke wrapper goes in
    `Tests/PathfindingService.Tests/NavigationInterop.cs` (extend
    existing). Then PROOF C asserts the corridor includes at least
    one `DT_POLYTYPE_OFFMESH_CONNECTION` polygon. With this helper,
    H2/H3 become directly testable rather than guess-and-test cycles.
    The freeze contract permits this as test-only diagnostic
    infrastructure.
  - **Branch H3 (area-cost investigation).** TerrainBuilder.cpp:1083
    sets `meshData.offMeshConnectionsAreas.append((unsigned char)0xFF)`.
    Detour packs area into 6 bits (mask 0x3F), so 0xFF stores as 63.
    The default `dtQueryFilter::m_areaCost[63] = 1.0f` (= ground cost),
    so this should not be a high-cost surface — but worth confirming
    with the polygon-list inspection helper that the off-mesh poly
    isn't being filtered out by an unexpected area mask.
  - **Branch B (Phase 5 BotRunner retirement) — heaviest slice, likely
    the real unblock.** Refactor `TransportWaitingLogic.HandleBoarding`
    to explicitly target the off-mesh upper-platform anchor when the
    destination is `ApproachPosition`, rather than letting Detour pick.
    The freeze contract has flagged this surface for retirement
    ("`BotRunner` `TransportWaitingLogic` boarding-position constants
    becomes 'follow off-mesh link'"). Higher-effort but more direct.
  - **Independent Phase 4 follow-ups (still in parallel):** GO-bake
    `[GO] map=… tile=…: baked …` build-log marker, Tirisfal-side
    disembark off-mesh authoring (map 0), `MMAP_FORMAT.md §3` doc fix.

---
- Active task: `PFS-OVERHAUL-003` Phase 3 off-mesh pilot — **mesh-side
  claim fully proven, live end-to-end still red on the OG↔UC boarding
  gap.** Off-mesh pilot test green (PROOF A + PROOF B). Docker
  `wwow-pathfinding` rebuilt + redeployed; `IsReady=true`. Live
  `CrossroadsToUndercity_UsesFlightAndZeppelin` failed at 8m22s with bot
  final pos (1338.1, -4646.0, 51.6) — exact match to
  `BotRunner.TransportWaitingLogic`'s hardcoded approach point (which is
  below the walkable mesh and was never updated when the off-mesh seed
  moved to z=96.29/98.54). This is the next freeze-contract retirement
  candidate (PFS-OVERHAUL-005's TransportWaitingLogic boarding-position
  cleanup).
- Last delta:
  - Authored `LongPathingRouteTests.OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink`
    in `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`. The test
    parses `D:/MaNGOS/data/mmaps/0014029.mmtile` directly (managed binary
    parse of the wrapper + `dtMeshHeader` + `dtOffMeshConnection` table),
    asserts a bidirectional connection between the OG zeppelin tower upper-
    platform anchor (1330.66, -4656.03, 96.29) and gangplank-end anchor
    (1315.33, -4650.00, 98.54), then issues a Tauren-Male
    `Navigation.CalculateValidatedPath` over the same segment and snapshots
    all six `NavigationPerformanceMetrics` repair counters before/after,
    asserting they all stayed at zero. Strict scope — only
    `LongPathingRouteTests.cs` was edited (no `Navigation.cs` /
    `NavigationPerformanceMetrics.cs` changes; no new P/Invoke).
  - Patched `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp::loadOffMeshConnections`
    lines 1058-1064 — upstream-vmangos bug — in a `// BEGIN WWoW divergence
    (PFS-OVERHAUL-003) / // END` block. Upstream emitted offmesh.txt's WoW
    (X, Y, Z) coords as (Y, Z, X) into `meshData.offMeshConnections`, but
    every other consumer in MmapGen uses (X, Z, Y); the mismatch put
    `pt[0] = WoW Y` into Detour's classifier, which compared it against the
    tile's WoW X bound and silently dropped every off-mesh entry. Verified
    by `maxLinkCount` delta: pre-fix `31856 / offMeshConCount=0`, post-fix
    `31860` (= +4 from 2 connections × 2 endpoints passing X/Z classifier;
    starts re-zeroed by the height check while the original z=51-54 seeds
    were below the walkable mesh floor).
  - Discovered second compounding issue: in tile (1, 29, 40) the
    polyMeshDetail walkable elevation extent is Y ∈ [72.29, 279.29] (per
    managed `.mmtile` binary parse — header `bmin[1]=48.54` is the
    heightfield extent, not the walkable mesh). Detour's
    `dtCreateNavMeshData::classifyOffMeshPoint` height check at lines
    344-348 rejects any off-mesh start whose Z lies outside `[hmin -
    walkableClimb, hmax + walkableClimb] = [70.49, 281.09]`. The original
    screenshot-derived seeds at z=51.60/53.89 were below that floor —
    these were the bot's recorded ground positions when stalled, not the
    actual walkable upper-platform / gangplank elevations.
  - Updated `tools/MmapGen/offmesh.txt` to snap the OG↔UC zeppelin seed
    anchors to the closest walkable detail verts in tile (1, 29, 40):
    upper-platform (1330.66, -4656.03, 96.29) ↔ gangplank-end (1315.33,
    -4650.00, 98.54). XY distance ~16 units (plausible OG zeppelin
    gangplank length); both anchors directly on the polyMesh. Original
    screenshot anchors preserved as comments for traceability.
  - Regenerated map 1 tiles 28-30 / 39-41 (9 tiles) with the patched
    MmapGen + walkable-snap seeds. Tile (1, 29, 40) now reports
    `offMeshConCount=2`, both `flags=0x01` (DT_OFFMESH_CON_BIDIR), tile
    size 1450912 bytes (vs 1450600 pre-fix and 1450664 axis-swap-only).
    The other 8 cluster tiles are pure capsule + axis-swap-lineage
    refresh (no off-mesh seed matches them).
  - Discovered: `MmapGen.exe --silent` does NOT skip the interactive
    thread-count prompt. Always pass `--threads N` explicitly, or
    MmapGen will block on stdin (generator.cpp lines 280-286).
  - Rebuilt and redeployed Docker `wwow-pathfinding`:
    `world-of-warcraft-wwow-pathfinding:latest` manifest sha256
    `f8133a0ecbb28f72fc2437001675f3463c027bc85205fbed70e2c2755e6a996e`.
    `docker exec wwow-pathfinding cat /app/pathfinding_status.json` →
    `IsReady=true, StatusMessage="Ready - navigation initialized"`.
  - Off-mesh proof test passed (12s):
    `tmp/test-runtime/results-pathfinding/phase3_offmesh_pilot_walkable_snap.trx`
    PROOF A green (`offMeshConCount=2`, both bidirectional). PROOF B green
    (9-corner native path, all six repair counters stayed at zero —
    `[PATH_NATIVE] map=1 mode=smooth path=[(1328.8,-4656.3,54.5) -> ... ->
    (1319.2,-4650.7,54.0)]`; the runtime path corners report Z≈54-55 rather
    than the anchor Z≈96-98 because Navigation.dll snaps the request Z to
    nearby ground / walkable mesh in a way the managed wrapper does not
    expose, but that does not undermine the test's assertions: the off-mesh
    edge IS in the tile, and the runtime resolved the query without
    invoking any of the six repair phases).
  - Live `CrossroadsToUndercity_UsesFlightAndZeppelin` was launched in
    background (25-min session timeout) — see "Live test outcome" below.
  - No commit. Working tree dirty; user prefers explicit commit requests
    and typically wants one commit per phase.
- Pass result: `mesh-side green, live red. (1) MmapGen TerrainBuilder offmesh axis-swap bug fixed (WWoW divergence in tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp lines 1058-1064). (2) offmesh.txt seeds walkable-snapped (z=96.29/98.54) for the proof anchors + asymmetric anchors added bridging upper platform → BotRunner's existing ApproachPosition (z=51.6) and BoardingPosition (z=53.89). Tile (1,29,40) now reports offMeshConCount=4, all bidirectional. (3) Docker wwow-pathfinding rebuilt + redeployed; IsReady=true with all 41 maps loaded. (4) Off-mesh pilot test PROOF A + PROOF B green. (5) Live CrossroadsToUndercity_UsesFlightAndZeppelin ran 11m33s and failed at the OG↔UC boarding gap. Walk-nav trace shows the bot descended from flight master (z=61) to OG city sea level (z=6-23) and walked along the dock at low elevation — the natural walkable corridor in tile (29,40) is shorter / preferred over my upper-platform off-mesh shortcut, so the off-mesh edges I baked are functionally unused at runtime. The dock area is therefore walkable (contrary to my earlier polyMeshDetail-only inference); the unblocker is no longer "make the dock walkable" but "make the off-mesh route preferred over the sea-level walk". Phase 3 mesh-side fully shipped; the live end-to-end requires Phase 4 navmesh tuning or Phase 5 BotRunner retirement to consume the off-mesh.`
- Live test outcome (final, after asymmetric off-mesh): **failed at the OG↔UC boarding gap (11m33s)**. Final snapshot: `map=1 pos=(1338.1,-4646.0,51.6) distToUndercity=4894.1 transport=0x0 offset=(0.0,0.0,0.0)`. Error: "The Orgrimmar -> Undercity zeppelin was detected at the dock, but the bot missed boarding before the transport left." Screenshot: `tmp/test-runtime/screenshots/long-pathing/The-Orgrimmar---Undercity-zeppelin-was-detected-at-the-dock-but-the-bot-missed-b-LPATHFG1-client-24556-win0-20260506_164712.png`. Final trx: `tmp/test-runtime/results-live/phase3_live_offmesh_after_asymmetric.trx`. **Diagnosis (final, corrected after walk-nav trace inspection):** the bot's runtime walk path from the OG flight master tower to ApproachPosition does NOT use my upper-platform off-mesh anchors — it descends from the flight master at z=61 down to OG city at **sea level** (z=6-23) and walks ALONG THE DOCK at low elevation. The path is 470 waypoints long; at 11+ minutes the bot only reached idx=119 (around `(1595.0,-4406.3,6.7)`, ~280 units from target). Detour picks the natural sea-level walkable corridor over my upper-platform off-mesh shortcut because the off-mesh's effective cost (jump from z=96 to z=51 via off-mesh + then to dock) is no shorter than the city walk. This means the off-mesh edges I baked into tile (1, 29, 40) are correct AND in the mesh AND verified by the pilot test (PROOF A + PROOF B), but they are functionally unused at runtime by the existing `BotRunner` walk path. The live test's `[TRAVEL_TRANSPORT_MISSED_BOARDING]` diagnostic eventually fires (in the bot's chat snapshot, not the test runner log) because the bot reaches the dock area too slowly to catch the zeppelin. **The earlier polyMesh dump showing min z=71 was misleading** — the runtime traversal proves the walkable mesh DOES extend to z=6-23 in tile (29, 40); the dump's elevation extent was probably the *detail* verts only, with the polyMesh verts having a much wider Z range. (Dock walkability is therefore NOT the Phase 4 unblocker I previously logged it as; updating accordingly below.) Phase 3's mesh-side claim is fully proven (pilot test green); the live end-to-end requires either (a) Phase 4 navmesh tuning so the off-mesh shortcut becomes the preferred corridor, OR (b) Phase 5 `TransportWaitingLogic` retirement so BotRunner explicitly routes via the off-mesh.
- Validation/tests run:
  - MmapGen rebuild: `tools/MmapGen/build-mmapgen.ps1` -> `[2/2] Linking CXX executable MmapGen.exe` (incremental, only TerrainBuilder.cpp.obj recompiled).
  - Tile (1, 29, 40) regen with patched MmapGen + walkable-snap seeds: log at `tmp/test-runtime/mmapgen-build/phase3-offmesh-walkable-snap-tile-29-40-20260506T200032Z.log`; `loadOffMeshConnections` printed both seeds; `offMeshConCount=2` in resulting tile.
  - Cluster regen (other 8 tiles): log at `tmp/test-runtime/mmapgen-build/phase3-walkable-snap-cluster-20260506T200648Z.log`; all 9 tiles green.
  - Off-mesh proof test: `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --filter "FullyQualifiedName~OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink" --logger "trx;LogFileName=phase3_offmesh_pilot_walkable_snap.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (1/1) [11s]`.
  - Docker rebuild: `docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding` -> image manifest sha256 `f8133a0ecbb28f72fc2437001675f3463c027bc85205fbed70e2c2755e6a996e`. `up -d` -> `IsReady=true`.
  - Live test (asymmetric off-mesh, final): `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:VMAP_PHYS_LOG_MASK='0'; $env:VMAP_PHYS_LOG_LEVEL='0'; dotnet test Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "trx;LogFileName=phase3_live_offmesh_after_asymmetric.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000` -> `failed (1/1) at 11m33s` with bot final pos (1338.1,-4646.0,51.6) and `[TRAVEL_TRANSPORT_MISSED_BOARDING]` diagnostic. Travel trace: `[TRAVEL_LEG] FlightPath complete reason=flight_arrived` -> `[TRAVEL_LEG] Walk start end=(1338.1,-4646.0,51.6)` -> never completes -> multiple zeppelin retry cycles -> missed-boarding fail.
  - Asymmetric off-mesh tile regen: `MmapGen.exe 1 --tile 29,40 --silent --threads 1 --offMeshInput tools\MmapGen\offmesh.txt --configInputPath tools\MmapGen\config.json` from cwd `D:\MaNGOS\data` -> tile size 1450912 -> 1451224 bytes (+312); `offMeshConCount=4`, all `flags=0x01`. Log: `tmp/test-runtime/mmapgen-build/phase3-asymmetric-offmesh-tile-29-40-20260506T200900Z.log`.
  - Off-mesh proof test re-run (after asymmetric tile): `dotnet test ... --filter "...OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink" --logger "trx;LogFileName=phase3_offmesh_pilot_asymmetric.trx" ...` -> `passed (1/1) [10s]`.
- Evidence:
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` — new test method `OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` + `OrgrimmarZeppelinUpperPlatformWalkable` / `OrgrimmarZeppelinGangplankEndWalkable` static fields + `OffMeshConnectionRecord` record struct + `ParseOffMeshConnectionsFromMmtile` helper.
  - `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp` — BEGIN/END WWoW divergence block at the offmesh.txt loader's swap site (lines 1058-1064 previously; now wrapped).
  - `tools/MmapGen/offmesh.txt` — seed anchors snapped to walkable detail verts; original screenshot anchors preserved as comments.
  - `D:/MaNGOS/data/mmaps/phase3-offmesh-fix-backup-20260506T194327Z/0014029.mmtile.preFix` (rollback target).
  - `tmp/test-runtime/mmapgen-build/phase3-offmesh-fix-tile-29-40-20260506T194327Z.log` (axis-swap-only regen).
  - `tmp/test-runtime/mmapgen-build/phase3-offmesh-walkable-snap-tile-29-40-20260506T200032Z.log` (final tile (29,40) regen).
  - `tmp/test-runtime/mmapgen-build/phase3-walkable-snap-cluster-20260506T200648Z.log` (other 8 tiles).
  - `tmp/test-runtime/results-pathfinding/phase3_offmesh_pilot_walkable_snap.trx` (off-mesh proof test green).
  - `tmp/test-runtime/results-live/phase3_live_offmesh_after_walkable_snap.trx` (live test result).
- Memory updated:
  - `C:\Users\lrhod\.claude\projects\e--repos\memory\project_mmapgen_offmesh_axis_swap.md` — full diagnosis + workflow notes for the next session.
- Next command: the OG↔UC live blocker is now correctly localized to "Detour's natural walkable corridor through OG city's sea-level dock is preferred over my upper-platform off-mesh shortcut". The mesh side has more to give but it's nuanced — see the new `PFS-OVERHAUL-004` "Off-mesh shortcut routing" sub-item. The Phase 4/5 candidates ranked by likelihood of unblocking the live test:
  - **Branch A (Phase 4 navmesh tuning — recommended next slice).** The off-mesh "(upper platform → ApproachPosition)" geometrically should be MUCH shorter than the 470-waypoint sea-level walk Detour currently picks. Investigate why Detour doesn't prefer it: (a) off-mesh radius (currently 4.0) might be too narrow — try 12.0 or larger, (b) the off-mesh's start anchor (1330.66, -4656.03, 96.29) might not be reachable from the flight master tower top via the walkable mesh (i.e., disconnected polygon graph), (c) Detour's findPath may not be selecting off-mesh links optimally. Add a focused PathfindingService.Tests case that path-queries from flight master `(1677.0, -4315.0, 62.0)` to ApproachPosition `(1338.1, -4646.0, 51.6)` and asserts the corridor includes one of the off-mesh polygons (would need a polygon-list inspection helper, NOT a managed-repair, just a test-only diagnostic). Use the existing `LongPathingRouteTests.OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` as the gate skeleton.
  - **Branch B (Phase 5 BotRunner retirement — heavier slice).** Refactor `TransportWaitingLogic.HandleBoarding` (and `TravelTask`'s walk-leg routing) to explicitly target the off-mesh upper-platform anchor when the destination is `ApproachPosition`, rather than letting Detour pick. The freeze contract flags this surface for retirement ("`BotRunner` `TransportWaitingLogic` boarding-position constants becomes 'follow off-mesh link'"). Add a feature flag if needed; flip per-transport once verified.
  - **Branch C (Phase 4 disconnect the sea-level walkable path).** Make the lower OG dock area NON-walkable for Tauren in MmapGen so Detour HAS to use the upper platform. Risk: this might break other quests / NPCs that legitimately need to walk along the dock. Lowest-priority option.
  - **Independent Phase 4 follow-ups (in parallel):** (a) GO-bake `[GO] map=… tile=…: baked …` build-log marker emission so `NavDataAudit`'s GO-bake gate passes for the phase 3 cluster regens, (b) Tirisfal-side disembark off-mesh authoring on map 0 (currently no map-0 entry; UC arrival cluster regen was a capsule + lineage refresh only), (c) `docs/physics/MMAP_FORMAT.md` §3 doc fix to match the runtime tile-coord conventions (see `project_pathfinding_tile_coords.md` memory), (d) **port FFXI's focus-safe `WindowCapture.CaptureWindow` helper into WWoW's test infrastructure**. Required for the new `mmo-movement-diagnostics` skill: the existing `LongPathingTests.CaptureFailureScreenshot` uses `Graphics.CopyFromScreen` with `SetForegroundWindow` + `HWND_TOPMOST` dance, which steals focus and is unsuitable for periodic timeline capture during a live multi-minute test (the bot's input gets interrupted continuously). FFXI's `Final Fantasy XI/src/ClientInterop/Memory/WindowCapture.cs` uses `PrintWindow(... PW_RENDERFULLCONTENT)` which captures while the window is behind another — that is the pattern to mirror. Once the helper exists in WWoW, wire it into `LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin` at every phase boundary (flight depart, flight arrive, walk start, walk stall, transport detect, boarding waypoint set, boarding attach, transport ride, disembark) and at the long-poll progress intervals. Pair each screenshot with a snapshot JSON record (player XYZ, heading, current path corner index/XYZ, behavior-tree node, time-since-last-corner). Gate via `WWOW_LONG_PATHING_TIMELINE=1` env var so existing runs are unaffected. The screenshot timeline is the diagnostic surface that will let an agent reason about why Detour preferred the sea-level corridor over my upper-platform off-mesh shortcut — without it, every "I tweaked off-mesh radius and re-ran the live test" cycle is partial-information guessing.

---

- Previous handoff:
- Last updated: 2026-05-06
- Active task: `PFS-ROUTEPACK-002` is closed for PathfindingService; the
  remaining Crossroads -> Undercity live blocker is BotRunner zeppelin
  boarding/transfer timing.
- Last delta:
  - Authored `OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink` in
    `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`. The test reads
    `D:/MaNGOS/data/mmaps/0014029.mmtile` directly, parses its 20-byte
    wrapper + `dtMeshHeader` + `dtOffMeshConnection` table, asserts a
    bidirectional connection between OG dock approach (1338.10, -4646.00,
    51.60) and OG-side zeppelin deck (1320.142944, -4653.158691, 53.891945),
    then issues a Tauren-Male `Navigation.CalculateValidatedPath` over the
    same segment and snapshots all six `NavigationPerformanceMetrics` repair
    counters before/after, asserting they all stayed at zero. Strict scope —
    only `LongPathingRouteTests.cs` was edited (no
    `Navigation.cs` / `NavigationPerformanceMetrics.cs` changes; no new
    P/Invoke surface).
  - Discovered an upstream-vmangos bug in `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp`
    `loadOffMeshConnections` lines 1058-1064: the swap from offmesh.txt's
    WoW (X, Y, Z) into `meshData.offMeshConnections` was emitted as
    (Y, Z, X), but every other consumer in MmapGen uses (X, Z, Y) (see the
    `solidVerts`/`liquidVerts` swap at lines 205-207 and
    `MapBuilder::getTileBounds` writing `bmax[0] = (32 - tileX) * GRID_SIZE`,
    `bmax[2] = (32 - tileY) * GRID_SIZE`). The mismatch put `pt[0] = WoW Y`
    into Detour's `dtCreateNavMeshData::classifyOffMeshPoint`, which then
    compared it against the tile's WoW X bound and silently dropped every
    off-mesh entry. Patched in a `// BEGIN WWoW divergence (PFS-OVERHAUL-003) /
    // END WWoW divergence` block per AGENTS.md.
  - Rebuilt MmapGen via `tools/MmapGen/build-mmapgen.ps1` (incremental, 2/2
    ninja steps green). Single-tile regen of map 1 tile (29, 40) ran clean
    (`--threads 1 --silent` is required; `--silent` alone still triggers an
    interactive thread-count prompt — generator.cpp lines 280-286). Tile
    grew from 1450600 to 1450664 bytes; the 64-byte delta corresponds
    exactly to `maxLinkCount` going from 31856 to 31860 (= +4 = 2 connections
    × 2 endpoints classified `0xff` by Detour's X/Z classifier). This proves
    the axis-swap fix took effect.
  - But `offMeshConCount` remains 0. Diagnosed: Detour's
    `dtCreateNavMeshData` runs a height-bound check on the off-mesh START
    after the X/Z classifier passes
    (`DetourNavMeshBuilder.cpp:344-348`). The check uses
    `[hmin - walkableClimb, hmax + walkableClimb]` where hmin/hmax come
    from `params->detailVerts[i*3+1]`. Managed binary dump of the
    regenerated tile's detail verts shows elevation Y ∈ [72.29, 279.29],
    so the height window is [70.49, 281.09]. The off-mesh seed Z values
    51.60 / 53.89 are below the floor and both starts are silently reset
    to `0`, leaving only ends classified `0xff` (which explains the +2
    delta in `offMeshConLinkCount` rather than +4). Zero detail verts lie
    within 10 units of (1338, -4646) — the lower OG dock area is rasterized
    into the heightfield (header `bmin[1]=48.54`) but non-walkable for the
    Tauren capsule. The fix is GO-bake / static-geometry work in MmapGen
    so the OG zeppelin tower's lower platform / dock approach are
    walkable at their actual elevations. That is Phase 4 (`PFS-OVERHAUL-004`)
    territory and the existing live `Crossroads→Undercity` failure mode
    (bot reaches `(1336.7, -4658.3, 49.3)` then misses zeppelin boarding) is
    a downstream symptom of the same gap.
  - Did NOT proceed to Docker rebuild + live test rerun. The off-mesh entry
    still does not bake into tile (29, 40), so the live test's outcome
    would not be informative for Phase 3 closure (it would still fail at
    the OG dock the same way it did last session). Both gates wait for
    the Phase 4 OG-dock-walkability sub-item.
  - No commit. Working tree dirty; user prefers explicit commit requests
    and typically wants one commit per phase.
- Pass result: `partial — axis-swap MmapGen fix shipped + verified; OG dock walkability remains as Phase 4 unblocker for PFS-OVERHAUL-003 test gate`
- Validation/tests run:
  - MmapGen rebuild: `tools/MmapGen/build-mmapgen.ps1` -> `[2/2] Linking CXX executable MmapGen.exe` (incremental, only TerrainBuilder.cpp.obj recompiled).
  - Tile (1, 29, 40) regen with patched MmapGen: log at `tmp/test-runtime/mmapgen-build/phase3-offmesh-fix-tile-29-40-20260506T194327Z.log`; tile size 1450664 bytes (+64 bytes vs pre-fix); `offMeshConCount=0`, `maxLinkCount=31860` (+4 vs pre-fix 31856).
  - Detail-vert elevation dump on the regenerated tile (managed `[System.IO.File]::ReadAllBytes` + `BitConverter` parse): `bmin=(1066.67,48.54,-4800.00) bmax=(1600.00,469.68,-4266.67); detailVerts (7850): X=[1066.67,1600.00] Y(elev)=[72.29,279.29] Z=[-4800.00,-4266.80]; detail verts within 10 units of (1338,-4646): 0`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~OrgrimmarToUndercityZeppelin_BoardingIsOffMeshLink" --logger "trx;LogFileName=phase3_offmesh_pilot_after_axis_swap_fix.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed` at PROOF A: `Tile D:\MaNGOS\data\mmaps\0014029.mmtile: 0 off-mesh connection(s)`. Trx at `tmp/test-runtime/results-pathfinding/phase3_offmesh_pilot_after_axis_swap_fix.trx`.
- Evidence:
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs` (new method +
    helper `ParseOffMeshConnectionsFromMmtile` + `OffMeshConnectionRecord`
    record struct).
  - `tools/MmapGen/contrib/mmap/src/TerrainBuilder.cpp` (BEGIN/END WWoW
    divergence block at the offmesh.txt loader's swap site).
  - `D:/MaNGOS/data/mmaps/phase3-offmesh-fix-backup-20260506T194327Z/0014029.mmtile.preFix`
    (pre-fix tile, for rollback).
  - `tmp/test-runtime/mmapgen-build/phase3-offmesh-fix-tile-29-40-20260506T194327Z.log`
    (single-tile regen log).
  - `tmp/test-runtime/results-pathfinding/phase3_offmesh_pilot_after_axis_swap_fix.trx`
    (proof-test trx; failed for the right reason).
- Next command: pick up `PFS-OVERHAUL-004` "Unblock OG↔UC zeppelin off-mesh bake" sub-item. First diagnostic is to verify whether the OG zeppelin tower's deck/platform polygons SHOULD be walkable for the Tauren capsule per the source heightfield — i.e., is the lower-deck area at z≈51-54 a navigable surface in the WoW client? If yes, find why Recast's polyMeshDetail filter excludes it (slope, step climb, GO bake masking). If no, raise the offmesh.txt seed's Z anchors to the actual walkable platform elevation (likely z≈72+ per detail-vert dump). Either path requires Phase 4 work; the axis-swap fix is the correct shipped delta for this slice.

---

- Last updated: 2026-05-06
- Active task: `PFS-ROUTEPACK-002` is closed for PathfindingService; the
  remaining Crossroads -> Undercity live blocker is BotRunner zeppelin
  boarding/transfer timing.
- Last delta:
  - Kept the generic route-selection repair: straight-path static breaks that
    retry through smooth native fallback now validate as smooth fallback
    results, so they are not rejected by the stricter straight-path validator.
  - Fixed local-physics reachability classification so flat/downhill segments
    are not marked as `local_physics_layer` just because local simulation is
    unavailable; the rise gate now runs before the simulation probe.
  - Restored the known-good generic affordance/static scan behavior that
    keeps the Orgrimmar flight-master -> zeppelin route away from the steep
    incline and known static object clips without route-specific detours.
  - Rebuilt and redeployed the Docker `wwow-pathfinding` service. The rebuilt
    image digest was
    `sha256:1fa40d9cc8b50021f7043d3a114310b1752139f6ea8f17ed313593f48c50e8ae`,
    and `/app/pathfinding_status.json` reported
    `Ready - navigation initialized` with `maps=41`.
  - Fresh live Crossroads -> Undercity validation moved past the previous
    pathfinding/lower-incline blocker and failed only after the Orgrimmar ->
    Undercity zeppelin was detected at the dock but boarding was missed.
  - Follow-up BotRunner boarding-target refresh coverage passed, but the live
    rerun still failed on client transport attachment at
    `map=1 pos=(1336.7,-4658.3,49.3) transport=0x0`; the PathfindingService
    conclusion is unchanged.
- Pass result: `delta shipped; PathfindingService route gates green and live pathing moved to BotRunner boarding`
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_static_blockers_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=420000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinApproachRoute_AvoidsKnownLiveBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=flightmaster_zeppelin_approach_restore_affordance_scan.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_lower_friction_regressions.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (16/16)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_after_affordance_restore.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> row results `passed (13/13)`, but VSTest exited `1` after the session shutdown timeout.
  - `docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding` -> `passed`; `docker compose -f docker-compose.vmangos-linux.yml up -d wwow-pathfinding` -> service healthy and ready with `maps=41`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_affordance_restore.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000` -> failed after reaching the dock area: zeppelin detected, boarding missed, final snapshot `map=1 pos=(1336.6,-4658.1,49.3) transport=0x0`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_transport_boarding_refresh_green2.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (68/68)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:VMAP_PHYS_LOG_MASK='0'; $env:VMAP_PHYS_LOG_LEVEL='0'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_boarding_refresh.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000` -> failed after reaching the dock area: zeppelin detected, boarding missed, final snapshot `map=1 pos=(1336.7,-4658.3,49.3) transport=0x0`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/flightmaster_static_blockers_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/flightmaster_zeppelin_approach_restore_affordance_scan.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_routepack_lower_friction_regressions.trx`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_after_affordance_restore.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_affordance_restore.trx`
  - `tmp/test-runtime/screenshots/long-pathing/The-Orgrimmar---Undercity-zeppelin-was-detected-at-the-dock-but-the-bot-missed-b-LPATHFG1-client-41528-win0-20260506_004545.png`
  - `tmp/test-runtime/results-botrunner/botrunner_transport_boarding_refresh_green2.trx`
  - `tmp/test-runtime/results-live/long_pathing_crossroads_undercity_after_boarding_refresh.trx`
  - `tmp/test-runtime/screenshots/long-pathing/The-Orgrimmar---Undercity-zeppelin-was-detected-at-the-dock-but-the-bot-missed-b-LPATHFG1-client-6048-win0-20260506_010905.png`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: `PFS-ROUTEPACK-002` deterministic lower-incline on-demand
  route-pack recovery shipped; live validation remains open.
- Last delta:
  - Reconfirmed the direct Orgrimmar upper-deck friction gate is still red:
    `OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable`
    fails on segment `1->2` from `(1339.2,-4645.6,52.0)` to
    `(1337.6,-4644.5,53.8)`, result `native_path_alternate_mode`,
    blocked reason `static_los`.
  - Added a generic endpoint-safety guard to local-physics reachability repair
    so the helper no longer replaces the requested final endpoint with a
    nearby lateral support during failed repairs.
  - Probed alternate start-layer and sampled micro-route approaches, then
    removed them because they either exposed more deck collision pockets or
    made the focused gate slower without producing a clean route.
  - Re-probed a bounded local-physics micro-route idea for the compact deck
    step. It partially repaired the first jump in one diagnostic run but then
    hit the next deck pocket and was too slow, so that speculative code was
    removed before handoff.
  - Added on-demand warmup for deferred static route-pack seeds and retry
    throttling for failed warmups.
  - Bumped the static route-pack algorithm signature to
    `PathfindingService.StaticRoutePack.v10`.
  - Split strict attachment validation from generated internal-corridor
    support validation so unsafe off-corridor/vertical attachments are still
    rejected while bounded generated corridor prefixes can warm.
  - Lower/exterior Orgrimmar recovery seeds now use corridor-seed generation
    and the screenshot-derived gangplank target.
- Pass result: `delta shipped; lower-incline deterministic route-pack recovery green, live proof still open`
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `passed`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_on_demand_gangplank_final_focus.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarStaticRoutePackSeeds_TargetGangplankAndDeferStartupWarmup|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_contract_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (3/3)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_suffix_safety_final.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed 1/2`; route-pack suffix safety passed, direct tower-deck friction recovery still has a local-physics break near `(1339.2,-4645.6,52.0)` -> `(1337.6,-4644.5,53.8)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_deck_friction_current_open.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed`; current direct upper-deck gate remains open.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarLowerInclineRecoveryRoutePack_OnDemandWarmsGangplankPath" --logger "console;verbosity=minimal" --logger "trx;LogFileName=lower_incline_routepack_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=180000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_RoutePackSuffixDoesNotAttachToUnreachableLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_routepack_suffix_endpoint_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (1/1)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerFrictionRecovery_PathFirstSegmentsAreLocallyReachable" --logger "console;verbosity=minimal" --logger "trx;LogFileName=orgrimmar_tower_deck_friction_current_open_after_micro_revert.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `failed`; final retained code still leaves the direct upper-deck gate open at segment `1->2`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_on_demand_gangplank_final_focus.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_contract_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_suffix_safety_final.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_current_open.trx`
  - `tmp/test-runtime/results-pathfinding/static_routepack_cache_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/lower_incline_routepack_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_routepack_suffix_endpoint_guard.trx`
  - `tmp/test-runtime/results-pathfinding/orgrimmar_tower_deck_friction_current_open_after_micro_revert.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: configurable Navigation mmap preload shipped;
  `PFS-ROUTEPACK-002` lower-incline/live real-route validation remains open.
- Last delta:
  - Added `Navigation:PreloadMaps` config for PathfindingService with values
    `none`, explicit map IDs, or `all`. Docker can override with
    `Navigation__PreloadMaps=all` when the selected mmap set is ready for full
    startup preload.
  - Added `Navigation:RunStartupDiagnostics` and defaulted it to `false` so
    tests do not implicitly load sample maps through diagnostics unless they
    ask for that behavior.
  - PathfindingService now reports configured preloaded map IDs in
    `pathfinding_status.json`.
  - Rebuilt and redeployed the Linux `wwow-pathfinding` Docker service with
    preload-all enabled through both managed and native config.
- Pass result: `delta shipped; preload config parser green; Docker preload-all redeploy ready`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerPreloadConfigTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_preload_config_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (3/3)`.
  - Diagnostic attempt `PathfindingSocketServerIntegrationTests.HandlePath_LiveCorpseRunRoute_ReturnsValidatedPathWithinBudget` -> failed the existing `10s` response-budget assertion after `44s`; logs showed map `1` and map `0` loaded, so this remains a long-route boundedness issue, not a preload config failure.
  - `docker compose -f .\docker-compose.vmangos-linux.yml up -d --build wwow-pathfinding` -> succeeded; image `world-of-warcraft-wwow-pathfinding:latest` rebuilt and container `wwow-pathfinding` recreated.
  - `docker inspect wwow-pathfinding --format '{{range .Config.Env}}{{println .}}{{end}}'` -> confirmed `WWOW_NAVIGATION_PRELOAD_MAPS=all`, `Navigation__PreloadMaps=all`, and `Navigation__RunStartupDiagnostics=false`.
  - `docker logs --since 5m wwow-pathfinding` -> reported `[Navigation] preloading 41 configured map(s)`, `Navigation loaded in 117.7s`, and `PathfindingService fully initialized and ready to handle requests`.
  - `docker exec wwow-pathfinding cat /app/pathfinding_status.json` -> `IsReady=true` with all 41 discovered map IDs in `LoadedMaps`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_preload_config_tests.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_socket_live_corpse_preload_config.trx`
  - live container `wwow-pathfinding` status file `/app/pathfinding_status.json`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-05
- Active task: Detour/mmap v6 migration cross-check complete;
  `PFS-ROUTEPACK-002` lower-incline/live real-route validation remains open.
- Last delta:
  - Revalidated the PathfindingService static route-pack cache unit suite
    after the native strict mmap loader, mmap wrapper version `6`, and
    focused Orgrimmar tile regeneration.
  - The unit cache slice stayed green and did not require route-specific
    production hacks.
  - The real Orgrimmar route gate still times out after regeneration:
    combined route/cache/Crossroads command hit the `10m` runsettings limit,
    and the single static-blocker route gate hit the extended `20m` limit.
    Treat this as the existing real-route/route-pack recovery gate still red,
    not as a green mmap migration result.
- Pass result: `delta shipped; static route-pack cache green after mmap v6 migration, real route gate still red`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_static_route_pack_cache_detour_mmap_v6.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (10/10)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor|FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_uc_detour_mmap_v6_route_gates.trx" --results-directory tmp/test-runtime/results-pathfinding` -> aborted at the `10m` runsettings timeout.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_detour_mmap_v6.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> aborted at the `20m` timeout.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_static_route_pack_cache_detour_mmap_v6.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_org_uc_detour_mmap_v6_route_gates.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_detour_mmap_v6.trx`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

---

- Last updated: 2026-05-04
- Active task: Detour compatibility baseline added; `PFS-ROUTEPACK-002`
  lower-incline live recovery remains open.
- Last delta:
  - Revalidated the PathfindingService cache/route-pack deterministic unit
    slice after adding native Detour compatibility probes.
  - The Detour baseline confirmed current nav data depends on the compiled
    64-bit-ref ABI (`DT_POLYREF64`) while Detour tiles remain
    `DT_NAVMESH_VERSION = 7`. A local 32-bit-ref trial made the Orgrimmar
    route gate return `no_path`, so any future mmap regeneration should choose
    and encode its target format deliberately.
  - A combined real-route/cache command still did not complete within `20m`
    and produced a zero-counter TRX; keep this as evidence that the
    lower-incline/real-route gate remains open, not as a green validation.
- Pass result: `delta shipped; cache/pack unit slice green after Detour baseline; real route gate still open`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_pack_detour_baseline_unit.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> `passed (14/14)`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_detour_baseline_route_cache.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> shell timeout after `20m`; TRX counters stayed `0`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Evidence:
  - `tmp/test-runtime/results-pathfinding/pathfinding_cache_pack_detour_baseline_unit.trx`
  - `tmp/test-runtime/results-pathfinding/pathfinding_detour_baseline_route_cache.trx`
- Files changed:
  - `Services/PathfindingService/TASKS.md`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/DllMain.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/DetourCompatibilityTests.cs`
  - `docs/physics/DETOUR_UPGRADE_BASELINE.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`

- Previous handoff:
- Last updated: 2026-05-04
- Active task: cache/metrics/socket logging shipped; `PFS-ROUTEPACK-002`
  lower-incline live recovery remains open.
- Last delta:
  - Added `Services/PathfindingService/RouteCaching/RouteResultCache.cs`.
    The cache keys static-overlay route results by map, quantized start/end,
    race/gender, capsule, smooth flag, route policy, nav-data signature,
    route-algorithm signature, and dynamic-overlay signature.
  - Added in-flight coalescing for equivalent concurrent static route
    requests, short-TTL negative caching for `no_path`/blocked results, and a
    `RouteResultCacheSnapshot` metric surface for hits/misses/coalesced/
    bypassed/expired/invalidated/stored-positive/stored-negative/slow/in-flight
    counts.
  - Wired `PathfindingSocketServer` so route-pack hits and native validated
    path results go through the service-owned route cache. Requests carrying
    dynamic overlays bypass the cache conservatively.
  - Added `NavigationPerformanceMetrics` and `[NAV_METRICS]` summary logging
    for path resolver attempts, native `FindPathForAgent`, corridor queries,
    managed validation, long LOS/static wall/steep/local-layer/segment/dynamic
    repairs, blocked/no-path results, and slow path/native/validation counts.
  - Fixed clean protobuf socket EOF handling in `BotCommLayer` so a client
    closing after a complete request no longer floods PathfindingService with
    `Unexpected EOF`; truncated payloads still warn.
  - Reworked the socket route-cache integration proof to use a deterministic
    generated route-pack fixture through the normal protobuf path contract.
    The repeat request asserts `server.RouteCacheStats.HitCount`.
  - Made startup route-pack warmup opt-in via
    `WWOW_ROUTE_PACK_STARTUP_WARMUP=1`, and marked the known lower-incline
    seed `WarmAtStartup=false` so startup no longer blocks on that known slow
    native route-pack generation path.
  - Added a per-seed route-pack generation timeout. The real
    Navigation-backed route-pack proof now fails bounded at about `30s` on
    seed warmup instead of hitting the `20m` test session timeout. Treat this
    as the existing route-pack/lower-incline gate still being red, not as live
    validation evidence.
  - Stopped for repo cleanup before another live rerun. No live
    Crossroads -> Undercity validation was launched after the screenshot
    anchor update.
  - Updated the default Orgrimmar route-pack seed end anchors and socket/route
    tests from the old between-NPC deck point to the screenshot-derived
    Orgrimmar -> Undercity gangplank at
    `(1320.142944,-4653.158691,53.891945)`.
  - `Tests/PathfindingService.Tests.NavigationFixture` now discovers usable
    local nav data roots itself, including ready-drive `MaNGOS\data` roots, so
    focused test commands do not need to set `WWOW_DATA_DIR` explicitly. The
    fixture still sets `WWOW_DATA_DIR` internally for Navigation.dll.
  - The route-pack/static focused screenshot-anchor command timed out after
    `20m`; do not treat that as green. Rerun a narrower deterministic slice
    before live validation.
  - Added `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`.
    The cache warms generated Navigation.dll outputs, keys them by seed/schema,
    map, nav-data signature, route-algorithm signature, race/gender capsule,
    smooth flag, and route policy, and bypasses reuse for incompatible dynamic
    overlays.
  - `PathfindingSocketServer` now warms the Orgrimmar flight-master and
    exterior-incline recovery seeds at startup and returns cache hits as normal
    `NavigationPathResult` instances with `route_pack_main_path` or
    `route_pack_suffix`.
  - Added generic suffix attachment validation through
    `Navigation.IsSegmentWalkableForAgent(...)`, including strict LOS, local
    agent affordance, and resolved endpoint Z checks to avoid vertical-layer
    snaps.
  - The Docker service warmed both route packs (`packs=2`) against the mounted
    `D:/MaNGOS/data` nav data. Warmup took about `184435ms`.
  - Latest live rerun is still red: the lower-layer request from
    `(1363.9,-4377.8,26.1)` to `(1341.0,-4638.6,53.5)` correctly bypassed the
    unsafe cached suffix, then native generation was still-running at `25s`
    and the BotRunner test failed near `(1363.9,-4378.2,26.1)`.
- Pass result: `delta shipped; PathfindingService cache/metrics/socket logging deterministic bundle green; Navigation-backed route-pack proof fails bounded at seed warmup`
- Validation/tests run:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RouteResultCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=route_result_cache_tests.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (4/4)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ProtobufSocketServerLoggingTests|FullyQualifiedName~StaticRoutePackCacheTests|FullyQualifiedName~RouteResultCacheTests|FullyQualifiedName~NavigationOverlayAwarePathTests.CalculateValidatedPath_RecordsResolverAndManagedValidationMetrics|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_RepeatedStaticRequest_UsesServiceRouteCacheThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_cache_socket_logging_metrics_timeout_bundle_after_assertion.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (18/18)` with the existing benign `dumpbin` applocal warning.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_real_after_warmup_timeout_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=240000` -> failed bounded at `30s` on route-pack seed warmup.
  - `git diff --check` -> no whitespace errors; line-ending warnings only.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CrossMapRouterTests|FullyQualifiedName~TransportWaitingLogicTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~LongPathingRouteBlockerGuardTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_zeppelin_screenshot_anchors.trx" --results-directory tmp/test-runtime/results-botrunner` -> `passed (94/94)` after updating an obsolete configured-boarding-position assertion.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_after_nav_fixture_discovery.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (8/8)` without explicitly setting `WWOW_DATA_DIR`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor|FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinDeckBoardingPoint_StaysOnUpperDeckLayer" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_routepack_screenshot_boarding_anchor.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> timed out after `20m`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~StaticRoutePackCacheTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=static_routepack_cache_unit_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding` -> `passed (7/7)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoutePack_CachesMainPathAndRecoveryAnchor" --logger "console;verbosity=minimal" --logger "trx;LogFileName=routepack_cache_real_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingSocketServerIntegrationTests.HandlePath_OrgrimmarRoutePackRequest_ReturnsCachedPathThroughNormalContract" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_socket_routepack_contract_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (1/1)`.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarExteriorInclineLiveStallExactRecovery_HasWalkablePathfindingRoute|FullyQualifiedName~LongPathingRouteTests.OrgrimmarZeppelinTowerBaseToDeckBoardingPoint_ReachesUpperDeckBoardingZ|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_routepack_cache_prep_after_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `passed (4/4)`.
  - `docker compose -f docker-compose.vmangos-linux.yml up -d --build pathfinding-service` -> succeeded.
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_routepack_resolved_z_guard.trx" --results-directory tmp/test-runtime/results-live` -> `failed (1/1)` at `(1363.9,-4378.2,26.1)`.
  - `.\run-tests.ps1 -ListRepoScopedProcesses` -> `No repo-scoped processes found`.
- Files changed:
  - `Services/PathfindingService/RouteCaching/RouteResultCache.cs`
  - `Services/PathfindingService/Repository/NavigationPerformanceMetrics.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Exports/BotCommLayer/ProtobufSocketServer.cs`
  - `Tests/PathfindingService.Tests/ProtobufSocketServerLoggingTests.cs`
  - `Tests/PathfindingService.Tests/RouteResultCacheTests.cs`
  - `Tests/PathfindingService.Tests/NavigationOverlayAwarePathTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Services/PathfindingService/TASKS.md`
  - `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`
  - `Services/PathfindingService/Repository/Navigation.cs`
  - `Services/PathfindingService/PathfindingSocketServer.cs`
  - `Tests/PathfindingService.Tests/StaticRoutePackCacheTests.cs`
  - `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`
  - `Tests/PathfindingService.Tests/PathfindingSocketServerIntegrationTests.cs`
  - `Tests/PathfindingService.Tests/NavigationFixture.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `.\run-tests.ps1 -ListRepoScopedProcesses`
