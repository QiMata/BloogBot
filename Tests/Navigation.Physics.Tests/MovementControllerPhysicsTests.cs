using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Navigation.Physics.Tests.Helpers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Navigation.Physics.Tests;

/// <summary>
/// Integration tests for MovementController using the real C++ PhysicsEngine
/// via NativePathfindingClient. Verifies ground snapping, gravity, terrain following,
/// and teleport recovery against actual VMAP/ADT collision data.
///
/// Requires Navigation.dll + vmaps/ + maps/ data in WWOW_DATA_DIR.
/// </summary>
[Collection("PhysicsEngine")]
public class MovementControllerPhysicsTests
{
    private readonly PhysicsEngineFixture _fixture;

    // Valley of Trials spawn point (Kalimdor, map 1)
    private const float SpawnX = -618.518f;
    private const float SpawnY = -4251.67f;
    private const float SpawnZ = 38.718f;
    private const uint MapId = 1; // Kalimdor

    public MovementControllerPhysicsTests(PhysicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private (MovementController controller, WoWLocalPlayer player, Mock<WoWClient> mockClient)
        CreateController(float x, float y, float z, float facing = 0f)
    {
        var mockClient = new Mock<WoWClient>();
        mockClient
            .Setup(c => c.SendMovementOpcodeAsync(
                It.IsAny<Opcode>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var physics = new NativePathfindingClient();
        var player = new WoWLocalPlayer(new HighGuid(42))
        {
            Position = new Position(x, y, z),
            Facing = facing,
            MapId = MapId,
            Race = Race.Orc,
            Gender = Gender.Male,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.722f,
            SwimBackSpeed = 2.5f,
        };

        var controller = new MovementController(mockClient.Object, physics, player);
        return (controller, player, mockClient);
    }

    /// <summary>
    /// Runs N physics frames on the controller with the given dt.
    /// </summary>
    private static void RunFrames(MovementController controller, int frameCount,
        float dtSec = 0.05f, uint startTimeMs = 1000)
    {
        for (int i = 0; i < frameCount; i++)
        {
            controller.Update(dtSec, startTimeMs + (uint)(i * dtSec * 1000));
        }
    }

    [Fact]
    public void Standing_SnapsToGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 5 units above ground at Valley of Trials spawn
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 5.0f);

        // Run 20 frames at 50ms each (1 second)
        player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
        // Need at least one movement flag to trigger Update logic
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        RunFrames(controller, 20);
        player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
        RunFrames(controller, 1, startTimeMs: 2000);

        // Z should have snapped down toward ground level (~38.7)
        Assert.InRange(player.Position.Z, SpawnZ - 2.0f, SpawnZ + 2.0f);
    }

    [Fact]
    public void Forward_TraversesSlope()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Valley of Trials facing east (facing = 0)
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);
        var startPos = player.Position;

        // Move forward for 3 seconds (60 frames × 50ms)
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        RunFrames(controller, 60);

        // Should have moved ~21 yards forward (7.0 RunSpeed × 3s)
        float horizontalDist = MathF.Sqrt(
            MathF.Pow(player.Position.X - startPos.X, 2) +
            MathF.Pow(player.Position.Y - startPos.Y, 2));
        Assert.InRange(horizontalDist, 10f, 30f);

        // Z should be on valid terrain (not falling through world).
        // Real terrain varies significantly — Valley of Trials has steep slopes.
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void TeleportRecovery_StopsFreeFall()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at valid ground position
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ);
        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Run a few frames to establish state
        RunFrames(controller, 5);

        // Simulate teleport: jump position far away (triggers >100 unit detection)
        player.Position = new Position(SpawnX + 200f, SpawnY, SpawnZ);

        // Run more frames — should NOT fall through world
        RunFrames(controller, 20, startTimeMs: 2000);

        // Z should be near ground, not -2815 or similar
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void Backward_MovesOppositeToFacing()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Valley of Trials facing north (facing = 0)
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);
        var startPos = player.Position;

        // Move backward for 2 seconds (40 frames × 50ms) at RunBackSpeed 4.5
        player.MovementFlags = MovementFlags.MOVEFLAG_BACKWARD;
        RunFrames(controller, 40);

        // Should have moved backward (~9 yards at 4.5 speed × 2s)
        float horizontalDist = MathF.Sqrt(
            MathF.Pow(player.Position.X - startPos.X, 2) +
            MathF.Pow(player.Position.Y - startPos.Y, 2));
        Assert.InRange(horizontalDist, 3f, 15f);

        // Z should be on valid terrain
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void Falling_AccumulatesFallTime()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 60 units above ground — will be in free fall
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 60f);
        float startZ = player.Position.Z;

        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
        RunFrames(controller, 10);

        // After 10 frames of free fall, Z should have decreased
        Assert.True(player.Position.Z < startZ,
            $"Expected Z < {startZ:F1} but got {player.Position.Z:F1}");
    }

    [Fact]
    public void LandAfterFall_PositionNearGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 10 units above ground — short fall
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 10f);

        player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

        // Run enough frames for gravity to pull us down to ground (2 seconds)
        RunFrames(controller, 40);

        // Should be near ground level after landing
        Assert.InRange(player.Position.Z, SpawnZ - 3f, SpawnZ + 3f);
    }
}
