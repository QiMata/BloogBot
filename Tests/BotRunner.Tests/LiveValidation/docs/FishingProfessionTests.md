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

The pass condition no longer depends only on RNG skill-up. A run now succeeds if BG either:
- catches loot and the bag contents change
- or gains fishing skill during the cast/catch cycle

Latest broad-suite boundary:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `30 passed, 2 failed, 3 skipped`
- one of the two failures is `Fishing_CatchFish_SkillIncreases`
- in that full-suite run, BG exhausted all four shoreline positions without a catch and finished at skill `1 -> 1`
- the final logged cast showed `SMSG_SPELL_START`, `SMSG_SPELL_GO`, `MSG_CHANNEL_START`, then immediate channel loss with no sustained bobber/catch signal

That means `BRT-OVR-004` is now both a task-ownership follow-up and a broad-suite stability issue.

## Overhaul Notes

- This suite still needs to become the planned `FishingTaskTests` coverage so the behavior is driven through a dedicated `FishingTask`.
- The next fishing-focused pass should compare the isolated pass against the broad-suite failure to explain why BG loses channel/bobber state after earlier live tests.
- FG/BG packet-timing capture around bobber spawn, custom anim, and auto-interact timing is still the required reference path instead of re-debugging the old cast gate.
