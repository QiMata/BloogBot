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
/// Shodan-directed map transition hardening. SHODAN stages the BG action
/// target at the Ironforge tram entrance and triggers the rejected Deeprun
/// Tram transition; the BotRunner target receives only a post-bounce action
/// to prove it stayed responsive.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MapTransitionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float IfTramX = -4838f;
    private const float IfTramY = -1317f;
    private static int s_mapTransitionCorrelationSequence;

    public MapTransitionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task MapTransition_DeeprunTramBounce_ClientSurvives()
    {
        _output.WriteLine("=== Deeprun Tram Map Transition Bounce: Shodan-staged BG action target ===");

        var target = await EnsureMapTransitionSettingsAndTargetAsync();
        var cleanupRequired = false;

        try
        {
            var staged = await _bot.StageBotRunnerAtIronforgeTramEntranceAsync(
                target.AccountName,
                target.RoleLabel);
            cleanupRequired = true;
            Assert.True(staged, $"{target.RoleLabel}: failed to stage at Ironforge tram entrance.");

            await _bot.RefreshSnapshotsAsync();
            var ironforgeSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(ironforgeSnapshot);
            var ironforgePosition = ironforgeSnapshot!.Player?.Unit?.GameObject?.Base?.Position;
            Assert.NotNull(ironforgePosition);
            var distFromIronforge = LiveBotFixture.Distance2D(
                ironforgePosition!.X,
                ironforgePosition.Y,
                IfTramX,
                IfTramY);
            _output.WriteLine(
                $"[{target.RoleLabel}] staged position: ({ironforgePosition.X:F1}, {ironforgePosition.Y:F1}, {ironforgePosition.Z:F1}); " +
                $"distance from IF target={distFromIronforge:F1}y");
            Assert.True(distFromIronforge <= 80f, $"{target.RoleLabel}: target did not reach Ironforge tram staging area.");

            var bounced = await _bot.TriggerBotRunnerRejectedDeeprunTramTransitionAsync(
                target.AccountName,
                target.RoleLabel);
            Assert.True(bounced, $"{target.RoleLabel}: Deeprun Tram rejected transition did not settle back to InWorld.");

            await _bot.RefreshSnapshotsAsync();
            var bounceSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(bounceSnapshot);
            Assert.Equal("InWorld", bounceSnapshot!.ScreenState);
            Assert.Equal(BotConnectionState.BotInWorld, bounceSnapshot.ConnectionState);
            Assert.False(bounceSnapshot.IsMapTransition, $"{target.RoleLabel}: map transition flag remained set after bounce.");

            var bouncePosition = bounceSnapshot.Player?.Unit?.GameObject?.Base?.Position;
            Assert.NotNull(bouncePosition);
            _output.WriteLine(
                $"[{target.RoleLabel}] position after bounce: ({bouncePosition!.X:F1}, {bouncePosition.Y:F1}, {bouncePosition.Z:F1}); " +
                $"currentMap={bounceSnapshot.CurrentMapId}");

            Assert.True(
                MathF.Abs(bouncePosition.X) > 10 || MathF.Abs(bouncePosition.Y) > 10,
                $"{target.RoleLabel}: position after bounce is suspiciously close to origin.");

            await SendMapTransitionActionAsync(
                target,
                new ActionMessage
                {
                    ActionType = ActionType.Goto,
                    Parameters =
                    {
                        new RequestParameter { FloatParam = bouncePosition.X },
                        new RequestParameter { FloatParam = bouncePosition.Y },
                        new RequestParameter { FloatParam = bouncePosition.Z },
                        new RequestParameter { FloatParam = 8.0f }
                    }
                },
                "PostBounceGoto",
                timeoutSeconds: 12);

            _output.WriteLine($"[PASS] {target.RoleLabel} client survived Deeprun Tram bounce and accepted a BotRunner action.");
        }
        finally
        {
            if (cleanupRequired)
                await _bot.ReturnBotRunnerToOrgrimmarSafeZoneAsync(target.AccountName, target.RoleLabel);
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureMapTransitionSettingsAndTargetAsync()
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
            .Single(target => !target.IsForeground);

        _output.WriteLine(
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            "BG map-transition action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no action dispatch.");

        return target;
    }

    private async Task<ResponseResult> SendMapTransitionActionAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ActionMessage action,
        string stepName,
        int timeoutSeconds)
    {
        var correlationId =
            $"maptransition:{target.AccountName}:{Interlocked.Increment(ref s_mapTransitionCorrelationSequence)}";
        action.CorrelationId = correlationId;

        var result = await _bot.SendActionAsync(target.AccountName, action);
        _output.WriteLine($"[MAP-TRANSITION] {target.RoleLabel} {stepName} dispatch result: {result}");
        Assert.Equal(ResponseResult.Success, result);

        var completed = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => HasCompletedAction(snapshot, correlationId),
            TimeSpan.FromSeconds(timeoutSeconds),
            pollIntervalMs: 250,
            progressLabel: $"{target.RoleLabel} {stepName} action");

        await _bot.RefreshSnapshotsAsync();
        var latest = await _bot.GetSnapshotAsync(target.AccountName);
        var ack = FindLatestMatchingAck(latest, correlationId);
        if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
        {
            Assert.Fail(
                $"{target.RoleLabel} {stepName} reported ACK {ack.Status} " +
                $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
        }

        Assert.True(completed, $"{target.RoleLabel} {stepName} did not complete within {timeoutSeconds}s.");
        return result;
    }

    private static bool HasCompletedAction(WoWActivitySnapshot snapshot, string correlationId)
    {
        var ack = FindLatestMatchingAck(snapshot, correlationId);
        if (ack != null && ack.Status != CommandAckEvent.Types.AckStatus.Pending)
            return true;

        return string.Equals(
            snapshot.PreviousAction?.CorrelationId,
            correlationId,
            StringComparison.Ordinal);
    }

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
