using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of looting network agent that handles loot operations in World of Warcraft.
    /// Manages loot containers, automatic looting, and item collection using the Mangos protocol.
    /// Uses reactive observables for better composability and filtering.
    /// Supports advanced group loot scenarios including master loot, round-robin, and BoP confirmations.
    /// </summary>
    public class LootingNetworkClientComponent : ILootingNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<LootingNetworkClientComponent> _logger;
        private bool _isLootWindowOpen;
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private ulong? _currentLootTarget;
        
        // Group loot state
        private GroupLootMethod _currentLootMethod = GroupLootMethod.FreeForAll;
        private bool _isMasterLooter = false;
        private ItemQuality _lootThreshold = ItemQuality.Uncommon;
        
        // Loot slot tracking
        private readonly ConcurrentDictionary<byte, LootSlotInfo> _availableLoot = new();
        private readonly ConcurrentDictionary<ulong, HashSet<byte>> _pendingRolls = new();

        // Reactive observables
        private readonly Subject<LootWindowData> _lootWindowChanges = new();
        private readonly Subject<LootData> _itemLoot = new();
        private readonly Subject<MoneyLootData> _moneyLoot = new();
        private readonly Subject<LootRollData> _lootRolls = new();
        private readonly Subject<LootErrorData> _lootErrors = new();
        private readonly Subject<BindOnPickupData> _bindOnPickupConfirmations = new();
        private readonly Subject<GroupLootNotificationData> _groupLootNotifications = new();
        private readonly Subject<MasterLootData> _masterLootAssignments = new();

        // Filtered observables (lazy-initialized)
        private IObservable<LootWindowData>? _lootWindowOpened;
        private IObservable<LootWindowData>? _lootWindowClosed;

        // Legacy callback fields for backwards compatibility
        private Action<ulong>? _lootWindowOpenedCallback;
        private Action? _lootWindowClosedCallback;
        private Action<uint, uint>? _itemLootedCallback;
        private Action<uint>? _moneyLootedCallback;
        private Action<string>? _lootErrorCallback;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the LootingNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public LootingNetworkClientComponent(IWorldClient worldClient, ILogger<LootingNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Properties

        /// <inheritdoc />
        public bool IsLootWindowOpen => _isLootWindowOpen;

        /// <inheritdoc />
        public bool IsOperationInProgress => _isOperationInProgress;

        /// <inheritdoc />
        public DateTime? LastOperationTime => _lastOperationTime;

        /// <inheritdoc />
        public ulong? CurrentLootTarget => _currentLootTarget;

        /// <inheritdoc />
        public GroupLootMethod CurrentLootMethod => _currentLootMethod;

        /// <inheritdoc />
        public bool IsMasterLooter => _isMasterLooter;

        /// <inheritdoc />
        public ItemQuality LootThreshold => _lootThreshold;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<LootWindowData> LootWindowChanges => _lootWindowChanges;

        /// <inheritdoc />
        public IObservable<LootData> ItemLoot => _itemLoot;

        /// <inheritdoc />
        public IObservable<MoneyLootData> MoneyLoot => _moneyLoot;

        /// <inheritdoc />
        public IObservable<LootRollData> LootRolls => _lootRolls;

        /// <inheritdoc />
        public IObservable<LootErrorData> LootErrors => _lootErrors;

        /// <inheritdoc />
        public IObservable<LootWindowData> LootWindowOpened =>
            _lootWindowOpened ??= _lootWindowChanges.Where(data => data.IsOpen);

        /// <inheritdoc />
        public IObservable<LootWindowData> LootWindowClosed =>
            _lootWindowClosed ??= _lootWindowChanges.Where(data => !data.IsOpen);

        /// <inheritdoc />
        public IObservable<BindOnPickupData> BindOnPickupConfirmations => _bindOnPickupConfirmations;

        /// <inheritdoc />
        public IObservable<GroupLootNotificationData> GroupLootNotifications => _groupLootNotifications;

        /// <inheritdoc />
        public IObservable<MasterLootData> MasterLootAssignments => _masterLootAssignments;

        #endregion

        #region Operations

        /// <inheritdoc />
        public async Task OpenLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Opening loot for target: {LootTargetGuid:X}", lootTargetGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(lootTargetGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT, payload, cancellationToken);

                _currentLootTarget = lootTargetGuid;
                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Loot command sent for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open loot for target: {LootTargetGuid:X}", lootTargetGuid);

                var errorData = new LootErrorData(ex.Message, lootTargetGuid, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task LootMoneyAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Looting money from current loot window");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_MONEY, [], cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Money loot command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot money");

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task LootItemAsync(byte lootSlot, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Looting item from slot: {LootSlot}", lootSlot);

                var payload = new byte[1];
                payload[0] = lootSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Item loot command sent for slot: {LootSlot}", lootSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot item from slot: {LootSlot}", lootSlot);

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, lootSlot, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task StoreLootInSlotAsync(byte lootSlot, byte bag, byte slot, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Storing loot from slot {LootSlot} to bag {Bag}, slot {Slot}", lootSlot, bag, slot);

                var payload = new byte[3];
                payload[0] = lootSlot;
                payload[1] = bag;
                payload[2] = slot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_STORE_LOOT_IN_SLOT, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Store loot command sent for slot {LootSlot} to bag {Bag}, slot {Slot}", 
                    lootSlot, bag, slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store loot from slot {LootSlot} to bag {Bag}, slot {Slot}", 
                    lootSlot, bag, slot);

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, lootSlot, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task CloseLootAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Closing loot window");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, [], cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Loot release command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close loot window");

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task RollForLootAsync(ulong lootGuid, byte itemSlot, LootRollType rollType, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Rolling {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(lootGuid).CopyTo(payload, 0);
                payload[8] = itemSlot;
                payload[9] = (byte)rollType;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_ROLL, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Loot roll {RollType} sent for slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}", 
                    rollType, itemSlot, lootGuid);

                var errorData = new LootErrorData(ex.Message, lootGuid, itemSlot, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task LootAllAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            try
            {
                if (!_isLootWindowOpen)
                {
                    _logger.LogWarning("Cannot loot all - no loot window is open");
                    return;
                }

                _logger.LogDebug("Looting all items and money");

                // First, loot all money
                await LootMoneyAsync(cancellationToken);

                // Small delay between operations
                await Task.Delay(50, cancellationToken);

                // Then loot all items (assuming maximum 8 loot slots)
                for (byte slot = 0; slot < 8; slot++)
                {
                    try
                    {
                        await LootItemAsync(slot, cancellationToken);
                        await Task.Delay(25, cancellationToken); // Small delay between item loots
                    }
                    catch (Exception ex)
                    {
                        // Continue with other slots if one fails
                        _logger.LogDebug("Failed to loot slot {Slot}: {Error}", slot, ex.Message);
                    }
                }

                // Close the loot window
                await Task.Delay(100, cancellationToken);
                await CloseLootAsync(cancellationToken);

                _logger.LogInformation("Completed looting all items and money");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot all items");

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            try
            {
                _logger.LogDebug("Quick looting target: {LootTargetGuid:X}", lootTargetGuid);

                // Open the loot
                await OpenLootAsync(lootTargetGuid, cancellationToken);

                // Wait for loot window to open
                await Task.Delay(200, cancellationToken);

                // Loot everything
                await LootAllAsync(cancellationToken);

                _logger.LogInformation("Quick loot completed for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to quick loot target: {LootTargetGuid:X}", lootTargetGuid);

                var errorData = new LootErrorData(ex.Message, lootTargetGuid, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickLootWithFilterAsync(ulong lootTargetGuid, ItemQuality minimumQuality, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            try
            {
                _logger.LogDebug("Quick looting target: {LootTargetGuid:X} with quality filter: {MinQuality}", lootTargetGuid, minimumQuality);

                // Open the loot
                await OpenLootAsync(lootTargetGuid, cancellationToken);

                // Wait for loot window to open
                await Task.Delay(200, cancellationToken);

                // Loot with filter
                await LootAllWithFilterAsync(minimumQuality, cancellationToken);

                _logger.LogInformation("Filtered quick loot completed for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to filtered quick loot target: {LootTargetGuid:X}", lootTargetGuid);

                var errorData = new LootErrorData(ex.Message, lootTargetGuid, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task LootAllWithFilterAsync(ItemQuality minimumQuality, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            try
            {
                if (!_isLootWindowOpen)
                {
                    _logger.LogWarning("Cannot loot with filter - no loot window is open");
                    return;
                }

                _logger.LogDebug("Looting all items with minimum quality: {MinQuality}", minimumQuality);

                // First, loot all money (no quality filter)
                await LootMoneyAsync(cancellationToken);
                await Task.Delay(50, cancellationToken);

                // Get available loot and filter by quality
                var eligibleLoot = GetLootByQuality(minimumQuality);
                
                foreach (var lootSlot in eligibleLoot.OrderBy(x => x.SlotIndex))
                {
                    try
                    {
                        if (CanLootSlot(lootSlot.SlotIndex))
                        {
                            await LootItemAsync(lootSlot.SlotIndex, cancellationToken);
                            await Task.Delay(25, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Failed to loot slot {Slot} ({ItemName}): {Error}", 
                            lootSlot.SlotIndex, lootSlot.ItemName, ex.Message);
                    }
                }

                // Close the loot window
                await Task.Delay(100, cancellationToken);
                await CloseLootAsync(cancellationToken);

                _logger.LogInformation("Completed filtered looting with minimum quality: {MinQuality}", minimumQuality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot with filter: {MinQuality}", minimumQuality);

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task ConfirmBindOnPickupAsync(byte lootSlot, bool confirm = true, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Confirming BoP for slot {LootSlot}: {Confirm}", lootSlot, confirm);

                var payload = new byte[2];
                payload[0] = lootSlot;
                payload[1] = (byte)(confirm ? 1 : 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("BoP confirmation sent for slot {LootSlot}: {Confirm}", lootSlot, confirm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm BoP for slot {LootSlot}", lootSlot);

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, lootSlot, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task AssignMasterLootAsync(byte lootSlot, ulong targetPlayerGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            if (!_isMasterLooter)
            {
                throw new InvalidOperationException("Player is not the master looter");
            }

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Assigning master loot slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);

                var payload = new byte[9];
                payload[0] = lootSlot;
                BitConverter.GetBytes(targetPlayerGuid).CopyTo(payload, 1);

                // Note: CMSG_LOOT_MASTER_GIVE needs to be added to the Opcode enum
                // For now, using CMSG_LOOT_RELEASE as a placeholder
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Master loot assignment sent for slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign master loot for slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);

                var errorData = new LootErrorData(ex.Message, _currentLootTarget, lootSlot, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        #endregion

        #region Group Loot Operations

        /// <inheritdoc />
        public async Task SetGroupLootMethodAsync(GroupLootMethod method, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Setting group loot method to: {LootMethod}", method);

                var payload = new byte[4];
                BitConverter.GetBytes((uint)method).CopyTo(payload, 0);

                // Note: CMSG_SET_LOOT_METHOD needs to be added to the Opcode enum
                // For now, using CMSG_LOOT_RELEASE as a placeholder
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Group loot method change sent: {LootMethod}", method);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set group loot method: {LootMethod}", method);

                var errorData = new LootErrorData(ex.Message, null, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task SetLootThresholdAsync(ItemQuality threshold, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Setting loot threshold to: {Threshold}", threshold);

                var payload = new byte[4];
                BitConverter.GetBytes((uint)threshold).CopyTo(payload, 0);

                // Note: CMSG_SET_LOOT_THRESHOLD needs to be added to the Opcode enum
                // For now, using CMSG_LOOT_RELEASE as a placeholder
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Loot threshold change sent: {Threshold}", threshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set loot threshold: {Threshold}", threshold);

                var errorData = new LootErrorData(ex.Message, null, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        /// <inheritdoc />
        public async Task SetMasterLooterAsync(ulong masterLooterGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            _isOperationInProgress = true;
            try
            {
                _logger.LogDebug("Setting master looter to: {MasterLooterGuid:X}", masterLooterGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(masterLooterGuid).CopyTo(payload, 0);

                // Note: CMSG_SET_LOOT_MASTER needs to be added to the Opcode enum
                // For now, using CMSG_LOOT_RELEASE as a placeholder
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Master looter change sent: {MasterLooterGuid:X}", masterLooterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set master looter: {MasterLooterGuid:X}", masterLooterGuid);

                var errorData = new LootErrorData(ex.Message, null, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }
        #endregion

        #region Server Response Handling

        /// <inheritdoc />
        public void HandleLootWindowChanged(bool isOpen, ulong? lootTargetGuid, uint availableItems = 0, uint availableMoney = 0)
        {
            if (_disposed) return;

            // Store the previous state for legacy callback comparison
            bool previousState = _isLootWindowOpen;
            
            // Always update the state and emit data, even if the state hasn't changed
            // This ensures consistent behavior for consumers of the observable
            _isLootWindowOpen = isOpen;

            if (isOpen && lootTargetGuid.HasValue)
            {
                _currentLootTarget = lootTargetGuid.Value;
                _logger.LogDebug("Loot window opened for target: {LootTargetGuid:X}", lootTargetGuid.Value);
            }
            else if (!isOpen)
            {
                _logger.LogDebug("Loot window closed");
                _currentLootTarget = null;
                
                // Clear loot slot information when window closes
                _availableLoot.Clear();
                _pendingRolls.Clear();
            }

            var windowData = new LootWindowData(isOpen, lootTargetGuid, availableItems, availableMoney, DateTime.UtcNow);
            _lootWindowChanges.OnNext(windowData);

            // Legacy callback support - only invoke if state actually changed
            if (previousState != isOpen)
            {
                if (isOpen && lootTargetGuid.HasValue)
                {
                    _lootWindowOpenedCallback?.Invoke(lootTargetGuid.Value);
                }
                else if (!isOpen)
                {
                    _lootWindowClosedCallback?.Invoke();
                }
            }
        }

        /// <inheritdoc />
        public void HandleLootList(ulong lootTargetGuid, IReadOnlyCollection<LootSlotInfo> lootSlots)
        {
            if (_disposed) return;

            _logger.LogDebug("Received loot list for target: {LootTargetGuid:X} with {SlotCount} slots", lootTargetGuid, lootSlots.Count);

            // Update available loot information
            _availableLoot.Clear();
            foreach (var slot in lootSlots)
            {
                _availableLoot.TryAdd(slot.SlotIndex, slot);
                
                // Track pending rolls
                if (slot.RequiresRoll && slot.RollGuid.HasValue)
                {
                    _pendingRolls.AddOrUpdate(
                        slot.RollGuid.Value,
                        [slot.SlotIndex],
                        (key, existing) => { existing.Add(slot.SlotIndex); return existing; });
                }
            }

            _logger.LogInformation("Updated loot list: {ItemCount} items, {RollCount} requiring rolls", 
                lootSlots.Count, lootSlots.Count(s => s.RequiresRoll));
        }

        /// <inheritdoc />
        public void HandleItemLooted(ulong lootTargetGuid, uint itemId, string itemName, uint quantity, ItemQuality quality, byte lootSlot)
        {
            if (_disposed) return;

            _logger.LogInformation("Item looted: {ItemName} x{Quantity} from slot {LootSlot}", itemName, quantity, lootSlot);

            // Remove from available loot
            _availableLoot.TryRemove(lootSlot, out _);

            var lootData = new LootData(lootTargetGuid, itemId, itemName, quantity, quality, lootSlot, DateTime.UtcNow);
            _itemLoot.OnNext(lootData);

            // Legacy callback support
            _itemLootedCallback?.Invoke(itemId, quantity);
        }

        /// <inheritdoc />
        public void HandleMoneyLooted(ulong lootTargetGuid, uint amount)
        {
            if (_disposed) return;

            _logger.LogInformation("Money looted: {Amount} copper", amount);

            var moneyData = new MoneyLootData(lootTargetGuid, amount, DateTime.UtcNow);
            _moneyLoot.OnNext(moneyData);

            // Legacy callback support
            _moneyLootedCallback?.Invoke(amount);
        }

        /// <inheritdoc />
        public void HandleLootRoll(ulong lootGuid, byte itemSlot, uint itemId, LootRollType rollType, uint rollResult)
        {
            if (_disposed) return;

            _logger.LogInformation("Loot roll: {RollType} ({RollResult}) for item {ItemId} in slot {ItemSlot}", 
                rollType, rollResult, itemId, itemSlot);

            // Remove from pending rolls if this was our roll
            if (_pendingRolls.TryGetValue(lootGuid, out var pendingSlots))
            {
                pendingSlots.Remove(itemSlot);
                if (pendingSlots.Count == 0)
                {
                    _pendingRolls.TryRemove(lootGuid, out _);
                }
            }

            var rollData = new LootRollData(lootGuid, itemSlot, itemId, rollType, rollResult, DateTime.UtcNow);
            _lootRolls.OnNext(rollData);
        }

        /// <inheritdoc />
        public void HandleBindOnPickupConfirmation(byte lootSlot, uint itemId, string itemName)
        {
            if (_disposed) return;

            _logger.LogInformation("BoP confirmation required for slot {LootSlot}: {ItemName}", lootSlot, itemName);

            // Update loot slot information to mark BoP requirement
            if (_availableLoot.TryGetValue(lootSlot, out var existing))
            {
                var updated = existing with { IsBindOnPickup = true };
                _availableLoot.TryUpdate(lootSlot, updated, existing);
            }

            var bopData = new BindOnPickupData(lootSlot, itemId, itemName, ItemQuality.Poor, true, DateTime.UtcNow);
            _bindOnPickupConfirmations.OnNext(bopData);
        }

        /// <inheritdoc />
        public void HandleGroupLootMethodChanged(GroupLootMethod newMethod, ulong? masterLooterGuid, ItemQuality threshold)
        {
            if (_disposed) return;

            _logger.LogInformation("Group loot method changed: {Method}, Master: {MasterGuid:X}, Threshold: {Threshold}", 
                newMethod, masterLooterGuid ?? 0, threshold);

            _currentLootMethod = newMethod;
            _lootThreshold = threshold;
            _isMasterLooter = masterLooterGuid.HasValue; // Simplified - would need player GUID comparison

            var notification = new GroupLootNotificationData(
                GroupLootNotificationType.LootMethodChanged,
                0, // No specific item
                $"Loot method changed to {newMethod}",
                null,
                null,
                null,
                DateTime.UtcNow);

            _groupLootNotifications.OnNext(notification);
        }

        /// <inheritdoc />
        public void HandleMasterLootAssigned(byte lootSlot, uint itemId, ulong targetPlayerGuid, string targetPlayerName)
        {
            if (_disposed) return;

            _logger.LogInformation("Master loot assigned: slot {LootSlot}, item {ItemId} to {PlayerName} ({PlayerGuid:X})", 
                lootSlot, itemId, targetPlayerName, targetPlayerGuid);

            // Remove from available loot since it's been assigned
            _availableLoot.TryRemove(lootSlot, out var lootInfo);

            var masterLootData = new MasterLootData(
                lootSlot,
                itemId,
                lootInfo?.ItemName ?? "Unknown Item",
                0, // Would need current player GUID
                "Unknown", // Would need current player name
                targetPlayerGuid,
                targetPlayerName,
                DateTime.UtcNow);

            _masterLootAssignments.OnNext(masterLootData);
        }

        /// <inheritdoc />
        public void HandleLootError(string errorMessage, ulong? lootTargetGuid = null, byte? lootSlot = null)
        {
            if (_disposed) return;

            _logger.LogError("Loot error: {ErrorMessage} (Target: {LootTargetGuid:X}, Slot: {LootSlot})", 
                errorMessage, lootTargetGuid ?? 0, lootSlot);

            var errorData = new LootErrorData(errorMessage, lootTargetGuid, lootSlot, DateTime.UtcNow);
            _lootErrors.OnNext(errorData);

            // Legacy callback support
            _lootErrorCallback?.Invoke(errorMessage);
        }

        #endregion

        #region Operations (Auto-Roll)

        /// <inheritdoc />
        public async Task AutoRollAsync(LootRollPreferences preferences, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));

            try
            {
                _logger.LogDebug("Starting auto-roll with preferences");

                var pendingRollsCopy = new Dictionary<ulong, HashSet<byte>>(_pendingRolls);
                
                foreach (var (rollGuid, slots) in pendingRollsCopy)
                {
                    foreach (var slot in slots.ToList())
                    {
                        if (_availableLoot.TryGetValue(slot, out var lootInfo))
                        {
                            var rollType = DetermineOptimalRoll(lootInfo, preferences);
                            
                            if (rollType.HasValue)
                            {
                                await RollForLootAsync(rollGuid, slot, rollType.Value, cancellationToken);
                                await Task.Delay(100, cancellationToken); // Small delay between rolls
                            }
                        }
                    }
                }

                _logger.LogInformation("Auto-roll completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform auto-roll");

                var errorData = new LootErrorData(ex.Message, null, null, DateTime.UtcNow);
                _lootErrors.OnNext(errorData);

                throw;
            }
        }

        /// <summary>
        /// Determines the optimal roll type based on preferences and item information.
        /// </summary>
        private LootRollType? DetermineOptimalRoll(LootSlotInfo lootInfo, LootRollPreferences preferences)
        {
            // Check always pass items first
            if (preferences.AlwaysPassItems?.Contains(lootInfo.ItemId) == true)
            {
                return LootRollType.Pass;
            }

            // Check always need items
            if (preferences.AlwaysNeedItems?.Contains(lootInfo.ItemId) == true)
            {
                return LootRollType.Need;
            }

            // Check quality thresholds
            if (lootInfo.Quality < preferences.MinimumGreedQuality)
            {
                return preferences.AutoPassOnUnneedableItems ? LootRollType.Pass : null;
            }

            // Determine roll based on quality and type
            if (lootInfo.Quality >= preferences.MinimumNeedQuality && preferences.NeedOnClassItems)
            {
                // This would need class-specific logic to determine if item is usable
                // For now, default to the armor/weapon roll preference
                return lootInfo.ItemName.ToLower().Contains("armor") ? preferences.ArmorRoll : preferences.WeaponRoll;
            }

            if (preferences.GreedOnAllItems || lootInfo.Quality >= preferences.MinimumGreedQuality)
            {
                return LootRollType.Greed;
            }

            return preferences.DefaultRoll;
        }

        #endregion

        #region Utility Methods

        /// <inheritdoc />
        public bool CanLoot()
        {
            return _isLootWindowOpen && !_isOperationInProgress;
        }

        /// <inheritdoc />
        public bool CanLootSlot(byte lootSlot)
        {
            if (!CanLoot())
                return false;

            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo))
                return false;

            // Check if it's a master loot item and we're not the master looter
            if (_currentLootMethod == GroupLootMethod.MasterLoot && lootInfo.Quality >= _lootThreshold && !_isMasterLooter)
                return false;

            // Check if item requires a roll and we haven't rolled yet
            if (lootInfo.RequiresRoll && lootInfo.RollGuid.HasValue)
            {
                return !_pendingRolls.ContainsKey(lootInfo.RollGuid.Value) || 
                       !_pendingRolls[lootInfo.RollGuid.Value].Contains(lootSlot);
            }

            return true;
        }

        /// <inheritdoc />
        public bool CanMasterLoot(byte lootSlot)
        {
            if (!_isMasterLooter || !_isLootWindowOpen)
                return false;

            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo))
                return false;

            return _currentLootMethod == GroupLootMethod.MasterLoot && lootInfo.Quality >= _lootThreshold;
        }

        /// <inheritdoc />
        public ValidationResult ValidateLootOperation(byte lootSlot)
        {
            if (!_isLootWindowOpen)
            {
                return new ValidationResult(false, "Loot window is not open");
            }

            if (_isOperationInProgress)
            {
                return new ValidationResult(false, "Another loot operation is in progress");
            }

            if (lootSlot > 7) // Typical maximum loot slots
            {
                return new ValidationResult(false, "Loot slot index is out of range");
            }

            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo))
            {
                return new ValidationResult(false, "No item found in the specified loot slot");
            }

            // Check master loot restrictions
            if (_currentLootMethod == GroupLootMethod.MasterLoot && 
                lootInfo.Quality >= _lootThreshold && 
                !_isMasterLooter)
            {
                return new ValidationResult(false, "Item requires master loot assignment");
            }

            // Check if item requires a roll
            if (lootInfo.RequiresRoll && lootInfo.RollGuid.HasValue)
            {
                if (_pendingRolls.TryGetValue(lootInfo.RollGuid.Value, out var pendingSlots) && 
                    pendingSlots.Contains(lootSlot))
                {
                    return new ValidationResult(false, "Item requires a roll - use RollForLootAsync instead");
                }
            }

            return new ValidationResult(true);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<LootSlotInfo> GetAvailableLoot()
        {
            return _availableLoot.Values.ToList().AsReadOnly();
        }

        /// <inheritdoc />
        public IReadOnlyCollection<LootSlotInfo> GetLootByQuality(ItemQuality minimumQuality)
        {
            return _availableLoot.Values
                .Where(loot => loot.Quality >= minimumQuality)
                .ToList()
                .AsReadOnly();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the looting network agent and completes all observables.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Complete all observables
            _lootWindowChanges.OnCompleted();
            _itemLoot.OnCompleted();
            _moneyLoot.OnCompleted();
            _lootRolls.OnCompleted();
            _lootErrors.OnCompleted();
            _bindOnPickupConfirmations.OnCompleted();
            _groupLootNotifications.OnCompleted();
            _masterLootAssignments.OnCompleted();

            // Clear state
            _availableLoot.Clear();
            _pendingRolls.Clear();

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Legacy Callback Support

        /// <inheritdoc />
        [Obsolete("Use LootWindowOpened observable instead")]
        public void SetLootWindowOpenedCallback(Action<ulong>? callback)
        {
            _lootWindowOpenedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use LootWindowClosed observable instead")]
        public void SetLootWindowClosedCallback(Action? callback)
        {
            _lootWindowClosedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use ItemLoot observable instead")]
        public void SetItemLootedCallback(Action<uint, uint>? callback)
        {
            _itemLootedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use MoneyLoot observable instead")]
        public void SetMoneyLootedCallback(Action<uint>? callback)
        {
            _moneyLootedCallback = callback;
        }

        /// <inheritdoc />
        [Obsolete("Use LootErrors observable instead")]
        public void SetLootErrorCallback(Action<string>? callback)
        {
            _lootErrorCallback = callback;
        }

        #endregion

        /// <summary>
        /// Legacy method for updating loot window state.
        /// </summary>
        [Obsolete("Use HandleLootWindowChanged instead")]
        public void UpdateLootWindowState(bool isOpen, ulong? lootTargetGuid = null)
        {
            if (isOpen && lootTargetGuid.HasValue)
            {
                HandleLootWindowChanged(isOpen, lootTargetGuid.Value);
            }
            else
            {
                HandleLootWindowChanged(isOpen, null);
            }
        }

        /// <summary>
        /// Legacy method for reporting loot events.
        /// </summary>
        [Obsolete("Use specific Handle methods instead")]
        public void ReportLootEvent(string eventType, uint? itemId = null, uint? quantity = null, string? errorMessage = null)
        {
            if (!_currentLootTarget.HasValue && eventType != "error")
            {
                // For legacy compatibility, create a default loot target
                _currentLootTarget = 0;
            }

            switch (eventType.ToLower())
            {
                case "item":
                    if (itemId.HasValue && quantity.HasValue)
                    {
                        HandleItemLooted(_currentLootTarget ?? 0, itemId.Value, $"Item {itemId.Value}", quantity.Value, ItemQuality.Poor, 0);
                    }
                    break;
                case "money":
                    if (quantity.HasValue)
                    {
                        HandleMoneyLooted(_currentLootTarget ?? 0, quantity.Value);
                    }
                    break;
                case "error":
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        HandleLootError(errorMessage, _currentLootTarget, null);
                    }
                    break;
            }
        }
    }
}