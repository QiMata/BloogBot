# Master Tasks — Integration & DLL Separation

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker). Never defer live validation tests.
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. Every fix must include or update a focused test.
7. After each shipped delta, commit and push before ending the pass.
8. **Navigation.dll x86 for BotRunner.Tests:** Copy from MSBuild x86 output or `Bot/Release/net8.0/x86/`.
9. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-05)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1417 | 0 | 1 | Green |
| Navigation.Physics.Tests | 666 | 2 | 1 | 2 pre-existing Undercity elevator |
| BotRunner.Tests (unit, non-LV) | 1623 | 0 | 4 | Green |
| BotRunner.Tests (LiveValidation) | 15 | 12 | 3 | 12 infra/timing failures, 0 code bugs |

---

## L1 — Fix LiveValidation Failures (Priority: CRITICAL)

**12 failures, 0 code bugs — all infra/timing. Fix them.**

Last run categorization:
- 5 DualClientParityTests — failed because WoW.exe was killed for build. Need WoW.exe running.
- 5 Dungeon entry tests (ZF, Mara, Strat×2, Gnomer) — coordinator fixture timeouts
- 1 AQ40 raid test — timeout
- 1 RFC_PrepareAndOrganizeRaid — timeout

| # | Task | Spec |
|---|------|------|
| 1.1 | **Run LiveValidation with WoW.exe alive** — Don't kill WoW.exe before running. Use `--no-build` with pre-built DLLs. Capture per-test pass/fail. | Open |
| 1.2 | **Fix DualClientParityTests** — These need both FG (WoW.exe) and BG bots. Ensure fixture launches both. Investigate each: Position, Health, NearbyUnits, GmCommand, SpellList. | Open |
| 1.3 | **Fix dungeon entry timeouts** — ZF, Mara, Strat×2, Gnomer all timeout during coordinator fixture. Root cause: coordinator waits for N bots but not enough connect in time. Increase timeout or reduce required bot count. | Open |
| 1.4 | **Fix AQ40 raid entry timeout** — 40-bot fixture likely OOMs or times out. Reduce to 10-bot smoke test or increase timeout. | Open |
| 1.5 | **Fix RFC_PrepareAndOrganizeRaid** — Coordinator prep phase times out. Check if bots are stuck at teleport or group formation. | Open |
| 1.6 | **Get LiveValidation to >30 passing** — Run full suite, fix failures one by one until >30 pass. Document each fix. | Open |

---

## L2 — Fix 156 Skipped LiveValidation Tests (Priority: High)

**156 tests skip because LiveBotFixture.IsReady=false. The bots connect but the fixture times out waiting for world entry. Fix the fixture or the bot startup pipeline.**

| # | Task | Spec |
|---|------|------|
| 2.1 | **Diagnose IsReady=false root cause** — Add logging to LiveBotFixture.InitializeAsync to show exactly where it fails: SOAP check? StateManager connect? Bot world entry timeout? Which bot doesn't enter? | Open |
| 2.2 | **Fix bot world entry timeout** — The 120s timeout may be too short if GetGroundZ is slow or pathfinding service needs warmup. Try 180s. Or pre-warm the PathfindingService before bot launch. | Open |
| 2.3 | **Run LiveValidation collection tests** — After fixing IsReady, run the 156 tests in LiveValidationCollection. Count new pass/fail/skip. Target: >80 passing. | Open |
| 2.4 | **Fix individual test failures** — For each failing test, investigate: wrong API usage, missing GM setup, incorrect assertions, timing issues. Fix and verify. | Open |

---

## D1 — Physics.dll DLL Separation (Priority: High)

**Split Navigation.dll into Physics.dll (local) + Navigation.dll (path-only, Docker).**

CMake project exists at `Exports/Physics/CMakeLists.txt`. Needs VS Developer Command Prompt to configure.

| # | Task | Spec |
|---|------|------|
| 1.1 | **Build Physics.dll x64** — `cmake -B build_x64 -A x64` from VS Developer Command Prompt. Fix any compile errors. Verify all physics exports present. | Open |
| 1.2 | **Build Physics.dll x86** — Same with `-A Win32`. Verify x86 exports match x64. | Open |
| 1.3 | **Refactor Navigation.dll to path-only** — Create a new CMakeLists.txt or vcxproj that only includes PathFinder.cpp, MoveMap.cpp, Detour. No physics, no scene. Build x64 only. | Open |
| 1.4 | **Update C# P/Invoke DllName** — NativeLocalPhysics: change `"Navigation"` to `"Physics"`. PathfindingClient stays `"Navigation"`. Test both load correctly. | Open |
| 1.5 | **Update Docker PathfindingService** — Dockerfile builds path-only Navigation.dll. Verify path requests still work. | Open |
| 1.6 | **Run full test suite with split DLLs** — Physics tests use Physics.dll, pathfinding tests use Navigation.dll. All 3706+ tests pass. | Open |

---

## Canonical Commands

```bash
# Kill WoW.exe before building (MANDATORY)
tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST
taskkill //F //PID <pid>

# Build .NET
dotnet build WestworldOfWarcraft.sln --configuration Release

# Build Navigation.dll (both architectures)
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -p:OutDir="$(pwd)/Bot/Release/net8.0/x86/"

# Unit tests
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build

# LiveValidation (don't kill WoW.exe first!)
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~LiveValidation" --no-build --blame-hang --blame-hang-timeout 10m

# Docker
docker compose -f docker-compose.vmangos-linux.yml up -d pathfinding-service scene-data-service
```
