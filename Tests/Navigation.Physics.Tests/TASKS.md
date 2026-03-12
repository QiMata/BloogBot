# Navigation.Physics.Tests Tasks

## Scope
- Directory: `Tests/Navigation.Physics.Tests`
- Project: `Navigation.Physics.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-023`)
- Local goal: make physics parity regressions deterministic, actionable, and fast to validate.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing fixture/data.
2. Keep every validation command one-line and runnable without custom wrappers.
3. Use `test.runsettings` for hard timeout enforcement on every command.
4. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
5. Archive completed IDs to `Tests/Navigation.Physics.Tests/TASKS_ARCHIVE.md` in the same session.
6. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
7. Start each pass by running the previous `Session Handoff -> Next command` verbatim before any broader scan.
8. After shipping one local delta, set `Next command` to the next queue-file read command so one-by-one progression survives compaction.

## Environment Checklist (Run Before P0)
- [x] `Navigation.dll` is present for this test project (`Bot/$(Config)/net8.0/Navigation.dll`).
- [x] `WWOW_DATA_DIR` resolves to a root containing `maps/`, `vmaps/`, and `mmaps/` (auto-discovered from `AppContext.BaseDirectory` = `Bot/$(Config)/net8.0/`).
- [x] `Tests/Navigation.Physics.Tests/test.runsettings` is used (10-minute `TestSessionTimeout`, `TargetPlatform=x64`).

## Evidence Snapshot (2026-03-12)
- The physics calibration archive remains valid; replay parity work is still archived in `TASKS_ARCHIVE.md`.
- New native walkability coverage is now present for `ValidateWalkableSegment`:
  - zero-distance same-ground probe returns `Clear`
  - obstructed route returns a non-clear native classification
- A real Orgrimmar graveyard->center raw-path segment is now pinned here as a deterministic regression. The export now clears it only because the native validator was hardened to use capsule-footprint support probes plus a `PhysicsStepV2` fallback for straight-sweep false negatives.
- Whole-route Orgrimmar corpse-run coverage now also exists here. The route contract carries grounded segment ends forward between validations so the deterministic check matches how runtime movement actually consumes the path instead of trusting raw smooth-path Zs blindly.
- This project is now the deterministic owner for native segment-walkability diagnostics while `Exports/Navigation` hardens support-surface selection and native route shaping.

## P0 Active Tasks (Ordered)

No active tasks - all legacy P0 tasks remain completed. Current work is support coverage for native `NAV-OBJ-002`.

### Parity Routing (BRT-PAR-002)
BRT-PAR-001 parity loop (2026-02-28) found **no physics/navigation regressions**. All 4 live failures (gathering node visibility, FlightMaster NPC timing, quest snapshot sync, PathfindingService readiness) are routed to non-physics owners:
- World object visibility -> `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-001)
- NPC interaction timing -> `Services/BackgroundBotRunner/TASKS.md` (BBR-PAR-002)
- Quest snapshot sync -> `Services/WoWStateManager/TASKS.md` (WSM-PAR-001)
- PathfindingService readiness -> `Services/PathfindingService/TASKS.md` (PFS-PAR-001)

## Simple Command Set
1. Single project sweep: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`.
2. Fast frame-loop verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
3. Teleport/fall verification: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
4. Native segment walkability focus: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"`.

## Session Handoff
- Last updated: 2026-03-12 (session 70)
- Active task: keep native `ValidateWalkableSegment` regressions pinned while `PathFinder.cpp` absorbs more of the whole-route shaping work
- Last delta: `SegmentWalkabilityTests.cs` now covers both the full Orgrimmar graveyard->center route and a short near-complete ramp regression. The test now carries grounded segment ends forward between validations, which lets the deterministic suite validate the route the same way the movement system actually traverses it. The focused native slice now passes `5/5`.
- Pass result: `delta shipped`
- Files changed:
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/SegmentWalkabilityTests.cs`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `Exports/Navigation/Navigation.h`
  - `Exports/Navigation/Navigation.cpp`
  - `Exports/Navigation/PathFinder.h`
  - `Exports/Navigation/PathFinder.cpp`
  - `Exports/Navigation/DllMain.cpp`
- Validation:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> succeeded
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~SegmentWalkabilityTests" --logger "console;verbosity=minimal"` -> `5 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"` -> `102 passed, 1 skipped`
- Blockers: deterministic whole-route coverage is now in place, but native detour generation still belongs in `Exports/Navigation/PathFinder.cpp`. This project should keep pinning regressions while native route shaping gets stronger.
- Next command: `Get-Content Exports/Navigation/PathFinder.cpp | Select-Object -Skip 260 -First 260`
