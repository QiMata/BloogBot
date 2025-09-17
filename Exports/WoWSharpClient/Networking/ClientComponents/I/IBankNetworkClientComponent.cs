using System.Reactive;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling personal bank operations in World of Warcraft.
    /// Manages depositing and withdrawing items or gold from the player's personal bank.
    /// Uses reactive observables for better composability and filtering.
    /// </summary>
    public interface IBankNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether a bank window is currently open.
        /// </summary>
        bool IsBankWindowOpen { get; }

        /// <summary>
        /// Gets the current bank slot count available to the player.
        /// </summary>
        uint AvailableBankSlots { get; }

        /// <summary>
        /// Gets the number of purchased bank bag slots.
        /// </summary>
        uint PurchasedBankBagSlots { get; }

        #region Reactive Observables
        /// <summary>
        /// Observable stream fired when a bank window is opened. Carries the banker GUID.
        /// </summary>
        IObservable<ulong> BankWindowOpenedStream { get; }

        /// <summary>
        /// Observable stream fired when a bank window is closed.
        /// </summary>
        IObservable<Unit> BankWindowClosedStream { get; }

        /// <summary>
        /// Observable stream fired when an item is deposited into the bank.
        /// </summary>
        IObservable<ItemMovementData> ItemDepositedStream { get; }

        /// <summary>
        /// Observable stream fired when an item is withdrawn from the bank.
        /// </summary>
        IObservable<ItemMovementData> ItemWithdrawnStream { get; }

        /// <summary>
        /// Observable stream fired when items are swapped between inventory and bank.
        /// </summary>
        IObservable<ItemsSwappedData> ItemsSwappedStream { get; }

        /// <summary>
        /// Observable stream fired when gold is deposited to the bank.
        /// </summary>
        IObservable<uint> GoldDepositedStream { get; }

        /// <summary>
        /// Observable stream fired when gold is withdrawn from the bank.
        /// </summary>
        IObservable<uint> GoldWithdrawnStream { get; }

        /// <summary>
        /// Observable stream fired when a bank bag slot is purchased.
        /// </summary>
        IObservable<BankSlotPurchaseData> BankSlotPurchasedStream { get; }

        /// <summary>
        /// Observable stream fired when bank information is updated.
        /// </summary>
        IObservable<BankInfoData> BankInfoUpdatedStream { get; }

        /// <summary>
        /// Observable stream fired when a bank operation fails.
        /// </summary>
        IObservable<BankOperationErrorData> BankOperationFailedStream { get; }
        #endregion

        /// <summary>
        /// Opens the bank window by greeting the specified banker NPC.
        /// Sends CMSG_BANKER_ACTIVATE to initiate bank interaction.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenBankAsync(ulong bankerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deposits an item from inventory into the bank.
        /// Moves an item from a bag slot to an available bank slot.
        /// </summary>
        /// <param name="bagId">The source bag ID in inventory.</param>
        /// <param name="slotId">The source slot ID in the bag.</param>
        /// <param name="quantity">The quantity to deposit (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DepositItemAsync(byte bagId, byte slotId, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deposits an item from inventory into a specific bank slot.
        /// Moves an item from a bag slot to a specific bank slot.
        /// </summary>
        /// <param name="sourceBagId">The source bag ID in inventory.</param>
        /// <param name="sourceSlotId">The source slot ID in the bag.</param>
        /// <param name="bankSlot">The target bank slot.</param>
        /// <param name="quantity">The quantity to deposit (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DepositItemToSlotAsync(byte sourceBagId, byte sourceSlotId, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Withdraws an item from the bank to inventory.
        /// Moves an item from a bank slot to an available inventory slot.
        /// </summary>
        /// <param name="bankSlot">The source bank slot.</param>
        /// <param name="quantity">The quantity to withdraw (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WithdrawItemAsync(byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Withdraws an item from the bank to a specific inventory slot.
        /// Moves an item from a bank slot to a specific bag slot.
        /// </summary>
        /// <param name="bankSlot">The source bank slot.</param>
        /// <param name="targetBagId">The target bag ID in inventory.</param>
        /// <param name="targetSlotId">The target slot ID in the bag.</param>
        /// <param name="quantity">The quantity to withdraw (for stackable items).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WithdrawItemToSlotAsync(byte bankSlot, byte targetBagId, byte targetSlotId, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Swaps an item in inventory with an item in the bank.
        /// Exchanges the positions of two items between inventory and bank.
        /// </summary>
        /// <param name="inventoryBagId">The bag ID of the inventory item.</param>
        /// <param name="inventorySlotId">The slot ID of the inventory item.</param>
        /// <param name="bankSlot">The bank slot of the bank item.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SwapItemWithBankAsync(byte inventoryBagId, byte inventorySlotId, byte bankSlot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deposits gold into the bank.
        /// Transfers gold from the player's inventory to the bank.
        /// </summary>
        /// <param name="amount">The amount of gold to deposit in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DepositGoldAsync(uint amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Withdraws gold from the bank.
        /// Transfers gold from the bank to the player's inventory.
        /// </summary>
        /// <param name="amount">The amount of gold to withdraw in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WithdrawGoldAsync(uint amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Purchases a bank bag slot to expand bank storage.
        /// Sends CMSG_BUY_BANK_SLOT to purchase additional storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PurchaseBankSlotAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the bank window.
        /// This typically happens automatically when moving away from the banker.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseBankAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests updated bank information from the server.
        /// Gets current bank slot counts and available space.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestBankInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified banker GUID has an open bank window.
        /// </summary>
        /// <param name="bankerGuid">The GUID to check.</param>
        /// <returns>True if the bank window is open for the specified GUID, false otherwise.</returns>
        bool IsBankOpenWith(ulong bankerGuid);

        /// <summary>
        /// Finds an empty bank slot for depositing items.
        /// </summary>
        /// <returns>The index of an empty bank slot, or null if no slots are available.</returns>
        byte? FindEmptyBankSlot();

        /// <summary>
        /// Gets the cost of purchasing the next bank bag slot.
        /// </summary>
        /// <returns>The cost in copper for the next bank slot, or null if no more slots can be purchased.</returns>
        uint? GetNextBankSlotCost();

        /// <summary>
        /// Checks if there is enough space in the bank for a new item.
        /// </summary>
        /// <param name="requiredSlots">The number of slots required.</param>
        /// <returns>True if there is enough space, false otherwise.</returns>
        bool HasBankSpace(uint requiredSlots = 1);

        /// <summary>
        /// Performs a complete bank interaction: open, deposit item, close.
        /// This is a convenience method for quick deposits.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        /// <param name="bagId">The source bag ID in inventory.</param>
        /// <param name="slotId">The source slot ID in the bag.</param>
        /// <param name="quantity">The quantity to deposit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickDepositAsync(ulong bankerGuid, byte bagId, byte slotId, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete bank interaction: open, withdraw item, close.
        /// This is a convenience method for quick withdrawals.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        /// <param name="bankSlot">The source bank slot.</param>
        /// <param name="quantity">The quantity to withdraw.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickWithdrawAsync(ulong bankerGuid, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete bank interaction: open, deposit gold, close.
        /// This is a convenience method for quick gold deposits.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        /// <param name="amount">The amount of gold to deposit in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickDepositGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a complete bank interaction: open, withdraw gold, close.
        /// This is a convenience method for quick gold withdrawals.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        /// <param name="amount">The amount of gold to withdraw in copper.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickWithdrawGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the type of bank operation for error reporting.
    /// </summary>
    public enum BankOperationType
    {
        OpenBank,
        CloseBank,
        DepositItem,
        WithdrawItem,
        SwapItem,
        DepositGold,
        WithdrawGold,
        PurchaseSlot,
        RequestInfo
    }

    /// <summary>
    /// Data representing item movement to/from bank.
    /// </summary>
    public record ItemMovementData(ulong ItemGuid, uint ItemId, uint Quantity, byte Slot);

    /// <summary>
    /// Data representing a swap between inventory and bank items.
    /// </summary>
    public record ItemsSwappedData(ulong InventoryItemGuid, ulong BankItemGuid);

    /// <summary>
    /// Data representing a bank slot purchase.
    /// </summary>
    public record BankSlotPurchaseData(byte SlotIndex, uint Cost);

    /// <summary>
    /// Data representing bank info update.
    /// </summary>
    public record BankInfoData(uint AvailableSlots, uint PurchasedBagSlots);

    /// <summary>
    /// Data representing a bank operation failure.
    /// </summary>
    public record BankOperationErrorData(BankOperationType Operation, string ErrorMessage);
}