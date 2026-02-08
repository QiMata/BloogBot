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
    ///
    /// MaNGOS 1.12.1 bank slot layout (bag 0xFF):
    ///   Slots 39-62 = bank item storage (24 slots, BANK_SLOT_ITEM_START..END)
    ///   Slots 63-68 = purchasable bank bag slots (6 max, BANK_SLOT_BAG_START..END)
    ///
    /// Key opcodes:
    ///   CMSG_BANKER_ACTIVATE       (0x1B7): guid(8)
    ///   SMSG_SHOW_BANK             (0x1B8): bankerGuid(8)
    ///   CMSG_BUY_BANK_SLOT         (0x1B9): bankerGuid(8)
    ///   SMSG_BUY_BANK_SLOT_RESULT  (0x1BA): result(4, uint32)
    ///   CMSG_AUTOBANK_ITEM         (0x283): srcBag(1) + srcSlot(1) — inventory → bank
    ///   CMSG_AUTOSTORE_BANK_ITEM   (0x282): srcBag(1) + srcSlot(1) — bank → inventory
    ///   CMSG_SWAP_ITEM             (0x10C): dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1)
    ///
    /// Note: Personal gold banking does not exist in vanilla 1.12.1.
    /// Guild banks (with gold deposit/withdraw) were added in TBC 2.0.
    /// </summary>
    public class BankNetworkClientComponent : NetworkClientComponent, IBankNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<BankNetworkClientComponent> _logger;

        private bool _isBankWindowOpen;
        private ulong? _currentBankerGuid;
        private uint _availableBankSlots;
        private uint _purchasedBankBagSlots;
        private bool _disposed;

        private const byte BANK_SLOT_ITEM_START = 39;
        private const byte BANK_SLOT_ITEM_COUNT = 24;
        private const byte BANK_BAG = 0xFF;
        private const byte MAX_BANK_BAG_SLOTS = 7;
        private static readonly uint[] BankSlotCosts = [1000, 7500, 15000, 37500, 75000, 150000, 300000];

        #region Constructor

        public BankNetworkClientComponent(IWorldClient worldClient, ILogger<BankNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                .Select(ParseShowBank)
                .Where(g => g.HasValue)
                .Select(g =>
                {
                    _isBankWindowOpen = true;
                    _currentBankerGuid = g!.Value;
                    _availableBankSlots = BANK_SLOT_ITEM_COUNT;
                    return g.Value;
                })
        );

        public IObservable<Unit> BankWindowClosedStream => Observable.Defer(() =>
            (_worldClient.WhenDisconnected ?? Observable.Empty<Exception?>()).Select(_ => Unit.Default)
        );

        public IObservable<ItemMovementData> ItemDepositedStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_ITEM_PUSH_RESULT)
                .Select(ParseItemMovement)
                .Where(m => m != null)
                .Select(m => m!)
        );

        public IObservable<ItemMovementData> ItemWithdrawnStream => Observable.Defer(() =>
            SafeStream(Opcode.SMSG_ITEM_PUSH_RESULT)
                .Select(ParseItemMovement)
                .Where(m => m != null)
                .Select(m => m!)
        );

        public IObservable<ItemsSwappedData> ItemsSwappedStream => Observable.Defer(() =>
            Observable.Empty<ItemsSwappedData>()
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
                    if (_availableBankSlots == 0)
                        _availableBankSlots = BANK_SLOT_ITEM_COUNT;
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

                // CMSG_BANKER_ACTIVATE: guid(8)
                var payload = new byte[8];
                BitConverter.GetBytes(bankerGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BANKER_ACTIVATE, payload, cancellationToken);

                _currentBankerGuid = bankerGuid;
                _isBankWindowOpen = true;
                _availableBankSlots = BANK_SLOT_ITEM_COUNT;

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
            _logger.LogDebug("Depositing item from bag {BagId}:{SlotId}", bagId, slotId);

            if (!_isBankWindowOpen)
                throw new InvalidOperationException("Bank window is not open");

            try
            {
                SetOperationInProgress(true);

                // CMSG_AUTOBANK_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
                // Server auto-finds an empty bank slot
                var payload = new byte[2];
                payload[0] = bagId;
                payload[1] = slotId;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOBANK_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auto-deposit sent from {BagId}:{SlotId}", bagId, slotId);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Failed to deposit item from {BagId}:{SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task DepositItemToSlotAsync(byte sourceBagId, byte sourceSlotId, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Depositing item from bag {BagId}:{SlotId} to bank slot {BankSlot}",
                sourceBagId, sourceSlotId, bankSlot);

            if (!_isBankWindowOpen)
                throw new InvalidOperationException("Bank window is not open");

            try
            {
                SetOperationInProgress(true);

                // CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
                var payload = new byte[4];
                payload[0] = BANK_BAG;                              // dstBag: bank container
                payload[1] = (byte)(BANK_SLOT_ITEM_START + bankSlot); // dstSlot: bank slot
                payload[2] = sourceBagId;                           // srcBag: inventory bag
                payload[3] = sourceSlotId;                          // srcSlot: inventory slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item deposited from {BagId}:{SlotId} to bank slot {BankSlot}", sourceBagId, sourceSlotId, bankSlot);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
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
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot}", bankSlot);

            if (!_isBankWindowOpen)
                throw new InvalidOperationException("Bank window is not open");

            try
            {
                SetOperationInProgress(true);

                // CMSG_AUTOSTORE_BANK_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
                // Server auto-finds an empty inventory slot
                var payload = new byte[2];
                payload[0] = BANK_BAG;                              // srcBag: bank container
                payload[1] = (byte)(BANK_SLOT_ITEM_START + bankSlot); // srcSlot: bank slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_BANK_ITEM, payload, cancellationToken);

                _logger.LogInformation("Auto-withdraw sent from bank slot {BankSlot}", bankSlot);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
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
            _logger.LogDebug("Withdrawing item from bank slot {BankSlot} to bag {BagId}:{SlotId}",
                bankSlot, targetBagId, targetSlotId);

            if (!_isBankWindowOpen)
                throw new InvalidOperationException("Bank window is not open");

            try
            {
                SetOperationInProgress(true);

                // CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
                var payload = new byte[4];
                payload[0] = targetBagId;                           // dstBag: inventory bag
                payload[1] = targetSlotId;                          // dstSlot: inventory slot
                payload[2] = BANK_BAG;                              // srcBag: bank container
                payload[3] = (byte)(BANK_SLOT_ITEM_START + bankSlot); // srcSlot: bank slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item withdrawn from bank slot {BankSlot} to {BagId}:{SlotId}", bankSlot, targetBagId, targetSlotId);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
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
                throw new InvalidOperationException("Bank window is not open");

            try
            {
                SetOperationInProgress(true);

                // CMSG_SWAP_ITEM: dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
                var payload = new byte[4];
                payload[0] = BANK_BAG;                              // dstBag: bank container
                payload[1] = (byte)(BANK_SLOT_ITEM_START + bankSlot); // dstSlot: bank slot
                payload[2] = inventoryBagId;                        // srcBag: inventory bag
                payload[3] = inventorySlotId;                       // srcSlot: inventory slot
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);

                _logger.LogInformation("Item swapped between {BagId}:{SlotId} and bank slot {BankSlot}", inventoryBagId, inventorySlotId, bankSlot);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
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
            // Personal gold banking does not exist in vanilla 1.12.1.
            // Guild banks with gold deposit/withdraw were added in TBC 2.0.
            _logger.LogWarning("Gold deposit not available in vanilla 1.12.1 (guild bank feature, TBC+)");
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task WithdrawGoldAsync(uint amount, CancellationToken cancellationToken = default)
        {
            // Personal gold banking does not exist in vanilla 1.12.1.
            _logger.LogWarning("Gold withdrawal not available in vanilla 1.12.1 (guild bank feature, TBC+)");
            await Task.CompletedTask;
        }

        #endregion

        #region Bank Management

        /// <inheritdoc />
        public async Task PurchaseBankSlotAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Purchasing bank bag slot");

            if (!_isBankWindowOpen || !_currentBankerGuid.HasValue)
                throw new InvalidOperationException("Bank window is not open");

            if (_purchasedBankBagSlots >= MAX_BANK_BAG_SLOTS)
            {
                _logger.LogWarning("Maximum bank bag slots already purchased");
                return;
            }

            try
            {
                SetOperationInProgress(true);

                // CMSG_BUY_BANK_SLOT: bankerGuid(8)
                var payload = new byte[8];
                BitConverter.GetBytes(_currentBankerGuid.Value).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_BANK_SLOT, payload, cancellationToken);

                _logger.LogInformation("Purchase bank bag slot command sent");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
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
            // Bank info is delivered via SMSG_SHOW_BANK (bankerGuid) when opening the bank.
            // No separate request opcode exists; info comes from the server automatically.
            _availableBankSlots = BANK_SLOT_ITEM_COUNT;
            _logger.LogDebug("Bank info set - Available item slots: {Available}, Purchased bag slots: {Purchased}",
                _availableBankSlots, _purchasedBankBagSlots);
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
                return 0; // Placeholder — real slot tracking requires inventory state from SMSG_UPDATE_OBJECT
            }
            return null;
        }

        /// <inheritdoc />
        public uint? GetNextBankSlotCost()
        {
            if (_purchasedBankBagSlots >= MAX_BANK_BAG_SLOTS)
                return null;

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
            if (!_isBankWindowOpen)
                await OpenBankAsync(bankerGuid, cancellationToken);

            await DepositItemAsync(bagId, slotId, quantity, cancellationToken);
            await CloseBankAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task QuickWithdrawAsync(ulong bankerGuid, byte bankSlot, uint quantity = 0, CancellationToken cancellationToken = default)
        {
            if (!_isBankWindowOpen)
                await OpenBankAsync(bankerGuid, cancellationToken);

            await WithdrawItemAsync(bankSlot, quantity, cancellationToken);
            await CloseBankAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task QuickDepositGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            if (!_isBankWindowOpen)
                await OpenBankAsync(bankerGuid, cancellationToken);

            await DepositGoldAsync(amount, cancellationToken);
            await CloseBankAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task QuickWithdrawGoldAsync(ulong bankerGuid, uint amount, CancellationToken cancellationToken = default)
        {
            if (!_isBankWindowOpen)
                await OpenBankAsync(bankerGuid, cancellationToken);

            await WithdrawGoldAsync(amount, cancellationToken);
            await CloseBankAsync(cancellationToken);
        }

        #endregion

        #region Server Response Handlers (state updates only)

        public void HandleBankWindowOpened(ulong bankerGuid)
        {
            _currentBankerGuid = bankerGuid;
            _isBankWindowOpen = true;
            _availableBankSlots = BANK_SLOT_ITEM_COUNT;
            _logger.LogDebug("Bank window opened event handled for banker: {BankerGuid:X}", bankerGuid);
        }

        public void HandleBankWindowClosed()
        {
            _isBankWindowOpen = false;
            _currentBankerGuid = null;
            _logger.LogDebug("Bank window closed event handled");
        }

        public void HandleBankInfoUpdate(uint availableSlots, uint purchasedBagSlots)
        {
            _availableBankSlots = availableSlots;
            _purchasedBankBagSlots = purchasedBagSlots;
            _logger.LogDebug("Bank info updated: Available: {Available}, Purchased: {Purchased}",
                availableSlots, purchasedBagSlots);
        }

        #endregion

        #region IDisposable Implementation

        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("BankNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion

        #region Parsing helpers

        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        /// <summary>
        /// Parses SMSG_SHOW_BANK: bankerGuid(8)
        /// </summary>
        private ulong? ParseShowBank(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 8) return null;
            return BitConverter.ToUInt64(span.Slice(0, 8));
        }

        /// <summary>
        /// Parses SMSG_ITEM_PUSH_RESULT: playerGuid(8) + newItem(4) + createdItem(4) + ...
        /// Minimal parse — extracts item GUID, entry, count, and destination slot.
        ///
        /// MaNGOS WorldSession::SendNewItem format:
        ///   playerGuid(8) + newItem(4) + createdFromSpell(4) + isCreated(4) +
        ///   containerSlot(4) + slot(4) + itemEntry(4) + suffixFactor(4) +
        ///   randomPropertyId(4) + count(4)
        /// Total: 44 bytes minimum
        /// </summary>
        private ItemMovementData? ParseItemMovement(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 44) return null;

            // Skip playerGuid(8) + newItem(4) + createdFromSpell(4) + isCreated(4)
            // containerSlot is at offset 20, slot at 24, itemEntry at 28
            byte slot = (byte)BitConverter.ToUInt32(span.Slice(24, 4));
            uint itemEntry = BitConverter.ToUInt32(span.Slice(28, 4));
            uint count = BitConverter.ToUInt32(span.Slice(40, 4));

            // We don't have the item GUID from this packet — use 0 as placeholder
            return new ItemMovementData(0, itemEntry, count, slot);
        }

        /// <summary>
        /// Parses SMSG_BUY_BANK_SLOT_RESULT: result(4, uint32)
        /// Result codes: 0=TooMany, 1=InsufficientFunds, 2=NotBanker, 3=OK
        /// </summary>
        private (bool Success, BuyBankSlotResult Result, string? ErrorMessage) ParseBuyBankSlotResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 4) return (false, BuyBankSlotResult.ERR_BANKSLOT_FAILED_TOO_MANY, "Malformed payload");

            var result = (BuyBankSlotResult)BitConverter.ToUInt32(span.Slice(0, 4));
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
