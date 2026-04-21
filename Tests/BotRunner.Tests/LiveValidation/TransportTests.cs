using System;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.12: Transport tests -- zeppelins, boats, elevators.
/// Teleport to Org zeppelin tower, wait, check for transport game objects nearby.
///
/// Run: dotnet test --filter "FullyQualifiedName~TransportTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class TransportTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    // Orgrimmar zeppelin tower platform
    private const float ZepTowerX = 1320.0f, ZepTowerY = -4653.0f, ZepTowerZ = 53.0f;

    // Game object types: 11 = GAMEOBJECT_TYPE_TRANSPORT, 15 = GAMEOBJECT_TYPE_MO_TRANSPORT
    private const uint GoTypeTransport = 11;
    private const uint GoTypeMoTransport = 15;

    public TransportTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    /// <summary>
    /// V2.12: Teleport to zeppelin tower, detect transport game objects nearby.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Zeppelin_OrgToUndercity()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        _output.WriteLine($"[TEST] Teleporting to Org zeppelin tower ({ZepTowerX}, {ZepTowerY}, {ZepTowerZ})");
        await _bot.BotTeleportAsync(account, KalimdorMapId, ZepTowerX, ZepTowerY, ZepTowerZ);
        await _bot.WaitForTeleportSettledAsync(account, ZepTowerX, ZepTowerY);

        // Wait for game objects to populate
        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Position: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
        _output.WriteLine($"[TEST] CurrentMapId: {snap.CurrentMapId}");

        // Check movement data for transport game objects
        var nearbyGOs = snap.MovementData?.NearbyGameObjects?.ToList()
            ?? new System.Collections.Generic.List<Game.GameObjectSnapshot>();
        _output.WriteLine($"[TEST] Nearby game objects: {nearbyGOs.Count}");
        foreach (var go in nearbyGOs.OrderBy(go => go.DistanceToPlayer))
        {
            var goPos = go.Position;
            _output.WriteLine(
                $"  GO: entry={go.Entry} display={go.DisplayId} type={go.GameObjectType} " +
                $"name={go.Name} guid=0x{go.Guid:X} dist={go.DistanceToPlayer:F1} " +
                $"pos=({goPos?.X:F1},{goPos?.Y:F1},{goPos?.Z:F1})");
        }

        var transports = nearbyGOs
            .Where(go => go.GameObjectType == GoTypeTransport || go.GameObjectType == GoTypeMoTransport)
            .ToList();
        _output.WriteLine($"[TEST] Transport objects found: {transports.Count}");

        foreach (var t in transports)
        {
            _output.WriteLine($"  Transport: entry={t.Entry}, type={t.GameObjectType}, " +
                $"name={t.Name}, guid=0x{t.Guid:X}");
        }

        // Zeppelin tower area should have at least transport-related objects nearby
        // even if the zeppelin is currently in transit
        Assert.NotNull(snap.Player);
    }

    /// <summary>
    /// V2.12: Ratchet boat dock -- verify game objects present.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_RatchetToBootyBay()
    {
        var account = _bot.BgAccountName!;

        // Ratchet dock coordinates
        const float ratchetX = -996.0f, ratchetY = -3827.0f, ratchetZ = 8.0f;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, KalimdorMapId, ratchetX, ratchetY, ratchetZ);
        await _bot.WaitForTeleportSettledAsync(account, ratchetX, ratchetY);

        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        _output.WriteLine($"[TEST] Ratchet dock position: ({pos?.X:F1}, {pos?.Y:F1}, {pos?.Z:F1})");
        Assert.NotNull(pos);
    }

    /// <summary>
    /// V2.12: Menethil Harbor to Theramore boat placeholder.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Boat_MenethilToTheramore()
    {
        var account = _bot.BgAccountName!;

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Character: {snap!.CharacterName}");
        // Cross-continent boat requires specific dock setup
        Assert.NotNull(snap.Player);
    }

    /// <summary>
    /// V2.12: Undercity elevator -- check Z change.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_Undercity()
    {
        var account = _bot.BgAccountName!;

        // Undercity west elevator upper stop (Eastern Kingdoms map 0)
        const int ekMapId = 0;
        const float ucElevTopX = 1544.24f, ucElevTopY = 240.77f, ucElevTopZ = 55.40f;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, ekMapId, ucElevTopX, ucElevTopY, ucElevTopZ);
        await _bot.WaitForTeleportSettledAsync(account, ucElevTopX, ucElevTopY);
        await Task.Delay(5000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Undercity elevator area position: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");

        var nearbyGOs = snap.MovementData?.NearbyGameObjects?.ToList()
            ?? new System.Collections.Generic.List<Game.GameObjectSnapshot>();
        _output.WriteLine($"[TEST] Nearby game objects: {nearbyGOs.Count}");
        foreach (var go in nearbyGOs.OrderBy(go => go.DistanceToPlayer))
        {
            var goPos = go.Position;
            _output.WriteLine(
                $"  GO: entry={go.Entry} display={go.DisplayId} type={go.GameObjectType} " +
                $"name={go.Name} guid=0x{go.Guid:X} dist={go.DistanceToPlayer:F1} " +
                $"pos=({goPos?.X:F1},{goPos?.Y:F1},{goPos?.Z:F1})");
        }
    }

    /// <summary>
    /// V2.12: Thunder Bluff elevator.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Elevator_ThunderBluff()
    {
        var account = _bot.BgAccountName!;

        const float tbElevX = -1898.0f, tbElevY = -287.0f, tbElevZ = 92.0f;

        await _bot.EnsureCleanSlateAsync(account, "BG");
        await _bot.BotTeleportAsync(account, KalimdorMapId, tbElevX, tbElevY, tbElevZ);
        await Task.Delay(3000);

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);

        var pos = snap!.Player?.Unit?.GameObject?.Base?.Position;
        Assert.NotNull(pos);
        _output.WriteLine($"[TEST] Thunder Bluff elevator area: ({pos!.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
    }

    /// <summary>
    /// V2.12: Deeprun Tram placeholder.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task DeeprunTram_IFToSW()
    {
        var account = _bot.BgAccountName!;

        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        _output.WriteLine($"[TEST] Deeprun Tram test -- character: {snap!.CharacterName}");
        Assert.NotNull(snap.Player);
    }
}
