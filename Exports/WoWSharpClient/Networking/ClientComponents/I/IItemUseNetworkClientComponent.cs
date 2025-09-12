namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling item usage operations in World of Warcraft.
    /// Manages using consumables, activating items, and handling item interactions.
    /// </summary>
    public interface IItemUseNetworkAgent
    {
        /// <summary>
        /// Gets a value indicating whether an item use operation is currently in progress.
        /// </summary>
        bool IsUsingItem { get; }

        /// <summary>
        /// Gets the GUID of the item currently being used, if any.
        /// </summary>
        ulong? CurrentItemInUse { get; }

        /// <summary>
        /// Event fired when an item is successfully used.
        /// </summary>
        /// <param name="itemGuid">The GUID of the used item.</param>
        /// <param name="itemId">The ID of the used item.</param>
        /// <param name="targetGuid">The target GUID if the item was used on a target.</param>
        event Action<ulong, uint, ulong?>? ItemUsed;

        /// <summary>
        /// Event fired when an item use operation starts (for items with cast times).
        /// </summary>
        /// <param name="itemGuid">The GUID of the item being used.</param>
        /// <param name="castTime">The cast time in milliseconds.</param>
        event Action<ulong, uint>? ItemUseStarted;

        /// <summary>
        /// Event fired when an item use operation is completed.
        /// </summary>
        /// <param name="itemGuid">The GUID of the item that was used.</param>
        event Action<ulong>? ItemUseCompleted;

        /// <summary>
        /// Event fired when an item use operation is interrupted or fails.
        /// </summary>
        /// <param name="itemGuid">The GUID of the item that failed to be used.</param>
        /// <param name="error">The error message.</param>
        event Action<ulong, string>? ItemUseFailed;

        /// <summary>
        /// Event fired when a consumable item effect is applied.
        /// </summary>
        /// <param name="itemId">The ID of the consumable item.</param>
        /// <param name="spellId">The spell ID of the effect applied.</param>
        event Action<uint, uint>? ConsumableEffectApplied;

        /// <summary>
        /// Uses an item from the inventory.
        /// Sends CMSG_USE_ITEM with the item location.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses an item from the inventory on a specific target.
        /// Sends CMSG_USE_ITEM with the item location and target GUID.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="targetGuid">The GUID of the target to use the item on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseItemOnTargetAsync(byte bagId, byte slotId, ulong targetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses an item from the inventory at specific coordinates.
        /// Sends CMSG_USE_ITEM with the item location and world coordinates.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is located.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="z">The Z coordinate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseItemAtLocationAsync(byte bagId, byte slotId, float x, float y, float z, CancellationToken cancellationToken = default);

        /// <summary>
        /// Activates an item by its GUID (for items already in use or equipped).
        /// Sends CMSG_USE_ITEM with the item GUID.
        /// </summary>
        /// <param name="itemGuid">The GUID of the item to activate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ActivateItemAsync(ulong itemGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses a consumable item (food, drink, potion, etc.).
        /// This is a convenience method that handles consumable-specific logic.
        /// </summary>
        /// <param name="bagId">The bag ID where the consumable is located.</param>
        /// <param name="slotId">The slot ID where the consumable is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseConsumableAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a container item (bags, boxes, chests in inventory).
        /// Sends CMSG_USE_ITEM for container items to open their contents.
        /// </summary>
        /// <param name="bagId">The bag ID where the container is located.</param>
        /// <param name="slotId">The slot ID where the container is located.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenContainerAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uses a tool item (fishing pole, mining pick, etc.).
        /// Handles tool-specific usage patterns and requirements.
        /// </summary>
        /// <param name="bagId">The bag ID where the tool is located.</param>
        /// <param name="slotId">The slot ID where the tool is located.</param>
        /// <param name="targetGuid">Optional target GUID for the tool use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UseToolAsync(byte bagId, byte slotId, ulong? targetGuid = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the current item use operation if one is in progress.
        /// Sends CMSG_CANCEL_CAST to interrupt the item use.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CancelItemUseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a specific item can be used based on cooldowns and requirements.
        /// </summary>
        /// <param name="itemId">The item ID to check.</param>
        /// <returns>True if the item can be used, false otherwise.</returns>
        bool CanUseItem(uint itemId);

        /// <summary>
        /// Gets the remaining cooldown time for an item in milliseconds.
        /// </summary>
        /// <param name="itemId">The item ID to check.</param>
        /// <returns>The remaining cooldown time in milliseconds, or 0 if no cooldown.</returns>
        uint GetItemCooldown(uint itemId);

        /// <summary>
        /// Finds and uses the first available item of a specific type in the inventory.
        /// This is a convenience method for using consumables by item ID.
        /// </summary>
        /// <param name="itemId">The item ID to find and use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation. Returns true if item was found and used.</returns>
        Task<bool> FindAndUseItemAsync(uint itemId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates reagent consumption tracking based on server response.
        /// </summary>
        /// <param name="itemId">The item ID that consumed reagents.</param>
        /// <param name="reagentsConsumed">List of reagent item IDs that were consumed.</param>
        void UpdateReagentConsumption(uint itemId, List<uint> reagentsConsumed);

        /// <summary>
        /// Updates projectile consumption tracking for ranged weapons.
        /// </summary>
        /// <param name="projectileType">The type of projectile (arrows, bullets, etc.).</param>
        /// <param name="amountConsumed">Number of projectiles consumed.</param>
        void UpdateProjectileConsumption(uint projectileType, uint amountConsumed);
    }
}