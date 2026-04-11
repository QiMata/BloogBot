using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Communication;
using BotRunner.Tests.LiveValidation.Battlegrounds;
using BotRunner.Travel;
using Game;
using WoWStateManager.Settings;
using SettingsBotRunnerType = WoWStateManager.Settings.BotRunnerType;

namespace BotRunner.Tests.LiveValidation;

public sealed class BattlegroundFixtureConfigurationTests
{
    [Fact]
    public void WarsongGulchFixture_UsesForegroundLeadersForBothFactions()
    {
        var fixture = new WarsongGulchFixture();
        var settings = GetCharacterSettings(fixture);

        Assert.Equal(WarsongGulchFixture.TotalBotCount, settings.Count);
        Assert.False(GetPrepareDuringInitialization(fixture));
        AssertForegroundLeaders(settings, WarsongGulchFixture.HordeLeaderAccount, WarsongGulchFixture.AllianceLeaderAccount);
    }

    [Fact]
    public void ArathiBasinFixture_UsesForegroundLeadersForBothFactions()
    {
        var fixture = new ArathiBasinFixture();
        var settings = GetCharacterSettings(fixture);

        Assert.Equal(ArathiBasinFixture.TotalBotCount, settings.Count);
        Assert.False(GetPrepareDuringInitialization(fixture));
        AssertForegroundLeaders(settings, ArathiBasinFixture.HordeLeaderAccount, ArathiBasinFixture.AllianceLeaderAccount);
    }

    [Fact]
    public void AlteracValleyFixture_UsesForegroundLeadersForBothFactions()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);

        Assert.Equal(AlteracValleyFixture.TotalBotCount, settings.Count);
        Assert.False(GetPrepareDuringInitialization(fixture));
        AssertForegroundLeaders(settings, AlteracValleyFixture.HordeLeaderAccount, AlteracValleyFixture.AllianceLeaderAccount);
    }

    [Fact]
    public void BattlegroundFixtures_PreserveExistingCharacters_WhenAnyConfiguredCharacterMatches()
    {
        Assert.True(GetPreserveExistingCharactersWhenAnyMatch(new WarsongGulchFixture()));
        Assert.True(GetPreserveExistingCharactersWhenAnyMatch(new ArathiBasinFixture()));
        Assert.True(GetPreserveExistingCharactersWhenAnyMatch(new AlteracValleyFixture()));
    }

    [Fact]
    public void BattlegroundFixtures_UseDedicatedNonOverlappingAccountPools()
    {
        var avAccounts = GetCharacterSettings(new AlteracValleyFixture())
            .Select(setting => setting.AccountName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wsgAccounts = GetCharacterSettings(new WarsongGulchFixture())
            .Select(setting => setting.AccountName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var abAccounts = GetCharacterSettings(new ArathiBasinFixture())
            .Select(setting => setting.AccountName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(avAccounts.Intersect(wsgAccounts, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(avAccounts.Intersect(abAccounts, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(wsgAccounts.Intersect(abAccounts, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void AlteracValleyFixture_UsesLevel60ObjectiveReadyLeaderRoster()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);

        Assert.Equal(AlteracValleyLoadoutPlan.TargetLevel, GetProtectedIntProperty(fixture, "TargetLevel"));

        var hordeLeader = Assert.Single(settings.Where(setting => setting.AccountName == AlteracValleyFixture.HordeLeaderAccount));
        Assert.Equal("Warrior", hordeLeader.CharacterClass);
        Assert.Equal("Tauren", hordeLeader.CharacterRace);
        Assert.Equal(SettingsBotRunnerType.Foreground, hordeLeader.RunnerType);

        var allianceLeader = Assert.Single(settings.Where(setting => setting.AccountName == AlteracValleyFixture.AllianceLeaderAccount));
        Assert.Equal("Paladin", allianceLeader.CharacterClass);
        Assert.Equal("Human", allianceLeader.CharacterRace);
        Assert.Equal(SettingsBotRunnerType.Foreground, allianceLeader.RunnerType);
    }

    [Fact]
    public void AlteracValleyFixture_BuildsLeaderAndRaidLoadouts()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);
        var loadouts = settings.ToDictionary(
            setting => setting.AccountName,
            AlteracValleyLoadoutPlan.ResolveLoadout,
            StringComparer.OrdinalIgnoreCase);

        var hordeLeader = loadouts[AlteracValleyFixture.HordeLeaderAccount];
        Assert.Equal((uint)383, hordeLeader.ArmorSetId);
        Assert.Contains((uint)18831, hordeLeader.EquipItemIds);
        Assert.Equal(AlteracValleyLoadoutPlan.HordeFactionMountItemId, hordeLeader.MountItemId);
        Assert.Equal(AlteracValleyLoadoutPlan.PvPRankForLoadout, hordeLeader.HonorRank);

        var allianceLeader = loadouts[AlteracValleyFixture.AllianceLeaderAccount];
        Assert.Equal((uint)402, allianceLeader.ArmorSetId);
        Assert.Contains((uint)23454, allianceLeader.EquipItemIds);
        Assert.Contains((uint)18825, allianceLeader.EquipItemIds);
        Assert.Equal(AlteracValleyLoadoutPlan.AllianceFactionMountItemId, allianceLeader.MountItemId);

        var hordeRaidMember = loadouts["AVBOT2"];
        Assert.Equal((uint)538, hordeRaidMember.ArmorSetId);
        Assert.Contains((uint)23464, hordeRaidMember.EquipItemIds);

        var allianceRaidMember = loadouts["AVBOTA2"];
        Assert.Equal((uint)545, allianceRaidMember.ArmorSetId);
        Assert.Contains((uint)18876, allianceRaidMember.EquipItemIds);
    }

    [Fact]
    public void AlteracValleyFixture_BuildsSupplementalItemsWithoutArmorSetDuplicates()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);
        var loadouts = settings.ToDictionary(
            setting => setting.AccountName,
            AlteracValleyLoadoutPlan.ResolveLoadout,
            StringComparer.OrdinalIgnoreCase);

        var leaderSupplemental = AlteracValleyFixture.BuildSupplementalItemIds(loadouts[AlteracValleyFixture.HordeLeaderAccount]);
        Assert.DoesNotContain((uint)16541, leaderSupplemental);
        Assert.Contains((uint)18831, leaderSupplemental);
        Assert.Contains(AlteracValleyLoadoutPlan.HordeFactionMountItemId, leaderSupplemental);
        Assert.Contains(AlteracValleyLoadoutPlan.ElixirOfTheMongooseItemId, leaderSupplemental);
        Assert.Contains(AlteracValleyLoadoutPlan.ElixirOfSuperiorDefenseItemId, leaderSupplemental);

        var paladinSupplemental = AlteracValleyFixture.BuildSupplementalItemIds(loadouts[AlteracValleyFixture.AllianceLeaderAccount]);
        Assert.DoesNotContain((uint)16471, paladinSupplemental);
        Assert.Contains((uint)23454, paladinSupplemental);
        Assert.Contains((uint)18825, paladinSupplemental);
        Assert.Contains(AlteracValleyLoadoutPlan.AllianceFactionMountItemId, paladinSupplemental);
    }

    [Fact]
    public void AlteracValleyFixture_BuildsFirstObjectiveAssignmentsForEveryAccount()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);
        var assignments = AlteracValleyLoadoutPlan.BuildFirstObjectiveAssignments(settings);

        Assert.Equal(AlteracValleyFixture.TotalBotCount, assignments.Count);

        var hordeLeader = assignments[AlteracValleyFixture.HordeLeaderAccount];
        Assert.Equal((uint)30, hordeLeader.MapId);
        Assert.InRange(hordeLeader.X, 540f, 570f);
        Assert.InRange(hordeLeader.Y, -110f, -70f);

        var allianceLeader = assignments[AlteracValleyFixture.AllianceLeaderAccount];
        Assert.Equal((uint)30, allianceLeader.MapId);
        Assert.InRange(allianceLeader.X, -590f, -555f);
        Assert.InRange(allianceLeader.Y, -285f, -240f);
    }

    [Fact]
    public void AlteracValleyLoadoutPlan_BuildAdaptiveAssignments_UsesLiveFactionCenters()
    {
        var fixture = new AlteracValleyFixture();
        var settings = GetCharacterSettings(fixture);

        var snapshots = new List<WoWActivitySnapshot>();
        foreach (var account in AlteracValleyFixture.HordeAccountsOrdered.Take(12))
            snapshots.Add(BuildSnapshot(account, mapId: AlteracValleyFixture.AvMapId, x: 620f, y: -95f, z: 62f));
        foreach (var account in AlteracValleyFixture.AllianceAccountsOrdered.Take(12))
            snapshots.Add(BuildSnapshot(account, mapId: AlteracValleyFixture.AvMapId, x: -620f, y: -255f, z: 88f));

        var assignments = AlteracValleyLoadoutPlan.BuildAdaptiveFirstObjectiveAssignments(settings, snapshots);

        var hordeLeader = assignments[AlteracValleyFixture.HordeLeaderAccount];
        var allianceLeader = assignments[AlteracValleyFixture.AllianceLeaderAccount];
        Assert.True(hordeLeader.X < 620f);
        Assert.True(allianceLeader.X > -620f);
    }

    [Fact]
    public void BattlegroundFixtures_DisableForegroundPacketHooks_ForCrossMapTransfers()
    {
        var originalInjectionDisablePacketHooks = Environment.GetEnvironmentVariable("Injection__DisablePacketHooks");
        var originalDisablePacketHooks = Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS");

        try
        {
            var fixture = new TestableWarsongGulchFixture();

            fixture.ApplyCoordinatorEnvironment();

            Assert.Equal("true", Environment.GetEnvironmentVariable("Injection__DisablePacketHooks"));
            Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Injection__DisablePacketHooks", originalInjectionDisablePacketHooks);
            Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", originalDisablePacketHooks);
        }
    }

    [Fact]
    public void BattlegroundFixtures_ExtendEnterWorldTimeouts_ForFirstLoginCinematics()
    {
        var fixture = new WarsongGulchFixture();

        Assert.Equal(TimeSpan.FromMinutes(5), GetProtectedTimeSpanProperty(fixture, "EnterWorldMaxTimeout"));
        Assert.Equal(TimeSpan.FromMinutes(5), GetBaseFixtureTimeSpanProperty(fixture, "InitialWorldEntryTimeout"));
        Assert.Equal(TimeSpan.FromSeconds(90), GetProtectedTimeSpanProperty(fixture, "EnterWorldStaleTimeout"));
    }

    [Fact]
    public void AlteracValleyFixture_ExtendsEnterWorldTimeouts_ForEightyBotColdStarts()
    {
        var fixture = new AlteracValleyFixture();

        Assert.Equal(TimeSpan.FromMinutes(10), GetProtectedTimeSpanProperty(fixture, "EnterWorldMaxTimeout"));
        Assert.Equal(TimeSpan.FromMinutes(10), GetBaseFixtureTimeSpanProperty(fixture, "InitialWorldEntryTimeout"));
        Assert.Equal(TimeSpan.FromMinutes(2), GetProtectedTimeSpanProperty(fixture, "EnterWorldStaleTimeout"));
    }

    [Fact]
    public void BattlegroundFixtures_UseBattlegroundSpecificMinimumLevels()
    {
        Assert.Equal(
            60,
            GetProtectedIntProperty(new WarsongGulchFixture(), "TargetLevel"));
        Assert.Equal(
            BattlemasterData.GetMinimumLevel(BattlemasterData.BattlegroundType.ArathiBasin),
            GetProtectedIntProperty(new ArathiBasinFixture(), "TargetLevel"));
        Assert.Equal(
            AlteracValleyLoadoutPlan.TargetLevel,
            GetProtectedIntProperty(new AlteracValleyFixture(), "TargetLevel"));
    }

    private static IReadOnlyList<CharacterSettings> GetCharacterSettings(CoordinatorFixtureBase fixture)
    {
        var method = fixture.GetType().GetMethod("BuildCharacterSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(fixture, null);
        var settings = Assert.IsAssignableFrom<IReadOnlyList<CharacterSettings>>(result);
        return settings;
    }

    private static bool GetPrepareDuringInitialization(CoordinatorFixtureBase fixture)
    {
        var property = typeof(CoordinatorFixtureBase).GetProperty("PrepareDuringInitialization", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);

        return Assert.IsType<bool>(property!.GetValue(fixture));
    }

    private static TimeSpan GetProtectedTimeSpanProperty(CoordinatorFixtureBase fixture, string propertyName)
    {
        var property = typeof(CoordinatorFixtureBase).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);

        return Assert.IsType<TimeSpan>(property!.GetValue(fixture));
    }

    private static TimeSpan GetBaseFixtureTimeSpanProperty(CoordinatorFixtureBase fixture, string propertyName)
    {
        var property = typeof(LiveBotFixture).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);

        return Assert.IsType<TimeSpan>(property!.GetValue(fixture));
    }

    private static int GetProtectedIntProperty(CoordinatorFixtureBase fixture, string propertyName)
    {
        var property = fixture.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);

        return Assert.IsType<int>(property!.GetValue(fixture));
    }

    private static bool GetPreserveExistingCharactersWhenAnyMatch(CoordinatorFixtureBase fixture)
    {
        var property = typeof(CoordinatorFixtureBase).GetProperty("PreserveExistingCharactersWhenAnyMatch", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);

        return Assert.IsType<bool>(property!.GetValue(fixture));
    }

    private static void AssertForegroundLeaders(
        IReadOnlyList<CharacterSettings> settings,
        string hordeLeaderAccount,
        string allianceLeaderAccount)
    {
        var foregroundAccounts = settings
            .Where(setting => setting.RunnerType == SettingsBotRunnerType.Foreground)
            .Select(setting => setting.AccountName)
            .OrderBy(account => account)
            .ToArray();

        Assert.Equal(2, foregroundAccounts.Length);
        Assert.Contains(hordeLeaderAccount, foregroundAccounts);
        Assert.Contains(allianceLeaderAccount, foregroundAccounts);
    }

    private sealed class TestableWarsongGulchFixture : WarsongGulchFixture
    {
        public void ApplyCoordinatorEnvironment() => ConfigureCoordinatorEnvironment();
    }

    private static WoWActivitySnapshot BuildSnapshot(string accountName, uint mapId, float x, float y, float z)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CurrentMapId = mapId,
            ScreenState = "InWorld",
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject
                        {
                            MapId = mapId,
                            Position = new Position { X = x, Y = y, Z = z }
                        }
                    }
                }
            }
        };
    }
}
