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
| 0.1 | **Audit current Navigation.dll exports** — Map every export to its category: physics (PhysicsStepV2, GetGroundZ, CollideAndSlide), scene (QueryTerrainTriangles, InjectSceneTriangles), pathfinding (FindPath, FindSmoothPath). Document which belong in which DLL. | Open |
| 0.2 | **Design Physics.dll API** — Define the exported functions: `PhysicsStepV2`, `GetGroundZ`, `GetTerrainHeight`, `LineOfSight`, `SweepCapsule`, `CollisionStep`, `InitializePhysics`, `ShutdownPhysics`, `PreloadMap`, `SetDataDirectory`. These are the functions called every tick by MovementController. | Open |
| 0.3 | **Design Navigation.dll API (path-only)** — `FindPath`, `FindSmoothPath`, `CorridorUpdate`, `PathArrFree`. No physics, no GetGroundZ. This is what the Docker PathfindingService wraps. | Open |
| 0.4 | **Design SceneData.dll API** — `QueryTerrainTriangles`, `InjectSceneTriangles`, `ClearSceneCache`, `SetSceneSliceMode`, `LoadDynamicObjectMapping`, `RegisterDynamicObject`, `UnregisterDynamicObject`. Scene geometry loading and serving. | Open |
| 0.5 | **Create Physics.dll CMake project** — New CMakeLists.txt in `Exports/Physics/`. Compiles PhysicsEngine.cpp, PhysicsCollideSlide.cpp, PhysicsMovement.cpp, PhysicsGroundSnap.cpp + terrain query code. Builds both x86 and x64. | Open |
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
| WoWSharpClient.Tests | 1410 | 0 | 1 | Green |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (unit, non-LV) | 1624 | 4 | 1 | 4 infra-dependent |
| BotRunner.Tests (LiveValidation) | 26 | 13 | 156 | Pre-GetGroundZ-fix run |

---

## P1 — P/Invoke & Native DLL Parity Audit (Priority: CRITICAL)

**Problem:** Bots float in the air because local physics can't find the ground. The `GetGroundZ` entry point fix (cbe794eb) resolves the main issue, but other P/Invoke bindings may have similar mismatches or missing exports in the x86 build.

**Goal:** Every P/Invoke declaration in the C# code has a matching export in the x86 Navigation.dll. Any mismatch = bot stall.

### Current P/Invoke vs DLL Export Map

| C# Method | DllImport EntryPoint | x86 DLL Export | Status |
|-----------|---------------------|----------------|--------|
| `GetGroundZNative` | `GetGroundZ` | `GetGroundZ` | **Fixed** (cbe794eb) |
| `LineOfSightNative` | `LineOfSight` | `LineOfSight` | OK |
| `SegmentIntersectsDynamicObjectsNative` | `SegmentIntersectsDynamicObjects` | NOT EXPORTED | **Missing** |
| `IsPointOnNavmeshNative` | `IsPointOnNavmesh` | NOT EXPORTED | **Missing** |
| `FindNearestWalkablePointNative` | `FindNearestWalkablePoint` | NOT EXPORTED | **Missing** |
| `NativePhysics.PhysicsStepV2` | (matches) | `PhysicsStepV2` | OK |
| `NativePhysics.GetGroundZ` | (matches) | `GetGroundZ` | OK |
| `NativePhysics.PreloadMap` | (matches) | `PreloadMap` | OK |
| `NativePhysics.SetDataDirectory` | (matches) | `SetDataDirectory` | needs verify |
| `NativePhysics.SetSceneSliceMode` | (matches) | `SetSceneSliceMode` | needs verify |
| `NativePhysics.InjectSceneTriangles` | (matches) | `InjectSceneTriangles` | needs verify |

| # | Task | Spec |
|---|------|------|
| 1.1 | **Verify ALL P/Invoke entry points match x86 DLL exports** — Run `strings Navigation.dll | sort` against all DllImport declarations. Document every mismatch. Fix all EntryPoint attributes. | Open |
| 1.2 | **Add missing C++ exports for x86 build** — `SegmentIntersectsDynamicObjects`, `IsPointOnNavmesh`, `FindNearestWalkablePoint` are in x64 but not x86. Either add `__declspec(dllexport)` to the x86 CMake build OR add graceful fallback in C# when export is missing. | Open |
| 1.3 | **Validate GetGroundZ works after fix** — Run BasicLoopTests with rebuilt DLLs. Bot must land on ground (Z stabilizes) within 2s of teleport. Log: `[PHYS] GetGroundZ({x},{y}) = {z}` succeeds, no "entry point" errors. | Open |
| 1.4 | **Validate PhysicsStepV2 produces forward movement** — Unit test with x86 Navigation.dll: input with MOVEFLAG_FORWARD at Orgrimmar coords, assert output position differs from input (bot actually moves). | Open |
| 1.5 | **Validate LineOfSight works** — Unit test: two points with clear LOS return true; two points through a building return false. | Open |
| 1.6 | **Create P/Invoke smoke test suite** — `Tests/BotRunner.Tests/Native/NavigationDllSmokeTests.cs`. One test per P/Invoke export: call each function, assert no DllNotFoundException or EntryPointNotFoundException. Run as part of CI. | Open |

---

## P2 — Movement Controller Binary Parity Validation (Priority: CRITICAL)

**Problem:** Even with GetGroundZ fixed, the full movement pipeline (MovementController → NativeLocalPhysics → Navigation.dll) must produce WoW.exe-identical behavior. The physics constants are verified (P6), but the movement controller integration may have gaps.

**Parity baseline (commit 70c72973):** 666/669 physics replay tests pass. The MovementController was at 100% parity before remote-physics workarounds were added and then removed.

| # | Task | Spec |
|---|------|------|
| 2.1 | **Run 666 physics replay tests with x86 Navigation.dll** — `dotnet test Tests/Navigation.Physics.Tests/ --configuration Release`. Assert: 666 pass, 2 fail (pre-existing elevator), 1 skip. Any new failures = regression. | Open |
| 2.2 | **Verify MovementController idle→moving transition** — After teleport + GetGroundZ success, MovementController must transition from idle to forward movement within 1 physics tick when MoveToward() is called. Trace: log movement flags, velocity, position delta per tick. | Open |
| 2.3 | **Verify heartbeat packet emission** — MovementController must send MSG_MOVE_HEARTBEAT every 100ms (WoW.exe binary constant at 0x5E2110). Capture: count heartbeats over 5s of forward movement, assert 45-55 heartbeats. | Open |
| 2.4 | **Verify collision slide behavior** — Bot walks into a wall, verify CollideAndSlide produces slide along wall (not stop). Compare against physics replay test cases. | Open |
| 2.5 | **Verify gravity/falling behavior** — Bot walks off a ledge, verify FALLINGFAR flag set, terminal velocity approached, landing detected. Compare against physics replay ground-contact tests. | Open |
| 2.6 | **Diff MovementController against parity baseline** — `git diff 70c72973 HEAD -- Exports/WoWSharpClient/Movement/MovementController.cs`. Review every change since parity baseline. Flag any behavior change that doesn't have binary evidence. | Open |

---

## P3 — PathfindingService Docker Validation (Priority: High)

**Goal:** PathfindingService serves paths only. Runs in Docker. Verify it works end-to-end with the BG bot.

| # | Task | Spec |
|---|------|------|
| 3.1 | **Verify Docker container running** — `docker ps` shows `pathfinding-service` healthy on port 5001. | Open |
| 3.2 | **Verify path request round-trip** — BG bot sends CalculatePathRequest(mapId=1, start=OrgBank, end=OrgAH). Assert: non-empty waypoint array returned. Log latency. | Open |
| 3.3 | **Verify WWOW_DATA_DIR volume mount** — Container has mmaps/, maps/, vmaps/ from host volume. `docker exec pathfinding-service ls /data/mmaps` shows map tiles. | Open |
| 3.4 | **Add health check to docker-compose** — PathfindingService HEALTHCHECK: TCP connect to 5001. StateManager `depends_on` with condition. | Open |
| 3.5 | **Test pathfinding under load** — 10 concurrent path requests from different map positions. Assert: all return valid paths within 5s. No deadlocks. | Open |

---

## P4 — SceneDataService Docker Validation (Priority: High)

**Goal:** SceneDataService serves scene collision triangles for local physics. Runs in Docker.

| # | Task | Spec |
|---|------|------|
| 4.1 | **Verify Docker container running** — `docker ps` shows `scene-data-service` healthy on port 5003. | Open |
| 4.2 | **Verify scene slice request** — BG bot requests scene slice for Orgrimmar area. Assert: triangle data returned. | Open |
| 4.3 | **Verify VMAP data accessible** — Container has vmaps/ from host volume. Scene queries return non-empty triangle sets. | Open |
| 4.4 | **Add health check to docker-compose** — TCP connect to 5003. StateManager `depends_on`. | Open |

---

## P5 — Legacy Code Cleanup (Priority: Medium)

**Goal:** Remove dead code from remote-physics era to reduce confusion.

| # | Task | Spec |
|---|------|------|
| 5.1 | **Delete IPhysicsClient** — DONE (cbe794eb). Interface + all references removed. | **Done** |
| 5.2 | **Simplify BackgroundPhysicsMode** — Remove `SharedPathfinding` enum value. Only `LocalInProcess` and `LocalSceneSlices` are real. Clean resolver. | Open |
| 5.3 | **Remove stale remote-physics comments** — Update comments in MovementController, PathfindingClient, BackgroundBotWorker that reference remote physics. | Open |

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
| 7.1 | **Review docker-compose.windows.yml** — Verify services, ports, volumes, env vars. | Open |
| 7.2 | **Ensure consistent WWOW_DATA_DIR** — Both services read from same host volume mount. | Open |
| 7.3 | **End-to-end Docker test** — `docker compose up -d`, run BasicLoopTests, bots enter world and MOVE (not float). | Open |
| 7.4 | **Document setup in DEVELOPMENT_GUIDE.md** — Prerequisites, data volumes, commands. | Open |

---

## P8 — Unit Test Coverage for New Implementations (Priority: Medium)

**Status:** 156 unit tests written in T1 (all passing). Covers: ThreatTracker, RaidCooldownCoordinator, EncounterPositioning, HostilePlayerDetector, RaidRoleAssignment, LootCouncilSimulator, AuctionPostingService, GoldThresholdManager, WhisperTracker, TalentAutoAllocator, ZoneLevelingRoute, QuestChainRouter, ProfessionTrainerScheduler, AmmoManager, PathResultCache, SnapshotBatcher, ConnectionMultiplexer, PathfindingShardRouter, SnapshotDeltaComputer, TransportScheduleService, InnkeeperData, SummoningStoneData, ProtoRoundTrip.

| # | Task | Spec |
|---|------|------|
| 8.1 | **Run all 156 unit tests** — Confirm still green after IPhysicsClient removal. `dotnet test --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation"` | Open |
| 8.2 | **Add NavigationDllSmokeTests** — Per P1.6. One test per P/Invoke. | Open |
| 8.3 | **Add MovementController integration tests** — Test idle→moving, MoveToward→position change, StopAllMovement→idle. Use mocked Navigation.dll or local x86 DLL. | Open |

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
| 9.1 | **Audit all LiveValidation test files for missing state reset** — Grep for test classes that DON'T call `EnsureCleanSlateAsync`. List every file. The new V2 tests (Trading, AH, Bank, Mail, Guild, Wand, BG Queue, Channel, Quest, Pet, Spirit Healer, Gossip, Travel, Taxi, Transport, Mage Teleport, Raid, Summon, Integration, Scalability) are all suspect. | Open |
| 9.2 | **Add EnsureCleanSlateAsync to every test method** — Every `[SkippableFact]` in LiveValidation must start with `await _bot.EnsureCleanSlateAsync(bgAccount, "BG")`. If FG bot is used, also clean FG. No exceptions. | Open |
| 9.3 | **Verify test isolation** — Run the 5 most state-dependent tests (Trading, Bank, Mail, Guild, Quest) in sequence 3 times. Assert: all pass every run, no state leakage. | Open |
| 9.4 | **Add explicit teardown for guild/group tests** — Guild tests must `.guild delete` at the end. Group tests must `LEAVE_GROUP`/`DISBAND_GROUP`. Trade tests must `DECLINE_TRADE` if still open. | Open |
| 9.5 | **Standardize test structure** — Every LiveValidation test follows: `Setup (EnsureCleanSlate + GM commands) → Action (SendActionAsync) → Assert (snapshot verification) → Cleanup (if needed)`. Document this pattern in `Tests/CLAUDE.md`. | Open |
| 9.6 | **Fix tests that use wrong LiveBotFixture API** — Audit for: `_bot.TeleportAsync` (should be `_bot.BotTeleportAsync`), `snap.X/Y/Z` (should be `snap.Player?.Unit?.GameObject?.Base?.Position`), `snap.RecentChatMessages` (may not exist). Fix all API mismatches. | Open |

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
| 10.1 | **Audit all 37 project names** — List every .csproj, its namespace, and its purpose. Flag naming inconsistencies (BloogBot vs WWoW, case mismatches, unclear names). | Open |
| 10.2 | **Fix `WowSharpClient.NetworkTests` casing** — Should be `WoWSharpClient.NetworkTests` to match the main project. Rename csproj + directory + namespace. | Open |
| 10.3 | **Fix `WWoWBot.AI.Tests` vs `BloogBot.AI`** — Align naming. Either both use `BloogBot` or both use `WWoW`. | Open |
| 10.4 | **Evaluate `BotRunner` rename** — Is `BotRunner` clear enough? It's the core orchestration engine. Consider: keep as-is (too many references to rename safely) vs alias in docs. | Open |
| 10.5 | **Evaluate `BotCommLayer` rename** — Communication layer. Could be `WoW.Communication` but 200+ references. Cost/benefit analysis. | Open |
| 10.6 | **Clean up tool project names** — `pfprobe` → `PathfindingProbe`, `wwow-path-probe` → merge with pfprobe or clarify distinction. | Open |
| 10.7 | **Document naming conventions** — Add section to `CLAUDE.md` or `DEVELOPMENT_GUIDE.md` with project naming rules for future additions. | Open |

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
