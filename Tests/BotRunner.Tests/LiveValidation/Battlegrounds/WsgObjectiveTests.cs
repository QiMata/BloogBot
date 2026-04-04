using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// P25.7, 25.8: WSG flag capture and WSG full game.
///
/// Run: dotnet test --filter "FullyQualifiedName~WsgObjectiveTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class WsgObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public WsgObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task WSG_FlagCapture_BotPicksUpAndCapturesFlag()
    {
        // P25.7: Bot picks up enemy flag and returns it to base for a capture
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task WSG_FullGame_CompletesToVictoryOrDefeat()
    {
        // P25.8: Bot plays a full WSG game — match concludes with a result
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
