using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;

namespace BotRunner.Combat;

public interface IEquipmentService
{
    /// <summary>
    /// Scans inventory for equippable upgrades and equips them.
    /// Returns the number of items equipped.
    /// </summary>
    int TryEquipUpgrades(IObjectManager objectManager, uint playerLevel);
}

public class EquipmentService : IEquipmentService
{
    // Cooldown tracking to avoid rapid equip attempts
    private DateTime _lastEquipAttempt = DateTime.MinValue;
    private const int EQUIP_COOLDOWN_MS = 5000;

    public int TryEquipUpgrades(IObjectManager objectManager, uint playerLevel)
    {
        if ((DateTime.Now - _lastEquipAttempt).TotalMilliseconds < EQUIP_COOLDOWN_MS)
            return 0;

        _lastEquipAttempt = DateTime.Now;
        int equipped = 0;

        // Iterate all bag positions to find equippable upgrades
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20; // backpack has 16, extra bags up to ~18
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var item = objectManager.GetContainedItem(bag, slot);
                if (item?.Info == null) continue;

                var equipSlot = item.Info.EquipSlot;

                // Skip non-equipment slots
                if (!IsEquippableSlot(equipSlot)) continue;

                // Skip items above player's level
                if (item.RequiredLevel > playerLevel) continue;

                // Skip weapons/armor the player's class can't use
                var playerClass = objectManager.Player?.Class ?? 0;
                if (playerClass != 0 && !CanEquipItem(playerClass, item.Info))
                    continue;

                // For dual-slot items (rings, trinkets), check both slots
                var slotsToCheck = GetEquipmentSlots(equipSlot);
                EquipSlot? bestSlot = null;
                IWoWItem? worstEquipped = null;

                foreach (var checkSlot in slotsToCheck)
                {
                    var currentItem = objectManager.GetEquippedItem(checkSlot);
                    if (currentItem == null)
                    {
                        // Empty slot — always equip here
                        bestSlot = checkSlot;
                        worstEquipped = null;
                        break;
                    }

                    if (worstEquipped == null || IsWorseThan(currentItem, worstEquipped))
                    {
                        worstEquipped = currentItem;
                        bestSlot = checkSlot;
                    }
                }

                if (bestSlot == null) continue;

                // Compare: is the inventory item better than what's equipped?
                if (worstEquipped == null || IsUpgrade(item, worstEquipped))
                {
                    Log.Information("[EQUIP] Equipping upgrade: {ItemName} (Q:{Quality}, RL:{ReqLvl}) → {Slot}",
                        item.Info.Name, item.Quality, item.RequiredLevel, bestSlot.Value);
                    objectManager.EquipItem(bag, slot, bestSlot.Value);
                    equipped++;

                    // Only equip one item per scan to avoid conflicts
                    return equipped;
                }
            }
        }

        return equipped;
    }

    private static bool IsEquippableSlot(EquipSlot slot) => slot switch
    {
        EquipSlot.Head => true,
        EquipSlot.Neck => true,
        EquipSlot.Shoulders => true,
        EquipSlot.Chest => true,
        EquipSlot.Waist => true,
        EquipSlot.Legs => true,
        EquipSlot.Feet => true,
        EquipSlot.Wrist => true,
        EquipSlot.Hands => true,
        EquipSlot.Finger1 => true,
        EquipSlot.Finger2 => true,
        EquipSlot.Trinket1 => true,
        EquipSlot.Trinket2 => true,
        EquipSlot.Back => true,
        EquipSlot.MainHand => true,
        EquipSlot.OffHand => true,
        EquipSlot.Ranged => true,
        _ => false
    };

    /// <summary>
    /// Returns all equipment slots that this item type can go into.
    /// Rings can go in either finger slot, trinkets in either trinket slot.
    /// </summary>
    private static EquipSlot[] GetEquipmentSlots(EquipSlot itemSlot) => itemSlot switch
    {
        EquipSlot.Finger1 or EquipSlot.Finger2 => [EquipSlot.Finger1, EquipSlot.Finger2],
        EquipSlot.Trinket1 or EquipSlot.Trinket2 => [EquipSlot.Trinket1, EquipSlot.Trinket2],
        _ => [itemSlot]
    };

    /// <summary>
    /// Determines if a new item is an upgrade over the currently equipped item.
    /// Comparison order: Quality (higher = better), then RequiredLevel as proxy for item power.
    /// </summary>
    private static bool IsUpgrade(IWoWItem newItem, IWoWItem currentItem)
    {
        // Higher quality is always better
        if (newItem.Quality > currentItem.Quality) return true;
        if (newItem.Quality < currentItem.Quality) return false;

        // Same quality: higher required level usually means stronger
        return newItem.RequiredLevel > currentItem.RequiredLevel;
    }

    /// <summary>
    /// Determines if itemA is worse than itemB (for finding the worst equipped item in dual slots).
    /// </summary>
    private static bool IsWorseThan(IWoWItem itemA, IWoWItem itemB)
    {
        if (itemA.Quality < itemB.Quality) return true;
        if (itemA.Quality > itemB.Quality) return false;
        return itemA.RequiredLevel < itemB.RequiredLevel;
    }

    /// <summary>
    /// Checks whether the player's class can equip this item (weapon type + armor proficiency).
    /// Uses ItemSubclass directly since ItemClass enum is unreliable.
    /// </summary>
    internal static bool CanEquipItem(Class playerClass, ItemCacheInfo info)
    {
        var sub = info.ItemSubclass;
        if (IsWeaponSubclass(sub))
            return CanUseWeaponType(playerClass, sub);
        if (IsArmorSubclass(sub))
            return CanUseArmorType(playerClass, sub);
        return true;
    }

    private static bool IsWeaponSubclass(ItemSubclass sub) => sub is
        ItemSubclass.OneHandedAxe or ItemSubclass.TwoHandedAxe
        or ItemSubclass.Bow or ItemSubclass.Gun
        or ItemSubclass.OneHandedMace or ItemSubclass.TwoHandedMace
        or ItemSubclass.Polearm
        or ItemSubclass.OneHandedSword or ItemSubclass.TwoHandedSword
        or ItemSubclass.Staff or ItemSubclass.FistWeapon
        or ItemSubclass.MiscellaneousWeapon or ItemSubclass.Dagger
        or ItemSubclass.Thrown or ItemSubclass.Crossbow
        or ItemSubclass.Wand or ItemSubclass.FishingPole;

    private static bool IsArmorSubclass(ItemSubclass sub) => sub is
        ItemSubclass.MiscellaneousArmor or ItemSubclass.Cloth
        or ItemSubclass.Leather or ItemSubclass.Mail or ItemSubclass.Plate
        or ItemSubclass.Shield or ItemSubclass.Libram
        or ItemSubclass.Idol or ItemSubclass.Totem;

    /// <summary>
    /// Vanilla 1.12.1 weapon proficiency by class.
    /// </summary>
    internal static bool CanUseWeaponType(Class playerClass, ItemSubclass weaponType) => playerClass switch
    {
        Class.Warrior => weaponType is ItemSubclass.OneHandedAxe or ItemSubclass.TwoHandedAxe
            or ItemSubclass.OneHandedMace or ItemSubclass.TwoHandedMace
            or ItemSubclass.OneHandedSword or ItemSubclass.TwoHandedSword
            or ItemSubclass.Dagger or ItemSubclass.FistWeapon
            or ItemSubclass.Polearm or ItemSubclass.Staff
            or ItemSubclass.Bow or ItemSubclass.Crossbow or ItemSubclass.Gun or ItemSubclass.Thrown,

        Class.Paladin => weaponType is ItemSubclass.OneHandedAxe or ItemSubclass.TwoHandedAxe
            or ItemSubclass.OneHandedMace or ItemSubclass.TwoHandedMace
            or ItemSubclass.OneHandedSword or ItemSubclass.TwoHandedSword
            or ItemSubclass.Polearm,

        Class.Hunter => weaponType is ItemSubclass.OneHandedAxe or ItemSubclass.TwoHandedAxe
            or ItemSubclass.OneHandedSword or ItemSubclass.TwoHandedSword
            or ItemSubclass.Dagger or ItemSubclass.FistWeapon
            or ItemSubclass.Polearm or ItemSubclass.Staff
            or ItemSubclass.Bow or ItemSubclass.Crossbow or ItemSubclass.Gun,

        Class.Rogue => weaponType is ItemSubclass.OneHandedMace or ItemSubclass.OneHandedSword
            or ItemSubclass.Dagger or ItemSubclass.FistWeapon
            or ItemSubclass.Bow or ItemSubclass.Crossbow or ItemSubclass.Gun or ItemSubclass.Thrown,

        Class.Priest => weaponType is ItemSubclass.OneHandedMace
            or ItemSubclass.Dagger or ItemSubclass.Staff or ItemSubclass.Wand,

        Class.Shaman => weaponType is ItemSubclass.OneHandedAxe or ItemSubclass.TwoHandedAxe
            or ItemSubclass.OneHandedMace or ItemSubclass.TwoHandedMace
            or ItemSubclass.Dagger or ItemSubclass.FistWeapon or ItemSubclass.Staff,

        Class.Mage => weaponType is ItemSubclass.OneHandedSword
            or ItemSubclass.Dagger or ItemSubclass.Staff or ItemSubclass.Wand,

        Class.Warlock => weaponType is ItemSubclass.OneHandedSword
            or ItemSubclass.Dagger or ItemSubclass.Staff or ItemSubclass.Wand,

        Class.Druid => weaponType is ItemSubclass.OneHandedMace or ItemSubclass.TwoHandedMace
            or ItemSubclass.Dagger or ItemSubclass.FistWeapon
            or ItemSubclass.Polearm or ItemSubclass.Staff,

        _ => true
    };

    /// <summary>
    /// Vanilla 1.12.1 armor proficiency by class.
    /// </summary>
    internal static bool CanUseArmorType(Class playerClass, ItemSubclass armorType) => armorType switch
    {
        // Everyone can use misc armor, cloth, shields depend on class
        ItemSubclass.MiscellaneousArmor => true,
        ItemSubclass.Cloth => true,

        ItemSubclass.Leather => playerClass is Class.Warrior or Class.Paladin or Class.Hunter
            or Class.Rogue or Class.Shaman or Class.Druid,

        ItemSubclass.Mail => playerClass is Class.Warrior or Class.Paladin or Class.Hunter or Class.Shaman,

        ItemSubclass.Plate => playerClass is Class.Warrior or Class.Paladin,

        ItemSubclass.Shield => playerClass is Class.Warrior or Class.Paladin or Class.Shaman,

        // Class-specific relics
        ItemSubclass.Libram => playerClass is Class.Paladin,
        ItemSubclass.Idol => playerClass is Class.Druid,
        ItemSubclass.Totem => playerClass is Class.Shaman,

        _ => true
    };
}
