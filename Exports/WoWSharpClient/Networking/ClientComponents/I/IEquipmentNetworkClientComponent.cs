using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling equipment operations in World of Warcraft.
    /// Manages equipping, unequipping, and equipment state tracking.
    /// </summary>
    public interface IEquipmentNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Event fired when an item is successfully equipped.
        /// </summary>
        /// <param name="itemGuid">The GUID of the equipped item.</param>
        /// <param name="slot">The equipment slot where the item was equipped.</param>
        event Action<ulong, EquipmentSlot>? ItemEquipped;

        /// <summary>
        /// Event fired when an item is successfully unequipped.
        /// </summary>
        /// <param name="itemGuid">The GUID of the unequipped item.</param>
        /// <param name="slot">The equipment slot from which the item was unequipped.</param>
        event Action<ulong, EquipmentSlot>? ItemUnequipped;

        /// <summary>
        /// Event fired when equipment items are swapped between slots.
        /// </summary>
        /// <param name="firstItemGuid">The GUID of the first item.</param>
        /// <param name="firstSlot">The first equipment slot.</param>
        /// <param name="secondItemGuid">The GUID of the second item.</param>
        /// <param name="secondSlot">The second equipment slot.</param>
        event Action<ulong, EquipmentSlot, ulong, EquipmentSlot>? EquipmentSwapped;

        /// <summary>
        /// Event fired when an equipment operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? EquipmentError;

        /// <summary>
        /// Event fired when equipment durability changes.
        /// </summary>
        /// <param name="slot">The equipment slot.</param>
        /// <param name="currentDurability">The current durability.</param>
        /// <param name="maxDurability">The maximum durability.</param>
        event Action<EquipmentSlot, uint, uint>? DurabilityChanged;

        /// <summary>
        /// Equips an item from the inventory to a specific equipment slot.
        /// Sends CMSG_AUTOEQUIP_ITEM with the source location and destination slot.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="equipSlot">The equipment slot to equip the item to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task EquipItemAsync(byte bagId, byte slotId, EquipmentSlot equipSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Auto-equips an item from the inventory to its appropriate slot.
        /// Sends CMSG_AUTOEQUIP_ITEM, letting the server determine the best slot.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AutoEquipItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unequips an item from an equipment slot to the inventory.
        /// Sends CMSG_AUTOSTORE_BAG_ITEM to move the item to an available bag slot.
        /// </summary>
        /// <param name="equipSlot">The equipment slot to unequip.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnequipItemAsync(EquipmentSlot equipSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unequips an item from an equipment slot to a specific inventory location.
        /// Sends CMSG_SWAP_INV_ITEM with the equipment slot and target bag location.
        /// </summary>
        /// <param name="equipSlot">The equipment slot to unequip.</param>
        /// <param name="targetBag">The target bag ID.</param>
        /// <param name="targetSlot">The target slot ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnequipItemToSlotAsync(EquipmentSlot equipSlot, byte targetBag, byte targetSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps equipment between two equipment slots.
        /// Sends CMSG_SWAP_INV_ITEM with both equipment slot locations.
        /// </summary>
        /// <param name="firstSlot">The first equipment slot.</param>
        /// <param name="secondSlot">The second equipment slot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SwapEquipmentAsync(EquipmentSlot firstSlot, EquipmentSlot secondSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps an equipped item with an item in the inventory.
        /// Sends CMSG_SWAP_INV_ITEM with the equipment slot and inventory location.
        /// </summary>
        /// <param name="equipSlot">The equipment slot.</param>
        /// <param name="bagId">The inventory bag ID.</param>
        /// <param name="slotId">The inventory slot ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SwapEquipmentWithInventoryAsync(EquipmentSlot equipSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Equips all items from the inventory that can be auto-equipped.
        /// This is a convenience method that attempts to equip the best available items.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AutoEquipAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Unequips all equipment to the inventory.
        /// This is a convenience method for clearing all equipment slots.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UnequipAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an equipment slot is currently occupied.
        /// </summary>
        /// <param name="slot">The equipment slot to check.</param>
        /// <returns>True if the slot is occupied, false otherwise.</returns>
        bool IsSlotEquipped(EquipmentSlot slot);

        /// <summary>
        /// Gets the GUID of the item in a specific equipment slot.
        /// </summary>
        /// <param name="slot">The equipment slot to check.</param>
        /// <returns>The GUID of the equipped item, or null if the slot is empty.</returns>
        ulong? GetEquippedItem(EquipmentSlot slot);

        /// <summary>
        /// Gets the item ID of the item in a specific equipment slot.
        /// </summary>
        /// <param name="slot">The equipment slot to check.</param>
        /// <returns>The item ID of the equipped item, or null if the slot is empty.</returns>
        uint? GetEquippedItemId(EquipmentSlot slot);

        /// <summary>
        /// Gets the durability information for an equipped item.
        /// </summary>
        /// <param name="slot">The equipment slot to check.</param>
        /// <returns>A tuple containing current and maximum durability, or null if no item is equipped.</returns>
        (uint Current, uint Maximum)? GetItemDurability(EquipmentSlot slot);

        /// <summary>
        /// Checks if any equipped items need repair (below 100% durability).
        /// </summary>
        /// <returns>True if any items need repair, false otherwise.</returns>
        bool HasDamagedEquipment();

        /// <summary>
        /// Gets a list of equipment slots that contain items needing repair.
        /// </summary>
        /// <returns>An enumerable of equipment slots with damaged items.</returns>
        IEnumerable<EquipmentSlot> GetDamagedEquipmentSlots();
    }
}