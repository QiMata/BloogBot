using ForegroundBotRunner.Statics;
using GameData.Core.Enums;

namespace ForegroundBotRunner.Tests;

public sealed class VendorInteractionHelperTests
{
    [Fact]
    public void NormalizeQuantity_ClampsZeroToOne()
    {
        Assert.Equal(1u, VendorInteractionHelper.NormalizeQuantity(0));
        Assert.Equal(3u, VendorInteractionHelper.NormalizeQuantity(3));
    }

    [Fact]
    public void ResolveSellItemGuid_SequentialBagUsesContainedItemOrder()
    {
        var sequentialGuids = new List<ulong> { 11ul, 22ul, 33ul };

        ulong resolved = VendorInteractionHelper.ResolveSellItemGuid(
            VendorInteractionHelper.SequentialBagId,
            slotId: 1,
            sequentialGuids,
            (_, _) => 0);

        Assert.Equal(22ul, resolved);
    }

    [Fact]
    public void ResolveSellItemGuid_DirectBagSlotUsesResolver()
    {
        ulong resolved = VendorInteractionHelper.ResolveSellItemGuid(
            bagId: 2,
            slotId: 5,
            sequentialItemGuids: [],
            (bagId, slotId) =>
            {
                Assert.Equal((byte)2, bagId);
                Assert.Equal((byte)5, slotId);
                return 44ul;
            });

        Assert.Equal(44ul, resolved);
    }

    [Fact]
    public void BuildResolveMerchantSlotLua_SearchesForItemId()
    {
        string lua = VendorInteractionHelper.BuildResolveMerchantSlotLua(159u);

        Assert.Contains("local targetId = 159;", lua);
        Assert.Contains("GetMerchantNumItems()", lua);
        Assert.Contains("GetMerchantItemLink(i)", lua);
        Assert.Contains("item:(%d+):", lua);
        Assert.Contains("{0} = found", lua);
    }

    [Fact]
    public void BuildBuyMerchantItemLua_UsesNormalizedQuantity()
    {
        Assert.Equal("BuyMerchantItem(7, 1)", VendorInteractionHelper.BuildBuyMerchantItemLua(7, 0));
        Assert.Equal("BuyMerchantItem(7, 4)", VendorInteractionHelper.BuildBuyMerchantItemLua(7, 4));
    }

    [Theory]
    [InlineData(6948u, "Hearthstone", ItemQuality.Poor, false)]
    [InlineData(117u, "Tough Jerky", ItemQuality.Common, false)]
    [InlineData(118u, "Worn Shortsword", ItemQuality.Common, true)]
    [InlineData(119u, "Rusty Hatchet", ItemQuality.Poor, true)]
    [InlineData(120u, "Uncommon Shield", ItemQuality.Uncommon, false)]
    public void IsLikelyJunk_MatchesVendorHeuristic(uint itemId, string itemName, ItemQuality quality, bool expected)
    {
        Assert.Equal(expected, VendorInteractionHelper.IsLikelyJunk(itemId, itemName, quality));
    }
}
