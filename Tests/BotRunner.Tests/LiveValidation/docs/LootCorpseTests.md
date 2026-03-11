# LootCorpseTests

Validates kill-to-loot flow without forcing creature respawns.

## Test Method

### Loot_KillAndLootMob_InventoryChanges

**Bots:** BG and FG when FG is actionable.

**Flow:**
1. `EnsureCleanSlateAsync()`
2. Clear bags and record baseline item count.
3. Teleport to the Valley of Trials boar area.
4. Wait for a living boar in snapshot data. If none appears within the wait window, the test skips.
5. Teleport near the boar and start melee attack.
6. Use `.damage 500` only to shorten the kill, since this suite is testing loot not combat.
7. Dispatch `LootCorpse`.
8. Assert the dispatch succeeds and log whether bag contents increased.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/LootCorpseTests.cs`
- Loot action forwarding: `Exports/BotRunner/BotRunnerService.cs`
- Loot handling: `Exports/BotRunner/Tasks/`
- Snapshot inventory view: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`

**Assertions:**
- A living target can be claimed from snapshot data without forced respawn
- `ActionType.LootCorpse` dispatch succeeds
- Inventory change is observed when the corpse actually drops loot
- No-loot corpses are logged as non-fatal because the dispatch path is the primary target
