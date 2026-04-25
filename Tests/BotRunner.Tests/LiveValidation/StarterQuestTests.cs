using System;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed starter quest integration. SHODAN stages quest cleanup and
/// NPC locations; BG receives the AcceptQuest / CompleteQuest action dispatches.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class StarterQuestTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint TestQuestId = 4641;
    private const int KaltunkEntry = 10176;
    private const int GornekEntry = 3143;

    public StarterQuestTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    public async Task Quest_AcceptAndTurnIn_StarterQuest()
    {
        await QuestTestSupport.EnsureQuestSettingsAsync(_bot, _output);
        var target = QuestTestSupport.ResolveBgActionTarget(_bot, _output);

        var absent = await _bot.StageBotRunnerQuestAbsentAsync(
            target.AccountName,
            target.RoleLabel,
            TestQuestId);
        Assert.True(absent, $"[{target.RoleLabel}] Quest {TestQuestId} should be absent before accept.");

        try
        {
            var stagedAtKaltunk = await _bot.StageBotRunnerAtValleyOfTrialsQuestGiverAsync(
                target.AccountName,
                target.RoleLabel,
                cleanSlate: true);
            Assert.True(stagedAtKaltunk, $"{target.RoleLabel}: expected to stage near Kaltunk.");

            var kaltunkGuid = await QuestTestSupport.FindNpcByEntryAsync(
                _bot,
                _output,
                target.AccountName,
                target.RoleLabel,
                KaltunkEntry,
                "Kaltunk");
            Assert.True(kaltunkGuid != 0, $"[{target.RoleLabel}] Kaltunk should be visible after Shodan staging.");

            var questAdded = false;
            for (var acceptAttempt = 0; acceptAttempt < 2 && !questAdded; acceptAttempt++)
            {
                if (acceptAttempt > 0)
                {
                    _output.WriteLine($"  [{target.RoleLabel}] Retrying AcceptQuest (attempt {acceptAttempt + 1}).");
                    await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, TestQuestId);
                }

                var acceptResult = await QuestTestSupport.SendQuestActionAsync(
                    _bot,
                    _output,
                    target,
                    QuestTestSupport.MakeAcceptQuest(kaltunkGuid, TestQuestId),
                    "AcceptQuest");
                Assert.Equal(ResponseResult.Success, acceptResult);

                questAdded = await QuestTestSupport.WaitForQuestPresenceAsync(
                    _bot,
                    target.AccountName,
                    TestQuestId,
                    shouldExist: true,
                    TimeSpan.FromSeconds(10));
            }

            Assert.True(questAdded, $"[{target.RoleLabel}] Quest {TestQuestId} should appear after AcceptQuest.");

            var stagedAtGornek = await _bot.StageBotRunnerAtValleyOfTrialsQuestTurnInAsync(
                target.AccountName,
                target.RoleLabel,
                cleanSlate: false);
            Assert.True(stagedAtGornek, $"{target.RoleLabel}: expected to stage near Gornek.");

            var gornekGuid = await QuestTestSupport.FindNpcByEntryAsync(
                _bot,
                _output,
                target.AccountName,
                target.RoleLabel,
                GornekEntry,
                "Gornek");
            Assert.True(gornekGuid != 0, $"[{target.RoleLabel}] Gornek should be visible after Shodan staging.");

            var completeResult = await QuestTestSupport.SendQuestActionAsync(
                _bot,
                _output,
                target,
                QuestTestSupport.MakeCompleteQuest(gornekGuid, TestQuestId),
                "CompleteQuest");
            Assert.Equal(ResponseResult.Success, completeResult);

            var questRemoved = await QuestTestSupport.WaitForQuestPresenceAsync(
                _bot,
                target.AccountName,
                TestQuestId,
                shouldExist: false,
                TimeSpan.FromSeconds(10));
            Assert.True(questRemoved, $"[{target.RoleLabel}] Quest {TestQuestId} should be absent after completion.");
        }
        finally
        {
            await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, TestQuestId);
            await _bot.StageBotRunnerAtOrgrimmarTradeSpotAsync(
                target.AccountName,
                target.RoleLabel,
                cleanSlate: false);
        }
    }
}
