# PathfindingService.Tests Tasks

## Scope
- Directory: `Tests/PathfindingService.Tests`
- Project: `PathfindingService.Tests.csproj`
- Master tracker: `docs/TASKS.md` (`MASTER-SUB-024`)
- Local goal: verify pathfinding outputs are valid and consumed deterministically for corpse runback, combat movement, and gathering travel parity.

## Execution Rules
1. Execute tasks in numeric order unless blocked by missing data or fixture prerequisites.
2. Keep scan scope to this project path and directly referenced implementation files only.
3. Use one-line `dotnet test` commands and include `test.runsettings` for timeout enforcement.
4. Never blanket-kill `dotnet`; use repo-scoped process cleanup only and record evidence.
5. Move completed IDs to `Tests/PathfindingService.Tests/TASKS_ARCHIVE.md` in the same session.
6. If two consecutive passes produce no file delta, record blocker and exact next command, then advance to the next queue file in `docs/TASKS.md`.
7. Add a one-line `Pass result` in `Session Handoff` (`delta shipped` or `blocked`) every pass so compaction resumes from `Next command` directly.
8. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
9. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `Navigation.dll` is present in test output.
- [x] `NavigationFixture` auto-discovers a working nav data root for the current shell.
- [x] `Tests/PathfindingService.Tests/test.runsettings` is used (`10-minute TestSessionTimeout`).

## P0 Active Tasks (Ordered)

- None.

## Simple Command Set
1. Full project sweep: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
2. Reroute + corpse-run focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`
3. Route validity focus: `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine|FullyQualifiedName=PathfindingService.Tests.PathfindingTests.CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget|FullyQualifiedName~PathfindingBotTaskTests" --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-04-15
- Active task: `none`
- Last delta:
  - Added `PathAffordanceClassifierTests` to pin native-classification aggregation, counts, max climb/gap/drop metrics, and unsupported route decisions.
  - Revalidated `NavigationOverlayAwarePathTests` and `PathfindingSocketServerIntegrationTests` against the expanded path response metadata.
  - Kept native affordance response aggregation latency-safe by covering the default fast classifier path while leaving explicit native segment classification to `Navigation.Physics.Tests`.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --settings Tests/PathfindingService.Tests/test.runsettings --filter "FullyQualifiedName~NavigationOverlayAwarePathTests|FullyQualifiedName~PathAffordanceClassifierTests|FullyQualifiedName~PathfindingSocketServerIntegrationTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
- Files changed:
  - `Tests/PathfindingService.Tests/PathAffordanceClassifierTests.cs`
  - `Tests/PathfindingService.Tests/TASKS.md`
  - `docs/TASKS.md`
- Blockers:
  - none
- Next command: `rg -n "^- \\[ \\]|\\[ \\] Problem|Active task:" docs/TASKS.md Exports/Navigation/TASKS.md Services/PathfindingService/TASKS.md Tests/PathfindingService.Tests/TASKS.md Tests/Navigation.Physics.Tests/TASKS.md Exports/BotRunner/TASKS.md Tests/BotRunner.Tests/TASKS.md`
