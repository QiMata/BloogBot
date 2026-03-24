# BotRunner.Tests Tasks

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: keep BotRunner deterministic tests and live-validation assertions aligned with current FG/BG runtime parity.

## Execution Rules
1. Do not run live validation until the remaining code-only parity work is complete.
2. Prefer compile-only or deterministic test slices when the change only touches live-validation assertions.
3. Keep assertions snapshot-driven; do not reintroduce direct DB validation or FG/BG-specific skip logic for fields that now exist in both models.
4. Use repo-scoped cleanup only; never blanket-kill `dotnet` or `WoW.exe`.
5. Update this file in the same session as any BotRunner test delta.

## Active Priorities
1. Live-validation expectation cleanup
- [x] Remove stale FG coinage stub assumptions from mail/trainer live assertions now that `WoWPlayer.Coinage` is descriptor-backed.
- [ ] Sweep remaining live-validation suites for FG/BG divergence assumptions that are no longer true.
- [ ] Keep moving explicitly BG-only live suites onto BG-only fixtures/settings so behavior regressions are isolated without launching unnecessary FG clients.

2. Final validation prep
- [ ] Keep the final live-validation chunk queued until the remaining parity implementation work is done.
- [ ] Use the final run to collect fresh Orgrimmar transport evidence with the updated FG recorder.

## Simple Command Set
1. Build:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`

2. Deterministic snapshot/protobuf slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`

3. Final live-validation chunk after code-only parity closes:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m`

## Session Handoff
- Last updated: `2026-03-24`
- Pass result: `remaining-corridor execution export shipped; deterministic movement slice passes`
- Last delta:
  - Added `CurrentWaypoints_ReturnsRemainingCorridorAfterWaypointAdvance` so `NavigationPath` now proves it only exports the active remaining corridor once the bot has consumed an earlier waypoint.
  - This closes the immediate execution-side handoff bug where `MovementController` could be reset onto stale historical corners even after BotRunner had advanced the path index.
  - Kept the deterministic `GatheringRouteTaskTests` slice green; live mining has not been re-run yet.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests"` -> `passed (58/58)`
- Files changed:
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m`
