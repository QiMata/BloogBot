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
- full `LiveValidation` is green again: `33 passed, 0 failed, 2 skipped`

The pass condition no longer depends only on RNG skill-up. A run now succeeds if BG either:
- catches loot and the bag contents change
- or gains fishing skill during the cast/catch cycle

## Overhaul Notes

- This suite still needs to become the planned `FishingTaskTests` coverage so the behavior is driven through a dedicated `FishingTask`.
- The next fishing-focused pass should compare FG and BG packet/timing around bobber spawn, custom anim, and auto-interact timing rather than re-debugging the old cast gate.
