# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps; the Vanilla player/controller/observer matrix is now covered, so only binary-backed leftovers should remain.
2. Continue the spline audit from the new server-time/facing fixes and confirm whether any binary-backed modes beyond those still differ.
3. Add recorded-motion validation for remote extrapolation and knockback handling.
4. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `MovementHandler` now parses the remaining observer-side player movement broadcasts from the Vanilla 1.12.1 sender matrix, including hover/feather-fall, run/walk mode, and the missing run-back, walk, swim-back, and turn-rate speed packets.
  - `WorldClient` and `OpCodeDispatcher` now bridge the full Vanilla player/controller/observer movement matrix instead of leaving those observer opcodes silently unhandled.
  - Deterministic tests now cover the complete controller speed family plus the observer-side speed/flag broadcast surface for remote units.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet build Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ForceSpeedChangeOpcodes_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementSpeedOpcodes_UpdateRemoteUnitState" -v n` -> `22 passed`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WorldClientTests.BridgeRegistration_MovementOpcodes_Registered" -v n` -> `1 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1336 passed`, `1 skipped`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n` -> `117 passed`
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- Next command:
  - `Get-Content Exports/WoWSharpClient/Movement/SplineController.cs | Select-Object -First 260`
