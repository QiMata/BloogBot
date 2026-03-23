using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ForegroundBotRunner.Statics
{
    public partial class ObjectManager
    {


        public static void PickupInventoryItem(int inventorySlot)
        {
            MainThreadLuaCall($"PickupInventoryItem({inventorySlot})");
        }


        public int GetItemCount(string parItemName)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k <= slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Info.Name == parItemName) totalCount += (int)item.StackCount;
                }
            }
            return totalCount;
        }



        public int GetItemCount(int itemId)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k <= slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.ItemId == itemId) totalCount += (int)item.StackCount;
                }
            }
            return totalCount;
        }



        public IList<IWoWItem> GetAllItems()
        {
            var items = new List<IWoWItem>();
            for (int bag = 0; bag < 5; bag++)
            {
                var container = GetExtraBag(bag - 1);
                if (bag != 0 && container == null)
                {
                    continue;
                }

                for (int slot = 0; slot < (bag == 0 ? 16 : container.NumOfSlots); slot++)
                {
                    var item = GetItem(bag, slot);
                    if (item == null)
                    {
                        continue;
                    }

                    items.Add(item);
                }
            }

            return items;
        }



        public int CountFreeSlots(bool parCountSpecialSlots)
        {
            var freeSlots = 0;
            for (var i = 0; i < 16; i++)
            {
                var tmpSlotGuid = GetBackpackItemGuid(i);
                if (tmpSlotGuid == 0) freeSlots++;
            }
            var bagGuids = new List<ulong>();
            for (var i = 0; i < 4; i++)
                bagGuids.Add(MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, i * 8)));

            var tmpItems = Containers
                .Where(i => i.NumOfSlots != 0 && bagGuids.Contains(i.Guid)).ToList();

            foreach (var bag in tmpItems)
            {
                if ((bag.Info.Name.Contains("Quiver") || bag.Info.Name.Contains("Ammo") || bag.Info.Name.Contains("Shot") ||
                     bag.Info.Name.Contains("Herb") || bag.Info.Name.Contains("Soul")) && !parCountSpecialSlots) continue;

                for (var i = 1; i < bag.NumOfSlots; i++)
                {
                    var tmpSlotGuid = bag.GetItemGuid(i);
                    if (tmpSlotGuid == 0) freeSlots++;
                }
            }
            return freeSlots;
        }



        public static int EmptyBagSlots
        {
            get
            {
                var bagGuids = new List<ulong>();
                for (var i = 0; i < 4; i++)
                    bagGuids.Add(MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, i * 8)));

                return bagGuids.Count(b => b == 0);
            }
        }

        ILoginScreen IObjectManager.LoginScreen => _fgLoginScreen;
        IRealmSelectScreen IObjectManager.RealmSelectScreen => _fgRealmSelectScreen;
        ICharacterSelectScreen IObjectManager.CharacterSelectScreen => _fgCharacterSelectScreen;
        IGossipFrame IObjectManager.GossipFrame => _fgGossipFrame;
        ILootFrame IObjectManager.LootFrame => _fgLootFrame;
        IMerchantFrame IObjectManager.MerchantFrame => _fgMerchantFrame;
        ICraftFrame IObjectManager.CraftFrame => _fgCraftFrame;
        IQuestFrame IObjectManager.QuestFrame => _fgQuestFrame;
        IQuestGreetingFrame IObjectManager.QuestGreetingFrame => null;
        ITaxiFrame IObjectManager.TaxiFrame => _fgTaxiFrame;
        ITradeFrame IObjectManager.TradeFrame => null;
        ITrainerFrame IObjectManager.TrainerFrame => _fgTrainerFrame;
        ITalentFrame IObjectManager.TalentFrame => _fgTalentFrame;



        public uint GetBagId(ulong itemGuid)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k < slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Guid == itemGuid) return (uint)i;
                }
            }
            return (uint)totalCount;
        }



        public uint GetSlotId(ulong itemGuid)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k < slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Guid == itemGuid) return (uint)k + 1;
                }
            }
            return (uint)totalCount;
        }



        public IWoWItem GetEquippedItem(EquipSlot slot)
        {
            var guid = GetEquippedItemGuid(slot);
            if (guid == 0) return null;
            return Items.FirstOrDefault(i => i.Guid == guid);
        }


        public IEnumerable<IWoWItem> GetEquippedItems()
        {
            IWoWItem headItem = GetEquippedItem(EquipSlot.Head);
            IWoWItem neckItem = GetEquippedItem(EquipSlot.Neck);
            IWoWItem shoulderItem = GetEquippedItem(EquipSlot.Shoulders);
            IWoWItem backItem = GetEquippedItem(EquipSlot.Back);
            IWoWItem chestItem = GetEquippedItem(EquipSlot.Chest);
            IWoWItem shirtItem = GetEquippedItem(EquipSlot.Shirt);
            IWoWItem tabardItem = GetEquippedItem(EquipSlot.Tabard);
            IWoWItem wristItem = GetEquippedItem(EquipSlot.Wrist);
            IWoWItem handsItem = GetEquippedItem(EquipSlot.Hands);
            IWoWItem waistItem = GetEquippedItem(EquipSlot.Waist);
            IWoWItem legsItem = GetEquippedItem(EquipSlot.Legs);
            IWoWItem feetItem = GetEquippedItem(EquipSlot.Feet);
            IWoWItem finger1Item = GetEquippedItem(EquipSlot.Finger1);
            IWoWItem finger2Item = GetEquippedItem(EquipSlot.Finger2);
            IWoWItem trinket1Item = GetEquippedItem(EquipSlot.Trinket1);
            IWoWItem trinket2Item = GetEquippedItem(EquipSlot.Trinket2);
            IWoWItem mainHandItem = GetEquippedItem(EquipSlot.MainHand);
            IWoWItem offHandItem = GetEquippedItem(EquipSlot.OffHand);
            IWoWItem rangedItem = GetEquippedItem(EquipSlot.Ranged);

            List<IWoWItem> list =
            [
                .. headItem != null ? new List<IWoWItem> { headItem } : [],
                .. neckItem != null ? new List<IWoWItem> { neckItem } : [],
                .. shoulderItem != null ? new List<IWoWItem> { shoulderItem } : [],
                .. backItem != null ? new List<IWoWItem> { backItem } : [],
                .. chestItem != null ? new List<IWoWItem> { chestItem } : [],
                .. shirtItem != null ? new List<IWoWItem> { shirtItem } : [],
                .. tabardItem != null ? new List<IWoWItem> { tabardItem } : [],
                .. wristItem != null ? new List<IWoWItem> { wristItem } : [],
                .. handsItem != null ? new List<IWoWItem> { handsItem } : [],
                .. waistItem != null ? new List<IWoWItem> { waistItem } : [],
                .. legsItem != null ? new List<IWoWItem> { legsItem } : [],
                .. feetItem != null ? new List<IWoWItem> { feetItem } : [],
                .. finger1Item != null ? new List<IWoWItem> { finger1Item } : [],
                .. finger2Item != null ? new List<IWoWItem> { finger2Item } : [],
                .. trinket1Item != null ? new List<IWoWItem> { trinket1Item } : [],
                .. trinket2Item != null ? new List<IWoWItem> { trinket2Item } : [],
                .. mainHandItem != null ? new List<IWoWItem> { mainHandItem } : [],
                .. offHandItem != null ? new List<IWoWItem> { offHandItem } : [],
                .. rangedItem != null ? new List<IWoWItem> { rangedItem } : [],
            ];
            return list;
        }



        private IWoWContainer GetExtraBag(int parSlot)
        {
            if (parSlot > 3 || parSlot < 0) return null;
            var bagGuid = MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, parSlot * 8));
            return bagGuid == 0 ? null : Containers.FirstOrDefault(i => i.Guid == bagGuid);
        }



        public IWoWItem GetItem(int parBag, int parSlot)
        {
            parBag += 1;
            switch (parBag)
            {
                case 1:
                    ulong itemGuid = 0;
                    if (parSlot < 16 && parSlot >= 0)
                        itemGuid = GetBackpackItemGuid(parSlot);
                    return itemGuid == 0 ? null : Items.FirstOrDefault(i => i.Guid == itemGuid);

                case 2:
                case 3:
                case 4:
                case 5:
                    var tmpBag = GetExtraBag(parBag - 2);
                    if (tmpBag == null) return null;
                    var tmpItemGuid = tmpBag.GetItemGuid(parSlot);
                    if (tmpItemGuid == 0) return null;
                    return Items.FirstOrDefault(i => i.Guid == tmpItemGuid);

                default:
                    return null;
            }
        }


        public ulong GetBackpackItemGuid(int slot) => MemoryManager.ReadUlong(((LocalPlayer)Player).GetDescriptorPtr() + (MemoryAddresses.LocalPlayer_BackpackFirstItemOffset + slot * 8));



        public ulong GetEquippedItemGuid(EquipSlot slot) => MemoryManager.ReadUlong(nint.Add(((LocalPlayer)Player).Pointer, MemoryAddresses.LocalPlayer_EquipmentFirstItemOffset + ((int)slot - 1) * 0x8));



        public uint GetItemCount(uint itemId) => (uint)GetItemCount((int)itemId);



        public void UseItem(int bagId, int slotId, ulong targetGuid = 0)
        {
            // WoW Lua bags are 0-based: 0=backpack, 1-4=extra bags
            // Slot is 1-based in Lua
            if (TryGetEquippedInventorySlot(targetGuid, out var inventorySlot))
            {
                MainThreadLuaCall($"UseContainerItem({bagId},{slotId + 1})");
                MainThreadLuaCall($"PickupInventoryItem({inventorySlot})");
                return;
            }

            MainThreadLuaCall($"UseContainerItem({bagId},{slotId + 1})");
        }



        public IWoWItem GetContainedItem(int bagSlot, int slotId)
        {
            return GetItem(bagSlot, slotId);
        }



        public IEnumerable<IWoWItem> GetContainedItems()
        {
            var items = new List<IWoWItem>();
            for (var bag = 0; bag < 5; bag++)
            {
                int slots;
                if (bag == 0)
                {
                    slots = 16;
                }
                else
                {
                    var extraBag = GetExtraBag(bag - 1);
                    if (extraBag == null) continue;
                    slots = extraBag.NumOfSlots;
                }
                for (var slot = 0; slot < slots; slot++)
                {
                    var item = GetItem(bag, slot);
                    if (item != null)
                        items.Add(item);
                }
            }
            return items;
        }



        public uint GetBagGuid(EquipSlot equipSlot)
        {
            return (uint)GetEquippedItemGuid(equipSlot);
        }



        public void PickupContainedItem(int bagSlot, int slotId, int quantity)
        {
            MainThreadLuaCall($"PickupContainerItem({bagSlot},{slotId + 1})");
            if (quantity > 0)
                MainThreadLuaCall($"SplitContainerItem({bagSlot},{slotId + 1},{quantity})");
        }



        public void PlaceItemInContainer(int bagSlot, int slotId)
        {
            MainThreadLuaCall($"PickupContainerItem({bagSlot},{slotId + 1})");
        }



        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1)
        {
            MainThreadLuaCall($"PickupContainerItem({bagSlot},{slotId + 1})");
            MainThreadLuaCall("DeleteCursorItem()");
        }



        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot)
        {
            MainThreadLuaCall($"SplitContainerItem({bag},{slot + 1},{quantity})");
            MainThreadLuaCall($"PickupContainerItem({destinationBag},{destinationSlot + 1})");
        }



        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null)
        {
            MainThreadLuaCall($"UseContainerItem({bagSlot},{slotId + 1})");
        }



        public void UnequipItem(EquipSlot slot)
        {
            PickupInventoryItem((int)slot);
            // Put item in first free bag slot
            MainThreadLuaCall("PutItemInBackpack()");
        }



        public void UseContainerItem(int bag, int slot)
        {
            MainThreadLuaCall($"UseContainerItem({bag},{slot})");
        }



        public void PickupContainerItem(uint bag, uint slot)
        {
            MainThreadLuaCall($"PickupContainerItem({bag},{slot})");
        }

        private bool TryGetEquippedInventorySlot(ulong targetGuid, out int inventorySlot)
        {
            inventorySlot = 0;
            if (targetGuid == 0)
                return false;

            for (var slot = EquipSlot.Head; slot <= EquipSlot.Ranged; slot++)
            {
                if (GetEquippedItemGuid(slot) != targetGuid)
                    continue;

                inventorySlot = (int)slot;
                return true;
            }

            return false;
        }
    }
}
