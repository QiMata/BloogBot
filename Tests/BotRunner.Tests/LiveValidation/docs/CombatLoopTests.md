# CombatLoopTests

Live melee-combat baseline for the dedicated `COMBATTEST` account.

## Bot Execution Mode

**CombatTest-Only** — Dedicated COMBATTEST account. No FG observation or parity comparison. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite currently validates the raw melee entry point through:
- `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`

## Test Methods

### Combat_AutoAttacksMob_DealsDamageInMeleeRange

**Bot:** `COMBATTEST` only

**Why COMBATTEST?**
- account-level GM access for setup commands
- never receives `.gm on`
- avoids faction-template corruption during hostile mob tests

## Test Flow

1. Ensure the bot is alive.
2. Learn one-hand maces, set mace skill, add `Worn Mace`, and equip it.
3. Teleport near the Valley of Trials combat area.
4. Find a living nearby mob from the allowed entries:
   - `3098` Mottled Boar
   - `3108` Vile Familiar
   - `3124` Scorpid Worker
5. Assert the GM player flag is still clear.
6. Dispatch `StartMeleeAttack` from the bot's current nearby position.
7. Poll snapshots until:
   - first damage is observed
   - the mob dies
   - or the target evades / times out

## Metrics

The live assertions and diagnostics record:
- target GUID, entry, health, and starting distance
- GM flag / faction-template sanity before combat
- first-hit confirmation
- HP progression over time
- distance-to-target during the combat window
- target-clear / evade behavior

## Overhaul Notes

- No `.npc add temp` fallback remains.
- The test no longer teleports directly onto the target.
- This is still a transitional live combat test; the longer-term replacement remains the planned class-based `CombatClassTests`.
