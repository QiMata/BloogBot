# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps; the Vanilla player/controller/observer matrix is now covered, so only binary-backed leftovers should remain.
2. Add recorded-motion validation for remote extrapolation and knockback handling.
3. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `MovementHandler` now decodes `SMSG_MONSTER_MOVE` using the real Vanilla wire formats: linear paths rebuild their node list from a destination plus packed `appendPackXYZ` offsets, while smooth (`Flying`) paths read raw Catmull-Rom nodes.
  - Cyclic smooth splines now normalize the fake `EnterCycle` start vertex into the managed runtime’s `[start, ...nodes..., start]` loop shape, and `ActiveSpline` now wraps Catmull-Rom control-point lookup across the closing segment instead of clamping at the ends.
  - The reachable managed spline audit is closed for Vanilla `SMSG_MONSTER_MOVE`: Bezier/other evaluator paths are still present in server/client code, but the current Vanilla movement wire surface reaches linear and `Flying`/Catmull-Rom only.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnFirstSegment|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnClosingSegment" -v n` -> `5 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1340 passed`, `1 skipped`
  - `dotnet test Tests/WowSharpClient.NetworkTests/WowSharpClient.NetworkTests.csproj --configuration Release --no-build -v n` -> `117 passed`
- Files changed:
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs | Select-Object -First 260`
