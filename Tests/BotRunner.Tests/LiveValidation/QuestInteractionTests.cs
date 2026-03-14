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
/// Quest snapshot-plumbing coverage — validates GM-driven quest state propagation.
///
/// Tests quest-log field projection (QuestLog1/2/3) through the snapshot pipeline,
/// including add/complete/remove transitions for a quest with kill objectives (786 Encroachment).
/// Task-driven quest accept/turn-in is covered by StarterQuestTests (AcceptQuest/CompleteQuest
/// action dispatch for quest 4641).
///
/// Flow per client:
///   1) Ensure strict-alive, remove stale quest state.
///   2) .quest add -> assert snapshot QuestLog1 presence.
///   3) .quest complete -> assert QuestLog2/3 state change.
///   4) .quest remove -> assert quest absence from snapshot.
/// </summary>
[RequiresMangosStack]
[Collection(LiveValidationCollection.Name)]
public class QuestInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int TestQuestId = 786; // Encroachment (kill 4 Quilboar + 4 Scouts — has countable objectives)

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

        if (_bot.IsFgActionable)
        {
            _output.WriteLine($"=== FG Bot: {_bot.FgCharacterName} ===");
            _output.WriteLine("[PARITY] Running BG and FG quest scenarios in parallel.");

            var bgTask = RunQuestScenario(_bot.BgAccountName!, "BG");
            var fgTask = RunQuestScenario(_bot.FgAccountName!, "FG");
            await Task.WhenAll(bgTask, fgTask);
        }
        else
        {
            await RunQuestScenario(_bot.BgAccountName!, "BG");
            _output.WriteLine("\nFG Bot: NOT AVAILABLE");
        }
    }

    private async Task RunQuestScenario(string account, string label)
    {
        await _bot.EnsureCleanSlateAsync(account, label);
        await EnsureQuestAbsentAsync(account, label, TestQuestId);

        try
        {
            // Self-selection required — MaNGOS .quest commands need getSelectedPlayer()
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(500); // Brief pause for .targetself to process

            _output.WriteLine($"  [{label}] Step 1: Add quest {TestQuestId}");
            var addTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest add {TestQuestId}", captureResponse: true, delayMs: 1500);
            AssertCommandSucceeded(addTrace, label, ".quest add");

            var added = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: true, TimeSpan.FromSeconds(12));
            Assert.True(added, $"[{label}] Quest {TestQuestId} should appear in ActivitySnapshot quest log after add.");
            await _bot.RefreshSnapshotsAsync();
            var addedSnap = await _bot.GetSnapshotAsync(account);
            var addedQuest = addedSnap?.Player?.QuestLogEntries?.FirstOrDefault(q => q.QuestLog1 == (uint)TestQuestId);
            _output.WriteLine($"  [{label}] After add: QuestLog1={addedQuest?.QuestLog1} QuestLog2={addedQuest?.QuestLog2} QuestLog3={addedQuest?.QuestLog3}");

            _output.WriteLine($"  [{label}] Step 2: Complete quest {TestQuestId}");
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(500);
            var completeTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest complete {TestQuestId}", captureResponse: true, delayMs: 1500);
            AssertCommandSucceeded(completeTrace, label, ".quest complete");

            var completedOrChanged = await WaitForQuestCompletedChangedOrRemovedAsync(
                account,
                TestQuestId,
                addedQuest?.QuestLog2 ?? 0,
                addedQuest?.QuestLog3 ?? 0,
                TimeSpan.FromSeconds(12));
            Assert.True(completedOrChanged,
                $"[{label}] Quest {TestQuestId} completion must be reflected by quest-log change/removal in the ActivitySnapshot.");

            _output.WriteLine($"  [{label}] Step 3: Remove quest {TestQuestId}");
            await _bot.BotSelectSelfAsync(account);
            await Task.Delay(500);
            var removeTrace = await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {TestQuestId}", captureResponse: true, delayMs: 1500);
            AssertCommandSucceeded(removeTrace, label, ".quest remove");

            var removed = await WaitForQuestPresenceAsync(account, TestQuestId, shouldExist: false, TimeSpan.FromSeconds(12));
            Assert.True(removed, $"[{label}] Quest {TestQuestId} should be absent from ActivitySnapshot quest log after remove.");
        }
        finally
        {
            // Ensure quest is cleaned up even if assertions fail mid-scenario.
            // This prevents stale quest state from contaminating subsequent test runs.
            try
            {
                await _bot.RefreshSnapshotsAsync();
                var snap = await _bot.GetSnapshotAsync(account);
                if (HasQuest(snap, TestQuestId))
                {
                    _output.WriteLine($"  [{label}] Cleanup: removing quest {TestQuestId} left over from failed scenario.");
                    await _bot.BotSelectSelfAsync(account);
                    await Task.Delay(1000);
                    await _bot.SendGmChatCommandTrackedAsync(account, $".quest remove {TestQuestId}", captureResponse: false, delayMs: 500);
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  [{label}] Cleanup warning: quest removal failed — {ex.Message}");
            }
        }
    }

    private Task EnsureStrictAliveAsync(string account, string label)
        => _bot.EnsureStrictAliveAsync(account, label);

    private async Task EnsureQuestAbsentAsync(string account, string label, int questId)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (!HasQuest(snap, questId))
            return;

        _output.WriteLine($"  [{label}] Quest {questId} already present; removing for clean setup.");
        await _bot.BotSelectSelfAsync(account);
        await Task.Delay(300);
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

}
