using System.Collections.Generic;
using System.Linq;
using BotRunner.Travel;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Arathi Basin tests. Launches 30 bots: 15 Horde (1 FG + 14 BG) and
/// 15 Alliance (1 FG + 14 BG).
/// Fixture prep handles revive/level/teleport/GM-off; the coordinator handles queue and entry only.
/// </summary>
public class ArathiBasinFixture : BattlegroundCoordinatorFixtureBase
{
    public const int HordeBotCount = 15;
    public const int AllianceBotCount = 15;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AbMapId = 529;
    public const string HordeLeaderAccount = "ABBOT1";
    public const string AllianceLeaderAccount = "ABBOTA1";

    private static readonly string[] HordeClasses =
    [
        "Warrior", "Shaman", "Druid", "Priest", "Warlock", "Hunter", "Rogue", "Mage",
        "Warrior", "Warrior", "Shaman", "Priest", "Warlock", "Hunter", "Rogue"
    ];

    private static readonly string[] HordeRaces =
    [
        "Orc", "Orc", "Tauren", "Undead", "Undead", "Orc", "Undead", "Troll",
        "Orc", "Tauren", "Orc", "Undead", "Undead", "Troll", "Undead"
    ];

    private static readonly string[] AllianceClasses =
    [
        "Warrior", "Paladin", "Druid", "Priest", "Warlock", "Hunter", "Rogue", "Mage",
        "Warrior", "Warrior", "Paladin", "Priest", "Warlock", "Hunter", "Rogue"
    ];

    private static readonly string[] AllianceRaces =
    [
        "Human", "Human", "NightElf", "Human", "Human", "NightElf", "Human", "Gnome",
        "Human", "Dwarf", "Dwarf", "NightElf", "Gnome", "Dwarf", "NightElf"
    ];

    protected override string SettingsFileName => "ArathiBasin.settings.json";

    protected override string FixtureLabel => "AB";

    protected override uint BattlegroundTypeId => 3;

    protected override uint BattlegroundMapId => AbMapId;

    protected override int TargetLevel => BattlemasterData.GetMinimumLevel(BattlemasterData.BattlegroundType.ArathiBasin);

    protected override IReadOnlyCollection<string> HordeAccounts
        => Enumerable.Range(0, HordeBotCount)
            .Select(index => $"ABBOT{index + 1}")
            .ToArray();

    protected override IReadOnlyCollection<string> AllianceAccounts
        => Enumerable.Range(0, AllianceBotCount)
            .Select(index => $"ABBOTA{index + 1}")
            .ToArray();

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarAb.MapId,
        BattlemasterData.OrgrimmarAb.Position.X,
        BattlemasterData.OrgrimmarAb.Position.Y,
        BattlemasterData.OrgrimmarAb.Position.Z + 3f);

    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindAb.MapId,
        BattlemasterData.StormwindAb.Position.X,
        BattlemasterData.StormwindAb.Position.Y,
        BattlemasterData.StormwindAb.Position.Z + 3f);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var bots = new List<CharacterSettings>(TotalBotCount);

        for (var index = 0; index < HordeBotCount; index++)
        {
            bots.Add(CreateCharacterSetting(
                accountName: $"ABBOT{index + 1}",
                characterClass: HordeClasses[index],
                characterRace: HordeRaces[index],
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: index == 0 ? BotRunnerType.Foreground : BotRunnerType.Background));
        }

        for (var index = 0; index < AllianceBotCount; index++)
        {
            bots.Add(CreateCharacterSetting(
                accountName: $"ABBOTA{index + 1}",
                characterClass: AllianceClasses[index],
                characterRace: AllianceRaces[index],
                characterGender: index % 2 == 0 ? "Female" : "Male",
                runnerType: index == 0 ? BotRunnerType.Foreground : BotRunnerType.Background));
        }

        return bots;
    }
}

[CollectionDefinition(Name)]
public class ArathiBasinCollection : ICollectionFixture<ArathiBasinFixture>
{
    public const string Name = "ArathiBasinValidation";
}
