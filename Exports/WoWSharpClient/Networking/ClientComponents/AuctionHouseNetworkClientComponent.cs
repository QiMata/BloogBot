using System.Buffers.Binary;
using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of auction house network agent that handles auction house operations in World of Warcraft.
    /// Manages browsing auctions, placing bids, posting auctions, and collecting sold items using the Mangos protocol.
    /// Uses opcode-driven observables instead of subjects.
    /// </summary>
    public class AuctionHouseNetworkClientComponent : NetworkClientComponent, IAuctionHouseNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<AuctionHouseNetworkClientComponent> _logger;

        private bool _isAuctionHouseOpen;
        private ulong? _currentAuctioneerGuid;
        private readonly List<AuctionData> _currentAuctions = [];
        private readonly List<AuctionData> _ownedAuctions = [];
        private readonly List<AuctionData> _bidderAuctions = [];
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

        /// <inheritdoc />
        public bool IsAuctionHouseOpen => _isAuctionHouseOpen;

        /// <summary>
        /// Gets the most recent auction search results captured by the component.
        /// Exposed for tests and read-only consumers.
        /// </summary>
        public IReadOnlyList<AuctionData> CurrentAuctions => _currentAuctions;

        #region Reactive Observables (lazy, opcode-driven)
        public IObservable<ulong> AuctionHouseOpenedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_LIST_RESULT)
                .Select(_ => _currentAuctioneerGuid)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
        );

        public IObservable<Unit> AuctionHouseClosedStream => Observable.Defer(() =>
            (_worldClient.WhenDisconnected ?? Observable.Empty<Exception?>()).Select(_ => Unit.Default)
        );

        public IObservable<IReadOnlyList<AuctionData>> AuctionSearchResultsStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_LIST_RESULT)
                .Select(ParseAuctionList)
                .Do(list => { _currentAuctions.Clear(); _currentAuctions.AddRange(list); })
        );

        public IObservable<IReadOnlyList<AuctionData>> OwnedAuctionResultsStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_OWNER_LIST_RESULT)
                .Select(ParseAuctionList)
                .Do(list => { _ownedAuctions.Clear(); _ownedAuctions.AddRange(list); })
        );

        public IObservable<IReadOnlyList<AuctionData>> BidderAuctionResultsStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_BIDDER_LIST_RESULT)
                .Select(ParseAuctionList)
                .Do(list => { _bidderAuctions.Clear(); _bidderAuctions.AddRange(list); })
        );

        public IObservable<AuctionOperationResult> AuctionOperationCompletedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Success)
                .Select(c => new AuctionOperationResult(c.Operation, c.AuctionId))
        );

        public IObservable<AuctionOperationError> AuctionOperationFailedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => !c.Success)
                .Select(c => new AuctionOperationError(c.Operation, c.Error ?? "Unknown error"))
        );

        public IObservable<BidPlacedData> BidPlacedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Success && c.Operation == AuctionOperationType.PlaceBid)
                .Select(c => new BidPlacedData(c.AuctionId, c.BidAmount ?? 0))
        );

        public IObservable<AuctionPostedData> AuctionPostedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Success && c.Operation == AuctionOperationType.PostAuction)
                .Select(c => new AuctionPostedData(c.ItemId ?? 0, c.StartBid ?? 0, c.Buyout ?? 0, c.Duration ?? 0))
        );

        public IObservable<uint> AuctionCancelledStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_REMOVED_NOTIFICATION)
                .Select(ParseRemovedNotification)
                .Where(id => id != 0)
        );

        public IObservable<AuctionNotificationData> AuctionNotificationStream => Observable.Defer(() =>
            Observable.Merge(
                SafeStream(Opcode.SMSG_AUCTION_BIDDER_NOTIFICATION).Select(payload => ParseNotification(payload, isOwner: false)),
                SafeStream(Opcode.SMSG_AUCTION_OWNER_NOTIFICATION).Select(payload => ParseNotification(payload, isOwner: true))
            )
        );
        #endregion

        /// <inheritdoc />
        public async Task OpenAuctionHouseAsync(ulong auctioneerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening auction house interaction with: {AuctioneerGuid:X}", auctioneerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);

                _currentAuctioneerGuid = auctioneerGuid;
                _isAuctionHouseOpen = true;

                await _worldClient.SendOpcodeAsync(Opcode.MSG_AUCTION_HELLO, payload, cancellationToken);

                _logger.LogInformation("Auction house interaction initiated with: {AuctioneerGuid:X}", auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open auction house interaction with: {AuctioneerGuid:X}", auctioneerGuid);
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

                _logger.LogInformation("Auction house window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close auction house window");
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
                throw;
            }
        }

        #region Server Response Handlers (optional, state updates only)
        public void HandleAuctionHouseOpened(ulong auctioneerGuid)
        {
            _isAuctionHouseOpen = true;
            _currentAuctioneerGuid = auctioneerGuid;
            _logger.LogDebug("Auction house opened for auctioneer: {AuctioneerGuid:X}", auctioneerGuid);
        }

        public void HandleAuctionSearchResults(IReadOnlyList<AuctionData> auctions)
        {
            _currentAuctions.Clear();
            _currentAuctions.AddRange(auctions);
            _logger.LogDebug("Received {Count} auction search results", auctions.Count);
        }

        public void HandleOwnedAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            _ownedAuctions.Clear();
            _ownedAuctions.AddRange(auctions);
            _logger.LogDebug("Received {Count} owned auction results", auctions.Count);
        }

        public void HandleBidderAuctionResults(IReadOnlyList<AuctionData> auctions)
        {
            _bidderAuctions.Clear();
            _bidderAuctions.AddRange(auctions);
            _logger.LogDebug("Received {Count} bidder auction results", auctions.Count);
        }

        public void HandleAuctionOperationCompleted(AuctionOperationType operation, uint auctionId)
        {
            _logger.LogDebug("Auction operation {Operation} completed for auction {AuctionId}", operation, auctionId);
        }

        public void HandleAuctionOperationFailed(AuctionOperationType operation, string errorReason)
        {
            _logger.LogWarning("Auction operation {Operation} failed: {Error}", operation, errorReason);
        }

        public void HandleBidPlaced(uint auctionId, uint bidAmount)
        {
            _logger.LogDebug("Bid of {BidAmount} copper placed on auction {AuctionId}", bidAmount, auctionId);
        }

        public void HandleAuctionPosted(uint itemId, uint startBid, uint buyoutPrice, uint duration)
        {
            _logger.LogDebug("Auction posted for item {ItemId} with start bid {StartBid}, buyout {BuyoutPrice}, duration {Duration}h",
                itemId, startBid, buyoutPrice, duration);
        }

        public void HandleAuctionCancelled(uint auctionId)
        {
            _logger.LogDebug("Auction {AuctionId} cancelled", auctionId);
        }

        public void HandleAuctionNotification(AuctionNotificationType notificationType, uint auctionId, uint itemId)
        {
            _logger.LogDebug("Auction notification: {NotificationType} for auction {AuctionId} (item {ItemId})",
                notificationType, auctionId, itemId);
        }
        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("AuctionHouseNetworkClientComponent disposed");
        }

        #endregion

        #region Parsing helpers and SafeStream
        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private static uint ReadUInt32(ReadOnlySpan<byte> span, int offset)
        {
            return span.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) : 0u;
        }

        private static ulong ReadUInt64(ReadOnlySpan<byte> span, int offset)
        {
            return span.Length >= offset + 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)) : 0UL;
        }

        private IReadOnlyList<AuctionData> ParseAuctionList(ReadOnlyMemory<byte> payload)
        {
            // Best-effort parsing: protocol specifics are not guaranteed here; return empty list if unknown.
            // Integrators can replace with real parsing logic later.
            return Array.Empty<AuctionData>();
        }

        private (bool Success, AuctionOperationType Operation, uint AuctionId, string? Error, uint? BidAmount, uint? ItemId, uint? StartBid, uint? Buyout, uint? Duration) ParseCommandResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            // Heuristic layout: [result(1)] [operation(4)] [auctionId(4)] [optional details...]
            bool success = span.Length > 0 && span[0] == 0;
            var op = (AuctionOperationType)ReadUInt32(span, 1);
            uint auctionId = ReadUInt32(span, 5);
            // Optional values (best-effort)
            uint? bid = span.Length >= 13 ? ReadUInt32(span, 9) : null;
            // No error string parsing available -> null
            return (success, op, auctionId, null, bid, null, null, null, null);
        }

        private uint ParseRemovedNotification(ReadOnlyMemory<byte> payload)
        {
            // Heuristic: first 4 bytes may be auctionId
            return ReadUInt32(payload.Span, 0);
        }

        private AuctionNotificationData ParseNotification(ReadOnlyMemory<byte> payload, bool isOwner)
        {
            // Heuristic mapping: read minimal fields
            var span = payload.Span;
            uint auctionId = ReadUInt32(span, 0);
            uint itemId = ReadUInt32(span, 4);
            var type = isOwner ? AuctionNotificationType.Sold : AuctionNotificationType.Outbid;
            return new AuctionNotificationData(type, auctionId, itemId);
        }
        #endregion
    }
}