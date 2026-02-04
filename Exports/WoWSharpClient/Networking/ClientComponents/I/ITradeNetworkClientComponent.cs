using GameData.Core.Enums;
using System.Reactive; // for Unit

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for player-to-player trading operations over the network.
    /// Supports initiating trades, offering items/money, and accepting/canceling trades.
    /// </summary>
    public interface ITradeNetworkClientComponent : INetworkClientComponent
    {
        // State
        bool IsTradeOpen { get; }
        ulong? TradingWithGuid { get; }
        uint OfferedCopper { get; }

        // Reactive observables (preferred)
        IObservable<ulong> TradeRequests { get; }
        IObservable<Unit> TradesOpened { get; }
        IObservable<Unit> TradesClosed { get; }
        IObservable<uint> OfferedMoneyChanges { get; }
        IObservable<(int TradeWindowSlot, TradeSlots Slot)> TradeItemSlotsChanged { get; }
        IObservable<(string Operation, string Error)> TradeErrors { get; }

        // Operations
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
