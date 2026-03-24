# WoWStateManager Tasks

## Scope
- Directory: `Services/WoWStateManager`
- Project: `WoWStateManager.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: lifecycle orchestration, snapshot/action forwarding, docker-aware service bootstrap, and spawned bot-worker parity.

## Execution Rules
1. Keep changes scoped to `Services/WoWStateManager` plus direct consumers/tests.
2. Never blanket-kill `dotnet` or `WoW.exe`; use repo-scoped cleanup or explicit PIDs only.
3. Every lifecycle/bootstrap change must include a concrete validation command in `Session Handoff`.
4. Archive completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md` in the same session when they no longer need follow-up.
5. Every pass must record one-line `Pass result` and exactly one executable `Next command`.

## Active Priorities
1. `WSM-PAR-001` Quest snapshot sync lag
- [ ] Trace quest-state latency between WoWSharpClient packet handlers, StateManager snapshot publication, and test assertions.

2. `WSM-HOST-001` Host-side orchestrator against containerized dependencies
- [ ] Validate `WoWStateManager` as a host-side process against containerized `vmangos-server` / `pathfinding-service` dependencies.
- [ ] Verify spawned `BackgroundBotRunner` instances inherit the docker-safe endpoint overrides (`PathfindingService__*`, `CharacterStateListener__*`, `RealmEndpoint__IpAddress`) while `WoWStateManager` remains outside Docker.

3. `WSM-BOOT-001` Bootstrap cleanup follow-up
- [ ] Re-check any remaining assumptions that local `C:\Mangos\server` processes are always host-launched once the docker path becomes the default path.

## Session Handoff
- Last updated: 2026-03-24
- Active task: `WSM-HOST-001`
- Last delta:
  - `WoWStateManager` is now treated as host-side by design because it must launch local `WoW.exe` clients; the Windows compose stack should no longer include a `wow-state-manager` container.
  - Kept the idle host-side `WoWStateManager` path in place with `MangosServer__AutoLaunch=false` and `WWOW_SETTINGS_OVERRIDE=StateManagerSettings.Idle.json`.
  - Updated the stack docs so the containerized pieces stay `vmangos-server` / `pathfinding-service`, while `WoWStateManager` remains outside Docker.
- Pass result: `delta shipped`
- Validation/tests run:
  - `Start-Process Bot\Release\net8.0\WoWStateManager.exe` with `MangosServer__AutoLaunch=false` and `WWOW_SETTINGS_OVERRIDE=Services\WoWStateManager\Settings\StateManagerSettings.Idle.json` -> `succeeded` (`PID 27628`)
  - `Get-NetTCPConnection -LocalPort 5002 -State Listen` -> `succeeded` (`127.0.0.1:5002`, `PID 27628`)
  - `Get-NetTCPConnection -LocalPort 8088 -State Listen` -> `succeeded` (`127.0.0.1:8088`, `PID 27628`)
  - `Get-Content logs\service-host\wowstatemanager.stdout.log -Tail 120` -> `succeeded` (`CharacterSettings count: 0`, `MaNGOS auto-launch disabled.`, `PathfindingService is READY`)
  - `Get-CimInstance Win32_Process ... BackgroundBotRunner.exe|ForegroundBotRunner.exe|WoW.exe` -> `succeeded` (no bot children spawned)
- Files changed:
  - `docker-compose.windows.yml`
  - `docs/DOCKER_STACK.md`
- Next command: `Get-Content .\logs\service-host\wowstatemanager.stdout.log -Tail 120`
- Blockers: none on the host-side launch path; the containerized `WoWStateManager` path is intentionally removed because it cannot launch local game clients.
