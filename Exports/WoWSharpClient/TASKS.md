# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Audit `SplineController` against WoW.exe's remaining spline interpolation modes and add any missing behavior.
2. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps.
3. Keep transport parity moving by closing `P7.5` runtime elevator spline evaluation support in managed/runtime movement code.
4. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `WoWUnit.GetExtrapolatedPosition()` now handles backward, strafe, and diagonal movement with the same basis vectors and `sin(45 deg)` damping used by the physics layer.
  - `WoWSharpObjectManager` now seeds remote-unit extrapolation state when a unit is first created from a movement block, fixing missing prediction startup state.
  - Knockback coverage now proves parse -> ACK -> pending impulse -> physics input consumption without adding any live-only workaround logic.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWUnitExtrapolationTests|FullyQualifiedName~ObjectManagerWorldSessionTests.RemoteUnitAdd_PrimesExtrapolationStateFromMovementBlock|FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack_ParseStoresImpulseClearsDirectionAndAcks|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" -v n` -> `6 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1286 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/Models/WoWUnit.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Exports/WoWSharpClient/Movement/SplineController.cs | Select-Object -First 260`
