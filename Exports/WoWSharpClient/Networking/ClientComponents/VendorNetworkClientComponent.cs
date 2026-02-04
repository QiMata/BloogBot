using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    public class VendorNetworkClientComponent : NetworkClientComponent, IVendorNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<VendorNetworkClientComponent> _logger;

        private VendorInfo? _currentVendor;
        private bool _disposed;
        private readonly ConcurrentQueue<SoulboundConfirmation> _pendingSoulboundConfirmations = new();

        private static readonly HashSet<uint> SpecialItems = [ 6948 ];
        private static readonly HashSet<string> JunkItemNames = ["broken", "cracked", "damaged", "worn", "tattered", "frayed", "bent", "rusty"];

        // Subjects for observable API
        private readonly Subject<VendorInfo> _vendorWindowsOpenedSubject = new();
        private readonly Subject<Unit> _vendorWindowsClosedSubject = new();
        private readonly Subject<VendorPurchaseData> _itemsPurchasedSubject = new();
        private readonly Subject<VendorSaleData> _itemsSoldSubject = new();
        private readonly Subject<VendorRepairData> _itemsRepairedSubject = new();
        private readonly Subject<string> _vendorErrorsSubject = new();
        private readonly Subject<SoulboundConfirmation> _soulboundConfirmationsSubject = new();

        public VendorNetworkClientComponent(IWorldClient worldClient, ILogger<VendorNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Wire opcode streams -> subjects. Best-effort parsing.
            SafeOpcodeStream(Opcode.SMSG_LIST_INVENTORY)
                .Subscribe(payload =>
                {
                    var info = ParseVendorList(payload);
                    _currentVendor = info;
                    _currentVendor.IsWindowOpen = true;
                    _currentVendor.LastInventoryUpdate = DateTime.UtcNow;
                    _logger.LogDebug("Vendor window opened for: {VendorGuid:X} ({VendorName})", info.VendorGuid, info.VendorName);
                    _vendorWindowsOpenedSubject.OnNext(info);
                });

            SafeOpcodeStream(Opcode.SMSG_GOSSIP_COMPLETE)
                .Subscribe(_ =>
                {
                    if (_currentVendor != null)
                    {
                        _currentVendor.IsWindowOpen = false;
                        _currentVendor = null;
                    }
                    _logger.LogDebug("Vendor window closed by server");
                    _vendorWindowsClosedSubject.OnNext(Unit.Default);
                });
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        // Public observable properties
        public IObservable<VendorInfo> VendorWindowsOpened => _vendorWindowsOpenedSubject.AsObservable();
        public IObservable<Unit> VendorWindowsClosed => _vendorWindowsClosedSubject.AsObservable();
        public IObservable<VendorPurchaseData> ItemsPurchased => _itemsPurchasedSubject.AsObservable();
        public IObservable<VendorSaleData> ItemsSold => _itemsSoldSubject.AsObservable();
        public IObservable<VendorRepairData> ItemsRepairEvents => _itemsRepairedSubject.AsObservable();
        public IObservable<string> VendorErrors => _vendorErrorsSubject.AsObservable();
        public IObservable<SoulboundConfirmation> SoulboundConfirmations => _soulboundConfirmationsSubject.AsObservable();

        public bool IsVendorWindowOpen => _currentVendor?.IsWindowOpen ?? false;
        public VendorInfo? CurrentVendor => _currentVendor;

        public async Task OpenVendorAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Opening vendor interaction with: {VendorGuid:X}", vendorGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _logger.LogInformation("Vendor interaction initiated with: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open vendor interaction with: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task RequestVendorInventoryAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting vendor inventory from: {VendorGuid:X}", vendorGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_LIST_INVENTORY, payload, cancellationToken);

                _logger.LogInformation("Vendor inventory request sent to: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request vendor inventory from: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task BuyItemAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
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

                _logger.LogInformation("Purchase request sent for item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item {ItemId} from vendor: {VendorGuid:X}", itemId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task BuyItemBySlotAsync(ulong vendorGuid, byte vendorSlot, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Buying item from vendor slot {VendorSlot} (quantity: {Quantity}) from vendor: {VendorGuid:X}", vendorSlot, quantity, vendorGuid);

                var payload = new byte[13];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = vendorSlot;
                BitConverter.GetBytes(quantity).CopyTo(payload, 9);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM, payload, cancellationToken);

                _logger.LogInformation("Purchase request sent for vendor slot {VendorSlot} (quantity: {Quantity}) from vendor: {VendorGuid:X}", vendorSlot, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item from vendor slot {VendorSlot} from vendor: {VendorGuid:X}", vendorSlot, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task BuyItemBulkAsync(ulong vendorGuid, uint itemId, uint totalQuantity, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();
            var startTime = DateTime.UtcNow;

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Starting bulk purchase of item {ItemId} (total quantity: {TotalQuantity}) from vendor: {VendorGuid:X}", itemId, totalQuantity, vendorGuid);

                var vendorItem = FindVendorItem(itemId);
                if (vendorItem == null)
                {
                    throw new InvalidOperationException($"Item {itemId} not found in vendor inventory");
                }

                var stackSize = vendorItem.StackSize == 0 ? 1u : vendorItem.StackSize;
                var fullStacks = totalQuantity / stackSize;
                var remainingItems = totalQuantity % stackSize;
                var totalOperations = (int)(fullStacks + (remainingItems > 0 ? 1 : 0));

                _logger.LogDebug("Bulk purchase plan: {FullStacks} full stacks of {StackSize}, {RemainingItems} remaining items", fullStacks, stackSize, remainingItems);

                var operationCount = 0;

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

                if (remainingItems > 0)
                {
                    try
                    {
                        await BuyItemAsync(vendorGuid, itemId, remainingItems, cancellationToken);
                        operationCount++;
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
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task BuyItemInSlotAsync(ulong vendorGuid, uint itemId, uint quantity, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Buying item {ItemId} (quantity: {Quantity}) into bag {BagId} slot {SlotId} from vendor: {VendorGuid:X}", itemId, quantity, bagId, slotId, vendorGuid);

                var payload = new byte[18];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(itemId).CopyTo(payload, 8);
                BitConverter.GetBytes(quantity).CopyTo(payload, 12);
                payload[16] = bagId;
                payload[17] = slotId;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_BUY_ITEM_IN_SLOT, payload, cancellationToken);

                _logger.LogInformation("Purchase request sent for item {ItemId} (quantity: {Quantity}) into bag {BagId} slot {SlotId} from vendor: {VendorGuid:X}", itemId, quantity, bagId, slotId, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to buy item {ItemId} into slot from vendor: {VendorGuid:X}", itemId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SellItemAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
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

                _logger.LogInformation("Sell request sent for item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sell item from bag {BagId} slot {SlotId} to vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task<uint> SellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();

            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Starting to sell all junk items to vendor: {VendorGuid:X}", vendorGuid);

                var junkItems = await GetJunkItemsAsync(options);
                return await SellItemsAsync(vendorGuid, junkItems, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sell all junk to vendor: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task<uint> SellItemsAsync(ulong vendorGuid, IEnumerable<JunkItem> junkItems, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();
            var startTime = DateTime.UtcNow;
            uint totalValue = 0;

            try
            {
                SetOperationInProgress(true);
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
                        if (junkItem.IsBound && !options.AutoConfirmSoulbound)
                        {
                            var confirmation = new SoulboundConfirmation
                            {
                                ItemId = junkItem.ItemId,
                                ItemName = junkItem.ItemName,
                                ConfirmationType = SoulboundConfirmationType.SellItem,
                                ContextData = junkItem
                            };

                            _pendingSoulboundConfirmations.Enqueue(confirmation);
                            _logger.LogDebug("Queued soulbound confirmation for item: {ItemName}", junkItem.ItemName);
                            _soulboundConfirmationsSubject.OnNext(confirmation);
                            continue;
                        }

                        await SellItemAsync(vendorGuid, junkItem.BagId, junkItem.SlotId, junkItem.Quantity, cancellationToken);
                        totalValue += junkItem.EstimatedValue;
                        operationCount++;

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
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task RepairItemAsync(ulong vendorGuid, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Repairing item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = bagId;
                payload[9] = slotId;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);

                _logger.LogInformation("Repair request sent for item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repair item from bag {BagId} slot {SlotId} with vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                if (_currentVendor != null && !_currentVendor.CanRepair)
                {
                    throw new InvalidOperationException("Current vendor cannot repair items");
                }

                _logger.LogDebug("Repairing all items with vendor: {VendorGuid:X}", vendorGuid);

                var payload = new byte[10];
                BitConverter.GetBytes(vendorGuid).CopyTo(payload, 0);
                payload[8] = 0xFF;
                payload[9] = 0xFF;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_REPAIR_ITEM, payload, cancellationToken);

                _logger.LogInformation("Repair all request sent with vendor: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to repair all items with vendor: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task<uint> GetRepairCostAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Requesting repair cost from vendor: {VendorGuid:X}", vendorGuid);

                await Task.CompletedTask;
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get repair cost from vendor: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task CloseVendorAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Closing vendor window");

                if (_currentVendor != null)
                {
                    _currentVendor.IsWindowOpen = false;
                    _currentVendor = null;
                }

                _logger.LogInformation("Vendor window closed");
                _vendorWindowsClosedSubject.OnNext(Unit.Default);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close vendor window");
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public bool IsVendorOpen(ulong vendorGuid)
        {
            return _currentVendor?.IsWindowOpen == true && _currentVendor.VendorGuid == vendorGuid;
        }

        public IReadOnlyList<VendorItem> GetAvailableItems()
        {
            return _currentVendor?.AvailableItems ?? [];
        }

        public VendorItem? FindVendorItem(uint itemId)
        {
            return _currentVendor?.AvailableItems.FirstOrDefault(item => item.ItemId == itemId);
        }

        public async Task<IReadOnlyList<JunkItem>> GetJunkItemsAsync(BulkVendorOptions? options = null)
        {
            options ??= new BulkVendorOptions();
            var junkItems = new List<JunkItem>();

            try
            {
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

        public bool CanPurchaseItem(uint itemId, uint quantity = 1)
        {
            var vendorItem = FindVendorItem(itemId);
            if (vendorItem == null)
            {
                return false;
            }

            if (vendorItem.AvailableQuantity >= 0 && vendorItem.AvailableQuantity < quantity)
            {
                return false;
            }

            if (!vendorItem.CanUse)
            {
                return false;
            }

            return true;
        }

        public bool CanSellItem(byte bagId, byte slotId, uint quantity = 1)
        {
            return true;
        }

        public async Task RespondToSoulboundConfirmationAsync(SoulboundConfirmation confirmation, bool accept, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Responding to soulbound confirmation for item {ItemId}: {Accept}", confirmation.ItemId, accept);

                if (accept)
                {
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
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickBuyAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick buy of item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                await RequestVendorInventoryAsync(vendorGuid, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await BuyItemAsync(vendorGuid, itemId, quantity, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick buy completed for item {ItemId} (quantity: {Quantity}) from vendor: {VendorGuid:X}", itemId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick buy failed for item {ItemId} from vendor: {VendorGuid:X}", itemId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickSellAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick sell of item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await SellItemAsync(vendorGuid, bagId, slotId, quantity, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick sell completed for item from bag {BagId} slot {SlotId} (quantity: {Quantity}) to vendor: {VendorGuid:X}", bagId, slotId, quantity, vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick sell failed for item from bag {BagId} slot {SlotId} to vendor: {VendorGuid:X}", bagId, slotId, vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickRepairAllAsync(ulong vendorGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick repair all with vendor: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await RepairAllItemsAsync(vendorGuid, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick repair all completed with vendor: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick repair all failed with vendor: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task<uint> QuickSellAllJunkAsync(ulong vendorGuid, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Performing quick sell all junk with vendor: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);

                await Task.Delay(100, cancellationToken);

                var totalValue = await SellAllJunkAsync(vendorGuid, options, cancellationToken);

                await Task.Delay(100, cancellationToken);

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick sell all junk completed with vendor: {VendorGuid:X}, total value: {TotalValue} copper", vendorGuid, totalValue);
                return totalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick sell all junk failed with vendor: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, BulkVendorOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new BulkVendorOptions();
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Starting quick vendor visit to: {VendorGuid:X}", vendorGuid);

                await OpenVendorAsync(vendorGuid, cancellationToken);
                await RequestVendorInventoryAsync(vendorGuid, cancellationToken);

                if (options.DelayBetweenOperations > TimeSpan.Zero)
                {
                    await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                }

                if (_currentVendor?.CanRepair == true)
                {
                    await RepairAllItemsAsync(vendorGuid, cancellationToken);
                }

                if (itemsToBuy != null && itemsToBuy.Count > 0)
                {
                    foreach (var kvp in itemsToBuy)
                    {
                        var itemId = kvp.Key;
                        var quantity = kvp.Value;

                        if (CanPurchaseItem(itemId, quantity))
                        {
                            await BuyItemAsync(vendorGuid, itemId, quantity, cancellationToken);
                        }

                        if (options.DelayBetweenOperations > TimeSpan.Zero)
                        {
                            await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                        }
                    }
                }

                if (options.DelayBetweenOperations > TimeSpan.Zero)
                {
                    await Task.Delay(options.DelayBetweenOperations, cancellationToken);
                }

                await CloseVendorAsync(cancellationToken);

                _logger.LogInformation("Quick vendor visit completed for: {VendorGuid:X}", vendorGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick vendor visit failed for: {VendorGuid:X}", vendorGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        // Manual handlers to push into observables (used by tests)
        public void HandleVendorWindowOpened(VendorInfo vendorInfo)
        {
            _currentVendor = vendorInfo;
            _currentVendor.IsWindowOpen = true;
            _currentVendor.LastInventoryUpdate = DateTime.UtcNow;
            _logger.LogDebug("Vendor window opened for: {VendorGuid:X} ({VendorName})", vendorInfo.VendorGuid, vendorInfo.VendorName);
            _vendorWindowsOpenedSubject.OnNext(vendorInfo);
        }

        public void HandleItemPurchased(VendorPurchaseData purchaseData)
        {
            _logger.LogDebug("Item purchased: {ItemName} (quantity: {Quantity}, cost: {Cost})", purchaseData.ItemName, purchaseData.Quantity, purchaseData.TotalCost);
            _itemsPurchasedSubject.OnNext(purchaseData);
        }

        public void HandleItemSold(VendorSaleData saleData)
        {
            _logger.LogDebug("Item sold: {ItemName} (quantity: {Quantity}, value: {Value})", saleData.ItemName, saleData.Quantity, saleData.TotalValue);
            _itemsSoldSubject.OnNext(saleData);
        }

        public void HandleItemsRepaired(VendorRepairData repairData)
        {
            _logger.LogDebug("Items repaired (cost: {Cost})", repairData.TotalCost);
            _itemsRepairedSubject.OnNext(repairData);
        }

        public void HandleVendorError(string errorMessage)
        {
            _logger.LogWarning("Vendor operation failed: {Error}", errorMessage);
            _vendorErrorsSubject.OnNext(errorMessage);
        }

        public void HandleSoulboundConfirmationRequest(SoulboundConfirmation confirmation)
        {
            _pendingSoulboundConfirmations.Enqueue(confirmation);
            _logger.LogDebug("Soulbound confirmation required for item: {ItemName}", confirmation.ItemName);
            _soulboundConfirmationsSubject.OnNext(confirmation);
        }

        protected bool IsJunkItem(uint itemId, string itemName, ItemQuality quality, BulkVendorOptions options)
        {
            if (SpecialItems.Contains(itemId))
            {
                return false;
            }
            if (quality < options.MinimumJunkQuality || quality > options.MaximumJunkQuality)
            {
                return false;
            }
            var lowerName = itemName.ToLowerInvariant();
            return JunkItemNames.Any(pattern => lowerName.Contains(pattern));
        }

        #region Parsing helpers (best-effort)
        private static uint ReadUInt32(ReadOnlySpan<byte> span, int offset)
            => (uint)(span.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) : 0u);
        private static ulong ReadUInt64(ReadOnlySpan<byte> span, int offset)
            => span.Length >= offset + 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)) : 0UL;

        private VendorInfo ParseVendorList(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            ulong vendorGuid = ReadUInt64(span, 0);
            int offset = 8;
            uint count = span.Length >= offset + 4 ? ReadUInt32(span, offset) : 0u;
            offset += span.Length >= 12 ? 4 : 0;

            var info = new VendorInfo
            {
                VendorGuid = vendorGuid,
                VendorName = string.Empty,
                CanRepair = true,
                IsWindowOpen = true,
                AvailableItems = []
            };

            int recordMinSize = 16;
            for (int i = 0; i < count && span.Length >= offset + recordMinSize; i++)
            {
                var item = new VendorItem
                {
                    VendorSlot = (byte)(ReadUInt32(span, offset + 0) & 0xFF),
                    ItemId = ReadUInt32(span, offset + 4),
                    Price = ReadUInt32(span, offset + 8),
                    StackSize = ReadUInt32(span, offset + 12),
                    AvailableQuantity = -1,
                    MaxQuantity = -1,
                    CanUse = true
                };
                info.AvailableItems.Add(item);
                offset += recordMinSize;
            }

            return info;
        }

        private VendorPurchaseData ParsePurchaseSucceeded(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            var data = new VendorPurchaseData
            {
                VendorGuid = span.Length >= 8 ? ReadUInt64(span, 0) : 0UL,
                ItemId = ReadUInt32(span, span.Length >= 12 ? 8 : 0),
                Quantity = ReadUInt32(span, span.Length >= 16 ? 12 : 0),
                TotalCost = ReadUInt32(span, span.Length >= 20 ? 16 : 0),
                Result = VendorBuyResult.Success,
                Timestamp = DateTime.UtcNow
            };
            return data;
        }

        private VendorSaleData ParseSellSucceeded(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            var data = new VendorSaleData
            {
                VendorGuid = span.Length >= 8 ? ReadUInt64(span, 0) : 0UL,
                ItemId = ReadUInt32(span, span.Length >= 12 ? 8 : 0),
                Quantity = ReadUInt32(span, span.Length >= 16 ? 12 : 0),
                TotalValue = ReadUInt32(span, span.Length >= 20 ? 16 : 0),
                Result = VendorSellResult.Success,
                Timestamp = DateTime.UtcNow
            };
            return data;
        }

        private VendorRepairData ParseRepairResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            var data = new VendorRepairData
            {
                VendorGuid = span.Length >= 8 ? ReadUInt64(span, 0) : 0UL,
                IsRepairAll = true,
                TotalCost = ReadUInt32(span, span.Length >= 12 ? 8 : 0),
                Result = VendorRepairResult.Success,
                Timestamp = DateTime.UtcNow
            };
            return data;
        }

        private string ParseVendorError(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            uint code = ReadUInt32(span, 0);
            return $"Vendor operation failed (code {code})";
        }

        private SoulboundConfirmation? ParseSoulboundConfirmation(ReadOnlyMemory<byte> payload)
        {
            return null;
        }
        #endregion

        #region IDisposable Implementation
        public override void Dispose()
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing VendorNetworkClientComponent");

            _vendorWindowsOpenedSubject.OnCompleted();
            _vendorWindowsClosedSubject.OnCompleted();
            _itemsPurchasedSubject.OnCompleted();
            _itemsSoldSubject.OnCompleted();
            _itemsRepairedSubject.OnCompleted();
            _vendorErrorsSubject.OnCompleted();
            _soulboundConfirmationsSubject.OnCompleted();

            _vendorWindowsOpenedSubject.Dispose();
            _vendorWindowsClosedSubject.Dispose();
            _itemsPurchasedSubject.Dispose();
            _itemsSoldSubject.Dispose();
            _itemsRepairedSubject.Dispose();
            _vendorErrorsSubject.Dispose();
            _soulboundConfirmationsSubject.Dispose();

            _disposed = true;
            _logger.LogDebug("VendorNetworkClientComponent disposed");
            base.Dispose();
        }
        #endregion
    }
}