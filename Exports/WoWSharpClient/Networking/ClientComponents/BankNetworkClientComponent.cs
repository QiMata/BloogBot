using System.Reactive;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of the bank network agent for handling personal bank operations.
    /// Manages depositing and withdrawing items or gold from the player's personal bank.
    /// Uses opcode-driven observables instead of events.
    /// </summary>
    public class BankNetworkClientComponent : NetworkClientComponent, IBankNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<BankNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        // Bank state tracking
        private bool _isBankWindowOpen;
        private ulong? _currentBankerGuid;
        private uint _availableBankSlots;
        private uint _purchasedBankBagSlots;
        private bool _disposed;

        // Constants for bank operations
        private const byte BANK_SLOT_COUNT = 24; // Standard bank slots
        private const byte MAX_BANK_BAG_SLOTS = 7; // Maximum purchasable bag slots
        private static readonly uint[] BankSlotCosts = [1000, 7500, 15000, 37500, 75000, 150000, 300000]; // Costs in copper

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the BankNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance for the bank agent.</param>
        public BankNetworkClientComponent(IWorldClient worldClient, ILogger<BankNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogDebug("BankNetworkClientComponent initialized");
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public bool IsBankWindowOpen => _isBankWindowOpen;

        /// <inheritdoc />
        public uint AvailableBankSlots => _availableBankSlots;

        /// <inheritdoc />
        public uint PurchasedBankBagSlots => _purchasedBankBagSlots;

        #endregion

        #region Reactive Observables (lazy, opcode-driven)
        public IObservable<ulong> BankWindowOpenedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_SHOW_BANK)
                .Select(_ =>
                {
                    // When server shows bank, mark open state
                    _isBankWindowOpen = true;
                    return _currentBankerGuid;
                })
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
        );

        public IObservable<Unit> BankWindowClosedStream => Observable.Defer(() =>
            (_worldClient.WhenDisconnected ?? Observable.Empty<Exception?>()).Select(_ => Unit.Default)
        );

        public IObservable<ItemMovementData> ItemDepositedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_ITEM_PUSH_RESULT)
                .Select(payload => ParseItemMovement(payload))
                .Where(m => m != null)
                .Select(m => m!)
        );

        public IObservable<ItemMovementData> ItemWithdrawnStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_ITEM_PUSH_RESULT)
                .Select(payload => ParseItemMovement(payload))
                .Where(m => m != null)
                .Select(m => m!)
        );

        public IObservable<ItemsSwappedData> ItemsSwappedStream => Observable.Defer(() =>
            Observable.Empty<ItemsSwappedData>() // No dedicated opcode available; integrators can extend parsing
        );

        public IObservable<uint> GoldDepositedStream => Observable.Empty<uint>();

        public IObservable<uint> GoldWithdrawnStream => Observable.Empty<uint>();

        public IObservable<BankSlotPurchaseData> BankSlotPurchasedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_BUY_BANK_SLOT_RESULT)
                .Select(ParseBuyBankSlotResult)
                .Where(r => r.Success)
                .Select(_ =>
                {
                    var slotIndex = (byte)_purchasedBankBagSlots;
                    var cost = BankSlotCosts[Math.Min(_purchasedBankBagSlots, (uint)BankSlotCosts.Length - 1)];
                    _purchasedBankBagSlots = Math.Min(_purchasedBankBagSlots + 1, MAX_BANK_BAG_SLOTS);
                    return new BankSlotPurchaseData(slotIndex, cost);
                })
        );

        public IObservable<BankInfoData> BankInfoUpdatedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_SHOW_BANK)
                .Select(_ =>
                {
                    // Default behavior: server shows bank; set baseline values if unset
                    if (_availableBankSlots == 0)
                        _availableBankSlots = BANK_SLOT_COUNT;
                    return new BankInfoData(_availableBankSlots, _purchasedBankBagSlots);
                })
        );

        public IObservable<BankOperationErrorData> BankOperationFailedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_BUY_BANK_SLOT_RESULT)
                .Select(ParseBuyBankSlotResult)
                .Where(r => !r.Success)
                .Select(r => new BankOperationErrorData(BankOperationType.PurchaseSlot, r.ErrorMessage ?? "Unknown error"))
        );
        #endregion

        #region Core Bank Operations

        /// <inheritdoc />
        public async Task OpenBankAsync(ulong bankerGuid, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Opening bank with banker: {BankerGuid:X}", bankerGuid);

            try
            {
                SetOperationInProgress(true);

                // Send CMSG_BANKER_ACTIVATE to open the bank
                var payload = new byte[8];
                BitConverter.GetBytes(bankerGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BANKER_ACTIVATE, payload, cancellationToken);

                _currentBankerGuid = bankerGuid;
                _isBankWindowOpen = true;

                // Request bank information to update slot counts (best-effort)
                await RequestBankInfoAsync(cancellationToken);

                _logger.LogInformation("Bank window opened with banker: {BankerGuid:X}", bankerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open bank with banker: {BankerGuid:X}", bankerGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CloseBankAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Closing bank window");

            try
            {
                SetOperationInProgress(true);

                if (_isBankWindowOpen)
                {
                    _isBankWindowOpen = false;
                    _currentBankerGuid = null;
                    _logger.LogInformation("Bank window closed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close bank window");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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
                _logger.LogWarning("Bank window is not open");
                return;
            }

            var emptySlot = FindEmptyBankSlot();
            if (!emptySlot.HasValue)
            {
                _logger.LogWarning("No empty bank slots available");
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
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Send item swap packet from inventory to bank using CMSG_SWAP_INV_ITEM
                var payload = new byte[2];
                payload[0] = (byte)(sourceBagId * 16 + sourceSlotId); // source slot
                payload[1] = (byte)(BANK_SLOT_COUNT + bankSlot); // destination bank slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item deposited from {BagId}:{SlotId} to bank slot {BankSlot}", sourceBagId, sourceSlotId, bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deposit item from {BagId}:{SlotId} to bank slot {BankSlot}", sourceBagId, sourceSlotId, bankSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task WithdrawItemAsync(byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot}, quantity: {Quantity}", bankSlot, quantity);

            if (!_isBankWindowOpen)
            {
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Send item move packet from bank to first available inventory slot using CMSG_AUTOBANK_ITEM
                var payload = new byte[1];
                payload[0] = (byte)(BANK_SLOT_COUNT + bankSlot);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOBANK_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item withdrawn from bank slot {BankSlot}", bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw item from bank slot {BankSlot}", bankSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task WithdrawItemToSlotAsync(byte bankSlot, byte targetBagId, byte targetSlotId, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot} to bag {BagId}:{SlotId}, quantity: {Quantity}", 
                bankSlot, targetBagId, targetSlotId, quantity);

            if (!_isBankWindowOpen)
            {
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Send item swap packet from bank to specific inventory slot
                var payload = new byte[2];
                payload[0] = (byte)(BANK_SLOT_COUNT + bankSlot); // source bank slot
                payload[1] = (byte)(targetBagId * 16 + targetSlotId); // destination slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item withdrawn from bank slot {BankSlot} to {BagId}:{SlotId}", bankSlot, targetBagId, targetSlotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw item from bank slot {BankSlot} to {BagId}:{SlotId}", bankSlot, targetBagId, targetSlotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SwapItemWithBankAsync(byte inventoryBagId, byte inventorySlotId, byte bankSlot, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Swapping item in {BagId}:{SlotId} with bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);

            if (!_isBankWindowOpen)
            {
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Send item swap packet between inventory and bank
                var payload = new byte[2];
                payload[0] = (byte)(inventoryBagId * 16 + inventorySlotId); // inventory slot
                payload[1] = (byte)(BANK_SLOT_COUNT + bankSlot); // bank slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item swapped between {BagId}:{SlotId} and bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap item between {BagId}:{SlotId} and bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Not implemented - protocol needs clarification
                _logger.LogWarning("Gold deposit operation not fully implemented - packet structure needs verification");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deposit {Amount} copper to bank", amount);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task WithdrawGoldAsync(uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Withdrawing {Amount} copper from bank", amount);

            if (!_isBankWindowOpen)
            {
                _logger.LogWarning("Bank window is not open");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Not implemented - protocol needs clarification
                _logger.LogWarning("Gold withdrawal operation not fully implemented - packet structure needs verification");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to withdraw {Amount} copper from bank", amount);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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
                _logger.LogWarning("Bank window is not open");
                return;
            }

            if (_purchasedBankBagSlots >= MAX_BANK_BAG_SLOTS)
            {
                _logger.LogWarning("Maximum bank bag slots already purchased");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // Send CMSG_BUY_BANK_SLOT
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_BANK_SLOT, [], cancellationToken);

                _logger.LogInformation("Purchase bank bag slot command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purchase bank bag slot");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task RequestBankInfoAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Requesting bank information");

            try
            {
                SetOperationInProgress(true);

                // Typically handled by server responses when opening the bank
                _availableBankSlots = BANK_SLOT_COUNT;
                _logger.LogDebug("Bank info updated - Available slots: {Available}, Purchased bag slots: {Purchased}", 
                    _availableBankSlots, _purchasedBankBagSlots);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request bank information");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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
            if (HasBankSpace())
            {
                return 0; // Return first available slot (placeholder)
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
                SetOperationInProgress(true);

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
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QuickWithdrawAsync(ulong bankerGuid, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick withdraw from bank slot {BankSlot} with banker {BankerGuid:X}", bankSlot, bankerGuid);

            try
            {
                SetOperationInProgress(true);

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
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QuickDepositGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick gold deposit of {Amount} copper with banker {BankerGuid:X}", amount, bankerGuid);

            try
            {
                SetOperationInProgress(true);

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
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task QuickWithdrawGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Quick gold withdraw of {Amount} copper with banker {BankerGuid:X}", amount, bankerGuid);

            try
            {
                SetOperationInProgress(true);

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
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Server Response Handlers (state updates only)

        /// <summary>
        /// Handles bank window opened response from the server.
        /// </summary>
        /// <param name="bankerGuid">The GUID of the banker NPC.</param>
        public void HandleBankWindowOpened(ulong bankerGuid)
        {
            _currentBankerGuid = bankerGuid;
            _isBankWindowOpen = true;
            _logger.LogDebug("Bank window opened event handled for banker: {BankerGuid:X}", bankerGuid);
        }

        /// <summary>
        /// Handles bank window closed response from the server.
        /// </summary>
        public void HandleBankWindowClosed()
        {
            _isBankWindowOpen = false;
            _currentBankerGuid = null;
            _logger.LogDebug("Bank window closed event handled");
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
            _logger.LogDebug("Bank info updated event handled: Available: {Available}, Purchased: {Purchased}", 
                availableSlots, purchasedBagSlots);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the bank network client component and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("BankNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion

        #region Parsing helpers and SafeStream
        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private ItemMovementData? ParseItemMovement(ReadOnlyMemory<byte> payload)
        {
            // Heuristic parsing: [itemGuid(8)][itemId(4)][quantity(4)][slot(1)]
            var span = payload.Span;
            if (span.Length < 17) return null;
            ulong itemGuid = BitConverter.ToUInt64(span.Slice(0, 8));
            uint itemId = BitConverter.ToUInt32(span.Slice(8, 4));
            uint qty = BitConverter.ToUInt32(span.Slice(12, 4));
            byte slot = span[16];
            return new ItemMovementData(itemGuid, itemId, qty, slot);
        }

        private (bool Success, BuyBankSlotResult Result, string? ErrorMessage) ParseBuyBankSlotResult(ReadOnlyMemory<byte> payload)
        {
            // Heuristic parsing: first byte is result
            var span = payload.Span;
            if (span.Length == 0) return (false, BuyBankSlotResult.ERR_BANKSLOT_FAILED_TOO_MANY, "Malformed payload");
            var result = (BuyBankSlotResult)span[0];
            bool success = result == BuyBankSlotResult.ERR_BANKSLOT_OK;
            string? error = success ? null : result switch
            {
                BuyBankSlotResult.ERR_BANKSLOT_FAILED_TOO_MANY => "Too many bank slots",
                BuyBankSlotResult.ERR_BANKSLOT_INSUFFICIENT_FUNDS => "Insufficient funds",
                BuyBankSlotResult.ERR_BANKSLOT_NOTBANKER => "Not a banker",
                _ => "Unknown error"
            };
            return (success, result, error);
        }
        #endregion
    }
}