using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling vendor operations in World of Warcraft.
    /// Manages buying, selling, and repairing items with NPC vendors with enhanced functionality
    /// for bulk operations, junk selling, and special item handling.
    /// </summary>
    public interface IVendorNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether a vendor window is currently open.
        /// </summary>
        bool IsVendorWindowOpen { get; }

        /// <summary>
        /// Gets the current vendor information (if any vendor is open).
        /// </summary>
        VendorInfo? CurrentVendor { get; }

        /// <summary>
        /// Gets the last vendor operation timestamp.
        /// </summary>
        DateTime? LastOperationTime { get; }

        /// <summary>
        /// Event fired when a vendor window is opened.
        /// </summary>
        event Action<VendorInfo>? VendorWindowOpened;

        /// <summary>
        /// Event fired when a vendor window is closed.
        /// </summary>
        event Action? VendorWindowClosed;

        /// <summary>
        /// Event fired when an item is successfully purchased.
        /// </summary>
        event Action<VendorPurchaseData>? ItemPurchased;

        /// <summary>
        /// Event fired when an item is successfully sold.
        /// </summary>
        event Action<VendorSaleData>? ItemSold;

        /// <summary>
        /// Event fired when items are successfully repaired.
        /// </summary>
        event Action<VendorRepairData>? ItemsRepaired;

        /// <summary>
        /// Event fired when a vendor operation fails.
        /// </summary>
        event Action<string>? VendorError;

        /// <summary>
        /// Event fired when a soulbound confirmation is required.
        /// </summary>
        event Action<SoulboundConfirmation>? SoulboundConfirmationRequired;

        /// <summary>
        /// Event fired when a bulk operation progresses.
        /// </summary>
        event Action<string, int, int>? BulkOperationProgress;

        /// <summary>
        /// Opens the vendor window by greeting the specified vendor NPC.
        /// Sends CMSG_GOSSIP_HELLO to initiate vendor interaction.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenVendorAsync(ulong vendorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the vendor's inventory list.
        /// Sends CMSG_LIST_INVENTORY to get available items for purchase.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestVendorInventoryAsync(ulong vendorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Purchases an item from the vendor.
        /// Sends CMSG_BUY_ITEM with the specified item and quantity.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="quantity">The quantity to purchase.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BuyItemAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Purchases an item from the vendor by vendor slot index.
        /// More efficient when you know the vendor slot index.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="vendorSlot">The vendor slot index.</param>
        /// <param name="quantity">The quantity to purchase.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BuyItemBySlotAsync(ulong vendorGuid, byte vendorSlot, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Purchases multiple stacks of an item in bulk.
        /// Automatically handles stack size calculations and multiple purchases.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="totalQuantity">The total quantity desired.</param>
        /// <param name="options">Bulk operation options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BuyItemBulkAsync(ulong vendorGuid, uint itemId, uint totalQuantity, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Purchases an item from the vendor into a specific bag slot.
        /// Sends CMSG_BUY_ITEM_IN_SLOT with the specified item, quantity, and destination.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="quantity">The quantity to purchase.</param>
        /// <param name="bagId">The destination bag ID.</param>
        /// <param name="slotId">The destination slot ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task BuyItemInSlotAsync(ulong vendorGuid, uint itemId, uint quantity, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sells an item to the vendor.
        /// Sends CMSG_SELL_ITEM with the specified item location and quantity.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="quantity">The quantity to sell.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SellItemAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sells all junk items (poor quality) in inventory to the vendor.
        /// Automatically identifies and sells junk items based on quality.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="options">Bulk operation options for quality filtering and behavior.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with total value received.</returns>
        Task<uint> SellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sells specific items from a list.
        /// Useful for selling predetermined items or items from custom logic.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="junkItems">The list of items to sell.</param>
        /// <param name="options">Bulk operation options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with total value received.</returns>
        Task<uint> SellItemsAsync(ulong vendorGuid, IEnumerable<JunkItem> junkItems, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Repairs a specific item with the vendor.
        /// Sends CMSG_REPAIR_ITEM with the specified item location.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RepairItemAsync(ulong vendorGuid, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Repairs all damaged items with the vendor.
        /// Sends CMSG_REPAIR_ITEM with a special parameter to repair all items.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the estimated repair cost for all damaged items.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with the repair cost.</returns>
        Task<uint> GetRepairCostAsync(ulong vendorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the vendor window.
        /// This typically happens automatically when moving away from the vendor.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseVendorAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified vendor GUID has an open vendor window.
        /// </summary>
        /// <param name="vendorGuid">The GUID to check.</param>
        /// <returns>True if the vendor window is open for the specified GUID, false otherwise.</returns>
        bool IsVendorOpen(ulong vendorGuid);

        /// <summary>
        /// Gets the list of items available for purchase from the current vendor.
        /// </summary>
        /// <returns>A read-only list of vendor items.</returns>
        IReadOnlyList<VendorItem> GetAvailableItems();

        /// <summary>
        /// Finds a vendor item by item ID.
        /// </summary>
        /// <param name="itemId">The item ID to find.</param>
        /// <returns>The vendor item if found, null otherwise.</returns>
        VendorItem? FindVendorItem(uint itemId);

        /// <summary>
        /// Gets all junk items in the player's inventory that can be sold.
        /// </summary>
        /// <param name="options">Options for determining what constitutes junk.</param>
        /// <returns>A list of junk items.</returns>
        Task<IReadOnlyList<JunkItem>> GetJunkItemsAsync(BulkVendorOptions? options = null);

        /// <summary>
        /// Validates whether an item can be purchased from the vendor.
        /// </summary>
        /// <param name="itemId">The item ID to validate.</param>
        /// <param name="quantity">The quantity to purchase.</param>
        /// <returns>True if the item can be purchased, false otherwise.</returns>
        bool CanPurchaseItem(uint itemId, uint quantity = 1);

        /// <summary>
        /// Validates whether an item can be sold to the vendor.
        /// </summary>
        /// <param name="bagId">The bag ID of the item.</param>
        /// <param name="slotId">The slot ID of the item.</param>
        /// <param name="quantity">The quantity to sell.</param>
        /// <returns>True if the item can be sold, false otherwise.</returns>
        bool CanSellItem(byte bagId, byte slotId, uint quantity = 1);

        /// <summary>
        /// Confirms or denies a soulbound item operation.
        /// </summary>
        /// <param name="confirmation">The soulbound confirmation to respond to.</param>
        /// <param name="accept">True to accept, false to deny.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RespondToSoulboundConfirmationAsync(SoulboundConfirmation confirmation, bool accept, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete vendor interaction: open, buy item, close.
        /// This is a convenience method for simple purchases.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="quantity">The quantity to purchase.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickBuyAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete vendor interaction: open, sell item, close.
        /// This is a convenience method for simple sales.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="quantity">The quantity to sell.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickSellAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete vendor interaction: open, repair all, close.
        /// This is a convenience method for quick repairs.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickRepairAllAsync(ulong vendorGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete vendor interaction: open, sell all junk, close.
        /// This is a convenience method for cleaning inventory.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="options">Options for junk selling.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation with total value received.</returns>
        Task<uint> QuickSellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a comprehensive vendor visit: repair all, sell junk, buy specified items.
        /// This is a convenience method for a complete vendor interaction.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor NPC.</param>
        /// <param name="itemsToBuy">Items to purchase (item ID, quantity).</param>
        /// <param name="options">Options for the vendor visit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, BulkVendorOptions? options = null, CancellationToken cancellationToken = default);

        // Server response handlers
        /// <summary>
        /// Handles server responses for vendor window opening.
        /// This method should be called when SMSG_LIST_INVENTORY is received.
        /// </summary>
        /// <param name="vendorInfo">The vendor information.</param>
        void HandleVendorWindowOpened(VendorInfo vendorInfo);

        /// <summary>
        /// Handles server responses for successful item purchases.
        /// This method should be called when SMSG_BUY_ITEM is received.
        /// </summary>
        /// <param name="purchaseData">The purchase data.</param>
        void HandleItemPurchased(VendorPurchaseData purchaseData);

        /// <summary>
        /// Handles server responses for successful item sales.
        /// This method should be called when SMSG_SELL_ITEM is received.
        /// </summary>
        /// <param name="saleData">The sale data.</param>
        void HandleItemSold(VendorSaleData saleData);

        /// <summary>
        /// Handles server responses for successful repairs.
        /// This method should be called when a repair operation succeeds.
        /// </summary>
        /// <param name="repairData">The repair data.</param>
        void HandleItemsRepaired(VendorRepairData repairData);

        /// <summary>
        /// Handles server responses for vendor operation failures.
        /// This method should be called when SMSG_BUY_FAILED or similar error responses are received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        void HandleVendorError(string errorMessage);

        /// <summary>
        /// Handles soulbound confirmation requests from the server.
        /// </summary>
        /// <param name="confirmation">The soulbound confirmation request.</param>
        void HandleSoulboundConfirmationRequest(SoulboundConfirmation confirmation);
    }
}