using GameData.Core.Enums;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents the result of a vendor buy operation.
    /// </summary>
    public enum VendorBuyResult
    {
        Success = 0,
        InsufficientFunds = 1,
        ItemNotAvailable = 2,
        InventoryFull = 3,
        TooFarFromVendor = 4,
        VendorNotFound = 5,
        InvalidItem = 6,
        ReputationTooLow = 7,
        RequirementNotMet = 8,
        Unknown = 99
    }

    /// <summary>
    /// Represents the result of a vendor sell operation.
    /// </summary>
    public enum VendorSellResult
    {
        Success = 0,
        ItemNotSellable = 1,
        ItemNotOwned = 2,
        VendorCantBuyItem = 3,
        TooFarFromVendor = 4,
        VendorNotFound = 5,
        ItemBound = 6,
        Unknown = 99
    }

    /// <summary>
    /// Represents the result of a vendor repair operation.
    /// </summary>
    public enum VendorRepairResult
    {
        Success = 0,
        InsufficientFunds = 1,
        ItemNotDamaged = 2,
        ItemNotRepairable = 3,
        TooFarFromVendor = 4,
        VendorNotFound = 5,
        VendorCantRepair = 6,
        Unknown = 99
    }

    /// <summary>
    /// Represents an item available for purchase from a vendor.
    /// </summary>
    public class VendorItem
    {
        /// <summary>
        /// Gets or sets the vendor slot index.
        /// </summary>
        public byte VendorSlot { get; set; }

        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item price in copper.
        /// </summary>
        public uint Price { get; set; }

        /// <summary>
        /// Gets or sets the maximum quantity available (-1 for unlimited).
        /// </summary>
        public int MaxQuantity { get; set; } = -1;

        /// <summary>
        /// Gets or sets the current quantity available.
        /// </summary>
        public int AvailableQuantity { get; set; } = -1;

        /// <summary>
        /// Gets or sets the number of items in a stack purchase.
        /// </summary>
        public uint StackSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the item quality.
        /// </summary>
        public ItemQuality Quality { get; set; }

        /// <summary>
        /// Gets or sets whether the player can use this item.
        /// </summary>
        public bool CanUse { get; set; } = true;

        /// <summary>
        /// Gets or sets the required reputation faction (if any).
        /// </summary>
        public uint RequiredReputation { get; set; }

        /// <summary>
        /// Gets or sets whether this item requires special currency.
        /// </summary>
        public bool RequiresSpecialCurrency { get; set; }
    }

    /// <summary>
    /// Represents vendor information and state.
    /// </summary>
    public class VendorInfo
    {
        /// <summary>
        /// Gets or sets the vendor GUID.
        /// </summary>
        public ulong VendorGuid { get; set; }

        /// <summary>
        /// Gets or sets the vendor name.
        /// </summary>
        public string VendorName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the vendor can repair items.
        /// </summary>
        public bool CanRepair { get; set; }

        /// <summary>
        /// Gets or sets whether the vendor window is currently open.
        /// </summary>
        public bool IsWindowOpen { get; set; }

        /// <summary>
        /// Gets or sets the list of items available for purchase.
        /// </summary>
        public List<VendorItem> AvailableItems { get; set; } = [];

        /// <summary>
        /// Gets or sets the last time the vendor inventory was updated.
        /// </summary>
        public DateTime LastInventoryUpdate { get; set; }

        /// <summary>
        /// Gets or sets the vendor type flags.
        /// </summary>
        public uint VendorFlags { get; set; }
    }

    /// <summary>
    /// Represents data for a vendor purchase transaction.
    /// </summary>
    public class VendorPurchaseData
    {
        /// <summary>
        /// Gets or sets the vendor GUID.
        /// </summary>
        public ulong VendorGuid { get; set; }

        /// <summary>
        /// Gets or sets the item ID purchased.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity purchased.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets the total cost in copper.
        /// </summary>
        public uint TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the purchase result.
        /// </summary>
        public VendorBuyResult Result { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the purchase.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents data for a vendor sale transaction.
    /// </summary>
    public class VendorSaleData
    {
        /// <summary>
        /// Gets or sets the vendor GUID.
        /// </summary>
        public ulong VendorGuid { get; set; }

        /// <summary>
        /// Gets or sets the item ID sold.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity sold.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets the total value received in copper.
        /// </summary>
        public uint TotalValue { get; set; }

        /// <summary>
        /// Gets or sets the bag ID where the item was located.
        /// </summary>
        public byte BagId { get; set; }

        /// <summary>
        /// Gets or sets the slot ID where the item was located.
        /// </summary>
        public byte SlotId { get; set; }

        /// <summary>
        /// Gets or sets the sale result.
        /// </summary>
        public VendorSellResult Result { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the sale.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents data for a vendor repair transaction.
    /// </summary>
    public class VendorRepairData
    {
        /// <summary>
        /// Gets or sets the vendor GUID.
        /// </summary>
        public ulong VendorGuid { get; set; }

        /// <summary>
        /// Gets or sets whether this was a repair all operation.
        /// </summary>
        public bool IsRepairAll { get; set; }

        /// <summary>
        /// Gets or sets the bag ID (for single item repairs).
        /// </summary>
        public byte? BagId { get; set; }

        /// <summary>
        /// Gets or sets the slot ID (for single item repairs).
        /// </summary>
        public byte? SlotId { get; set; }

        /// <summary>
        /// Gets or sets the total repair cost in copper.
        /// </summary>
        public uint TotalCost { get; set; }

        /// <summary>
        /// Gets or sets the repair result.
        /// </summary>
        public VendorRepairResult Result { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the repair.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an item that can be sold as junk.
    /// </summary>
    public class JunkItem
    {
        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bag ID.
        /// </summary>
        public byte BagId { get; set; }

        /// <summary>
        /// Gets or sets the slot ID.
        /// </summary>
        public byte SlotId { get; set; }

        /// <summary>
        /// Gets or sets the quantity available.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets the estimated vendor value.
        /// </summary>
        public uint EstimatedValue { get; set; }

        /// <summary>
        /// Gets or sets the item quality.
        /// </summary>
        public ItemQuality Quality { get; set; }

        /// <summary>
        /// Gets or sets whether this item is bound.
        /// </summary>
        public bool IsBound { get; set; }
    }

    /// <summary>
    /// Represents a soulbound item confirmation request.
    /// </summary>
    public class SoulboundConfirmation
    {
        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the confirmation type (Buy/Sell).
        /// </summary>
        public SoulboundConfirmationType ConfirmationType { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when confirmation was requested.
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets additional context data.
        /// </summary>
        public object? ContextData { get; set; }
    }

    /// <summary>
    /// Represents the type of soulbound confirmation.
    /// </summary>
    public enum SoulboundConfirmationType
    {
        BuyItem,
        SellItem,
        RepairItem
    }

    /// <summary>
    /// Represents options for bulk vendor operations.
    /// </summary>
    public class BulkVendorOptions
    {
        /// <summary>
        /// Gets or sets the maximum time to spend on the operation.
        /// </summary>
        public TimeSpan MaxOperationTime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to automatically confirm soulbound items.
        /// </summary>
        public bool AutoConfirmSoulbound { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum item quality to consider for junk selling.
        /// </summary>
        public ItemQuality MinimumJunkQuality { get; set; } = ItemQuality.Poor;

        /// <summary>
        /// Gets or sets the maximum item quality to consider for junk selling.
        /// </summary>
        public ItemQuality MaximumJunkQuality { get; set; } = ItemQuality.Common;

        /// <summary>
        /// Gets or sets whether to skip items with special significance.
        /// </summary>
        public bool SkipSpecialItems { get; set; } = true;

        /// <summary>
        /// Gets or sets the delay between individual operations.
        /// </summary>
        public TimeSpan DelayBetweenOperations { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Gets or sets whether to continue on errors.
        /// </summary>
        public bool ContinueOnError { get; set; } = true;
    }
}