# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Finish the movement opcode completeness sweep against the client dispatch table and close any remaining ACK/application gaps.
2. Continue the spline audit from the new server-time/facing fixes and confirm whether any binary-backed modes beyond those still differ.
3. Add recorded-motion validation for remote extrapolation and knockback handling.
4. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Moving transport gameobjects now retain spline-facing metadata and can activate runtime splines from object create/update movement blocks.
  - `SplineController` now advances gameobject-owned splines directly and resyncs transport passengers after each mover step, which closes the managed/runtime elevator path for transport gameobjects.
  - `WoWSharpObjectManager` now applies movement blocks to gameobjects instead of only stamping raw coordinates, preserving orientation and facing modes for elevator movers.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectMonsterMove" -v n` -> `3 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1297 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/Models/WoWGameObject.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Objects.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content docs/physics/wow_exe_decompilation.md | Select-Object -Skip 320 -First 140`
