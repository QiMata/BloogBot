using System;
using System.Linq;
using System.Threading.Tasks;
using WoWStateManager.Coordination;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Fixture for Ragefire Chasm tests. Two modes:
///
/// 1. **Fixture-prep mode** (WWOW_COORDINATOR_SKIP_PREP=1):
///    Fixture does all prep (level, spells, gear, teleport to Org) via SOAP + bot chat.
///    Coordinator starts at FormGroup_Inviting. Matches BattlegroundCoordinator pattern.
///
/// 2. **Coordinator-prep mode** (default, backward compatible):
///    Coordinator handles everything from WaitingForBots to DungeonInProgress.
///    Fixture just launches StateManager.
///
/// Set <see cref="UseFixturePrep"/> = true to enable fixture-prep mode.
/// </summary>
public class RfcBotFixture : LiveBotFixture, IAsyncLifetime
{
    /// <summary>When true, fixture does prep and coordinator skips to FormGroup.</summary>
    public bool UseFixturePrep { get; set; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Enable coordinator — it owns group lifecycle, so skip fixture group cleanup
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
        Environment.SetEnvironmentVariable("WWOW_COORDINATOR_SKIP_PREP",
            UseFixturePrep ? "1" : "0");
        SkipGroupCleanup = true;

        // Set RFC settings before launching StateManager
        var settingsPath = ResolveTestSettingsPath("RagefireChasm.settings.json");
        if (settingsPath != null)
            SetCustomSettingsPath(settingsPath);

        await base.InitializeAsync();
    }

    /// <summary>
    /// Run fixture-owned prep: revive, level to 8, learn spells, add gear, teleport to Orgrimmar.
    /// Only needed when UseFixturePrep = true. Uses data from DungeoneeringCoordinator.Level8KeySpells
    /// and Level8Gear to match what the coordinator would do.
    /// </summary>
    public async Task PrepareBotsForRfcAsync()
    {
        if (!IsReady || AllBots.Count == 0) return;

        // Step 1: Revive + level via SOAP
        foreach (var snap in AllBots)
        {
            await ExecuteGMCommandAsync($".revive {snap.CharacterName}");
            await ExecuteGMCommandAsync($".character level {snap.CharacterName} 8");
        }
        await Task.Delay(2000);

        // Step 2: Learn spells + add gear via bot chat
        foreach (var snap in AllBots)
        {
            var charClass = snap.Player?.Unit?.GameObject?.Name ?? "Warrior";
            // Reset spells/talents/items first
            await SendGmChatCommandAsync(snap.AccountName, ".reset spells");
            await SendGmChatCommandAsync(snap.AccountName, ".reset talents");
            await SendGmChatCommandAsync(snap.AccountName, ".reset items");

            // Learn class spells
            if (DungeoneeringCoordinator.Level8KeySpells.TryGetValue(charClass, out var spells))
            {
                foreach (var spellId in spells)
                    await SendGmChatCommandAsync(snap.AccountName, $".learn {spellId}");
            }

            // Add gear
            if (DungeoneeringCoordinator.Level8Gear.TryGetValue(charClass, out var gear))
            {
                foreach (var (itemId, _) in gear)
                    await SendGmChatCommandAsync(snap.AccountName, $".additem {itemId}");
            }
        }
        await Task.Delay(2000);

        // Step 3: Teleport to Orgrimmar (safe zone for group formation)
        foreach (var snap in AllBots)
        {
            await BotTeleportAsync(snap.AccountName, 1, 1629.4f, -4373.4f, 37.2f);
            await Task.Delay(200);
        }
        await Task.Delay(3000);

        // Step 4: Turn GM off
        foreach (var snap in AllBots)
            await SendGmChatCommandAsync(snap.AccountName, ".gm off");
        await Task.Delay(1000);
    }
}
