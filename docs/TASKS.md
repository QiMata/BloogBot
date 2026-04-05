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
