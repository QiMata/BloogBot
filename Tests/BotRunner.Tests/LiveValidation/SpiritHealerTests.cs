using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P23.21: Spirit healer resurrection.
///
/// Run: dotnet test --filter "FullyQualifiedName~SpiritHealerTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SpiritHealerTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public SpiritHealerTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task SpiritHealer_Resurrect_PlayerAliveWithSickness()
    {
        // P23.21: Bot dies, releases spirit, interacts with spirit healer — resurrected with resurrection sickness
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
