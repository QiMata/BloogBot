using System.Reactive; // for Unit
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling vendor interactions via observables.
    /// </summary>
    public interface IVendorNetworkClientComponent : INetworkClientComponent
    {
        // State
        bool IsVendorWindowOpen { get; }
        VendorInfo? CurrentVendor { get; }

        // Observables (preferred reactive API)
        IObservable<VendorInfo> VendorWindowsOpened { get; }
        IObservable<Unit> VendorWindowsClosed { get; }
        IObservable<VendorPurchaseData> ItemsPurchased { get; }
        IObservable<VendorSaleData> ItemsSold { get; }
        IObservable<VendorRepairData> ItemsRepairEvents { get; }
        IObservable<string> VendorErrors { get; }
        IObservable<SoulboundConfirmation> SoulboundConfirmations { get; }

        // Commands
        Task OpenVendorAsync(ulong vendorGuid, CancellationToken cancellationToken = default);
        Task RequestVendorInventoryAsync(ulong vendorGuid, CancellationToken cancellationToken = default);
        Task BuyItemAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default);
        Task BuyItemBySlotAsync(ulong vendorGuid, byte vendorSlot, uint quantity = 1, CancellationToken cancellationToken = default);
        Task BuyItemBulkAsync(ulong vendorGuid, uint itemId, uint totalQuantity, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);
        Task BuyItemInSlotAsync(ulong vendorGuid, uint itemId, uint quantity, byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task SellItemAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);
        Task<uint> SellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);
        Task<uint> SellItemsAsync(ulong vendorGuid, IEnumerable<JunkItem> junkItems, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);
        Task RepairItemAsync(ulong vendorGuid, byte bagId, byte slotId, CancellationToken cancellationToken = default);
        Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken cancellationToken = default);
        Task<uint> GetRepairCostAsync(ulong vendorGuid, CancellationToken cancellationToken = default);
        Task CloseVendorAsync(CancellationToken cancellationToken = default);

        // Queries
        bool IsVendorOpen(ulong vendorGuid);
        IReadOnlyList<VendorItem> GetAvailableItems();
        VendorItem? FindVendorItem(uint itemId);
        Task<IReadOnlyList<JunkItem>> GetJunkItemsAsync(BulkVendorOptions? options = null);
        bool CanPurchaseItem(uint itemId, uint quantity = 1);
        bool CanSellItem(byte bagId, byte slotId, uint quantity = 1);

        // Confirmations
        Task RespondToSoulboundConfirmationAsync(SoulboundConfirmation confirmation, bool accept, CancellationToken cancellationToken = default);

        // Shortcuts
        Task QuickBuyAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default);
        Task QuickSellAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);
        Task QuickRepairAllAsync(ulong vendorGuid, CancellationToken cancellationToken = default);
        Task<uint> QuickSellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);
        Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);
    }
}