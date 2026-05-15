using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG merchant-frame surface backed by <see cref="IVendorNetworkClientComponent"/>.
/// Routes <see cref="IMerchantFrame"/> operations through the BG packet path so
/// InteractionSequenceBuilder's buyback / single-slot repair / repair-all
/// sequences no longer short-circuit with "MerchantFrame is null --
/// use vendorGuid-based ... for BG bot" on BG bots. Closes S1.17.
///
/// Behavior parity with FG (<c>Services/ForegroundBotRunner/Frames/FgMerchantFrame.cs</c>):
/// <c>RepairCost(EquipSlot)</c> returns the total repair cost (the FG Lua bridge
/// `GetRepairAllCost()` exposes total only); <c>RepairByEquipSlot</c> is
/// equivalent to <c>RepairAll</c> when total cost is positive. Matches the
/// shipping "Can Afford Repair" check in InteractionSequenceBuilder which gates
/// on <c>Player.Copper &gt; RepairCost(slot)</c>.
/// </summary>
public sealed class NetworkMerchantFrame(Func<IVendorNetworkClientComponent?> resolveVendorAgent) : IMerchantFrame
{
    public bool IsOpen => resolveVendorAgent()?.IsVendorWindowOpen == true;

    public void Close()
    {
        var vendor = resolveVendorAgent();
        if (vendor?.IsVendorWindowOpen != true) return;
        vendor.CloseVendorAsync().GetAwaiter().GetResult();
    }

    public bool CanRepair => resolveVendorAgent()?.CurrentVendor?.CanRepair == true;

    public int TotalRepairCost
    {
        get
        {
            var vendor = resolveVendorAgent();
            var currentGuid = vendor?.CurrentVendor?.VendorGuid;
            if (vendor == null || currentGuid == null || currentGuid == 0UL) return 0;
            try
            {
                return (int)vendor.GetRepairCostAsync(currentGuid.Value).GetAwaiter().GetResult();
            }
            catch
            {
                // Vendor cost query failed (window closed mid-query, packet error, etc.).
                // Returning 0 surfaces as "no repair needed" so the "Can Afford Repair"
                // gate proceeds and the repair packet itself produces the authoritative
                // failure if the vendor is actually unavailable.
                return 0;
            }
        }
    }

    public bool Ready => IsOpen;

    public IReadOnlyList<MerchantItem> Items
    {
        get
        {
            var available = resolveVendorAgent()?.GetAvailableItems();
            if (available == null || available.Count == 0) return Array.Empty<MerchantItem>();
            // BG-side MerchantItem placeholder: only the vendor slot + item id survive
            // the protocol gap because IObjectManager's ItemCacheEntry needs a memory
            // pointer (FG-only). Callers that need MerchantItem.Price/Name should query
            // the vendor agent directly via FindVendorItem(itemId).
            return available
                .Select((it, idx) => new MerchantItem(idx + 1, (int)it.ItemId, new ItemCacheEntry(IntPtr.Zero)))
                .ToList();
        }
    }

    public void SellItem(int bagId, int slotId, int quantity)
    {
        var vendor = resolveVendorAgent();
        var currentGuid = vendor?.CurrentVendor?.VendorGuid;
        if (vendor == null || currentGuid == null || currentGuid == 0UL) return;
        vendor.SellItemAsync(currentGuid.Value, (byte)bagId, (byte)slotId, (uint)Math.Max(1, quantity))
            .GetAwaiter().GetResult();
    }

    public void BuyItem(int itemGuid, int itemCount)
    {
        // IMerchantFrame.BuyItem's `int itemGuid` is the 1-based vendor slot index
        // (matches the FG Lua call `BuyMerchantItem(slot, count)`), NOT a world GUID.
        var vendor = resolveVendorAgent();
        var currentGuid = vendor?.CurrentVendor?.VendorGuid;
        if (vendor == null || currentGuid == null || currentGuid == 0UL) return;
        byte vendorSlot = (byte)Math.Max(0, itemGuid - 1); // FG passes 1-based; BG packet is 0-based
        vendor.BuyItemBySlotAsync(currentGuid.Value, vendorSlot, (uint)Math.Max(1, itemCount))
            .GetAwaiter().GetResult();
    }

    public void BuybackItem(int itemGuid, int itemCount)
    {
        // IMerchantFrame.BuybackItem's `int itemGuid` is the buyback slot index
        // (matches FG Lua `BuybackItem(slot)`). itemCount is ignored — buyback is
        // always whole-stack.
        var vendor = resolveVendorAgent();
        var currentGuid = vendor?.CurrentVendor?.VendorGuid;
        if (vendor == null || currentGuid == null || currentGuid == 0UL) return;
        vendor.BuybackItemAsync(currentGuid.Value, (uint)Math.Max(0, itemGuid))
            .GetAwaiter().GetResult();
    }

    public int RepairCost(EquipSlot equipSlot)
    {
        // Mirror FG: per-slot cost is not exposed by the WoW protocol; return total.
        // Callers gate "Can Afford" on Player.Copper > RepairCost(slot), which is
        // satisfied identically for total-vs-slot. If the vendor can't repair (no
        // repair-anvil flag), return 0 so the gate fails out cleanly.
        if (!CanRepair) return 0;
        return TotalRepairCost;
    }

    public void RepairByEquipSlot(EquipSlot parSlot)
    {
        if (RepairCost(parSlot) > 0)
            RepairAll();
    }

    public void RepairAll()
    {
        var vendor = resolveVendorAgent();
        var currentGuid = vendor?.CurrentVendor?.VendorGuid;
        if (vendor == null || currentGuid == null || currentGuid == 0UL) return;
        vendor.RepairAllItemsAsync(currentGuid.Value).GetAwaiter().GetResult();
    }

    public void ItemCallback(int parItemId) { }

    public bool IsItemAvaible(int parItemId)
        => resolveVendorAgent()?.FindVendorItem((uint)parItemId) != null;

    public void VendorByGuid(ulong guid, uint itemCount = 1)
    {
        // FG path uses `BuyItem((int)guid, (int)itemCount)` when guid fits int range.
        // On BG the more direct path is BuyItemBySlotAsync once the vendor is open,
        // but the GUID-keyed contract here can't translate to a slot without first
        // opening the vendor — match FG's narrow shape and let BuyItem handle it.
        if (guid > int.MaxValue) return;
        BuyItem((int)guid, (int)itemCount);
    }
}
