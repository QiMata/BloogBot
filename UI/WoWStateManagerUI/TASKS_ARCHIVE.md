# Task Archive

Completed items moved from TASKS.md.

## UI-MISS-001 (Completed)
**Remove `NotImplementedException` path from `ConvertBack` and make converter direction explicit.**
- `ConvertBack` now returns `Binding.DoNothing` instead of throwing.
- Completed in a prior session.

## UI-MISS-002 (Completed 2026-02-28)
**Align converter naming and logic with selection semantics (`-1` invalid, `0+` valid).**
- Fixed converter logic from `>= 0` to `> 0` to match the class name "GreaterThanZero".

## UI-MISS-003 (Completed 2026-02-28)
**Add converter-level regression coverage for selection gating.**
- Created new test project: `Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj` (xunit, net8.0-windows, UseWPF).
- Added to solution: `WestworldOfWarcraft.sln`.
- Test files (25 tests total, all passing):
  - `GreaterThanZeroToBooleanConverterTests.cs`: 8 tests covering positive ints (true), zero/negative (false), non-int fallback (false), ConvertBack (Binding.DoNothing), and selection gating boundary (-1 default).
  - `InverseBooleanConverterTests.cs`: 4 tests covering true->false, false->true, non-bool target type (throws), ConvertBack (throws).
  - `EnumDescriptionConverterTests.cs`: 5 tests covering enums with [Description], without description (null), non-string target (throws), ConvertBack (throws).
- Validation: `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release` -> 25 passed, 0 failed, 0 warnings.

## UI-MISS-004 (Completed 2026-02-28)
**Reduce README to a command-first operator/developer flow.**
- Rewrote `UI/WoWStateManagerUI/README.md` from 289 lines to ~60 lines.
- Replaced stale commands (`StateManagerUI.csproj`) with correct project name (`WoWStateManagerUI.csproj`).
- Content: Quick Start (build/run/test commands), Ports table, Project Structure, Converter Binding Contract (all 3 converters with input/output/usage), Dependencies.
- Validation: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` -> 0 warnings, 0 errors.
