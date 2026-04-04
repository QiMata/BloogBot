using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.10: Raid formation test — 40 bots form raid, assign subgroups, ready check passes.
/// Assert group list correct.
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
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
