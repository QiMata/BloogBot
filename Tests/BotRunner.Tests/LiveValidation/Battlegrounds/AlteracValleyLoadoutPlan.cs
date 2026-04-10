using System;
using System.Collections.Generic;
using System.Linq;
using Communication;
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

    // Static fallback anchors (classic first clash lanes).
    private static readonly ObjectiveTarget HordeFirstObjectiveBase = new(30, 557.2f, -86.9f, 62.2f);
    private static readonly ObjectiveTarget AllianceFirstObjectiveBase = new(30, -572.3f, -262.5f, 88.6f);
    // Lane references used for adaptive cave-egress targeting.
    private static readonly ObjectiveTarget HordeLaneReference = new(30, -755f, -355f, 68f);
    private static readonly ObjectiveTarget AllianceLaneReference = new(30, 760f, -470f, 110f);
    private const float AdaptiveObjectiveAdvanceDistance = 220f;
    private const float AdaptiveObjectiveMinimumAdvanceDistance = 80f;
    private const int AdaptiveAnchorMinimumSnapshots = 10;
    private const float AdaptivePerAccountStepMinimum = 28f;
    private const float AdaptivePerAccountStepMaximum = 70f;
    private const float AdaptivePerAccountHighVerticalDeltaThreshold = 24f;
    private const float AdaptivePerAccountHighVerticalDeltaStepCap = 36f;

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
        return BuildFirstObjectiveAssignments(settings, HordeFirstObjectiveBase, AllianceFirstObjectiveBase);
    }

    internal static IReadOnlyDictionary<string, ObjectiveTarget> BuildAdaptiveFirstObjectiveAssignments(
        IEnumerable<CharacterSettings> settings,
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        Action<string>? log = null)
    {
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (TryResolveAdaptiveAnchors(snapshots, out var hordeAnchor, out var allianceAnchor, out var diagnostics))
        {
            var assignments = BuildAdaptiveFirstObjectiveAssignments(
                settings,
                snapshotLookup,
                hordeAnchor,
                allianceAnchor,
                out var adjustedCount);
            log?.Invoke(
                $"adaptive anchors horde=({hordeAnchor.X:F1},{hordeAnchor.Y:F1},{hordeAnchor.Z:F1}) " +
                $"alliance=({allianceAnchor.X:F1},{allianceAnchor.Y:F1},{allianceAnchor.Z:F1}); " +
                $"perAccountTargets={adjustedCount}; {diagnostics}");
            return assignments;
        }

        log?.Invoke($"static anchors in use; {diagnostics}");
        return BuildFirstObjectiveAssignments(settings);
    }

    private static IReadOnlyDictionary<string, ObjectiveTarget> BuildAdaptiveFirstObjectiveAssignments(
        IEnumerable<CharacterSettings> settings,
        IReadOnlyDictionary<string, WoWActivitySnapshot> snapshotLookup,
        ObjectiveTarget hordeBase,
        ObjectiveTarget allianceBase,
        out int adjustedCount)
    {
        var assignments = new Dictionary<string, ObjectiveTarget>(StringComparer.OrdinalIgnoreCase);
        adjustedCount = 0;

        foreach (var setting in settings)
        {
            var baseTarget = ResolveFirstObjectiveTarget(setting, hordeBase, allianceBase);
            var adjustedTarget = baseTarget;

            if (snapshotLookup.TryGetValue(setting.AccountName, out var snapshot))
            {
                var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
                if (mapId == AlteracValleyFixture.AvMapId && position != null)
                {
                    var dx = baseTarget.X - position.X;
                    var dy = baseTarget.Y - position.Y;
                    var distanceToBase = MathF.Sqrt((dx * dx) + (dy * dy));
                    if (distanceToBase > 1f)
                    {
                        // Use shorter first pushes so cave-spawned bots can clear egress geometry
                        // before committing to long objective lanes.
                        var requestedStep = MathF.Min(AdaptivePerAccountStepMaximum, distanceToBase * 0.35f);
                        var step = MathF.Min(distanceToBase, MathF.Max(requestedStep, AdaptivePerAccountStepMinimum));
                        if (MathF.Abs(position.Z - baseTarget.Z) >= AdaptivePerAccountHighVerticalDeltaThreshold)
                            step = MathF.Min(step, AdaptivePerAccountHighVerticalDeltaStepCap);
                        var nx = dx / distanceToBase;
                        var ny = dy / distanceToBase;
                        adjustedTarget = new ObjectiveTarget(
                            baseTarget.MapId,
                            position.X + (nx * step),
                            position.Y + (ny * step),
                            position.Z);
                        adjustedCount++;
                    }
                }
            }

            assignments[setting.AccountName] = adjustedTarget;
        }

        return assignments;
    }

    private static IReadOnlyDictionary<string, ObjectiveTarget> BuildFirstObjectiveAssignments(
        IEnumerable<CharacterSettings> settings,
        ObjectiveTarget hordeBase,
        ObjectiveTarget allianceBase)
    {
        var assignments = new Dictionary<string, ObjectiveTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings)
            assignments[setting.AccountName] = ResolveFirstObjectiveTarget(setting, hordeBase, allianceBase);

        return assignments;
    }

    private static ObjectiveTarget ResolveFirstObjectiveTarget(
        CharacterSettings settings,
        ObjectiveTarget hordeBase,
        ObjectiveTarget allianceBase)
    {
        var isHorde = IsHordeRace(settings.CharacterRace);
        var index = isHorde
            ? IndexOfAccount(AlteracValleyFixture.HordeAccountsOrdered, settings.AccountName)
            : IndexOfAccount(AlteracValleyFixture.AllianceAccountsOrdered, settings.AccountName);
        return BuildFormationTarget(isHorde ? hordeBase : allianceBase, index);
    }

    private static bool TryResolveAdaptiveAnchors(
        IReadOnlyList<WoWActivitySnapshot> snapshots,
        out ObjectiveTarget hordeAnchor,
        out ObjectiveTarget allianceAnchor,
        out string diagnostics)
    {
        hordeAnchor = HordeFirstObjectiveBase;
        allianceAnchor = AllianceFirstObjectiveBase;

        if (snapshots.Count == 0)
        {
            diagnostics = "snapshot list is empty";
            return false;
        }

        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var hordeCount = 0;
        var allianceCount = 0;
        var hasHordeCenter = TryResolveFactionCenter(
            snapshotLookup,
            AlteracValleyFixture.HordeAccountsOrdered,
            out var hordeCenter,
            out hordeCount);
        var hasAllianceCenter = TryResolveFactionCenter(
            snapshotLookup,
            AlteracValleyFixture.AllianceAccountsOrdered,
            out var allianceCenter,
            out allianceCount);

        if (!hasHordeCenter || !hasAllianceCenter)
        {
            diagnostics =
                $"insufficient faction snapshots on map {AlteracValleyFixture.AvMapId} " +
                $"(horde={hordeCount}, alliance={allianceCount}, min={AdaptiveAnchorMinimumSnapshots})";
            return false;
        }

        var (hasHordeLaneVector, hordeDirectionX, hordeDirectionY, hordeReferenceDistance) =
            TryResolveLaneDirection(hordeCenter.X, hordeCenter.Y, HordeLaneReference.X, HordeLaneReference.Y);
        var (hasAllianceLaneVector, allianceDirectionX, allianceDirectionY, allianceReferenceDistance) =
            TryResolveLaneDirection(allianceCenter.X, allianceCenter.Y, AllianceLaneReference.X, AllianceLaneReference.Y);

        if (!hasHordeLaneVector || !hasAllianceLaneVector)
        {
            diagnostics =
                $"unable to resolve lane vector " +
                $"(hordeRefDist={hordeReferenceDistance:F1}, allianceRefDist={allianceReferenceDistance:F1})";
            return false;
        }

        var hordeAdvanceDistance = MathF.Min(AdaptiveObjectiveAdvanceDistance, hordeReferenceDistance * 0.80f);
        if (hordeAdvanceDistance < AdaptiveObjectiveMinimumAdvanceDistance)
            hordeAdvanceDistance = MathF.Min(hordeReferenceDistance, AdaptiveObjectiveMinimumAdvanceDistance);

        var allianceAdvanceDistance = MathF.Min(AdaptiveObjectiveAdvanceDistance, allianceReferenceDistance * 0.80f);
        if (allianceAdvanceDistance < AdaptiveObjectiveMinimumAdvanceDistance)
            allianceAdvanceDistance = MathF.Min(allianceReferenceDistance, AdaptiveObjectiveMinimumAdvanceDistance);

        hordeAnchor = new ObjectiveTarget(
            AlteracValleyFixture.AvMapId,
            hordeCenter.X + (hordeDirectionX * hordeAdvanceDistance),
            hordeCenter.Y + (hordeDirectionY * hordeAdvanceDistance),
            hordeCenter.Z);

        allianceAnchor = new ObjectiveTarget(
            AlteracValleyFixture.AvMapId,
            allianceCenter.X + (allianceDirectionX * allianceAdvanceDistance),
            allianceCenter.Y + (allianceDirectionY * allianceAdvanceDistance),
            allianceCenter.Z);

        diagnostics =
            $"factionCenters horde=({hordeCenter.X:F1},{hordeCenter.Y:F1}) " +
            $"alliance=({allianceCenter.X:F1},{allianceCenter.Y:F1}) " +
            $"hordeRefDist={hordeReferenceDistance:F1} hordeAdvance={hordeAdvanceDistance:F1} " +
            $"allianceRefDist={allianceReferenceDistance:F1} allianceAdvance={allianceAdvanceDistance:F1}";
        return true;
    }

    private static (bool HasDirection, float X, float Y, float Distance) TryResolveLaneDirection(
        float originX,
        float originY,
        float referenceX,
        float referenceY)
    {
        var dx = referenceX - originX;
        var dy = referenceY - originY;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < 25f)
            return (false, 0f, 0f, distance);

        return (true, dx / distance, dy / distance, distance);
    }

    private static bool TryResolveFactionCenter(
        IReadOnlyDictionary<string, WoWActivitySnapshot> snapshotLookup,
        IReadOnlyList<string> accounts,
        out (float X, float Y, float Z) center,
        out int includedCount)
    {
        float sumX = 0f;
        float sumY = 0f;
        float sumZ = 0f;
        includedCount = 0;

        foreach (var account in accounts)
        {
            if (!snapshotLookup.TryGetValue(account, out var snapshot))
                continue;

            var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
            var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
            if (mapId != AlteracValleyFixture.AvMapId || position == null)
                continue;

            sumX += position.X;
            sumY += position.Y;
            sumZ += position.Z;
            includedCount++;
        }

        if (includedCount < AdaptiveAnchorMinimumSnapshots)
        {
            center = default;
            return false;
        }

        center = (sumX / includedCount, sumY / includedCount, sumZ / includedCount);
        return true;
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
