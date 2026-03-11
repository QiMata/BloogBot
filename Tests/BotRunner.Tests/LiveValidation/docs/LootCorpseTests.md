# LootCorpseTests

Tests killing a mob and looting its corpse — verifies inventory changes after loot.

## Test Methods (1)

### Loot_KillAndLootMob_InventoryChanges

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Clear inventory | `BotClearInventoryAsync()`. Record baseline bag count. |
| 2 | Teleport to mob area | Valley of Trials boar area (Map 1, -620, -4385, 44). `WaitForTeleportSettledAsync()`. |
| 3 | Find living boar | NearbyUnits: name contains "Boar" or Entry==3098, Health > 0, not claimed by other bot. If none: `.respawn`, retry. |
| 4 | Engage | Teleport near boar (boarX+2, boarY, boarZ+3). `.targetself`. **Dispatch `ActionType.StartMeleeAttack`** with `LongParam = boarGuid`. Wait 1.5s. |
| 5 | Weaken mob | `.damage 500` via GM chat. Wait 500ms. |
| 6 | Wait for death | Poll 20s for boar.Health == 0 |
| 7 | Stop attack | **Dispatch `ActionType.StopAttack`** |
| 8 | Loot corpse | **Dispatch `ActionType.LootCorpse`** with `LongParam = boarGuid`. Assert Success. |
| 9 | Verify loot | Poll 10s for `BagContents.Count > baselineBagCount` |

**StateManager/BotRunner Action Flow:**

- **StartMeleeAttack:** `BuildStartMeleeAttackSequence(boarGuid)` → target + CMSG_ATTACK_SWING
- **StopAttack:** `StopAttackSequence` → CMSG_ATTACK_STOP
- **LootCorpse:** inline `Do()` → `_objectManager.LootTargetAsync(boarGuid)` → CMSG_LOOT packet. Server responds with SMSG_LOOT_RESPONSE containing loot table. Auto-loot processes each item → SMSG_ITEM_PUSH_RESULT updates BagContents.

**Key IDs:**
- Mob 3098 = Mottled Boar (Valley of Trials)

**GM Commands:** `.targetself`, `.respawn`, `.damage 500` (weaken for quick kill).

**Assertions:** Loot dispatch succeeds. BagContents count increases after looting.

**Note:** `.damage 500` is used to speed up the kill — the test's focus is loot mechanics, not combat duration.
