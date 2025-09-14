namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling inventory operations in World of Warcraft.
    /// Manages bag operations, item movement, and inventory state tracking.
    /// </summary>
    public interface IInventoryNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the inventory window is currently open.
        /// </summary>
        bool IsInventoryOpen { get; }

        /// <summary>
        /// Event fired when an item is moved in the inventory.
        /// </summary>
        /// <param name="itemGuid">The GUID of the moved item.</param>
        /// <param name="sourceBag">The source bag ID.</param>
        /// <param name="sourceSlot">The source slot ID.</param>
        /// <param name="destinationBag">The destination bag ID.</param>
        /// <param name="destinationSlot">The destination slot ID.</param>
        event Action<ulong, byte, byte, byte, byte>? ItemMoved;

        /// <summary>
        /// Event fired when an item stack is split.
        /// </summary>
        /// <param name="itemGuid">The GUID of the split item.</param>
        /// <param name="splitQuantity">The quantity that was split.</param>
        event Action<ulong, uint>? ItemSplit;

        /// <summary>
        /// Event fired when items are swapped between slots.
        /// </summary>
        /// <param name="firstItemGuid">The GUID of the first item.</param>
        /// <param name="secondItemGuid">The GUID of the second item.</param>
        event Action<ulong, ulong>? ItemsSwapped;

        /// <summary>
        /// Event fired when an item is destroyed.
        /// </summary>
        /// <param name="itemGuid">The GUID of the destroyed item.</param>
        /// <param name="quantity">The quantity destroyed.</param>
        event Action<ulong, uint>? ItemDestroyed;

        /// <summary>
        /// Event fired when an inventory operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? InventoryError;

        /// <summary>
        /// Moves an item from one bag slot to another.
        /// Sends CMSG_AUTOSTORE_BAG_ITEM or CMSG_SWAP_INV_ITEM based on the destination.
        /// </summary>
        /// <param name="sourceBag">The source bag ID.</param>
        /// <param name="sourceSlot">The source slot ID.</param>
        /// <param name="destinationBag">The destination bag ID.</param>
        /// <param name="destinationSlot">The destination slot ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task MoveItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps two items in the inventory.
        /// Sends CMSG_SWAP_INV_ITEM with both item locations.
        /// </summary>
        /// <param name="firstBag">The bag ID of the first item.</param>
        /// <param name="firstSlot">The slot ID of the first item.</param>
        /// <param name="secondBag">The bag ID of the second item.</param>
        /// <param name="secondSlot">The slot ID of the second item.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SwapItemsAsync(byte firstBag, byte firstSlot, byte secondBag, byte secondSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Splits a stack of items, moving part of the stack to a new location.
        /// Sends CMSG_SPLIT_ITEM with the source location, destination, and quantity.
        /// </summary>
        /// <param name="sourceBag">The source bag ID.</param>
        /// <param name="sourceSlot">The source slot ID.</param>
        /// <param name="destinationBag">The destination bag ID.</param>
        /// <param name="destinationSlot">The destination slot ID.</param>
        /// <param name="quantity">The quantity to split from the stack.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SplitItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Destroys an item from the inventory.
        /// Sends CMSG_DESTROYITEM with the item location and quantity.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="quantity">The quantity to destroy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DestroyItemAsync(byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Auto-sorts inventory items to optimize bag space.
        /// Sends CMSG_AUTOSTORE_BAG_ITEM for optimal item placement.
        /// </summary>
        /// <param name="bagId">The bag ID to sort. Use 0 for main backpack.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SortBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a bag in the inventory.
        /// This updates the client state to show the bag contents.
        /// </summary>
        /// <param name="bagId">The bag ID to open.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a bag in the inventory.
        /// This updates the client state to hide the bag contents.
        /// </summary>
        /// <param name="bagId">The bag ID to close.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds an empty slot in the inventory for an item.
        /// Searches through all available bags for the first empty slot.
        /// </summary>
        /// <param name="itemId">The item ID to check size requirements for.</param>
        /// <returns>A tuple containing the bag ID and slot ID, or null if no space found.</returns>
        (byte BagId, byte SlotId)? FindEmptySlot(uint itemId = 0);

        /// <summary>
        /// Counts the total quantity of a specific item in the inventory.
        /// Searches through all bags for the specified item.
        /// </summary>
        /// <param name="itemId">The item ID to count.</param>
        /// <returns>The total quantity of the item found.</returns>
        uint CountItem(uint itemId);

        /// <summary>
        /// Checks if the inventory has enough space for new items.
        /// </summary>
        /// <param name="requiredSlots">The number of empty slots required.</param>
        /// <returns>True if enough space is available, false otherwise.</returns>
        bool HasEnoughSpace(int requiredSlots);

        /// <summary>
        /// Gets the total number of free slots in the inventory.
        /// </summary>
        /// <returns>The number of available inventory slots.</returns>
        int GetFreeSlotCount();

        /// <summary>
        /// Enhanced inventory state update for better tracking.
        /// </summary>
        /// <param name="bagId">The bag ID.</param>
        /// <param name="slotId">The slot ID.</param>
        /// <param name="itemId">The item ID (0 if slot is empty).</param>
        /// <param name="quantity">The item quantity (0 if slot is empty).</param>
        void UpdateInventorySlot(byte bagId, byte slotId, uint itemId, uint quantity = 0);
    }
}