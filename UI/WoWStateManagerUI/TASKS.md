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

## Session Handoff
- Last updated: 2026-02-28
- Active task: None (all UI-MISS tasks complete).
- Last delta: Completed `UI-MISS-003` (added `Tests/WoWStateManagerUI.Tests` with 25 converter regression tests covering all 3 converters) and `UI-MISS-004` (simplified README from 289 lines to ~60 lines with command-first flow and converter binding contract).
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release` -> `0 Warning(s)`, `0 Error(s)`.
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release` -> 25 passed, 0 failed.
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` -> `0 Warning(s)`, `0 Error(s)`.
- Files changed:
  - `Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj` (new test project)
  - `Tests/WoWStateManagerUI.Tests/Converters/GreaterThanZeroToBooleanConverterTests.cs` (new, 8 tests)
  - `Tests/WoWStateManagerUI.Tests/Converters/InverseBooleanConverterTests.cs` (new, 4 tests)
  - `Tests/WoWStateManagerUI.Tests/Converters/EnumDescriptionConverterTests.cs` (new, 5 tests)
  - `UI/WoWStateManagerUI/README.md` (rewritten, command-first)
  - `UI/WoWStateManagerUI/TASKS.md` (updated)
  - `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` (updated)
  - `WestworldOfWarcraft.sln` (added test project)
- Blockers: None.
- Next task: None remaining in this TASKS.md.
- Next command: `Get-Content -Path 'docs/TASKS.md' -TotalCount 420`.
