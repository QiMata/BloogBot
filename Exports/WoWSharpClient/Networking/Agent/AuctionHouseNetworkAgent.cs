using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of auction house network agent that handles auction house operations in World of Warcraft.
    /// Manages browsing auctions, placing bids, posting auctions, and collecting sold items using the Mangos protocol.
    /// </summary>
    public class AuctionHouseNetworkAgent : IAuctionHouseNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AuctionHouseNetworkAgent> _logger;

        private bool _isAuctionHouseOpen;
        private ulong? _currentAuctioneerGuid;

        /// <summary>
        /// Initializes a new instance of the AuctionHouseNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public AuctionHouseNetworkAgent(IWorldClient worldClient, ILogger<AuctionHouseNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsAuctionHouseOpen => _isAuctionHouseOpen;

        /// <inheritdoc />
        public event Action<ulong>? AuctionHouseOpened;

        /// <inheritdoc />
        public event Action? AuctionHouseClosed;

        /// <inheritdoc />
        public event Action<IReadOnlyList<AuctionData>>? AuctionSearchResults;

        /// <inheritdoc />
        public event Action<IReadOnlyList<AuctionData>>? OwnedAuctionResults;

        /// <inheritdoc />
        public event Action<IReadOnlyList<AuctionData>>? BidderAuctionResults;

        /// <inheritdoc />
        public event Action<AuctionOperationType, uint>? AuctionOperationCompleted;

        /// <inheritdoc />
        public event Action<AuctionOperationType, string>? AuctionOperationFailed;

        /// <inheritdoc />
        public event Action<uint, uint>? BidPlaced;

        /// <inheritdoc />
        public event Action<uint, uint, uint, uint>? AuctionPosted;

        /// <inheritdoc />
        public event Action<uint>? AuctionCancelled;

        /// <inheritdoc />
        public event Action<AuctionNotificationType, uint, uint>? AuctionNotification;

        /// <inheritdoc />
        public async Task OpenAuctionHouseAsync(ulong auctioneerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening auction house interaction with: {AuctioneerGuid:X}", auctioneerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.MSG_AUCTION_HELLO, payload, cancellationToken);

                _logger.LogInformation("Auction house interaction initiated with: {AuctioneerGuid:X}", auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open auction house interaction with: {AuctioneerGuid:X}", auctioneerGuid);
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Failed to open auction house: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseAuctionHouseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing auction house window");

                // Auction house windows typically close automatically when moving away
                // But we can update our internal state
                _isAuctionHouseOpen = false;
                _currentAuctioneerGuid = null;
                AuctionHouseClosed?.Invoke();

                _logger.LogInformation("Auction house window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close auction house window");
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Failed to close auction house: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SearchAuctionsAsync(
            string name = "",
            uint levelMin = 0,
            uint levelMax = 0,
            uint category = 0,
            uint subCategory = 0,
            AuctionQuality quality = AuctionQuality.Any,
            bool usable = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Searching auctions with criteria: Name='{Name}', LevelMin={LevelMin}, LevelMax={LevelMax}, Category={Category}, SubCategory={SubCategory}, Quality={Quality}, Usable={Usable}",
                    name, levelMin, levelMax, category, subCategory, quality, usable);

                // Build the payload for CMSG_AUCTION_LIST_ITEMS
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                var payload = new byte[24 + nameBytes.Length + 1]; // 24 bytes for parameters + string + null terminator

                var offset = 0;
                
                // String (null-terminated)
                nameBytes.CopyTo(payload, offset);
                offset += nameBytes.Length;
                payload[offset++] = 0; // null terminator

                // Level range
                BitConverter.GetBytes(levelMin).CopyTo(payload, offset);
                offset += 4;
                BitConverter.GetBytes(levelMax).CopyTo(payload, offset);
                offset += 4;

                // Category filters
                BitConverter.GetBytes(category).CopyTo(payload, offset);
                offset += 4;
                BitConverter.GetBytes(subCategory).CopyTo(payload, offset);
                offset += 4;

                // Quality filter
                BitConverter.GetBytes((uint)quality).CopyTo(payload, offset);
                offset += 4;

                // Usable filter
                BitConverter.GetBytes(usable ? 1u : 0u).CopyTo(payload, offset);

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_LIST_ITEMS, payload, cancellationToken);

                _logger.LogInformation("Auction search request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search auctions");
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Failed to search auctions: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task GetOwnedAuctionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting owned auctions");

                // CMSG_AUCTION_LIST_OWNER_ITEMS has no payload
                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Owned auctions request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get owned auctions");
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Failed to get owned auctions: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task GetBidderAuctionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting bidder auctions");

                // CMSG_AUCTION_LIST_BIDDER_ITEMS has no payload
                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS, Array.Empty<byte>(), cancellationToken);

                _logger.LogInformation("Bidder auctions request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get bidder auctions");
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Failed to get bidder auctions: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PlaceBidAsync(uint auctionId, uint bidAmount, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Placing bid of {BidAmount} copper on auction {AuctionId}", bidAmount, auctionId);

                var payload = new byte[8];
                BitConverter.GetBytes(auctionId).CopyTo(payload, 0);
                BitConverter.GetBytes(bidAmount).CopyTo(payload, 4);

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_PLACE_BID, payload, cancellationToken);

                _logger.LogInformation("Bid placement request sent for auction {AuctionId} with amount {BidAmount}", auctionId, bidAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to place bid on auction {AuctionId}", auctionId);
                AuctionOperationFailed?.Invoke(AuctionOperationType.PlaceBid, $"Failed to place bid: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PostAuctionAsync(
            byte bagId,
            byte slotId,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Posting auction for item in bag {BagId} slot {SlotId} with start bid {StartBid}, buyout {BuyoutPrice}, duration {Duration}h",
                    bagId, slotId, startBid, buyoutPrice, (uint)duration);

                var payload = new byte[14];
                var offset = 0;

                // Bag and slot
                payload[offset++] = bagId;
                payload[offset++] = slotId;

                // Pricing
                BitConverter.GetBytes(startBid).CopyTo(payload, offset);
                offset += 4;
                BitConverter.GetBytes(buyoutPrice).CopyTo(payload, offset);
                offset += 4;

                // Duration in hours
                BitConverter.GetBytes((uint)duration).CopyTo(payload, offset);

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_SELL_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auction posting request sent for item in bag {BagId} slot {SlotId}", bagId, slotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post auction for item in bag {BagId} slot {SlotId}", bagId, slotId);
                AuctionOperationFailed?.Invoke(AuctionOperationType.PostAuction, $"Failed to post auction: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CancelAuctionAsync(uint auctionId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Cancelling auction {AuctionId}", auctionId);

                var payload = new byte[4];
                BitConverter.GetBytes(auctionId).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUCTION_REMOVE_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auction cancellation request sent for auction {AuctionId}", auctionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel auction {AuctionId}", auctionId);
                AuctionOperationFailed?.Invoke(AuctionOperationType.CancelAuction, $"Failed to cancel auction: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task BuyoutAuctionAsync(uint auctionId, uint buyoutPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Buying out auction {AuctionId} for {BuyoutPrice} copper", auctionId, buyoutPrice);

                // Buyout is essentially placing a bid equal to the buyout price
                await PlaceBidAsync(auctionId, buyoutPrice, cancellationToken);

                _logger.LogInformation("Buyout request sent for auction {AuctionId}", auctionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buyout auction {AuctionId}", auctionId);
                AuctionOperationFailed?.Invoke(AuctionOperationType.Buyout, $"Failed to buyout auction: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsAuctionHouseOpenWith(ulong auctioneerGuid)
        {
            return _isAuctionHouseOpen && _currentAuctioneerGuid == auctioneerGuid;
        }

        /// <inheritdoc />
        public async Task QuickSearchAsync(ulong auctioneerGuid, string itemName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick search for '{ItemName}' with auctioneer: {AuctioneerGuid:X}", itemName, auctioneerGuid);

                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                
                // Small delay to allow auction house window to open
                await Task.Delay(100, cancellationToken);
                
                await SearchAuctionsAsync(itemName, cancellationToken: cancellationToken);
                
                // Small delay to allow search to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseAuctionHouseAsync(cancellationToken);

                _logger.LogInformation("Quick search completed for '{ItemName}' with auctioneer: {AuctioneerGuid:X}", itemName, auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick search failed for '{ItemName}' with auctioneer: {AuctioneerGuid:X}", itemName, auctioneerGuid);
                AuctionOperationFailed?.Invoke(AuctionOperationType.Search, $"Quick search failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickPostAsync(
            ulong auctioneerGuid,
            byte bagId,
            byte slotId,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick post for item in bag {BagId} slot {SlotId} with auctioneer: {AuctioneerGuid:X}",
                    bagId, slotId, auctioneerGuid);

                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                
                // Small delay to allow auction house window to open
                await Task.Delay(100, cancellationToken);
                
                await PostAuctionAsync(bagId, slotId, startBid, buyoutPrice, duration, cancellationToken);
                
                // Small delay to allow posting to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseAuctionHouseAsync(cancellationToken);

                _logger.LogInformation("Quick post completed for item in bag {BagId} slot {SlotId} with auctioneer: {AuctioneerGuid:X}",
                    bagId, slotId, auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick post failed for item in bag {BagId} slot {SlotId} with auctioneer: {AuctioneerGuid:X}",
                    bagId, slotId, auctioneerGuid);
                AuctionOperationFailed?.Invoke(AuctionOperationType.PostAuction, $"Quick post failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickBuyoutAsync(ulong auctioneerGuid, uint auctionId, uint buyoutPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick buyout for auction {AuctionId} with auctioneer: {AuctioneerGuid:X}",
                    auctionId, auctioneerGuid);

                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                
                // Small delay to allow auction house window to open
                await Task.Delay(100, cancellationToken);
                
                await BuyoutAuctionAsync(auctionId, buyoutPrice, cancellationToken);
                
                // Small delay to allow buyout to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseAuctionHouseAsync(cancellationToken);

                _logger.LogInformation("Quick buyout completed for auction {AuctionId} with auctioneer: {AuctioneerGuid:X}",
                    auctionId, auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick buyout failed for auction {AuctionId} with auctioneer: {AuctioneerGuid:X}",
                    auctionId, auctioneerGuid);
                AuctionOperationFailed?.Invoke(AuctionOperationType.Buyout, $"Quick buyout failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles server responses for auction house window opening.
        /// This method should be called when auction house list responses are received.
        /// </summary>
        /// <param name="auctioneerGuid">The GUID of the auctioneer.</param>
        public void HandleAuctionHouseOpened(ulong auctioneerGuid)
        {
            _isAuctionHouseOpen = true;
            _currentAuctioneerGuid = auctioneerGuid;
            AuctionHouseOpened?.Invoke(auctioneerGuid);
            _logger.LogDebug("Auction house opened for auctioneer: {AuctioneerGuid:X}", auctioneerGuid);
        }

        /// <summary>
        /// Handles server responses for auction search results.
        /// This method should be called when SMSG_AUCTION_LIST_RESULT is received.
        /// </summary>
        /// <param name="auctions">The list of auction data received.</param>
        public void HandleAuctionSearchResults(IReadOnlyList<AuctionData> auctions)
        {
            AuctionSearchResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} auction search results", auctions.Count);
        }

        /// <summary>
        /// Handles server responses for owned auction results.
        /// This method should be called when SMSG_AUCTION_OWNER_LIST_RESULT is received.
        /// </summary>
        /// <param name="auctions">The list of owned auction data.</param>
        public void HandleOwnedAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            OwnedAuctionResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} owned auction results", auctions.Count);
        }

        /// <summary>
        /// Handles server responses for bidder auction results.
        /// This method should be called when SMSG_AUCTION_BIDDER_LIST_RESULT is received.
        /// </summary>
        /// <param name="auctions">The list of bidder auction data.</param>
        public void HandleBidderAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            BidderAuctionResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} bidder auction results", auctions.Count);
        }

        /// <summary>
        /// Handles server responses for successful auction operations.
        /// This method should be called when SMSG_AUCTION_COMMAND_RESULT indicates success.
        /// </summary>
        /// <param name="operation">The type of operation that completed.</param>
        /// <param name="auctionId">The auction ID that was affected.</param>
        public void HandleAuctionOperationCompleted(AuctionOperationType operation, uint auctionId)
        {
            AuctionOperationCompleted?.Invoke(operation, auctionId);
            _logger.LogDebug("Auction operation {Operation} completed for auction {AuctionId}", operation, auctionId);
        }

        /// <summary>
        /// Handles server responses for auction operation failures.
        /// This method should be called when SMSG_AUCTION_COMMAND_RESULT indicates failure.
        /// </summary>
        /// <param name="operation">The type of operation that failed.</param>
        /// <param name="errorReason">The reason for the failure.</param>
        public void HandleAuctionOperationFailed(AuctionOperationType operation, string errorReason)
        {
            AuctionOperationFailed?.Invoke(operation, errorReason);
            _logger.LogWarning("Auction operation {Operation} failed: {Error}", operation, errorReason);
        }

        /// <summary>
        /// Handles server responses for successful bid placement.
        /// This method should be called when a bid is successfully placed.
        /// </summary>
        /// <param name="auctionId">The auction ID that was bid on.</param>
        /// <param name="bidAmount">The amount of the bid in copper.</param>
        public void HandleBidPlaced(uint auctionId, uint bidAmount)
        {
            BidPlaced?.Invoke(auctionId, bidAmount);
            _logger.LogDebug("Bid of {BidAmount} copper placed on auction {AuctionId}", bidAmount, auctionId);
        }

        /// <summary>
        /// Handles server responses for successful auction posting.
        /// This method should be called when an auction is successfully posted.
        /// </summary>
        /// <param name="itemId">The item ID that was posted.</param>
        /// <param name="startBid">The starting bid in copper.</param>
        /// <param name="buyoutPrice">The buyout price in copper.</param>
        /// <param name="duration">The auction duration in hours.</param>
        public void HandleAuctionPosted(uint itemId, uint startBid, uint buyoutPrice, uint duration)
        {
            AuctionPosted?.Invoke(itemId, startBid, buyoutPrice, duration);
            _logger.LogDebug("Auction posted for item {ItemId} with start bid {StartBid}, buyout {BuyoutPrice}, duration {Duration}h",
                itemId, startBid, buyoutPrice, duration);
        }

        /// <summary>
        /// Handles server responses for successful auction cancellation.
        /// This method should be called when an auction is successfully cancelled.
        /// </summary>
        /// <param name="auctionId">The auction ID that was cancelled.</param>
        public void HandleAuctionCancelled(uint auctionId)
        {
            AuctionCancelled?.Invoke(auctionId);
            _logger.LogDebug("Auction {AuctionId} cancelled", auctionId);
        }

        /// <summary>
        /// Handles server responses for auction house notifications.
        /// This method should be called for auction notifications (outbid, won, etc.).
        /// </summary>
        /// <param name="notificationType">The type of notification.</param>
        /// <param name="auctionId">The auction ID related to the notification.</param>
        /// <param name="itemId">The item ID related to the notification.</param>
        public void HandleAuctionNotification(AuctionNotificationType notificationType, uint auctionId, uint itemId)
        {
            AuctionNotification?.Invoke(notificationType, auctionId, itemId);
            _logger.LogDebug("Auction notification: {NotificationType} for auction {AuctionId} (item {ItemId})",
                notificationType, auctionId, itemId);
        }
    }
}