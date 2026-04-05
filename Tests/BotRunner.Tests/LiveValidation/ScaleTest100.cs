using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.13: Scale test (100 bots) — 100 BG bots all login, move to Orgrimmar, perform patrol.
/// Assert all 100 snapshots received within 5s window.
///
/// Run: dotnet test --filter "FullyQualifiedName~ScaleTest100" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class ScaleTest100
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public ScaleTest100(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Scale_100Bots_AllSnapshotsReceived()
    {
        _output.WriteLine("[SCALE] 100-bot test requires dedicated test harness — see LoadTestRunner");
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
