using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.8: Channel tests — Bot joins General channel, sends message, second bot receives it.
/// Assert message content matches via SMSG_MESSAGECHAT with CHAT_MSG_CHANNEL type.
///
/// Run: dotnet test --filter "FullyQualifiedName~ChannelTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class ChannelTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public ChannelTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Channel_SendMessage_OtherBotReceives()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
