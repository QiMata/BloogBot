using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameData.Core.Models.Activities;
using WoWStateManager.Activities;
using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Phase 0 slot S0.4 — catalog invariant tests.
///
/// Asserts the 7 invariants from
/// <c>docs/Spec/04_ACTIVITIES.md#catalog--hard-coded-source-of-truth</c>
/// hold for the compiled <see cref="ActivityCatalog"/>. Adding a row that
/// violates any invariant fails the matching test, which surfaces the
/// shape error before the row reaches a downstream consumer.
///
/// Invariants:
///   R1  — every row has a unique <see cref="ActivityDefinition.Id"/>.
///   R14 — every <see cref="ActivityDefinition.Location"/> resolves in
///         <c>Bot/named-locations.json</c>.
///   3   — every <see cref="LevelRange"/> is within [1, 60] and Min &lt;= Max.
///   4   — every <see cref="RoleTemplate"/> sums within
///         [<see cref="ActivityDefinition.MinPlayers"/>,
///          <see cref="ActivityDefinition.MaxPlayers"/>].
///   R16 — every <see cref="ActivityDefinition.TaskFamily"/> is one of
///         the fixed family-head strings from
///         <c>docs/Spec/03_BOTRUNNER.md#catalog-of-task-families</c>.
///   7   — every <see cref="ActivityDefinition.Family"/> is a valid
///         <see cref="ActivityFamily"/> enum value.
///   R18 — every row has a non-empty
///         <see cref="ActivityDefinition.Rewards"/> list
///         (always-picks-reward invariant).
/// </summary>
public sealed class ActivityCatalogTests
{
    private readonly IActivityCatalog _catalog = new ActivityCatalog();

    /// <summary>
    /// Fixed task-family head set per
    /// <c>docs/Spec/03_BOTRUNNER.md#catalog-of-task-families</c> and R16
    /// in <c>docs/Plan/QUESTIONS.md</c>.
    /// </summary>
    private static readonly HashSet<string> ValidTaskFamilies = new(StringComparer.Ordinal)
    {
        "Travel",
        "Combat",
        "Questing",
        "Dungeoneering",
        "Raid",
        "Bg",
        "Gathering",
        "Crafting",
        "Economy",
        "Social",
        "Recovery",
        "Equipment",
        "WorldEvent",
        "Loadout",
    };

    [Fact]
    public void Every_Row_Has_Unique_Id()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        foreach (var def in _catalog.All)
        {
            if (!seen.Add(def.Id))
            {
                duplicates.Add(def.Id);
            }
        }

        Assert.True(
            duplicates.Count == 0,
            "ActivityCatalog has duplicate Ids: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void Every_Location_Resolves_In_Named_Locations_Json()
    {
        var locations = LoadNamedLocations();

        Assert.NotEmpty(locations);

        var missing = new List<string>();
        foreach (var def in _catalog.All)
        {
            if (!locations.Contains(def.Location))
            {
                missing.Add($"{def.Id} -> '{def.Location}'");
            }
        }

        Assert.True(
            missing.Count == 0,
            "ActivityDefinition.Location values not found in Bot/named-locations.json: " +
            string.Join("; ", missing));
    }

    [Fact]
    public void Every_LevelRange_Is_Within_1_To_60_With_Min_Le_Max()
    {
        var failures = new List<string>();

        foreach (var def in _catalog.All)
        {
            var lr = def.LevelRange;
            if (lr.Min < 1 || lr.Max > 60 || lr.Min > lr.Max)
            {
                failures.Add($"{def.Id} -> LevelRange({lr.Min}, {lr.Max})");
            }
        }

        Assert.True(
            failures.Count == 0,
            "ActivityDefinition.LevelRange must satisfy 1 <= Min <= Max <= 60: " +
            string.Join("; ", failures));
    }

    [Fact]
    public void Every_RoleTemplate_Sums_Within_MinPlayers_To_MaxPlayers()
    {
        var failures = new List<string>();

        foreach (var def in _catalog.All)
        {
            var rt = def.RoleTemplate;
            var sum = rt.Tanks + rt.Healers + rt.Dps + rt.Support;

            if (sum < def.MinPlayers || sum > def.MaxPlayers)
            {
                failures.Add(
                    $"{def.Id} -> RoleTemplate(T={rt.Tanks}, H={rt.Healers}, D={rt.Dps}, S={rt.Support}) " +
                    $"sum={sum} outside [{def.MinPlayers}, {def.MaxPlayers}]");
            }
        }

        Assert.True(
            failures.Count == 0,
            "RoleTemplate sum must be within [MinPlayers, MaxPlayers]: " +
            string.Join("; ", failures));
    }

    [Fact]
    public void Every_TaskFamily_Is_Fixed_Family_Head()
    {
        var failures = new List<string>();

        foreach (var def in _catalog.All)
        {
            if (!ValidTaskFamilies.Contains(def.TaskFamily))
            {
                failures.Add($"{def.Id} -> TaskFamily='{def.TaskFamily}'");
            }
        }

        Assert.True(
            failures.Count == 0,
            "ActivityDefinition.TaskFamily must be one of {" +
            string.Join(", ", ValidTaskFamilies) + "}: " +
            string.Join("; ", failures));
    }

    [Fact]
    public void Every_Family_Is_Valid_ActivityFamily_Enum_Value()
    {
        var failures = new List<string>();

        foreach (var def in _catalog.All)
        {
            if (!Enum.IsDefined(typeof(ActivityFamily), def.Family))
            {
                failures.Add($"{def.Id} -> Family={(int)def.Family}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "ActivityDefinition.Family must be a defined ActivityFamily enum value: " +
            string.Join("; ", failures));
    }

    [Fact]
    public void Every_Row_Has_NonEmpty_Rewards()
    {
        var failures = new List<string>();

        foreach (var def in _catalog.All)
        {
            if (def.Rewards is null || def.Rewards.Count == 0)
            {
                failures.Add(def.Id);
            }
        }

        Assert.True(
            failures.Count == 0,
            "ActivityDefinition.Rewards must be non-empty (R18 always-picks-reward invariant). " +
            "Rows with empty rewards: " + string.Join(", ", failures));
    }

    // ----------------------------------------------------------------- helpers

    private static HashSet<string> LoadNamedLocations()
    {
        var path = LocateNamedLocationsJson();
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // Skip JSON Schema pragmas (e.g. "$schema").
            if (prop.Name.StartsWith("$", StringComparison.Ordinal))
                continue;

            keys.Add(prop.Name);
        }
        return keys;
    }

    private static string LocateNamedLocationsJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Bot", "named-locations.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate Bot/named-locations.json by walking up from " +
            $"'{AppContext.BaseDirectory}'. The catalog Location test needs the file to run.");
    }
}
