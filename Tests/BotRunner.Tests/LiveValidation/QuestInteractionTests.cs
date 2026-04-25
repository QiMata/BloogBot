using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed quest snapshot-plumbing coverage. Quest state mutations are
/// staged behind fixture helpers; the test body asserts snapshot projection.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class QuestInteractionTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint TestQuestId = 786;

    public QuestInteractionTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Quest_AddCompleteAndRemove_AreReflectedInSnapshots()
    {
        await QuestTestSupport.EnsureQuestSettingsAsync(_bot, _output);
        var target = QuestTestSupport.ResolveBgActionTarget(_bot, _output);

        await _bot.StageBotRunnerLoadoutAsync(
            target.AccountName,
            target.RoleLabel,
            cleanSlate: true,
            clearInventoryFirst: true);

        var absent = await _bot.StageBotRunnerQuestAbsentAsync(
            target.AccountName,
            target.RoleLabel,
            TestQuestId);
        Assert.True(absent, $"[{target.RoleLabel}] Quest {TestQuestId} should be absent before add staging.");

        try
        {
            var added = await _bot.StageBotRunnerQuestAddedAsync(
                target.AccountName,
                target.RoleLabel,
                TestQuestId);
            Assert.True(added, $"[{target.RoleLabel}] Quest {TestQuestId} should appear in ActivitySnapshot quest log after add.");

            await _bot.RefreshSnapshotsAsync();
            var addedSnap = await _bot.GetSnapshotAsync(target.AccountName);
            var addedQuest = addedSnap?.Player?.QuestLogEntries?.FirstOrDefault(q => q.QuestLog1 == TestQuestId);
            Assert.NotNull(addedQuest);
            _output.WriteLine(
                $"  [{target.RoleLabel}] After add: QuestLog1={addedQuest!.QuestLog1} QuestLog2={addedQuest.QuestLog2} QuestLog3={addedQuest.QuestLog3}");

            var completedOrChanged = await _bot.StageBotRunnerQuestCompletedAsync(
                target.AccountName,
                target.RoleLabel,
                TestQuestId);
            Assert.True(
                completedOrChanged,
                $"[{target.RoleLabel}] Quest {TestQuestId} completion must be reflected by quest-log change/removal.");

            var removed = await _bot.StageBotRunnerQuestAbsentAsync(
                target.AccountName,
                target.RoleLabel,
                TestQuestId);
            Assert.True(removed, $"[{target.RoleLabel}] Quest {TestQuestId} should be absent after remove staging.");
        }
        finally
        {
            await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, TestQuestId);
        }
    }
}
