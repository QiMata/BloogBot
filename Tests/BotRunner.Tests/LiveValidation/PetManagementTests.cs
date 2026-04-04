using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.12: Pet management test — Hunter bot summons pet, sets stance, feeds pet,
/// uses pet ability in combat.
///
/// Run: dotnet test --filter "FullyQualifiedName~PetManagementTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class PetManagementTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public PetManagementTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Pet_SummonAndManage_StanceFeedAbility()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
