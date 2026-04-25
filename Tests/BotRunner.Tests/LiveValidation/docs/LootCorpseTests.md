# LootCorpseTests

Validates the kill-to-loot flow using natural BotRunner melee combat and corpse
loot dispatch.

## Shodan Shape

- Uses `Services/WoWStateManager/Settings/Configs/Loot.config.json`.
- `LOOTBG1` is the only BotRunner action target for the live proof.
- `LOOTFG1` launches idle for topology parity.
- SHODAN is director-only and owns setup through fixture helpers.

## Test Method

### Loot_KillAndLootMob_InventoryChanges

Flow:

1. `StageBotRunnerLoadoutAsync(...)` revives/repairs the BG target and clears bags.
2. `StageBotRunnerAtDurotarMobAreaAsync(...)` stages the BG target near low-level Durotar mobs.
3. The test finds a living boar/scorpid/familiar in snapshot data.
4. The BG target receives `ActionType.StartMeleeAttack` and the test waits for the mob to die or disappear from snapshots.
5. The BG target receives `ActionType.StopAttack`, then `ActionType.LootCorpse`.
6. The test asserts the loot dispatch succeeds and logs whether bag contents increased.

No-loot corpses remain non-fatal because the dispatch path is the behavior under
validation.

## Validation

- Direct GM/setup grep over `LootCorpseTests.cs` -> no matches.
- Deterministic safety bundle -> passed `33/33`.
- Dispatch readiness coverage -> passed `60/60`.
- `loot_corpse_shodan.trx` -> passed `1/1`.
