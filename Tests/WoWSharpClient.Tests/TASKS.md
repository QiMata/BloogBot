# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded directional remote-unit packet fixtures so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Keep remote extrapolation work focused on fixture-backed parity gaps; deterministic math thresholds and basis handling are already covered here.
3. Keep the movement-opcode sweep closed by adding coverage only when a new binary-backed non-cheat dispatch gap is discovered.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added deterministic extrapolation guardrail coverage for the WoW.exe-backed thresholds already implemented in `WoWUnit.GetExtrapolatedPosition(...)`: sub-jitter speed, teleport-speed outliers, and stale updates now all prove the method returns the current position instead of inventing drift.
  - The remaining extrapolation gap is still fixture quality, not managed math: the repo does not yet contain a recorded directional remote-unit packet stream suitable for measured replay-accuracy assertions.
  - Existing full-suite coverage still holds after the new threshold tests.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWUnitExtrapolationTests" -v n` -> `6 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1349 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
- Next command:
  - `rg -n "OrgrimmarElevator|elevator" Tests/Navigation.Physics.Tests Tests/WoWSharpClient.Tests docs -g "*.cs" -g "*.md"`

## Prior Session
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
