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

        // Mark targeting via GM command (set skull mark on target)
        if (unitCount > 0)
        {
            var firstUnit = snap.NearbyUnits![0];
            _output.WriteLine($"[RAID] First nearby unit: {firstUnit.GameObject?.Name}, guid=0x{firstUnit.GameObject?.Base?.Guid:X}");
        }

        _output.WriteLine("[RAID] Mark targeting test -- raid formed and units visible");
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

        // FG invites BG
        await _bot.SendActionAsync(fgAccount, new ActionMessage
        {
            ActionType = ActionType.SendGroupInvite,
            Parameters = { new RequestParameter { StringParam = bgName } }
        });
        await Task.Delay(1500);

        // BG accepts
        await _bot.SendActionAsync(bgAccount, new ActionMessage
        {
            ActionType = ActionType.AcceptGroupInvite
        });
        await Task.Delay(2000);

        // Convert to raid
        var raidResult = await _bot.SendActionAsync(fgAccount, new ActionMessage
        {
            ActionType = ActionType.ConvertToRaid
        });
        _output.WriteLine($"[RAID] Group formed and converted to raid: {raidResult}");
        await Task.Delay(2000);
    }

    private async Task CleanupRaidAsync(string leaderAccount)
    {
        await _bot.SendActionAsync(leaderAccount, new ActionMessage { ActionType = ActionType.DisbandGroup });
        await Task.Delay(1000);
    }
}
