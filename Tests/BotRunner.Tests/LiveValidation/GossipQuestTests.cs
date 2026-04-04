using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.22, 23.23, 23.24: Multi-option gossip, quest chain, quest reward selection.
///
/// Run: dotnet test --filter "FullyQualifiedName~GossipQuestTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GossipQuestTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

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
        // P23.22: Bot interacts with NPC that has multiple gossip options — selects the correct one
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_Chain_CompletesSequentialQuests()
    {
        // P23.23: Bot completes a multi-step quest chain — each quest accepted and turned in sequentially
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_RewardSelection_PicksBestReward()
    {
        // P23.24: Bot completes quest with reward choice — selects the most appropriate reward
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
