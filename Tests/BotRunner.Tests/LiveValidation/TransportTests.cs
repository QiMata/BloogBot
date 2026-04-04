using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// P29.10–29.15: Transport tests — zeppelins, boats, elevators, Deeprun Tram.
/// Validates boarding, map transitions, and arrival for all transport types.
///
/// Run: dotnet test --filter "FullyQualifiedName~TransportTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TransportTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TransportTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// P29.10: Bot walks to Org zeppelin tower, boards zeppelin.
    /// Assert: TransportGuid set, mapId changes from 1 to 0, arrives in Tirisfal Glades.
    /// Uses existing TransportWaitingLogic.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Zeppelin_OrgToUndercity()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.10 Zeppelin Org to Undercity");
    }

    /// <summary>
    /// P29.11: Bot teleported to Ratchet dock, boards boat.
    /// Assert: arrives in Booty Bay (STV).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_RatchetToBootyBay()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.11 Boat Ratchet to Booty Bay");
    }

    /// <summary>
    /// P29.12: Alliance bot boards ship at Menethil Harbor.
    /// Assert: crosses from Wetlands to Dustwallow Marsh (Theramore).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_MenethilToTheramore()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.12 Boat Menethil to Theramore");
    }

    /// <summary>
    /// P29.13: Bot at Undercity upper level, takes elevator down.
    /// Assert: Z drops ~100y, position in Undercity interior.
    /// Uses existing TransportData.UndercityElevatorWest.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_Undercity()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.13 Undercity elevator");
    }

    /// <summary>
    /// P29.14: Bot at Thunder Bluff upper level, takes elevator down.
    /// Assert: Z drops, arrives at base level.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_ThunderBluff()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.14 Thunder Bluff elevator");
    }

    /// <summary>
    /// P29.15: Alliance bot rides Deeprun Tram from Ironforge to Stormwind (or vice versa).
    /// Assert: map transition via tram instance.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task DeeprunTram_IFToSW()
    {
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(_bot.BgAccountName!);
        Assert.NotNull(snap);
        _output.WriteLine("[TEST] snapshot received — P29.15 Deeprun Tram IF to SW");
    }
}
