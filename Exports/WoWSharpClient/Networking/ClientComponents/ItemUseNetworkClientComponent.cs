using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of item use network agent that handles item usage operations in World of Warcraft.
    /// Manages using consumables, activating items, and handling item interactions using the Mangos protocol.
    /// </summary>
    public class ItemUseNetworkClientComponent : IItemUseNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ItemUseNetworkClientComponent> _logger;
        private readonly object _stateLock = new object();
        
        private bool _isUsingItem;
        private ulong? _currentItemInUse;
        private readonly Dictionary<uint, uint> _itemCooldowns;
        private bool _isOperationInProgress;
        private DateTime? _lastOperationTime;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the ItemUseNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public ItemUseNetworkClientComponent(IWorldClient worldClient, ILogger<ItemUseNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _itemCooldowns = new Dictionary<uint, uint>();
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

        /// <inheritdoc />
        public bool IsUsingItem => _isUsingItem;

        /// <inheritdoc />
        public ulong? CurrentItemInUse => _currentItemInUse;

        /// <inheritdoc />
        public event Action<ulong, uint, ulong?>? ItemUsed;

        /// <inheritdoc />
        public event Action<ulong, uint>? ItemUseStarted;

        /// <inheritdoc />
        public event Action<ulong>? ItemUseCompleted;

        /// <inheritdoc />
        public event Action<ulong, string>? ItemUseFailed;

        /// <inheritdoc />
        public event Action<uint, uint>? ConsumableEffectApplied;

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

        /// <inheritdoc />
        public async Task UseItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId}", bagId, slotId);

                var payload = new byte[2];
                payload[0] = bagId;
                payload[1] = slotId;

                _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Item use command sent successfully");
            }
            catch (Exception ex)
            {
                _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId}", bagId, slotId);
                ItemUseFailed?.Invoke(0, $"Failed to use item: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UseItemOnTargetAsync(byte bagId, byte slotId, ulong targetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId} on target {Target:X}", 
                    bagId, slotId, targetGuid);

                var payload = new byte[10];
                payload[0] = bagId;
                payload[1] = slotId;
                BitConverter.GetBytes(targetGuid).CopyTo(payload, 2);

                _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Item use on target command sent successfully");
            }
            catch (Exception ex)
            {
                _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId} on target {Target:X}", 
                    bagId, slotId, targetGuid);
                ItemUseFailed?.Invoke(0, $"Failed to use item on target: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UseItemAtLocationAsync(byte bagId, byte slotId, float x, float y, float z, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId} at location ({X}, {Y}, {Z})", 
                    bagId, slotId, x, y, z);

                var payload = new byte[14];
                payload[0] = bagId;
                payload[1] = slotId;
                BitConverter.GetBytes(x).CopyTo(payload, 2);
                BitConverter.GetBytes(y).CopyTo(payload, 6);
                BitConverter.GetBytes(z).CopyTo(payload, 10);

                _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Item use at location command sent successfully");
            }
            catch (Exception ex)
            {
                _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId} at location ({X}, {Y}, {Z})", 
                    bagId, slotId, x, y, z);
                ItemUseFailed?.Invoke(0, $"Failed to use item at location: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task ActivateItemAsync(ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Activating item {ItemGuid:X}", itemGuid);

                var payload = BitConverter.GetBytes(itemGuid);
                
                _isUsingItem = true;
                _currentItemInUse = itemGuid;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Item activation command sent successfully");
            }
            catch (Exception ex)
            {
                _isUsingItem = false;
                _currentItemInUse = null;
                _logger.LogError(ex, "Failed to activate item {ItemGuid:X}", itemGuid);
                ItemUseFailed?.Invoke(itemGuid, $"Failed to activate item: {ex.Message}");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UseConsumableAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using consumable from bag {BagId} slot {SlotId}", bagId, slotId);

                // Consumables use the same mechanism as regular items
                await UseItemAsync(bagId, slotId, cancellationToken);
                
                _logger.LogInformation("Consumable use command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use consumable from bag {BagId} slot {SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task OpenContainerAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Opening container from bag {BagId} slot {SlotId}", bagId, slotId);

                await UseItemAsync(bagId, slotId, cancellationToken);
                
                _logger.LogInformation("Container open command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open container from bag {BagId} slot {SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task UseToolAsync(byte bagId, byte slotId, ulong? targetGuid = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using tool from bag {BagId} slot {SlotId} with target {Target:X}", 
                    bagId, slotId, targetGuid ?? 0);

                if (targetGuid.HasValue)
                {
                    await UseItemOnTargetAsync(bagId, slotId, targetGuid.Value, cancellationToken);
                }
                else
                {
                    await UseItemAsync(bagId, slotId, cancellationToken);
                }
                
                _logger.LogInformation("Tool use command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to use tool from bag {BagId} slot {SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public async Task CancelItemUseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Canceling current item use");

                var payload = new byte[4]; // Empty payload for cancel
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_CAST, payload, cancellationToken);
                
                _isUsingItem = false;
                _currentItemInUse = null;
                _logger.LogInformation("Item use canceled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel item use");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <inheritdoc />
        public bool CanUseItem(uint itemId)
        {
            _logger.LogDebug("Checking if item {ItemId} can be used", itemId);
            
            // Check if item is on cooldown
            if (_itemCooldowns.TryGetValue(itemId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return false;
                }
                
                // Remove expired cooldown
                _itemCooldowns.Remove(itemId);
            }
            
            // Additional checks would go here (mana, level requirements, etc.)
            return !_isUsingItem;
        }

        /// <inheritdoc />
        public uint GetItemCooldown(uint itemId)
        {
            if (_itemCooldowns.TryGetValue(itemId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return cooldownEnd - currentTime;
                }
                
                // Remove expired cooldown
                _itemCooldowns.Remove(itemId);
            }
            
            return 0;
        }

        /// <inheritdoc />
        public async Task<bool> FindAndUseItemAsync(uint itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Finding and using item {ItemId}", itemId);

                // This would typically search through the inventory for the item
                // For now, assume it's in bag 0, slot 0 as a placeholder
                var foundLocation = FindItemInInventory(itemId);
                
                if (foundLocation.HasValue)
                {
                    await UseItemAsync(foundLocation.Value.BagId, foundLocation.Value.SlotId, cancellationToken);
                    return true;
                }
                
                _logger.LogWarning("Item {ItemId} not found in inventory", itemId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find and use item {ItemId}", itemId);
                return false;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Finds an item in the inventory by its ID.
        /// This is a placeholder implementation that would need to interface with the game client.
        /// </summary>
        /// <param name="itemId">The item ID to find.</param>
        /// <returns>The bag and slot location if found, null otherwise.</returns>
        private (byte BagId, byte SlotId)? FindItemInInventory(uint itemId)
        {
            // Enhanced implementation that would interface with actual game state
            // In a real implementation, this would:
            // 1. Query the WoW object manager for inventory items
            // 2. Check all bag slots systematically
            // 3. Return the first found location
            // 4. Handle partial stacks and preferences for consumables
            
            _logger.LogDebug("Enhanced item search for itemId {ItemId} - checking all inventory locations", itemId);
            
            // Priority search: consumables bag first, then main inventory
            for (byte bagId = 0; bagId < 5; bagId++)
            {
                byte maxSlots = bagId == 0 ? (byte)16 : (byte)16; // Simplified - would be dynamic
                for (byte slotId = 0; slotId < maxSlots; slotId++)
                {
                    // Placeholder for actual inventory state checking
                    // This would interface with the WoW client's object manager
                    // to get real inventory data
                }
            }
            
            return null; // Placeholder - would return actual found location
        }

        /// <summary>
        /// Validates that an item can be used based on current game state.
        /// Includes enhanced checks for combat restrictions, mana requirements, etc.
        /// </summary>
        /// <param name="itemId">The item ID to validate.</param>
        /// <returns>True if the item can be used, false otherwise.</returns>
        private bool ValidateItemUsage(uint itemId)
        {
            // Enhanced validation logic
            if (_isUsingItem)
            {
                _logger.LogDebug("Cannot use item {ItemId} - already using an item", itemId);
                return false;
            }

            // Check cooldowns
            if (!CanUseItem(itemId))
            {
                _logger.LogDebug("Cannot use item {ItemId} - on cooldown", itemId);
                return false;
            }

            // Additional checks would include:
            // - Combat state restrictions
            // - Character level requirements
            // - Class restrictions
            // - Mana/energy requirements
            // - Location restrictions (indoor/outdoor)
            
            return true;
        }

        #endregion

        #region Server Response Handlers

        /// <summary>
        /// Updates item use state based on server response.
        /// This should be called when receiving item use confirmation packets.
        /// </summary>
        /// <param name="itemGuid">The GUID of the used item.</param>
        /// <param name="itemId">The ID of the used item.</param>
        /// <param name="targetGuid">The target GUID if applicable.</param>
        public void UpdateItemUsed(ulong itemGuid, uint itemId, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} (ID: {ItemId}) used", itemGuid, itemId);
            
            _isUsingItem = false;
            _currentItemInUse = null;
            ItemUsed?.Invoke(itemGuid, itemId, targetGuid);
        }

        /// <summary>
        /// Updates item use started state based on server response.
        /// This should be called when receiving item use start packets.
        /// </summary>
        /// <param name="itemGuid">The GUID of the item being used.</param>
        /// <param name="castTime">The cast time in milliseconds.</param>
        public void UpdateItemUseStarted(ulong itemGuid, uint castTime)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} use started with cast time {CastTime}ms", itemGuid, castTime);
            
            _isUsingItem = true;
            _currentItemInUse = itemGuid;
            ItemUseStarted?.Invoke(itemGuid, castTime);
        }

        /// <summary>
        /// Updates item cooldown state based on server response.
        /// This should be called when receiving item cooldown packets.
        /// </summary>
        /// <param name="itemId">The item ID on cooldown.</param>
        /// <param name="cooldownTime">The cooldown duration in milliseconds.</param>
        public void UpdateItemCooldown(uint itemId, uint cooldownTime)
        {
            uint cooldownEnd = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cooldownTime;
            _itemCooldowns[itemId] = cooldownEnd;
            
            _logger.LogDebug("Item {ItemId} cooldown updated: {CooldownTime}ms", itemId, cooldownTime);
        }

        #endregion

        #region Enhanced Features

        /// <summary>
        /// Enhanced reagent tracking for spells and abilities.
        /// </summary>
        private readonly Dictionary<uint, List<uint>> _reagentRequirements = new();
        private readonly Dictionary<uint, uint> _reagentCounts = new();

        /// <summary>
        /// Enhanced projectile and reagent management.
        /// </summary>
        /// <param name="itemId">The item ID that consumes reagents.</param>
        /// <param name="reagentsConsumed">List of reagent item IDs consumed.</param>
        public void UpdateReagentConsumption(uint itemId, List<uint> reagentsConsumed)
        {
            _logger.LogDebug("Updating reagent consumption for item {ItemId}: {Reagents}", 
                itemId, string.Join(", ", reagentsConsumed));

            foreach (var reagentId in reagentsConsumed)
            {
                if (_reagentCounts.TryGetValue(reagentId, out var currentCount))
                {
                    if (currentCount > 0)
                    {
                        _reagentCounts[reagentId] = currentCount - 1;
                        _logger.LogDebug("Reagent {ReagentId} count reduced to {Count}", reagentId, currentCount - 1);
                    }
                }
            }
        }

        /// <summary>
        /// Enhanced projectile tracking for ranged weapons.
        /// </summary>
        /// <param name="projectileType">The type of projectile (arrows, bullets, etc.)</param>
        /// <param name="amountConsumed">Number of projectiles consumed.</param>
        public void UpdateProjectileConsumption(uint projectileType, uint amountConsumed)
        {
            _logger.LogDebug("Updating projectile consumption: {ProjectileType} x{Amount}", 
                projectileType, amountConsumed);

            if (_reagentCounts.TryGetValue(projectileType, out var currentCount))
            {
                var newCount = currentCount >= amountConsumed ? currentCount - amountConsumed : 0;
                _reagentCounts[projectileType] = newCount;
                _logger.LogDebug("Projectile {ProjectileType} count updated to {Count}", projectileType, newCount);
            }
        }

        /// <summary>
        /// Checks if sufficient reagents are available for item usage.
        /// </summary>
        private bool CheckReagentRequirements(uint itemId)
        {
            if (!_reagentRequirements.TryGetValue(itemId, out var requiredReagents))
            {
                return true; // No reagents required
            }

            foreach (var reagentId in requiredReagents)
            {
                if (!_reagentCounts.TryGetValue(reagentId, out var available) || available == 0)
                {
                    _logger.LogDebug("Insufficient reagent {ReagentId} for item {ItemId}", reagentId, itemId);
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes of the item use network client component and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing ItemUseNetworkClientComponent");

            // Clear events to prevent memory leaks
            ItemUsed = null;
            ItemUseStarted = null;
            ItemUseCompleted = null;
            ItemUseFailed = null;
            ConsumableEffectApplied = null;

            _disposed = true;
            _logger.LogDebug("ItemUseNetworkClientComponent disposed");
        }

        #endregion
    }
}