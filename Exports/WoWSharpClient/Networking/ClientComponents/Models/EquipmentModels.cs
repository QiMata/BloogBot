using GameData.Core.Enums;
using System;
using System.Collections.Generic;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents an equipped item with its properties.
    /// </summary>
    public class EquippedItem
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
        /// Gets or sets the equipment slot.
        /// </summary>
        public EquipmentSlot EquipmentSlot { get; set; }

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
        /// Gets or sets the timestamp when this item was equipped.
        /// </summary>
        public DateTime EquippedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the item enchantments.
        /// </summary>
        public ItemEnchantment[] Enchantments { get; set; } = Array.Empty<ItemEnchantment>();

        /// <summary>
        /// Gets whether the item needs repair (durability below 100%).
        /// </summary>
        public bool NeedsRepair => MaxDurability > 0 && CurrentDurability < MaxDurability;

        /// <summary>
        /// Gets whether the item is broken (0 durability).
        /// </summary>
        public bool IsBroken => MaxDurability > 0 && CurrentDurability == 0;

        /// <summary>
        /// Gets the durability percentage.
        /// </summary>
        public float DurabilityPercentage => MaxDurability > 0 ? (float)CurrentDurability / MaxDurability * 100f : 100f;
    }

    /// <summary>
    /// Represents an item enchantment.
    /// </summary>
    public class ItemEnchantment
    {
        /// <summary>
        /// Gets or sets the enchantment ID.
        /// </summary>
        public uint EnchantmentId { get; set; }

        /// <summary>
        /// Gets or sets the enchantment slot.
        /// </summary>
        public uint Slot { get; set; }

        /// <summary>
        /// Gets or sets the enchantment charges remaining.
        /// </summary>
        public uint Charges { get; set; }

        /// <summary>
        /// Gets or sets the enchantment duration in seconds.
        /// </summary>
        public uint Duration { get; set; }

        /// <summary>
        /// Gets or sets when the enchantment was applied.
        /// </summary>
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether the enchantment is temporary.
        /// </summary>
        public bool IsTemporary => Duration > 0;

        /// <summary>
        /// Gets whether the enchantment has expired.
        /// </summary>
        public bool IsExpired => IsTemporary && DateTime.UtcNow > AppliedAt.AddSeconds(Duration);
    }

    /// <summary>
    /// Represents item durability information.
    /// </summary>
    public readonly record struct ItemDurability(
        uint Current,
        uint Maximum
    )
    {
        /// <summary>
        /// Gets whether the item needs repair.
        /// </summary>
        public bool NeedsRepair => Maximum > 0 && Current < Maximum;

        /// <summary>
        /// Gets whether the item is broken.
        /// </summary>
        public bool IsBroken => Maximum > 0 && Current == 0;

        /// <summary>
        /// Gets the durability percentage.
        /// </summary>
        public float Percentage => Maximum > 0 ? (float)Current / Maximum * 100f : 100f;
    }

    /// <summary>
    /// Represents an equipment change event.
    /// </summary>
    public class EquipmentChangeData
    {
        /// <summary>
        /// Gets or sets the equipment slot that changed.
        /// </summary>
        public EquipmentSlot Slot { get; set; }

        /// <summary>
        /// Gets or sets the previously equipped item (null if slot was empty).
        /// </summary>
        public EquippedItem? PreviousItem { get; set; }

        /// <summary>
        /// Gets or sets the newly equipped item (null if item was unequipped).
        /// </summary>
        public EquippedItem? NewItem { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the change.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets whether this was an equip operation.
        /// </summary>
        public bool IsEquip => PreviousItem == null && NewItem != null;

        /// <summary>
        /// Gets whether this was an unequip operation.
        /// </summary>
        public bool IsUnequip => PreviousItem != null && NewItem == null;

        /// <summary>
        /// Gets whether this was a replacement operation.
        /// </summary>
        public bool IsReplacement => PreviousItem != null && NewItem != null;
    }

    /// <summary>
    /// Represents the result of an equipment operation.
    /// </summary>
    public enum EquipmentResult
    {
        Success = 0,
        InvalidItem = 1,
        SlotOccupied = 2,
        InsufficientLevel = 3,
        WrongClass = 4,
        WrongRace = 5,
        ItemNotEquippable = 6,
        BagFull = 7,
        ItemBound = 8,
        ItemOnCooldown = 9,
        ItemDamaged = 10,
        Unknown = 99
    }

    /// <summary>
    /// Represents equipment operation data.
    /// </summary>
    public class EquipmentOperationData
    {
        /// <summary>
        /// Gets or sets the equipment slot.
        /// </summary>
        public EquipmentSlot Slot { get; set; }

        /// <summary>
        /// Gets or sets the item GUID involved in the operation.
        /// </summary>
        public ulong ItemGuid { get; set; }

        /// <summary>
        /// Gets or sets the operation result.
        /// </summary>
        public EquipmentResult Result { get; set; }

        /// <summary>
        /// Gets or sets the error message (if operation failed).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the operation.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents equipment set information.
    /// </summary>
    public class EquipmentSet
    {
        /// <summary>
        /// Gets or sets the set name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the set description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the items in the set by slot.
        /// </summary>
        public Dictionary<EquipmentSlot, ulong> Items { get; set; } = new();

        /// <summary>
        /// Gets or sets when the set was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets when the set was last used.
        /// </summary>
        public DateTime? LastUsed { get; set; }
    }
}