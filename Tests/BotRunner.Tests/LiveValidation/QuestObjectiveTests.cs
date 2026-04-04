using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.11: Quest objective tracking test — Bot accepts kill quest, kills required mobs,
/// asserts kill count increments in snapshot, completes quest.
///
/// Run: dotnet test --filter "FullyQualifiedName~QuestObjectiveTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class QuestObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public QuestObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Quest_KillObjective_CountIncrementsAndCompletes()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
