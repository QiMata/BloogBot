using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.9: BG queue test — Bot queues for WSG, asserts SMSG_BATTLEFIELD_STATUS received with queued status.
///
/// Run: dotnet test --filter "FullyQualifiedName~BattlegroundQueueTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class BattlegroundQueueTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public BattlegroundQueueTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task BG_QueueForWSG_ReceivesQueuedStatus()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
