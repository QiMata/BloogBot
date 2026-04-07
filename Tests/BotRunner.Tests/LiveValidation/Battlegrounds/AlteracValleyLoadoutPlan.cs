using System;
using System.Collections.Generic;
using System.Linq;
using WoWStateManager.Settings;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

internal static class AlteracValleyLoadoutPlan
{
    internal const int TargetLevel = 60;
    internal const int PvPRankForLoadout = 14; // Grand Marshal / High Warlord (Vanilla max)
    internal const uint RidingSkillId = 762;
    internal const uint ApprenticeRidingSpellId = 33389;
    internal const int EpicRidingSkill = 150;
    internal const uint HordeFactionMountItemId = 19029; // Horn of the Frostwolf Howler
    internal const uint AllianceFactionMountItemId = 19030; // Stormpike Battle Charger
    internal const uint ElixirOfFortitudeItemId = 3825;
    internal const uint ElixirOfGreaterIntellectItemId = 9179;
    internal const uint ElixirOfSuperiorDefenseItemId = 13445;
    internal const uint ElixirOfTheMongooseItemId = 13452;

    private static readonly ObjectiveTarget HordeFirstObjectiveBase = new(30, 557.2f, -86.9f, 62.2f);
    private static readonly ObjectiveTarget AllianceFirstObjectiveBase = new(30, -572.3f, -262.5f, 88.6f);

    private static readonly IReadOnlyDictionary<string, FactionLoadoutDefinition> HordeLoadouts =
        new Dictionary<string, FactionLoadoutDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Warrior"] = new(537, [22858, 22868, 22872, 22873, 23243, 23244], [18877]),
            ["Shaman"] = new(538, [22857, 22867, 22876, 22887, 23259, 23260], [23464, 18826]),
            ["Druid"] = new(539, [22852, 22863, 22877, 22878, 23253, 23254], [18874]),
            ["Priest"] = new(540, [22859, 22869, 22882, 22885, 23261, 23262], [18874]),
            ["Warlock"] = new(541, [22855, 22865, 22881, 22884, 23255, 23256], [18874]),
            ["Mage"] = new(542, [22860, 22870, 22883, 22886, 23263, 23264], [18874]),
            ["Hunter"] = new(543, [22843, 22862, 22874, 22875, 23251, 23252], [18835]),
            ["Rogue"] = new(522, [22856, 22864, 22879, 22880, 23257, 23258], [23467]),
        };

    private static readonly IReadOnlyDictionary<string, FactionLoadoutDefinition> AllianceLoadouts =
        new Dictionary<string, FactionLoadoutDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Warrior"] = new(545, [23286, 23287, 23300, 23301, 23314, 23315], [18876]),
            ["Paladin"] = new(544, [23272, 23273, 23274, 23275, 23276, 23277], [23454, 18825]),
            ["Druid"] = new(551, [23280, 23281, 23294, 23295, 23308, 23309], [18873]),
            ["Priest"] = new(549, [23288, 23289, 23302, 23303, 23316, 23317], [18873]),
            ["Warlock"] = new(547, [23282, 23283, 23296, 23297, 23310, 23311], [18873]),
            ["Mage"] = new(546, [23290, 23291, 23304, 23305, 23318, 23319], [18873]),
            ["Hunter"] = new(550, [23278, 23279, 23292, 23293, 23306, 23307], [18833]),
            ["Rogue"] = new(548, [23284, 23285, 23298, 23299, 23312, 23313], [23456]),
        };

    internal static AlteracValleyLoadout ResolveLoadout(CharacterSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var accountName = settings.AccountName;
        var characterClass = settings.CharacterClass
            ?? throw new InvalidOperationException($"AV loadout requires CharacterClass for '{accountName}'.");
        var isHorde = IsHordeRace(settings.CharacterRace);

        var definitions = isHorde ? HordeLoadouts : AllianceLoadouts;
        if (!definitions.TryGetValue(characterClass, out var definition))
            throw new InvalidOperationException($"No AV loadout definition exists for {(isHorde ? "Horde" : "Alliance")} class '{characterClass}'.");

        var isLeader = string.Equals(accountName, AlteracValleyFixture.HordeLeaderAccount, StringComparison.OrdinalIgnoreCase)
            || string.Equals(accountName, AlteracValleyFixture.AllianceLeaderAccount, StringComparison.OrdinalIgnoreCase);

        var armorSetId = definition.BlueArmorSetId;
        var armorItemIds = definition.BlueArmorItemIds;
        var equipItemIds = new List<uint>(armorItemIds.Count + definition.FactionWeaponItemIds.Count)
        {
        };
        equipItemIds.AddRange(armorItemIds);
        equipItemIds.AddRange(definition.FactionWeaponItemIds);

        if (isLeader)
        {
            if (string.Equals(accountName, AlteracValleyFixture.HordeLeaderAccount, StringComparison.OrdinalIgnoreCase))
            {
                armorSetId = 383;
                armorItemIds = [16541, 16542, 16543, 16544, 16545, 16548];
                equipItemIds = [16541, 16542, 16543, 16544, 16545, 16548, 18831];
            }
            else
            {
                armorSetId = 402;
                armorItemIds = [16471, 16472, 16473, 16474, 16475, 16476];
                equipItemIds = [16471, 16472, 16473, 16474, 16475, 16476, 23454, 18825];
            }
        }

        return new AlteracValleyLoadout(
            accountName,
            characterClass,
            armorSetId,
            armorItemIds,
            equipItemIds,
            isHorde ? HordeFactionMountItemId : AllianceFactionMountItemId,
            ResolveElixirs(characterClass),
            PvPRankForLoadout,
            BuildFormationTarget(
                isHorde ? HordeFirstObjectiveBase : AllianceFirstObjectiveBase,
                isHorde
                    ? IndexOfAccount(AlteracValleyFixture.HordeAccountsOrdered, accountName)
                    : IndexOfAccount(AlteracValleyFixture.AllianceAccountsOrdered, accountName)));
    }

    internal static IReadOnlyDictionary<string, ObjectiveTarget> BuildFirstObjectiveAssignments(IEnumerable<CharacterSettings> settings)
    {
        var assignments = new Dictionary<string, ObjectiveTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings)
            assignments[setting.AccountName] = ResolveLoadout(setting).FirstObjectiveTarget;

        return assignments;
    }

    private static IReadOnlyList<uint> ResolveElixirs(string characterClass)
    {
        return characterClass.ToLowerInvariant() switch
        {
            "mage" or "priest" or "warlock" => [ElixirOfGreaterIntellectItemId, ElixirOfFortitudeItemId],
            _ => [ElixirOfTheMongooseItemId, ElixirOfSuperiorDefenseItemId],
        };
    }

    private static ObjectiveTarget BuildFormationTarget(ObjectiveTarget anchor, int index)
    {
        if (index < 0)
            return anchor;

        var column = (index % 5) - 2;
        var row = index / 5;
        var xOffset = column * 4f;
        var yOffset = (row * 4f) - 14f;
        return anchor with
        {
            X = anchor.X + xOffset,
            Y = anchor.Y + yOffset,
        };
    }

    private static int IndexOfAccount(IReadOnlyList<string> accounts, string accountName)
    {
        for (var index = 0; index < accounts.Count; index++)
        {
            if (string.Equals(accounts[index], accountName, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static bool IsHordeRace(string? race)
    {
        return race != null && (
            race.Equals("Orc", StringComparison.OrdinalIgnoreCase)
            || race.Equals("Undead", StringComparison.OrdinalIgnoreCase)
            || race.Equals("Tauren", StringComparison.OrdinalIgnoreCase)
            || race.Equals("Troll", StringComparison.OrdinalIgnoreCase));
    }

    internal readonly record struct AlteracValleyLoadout(
        string AccountName,
        string CharacterClass,
        uint ArmorSetId,
        IReadOnlyList<uint> ArmorItemIds,
        IReadOnlyList<uint> EquipItemIds,
        uint MountItemId,
        IReadOnlyList<uint> ElixirItemIds,
        int HonorRank,
        ObjectiveTarget FirstObjectiveTarget);

    internal readonly record struct ObjectiveTarget(uint MapId, float X, float Y, float Z);

    private sealed record FactionLoadoutDefinition(
        uint BlueArmorSetId,
        IReadOnlyList<uint> BlueArmorItemIds,
        IReadOnlyList<uint> FactionWeaponItemIds);
}
