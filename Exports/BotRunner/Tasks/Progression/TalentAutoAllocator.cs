using GameData.Core.Interfaces;
using Serilog; // TODO: migrate to ILogger when DI is available
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Allocates talent points on level-up based on pre-defined talent build paths.
/// Each class/spec has an ordered list of talent allocations.
/// On level-up, applies the next unspent talent point to the next talent in the build.
/// </summary>
public static class TalentAutoAllocator
{
    /// <summary>
    /// Get the next talent to learn for a given class, spec, and current level.
    /// Returns null if all talents for current level are allocated.
    /// </summary>
    public static TalentAllocation? GetNextAllocation(
        string className,
        string specName,
        int characterLevel,
        int talentPointsSpent)
    {
        var buildKey = $"{className}_{specName}";
        if (!TalentBuilds.TryGetValue(buildKey, out var build))
        {
            Log.Warning("[TALENT] No build defined for {Class}_{Spec}", className, specName);
            return null;
        }

        // Available talent points = (level - 9) since talents start at level 10
        var availablePoints = characterLevel - 9;
        if (availablePoints <= 0) return null;

        // If all available points are spent, nothing to do
        if (talentPointsSpent >= availablePoints) return null;

        // Get the next allocation in the build order
        if (talentPointsSpent >= build.Count) return null;

        return build[talentPointsSpent];
    }

    /// <summary>
    /// Get all pending allocations for a level-up burst (multiple unspent points).
    /// </summary>
    public static IReadOnlyList<TalentAllocation> GetPendingAllocations(
        string className,
        string specName,
        int characterLevel,
        int talentPointsSpent)
    {
        var result = new List<TalentAllocation>();
        var availablePoints = characterLevel - 9;

        var buildKey = $"{className}_{specName}";
        if (!TalentBuilds.TryGetValue(buildKey, out var build))
            return result;

        for (int i = talentPointsSpent; i < availablePoints && i < build.Count; i++)
        {
            result.Add(build[i]);
        }

        return result;
    }

    /// <summary>
    /// Pre-defined talent builds. Key = "ClassName_SpecName".
    /// Each entry is an ordered list of talent allocations (tab, row, col).
    /// </summary>
    public static readonly Dictionary<string, List<TalentAllocation>> TalentBuilds = new()
    {
        // Fury Warrior 0/31/20 leveling
        ["Warrior_Fury"] =
        [
            new(1, 0, 0, "Booming Voice"), new(1, 0, 0, "Booming Voice"), new(1, 0, 0, "Booming Voice"), new(1, 0, 0, "Booming Voice"), new(1, 0, 0, "Booming Voice"),
            new(1, 0, 1, "Cruelty"), new(1, 0, 1, "Cruelty"), new(1, 0, 1, "Cruelty"), new(1, 0, 1, "Cruelty"), new(1, 0, 1, "Cruelty"),
            new(1, 1, 1, "Unbridled Wrath"), new(1, 1, 1, "Unbridled Wrath"), new(1, 1, 1, "Unbridled Wrath"), new(1, 1, 1, "Unbridled Wrath"), new(1, 1, 1, "Unbridled Wrath"),
            new(1, 2, 1, "Flurry"), new(1, 2, 1, "Flurry"), new(1, 2, 1, "Flurry"), new(1, 2, 1, "Flurry"), new(1, 2, 1, "Flurry"),
            new(1, 3, 1, "Bloodthirst"),
            // Continue into Arms/Prot...
        ],

        // Arms Warrior 31/20/0
        ["Warrior_Arms"] =
        [
            new(0, 0, 1, "Deflection"), new(0, 0, 1, "Deflection"), new(0, 0, 1, "Deflection"), new(0, 0, 1, "Deflection"), new(0, 0, 1, "Deflection"),
            new(0, 1, 0, "Tactical Mastery"), new(0, 1, 0, "Tactical Mastery"), new(0, 1, 0, "Tactical Mastery"), new(0, 1, 0, "Tactical Mastery"), new(0, 1, 0, "Tactical Mastery"),
            new(0, 2, 1, "Impale"), new(0, 2, 1, "Impale"),
            new(0, 3, 1, "Mortal Strike"),
        ],

        // Protection Warrior 0/5/46
        ["Warrior_Protection"] =
        [
            new(2, 0, 0, "Shield Specialization"), new(2, 0, 0, "Shield Specialization"), new(2, 0, 0, "Shield Specialization"), new(2, 0, 0, "Shield Specialization"), new(2, 0, 0, "Shield Specialization"),
            new(2, 0, 2, "Anticipation"), new(2, 0, 2, "Anticipation"), new(2, 0, 2, "Anticipation"), new(2, 0, 2, "Anticipation"), new(2, 0, 2, "Anticipation"),
            new(2, 1, 1, "Toughness"), new(2, 1, 1, "Toughness"), new(2, 1, 1, "Toughness"), new(2, 1, 1, "Toughness"), new(2, 1, 1, "Toughness"),
        ],

        // Holy Priest 21/30/0
        ["Priest_Holy"] =
        [
            new(0, 0, 1, "Healing Focus"), new(0, 0, 1, "Healing Focus"), new(0, 0, 2, "Holy Specialization"), new(0, 0, 2, "Holy Specialization"), new(0, 0, 2, "Holy Specialization"),
            new(0, 0, 2, "Holy Specialization"), new(0, 0, 2, "Holy Specialization"), new(0, 1, 2, "Inspiration"), new(0, 1, 2, "Inspiration"), new(0, 1, 2, "Inspiration"),
        ],

        // Frost Mage 0/0/51 leveling
        ["Mage_Frost"] =
        [
            new(2, 0, 0, "Improved Frostbolt"), new(2, 0, 0, "Improved Frostbolt"), new(2, 0, 0, "Improved Frostbolt"), new(2, 0, 0, "Improved Frostbolt"), new(2, 0, 0, "Improved Frostbolt"),
            new(2, 0, 2, "Frostbite"), new(2, 0, 2, "Frostbite"), new(2, 0, 2, "Frostbite"),
            new(2, 1, 0, "Ice Shards"), new(2, 1, 0, "Ice Shards"), new(2, 1, 0, "Ice Shards"), new(2, 1, 0, "Ice Shards"), new(2, 1, 0, "Ice Shards"),
            new(2, 2, 1, "Shatter"), new(2, 2, 1, "Shatter"), new(2, 2, 1, "Shatter"), new(2, 2, 1, "Shatter"), new(2, 2, 1, "Shatter"),
        ],
    };
}

/// <summary>
/// A single talent allocation: which tab, row, column, and the talent name for logging.
/// </summary>
public record TalentAllocation(int TabIndex, int Row, int Column, string TalentName);
