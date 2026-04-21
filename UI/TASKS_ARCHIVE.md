# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - UI umbrella closeout

- [x] `UI-UMB-001` AppHost child execution (`SAH-MISS-001` through `SAH-MISS-006`).
- [x] `UI-UMB-002` ServiceDefaults child execution (`SSD-MISS-001` through `SSD-MISS-006`).
- [x] `UI-UMB-003` WoWStateManagerUI child execution plus converter follow-up (`UI-MISS-005`).
- [x] `UI-UMB-004` Parent/master status sync after child closeouts.
- Validation:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md` -> matched the current handoff command; the previous master queue fields are no longer present in the current docs structure.
