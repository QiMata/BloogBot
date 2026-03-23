# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Keep transport parity moving by closing `P7.5` runtime elevator spline evaluation support in managed/runtime movement code.
2. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps.
3. Continue the spline audit from the new server-time/facing fixes and confirm whether any binary-backed modes beyond those still differ.
4. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `SMSG_MONSTER_MOVE` parsing now carries the server spline start time through to managed runtime state instead of throwing it away.
  - `SplineController` now seeds new active splines from server start time, preserves the last point at the exact cyclic boundary, and resolves runtime facing from `SplineType`.
  - New deterministic coverage proves monster-move parsing, mid-spline activation, cyclic boundary timing, and facing resolution without live-only instrumentation.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests|FullyQualifiedName~SplineFacingTests|FullyQualifiedName~MovementBlockUpdateCloneBugTests" -v n` -> `33 passed`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MovementBlockUpdate|FullyQualifiedName~MovementInfoUpdate" -v n` -> `27 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1294 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Models/MovementBlockUpdate.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementBlockUpdateCloneBugTests.cs`
- Next command:
  - `Get-Content Exports/WoWSharpClient/Handlers/MovementHandler.cs | Select-Object -Skip 140 -First 180`
