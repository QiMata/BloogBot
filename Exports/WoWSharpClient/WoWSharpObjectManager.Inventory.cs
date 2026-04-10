using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WoWSharpClient
{
    /// <summary>
    /// Partial class that delegates item use, equip/unequip, loot, vendor, banking,
    /// auction, and trade operations to <see cref="InventoryManager"/>.
    /// All public IObjectManager method signatures are preserved; implementation lives in the extracted class.
    /// </summary>
    public partial class WoWSharpObjectManager
    {
        // ---- Item Use ----

        public void UseItem(int bagId, int slotId, ulong targetGuid = 0) => _inventory.UseItem(bagId, slotId, targetGuid);

        // ---- GUID Lookups ----

        public ulong GetBackpackItemGuid(int parSlot) => _inventory.GetBackpackItemGuid(parSlot);
        public ulong GetEquippedItemGuid(EquipSlot slot) => _inventory.GetEquippedItemGuid(slot);
        public IWoWItem GetEquippedItem(EquipSlot slot) => _inventory.GetEquippedItem(slot);
        public IWoWItem GetContainedItem(int bagSlot, int slotId) => _inventory.GetContainedItem(bagSlot, slotId);
        public IEnumerable<IWoWItem> GetEquippedItems() => _inventory.GetEquippedItems();
        public IEnumerable<IWoWItem> GetContainedItems() => _inventory.GetContainedItems();
        public int CountFreeSlots(bool countSpecialSlots = false) => _inventory.CountFreeSlots(countSpecialSlots);
        public uint GetItemCount(uint itemId) => _inventory.GetItemCount(itemId);
        public uint GetBagGuid(EquipSlot equipSlot) => _inventory.GetBagGuid(equipSlot);

        // ---- Cursor / Move / Destroy ----

        public void PickupContainedItem(int bagSlot, int slotId, int quantity) => _inventory.PickupContainedItem(bagSlot, slotId, quantity);
        public void PlaceItemInContainer(int bagSlot, int slotId) => _inventory.PlaceItemInContainer(bagSlot, slotId);
        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1) => _inventory.DestroyItemInContainer(bagSlot, slotId, quantity);
        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot) => _inventory.SplitStack(bag, slot, quantity, destinationBag, destinationSlot);

        // ---- Equip / Unequip ----

        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null) => _inventory.EquipItem(bagSlot, slotId, equipSlot);
        public void UnequipItem(EquipSlot slot) => _inventory.UnequipItem(slot);

        // ---- Game Object Interaction ----

        public void InteractWithGameObject(ulong guid) => _inventory.InteractWithGameObject(guid);

        // ---- Loot ----

        public void AutoStoreLootItem(byte slot) => _inventory.AutoStoreLootItem(slot);
        public void ReleaseLoot(ulong lootGuid) => _inventory.ReleaseLoot(lootGuid);
        public async Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default) => await _inventory.LootTargetAsync(targetGuid, ct);

        // ---- Vendor ----

        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default) => await _inventory.QuickVendorVisitAsync(vendorGuid, itemsToBuy, ct);
        public async Task BuyItemFromVendorAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken ct = default) => await _inventory.BuyItemFromVendorAsync(vendorGuid, itemId, quantity, ct);
        public async Task SellItemToVendorAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken ct = default) => await _inventory.SellItemToVendorAsync(vendorGuid, bagId, slotId, quantity, ct);
        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken ct = default) => await _inventory.RepairAllItemsAsync(vendorGuid, ct);

        // ---- Mail ----

        public async Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default) => await _inventory.CollectAllMailAsync(mailboxGuid, ct);

        // ---- Banking ----

        public async Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default) => await _inventory.DepositExcessItemsAsync(bankerGuid, ct);

        // ---- Auction ----

        public async Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default) => await _inventory.PostAuctionItemsAsync(auctioneerGuid, ct);

        // ---- Trade ----

        public async Task InitiateTradeAsync(ulong playerGuid, CancellationToken ct = default) => await _inventory.InitiateTradeAsync(playerGuid, ct);
        public async Task SetTradeGoldAsync(uint copper, CancellationToken ct = default) => await _inventory.SetTradeGoldAsync(copper, ct);
        public async Task SetTradeItemAsync(byte tradeSlot, byte bagId, byte slotId, CancellationToken ct = default) => await _inventory.SetTradeItemAsync(tradeSlot, bagId, slotId, ct);
        public async Task AcceptTradeAsync(CancellationToken ct = default) => await _inventory.AcceptTradeAsync(ct);
        public async Task CancelTradeAsync(CancellationToken ct = default) => await _inventory.CancelTradeAsync(ct);
    }
}
