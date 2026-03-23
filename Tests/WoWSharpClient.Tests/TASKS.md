# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add focused coverage for transport mover/path-progress behavior once `P7.5` runtime elevator support lands.
2. Add a recorded directional remote-unit packet fixture so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
3. Add movement-opcode sweep tests as new gaps are discovered in the dispatch-table audit.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added monster-move parser coverage proving spline start time is preserved from `SMSG_MONSTER_MOVE`.
  - Added deterministic spline runtime coverage for mid-spline activation from server time, exact cyclic boundary timing, and `SplineType` facing resolution.
  - Extended clone regression coverage so `MovementBlockUpdate.Clone()` now protects the new spline start-time field too.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MonsterMoveParsingTests|FullyQualifiedName~ActiveSplineStepTests|FullyQualifiedName~SplineFacingTests|FullyQualifiedName~MovementBlockUpdateCloneBugTests" -v n` -> `33 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1294 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/Handlers/MonsterMoveParsingTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/ActiveSplineTests.cs`
  - `Tests/WoWSharpClient.Tests/Movement/MovementBlockUpdateCloneBugTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs | Select-Object -First 260`
