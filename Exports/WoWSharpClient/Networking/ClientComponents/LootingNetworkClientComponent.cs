using System.Collections.Concurrent;
using System.Reactive.Linq;
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

        // Placeholder server opcode used to drive streams until concrete loot opcodes are wired.
        // Replace with real SMSG_* loot opcodes when available.
        private static readonly Opcode PlaceholderLootServerOpcode = Opcode.SMSG_UPDATE_OBJECT;

        /// <summary>
        /// Initializes a new instance of the LootingNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public LootingNetworkClientComponent(IWorldClient worldClient, ILogger<LootingNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Network-driven streams (placeholders) merged with manual streams from Handle* methods
            var lootWindowFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseLootWindowChange)
                .Where(x => x != null)
                .Select(x => x!);

            _lootWindowChanges = lootWindowFromNetwork
                .Merge(CreateManualStream(_lootWindowObservers))
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

            var itemLootFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseItemLoot)
                .Where(x => x != null)
                .Select(x => x!);

            _itemLoot = itemLootFromNetwork
                .Merge(CreateManualStream(_itemLootObservers))
                .Do(ld => _availableLoot.TryRemove(ld.LootSlot, out _));

            var moneyFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseMoneyLoot)
                .Where(x => x != null)
                .Select(x => x!);

            _moneyLoot = moneyFromNetwork
                .Merge(CreateManualStream(_moneyLootObservers));

            var lootRollFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseLootRoll)
                .Where(x => x != null)
                .Select(x => x!);

            _lootRolls = lootRollFromNetwork
                .Merge(CreateManualStream(_lootRollObservers))
                .Do(roll =>
                {
                    if (_pendingRolls.TryGetValue(roll.LootGuid, out var slots))
                    {
                        slots.Remove(roll.ItemSlot);
                        if (slots.Count == 0) _pendingRolls.TryRemove(roll.LootGuid, out _);
                    }
                });

            var lootErrorsFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseLootError)
                .Where(x => x is not null)!;

            _lootErrors = lootErrorsFromNetwork
                .Merge(CreateManualStream(_lootErrorObservers));

            var bopFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseBindOnPickup)
                .Where(x => x != null)
                .Select(x => x!);

            _bindOnPickupConfirmations = bopFromNetwork
                .Merge(CreateManualStream(_bopObservers));

            var masterFromNetwork = SafeOpcodeStream(PlaceholderLootServerOpcode)
                .Select(TryParseMasterLoot)
                .Where(x => x != null)
                .Select(x => x!);

            _masterLootAssignments = masterFromNetwork
                .Merge(CreateManualStream(_masterLootObservers))
                .Do(m => _availableLoot.TryRemove(m.LootSlot, out _));

            _groupLootNotifications = Observable.Never<GroupLootNotificationData>();
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
                // Surface via errors stream (parsed from server) – nothing to push directly.
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
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, [], cancellationToken);
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

                var payload = new byte[10];
                BitConverter.GetBytes(lootGuid).CopyTo(payload, 0);
                payload[8] = itemSlot;
                payload[9] = (byte)rollType;

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
            SetOperationInProgress(true);
            try
            {
                _logger.LogDebug("Confirming BoP for slot {LootSlot}: {Confirm}", lootSlot, confirm);

                var payload = new byte[2] { lootSlot, (byte)(confirm ? 1 : 0) };

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);
                _logger.LogInformation("BoP confirmation sent for slot {LootSlot}: {Confirm}", lootSlot, confirm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm BoP for slot {LootSlot}", lootSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
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

                var payload = new byte[9];
                payload[0] = lootSlot;
                BitConverter.GetBytes(targetPlayerGuid).CopyTo(payload, 1);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

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

                var payload = BitConverter.GetBytes((uint)method);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

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

                var payload = BitConverter.GetBytes((uint)threshold);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

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

                var payload = BitConverter.GetBytes(masterLooterGuid);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LOOT_RELEASE, payload, cancellationToken);

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

        private sealed class ManualUnsubscriber<T> : IDisposable
        {
            private readonly List<IObserver<T>> _list;
            private readonly IObserver<T> _observer;
            public ManualUnsubscriber(List<IObserver<T>> list, IObserver<T> observer) { _list = list; _observer = observer; }
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

        private LootWindowData? TryParseLootWindowChange(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                // Heuristic: if >= 9 bytes, [0..7]=guid, [8]=isOpen (1/0), [9..12] items, [13..16] money (optional)
                if (span.Length >= 9)
                {
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    bool isOpen = span[8] != 0;
                    uint items = span.Length >= 13 ? BitConverter.ToUInt32(span.Slice(9, 4)) : 0u;
                    uint money = span.Length >= 17 ? BitConverter.ToUInt32(span.Slice(13, 4)) : 0u;
                    return new LootWindowData(isOpen, isOpen ? guid : null, items, money, DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private LootData? TryParseItemLoot(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length >= 17)
                {
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    uint itemId = BitConverter.ToUInt32(span.Slice(8, 4));
                    byte slot = span[12];
                    uint qty = BitConverter.ToUInt32(span.Slice(13, 4));
                    return new LootData(guid, itemId, $"Item {itemId}", qty, ItemQuality.Common, slot, DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private MoneyLootData? TryParseMoneyLoot(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length >= 12)
                {
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    uint amount = BitConverter.ToUInt32(span.Slice(8, 4));
                    return new MoneyLootData(guid, amount, DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private LootRollData? TryParseLootRoll(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length >= 17)
                {
                    ulong lootGuid = BitConverter.ToUInt64(span[..8]);
                    byte itemSlot = span[8];
                    uint itemId = BitConverter.ToUInt32(span.Slice(9, 4));
                    var rollType = (LootRollType)(span[13] % 3);
                    uint rollResult = BitConverter.ToUInt32(span.Slice(13, 4));
                    return new LootRollData(lootGuid, itemSlot, itemId, rollType, rollResult, DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private LootErrorData? TryParseLootError(ReadOnlyMemory<byte> payload) => null;

        private BindOnPickupData? TryParseBindOnPickup(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length >= 9)
                {
                    byte slot = span[0];
                    uint itemId = BitConverter.ToUInt32(span.Slice(1, 4));
                    return new BindOnPickupData(slot, itemId, $"Item {itemId}", ItemQuality.Uncommon, true, DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private MasterLootData? TryParseMasterLoot(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length >= 25)
                {
                    byte slot = span[0];
                    uint itemId = BitConverter.ToUInt32(span.Slice(1, 4));
                    ulong fromGuid = BitConverter.ToUInt64(span.Slice(5, 8));
                    ulong toGuid = BitConverter.ToUInt64(span.Slice(13, 8));
                    return new MasterLootData(slot, itemId, $"Item {itemId}", fromGuid, "Unknown", toGuid, "Unknown", DateTime.UtcNow);
                }
            }
            catch { }
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LootingNetworkClientComponent));
        }
    }
}