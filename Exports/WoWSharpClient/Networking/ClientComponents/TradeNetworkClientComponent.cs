using System.Buffers.Binary;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Trade window slot indices for item placement (0-5 tradeable, 6 non-traded display).
    /// </summary>
    public enum TradeSlots
    {
        TradeSlot1 = 0,
        TradeSlot2 = 1,
        TradeSlot3 = 2,
        TradeSlot4 = 3,
        TradeSlot5 = 4,
        TradeSlot6 = 5,
        NonTraded = 6
    }

    public class TradeNetworkClientComponent : NetworkClientComponent, ITradeNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TradeNetworkClientComponent> _logger;

        private bool _isTradeWindowOpen;
        private ulong? _tradePartnerGuid;
        private uint _ourMoneyOffer;
        private bool _weAccepted;
        private bool _theyAccepted;
        private bool _disposed;
        private TradeWindowData? _traderWindowData;
        private TradeWindowData? _ownWindowData;

        private readonly Subject<(int TradeWindowSlot, TradeSlots Slot)> _localTradeItemSlotChanged = new();
        private readonly Subject<uint> _localMoneyOfferedChanged = new();
        private readonly Subject<(string Operation, string Error)> _localTradeErrors = new();

        private readonly IObservable<ulong> _tradeRequests;
        private readonly IObservable<Unit> _tradesOpened;
        private readonly IObservable<Unit> _tradesClosed;
        private readonly IObservable<uint> _offeredMoneyChanges;
        private readonly IObservable<(int TradeWindowSlot, TradeSlots Slot)> _tradeItemSlotsChanged;
        private readonly IObservable<(string Operation, string Error)> _tradeErrors;
        private readonly IObservable<TradeWindowData> _tradeWindowUpdates;

        public TradeNetworkClientComponent(IWorldClient worldClient, ILogger<TradeNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var statusStream = SafeOpcodeStream(Opcode.SMSG_TRADE_STATUS);
            var extendedStream = SafeOpcodeStream(Opcode.SMSG_TRADE_STATUS_EXTENDED);

            _tradeRequests = statusStream
                .Select(ParseTradeRequestGuid)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .Do(guid =>
                {
                    _tradePartnerGuid = guid;
                    TradeRequested?.Invoke(guid);
                    _logger.LogDebug("Trade requested by player: {TraderGuid:X}", guid);
                })
                .Publish()
                .RefCount();

            _tradesOpened = statusStream.Where(IsOpenStatus)
                .Select(_ => Unit.Default)
                .Do(_ =>
                {
                    _isTradeWindowOpen = true;
                    TradeOpened?.Invoke();
                    _logger.LogDebug("Trade window opened");
                })
                .Publish()
                .RefCount();

            _tradesClosed = statusStream.Where(IsCloseOrCompleteStatus)
                .Select(_ => Unit.Default)
                .Do(_ =>
                {
                    ResetTradeState();
                    TradeClosed?.Invoke();
                    _logger.LogDebug("Trade window closed");
                })
                .Publish()
                .RefCount();

            _offeredMoneyChanges = _localMoneyOfferedChanged
                .Do(copper =>
                {
                    _ourMoneyOffer = copper;
                    MoneyOfferedChanged?.Invoke(_ourMoneyOffer);
                    _logger.LogDebug("Money offer changed to: {Copper} copper", copper);
                })
                .Publish()
                .RefCount();

            _tradeWindowUpdates = extendedStream
                .Select(ParseTradeStatusExtended)
                .Where(d => d != null)
                .Select(d => d!)
                .Do(data =>
                {
                    if (data.IsTraderView)
                        _traderWindowData = data;
                    else
                        _ownWindowData = data;
                    TradeWindowUpdated?.Invoke(data);
                    _logger.LogDebug("Trade window updated (trader={IsTrader}, gold={Gold})", data.IsTraderView, data.Gold);
                })
                .Publish()
                .RefCount();

            _tradeItemSlotsChanged = _localTradeItemSlotChanged
                .Do(tuple =>
                {
                    TradeItemSlotChanged?.Invoke(tuple.TradeWindowSlot, tuple.Slot);
                    _logger.LogDebug("Trade item slot changed for slot {Slot}", tuple.TradeWindowSlot);
                })
                .Publish()
                .RefCount();

            _tradeErrors = _localTradeErrors
                .Do(err =>
                {
                    TradeOperationFailed?.Invoke(err.Operation, err.Error);
                    _logger.LogWarning("Trade operation failed: {Operation} - {Error}", err.Operation, err.Error);
                })
                .Publish()
                .RefCount();
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #region SMSG Parsers

        /// <summary>
        /// SMSG_TRADE_STATUS: uint32 status code. BEGIN_TRADE (1) has extra ObjectGuid (8 bytes).
        /// </summary>
        private static bool IsOpenStatus(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length < 4) return false;
            var status = (TradeStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload.Span[..4]);
            return status == TradeStatus.OpenWindow;
        }

        private static bool IsCloseOrCompleteStatus(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length < 4) return false;
            var status = (TradeStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload.Span[..4]);
            return status == TradeStatus.TradeCanceled
                || status == TradeStatus.TradeComplete
                || status == TradeStatus.CloseWindow;
        }

        /// <summary>
        /// Parses BEGIN_TRADE status: uint32(1) + ObjectGuid(8).
        /// </summary>
        private static ulong? ParseTradeRequestGuid(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length < 12) return null;
            var span = payload.Span;
            var status = (TradeStatus)BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);
            if (status != TradeStatus.BeginTrade) return null;
            return BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(4, 8));
        }

        /// <summary>
        /// Parses SMSG_TRADE_STATUS_EXTENDED:
        /// uint8 traderState + uint32 slotCount + uint32 slotCount + uint32 gold + uint32 spellId
        /// + 7 x [uint8 slotIndex + (item data: 13 fields totaling 60 bytes OR 15 x uint32 zeros)]
        /// </summary>
        public static TradeWindowData? ParseTradeStatusExtended(ReadOnlyMemory<byte> payload)
        {
            // Minimum: header(17) + 7 slots * (1 + 60) = 17 + 427 = 444 bytes
            if (payload.Length < 17) return null;

            var span = payload.Span;
            int offset = 0;

            byte traderState = span[offset]; offset += 1;
            uint slotCount1 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            uint slotCount2 = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            uint gold = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            uint spellId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

            int count = (int)Math.Min(slotCount1, 7u);
            var items = new TradeItemInfo[count];

            for (int i = 0; i < count && offset + 61 <= span.Length; i++)
            {
                byte slotIndex = span[offset]; offset += 1;

                uint itemEntry = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint displayId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint stackCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint wrapped = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                ulong giftCreator = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
                uint enchantId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                ulong creator = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
                int charges = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint suffixFactor = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint randomProp = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint lockId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint maxDurability = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                uint durability = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

                items[i] = new TradeItemInfo(
                    slotIndex, itemEntry, displayId, stackCount,
                    wrapped != 0, giftCreator, enchantId, creator,
                    charges, suffixFactor, randomProp, lockId,
                    maxDurability, durability
                );
            }

            return new TradeWindowData
            {
                IsTraderView = traderState != 0,
                Gold = gold,
                SpellId = spellId,
                Items = items
            };
        }

        /// <summary>
        /// Gets the TradeStatus from a SMSG_TRADE_STATUS payload.
        /// </summary>
        public static TradeStatus? ParseTradeStatusCode(ReadOnlyMemory<byte> payload)
        {
            if (payload.Length < 4) return null;
            return (TradeStatus)BinaryPrimitives.ReadUInt32LittleEndian(payload.Span[..4]);
        }

        #endregion

        #region State Properties

        public bool IsTradeOpen => _isTradeWindowOpen;
        public ulong? TradingWithGuid => _tradePartnerGuid;
        public uint OfferedCopper => _ourMoneyOffer;
        public TradeWindowData? TraderWindowData => _traderWindowData;
        public TradeWindowData? OwnWindowData => _ownWindowData;

        public event Action<ulong>? TradeRequested;
        public event Action? TradeOpened;
        public event Action? TradeClosed;
        public event Action<uint>? MoneyOfferedChanged;
        public event Action<int, TradeSlots>? TradeItemSlotChanged;
        public event Action<string, string>? TradeOperationFailed;
        public event Action<TradeWindowData>? TradeWindowUpdated;

        public IObservable<ulong> TradeRequests => _tradeRequests;
        public IObservable<Unit> TradesOpened => _tradesOpened;
        public IObservable<Unit> TradesClosed => _tradesClosed;
        public IObservable<uint> OfferedMoneyChanges => _offeredMoneyChanges;
        public IObservable<(int TradeWindowSlot, TradeSlots Slot)> TradeItemSlotsChanged => _tradeItemSlotsChanged;
        public IObservable<(string Operation, string Error)> TradeErrors => _tradeErrors;
        public IObservable<TradeWindowData> TradeWindowUpdates => _tradeWindowUpdates;

        #endregion

        #region CMSG Operations

        /// <summary>CMSG_INITIATE_TRADE: ObjectGuid(8).</summary>
        public async Task InitiateTradeAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Initiating trade with player: {PlayerGuid:X}", playerGuid);

                var payload = BitConverter.GetBytes(playerGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_INITIATE_TRADE, payload, cancellationToken);

                _tradePartnerGuid = playerGuid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate trade with player: {PlayerGuid:X}", playerGuid);
                _localTradeErrors.OnNext(("InitiateTrade", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>CMSG_ACCEPT_TRADE: uint32 unknown (4 bytes, skipped by server).</summary>
        public async Task AcceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Accepting trade");

                // MaNGOS reads and skips a uint32 field
                var payload = new byte[4]; // zeros
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ACCEPT_TRADE, payload, cancellationToken);

                _weAccepted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept trade");
                _localTradeErrors.OnNext(("AcceptTrade", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>CMSG_UNACCEPT_TRADE: empty payload.</summary>
        public async Task UnacceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_UNACCEPT_TRADE, Array.Empty<byte>(), cancellationToken);
                _weAccepted = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unaccept trade");
                _localTradeErrors.OnNext(("UnacceptTrade", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>CMSG_CANCEL_TRADE: empty payload.</summary>
        public async Task CancelTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_TRADE, Array.Empty<byte>(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel trade");
                _localTradeErrors.OnNext(("CancelTrade", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>CMSG_SET_TRADE_GOLD: uint32 copper (4 bytes).</summary>
        public async Task OfferMoneyAsync(uint copper, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);

                var payload = BitConverter.GetBytes(copper);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_GOLD, payload, cancellationToken);

                _ourMoneyOffer = copper;
                _localMoneyOfferedChanged.OnNext(_ourMoneyOffer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer money in trade: {Copper} copper", copper);
                _localTradeErrors.OnNext(("OfferMoney", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// CMSG_SET_TRADE_ITEM: tradeSlot(1) + bag(1) + slot(1) = 3 bytes.
        /// MaNGOS trades the entire item stack; there is no quantity field.
        /// </summary>
        public async Task OfferItemAsync(byte tradeSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Offering item from bag {BagId} slot {SlotId} to trade slot {TradeSlot}",
                    bagId, slotId, tradeSlot);

                // MaNGOS HandleSetTradeItemOpcode reads: tradeSlot, bag, slot (3 bytes)
                var payload = new byte[] { tradeSlot, bagId, slotId };
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM, payload, cancellationToken);

                _localTradeItemSlotChanged.OnNext((tradeSlot, (TradeSlots)tradeSlot));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer item in trade");
                _localTradeErrors.OnNext(("OfferItem", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>CMSG_CLEAR_TRADE_ITEM: tradeSlot(1) = 1 byte.</summary>
        public async Task ClearOfferedItemAsync(int tradeWindowSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);

                var payload = new byte[] { (byte)tradeWindowSlot };
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CLEAR_TRADE_ITEM, payload, cancellationToken);

                _localTradeItemSlotChanged.OnNext((tradeWindowSlot, (TradeSlots)tradeWindowSlot));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear item from trade");
                _localTradeErrors.OnNext(("ClearItem", ex.Message));
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        /// <summary>
        /// HandleServerResponse for direct (non-reactive) use.
        /// SMSG_TRADE_STATUS format: uint32 status + variable extra data per status code.
        /// </summary>
        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_TRADE_STATUS:
                        HandleTradeStatus(data);
                        break;
                    case Opcode.SMSG_TRADE_STATUS_EXTENDED:
                        HandleTradeStatusExtended(data);
                        break;
                    default:
                        _logger.LogWarning("Unhandled trade opcode: {Opcode}", opcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling server response for opcode {Opcode}", opcode);
                _localTradeErrors.OnNext(("ServerResponse", ex.Message));
            }
        }

        private void HandleTradeStatus(byte[] data)
        {
            if (data.Length < 4) return;

            var status = (TradeStatus)BitConverter.ToUInt32(data, 0);

            switch (status)
            {
                case TradeStatus.BeginTrade:
                    // uint32 status + ObjectGuid(8) = 12 bytes
                    if (data.Length >= 12)
                    {
                        var guid = BitConverter.ToUInt64(data, 4);
                        HandleTradeRequested(guid);
                    }
                    break;

                case TradeStatus.OpenWindow:
                    HandleTradeOpened();
                    break;

                case TradeStatus.TradeAccept:
                    _theyAccepted = true;
                    _logger.LogDebug("Trade partner accepted");
                    break;

                case TradeStatus.BackToTrade:
                    _theyAccepted = false;
                    _logger.LogDebug("Trade partner unaccepted");
                    break;

                case TradeStatus.TradeComplete:
                case TradeStatus.TradeCanceled:
                case TradeStatus.CloseWindow:
                    HandleTradeClosed();
                    break;

                default:
                    _logger.LogDebug("Trade status: {Status} ({Code})", status, (uint)status);
                    break;
            }
        }

        private void HandleTradeStatusExtended(byte[] data)
        {
            var windowData = ParseTradeStatusExtended(data);
            if (windowData != null)
            {
                if (windowData.IsTraderView)
                    _traderWindowData = windowData;
                else
                    _ownWindowData = windowData;
                TradeWindowUpdated?.Invoke(windowData);
                _logger.LogDebug("Extended trade update: trader={IsTrader}, gold={Gold}, items={Count}",
                    windowData.IsTraderView, windowData.Gold, windowData.Items.Count(i => !i.IsEmpty));
            }
        }

        public void HandleTradeRequested(ulong traderGuid)
        {
            _tradePartnerGuid = traderGuid;
            TradeRequested?.Invoke(traderGuid);
            _logger.LogDebug("Trade requested by player: {TraderGuid:X}", traderGuid);
        }

        public void HandleTradeOpened()
        {
            _isTradeWindowOpen = true;
            TradeOpened?.Invoke();
            _logger.LogDebug("Trade window opened");
        }

        public void HandleTradeClosed()
        {
            ResetTradeState();
            TradeClosed?.Invoke();
            _logger.LogDebug("Trade window closed");
        }

        private void ResetTradeState()
        {
            _isTradeWindowOpen = false;
            _tradePartnerGuid = null;
            _ourMoneyOffer = 0;
            _weAccepted = false;
            _theyAccepted = false;
            _traderWindowData = null;
            _ownWindowData = null;
        }

        public override void Dispose()
        {
            if (_disposed) return;

            _localTradeItemSlotChanged.OnCompleted();
            _localMoneyOfferedChanged.OnCompleted();
            _localTradeErrors.OnCompleted();

            TradeRequested = null;
            TradeOpened = null;
            TradeClosed = null;
            MoneyOfferedChanged = null;
            TradeItemSlotChanged = null;
            TradeOperationFailed = null;
            TradeWindowUpdated = null;

            _disposed = true;
            base.Dispose();
        }
    }
}
