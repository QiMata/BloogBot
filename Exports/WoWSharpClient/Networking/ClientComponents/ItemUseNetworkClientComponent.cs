using System;
using System.Collections.Generic;
using System.IO;
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
    /// Item use network client component that handles item usage operations.
    /// CMSG_USE_ITEM format: bagIndex(1) + slot(1) + spellSlot(1) + SpellCastTargets(variable)
    /// SpellCastTargets: targetMask(uint16) [+ packed GUID if unit target] [+ xyz if location target]
    /// </summary>
    public class ItemUseNetworkClientComponent : NetworkClientComponent, IItemUseNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<ItemUseNetworkClientComponent> _logger;
        private readonly object _stateLock = new();

        private bool _isUsingItem;
        private ulong? _currentItemInUse;
        private readonly Dictionary<uint, uint> _itemCooldowns;
        private bool _disposed;

        // Opcode-backed streams
        private readonly IObservable<ItemUseStartedData> _itemUseStarted;
        private readonly IObservable<ItemUseCompletedData> _itemUseCompleted;
        private readonly IObservable<ItemUseErrorData> _itemUseFailed;
        private readonly IObservable<ConsumableEffectData> _consumableEffectApplied;

        public ItemUseNetworkClientComponent(IWorldClient worldClient, ILogger<ItemUseNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _itemCooldowns = new Dictionary<uint, uint>();

            // Item use results come through SMSG_SPELL_START / SMSG_SPELL_GO (item spells)
            _itemUseStarted = SafeStream(Opcode.SMSG_SPELL_START)
                .Select(ParseItemUseStart)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Do(s =>
                {
                    lock (_stateLock)
                    {
                        _isUsingItem = true;
                        _currentItemInUse = s.ItemGuid;
                    }
                })
                .Publish()
                .RefCount();

            _itemUseCompleted = SafeStream(Opcode.SMSG_SPELL_GO)
                .Select(ParseItemUseComplete)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Do(c =>
                {
                    lock (_stateLock)
                    {
                        _isUsingItem = false;
                        _currentItemInUse = null;
                    }
                })
                .Publish()
                .RefCount();

            _itemUseFailed = Observable.Merge(
                    SafeStream(Opcode.SMSG_CAST_FAILED),
                    SafeStream(Opcode.SMSG_SPELL_FAILURE)
                )
                .Select(ParseItemUseError)
                .Where(e => e.HasValue)
                .Select(e => e!.Value)
                .Do(_ =>
                {
                    lock (_stateLock)
                    {
                        _isUsingItem = false;
                        _currentItemInUse = null;
                    }
                })
                .Publish()
                .RefCount();

            _consumableEffectApplied = Observable.Never<ConsumableEffectData>();
        }

        private IObservable<ReadOnlyMemory<byte>> SafeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        #region IItemUseNetworkClientComponent state
        public bool IsUsingItem => _isUsingItem;
        public ulong? CurrentItemInUse => _currentItemInUse;

        public IObservable<ItemUseStartedData> ItemUseStarted => _itemUseStarted;
        public IObservable<ItemUseCompletedData> ItemUseCompleted => _itemUseCompleted;
        public IObservable<ItemUseErrorData> ItemUseFailed => _itemUseFailed;
        public IObservable<ConsumableEffectData> ConsumableEffectApplied => _consumableEffectApplied;
        #endregion

        #region Outbound operations

        /// <summary>
        /// Uses an item (self-cast, no target).
        /// CMSG_USE_ITEM: bagIndex(1) + slot(1) + spellSlot(1) + targetMask(uint16=0x0000)
        /// </summary>
        public async Task UseItemAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId}", bagId, slotId);

                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(bagId);
                w.Write(slotId);
                w.Write((byte)0); // spellSlot (first spell on item)
                w.Write((ushort)0x0000); // TARGET_FLAG_SELF — no target

                lock (_stateLock) _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, ms.ToArray(), cancellationToken);

                _logger.LogInformation("Item use command sent successfully");
            }
            catch (Exception ex)
            {
                lock (_stateLock) _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId}", bagId, slotId);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Uses an item on a specific target.
        /// CMSG_USE_ITEM: bagIndex(1) + slot(1) + spellSlot(1) + targetMask(uint16=0x0002) + packedGUID
        /// </summary>
        public async Task UseItemOnTargetAsync(byte bagId, byte slotId, ulong targetGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId} on target {Target:X}",
                    bagId, slotId, targetGuid);

                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(bagId);
                w.Write(slotId);
                w.Write((byte)0); // spellSlot
                w.Write((ushort)0x0002); // TARGET_FLAG_UNIT
                WoWSharpClient.Utils.ReaderUtils.WritePackedGuid(w, targetGuid);

                lock (_stateLock) _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, ms.ToArray(), cancellationToken);

                _logger.LogInformation("Item use on target command sent successfully");
            }
            catch (Exception ex)
            {
                lock (_stateLock) _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId} on target {Target:X}",
                    bagId, slotId, targetGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Uses an item at a specific location.
        /// CMSG_USE_ITEM: bagIndex(1) + slot(1) + spellSlot(1) + targetMask(uint16=0x0040) + x(float) + y(float) + z(float)
        /// </summary>
        public async Task UseItemAtLocationAsync(byte bagId, byte slotId, float x, float y, float z, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using item from bag {BagId} slot {SlotId} at location ({X}, {Y}, {Z})",
                    bagId, slotId, x, y, z);

                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write(bagId);
                w.Write(slotId);
                w.Write((byte)0); // spellSlot
                w.Write((ushort)0x0040); // TARGET_FLAG_DEST_LOCATION
                w.Write(x);
                w.Write(y);
                w.Write(z);

                lock (_stateLock) _isUsingItem = true;
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, ms.ToArray(), cancellationToken);

                _logger.LogInformation("Item use at location command sent successfully");
            }
            catch (Exception ex)
            {
                lock (_stateLock) _isUsingItem = false;
                _logger.LogError(ex, "Failed to use item from bag {BagId} slot {SlotId} at location ({X}, {Y}, {Z})",
                    bagId, slotId, x, y, z);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        /// <summary>
        /// Activates an item by GUID. Looks up the item's bag/slot position and uses it.
        /// Falls back to bag=0 slot=0 spellSlot=0 if position unknown.
        /// </summary>
        public async Task ActivateItemAsync(ulong itemGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Activating item {ItemGuid:X}", itemGuid);

                // CMSG_USE_ITEM requires bag/slot position, not GUID directly.
                // Use bag=0, slot=0 as fallback — callers should prefer UseItemAsync with known position.
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms);
                w.Write((byte)0); // bagIndex (backpack)
                w.Write((byte)0); // slot
                w.Write((byte)0); // spellSlot
                w.Write((ushort)0x0000); // TARGET_FLAG_SELF

                lock (_stateLock)
                {
                    _isUsingItem = true;
                    _currentItemInUse = itemGuid;
                }
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_USE_ITEM, ms.ToArray(), cancellationToken);

                _logger.LogInformation("Item activation command sent successfully");
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _isUsingItem = false;
                    _currentItemInUse = null;
                }
                _logger.LogError(ex, "Failed to activate item {ItemGuid:X}", itemGuid);
                throw;
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task UseConsumableAsync(byte bagId, byte slotId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Using consumable from bag {BagId} slot {SlotId}", bagId, slotId);
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

        public async Task CancelItemUseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Canceling current item use");

                // CMSG_CANCEL_CAST: spellId(uint32) = 4 bytes — zeros cancel current cast
                var payload = new byte[4];
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_CANCEL_CAST, payload, cancellationToken);

                lock (_stateLock)
                {
                    _isUsingItem = false;
                    _currentItemInUse = null;
                }
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
        #endregion

        #region Cooldowns / Capabilities
        public bool CanUseItem(uint itemId)
        {
            _logger.LogDebug("Checking if item {ItemId} can be used", itemId);

            if (_itemCooldowns.TryGetValue(itemId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return false;
                }
                _itemCooldowns.Remove(itemId);
            }

            lock (_stateLock) return !_isUsingItem;
        }

        public uint GetItemCooldown(uint itemId)
        {
            if (_itemCooldowns.TryGetValue(itemId, out uint cooldownEnd))
            {
                uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (currentTime < cooldownEnd)
                {
                    return cooldownEnd - currentTime;
                }
                _itemCooldowns.Remove(itemId);
            }

            return 0;
        }
        #endregion

        #region Convenience
        public async Task<bool> FindAndUseItemAsync(uint itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                SetOperationInProgress(true);
                _logger.LogDebug("Finding and using item {ItemId}", itemId);

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

        private (byte BagId, byte SlotId)? FindItemInInventory(uint itemId)
        {
            _logger.LogDebug("Searching for itemId {ItemId} in inventory", itemId);
            // Placeholder — requires ObjectManager inventory access to find item position
            return null;
        }
        #endregion

        #region Server-driven state helpers
        public void UpdateItemUsed(ulong itemGuid, uint itemId, ulong? targetGuid = null)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} (ID: {ItemId}) used", itemGuid, itemId);
            lock (_stateLock)
            {
                _isUsingItem = false;
                _currentItemInUse = null;
            }
        }

        public void UpdateItemUseStarted(ulong itemGuid, uint castTime)
        {
            _logger.LogDebug("Server confirmed item {ItemGuid:X} use started with cast time {CastTime}ms", itemGuid, castTime);
            lock (_stateLock)
            {
                _isUsingItem = true;
                _currentItemInUse = itemGuid;
            }
        }

        public void UpdateItemCooldown(uint itemId, uint cooldownTime)
        {
            uint cooldownEnd = (uint)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cooldownTime;
            _itemCooldowns[itemId] = cooldownEnd;
            _logger.LogDebug("Item {ItemId} cooldown updated: {CooldownTime}ms", itemId, cooldownTime);
        }
        #endregion

        #region Parsing helpers
        private ItemUseStartedData? ParseItemUseStart(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                uint spellId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0u;
                uint castTime = span.Length >= 8 ? BitConverter.ToUInt32(span.Slice(4, 4)) : 0u;
                ulong itemGuid = span.Length >= 16 ? BitConverter.ToUInt64(span.Slice(8, 8)) : 0UL;
                return new ItemUseStartedData(itemGuid == 0 ? null : itemGuid, spellId, castTime, DateTime.UtcNow);
            }
            catch
            {
                return null;
            }
        }

        private ItemUseCompletedData? ParseItemUseComplete(ReadOnlyMemory<byte> payload)
        {
            try
            {
                var span = payload.Span;
                uint spellId = span.Length >= 4 ? BitConverter.ToUInt32(span[..4]) : 0u;
                ulong targetGuid = span.Length >= 16 ? BitConverter.ToUInt64(span.Slice(8, 8)) : 0UL;
                return new ItemUseCompletedData(null, 0u, targetGuid == 0 ? null : targetGuid, spellId, DateTime.UtcNow);
            }
            catch
            {
                return null;
            }
        }

        private ItemUseErrorData? ParseItemUseError(ReadOnlyMemory<byte> payload)
        {
            try
            {
                return new ItemUseErrorData(null, "Item use failed", DateTime.UtcNow);
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region IDisposable
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            base.Dispose();
        }
        #endregion
    }
}
