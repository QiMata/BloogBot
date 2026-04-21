# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - AppHost configuration and preflight closeout

- [x] `SAH-MISS-001` Externalized AppHost container settings from hardcoded constants to `WowServer` configuration.
- [x] `SAH-MISS-002` Added startup preflight validation for required config/data bind-mount sources.
- [x] `SAH-MISS-003` Normalized host path resolution to absolute paths rooted at the AppHost project directory by default.
- [x] `SAH-MISS-004` Added deterministic startup diagnostics for DB -> WoW dependency readiness.
- [x] `SAH-MISS-005` Added a `local` launch profile for no-browser HTTP debugging.
- [x] `SAH-MISS-006` Corrected README command paths and prerequisites for the actual repository layout.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local` -> expected preflight failure listing missing `config`/`data` bind-mount sources in this workspace
