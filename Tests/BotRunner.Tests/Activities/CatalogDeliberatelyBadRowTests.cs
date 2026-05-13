using System;
using System.Collections.Generic;
using System.Linq;
using GameData.Core.Models.Activities;
using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Phase 0 slot S0.4 — deliberately-bad-row negative-path coverage.
///
/// Per the slot procedure: "Deliberately bad row (in a separate unit test
/// using a sample catalog) is rejected." This class builds a small
/// in-memory sample of malformed <see cref="ActivityDefinition"/> rows
/// and asserts that each invariant check rejects the matching row.
///
/// The checks mirror <see cref="ActivityCatalogTests"/> one-for-one but
/// run against the bad sample instead of the compiled catalog. Whenever
/// <see cref="ActivityCatalogTests"/> grows a new invariant, this class
/// grows a matching negative case.
/// </summary>
public sealed class CatalogDeliberatelyBadRowTests
{
    [Fact]
    public void Duplicate_Id_Is_Rejected()
    {
        var sample = new[]
        {
            MakeGoodRow(id: "dup.row"),
            MakeGoodRow(id: "dup.row"),
        };

        Assert.False(HasUniqueIds(sample));
        // Sanity: a corrected sample passes.
        Assert.True(HasUniqueIds(new[]
        {
            MakeGoodRow(id: "dup.row"),
            MakeGoodRow(id: "dup.row.b"),
        }));
    }

    [Fact]
    public void LevelRange_Inverted_Min_Max_Is_Rejected()
    {
        var bad = MakeGoodRow(levelRange: new LevelRange(70, 60));
        Assert.False(LevelRangeIsValid(bad));

        var good = MakeGoodRow(levelRange: new LevelRange(1, 60));
        Assert.True(LevelRangeIsValid(good));
    }

    [Fact]
    public void LevelRange_Below_One_Is_Rejected()
    {
        var bad = MakeGoodRow(levelRange: new LevelRange(0, 10));
        Assert.False(LevelRangeIsValid(bad));
    }

    [Fact]
    public void LevelRange_Above_Sixty_Is_Rejected()
    {
        var bad = MakeGoodRow(levelRange: new LevelRange(40, 70));
        Assert.False(LevelRangeIsValid(bad));
    }

    [Fact]
    public void RoleTemplate_Exceeding_MaxPlayers_Is_Rejected()
    {
        var bad = MakeGoodRow(
            minPlayers: 1,
            maxPlayers: 3,
            roleTemplate: new RoleTemplate(Tanks: 2, Healers: 2, Dps: 5));

        Assert.False(RoleTemplateFitsParty(bad));
    }

    [Fact]
    public void RoleTemplate_Below_MinPlayers_Is_Rejected()
    {
        var bad = MakeGoodRow(
            minPlayers: 5,
            maxPlayers: 10,
            roleTemplate: new RoleTemplate(Tanks: 1, Healers: 0, Dps: 1));

        Assert.False(RoleTemplateFitsParty(bad));
    }

    [Fact]
    public void Unknown_TaskFamily_Is_Rejected()
    {
        var bad = MakeGoodRow(taskFamily: "NotAFamilyHead");
        Assert.False(TaskFamilyIsValid(bad));

        var good = MakeGoodRow(taskFamily: "Questing");
        Assert.True(TaskFamilyIsValid(good));
    }

    [Fact]
    public void Undefined_ActivityFamily_Enum_Value_Is_Rejected()
    {
        // (ActivityFamily)9999 is intentionally outside the defined enum range.
        var bad = MakeGoodRow(family: (ActivityFamily)9999);
        Assert.False(FamilyIsDefined(bad));

        var good = MakeGoodRow(family: ActivityFamily.Dungeon);
        Assert.True(FamilyIsDefined(good));
    }

    [Fact]
    public void Empty_Rewards_Is_Rejected()
    {
        var bad = MakeGoodRow(rewards: Array.Empty<RewardDefinition>());
        Assert.False(HasRewards(bad));

        var good = MakeGoodRow();
        Assert.True(HasRewards(good));
    }

    // --------------------------------------------------- invariant predicates

    private static bool HasUniqueIds(IEnumerable<ActivityDefinition> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (!seen.Add(r.Id))
                return false;
        }
        return true;
    }

    private static bool LevelRangeIsValid(ActivityDefinition def) =>
        def.LevelRange.Min >= 1
        && def.LevelRange.Max <= 60
        && def.LevelRange.Min <= def.LevelRange.Max;

    private static bool RoleTemplateFitsParty(ActivityDefinition def)
    {
        var sum = def.RoleTemplate.Tanks
                  + def.RoleTemplate.Healers
                  + def.RoleTemplate.Dps
                  + def.RoleTemplate.Support;
        return sum >= def.MinPlayers && sum <= def.MaxPlayers;
    }

    private static readonly HashSet<string> _validTaskFamilies = new(StringComparer.Ordinal)
    {
        "Travel", "Combat", "Questing", "Dungeoneering", "Raid", "Bg",
        "Gathering", "Crafting", "Economy", "Social", "Recovery",
        "Equipment", "WorldEvent", "Loadout",
    };

    private static bool TaskFamilyIsValid(ActivityDefinition def) =>
        _validTaskFamilies.Contains(def.TaskFamily);

    private static bool FamilyIsDefined(ActivityDefinition def) =>
        Enum.IsDefined(typeof(ActivityFamily), def.Family);

    private static bool HasRewards(ActivityDefinition def) =>
        def.Rewards is { Count: > 0 };

    // ------------------------------------------------------------- factories

    /// <summary>
    /// Builds an otherwise-valid <see cref="ActivityDefinition"/>. Tests
    /// pass overrides to mutate exactly the field under test, isolating the
    /// invariant being asserted.
    /// </summary>
    private static ActivityDefinition MakeGoodRow(
        string id = "test.good.row",
        ActivityFamily family = ActivityFamily.Dungeon,
        LevelRange? levelRange = null,
        int minPlayers = 1,
        int maxPlayers = 5,
        RoleTemplate? roleTemplate = null,
        string taskFamily = "Dungeoneering",
        IReadOnlyList<RewardDefinition>? rewards = null)
    {
        return new ActivityDefinition
        {
            Id = id,
            Family = family,
            Activity = "Test",
            Location = "Elwynn Forest",
            LevelRange = levelRange ?? new LevelRange(10, 20),
            FactionPolicy = new FactionPolicy(FactionRequirement.Either, AllowCrossFaction: false),
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers,
            RoleTemplate = roleTemplate ?? new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
            EntryRequirements = new EntryRequirements
            {
                RequiredItems = Array.Empty<int>(),
                RequiredQuests = Array.Empty<int>(),
                RequiredReputations = Array.Empty<FactionStanding>(),
                RequiredAttunements = Array.Empty<string>(),
                RequiredCapabilities = Array.Empty<string>(),
                LockoutPolicy = LockoutPolicy.None(),
            },
            TravelTarget = new TravelTarget(
                MapId: 0, X: 0f, Y: 0f, Z: 0f,
                NamedLocation: "Elwynn Forest"),
            ExpectedDuration = TimeSpan.FromHours(1),
            HumanJoinPolicy = new HumanJoinPolicy
            {
                HumanCanInitiate = true,
                HumanRole = HumanGroupRole.Member,
                RequireFactionMatch = true,
                LootPriorityToHuman = false,
                HumanIdleTimeout = TimeSpan.FromMinutes(5),
            },
            BotSelectionPolicy = new BotSelectionPolicy(),
            ProgressionTags = Array.Empty<string>(),
            Rewards = rewards ??
            [
                new RewardDefinition(RewardKind.XpRange, Min: 1000, Max: 2000, ItemId: null, FactionId: null),
            ],
            TaskFamily = taskFamily,
        };
    }
}
