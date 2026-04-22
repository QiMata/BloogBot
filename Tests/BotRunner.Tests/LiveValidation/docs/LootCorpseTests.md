# LootCorpseTests

Validates kill-to-loot flow using natural auto-attack combat.

## Bot Execution Mode

**CombatTest-Only** — Uses dedicated `COMBATTEST` account with account-level GM access only. No FG observation or parity comparison. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Test Method

### Loot_KillAndLootMob_InventoryChanges

**Bot:** COMBATTEST only

**Flow:**
1. `EnsureCleanSlateAsync()`
2. Clear bags and record baseline item count.
3. Teleport to the Valley of Trials boar area.
4. Wait for a living boar in snapshot data. If none appears, the test fails (not skips).
5. Dispatch `StartMeleeAttack` — natural auto-attack combat with 45s timeout.
6. Dispatch `LootCorpse`.
7. Assert the dispatch succeeds and log whether bag contents increased.

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
