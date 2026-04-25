using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed Battle Shout spell-cast validation. SHODAN owns warrior
/// loadout, rage, and aura cleanup staging; the BG BotRunner target receives
/// only the CastSpell action under test.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SpellCastOnTargetTests
{
    private const uint BattleShoutSpellId = 6673;
    private const int BattleShoutRageInternalUnits = 200;

    private static readonly uint[] CleanupAuraSpellIds =
    [
        BattleShoutSpellId,
        2457,
        2367,
    ];

    private static int s_correlationSequence;

    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public SpellCastOnTargetTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task CastSpell_BattleShout_AuraApplied()
    {
        var target = await EnsureSpellCastTargetAsync();

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            spellsToLearn: [BattleShoutSpellId],
            cleanSlate: true,
            clearInventoryFirst: false);

        var spellKnown = await _bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => snapshot.Player?.SpellList?.Contains(BattleShoutSpellId) == true,
            TimeSpan.FromSeconds(8),
            pollIntervalMs: 300,
            progressLabel: $"{target.RoleLabel} battle-shout-learn");
        Assert.True(spellKnown, $"{target.RoleLabel}: Battle Shout should be present in SpellList before cast.");

        await _bot.StageBotRunnerRageAsync(
            target.AccountName,
            target.RoleLabel,
            BattleShoutRageInternalUnits);
        await _bot.StageBotRunnerAurasAbsentAsync(
            target.AccountName,
            target.RoleLabel,
            CleanupAuraSpellIds);

        await _bot.RefreshSnapshotsAsync();
        var before = await _bot.GetSnapshotAsync(target.AccountName);
        _output.WriteLine(
            $"[{target.RoleLabel}] Before cast: hasBattleShout={HasBattleShoutAura(before)} " +
            $"auras=[{string.Join(", ", before?.Player?.Unit?.Auras ?? [])}]");

        var auraAppeared = false;
        try
        {
            for (var attempt = 1; attempt <= 2 && !auraAppeared; attempt++)
            {
                if (attempt > 1)
                {
                    _output.WriteLine(
                        $"[{target.RoleLabel}] Battle Shout aura was not detected; re-staging rage and retrying.");
                    await _bot.StageBotRunnerRageAsync(
                        target.AccountName,
                        target.RoleLabel,
                        BattleShoutRageInternalUnits);
                }

                var correlationId = $"spell-battle-shout:{target.AccountName}:{Interlocked.Increment(ref s_correlationSequence)}";
                var castResult = await _bot.SendActionAsync(target.AccountName, new ActionMessage
                {
                    ActionType = ActionType.CastSpell,
                    CorrelationId = correlationId,
                    Parameters =
                    {
                        new RequestParameter { IntParam = (int)BattleShoutSpellId },
                    },
                });
                _output.WriteLine(
                    $"[{target.RoleLabel}] CastSpell Battle Shout dispatched " +
                    $"(attempt={attempt}, result={castResult}, corr={correlationId}).");
                Assert.Equal(ResponseResult.Success, castResult);

                auraAppeared = await WaitForBattleShoutAuraOrFailAsync(
                    target.AccountName,
                    target.RoleLabel,
                    correlationId,
                    TimeSpan.FromSeconds(12));
            }

            if (!auraAppeared)
            {
                var finalSnap = await _bot.GetSnapshotAsync(target.AccountName);
                _output.WriteLine(
                    $"[{target.RoleLabel}] Battle Shout aura not found after retry. " +
                    $"auras=[{string.Join(", ", finalSnap?.Player?.Unit?.Auras ?? [])}]");
            }

            Assert.True(auraAppeared, $"{target.RoleLabel}: Battle Shout aura should appear after CastSpell.");
        }
        finally
        {
            await _bot.StageBotRunnerAurasAbsentAsync(
                target.AccountName,
                target.RoleLabel,
                [BattleShoutSpellId]);
        }
    }

    private async Task<LiveBotFixture.BotRunnerActionTarget> EnsureSpellCastTargetAsync()
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
            $"[ACTION-PLAN] {target.RoleLabel} {target.AccountName}/{target.CharacterName}: " +
            $"BG Battle Shout CastSpell action target.");
        _output.WriteLine(
            $"[ACTION-PLAN] FG {_bot.FgAccountName}/{_bot.FgCharacterName}: " +
            "launched idle for topology parity; FG ActionType.CastSpell-by-id is tracked separately.");
        _output.WriteLine(
            $"[ACTION-PLAN] SHODAN {_bot.ShodanAccountName}/{_bot.ShodanCharacterName}: " +
            "director only, no CastSpell dispatch.");

        return target;
    }

    private async Task<bool> WaitForBattleShoutAuraOrFailAsync(
        string accountName,
        string label,
        string correlationId,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastProgressLog = TimeSpan.Zero;

        while (stopwatch.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snapshot = await _bot.GetSnapshotAsync(accountName);
            if (HasBattleShoutAura(snapshot))
            {
                _output.WriteLine(
                    $"[{label}] Battle Shout aura detected after {stopwatch.ElapsedMilliseconds}ms.");
                return true;
            }

            var ack = FindLatestMatchingAck(snapshot, correlationId);
            if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
            {
                Assert.Fail(
                    $"[{label}] CastSpell Battle Shout ACK {ack.Status} " +
                    $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
                return false;
            }

            if (stopwatch.Elapsed - lastProgressLog >= TimeSpan.FromSeconds(5))
            {
                lastProgressLog = stopwatch.Elapsed;
                _output.WriteLine(
                    $"[{label}] Waiting for Battle Shout aura... " +
                    $"{stopwatch.Elapsed.TotalSeconds:F0}s / {timeout.TotalSeconds:F0}s elapsed.");
            }

            await Task.Delay(300);
        }

        return false;
    }

    private static bool HasBattleShoutAura(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.Auras?.Contains(BattleShoutSpellId) == true;

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
