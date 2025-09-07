using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of equipment network agent that handles equipment operations in World of Warcraft.
    /// Manages equipping, unequipping, and equipment state tracking using the Mangos protocol.
    /// </summary>
    public class EquipmentNetworkAgent : IEquipmentNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<EquipmentNetworkAgent> _logger;
        private readonly Dictionary<EquipmentSlot, ulong?> _equippedItems;
        private readonly Dictionary<EquipmentSlot, (uint Current, uint Maximum)> _itemDurability;

        /// <summary>
        /// Initializes a new instance of the EquipmentNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public EquipmentNetworkAgent(IWorldClient worldClient, ILogger<EquipmentNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _equippedItems = new Dictionary<EquipmentSlot, ulong?>();
            _itemDurability = new Dictionary<EquipmentSlot, (uint Current, uint Maximum)>();
        }

        /// <inheritdoc />
        public event Action<ulong, EquipmentSlot>? ItemEquipped;

        /// <inheritdoc />
        public event Action<ulong, EquipmentSlot>? ItemUnequipped;

        /// <inheritdoc />
        public event Action<ulong, EquipmentSlot, ulong, EquipmentSlot>? EquipmentSwapped;

        /// <inheritdoc />
        public event Action<string>? EquipmentError;

        /// <inheritdoc />
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOEQUIP_ITEM, payload, cancellationToken);
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOEQUIP_ITEM, payload, cancellationToken);
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOSTORE_BAG_ITEM, payload, cancellationToken);
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
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

                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
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
                _logger.LogDebug("Auto-equipping all available items");

                // This would typically scan the inventory for equippable items
                // and send multiple CMSG_AUTOEQUIP_ITEM commands
                // For now, this is a placeholder implementation
                
                // In a real implementation, this would:
                // 1. Scan all inventory slots
                // 2. Identify equippable items
                // 3. Check if they're better than current equipment
                // 4. Auto-equip the best items

                _logger.LogInformation("Auto-equip all command completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-equip all items");
                EquipmentError?.Invoke($"Failed to auto-equip all: {ex.Message}");
                throw;
            }
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
            return _equippedItems.TryGetValue(slot, out var itemGuid) && itemGuid.HasValue;
        }

        /// <inheritdoc />
        public ulong? GetEquippedItem(EquipmentSlot slot)
        {
            _equippedItems.TryGetValue(slot, out var itemGuid);
            return itemGuid;
        }

        /// <inheritdoc />
        public uint? GetEquippedItemId(EquipmentSlot slot)
        {
            // This would typically query the item database based on the item GUID
            // For now, return null as placeholder
            return null;
        }

        /// <inheritdoc />
        public (uint Current, uint Maximum)? GetItemDurability(EquipmentSlot slot)
        {
            if (_itemDurability.TryGetValue(slot, out var durability))
            {
                return durability;
            }
            return null;
        }

        /// <inheritdoc />
        public bool HasDamagedEquipment()
        {
            return _itemDurability.Values.Any(d => d.Current < d.Maximum);
        }

        /// <inheritdoc />
        public IEnumerable<EquipmentSlot> GetDamagedEquipmentSlots()
        {
            return _itemDurability
                .Where(kvp => kvp.Value.Current < kvp.Value.Maximum)
                .Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Updates equipped item state based on server response.
        /// This should be called when receiving equipment update packets.
        /// </summary>
        /// <param name="slot">The equipment slot.</param>
        /// <param name="itemGuid">The GUID of the equipped item, or null if unequipped.</param>
        public void UpdateEquippedItem(EquipmentSlot slot, ulong? itemGuid)
        {
            var previousItem = _equippedItems.TryGetValue(slot, out var prev) ? prev : null;
            _equippedItems[slot] = itemGuid;

            _logger.LogDebug("Equipment slot {Slot} updated: {PreviousItem:X} -> {NewItem:X}",
                slot, previousItem ?? 0, itemGuid ?? 0);

            if (itemGuid.HasValue && itemGuid != previousItem)
            {
                ItemEquipped?.Invoke(itemGuid.Value, slot);
            }
            else if (!itemGuid.HasValue && previousItem.HasValue)
            {
                ItemUnequipped?.Invoke(previousItem.Value, slot);
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
            _itemDurability[slot] = (currentDurability, maxDurability);
            
            _logger.LogDebug("Durability updated for slot {Slot}: {Current}/{Max}",
                slot, currentDurability, maxDurability);
            
            DurabilityChanged?.Invoke(slot, currentDurability, maxDurability);
        }
    }
}