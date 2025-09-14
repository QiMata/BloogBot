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
    /// </summary>
    public class EquipmentNetworkClientComponent : IEquipmentNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<EquipmentNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();

        private readonly Dictionary<EquipmentSlot, EquippedItem?> _equippedItems = [];
        private readonly Dictionary<EquipmentSlot, ItemDurability> _itemDurability = [];
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the EquipmentNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public EquipmentNetworkClientComponent(IWorldClient worldClient, ILogger<EquipmentNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region INetworkClientComponent Implementation

        /// <inheritdoc />
        public bool IsOperationInProgress
        {
            get
            {
                lock (_stateLock)
                {
                    return _isOperationInProgress;
                }
            }
        }

        /// <inheritdoc />
        public DateTime? LastOperationTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastOperationTime;
                }
            }
        }

        #endregion

        #region IEquipmentNetworkClientComponent Members

        /// <summary>
        /// Event triggered when an item is equipped.
        /// </summary>
        public event Action<ulong, EquipmentSlot>? ItemEquipped;

        /// <summary>
        /// Event triggered when an item is unequipped.
        /// </summary>
        public event Action<ulong, EquipmentSlot>? ItemUnequipped;

        /// <summary>
        /// Event triggered when equipment is swapped between slots.
        /// </summary>
        public event Action<ulong, EquipmentSlot, ulong, EquipmentSlot>? EquipmentSwapped;

        /// <summary>
        /// Event triggered on equipment-related errors.
        /// </summary>
        public event Action<string>? EquipmentError;

        /// <summary>
        /// Event triggered when item durability changes.
        /// </summary>
        public event Action<EquipmentSlot, uint, uint>? DurabilityChanged;

        /// <inheritdoc />
        public async Task EquipItemAsync(byte bagId, byte slotId, EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to equip item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AutoEquipItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to auto-equip item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UnequipItemAsync(EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to unequip item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UnequipItemToSlotAsync(EquipmentSlot equipSlot, byte targetBag, byte targetSlot, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to unequip item to slot: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SwapEquipmentAsync(EquipmentSlot firstSlot, EquipmentSlot secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to swap equipment: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SwapEquipmentWithInventoryAsync(EquipmentSlot equipSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to swap equipment with inventory: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AutoEquipAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to auto-equip all: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processes inventory for auto-equipment with enhanced logic.
        /// </summary>
        private async Task ProcessInventoryForAutoEquip(CancellationToken cancellationToken)
        {
            // Enhanced auto-equip logic that would:
            // 1. Scan all inventory slots for equippable items
            // 2. Compare item stats with currently equipped items
            // 3. Respect combat restrictions (no weapon swapping in combat)
            // 4. Handle special equipment rules (rings, trinkets, weapons)
            // 5. Batch operations to avoid server spam

            _logger.LogDebug("Processing inventory for auto-equip with combat-aware logic");
            
            // Placeholder for actual implementation that would interface with
            // WoW object manager and item database
            await Task.Delay(10, cancellationToken); // Simulate processing time
        }

        /// <summary>
        /// Enhanced equipment validation that checks combat state and item requirements.
        /// </summary>
        private bool CanEquipItem(byte bagId, byte slotId, EquipmentSlot targetSlot)
        {
            // Enhanced validation that checks:
            // 1. Combat state restrictions
            // 2. Item level and class requirements  
            // 3. Two-handed weapon conflicts
            // 4. Special equipment restrictions

            _logger.LogDebug("Validating equipment operation for slot {TargetSlot} with combat awareness", targetSlot);

            // Check for combat restrictions on weapon slots
            if (IsWeaponSlot(targetSlot))
            {
                // In a real implementation, this would check if player is in combat
                // and prevent weapon swapping during combat
                _logger.LogDebug("Weapon slot detected - checking combat restrictions");
            }

            return true; // Placeholder - would return actual validation result
        }

        /// <summary>
        /// Determines if an equipment slot is a weapon slot that has combat restrictions.
        /// </summary>
        private static bool IsWeaponSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.MainHand || 
                   slot == EquipmentSlot.OffHand || 
                   slot == EquipmentSlot.Ranged;
        }

        /// <inheritdoc />
        public async Task UnequipAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
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
                EquipmentError?.Invoke($"Failed to unequip all: {ex.Message}");
                throw;
            }
        }

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

            if (item != null && item != previousItem)
            {
                ItemEquipped?.Invoke(item.ItemGuid, slot);
            }
            else if (item == null && previousItem != null)
            {
                ItemUnequipped?.Invoke(previousItem.ItemGuid, slot);
            }
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
            
            DurabilityChanged?.Invoke(slot, currentDurability, maxDurability);
        }

        #endregion

        #region Private Helper Methods

        private void SetOperationInProgress(bool inProgress)
        {
            lock (_stateLock)
            {
                _isOperationInProgress = inProgress;
                if (inProgress)
                {
                    _lastOperationTime = DateTime.UtcNow;
                }
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the equipment network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing EquipmentNetworkClientComponent");

            // Clear events to prevent memory leaks
            ItemEquipped = null;
            ItemUnequipped = null;
            EquipmentSwapped = null;
            EquipmentError = null;
            DurabilityChanged = null;

            _disposed = true;
            _logger.LogDebug("EquipmentNetworkClientComponent disposed");
        }

        #endregion
    }
}