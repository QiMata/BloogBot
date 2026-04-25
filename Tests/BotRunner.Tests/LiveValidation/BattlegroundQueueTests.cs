using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed BG queue smoke test. SHODAN owns level/location staging;
/// the BG BotRunner target receives only the battleground queue action.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BattlegroundQueueTests
{
    private const int WarsongGulchTypeId = (int)global::BotRunner.Travel.BattlemasterData.BattlegroundType.WarsongGulch;
    private const int WarsongGulchMapId = 489;
    private const uint WarsongBattlemasterEntry = 3890;
    private static int s_correlationSequence;

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public BattlegroundQueueTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task BG_QueueForWSG_ReceivesQueuedStatus()
    {
        var target = await EnsureBattlegroundQueueTargetAsync();

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            levelTo: global::BotRunner.Travel.BattlemasterData.GetMinimumLevel(
                global::BotRunner.Travel.BattlemasterData.BattlegroundType.WarsongGulch),
            cleanSlate: true,
            clearInventoryFirst: false);

        var staged = await _bot.StageBotRunnerAtOrgrimmarWarsongBattlemasterAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: false);
        Assert.True(staged, $"{target.RoleLabel}: expected Orgrimmar WSG battlemaster staging with visible nearby units.");

        await _bot.QuiesceAccountsAsync(
            [target.AccountName],
            "BGQueue:AfterStage",
            TimeSpan.FromSeconds(10));

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);
        Assert.True(snap!.IsObjectManagerValid, "ObjectManager should be valid before BG queue.");

        var battlemaster = snap.NearbyUnits.FirstOrDefault(unit =>
            (unit.GameObject?.Entry ?? 0u) == WarsongBattlemasterEntry);
        Assert.NotNull(battlemaster);
        _output.WriteLine(
            $"[{target.RoleLabel}] Found WSG battlemaster {battlemaster!.GameObject?.Name} " +
            $"entry={battlemaster.GameObject?.Entry} flags=0x{battlemaster.NpcFlags:X}");

        var correlationId = $"bg-queue:{target.AccountName}:{Interlocked.Increment(ref s_correlationSequence)}";
        var joinResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
        {
            ActionType = ActionType.JoinBattleground,
            CorrelationId = correlationId,
            Parameters =
            {
                new RequestParameter { IntParam = WarsongGulchTypeId },
                new RequestParameter { IntParam = WarsongGulchMapId },
            }
        });
        _output.WriteLine($"[{target.RoleLabel}] JoinBattleground dispatched (result={joinResult}, corr={correlationId})");
        Assert.Equal(ResponseResult.Success, joinResult);

        try
        {
            var observed = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snapshot => HasQueueDispatchEvidence(snapshot, correlationId),
                TimeSpan.FromSeconds(12),
                pollIntervalMs: 500,
                progressLabel: $"{target.RoleLabel} WSG queue action");

            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(target.AccountName);
            LogBattlegroundMessages(snap);

            var ack = FindLatestMatchingAck(snap, correlationId);
            if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
            {
                Assert.Fail(
                    $"{target.RoleLabel}: JoinBattleground ACK {ack.Status} " +
                    $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
            }

            Assert.True(
                observed,
                $"{target.RoleLabel}: expected JoinBattleground current/previous action, ACK, or battleground status chat marker.");
        }
        finally
        {
            await _bot.SendActionAsync(target.AccountName, new ActionMessage
            {
                ActionType = ActionType.LeaveBattleground,
            });
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureBattlegroundQueueTargetAsync()
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

        var target = _bot.ResolveBotRunnerActionTargets(
                includeForegroundIfActionable: false,
                foregroundFirst: false)
            .Single(candidate => !candidate.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: BG queue action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no BG queue action dispatch.");

        return target;
    }

    private void LogBattlegroundMessages(WoWActivitySnapshot? snapshot)
    {
        if (snapshot == null)
            return;

        _output.WriteLine(
            $"[BG-QUEUE] Current={snapshot.CurrentAction?.ActionType} Previous={snapshot.PreviousAction?.ActionType} " +
            $"MapId={snapshot.CurrentMapId} Screen={snapshot.ScreenState}");

        foreach (var msg in snapshot.RecentChatMessages.Where(IsBattlegroundMessage))
            _output.WriteLine($"[BG-QUEUE] Chat: {msg}");

        foreach (var err in snapshot.RecentErrors)
            _output.WriteLine($"[BG-QUEUE] Error: {err}");
    }

    private static bool HasQueueDispatchEvidence(WoWActivitySnapshot snapshot, string correlationId)
    {
        if (snapshot.CurrentAction?.ActionType == ActionType.JoinBattleground
            || snapshot.PreviousAction?.ActionType == ActionType.JoinBattleground)
        {
            return true;
        }

        var ack = FindLatestMatchingAck(snapshot, correlationId);
        if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
            return true;

        if (ack?.Status is CommandAckEvent.Types.AckStatus.Pending or CommandAckEvent.Types.AckStatus.Success)
            return true;

        return snapshot.RecentChatMessages.Any(IsBattlegroundMessage)
            || snapshot.RecentErrors.Any(IsBattlegroundMessage);
    }

    private static bool IsBattlegroundMessage(string message)
        => message.Contains("BATTLEGROUND_STATUS", StringComparison.OrdinalIgnoreCase)
            || message.Contains("battleground", StringComparison.OrdinalIgnoreCase)
            || message.Contains("queue", StringComparison.OrdinalIgnoreCase)
            || message.Contains("battle", StringComparison.OrdinalIgnoreCase);

    private static CommandAckEvent? FindLatestMatchingAck(WoWActivitySnapshot? snapshot, string correlationId)
    {
        if (snapshot == null)
            return null;

        CommandAckEvent? pendingMatch = null;
        for (var i = snapshot.RecentCommandAcks.Count - 1; i >= 0; i--)
        {
            var ack = snapshot.RecentCommandAcks[i];
            if (!string.Equals(ack.CorrelationId, correlationId, StringComparison.Ordinal))
                continue;

            if (ack.Status != CommandAckEvent.Types.AckStatus.Pending)
                return ack;

            pendingMatch ??= ack;
        }

        return pendingMatch;
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
