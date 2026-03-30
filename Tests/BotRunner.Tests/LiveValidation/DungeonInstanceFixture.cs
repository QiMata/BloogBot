using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BotRunner.Travel;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Reusable fixture for dungeon instance tests. Generates a settings JSON for the
/// specified dungeon with 10 bots (1 FG + 9 BG), all Horde race/class mix.
/// Extends LiveBotFixture to get StateManager lifecycle, snapshot polling, and GM commands.
///
/// Usage: Create a concrete fixture class per dungeon that sets DungeonDefinition in ctor,
/// then use with xUnit [Collection] + ICollectionFixture.
/// </summary>
public class DungeonInstanceFixture : LiveBotFixture, IAsyncLifetime
{
    /// <summary>
    /// The dungeon definition for this test instance. Set by derived fixture constructors.
    /// </summary>
    public DungeonEntryData.DungeonDefinition? Dungeon { get; protected set; }

    /// <summary>
    /// Account prefix for this dungeon (e.g., "WCBOT" for Wailing Caverns).
    /// TESTBOT1 is always the FG raid leader.
    /// </summary>
    protected string AccountPrefix { get; set; } = "DUNBOT";

    /// <summary>Number of bots (default 10: 1 FG + 9 BG).</summary>
    protected int BotCount { get; set; } = 10;

    /// <summary>
    /// Standard 10-bot Horde composition for dungeon tests.
    /// Slot 1 (FG): Warrior tank. Slots 2-10 (BG): mixed classes.
    /// </summary>
    private static readonly (string Class, string Race, string Gender)[] DefaultComposition =
    [
        ("Warrior", "Orc", "Female"),      // 1: FG — Main Tank
        ("Shaman", "Orc", "Female"),       // 2: Off-Tank / Healer hybrid
        ("Druid", "Tauren", "Male"),       // 3: Healer
        ("Priest", "Undead", "Male"),      // 4: Healer
        ("Warlock", "Undead", "Male"),     // 5: DPS (has summoning)
        ("Hunter", "Orc", "Female"),       // 6: DPS
        ("Rogue", "Undead", "Female"),     // 7: DPS
        ("Mage", "Troll", "Male"),         // 8: DPS
        ("Warrior", "Orc", "Female"),      // 9: DPS
        ("Warrior", "Tauren", "Female"),   // 10: DPS
    ];

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (Dungeon == null)
            throw new InvalidOperationException("DungeonDefinition must be set before initialization");

        // Enable coordinator for dungeon lifecycle
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        SkipGroupCleanup = true;

        // Generate and set the settings file
        var settingsPath = GenerateSettingsFile();
        SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }

    /// <summary>
    /// Generates a temporary settings JSON file for this dungeon's bot composition.
    /// </summary>
    private string GenerateSettingsFile()
    {
        var bots = new List<object>();
        var count = Math.Min(BotCount, DefaultComposition.Length);

        for (int i = 0; i < count; i++)
        {
            var (cls, race, gender) = DefaultComposition[i];
            var accountName = i == 0 ? "TESTBOT1" : $"{AccountPrefix}{i + 1}";
            var runnerType = i == 0 ? "Foreground" : "Background";

            bots.Add(new
            {
                AccountName = accountName,
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
                RunnerType = runnerType,
            });
        }

        var json = JsonSerializer.Serialize(bots, new JsonSerializerOptions { WriteIndented = true });
        var dir = Path.Combine(Path.GetTempPath(), "WWoW", "TestSettings");
        Directory.CreateDirectory(dir);
        var fileName = $"{Dungeon!.Abbreviation}.settings.json";
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>
    /// All account names for this dungeon test.
    /// </summary>
    public string[] GetAccountNames()
    {
        var count = Math.Min(BotCount, DefaultComposition.Length);
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = i == 0 ? "TESTBOT1" : $"{AccountPrefix}{i + 1}";
        return names;
    }
}
