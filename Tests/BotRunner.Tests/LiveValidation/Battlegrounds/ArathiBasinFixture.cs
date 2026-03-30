using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Arathi Basin tests. 30 bots: 15 Horde (1 FG + 14 BG) + 15 Alliance (15 BG).
/// </summary>
public class ArathiBasinFixture : LiveBotFixture, IAsyncLifetime
{
    public const int HordeBotCount = 15;
    public const int AllianceBotCount = 15;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint AbMapId = 529;

    private static readonly string[] HordeClasses = ["Warrior", "Shaman", "Druid", "Priest", "Warlock", "Hunter", "Rogue", "Mage", "Warrior", "Warrior", "Shaman", "Priest", "Warlock", "Hunter", "Rogue"];
    private static readonly string[] HordeRaces = ["Orc", "Orc", "Tauren", "Undead", "Undead", "Orc", "Undead", "Troll", "Orc", "Tauren", "Orc", "Undead", "Undead", "Troll", "Undead"];
    private static readonly string[] AllianceClasses = ["Warrior", "Paladin", "Druid", "Priest", "Warlock", "Hunter", "Rogue", "Mage", "Warrior", "Warrior", "Paladin", "Priest", "Warlock", "Hunter", "Rogue"];
    private static readonly string[] AllianceRaces = ["Human", "Human", "NightElf", "Human", "Human", "NightElf", "Human", "Gnome", "Human", "Dwarf", "Dwarf", "NightElf", "Gnome", "Dwarf", "NightElf"];

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
            bots.Add(new
            {
                AccountName = i == 0 ? "TESTBOT1" : $"ABBOT{i + 1}",
                CharacterClass = HordeClasses[i],
                CharacterRace = HordeRaces[i],
                CharacterGender = i % 2 == 0 ? "Female" : "Male",
                GmLevel = 6, Openness = 0.7, Conscientiousness = 0.85, Extraversion = 0.6,
                Agreeableness = 0.8, Neuroticism = 0.3, ShouldRun = true,
                RunnerType = i == 0 ? "Foreground" : "Background",
            });
        }
        for (int i = 0; i < AllianceBotCount; i++)
        {
            bots.Add(new
            {
                AccountName = $"ABBOTA{i + 1}",
                CharacterClass = AllianceClasses[i],
                CharacterRace = AllianceRaces[i],
                CharacterGender = i % 2 == 0 ? "Female" : "Male",
                GmLevel = 6, Openness = 0.7, Conscientiousness = 0.85, Extraversion = 0.6,
                Agreeableness = 0.8, Neuroticism = 0.3, ShouldRun = true,
                RunnerType = "Background",
            });
        }

        var json = JsonSerializer.Serialize(bots, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.Combine(Path.GetTempPath(), "WWoW", "TestSettings");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ArathiBasin.settings.json");
        File.WriteAllText(path, json);
        return path;
    }
}

[CollectionDefinition(Name)]
public class ArathiBasinCollection : ICollectionFixture<ArathiBasinFixture>
{ public const string Name = "ArathiBasinValidation"; }
