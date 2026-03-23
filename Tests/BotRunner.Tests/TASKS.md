# BotRunner.Tests Tasks

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: keep BotRunner deterministic tests and live-validation assertions aligned with current FG/BG runtime parity.

## Execution Rules
1. Do not run live validation until the remaining code-only parity work is complete.
2. Prefer compile-only or deterministic test slices when the change only touches live-validation assertions.
3. Keep assertions snapshot-driven; do not reintroduce direct DB validation or FG/BG-specific skip logic for fields that now exist in both models.
4. Use repo-scoped cleanup only; never blanket-kill `dotnet` or `WoW.exe`.
5. Update this file in the same session as any BotRunner test delta.

## Active Priorities
1. Live-validation expectation cleanup
- [x] Remove stale FG coinage stub assumptions from mail/trainer live assertions now that `WoWPlayer.Coinage` is descriptor-backed.
- [ ] Sweep remaining live-validation suites for FG/BG divergence assumptions that are no longer true.

2. Final validation prep
- [ ] Keep the final live-validation chunk queued until the remaining parity implementation work is done.
- [ ] Use the final run to collect fresh Orgrimmar transport evidence with the updated FG recorder.

## Simple Command Set
1. Build:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`

2. Deterministic snapshot/protobuf slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`

3. Final live-validation chunk after code-only parity closes:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m`

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Removed the stale FG coinage stub branches from `EconomyInteractionTests` and `NpcInteractionTests`; both suites now assert FG coinage deltas directly instead of skipping when FG reported `0`.
  - Replaced the conflicted local task file with a clean current-state tracker so future BotRunner test deltas have a valid handoff target again.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"` -> `17 passed`
- Files changed:
  - `Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/NpcInteractionTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command:
  - `rg -n "skip|stub|not actionable|TODO" Tests/BotRunner.Tests/LiveValidation -g '!**/bin/**' -g '!**/obj/**'`
