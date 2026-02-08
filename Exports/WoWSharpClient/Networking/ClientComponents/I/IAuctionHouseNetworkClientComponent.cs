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
        /// Observable stream fired when a bid is successfully placed (includes outbid increment).
        /// </summary>
        IObservable<BidPlacedData> BidPlacedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction is successfully posted.
        /// </summary>
        IObservable<AuctionPostedData> AuctionPostedStream { get; }

        /// <summary>
        /// Observable stream fired when an auction is removed. Carries removal data (auctionId, itemEntry, randomPropertyId).
        /// </summary>
        IObservable<AuctionNotificationData> AuctionRemovedStream { get; }

        /// <summary>
        /// Observable stream fired when receiving auction house notifications (outbid, won, sold, expired).
        /// </summary>
        IObservable<AuctionNotificationData> AuctionNotificationStream { get; }

        /// <summary>
        /// Total number of auctions matching the last search (may exceed the per-packet item count).
        /// </summary>
        uint TotalSearchResultCount { get; }
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
        /// <param name="levelMin">Minimum item level requirement (0 for no limit).</param>
        /// <param name="levelMax">Maximum item level requirement (0 for no limit).</param>
        /// <param name="inventoryType">Inventory type filter (0 for any).</param>
        /// <param name="itemClass">Item class filter (0 for any).</param>
        /// <param name="itemSubClass">Item subclass filter (0 for any).</param>
        /// <param name="quality">Item quality filter (-1 for any).</param>
        /// <param name="usable">Whether to filter for usable items only.</param>
        /// <param name="listOffset">Page offset for pagination (0 = first page).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SearchAuctionsAsync(
            string name = "",
            byte levelMin = 0,
            byte levelMax = 0,
            uint inventoryType = 0,
            uint itemClass = 0,
            uint itemSubClass = 0,
            AuctionQuality quality = AuctionQuality.Any,
            bool usable = false,
            uint listOffset = 0,
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
        /// Sends CMSG_AUCTION_SELL_ITEM with item GUID and pricing.
        /// </summary>
        /// <param name="itemGuid">The GUID of the item to auction.</param>
        /// <param name="startBid">The starting bid amount in copper.</param>
        /// <param name="buyoutPrice">The buyout price in copper (0 for no buyout).</param>
        /// <param name="duration">The auction duration (12, 24, or 48 hours).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PostAuctionAsync(
            ulong itemGuid,
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
        /// <param name="itemGuid">The GUID of the item to auction.</param>
        /// <param name="startBid">The starting bid amount in copper.</param>
        /// <param name="buyoutPrice">The buyout price in copper.</param>
        /// <param name="duration">The auction duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickPostAsync(
            ulong auctioneerGuid,
            ulong itemGuid,
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
    /// Represents auction data received from the server (BuildAuctionInfo format).
    /// Per-entry: 68 bytes in SMSG_AUCTION_LIST_RESULT / OWNER_LIST / BIDDER_LIST.
    /// </summary>
    public record AuctionData(
        uint AuctionId,
        uint ItemEntry,
        uint EnchantmentId,
        uint RandomPropertyId,
        uint SuffixFactor,
        uint ItemCount,
        uint SpellCharges,
        ulong OwnerGuid,
        uint StartBid,
        uint MinOutbid,
        uint BuyoutPrice,
        uint TimeLeftMs,
        ulong BidderGuid,
        uint CurrentBid);

    /// <summary>
    /// Server-side auction action codes from SMSG_AUCTION_COMMAND_RESULT.
    /// Values must match the MaNGOS AuctionAction enum.
    /// </summary>
    public enum AuctionAction : uint
    {
        Started = 0,     // AUCTION_STARTED — auction created
        Removed = 1,     // AUCTION_REMOVED — auction cancelled
        BidPlaced = 2    // AUCTION_BID_PLACED — bid placed or buyout
    }

    /// <summary>
    /// Server-side auction error codes from SMSG_AUCTION_COMMAND_RESULT.
    /// </summary>
    public enum AuctionError : uint
    {
        Ok = 0,
        InternalError = 1,
        NotEnoughMoney = 2,
        ItemNotFound = 3,
        HigherBid = 4,
        IncrementTooLow = 5,
        NotEnoughSpace = 7,
        DatabaseError = 8,
        ItemHasQuote = 9,
        ItemNotInInventory = 10,
        RestrictedAccount = 13
    }

    /// <summary>
    /// Represents auction house notification types.
    /// </summary>
    public enum AuctionNotificationType
    {
        Outbid,
        Won,
        Sold,
        Expired
    }

    /// <summary>
    /// Represents auction duration options (in hours, converted to minutes for CMSG).
    /// </summary>
    public enum AuctionDuration : uint
    {
        TwelveHours = 12,
        TwentyFourHours = 24,
        FortyEightHours = 48
    }

    /// <summary>
    /// Represents item quality filters for auction searches.
    /// Server uses int32: -1 = any quality.
    /// </summary>
    public enum AuctionQuality : int
    {
        Any = -1,
        Poor = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5
    }

    /// <summary>
    /// Data for a successful auction operation completion.
    /// </summary>
    public record AuctionOperationResult(AuctionAction Action, uint AuctionId);

    /// <summary>
    /// Data for a failed auction operation.
    /// </summary>
    public record AuctionOperationError(AuctionAction Action, AuctionError Error, string ErrorReason);

    /// <summary>
    /// Data for a successful bid placement.
    /// </summary>
    public record BidPlacedData(uint AuctionId, uint OutbidAmount);

    /// <summary>
    /// Data for a successful auction post.
    /// </summary>
    public record AuctionPostedData(uint AuctionId);

    /// <summary>
    /// Data for auction notifications (bidder or owner).
    /// </summary>
    public record AuctionNotificationData(
        AuctionNotificationType NotificationType,
        uint AuctionId,
        uint ItemEntry,
        uint RandomPropertyId);

    /// <summary>
    /// Extended bidder notification data (includes outbid amount).
    /// </summary>
    public record AuctionBidderNotificationData(
        uint HouseId,
        uint AuctionId,
        ulong BidderGuid,
        uint OutbidAmount,
        uint MinOutbid,
        uint ItemEntry,
        uint RandomPropertyId) : AuctionNotificationData(
            AuctionNotificationType.Outbid, AuctionId, ItemEntry, RandomPropertyId);

    /// <summary>
    /// Extended owner notification data (includes bid amount).
    /// </summary>
    public record AuctionOwnerNotificationData(
        uint AuctionId,
        uint BidAmount,
        uint MinOutbid,
        ulong BuyerGuid,
        uint ItemEntry,
        uint RandomPropertyId) : AuctionNotificationData(
            BidAmount > 0 ? AuctionNotificationType.Sold : AuctionNotificationType.Expired,
            AuctionId, ItemEntry, RandomPropertyId);
}