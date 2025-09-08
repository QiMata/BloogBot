using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;
using GameData.Core.Enums;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of the bank network agent for handling personal bank operations.
    /// Manages depositing and withdrawing items or gold from the player's personal bank.
    /// </summary>
    public class BankNetworkAgent : IBankNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<BankNetworkAgent> _logger;

        // Bank state tracking
        private bool _isBankWindowOpen;
        private ulong? _currentBankerGuid;
        private uint _availableBankSlots;
        private uint _purchasedBankBagSlots;

        // Constants for bank operations
        private const byte BANK_SLOT_COUNT = 24; // Standard bank slots
        private const byte MAX_BANK_BAG_SLOTS = 7; // Maximum purchasable bag slots
        private static readonly uint[] BankSlotCosts = { 1000, 7500, 15000, 37500, 75000, 150000, 300000 }; // Costs in copper

        #region Properties

        /// <inheritdoc />
        public bool IsBankWindowOpen => _isBankWindowOpen;

        /// <inheritdoc />
        public uint AvailableBankSlots => _availableBankSlots;

        /// <inheritdoc />
        public uint PurchasedBankBagSlots => _purchasedBankBagSlots;

        #endregion

        #region Events

        /// <inheritdoc />
        public event Action<ulong>? BankWindowOpened;

        /// <inheritdoc />
        public event Action? BankWindowClosed;

        /// <inheritdoc />
        public event Action<ulong, uint, uint, byte>? ItemDeposited;

        /// <inheritdoc />
        public event Action<ulong, uint, uint, byte>? ItemWithdrawn;

        /// <inheritdoc />
        public event Action<ulong, ulong>? ItemsSwapped;

        /// <inheritdoc />
        public event Action<uint>? GoldDeposited;

        /// <inheritdoc />
        public event Action<uint>? GoldWithdrawn;

        /// <inheritdoc />
        public event Action<byte, uint>? BankSlotPurchased;

        /// <inheritdoc />
        public event Action<uint, uint>? BankInfoUpdated;

        /// <inheritdoc />
        public event Action<BankOperationType, string>? BankOperationFailed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the BankNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the bank agent.</param>
        public BankNetworkAgent(IWorldClient worldClient, ILogger<BankNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("BankNetworkAgent initialized");
        }

        #endregion

        #region Core Bank Operations

        /// <inheritdoc />
        public async Task OpenBankAsync(ulong bankerGuid, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Opening bank with banker: {BankerGuid:X}", bankerGuid);

            try
            {
                // Send CMSG_BANKER_ACTIVATE to open the bank
                var payload = new byte[8];
                BitConverter.GetBytes(bankerGuid).CopyTo(payload, 0);
                await _worldClient.SendMovementAsync(Opcode.CMSG_BANKER_ACTIVATE, payload, cancellationToken);

                _currentBankerGuid = bankerGuid;
                _isBankWindowOpen = true;

                // Request bank information to update slot counts
                await RequestBankInfoAsync(cancellationToken);

                BankWindowOpened?.Invoke(bankerGuid);
                _logger.LogInformation("Bank window opened with banker: {BankerGuid:X}", bankerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open bank with banker: {BankerGuid:X}", bankerGuid);
                BankOperationFailed?.Invoke(BankOperationType.OpenBank, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseBankAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Closing bank window");

            try
            {
                if (_isBankWindowOpen)
                {
                    _isBankWindowOpen = false;
                    _currentBankerGuid = null;

                    BankWindowClosed?.Invoke();
                    _logger.LogInformation("Bank window closed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close bank window");
                BankOperationFailed?.Invoke(BankOperationType.CloseBank, ex.Message);
                throw;
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Item Operations

        /// <inheritdoc />
        public async Task DepositItemAsync(byte bagId, byte slotId, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Depositing item from bag {BagId}:{SlotId}, quantity: {Quantity}", bagId, slotId, quantity);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.DepositItem, error);
                return;
            }

            var emptySlot = FindEmptyBankSlot();
            if (!emptySlot.HasValue)
            {
                var error = "No empty bank slots available";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.DepositItem, error);
                return;
            }

            await DepositItemToSlotAsync(bagId, slotId, emptySlot.Value, quantity, cancellationToken);
        }

        /// <inheritdoc />
        public async Task DepositItemToSlotAsync(byte sourceBagId, byte sourceSlotId, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Depositing item from bag {BagId}:{SlotId} to bank slot {BankSlot}, quantity: {Quantity}", 
                sourceBagId, sourceSlotId, bankSlot, quantity);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.DepositItem, error);
                return;
            }

            try
            {
                // Send item swap packet from inventory to bank using CMSG_SWAP_INV_ITEM
                var payload = new byte[2];
                payload[0] = (byte)(sourceBagId * 16 + sourceSlotId); // source slot
                payload[1] = (byte)(BANK_SLOT_COUNT + bankSlot); // destination bank slot
                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item deposited from {BagId}:{SlotId} to bank slot {BankSlot}", sourceBagId, sourceSlotId, bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deposit item from {BagId}:{SlotId} to bank slot {BankSlot}", sourceBagId, sourceSlotId, bankSlot);
                BankOperationFailed?.Invoke(BankOperationType.DepositItem, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task WithdrawItemAsync(byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot}, quantity: {Quantity}", bankSlot, quantity);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawItem, error);
                return;
            }

            try
            {
                // Send item move packet from bank to first available inventory slot using CMSG_AUTOBANK_ITEM
                var payload = new byte[1];
                payload[0] = (byte)(BANK_SLOT_COUNT + bankSlot);
                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOBANK_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item withdrawn from bank slot {BankSlot}", bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw item from bank slot {BankSlot}", bankSlot);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawItem, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task WithdrawItemToSlotAsync(byte bankSlot, byte targetBagId, byte targetSlotId, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot} to bag {BagId}:{SlotId}, quantity: {Quantity}", 
                bankSlot, targetBagId, targetSlotId, quantity);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawItem, error);
                return;
            }

            try
            {
                // Send item swap packet from bank to specific inventory slot
                var payload = new byte[2];
                payload[0] = (byte)(BANK_SLOT_COUNT + bankSlot); // source bank slot
                payload[1] = (byte)(targetBagId * 16 + targetSlotId); // destination slot
                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item withdrawn from bank slot {BankSlot} to {BagId}:{SlotId}", bankSlot, targetBagId, targetSlotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw item from bank slot {BankSlot} to {BagId}:{SlotId}", bankSlot, targetBagId, targetSlotId);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawItem, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SwapItemWithBankAsync(byte inventoryBagId, byte inventorySlotId, byte bankSlot, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Swapping item in {BagId}:{SlotId} with bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.SwapItem, error);
                return;
            }

            try
            {
                // Send item swap packet between inventory and bank
                var payload = new byte[2];
                payload[0] = (byte)(inventoryBagId * 16 + inventorySlotId); // inventory slot
                payload[1] = (byte)(BANK_SLOT_COUNT + bankSlot); // bank slot
                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item swapped between {BagId}:{SlotId} and bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap item between {BagId}:{SlotId} and bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);
                BankOperationFailed?.Invoke(BankOperationType.SwapItem, ex.Message);
                throw;
            }
        }

        #endregion

        #region Gold Operations

        /// <inheritdoc />
        public async Task DepositGoldAsync(uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Depositing {Amount} copper to bank", amount);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.DepositGold, error);
                return;
            }

            try
            {
                // For now, we'll log this as a placeholder since the specific packet structure may need more investigation
                _logger.LogWarning("Gold deposit operation not fully implemented - packet structure needs verification");
                BankOperationFailed?.Invoke(BankOperationType.DepositGold, "Gold operations not yet fully implemented");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deposit {Amount} copper to bank", amount);
                BankOperationFailed?.Invoke(BankOperationType.DepositGold, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task WithdrawGoldAsync(uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing {Amount} copper from bank", amount);

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawGold, error);
                return;
            }

            try
            {
                // For now, we'll log this as a placeholder since the specific packet structure may need more investigation
                _logger.LogWarning("Gold withdrawal operation not fully implemented - packet structure needs verification");
                BankOperationFailed?.Invoke(BankOperationType.WithdrawGold, "Gold operations not yet fully implemented");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw {Amount} copper from bank", amount);
                BankOperationFailed?.Invoke(BankOperationType.WithdrawGold, ex.Message);
                throw;
            }
        }

        #endregion

        #region Bank Management

        /// <inheritdoc />
        public async Task PurchaseBankSlotAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Purchasing bank bag slot");

            if (!_isBankWindowOpen)
            {
                var error = "Bank window is not open";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.PurchaseSlot, error);
                return;
            }

            if (_purchasedBankBagSlots >= MAX_BANK_BAG_SLOTS)
            {
                var error = "Maximum bank bag slots already purchased";
                _logger.LogWarning(error);
                BankOperationFailed?.Invoke(BankOperationType.PurchaseSlot, error);
                return;
            }

            try
            {
                // Send CMSG_BUY_BANK_SLOT
                await _worldClient.SendMovementAsync(Opcode.CMSG_BUY_BANK_SLOT, Array.Empty<byte>(), cancellationToken);

                var slotIndex = (byte)_purchasedBankBagSlots;
                var cost = BankSlotCosts[_purchasedBankBagSlots];
                _purchasedBankBagSlots++;

                BankSlotPurchased?.Invoke(slotIndex, cost);
                _logger.LogInformation("Purchased bank bag slot {SlotIndex} for {Cost} copper", slotIndex, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purchase bank bag slot");
                BankOperationFailed?.Invoke(BankOperationType.PurchaseSlot, ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestBankInfoAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Requesting bank information");

            try
            {
                // This would typically be handled by server responses when opening the bank
                // For now, we'll simulate the response with default values
                _availableBankSlots = BANK_SLOT_COUNT;
                
                BankInfoUpdated?.Invoke(_availableBankSlots, _purchasedBankBagSlots);
                _logger.LogDebug("Bank info updated - Available slots: {Available}, Purchased bag slots: {Purchased}", 
                    _availableBankSlots, _purchasedBankBagSlots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request bank information");
                BankOperationFailed?.Invoke(BankOperationType.RequestInfo, ex.Message);
                throw;
            }

            await Task.CompletedTask;
        }

        #endregion

        #region Helper Methods

        /// <inheritdoc />
        public bool IsBankOpenWith(ulong bankerGuid)
        {
            return _isBankWindowOpen && _currentBankerGuid == bankerGuid;
        }

        /// <inheritdoc />
        public byte? FindEmptyBankSlot()
        {
            // This would typically query the actual bank state from the client
            // For now, we'll return the first slot as a placeholder
            if (HasBankSpace())
            {
                return 0; // Return first available slot
            }
            return null;
        }

        /// <inheritdoc />
        public uint? GetNextBankSlotCost()
        {
            if (_purchasedBankBagSlots >= MAX_BANK_BAG_SLOTS)
            {
                return null; // No more slots can be purchased
            }

            return BankSlotCosts[_purchasedBankBagSlots];
        }

        /// <inheritdoc />
        public bool HasBankSpace(uint requiredSlots = 1)
        {
            // This would typically check the actual bank state
            // For now, we'll assume there's space if we have available slots
            return _availableBankSlots >= requiredSlots;
        }

        #endregion

        #region Convenience Methods

        /// <inheritdoc />
        public async Task QuickDepositAsync(ulong bankerGuid, byte bagId, byte slotId, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick deposit from bag {BagId}:{SlotId} with banker {BankerGuid:X}", bagId, slotId, bankerGuid);

            try
            {
                if (!_isBankWindowOpen)
                {
                    await OpenBankAsync(bankerGuid, cancellationToken);
                }

                await DepositItemAsync(bagId, slotId, quantity, cancellationToken);
                await CloseBankAsync(cancellationToken);

                _logger.LogInformation("Quick deposit completed for bag {BagId}:{SlotId}", bagId, slotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick deposit failed for bag {BagId}:{SlotId}", bagId, slotId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickWithdrawAsync(ulong bankerGuid, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick withdraw from bank slot {BankSlot} with banker {BankerGuid:X}", bankSlot, bankerGuid);

            try
            {
                if (!_isBankWindowOpen)
                {
                    await OpenBankAsync(bankerGuid, cancellationToken);
                }

                await WithdrawItemAsync(bankSlot, quantity, cancellationToken);
                await CloseBankAsync(cancellationToken);

                _logger.LogInformation("Quick withdraw completed for bank slot {BankSlot}", bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick withdraw failed for bank slot {BankSlot}", bankSlot);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickDepositGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick gold deposit of {Amount} copper with banker {BankerGuid:X}", amount, bankerGuid);

            try
            {
                if (!_isBankWindowOpen)
                {
                    await OpenBankAsync(bankerGuid, cancellationToken);
                }

                await DepositGoldAsync(amount, cancellationToken);
                await CloseBankAsync(cancellationToken);

                _logger.LogInformation("Quick gold deposit completed for {Amount} copper", amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick gold deposit failed for {Amount} copper", amount);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickWithdrawGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick gold withdraw of {Amount} copper with banker {BankerGuid:X}", amount, bankerGuid);

            try
            {
                if (!_isBankWindowOpen)
                {
                    await OpenBankAsync(bankerGuid, cancellationToken);
                }

                await WithdrawGoldAsync(amount, cancellationToken);
                await CloseBankAsync(cancellationToken);

                _logger.LogInformation("Quick gold withdraw completed for {Amount} copper", amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick gold withdraw failed for {Amount} copper", amount);
                throw;
            }
        }

        #endregion

        #region Server Response Handlers

        /// <summary>
        /// Handles bank window opened response from the server.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        public void HandleBankWindowOpened(ulong bankerGuid)
        {
            _currentBankerGuid = bankerGuid;
            _isBankWindowOpen = true;
            
            BankWindowOpened?.Invoke(bankerGuid);
            _logger.LogDebug("Bank window opened event handled for banker: {BankerGuid:X}", bankerGuid);
        }

        /// <summary>
        /// Handles bank window closed response from the server.
        /// </summary>
        public void HandleBankWindowClosed()
        {
            _isBankWindowOpen = false;
            _currentBankerGuid = null;
            
            BankWindowClosed?.Invoke();
            _logger.LogDebug("Bank window closed event handled");
        }

        /// <summary>
        /// Handles item deposited response from the server.
        /// </summary>
        /// <param name="itemGuid">The GUID of the deposited item.</param>
        /// <param name="itemId">The ID of the deposited item.</param>
        /// <param name="quantity">The quantity deposited.</param>
        /// <param name="bankSlot">The bank slot where the item was deposited.</param>
        public void HandleItemDeposited(ulong itemGuid, uint itemId, uint quantity, byte bankSlot)
        {
            ItemDeposited?.Invoke(itemGuid, itemId, quantity, bankSlot);
            _logger.LogDebug("Item deposited event handled: {ItemId} (GUID: {ItemGuid:X}), Quantity: {Quantity}, Bank Slot: {BankSlot}", 
                itemId, itemGuid, quantity, bankSlot);
        }

        /// <summary>
        /// Handles item withdrawn response from the server.
        /// </summary>
        /// <param name="itemGuid">The GUID of the withdrawn item.</param>
        /// <param name="itemId">The ID of the withdrawn item.</param>
        /// <param name="quantity">The quantity withdrawn.</param>
        /// <param name="bagSlot">The bag slot where the item was placed.</param>
        public void HandleItemWithdrawn(ulong itemGuid, uint itemId, uint quantity, byte bagSlot)
        {
            ItemWithdrawn?.Invoke(itemGuid, itemId, quantity, bagSlot);
            _logger.LogDebug("Item withdrawn event handled: {ItemId} (GUID: {ItemGuid:X}), Quantity: {Quantity}, Bag Slot: {BagSlot}", 
                itemId, itemGuid, quantity, bagSlot);
        }

        /// <summary>
        /// Handles items swapped response from the server.
        /// </summary>
        /// <param name="inventoryItemGuid">The GUID of the inventory item.</param>
        /// <param name="bankItemGuid">The GUID of the bank item.</param>
        public void HandleItemsSwapped(ulong inventoryItemGuid, ulong bankItemGuid)
        {
            ItemsSwapped?.Invoke(inventoryItemGuid, bankItemGuid);
            _logger.LogDebug("Items swapped event handled: Inventory {InventoryGuid:X} <-> Bank {BankGuid:X}", 
                inventoryItemGuid, bankItemGuid);
        }

        /// <summary>
        /// Handles gold deposited response from the server.
        /// </summary>
        /// <param name="amount">The amount of gold deposited in copper.</param>
        public void HandleGoldDeposited(uint amount)
        {
            GoldDeposited?.Invoke(amount);
            _logger.LogDebug("Gold deposited event handled: {Amount} copper", amount);
        }

        /// <summary>
        /// Handles gold withdrawn response from the server.
        /// </summary>
        /// <param name="amount">The amount of gold withdrawn in copper.</param>
        public void HandleGoldWithdrawn(uint amount)
        {
            GoldWithdrawn?.Invoke(amount);
            _logger.LogDebug("Gold withdrawn event handled: {Amount} copper", amount);
        }

        /// <summary>
        /// Handles bank slot purchased response from the server.
        /// </summary>
        /// <param name="slotIndex">The index of the purchased slot.</param>
        /// <param name="cost">The cost paid in copper.</param>
        public void HandleBankSlotPurchased(byte slotIndex, uint cost)
        {
            _purchasedBankBagSlots = Math.Max(_purchasedBankBagSlots, (uint)(slotIndex + 1));
            
            BankSlotPurchased?.Invoke(slotIndex, cost);
            _logger.LogDebug("Bank slot purchased event handled: Slot {SlotIndex}, Cost: {Cost} copper", slotIndex, cost);
        }

        /// <summary>
        /// Handles bank information update from the server.
        /// </summary>
        /// <param name="availableSlots">Number of available bank slots.</param>
        /// <param name="purchasedBagSlots">Number of purchased bag slots.</param>
        public void HandleBankInfoUpdate(uint availableSlots, uint purchasedBagSlots)
        {
            _availableBankSlots = availableSlots;
            _purchasedBankBagSlots = purchasedBagSlots;
            
            BankInfoUpdated?.Invoke(availableSlots, purchasedBagSlots);
            _logger.LogDebug("Bank info updated event handled: Available: {Available}, Purchased: {Purchased}", 
                availableSlots, purchasedBagSlots);
        }

        /// <summary>
        /// Handles bank operation error from the server.
        /// </summary>
        /// <param name="operation">The type of operation that failed.</param>
        /// <param name="errorMessage">The error message.</param>
        public void HandleBankOperationError(BankOperationType operation, string errorMessage)
        {
            BankOperationFailed?.Invoke(operation, errorMessage);
            _logger.LogWarning("Bank operation error handled: {Operation} - {Error}", operation, errorMessage);
        }

        #endregion
    }
}