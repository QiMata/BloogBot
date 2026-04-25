using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed mail system baselines. SHODAN stages mailbox location and
/// SOAP mail payloads; the BG action target dispatches only CheckMail.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MailSystemTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint LinenClothItemId = LiveBotFixture.TestItems.LinenCloth;

    public MailSystemTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendGold_RecipientReceives()
    {
        await EnsureEconomySettingsAsync();
        var target = ResolveMailActionTarget();

        var mailboxGuid = await StageMailboxAndFindGuidAsync(target);
        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        var coinageBefore = before?.Player?.Coinage ?? 0;

        await _bot.StageBotRunnerMailboxMoneyAsync(
            target.AccountName,
            target.RoleLabel,
            copper: 10,
            subject: "Gold Test",
            body: "Testing mail gold");

        var checkResult = await SendCheckMailAsync(target, mailboxGuid);
        Assert.Equal(ResponseResult.Success, checkResult);

        var coinageIncreased = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snap => (snap.Player?.Coinage ?? 0) > coinageBefore,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} mail-gold-coinage");
        Assert.True(coinageIncreased, $"{target.RoleLabel}: coinage should increase after collecting mail gold.");

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(after);
        Assert.True(after!.IsObjectManagerValid, "ObjectManager should be valid after mail gold check.");
        _output.WriteLine($"[MAIL] {target.RoleLabel} coinage {coinageBefore}->{after.Player?.Coinage ?? 0}");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendItem_RecipientReceivesItem()
    {
        await EnsureEconomySettingsAsync();
        var target = ResolveMailActionTarget();

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: true);

        var mailboxGuid = await StageMailboxAndFindGuidAsync(target, cleanSlate: false);
        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        var itemCountBefore = CountItemSlots(before, LinenClothItemId);

        await _bot.StageBotRunnerMailboxItemAsync(
            target.AccountName,
            target.RoleLabel,
            LinenClothItemId,
            count: 1,
            subject: "Item Test",
            body: "Testing mail item");

        var checkResult = await SendCheckMailAsync(target, mailboxGuid);
        Assert.Equal(ResponseResult.Success, checkResult);

        var itemReceived = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snap => CountItemSlots(snap, LinenClothItemId) >= itemCountBefore + 1,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} mail-item-received");
        Assert.True(itemReceived, $"{target.RoleLabel}: Linen Cloth should appear after collecting item mail.");

        await _bot.RefreshSnapshotsAsync();
        var after = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(after);
        Assert.True(after!.IsObjectManagerValid, "ObjectManager should be valid after mail item check.");
        _output.WriteLine($"[MAIL] {target.RoleLabel} Linen Cloth {itemCountBefore}->{CountItemSlots(after, LinenClothItemId)}");
    }

    private async Task<ulong> StageMailboxAndFindGuidAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        bool cleanSlate = true)
    {
        var staged = await _bot.StageBotRunnerAtOrgrimmarMailboxAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate);
        Assert.True(staged, $"{target.RoleLabel}: expected mailbox staging with visible mailbox object.");

        var mailboxGuid = await WaitForMailboxGuidAsync(target.AccountName, target.RoleLabel);
        Assert.NotEqual(0UL, mailboxGuid);
        return mailboxGuid;
    }

    private async Task<ResponseResult> SendCheckMailAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ulong mailboxGuid)
    {
        var result = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.CheckMail,
            Parameters = { new RequestParameter { LongParam = (long)mailboxGuid } }
        });
        _output.WriteLine($"[MAIL] {target.RoleLabel} CheckMail result: {result}");
        return result;
    }

    private async Task EnsureEconomySettingsAsync()
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await _bot.EnsureSettingsAsync(settingsPath);
        _bot.SetOutput(_output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
        await _bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(_bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");
    }

    private LiveBotFixture.BotRunnerActionTarget ResolveMailActionTarget()
    {
        var target = _bot.ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no mail action dispatch.");
        if (!string.IsNullOrWhiteSpace(_bot.FgAccountName))
        {
            _output.WriteLine(
                $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched for Shodan topology parity; MailSystem baseline remains BG-only.");
        }
        _output.WriteLine(
            $"[ACTION-PLAN] BG {target.AccountName}/{target.CharacterName}: stage mailbox and dispatch CheckMail.");

        return target;
    }

    private async Task<ulong> WaitForMailboxGuidAsync(string account, string label)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < TimeSpan.FromSeconds(5))
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var mailbox = snap?.NearbyObjects?
                .FirstOrDefault(go => go.GameObjectType == 19
                    || (go.Name ?? string.Empty).Contains("mail", StringComparison.OrdinalIgnoreCase));

            var guid = mailbox?.Base?.Guid ?? 0UL;
            if (guid != 0)
            {
                _output.WriteLine($"[{label}] Mailbox found: type={mailbox!.GameObjectType} name='{mailbox.Name}' GUID={guid:X}");
                return guid;
            }

            await Task.Delay(200);
        }

        _output.WriteLine($"[{label}] Mailbox GUID not found after 5s.");
        return 0;
    }

    private static int CountItemSlots(WoWActivitySnapshot? snapshot, uint itemId)
        => snapshot?.Player?.BagContents?.Values.Count(value => value == itemId) ?? 0;

    private static string ResolveRepoPath(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine([dir.FullName, .. segments]);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate repo path: {Path.Combine(segments)}");
    }
}
