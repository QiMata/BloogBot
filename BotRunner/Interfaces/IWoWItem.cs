﻿using BotRunner.Constants;

namespace BotRunner.Interfaces
{
    public interface IWoWItem : IWoWObject
    {
        uint ItemId { get; }
        uint Quantity { get; }
        uint StackCount { get; }
        uint MaxDurability { get; }
        uint RequiredLevel { get; }
        uint Durability { get; }
        bool IsCoins { get; }
        ItemCacheInfo Info { get; }
        ItemDynFlags Flags { get; set; }
        ItemQuality Quality { get; }
        uint DurabilityPercentage => (uint)((double)Durability / MaxDurability * 100);
        void Use();
        void Loot();
    }

    enum InventoryResult
    {
        EQUIP_ERR_OK = 0,
        EQUIP_ERR_CANT_EQUIP_LEVEL_I = 1,       // ERR_CANT_EQUIP_LEVEL_I
        EQUIP_ERR_CANT_EQUIP_SKILL = 2,       // ERR_CANT_EQUIP_SKILL
        EQUIP_ERR_ITEM_DOESNT_GO_TO_SLOT = 3,       // ERR_WRONG_SLOT
        EQUIP_ERR_BAG_FULL = 4,       // ERR_BAG_FULL
        EQUIP_ERR_NONEMPTY_BAG_OVER_OTHER_BAG = 5,       // ERR_BAG_IN_BAG
        EQUIP_ERR_CANT_TRADE_EQUIP_BAGS = 6,       // ERR_TRADE_EQUIPPED_BAG
        EQUIP_ERR_ONLY_AMMO_CAN_GO_HERE = 7,       // ERR_AMMO_ONLY
        EQUIP_ERR_NO_REQUIRED_PROFICIENCY = 8,       // ERR_PROFICIENCY_NEEDED
        EQUIP_ERR_NO_EQUIPMENT_SLOT_AVAILABLE = 9,       // ERR_NO_SLOT_AVAILABLE
        EQUIP_ERR_YOU_CAN_NEVER_USE_THAT_ITEM = 10,      // ERR_CANT_EQUIP_EVER
        EQUIP_ERR_YOU_CAN_NEVER_USE_THAT_ITEM2 = 11,      // ERR_CANT_EQUIP_EVER
        EQUIP_ERR_NO_EQUIPMENT_SLOT_AVAILABLE2 = 12,      // ERR_NO_SLOT_AVAILABLE
        EQUIP_ERR_CANT_EQUIP_WITH_TWOHANDED = 13,      // ERR_2HANDED_EQUIPPED
        EQUIP_ERR_CANT_DUAL_WIELD = 14,      // ERR_2HSKILLNOTFOUND
        EQUIP_ERR_ITEM_DOESNT_GO_INTO_BAG = 15,      // ERR_WRONG_BAG_TYPE
        EQUIP_ERR_ITEM_DOESNT_GO_INTO_BAG2 = 16,      // ERR_WRONG_BAG_TYPE
        EQUIP_ERR_CANT_CARRY_MORE_OF_THIS = 17,      // ERR_ITEM_MAX_COUNT
        EQUIP_ERR_NO_EQUIPMENT_SLOT_AVAILABLE3 = 18,      // ERR_NO_SLOT_AVAILABLE
        EQUIP_ERR_ITEM_CANT_STACK = 19,      // ERR_CANT_STACK
        EQUIP_ERR_ITEM_CANT_BE_EQUIPPED = 20,      // ERR_NOT_EQUIPPABLE
        EQUIP_ERR_ITEMS_CANT_BE_SWAPPED = 21,      // ERR_CANT_SWAP
        EQUIP_ERR_SLOT_IS_EMPTY = 22,      // ERR_SLOT_EMPTY
        EQUIP_ERR_ITEM_NOT_FOUND = 23,      // ERR_ITEM_NOT_FOUND
        EQUIP_ERR_CANT_DROP_SOULBOUND = 24,      // ERR_DROP_BOUND_ITEM
        EQUIP_ERR_OUT_OF_RANGE = 25,      // ERR_OUT_OF_RANGE
        EQUIP_ERR_TRIED_TO_SPLIT_MORE_THAN_COUNT = 26,      // ERR_TOO_FEW_TO_SPLIT
        EQUIP_ERR_COULDNT_SPLIT_ITEMS = 27,      // ERR_SPLIT_FAILED
        EQUIP_ERR_MISSING_REAGENT = 28,      // ERR_SPELL_FAILED_REAGENTS_GENERIC
        EQUIP_ERR_NOT_ENOUGH_MONEY = 29,      // ERR_NOT_ENOUGH_MONEY
        EQUIP_ERR_NOT_A_BAG = 30,      // ERR_NOT_A_BAG
        EQUIP_ERR_CAN_ONLY_DO_WITH_EMPTY_BAGS = 31,      // ERR_DESTROY_NONEMPTY_BAG
        EQUIP_ERR_DONT_OWN_THAT_ITEM = 32,      // ERR_NOT_OWNER
        EQUIP_ERR_CAN_EQUIP_ONLY1_QUIVER = 33,      // ERR_ONLY_ONE_QUIVER
        EQUIP_ERR_MUST_PURCHASE_THAT_BAG_SLOT = 34,      // ERR_NO_BANK_SLOT
        EQUIP_ERR_TOO_FAR_AWAY_FROM_BANK = 35,      // ERR_NO_BANK_HERE
        EQUIP_ERR_ITEM_LOCKED = 36,      // ERR_ITEM_LOCKED
        EQUIP_ERR_YOU_ARE_STUNNED = 37,      // ERR_GENERIC_STUNNED
        EQUIP_ERR_YOU_ARE_DEAD = 38,      // ERR_PLAYER_DEAD
        EQUIP_ERR_CANT_DO_RIGHT_NOW = 39,      // ERR_CLIENT_LOCKED_OUT
        EQUIP_ERR_INT_BAG_ERROR = 40,      // ERR_INTERNAL_BAG_ERROR
        EQUIP_ERR_CAN_EQUIP_ONLY1_BOLT = 41,      // ERR_ONLY_ONE_BOLT
        EQUIP_ERR_CAN_EQUIP_ONLY1_AMMOPOUCH = 42,      // ERR_ONLY_ONE_AMMO
        EQUIP_ERR_STACKABLE_CANT_BE_WRAPPED = 43,      // ERR_CANT_WRAP_STACKABLE
        EQUIP_ERR_EQUIPPED_CANT_BE_WRAPPED = 44,      // ERR_CANT_WRAP_EQUIPPED
        EQUIP_ERR_WRAPPED_CANT_BE_WRAPPED = 45,      // ERR_CANT_WRAP_WRAPPED
        EQUIP_ERR_BOUND_CANT_BE_WRAPPED = 46,      // ERR_CANT_WRAP_BOUND
        EQUIP_ERR_UNIQUE_CANT_BE_WRAPPED = 47,      // ERR_CANT_WRAP_UNIQUE
        EQUIP_ERR_BAGS_CANT_BE_WRAPPED = 48,      // ERR_CANT_WRAP_BAGS
        EQUIP_ERR_ALREADY_LOOTED = 49,      // ERR_LOOT_GONE
        EQUIP_ERR_INVENTORY_FULL = 50,      // ERR_INV_FULL
        EQUIP_ERR_BANK_FULL = 51,      // ERR_BAG_FULL
        EQUIP_ERR_ITEM_IS_CURRENTLY_SOLD_OUT = 52,      // ERR_VENDOR_SOLD_OUT
        EQUIP_ERR_BAG_FULL3 = 53,      // ERR_BAG_FULL
        EQUIP_ERR_ITEM_NOT_FOUND2 = 54,      // ERR_ITEM_NOT_FOUND
        EQUIP_ERR_ITEM_CANT_STACK2 = 55,      // ERR_CANT_STACK
        EQUIP_ERR_BAG_FULL4 = 56,      // ERR_BAG_FULL
        EQUIP_ERR_ITEM_SOLD_OUT = 57,      // ERR_VENDOR_SOLD_OUT
        EQUIP_ERR_OBJECT_IS_BUSY = 58,      // ERR_OBJECT_IS_BUSY
        EQUIP_ERR_NONE = 59,      // ERR_CANT_BE_DISENCHANTED
        EQUIP_ERR_NOT_IN_COMBAT = 60,      // ERR_NOT_IN_COMBAT
        EQUIP_ERR_NOT_WHILE_DISARMED = 61,      // ERR_NOT_WHILE_DISARMED
        EQUIP_ERR_BAG_FULL6 = 62,      // ERR_BAG_FULL
        EQUIP_ERR_CANT_EQUIP_RANK = 63,      // ERR_CANT_EQUIP_RANK
        EQUIP_ERR_CANT_EQUIP_REPUTATION = 64,      // ERR_CANT_EQUIP_REPUTATION
        EQUIP_ERR_TOO_MANY_SPECIAL_BAGS = 65,      // ERR_TOO_MANY_SPECIAL_BAGS
        EQUIP_ERR_LOOT_CANT_LOOT_THAT_NOW = 66,      // ERR_LOOT_CANT_LOOT_THAT_NOW
                                                     // any greater values show as "bag full"
    }

    enum BuyResult
    {
        BUY_ERR_CANT_FIND_ITEM = 0,
        BUY_ERR_ITEM_ALREADY_SOLD = 1,
        BUY_ERR_NOT_ENOUGHT_MONEY = 2,
        BUY_ERR_SELLER_DONT_LIKE_YOU = 4,
        BUY_ERR_DISTANCE_TOO_FAR = 5,
        BUY_ERR_ITEM_SOLD_OUT = 7,
        BUY_ERR_CANT_CARRY_MORE = 8,
        BUY_ERR_RANK_REQUIRE = 11,
        BUY_ERR_REPUTATION_REQUIRE = 12
    }

    enum SellResult
    {
        SELL_ERR_CANT_FIND_ITEM = 1,
        SELL_ERR_CANT_SELL_ITEM = 2,       // merchant doesn't like that item
        SELL_ERR_CANT_FIND_VENDOR = 3,       // merchant doesn't like you
        SELL_ERR_YOU_DONT_OWN_THAT_ITEM = 4,       // you don't own that item
        SELL_ERR_UNK = 5,       // nothing appears...
        SELL_ERR_ONLY_EMPTY_BAG = 6        // can only do with empty bags
    }

    // -1 from client enchantment slot number
    enum EnchantmentSlot
    {
        PERM_ENCHANTMENT_SLOT = 0,
        TEMP_ENCHANTMENT_SLOT = 1,
        MAX_INSPECTED_ENCHANTMENT_SLOT = 2,

        PROP_ENCHANTMENT_SLOT_0 = 3,                        // used with RandomSuffix
        PROP_ENCHANTMENT_SLOT_1 = 4,                        // used with RandomSuffix
        PROP_ENCHANTMENT_SLOT_2 = 5,                        // used with RandomSuffix
        PROP_ENCHANTMENT_SLOT_3 = 6,
        MAX_ENCHANTMENT_SLOT = 7
    }


    enum EnchantmentOffset
    {
        ENCHANTMENT_ID_OFFSET = 0,
        ENCHANTMENT_DURATION_OFFSET = 1,
        ENCHANTMENT_CHARGES_OFFSET = 2                         // now here not only charges, but something new in wotlk
    }

    [Flags]
    enum EnchantmentSlotMask
    {
        ENCHANTMENT_CAN_SOULBOUND = 0x01,
        ENCHANTMENT_UNK1 = 0x02,
        ENCHANTMENT_UNK2 = 0x04,
        ENCHANTMENT_UNK3 = 0x08
    }

    enum ItemUpdateState
    {
        ITEM_UNCHANGED = 0,
        ITEM_CHANGED = 1,
        ITEM_NEW = 2,
        ITEM_REMOVED = 3
    }

    enum ItemLootUpdateState
    {
        ITEM_LOOT_NONE = 0,      // loot not generated
        ITEM_LOOT_TEMPORARY = 1,      // generated loot is temporary (will deleted at loot window close)
        ITEM_LOOT_UNCHANGED = 2,
        ITEM_LOOT_CHANGED = 3,
        ITEM_LOOT_NEW = 4,
        ITEM_LOOT_REMOVED = 5
    }

    // masks for ITEM_FIELD_FLAGS field
    [Flags]
    public enum ItemDynFlags
    {
        ITEM_DYNFLAG_BINDED = 0x00000001, // set in game at binding
        ITEM_DYNFLAG_UNK1 = 0x00000002,
        ITEM_DYNFLAG_UNLOCKED = 0x00000004, // have meaning only for item with proto->LockId, if not set show as "Locked, req. lockpicking N"
        ITEM_DYNFLAG_WRAPPED = 0x00000008, // mark item as wrapped into wrapper container
        ITEM_DYNFLAG_UNK4 = 0x00000010, // can't repeat old note: appears red icon (like when item durability==0)
        ITEM_DYNFLAG_UNK5 = 0x00000020,
        ITEM_DYNFLAG_UNK6 = 0x00000040, // ? old note: usable
        ITEM_DYNFLAG_UNK7 = 0x00000080,
        ITEM_DYNFLAG_UNK8 = 0x00000100,
        ITEM_DYNFLAG_READABLE = 0x00000200, // can be open for read, it or item proto pagetText make show "Right click to read"
        ITEM_DYNFLAG_UNK10 = 0x00000400,
        ITEM_DYNFLAG_UNK11 = 0x00000800,
        ITEM_DYNFLAG_UNK12 = 0x00001000,
        ITEM_DYNFLAG_UNK13 = 0x00002000,
        ITEM_DYNFLAG_UNK14 = 0x00004000,
        ITEM_DYNFLAG_UNK15 = 0x00008000,
        ITEM_DYNFLAG_UNK16 = 0x00010000,
        ITEM_DYNFLAG_UNK17 = 0x00020000,
    }

    enum ItemRequiredTargetType
    {
        ITEM_TARGET_TYPE_CREATURE = 1,
        ITEM_TARGET_TYPE_DEAD = 2
    }
}
