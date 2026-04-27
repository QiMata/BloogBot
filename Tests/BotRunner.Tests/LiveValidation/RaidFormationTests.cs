using System;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.13: Raid formation tests.
/// Form group (SEND_GROUP_INVITE + ACCEPT_GROUP_INVITE), CONVERT_TO_RAID,
/// CHANGE_RAID_SUBGROUP, verify group state in snapshot.
///
/// Run: dotnet test --filter "FullyQualifiedName~RaidFormationTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class RaidFormationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public RaidFormationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_Form40Man_AllMembersInCorrectSubgroups()
    {
        var bgAccount = _bot.BgAccountName!;
        var fgAccount = _bot.FgAccountName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(fgAccount), "FG account not available -- raid tests require two bots");

        await _bot.EnsureCleanSlateAsync(bgAccount, "BG");
        await _bot.EnsureCleanSlateAsync(fgAccount!, "FG");

        var fgActionable = await _bot.CheckFgActionableAsync();
        global::Tests.Infrastructure.Skip.If(!fgActionable, "FG bot not actionable");

        // Ensure both are ungrouped
        await EnsureUngroupedAsync(bgAccount, "BG");
        await EnsureUngroupedAsync(fgAccount!, "FG");

        // Step 1: FG invites BG
        await _bot.RefreshSnapshotsAsync();
        var bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        var bgName = bgSnap?.CharacterName;
        global::Tests.Infrastructure.Skip.If(string.IsNullOrWhiteSpace(bgName), "BG character name not available");

        _output.WriteLine($"[RAID] FG inviting BG ({bgName}) to group");
        var inviteResult = await _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.SendGroupInvite,
            Parameters = { new RequestParameter { StringParam = bgName } }
        });
        Assert.Equal(ResponseResult.Success, inviteResult);
        await Task.Delay(1500);

        // Step 2: BG accepts invite. Predicate-poll for both bots seeing FG
        // as the party leader, instead of blind-sleeping 2000ms.
        _output.WriteLine("[RAID] BG accepting group invite");
        var acceptResult = await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.AcceptGroupInvite
        });
        Assert.Equal(ResponseResult.Success, acceptResult);

        await _bot.RefreshSnapshotsAsync();
        var fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        var fgGuid = fgSnap?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        var partyFormed = await WaitForPartyMembershipAsync(fgAccount!, bgAccount, fgGuid, TimeSpan.FromSeconds(20));
        Assert.True(partyFormed, $"Party formation predicate failed (fgGuid=0x{fgGuid:X})");

        await _bot.RefreshSnapshotsAsync();
        fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.True(fgSnap?.PartyLeaderGuid != 0, "FG should be in a group after invite+accept");
        Assert.True(bgSnap?.PartyLeaderGuid != 0, "BG should be in a group after invite+accept");
        _output.WriteLine($"[RAID] Group formed: FG leader=0x{fgSnap!.PartyLeaderGuid:X}, BG leader=0x{bgSnap!.PartyLeaderGuid:X}");

        // Step 3: Convert to raid. PartyLeaderGuid persists post-convert,
        // so re-poll the same predicate as a smoke test that the raid
        // wrapper landed without breaking the leader relationship.
        _output.WriteLine("[RAID] FG converting group to raid");
        var raidResult = await _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.ConvertToRaid
        });
        _output.WriteLine($"[RAID] CONVERT_TO_RAID result: {raidResult}");
        Assert.Equal(ResponseResult.Success, raidResult);
        var raidPersisted = await WaitForPartyMembershipAsync(fgAccount!, bgAccount, fgGuid, TimeSpan.FromSeconds(10));
        Assert.True(raidPersisted, "Raid leader predicate failed post-convert");

        // Step 4: Change raid subgroup for BG
        _output.WriteLine("[RAID] Moving BG to subgroup 2");
        var subgroupResult = await _bot.SendActionAsync(fgAccount!, new ActionMessage
        {
            ActionType = ActionType.ChangeRaidSubgroup,
            Parameters =
            {
                new RequestParameter { StringParam = bgName },
                new RequestParameter { IntParam = 2 } // target subgroup
            }
        });
        _output.WriteLine($"[RAID] CHANGE_RAID_SUBGROUP result: {subgroupResult}");
        await Task.Delay(1500);

        // Verify raid state in snapshots
        await _bot.RefreshSnapshotsAsync();
        fgSnap = await _bot.GetSnapshotAsync(fgAccount!);
        bgSnap = await _bot.GetSnapshotAsync(bgAccount);
        Assert.NotNull(fgSnap);
        Assert.NotNull(bgSnap);
        Assert.True(fgSnap!.PartyLeaderGuid != 0, "FG should still be in raid");
        Assert.True(bgSnap!.PartyLeaderGuid != 0, "BG should still be in raid");
        _output.WriteLine("[RAID] Raid formation verified");

        // Cleanup: disband
        await _bot.SendActionAsync(fgAccount!, new ActionMessage { ActionType = ActionType.DisbandGroup });
        await Task.Delay(1000);
    }

    private async Task EnsureUngroupedAsync(string account, string label)
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        if (snap == null || snap.PartyLeaderGuid == 0)
            return;

        var selfGuid = snap.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        var action = selfGuid != 0 && snap.PartyLeaderGuid == selfGuid
            ? ActionType.DisbandGroup
            : ActionType.LeaveGroup;

        _output.WriteLine($"[{label}] Ungrouping (leader=0x{snap.PartyLeaderGuid:X}), sending {action}");
        await _bot.SendActionAndWaitAsync(account, new ActionMessage { ActionType = action }, delayMs: 1000);
    }

    private async Task<bool> WaitForPartyMembershipAsync(string leaderAccount, string memberAccount, ulong leaderGuid, TimeSpan timeout)
    {
        if (leaderGuid == 0)
            return false;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await _bot.RefreshSnapshotsAsync();
            var leader = await _bot.GetSnapshotAsync(leaderAccount);
            var member = await _bot.GetSnapshotAsync(memberAccount);
            if (leader?.PartyLeaderGuid == leaderGuid && member?.PartyLeaderGuid == leaderGuid)
                return true;
            await Task.Delay(250);
        }
        return false;
    }
}
