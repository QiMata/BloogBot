# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded directional remote-unit packet fixtures so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Add focused knockback trajectory coverage against parsed movement impulses.
3. Keep the movement-opcode sweep closed by adding coverage only when a new binary-backed non-cheat dispatch gap is discovered.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added observer-state coverage for the remaining non-cheat Vanilla movement rebroadcasts discovered in the dispatch-table sweep: swim start/stop plus pitch start/stop/set.
  - Remote-unit tests now prove those packets update `MOVEFLAG_SWIMMING` and `SwimPitch` through the same managed path the object manager uses at runtime.
  - `WorldClient` bridge-registration coverage now includes the new swim/pitch opcodes, reducing the outstanding movement-opcode work to future binary-backed discoveries rather than known gaps.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ObserverMovementFlagOpcodes_UpdateRemoteUnitState|FullyQualifiedName~ObserverMovementPitchOpcodes_UpdateRemoteUnitSwimPitch" -v n` -> `16 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1346 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WowSharpClient.NetworkTests/WorldClientTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs | Select-Object -First 260`
