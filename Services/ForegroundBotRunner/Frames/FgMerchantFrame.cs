using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner.Frames;

public sealed class FgMerchantFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult,
    Func<int, int, IWoWItem?> containedItemProvider,
    Func<IEnumerable<IWoWItem>> containedItemsProvider,
    Func<EquipSlot, IWoWItem?> equippedItemProvider,
    Func<ulong> merchantGuidProvider) : IMerchantFrame
{
    private const string MerchantVisibleLua =
        "if MerchantFrame and MerchantFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string MerchantRepairLua =
        "if MerchantFrame and MerchantFrame:IsVisible() and CanMerchantRepair() then {0} = 1 else {0} = 0 end";
    private const string MerchantRepairCostLua =
        "if MerchantFrame and MerchantFrame:IsVisible() then " +
        "local cost, canRepair = GetRepairAllCost(); " +
        "if canRepair then {0} = cost or 0 else {0} = 0 end " +
        "else {0} = 0 end";
    private const string MerchantItemCountLua =
        "if MerchantFrame and MerchantFrame:IsVisible() then {0} = GetMerchantNumItems() or 0 else {0} = 0 end";

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, MerchantVisibleLua);

    public void Close() => luaCall(VendorInteractionHelper.CloseMerchantLua);

    public bool CanRepair => FrameLuaReader.ReadBool(luaCallWithResult, MerchantRepairLua);

    public int TotalRepairCost => FrameLuaReader.ReadInt(luaCallWithResult, MerchantRepairCostLua);

    public bool Ready => IsOpen;

    public IReadOnlyList<MerchantItem> Items
        => Enumerable.Range(1, FrameLuaReader.ReadInt(luaCallWithResult, MerchantItemCountLua))
            .Select(CreatePlaceholderMerchantItem)
            .ToList();

    public void SellItem(int bagId, int slotId, int quantity)
    {
        ulong itemGuid = VendorInteractionHelper.ResolveSellItemGuid(
            (byte)bagId,
            (byte)slotId,
            containedItemsProvider().Select(item => item.Guid).ToList(),
            (resolvedBagId, resolvedSlotId) => containedItemProvider(resolvedBagId, resolvedSlotId)?.Guid ?? 0);

        if (itemGuid == 0)
            return;

        ulong merchantGuid = merchantGuidProvider();
        if (merchantGuid == 0)
            return;

        ThreadSynchronizer.RunOnMainThread(() =>
            Functions.SellItemByGuid(
                VendorInteractionHelper.NormalizeQuantity((uint)Math.Max(1, quantity)),
                merchantGuid,
                itemGuid));
    }

    public void BuyItem(int itemGuid, int itemCount)
        => luaCall($"if MerchantFrame and MerchantFrame:IsVisible() then BuyMerchantItem({Math.Max(1, itemGuid)}, {Math.Max(1, itemCount)}) end");

    public void BuybackItem(int itemGuid, int itemCount)
        => luaCall($"if MerchantFrame and MerchantFrame:IsVisible() then BuybackItem({Math.Max(1, itemGuid)}) end");

    public int RepairCost(EquipSlot equipSlot)
    {
        var item = equippedItemProvider(equipSlot);
        if (item == null || item.MaxDurability == 0 || item.Durability >= item.MaxDurability)
            return 0;

        return TotalRepairCost;
    }

    public void RepairByEquipSlot(EquipSlot parSlot)
    {
        if (RepairCost(parSlot) > 0)
            RepairAll();
    }

    public void RepairAll() => luaCall(VendorInteractionHelper.RepairAllLua);

    public void ItemCallback(int parItemId)
    {
    }

    public bool IsItemAvaible(int parItemId)
        => FrameLuaReader.ReadInt(luaCallWithResult, VendorInteractionHelper.BuildResolveMerchantSlotLua((uint)parItemId)) > 0;

    public void VendorByGuid(ulong guid, uint itemCount = 1)
    {
        if (guid > int.MaxValue)
            return;

        BuyItem((int)guid, (int)itemCount);
    }

    private static MerchantItem CreatePlaceholderMerchantItem(int vendorItemNumber)
    {
        return new MerchantItem(vendorItemNumber, 0, new ItemCacheEntry(IntPtr.Zero));
    }
}
