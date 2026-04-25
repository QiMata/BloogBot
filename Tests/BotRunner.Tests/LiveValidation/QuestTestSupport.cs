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

internal static class QuestTestSupport
{
    private static int s_questCorrelationSequence;

    internal static async Task EnsureQuestSettingsAsync(
        LiveBotFixture bot,
        ITestOutputHelper output)
    {
        var settingsPath = ResolveRepoPath(
            "Services", "WoWStateManager", "Settings", "Configs", "Economy.config.json");

        await bot.EnsureSettingsAsync(settingsPath);
        bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(bot.IsReady, bot.FailureReason ?? "Live bot not ready");
        await bot.AssertConfiguredCharactersMatchAsync(settingsPath);

        global::Tests.Infrastructure.Skip.If(
            string.IsNullOrWhiteSpace(bot.ShodanAccountName),
            "Shodan director was not launched by Economy.config.json.");
    }

    internal static LiveBotFixture.BotRunnerActionTarget ResolveBgActionTarget(
        LiveBotFixture bot,
        ITestOutputHelper output)
    {
        var target = bot.ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)
            .Single(actionTarget => !actionTarget.IsForeground);

        output.WriteLine(
            $"[ACTION-PLAN] SHODAN {bot.ShodanAccountName}/{bot.ShodanCharacterName}: director only, no quest action dispatch.");
        output.WriteLine(
            $"[ACTION-PLAN] BG {target.AccountName}/{target.CharacterName}: quest action target.");

        return target;
    }

    internal static async Task<ResponseResult> SendQuestActionAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        LiveBotFixture.BotRunnerActionTarget target,
        ActionMessage action,
        string stepName,
        int timeoutSeconds = 10)
    {
        var correlationId =
            $"quest:{target.AccountName}:{Interlocked.Increment(ref s_questCorrelationSequence)}";
        action.CorrelationId = correlationId;

        var result = await bot.SendActionAsync(target.AccountName, action);
        output.WriteLine($"[QUEST] {target.RoleLabel} {stepName} dispatch result: {result}");
        if (result != ResponseResult.Success)
            return result;

        var completed = await bot.WaitForSnapshotConditionAsync(
            target.AccountName,
            snapshot => HasCompletedAction(snapshot, correlationId),
            TimeSpan.FromSeconds(timeoutSeconds),
            pollIntervalMs: 250,
            progressLabel: $"{target.RoleLabel} {stepName} action");

        await bot.RefreshSnapshotsAsync();
        var latest = await bot.GetSnapshotAsync(target.AccountName);
        var ack = FindLatestMatchingAck(latest, correlationId);
        if (ack?.Status is CommandAckEvent.Types.AckStatus.Failed or CommandAckEvent.Types.AckStatus.TimedOut)
        {
            Assert.Fail(
                $"{target.RoleLabel} {stepName} reported ACK {ack.Status} " +
                $"(reason={ack.FailureReason ?? "(none)"}, corr={correlationId}).");
        }

        output.WriteLine($"[QUEST] {target.RoleLabel} {stepName} completion corr={correlationId}: {completed}");
        Assert.True(completed, $"{target.RoleLabel} {stepName} did not complete within {timeoutSeconds}s.");
        return result;
    }

    internal static ActionMessage MakeInteractWith(ulong npcGuid) => new()
    {
        ActionType = ActionType.InteractWith,
        Parameters = { new RequestParameter { LongParam = unchecked((long)npcGuid) } },
    };

    internal static ActionMessage MakeStartMeleeAttack(ulong targetGuid) => new()
    {
        ActionType = ActionType.StartMeleeAttack,
        Parameters = { new RequestParameter { LongParam = unchecked((long)targetGuid) } },
    };

    internal static ActionMessage MakeAcceptQuest(ulong npcGuid, uint questId) => new()
    {
        ActionType = ActionType.AcceptQuest,
        Parameters =
        {
            new RequestParameter { LongParam = unchecked((long)npcGuid) },
            new RequestParameter { IntParam = (int)questId },
        },
    };

    internal static ActionMessage MakeCompleteQuest(ulong npcGuid, uint questId) => new()
    {
        ActionType = ActionType.CompleteQuest,
        Parameters =
        {
            new RequestParameter { LongParam = unchecked((long)npcGuid) },
            new RequestParameter { IntParam = (int)questId },
        },
    };

    internal static async Task<ulong> FindNpcByEntryAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        string account,
        string label,
        int npcEntry,
        string npcName,
        int attempts = 5)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await bot.RefreshSnapshotsAsync();
            var snap = await bot.GetSnapshotAsync(account);
            var units = snap?.NearbyUnits?.ToList() ?? [];

            foreach (var unit in units)
            {
                if ((unit.GameObject?.Entry ?? 0) != npcEntry)
                    continue;

                var guid = unit.GameObject?.Base?.Guid ?? 0UL;
                output.WriteLine(
                    $"  [{label}] Found {npcName}: GUID=0x{guid:X} Entry={npcEntry} NpcFlags={unit.NpcFlags}");
                return guid;
            }

            if (attempt < attempts - 1)
            {
                output.WriteLine($"  [{label}] {npcName} not found on attempt {attempt + 1}; retrying.");
                await Task.Delay(1000);
            }
        }

        await bot.RefreshSnapshotsAsync();
        var debugSnap = await bot.GetSnapshotAsync(account);
        var allUnits = debugSnap?.NearbyUnits?.Take(15).ToList() ?? [];
        output.WriteLine($"  [{label}] {npcName} not found. Visible units ({allUnits.Count}):");
        foreach (var unit in allUnits)
        {
            var guid = unit.GameObject?.Base?.Guid ?? 0UL;
            output.WriteLine(
                $"    [0x{guid:X8}] Entry={unit.GameObject?.Entry} {unit.GameObject?.Name} NpcFlags={unit.NpcFlags}");
        }

        return 0;
    }

    internal static async Task<ulong> FindNearbyUnitByFlagsAsync(
        LiveBotFixture bot,
        ITestOutputHelper output,
        string account,
        string label,
        uint npcFlags,
        string purpose)
    {
        var unit = await bot.WaitForNearbyUnitAsync(
            account,
            npcFlags,
            timeoutMs: 15000,
            progressLabel: $"{label} {purpose}");

        Assert.NotNull(unit);
        var guid = unit!.GameObject?.Base?.Guid ?? 0UL;
        output.WriteLine(
            $"[QUEST] {label} found {purpose}: {unit.GameObject?.Name}, flags={unit.NpcFlags}, guid=0x{guid:X}");
        Assert.True(guid != 0, $"{label}: {purpose} should have a valid GUID.");
        return guid;
    }

    internal static bool HasQuest(WoWActivitySnapshot? snapshot, uint questId)
        => snapshot?.Player?.QuestLogEntries?.Any(q => q.QuestLog1 == questId || q.QuestId == questId) == true;

    internal static async Task<bool> WaitForQuestPresenceAsync(
        LiveBotFixture bot,
        string account,
        uint questId,
        bool shouldExist,
        TimeSpan timeout)
        => await bot.WaitForSnapshotConditionAsync(
            account,
            snap => HasQuest(snap, questId) == shouldExist,
            timeout,
            pollIntervalMs: 500,
            progressLabel: $"quest {questId} present={shouldExist}");

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
