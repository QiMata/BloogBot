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
  - Added deterministic coverage for the full controller speed-change family so run, run-back, swim, walk, swim-back, and turn-rate ACK/application now all have direct managed proof.
  - Added deterministic remote-unit coverage for the observer-side player movement broadcast matrix, including hover/feather-fall, run/walk mode, and the missing run-back, walk, swim-back, and turn-rate speed packets.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.ForceSpeedChangeOpcodes_ParseApplyAndAck|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObjectManagerWorldSessionTests.ObserverMovementSpeedOpcodes_UpdateRemoteUnitState" -v n` -> `22 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1336 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs | Select-Object -First 260`
