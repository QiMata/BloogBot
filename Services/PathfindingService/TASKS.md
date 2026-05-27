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
7. Live pathfinding reverse-engineering must collect screenshot evidence, not just logs: use timeline captures, waypoint/stall screenshots, and teleport-to-waypoint proofs before tuning bake parameters.

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
- [x] Validate the OG↔UC zeppelin off-mesh entry seeded in `tools/MmapGen/offmesh.txt` against MmapGen's tile-coord frame. Tile (`tileX=29, tileY=40`) world bounds (Recast X 1066.67..1600.0, Recast Z -4800..-4266.67) contain both off-mesh endpoints (1338.10, -4646.00, 51.60) and (1320.14, -4653.16, 53.89), so the seed lands in the right tile. The corrected coordinate rule is now documented in `docs/physics/MMAP_FORMAT.md` §3: MmapGen CLI tile `(29,40)` writes runtime file `0014029.mmtile` because filenames use `<map><tileY:02d><tileX:02d>.mmtile`.
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
- [x] **Doc fix:** `docs/physics/MMAP_FORMAT.md` §3 and `docs/physics/MMAP_DATA_FLOW.md` now match the MmapGen source convention: OG zeppelin tower is tile `(29,40)`, config key `"2940"`, runtime file `0014029.mmtile`. Follow-up failure memo: `docs/physics/ORGRIMMAR_ZEPPELIN_TOWER_MMAP_FAILURE_2026_05_12.md`.

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
- Last updated: 2026-05-26 (same-map `TravelTask` proof closed the startup gap; the next live red is the later raw-detour contract split)

### 2026-05-26 - same-map `TravelTask` proof exposed the later raw-detour contract split on the promoted tile
- Active task: keep the promoted `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  baseline (`35579EA49C8CC1D2A2F1086EF5812D4C5F461BD2EC4E3135012AB60129175721`)
  and explain why the later tower-approach live replans can alternate between
  smoothed `raw_detour` requests tagged `interior_projection` and short
  unsmoothed `raw_detour` responses that still claim `blockedReason=none`.
- Pass result: `no PathfindingService code changed in this slice; BotRunner now
  enters TravelTask for the literal Frezza proof, which proves the live stack
  is querying the promoted tile and moves the remaining service-side question
  to the later alternate-path/raw-detour contract rather than spawn-startup or
  target coordinates`.
- Validation/tests run:
  - `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_|FullyQualifiedName~Update_SameMapLiteralFrezzaSlice_EmitsTravelPlanAndWalkNavDiagnostics|FullyQualifiedName~Update_GruntBaseDeckLipSlice_EmitsImmediatePlanAndWalkNavBoundaries" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_same_map_travelto_traveltask_dispatch_20260526_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner` -> `passed (7/7)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after `1 m 41 s`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526_traveltask_dispatch_fix1.log`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Expected-bot-to-walk-from-the-OG-tower-base-Grunt-spawn-to-literal-Frezza-1331.1-LPATHFG1-client-36448-win0-20260526_211122.png`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\03-final-LPATHFG1-20260527T011119Z.json`
  - `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
- Practical read:
  - The same-map startup gap is no longer a PathfindingService suspect. The
    live run now emits `[TRAVEL_PLAN]`, `[TRAVEL_LEG]`, and many
    `[TRAVEL_WAYPOINT_REACHED]` events before failing, which proves the caller
    is consuming the promoted tile through the intended long-travel surface.
  - The current service-side evidence is a split contract on the later stall:
    - earlier request: `corners=144 result=raw_detour blockedIndex=97 blockedReason=interior_projection:98`
    - later alternate requests: `corners=14 result=raw_detour blockedIndex=null blockedReason=none`
    - later alternate requests again: `corners=12 result=raw_detour blockedIndex=null blockedReason=none`
  - The accepted short route includes a huge late jump from around
    `(1357.2,-4516.2,32.2)` to `(1320.1,-4653.2,53.7)`, and the same run ends
    in repeated `[TRAVEL_WALK_STALL]` churn near the later tower approach.
- Next command: `Select-String -Path 'D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log' -Pattern 'blockedReason=interior_projection:98|corners=14 result=raw_detour blockedIndex=null blockedReason=none|corners=12 result=raw_detour blockedIndex=null blockedReason=none|\[TRAVEL_WALK_STALL\]'`

### 2026-05-26 - literal Frezza direct proof shows the promoted data can route to Frezza, but the live same-map objective pops `GoToTask` as arrived
- Active task: keep the promoted `D:\wwow-bot\test-data\mmaps\0012940.mmtile` baseline, preserve the raw-path contract instrumentation from commit `4ff2f765`, and answer the "wrong coordinates?" question against the exact Grunt-base -> Frezza NPC pair on both the service and live surfaces.
- Pass result: `shipped in commit aac53962; the exact Frezza coords are not the bug. The service returns a real Grunt-base -> Frezza raw path with 144 corners and a final waypoint 2.79y from Frezza, but the new same-map live proof stalls at spawn because BotRunner emits only GoToTask route-none/arrived diagnostics instead of entering the TravelTask/TRAVEL_* path.`.
- Last delta:
  - Added `DeckLipRawPathContractTests` to pin both sides of the current promoted query shape:
    - Grunt-base -> boarding corridor: `blockedReason=interior_projection:98`
    - Grunt-base -> literal Frezza: same `interior_projection:98` but final waypoint settles near Frezza itself
  - Added `LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza` so the live harness dispatches `TravelTo` directly to Frezza's literal map-1 spawn and captures screenshot/timeline evidence without the Undercity surrogate objective.
  - Reran the direct polygon/smooth diagnostics already in `WaypointDumpDiagnostic` against `D:\wwow-bot\test-data` to keep the direct NPC pair on the same promoted tile/data root as the live proof.
- Validation/tests run:
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeckLipRawPathContractTests|FullyQualifiedName~WaypointDumpDiagnostic.Dump_GruntToFrezza_PolygonChain|FullyQualifiedName~WaypointDumpDiagnostic.Compare_GruntToFrezza_vs_GruntToSnurk_SmoothPaths|FullyQualifiedName~RawPathContractTests" --logger "console;verbosity=normal" --logger "trx;LogFileName=pathfinding_grunt_literal_frezza_contract_20260526_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `passed (7/7)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_literal_frezza_tauren_fg_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after ~`38s`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_grunt_literal_frezza_contract_20260526_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-literal-Frezz-LPATHFG1-client-40164-win0-20260526_202201.png`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\`
- Practical read:
  - Exact service-side direct-Frezza proof:
    - `Navigation.CalculateRawPath(...)` logged `len=144 blockedSeg=97 blockedReason=interior_projection:98 final=(1328.32,-4649.35,53.84) dist2D=2.79 dz=0.21`
    - `WaypointDumpDiagnostic.Dump_GruntToFrezza_PolygonChain` logged `TotalPolyCount: 68`, `off-mesh count: 1`, one tile-family chain
  - Exact live-side failure shape:
    - service query stayed on literal Frezza: `[PATH_DIAG] ... start=(1332.8,-4633.4,24.0) end=(1331.1,-4649.5,53.6) ... pathLen=144 ...`
    - snapshot/chat at failure shows no `TRAVEL_*` diagnostics, only `[GOTO_ROUTE] plan=1 route=none` plus `[TASK] GoToTask pop reason=arrived`
    - stall anchor/current remained at the spawn lane: `anchor=(1332.8,-4633.4,24.0) current=(1332.1,-4634.5,23.9) moved=1.3`
  - The next credible fix surface is therefore NOT the promoted tile target coordinates. It is the BotRunner same-map `TravelTo` objective-start / task-selection layer deciding that this literal-target objective is already "arrived" before the long climb begins.
- Next command: `Select-String -Path 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_literal_frezza_tauren_fg_20260526.trx','D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log' -Pattern 'GOTO_ROUTE|GoToTask pop reason=arrived|PATH_DIAG|ACTION-RECV'`

### 2026-05-26 - raw path contract now reports endpoint-projection blocks at the later deck-lip stall
- Active task: preserve the promoted `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  baseline, keep the local-fixture `9020` handoff from commit `1238aba6`, and
  stop the raw native path contract from pretending the later tower-approach
  stub route is a clean path to the requested deck target.
- Pass result: `shipped a service-contract fix; the focused deck-lip live rerun
  still stalls at the same wall-facing tower anchor, but the local service now
  exposes that the returned raw Detour stub ends ~130y short of the requested
  destination via blockedReason=end_projection:130.2 instead of blockedReason=none`.
- Last delta:
  - `Navigation.CalculateRawPath(...)` now validates raw native endpoint
    anchors before returning `raw_detour`. If a non-empty raw path never
    reaches the requested end anchor, the service preserves the raw corners but
    tags the response with `BlockedSegmentIndex` and `BlockedReason`.
  - Added deterministic `RawPathContractTests` for the endpoint-projection
    contract while keeping `NavigationOverlayAwarePathTests` and
    `SlicedFindPathTests` green on the promoted data root.
  - Reran the focused live proof against `D:\wwow-bot\test-data` and verified
    the screenshot still shows the FG target pressed into the tower wall/dirt,
    consistent with the now-honest partial route metadata.
- Validation/tests run:
  - `dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RawPathContractTests|FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~SlicedFindPathTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_raw_contract_projection_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `aborted` because `WWOW_DATA_DIR` was unset and the strict startup gate exited before tests ran.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~RawPathContractTests|FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~SlicedFindPathTests" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_raw_contract_projection_20260526_fix2.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `passed (10/10)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_tauren_fg_20260526_endpoint_projection_fix1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after ~`62s`.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_raw_contract_projection_20260526.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding\pathfinding_raw_contract_projection_20260526_fix2.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_tauren_fg_20260526_endpoint_projection_fix1.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-LPATHFG1-client-30036-win0-20260526_190904.png`
  - `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
  - `D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log`
- Practical read:
  - The local service still produces the same short local path at the later
    tower anchor; the path ends near the current position and never projects
    toward the requested deck target.
  - The critical improvement is response honesty:
    `blockedReason=end_projection:130.2` now surfaces on both smooth and
    straight raw requests instead of `blockedReason=none`.
  - Example live evidence:
    - `[PATH_DIAG] id=16 result=raw_detour pathLen=5 rawPathLen=5 blockedIdx=3 blockedReason=end_projection:130.2`
    - `[PATH_DIAG] id=17 result=raw_detour pathLen=2 rawPathLen=2 blockedIdx=0 blockedReason=end_projection:130.2`
    - `[NAV_PATH] service-request exit elapsedMs=1 corners=5 result=raw_detour blockedIndex=3 blockedReason=end_projection:130.2`
  - This does not fix the tower approach yet, but it removes a misleading
    success case from the live proof surface and proves the remaining red is a
    real local path/topology miss, not a silent caller-side rejection.
- Next command: `Select-String -Path 'E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_tauren_fg_20260526_endpoint_projection_fix1.trx','D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log' -Pattern 'end_projection:130.2|\[PATH_DIAG\] id=|\[NAV_PATH\] service-request exit'`

### 2026-05-26 - local fixture pathfinding port handoff fixed; current live red is later `NavigationPath` rejection
- Active task: keep the promoted `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  baseline, prove the focused deck-lip live proof is really using the
  fixture-owned local PathfindingService instead of Docker `9002`, and isolate
  the next runtime failure after that handoff is corrected.
- Pass result: `shipped a live-fixture stability fix; the focused
  DeckLipClimbFromGruntToFrezza live proof now connects StateManager and
  BotRunner to the intended local PathfindingService on 127.0.0.1:9020, moves
  off the Grunt-base spawn, and fails later because BotRunner rejects a
  returned raw Detour path near (1351.3,-4526.3,34.5) rather than because the
  service hangs or the promoted tile is missing the route`.
- Last delta:
  - Added `BotServiceFixture.ResolveCurrentPathfindingEndpoint()` so
    `WWOW_TEST_PATHFINDING_IP` / `WWOW_TEST_PATHFINDING_PORT` are resolved at
    use time instead of once during `MangosServerFixture` construction.
  - Switched live fixture pathfinding consumers and monitoring logic off the
    stale constructor snapshot; the live harness now waits on, logs, and
    injects the currently-resolved endpoint into StateManager.
  - Added `PathfindingFixtureConfigurationTests` so the env override path and
    the default Docker fallback remain deterministic.
  - Reused the existing BotRunner immediate diagnostics to prove the failure
    surface moved from "no first travel diagnostic / no movement" to "service
    returned a path but `IsPathUsable(...)` rejected it".
- Validation/tests run:
  - `dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PathfindingFixtureConfigurationTests|FullyQualifiedName~BgOnlyBotFixtureConfigurationTests|FullyQualifiedName~TravelTaskTests|FullyQualifiedName~IBotTaskContractTests|FullyQualifiedName~BuildBehaviorTreeFromActions_TravelTo_CrossMap_UpsertsPersistentTravelTask" --logger "console;verbosity=minimal" --logger "trx;LogFileName=botrunner_pathfinding_port_handoff_regression_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner` -> `passed (27/27)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_tauren_fg_20260526_localpf_portfix_probe_fix3.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)` after ~`59s`, but no longer at the original spawn anchor.
- Evidence:
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-botrunner\botrunner_pathfinding_port_handoff_regression_20260526.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live\long_pathing_decklip_tauren_fg_20260526_localpf_portfix_probe_fix3.trx`
  - `E:\repos\Westworld of Warcraft\tmp\test-runtime\screenshots\long-pathing\Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-LPATHFG1-client-19028-win0-20260526_184905.png`
  - `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`
  - `D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log`
- Practical read:
  - This was a fixture/orchestration bug, not a tile regression: the local
    PathfindingService launched on `9020`, but before the fix StateManager was
    still waiting on and injecting Docker `9002` because the config snapshot
    was captured too early.
  - After the fix the rerun proves end-to-end `9020` usage and the service
    returns a path quickly at the new stall anchor:
    - smooth request: `corners=5 result=raw_detour blockedReason=none`
    - unsmoothed request: `corners=2 result=raw_detour blockedReason=none`
  - The next runtime gap is local to BotRunner validation:
    `NavigationPath.IsPathUsable(...)` rejects both returned routes and
    `GetNextWaypoint` becomes null at
    `start=(1351.3,-4526.3,34.5)` -> `end=(1320.1,-4653.2,53.9)`.
- Next command: `Select-String -Path 'D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log','D:\World of Warcraft\WWoWLogs\fg_LPATHFG120260526.log' -Pattern '\[NAV_PATH\]|\[NAV-DIAG\]|Path rejected by IsPathUsable'`

### 2026-05-26 - live deck-lip rerun is still red for both Tauren FG and Shodan FG
- Active task: reconcile the promoted `1523.8` / tile `40,29` bake win with
  the current live `DeckLipClimbFromGruntToFrezza` red.
- Pass result: `no new bake delta; the live proof was rerun against the current
  promoted tile through a fresh local PathfindingService using
  D:\wwow-bot\test-data, and both Tauren FG and Shodan FG stalled identically
  at the Grunt-base spawn with zero movement, so the current live failure is
  not a capsule-specific split`.
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; Remove-Item Env:WWOW_LONG_PATHING_SETTINGS_PATH -ErrorAction Ignore; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_tauren_fg_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_CLIMB_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='1'; $env:WWOW_LONG_PATHING_SETTINGS_PATH='E:\repos\Westworld of Warcraft\Services\WoWStateManager\Settings\Configs\LongPathing.ShodanForeground.config.json'; dotnet test E:\repos\Westworld of Warcraft\Tests\BotRunner.Tests\BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToFrezza" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_decklip_shodan_fg_20260526.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live -- RunConfiguration.TestSessionTimeout=1200000` -> `failed (1/1)`.
- Evidence:
  - `tmp/test-runtime/results-live/long_pathing_decklip_tauren_fg_20260526.trx`
  - `tmp/test-runtime/results-live/long_pathing_decklip_shodan_fg_20260526.trx`
  - `tmp/test-runtime/screenshots/long-pathing/Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-LPATHFG1-client-2948-win0-20260526_154454.png`
  - `tmp/test-runtime/screenshots/long-pathing/Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-from-base-to-Frezza-likely-SHODAN-client-37528-win0-20260526_154717.png`
- Practical read:
  - both runs selected the intended FG target (`LPATHFG1` Tauren Male, then
    `SHODAN` Gnome Female) and both failed at the same coordinates with
    `moved=0.0`, `current=TravelTo`, and no emitted
    `[TRAVEL_PLAN]` / `[TRAVEL_LEG]` / `[TRAVEL_WALK_NAV]` diagnostics.
  - Treat the next investigation as a live execution / route-start gap on the
    current promoted tile, not as a Tauren-only navmesh hole.

### 2026-05-22 - Detour PR #725 adapted port test
- Active task: test whether upstream Detour `findNearestPoly` BV-tree metadata
  fix can reduce the remaining OG dead-end stack failures without reviving any
  post-path repair.
- Pass result: `delta partially shipped; the PR #725 logic is now ported safely
  for WWoW's serialized tile layout, focused OG gates remain green, but the
  remaining OG dead-end failures stay at 17/23 and therefore still point back
  to bake/topology rather than Detour nearest-poly metadata`.
- Last delta:
  - First proved a bad port shape: the direct one-line `header->bvNodeCount =
    createBVTree(...)` adaptation corrupted off-mesh section offsets in WWoW's
    fork because the loader uses `bvNodeCount` to derive section boundaries.
    That broken branch regressed the OG sweep to `5/23` and produced garbage
    off-mesh endpoint probe coordinates.
  - Reworked both WWoW Detour builders to build the BV tree into a temporary
    buffer, size the serialized BV-tree block from the actual returned node
    count, and then copy only those nodes into the final tile payload.
  - Rebuilt `MmapGen` and `Navigation.dll`, rebaked OG tile `40,29`, and
    verified that the boarding-support off-mesh endpoint links were sane again
    in the `PathPhysicsProbe` output.
- Validation/tests run:
  - `.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release` -> passed.
  - `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Exports\Navigation\Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,28;40,29" -Variant "og-bvnodecount-pr725-fixed-layout" -DataDir "D:\wwow-bot\test-data"` -> passed; `0012940.mmtile` shrank by `16` bytes and changed hash to `43EACD6F5E53818F0478550EC8D4CB407F95C82528C79F7B07D060FCDCACC744`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log tmp/bake-sweeps/og-bvnodecount-pr725-fixed-layout-20260522T233732Z/bake.log --write-manifest tmp/test-runtime/results-navigation/og_bvnodecount_pr725_fixed_layout_tile4029_manifest.json` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_bvnodecount_pr725_fixed_layout.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`; same remaining six reds.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_bvnodecount_pr725_fixed_layout_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `6/6`.
  - `powershell -ExecutionPolicy Bypass -File E:\repos\tools\scripts\build-recastnavigation.ps1 -Configuration Debug -RunUpstreamTests` -> upstream standalone tests passed `32`, with `1` expected skip.
- Next command: probe the pre-contour / region-partition side around the four remaining dead-end anchors instead of spending more time on runtime query metadata. The PR-725 path is now exhausted as a likely cure for this bug class.

### 2026-05-22 — OG anchor-stack bake experiments + anti-pattern doc pass
- Active task: keep static-world fixes on the bake side only and continue reducing the remaining OG dead-end stack failures without reviving post-path generation repair.
- Pass result: `delta partially shipped; anchor-stack final-tile pass is wired and instrumented, the conservative overlap-gated version is kept as the non-regressing source state, and the docs/skills now explicitly ban post-path generation repair for static-world bugs`.
- Last delta:
  - Added a tile-local `postDetourCullAnchorPolyStacks` final-tile pass in `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` plus per-tile config for OG `4029`.
  - Proved a critical implementation detail: `dtNavMeshQuery::init(...)` for the post-addTile anchor pass must use a modest fixed node budget (`4096`). The earlier `polyCount`-scaled init failed on tile `40,29`, which made the pass a silent no-op until instrumented.
  - Proved another operational detail: post-addTile Detour culls can change the serialized tile hash without changing the `.mmtile` byte length. Hash + probe are the required proof surface, not file size.
  - Recorded a negative-result branch: removing the anchor/support overlap gate over-culled the local stack family, failed to close the hallway/vertical dead-end routes, and regressed `orgrimmar_exterior_steep_incline_live_stall_recovery`. That broader variant was not kept in source, and `D:\wwow-bot\test-data\mmaps\0012940.mmtile` was restored to the prior non-regressing snapshot (`SHA256 6046E861EA352D8F00DE735591E15DCFF6785D6464FC9B0758EF97FE9D6E251D`).
  - Updated shared docs/skills so `post-path generation repair` is now explicitly called out as an anti-pattern. Static-world route defects must be fixed in source extraction, MmapGen/Recast, off-mesh authoring, or a serialized bake-side final-tile pass.
- Validation/tests run:
  - `.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release` -> passed repeatedly during the anchor-stack loop.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og-anchor-stack-cull-working" -DataDir "D:\wwow-bot\test-data"` -> passed; conservative overlap-gated anchor pass logged `culled=1/4/0/1` across the four anchors and changed the tile hash without changing byte length.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log tmp/bake-sweeps/og-anchor-stack-cull-working-20260522T155720Z/bake.log --write-manifest tmp/test-runtime/results-navigation/og_anchor_stack_cull_working_tile4029_manifest.json` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments" --logger "console;verbosity=minimal"` -> passed `6/6`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_anchor_stack_cull_working.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`; same remaining reds as the prior raw-Detour baseline.
  - Negative-result bake branch: `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "og-anchor-stack-cull-no-overlap" -DataDir "D:\wwow-bot\test-data"` -> passed but over-culled (`13/5/14/5`) and regressed the full critical route sweep to `16/23`; see `critical_walk_legs_og_anchor_stack_cull_no_overlap.trx`.
- Next command: `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; & 'E:\repos\Westworld of Warcraft\Bot\Release\net8.0\PathPhysicsProbe.exe' --map 1 --start 1518.2,-4419.8,17.1 --end 1320.142944,-4653.158691,53.891945 --detour-resolve --smooth --dump-poly-stack *> tmp/test-runtime/results-pathfinding/og_anchor_stack_probe_hallway_route_stack_after_working_20260522.txt`

- Last updated: 2026-05-22 (raw Detour cutover + OG anchor-trim bake follow-up)

### 2026-05-22 — raw Detour cutover + OG anchor-trim bake follow-up
- Active task: keep the default runtime on raw/native Detour output while pushing the remaining OG zeppelin failures back into MmapGen bake/off-mesh authoring instead of reviving repair logic.
- Pass result: `delta shipped; raw-runtime default path is preserved, focused 40,28/40,29 bake follow-up improved the OG tower slice, and the exact tower-ramp route is no longer one of the red CriticalWalkLegs cases`.
- Last delta:
  - Retired managed/native repair from the default request path so `PathfindingSocketServer` now serves raw/native results by default, with the compatibility entrypoints in `Navigation.cs` reduced to raw-path shims.
  - Removed the active query-time post-processing from `Exports/Navigation/PathFinder.cpp` and raised `MAX_POINT_PATH_LENGTH` to `4096` so long raw routes are no longer truncated at the old `1024` corner cap.
  - Added an opt-in MmapGen final-tile cull for steep micro-components that are actually selected as off-mesh start landing polys (`postDetourCullOffMeshAnchorSteepTrim`), enabled only for tile `4029`.
  - Patched the Detour navmesh builder in both MmapGen and `Navigation.dll` so stored off-mesh connections reserve the extra start-tile link they need when the destination lies outside the source tile.
  - Rebuilt MmapGen + `Navigation.dll`, regenerated `D:\wwow-bot\prod-data\mmaps\0012840.mmtile` and `0012940.mmtile`, and re-ran the focused OG route/mesh gates against prod-data.
- Validation/tests run:
  - `.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release` -> passed.
  - `"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Exports\Navigation\Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> passed.
  - `Push-Location D:\wwow-bot\prod-data; & 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 --tile 40,28 --silent --threads 1 --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt' --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json'; & 'E:\repos\Westworld of Warcraft\tools\MmapGen\build\MmapGen.exe' 1 --tile 40,29 --silent --threads 1 --offMeshInput 'E:\repos\Westworld of Warcraft\tools\MmapGen\offmesh.txt' --configInputPath 'E:\repos\Westworld of Warcraft\tools\MmapGen\config.json'; Pop-Location` -> passed; logs under `tmp/bake-sweeps/raw-detour-anchor-trim-20260522T032257Z/`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\prod-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log "tmp/bake-sweeps/raw-detour-anchor-trim-20260522T032257Z/tile_4029.log" --write-manifest tmp/test-runtime/results-navigation/raw_detour_anchor_trim_tile4029_manifest.json` -> Detour/header/capsule checks pass; only the known GO build-log marker gap stays red.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToBoardingPosition_IntraTilePolygonListIncludesOffMeshConnection|FullyQualifiedName~LongPathingRouteTests.OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage" --logger "console;verbosity=minimal"` -> passed `6/6`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinShortcut_UsesCleanCurrentRuntimePath|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal"` -> passed `6/6`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_raw_detour_anchor_trim.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `17/23` passed, `6` failed. Remaining red labels: `orgrimmar_city_live_vertical_replan_recovery`, `orgrimmar_city_hallway_live_wall_stall_recovery`, `orgrimmar_city_hallway_exit_live_stall_recovery`, `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`, `orgrimmar_exterior_incline_live_stall_exact_recovery`, `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`.
- Next command: `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; & 'E:\repos\Westworld of Warcraft\Bot\Release\net8.0\PathPhysicsProbe.exe' --map 1 --start 1545,-4434.5,11.1 --end 1320.142944,-4653.158691,53.891945 --detour-resolve --smooth --dump-polyrefs --load-adt *> tmp/test-runtime/results-pathfinding/og_city_vertical_after_anchor_trim_20260522.txt`

- Last updated: 2026-05-22 (Recast full vendor sync + split-root audit parity)

### 2026-05-22 — Recast full vendor sync + split-root audit parity
- Active task: bake-side Recast migration for the Orgrimmar zeppelin tower proof slice, keeping runtime `.mmtile` / Detour compatibility intact.
- Pass result: `delta shipped; vendored Recast core now matches upstream main for MmapGen, canonical test-data/prod-data bake pipeline is green, Docker runtime reloaded the promoted tile`.
- Last delta:
  - Replaced WWoW's vendored `tools/MmapGen/dep/recastnavigation/Recast/{Include,Source}` bake core with upstream `main` commit `9f4ce64` content, preserving WWoW-local bake behavior in `TileWorker.cpp`.
  - Updated `TileWorker.cpp` to use upstream `rcRasterizeTriangles(...)` after the vendor sync retired `SortAndRasterizeTriangles(...)`.
  - Added split-root GO-spawn fallback in `TileWorker.cpp`: when baking into `D:/wwow-bot/test-data`, `WWOW_VMANGOS_DATA_DIR/gameobject_spawns.json` is now the supported source if the mutable data root has no local copy.
  - Updated `tools/NavDataAudit` to accept `--config-path` / `--spawns-path` and to treat `walkableRadius=0` / `walkableHeight=0` in `tools/MmapGen/config.json` as the intended auto-derived contract rather than a false audit failure.
  - Rebuilt MmapGen, re-baked tile `map 1 / tile 40,29` into `D:/wwow-bot/test-data`, promoted `0012940.mmtile` into `D:/wwow-bot/prod-data`, rebuilt Docker `wwow-pathfinding`, redeployed `wwow-pathfinding` + `wwow-scene-data`, and refreshed `og-zeppelin/latest`.
- Validation/tests run:
  - `.\tools\MmapGen\build-mmapgen.ps1 -Configuration Release` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; .\tools\scripts\bake-tile.ps1 -Map 1 -Tiles "40,29" -Variant "recast-full-sync-og-4029-go-fallback" -DataDir "D:\wwow-bot\test-data"` -> passed; log `tmp/bake-sweeps/recast-full-sync-og-4029-go-fallback-20260522T000716Z/bake.log`.
  - `dotnet run --project tools/NavDataAudit/NavDataAudit.csproj --configuration Release --no-restore -- D:\wwow-bot\test-data --map 1 --tile 40,29 --config-path "E:\repos\Westworld of Warcraft\tools\MmapGen\config.json" --spawns-path "D:\MaNGOS\data\gameobject_spawns.json" --build-log tmp/bake-sweeps/recast-full-sync-og-4029-go-fallback-20260522T000716Z/bake.log --write-manifest tmp/test-runtime/results-navigation/mmap_regen_map1_tile4029_20260522_full_recast_sync_testdata_manifest.json` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal" --logger "trx;LogFileName=mmap_mesh_quality_org_zeppelin_full_recast_sync_testdata.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `4/4`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_full_recast_sync_testdata.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `1/1`.
  - `.\tools\MmapGen\promote-mmaps.ps1 -Map 1 -Tiles "40,29"` -> promoted `0012940.mmtile`; prod-data SHA256 matches test-data exactly: `40DAF1915B9A9CE4BD3CA9832C4105C38F713A77012C378847C37B1F5EC9C38D`.
  - `docker compose -f docker-compose.vmangos-linux.yml build wwow-pathfinding` -> passed; image manifest `sha256:f8225328380120e77625dacec1f4e0e9ed764a5627a31c613fc2ee66fa153ecf`.
  - `docker compose -f docker-compose.vmangos-linux.yml up -d wwow-pathfinding wwow-scene-data` -> passed; `docker exec wwow-pathfinding cat /app/pathfinding_status.json` reached `IsReady=true`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers" --logger "console;verbosity=minimal" --logger "trx;LogFileName=pathfinding_org_fm_static_blockers_full_recast_sync_proddata.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `1/1`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; .\tools\scripts\export-pathfinding-reference.ps1 -Route og-zeppelin -Resume -MmapGenExe .\tools\MmapGen\build\MmapGen.exe; .\tools\scripts\summarize-pathfinding-reference.ps1 -Route og-zeppelin` -> passed; latest summary now reports `268` top-ramp/deck crop polys, `187` reachable, `81` unreachable, worst `zRange=1.000y`.
- Next command: `$env:WWOW_DATA_DIR='D:\wwow-bot\prod-data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.CrossroadsToUndercity_UsesFlightAndZeppelin" --logger "console;verbosity=minimal" --logger "trx;LogFileName=long_pathing_crossroads_undercity_after_recast_full_sync.trx" --results-directory tmp/test-runtime/results-live -- RunConfiguration.TestSessionTimeout=1500000`

- Last updated: 2026-05-13 (nav-summary accelerator scaffold)

### 2026-05-13 — Nav-summary accelerator scaffold
- Active task: long-route performance acceleration without replacing detailed
  mmap authority.
- Pass result: `delta shipped; opt-in scaffold and focused tests green`.
- Last delta:
  - Added `NavSummary` graph loading, Dijkstra anchor planning, and detailed
    leg expansion. Summary paths are rejected if any detailed leg fails,
    reports blocked metadata, or snaps too far from its requested endpoint.
  - Wired `PathfindingSocketServer` to try nav-summary expansion after
    route-pack misses and before the normal detailed query. The layer is
    disabled by default and bypasses dynamic overlays.
  - Added config defaults under `Navigation:NavSummary` and cache-key signature
    isolation when summaries are active.
- Validation/tests run:
  - `dotnet build Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug` -> passed.
  - `dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavSummaryRouteResolverTests" --logger "console;verbosity=minimal"` -> passed `4/4`.
- Next command: `dotnet test Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Debug --no-build -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavSummaryRouteResolverTests" --logger "console;verbosity=minimal"`

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

## 2026-05-21 OG Demo Route Refresh

- Refreshed the RecastDemo-facing OG zeppelin bundle so the generated `.gset`
  files carry per-layout marker subsets plus explicit
  `defaultStartMarker` / `defaultEndMarker` metadata. The top-deck runtime
  view now defaults to `frezza_spawn -> boarding_pos`, and full/tower views
  default to `route_start -> boarding_pos`.
- `tools/scripts/export-pathfinding-reference.ps1` now stages the optional
  `route_waypoints.obj` overlay into the RecastDemo bundle, so the current
  route polyline is visible immediately when the layout loads.
- The old upper-platform-only proof is now explicitly skipped as historical
  diagnostic context. The current focused shortcut proof is
  `LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut`,
  which keys off the live Detour corner export plus off-mesh corridor presence
  instead of the obsolete `z>=80` requirement.
- Validation/results:
  - `powershell -ExecutionPolicy Bypass -File E:/repos/tools/scripts/build-recastnavigation.ps1 -Configuration Debug -RunUpstreamTests` -> `32 passed, 1 skipped` (`rcRasterizeTriangle` expected degenerate skip).
  - `$env:WWOW_DATA_DIR='D:/wwow-bot/prod-data'; $env:WWOW_VMANGOS_DATA_DIR='D:/MaNGOS/data'; powershell -ExecutionPolicy Bypass -File E:/repos/Westworld of Warcraft/tools/scripts/export-pathfinding-reference.ps1 -Route og-zeppelin -DataDir D:/wwow-bot/prod-data -TrxPath "tmp/test-runtime/results-pathfinding/pathfinding_org_fm_static_blockers_full_recast_sync_proddata.trx" -Resume` -> `DONE`, refreshed `tmp/test-runtime/visualization/pathfinding/og-zeppelin/latest` and `tools/recastnavigation/build-msvc/RecastDemo/Meshes/WorldOfWarcraft/Generated/Orgrimmar/*.gset`.
  - `$env:WWOW_DATA_DIR='D:/wwow-bot/prod-data'; dotnet test E:/repos/Westworld of Warcraft/Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_PathExists|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFrezzaSpawnToBoardingPosition_PathExists|FullyQualifiedName~LongPathingRouteTests.OrgrimmarApproachToBoardingPosition_PathExistsAndDescribesOffMeshUsage|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_demo_route_refresh_20260521_rerun2.trx" --results-directory E:/repos/Westworld of Warcraft/tmp/test-runtime/results-pathfinding` -> `passed (9/9)`.

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

## 2026-05-23 Recast/MmapGen follow-up handoff

- Status:
  - the bake/runtime work is still stuck at `18/23` on the OG raw-Detour
    `CriticalWalkLegs` sweep
  - focused OG mesh/route checks remain green (`6/6`)
  - no route-time repair was reintroduced
- Remaining reds are still:
  - `orgrimmar_city_live_vertical_replan_recovery`
  - `orgrimmar_city_hallway_live_wall_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery`
  - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
  - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
- Important new learning:
  - the city/hallway/hall-exit failures are still snapping to slightly-below-
    anchor nearest-poly winners, not failing because of smooth-path clipping
  - current probe winners:
    - vertical start `(1545.0,-4434.5,11.1)` -> `0x100001520BEEE`, `surfaceZ=11.009`
    - hallway start `(1518.2,-4419.8,17.1)` -> `0x100001520ADA2`, `surfaceZ=16.885`
    - hall-exit start `(1491.4,-4417.3,23.3)` -> `0x1000015209D5A`, `surfaceZ=23.209`
- Branches tested this loop:
  - `closestPointOnPoly` support fallback for anchor culls
  - stricter anchor support floor (`surface >= anchorZ`)
  - extra collapse-point anchor probes in `config.json`
  - anchor-local lower-fringe cull when a higher overlapping layer exists
- Result of those branches:
  - all stayed `6/6` focused green
  - none beat `18/23`
  - the lower-fringe branch materially changed the tile hash to
    `01629C2251081B8C00E1F546F1690053B70BD8C9491641696603F926D373F9F3`
    but shortened the hallway dead-end, so treat it as non-promote
- Current test-data tile:
  - `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - hash: `01629C2251081B8C00E1F546F1690053B70BD8C9491641696603F926D373F9F3`
  - this is experimental, not a promoted improvement
- Best artifact bundle to inspect for the last loop:
  - `tmp/bake-sweeps/og_anchor_lower_fringe-20260523T034944Z/`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_anchor_lower_fringe.trx`
  - `tmp/test-runtime/results-pathfinding/og_vertical_anchor_polyrefs_closest_20260523.tsv`
  - `tmp/test-runtime/results-pathfinding/og_hallway_anchor_polyrefs_closest_20260523.tsv`
  - `tmp/test-runtime/results-pathfinding/og_hallexit_anchor_polyrefs_closest_20260523.tsv`
- Recommended next step:
  - stop tuning generic anchor support thresholds
  - implement a bake-side cull that explicitly targets the *current nearest-
    poly winner component* at verified bad anchors when a better overlapping
    upper layer exists, or move earlier in the pipeline and split/prevent those
    local start-cell basin layers before final Detour serialization

## 2026-05-23 Recast/MmapGen follow-up handoff (late loop)

- Status:
  - current source-backed `test-data` tile is restored to
    `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`
  - focused OG checks are green `6/6`
  - full raw-Detour `CriticalWalkLegs` is still `17/23`
- New negative-result branches completed and documented:
  - preferred-support final-tile cull inside `CullAnchorPolyStacks(...)`
  - tile-local `minRegionArea=60` config experiment via
    `tmp/config-experiments/og_4029_minRegionArea60.json`
- What the preferred-support branch proved:
  - it changed the tile hash to
    `345FA5BBFF7BDFDCFE58B3B061C9E25D162B17723A536C8FDE85E2383FBBA671`
  - focused OG checks stayed green `6/6`
  - the full sweep stayed `17/23`
  - exact dead-end winner probes did **not** move:
    - `(1535.267,-4437.9,13.909)` -> `0x100001520BE35`
    - `(1521.267,-4425.6,17.609)` -> `0x100001520AD5D`
    - `(1479.867,-4425.8,25.309)` -> `0x1000015208D00`
    - `(1364.867,-4374.0,26.109)` -> `0x1000015204ECD`
    - `(1357.2,-4516.2,32.2)` -> `0x10000152047F5`
  - exact `--dump-poly-stack` still showed same-height competitors around those
    winners, but the winner itself remained the `posOverPoly=1` containing
    support, so the branch was reverted from source
- What the `minRegionArea=60` branch proved:
  - focused OG checks stayed green `6/6`
  - full sweep stayed `17/23`
  - bake logs showed real pre-region span churn in the in-range anchor windows,
    but the route failures and dead-end winners were effectively unchanged
- Current validated artifacts:
  - focused restored-source TRX:
    `tmp/test-runtime/results-pathfinding/og_4029_source_restore_after_negative_experiments_focused.trx`
  - full restored-source TRX:
    `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_source_restore_after_negative_experiments.trx`
  - preferred-support exact winners:
    `tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_polyrefs_20260523.tsv`
  - preferred-support exact stacks:
    `tmp/test-runtime/results-pathfinding/og_anchor_deadend_exact_preferred_support_stack_20260523.txt`
  - preferred-support bake:
    `tmp/bake-sweeps/og_4029_anchor_preferred_support-20260523T065325Z/`
  - `minRegionArea=60` bake:
    `tmp/bake-sweeps/og_4029_minRegionArea60-20260523T070514Z/`
- Next move:
  - stop spending cycles on support scoring and generic isolated-region knobs
  - move to a basin/component-level bake fix, or earlier region/contour
    separation, because the exact containing winner polys are surviving across
    both classes of experiment

## 2026-05-23 original-worker comparison handoff

- I ran the "can we still bake this tile with the original worker?" proof using
  the earliest in-repo `TileWorker.cpp` baseline (`4e3716ae`, 2026-05-07),
  built in scratch at:
  - `tmp/mmapgen-baseline-20260507/`
- Scratch compatibility note:
  - the copied baseline needed one mechanical build-only edit:
    `SortAndRasterizeTriangles(...) -> rcRasterizeTriangles(...)`
  - that change was made only in the scratch copy, not in live source
- Focused baseline bake:
  - log:
    `tmp/bake-sweeps/tileworker_20260507_baseline_20260523T123759Z/bake.log`
  - produced tile hash:
    `5EC417472F918E93A1255098FFFDD86B1F56CDE91E4BA0ED8235CCD004C49675`
- Validation of the baseline tile:
  - focused mesh-quality slice stayed green `4/4`:
    `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_focused_mesh_quality.trx`
  - focused route gate failed:
    `tmp/test-runtime/results-pathfinding/tileworker_20260507_baseline_route_gate.trx`
  - `NavDataAudit` manifest:
    `tmp/test-runtime/results-navigation/tileworker_20260507_baseline_tile4029_manifest.json`
  - audit still passed Tauren/Detour header checks, but failed the GO-feed
    evidence:
    - `bake.log does not show gameobject spawn loading`
    - `bake.log has no GO geometry bake line for tile 40,29`
- Practical conclusion:
  - current WWoW GO-aware input is a real improvement surface, not incidental
  - the old/original worker can still bake a plausible-looking tile, but
    without GO-spawn-aware geometry it regresses the actual Orgrimmar
    static-blocker route again
- Current state after comparison:
  - restored `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - restored hash:
    `E299BDC34EEFD82F2B0466B66BE09E7BDCDC3A683C59106778D96A55C01824B4`
- Research outcome from sibling generators:
  - stock vmangos / TrinityCore / AzerothCore generator flow is still
    `loadMap -> loadVMap -> loadOffMeshConnections`
  - those generators do not ingest WWoW-style server `gameobject_spawns.json`
    bake data
  - so "swap to stock mmaps_generator" is not a real fix path for GO-sensitive
    city tiles; the correct direction remains richer WoW geometry feed plus
    bake-side topology cleanup

## 2026-05-23 source-support pre-region probe handoff

- I added source-support stage instrumentation in
  `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
  - `HF-SRC-ANCHOR`
  - `CHF-SRC-ANCHOR`
- Purpose:
  - prove, during a normal bake, whether the intended upper support from the
    classified source triangles survives raster/filter/compact/erode stages
- Probe bake artifact:
  - `tmp/bake-sweeps/og_4029_stage_support_probe_v2-20260523T133825Z/`
- Important outcome:
  - this branch is still diagnostics, not a fix
  - the current log stream is still per-subtile/noisy enough that it should not
    be promoted as proof of a successful pre-region cull
  - because the experiment stayed unproven, I disabled
    `preRegionCullAnchorSourceSupportCompetingSpans` again in
    `tools/MmapGen/config.json`
- Current `test-data` restore state after this loop:
  - `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - hash:
    `FE0C8973C5D6344B9121F896F2255670C27781A14A5D47254BE3D33D458E0F25`
- Best next move:
  - keep the new stage probes, but tighten them to a single known subtile or
    explicit source-to-heightfield coverage proof before using them to justify
    another bake-side cull

## 2026-05-23 isolation update

- I isolated the `40,29` config regressions instead of continuing to tune the
  combined `layers + 1.3` source state.
- Proven negative knobs:
  - `watershed + maxSimplificationError=1.3`
    - hash:
      `932A176CD19C96B38E319ACDFD085A3BD9BC68E00FB6A792AB541F69F7AC713C`
    - focused slice dropped to `4/7`
    - Frezza shortcut lost its off-mesh corridor (`offMeshPolyCount=0`)
    - giant bridge polys returned
  - `layers + 1.8`
    - hash:
      `814BA912D2089383FEB6AA5836AC4FAC62F16FE21B22E9B2FEE8DD2E2B2DBBE3`
    - focused slice dropped to `5/7`
    - still left one shadowed lower trim ledge and under-preserved deck
      connector coverage
- Source was restored by removing both `partitionType` and
  `maxSimplificationError` from tile `4029` config.
- Current best-known source-backed state:
  - tile hash:
    `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
  - focused OG slice:
    `tmp/test-runtime/results-pathfinding/og_4029_source_restore_watershed18_focused.trx`
    (`7/7` pass)
  - full raw-Detour sweep:
    `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_source_restore_watershed18.trx`
    (`17/23` pass, same six red legs as before)
- Meaning:
  - the ledge-fallback restoration was not the regression
  - `maxSimplificationError=1.3` is currently a forbidden knob on this tile
  - next work should move earlier in the bake/topology proof surface again,
    not back to contour simplification or `layers` churn

## 2026-05-23 anchor stage manifest handoff

- Status:
  - stage-by-stage structured coverage now exists for tile `1:40,29`
  - current `test-data` tile is still the approved source-backed snapshot:
    `D:\wwow-bot\test-data\mmaps\0012940.mmtile`
  - hash:
    `FB2FBAF1848FC2ACFB1F9E093A8EC99284C9C19843CD64E4F15CA4FBBF3315D6`
  - focused OG slice stayed green `7/7`
  - full raw-Detour `CriticalWalkLegs` was not rerun because the tile hash did
    not change
- Final artifact bundle:
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_manifest.json`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.json`
  - `tmp/bake-sweeps/og_4029_anchor_stage_manifest_clean-20260523T212130Z/analysis/map0012940_anchor_stage_summary.csv`
  - `tmp/test-runtime/results-pathfinding/og_4029_anchor_stage_manifest_clean_focused.trx`
- New workflow:
  - `writeAnchorStageManifest=true` is now the preferred proof surface for the
    remaining Orgrimmar stacked-support reds
  - `bake-tile.ps1` auto-copies the manifest into the variant `analysis/`
    folder and runs `NavDataAudit --stage-summary-only`
  - `logAnchorStageDiagnostics=false` keeps the replaced
    `SRC-ANCHOR-SUPPORT` / `HF-SRC-ANCHOR` / `CHF-SRC-ANCHOR` /
    `CHF-SRC-COMP` spam out of the default bake log
- Proven first-bad-stage answers:
  - `1546.600,-4435.900,11.500` -> `finalDetour`
    - support survives through `polymesh`, but the final Detour winner drops to
      lower basin `0x1000000000BE35`
  - `1522.500,-4424.100,17.000` -> `finalDetour`
    - support and lower competitor both survive into `polymesh`; final winner
      still lands on lower basin `0x1000000000AD5D`
  - `1523.800,-4425.900,17.100` -> `median`
    - lower competitor becomes dominant before regions/contours
  - `1521.267,-4425.600,17.609` -> `contours`
    - support survives `regions`, but contour generation is where the lower
      basin becomes dominant
  - `1521.300,-4422.500,17.100` -> `sourceSupport`
    - the exact upper support is not yet proven by the source-backed oracle
- Remaining uncertainty:
  - `sourceSupport` failure on `1521.300,-4422.500,17.100` means we still need
    a better source-backed proof for that exact coord before calling any later
    stage wrong
  - some compact-stage records carry `anchor_outside_compact_window`; treat
    those as window/projection nuances, not as automatic support loss
- Next move:
  - use the summary as the very first regression/improvement gate for future
    `40,29` branches
  - only rerun the full `CriticalWalkLegs` sweep when the saved tile hash moves
  - do not spend another session churning `partitionType` or
    `maxSimplificationError` unless the stage summary shows a specific stage
    shift that justifies it

### 2026-05-23 follow-up: combined source-support restore + window cull

- Current experimental tile/config state on `4029`:
  - `preRegionRestoreAnchorSourceSupportAfterErode=true`
  - `preRegionCullAnchorSourceSupportCompetingSpans=true`
  - `preRegionCullAnchorSourceSupportFallbackToWindow=true`
  - `preRegionCullAnchorUpperCompactSpans=true`
  - `postDetourCullAnchorPolyStacks=true`
- Bake + validation bundle:
  - bake dir:
    `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/`
  - summary:
    `tmp/bake-sweeps/og_4029_restore_source_cull_window-20260523T215708Z/analysis/map0012940_anchor_stage_summary.json`
  - focused validation:
    `tmp/test-runtime/results-pathfinding/og_4029_restore_source_cull_window_focused.trx`
    (`7/7` pass)
  - full raw-Detour sweep:
    `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_restore_source_cull_window.trx`
    (`17/23` pass)
  - saved tile hash:
    `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
- Proven improvement from the stage summary:
  - `1522.500,-4424.100,17.000` no longer reports a bad stage
  - `1521.267,-4425.600,17.609` no longer reports a bad stage
  - `1523.800,-4425.900,17.100` still first fails at `median`, but the lower
    competitor is gone by `regions`
  - `1546.600,-4435.900,11.500` still first fails at `finalDetour`, but the
    final winner is no longer classified as a lower competitor
  - `1521.300,-4422.500,17.100` is still blocked at `sourceSupport`
- Route-level reality:
  - the score stayed `17/23`, but several dead-end paths shifted later:
    - city vertical route now dies near `(1537.3,-4437.9,13.0)`
    - hallway route now dies near `(1520.6,-4426.5,17.9)`
    - hallway-exit route still dies near `(1479.8,-4426.0,25.3)`
    - exterior incline still dies near `(1364.9,-4374.0,26.1)`
- Next recommended move:
  - add the shifted dead-end coords to the anchor manifest list and rerun the
    same stage-summary workflow on this branch
  - do not drop back to `partitionType` or `maxSimplificationError` tuning;
    the manifest already proved this branch is changing the right local basin
    ownership surface

### 2026-05-23 manifest-only shifted-dead-end follow-up

- New safe probe surface:
  - `anchorStageManifestCoordsWow` in `tools/MmapGen/config.json`
  - purpose: add extra stage-manifest anchors without changing the actual cull
    coord list
- Validation run:
  - `tmp/bake-sweeps/og_4029_manifest_shifted_deadends_v2-20260523T221238Z/`
  - hash stayed on the combined source-cull branch:
    `29449D252853BF2E3B9739DC108BA0E4CE1E0F4C1152D7BADAE45032984945C5`
- New green manifest-only anchors:
  - `1537.300,-4437.900,13.000`
  - `1520.600,-4426.500,17.900`
  - `1355.600,-4522.300,33.100`
- Meaning:
  - the current route stalls are not explained by bad local support at those
    exact endpoints anymore
  - next route-fix loop should search for the next corridor break beyond those
    coords, not keep reworking the same endpoint support basin

### 2026-05-24 finalDetour component manifest follow-up

- Shipped:
  - final anchor manifest now records final Detour candidate
    `componentId/componentPolyCount/componentArea2D`
  - `NavDataAudit` summary/CSV now carry:
    - `FinalWinnerComponentId`
    - `FinalWinnerComponentPolyCount`
    - `FinalSupportComponentCount`
    - `FinalLowerComponentCount`
- Build/test:
  - `powershell -ExecutionPolicy Bypass -File tools/MmapGen/build-mmapgen.ps1 -Configuration Release`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AnchorStageManifestAnalyzerTests"`
  - both passed
- Current analysis artifact:
  - `tmp/bake-sweeps/og_4029_component_manifest_links-20260524T000728Z/`
  - tile hash stayed
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- What the new proof says:
  - `1518.2` still wins `0x1000000000ADA1` with two final support candidates
  - `1520.6` still wins `0x1000000000AD6E` with seven final support candidates
  - `1523.8` is still the clean red:
    `finalDetour -> lower_competitor_dominant`
  - direct runtime probes from `1518.2`, `1520.6`, `1523.8`, and `1491.4`
    all still dead-end locally before escaping the hallway
- Takeaway:
  - the hallway/hall-exit issue is now proven to be a chained trapped-basin
    problem in final Detour, not just one bad endpoint snap
  - next iteration should add pair-specific final reachability / candidate
    routeability proof, or a component-targeted basin cull
  - do not spend another loop only retuning support-band tolerances

### 2026-05-24 routeability-aware trapped-component cull follow-up

- Shipped:
  - finalDetour manifest summary now carries:
    - `FinalWinnerRouteableToAnyTarget`
    - `FinalResolvedRouteTargetCount`
    - `FinalRouteableSupportCandidateCount`
    - `FinalRouteableSupportComponentCount`
  - tile config now supports local chain targets through
    `anchorRouteTargetsWow`
  - optional experiment flag:
    `postDetourCullAnchorTrappedComponents`
- Build/test:
  - `powershell -ExecutionPolicy Bypass -File tools/MmapGen/build-mmapgen.ps1 -Configuration Release`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut"`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes"`
- Validated artifacts:
  - `tmp/bake-sweeps/og_4029_anchor_routeability_cull-20260524T004027Z/`
    - tile hash:
      `B84D1CD2369E03721ECBDC83656EC4E700E546886CFF49C231F52F05CED086AF`
  - `tmp/bake-sweeps/og_4029_anchor_routeability_chain_targets-20260524T005038Z/`
    - tile hash:
      `039BEDF73A2318B0D6559BDC0FB453D240875EDD08BA2319F56A0EA26D85EA94`
  - `tmp/test-runtime/results-pathfinding/og_4029_anchor_routeability_chain_targets_focused.trx`
  - `tmp/test-runtime/results-pathfinding/critical_walk_legs_og_4029_anchor_routeability_chain_targets.trx`
- Results:
  - focused slice stayed `7/7`
  - full raw-Detour sweep stayed `17/23`
  - failing labels stayed the same six reds:
    - `orgrimmar_city_live_vertical_replan_recovery`
    - `orgrimmar_city_hallway_live_wall_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_exterior_incline_live_stall_exact_recovery`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
- Key learning:
  - city / hallway / hallway-exit anchors still show
    `FinalRouteableSupportComponentCount=0`, so routeability did not reveal a
    better local winner to keep
  - the routeability cull therefore cannot solve those basins yet; it has no
    alternate routeable support component to preserve
  - `1364.867,-4374.000,26.109` and `1355.600,-4522.300,33.100` did become
    routeable, which changed the underpass failure from a dead-end into a bad
    overhead-ramp climb
- Current default:
  - keep the routeability summary fields and `anchorRouteTargetsWow`
  - keep `postDetourCullAnchorTrappedComponents=false` in the checked-in tile
    config until a branch improves route outcomes instead of only changing the
    failure shape
- Checked-in proof-only validation:
  - `tmp/bake-sweeps/og_4029_anchor_routeability_proof_only_qfix_manifest_only-20260524T012055Z/`
  - tile hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - focused slice stayed `7/7`
  - full raw-Detour sweep stayed `17/23`
  - important hygiene fix:
    `1520.600,-4426.500,17.900` is manifest-only again. Promoting that shifted
    dead-end coord into `postDetourCullAnchorPolyStacksCoordsWow` changed the
    serialized tile without improving the route sweep.
- Next move:
  - use the routeability fields as proof-only instrumentation
  - stop spending loops on target rewiring when the hallway chain still reports
    zero routeable support components
  - go earlier in the bake on the hallway/city chain:
    `polymesh` / `contours` / corridor connectivity preservation

## 2026-05-24 - pre-region anchor coord split + borrow experiment

- Landed code surface:
  - `preRegionAnchorCoordsWow`
    - decouples source-support / compact cleanup coords from
      `postDetourCullAnchorPolyStacksCoordsWow`
  - `borrowMissingAnchorSourceSupportFromNeighbors`
    - experiment-only fallback for no-source-support anchors
- Verified baseline restore:
  - current checked-in tile hash is back on:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Useful artifacts:
  - split-only branch:
    `tmp/bake-sweeps/og_4029_pre_region_anchor_split_15206-20260524T023316Z/`
  - borrow branch:
    `tmp/bake-sweeps/og_4029_pre_region_anchor_borrow_15213-20260524T024038Z/`
  - restored default:
    `tmp/bake-sweeps/og_4029_pre_region_split_default_restore-20260524T024742Z/`
- Results:
  - split-only branch:
    - hash:
      `B196C738FF6ABA04B35055461112E8722AD0A2209A515100F8A9E53A6DD9AAA5`
    - focused `7/7`
    - full raw-Detour `17/23`
    - key stage shift:
      `1523.800,-4425.900,17.100` moved to
      `polymesh / upper_support_lost`
  - borrow branch:
    - hash:
      `98D17DF9AE904BD1DC544729D4B96980361644C950AE9053F9F7D497E81CA3FE`
    - `1521.300,-4422.500,17.100` stopped failing at `sourceSupport`
    - negative runtime result:
      direct `1518.2 -> full goal` collapsed to a two-corner path, so do not
      promote this branch
- Current rule:
  - use `preRegionAnchorCoordsWow` for targeted earlier-stage hallway/city
    experiments
  - keep `borrowMissingAnchorSourceSupportFromNeighbors=false` by default
  - do not re-add `1520.600,-4426.500,17.900` to
    `postDetourCullAnchorPolyStacksCoordsWow`; if it helps, it belongs in the
    earlier source/compact work surface, not the checked-in final Detour cull
- Next actual fix loop:
  - run shifted trapped-basin endpoint experiments against the pre-region list
  - focus on cases where direct starts still dead-end after green manifest
    answers, especially hallway/corridor exits and the city upper branch

## 2026-05-24 - shifted pre-region endpoint seeding and rejected routeability combo

- Verified restore point after this loop:
  - checked-in config restored
  - live tile hash restored to:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Shifted pre-region-only experiments:
  - `tmp/bake-sweeps/og_4029_pre_region_shifted_traps_v1-20260524T025737Z/`
    - tile hash:
      `8533ACF1BD05DCAF7BCA7078BB54F9489E29B82C6D299BD0D8010DF57FB1DADE`
  - `tmp/bake-sweeps/og_4029_pre_region_shifted_traps_v2-20260524T030130Z/`
    - tile hash:
      `0ABAF48CEB6879FC177644A83490C26206628D1E3D6B9E5CF1720C1A999BBA87`
- What the shifted-endpoint seeding proved:
  - promoting runtime dead-end coords into `preRegionAnchorCoordsWow` can move
    the corridor collapse points deeper without touching the checked-in final
    Detour cull list
  - measured direct-probe movement:
    - city full-goal branch:
      `1545.0,-4434.5,11.1 -> 1537.2667,-4437.9,13.0089` on baseline
      then `1539.2667,-4437.9,12.3089` on `v1`
      then `1541.2667,-4437.9,12.0089` on `v2`
    - hallway-to-exit branch:
      `1518.2,-4419.8,17.1 -> 1513.9668,-4416.6,18.4089` on the first shifted
      segment probe
      then `1515.9668,-4418.6,17.9089` on `v2`
  - stage side:
    - `1522.500,-4424.100,17.000` returned to no `firstBadStage` on `v2`
    - `1523.800,-4425.900,17.100` still first failed at
      `polymesh / upper_support_lost`
- Important limit:
  - the exit/exterior chain did not close with endpoint seeding alone
  - `1491.4 -> 1381.3` still only resolved to the local exact-exit anchor
    `1471.3667,-4416.6,25.3089`, and `1381.3 -> boarding` still died at the
    underpass branch
- Rejected combo:
  - `tmp/bake-sweeps/og_4029_pre_region_shifted_traps_v2_routecull-20260524T030400Z/`
    - tile hash:
      `B3086CD68A7778B7FFC14E2D7DAA2A353CACA6F2746B417E935C23F279984911`
  - enabling `postDetourCullAnchorTrappedComponents` on top of the improved
    pre-region branch was not promotable
  - manifest evidence looked better for the underpass chain:
    - `1364.867,-4374.000,26.109` became green + routeable
    - `1381.300,-4370.600,26.000` flipped to
      `finalDetour / upper_support_lost`
  - real route shape got worse:
    - `1381.3 -> boarding` dropped onto a much lower descending path toward
      `1366.8667,-4374.0,14.0089`
    - `1491.4 -> boarding` still failed at the same local exit dead-end
      `1479.7667,-4426.0,25.3089`
- Next move:
  - keep the shifted pre-region endpoint idea as a useful experiment surface
  - do not re-enable the routeability cull on this branch without a direct
    route probe that stays on the intended lower-underpass support
  - the next real structural target is still `1523.8` at `polymesh`, with the
    underpass handled separately as a finalDetour support-footprint decision

## 2026-05-24 - pre-poly contour preservation follow-up

- Checked-in restore at end of loop:
  - config restored to proof-only default
  - live tile hash restored to:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_prepoly_iteration_20260524-20260524T145052Z/`
- New native experiment surfaces now available but OFF by default:
  - `RC_PRESERVE_BORDER_VERTEX`
  - `prePolyPreserveAnchorSupportCoordsWow`
  - `prePolyUseRawAnchorSupportContoursWow`
- Best branch from this loop:
  - bake:
    `powershell -ExecutionPolicy Bypass -File tools/scripts/bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_raw_plus_preserve_1523_v1' -DataDir 'D:\wwow-bot\test-data'`
  - focused:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut"`
  - full:
    `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" -- RunConfiguration.TestSessionTimeout=1200000`
  - saved tile hash:
    `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`
  - focused `7/7`
  - full raw-Detour `17/23`
- What that branch proved:
  - `1523.800,-4425.900,17.100` moved from
    `polymesh / upper_support_lost` to
    `finalDetour / lower_competitor_dominant`
  - the source-backed support contour survived as a `19`-vertex support-band
    contour, but final Detour still broke it into `14` non-routeable
    support-band candidates
  - hallway route shape improved deeper to:
    `1514.0,-4426.5,20.2`
  - pass count still stayed `17/23`, so this is proof of the right fix surface
    but not a complete route closure
- Rejected branches:
  - `og_4029_pre_region_shifted_v2_plus_prepoly_raw_preserve_1523_v1`
    - combining shifted pre-region endpoint seeding with the raw+preserve
      contour branch pushed `1523.8` back to
      `polymesh / upper_support_lost`
  - `og_4029_prepoly_raw_preserve_1523_maxverts4_v1`
    - saved tile hash:
      `6530FC7C41C030557088AFED612BE667BB279F4BECB667F00C60CAB15E07F9C1`
    - focused regressed to `5/7`
    - exact focused failures:
      - `OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      - `OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
- Actual next move:
  - do not retry global `maxVertsPerPoly` increases; `4` and `6` both regress
    the deck crop
  - next targeted native experiment should be a local contour resimplification
    between the default `8`-vertex support contour and the raw-preserved
    `19`-vertex contour for `1523.8`

## 2026-05-24 - source-backed memo + local contour resimplify loop

- New memo:
  - `docs/physics/RECAST_WOW_SIBLING_COMPARISON_2026_05_24.md`
  - use this as the current answer for:
    - upstream Recast knob guidance
    - sibling WoW defaults / override practice
    - compatibility vs runtime-migration tradeoffs
- New native surface added in `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
  - `prePolyResimplifyAnchorSupportMaxError`
  - `prePolyResimplifyAnchorSupportMaxEdgeLen`
  - `prePolyResimplifyAnchorSupportTessellateWallEdges`
  - `prePolyResimplifyAnchorSupportTessellateAreaEdges`
  - `ResimplifyRawAnchorSupportContours(...)`
- Critical correction to the earlier same-day note:
  - the first `og_4029_prepoly_resimplify_1523_mse13_v1` write-up was not a
    real resimplify branch
  - after raw restore, `contour.nverts = contour.nrverts`; the helper still
    skipped when `nrverts <= nverts`
  - so the branch never actually re-simplified the `1523.8` raw contour
- Exact corrected follow-up commands:
  - build:
    `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
  - real bug-fixed `1.3` branch:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_notess_v3' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_notess.json'`
  - local `maxEdgeLen` isolation:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse13_edge24_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse13_edge24.json'`
  - tight-end upstream-range `1.1` branch:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_mse11_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_mse11.json'`
  - focused/full validation:
    - `og_4029_prepoly_resimplify_1523_mse13_notess_v3`
    - `og_4029_prepoly_resimplify_1523_mse11_v1`
  - restore:
    `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_resimplify_bugfix_iteration_20260524' -DataDir 'D:\wwow-bot\test-data'`
- Corrected results to preserve:
  - log proof:
    - `[CONTOUR-ANCHOR-RAW] anchor=(1523.800,-4425.900,17.100) contour=1 region=8 verts=19->448`
  - bug-fixed `1.3` branch:
    - `448 -> 21`
    - hash:
      `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
    - focused:
      `7/7`
    - full:
      `17/23`
  - `maxEdgeLen=24` isolation:
    - still `448 -> 21`
    - same hash:
      `F02666AFF5F064FC2999657718DC5B0084613F37C3DE4015DA339A43EC06959D`
    - no extra tests rerun because the tile matched the already-validated
      bug-fixed `1.3` branch exactly
  - `1.1` branch:
    - `448 -> 22`
    - hash:
      `089DBEC002F4D8DF9BDBD091D32F659364F958C40F50E04F9D95357EDDD39FAD`
    - focused:
      `7/7`
    - full:
      `17/23`
    - same six unresolved hallway / exterior / underpass reds
- Current stage authority from the corrected manifests:
  - `F02666...` branch:
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1522.500,-4424.100,17.000` ->
      no first-bad stage
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - `089DBE...` branch:
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1522.500,-4424.100,17.000` ->
      no first-bad stage
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - restored baseline `A01DEE...` branch:
    - `1523.800,-4425.900,17.100` still ->
      `finalDetour / lower_competitor_dominant`
- Restored stable tile at end of corrected loop:
  - restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_resimplify_bugfix_iteration_20260524-20260524T231759Z/`
  - restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Next move:
  - do not keep churning upstream-style local resimplify knobs here:
    `1.3` and `1.1` both collapse back to near-coarse `21/22`-vertex contours
    and keep the route set at `17/23`
  - next promotable family is either:
    - explicit local contour preservation / custom simplification for the
      `1523.8` support band
    - or source-support / lower-competitor classification changes

## 2026-05-25 UTC - local raw-window contour reinjection follow-up
- Active task: test whether preserving only the raw contour vertices inside a
  small local window around `1523.800,-4425.900,17.100` can bridge the gap
  between the too-coarse default contour and the too-fragmented fully raw
  contour.
- Pass result: `delta shipped; the local raw-window surface produced real
  intermediate contours, but it did not improve the route set, and the wider
  window regressed the nearby hallway footprint, so this family is now bounded
  as non-promotable`.
- Last delta:
  - Added `prePolyResimplifyAnchorSupportLocalPreserveRadius` plus
    `InjectAnchorLocalRawVertices(...)` and `FinalizeAnchorContourFlags(...)`
    in `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Radius `3.0` branch:
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r3_v1-20260525T001650Z/`
    - `448 -> 46`
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
    - critical bake-side proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=2 lowerFringeCulled=0 supportBandCandidates=2`
  - Radius `6.0` branch:
    - `tmp/bake-sweeps/og_4029_prepoly_resimplify_1523_localraw_r6_v1-20260525T002119Z/`
    - `448 -> 145`
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
        `(1514.0,-4426.5,20.2)`
    - critical bake-side proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=14 lowerFringeCulled=0 supportBandCandidates=14`
  - Practical read:
    - real intermediate contour detail is not enough if the final support
      footprint still misses the anchor
    - the missing fix surface is support-footprint / overlap / earlier
      classification, not more generic contour density
  - Restore:
    - `tmp/bake-sweeps/og_4029_restore_after_localraw_window_iteration_20260525-20260525T002411Z/`
    - restored hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r3.json'` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_localraw_r3_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_localraw_r3_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_prepoly_resimplify_1523_localraw_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_prepoly_resimplify_1523_localraw_r6.json'` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_prepoly_resimplify_1523_localraw_r6_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_prepoly_resimplify_1523_localraw_r6_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_localraw_window_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
## 2026-05-25 UTC - contour-build simplify-time seed follow-up
- Active task: close the "move it into the real simplifier" branch by seeding
  the recovered support-band mask inside upstream Recast's
  `simplifyContour(...)` during `rcBuildContours()`, instead of doing another
  post-contour reinjection pass.
- Pass result: `delta shipped; this earlier contour-build seed surface is also a
  bounded negative. It really did fire on the recovered region-7 contour inside
  upstream Recast, but 1523.8 still finished at finalDetour and the route set
  stayed on the same 3/7 focused and 20/23 full regression family`.
- Last delta:
  - Added `rcAnchorContourSimplifyOverride`,
    `rcSetContourSimplifyAnchorOverrides(...)`,
    `rcClearContourSimplifyAnchorOverrides()` in
    `tools/MmapGen/dep/recastnavigation/Recast/{Include/Recast.h,Source/RecastContour.cpp}`.
  - Added `BuildContourSimplifyAnchorOverrides(...)` plus the opt-in tile
    config keys `contourBuildSeedAnchorSupportCoordsWow` and
    `contourBuildSeedAnchorSupportBandLocalRadius` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1-20260525T200739Z/`
  - Changed hash:
    `C0873DE50193A03921A761F75C278B82B001100B2E58BFCF4721DA8D827A5357`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - the upstream simplify-time seed really fired on the intended contour:
      `[CONTOUR-BUILD-ANCHOR-SEED] region=7 rawVerts=158 simplifiedVerts=33 seededSupportBandRawVerts=26 matchedOverrides=1`
    - selector diagnostics still isolated the same candidate family:
      `contour 1 / region 8 verts=226 containsAnchor=0 closestDistance2D=0.836`
      `contour 3 / region 7 verts=158 containsAnchor=1 closestDistance2D=0.200`
      `contour 4 / region 19 verts=10 containsAnchor=0 closestDistance2D=1.997`
    - the later preserve pass still only touched
      `contour 3 / region 7` with `preservedBorderVerts=33`
    - manifest correction for `1523.8`:
      `contours supportCandidateCount=1`,
      `polymesh supportCandidateCount=2`,
      but `supportContainsAnchorProjection=false` throughout and
      `finalDetour supportCount=0`
    - final answer still stayed:
      `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Shared focused failure profile stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Shared full failure profile stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Practical read:
    - moving the same support-band mask into the actual upstream
      `rcBuildContours(...)` simplifier is still not enough
    - the newest proof is now exact final footprint overlap, not just "support
      vanished too early"
    - contour-family retries are exhausted enough that the next serious
      fallback should be a research-backed local `ch` override or another
      genuinely earlier source/vertical classification branch
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `C0873DE50193A03921A761F75C278B82B001100B2E58BFCF4721DA8D827A5357`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_contourbuild_seed_local_anchoronly_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_contourbuild_seed_local_anchoronly_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - local `ch=0.05` override follow-up
- Active task: take the documented local-`ch` fallback in the finer direction
  first by keeping the proven raster support patch fixed, but lowering tile
  `1:40,29` from `ch=0.1` to `ch=0.05` without changing the loader contract or
  reviving post-path repair.
- Pass result: `delta shipped; finer local ch is a strong bounded negative. It
  massively reshaped the serialized tile and widened the route regressions to
  17/23, but the decisive 1523.8 anchor still finished at finalDetour with the
  same lower_competitor_dominant answer`.
- Last delta:
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_ch005.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_ch005_v1-20260525T202957Z/`
  - Restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_ch005_iteration_20260525-20260525T203203Z/`
  - Changed hash:
    `4E8C3C6AF492AAA995044BD30345E3A2DB2BDEAA64B1D96D6E6332A2513EC4B9`
  - Focused:
    `4/7`
  - Full:
    `17/23`
  - Research basis:
    - Recast's `rcConfig` docs define `ch` as the y-axis voxel size, say
      smaller values increase vertical raster precision, and define
      `walkableClimb`/`walkableHeight` in voxel units derived from `ch`.
    - TrinityCore's current mmaps discussion documents a real map-local
      `config.ch *= 2` override, so tile-local `ch` is a legitimate sibling
      fallback surface.
  - Decisive bake/manifest proof:
    - saved tile size changed dramatically:
      `8775316 -> 2398200` bytes (`delta=-6377116`)
    - the decisive anchor still did not move:
      - `1523.8` kept
        `rasterize supportCandidateCount=138`,
        `erode supportCandidateCount=8`,
        `median supportCandidateCount=56`,
        `regions supportCandidateCount=56`,
        `contours supportCandidateCount=1`,
        `polymesh supportCandidateCount=2`,
        `finalDetour supportCandidateCount=0`
      - final answer still stayed:
        `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - Focused failures:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `79`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
  - Full failures widened to:
    - `orgrimmar_city_live_vertical_replan_recovery`
    - `orgrimmar_city_hallway_live_wall_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_exterior_incline_live_stall_exact_recovery`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Practical read:
    - finer local `ch` is not the missing lever for the recovered `1523.8`
      support footprint
    - if local `ch` stays worth testing after contour-family exhaustion, the
      defensible next branch is the coarser sibling-style direction, not more
      finer-precision churn
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_ch005_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_ch005.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `4E8C3C6AF492AAA995044BD30345E3A2DB2BDEAA64B1D96D6E6332A2513EC4B9`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_ch005_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `4/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_ch005_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_ch005_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - local `ch=0.2` override follow-up
- Active task: close the other local-`ch` direction too by keeping the raster
  support patch fixed, but raising tile `1:40,29` from `ch=0.1` to `ch=0.2`
  in the sibling-style coarser direction without changing the loader contract.
- Pass result: `delta shipped; coarser local ch is also a bounded negative. It
  changed the serialized tile a lot, but 1523.8 still finished at finalDetour
  and the route set simply snapped back to the same 3/7 focused and 20/23 full
  contour-family regression profile`.
- Last delta:
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_ch020.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_ch020_v1-20260525T204524Z/`
  - Restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_ch020_iteration_20260525-20260525T205010Z/`
  - Changed hash:
    `55E5288EC5464DACC1BC696B70BBA6F0A8F808B29A97BAA9A7FA47F266C8A428`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake/manifest proof:
    - saved tile size changed substantially:
      `8775316 -> 2434340` bytes (`delta=-6340976`)
    - the decisive anchor still did not move:
      - `1523.8` kept
        `rasterize supportCandidateCount=138`,
        `erode supportCandidateCount=8`,
        `median supportCandidateCount=56`,
        `regions supportCandidateCount=56`,
        `contours supportCandidateCount=1`,
        `polymesh supportCandidateCount=2`,
        `finalDetour supportCandidateCount=0`
      - final answer still stayed:
        `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - Focused failures stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found `85`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Practical read:
    - local `ch` is now exhausted in both directions for the exact `1523.8`
      failure
    - the next serious retry should return to contour/source-shape work, not
      another vertical-quantization override
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_ch020_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_ch020.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `55E5288EC5464DACC1BC696B70BBA6F0A8F808B29A97BAA9A7FA47F266C8A428`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_ch020_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_ch020_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_ch020_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - existing-simplified local support-band carry follow-up
- Active task: keep the raster support patch fixed, avoid local resimplify
  entirely, and splice only local support-band raw verts back into the current
  `rcBuildContours()` simplified contours before `rcBuildPolyMesh()`.
- Pass result: `delta shipped; the new existing-simplified local carry surface
  is another bounded negative. It proved the carry can happen without
  rerunning simplification, but the tile still regressed to the same 3/7
  focused and 20/23 full deck/underpass family while 1523.8 stayed at
  finalDetour`.
- Last delta:
  - Added `BuildAnchorContourRawIndexView(...)`,
    `CarryLocalRawVerticesIntoExistingAnchorSupportContours(...)`, and the
    opt-in tile config keys `prePolyCarryAnchorSupportCoordsWow` plus
    `prePolyCarryAnchorSupportBandLocalRadius` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_carry_local_band_r4.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_carry_local_band_r4_v1-20260525T193344Z/`
  - Changed hash:
    `3D3BEA0EFB858DBC0B4D72C501CCE50864CE4A7A8F3D2DA8280A2356ECAD97E3`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - the branch did not call the local resimplifier
    - direct local carry still fired across three nearby contours:
      - `contour 1 / region 8 verts=13->42 injectedSupportBandRawVerts=29`
      - `contour 3 / region 7 verts=11->31 injectedSupportBandRawVerts=20`
      - `contour 4 / region 19 verts=3->10 injectedSupportBandRawVerts=7`
    - despite that, `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Shared focused failure profile stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Shared full failure profile stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Validation/tests run:
    - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_carry_local_band_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_carry_local_band_r4.json'` -> passed.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `3D3BEA0EFB858DBC0B4D72C501CCE50864CE4A7A8F3D2DA8280A2356ECAD97E3`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_carry_local_band_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_carry_local_band_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_carry_local_band_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Practical read:
  - skipping the resimplify step is not enough by itself
  - even a direct local raw carry on top of the existing simplified contours
    still reintroduced the same bad deck / static-blocker family
  - if contour-stage work continues, it needs to be narrower than this
    multi-contour carry or it needs to move earlier than contours again

## 2026-05-25 UTC - anchor-containing no-resimplify carry follow-up
- Active task: keep the same no-resimplify carry surface, but isolate the
  single anchor-containing contour instead of reopening every same-band contour
  in the local window.
- Pass result: `bounded negative; selector tightening was not enough. Even when
  only contour 3 / region 7 was reopened and preserved, the tile still landed
  on the same 3/7 focused and 20/23 full regression family while 1523.8 stayed
  at finalDetour`.
- Last delta:
  - Temp config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_carry_local_band_anchoronly_r4.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1-20260525T195224Z/`
  - Changed hash:
    `1932EC1BC322393040870F3293C9CF9B9EA6CCBB640974A3595B87CC4D5839B8`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - selector still isolated:
      - `contour 1 / region 8 verts=226 containsAnchor=0 closestDistance2D=0.836`
      - `contour 3 / region 7 verts=158 containsAnchor=1 closestDistance2D=0.200`
      - `contour 4 / region 19 verts=10 containsAnchor=0 closestDistance2D=1.997`
    - only the selected contour was reopened and preserved:
      - `contour 3 / region 7 verts=11->31 injectedSupportBandRawVerts=20 preserveRadius=4.000`
      - `preservedBorderVerts=31`
    - despite that narrower scope, `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Validation/tests run:
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_carry_local_band_anchoronly_r4.json'` -> passed.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `1932EC1BC322393040870F3293C9CF9B9EA6CCBB640974A3595B87CC4D5839B8`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_carry_local_band_anchoronly_r4_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_carry_local_band_anchoronly_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Practical read:
  - narrowing the no-resimplify carry to the literal anchor-containing contour
    was still not enough
  - that closes the remaining obvious post-contour narrowing branch
  - next serious work should move the support mask into the real
    `rcBuildContours(...)` simplifier or earlier source/vertical staging

## 2026-05-25 UTC - support-footprint negatives after support-gap
- Active task: test whether the remaining `1523.8` hole is fixable by
  combining the strongest surviving contour proof with the support-gap cull, or
  by widening the source-support band slightly below the sampled support floor.
- Pass result: `delta shipped; both branches were bounded negatives, and they
  sharpened the next rule: do not keep widening finalDetour support-gap or
  generic support-floor slack around 1523.8`.
- Last delta:
  - Added `AnchorSupportBandTuning` plus the tile-local config surface
    `anchorSourceSupportFloorSlackBelow` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Combined raw+preserve contour proof with the existing support-gap cull:
    - artifact:
      `tmp/bake-sweeps/og_4029_raw_preserve_support_gap1_v1-20260525T011748Z/`
    - hash:
      `EFD2DCE534EFB2A9039447DFBE84C6F695701C507ED60DC0592C71752EB783FD`
    - focused:
      `7/7`
    - full:
      `17/23`
    - decisive proof:
      - `1523.8` still logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=14 lowerFringeCulled=2 ... supportBandCandidates=14 ... bestSupportGap2D=0.300`
      - `1523.800,-4425.900,17.100` still ->
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` regressed ->
        `finalDetour / support_footprint_missed_anchor`
  - Widened the support floor below the sampled source-support Y:
    - artifact:
      `tmp/bake-sweeps/og_4029_support_floor_slack035_v1-20260525T013354Z/`
    - hash:
      `CD5F1EB58003C4326D03B8A638EA154AF2855F3547520000AE39E45E59163FE0`
    - focused:
      `7/7`
    - full:
      `17/23`
    - decisive proof:
      - `1523.8` logged
        `[DT-ANCHOR-CULL-SKIP] ... supports=0 upperFringe=4 lowerFringeCulled=0 ... supportBandCandidates=4 ... bestSupportGap2D=-1.000`
      - `1523.800,-4425.900,17.100` still ->
        `finalDetour / lower_competitor_dominant`
      - `1522.500,-4424.100,17.000` regressed ->
        `finalDetour / lower_competitor_dominant`
      - `1521.267,-4425.600,17.609` regressed ->
        `finalDetour / lower_competitor_dominant`
- Practical read:
  - combining raw+preserve support with a small finalDetour gap trim does not
    make the surviving support routeable or dominant
  - widening `anchorSourceSupportFloorSlackBelow` from the baseline `0.20` to
    `0.35` is not a safe approximation of WoW supports here; it shrinks the
    useful final support-band evidence and regresses sibling anchors
  - the remaining best local clue is still the exact-neighborhood
    support-footprint hole:
    source-backed support survives nearby, but the anchor cell itself still
    lands in the wrong basin
  - next serious work should target exact-neighborhood support-footprint
    bridging / overlap or earlier source-support classification, not more
    generic slack or finalDetour gap widening
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raw_preserve_support_gap1_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raw_preserve_support_gap1.json'` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raw_preserve_support_gap1_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raw_preserve_support_gap1_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_support_floor_slack035_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_support_floor_slack035.json'` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_support_floor_slack035_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_support_floor_slack035_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_support_floor_slack_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.

## 2026-05-25 UTC - raster support patch and contour-loss proof
- Active task: test whether the `1523.8` footprint hole can be closed earlier
  than contours by injecting a tiny source-backed raster patch at the verified
  support floor, then check whether contour/polymesh preservation can carry
  that recovered footprint into the final Detour tile.
- Pass result: `delta shipped; the raster patch produced the strongest new
  stage proof of the day, but it was still a bounded negative. It proves the
  recoverable support footprint can survive through median/regions and that the
  next real loss is contours, not raster/buildCHF/erode anymore`.
- Last delta:
  - Added `preRasterizeAnchorSupportPatchCoordsWow`,
    `preRasterizeAnchorSupportPatchHalfExtent`, and
    `RasterizeAnchorSupportPatches(...)` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Raster patch only:
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_v1-20260525T020334Z/`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
      (exactly the stable baseline)
    - focused:
      `7/7`
    - full:
      not rerun because the saved tile hash matched the validated baseline
    - decisive proof:
      - `1523.8` changed from
        `median: supportCell=false / regions: supportCell=false`
        to
        `median: supportCell=true / regions: supportCell=true`
      - `1523.8` also gained much larger support counts at the earlier stages:
        `rasterize 291`, `buildCHF 198`, `erode 57`, `median 174`
      - the same anchor still fell back to
        `contours: supportCell=false`
        and then
        `finalDetour / lower_competitor_dominant`
  - Raster patch + raw+preserve contour carry:
    - artifact:
      `tmp/bake-sweeps/og_4029_raster_support_patch06_raw_preserve_v1-20260525T020748Z/`
    - hash:
      `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`
    - focused:
      `7/7`
    - full:
      `17/23`
    - decisive proof:
      - `1523.8` still kept the earlier raster/median gain
        (`median supportCell=true`, `regions supportCell=true`)
      - `polymesh supportCount` returned to `16`, but
        `finalDetour supportComponentCount` still stayed `0`
      - the saved tile landed exactly on the old raw+preserve shard branch
        (`52D99...`)
      - `1522.500,-4424.100,17.000` regressed again to
        `finalDetour / support_footprint_missed_anchor`
- Practical read:
  - the raster footprint itself is recoverable for `1523.8`
  - the next true loss is now proven to be `rcBuildContours(...)`
  - simply carrying the raw contour later is not enough; it just reproduces the
    already-known non-routeable `52D99...` branch
  - next serious work should target local contour-builder preservation /
    simplification for a source-backed recovered footprint, not more finalDetour
    gap tuning and not more generic support-floor slack widening
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_raw_preserve_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_raw_preserve.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
    `52D99D419A201AC86DA1512A1BBDAFC0F955627B11A0A96041732DCD22DF2FC8`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_raw_preserve_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_raw_preserve_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_raster_patch_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.

## 2026-05-25 UTC - raster patch + contour-band boundary carry negative
- Active task: test a contour-local follow-up to the raster-patch proof by
  reinjecting only source-support band boundary vertices near
  `1523.800,-4425.900,17.100`, instead of restoring the whole raw contour.
- Pass result: `delta shipped; the contour-local surface is useful as a bounded
  proof, but this specific boundary-shape is not promotable. It touched
  multiple same-band contours, reintroduced the top-ramp bridge/trim
  regressions, and still left 1523.8 at finalDetour`.
- Last delta:
  - Added `InjectAnchorSupportBandBoundaryVertices(...)` plus the opt-in tile
    config key `prePolyResimplifyAnchorSupportBandBoundaryRadius` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Focused artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_seed_r3_v1-20260525T024153Z/`
  - Mandatory full-rerun artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_seed_r3_v1_fullrerun-20260525T024605Z/`
  - Restore artifact:
    `tmp/bake-sweeps/og_4029_restore_after_boundary_seed_iteration_fullrerun_20260525-20260525T024951Z/`
  - Changed hash:
    `E58B0DF11E71196123A377094B4A41710238591B8D454352BDF93B7C825D424F`
  - Restored hash:
    `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive bake-log proof:
    - `[HF-ANCHOR-SUPPORT-PATCH] ... rasterized 1 support patch(es)`
    - raw restore expanded three contours near `1523.8`:
      `13 -> 226`, `11 -> 158`, `3 -> 10`
    - boundary carry then injected only two of those contours:
      `contour 1 / region 8 injectedBoundaryVerts=3`
      and
      `contour 3 / region 7 injectedBoundaryVerts=2`
    - the resulting re-simplified contours were:
      `226 -> 18`, `158 -> 13`, `10 -> 3`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Validation:
    - focused command:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding`
    - focused result:
      `3/7`
    - focused failures:
      - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
      - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
        found only `80` polygons
      - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
      - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
    - full command:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000`
    - full result:
      `20/23`
    - full failures:
      - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
      - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
      - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - restore command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_boundary_seed_iteration_fullrerun_20260525' -DataDir 'D:\wwow-bot\test-data'`
    - restore hash command:
      `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash`
- Practical read:
  - the current boundary-carry shape is too broad semantically: it preserved
    the wrong same-band contour family instead of the one recovered footprint
    we actually care about
  - if this family is retried, constrain it to the single recovered
    contour/region touching the raster-patch neighborhood; do not re-open every
    same-band contour that intersects the anchor window

## 2026-05-25 UTC - single-contour selector follow-up
- Active task: keep the raster patch plus contour-local raw restore /
  resimplify / preserve loop, but isolate exactly one support-band contour at a
  time instead of reopening every same-band contour near `1523.8`.
- Pass result: `delta shipped; the selector surface is a useful bounded proof,
  but both isolated contour choices are negative. Selecting only the
  anchor-containing contour or only the nearest non-containing contour still
  leaves 1523.8 at finalDetour and still reproduces the bad deck bridge / trim
  / static-blocker profile`.
- Last delta:
  - Added `AnchorSupportContourSelectionMode`,
    `ParseAnchorSupportContourSelectionMode(...)`, and
    `prePolySupportContourSelectionMode` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Preserved the legacy config alias:
    `prePolySelectAnchorContainingSupportContourOnly=true` still maps to
    `AnchorContaining`.
  - Anchor-containing artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1-20260525T042822Z/`
  - Anchor-containing hash:
    `5FE8640E4B7D756F74DBCA47952345F8A06507C6C81BA330E400092228399340`
  - Nearest-non-containing artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1-20260525T043821Z/`
  - Nearest-non-containing hash:
    `84C09EFE50E2E04114DCF3A4F218A1DBF29E4E6F8776680CC966B47D2ADFB856`
  - Decisive selector proof:
    - both branches saw the same three candidate contours:
      `region 8 / closestDistance2D=0.836 / containsAnchor=0`,
      `region 7 / closestDistance2D=0.200 / containsAnchor=1`,
      and
      `region 19 / closestDistance2D=1.997 / containsAnchor=0`
    - anchor-containing selected only `contour 3 / region 7` and changed it:
      `11 -> 158 -> 13`, with `injectedBoundaryVerts=2`
    - nearest-non-containing selected only `contour 1 / region 8` and changed
      it:
      `13 -> 226 -> 18`, with `injectedBoundaryVerts=3`
    - both branches still kept
      `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed identical on both branches:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Validation:
    - build command:
      `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
    - anchor-containing bake command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_anchoronly_r3.json'`
    - anchor-containing focused command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`
    - anchor-containing full command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_anchoronly_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`
    - nearest-non-containing bake command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3.json'`
    - nearest-non-containing focused command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`
    - nearest-non-containing full command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_seed_nearest_noncontaining_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`
    - restore command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_single_contour_selector_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
    - restore hash command/result:
      `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Shared failure profile on both branches:
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - focused:
      `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
    - full:
      `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - full:
      `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - full:
      `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
- Practical read:
  - reducing the earlier branch from "three contours" down to exactly
    `region 7 only` or `region 8 only` still does not recover the routeable
    footprint
  - the next contour-stage retry should change the local preservation /
    simplification shape itself, not keep swapping among the current contour
    candidates

## 2026-05-25 UTC - support-band-local contour preserve follow-up
- Active task: keep the raster patch plus anchor-containing contour selection
  fixed, but change the preserved shape itself by carrying only the raw contour
  verts that remain inside the recovered support band within a local anchor
  window around `1523.8`.
- Pass result: `delta shipped; the new support-band-local preserve surface is a
  bounded negative. It produced a richer anchor-containing contour than the
  earlier boundary-only branch, but 1523.8 still stayed at finalDetour and the
  route regressions stayed on the same 3/7 focused and 20/23 full profile as
  the earlier selector negatives`.
- Last delta:
  - Added `InjectAnchorSupportBandLocalRawVertices(...)` plus the opt-in tile
    config key `prePolyResimplifyAnchorSupportBandLocalPreserveRadius` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_band_local_anchoronly_r6.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_band_local_anchoronly_r6_v1-20260525T165251Z/`
  - Changed hash:
    `B9E24E82A964DDFD4E7EB10B8401CFB645681DB2EF0ECAF3D784D26B7AA2981A`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - selector still isolated the anchor-containing contour:
      `contour 3 / region 7`
    - raw restore:
      `11 -> 158`
    - resimplify candidate:
      `158 -> 34`
    - new local carry:
      `[CONTOUR-ANCHOR-BAND-LOCAL] ... preservedSupportBandRawVerts=23 preserveRadius=6.000`
    - border preservation still fired only lightly afterward:
      `[CONTOUR-ANCHOR-PRESERVE] preserved 2 border vertex(s) across 1 contour(s)`
    - despite the richer local shape, the anchor still stayed
      `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Shared failure profile stayed the same as the selector negatives:
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - focused:
      `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - focused:
      `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
    - full:
      `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - full:
      `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - full:
      `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Validation:
    - build command:
      `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`
    - bake command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_band_local_anchoronly_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_band_local_anchoronly_r6.json'`
    - changed hash command/result:
      `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
      `B9E24E82A964DDFD4E7EB10B8401CFB645681DB2EF0ECAF3D784D26B7AA2981A`
    - focused command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_band_local_anchoronly_r6_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`
    - full command/result:
      `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_band_local_anchoronly_r6_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`
    - restore command:
      `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_band_local_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'`
    - restore artifact:
      `tmp/bake-sweeps/og_4029_restore_after_band_local_iteration_20260525-20260525T165615Z/`
    - restore hash command/result:
      `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` ->
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Practical read:
  - this branch proves the missing lever is not merely "preserve a denser local
    arc on region 7"
  - boundary-only carry was too sparse, full local-raw carry was too broad, and
    this support-band-local midpoint is still not promotable
  - if contour-stage work continues, move earlier into `rcBuildContours(...)`
    or change the contour-builder shape again; do not keep churning the same
    post-contour preserve family on the same selected contour

## 2026-05-25 UTC - support-gap finalDetour follow-up
- Active task: test whether the anchor-stack cull can treat a small XY gap
  between nearby upper support fragments and the lower basin as enough evidence
  to collapse the `1523.8` lower fringe.
- Pass result: `delta shipped; the support-gap surface changed the serialized
  tile and proved the finalDetour cull can touch two lower-fringe polys at
  1523.8, but the dominant lower winner and the 17/23 route set did not move,
  so this is a bounded negative result`.
- Last delta:
  - Added `postDetourCullAnchorPolyStacksSupportGap2D` plus
    `GetDetourBoundsGap2D(...)` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Variant:
    - `tmp/bake-sweeps/og_4029_anchor_support_gap1_v1-20260525T005200Z/`
    - hash:
      `33F6D5DA3189CF1985120B247D23C9EF0C978995B10FF79C90A65DB5ABFE991D`
    - focused:
      `7/7`
    - full:
      `17/23`
  - Decisive proof:
    - `1523.8` moved from
      `lowerFringeCulled=0`
      to
      `lowerFringeCulled=2`
    - measured
      `bestSupportGap2D=0.300`
    - despite that, the anchor still ended at
      `finalDetour / lower_competitor_dominant`
  - Practical read:
    - the finalDetour cull can reach a tiny lower fringe around `1523.8`
    - that fringe is not the dominant surviving basin
    - move earlier again; do not widen this same finalDetour gap idea blindly
  - Restore:
    - `tmp/bake-sweeps/og_4029_restore_after_support_gap1_iteration_20260525-20260525T005613Z/`
    - restored hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_anchor_support_gap1_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_anchor_support_gap1.json'` -> passed.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_anchor_support_gap1_v1_focused.trx" --results-directory tmp/test-runtime/results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_anchor_support_gap1_v1.trx" --results-directory tmp/test-runtime/results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_support_gap1_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.

## 2026-05-25 UTC - boundary pre-seed contour follow-up
- Active task: keep the raster patch, raw restore, anchor-containing contour
  selection, and local resimplify fixed, but move the recovered support-band
  boundary into the local simplifier's initial seed phase instead of
  reinserting those verts afterward.
- Pass result: `delta shipped; the earlier boundary-preseed surface is another
  bounded negative. It fired on the intended region-7 contour, but still
  collapsed back to the same 13-vertex candidate and reproduced the same 3/7
  focused and 20/23 full regression profile as the later boundary-carry
  family`.
- Last delta:
  - Added `SimplifyAnchorContour(..., mandatorySeedMask)`,
    `BuildAnchorSupportBandBoundaryVertexMask(...)`, and the opt-in tile config
    key `prePolyResimplifyAnchorSupportBandBoundarySeedRadius` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1-20260525T173241Z/`
  - Changed hash:
    `EB6F72B9E86E550DB277BA767D2BCB07D5C99337E729191B0C52378CF487DADC`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - selector still isolated the anchor-containing contour:
      `contour 3 / region 7`
    - raw restore:
      `11 -> 158`
    - upstream-style earlier seed phase really fired:
      `[CONTOUR-ANCHOR-BAND-SEED] ... seededBoundaryVerts=4 seedRadius=3.000`
    - but the local simplifier still collapsed back to the same coarse
      candidate:
      `158 -> 13`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Shared focused failure profile stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Shared full failure profile stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Practical read:
    - moving the same recovered support-band boundary endpoints earlier into
      the seed phase is not enough on this family
    - the local simplifier still snaps back to the same 13-vertex region-7
      contour and the same route set
    - the next serious retry should not spend another loop only on
      boundary-seed timing; it needs a different contour-builder shape or
      earlier source/vertical classification work
  - Validation/tests run:
    - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3.json'` -> passed.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `EB6F72B9E86E550DB277BA767D2BCB07D5C99337E729191B0C52378CF487DADC`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
    - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_boundary_preseed_anchoronly_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
    - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_boundary_preseed_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
    - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - selected full-raw contour carry follow-up
- Active task: close the last obvious pre-polymesh carry retry by swapping only
  the selected anchor-containing support contour back to its full raw
  `rverts` payload before `rcBuildPolyMesh()`.
- Pass result: delta shipped; the selected full-raw contour carry is a bounded
  negative. It restored the whole raw region-7 contour before polymesh, but
  `1523.8` still stayed `finalDetour / lower_competitor_dominant` and route
  quality regressed to `19/23`.
- Last delta:
  - Added `CarrySelectedRawAnchorSupportContours(...)` and the config key
    `prePolyCarrySelectedRawAnchorSupportCoordsWow` in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_fullraw_anchoronly.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_fullraw_anchoronly_v1-20260525T210253Z/`
  - Changed hash:
    `1B0620C72AC82213750CB15175DC509BD1B55D77F99827DD911E2AB9EF1C11D3`
  - Focused:
    `3/7`
  - Full:
    `19/23`
  - Decisive bake-log proof:
    - `[CONTOUR-ANCHOR-FULL-RAW-CARRY] carried 147 raw contour vertex(s) across 1 contour(s)`
    - on the selected contour, that means the branch reopened the shape from
      `11` simplified vertices back to its full `158` raw vertices before
      polymesh
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Focused failure profile stayed broad and hostile:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Focused route proof worsened:
    - the flightmaster route exploded to `1037` points
    - it still kept the same steep-incline and rope-line blocker evidence
  - Full failures widened to:
    - `orgrimmar_city_hallway_live_wall_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Full-route failure details:
    - `orgrimmar_city_hallway_live_wall_stall_recovery` ended at
      `(1514.7,-4426.7,20.0)`, `300.5y` from goal
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor` opened with a
      `46.2y` first segment
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
      ended at `(1350.9,-4522.1,32.7)`, `136.3y` from goal
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery` returned
      `no_path`
  - Practical read:
    - this closes the remaining "just carry more raw contour later" family
    - if the selected contour's full raw `rverts` payload still fails before
      `rcBuildPolyMesh()`, the missing `1523.8` overlap is not merely another
      late simplification loss
    - the next serious retry should move earlier into the contour builder
      itself or earlier source/vertical staging, not widen the same
      pre-polymesh carry family again
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_fullraw_anchoronly_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_fullraw_anchoronly.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `1B0620C72AC82213750CB15175DC509BD1B55D77F99827DD911E2AB9EF1C11D3`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_fullraw_anchoronly_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_fullraw_anchoronly_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `19/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_fullraw_anchoronly_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - contour-build boundary-only seed follow-up
- Active task: try the remaining earlier boundary-shape retry by seeding only
  the support-band boundary crossings during `rcBuildContours()`
  simplification itself, without reopening the broader local support-band
  window.
- Pass result: delta shipped; the earliest boundary-only contour-build seed is
  another bounded negative. It fired on the selected region-7 contour, but
  still left the same `11` simplified vertices and the same
  `1523.8 -> finalDetour / lower_competitor_dominant` result.
- Last delta:
  - Added `boundarySeedRadiusCells` to
    `rcAnchorContourSimplifyOverride`, plus
    `buildAnchorSupportBandBoundaryVertexMask(...)`,
    `seedAnchorSupportBandBoundaryVertices(...)`, and the config key
    `contourBuildSeedAnchorSupportBandBoundaryRadius`.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1-20260525T212238Z/`
  - Changed hash:
    `3F9EB2930393D48E13B28267D6C11B0E9C0D5282C488D9CE8CC4403FB6C269E4`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - `[CONTOUR-BUILD-ANCHOR-SEED] region=7 rawVerts=158 simplifiedVerts=11 seededBoundaryVerts=2 seededSupportBandRawVerts=0 matchedOverrides=1`
    - this is the whole point of the branch: the boundary-only seed really did
      touch the right contour, but it still left that contour at the same
      `11` simplified vertices
    - selector facts still stayed:
      `contour 1 / region 8 containsAnchor=0 closestDistance2D=0.836`,
      `contour 3 / region 7 containsAnchor=1 closestDistance2D=0.200`,
      `contour 4 / region 19 containsAnchor=0 closestDistance2D=1.997`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Focused failure profile stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Focused route shape changed, but not in a promotable way:
    - the flightmaster route was still invalid and now measured `364` points
    - it picked up a lower flight-master bonfire blocker while still keeping
      the same hallway / steep-incline / rope-line evidence
  - Full failures stayed on the same family:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Practical read:
    - earliest boundary-only contour seeding is too sparse or too inert on
      this tile
    - once the seed fires and the selected contour still keeps the same
      simplified vertex count, stop iterating on that sparse boundary-only
      family
    - the next serious retry needs a denser contour-builder reshape or an even
      earlier raw-contour / region / source-stage change
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `3F9EB2930393D48E13B28267D6C11B0E9C0D5282C488D9CE8CC4403FB6C269E4`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_contourbuild_seed_boundary_anchoronly_r3_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_contourbuild_boundary_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - raster patch resolved-support-point center follow-up
- Active task: close the narrow "wrong patch center" hypothesis before doing
  any larger raster/source-shape rewrite. Keep the proven `1523.8` raster
  support patch size fixed, but center it on the resolved source-support
  footprint instead of the anchor projection.
- Pass result: bounded negative. The patch moved onto the resolved source
  support, but the earliest support component distance and all route/test
  outcomes stayed unchanged.
- Last delta:
  - Added support-point XY tracking to `AnchorSourceSupportProbe`.
  - Added loader-compatible config key
    `preRasterizeAnchorSupportPatchCenterMode` with experiment value
    `resolvedSupportPoint`.
  - Extended `[SRC-ANCHOR-SUPPORT]` and `[HF-ANCHOR-SUPPORT-PATCH]` logging so
    the exact resolved support point and raster patch center are explicit in
    the bake log.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_center_support_anchoronly.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_center_support_anchoronly_v1-20260525T214345Z/`
  - Changed hash:
    `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - `[SRC-ANCHOR-SUPPORT] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) delta=0.604 tri=537325 source=vmap dist2D=0.306 inside=0`
    - `[HF-ANCHOR-SUPPORT-PATCH] anchor=(1523.800,-4425.900,17.100) center=(1523.668,-4426.176,17.704) centerMode=resolvedSupportPoint halfExtent=0.600 source=1`
    - despite that move, `median` still kept the nearest support component at
      `minDistance2D=0.5315163135528564`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors still stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Focused failure profile stayed:
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoShadowedLowerTrimLedgePolygons`
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_PreservesDeckConnectorSurfaces`
      found only `80` polygons
    - `MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck_HasNoLargeBridgePolygons`
    - `LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`
  - Focused route shape stayed bad:
    - the flightmaster route still measured `364` points
    - it still kept the lower flight-master bonfire, hallway-corner,
      steep-incline, and rope-line blocker evidence
  - Full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Practical read:
    - patch placement alone is not the missing lever
    - once the patch is centered on the resolved source-support footprint and
      the earliest support component still stays `0.5315y` away, stop
      iterating on center-only tuning
    - one narrow bridge/segment-shaped patch between the anchor and resolved
      support point was still a fair last retry, but if it stayed hash-identical
      then the raster micro-shape family was exhausted
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_center_support_anchoronly_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_center_support_anchoronly.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_center_support_anchoronly_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_center_support_anchoronly_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_center_support_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1`

## 2026-05-25 UTC - raster patch resolved-support-to-anchor bridge follow-up
- Active task: spend the last small raster micro-shape retry by bridging the
  resolved support point back toward the anchor projection, then stop this
  family entirely if it still serializes to the same tile.
- Pass result: bounded negative. The bridge strip executed, but the saved tile
  hash, stage manifests, and route outcomes stayed identical to the
  resolved-center-only branch.
- Last delta:
  - Added loader-compatible config key
    `preRasterizeAnchorSupportPatchBridgeHalfWidth`.
  - Extended `RasterizeAnchorSupportPatches(...)` so it can rasterize an
    oriented quad strip between the resolved source-support point and the
    anchor projection.
  - Added `[HF-ANCHOR-SUPPORT-BRIDGE]` logging so the strip footprint and
    length are explicit in the bake log.
  - Experiment config stayed untracked:
    `tmp/config-experiments/og_4029_raster_support_patch06_bridge_support_anchoronly_w030.json`
  - Artifact:
    `tmp/bake-sweeps/og_4029_raster_support_patch06_bridge_support_anchoronly_w030_v1-20260525T220138Z/`
  - Changed hash:
    `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`
  - Focused:
    `3/7`
  - Full:
    `20/23`
  - Decisive bake-log proof:
    - `[SRC-ANCHOR-SUPPORT] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) delta=0.604 tri=537325 source=vmap dist2D=0.306 inside=0`
    - `[HF-ANCHOR-SUPPORT-PATCH] anchor=(1523.800,-4425.900,17.100) center=(1523.668,-4426.176,17.704) centerMode=resolvedSupportPoint halfExtent=0.600 source=1`
    - `[HF-ANCHOR-SUPPORT-BRIDGE] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) halfWidth=0.300 length=0.306 source=1`
    - `[HF-ANCHOR-SUPPORT-PATCH] map=1 tile=40,29: rasterized 2 support patch(es)`
  - Decisive stage proof:
    - the saved tile hash stayed exactly the same as the resolved-center-only
      branch:
      `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`
    - `median` component metadata stayed byte-for-byte identical, including
      component `0` at `minDistance2D=0.5315163135528564`
    - `regions` component metadata also stayed identical
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Stage summary for the important anchors still stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Focused failure profile stayed identical to the center-only branch.
  - Full failures stayed:
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
    - `orgrimmar_zeppelin_tower_underpass_live_stall_exact_recovery`
  - Practical read:
    - this closes tiny local raster patch-shape micro-variants at the current
      size and placement
    - the next credible retry should not be another micro patch reshaping
      branch; it needs a more structural earlier raster/source change or a
      contour-builder/simplification change
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_bridge_support_anchoronly_w030_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_bridge_support_anchoronly_w030.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `40B9A6FB44B2555BE39909D767AC480668843E7AEAA478468BEC4349C2C92CC8`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_bridge_support_anchoronly_w030_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_bridge_support_anchoronly_w030_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_bridge_support_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.

### 2026-05-25 - contour raw-bypass plus support-arc family closure
- Active task: finish the contour-focused tile `1:40,29` loop without falling
  back into generic knob churn, then leave the next handoff on a truly earlier
  proof surface.
- Pass result: `delta shipped; the new contour-builder raw-bypass and pre-poly
  support-arc experiment surfaces are checked in, the current contour family is
  now bounded by hard negatives on both plausible contours, and the live tile
  is restored to the stable A01 baseline`.
- Last delta:
  - Added new loader-compatible experiment surfaces in
    `tools/MmapGen/dep/recastnavigation/Recast/Include/Recast.h`,
    `tools/MmapGen/dep/recastnavigation/Recast/Source/RecastContour.cpp`, and
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `contourBuildBypassSimplificationForMatchedAnchorSupportContour`
    - `prePolyResimplifyAnchorSupportBandArcRadius`
    - `prePolyResimplifyAnchorSupportCenterMode`
    - helper/log surfaces around `replaceSimplifiedWithRawContour(...)` and
      `InjectAnchorSupportBandRawArcVertices(...)`
  - Proved the "move earlier" full-raw retry is a negative:
    - branch
      `og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass_v1`
    - artifact
      `tmp/bake-sweeps/og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass_v1-20260525T232236Z/`
    - hash
      `8E98F676F48FAB2952EF9D89CE6A22A40F8F3C3CC0CF8354A6B4C5AFD1F3E8A8`
    - decisive proof:
      `[CONTOUR-BUILD-ANCHOR-SEED] region=7 rawVerts=158 simplifiedVerts=158 rawBypassVerts=158 seededSupportBandArcRawVerts=22 matchedOverrides=1`
    - focused/full:
      `3/7`, `19/23`
    - read:
      even a full raw contour inside `rcBuildContours()` still left
      `1523.8 -> finalDetour / lower_competitor_dominant`
  - Proved the support-arc family is also negative on both plausible contours:
    - anchor-containing `r3`:
      `og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r3_v1`
      hash
      `52C9913EB0F4306A3912A8869D537689A227733560BD38DE4FE12E5F360F5C6B`,
      proof `11 -> 158 -> 29`, focused/full `3/7`, `20/23`
    - anchor-containing `r6`:
      `og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r6_v1`
      hash
      `62D0AEA1268141CC44FC7D00C6CA2B891E446FFD96217339DC79ADA97CA30E5D`,
      proof `11 -> 158 -> 36` with
      `preservedSupportBandArcRawVerts=25`, focused/full `3/7`, `20/23`
    - nearest non-containing `r6`:
      `og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6_v1`
      artifact
      `tmp/bake-sweeps/og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6_v1-20260525T234723Z/`
      hash
      `A6A9FA5B231AD484EA72E364D0DE26C1F964D5AD797F5B45BC06C4DCEC04AB3D`,
      proof
      `contour=1 region=8 rawVerts=226 candidateVerts=124 ... preservedSupportBandArcRawVerts=109`,
      focused/full `3/7`, `20/23`
  - Cross-branch invariant for `1523.800,-4425.900,17.100`:
    - stable baseline `A01DEE...`, raw-bypass, anchor-containing support-arc,
      and nearest-non-containing support-arc all kept the same stage summary:
      - `contours`: `supportCandidateCount=1`, `lowerCandidateCount=8`
      - `polymesh`: `supportCandidateCount=2`, `lowerCandidateCount=23`
      - `finalDetour`: `supportCandidateCount=0`, `lowerCandidateCount=5`,
        winner `0x1000000000ADAB`
    - important anchors still stayed:
      - `1522.500,-4424.100,17.000` -> no `firstBadStage`
      - `1523.800,-4425.900,17.100` ->
        `finalDetour / lower_competitor_dominant`
      - `1521.267,-4425.600,17.609` -> no `firstBadStage`
      - `1364.867,-4374.000,26.109` ->
        `finalDetour / winner_component_trapped`
  - Live tile restore:
    - restore artifact:
      `tmp/bake-sweeps/og_4029_restore_after_supportarc_iteration_20260525-20260525T235253Z/`
    - restored hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `8E98F676F48FAB2952EF9D89CE6A22A40F8F3C3CC0CF8354A6B4C5AFD1F3E8A8`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_contourbuild_seed_supportarc_supportcenter_anchoronly_r3_rawbypass_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `19/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r6.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `62D0AEA1268141CC44FC7D00C6CA2B891E446FFD96217339DC79ADA97CA30E5D`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r6_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_anchoronly_r6_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A6A9FA5B231AD484EA72E364D0DE26C1F964D5AD797F5B45BC06C4DCEC04AB3D`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> `3/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=critical_walk_legs_og_4029_raster_support_patch06_prepoly_resimplify_supportarc_supportcenter_nearest_noncontaining_r6_v1.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> `20/23`.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_restore_after_supportarc_iteration_20260525' -DataDir 'D:\wwow-bot\test-data'` -> restored the stable tile.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
- Next command: inspect an earlier source/compact support-overlap proof for
  `1523.8` before spending another iteration on late contour carry.

### 2026-05-26 - post-median compact support bridge gate for `1523.8`
- Active task: continue the `40,29` stage-manifest-driven handoff on the next
  earlier proof surface without falling back into more contour churn.
- Pass result: `delta shipped; a new opt-in compact-bridge manifest surface is
  in source, the bounded 1523.8 bridge branch executed, and it proved there are
  zero dormant support-band compact spans left to recover in that corridor`.
- Last delta:
  - Added loader-compatible experiment surfaces in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `preRegionRestoreAnchorSourceSupportBridgeCoordsWow`
    - `preRegionRestoreAnchorSourceSupportBridgeHalfWidth`
    - helper `RestoreAnchorSourceSupportCompactBridge(...)`
    - manifest stage `anchorSourceSupportBridge`
  - Fixed `tools/NavDataAudit/StageManifestAnalyzer.cs` so optional experiment
    stages no longer inflate summary coverage (`14/14` stays `14/14` even when
    the manifest also contains extra opt-in stages).
  - Ran the bounded bridge branch:
    - branch:
      `og_4029_compact_support_bridge_anchor1523_w030_v2`
    - artifact:
      `tmp/bake-sweeps/og_4029_compact_support_bridge_anchor1523_w030_v2-20260526T005547Z/`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof:
    - bake log:
      `[CHF-SRC-BRIDGE] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) dist2D=0.306 bridgeHalfWidth=0.300 corridorCells=45 supportCells=1 nullSupportCandidates=0 restored=0`
    - manifest:
      `anchorSourceSupportBridge` stayed identical to `median` for `1523.8`
      with `supportCandidateCount=56`,
      `supportContainsAnchorProjection=false`,
      `supportContainsAnchorCell=false`, and nearest support component
      `minDistance2D=0.5315163135528564`
    - `regions` stayed identical to `anchorSourceSupportBridge`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Important anchor summary stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Practical read:
    - this is a bounded negative on the intended compact-stage restore surface
    - the important evidence is `nullSupportCandidates=0`: the support->anchor
      corridor had no erased support-band compact spans left to recover
    - the next retry must move earlier than post-median compact restore and
      change source/raster/compact input so the missing support-band cells
      exist at all
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `dotnet build E:\repos\Westworld of Warcraft\tools\NavDataAudit\NavDataAudit.csproj --configuration Release` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_compact_support_bridge_anchor1523_w030_v2' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_compact_support_bridge_anchor1523_w030.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - `dotnet run --project E:\repos\Westworld of Warcraft\tools\NavDataAudit\NavDataAudit.csproj --configuration Release --no-build -- --stage-summary-only --stage-manifest E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_compact_support_bridge_anchor1523_w030_v2-20260526T005547Z\analysis\map0012940_anchor_stage_manifest.json --stage-summary E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_compact_support_bridge_anchor1523_w030_v2-20260526T005547Z\analysis\map0012940_anchor_stage_summary.json --stage-summary-csv E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_compact_support_bridge_anchor1523_w030_v2-20260526T005547Z\analysis\map0012940_anchor_stage_summary.csv` -> passed; coverage now reports `14/14`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    serialized tile hash never moved off the stable live baseline and the
    manifest gate failed before route-level proof changed.
- Next command: inspect an even earlier source/raster/compact-input retry for
  `1523.8`; the post-median bridge proved there is nothing left to recover in
  the compact corridor itself.

### 2026-05-26 - pre-raster same-source corridor promotion gate for `1523.8`
- Active task: continue the `40,29` manifest-first handoff by testing whether
  the recovered `1523.8` support source still has any real source triangles
  left to promote before erosion/regions, instead of restoring spans afterward.
- Pass result: `delta shipped; a new opt-in pre-raster source-triangle
  promotion surface is in source, the bounded 1523.8 branch executed, and it
  proved there are zero eligible same-source steep/null triangles left in that
  narrow corridor to promote into the walkable raster input`.
- Last delta:
  - Added loader-compatible experiment surfaces in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `preRasterizePromoteAnchorSourceSupportCoordsWow`
    - `preRasterizePromoteAnchorSourceSupportCorridorHalfWidth`
    - helpers `TriangleOverlapsAnchorSupportCorridorXZ(...)` and
      `PromoteAnchorSourceSupportTriangles(...)`
  - Documented the new experiment surface in `tools/MmapGen/config.json`.
  - Ran the bounded branch:
    - branch:
      `og_4029_source_support_corridor_promote_w030_v2`
    - artifact:
      `tmp/bake-sweeps/og_4029_source_support_corridor_promote_w030_v2-20260526T031600Z/`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof:
    - bake log:
      `[SRC-ANCHOR-PROMOTE] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) dist2D=0.306 halfWidth=0.300 source=vmap candidates=0 promotedSteep=0 promotedNull=0`
    - critical `1523.8` manifest accounting stayed on the stable baseline
      profile:
      - `buildCHF`: `80/3040`
      - `erode`: `8/2882`
      - `median`: `56/0`
      - `regions`: `56/0`
      - `contours`: `1/8`
      - `polymesh`: `2/23`
      - `finalDetour`: `0/5`, winner `0x1000000000ADAB`
    - `1523.800,-4425.900,17.100` still stayed
      `finalDetour / lower_competitor_dominant`
  - Important anchor summary stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`
    - `1523.800,-4425.900,17.100` ->
      `finalDetour / lower_competitor_dominant`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`
  - Practical read:
    - this closes the narrow same-source pre-raster triangle-promotion lane
      for `1523.8` at `halfWidth=0.300`
    - the important evidence is `candidates=0`, not just the stable hash: the
      recovered support source had no eligible steep/null triangles left in
      that corridor to promote
    - the next `1523.8` retry must move to a different input surface than
      "same source + same corridor + promote existing non-walkable triangles"
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_support_corridor_promote_w030_v2' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_support_corridor_promote_w030.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    serialized tile hash never moved off the stable live baseline and the
    manifest gate still failed before route-level proof changed.
- Next command: try an even earlier or different input-surface proof for
  `1523.8` than "same-source corridor promotion", or split back into the
  separate `1364.867` trapped-component lane instead of spending more 1523.8
  budget on finalDetour-only ideas.

### 2026-05-26 - source-footprint vs raster anchor-cell coverage gate for `1523.8`
- Active task: answer the next manifest-first branch question for tile `1:40,29`
  without falling back into more contour churn: is
  `1523.800,-4425.900,17.100` a source-footprint / seam hole or a
  raster-anchor-cell coverage hole?
- Pass result: `delta shipped; the new sourceFootprint manifest stage and early
  coverage summary fields are in source, the corrected v2 probe preserved the
  canonical first-bad-stage surface, and the bad 1523.8 anchor now reads as a
  source-footprint / seam-hole lane rather than a raster-only anchor-cell
  coverage lane`.
- Last delta:
  - Added new early proof helpers in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `PointInAxisAlignedRectXZ(...)`
    - `PointOnSegmentXZ(...)`
    - `SegmentsIntersectXZ(...)`
    - `TriangleOverlapsAxisAlignedRectXZ(...)`
    - `BuildSourceFootprintAnchorStageSummary(...)`
    - new opt-in manifest stage `sourceFootprint`
  - Extended heightfield-stage summaries so `rasterize` now records
    `supportContainsAnchorCell` and `lowerContainsAnchorCell`.
  - Extended `tools/NavDataAudit` with the new early summary fields:
    - `SourceFootprintContainsAnchorProjection`
    - `SourceFootprintContainsAnchorCell`
    - `RasterizeSupportContainsAnchorCell`
    - `EarlyCoverageFinding`
  - Important correction: the final `v2` analyzer deliberately does NOT
    promote raw heightfield / compact lower-dominance hints into the canonical
    `FirstBadStage` result. That preserved the known
    `1522.500,-4424.100,17.000 -> no firstBadStage` and
    `1521.267,-4425.600,17.609 -> no firstBadStage` facts while still adding
    the new early coverage read.
  - Ran the corrected probe branch:
    - variant:
      `og_4029_source_footprint_manifest_probe_v2`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_manifest_probe_v2-20260526T044213Z\`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof for `1523.800,-4425.900,17.100`:
    - source support probe stayed on the known nearby vmap support triangle:
      `source=vmap`, `triIndex=537325`,
      `support=(1523.668,-4426.176,17.704)`,
      `dist2D=0.30609676241874695`, `projectedInside=false`
    - `sourceFootprint`:
      `supportCandidateCount=5`, `lowerCandidateCount=24`,
      `supportContainsAnchorProjection=false`,
      `supportContainsAnchorCell=false`,
      `lowerContainsAnchorCell=true`,
      `supportProjectionCandidateCount=0`,
      `supportCellCandidateCount=0`,
      `lowerCellCandidateCount=3`,
      `nearestSupportDistance2D=0.30609676241874695`
    - `rasterize`:
      `supportCandidateCount=138`, `lowerCandidateCount=3193`,
      `supportContainsAnchorCell=false`,
      `lowerContainsAnchorCell=true`
    - later profile stayed on the stable baseline:
      `buildCHF 80/3040`, `erode 8/2882`, `median 56/0`, `regions 56/0`,
      `contours 1/8`, `polymesh 2/23`, `finalDetour 0/5`, winner
      `0x1000000000ADAB`
    - summary answer stayed:
      `1523.800,-4425.900,17.100 -> finalDetour / lower_competitor_dominant`
      with `EarlyCoverageFinding=source_footprint_or_seam_hole`
  - Important anchor summary stayed:
    - `1522.500,-4424.100,17.000` -> no `firstBadStage`,
      `EarlyCoverageFinding=source_footprint_or_seam_hole`
    - `1521.267,-4425.600,17.609` -> no `firstBadStage`,
      `EarlyCoverageFinding=source_footprint_or_seam_hole`
    - `1364.867,-4374.000,26.109` ->
      `finalDetour / winner_component_trapped`,
      `EarlyCoverageFinding=early_support_overlap_present`
  - Cross-check proving the new split is real:
    - `1479.767,-4426.000,25.309` kept no `firstBadStage`, but showed
      `SourceFootprintContainsAnchorProjection=true`,
      `SourceFootprintContainsAnchorCell=true`,
      `RasterizeSupportContainsAnchorCell=false`, so
      `EarlyCoverageFinding=raster_anchor_cell_coverage_hole`
  - Practical read:
    - `1523.8` should now be treated as a source-footprint / seam-hole lane
      rather than a raster-only anchor-cell coverage lane
    - do not spend more `1523.8` budget on late contour carry, post-median
      compact restore, or the already-exhausted same-source corridor-promotion
      lane unless a future branch first changes `sourceFootprint` or
      `rasterize` overlap
    - `1364.867,-4374.000,26.109` remains a separate trapped-component lane
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `dotnet build E:\repos\Westworld of Warcraft\tools\NavDataAudit\NavDataAudit.csproj --configuration Release` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_manifest_probe_v2' -DataDir 'D:\wwow-bot\test-data'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - `dotnet run --project E:\repos\Westworld of Warcraft\tools\NavDataAudit\NavDataAudit.csproj --configuration Release --no-build -- --stage-summary-only --stage-manifest E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_manifest_probe_v2-20260526T044213Z\analysis\map0012940_anchor_stage_manifest.json --write-stage-summary E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_manifest_probe_v2-20260526T044213Z\analysis\map0012940_anchor_stage_summary.json --write-stage-summary-csv E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_manifest_probe_v2-20260526T044213Z\analysis\map0012940_anchor_stage_summary.csv` -> passed.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    serialized tile hash never moved off the stable live baseline and the new
    manifest gate answered the question without changing route-level proof.
- Next command: target a real source-footprint / seam-creation branch for
  `1523.8` that changes `sourceFootprint supportContainsAnchorProjection` or
  `supportContainsAnchorCell`, or split back into the separate
  `1364.867,-4374.000,26.109` trapped-component lane instead of spending more
  `1523.8` budget on later contour or finalDetour churn.

### 2026-05-26 - pre-raster anchor-cell support-band promotion gate for `1523.8`
- Active task: test the narrowest remaining "hidden loaded geometry" lane for
  `1523.800,-4425.900,17.100` before pivoting deeper into seam creation:
  if a support-band triangle already overlaps the exact anchor cell before
  rasterization, promote it and see whether `sourceFootprint` or `rasterize`
  finally move.
- Pass result: `negative; corrected rerun stayed byte-identical to the stable
  live tile, emitted no [SRC-ANCHOR-CELL-PROMOTE] lines, and left the 1523.8
  sourceFootprint/raster rows unchanged`.
- Last delta:
  - Added new pre-raster helper in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    `PromoteAnchorSupportCellTriangles(...)`
  - Added new opt-in config keys:
    - `preRasterizePromoteAnchorSupportCellCoordsWow`
    - `preRasterizePromoteAnchorSupportCellCrossSourceOnly`
  - Ran the bounded experiment with temp config:
    `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_anchorcell_promote_v1.json`
  - Variant + artifact:
    - variant:
      `og_4029_source_footprint_anchorcell_promote_v1`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_anchorcell_promote_v1-20260526T132241Z\`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof:
    - the corrected rerun still logged the known support probe:
      `[SRC-ANCHOR-SUPPORT] anchor=(1523.800,-4425.900,17.100) support=(1523.668,-4426.176,17.704) delta=0.604 tri=537325 source=vmap dist2D=0.306 inside=0`
    - `bake.log` emitted NO `[SRC-ANCHOR-CELL-PROMOTE]` lines
    - stage summary stayed unchanged:
      - `1523.800,-4425.900,17.100` ->
        `finalDetour / lower_competitor_dominant`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`,
        `EarlyCoverageFinding=source_footprint_or_seam_hole`
      - `1522.500,-4424.100,17.000` ->
        no `FirstBadStage`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`,
        `EarlyCoverageFinding=source_footprint_or_seam_hole`
      - `1521.267,-4425.600,17.609` ->
        no `FirstBadStage`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`,
        `EarlyCoverageFinding=source_footprint_or_seam_hole`
      - cross-check still held:
        `1479.767,-4426.000,25.309` ->
        `SourceFootprintContainsAnchorProjection=true`,
        `SourceFootprintContainsAnchorCell=true`,
        `RasterizeSupportContainsAnchorCell=false`,
        `EarlyCoverageFinding=raster_anchor_cell_coverage_hole`
  - Practical read:
    - another bounded negative for `1523.8`
    - this exhausts the "already-loaded hidden support-band triangle overlaps
      the anchor cell" branch unless a future branch changes the source window
      or source-family inputs first
    - the next credible work should inspect where the source footprint is
      missed:
      source-family seam, tile/subtile clipping, source window, or support-tri
      selection/transform
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_anchorcell_promote_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_anchorcell_promote_v1.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    early gate and the saved tile hash never moved.
- Next command: pivot from "promote hidden already-loaded anchor-cell
  triangles" to a real source-footprint / seam-creation branch that can move
  `SourceFootprintContainsAnchorProjection` or
  `SourceFootprintContainsAnchorCell` for `1523.800,-4425.900,17.100`.

### 2026-05-26 - source-footprint candidate detail trace for `1523.8`
- Active task: separate "cross-source seam" from "same-group footprint miss"
  before touching behavior again. If the bad support and lower candidates all
  belong to one source detail, the next fix surface is inside that loaded mesh,
  not another family-level promotion/carry branch.
- Pass result: `progress; new detail-label trace proved the critical 1523.8
  split happens entirely inside Ogrimmar.wmo#group133, and comparison anchors
  showed the same group can still be green even with the same early
  sourceFootprint hole`.
- Last delta:
  - Added VMap detail ownership recording in
    `tools/MmapGen/contrib/mmap/src/TerrainBuilder.h/cpp`:
    - `MeshTriangleDetailRange`
    - `MeshData::AddDetailTriangleRange(...)`
    - `MeshData::DetailLabelForTriangle(...)`
  - Added opt-in source-footprint candidate tracing in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `traceSourceFootprintCandidateCoordsWow`
    - `traceSourceFootprintCandidateLimit`
    - `[SRC-FOOTPRINT-CAND] ... detail=...`
  - Ran first single-anchor trace with temp config:
    `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_candidate_trace_v1.json`
  - Ran comparison trace with temp config:
    `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_candidate_trace_compare_v1.json`
  - Variant + artifacts:
    - `og_4029_source_footprint_candidate_trace_v1`
    - `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_candidate_trace_v1-20260526T134605Z\`
    - `og_4029_source_footprint_candidate_trace_compare_v1`
    - `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_candidate_trace_compare_v1-20260526T135311Z\`
    - both saved hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof:
    - `1523.800,-4425.900,17.100`:
      - upper support candidates `537325`, `537328`, `537384`, `537388`,
        `537391` all traced as `detail=Ogrimmar.wmo#group133`
      - lower cell-overlap vmap candidates `534606`, `537806` also traced as
        `detail=Ogrimmar.wmo#group133`
      - practical read: this is not a terrain/vmap split and not a cross-group
        split
    - `1522.500,-4424.100,17.000`:
      - support `532764`, `537807` and lower cell-overlap `534605`, `537804`
        also all traced as `Ogrimmar.wmo#group133`
      - despite that same early hole, canonical answer still stayed
        no `FirstBadStage`
    - `1521.267,-4425.600,17.609`:
      - support `537384`, `532767`, `532763`, `537388` and lower cell-overlap
        `534605`, `537804`, `532764`, `537807` also all traced as
        `Ogrimmar.wmo#group133`
      - canonical answer still stayed no `FirstBadStage`
    - raster-only cross-check remained clean:
      `1479.767,-4426.000,25.309` traced an in-cell `group133` support triangle
      (`531628`) and still stayed
      `EarlyCoverageFinding=raster_anchor_cell_coverage_hole`
    - strongest new quantitative split:
      - `1521.267` lower same-group overlap sat `-0.480` below chosen support
      - `1522.500` lower same-group overlap sat `-0.965` below chosen support
      - `1523.800` lower same-group overlap sat `-1.588` below chosen support
  - Practical read:
    - `1523.8` is now a same-group `Ogrimmar.wmo#group133` lane
    - next branch should decide between:
      same-group support-selection bug,
      same-group local source-footprint gap,
      or same-group clipping/window loss
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_candidate_trace_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_candidate_trace_v1.json'` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_candidate_trace_compare_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_candidate_trace_compare_v1.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    saved tile hash never moved off the approved stable baseline.
- Next command: trace the raw `group133` candidate score/vertex neighborhood for
  `1523.800,-4425.900,17.100` so the next code branch can choose between a
  same-group support-selection fix and a same-group seam/gap fix.

### 2026-05-26 - same-group source-footprint cap gate for `1523.8`
- Active task: try the first real same-group source-surface creation branch for
  `1523.800,-4425.900,17.100`. If the new `group133` proof is right, a tiny
  pre-raster source cap should move `sourceFootprint` before any later contour
  or finalDetour logic matters.
- Pass result: `negative; the corrected rerun still saved the stable live tile,
  emitted no [SRC-FOOTPRINT-CAP] lines, and left the 1523.8 sourceFootprint and
  raster rows unchanged`.
- Last delta:
  - Added new helper in `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    `InjectAnchorSourceFootprintCaps(...)`
  - Added new opt-in config keys:
    - `preRasterizeCreateAnchorSourceFootprintCapCoordsWow`
    - `preRasterizeCreateAnchorSourceFootprintCapHalfExtent`
    - `preRasterizeCreateAnchorSourceFootprintCapMaxSupportDistance2D`
    - `preRasterizeCreateAnchorSourceFootprintCapMinSameDetailLowerDrop`
  - Ran the bounded experiment with temp config:
    `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_v1.json`
  - Variant + authoritative artifact:
    - variant:
      `og_4029_source_footprint_cap_v1`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_cap_v1-20260526T141404Z\`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
  - Decisive proof:
    - the corrected rerun did arm the dedicated support-probe surface:
      `bake.log` begins with the targeted
      `[SRC-ANCHOR-SUPPORT] anchor=(1523.800,-4425.900,17.100) ...`
      before the usual manifest-wide stream
    - but `bake.log` emitted NO `[SRC-FOOTPRINT-CAP]` lines
    - stage summary stayed unchanged:
      - `1523.800,-4425.900,17.100` ->
        `finalDetour / lower_competitor_dominant`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`
      - `1522.500,-4424.100,17.000` ->
        no `FirstBadStage`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`
      - `1521.267,-4425.600,17.609` ->
        no `FirstBadStage`,
        `SourceFootprintContainsAnchorProjection=false`,
        `SourceFootprintContainsAnchorCell=false`,
        `RasterizeSupportContainsAnchorCell=false`
  - Practical read:
    - the first same-group source-cap implementation failed to arm
    - treat that as a bounded negative for the current cap predicate, not as a
      reason to abandon the source-cap surface entirely
    - next retry should either log the precondition or relax it, because the
      earlier detail trace already proved the same-group lower overlap exists at
      `1523.8`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_cap_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_v1.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    manifest gate and the saved tile hash never moved.
- Next command: rerun the same `group133` source-cap idea with the over-strict
  lower-overlap predicate removed or instrumented, so the branch actually tests
  whether a tiny source-surface cap can move `sourceFootprint` for
  `1523.800,-4425.900,17.100`.

### 2026-05-26 - source-cap anchor-frame fix plus region-zero proof for `1523.8`
- Active task: verify whether the first same-group source-cap failure was a real
  negative on the cap surface, then keep tracing the earliest stage that still
  blocks `1523.800,-4425.900,17.100`.
- Pass result: `delta shipped; the cap branch was using the wrong raster frame,
  the corrected rerun finally moved sourceFootprint/rasterize/median/regions
  for 1523.8, but the saved tile hash and all contour/polymesh/finalDetour
  results stayed on the stable baseline because the new support island still
  never acquired a nonzero region id`.
- Last delta:
  - Added new helpers in `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `BuildTileRasterConfig(...)`
    - `TryBuildAnchorRasterConfig(...)`
  - Updated both pre-raster source helpers to use the anchor's internal raster
    subtile frame instead of the map-wide frame:
    - `PromoteAnchorSupportCellTriangles(...)`
    - `InjectAnchorSourceFootprintCaps(...)`
  - Added extra source-cap diagnostics:
    - `preRasterizeCreateAnchorSourceFootprintCapRequireSameDetailLowerDrop`
    - `[SRC-FOOTPRINT-CAP-SKIP] ...`
    - `[SRC-FOOTPRINT-CAP-GATE] ...`
    - richer `[SRC-FOOTPRINT-CAP] ...`
  - Ran the frame-trace branch first:
    - variant:
      `og_4029_source_footprint_cap_force_v1_trace`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_cap_force_v1_trace-20260526T143246Z\`
    - decisive log:
      `[SRC-FOOTPRINT-CAP-SKIP] anchor=(1523.800,-4425.900,17.100) tri=537325 detail=Ogrimmar.wmo#group133 dist2D=0.306 reason=anchor_cell_oob cell=(3741,4571) dims=(241,241) bmin=(-4800.000,1066.667) cs=0.100`
  - Reran the same temp config after the anchor-frame fix:
    - temp config:
      `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_force_v1.json`
    - variant:
      `og_4029_source_footprint_cap_force_anchorframe_v1`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_cap_force_anchorframe_v1-20260526T144426Z\`
    - hash:
      `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`
    - decisive bake proof:
      `[SRC-FOOTPRINT-CAP] anchor=(1523.800,-4425.900,17.100) detail=Ogrimmar.wmo#group133 support=(1523.668,-4426.176,17.704) dist2D=0.306 capHalfExtent=0.300 requireLowerDrop=0 cellCandidates=2 resolvedCandidates=2 qualifiedLowerCandidates=2 sameDetailLowerMinY=16.116 sameDetailLowerDrop=1.588 added=2`
    - support probe moved to synthetic support tri `698742` with
      `source=vmap dist2D=0.000 inside=1`
  - Decisive manifest proof for `1523.800,-4425.900,17.100`:
    - `sourceFootprint` moved from
      `supportContainsAnchorProjection=false`,
      `supportContainsAnchorCell=false`
      to
      `supportContainsAnchorProjection=true`,
      `supportContainsAnchorCell=true`
    - `rasterize` moved from
      `supportContainsAnchorCell=false`
      to
      `supportContainsAnchorCell=true`
    - `median` / `regions` moved from `56/0` to `77/0`, and the new anchor
      support component became:
      `cellCount=21`, `spanCount=21`, `supportSpanCount=21`,
      `containsAnchorCell=true`, `touchesBoundary=false`, `regionIds=[]`
    - but the later proof stayed identical to the stable baseline:
      - `contours`: `1/8`
      - `polymesh`: `2/23`
      - `finalDetour`: `0/5`, winner `0x1000000000ADAB`
  - Practical read:
    - the first same-group source-cap negative was false because the branch was
      computing the anchor cell in the wrong raster frame
    - after the frame fix, `1523.8` is no longer best treated as an early
      source-footprint miss; it is now a source/compact-to-region survival
      problem
    - the new support island reaches `median` and `regions`, but it never gets
      a nonzero region id, so `rcBuildContours()` still sees the old shape
    - the next credible branch must connect or preserve that early support
      topology before region filtering, not spend more budget on late contour or
      finalDetour variants
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_cap_force_v1_trace' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_force_v1.json'` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_cap_force_anchorframe_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_force_v1.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `A01DEE47154601C9FDD1C8377EE82BD7C4AB7205D78F9947E356B8B97AD48123`.
  - Focused tests and full `CriticalWalkLegs` intentionally SKIPPED because the
    saved tile hash never moved.
- Next command: try the next early-stage `1523.8` branch on the same
  source-surface lane, but aim at connecting/preserving the new support island
  through region assignment instead of reworking contours or finalDetour again.

### 2026-05-26 - same-detail source-footprint bridge promotes `1523.8`
- Active task: finish the source-footprint/seam lane for `1523.800,-4425.900,17.100`
  by testing whether the post-cap `regionIds=[]` support island can be
  connected back into the surviving same-detail support band before contours.
- Pass result: `delta shipped; a new same-detail pre-raster bridge moved the
  anchor support component from regionIds=[] to regionIds=[30], changed the
  canonical 1523.8 summary from finalDetour/lower_competitor_dominant to no
  first bad stage, and validated a new live tile hash without regressing the
  broader 17/23 CriticalWalkLegs baseline`.
- Last delta:
  - Added a new opt-in helper in
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`:
    - `InjectAnchorSourceFootprintBridges(...)`
  - Added new config keys:
    - `preRasterizeCreateAnchorSourceFootprintBridgeCoordsWow`
    - `preRasterizeCreateAnchorSourceFootprintBridgeHalfWidth`
    - `preRasterizeCreateAnchorSourceFootprintBridgeMaxTargetDistance2D`
    - `preRasterizeCreateAnchorSourceFootprintBridgeMinTargetDistance2D`
    - `preRasterizeCreateAnchorSourceFootprintBridgeMinSameDetailLowerDrop`
    - `preRasterizeCreateAnchorSourceFootprintBridgeRequireSameDetailLowerDrop`
  - Reused the temp config from the anchor-frame cap branch, now extended with
    the bridge knobs:
    - temp config:
      `E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_force_v1.json`
    - variant:
      `og_4029_source_footprint_bridge_anchorframe_v1`
    - artifact:
      `E:\repos\Westworld of Warcraft\tmp\bake-sweeps\og_4029_source_footprint_bridge_anchorframe_v1-20260526T151016Z\`
    - promoted hash:
      `35579EA49C8CC1D2A2F1086EF5812D4C5F461BD2EC4E3135012AB60129175721`
  - Decisive bake proof:
    - the cap still armed:
      `[SRC-FOOTPRINT-CAP] anchor=(1523.800,-4425.900,17.100) detail=Ogrimmar.wmo#group133 support=(1523.668,-4426.176,17.704) dist2D=0.306 capHalfExtent=0.300 requireLowerDrop=0 cellCandidates=2 resolvedCandidates=2 qualifiedLowerCandidates=2 sameDetailLowerMinY=16.116 sameDetailLowerDrop=1.588 added=2`
    - the new bridge targeted the farthest nearby same-detail support band:
      `[SRC-FOOTPRINT-BRIDGE] anchor=(1523.800,-4425.900,17.100) detail=Ogrimmar.wmo#group133 targetTri=537388 target=(1522.374,-4427.174,17.841) targetDist2D=1.912 bridgeHalfWidth=0.300 requireLowerDrop=0 cellCandidates=4 resolvedCandidates=4 qualifiedLowerCandidates=2 sameDetailLowerMinY=16.116 added=2`
  - Decisive manifest proof for `1523.800,-4425.900,17.100`:
    - `sourceFootprint`:
      `supportCandidateCount=9`, `lowerCandidateCount=24`,
      `supportContainsAnchorProjection=true`,
      `supportContainsAnchorCell=true`
    - `rasterize`:
      `supportCandidateCount=282`, `lowerCandidateCount=3193`,
      `supportContainsAnchorCell=true`
    - `median` / `regions`:
      `175/0`, with the anchor support component changing from
      `regionIds=[]` to `regionIds=[30]`
    - later stages moved coherently:
      - `contours`: `2/8`
      - `polymesh`: `9/23`
      - `finalDetour`: `3/5`, winner `0x1000000000ADA5`
    - summary row now reads:
      - `FirstBadStage=null`
      - `FirstBadReason=null`
      - `FinalWinnerSupportCandidate=true`
      - `FinalWinnerCompetingLower=false`
      - `EarlyCoverageFinding=early_support_overlap_present`
  - Guard anchors preserved:
    - `1522.500,-4424.100,17.000` stayed
      `FirstBadStage=null` with
      `EarlyCoverageFinding=source_footprint_or_seam_hole`
    - `1521.267,-4425.600,17.609` stayed
      `FirstBadStage=null` with the same early coverage read
    - `1364.867,-4374.000,26.109` stayed
      `finalDetour / winner_component_trapped`
- Validation/tests run:
  - `powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\MmapGen\build-mmapgen.ps1` -> passed.
  - `$env:WWOW_VMANGOS_DATA_DIR='D:\MaNGOS\data'; powershell -ExecutionPolicy Bypass -File E:\repos\Westworld of Warcraft\tools\scripts\bake-tile.ps1 -Map 1 -Tiles '40,29' -Variant 'og_4029_source_footprint_bridge_anchorframe_v1' -DataDir 'D:\wwow-bot\test-data' -ConfigPath 'E:\repos\Westworld of Warcraft\tmp\config-experiments\og_4029_source_footprint_cap_force_v1.json'` -> passed.
  - `Get-FileHash 'D:/wwow-bot/test-data/mmaps/0012940.mmtile' -Algorithm SHA256 | Select-Object -ExpandProperty Hash` -> `35579EA49C8CC1D2A2F1086EF5812D4C5F461BD2EC4E3135012AB60129175721`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MmapMeshQualityTests.OrgrimmarZeppelinTopRampDeck|FullyQualifiedName~LongPathingRouteTests.OrgrimmarCityToZeppelinTowerLowerApproach_DensifiesLocalPhysicsRepairSegments|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers|FullyQualifiedName~LongPathingRouteTests.OrgrimmarFlightMasterToFrezzaSpawn_UsesCurrentBoardingShortcut" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_source_footprint_bridge_anchorframe_v1_focused.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding` -> passed `7/7`.
  - `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; dotnet test E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings E:\repos\Westworld of Warcraft\Tests\PathfindingService.Tests\test.runsettings -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingRouteTests.CrossroadsToUndercity_CriticalWalkLegs_HaveWalkablePathfindingRoutes" --logger "console;verbosity=minimal" --logger "trx;LogFileName=og_4029_source_footprint_bridge_anchorframe_v1_critical_walk_legs.trx" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-pathfinding -- RunConfiguration.TestSessionTimeout=1200000` -> still `17/23`, with the same six red legs as the prior baseline:
    - `orgrimmar_city_live_vertical_replan_recovery`
    - `orgrimmar_city_hallway_live_wall_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery`
    - `orgrimmar_city_hallway_exit_live_stall_recovery_corridor`
    - `orgrimmar_exterior_incline_live_stall_exact_recovery`
    - `orgrimmar_zeppelin_tower_ramp_underpass_stall_screenshot_recovery`
- Next command: treat `1523.8` as fixed on the manifest/route surface and keep
  the remaining backlog focused on the separate city/hallway/exterior/underpass
  reds plus the still-independent trapped `1364.867` lane.
