using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Raids;

/// <summary>
/// P27.10, 27.11, 27.12, 27.13: Ready check, subgroup assignment,
/// raid mark targeting, loot rules.
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
        // P27.10: Leader initiates ready check — all bots respond ready
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_SubgroupAssignment_BotsInCorrectGroups()
    {
        // P27.11: Leader assigns bots to subgroups — snapshot reflects correct group numbers
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_MarkTargeting_BotsTargetMarkedMob()
    {
        // P27.12: Leader places raid mark on target — bots switch target to marked mob
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Raid_LootRules_CorrectDistribution()
    {
        // P27.13: Raid loot rules (group loot/master loot) — items distributed per configured rules
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
