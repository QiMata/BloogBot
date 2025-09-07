using GameData.Core.Enums;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Interface for handling looting operations in World of Warcraft.
    /// Manages loot containers, automatic looting, and item collection.
    /// </summary>
    public interface ILootingNetworkAgent
    {
        /// <summary>
        /// Gets whether a loot window is currently open.
        /// </summary>
        bool IsLootWindowOpen { get; }

        /// <summary>
        /// Event fired when a loot window is opened.
        /// </summary>
        event Action<ulong> LootWindowOpened;

        /// <summary>
        /// Event fired when a loot window is closed.
        /// </summary>
        event Action LootWindowClosed;

        /// <summary>
        /// Event fired when an item is successfully looted.
        /// </summary>
        event Action<uint, uint> ItemLooted; // ItemId, Quantity

        /// <summary>
        /// Event fired when money is looted.
        /// </summary>
        event Action<uint> MoneyLooted; // Amount in copper

        /// <summary>
        /// Event fired when a loot error occurs.
        /// </summary>
        event Action<string> LootError;

        /// <summary>
        /// Opens a loot container (corpse, chest, etc.).
        /// Sends CMSG_LOOT to the server.
        /// </summary>
        /// <param name="lootTargetGuid">The GUID of the loot target.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loots all money from the current loot window.
        /// Sends CMSG_LOOT_MONEY to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LootMoneyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loots a specific item from the loot window.
        /// Sends CMSG_AUTOSTORE_LOOT_ITEM to the server.
        /// </summary>
        /// <param name="lootSlot">The slot index of the item to loot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LootItemAsync(byte lootSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a loot item in a specific inventory slot.
        /// Sends CMSG_STORE_LOOT_IN_SLOT to the server.
        /// </summary>
        /// <param name="lootSlot">The loot slot index.</param>
        /// <param name="bag">The destination bag.</param>
        /// <param name="slot">The destination slot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StoreLootInSlotAsync(byte lootSlot, byte bag, byte slot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the current loot window.
        /// Sends CMSG_LOOT_RELEASE to the server.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseLootAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs loot roll for a group loot item.
        /// Sends CMSG_LOOT_ROLL to the server.
        /// </summary>
        /// <param name="lootGuid">The GUID of the loot object.</param>
        /// <param name="itemSlot">The item slot in the loot.</param>
        /// <param name="rollType">The type of roll (need, greed, pass).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RollForLootAsync(ulong lootGuid, byte itemSlot, LootRollType rollType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loots all available items and money from the current loot window.
        /// This is a convenience method that automates the entire looting process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LootAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens loot and automatically loots everything.
        /// This is a convenience method for quick looting.
        /// </summary>
        /// <param name="lootTargetGuid">The GUID of the loot target.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Enumeration for loot roll types in group situations.
    /// </summary>
    public enum LootRollType : byte
    {
        /// <summary>
        /// Pass on the item.
        /// </summary>
        Pass = 0,

        /// <summary>
        /// Roll need for the item.
        /// </summary>
        Need = 1,

        /// <summary>
        /// Roll greed for the item.
        /// </summary>
        Greed = 2
    }
}