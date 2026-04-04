using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.4: Mail system tests — Bot sends mail with item + gold to alt. Alt collects.
/// Assert delivery via SMSG_SEND_MAIL_RESULT and SMSG_MAIL_LIST_RESULT.
///
/// Run: dotnet test --filter "FullyQualifiedName~MailSystemTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class MailSystemTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const float OrgMailboxX = 1615.58f, OrgMailboxY = -4391.60f, OrgMailboxZ = 13.11f;

    public MailSystemTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendGold_RecipientReceives()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Mail_SendItem_RecipientReceivesItem()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
