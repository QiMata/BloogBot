# WoWSharpClient.Tests Tasks

## Scope
- Project: `Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj`
- Owns deterministic coverage for BG packet parsing, object-manager state application, movement modeling, and protocol parity regressions.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Add recorded directional remote-unit packet fixtures so extrapolation accuracy can be measured against real movement data instead of only deterministic math.
2. Add focused knockback trajectory coverage against parsed movement impulses.
3. Add movement-opcode sweep tests as new gaps are discovered in the dispatch-table audit.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - Added direct runtime coverage for moving transport gameobjects carrying passengers, proving elevator-style `SMSG_MONSTER_MOVE` updates move the transport and keep rider world coordinates synchronized.
  - The direct monster-move runtime coverage now spans world movers, transport-local movers, and moving transports themselves.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~DirectMonsterMove" -v n` -> `3 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build -v n` -> `1297 passed`, `1 skipped`
- Files changed:
  - `Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`
- Next command:
  - `Get-Content Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs | Select-Object -First 220`
