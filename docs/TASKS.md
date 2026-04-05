# Master Tasks — Service Hardening & Movement Validation

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order within each phase.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live.** Never defer live validation tests.
5. Every fix must include or update a focused test.
6. After each shipped delta, commit and push before ending the pass.
7. **Navigation.dll x86 for tests:** BotRunner.Tests targets x86. Copy `Exports/Navigation/cmake_build_x86/Release/Navigation.dll` to `Bot/Release/net8.0/Navigation.dll` before running LiveValidation tests.
8. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-05)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1410 | 0 | 1 | Green |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (unit, non-LV) | 1624 | 4 | 1 | 4 infra-dependent |
| BotRunner.Tests (LiveValidation) | 26 | 13 | 156 | Singleton fix + x86 Nav.dll applied |

**Live observation (2026-04-05):** Bots properly fall after teleport, dungeoneering coordinator starts, leader engages, healers activate. Some bots stall (don't move). Crash occurred.

---

## S1 — Remove Legacy Remote-Physics Artifacts (Priority: High)

**Goal:** Strip dead code from the remote-physics era. Architecture is correct (physics is local) but legacy interfaces, unused parameters, and dead enum values add confusion and potential for misuse.

| # | Task | Status |
|---|------|--------|
| 1.1 | **Delete `IPhysicsClient` interface** — `Exports/BotRunner/Clients/IPhysicsClient.cs` is unused. No class implements it. Remove file + all references (BotContext.PhysicsClient property, MovementController constructor parameter, BackgroundBotWorker null assignment). | Open |
| 1.2 | **Simplify `BackgroundPhysicsMode`** — Remove `SharedPathfinding` enum value and resolver branch. Only `LocalInProcess` and `LocalSceneSlices` are real modes. Clean up `BackgroundPhysicsModeResolver.cs`. | Open |
| 1.3 | **Remove unused MovementController physics parameter** — Constructor accepts `IPhysicsClient? physics` but never uses it. Remove parameter, update all callers. | Open |
| 1.4 | **Clean up remote-physics comments** — Update stale comments in MovementController, PathfindingClient, BackgroundBotWorker that reference "remote physics", "fallback physics", or "shared PathfindingService physics". | Open |

---

## S2 — PathfindingService Docker Hardening (Priority: High)

**Goal:** Ensure PathfindingService runs reliably in Docker with correct data volumes, health checks, and graceful shutdown. Path-only — no physics.

| # | Task | Status |
|---|------|--------|
| 2.1 | **Verify PathfindingService Dockerfile builds clean** — `docker build` from scratch with Navigation.dll CMake + .NET publish. Validate mmaps/maps data volume mount works. | Open |
| 2.2 | **Add health check endpoint** — PathfindingService should expose a TCP health check on port 5001 that confirms Navigation.dll loaded and map data is accessible. Docker HEALTHCHECK instruction. | Open |
| 2.3 | **Test PathfindingService path requests from BG bot** — BG bot requests path from Orgrimmar to Crossroads via Docker PathfindingService. Assert: valid waypoint array returned, bot follows path. | Open |
| 2.4 | **Validate docker-compose startup order** — PathfindingService must be healthy before StateManager launches bots. Add `depends_on` with health check condition. | Open |

---

## S3 — SceneDataService Docker Hardening (Priority: High)

**Goal:** SceneDataService serves scene collision triangles for local physics. Ensure reliable Docker deployment with VMAP/ADT data access.

| # | Task | Status |
|---|------|--------|
| 3.1 | **Verify SceneDataService Dockerfile builds clean** — CMake + .NET publish. VMAP data volume mount. | Open |
| 3.2 | **Add health check endpoint** — TCP health on port 5003 confirming scene data loadable. | Open |
| 3.3 | **Test scene slice request from BG bot** — Bot requests scene slice for Orgrimmar area. Assert: triangle data returned, local physics uses it for collision. | Open |
| 3.4 | **Validate scene slice caching** — Scene slices should be cached after first load. Measure: second request for same area should be <1ms. | Open |

---

## S4 — Movement Controller / Local Physics Parity (Priority: Critical)

**Goal:** Ensure MovementController + NativeLocalPhysics produce WoW.exe-binary-parity movement. The physics engine constants are verified (P6), but the movement pipeline has integration gaps causing bot stalls.

| # | Task | Status |
|---|------|--------|
| 4.1 | **Diagnose bot stall root cause** — During dungeoneering, some bots don't move after teleport. Capture: which bots stall, their movement flags, whether they have a valid path, whether physics Step() is being called. Add diagnostic logging to MovementController.Update(). | Open |
| 4.2 | **Verify NativeLocalPhysics.Step() produces valid output** — Unit test: given a forward-moving input at Orgrimmar coordinates, Step() returns a new position with forward displacement. Test with x86 Navigation.dll. | Open |
| 4.3 | **Verify PathfindingClient.GetPath() returns valid waypoints** — Unit test: request path from Org bank to Org AH. Assert: non-empty waypoint array, all positions on navmesh. | Open |
| 4.4 | **Verify NavigationPath waypoint following** — Unit test: given a path with 5 waypoints, GetNextWaypoint advances correctly as player position updates. | Open |
| 4.5 | **Test MovementController idle-to-moving transition** — After teleport, bot should transition from idle to moving within 1 tick when a target waypoint is set. Verify MoveToward() triggers forward movement flags. | Open |
| 4.6 | **Live dungeoneering movement test** — 10-bot RFC clear. Assert: all bots reach dungeon entrance, all bots move inside (no stalls). Count bots that reach each waypoint. | Open |

---

## S5 — Docker Compose Full Stack (Priority: Medium)

**Goal:** Single `docker compose up` brings up the entire bot infrastructure: PathfindingService, SceneDataService, and optionally MaNGOS.

| # | Task | Status |
|---|------|--------|
| 5.1 | **Review docker-compose.windows.yml** — Verify all services defined, ports correct, volumes mounted, environment variables set. | Open |
| 5.2 | **Add WWOW_DATA_DIR forwarding** — All services that need map/vmap/mmap data should read from the same volume. Ensure consistent data directory across PathfindingService and SceneDataService. | Open |
| 5.3 | **End-to-end Docker test** — `docker compose up`, run BasicLoopTests, bots enter world and move. Assert: no 0x8007000B errors, no stalls, physics works. | Open |
| 5.4 | **Document Docker setup in DEVELOPMENT_GUIDE.md** — Prerequisites, data volumes, compose commands, troubleshooting. | Open |

---

## S6 — LiveValidation Test Results (Priority: High)

**Goal:** Get the full LiveValidation suite to a known-good state with documented pass/fail/skip for every test.

| # | Task | Status |
|---|------|--------|
| 6.1 | **Run full LiveValidation with verbose output** — Capture per-test pass/fail/skip names. Categorize failures as: code bug, missing data, fixture issue, or expected skip. | Open |
| 6.2 | **Fix test failures from T3.9** — 13 failures identified. Investigate and fix each one. | Open |
| 6.3 | **Reduce skip count** — 156 tests skipped. Categorize: which need FG bot, which need pathfinding, which need specific fixtures. Document skip reasons. | Open |
| 6.4 | **Update test baseline** — After fixes, record new pass/fail/skip counts. Target: >50 passing LiveValidation tests. | Open |

---

## Canonical Commands

```bash
# Unit tests (fast, no server)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# WoWSharpClient tests
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure" --no-build

# Physics tests
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build

# LiveValidation (needs MaNGOS + x86 Navigation.dll)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m

# Docker services
docker compose -f docker-compose.windows.yml up -d pathfinding-service scene-data-service
```
