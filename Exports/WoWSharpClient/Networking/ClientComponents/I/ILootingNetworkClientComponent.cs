using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling looting operations in World of Warcraft.
    /// Manages loot containers, automatic looting, and item collection.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface ILootingNetworkClientComponent : INetworkClientComponent
    {
        #region Properties

        /// <summary>
        /// Gets whether a loot window is currently open.
        /// </summary>
        bool IsLootWindowOpen { get; }

        /// <summary>
        /// Gets the current loot target GUID if loot window is open.
        /// </summary>
        ulong? CurrentLootTarget { get; }

        /// <summary>
        /// Gets the current group loot method being used.
        /// </summary>
        GroupLootMethod CurrentLootMethod { get; }

        /// <summary>
        /// Gets whether the player is currently the loot master.
        /// </summary>
        bool IsMasterLooter { get; }

        /// <summary>
        /// Gets the current loot threshold for group loot.
        /// </summary>
        ItemQuality LootThreshold { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of loot window state changes.
        /// </summary>
        IObservable<LootWindowData> LootWindowChanges { get; }

        /// <summary>
        /// Observable stream of item loot operations.
        /// </summary>
        IObservable<LootData> ItemLoot { get; }

        /// <summary>
        /// Observable stream of money loot operations.
        /// </summary>
        IObservable<MoneyLootData> MoneyLoot { get; }

        /// <summary>
        /// Observable stream of loot roll operations.
        /// </summary>
        IObservable<LootRollData> LootRolls { get; }

        /// <summary>
        /// Observable stream of loot errors.
        /// </summary>
        IObservable<LootErrorData> LootErrors { get; }

        /// <summary>
        /// Observable stream of loot window openings.
        /// </summary>
        IObservable<LootWindowData> LootWindowOpened { get; }

        /// <summary>
        /// Observable stream of loot window closings.
        /// </summary>
        IObservable<LootWindowData> LootWindowClosed { get; }

        /// <summary>
        /// Observable stream of bind on pickup confirmations.
        /// </summary>
        IObservable<BindOnPickupData> BindOnPickupConfirmations { get; }

        /// <summary>
        /// Observable stream of group loot notifications.
        /// </summary>
        IObservable<GroupLootNotificationData> GroupLootNotifications { get; }

        /// <summary>
        /// Observable stream of master loot assignments.
        /// </summary>
        IObservable<MasterLootData> MasterLootAssignments { get; }

        #endregion

        #region Operations

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
        /// Confirms binding of a bind-on-pickup item.
        /// Sends CMSG_LOOT_RELEASE to confirm the binding.
        /// </summary>
        /// <param name="lootSlot">The slot of the BoP item.</param>
        /// <param name="confirm">Whether to confirm or cancel the binding.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ConfirmBindOnPickupAsync(byte lootSlot, bool confirm = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Assigns a master loot item to a specific player.
        /// Available only when player is the master looter.
        /// </summary>
        /// <param name="lootSlot">The slot of the item to assign.</param>
        /// <param name="targetPlayerGuid">The GUID of the target player.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AssignMasterLootAsync(byte lootSlot, ulong targetPlayerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loots all available items and money from the current loot window.
        /// This is a convenience method that automates the entire looting process.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LootAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loots all items meeting the specified quality filter.
        /// </summary>
        /// <param name="minimumQuality">The minimum item quality to loot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LootAllWithFilterAsync(ItemQuality minimumQuality, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens loot and automatically loots everything.
        /// This is a convenience method for quick looting.
        /// </summary>
        /// <param name="lootTargetGuid">The GUID of the loot target.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens loot and automatically loots items meeting quality requirements.
        /// This is a convenience method for filtered quick looting.
        /// </summary>
        /// <param name="lootTargetGuid">The GUID of the loot target.</param>
        /// <param name="minimumQuality">The minimum item quality to loot.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickLootWithFilterAsync(ulong lootTargetGuid, ItemQuality minimumQuality, CancellationToken cancellationToken = default);

        #endregion

        #region Group Loot Operations

        /// <summary>
        /// Sets the group loot method. Only available to party/raid leaders.
        /// </summary>
        /// <param name="method">The loot method to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetGroupLootMethodAsync(GroupLootMethod method, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the loot threshold for group loot.
        /// </summary>
        /// <param name="threshold">The minimum quality for group loot rolls.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetLootThresholdAsync(ItemQuality threshold, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the master looter for the group.
        /// </summary>
        /// <param name="masterLooterGuid">The GUID of the player to make master looter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetMasterLooterAsync(ulong masterLooterGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls for all eligible items based on intelligent decision making.
        /// </summary>
        /// <param name="preferences">Roll preferences for different scenarios.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AutoRollAsync(LootRollPreferences preferences, CancellationToken cancellationToken = default);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Checks if looting is currently possible (loot window is open).
        /// </summary>
        /// <returns>True if looting is possible, false otherwise.</returns>
        bool CanLoot();

        /// <summary>
        /// Checks if a specific loot operation can be performed.
        /// </summary>
        /// <param name="lootSlot">The loot slot to check.</param>
        /// <returns>True if the slot can be looted, false otherwise.</returns>
        bool CanLootSlot(byte lootSlot);

        /// <summary>
        /// Checks if the player can master loot the specified slot.
        /// </summary>
        /// <param name="lootSlot">The loot slot to check.</param>
        /// <returns>True if master loot is possible, false otherwise.</returns>
        bool CanMasterLoot(byte lootSlot);

        /// <summary>
        /// Validates a loot operation before execution.
        /// </summary>
        /// <param name="lootSlot">The loot slot to validate.</param>
        /// <returns>A validation result indicating whether the operation is valid.</returns>
        ValidationResult ValidateLootOperation(byte lootSlot);

        /// <summary>
        /// Gets the current loot information for all available slots.
        /// </summary>
        /// <returns>A collection of loot slot information.</returns>
        IReadOnlyCollection<LootSlotInfo> GetAvailableLoot();

        /// <summary>
        /// Gets loot information filtered by quality.
        /// </summary>
        /// <param name="minimumQuality">The minimum quality to include.</param>
        /// <returns>A collection of filtered loot slot information.</returns>
        IReadOnlyCollection<LootSlotInfo> GetLootByQuality(ItemQuality minimumQuality);

        #endregion

        #region Server Response Handling

        /// <summary>
        /// Handles loot window state change from the server.
        /// This method should be called by the packet handler when loot window opens/closes.
        /// </summary>
        /// <param name="isOpen">Whether the loot window is open.</param>
        /// <param name="lootTargetGuid">The loot target GUID.</param>
        /// <param name="availableItems">Number of available items.</param>
        /// <param name="availableMoney">Amount of available money.</param>
        void HandleLootWindowChanged(bool isOpen, ulong? lootTargetGuid, uint availableItems = 0, uint availableMoney = 0);

        /// <summary>
        /// Handles loot list information from the server.
        /// </summary>
        /// <param name="lootTargetGuid">The loot target GUID.</param>
        /// <param name="lootSlots">Information about available loot slots.</param>
        void HandleLootList(ulong lootTargetGuid, IReadOnlyCollection<LootSlotInfo> lootSlots);

        /// <summary>
        /// Handles item loot from the server.
        /// </summary>
        /// <param name="lootTargetGuid">The loot target GUID.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="quantity">The quantity looted.</param>
        /// <param name="quality">The item quality.</param>
        /// <param name="lootSlot">The loot slot.</param>
        void HandleItemLooted(ulong lootTargetGuid, uint itemId, string itemName, uint quantity, ItemQuality quality, byte lootSlot);

        /// <summary>
        /// Handles money loot from the server.
        /// </summary>
        /// <param name="lootTargetGuid">The loot target GUID.</param>
        /// <param name="amount">The amount of money looted.</param>
        void HandleMoneyLooted(ulong lootTargetGuid, uint amount);

        /// <summary>
        /// Handles loot roll result from the server.
        /// </summary>
        /// <param name="lootGuid">The loot GUID.</param>
        /// <param name="itemSlot">The item slot.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="rollType">The roll type.</param>
        /// <param name="rollResult">The roll result.</param>
        void HandleLootRoll(ulong lootGuid, byte itemSlot, uint itemId, LootRollType rollType, uint rollResult);

        /// <summary>
        /// Handles bind on pickup confirmation request from the server.
        /// </summary>
        /// <param name="lootSlot">The slot of the BoP item.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="itemName">The item name.</param>
        void HandleBindOnPickupConfirmation(byte lootSlot, uint itemId, string itemName);

        /// <summary>
        /// Handles group loot method change notification.
        /// </summary>
        /// <param name="newMethod">The new loot method.</param>
        /// <param name="masterLooterGuid">The master looter GUID (if applicable).</param>
        /// <param name="threshold">The loot threshold.</param>
        void HandleGroupLootMethodChanged(GroupLootMethod newMethod, ulong? masterLooterGuid, ItemQuality threshold);

        /// <summary>
        /// Handles master loot assignment notification.
        /// </summary>
        /// <param name="lootSlot">The loot slot.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="targetPlayerGuid">The target player GUID.</param>
        /// <param name="targetPlayerName">The target player name.</param>
        void HandleMasterLootAssigned(byte lootSlot, uint itemId, ulong targetPlayerGuid, string targetPlayerName);

        /// <summary>
        /// Handles loot error from the server.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="lootTargetGuid">The loot target GUID.</param>
        /// <param name="lootSlot">The loot slot that caused the error.</param>
        void HandleLootError(string errorMessage, ulong? lootTargetGuid = null, byte? lootSlot = null);

        #endregion

        #region Legacy Callback Support (for backwards compatibility)

        /// <summary>
        /// Sets the callback function to be invoked when a loot window is opened.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the loot target GUID.</param>
        [Obsolete("Use LootWindowOpened observable instead")]
        void SetLootWindowOpenedCallback(Action<ulong>? callback);

        /// <summary>
        /// Sets the callback function to be invoked when a loot window is closed.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function to invoke when loot window closes.</param>
        [Obsolete("Use LootWindowClosed observable instead")]
        void SetLootWindowClosedCallback(Action? callback);

        /// <summary>
        /// Sets the callback function to be invoked when an item is successfully looted.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives ItemId and Quantity.</param>
        [Obsolete("Use ItemLoot observable instead")]
        void SetItemLootedCallback(Action<uint, uint>? callback);

        /// <summary>
        /// Sets the callback function to be invoked when money is looted.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the amount in copper.</param>
        [Obsolete("Use MoneyLoot observable instead")]
        void SetMoneyLootedCallback(Action<uint>? callback);

        /// <summary>
        /// Sets the callback function to be invoked when a loot error occurs.
        /// This is provided for backwards compatibility. Use reactive observables instead.
        /// </summary>
        /// <param name="callback">Callback function that receives the error message.</param>
        [Obsolete("Use LootErrors observable instead")]
        void SetLootErrorCallback(Action<string>? callback);

        #endregion
    }
}