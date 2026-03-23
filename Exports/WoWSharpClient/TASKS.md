# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded-motion validation for remote extrapolation and knockback handling.
2. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.
3. Keep the movement opcode sweep closed by only adding new bridge/application handlers when a binary-backed non-cheat gap is found.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `MovementHandler`, `OpCodeDispatcher`, and `WorldClient` now cover the remaining non-cheat observer movement rebroadcasts from the Vanilla dispatch sweep: `MSG_MOVE_START_SWIM`, `MSG_MOVE_STOP_SWIM`, `MSG_MOVE_START_PITCH_UP`, `MSG_MOVE_START_PITCH_DOWN`, `MSG_MOVE_STOP_PITCH`, and `MSG_MOVE_SET_PITCH`.
  - Remote units now retain swimming state and `SwimPitch` when those packets arrive instead of dropping the updates, which closes the last normal observer-side movement opcode gap found in the current 1.12.1 dispatch-table audit.
  - The remaining opcode names absent from the dispatcher/bridge are cheat/debug-only paths (`*_CHEAT`, logging/collision/gravity toggles, raw-position ack), not runtime parity gaps for normal movement.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObserverMovementPitchOpcodes_UpdateRemoteUnitSwimPitch" -v n` -> `16 passed`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~BridgeRegistration_MovementOpcodes_Registered" -v n` -> `1 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1346 passed`, `1 skipped`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n` -> `117 passed`
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs | Select-Object -First 260`
