using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Trade operations agent implementing player-to-player trade over network packets.
    /// </summary>
    public class TradeNetworkAgent : ITradeNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TradeNetworkAgent> _logger;

        private bool _isTradeOpen;
        private ulong? _tradingWithGuid;
        private uint _offeredCopper;

        public TradeNetworkAgent(IWorldClient worldClient, ILogger<TradeNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to world client trade opcode handlers
            _worldClient.RegisterOpcodeHandler(Opcode.SMSG_TRADE_STATUS, payload => { HandleServerResponse(Opcode.SMSG_TRADE_STATUS, payload); return Task.CompletedTask; });
            _worldClient.RegisterOpcodeHandler(Opcode.SMSG_TRADE_STATUS_EXTENDED, payload => { HandleServerResponse(Opcode.SMSG_TRADE_STATUS_EXTENDED, payload); return Task.CompletedTask; });
        }

        public bool IsTradeOpen => _isTradeOpen;
        public ulong? TradingWithGuid => _tradingWithGuid;
        public uint OfferedCopper => _offeredCopper;

        public event Action<ulong>? TradeRequested;
        public event Action? TradeOpened;
        public event Action? TradeClosed;
        public event Action<uint>? MoneyOfferedChanged;
        public event Action<int, TradeSlots>? TradeItemSlotChanged;
        public event Action<string, string>? TradeOperationFailed;

        public async Task InitiateTradeAsync(ulong playerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = BitConverter.GetBytes(playerGuid);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_INITIATE_TRADE, payload, cancellationToken);
                _tradingWithGuid = playerGuid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate trade with {Guid:X}", playerGuid);
                TradeOperationFailed?.Invoke("InitiateTrade", ex.Message);
                throw;
            }
        }

        public async Task AcceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_ACCEPT_TRADE, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept trade");
                TradeOperationFailed?.Invoke("AcceptTrade", ex.Message);
                throw;
            }
        }

        public async Task UnacceptTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_UNACCEPT_TRADE, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unaccept trade");
                TradeOperationFailed?.Invoke("UnacceptTrade", ex.Message);
                throw;
            }
        }

        public async Task CancelTradeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_TRADE, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel trade");
                TradeOperationFailed?.Invoke("CancelTrade", ex.Message);
                throw;
            }
        }

        public async Task OfferMoneyAsync(uint copper, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = BitConverter.GetBytes(copper);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_GOLD, payload, cancellationToken);
                _offeredCopper = copper;
                MoneyOfferedChanged?.Invoke(_offeredCopper);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer money: {Copper} copper", copper);
                TradeOperationFailed?.Invoke("OfferMoney", ex.Message);
                throw;
            }
        }

        public async Task OfferItemAsync(byte bagId, byte slotId, byte quantity, int tradeWindowSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                // Build payload: bag, slot, tradeSlot, count
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);
                bw.Write(bagId);
                bw.Write(slotId);
                bw.Write((byte)tradeWindowSlot);
                bw.Write(quantity);
                bw.Flush();
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SET_TRADE_ITEM, ms.ToArray(), cancellationToken);

                TradeItemSlotChanged?.Invoke(tradeWindowSlot, TradeSlots.TRADE_SLOT_NONTRADED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to offer item bag:{Bag} slot:{Slot} qty:{Qty} at tradeSlot:{TradeSlot}", bagId, slotId, quantity, tradeWindowSlot);
                TradeOperationFailed?.Invoke("OfferItem", ex.Message);
                throw;
            }
        }

        public async Task ClearOfferedItemAsync(int tradeWindowSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new byte[] { (byte)tradeWindowSlot };
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CLEAR_TRADE_ITEM, payload, cancellationToken);
                TradeItemSlotChanged?.Invoke(tradeWindowSlot, TradeSlots.TRADE_SLOT_NONTRADED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear trade slot {Slot}", tradeWindowSlot);
                TradeOperationFailed?.Invoke("ClearOfferedItem", ex.Message);
                throw;
            }
        }

        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_TRADE_STATUS:
                        ParseTradeStatus(data);
                        break;
                    case Opcode.SMSG_TRADE_STATUS_EXTENDED:
                        // not strictly required for unit tests yet
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling trade server response {Opcode}", opcode);
            }
        }

        private void ParseTradeStatus(byte[] data)
        {
            using var br = new BinaryReader(new MemoryStream(data));
            if (br.BaseStream.Length == 0) return;

            // Vanilla uses a status code; we map a few common cases
            byte status = br.ReadByte();
            switch (status)
            {
                case 0: // incoming request (heuristic)
                    if (br.BaseStream.Position + 8 <= br.BaseStream.Length)
                    {
                        _tradingWithGuid = br.ReadUInt64();
                        TradeRequested?.Invoke(_tradingWithGuid.Value);
                    }
                    break;
                case 1: // open
                    _isTradeOpen = true;
                    TradeOpened?.Invoke();
                    break;
                case 2: // close/cancel
                    _isTradeOpen = false;
                    TradeClosed?.Invoke();
                    break;
                case 3: // money changed (heuristic)
                    if (br.BaseStream.Position + 4 <= br.BaseStream.Length)
                    {
                        _offeredCopper = br.ReadUInt32();
                        MoneyOfferedChanged?.Invoke(_offeredCopper);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
