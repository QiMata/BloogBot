using GameData.Core.Enums;
using System;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents an item in the player's inventory.
    /// </summary>
    public class InventoryItem
    {
        /// <summary>
        /// Gets or sets the item GUID.
        /// </summary>
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bag where the item is located.
        /// </summary>
        public byte Bag { get; set; }

        /// <summary>
        /// Gets or sets the slot where the item is located.
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// Gets or sets the stack count.
        /// </summary>
        public uint Count { get; set; }

        /// <summary>
        /// Gets or sets the item quality.
        /// </summary>
        public ItemQuality Quality { get; set; }

        /// <summary>
        /// Gets or sets the item level.
        /// </summary>
        public uint ItemLevel { get; set; }

        /// <summary>
        /// Gets or sets the required level to use this item.
        /// </summary>
        public uint RequiredLevel { get; set; }

        /// <summary>
        /// Gets or sets the item charges (for consumables).
        /// </summary>
        public uint Charges { get; set; }

        /// <summary>
        /// Gets or sets the current durability.
        /// </summary>
        public uint CurrentDurability { get; set; }

        /// <summary>
        /// Gets or sets the maximum durability.
        /// </summary>
        public uint MaxDurability { get; set; }

        /// <summary>
        /// Gets or sets whether the item is bound.
        /// </summary>
        public bool IsBound { get; set; }

        /// <summary>
        /// Gets or sets whether the item is locked.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Gets or sets the item type.
        /// </summary>
        public uint ItemType { get; set; }

        /// <summary>
        /// Gets or sets the item subtype.
        /// </summary>
        public uint ItemSubType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the item was obtained.
        /// </summary>
        public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether the item is stackable.
        /// </summary>
        public bool IsStackable => Count > 1;

        /// <summary>
        /// Gets whether the item has durability.
        /// </summary>
        public bool HasDurability => MaxDurability > 0;

        /// <summary>
        /// Gets whether the item needs repair.
        /// </summary>
        public bool NeedsRepair => HasDurability && CurrentDurability < MaxDurability;

        /// <summary>
        /// Gets whether the item is broken.
        /// </summary>
        public bool IsBroken => HasDurability && CurrentDurability == 0;
    }

    /// <summary>
    /// Represents information about a bag in the inventory.
    /// </summary>
    public class BagInfo
    {
        /// <summary>
        /// Gets or sets the bag ID.
        /// </summary>
        public byte BagId { get; set; }

        /// <summary>
        /// Gets or sets the bag GUID.
        /// </summary>
        public ulong BagGuid { get; set; }

        /// <summary>
        /// Gets or sets the bag item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the bag name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of slots in the bag.
        /// </summary>
        public byte TotalSlots { get; set; }

        /// <summary>
        /// Gets or sets the number of free slots.
        /// </summary>
        public byte FreeSlots { get; set; }

        /// <summary>
        /// Gets or sets the bag type (normal, herb, enchanting, etc.).
        /// </summary>
        public BagType BagType { get; set; }

        /// <summary>
        /// Gets or sets whether the bag is equipped.
        /// </summary>
        public bool IsEquipped { get; set; }

        /// <summary>
        /// Gets the number of used slots.
        /// </summary>
        public byte UsedSlots => (byte)(TotalSlots - FreeSlots);

        /// <summary>
        /// Gets whether the bag is full.
        /// </summary>
        public bool IsFull => FreeSlots == 0;

        /// <summary>
        /// Gets whether the bag is empty.
        /// </summary>
        public bool IsEmpty => FreeSlots == TotalSlots;

        /// <summary>
        /// Gets the fill percentage.
        /// </summary>
        public float FillPercentage => TotalSlots > 0 ? (float)UsedSlots / TotalSlots * 100f : 0f;
    }

    /// <summary>
    /// Represents bag types.
    /// </summary>
    public enum BagType : uint
    {
        Normal = 0,
        Quiver = 1,
        AmmoPouch = 2,
        SoulBag = 4,
        LeatherworkingBag = 8,
        InscriptionBag = 16,
        HerbBag = 32,
        EnchantingBag = 64,
        EngineeringBag = 128,
        KeyRing = 256,
        GemBag = 512,
        MiningBag = 1024
    }

    // Inventory event models used by InventoryNetworkClientComponent reactive streams
    public readonly record struct ItemMovedData(
        ulong ItemGuid,
        byte SourceBag,
        byte SourceSlot,
        byte DestinationBag,
        byte DestinationSlot
    );

    public readonly record struct ItemSplitData(
        ulong ItemGuid,
        uint SplitQuantity
    );

    public readonly record struct ItemDestroyedData(
        ulong ItemGuid,
        uint Quantity
    );

    /// <summary>
    /// Represents inventory operation results.
    /// </summary>
    public enum InventoryResult
    {
        Ok = 0,
        CantEquipLevelI = 1,
        CantEquipSkill = 2,
        ItemDoesntGoToSlot = 3,
        BagFull = 4,
        NonemptyBagOverOtherBag = 5,
        CantTradeEquipBags = 6,
        OnlyAmmoCanGoHere = 7,
        NoRequiredProficiency = 8,
        NoEquipmentSlotAvailable = 9,
        YouCanNeverUseThatItem = 10,
        YouCanNeverUseThatItem2 = 11,
        NoEquipmentSlotAvailable2 = 12,
        CantEquipWithTwohanded = 13,
        CantDualWieldTwohanded = 14,
        ItemDoesntGoIntoBag = 15,
        ItemDoesntGoIntoBag2 = 16,
        CantCarryMoreOfThis = 17,
        NoEquipmentSlotAvailable3 = 18,
        ItemCantStack = 19,
        ItemCantBeEquipped = 20,
        ItemsCantBeSwapped = 21,
        SlotIsEmpty = 22,
        ItemNotFound = 23,
        CantDropSoulbound = 24,
        OutOfRange = 25,
        TriedToSplitMoreThanCount = 26,
        CouldntSplitItems = 27,
        MissingReagent = 28,
        NotEnoughMoney = 29,
        NotABag = 30,
        CanOnlyDoWithEmptyBags = 31,
        DontOwnThatItem = 32,
        CanEquipOnly1Quiver = 33,
        MustPurchaseThatBagSlot = 34,
        TooFarAwayFromBank = 35,
        ItemLocked = 36,
        YouAreStunned = 37,
        YouAreDead = 38,
        CantDoRightNow = 39,
        IntBagError = 40,
        CanEquipOnly1Bolt = 41,
        CanEquipOnly1Ammopouch = 42,
        StackableCantBeWrapped = 43,
        EquippedCantBeWrapped = 44,
        WrappedCantBeWrapped = 45,
        BoundCantBeWrapped = 46,
        UniqueCantBeWrapped = 47,
        BagsCantBeWrapped = 48,
        AlreadyLooted = 49,
        InventoryFull = 50,
        BankFull = 51,
        ItemIsCurrentlySoldOut = 52,
        BagFull3 = 53,
        ItemNotFound2 = 54,
        ItemCantStack2 = 55,
        BagFull4 = 56,
        ItemSoldOut = 57,
        ObjectIsBusy = 58,
        None = 59,
        CantDoInCombat = 60,
        CantDoWhileDisarmed = 61,
        BagFull6 = 62,
        CantEquipRank = 63,
        CantEquipReputation = 64,
        TooManySpecialBags = 65,
        LootCantLootThatNow = 66
    }

    /// <summary>
    /// Represents inventory operation data.
    /// </summary>
    public class InventoryOperationData
    {
        /// <summary>
        /// Gets or sets the operation result.
        /// </summary>
        public InventoryResult Result { get; set; }

        /// <summary>
        /// Gets or sets the item involved in the operation.
        /// </summary>
        public InventoryItem? Item { get; set; }

        /// <summary>
        /// Gets or sets the source bag.
        /// </summary>
        public byte? SourceBag { get; set; }

        /// <summary>
        /// Gets or sets the source slot.
        /// </summary>
        public byte? SourceSlot { get; set; }

        /// <summary>
        /// Gets or sets the destination bag.
        /// </summary>
        public byte? DestinationBag { get; set; }

        /// <summary>
        /// Gets or sets the destination slot.
        /// </summary>
        public byte? DestinationSlot { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the operation.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
