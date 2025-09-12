using System.Collections.Concurrent;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Enhanced implementation of vendor network agent that handles vendor operations in World of Warcraft.
    /// Manages buying, selling, and repairing items with NPC vendors using the Mangos protocol with
    /// advanced features for bulk operations, junk selling, and soulbound item handling.
    /// </summary>
    public class VendorNetworkClientComponent : IVendorNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<VendorNetworkClientComponent> _logger;

        private VendorInfo? _currentVendor;
        private DateTime? _lastOperationTime;
        private readonly ConcurrentQueue<SoulboundConfirmation> _pendingSoulboundConfirmations = new();

        // Item lists for junk detection - these could be loaded from configuration
        private static readonly HashSet<uint> SpecialItems =
        [
            6948 // Hearthstone
        ];

        private static readonly HashSet<string> JunkItemNames =
            ["broken", "cracked", "damaged", "worn", "tattered", "frayed", "bent", "rusty"];

        /// <summary>
        /// Initializes a new instance of the VendorNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public VendorNetworkClientComponent(IWorldClient worldClient, ILogger<VendorNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool IsVendorWindowOpen => _currentVendor?.IsWindowOpen ?? false;

        /// <inheritdoc />
        public VendorInfo? CurrentVendor => _currentVendor;

        /// <inheritdoc />
        public DateTime? LastOperationTime => _lastOperationTime;

        /// <inheritdoc />
        public event Action<VendorInfo>? VendorWindowOpened;

        /// <inheritdoc />
        public event Action? VendorWindowClosed;

        /// <inheritdoc />
        public event Action<VendorPurchaseData>? ItemPurchased;

        /// <inheritdoc />
        public event Action<VendorSaleData>? ItemSold;

        /// <inheritdoc />
        public event Action<VendorRepairData>? ItemsRepaired;

        /// <inheritdoc />
        public event Action<string>? VendorError;

        /// <inheritdoc />
        public event Action<SoulboundConfirmation>? SoulboundConfirmationRequired;

        /// <inheritdoc />
        public event Action<string, int, int>? BulkOperationProgress;

        /// <inheritdoc />
        public async Task OpenVendorAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening vendor interaction with: {VendorGuid:X}", vendorGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LIST_INVENTORY, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
                if (!CanPurchaseItem(itemId, quantity))
                {
                    throw new InvalidOperationException($"Cannot purchase item {itemId} (quantity: {quantity})");
                }

                _logger.LogDebug("Buying item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);

                var payload = new byte[16];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(itemId).CopyTo(payload, 8);
                BitConverter.GetBytes(quantity).CopyTo(payload, 12);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
        public async Task BuyItemBySlotAsync(ulong vendorGuid, byte vendorSlot, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Buying item from vendor slot {VendorSlot} (quantity: {Quantity}) from vendor: {VendorGuid:X}", vendorSlot, quantity, vendorGuid);

                var payload = new byte[13];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = vendorSlot;
                BitConverter.GetBytes(quantity).CopyTo(payload, 9);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

                _logger.LogInformation("Purchase request sent for vendor slot {VendorSlot} (quantity: {Quantity}) from vendor: {VendorGuid:X}", vendorSlot, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item from vendor slot {VendorSlot} from vendor: {VendorGuid:X}", vendorSlot, vendorGuid);
                VendorError?.Invoke($"Failed to buy item from slot: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task BuyItemBulkAsync(ulong vendorGuid, uint itemId, uint totalQuantity, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogDebug("Starting bulk purchase of item {ItemId} (total quantity: {TotalQuantity}) from vendor: {VendorGuid:X}", itemId, totalQuantity, vendorGuid);

                var vendorItem = FindVendorItem(itemId);
                if (vendorItem == null)
                {
                    throw new InvalidOperationException($"Item {itemId} not found in vendor inventory");
                }

                var stackSize = vendorItem.StackSize;
                var fullStacks = totalQuantity / stackSize;
                var remainingItems = totalQuantity % stackSize;
                var totalOperations = fullStacks + (remainingItems > 0 ? 1 : 0);

                _logger.LogDebug("Bulk purchase plan: {FullStacks} full stacks of {StackSize}, {RemainingItems} remaining items", fullStacks, stackSize, remainingItems);

                var operationCount = 0;

                // Buy full stacks
                for (uint i = 0; i < fullStacks; i++)
                {
                    if (DateTime.UtcNow - startTime > options.MaxOperationTime)
                    {
                        _logger.LogWarning("Bulk purchase timed out after {ElapsedTime}", DateTime.UtcNow - startTime);
                        break;
                    }

                    try
                    {
                        await BuyItemAsync(vendorGuid, itemId, stackSize, cancellationToken);
                        operationCount++;
                        BulkOperationProgress?.Invoke("Buying items", operationCount, (int)totalOperations);

                        if (options.DelayBetweenOperations > TimeSpan.Zero)
                        {
                            await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                        }
                    }
                    catch (Exception ex) when (options.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Failed to buy stack {StackNumber} during bulk purchase, continuing", i + 1);
                    }
                }

                // Buy remaining items
                if (remainingItems > 0)
                {
                    try
                    {
                        await BuyItemAsync(vendorGuid, itemId, remainingItems, cancellationToken);
                        operationCount++;
                        BulkOperationProgress?.Invoke("Buying items", operationCount, (int)totalOperations);
                    }
                    catch (Exception ex) when (options.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Failed to buy remaining {RemainingItems} items during bulk purchase", remainingItems);
                    }
                }

                _logger.LogInformation("Bulk purchase completed: {OperationCount}/{TotalOperations} operations successful for item {ItemId}", operationCount, totalOperations, itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk purchase failed for item {ItemId} from vendor: {VendorGuid:X}", itemId, vendorGuid);
                VendorError?.Invoke($"Bulk purchase failed: {ex.Message}");
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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM_IN_SLOT, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
                if (!CanSellItem(bagId, slotId, quantity))
                {
                    throw new InvalidOperationException($"Cannot sell item from bag {bagId} slot {slotId} (quantity: {quantity})");
                }

                _logger.LogDebug("Selling item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);

                var payload = new byte[14];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = bagId;
                payload[9] = slotId;
                BitConverter.GetBytes(quantity).CopyTo(payload, 10);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SELL_ITEM, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
        public async Task<uint> SellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();

            try
            {
                _logger.LogDebug("Starting to sell all junk items to vendor: {VendorGuid:X}", vendorGuid);

                var junkItems = await GetJunkItemsAsync(options);
                return await SellItemsAsync(vendorGuid, junkItems, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sell all junk to vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to sell all junk: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<uint> SellItemsAsync(ulong vendorGuid, IEnumerable<JunkItem> junkItems, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();
            var startTime = DateTime.UtcNow;
            uint totalValue = 0;

            try
            {
                var itemList = junkItems.ToList();
                _logger.LogDebug("Starting to sell {ItemCount} items to vendor: {VendorGuid:X}", itemList.Count, vendorGuid);

                var operationCount = 0;

                foreach (var junkItem in itemList)
                {
                    if (DateTime.UtcNow - startTime > options.MaxOperationTime)
                    {
                        _logger.LogWarning("Bulk sell operation timed out after {ElapsedTime}", DateTime.UtcNow - startTime);
                        break;
                    }

                    try
                    {
                        // Check for soulbound confirmation if needed
                        if (junkItem.IsBound && !options.AutoConfirmSoulbound)
                        {
                            var confirmation = new SoulboundConfirmation
                            {
                                ItemId = junkItem.ItemId,
                                ItemName = junkItem.ItemName,
                                ConfirmationType = SoulboundConfirmationType.SellItem,
                                ContextData = junkItem
                            };

                            SoulboundConfirmationRequired?.Invoke(confirmation);
                            _pendingSoulboundConfirmations.Enqueue(confirmation);
                            continue;
                        }

                        await SellItemAsync(vendorGuid, junkItem.BagId, junkItem.SlotId, junkItem.Quantity, cancellationToken);
                        totalValue += junkItem.EstimatedValue;
                        operationCount++;

                        BulkOperationProgress?.Invoke("Selling junk items", operationCount, itemList.Count);

                        if (options.DelayBetweenOperations > TimeSpan.Zero)
                        {
                            await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                        }
                    }
                    catch (Exception ex) when (options.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Failed to sell item {ItemName} from bag {BagId} slot {SlotId}, continuing", junkItem.ItemName, junkItem.BagId, junkItem.SlotId);
                    }
                }

                _logger.LogInformation("Bulk sell completed: {OperationCount}/{TotalItems} items sold for {TotalValue} copper", operationCount, itemList.Count, totalValue);
                return totalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sell items to vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to sell items: {ex.Message}");
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

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
                if (_currentVendor != null && !_currentVendor.CanRepair)
                {
                    throw new InvalidOperationException("Current vendor cannot repair items");
                }

                _logger.LogDebug("Repairing all items with vendor: {VendorGuid:X}", vendorGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                // Special values to indicate "repair all"
                payload[8] = 0xFF;
                payload[9] = 0xFF;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);
                _lastOperationTime = DateTime.UtcNow;

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
        public async Task<uint> GetRepairCostAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting repair cost from vendor: {VendorGuid:X}", vendorGuid);

                // This would typically involve querying game state or sending a specific packet
                // For now, return 0 as this would need game state integration
                await Task.CompletedTask;
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get repair cost from vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Failed to get repair cost: {ex.Message}");
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
                if (_currentVendor != null)
                {
                    _currentVendor.IsWindowOpen = false;
                    _currentVendor = null;
                }

                VendorWindowClosed?.Invoke();
                _lastOperationTime = DateTime.UtcNow;

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
            return _currentVendor?.IsWindowOpen == true && _currentVendor.VendorGuid == vendorGuid;
        }

        /// <inheritdoc />
        public IReadOnlyList<VendorItem> GetAvailableItems()
        {
            return _currentVendor?.AvailableItems ?? [];
        }

        /// <inheritdoc />
        public VendorItem? FindVendorItem(uint itemId)
        {
            return _currentVendor?.AvailableItems.FirstOrDefault(item => item.ItemId == itemId);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<JunkItem>> GetJunkItemsAsync(BulkVendorOptions? options = null)
        {
            options ??= new BulkVendorOptions();
            var junkItems = new List<JunkItem>();

            try
            {
                // This would typically integrate with the inventory system
                // For now, return empty list as this needs game state integration
                await Task.CompletedTask;

                _logger.LogDebug("Found {JunkItemCount} junk items matching criteria", junkItems.Count);
                return junkItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get junk items");
                return new List<JunkItem>();
            }
        }

        /// <inheritdoc />
        public bool CanPurchaseItem(uint itemId, uint quantity = 1)
        {
            var vendorItem = FindVendorItem(itemId);
            if (vendorItem == null)
            {
                return false;
            }

            // Check availability
            if (vendorItem.AvailableQuantity >= 0 && vendorItem.AvailableQuantity < quantity)
            {
                return false;
            }

            // Check if player can use the item
            if (!vendorItem.CanUse)
            {
                return false;
            }

            // Additional checks could be added here (money, reputation, etc.)
            return true;
        }

        /// <inheritdoc />
        public bool CanSellItem(byte bagId, byte slotId, uint quantity = 1)
        {
            // This would typically check the item in the specified slot
            // For now, assume it can be sold
            return true;
        }

        /// <inheritdoc />
        public async Task RespondToSoulboundConfirmationAsync(SoulboundConfirmation confirmation, bool accept, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Responding to soulbound confirmation for item {ItemId}: {Accept}", confirmation.ItemId, accept);

                if (accept)
                {
                    // Send confirmation packet - this would depend on the specific confirmation type
                    // For now, just log the acceptance
                    _logger.LogInformation("Accepted soulbound confirmation for item {ItemId}", confirmation.ItemId);
                }
                else
                {
                    _logger.LogInformation("Denied soulbound confirmation for item {ItemId}", confirmation.ItemId);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to respond to soulbound confirmation for item {ItemId}", confirmation.ItemId);
                VendorError?.Invoke($"Failed to respond to soulbound confirmation: {ex.Message}");
                throw;
            }
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

        /// <inheritdoc />
        public async Task<uint> QuickSellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick sell all junk with vendor: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);

                // Small delay to allow vendor window to open
                await Task.Delay(100, cancellationToken);

                var totalValue = await SellAllJunkAsync(vendorGuid, options, cancellationToken);

                // Small delay to allow sales to complete
                await Task.Delay(100, cancellationToken);

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick sell all junk completed with vendor: {VendorGuid:X}, total value: {TotalValue} copper", vendorGuid, totalValue);
                return totalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick sell all junk failed with vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Quick sell all junk failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();

            try
            {
                _logger.LogDebug("Performing comprehensive vendor visit with vendor: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                await RequestVendorInventoryAsync(vendorGuid, cancellationToken);

                // Small delay to allow vendor window to open
                await Task.Delay(200, cancellationToken);

                // Step 1: Repair all items (if vendor can repair)
                if (_currentVendor?.CanRepair == true)
                {
                    try
                    {
                        await RepairAllItemsAsync(vendorGuid, cancellationToken);
                        await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                    }
                    catch (Exception ex) when (options.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Failed to repair items during vendor visit, continuing");
                    }
                }

                // Step 2: Sell all junk items
                try
                {
                    var junkValue = await SellAllJunkAsync(vendorGuid, options, cancellationToken);
                    _logger.LogInformation("Sold junk items for {JunkValue} copper", junkValue);
                    await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                }
                catch (Exception ex) when (options.ContinueOnError)
                {
                    _logger.LogWarning(ex, "Failed to sell junk items during vendor visit, continuing");
                }

                // Step 3: Buy specified items
                if (itemsToBuy != null)
                {
                    foreach (var (itemId, quantity) in itemsToBuy)
                    {
                        try
                        {
                            await BuyItemAsync(vendorGuid, itemId, quantity, cancellationToken);
                            await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                        }
                        catch (Exception ex) when (options.ContinueOnError)
                        {
                            _logger.LogWarning(ex, "Failed to buy item {ItemId} during vendor visit, continuing", itemId);
                        }
                    }
                }

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Comprehensive vendor visit completed with vendor: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Comprehensive vendor visit failed with vendor: {VendorGuid:X}", vendorGuid);
                VendorError?.Invoke($"Vendor visit failed: {ex.Message}");
                throw;
            }
        }

        // Server response handlers

        /// <inheritdoc />
        public void HandleVendorWindowOpened(VendorInfo vendorInfo)
        {
            _currentVendor = vendorInfo;
            _currentVendor.IsWindowOpen = true;
            _currentVendor.LastInventoryUpdate = DateTime.UtcNow;

            VendorWindowOpened?.Invoke(vendorInfo);
            _logger.LogDebug("Vendor window opened for: {VendorGuid:X} ({VendorName})", vendorInfo.VendorGuid, vendorInfo.VendorName);
        }

        /// <inheritdoc />
        public void HandleItemPurchased(VendorPurchaseData purchaseData)
        {
            ItemPurchased?.Invoke(purchaseData);
            _logger.LogDebug("Item purchased: {ItemName} (quantity: {Quantity}, cost: {Cost})", purchaseData.ItemName, purchaseData.Quantity, purchaseData.TotalCost);
        }

        /// <inheritdoc />
        public void HandleItemSold(VendorSaleData saleData)
        {
            ItemSold?.Invoke(saleData);
            _logger.LogDebug("Item sold: {ItemName} (quantity: {Quantity}, value: {Value})", saleData.ItemName, saleData.Quantity, saleData.TotalValue);
        }

        /// <inheritdoc />
        public void HandleItemsRepaired(VendorRepairData repairData)
        {
            ItemsRepaired?.Invoke(repairData);
            _logger.LogDebug("Items repaired (cost: {Cost})", repairData.TotalCost);
        }

        /// <inheritdoc />
        public void HandleVendorError(string errorMessage)
        {
            VendorError?.Invoke(errorMessage);
            _logger.LogWarning("Vendor operation failed: {Error}", errorMessage);
        }

        /// <inheritdoc />
        public void HandleSoulboundConfirmationRequest(SoulboundConfirmation confirmation)
        {
            SoulboundConfirmationRequired?.Invoke(confirmation);
            _pendingSoulboundConfirmations.Enqueue(confirmation);
            _logger.LogDebug("Soulbound confirmation required for item: {ItemName}", confirmation.ItemName);
        }

        /// <summary>
        /// Helper method to determine if an item is considered junk based on quality and name.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="quality">The item quality.</param>
        /// <param name="options">Options for junk determination.</param>
        /// <returns>True if the item is considered junk, false otherwise.</returns>
        protected bool IsJunkItem(uint itemId, string itemName, ItemQuality quality, BulkVendorOptions options)
        {
            // Never sell special items
            if (SpecialItems.Contains(itemId))
            {
                return false;
            }

            // Check quality range
            if (quality < options.MinimumJunkQuality || quality > options.MaximumJunkQuality)
            {
                return false;
            }

            // Check for junk item name patterns
            var lowerName = itemName.ToLowerInvariant();
            return JunkItemNames.Any(pattern => lowerName.Contains(pattern));
        }
    }
}