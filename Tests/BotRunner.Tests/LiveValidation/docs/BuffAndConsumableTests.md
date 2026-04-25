# BuffAndConsumableTests

Shodan-directed consumable item-use and buff-cancel coverage.

## Bot Execution Mode

**Shodan BG-action / tracked skip** - `Loot.config.json` launches `LOOTBG1`
as the BG action target, `LOOTFG1` idle for topology parity, and SHODAN as the
director. SHODAN performs setup through fixture helpers; only `LOOTBG1`
receives BotRunner actions.

## Active Tests

### UseConsumable_AppliesBuff

**Purpose:** Validate the BG `ActionType.UseItem` dispatch path for Elixir of
Lion's Strength (`2454`) after Shodan-owned inventory and aura staging.

**Setup path:**
- `StageBotRunnerConsumableStateAsync(...)` clears the target inventory, adds
  the elixir, and removes Lion's Strength aura ids `2367` / `2457`.
- The test body issues no direct GM setup commands.

**Action path:**
- `ActionType.UseItem` with item id `2454`.

**Current live result:**
- `buff_consumable_shodan.trx` passes overall, but this richer assertion skips
  when the delivered BG action does not produce a stable Lion's Strength aura
  assertion.
- The action forwarding layer returns `ResponseResult.Success`; the remaining
  gap is the runtime consumable/aura observation path, not the Shodan
  migration shape.

### DismissBuff_RemovesBuff

**Purpose:** Validate `ActionType.DismissBuff` after the target reaches a
buffed state.

**Action path:**
- `ActionType.DismissBuff` with buff name `Lesser Strength`.

**Current live result:**
- Skips with `BB-BUFF-001`: BG currently cannot prove dismissal because
  `WoWSharpClient` does not populate enough `WoWUnit.Buffs` metadata for this
  buff-cancel assertion.
- Cleanup returns through fixture-owned aura removal so later tests start
  cleanly.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `buff_consumable_shodan.trx` -> passed overall (`1` passed, `2` skipped).
