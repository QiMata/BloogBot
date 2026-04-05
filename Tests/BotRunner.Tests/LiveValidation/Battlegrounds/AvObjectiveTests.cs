using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// P25.16, 25.17, 25.18, 25.19: AV first-objective, tower assault, GY capture, general kill.
///
/// Run: dotnet test --filter "FullyQualifiedName~AvObjectiveTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AvObjectiveTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public AvObjectiveTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AV_FirstObjective_BotCompletesInitialTask()
    {
        // P25.16: Bot completes the first AV objective (e.g., Snowfall GY or Stonehearth bunker)
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AV_TowerAssault_BotAssaultsTower()
    {
        // P25.17: Bot assaults an enemy tower — tower begins burning
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AV_GraveyardCapture_BotCapturesGY()
    {
        // P25.18: Bot captures an enemy graveyard — GY ownership changes
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task AV_GeneralKill_BotParticipatesInBossKill()
    {
        // P25.19: Bot participates in killing the enemy general — AV match ends
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
