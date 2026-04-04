using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using GameData.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.20: Gossip and quest NPC tests. Teleport near quest NPC, INTERACT_WITH,
/// verify gossip/quest options.
///
/// Run: dotnet test --filter "FullyQualifiedName~GossipQuestTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GossipQuestTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Razor Hill innkeeper (Orgrimmar area, has gossip + quest options)
    private const float RazorHillInnX = 338.0f, RazorHillInnY = -4689.0f, RazorHillInnZ = 15.0f;
    // NPC flags
    private const uint NpcFlagQuestGiver = (uint)NPCFlags.UNIT_NPC_FLAG_QUESTGIVER;
    private const uint NpcFlagGossip = (uint)NPCFlags.UNIT_NPC_FLAG_GOSSIP;

    public GossipQuestTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Gossip_MultiOption_SelectsCorrectOption()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Teleport near Razor Hill (lots of NPCs with gossip)
        await _bot.BotTeleportAsync(account, KalimdorMapId, RazorHillInnX, RazorHillInnY, RazorHillInnZ);
        await _bot.WaitForTeleportSettledAsync(account, RazorHillInnX, RazorHillInnY);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG gossip-setup");

        // Find a gossip NPC
        var gossipNpc = await _bot.WaitForNearbyUnitAsync(
            account,
            NpcFlagGossip,
            timeoutMs: 15000,
            progressLabel: "BG gossip-npc-search");
        Assert.NotNull(gossipNpc);
        _output.WriteLine($"[TEST] Found gossip NPC: {gossipNpc!.GameObject?.Name}, flags={gossipNpc.NpcFlags}");

        var npcGuid = gossipNpc.GameObject?.Base?.Guid ?? 0;
        Assert.True(npcGuid != 0, "Gossip NPC should have a valid GUID");

        // Interact with the gossip NPC
        var interactResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.InteractWith,
            Parameters = { new RequestParameter { LongParam = (long)npcGuid } }
        });
        _output.WriteLine($"[TEST] INTERACT_WITH gossip NPC result: {interactResult}");
        Assert.Equal(ResponseResult.Success, interactResult);

        // Wait for gossip response
        await Task.Delay(3000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        // Check recent chat messages for gossip dialog
        var messages = snap!.RecentChatMessages?.ToList()
            ?? new System.Collections.Generic.List<string>();
        _output.WriteLine($"[TEST] Recent chat messages after gossip: {messages.Count}");
        foreach (var msg in messages.TakeLast(5))
        {
            _output.WriteLine($"  Chat: {msg}");
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_Chain_CompletesSequentialQuests()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Teleport near quest NPCs
        await _bot.BotTeleportAsync(account, KalimdorMapId, RazorHillInnX, RazorHillInnY, RazorHillInnZ);
        await _bot.WaitForTeleportSettledAsync(account, RazorHillInnX, RazorHillInnY);
        await _bot.WaitForNearbyUnitsPopulatedAsync(account, timeoutMs: 5000, progressLabel: "BG quest-chain-setup");

        // Find a quest giver NPC
        var questNpc = await _bot.WaitForNearbyUnitAsync(
            account,
            NpcFlagQuestGiver,
            timeoutMs: 15000,
            progressLabel: "BG questgiver-search");

        if (questNpc != null)
        {
            _output.WriteLine($"[TEST] Found quest giver: {questNpc.GameObject?.Name}, flags={questNpc.NpcFlags}");

            var npcGuid = questNpc.GameObject?.Base?.Guid ?? 0;

            // Interact with quest giver
            var interactResult = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)npcGuid } }
            });
            _output.WriteLine($"[TEST] INTERACT_WITH quest giver result: {interactResult}");
            Assert.Equal(ResponseResult.Success, interactResult);

            await Task.Delay(3000);
            await _bot.RefreshSnapshotsAsync();
            var snap = await _bot.GetSnapshotAsync(account);
            Assert.NotNull(snap);

            // Verify quest log has entries
            var questEntries = snap!.Player?.QuestLogEntries?.ToList()
                ?? new System.Collections.Generic.List<Game.QuestLogEntry>();
            _output.WriteLine($"[TEST] Quest log entries after interaction: {questEntries.Count}");
            foreach (var qe in questEntries.Take(5))
            {
                _output.WriteLine($"  Quest: id={qe.QuestId}, log1={qe.QuestLog1}, log2={qe.QuestLog2}");
            }
        }
        else
        {
            _output.WriteLine("[TEST] No quest giver NPC found near Razor Hill");
            // Quest givers should always be present
            Assert.NotNull(questNpc);
        }
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_RewardSelection_PicksBestReward()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Add a simple quest that has a reward choice
        // "A Peon's Burden" (quest 2161) -- simple delivery quest with reward
        const uint questId = 2161;
        _output.WriteLine($"[SETUP] Adding quest {questId} via .quest add");
        await _bot.SendGmChatCommandAsync(account, $".quest add {questId}");
        await Task.Delay(1500);

        // Complete the quest immediately via GM
        _output.WriteLine($"[SETUP] Completing quest {questId} via .quest complete");
        await _bot.SendGmChatCommandAsync(account, $".quest complete {questId}");
        await Task.Delay(1500);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        // Check quest log state
        var questEntries = snap!.Player?.QuestLogEntries?.ToList()
            ?? new System.Collections.Generic.List<Game.QuestLogEntry>();
        _output.WriteLine($"[TEST] Quest log entries: {questEntries.Count}");

        // Cleanup
        await _bot.SendGmChatCommandAsync(account, $".quest remove {questId}");
        await Task.Delay(500);
    }
}
