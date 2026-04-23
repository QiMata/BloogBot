using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Microsoft.Extensions.Logging;

namespace BotRunner.Tests.LiveValidation;

public partial class LiveBotFixture
{
    private static readonly (string SlotName, int ItemId)[] ShodanMageAdminLoadout =
    {
        ("Head",       22498), // Frostfire Circlet
        ("Neck",       23058), // Life Channeling Necklace
        ("Shoulders",  22499), // Frostfire Shoulderpads
        ("Back",       22731), // Cloak of the Devoured
        ("Chest",      22496), // Frostfire Robe
        ("Wrist",      22503), // Frostfire Bindings
        ("Hands",      22501), // Frostfire Gloves
        ("Waist",      22502), // Frostfire Belt
        ("Legs",       22497), // Frostfire Leggings
        ("Feet",       22500), // Frostfire Sandals
        ("Finger1",    23062), // Frostfire Ring
        ("Finger2",    23031), // Band of the Inevitable
        ("Trinket1",   23046), // The Restrained Essence of Sapphiron
        ("Trinket2",   19379), // Neltharion's Tear (BWL)
        ("MainHand",   22589), // Atiesh, Greatstaff of the Guardian (Mage)
        ("Ranged",     22820), // Wand of Fates (Naxx)
    };

    private static int CountBagItemById(Game.WoWPlayer? player, uint itemId)
        => player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    /// <summary>
    /// Dedicated admin-bot loadout path for the Ratchet fishing fixture.
    /// Shodan is reset to a clean inventory/equipment state, given a valid mage wand,
    /// and every configured item is explicitly equipped through the normal bot action path.
    /// </summary>
    public async Task EnsureShodanAdminLoadoutAsync(string shodanAccountName, string? shodanCharacterName = null)
    {
        if (string.IsNullOrWhiteSpace(shodanAccountName))
            throw new InvalidOperationException("Shodan account name is required.");

        const uint wandProficiencySpellId = 5019;

        if (!string.IsNullOrWhiteSpace(shodanCharacterName))
        {
            var levelResult = await ExecuteGMCommandAsync($".character level {shodanCharacterName} 60");
            _logger.LogInformation("[SHODAN-LOADOUT] .character level 60 -> {Result}", levelResult);

            var resetResult = await ExecuteGMCommandAsync($".reset items {shodanCharacterName}");
            _logger.LogInformation("[SHODAN-LOADOUT] .reset items {Character} -> {Result}", shodanCharacterName, resetResult);
        }
        else
        {
            await BotSelectSelfAsync(shodanAccountName);
            await Task.Delay(300);
            await SendGmChatCommandAndAwaitServerAckAsync(shodanAccountName, ".character level 60");
            await SendGmChatCommandAndAwaitServerAckAsync(shodanAccountName, ".reset items");
        }

        var resetVerified = await WaitForSnapshotConditionAsync(
            shodanAccountName,
            snapshot => (snapshot.Player?.BagContents?.Count ?? 0) == 0,
            TimeSpan.FromSeconds(10),
            pollIntervalMs: 250,
            progressLabel: "SHODAN reset-items");
        if (!resetVerified)
        {
            throw new InvalidOperationException(
                $"Shodan inventory reset never settled for account '{shodanAccountName}'.");
        }

        await BotLearnSpellAsync(shodanAccountName, wandProficiencySpellId);

        foreach (var (slotName, itemId) in ShodanMageAdminLoadout)
        {
            var addAck = await SendGmChatCommandAndAwaitServerAckAsync(
                shodanAccountName,
                string.Create(CultureInfo.InvariantCulture, $".additem {itemId} 1"));
            if (!addAck)
            {
                throw new InvalidOperationException(
                    $"Shodan loadout add-item ack failed for slot '{slotName}' item {itemId}.");
            }

            var itemAdded = await WaitForSnapshotConditionAsync(
                shodanAccountName,
                snapshot => CountBagItemById(snapshot.Player, (uint)itemId) > 0,
                TimeSpan.FromSeconds(6),
                pollIntervalMs: 250,
                progressLabel: $"SHODAN add {slotName}");
            if (!itemAdded)
            {
                throw new InvalidOperationException(
                    $"Shodan never observed item {itemId} in bags for slot '{slotName}'.");
            }

            var equipResult = await SendActionAsync(
                shodanAccountName,
                new ActionMessage
                {
                    ActionType = ActionType.EquipItem,
                    Parameters = { new RequestParameter { IntParam = itemId } }
                },
                emitOutput: false);
            if (equipResult != ResponseResult.Success)
            {
                throw new InvalidOperationException(
                    $"Shodan equip dispatch failed for slot '{slotName}' item {itemId}: {equipResult}.");
            }

            var equipped = await WaitForSnapshotConditionAsync(
                shodanAccountName,
                snapshot => CountBagItemById(snapshot.Player, (uint)itemId) == 0,
                TimeSpan.FromSeconds(8),
                pollIntervalMs: 250,
                progressLabel: $"SHODAN equip {slotName}");
            if (!equipped)
            {
                await RefreshSnapshotsAsync();
                var snap = await GetSnapshotAsync(shodanAccountName);
                throw new InvalidOperationException(
                    $"Shodan never equipped slot '{slotName}' item {itemId}. " +
                    $"BagCount={CountBagItemById(snap?.Player, (uint)itemId)}.");
            }
        }

        _logger.LogInformation(
            "[SHODAN-LOADOUT] Added and equipped {Count} BIS items for '{Account}'.",
            ShodanMageAdminLoadout.Length,
            shodanAccountName);
    }
}
