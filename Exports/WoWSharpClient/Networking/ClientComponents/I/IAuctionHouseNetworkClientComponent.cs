using System.Reactive;
namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling auction house operations in World of Warcraft.
    /// Manages browsing auctions, placing bids, posting auctions, and collecting sold items.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface IAuctionHouseNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether an auction house window is currently open.
        /// </summary>
        bool IsAuctionHouseOpen { get; }

        #region Reactive Observables
        /// <summary>
        /// Observable stream fired when an auction house window is opened. Carries the auctioneer GUID.
        /// </summary>
        IObservable<ulong> AuctionHouseOpenedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction house window is closed.
        /// </summary>
        IObservable<Unit> AuctionHouseClosedStream { get; }

        /// <summary>
        /// Observable stream fired when auction search results are received.
        /// </summary>
        IObservable<IReadOnlyList<AuctionData>> AuctionSearchResultsStream { get; }

        /// <summary>
        /// Observable stream fired when owned auction results are received.
        /// </summary>
        IObservable<IReadOnlyList<AuctionData>> OwnedAuctionResultsStream { get; }

        /// <summary>
        /// Observable stream fired when bidder auction results are received.
        /// </summary>
        IObservable<IReadOnlyList<AuctionData>> BidderAuctionResultsStream { get; }

        /// <summary>
        /// Observable stream fired when an auction operation completes successfully.
        /// </summary>
        IObservable<AuctionOperationResult> AuctionOperationCompletedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction operation fails.
        /// </summary>
        IObservable<AuctionOperationError> AuctionOperationFailedStream { get; }

        /// <summary>
        /// Observable stream fired when a bid is successfully placed.
        /// </summary>
        IObservable<BidPlacedData> BidPlacedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction is successfully posted.
        /// </summary>
        IObservable<AuctionPostedData> AuctionPostedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction is successfully cancelled. Carries the auction ID.
        /// </summary>
        IObservable<uint> AuctionCancelledStream { get; }

        /// <summary>
        /// Observable stream fired when receiving auction house notifications (outbid, won, etc.).
        /// </summary>
        IObservable<AuctionNotificationData> AuctionNotificationStream { get; }
        #endregion

        /// <summary>
        /// Opens the auction house window by greeting the specified auctioneer NPC.
        /// Sends MSG_AUCTION_HELLO to initiate auction house interaction.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID of the auctioneer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenAuctionHouseAsync(ulong auctioneerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the auction house window.
        /// This typically happens automatically when moving away from the auctioneer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseAuctionHouseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for auctions based on the specified criteria.
        /// Sends CMSG_AUCTION_LIST_ITEMS with search parameters.
        /// </summary>
        /// <param name="name">Item name to search for (can be partial).</param>
        /// <param name="levelMin">Minimum level requirement (0 for no limit).</param>
        /// <param name="levelMax">Maximum level requirement (0 for no limit).</param>
        /// <param name="category">Item category filter.</param>
        /// <param name="subCategory">Item subcategory filter.</param>
        /// <param name="quality">Item quality filter.</param>
        /// <param name="usable">Whether to filter for usable items only.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SearchAuctionsAsync(
            string name = "",
            uint levelMin = 0,
            uint levelMax = 0,
            uint category = 0,
            uint subCategory = 0,
            AuctionQuality quality = AuctionQuality.Any,
            bool usable = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all auctions owned by the current player.
        /// Sends CMSG_AUCTION_LIST_OWNER_ITEMS to get owned auctions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GetOwnedAuctionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all auctions where the current player has placed bids.
        /// Sends CMSG_AUCTION_LIST_BIDDER_ITEMS to get bidder auctions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GetBidderAuctionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Places a bid on the specified auction.
        /// Sends CMSG_AUCTION_PLACE_BID with the auction ID and bid amount.
        /// </summary>
        /// <param name="auctionId">The ID of the auction to bid on.</param>
        /// <param name="bidAmount">The amount to bid in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PlaceBidAsync(uint auctionId, uint bidAmount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts an item for auction with the specified parameters.
        /// Sends CMSG_AUCTION_SELL_ITEM with item details and pricing.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="startBid">The starting bid amount in copper.</param>
        /// <param name="buyoutPrice">The buyout price in copper (0 for no buyout).</param>
        /// <param name="duration">The auction duration (12, 24, or 48 hours).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PostAuctionAsync(
            byte bagId,
            byte slotId,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels an owned auction and returns the item to the player.
        /// Sends CMSG_AUCTION_REMOVE_ITEM with the auction ID.
        /// </summary>
        /// <param name="auctionId">The ID of the auction to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelAuctionAsync(uint auctionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a buyout on the specified auction at the buyout price.
        /// This is a convenience method that places a bid equal to the buyout price.
        /// </summary>
        /// <param name="auctionId">The ID of the auction to buy out.</param>
        /// <param name="buyoutPrice">The buyout price in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BuyoutAuctionAsync(uint auctionId, uint buyoutPrice, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete auction house interaction: open, search, close.
        /// This is a convenience method for simple searches.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID of the auctioneer NPC.</param>
        /// <param name="itemName">The item name to search for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickSearchAsync(ulong auctioneerGuid, string itemName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete auction house interaction: open, post auction, close.
        /// This is a convenience method for simple auction posting.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID of the auctioneer NPC.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="startBid">The starting bid amount in copper.</param>
        /// <param name="buyoutPrice">The buyout price in copper.</param>
        /// <param name="duration">The auction duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickPostAsync(
            ulong auctioneerGuid,
            byte bagId,
            byte slotId,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete auction house interaction: open, buyout auction, close.
        /// This is a convenience method for simple buyouts.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID of the auctioneer NPC.</param>
        /// <param name="auctionId">The ID of the auction to buy out.</param>
        /// <param name="buyoutPrice">The buyout price in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickBuyoutAsync(ulong auctioneerGuid, uint auctionId, uint buyoutPrice, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified auctioneer GUID has an open auction house window.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID to check.</param>
        /// <returns>True if the auction house window is open for the specified GUID, false otherwise.</returns>
        bool IsAuctionHouseOpenWith(ulong auctioneerGuid);
    }

    /// <summary>
    /// Represents auction data received from the server.
    /// </summary>
    public record AuctionData(
        uint AuctionId,
        uint ItemId,
        uint ItemCount,
        uint ItemCharges,
        ulong OwnerGuid,
        uint StartBid,
        uint MinBid,
        uint BuyoutPrice,
        uint TimeLeft,
        ulong HighestBidderGuid,
        uint HighestBid);

    /// <summary>
    /// Represents the type of auction operation.
    /// </summary>
    public enum AuctionOperationType
    {
        Search,
        PlaceBid,
        PostAuction,
        CancelAuction,
        Buyout
    }

    /// <summary>
    /// Represents auction house notification types.
    /// </summary>
    public enum AuctionNotificationType
    {
        Outbid,
        Won,
        Sold,
        Expired,
        Cancelled
    }

    /// <summary>
    /// Represents auction duration options.
    /// </summary>
    public enum AuctionDuration : uint
    {
        TwelveHours = 12,
        TwentyFourHours = 24,
        FortyEightHours = 48
    }

    /// <summary>
    /// Represents item quality filters for auction searches.
    /// </summary>
    public enum AuctionQuality : uint
    {
        Any = 0,
        Poor = 1,
        Common = 2,
        Uncommon = 3,
        Rare = 4,
        Epic = 5,
        Legendary = 6
    }

    /// <summary>
    /// Data for a successful auction operation completion.
    /// </summary>
    public record AuctionOperationResult(AuctionOperationType Operation, uint AuctionId);

    /// <summary>
    /// Data for a failed auction operation.
    /// </summary>
    public record AuctionOperationError(AuctionOperationType Operation, string ErrorReason);

    /// <summary>
    /// Data for a successful bid placement.
    /// </summary>
    public record BidPlacedData(uint AuctionId, uint BidAmount);

    /// <summary>
    /// Data for a successful auction post.
    /// </summary>
    public record AuctionPostedData(uint ItemId, uint StartBid, uint BuyoutPrice, uint Duration);

    /// <summary>
    /// Data for auction notifications.
    /// </summary>
    public record AuctionNotificationData(AuctionNotificationType NotificationType, uint AuctionId, uint ItemId);
}