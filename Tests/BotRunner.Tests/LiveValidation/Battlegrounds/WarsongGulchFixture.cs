using System.Collections.Generic;
using System.Linq;
using BotRunner.Travel;
using WoWStateManager.Settings;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Warsong Gulch tests. Launches the full 20 bots: 10 Horde (1 FG + 9 BG) and
/// 10 Alliance (1 FG + 9 BG).
/// Fixture prep handles revive/level/teleport/GM-off; the coordinator handles queue and entry only.
/// </summary>
public class WarsongGulchFixture : BattlegroundCoordinatorFixtureBase
{
    public const int HordeBotCount = 10;
    public const int AllianceBotCount = 10;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint WsgMapId = 489;

    private static readonly IReadOnlyList<CharacterSettings> HordeComposition =
    [
        CreateCharacterSetting("WSGBOT1", "Warrior", "Orc", "Female", BotRunnerType.Foreground),
        CreateCharacterSetting("WSGBOT2", "Shaman", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT3", "Druid", "Tauren", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT4", "Priest", "Undead", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT5", "Warlock", "Undead", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT6", "Hunter", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT7", "Rogue", "Undead", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT8", "Mage", "Troll", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT9", "Warrior", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOT10", "Shaman", "Troll", "Male", BotRunnerType.Background),
    ];

    private static readonly IReadOnlyList<CharacterSettings> AllianceComposition =
    [
        CreateCharacterSetting("WSGBOTA1", "Warrior", "Human", "Female", BotRunnerType.Foreground),
        CreateCharacterSetting("WSGBOTA2", "Paladin", "Human", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA3", "Druid", "NightElf", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA4", "Priest", "Human", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA5", "Warlock", "Human", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA6", "Hunter", "NightElf", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA7", "Rogue", "Human", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA8", "Mage", "Gnome", "Male", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA9", "Warrior", "Dwarf", "Female", BotRunnerType.Background),
        CreateCharacterSetting("WSGBOTA10", "Paladin", "Dwarf", "Male", BotRunnerType.Background),
    ];

    protected override string SettingsFileName => "WarsongGulch.settings.json";

    protected override string FixtureLabel => "WSG";

    protected override uint BattlegroundTypeId => 2;

    protected override uint BattlegroundMapId => WsgMapId;

    protected override int TargetLevel => 10;

    protected override IReadOnlyCollection<string> HordeAccounts => HordeComposition.Select(settings => settings.AccountName).ToArray();

    protected override IReadOnlyCollection<string> AllianceAccounts => AllianceComposition.Select(settings => settings.AccountName).ToArray();

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarWsg.MapId,
        BattlemasterData.OrgrimmarWsg.Position.X,
        BattlemasterData.OrgrimmarWsg.Position.Y,
        BattlemasterData.OrgrimmarWsg.Position.Z + 3f);

    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindWsg.MapId,
        BattlemasterData.StormwindWsg.Position.X,
        BattlemasterData.StormwindWsg.Position.Y,
        BattlemasterData.StormwindWsg.Position.Z + 3f);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
        => HordeComposition.Concat(AllianceComposition).ToArray();
}
