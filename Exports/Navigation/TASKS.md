# Navigation Tasks

## Scope
- Project: `Exports/Navigation`
- Owns native pathfinding, collision queries, and physics integration consumed by pathfinding/physics services and tests.
- This file tracks first-party implementation gaps only (exclude third-party vendor TODOs under `Detour/` and `g3dlite/`).
- Master tracker: `MASTER-SUB-007`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep scans scoped to `Exports/Navigation` and related direct test projects only.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/Navigation/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [ ] Navigation native build succeeds (`Release|x64`) before test validation.
- [ ] Pathfinding runtime has access to expected MMAP/VMAP assets when validating corpse-run behavior.
- [ ] Native/exported API contracts are synchronized with downstream C#/protobuf consumers.

## Evidence Snapshot (2026-02-25)
- `OverlapCapsule` export is still stubbed:
  - `Exports/Navigation/PhysicsTestExports.cpp:231` returns `0` with `TODO`.
- Query contract ambiguity remains in `SceneQuery`:
  - `Exports/Navigation/SceneQuery.h:21` marks `backfaceCulling` as unused/TODO.
  - `Exports/Navigation/SceneQuery.h:26` marks `returnPhysMat` as future/not implemented.
- PathFinder still has machine-specific debug side effects:
  - `Exports/Navigation/PathFinder.cpp:525` writes to `C:\\Users\\Drew\\Repos\\bloog-bot-v2\\Bot\\navigationDebug.txt`.
  - `Exports/Navigation/PathFinder.cpp:489` contains unresolved `createFilter` TODO.
- Native build is blocked in this shell:
  - `dotnet msbuild Exports/Navigation/Navigation.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64` fails with `MSB4278` (`Microsoft.Cpp.Default.props` missing).
- Downstream pathfinding test inventory is discoverable:
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --list-tests` lists path and physics tests.

## P0 Active Tasks (Ordered)

### NAV-MISS-001 Implement `OverlapCapsule` test export by routing to existing SceneQuery implementation
- [ ] Problem: `PhysicsTestExports.cpp` currently returns `0` for `OverlapCapsule`, masking overlap regressions.
- [ ] Target files:
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/SceneQuery.h`
- [ ] Required change: resolve map tree access and call `SceneQuery::OverlapCapsule` so export output reflects real collision geometry.
- [ ] Validation command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Capsule|FullyQualifiedName~Collision" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: `OverlapCapsule` no longer returns stubbed zero for valid overlap scenarios; failing overlap cases surface in test output.

### NAV-MISS-002 Resolve explicit query-contract drift in `QueryParams` (`returnPhysMat`, `backfaceCulling`)
- [ ] Problem: `SceneQuery.h` advertises fields marked future/unused, creating ambiguous behavior for callers.
- [ ] Target files:
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
- [ ] Required change: implement or remove unsupported flags and document active behavior so callers cannot rely on undefined query semantics.
- [ ] Validation command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~SceneQuery|FullyQualifiedName~PhysicsReplay" --logger "console;verbosity=minimal"`.
- [ ] Acceptance: active query flags have deterministic behavior and no first-party "future/not implemented" ambiguity remains.

### NAV-MISS-003 Remove machine-specific fallback/debug side effects from `PathFinder`
- [ ] Problem: `PathFinder::HaveTile` writes to a hardcoded local path (`C:\Users\Drew\Repos\...\navigationDebug.txt`) and `createFilter` still contains an unresolved TODO.
- [ ] Target files:
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/PathFinder.h`
- [ ] Required change: replace local-path file write with repo-safe diagnostics strategy and make filter initialization intent explicit (no ambiguous TODO placeholder).
- [ ] Validation command: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- [ ] Acceptance: no machine-specific debug artifact paths remain; filter behavior is explicit and reproducible across environments.

### NAV-MISS-004 Validate corpse runback path use (consume returned path nodes without wall-loop fallback)
- [ ] Problem: corpse runback can degrade into wall-running behavior when path output consumption diverges from generated waypoints.
- [ ] Target files:
  - `Exports/Navigation/PathFinder.cpp`
  - `Services/BackgroundBotRunner` path-consumption call sites (linked task execution in service backlog)
- [ ] Required change: verify generated path is valid and consumed directly by runback movement flow; remove probe-point/random-strafe fallback in this scenario.
- [ ] Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`.
- [ ] Acceptance: runback follows path nodes around obstacles and reaches corpse/rez window without repetitive wall collisions.

## Simple Command Set
1. `msbuild Exports/Navigation/Navigation.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented|stub" Exports/Navigation`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `MASTER-SUB-007` (`Exports/Navigation/TASKS.md`)
- Current focus: `NAV-MISS-001`
- Last delta: added evidence-backed blocker context (`MSB4278` native build tooling), plus concrete symbol-level findings for `OverlapCapsule`, query flags, and `PathFinder` debug side effects.
- Pass result: `delta shipped`
- Validation/tests run:
  - `rg --line-number "OverlapCapsule|TODO" Exports/Navigation/PhysicsTestExports.cpp Exports/Navigation/SceneQuery.h Exports/Navigation/PathFinder.cpp`
  - `rg --line-number "navigationDebug.txt|HaveTile\\(|returnPhysMat|backfaceCulling|createFilter" Exports/Navigation/PathFinder.cpp Exports/Navigation/SceneQuery.h`
  - `dotnet msbuild Exports/Navigation/Navigation.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64` (`MSB4278` in current shell)
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --list-tests`
- Files changed:
  - `Exports/Navigation/TASKS.md`
- Next command: `Get-Content -Path 'Exports/WinImports/TASKS.md' -TotalCount 360`
- Loop Break: if two passes produce no delta, record blocker + exact next command and move to next queued file.
