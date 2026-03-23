# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded directional remote-unit packet fixtures so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Add focused knockback trajectory coverage against parsed movement impulses.
3. Add movement-opcode sweep tests as new gaps are discovered in the dispatch-table audit, including server-controlled mover flag/rate packets.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added deterministic coverage for local-player movement flag toggle packets, verifying parse -> player-state mutation -> ACK payload for water-walk, land-walk, hover, unhover, feather-fall, and normal-fall.
  - Added deterministic remote-unit coverage for the missing server-controlled spline speed and spline flag opcodes so mover-state regressions now fail in managed tests instead of only in live parity checks.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineSpeedOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.SplineFlagOpcodes_UpdateRemoteUnitState" -v n` -> `20 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1317 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs | Select-Object -Skip 2100 -First 220`
