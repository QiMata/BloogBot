using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for Ragefire Chasm tests.
/// Prep is fixture-owned: clear stale groups, revive, level, learn spells, add gear,
/// teleport to Orgrimmar, GM off. The coordinator stays disabled until prep is complete.
/// </summary>
public class RfcBotFixture : CoordinatorFixtureBase
{
    private static readonly IReadOnlyList<CharacterSettings> Composition =
    [
        CreateCharacterSetting("RFCBOT1", "Warrior", "Orc", "Female", BotRunnerType.Foreground),
        CreateCharacterSetting("RFCBOT2", "Shaman", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT3", "Druid", "Tauren", "Male", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT4", "Priest", "Undead", "Male", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT5", "Warlock", "Undead", "Male", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT6", "Hunter", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT7", "Rogue", "Undead", "Female", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT8", "Mage", "Troll", "Male", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT9", "Warrior", "Orc", "Female", BotRunnerType.Background),
        CreateCharacterSetting("RFCBOT10", "Warrior", "Tauren", "Female", BotRunnerType.Background),
    ];

    protected override string SettingsFileName => "RagefireChasm.settings.json";

    protected override string FixtureLabel => "RFC";

    protected override bool DisableCoordinatorDuringPreparation => true;

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings() => Composition;

    protected override async Task PrepareBotsAsync()
    {
        await EnsureAccountsNotGroupedAsync(AccountNames, "RfcPrepCleanGroup");
        await RefreshSnapshotsAsync();

        var snapshotsByAccount = AllBots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .ToDictionary(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase);

        foreach (var settings in CharacterSettings)
        {
            if (!snapshotsByAccount.TryGetValue(settings.AccountName, out var snapshot)
                || string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                throw new InvalidOperationException($"RFC prep could not resolve the live character for account '{settings.AccountName}'.");
            }

            await ExecuteGMCommandAsync($".revive {snapshot.CharacterName}");
            await ExecuteGMCommandAsync($".character level {snapshot.CharacterName} 8");
        }
        await Task.Delay(2000);

        foreach (var settings in CharacterSettings)
        {
            var characterClass = settings.CharacterClass
                ?? throw new InvalidOperationException($"RFC fixture is missing CharacterClass for '{settings.AccountName}'.");

            await SendGmChatCommandAsync(settings.AccountName, ".reset spells");
            await SendGmChatCommandAsync(settings.AccountName, ".reset talents");
            await SendGmChatCommandAsync(settings.AccountName, ".reset items");

            if (DungeoneeringCoordinator.Level8KeySpells.TryGetValue(characterClass, out var spells))
            {
                foreach (var spellId in spells)
                    await SendGmChatCommandAsync(settings.AccountName, $".learn {spellId}");
            }

            if (DungeoneeringCoordinator.Level8Gear.TryGetValue(characterClass, out var gear))
            {
                foreach (var itemId in gear.Select(item => item.ItemId).Distinct())
                    await SendGmChatCommandAsync(settings.AccountName, $".additem {itemId}");
            }
        }
        await Task.Delay(2000);

        foreach (var accountName in AccountNames)
        {
            await BotTeleportAsync(accountName, 1, 1629.4f, -4373.4f, 37.2f);
            await Task.Delay(200);
        }
        await Task.Delay(3000);

        foreach (var accountName in AccountNames)
            await SendGmChatCommandAsync(accountName, ".gm off");
        await Task.Delay(1000);
    }

    protected override async Task AfterPrepareAsync()
    {
        await base.AfterPrepareAsync();
        await RefreshSnapshotsAsync();
    }
}
