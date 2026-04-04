using GameData.Core.Frames;
using System;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// Lua-based FG auction house frame. Wraps WoW 1.12.1 AH Lua API.
/// </summary>
public sealed class FgAuctionFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : IAuctionFrame
{
    private const string AhVisibleLua =
        "if AuctionFrame and AuctionFrame:IsVisible() then {0} = 1 else {0} = 0 end";

    public bool IsOpen
    {
        get
        {
            var result = luaCallWithResult(AhVisibleLua);
            return result.Length > 0 && result[0] == "1";
        }
    }

    public void SearchByName(string name)
    {
        luaCall($"BrowseName:SetText(\"{name}\"); AuctionFrameBrowse_Search()");
    }

    public void PostItem(int bagId, int slotId, int startBid, int buyout, int durationHours)
    {
        // PickupContainerItem places the item on the cursor, then PostAuction sends it
        luaCall($"PickupContainerItem({bagId},{slotId}); ClickAuctionSellItemButton(); " +
                $"StartAuction({startBid},{buyout},{durationHours * 60})");
    }

    public void PlaceBid(int auctionId, int amount)
    {
        luaCall($"PlaceAuctionBid(\"list\",{auctionId},{amount})");
    }

    public void CancelAuction(int auctionId)
    {
        luaCall($"CancelAuction({auctionId})");
    }

    public void Close()
    {
        luaCall("CloseAuctionHouse()");
    }
}
