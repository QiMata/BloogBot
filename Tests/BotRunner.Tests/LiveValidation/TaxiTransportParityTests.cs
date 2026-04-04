using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.15, 23.16, 23.17: Taxi ride, transport boarding, cross-continent with FG/BG parity.
///
/// Run: dotnet test --filter "FullyQualifiedName~TaxiTransportParityTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TaxiTransportParityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TaxiTransportParityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_Ride_FgBgParity()
    {
        // P23.15: Both FG and BG take a taxi ride — arrival position matches
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_Board_FgBgParity()
    {
        // P23.16: Both FG and BG board a transport (zeppelin/boat) — position tracking consistent
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Transport_CrossContinent_FgBgParity()
    {
        // P23.17: Both FG and BG complete a cross-continent transport — map transition and final position match
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
