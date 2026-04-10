using Game;
using System;
using System.IO;
using WoWStateManager.Settings;
using WoWActivitySnapshot = Communication.WoWActivitySnapshot;

namespace BotRunner.Tests.LiveValidation;

public class CoordinatorFixtureBaseTests
{
    [Fact]
    public void SerializeSettings_UsesStringEnums_AndOmitsNullProperties()
    {
        var json = CoordinatorFixtureBase.SerializeSettings(
        [
            CoordinatorFixtureBase.CreateCharacterSetting(
                accountName: "RFCBOT1",
                characterClass: "Warrior",
                characterRace: "Orc",
                characterGender: "Female",
                runnerType: BotRunnerType.Foreground)
        ]);

        Assert.Contains("\"RunnerType\": \"Foreground\"", json);
        Assert.Contains("\"CharacterClass\": \"Warrior\"", json);
        Assert.DoesNotContain("\"BuildConfig\":", json);
        Assert.DoesNotContain("\"TargetProcessId\":", json);
    }

    [Fact]
    public void CanReuseExistingCharacters_ReturnsTrue_ForSingleMatchingCharacter()
    {
        var settings = CoordinatorFixtureBase.CreateCharacterSetting(
            accountName: "WSGBOTA9",
            characterClass: "Warrior",
            characterRace: "Dwarf",
            characterGender: "Female",
            runnerType: BotRunnerType.Background);

        var existingCharacters = new[]
        {
            new AccountCharacterRecord("ExpectedDwarf", RaceId: 3, ClassId: 1, GenderId: 1)
        };

        Assert.True(CoordinatorFixtureBase.CanReuseExistingCharacters(settings, existingCharacters));
    }

    [Fact]
    public void CanReuseExistingCharacters_ReturnsFalse_ForSingleMismatchedCharacter()
    {
        var settings = CoordinatorFixtureBase.CreateCharacterSetting(
            accountName: "WSGBOTA9",
            characterClass: "Warrior",
            characterRace: "Dwarf",
            characterGender: "Female",
            runnerType: BotRunnerType.Background);

        var existingCharacters = new[]
        {
            new AccountCharacterRecord("WrongRace", RaceId: 1, ClassId: 1, GenderId: 1)
        };

        Assert.False(CoordinatorFixtureBase.CanReuseExistingCharacters(settings, existingCharacters));
    }

    [Fact]
    public void CanReuseExistingCharacters_ReturnsFalse_WhenAccountHasMultipleCharacters()
    {
        var settings = CoordinatorFixtureBase.CreateCharacterSetting(
            accountName: "WSGBOTA9",
            characterClass: "Warrior",
            characterRace: "Dwarf",
            characterGender: "Female",
            runnerType: BotRunnerType.Background);

        var existingCharacters = new[]
        {
            new AccountCharacterRecord("ExpectedDwarf", RaceId: 3, ClassId: 1, GenderId: 1),
            new AccountCharacterRecord("ExtraCharacter", RaceId: 1, ClassId: 1, GenderId: 1)
        };

        Assert.False(CoordinatorFixtureBase.CanReuseExistingCharacters(settings, existingCharacters));
    }

    [Fact]
    public void HasAnyMatchingCharacter_ReturnsTrue_WhenAnyCharacterMatches()
    {
        var settings = CoordinatorFixtureBase.CreateCharacterSetting(
            accountName: "WSGBOTA9",
            characterClass: "Warrior",
            characterRace: "Dwarf",
            characterGender: "Female",
            runnerType: BotRunnerType.Background);

        var existingCharacters = new[]
        {
            new AccountCharacterRecord("WrongRace", RaceId: 1, ClassId: 1, GenderId: 1),
            new AccountCharacterRecord("ExpectedDwarf", RaceId: 3, ClassId: 1, GenderId: 1)
        };

        Assert.True(CoordinatorFixtureBase.HasAnyMatchingCharacter(settings, existingCharacters));
    }

    [Fact]
    public void HasAnyMatchingCharacter_ReturnsFalse_WhenNoCharacterMatches()
    {
        var settings = CoordinatorFixtureBase.CreateCharacterSetting(
            accountName: "WSGBOTA9",
            characterClass: "Warrior",
            characterRace: "Dwarf",
            characterGender: "Female",
            runnerType: BotRunnerType.Background);

        var existingCharacters = new[]
        {
            new AccountCharacterRecord("WrongRace1", RaceId: 1, ClassId: 1, GenderId: 1),
            new AccountCharacterRecord("WrongRace2", RaceId: 2, ClassId: 1, GenderId: 1)
        };

        Assert.False(CoordinatorFixtureBase.HasAnyMatchingCharacter(settings, existingCharacters));
    }

    [Fact]
    public void BuildRaidInviteBatches_SplitsInitialPartyFromRaidRemainder()
    {
        var batches = CoordinatorFixtureBase.BuildRaidInviteBatches(
            ["LEADER", "BOT2", "BOT3", "BOT4", "BOT5", "BOT6", "BOT7"]);

        Assert.Equal(2, batches.Count);
        Assert.Equal(["BOT2", "BOT3", "BOT4", "BOT5"], batches[0]);
        Assert.Equal(["BOT6", "BOT7"], batches[1]);
    }

    [Fact]
    public void DescribeAccountsNotGroupedToLeader_FlagsMembersWithWrongLeaderGuid()
    {
        const ulong leaderGuid = 0x1000UL;
        var snapshots = new[]
        {
            CreateSnapshot("LEADER", selfGuid: leaderGuid, partyLeaderGuid: leaderGuid),
            CreateSnapshot("BOT2", selfGuid: 0x1001UL, partyLeaderGuid: leaderGuid),
            CreateSnapshot("BOT3", selfGuid: 0x1002UL, partyLeaderGuid: 0),
            CreateSnapshot("BOT4", selfGuid: 0x1003UL, partyLeaderGuid: 0x9999UL),
        };

        var issues = CoordinatorFixtureBase.DescribeAccountsNotGroupedToLeader(
            "LEADER",
            ["LEADER", "BOT2", "BOT3", "BOT4"],
            snapshots);

        Assert.Equal(["BOT3(leader=0x0)", "BOT4(leader=0x9999)"], issues);
    }

    [Theory]
    [InlineData(30u, true)]
    [InlineData(489u, true)]
    [InlineData(529u, true)]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    public void IsBattlegroundMapId_RecognizesKnownVanillaBattlegroundMaps(uint mapId, bool expected)
    {
        Assert.Equal(expected, CoordinatorFixtureBase.IsBattlegroundMapId(mapId));
    }

    [Fact]
    public void DescribeAccountsOnBattlegroundMaps_ReturnsOnlyAccountsStillInsideBattlegroundMaps()
    {
        var snapshots = new[]
        {
            CreateSnapshot("WSG1", selfGuid: 0x1UL, partyLeaderGuid: 0, mapId: 489u),
            CreateSnapshot("AV1", selfGuid: 0x2UL, partyLeaderGuid: 0, mapId: 30u),
            CreateSnapshot("CITY1", selfGuid: 0x3UL, partyLeaderGuid: 0, mapId: 1u),
        };

        var results = CoordinatorFixtureBase.DescribeAccountsOnBattlegroundMaps(
            ["WSG1", "AV1", "CITY1"],
            snapshots);

        Assert.Equal(["WSG1(map=489)", "AV1(map=30)"], results);
    }

    [Fact]
    public async Task WaitForConditionAsync_ReturnsTrue_WhenConditionEventuallySucceeds()
    {
        var attempts = 0;

        var result = await CoordinatorFixtureBase.WaitForConditionAsync(
            () => Task.FromResult(++attempts >= 3),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(1));

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task WaitForConditionAsync_ReturnsFalse_WhenConditionNeverSucceeds()
    {
        var attempts = 0;

        var result = await CoordinatorFixtureBase.WaitForConditionAsync(
            () => Task.FromResult(++attempts >= 100),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(1));

        Assert.False(result);
        Assert.True(attempts > 0);
    }

    [Fact]
    public void WriteSettingsFile_UsesConfiguredRuntimeRoot()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"wwow-runtime-{Guid.NewGuid():N}");
        var previousRoot = Environment.GetEnvironmentVariable("WWOW_TEST_RUNTIME_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("WWOW_TEST_RUNTIME_ROOT", runtimeRoot);
            var path = CoordinatorFixtureBase.WriteSettingsFile(
            [
                CoordinatorFixtureBase.CreateCharacterSetting(
                    accountName: "RFCBOT1",
                    characterClass: "Warrior",
                    characterRace: "Orc",
                    characterGender: "Female",
                    runnerType: BotRunnerType.Foreground)
            ],
            "runtime-path-test.settings.json");

            Assert.StartsWith(Path.GetFullPath(runtimeRoot), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_TEST_RUNTIME_ROOT", previousRoot);
            if (Directory.Exists(runtimeRoot))
                Directory.Delete(runtimeRoot, recursive: true);
        }
    }

    private static WoWActivitySnapshot CreateSnapshot(
        string accountName,
        ulong selfGuid,
        ulong partyLeaderGuid,
        uint mapId = 1u)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CharacterName = accountName,
            ScreenState = "InWorld",
            IsObjectManagerValid = true,
            PartyLeaderGuid = partyLeaderGuid,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject
                        {
                            Guid = selfGuid,
                            MapId = mapId
                        }
                    }
                }
            }
        };
    }
}
