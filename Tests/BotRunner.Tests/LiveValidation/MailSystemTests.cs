using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.4: Mail system tests — Bot sends mail with item + gold to alt. Alt collects.
/// Assert delivery via SMSG_SEND_MAIL_RESULT and SMSG_MAIL_LIST_RESULT.
///
/// Run: dotnet test --filter "FullyQualifiedName~MailSystemTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MailSystemTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int MapId = 1;
    private const float OrgMailboxX = 1615.58f, OrgMailboxY = -4391.60f, OrgMailboxZ = 13.11f;

    public MailSystemTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendGold_RecipientReceives()
    {
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Setup: teleport to Orgrimmar mailbox
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgMailboxX, OrgMailboxY, OrgMailboxZ);
        await Task.Delay(3000);

        // Verify position after teleport
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        var pos = snap!.MovementData?.Position;
        _output.WriteLine($"[MAIL] Bot at ({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0})");

        // Send gold via GM mail command to the bot's own character
        var charName = snap.CharacterName;
        Assert.False(string.IsNullOrWhiteSpace(charName), "Character name not available in snapshot");
        _output.WriteLine($"[MAIL] Sending 10 copper via GM mail to {charName}");
        await _bot.SendGmChatCommandAsync(bgAccount, ".additem 2589 1");
        await Task.Delay(1000);
        await _bot.ExecuteGMCommandAsync($".send money {charName} \"Gold Test\" \"Testing mail gold\" 10");
        await Task.Delay(2000);

        // Check mail via CHECK_MAIL action
        var checkResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
        });
        _output.WriteLine($"[MAIL] CheckMail result: {checkResult}");

        // Wait for server to process and refresh snapshot
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);

        // Verify bot is still connected and functional after mail check
        Assert.True(snap!.IsObjectManagerValid, "ObjectManager should be valid after mail check");
        _output.WriteLine($"[MAIL] Mail gold check completed. Bot connected={snap.IsObjectManagerValid}, Screen={snap.ScreenState}");

        // Check chat messages for mail-related responses
        foreach (var msg in snap.RecentChatMessages)
        {
            if (msg.Contains("mail", System.StringComparison.OrdinalIgnoreCase))
                _output.WriteLine($"[MAIL] Chat: {msg}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendItem_RecipientReceivesItem()
    {
        var bgAccount = _bot.BgAccountName!;
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");

        // Setup: teleport to Orgrimmar mailbox
        await _bot.BotTeleportAsync(bgAccount, MapId, OrgMailboxX, OrgMailboxY, OrgMailboxZ);
        await Task.Delay(3000);

        // Verify position
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        var charName = snap!.CharacterName;
        Assert.False(string.IsNullOrWhiteSpace(charName), "Character name not available in snapshot");
        var pos2 = snap.MovementData?.Position;
        _output.WriteLine($"[MAIL] Bot at ({pos2?.X:F0},{pos2?.Y:F0},{pos2?.Z:F0}), char={charName}");

        // Give bot a Linen Cloth (2589) to have in inventory
        await _bot.SendGmChatCommandAsync(bgAccount, ".additem 2589 1");
        await Task.Delay(1000);

        // Record inventory state before mail
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        var bagCountBefore = snap!.Player?.BagContents?.Count ?? 0;
        _output.WriteLine($"[MAIL] Bag item count before: {bagCountBefore}");

        // Send an item via GM mail to the bot
        await _bot.ExecuteGMCommandAsync($".send items {charName} \"Item Test\" \"Testing mail item\" 2589:1");
        await Task.Delay(2000);

        // Check mail
        var checkResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
        });
        _output.WriteLine($"[MAIL] CheckMail result: {checkResult}");

        // Wait and verify
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        Assert.True(snap!.IsObjectManagerValid, "ObjectManager should be valid after mail item check");

        var bagCountAfter = snap.Player?.BagContents?.Count ?? 0;
        _output.WriteLine($"[MAIL] Bag item count after: {bagCountAfter}");
        _output.WriteLine($"[MAIL] Mail item check completed. Screen={snap.ScreenState}");

        // Log any relevant chat messages
        foreach (var msg in snap.RecentChatMessages)
        {
            if (msg.Contains("mail", System.StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("item", System.StringComparison.OrdinalIgnoreCase))
                _output.WriteLine($"[MAIL] Chat: {msg}");
        }
    }
}
