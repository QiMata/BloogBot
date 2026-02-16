using GameData.Core.Enums;
using System;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    public interface ITradeNetworkClientComponent : INetworkClientComponent
    {
        // State
        bool IsTradeOpen { get; }
        ulong? TradingWithGuid { get; }
        uint OfferedCopper { get; }
        TradeWindowData? TraderWindowData { get; }
        TradeWindowData? OwnWindowData { get; }

        // Reactive observables
        IObservable<ulong> TradeRequests { get; }
        IObservable<Unit> TradesOpened { get; }
        IObservable<Unit> TradesClosed { get; }
        IObservable<uint> OfferedMoneyChanges { get; }
        IObservable<(int TradeWindowSlot, TradeSlots Slot)> TradeItemSlotsChanged { get; }
        IObservable<(string Operation, string Error)> TradeErrors { get; }
        IObservable<TradeWindowData> TradeWindowUpdates { get; }

        // Operations
        Task InitiateTradeAsync(ulong playerGuid, CancellationToken cancellationToken = default);
        Task AcceptTradeAsync(CancellationToken cancellationToken = default);
        Task UnacceptTradeAsync(CancellationToken cancellationToken = default);
        Task CancelTradeAsync(CancellationToken cancellationToken = default);

        Task OfferMoneyAsync(uint copper, CancellationToken cancellationToken = default);
        /// <summary>
        /// Offers an item in a trade slot. MaNGOS trades the entire stack (no quantity field).
        /// CMSG_SET_TRADE_ITEM: tradeSlot(1) + bag(1) + slot(1) = 3 bytes.
        /// </summary>
        Task OfferItemAsync(byte tradeSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task ClearOfferedItemAsync(int tradeWindowSlot, CancellationToken cancellationToken = default);

        void HandleServerResponse(Opcode opcode, byte[] data);
    }
}
