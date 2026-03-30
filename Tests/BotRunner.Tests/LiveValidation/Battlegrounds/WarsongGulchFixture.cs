using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Warsong Gulch tests. Launches 20 bots: 10 Horde (1 FG + 9 BG) + 10 Alliance (10 BG).
/// Both sides form raid, queue for WSG, and enter the battleground.
/// </summary>
public class WarsongGulchFixture : LiveBotFixture, IAsyncLifetime
{
    public const int HordeBotCount = 10;
    public const int AllianceBotCount = 10;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;

    /// <summary>WSG map ID.</summary>
    public const uint WsgMapId = 489;

    // Horde composition (Orgrimmar battlemasters)
    private static readonly (string Account, string Class, string Race, string Gender, string Runner)[] HordeComposition =
    [
        ("TESTBOT1", "Warrior", "Orc", "Female", "Foreground"),    // FG raid leader
        ("WSGBOT2", "Shaman", "Orc", "Female", "Background"),
        ("WSGBOT3", "Druid", "Tauren", "Male", "Background"),
        ("WSGBOT4", "Priest", "Undead", "Male", "Background"),
        ("WSGBOT5", "Warlock", "Undead", "Male", "Background"),
        ("WSGBOT6", "Hunter", "Orc", "Female", "Background"),
        ("WSGBOT7", "Rogue", "Undead", "Female", "Background"),
        ("WSGBOT8", "Mage", "Troll", "Male", "Background"),
        ("WSGBOT9", "Warrior", "Orc", "Female", "Background"),
        ("WSGBOT10", "Warrior", "Tauren", "Female", "Background"),
    ];

    // Alliance composition (Stormwind battlemasters)
    private static readonly (string Account, string Class, string Race, string Gender, string Runner)[] AllianceComposition =
    [
        ("WSGBOTA1", "Warrior", "Human", "Female", "Background"),  // Alliance raid leader
        ("WSGBOTA2", "Paladin", "Human", "Male", "Background"),
        ("WSGBOTA3", "Druid", "NightElf", "Male", "Background"),
        ("WSGBOTA4", "Priest", "Human", "Male", "Background"),
        ("WSGBOTA5", "Warlock", "Human", "Male", "Background"),
        ("WSGBOTA6", "Hunter", "NightElf", "Female", "Background"),
        ("WSGBOTA7", "Rogue", "Human", "Female", "Background"),
        ("WSGBOTA8", "Mage", "Gnome", "Male", "Background"),
        ("WSGBOTA9", "Warrior", "Human", "Female", "Background"),
        ("WSGBOTA10", "Warrior", "Dwarf", "Female", "Background"),
    ];

    async Task IAsyncLifetime.InitializeAsync()
    {
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        SkipGroupCleanup = true;

        var settingsPath = GenerateSettingsFile();
        SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }

    private string GenerateSettingsFile()
    {
        var bots = new List<object>();

        foreach (var (account, cls, race, gender, runner) in HordeComposition)
        {
            bots.Add(new
            {
                AccountName = account,
                CharacterClass = cls,
                CharacterRace = race,
                CharacterGender = gender,
                GmLevel = 6,
                Openness = 0.7,
                Conscientiousness = 0.85,
                Extraversion = 0.6,
                Agreeableness = 0.8,
                Neuroticism = 0.3,
                ShouldRun = true,
                RunnerType = runner,
            });
        }

        foreach (var (account, cls, race, gender, runner) in AllianceComposition)
        {
            bots.Add(new
            {
                AccountName = account,
                CharacterClass = cls,
                CharacterRace = race,
                CharacterGender = gender,
                GmLevel = 6,
                Openness = 0.7,
                Conscientiousness = 0.85,
                Extraversion = 0.6,
                Agreeableness = 0.8,
                Neuroticism = 0.3,
                ShouldRun = true,
                RunnerType = runner,
            });
        }

        var json = JsonSerializer.Serialize(bots, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.Combine(Path.GetTempPath(), "WWoW", "TestSettings");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "WarsongGulch.settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    public string[] HordeAccounts => HordeComposition.Select(c => c.Account).ToArray();
    public string[] AllianceAccounts => AllianceComposition.Select(c => c.Account).ToArray();
}
