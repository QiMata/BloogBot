using System;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Inventory network client component.
    /// This implementation exposes only cold observables derived directly from <see cref="IWorldClient"/> opcode streams.
    /// No events or Subject/Relay based fan-out is used – consumers compose over the exposed streams.
    /// </summary>
    public class InventoryNetworkClientComponent : NetworkClientComponent, IInventoryNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<InventoryNetworkClientComponent> _logger;

        private uint _currentMoney;
        private bool _isInventoryOpen;
        private bool _disposed;

        // Enhanced inventory state tracking and validation.
        private readonly Dictionary<(byte BagId, byte SlotId), uint> _inventoryState = new();
        private readonly object _inventoryLock = new();

        // Lazy-built observables (no Subjects / events)
        private IObservable<ItemMovedData>? _itemMovedStream;
        private IObservable<ItemSplitData>? _itemSplitStream;
        private IObservable<ItemDestroyedData>? _itemDestroyedStream;
        private IObservable<string>? _inventoryErrorsStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="InventoryNetworkClientComponent"/> class.
        /// </summary>
        public InventoryNetworkClientComponent(IWorldClient worldClient, ILogger<InventoryNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsInventoryOpen => _isInventoryOpen;

        #region Reactive Streams
        // NOTE: Specific SMSG_* inventory opcodes are not present in the current Opcode enum excerpt.
        // Placeholder implementation wires to generic update / world state opcode(s) and attempts heuristic parsing.
        // When concrete inventory-related server opcodes are introduced, replace the placeholders below accordingly.

        // Chosen placeholder opcode (repurpose to real ones when available)
        private static readonly Opcode PlaceholderInventoryServerOpcode = Opcode.SMSG_UPDATE_WORLD_STATE;

        public IObservable<ItemMovedData> ItemMovedStream =>
            _itemMovedStream ??= _worldClient
                .RegisterOpcodeHandler(PlaceholderInventoryServerOpcode)
                .Select(TryParseItemMoved)
                .Where(m => m.HasValue)
                .Select(m => m!.Value);

        public IObservable<ItemSplitData> ItemSplitStream =>
            _itemSplitStream ??= _worldClient
                .RegisterOpcodeHandler(PlaceholderInventoryServerOpcode)
                .Select(TryParseItemSplit)
                .Where(s => s.HasValue)
                .Select(s => s!.Value);

        public IObservable<ItemDestroyedData> ItemDestroyedStream =>
            _itemDestroyedStream ??= _worldClient
                .RegisterOpcodeHandler(PlaceholderInventoryServerOpcode)
                .Select(TryParseItemDestroyed)
                .Where(d => d.HasValue)
                .Select(d => d!.Value);

        public IObservable<string> InventoryErrors =>
            _inventoryErrorsStream ??= _worldClient
                .RegisterOpcodeHandler(PlaceholderInventoryServerOpcode)
                .Select(TryParseInventoryError)
                .Where(e => e is not null)!;
        #endregion

        #region Outbound Operations
        public async Task MoveItemAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Moving item {SourceBag}:{SourceSlot} -> {DestBag}:{DestSlot}", sourceBag, sourceSlot, destinationBag, destinationSlot);

                var payload = new byte[4];
                payload[0] = sourceBag;
                payload[1] = sourceSlot;
                payload[2] = destinationBag;
                payload[3] = destinationSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move item {SBag}:{SSlot}->{DBag}:{DSlot}", sourceBag, sourceSlot, destinationBag, destinationSlot);
                // Error surfaced via InventoryErrors stream (placeholder – currently no direct push)
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SwapItemsAsync(byte firstBag, byte firstSlot, byte secondBag, byte secondSlot, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Swapping items {Bag1}:{Slot1} <-> {Bag2}:{Slot2}", firstBag, firstSlot, secondBag, secondSlot);

                var payload = new byte[4];
                payload[0] = firstBag;
                payload[1] = firstSlot;
                payload[2] = secondBag;
                payload[3] = secondSlot;

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_SWAP_INV_ITEM, payload, cancellationToken);
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

        public async Task SplitItemStackAsync(byte sourceBag, byte sourceSlot, byte destinationBag, byte destinationSlot, uint quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Splitting stack {Qty} from {SBag}:{SSlot} -> {DBag}:{DSlot}", quantity, sourceBag, sourceSlot, destinationBag, destinationSlot);

                var payload = new byte[8];
                payload[0] = sourceBag;
                payload[1] = sourceSlot;
                payload[2] = destinationBag;
                payload[3] = destinationSlot;
                BitConverter.GetBytes(quantity).CopyTo(payload, 4);

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

        public async Task DestroyItemAsync(byte bagId, byte slotId, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Destroying {Qty} of item at {Bag}:{Slot}", quantity, bagId, slotId);

                var payload = new byte[6];
                payload[0] = bagId;
                payload[1] = slotId;
                BitConverter.GetBytes(quantity).CopyTo(payload, 2);

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

        public async Task SortBagAsync(byte bagId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                var payload = new byte[1] { bagId };
                _logger.LogDebug("Sorting bag {Bag}", bagId);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_AUTOSTORE_BAG_ITEM, payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sort bag {Bag}", bagId);
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
                    if (kv.Value == itemId) count++; // quantity not tracked in placeholder model
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

        #region Placeholder Parsing
        private ItemMovedData? TryParseItemMoved(ReadOnlyMemory<byte> payload)
        {
            try
            {
                // Heuristic placeholder: expect 5 bytes (sBag,sSlot,dBag,dSlot) + 8 guid
                if (payload.Length == 12)
                {
                    var span = payload.Span;
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    byte sBag = span[8];
                    byte sSlot = span[9];
                    byte dBag = span[10];
                    byte dSlot = span[11];
                    return new ItemMovedData(guid, sBag, sSlot, dBag, dSlot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse item moved payload length {Len}", payload.Length);
            }
            return null;
        }

        private ItemSplitData? TryParseItemSplit(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length == 12)
                {
                    var span = payload.Span;
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    uint qty = BitConverter.ToUInt32(span[8..12]);
                    return new ItemSplitData(guid, qty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse item split payload length {Len}", payload.Length);
            }
            return null;
        }

        private ItemDestroyedData? TryParseItemDestroyed(ReadOnlyMemory<byte> payload)
        {
            try
            {
                if (payload.Length == 12)
                {
                    var span = payload.Span;
                    ulong guid = BitConverter.ToUInt64(span[..8]);
                    uint qty = BitConverter.ToUInt32(span[8..12]);
                    return new ItemDestroyedData(guid, qty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to parse item destroyed payload length {Len}", payload.Length);
            }
            return null;
        }

        private string? TryParseInventoryError(ReadOnlyMemory<byte> payload)
        {
            // Without concrete opcode layout we cannot decode specific errors – return null.
            return null;
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