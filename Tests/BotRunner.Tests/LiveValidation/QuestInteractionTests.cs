using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Quest interaction integration test - dual-client validation.
///
/// Flow per client:
///   1) Ensure strict-alive setup from snapshot state.
///   2) Ensure test quest is absent.
///   3) Add quest via GM and assert snapshot quest-log presence.
///   4) Complete quest via GM and assert completed-or-removed state in snapshot.
///   5) Remove quest via GM and assert snapshot quest-log absence.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class QuestInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int TestQuestId = 783; // A Threat Within
    private const uint PlayerFlagGhost = 0x10; // PLAYER_FLAGS_GHOST
    private const uint StandStateMask = 0xFF;
    private const uint StandStateDead = 7; // UNIT_STAND_STATE_DEAD

    public QuestInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    public async Task Quest_AddCompleteAndRemove_AreReflectedInSnapshots()
    {
        _output.WriteLine($"=== BG Bot: {_bot.BgCharacterName} ===");
        await RunQuestScenario(_bot.BgAccountName!, "BG");

        if (_bot.ForegroundBot != null)
        {
            _output.WriteLine($"\n=== FG Bot: {_bot.FgCharacterName} ===");
            await RunQuestScenario(_bot.FgAccountName!, "FG");
        }
        else
        {
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
    }

    private async Task RunQuestScenario(string account, string label)
    {
        await EnsureStrictAliveAsync(account, label);
        await EnsureQuestAbsentAsync(account, label, TestQuestId);

        _output.WriteLine($"  [{label}] Step 1: Add quest {TestQuestId}");
        var addTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest add {TestQuestId}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(addTrace, label, ".quest add");

        var added = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: true, TimeSpan.FromSeconds(12));
        Assert.True(added, $"[{label}] Quest {TestQuestId} should appear in ActivitySnapshot quest log after add.");
        await _bot.RefreshSnapshotsAsync();
        var addedSnap = await _bot.GetSnapshotAsync(account);
        var addedQuest = addedSnap?.Player?.QuestLogEntries?.FirstOrDefault(q => q.QuestLog1 == (uint)TestQuestId);

        _output.WriteLine($"  [{label}] Step 2: Complete quest {TestQuestId}");
        var completeTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest complete {TestQuestId}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(completeTrace, label, ".quest complete");

        var completedOrChanged = await WaitForQuestCompletedChangedOrRemovedAsync(
            account,
            TestQuestId,
            addedQuest?.QuestLog2 ?? 0,
            addedQuest?.QuestLog3 ?? 0,
            TimeSpan.FromSeconds(12));
        var completionReportedInChat = completeTrace.ChatMessages.Concat(completeTrace.ErrorMessages)
            .Any(m => m.Contains("completed", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            completedOrChanged || completionReportedInChat,
            $"[{label}] Quest {TestQuestId} completion should be reflected by quest-log change/removal or explicit completed chat response.");

        _output.WriteLine($"  [{label}] Step 3: Remove quest {TestQuestId}");
        var removeTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {TestQuestId}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(removeTrace, label, ".quest remove");

        var removed = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: false, TimeSpan.FromSeconds(12));
        Assert.True(removed, $"[{label}] Quest {TestQuestId} should be absent from ActivitySnapshot quest log after remove.");
    }

    private async Task EnsureStrictAliveAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (IsStrictAlive(snap))
            return;

        var characterName = snap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(characterName), $"{label}: missing character name for revive setup.");

        _output.WriteLine($"  [{label}] Not strict-alive; reviving before quest setup.");
        await _bot.RevivePlayerAsync(characterName!);

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(1000);
            await _bot.RefreshSnapshotsAsync();
            snap = await _bot.GetSnapshotAsync(account);
            if (IsStrictAlive(snap))
                return;
        }

        global::Tests.Infrastructure.Skip.If(true, $"{label}: could not establish strict-alive setup state.");
    }

    private async Task EnsureQuestAbsentAsync(string account, string label, int questId)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (!HasQuest(snap, questId))
            return;

        _output.WriteLine($"  [{label}] Quest {questId} already present; removing for clean setup.");
        var trace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {questId}", captureResponse: true, delayMs: 1000);
        AssertCommandSucceeded(trace, label, ".quest remove (setup)");

        var removed = await WaitForQuestPresenceAsync(account, questId, shouldExist: false, TimeSpan.FromSeconds(12));
        Assert.True(removed, $"[{label}] Quest {questId} should be removed during setup.");
    }

    private async Task<bool> WaitForQuestPresenceAsync(string account, int questId, bool shouldExist, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var exists = HasQuest(snap, questId);
            if (exists == shouldExist)
                return true;

            await Task.Delay(500);
        }

        return false;
    }

    private async Task<bool> WaitForQuestCompletedChangedOrRemovedAsync(
        string account,
        int questId,
        uint baselineQuestLog2,
        uint baselineQuestLog3,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            var quest = snap?.Player?.QuestLogEntries?.FirstOrDefault(q => q.QuestLog1 == (uint)questId);

            if (quest == null)
                return true; // removed by server after completion flow

            if (quest.QuestLog2 != baselineQuestLog2 || quest.QuestLog3 != baselineQuestLog3)
                return true; // quest state changed

            await Task.Delay(500);
        }

        return false;
    }

    private static bool HasQuest(WoWActivitySnapshot? snap, int questId)
        => snap?.Player?.QuestLogEntries?.Any(q => q.QuestLog1 == (uint)questId) == true;

    private void AssertCommandSucceeded(LiveBotFixture.GmChatCommandTrace trace, string label, string command)
    {
        Assert.Equal(ResponseResult.Success, trace.DispatchResult);

        var rejected = trace.ChatMessages.Concat(trace.ErrorMessages).Any(LiveBotFixture.ContainsCommandRejection);
        Assert.False(rejected, $"[{label}] {command} was rejected by command table or permissions.");
    }

    private static bool IsStrictAlive(WoWActivitySnapshot? snap)
    {
        var player = snap?.Player;
        var unit = player?.Unit;
        if (player == null || unit == null)
            return false;

        var hasGhostFlag = (player.PlayerFlags & PlayerFlagGhost) != 0;
        var standState = unit.Bytes1 & StandStateMask;
        return unit.Health > 0 && !hasGhostFlag && standState != StandStateDead;
    }
}
