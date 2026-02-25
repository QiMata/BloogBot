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
- [x] `Program.cs` provisions MySQL + WoW containers with bind mounts and endpoint mapping, but startup preflight checks for required host paths are absent (`Program.cs:22-27`).
- [x] `WowServerConfig.cs` hardcodes credentials and unpinned container images (`WowServerConfig.cs:4-9`).
- [x] Bind-mount sources use relative `./config` and `./data` paths with no explicit root normalization (`WowServerConfig.cs:28-29`, `Program.cs:22-27`).
- [x] README command path is outdated (`dotnet run --project UI/WWoW.Systems/WWoW.Systems.AppHost` at `README.md:121`) and references old naming (`WWoW.Systems.*` headers/paths).
- [x] Launch profiles exist (`https`, `http`), but no explicit task-level contract currently ties them to simple operator commands (`Properties/launchSettings.json`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore UI/Systems/Systems.AppHost/Systems.AppHost.csproj` succeeded.
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Hardcoded configuration and relative paths confirmed:
- constants for DB credentials and container images in `WowServerConfig.cs:4-9`;
- relative path constants in `WowServerConfig.cs:28-29`;
- bind mounts in `Program.cs:22-27`.
- Dependency linkage currently relies on `wowServer.WaitFor(database)` (`Program.cs:39`) with no explicit readiness diagnostics contract in docs/tests.
- README drift confirmed:
- top-level naming/paths still use `WWoW.Systems.AppHost` style (`README.md:1`, `:36`, `:39`);
- run command still points to old path at `README.md:121`.

## P0 Active Tasks (Ordered)
1. [ ] `SAH-MISS-001` Externalize AppHost container settings from hardcoded constants to configuration.
- Evidence: `WowServerConfig.cs` stores DB credentials and container image names as compile-time constants.
- Files: `UI/Systems/Systems.AppHost/WowServerConfig.cs`, `UI/Systems/Systems.AppHost/Program.cs`, `UI/Systems/Systems.AppHost/appsettings*.json`.
- Required breakdown: bind image names, credentials, and optional tags from configuration/user-secrets; keep sane defaults for local runs.
- Validation: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

2. [ ] `SAH-MISS-002` Add startup preflight validation for required config/data bind-mount sources.
- Evidence: `Program.cs` mounts `./config/*` and `./data/{dbc,maps,mmaps,vmaps}` without pre-checks.
- Files: `UI/Systems/Systems.AppHost/Program.cs`, `UI/Systems/Systems.AppHost/WowServerConfig.cs`.
- Required breakdown: verify required files/directories exist before container creation and fail fast with actionable error output.
- Validation: `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

3. [ ] `SAH-MISS-003` Normalize host path resolution so AppHost behaves identically when launched from solution root or project directory.
- Evidence: current mount source paths are relative strings and can resolve differently by working directory.
- Files: `UI/Systems/Systems.AppHost/Program.cs`, `UI/Systems/Systems.AppHost/WowServerConfig.cs`.
- Required breakdown: compute absolute paths from a deterministic base and log resolved path values at startup.
- Validation: `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

4. [ ] `SAH-MISS-004` Add deterministic readiness checks and failure output for DB -> WoW dependency startup.
- Evidence: `wowServer.WaitFor(database)` exists, but there is no explicit validation contract for endpoint readiness/failure diagnostics.
- Files: `UI/Systems/Systems.AppHost/Program.cs`, `UI/Systems/Systems.AppHost/README.md`.
- Required breakdown: define ready/not-ready signals for DB and WoW services and document expected startup/failure behavior.
- Validation: `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

5. [ ] `SAH-MISS-005` Add a simple no-surprises local command profile for AppHost debugging.
- Evidence: launch settings exist, but task/docs command surface is inconsistent and drifts between files.
- Files: `UI/Systems/Systems.AppHost/Properties/launchSettings.json`, `UI/Systems/Systems.AppHost/README.md`, `UI/Systems/Systems.AppHost/TASKS.md`.
- Required breakdown: keep one default local run command and one explicit development profile command.
- Validation: `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj`.

6. [ ] `SAH-MISS-006` Correct README command paths and prerequisites to match actual repository layout.
- Evidence: README still references `UI/WWoW.Systems/WWoW.Systems.AppHost`, which does not match this project's location.
- Files: `UI/Systems/Systems.AppHost/README.md`.
- Required breakdown: update path references, prerequisite sections, and service endpoint documentation so users can launch without rediscovery.
- Validation: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

## Simple Command Set
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `SAH-MISS-001` (externalize hardcoded AppHost container settings).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `UI/Systems/Systems.AppHost/TASKS.md`.
- Blockers: None.
- Next task: `SAH-MISS-001`.
- Next command: `Get-Content -Path 'UI/Systems/Systems.ServiceDefaults/TASKS.md' -TotalCount 360`.
