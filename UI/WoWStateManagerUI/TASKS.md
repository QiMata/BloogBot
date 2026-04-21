# WoWStateManagerUI Tasks

## Scope
- Directory: `UI/WoWStateManagerUI`
- Project: `UI/WoWStateManagerUI/WoWStateManagerUI.csproj`
- Test project: `Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj`
- Master tracker: `MASTER-SUB-038`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line.
3. Prioritize deterministic, explicit binding behavior over implicit WPF defaults.
4. Do not leave converter behavior ambiguous; every converter must define clear one-way or two-way contract.
5. Move completed items to `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Simple Command Set
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet run --project UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release`

## P0 Active Tasks (Ordered)

All tasks complete. No open items.

## Completed Follow-up Items
- [x] `UI-MISS-005` Add converter-contract coverage for the remaining WPF converters (`NullToBoolConverter`, `PathToFilenameConverter`, `ServiceStatusToBrushConverter`) and update README binding contract.

## Session Handoff
- Last updated: 2026-04-15
- Active task: None (all UI-MISS tasks complete).
- Last delta: Completed `UI-MISS-005` by adding regression coverage for the remaining three converters and updating the README converter binding contract to cover every converter currently used by the UI.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
- Files changed:
  - `Tests/WoWStateManagerUI.Tests/Converters/NullToBoolConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/PathToFilenameConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/ServiceStatusToBrushConverterTests.cs`
  - `UI/WoWStateManagerUI/README.md`
  - `UI/WoWStateManagerUI/TASKS.md` (updated)
  - `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` (updated)
- Blockers: None.
- Next task: None remaining in this TASKS.md.
- Next command: `Get-Content -Path 'docs/TASKS.md' -TotalCount 420`.
