# Master Tasks — Parity Validation & Service Hardening

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order within each phase.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker). Never defer live validation tests.
5. **WoW.exe binary parity is THE rule** for physics/movement. No heuristics without binary evidence.
6. Every fix must include or update a focused test.
7. After each shipped delta, commit and push before ending the pass.
8. **Navigation.dll x86 for tests:** BotRunner.Tests targets x86. Copy `Exports/Navigation/cmake_build_x86/Release/Navigation.dll` to `Bot/Release/net8.0/Navigation.dll`.
9. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## P0 — Native DLL Separation: Physics, Scene, Pathfinding (Priority: CRITICAL)

**Problem:** Navigation.dll is a monolith that contains pathfinding, physics, AND scene data. The x86 MSBuild build doesn't include DllMain.cpp (which has newer exports like `SegmentIntersectsDynamicObjects`, `IsPointOnNavmesh`, `FindNearestWalkablePoint`) because DllMain.cpp uses `__try` (SEH) which conflicts with C++ exception handling (`/EHsc`). The CMake x64 build includes everything but the test host is x86.

**Architecture goal (matching WoW.exe binary):**
- **Physics.dll** — Local physics ONLY. `PhysicsStepV2`, `CollisionStep`, gravity, movement. This is what runs every tick inside the bot process. No remote calls. GetGroundZ is part of physics (used by `CMovement::CollisionStep` at VA 0x633840 to resolve ground contact). It stays here.
- **SceneData.dll** — Scene collision geometry loading (VMAP triangles, ADT terrain). Serves data to Physics.dll. Can run as local DLL or Docker service for headless bots.
- **Navigation.dll** — Pathfinding ONLY. Detour navmesh A* path queries. Runs as Docker service. No physics, no GetGroundZ.

**Key principle:** GetGroundZ is NOT a "remote" operation. In WoW.exe, `CMovement::CollisionStep` calls ground-height queries locally every physics tick. There is no network round-trip for ground height. Our architecture must match: GetGroundZ lives in the local physics DLL, not in a remote service.

| # | Task | Spec |
|---|------|------|
| 0.1 | **Audit current Navigation.dll exports** — 100+ exports categorized: Physics (28), Scene (14), Pathfinding (8). See `docs/dll-separation-audit.md`. | **Done** |
| 0.2 | **Design Physics.dll API** — 28 exports defined in audit doc. PhysicsStepV2, GetGroundZ, LineOfSight, SweepCapsule + 80 parity test exports + dynamic objects. | **Done** |
| 0.3 | **Design Navigation.dll API (path-only)** — 8 exports: FindPath, PathArrFree, FindPathCorridor, CorridorUpdate/MoveTarget/IsValid/Destroy. | **Done** |
| 0.4 | **Design SceneData.dll API** — 14 exports: QueryTerrainTriangles, InjectSceneTriangles, ClearSceneCache, SetSceneSliceMode, MapLoader, SceneCache ops. | **Done** |
| 0.5 | **x86 build now includes DllMain.cpp** — Fixed __try→try/catch for MSVC, removed duplicate include, /EHa. All 20 exports present. DLL separation (Physics/Scene/Nav split) is P0.5b below. | **Done** (768f8bd9) |
| 0.5b | **Create Physics.dll CMake project** — Separate Physics.dll from Navigation.dll. CMakeLists.txt in `Exports/Physics/`. x86+x64. | Open |
| 0.6 | **Create SceneData.dll CMake project** — `Exports/SceneData/`. Compiles VMAP loading, ADT parsing, triangle extraction. x64 only (Docker service). | Open |
| 0.7 | **Refactor Navigation.dll to path-only** — Remove physics and scene code. Keep only Detour navmesh, PathFinder, MoveMap. x64 only (Docker service). | Open |
| 0.8 | **Update C# P/Invoke declarations** — PathfindingClient loads Navigation.dll (paths). NativeLocalPhysics loads Physics.dll (physics). SceneDataClient loads SceneData.dll (scene). Update DllImport constants. | Open |
| 0.9 | **Update Docker builds** — PathfindingService Dockerfile builds Navigation.dll (path-only). SceneDataService Dockerfile builds SceneData.dll. Physics.dll is local (no Docker). | Open |
| 0.10 | **Build x86 Physics.dll** — Must build for x86 (test host) AND x64 (production). Both targets in CMake. Verify all exports present in both architectures. | Open |
| 0.11 | **Integration test: Physics.dll local step** — Bot teleports, PhysicsStepV2 from Physics.dll resolves ground contact, bot lands. No remote calls. | Open |
| 0.12 | **Integration test: Navigation.dll remote path** — Bot requests path from Docker PathfindingService. Navigation.dll (path-only) returns waypoints. | Open |

---

## Critical Bug — FIXED in cbe794eb

**GetGroundZ P/Invoke entry point mismatch.** The C# method `GetGroundZNative` had no `EntryPoint` attribute, but the DLL exports `GetGroundZ` (not `GetGroundZNative`). This caused ALL local ground-height queries to fail with "Unable to find entry point", making bots float in the air after teleport. Fixed by adding `EntryPoint = "GetGroundZ"` to the DllImport.

**User must rebuild + redeploy** to pick up fix from commit `cbe794eb`.

---

## Test Baseline (2026-04-05)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1417 | 0 | 1 | Green — 7 MC integration tests added |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (unit, non-LV) | 1623 | 0 | 4 | All green — tagged PathfindingPerf as infra |
| BotRunner.Tests (LiveValidation) | TBD | TBD | TBD | Running with all fixes (GetGroundZ + singleton + x86 DLL) |

---

## P1 — P/Invoke & Native DLL Parity Audit (Priority: CRITICAL)

**Problem:** Bots float in the air because local physics can't find the ground. The `GetGroundZ` entry point fix (cbe794eb) resolves the main issue, but other P/Invoke bindings may have similar mismatches or missing exports in the x86 build.

**Goal:** Every P/Invoke declaration in the C# code has a matching export in the x86 Navigation.dll. Any mismatch = bot stall.

### Current P/Invoke vs DLL Export Map

| C# Method | DllImport EntryPoint | x86 DLL Export | Status |
|-----------|---------------------|----------------|--------|
| `GetGroundZNative` | `GetGroundZ` | `GetGroundZ` | **Fixed** (cbe794eb) |
| `LineOfSightNative` | `LineOfSight` | `LineOfSight` | OK |
| `SegmentIntersectsDynamicObjectsNative` | `SegmentIntersectsDynamicObjects` | `SegmentIntersectsDynamicObjects` | **Fixed** (768f8bd9) |
| `IsPointOnNavmeshNative` | `IsPointOnNavmesh` | `IsPointOnNavmesh` | **Fixed** (768f8bd9) |
| `FindNearestWalkablePointNative` | `FindNearestWalkablePoint` | `FindNearestWalkablePoint` | **Fixed** (768f8bd9) |
| `NativePhysics.PhysicsStepV2` | (matches) | `PhysicsStepV2` | OK |
| `NativePhysics.GetGroundZ` | (matches) | `GetGroundZ` | OK |
| `NativePhysics.PreloadMap` | (matches) | `PreloadMap` | OK |
| `NativePhysics.SetDataDirectory` | (matches) | `SetDataDirectory` | needs verify |
| `NativePhysics.SetSceneSliceMode` | (matches) | `SetSceneSliceMode` | needs verify |
| `NativePhysics.InjectSceneTriangles` | (matches) | `InjectSceneTriangles` | needs verify |

| # | Task | Spec |
|---|------|------|
| 1.1 | **Verify ALL P/Invoke entry points match x86 DLL exports** — All 20 exports verified. 3 fixed in 768f8bd9. | **Done** (768f8bd9) |
| 1.2 | **Add missing C++ exports for x86 build** — Fixed DllMain.cpp __try→try/catch, all exports now present in x86 build. | **Done** (768f8bd9) |
| 1.3 | **Validate GetGroundZ works** — `GetGroundZ_Orgrimmar_ReturnsValidHeight` test (needs WWOW_DATA_DIR). | **Done** (e7c8d010) |
| 1.4 | **Validate PhysicsStepV2 produces forward movement** — `PhysicsStepV2_ForwardMovement_ProducesPositionChange` test. | **Done** (e7c8d010) |
| 1.5 | **Validate LineOfSight works** — `LineOfSight_OpenAir_ReturnsTrue` test. | **Done** (e7c8d010) |
| 1.6 | **Create P/Invoke smoke test suite** — 9 export linkage tests + 3 functional tests in NavigationDllSmokeTests.cs. | **Done** (83952b21, e7c8d010) |

---

## P2 — Movement Controller Binary Parity Validation (Priority: CRITICAL)

**Problem:** Even with GetGroundZ fixed, the full movement pipeline (MovementController → NativeLocalPhysics → Navigation.dll) must produce WoW.exe-identical behavior. The physics constants are verified (P6), but the movement controller integration may have gaps.

**Parity baseline (commit 70c72973):** 666/669 physics replay tests pass. The MovementController was at 100% parity before remote-physics workarounds were added and then removed.

| # | Task | Spec |
|---|------|------|
| 2.1 | **Run physics replay tests** — 666 pass, 2 fail (pre-existing elevator), 1 skip. x64 build with DllMain.cpp fix. No regressions from __try→try/catch. x86 build at `Bot/Release/net8.0/x86/Navigation.dll`. | **Done** |
| 2.2 | **Verify MovementController idle→moving transition** — After teleport + GetGroundZ success, MovementController must transition from idle to forward movement within 1 physics tick when MoveToward() is called. Trace: log movement flags, velocity, position delta per tick. | Open |
| 2.3 | **Verify heartbeat packet emission** — MovementController must send MSG_MOVE_HEARTBEAT every 100ms (WoW.exe binary constant at 0x5E2110). Capture: count heartbeats over 5s of forward movement, assert 45-55 heartbeats. | Open |
| 2.4 | **Verify collision slide behavior** — Bot walks into a wall, verify CollideAndSlide produces slide along wall (not stop). Compare against physics replay test cases. | Open |
| 2.5 | **Verify gravity/falling behavior** — Bot walks off a ledge, verify FALLINGFAR flag set, terminal velocity approached, landing detected. Compare against physics replay ground-contact tests. | Open |
| 2.6 | **Diff MovementController against parity baseline** — 72 lines added, 15 removed. Changes: IPhysicsClient→local NativeLocalPhysics.Step, workarounds removed, idle guard restored, SceneData refresh added. No new physics behavior. All changes are cleanup or architecture alignment. | **Done** |

---

## P3 — PathfindingService Docker Validation (Priority: High)

**Goal:** PathfindingService serves paths only. Runs in Docker. Verify it works end-to-end with the BG bot.

| # | Task | Spec |
|---|------|------|
| 3.1 | **Verify Docker container running** — Up 37h, port 5001, Linux container, WWOW_DATA_DIR=/wwow-data, volume D:/MaNGOS/data→/wwow-data. | **Done** |
| 3.2 | **Verify path request round-trip** — BG bot sends CalculatePathRequest(mapId=1, start=OrgBank, end=OrgAH). Assert: non-empty waypoint array returned. Log latency. | Open |
| 3.3 | **Verify WWOW_DATA_DIR volume mount** — Container has mmaps/, maps/, vmaps/ from host volume. `docker exec pathfinding-service ls /data/mmaps` shows map tiles. | Open |
| 3.4 | **Add health check to docker-compose** — TCP healthcheck on 5001, 30s interval, 15s start. Added to vmangos-linux.yml. | **Done** (ceda708d) |
| 3.5 | **Test pathfinding under load** — 10 concurrent path requests from different map positions. Assert: all return valid paths within 5s. No deadlocks. | Open |

---

## P4 — SceneDataService Docker Validation (Priority: High)

**Goal:** SceneDataService serves scene collision triangles for local physics. Runs in Docker.

| # | Task | Spec |
|---|------|------|
| 4.1 | **Verify Docker container running** — Up 37h, port 5003, Linux container, same volume mount. | **Done** |
| 4.2 | **Verify scene slice request** — BG bot requests scene slice for Orgrimmar area. Assert: triangle data returned. | Open |
| 4.3 | **Verify VMAP data accessible** — Container has vmaps/ from host volume. Scene queries return non-empty triangle sets. | Open |
| 4.4 | **Add health check to docker-compose** — TCP healthcheck on 5003, 30s interval, 15s start. Added to vmangos-linux.yml. | **Done** (ceda708d) |

---

## P5 — Legacy Code Cleanup (Priority: Medium)

**Goal:** Remove dead code from remote-physics era to reduce confusion.

| # | Task | Spec |
|---|------|------|
| 5.1 | **Delete IPhysicsClient** — DONE (cbe794eb). Interface + all references removed. | **Done** |
| 5.2 | **Simplify BackgroundPhysicsMode** — SharedPathfinding removed. Resolver always returns LocalInProcess. 13/13 tests pass. | **Done** (7bd43fe0) |
| 5.3 | **Remove stale remote-physics comments** — Only 1 reference found and it's already correct ("no remote physics fallback"). | **Done** |

---

## P6 — LiveValidation Full Sweep (Priority: High)

**Goal:** Run every LiveValidation test, document results, fix failures.

| # | Task | Spec |
|---|------|------|
| 6.1 | **Rebuild with all fixes** — `dotnet build WestworldOfWarcraft.sln --configuration Release`. Copy x86 Navigation.dll. Verify 0 CS errors. | Open |
| 6.2 | **Run full LiveValidation suite** — `dotnet test --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m`. Capture per-test pass/fail/skip with test names. | Open |
| 6.3 | **Categorize all 13 failures** — For each: code bug, data issue, fixture issue, or expected skip. Create fix task for each code bug. | Open |
| 6.4 | **Categorize all 156 skips** — Which need FG bot, pathfinding, specific fixtures. Document skip reasons. Identify which can be converted to pass with minor fixes. | Open |
| 6.5 | **Fix code-bug failures** — Each failure gets its own investigation + fix + test verification. | Open |
| 6.6 | **Update baseline** — Record new pass/fail/skip. Target: >40 passing, <10 failing. | Open |

---

## P7 — Docker Compose Full Stack (Priority: Medium)

**Goal:** Single `docker compose up` brings up PathfindingService + SceneDataService. StateManager runs natively on Windows (needs WoW.exe access).

| # | Task | Spec |
|---|------|------|
| 7.1 | **Review docker-compose** — Both linux yml and windows yml verified. Services, ports, volumes, env vars correct. Healthchecks added. | **Done** (ceda708d) |
| 7.2 | **Ensure consistent WWOW_DATA_DIR** — Both services use D:/MaNGOS/data→/wwow-data. WWOW_DATA_DIR=/wwow-data in both. | **Done** |
| 7.3 | **End-to-end Docker test** — `docker compose up -d`, run BasicLoopTests, bots enter world and MOVE (not float). | Open |
| 7.4 | **Document setup in DEVELOPMENT_GUIDE.md** — Prerequisites, data volumes, commands. | Open |

---

## P8 — Unit Test Coverage for New Implementations (Priority: Medium)

**Status:** 156 unit tests written in T1 (all passing). Covers: ThreatTracker, RaidCooldownCoordinator, EncounterPositioning, HostilePlayerDetector, RaidRoleAssignment, LootCouncilSimulator, AuctionPostingService, GoldThresholdManager, WhisperTracker, TalentAutoAllocator, ZoneLevelingRoute, QuestChainRouter, ProfessionTrainerScheduler, AmmoManager, PathResultCache, SnapshotBatcher, ConnectionMultiplexer, PathfindingShardRouter, SnapshotDeltaComputer, TransportScheduleService, InnkeeperData, SummoningStoneData, ProtoRoundTrip.

| # | Task | Spec |
|---|------|------|
| 8.1 | **Run all unit tests** — 326 passed, 0 failed (Release). StateManagerLoadTests tagged RequiresInfrastructure. | **Done** (7eff1457) |
| 8.2 | **Add NavigationDllSmokeTests** — 12 tests (9 linkage + 3 functional). Covered by P1.6. | **Done** (83952b21, e7c8d010) |
| 8.3 | **MovementController integration tests** — 7 tests: construct, SetTargetWaypoint, ClearPath, Update, Reset, SetGroundedState, SetPath. All pass. Also fixed 2 MoveTowardWithFacing failures (useLocalPhysics:true). | **Done** (f62947ad) |

---

## P9 — Integration Test State Reset & Scrub (Priority: High)

**Problem:** Many LiveValidation tests (especially the new V2 tests: TradingTests, AuctionHouseTests, BankInteractionTests, MailSystemTests, etc.) do NOT call `EnsureCleanSlateAsync()` at the start. This means test state leaks between runs — items in inventory, gold amounts, guild memberships, quest log entries, buff states all carry over. Tests that pass in isolation fail when run in sequence.

**The existing pattern (from working tests like BgInteractionTests, CraftingProfessionTests):**
```csharp
// START of every test method:
await _bot.EnsureCleanSlateAsync(bgAccount, "BG");  // .reset items, .gm on, teleport to safe zone
await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");  // same for FG bot

// ... test body ...

// END: no explicit teardown needed — next test's EnsureCleanSlate handles it
```

**`EnsureCleanSlateAsync` does:**
1. `.gm on` — enable GM mode
2. `.reset items` — strip all inventory/gear
3. Teleport to Orgrimmar safe zone (away from mobs)
4. Wait for position to stabilize

| # | Task | Spec |
|---|------|------|
| 9.1 | **Audit all LiveValidation test files** — 29 files found missing EnsureCleanSlateAsync. All fixed. | **Done** (adf54d6c) |
| 9.2 | **Add EnsureCleanSlateAsync to every test method** — 103 methods across 29 files. 121 calls added. Zero remaining gaps. | **Done** (adf54d6c) |
| 9.3 | **Verify test isolation** — Needs LiveValidation run with StateManager. Blocked on P6.2. | Blocked |
| 9.4 | **Explicit teardown for guild/group tests** — Guild: `.guild delete` present. Raid: `DisbandGroup` present. Trade: EnsureCleanSlate at next test handles cleanup. | **Done** |
| 9.5 | **Standardize test structure** — Documented in Tests/CLAUDE.md with full pattern + rules. | **Done** (c17ceeeb) |
| 9.6 | **Fix tests that use wrong LiveBotFixture API** — Audit found 0 mismatches. All 80 teleport calls use BotTeleportAsync, all position access uses full path, RecentChatMessages exists. | **Done** |

---

## P10 — Project & Namespace Naming Review (Priority: Medium)

**Goal:** Audit all project names, namespaces, and class names for consistency, clarity, and alignment with the architecture. Rename where improvements can be made.

**Current naming concerns:**
- `BloogBot.AI` vs `WWoWBot.AI.Tests` — inconsistent prefix (BloogBot vs WWoW)
- `BotRunner` — generic name, could be `WoW.BotEngine` or `WoW.BotOrchestration`
- `WoWSharpClient` — good but sometimes aliased as `WowSharpClient` (case inconsistency in `WowSharpClient.NetworkTests`)
- `BotCommLayer` — could be `WoW.IPC` or `WoW.Communication`
- `WinProcessImports` — very specific, could be under a parent `WoW.Native` namespace
- `GameData.Core` — good
- `BotProfiles` — good
- `pfprobe` / `wwow-path-probe` — tool naming inconsistency

| # | Task | Spec |
|---|------|------|
| 10.1 | **Audit all 37 project names** — 6 issues: WowSharpClient casing, WWoWBot vs BloogBot prefix, pfprobe naming, LoadTests/WinImports dir mismatches. | **Done** |
| 10.2 | **Fix `WowSharpClient.NetworkTests` casing** — Should be `WoWSharpClient.NetworkTests` to match the main project. Rename csproj + directory + namespace. | Open |
| 10.3 | **Fix `WWoWBot.AI.Tests` vs `BloogBot.AI`** — Align naming. Either both use `BloogBot` or both use `WWoW`. | Open |
| 10.4 | **Evaluate `BotRunner` rename** — 704 file references. Keep as-is — rename risk too high for cosmetic benefit. | **Done** — keep |
| 10.5 | **Evaluate `BotCommLayer` rename** — 42 file references. Feasible but low priority. Keep as-is for now. | **Done** — keep |
| 10.6 | **Clean up tool project names** — `pfprobe` → `PathfindingProbe`, `wwow-path-probe` → merge with pfprobe or clarify distinction. | Open |
| 10.7 | **Document naming conventions** — Added to CLAUDE.md: pattern table, known issues, do-not-rename list. | **Done** |

---

## P11 — Pathfinding Corner & Obstacle Avoidance (Priority: High)

**Problem:** Bots get stuck on corners and objects. The pathfinding string-pull (`findSmoothPath` in PathFinder.cpp) produces paths that cut corners too tightly, and the bot's collision capsule clips building edges, doorframes, and terrain features. The bot then stalls because CollideAndSlide can't resolve the collision.

**Root cause candidates:**
1. **String-pull cuts corners too tight** — `findSmoothPath` in `PathFinder.cpp:1073` uses Detour's `dtNavMeshQuery::moveAlongSurface` which can produce waypoints that clip corners. The capsule radius (0.3064 for Orc) isn't accounted for in path smoothing.
2. **No capsule-width path offset** — Waypoints are on the navmesh centerline, but the bot has a physical radius. Paths near walls need to be offset inward by capsule radius.
3. **CollideAndSlide gets stuck in concave corners** — When the bot slides along wall A into corner where wall A meets wall B, the slide direction may oscillate between the two wall normals, causing a stall.
4. **Dynamic objects not in navmesh** — Detour navmesh is static. Dynamic objects (doors, event structures) aren't in the navmesh, so paths route through them. LOS check against DynamicObjectRegistry was added (session 11) but path segments still route through dynamic structures.

**WoW.exe binary reference:**
- String-pull uses `dtNavMeshQuery::findStraightPath` (VA 0x6B2A40) with `DT_STRAIGHTPATH_AREA_CROSSINGS` flag
- Capsule sweep uses WoW.exe's `CMovement::CollisionStep` (VA 0x633840) — already implemented in PhysicsEngine.cpp
- Corner resolution uses iterative slide with max 4 iterations (VA 0x633A10)

| # | Task | Spec |
|---|------|------|
| 11.1 | **Reproduce corner-stuck scenario** — Teleport bot to a known tight-corner location (Orgrimmar doorways, Undercity tunnels, RFC corridors). Log: path waypoints, collision normals, position per tick. Identify where the bot stalls. | Open |
| 11.2 | **Add capsule-radius path offset** — After string-pull produces waypoints, offset each waypoint away from nearby navmesh edges by capsule radius. This prevents the path from clipping walls. Implement in `PathFinder::findSmoothPath`. | Open |
| 11.3 | **Fix CollideAndSlide concave corner resolution** — When slide direction oscillates (dot product with previous slide < 0), clamp to the corner bisector or stop. Compare against WoW.exe's 4-iteration limit at VA 0x633A10. The current implementation in `PhysicsCollideSlide.cpp` may lack this. | Open |
| 11.4 | **Add stuck detection + recovery in NavigationPath** — `NavigationPath.GetNextWaypoint()` already has `MovementStuckRecoveryGeneration` support. Verify: if bot hasn't moved >0.5y in 3s, trigger path recalculation with a wider search. Log stuck events. | Open |
| 11.5 | **Test corner navigation: Orgrimmar buildings** — Bot navigates from Org bank to Org AH (tight corners around buildings). Assert: arrives within 60s, no stalls >3s. Record path waypoints for visualization. | Open |
| 11.6 | **Test corner navigation: RFC corridors** — Bot navigates through RFC dungeon corridors (narrow passages, doorframes). Assert: passes through all doorways without stalling. | Open |
| 11.7 | **Test obstacle avoidance: dynamic objects** — Place bot near Darkmoon Faire tents or closed doors. Verify LOS check blocks path shortcuts. Bot should pathfind around the obstacle. | Open |
| 11.8 | **Validate against physics replay: Undercity tunnels** — Use existing Undercity recording. Compare: does the bot follow the same corridor path as the FG recording? Flag any divergence at corners. | Open |

---

## Canonical Commands

```bash
# Kill WoW.exe before building (MANDATORY)
tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST
taskkill //F //PID <pid>

# Build
dotnet build WestworldOfWarcraft.sln --configuration Release

# Copy x86 Navigation.dll for tests
cp Exports/Navigation/cmake_build_x86/Release/Navigation.dll Bot/Release/net8.0/Navigation.dll

# Unit tests (fast, no server)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# Physics replay tests
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build

# LiveValidation (needs MaNGOS + StateManager)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Docker services
docker compose -f docker-compose.windows.yml up -d pathfinding-service scene-data-service

# Check Navigation.dll exports
strings Bot/Release/net8.0/Navigation.dll | grep -E "^[A-Z][a-zA-Z]+$" | sort -u
```
