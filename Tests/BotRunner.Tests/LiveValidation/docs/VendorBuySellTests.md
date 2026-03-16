# VendorBuySellTests

Live BG vendor packet baselines for explicit buy and sell behavior.

## Bot Execution Mode

**BG-Only** — FG excluded due to merchant-frame legacy gap. No FG observation. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

This suite currently validates the packet-driven vendor flow through:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Inventory.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/VendorNetworkClientComponent.cs`

## Test Methods

### Vendor_BuyItem_AppearsInInventory

**Bot:** `TESTBOT2` only

**Flow**

1. Reset to fixture clean slate and clear bags.
2. Teleport to Grimtak in Razor Hill and assert a vendor NPC is visible nearby.
3. Ensure baseline money.
4. Record item `159` and coinage counts.
5. Dispatch `ActionType.BuyItem`.
6. Poll until the item count increases and coinage decreases.

### Vendor_SellItem_RemovedFromInventory

**Bot:** `TESTBOT2` only

**Flow**

1. Reset to fixture clean slate and clear bags.
2. Teleport to Grimtak and assert vendor visibility + interaction distance.
3. Add exactly one `2589` Linen Cloth.
4. Resolve the bag/slot from snapshot inventory.
5. Dispatch `ActionType.SellItem`.
6. Poll until the item disappears and coinage increases.

## Metrics

The live assertions record:
- vendor visibility and distance from player
- item count before and after each action
- coinage before and after each action
- inventory update latency in milliseconds

## Overhaul Notes

- FG parity is intentionally removed from this suite.
- This remains an explicit packet baseline; the later `VendorVisitTask` merge into the NPC interaction suite is still pending.
