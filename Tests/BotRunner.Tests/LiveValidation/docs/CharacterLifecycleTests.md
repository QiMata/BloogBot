# CharacterLifecycleTests

Tests inventory operations and the death/revive lifecycle.

## Test Methods (4)

### 1. Equipment_AddItemToInventory

**Bots:** BG + FG

**Test Flow:**
1. `EnsureStrictAliveAsync()`
2. Baseline: count item 1 (Linen Cloth) in BagContents
3. If preexisting or bags near-full, `BotClearInventoryAsync()`
4. `.additem 1 1` via `SendGmChatCommandTrackedAsync()` (1200ms delay)
5. Assert command success (no "FAULT:", no "no such command")
6. `WaitForBagItemPresenceAsync()` — poll 10s, 400ms interval
7. Assert: item count changed

**StateManager/BotRunner Role:** `.additem` is a GM chat command — BotRunnerService processes `SendChat` action. Server adds item, sends SMSG_ITEM_PUSH_RESULT. BG processes packet → updates ObjectManager inventory. FG reads memory directly.

**Key IDs:** Item 1 = Linen Cloth

---

### 2. Consumable_AddPotionToInventory

**Bots:** BG + FG

Same flow as Equipment_AddItemToInventory but with item 118 (Minor Healing Potion), quantity 5.

---

### 3. Death_KillAndRevive

**Bots:** BG + FG

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure alive | `EnsureStrictAliveAsync()` — verify baseline alive state |
| 2 | Kill | `InduceDeathForTestAsync()` — tries `.die`, `.kill`, `.damage 5000` via chat; fallback SOAP (15s timeout) |
| 3 | Wait for death | Poll snapshot for ghost flag `(PlayerFlags & 0x10)` OR `Health == 0` OR `StandState == 7` (DEAD) |
| 4 | Revive | `RevivePlayerAsync()` via SOAP `.revive` |
| 5 | Wait for alive | Poll `IsStrictAlive()` up to 20s: health > 0 AND no ghost flag AND standState != dead |

**StateManager/BotRunner Role:** Death/revive are server-side state changes. BotRunnerService's `PushDeathRecoveryIfNeeded()` would normally push ReleaseCorpseTask, but env var `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1` prevents this during tests. Snapshot captures state transitions passively.

**Player Flags:** 0x10 = PLAYER_FLAGS_GHOST. StandState 7 = UNIT_STAND_STATE_DEAD.

**Assertions:** Alive → dead/ghost → alive transition confirmed in snapshots.

---

### 4. CharacterCreation_InfoAvailable

**Bots:** BG + FG

**Test Flow:**
1. `RefreshSnapshotsAsync()`
2. Assert: GUID != 0, Position != null, Level > 0, CharacterName/AccountName populated

**StateManager/BotRunner Role:** Passive snapshot observation.

---

## Cleanup/Teardown

Death_KillAndRevive revives via SOAP after assertion. No persistent state changes.
