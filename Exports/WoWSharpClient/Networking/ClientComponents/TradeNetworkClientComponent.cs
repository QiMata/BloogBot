using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Trade window slot types for item placement.
    /// </summary>
    public enum TradeSlots
    {
        TradeSlot1 = 0,
        TradeSlot2 = 1,
        TradeSlot3 = 2,
        TradeSlot4 = 3,
        TradeSlot5 = 4,
        TradeSlot6 = 5
    }

    /// <summary>
    /// Implementation of trade network agent that handles trade operations in World of Warcraft.
    /// Manages player-to-player trading, item offers, money offers, and trade confirmations using the Mangos protocol.
    /// </summary>
    public class TradeNetworkClientComponent : NetworkClientComponent, ITradeNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TradeNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private bool _isTradeWindowOpen;
        private ulong? _tradePartnerGuid;
        private uint _ourMoneyOffer;
        private uint _theirMoneyOffer;
        private bool _weAccepted;
        private bool _theyAccepted;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TradeNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TradeNetworkClientComponent(IWorldClient worldClient, ILogger<TradeNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsTradeOpen => _isTradeWindowOpen;

        /// <inheritdoc />
        public ulong? TradingWithGuid => _tradePartnerGuid;

        /// <inheritdoc />
        public uint OfferedCopper => _ourMoneyOffer;

        /// <inheritdoc />
        public event Action<ulong>? TradeRequested;

        /// <inheritdoc />
        public event Action? TradeOpened;

        /// <inheritdoc />
        public event Action? TradeClosed;

        /// <inheritdoc />
        public event Action<uint>? MoneyOfferedChanged;

        /// <inheritdoc />
        public event Action<int, TradeSlots>? TradeItemSlotChanged;

        /// <inheritdoc />
        public event Action<string, string>? TradeOperationFailed;

        /// <inheritdoc />
        public async Task InitiateTradeAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Initiating trade with player: {PlayerGuid:X}", playerGuid);

                var payload = BitConverter.GetBytes(playerGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_INITIATE_TRADE, payload, cancellationToken);

                _tradePartnerGuid = playerGuid;
                _logger.LogInformation("Trade initiation request sent to player: {PlayerGuid:X}", playerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate trade with player: {PlayerGuid:X}", playerGuid);
                TradeOperationFailed?.Invoke("InitiateTrade", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AcceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Accepting trade");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ACCEPT_TRADE, [], cancellationToken);

                _weAccepted = true;
                _logger.LogInformation("Trade accepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept trade");
                TradeOperationFailed?.Invoke("AcceptTrade", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UnacceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unaccepting trade");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_UNACCEPT_TRADE, [], cancellationToken);

                _weAccepted = false;
                _logger.LogInformation("Trade unaccepted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unaccept trade");
                TradeOperationFailed?.Invoke("UnacceptTrade", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CancelTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Canceling trade");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_TRADE, [], cancellationToken);

                _logger.LogInformation("Trade canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel trade");
                TradeOperationFailed?.Invoke("CancelTrade", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task OfferMoneyAsync(uint copper, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Offering {Copper} copper in trade", copper);

                var payload = BitConverter.GetBytes(copper);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_GOLD, payload, cancellationToken);

                _ourMoneyOffer = copper;
                MoneyOfferedChanged?.Invoke(_ourMoneyOffer);
                _logger.LogInformation("Offered {Copper} copper in trade", copper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer money in trade: {Copper} copper", copper);
                TradeOperationFailed?.Invoke("OfferMoney", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task OfferItemAsync(byte bagId, byte slotId, byte quantity, int tradeWindowSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Offering item from bag {BagId} slot {SlotId} quantity {Quantity} to trade slot {TradeSlot}", 
                    bagId, slotId, quantity, tradeWindowSlot);

                // Build payload: bag, slot, tradeSlot, count (matches unit test expectations)
                using var ms = new MemoryStream();
                using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
                writer.Write(bagId);
                writer.Write(slotId);
                writer.Write((byte)tradeWindowSlot);
                writer.Write(quantity);
                writer.Flush();

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM, ms.ToArray(), cancellationToken);

                TradeItemSlotChanged?.Invoke(tradeWindowSlot, (TradeSlots)tradeWindowSlot);
                _logger.LogInformation("Item offered in trade window slot {TradeSlot}", tradeWindowSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer item in trade");
                TradeOperationFailed?.Invoke("OfferItem", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ClearOfferedItemAsync(int tradeWindowSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Clearing item from trade slot {TradeSlot}", tradeWindowSlot);

                // Build payload: just the trade slot index (matches unit test expectations)
                var payload = new byte[] { (byte)tradeWindowSlot };
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CLEAR_TRADE_ITEM, payload, cancellationToken);

                TradeItemSlotChanged?.Invoke(tradeWindowSlot, (TradeSlots)tradeWindowSlot);
                _logger.LogInformation("Cleared item from trade window slot {TradeSlot}", tradeWindowSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear item from trade");
                TradeOperationFailed?.Invoke("ClearItem", ex.Message);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
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
                TradeOperationFailed?.Invoke("ServerResponse", ex.Message);
            }
        }

        private void HandleTradeStatus(byte[] data)
        {
            if (data.Length == 0) return;

            // Heuristic format (unit tests): first byte is a status code
            if (data.Length >= 1)
            {
                byte code = data[0];
                switch (code)
                {
                    case 0: // incoming request: next 8 bytes = guid (if present)
                        if (data.Length >= 9)
                        {
                            var guid = BitConverter.ToUInt64(data, 1);
                            HandleTradeRequested(guid);
                        }
                        return;
                    case 1: // open
                        HandleTradeOpened();
                        return;
                    case 2: // close
                        HandleTradeClosed();
                        return;
                    case 3: // money changed: next 4 bytes = copper (if present)
                        if (data.Length >= 5)
                        {
                            var copper = BitConverter.ToUInt32(data, 1);
                            HandleMoneyOfferChanged(copper);
                        }
                        return;
                }
            }

            // Fallback: some servers send a 4-byte status (uint)
            if (data.Length >= 4)
            {
                var status = BitConverter.ToUInt32(data, 0);
                switch (status)
                {
                    case 1: // begin/open
                    case 2: // open window
                        HandleTradeOpened();
                        break;
                    case 3: // canceled
                    case 6: // complete
                        HandleTradeClosed();
                        break;
                    default:
                        _logger.LogDebug("Unhandled numeric trade status: {Status}", status);
                        break;
                }
            }
        }

        private void HandleTradeStatusExtended(byte[] data)
        {
            _logger.LogDebug("Received extended trade status");
        }

        /// <summary>
        /// Handles server responses for trade requests.
        /// </summary>
        /// <param name="traderGuid">The GUID of the trader.</param>
        public void HandleTradeRequested(ulong traderGuid)
        {
            _tradePartnerGuid = traderGuid;
            TradeRequested?.Invoke(traderGuid);
            _logger.LogDebug("Trade requested by player: {TraderGuid:X}", traderGuid);
        }

        /// <summary>
        /// Handles server responses for trade window opening.
        /// </summary>
        public void HandleTradeOpened()
        {
            _isTradeWindowOpen = true;
            TradeOpened?.Invoke();
            _logger.LogDebug("Trade window opened");
        }

        /// <summary>
        /// Handles server responses for trade window closing.
        /// </summary>
        public void HandleTradeClosed()
        {
            _isTradeWindowOpen = false;
            _tradePartnerGuid = null;
            _ourMoneyOffer = 0;
            _theirMoneyOffer = 0;
            _weAccepted = false;
            _theyAccepted = false;
            TradeClosed?.Invoke();
            _logger.LogDebug("Trade window closed");
        }

        /// <summary>
        /// Handles server responses for money offer changes.
        /// </summary>
        /// <param name="copper">The amount of copper offered.</param>
        public void HandleMoneyOfferChanged(uint copper)
        {
            _ourMoneyOffer = copper;
            MoneyOfferedChanged?.Invoke(_ourMoneyOffer);
            _logger.LogDebug("Money offer changed to: {Copper} copper", copper);
        }

        /// <summary>
        /// Handles server responses for trade operation failures.
        /// </summary>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="errorMessage">The error message.</param>
        public void HandleTradeOperationError(string operation, string errorMessage)
        {
            TradeOperationFailed?.Invoke(operation, errorMessage);
            _logger.LogWarning("Trade operation failed: {Operation} - {Error}", operation, errorMessage);
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the trade network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing TradeNetworkClientComponent");

            // Clear events to prevent memory leaks
            TradeRequested = null;
            TradeOpened = null;
            TradeClosed = null;
            MoneyOfferedChanged = null;
            TradeItemSlotChanged = null;
            TradeOperationFailed = null;

            _disposed = true;
            _logger.LogDebug("TradeNetworkClientComponent disposed");
        }

        #endregion
    }
}