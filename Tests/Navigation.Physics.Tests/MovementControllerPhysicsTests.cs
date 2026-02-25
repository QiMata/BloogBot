using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Navigation.Physics.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    // Valley of Trials spawn point (Kalimdor, map 1)
    private const float SpawnX = -618.518f;
    private const float SpawnY = -4251.67f;
    private const float SpawnZ = 38.718f;
    private const uint MapId = 1; // Kalimdor

    public MovementControllerPhysicsTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private readonly record struct FrameSnapshot(
        int Frame,
        uint TimeMs,
        float X,
        float Y,
        float Z,
        float DeltaZ,
        float GroundZ,
        float GroundGap,
        MovementFlags Flags);

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
    /// Runs N physics frames and captures per-frame diagnostics for calibration.
    /// </summary>
    private static List<FrameSnapshot> RunFramesWithTrace(
        MovementController controller,
        WoWLocalPlayer player,
        int frameCount,
        float dtSec = 0.05f,
        uint startTimeMs = 1000,
        MovementFlags? forceFlagsEachFrame = null)
    {
        var frames = new List<FrameSnapshot>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            if (forceFlagsEachFrame.HasValue)
            {
                player.MovementFlags = forceFlagsEachFrame.Value;
            }

            float prevZ = player.Position.Z;
            uint timeMs = startTimeMs + (uint)MathF.Round(i * dtSec * 1000f);
            controller.Update(dtSec, timeMs);

            float groundZ = ProbeGroundZ(player.MapId, player.Position.X, player.Position.Y, player.Position.Z);
            float groundGap = float.IsNaN(groundZ) ? float.NaN : player.Position.Z - groundZ;

            frames.Add(new FrameSnapshot(
                i,
                timeMs,
                player.Position.X,
                player.Position.Y,
                player.Position.Z,
                player.Position.Z - prevZ,
                groundZ,
                groundGap,
                player.MovementFlags));
        }

        return frames;
    }

    private static float ProbeGroundZ(uint mapId, float x, float y, float z)
    {
        const float InvalidGround = -50000f;

        float[] probeHeights = [z + 2f, z + 12f, z + 30f];
        float[] searchDistances = [8f, 24f, 48f];

        for (int i = 0; i < probeHeights.Length; i++)
        {
            float groundZ = NavigationInterop.GetGroundZ(mapId, x, y, probeHeights[i], searchDistances[i]);
            if (groundZ > InvalidGround)
            {
                return groundZ;
            }
        }

        return float.NaN;
    }

    private void WriteFrameTrace(string scenario, IReadOnlyList<FrameSnapshot> frames)
    {
        _output.WriteLine($"=== {scenario}: {frames.Count} frames ===");
        foreach (var frame in frames)
        {
            string groundText = float.IsNaN(frame.GroundZ)
                ? "n/a"
                : frame.GroundZ.ToString("F3", CultureInfo.InvariantCulture);
            string gapText = float.IsNaN(frame.GroundGap)
                ? "n/a"
                : frame.GroundGap.ToString("F3", CultureInfo.InvariantCulture);

            _output.WriteLine(
                $"  f={frame.Frame,3} t={frame.TimeMs,5} " +
                $"pos=({frame.X:F3},{frame.Y:F3},{frame.Z:F3}) " +
                $"dZ={frame.DeltaZ.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),7} " +
                $"ground={groundText,8} gap={gapText,8} flags=0x{(uint)frame.Flags:X8}");
        }
    }

    private static float MinAbsGroundGap(IReadOnlyList<FrameSnapshot> frames)
    {
        float minGap = float.PositiveInfinity;
        foreach (var frame in frames)
        {
            if (float.IsNaN(frame.GroundGap))
            {
                continue;
            }

            float absGap = MathF.Abs(frame.GroundGap);
            if (absGap < minGap)
            {
                minGap = absGap;
            }
        }

        return minGap;
    }

    private static string Summarize(IReadOnlyList<FrameSnapshot> frames)
    {
        if (frames.Count == 0)
        {
            return "no frames captured";
        }

        float minGap = MinAbsGroundGap(frames);
        string minGapText = float.IsPositiveInfinity(minGap) ? "n/a" : minGap.ToString("F3", CultureInfo.InvariantCulture);
        var first = frames[0];
        var last = frames[^1];

        return $"firstZ={first.Z:F3}, lastZ={last.Z:F3}, minAbsGap={minGapText}";
    }

    [Fact]
    public void Standing_SnapsToGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 5 units above ground at Valley of Trials spawn
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 5.0f);
        float startZ = player.Position.Z;

        // Hold movement intent so Update() continues stepping physics each frame.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 20,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Standing_SnapsToGround), trace);

        float minGap = MinAbsGroundGap(trace);
        Assert.True(player.Position.Z < startZ - 1.0f,
            $"Expected a visible downward snap from {startZ:F3} but got {player.Position.Z:F3}. {Summarize(trace)}");

        Assert.True(minGap <= 1.5f,
            $"Expected ground contact gap <= 1.5y but min was {minGap:F3}. {Summarize(trace)}");
    }

    [Fact]
    public void Forward_TraversesSlope()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at Valley of Trials facing east (facing = 0)
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ, facing: 0f);
        var startPos = player.Position;

        // Move forward for 3 seconds (60 frames x 50ms)
        var _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 60,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        // Should have moved ~21 yards forward (7.0 RunSpeed x 3s)
        float horizontalDist = MathF.Sqrt(
            MathF.Pow(player.Position.X - startPos.X, 2) +
            MathF.Pow(player.Position.Y - startPos.Y, 2));
        Assert.InRange(horizontalDist, 10f, 30f);

        // Z should be on valid terrain (not falling through world).
        Assert.True(player.Position.Z > -50f,
            $"Player fell through world: Z={player.Position.Z:F1}");
    }

    [Fact]
    public void TeleportRecovery_StopsFreeFall()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start at valid ground position
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ);

        // Run a few frames to establish state
        _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 5,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);

        // Teleport to a location known to have ground, but place player well above it.
        float teleportX = SpawnX + 200f;
        float teleportY = SpawnY;
        float teleportGroundZ = ProbeGroundZ(MapId, teleportX, teleportY, SpawnZ + 40f);
        if (float.IsNaN(teleportGroundZ))
        {
            teleportGroundZ = SpawnZ;
        }
        float teleportZ = teleportGroundZ + 15f;

        // Simulate teleport: jump position far away (triggers >100 unit detection)
        player.Position = new Position(teleportX, teleportY, teleportZ);
        _output.WriteLine($"Teleport target: ({teleportX:F3}, {teleportY:F3}, {teleportZ:F3}), probedGround={teleportGroundZ:F3}");

        // Continue holding movement intent so post-reset frames continue stepping.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 40,
            startTimeMs: 2000,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(TeleportRecovery_StopsFreeFall), trace);

        int descentEndFrame = Math.Min(trace.Count - 1, 10);
        float earlyDrop = trace[0].Z - trace[descentEndFrame].Z;
        float minGap = MinAbsGroundGap(trace);

        Assert.True(earlyDrop > 0.75f,
            $"Expected post-teleport descent by frame {descentEndFrame}, drop={earlyDrop:F3}. {Summarize(trace)}");

        Assert.True(minGap <= 2.0f,
            $"Expected landing contact gap <= 2.0y after teleport, min was {minGap:F3}. {Summarize(trace)}");

        // Guard against through-world failures.
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

        // Move backward for 2 seconds (40 frames x 50ms) at RunBackSpeed 4.5
        var _ = RunFramesWithTrace(
            controller,
            player,
            frameCount: 40,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_BACKWARD);

        // Should have moved backward (~9 yards at 4.5 speed x 2s)
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

        // Start 60 units above ground - will be in free fall
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 60f);
        float startZ = player.Position.Z;

        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 10,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(Falling_AccumulatesFallTime), trace);

        // After 10 frames of free fall, Z should have decreased
        Assert.True(player.Position.Z < startZ,
            $"Expected Z < {startZ:F1} but got {player.Position.Z:F1}");
    }

    [Fact]
    public void LandAfterFall_PositionNearGround()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        // Start 10 units above ground - short fall
        var (controller, player, _) = CreateController(SpawnX, SpawnY, SpawnZ + 10f);
        float startZ = player.Position.Z;

        // Run enough frames for gravity to pull us down to local ground.
        var trace = RunFramesWithTrace(
            controller,
            player,
            frameCount: 40,
            forceFlagsEachFrame: MovementFlags.MOVEFLAG_FORWARD);
        WriteFrameTrace(nameof(LandAfterFall_PositionNearGround), trace);

        float drop = startZ - player.Position.Z;
        float minGap = MinAbsGroundGap(trace);

        Assert.True(drop > 3f,
            $"Expected at least 3y descent from {startZ:F3}, actual drop={drop:F3}. {Summarize(trace)}");
        Assert.True(minGap <= 1.75f,
            $"Expected to settle near local ground (gap <= 1.75y), min gap was {minGap:F3}. {Summarize(trace)}");
    }
}
