# Systems.AppHost Tasks

## Scope
- Directory: `UI/Systems/Systems.AppHost`
- Project: `UI/Systems/Systems.AppHost/Systems.AppHost.csproj`
- Master tracker: `MASTER-SUB-036`
- Primary implementation surfaces:
  - `UI/Systems/Systems.AppHost/Program.cs`
  - `UI/Systems/Systems.AppHost/WowServerConfig.cs`
  - `UI/Systems/Systems.AppHost/appsettings*.json`
  - `UI/Systems/Systems.AppHost/README.md`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line.
3. Ensure AppHost orchestration remains deterministic for local parity runs.
4. Never blanket-kill `dotnet`; any process cleanup must be repo-scoped and recorded.
5. Move completed items to `UI/Systems/Systems.AppHost/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] Container images, credentials, ports, volumes, and bind paths are read from `WowServer` configuration.
- [x] Required host bind-mount paths are validated before Aspire resource creation.
- [x] Bind-mount sources resolve to absolute paths from the AppHost project directory by default.
- [x] README command paths match the actual repository layout.
- [x] A `local` launch profile provides a no-browser HTTP debugging path.

## P0 Active Tasks (Ordered)
None.

Known remaining work in this owner: `0` items.

## Completed P0 Items
- [x] `SAH-MISS-001` Externalize AppHost container settings from hardcoded constants to configuration.
- [x] `SAH-MISS-002` Add startup preflight validation for required config/data bind-mount sources.
- [x] `SAH-MISS-003` Normalize host path resolution so AppHost behaves identically when launched from solution root or project directory.
- [x] `SAH-MISS-004` Add deterministic readiness checks and failure output for DB -> WoW dependency startup.
- [x] `SAH-MISS-005` Add a simple no-surprises local command profile for AppHost debugging.
- [x] `SAH-MISS-006` Correct README command paths and prerequisites to match actual repository layout.

## Simple Command Set
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local`
- `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `SAH-MISS-001` through `SAH-MISS-006` are complete.
- Last delta: externalized AppHost settings to `WowServer` configuration, added absolute project-root path resolution and bind-mount preflight validation, added a `local` launch profile, and replaced stale README paths.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local` -> expected preflight failure listing missing `config`/`data` bind-mount sources in this workspace
- Files changed:
  - `UI/Systems/Systems.AppHost/Program.cs`
  - `UI/Systems/Systems.AppHost/WowServerConfig.cs`
  - `UI/Systems/Systems.AppHost/appsettings.json`
  - `UI/Systems/Systems.AppHost/Properties/launchSettings.json`
  - `UI/Systems/Systems.AppHost/README.md`
  - `UI/Systems/Systems.AppHost/TASKS.md`
  - `UI/Systems/Systems.AppHost/TASKS_ARCHIVE.md`
- Blockers: none.
- Next command: `Get-Content -Path 'UI/Systems/Systems.ServiceDefaults/TASKS.md' -TotalCount 360`
