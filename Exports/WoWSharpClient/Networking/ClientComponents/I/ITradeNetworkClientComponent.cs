using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for player-to-player trading operations over the network.
    /// Supports initiating trades, offering items/money, and accepting/canceling trades.
    /// </summary>
    public interface ITradeNetworkAgent
    {
        bool IsTradeOpen { get; }
        ulong? TradingWithGuid { get; }
        uint OfferedCopper { get; }

        event Action<ulong>? TradeRequested; // someone requested a trade with us
        event Action? TradeOpened;
        event Action? TradeClosed;
        event Action<uint>? MoneyOfferedChanged;
        event Action<int, TradeSlots>? TradeItemSlotChanged; // index, slot
        event Action<string, string>? TradeOperationFailed;

        Task InitiateTradeAsync(ulong playerGuid, CancellationToken cancellationToken = default);
        Task AcceptTradeAsync(CancellationToken cancellationToken = default);
        Task UnacceptTradeAsync(CancellationToken cancellationToken = default);
        Task CancelTradeAsync(CancellationToken cancellationToken = default);

        Task OfferMoneyAsync(uint copper, CancellationToken cancellationToken = default);
        Task OfferItemAsync(byte bagId, byte slotId, byte quantity, int tradeWindowSlot, CancellationToken cancellationToken = default);
        Task ClearOfferedItemAsync(int tradeWindowSlot, CancellationToken cancellationToken = default);

        void HandleServerResponse(Opcode opcode, byte[] data);
    }
}
