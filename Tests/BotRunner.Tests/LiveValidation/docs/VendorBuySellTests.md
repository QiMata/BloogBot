# VendorBuySellTests

Tests vendor buy and sell via ActionType dispatches — verifies inventory changes.

## Test Methods (2)

### 1. Vendor_BuyItem_AppearsInInventory

**Bots:** BG + FG

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 0 | Clear bags | `BotClearInventoryAsync()` |
| 1 | Teleport to Grimtak | Razor Hill vendor (305.722, -4665.87, 16.527+1). Wait for settlement. |
| 2 | Find vendor | `FindNpcByFlagAsync(UNIT_NPC_FLAG_VENDOR)` — retry 3x. Assert GUID != 0. |
| 3 | Ensure money | `EnsureMoneyAsync()` — if < 1000 copper: `.modify money 1000` |
| 4 | Record baseline | Count item 159 (Refreshing Spring Water) in bags (expect 0) |
| 5 | Buy item | **Dispatch `ActionType.BuyItem`** with `LongParam = vendorGuid`, `IntParam = 159`, qty=1 |
| 6 | Verify | Poll 12s for item count increase via `WaitForItemCountChangeAsync()` |
| 7 | Cleanup | `DestroyItemByIdAsync()` — find item slot, dispatch `ActionType.DestroyItem` |

---

### 2. Vendor_SellItem_RemovedFromInventory

**Bots:** BG + FG

**Test Flow:**

| Step | Action | Details |
|------|--------|---------|
| 1 | Teleport to Grimtak | Same coordinates as buy test |
| 2 | Find vendor | Same NPC flag search |
| 3 | Add sell item | `.additem 2589 1` (Linen Cloth). Poll 5s for presence. |
| 4 | Find bag/slot | `FindItemBagSlot()` — BagContents key=absolute slot (23-38 for backpack) |
| 5 | Sell item | **Dispatch `ActionType.SellItem`** with `LongParam = vendorGuid`, bagId=0xFF, slotId=absolute slot, qty=1 |
| 6 | Verify | Poll 8s for item absent via `WaitForItemAbsentAsync()` |

**StateManager/BotRunner Action Flow:**

**BuyItem dispatch chain:**
1. ActionMessage with `ActionType.BuyItem`, `LongParam=vendorGuid`, `IntParam=159`
2. `BuildBuyItemSequence()` OR `_objectManager.BuyItemFromVendorAsync(vendorGuid, 159, 1)`
3. **FG path:** Legacy MerchantFrame interaction via Lua
4. **BG path:** CMSG_BUY_ITEM packet with vendorGuid + itemId + quantity
5. Server validates: money >= cost, bag space available
6. SMSG_BUY_ITEM response → SMSG_ITEM_PUSH_RESULT → BagContents updated

**SellItem dispatch chain:**
1. `_objectManager.SellItemToVendorAsync(vendorGuid, 0xFF, slotId, 1)`
2. **FG path:** Legacy MerchantFrame via Lua
3. **BG path:** CMSG_SELL_ITEM packet with vendorGuid + bagId(0xFF) + slotId + quantity
4. Server removes item, adds copper → SMSG_DESTROY_OBJECT + gold update

**DestroyItem dispatch chain:**
1. `BuildDestroyItemSequence(bagId, slotId, count)` → CMSG_DESTROYITEM packet

**FG vs BG Critical Difference:** FG uses legacy MerchantFrame (Lua GUI interaction). BG uses packet-based vendor API. Both produce same server result.

**Key IDs:**
- Item 159 = Refreshing Spring Water (buy test, costs 25 copper)
- Item 2589 = Linen Cloth (sell test)
- NPC: Grimtak (Razor Hill vendor, entry 3881)

**Inventory Slot Mapping:**
- BagContents key = absolute inventory slot (backpack 23-38)
- CMSG_SELL_ITEM: bagId=0xFF (INVENTORY_SLOT_BAG_0), slotId=key from BagContents

**Vendor Coordinates:** (305.722, -4665.87, 16.527)

**GM Commands:** `.modify money 1000`, `.additem 2589 1`.

**Assertions:** Item appears in bags after buy. Item absent from bags after sell. Money sufficient for purchase.
