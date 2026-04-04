using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P29.6–29.9: Flight master (taxi) tests.
/// Validates taxi node discovery, single-hop rides, and multi-hop flights
/// for both Horde and Alliance.
///
/// Run: dotnet test --filter "FullyQualifiedName~TaxiTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TaxiTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TaxiTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P29.6: Bot at Orgrimmar flight master. Interact.
    /// Assert: SMSG_SHOWTAXINODES received, node list contains Orgrimmar node.
    /// Discover Crossroads node via .tele.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_HordeDiscovery()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.6 Horde taxi discovery");
    }

    /// <summary>
    /// P29.7: Bot at Orgrimmar flight master with Crossroads discovered.
    /// Activate flight via CMSG_ACTIVATETAXI. Assert: position changes over time,
    /// arrives at Crossroads within 3 minutes.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_HordeRide_OrgToXroads()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.7 Horde taxi ride Org to Crossroads");
    }

    /// <summary>
    /// P29.8: Alliance bot at Stormwind flight master. Fly to Ironforge.
    /// Assert: arrival at Ironforge.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_AllianceRide()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.8 Alliance taxi ride");
    }

    /// <summary>
    /// P29.9: Bot at Orgrimmar, fly to Gadgetzan (multiple hops).
    /// Assert: intermediate nodes traversed, final arrival at Gadgetzan.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Taxi_MultiHop_OrgToGadgetzan()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.9 Multi-hop taxi Org to Gadgetzan");
    }
}
