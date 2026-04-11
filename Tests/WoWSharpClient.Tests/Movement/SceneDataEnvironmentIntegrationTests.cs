using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using Xunit;
using Xunit.Abstractions;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// Focused live verification for scene-data-backed indoor/outdoor environment flags.
/// These tests exercise the local MovementController + SceneDataService path only.
/// </summary>
public sealed class SceneDataEnvironmentIntegrationTests(ITestOutputHelper output) : IDisposable
{
    private const string SceneDataHost = "127.0.0.1";
    private const int SceneDataPort = 5003;

    private static readonly ProbeLocation OutdoorLocation = new(
        MapId: 1,
        X: -618.518f,
        Y: -4251.67f,
        Z: 38.718f,
        Label: "Valley of Trials");

    private static readonly ProbeLocation IndoorLocation = new(
        MapId: 389,
        X: 3f,
        Y: -11f,
        Z: -18f,
        Label: "Ragefire Chasm interior");

    private readonly Mock<WoWClient> _client = new();
    private readonly WoWLocalPlayer _player = new(new HighGuid(42))
    {
        Position = new Position(OutdoorLocation.X, OutdoorLocation.Y, OutdoorLocation.Z),
        Facing = 0f,
        WalkSpeed = 2.5f,
        RunSpeed = 7.0f,
        RunBackSpeed = 4.5f,
        SwimSpeed = 4.722f,
        SwimBackSpeed = 2.5f,
        MapId = OutdoorLocation.MapId,
        Race = Race.Orc,
        Gender = Gender.Male,
    };

    public void Dispose()
    {
        SceneDataClient.TestEnsureSceneDataAroundOverride = null;
        SceneDataClient.TestSendTileRequestOverride = null;
        SceneDataClient.TestInjectOverride = null;
        SceneDataClient.TestUtcNowOverride = null;
        NativeLocalPhysics.TestStepOverride = null;
        NativeLocalPhysics.TestClearSceneCacheOverride = null;
        NativeLocalPhysics.TestPreloadMapOverride = null;
        NativeLocalPhysics.TestSetDataDirectoryOverride = null;
        NativeLocalPhysics.TestResolveDataDirectoryOverride = null;

        NativeLocalPhysics.ClearSceneCache(OutdoorLocation.MapId);
        NativeLocalPhysics.ClearSceneCache(IndoorLocation.MapId);
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_DurotarSafeZone_ReportsOutdoorEnvironment()
    {
        var result = ProbeEnvironment(OutdoorLocation);

        Assert.False(result.Flags.IsIndoors());
        Assert.True(result.Flags.AllowsMountByEnvironment());
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public void LiveService_RagefireChasm_ReportsIndoorEnvironment()
    {
        var result = ProbeEnvironment(IndoorLocation);

        Assert.True(result.Flags.IsIndoors());
        Assert.False(result.Flags.AllowsMountByEnvironment());
    }

    private ProbeResult ProbeEnvironment(ProbeLocation location)
    {
        NativeLocalPhysics.ClearSceneCache(location.MapId);

        _client
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sceneClient = new SceneDataClient(SceneDataHost, SceneDataPort, NullLogger.Instance);
        var preloaded = sceneClient.EnsureSceneDataAround(location.MapId, location.X, location.Y);
        Skip.IfNot(preloaded, $"SceneDataService did not return scene data for {location.Label}.");

        _player.MapId = location.MapId;
        _player.Position = new Position(location.X, location.Y, location.Z);
        _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
        _player.TransportGuid = 0;
        _player.Transport = null;

        var controller = new MovementController(_client.Object, _player, sceneClient);
        controller.Reset(teleportDestZ: location.Z);

        var gameTimeMs = 1000u;
        for (var frame = 0; frame < 90; frame++)
        {
            controller.Update(1f / 30f, gameTimeMs);
            gameTimeMs += 33u;

            if (!controller.NeedsGroundSnap)
                break;
        }

        var flags = controller.LastEnvironmentFlags;
        output.WriteLine(
            $"[{location.Label}] map={location.MapId} pos=({_player.Position.X:F1},{_player.Position.Y:F1},{_player.Position.Z:F1}) " +
            $"flags=0x{(uint)flags:X8} indoors={flags.IsIndoors()} mountAllowed={flags.AllowsMountByEnvironment()}");

        return new ProbeResult(location, flags, _player.Position);
    }

    private readonly record struct ProbeLocation(uint MapId, float X, float Y, float Z, string Label);

    private readonly record struct ProbeResult(ProbeLocation Location, SceneEnvironmentFlags Flags, Position Position);
}
