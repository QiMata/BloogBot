# WoWStateManagerUI Tasks

## Scope
- Directory: `UI/WoWStateManagerUI`
- Project: `UI/WoWStateManagerUI/WoWStateManagerUI.csproj`
- Master tracker: `MASTER-SUB-038`
- Primary implementation surfaces:
- `UI/WoWStateManagerUI/Converters/GreaterThanZeroToBooleanConverter.cs`
- `UI/WoWStateManagerUI/MainWindow.xaml`
- `UI/WoWStateManagerUI/Views/StateManagerViewModel.cs`
- Documentation surface:
- `UI/WoWStateManagerUI/README.md`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line.
3. Prioritize deterministic, explicit binding behavior over implicit WPF defaults.
4. Do not leave converter behavior ambiguous; every converter must define clear one-way or two-way contract.
5. Move completed items to `UI/WoWStateManagerUI/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `GreaterThanZeroToBooleanConverter.ConvertBack` currently throws `NotImplementedException` (`Converters/GreaterThanZeroToBooleanConverter.cs:18`).
- [x] Current convert logic enables for non-negative indexes (`intValue >= 0`) (`Converters/GreaterThanZeroToBooleanConverter.cs:12`).
- [x] `SelectCharacterIndex` is projected from `_selectedCharacterIndex` and is used as the UI selection gate (`Views/StateManagerViewModel.cs:126`).
- [x] Converter is wired to many `IsEnabled` bindings in `MainWindow.xaml` (`MainWindow.xaml:86-136`) and is a high-impact control gate.
- [x] No focused converter tests currently exist under `Tests/*` for `GreaterThanZeroToBooleanConverter`/`SelectCharacterIndex` (targeted `rg` returned no matches).

## Evidence Snapshot (2026-02-25)
- `dotnet restore UI/WoWStateManagerUI/WoWStateManagerUI.csproj` succeeded (`All projects are up-to-date for restore`).
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` succeeded (warnings only, no errors).
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Converter contract drift confirmed:
- name implies strict `> 0`, but implementation currently uses `>= 0` (`Converters/GreaterThanZeroToBooleanConverter.cs:12`);
- reverse conversion throws `NotImplementedException` (`Converters/GreaterThanZeroToBooleanConverter.cs:18`).
- Binding footprint confirmed:
- converter resource declaration at `MainWindow.xaml:12`;
- repeated `IsEnabled` usage bound to `SelectCharacterIndex` at `MainWindow.xaml:86-136`.
- README command drift confirmed:
- uses stale commands `dotnet build StateManagerUI.csproj` / `dotnet run --project StateManagerUI.csproj` (`README.md:203`, `:209`) instead of `WoWStateManagerUI.csproj`.

## P0 Active Tasks (Ordered)
1. [x] `UI-MISS-001` Remove `NotImplementedException` path from `ConvertBack` and make converter direction explicit.
- **Done (prior session).** `ConvertBack` now returns `Binding.DoNothing`.

2. [x] `UI-MISS-002` Align converter naming and logic with selection semantics (`-1` invalid, `0+` valid).
- **Done (2026-02-28).** Fixed converter logic from `>= 0` to `> 0` to match the class name "GreaterThanZero".

3. [ ] `UI-MISS-003` Add converter-level regression coverage for selection gating.
- Evidence: no direct test currently asserts selection-index to enabled-state conversion behavior.
- Files: `UI/WoWStateManagerUI/Converters/GreaterThanZeroToBooleanConverter.cs`, `Tests/*` (new/updated focused tests).
- Required breakdown: add cases for `-1`, `0`, positive values, non-int values, and convert-back behavior contract.
- Validation: `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UI|FullyQualifiedName~StateManager|FullyQualifiedName~Converter" --logger "console;verbosity=minimal"`.

4. [ ] `UI-MISS-004` Reduce README to a command-first operator/developer flow for this UI project.
- Evidence: README is broad and includes stale/indirect guidance that slows agent startup.
- Files: `UI/WoWStateManagerUI/README.md`, `UI/WoWStateManagerUI/TASKS.md`.
- Required breakdown: keep one build command, one run command, and one converter-binding contract section tied to `UI-MISS-001..003`.
- Validation: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`.

## Simple Command Set
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet run --project UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `rg -n "GreaterThanZeroToBooleanConverter|SelectCharacterIndex|IsEnabled=\\\"\\{Binding SelectCharacterIndex" UI/WoWStateManagerUI`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `UI-MISS-001` (remove `NotImplementedException` convert-back path and set explicit converter direction).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `UI/WoWStateManagerUI/TASKS.md`.
- Blockers: None.
- Next task: `UI-MISS-001`.
- Next command: `Get-Content -Path 'docs/TASKS.md' -TotalCount 420`.
