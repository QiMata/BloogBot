using GameData.Core.Enums;

namespace BotRunner.Progression;

/// <summary>
/// Represents a target piece of gear the bot should acquire for a specific equipment slot.
/// </summary>
/// <param name="Slot">The equipment slot this goal targets.</param>
/// <param name="TargetItemId">The item_template entry ID of the desired item.</param>
/// <param name="ItemName">Display name of the target item.</param>
/// <param name="Source">Acquisition source, e.g. "Dungeon:Stratholme:BaronRivendare", "Quest:TheGreatMasquerade", "Craft:ArcaniteReaper", "Vendor:OrgWeaponsmith", "AH".</param>
/// <param name="Priority">1=immediate upgrade, 2=next upgrade, 3=eventual BiS.</param>
public record GearGoal(
    EquipSlot Slot,
    int TargetItemId,
    string ItemName,
    string Source,
    int Priority);

/// <summary>
/// Represents a gap between currently equipped gear and a target goal.
/// </summary>
/// <param name="Slot">The equipment slot with the gap.</param>
/// <param name="CurrentItemId">Entry ID of the currently equipped item (0 if empty).</param>
/// <param name="CurrentItemName">Name of the currently equipped item ("(empty)" if none).</param>
/// <param name="TargetItemId">Entry ID of the target item.</param>
/// <param name="TargetItemName">Name of the target item.</param>
/// <param name="Source">Acquisition source string.</param>
/// <param name="Priority">Priority level from the originating GearGoal.</param>
public record GearGap(
    EquipSlot Slot,
    int CurrentItemId,
    string CurrentItemName,
    int TargetItemId,
    string TargetItemName,
    string Source,
    int Priority);
