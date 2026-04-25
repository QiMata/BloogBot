using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed spirit-healer coverage. SHODAN stages the target at a
/// graveyard and induces corpse state; the BotRunner target receives only
/// death/recovery ActionType dispatches.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SpiritHealerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint NpcFlagSpiritHealer = 0x20;
    private static int s_spiritCorrelationSequence;

    public SpiritHealerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task SpiritHealer_Resurrect_PlayerAliveWithSickness()
    {
        _output.WriteLine("=== Spirit Healer: Shodan-staged corpse, BG action recovery ===");

        var target = await EnsureSpiritHealerSettingsAndTargetAsync();
        var cleanupRequired = false;

        try
        {
            var death = await _bot.StageBotRunnerCorpseAtValleySpiritHealerAsync(
                target.AccountName,
                target.RoleLabel);
            cleanupRequired = true;
            _output.WriteLine(
                $"[{target.RoleLabel}] death staged: command={death.Command}, " +
                $"corpseState={death.ObservedCorpseState}, details={death.Details}");

            await _bot.RefreshSnapshotsAsync();
            var corpseSnapshot = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(corpseSnapshot);
            Assert.False(LiveBotFixture.IsStrictAlive(corpseSnapshot), $"{target.RoleLabel}: target should be dead before release.");

            await SendSpiritHealerActionAsync(target, ActionType.ReleaseCorpse, "ReleaseCorpse");

            var ghostConfirmed = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                IsGhostSnapshot,
                TimeSpan.FromSeconds(15),
                pollIntervalMs: 500,
                progressLabel: $"{target.RoleLabel} ghost-state");
            Assert.True(ghostConfirmed, $"{target.RoleLabel}: target never transitioned to ghost state after ReleaseCorpse.");

            var spiritHealer = await _bot.WaitForNearbyUnitAsync(
                target.AccountName,
                NpcFlagSpiritHealer,
                timeoutMs: 15000,
                progressLabel: $"{target.RoleLabel} spirit-healer lookup");
            Assert.NotNull(spiritHealer);

            var spiritHealerGuid = spiritHealer!.GameObject?.Base?.Guid ?? 0;
            Assert.NotEqual(0UL, spiritHealerGuid);
            var spiritHealerPosition = spiritHealer.GameObject?.Base?.Position;
            Assert.NotNull(spiritHealerPosition);
            _output.WriteLine(
                $"[{target.RoleLabel}] spirit healer: name={spiritHealer.GameObject?.Name}, " +
                $"guid=0x{spiritHealerGuid:X}, flags=0x{spiritHealer.NpcFlags:X}, " +
                $"pos=({spiritHealerPosition!.X:F1},{spiritHealerPosition.Y:F1},{spiritHealerPosition.Z:F1})");

            await SendSpiritHealerActionAsync(
                target,
                new ActionMessage
                {
                    ActionType = ActionType.Goto,
                    Parameters =
                    {
                        new RequestParameter { FloatParam = spiritHealerPosition.X },
                        new RequestParameter { FloatParam = spiritHealerPosition.Y },
                        new RequestParameter { FloatParam = spiritHealerPosition.Z },
                        new RequestParameter { FloatParam = 4.0f }
                    }
                },
                "MoveToSpiritHealer",
                timeoutSeconds: 35);

            var closeEnough = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                snapshot => IsWithin2D(snapshot, spiritHealerPosition.X, spiritHealerPosition.Y, 5.0f),
                TimeSpan.FromSeconds(20),
                pollIntervalMs: 500,
                progressLabel: $"{target.RoleLabel} spirit-healer approach");
            Assert.True(closeEnough, $"{target.RoleLabel}: ghost did not move within spirit-healer interaction range.");

            await SendSpiritHealerActionAsync(
                target,
                new ActionMessage
                {
                    ActionType = ActionType.InteractWith,
                    Parameters = { new RequestParameter { LongParam = unchecked((long)spiritHealerGuid) } }
                },
                "InteractWithSpiritHealer",
                timeoutSeconds: 12);

            var alive = await _bot.WaitForSnapshotConditionAsync(
                target.AccountName,
                LiveBotFixture.IsStrictAlive,
                TimeSpan.FromSeconds(20),
                pollIntervalMs: 500,
                progressLabel: $"{target.RoleLabel} spirit-healer resurrection");

            if (!alive)
            {
                await _bot.RefreshSnapshotsAsync();
                _bot.DumpSnapshotDiagnostics(await _bot.GetSnapshotAsync(target.AccountName), $"{target.RoleLabel}-spirit-healer-failure");
            }

            Assert.True(alive, $"{target.RoleLabel}: InteractWith spirit healer did not restore strict-alive state.");
        }
        finally
        {
            if (cleanupRequired)
                await _bot.RestoreBotRunnerAliveAtValleySpiritHealerAsync(target.AccountName, target.RoleLabel);
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureSpiritHealerSettingsAndTargetAsync()
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
            "BG spirit-healer action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: launched idle for topology parity; " +
            "ghost-form foreground recovery remains covered by guarded CRASH-001 tests.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: director only, no recovery action dispatch.");

        return target;
    }

    private Task<ResponseResult> SendSpiritHealerActionAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ActionType actionType,
        string stepName,
        int timeoutSeconds = 12)
        => SendSpiritHealerActionAsync(
            target,
            new ActionMessage { ActionType = actionType },
            stepName,
            timeoutSeconds);

    private async Task<ResponseResult> SendSpiritHealerActionAsync(
        LiveBotFixture.BotRunnerActionTarget target,
        ActionMessage action,
        string stepName,
        int timeoutSeconds = 12)
    {
        var correlationId =
            $"spirit:{target.AccountName}:{Interlocked.Increment(ref s_spiritCorrelationSequence)}";
        action.CorrelationId = correlationId;

        var result = await _bot.SendActionAsync(target.AccountName, action);
        _output.WriteLine($"[SPIRIT] {target.RoleLabel} {stepName} dispatch result: {result}");
        if (result != ResponseResult.Success)
            return result;

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

    private static bool IsGhostSnapshot(WoWActivitySnapshot snapshot)
        => (snapshot.Player?.PlayerFlags & 0x10) != 0;

    private static bool IsWithin2D(WoWActivitySnapshot snapshot, float x, float y, float radius)
    {
        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        return position != null && LiveBotFixture.Distance2D(position.X, position.Y, x, y) <= radius;
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
