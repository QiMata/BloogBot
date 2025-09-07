using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Agent.I;

namespace WoWSharpClient.Networking.Agent
{
    /// <summary>
    /// Implementation of vendor network agent that handles vendor operations in World of Warcraft.
    /// Manages buying, selling, and repairing items with NPC vendors using the Mangos protocol.
    /// </summary>
    public class VendorNetworkAgent : IVendorNetworkAgent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<VendorNetworkAgent> _logger;

        private bool _isVendorWindowOpen;
        private ulong? _currentVendorGuid;

        /// <summary>
        /// Initializes a new instance of the VendorNetworkAgent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public VendorNetworkAgent(IWorldClient worldClient, ILogger<VendorNetworkAgent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsVendorWindowOpen => _isVendorWindowOpen;

        /// <inheritdoc />
        public event Action<ulong>? VendorWindowOpened;

        /// <inheritdoc />
        public event Action? VendorWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint, uint>? ItemPurchased;

        /// <inheritdoc />
        public event Action<uint, uint, uint>? ItemSold;

        /// <inheritdoc />
        public event Action<uint>? ItemsRepaired;

        /// <inheritdoc />
        public event Action<string>? VendorError;

        /// <inheritdoc />
        public async Task OpenVendorAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening vendor interaction with: {VendorGuid:X}", vendorGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _logger.LogInformation("Vendor interaction initiated with: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open vendor interaction with: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to open vendor: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestVendorInventoryAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting vendor inventory from: {VendorGuid:X}", vendorGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);

                await _worldClient.SendMovementAsync(Opcode.CMSG_LIST_INVENTORY, payload, cancellationToken);

                _logger.LogInformation("Vendor inventory request sent to: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request vendor inventory from: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to request inventory: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task BuyItemAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Buying item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(itemId).CopyTo(payload, 8);
                BitConverter.GetBytes(quantity).CopyTo(payload, 12);

                await _worldClient.SendMovementAsync(Opcode.CMSG_BUY_ITEM, payload, cancellationToken);

                _logger.LogInformation("Purchase request sent for item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item {ItemId} from vendor: {VendorGuid:X}", itemId, vendorGuid);
                VendorError?.Invoke($"Failed to buy item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task BuyItemInSlotAsync(ulong vendorGuid, uint itemId, uint quantity, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Buying item {ItemId} (quantity: {Quantity}) into bag {BagId} slot {SlotId} from vendor: {VendorGuid:X}", itemId, quantity, bagId, slotId, vendorGuid);

                var payload = new byte[18];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(itemId).CopyTo(payload, 8);
                BitConverter.GetBytes(quantity).CopyTo(payload, 12);
                payload[16] = bagId;
                payload[17] = slotId;

                await _worldClient.SendMovementAsync(Opcode.CMSG_BUY_ITEM_IN_SLOT, payload, cancellationToken);

                _logger.LogInformation("Purchase request sent for item {ItemId} (quantity: {Quantity}) into bag {BagId} slot {SlotId} from vendor: {VendorGuid:X}", itemId, quantity, bagId, slotId, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item {ItemId} into slot from vendor: {VendorGuid:X}", itemId, vendorGuid);
                VendorError?.Invoke($"Failed to buy item into slot: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SellItemAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Selling item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);

                var payload = new byte[14];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = bagId;
                payload[9] = slotId;
                BitConverter.GetBytes(quantity).CopyTo(payload, 10);

                await _worldClient.SendMovementAsync(Opcode.CMSG_SELL_ITEM, payload, cancellationToken);

                _logger.LogInformation("Sell request sent for item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sell item from bag {BagId} slot {SlotId} to vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                VendorError?.Invoke($"Failed to sell item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RepairItemAsync(ulong vendorGuid, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Repairing item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = bagId;
                payload[9] = slotId;

                await _worldClient.SendMovementAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);

                _logger.LogInformation("Repair request sent for item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repair item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                VendorError?.Invoke($"Failed to repair item: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Repairing all items with vendor: {VendorGuid:X}", vendorGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                // Special values to indicate "repair all"
                payload[8] = 0xFF;
                payload[9] = 0xFF;

                await _worldClient.SendMovementAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);

                _logger.LogInformation("Repair all request sent with vendor: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repair all items with vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to repair all items: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseVendorAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing vendor window");

                // Vendor windows typically close automatically when moving away
                // But we can update our internal state
                _isVendorWindowOpen = false;
                _currentVendorGuid = null;
                VendorWindowClosed?.Invoke();

                _logger.LogInformation("Vendor window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close vendor window");
                VendorError?.Invoke($"Failed to close vendor: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsVendorOpen(ulong vendorGuid)
        {
            return _isVendorWindowOpen && _currentVendorGuid == vendorGuid;
        }

        /// <inheritdoc />
        public async Task QuickBuyAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick buy of item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                await RequestVendorInventoryAsync(vendorGuid, cancellationToken);
                
                // Small delay to allow vendor window to open
                await Task.Delay(100, cancellationToken);
                
                await BuyItemAsync(vendorGuid, itemId, quantity, cancellationToken);
                
                // Small delay to allow purchase to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick buy completed for item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick buy failed for item {ItemId} from vendor: {VendorGuid:X}", itemId, vendorGuid);
                VendorError?.Invoke($"Quick buy failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickSellAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick sell of item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                
                // Small delay to allow vendor window to open
                await Task.Delay(100, cancellationToken);
                
                await SellItemAsync(vendorGuid, bagId, slotId, quantity, cancellationToken);
                
                // Small delay to allow sale to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick sell completed for item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick sell failed for item from bag {BagId} slot {SlotId} to vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                VendorError?.Invoke($"Quick sell failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickRepairAllAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick repair all with vendor: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                
                // Small delay to allow vendor window to open
                await Task.Delay(100, cancellationToken);
                
                await RepairAllItemsAsync(vendorGuid, cancellationToken);
                
                // Small delay to allow repair to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick repair all completed with vendor: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick repair all failed with vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Quick repair all failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles server responses for vendor window opening.
        /// This method should be called when SMSG_LIST_INVENTORY is received.
        /// </summary>
        /// <param name="vendorGuid">The GUID of the vendor.</param>
        public void HandleVendorWindowOpened(ulong vendorGuid)
        {
            _isVendorWindowOpen = true;
            _currentVendorGuid = vendorGuid;
            VendorWindowOpened?.Invoke(vendorGuid);
            _logger.LogDebug("Vendor window opened for: {VendorGuid:X}", vendorGuid);
        }

        /// <summary>
        /// Handles server responses for successful item purchases.
        /// This method should be called when SMSG_BUY_ITEM is received.
        /// </summary>
        /// <param name="itemId">The ID of the purchased item.</param>
        /// <param name="quantity">The quantity purchased.</param>
        /// <param name="cost">The cost in copper.</param>
        public void HandleItemPurchased(uint itemId, uint quantity, uint cost)
        {
            ItemPurchased?.Invoke(itemId, quantity, cost);
            _logger.LogDebug("Item purchased: {ItemId} (quantity: {Quantity}, cost: {Cost})", itemId, quantity, cost);
        }

        /// <summary>
        /// Handles server responses for successful item sales.
        /// This method should be called when SMSG_SELL_ITEM is received.
        /// </summary>
        /// <param name="itemId">The ID of the sold item.</param>
        /// <param name="quantity">The quantity sold.</param>
        /// <param name="value">The value received in copper.</param>
        public void HandleItemSold(uint itemId, uint quantity, uint value)
        {
            ItemSold?.Invoke(itemId, quantity, value);
            _logger.LogDebug("Item sold: {ItemId} (quantity: {Quantity}, value: {Value})", itemId, quantity, value);
        }

        /// <summary>
        /// Handles server responses for successful repairs.
        /// This method should be called when a repair operation succeeds.
        /// </summary>
        /// <param name="cost">The total repair cost in copper.</param>
        public void HandleItemsRepaired(uint cost)
        {
            ItemsRepaired?.Invoke(cost);
            _logger.LogDebug("Items repaired (cost: {Cost})", cost);
        }

        /// <summary>
        /// Handles server responses for vendor operation failures.
        /// This method should be called when SMSG_BUY_FAILED or similar error responses are received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleVendorError(string errorMessage)
        {
            VendorError?.Invoke(errorMessage);
            _logger.LogWarning("Vendor operation failed: {Error}", errorMessage);
        }
    }
}