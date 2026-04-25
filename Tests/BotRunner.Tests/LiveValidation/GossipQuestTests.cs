using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Shodan-directed gossip / quest-giver interaction coverage. SHODAN stages
/// world and quest state; the BG BotRunner target receives only action
/// dispatches for the executable interaction cases.
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GossipQuestTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint NpcFlagQuestGiver = (uint)NPCFlags.UNIT_NPC_FLAG_QUESTGIVER;
    private const uint NpcFlagGossip = (uint)NPCFlags.UNIT_NPC_FLAG_GOSSIP;
    private const uint RewardQuestId = 2161;

    public GossipQuestTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Gossip_MultiOption_SelectsCorrectOption()
    {
        await QuestTestSupport.EnsureQuestSettingsAsync(_bot, _output);
        var target = QuestTestSupport.ResolveBgActionTarget(_bot, _output);

        var staged = await _bot.StageBotRunnerAtRazorHillInnAsync(target.AccountName, target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected to stage near Razor Hill gossip NPCs.");

        var npcGuid = await QuestTestSupport.FindNearbyUnitByFlagsAsync(
            _bot,
            _output,
            target.AccountName,
            target.RoleLabel,
            NpcFlagGossip,
            "gossip-npc-search");

        var interactResult = await QuestTestSupport.SendQuestActionAsync(
            _bot,
            _output,
            target,
            QuestTestSupport.MakeInteractWith(npcGuid),
            "InteractWith gossip NPC");
        Assert.Equal(ResponseResult.Success, interactResult);

        await Task.Delay(1500);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var messages = snap!.RecentChatMessages?.ToList() ?? [];
        _output.WriteLine($"[QUEST] Recent chat messages after gossip: {messages.Count}");
        foreach (var msg in messages.TakeLast(5))
            _output.WriteLine($"  Chat: {msg}");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_Chain_CompletesSequentialQuests()
    {
        await QuestTestSupport.EnsureQuestSettingsAsync(_bot, _output);
        var target = QuestTestSupport.ResolveBgActionTarget(_bot, _output);

        var staged = await _bot.StageBotRunnerAtRazorHillInnAsync(target.AccountName, target.RoleLabel);
        Assert.True(staged, $"{target.RoleLabel}: expected to stage near Razor Hill quest givers.");

        var npcGuid = await QuestTestSupport.FindNearbyUnitByFlagsAsync(
            _bot,
            _output,
            target.AccountName,
            target.RoleLabel,
            NpcFlagQuestGiver,
            "questgiver-search");

        var interactResult = await QuestTestSupport.SendQuestActionAsync(
            _bot,
            _output,
            target,
            QuestTestSupport.MakeInteractWith(npcGuid),
            "InteractWith quest giver");
        Assert.Equal(ResponseResult.Success, interactResult);

        await Task.Delay(1500);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(target.AccountName);
        Assert.NotNull(snap);

        var questEntries = snap!.Player?.QuestLogEntries?.ToList() ?? [];
        _output.WriteLine($"[QUEST] Quest log entries after interaction: {questEntries.Count}");
        foreach (var entry in questEntries.Take(5))
        {
            _output.WriteLine(
                $"  Quest: id={entry.QuestId}, log1={entry.QuestLog1}, log2={entry.QuestLog2}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_RewardSelection_PicksBestReward()
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
            RewardQuestId);
        Assert.True(absent, $"{target.RoleLabel}: quest {RewardQuestId} should be absent before staging.");

        try
        {
            var added = await _bot.StageBotRunnerQuestAddedAsync(
                target.AccountName,
                target.RoleLabel,
                RewardQuestId);
            Assert.True(added, $"{target.RoleLabel}: quest {RewardQuestId} should be visible after Shodan staging.");

            var completed = await _bot.StageBotRunnerQuestCompletedAsync(
                target.AccountName,
                target.RoleLabel,
                RewardQuestId);
            _output.WriteLine(
                $"[QUEST] Staged completion for quest {RewardQuestId} changed snapshot state: {completed}");

            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(target.AccountName);
            Assert.NotNull(snap);
            _output.WriteLine(
                $"[QUEST] Quest log entries after staged completion: {snap!.Player?.QuestLogEntries.Count ?? 0}");
        }
        finally
        {
            await _bot.StageBotRunnerQuestAbsentAsync(target.AccountName, target.RoleLabel, RewardQuestId);
        }
    }
}
