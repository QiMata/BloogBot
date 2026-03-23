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
  - Direct `SMSG_MONSTER_MOVE` and `SMSG_MONSTER_MOVE_TRANSPORT` now route through the managed movement handler, dispatcher, and legacy world-client bridge instead of falling through as unhandled packets.
  - Transport movers now advance splines in transport-local space, then resync world position/facing from the owning transport after each spline step.
  - `WoWSharpObjectManager` now guarantees a valid world-time tracker before runtime spline activation, which fixes direct monster-move processing before the normal game loop/login-verify path spins up.
  - Test queue draining now waits for pending movement-only updates instead of assuming object-count stability means processing is complete.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectMonsterMove" -v n` -> `2 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1296 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/Client/WorldClient.cs`
  - `Exports/WoWSharpClient/Handlers/MovementHandler.cs`
  - `Exports/WoWSharpClient/Movement/SplineController.cs`
  - `Exports/WoWSharpClient/OpCodeDispatcher.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
  - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Util/UpdateProcessingHelper.cs`
- Next command:
  - `Get-Content Exports/WoWSharpClient/Movement/SplineController.cs | Select-Object -First 260`
