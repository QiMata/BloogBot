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
    // Reduced to 10 bots (5v5) to prevent test host OOM crash.
    // VMaNGOS min_players_per_team=4 for WSG, so 5v5 is sufficient.
    public const int HordeBotCount = 5;
    public const int AllianceBotCount = 5;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;

    /// <summary>WSG map ID.</summary>
    public const uint WsgMapId = 489;

    // Horde composition (Orgrimmar battlemasters) — 5 BG bots, no FG
    private static readonly (string Account, string Class, string Race, string Gender, string Runner)[] HordeComposition =
    [
        ("WSGBOT2", "Shaman", "Orc", "Female", "Background"),
        ("WSGBOT3", "Druid", "Tauren", "Male", "Background"),
        ("WSGBOT4", "Priest", "Undead", "Male", "Background"),
        ("WSGBOT5", "Warlock", "Undead", "Male", "Background"),
        ("WSGBOT6", "Hunter", "Orc", "Female", "Background"),
    ];

    // Alliance composition (Stormwind battlemasters) — 5 BG bots
    private static readonly (string Account, string Class, string Race, string Gender, string Runner)[] AllianceComposition =
    [
        ("WSGBOTA1", "Warrior", "Human", "Female", "Background"),
        ("WSGBOTA2", "Paladin", "Human", "Male", "Background"),
        ("WSGBOTA3", "Druid", "NightElf", "Male", "Background"),
        ("WSGBOTA4", "Priest", "Human", "Male", "Background"),
        ("WSGBOTA5", "Warlock", "Human", "Male", "Background"),
    ];

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Coordinator is enabled — it waits for level>=10 before queueing.
        // Test teleports FIRST, then levels LAST, so coordinator doesn't start
        // until bots are at battlemaster positions.
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", "battleground");
        Environment.SetEnvironmentVariable("WWOW_BG_TYPE", "2");  // WSG
        Environment.SetEnvironmentVariable("WWOW_BG_MAP", "489"); // WSG map
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
