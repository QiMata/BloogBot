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
  - Added parser coverage for the real Vanilla `SMSG_MONSTER_MOVE` wire shapes, including linear destination-plus-packed-offset payloads and cyclic smooth (`Flying|Cyclic|EnterCycle`) payload normalization.
  - Added `ActiveSpline` coverage for cyclic Catmull-Rom wrap behavior so the first and closing segments both use wrapped control points instead of endpoint clamping.
  - Updated the shared object-manager monster-move payload helper so future runtime tests emit the real packet layout instead of the earlier simplified point list.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> `succeeded`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnFirstSegment|FullyQualifiedName~ActiveSplineStepTests.Step_CyclicFlyingSpline_UsesWrappedNeighborOnClosingSegment" -v n` -> `5 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1340 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs | Select-Object -First 260`
