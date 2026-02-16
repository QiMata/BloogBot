using System;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling inventory operations in World of Warcraft.
    /// Manages bag operations, item movement, and inventory state tracking using reactive streams.
    /// </summary>
    public interface IInventoryNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the inventory window is currently open.
        /// </summary>
        bool IsInventoryOpen { get; }

        /// <summary>
        /// Stream of item move confirmations parsed from server messages.
        /// </summary>
        IObservable<ItemMovedData> ItemMovedStream { get; }

        /// <summary>
        /// Stream of item split confirmations parsed from server messages.
        /// </summary>
        IObservable<ItemSplitData> ItemSplitStream { get; }

        /// <summary>
        /// Stream of item destroy confirmations parsed from server messages.
        /// </summary>
        IObservable<ItemDestroyedData> ItemDestroyedStream { get; }

        /// <summary>
        /// Stream of inventory operation errors.
        /// </summary>
        IObservable<string> InventoryErrors { get; }

        /// <summary>
        /// Moves an item from one bag slot to another.
        /// Sends CMSG_AUTOSTORE_BAG_ITEM or CMSG_SWAP_INV_ITEM based on the destination.
        /// </summary>
        Task MoveItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps two items in the inventory.
        /// Sends CMSG_SWAP_INV_ITEM with both item locations.
        /// </summary>
        Task SwapItemsAsync(byte firstBag, byte firstSlot, byte secondBag, byte secondSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Splits a stack of items, moving part of the stack to a new location.
        /// Sends CMSG_SPLIT_ITEM with the source location, destination, and quantity.
        /// </summary>
        Task SplitItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Destroys an item from the inventory.
        /// Sends CMSG_DESTROYITEM with the item location and quantity.
        /// </summary>
        Task DestroyItemAsync(byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Auto-sorts inventory items to optimize bag space.
        /// Sends CMSG_AUTOSTORE_BAG_ITEM for optimal item placement.
        /// </summary>
        Task SortBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a bag in the inventory. Client-side state only.
        /// </summary>
        Task OpenBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes a bag in the inventory. Client-side state only.
        /// </summary>
        Task CloseBagAsync(byte bagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds an empty slot in the inventory for an item.
        /// </summary>
        (byte BagId, byte SlotId)? FindEmptySlot(uint itemId = 0);

        /// <summary>
        /// Counts the total quantity of a specific item in the inventory.
        /// </summary>
        uint CountItem(uint itemId);

        /// <summary>
        /// Checks if the inventory has enough space for new items.
        /// </summary>
        bool HasEnoughSpace(int requiredSlots);

        /// <summary>
        /// Gets the total number of free slots in the inventory.
        /// </summary>
        int GetFreeSlotCount();

        /// <summary>
        /// Enhanced inventory state update for better tracking.
        /// </summary>
        void UpdateInventorySlot(byte bagId, byte slotId, uint itemId, uint quantity = 0);
    }
}