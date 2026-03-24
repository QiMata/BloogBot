# BackgroundBotRunner Tasks

## Scope
- Directory: `Services/BackgroundBotRunner`
- Project: `BackgroundBotRunner.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: headless runner lifecycle, docker packaging, and FG/BG behavior parity through the shared BotRunner stack.

## Execution Rules
1. Keep changes scoped to the worker plus directly related startup/config call sites.
2. Every parity or lifecycle slice must leave a concrete validation command in `Session Handoff`.
3. Never blanket-kill repo processes; use repo-scoped cleanup or explicit PIDs only.
4. Archive completed items to `Services/BackgroundBotRunner/TASKS_ARCHIVE.md` when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `BBR-PAR-001` FG/BG action and movement parity
- [ ] Continue tracing the remaining follow-loop and interaction timing divergences against the now-complete FG interaction surface.

2. `BBR-PAR-002` Live gathering/NPC timing
- [ ] Re-run the existing gathering and NPC interaction parity work once the dockerized vmangos stack is online so visibility timing is measured against the new environment.

3. `BBR-DOCKER-001` Containerized worker validation
- [ ] Validate the standalone BG container profile and the `WoWStateManager`-spawned BG worker path against the same endpoint contract.

## Session Handoff
- Last updated: 2026-03-24
- Active task: `BBR-PAR-002`
- Last delta:
  - Updated `CombatRotationTask.Update()` again so melee engage now behaves like the older sequence path: a grounded face/settle tick happens before `ATTACKSWING`, and airborne melee engage attempts stay suppressed until the bot has landed and re-faced the target.
  - Removed the old shared-task aggressor chase-timeout fallback that blindly forced `StartMeleeAttack()` after ~3s of chase. In the current outdoor mining repro that fallback was firing on ledge fights and pinning the bot in stationary combat instead of letting chase/path recovery continue.
  - Expanded `CombatRotationTaskTests` to cover the new melee engage timing and to lock out the old blind-chase regression: in-range melee now primes once before attacking, airborne melee waits for a grounded face tick, and out-of-range aggressors no longer auto-swing just because a chase timeout elapsed.
  - Re-ran the BG-only mining slice twice. The remaining live blocker moved materially: first from candidate `7/15` to candidate `4/15`, then to candidate `3/15`. `BADFACING` dropped to `1`, `NOTINRANGE` to `0`, `NullWaypoint` to `4`, and `AirborneBlocked` to `321`; the current failure is a later stationary melee/combat loop around `(-443.9,-4829.0,36.5)` while `GatheringRouteTask` is paused on candidate `3/15`.
  - Left `PathfindingService` untouched and undeployed in this pass per the current deployment rule; the live improvement came entirely from shared BotRunner combat logic.
- Pass result: `melee engage timing improved; live mining progressed from candidate 7 to candidate 3 before stalling in a later stationary combat loop`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (89/89)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (90/90)` after removing the blind aggressor chase-timeout swing and adding its regression test
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m` -> `failed after 5m16s` with the blocker shifted to candidate `4/15` (`BADFACING=1`, `NullWaypoint=10`, `AirborneBlocked=412`, `HeroicStrike=95`)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m` -> `failed after 5m15s` with the blocker shifted again to candidate `3/15` (`BADFACING=1`, `NullWaypoint=4`, `AirborneBlocked=321`, `HeroicStrike=79`)
  - `Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and ($targets -contains $_.ExecutablePath) }` -> `no leftover WoWStateManager/BackgroundBotRunner/PathfindingService/WoW.exe processes`
- Files changed:
  - `Exports/BotRunner/Tasks/CombatRotationTask.cs`
  - `Tests/BotRunner.Tests/Combat/CombatRotationTaskTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Services/BackgroundBotRunner/TASKS.md`
- Next command: `rg -n -C 2 "candidate=3/15|pause reason=combat state=MoveToCandidate candidate=3/15|MSG_MOVE_HEARTBEAT Pos=\(-443\.9,-4829\.0,36\.5\)|spell=78 targetUnit=0xF130000C350032B1" TestResults/LiveLogs/GatheringProfessionTests.log`
- Blockers: the remaining mining failure is no longer the old candidate-7 cliff/facing issue. The bot now advances to candidate `3/15` before stalling in a later stationary combat loop while being hit at `(-443.9,-4829.0,36.5)`; the next fix should target that later melee/chase ownership window, while the walkable-triangle-preserving smoothing follow-up remains intentionally deferred until the current higher-priority BG combat/movement recovery work is cleared.
