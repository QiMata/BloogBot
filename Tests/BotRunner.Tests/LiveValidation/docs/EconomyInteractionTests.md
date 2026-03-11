# EconomyInteractionTests

Tests NPC and game object interactions for economy services: banker, auctioneer, mailbox.

## Test Methods (3)

### 1. Bank_OpenAndDeposit

**Bots:** BG + FG

**Test Flow:**
1. Teleport both bots to Orgrimmar bank (1627.32, -4376.07, 11.81)
2. Wait for arrival within 40y
3. `RefreshSnapshotsAsync()`
4. Search NearbyUnits for NPC with `UNIT_NPC_FLAG_BANKER` flag
5. **Dispatch `ActionType.InteractWith`** with `LongParam = bankerGuid`
6. Assert: dispatch returns `ResponseResult.Success`

**StateManager/BotRunner Action Flow:**
- **InteractWith (NPC):** `BuildInteractWithSequence(bankerGuid)` → detects unit (not GameObject) → `_objectManager.InteractWithNpcAsync(guid)` → FG: right-click via memory call at 0x60BEA0 / BG: CMSG_GOSSIP_HELLO packet

---

### 2. AuctionHouse_OpenAndList

**Bots:** BG + FG

**Test Flow:**
1. Teleport to Orgrimmar AH (1687.26, -4464.71, 20.15)
2. Wait for arrival within 40y
3. Search NearbyUnits for NPC with `UNIT_NPC_FLAG_AUCTIONEER` flag
4. **Dispatch `ActionType.InteractWith`** with `LongParam = auctioneerGuid`
5. Assert: dispatch Success

---

### 3. Mail_OpenMailbox

**Bots:** BG + FG

**Test Flow:**
1. Teleport to Orgrimmar mailbox (1615.58, -4391.60, 10.11)
2. Wait for arrival within 40y
3. Poll NearbyObjects for mailbox game object (5s, 200ms interval)
4. Prioritize by name containing "mail" (case-insensitive), fallback to closest
5. **Dispatch `ActionType.InteractWith`** with `LongParam = mailboxGuid`
6. Assert: dispatch Success

**StateManager/BotRunner Action Flow (GameObject path):**
- **InteractWith (GameObject):** `BuildInteractWithSequence(mailboxGuid)` → detects GameObject → `_objectManager.InteractWithGameObject(guid)` → CMSG_GAMEOBJ_USE packet

---

## Key Coordinates

| Location | X | Y | Z |
|----------|---|---|---|
| Orgrimmar Bank | 1627.32 | -4376.07 | 11.81 |
| Orgrimmar AH | 1687.26 | -4464.71 | 20.15 |
| Orgrimmar Mailbox | 1615.58 | -4391.60 | 10.11 |

**GM Commands:** None — tests only use teleport for positioning.

**Assertions:** NPC/object found with correct flags. InteractWith dispatches successfully.
