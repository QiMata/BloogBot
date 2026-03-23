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
  - Added direct `SMSG_MONSTER_MOVE` runtime coverage proving remote units activate managed splines and advance in world space without the network pipeline in the loop.
  - Added direct `SMSG_MONSTER_MOVE_TRANSPORT` runtime coverage proving transport-local offsets advance along the spline and resync to correct world coordinates/facing.
  - Fixed the deterministic update-drain helper so movement-only updates are observed reliably instead of being lost behind object-count-only heuristics.
- Validation/tests run:
  - `dotnet build Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore` -> succeeded
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectMonsterMove" -v n` -> `2 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1296 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
  - `Tests/WoWSharpClient.Tests/Util/UpdateProcessingHelper.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs | Select-Object -Skip 160 -First 140`
