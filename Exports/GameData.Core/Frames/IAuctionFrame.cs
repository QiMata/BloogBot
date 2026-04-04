namespace GameData.Core.Frames;

/// <summary>
/// Auction house frame interface. FG: Lua-based AH interaction.
/// BG: packet-based via AuctionHouseNetworkClientComponent.
/// </summary>
public interface IAuctionFrame
{
    bool IsOpen { get; }
    void SearchByName(string name);
    void PostItem(int bagId, int slotId, int startBid, int buyout, int durationHours);
    void PlaceBid(int auctionId, int amount);
    void CancelAuction(int auctionId);
    void Close();
}
