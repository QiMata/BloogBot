using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Utils;

namespace WoWSharpClient
{
    /// <summary>
    /// Handles item use, equip/unequip, loot, vendor, banking, auction, and trade operations.
    /// Extracted from WoWSharpObjectManager.Inventory.cs to reduce partial-class sprawl.
    /// All public IObjectManager methods remain on WoWSharpObjectManager; this class
    /// provides the implementation and the partial class delegates to it.
    /// </summary>
    internal sealed class InventoryManager
    {
        // vMangos battleground bots cast SPELL_CAPTURE_BANNER (21651) directly on
        // AB/AV banner game objects. Raw GAMEOBJ_USE only flips the goober/button
        // state; the actual capture path is a player -> GO open-lock spell cast.
        private const int BattlegroundBannerCaptureSpellId = 21651;
        private static readonly HashSet<uint> BattlegroundBannerEntries =
        [
            178364u, // AV Horde Banner 1
            178943u, // AV Horde Banner 2
            178365u, // AV Alliance Banner 1
            178925u, // AV Alliance Banner 2
            178940u, // AV Contested Banner 1
            179286u, // AV Contested Banner 2
            179287u, // AV Contested Banner 3
            179435u, // AV Contested Banner 4
            180418u, // AV Snowfall Banner
            180058u, // AB Alliance Banner
            180059u, // AB Contested Banner 1
            180060u, // AB Horde Banner
            180061u, // AB Contested Banner 2
            180087u, // AB Stable Banner
            180088u, // AB Blacksmith Banner
            180089u, // AB Farm Banner
            180090u, // AB Lumber Mill Banner
            180091u  // AB Gold Mine Banner
        ];

        private readonly WoWSharpObjectManager _om;

        // Two-phase cursor emulation: PickupContainedItem stores source, PlaceItemInContainer sends CMSG_SWAP_ITEM
        private (byte Bag, byte Slot, int Quantity)? _cursorItem;

        public InventoryManager(WoWSharpObjectManager objectManager)
        {
            _om = objectManager;
        }

        // ---- Accessors for internal wiring ----

        private WoWClient WoWClient => _om.WoWClientInternal;
        private IWoWLocalPlayer Player => _om.Player;
        private Func<IAgentFactory> AgentFactory => _om.AgentFactoryAccessor;

        // ---- Item Use ----

        public void UseItem(int bagId, int slotId, ulong targetGuid = 0)
        {
            if (WoWClient == null) return;
            byte srcBag = bagId == 0 ? (byte)0xFF : (byte)(18 + bagId);
            byte srcSlot = bagId == 0 ? (byte)(23 + slotId) : (byte)slotId;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(srcBag);
            w.Write(srcSlot);
            w.Write((byte)0); // spellSlot
            if (targetGuid == 0)
            {
                w.Write((ushort)0x0000); // TARGET_FLAG_SELF
            }
            else if (IsEquippedItemGuid(targetGuid))
            {
                w.Write((ushort)0x0010); // TARGET_FLAG_ITEM
                ReaderUtils.WritePackedGuid(w, targetGuid);
            }
            else
            {
                w.Write((ushort)0x0002); // TARGET_FLAG_UNIT
                ReaderUtils.WritePackedGuid(w, targetGuid);
            }
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_USE_ITEM, ms.ToArray());
        }

        // ---- GUID lookups ----

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
            int internalSlot = (int)slot - 1;
            if (internalSlot < 0) return 0;
            int index = internalSlot * 2;
            if (index + 1 >= player.Inventory.Length) return 0;
            return ((ulong)player.Inventory[index + 1] << 32) | player.Inventory[index];
        }

        public IWoWItem GetEquippedItem(EquipSlot slot)
        {
            var player = Player as WoWPlayer;
            if (player == null) return null;

            int slotIndex = (int)slot - 1;
            if (slotIndex >= 0 && slotIndex < player.VisibleItems.Length)
            {
                var visible = player.VisibleItems[slotIndex];
                if (visible?.ItemId > 0) return visible;
            }

            var guid = GetEquippedItemGuid(slot);
            return _om.FindItemByGuidInternal(guid);
        }

        public IWoWItem GetContainedItem(int bagSlot, int slotId)
        {
            ulong itemGuid;
            if (bagSlot == 0)
            {
                itemGuid = GetBackpackItemGuid(slotId);
            }
            else
            {
                var bagEquipSlot = (EquipSlot)(19 + bagSlot);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) return null;

                var container = _om.GetContainerByGuid(bagGuid);
                if (container == null) return null;

                itemGuid = container.GetItemGuid(slotId);
            }

            return _om.FindItemByGuidInternal(itemGuid);
        }

        public IEnumerable<IWoWItem> GetEquippedItems()
        {
            var player = Player as WoWPlayer;
            if (player == null) return [];

            var items = new List<IWoWItem>();
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

                var container = _om.GetContainerByGuid(bagGuid);
                if (container == null) continue;

                for (int slot = 0; slot < container.NumOfSlots; slot++)
                {
                    var item = _om.FindItemByGuidInternal(container.GetItemGuid(slot));
                    if (item != null && item.ItemId > 0) items.Add(item);
                }
            }

            return items;
        }

        public int CountFreeSlots(bool countSpecialSlots = false)
        {
            int freeSlots = 0;
            for (int i = 0; i < 16; i++)
                if (GetContainedItem(0, i) == null) freeSlots++;
            for (int bag = 1; bag <= 4; bag++)
            {
                var bagEquipSlot = (EquipSlot)(19 + bag);
                var bagGuid = GetEquippedItemGuid(bagEquipSlot);
                if (bagGuid == 0) continue;
                var container = _om.GetContainerByGuid(bagGuid);
                if (container == null) continue;
                for (int slot = 0; slot < container.NumOfSlots; slot++)
                    if (_om.FindItemByGuidInternal(container.GetItemGuid(slot)) == null) freeSlots++;
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
            var guid = GetEquippedItemGuid(equipSlot);
            return (uint)(guid & 0xFFFFFFFF);
        }

        private bool IsEquippedItemGuid(ulong guid)
        {
            if (guid == 0) return false;
            for (var slot = EquipSlot.Head; slot <= EquipSlot.Ranged; slot++)
            {
                if (GetEquippedItemGuid(slot) == guid)
                    return true;
            }
            return false;
        }

        // ---- Cursor / Move / Destroy ----

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

            var factory = AgentFactory?.Invoke();
            if (factory?.InventoryAgent != null)
                _ = factory.InventoryAgent.MoveItemAsync(src.Bag, src.Slot, dstBag, dstSlot);
        }

        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1)
        {
            if (WoWClient == null) { Log.Warning("[DestroyItem] _woWClient is null, cannot send packet"); return; }
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            byte count = quantity < 0 ? (byte)0xFF : (byte)Math.Min(quantity, 255);
            Log.Information("[DestroyItem] Sending CMSG_DESTROYITEM: bag=0x{Bag:X2}, slot={Slot}, count={Count}",
                srcBag, srcSlot, count);
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_DESTROYITEM, [srcBag, srcSlot, count, 0, 0, 0]);
        }

        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot)
        {
            byte srcBag = bag == 0 ? (byte)0xFF : (byte)(18 + bag);
            byte srcSlot = bag == 0 ? (byte)(23 + slot) : (byte)slot;
            byte dstBag = destinationBag == 0 ? (byte)0xFF : (byte)(18 + destinationBag);
            byte dstSlot = destinationBag == 0 ? (byte)(23 + destinationSlot) : (byte)destinationSlot;

            var factory = AgentFactory?.Invoke();
            if (factory?.InventoryAgent != null)
                _ = factory.InventoryAgent.SplitItemAsync(srcBag, srcSlot, dstBag, dstSlot, (uint)quantity);
        }

        // ---- Cursor operations (bridged from main class) ----

        /// <summary>Try to consume the cursor item for deletion. Returns the source bag/slot/quantity or null.</summary>
        internal (byte Bag, byte Slot, int Quantity)? TryConsumeCursorItem()
        {
            if (_cursorItem == null) return null;
            var src = _cursorItem.Value;
            _cursorItem = null;
            return src;
        }

        /// <summary>Try to consume the cursor item for equipping. Returns the source bag/slot or null.</summary>
        internal (byte Bag, byte Slot)? TryConsumeCursorItemForEquip()
        {
            if (_cursorItem == null) return null;
            var result = (_cursorItem.Value.Bag, _cursorItem.Value.Slot);
            _cursorItem = null;
            return result;
        }

        // ---- Equip / Unequip ----

        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null)
        {
            if (WoWClient == null) return;
            byte srcBag = bagSlot == 0 ? (byte)0xFF : (byte)(18 + bagSlot);
            byte srcSlot = bagSlot == 0 ? (byte)(23 + slotId) : (byte)slotId;
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOEQUIP_ITEM, [srcBag, srcSlot]);
        }

        public void UnequipItem(EquipSlot slot)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            var equipmentSlot = (EquipmentSlot)((int)slot - 1);
            factory.EquipmentAgent.UnequipItemAsync(equipmentSlot, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        // ---- Game Object Interaction ----

        public void InteractWithGameObject(ulong guid)
        {
            if (WoWClient == null) return;

            var player = Player;
            if (player != null)
            {
                var node = _om.Objects.FirstOrDefault(o => o.Guid == guid);
                var nodeDist = node != null ? player.Position.DistanceTo(node.Position) : -1f;
                Log.Information("[GAMEOBJ_USE] Player pos=({X:F1},{Y:F1},{Z:F1}) flags=0x{Flags:X} nodeDist={Dist:F1}",
                    player.Position.X, player.Position.Y, player.Position.Z,
                    (uint)((WoWLocalPlayer)player).MovementFlags, nodeDist);
            }

            Log.Information("[GAMEOBJ_USE] _isBeingTeleported={Tp} _isInControl={Ctrl}",
                _om.IsInMapTransition, _om.IsInControlInternal);

            var payload = BitConverter.GetBytes(guid);
            Log.Information("[GAMEOBJ_USE] Sending CMSG_GAMEOBJ_USE for GUID=0x{Guid:X} (8 bytes: {Hex})",
                guid, BitConverter.ToString(payload));
            WoWClient.SendMSGPackedAsync(Opcode.CMSG_GAMEOBJ_USE, payload).GetAwaiter().GetResult();

            if (TryGetBattlegroundBannerEntry(guid, out var bannerEntry))
            {
                Log.Information(
                    "[GAMEOBJ_USE] Banner entry {Entry} requires capture spell {SpellId}; sending CMSG_CAST_SPELL on GUID=0x{Guid:X}",
                    bannerEntry,
                    BattlegroundBannerCaptureSpellId,
                    guid);
                _om.CastSpellOnGameObject(BattlegroundBannerCaptureSpellId, guid);
            }

            // Temporary packet sniffer: log all opcodes received for 5 seconds after GAMEOBJ_USE
            _om.SniffingGameObjUse = true;
            _om.SniffStartTime = DateTime.UtcNow;
            Task.Delay(5000).ContinueWith(_ =>
            {
                _om.SniffingGameObjUse = false;
                Log.Information("[GAMEOBJ_USE] Packet sniffer ended — no more logging");
            }, TaskScheduler.Default);
        }

        private bool TryGetBattlegroundBannerEntry(ulong guid, out uint entry)
        {
            entry = 0;

            if (_om.Objects.FirstOrDefault(o => o.Guid == guid) is not WoWGameObject gameObject)
                return false;

            if (!BattlegroundBannerEntries.Contains(gameObject.Entry))
                return false;

            entry = gameObject.Entry;
            return true;
        }

        // ---- Loot ----

        public void AutoStoreLootItem(byte slot)
        {
            if (WoWClient == null) return;
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_AUTOSTORE_LOOT_ITEM, new byte[] { slot });
        }

        public void ReleaseLoot(ulong lootGuid)
        {
            if (WoWClient == null) return;
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT_RELEASE, BitConverter.GetBytes(lootGuid));
        }

        public async Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory != null)
            {
                await factory.LootingAgent.QuickLootAsync(targetGuid, ct);
            }
            else if (WoWClient != null)
            {
                await WoWClient.SendMSGPackedAsync(Opcode.CMSG_LOOT, BitConverter.GetBytes(targetGuid));
            }
        }

        // ---- Vendor ----

        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory != null)
            {
                await factory.VendorAgent.QuickVendorVisitAsync(vendorGuid, itemsToBuy, cancellationToken: ct);
            }
        }

        public async Task BuyItemFromVendorAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;

            await factory.VendorAgent.RequestVendorInventoryAsync(vendorGuid, ct);
            await factory.VendorAgent.WaitForVendorWindowAsync(ct);

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
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;

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
                Log.Warning("[SellItemToVendor] Could not resolve item GUID for bag={BagId} slot={SlotId}", bagId, slotId);
                return;
            }

            await factory.VendorAgent.SellItemByGuidAsync(vendorGuid, itemGuid, (byte)Math.Min(quantity, 255), ct);
        }

        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            await factory.VendorAgent.QuickRepairAllAsync(vendorGuid, ct);
        }

        // ---- Mail ----

        public async Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory != null)
            {
                await factory.MailAgent.QuickCollectAllMailAsync(mailboxGuid, ct);
            }
        }

        // ---- Banking ----

        public async Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
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

                    var info = item.Info;
                    if (info != null && (info.ItemClass == ItemClass.Consumable
                        || info.ItemClass == ItemClass.Quest
                        || info.ItemClass == ItemClass.Reagent
                        || info.ItemClass == ItemClass.Key
                        || info.ItemClass == ItemClass.Lockpick
                        || info.ItemClass == ItemClass.Arrow
                        || info.ItemClass == ItemClass.Bullet))
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

        // ---- Auction ----

        public async Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
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
                .Where(item => item.Quality >= ItemQuality.Uncommon)
                .Take(5)
                .ToList();

            foreach (var item in items)
            {
                uint basePrice = item.Quality switch
                {
                    ItemQuality.Uncommon => 5000u,
                    ItemQuality.Rare => 50000u,
                    ItemQuality.Epic => 500000u,
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

        // ---- Trade ----

        public async Task InitiateTradeAsync(ulong playerGuid, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            await factory.TradeAgent.InitiateTradeAsync(playerGuid, ct);
        }

        public async Task SetTradeGoldAsync(uint copper, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            await factory.TradeAgent.OfferMoneyAsync(copper, ct);
        }

        public async Task SetTradeItemAsync(byte tradeSlot, byte bagId, byte slotId, CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;

            byte packetBag = bagId == 0 ? (byte)0xFF : (byte)(18 + bagId);
            byte packetSlot = bagId == 0 ? (byte)(23 + slotId) : slotId;
            await factory.TradeAgent.OfferItemAsync(tradeSlot, packetBag, packetSlot, ct);
        }

        public async Task AcceptTradeAsync(CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            await factory.TradeAgent.AcceptTradeAsync(ct);
        }

        public async Task CancelTradeAsync(CancellationToken ct = default)
        {
            var factory = AgentFactory?.Invoke();
            if (factory == null) return;
            await factory.TradeAgent.CancelTradeAsync(ct);
        }
    }
}
