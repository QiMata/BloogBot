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
    /// Handles auction house operations using the MaNGOS 1.12.1 protocol.
    /// SMSG parsers: AUCTION_LIST_RESULT (68 bytes/entry), AUCTION_COMMAND_RESULT (conditional),
    /// AUCTION_REMOVED_NOTIFICATION (12 bytes), BIDDER_NOTIFICATION (32 bytes), OWNER_NOTIFICATION (28 bytes).
    /// CMSG formats: All include auctioneerGuid prefix as required by the server.
    /// </summary>
    public class AuctionHouseNetworkClientComponent : NetworkClientComponent, IAuctionHouseNetworkClientComponent, IDisposable
    {
        private const int AUCTION_ENTRY_SIZE = 64;

        private readonly IWorldClient _worldClient;
        private readonly ILogger<AuctionHouseNetworkClientComponent> _logger;

        private bool _isAuctionHouseOpen;
        private ulong? _currentAuctioneerGuid;
        private readonly List<AuctionData> _currentAuctions = [];
        private readonly List<AuctionData> _ownedAuctions = [];
        private readonly List<AuctionData> _bidderAuctions = [];
        private uint _totalSearchResultCount;
        private bool _disposed;

        public AuctionHouseNetworkClientComponent(IWorldClient worldClient, ILogger<AuctionHouseNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsAuctionHouseOpen => _isAuctionHouseOpen;

        /// <inheritdoc />
        public uint TotalSearchResultCount => _totalSearchResultCount;

        /// <summary>
        /// Gets the most recent auction search results captured by the component.
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
                .Where(c => c.Error == AuctionError.Ok)
                .Select(c => new AuctionOperationResult(c.Action, c.AuctionId))
        );

        public IObservable<AuctionOperationError> AuctionOperationFailedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Error != AuctionError.Ok)
                .Select(c => new AuctionOperationError(c.Action, c.Error, c.Error.ToString()))
        );

        public IObservable<BidPlacedData> BidPlacedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Error == AuctionError.Ok && c.Action == AuctionAction.BidPlaced)
                .Select(c => new BidPlacedData(c.AuctionId, c.OutbidAmount))
        );

        public IObservable<AuctionPostedData> AuctionPostedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_COMMAND_RESULT)
                .Select(ParseCommandResult)
                .Where(c => c.Error == AuctionError.Ok && c.Action == AuctionAction.Started)
                .Select(c => new AuctionPostedData(c.AuctionId))
        );

        public IObservable<AuctionNotificationData> AuctionRemovedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_AUCTION_REMOVED_NOTIFICATION)
                .Select(ParseRemovedNotification)
                .Where(n => n.AuctionId != 0)
        );

        public IObservable<AuctionNotificationData> AuctionNotificationStream => Observable.Defer(() =>
            Observable.Merge(
                SafeStream(Opcode.SMSG_AUCTION_BIDDER_NOTIFICATION).Select(ParseBidderNotification),
                SafeStream(Opcode.SMSG_AUCTION_OWNER_NOTIFICATION).Select(ParseOwnerNotification)
            )
        );
        #endregion

        #region CMSG Methods

        /// <inheritdoc />
        public async Task OpenAuctionHouseAsync(ulong auctioneerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening auction house with auctioneer: {AuctioneerGuid:X}", auctioneerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);

                _currentAuctioneerGuid = auctioneerGuid;
                _isAuctionHouseOpen = true;

                await _worldClient.SendOpcodeAsync(Opcode.MSG_AUCTION_HELLO, payload, cancellationToken);

                _logger.LogInformation("Auction house opened with auctioneer: {AuctioneerGuid:X}", auctioneerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open auction house with: {AuctioneerGuid:X}", auctioneerGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseAuctionHouseAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Closing auction house window");
            _isAuctionHouseOpen = false;
            _currentAuctioneerGuid = null;
            _logger.LogInformation("Auction house window closed");
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task SearchAuctionsAsync(
            string name = "",
            byte levelMin = 0,
            byte levelMax = 0,
            uint inventoryType = 0,
            uint itemClass = 0,
            uint itemSubClass = 0,
            AuctionQuality quality = AuctionQuality.Any,
            bool usable = false,
            uint listOffset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Searching auctions: Name='{Name}', Level={LevelMin}-{LevelMax}, Class={ItemClass}, SubClass={ItemSubClass}, Quality={Quality}",
                    name, levelMin, levelMax, itemClass, itemSubClass, quality);

                // CMSG_AUCTION_LIST_ITEMS format (MaNGOS):
                // auctioneerGuid(8) + listfrom(4) + name(null-term) + levelmin(1) + levelmax(1)
                // + auctionSlotID(4) + auctionMainCategory(4) + auctionSubCategory(4) + quality(4) + usable(1)
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
                var payload = new byte[8 + 4 + nameBytes.Length + 1 + 1 + 1 + 4 + 4 + 4 + 4 + 1];
                var offset = 0;

                // Auctioneer GUID
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, offset);
                offset += 8;

                // List offset (pagination)
                BitConverter.GetBytes(listOffset).CopyTo(payload, offset);
                offset += 4;

                // Search name (null-terminated)
                nameBytes.CopyTo(payload, offset);
                offset += nameBytes.Length;
                payload[offset++] = 0;

                // Level range (uint8 each)
                payload[offset++] = levelMin;
                payload[offset++] = levelMax;

                // Inventory type (auctionSlotID)
                BitConverter.GetBytes(inventoryType).CopyTo(payload, offset);
                offset += 4;

                // Item class (auctionMainCategory)
                BitConverter.GetBytes(itemClass).CopyTo(payload, offset);
                offset += 4;

                // Item subclass (auctionSubCategory)
                BitConverter.GetBytes(itemSubClass).CopyTo(payload, offset);
                offset += 4;

                // Quality (int32, -1 = any)
                BitConverter.GetBytes((int)quality).CopyTo(payload, offset);
                offset += 4;

                // Usable (uint8)
                payload[offset] = usable ? (byte)1 : (byte)0;

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
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Requesting owned auctions");

                // CMSG_AUCTION_LIST_OWNER_ITEMS: auctioneerGuid(8) + listfrom(4)
                var payload = new byte[12];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);
                // listfrom = 0 (first page)

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_LIST_OWNER_ITEMS, payload, cancellationToken);

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
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Requesting bidder auctions");

                // CMSG_AUCTION_LIST_BIDDER_ITEMS: auctioneerGuid(8) + listfrom(4) + outbiddedCount(4) + [outbiddedAuctionIds]
                var payload = new byte[16];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);
                // listfrom = 0, outbiddedCount = 0

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_LIST_BIDDER_ITEMS, payload, cancellationToken);

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
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Placing bid of {BidAmount} copper on auction {AuctionId}", bidAmount, auctionId);

                // CMSG_AUCTION_PLACE_BID: auctioneerGuid(8) + auctionId(4) + price(4)
                var payload = new byte[16];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(auctionId).CopyTo(payload, 8);
                BitConverter.GetBytes(bidAmount).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_PLACE_BID, payload, cancellationToken);

                _logger.LogInformation("Bid placed: {BidAmount} copper on auction {AuctionId}", bidAmount, auctionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to place bid on auction {AuctionId}", auctionId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PostAuctionAsync(
            ulong itemGuid,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Posting auction for item {ItemGuid:X} bid={StartBid} buyout={BuyoutPrice} duration={Duration}h",
                    itemGuid, startBid, buyoutPrice, (uint)duration);

                // CMSG_AUCTION_SELL_ITEM: auctioneerGuid(8) + itemGuid(8) + bid(4) + buyout(4) + etime(4)
                // etime is in minutes: 720 (12h), 1440 (24h), 2880 (48h)
                var payload = new byte[28];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(itemGuid).CopyTo(payload, 8);
                BitConverter.GetBytes(startBid).CopyTo(payload, 16);
                BitConverter.GetBytes(buyoutPrice).CopyTo(payload, 20);
                BitConverter.GetBytes((uint)duration * 60).CopyTo(payload, 24); // hours → minutes

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_SELL_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auction posted for item {ItemGuid:X}", itemGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post auction for item {ItemGuid:X}", itemGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CancelAuctionAsync(uint auctionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var auctioneerGuid = _currentAuctioneerGuid
                    ?? throw new InvalidOperationException("Auction house is not open");

                _logger.LogDebug("Cancelling auction {AuctionId}", auctionId);

                // CMSG_AUCTION_REMOVE_ITEM: auctioneerGuid(8) + auctionId(4)
                var payload = new byte[12];
                BitConverter.GetBytes(auctioneerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(auctionId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUCTION_REMOVE_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auction {AuctionId} cancellation request sent", auctionId);
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
                _logger.LogDebug("Quick search for '{ItemName}' with auctioneer: {AuctioneerGuid:X}", itemName, auctioneerGuid);
                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await SearchAuctionsAsync(itemName, cancellationToken: cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseAuctionHouseAsync(cancellationToken);
                _logger.LogInformation("Quick search completed for '{ItemName}'", itemName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick search failed for '{ItemName}'", itemName);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickPostAsync(
            ulong auctioneerGuid,
            ulong itemGuid,
            uint startBid,
            uint buyoutPrice,
            AuctionDuration duration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Quick post for item {ItemGuid:X} with auctioneer: {AuctioneerGuid:X}",
                    itemGuid, auctioneerGuid);
                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await PostAuctionAsync(itemGuid, startBid, buyoutPrice, duration, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseAuctionHouseAsync(cancellationToken);
                _logger.LogInformation("Quick post completed for item {ItemGuid:X}", itemGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick post failed for item {ItemGuid:X}", itemGuid);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickBuyoutAsync(ulong auctioneerGuid, uint auctionId, uint buyoutPrice, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Quick buyout for auction {AuctionId} with auctioneer: {AuctioneerGuid:X}",
                    auctionId, auctioneerGuid);
                await OpenAuctionHouseAsync(auctioneerGuid, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await BuyoutAuctionAsync(auctionId, buyoutPrice, cancellationToken);
                await Task.Delay(100, cancellationToken);
                await CloseAuctionHouseAsync(cancellationToken);
                _logger.LogInformation("Quick buyout completed for auction {AuctionId}", auctionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick buyout failed for auction {AuctionId}", auctionId);
                throw;
            }
        }

        #endregion

        #region Server Response Handlers (state updates only)
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

        public void HandleAuctionOperationCompleted(AuctionAction action, uint auctionId)
        {
            _logger.LogDebug("Auction operation {Action} completed for auction {AuctionId}", action, auctionId);
        }

        public void HandleAuctionOperationFailed(AuctionAction action, AuctionError error)
        {
            _logger.LogWarning("Auction operation {Action} failed: {Error}", action, error);
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

        #region SMSG Parsers

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

        /// <summary>
        /// Parses SMSG_AUCTION_LIST_RESULT, SMSG_AUCTION_OWNER_LIST_RESULT, or SMSG_AUCTION_BIDDER_LIST_RESULT.
        /// Format: itemCount(4) + [itemCount × 68-byte BuildAuctionInfo entries] + totalCount(4).
        /// </summary>
        private IReadOnlyList<AuctionData> ParseAuctionList(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            if (span.Length < 4)
            {
                _logger.LogWarning("Auction list payload too small: {Length} bytes", span.Length);
                return Array.Empty<AuctionData>();
            }

            uint itemCount = ReadUInt32(span, 0);
            int dataStart = 4;
            int expectedDataSize = dataStart + (int)(itemCount * AUCTION_ENTRY_SIZE) + 4; // +4 for totalCount at end

            if (span.Length < dataStart + (int)(itemCount * AUCTION_ENTRY_SIZE))
            {
                _logger.LogWarning("Auction list payload too small for {ItemCount} entries: {Length} bytes (expected >= {Expected})",
                    itemCount, span.Length, dataStart + itemCount * AUCTION_ENTRY_SIZE);
                return Array.Empty<AuctionData>();
            }

            var auctions = new AuctionData[itemCount];
            int offset = dataStart;

            for (int i = 0; i < itemCount; i++)
            {
                // BuildAuctionInfo per-entry format (68 bytes):
                uint auctionId = ReadUInt32(span, offset);           // 0
                uint itemEntry = ReadUInt32(span, offset + 4);       // 4
                uint enchantmentId = ReadUInt32(span, offset + 8);   // 8  (PERM_ENCHANTMENT_SLOT)
                uint randomPropertyId = ReadUInt32(span, offset + 12); // 12
                uint suffixFactor = ReadUInt32(span, offset + 16);   // 16
                uint count = ReadUInt32(span, offset + 20);          // 20 (stack size)
                uint spellCharges = ReadUInt32(span, offset + 24);   // 24
                ulong ownerGuid = ReadUInt64(span, offset + 28);     // 28
                uint startBid = ReadUInt32(span, offset + 36);       // 36
                uint minOutbid = ReadUInt32(span, offset + 40);      // 40
                uint buyoutPrice = ReadUInt32(span, offset + 44);    // 44
                uint timeLeftMs = ReadUInt32(span, offset + 48);     // 48 (milliseconds remaining)
                ulong bidderGuid = ReadUInt64(span, offset + 52);    // 52
                uint currentBid = ReadUInt32(span, offset + 60);     // 60

                auctions[i] = new AuctionData(
                    auctionId, itemEntry, enchantmentId, randomPropertyId, suffixFactor,
                    count, spellCharges, ownerGuid, startBid, minOutbid,
                    buyoutPrice, timeLeftMs, bidderGuid, currentBid);

                offset += AUCTION_ENTRY_SIZE;
            }

            // Read totalCount at end of payload
            if (span.Length >= offset + 4)
            {
                _totalSearchResultCount = ReadUInt32(span, offset);
            }

            _logger.LogInformation("Parsed {ItemCount}/{TotalCount} auctions from list result",
                itemCount, _totalSearchResultCount);

            return auctions;
        }

        /// <summary>
        /// Parses SMSG_AUCTION_COMMAND_RESULT.
        /// Format: auctionId(4) + action(4) + error(4) + [conditional fields based on action and error].
        /// </summary>
        private (AuctionAction Action, uint AuctionId, AuctionError Error, uint OutbidAmount)
            ParseCommandResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            if (span.Length < 12)
            {
                _logger.LogWarning("Auction command result payload too small: {Length} bytes", span.Length);
                return (AuctionAction.Started, 0, AuctionError.InternalError, 0);
            }

            uint auctionId = ReadUInt32(span, 0);
            var action = (AuctionAction)ReadUInt32(span, 4);
            var error = (AuctionError)ReadUInt32(span, 8);

            uint outbidAmount = 0;

            if (error == AuctionError.Ok && action == AuctionAction.BidPlaced)
            {
                // On successful bid: uint32 outbid increment follows
                if (span.Length >= 16)
                    outbidAmount = ReadUInt32(span, 12);
            }
            else if (error == AuctionError.HigherBid)
            {
                // On higher bid error: bidderGuid(8) + bidAmount(4) + outbidAmount(4)
                if (span.Length >= 28)
                    outbidAmount = ReadUInt32(span, 24); // skip bidderGuid(8) + bidAmount(4)
            }

            _logger.LogDebug("Auction command: action={Action}, auctionId={AuctionId}, error={Error}",
                action, auctionId, error);

            return (action, auctionId, error, outbidAmount);
        }

        /// <summary>
        /// Parses SMSG_AUCTION_REMOVED_NOTIFICATION.
        /// Format: auctionId(4) + itemEntry(4) + randomPropertyId(4) = 12 bytes.
        /// </summary>
        private AuctionNotificationData ParseRemovedNotification(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            if (span.Length < 12)
            {
                _logger.LogWarning("Auction removed notification too small: {Length} bytes", span.Length);
                return new AuctionNotificationData(AuctionNotificationType.Expired, 0, 0, 0);
            }

            uint auctionId = ReadUInt32(span, 0);
            uint itemEntry = ReadUInt32(span, 4);
            uint randomPropertyId = ReadUInt32(span, 8);

            _logger.LogDebug("Auction removed: id={AuctionId}, item={ItemEntry}", auctionId, itemEntry);

            return new AuctionNotificationData(AuctionNotificationType.Expired, auctionId, itemEntry, randomPropertyId);
        }

        /// <summary>
        /// Parses SMSG_AUCTION_BIDDER_NOTIFICATION (32 bytes).
        /// Format: houseId(4) + auctionId(4) + bidderGuid(8) + outbidAmount(4) + minOutbid(4) + itemEntry(4) + randomPropertyId(4).
        /// </summary>
        private AuctionNotificationData ParseBidderNotification(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            if (span.Length < 32)
            {
                _logger.LogWarning("Bidder notification too small: {Length} bytes", span.Length);
                return new AuctionNotificationData(AuctionNotificationType.Outbid, 0, 0, 0);
            }

            uint houseId = ReadUInt32(span, 0);
            uint auctionId = ReadUInt32(span, 4);
            ulong bidderGuid = ReadUInt64(span, 8);
            uint outbidAmount = ReadUInt32(span, 16);
            uint minOutbid = ReadUInt32(span, 20);
            uint itemEntry = ReadUInt32(span, 24);
            uint randomPropertyId = ReadUInt32(span, 28);

            // outbidAmount == 0 means we won the auction
            var type = outbidAmount == 0 ? AuctionNotificationType.Won : AuctionNotificationType.Outbid;

            _logger.LogDebug("Bidder notification: {Type} auction={AuctionId} item={ItemEntry}", type, auctionId, itemEntry);

            return new AuctionBidderNotificationData(
                houseId, auctionId, bidderGuid, outbidAmount, minOutbid, itemEntry, randomPropertyId);
        }

        /// <summary>
        /// Parses SMSG_AUCTION_OWNER_NOTIFICATION (28 bytes).
        /// Format: auctionId(4) + bidAmount(4) + minOutbid(4) + buyerGuid(8) + itemEntry(4) + randomPropertyId(4).
        /// bidAmount == 0 means auction expired; bidAmount > 0 means sold.
        /// </summary>
        private AuctionNotificationData ParseOwnerNotification(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;

            if (span.Length < 28)
            {
                _logger.LogWarning("Owner notification too small: {Length} bytes", span.Length);
                return new AuctionNotificationData(AuctionNotificationType.Expired, 0, 0, 0);
            }

            uint auctionId = ReadUInt32(span, 0);
            uint bidAmount = ReadUInt32(span, 4);
            uint minOutbid = ReadUInt32(span, 8);
            ulong buyerGuid = ReadUInt64(span, 12);
            uint itemEntry = ReadUInt32(span, 20);
            uint randomPropertyId = ReadUInt32(span, 24);

            var type = bidAmount > 0 ? AuctionNotificationType.Sold : AuctionNotificationType.Expired;

            _logger.LogDebug("Owner notification: {Type} auction={AuctionId} item={ItemEntry}", type, auctionId, itemEntry);

            return new AuctionOwnerNotificationData(
                auctionId, bidAmount, minOutbid, buyerGuid, itemEntry, randomPropertyId);
        }

        #endregion
    }
}
