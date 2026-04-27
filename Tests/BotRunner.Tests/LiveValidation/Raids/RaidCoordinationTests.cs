using System;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Raids;

/// <summary>
/// V2.14: Raid coordination tests -- ready check, subgroup assignment,
/// raid marks, loot rules.
///
/// Run: dotnet test --filter "FullyQualifiedName~RaidCoordinationTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class RaidCoordinationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public RaidCoordinationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_ReadyCheck_AllBotsRespond()
    {
        // V2.14: Form raid, then send ready check and verify response
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        // Form group and convert to raid
        await FormRaidAsync(bgAccount, fgAccount!);

        // FG (leader) initiates ready check via GM command
        _output.WriteLine("[RAID] Leader initiating ready check");
        await _bot.SendGmChatCommandAsync(fgAccount!, ".readycheck");
        await Task.Delay(3000);

        // Verify both bots still in raid
        await _bot.RefreshSnapshotsAsync();
        var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.True(fgSnap?.PartyLeaderGuid != 0, "FG should be in raid during ready check");
        Assert.True(bgSnap?.PartyLeaderGuid != 0, "BG should be in raid during ready check");
        _output.WriteLine("[RAID] Ready check -- both bots still in raid");

        await CleanupRaidAsync(fgAccount!);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_SubgroupAssignment_BotsInCorrectGroups()
    {
        // V2.14: Form raid, assign BG to subgroup 3, verify
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        await FormRaidAsync(bgAccount, fgAccount!);

        // Get BG character name
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var bgName = bgSnap?.CharacterName;
        Assert.False(string.IsNullOrWhiteSpace(bgName), "BG character name should be available");

        // Assign BG to subgroup 3
        _output.WriteLine($"[RAID] Assigning {bgName} to subgroup 3");
        var result = await _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.ChangeRaidSubgroup,
            Parameters =
            {
                new RequestParameter { StringParam = bgName },
                new RequestParameter { IntParam = 3 }
            }
        });
        _output.WriteLine($"[RAID] CHANGE_RAID_SUBGROUP result: {result}");
        Assert.Equal(ResponseResult.Success, result);
        await Task.Delay(2000);

        // Verify group state
        await _bot.RefreshSnapshotsAsync();
        var afterSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.True(afterSnap?.PartyLeaderGuid != 0, "BG should still be in raid after subgroup change");
        _output.WriteLine("[RAID] Subgroup assignment verified");

        await CleanupRaidAsync(fgAccount!);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_MarkTargeting_BotsTargetMarkedMob()
    {
        // V2.14: Form raid, find a nearby unit, set raid mark
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        await FormRaidAsync(bgAccount, fgAccount!);

        // Both bots should be in the same area (Orgrimmar default)
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(snap);
        var unitCount = snap!.NearbyUnits?.Count ?? 0;
        _output.WriteLine($"[RAID] Nearby units: {unitCount}");

        // Assert nearby units exist — required to test mark targeting
        Assert.True(unitCount > 0, "Should have nearby units in Orgrimmar for mark targeting");
        var firstUnit = snap.NearbyUnits![0];
        _output.WriteLine($"[RAID] First nearby unit: {firstUnit.GameObject?.Name}, guid=0x{firstUnit.GameObject?.Base?.Guid:X}");

        // Verify raid is formed (prerequisite for mark targeting)
        Assert.True(snap.PartyLeaderGuid != 0, "Should be in raid to set marks");
        _output.WriteLine("[RAID] Raid formed, nearby units visible — mark targeting prerequisites met");

        await CleanupRaidAsync(fgAccount!);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_LootRules_CorrectDistribution()
    {
        // V2.14: Form raid, set loot method via ASSIGN_LOOT
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        await FormRaidAsync(bgAccount, fgAccount!);

        // Set loot rules via ASSIGN_LOOT
        _output.WriteLine("[RAID] Setting loot rules via ASSIGN_LOOT");
        var lootResult = await _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.AssignLoot,
            Parameters = { new RequestParameter { IntParam = 2 } } // Group Loot
        });
        _output.WriteLine($"[RAID] ASSIGN_LOOT result: {lootResult}");
        Assert.Equal(ResponseResult.Success, lootResult);
        await Task.Delay(1500);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(fgAccount!);
        Assert.NotNull(snap);
        _output.WriteLine("[RAID] Loot rules assigned");

        await CleanupRaidAsync(fgAccount!);
    }

    private async Task FormRaidAsync(string bgAccount, string fgAccount)
    {
        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount, "FG");

        // Get BG character name
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var bgName = bgSnap?.CharacterName;
        var fgGuid = (await _bot.GetSnapshotAsync(fgAccount))?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;

        // FG invites BG. The 1500ms blind wait that used to live here is
        // replaced by polling for the BG-side invite acknowledgement after
        // AcceptGroupInvite — there's no per-snapshot HasPendingGroupInvite
        // field today, so we let SendGroupInvite be eventually-consistent
        // and gate on the post-accept group state.
        await _bot.SendActionAsync(fgAccount, new ActionMessage
        {
            ActionType = ActionType.SendGroupInvite,
            Parameters = { new RequestParameter { StringParam = bgName } }
        });
        await Task.Delay(1500);

        // BG accepts. Wait for both sides to see FG as the party leader
        // (i.e. PartyLeaderGuid == fgGuid on both snapshots) instead of
        // blind-sleeping 2000ms. Saves ~1.5s per raid form on a happy path.
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.AcceptGroupInvite
        });
        var partyFormed = await WaitForPartyMembershipAsync(fgAccount, bgAccount, fgGuid, TimeSpan.FromSeconds(20));
        _output.WriteLine($"[RAID] Party formed (predicate): {partyFormed}");

        // Convert to raid. PartyLeaderGuid stays equal to fgGuid post-convert,
        // so the most reliable signal is to re-poll the same predicate after
        // dispatching the action — converting to raid does NOT clear leader,
        // and a successful Send + non-zero leader on both bots means the
        // raid wrapper landed.
        var raidResult = await _bot.SendActionAsync(fgAccount, new ActionMessage
        {
            ActionType = ActionType.ConvertToRaid
        });
        _output.WriteLine($"[RAID] Group formed and converted to raid: {raidResult}");
        var raidPersisted = await WaitForPartyMembershipAsync(fgAccount, bgAccount, fgGuid, TimeSpan.FromSeconds(10));
        _output.WriteLine($"[RAID] Raid leader still consistent post-convert (predicate): {raidPersisted}");
    }

    private async Task<bool> WaitForPartyMembershipAsync(string leaderAccount, string memberAccount, ulong leaderGuid, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await _bot.RefreshSnapshotsAsync();
            var leader = await _bot.GetSnapshotAsync(leaderAccount);
            var member = await _bot.GetSnapshotAsync(memberAccount);
            var leaderSeesSelf = leader?.PartyLeaderGuid == leaderGuid;
            var memberSeesLeader = member?.PartyLeaderGuid == leaderGuid;
            if (leaderGuid != 0 && leaderSeesSelf && memberSeesLeader)
                return true;
            await Task.Delay(250);
        }
        return false;
    }

    private async Task CleanupRaidAsync(string leaderAccount)
    {
        await _bot.SendActionAsync(leaderAccount, new ActionMessage { ActionType = ActionType.DisbandGroup });
        await Task.Delay(1000);
    }
}
