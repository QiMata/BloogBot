# CraftingProfessionTests

Live BG crafting baseline for Linen Bandage production.

## Bot Execution Mode

**BG-Only** — FG excluded due to legacy Lua/UI crafting dependency. No FG observation. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite currently validates the BackgroundBotRunner spell-cast path through:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Combat.cs`

## Test Methods

### FirstAid_LearnAndCraft_ProducesLinenBandage

**Bot:** `TESTBOT2` only

## Test Flow

1. Reset the bot to the fixture clean slate and clear bags for deterministic inventory state.
2. Learn `3273` (First Aid Apprentice) and `3275` (Linen Bandage recipe).
3. Add exactly one `2589` Linen Cloth and assert the bag state is `cloth=1`, `bandage=0`.
4. Dispatch `ActionType.CastSpell` with spell `3275`.
5. Poll snapshots until bag state becomes `cloth=0`, `bandage=1`.

## Metrics

The live assertions record:
- spell-list confirmation for apprentice + recipe
- pre-craft cloth slot count
- pre-craft bandage slot count
- post-craft cloth slot count
- post-craft bandage slot count
- final bag item count
- craft latency in milliseconds

`SpellList` confirmation is treated as diagnostic evidence, not the primary pass/fail gate, because BG can craft successfully even when the learned-spell snapshot has not caught up yet.

## Overhaul Notes

- FG parity is intentionally removed from this suite.
- This is still an action-driven baseline; the longer-term replacement remains the planned `CraftItemTask` coverage.
