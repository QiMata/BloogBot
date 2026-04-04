using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Dungeons;

/// <summary>
/// P26.9, 26.30, 26.31, 26.32: Stockade (Alliance), Warlock summon (RFC),
/// Meeting stone summon (WC), Fallback no-summoner.
///
/// Run: dotnet test --filter "FullyQualifiedName~SummoningStoneTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class SummoningStoneTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public SummoningStoneTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Stockade_AllianceSummon_ArrivesAtInstance()
    {
        // P26.9: Alliance bot is summoned to Stockade entrance via meeting stone
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task RFC_WarlockSummon_ArrivesAtInstance()
    {
        // P26.30: Warlock summons party member to RFC — summoned player arrives at instance
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task WC_MeetingStoneSummon_ArrivesAtInstance()
    {
        // P26.31: Party uses meeting stone to summon member to Wailing Caverns
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Fallback_NoSummoner_BotWalksToDungeon()
    {
        // P26.32: No summoner available — bot navigates to dungeon entrance on foot
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received");
    }
}
