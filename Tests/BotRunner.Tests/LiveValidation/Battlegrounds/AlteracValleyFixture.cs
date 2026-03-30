using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Alterac Valley tests. 80 bots: 40 Horde (1 FG + 39 BG) + 40 Alliance (40 BG).
/// This is the largest-scale test — validates 80 concurrent bot connections.
/// </summary>
public class AlteracValleyFixture : LiveBotFixture, IAsyncLifetime
{
    public const int HordeBotCount = 40;
    public const int AllianceBotCount = 40;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AvMapId = 30;

    // Cycle through valid Horde race/class combos
    private static readonly (string Class, string Race)[] HordeTemplates =
    [
        ("Warrior", "Orc"), ("Shaman", "Orc"), ("Druid", "Tauren"), ("Priest", "Undead"),
        ("Warlock", "Undead"), ("Hunter", "Orc"), ("Rogue", "Undead"), ("Mage", "Troll"),
        ("Warrior", "Tauren"), ("Hunter", "Troll"), ("Shaman", "Troll"), ("Mage", "Undead"),
    ];

    private static readonly (string Class, string Race)[] AllianceTemplates =
    [
        ("Warrior", "Human"), ("Paladin", "Human"), ("Druid", "NightElf"), ("Priest", "Human"),
        ("Warlock", "Human"), ("Hunter", "NightElf"), ("Rogue", "Human"), ("Mage", "Gnome"),
        ("Warrior", "Dwarf"), ("Hunter", "Dwarf"), ("Paladin", "Dwarf"), ("Priest", "NightElf"),
    ];

    async Task IAsyncLifetime.InitializeAsync()
    {
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        SkipGroupCleanup = true;
        SetCustomSettingsPath(GenerateSettingsFile());
        await base.InitializeAsync();
    }

    private string GenerateSettingsFile()
    {
        var bots = new List<object>();

        for (int i = 0; i < HordeBotCount; i++)
        {
            var template = HordeTemplates[i % HordeTemplates.Length];
            bots.Add(new
            {
                AccountName = i == 0 ? "TESTBOT1" : $"AVBOT{i + 1}",
                CharacterClass = template.Class,
                CharacterRace = template.Race,
                CharacterGender = i % 2 == 0 ? "Female" : "Male",
                GmLevel = 6, Openness = 0.7, Conscientiousness = 0.85, Extraversion = 0.6,
                Agreeableness = 0.8, Neuroticism = 0.3, ShouldRun = true,
                RunnerType = i == 0 ? "Foreground" : "Background",
            });
        }

        for (int i = 0; i < AllianceBotCount; i++)
        {
            var template = AllianceTemplates[i % AllianceTemplates.Length];
            bots.Add(new
            {
                AccountName = $"AVBOTA{i + 1}",
                CharacterClass = template.Class,
                CharacterRace = template.Race,
                CharacterGender = i % 2 == 0 ? "Female" : "Male",
                GmLevel = 6, Openness = 0.7, Conscientiousness = 0.85, Extraversion = 0.6,
                Agreeableness = 0.8, Neuroticism = 0.3, ShouldRun = true,
                RunnerType = "Background",
            });
        }

        var json = JsonSerializer.Serialize(bots, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.Combine(Path.GetTempPath(), "WWoW", "TestSettings");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "AlteracValley.settings.json");
        File.WriteAllText(path, json);
        return path;
    }
}

[CollectionDefinition(Name)]
public class AlteracValleyCollection : ICollectionFixture<AlteracValleyFixture>
{ public const string Name = "AlteracValleyValidation"; }
