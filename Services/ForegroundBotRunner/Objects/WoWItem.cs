using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;

namespace ForegroundBotRunner.Objects
{
    public class WoWItem : WoWObject, IWoWItem
    {
        internal WoWItem(
            nint pointer,
            HighGuid guid,
            WoWObjectType objectType)
            : base(pointer, guid, objectType)
        {
            var addr = Functions.GetItemCacheEntry((int)ItemId);
            if (addr != nint.Zero)
            {
                var itemCacheEntry = MemoryManager.ReadItemCacheEntry(addr);
                CacheInfo = new ItemCacheInfo(itemCacheEntry);
            }
        }

        public uint ItemId => (uint)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWItem_ItemIdOffset);

        public uint StackCount => (uint)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWItem_StackCountOffset);

        private readonly ItemCacheInfo CacheInfo;
        public ItemCacheInfo Info => CacheInfo;

        public void Use() => Functions.UseItem(Pointer);

        public void Loot() { }

        public ItemQuality Quality => CacheInfo?.Quality ?? ItemQuality.Common;

        public uint Durability => (uint)MemoryManager.ReadInt(GetDescriptorPtr() + MemoryAddresses.WoWItem_DurabilityOffset);

        public uint DurabilityPercentage => MaxDurability > 0 ? (uint)((double)Durability / MaxDurability * 100) : 100;

        public uint MaxDurability => CacheInfo != null ? (uint)CacheInfo.MaxDurability : 0;

        public uint RequiredLevel => CacheInfo != null ? (uint)CacheInfo.RequiredLevel : 1;

        public bool IsCoins => false;

        public ItemDynFlags ItemDynamicFlags { get; set; }

        public uint Quantity => StackCount;

        public uint Duration => 0;

        public uint[] SpellCharges { get; } = new uint[5];

        public uint[] Enchantments { get; } = new uint[21];

        public uint PropertySeed => 0;

        public uint RandomPropertiesId => 0;

        public uint ItemTextId => 0;

        public HighGuid Owner { get; } = new HighGuid(new byte[4], new byte[4]);

        public HighGuid Contained { get; } = new HighGuid(new byte[4], new byte[4]);

        public HighGuid CreatedBy { get; } = new HighGuid(new byte[4], new byte[4]);

        public HighGuid GiftCreator { get; } = new HighGuid(new byte[4], new byte[4]);
    }
}
