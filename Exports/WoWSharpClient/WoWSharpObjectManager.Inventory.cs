using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;

namespace WoWSharpClient
{
    public partial class WoWSharpObjectManager
    {

        public void UseItem(int bagId, int slotId, ulong targetGuid = 0)
        {
            if (_woWClient == null) return;
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            byte srcBag = bagId == 0 ? (byte)0xFF : (byte)(18 + bagId);
            byte srcSlot = bagId == 0 ? (byte)(23 + slotId) : (byte)slotId;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(srcBag);
            w.Write(srcSlot);
            w.Write((byte)0); // spellSlot
            w.Write((ushort)0x0000); // TARGET_FLAG_SELF
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_USE_ITEM, ms.ToArray());
        }


        public ulong GetBackpackItemGuid(int parSlot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return 0;
            int index = parSlot * 2;
            if (index < 0 || index + 1 >= player.PackSlots.Length) return 0;
            return ((ulong)player.PackSlots[index + 1] << 32) | player.PackSlots[index];
        }


        public ulong GetEquippedItemGuid(EquipSlot slot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return 0;
            // EquipSlot enum is offset by 1 from WoW internal slot numbering
            // (EquipSlot.Head=1 → internal slot 0, etc.)
            int internalSlot = (int)slot - 1;
            if (internalSlot < 0) return 0; // Ammo=0 has no inventory slot
            int index = internalSlot * 2;
            if (index + 1 >= player.Inventory.Length) return 0;
            return ((ulong)player.Inventory[index + 1] << 32) | player.Inventory[index];
        }


        public IWoWItem GetEquippedItem(EquipSlot slot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return null;

            // Try VisibleItems first (already populated with full item data)
            int slotIndex = (int)slot - 1;
            if (slotIndex >= 0 && slotIndex < player.VisibleItems.Length)
            {
                var visible = player.VisibleItems[slotIndex];
                if (visible?.ItemId > 0) return visible;
            }

            // Fall back to GUID lookup in objects list
            var guid = GetEquippedItemGuid(slot);
            return FindItemByGuid(guid);
        }


        public IWoWItem GetContainedItem(int bagSlot, int slotId)
        {
            ulong itemGuid;
            if (bagSlot == 0)
            {
                // Backpack — look up from PackSlots
                itemGuid = GetBackpackItemGuid(slotId);
            }
            else
            {
                // Extra bag — find the bag container, then look up its slot
                var bagEquipSlot = (EquipSlot)(19 + bagSlot); // Bag0=20 for bagSlot 1
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) return null;

                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) return null;

                itemGuid = container.GetItemGuid(slotId);
            }

            return FindItemByGuid(itemGuid);
        }


        public IEnumerable<IWoWItem> GetEquippedItems()
        {
            var player = Player as WoWPlayer;
            if (player == null) return [];

            var items = new List<IWoWItem>();
            // Equipment slots: Head(1) through Ranged(18)
            for (var slot = EquipSlot.Head; slot <= EquipSlot.Ranged; slot++)
            {
                var item = GetEquippedItem(slot);
                if (item != null && item.ItemId > 0) items.Add(item);
            }
            return items;
        }


        public IEnumerable<IWoWItem> GetContainedItems()
        {
            var player = Player as WoWPlayer;
            if (player == null) return [];

            var items = new List<IWoWItem>();

            // Backpack (bag 0): 16 slots
            for (int slot = 0; slot < 16; slot++)
            {
                var item = GetContainedItem(0, slot);
                if (item != null && item.ItemId > 0) items.Add(item);
            }

            // Extra bags (bag 1-4)
            for (int bag = 1; bag <= 4; bag++)
            {
                var bagEquipSlot = (EquipSlot)(19 + bag);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) continue;

                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) continue;

                for (int slot = 0; slot < container.NumOfSlots; slot++)
                {
                    var item = FindItemByGuid(container.GetItemGuid(slot));
                    if (item != null && item.ItemId > 0) items.Add(item);
                }
            }

            return items;
        }


        public int CountFreeSlots(bool countSpecialSlots = false)
        {
            int freeSlots = 0;
            // Backpack: 16 slots
            for (int i = 0; i < 16; i++)
                if (GetContainedItem(0, i) == null) freeSlots++;
            // Extra bags
            for (int bag = 1; bag <= 4; bag++)
            {
                var bagEquipSlot = (EquipSlot)(19 + bag);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) continue;
                WoWContainer container;
                lock (_objectsLock)
                    container = _objects.FirstOrDefault(o => o.Guid == bagGuid) as WoWContainer;
                if (container == null) continue;
                for (int slot = 0; slot < container.NumOfSlots; slot++)
                    if (FindItemByGuid(container.GetItemGuid(slot)) == null) freeSlots++;
            }
            return freeSlots;
        }


        public uint GetItemCount(uint itemId)
        {
            uint count = 0;
            foreach (var item in GetContainedItems())
                if (item.ItemId == itemId) count += item.StackCount;
            return count;
        }


        public uint GetBagGuid(EquipSlot equipSlot)
        {
            // Return low 32 bits of the bag GUID for compatibility
            var guid = GetEquippedItemGuid(equipSlot);
            return (uint)(guid & 0xFFFFFFFF);
        }


        private WoWItem FindItemByGuid(ulong guid)
        {
            if (guid == 0) return null;
            lock (_objectsLock)
                return _objects.FirstOrDefault(o => o.Guid == guid) as WoWItem;
        }


        // Two-phase cursor emulation: PickupContainedItem stores source, PlaceItemInContainer sends CMSG_SWAP_ITEM
        private (byte Bag, byte Slot, int Quantity)? _cursorItem;

        public void PickupContainedItem(int bagSlot, int slotId, int quantity)
        {
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            _cursorItem = (srcBag, srcSlot, quantity);
        }


        public void PlaceItemInContainer(int bagSlot, int slotId)
        {
            if (_cursorItem == null) return;
            var src = _cursorItem.Value;
            _cursorItem = null;

            byte dstBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte dstSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;

            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.InventoryAgent != null)
                _ = factory.InventoryAgent.MoveItemAsync(src.Bag, src.Slot, dstBag, dstSlot);
        }


        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1)
        {
            if (_woWClient == null) { Log.Warning("[DestroyItem] _woWClient is null, cannot send packet"); return; }
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            byte count = quantity < 0 ? (byte)0xFF : (byte)Math.Min(quantity, 255);
            Log.Information("[DestroyItem] Sending CMSG_DESTROYITEM: bag=0x{Bag:X2}, slot={Slot}, count={Count}",
                srcBag, srcSlot, count);
            // CMSG_DESTROYITEM: bag(1) + slot(1) + count(1) + reserved(3) = 6 bytes
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_DESTROYITEM, [srcBag, srcSlot, count, 0, 0, 0]);
        }


        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot)
        {
            byte srcBag = bag == 0 ? (byte)0xFF : (byte)(18 + bag);
            byte srcSlot = bag == 0 ? (byte)(23 + slot) : (byte)slot;
            byte dstBag = destinationBag == 0 ? (byte)0xFF : (byte)(18 + destinationBag);
            byte dstSlot = destinationBag == 0 ? (byte)(23 + destinationSlot) : (byte)destinationSlot;

            var factory = _agentFactoryAccessor?.Invoke();
            if (factory?.InventoryAgent != null)
                _ = factory.InventoryAgent.SplitItemAsync(srcBag, srcSlot, dstBag, dstSlot, (uint)quantity);
        }


        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null)
        {
            if (_woWClient == null) return;
            // Map logical bag index (0=backpack, 1-4=extra bags) to WoW packet bag/slot values.
            // For backpack (0xFF): slot uses ABSOLUTE inventory index (23 = INVENTORY_SLOT_ITEM_START).
            // For extra bags (19-22): slot is relative within the bag container.
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            // CMSG_AUTOEQUIP_ITEM: srcBag(1) + srcSlot(1) = 2 bytes
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOEQUIP_ITEM, [srcBag, srcSlot]);
        }


        public void UnequipItem(EquipSlot slot)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            // EquipSlot (GameData.Core) is offset by 1 from EquipmentSlot (network layer):
            // EquipSlot.Head=1 → EquipmentSlot.Head=0, etc.
            var equipmentSlot = (EquipmentSlot)((int)slot - 1);
            factory.EquipmentAgent.UnequipItemAsync(equipmentSlot, CancellationToken.None)
                .GetAwaiter().GetResult();
        }


        public void InteractWithGameObject(ulong guid)
        {
            if (_woWClient == null) return;

            // Log player position + node distance for diagnostics (server silently drops if out of range)
            var player = Player;
            if (player != null)
            {
                var node = Objects.FirstOrDefault(o => o.Guid == guid);
                var nodeDist = node != null ? player.Position.DistanceTo(node.Position) : -1f;
                Log.Information("[GAMEOBJ_USE] Player pos=({X:F1},{Y:F1},{Z:F1}) flags=0x{Flags:X} nodeDist={Dist:F1}",
                    player.Position.X, player.Position.Y, player.Position.Z,
                    (uint)((WoWLocalPlayer)player).MovementFlags, nodeDist);
            }

            Log.Information("[GAMEOBJ_USE] _isBeingTeleported={Tp} _isInControl={Ctrl}",
                _isBeingTeleported, _isInControl);

            var payload = BitConverter.GetBytes(guid);
            Log.Information("[GAMEOBJ_USE] Sending CMSG_GAMEOBJ_USE for GUID=0x{Guid:X} (8 bytes: {Hex})",
                guid, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_GAMEOBJ_USE, payload);

            // Temporary packet sniffer: log all opcodes received for 5 seconds after GAMEOBJ_USE
            _sniffingGameObjUse = true;
            _sniffStartTime = DateTime.UtcNow;
            Task.Delay(5000).ContinueWith(_ =>
            {
                _sniffingGameObjUse = false;
                Log.Information("[GAMEOBJ_USE] Packet sniffer ended — no more logging");
            });
        }


        public void AutoStoreLootItem(byte slot)
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, new byte[] { slot });
        }


        public void ReleaseLoot(ulong lootGuid)
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT_RELEASE, BitConverter.GetBytes(lootGuid));
        }


        public async Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.LootingAgent.QuickLootAsync(targetGuid, ct);
            }
            else if (_woWClient != null)
            {
                // Fallback: send raw CMSG_LOOT packet
                await _woWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT, BitConverter.GetBytes(targetGuid));
            }
        }


        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.VendorAgent.QuickVendorVisitAsync(vendorGuid, itemsToBuy, cancellationToken: ct);
            }
        }


        public async Task BuyItemFromVendorAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            // Request vendor inventory via CMSG_LIST_INVENTORY (skips gossip)
            await factory.VendorAgent.RequestVendorInventoryAsync(vendorGuid, ct);
            await factory.VendorAgent.WaitForVendorWindowAsync(ct);

            // If vendor window opened, use validated buy; otherwise send raw packet
            if (factory.VendorAgent.IsVendorWindowOpen)
            {
                await factory.VendorAgent.BuyItemAsync(vendorGuid, itemId, quantity, ct);
            }
            else
            {
                await factory.VendorAgent.SendBuyItemPacketAsync(vendorGuid, itemId, quantity, ct);
            }

            await Task.Delay(100, ct);
            await factory.VendorAgent.CloseVendorAsync(ct);
        }


        public async Task SellItemToVendorAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            // BagContents uses sequential indices from GetContainedItems(), not WoW slot indices.
            // When bagId=0xFF, slotId is the sequential index into GetContainedItems().
            ulong itemGuid = 0;
            if (bagId == 0xFF)
            {
                var items = GetContainedItems().ToList();
                if (slotId < items.Count)
                    itemGuid = items[slotId].Guid;
            }
            else
            {
                var item = GetContainedItem(bagId, slotId);
                itemGuid = item?.Guid ?? 0;
            }

            if (itemGuid == 0)
            {
                Serilog.Log.Warning("[SellItemToVendor] Could not resolve item GUID for bag={BagId} slot={SlotId}", bagId, slotId);
                return;
            }

            await factory.VendorAgent.SellItemByGuidAsync(vendorGuid, itemGuid, (byte)Math.Min(quantity, 255), ct);
        }


        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;
            await factory.VendorAgent.QuickRepairAllAsync(vendorGuid, ct);
        }


        public async Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory != null)
            {
                await factory.MailAgent.QuickCollectAllMailAsync(mailboxGuid, ct);
            }
        }


        public async Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var bank = factory.BankAgent;
            await bank.OpenBankAsync(bankerGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (bank.IsBankWindowOpen) break;
                await Task.Delay(100, ct);
            }

            if (!bank.IsBankWindowOpen) return;

            int deposited = 0;
            for (byte bag = 0; bag < 5 && deposited < 10; bag++)
            {
                byte maxSlots = bag == 0 ? (byte)16 : (byte)20;
                for (byte slot = 0; slot < maxSlots && deposited < 10; slot++)
                {
                    var item = GetContainedItem(bag, slot);
                    if (item == null) continue;

                    // Keep consumables, quest items, reagents, keys, ammo
                    var info = item.Info;
                    if (info != null && (info.ItemClass == GameData.Core.Enums.ItemClass.Consumable
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Quest
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Reagent
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Key
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Lockpick
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Arrow
                        || info.ItemClass == GameData.Core.Enums.ItemClass.Bullet))
                        continue;

                    try
                    {
                        await bank.DepositItemAsync(bag, slot, 0, ct);
                        deposited++;
                        await Task.Delay(200, ct);
                    }
                    catch { }
                }
            }

            await bank.CloseBankAsync(ct);
        }


        public async Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var ah = factory.AuctionHouseAgent;
            await ah.OpenAuctionHouseAsync(auctioneerGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (ah.IsAuctionHouseOpen) break;
                await Task.Delay(100, ct);
            }

            if (!ah.IsAuctionHouseOpen) return;

            var items = GetContainedItems()
                .Where(item => item.Quality >= GameData.Core.Enums.ItemQuality.Uncommon)
                .Take(5)
                .ToList();

            foreach (var item in items)
            {
                uint basePrice = item.Quality switch
                {
                    GameData.Core.Enums.ItemQuality.Uncommon => 5000u,
                    GameData.Core.Enums.ItemQuality.Rare => 50000u,
                    GameData.Core.Enums.ItemQuality.Epic => 500000u,
                    _ => 5000u,
                };
                int reqLevel = item.Info?.RequiredLevel ?? 1;
                uint startBid = (uint)(basePrice * (1f + reqLevel / 10f));
                uint buyout = (uint)(startBid * 1.5f);

                try
                {
                    await ah.PostAuctionAsync(item.Guid, startBid, buyout,
                        AuctionDuration.TwentyFourHours, ct);
                    await Task.Delay(300, ct);
                }
                catch { }
            }

            try { await ah.CloseAuctionHouseAsync(ct); } catch { }
        }
    }
}
