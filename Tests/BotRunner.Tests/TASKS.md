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
- Pass result: `combat parity tightened again; live mining advanced from candidate 7 to candidate 3 before stalling in a later combat loop`
- Last delta:
  - Expanded `CombatRotationTaskTests` again for the next shared BG melee-parity slice: in-range melee now primes a grounded face tick before `StartMeleeAttack()`, airborne melee waits until after landing plus a grounded face tick, and out-of-range aggressors no longer trigger the old blind chase-timeout auto-swing.
  - Updated the shared BotRunner combat/task path accordingly in `CombatRotationTask`: melee engage timing now matches the older sequence path more closely, and the old aggressor chase-timeout fallback has been removed because it was pinning outdoor gather fights in stationary combat instead of allowing chase/path recovery.
  - Re-ran the BG-only mining slice twice against the current code. The failure moved materially from candidate `7/15` to candidate `4/15`, then to candidate `3/15`. The old cliff/facing error signature mostly collapsed (`BADFACING=1`, `NOTINRANGE=0`, `NullWaypoint=4`, `AirborneBlocked=321` on the latest run); the remaining blocker is a later stationary melee/combat loop around `(-443.9,-4829.0,36.5)`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (89/89)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (90/90)` after adding the blind-chase regression
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m` -> `failed after 5m16s` with the blocker shifted to candidate `4/15` (`BADFACING=1`, `NullWaypoint=10`, `AirborneBlocked=412`, `HeroicStrike=95`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m` -> `failed after 5m15s` with the blocker shifted to candidate `3/15` (`BADFACING=1`, `NullWaypoint=4`, `AirborneBlocked=321`, `HeroicStrike=79`)
- Files changed:
  - `Exports/BotRunner/Tasks/CombatRotationTask.cs`
  - `Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `rg -n -C 2 "candidate=3/15|pause reason=combat state=MoveToCandidate candidate=3/15|MSG_MOVE_HEARTBEAT Pos=\(-443\.9,-4829\.0,36\.5\)|spell=78 targetUnit=0xF130000C350032B1" TestResults/LiveLogs/GatheringProfessionTests.log`
