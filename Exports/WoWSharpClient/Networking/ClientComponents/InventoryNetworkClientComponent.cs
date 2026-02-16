using System;
using System.Collections.Generic;
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
    /// Inventory network client component.
    /// Handles CMSG inventory operations (swap, split, destroy) and SMSG_INVENTORY_CHANGE_FAILURE parsing.
    /// Item state changes (moves, creation, destruction) come through SMSG_UPDATE_OBJECT, not dedicated opcodes.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InventoryNetworkClientComponent"/> class.
    /// </remarks>
    public class InventoryNetworkClientComponent(IWorldClient worldClient, ILogger<InventoryNetworkClientComponent> logger) : NetworkClientComponent, IInventoryNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
        private readonly ILogger<InventoryNetworkClientComponent> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private uint _currentMoney;
        private bool _isInventoryOpen;
        private bool _disposed;

        // Enhanced inventory state tracking and validation.
        private readonly Dictionary<(byte BagId, byte SlotId), uint> _inventoryState = new();
        private readonly object _inventoryLock = new();

        // Lazy-built observables
        private IObservable<ItemMovedData>? _itemMovedStream;
        private IObservable<ItemSplitData>? _itemSplitStream;
        private IObservable<ItemDestroyedData>? _itemDestroyedStream;
        private IObservable<string>? _inventoryErrorsStream;

        public bool IsInventoryOpen => _isInventoryOpen;

        #region Reactive Streams

        // Item movement/split/destruction state changes come through SMSG_UPDATE_OBJECT (object field changes),
        // not through dedicated SMSG opcodes. These streams are populated externally when the ObjectUpdateHandler
        // detects inventory changes. For now, expose empty streams that consumers can compose with.
        public IObservable<ItemMovedData> ItemMovedStream =>
            _itemMovedStream ??= Observable.Never<ItemMovedData>();

        public IObservable<ItemSplitData> ItemSplitStream =>
            _itemSplitStream ??= Observable.Never<ItemSplitData>();

        public IObservable<ItemDestroyedData> ItemDestroyedStream =>
            _itemDestroyedStream ??= Observable.Never<ItemDestroyedData>();

        // SMSG_INVENTORY_CHANGE_FAILURE: real opcode for inventory error feedback
        public IObservable<string> InventoryErrors =>
            _inventoryErrorsStream ??= SafeOpcodeStream(Opcode.SMSG_INVENTORY_CHANGE_FAILURE)
                .Select(ParseInventoryChangeFailure)
                .Where(e => e is not null)!;

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #endregion

        #region Outbound Operations

        /// <summary>
        /// Moves an item between two bag positions.
        /// CMSG_SWAP_ITEM (0x10C): dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
        /// </summary>
        public async Task MoveItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Moving item {SourceBag}:{SourceSlot} -> {DestBag}:{DestSlot}", sourceBag, sourceSlot, destinationBag, destinationSlot);

                // CMSG_SWAP_ITEM: dstBag, dstSlot, srcBag, srcSlot
                var payload = new byte[4];
                payload[0] = destinationBag;
                payload[1] = destinationSlot;
                payload[2] = sourceBag;
                payload[3] = sourceSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move item {SBag}:{SSlot}->{DBag}:{DSlot}", sourceBag, sourceSlot, destinationBag, destinationSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Swaps two items between bag positions.
        /// CMSG_SWAP_ITEM (0x10C): dstBag(1) + dstSlot(1) + srcBag(1) + srcSlot(1) = 4 bytes
        /// </summary>
        public async Task SwapItemsAsync(byte firstBag, byte firstSlot, byte secondBag, byte secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping items {Bag1}:{Slot1} <-> {Bag2}:{Slot2}", firstBag, firstSlot, secondBag, secondSlot);

                // CMSG_SWAP_ITEM: dstBag, dstSlot, srcBag, srcSlot
                var payload = new byte[4];
                payload[0] = secondBag;
                payload[1] = secondSlot;
                payload[2] = firstBag;
                payload[3] = firstSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to swap items {B1}:{S1} <-> {B2}:{S2}", firstBag, firstSlot, secondBag, secondSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Splits a stack of items.
        /// CMSG_SPLIT_ITEM (0x10E): srcBag(1) + srcSlot(1) + dstBag(1) + dstSlot(1) + count(uint8) = 5 bytes
        /// </summary>
        public async Task SplitItemStackAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Splitting stack {Qty} from {SBag}:{SSlot} -> {DBag}:{DSlot}", quantity, sourceBag, sourceSlot, destinationBag, destinationSlot);

                // CMSG_SPLIT_ITEM: srcBag, srcSlot, dstBag, dstSlot, uint8 count
                var payload = new byte[5];
                payload[0] = sourceBag;
                payload[1] = sourceSlot;
                payload[2] = destinationBag;
                payload[3] = destinationSlot;
                payload[4] = (byte)Math.Min(quantity, 255);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SPLIT_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to split stack {Qty} from {SBag}:{SSlot} -> {DBag}:{DSlot}", quantity, sourceBag, sourceSlot, destinationBag, destinationSlot);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SplitItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default)
            => await SplitItemStackAsync(sourceBag, sourceSlot, destinationBag, destinationSlot, quantity, cancellationToken);

        /// <summary>
        /// Destroys an item in the inventory.
        /// CMSG_DESTROYITEM (0x111): bag(1) + slot(1) + count(uint8) + reserved(3) = 6 bytes
        /// </summary>
        public async Task DestroyItemAsync(byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Destroying {Qty} of item at {Bag}:{Slot}", quantity, bagId, slotId);

                // CMSG_DESTROYITEM: bag, slot, uint8 count, 3x reserved uint8
                var payload = new byte[6];
                payload[0] = bagId;
                payload[1] = slotId;
                payload[2] = (byte)Math.Min(quantity, 255);
                payload[3] = 0; // reserved
                payload[4] = 0; // reserved
                payload[5] = 0; // reserved

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_DESTROYITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to destroy item {Bag}:{Slot}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Auto-stores an item from one bag to another.
        /// CMSG_AUTOSTORE_BAG_ITEM (0x10B): srcBag(1) + srcSlot(1) + dstBag(1) = 3 bytes
        /// </summary>
        public async Task SortBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Auto-storing items in bag {Bag}", bagId);

                // CMSG_AUTOSTORE_BAG_ITEM: srcBag, srcSlot, dstBag
                // This opcode auto-stores a single item, not a batch sort.
                // Send with srcBag=bagId, srcSlot=0, dstBag=255 (auto-find destination)
                var payload = new byte[3];
                payload[0] = bagId;
                payload[1] = 0;
                payload[2] = 255; // INVENTORY_SLOT_BAG_0 — auto-find space

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_BAG_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-store bag item {Bag}", bagId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task OpenBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _isInventoryOpen = true;
                _logger.LogDebug("Bag {Bag} opened (client-side only)", bagId);
                await Task.CompletedTask;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task CloseBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _isInventoryOpen = false;
                _logger.LogDebug("Bag {Bag} closed (client-side only)", bagId);
                await Task.CompletedTask;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }
        #endregion

        #region Inventory State Helpers
        public (byte BagId, byte SlotId)? FindEmptySlot(uint itemId = 0)
        {
            lock (_inventoryLock)
            {
                for (byte bagId = 0; bagId < 5; bagId++)
                {
                    for (byte slot = 0; slot < GetBagSlotCount(bagId); slot++)
                    {
                        if (!_inventoryState.ContainsKey((bagId, slot)))
                            return (bagId, slot);
                    }
                }
                return null;
            }
        }

        private byte GetBagSlotCount(byte bagId) => bagId == 0 ? (byte)16 : (byte)16; // placeholder

        public void UpdateInventorySlot(byte bagId, byte slotId, uint itemId, uint quantity = 0)
        {
            lock (_inventoryLock)
            {
                if (itemId == 0 || quantity == 0)
                {
                    _inventoryState.Remove((bagId, slotId));
                }
                else
                {
                    _inventoryState[(bagId, slotId)] = itemId;
                }
            }
        }

        public uint CountItem(uint itemId)
        {
            lock (_inventoryLock)
            {
                uint count = 0;
                foreach (var kv in _inventoryState)
                {
                    if (kv.Value == itemId) count++;
                }
                return count;
            }
        }

        public bool HasEnoughSpace(int requiredSlots) => GetFreeSlotCount() >= requiredSlots;

        public int GetFreeSlotCount()
        {
            lock (_inventoryLock)
            {
                int total = 0;
                for (byte bagId = 0; bagId < 5; bagId++) total += GetBagSlotCount(bagId);
                return total - _inventoryState.Count;
            }
        }
        #endregion

        #region SMSG_INVENTORY_CHANGE_FAILURE Parser

        /// <summary>
        /// SMSG_INVENTORY_CHANGE_FAILURE (0x112)
        /// Format: msg(uint8) [+ requiredLevel(uint32) if msg==CantEquipLevelI] + itemGuid1(uint64) + itemGuid2(uint64) + bagType(uint8)
        /// If msg == Ok (0), only the 1-byte msg is sent.
        /// </summary>
        private string? ParseInventoryChangeFailure(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                if (span.Length < 1) return null;

                byte errorCode = span[0];
                if (errorCode == (byte)InventoryResult.Ok) return null; // Success — no error

                var result = (InventoryResult)errorCode;
                int offset = 1;

                // CantEquipLevelI includes a required level field
                uint requiredLevel = 0;
                if (result == InventoryResult.CantEquipLevelI && offset + 4 <= span.Length)
                {
                    requiredLevel = BitConverter.ToUInt32(span.Slice(offset, 4));
                    offset += 4;
                }

                // Read item GUIDs if present
                ulong itemGuid1 = 0, itemGuid2 = 0;
                if (offset + 8 <= span.Length)
                {
                    itemGuid1 = BitConverter.ToUInt64(span.Slice(offset, 8));
                    offset += 8;
                }
                if (offset + 8 <= span.Length)
                {
                    itemGuid2 = BitConverter.ToUInt64(span.Slice(offset, 8));
                    offset += 8;
                }

                string errorMessage = result switch
                {
                    InventoryResult.CantEquipLevelI => $"Requires level {requiredLevel}",
                    InventoryResult.BagFull => "Inventory is full",
                    InventoryResult.ItemDoesntGoToSlot => "Item doesn't go in that slot",
                    InventoryResult.CantDropSoulbound => "Cannot drop soulbound item",
                    InventoryResult.ItemNotFound => "Item not found",
                    InventoryResult.ItemsCantBeSwapped => "Items can't be swapped",
                    InventoryResult.SlotIsEmpty => "Slot is empty",
                    InventoryResult.TooFarAwayFromBank => "Too far from bank",
                    InventoryResult.NotEnoughMoney => "Not enough money",
                    InventoryResult.YouAreDead => "Cannot do that while dead",
                    InventoryResult.YouAreStunned => "Cannot do that while stunned",
                    _ => $"Inventory error: {result} ({errorCode})"
                };

                _logger.LogWarning("Inventory change failure: {Error} (item1={Item1:X}, item2={Item2:X})",
                    errorMessage, itemGuid1, itemGuid2);

                return errorMessage;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse SMSG_INVENTORY_CHANGE_FAILURE payload length {Len}", payload.Length);
                return null;
            }
        }

        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("InventoryNetworkClientComponent disposed");
        }
        #endregion
    }
}
