using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Statics
{
    internal static class VendorInteractionHelper
    {
        internal const byte SequentialBagId = 0xFF;
        internal const string MerchantOpenProbeLua =
            "if MerchantFrame and MerchantFrame:IsVisible() then {0} = '1' else {0} = '0' end";
        internal const string CloseMerchantLua =
            "if MerchantFrame and MerchantFrame:IsVisible() then CloseMerchant() end";
        internal const string RepairAllLua =
            "if MerchantFrame and MerchantFrame:IsVisible() then RepairAllItems() end";

        internal static uint NormalizeQuantity(uint quantity) => Math.Max(1u, quantity);

        internal static ulong ResolveSellItemGuid(
            byte bagId,
            byte slotId,
            IReadOnlyList<ulong> sequentialItemGuids,
            Func<byte, byte, ulong> bagSlotResolver)
        {
            if (bagId == SequentialBagId)
                return slotId < sequentialItemGuids.Count ? sequentialItemGuids[slotId] : 0;

            return bagSlotResolver(bagId, slotId);
        }

        internal static string BuildResolveMerchantSlotLua(uint itemId)
        {
            return
                "local targetId = " + itemId + "; " +
                "local found = 0; " +
                "local n = GetMerchantNumItems(); " +
                "for i = 1, n do " +
                "  local link = GetMerchantItemLink(i); " +
                "  if link then " +
                "    local _, _, currentId = string.find(link, 'item:(%d+):'); " +
                "    if tonumber(currentId) == targetId then found = i; break; end; " +
                "  end; " +
                "end; " +
                "{0} = found";
        }

        internal static string BuildBuyMerchantItemLua(int merchantSlot, uint quantity)
        {
            return "BuyMerchantItem(" + merchantSlot + ", " + NormalizeQuantity(quantity) + ")";
        }
    }
}
