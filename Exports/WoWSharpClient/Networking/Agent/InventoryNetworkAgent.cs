using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of inventory network agent that handles inventory operations in World of Warcraft.
    /// Manages bag operations, item movement, and inventory state tracking using the Mangos protocol.
    /// </summary>
    public class InventoryNetworkAgent : IInventoryNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<InventoryNetworkAgent> _logger;
        private bool _isInventoryOpen;

        /// <summary>
        /// Initializes a new instance of the InventoryNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public InventoryNetworkAgent(IWorldClient worldClient, ILogger<InventoryNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsInventoryOpen => _isInventoryOpen;

        /// <inheritdoc />
        public event Action<ulong, byte, byte, byte, byte>? ItemMoved;

        /// <inheritdoc />
        public event Action<ulong, uint>? ItemSplit;

        /// <inheritdoc />
        public event Action<ulong, ulong>? ItemsSwapped;

        /// <inheritdoc />
        public event Action<ulong, uint>? ItemDestroyed;

        /// <inheritdoc />
        public event Action<string>? InventoryError;

        /// <inheritdoc />
        public async Task MoveItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Moving item from bag {SourceBag} slot {SourceSlot} to bag {DestBag} slot {DestSlot}",
                    sourceBag, sourceSlot, destinationBag, destinationSlot);

                var payload = new byte[4];
                payload[0] = sourceBag;
                payload[1] = sourceSlot;
                payload[2] = destinationBag;
                payload[3] = destinationSlot;

                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item move command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move item from {SourceBag}:{SourceSlot} to {DestBag}:{DestSlot}",
                    sourceBag, sourceSlot, destinationBag, destinationSlot);
                InventoryError?.Invoke($"Failed to move item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SwapItemsAsync(byte firstBag, byte firstSlot, byte secondBag, byte secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Swapping items between {FirstBag}:{FirstSlot} and {SecondBag}:{SecondSlot}",
                    firstBag, firstSlot, secondBag, secondSlot);

                var payload = new byte[4];
                payload[0] = firstBag;
                payload[1] = firstSlot;
                payload[2] = secondBag;
                payload[3] = secondSlot;

                await _worldClient.SendMovementAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item swap command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap items between {FirstBag}:{FirstSlot} and {SecondBag}:{SecondSlot}",
                    firstBag, firstSlot, secondBag, secondSlot);
                InventoryError?.Invoke($"Failed to swap items: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SplitItemStackAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Splitting {Quantity} items from {SourceBag}:{SourceSlot} to {DestBag}:{DestSlot}",
                    quantity, sourceBag, sourceSlot, destinationBag, destinationSlot);

                var payload = new byte[8];
                payload[0] = sourceBag;
                payload[1] = sourceSlot;
                payload[2] = destinationBag;
                payload[3] = destinationSlot;
                BitConverter.GetBytes(quantity).CopyTo(payload, 4);

                await _worldClient.SendMovementAsync(Opcode.CMSG_SPLIT_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item split command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to split {Quantity} items from {SourceBag}:{SourceSlot} to {DestBag}:{DestSlot}",
                    quantity, sourceBag, sourceSlot, destinationBag, destinationSlot);
                InventoryError?.Invoke($"Failed to split item stack: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DestroyItemAsync(byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Destroying {Quantity} items from bag {BagId} slot {SlotId}", quantity, bagId, slotId);

                var payload = new byte[6];
                payload[0] = bagId;
                payload[1] = slotId;
                BitConverter.GetBytes(quantity).CopyTo(payload, 2);

                await _worldClient.SendMovementAsync(Opcode.CMSG_DESTROYITEM, payload, cancellationToken);
                _logger.LogInformation("Item destroy command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to destroy {Quantity} items from bag {BagId} slot {SlotId}",
                    quantity, bagId, slotId);
                InventoryError?.Invoke($"Failed to destroy item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SortBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Sorting bag {BagId}", bagId);

                var payload = new byte[1] { bagId };
                await _worldClient.SendMovementAsync(Opcode.CMSG_AUTOSTORE_BAG_ITEM, payload, cancellationToken);
                
                _logger.LogInformation("Bag sort command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sort bag {BagId}", bagId);
                InventoryError?.Invoke($"Failed to sort bag: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task OpenBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening bag {BagId}", bagId);
                
                _isInventoryOpen = true;
                _logger.LogInformation("Bag {BagId} opened", bagId);
                
                // For WoW, opening a bag is typically a client-side operation
                // The server is notified when items are accessed, not when bags are opened
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open bag {BagId}", bagId);
                InventoryError?.Invoke($"Failed to open bag: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing bag {BagId}", bagId);
                
                _isInventoryOpen = false;
                _logger.LogInformation("Bag {BagId} closed", bagId);
                
                // For WoW, closing a bag is typically a client-side operation
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close bag {BagId}", bagId);
                throw;
            }
        }

        /// <inheritdoc />
        public (byte BagId, byte SlotId)? FindEmptySlot(uint itemId = 0)
        {
            // This would typically query the client's inventory state
            // For now, return a placeholder implementation
            _logger.LogDebug("Finding empty slot for item {ItemId}", itemId);
            
            // Start with main backpack (bag 0), then check additional bags
            for (byte bagId = 0; bagId < 5; bagId++)
            {
                for (byte slotId = 0; slotId < 36; slotId++)
                {
                    // This would check if the slot is empty in the actual implementation
                    // Return first available slot as placeholder
                    if (bagId == 0 && slotId == 0)
                        return (bagId, slotId);
                }
            }
            
            return null;
        }

        /// <inheritdoc />
        public uint CountItem(uint itemId)
        {
            _logger.LogDebug("Counting items with ID {ItemId}", itemId);
            
            // This would typically query the client's inventory state
            // Return 0 as placeholder
            return 0;
        }

        /// <inheritdoc />
        public bool HasEnoughSpace(int requiredSlots)
        {
            _logger.LogDebug("Checking if inventory has {RequiredSlots} free slots", requiredSlots);
            
            // This would typically check the client's inventory state
            return GetFreeSlotCount() >= requiredSlots;
        }

        /// <inheritdoc />
        public int GetFreeSlotCount()
        {
            _logger.LogDebug("Getting free slot count");
            
            // This would typically query the client's inventory state
            // Return placeholder value
            return 10;
        }

        /// <summary>
        /// Updates item moved state based on server response.
        /// This should be called when receiving item movement packets.
        /// </summary>
        /// <param name="itemGuid">The GUID of the moved item.</param>
        /// <param name="sourceBag">The source bag ID.</param>
        /// <param name="sourceSlot">The source slot ID.</param>
        /// <param name="destinationBag">The destination bag ID.</param>
        /// <param name="destinationSlot">The destination slot ID.</param>
        public void UpdateItemMoved(ulong itemGuid, byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} moved from {SourceBag}:{SourceSlot} to {DestBag}:{DestSlot}",
                itemGuid, sourceBag, sourceSlot, destinationBag, destinationSlot);
            
            ItemMoved?.Invoke(itemGuid, sourceBag, sourceSlot, destinationBag, destinationSlot);
        }

        /// <summary>
        /// Updates item split state based on server response.
        /// This should be called when receiving item split packets.
        /// </summary>
        /// <param name="itemGuid">The GUID of the split item.</param>
        /// <param name="splitQuantity">The quantity that was split.</param>
        public void UpdateItemSplit(ulong itemGuid, uint splitQuantity)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} split with quantity {Quantity}",
                itemGuid, splitQuantity);
            
            ItemSplit?.Invoke(itemGuid, splitQuantity);
        }

        /// <summary>
        /// Updates item destroyed state based on server response.
        /// This should be called when receiving item destruction packets.
        /// </summary>
        /// <param name="itemGuid">The GUID of the destroyed item.</param>
        /// <param name="quantity">The quantity destroyed.</param>
        public void UpdateItemDestroyed(ulong itemGuid, uint quantity)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} destroyed with quantity {Quantity}",
                itemGuid, quantity);
            
            ItemDestroyed?.Invoke(itemGuid, quantity);
        }
    }
}