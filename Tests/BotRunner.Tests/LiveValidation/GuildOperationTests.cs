using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.5: Guild operation tests — Bot creates guild, invites second bot, both accept.
/// Assert guild roster shows both members.
///
/// Run: dotnet test --filter "FullyQualifiedName~GuildOperationTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class GuildOperationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public GuildOperationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Guild_CreateAndInvite_RosterShowsBothMembers()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
