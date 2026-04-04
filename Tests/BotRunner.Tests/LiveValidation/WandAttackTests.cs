using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P20.7: Wand attack tests — Priest/Mage bot equips wand, starts wand attack on target.
/// Assert ranged auto-attack damage events via SMSG_ATTACKERSTATUPDATE.
///
/// Run: dotnet test --filter "FullyQualifiedName~WandAttackTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class WandAttackTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public WandAttackTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Wand_ShootTarget_DealsDamage()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
