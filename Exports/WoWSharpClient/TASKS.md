# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps, especially any binary-backed flight/control toggles still missing.
2. Continue the spline audit from the new server-time/facing fixes and confirm whether any binary-backed modes beyond those still differ.
3. Add recorded-motion validation for remote extrapolation and knockback handling.
4. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `MovementHandler` now parses/applies the remaining local-player movement flag toggle opcodes for water-walk, land-walk, hover, unhover, feather-fall, and normal-fall.
  - `WoWSharpObjectManager` now mutates the local player and emits the correct ACK payload for those movement-control packets before the next heartbeat can diverge.
  - Remote units now apply the missing server-controlled spline speed and flag opcodes so object-manager state stays aligned with mover packets during server-owned movement.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineSpeedOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineFlagOpcodes_UpdateRemoteUnitState" -v n` -> `20 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1317 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Exports/WoWSharpClient/Handlers/MovementHandler.cs | Select-Object -Skip 120 -First 220`
