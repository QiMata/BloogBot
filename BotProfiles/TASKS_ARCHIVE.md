# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-12) - BotProfiles Factory Backlog Closeout

- [x] `BP-MISS-001` Fix miswired PvP factories that returned `PvERotationTask`.
  - Archived after verification; the implementation was already present in the workspace for all 16 listed profiles.
  - `rg -n -U -P "CreatePvPRotationTask\(IBotContext botContext\)\s*=>\s*\R\s*new\s+PvERotationTask" BotProfiles -g "*.cs"` -> no matches.
- [x] `BP-MISS-002` Add regression test that guards profile factory wiring.
  - Archived after verification; `Tests/BotRunner.Tests/Profiles/BotProfileFactoryBindingsTests.cs` already existed and passed.
- [x] `BP-MISS-003` Resolve druid feral identity inconsistency.
- [x] `BP-MISS-004` Add profile capability map for low-context handoff.
- Validation:
  - `dotnet build BotProfiles/BotProfiles.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BotProfileFactoryBindingsTests" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `passed (1/1)`
