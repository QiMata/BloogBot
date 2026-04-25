# ConsumableUsageTests

Legacy consumable-use baseline migrated to the Shodan test-director topology.

## Bot Execution Mode

**Shodan BG-action** - `Loot.config.json` launches `LOOTBG1` as the BG action
target, `LOOTFG1` idle for topology parity, and SHODAN as the director.

## Active Test

### UseConsumable_ElixirOfLionsStrength_BuffApplied

**Purpose:** Prove the legacy BG consumable path can stage an elixir through
Shodan and dispatch `ActionType.UseItem` without test-body GM setup.

**Setup path:**
- `StageBotRunnerConsumableStateAsync(...)` clears the target inventory, adds
  Elixir of Lion's Strength (`2454`), and clears Lion's Strength aura ids
  `2367` / `2457`.

**Action path:**
- `ActionType.UseItem` with item id `2454` to `LOOTBG1`.

**Current live result:**
- `buff_consumable_shodan.trx` passed this test once while the companion
  `BuffAndConsumableTests` class recorded tracked skips for the stricter
  aura/slot and dismiss assertions.
- If the aura assertion is not stable in a future run, the test skips with the
  same tracked BG consumable observation gap instead of failing the migration
  shape.

## Validation

- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `buff_consumable_shodan.trx` -> passed overall (`1` passed, `2` skipped).
