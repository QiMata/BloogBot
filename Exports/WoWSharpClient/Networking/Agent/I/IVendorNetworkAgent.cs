namespace WoWSharpClient.Networking.Agent.I
{
    /// <summary>
    /// Interface for handling vendor operations in World of Warcraft.
    /// Manages buying, selling, and repairing items with NPC vendors.
    /// </summary>
    public interface IVendorNetworkAgent
    {
        /// <summary>
        /// Gets a value indicating whether a vendor window is currently open.
        /// </summary>
        bool IsVendorWindowOpen { get; }

        /// <summary>
        /// Event fired when a vendor window is opened.
        /// </summary>
        event Action<ulong>? VendorWindowOpened;

        /// <summary>
        /// Event fired when a vendor window is closed.
        /// </summary>
        event Action? VendorWindowClosed;

        /// <summary>
        /// Event fired when an item is successfully purchased.
        /// </summary>
        /// <param name="itemId">The ID of the purchased item.</param>
        /// <param name="quantity">The quantity purchased.</param>
        /// <param name="cost">The cost in copper.</param>
        event Action<uint, uint, uint>? ItemPurchased;

        /// <summary>
        /// Event fired when an item is successfully sold.
        /// </summary>
        /// <param name="itemId">The ID of the sold item.</param>
        /// <param name="quantity">The quantity sold.</param>
        /// <param name="value">The value received in copper.</param>
        event Action<uint, uint, uint>? ItemSold;

        /// <summary>
        /// Event fired when items are successfully repaired.
        /// </summary>
        /// <param name="cost">The total repair cost in copper.</param>
        event Action<uint>? ItemsRepaired;

        /// <summary>
        /// Event fired when a vendor operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? VendorError;

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
    }
}