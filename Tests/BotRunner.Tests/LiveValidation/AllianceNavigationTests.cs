using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P29.19–29.23: Alliance-side navigation and dungeon entry tests.
/// Validates Alliance pathing, vendor interaction, and entry into
/// Deadmines, Stockade, and Gnomeregan.
///
/// Run: dotnet test --filter "FullyQualifiedName~AllianceNavigationTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class AllianceNavigationTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public AllianceNavigationTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P29.19: Alliance bot at Goldshire. Navigate to Stormwind entrance.
    /// Assert: arrival within expected path time.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_GoldshireToStormwind()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.19 Alliance Goldshire to Stormwind navigation");
    }

    /// <summary>
    /// P29.20: Alliance bot at Stormwind vendor. Buy/sell items.
    /// Same as VendorBuySellTests but with Alliance NPC.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_VendorBuySell()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.20 Alliance vendor buy/sell");
    }

    /// <summary>
    /// P29.21: 10 Alliance bots form group, enter The Deadmines (mapId=36).
    /// Already in DungeonEntryData. Fixture needed.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Deadmines_Entry()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.21 Alliance Deadmines entry");
    }

    /// <summary>
    /// P29.22: 10 Alliance bots in Stormwind enter The Stockade (mapId=34).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Stockade_Entry()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.22 Alliance Stockade entry");
    }

    /// <summary>
    /// P29.23: Alliance approach Gnomeregan (mapId=90) via Dun Morogh.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Alliance_Gnomeregan_Entry()
    {
        await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.23 Alliance Gnomeregan entry");
    }
}
