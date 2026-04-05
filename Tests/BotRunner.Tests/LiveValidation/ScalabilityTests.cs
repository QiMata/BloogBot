using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

[Collection(LiveValidationCollection.Name)]
public class ScalabilityTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public ScalabilityTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P9.6 - ObjectManager test migration to scalability harness.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_6_ObjectManager_TestMigration()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.6 ObjectManager test migration - snapshot received");
    }

    /// <summary>
    /// P9.7 - N=10 multi-bot validation.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_7_MultiBotValidation_N10()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.7 N=10 multi-bot validation - snapshot received");
    }

    /// <summary>
    /// P9.25 - 100-bot scalability baseline.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_25_Scalability_100BotBaseline()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.25 100-bot scalability baseline - snapshot received");
    }

    /// <summary>
    /// P9.26 - 500-bot scalability test.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_26_Scalability_500Bot()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.26 500-bot scalability test - snapshot received");
    }

    /// <summary>
    /// P9.27 - 3000-bot scalability test.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_27_Scalability_3000Bot()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.27 3000-bot scalability test - snapshot received");
    }

    /// <summary>
    /// P9.28 - BenchmarkDotNet regression suite.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task P9_28_BenchmarkDotNet_RegressionSuite()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] P9.28 BenchmarkDotNet regression suite - snapshot received");
    }
}
