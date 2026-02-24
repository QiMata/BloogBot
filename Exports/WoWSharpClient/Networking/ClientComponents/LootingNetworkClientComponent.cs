using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of looting network agent that handles loot operations in World of Warcraft.
    /// Uses opcode-backed observables (no Subjects/events). Follows the same pattern as other
    /// NetworkClientComponent classes by deriving streams from IWorldClient opcode handlers.
    /// </summary>
    public class LootingNetworkClientComponent : NetworkClientComponent, ILootingNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<LootingNetworkClientComponent> _logger;
        private bool _isLootWindowOpen;
        private ulong? _currentLootTarget;

        // Group loot state
        private GroupLootMethod _currentLootMethod = GroupLootMethod.FreeForAll;
        private bool _isMasterLooter = false;
        private ItemQuality _lootThreshold = ItemQuality.Uncommon;

        // Loot slot tracking
        private readonly ConcurrentDictionary<byte, LootSlotInfo> _availableLoot = new();
        private readonly ConcurrentDictionary<ulong, HashSet<byte>> _pendingRolls = new();

        // Manual observer lists (no Subjects)
        private readonly List<IObserver<LootWindowData>> _lootWindowObservers = new();
        private readonly List<IObserver<LootData>> _itemLootObservers = new();
        private readonly List<IObserver<MoneyLootData>> _moneyLootObservers = new();
        private readonly List<IObserver<LootRollData>> _lootRollObservers = new();
        private readonly List<IObserver<LootErrorData>> _lootErrorObservers = new();
        private readonly List<IObserver<BindOnPickupData>> _bopObservers = new();
        private readonly List<IObserver<MasterLootData>> _masterLootObservers = new();

        // Legacy callbacks
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

            // Observable streams fed by Handle* methods via manual observer lists.
            // Real SMSG opcodes are subscribed below and call Handle* methods which publish to these streams.
            _lootWindowChanges = CreateManualStream(_lootWindowObservers)
                .Do(data =>
                {
                    _isLootWindowOpen = data.IsOpen;
                    _currentLootTarget = data.LootTargetGuid;
                    if (!data.IsOpen)
                    {
                        _availableLoot.Clear();
                        _pendingRolls.Clear();
                    }
                })
                .Publish()
                .RefCount();

            _lootWindowOpened = _lootWindowChanges.Where(d => d.IsOpen);
            _lootWindowClosed = _lootWindowChanges.Where(d => !d.IsOpen);

            _itemLoot = CreateManualStream(_itemLootObservers)
                .Do(ld => _availableLoot.TryRemove(ld.LootSlot, out _));

            _moneyLoot = CreateManualStream(_moneyLootObservers);

            _lootRolls = CreateManualStream(_lootRollObservers)
                .Do(roll =>
                {
                    if (_pendingRolls.TryGetValue(roll.LootGuid, out var slots))
                    {
                        slots.Remove(roll.ItemSlot);
                        if (slots.Count == 0) _pendingRolls.TryRemove(roll.LootGuid, out _);
                    }
                });

            _lootErrors = CreateManualStream(_lootErrorObservers);
            _bindOnPickupConfirmations = CreateManualStream(_bopObservers);

            _masterLootAssignments = CreateManualStream(_masterLootObservers)
                .Do(m => _availableLoot.TryRemove(m.LootSlot, out _));

            _groupLootNotifications = Observable.Never<GroupLootNotificationData>();

            // Subscribe to real SMSG loot opcodes and route to Handle* methods
            SafeOpcodeStream(Opcode.SMSG_LOOT_RESPONSE).Subscribe(OnLootResponseReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_RELEASE_RESPONSE).Subscribe(OnLootReleaseReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_REMOVED).Subscribe(OnLootRemovedReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_MONEY_NOTIFY).Subscribe(OnMoneyNotifyReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_CLEAR_MONEY).Subscribe(OnClearMoneyReceived);
            SafeOpcodeStream(Opcode.SMSG_ITEM_PUSH_RESULT).Subscribe(OnItemPushResultReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_START_ROLL).Subscribe(OnLootStartRollReceived);
            SafeOpcodeStream(Opcode.SMSG_LOOT_ROLL).Subscribe(OnLootRollReceived);
        }

        #region Properties

        public bool IsLootWindowOpen => _isLootWindowOpen;
        public ulong? CurrentLootTarget => _currentLootTarget;
        public GroupLootMethod CurrentLootMethod => _currentLootMethod;
        public bool IsMasterLooter => _isMasterLooter;
        public ItemQuality LootThreshold => _lootThreshold;

        #endregion

        #region Reactive Observables
        private readonly IObservable<LootWindowData> _lootWindowChanges;
        private IObservable<LootWindowData> _lootWindowOpened;
        private IObservable<LootWindowData> _lootWindowClosed;
         private readonly IObservable<LootData> _itemLoot;
         private readonly IObservable<MoneyLootData> _moneyLoot;
         private readonly IObservable<LootRollData> _lootRolls;
         private readonly IObservable<LootErrorData> _lootErrors;
         private readonly IObservable<BindOnPickupData> _bindOnPickupConfirmations;
         private readonly IObservable<GroupLootNotificationData> _groupLootNotifications;
         private readonly IObservable<MasterLootData> _masterLootAssignments;

         public IObservable<LootWindowData> LootWindowChanges => _lootWindowChanges;
         public IObservable<LootData> ItemLoot => _itemLoot;
         public IObservable<MoneyLootData> MoneyLoot => _moneyLoot;
         public IObservable<LootRollData> LootRolls => _lootRolls;
         public IObservable<LootErrorData> LootErrors => _lootErrors;
         public IObservable<LootWindowData> LootWindowOpened => _lootWindowOpened;
         public IObservable<LootWindowData> LootWindowClosed => _lootWindowClosed;
         public IObservable<BindOnPickupData> BindOnPickupConfirmations => _bindOnPickupConfirmations;
         public IObservable<GroupLootNotificationData> GroupLootNotifications => _groupLootNotifications;
         public IObservable<MasterLootData> MasterLootAssignments => _masterLootAssignments;
        #endregion

        #region Operations

        public async Task OpenLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Opening loot for target: {LootTargetGuid:X}", lootTargetGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(lootTargetGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT, payload, cancellationToken);
                _currentLootTarget = lootTargetGuid;
                _logger.LogInformation("Loot command sent for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open loot for target: {LootTargetGuid:X}", lootTargetGuid);
                // Surface via errors stream (parsed from server) � nothing to push directly.
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task LootMoneyAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Looting money from current loot window");
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_MONEY, [], cancellationToken);
                _logger.LogInformation("Money loot command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot money");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task LootItemAsync(byte lootSlot, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Looting item from slot: {LootSlot}", lootSlot);

                var payload = new byte[1];
                payload[0] = lootSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item loot command sent for slot: {LootSlot}", lootSlot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot item from slot: {LootSlot}", lootSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task StoreLootInSlotAsync(byte lootSlot, byte bag, byte slot, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Storing loot from slot {LootSlot} to bag {Bag}, slot {Slot}", lootSlot, bag, slot);

                var payload = new byte[3] { lootSlot, bag, slot };

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_STORE_LOOT_IN_SLOT, payload, cancellationToken);
                _logger.LogInformation("Store loot command sent for slot {LootSlot} to bag {Bag}, slot {Slot}",
                    lootSlot, bag, slot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store loot from slot {LootSlot} to bag {Bag}, slot {Slot}",
                    lootSlot, bag, slot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task CloseLootAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Closing loot window");

                // CMSG_LOOT_RELEASE: ObjectGuid lootGuid (8)
                var payload = new byte[8];
                BitConverter.GetBytes(_currentLootTarget ?? 0UL).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);
                _logger.LogInformation("Loot release command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close loot window");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task RollForLootAsync(ulong lootGuid, byte itemSlot, LootRollType rollType, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Rolling {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}",
                    rollType, itemSlot, lootGuid);

                // CMSG_LOOT_ROLL: ObjectGuid lootGuid (8) + uint32 itemSlot (4) + uint8 rollType (1) = 13 bytes
                var payload = new byte[13];
                BitConverter.GetBytes(lootGuid).CopyTo(payload, 0);
                BitConverter.GetBytes((uint)itemSlot).CopyTo(payload, 8);
                payload[12] = (byte)rollType;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_ROLL, payload, cancellationToken);
                _logger.LogInformation("Loot roll {RollType} sent for slot {ItemSlot} from: {LootGuid:X}",
                    rollType, itemSlot, lootGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll {RollType} for loot in slot {ItemSlot} from: {LootGuid:X}",
                    rollType, itemSlot, lootGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task LootAllAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                if (!_isLootWindowOpen)
                {
                    _logger.LogWarning("Cannot loot all - no loot window is open");
                    return;
                }

                _logger.LogDebug("Looting all items and money");

                await LootMoneyAsync(cancellationToken);
                await Task.Delay(50, cancellationToken);

                for (byte slot = 0; slot < 8; slot++)
                {
                    try
                    {
                        await LootItemAsync(slot, cancellationToken);
                        await Task.Delay(25, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Failed to loot slot {Slot}: {Error}", slot, ex.Message);
                    }
                }

                await Task.Delay(100, cancellationToken);
                await CloseLootAsync(cancellationToken);

                _logger.LogInformation("Completed looting all items and money");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot all items");
                throw;
            }
        }

        public async Task QuickLootAsync(ulong lootTargetGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                _logger.LogDebug("Quick looting target: {LootTargetGuid:X}", lootTargetGuid);

                await OpenLootAsync(lootTargetGuid, cancellationToken);
                await Task.Delay(200, cancellationToken);
                await LootAllAsync(cancellationToken);

                _logger.LogInformation("Quick loot completed for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to quick loot target: {LootTargetGuid:X}", lootTargetGuid);
                throw;
            }
        }

        public async Task QuickLootWithFilterAsync(ulong lootTargetGuid, ItemQuality minimumQuality, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                _logger.LogDebug("Quick looting target: {LootTargetGuid:X} with quality filter: {MinQuality}", lootTargetGuid, minimumQuality);

                await OpenLootAsync(lootTargetGuid, cancellationToken);
                await Task.Delay(200, cancellationToken);
                await LootAllWithFilterAsync(minimumQuality, cancellationToken);

                _logger.LogInformation("Filtered quick loot completed for target: {LootTargetGuid:X}", lootTargetGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to filtered quick loot target: {LootTargetGuid:X}", lootTargetGuid);
                throw;
            }
        }

        public async Task LootAllWithFilterAsync(ItemQuality minimumQuality, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                if (!_isLootWindowOpen)
                {
                    _logger.LogWarning("Cannot loot with filter - no loot window is open");
                    return;
                }

                _logger.LogDebug("Looting all items with minimum quality: {MinQuality}", minimumQuality);

                await LootMoneyAsync(cancellationToken);
                await Task.Delay(50, cancellationToken);

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

                await Task.Delay(100, cancellationToken);
                await CloseLootAsync(cancellationToken);

                _logger.LogInformation("Completed filtered looting with minimum quality: {MinQuality}", minimumQuality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to loot with filter: {MinQuality}", minimumQuality);
                throw;
            }
        }

        public async Task ConfirmBindOnPickupAsync(byte lootSlot, bool confirm = true, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            try
            {
                _logger.LogDebug("Confirming BoP for slot {LootSlot}: {Confirm}", lootSlot, confirm);

                if (confirm)
                {
                    // In vanilla 1.12.1, BoP is handled server-side when you loot the item.
                    // The client just sends CMSG_AUTOSTORE_LOOT_ITEM as normal.
                    await LootItemAsync(lootSlot, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("BoP declined for slot {LootSlot}, not looting", lootSlot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm BoP for slot {LootSlot}", lootSlot);
                throw;
            }
        }

        public async Task AssignMasterLootAsync(byte lootSlot, ulong targetPlayerGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!_isMasterLooter) throw new InvalidOperationException("Player is not the master looter");

            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Assigning master loot slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);

                // CMSG_LOOT_MASTER_GIVE: ObjectGuid lootGuid (8) + uint8 slotId (1) + ObjectGuid targetGuid (8) = 17 bytes
                var payload = new byte[17];
                BitConverter.GetBytes(_currentLootTarget ?? 0UL).CopyTo(payload, 0);
                payload[8] = lootSlot;
                BitConverter.GetBytes(targetPlayerGuid).CopyTo(payload, 9);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_MASTER_GIVE, payload, cancellationToken);

                _logger.LogInformation("Master loot assignment sent for slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign master loot for slot {LootSlot} to player: {PlayerGuid:X}", lootSlot, targetPlayerGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SetGroupLootMethodAsync(GroupLootMethod method, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Setting group loot method to: {LootMethod}", method);

                // CMSG_LOOT_METHOD: uint32 lootMethod (4) + ObjectGuid masterLooterGuid (8) + uint32 lootThreshold (4) = 16 bytes
                var payload = new byte[16];
                BitConverter.GetBytes((uint)method).CopyTo(payload, 0);
                // masterLooterGuid = 0 (no master looter change)
                BitConverter.GetBytes((uint)_lootThreshold).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, payload, cancellationToken);

                _logger.LogInformation("Group loot method change sent: {LootMethod}", method);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set group loot method: {LootMethod}", method);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SetLootThresholdAsync(ItemQuality threshold, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Setting loot threshold to: {Threshold}", threshold);

                // CMSG_LOOT_METHOD: uint32 lootMethod (4) + ObjectGuid masterLooterGuid (8) + uint32 lootThreshold (4) = 16 bytes
                var payload = new byte[16];
                BitConverter.GetBytes((uint)_currentLootMethod).CopyTo(payload, 0);
                // masterLooterGuid = 0 (no change)
                BitConverter.GetBytes((uint)threshold).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, payload, cancellationToken);

                _logger.LogInformation("Loot threshold change sent: {Threshold}", threshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set loot threshold: {Threshold}", threshold);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SetMasterLooterAsync(ulong masterLooterGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Setting master looter to: {MasterLooterGuid:X}", masterLooterGuid);

                // CMSG_LOOT_METHOD: uint32 lootMethod (4) + ObjectGuid masterLooterGuid (8) + uint32 lootThreshold (4) = 16 bytes
                // Setting master looter implies Master Loot method (2)
                var payload = new byte[16];
                BitConverter.GetBytes((uint)GroupLootMethod.MasterLoot).CopyTo(payload, 0);
                BitConverter.GetBytes(masterLooterGuid).CopyTo(payload, 4);
                BitConverter.GetBytes((uint)_lootThreshold).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_METHOD, payload, cancellationToken);

                _logger.LogInformation("Master looter change sent: {MasterLooterGuid:X}", masterLooterGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set master looter: {MasterLooterGuid:X}", masterLooterGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }
        #endregion

        #region Server Response Handling
        public void HandleLootWindowChanged(bool isOpen, ulong? lootTargetGuid, uint availableItems = 0, uint availableMoney = 0)
        {
            if (_disposed) return;

            _isLootWindowOpen = isOpen;
            if (isOpen && lootTargetGuid.HasValue)
            {
                _currentLootTarget = lootTargetGuid.Value;
                _logger.LogDebug("Loot window opened for target: {LootTargetGuid:X}", lootTargetGuid.Value);
                Publish(_lootWindowObservers, new LootWindowData(true, lootTargetGuid.Value, availableItems, availableMoney, DateTime.UtcNow));
                _lootWindowOpenedCallback?.Invoke(lootTargetGuid.Value);
            }
            else if (!isOpen)
            {
                _logger.LogDebug("Loot window closed");
                _currentLootTarget = null;
                _availableLoot.Clear();
                _pendingRolls.Clear();
                Publish(_lootWindowObservers, new LootWindowData(false, null, availableItems, availableMoney, DateTime.UtcNow));
                _lootWindowClosedCallback?.Invoke();
            }
        }

        public void HandleLootList(ulong lootTargetGuid, IReadOnlyCollection<LootSlotInfo> lootSlots)
        {
            if (_disposed) return;

            _logger.LogDebug("Received loot list for target: {LootTargetGuid:X} with {SlotCount} slots", lootTargetGuid, lootSlots.Count);

            _availableLoot.Clear();
            foreach (var slot in lootSlots)
            {
                _availableLoot.TryAdd(slot.SlotIndex, slot);

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

        public void HandleItemLooted(ulong lootTargetGuid, uint itemId, string itemName, uint quantity, ItemQuality quality, byte lootSlot)
        {
            if (_disposed) return;

            _logger.LogInformation("Item looted: {ItemName} x{Quantity} from slot {LootSlot}", itemName, quantity, lootSlot);
            _availableLoot.TryRemove(lootSlot, out _);
            Publish(_itemLootObservers, new LootData(lootTargetGuid, itemId, itemName, quantity, quality, lootSlot, DateTime.UtcNow));
            _itemLootedCallback?.Invoke(itemId, quantity);
        }

        public void HandleMoneyLooted(ulong lootTargetGuid, uint amount)
        {
            if (_disposed) return;
            _logger.LogInformation("Money looted: {Amount} copper", amount);
            Publish(_moneyLootObservers, new MoneyLootData(lootTargetGuid, amount, DateTime.UtcNow));
            _moneyLootedCallback?.Invoke(amount);
        }

        public void HandleLootRoll(ulong lootGuid, byte itemSlot, uint itemId, LootRollType rollType, uint rollResult)
        {
            if (_disposed) return;

            _logger.LogInformation("Loot roll: {RollType} ({RollResult}) for item {ItemId} in slot {ItemSlot}",
                rollType, rollResult, itemId, itemSlot);

            if (_pendingRolls.TryGetValue(lootGuid, out var pendingSlots))
            {
                pendingSlots.Remove(itemSlot);
                if (pendingSlots.Count == 0) _pendingRolls.TryRemove(lootGuid, out _);
            }
            Publish(_lootRollObservers, new LootRollData(lootGuid, itemSlot, itemId, rollType, rollResult, DateTime.UtcNow));
        }

        public void HandleBindOnPickupConfirmation(byte lootSlot, uint itemId, string itemName)
        {
            if (_disposed) return;

            _logger.LogInformation("BoP confirmation required for slot {LootSlot}: {ItemName}", lootSlot, itemName);

            if (_availableLoot.TryGetValue(lootSlot, out var _)) { }

            Publish(_bopObservers, new BindOnPickupData(lootSlot, itemId, itemName, ItemQuality.Uncommon, true, DateTime.UtcNow));
        }

        public void HandleGroupLootMethodChanged(GroupLootMethod newMethod, ulong? masterLooterGuid, ItemQuality threshold)
        {
            if (_disposed) return;

            _logger.LogInformation("Group loot method changed: {Method}, Master: {MasterGuid:X}, Threshold: {Threshold}",
                newMethod, masterLooterGuid ?? 0, threshold);

            _currentLootMethod = newMethod;
            _lootThreshold = threshold;
            _isMasterLooter = masterLooterGuid.HasValue;
        }

        public void HandleMasterLootAssigned(byte lootSlot, uint itemId, ulong targetPlayerGuid, string targetPlayerName)
        {
            if (_disposed) return;

            _logger.LogInformation("Master loot assigned: slot {LootSlot}, item {ItemId} to {PlayerName} ({PlayerGuid:X})",
                lootSlot, itemId, targetPlayerName, targetPlayerGuid);

            _availableLoot.TryRemove(lootSlot, out _);
            Publish(_masterLootObservers, new MasterLootData(lootSlot, itemId, "Unknown Item", 0, "Unknown", targetPlayerGuid, targetPlayerName, DateTime.UtcNow));
        }

        public void HandleLootError(string errorMessage, ulong? lootTargetGuid = null, byte? lootSlot = null)
        {
            if (_disposed) return;
            _logger.LogError("Loot error: {ErrorMessage} (Target: {LootTargetGuid:X}, Slot: {LootSlot})",
                errorMessage, lootTargetGuid ?? 0, lootSlot);
            Publish(_lootErrorObservers, new LootErrorData(errorMessage, lootTargetGuid, lootSlot, DateTime.UtcNow));
            _lootErrorCallback?.Invoke(errorMessage);
        }

        #endregion

        #region Operations (Auto-Roll)
        public async Task AutoRollAsync(LootRollPreferences preferences, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
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
                                await Task.Delay(100, cancellationToken);
                            }
                        }
                    }
                }

                _logger.LogInformation("Auto-roll completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform auto-roll");
                throw;
            }
        }

        private LootRollType? DetermineOptimalRoll(LootSlotInfo lootInfo, LootRollPreferences preferences)
        {
            if (preferences.AlwaysPassItems?.Contains(lootInfo.ItemId) == true)
            {
                return LootRollType.Pass;
            }

            if (preferences.AlwaysNeedItems?.Contains(lootInfo.ItemId) == true)
            {
                return LootRollType.Need;
            }

            if (lootInfo.Quality < preferences.MinimumGreedQuality)
            {
                return preferences.AutoPassOnUnneedableItems ? LootRollType.Pass : null;
            }

            if (lootInfo.Quality >= preferences.MinimumNeedQuality && preferences.NeedOnClassItems)
            {
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
        public bool CanLoot()
        {
            return _isLootWindowOpen && !IsOperationInProgress;
        }

        public bool CanLootSlot(byte lootSlot)
        {
            if (!CanLoot()) return false;
            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo)) return false;
            if (_currentLootMethod == GroupLootMethod.MasterLoot && lootInfo.Quality >= _lootThreshold && !_isMasterLooter) return false;
            if (lootInfo.RequiresRoll && lootInfo.RollGuid.HasValue)
            {
                return !_pendingRolls.ContainsKey(lootInfo.RollGuid.Value) || !_pendingRolls[lootInfo.RollGuid.Value].Contains(lootSlot);
            }
            return true;
        }

        public bool CanMasterLoot(byte lootSlot)
        {
            if (!_isMasterLooter || !_isLootWindowOpen) return false;
            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo)) return false;
            return _currentLootMethod == GroupLootMethod.MasterLoot && lootInfo.Quality >= _lootThreshold;
        }

        public ValidationResult ValidateLootOperation(byte lootSlot)
        {
            if (!_isLootWindowOpen) return new ValidationResult(false, "Loot window is not open");
            if (IsOperationInProgress) return new ValidationResult(false, "Another loot operation is in progress");
            if (lootSlot > 7) return new ValidationResult(false, "Loot slot index is out of range");
            if (!_availableLoot.TryGetValue(lootSlot, out var lootInfo)) return new ValidationResult(false, "No item found in the specified loot slot");
            if (_currentLootMethod == GroupLootMethod.MasterLoot && lootInfo.Quality >= _lootThreshold && !_isMasterLooter) return new ValidationResult(false, "Item requires master loot assignment");
            if (lootInfo.RequiresRoll && lootInfo.RollGuid.HasValue)
            {
                if (_pendingRolls.TryGetValue(lootInfo.RollGuid.Value, out var pendingSlots) && pendingSlots.Contains(lootSlot))
                {
                    return new ValidationResult(false, "Item requires a roll - use RollForLootAsync instead");
                }
            }
            return new ValidationResult(true);
        }

        public IReadOnlyCollection<LootSlotInfo> GetAvailableLoot() => _availableLoot.Values.ToList().AsReadOnly();
        public IReadOnlyCollection<LootSlotInfo> GetLootByQuality(ItemQuality minimumQuality) => _availableLoot.Values.Where(loot => loot.Quality >= minimumQuality).ToList().AsReadOnly();

        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _availableLoot.Clear();
            _pendingRolls.Clear();
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Legacy Callback Support
        [Obsolete("Use LootWindowOpened observable instead")]
        public void SetLootWindowOpenedCallback(Action<ulong>? callback) { _lootWindowOpenedCallback = callback; }

        [Obsolete("Use LootWindowClosed observable instead")]
        public void SetLootWindowClosedCallback(Action? callback) { _lootWindowClosedCallback = callback; }

        [Obsolete("Use ItemLoot observable instead")]
        public void SetItemLootedCallback(Action<uint, uint>? callback) { _itemLootedCallback = callback; }

        [Obsolete("Use MoneyLoot observable instead")]
        public void SetMoneyLootedCallback(Action<uint>? callback) { _moneyLootedCallback = callback; }

        [Obsolete("Use LootErrors observable instead")]
        public void SetLootErrorCallback(Action<string>? callback) { _lootErrorCallback = callback; }
        #endregion

        [Obsolete("Use HandleLootWindowChanged instead")]
        public void UpdateLootWindowState(bool isOpen, ulong? lootTargetGuid = null)
        {
            if (isOpen && lootTargetGuid.HasValue) HandleLootWindowChanged(true, lootTargetGuid.Value);
            else HandleLootWindowChanged(false, null);
        }

        [Obsolete("Use specific Handle methods instead")]
        public void ReportLootEvent(string eventType, uint? itemId = null, uint? quantity = null, string? errorMessage = null)
        {
            switch (eventType.ToLowerInvariant())
            {
                case "item":
                    if (itemId.HasValue && quantity.HasValue)
                    {
                        _itemLootedCallback?.Invoke(itemId.Value, quantity.Value);
                    }
                    break;
                case "money":
                    if (quantity.HasValue)
                    {
                        _moneyLootedCallback?.Invoke(quantity.Value);
                    }
                    break;
                case "error":
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        _lootErrorCallback?.Invoke(errorMessage);
                    }
                    break;
            }
        }

        private IObservable<T> CreateManualStream<T>(List<IObserver<T>> observers)
        {
            return Observable.Create<T>(observer =>
            {
                lock (observers) observers.Add(observer);
                return new ManualUnsubscriber<T>(observers, observer);
            });
        }

        private sealed class ManualUnsubscriber<T>(List<IObserver<T>> list, IObserver<T> observer) : IDisposable
        {
            private readonly List<IObserver<T>> _list = list;
            private readonly IObserver<T> _observer = observer;

            public void Dispose()
            {
                lock (_list)
                {
                    _list.Remove(_observer);
                }
            }
        }

        private void Publish<T>(List<IObserver<T>> observers, T value)
        {
            IObserver<T>[] snapshot;
            lock (observers) snapshot = observers.ToArray();
            foreach (var o in snapshot) o.OnNext(value);
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #region SMSG Packet Handlers

        /// <summary>
        /// Handles SMSG_LOOT_RESPONSE (0x160).
        /// Format: guid(8) + lootType(1) + gold(4) + itemCount(1) + [per item: lootIndex(1) + itemId(4) + count(4) + displayInfoId(4) + reserved(4) + randomPropertyId(4) + slotType(1)]
        /// </summary>
        private void OnLootResponseReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 14) return; // minimum: guid(8) + lootType(1) + gold(4) + itemCount(1)

                ulong guid = BitConverter.ToUInt64(span[..8]);
                byte lootType = span[8];
                uint gold = BitConverter.ToUInt32(span.Slice(9, 4));
                byte itemCount = span[13];

                _logger.LogDebug("SMSG_LOOT_RESPONSE: guid={Guid:X}, type={Type}, gold={Gold}, items={Count}",
                    guid, lootType, gold, itemCount);

                // Parse loot items (each item = 22 bytes: index(1) + itemId(4) + count(4) + displayId(4) + reserved(4) + randomPropId(4) + slotType(1))
                var lootSlots = new List<LootSlotInfo>();
                int offset = 14;
                for (int i = 0; i < itemCount && offset + 22 <= span.Length; i++)
                {
                    byte lootIndex = span[offset];
                    uint itemId = BitConverter.ToUInt32(span.Slice(offset + 1, 4));
                    uint count = BitConverter.ToUInt32(span.Slice(offset + 5, 4));
                    // displayInfoId at offset + 9 (4 bytes) - not needed
                    // reserved at offset + 13 (4 bytes) - always 0
                    // randomPropertyId at offset + 17 (4 bytes) - not needed for tracking
                    byte serverSlotType = span[offset + 21];
                    offset += 22;

                    // serverSlotType: 0=AllowLoot, 1=RollOngoing, 2=Master, 3=Locked, 4=Owner
                    bool requiresRoll = serverSlotType == 1;
                    var slotInfo = new LootSlotInfo(
                        lootIndex, itemId, $"Item#{itemId}", count,
                        ItemQuality.Common, false, requiresRoll,
                        LootSlotType.Item, requiresRoll ? guid : null);

                    lootSlots.Add(slotInfo);
                }

                HandleLootList(guid, lootSlots.AsReadOnly());
                HandleLootWindowChanged(true, guid, (uint)lootSlots.Count, gold);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_RESPONSE");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_RELEASE_RESPONSE (0x161).
        /// Format: guid(8) + unk(1)
        /// </summary>
        private void OnLootReleaseReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length < 9) return;
                ulong guid = BitConverter.ToUInt64(payload.Span[..8]);
                _logger.LogDebug("SMSG_LOOT_RELEASE_RESPONSE: guid={Guid:X}", guid);
                HandleLootWindowChanged(false, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_RELEASE_RESPONSE");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_REMOVED (0x162).
        /// Format: lootSlot(1)
        /// </summary>
        private void OnLootRemovedReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length < 1) return;
                byte lootSlot = payload.Span[0];
                _logger.LogDebug("SMSG_LOOT_REMOVED: slot={Slot}", lootSlot);
                _availableLoot.TryRemove(lootSlot, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_REMOVED");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_MONEY_NOTIFY (0x163).
        /// Format: amount(4)
        /// </summary>
        private void OnMoneyNotifyReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length < 4) return;
                uint amount = BitConverter.ToUInt32(payload.Span[..4]);
                _logger.LogDebug("SMSG_LOOT_MONEY_NOTIFY: amount={Amount}", amount);
                HandleMoneyLooted(_currentLootTarget ?? 0, amount);
                WoWSharpEventEmitter.Instance.FireOnLootMoney((int)amount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_MONEY_NOTIFY");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_CLEAR_MONEY (0x165).
        /// Format: empty payload
        /// </summary>
        private void OnClearMoneyReceived(ReadOnlyMemory<byte> payload)
        {
            _logger.LogDebug("SMSG_LOOT_CLEAR_MONEY received");
        }

        /// <summary>
        /// Handles SMSG_ITEM_PUSH_RESULT (0x166).
        /// Format: playerGuid(8) + received(4) + created(4) + showInChat(4) + bagSlot(1) + itemSlot(4) + itemEntry(4) + suffixFactor(4) + randomPropertyId(4) + count(4)
        /// </summary>
        private void OnItemPushResultReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 41) return;

                ulong playerGuid = BitConverter.ToUInt64(span[..8]);
                uint received = BitConverter.ToUInt32(span.Slice(8, 4));   // 0=looted, 1=from NPC
                // created at offset 12 (4 bytes)
                // showInChat at offset 16 (4 bytes)
                byte bagSlot = span[20];
                uint itemSlot = BitConverter.ToUInt32(span.Slice(21, 4));
                uint itemEntry = BitConverter.ToUInt32(span.Slice(25, 4));
                // suffixFactor at offset 29 (4 bytes)
                // randomPropertyId at offset 33 (4 bytes)
                uint count = BitConverter.ToUInt32(span.Slice(37, 4));

                _logger.LogDebug("SMSG_ITEM_PUSH_RESULT: player={Guid:X}, item={ItemId}, count={Count}, bag={Bag}, slot={Slot}",
                    playerGuid, itemEntry, count, bagSlot, itemSlot);

                // Only report as loot if received == 0 (looted, not from NPC)
                if (received == 0)
                {
                    HandleItemLooted(_currentLootTarget ?? 0, itemEntry, $"Item#{itemEntry}", count, ItemQuality.Common, bagSlot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_ITEM_PUSH_RESULT");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_START_ROLL (0x2A1).
        /// Format: lootSourceGuid(8) + mapId(4) + lootSlot(4) + itemEntry(4) + itemRandomSuffix(4) + itemRandomPropertyId(4) + countdown(4)
        /// </summary>
        private void OnLootStartRollReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 32) return;

                ulong lootGuid = BitConverter.ToUInt64(span[..8]);
                // mapId at offset 8 (4 bytes) - not needed
                uint lootSlot = BitConverter.ToUInt32(span.Slice(12, 4));
                uint itemEntry = BitConverter.ToUInt32(span.Slice(16, 4));
                // randomSuffix at offset 20, randomProperty at offset 24
                uint countdown = BitConverter.ToUInt32(span.Slice(28, 4));

                _logger.LogInformation("SMSG_LOOT_START_ROLL: guid={Guid:X}, slot={Slot}, item={ItemId}, countdown={Countdown}ms",
                    lootGuid, lootSlot, itemEntry, countdown);

                byte slotByte = (byte)lootSlot;

                // Mark as pending roll
                _pendingRolls.AddOrUpdate(
                    lootGuid,
                    [slotByte],
                    (key, existing) => { existing.Add(slotByte); return existing; });

                // Update slot info to require roll
                if (_availableLoot.TryGetValue(slotByte, out var existingSlot))
                {
                    _availableLoot[slotByte] = existingSlot with { RequiresRoll = true, RollGuid = lootGuid };
                }

                HandleLootRoll(lootGuid, slotByte, itemEntry, LootRollType.Need, 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_START_ROLL");
            }
        }

        /// <summary>
        /// Handles SMSG_LOOT_ROLL (0x2A2).
        /// Format: lootSourceGuid(8) + lootSlot(4) + playerGuid(8) + itemEntry(4) + itemRandomSuffix(4) + itemRandomPropertyId(4) + rollNumber(4) + rollType(1)
        /// </summary>
        private void OnLootRollReceived(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 37) return;

                ulong lootGuid = BitConverter.ToUInt64(span[..8]);
                uint lootSlot = BitConverter.ToUInt32(span.Slice(8, 4));
                ulong playerGuid = BitConverter.ToUInt64(span.Slice(12, 8));
                uint itemEntry = BitConverter.ToUInt32(span.Slice(20, 4));
                // randomSuffix at offset 24, randomProperty at offset 28
                uint rollNumber = BitConverter.ToUInt32(span.Slice(32, 4));
                byte rollType = span[36];

                _logger.LogDebug("SMSG_LOOT_ROLL: guid={Guid:X}, slot={Slot}, player={Player:X}, item={Item}, roll={Roll}, type={Type}",
                    lootGuid, lootSlot, playerGuid, itemEntry, rollNumber, rollType);

                var rollTypeEnum = rollType switch
                {
                    0 => LootRollType.Pass,
                    1 => LootRollType.Need,
                    2 => LootRollType.Greed,
                    _ => LootRollType.Pass
                };

                HandleLootRoll(lootGuid, (byte)lootSlot, itemEntry, rollTypeEnum, rollNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SMSG_LOOT_ROLL");
            }
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));
        }
    }
}