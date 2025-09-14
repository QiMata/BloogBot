using System.Reactive.Linq;
using System.Reactive;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of auction house network agent that handles auction house operations in World of Warcraft.
    /// Manages browsing auctions, placing bids, posting auctions, and collecting sold items using the Mangos protocol.
    /// </summary>
    public class AuctionHouseNetworkClientComponent : IAuctionHouseNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AuctionHouseNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private bool _isAuctionHouseOpen;
        private ulong? _currentAuctioneerGuid;
        private readonly List<AuctionData> _currentAuctions = [];
        private readonly List<AuctionData> _ownedAuctions = [];
        private readonly List<AuctionData> _bidderAuctions = [];
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the AuctionHouseNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public AuctionHouseNetworkClientComponent(IWorldClient worldClient, ILogger<AuctionHouseNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region INetworkClientComponent Implementation

        /// <inheritdoc />
        public bool IsOperationInProgress
        {
            get
            {
                lock (_stateLock)
                {
                    return _isOperationInProgress;
                }
            }
        }

        /// <inheritdoc />
        public DateTime? LastOperationTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastOperationTime;
                }
            }
        }

        #endregion

        /// <inheritdoc />
        public bool IsAuctionHouseOpen => _isAuctionHouseOpen;

        #region Legacy events (backwards compatibility)
        public event Action<ulong>? AuctionHouseOpened;
        public event Action? AuctionHouseClosed;
        public event Action<IReadOnlyList<AuctionData>>? AuctionSearchResults;
        public event Action<IReadOnlyList<AuctionData>>? OwnedAuctionResults;
        public event Action<IReadOnlyList<AuctionData>>? BidderAuctionResults;
        public event Action<AuctionOperationType, uint>? AuctionOperationCompleted;
        public event Action<AuctionOperationType, string>? AuctionOperationFailed;
        public event Action<uint, uint>? BidPlaced;
        public event Action<uint, uint, uint, uint>? AuctionPosted;
        public event Action<uint>? AuctionCancelled;
        public event Action<AuctionNotificationType, uint, uint>? AuctionNotification;
        #endregion

        #region Reactive Observables (derived from events)
        public IObservable<ulong> AuctionHouseOpenedStream =>
            Observable.FromEvent<Action<ulong>, ulong>(
                h => (guid) => h(guid),
                h => AuctionHouseOpened += h,
                h => AuctionHouseOpened -= h);

        public IObservable<Unit> AuctionHouseClosedStream =>
            Observable.FromEvent<Action, Unit>(
                    h => () => h(Unit.Default),
                    h => AuctionHouseClosed += h,
                    h => AuctionHouseClosed -= h);

        public IObservable<IReadOnlyList<AuctionData>> AuctionSearchResultsStream =>
            Observable.FromEvent<Action<IReadOnlyList<AuctionData>>, IReadOnlyList<AuctionData>>(
                h => results => h(results),
                h => AuctionSearchResults += h,
                h => AuctionSearchResults -= h);

        public IObservable<IReadOnlyList<AuctionData>> OwnedAuctionResultsStream =>
            Observable.FromEvent<Action<IReadOnlyList<AuctionData>>, IReadOnlyList<AuctionData>>(
                h => results => h(results),
                h => OwnedAuctionResults += h,
                h => OwnedAuctionResults -= h);

        public IObservable<IReadOnlyList<AuctionData>> BidderAuctionResultsStream =>
            Observable.FromEvent<Action<IReadOnlyList<AuctionData>>, IReadOnlyList<AuctionData>>(
                h => results => h(results),
                h => BidderAuctionResults += h,
                h => BidderAuctionResults -= h);

        public IObservable<AuctionOperationResult> AuctionOperationCompletedStream =>
            Observable.FromEvent<Action<AuctionOperationType, uint>, AuctionOperationResult>(
                h => (op, id) => h(new AuctionOperationResult(op, id)),
                h => AuctionOperationCompleted += h,
                h => AuctionOperationCompleted -= h);

        public IObservable<AuctionOperationError> AuctionOperationFailedStream =>
            Observable.FromEvent<Action<AuctionOperationType, string>, AuctionOperationError>(
                h => (op, err) => h(new AuctionOperationError(op, err)),
                h => AuctionOperationFailed += h,
                h => AuctionOperationFailed -= h);

        public IObservable<BidPlacedData> BidPlacedStream =>
            Observable.FromEvent<Action<uint, uint>, BidPlacedData>(
                h => (id, amount) => h(new BidPlacedData(id, amount)),
                h => BidPlaced += h,
                h => BidPlaced -= h);

        public IObservable<AuctionPostedData> AuctionPostedStream =>
            Observable.FromEvent<Action<uint, uint, uint, uint>, AuctionPostedData>(
                h => (itemId, startBid, buyout, duration) => h(new AuctionPostedData(itemId, startBid, buyout, duration)),
                h => AuctionPosted += h,
                h => AuctionPosted -= h);

        public IObservable<uint> AuctionCancelledStream =>
            Observable.FromEvent<Action<uint>, uint>(
                h => id => h(id),
                h => AuctionCancelled += h,
                h => AuctionCancelled -= h);

        public IObservable<AuctionNotificationData> AuctionNotificationStream =>
            Observable.FromEvent<Action<AuctionNotificationType, uint, uint>, AuctionNotificationData>(
                h => (type, auctionId, itemId) => h(new AuctionNotificationData(type, auctionId, itemId)),
                h => AuctionNotification += h,
                h => AuctionNotification -= h);
        #endregion

        /// <inheritdoc />
        public async Task OpenAuctionHouseAsync(ulong auctioneerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening auction house interaction with: {AuctioneerGuid:X}", auctioneerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.MSG_AUCTION_HELLO, payload, cancellationToken);

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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_LIST_ITEMS, payload, cancellationToken);

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
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS, [], cancellationToken);

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
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS, [], cancellationToken);

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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_PLACE_BID, payload, cancellationToken);

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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_SELL_ITEM, payload, cancellationToken);

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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_REMOVE_ITEM, payload, cancellationToken);

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

        #region Server Response Handlers
        public void HandleAuctionHouseOpened(ulong auctioneerGuid)
        {
            _isAuctionHouseOpen = true;
            _currentAuctioneerGuid = auctioneerGuid;
            AuctionHouseOpened?.Invoke(auctioneerGuid);
            _logger.LogDebug("Auction house opened for auctioneer: {AuctioneerGuid:X}", auctioneerGuid);
        }

        public void HandleAuctionSearchResults(IReadOnlyList<AuctionData> auctions)
        {
            AuctionSearchResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} auction search results", auctions.Count);
        }

        public void HandleOwnedAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            OwnedAuctionResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} owned auction results", auctions.Count);
        }

        public void HandleBidderAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            BidderAuctionResults?.Invoke(auctions);
            _logger.LogDebug("Received {Count} bidder auction results", auctions.Count);
        }

        public void HandleAuctionOperationCompleted(AuctionOperationType operation, uint auctionId)
        {
            AuctionOperationCompleted?.Invoke(operation, auctionId);
            _logger.LogDebug("Auction operation {Operation} completed for auction {AuctionId}", operation, auctionId);
        }

        public void HandleAuctionOperationFailed(AuctionOperationType operation, string errorReason)
        {
            AuctionOperationFailed?.Invoke(operation, errorReason);
            _logger.LogWarning("Auction operation {Operation} failed: {Error}", operation, errorReason);
        }

        public void HandleBidPlaced(uint auctionId, uint bidAmount)
        {
            BidPlaced?.Invoke(auctionId, bidAmount);
            _logger.LogDebug("Bid of {BidAmount} copper placed on auction {AuctionId}", bidAmount, auctionId);
        }

        public void HandleAuctionPosted(uint itemId, uint startBid, uint buyoutPrice, uint duration)
        {
            AuctionPosted?.Invoke(itemId, startBid, buyoutPrice, duration);
            _logger.LogDebug("Auction posted for item {ItemId} with start bid {StartBid}, buyout {BuyoutPrice}, duration {Duration}h",
                itemId, startBid, buyoutPrice, duration);
        }

        public void HandleAuctionCancelled(uint auctionId)
        {
            AuctionCancelled?.Invoke(auctionId);
            _logger.LogDebug("Auction {AuctionId} cancelled", auctionId);
        }

        public void HandleAuctionNotification(AuctionNotificationType notificationType, uint auctionId, uint itemId)
        {
            AuctionNotification?.Invoke(notificationType, auctionId, itemId);
            _logger.LogDebug("Auction notification: {NotificationType} for auction {AuctionId} (item {ItemId})",
                notificationType, auctionId, itemId);
        }
        #endregion

        #region Private Helper Methods

        private void SetOperationInProgress(bool inProgress)
        {
            lock (_stateLock)
            {
                _isOperationInProgress = inProgress;
                if (inProgress)
                {
                    _lastOperationTime = DateTime.UtcNow;
                }
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the auction house network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing AuctionHouseNetworkClientComponent");

            _disposed = true;
            _logger.LogDebug("AuctionHouseNetworkClientComponent disposed");
        }

        #endregion
    }
}