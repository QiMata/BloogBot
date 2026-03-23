# WoWSharpClient Tasks

## Scope
- Project: `Exports/WoWSharpClient`
- Owns BG-side packet handling, object-model state, movement state application, and protocol parity with WoW 1.12.1.
- Master tracker: `docs/TASKS.md`

## Active Priorities
1. Keep recorded-motion validation in place for remote extrapolation and knockback handling.
2. Support the FG hardening sweep where WoWSharpClient contracts or offsets are shared with injected/runtime code.
3. Keep the movement opcode sweep closed by only adding new bridge/application handlers when a binary-backed non-cheat gap is found.

## Session Handoff
- Last updated: `2026-03-23`
- Pass result: `delta shipped`
- Last delta:
  - `WoWUnitExtrapolationTests` now includes replay-backed coverage from real movement recordings instead of only synthetic vector math.
  - Added a slow-walk Undercity fixture that proves the `<3y/s` WoW.exe jitter guard returns the raw server position even when a nearby unit keeps walking for another half-second.
  - Added a fast-running Blackrock Spire fixture that stays within `0.02y` horizontal drift against observed motion, proving the positive extrapolation path against a real remote-unit trajectory instead of only hand-authored test data.
- Validation/tests run:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --filter "FullyQualifiedName~WoWUnitExtrapolationTests" -v n` -> `8 passed`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -v n` -> `1351 passed`, `1 skipped`
- Files changed:
  - `Exports/WoWSharpClient/TASKS.md`
  - `Tests/WoWSharpClient.Tests/Models/WoWUnitExtrapolationTests.cs`
- Next command:
  - `Get-Content docs/TASKS.md | Select-Object -Skip 283 -First 80`
