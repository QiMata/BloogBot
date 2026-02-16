using System;
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
        public EquipmentNetworkClientComponent(IWorldClient worldClient, ILogger<EquipmentNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // SMSG_INVENTORY_CHANGE_FAILURE (0x112): real error feedback from server
            _equipmentOperations = SafeOpcodeStream(Opcode.SMSG_INVENTORY_CHANGE_FAILURE)
                .Select(ParseInventoryChangeFailure)
                .Do(op => _logger.LogDebug("Equipment op result: {Result} Item: {ItemGuid:X} Slot: {Slot}", op.Result, op.ItemGuid, op.Slot))
                .Publish()
                .RefCount();

            // Equipment changes come through SMSG_UPDATE_OBJECT (item field updates)
            _equipmentChanges = Observable.Never<EquipmentChangeData>();

            // Durability changes come through SMSG_UPDATE_OBJECT (ITEM_FIELD_DURABILITY updates)
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

        /// <summary>
        /// Equips an item to the best available equipment slot (server auto-selects).
        /// CMSG_AUTOEQUIP_ITEM (0x10A): srcBag(1) + srcSlot(1) = 2 bytes
        /// Note: equipSlot parameter is ignored — server determines the optimal slot.
        /// </summary>
        public async Task EquipItemAsync(byte bagId, byte slotId, EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Equipping item from bag {BagId} slot {SlotId} (server will auto-select slot)", bagId, slotId);

                // CMSG_AUTOEQUIP_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
                // Server determines the best equipment slot automatically
                var payload = new byte[2];
                payload[0] = bagId;
                payload[1] = slotId;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOEQUIP_ITEM, payload, cancellationToken);
                _logger.LogInformation("Item equip command sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to equip item from bag {BagId} slot {SlotId}", bagId, slotId);
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

                // CMSG_AUTOEQUIP_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
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

        /// <summary>
        /// Unequips an item to the first available bag slot.
        /// CMSG_AUTOSTORE_BAG_ITEM (0x10B): srcBag(1) + srcSlot(1) + dstBag(1) = 3 bytes
        /// Equipment slots are in INVENTORY_SLOT_BAG_0 (255), dstBag=255 means auto-find space.
        /// </summary>
        public async Task UnequipItemAsync(EquipmentSlot equipSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unequipping item from equipment slot {EquipSlot}", equipSlot);

                // CMSG_AUTOSTORE_BAG_ITEM: srcBag(1) + srcSlot(1) + dstBag(1) = 3 bytes
                var payload = new byte[3];
                payload[0] = 255; // INVENTORY_SLOT_BAG_0 — equipment container
                payload[1] = (byte)equipSlot;
                payload[2] = 255; // INVENTORY_SLOT_BAG_0 — auto-find space in any bag

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

        /// <summary>
        /// Unequips an item to a specific bag slot.
        /// CMSG_SWAP_ITEM (0x10C): dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
        /// </summary>
        public async Task UnequipItemToSlotAsync(EquipmentSlot equipSlot, byte targetBag, byte targetSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Unequipping item from equipment slot {EquipSlot} to bag {TargetBag} slot {TargetSlot}",
                    equipSlot, targetBag, targetSlot);

                // CMSG_SWAP_ITEM: dstBag, dstSlot, srcBag, srcSlot
                var payload = new byte[4];
                payload[0] = targetBag;
                payload[1] = targetSlot;
                payload[2] = 255; // INVENTORY_SLOT_BAG_0 — equipment container
                payload[3] = (byte)equipSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);
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

        /// <summary>
        /// Swaps two equipment slots.
        /// CMSG_SWAP_INV_ITEM (0x10D): srcSlot(1) + dstSlot(1) = 2 bytes
        /// Both equipment slots are in the same container (INVENTORY_SLOT_BAG_0).
        /// </summary>
        public async Task SwapEquipmentAsync(EquipmentSlot firstSlot, EquipmentSlot secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping equipment between slots {FirstSlot} and {SecondSlot}", firstSlot, secondSlot);

                // CMSG_SWAP_INV_ITEM: srcSlot(1) + dstSlot(1) = 2 bytes
                // Both equipment slots are indices within INVENTORY_SLOT_BAG_0
                var payload = new byte[2];
                payload[0] = (byte)firstSlot;
                payload[1] = (byte)secondSlot;

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

        /// <summary>
        /// Swaps an equipment slot with a bag slot.
        /// CMSG_SWAP_ITEM (0x10C): dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
        /// </summary>
        public async Task SwapEquipmentWithInventoryAsync(EquipmentSlot equipSlot, byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping equipment slot {EquipSlot} with bag {BagId} slot {SlotId}",
                    equipSlot, bagId, slotId);

                // CMSG_SWAP_ITEM: dstBag, dstSlot, srcBag, srcSlot
                var payload = new byte[4];
                payload[0] = bagId;
                payload[1] = slotId;
                payload[2] = 255; // INVENTORY_SLOT_BAG_0 — equipment container
                payload[3] = (byte)equipSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);
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
                _logger.LogDebug("Auto-equipping all available items");
                await ProcessInventoryForAutoEquip(cancellationToken);
                _logger.LogInformation("Auto-equip all command completed");
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

                foreach (var slot in Enum.GetValues<EquipmentSlot>())
                {
                    if (IsSlotEquipped(slot))
                    {
                        await UnequipItemAsync(slot, cancellationToken);
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

        public void UpdateEquippedItem(EquipmentSlot slot, EquippedItem? item)
        {
            var previousItem = _equippedItems.TryGetValue(slot, out var prev) ? prev : null;
            _equippedItems[slot] = item;

            _logger.LogDebug("Equipment slot {Slot} updated: {PreviousItem} -> {NewItem}",
                slot, previousItem?.ItemGuid ?? 0, item?.ItemGuid ?? 0);
        }

        public void UpdateItemDurability(EquipmentSlot slot, uint currentDurability, uint maxDurability)
        {
            _itemDurability[slot] = new ItemDurability(currentDurability, maxDurability);

            _logger.LogDebug("Durability updated for slot {Slot}: {Current}/{Max}",
                slot, currentDurability, maxDurability);
        }

        #endregion

        #region SMSG_INVENTORY_CHANGE_FAILURE Parser

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        /// <summary>
        /// SMSG_INVENTORY_CHANGE_FAILURE (0x112)
        /// Format: msg(uint8) [+ requiredLevel(uint32) if msg==CantEquipLevelI] + itemGuid1(uint64) + itemGuid2(uint64) + bagType(uint8)
        /// If msg == Ok (0), only the 1-byte msg is sent.
        /// </summary>
        private static EquipmentOperationData ParseInventoryChangeFailure(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 1)
            {
                return new EquipmentOperationData
                {
                    Slot = default,
                    ItemGuid = 0,
                    Result = EquipmentResult.Unknown,
                    ErrorMessage = "Empty inventory change failure packet",
                    Timestamp = DateTime.UtcNow
                };
            }

            byte errorCode = span[0];
            var inventoryResult = (InventoryResult)errorCode;
            int offset = 1;

            // CantEquipLevelI has extra uint32 requiredLevel
            uint requiredLevel = 0;
            if (inventoryResult == InventoryResult.CantEquipLevelI && offset + 4 <= span.Length)
            {
                requiredLevel = BitConverter.ToUInt32(span.Slice(offset, 4));
                offset += 4;
            }

            // Read item GUIDs
            ulong itemGuid1 = 0;
            if (offset + 8 <= span.Length)
            {
                itemGuid1 = BitConverter.ToUInt64(span.Slice(offset, 8));
                offset += 8;
            }

            // Map InventoryResult to EquipmentResult
            var equipResult = inventoryResult switch
            {
                InventoryResult.Ok => EquipmentResult.Success,
                InventoryResult.CantEquipLevelI => EquipmentResult.InsufficientLevel,
                InventoryResult.YouCanNeverUseThatItem or InventoryResult.YouCanNeverUseThatItem2
                    => EquipmentResult.ItemNotEquippable,
                InventoryResult.ItemDoesntGoToSlot => EquipmentResult.InvalidItem,
                InventoryResult.BagFull => EquipmentResult.BagFull,
                InventoryResult.CantDropSoulbound => EquipmentResult.ItemBound,
                InventoryResult.ItemNotFound => EquipmentResult.InvalidItem,
                _ => EquipmentResult.Unknown
            };

            string errorMessage = inventoryResult switch
            {
                InventoryResult.CantEquipLevelI => $"Requires level {requiredLevel}",
                InventoryResult.BagFull => "Inventory is full",
                InventoryResult.ItemDoesntGoToSlot => "Item doesn't go in that slot",
                InventoryResult.CantDropSoulbound => "Cannot drop soulbound item",
                _ => $"Equipment error: {inventoryResult} ({errorCode})"
            };

            return new EquipmentOperationData
            {
                Slot = default,
                ItemGuid = itemGuid1,
                Result = equipResult,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };
        }

        #endregion

        #region Private Helper Methods

        private static bool IsWeaponSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.MainHand ||
                   slot == EquipmentSlot.OffHand ||
                   slot == EquipmentSlot.Ranged;
        }

        private async Task ProcessInventoryForAutoEquip(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Processing inventory for auto-equip");
            await Task.Delay(10, cancellationToken);
        }

        #endregion

        #region IDisposable Implementation

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
