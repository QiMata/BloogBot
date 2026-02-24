using GameData.Core.Enums;
using Serilog;
using System;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to use an item, either on the bot or a target.
        /// </summary>
        /// <param name="fromBag">The bag the item is in.</param>
        /// <param name="fromSlot">The slot the item is in.</param>
        /// <param name="targetGuid">The GUID of the target on which to use the item (optional).</param>
        /// <returns>IBehaviourTreeNode that manages using the item.</returns>
        private IBehaviourTreeNode BuildUseItemSequence(int fromBag, int fromSlot, ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Use Item Sequence")
                // Ensure the bot has the item available to use
                .Condition("Has Item", time => _objectManager.GetContainedItem(fromBag, fromSlot) != null)

                // Use the item on the target (or self if target is null)
                .Do("Use Item", time =>
                {
                    _objectManager.UseItem(fromBag, fromSlot, targetGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        private IBehaviourTreeNode BuildUseItemByIdSequence(int itemId)
        {
            var diagPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "botrunner_useitem_diag.txt");
            return new BehaviourTreeBuilder()
                .Sequence("Use Item By ID")
                    .Do("Find and Use Item", time =>
                    {
                        try { System.IO.File.AppendAllText(diagPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] BuildUseItemByIdSequence: looking for itemId={itemId}\n"); } catch { }

                        // Search all bag slots for the item by ID
                        for (int bag = 0; bag <= 4; bag++)
                        {
                            int maxSlot = bag == 0 ? 16 : 36;
                            for (int slot = 0; slot < maxSlot; slot++)
                            {
                                var contained = _objectManager.GetContainedItem(bag, slot);
                                if (contained != null && contained.ItemId == (uint)itemId)
                                {
                                    Log.Information("[BOT RUNNER] Found item {ItemId} at bag={Bag}, slot={Slot}. Using.", itemId, bag, slot);
                                    try { System.IO.File.AppendAllText(diagPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] FOUND item {itemId} at bag={bag}, slot={slot}. Calling UseItem.\n"); } catch { }
                                    _objectManager.UseItem(bag, slot, 0);
                                    return BehaviourTreeStatus.Success;
                                }
                            }
                        }

                        // Fallback: brute-force use all backpack slots (server ignores invalid)
                        Log.Warning("[BOT RUNNER] Item {ItemId} not found in tracked inventory. Trying brute-force use for all backpack slots.", itemId);
                        try { System.IO.File.AppendAllText(diagPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] BRUTE-FORCE: item {itemId} not found, trying all 16 backpack slots\n"); } catch { }
                        for (int slot = 0; slot < 16; slot++)
                            _objectManager.UseItem(0, slot, 0);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        /// <summary>
        /// Sequence to move an item from one bag and slot to another bag and slot.
        /// </summary>
        /// <param name="fromBag">The source bag ID.</param>
        /// <param name="fromSlot">The source slot ID.</param>
        /// <param name="toBag">The destination bag ID.</param>
        /// <param name="toSlot">The destination slot ID.</param>
        /// <returns>IBehaviourTreeNode that manages moving the item.</returns>
        private IBehaviourTreeNode BuildMoveItemSequence(int fromBag, int fromSlot, int quantity, int toBag, int toSlot) => new BehaviourTreeBuilder()
            .Sequence("Move Item Sequence")
                // Ensure the bot has the item in the source slot
                .Condition("Has Item to Move", time => _objectManager.GetContainedItem(fromBag, fromSlot).Quantity >= quantity)

                // Move the item to the destination slot
                .Do("Move Item", time =>
                {
                    _objectManager.PickupContainedItem(fromBag, fromSlot, quantity);
                    _objectManager.PlaceItemInContainer(toBag, toSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to destroy an item from the inventory.
        /// </summary>
        /// <param name="itemId">The ID of the item to destroy.</param>
        /// <param name="quantity">The quantity of the item to destroy.</param>
        /// <returns>IBehaviourTreeNode that manages destroying the item.</returns>
        private IBehaviourTreeNode BuildDestroyItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
            .Sequence("Destroy Item Sequence")
                // Send CMSG_DESTROYITEM unconditionally — server safely ignores empty slots.
                // Skipping the GetContainedItem condition avoids false negatives when the
                // ObjectManager hasn't fully synced inventory (e.g., after bulk DestroyItem).
                .Do("Destroy Item", time =>
                {
                    Log.Information("[BOT RUNNER] DestroyItem: bag={Bag}, slot={Slot}, qty={Qty}", bagId, slotId, quantity);
                    _objectManager.DestroyItemInContainer(bagId, slotId, quantity);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to equip an item from a bag.
        /// </summary>
        /// <param name="bag">The bag where the item is located.</param>
        /// <param name="slot">The slot in the bag where the item is located.</param>
        /// <returns>IBehaviourTreeNode that manages equipping the item.</returns>
        private IBehaviourTreeNode BuildEquipItemByIdSequence(int itemId)
        {
            var allItems = _objectManager.GetContainedItems().ToList();
            var allObjects = _objectManager.Objects.ToList();
            var objectsByType = allObjects.GroupBy(o => o.ObjectType)
                .Select(g => $"{g.Key}={g.Count()}")
                .ToList();
            Log.Information("[BOT RUNNER] BuildEquipItemByIdSequence: itemId={ItemId}, containedItems={Count}, itemIds=[{Items}], totalObjects={Total}, byType=[{Types}]",
                itemId, allItems.Count, string.Join(",", allItems.Select(i => i.ItemId)),
                allObjects.Count, string.Join(",", objectsByType));
            return new BehaviourTreeBuilder()
                .Sequence("Equip Item By ID")
                    .Do("Find and Equip Item", time =>
                    {
                        // Fast path: find item by ID in tracked inventory
                        foreach (var item in _objectManager.GetContainedItems())
                        {
                            if (item.ItemId == (uint)itemId)
                            {
                                for (int bag = 0; bag <= 4; bag++)
                                {
                                    int maxSlot = bag == 0 ? 16 : 36;
                                    for (int slot = 0; slot < maxSlot; slot++)
                                    {
                                        var contained = _objectManager.GetContainedItem(bag, slot);
                                        if (contained != null && contained.ItemId == (uint)itemId)
                                        {
                                            Log.Information("[BOT RUNNER] Found item {ItemId} at bag={Bag}, slot={Slot}. Equipping.", itemId, bag, slot);
                                            _objectManager.EquipItem(bag, slot);
                                            return BehaviourTreeStatus.Success;
                                        }
                                    }
                                }
                            }
                        }

                        // Fallback: item not tracked in ObjectManager (e.g., added via GM command).
                        // Send CMSG_AUTOEQUIP_ITEM for all 16 backpack slots. The server will
                        // equip valid items and ignore empty/non-equippable slots.
                        Log.Warning("[BOT RUNNER] Item {ItemId} not found in tracked inventory. Trying brute-force equip for all backpack slots.", itemId);
                        for (int slot = 0; slot < 16; slot++)
                            _objectManager.EquipItem(0, slot);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        private IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                // Ensure the bot has the item to equip
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)

                // Equip the item into the designated equipment slot
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to equip an item from a bag into a specific equipment slot.
        /// </summary>
        /// <param name="bag">The bag where the item is located.</param>
        /// <param name="slot">The slot in the bag where the item is located.</param>
        /// <param name="equipSlot">The equipment slot to place the item into.</param>
        /// <returns>IBehaviourTreeNode that manages equipping the item.</returns>
        private IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot, EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                // Ensure the bot has the item to equip
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)

                // Equip the item into the designated equipment slot
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot, equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to unequip an item from a specific equipment slot and place it in the inventory.
        /// </summary>
        /// <param name="equipSlot">The equipment slot from which to unequip the item.</param>
        /// <returns>IBehaviourTreeNode that manages unequipping the item.</returns>
        private IBehaviourTreeNode BuildUnequipItemSequence(EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Unequip Item Sequence")
                // Ensure there is an item in the specified equipment slot
                .Condition("Has Item Equipped", time => _objectManager.GetEquippedItem(equipSlot) != null)

                // Unequip the item from the specified equipment slot
                .Do("Unequip Item", time =>
                {
                    _objectManager.UnequipItem(equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to split a stack of items into two slots in the inventory.
        /// </summary>
        /// <param name="bag">The bag where the stack is located.</param>
        /// <param name="slot">The slot where the stack is located.</param>
        /// <param name="quantity">The quantity to move to a new slot.</param>
        /// <param name="destinationBag">The destination bag for the split stack.</param>
        /// <param name="destinationSlot">The destination slot for the split stack.</param>
        /// <returns>IBehaviourTreeNode that manages splitting the item stack.</returns>
        private IBehaviourTreeNode BuildSplitStackSequence(int bag, int slot, int quantity, int destinationBag, int destinationSlot) => new BehaviourTreeBuilder()
            .Sequence("Split Stack Sequence")
                // Ensure the bot has the stack of items available
                .Condition("Has Item Stack", time => _objectManager.GetContainedItem(bag, slot).Quantity >= quantity)

                // Split the stack into the destination slot
                .Do("Split Stack", time =>
                {
                    _objectManager.SplitStack(bag, slot, quantity, destinationBag, destinationSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to repair a specific item in the inventory.
        /// </summary>
        /// <param name="repairSlot">The slot where the item is located for repair.</param>
        /// <param name="cost">The cost in copper to repair the item.</param>
        /// <returns>IBehaviourTreeNode that manages repairing the item.</returns>
        private IBehaviourTreeNode BuildRepairItemSequence(int repairSlot) => new BehaviourTreeBuilder()
            .Sequence("Repair Item Sequence")
                // Ensure the bot has enough money to repair the item
                .Condition("Can Afford Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.RepairCost((EquipSlot)repairSlot))

                // Repair the item in the specified slot
                .Do("Repair Item", time =>
                {
                    _objectManager.MerchantFrame.RepairByEquipSlot((EquipSlot)repairSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to repair all damaged items in the inventory.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages repairing all items.</returns>
        private IBehaviourTreeNode RepairAllItemsSequence => new BehaviourTreeBuilder()
            .Sequence("Repair All Items Sequence")
                // Ensure the bot has enough money to repair all items
                .Condition("Can Afford Full Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.TotalRepairCost)

                // Repair all damaged items
                .Do("Repair All Items", time =>
                {
                    _objectManager.MerchantFrame.RepairAll();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to dismiss a currently active buff.
        /// </summary>
        /// <param name="buffSlot">The slot or index of the buff to dismiss.</param>
        /// <returns>IBehaviourTreeNode that manages dismissing the buff.</returns>
        private IBehaviourTreeNode BuildDismissBuffSequence(string buff) => new BehaviourTreeBuilder()
            .Sequence("Dismiss Buff Sequence")
                // Ensure the bot has the buff in the specified slot
                .Condition("Has Buff", time => _objectManager.Player.HasBuff(buff))

                // Dismiss the buff
                .Do("Dismiss Buff", time =>
                {
                    _objectManager.Player.DismissBuff(buff);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to craft an item using a specific craft recipe or slot.
        /// </summary>
        /// <param name="craftSlotId">The ID of the crafting recipe or slot to use.</param>
        /// <returns>IBehaviourTreeNode that manages crafting the item.</returns>
        private IBehaviourTreeNode BuildCraftSequence(int craftSlotId) => new BehaviourTreeBuilder()
            .Sequence("Craft Sequence")
                // Ensure the bot can craft the item
                .Condition("Can Craft Item", time => _objectManager.CraftFrame.HasMaterialsNeeded(craftSlotId))

                // Perform the crafting action
                .Do("Craft Item", time =>
                {
                    _objectManager.CraftFrame.Craft(craftSlotId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
