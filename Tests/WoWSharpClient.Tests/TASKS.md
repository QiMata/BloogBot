# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add a recorded directional remote-unit packet fixture so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Expand spline parity coverage once the managed spline audit identifies which WoW.exe interpolation paths are still unmatched.
3. Add movement-opcode sweep tests as new gaps are discovered in the dispatch-table audit.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added deterministic extrapolation tests for backward speed, strafe basis, and diagonal damping.
  - Added object-manager coverage proving remote-unit create packets now seed extrapolation base/facing/flags/time immediately.
  - Added knockback coverage for parse -> ACK -> pending impulse storage and `MovementController` impulse consumption into physics input.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWUnitExtrapolationTests|FullyQualifiedName~ObjectManagerWorldSessionTests.RemoteUnitAdd_PrimesExtrapolationStateFromMovementBlock|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack_ParseStoresImpulseClearsDirectionAndAcks|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" -v n` -> `6 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1286 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Movement/SplineControllerTests.cs | Select-Object -First 260`
