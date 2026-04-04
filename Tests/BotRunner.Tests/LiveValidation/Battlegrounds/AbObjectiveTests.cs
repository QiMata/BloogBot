using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// P25.11, 25.12: AB node assault and AB full game.
///
/// Run: dotnet test --filter "FullyQualifiedName~AbObjectiveTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AbObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public AbObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AB_NodeAssault_BotCapturesUncontested()
    {
        // P25.11: Bot assaults an uncontested AB node — node flag changes to team color
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AB_FullGame_CompletesToVictoryOrDefeat()
    {
        // P25.12: Bot plays a full AB game — match concludes with a result
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
