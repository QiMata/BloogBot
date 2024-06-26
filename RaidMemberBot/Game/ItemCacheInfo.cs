﻿using RaidMemberBot.Mem;
using System;
using System.Collections.Generic;
using System.Linq;
using static RaidMemberBot.Constants.Enums;

namespace RaidMemberBot.Game
{
    public class ItemCacheInfo
    {
        static readonly IList<ItemSubclass[]> ItemSubclasses = new List<ItemSubclass[]>
        {
            // these are untested for Vanilla
            new []
            {
                ItemSubclass.Consumable,
                ItemSubclass.Potion,
                ItemSubclass.Elixir,
                ItemSubclass.Flask,
                ItemSubclass.Scroll,
                ItemSubclass.FoodAndDrink,
                ItemSubclass.ItemEnhancement,
                ItemSubclass.Bandage,
                ItemSubclass.Other
            },
            new []
            {
                ItemSubclass.Container,
                ItemSubclass.SoulBag,
                ItemSubclass.HerbBag,
                ItemSubclass.EnchantingBag,
                ItemSubclass.EngineeringBag,
                ItemSubclass.GemBag,
                ItemSubclass.MiningBag,
                ItemSubclass.LeatherworkingBag,
                ItemSubclass.InscriptionBag
            },
            new []
            {
                ItemSubclass.OneHandedAxe,
                ItemSubclass.TwoHandedAxe,
                ItemSubclass.Bow,
                ItemSubclass.Gun,
                ItemSubclass.OneHandedMace,
                ItemSubclass.TwoHandedMace,
                ItemSubclass.Polearm,
                ItemSubclass.OneHandedSword,
                ItemSubclass.TwoHandedSword,
                ItemSubclass.Obsolete,
                ItemSubclass.Staff,
                ItemSubclass.OneHandedExotic,
                ItemSubclass.TwoHandedExotic,
                ItemSubclass.FistWeapon,
                ItemSubclass.MiscellaneousWeapon,
                ItemSubclass.Dagger,
                ItemSubclass.Thrown,
                ItemSubclass.Spear,
                ItemSubclass.Crossbow,
                ItemSubclass.Wand,
                ItemSubclass.FishingPole
            },
            new []
            {
                ItemSubclass.RedJewel,
                ItemSubclass.BlueJewel,
                ItemSubclass.YellowJewel,
                ItemSubclass.PurpleJewel,
                ItemSubclass.GreenJewel,
                ItemSubclass.OrangeJewel,
                ItemSubclass.MetaJewel,
                ItemSubclass.SimpleJewel,
                ItemSubclass.PrismaticJewel
            },
            new []
            {
                ItemSubclass.MiscellaneousArmor,
                ItemSubclass.Cloth,
                ItemSubclass.Leather,
                ItemSubclass.Mail,
                ItemSubclass.Plate,
                ItemSubclass.BucklerOBSOLETE,
                ItemSubclass.Shield,
                ItemSubclass.Libram,
                ItemSubclass.Idol,
                ItemSubclass.Totem,
                ItemSubclass.Sigil
            },
            new []
            {
                ItemSubclass.Reagent
            },
            new []
            {
                ItemSubclass.WandOBSOLETE,
                ItemSubclass.BoltOBSOLETE,
                ItemSubclass.Arrow,
                ItemSubclass.Bullet,
                ItemSubclass.ThrownOBSOLETE
            },
            new []
            {
                ItemSubclass.TradeGood,
                ItemSubclass.Parts,
                ItemSubclass.Explosives,
                ItemSubclass.Devices,
                ItemSubclass.CraftingJewelcrafting,
                ItemSubclass.CraftingCloth,
                ItemSubclass.CraftingLeather,
                ItemSubclass.CraftingMetalAndStone,
                ItemSubclass.CraftingMeat,
                ItemSubclass.CraftingHerb,
                ItemSubclass.CraftingElemental,
                ItemSubclass.CraftingOther,
                ItemSubclass.CraftingEnchanting,
                ItemSubclass.CraftingMaterials,
                ItemSubclass.CraftingArmorEnchantment,
                ItemSubclass.CraftingWeaponEnchantment
            },
            new []
            {
                ItemSubclass.GenericOBSOLETE
            },
            new []
            {
                ItemSubclass.RecipeBook,
                ItemSubclass.RecipeLeatherworking,
                ItemSubclass.RecipeTailoring,
                ItemSubclass.RecipeEngineering,
                ItemSubclass.RecipeBlacksmithing,
                ItemSubclass.RecipeCooking,
                ItemSubclass.RecipeAlchemy,
                ItemSubclass.RecipeFirstAid,
                ItemSubclass.RecipeEnchanting,
                ItemSubclass.RecipeFishing,
                ItemSubclass.RecipeJewelcrafting
            },
            new []
            {
                ItemSubclass.MoneyOBSOLETE
            },
            new []
            {
                ItemSubclass.Quiver1OBSOLETE,
                ItemSubclass.Quiver2OBSOLETE,
                ItemSubclass.Quiver,
                ItemSubclass.AmmoPouch
            },
            new []
            {
                ItemSubclass.Quest
            },
            new []
            {
                ItemSubclass.Key,
                ItemSubclass.Lockpick
            },
            new []
            {
                ItemSubclass.Permanent
            },
            new []
            {
                ItemSubclass.MiscJunk,
                ItemSubclass.MiscReagent,
                ItemSubclass.MiscPet,
                ItemSubclass.MiscHoliday,
                ItemSubclass.MiscOther,
                ItemSubclass.MiscMount
            }
        };

        readonly ItemCacheEntry itemCacheEntry;

        internal ItemCacheInfo(ItemCacheEntry itemCacheEntry)
        {
            this.itemCacheEntry = itemCacheEntry;
        }

        public ItemClass ItemClass => itemCacheEntry.ItemClass;

        public ItemSubclass ItemSubclass => ItemSubclasses.ElementAt((int)ItemClass)[itemCacheEntry.ItemSubclassID];

        public ItemQuality Quality => itemCacheEntry.ItemQuality;

        public EquipSlot EquipSlot => itemCacheEntry.EquipSlot;

        public int RequiredLevel => itemCacheEntry.RequiredLevel;

        public int MaxDurability => itemCacheEntry.MaxDurability;

        public string Name => MemoryManager.ReadString(itemCacheEntry.NamePtr);
    }

    public class ItemCacheEntry
    {
        readonly IntPtr baseAddress;

        internal ItemCacheEntry(IntPtr baseAddress)
        {
            this.baseAddress = baseAddress;
        }

        internal ItemClass ItemClass => (ItemClass)MemoryManager.ReadByte(baseAddress + 0x0);

        internal int ItemSubclassID => MemoryManager.ReadInt(baseAddress + 0x4);

        internal ItemQuality ItemQuality => (ItemQuality)MemoryManager.ReadByte(baseAddress + 0x1C);

        internal EquipSlot EquipSlot => (EquipSlot)MemoryManager.ReadByte(baseAddress + 0x2C);

        internal int RequiredLevel => MemoryManager.ReadInt(baseAddress + 0x3C);

        internal int MaxDurability => MemoryManager.ReadInt(baseAddress + 0x1C4);

        internal IntPtr NamePtr => MemoryManager.ReadIntPtr(baseAddress + 0x8);
    }
}
