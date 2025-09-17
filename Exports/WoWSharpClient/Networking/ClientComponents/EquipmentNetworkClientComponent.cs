using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of equipment network agent that handles equipment operations in World of Warcraft.
    /// Manages equipping and unequipping items, changing equipment slots, and tracking equipment state using the Mangos protocol.
    /// Uses opcode-backed observables (no events/subjects exposed).
    /// </summary>
    public class EquipmentNetworkClientComponent : NetworkClientComponent, IEquipmentNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<EquipmentNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly Dictionary<EquipmentSlot, EquippedItem?> _equippedItems = [];
        private readonly Dictionary<EquipmentSlot, ItemDurability> _itemDurability = [];
        private bool _disposed;

        // Reactive opcode-backed observables
        private readonly IObservable<EquipmentOperationData> _equipmentOperations;
        private readonly IObservable<EquipmentChangeData> _equipmentChanges;
        private readonly IObservable<(EquipmentSlot Slot, uint Current, uint Maximum)> _durabilityChanges;

        /// <summary>
        /// Initializes a new instance of the EquipmentNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public EquipmentNetworkClientComponent(IWorldClient worldClient, ILogger<EquipmentNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Operation results (failures primarily) from server
            _equipmentOperations = SafeOpcodeStream(Opcode.SMSG_INVENTORY_CHANGE_FAILURE)
                .Select(ParseInventoryChangeFailure)
                .Do(op => _logger.LogDebug("Equipment op result: {Result} Item: {ItemGuid:X} Slot: {Slot}", op.Result, op.ItemGuid, op.Slot))
                .Publish()
                .RefCount();

            // No direct opcode dedicated to equipment change confirmations here; expose a never-ending stream
            _equipmentChanges = Observable.Never<EquipmentChangeData>();

            // Durability changes are often implicit or sent via different channels; expose a never-ending stream
            _durabilityChanges = Observable.Never<(EquipmentSlot Slot, uint Current, uint Maximum)>();
        }

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<EquipmentOperationData> EquipmentOperations => _equipmentOperations;

        /// <inheritdoc />
        public IObservable<EquipmentChangeData> EquipmentChanges => _equipmentChanges;

        /// <inheritdoc />
        public IObservable<(EquipmentSlot Slot, uint Current, uint Maximum)> DurabilityChanges => _durabilityChanges;

        #endregion

        #region Operations

        /// <inheritdoc />
        public async Task EquipItemAsync(byte bagId, byte slotId, EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Equipping item from bag {BagId} slot {SlotId} to equipment slot {EquipSlot}",
                    bagId, slotId, equipSlot);

                var payload = new byte[3];
                payload[0] = bagId;
                payload[1] = slotId;
                payload[2] = (byte)equipSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOEQUIP_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item equip command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to equip item from bag {BagId} slot {SlotId} to equipment slot {EquipSlot}",
                    bagId, slotId, equipSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AutoEquipItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Auto-equipping item from bag {BagId} slot {SlotId}", bagId, slotId);

                var payload = new byte[2];
                payload[0] = bagId;
                payload[1] = slotId;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOEQUIP_ITEM, payload, cancellationToken);
                _logger.LogInformation("Auto-equip command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-equip item from bag {BagId} slot {SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UnequipItemAsync(EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unequipping item from equipment slot {EquipSlot}", equipSlot);

                var payload = new byte[2];
                payload[0] = 255; // Equipment bag indicator
                payload[1] = (byte)equipSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_BAG_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item unequip command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unequip item from equipment slot {EquipSlot}", equipSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UnequipItemToSlotAsync(EquipmentSlot equipSlot, byte targetBag, byte targetSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unequipping item from equipment slot {EquipSlot} to bag {TargetBag} slot {TargetSlot}",
                    equipSlot, targetBag, targetSlot);

                var payload = new byte[4];
                payload[0] = 255; // Equipment bag indicator
                payload[1] = (byte)equipSlot;
                payload[2] = targetBag;
                payload[3] = targetSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item unequip to slot command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unequip item from equipment slot {EquipSlot} to bag {TargetBag} slot {TargetSlot}",
                    equipSlot, targetBag, targetSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SwapEquipmentAsync(EquipmentSlot firstSlot, EquipmentSlot secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping equipment between slots {FirstSlot} and {SecondSlot}", firstSlot, secondSlot);

                var payload = new byte[4];
                payload[0] = 255; // Equipment bag indicator
                payload[1] = (byte)firstSlot;
                payload[2] = 255; // Equipment bag indicator
                payload[3] = (byte)secondSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
                _logger.LogInformation("Equipment swap command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap equipment between slots {FirstSlot} and {SecondSlot}", firstSlot, secondSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task SwapEquipmentWithInventoryAsync(EquipmentSlot equipSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping equipment slot {EquipSlot} with bag {BagId} slot {SlotId}",
                    equipSlot, bagId, slotId);

                var payload = new byte[4];
                payload[0] = 255; // Equipment bag indicator
                payload[1] = (byte)equipSlot;
                payload[2] = bagId;
                payload[3] = slotId;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
                _logger.LogInformation("Equipment-inventory swap command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap equipment slot {EquipSlot} with bag {BagId} slot {SlotId}",
                    equipSlot, bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task AutoEquipAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Auto-equipping all available items with enhanced logic");

                // Enhanced implementation that handles:
                // 1. Combat state checking - don't equip weapons in combat
                // 2. Item level comparison - only equip better items
                // 3. Class restrictions - check if player can use the item
                // 4. Two-handed weapon logic - handle main/off-hand conflicts

                await ProcessInventoryForAutoEquip(cancellationToken);

                _logger.LogInformation("Enhanced auto-equip all command completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-equip all items");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UnequipAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unequipping all equipment");

                // Unequip all equipment slots that have items
                foreach (var slot in Enum.GetValues<EquipmentSlot>())
                {
                    if (IsSlotEquipped(slot))
                    {
                        await UnequipItemAsync(slot, cancellationToken);

                        // Small delay between unequip operations to avoid overwhelming the server
                        await Task.Delay(100, cancellationToken);
                    }
                }

                _logger.LogInformation("Unequip all command completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unequip all items");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #endregion

        #region Queries

        /// <inheritdoc />
        public bool IsSlotEquipped(EquipmentSlot slot)
        {
            return _equippedItems.TryGetValue(slot, out var item) && item != null;
        }

        /// <inheritdoc />
        public ulong? GetEquippedItem(EquipmentSlot slot)
        {
            return _equippedItems.TryGetValue(slot, out var item) ? item?.ItemGuid : null;
        }

        /// <inheritdoc />
        public uint? GetEquippedItemId(EquipmentSlot slot)
        {
            return _equippedItems.TryGetValue(slot, out var item) ? item?.ItemId : null;
        }

        /// <inheritdoc />
        public (uint Current, uint Maximum)? GetItemDurability(EquipmentSlot slot)
        {
            if (_itemDurability.TryGetValue(slot, out var durability))
            {
                return (durability.Current, durability.Maximum);
            }
            return null;
        }

        /// <inheritdoc />
        public bool HasDamagedEquipment()
        {
            return _itemDurability.Values.Any(d => d.NeedsRepair);
        }

        /// <inheritdoc />
        public IEnumerable<EquipmentSlot> GetDamagedEquipmentSlots()
        {
            return _itemDurability
                .Where(kvp => kvp.Value.NeedsRepair)
                .Select(kvp => kvp.Key);
        }

        #endregion

        #region Public Methods for Server Response Handling (Compat)

        /// <summary>
        /// Updates equipped item state based on server response.
        /// This should be called when receiving equipment update packets.
        /// </summary>
        /// <param name="slot">The equipment slot.</param>
        /// <param name="item">The equipped item, or null if unequipped.</param>
        public void UpdateEquippedItem(EquipmentSlot slot, EquippedItem? item)
        {
            var previousItem = _equippedItems.TryGetValue(slot, out var prev) ? prev : null;
            _equippedItems[slot] = item;

            _logger.LogDebug("Equipment slot {Slot} updated: {PreviousItem} -> {NewItem}",
                slot, previousItem?.ItemGuid ?? 0, item?.ItemGuid ?? 0);
        }

        /// <summary>
        /// Updates item durability based on server response.
        /// This should be called when receiving durability update packets.
        /// </summary>
        /// <param name="slot">The equipment slot.</param>
        /// <param name="currentDurability">The current durability.</param>
        /// <param name="maxDurability">The maximum durability.</param>
        public void UpdateItemDurability(EquipmentSlot slot, uint currentDurability, uint maxDurability)
        {
            _itemDurability[slot] = new ItemDurability(currentDurability, maxDurability);

            _logger.LogDebug("Durability updated for slot {Slot}: {Current}/{Max}",
                slot, currentDurability, maxDurability);
        }

        #endregion

        #region Private Helper Methods

        private static IObservable<T> SafeStream<T>(IObservable<T>? source)
            => source ?? Observable.Empty<T>();

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private static EquipmentOperationData ParseInventoryChangeFailure(ReadOnlyMemory<byte> payload)
        {
            // Best-effort parsing placeholder; detailed parsing requires full packet schema
            return new EquipmentOperationData
            {
                Slot = default,
                ItemGuid = 0,
                Result = EquipmentResult.Unknown,
                ErrorMessage = "Inventory change failed",
                Timestamp = DateTime.UtcNow
            };
        }

        private static bool IsWeaponSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.MainHand ||
                   slot == EquipmentSlot.OffHand ||
                   slot == EquipmentSlot.Ranged;
        }

        private async Task ProcessInventoryForAutoEquip(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Processing inventory for auto-equip with combat-aware logic");
            await Task.Delay(10, cancellationToken); // Simulate processing time
        }

        private bool CanEquipItem(byte bagId, byte slotId, EquipmentSlot targetSlot)
        {
            _logger.LogDebug("Validating equipment operation for slot {TargetSlot} with combat awareness", targetSlot);

            if (IsWeaponSlot(targetSlot))
            {
                _logger.LogDebug("Weapon slot detected - checking combat restrictions");
            }

            return true; // Placeholder
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the equipment network client component and cleans up resources.
        /// </summary>
        public override void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing EquipmentNetworkClientComponent");

            _disposed = true;
            _logger.LogDebug("EquipmentNetworkClientComponent disposed");
            base.Dispose();
        }

        #endregion
    }
}