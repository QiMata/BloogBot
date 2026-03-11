# FishingProfessionTests

Live BG-first fishing baseline covering spell sync, shoreline stability, cast entry, and catch detection.

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
- `Exports/BotRunner/Combat/FishingData.cs`
- `Exports/WoWSharpClient/Handlers/SpellHandler.cs`
- `Exports/WoWSharpClient/OpCodeDispatcher.cs`
- `Exports/WoWSharpClient/Client/WorldClient.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`

## Test Methods

### Fishing_CatchFish_SkillIncreases

**Bots:** `TESTBOT2` is the asserted BG fishing bot. `TESTBOT1` is parked as a non-blocking FG reference bot.

## Test Flow

1. Force a fresh fishing spell sync with `.unlearn -> .learn` so BG receives `SMSG_LEARNED_SPELL`, `SMSG_SUPERCEDED_SPELL`, and `SMSG_REMOVED_SPELL`.
2. Set fishing skill to `1/300`, clear gear, add a pole, equip it, and add `Shiny Bauble`.
3. Teleport across the known shoreline positions and reject unstable landings.
4. Set facing toward water at each position.
5. Resolve the castable fishing rank from the current known-spell list and dispatch `ActionType.CastSpell`.
6. Look for:
   - an active channel
   - a bobber game object
   - a bag/loot delta that proves a catch
   - or a later fishing skill increase

## Metrics

The live diagnostics record:
- fishing skill before and after the run
- spell-count, castable fishing rank, and pole proficiency after spell sync
- per-location landing stability and `deltaFromShore`
- whether channel state was entered
- whether a bobber appeared
- whether catch success came from loot delta, skill-up, or both

## Current Status

`2026-03-11` live runs moved the suite past the BG cast gate:
- BG now updates known fishing ranks from `SMSG_SUPERCEDED_SPELL` and `SMSG_REMOVED_SPELL`
- `ResolveCastableFishingSpellId(...)` correctly prefers the highest currently-known fishing rank
- the live run reaches `CastSpellAtLocation()`, channel start, bobber creation, and auto-loot/catch detection
- the focused fishing slice still passes in isolation
- BG fishing setup now starts with `EnsureCleanSlateAsync(...)`, which aligns it with the other live suites and removes prior-test position/death leakage before fishing gear and spell sync are applied

The pass condition no longer depends only on RNG skill-up. A run now succeeds if BG either:
- catches loot and the bag contents change
- or gains fishing skill during the cast/catch cycle

Latest broad-suite boundary:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore` -> succeeded
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `1 passed`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~FishingProfessionTests|FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` still exited nonzero, but the fresh `TestResults/LiveLogs/FishingProfessionTests.log` no longer shows a fishing failure:
  - Ratchet dock cast started a real channel
  - bobber custom anim fired
  - BG sent `CMSG_GAMEOBJ_USE` after `ForceStopImmediate()`
  - MaNGOS returned `SMSG_LOOT_RESPONSE`
  - item `20708` was pushed into bags and the bobber was released

That means the current slice moved the observed broad-suite boundary forward: fishing is no longer the failing symptom in the latest live logs, but `BRT-OVR-004` is still open until the full suite finishes green and the test is converted to task-owned coverage.

## Overhaul Notes

- This suite still needs to become the planned `FishingTaskTests` coverage so the behavior is driven through a dedicated `FishingTask`.
- The next fishing-focused pass should capture the same successful Ratchet packet/timing sequence from FG and compare it to the current BG log rather than re-debugging the old cast gate.
- FG/BG packet-timing capture around bobber spawn, custom anim, and auto-interact timing is still the required reference path instead of re-debugging the old cast gate.
