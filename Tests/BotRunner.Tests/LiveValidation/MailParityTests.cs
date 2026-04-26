using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed mail parity baselines. SHODAN stages mailbox location and
/// SOAP mail payloads; FG/BG action targets receive CheckMail so foreground
/// mail collection stays covered under combined-suite load.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MailParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint LinenClothItemId = LiveBotFixture.TestItems.LinenCloth;
    private static readonly TimeSpan ForegroundMailTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BackgroundMailTimeout = TimeSpan.FromSeconds(8);

    public MailParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendGold_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredMailTargets();

        foreach (var target in targets)
        {
            var mailboxGuid = await StageMailboxAndFindGuidAsync(target);
            await _bot.RefreshSnapshotsAsync();
            var before = await _bot.GetSnapshotAsync(target.AccountName);
            var coinageBefore = before?.Player?.Coinage ?? 0;
            var baselineMessages = before?.RecentChatMessages.ToArray() ?? Array.Empty<string>();

            await _bot.StageBotRunnerMailboxMoneyAsync(
                target.AccountName,
                target.RoleLabel,
                copper: 25,
                subject: "Parity Gold",
                body: "Mail parity gold");

            var checkResult = await SendCheckMailAsync(target, mailboxGuid);
            Assert.Equal(ResponseResult.Success, checkResult);

            var collectionObserved = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snap => (snap.Player?.Coinage ?? 0) > coinageBefore
                    || (target.IsForeground && HasMailCollectionMarker(snap, "Parity Gold", 25, baselineMessages)),
                GetMailTimeout(target),
                pollIntervalMs: 300,
                progressLabel: $"{target.RoleLabel} parity mail-gold");
            Assert.True(collectionObserved, $"{target.RoleLabel}: coinage or foreground mail collection marker should reflect collected parity gold mail.");

            await _bot.RefreshSnapshotsAsync();
            var after = await _bot.GetSnapshotAsync(target.AccountName);
            _output.WriteLine($"[MAIL-PARITY] {target.RoleLabel} coinage {coinageBefore}->{after?.Player?.Coinage ?? 0} marker={FindMailCollectionMarker(after, "Parity Gold", baselineMessages) ?? "<none>"}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendItem_FgBgParity()
    {
        await EnsureEconomySettingsAsync();
        var targets = ResolveRequiredMailTargets();

        foreach (var target in targets)
        {
            await _bot.StageBotRunnerLoadoutAsync(
                target.AccountName,
                target.RoleLabel,
                cleanSlate: true,
                clearInventoryFirst: true);

            var mailboxGuid = await StageMailboxAndFindGuidAsync(target, cleanSlate: false);
            await _bot.RefreshSnapshotsAsync();
            var before = await _bot.GetSnapshotAsync(target.AccountName);
            var itemCountBefore = CountItemSlots(before, LinenClothItemId);
            var baselineMessages = before?.RecentChatMessages.ToArray() ?? Array.Empty<string>();

            await _bot.StageBotRunnerMailboxItemAsync(
                target.AccountName,
                target.RoleLabel,
                LinenClothItemId,
                count: 1,
                subject: "Parity Item",
                body: "Mail parity item");

            var checkResult = await SendCheckMailAsync(target, mailboxGuid);
            Assert.Equal(ResponseResult.Success, checkResult);

            var itemReceived = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snap => CountItemSlots(snap, LinenClothItemId) >= itemCountBefore + 1
                    || (target.IsForeground && HasMailSubjectMarker(snap, "Parity Item", baselineMessages)),
                GetMailTimeout(target),
                pollIntervalMs: 300,
                progressLabel: $"{target.RoleLabel} parity mail-item");
            Assert.True(itemReceived, $"{target.RoleLabel}: Linen Cloth should appear after collecting parity item mail.");

            await _bot.RefreshSnapshotsAsync();
            var after = await _bot.GetSnapshotAsync(target.AccountName);
            _output.WriteLine($"[MAIL-PARITY] {target.RoleLabel} Linen Cloth {itemCountBefore}->{CountItemSlots(after, LinenClothItemId)}");
        }
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
        _output.WriteLine($"[MAIL-PARITY] {target.RoleLabel} CheckMail result: {result}");
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

    private IReadOnlyList<LiveBotFixture.BotRunnerActionTarget> ResolveRequiredMailTargets()
    {
        var targets = _bot.ResolveBotRunnerActionTargets();
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no mail parity action dispatch.");

        foreach (var target in targets)
        {
            _output.WriteLine(
                $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: stage mailbox and dispatch CheckMail.");
        }

        return targets;
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

    private static bool HasMailCollectionMarker(
        WoWActivitySnapshot? snapshot,
        string subject,
        uint minimumCopper,
        IReadOnlyCollection<string> baselineMessages)
    {
        var marker = FindMailCollectionMarker(snapshot, subject, baselineMessages);
        return marker != null && TryReadMarkerUInt(marker, "money=", out var money) && money >= minimumCopper;
    }

    private static bool HasMailSubjectMarker(
        WoWActivitySnapshot? snapshot,
        string subject,
        IReadOnlyCollection<string> baselineMessages)
        => FindMailCollectionMarker(snapshot, subject, baselineMessages) != null;

    private static TimeSpan GetMailTimeout(LiveBotFixture.BotRunnerActionTarget target)
        => target.IsForeground ? ForegroundMailTimeout : BackgroundMailTimeout;

    private static string? FindMailCollectionMarker(
        WoWActivitySnapshot? snapshot,
        string subject,
        IReadOnlyCollection<string> baselineMessages)
        => snapshot?.RecentChatMessages?
            .LastOrDefault(message => message.Contains("[MAIL-COLLECT]", StringComparison.Ordinal)
                && message.Contains(subject, StringComparison.OrdinalIgnoreCase)
                && !baselineMessages.Contains(message));

    private static bool TryReadMarkerUInt(string marker, string key, out uint value)
    {
        value = 0;
        var start = marker.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += key.Length;
        var end = marker.IndexOf(' ', start);
        var span = end < 0 ? marker[start..] : marker[start..end];
        return uint.TryParse(span, out value);
    }

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
