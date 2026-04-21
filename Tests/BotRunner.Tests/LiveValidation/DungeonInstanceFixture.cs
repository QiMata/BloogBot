using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner;
using BotRunner.Travel;
using Communication;
using GameData.Core.Enums;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Reusable fixture for dungeon and raid instance entry tests. Generates a settings JSON for the
/// specified instance with 10 Horde bots and prepares missing accounts/characters before launch.
/// </summary>
public class DungeonInstanceFixture : LiveBotFixture, IAsyncLifetime
{
    private const string InjectionDisablePacketHooksEnvVar = "Injection__DisablePacketHooks";
    private const string DisablePacketHooksEnvVar = "WWOW_DISABLE_PACKET_HOOKS";
    private const int MaxCharacterNameAttemptOffset = 128;

    /// <summary>
    /// The dungeon definition for this test instance. Set by derived fixture constructors.
    /// </summary>
    public DungeonEntryData.DungeonDefinition? Dungeon { get; protected set; }

    /// <summary>
    /// Account prefix for this dungeon (for example, "WCBOT" for Wailing Caverns).
    /// BG-led fixtures use a dedicated <prefix>1 leader account so they do not depend on TESTBOT1.
    /// </summary>
    protected string AccountPrefix { get; set; } = "DUNBOT";

    /// <summary>Number of bots (default 10).</summary>
    protected int BotCount { get; set; } = 10;

    /// <summary>
    /// Dungeon/raid entry coverage is coordinator- and server-driven, so the leader stays BG
    /// unless a specific fixture opts back into foreground coverage.
    /// </summary>
    protected virtual bool UseForegroundLeader => false;

    protected virtual string LeaderAccountName =>
        ResolveLeaderAccountName(AccountPrefix, UseForegroundLeader);

    internal static string ResolveLeaderAccountName(string accountPrefix, bool useForegroundLeader) =>
        useForegroundLeader ? "TESTBOT1" : $"{accountPrefix}1";

    internal static string ResolveAccountName(int index, string accountPrefix, bool useForegroundLeader) =>
        index == 0 ? ResolveLeaderAccountName(accountPrefix, useForegroundLeader) : $"{accountPrefix}{index + 1}";

    internal static string ResolveRunnerType(int index, bool useForegroundLeader) =>
        index == 0 && useForegroundLeader ? "Foreground" : "Background";

    internal static string ResolveExecutionLabel(int index, bool useForegroundLeader) =>
        ResolveRunnerType(index, useForegroundLeader) == "Foreground" ? "FG" : "BG";

    /// <summary>
    /// Standard 10-bot Horde composition for dungeon tests.
    /// Slot 1 is the coordinator leader; all slots run headless by default.
    /// </summary>
    private static readonly (string Class, string Race, string Gender)[] DefaultComposition =
    [
        ("Warrior", "Orc", "Female"),      // 1: Leader / Main Tank
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

        var characterSettings = BuildCharacterSettings();
        await EnsureAccountsAndCharactersReadyForLaunchAsync(characterSettings);

        ConfigureCoordinatorEnvironment();
        SkipGroupCleanup = true;

        SetCustomSettingsPath(GenerateSettingsFile(characterSettings));
        await base.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", null);
        Environment.SetEnvironmentVariable(InjectionDisablePacketHooksEnvVar, null);
        Environment.SetEnvironmentVariable(DisablePacketHooksEnvVar, null);
        await base.DisposeAsync();
    }

    protected virtual void ConfigureCoordinatorEnvironment()
    {
        // Foreground packet hooks are unstable during dungeon and raid map transfers.
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        Environment.SetEnvironmentVariable(InjectionDisablePacketHooksEnvVar, "true");
        Environment.SetEnvironmentVariable(DisablePacketHooksEnvVar, "1");
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar, Dungeon!.Abbreviation);
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar, Dungeon.InstanceMapId.ToString());
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetXEnvVar, Dungeon.InstanceEntryPosition.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetYEnvVar, Dungeon.InstanceEntryPosition.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetZEnvVar, Dungeon.InstanceEntryPosition.Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private string GenerateSettingsFile(IReadOnlyList<CharacterSettings> characterSettings) =>
        CoordinatorFixtureBase.WriteSettingsFile(characterSettings, $"{Dungeon!.Abbreviation}.settings.json");

    /// <summary>
    /// All account names for this dungeon test.
    /// </summary>
    public string[] GetAccountNames()
    {
        var count = Math.Min(BotCount, DefaultComposition.Length);
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = ResolveAccountName(i, AccountPrefix, UseForegroundLeader);
        return names;
    }

    public string GetAccountExecutionLabel(int index) =>
        ResolveExecutionLabel(index, UseForegroundLeader);

    internal Task<ResponseResult> SetCoordinatorEnabledForEntrySetupAsync(bool enabled) =>
        SetCoordinatorEnabledAsync(enabled);

    private IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var settings = new List<CharacterSettings>();
        var count = Math.Min(BotCount, DefaultComposition.Length);

        for (int i = 0; i < count; i++)
        {
            var (characterClass, characterRace, characterGender) = DefaultComposition[i];
            var accountName = ResolveAccountName(i, AccountPrefix, UseForegroundLeader);
            var runnerType = ResolveRunnerType(i, UseForegroundLeader) == "Foreground"
                ? WoWStateManager.Settings.BotRunnerType.Foreground
                : WoWStateManager.Settings.BotRunnerType.Background;

            settings.Add(CoordinatorFixtureBase.CreateCharacterSetting(
                accountName,
                characterClass,
                characterRace,
                characterGender,
                runnerType));
        }

        return settings;
    }

    private async Task EnsureAccountsAndCharactersReadyForLaunchAsync(IReadOnlyList<CharacterSettings> characterSettings)
    {
        var accountsNeedingCharacters = new List<CharacterSettings>();

        foreach (var settings in characterSettings)
        {
            if (!await AccountExistsAsync(settings.AccountName))
            {
                _ = await ExecuteGMCommandWithRetryAsync($".account create {settings.AccountName} PASSWORD");

                var accountCreated = await CoordinatorFixtureBase.WaitForConditionAsync(
                    () => AccountExistsAsync(settings.AccountName),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromMilliseconds(500));
                Assert.True(accountCreated, $"Account '{settings.AccountName}' was not created before launch.");

                await Task.Delay(250);
            }

            var existingCharacters = (await QueryCharactersForAccountAsync(settings.AccountName)).ToArray();
            if (existingCharacters.Length == 0)
            {
                accountsNeedingCharacters.Add(settings);
                settings.CharacterNameAttemptOffset = null;
                continue;
            }

            if (CoordinatorFixtureBase.CanReuseExistingCharacters(settings, existingCharacters))
            {
                settings.CharacterNameAttemptOffset = null;
                continue;
            }

            foreach (var existingCharacter in existingCharacters)
            {
                _ = await ExecuteGMCommandWithRetryAsync($".character erase {existingCharacter.Name}");
                await Task.Delay(250);
            }

            var cleared = await CoordinatorFixtureBase.WaitForConditionAsync(
                async () => (await QueryCharactersForAccountAsync(settings.AccountName)).Count == 0,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(500));
            Assert.True(cleared, $"Account '{settings.AccountName}' still had stale characters after cleanup.");

            accountsNeedingCharacters.Add(settings);
            settings.CharacterNameAttemptOffset = null;
        }

        if (accountsNeedingCharacters.Count == 0)
            return;

        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var settings in accountsNeedingCharacters)
        {
            var reserved = false;
            for (var attemptOffset = 0; attemptOffset <= MaxCharacterNameAttemptOffset; attemptOffset++)
            {
                var candidateName = BuildGeneratedCharacterName(settings, attemptOffset);
                if (reservedNames.Contains(candidateName))
                    continue;

                if (await CharacterNameExistsAsync(candidateName))
                    continue;

                settings.CharacterNameAttemptOffset = attemptOffset == 0 ? null : attemptOffset;
                reservedNames.Add(candidateName);
                reserved = true;
                break;
            }

            Assert.True(reserved, $"Unable to reserve an unused generated name for '{settings.AccountName}'.");
        }
    }

    private static string BuildGeneratedCharacterName(CharacterSettings settings, int attemptOffset)
    {
        var characterClass = ResolveCharacterClass(settings);
        var race = ResolveCharacterRace(settings);
        var gender = ResolveCharacterGender(settings, characterClass);
        var seed = BotRunnerService.BuildCharacterUniquenessSeed(settings.AccountName, 0, attemptOffset);
        return WoWNameGenerator.GenerateName(race, gender, seed);
    }

    private static Class ResolveCharacterClass(CharacterSettings settings)
    {
        if (TryParseConfiguredEnum<Class>(settings.CharacterClass, out var configuredClass))
            return configuredClass;

        if (!string.IsNullOrWhiteSpace(settings.AccountName) && settings.AccountName.Length >= 4)
        {
            try
            {
                return WoWNameGenerator.ParseClassCode(settings.AccountName.Substring(2, 2));
            }
            catch (ArgumentException)
            {
            }
        }

        return Class.Warrior;
    }

    private static Race ResolveCharacterRace(CharacterSettings settings)
    {
        if (TryParseConfiguredEnum<Race>(settings.CharacterRace, out var configuredRace))
            return configuredRace;

        if (!string.IsNullOrWhiteSpace(settings.AccountName) && settings.AccountName.Length >= 2)
        {
            try
            {
                return WoWNameGenerator.ParseRaceCode(settings.AccountName[..2]);
            }
            catch (ArgumentException)
            {
            }
        }

        return Race.Orc;
    }

    private static Gender ResolveCharacterGender(CharacterSettings settings, Class characterClass)
    {
        if (TryParseConfiguredEnum<Gender>(settings.CharacterGender, out var configuredGender))
            return configuredGender;

        return WoWNameGenerator.DetermineGender(characterClass);
    }

    private static bool TryParseConfiguredEnum<TEnum>(string? configuredValue, out TEnum parsedValue)
        where TEnum : struct, Enum
    {
        parsedValue = default;
        if (string.IsNullOrWhiteSpace(configuredValue))
            return false;

        var token = new string(configuredValue.Where(char.IsLetterOrDigit).ToArray());
        return Enum.TryParse(token, ignoreCase: true, out parsedValue);
    }
}
