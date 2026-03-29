using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PathfindingService;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using Xunit;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Tests the full MovementController -> PathfindingClient -> TCP -> PathfindingSocketServer -> C++ -> back pipeline.
/// Verifies that the IPC round-trip preserves physics correctness on Valley of Trials terrain.
/// </summary>
[Collection("PhysicsEngine")]
public sealed class MovementControllerIpcParityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly PathfindingSocketServer _server;
    private readonly PathfindingClient _client;

    public MovementControllerIpcParityTests(ITestOutputHelper output)
    {
        _output = output;
        var port = GetFreePort();
        _server = new PathfindingSocketServer("127.0.0.1", port,
            NullLoggerFactory.Instance.CreateLogger("PathfindingSocketServer"));
        _server.InitializeNavigation();
        _client = new PathfindingClient("127.0.0.1", port,
            NullLoggerFactory.Instance.CreateLogger("PathfindingClient"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public void FlatPath_StaysGrounded() => RunTest("FlatPath", -260, -4350, 57, -230, -4310, 120, 0);

    [Fact]
    public void SteepDescent_StaysGrounded() => RunTest("SteepDescent", -224, -4310, 65, -310, -4410, 120, 0);

    [Fact]
    public void ReverseHill_StaysGrounded()
    {
        float groundZ = NavigationInterop.GetGroundZ(1, -240, -4320, 70, 10);
        float startZ = groundZ > -50000 ? groundZ : 62;
        _output.WriteLine($"ReverseHill start groundZ={groundZ:F2}, using startZ={startZ:F2}");
        RunTest("ReverseHill", -240, -4320, startZ, -270, -4380, 120, 0);
    }

    [Fact]
    public void HillPath_StaysGrounded()
    {
        // Query actual terrain Z at start position instead of hardcoding
        float groundZ = NavigationInterop.GetGroundZ(1, -310, -4410, 50, 10);
        float startZ = groundZ > -50000 ? groundZ : 48;
        _output.WriteLine($"HillPath start groundZ={groundZ:F2}, using startZ={startZ:F2}");
        RunTest("HillPath", -310, -4410, startZ, -260, -4350, 120, 3);
    }

    [Fact]
    public void SteepClimb_StaysGrounded()
    {
        float groundZ = NavigationInterop.GetGroundZ(1, -310, -4410, 50, 10);
        float startZ = groundZ > -50000 ? groundZ : 48;
        _output.WriteLine($"SteepClimb start groundZ={groundZ:F2}, using startZ={startZ:F2}");
        RunTest("SteepClimb", -310, -4410, startZ, -224, -4310, 120, 3);
    }

    private void RunTest(string name, float sx, float sy, float sz, float tx, float ty, int frames, int maxAirborne)
    {
        var mockClient = new Mock<WoWClient>();
        mockClient.Setup(c => c.SendMovementOpcodeAsync(
            It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(sx, sy, sz),
            Facing = MathF.Atan2(ty - sy, tx - sx),
            MapId = 1, Race = Race.Orc, Gender = Gender.Male,
            WalkSpeed = 2.5f, RunSpeed = 7.0f, RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f, SwimBackSpeed = 2.5f,
        };

        var controller = new MovementController(mockClient.Object, _client, player);
        controller.SetPath([new Position(sx, sy, sz), new Position(tx, ty, sz)]);

        int airborne = 0;
        float totalDist = 0, prevX = sx, prevY = sy;

        for (int i = 0; i < frames; i++)
        {
            var phys = player.MovementFlags & (MovementFlags.MOVEFLAG_JUMPING | MovementFlags.MOVEFLAG_SWIMMING |
                MovementFlags.MOVEFLAG_FLYING | MovementFlags.MOVEFLAG_LEVITATING | MovementFlags.MOVEFLAG_FALLINGFAR);
            player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | phys;

            controller.Update(0.05f, (uint)(1000 + i * 50));

            bool air = (player.MovementFlags & (MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) != 0;
            if (air) airborne++;

            float dx = player.Position.X - prevX, dy = player.Position.Y - prevY;
            totalDist += MathF.Sqrt(dx * dx + dy * dy);
            prevX = player.Position.X; prevY = player.Position.Y;

            if (i < 3 || air)
                _output.WriteLine($"  f={i,3} Z={player.Position.Z:F2} flags=0x{(uint)player.MovementFlags:X}");
        }

        float speed = totalDist / (frames * 0.05f);
        _output.WriteLine($"[{name}] Airborne={airborne}/{frames} Speed={speed:F2}y/s");

        Assert.True(airborne <= maxAirborne,
            $"[{name}] IPC FALLINGFAR oscillation: {airborne}/{frames} (max {maxAirborne}). Speed={speed:F2}");
        Assert.True(speed >= 3.0f,
            $"[{name}] IPC speed too low: {speed:F2} (min 3.0). Airborne={airborne}/{frames}");
    }

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start(); int p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p;
    }
}
